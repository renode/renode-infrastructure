//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // This is just a mock handling hint flags
    public class OpenTitan_ClockManager : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_ClockManager(IMachine machine, OpenTitan_BigNumberAccelerator otbn) : base(machine)
        {
            this.otbn = otbn;
            DefineRegisters();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Register.ClockHints.Define(this, 0xF)
                .WithFlag(0, out aesHint,name: "CLK_MAIN_AES_HINT")
                .WithFlag(1, out hmacHint,name: "CLK_MAIN_HMAC_HINT")
                .WithFlag(2, out kmacHint,name: "CLK_MAIN_KMAC_HINT")
                .WithFlag(3, out otbnHint,name: "CLK_MAIN_OTBN_HINT")
                .WithReservedBits(4, 28)
            ;

            Register.ClockHintsStatus.Define(this, 0xF)
                .WithFlag(0, FieldMode.Read, name: "CLK_MAIN_AES_VAL", valueProviderCallback: _ => aesHint.Value)
                .WithFlag(1, FieldMode.Read, name: "CLK_MAIN_HMAC_VAL", valueProviderCallback: _ => hmacHint.Value)
                .WithFlag(2, FieldMode.Read, name: "CLK_MAIN_KMAC_VAL", valueProviderCallback: _ => kmacHint.Value)
                .WithFlag(3, FieldMode.Read, name: "CLK_MAIN_OTBN_VAL", valueProviderCallback: _ => otbnHint.Value | !otbn.IsIdle)
                .WithReservedBits(4, 28)
            ;
        }

        private IFlagRegisterField aesHint;
        private IFlagRegisterField hmacHint;
        private IFlagRegisterField kmacHint;
        private IFlagRegisterField otbnHint;

        private readonly OpenTitan_BigNumberAccelerator otbn;

        private enum Register
        {
            ClockHints = 0x1c,
            ClockHintsStatus = 0x20
        }
    }
}
