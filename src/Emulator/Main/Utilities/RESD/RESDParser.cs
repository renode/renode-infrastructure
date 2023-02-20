//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities.RESD
{
    public enum RESDStreamStatus
    {
        // Provided sample is from requested timestamp
        OK = 0,
        // There are no samples for given samples in current block yet
        BeforeStream = -1,
        // There are no more samples of given type in the block/file
        AfterStream = -2,
    }

    public static class RESDStreamExtension
    {
        public static RESDStream<T> CreateRESDStream<T>(this IEmulationElement @this, ReadFilePath path, uint channel) where T: RESDSample, new()
        {
            var stream = new RESDStream<T>(path, channel);
            stream.Owner = @this;
            return stream;
        }
    }

    public class RESDStream<T> : IDisposable where T : RESDSample, new()
    {
        public RESDStream(ReadFilePath path, uint channel)
        {
            var sampleTypeAttribute = typeof(T).GetCustomAttributes(typeof(SampleTypeAttribute), true).FirstOrDefault() as SampleTypeAttribute;
            if(sampleTypeAttribute == null)
            {
                throw new RESDException($"Unsupported RESD sample type: {typeof(T).Name}");
            }

            this.sampleType = sampleTypeAttribute.SampleType;
            this.channel = channel;
            this.managedThreads = new List<IManagedThread>();

            reader = new SafeBinaryReader(File.OpenRead(path));
            reader.EndOfStreamEvent += (message) =>
            {
                throw new RESDException($"RESD file ended while reading data: {message} ({reader.BaseStream.Position} > {reader.BaseStream.Length})");
            };
            ReadHeader();
            PrereadFirstBlock();
        }

        public RESDStreamStatus TryGetSample(ulong timestamp, out T sample)
        {
            if(timestamp < currentBlock.StartTime)
            {
                sample = null;
                return RESDStreamStatus.BeforeStream;
            }

            while(!reader.EOF)
            {
                if(currentBlock == null)
                {
                    if(!TryGetNextBlock(out currentBlock))
                    {
                        break;
                    }
                    MetadataChanged?.Invoke();
                }

                switch(currentBlock.TryGetSample(timestamp, out sample))
                {
                    case RESDStreamStatus.BeforeStream:
                        sample = null;
                        return RESDStreamStatus.BeforeStream;
                    case RESDStreamStatus.OK:
                        // Just return sample
                        return RESDStreamStatus.OK;
                    case RESDStreamStatus.AfterStream:
                        // Find next block
                        currentBlock = null;
                        continue;
                }

                return RESDStreamStatus.OK;
            }

            sample = null;
            return RESDStreamStatus.AfterStream;
        }

        public IManagedThread StartSampleFeedThread(IPeripheral owner, uint frequency, Action<T, TimeInterval, RESDStreamStatus> newSampleCallback, ulong startTime = 0, ulong sampleOffsetTime = 0)
        {
            var machine = owner.GetMachine();
            Action feedSample = () =>
            {
                var timestamp = machine.ClockSource.CurrentValue;
                var timestampInNanoseconds = sampleOffsetTime + timestamp.TotalMicroseconds * 1000;
                var status = TryGetSample(timestampInNanoseconds, out var sample);
                newSampleCallback(sample, timestamp, status);
            };

            Func<bool> stopCondition = () =>
            {
                if(reader.EOF)
                {
                    newSampleCallback(null, TimeInterval.Empty, RESDStreamStatus.AfterStream);
                }
                return reader.EOF;
            };

            var thread = machine.ObtainManagedThread(feedSample, frequency, "RESD stream thread", owner, stopCondition);
            thread.StartDelayed(TimeInterval.FromMicroseconds(startTime / 1000));
            managedThreads.Add(thread);
            return thread;
        }

        public void Dispose()
        {
            foreach(var thread in managedThreads)
            {
                thread.Dispose();
            }
            managedThreads.Clear();
            reader.Dispose();
        }

        public IEmulationElement Owner { get; set; }

        public T CurrentSample => currentBlock?.CurrentSample;
        public Action MetadataChanged;

        private void ReadHeader()
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
        }

        private void PrereadFirstBlock()
        {
            if(!TryGetNextBlock(out currentBlock))
            {
                throw new RESDException($"Provided RESD file doesn't contain data for {typeof(T)}");
            }
            Logger.Log(Owner, LogLevel.Debug, "First sample has timestamp {0}", currentBlock.StartTime);
        }

        private bool TryGetNextBlock(out DataBlock<T> block)
        {
            while(!reader.EOF)
            {
                var dataBlockHeader = DataBlockHeader.ReadFromStream(reader);
                if(dataBlockHeader.SampleType != sampleType || dataBlockHeader.ChannelId != channel)
                {
                    Logger.Log(Owner, LogLevel.Debug, "Skipping block of type {0} and size {1} bytes", dataBlockHeader.BlockType, dataBlockHeader.Size);
                    reader.SkipBytes((int)dataBlockHeader.Size);
                    continue;
                }

                var limitedReader = reader.WithLength(reader.BaseStream.Position + (long)dataBlockHeader.Size);

                switch(dataBlockHeader.BlockType)
                {
                    case BlockType.ConstantFrequencySamples:
                        block = ConstantFrequencySamplesDataBlock<T>.ReadFromStream(dataBlockHeader, limitedReader);
                        return true;

                    default:
                        // skip the rest of the unsupported block
                        Logger.Log(Owner, LogLevel.Warning, "Skipping unupported block of type {0} and size {1} bytes", dataBlockHeader.BlockType, dataBlockHeader.Size);
                        reader.SkipBytes((int)dataBlockHeader.Size);
                        break;
                }
            }

            block = null;
            return false;
        }

        private DataBlock<T> currentBlock;

        private readonly SampleType sampleType;
        private readonly uint channel;
        private readonly SafeBinaryReader reader;
        private readonly IList<IManagedThread> managedThreads;

        private const byte SupportedVersion = 0x1;
        private const int HeaderPaddingLength = 3;
        private static readonly byte[] MagicValue = new byte[] { (byte)'R', (byte)'E', (byte)'S', (byte)'D' };
    }
}
