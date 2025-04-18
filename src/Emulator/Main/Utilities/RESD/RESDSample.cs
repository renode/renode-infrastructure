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

        public abstract int? Width { get; }
        public abstract bool TryReadFromStream(SafeBinaryReader reader);

        public IDictionary<string, MetadataValue> Metadata { get; private set; }

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
            Temperature = reader.ReadInt32();

            return true;
        }

        public override string ToString()
        {
            return $"{DecimalToString(Temperature / (decimal)1e3)} °C";
        }

        public override int? Width => 4;

        public int Temperature { get; private set; }
    }

    [SampleType(SampleType.Acceleration)]
    public class AccelerationSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            AccelerationX = reader.ReadInt32();
            AccelerationY = reader.ReadInt32();
            AccelerationZ = reader.ReadInt32();

            return true;
        }

        public override string ToString()
        {
            var xStr = DecimalToString(AccelerationX / (decimal)1e6);
            var yStr = DecimalToString(AccelerationY / (decimal)1e6);
            var zStr = DecimalToString(AccelerationZ / (decimal)1e6);

            return $"[{xStr}, {yStr}, {zStr}] g";
        }

        public override int? Width => 4 * 3;

        public int AccelerationX { get; private set; }
        public int AccelerationY { get; private set; }
        public int AccelerationZ { get; private set; }
    }

    [SampleType(SampleType.AngularRate)]
    public class AngularRateSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            AngularRateX = reader.ReadInt32();
            AngularRateY = reader.ReadInt32();
            AngularRateZ = reader.ReadInt32();

            return true;
        }

        public override string ToString()
        {
            var xStr = DecimalToString(AngularRateX / (decimal)1e5);
            var yStr = DecimalToString(AngularRateY / (decimal)1e5);
            var zStr = DecimalToString(AngularRateZ / (decimal)1e5);

            return $"[{xStr}, {yStr}, {zStr}] rad/s";
        }

        public override int? Width => 4 * 3;

        public int AngularRateX { get; private set; }
        public int AngularRateY { get; private set; }
        public int AngularRateZ { get; private set; }
    }

    [SampleType(SampleType.Voltage)]
    public class VoltageSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            Voltage = reader.ReadUInt32();

            return true;
        }

        public override string ToString()
        {
            return $"{DecimalToString(Voltage / (decimal)1e6)} V";
        }

        public override int? Width => 4;

        public uint Voltage { get; private set; }
    }

    [SampleType(SampleType.ECG)]
    public class ECGSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            ECG = reader.ReadInt32();

            return true;
        }

        public override string ToString()
        {
            return $"{ECG} nV";
        }

        public override int? Width => 4;

        public int ECG { get; private set; }
    }

    [SampleType(SampleType.Humidity)]
    public class HumiditySample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            Humidity = reader.ReadUInt32();

            return true;
        }

        public override string ToString()
        {
            return $"{DecimalToString(Humidity / 1e3m)} %RH";
        }

        public override int? Width => 4;

        public uint Humidity { get; private set; }
    }

    [SampleType(SampleType.Pressure)]
    public class PressureSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            Pressure = reader.ReadUInt64();

            return true;
        }

        public override string ToString()
        {
            return $"{DecimalToString(Pressure / (decimal)1e3)} Pa";
        }

        public override int? Width => 8;

        public ulong Pressure { get; private set; }
    }

    [SampleType(SampleType.MagneticFluxDensity)]
    public class MagneticSample : RESDSample
    {
        public override bool TryReadFromStream(SafeBinaryReader reader)
        {
            MagneticFluxDensityX = reader.ReadInt32();
            MagneticFluxDensityY = reader.ReadInt32();
            MagneticFluxDensityZ = reader.ReadInt32();

            return true;
        }

        public override string ToString()
        {
            return $"[{MagneticFluxDensityX}, {MagneticFluxDensityY}, {MagneticFluxDensityZ}] nT";
        }

        public override int? Width => 4 * 3;

        public int MagneticFluxDensityX { get; private set; }
        public int MagneticFluxDensityY { get; private set; }
        public int MagneticFluxDensityZ { get; private set; }
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
            var length = reader.ReadUInt32();
            Data = reader.ReadBytes((int)length);

            return true;
        }

        public override string ToString()
        {
            return Misc.PrettyPrintCollectionHex(Data);
        }

        public override int? Width => null;

        public byte[] Data { get; private set; } = new byte[0];

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
