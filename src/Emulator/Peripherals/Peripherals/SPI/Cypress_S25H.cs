//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Cypress_S25H : GenericSpiFlash
    {
        public Cypress_S25H(MappedMemory underlyingMemory)
            : base(underlyingMemory, manufacturerId: ManufacturerId, memoryType: MemoryType,
                   writeStatusCanSetWriteEnable: true, extendedDeviceId: ExtendedDeviceID,
                   remainingIdBytes: RemainingIDBytes, deviceConfiguration: DeviceConfiguration)
        {
            if(underlyingMemory.Size < 32.MB() || underlyingMemory.Size > 128.MB())
            {
                throw new ConstructionException("Size of the underlying memory must be in range 32MB - 128MB");
            }
        }

        protected override byte GetCapacityCode()
        {
            return (byte)BitHelper.GetMostSignificantSetBitIndex((ulong)this.UnderlyingMemory.Size);
        }

        private const byte ManufacturerId = 0x34;
        private const byte MemoryType = 0x2B; // HS
        private const byte RemainingIDBytes = 0x0F;
        private const byte ExtendedDeviceID = 0x03;
        private const byte DeviceConfiguration = 0x90;
    }
}
