//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class RenesasRZG_GPIO : BaseGPIOPort, IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public RenesasRZG_GPIO(Machine machine) : base(machine, NrOfPins)
        {
            // Prepare gpio indices offsets per port for easy lookup
            gpioOffsetPerPort[0] = 0;
            for(var portIdx = 1; portIdx < NrOfPorts; portIdx++)
            {
                gpioOffsetPerPort[portIdx] = gpioOffsetPerPort[portIdx - 1] + GetNrOfGPIOPinsForPort(portIdx - 1);
            }

            DefineRegisters();
        }

        // Every register, allows all access widths less than or equal to it's own width.
        public byte ReadByte(long offset)
        {
            // We align offset to registers width
            if(doubleWordRegisters.HasRegisterAtOffset(offset & ~0x3))
            {
                return this.ReadByteUsingDoubleWord(offset);
            }
            else if(wordRegisters.HasRegisterAtOffset(offset & ~0x1))
            {
                return this.ReadByteUsingWord(offset);
            }
            return byteRegisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            // We align offset to registers width
            if(doubleWordRegisters.HasRegisterAtOffset(offset & ~0x3))
            {
                this.WriteByteUsingDoubleWord(offset, value);
                return;
            }
            if(wordRegisters.HasRegisterAtOffset(offset & ~0x1))
            {
                this.WriteByteUsingWord(offset, value);
                return;
            }
            byteRegisters.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            // We align offset to registers width
            if(doubleWordRegisters.HasRegisterAtOffset(offset & ~0x3))
            {
                return this.ReadWordUsingDoubleWord(offset);
            }
            return wordRegisters.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            // We align offset to registers width
            if(doubleWordRegisters.HasRegisterAtOffset(offset & ~0x3))
            {
                this.WriteWordUsingDoubleWord(offset, value);
                return;
            }
            wordRegisters.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return doubleWordRegisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            doubleWordRegisters.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            byteRegisters.Reset();
            wordRegisters.Reset();
            doubleWordRegisters.Reset();
            Array.Clear(output, 0, output.Length);
            Array.Clear(portMode, 0, portMode.Length);
            Array.Clear(interruptEnabled, 0, interruptEnabled.Length);
            Array.Clear(pinFunction, 0, pinFunction.Length);
            Array.Clear(pinFunctionEnabled, 0, pinFunctionEnabled.Length);
            foreach (var irq in functionInterrupts)
            {
                irq.Unset();
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            UpdateGPIO();
        }

        public long Size => 0x10000;

        public GPIO IRQ0 => functionInterrupts[0];
        public GPIO IRQ1 => functionInterrupts[1];
        public GPIO IRQ2 => functionInterrupts[2];
        public GPIO IRQ3 => functionInterrupts[3];
        public GPIO IRQ4 => functionInterrupts[4];
        public GPIO IRQ5 => functionInterrupts[5];
        public GPIO IRQ6 => functionInterrupts[6];
        public GPIO IRQ7 => functionInterrupts[7];

        private void DefineRegisters()
        {
            var byteRegistersMap = new Dictionary <long, ByteRegister>();
            var wordRegistersMap = new Dictionary <long, WordRegister>();
            var doubleWordRegistersMap = new Dictionary <long, DoubleWordRegister>();

            // Interrupt enable registers are not distributed uniformly across it's region
            // we keep track of their current offset in this variable instead of calculating it every time.
            var interruptEnableControlOffset = (long)Registers.InterruptEnableControl;

            // Although different ports have different number of GPIO pins, with maximum of 5 usable pins,
            // they have the same registers structure.
            for(var portIdx = 0; portIdx < NrOfPorts; portIdx++)
            {
                var portOffset = (long)Registers.Port + portIdx;
                byteRegistersMap[portOffset] = new ByteRegister(this)
                    .WithFlags(0, NrOfPinsInPortRegister,
                        writeCallback: CreateGPIOOutputWriteCallback(portIdx),
                        valueProviderCallback: CreateGPIOOutputValueProviderCallback(portIdx),
                        name: "P")
                    .WithWriteCallback((_, __) => UpdateGPIO());

                var portModeOffset = (long)Registers.PortMode + portIdx * 0x2;
                wordRegistersMap[portModeOffset] = new WordRegister(this)
                    .WithEnumFields<WordRegister, PortMode>(0, 2, 5,
                        valueProviderCallback: CreatePortModeValueProviderCallback(portIdx),
                        writeCallback: CreatePortModeWriteCallback(portIdx),
                        name: "PM")
                    .WithWriteCallback((_, __) => UpdateGPIO());

                var portModeControlOffset = (long)Registers.PortModeControl + portIdx;
                byteRegistersMap[portModeControlOffset] = new ByteRegister(this)
                    .WithFlags(0, 5,
                        writeCallback: CreatePortModeControlWriteCallback(portIdx),
                        valueProviderCallback: CreatePortModeControlValueProviderCallback(portIdx),
                        name: "PMC")
                    .WithWriteCallback((_, __) => UpdateGPIO());

                var portFunctionControlOffset = (long)Registers.PortFunctionControl + portIdx * 0x4;
                doubleWordRegistersMap[portFunctionControlOffset] = new DoubleWordRegister(this)
                    .WithValueField(0, 3,
                        writeCallback: CreatePortFunctionControlWriteCallback(portIdx, 0),
                        valueProviderCallback: CreatePortFunctionControlValueProviderCallback(portIdx, 0),
                        name: "PFC0")
                    .WithReservedBits(3, 1)
                    .WithValueField(4, 3,
                        writeCallback: CreatePortFunctionControlWriteCallback(portIdx, 1),
                        valueProviderCallback: CreatePortFunctionControlValueProviderCallback(portIdx, 1),
                        name: "PFC1")
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 3,
                        writeCallback: CreatePortFunctionControlWriteCallback(portIdx, 2),
                        valueProviderCallback: CreatePortFunctionControlValueProviderCallback(portIdx, 2),
                        name: "PFC2")
                    .WithReservedBits(11, 1)
                    .WithValueField(12, 3,
                        writeCallback: CreatePortFunctionControlWriteCallback(portIdx, 3),
                        valueProviderCallback: CreatePortFunctionControlValueProviderCallback(portIdx, 3),
                        name: "PFC3")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 3,
                        writeCallback: CreatePortFunctionControlWriteCallback(portIdx, 4),
                        valueProviderCallback: CreatePortFunctionControlValueProviderCallback(portIdx, 4),
                        name: "PFC4")
                    .WithReservedBits(19, 13)
                    .WithWriteCallback((_, __) => UpdateGPIO());

                var portInputOffset = (long)Registers.PortInput + portIdx;
                byteRegistersMap[portInputOffset] = new ByteRegister(this)
                    .WithFlags(0, NrOfPinsInPortRegister, FieldMode.Read,
                        valueProviderCallback: CreateGPIOInputValueProviderCallback(portIdx),
                        name: "PIN");

                doubleWordRegistersMap[interruptEnableControlOffset] = new DoubleWordRegister(this)
                    .WithFlag(0,
                        writeCallback: CreateInterruptEnableControlWriteCallback(portIdx, 0),
                        valueProviderCallback: CreateInterruptEnableControlValueProviderCallback(portIdx, 0),
                        name: "ISEL0")
                    .WithReservedBits(1, 7)
                    .WithFlag(8,
                        writeCallback: CreateInterruptEnableControlWriteCallback(portIdx, 1),
                        valueProviderCallback: CreateInterruptEnableControlValueProviderCallback(portIdx, 1),
                        name: "ISEL1")
                    .WithReservedBits(9, 7)
                    .WithFlag(16,
                        writeCallback: CreateInterruptEnableControlWriteCallback(portIdx, 2),
                        valueProviderCallback: CreateInterruptEnableControlValueProviderCallback(portIdx, 2),
                        name: "ISEL2")
                    .WithReservedBits(17, 7)
                    .WithFlag(24,
                        writeCallback: CreateInterruptEnableControlWriteCallback(portIdx, 3),
                        valueProviderCallback: CreateInterruptEnableControlValueProviderCallback(portIdx, 3),
                        name: "ISEL3")
                    .WithReservedBits(25, 7)
                    .WithWriteCallback((_, __) => UpdateGPIO());

                interruptEnableControlOffset += 0x4;

                if(GetNrOfGPIOPinsForPort(portIdx) > 4)
                {
                    // We have 5 pins per port at most, so we only define here one flag
                    doubleWordRegistersMap[interruptEnableControlOffset] = new DoubleWordRegister(this)
                        .WithFlag(0,
                            writeCallback: CreateInterruptEnableControlWriteCallback(portIdx, 4),
                            valueProviderCallback: CreateInterruptEnableControlValueProviderCallback(portIdx, 4),
                            name: "ISEL0")
                        .WithReservedBits(1, 7)
                        .WithTaggedFlag("ISEL1", 8)
                        .WithReservedBits(9, 7)
                        .WithTaggedFlag("ISEL2", 16)
                        .WithReservedBits(17, 7)
                        .WithTaggedFlag("ISEL3", 24)
                        .WithReservedBits(25, 7)
                        .WithWriteCallback((_, __) => UpdateGPIO());

                    interruptEnableControlOffset += 0x4;
                }
            }

            // Some registers are not uniformly distributed over their regions.
            // We define them in separate loops.
            for(var registerIdx = 0; registerIdx < NrOfDrivingAbilityControlRegisters; registerIdx++)
            {
                var drivingAbilityControlOffset = (long)Registers.DrivingAbilityControl + registerIdx * 0x4;
                doubleWordRegistersMap[drivingAbilityControlOffset] = new DoubleWordRegister(this)
                    .WithTag("IOLH0", 0, 2)
                    .WithReservedBits(2, 6)
                    .WithTag("IOLH1", 8, 2)
                    .WithReservedBits(10, 6)
                    .WithTag("IOLH2", 16, 2)
                    .WithReservedBits(18, 6)
                    .WithTag("IOLH3", 24, 2)
                    .WithReservedBits(26, 6);
            }

            for(var registerIdx = 0; registerIdx < NrOfSlewRateSwitchingRegisters; registerIdx++)
            {
                var slewRateSwitchingOffset = (long)Registers.SlewRateSwitching + registerIdx * 0x4;
                doubleWordRegistersMap[slewRateSwitchingOffset] = new DoubleWordRegister(this)
                    .WithTag("SR0", 0, 1)
                    .WithReservedBits(1, 7)
                    .WithTag("SR1", 8, 1)
                    .WithReservedBits(9, 7)
                    .WithTag("SR2", 16, 1)
                    .WithReservedBits(17, 7)
                    .WithTag("SR3", 24, 1)
                    .WithReservedBits(25, 7);
            }

            for(var registerIdx = 0; registerIdx < NrOfPullUpPullDownSwitchingRegisters; registerIdx++)
            {
                var pullUpPullDownSwitchingOffset = (long)Registers.PullUpPullDownSwitching + registerIdx * 0x4;
                doubleWordRegistersMap[pullUpPullDownSwitchingOffset] = new DoubleWordRegister(this)
                    .WithTag("PUPD0", 0, 2)
                    .WithReservedBits(2, 6)
                    .WithTag("PUPD1", 8, 2)
                    .WithReservedBits(10, 6)
                    .WithTag("PUPD2", 16, 2)
                    .WithReservedBits(18, 6)
                    .WithTag("PUPD3", 24, 2)
                    .WithReservedBits(26, 6);
            }

            for(var registerIdx = 0; registerIdx < NrOfDigitalNoiseFilterRegisters; registerIdx++)
            {
                var digitalNoiseFilterSwitchingOffset = (long)Registers.DigitalNoiseFilterSwitching + registerIdx * 0x4;
                doubleWordRegistersMap[digitalNoiseFilterSwitchingOffset] = new DoubleWordRegister(this)
                    .WithTag("FILON0", 0, 1)
                    .WithReservedBits(1, 7)
                    .WithTag("FILON1", 8, 1)
                    .WithReservedBits(9, 7)
                    .WithTag("FILON2", 16, 1)
                    .WithReservedBits(17, 7)
                    .WithTag("FILON3", 24, 1)
                    .WithReservedBits(25, 7);

                var digitalNoiseFilterNumberOffset = (long)Registers.DigitalNoiseFilterNumber + registerIdx * 0x4;
                doubleWordRegistersMap[digitalNoiseFilterNumberOffset] = new DoubleWordRegister(this)
                    .WithTag("FILNUM0", 0, 2)
                    .WithReservedBits(2, 6)
                    .WithTag("FILNUM1", 8, 2)
                    .WithReservedBits(10, 6)
                    .WithTag("FILNUM2", 16, 2)
                    .WithReservedBits(18, 6)
                    .WithTag("FILNUM3", 24, 2)
                    .WithReservedBits(26, 6);

                var digitalNoiseFilterClockSelectionOffset = (long)Registers.DigitalNoiseFilterClockSelection + registerIdx * 0x4;
                doubleWordRegistersMap[digitalNoiseFilterClockSelectionOffset] = new DoubleWordRegister(this)
                    .WithTag("FILCLK0", 0, 2)
                    .WithReservedBits(2, 6)
                    .WithTag("FILCLK1", 8, 2)
                    .WithReservedBits(10, 6)
                    .WithTag("FILCLK2", 16, 2)
                    .WithReservedBits(18, 6)
                    .WithTag("FILCLK3", 24, 2)
                    .WithReservedBits(26, 6);
            }

            byteRegistersMap[(long)Registers.SDChannel0VoltageControlRegister] = new ByteRegister(this)
                .WithTaggedFlag("SD0_PVDD", 0)
                .WithReservedBits(1, 7);

            byteRegistersMap[(long)Registers.SDChannel1VoltageControlRegister] = new ByteRegister(this)
                .WithTaggedFlag("SD1_PVDD", 0)
                .WithReservedBits(1, 7);

            byteRegistersMap[(long)Registers.QSPIVoltageControlRegister] = new ByteRegister(this)
                .WithTaggedFlag("QSPI_PVDD", 0)
                .WithReservedBits(1, 7);

            byteRegistersMap[(long)Registers.EthernetChannel0IOVoltageModeControl] = new ByteRegister(this)
                .WithTaggedFlag("ETH0_1.8V_PVDD", 0)
                .WithTaggedFlag("ETH0_2.5V_PVDD", 1)
                .WithReservedBits(2, 6);

            byteRegistersMap[(long)Registers.EthernetChannel1IOVoltageModeControl] = new ByteRegister(this)
                .WithTaggedFlag("ETH1_1.8V_PVDD", 0)
                .WithTaggedFlag("ETH1_2.5V_PVDD", 1)
                .WithReservedBits(2, 6);

            byteRegistersMap[(long)Registers.WriteProtected] = new ByteRegister(this)
                .WithReservedBits(0, 6)
                .WithTaggedFlag("PFCWE", 6)
                .WithTaggedFlag("BOWI", 7);

            byteRegistersMap[(long)Registers.EthernetMiiRgmiiModeControl] = new ByteRegister(this)
                .WithTaggedFlag("ETH0_MODE", 0)
                .WithTaggedFlag("ETH1_MODE", 1)
                .WithReservedBits(2, 6);

            byteRegisters = new ByteRegisterCollection(this, byteRegistersMap);
            wordRegisters = new WordRegisterCollection(this, wordRegistersMap);
            doubleWordRegisters = new DoubleWordRegisterCollection(this, doubleWordRegistersMap);
        }

        private void UpdateGPIO()
        {
            for(var gpioIdx = 0; gpioIdx < NrOfPins; gpioIdx++)
            {
                var mode = portMode[gpioIdx];
                if(interruptEnabled[gpioIdx])
                {
                    Connections[gpioIdx].Set(State[gpioIdx]);
                }
                else if(mode == PortMode.Output)
                {
                    Connections[gpioIdx].Set(output[gpioIdx]);
                }
                else if(mode == PortMode.OutputInput)
                {
                    Connections[gpioIdx].Set(State[gpioIdx] || output[gpioIdx]);
                }
                else
                {
                    Connections[gpioIdx].Set(false);
                }
                this.DebugLog("GPIO pin {0}: {1}", gpioIdx, Connections[gpioIdx].IsSet ? "set" : "unset");
            }

            foreach(var entry in pinAndFunctionToInterruptMap)
            {
                var gpioIdx = entry.Key.Item1;
                var functionIdx = entry.Key.Item2;
                var interruptIdx = entry.Value;

                if(pinFunctionEnabled[gpioIdx] && pinFunction[gpioIdx] == functionIdx)
                {
                    functionInterrupts[interruptIdx].Set(State[gpioIdx]);
                    this.DebugLog("IRQ{0}: {1}", interruptIdx, State[gpioIdx] ? "set" : "unset");
                }
                else
                {
                    functionInterrupts[interruptIdx].Set(false);
                    this.DebugLog("IRQ{0}: unset", interruptIdx);
                }
            }
        }

        private Func<int, bool, bool> CreateGPIOOutputValueProviderCallback(int portIdx)
        {
            return (pinIdx, _) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    switch(portMode[gpioIdx])
                    {
                        case PortMode.Output:
                            return output[gpioIdx];
                        case PortMode.OutputInput:
                            return State[gpioIdx] || output[gpioIdx];
                        default:
                            return false;
                    }
                }
                LogOutOfRangePinWrite(portIdx, pinIdx, Registers.Port);
                return false;
            };
        }

        private Action<int, bool, bool> CreateGPIOOutputWriteCallback(int portIdx)
        {
            return (pinIdx, _, value) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    var mode = portMode[gpioIdx];
                    if(mode == PortMode.Output || mode == PortMode.OutputInput)
                    {
                        output[gpioIdx] = value;
                    }
                }
                else
                {
                    LogOutOfRangePinRead(portIdx, pinIdx, Registers.Port);
                }
            };
        }

        private Func<int, bool, bool> CreateGPIOInputValueProviderCallback(int portIdx)
        {
            return (pinIdx, _) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    switch(portMode[gpioIdx])
                    {
                        case PortMode.Input:
                            return State[gpioIdx];
                        case PortMode.OutputInput:
                            return State[gpioIdx] || output[gpioIdx];
                        default:
                            return false;
                    }
                }
                LogOutOfRangePinRead(portIdx, pinIdx, Registers.PortInput);
                return false;
            };
        }

        private Func<int, PortMode, PortMode> CreatePortModeValueProviderCallback(int portIdx)
        {
            return (pinIdx, _) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    return portMode[gpioIdx];
                }
                LogOutOfRangePinRead(portIdx, pinIdx, Registers.PortMode);
                return PortMode.HighImpedance;
            };
        }

        private Action<int, PortMode, PortMode> CreatePortModeWriteCallback(int portIdx)
        {
            return (pinIdx, _, value) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    portMode[gpioIdx] = value;
                    switch(value)
                    {
                        case PortMode.HighImpedance:
                            output[gpioIdx] = false;
                            State[gpioIdx] = false;
                            break;
                        case PortMode.Output:
                            State[gpioIdx] = false;
                            break;
                        case PortMode.Input:
                            output[gpioIdx] = false;
                            break;
                        case PortMode.OutputInput:
                            // Setting this mode doesn't change anything.
                            break;
                        default:
                            throw new Exception("unreachable");
                    }
                }
                else
                {
                    LogOutOfRangePinWrite(portIdx, pinIdx, Registers.PortMode);
                }
            };
        }

        private Func<int, bool, bool> CreatePortModeControlValueProviderCallback(int portIdx)
        {
            return (pinIdx, _) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    return pinFunctionEnabled[gpioIdx];
                }
                LogOutOfRangePinRead(portIdx, pinIdx, Registers.PortModeControl);
                return false;
            };
        }

        private Action<int, bool, bool> CreatePortModeControlWriteCallback(int portIdx)
        {
            return (pinIdx, _, value) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    pinFunctionEnabled[gpioIdx] = value;
                }
                else
                {
                    LogOutOfRangePinWrite(portIdx, pinIdx, Registers.PortMode);
                }
            };
        }

        private Func<bool, bool> CreateInterruptEnableControlValueProviderCallback(int portIdx, int pinIdx)
        {
            return (_) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    return interruptEnabled[gpioIdx];
                }
                LogOutOfRangePinRead(portIdx, pinIdx, Registers.PortMode);
                return false;
            };
        }

        private Action<bool, bool> CreateInterruptEnableControlWriteCallback(int portIdx, int pinIdx)
        {
            return (_, value) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    interruptEnabled[gpioIdx] = value;
                }
                else
                {
                    LogOutOfRangePinWrite(portIdx, pinIdx, Registers.InterruptEnableControl);
                }
            };
        }

        private Func<ulong, ulong> CreatePortFunctionControlValueProviderCallback(int portIdx, int pinIdx)
        {
            return (_) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    return (ulong)pinFunction[gpioIdx];
                }
                LogOutOfRangePinRead(portIdx, pinIdx, Registers.PortMode);
                return 0;
            };
        }

        private Action<ulong, ulong> CreatePortFunctionControlWriteCallback(int portIdx, int pinIdx)
        {
            return (_, value) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    // We only implement interrupt peripheral functions.
                    // We also allow selecting default function for pin, which does nothing.
                    if(value == DefaultPinFunctionIdx || pinAndFunctionToInterruptMap.ContainsKey(Tuple.Create(gpioIdx, (int)value)))
                    {
                        pinFunction[gpioIdx] = (int)value;
                    }
                    else if(value > MaxPinFunctionIdx)
                    {
                        this.WarningLog(
                            "Trying to set pin function to invalid value {0}. Function value won't change.",
                            value
                        );
                    }
                    else
                    {
                        this.WarningLog(
                            "Trying to enable function {0} which is not currently supported for port {1}, pin {2}. Function value won't change.",
                            value,
                            portIdx,
                            pinIdx
                        );
                    }
                }
                else
                {
                    LogOutOfRangePinWrite(portIdx, pinIdx, Registers.InterruptEnableControl);
                }
            };
        }

        private bool TryGetGPIOIdx(int portIdx, int pinIdx, out int gpioIdx)
        {
            if(pinIdx > GetNrOfGPIOPinsForPort(portIdx))
            {
                gpioIdx = -1;
                return false;
            }
            gpioIdx = gpioOffsetPerPort[portIdx] + pinIdx;
            return true;
        }

        private int GetNrOfGPIOPinsForPort(int portIdx)
        {
            // Different ports, have different number of GPIO pins.
            switch(portIdx)
            {
                case 42:
                case 48:
                    return 5;
                case 43:
                case 44:
                case 45:
                case 46:
                case 47:
                    return 4;
                case 5:
                case 7:
                case 8:
                case 13:
                case 17:
                case 20:
                case 37:
                case 39:
                case 40:
                    return 3;
                default:
                    return 2;
            }
        }

        private void LogOutOfRangePinRead(int portIdx, int pinIdx, Registers register)
        {
            this.WarningLog(
                "Trying to read from port {0}, pin {1} in register {2}. This pin doesn't exist. Returning 0x0.",
                portIdx,
                pinIdx,
                register
            );
        }

        private void LogOutOfRangePinWrite(int portIdx, int pinIdx, Registers register)
        {
            this.WarningLog(
                "Trying to write to port {0}, pin {1} in register {2}. This pin doesn't exist. Nothing will happen.",
                portIdx,
                pinIdx,
                register
            );
        }

        private const int NrOfPorts = 49;
        private const int NrOfPins = 123;
        private const int NrOfFunctionInterrutps = 8;
        private const int NrOfPinsInPortRegister = 8;
        private const int NrOfDrivingAbilityControlRegisters = 46;
        private const int NrOfSlewRateSwitchingRegisters = 46;
        private const int NrOfPullUpPullDownSwitchingRegisters = 33;
        private const int NrOfDigitalNoiseFilterRegisters = 52;
        private const int MaxPinFunctionIdx = 5;
        private const int DefaultPinFunctionIdx = 0;

        private readonly bool[] output = new bool[NrOfPins];
        private readonly bool[] interruptEnabled = new bool[NrOfPins];
        private readonly int[] pinFunction = new int[NrOfPins];
        private readonly bool[] pinFunctionEnabled = new bool[NrOfPins];
        private readonly PortMode[] portMode = new PortMode[NrOfPins];
        private readonly int[] gpioOffsetPerPort = new int[NrOfPorts];
        private readonly GPIO[] functionInterrupts = Enumerable.Range(0, NrOfFunctionInterrutps).Select(_ => new GPIO()).ToArray();
        private readonly Dictionary<Tuple<int, int>, int> pinAndFunctionToInterruptMap = new Dictionary<Tuple<int, int>, int>
        {
            {Tuple.Create(0, 1),    0},
            {Tuple.Create(1, 1),    1},
            {Tuple.Create(2, 1),    2},
            {Tuple.Create(3, 1),    3},
            {Tuple.Create(4, 1),    4},
            {Tuple.Create(5, 1),    5},
            {Tuple.Create(6, 1),    6},
            {Tuple.Create(7, 1),    7},
            {Tuple.Create(30, 4),   0},
            {Tuple.Create(31, 4),   1},
            {Tuple.Create(32, 4),   2},
            {Tuple.Create(32, 3),   7},
            {Tuple.Create(37, 4),   3},
            {Tuple.Create(38, 4),   4},
            {Tuple.Create(39, 4),   5},
            {Tuple.Create(40, 4),   6},
            {Tuple.Create(41, 4),   7},
            {Tuple.Create(98, 4),   4},
            {Tuple.Create(99, 4),   5},
            {Tuple.Create(100, 4),  6},
            {Tuple.Create(101, 4),  7},
            {Tuple.Create(114, 3),  0},
            {Tuple.Create(115, 3),  1},
            {Tuple.Create(116, 3),  2},
            {Tuple.Create(117, 3),  3},
        };

        private ByteRegisterCollection byteRegisters;
        private WordRegisterCollection wordRegisters;
        private DoubleWordRegisterCollection doubleWordRegisters;

        private enum PortMode
        {
            HighImpedance   = 0x0,
            Input           = 0x1,
            Output          = 0x2,
            OutputInput     = 0x3,
        }

        private enum Registers
        {
            Port                                    = 0x10,
            PortMode                                = 0x120,
            PortModeControl                         = 0x210,
            PortFunctionControl                     = 0x440,
            PortInput                               = 0x810,
            DrivingAbilityControl                   = 0x1010,
            SlewRateSwitching                       = 0x1410,
            PullUpPullDownSwitching                 = 0x1C80,
            DigitalNoiseFilterSwitching             = 0x2008,
            DigitalNoiseFilterNumber                = 0x2408,
            DigitalNoiseFilterClockSelection        = 0x2808,
            InterruptEnableControl                  = 0x2C80,
            SDChannel0VoltageControlRegister        = 0x3000,
            SDChannel1VoltageControlRegister        = 0x3004,
            QSPIVoltageControlRegister              = 0x3008,
            EthernetChannel0IOVoltageModeControl    = 0x300C,
            EthernetChannel1IOVoltageModeControl    = 0x3010,
            WriteProtected                          = 0x3014,
            EthernetMiiRgmiiModeControl             = 0x3018,
        }
    }
}
