//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class NXPGPIOPort : BaseGPIOPort, IBusPeripheral
    {
        public NXPGPIOPort(IMachine machine, int numberOfPins) : base(machine, numberOfPins)
        {
            IRQ = new GPIO();
            interruptManager = new GPIOInterruptManager(IRQ, State);
            DefineGPIORegisters();
            DefinePortRegisters();
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
                if(number >= inputDisabled.Length)
                {
                    this.Log(LogLevel.Error, "Trying to signal GPIO {0:X}, which is out of range (should be lower than {1:X})", number, inputDisabled.Length);
                    return;
                }
                if(inputDisabled[number].Value)
                {
                    return;
                }
                base.OnGPIO(number, value);
                interruptManager.RefreshInterrupts();
            }
        }

        public override void Reset()
        {
            portRegisters.Reset();
            gpioRegisters.Reset();
            interruptManager.Reset();
            IRQ.Unset();
        }

        [UiAccessible]
        public string[,] PrintCurrentConfiguration()
        {
            const string notApplicable = "---";
            const string disabled = "Disabled";
            var result = new Table();
            result.AddRow("Pin", "Direction", "State", "Input enabled", "Trigger mode", "Active interrupt");
            for(var i = 0; i < NumberOfConnections; i++)
            {
                var isInput = interruptManager.PinDirection[i] == GPIOInterruptManager.Direction.Input;
                result.AddRow(
                    i.ToString(),
                    interruptManager.PinDirection[i].ToString(),
                    isInput ? State[i].ToString() : Connections[i].IsSet.ToString(),
                    isInput ? (!inputDisabled[i].Value).ToString() : notApplicable,
                    isInput
                        ? (interruptManager.InterruptEnable[i]
                            ? interruptManager.InterruptType[i].ToString()
                            : disabled)
                        : notApplicable,
                    isInput ? interruptManager.ActiveInterrupts.ElementAt(i).ToString() : notApplicable
                );
            }
            return result.ToArray();
        }

        public GPIO IRQ { get; }

        private void DefinePortRegisters()
        {
            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.GlobalPinControlLow, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var globalPinWriteEnableLow, FieldMode.Write, name: "GPWE") //order of fields is relevant
                    .WithValueField(16, 16, FieldMode.Write, writeCallback: (_, value) => GlobalPinControlWrite((uint)value, (uint)globalPinWriteEnableLow.Value, highBits: false, highRegisters: false), name: "GPWE")
                },
                {(long)Registers.GlobalPinControlHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var globalPinWriteEnableHigh, FieldMode.Write, name: "GPWE") //order of fields is relevant
                    .WithValueField(16, 16, FieldMode.Write, writeCallback: (_, value) => GlobalPinControlWrite((uint)value, (uint)globalPinWriteEnableHigh.Value, highBits: false, highRegisters: true), name: "GPWD")
                },
                {(long)Registers.GlobalInterruptControlLow, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var globalInterruptWriteEnableLow, FieldMode.Write, name: "GIWE") //order of fields is relevant
                    .WithValueField(16, 16, FieldMode.Write, writeCallback: (_, value) => GlobalPinControlWrite((uint)value, (uint)globalInterruptWriteEnableLow.Value, highBits: true, highRegisters: false), name: "GIWE")
                },
                {(long)Registers.GlobalInterruptControlHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out var globalInterruptWriteEnableHigh, FieldMode.Write, name: "GIWE") //order of fields is relevant
                    .WithValueField(16, 16, FieldMode.Write, writeCallback: (_, value) => GlobalPinControlWrite((uint)value, (uint)globalInterruptWriteEnableHigh.Value, highBits: true, highRegisters: true), name: "GIWD")
                },
                {(long)Registers.InterruptStatusFlag, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfConnections, FieldMode.Read | FieldMode.WriteOneToClear, valueProviderCallback: (i, _) => interruptManager.ActiveInterrupts.ElementAt(i), writeCallback: (i, _, value) =>
                    {
                        if(value)
                        {
                            interruptManager.ClearInterrupt(i);
                        }
                    }, name: "ISF")
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.DigitalFilterEnable, new DoubleWordRegister(this)
                    .WithTag("DFE", 0, 32)
                },
                {(long)Registers.DigitalFilterClock, new DoubleWordRegister(this)
                    .WithTaggedFlag("CS", 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.DigitalFilterWidth, new DoubleWordRegister(this)
                    .WithTag("FILT", 0, 5)
                    .WithReservedBits(5, 27)
                },
            };

            pinControl = new DoubleWordRegister[NumberOfConnections];
            for(var i = 0; i < NumberOfConnections; ++i)
            {
                var j = i;
                pinControl[j] = new DoubleWordRegister(this)
                    .WithTaggedFlag("PE", 0)
                    .WithTaggedFlag("PS", 1)
                    .WithReservedBits(2, 2)
                    .WithTaggedFlag("PFE", 4)
                    .WithReservedBits(5, 1)
                    .WithTaggedFlag("DSE", 6)
                    .WithReservedBits(7, 1)
                    .WithTag("MUX", 8, 3)
                    .WithReservedBits(11, 4)
                    .WithTaggedFlag("LK", 15)
                    .WithEnumField<DoubleWordRegister, InterruptConfiguration>(16, 4, writeCallback: (_, value) =>
                    {
                        interruptManager.InterruptEnable[j] = value != InterruptConfiguration.Disabled;
                        interruptManager.InterruptType[j] = CalculateInterruptType(value);
                        UpdateInterrupts();
                    }, name: "IRQC")
                    .WithReservedBits(20, 4)
                    .WithFlag(24, FieldMode.Read | FieldMode.WriteOneToClear, valueProviderCallback: _ => interruptManager.ActiveInterrupts.ElementAt(j), writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            interruptManager.ClearInterrupt(j);
                            UpdateInterrupts();
                        }
                    }, name: "ISF")
                    .WithReservedBits(25, 7)
                ;
                registers.Add((long)Registers.PinControlRegisterStart + (0x4 * j), pinControl[j]);
            }

            portRegisters = new DoubleWordRegisterCollection(this, registers);
        }

        private void DefineGPIORegisters()
        {
            var registers = new Dictionary<long, DoubleWordRegister>
            {
                {(long)GPIORegisters.DataOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfConnections,
                        valueProviderCallback: _ => GetSetConnectionBits(),
                        writeCallback: (_, value) => {
                            for(byte i = 0; i < NumberOfConnections; i++)
                            {
                                if(interruptManager.PinDirection[i] != GPIOInterruptManager.Direction.Output)
                                {
                                    continue;
                                }
                                Connections[i].Set(BitHelper.IsBitSet(value, i));
                            }
                        },
                        name: "PDOR")
                    //We could have WithReservedBits depending on NumberOfConnections,
                    //but we'd need to filter for 32 bits used
                },
                {(long)GPIORegisters.SetOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfConnections, FieldMode.Write,
                        writeCallback: (_, value) => {
                            value = FilterForDirection((uint)value, true);
                            for(byte i = 0; i < NumberOfConnections; i++)
                            {
                                if(BitHelper.IsBitSet(value, i))
                                {
                                    Connections[i].Set();
                                }
                            }
                        },
                        name: "PSOR")
                },
                {(long)GPIORegisters.ClearOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfConnections, FieldMode.Write,
                        writeCallback: (_, value) => {
                            value = FilterForDirection((uint)value, true);
                            for(byte i = 0; i < NumberOfConnections; i++)
                            {
                                if(BitHelper.IsBitSet(value, i))
                                {
                                    Connections[i].Unset();
                                }
                            }
                        },
                        name: "PCOR")
                },
                {(long)GPIORegisters.ToggleOutput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfConnections, FieldMode.Write,
                        writeCallback: (_, value) => {
                            value = FilterForDirection((uint)value, true);
                            for(byte i = 0; i < NumberOfConnections; i++)
                            {
                                if(BitHelper.IsBitSet(value, i))
                                {
                                    Connections[i].Toggle();
                                }
                            }
                        },
                        name: "PTOR")
                },
                {(long)GPIORegisters.DataInput, new DoubleWordRegister(this)
                    .WithValueField(0, NumberOfConnections, FieldMode.Read,
                        valueProviderCallback: _ => FilterForDirection(BitHelper.GetValueFromBitsArray(State), false),
                        name: "PDIR")
                },
                {(long)GPIORegisters.DataDirection, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfConnections,
                        writeCallback: (i, _, value) => interruptManager.PinDirection[i] = value ? GPIOInterruptManager.Direction.Output : GPIOInterruptManager.Direction.Input,
                        valueProviderCallback: (i, _) => interruptManager.PinDirection[i] == GPIOInterruptManager.Direction.Output,
                        name: "PDDR")
                    .WithWriteCallback((_, __) => interruptManager.RefreshInterrupts())
                },
                {(long)GPIORegisters.InputDisable, new DoubleWordRegister(this)
                    .WithFlags(0, NumberOfConnections, out inputDisabled, name: "PIDR")
                    .WithWriteCallback((_, __) => interruptManager.RefreshInterrupts())
                },
            };
            gpioRegisters = new DoubleWordRegisterCollection(this, registers);
        }

        private void UpdateInterrupts()
        {
            interruptManager.RefreshInterrupts();
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
                    // we have to return something, so we return ActiveLow - but it should not be relevant
                    return GPIOInterruptManager.InterruptTrigger.ActiveLow;
                default:
                    this.Log(LogLevel.Error, "Unsupported interrupt configuration: {0}", type);
                    return GPIOInterruptManager.InterruptTrigger.ActiveLow;
            }
        }

        private void GlobalPinControlWrite(uint value, uint whichRegisters, bool highBits, bool highRegisters)
        {
            var firstRegister = highRegisters ? 16 : 0;
            var firstBit = highBits ? 16 : 0;
            for(var i = firstRegister; i < Math.Min(16 + firstRegister, NumberOfConnections); i++)
            {
                if(!BitHelper.IsBitSet(whichRegisters, (byte)(i - firstRegister)))
                {
                    continue;
                }
                var currentValue = pinControl[i].Read();
                BitHelper.SetMaskedValue(ref currentValue, value, firstBit, 16);
                pinControl[i].Write((long)(Registers.PinControlRegisterStart + 0x4 * i), currentValue);
            }
        }

        private uint FilterForDirection(uint value, bool output)
        {
            var mask = BitHelper.GetValueFromBitsArray(interruptManager.PinDirection.Select(x => x == GPIOInterruptManager.Direction.Output));
            if(!output)
            {
                mask = ~mask & ~BitHelper.GetValueFromBitsArray(inputDisabled.Select(x => x.Value));
            }

            return value & mask;
        }

        private DoubleWordRegisterCollection gpioRegisters;
        private DoubleWordRegisterCollection portRegisters;
        private IFlagRegisterField[] inputDisabled;
        private DoubleWordRegister[] pinControl;

        private readonly GPIOInterruptManager interruptManager;
        private readonly object locker = new object();

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

        private enum GPIORegisters
        {
            DataOutput = 0x00,
            SetOutput = 0x04,
            ClearOutput = 0x08,
            ToggleOutput = 0x0C,
            DataInput = 0x10,
            DataDirection = 0x14,
            InputDisable = 0x18
        }

        private enum Registers
        {
            PinControlRegisterStart = 0x00,
            PinControlRegisterEnd = 0x7C,
            GlobalPinControlLow  = 0x80,
            GlobalPinControlHigh = 0x84,
            GlobalInterruptControlLow = 0x88,
            GlobalInterruptControlHigh = 0x8C,
            InterruptStatusFlag = 0xA0,
            DigitalFilterEnable = 0xC0,
            DigitalFilterClock = 0xC4,
            DigitalFilterWidth = 0xC8,
        }
    }
}
