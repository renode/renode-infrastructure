//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;

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
            this.Ratio = FrequencyToRatio(owner, Step * Frequency);
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
            result.ValueResiduum = ValueResiduum;
            return result;
        }

        public ulong Value;
        public ulong ValueResiduum;
        
        public ulong Period { get; }
        public Action Handler { get; }
        public bool Enabled { get; }
        public Direction Direction { get; }
        public WorkMode WorkMode { get; }
        public IEmulationElement Owner { get; }
        public string LocalName { get; }
        public long Step { get; } 
        public long Frequency { get; }
        // Ratio - i.e. how many emulator ticks are needed for this clock entry tick (when ratio is positive)
        // or how many clock entry tick are needed for emulator tick (when ratio is negative)
        public long Ratio { get; }

        private static long FrequencyToRatio(object parentForLogging, long desiredFrequency)
        {
            var maxHz = (long)TimeInterval.TicksPerSecond;
            long result;
            double error;
            if(desiredFrequency > maxHz)
            {
                result = (long)Math.Round(desiredFrequency / (double)maxHz);
                error = Math.Abs((result * maxHz - desiredFrequency) / (double)desiredFrequency);
            }
            else
            {
                // negative values here (i.e. -maxHz and -result then) are used to be consistent
                // with general meaning of ratio (which is positive when desireq frequency is higher
                // than the basic (maxHz) frequency and negative otherwise
                result = (long)Math.Round(-maxHz / (double)desiredFrequency);
                error = Math.Abs(((maxHz / -result) - desiredFrequency) / (double)desiredFrequency);
            }

            if(error > FrequencyErrorThreshold)
            {
                Logger.LogAs(parentForLogging, LogLevel.Warning, "Set frequency differs from intended by {0}%", error * 100);
            }

            return result;
        }

        private const double FrequencyErrorThreshold = 0.1;
    }
}

