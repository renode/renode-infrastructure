//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class TCMConfiguration
    {
        public TCMConfiguration(uint address, ulong size, uint regionIndex, uint interfaceIndex = 0, IMemory memory = null)
        {
            Address = address;
            Size = size;
            InterfaceIndex = interfaceIndex;
            Memory = memory;
            RegionIndex = regionIndex;
        }

        public static bool TryCreate(ICPU cpu, IMemory memory, uint regionIndex, out TCMConfiguration configuration, uint interfaceIndex = 0)
        {
            if(!TCMConfiguration.TryFindRegistrationAddress(cpu.GetMachine().SystemBus, cpu, memory, out var address))
            {
                configuration = default(TCMConfiguration);
                return false;
            }

            configuration = new TCMConfiguration(checked((uint)address), checked((ulong)memory.Size), regionIndex, interfaceIndex, memory);
            return true;
        }

        public static bool TryFindRegistrationAddress(IBusController sysbus, ICPU cpu, IMemory memory, out ulong address)
        {
            address = 0x0ul;
            var busRegistration = ((SystemBus)sysbus).GetRegistrationPoints(memory, cpu)
                .OfType<IBusRegistration>()
                .Where(x => x.Initiator == cpu)
                .SingleOrDefault();

            if(busRegistration == default(IBusRegistration))
            {
                return false;
            }
            address = busRegistration.StartingPoint;
            return true;
        }

        public uint Address { get; }
        public ulong Size { get; }
        public uint InterfaceIndex { get; }
        public uint RegionIndex { get; }
        public IMemory Memory { get; }
    }
}
