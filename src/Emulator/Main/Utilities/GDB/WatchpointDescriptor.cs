//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Utilities.GDB
{
    public class WatchpointDescriptor
    {
        public WatchpointDescriptor(ulong address, SysbusAccessWidth width, Access access, BusHookDelegate hook)
        {
            Address = address;
            Width = width;
            Access = access;
            Hook = hook;
        }

        public override bool Equals(object obj)
        {
            var objAsBreakpointDescriptor = obj as WatchpointDescriptor;
            if(objAsBreakpointDescriptor == null)
            {
                return false;
            }

            return objAsBreakpointDescriptor.Address == Address
                    && objAsBreakpointDescriptor.Width == Width
                    && objAsBreakpointDescriptor.Access == Access
                    && objAsBreakpointDescriptor.Hook == Hook;
        }

        public override int GetHashCode()
        {
            return 17 * (int)Address
                + 23 * (int)Width
                + 17 * (int)Access
                + 17 * Hook.GetHashCode();
        }

        public readonly ulong Address;
        public readonly SysbusAccessWidth Width;
        public readonly Access Access;
        public readonly BusHookDelegate Hook;
    }
}