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
                sampleReady = false;
                if(!currentSample.Skip(reader, count))
                {
                    reader.SeekToEnd();
                    return false;
                }
            }

            return !reader.EOF;
        }

        private bool sampleReady;
        private T currentSample;
        private readonly SafeBinaryReader reader;
    }
}
