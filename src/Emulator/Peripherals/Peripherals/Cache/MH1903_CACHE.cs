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
            // InstructionData0 at offset 0x00
            Registers.InstructionData0.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "InstructionData0");

            // InstructionData1 at offset 0x04
            Registers.InstructionData1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "InstructionData1");

            // InstructionData2 at offset 0x08
            Registers.InstructionData2.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "InstructionData2");

            // InstructionData3 at offset 0x0C
            Registers.InstructionData3.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "InstructionData3");

            // KeyData0 at offset 0x10
            Registers.KeyData0.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "KeyData0");

            // KeyData1 at offset 0x14
            Registers.KeyData1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "KeyData1");

            // KeyData2 at offset 0x18
            Registers.KeyData2.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "KeyData2");

            // KeyData3 at offset 0x1C
            Registers.KeyData3.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "KeyData3");

            // ChipSelect at offset 0x20
            Registers.ChipSelect.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "ChipSelect");

            // Reference at offset 0x24 - when written, gets cleared to 0
            Registers.Reference.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "Reference",
                    writeCallback: (_, __) => { /* Writing clears it, do nothing */ });

            // Reserved area 0x28-0x3F (6 words)
            Registers.Reserved28.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved2C.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved30.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved34.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved38.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved3C.Define(this)
                .WithReservedBits(0, 32);

            // Configuration at offset 0x40
            Registers.Configuration.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Configuration");

            // Reserved area 0x44-0x73 (12 words)
            Registers.Reserved44.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved48.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved4C.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved50.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved54.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved58.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved5C.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved60.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved64.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved68.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved6C.Define(this)
                .WithReservedBits(0, 32);
            Registers.Reserved70.Define(this)
                .WithReservedBits(0, 32);

            // StartAddress at offset 0x74
            Registers.StartAddress.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "StartAddress");

            // EndAddress at offset 0x78
            Registers.EndAddress.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "EndAddress");
        }

        private enum Registers : long
        {
            InstructionData0 = 0x00,
            InstructionData1 = 0x04,
            InstructionData2 = 0x08,
            InstructionData3 = 0x0C,
            KeyData0 = 0x10,
            KeyData1 = 0x14,
            KeyData2 = 0x18,
            KeyData3 = 0x1C,
            ChipSelect = 0x20,
            Reference = 0x24,
            // Reserved 0x28-0x3F (6 words)
            Reserved28 = 0x28,
            Reserved2C = 0x2C,
            Reserved30 = 0x30,
            Reserved34 = 0x34,
            Reserved38 = 0x38,
            Reserved3C = 0x3C,
            Configuration = 0x40,
            // Reserved 0x44-0x70 (12 words)
            Reserved44 = 0x44,
            Reserved48 = 0x48,
            Reserved4C = 0x4C,
            Reserved50 = 0x50,
            Reserved54 = 0x54,
            Reserved58 = 0x58,
            Reserved5C = 0x5C,
            Reserved60 = 0x60,
            Reserved64 = 0x64,
            Reserved68 = 0x68,
            Reserved6C = 0x6C,
            Reserved70 = 0x70,
            StartAddress = 0x74,
            EndAddress = 0x78,
        }
    }
}
