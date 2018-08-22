//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU
{
    public enum BreakpointType
    {
        MemoryBreakpoint,
        HardwareBreakpoint,
        WriteWatchpoint,
        ReadWatchpoint,
        AccessWatchpoint
    }
}

