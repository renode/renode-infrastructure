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
            // ADC_CR1 - Control Register 1 at 0x00
            Registers.ADC_CR1.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "adc_cr1");

            // ADC_SR - Status Register at 0x04
            Registers.ADC_SR.Define(this, resetValue: 0x00000001)
                .WithFlag(0, FieldMode.Read, name: "adc_ready", valueProviderCallback: _ => true)
                .WithReservedBits(1, 31);

            // ADC_FIFO - FIFO Register at 0x08
            Registers.ADC_FIFO.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "adc_fifo");

            // ADC_DATA - Data Register at 0x0C
            Registers.ADC_DATA.Define(this, resetValue: 0x00000D70)  // 3440 = ~4.2V
                .WithValueField(0, 16, FieldMode.Read, name: "adc_data",
                    valueProviderCallback: _ => adcData);

            // ADC_FIFO_FL - FIFO Level at 0x10
            Registers.ADC_FIFO_FL.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "adc_fifo_fl");

            // ADC_FIFO_THR - FIFO Threshold at 0x14
            Registers.ADC_FIFO_THR.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "adc_fifo_thr");

            // ADC_CR2 - Control Register 2 at 0x18
            Registers.ADC_CR2.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "adc_cr2");
        }

        private readonly ushort adcData = 0x0D70;  // 3440 = ~4.2V

        private enum Registers : long
        {
            ADC_CR1 = 0x00,
            ADC_SR = 0x04,
            ADC_FIFO = 0x08,
            ADC_DATA = 0x0C,
            ADC_FIFO_FL = 0x10,
            ADC_FIFO_THR = 0x14,
            ADC_CR2 = 0x18,
        }
    }
}
