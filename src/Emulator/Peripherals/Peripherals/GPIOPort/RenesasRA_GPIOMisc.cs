//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class RenesasRA_GPIOMisc : BasicBytePeripheral, IKnownSize
    {
        public RenesasRA_GPIOMisc(IMachine machine, Version version = Version.Default) : base(machine)
        {
            this.version = version;
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            PFSWriteEnabled = false;
        }

        public bool PFSWriteEnabled { get; private set; }

        public long Size
        {
            get
            {
                switch(version)
                {
                    case Version.Default:
                        return 0x10;
                    case Version.RA8:
                        return 0x20;
                    default:
                        throw new Exception("unreachable");
                }
            }
        }

        protected override void DefineRegisters()
        {
            switch(version)
            {
                case Version.Default:
                    DefineRegistersDefault();
                    break;
                case Version.RA8:
                    DefineRegistersRA8();
                    break;
                default:
                    throw new Exception("unreachable");
            }
        }

        private void DefineRegistersDefault()
        {
            Registers.EthernetControl.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("PHYMODE0", 4)
                .WithReservedBits(5, 3)
            ;

            DefineWriteProtectRegister(Registers.WriteProtect);
        }

        private void DefineRegistersRA8()
        {
            RegistersRA8.EthernetControl.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("PHYMODE0", 4)
                .WithReservedBits(5, 3)
            ;

            DefineWriteProtectRegister(RegistersRA8.WriteProtectSecure);
        }

        private void DefineWriteProtectRegister<T>(T register)
            where T : IConvertible
        {
            RegistersCollection.DefineRegister(Convert.ToInt64(register), 0x80)
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

        private readonly Version version;

        public enum Version
        {
            Default,
            RA8,
        }

        private enum Registers
        {
            EthernetControl = 0x00,
            WriteProtect = 0x03,
            WriteProtectForSecure = 0x05,
        }

        private enum RegistersRA8
        {
            EthernetControl     = 0x00,
            WriteProtect        = 0x0C,
            WriteProtectSecure  = 0x14,
        }
    }
}
