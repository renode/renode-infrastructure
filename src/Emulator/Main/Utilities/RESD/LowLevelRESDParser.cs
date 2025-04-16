//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Utilities.RESD
{
    public class LowLevelRESDParser
    {
        public LowLevelRESDParser(ReadFilePath path)
        {
            filePath = path;
            ReadHeader();
        }

        public IEnumerable<IDataBlock> GetDataBlockEnumerator()
        {
            return new DataBlockEnumerator(this, null);
        }

        public IEnumerable<DataBlock<T>> GetDataBlockEnumerator<T>() where T : RESDSample, new()
        {
            return new DataBlockEnumerator(this, typeof(T)).OfType<DataBlock<T>>();
        }

        public long FirstBlockOffset { get; private set; }

        public string FilePath => serializedBuffer == null ? filePath : null;

        public event Action<LogLevel, string> LogCallback;

        private SafeBinaryReader GetNewReader()
        {
            if(serializedBuffer == null)
            {
                return new SafeBinaryReader(File.OpenRead(filePath));
            }
            else
            {
                return new SafeBinaryReader(new MemoryStream(serializedBuffer));
            }
        }

        private void ReadHeader()
        {
            using(var reader = GetNewReader())
            {
                var magic = reader.ReadBytes(MagicValue.Length);
                if(!Enumerable.SequenceEqual(magic, MagicValue))
                {
                    throw new RESDException($"Invalid magic number for RESD file, excepted: {Misc.PrettyPrintCollectionHex(MagicValue)}, got {Misc.PrettyPrintCollectionHex(magic)}");
                }

                var version = reader.ReadByte();
                if(version != SupportedVersion)
                {
                    throw new RESDException($"Version {version} is not supported");
                }

                var padding = reader.ReadBytes(HeaderPaddingLength);
                if(padding.Length != HeaderPaddingLength || padding.Any(b => b != 0x00))
                {
                    throw new RESDException($"Invalid padding in RESD header (expected {HeaderPaddingLength} zeros, got {Misc.PrettyPrintCollectionHex(padding)})");
                }

                LogCallback?.Invoke(LogLevel.Debug, "RESD: Read header succesfully");
                FirstBlockOffset = reader.BaseStream.Position;
            }
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            if(serializedBuffer != null)
            {
                return;
            }

            var reader = GetNewReader();
            serializedBuffer = reader.ReadBytes((int)reader.Length);
        }

        [PostSerialization]
        private void AfterSerialization()
        {
            serializedBuffer = null;
        }

        private class DataBlockEnumerator : IEnumerator<IDataBlock>, IEnumerable<IDataBlock>
        {
            public DataBlockEnumerator(LowLevelRESDParser parser, Type sampleFilter)
            {
                this.parser = parser;
                this.sampleFilter = sampleFilter;

                reader = parser.GetNewReader();
                reader.EndOfStreamEvent += (message) =>
                {
                    throw new RESDException($"RESD file ended while reading data: {message} ({reader.BaseStream.Position} > {reader.BaseStream.Length})");
                };

                Reset();
            }

            public void Dispose()
            {
                reader.Dispose();
            }

            public bool MoveNext()
            {
                while(!reader.EOF)
                {
                    limitedReader?.SeekToEnd();
                    if(reader.EOF)
                    {
                        return false;
                    }

                    var classType = sampleFilter;

                    var dataBlockHeader = DataBlockHeader.ReadFromStream(reader);
                    limitedReader = reader.WithLength(reader.BaseStream.Position + (long)dataBlockHeader.Size);
                    var sampleType = dataBlockHeader.SampleType;

                    if(classType != null)
                    {
                        var sampleTypeAttribute = classType.GetCustomAttributes(typeof(SampleTypeAttribute), true).FirstOrDefault() as SampleTypeAttribute;
                        if(sampleTypeAttribute.SampleType != sampleType)
                        {
                            reader.SkipBytes((int)dataBlockHeader.Size);
                            continue;
                        }
                    }
                    else if(!TryFindTypeBySampleType(sampleType, out classType))
                    {
                        parser.LogCallback?.Invoke(LogLevel.Error, $"Could not find RESDSample implementator for {sampleType}");
                        reader.SkipBytes((int)dataBlockHeader.Size);
                        continue;
                    }

                    Type dataBlockType;
                    switch(dataBlockHeader.BlockType)
                    {
                        case BlockType.ConstantFrequencySamples:
                            dataBlockType = typeof(ConstantFrequencySamplesDataBlock<>).MakeGenericType(new[] { classType });
                            break;

                        case BlockType.ArbitraryTimestampSamples:
                            dataBlockType = typeof(ArbitraryTimestampSamplesDataBlock<>).MakeGenericType(new[] { classType });
                            break;

                        default:
                            // skip the rest of the unsupported block
                            parser.LogCallback?.Invoke(LogLevel.Warning, $"RESD: Skipping unupported block of type {dataBlockHeader.BlockType} and size {dataBlockHeader.Size} bytes");
                            reader.SkipBytes((int)dataBlockHeader.Size);
                            continue;
                    }

                    Current = (IDataBlock)dataBlockType.GetMethod("ReadFromStream").Invoke(null, new object[] { dataBlockHeader, limitedReader });
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                reader.BaseStream.Seek(parser.FirstBlockOffset, SeekOrigin.Begin);
            }

            public IEnumerator<IDataBlock> GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }

            public IDataBlock Current { get; private set; }
            object IEnumerator.Current => Current;

            public long FirstBlockOffset { get; private set; }

            private bool TryFindTypeBySampleType(SampleType sampleType, out Type type)
            {
                foreach(var autoLoadedType in TypeManager.Instance.AutoLoadedTypes)
                {
                    var sampleTypeAttribute = autoLoadedType.GetCustomAttributes(typeof(SampleTypeAttribute), true).FirstOrDefault() as SampleTypeAttribute;
                    if(sampleTypeAttribute == null || sampleTypeAttribute.SampleType != sampleType)
                    {
                        continue;
                    }

                    type = autoLoadedType;
                    return true;
                }

                type = typeof(object);
                return false;
            }

            [PostDeserialization]
            private void AfterDeserialization()
            {
                reader = parser.GetNewReader();
                reader.BaseStream.Seek(parser.FirstBlockOffset, SeekOrigin.Begin);
            }

            [Transient]
            private SafeBinaryReader reader;
            [Transient]
            private SafeBinaryReader limitedReader;

            private readonly LowLevelRESDParser parser;
            private readonly Type sampleFilter;
        }

        private byte[] serializedBuffer;
        private readonly string filePath;

        private const byte SupportedVersion = 0x1;
        private const int HeaderPaddingLength = 3;
        private static readonly byte[] MagicValue = new byte[] { (byte)'R', (byte)'E', (byte)'S', (byte)'D' };
    }
}
