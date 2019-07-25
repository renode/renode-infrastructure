//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU
{
    public class HaltArguments
    {
        public HaltArguments(HaltReason reason, uint cpuId, ulong? address = null, BreakpointType? breakpointType = null)
        {
            Reason = reason;
            CpuId = cpuId;
            Address = address;
            BreakpointType = breakpointType;
        }

        public HaltReason Reason { get; private set; }
        public uint CpuId { get; private set; }
        public ulong? Address { get; private set; }
        public BreakpointType? BreakpointType { get; private set; }
    }
}

