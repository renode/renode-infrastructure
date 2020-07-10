//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.UserInterface
{
    // this class provides extension methods for monitor only!
    public static class MonitorTimeExtensions
    {
        public static void SetAdvanceImmediately(this Machine @this, bool val)
        {
            @this.LocalTimeSource.AdvanceImmediately = val;
        }

        public static void SetPerformance(this Machine @this, double p)
        {
            @this.LocalTimeSource.Performance = p;
        }

        public static void SetQuantum(this Machine @this, TimeInterval interval)
        {
            @this.LocalTimeSource.Quantum = interval;
        }

        public static string GetTimeSourceInfo(this Machine @this)
        {
            return @this.LocalTimeSource.ToString();
        }

        public static void SetAdvanceImmediately(this Emulation @this, bool val)
        {
            @this.MasterTimeSource.AdvanceImmediately = val;
        }

        public static void SetPerformance(this Emulation @this, double p)
        {
            @this.MasterTimeSource.Performance = p;
        }

        public static void SetQuantum(this Emulation @this, TimeInterval interval)
        {
            @this.MasterTimeSource.Quantum = interval;
        }

        public static void SetGlobalQuantum(this Emulation @this, TimeInterval quantum)
        {
            SetQuantumRecursively(@this.MasterTimeSource, quantum);
        }

        public static void SetGlobalPerformance(this Emulation @this, double p)
        {
            SetPerformanceRecursively(@this.MasterTimeSource, p);
        }

        public static void SetGlobalAdvanceImmediately(this Emulation @this, bool val)
        {
            SetAdvanceImmediatelyRecursively(@this.MasterTimeSource, val);
        }

        public static string GetTimeSourceInfo(this Emulation @this)
        {
            return @this.MasterTimeSource.ToString();
        }

        public static void SetGlobalSerialExecution(this Emulation @this, bool val)
        {
            SetSerialExecutionRecursively(@this.MasterTimeSource, val);
        }

        public static bool SetSerialExecution(this Machine @this, bool val)
        {
            var previousValue = @this.LocalTimeSource.ExecuteInSerial;
            @this.LocalTimeSource.ExecuteInSerial = val;
            return previousValue;
        }

        private static void SetSerialExecutionRecursively(TimeSourceBase source, bool val)
        {
            source.ExecuteInSerial = val;
            foreach(var sink in source.Sinks.OfType<TimeSourceBase>())
            {
                 SetSerialExecutionRecursively(sink, val);
            }
        }

        private static void SetAdvanceImmediatelyRecursively(TimeSourceBase source, bool val)
        {
            source.AdvanceImmediately = val;
            foreach(var sink in source.Sinks.OfType<TimeSourceBase>())
            {
                SetAdvanceImmediatelyRecursively(sink, val);
            }
        }

        private static void SetPerformanceRecursively(TimeSourceBase source, double p)
        {
            source.Performance = p;
            foreach(var sink in source.Sinks.OfType<TimeSourceBase>())
            {
                SetPerformanceRecursively(sink, p);
            }
        }

        private static void SetQuantumRecursively(TimeSourceBase source, TimeInterval quantum)
        {
            source.Quantum = quantum;
            foreach(var sink in source.Sinks.OfType<TimeSourceBase>())
            {
                SetQuantumRecursively(sink, quantum);
            }
        }
    }
}