using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MH1903_TRNG : BasicDoubleWordPeripheral, IKnownSize
    {
        public MH1903_TRNG(IMachine machine) : base(machine)
        {
            DefineRegisters();
            ticker = new LimitTimer(machine.ClockSource, 32768, this, nameof(ticker), 32, direction: Direction.Descending, eventEnabled: true, autoUpdate: true);
            ticker.LimitReached += UpdateState;
            ticker.Enabled = true;
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x100;

        public override void Reset()
        {
            base.Reset();
            ticker.Reset();
            IRQ.Unset();
        }

        private void DefineRegisters()
        {
            Registers.RngControlStatusRegister.Define(this, resetValue: 0x00000020)
                .WithFlag(0, name: "RNG0Sample128", mode: FieldMode.Read, valueProviderCallback: _ => true)
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "RNG0Attack", mode: FieldMode.Read, valueProviderCallback: _ => false)
                .WithReservedBits(3, 1)
                .WithFlag(4, name: "RngInterruptEnable")
                .WithReservedBits(5, 27);

            Registers.RngDataRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "RngData", mode: FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        // Increment RNG_INDEX
                        var index = RegistersCollection.Read((long)Registers.RngIndexRegister);
                        index = (index + 1);
                        // If overflow, set the overflow flag
                        if((index & 0x3) == 0)
                        {
                            index = 3;
                            index |= (uint)RNG_FIFO.RNG0FifoOverflow;
                        }

                        RegistersCollection.Write((long)Registers.RngIndexRegister, index);
                        return (uint)rng.Next();
                    });

            Registers.ReservedRegister.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 32);

            Registers.RngAnalogMeasurementRegister.Define(this, resetValue: 0x000FF486)
                .WithValueField(0, 32, name: "RngAnalogMeasurement");

            Registers.RngPseudoNoiseRegister.Define(this, resetValue: 0x69D84C18)
                .WithValueField(0, 32, name: "RngPseudoNoise");

            Registers.RngIndexRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 2, name: "RngIndex")
                .WithReservedBits(2, 29)
                .WithFlag(31, name: "RngFifoOverflow");
        }

        private void UpdateState()
        {
            // Set RNG_CSR to RNG0_S128
            var csr = RegistersCollection.Read((long)Registers.RngControlStatusRegister);
            csr |= (uint)RNGCSRBit.RNG0Sample128;
            RegistersCollection.Write((long)Registers.RngControlStatusRegister, csr);
            // if interrupt is enabled, trigger it
            if((csr & (uint)RNGCSRBit.RngInterruptEnable) != 0 && !IRQ.IsSet)
            {
                this.Log(LogLevel.Debug, "TRNG interrupt triggered.");
                IRQ.Set();
            }
        }

        private readonly PseudorandomNumberGenerator rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private readonly LimitTimer ticker;

        public enum RNGCSRBit : ulong
        {
            RNG0Sample128 = 1u << 0,
            RNG0Attack = 1u << 2,
            RngInterruptEnable = 1u << 4,
        }

        public enum RNG_FIFO : ulong
        {
            // FIFO Depth
            RNG0Index = 0x00000003,

            // RNG0FifoOverflow RNG FIFO overflow bit
            // FIFO read overflow indicator, when a newly generated 128-bit random number is read,
            // and then a read operation is performed on the FIFO, this is set to 1, and a write
            // operation clears it to 0.
            RNG0FifoOverflow = 1u << 31,
        }

        private enum Registers : long
        {
            RngControlStatusRegister = 0x00,
            RngDataRegister = 0x04,
            ReservedRegister = 0x08,
            RngAnalogMeasurementRegister = 0x0C,
            RngPseudoNoiseRegister = 0x10,
            RngIndexRegister = 0x14,
        }
    }
}
