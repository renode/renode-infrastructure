//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Analog
{
    public class ADCChannel
    {
        public static IEnumerable<uint> ParseSamplesFile(string path)
        {
            var localQueue = new Queue<uint>();
            var lineNumber = 0;

            try
            {
                using(var reader = File.OpenText(path))
                {
                    var line = "";
                    while((line = reader.ReadLine()) != null)
                    {
                        ++lineNumber;
                        if(!uint.TryParse(line.Trim(), out var sample))
                        {
                            throw new RecoverableException($"Wrong data file format at line {lineNumber}. Expected an unsigned integer number, but got '{line}'");
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

                // this is to nicely handle IO errors in monitor
                throw new RecoverableException(e.Message);
            }

            return localQueue;
        }

        public ADCChannel(IPeripheral parent, int channelId)
        {
            this.channelId = channelId;
            this.parent = parent;
            samples = new Queue<uint>();
        }

        public void PrepareSample()
        {
            lock(lockObject)
            {
                if(samples.Count == 0 && bufferedSamples != null)
                {
                    samples.EnqueueRange(bufferedSamples);
                }

                if(!samples.TryDequeue(out currentSample))
                {
                    currentSample = 0;
                    parent.Log(LogLevel.Warning, "No more samples available in channel {0}", channelId);
                }
            }
        }

        public uint GetSample()
        {
            lock(lockObject)
            {
                return currentSample;
            }
        }

        public void FeedSample(uint sample, int repeat = 1)
        {
            lock(lockObject)
            {
                if(repeat == -1)
                {
                    bufferedSamples = new uint[] { sample };
                }
                else
                {
                    bufferedSamples = null;
                    for(var i = 0; i < repeat; i++)
                    {
                        samples.Enqueue(sample);
                    }
                }
            }
        }

        public void FeedSample(IEnumerable<uint> samplesCollection, int repeat = 1)
        {
            lock(lockObject)
            {
                if(repeat == -1)
                {
                    bufferedSamples = samplesCollection;
                }
                else
                {
                    bufferedSamples = null;
                    for(var i = 0; i < repeat; i++)
                    {
                        samples.EnqueueRange(samplesCollection);
                    }
                }
            }
        }

        public void Reset()
        {
            lock(lockObject)
            {
                bufferedSamples = null;
                samples.Clear();
                currentSample = 0;
            }
        }

        // Sample returned when queried
        private uint currentSample;
        // Used for implementing repeat, need to store a sequence of samples
        private IEnumerable<uint> bufferedSamples;

        private readonly object lockObject = new object();

        private readonly IPeripheral parent;
        private readonly int channelId;
        // The current sample sequence
        private readonly Queue<uint> samples;
    }
}