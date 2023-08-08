//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Collections;
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

        public IManagedThread FeedSamplesPeriodically(IMachine machine, IPeripheral owner, string name, string delayString, uint frequency, IEnumerable<T> samples)
        {
            if(!TimeInterval.TryParse(delayString, out var delay))
            {
                throw new RecoverableException($"Invalid delay: {delayString}");
            }

            return FeedSamplesInner(machine, owner, name, delay, frequency, samples, true);
        }

        public IManagedThread FeedSamplesFromBinaryFile(IMachine machine, IPeripheral owner, string name, string path, string delayString, Func<List<decimal>, T> sampleConstructor = null, Action onFinish = null)
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

            var result = new ManagedThreadsContainer();
            var packets = SensorSamplesPacket<T>.ParseFile(path, sampleConstructor);
            foreach(var packet in packets)
            {
                firstPacketStartTime = firstPacketStartTime ?? packet.StartTime;
                var packetDelay = packet.StartTime - firstPacketStartTime.Value + delay;
                var feeder = FeedSamplesInner(machine, owner, name, packetDelay, packet.Frequency, packet.Samples, false, onFinish);
                result.Add(feeder);
            }

            return result;
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

        private IManagedThread FeedSamplesInner(IMachine machine, IPeripheral owner, string name, TimeInterval startDelay, uint frequency, IEnumerable<T> samples, bool repeatMode = false, Action onFinish = null)
        {
            var samplesBuffer = new SamplesBuffer(samples, autoRewind: repeatMode);
            
            owner.Log(LogLevel.Noisy, "{0}: {1} samples will be fed at {2} Hz starting at {3:c}", name, samplesBuffer.TotalSamplesCount, frequency, startDelay.ToTimeSpan());

            Action feedSample = () =>
            {
                if(!samplesBuffer.TryGetNextSample(out var sample))
                {
                    return;
                }
                owner.Log(LogLevel.Noisy, "{0} {1}: Feeding sample {2}/{3}: {4}", machine.ElapsedVirtualTime.TimeElapsed, name, samplesBuffer.CurrentSampleIndex, samplesBuffer.TotalSamplesCount, sample);
                FeedSample(sample);
            };

            Func<bool> stopCondition = () =>
            {
                if(samplesBuffer.IsFinished)
                {
                    owner.Log(LogLevel.Noisy, "{0} {1}: All samples fed", machine.ElapsedVirtualTime.TimeElapsed, name);
                    onFinish?.Invoke();
                    return true;
                }
                return false;
            };

            var feederThread = machine.ObtainManagedThread(feedSample, frequency, name + " sample loader", owner, stopCondition);
            feederThread.StartDelayed(startDelay);
            return feederThread;
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

        private class ManagedThreadsContainer : IManagedThread
        {
            public void Add(IManagedThread t)
            {
                threads.Add(t);
            }

            public void Dispose()
            {
                foreach(var t in threads)
                {
                    t.Dispose();
                }
            }

            public void Start()
            {
                foreach(var t in threads)
                {
                    t.Start();
                }
            }
            
            public void StartDelayed(TimeInterval i)
            {
                foreach(var t in threads)
                {
                    t.StartDelayed(i);
                }
            }
            
            public void Stop()
            {
                foreach(var t in threads)
                {
                    t.Stop();
                }
            }

            public uint Frequency
            {
                // We assume, that all threads have the same frequency
                get => threads[0].Frequency;
                set
                {
                    foreach(var t in threads)
                    {
                        t.Frequency = value;
                    }
                }
            }

            private readonly List<IManagedThread> threads = new List<IManagedThread>();
        }
        
        private class SamplesBuffer
        {
            public SamplesBuffer(IEnumerable<T> samples, bool autoRewind)
            {
                internalSamples = new List<T>(samples);
                this.autoRewind = autoRewind;
            }

            public bool TryGetNextSample(out T sample)
            {
                if(IsFinished)
                {
                    sample = default(T);
                    return false;
                }

                // if `autoRewind` was false, IsFinished would return `false`
                if(CurrentSampleIndex == internalSamples.Count)
                {
                    CurrentSampleIndex = 0;
                }

                sample = internalSamples[CurrentSampleIndex];
                CurrentSampleIndex++;
                return true;
            }

            public bool IsFinished => internalSamples.Count == 0 || (CurrentSampleIndex == internalSamples.Count && !autoRewind);

            public int TotalSamplesCount => internalSamples.Count;

            public int CurrentSampleIndex { get; private set; }

            private readonly bool autoRewind;
            private readonly List<T> internalSamples;
        }
    }
}
