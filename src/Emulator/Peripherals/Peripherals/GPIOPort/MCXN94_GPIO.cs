//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class MCXN94_GPIO : BaseGPIOPort, IBusPeripheral, IKnownSize
    {
        public MCXN94_GPIO(IMachine machine) : base(machine, NumberOfPins)
        {
            IRQ0 = new GPIO();
            IRQ1 = new GPIO();

            interruptEnabled = new bool[NumberOfPins];
            interruptRoute = new bool[NumberOfPins];
            interruptType = new GPIOInterruptManager.InterruptTrigger[NumberOfPins];
            pinDirection = Enumerable.Repeat(GPIOInterruptManager.Direction.Input, NumberOfPins).ToArray();
            outputData = new bool[NumberOfPins];
            previousState = new bool[NumberOfPins];
            activeInterrupts = new bool[NumberOfPins];

            DefineGPIORegisters();
            DefinePORTRegisters();
        }

        [ConnectionRegion("gpio")]
        public uint ReadDoubleWordFromGPIO(long offset)
        {
            lock(locker)
            {
                return gpioRegisters.Read(offset);
            }
        }

        [ConnectionRegion("gpio")]
        public void WriteDoubleWordToGPIO(long offset, uint value)
        {
            lock(locker)
            {
                gpioRegisters.Write(offset, value);
            }
        }

        [ConnectionRegion("gpio")]
        public byte ReadByteFromGPIO(long offset)
        {
            lock(locker)
            {
                if(TryReadPinDataByte(offset, out var result))
                {
                    return result;
                }

                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        [ConnectionRegion("gpio")]
        public void WriteByteToGPIO(long offset, byte value)
        {
            lock(locker)
            {
                if(TryWritePinDataByte(offset, value))
                {
                    return;
                }

                this.LogUnhandledWrite(offset, value);
            }
        }

        [ConnectionRegion("port")]
        public uint ReadDoubleWordFromPORT(long offset)
        {
            lock(locker)
            {
                return portRegisters.Read(offset);
            }
        }

        [ConnectionRegion("port")]
        public void WriteDoubleWordToPORT(long offset, uint value)
        {
            lock(locker)
            {
                portRegisters.Write(offset, value);
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            lock(locker)
            {
                if(!CheckPinNumber(number))
                {
                    return;
                }

                if(inputDisabled[number].Value)
                {
                    return;
                }

                base.OnGPIO(number, value);
                RefreshInterrupts();
            }
        }

        public override void Reset()
        {
            lock(locker)
            {
                base.Reset();
                gpioRegisters.Reset();
                portRegisters.Reset();

                Array.Clear(interruptEnabled, 0, interruptEnabled.Length);
                Array.Clear(interruptRoute, 0, interruptRoute.Length);
                Array.Clear(interruptType, 0, interruptType.Length);
                Array.Clear(outputData, 0, outputData.Length);
                Array.Clear(previousState, 0, previousState.Length);
                Array.Clear(activeInterrupts, 0, activeInterrupts.Length);
                for(var i = 0; i < pinDirection.Length; ++i)
                {
                    pinDirection[i] = GPIOInterruptManager.Direction.Input;
                }

                IRQ0.Unset();
                IRQ1.Unset();
            }
        }

        public GPIO IRQ0 { get; }

        public GPIO IRQ1 { get; }

        public long Size => 0x200;

        private static bool IsInterruptConfiguration(InterruptConfiguration configuration)
        {
            return configuration == InterruptConfiguration.InterruptWhenLow
                || configuration == InterruptConfiguration.InterruptRisingEdge
                || configuration == InterruptConfiguration.InterruptFallingEdge
                || configuration == InterruptConfiguration.InterruptEitherEdge
                || configuration == InterruptConfiguration.InterruptWhenHigh;
        }

        private void DefineGPIORegisters()
        {
            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)GPIORegisters.PortDataOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfPins,
                        valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(outputData),
                        writeCallback: (_, value) =>
                        {
                            for(var i = 0; i < NumberOfPins; ++i)
                            {
                                outputData[i] = BitHelper.IsBitSet(value, (byte)i);
                            }
                        },
                        name: "PDOR")
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)GPIORegisters.PortSetOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfPins, FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            for(var i = 0; i < NumberOfPins; ++i)
                            {
                                if(BitHelper.IsBitSet(value, (byte)i))
                                {
                                    outputData[i] = true;
                                }
                            }
                        },
                        name: "PSOR")
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)GPIORegisters.PortClearOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfPins, FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            for(var i = 0; i < NumberOfPins; ++i)
                            {
                                if(BitHelper.IsBitSet(value, (byte)i))
                                {
                                    outputData[i] = false;
                                }
                            }
                        },
                        name: "PCOR")
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)GPIORegisters.PortToggleOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfPins, FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            for(var i = 0; i < NumberOfPins; ++i)
                            {
                                if(BitHelper.IsBitSet(value, (byte)i))
                                {
                                    outputData[i] ^= true;
                                }
                            }
                        },
                        name: "PTOR")
                    .WithWriteCallback((_, __) => UpdateConnections())
                },
                {(long)GPIORegisters.PortDataInput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfPins, FieldMode.Read, valueProviderCallback: _ => GetInputBits(), name: "PDIR")
                },
                {(long)GPIORegisters.PortDataDirection, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins,
                        writeCallback: (index, _, value) => pinDirection[index] = value ? GPIOInterruptManager.Direction.Output : GPIOInterruptManager.Direction.Input,
                        valueProviderCallback: (index, _) => IsOutput(index),
                        name: "PDDR")
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateConnections();
                        RefreshInterrupts();
                    })
                },
                {(long)GPIORegisters.PortInputDisable, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, out inputDisabled, name: "PIDR")
                    .WithWriteCallback((_, __) => RefreshInterrupts())
                },
                {(long)GPIORegisters.GlobalInterruptControlLow, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var interruptWriteEnableLow, FieldMode.Write, name: "GIWE")
                    .WithValueField(16, 16, FieldMode.Write,
                        writeCallback: (_, value) => GlobalInterruptControlWrite((ushort)value, (ushort)interruptWriteEnableLow.Value, upperPins: false),
                        name: "GIWD")
                },
                {(long)GPIORegisters.GlobalInterruptControlHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var interruptWriteEnableHigh, FieldMode.Write, name: "GIWE")
                    .WithValueField(16, 16, FieldMode.Write,
                        writeCallback: (_, value) => GlobalInterruptControlWrite((ushort)value, (ushort)interruptWriteEnableHigh.Value, upperPins: true),
                        name: "GIWD")
                },
                {(long)GPIORegisters.InterruptStatusFlags0, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Read | FieldMode.WriteOneToClear,
                        valueProviderCallback: (index, _) => !interruptRoute[index] && activeInterrupts[index],
                        writeCallback: (index, _, value) =>
                        {
                            if(value && !interruptRoute[index])
                            {
                                activeInterrupts[index] = false;
                            }
                        },
                        name: "ISF")
                    .WithWriteCallback((_, __) => RefreshInterrupts())
                },
                {(long)GPIORegisters.InterruptStatusFlags1, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfPins, FieldMode.Read | FieldMode.WriteOneToClear,
                        valueProviderCallback: (index, _) => interruptRoute[index] && activeInterrupts[index],
                        writeCallback: (index, _, value) =>
                        {
                            if(value && interruptRoute[index])
                            {
                                activeInterrupts[index] = false;
                            }
                        },
                        name: "ISF")
                    .WithWriteCallback((_, __) => RefreshInterrupts())
                },
            };

            interruptControlRegisters = new DoubleWordRegister[NumberOfPins];
            for(var i = 0; i < NumberOfPins; ++i)
            {
                var index = i;
                interruptControlRegisters[index] = new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithEnumField<DoubleWordRegister, InterruptConfiguration>(16, 4,
                        writeCallback: (_, value) =>
                        {
                            interruptEnabled[index] = IsInterruptConfiguration(value);
                            interruptType[index] = CalculateInterruptType(value);
                            RefreshInterrupts();
                        },
                        name: "IRQC")
                    .WithFlag(20, writeCallback: (_, value) =>
                        {
                            interruptRoute[index] = value;
                            RefreshInterrupts();
                        },
                        valueProviderCallback: _ => interruptRoute[index],
                        name: "IRQS")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, FieldMode.Read | FieldMode.WriteOneToClear,
                        valueProviderCallback: _ => activeInterrupts[index],
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                activeInterrupts[index] = false;
                                RefreshInterrupts();
                            }
                        },
                        name: "ISF")
                    .WithReservedBits(25, 7);
                registers.Add((long)GPIORegisters.InterruptControlBase + (0x4 * index), interruptControlRegisters[index]);
            }

            gpioRegisters = new DoubleWordRegisterCollection(this, registers);
        }

        private void DefinePORTRegisters()
        {
            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)PORTRegisters.GlobalPinControlLow, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var pinWriteEnableLow, FieldMode.Write, name: "GPWE")
                    .WithValueField(16, 16, FieldMode.Write,
                        writeCallback: (_, value) => GlobalPinControlWrite((ushort)value, (ushort)pinWriteEnableLow.Value, upperPins: false),
                        name: "GPWD")
                },
                {(long)PORTRegisters.GlobalPinControlHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var pinWriteEnableHigh, FieldMode.Write, name: "GPWE")
                    .WithValueField(16, 16, FieldMode.Write,
                        writeCallback: (_, value) => GlobalPinControlWrite((ushort)value, (ushort)pinWriteEnableHigh.Value, upperPins: true),
                        name: "GPWD")
                },
            };

            portControlRegisters = new DoubleWordRegister[NumberOfPins];
            for(var i = 0; i < NumberOfPins; ++i)
            {
                portControlRegisters[i] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "PCR");
                registers.Add((long)PORTRegisters.PortControlBase + (0x4 * i), portControlRegisters[i]);
            }

            portRegisters = new DoubleWordRegisterCollection(this, registers);
        }

        private void GlobalPinControlWrite(ushort value, ushort whichPins, bool upperPins)
        {
            var firstPin = upperPins ? 16 : 0;
            for(var i = firstPin; i < firstPin + 16; ++i)
            {
                if(!BitHelper.IsBitSet(whichPins, (byte)(i - firstPin)))
                {
                    continue;
                }

                var currentValue = portControlRegisters[i].Read();
                BitHelper.SetMaskedValue(ref currentValue, value, 0, 16);
                portControlRegisters[i].Write((long)PORTRegisters.PortControlBase + 0x4 * i, currentValue);
            }
        }

        private void GlobalInterruptControlWrite(ushort value, ushort whichPins, bool upperPins)
        {
            var firstPin = upperPins ? 16 : 0;
            for(var i = firstPin; i < firstPin + 16; ++i)
            {
                if(!BitHelper.IsBitSet(whichPins, (byte)(i - firstPin)))
                {
                    continue;
                }

                var currentValue = interruptControlRegisters[i].Read();
                BitHelper.SetMaskedValue(ref currentValue, value, 16, 16);
                interruptControlRegisters[i].Write((long)GPIORegisters.InterruptControlBase + 0x4 * i, currentValue);
            }
        }

        private void RefreshInterrupts()
        {
            var irq0State = false;
            var irq1State = false;

            for(var i = 0; i < NumberOfPins; ++i)
            {
                if(interruptEnabled[i] && !inputDisabled[i].Value && !IsOutput(i))
                {
                    var currentState = State[i];
                    var edge = currentState != previousState[i];

                    switch(interruptType[i])
                    {
                    case GPIOInterruptManager.InterruptTrigger.ActiveHigh:
                        activeInterrupts[i] |= currentState;
                        break;
                    case GPIOInterruptManager.InterruptTrigger.ActiveLow:
                        activeInterrupts[i] |= !currentState;
                        break;
                    case GPIOInterruptManager.InterruptTrigger.RisingEdge:
                        activeInterrupts[i] |= edge && currentState;
                        break;
                    case GPIOInterruptManager.InterruptTrigger.FallingEdge:
                        activeInterrupts[i] |= edge && !currentState;
                        break;
                    case GPIOInterruptManager.InterruptTrigger.BothEdges:
                        activeInterrupts[i] |= edge;
                        break;
                    default:
                        break;
                    }
                }

                previousState[i] = State[i];

                if(!activeInterrupts[i])
                {
                    continue;
                }

                if(interruptRoute[i])
                {
                    irq1State = true;
                }
                else
                {
                    irq0State = true;
                }
            }

            IRQ0.Set(irq0State);
            IRQ1.Set(irq1State);
        }

        private GPIOInterruptManager.InterruptTrigger CalculateInterruptType(InterruptConfiguration type)
        {
            switch(type)
            {
            case InterruptConfiguration.InterruptWhenLow:
                return GPIOInterruptManager.InterruptTrigger.ActiveLow;
            case InterruptConfiguration.InterruptFallingEdge:
                return GPIOInterruptManager.InterruptTrigger.FallingEdge;
            case InterruptConfiguration.InterruptRisingEdge:
                return GPIOInterruptManager.InterruptTrigger.RisingEdge;
            case InterruptConfiguration.InterruptEitherEdge:
                return GPIOInterruptManager.InterruptTrigger.BothEdges;
            case InterruptConfiguration.InterruptWhenHigh:
                return GPIOInterruptManager.InterruptTrigger.ActiveHigh;
            case InterruptConfiguration.Disabled:
            case InterruptConfiguration.DMARequestRisingEdge:
            case InterruptConfiguration.DMARequestFallingEdge:
            case InterruptConfiguration.DMARequestEitherEdge:
                return GPIOInterruptManager.InterruptTrigger.ActiveLow;
            default:
                this.Log(LogLevel.Warning, "Unsupported interrupt configuration: {0}", type);
                return GPIOInterruptManager.InterruptTrigger.ActiveLow;
            }
        }

        private bool TryReadPinDataByte(long offset, out byte value)
        {
            value = 0;
            if(offset < (long)GPIORegisters.PinDataBase || offset >= (long)GPIORegisters.PinDataBase + NumberOfPins)
            {
                return false;
            }

            var pin = (int)(offset - (long)GPIORegisters.PinDataBase);
            value = (byte)(GetPinData(pin) ? 1 : 0);
            return true;
        }

        private bool TryWritePinDataByte(long offset, byte value)
        {
            if(offset < (long)GPIORegisters.PinDataBase || offset >= (long)GPIORegisters.PinDataBase + NumberOfPins)
            {
                return false;
            }

            var pin = (int)(offset - (long)GPIORegisters.PinDataBase);
            outputData[pin] = (value & 0x1) != 0;
            UpdateConnections();
            return true;
        }

        private bool GetPinData(int pin)
        {
            if(IsOutput(pin))
            {
                return outputData[pin];
            }

            return !inputDisabled[pin].Value && State[pin];
        }

        private uint GetInputBits()
        {
            var value = 0u;
            for(var i = 0; i < NumberOfPins; ++i)
            {
                if(!IsOutput(i) && !inputDisabled[i].Value && State[i])
                {
                    value |= 1u << i;
                }
            }
            return value;
        }

        private bool IsOutput(int pin)
        {
            return (pinDirection[pin] & GPIOInterruptManager.Direction.Output) != 0;
        }

        private void UpdateConnections()
        {
            for(var i = 0; i < NumberOfPins; ++i)
            {
                Connections[i].Set(IsOutput(i) && outputData[i]);
            }
        }

        private DoubleWordRegisterCollection gpioRegisters;
        private DoubleWordRegisterCollection portRegisters;
        private DoubleWordRegister[] interruptControlRegisters;
        private DoubleWordRegister[] portControlRegisters;
        private IFlagRegisterField[] inputDisabled;

        private readonly bool[] interruptEnabled;
        private readonly bool[] interruptRoute;
        private readonly GPIOInterruptManager.InterruptTrigger[] interruptType;
        private readonly GPIOInterruptManager.Direction[] pinDirection;
        private readonly bool[] outputData;
        private readonly bool[] previousState;
        private readonly bool[] activeInterrupts;
        private readonly object locker = new object();

        private const int NumberOfPins = 32;

        private enum InterruptConfiguration
        {
            Disabled = 0,
            DMARequestRisingEdge = 1,
            DMARequestFallingEdge = 2,
            DMARequestEitherEdge = 3,
            InterruptWhenLow = 8,
            InterruptRisingEdge = 9,
            InterruptFallingEdge = 10,
            InterruptEitherEdge = 11,
            InterruptWhenHigh = 12,
        }

        private enum GPIORegisters : long
        {
            PortDataOutput = 0x40,
            PortSetOutput = 0x44,
            PortClearOutput = 0x48,
            PortToggleOutput = 0x4C,
            PortDataInput = 0x50,
            PortDataDirection = 0x54,
            PortInputDisable = 0x58,
            PinDataBase = 0x60,
            InterruptControlBase = 0x80,
            GlobalInterruptControlLow = 0x100,
            GlobalInterruptControlHigh = 0x104,
            InterruptStatusFlags0 = 0x120,
            InterruptStatusFlags1 = 0x124,
        }

        private enum PORTRegisters : long
        {
            GlobalPinControlLow = 0x10,
            GlobalPinControlHigh = 0x14,
            PortControlBase = 0x80,
        }
    }
}
