//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.RESD
{
    public class SamplesData<T> where T : RESDSample, new()
    {
        public SamplesData(SafeBinaryReader reader, long samplesBorderOffset)
        {
            this.reader = reader;
            this.currentSample = new T();
            this.samplesBorderOffset = samplesBorderOffset;

            currentSample.TryReadMetadata(reader);
        }

        public T GetCurrentSample()
        {
            if(!sampleReady)
            {
                currentSample.TryReadFromStream(reader);
            }
            return currentSample;
        }

        public bool Move(int count)
        {
            if(reader.BaseStream.Position >= samplesBorderOffset)
            {
                // end of samples data
                return false;
            }

            if(count < 0)
            {
                // although technically possible, we assume it's not supported
                return false;
            }

            if(count > 0)
            {
                sampleReady = false;
                if(!currentSample.Skip(reader, count))
                {
                    return false;
                }
            }

            return !reader.EOF;
        }

        private bool sampleReady;
        private T currentSample;
        private readonly SafeBinaryReader reader;
        private readonly long samplesBorderOffset;
    }
}
