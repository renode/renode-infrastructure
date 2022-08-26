//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class SensorSamplesFifo<T> where T : SensorSample, new()
    {
        public SensorSamplesFifo()
        {
            samplesFifo = new Queue<T>();
        }

        public void DumpOldestSample()
        {
            lock(samplesFifo)
            {
                samplesFifo.TryDequeue<T>(out _);
            }
        }

        public virtual void FeedSample(T sample)
        {
            lock(samplesFifo)
            {
                samplesFifo.Enqueue(sample);
            }
        }

        public void FeedSamplesFromBinaryFile(Machine machine, IPeripheral owner, string name, string path, string delayString, Func<List<decimal>, T> sampleConstructor = null)
        {
            if(sampleConstructor == null)
            {
                sampleConstructor = values =>
                {
                    var sample = new T();
                    sample.Load(values);
                    return sample;
                };
            }

            if(!TimeInterval.TryParse(delayString, out var delay))
            {
                throw new RecoverableException($"Invalid delay: {delayString}");
            }
            TimeInterval? firstPacketStartTime = null;

            var packets = SensorSamplesPacket<T>.ParseFile(path, sampleConstructor);
            foreach(var packet in packets)
            {
                firstPacketStartTime = firstPacketStartTime ?? packet.StartTime;
                var packetDelay = packet.StartTime - firstPacketStartTime.Value + delay;
                FeedSamplesPeriodically(machine, owner, name, packetDelay, packet.Frequency, packet.Samples);
            }
        }

        public void FeedSamplesFromFile(string path, Func<string[], T> sampleConstructor = null)
        {
            if(sampleConstructor == null)
            {
                sampleConstructor = stringArray =>
                {
                    var sample = new T();
                    return sample.TryLoad(stringArray) ? sample : null;
                };
            }

            lock(samplesFifo)
            {
                var samples = ParseSamplesFile(path, sampleConstructor);
                samplesFifo.EnqueueRange(samples);
            }
        }

        public virtual void Reset()
        {
            currentSample = null;
            samplesFifo.Clear();
        }

        public virtual bool TryDequeueNewSample()
        {
            return samplesFifo.TryDequeue(out currentSample);
        }

        public virtual T Sample => currentSample ?? DefaultSample;

        public T DefaultSample { get; } = new T();

        public uint SamplesCount => (uint)samplesFifo.Count;

        private void FeedSamplesPeriodically(Machine machine, IPeripheral owner, string name, TimeInterval startDelay, uint frequency, Queue<T> samples)
        {
            owner.Log(LogLevel.Noisy, "{0}: {1} samples will be fed at {2} Hz starting at {3:c}", name, samples.Count, frequency, startDelay.ToTimeSpan());

            Action feedSample = () =>
            {
                var sample = samples.Dequeue();
                owner.Log(LogLevel.Noisy, "{0} {1}: Feeding sample: {2}; samples left: {3}", machine.ElapsedVirtualTime.TimeElapsed, name, sample, samples.Count);
                FeedSample(sample);
            };

            Func<bool> stopCondition = () =>
            {
                if(samples.Count == 0)
                {
                    owner.Log(LogLevel.Noisy, "{0} {1}: All samples fed", machine.ElapsedVirtualTime.TimeElapsed, name);
                    return true;
                }
                return false;
            };

            var thread = machine.ObtainManagedThread(feedSample, frequency, name + " sample loader", owner, stopCondition);
            thread.StartDelayed(startDelay);
        }

        private IEnumerable<T> ParseSamplesFile(string path, Func<string[], T> sampleConstructor)
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

                        var sample = sampleConstructor(numbers);
                        if(sample == null)
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

        private T currentSample;
        private readonly Queue<T> samplesFifo;
    }
}
