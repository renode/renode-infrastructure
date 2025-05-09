//
// Copyright (c) 2010-2025 Antmicro
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

            currentSample.ReadMetadata(reader);

            SampleDataOffset = reader.BaseStream.Position;
        }

        public T GetCurrentSample()
        {
            if(!sampleReady)
            {
                if(reader.EOF)
                {
                    return null;
                }
                ReadCurrentSample();
            }
            sampleReady = true;
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

            if(count < 0)
            {
                // although technically possible, we assume it's not supported
                return false;
            }

            if(reader.EOF)
            {
                if(sampleReady)
                {
                    sampleReady = false;
                    lastSample = currentSample;
                }
                return false;
            }

            // as GetCurrentSample lazily reads next sample, we have to
            // take it under account when skipping samples
            if(sampleReady)
            {
                count -= 1;
                sampleReady = false;
            }

            for(; count > 0; --count)
            {
                var previousPosition = reader.BaseStream.Position;
                if(!currentSample.Skip(reader, 1))
                {
                    throw new RESDException("Malformed RESD stream block, failed to skip sample, but data block has not ended");
                }

                if(reader.EOF)
                {
                    reader.BaseStream.Seek(previousPosition, SeekOrigin.Begin);
                    ReadCurrentSample();
                    lastSample = currentSample;
                    return false;
                }
            }

            return true;
        }

        private void ReadCurrentSample()
        {
            if(!currentSample.TryReadFromStream(reader))
            {
                throw new RESDException("Malformed RESD stream block, failed to read sample, but data block has not ended");
            }
        }

        public T LastSample => lastSample;

        public long SampleDataOffset { get; }

        private bool sampleReady;
        private T currentSample;
        private T lastSample;
        private readonly SafeBinaryReader reader;
    }
}
