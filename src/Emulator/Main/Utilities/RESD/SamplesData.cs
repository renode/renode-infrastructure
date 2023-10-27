//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities.RESD
{
    public class SamplesData<T> where T : RESDSample, new()
    {
        public SamplesData(SafeBinaryReader reader)
        {
            this.reader = reader;
            this.currentSample = new T();

            currentSample.TryReadMetadata(reader);

            SampleDataOffset = reader.BaseStream.Position;
        }

        public T GetCurrentSample()
        {
            if(!sampleReady)
            {
                sampleReady = currentSample.TryReadFromStream(reader);
            }
            return currentSample;
        }

        public bool Move(int count)
        {
            if(count == 0)
            {
                // if count is zero, we can return sample if either it's ready or we
                // aren't at the end of the file
                return sampleReady || !reader.EOF;
            }

            if(reader.EOF)
            {
                return false;
            }

            if(count < 0)
            {
                // although technically possible, we assume it's not supported
                return false;
            }

            if(count > 0)
            {
                // as GetCurrentSample lazily reads next sample, we have to
                // take it under account when skipping samples
                if(!currentSample.Skip(reader, count - (sampleReady ? 1 : 0)))
                {
                    reader.SeekToEnd();
                    return false;
                }
                sampleReady = false;
            }

            return !reader.EOF;
        }

        public long SampleDataOffset { get; }

        private bool sampleReady;
        private T currentSample;
        private readonly SafeBinaryReader reader;
    }
}
