using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Cache
{
    public class MH1903_CACHE : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_CACHE(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x80;

        private void DefineRegisters()
        {
            // CACHE_I0 at offset 0x00
            Registers.CACHE_I0.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_i0");

            // CACHE_I1 at offset 0x04
            Registers.CACHE_I1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_i1");

            // CACHE_I2 at offset 0x08
            Registers.CACHE_I2.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_i2");

            // CACHE_I3 at offset 0x0C
            Registers.CACHE_I3.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_i3");

            // CACHE_K0 at offset 0x10
            Registers.CACHE_K0.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_k0");

            // CACHE_K1 at offset 0x14
            Registers.CACHE_K1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_k1");

            // CACHE_K2 at offset 0x18
            Registers.CACHE_K2.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_k2");

            // CACHE_K3 at offset 0x1C
            Registers.CACHE_K3.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_k3");

            // CACHE_CS at offset 0x20
            Registers.CACHE_CS.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_cs");

            // CACHE_REF at offset 0x24 - when written, gets cleared to 0
            Registers.CACHE_REF.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "cache_ref",
                    writeCallback: (_, __) => { /* Writing clears it, do nothing */ });

            // Reserved area 0x28-0x3F (6 words)
            Registers.CACHE_RSVD0_0.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD0_1.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD0_2.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD0_3.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD0_4.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD0_5.Define(this)
                .WithReservedBits(0, 32);

            // CACHE_CONFIG at offset 0x40
            Registers.CACHE_CONFIG.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_config");

            // Reserved area 0x44-0x73 (12 words)
            Registers.CACHE_RSVD1_0.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_1.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_2.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_3.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_4.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_5.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_6.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_7.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_8.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_9.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_10.Define(this)
                .WithReservedBits(0, 32);
            Registers.CACHE_RSVD1_11.Define(this)
                .WithReservedBits(0, 32);

            // CACHE_SADDR at offset 0x74
            Registers.CACHE_SADDR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_saddr");

            // CACHE_EADDR at offset 0x78
            Registers.CACHE_EADDR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "cache_eaddr");
        }

        private enum Registers : long
        {
            CACHE_I0 = 0x00,
            CACHE_I1 = 0x04,
            CACHE_I2 = 0x08,
            CACHE_I3 = 0x0C,
            CACHE_K0 = 0x10,
            CACHE_K1 = 0x14,
            CACHE_K2 = 0x18,
            CACHE_K3 = 0x1C,
            CACHE_CS = 0x20,
            CACHE_REF = 0x24,
            // Reserved 0x28-0x3F (6 words)
            CACHE_RSVD0_0 = 0x28,
            CACHE_RSVD0_1 = 0x2C,
            CACHE_RSVD0_2 = 0x30,
            CACHE_RSVD0_3 = 0x34,
            CACHE_RSVD0_4 = 0x38,
            CACHE_RSVD0_5 = 0x3C,
            CACHE_CONFIG = 0x40,
            // Reserved 0x44-0x73 (12 words)
            CACHE_RSVD1_0 = 0x44,
            CACHE_RSVD1_1 = 0x48,
            CACHE_RSVD1_2 = 0x4C,
            CACHE_RSVD1_3 = 0x50,
            CACHE_RSVD1_4 = 0x54,
            CACHE_RSVD1_5 = 0x58,
            CACHE_RSVD1_6 = 0x5C,
            CACHE_RSVD1_7 = 0x60,
            CACHE_RSVD1_8 = 0x64,
            CACHE_RSVD1_9 = 0x68,
            CACHE_RSVD1_10 = 0x6C,
            CACHE_RSVD1_11 = 0x70,
            CACHE_SADDR = 0x74,
            CACHE_EADDR = 0x78,
        }
    }
}
