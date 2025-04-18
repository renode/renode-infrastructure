//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Utilities.RESD
{
    public enum RESDStreamStatus
    {
        // Provided sample is from requested timestamp
        OK = 0,
        // There are no samples for given samples in current block yet
        BeforeStream = -1,
        // There are no more samples of given type in the block/file
        AfterStream = -2,
    }

    public enum RESDStreamSampleOffset
    {
        // Use specified sample offset
        Specified,
        // User current virtual-time timestamp as offset
        CurrentVirtualTime,
    }

    public static class RESDStreamExtension
    {
        public static RESDStream<T> CreateRESDStream<T>(this IPeripheral @this, ReadFilePath path, uint channel,
            RESDStreamSampleOffset offsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0,
            Predicate<DataBlock<T>> extraFilter = null) where T: RESDSample, new()
        {
            if(offsetType == RESDStreamSampleOffset.CurrentVirtualTime)
            {
                var machine = @this.GetMachine();
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                sampleOffsetTime += (long)machine.ClockSource.CurrentValue.TotalMicroseconds * -1000L;
            }

            var stream = new RESDStream<T>(path, channel, sampleOffsetTime, extraFilter);
            stream.Owner = @this;
            return stream;
        }

        public static RESDStream<T, Out> CreateRESDStream<T, Out>(this IPeripheral @this, ReadFilePath path, uint channel, Func<T, Out> transformer,
            RESDStreamSampleOffset offsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0,
            Predicate<DataBlock<T>> extraFilter = null) where T: RESDSample, new()
        {
            if(offsetType == RESDStreamSampleOffset.CurrentVirtualTime)
            {
                var machine = @this.GetMachine();
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                sampleOffsetTime += (long)machine.ClockSource.CurrentValue.TotalMicroseconds * -1000L;
            }

            var stream = new RESDStream<T, Out>(path, channel, transformer, sampleOffsetTime, extraFilter);
            stream.Owner = @this;
            return stream;
        }

        public static IManagedThread StartSampleFeedThread<T>(this RESDStream<T> @this, IUnderstandRESD owner, uint frequency,
            ulong startTime = 0, string domain = null, bool shouldStop = true) where T: RESDSample, new()
        {
            Action<T, TimeInterval> beforeCallback = FindCallback<T>(owner, @this.SampleType, RESDStreamStatus.BeforeStream, @this.Channel, domain);
            Action<T, TimeInterval> currentCallback = FindCallback<T>(owner, @this.SampleType, RESDStreamStatus.OK, @this.Channel, domain);
            Action<T, TimeInterval> afterCallback = FindCallback<T>(owner, @this.SampleType, RESDStreamStatus.AfterStream, @this.Channel, domain);
            Action<T, TimeInterval, RESDStreamStatus> sampleCallback = (sample, ts, status) =>
            {
                switch(status)
                {
                    case RESDStreamStatus.BeforeStream:
                        beforeCallback(sample, ts);
                        break;
                    case RESDStreamStatus.OK:
                        currentCallback(sample, ts);
                        break;
                    case RESDStreamStatus.AfterStream:
                        afterCallback(sample, ts);
                        break;
                }
            };
            return @this.StartSampleFeedThread(owner, frequency, sampleCallback, startTime, shouldStop);
        }

        public static ISimpleManagedThread StartExactSampleFeedThread<T>(this RESDStream<T> @this, IUnderstandRESD owner,
            ulong startTime = 0, string domain = null) where T: RESDSample, new()
        {
            Action<T, TimeInterval> currentCallback = FindCallback<T>(owner, @this.SampleType, RESDStreamStatus.OK, @this.Channel, domain);
            Action<T, TimeInterval> afterCallback = FindCallback<T>(owner, @this.SampleType, RESDStreamStatus.AfterStream, @this.Channel, domain);
            Action<T, TimeInterval, RESDStreamStatus> sampleCallback = (sample, ts, status) =>
            {
                switch(status)
                {
                    case RESDStreamStatus.OK:
                        currentCallback(sample, ts);
                        break;
                    case RESDStreamStatus.AfterStream:
                        afterCallback(sample, ts);
                        break;
                }
            };
            return @this.StartExactSampleFeedThread(owner, sampleCallback, startTime);
        }

        public static IManagedThread StartSampleFeedThread<T, Out>(this RESDStream<T, Out> @this, IUnderstandRESD owner, uint frequency,
            ulong startTime = 0, string domain = null, bool shouldStop = true) where T: RESDSample, new()
        {
            Action<Out, TimeInterval> beforeCallback = FindCallback<Out>(owner, @this.SampleType, RESDStreamStatus.BeforeStream, @this.Channel, domain);
            Action<Out, TimeInterval> currentCallback = FindCallback<Out>(owner, @this.SampleType, RESDStreamStatus.OK, @this.Channel, domain);
            Action<Out, TimeInterval> afterCallback = FindCallback<Out>(owner, @this.SampleType, RESDStreamStatus.AfterStream, @this.Channel, domain);
            Action<Out, TimeInterval, RESDStreamStatus> sampleCallback = (sample, ts, status) =>
            {
                switch(status)
                {
                    case RESDStreamStatus.BeforeStream:
                        beforeCallback(sample, ts);
                        break;
                    case RESDStreamStatus.OK:
                        currentCallback(sample, ts);
                        break;
                    case RESDStreamStatus.AfterStream:
                        afterCallback(sample, ts);
                        break;
                }
            };
            return @this.StartSampleFeedThread(owner, frequency, sampleCallback, startTime, shouldStop);
        }

        public static ISimpleManagedThread StartExactSampleFeedThread<T, Out>(this RESDStream<T, Out> @this, IUnderstandRESD owner,
            ulong startTime = 0, string domain = null) where T: RESDSample, new()
        {
            Action<Out, TimeInterval> currentCallback = FindCallback<Out>(owner, @this.SampleType, RESDStreamStatus.OK, @this.Channel, domain);
            Action<Out, TimeInterval> afterCallback = FindCallback<Out>(owner, @this.SampleType, RESDStreamStatus.AfterStream, @this.Channel, domain);
            Action<Out, TimeInterval, RESDStreamStatus> sampleCallback = (sample, ts, status) =>
            {
                switch(status)
                {
                    case RESDStreamStatus.OK:
                        currentCallback(sample, ts);
                        break;
                    case RESDStreamStatus.AfterStream:
                        afterCallback(sample, ts);
                        break;
                }
            };
            return @this.StartExactSampleFeedThread(owner, sampleCallback, startTime);
        }

        public static RESDStreamStatus TryGetCurrentSample<T, Out>(this RESDStream<T> @this, IPeripheral peripheral, Func<T, Out> transformer,
            out Out sample, out TimeInterval timestamp) where T: RESDSample, new()
        {
            var result = @this.TryGetCurrentSample(peripheral, out var originalSample, out timestamp);
            sample = transformer.TransformSample(originalSample);
            return result;
        }

        public static Out TransformSample<T, Out>(this Func<T, Out> @this, T sample)
            where T: RESDSample, new()
        {
            if(sample == null)
            {
                return default(Out);
            }
            return @this(sample);
        }

        private static Action<Out, TimeInterval> FindCallback<Out>(IUnderstandRESD instance, SampleType sampleType, RESDStreamStatus status, uint channel, string domain)
        {
            Func<ParameterInfo[], bool> checkCorrectPrototype = parameters =>
                parameters.Length == 2 &&
                parameters[0].ParameterType == typeof(Out) &&
                parameters[1].ParameterType == typeof(TimeInterval);

            var methodInfo = instance.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(info => checkCorrectPrototype(info.GetParameters()))
                .FirstOrDefault(info =>
                    info.GetCustomAttributes(typeof(RESDSampleCallbackAttribute))
                        .OfType<RESDSampleCallbackAttribute>()
                        .Any(attribute =>
                            attribute.SampleType == sampleType &&
                            attribute.Status == status &&
                            (!attribute.ChannelId.HasValue || attribute.ChannelId == channel) &&
                            attribute.Domain == domain));

            if(methodInfo == null)
            {
                return delegate {};
            }

            return (sample, ts) => methodInfo.Invoke(instance, new object[] { sample, ts });
        }
    }

    public class RESDStream<T, Out> : RESDStream<T> where T: RESDSample, new()
    {
        public RESDStream(ReadFilePath path, uint channel, Func<T, Out> transformer, long sampleOffsetTime = 0, Predicate<DataBlock<T>> extraFilter = null)
            : base(path, channel, sampleOffsetTime, extraFilter)
        {
            this.transformer = transformer;
        }

        public RESDStreamStatus TryGetSample(ulong timestamp, out Out sample, long? overrideSampleOffsetTime = null)
        {
            var result = TryGetSample(timestamp, out T originalSample, overrideSampleOffsetTime);
            sample = transformer.TransformSample(originalSample);
            return result;
        }

        public RESDStreamStatus TryGetCurrentSample(IPeripheral peripheral, out Out sample, out TimeInterval timestamp)
        {
            var result = TryGetCurrentSample(peripheral, out T originalSample, out timestamp);
            sample = transformer.TransformSample(originalSample);
            return result;
        }

        public RESDStreamStatus TryGetNextSample(out TimeInterval timestamp, out Out sample)
        {
            var result = TryGetNextSample(out timestamp, out T originalSample);
            sample = transformer.TransformSample(originalSample);
            return result;
        }

        public IManagedThread StartSampleFeedThread(IPeripheral owner, uint frequency, Action<Out, TimeInterval, RESDStreamStatus> newSampleCallback, ulong startTime = 0, bool shouldStop = true)
        {
            Action<T, TimeInterval, RESDStreamStatus> transformedCallback =
                (sample, timestamp, status) => newSampleCallback(transformer.TransformSample(sample), timestamp, status);
            return StartSampleFeedThread(owner, frequency, transformedCallback, startTime, shouldStop);
        }

        public ISimpleManagedThread StartExactSampleFeedThread(IPeripheral owner, Action<Out, TimeInterval, RESDStreamStatus> newSampleCallback, ulong startTime = 0)
        {
            Action<T, TimeInterval, RESDStreamStatus> transformedCallback =
                (sample, timestamp, status) => newSampleCallback(transformer.TransformSample(sample), timestamp, status);
            return StartExactSampleFeedThread(owner, transformedCallback, startTime);
        }

        private readonly Func<T, Out> transformer;
    }

    public class RESDStream<T> : IDisposable where T : RESDSample, new()
    {
        public RESDStream(ReadFilePath path, uint channel, long sampleOffsetTime = 0, Predicate<DataBlock<T>> extraFilter = null)
        {
            var sampleTypeAttribute = typeof(T).GetCustomAttributes(typeof(SampleTypeAttribute), true).FirstOrDefault() as SampleTypeAttribute;
            if(sampleTypeAttribute == null)
            {
                throw new RESDException($"Unsupported RESD sample type: {typeof(T).Name}");
            }

            SampleType = sampleTypeAttribute.SampleType;
            Channel = channel;

            this.sampleOffsetTime = sampleOffsetTime;
            this.managedThreads = new List<ISimpleManagedThread>();
            this.parser = new LowLevelRESDParser(path);
            this.parser.LogCallback += (logLevel, message) => Owner?.Log(logLevel, message);
            this.blockEnumerator = parser.GetDataBlockEnumerator<T>().GetEnumerator();
            this.extraFilter = extraFilter;

            PrereadFirstBlock();
        }

        public RESDStreamStatus TryGetCurrentSample(IPeripheral peripheral, out T sample, out TimeInterval timestamp)
        {
            var machine = peripheral.GetMachine();
            timestamp = machine.ClockSource.CurrentValue;
            var timestampInNanoseconds = timestamp.TotalNanoseconds;
            return TryGetSample(timestampInNanoseconds, out sample);
        }

        public RESDStreamStatus TryGetSample(ulong timestamp, out T sample, long? overrideSampleOffsetTime = null)
        {
            currentTimestampInNanoseconds = timestamp;
            var currentSampleOffsetTime = overrideSampleOffsetTime ?? sampleOffsetTime;
            if(currentSampleOffsetTime < 0)
            {
                if(timestamp >= (ulong)(-currentSampleOffsetTime))
                {
                    timestamp = timestamp - (ulong)(-currentSampleOffsetTime);
                }
                else
                {
                    Owner?.Log(LogLevel.Debug, "RESD: Tried getting sample at timestamp {0}ns, before the start time of the current block"
                        + " after applying the {1}ns offset", timestamp, currentSampleOffsetTime);
                    sample = null;
                    return RESDStreamStatus.BeforeStream;
                }
            }
            else
            {
                timestamp = timestamp + (ulong)currentSampleOffsetTime;
            }

            if(blockEnumerator == null)
            {
                Owner?.Log(LogLevel.Debug, "RESD: Tried getting sample at timestamp {0}ns after the last sample of the current block", timestamp);
            }

            while(blockEnumerator != null)
            {
                if(currentBlock == null)
                {
                    if(!TryGetNextBlock(out currentBlock))
                    {
                        Owner?.Log(LogLevel.Debug, "RESD: That was the last block of the file");
                        break;
                    }
                    MetadataChanged?.Invoke();
                }

                switch(currentBlock.TryGetSample(timestamp, out sample))
                {
                    case RESDStreamStatus.BeforeStream:
                        Owner?.Log(LogLevel.Debug, "RESD: Tried getting sample at timestamp {0}ns, before the first sample in the block", timestamp);
                        sample = null;
                        return RESDStreamStatus.BeforeStream;
                    case RESDStreamStatus.OK:
                        // Just return sample
                        Owner?.Log(LogLevel.Debug, "RESD: Getting sample at timestamp {0}ns: {1}", timestamp, sample);
                        return RESDStreamStatus.OK;
                    case RESDStreamStatus.AfterStream:
                        // Find next block
                        Owner?.Log(LogLevel.Debug, "RESD: Tried getting sample at timestamp {0}ns after the last sample of the current block", timestamp);
                        currentBlock = null;
                        lastSample = sample;
                        continue;
                }

                return RESDStreamStatus.OK;
            }

            sample = lastSample;
            return RESDStreamStatus.AfterStream;
        }

        public RESDStreamStatus TryGetNextSample(out TimeInterval timestamp, out T sample, long? overrideSampleOffsetTime = null)
        {
            var currentSampleOffsetTime = overrideSampleOffsetTime ?? sampleOffsetTime;

            while(blockEnumerator != null)
            {
                if(currentBlock == null)
                {
                    if(!TryGetNextBlock(out currentBlock))
                    {
                        break;
                    }
                    MetadataChanged?.Invoke();
                }

                switch(currentBlock.TryGetNextSample(out timestamp, out sample))
                {
                case RESDStreamStatus.OK:
                    // Just return sample
                    if(currentSampleOffsetTime < 0)
                    {
                        timestamp += TimeInterval.FromNanoseconds((ulong)-currentSampleOffsetTime);
                    }
                    else
                    {
                        timestamp -= TimeInterval.FromNanoseconds((ulong)currentSampleOffsetTime);
                    }
                    Owner?.Log(LogLevel.Debug, "RESD: Getting next sample: {1} at timestamp {0}ns", timestamp.TotalNanoseconds, sample);
                    currentTimestampInNanoseconds = timestamp.TotalNanoseconds;
                    return RESDStreamStatus.OK;
                case RESDStreamStatus.AfterStream:
                    // Find next block
                    Owner?.Log(LogLevel.Debug, "RESD: Tried getting next sample after the last sample of the current block");
                    currentBlock = null;
                    continue;
                case RESDStreamStatus.BeforeStream:
                    // fall-through
                default:
                    throw new Exception("Unreachable");
                }
            }

            Owner?.Log(LogLevel.Debug, "RESD: That was the last block of the file");
            sample = null;
            timestamp = default(TimeInterval);
            return RESDStreamStatus.AfterStream;
        }

        // If shouldStop is false, the thread will continue running after the end of the stream
        public IManagedThread StartSampleFeedThread(IPeripheral owner, uint frequency, Action<T, TimeInterval, RESDStreamStatus> newSampleCallback, ulong startTime = 0, bool shouldStop = true)
        {
            var machine = owner.GetMachine();
            Action feedSample = () =>
            {
                var status = TryGetCurrentSample(owner, out var sample, out var timestamp);
                newSampleCallback(sample, timestamp, status);
            };

            Func<bool> stopCondition = () =>
            {
                if(blockEnumerator == null)
                {
                    if(shouldStop)
                    {
                        feedSample(); // invoke action to update timestamp and status before stopping thread
                        Owner?.Log(LogLevel.Debug, "RESD: End of sample feeding thread detected");
                    }
                    return shouldStop;
                }
                return false;
            };

            var thread = machine.ObtainManagedThread(feedSample, frequency, "RESD stream thread", owner, stopCondition);
            var delayInterval = TimeInterval.FromMicroseconds(startTime / 1000);
            Owner?.Log(LogLevel.Debug, "RESD: Starting samples feeding thread at frequency {0}Hz delayed by {1}us", frequency, delayInterval);
            thread.StartDelayed(delayInterval);
            managedThreads.Add(thread);
            return thread;
        }

        public ISimpleManagedThread StartExactSampleFeedThread(IPeripheral owner, Action<T, TimeInterval, RESDStreamStatus> newSampleCallback, ulong startTime = 0)
        {
            Func<TimeInterval, ISimpleManagedThread, TimeInterval> nextEvent = (now, @this) =>
            {
                // Assert that samples from the past are skipped, so that on each start
                // the next sample is guarantied to be the first from the future
                TryGetSample(now.TotalNanoseconds, out var _);
                if(RESDStreamStatus.OK != TryGetNextSample(out var timestamp, out _))
                {
                    @this.Stop();
                    newSampleCallback?.Invoke(null, now, RESDStreamStatus.AfterStream);
                    return default(TimeInterval);
                }

                return timestamp;
            };

            Action<TimeInterval, ISimpleManagedThread> callback = (now, _) =>
            {
                newSampleCallback?.Invoke(CurrentSample, now, RESDStreamStatus.OK);
            };

            var thread = new ExactSampleThread(owner.GetMachine(), nextEvent, callback);
            managedThreads.Add(thread);

            if(startTime == 0)
            {
                thread.Start();
            }
            else
            {
                thread.StartDelayed(TimeInterval.FromNanoseconds(startTime));
            }
            return thread;
        }

        public void Dispose()
        {
            foreach(var thread in managedThreads)
            {
                thread.Dispose();
            }
            managedThreads.Clear();
            blockEnumerator?.Dispose();
        }

        // The `Owner` property is used to log detailed messages
        // about the process of the RESD file parsing.
        // If it's not set (set to `null`, by default) no log messages
        // will be generated.
        public IEmulationElement Owner { get; set; }

        public T CurrentSample => currentBlock?.CurrentSample;
        public DataBlock<T> CurrentBlock => currentBlock;
        public long CurrentBlockNumber => currentBlockNumber;
        public SampleType SampleType { get; }
        public uint Channel { get; }
        public Action MetadataChanged;

        private void PrereadFirstBlock()
        {
            if(!TryGetNextBlock(out currentBlock))
            {
                throw new RESDException($"Provided RESD file doesn't contain data for {typeof(T)}");
            }
            Owner?.Log(LogLevel.Debug, "RESD: First sample of the file has timestamp {0}ns", currentBlock.StartTime);
        }

        private bool TryGetNextBlock(out DataBlock<T> block)
        {
            if(blockEnumerator == null)
            {
                block = null;
                return false;
            }

            while(blockEnumerator.TryGetNext(out block))
            {
                currentBlockNumber++;
                if(block.ChannelId != Channel || !(extraFilter?.Invoke(block) ?? true))
                {
                    Owner?.Log(LogLevel.Debug, "RESD: Skipping block of type {0} and size {1} bytes", block.BlockType, block.DataSize);
                    continue;
                }
                return true;
            }

            blockEnumerator = null;
            return false;
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            // After stream ended, current block is null so serialize timestamp as the last registered time
            // so after serialization it will also return RESDStreamStatus.AfterStream
            serializedTimestamp = currentBlock != null ? currentBlock.CurrentTimestamp : currentTimestampInNanoseconds;
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            PrereadFirstBlock();
            TryGetSample(serializedTimestamp, out _);
        }

        [Transient]
        private DataBlock<T> currentBlock;
        [Transient]
        private IEnumerator<DataBlock<T>> blockEnumerator;
        private T lastSample;
        private ulong serializedTimestamp;
        private ulong currentTimestampInNanoseconds;
        private long currentBlockNumber;
        private long sampleOffsetTime;

        private readonly LowLevelRESDParser parser;
        private readonly IList<ISimpleManagedThread> managedThreads;
        private readonly Predicate<DataBlock<T>> extraFilter;

        private class ExactSampleThread : ISimpleManagedThread
        {
            public ExactSampleThread(IMachine machine, Func<TimeInterval, ISimpleManagedThread, TimeInterval> getNextEvent, Action<TimeInterval, ISimpleManagedThread> eventCallback)
            {
                this.machine = machine;
                this.eventCallback = eventCallback;
                GetNextEvent = getNextEvent;

                var clockEntry = new ClockEntry(period: 0, frequency: NanosecondsInSecond, handler: HandleEvent, owner: null, localName: "RESDStream.ExactSampleThread", enabled: false, workMode: WorkMode.OneShot);
                machine.ClockSource.AddClockEntry(clockEntry);
            }

            public void Start()
            {
                stopped = false;
                EnqueueEvent();
            }

            public void StartDelayed(TimeInterval delay)
            {
                stopped = false;
                machine.ScheduleAction(delay, _ => Start());
            }

            public void Stop()
            {
                stopped = true;
            }

            public void Dispose()
            {
                Stop();
                machine.ClockSource.TryRemoveClockEntry(HandleEvent);
            }

            [PostDeserialization]
            private void EnqueueEvent()
            {
                if(stopped)
                {
                    return;
                }
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }

                var timeInterval = GetNextEvent(machine.ElapsedVirtualTime.TimeElapsed, this);
                if(stopped)
                {
                    return;
                }

                machine.ClockSource.ExchangeClockEntryWith(HandleEvent, entry => entry.With(
                    period: timeInterval.TotalNanoseconds,
                    value: timeInterval.TotalNanoseconds,
                    enabled: true
                ));
            }

            private void HandleEvent()
            {
                if(stopped)
                {
                    return;
                }
                eventCallback.Invoke(machine.ClockSource.CurrentValue, this);
                EnqueueEvent();
            }

            private Func<TimeInterval, ISimpleManagedThread, TimeInterval> GetNextEvent { get; }

            private bool stopped;
            private readonly IMachine machine;
            private readonly Action<TimeInterval, ISimpleManagedThread> eventCallback;

            private const long NanosecondsInSecond = 1 * 1000 * 1000 * 1000;
        }
    }
}
