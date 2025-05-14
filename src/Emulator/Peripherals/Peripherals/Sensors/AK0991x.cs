//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public abstract class AK0991x : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IMagneticSensor, IUnderstandRESD
    {
        public AK0991x(IMachine machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public void FeedMagneticSamplesFromRESD(ReadFilePath filePath, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            magResdStream?.Dispose();
            magResdStream = this.CreateRESDStream<MagneticSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime);
            this.Log(LogLevel.Noisy, "RESD stream set to {0}", filePath);
        }

        public void Write(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Writing bytes: {0}", Misc.PrettyPrintCollectionHex(data));
            foreach(var b in data)
            {
                switch(state)
                {
                    case State.Idle:
                        selectedRegister = (Registers)b;
                        this.Log(LogLevel.Noisy, "Selected register: 0x{0:X}", selectedRegister);
                        state = State.ReceivedFirstByte;
                        break;
                    case State.ReceivedFirstByte:
                    case State.WritingWaitingForValue:
                        this.Log(LogLevel.Noisy, "Writing to register 0x{0:X} value 0x{1:X}", selectedRegister, b);
                        RegistersCollection.Write((byte)selectedRegister, b);
                        state = State.WritingWaitingForValue;
                        selectedRegister = IICGetNextRegister();

                        break;
                    case State.Reading:
                        //this isn't documented, but reads are able to use address set during write transfer, opposite isn't true
                        this.Log(LogLevel.Warning, "Trying to write without specifying address, byte is omitted");
                        break;
                }
            }
        }

        public byte[] Read(int count)
        {
            state = State.Reading; //reading can be started regardless of state, last selectedRegister is used
            var buf = new byte[count];
            for(var i = 0; i < buf.Length; i++)
            {
                buf[i] = RegistersCollection.Read((byte)selectedRegister);
                selectedRegister = IICGetNextRegister();
            }
            this.Log(LogLevel.Noisy, "Reading bytes: {0}", Misc.PrettyPrintCollectionHex(buf));

            return buf;
        }

        public void FinishTransmission()
        {
            if(state != State.ReceivedFirstByte) //in case of reading we may (documentation permits this or repeated START) receive STOP before the read transfer
            {
                state = State.Idle;
            }
        }

        public void Reset()
        {
            SoftwareReset();
            magResdStream?.Dispose();
            magResdStream = null;
        }

        public ByteRegisterCollection RegistersCollection { get; }

        public int MagneticFluxDensityX
        {
            get => GetSampleFromRESDStream(ref magResdStream, Direction.X);
            set => throw new RecoverableException($"Explicitly setting magnetic flux density is not supported by this model. " +
                $"Magnetic flux density should be provided from a RESD file or set via the '{nameof(DefaultMagneticFluxDensityX)}' property");
        }
        public int MagneticFluxDensityY
        {
            get => GetSampleFromRESDStream(ref magResdStream, Direction.Y);
            set => throw new RecoverableException($"Explicitly setting magnetic flux density is not supported by this model. " +
                $"Magnetic flux density should be provided from a RESD file or set via the '{nameof(DefaultMagneticFluxDensityY)}' property");
        }
        public int MagneticFluxDensityZ
        {
            get => GetSampleFromRESDStream(ref magResdStream, Direction.Z);
            set => throw new RecoverableException($"Explicitly setting magnetic flux density is not supported by this model. " +
                $"Magnetic flux density should be provided from a RESD file or set via the '{nameof(DefaultMagneticFluxDensityZ)}' property");
        }

        public int DefaultMagneticFluxDensityX { get; set; }
        public int DefaultMagneticFluxDensityY { get; set; }
        public int DefaultMagneticFluxDensityZ { get; set; }

        public abstract byte CompanyID { get; }
        public abstract byte DeviceID { get; }

        private int GetMagneticSampleValueDefault(Direction d)
        {
            switch(d)
            {
            case Direction.X:
                return DefaultMagneticFluxDensityX;
            case Direction.Y:
                return DefaultMagneticFluxDensityY;
            case Direction.Z:
                return DefaultMagneticFluxDensityZ;
            default:
                throw new Exception("Unreachable");
            }
        }

        private int GetMagneticSampleValue(MagneticSample sample, Direction d)
        {
            switch(d)
            {
                case Direction.X:
                    return sample.MagneticFluxDensityX;
                case Direction.Y:
                    return sample.MagneticFluxDensityY;
                case Direction.Z:
                    return sample.MagneticFluxDensityZ;
                default:
                    throw new Exception("Unreachable");
            }
        }

        private int GetSampleFromRESDStream(ref RESDStream<MagneticSample> stream, Direction d)
        {
            if(mode.Value == Mode.PowerDown)
            {
                this.Log(LogLevel.Error, "Tried to read sample while in Power down mode, getting default value.");
                return GetMagneticSampleValueDefault(d);
            }
            if(stream == null)
            {
                this.Log(LogLevel.Noisy, "RESD stream not found, getting default value");
                return GetMagneticSampleValueDefault(d);
            }

            switch(stream.TryGetCurrentSample(this, out var sample, out var _))
            {
                case RESDStreamStatus.OK:
                    this.Log(LogLevel.Noisy, "RESD stream status OK, setting sample: {0}", sample);
                    return GetMagneticSampleValue(sample, d);
                case RESDStreamStatus.BeforeStream:
                    this.Log(LogLevel.Noisy, "RESD before stream status, setting default value");
                    return GetMagneticSampleValueDefault(d);
                case RESDStreamStatus.AfterStream:
                    this.Log(LogLevel.Noisy, "RESD after stream status, setting last sample: {0}", sample);
                    return GetMagneticSampleValue(sample, d);
                default:
                    throw new Exception("Unreachable");
            }
        }

        private Registers IICGetNextRegister()
        {
            if(selectedRegister == Registers.Reserved2)
            {
                return Registers.Status1;
            }
            else if(selectedRegister == Registers.Status2)
            {
                return Registers.CompanyID;
            }
            else if(selectedRegister == Registers.Control3)
            {
                return Registers.Control1;
            }
            return selectedRegister + 1;
        }

        private void SoftwareReset()
        {
            RegistersCollection.Reset();
            selectedRegister = 0;
        }

        private void DefineRegisters()
        {
            Registers.CompanyID.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "WIA1",
                        valueProviderCallback: _ => CompanyID);

            Registers.DeviceID.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "WIA2",
                        valueProviderCallback: _ => DeviceID);

            Registers.Status1.Define(this)
                .WithReservedBits(2, 6)
                .WithFlag(1, FieldMode.Read, name: "DOR", valueProviderCallback: _ => false)
                .WithFlag(0, FieldMode.Read, name: "DRDY", valueProviderCallback: _ => true);

            Registers.XAxisMeasurementDataLower.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "HX_Low",
                        valueProviderCallback: _ =>
                            (byte)BitHelper.GetValue((uint)(MagneticFluxDensityX / SensorSensitivity), 0, 8));

            Registers.XAxisMeasurementDataUpper.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "HX_High",
                        valueProviderCallback: _ =>
                            (byte)BitHelper.GetValue((uint)(MagneticFluxDensityX / SensorSensitivity), 8, 8));

            Registers.YAxisMeasurementDataLower.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "HY_Low",
                        valueProviderCallback: _ =>
                            (byte)BitHelper.GetValue((uint)(MagneticFluxDensityY / SensorSensitivity), 0, 8));

            Registers.YAxisMeasurementDataUpper.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "HY_High",
                        valueProviderCallback: _ =>
                            (byte)BitHelper.GetValue((uint)(MagneticFluxDensityY / SensorSensitivity), 8, 8));

            Registers.ZAxisMeasurementDataLower.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "ZY_Low",
                        valueProviderCallback: _ =>
                            (byte)BitHelper.GetValue((uint)(MagneticFluxDensityZ / SensorSensitivity), 0, 8));

            Registers.ZAxisMeasurementDataUpper.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "ZY_High",
                        valueProviderCallback: _ =>
                            (byte)BitHelper.GetValue((uint)(MagneticFluxDensityZ / SensorSensitivity), 8, 8));

            Registers.Dummy.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "DUMMY");

            Registers.Status2.Define(this)
                .WithReservedBits(4, 4)
                .WithFlag(3, name: "HOFL", valueProviderCallback: _ => false)
                .WithReservedBits(0, 3);

            Registers.Control1.Define(this)
                .WithTag("CTRL1", 0, 8);

            Registers.Control2.Define(this)
                .WithReservedBits(5, 3)
                .WithEnumField<ByteRegister, Mode>(0, 5, out mode, name: "MODE");

            Registers.Control3.Define(this)
                .WithReservedBits(1, 7)
                .WithFlag(0, name: "SRST",
                        valueProviderCallback: _ => false,
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                SoftwareReset();
                            }
                        });

            Registers.Test1.Define(this)
                .WithReservedBits(0, 8);

            Registers.Test2.Define(this)
                .WithReservedBits(0, 8);
        }

        private RESDStream<MagneticSample> magResdStream;

        private IEnumRegisterField<Mode> mode;
        private Registers selectedRegister;
        private State state;

        private const int SensorSensitivity = 150; // nT/LSB

        private enum State
        {
            Idle,
            Reading,
            Writing,
            ReceivedFirstByte,
            WaitingForAddress,
            WritingWaitingForValue,
        }

        private enum Registers : byte
        {
            CompanyID = 0x0,
            DeviceID = 0x1,
            Reserved1 = 0x2,
            Reserved2 = 0x3,
            Status1 = 0x10,
            XAxisMeasurementDataLower = 0x11,
            XAxisMeasurementDataUpper = 0x12,
            YAxisMeasurementDataLower = 0x13,
            YAxisMeasurementDataUpper = 0x14,
            ZAxisMeasurementDataLower = 0x15,
            ZAxisMeasurementDataUpper = 0x16,
            Dummy = 0x17,
            Status2 = 0x18,
            Control1 = 0x30,
            Control2 = 0x31,
            Control3 = 0x32,
            Test1 = 0x33,
            Test2 = 0x34
        }

        private enum Mode : byte
        {
            PowerDown = 0x0,
            SingleMeasurement = 0x1,
            ContinuousMeasurement1 = 0x2,
            ContinuousMeasurement2 = 0x4,
            ContinuousMeasurement3 = 0x6,
            ContinuousMeasurement4 = 0x8,
            SelfTest = 0x10,
        }

        private enum Direction : byte
        {
            X = 0x0,
            Y = 0x1,
            Z = 0x2
        }
    }
}
