//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
namespace Antmicro.Renode.Peripherals.CPU
{
    public class HaltArguments
    {
        public HaltArguments(HaltReason reason, ulong? address = null, BreakpointType? breakpointType = null)
        {
            Reason = reason;
            Address = address;
            BreakpointType = breakpointType;
        }

        public HaltReason Reason { get; private set; }
        public ulong? Address { get; private set; }
        public BreakpointType? BreakpointType { get; private set; }
    }
}

