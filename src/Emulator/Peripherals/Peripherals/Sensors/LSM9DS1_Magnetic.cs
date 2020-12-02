//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LSM9DS1_Magnetic: II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ISensor
    {
        public LSM9DS1_Magnetic()
        {
            fifo = new SensorSamplesFifo<Vector3DSample>();
            RegistersCollection = new ByteRegisterCollection(this);

            DefineRegisters();
        }

        public void Reset()
        {
            RegistersCollection.Reset();

            magneticSensitivity = Sensitivity.Gauss4;

            address = 0;
            addressAutoIncrement = false;
            state = State.Idle;
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Finishing transmission, going to the Idle state");
            state = State.Idle;
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Written {0} bytes: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            foreach(var b in data)
            {
                WriteByte(b);
            }
        }

        public void WriteByte(byte b)
        {
            switch(state)
            {
                case State.Idle:
                    address = BitHelper.GetValue(b, offset: 0, size: 7);
                    addressAutoIncrement = BitHelper.IsBitSet(b, 7);
                    this.Log(LogLevel.Noisy, "Setting register address to {0} (0x{0:X})", (Registers)address);
                    state = State.Processing;
                    break;

                case State.Processing:
                    this.Log(LogLevel.Noisy, "Writing value 0x{0:X} to register {1} (0x{1:X})", b, (Registers)address);
                    RegistersCollection.Write(address, b);
                    TryIncrementAddress();
                    break;

                default:
                    throw new ArgumentException($"Unexpected state: {state}");
            }
        }

        public byte[] Read(int count = 1)
        {
            switch(address)
            {
                case (byte)Registers.OutputXLow:
                {
                    // magnetic data is not queued
                    fifo.TryDequeueNewSample();
                }
                break;
            }

            var result = RegistersCollection.Read(address);
            this.NoisyLog("Reading register {1} (0x{1:X}) from device: 0x{0:X}", result, (Registers)address);
            TryIncrementAddress();

            return new byte [] { result };
        }

        public void FeedMagneticSample(decimal x, decimal y, decimal z, uint repeat = 1)
        {
            var sample = new Vector3DSample(x, y, z);

            for(var i = 0; i < repeat; i++)
            {
                fifo.FeedSample(sample);
            }
        }
        
        public void FeedMagneticSample(string path)
        {
            fifo.FeedSamplesFromFile(path);
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal MagneticX
        {
            get => fifo.DefaultSample.X;
            set
            {
                fifo.DefaultSample.X = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal MagneticY
        {
            get => fifo.DefaultSample.Y;
            set
            {
                fifo.DefaultSample.Y = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal MagneticZ
        {
            get => fifo.DefaultSample.Z;
            set
            {
                fifo.DefaultSample.Z = value;
            }
        }

        public ByteRegisterCollection RegistersCollection { get; }

        private void TryIncrementAddress()
        {
            if(!addressAutoIncrement)
            {
                return;
            }
            address = (byte)((address + 1) % 0x80);
        }

        private void DefineRegisters()
        {
            Registers.OutputXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_L_M", valueProviderCallback: _ => GetScaledValue(MagneticX, (short)magneticSensitivity, upperByte: false))
            ;

            Registers.OutputXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_H_M",  valueProviderCallback: _ => GetScaledValue(MagneticX, (short)magneticSensitivity, upperByte: true))
            ;

            Registers.OutputYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_L_M", valueProviderCallback: _ => GetScaledValue(MagneticY, (short)magneticSensitivity, upperByte: false))
            ;

            Registers.OutputYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_H_M", valueProviderCallback: _ => GetScaledValue(MagneticY, (short)magneticSensitivity, upperByte: true))
            ;

            Registers.OutputZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_L_M", valueProviderCallback: _ => GetScaledValue(MagneticZ, (short)magneticSensitivity, upperByte: false))
            ;

            Registers.OutputZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_H_M", valueProviderCallback: _ => GetScaledValue(MagneticZ, (short)magneticSensitivity, upperByte: true))
            ;

            Registers.WhoAmI.Define(this, 0x3d);

            Registers.Control2.Define(this)
                .WithReservedBits(0, 2)
                .WithTag("SOFT_RST", 2, 1)
                .WithTag("REBOOT", 3, 1)
                .WithReservedBits(4, 1)
                .WithValueField(5, 2, name: "FS: Magnetometer full-scale selection",
                    valueProviderCallback: _ =>
                    {
                        switch(magneticSensitivity)
                        {
                            case Sensitivity.Gauss4:
                                return 0;
                            case Sensitivity.Gauss8:
                                return 1;
                            case Sensitivity.Gauss12:
                                return 2;
                            case Sensitivity.Gauss16:
                                return 3;
                            default:
                                throw new ArgumentException("This should never happen");
                        }
                    },
                    writeCallback: (_, val) =>
                    {
                        switch(val)
                        {
                            case 0:
                                magneticSensitivity = Sensitivity.Gauss4;
                                break;
                            case 1:
                                magneticSensitivity = Sensitivity.Gauss8;
                                break;
                            case 2:
                                magneticSensitivity = Sensitivity.Gauss12;
                                break;
                            case 3:
                                magneticSensitivity = Sensitivity.Gauss16;
                                break;
                        }
                    })
                .WithReservedBits(7, 1)
            ;
        }

        private byte GetScaledValue(decimal value, short sensitivity, bool upperByte)
        {
            var scaled = (short)(value * sensitivity);
            return upperByte
                ? (byte)(scaled >> 8)
                : (byte)scaled;
        }

        private byte address;
        private bool addressAutoIncrement;
        private State state;
        private Sensitivity magneticSensitivity = Sensitivity.Gauss4;

        private readonly SensorSamplesFifo<Vector3DSample> fifo;
        
        private enum Sensitivity : ushort
        {
            Gauss4 = 8192,
            Gauss8 = 4096,
            Gauss12 = 3072,
            Gauss16 = 2048
        }

        private enum State
        {
            Idle,
            Processing
        }

        private enum Registers
        {
            // 0x0 - 0x04 are reserved
            OffsetXLow = 0x05,
            OffsetXHigh = 0x06,
            OffsetYLow = 0x07,
            OffsetYHigh = 0x08,
            OffsetZLow = 0x09,
            OffsetZHigh = 0x0A,

            // 0x0B - 0x0E are reserved
            WhoAmI = 0x0F,

            // 0x10 - 0x1F are reserved
            Control1 = 0x20,
            Control2 = 0x21,
            Control3 = 0x22,
            Control4 = 0x23,
            Control5 = 0x24,

            // 0x25 - 0x26 are reserved
            Status = 0x27,
            OutputXLow = 0x28,
            OutputXHigh = 0x29,
            OutputYLow = 0x2A,
            OutputYHigh = 0x2B,
            OutputZLow = 0x2C,
            OutputZHigh = 0x2D,

            // 0x2E - 0x2F are reserved
            InterruptConfig = 0x30,
            InterruptSource = 0x31,
            InterruptThresholdLow = 0x32,
            InterruptThresholdHigh = 0x33
        }
    }
}
