//
// Copyright (c) 2010 - 2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class LiteX_SoC_Controller : BasicDoubleWordPeripheral, IKnownSize
    {
        public LiteX_SoC_Controller(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.Reset.Define(this)
                .WithTag("reset", 0, 1)
                .WithReservedBits(1, 7)
                .WithIgnoredBits(8, 24)
            ;

            Registers.Scratch0.Define(this, 0x12)
                .WithValueField(0, 8, name: "scratch0")
                .WithIgnoredBits(8, 24)
            ;

            Registers.Scratch1.Define(this, 0x34)
                .WithValueField(0, 8, name: "scratch1")
                .WithIgnoredBits(8, 24)
            ;

            Registers.Scratch2.Define(this, 0x56)
                .WithValueField(0, 8, name: "scratch2")
                .WithIgnoredBits(8, 24)
            ;

            Registers.Scratch3.Define(this, 0x78)
                .WithValueField(0, 8, name: "scratch3")
                .WithIgnoredBits(8, 24)
            ;

            Registers.BusErrors0.Define(this)
                .WithTag("bus errors 0", 0, 8)
                .WithIgnoredBits(8, 24)
            ;

            Registers.BusErrors1.Define(this)
                .WithTag("bus errors 1", 0, 8)
                .WithIgnoredBits(8, 24)
            ;

            Registers.BusErrors2.Define(this)
                .WithTag("bus errors 2", 0, 8)
                .WithIgnoredBits(8, 24)
            ;

            Registers.BusErrors3.Define(this)
                .WithTag("bus errors 3", 0, 8)
                .WithIgnoredBits(8, 24)
            ;
        }

        private enum Registers
        {
            Reset = 0x0,
            Scratch0 = 0x4,
            Scratch1 = 0x8,
            Scratch2 = 0xC,
            Scratch3 = 0x10,
            BusErrors0 = 0x14,
            BusErrors1 = 0x18,
            BusErrors2 = 0x1C,
            BusErrors3 = 0x20
        }
    }
}
