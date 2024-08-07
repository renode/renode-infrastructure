//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Time
{
    public struct ClockEntry
    {
        public ClockEntry(ulong period, long frequency, Action handler, IEmulationElement owner, string localName, bool enabled = true, Direction direction = Direction.Ascending, WorkMode workMode = WorkMode.Periodic, long step = 1) : this()
        {
            this.Value = direction == Direction.Ascending ? 0 : period;
            this.Frequency = frequency;
            this.Step = step;
            this.Period = period;
            this.Handler = handler;
            this.Enabled = enabled;
            this.Direction = direction;
            this.WorkMode = workMode;
            this.Owner = owner;
            this.LocalName = localName;
            this.Ratio = FrequencyToRatio(Step * Frequency);
            this.ValueResiduum = Fraction.Zero;
        }

        public ClockEntry With(ulong? period = null, long? frequency = null, Action handler = null, bool? enabled = null,
            ulong? value = null, Direction? direction = null, WorkMode? workMode = null, long? step = null)
        {
            var result = new ClockEntry(
                period ?? Period,
                frequency ?? Frequency,
                handler ?? Handler,
                Owner,
                LocalName,
                enabled ?? Enabled,
                direction ?? Direction,
                workMode ?? WorkMode,
                step ?? Step);
            
            result.Value = value ?? Value;
            result.ValueResiduum = frequency != null ? Fraction.Zero : ValueResiduum;
            return result;
        }

        public ulong Value;
        public Fraction ValueResiduum;
        
        public ulong Period { get; }
        public Action Handler { get; }
        public bool Enabled { get; }
        public Direction Direction { get; }
        public WorkMode WorkMode { get; }
        public IEmulationElement Owner { get; }
        public string LocalName { get; }
        public long Step { get; } 
        public long Frequency { get; }
        // Ratio - i.e. how many emulator ticks are needed for this clock entry tick
        public Fraction Ratio { get; }

        private static Fraction FrequencyToRatio(long desiredFrequency)
        {
            var maxHz = TimeInterval.TicksPerSecond;
            return new Fraction((ulong)desiredFrequency, maxHz);
        }
    }
}

