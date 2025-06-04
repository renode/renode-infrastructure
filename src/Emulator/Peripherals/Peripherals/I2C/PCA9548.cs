//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class PCA9548 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public PCA9548()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public void Write(byte[] data)
        {
            this.NoisyLog("Written {0} bytes", data.Length);
            foreach(var b in data)
            {
                RegistersCollection.Write((long)Registers.Control, b);
                this.NoisyLog("Written byte 0x{0:X} to control register", b);
            }
        }

        public byte[] Read(int count)
        {
            var result = RegistersCollection.Read((long)Registers.Control);
            this.NoisyLog("Reading control register from device: 0x{0:X}", result);

            return new byte [] { result };
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going to the idle state");
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithFlags(0, 8, out channelStatus);
        }

        private IFlagRegisterField[] channelStatus;

        private enum Registers
        {
            Control = 0x0,
        }
    }
}
