//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Sensors
{
    // TODO: FIFO for other data than the accelerometer
    public class LIS2DW12 : II2CPeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, ITemperatureSensor, IUnderstandRESD
    {
        public LIS2DW12(IMachine machine)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            Interrupt1 = new GPIO();
            Interrupt2 = new GPIO();
            readyEnabledAcceleration = new IFlagRegisterField[2];
            fifoThresholdEnabled = new IFlagRegisterField[2];
            fifoFullEnabled = new IFlagRegisterField[2];
            DefineRegisters();

            accelerationFifo = new LIS2DW12_FIFO(this, "fifo", MaxFifoSize);
            accelerationFifo.OnOverrun += UpdateInterrupts;
            // Set the default acceleration here, it needs to be preserved across resets
            DefaultAccelerationZ = 1m;

            this.machine = machine;
        }

        public void FeedAccelerationSample(decimal x, decimal y, decimal z, uint repeat = 1)
        {
            FeedAccelerationSampleInner(x, y, z, keepOnReset: true, repeat: repeat);
        }

        [OnRESDSample(SampleType.Acceleration)]
        [BeforeRESDSample(SampleType.Acceleration)]
        private void HandleAccelerationSample(AccelerationSample sample, TimeInterval timestamp)
        {
            if(sample != null)
            {
                // Divide by 10^6 as RESD specification says AccelerationSamples are in μg,
                // while the peripheral's measurement unit is g.
                FeedAccelerationSampleInner(
                    sample.AccelerationX / 1e6m,
                    sample.AccelerationY / 1e6m,
                    sample.AccelerationZ / 1e6m,
                    keepOnReset: false
                );
            }
            else
            {
                FeedAccelerationSampleInner(
                    DefaultAccelerationX,
                    DefaultAccelerationY,
                    DefaultAccelerationZ,
                    keepOnReset: false
                );
            }
        }

        [AfterRESDSample(SampleType.Acceleration)]
        private void HandleAccelerationSampleEnded(AccelerationSample sample, TimeInterval timestamp)
        {
            if(isAfterStream)
            {
                feederThread?.Stop();
                feederThread = null;
                accelerationFifo.KeepFifoOnReset = true;
                isAfterStream = false;
                return;
            }
            HandleAccelerationSample(sample, timestamp);
            isAfterStream = true;
        }

        public void FeedAccelerationSamplesFromRESD(string path, uint channel = 0, ulong startTime = 0,
            RESDStreamSampleOffset sampleOffsetType = RESDStreamSampleOffset.Specified, long sampleOffsetTime = 0,
            RESDType type = RESDType.Normal)
        {
            if(type == RESDType.MultiFrequency)
            {
                FeedMultiFrequencyAccelerationSamplesFromRESD(path, startTime);
                return;
            }
            else if(type != RESDType.Normal)
            {
                throw new RecoverableException($"Unhandled RESD type {type}");
            }

            resdStream = this.CreateRESDStream<AccelerationSample>(path, channel, sampleOffsetType, sampleOffsetTime);
            accelerationFifo.KeepFifoOnReset = false;
            feederThread?.Stop();
            feederThread = resdStream.StartSampleFeedThread(this,
                SampleRate,
                startTime: startTime,
                shouldStop: false
            );
            isAfterStream = false;
        }

        public void FeedAccelerationSample(string path)
        {
            accelerationFifo.KeepFifoOnReset = true;
            accelerationFifo.FeedSamplesFromFile(path);
            UpdateInterrupts();
        }

        public void FinishTransmission()
        {
            regAddress = 0;
            state = State.WaitingForRegister;
            this.Log(LogLevel.Noisy, "Transmission finished");
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            SetScaleDivider();
            state = State.WaitingForRegister;
            regAddress = 0;
            Interrupt1.Set(false);
            Interrupt2.Set(false);
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }
            this.Log(LogLevel.Noisy, "Write with {0} bytes of data: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));
            if(state == State.WaitingForRegister)
            {
                regAddress = (Registers)data[0];
                this.Log(LogLevel.Noisy, "Preparing to access register {0} (0x{0:X})", regAddress);
                state = State.WaitingForData;
                data = data.Skip(1).ToArray();
            }
            if(state == State.WaitingForData)
            {
                InternalWrite(data);
            }
        }

        public byte[] Read(int count)
        {
            if(state == State.WaitingForRegister)
            {
                this.Log(LogLevel.Warning, "Unexpected read in state {0}", state);
                return new byte[count];
            }
            this.Log(LogLevel.Noisy, "Reading {0} bytes from register {1} (0x{1:X})", count, regAddress);
            var result = new byte[count];
            for(var i = 0; i < result.Length; i++)
            {
                result[i] = RegistersCollection.Read((byte)regAddress);
                this.Log(LogLevel.Noisy, "Read value 0x{0:X}", result[i]);
                if(autoIncrement.Value)
                {
                    RegistersAutoIncrement();
                }
            }
            return result;
        }

        public decimal AccelerationX
        {
            get => CurrentSample.X;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                CurrentSample.X = value;
                this.Log(LogLevel.Noisy, "AccelerationX set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal AccelerationY
        {
            get => CurrentSample.Y;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                CurrentSample.Y = value;
                this.Log(LogLevel.Noisy, "AccelerationY set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal AccelerationZ
        {
            get => CurrentSample.Z;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                CurrentSample.Z = value;
                this.Log(LogLevel.Noisy, "AccelerationZ set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal DefaultAccelerationX
        {
            get => DefaultSample.X;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                DefaultSample.X = value;
                this.Log(LogLevel.Noisy, "DefaultAccelerationX set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal DefaultAccelerationY
        {
            get => DefaultSample.Y;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                DefaultSample.Y = value;
                this.Log(LogLevel.Noisy, "DefaultAccelerationY set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal DefaultAccelerationZ
        {
            get => DefaultSample.Z;
            set
            {
                if(IsAccelerationOutOfRange(value))
                {
                    return;
                }
                DefaultSample.Z = value;
                this.Log(LogLevel.Noisy, "DefaultAccelerationZ set to {0}", value);
                UpdateInterrupts();
            }
        }

        public decimal Temperature
        {
            get => temperature;
            set
            {
                if(IsTemperatureOutOfRange(value))
                {
                    return;
                }
                temperature = value;
                this.Log(LogLevel.Noisy, "Temperature set to {0}", temperature);
                UpdateInterrupts();
            }
        }

        public GPIO Interrupt1 { get; }
        public GPIO Interrupt2 { get; }
        public ByteRegisterCollection RegistersCollection { get; }
        public uint SampleRate
        {
            get => sampleRate;
            private set
            {
                sampleRate = value;
                if(feederThread != null)
                {
                    feederThread.Frequency = sampleRate;
                }
                this.Log(LogLevel.Debug, "Sampling rate set to {0}", SampleRate);
                SampleRateChanged?.Invoke(value);
            }
        }

        private void FeedMultiFrequencyAccelerationSamplesFromRESD(string path, ulong startTime)
        {
            var parser = new LowLevelRESDParser(path);
            var mapping = parser.GetDataBlockEnumerator<AccelerationSample>()
                .OfType<ConstantFrequencySamplesDataBlock<AccelerationSample>>()
                .Select(block => new { Channel = block.ChannelId, block.Frequency, block.StartTime, block.SamplesCount })
                .ToList();

            // Count samples in blocks with numbers between blockFirst and blockLast (both exclusive)
            Func<int, int, ulong> countSkippedSamples = (blockFirst, blockLast) =>
            {
                var skippedSamples = mapping.Skip(blockFirst).Take(blockLast - blockFirst - 1)
                    .Aggregate(0UL, (samplesCount, block) => samplesCount + block.SamplesCount);
                return skippedSamples;
            };

            // Keep track of additional statistics for improved logging
            var previousBlockNumber = 0;
            var numberOfSampleInBlock = 0ul;
            var samplesInBlock = 0ul;
            var repeatCounter = 0ul;
            var prevSampleRate = 0u;
            var afterSampleRateChange = false;
            var previousRESDStreamStatus = RESDStreamStatus.BeforeStream;

            Action<ulong> resetCurrentBlockStats = (samplesCnt) =>
            {
                samplesInBlock = samplesCnt;
                numberOfSampleInBlock = 0;
                repeatCounter = 0;
            };

            // We keep the number of the last block used so far so that we can skip already used blocks on each event
            var blockNumber = 0;
            var init = true;
            Action<uint> findAndActivate = null;
            findAndActivate = (sampleRate) =>
            {
                if(sampleRate != prevSampleRate)
                {
                    afterSampleRateChange = true;
                }
                else if(afterSampleRateChange)
                {
                    if(fifoModeSelection.Value == FIFOModeSelection.FIFOMode)
                    {
                        // FIFO mode entered after changing sample rate. Repeat the previous block to synchronize with FIFO operation.
                        blockNumber = previousBlockNumber;
                    }
                    afterSampleRateChange = false;
                }
                previousBlockNumber = blockNumber;
                var currentEntry = mapping
                    .Select((item, i) => new { Item = item, Index = i })
                    .Skip(blockNumber)
                    .FirstOrDefault(o => o.Item.Frequency == sampleRate);

                if(currentEntry == null)
                {
                    // No more blocks at this sample rate
                    this.Log(LogLevel.Debug, "No more blocks for the sample rate {0}Hz in the RESD file", sampleRate);
                    feederThread?.Stop();
                    feederThread = null;
                    resdStream?.Dispose();
                    // If there are no more blocks, even without taking the sample rate
                    // into account, then we can unregister this sample rate change handler
                    if(blockNumber == mapping.Count)
                    {
                        this.Log(LogLevel.Debug, "No more blocks in the RESD file");
                        SampleRateChanged -= findAndActivate;
                        FIFOModeEntered -= findAndActivate;
                    }
                    return;
                }

                blockNumber = currentEntry.Index + 1;
                var samplesSkippedInLastBlock = previousRESDStreamStatus == RESDStreamStatus.OK ? samplesInBlock - numberOfSampleInBlock : 0;
                this.Log(LogLevel.Noisy, "Skipped {0} blocks while moving from the RESD block with the number {1} to {2}. Skipped {3} samples from the last active block and {4} samples in total",
                    blockNumber - previousBlockNumber - 1, previousBlockNumber, blockNumber, samplesSkippedInLastBlock, samplesSkippedInLastBlock + countSkippedSamples(previousBlockNumber, blockNumber));
                var block = currentEntry.Item;

                feederThread?.Stop();
                resdStream?.Dispose();

                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }

                // Offset the start time so that we start reading the block from the beginning even if
                // the sample rate was switched at a time different from the exact start time of the block
                resdStream = this.CreateRESDStream<AccelerationSample>(
                    path,
                    block.Channel,
                    RESDStreamSampleOffset.Specified,
                    (long)machine.ClockSource.CurrentValue.TotalMicroseconds * -1000L + (long)block.StartTime - (init ? (long)startTime : 0),
                    b => (b as ConstantFrequencySamplesDataBlock<AccelerationSample>)?.Frequency == sampleRate
                );
                resdStream.Owner = null; // turn off verbose internal RESD logging and use custom logs
                accelerationFifo.KeepFifoOnReset = false;
                resetCurrentBlockStats(block.SamplesCount);
                previousRESDStreamStatus = RESDStreamStatus.BeforeStream;

                // We start the thread with shouldStop: false so that it will keep feeding the last
                // sample into the FIFO at the specified frequency until it's stopped after a sample
                // rate switch
                AccelerationSample previousSample = null;
                // The same indexing as in 'ResdCommand' is used to log block's number
                this.Log(LogLevel.Noisy, "New RESD stream for the sample rate {0}Hz with the first block with the number {1} delayed by {2}ns", sampleRate, blockNumber, init ? (long)startTime : 0);
                feederThread = resdStream.StartSampleFeedThread(this, sampleRate, (sample, ts, status) =>
                {
                    switch(status)
                    {
                        case RESDStreamStatus.OK:
                        {
                            if(blockNumber < resdStream.CurrentBlockNumber)
                            {
                                // We received the first sample from the next block in the RESD stream 
                                // as a result of internal RESD operation, not due to a triggered event
                                if(fifoModeSelection.Value == FIFOModeSelection.FIFOMode)
                                {
                                    // In FIFO mode we don't allow automatic passtrough to the next block,
                                    // because it would interfere with event driven FIFOModeEntered logic.
                                    goto case RESDStreamStatus.BeforeStream;
                                }
                                resetCurrentBlockStats(resdStream.CurrentBlock.SamplesCount);
                                blockNumber = (int)resdStream.CurrentBlockNumber;
                                this.Log(LogLevel.Noisy, "Beginning of the new RESD block ({0}Hz) with the number {1}", sampleRate, blockNumber);
                            }

                            numberOfSampleInBlock++;
                            previousSample = sample;
                            this.Log(LogLevel.Noisy, "Fed current sample from the RESD block ({0}Hz) with the number {1}: {2} at {3} ({4}/{5})", sampleRate, blockNumber, previousSample, ts, numberOfSampleInBlock, samplesInBlock);
                            break;
                        }
                        case RESDStreamStatus.BeforeStream:
                        {
                            repeatCounter++;
                            this.Log(LogLevel.Noisy, "Repeated the last sample from the previous RESD block ({0}Hz) with the number {1}: {2} at {3} - {4} times", sampleRate, blockNumber, previousSample, ts, repeatCounter);
                            break;
                        }
                        case RESDStreamStatus.AfterStream:
                        {
                            repeatCounter++;
                            this.Log(LogLevel.Noisy, "No more samples in the RESD stream for the sample rate {0}Hz. Repeated the last sample from the previous RESD block with the number {1}: {2} at {3} - {4} times", sampleRate, blockNumber, previousSample, ts, repeatCounter);
                            break;
                        }
                    }
                    previousRESDStreamStatus = status;

                    if(previousSample != null)
                    {
                        HandleAccelerationSample(previousSample, ts);
                    }
                }, startTime: init ? startTime : 0, shouldStop: false);

                init = false;
                prevSampleRate = sampleRate;
            };

            findAndActivate(SampleRate);
            SampleRateChanged += findAndActivate;
            FIFOModeEntered += findAndActivate;
        }

        private void FeedAccelerationSampleInner(decimal x, decimal y, decimal z, bool keepOnReset, uint repeat = 1)
        {
            if(keepOnReset)
            {
                // this is a simplified implementation
                // that assumes that the `keepOnReset`
                // status applies to all samples;
                // might not work when mixing feeding
                // samples from RESD and manually
                accelerationFifo.KeepFifoOnReset = true;
            }

            var sample = new Vector3DSample(x, y, z);

            if(fifoModeSelection.Value == FIFOModeSelection.Bypass && !keepOnReset)
            {
                CurrentSample = sample;
            }
            else
            {
                for(var i = 0; i < repeat; i++)
                {
                    accelerationFifo.FeedSample(sample);
                }
            }

            UpdateInterrupts();
        }

        private void LoadNextSample()
        {
            if(fifoModeSelection.Value != FIFOModeSelection.Bypass || accelerationFifo.KeepFifoOnReset)
            {
                accelerationFifo.TryDequeueNewSample();
                CurrentSample = accelerationFifo.Sample;
            }
            this.Log(LogLevel.Noisy, "Acquired sample {0} during {1} operation", CurrentSample, fifoModeSelection.Value);
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.TemperatureOutLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Temperature output register in 12-bit resolution (OUT_T_L)",
                    valueProviderCallback: _ => (byte)(TwoComplementSignConvert(ReportedTemperature * TemperatureLsbsPerDegree) << 4));

            Registers.TemperatureOutHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Temperature output register in 12-bit resolution (OUT_T_H)",
                    valueProviderCallback: _ => (byte)(TwoComplementSignConvert(ReportedTemperature * TemperatureLsbsPerDegree) >> 4));

            Registers.WhoAmI.Define(this, 0x44);

            Registers.Control1.Define(this)
                .WithEnumField(0, 2, out lowPowerModeSelection, name: "Low-power mode selection (LP_MODE)")
                .WithEnumField(2, 2, out modeSelection, name: "Mode selection (MODE)")
                .WithEnumField(4, 4, out outDataRate, writeCallback: (_, __) =>
                    {
                        switch(outDataRate.Value)
                        {
                            case DataRateConfig.HighPerformanceLowPower1_6Hz:
                                SampleRate = 2;
                                break;
                            case DataRateConfig.HighPerformanceLowPower12_5Hz:
                                SampleRate = 13;
                                break;
                            case DataRateConfig.HighPerformanceLowPower25Hz:
                                SampleRate = 25;
                                break;
                            case DataRateConfig.HighPerformanceLowPower50Hz:
                                SampleRate = 50;
                                break;
                            case DataRateConfig.HighPerformanceLowPower100Hz:
                                SampleRate = 100;
                                break;
                            case DataRateConfig.HighPerformanceLowPower200Hz:
                                SampleRate = 200;
                                break;
                            case DataRateConfig.HighPerformanceLowPower400Hz:
                                SampleRate = 400;
                                break;
                            case DataRateConfig.HighPerformanceLowPower800Hz:
                                SampleRate = 800;
                                break;
                            case DataRateConfig.HighPerformanceLowPower1600Hz:
                                SampleRate = 1600;
                                break;
                            default:
                                SampleRate = 0;
                                break;
                        }
                    }, name: "Output data rate and mode selection (ODR)");

            Registers.Control2.Define(this, 0x4)
                .WithTaggedFlag("SPI serial interface mode selection (SIM)", 0)
                .WithTaggedFlag("Disable I2C communication protocol (I2C_DISABLE)", 1)
                .WithFlag(2, out autoIncrement, name: "Register address automatically incremented during multiple byte access with a serial interface (FF_ADD_INC)")
                .WithTaggedFlag("Block data update (BDU)", 3)
                .WithTaggedFlag("Disconnect CS pull-up (CS_PU_DISC)", 4)
                .WithReservedBits(5, 1)
                .WithFlag(6, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            Reset();
                        }
                    }, name: "Acts as reset for all control register (SOFT_RESET)")
                .WithTaggedFlag("Enables retrieving the correct trimming parameters from nonvolatile memory into registers (BOOT)", 7);

            Registers.Control3.Define(this)
                .WithTaggedFlag("Single data conversion on demand mode enable (SLP_MODE_1)", 0)
                .WithTaggedFlag("Single data conversion on demand mode selection (SLP_MODE_SEL)", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("Interrupt active high, low (HL_ACTIVE)", 3)
                .WithTaggedFlag("Latched Interrupt (LIR)", 4)
                .WithTaggedFlag("Push-pull/open-drain selection on interrupt pad (PP_OD)", 5)
                .WithEnumField(6, 2, out selfTestMode, name: "Self-test enable (ST)");

            Registers.Control4.Define(this)
                .WithFlag(0, out readyEnabledAcceleration[0], name: "Data-Ready is routed to INT1 pad (INT1_DRDY)")
                .WithFlag(1, out fifoThresholdEnabled[0], name: "FIFO threshold interrupt is routed to INT1 pad (INT1_FTH)")
                .WithFlag(2, out fifoFullEnabled[0], name: "FIFO full recognition is routed to INT1 pad (INT1_DIFF5)")
                .WithTaggedFlag("Double-tap recognition is routed to INT1 pad (INT1_TAP)", 3)
                .WithTaggedFlag("Free-fall recognition is routed to INT1 pad (INT1_FF)", 4)
                .WithTaggedFlag("Wakeup recognition is routed to INT1 pad (INT1_WU)", 5)
                .WithTaggedFlag("Single-tap recognition is routed to INT1 pad (INT1_SINGLE_TAP)", 6)
                .WithTaggedFlag("6D recognition is routed to INT1 pad (INT1_6D)", 7)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Control5.Define(this)
                .WithFlag(0, out readyEnabledAcceleration[1], name: "Data-ready is routed to INT2 pad (INT2_DRDY)")
                .WithFlag(1, out fifoThresholdEnabled[1], name: "FIFO threshold interrupt is routed to INT2 pad (INT2_FTH)")
                .WithFlag(2, out fifoFullEnabled[1], name: "FIFO full recognition is routed to INT2 pad (INT2_DIFF5)")
                .WithFlag(3, out fifoOverrunEnabled, name: "FIFO overrun interrupt is routed to INT2 pad (INT2_OVR)")
                .WithFlag(4, out readyEnabledTemperature, name: "Temperature data-ready is routed to INT2 (INT2_DRDY_T)")
                .WithTaggedFlag("Boot state routed to INT2 pad (INT2_BOOT)", 5)
                .WithTaggedFlag("Sleep change status routed to INT2 pad (INT2_SLEEP_CHG)", 6)
                .WithTaggedFlag("Enable routing of SLEEP_STATE on INT2 pad (INT2_SLEEP_STATE)", 7);

            Registers.Control6.Define(this)
                .WithReservedBits(0, 2)
                .WithTaggedFlag("Low-noise configuration (LOW_NOISE)", 2)
                .WithTaggedFlag("Filtered data type selection (FDS)", 3)
                .WithEnumField(4, 2, out fullScale, writeCallback: (_, __) => SetScaleDivider(), name: "Full-scale selection (FS)")
                .WithTag("Bandwidth selection (BW_FILT1)", 6, 2);

            Registers.TemperatureOut.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Temperature output register in 8-bit resolution (OUT_T)",
                    valueProviderCallback: _ => (byte)TwoComplementSignConvert(ReportedTemperature));

            Registers.Status.Define(this)
                .WithFlag(0, valueProviderCallback: _ => outDataRate.Value != DataRateConfig.PowerDown, name: "Data-ready status (DRDY)")
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 1)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 2)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 3)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 4)
                .WithTaggedFlag("Sleep event status (SLEEP_STATE)", 5)
                .WithTaggedFlag("Wakeup event detection status (WU_IA)", 6)
                .WithFlag(7, FieldMode.Read, name: "FIFO threshold status flag (FIFO_THS)",
                    valueProviderCallback: _ => FifoThresholdReached);

            Registers.DataOutXLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X-axis LSB output register (OUT_X_L)",
                    valueProviderCallback: _ =>
                    {
                        LoadNextSample();
                        return Convert(ReportedAccelerationX, upperByte: false);
                    });

            Registers.DataOutXHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "X-axis MSB output register (OUT_X_H)",
                    valueProviderCallback: _ => Convert(ReportedAccelerationX, upperByte: true));

            Registers.DataOutYLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y-axis LSB output register (OUT_Y_L)",
                    valueProviderCallback: _ => Convert(ReportedAccelerationY, upperByte: false));

            Registers.DataOutYHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Y-axis MSB output register (OUT_Y_H)",
                    valueProviderCallback: _ => Convert(ReportedAccelerationY, upperByte: true));

            Registers.DataOutZLow.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z-axis LSB output register (OUT_Z_L)",
                    valueProviderCallback: _ => Convert(ReportedAccelerationZ, upperByte: false));

            Registers.DataOutZHigh.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Z-axis MSB output register (OUT_Z_H)",
                    valueProviderCallback: _ => Convert(ReportedAccelerationZ, upperByte: true));

            Registers.FifoControl.Define(this)
                .WithValueField(0, 5, out fifoThreshold, name: "FIFO threshold level setting (FTH)")
                .WithEnumField(5, 3, out fifoModeSelection, writeCallback: (_, newMode) =>
                    {
                        if(newMode == FIFOModeSelection.Bypass)
                        {
                            accelerationFifo.Reset();
                        }
                    }, changeCallback: (oldMode, newMode) =>
                    {
                        if(oldMode == FIFOModeSelection.Bypass && newMode == FIFOModeSelection.FIFOMode)
                        {
                            FIFOModeEntered?.Invoke(SampleRate);
                        }
                    }, name: "FIFO mode selection bits (FMode)");

            Registers.FifoSamples.Define(this)
                .WithValueField(0, 6, FieldMode.Read, name: "Number of unread samples stored in FIFO (DIFF)",
                    valueProviderCallback: _ => accelerationFifo.Disabled ? 0 : accelerationFifo.SamplesCount)
                .WithFlag(6, FieldMode.Read, name: "FIFO overrun status (FIFO_OVR)",
                    valueProviderCallback: _ => FifoOverrunOccurred)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => FifoThresholdReached,
                    name: "FIFO threshold status flag (FIFO_FTH)");

            Registers.TapThreshholdX.Define(this)
                .WithTag("Threshold for tap recognition on X direction (TAP_THSX)", 0, 5)
                .WithTag("Thresholds for 4D/6D function (6D_THS)", 5, 2)
                .WithTag("4D detection portrait/landscape position enable (4D_EN)", 7, 1);

            Registers.TapThreshholdY.Define(this)
                .WithTag("Threshold for tap recognition on Y direction (TAP_THSY)", 0, 5)
                .WithTag("Selection of priority axis for tap detection (TAP_PRIOR)", 5, 3);

            Registers.TapThreshholdZ.Define(this)
                .WithTag("Threshold for tap recognition on Z direction (TAP_THSZ)", 0, 5)
                .WithTag("Enables Z direction in tap recognition (TAP_Z_EN)", 5, 1)
                .WithTag("Enables Y direction in tap recognition (TAP_Y_EN)", 6, 1)
                .WithTag("Enables X direction in tap recognition (TAP_X_EN)", 7, 1);

            Registers.InterruptDuration.Define(this)
                .WithTag("Maximum duration of over-threshold event (SHOCK)", 0, 2)
                .WithTag("Expected quiet time after a tap detection (QUIET)", 2, 2)
                .WithTag("Duration of maximum time gap for double-tap recognition (LATENCY)", 4, 4);

            Registers.WakeupThreshhold.Define(this)
                .WithTag("Wakeup threshold  (WK_THS)", 0, 6)
                .WithTag("Sleep (inactivity) enable (SLEEP_ON)", 6, 1)
                .WithTag("Enable single/double-tap event (SINGLE_DOUBLE_TAP)", 7, 1);

            Registers.WakeupAndSleepDuration.Define(this)
                .WithTag("Duration to go in sleep mode (SLEEP_DUR)", 0, 4)
                .WithTag("Enable stationary detection / motion detection (STATIONARY)", 4, 1)
                .WithTag("Wakeup duration (WAKE_DUR)", 5, 2)
                .WithTag("Free-fall duration (FF_DUR5)", 7, 1);

            Registers.FreeFall.Define(this)
                .WithTag("Free-fall threshold (FF_THS)", 0, 3)
                .WithTag("Free-fall duration (FF_DUR)", 3, 5);

            Registers.StatusEventDetection.Define(this)
                .WithFlag(0, valueProviderCallback: _ => outDataRate.Value != DataRateConfig.PowerDown, name: "Data-ready status (DRDY)")
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 1)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 2)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 3)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 4)
                .WithTaggedFlag("Sleep event status (SLEEP_STATE_IA)", 5)
                .WithFlag(6, valueProviderCallback: _ => outDataRate.Value != DataRateConfig.PowerDown, name: "Temperature status (DRDY_T)")
                .WithFlag(7, name: "FIFO overrun status flag (OVR)",
                    valueProviderCallback: _ => FifoOverrunOccurred);

            Registers.WakeupSource.Define(this)
                .WithTaggedFlag("Wakeup event detection status on Z-axis (Z_WU)", 0)
                .WithTaggedFlag("Wakeup event detection status on Y-axis (Y_WU)", 1)
                .WithTaggedFlag("Wakeup event detection status on X-axis (X_WU)", 2)
                .WithTaggedFlag("Wakeup event detection status (WU_IA)", 3)
                .WithTaggedFlag("Sleep event status (SLEEP_STATE_IA)", 4)
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 5)
                .WithReservedBits(6, 2);

            Registers.TapSource.Define(this)
                .WithTaggedFlag("Tap event detection status on Z-axis (Z_TAP)", 0)
                .WithTaggedFlag("Tap event detection status on Y-axis (Y_TAP)", 1)
                .WithTaggedFlag("Tap event detection status on X-axis (X_TAP)", 2)
                .WithTaggedFlag("Sign of acceleration detected by tap event (TAP_SIGN)", 3)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 4)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 5)
                .WithTaggedFlag("Tap event status (TAP_IA)", 6)
                .WithReservedBits(7, 1);

            Registers.SourceOf6D.Define(this)
                .WithTaggedFlag("XL over threshold (XL)", 0)
                .WithTaggedFlag("XH over threshold (XH)", 1)
                .WithTaggedFlag("YL over threshold (YL)", 2)
                .WithTaggedFlag("YH over threshold (YH)", 3)
                .WithTaggedFlag("ZL over threshold (ZL)", 4)
                .WithTaggedFlag("ZH over threshold (ZL)", 5)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 6)
                .WithReservedBits(7, 1);

            Registers.InterruptReset.Define(this)
                .WithTaggedFlag("Free-fall event detection status (FF_IA)", 0)
                .WithTaggedFlag("Wakeup event detection status (WU_IA)", 1)
                .WithTaggedFlag("Single-tap event status (SINGLE_TAP)", 2)
                .WithTaggedFlag("Double-tap event status (DOUBLE_TAP)", 3)
                .WithTaggedFlag("Source of change in position portrait/landscape/face-up/face-down (6D_IA)", 4)
                .WithTaggedFlag("Sleep change status (SLEEP_CHANGE_IA)", 5)
                .WithReservedBits(6, 2);

            Registers.UserOffsetX.Define(this)
                .WithTag("Two's complement user offset value on X-axis data, used for wakeup function (X_OFS_USR)", 0, 8);

            Registers.UserOffsetY.Define(this)
                .WithTag("Two's complement user offset value on Y-axis data, used for wakeup function (Y_OFS_USR)", 0, 8);

            Registers.UserOffsetZ.Define(this)
                .WithTag("Two's complement user offset value on Z-axis data, used for wakeup function (Z_OFS_USR)", 0, 8);

            Registers.Control7.Define(this)
                .WithTaggedFlag("Low pass filtered data sent to 6D interrupt function (LPASS_ON6D)", 0)
                .WithTaggedFlag("High-pass filter reference mode enable (HP_REF_MODE)", 1)
                .WithTaggedFlag("Selects the weight of the user offset words (USR_OFF_W)", 2)
                .WithTaggedFlag("Enable application of user offset value on XL data for wakeup function only (USR_OFF_ON_WU)", 3)
                .WithTaggedFlag("Enable application of user offset value on XL output data registers (USR_OFF_ON_OUT)", 4)
                .WithFlag(5, out eventInterruptEnable, name: "Enable interrupts (INTERRUPTS_ENABLE)")
                .WithTaggedFlag("Signal routing (INT2_ON_INT1)", 6)
                .WithTaggedFlag("Switches between latched and pulsed mode for data ready interrupt (DRDY_PULSED)", 7)
                .WithChangeCallback((_,__) => UpdateInterrupts());
        }

        private void RegistersAutoIncrement()
        {
            if(regAddress >= Registers.DataOutXLow && regAddress < Registers.DataOutZHigh
                || regAddress == Registers.TemperatureOutLow)
            {
                regAddress = (Registers)((int)regAddress + 1);
                this.Log(LogLevel.Noisy, "Auto-incrementing to the next register 0x{0:X} - {0}", regAddress);
            }
            else if((fifoModeSelection.Value == FIFOModeSelection.FIFOMode) && (regAddress == Registers.DataOutZHigh))
            {
                regAddress = Registers.DataOutXLow;
            }
        }

        private void UpdateInterrupts()
        {
            if(outDataRate.Value == DataRateConfig.PowerDown)
            {
                Interrupt1.Unset();
                Interrupt2.Unset();
                return;
            }

            var int1Status =
                readyEnabledAcceleration[0].Value ||
                fifoThresholdEnabled[0].Value && FifoThresholdReached ||
                fifoFullEnabled[0].Value && FifoFull;
            var int2Status =
                readyEnabledAcceleration[1].Value ||
                fifoThresholdEnabled[1].Value && FifoThresholdReached ||
                fifoOverrunEnabled.Value && FifoOverrunOccurred ||
                fifoFullEnabled[1].Value && FifoFull;

            this.Log(LogLevel.Noisy, "Setting interrupts: INT1 = {0}, INT2 = {1}", int1Status, int2Status);
            Interrupt1.Set(int1Status);
            Interrupt2.Set(int2Status);
        }

        private bool IsTemperatureOutOfRange(decimal temperature)
        {
            // This range protects from the overflow of the short variables in the 'Convert' function.
            if (temperature < MinTemperature || temperature > MaxTemperature)
            {
                this.Log(LogLevel.Warning, "Temperature {0} is out of range, use value from the range <{1:F2};{2:F2}>", temperature, MinTemperature, MaxTemperature);
                return true;
            }
            return false;
        }

        private bool IsAccelerationOutOfRange(decimal acceleration)
        {
            // This range protects from the overflow of the short variables in the 'Convert' function.
            if (acceleration < MinAcceleration || acceleration > MaxAcceleration)
            {
                this.Log(LogLevel.Warning, "Acceleration {0} is out of range, use value from the range <{1:F2};{2:F2}>", acceleration, MinAcceleration, MaxAcceleration);
                return true;
            }
            return false;
        }

        private byte Convert(decimal value, bool upperByte)
        {
            byte result = 0;
            if(outDataRate.Value == DataRateConfig.PowerDown)
            {
                return result;
            }

            var minValue = MinValue14Bit;
            var maxValue = MaxValue14Bit;
            var shift = 2;
            // Divide the divider by 4 because the divider for full scale = 2 g
            // is 4 and the base sensitivity is given for this scale setting
            var sensitivity = BaseSensitivity * (scaleDivider / 4);

            if(modeSelection.Value == ModeSelection.HighPerformance)
            {
                this.Log(LogLevel.Noisy, "High performance (14-bit resolution) mode is selected.");
            }
            else if((modeSelection.Value == ModeSelection.LowPower) && (lowPowerModeSelection.Value != LowPowerModeSelection.LowPowerMode1_12bitResolution))
            {
                this.Log(LogLevel.Noisy, "Low power (14-bit resolution) mode is selected.");
            }
            else if((modeSelection.Value == ModeSelection.LowPower) && (lowPowerModeSelection.Value == LowPowerModeSelection.LowPowerMode1_12bitResolution))
            {
                this.Log(LogLevel.Noisy, "Low power (12-bit resolution) mode is selected.");
                minValue = MinValue12Bit;
                maxValue = MaxValue12Bit;
                shift = 4;
                sensitivity *= 4;
            }
            else
            {
                this.Log(LogLevel.Noisy, "Other conversion mode selected.");
            }

            var valueAsInt = ((int)(value * 1000 / sensitivity)).Clamp(minValue, maxValue);
            var valueAsUshort = (ushort)(valueAsInt << shift); // left-align
            this.Log(LogLevel.Noisy, "Conversion done with sensitivity: {0:F3}, result: 0x{1:X4}", sensitivity, valueAsUshort);

            if(upperByte)
            {
                return (byte)(valueAsUshort >> 8);
            }
            result = (byte)(valueAsUshort);
            UpdateInterrupts();

            return result;
        }

        private void InternalWrite(byte[] data)
        {
            for(var i = 0; i < data.Length; i++)
            {
                this.Log(LogLevel.Noisy, "Writing 0x{0:X} to register {1} (0x{1:X})", data[i], regAddress);
                RegistersCollection.Write((byte)regAddress, data[i]);
                if(autoIncrement.Value)
                {
                    RegistersAutoIncrement();
                }
            }
        }

        private ushort TwoComplementSignConvert(decimal temp)
        {
            var tempAsUshort = Decimal.ToUInt16(Math.Abs(temp));
            if(temp < 0)
            {
                var twoComplementTemp = (ushort)(~tempAsUshort + 1);
                return twoComplementTemp;
            }
            return tempAsUshort;
        }

        private void SetScaleDivider()
        {
            switch(fullScale.Value)
            {
                case FullScaleSelect.FullScale4g:
                    scaleDivider = 8;
                    break;
                case FullScaleSelect.FullScale8g:
                    scaleDivider = 16;
                    break;
                case FullScaleSelect.FullScale16g:
                    scaleDivider = 32;
                    break;
                default:
                    scaleDivider = 4;
                    break;
            }
        }

        public Vector3DSample DefaultSample { get; } = new Vector3DSample();
        private Vector3DSample CurrentSample { get; set; } = new Vector3DSample();
        private decimal SelfTestAccelerationOffset =>
            SelfTestAcceleration * (selfTestMode.Value == SelfTestMode.PositiveSign ? 1 : selfTestMode.Value == SelfTestMode.NegativeSign ? -1 : 0);
        private decimal ReportedAccelerationX => AccelerationX + SelfTestAccelerationOffset;
        private decimal ReportedAccelerationY => AccelerationY + SelfTestAccelerationOffset;
        private decimal ReportedAccelerationZ => AccelerationZ + SelfTestAccelerationOffset;

        private decimal ReportedTemperature => Temperature - TemperatureBias;

        private bool FifoThresholdReached => fifoThreshold.Value != 0 && accelerationFifo.SamplesCount >= fifoThreshold.Value;
        private bool FifoFull => accelerationFifo.Full;
        private bool FifoOverrunOccurred => accelerationFifo.OverrunOccurred;

        private IFlagRegisterField autoIncrement;
        private IFlagRegisterField[] readyEnabledAcceleration;
        private IFlagRegisterField[] fifoThresholdEnabled;
        private IFlagRegisterField[] fifoFullEnabled;
        // Interrupt on overrun is only avaliable on INT2
        private IFlagRegisterField fifoOverrunEnabled;
        private IFlagRegisterField readyEnabledTemperature;
        // This flag controls whether 6D, tap, fall, etc. interrupts are enabled and is not a global
        // flag for all interrupts (FIFO and others)
        private IFlagRegisterField eventInterruptEnable;
        private IValueRegisterField fifoThreshold;
        private IEnumRegisterField<LowPowerModeSelection> lowPowerModeSelection;
        private IEnumRegisterField<DataRateConfig> outDataRate;
        private IEnumRegisterField<ModeSelection> modeSelection;
        private IEnumRegisterField<FIFOModeSelection> fifoModeSelection;
        private IEnumRegisterField<FullScaleSelect> fullScale;
        private IEnumRegisterField<SelfTestMode> selfTestMode;
        private uint sampleRate;

        private Registers regAddress;
        private State state;
        private int scaleDivider;

        private IManagedThread feederThread;
        private RESDStream<AccelerationSample> resdStream;
        private bool isAfterStream;

        private event Action<uint> SampleRateChanged;
        // This event is used in MultiFrequency RESD to precisely match RESD behavior with FIFO operation
        private event Action<uint> FIFOModeEntered;

        private decimal temperature;

        private readonly LIS2DW12_FIFO accelerationFifo;
        private readonly IMachine machine;

        private const decimal MinTemperature = -40.0m;
        // The temperature register has the value 0 at this temperature
        private const decimal TemperatureBias = 25.0m;
        private const decimal MaxTemperature = 85.0m;
        private const decimal MinAcceleration = -16m;
        private const decimal MaxAcceleration = 16m;
        private const decimal SelfTestAcceleration = 1m;
        // Calculated as floor(1000 / 0xfff, 3), 1000 mg / 12-bit max value, used as a base
        // to calculate the sensitivity for other ranges/resolutions, multiplying it by 2^n
        // to match the tables in the datasheet, appnote, and drivers
        private const decimal BaseSensitivity = 0.244m; // mg / digit
        private const int MaxFifoSize = 32;
        private const int MinValue14Bit = -0x2000;
        private const int MaxValue14Bit = 0x1FFF;
        private const int MinValue12Bit = -0x0800;
        private const int MaxValue12Bit = 0x07FF;
        private const int TemperatureLsbsPerDegree = 16;

        public enum RESDType
        {
            Normal,
            MultiFrequency,
        }

        private class LIS2DW12_FIFO
        {
            public LIS2DW12_FIFO(LIS2DW12 owner, string name, uint capacity)
            {
                Capacity = capacity;
                this.name = name;
                this.owner = owner;
                this.locker = new object();
                this.queue = new Queue<Vector3DSample>();
            }

            public void FeedAccelerationSample(decimal x, decimal y, decimal z)
            {
                var sample = new Vector3DSample(x, y, z);
                FeedSample(sample);
            }

            public void FeedSample(Vector3DSample sample)
            {
                lock(locker)
                {
                    latestSample = sample;

                    if(KeepFifoOnReset)
                    {
                        queue.Enqueue(sample);
                        return;
                    }

                    if(Mode == FIFOModeSelection.FIFOMode && Full)
                    {
                        return;
                    }
                    else if(Mode == FIFOModeSelection.Continuous && Full)
                    {
                        if(!OverrunOccurred)
                        {
                            owner.Log(LogLevel.Debug, "{0}: Overrun", name);
                            OverrunOccurred = true;
                            OnOverrun?.Invoke();
                        }

                        owner.Log(LogLevel.Noisy, "{0}: Fifo filled up. Dumping the oldest sample.", name);
                        queue.TryDequeue<Vector3DSample>(out _);
                    }
                    owner.Log(LogLevel.Noisy, "Enqueued sample {0} at index {1}", sample, queue.Count);
                    queue.Enqueue(sample);
                }
            }

            // Old, deprecated API. Added because this peripheral already included a public FeedSamplesFromFile method.
            public void FeedSamplesFromFile(string path)
            {
                try
                {
                    using(var reader = File.OpenText(path))
                    {
                        string line;
                        while((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();

                            if(line.StartsWith("#"))
                            {
                                continue;
                            }

                            var numbers = line.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();
                            var sample = new Vector3DSample();
                            if(!sample.TryLoad(numbers))
                            {
                                sample = null;
                            }
                            FeedSample(sample);
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
            }

            public void Reset()
            {
                owner.Log(LogLevel.Debug, "Resetting FIFO");
                OverrunOccurred = false;

                if(KeepFifoOnReset)
                {
                    owner.Log(LogLevel.Debug, "Keeping existing FIFO content");
                    return;
                }

                queue.Clear();
                latestSample = null;
            }

            public bool TryDequeueNewSample()
            {
                lock(locker)
                {
                    if(CheckEnabled() && queue.TryDequeue(out var sample))
                    {
                        owner.Log(LogLevel.Noisy, "New sample dequeued: {0}", sample);
                        latestSample = sample;
                        return true;
                    }
                }
                // If we're feeding from a file, the sensor might have been polled between
                // two samples - in this case, the earlier of these should be returned. Otherwise,
                // clearing the FIFO means we ran out of data and so we should go to the default
                // sample.
                if(!Disabled)
                {
                    latestSample = null;
                }
                owner.Log(LogLevel.Noisy, "Dequeueing new sample failed.");
                return false;
            }

            public bool KeepFifoOnReset { get; set; }

            public uint SamplesCount => (uint)Math.Min(queue.Count, Capacity);
            public bool Disabled => Mode == FIFOModeSelection.Bypass && !KeepFifoOnReset;
            public bool Empty => SamplesCount == 0;
            public bool Full => SamplesCount >= Capacity;

            public FIFOModeSelection Mode => owner.fifoModeSelection.Value;

            public bool OverrunOccurred { get; private set; }
            public Vector3DSample Sample => latestSample ?? owner.DefaultSample;

            public event Action OnOverrun;

            private bool CheckEnabled()
            {
                if(Disabled)
                {
                    owner.Log(LogLevel.Debug, "Sample unavailable -- FIFO disabled.");
                    return false;
                }
                return true;
            }

            private Vector3DSample latestSample;

            private readonly object locker;
            private readonly Queue<Vector3DSample> queue;
            private readonly string name;
            private readonly LIS2DW12 owner;
            private readonly uint Capacity;
        }

        private enum State
        {
            WaitingForRegister,
            WaitingForData
        }

        private enum FullScaleSelect : byte
        {
            FullScale2g = 0x00,
            FullScale4g = 0x01,
            FullScale8g = 0x02,
            FullScale16g = 0x03,
        }

        private enum DataRateConfig : byte
        {
            PowerDown = 0x00,
            HighPerformanceLowPower1_6Hz = 0x01,
            HighPerformanceLowPower12_5Hz = 0x02,
            HighPerformanceLowPower25Hz = 0x03,
            HighPerformanceLowPower50Hz = 0x04,
            HighPerformanceLowPower100Hz = 0x05,
            HighPerformanceLowPower200Hz = 0x06,
            HighPerformanceLowPower400Hz = 0x07,
            HighPerformanceLowPower800Hz = 0x08,
            HighPerformanceLowPower1600Hz = 0x09,
        }

        private enum ModeSelection : byte
        {
            LowPower = 0x00,
            HighPerformance = 0x01,
            SingleDataConversionOnDemand = 0x02,
            Reserved = 0x03
        }

        private enum FIFOModeSelection : byte
        {
            Bypass = 0x00,
            FIFOMode = 0x01,
            // Reserved: 0x02
            ContinuousToFIFO = 0x03,
            BypassToContinuous = 0x04,
            // Reserved: 0x05
            Continuous = 0x06,
            // Reserved: 0x07
        }

        private enum LowPowerModeSelection : byte
        {
            LowPowerMode1_12bitResolution = 0x00,
            LowPowerMode2_14bitResolution = 0x01,
            LowPowerMode3_14bitResolution = 0x02,
            LowPowerMode4_14bitResolution = 0x03
        }

        private enum SelfTestMode : byte
        {
            Disabled = 0x00,
            PositiveSign = 0x01,
            NegativeSign = 0x02,
            // Reserved: 0x03
        }

        private enum Registers : byte
        {
            // Reserved: 0x0 - 0xC
            TemperatureOutLow = 0xD,
            TemperatureOutHigh = 0xE,
            WhoAmI = 0x0F,
            // Reserved: 0x10 - 0x1F
            Control1 = 0x20,
            Control2 = 0x21,
            Control3 = 0x22,
            Control4 = 0x23,
            Control5 = 0x24,
            Control6 = 0x25,
            TemperatureOut = 0x26,
            Status = 0x27,
            DataOutXLow = 0x28,
            DataOutXHigh = 0x29,
            DataOutYLow = 0x2A,
            DataOutYHigh = 0x2B,
            DataOutZLow = 0x2C,
            DataOutZHigh = 0x2D,
            FifoControl = 0x2E,
            FifoSamples = 0x2F,
            TapThreshholdX = 0x30,
            TapThreshholdY = 0x31,
            TapThreshholdZ = 0x32,
            InterruptDuration = 0x33,
            WakeupThreshhold = 0x34,
            WakeupAndSleepDuration = 0x35,
            FreeFall = 0x36,
            StatusEventDetection = 0x37,
            WakeupSource = 0x38,
            TapSource = 0x39,
            SourceOf6D = 0x3A,
            InterruptReset = 0x3B,
            UserOffsetX = 0x3C,
            UserOffsetY = 0x3D,
            UserOffsetZ = 0x3E,
            Control7 = 0x3F
        }
    }
}
