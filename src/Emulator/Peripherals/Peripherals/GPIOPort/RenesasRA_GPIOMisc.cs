//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class RenesasRA_GPIOMisc : BasicBytePeripheral, IKnownSize
    {
        public RenesasRA_GPIOMisc(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            PFSWriteEnabled = false;
        }

        public bool PFSWriteEnabled { get; private set; }

        public long Size => 0x10;

        protected override void DefineRegisters()
        {
            Registers.EthernetControl.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("PHYMODE0", 4)
                .WithReservedBits(5, 3)
            ;

            Registers.WriteProtect.Define(this, 0x80)
                .WithReservedBits(0, 6)
                .WithFlag(6, name: "PFSWE",
                    valueProviderCallback: _ => PFSWriteEnabled,
                    changeCallback: (_, value) =>
                    {
                        if(pfsWriteEnableDisabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to write PFSE, but B0WI is asserted");
                            return;
                        }

                        PFSWriteEnabled = value;
                    }
                )
                // This is defined _after_ PFSWE to assert that this field will be
                // written zero before any change to PFSWE field.
                .WithFlag(7, out pfsWriteEnableDisabled, name: "B0WI")
            ;
        }

        private IFlagRegisterField pfsWriteEnableDisabled;

        private enum Registers
        {
            EthernetControl = 0x00,
            WriteProtect = 0x03,
            WriteProtectForSecure = 0x05,
        }
    }
}
