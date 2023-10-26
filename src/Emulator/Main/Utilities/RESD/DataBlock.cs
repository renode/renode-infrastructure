//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Utilities.RESD
{
    public static class DataBlockExtensions
    {
        public static ulong GetEndTime(this IDataBlock @this)
        {
            return @this.StartTime + @this.Duration;
        }
    }

    public interface IDataBlock
    {
        ulong StartTime { get; }
        SampleType SampleType { get; }
        ushort ChannelId { get; }
        ulong SamplesCount { get; }
        ulong Duration { get; }
        IDictionary<String, MetadataValue> Metadata { get; }
        IDictionary<String, String> ExtraInformation { get; }
        IEnumerable<KeyValuePair<TimeInterval, RESDSample>> Samples { get; }
    }

    public abstract class DataBlock<T> : IDataBlock where T: RESDSample, new()
    {
        public DataBlock(DataBlockHeader header)
        {
            Header = header;
        }

        public abstract RESDStreamStatus TryGetSample(ulong timestamp, out T sample);

        public abstract ulong StartTime { get; }
        public abstract T CurrentSample { get; }
        public abstract ulong CurrentTimestamp { get; }
        public abstract ulong SamplesCount { get; }
        public abstract ulong Duration { get; }
        public abstract IDictionary<String, String> ExtraInformation { get; }
        public abstract IEnumerable<KeyValuePair<TimeInterval, RESDSample>> Samples { get; }

        public virtual BlockType BlockType => Header.BlockType;
        public virtual SampleType SampleType => Header.SampleType;
        public virtual ushort ChannelId => Header.ChannelId;
        public virtual ulong DataSize => Header.Size;
        public virtual IDictionary<String, MetadataValue> Metadata => CurrentSample.Metadata;

        protected virtual DataBlockHeader Header { get; }
    }

    public class DataBlockHeader
    {
        public static DataBlockHeader ReadFromStream(SafeBinaryReader reader)
        {
            var startPosition = reader.BaseStream.Position;
            var blockType = (BlockType)reader.ReadByte();
            var sampleType = (SampleType)reader.ReadUInt16();
            var channelId = reader.ReadUInt16();
            var dataSize = reader.ReadUInt64();

            return new DataBlockHeader(blockType, sampleType, channelId, dataSize, startPosition);
        }

        public SampleType SampleType { get; }
        public BlockType BlockType { get; }
        public ushort ChannelId { get; }
        public ulong Size { get; }
        public long StartPosition { get; }

        private DataBlockHeader(BlockType blockType, SampleType sampleType, ushort channel, ulong dataSize, long startPosition)
        {
            SampleType = sampleType;
            BlockType = blockType;
            ChannelId = channel;
            Size = dataSize;
            StartPosition = startPosition;
        }
    }

    public class ConstantFrequencySamplesDataBlock<T> : DataBlock<T> where T : RESDSample, new()
    {
        public static ConstantFrequencySamplesDataBlock<T> ReadFromStream(DataBlockHeader header, SafeBinaryReader reader)
        {
            var startTime = reader.ReadUInt64();
            var period = reader.ReadUInt64();

            return new ConstantFrequencySamplesDataBlock<T>(header, startTime, period, reader);
        }

        public override RESDStreamStatus TryGetSample(ulong timestamp, out T sample)
        {
            if(Interlocked.Exchange(ref usingReader, 1) != 0)
            {
                throw new RESDException("trying to call TryGetSample when using Samples iterator");
            }

            if(timestamp < currentSampleTimestamp)
            {
                // we don't support moving back in time
                sample = null;
                return RESDStreamStatus.BeforeStream;
            }

            var samplesDiff = (timestamp - currentSampleTimestamp) / Period;
            if(!samplesData.Move((int)samplesDiff))
            {
                // past the current block
                sample = null;
                return RESDStreamStatus.AfterStream;
            }

            currentSampleTimestamp += samplesDiff * Period;
            sample = samplesData.GetCurrentSample();

            Interlocked.Exchange(ref usingReader, 0);

            return RESDStreamStatus.OK;
        }

        public ulong Period { get; }
        public long Frequency => (long)(NanosecondsInSecond / Period);

        public override ulong StartTime { get; }
        public override T CurrentSample => samplesData.GetCurrentSample();
        public override ulong CurrentTimestamp => currentSampleTimestamp;
        public override ulong SamplesCount => samplesCount.Value;
        public override ulong Duration => SamplesCount * Period;
        public override IDictionary<String, String> ExtraInformation => new Dictionary<String, String>() {
            {"Period", TimeInterval.FromMicroseconds(Period / 1000).ToString()},
            {"Frequency", $"{Frequency}Hz"}
        };

        public override IEnumerable<KeyValuePair<TimeInterval, RESDSample>> Samples
        {
            get
            {
                if(Interlocked.Exchange(ref usingReader, 1) != 0)
                {
                    throw new RESDException("Trying to use Samples iterator during TryGetSample");
                }
                using(reader.Checkpoint)
                {
                    reader.BaseStream.Seek(samplesData.SampleDataOffset, SeekOrigin.Begin);

                    var currentTime = StartTime;
                    var currentSample = new T();

                    while(!reader.EOF && currentSample.TryReadFromStream(reader))
                    {
                        yield return new KeyValuePair<TimeInterval, RESDSample>(TimeInterval.FromMicroseconds(currentTime / 1000), currentSample);
                        currentTime += Period;
                    }
                }
                Interlocked.Exchange(ref usingReader, 0);
            }
        }

        private ConstantFrequencySamplesDataBlock(DataBlockHeader header, ulong startTime, ulong period, SafeBinaryReader reader) : base(header)
        {
            this.reader = reader;
            this.samplesData = new SamplesData<T>(reader);

            currentSampleTimestamp = startTime;

            Period = period;
            StartTime = startTime;

            samplesCount = new Lazy<ulong>(() =>
            {
                using(reader.Checkpoint)
                {
                    reader.BaseStream.Seek(samplesData.SampleDataOffset, SeekOrigin.Begin);
                    if(reader.EOF)
                    {
                        return 0;
                    }

                    var sample = new T();
                    var packets = 0UL;
                    while(sample.Skip(reader, 1))
                    {
                        packets += 1;
                    }
                    return packets;
                }
            });
        }

        private ulong currentSampleTimestamp;
        private int usingReader;

        private readonly SafeBinaryReader reader;
        private readonly SamplesData<T> samplesData;
        private readonly Lazy<ulong> samplesCount;

        private const decimal NanosecondsInSecond = 1e9m;
    }

    public enum BlockType
    {
        ArbitraryTimestampSamples = 1,
        ConstantFrequencySamples = 2,
    }
}
