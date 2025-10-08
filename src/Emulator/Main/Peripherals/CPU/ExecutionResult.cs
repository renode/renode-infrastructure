//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU
{
    public enum ExecutionResult : ulong
    {
        Ok = 0,
        Interrupted = 1,
        WaitingForInterrupt = 2,
        StoppedAtBreakpoint = 3,
        StoppedAtWatchpoint = 4,
        ExternalMmuFault = 5,
        Aborted = ulong.MaxValue
    }
}