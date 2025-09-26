//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public abstract class ExternalPMPBase : IPeripheral
    {
        public virtual void RegisterCPU(BaseRiscV cpu)
        {
            if(this.cpu != null)
            {
                throw new RegistrationException($"CPU already registered with this ${nameof(ExternalPMPBase)}");
            }
            this.cpu = cpu;
        }

        public abstract void Reset();

        public abstract void ConfigCSRWrite(uint registerIndex, ulong value);

        public abstract ulong ConfigCSRRead(uint registerIndex);

        public abstract void AddressCSRWrite(uint registerIndex, ulong value);

        public abstract ulong AddressCSRRead(uint registerIndex);

        public abstract byte GetAccess(ulong address, ulong size, AccessType accessType);

        public abstract bool TryGetOverlappingRegion(ulong address, ulong size, uint startingIndex, out uint overlappingIndex);

        public abstract bool IsAnyRegionLocked();

        protected static byte EncodePermissions(bool read, bool write, bool execute)
        {
            var result = (byte) 0;
            BitHelper.SetBit(ref result, 0, read);
            BitHelper.SetBit(ref result, 1, write);
            BitHelper.SetBit(ref result, 2, execute);
            return result;
        }

        protected BaseRiscV cpu;
    }
}