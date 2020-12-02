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
    public class LSM9DS1_IMU: II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor
    {
        public LSM9DS1_IMU()
        {
            accelerationFifo = new SensorSamplesFifo<Vector3DSample>();
            angularRateFifo = new SensorSamplesFifo<Vector3DSample>();
            temperatureFifo = new SensorSamplesFifo<ScalarSample>();

            RegistersCollection = new ByteRegisterCollection(this);

            DefineRegisters();
        }

        public void Reset()
        {
            RegistersCollection.Reset();

            angularRateSensitivity = AngularRateSensitivity.DPS245;
            accelerationSensitivity = AccelerationSensitivity.G2;

            address = 0;
            currentReportedFifoDepth = 0;
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
            var dequeued = false;
            switch(address)
            {
                case (byte)Registers.AccelerometerOutputXLow:
                    dequeued = accelerationFifo.TryDequeueNewSample();
                    break;

                case (byte)Registers.GyroscopeOutputXLow:
                    dequeued = angularRateFifo.TryDequeueNewSample();
                    break;

                case (byte)Registers.TemperatureOutputLow:
                    // temperature data is not queued
                    temperatureFifo.TryDequeueNewSample();
                    break;
            }

            if(dequeued)
            {
                if(currentReportedFifoDepth > 0)
                {
                    currentReportedFifoDepth--;
                }
            }

            var result = RegistersCollection.Read(address);
            this.NoisyLog("Reading register {1} (0x{1:X}) from device: 0x{0:X}", result, (Registers)address);
            TryIncrementAddress();

            return new byte [] { result };
        }

        public void FeedAccelerationSample(decimal x, decimal y, decimal z, uint repeat = 1)
        {
            var sample = new Vector3DSample(x, y, z);

            for(var i = 0; i < repeat; i++)
            {
                accelerationFifo.FeedSample(sample);
            }
        }
        
        public void FeedAccelerationSample(string path)
        {
            accelerationFifo.FeedSamplesFromFile(path);
        }

        public void FeedAngularRateSample(decimal x, decimal y, decimal z, uint repeat = 1)
        {
            var sample = new Vector3DSample(x, y, z);

            for(var i = 0; i < repeat; i++)
            {
                angularRateFifo.FeedSample(sample);
            }
        }
        
        public void FeedAgularRateSample(string path)
        {
            angularRateFifo.FeedSamplesFromFile(path);
        }

        public void FeedTemperatureSample(decimal value, uint repeat = 1)
        {
            var sample = new ScalarSample(value);

            for(var i = 0; i < repeat; i++)
            {
                temperatureFifo.FeedSample(sample);
            }
        }
        
        public void FeedTemperatureSample(string path)
        {
            temperatureFifo.FeedSamplesFromFile(path);
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal AccelerationX
        {
            get => accelerationFifo.DefaultSample.X;
            set
            {
                accelerationFifo.DefaultSample.X = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal AccelerationY
        {
            get => accelerationFifo.DefaultSample.Y;
            set
            {
                accelerationFifo.DefaultSample.Y = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal AccelerationZ
        {
            get => accelerationFifo.DefaultSample.Z;
            set
            {
                accelerationFifo.DefaultSample.Z = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal Temperature
        {
            get => temperatureFifo.DefaultSample.Value;
            set
            {
                temperatureFifo.DefaultSample.Value = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal AngularRateX
        {
            get => angularRateFifo.DefaultSample.X;
            set
            {
                angularRateFifo.DefaultSample.X = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal AngularRateY
        {
            get => angularRateFifo.DefaultSample.Y;
            set
            {
                angularRateFifo.DefaultSample.Y = value;
            }
        }

        // NOTE: The meaning of this field
        // is the default value of the channel
        // when there are no samples in the fifo
        public decimal AngularRateZ
        {
            get => angularRateFifo.DefaultSample.Z;
            set
            {
                angularRateFifo.DefaultSample.Z = value;
            }
        }

        public ByteRegisterCollection RegistersCollection { get; }

        // When we provide a collection of samples from the file, they are not timestamped in any way
        // - we simplify and assume that each time SW reads a sample, we will advance to the next record.
        // 
        // This works fine with single reads, but not with fifos.
        // Some software (e.g., TensorFlow) might periodically poll the device and ask how many samples there are waiting in the queue.
        // For us it's a tricky question - we could say that all the samples in the buffer, but... 
        // as a result SW could read all of them at once.
        // This in turn could result in dropping those not fitting in the local buffer and causing the rest of the algorithm not to work.
        // So what we do instead is we report maximally MaxFifoDepth, giving SW chance to read and process samples in chunks.
        // 
        // Since the value is SW-specific, we can't hardcode any value in the model.
        public uint MaxFifoDepth { get; set; }

        private void TryIncrementAddress()
        {
            if(!addressAutoIncrement.Value)
            {
                return;
            }
            address = (byte)((address + 1) % 0x80);
        }

        private void DefineRegisters()
        {
            Registers.AccelerometerOutputXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_L_XL", valueProviderCallback: _ => GetScaledValue(accelerationFifo.Sample.X, (short)accelerationSensitivity, upperByte: false))
            ;

            Registers.AccelerometerOutputXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_H_XL", valueProviderCallback: _ => GetScaledValue(accelerationFifo.Sample.X, (short)accelerationSensitivity, upperByte: true))
            ;

            Registers.AccelerometerOutputYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_L_XL", valueProviderCallback: _ => GetScaledValue(accelerationFifo.Sample.Y, (short)accelerationSensitivity, upperByte: false))
            ;

            Registers.AccelerometerOutputYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_H_XL", valueProviderCallback: _ => GetScaledValue(accelerationFifo.Sample.Y, (short)accelerationSensitivity, upperByte: true))
            ;

            Registers.AccelerometerOutputZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_L_XL", valueProviderCallback: _ => GetScaledValue(accelerationFifo.Sample.Z, (short)accelerationSensitivity, upperByte: false))
            ;

            Registers.AccelerometerOutputZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_H_XL", valueProviderCallback: _ => GetScaledValue(accelerationFifo.Sample.Z, (short)accelerationSensitivity, upperByte: true))
            ;

            Registers.TemperatureOutputLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_TEMP_L", valueProviderCallback: _ => GetScaledTemperatureValue(upperByte: false))
            ;
            
            Registers.TemperatureOutputHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_TEMP_H", valueProviderCallback: _ => GetScaledTemperatureValue(upperByte: true))
            ;

            Registers.GyroscopeOutputXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_L_G", valueProviderCallback: _ => GetScaledValue(angularRateFifo.Sample.X, (short)angularRateSensitivity, upperByte: false))
            ;

            Registers.GyroscopeOutputXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_X_H_G", valueProviderCallback: _ => GetScaledValue(angularRateFifo.Sample.X, (short)angularRateSensitivity, upperByte: true))
            ;

            Registers.GyroscopeOutputYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_L_G", valueProviderCallback: _ => GetScaledValue(angularRateFifo.Sample.Y, (short)angularRateSensitivity, upperByte: false))
            ;

            Registers.GyroscopeOutputYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Y_H_G", valueProviderCallback: _ => GetScaledValue(angularRateFifo.Sample.Y, (short)angularRateSensitivity, upperByte: true))
            ;

            Registers.GyroscopeOutputZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_L_G", valueProviderCallback: _ => GetScaledValue(angularRateFifo.Sample.Z, (short)angularRateSensitivity, upperByte: false))
            ;

            Registers.GyroscopeOutputZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "OUT_Z_H_G", valueProviderCallback: _ => GetScaledValue(angularRateFifo.Sample.Z, (short)angularRateSensitivity, upperByte: true))
            ;

            Registers.WhoAmI.Define(this, 0x68);

            Registers.FifoSource.Define(this)
                .WithValueField(0, 6, FieldMode.Read, name: "FSS: Number of unread samples in FIFO", valueProviderCallback: _ =>
                {
                    if(currentReportedFifoDepth == 0)
                    {
                        // FIFO is supported in hardware only for accelerometer and gyroscope
                        currentReportedFifoDepth = Math.Min(MaxFifoDepth, Math.Max(accelerationFifo.SamplesCount, angularRateFifo.SamplesCount));
                        if(currentReportedFifoDepth == 0)
                        {
                            currentReportedFifoDepth = 1;
                        }
                        return 0;
                    }
                    return currentReportedFifoDepth;
                })
                .WithTaggedFlag("OVRN: FIFO overrun status", 6)
                .WithTaggedFlag("FTH: FIFO threshold status", 7)
            ;

            Registers.Control1.Define(this)
                .WithTag("BW: Gyroscope bandwith selection", 0, 2)
                .WithReservedBits(2, 1)
                .WithValueField(3, 2, name: "FS_G: Gyroscope full-scale selection",
                    valueProviderCallback: _ =>
                    {
                        switch(angularRateSensitivity)
                        {
                            case AngularRateSensitivity.DPS245:
                                return 0;
                            case AngularRateSensitivity.DPS500:
                                return 1;
                            case AngularRateSensitivity.DPS2000:
                                return 3;
                            default:
                                this.Log(LogLevel.Error, "Selected a not supported angular rate sensitivity");
                                return 2;
                        }
                    },
                    writeCallback: (_, val) =>
                    {
                        switch(val)
                        {
                            case 0:
                                angularRateSensitivity = AngularRateSensitivity.DPS245;
                                break;
                            case 1:
                                angularRateSensitivity = AngularRateSensitivity.DPS500;
                                break;
                            case 3:
                                angularRateSensitivity = AngularRateSensitivity.DPS2000;
                                break;
                            default:
                                this.Log(LogLevel.Warning, "Tried to set a not supported angular rate sensitivity");
                                break;
                        }
                    })
                .WithTag("ODR: Gyroscope output data rate", 5, 3)
            ;

            Registers.Control6.Define(this)
                .WithTag("BW_XL: Accelerometer anti-aliasing filter bandwith selection", 0, 2)
                .WithTag("BW_SCAL_ODR: Accelerometer bandwith selection", 2, 1)
                .WithValueField(3, 2, name: "FS_XL: Accelerometer full-scale selection",
                    valueProviderCallback: _ =>
                    {
                        switch(accelerationSensitivity)
                        {
                            case AccelerationSensitivity.G2:
                                return 0;
                            case AccelerationSensitivity.G16:
                                return 1;
                            case AccelerationSensitivity.G4:
                                return 2;
                            case AccelerationSensitivity.G8:
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
                                accelerationSensitivity = AccelerationSensitivity.G2;
                                break;
                            case 1:
                                accelerationSensitivity = AccelerationSensitivity.G16;
                                break;
                            case 2:
                                accelerationSensitivity = AccelerationSensitivity.G4;
                                break;
                            case 3:
                                accelerationSensitivity = AccelerationSensitivity.G8;
                                break;
                        }
                    })
                .WithTag("ODR_XL: Accelerometer output data rate", 5, 3)
            ;

            Registers.Control8.Define(this, 0x4)
                .WithTaggedFlag("SW_RESET: Software reset", 0)
                .WithTaggedFlag("BLE: Big/Little Endian data selection", 1)
                .WithFlag(2, out addressAutoIncrement, name: "IF_ADD_INC: Register address automatically incremented")
                .WithTaggedFlag("SIM: SPI serial interface mode", 3)
                .WithTaggedFlag("PP_OD: Push-pull/open-drain", 4)
                .WithTaggedFlag("H_LACTIVE: Interrupt activation level", 5)
                .WithTaggedFlag("BDU: Block data update", 6)
                .WithTaggedFlag("BOOT: Reboot memory", 7)
            ;
        }
        
        private byte GetScaledTemperatureValue(bool upperByte)
        {
            // temperature read is 0 for 25C
            // the sensivity is 16 per 1C
            var scaled = (short)((temperatureFifo.Sample.Value - 25) * 16);
            return upperByte
                ? (byte)(scaled >> 8)
                : (byte)scaled;
        }

        private byte GetScaledValue(decimal value, short sensitivity, bool upperByte)
        {
            var scaled = (short)(value * sensitivity);
            return upperByte
                ? (byte)(scaled >> 8)
                : (byte)scaled;
        }

        private uint currentReportedFifoDepth;
        private byte address;
        private State state;
        private AccelerationSensitivity accelerationSensitivity = AccelerationSensitivity.G2;
        private AngularRateSensitivity angularRateSensitivity = AngularRateSensitivity.DPS245;
        private IFlagRegisterField addressAutoIncrement;

        private readonly SensorSamplesFifo<Vector3DSample> accelerationFifo;
        private readonly SensorSamplesFifo<Vector3DSample> angularRateFifo;
        private readonly SensorSamplesFifo<ScalarSample> temperatureFifo;
        
        private enum AccelerationSensitivity : ushort
        {
            G2 = 16384,
            G4 = 8192,
            G8 = 4096,
            G16 = 2048
        }

        private enum AngularRateSensitivity : ushort
        {
            DPS245 = 120,
            DPS500 = 60,
            DPS2000 = 15
        }

        private enum State
        {
            Idle,
            Processing
        }

        private enum Registers: byte
        {
            // 0x0 - 0x03 are reserved
            ActivityThreshold = 0x04,            
            ActivityDuration = 0x05, 
            AccelerometerInterruptGeneratorConfig = 0x06,
            AccelerometerInterruptGeneratorThresholdX = 0x07,
            AccelerometerInterruptGeneratorThresholdY = 0x08,
            AccelerometerInterruptGeneratorThresholdZ = 0x09,
            AccelerometerInterruptGeneratorDuration = 0x0A,
            GyroscopeReference = 0x0B,
            InterruptControl1 = 0x0C,
            InterruptControl2 = 0x0D,
            // 0xE is reserved
            WhoAmI = 0x0F,
            Control1 = 0x10,
            Control2 = 0x11,
            Control3 = 0x12,
            GyroscopeOrientationConfiguration = 0x13,
            GyroscopeInterruptGeneratorSource = 0x14,
            TemperatureOutputLow = 0x15,
            TemperatureOutputHigh = 0x16,
            Status1 = 0x17,
            GyroscopeOutputXLow = 0x18,
            GyroscopeOutputXHigh = 0x19,
            GyroscopeOutputYLow = 0x1A,
            GyroscopeOutputYHigh = 0x1B,
            GyroscopeOutputZLow = 0x1C,
            GyroscopeOutputZHigh = 0x1D,
            Control4 = 0x1E,
            Control5 = 0x1F,
            Control6 = 0x20,
            Control7 = 0x21,
            Control8 = 0x22,
            Control9 = 0x23,
            Control10 = 0x24,
            // 0x25 is reserved
            AccelerometerInterruptGeneratorSource = 0x26,
            Status2 = 0x27,
            AccelerometerOutputXLow = 0x28,
            AccelerometerOutputXHigh = 0x29,
            AccelerometerOutputYLow = 0x2A,
            AccelerometerOutputYHigh = 0x2B,
            AccelerometerOutputZLow = 0x2C,
            AccelerometerOutputZHigh = 0x2D,
            FifoControl = 0x2E,
            FifoSource = 0x2F,
            GyroscopeInterruptGeneratorConfiguration = 0x30,
            GyroscopeInterruptGeneratorThresholdXHigh = 0x31,
            GyroscopeInterruptGeneratorThresholdXLow = 0x32,
            GyroscopeInterruptGeneratorThresholdYHigh = 0x33,
            GyroscopeInterruptGeneratorThresholdYLow = 0x34,
            GyroscopeInterruptGeneratorThresholdZHigh = 0x35,
            GyroscopeInterruptGeneratorThresholdZLow = 0x36,
            GyroscopeInterruptGeneratorDuration = 0x37,
            // 0x38 to 0x7F are reserved
        }
    }
}
