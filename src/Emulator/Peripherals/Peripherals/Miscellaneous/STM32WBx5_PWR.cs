//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2022 SICK AG
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public sealed class STM32WBx5_PWR : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32WBx5_PWR(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        private void DefineRegisters()
        {
            Registers.PowerControl1.Define(this, 0x00000200)
                .WithValueField(0, 3, name: "LPMS") // TODO: these do not get reset when exiting standby
                .WithReservedBits(3, 1)
                .WithTaggedFlag("FPDR", 4) // TODO: this can only be written after unlocking, see 6.6.1
                .WithTaggedFlag("FPDS", 5)
                .WithReservedBits(6, 2)
                .WithFlag(8, out var backupDomainDisabled, name: "DBP")
                .WithValueField(9, 2, name: "VOS")
                .WithReservedBits(11, 3)
                .WithTaggedFlag("LPR", 14)
                .WithReservedBits(15, 17);

            Registers.Cpu2Control.Define(this)
                .WithValueField(0, 3, name: "LPMS") // TODO: these do not get reset when exiting standby
                .WithReservedBits(3,1)
                .WithTaggedFlag("FPDR", 4)
                .WithTaggedFlag("FPDS", 5)
                .WithReservedBits(6, 8)
                .WithTaggedFlag("BLEEWKUP", 14)
                .WithTaggedFlag("802EWKUP", 15)
                .WithReservedBits(16, 16);
        }

        public long Size => 0x400;

        private enum Registers
        {
            PowerControl1 = 0x0,
            Cpu2Control = 0x80
        }
    }
}
