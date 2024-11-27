//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using System;
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
        }

        public long Size => 0x10000;

        private void DefineRegisters()
        {
            var byteRegistersMap = new Dictionary <long, ByteRegister>();
            var wordRegistersMap = new Dictionary <long, WordRegister>();
            var doubleWordRegistersMap = new Dictionary <long, DoubleWordRegister>();

            // Although different ports have different number of GPIO pins, with maximum of 5 usable pins,
            // they have the same registers structure.
            for(var portIdx = 0; portIdx < NrOfPorts; portIdx++)
            {
                var portOffset = (long)Registers.Port + portIdx;
                byteRegistersMap[portOffset] = new ByteRegister(this)
                    .WithFlags(0, NrOfPinsInPortRegister,
                        writeCallback: CreateGPIOOutputWriteCallback(portIdx),
                        valueProviderCallback: CreateGPIOOutputValueProviderCallback(portIdx),
                        name: "P");

                var portModeOffset = (long)Registers.PortMode + portIdx * 0x2;
                wordRegistersMap[portModeOffset] = new WordRegister(this)
                    .WithEnumFields<WordRegister, PortMode>(0, 2, 5,
                        valueProviderCallback: CreatePortModeValueProviderCallback(portIdx),
                        writeCallback: CreatePortModeWriteCallback(portIdx),
                        name: "PM");

                // We don't implement peripheral function mode, so we hardcode this registers with 0x0,
                // and log if someone tries to enable this mode.
                var portModeControlOffset = (long)Registers.PortModeControl + portIdx * 0x2;
                byteRegistersMap[portModeControlOffset] = new ByteRegister(this)
                    .WithFlags(0, 5,
                        writeCallback: CreatePortModeControlWriteCallback(portIdx),
                        valueProviderCallback: (_, __) => false,
                        name: "PMC");

                var portFunctionControlOffset = (long)Registers.PortFunctionControl + portIdx * 0x4;
                doubleWordRegistersMap[portModeOffset] = new DoubleWordRegister(this)
                    .WithTag("PFC0", 0, 3)
                    .WithReservedBits(3, 1)
                    .WithTag("PFC1", 4, 3)
                    .WithReservedBits(7, 1)
                    .WithTag("PFC2", 8, 3)
                    .WithReservedBits(11, 1)
                    .WithTag("PFC3", 12, 3)
                    .WithReservedBits(15, 1)
                    .WithTag("PFC4", 16, 3)
                    .WithReservedBits(19, 13);

                var portInputOffset = (long)Registers.PortInput + portIdx;
                byteRegistersMap[portInputOffset] = new ByteRegister(this)
                    .WithFlags(0, NrOfPinsInPortRegister, FieldMode.Read,
                        valueProviderCallback: CreateGPIOInputValueProviderCallback(portIdx),
                        name: "PIN");
            }

            // Some registers are not uniformly distributed over their regions.
            // We define them in separate loops.
            for(var registerIdx = 0; registerIdx < NrOfDrivingAbilityControlRegisters; registerIdx++)
            {
                var interrupEnableControlOffset = (long)Registers.InterruptEnableControl + registerIdx * 0x4;
                doubleWordRegistersMap[interrupEnableControlOffset] = new DoubleWordRegister(this)
                    .WithTaggedFlag("ISEL0", 0)
                    .WithReservedBits(1, 7)
                    .WithTaggedFlag("ISEL1", 8)
                    .WithReservedBits(9, 7)
                    .WithTaggedFlag("ISEL2", 16)
                    .WithReservedBits(17, 7)
                    .WithTaggedFlag("ISEL3", 24)
                    .WithReservedBits(25, 7);
            }

            for(var registerIdx = 0; registerIdx < NrOfDrivingAbilityControlRegisters; registerIdx++)
            {
                var drivingAbilityControlOffset = (long)Registers.PortFunctionControl + registerIdx * 0x4;
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

            for(var registerIdx = 0; registerIdx < NrOfPullUpPullDownSwitchingRegisters; registerIdx++)
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

        private Func<int, bool, bool> CreateGPIOOutputValueProviderCallback(int portIdx)
        {
            return (pinIdx, _) =>
            {
                // Documentation doesn't state what should be done when accessing pins with indices
                // greater than real number of pins
                if(TryGetGPIOIdx(portIdx, pinIdx, out var gpioIdx))
                {
                    return Connections[gpioIdx].IsSet;
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
                    Connections[gpioIdx].Set(value);
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
                    return State[gpioIdx];
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
                }
                else
                {
                    LogOutOfRangePinWrite(portIdx, pinIdx, Registers.PortMode);
                }
            };
        }

        private Action<int, bool, bool> CreatePortModeControlWriteCallback(int portIdx)
        {
            return (pinIdx, _, value) =>
            {
                if(value)
                {
                    this.WarningLog(
                        "Trying to use peripheral function mode on GPIO port {0}, pin {1}. This mode is not supported. Falling back to GPIO mode.",
                        portIdx,
                        pinIdx
                    );
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
                nameof(register)
            );
        }

        private void LogOutOfRangePinWrite(int portIdx, int pinIdx, Registers register)
        {
            this.WarningLog(
                "Trying to write to port {0}, pin {1} in register {2}. This pin doesn't exist. Nothing will happen.",
                portIdx,
                pinIdx,
                nameof(register)
            );
        }

        private const int NrOfPorts = 49;
        private const int NrOfPins = 123;
        private const int NrOfPinsInPortRegister = 8;
        private const int NrOfInterrupEnableControlRegisters = 51;
        private const int NrOfDrivingAbilityControlRegisters = 46;
        private const int NrOfSlewRateSwitchingRegisters = 46;
        private const int NrOfPullUpPullDownSwitchingRegisters = 33;
        private const int NrOfDigitalNoiseFilterRegisters = 52;

        private readonly PortMode[] portMode = new PortMode[NrOfPins];
        private readonly int[] gpioOffsetPerPort = new int[NrOfPorts];

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

