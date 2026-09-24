using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class MH1903_ADC : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_ADC(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x100;

        private void DefineRegisters()
        {
            // Control1 at 0x00
            Registers.Control1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Control1");

            // Status at 0x04
            Registers.Status.Define(this, resetValue: 0x00000001)
                .WithFlag(0, FieldMode.Read, name: "Ready", valueProviderCallback: _ => true)
                .WithReservedBits(1, 31);

            // Fifo at 0x08
            Registers.Fifo.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Fifo");

            // Data at 0x0C
            Registers.Data.Define(this, resetValue: 0x00000D70)  // 3440 = ~4.2V
                .WithValueField(0, 16, FieldMode.Read, name: "Data",
                    valueProviderCallback: _ => adcData);

            // FifoLevel at 0x10
            Registers.FifoLevel.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "FifoLevel");

            // FifoThreshold at 0x14
            Registers.FifoThreshold.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "FifoThreshold");

            // Control2 at 0x18
            Registers.Control2.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "Control2");
        }

        private readonly ushort adcData = 0x0D70;  // 3440 = ~4.2V

        private enum Registers : long
        {
            Control1 = 0x00,
            Status = 0x04,
            Fifo = 0x08,
            Data = 0x0C,
            FifoLevel = 0x10,
            FifoThreshold = 0x14,
            Control2 = 0x18,
        }
    }
}
