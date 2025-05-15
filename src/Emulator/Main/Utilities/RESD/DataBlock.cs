//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

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
        public abstract RESDStreamStatus TryGetNextSample(out TimeInterval timestamp, out T sample);

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
                throw new RESDException("Trying to call TryGetSample when using Samples iterator");
            }

            using(DisposableWrapper.New(() => Interlocked.Exchange(ref usingReader, 0)))
            {
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
                    sample = samplesData.LastSample;
                    return RESDStreamStatus.AfterStream;
                }

                currentSampleTimestamp += samplesDiff * Period;
                sample = samplesData.GetCurrentSample();

                return RESDStreamStatus.OK;
            }
        }

        public override RESDStreamStatus TryGetNextSample(out TimeInterval timestamp, out T sample)
        {
            if(Interlocked.Exchange(ref usingReader, 1) != 0)
            {
                throw new RESDException("Trying to call TryGetNextSample when using Samples iterator");
            }

            using(DisposableWrapper.New(() => Interlocked.Exchange(ref usingReader, 0)))
            {
                if(!samplesData.Move(1))
                {
                    // past the current block
                    sample = null;
                    timestamp = default(TimeInterval);
                    return RESDStreamStatus.AfterStream;
                }

                currentSampleTimestamp += Period;
                sample = samplesData.GetCurrentSample();
                timestamp = TimeInterval.FromNanoseconds(currentSampleTimestamp);
            }

            return RESDStreamStatus.OK;
        }

        public ulong Period { get; }
        public decimal Frequency => NanosecondsInSecond / Period;

        public override ulong StartTime { get; }
        public override T CurrentSample => samplesData.GetCurrentSample();
        public override ulong CurrentTimestamp => currentSampleTimestamp;
        public override ulong SamplesCount => samplesCount.Value;
        public override ulong Duration => SamplesCount * Period;
        public override IDictionary<String, String> ExtraInformation => new Dictionary<String, String>() {
            {"Period", TimeInterval.FromNanoseconds(Period).ToString()},
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
                        yield return new KeyValuePair<TimeInterval, RESDSample>(TimeInterval.FromNanoseconds(currentTime), currentSample);
                        currentSample = new T();
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

    public class ArbitraryTimestampSamplesDataBlock<T> : DataBlock<T> where T : RESDSample, new()
    {
        public static ArbitraryTimestampSamplesDataBlock<T> ReadFromStream(DataBlockHeader header, SafeBinaryReader reader)
        {
            var startTime = reader.ReadUInt64();

            return new ArbitraryTimestampSamplesDataBlock<T>(header, startTime, reader);
        }

        public override RESDStreamStatus TryGetSample(ulong timestamp, out T sample)
        {
            if(Interlocked.Exchange(ref usingReader, 1) != 0)
            {
                throw new RESDException("Trying to call TryGetSample when using Samples iterator");
            }

            var result = RESDStreamStatus.OK;
            using(DisposableWrapper.New(() => Interlocked.Exchange(ref usingReader, 0)))
            {
                if(timestamp < CurrentTimestamp)
                {
                    // we don't support moving back in time
                    sample = null;
                    return RESDStreamStatus.BeforeStream;
                }
                var wrappedSample = samplesData.GetCurrentSample();

                while(StartTime + wrappedSample.Timestamp <= timestamp)
                {
                    if(!samplesData.Move(1))
                    {
                        // past the current block
                        currentWrappedSample = samplesData.LastSample;
                        result = RESDStreamStatus.AfterStream;
                        break;
                    }
                    // currentWrappedSample is not synnchronized with samplesData.GetCurrentSample
                    // as it is used to peek for the next timestamp
                    currentWrappedSample = (TimestampedRESDSample)wrappedSample.Clone();
                    wrappedSample = samplesData.GetCurrentSample();
                }

                sample = CurrentSample;
            }

            return result;
        }

        public override RESDStreamStatus TryGetNextSample(out TimeInterval timestamp, out T sample)
        {
            if(Interlocked.Exchange(ref usingReader, 1) != 0)
            {
                throw new RESDException("Trying to call TryGetNextSample when using Samples iterator");
            }

            var result = RESDStreamStatus.AfterStream;
            using(DisposableWrapper.New(() => Interlocked.Exchange(ref usingReader, 0)))
            {
                // currentWrappedSample and samplesData.GetCurrentSample can be desynnchronized in TryGetSample
                if(currentWrappedSample != samplesData.GetCurrentSample() || samplesData.Move(1))
                {
                    result = RESDStreamStatus.OK;
                    currentWrappedSample = samplesData.GetCurrentSample();
                }

                timestamp = TimeInterval.FromNanoseconds(CurrentTimestamp);
                sample = CurrentSample;
            }

            return result;
        }

        public override ulong StartTime { get; }
        public override T CurrentSample => currentWrappedSample.Sample;
        public override ulong CurrentTimestamp => StartTime + currentWrappedSample.Timestamp;
        public override ulong SamplesCount => samplesCount.Value;
        public override ulong Duration => duration.Value;
        public override IDictionary<String, String> ExtraInformation => new Dictionary<String, String>();

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
                    var currentSample = new TimestampedRESDSample();

                    while(!reader.EOF && currentSample.TryReadFromStream(reader))
                    {
                        currentTime = StartTime + currentSample.Timestamp;
                        yield return new KeyValuePair<TimeInterval, RESDSample>(TimeInterval.FromNanoseconds(currentTime), currentSample.Sample);
                        currentSample = new TimestampedRESDSample();
                    }
                }
                Interlocked.Exchange(ref usingReader, 0);
            }
        }

        private ArbitraryTimestampSamplesDataBlock(DataBlockHeader header, ulong startTime, SafeBinaryReader reader) : base(header)
        {
            this.reader = reader;
            this.samplesData = new SamplesData<TimestampedRESDSample>(reader);

            StartTime = startTime;
            currentWrappedSample = samplesData.GetCurrentSample();

            var packets = 0UL;
            var lastTimestamp = 0UL;

            samplesCount = new Lazy<ulong>(() =>
            {
                if(!duration.IsValueCreated)
                {
                    GetSamplesCountAndDuration(out packets, out lastTimestamp);
                }
                return packets;
            });

            duration = new Lazy<ulong>(() =>
            {
                if(!samplesCount.IsValueCreated)
                {
                    GetSamplesCountAndDuration(out packets, out lastTimestamp);
                }
                return lastTimestamp;
            });
        }

        private void GetSamplesCountAndDuration(out ulong packets, out ulong lastTimestamp)
        {
            packets = 0;
            lastTimestamp = 0;

            using(reader.Checkpoint)
            {
                reader.BaseStream.Seek(samplesData.SampleDataOffset, SeekOrigin.Begin);
                if(reader.EOF)
                {
                    return;
                }

                var sample = new TimestampedRESDSample();
                while(sample.Skip(reader, 1))
                {
                    lastTimestamp = sample.Timestamp;
                    packets += 1;
                }
            }
        }

        private TimestampedRESDSample currentWrappedSample;
        private int usingReader;

        private readonly SafeBinaryReader reader;
        private readonly SamplesData<TimestampedRESDSample> samplesData;
        private readonly Lazy<ulong> samplesCount;
        private readonly Lazy<ulong> duration;

        private class TimestampedRESDSample : RESDSample, IAutoLoadType
        {
            public override void ReadMetadata(SafeBinaryReader reader) => Sample.ReadMetadata(reader);

            public override bool Skip(SafeBinaryReader reader, int count)
            {
                if(count < 0)
                {
                    throw new RESDException($"This sample type ({Sample.GetType().Name}) doesn't allow for skipping data backwards.");
                }

                if(count == 0)
                {
                    return true;
                }

                if(!Width.HasValue)
                {
                    return SkipWithDynamicWidth(reader, count);
                }

                if(reader.BaseStream.Position + count * Width.Value > reader.Length)
                {
                    return false;
                }

                if(count > 1)
                {
                    reader.SkipBytes((count - 1) * Width.Value);
                }

                Timestamp = reader.ReadUInt64();
                reader.SkipBytes(Sample.Width.Value);
                return true;
            }

            public override object Clone()
            {
                var cloned = (TimestampedRESDSample)base.Clone();
                cloned.Sample = (T)this.Sample.Clone();
                return cloned;
            }

            public override bool TryReadFromStream(SafeBinaryReader reader)
            {
                Timestamp = reader.ReadUInt64();

                return Sample.TryReadFromStream(reader);
            }

            public override string ToString()
            {
                return $"{Sample} @ {Timestamp}";
            }

            public override int? Width => TimestampSize + Sample.Width;

            public override IDictionary<string, MetadataValue> Metadata => Sample.Metadata;

            public T Sample { get; private set; } = new T();

            public ulong Timestamp { get; private set; }

            private bool SkipWithDynamicWidth(SafeBinaryReader reader, int count)
            {
                for(var i = 0; i < count; ++i)
                {
                    if(reader.BaseStream.Position + TimestampSize > reader.Length)
                    {
                        return false;
                    }

                    Timestamp = reader.ReadUInt64();

                    if(!Sample.Skip(reader, 1))
                    {
                        return false;
                    }
                }

                return true;
            }

            private const int TimestampSize = 8;
        }
    }

    public enum BlockType
    {
        ArbitraryTimestampSamples = 1,
        ConstantFrequencySamples = 2,
    }
}
