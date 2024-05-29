//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU
{
    public class HaltArguments
    {
        public HaltArguments(HaltReason reason, ICPU cpu, ulong? address = null, BreakpointType? breakpointType = null)
        {
            Reason = reason;
            Cpu = cpu;
            Address = address;
            BreakpointType = breakpointType;
        }

        public HaltReason Reason { get; private set; }
        public ICPU Cpu { get; private set; }
        public ulong? Address { get; private set; }
        public BreakpointType? BreakpointType { get; private set; }
    }
}

