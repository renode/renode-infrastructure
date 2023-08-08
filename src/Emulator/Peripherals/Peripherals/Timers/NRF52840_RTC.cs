//
// Copyright (c) 2010-2023 Antmicro
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
        public NRF52840_RTC(IMachine machine, int numberOfEvents)
        {
            IRQ = new GPIO();

            if(numberOfEvents > MaxNumberOfEvents)
            {
                throw new ConstructionException($"Cannot create {nameof(NRF52840_Timer)} with {numberOfEvents} events (must be less than {MaxNumberOfEvents})");
            }
            this.numberOfEvents = numberOfEvents;
            compareEventEnabled = new IFlagRegisterField[numberOfEvents];
            compareReached = new IFlagRegisterField[numberOfEvents];
            compareInterruptEnabled = new IFlagRegisterField[numberOfEvents];

            innerTimers = new ComparingTimer[numberOfEvents];
            for(var i = 0u; i < innerTimers.Length; i++)
            {
                var j = i;
                // counters are 24-bits
                innerTimers[i] = new ComparingTimer(machine.ClockSource, InitialFrequency, this, $"compare{i}", eventEnabled: true, limit: 0xFFFFFF, compare: 0xFFFFFF);
                innerTimers[i].CompareReached += () =>
                {
                    this.Log(LogLevel.Noisy, "IRQ #{0} triggered", j);
                    compareReached[j].Value = true;
                    if(compareEventEnabled[j].Value)
                    {
                       EventTriggered?.Invoke((uint)Register.Compare0EventPending + j * 4);
                    }
                    UpdateInterrupts();
                };
            }

            tickTimer = new LimitTimer(machine.ClockSource, InitialFrequency, this, "tick", eventEnabled: true, limit: 0x1);
            tickTimer.LimitReached += () =>
            {
                tickEvent.Value = true;
                UpdateInterrupts();
            };

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
            tickTimer.Reset();
            IRQ.Unset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        public event Action<uint> EventTriggered;

        private void UpdateTimersEnable(bool? global = null, bool? tick = null)
        {
            if(global.HasValue)
            {
                foreach(var timer in innerTimers)
                {
                    timer.Enabled = global.Value;
                }
            }

            // due to optimization reasons we try to keep
            // the tick timer disabled as long as possible
            // - we enable it only when the global timer is
            // enabled and the tick event is unmasked
            if(tick.HasValue || (global.HasValue && !global.Value))
            {
                tickTimer.Enabled = innerTimers[0].Enabled && tick.Value;
            }
        }

        private void DefineRegisters()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Register.Start, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_START", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            UpdateTimersEnable(global: true);
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.Stop, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, name: "TASKS_STOP", writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            UpdateTimersEnable(global: false);
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
                            tickTimer.Value = 0;
                        }
                    })
                    .WithReservedBits(1, 31)
                },
                {(long)Register.InterruptEnableSet, new DoubleWordRegister(this)
                    .WithFlag(0, out tickInterruptEnabled, FieldMode.Set | FieldMode.Read, name: "TICK")
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlags(16, numberOfEvents, out compareInterruptEnabled, FieldMode.Set | FieldMode.Read, name: "COMPARE")
                    .WithChangeCallback((_, __) =>
                    {
                        UpdateTimersEnable(tick: tickInterruptEnabled.Value);
                        UpdateInterrupts();
                    })
                },
                {(long)Register.InterruptEnableClear, new DoubleWordRegister(this)
                    .WithFlag(0, name: "TICK",
                          writeCallback: (_, value) => tickInterruptEnabled.Value &= !value,
                          valueProviderCallback: _ => tickInterruptEnabled.Value)
                    .WithTaggedFlag("OVRFLW", 1)
                    .WithReservedBits(2, 14)
                    .WithFlags(16, numberOfEvents, name: "COMPARE",
                          writeCallback: (j, _, value) => compareInterruptEnabled[j].Value &= !value,
                          valueProviderCallback: (j, value) => compareInterruptEnabled[j].Value)
                    //missing register fields defined below
                    .WithChangeCallback((_, __) =>
                    {
                        UpdateTimersEnable(tick: tickInterruptEnabled.Value);
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
                            timer.Divider = (uint)(value + 1u);
                        }
                        tickTimer.Divider = (int)(value + 1);
                    })
                    .WithReservedBits(12, 20)
                },
                {(long)Register.EventEnable, new DoubleWordRegister(this)
                   .WithFlags(16, numberOfEvents, out compareEventEnabled, name: "COMPARE")
                },
                {(long)Register.EventSet, new DoubleWordRegister(this)
                   .WithTaggedFlag("TICK", 0)
                   .WithTaggedFlag("OVRFLW", 1)
                   .WithReservedBits(2, 14)
                   .WithFlags(16, numberOfEvents,
                         writeCallback: (i, _, val) => compareEventEnabled[i].Value |= val,
                         valueProviderCallback: (i, _) => compareEventEnabled[i].Value)
                },
                {(long)Register.EventClear, new DoubleWordRegister(this)
                   .WithFlags(16, numberOfEvents,
                         writeCallback: (i, _, val) => compareEventEnabled[i].Value &= !val,
                         valueProviderCallback: (i, _) => compareEventEnabled[i].Value)
                },
                {(long)Register.Tick, new DoubleWordRegister(this)
                   .WithFlag(0, out tickEvent, name: "EVENTS_TICK")
                   .WithReservedBits(1, 31)
                   .WithWriteCallback((_, __) => UpdateInterrupts())
                }
            };

            for(var i = 0; i < numberOfEvents; i++)
            {
                var j = i;
                registersMap.Add((long)Register.Compare0 + j * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 24, name: $"COMPARE[{j}]", writeCallback: (_, value) =>
                    {
                        compareReached[j].Value = false;
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
                    .WithFlag(0, out compareReached[j], name: $"EVENTS_COMPARE[{j}]", writeCallback: (_, __) =>
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
                var thisEventSet = compareInterruptEnabled[i].Value && compareReached[i].Value;
                if(thisEventSet)
                {
                   this.Log(LogLevel.Noisy, "Interrupt set by CC{0} interruptEnable={1} compareSet={2} compareEventEnable={3}",
                         i, compareInterruptEnabled[i].Value, compareReached[i].Value, compareEventEnabled[i].Value);
                }
                flag |= thisEventSet;
            }

            flag |= tickEvent.Value && tickInterruptEnabled.Value;
            IRQ.Set(flag);
        }

        private DoubleWordRegisterCollection registers;
        private IFlagRegisterField[] compareEventEnabled;
        private IFlagRegisterField[] compareReached;
        private IFlagRegisterField[] compareInterruptEnabled;
        private IValueRegisterField prescaler;
        private IFlagRegisterField tickInterruptEnabled;
        private IFlagRegisterField tickEvent;

        private readonly LimitTimer tickTimer;
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
