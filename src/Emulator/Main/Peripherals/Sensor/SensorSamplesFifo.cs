//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class SensorSamplesFifo<T> where T : SensorSample, new()
    {
        public SensorSamplesFifo()
        {
            samplesFifo = new Queue<T>();
        }

        public void FeedSample(T sample)
        {
            lock(samplesFifo)
            {
                samplesFifo.Enqueue(sample);
            }
        }

        public void FeedSamplesFromFile(string path)
        {
            lock(samplesFifo)
            {
                var samples = ParseSamplesFile(path);
                samplesFifo.EnqueueRange(samples);
            }
        }

        public IEnumerable<T> ParseSamplesFile(string path)
        {
            var localQueue = new Queue<T>();
            var lineNumber = 0;

            try
            {
                using(var reader = File.OpenText(path))
                {
                    var line = "";
                    while((line = reader.ReadLine()) != null)
                    {
                        ++lineNumber;

                        if(line.Trim().StartsWith("#"))
                        {
                            // this is a comment, just ignore
                            continue;
                        }

                        var numbers = line.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToArray();

                        var sample = new T();
                        if(!sample.TryLoad(numbers))
                        {
                            throw new RecoverableException($"Wrong data file format at line {lineNumber}: {line}");
                        }
                        localQueue.Enqueue(sample);
                    }
                }
            }
            catch(Exception e)
            {
                if(e is RecoverableException)
                {
                    throw;
                }

                throw new RecoverableException($"There was a problem when reading samples file: {e.Message}");
            }

            return localQueue;
        }

        public bool TryDequeueNewSample()
        {
            return samplesFifo.TryDequeue(out currentSample);
        }

        public T Sample => currentSample ?? DefaultSample;

        public T DefaultSample { get; } = new T();

        public uint SamplesCount => (uint)samplesFifo.Count;

        private T currentSample;
        private readonly Queue<T> samplesFifo;
    }
}
