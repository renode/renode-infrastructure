//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sound
{
    // During emulation there is no need to simulate the decimation filter which facilitates PDM to PCM conversion.
    // Samples in the PCM format are loaded from the input file and represent digital data after decimation.
    // Hardware voice activity detector (HWVAD) is not emulated, but it's possible to manually trigger voice activity event.
    // Clock root frequency is currently hardcoded, but it should be implied by a clock tree configuration.
    public partial class IMXRT700_MICFIL : BasicDoubleWordPeripheral, IKnownSize
    {
        public IMXRT700_MICFIL(IMachine machine, uint clockRootFrequencyHz = 24576000) : base(machine)
        {
            this.clockRootFrequencyHz = clockRootFrequencyHz;
            IRQ = new GPIO();
            HWVAD = new GPIO();
            DmaRequest = new GPIO();
            for(byte i = 0; i < NumberOfDataChannels; i++)
            {
                dataChannels[i] = new DataChannel(i);
            }
            DefineRegisters();
        }

        // Order of data channels is important and must match the order of audio channels in the file.
        // As an example assume the input file represents stereo sound in the PCM format (left channel/right channel/left channel/right channel),
        // dataChannels=[0, 1] would mean samples for left audio channel go into data channel 0 and samples for right audio channel go into data channel 1.
        // dataChannels=[1, 0] would mean samples for left audio channel go into data channel 1 and samples for right audio channel go into data channel 0.
        public void LoadSamplesForDataChannels(string path, bool littleEndian, uint bitDepth, uint sampleRateHz, byte[] dataChannels)
        {
            if(bitDepth != 24 && bitDepth != 32)
            {
                throw new RecoverableException("Bit depth must be 24 bits for PCM samples or 32 bits for raw PDM samples");
            }

            if(dataChannelsLoadIndex >= NumberOfDataChannels)
            {
                throw new RecoverableException("Samples for all channels are already loaded");
            }

            var uniqueTest = new HashSet<byte>();
            foreach(var dataChannel in dataChannels)
            {
                if(dataChannel >= NumberOfDataChannels)
                {
                    throw new RecoverableException($"Specified invalid channel {dataChannel} - only channels 0-7 are supported");
                }
                if(dataChannelsWithSamplesInOrder.Take(dataChannelsLoadIndex).Contains(dataChannel))
                {
                    throw new RecoverableException($"Samples for channel {dataChannel} were already loaded");
                }
                if(!uniqueTest.Add(dataChannel))
                {
                    throw new RecoverableException($"Specified channel list contains duplicated number {dataChannel} - channels must be unique");
                }
            }

            var numberOfChannels = dataChannels.Length;
            var decoder = new PCMDecoder(bitDepth, sampleRateHz, (uint)numberOfChannels, concatenatedChannels: false, this);
            decoder.LoadFile(path, littleEndian);

            foreach(var dataChannel in dataChannels)
            {
                dataChannelsWithSamplesInOrder[dataChannelsLoadIndex] = dataChannel;
                pcmDecoders[dataChannelsLoadIndex] = decoder;
                dataChannelsLoadIndex++;
            }
        }

        public void SetVoiceActivityEvent(bool detected)
        {
            if(!hwvadEnable.Value)
            {
                this.WarningLog("Hardware voice activity detector is not enabled");
            }

            hwvadInterruptFlag.Value = detected;

            var state = hwvadInterruptEnable.Value && hwvadInterruptFlag.Value;
            HWVAD.Set(state);
        }

        public string InputFile { get; set; }

        public GPIO IRQ { get; }

        public GPIO HWVAD { get; }

        public GPIO DmaRequest { get; }

        public long Size => 0x1000;

        private void StartThread()
        {
            if(!TryCalculateOutputDataRate(out var sampleRate))
            {
                this.WarningLog("Unable to calculate sample rate, module is not configured yet");
                return;
            }

            var activeChannels = dataChannels.Where(x => x.ChannelEnable.Value).Select(x => x.ChannelNumber);
            this.InfoLog("Starting receiver at sampling rate {0} Hz for active channels {1}", sampleRate, activeChannels.ToLazyString<byte>());
            sampleThread = machine.ObtainManagedThread(InputFrame, sampleRate, name: $"micfil_rx_sampling");
            sampleThread.Start();
        }

        private void StopThread()
        {
            if(sampleThread == null)
            {
                this.DebugLog("Trying to stop sampling when it is not active");
                return;
            }
            sampleThread.Stop();
            sampleThread = null;
        }

        private void InputFrame()
        {
            // All enabled data channels are filled synchronously.
            for(int i = 0; i < NumberOfDataChannels; i++)
            {
                var dataChannelIndex = dataChannelsWithSamplesInOrder[i];
                var decoder = pcmDecoders[i];

                if(decoder == null)
                {
                    // No data to load for this channel.
                    // It shouldn't be reached, as default source of samples is assigned to every channel.
                    this.ErrorLog("No data source for channel {0}", dataChannelIndex);
                    continue;
                }

                var dataChannel = dataChannels[dataChannelIndex];
                // We dequeue a sample from a decoder even if the current channel is disabled to not impact other channels loaded from the same decoder.
                var channelSample = decoder.GetSingleSample();

                if(!dataChannel.ChannelEnable.Value)
                {
                    continue;
                }

                if(!dataChannel.TryEnqueueSample(channelSample))
                {
                    this.DebugLog("Unable to enqueue sample for data channel {0}: 0x{1:X}", dataChannelIndex, channelSample);
                }
            }

            UpdateInterrupts();
        }

        // Output data rate is different from PDM clock sample rate used during PDM to PCM conversion.
        // The output of a decimation filter is stored into a FIFO buffer and accessible via DATACHn[DATA].
        // Samples loaded by PCM decoder are fed to FIFO buffer with the output rate calculated by this method.
        // The decimation filter can be bypassed by configuring CTRL_2[DEC_BYPASS]
        // which enables writing of raw PDM data into FIFO in the form of 32 bits.
        private bool TryCalculateOutputDataRate(out uint sampleRate)
        {
            sampleRate = 0;

            var osr = (uint)(32 - cicDecimationRate.Value);
            var divider = clockDividerDisable.Value ? 1 : (uint)clockDivider.Value;

            if(osr == 0 || divider == 0)
            {
                return false;
            }

            sampleRate = clockRootFrequencyHz / (8 * osr * divider);
            return true;
        }

        private void UpdateInterrupts()
        {
            if(dmaRequestInProgress)
            {
                return;
            }

            if(!micfilEnable.Value)
            {
                IRQ.Unset();
                return;
            }

            if(dmaInterruptSelection.Value == RequestMode.Dma)
            {
                // Try to issue DMA requests before evaluating module interrupt requests.
                // FIFO DMA and IRQ requests are exclusive, so either one or another is triggered.
                CheckDmaRequestTrigger();
            }

            var fifoRequest = (dmaInterruptSelection.Value == RequestMode.Interrupt) && IsWatermarkReached();
            var errorRequest = errorInterruptionEnable.Value && IsError();
            var value = fifoRequest || errorRequest;
            this.DebugLog("IRQ set to {0}", value);
            IRQ.Set(value);
        }

        private void CheckDmaRequestTrigger()
        {
            while(IsWatermarkReached())
            {
                // Track progress of DMA requests (DMA mux controlled by SYSCON may be disabled or eDMA channel itself may be deactivated).
                var fifoCountWatch = GetTotalFifoCount();
                this.DebugLog("Triggering DMA read request");
                dmaRequestInProgress = true;
                // Blink is used to represent both DMA request and DMA done signals, because during emulation DMA transfers finish immediately.
                DmaRequest.Blink();
                dmaRequestInProgress = false;
                if(fifoCountWatch == GetTotalFifoCount())
                {
                    // There are a few cases when this condition is fulfilled:
                    // 1. DMA request was not accepted due to a programmed eDMA channel configuration.
                    // 2. Transfer was rejected due to errors detected by eDMA in Transfer Control Descriptor (TCD) - such errors are reported by eDMA and visible to the software.
                    // 3. TCD was correct, but it didn't target data channel register as source address. It may be correct depending on developer intent.
                    // 4. DMA requests are not used at all by software. We don't know it at the model level, as it depends on a dynamic eDMA configuration.
                    // In either case there is no progress at MICFIL level, so break DMA trigger loop and try at the next opportunity.
                    break;
                }
            }
        }

        private void StartSampling()
        {
            // When decimation filter bypass is enabled, PDM data is written to FIFO directly without any processing.
            // We still use PCM decoder as the file format is agnostic of PCM/PDM differences.
            if(!TryCalculateOutputDataRate(out var sampleRate))
            {
                this.WarningLog("Unable to calculate sample rate, module is not configured yet");
                return;
            }
            var sampleWidthBits = decimationFilterBypass.Value ? PDMSampleBitWidth : PCMSampleBitWidth;

            var channelsWithoutSamples = GetChannelsWithoutSamples();
            for(var i = 0; i < channelsWithoutSamples.Length; i++)
            {
                var dataChannelNumber = channelsWithoutSamples[i];
                // Ensure that every data channel has a source of audio samples.
                // If samples were not loaded explicitly, assume a default source which returns zeros.
                dataChannelsWithSamplesInOrder[dataChannelsLoadIndex] = dataChannelNumber;
                // Zeros will be returned, as decoder is not backed by the file.
                pcmDecoders[dataChannelsLoadIndex] = new PCMDecoder(sampleWidthBits, sampleRate, 1, concatenatedChannels: false, this);
                this.WarningLog("There is no input file loaded for data channel {0} - zeros will be returned", dataChannelNumber);
                dataChannelsLoadIndex++;
            }

            for(var i = 0; i < NumberOfDataChannels; i++)
            {
                var dataChannelNumber = dataChannelsWithSamplesInOrder[i];
                var decoder = pcmDecoders[i];
                if(decoder.SampleWidthBits != sampleWidthBits || decoder.SamplingRateHz != sampleRate)
                {
                    this.ErrorLog("Mismatch between samples source ({0} bits, {1} Hz) and MICFIL configuration ({2} bits, {3} Hz) for data channel {4}", decoder.SampleWidthBits, decoder.SamplingRateHz, sampleWidthBits, sampleRate, dataChannelNumber);
                }
            }

            busyFlag.Value = true;
            StartThread();
        }

        private void StopSampling()
        {
            busyFlag.Value = false;
            StopThread();
        }

        private byte[] GetChannelsWithoutSamples()
        {
            var channelsWithoutSamples = new HashSet<byte>();
            for(byte i = 0; i < NumberOfDataChannels; i++)
            {
                channelsWithoutSamples.Add(i);
            }

            // Remove channels where samples were already explicitly loaded by user.
            for(byte i = 0; i < dataChannelsLoadIndex; i++)
            {
                var channelWithSamples = dataChannelsWithSamplesInOrder[i];
                channelsWithoutSamples.Remove(channelWithSamples);
            }

            return channelsWithoutSamples.ToArray();
        }

        private int GetTotalFifoCount()
        {
            return dataChannels.Sum(x => x.FifoCount);
        }

        private bool IsAnyChannelEnabled()
        {
            return dataChannels.Any(x => x.ChannelEnable.Value);
        }

        private bool IsWatermarkReached()
        {
            if(!IsAnyChannelEnabled())
            {
                return false;
            }
            return dataChannels.Where(x => x.ChannelEnable.Value).All(x => x.FifoWatermarkLevelReached);
        }

        private bool IsError()
        {
            if(!IsAnyChannelEnabled())
            {
                return false;
            }
            return dataChannels.Where(x => x.ChannelEnable.Value).Any(x => x.Error);
        }

        private void DefineRegisters()
        {
            Registers.Control1.Define(this, 0x80000000, name: "CTRL_1")
                .For((r, i) => r.WithFlag(i, out dataChannels[i].ChannelEnable, name: $"CH{i}EN"), 0, NumberOfDataChannels)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("FSYNCEN", 16)
                .WithReservedBits(17, 3)
                .WithTaggedFlag("DECFILS", 20)
                .WithReservedBits(21, 2)
                .WithFlag(23, out errorInterruptionEnable, name: "ERREN")
                .WithEnumField(24, 2, out dmaInterruptSelection, writeCallback: (_, value) =>
                {
                    if(value == RequestMode.Dma)
                    {
                        foreach(var channel in dataChannels)
                        {
                            channel.OutputDataFlag.Value = false;
                        }
                    }
                }, name: "DISEL")
                .WithTaggedFlag("DBGE", 26)
                .WithFlag(27, valueProviderCallback: _ => false, writeCallback: (_, value) =>
                {
                    if(value)
                    {
                        // Reset all internal buffers and FIFOs.
                        for(int i = 0; i < NumberOfDataChannels; i++)
                        {
                            dataChannels[i].Reset();
                        }
                    }
                }, name: "SRES")
                .WithTaggedFlag("DBG", 28)
                .WithFlag(29, out micfilEnable, name: "PDMIEN")
                .WithTaggedFlag("DOZEN", 30)
                .WithFlag(31, out moduleDisable, name: "MDIS")
                .WithWriteCallback((_, __) =>
                {
                    if(moduleDisable.Value)
                    {
                        // Stop sampling thread if it's currently sampling.
                        if(busyFlag.Value)
                        {
                            StopSampling();
                        }
                        return;
                    }
                    if(micfilEnable.Value && IsAnyChannelEnabled())
                    {
                        // Start sampling thread when it's not currently sampling and some data channel is active and module is enabled.
                        if(!busyFlag.Value)
                        {
                            StartSampling();
                        }
                    }
                    else
                    {
                        // Stop sampling thread when it's currently sampling and no data channel is active or module is disabled.
                        if(busyFlag.Value)
                        {
                            StopSampling();
                        }
                    }
                    UpdateInterrupts();
                });

            Registers.Control2.Define(this, 0x00080000, name: "CTRL_2")
                .WithValueField(0, 8, out clockDivider, name: "CLKDIV")
                .WithReservedBits(8, 7)
                .WithFlag(15, out clockDividerDisable, name: "CLKDIVDIS")
                .WithValueField(16, 5, out cicDecimationRate, name: "CICOSR")
                .WithReservedBits(21, 4)
                .WithTag("QSEL", 25, 3)
                .WithReservedBits(28, 3)
                .WithFlag(31, out decimationFilterBypass, name: "DEC_BYPASS");

            Registers.Status.Define(this, name: "STAT")
                .For((r, i) => r.WithFlag(i, out dataChannels[i].OutputDataFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: $"CH{i}F"), 0, NumberOfDataChannels)
                .WithReservedBits(8, 8)
                .WithReservedBits(16, 13)
                .WithReservedBits(29, 1)
                .WithReservedBits(30, 1)
                .WithFlag(31, out busyFlag, FieldMode.Read, name: "BSY_FIL");

            Registers.FifoControl.Define(this, DefaultFifoWatermark, name: "FIFO_CTRL")
                .WithValueField(0, 3, out fifoWatermarkControl, changeCallback: (_, value) =>
                {
                    foreach(var channel in dataChannels)
                    {
                        channel.SetFifoWatermarkLevel((int)value);
                    }
                }, name: "FIFOWMK")
                .WithReservedBits(3, 29)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.FifoStatus.Define(this, name: "FIFO_STAT")
                .For((r, i) => r
                    .WithFlag(i, out dataChannels[i].FifoOverflowExceptionFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: $"FIFOOVF{i}")
                    .WithFlag(i + NumberOfDataChannels, out dataChannels[i].FifoUnderflowExceptionFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: $"FIFOUND{i}"),
                    0, NumberOfDataChannels)
                .WithReservedBits(16, 16);

            Registers.OutputResult0.DefineMany(this, NumberOfDataChannels, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, valueProviderCallback: _ =>
                    {
                        dataChannels[idx].TryDequeueSample(out var sample);
                        // FIFO data width is 32-bit, but for PDM-PCM decimator only 24 most significant bits have information and the other bits are always 0.
                        // When decimator is bypassed, all 32 bits contain raw PDM sample and audio processing should be done by the software.
                        if(!decimationFilterBypass.Value)
                        {
                            sample <<= 8;
                        }
                        return sample;
                    }, name: "DATA")
                    .WithReadCallback((_, __) => UpdateInterrupts());
            }, 4, name: "DATACHn");

            Registers.DCRemoverControl.Define(this, 0x0000FFFF, name: "DC_CTRL")
                .WithTag("DCCONFIG", 0, 16) // FieldMode.Read
                .WithReservedBits(16, 16);

            Registers.OutputDCRemoverControl.Define(this, name: "DC_OUT_CTRL")
                .WithTag("DCCONFIG", 0, 16)
                .WithReservedBits(16, 16);

            Registers.RangeControl.Define(this, name: "RANGE_CTRL")
                .WithTag("RANGEADJ", 0, 32);

            Registers.RangeStatus.Define(this, name: "RANGE_STAT")
                .WithTaggedFlags("RANGEOVFn", 0, NumberOfDataChannels) // FieldMode.Read | FieldMode.WriteOneToClear
                .WithReservedBits(8, 8)
                .WithTaggedFlags("RANGEUNFn", 16, NumberOfDataChannels) // FieldMode.Read | FieldMode.WriteOneToClear
                .WithReservedBits(24, 8);

            Registers.FrameSynchronizationControl.Define(this, name: "FSYNC_CTRL")
                .WithTag("FSYNCLEN", 0, 32);

            Registers.VersionID.Define(this, 0x02160000, name: "VERID")
                .WithValueField(0, 16, FieldMode.Read, name: "FEATURE")
                .WithValueField(16, 8, FieldMode.Read, name: "MINOR")
                .WithValueField(24, 8, FieldMode.Read, name: "MAJOR");

            Registers.Parameter.Define(this, 0x010B0734, name: "PARAM")
                .WithValueField(0, 4, FieldMode.Read, name: "NPAIR")        // Number of Microphone Pairs: 4 pairs
                .WithValueField(4, 4, FieldMode.Read, name: "FIFO_PTRWID")  // FIFO Pointer Width: 3 bits
                .WithFlag(8, FieldMode.Read, name: "FIL_OUT_WIDTH_24B")            // Filter Output Width: 24 bits
                .WithFlag(9, FieldMode.Read, name: "LOW_POWER")                    // Low-Power Decimation Filter: Enabled
                .WithFlag(10, FieldMode.Read, name: "DC_BYPASS")                   // Input DC Remover Bypass: Disabled
                .WithFlag(11, FieldMode.Read, name: "DC_OUT_BYPASS")               // Output DC Remover Bypass: Active
                .WithReservedBits(12, 4)
                .WithFlag(16, FieldMode.Read, name: "HWVAD")                       // HWVAD: Active
                .WithFlag(17, FieldMode.Read, name: "HWVAD_ENERGY_MODE")           // HWVAD Energy Mode: Active
                .WithReservedBits(18, 1)
                .WithFlag(19, FieldMode.Read, name: "HWVAD_ZCD")                   // HWVAD ZCD: Active
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, FieldMode.Read, name: "NUM_HWVAD")   // Number of HWVADs: 1
                .WithReservedBits(28, 4);

            /* Hardware voice activity detector (HWVAD) functions are not implemented */
            Registers.VoiceActivityDetector0Control1.Define(this, name: "VAD0_CTRL_1")
                .WithFlag(0, out hwvadEnable, name: "VADEN")           // HWVAD Enable
                .WithTaggedFlag("VADRST", 1)                                // HWVAD Reset
                .WithFlag(2, out hwvadInterruptEnable, name: "VADIE")  // Interruption Enable
                .WithTaggedFlag("VADERIE", 3)                               // Error Interruption Enable
                .WithTaggedFlag("VADST10", 4)                               // Internal Filters Initialization
                .WithReservedBits(5, 3)
                .WithTag("VADINITT", 8, 5)                           // Initialization Time
                .WithReservedBits(13, 3)
                .WithTag("VADCICOSR", 16, 4)                         // CIC Oversampling Rate
                .WithReservedBits(20, 2)
                .WithReservedBits(22, 2)
                .WithTag("VADCHSEL", 24, 3)                          // Channel Selector
                .WithReservedBits(27, 5);

            Registers.VoiceActivityDetector0Control2.Define(this, 0x000A0000, name: "VAD0_CTRL_2")
                .WithTag("VADHPF", 0, 2)        // High-Pass Filter
                .WithReservedBits(2, 6)
                .WithTag("VADINPGAIN", 8, 4)    // Input Gain
                .WithReservedBits(12, 4)
                .WithTag("VADFRAMET", 16, 6)    // Frame Time
                .WithReservedBits(22, 6)
                .WithTaggedFlag("VADFOUTDIS", 28)      // Force Output Disable
                .WithReservedBits(29, 1)
                .WithTaggedFlag("VADPREFEN", 30)       // Pre Filter Enable
                .WithTaggedFlag("VADFRENDIS", 31);     // Frame Energy Disable

            Registers.VoiceActivityDetector0Status.Define(this, 0x80000000, name: "VAD0_STAT")
                .WithFlag(0, out hwvadInterruptFlag, name: "VADIF")    // Interrupt Flag
                .WithReservedBits(1, 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("VADINSATF", 16)       // Input Saturation Flag
                .WithReservedBits(17, 14)
                .WithTaggedFlag("VADINITF", 31);       // Initialization Flag

            Registers.VoiceActivityDetector0SignalConfiguration.Define(this, name: "VAD0_SCONFIG")
                .WithTag("VADSGAIN", 0, 4)      // Signal Gain
                .WithReservedBits(4, 26)
                .WithTaggedFlag("VADSMAXEN", 30)       // Signal Maximum Enable
                .WithTaggedFlag("VADSFILEN", 31);      // Signal Filter Enable

            Registers.VoiceActivityDetector0NoiseConfiguration.Define(this, 0x80000000, name: "VAD0_NCONFIG")
                .WithTag("VADNGAIN", 0, 4)      // Noise Gain
                .WithReservedBits(4, 4)
                .WithTag("VADNFILADJ", 8, 5)    // Noise Filter Adjustment
                .WithReservedBits(13, 15)
                .WithTaggedFlag("VADNOREN", 28)        // Noise OR Enable
                .WithTaggedFlag("VADNDECEN", 29)       // Noise Decimation Enable
                .WithTaggedFlag("VADNMINEN", 30)       // Noise Minimum Enable
                .WithTaggedFlag("VADNFILAUTO", 31);    // Noise Filter Auto

            Registers.VoiceActivityDetector0NoiseData.Define(this, name: "VAD0_NDATA")
                .WithTag("VADNDATA", 0, 16)     // Noise Data
                .WithReservedBits(16, 16);

            Registers.VoiceActivityDetector0ZeroCrossingDetector.Define(this, 0x00000004, name: "VAD0_ZCD")
                .WithTaggedFlag("VADZCDEN", 0)         // ZCD Enable
                .WithReservedBits(1, 1)
                .WithTaggedFlag("VADZCDAUTO", 2)       // ZCD Automatic Threshold
                .WithReservedBits(3, 1)
                .WithTaggedFlag("VADZCDAND", 4)        // ZCD AND Behavior
                .WithReservedBits(5, 3)
                .WithTag("VADZCDADJ", 8, 4)     // ZCD Adjustment
                .WithReservedBits(12, 4)
                .WithTag("VADZCDTH", 16, 10)    // ZCD Threshold
                .WithReservedBits(26, 6);
        }

        private bool dmaRequestInProgress;
        private IManagedThread sampleThread;
        private int dataChannelsLoadIndex;

        private IFlagRegisterField errorInterruptionEnable;
        private IEnumRegisterField<RequestMode> dmaInterruptSelection;
        private IFlagRegisterField micfilEnable;
        private IFlagRegisterField moduleDisable;
        private IValueRegisterField clockDivider;
        private IFlagRegisterField clockDividerDisable;
        private IValueRegisterField cicDecimationRate;
        private IFlagRegisterField decimationFilterBypass;
        private IFlagRegisterField busyFlag;
        private IValueRegisterField fifoWatermarkControl;
        private IFlagRegisterField hwvadEnable;
        private IFlagRegisterField hwvadInterruptEnable;
        private IFlagRegisterField hwvadInterruptFlag;

        private readonly uint clockRootFrequencyHz;

        private readonly DataChannel[] dataChannels = new DataChannel[NumberOfDataChannels];
        private readonly byte[] dataChannelsWithSamplesInOrder = new byte[NumberOfDataChannels];
        private readonly PCMDecoder[] pcmDecoders = new PCMDecoder[NumberOfDataChannels];

        private const int NumberOfMicrophonePairs = 4;
        private const int NumberOfAudioChannelsForMicrophone = 2;
        private const int NumberOfDataChannels = NumberOfMicrophonePairs * NumberOfAudioChannelsForMicrophone;
        private const int FifoCapacity = 8;
        private const int DefaultFifoWatermark = 7;
        private const uint PCMSampleBitWidth = 24;
        private const uint PDMSampleBitWidth = 32;

        private class DataChannel
        {
            public DataChannel(byte channelNumber)
            {
                ChannelNumber = channelNumber;
                fifo = new Queue<uint>(FifoCapacity);
                fifoWatermarkLevel = DefaultFifoWatermark;
            }

            public void Reset()
            {
                fifo.Clear();
            }

            public void SetFifoWatermarkLevel(int fifoWatermarkLevel)
            {
                this.fifoWatermarkLevel = fifoWatermarkLevel;
            }

            public bool TryEnqueueSample(uint sample)
            {
                if(!ChannelEnable.Value)
                {
                    return false;
                }
                if(fifo.Count >= FifoCapacity)
                {
                    // overrun
                    FifoOverflowExceptionFlag.Value = true;
                    return false;
                }
                else
                {
                    fifo.Enqueue(sample);
                    OutputDataFlag.Value = FifoWatermarkLevelReached;
                    return true;
                }
            }

            public bool TryDequeueSample(out uint sample)
            {
                if(fifo.TryDequeue(out var value))
                {
                    OutputDataFlag.Value = FifoWatermarkLevelReached;
                    sample = value;
                    return true;
                }
                else
                {
                    // underrun
                    FifoUnderflowExceptionFlag.Value = true;
                    sample = 0;
                    return false;
                }
            }

            public byte ChannelNumber { get; }

            public bool FifoWatermarkLevelReached => fifo.Count > fifoWatermarkLevel;

            public bool Error => FifoOverflowExceptionFlag.Value || FifoUnderflowExceptionFlag.Value;

            public int FifoCount => fifo.Count;

            public IFlagRegisterField ChannelEnable;
            public IFlagRegisterField OutputDataFlag;
            public IFlagRegisterField FifoOverflowExceptionFlag;
            public IFlagRegisterField FifoUnderflowExceptionFlag;

            private int fifoWatermarkLevel;

            private readonly Queue<uint> fifo;
        }

        private enum RequestMode
        {
            Disabled = 0b00,
            Dma = 0b01,
            Interrupt = 0b10,
            Reserved = 0b11
        }

        private enum Registers
        {
            Control1 = 0x00,
            Control2 = 0x04,
            Status = 0x08,
            FifoControl = 0x10,
            FifoStatus = 0x14,
            OutputResult0 = 0x24,
            OutputResult1 = 0x28,
            OutputResult2 = 0x2C,
            OutputResult3 = 0x30,
            OutputResult4 = 0x34,
            OutputResult5 = 0x38,
            OutputResult6 = 0x3C,
            OutputResult7 = 0x40,
            DCRemoverControl = 0x64,
            OutputDCRemoverControl = 0x68,
            RangeControl = 0x74,
            RangeStatus = 0x7C,
            FrameSynchronizationControl = 0x80,
            VersionID = 0x84,
            Parameter = 0x88,
            VoiceActivityDetector0Control1 = 0x90,
            VoiceActivityDetector0Control2 = 0x94,
            VoiceActivityDetector0Status = 0x98,
            VoiceActivityDetector0SignalConfiguration = 0x9C,
            VoiceActivityDetector0NoiseConfiguration = 0xA0,
            VoiceActivityDetector0NoiseData = 0xA4,
            VoiceActivityDetector0ZeroCrossingDetector = 0xA8,
        }
    }
}