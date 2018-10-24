//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.PCI.Capabilities
{
    public class Capability
    {
        public Capability(IPCIePeripheral parent, byte id, uint size)
        {
            Registers = new List<DoubleWordRegister>();
            Id = id;
            Size = size;
            this.parent = parent;
        }

        public byte Id { get; }
        public uint Size { get; }
        public byte NextCapability { get; set; }
        public List<DoubleWordRegister> Registers { get; }

        protected readonly IPCIePeripheral parent;
    }
}
