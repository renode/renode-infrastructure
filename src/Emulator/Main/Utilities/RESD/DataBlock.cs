//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.RESD
{
    public abstract class DataBlock<T>
    {
        public DataBlock(DataBlockHeader header)
        {
        }

        public abstract RESDStreamStatus TryGetSample(ulong timestamp, out T sample);

        public abstract ulong StartTime { get; }
        public abstract T CurrentSample { get; }
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
            return RESDStreamStatus.OK;
        }

        public ulong Period { get; }
        public long Frequency => (long)(NanosecondsInSecond / Period);

        public override ulong StartTime { get; }
        public override T CurrentSample => samplesData.GetCurrentSample();

        private ConstantFrequencySamplesDataBlock(DataBlockHeader header, ulong startTime, ulong period, SafeBinaryReader reader) : base(header)
        {
            this.reader = reader;
            this.samplesData = new SamplesData<T>(reader);

            currentSampleTimestamp = startTime;

            Period = period;
            StartTime = startTime;
        }

        private ulong currentSampleTimestamp;

        private readonly SafeBinaryReader reader;
        private readonly SamplesData<T> samplesData;

        private const decimal NanosecondsInSecond = 1e9m;
    }

    public enum BlockType
    {
        ArbitraryTimestampSamples = 1,
        ConstantFrequencySamples = 2,
    }
}
