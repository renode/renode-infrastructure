//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class Cadence_TTC : IDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize
    {
        public Cadence_TTC(IMachine machine, long frequency = DefaultFrequency)
        {
            var irqs = new Dictionary<int, IGPIO>(TimerUnitsCount);
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            for(var index = 0; index < timerUnits.Length; index++)
            {
                var timer = new TimerUnit(machine.ClockSource, this, frequency, $"Timer{index + 1}");
                foreach(var register in BuildTimerUnitRegisters(timer))
                {
                    registersMap[register.Key + index * RegisterSize] = register.Value;
                }

                timerUnits[index] = timer;
                irqs[index] = timer.irq;
            }

            Connections = new ReadOnlyDictionary<int, IGPIO>(irqs);
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            registers.Reset();
            // Registers values depend only on a timer object (not on registers reset)
            foreach(var timer in timerUnits)
            {
                timer.Reset();
            }
        }

        public void SetCounterValue(int timerIndex, uint value)
        {
            if(timerIndex < 0 || timerIndex >= TimerUnitsCount)
            {
                throw new RecoverableException($"Invalid timer index: TTC contains {TimerUnitsCount} timers.");
            }
            timerUnits[timerIndex].Value = value;
        }

        public long Size => 0x100;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Frequency
        {
            get => timerUnits[0].Frequency;
            set
            {
                foreach(var timer in timerUnits)
                {
                    timer.Frequency = value;
                }
            }
        }

        private Dictionary<long, DoubleWordRegister> BuildTimerUnitRegisters(TimerUnit timer)
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ClockControl1, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithTaggedFlag("ExternalClockEdge", 6)
                    .WithTaggedFlag("ClockSource", 5)
                    .WithValueField(1, 4, name: "PrescalerValue",
                        writeCallback: (_, val) => timer.Prescaler = (int)val,
                        valueProviderCallback: (_) => (uint)timer.Prescaler
                    )
                    .WithFlag(0, name: "PrescalerEnable",
                        writeCallback: (_, val) => timer.PrescalerEnabled = val,
                        valueProviderCallback: (_) => timer.PrescalerEnabled
                    )
                },
                {(long)Registers.CounterControl1, new DoubleWordRegister(this)
                    .WithReservedBits(7, 25)
                    .WithTaggedFlag("WaveformPolarity", 6)
                    .WithTaggedFlag("WaveformOutputDisable", 5)
                    .WithFlag(4, name: "Reset",
                        writeCallback: (_, val) => { if(val) timer.ResetValue(); },
                        valueProviderCallback: (_) => false
                    )
                    .WithFlag(3, name: "MatchEnable",
                        writeCallback: (_, val) => timer.MatchEnabled = val,
                        valueProviderCallback: (_) => timer.MatchEnabled
                    )
                    .WithFlag(2, name: "CounterDecrement",
                        writeCallback: (_, val) => timer.Direction = (val ? Direction.Descending : Direction.Ascending),
                        valueProviderCallback: (_) => timer.Direction == Direction.Descending
                    )
                    .WithEnumField<DoubleWordRegister, CounterMode>(1, 1, name: "CounterMode",
                        writeCallback: (_, val) => timer.Mode = val,
                        valueProviderCallback: (_) => timer.Mode
                    )
                    .WithFlag(0, name: "Disable",
                        writeCallback: (_, val) => timer.Enabled = !val,
                        valueProviderCallback: (_) => !timer.Enabled
                    )
                },
                {(long)Registers.CounterValue1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "CounterValue",
                        valueProviderCallback: (_) => (uint)timer.Value
                    )
                },
                {(long)Registers.CounterInterval1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "IntervalCounter",
                        writeCallback: (_, val) => timer.Interval = (uint)val,
                        valueProviderCallback: (_) => timer.Interval
                    )
                },
                {(long)Registers.Match1Counter1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "Match1Value",
                        writeCallback: (_, val) => timer.Match[0].MatchValue = (uint)val,
                        valueProviderCallback: (_) => timer.Match[0].MatchValue
                    )
                },
                {(long)Registers.Match2Counter1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "Match1Value",
                        writeCallback: (_, val) => timer.Match[1].MatchValue = (uint)val,
                        valueProviderCallback: (_) => timer.Match[1].MatchValue
                    )
                },
                {(long)Registers.Match3Counter1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "Match1Value",
                        writeCallback: (_, val) => timer.Match[2].MatchValue = (uint)val,
                        valueProviderCallback: (_) => timer.Match[2].MatchValue
                    )
                },
                {(long)Registers.InterruptStatus1, new DoubleWordRegister(this)
                    .WithReservedBits(6, 26)
                    .WithTaggedFlag("EventTimerOverflowInterrupt", 5)
                    .WithFlag(4, FieldMode.ReadToClear, name: "CounterInterrupt",
                        readCallback: (_, __) => timer.OverflowInterruptFlag = false,
                        valueProviderCallback: (_) => timer.OverflowInterruptFlag
                    )
                    .WithFlag(3, FieldMode.ReadToClear, name: "Match3Interrupt",
                        readCallback: (_, __) => timer.Match[2].Interrupt = false,
                        valueProviderCallback: (_) => timer.Match[2].Interrupt
                    )
                    .WithFlag(2, FieldMode.ReadToClear, name: "Match2Interrupt",
                        readCallback: (_, __) => timer.Match[1].Interrupt = false,
                        valueProviderCallback: (_) => timer.Match[1].Interrupt
                    )
                    .WithFlag(1, FieldMode.ReadToClear, name: "Match1Interrupt",
                        readCallback: (_, __) => timer.Match[0].Interrupt = false,
                        valueProviderCallback: (_) => timer.Match[0].Interrupt
                    )
                    .WithFlag(0, FieldMode.ReadToClear, name: "IntervalInterrupt",
                        readCallback: (_, __) => timer.IntervalInterruptFlag = false,
                        valueProviderCallback: (_) => timer.IntervalInterruptFlag
                    )
                    .WithReadCallback((_, __) => timer.UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable1, new DoubleWordRegister(this)
                    .WithReservedBits(6, 26)
                    .WithTaggedFlag("EventTimerOverflowInterruptEnable", 5)
                    .WithFlag(4, name: "CounterInterruptEnable",
                        writeCallback: (_, val) => timer.OverflowInterruptEnabled = val,
                        valueProviderCallback: (_) => timer.OverflowInterruptEnabled
                    )
                    .WithFlag(3, name: "Match3InterruptEnable",
                        writeCallback: (_, val) => timer.Match[2].InterruptEnable = val,
                        valueProviderCallback: (_) => timer.Match[2].InterruptEnable
                    )
                    .WithFlag(2, name: "Match2InterruptEnable",
                        writeCallback: (_, val) => timer.Match[1].InterruptEnable = val,
                        valueProviderCallback: (_) => timer.Match[1].InterruptEnable
                    )
                    .WithFlag(1, name: "Match1InterruptEnable",
                        writeCallback: (_, val) => timer.Match[0].InterruptEnable = val,
                        valueProviderCallback: (_) => timer.Match[0].InterruptEnable
                    )
                    .WithFlag(0, name: "IntervalInterruptEnable",
                        writeCallback: (_, val) => timer.IntervalInterruptEnabled = val,
                        valueProviderCallback: (_) => timer.IntervalInterruptEnabled
                    )
                    .WithWriteCallback((_, __) => timer.UpdateInterrupts())
                }
            };
        }

        private readonly TimerUnit[] timerUnits = new TimerUnit[TimerUnitsCount];
        private readonly DoubleWordRegisterCollection registers;

        private const long DefaultFrequency = 33330000;
        private const int RegisterSize = 4;
        private const int TimerUnitsCount = 3;
        private const int MatchTimerUnitsCount = 3;

        private class TimerUnit : ITimer
        {
            public TimerUnit(IClockSource clockSource, IPeripheral parent, long frequency, string localName)
            {
                timer = new LimitTimer(clockSource, frequency, parent, localName, limit: OverflowLimit, direction: Direction.Ascending, eventEnabled: true);
                timer.LimitReached += OnLimitReached;

                Match = new MatchTimerUnit[MatchTimerUnitsCount];
                for(var i = 0; i < MatchTimerUnitsCount; i++)
                {
                    Match[i] = new MatchTimerUnit(clockSource, parent, this, frequency, $"{localName}-match{i}");
                }
            }

            public void Reset()
            {
                timer.Reset();
                MatchEnabled = false;
                Mode = CounterMode.Overflow;
                Interval = 0;
                PrescalerEnabled = false;
                Prescaler = 0;
                Array.ForEach(Match, m => m.Reset());

                OverflowInterruptEnabled = false;
                IntervalInterruptEnabled = false;
                ResetFlags();
            }

            public void ResetFlags()
            {
                OverflowInterruptFlag = false;
                IntervalInterruptFlag = false;
                UpdateInterrupts();
            }

            public void ResetValue()
            {
                timer.ResetValue();
                Array.ForEach(Match, m => m.Update());
            }

            public void UpdateInterrupts()
            {
                irq.Set((OverflowInterruptFlag && OverflowInterruptEnabled)
                    || (IntervalInterruptFlag && IntervalInterruptEnabled)
                    || Match.Any(m => m.IRQ));
            }

            public bool Enabled
            {
                get => timer.Enabled;
                set
                {
                    timer.Enabled = value;
                    Array.ForEach(Match, m => m.Update());
                }
            }

            public bool MatchEnabled
            {
                get => matchEnabled;
                set
                {
                    matchEnabled = value;
                    Array.ForEach(Match, m => m.Enabled = value);
                }
            }

            public CounterMode Mode
            {
                get => mode;
                set
                {
                    mode = value;
                    UpdateLimit();
                }
            }

            public uint Interval
            {
                get => interval;
                set
                {
                    interval = value;
                    UpdateLimit();
                }
            }

            public bool PrescalerEnabled
            {
                get => prescalerEnabled;
                set
                {
                    prescalerEnabled = value;
                    UpdateDivider();
                }
            }

            public int Prescaler
            {
                get => prescaler;
                set
                {
                    prescaler = value;
                    UpdateDivider();
                }
            }

            public Direction Direction
            {
                get => timer.Direction;
                set
                {
                    timer.Direction = value;
                    Array.ForEach(Match, m => m.Update());
                }
            }

            public ulong Value
            {
                get => timer.Value;
                set
                {
                    timer.Value = value;
                    Array.ForEach(Match, m => m.Update());
                }
            }

            public long Frequency
            {
                get => timer.Frequency;
                set
                {
                    timer.Frequency = value;
                    Array.ForEach(Match, m => m.Frequency = value);
                }
            }

            public bool OverflowInterruptFlag { get; set; }
            public bool OverflowInterruptEnabled { get; set; }
            public bool IntervalInterruptFlag { get; set; }
            public bool IntervalInterruptEnabled { get; set; }
            public MatchTimerUnit[] Match { get; }

            public readonly IGPIO irq = new GPIO();

            private void OnLimitReached()
            {
                if(Mode == CounterMode.Interval)
                {
                    IntervalInterruptFlag = true;
                }
                else
                {
                    OverflowInterruptFlag = true;
                }
                Array.ForEach(Match, m => m.Update());
                UpdateInterrupts();
            }

            private void UpdateDivider()
            {
                if(PrescalerEnabled)
                {
                    timer.Divider = 1 << (Prescaler + 1);
                }
                else
                {
                    timer.Divider = 1;
                }
                Array.ForEach(Match, m => m.Divider = timer.Divider);
            }

            private void UpdateLimit()
            {
                if(Mode == CounterMode.Interval)
                {
                    timer.Limit = Interval;
                }
                else
                {
                    timer.Limit = OverflowLimit;
                }
                Array.ForEach(Match, m => m.Limit = timer.Limit);
            }

            private CounterMode mode;
            private uint interval;
            private bool prescalerEnabled;
            private bool matchEnabled;
            private int prescaler;

            private readonly LimitTimer timer;

            private const uint OverflowLimit = UInt32.MaxValue;

            public class MatchTimerUnit
            {
                public MatchTimerUnit(IClockSource clockSource, IPeripheral parent, TimerUnit owner, long frequency, string localName)
                {
                    this.owner = owner;
                    timer = new LimitTimer(clockSource, frequency, parent, localName, limit: OverflowLimit, direction: Direction.Ascending, workMode: WorkMode.OneShot);
                    timer.LimitReached += owner.UpdateInterrupts;
                    limit = OverflowLimit;
                }

                public void Reset()
                {
                    timer.Reset();
                    matchValue = 0;
                    // `limit` and `matchEnabled` are reset by `owner`
                }

                public void Update()
                {
                    if(!enabled || matchValue > limit || !owner.Enabled || (IsAscending ? matchValue < owner.Value : owner.Value < matchValue))
                    {
                        timer.Enabled = false;
                        return;
                    }
                    TimerMatchValue = matchValue;
                    TimerValue = owner.Value;
                    timer.Enabled = true;
                }

                public uint MatchValue
                {
                    get => matchValue;
                    set
                    {
                        matchValue = value;
                        Update();
                    }
                }

                public bool Enabled
                {
                    get => enabled;
                    set
                    {
                        enabled = value;
                        Update();
                    }
                }

                public bool Interrupt
                {
                    get => timer.RawInterrupt;
                    set
                    {
                        if(!value)
                        {
                            timer.ClearInterrupt();
                        }
                    }
                }

                public bool IRQ => timer.Interrupt;

                public bool InterruptEnable
                {
                    get => timer.EventEnabled;
                    set => timer.EventEnabled = value;
                }

                public int Divider
                {
                    set => timer.Divider = value;
                }

                public ulong Limit
                {
                    set
                    {
                        limit = value;
                        Update();
                    }
                }

                public long Frequency
                {
                    set => timer.Frequency = value;
                }

                private ulong TranslateValueForInternalTimer(ulong value)
                {
                    // For descending this class flips direction, thus counting in ascending
                    // direction, changing sign and using values congruent modulo `limit`

                    // NOTE: ComparingTimer doesn't support descending direction so this
                    // translation and usage of LimitTimer is a workaround
                    return IsAscending ? value : limit - value;
                }

                private ulong TimerMatchValue
                {
                    get => TranslateValueForInternalTimer(timer.Limit);
                    set => timer.Limit = TranslateValueForInternalTimer(value);
                }

                private ulong TimerValue
                {
                    get => TranslateValueForInternalTimer(timer.Value);
                    set => timer.Value = TranslateValueForInternalTimer(value);
                }

                private bool IsAscending => owner.Direction == Direction.Ascending;

                private bool enabled;
                private ulong limit;
                private uint matchValue;

                private readonly TimerUnit owner;
                private readonly LimitTimer timer;
            }
        }

        private enum CounterMode
        {
            Overflow = 0x0,
            Interval = 0x1,
        }

        private enum Registers : long
        {
            ClockControl1 = 0x00,
            ClockControl2 = 0x04,
            ClockControl3 = 0x08,
            CounterControl1 = 0x0C,
            CounterControl2 = 0x10,
            CounterControl3 = 0x14,
            CounterValue1 = 0x18,
            CounterValue2 = 0x1C,
            CounterValue3 = 0x20,
            CounterInterval1 = 0x24,
            CounterInterval2 = 0x28,
            CounterInterval3 = 0x2C,
            Match1Counter1 = 0x30,
            Match1Counter2 = 0x34,
            Match1Counter3 = 0x38,
            Match2Counter1 = 0x3C,
            Match2Counter2 = 0x40,
            Match2Counter3 = 0x44,
            Match3Counter1 = 0x48,
            Match3Counter2 = 0x4C,
            Match3Counter3 = 0x50,
            InterruptStatus1 = 0x54,
            InterruptStatus2 = 0x58,
            InterruptStatus3 = 0x5C,
            InterruptEnable1 = 0x60,
            InterruptEnable2 = 0x64,
            InterruptEnable3 = 0x68,
            EventControlTimer1 = 0x6C,
            EventControlTimer2 = 0x70,
            EventControlTimer3 = 0x74,
            EventRegister1 = 0x78,
            EventRegister2 = 0x7C,
            EventRegister3 = 0x80
        }
    }
}
