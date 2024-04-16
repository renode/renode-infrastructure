//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Memory;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Macronix_MX25R : GenericSpiFlash
    {
        public Macronix_MX25R(MappedMemory underlyingMemory)
            : base(underlyingMemory, manufacturerId: ManufacturerId, memoryType: MemoryType,
                   writeStatusCanSetWriteEnable: false)
        {
            statusRegister
                .WithValueField(2, 4,
                    writeCallback: (_, value) => UpdateLockedRange((uint)value), name: "BP (level of protected block)")
                .WithTaggedFlag("QE (Quad Enable)", 6)
                .WithTaggedFlag("SRWD (Status register write protect)", 7);

           configurationRegister
               .WithReservedBits(0, 3)
               .WithFlag(3, out topBottom, name: "TB (top/bottom selected)")
               .WithReservedBits(4, 2)
               .WithTaggedFlag("DC (Dummy Cycle)", 6)
               .WithReservedBits(7, 1)
               .WithReservedBits(8, 1)
               .WithTaggedFlag("L/H Switch", 9)
               .WithReservedBits(10, 6);
        }

        private void UpdateLockedRange(uint blockProtectionValue)
        {
            if(blockProtectionValue == 0)
            {
                lockedRange = null;
                return;
            }

            // If protection is enabled (BP != 0), the minimum protected sector count
            // is 1 (1 << 0). The maximum is 16384 (1 << 14).
            var protectedSectorShift = (int)(blockProtectionValue - 1);
            var protectedSectorCount = 1 << protectedSectorShift;

            // Protected sectors can cover the whole flash.
            var protectedSize = Math.Min(sectorSize * protectedSectorCount, UnderlyingMemory.Size);
            var start = topBottom.Value ? 0 : UnderlyingMemory.Size - protectedSize;
            lockedRange = new Range((ulong)start, (ulong)protectedSize);
        }

        private readonly IFlagRegisterField topBottom;

        private const byte ManufacturerId = 0xC2;
        private const byte MemoryType = 0x28;
    }
}
