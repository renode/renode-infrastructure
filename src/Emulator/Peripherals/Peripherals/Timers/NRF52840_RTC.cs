//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NRF52840_RTC : IDoubleWordPeripheral, IKnownSize
    {
        public NRF52840_RTC(Machine machine)
        {
            IRQ = new GPIO();

            eventCompareEnabled = new IFlagRegisterField[NumberOfEvents];
            eventCompareInterruptEnabled= new IFlagRegisterField[NumberOfEvents];

            innerTimers = new ComparingTimer[NumberOfEvents];
            for(var i = 0; i < innerTimers.Length; i++)
            {
                var j = i;
                innerTimers[i] = new ComparingTimer(machine.ClockSource, InitialFrequency, this, $"compare{i}", eventEnabled: true);
                innerTimers[i].CompareReached += () =>
                {
                    eventCompareEnabled[j].Value = true;
                    UpdateInterrupts();
                };
            }

            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
            foreach(var timer in innerTimers)
            {
                timer.Reset();
            }
            IRQ.Unset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Register.Start, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_START", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            foreach(var timer in innerTimers)
                            {
                                timer.Enabled = true;
                            }
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Stop, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_STOP", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            foreach(var timer in innerTimers)
                            {
                                timer.Enabled = false;
                            }
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Clear, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_CLEAR", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            foreach(var timer in innerTimers)
                            {
                                timer.Value = 0
                            }
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Compare0EventPending, new DoubleWordRegister(this)
                    .WithFlag(0, out eventCompareEnabled[0], name: "EVENTS_COMPARE[0]", writeCallback: (_,__) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Compare1EventPending, new DoubleWordRegister(this)
                    .WithFlag(0, out eventCompareEnabled[1], name: "EVENTS_COMPARE[0]", writeCallback: (_,__) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Compare2EventPending, new DoubleWordRegister(this)
                    .WithFlag(0, out eventCompareEnabled[2], name: "EVENTS_COMPARE[0]", writeCallback: (_,__) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Compare3EventPending, new DoubleWordRegister(this)
                    .WithFlag(0, out eventCompareEnabled[3], name: "EVENTS_COMPARE[0]", writeCallback: (_,__) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithTaggedFlag("TICK", 0)
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlag(16, out eventCompareInterruptEnabled[0], FieldMode.Set | FieldMode.Read, name: "COMPARE[0]")
                    .WithFlag(17, out eventCompareInterruptEnabled[1], FieldMode.Set | FieldMode.Read, name: "COMPARE[1]")
                    .WithFlag(18, out eventCompareInterruptEnabled[2], FieldMode.Set | FieldMode.Read, name: "COMPARE[2]")
                    .WithFlag(19, out eventCompareInterruptEnabled[3], FieldMode.Set | FieldMode.Read, name: "COMPARE[3]")
                    .WithChangeCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },
                {(long)Register.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithTaggedFlag("TICK", 0)
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlag(16, name: "COMPARE[0]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[0].Value = false; })
                    .WithFlag(17, name: "COMPARE[1]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[1].Value = false; })
                    .WithFlag(18, name: "COMPARE[2]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[2].Value = false; })
                    .WithFlag(19, name: "COMPARE[3]", writeCallback: (_,value) => { if(value) eventCompareInterruptEnabled[3].Value = false; })
                    .WithChangeCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },
                {(long)Register.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 24, FieldMode.Read, name: "COUNTER", valueProviderCallback: _ =>
                    {
                        // all timers have the same value, so let's just pick the first one
                        return (uint)innerTimers[0].Value;
                    })
                    .WithReservedBits(24, 8)
                },
                {(long)Register.Prescaler, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out prescaler, name: "PRESCALER", writeCallback: (_, value) =>
                    {
                        foreach(var timer in innerTimers)
                        {
                            timer.Divider = value + 1;
                        }
                    })
                    .WithReservedBits(12, 20)
                },
                {(long)Register.Compare0, new DoubleWordRegister(this)
                    .WithValueField(0, 24, name: "COMPARE", writeCallback: (_, value) =>
                    {
                        eventCompareEnabled[0].Value = false;
                        UpdateInterrupts();
                        innerTimers[0].Compare = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        return (uint)innerTimers[0].Compare;
                    })
                    .WithReservedBits(24, 8)
                },
                {(long)Register.Compare1, new DoubleWordRegister(this)
                    .WithValueField(0, 24, name: "COMPARE", writeCallback: (_, value) =>
                    {
                        eventCompareEnabled[1].Value = false;
                        UpdateInterrupts();
                        innerTimers[1].Compare = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        return (uint)innerTimers[1].Compare;
                    })
                    .WithReservedBits(24, 8)
                },
                {(long)Register.Compare2, new DoubleWordRegister(this)
                    .WithValueField(0, 24, name: "COMPARE", writeCallback: (_, value) =>
                    {
                        eventCompareEnabled[2].Value = false;
                        UpdateInterrupts();
                        innerTimers[2].Compare = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        return (uint)innerTimers[2].Compare;
                    })
                    .WithReservedBits(24, 8)
                },
                {(long)Register.Compare3, new DoubleWordRegister(this)
                    .WithValueField(0, 24, name: "COMPARE", writeCallback: (_, value) =>
                    {
                        eventCompareEnabled[3].Value = false;
                        UpdateInterrupts();
                        innerTimers[3].Compare = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        return (uint)innerTimers[3].Compare;
                    })
                    .WithReservedBits(24, 8)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            flag |= eventCompareInterruptEnabled[0].Value && eventCompareEnabled[0].Value;
            flag |= eventCompareInterruptEnabled[1].Value && eventCompareEnabled[1].Value;
            flag |= eventCompareInterruptEnabled[2].Value && eventCompareEnabled[2].Value;
            flag |= eventCompareInterruptEnabled[3].Value && eventCompareEnabled[3].Value;

            IRQ.Set(flag);
        }

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField[] eventCompareEnabled;
        private IFlagRegisterField[] eventCompareInterruptEnabled;
        private IValueRegisterField prescaler;

        private readonly ComparingTimer[] innerTimers;

        private const int NumberOfEvents = 4;
        private const int InitialFrequency = 32768;

        private enum Register : long
        {
            Start = 0x000,
            Stop = 0x004,
            Clear = 0x008,
            Compare0EventPending = 0x140,
            Compare1EventPending = 0x144,
            Compare2EventPending = 0x148,
            Compare3EventPending = 0x14C,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            Counter = 0x504,
            Prescaler = 0x508,
            Compare0 = 0x540,
            Compare1 = 0x544,
            Compare2 = 0x548,
            Compare3 = 0x54C
        }
    }
}
