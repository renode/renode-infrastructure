//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class WindowMMUBusController : BusControllerProxy
    {
        public WindowMMUBusController(IEmulationElement emulationParent, IBusController parentController) : base(parentController)
        {
            this.emulationParent = emulationParent;
            Windows = new List<MMUWindow>();
        }

        public void AssertWindowsAreValid()
        {
            for(var i = 0; i < Windows.Count; i++)
            {
                Windows[i].AssertIsValid();
                for(var j = 0; j < Windows.Count; j++)
                {
                    if(i != j && Windows[i].ContainsAddress(Windows[j].Start) && Windows[j].Length > 0)
                    {
                        emulationParent.Log(LogLevel.Error, "MMUWindows (with indicies {0} and {1}) overlap each other.", i, j);
                    }
                }
            }
        }

        public event Action<ulong, BusAccessPrivileges, int?> OnFault;

        public List<MMUWindow> Windows { get; }

        protected override bool ValidateOperation(ref ulong address, BusAccessPrivileges accessType, IPeripheral context = null)
        {
            if(TryFindWindowIndex(address, out var index))
            {
                var privileges = Windows[index].Privileges;
                if((privileges & accessType) == accessType)
                {
                    address = Windows[index].TranslateAddress(address);
                    return true;
                }
                MMUFaultHandler(address, accessType, index);
            }
            else
            {
                MMUFaultHandler(address, accessType, null);
            }
            return false;
        }

        private bool TryFindWindowIndex(ulong address, out int index)
        {
            for(index = 0; index < Windows.Count; index++)
            {
                if(Windows[index].ContainsAddress(address))
                {
                    if(Windows[index].Valid)
                    {
                        return true;
                    }
                    else
                    {
                        emulationParent.Log(LogLevel.Warning, "The window at index {0} match the address, but isn't validated sucesfully.", index);
                    }
                }
            }
            index = -1;
            return false;
        }

        private void MMUFaultHandler(ulong address, BusAccessPrivileges accessType, int? windowIndex)
        {
            emulationParent.Log(LogLevel.Noisy, "IOMMU fault at 0x{0:X} when trying to access as {1}", address, accessType);
            OnFault?.Invoke(address, accessType, windowIndex);

            if(windowIndex == null)
            {
                emulationParent.Log(LogLevel.Error, "IOMMU fault - the address 0x{0:X} is not specified in any of the existing ranges", address);
            }
        }

        private readonly IEmulationElement emulationParent;

        public class MMUWindow
        {
            public MMUWindow(IEmulationElement emulationParent)
            {
                this.emulationParent = emulationParent;
            }

            public bool ContainsAddress(ulong address)
            {
                return address >= Start && address < End;
            }

            public void AssertIsValid()
            {
                Valid = true;
                if(Start > End)
                {
                    emulationParent.Log(LogLevel.Error, "MMUWindow has start address (0x{0:x}) grater than end address (0x{1:x}).", Start, End);
                    Valid = false;
                }

                if(Offset < 0 && Start < (ulong)(-Offset))
                {
                    emulationParent.Log(LogLevel.Error, "MMUWindow has incorrect offset ({0:d}) in relation to the start address (0x{1:x}).", Offset, Start);
                    Valid = false;
                }
                else if(Offset > 0 && End > UInt64.MaxValue - (ulong)Offset)
                {
                    emulationParent.Log(LogLevel.Error, "MMUWindow has incorrect offset ({0:d}) in relation to the end address (0x{1:x}).", Offset, End);
                    Valid = false;
                }

            }

            public ulong TranslateAddress(ulong address)
            {
                if(Offset < 0)
                {
                    return checked(address - (ulong)(-Offset));
                }
                else
                {
                    return checked(address + (ulong)Offset);
                }
            }

            public ulong Start { get; set; }
            public ulong End { get; set; }
            public ulong Length => checked(End - Start);
            public long Offset { get; set; }
            public BusAccessPrivileges Privileges { get; set; }
            public bool Valid { get; private set; }

            private readonly IEmulationElement emulationParent;
        }
    }
}
