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
    public class Micron_MT25Q : GenericSpiFlash
    {
        public Micron_MT25Q(MappedMemory underlyingMemory)
            : base(underlyingMemory, manufacturerId: ManufacturerId, memoryType: MemoryType)
        {
            // original MT25Q supports capacity 8MB to 256MB,
            // but we extended it down to 64KB
            // to become compatible with N25Q line
            if(underlyingMemory.Size < 64.KB() || underlyingMemory.Size > 256.MB())
            {
                throw new ConstructionException("Size of the underlying memory must be in range 64KB - 256MB");
            }
        }

        private const byte ManufacturerId = 0x20;
        private const byte MemoryType = 0xBB; // device voltage: 1.8V
    }
}
