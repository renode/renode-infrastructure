//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class NRF52840_RTC : IDoubleWordPeripheral, IKnownSize, INRFEventProvider
    {
        public NRF52840_RTC(Machine machine, int numberOfEvents)
        {
            IRQ = new GPIO();

            if(numberOfEvents > MaxNumberOfEvents)
            {
                throw new ConstructionException($"Cannot create {nameof(NRF52840_Timer)} with {numberOfEvents} events (must be less than {MaxNumberOfEvents})");
            }
            this.numberOfEvents = numberOfEvents;
            eventCompareEnabled = new IFlagRegisterField[numberOfEvents];
            eventCompareSet = new IFlagRegisterField[numberOfEvents];
            eventCompareInterruptEnabled = new IFlagRegisterField[numberOfEvents];

            innerTimers = new ComparingTimer[numberOfEvents];
            for(var i = 0u; i < innerTimers.Length; i++)
            {
                var j = i;
                innerTimers[i] = new ComparingTimer(machine.ClockSource, InitialFrequency, this, $"compare{i}", eventEnabled: true);
                innerTimers[i].CompareReached += () =>
                {
                    eventCompareSet[j].Value = true;
                    if (eventCompareEnabled[j].Value)
                    {
                       EventTriggered?.Invoke((uint)Register.Compare0EventPending + j * 4);
                    }
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

        public event Action<uint> EventTriggered;

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
                                timer.Value = 0;
                            }
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithTaggedFlag("TICK", 0)
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlags(16, numberOfEvents, out eventCompareInterruptEnabled, FieldMode.Set | FieldMode.Read, name: "COMPARE")
                    .WithChangeCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },
                {(long)Register.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithTaggedFlag("TICK", 0)
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlags(16, numberOfEvents, name: "COMPARE",
                          writeCallback: (j, _, value) => eventCompareInterruptEnabled[j].Value &= !value,
                          valueProviderCallback: (j, value) => eventCompareInterruptEnabled[j].Value)
                    //missing register fields defined below
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
                {(long)Register.EventEnable, new DoubleWordRegister(this)
                   .WithFlags(16, numberOfEvents, out eventCompareEnabled, name: "COMPARE", changeCallback: (i, _, val) => {
                            this.Log(LogLevel.Noisy, $"eventCompareEnabled[{i}] = {val}");
                         })
                },
                {(long)Register.EventSet, new DoubleWordRegister(this)
                   .WithFlags(16, numberOfEvents, mode: FieldMode.Read | FieldMode.Set,
                         writeCallback: (i, _, val) => {
                            if (val) eventCompareEnabled[i].Value = true;
                            this.Log(LogLevel.Noisy, $"eventCompareEnabled[{i}] = {val}");
                         })
                },
                {(long)Register.EventClear, new DoubleWordRegister(this)
                   .WithFlags(16, numberOfEvents, mode: FieldMode.Read | FieldMode.WriteOneToClear,
                         writeCallback: (i, _, val) => {
                               if (val) eventCompareEnabled[i].Value = false;
                               this.Log(LogLevel.Noisy, $"eventCompareEnabled[{i}] = {!val}");
                         })
                }
            };

            for(var i = 0; i < numberOfEvents; i++)
            {
                var j = i;
                registersMap.Add((long)Register.Compare0 + j * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 24, name: $"COMPARE[{j}]", writeCallback: (_, value) =>
                    {
                        eventCompareSet[j].Value = false;
                        UpdateInterrupts();
                        innerTimers[j].Compare = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        return (uint)innerTimers[j].Compare;
                    })
                    .WithReservedBits(24, 8)
                );

                registersMap.Add((long)Register.Compare0EventPending + j * 4, new DoubleWordRegister(this)
                    .WithFlag(0, out eventCompareSet[j], name: $"EVENTS_COMPARE[{j}]", writeCallback: (_,value) =>
                    {
                        UpdateInterrupts();
                    })
                    .WithReservedBits(1, 31)
                );
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        private void UpdateInterrupts()
        {
            var flag = false;

            for(var i = 0; i < numberOfEvents; i++)
            {
                var thisEventSet = eventCompareInterruptEnabled[i].Value && eventCompareSet[i].Value && eventCompareEnabled[i].Value;
                if (thisEventSet)
                {
                   this.Log(LogLevel.Noisy, "Interrupt set by CC{0} interruptEnable={1} compareSet={2} compareEventEnable={3}",
                         i, eventCompareInterruptEnabled[i].Value, eventCompareSet[i].Value, eventCompareEnabled[i].Value);
                }
                flag |= thisEventSet;
            }

            IRQ.Set(flag);
        }

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField[] eventCompareEnabled;
        private IFlagRegisterField[] eventCompareSet;
        private IFlagRegisterField[] eventCompareInterruptEnabled;
        private IValueRegisterField prescaler;

        private readonly ComparingTimer[] innerTimers;

        private readonly int numberOfEvents;
        private const int InitialFrequency = 32768;
        private const int MaxNumberOfEvents = 4;

        private enum Register : long
        {
            Start = 0x000,
            Stop = 0x004,
            Clear = 0x008,
            TriggerOverflow = 0x00C,
            Tick = 0x100,
            Overflow = 0x104,
            Compare0EventPending = 0x140,
            Compare1EventPending = 0x144,
            Compare2EventPending = 0x148,
            Compare3EventPending = 0x14C,
            InterruptEnableSet = 0x304,
            InterruptEnableClear = 0x308,
            EventEnable = 0x340,
            EventSet = 0x344,
            EventClear = 0x348,
            Counter = 0x504,
            Prescaler = 0x508,
            Compare0 = 0x540,
            Compare1 = 0x544,
            Compare2 = 0x548,
            Compare3 = 0x54C
        }
    }
}
