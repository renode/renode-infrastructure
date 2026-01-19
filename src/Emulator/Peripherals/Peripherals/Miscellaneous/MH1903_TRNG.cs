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
            Registers.RNG_CSR.Define(this, resetValue: 0x00000020)
                .WithFlag(0, name: "RNG0_S128", mode: FieldMode.Read, valueProviderCallback: _ => true)
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "RNG0_ATTACK", mode: FieldMode.Read, valueProviderCallback: _ => false)
                .WithReservedBits(3, 1)
                .WithFlag(4, name: "RNG0_INT_EN")
                .WithReservedBits(5, 27);

            Registers.RNG_DATA.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, name: "rng_data", mode: FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        // Increment RNG_INDEX
                        var index = RegistersCollection.Read((long)Registers.RNG_INDEX);
                        index = (index + 1);
                        // If overflow, set the overflow flag
                        if((index & 0x3) == 0)
                        {
                            index = 3;
                            index |= (uint)RNG_FIFO.RNG0_FIFO_OVERFLOW;
                        }

                        RegistersCollection.Write((long)Registers.RNG_INDEX, index);
                        return (uint)rng.Next();
                    });

            Registers.RES.Define(this, resetValue: 0x00000000)
                .WithReservedBits(0, 32);

            Registers.RNG_AMA.Define(this, resetValue: 0x000FF486)
                .WithValueField(0, 32, name: "rng_ama");

            Registers.RNG_PN.Define(this, resetValue: 0x69D84C18)
                .WithValueField(0, 32, name: "rng_pn");

            Registers.RNG_INDEX.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 2, name: "rng_index")
                .WithReservedBits(2, 29)
                .WithFlag(31, name: "rng_fifo_overflow");
        }

        private void UpdateState()
        {
            // Set RNG_CSR to RNG0_S128
            var csr = RegistersCollection.Read((long)Registers.RNG_CSR);
            csr |= (uint)RNGCSRBit.RNG0_S128;
            RegistersCollection.Write((long)Registers.RNG_CSR, csr);
            // if interrupt is enabled, trigger it
            if((csr & (uint)RNGCSRBit.RNG_INT_EN) != 0 && !IRQ.IsSet)
            {
                this.Log(LogLevel.Debug, "TRNG interrupt triggered.");
                IRQ.Set();
            }
        }

        private readonly PseudorandomNumberGenerator rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;

        private readonly LimitTimer ticker;

        public enum RNGCSRBit : ulong
        {
            RNG0_S128 = 1u << 0,
            RNG0_ATTACK = 1u << 2,
            RNG_INT_EN = 1u << 4,
        }

        public enum RNG_FIFO : ulong
        {
            // FIFO Depth
            RNG0_INDEX = 0x00000003,

            // RNG0_FIFO_OVERFLOW RNG FIFO overflow bit
            // FIFO read overflow indicator, when a newly generated 128-bit random number is read,
            // and then a read operation is performed on the FIFO, this is set to 1, and a write
            // operation clears it to 0.
            RNG0_FIFO_OVERFLOW = 1u << 31,
        }

        private enum Registers : long
        {
            RNG_CSR = 0x00,
            RNG_DATA = 0x04,
            RES = 0x08,
            RNG_AMA = 0x0C,
            RNG_PN = 0x10,
            RNG_INDEX = 0x14,
        }
    }
}
