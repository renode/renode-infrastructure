//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Antmicro.Renode.Utilities.RESD
{
    public abstract class RESDSample : IAutoLoadType
    {
        public virtual void ReadMetadata(SafeBinaryReader reader)
        {
            Metadata = MetadataBlock.ReadFromStream(reader);
        }

        public virtual bool Skip(SafeBinaryReader reader, int count)
        {
            if(!Width.HasValue)
            {
                throw new RESDException($"This sample type ({this.GetType().Name}) doesn't allow for skipping data.");
            }

            if(reader.BaseStream.Position + count * Width.Value > reader.Length)
            {
                return false;
            }

            reader.SkipBytes(count * Width.Value);
            return true;
        }

        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }

        public abstract int? Width { get; }
        public abstract bool TryReadFromStream(SafeBinaryReader reader);

        public virtual IDictionary<string, MetadataValue> Metadata { get; private set; }

        // Ensure decimal dots are always used regardless of the system locale
        // for consistent output formatting.
        protected static string DecimalToString(decimal value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }

    [SampleType(SampleType.Temperature)]
    public class TemperatureSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadInt32(out temperature);
        }

        public override string ToString()
        {
            return $"{DecimalToString(Temperature / (decimal)1e3)} °C";
        }

        public override int? Width => 4;

        public int Temperature => temperature;

        private int temperature;
    }

    [SampleType(SampleType.Acceleration)]
    public class AccelerationSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadInt32(out accelerationX)
                && reader.TryReadInt32(out accelerationY)
                && reader.TryReadInt32(out accelerationZ);
        }

        public override string ToString()
        {
            var xStr = DecimalToString(AccelerationX / (decimal)1e6);
            var yStr = DecimalToString(AccelerationY / (decimal)1e6);
            var zStr = DecimalToString(AccelerationZ / (decimal)1e6);

            return $"[{xStr}, {yStr}, {zStr}] g";
        }

        public override int? Width => 4 * 3;

        public int AccelerationX => accelerationX;
        public int AccelerationY => accelerationY;
        public int AccelerationZ => accelerationZ;

        private int accelerationX;
        private int accelerationY;
        private int accelerationZ;
    }

    [SampleType(SampleType.AngularRate)]
    public class AngularRateSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadInt32(out angularRateX)
                && reader.TryReadInt32(out angularRateY)
                && reader.TryReadInt32(out angularRateZ);
        }

        public override string ToString()
        {
            var xStr = DecimalToString(AngularRateX / (decimal)1e5);
            var yStr = DecimalToString(AngularRateY / (decimal)1e5);
            var zStr = DecimalToString(AngularRateZ / (decimal)1e5);

            return $"[{xStr}, {yStr}, {zStr}] rad/s";
        }

        public override int? Width => 4 * 3;

        public int AngularRateX => angularRateX;
        public int AngularRateY => angularRateY;
        public int AngularRateZ => angularRateZ;

        private int angularRateX;
        private int angularRateY;
        private int angularRateZ;
    }

    [SampleType(SampleType.Voltage)]
    public class VoltageSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadUInt32(out voltage);
        }

        public override string ToString()
        {
            return $"{DecimalToString(Voltage / (decimal)1e6)} V";
        }

        public override int? Width => 4;

        public uint Voltage => voltage;

        private uint voltage;
    }

    [SampleType(SampleType.ECG)]
    public class ECGSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadInt32(out ecg);
        }

        public override string ToString()
        {
            return $"{ECG} nV";
        }

        public override int? Width => 4;

        public int ECG => ecg;

        private int ecg;
    }

    [SampleType(SampleType.Humidity)]
    public class HumiditySample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadUInt32(out humidity);
        }

        public override string ToString()
        {
            return $"{DecimalToString(Humidity / 1e3m)} %RH";
        }

        public override int? Width => 4;

        public uint Humidity => humidity;

        private uint humidity;
    }

    [SampleType(SampleType.Pressure)]
    public class PressureSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadUInt64(out pressure);
        }

        public override string ToString()
        {
            return $"{DecimalToString(Pressure / (decimal)1e3)} Pa";
        }

        public override int? Width => 8;

        public ulong Pressure => pressure;

        private ulong pressure;
    }

    [SampleType(SampleType.MagneticFluxDensity)]
    public class MagneticSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadInt32(out magneticFluxDensityX)
                && reader.TryReadInt32(out magneticFluxDensityY)
                && reader.TryReadInt32(out magneticFluxDensityZ);
        }

        public override string ToString()
        {
            return $"[{MagneticFluxDensityX}, {MagneticFluxDensityY}, {MagneticFluxDensityZ}] nT";
        }

        public override int? Width => 4 * 3;

        public int MagneticFluxDensityX => magneticFluxDensityX;
        public int MagneticFluxDensityY => magneticFluxDensityY;
        public int MagneticFluxDensityZ => magneticFluxDensityZ;

        private int magneticFluxDensityX;
        private int magneticFluxDensityY;
        private int magneticFluxDensityZ;
    }

    [SampleType(SampleType.BinaryData)]
    public class BinaryDataSample : RESDSample
    {
        public override bool Skip(SafeBinaryReader reader, int count)
        {
            for(var i = 0; i < count; ++i)
            {
                if(reader.BaseStream.Position + LengthSize > reader.Length)
                {
                    return false;
                }

                var length = reader.ReadUInt32();

                if(reader.BaseStream.Position + length > reader.Length)
                {
                    return false;
                }

                reader.SkipBytes(length);
            }

            return true;
        }

        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            return reader.TryReadUInt32(out var length)
                && reader.TryReadBytes((int)length, out data);
        }

        public override string ToString()
        {
            return Misc.PrettyPrintCollectionHex(Data);
        }

        public override int? Width => null;

        public byte[] Data => data ?? new byte[0];

        private byte[] data;

        private const int LengthSize = 4;
    }

    public class SampleTypeAttribute : Attribute
    {
        public SampleTypeAttribute(SampleType sampleType)
        {
            this.SampleType = sampleType;
        }

        public SampleType SampleType { get; }
    }

    public enum SampleType
    {
        // General sample types
        Temperature = 0x0001,
        Acceleration = 0x002,
        AngularRate = 0x0003,
        Voltage = 0x0004,
        ECG = 0x0005,
        Humidity = 0x0006,
        Pressure = 0x0007,
        MagneticFluxDensity = 0x0008,
        BinaryData = 0x0009,

        // Custom sample types
        Custom = 0xF000,
    }
}
