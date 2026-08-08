//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

// Known limitation, deliberate for this version:
// This models the ICACHE register interface over a no-op cache. HMONR/MMONR always read 0
// and must not be read as a measurement; CACHEINV completes in zero emulated time so BUSYF
// is never observable as set.
//
// Future work: line-level cache storage with access-driven hit and miss counters, and
// invalidation that occupies emulated time so BUSYF is observable.

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Cache
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class STM32H5_ICACHE : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32H5_ICACHE(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            bsyendf = false;
            errf = false;
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlag(0, name: "EN")
                .WithFlag(1, FieldMode.Write, name: "CACHEINV",
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            // Invalidation completes instantly in a no-op cache.
                            bsyendf = true;
                        }
                    })
                .WithFlag(2, name: "WAYSEL")
                .WithReservedBits(3, 13)
                .WithFlag(16, name: "HITMEN")
                .WithFlag(17, name: "MISSMEN")
                .WithFlag(18, FieldMode.Write, name: "HITMRST",
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            hitMonitor.Value = 0;
                        }
                    })
                .WithFlag(19, FieldMode.Write, name: "MISSMRST",
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            missMonitor.Value = 0;
                        }
                    })
                .WithReservedBits(20, 12);

            Registers.Status.Define(this)
                .WithFlag(0, FieldMode.Read, name: "BUSYF",
                    valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "BSYENDF",
                    valueProviderCallback: _ => bsyendf)
                .WithFlag(2, FieldMode.Read, name: "ERRF",
                    valueProviderCallback: _ => errf)
                .WithReservedBits(3, 29);

            Registers.InterruptEnable.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, name: "BSYENDIE")
                .WithFlag(2, name: "ERRIE")
                .WithReservedBits(3, 29);

            Registers.FlagClear.Define(this, name: "ICACHE_FCR")
                .WithReservedBits(0, 1)
                .WithFlag(1, FieldMode.Write, name: "CBSYENDF",
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            bsyendf = false;
                        }
                    })
                .WithFlag(2, FieldMode.Write, name: "CERRF",
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            errf = false;
                        }
                    })
                .WithReservedBits(3, 29);

            Registers.HitMonitor.Define(this)
                .WithValueField(0, 32, out hitMonitor, FieldMode.Read, name: "HITMON");

            Registers.MissMonitor.Define(this)
                .WithValueField(0, 32, out missMonitor, FieldMode.Read, name: "MISSMON");

            Registers.RegionConfiguration0.Define(this)
                .WithTag("CRR0", 0, 32);

            Registers.RegionConfiguration1.Define(this)
                .WithTag("CRR1", 0, 32);

            Registers.RegionConfiguration2.Define(this)
                .WithTag("CRR2", 0, 32);

            Registers.RegionConfiguration3.Define(this)
                .WithTag("CRR3", 0, 32);
        }

        private IValueRegisterField hitMonitor;
        private IValueRegisterField missMonitor;
        private bool bsyendf;
        private bool errf;

        private enum Registers
        {
            Control = 0x00,
            Status = 0x04,
            InterruptEnable = 0x08,
            FlagClear = 0x0C,
            HitMonitor = 0x10,
            MissMonitor = 0x14,
            // 0x18-0x1C reserved
            RegionConfiguration0 = 0x20,
            RegionConfiguration1 = 0x24,
            RegionConfiguration2 = 0x28,
            RegionConfiguration3 = 0x2C,
        }
    }
}
