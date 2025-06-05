//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class TCA6416 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
    {
        public TCA6416()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();

            Reset();
        }

        public void Write(byte[] data)
        {
            this.NoisyLog("Written {0} bytes", data.Length);
            foreach(var b in data)
            {
                WriteByte(b);
            }
        }

        public byte[] Read(int count)
        {
            var data = new byte[count];
            for(var i = 0; i < count; i++)
            {
                var result = RegistersCollection.Read(address);
                this.NoisyLog("Reading register 0x{0:X} from device: 0x{1:X}", address, result);
                data[i] = result;
                SetNextAddress();
            }

            return data;
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going to the idle state");
            ResetState();
        }

        public virtual void Reset()
        {
            address = 0;
            ResetState();
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void ResetState()
        {
            // Address is kept until it is overwritten in WriteByte; this way Read method can access register at previously selected address.
            state = State.CollectingAddress;
        }

        private void WriteByte(byte b)
        {
            switch(state)
            {
                case State.CollectingAddress:
                    address = b;
                    this.NoisyLog("Setting register address to 0x{0:X}", address);
                    state = State.Processing;
                    break;
                case State.Processing:
                    this.NoisyLog("Writing value 0x{0:X} to register 0x{1:X}", b, address);
                    RegistersCollection.Write(address, b);
                    SetNextAddress();
                    break;
                default:
                    throw new ArgumentException($"Unexpected state: {state}");
            }
        }

        // The eight registers within peripheral are configured to operate as four register pairs.
        // After sending data to one register, the next data byte is sent to the other register in the pair.
        private void SetNextAddress()
        {
            if(address % 2 == 0)
            {
                address++;
            }
            else
            {
                address--;
            }
        }

        private void DefineRegisters()
        {
            Registers.InputPort0.Define(this)
                .WithFlags(0, 8, out pinLevel0, FieldMode.Read);
            Registers.InputPort1.Define(this)
                .WithFlags(0, 8, out pinLevel1, FieldMode.Read);
            Registers.OutputPort0.Define(this, 0xff);
            Registers.OutputPort1.Define(this, 0xff);
            Registers.PolarityInversionPort0.Define(this);
            Registers.PolarityInversionPort1.Define(this);
            Registers.ConfigurationPort0.Define(this, 0xff);
            Registers.ConfigurationPort1.Define(this, 0xff);
        }

        private IFlagRegisterField[] pinLevel0;
        private IFlagRegisterField[] pinLevel1;

        private uint address;
        private State state;

        private enum State
        {
            CollectingAddress,
            Processing
        }

        private enum Registers
        {
            InputPort0 = 0x0,
            InputPort1 = 0x1,
            OutputPort0 = 0x2,
            OutputPort1 = 0x3,
            PolarityInversionPort0 = 0x4,
            PolarityInversionPort1 = 0x5,
            ConfigurationPort0 = 0x6,
            ConfigurationPort1 = 0x7
        }
    }
}
