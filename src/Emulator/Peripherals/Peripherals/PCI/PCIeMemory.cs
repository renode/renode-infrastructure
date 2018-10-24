//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.PCI.BAR;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.PCI
{
    public class PCIeMemory : PCIeEndpoint
    {
        public PCIeMemory(IPCIeRouter parent, uint size) : base(parent)
        {
            this.memory = new uint[size / 4];
            for(var i = 0u; i < HeaderType.MaxNumberOfBARs(); ++i)
            {
                AddBaseAddressRegister(i, new MemoryBaseAddressRegister(size, MemoryBaseAddressRegister.BarType.LocateIn32Bit, true));
            }
        }

        public override void Reset()
        {
            base.Reset();
            Array.Clear(memory, 0, memory.Length);
        }

        protected override void WriteDoubleWordToBar(uint bar, long offset, uint value)
        {
            offset /= 4; //we keep uints, so we divide the offset
            if(offset >= memory.Length)
            {
                this.Log(LogLevel.Warning, "Trying to write 0x{0:X} out of memory range, at offset 0x{1:X}. Size of memory is 0x{2:X}.", value, offset, memory.Length);
                return;
            }
            memory[offset] = value;
        }

        protected override uint ReadDoubleWordFromBar(uint bar, long offset)
        {
            offset /= 4; //we keep uints, so we divide the offset
            if(offset >= memory.Length)
            {
                this.Log(LogLevel.Warning, "Trying to read from offset 0x{0:X}, beyond the memory range 0x{1:X}.", offset, memory.Length);
                return 0u;
            }
            return memory[offset];
        }

        private uint[] memory;
    }
}