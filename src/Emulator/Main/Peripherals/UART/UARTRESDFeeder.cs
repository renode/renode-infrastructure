//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.UART
{
    public static class UARTRESDFeederExtensions
    {
        /// <summary>
        /// Creates a model instance and registers it in the machine's peripheral tree.
        /// </summary>
        public static void CreateUARTRESDFeeder(this IMachine machine, string name)
        {
            var feeder = new UARTRESDFeeder(machine);
            machine.RegisterAsAChildOf(machine.SystemBus, feeder, NullRegistrationPoint.Instance);
            machine.SetLocalName(feeder, name);
        }
    }

    /// <summary>
    /// A UART data producer peripheral implementing <see cref="IUART"> for mocking, testing and scripting UART communication
    /// from RESD files.
    /// Model extends <see cref="VirtualConsole"> with support for outputting data from RESD files in a static or a responsive way.
    /// </summary>
    public class UARTRESDFeeder : VirtualConsole, IUnderstandRESD
    {
        /// <summary>
        /// Creates a model instance.
        /// </summary>
        public UARTRESDFeeder(IMachine machine) : base(machine)
        {
            this.machine = machine;
        }

        /// <summary>
        /// Implements <see cref="IPeripheral">.
        /// Will clear the internal buffer and stops use of any RESD files.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            dataStream?.Dispose();
            dataStream = null;
            samples = null;
            mode = FeedingMode.Normal;
        }

        /// <summary>
        /// Transmits byte <paramref name="value"/> when not in <see cref="RESDMode"> or <see cref="AllowUserOutput"> is <c>true</c>.
        /// </summary>
        /// <param name="value">
        /// Byte value to be transmitted.
        /// </param>
        public override void DisplayChar(byte value)
        {
            if(RESDMode && !AllowUserOutput)
            {
                return;
            }
            base.DisplayChar(value);
        }

        /// <summary>
        /// Starts data outputting from a RESD file.
        /// Data outputting can be performed in two ways based on <paramref name="mode">.
        /// <c>FeedingMode.Normal</c> mode will transmit data when data becomes available.
        /// <c>FeedingMode.Trigger</c> mode will reinterpret RESD timing data as a delay between <see cref="Trigger"> call and data transmission.
        /// </summary>
        /// <param name="filePath">
        /// A file to be used as a source for RESD data.
        /// </param>
        /// <param name="mode">
        /// Determines the way the data is handled.
        /// </param>
        /// <param name="channelId">
        /// Selects the data channel id to be used.
        /// </param>
        /// <param name="sampleOffsetType">
        /// Selects the time offsetting method as defined by <see cref="RESDStreamSampleOffset">.
        /// Ignored when <paramref name="mode"> is set to <c>FeedingMode.Trigger</c>.
        /// </param>
        /// <param name="sampleOffsetTime">
        /// Value by which the timing information should be offset.
        /// Ignored when <paramref name="mode"> is set to <c>FeedingMode.Trigger</c>.
        /// </param>
        public void FeedDataFromRESD(ReadFilePath filePath, FeedingMode mode = FeedingMode.Normal, uint channelId = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0)
        {
            this.mode = mode;
            dataStream?.Dispose();
            dataStream = null;
            samples = null;

            switch(mode)
            {
            case FeedingMode.Normal:
                dataStream = this.CreateRESDStream<BinaryDataSample>(filePath, channelId, sampleOffsetType, sampleOffsetTime, IsBlockInCurrentMode);
                dataStream.StartExactSampleFeedThread(this);
                break;
            case FeedingMode.Trigger:
                CreateSamplesEnumerator(filePath, channelId);
                break;
            default:
                throw new RecoverableException($"'{nameof(mode)}' value ({mode}) not supported");
            }

            this.Log(LogLevel.Noisy, "RESD stream set to {0}", filePath);
            RESDFileLoaded?.Invoke();
        }

        /// <summary>
        /// Triggers a scheduled transmission.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> when data for scheduled transmission exits and action been scheduled, <c>false</c> otherwise.
        /// </returns>
        /// <exception cref="RecoverableException">
        /// Thrown when a RESD based transmission has not been started with mode set to <c>FeedingMode.Trigger</c>.
        /// </remarks>
        public bool Trigger()
        {
            if(mode != FeedingMode.Trigger)
            {
                throw new RecoverableException("Trigger mode not enabled, trigger ignored");
            }

            if(samples == null)
            {
                return false;
            }

            var sample = samples.Current;
            if(!samples.MoveNext())
            {
                samples = null;
            }

            machine.ScheduleAction(sample.Key, _ => WriteSample(sample.Value));
            return true;
        }

        /// <summary>
        /// Retrieves next trigger sample.
        /// </summary>
        /// <param name="timeInterval">
        /// Set the the next trigger transmission's delay value.
        /// </param>
        /// <param name="sample">
        /// Set to the next trigger transmission value.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when next trigger sample is exists, <c>false</c> otherwise.
        /// </returns>
        /// <exception cref="RecoverableException">
        /// Thrown when a RESD based transmission has not been started with mode set to <c>FeedingMode.Trigger</c>.
        /// </remarks>
        public bool TryGetNextTriggerSample(out TimeInterval timeInterval, out byte[] sample)
        {
            if(mode != FeedingMode.Trigger)
            {
                throw new RecoverableException("Trigger mode not enabled, trigger samples not available");
            }

            if(samples == null)
            {
                timeInterval = default(TimeInterval);
                sample = default(byte[]);
                return false;
            }

            var currentSample = samples.Current;
            timeInterval = currentSample.Key;
            sample = (byte[])currentSample.Value.Data.Clone();
            return true;
        }

        /// <summary>
        /// Skips use of the next trigger sample.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> when next trigger sample is exists, <c>false</c> otherwise.
        /// </returns>
        /// <exception cref="RecoverableException">
        /// Thrown when a RESD based transmission has not been started with mode set to <c>FeedingMode.Trigger</c>.
        /// </remarks>
        public bool SkipTriggerSample()
        {
            if(mode != FeedingMode.Trigger)
            {
                throw new RecoverableException("Trigger mode not enabled, trigger samples not available");
            }

            if(!samples?.MoveNext() ?? false)
            {
                samples = null;
            }

            return samples != null;
        }

        /// <summary>
        /// Controls automatic transmission of received data.
        /// </summary>
        /// <remarks>
        /// Is forced to <c>false</c> when in <see cref="RESDMode"> and <see cref="AllowUserOutput"> is not enabled.
        /// </remarks>
        public override bool Echo
        {
            get => RESDMode ? (AllowUserOutput && base.Echo) : base.Echo;
            set => base.Echo = value;
        }

        /// <summary>
        /// Controls <see cref="DisplayChar"> and <see cref="Echo"> based data transmissions.
        /// </summary>
        public bool AllowUserOutput { get; set; }

        /// <summary>
        /// Is <c>true</c> when a data transmission from a RESD file is in progress.
        /// </summary>
        public bool RESDMode => dataStream != null || samples != null;

        /// <summary>
        /// Informs about completion of a RESD transmission.
        /// </summary>
        [field: Transient]
        public event Action SampleWritten;

        /// <summary>
        /// Informs about new RESD file loaded.
        /// </summary>
        [field: Transient]
        public event Action RESDFileLoaded;

        private void WriteSample(BinaryDataSample sample)
        {
            foreach(var b in sample.Data)
            {
                base.DisplayChar(b);
            }
            SampleWritten?.Invoke();
        }

        [OnRESDSample(SampleType.BinaryData)]
        private void HandleRESDSample(BinaryDataSample sample, TimeInterval _) => WriteSample(sample);

        private void CreateSamplesEnumerator(ReadFilePath filePath, uint channelId = 0)
        {
            var parser = new LowLevelRESDParser(filePath);
            parser.LogCallback += this.Log;

            var blocks = parser.GetDataBlockEnumerator<BinaryDataSample>().Where(block => block.ChannelId == channelId);
            samples = blocks.SelectMany(block =>
            {
                if(!IsBlockInCurrentMode(block))
                {
                    return Enumerable.Empty<KeyValuePair<TimeInterval, BinaryDataSample>>();
                }
                return block.Samples.Select(kv => new KeyValuePair<TimeInterval, BinaryDataSample>(kv.Key, kv.Value as BinaryDataSample));
            }).GetEnumerator();

            if(!samples.MoveNext())
            {
                this.Log(LogLevel.Warning, "Loaded RESD stream is empty");
                samples = null;
            }
        }

        private bool IsBlockInCurrentMode(IDataBlock block)
        {
            if(!TryGetModeMetadata(block, out var mode))
            {
                this.Log(LogLevel.Warning, "Encountered data block in RESD file that does not specify intended feeding mode, keeping data block");
            }
            else if(mode != this.mode)
            {
                this.Log(LogLevel.Warning, "Encountered data block in RESD file that specifies {0} feeding mode, but {1} is selected, skipping data block", mode, this.mode);
                return false;
            }
            return true;
        }

        private bool TryGetModeMetadata(IDataBlock block, out FeedingMode mode)
        {
            mode = default(FeedingMode);

            if(!block.Metadata.TryGetValue("feeding_mode", out var value) || !value.TryAs<string>(out var stringValue))
            {
                return false;
            }

            switch(stringValue)
            {
            case "normal":
                mode = FeedingMode.Normal;
                return true;
            case "trigger":
                mode = FeedingMode.Trigger;
                return true;
            default:
                return false;
            }
        }

        private FeedingMode mode;
        private RESDStream<BinaryDataSample> dataStream;
        private IEnumerator<KeyValuePair<TimeInterval, BinaryDataSample>> samples;

        private readonly IMachine machine;

        public enum FeedingMode
        {
            Normal,
            Trigger,
        }
    }
}
