using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class MH1903_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public MH1903_GPIO(IMachine machine) : base(machine, PinsPerPort)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x10;

        private void OnBSSRChange(uint newValue)
        {
            // BSRR register layout:
            // Bits [15:0]  - Set bits (BS): writing 1 sets the corresponding GPIO pin
            // Bits [31:16] - Reset bits (BR): writing 1 clears the corresponding GPIO pin
            var resetPins = (newValue >> 16) & 0xFFFF;
            var setPins = newValue & 0xFFFF;

            // Update State[] array to reflect the pin changes
            for(int pin = 0; pin < PinsPerPort; pin++)
            {
                // Check if this pin was set
                if((setPins & (1 << pin)) != 0)
                {
                    WritePin(pin, true);
                }
                // Check if this pin was reset
                if((resetPins & (1 << pin)) != 0)
                {
                    WritePin(pin, false);
                }
            }
        }

        private void WritePin(int number, bool value)
        {
            State[number] = value;
            Connections[number].Set(value);
        }

        private void DefineRegisters()
        {
            // InputOutputDataRegister - Input/Output Data Register
            Registers.InputOutputDataRegister.Define(this, resetValue: 0xFFFF0000)
                .WithValueField(0, 16, name: "OutputDataRegister")  // Output Data Register
                .WithValueField(16, 16, name: "InputDataRegister"); // Input Data Register

            // BitSetResetRegister - Bit Set/Reset Register
            Registers.BitSetResetRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 16, name: "BitSet")   // Bit Set
                .WithValueField(16, 16, name: "BitReset")  // Bit Reset
                .WithChangeCallback((_, newValue) => OnBSSRChange(newValue));

            // OutputEnableRegister - Output Enable Register
            Registers.OutputEnableRegister.Define(this, resetValue: 0x0000FFFF)
                .WithValueField(0, 16, name: "OutputEnable")  // Output Enable
                .WithReservedBits(16, 16);

            // PullUpEnableRegister - Pull-Up Enable Register
            Registers.PullUpEnableRegister.Define(this, resetValue: 0x0000FFFF)
                .WithValueField(0, 16, name: "PullUpEnable")  // Pull-Up Enable
                .WithReservedBits(16, 16);
        }

        private const int PinsPerPort = 16;

        private enum Registers : ulong
        {
            InputOutputDataRegister = 0x00,
            BitSetResetRegister = 0x04,
            OutputEnableRegister = 0x08,
            PullUpEnableRegister = 0x0C,
        }
    }
}
