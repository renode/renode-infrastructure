//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB
{
    public static class BreakpointTypeExtensions
    {
        public static string GetStopReason(this BreakpointType type)
        {
            switch (type) 
            {
            case BreakpointType.AccessWatchpoint:
                return "awatch";
            case BreakpointType.WriteWatchpoint:
                return "watch";
            case BreakpointType.ReadWatchpoint:
                return "rwatch";
            case BreakpointType.HardwareBreakpoint:
                return "hwbreak";
            case BreakpointType.MemoryBreakpoint:
                return "swbreak";
            default:
                throw new ArgumentException();
            }
        }
    }
}

