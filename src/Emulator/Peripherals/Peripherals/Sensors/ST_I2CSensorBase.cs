//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Sensors
{
    // This class implements a common ST sensor
    // register handling logic.
    // It can be used as a base for all I2C sensors that
    // support 7-bit-length registers addresses.

    // it should be enum instead of IConvertible, but earlier versions of C# do not support this
    public abstract class ST_I2CSensorBase<T> : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection> where T : IConvertible
    {
        public ST_I2CSensorBase()
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Written {0} bytes: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            foreach(var b in data)
            {
                WriteByte(b);
            }
        }

        public byte[] Read(int count)
        {
            var result = RegistersCollection.Read(address);
            this.NoisyLog("Reading register {1} (0x{1:X}) from device: 0x{0:X}", result, Enum.GetName(typeof(T), address));

            return new byte [] { result };
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going to the Idle state");
            state = State.Idle;
        }

        public virtual void Reset()
        {
            address = 0;
            state = State.Idle;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        protected byte GetScaledValue(decimal value, short sensitivity, bool upperByte)
        {
            var scaled = (short)(value * sensitivity);
            return upperByte
                ? (byte)(scaled >> 8)
                : (byte)scaled;
        }

        protected short CalculateScale(int minVal, int maxVal, int width)
        {
            var range = maxVal - minVal;
            return (short)(((1 << width) / range) - 1);
        }

        protected abstract void DefineRegisters();

        private void WriteByte(byte b)
        {
            switch(state)
            {
                case State.Idle:
                    address = BitHelper.GetValue(b, offset: 0, size: 7);
                    this.Log(LogLevel.Noisy, "Setting register address to {0} (0x{0:X})", Enum.GetName(typeof(T), address));
                    state = State.Processing;
                    break;

                case State.Processing:
                    this.Log(LogLevel.Noisy, "Writing value 0x{0:X} to register {1} (0x{1:X})", b, Enum.GetName(typeof(T), address));
                    RegistersCollection.Write(address, b);
                    break;

                default:
                    throw new ArgumentException($"Unexpected state: {state}");
            }
        }

        private byte address;
        private State state;

        private enum State
        {
            Idle,
            Processing
        }
    }
}
