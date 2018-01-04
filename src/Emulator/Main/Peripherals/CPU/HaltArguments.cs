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
        public HaltArguments(HaltReason reason, long address = -1, BreakpointType? breakpointType = null)
        {
            Reason = reason;
            Address = address;
            BreakpointType = breakpointType;
        }

        public HaltReason Reason { get; private set; }
        public long Address { get; private set; }
        public BreakpointType? BreakpointType { get; private set; }
    }
}

