//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class Micron_MT25Q : GenericSpiFlash
    {
        public Micron_MT25Q(MappedMemory underlyingMemory) : base(underlyingMemory)
        {
        }
    }
}
