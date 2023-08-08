//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class LiteX_SoC_Controller_CSR32 : BasicDoubleWordPeripheral, IKnownSize
    {
        public LiteX_SoC_Controller_CSR32(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0xC;

        private void DefineRegisters()
        {
            Registers.Reset.Define(this)
                .WithTag("reset", 0, 1)
                .WithReservedBits(1, 31)
            ;

            Registers.Scratch.Define(this, 0x12345678)
                .WithValueField(0, 32, name: "scratch")
            ;

            Registers.BusErrors.Define(this)
                .WithTag("bus errors", 0, 32)
            ;
        }

        private enum Registers
        {
            Reset = 0x0,
            Scratch = 0x4,
            BusErrors = 0x8,
        }
    }
}
