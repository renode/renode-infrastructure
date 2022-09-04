//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class SensorSamplesPacket<T> where T : SensorSample, new()
    {
        public SensorSamplesPacket(TimeInterval startTime, uint frequency)
        {
            Frequency = frequency;
            StartTime = startTime;
            Samples = new Queue<T>();
        }

        public static IList<SensorSamplesPacket<T>> ParseFile(string path, Func<List<decimal>, T> sampleConstructor)
        {
            SensorSamplesPacket<T> currentPacket = null;
            var packets = new List<SensorSamplesPacket<T>>();
            decimal? sensitivity = null;
            int? valueDimensions = null;
            int? valueSize = null;

            using(Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                int type;
                while((type = stream.ReadByte()) != -1)
                {
                    switch((DataType)type)
                    {
                        case DataType.Header:
                            var header = stream.ReadStruct<SensorSamplesPacketHeader>();
                            sensitivity = (decimal)header.Sensitivity;
                            valueDimensions = header.ValueDimensions;
                            valueSize = header.ValueSize;

                            currentPacket = new SensorSamplesPacket<T>(
                                    TimeInterval.FromSeconds(header.Time),
                                    header.Frequency);
                            packets.Add(currentPacket);
                            break;
                        case DataType.Value:
                            if(currentPacket == null || !sensitivity.HasValue || !valueDimensions.HasValue || !valueSize.HasValue)
                            {
                                throw new Exceptions.RecoverableException($"Broken Sensor Samples file: {path}");
                            }

                            try
                            {
                                var rawSample = stream.ReadBytes(valueSize.Value * valueDimensions.Value, throwIfEndOfStream: true);
                                var sampleValues = ParseSampleValues(rawSample, valueSize.Value, sensitivity.Value);
                                currentPacket.Samples.Enqueue(sampleConstructor(sampleValues));
                            }
                            catch(EndOfStreamException)
                            {
                                throw new Exceptions.RecoverableException($"Sensor Samples file ends in the middle of a sample: {path}");
                            }
                            break;
                        default:
                            throw new Exceptions.RecoverableException($"Invalid type: {type}");
                    }
                }
            }
            return packets;
        }

        public uint Frequency { get; }
        public Queue<T> Samples { get; }
        public TimeInterval StartTime { get; }

        private static List<decimal> ParseSampleValues(byte[] rawSample, int valueSize, decimal sensitivity)
        {
            var values = new List<decimal>();
            for(int i = 0; i < rawSample.Length; i += valueSize)
            {
                decimal rawValue;
                switch(valueSize)
                {
                    case 2:
                        rawValue = BitConverter.ToInt16(rawSample, i);
                        break;
                    case 4:
                        rawValue = BitConverter.ToInt32(rawSample, i);
                        break;
                    case 8:
                        rawValue = BitConverter.ToInt64(rawSample, i);
                        break;
                    default:
                        throw new Exceptions.RecoverableException($"Invalid value size: {valueSize}B");
                }
                values.Add(rawValue / sensitivity);
            }
            return values;
        }

        private enum DataType : byte
        {
            Header,
            Value,
        }
    }

    // It was supposed to be a private 'Header' struct inside the 'SensorSamplesPacket' class.
    // However, a non-generic 'Marshal.PtrToStructure' throws an exception if the type passed is
    // "a generic type definition". It applies to the structures defined in generic classes too.
    //
    // Generic Marshal methods aren't available in .NET Framework 4.5. It can be moved to the class
    // after upgrading Renode's .NET version and 'ReadStruct' / 'ToStruct' extensions ('Misc.cs').
    struct SensorSamplesPacketHeader
    {
        public override string ToString()
        {
            return $"time: {Time}; frequency: {Frequency}; sensitivity: {Sensitivity}; value: dimensions: {ValueDimensions}, size: {ValueSize}";
        }

// Suppress the "CS0649: Field is never assigned to, and will always have its default value 0" warning.
#pragma warning disable 0649
        public double Time;
        public uint Frequency;
        public byte ValueDimensions;
        public byte ValueSize;
        // 'Marshal.PtrToStructure' expects two empty bytes here as padding.
        public double Sensitivity;
#pragma warning restore 0649
    }
}

