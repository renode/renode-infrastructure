//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public class SiLabs_xG301_LPW : IBusPeripheral, IRadio, SiLabs_IProtocolTimer, SiLabs_IPacketTraceSniffer
    {
        public SiLabs_xG301_LPW(Machine machine, CV32E40P sequencer = null, SiLabs_IRvConfig sequencerConfig = null)
        {
            this.machine = machine;
            this.sequencer = sequencer;
            this.sequencerConfig = sequencerConfig;

            proTimer = new LimitTimer(machine.ClockSource, HfxoFrequency, this, "protimer", 0xFFFFUL, direction: Direction.Ascending,
                                      enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            proTimer.LimitReached += PROTIMER_HandleTimerLimitReached;

            paRampingTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "parampingtimer", 0xFFFFUL, direction: Direction.Ascending,
                                            enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            paRampingTimer.LimitReached += RAC_PaRampingTimerHandleLimitReached;

            rssiUpdateTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "rssiupdatetimer", 0xFFFFUL, direction: Direction.Ascending,
                                             enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            rssiUpdateTimer.LimitReached += AGC_RssiUpdateTimerHandleLimitReached;

            synthTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "synthtimer", 0xFFFFUL, direction: Direction.Ascending,
                                        enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            synthTimer.LimitReached += SYNTH_TimerLimitReached;

            txTimer = new LimitTimer(machine.ClockSource, HfxoFrequency, this, "txtimer", 0xFFFFUL, direction: Direction.Ascending,
                                     enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            txTimer.LimitReached += RAC_TxTimerLimitReached;

            rxTimer = new LimitTimer(machine.ClockSource, HfxoFrequency, this, "rxtimer", 0xFFFFUL, direction: Direction.Ascending,
                                     enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            rxTimer.LimitReached += RAC_RxTimerLimitReached;

            // Host CPU Interrupts
            FrameControllerPrioritizedIRQ = new GPIO();
            FrameControllerIRQ = new GPIO();
            ModulatorAndDemodulatorIRQ = new GPIO();
            RadioControllerSequencerIRQ = new GPIO();
            RadioControllerRadioStateMachineIRQ = new GPIO();
            BufferControllerIRQ = new GPIO();
            ProtocolTimerIRQ = new GPIO();
            SynthesizerIRQ = new GPIO();
            AutomaticGainControlIRQ = new GPIO();
            Lpw0PortalIRQ = new GPIO();
            RfTimerIRQ = new GPIO();

            // Sequencer CPU Interrupts
            SeqOffIRQ = new GPIO();
            SeqRxWarmIRQ = new GPIO();
            SeqRxSearchIRQ = new GPIO();
            SeqRxFrameIRQ = new GPIO();
            SeqRxWrapUpIRQ = new GPIO();
            SeqTxWarmIRQ = new GPIO();
            SeqTxIRQ = new GPIO();
            SeqTxWrapUpIRQ = new GPIO();
            SeqShutdownIRQ = new GPIO();
            SeqFrameControllerIRQ = new GPIO();
            SeqRadioControllerIRQ = new GPIO();
            SeqBufferControllerIRQ = new GPIO();
            SeqProtocolTimerIRQ = new GPIO();
            SeqModulatorAndDemodulatorIRQ = new GPIO();
            SeqSynthesizerIRQ = new GPIO();
            SeqAutomaticGainControlIRQ = new GPIO();
            SeqHostPortalIRQ = new GPIO();
            SeqRfMailboxIRQ = new GPIO();

            // FRC stuff
            FRC_frameDescriptor = new FRC_FrameDescriptor[FRC_NumberOfFrameDescriptors];
            for(var idx = 0u; idx < FRC_NumberOfFrameDescriptors; ++idx)
            {
                FRC_frameDescriptor[idx] = new FRC_FrameDescriptor();
            }
            FRC_packetBufferCapture = new byte[FRC_PacketBufferCaptureSize];

            // BUFC stuff
            BUFC_buffer = new BUFC_Buffer[BUFC_NumberOfBuffers];
            for(var idx = 0u; idx < BUFC_NumberOfBuffers; ++idx)
            {
                BUFC_buffer[idx] = new BUFC_Buffer(this, this.machine, idx);
            }

            // Protocol Timer stuff
            PROTIMER_timeoutCounter = new PROTIMER_TimeoutCounter[PROTIMER_NumberOfTimeoutCounters];
            for(var idx = 0; idx < PROTIMER_NumberOfTimeoutCounters; ++idx)
            {
                var i = idx;
                PROTIMER_timeoutCounter[i] = new PROTIMER_TimeoutCounter(this, (uint)i);
            }
            PROTIMER_timeoutCounter[0].Synchronized += () => PROTIMER_TimeoutCounter0HandleSynchronize();
            PROTIMER_timeoutCounter[0].Underflowed += () => PROTIMER_TimeoutCounter0HandleUnderflow();
            PROTIMER_timeoutCounter[0].Finished += () => PROTIMER_TimeoutCounter0HandleFinish();

            PROTIMER_captureCompareChannel = new PROTIMER_CaptureCompareChannel[PROTIMER_NumberOfCaptureCompareChannels];
            for(var idx = 0; idx < PROTIMER_NumberOfCaptureCompareChannels; ++idx)
            {
                var i = idx;
                PROTIMER_captureCompareChannel[i] = new PROTIMER_CaptureCompareChannel(this, (uint)i);
            }
            PROTIMER_ListenBeforeTalkRandomBackoffValue = new IValueRegisterField[PROTIMER_NumberOfListenBeforeTalkRandomBackoffValues];

            frameControllerRegistersCollection = BuildFrameControllerRegistersCollection();
            bufferControllerRegistersCollection = BuildBufferControllerRegistersCollection();
            cyclicRedundancyCheckRegistersCollection = BuildCyclicRedundancyCheckRegistersCollection();
            synthesizerRegistersCollection = BuildSynthesizerRegistersCollection();
            radioControllerRegistersCollection = BuildRadioControllerRegistersCollection();
            protocolTimerRegistersCollection = BuildProtocolTimerRegistersCollection();
            modulatorAndDemodulatorRegistersCollection = BuildModulatorAndDemodulatorRegistersCollection();
            automaticGainControlRegistersCollection = BuildAutomaticGainControlRegistersCollection();
            // HOST / sequencer communication
            hostPortalRegistersCollection = BuildHostPortalRegistersCollection();
            lpw0PortalRegistersCollection = BuildLpw0PortalRegistersCollection();
            // FSW / sequencer communication
            rfMailboxRegistersCollection = BuildRfMailboxRegistersCollection();
            fswMailboxRegistersCollection = BuildFswMailboxRegistersCollection();

            InterferenceQueue.InterferenceQueueChanged += InteferenceQueueChangedCallback;
        }

        public void Reset()
        {
            SYNTH_state = SYNTH_State.Idle;
        }

        public void InteferenceQueueChangedCallback()
        {
            if (RAC_currentRadioState == RAC_RadioState.RxSearch || RAC_currentRadioState == RAC_RadioState.RxFrame)
            {
                AGC_UpdateRssi();
            }
        }

        public void ReceiveFrame(byte[] frame, IRadio sender)
        {
            TimeInterval txStartTime = InterferenceQueue.GetTxStartTime(sender);
            if (txStartTime == TimeInterval.Empty)
            {
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "ReceiveFrame() at {0} on channel {1} ({2}): Dropping (TX was aborted)",
                         GetTime(), Channel, MODEM_GetCurrentPhy());
                return;
            }

            var txRxSimulatorDelayUs = (GetTime() - txStartTime).TotalMicroseconds;

            this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "ReceiveFrame() at {0} on channel {1} ({2}), TX started at {3} (diff: {4})", 
                     GetTime(), Channel, MODEM_GetCurrentPhy(), txStartTime, txRxSimulatorDelayUs);

            if (RAC_internalRxState != RAC_InternalRxState.Idle)
            {
                // The node is already in the process of receiving a packet, and a new packet is being received. 
                // TODO: for now we always consider this a collision. In the future we will want to take into account
                // the RSSI of both packets and determine if we could hear one of them.
                // We drop this packet, while the ongoing RX is marked as "interfered", which can result in either a 
                // preamble not heard at all or a frame that fails CRC.
                RAC_ongoingRxCollided = true;
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping: (RX already ongoing)");
                return;
            }

            // Ideally we would schedule a first RX step that accounts for the txChainDelay and some portion of the preamble
            // to still allow reception of frames for which we only heard some portion of the preamble.
            // For simplicity, we just skip this step and check the radio state after both preamble and sync word have been received.

            // We first schedule the RX timer to the point that the preamble and sych word are fully transmitted.
            // That must also include the txChainDelay since at the transmitter we don't delay the FrameTxStart event.
            double delayUs = MODEM_GetTxChainDelayUs() + MODEM_GetPreambleOverTheAirTimeUs() + MODEM_GetSyncWordOverTheAirTimeUs();
            RAC_internalRxState = RAC_InternalRxState.PreambleAndSyncWord;
            MODEM_demodulatorState.Value = MODEM_DemodulatorState.FrameSearch;

            currentFrame = frame;

            // We set the RSSIFRAME here to make sure the sender hasn't removed the packet from the interference queue
            // by the time the receiver has completed its RX delay.
            AGC_FrameRssiIntegerPart = (sbyte)InterferenceQueue.GetCurrentRssi(this, MODEM_GetCurrentPhy(), Channel);

            if (delayUs > txRxSimulatorDelayUs && PROTIMER_UsToPreCntOverflowTicks(delayUs - txRxSimulatorDelayUs) > 0)
            {
                RAC_rxTimeAlreadyPassedUs = 0;
                rxTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
                rxTimer.Limit = PROTIMER_UsToPreCntOverflowTicks(delayUs - txRxSimulatorDelayUs);
                rxTimer.Enabled = true;
                this.Log(LogLevel.Noisy, "Schedule rxTimer for PRE/SYNC: {0}us PRE={1} SYNC={2} TXCHAIN={3}", 
                         delayUs - txRxSimulatorDelayUs, MODEM_GetPreambleOverTheAirTimeUs(), MODEM_GetSyncWordOverTheAirTimeUs(), MODEM_GetTxChainDelayUs());
            }
            else
            {
                // The RX/TX simulator delay already accounted for the intended delay.
                // We store the time we already spent RXing the actual frame and call the RX timer
                // handler directly.
                RAC_rxTimeAlreadyPassedUs = txRxSimulatorDelayUs - delayUs;
                RAC_RxTimerLimitReached();
            }
            FrcSnifferReceiveFrame(frame);
        }

        private bool TransmitFrame(byte[] frame)
        {
            // TransmitFrame() is invoked as soon as the radio state machine transitions to the TX state.

            if (RAC_internalTxState != RAC_InternalTxState.Idle)
            {
                throw new Exception("TransmitFrame(): state not IDLE");
            }

            RAC_TxEnable = false;

            if (frame.Length == 0)
            {
                return false;
            }
            
            // We schedule the TX timer to include the whole frame (including the preamble and SYNC word) plus the 
            // TxChainDelay and TxChainDoneDelay, so that when the timer expires, we can simply complete the TX process.
            // Note, we subtract the TxDoneDelay since that signal occurs BEFORE the last bit of the frame actually went over the air.
            var timerDelayUs = MODEM_GetFrameOverTheAirTimeUs(frame, true, true) + MODEM_GetTxChainDelayUs() - MODEM_GetTxChainDoneDelayUs();
            
            this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Sending frame at {0} on channel {1} ({2}): {3}", 
                     GetTime(), Channel, MODEM_GetCurrentPhy(), BitConverter.ToString(frame));
            
            this.Log(LogLevel.Noisy, "TX timer delay (us)={0} (PRECNT overflows={1}) (OTA frame time (us)={2})", 
                     timerDelayUs, PROTIMER_UsToPreCntOverflowTicks(timerDelayUs), MODEM_GetFrameOverTheAirTimeUs(frame, true, true));

            InterferenceQueue.Add(this, MODEM_GetCurrentPhy(), Channel, 0 /*TODO: TxPower*/, frame);
            FrcSnifferTransmitFrame(frame);
            FrameSent?.Invoke(this, frame);

            MODEM_txPreambleSentInterrupt.Value = true;
            MODEM_seqTxPreambleSentInterrupt.Value = true;
            MODEM_txSyncSentInterrupt.Value |= !MODEM_syncData.Value;
            MODEM_seqTxSyncSentInterrupt.Value = MODEM_txSyncSentInterrupt.Value;

            UpdateInterrupts();

            RAC_internalTxState = RAC_InternalTxState.Tx;
            txTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
            txTimer.Limit = PROTIMER_UsToPreCntOverflowTicks(timerDelayUs);
            txTimer.Enabled = true;

            return true;
        }

#region Region Accessors
        [ConnectionRegionAttribute("frc_s")]
        public uint ReadDoubleWordFromFrameController(long offset)
        {
            return Read<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_S)", offset);
        }

        [ConnectionRegionAttribute("frc_s")]
        public byte ReadByteFromFrameController(long offset)
        {
            return ReadByte<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_S)", offset);
        }

        [ConnectionRegionAttribute("frc_s")]
        public void WriteDoubleWordToFrameController(long offset, uint value)
        {
            Write<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_S)", offset, value);
        }

        [ConnectionRegionAttribute("frc_s")]
        public void WriteByteToFrameController(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("frc_ns")]
        public uint ReadDoubleWordFromFrameControllerNonSecure(long offset)
        {
            return Read<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset);
        }

        [ConnectionRegionAttribute("frc_ns")]
        public byte ReadByteFromFrameControllerNonSecure(long offset)
        {
            return ReadByte<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset);
        }

        [ConnectionRegionAttribute("frc_ns")]
        public void WriteDoubleWordToFrameControllerNonSecure(long offset, uint value)
        {
            Write<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("frc_ns")]
        public void WriteByteToFrameControllerNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("agc_s")]
        public uint ReadDoubleWordFromAutomaticGainController(long offset)
        {
            return Read<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset);
        }

        [ConnectionRegionAttribute("agc_s")]
        public byte ReadByteFromAutomaticGainController(long offset)
        {
            return ReadByte<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset);
        }

        [ConnectionRegionAttribute("agc_s")]
        public void WriteDoubleWordToAutomaticGainController(long offset, uint value)
        {
            Write<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset, value);
        }

        [ConnectionRegionAttribute("agc_s")]
        public void WriteByteToAutomaticGainController(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("agc_ns")]
        public uint ReadDoubleWordFromAutomaticGainControllerNonSecure(long offset)
        {
            return Read<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_NS)", offset);
        }

        [ConnectionRegionAttribute("agc_ns")]
        public byte ReadByteFromAutomaticGainControllerNonSecure(long offset)
        {
            return ReadByte<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset);
        }

        [ConnectionRegionAttribute("agc_ns")]
        public void WriteDoubleWordToAutomaticGainControllerNonSecure(long offset, uint value)
        {
            Write<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("agc_ns")]
        public void WriteByteToAutomaticGainControllerNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("crc_s")]
        public uint ReadDoubleWordFromCyclicRedundancyCheck(long offset)
        {
            return Read<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_S)", offset);
        }

        [ConnectionRegionAttribute("crc_s")]
        public byte ReadByteFromCyclicRedundancyCheck(long offset)
        {
            return ReadByte<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_S)", offset);
        }

        [ConnectionRegionAttribute("crc_s")]
        public void WriteDoubleWordToCyclicRedundancyCheck(long offset, uint value)
        {
            Write<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_S)", offset, value);
        }

        [ConnectionRegionAttribute("crc_s")]
        public void WriteByteToCyclicRedundancyCheck(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("crc_ns")]
        public uint ReadDoubleWordFromCyclicRedundancyCheckNonSecure(long offset)
        {
            return Read<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset);
        }

        [ConnectionRegionAttribute("crc_ns")]
        public byte ReadByteFromCyclicRedundancyCheckNonSecure(long offset)
        {
            return ReadByte<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset);
        }

        [ConnectionRegionAttribute("crc_ns")]
        public void WriteDoubleWordToCyclicRedundancyCheckNonSecure(long offset, uint value)
        {
            Write<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("crc_ns")]
        public void WriteByteToCyclicRedundancyCheckNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("modem_s")]
        public uint ReadDoubleWordFromModulatorAndDemodulator(long offset)
        {
            return Read<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_S)", offset);
        }

        [ConnectionRegionAttribute("modem_s")]
        public byte ReadByteFromModulatorAndDemodulator(long offset)
        {
            return ReadByte<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_S)", offset);
        }

        [ConnectionRegionAttribute("modem_s")]
        public void WriteDoubleWordToModulatorAndDemodulator(long offset, uint value)
        {
            Write<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_S)", offset, value);
        }

        [ConnectionRegionAttribute("modem_s")]
        public void WriteByteToModulatorAndDemodulator(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("modem_ns")]
        public uint ReadDoubleWordFromModulatorAndDemodulatorNonSecure(long offset)
        {
            return Read<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset);
        }

        [ConnectionRegionAttribute("modem_ns")]
        public byte ReadByteFromModulatorAndDemodulatorNonSecure(long offset)
        {
            return ReadByte<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset);
        }

        [ConnectionRegionAttribute("modem_ns")]
        public void WriteDoubleWordToModulatorAndDemodulatorNonSecure(long offset, uint value)
        {
            Write<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset, value);
        }

        [ConnectionRegionAttribute("modem_ns")]
        public void WriteByteToModulatorAndDemodulatorNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("synth_s")]
        public uint ReadDoubleWordFromSynthesizer(long offset)
        {
            return Read<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_S)", offset);
        }

        [ConnectionRegionAttribute("synth_s")]
        public byte ReadByteFromSynthesizer(long offset)
        {
            return ReadByte<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_S)", offset);
        }

        [ConnectionRegionAttribute("synth_s")]
        public void WriteDoubleWordToSynthesizer(long offset, uint value)
        {
            Write<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_S)", offset, value);
        }

        [ConnectionRegionAttribute("synth_s")]
        public void WriteByteToSynthesizer(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("synth_ns")]
        public uint ReadDoubleWordFromSynthesizerNonSecure(long offset)
        {
            return Read<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset);
        }

        [ConnectionRegionAttribute("synth_ns")]
        public byte ReadByteFromSynthesizerNonSecure(long offset)
        {
            return ReadByte<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset);
        }

        [ConnectionRegionAttribute("synth_ns")]
        public void WriteDoubleWordToSynthesizerNonSecure(long offset, uint value)
        {
            Write<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset, value);
        }

        [ConnectionRegionAttribute("synth_ns")]
        public void WriteByteToSynthesizerNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("protimer_s")]
        public uint ReadDoubleWordFromProtocolTimer(long offset)
        {
            return Read<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_S)", offset);
        }

        [ConnectionRegionAttribute("protimer_s")]
        public byte ReadByteFromProtocolTimer(long offset)
        {
            return ReadByte<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_S)", offset);
        }

        [ConnectionRegionAttribute("protimer_s")]
        public void WriteDoubleWordToProtocolTimer(long offset, uint value)
        {
            Write<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_S)", offset, value);
        }

        [ConnectionRegionAttribute("protimer_s")]
        public void WriteByteToProtocolTimer(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public uint ReadDoubleWordFromProtocolTimerNonSecure(long offset)
        {
            return Read<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset);
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public byte ReadByteFromProtocolTimerNonSecure(long offset)
        {
            return ReadByte<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset);
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public void WriteDoubleWordToProtocolTimerNonSecure(long offset, uint value)
        {
            Write<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset, value);
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public void WriteByteToProtocolTimerNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("rac_s")]
        public uint ReadDoubleWordFromRadioController(long offset)
        {
            return Read<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_S)", offset);
        }

        [ConnectionRegionAttribute("rac_s")]
        public byte ReadByteFromRadioController(long offset)
        {
            return ReadByte<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_S)", offset);
        }

        [ConnectionRegionAttribute("rac_s")]
        public void WriteDoubleWordToRadioController(long offset, uint value)
        {
            Write<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_S)", offset, value);
        }

        [ConnectionRegionAttribute("rac_s")]
        public void WriteByteToRadioController(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("rac_ns")]
        public uint ReadDoubleWordFromRadioControllerNonSecure(long offset)
        {
            return Read<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset);
        }

        [ConnectionRegionAttribute("rac_ns")]
        public byte ReadByteFromRadioControllerNonSecure(long offset)
        {
            return ReadByte<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset);
        }

        [ConnectionRegionAttribute("rac_ns")]
        public void WriteDoubleWordToRadioControllerNonSecure(long offset, uint value)
        {
            Write<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset, value);
        }


        [ConnectionRegionAttribute("rac_ns")]
        public void WriteByteToRadioControllerNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("bufc_s")]
        public uint ReadDoubleWordFromBufferController(long offset)
        {
            return Read<BufferControllerRegisters>(bufferControllerRegistersCollection, "Buffer Controller (BUFC_S)", offset);
        }

        [ConnectionRegionAttribute("bufc_s")]
        public byte ReadByteFromBufferController(long offset)
        {
            return ReadByte<BufferControllerRegisters>(bufferControllerRegistersCollection, "Buffer Controller (BUFC_S)", offset);
        }

        [ConnectionRegionAttribute("bufc_s")]
        public void WriteDoubleWordToBufferController(long offset, uint value)
        {
            Write<BufferControllerRegisters>(bufferControllerRegistersCollection, "Buffer Controller (BUFC_S)", offset, value);
        }

        [ConnectionRegionAttribute("bufc_s")]
        public void WriteByteToBufferController(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("bufc_ns")]
        public uint ReadDoubleWordFromBufferControllerNonSecure(long offset)
        {
            return Read<BufferControllerRegisters>(bufferControllerRegistersCollection, "Buffer Controller (BUFC_NS)", offset);
        }

        [ConnectionRegionAttribute("bufc_ns")]
        public byte ReadByteFromBufferControllerNonSecure(long offset)
        {
            return ReadByte<BufferControllerRegisters>(bufferControllerRegistersCollection, "Buffer Controller (BUFC_NS)", offset);
        }

        [ConnectionRegionAttribute("bufc_ns")]
        public void WriteDoubleWordToBufferControllerNonSecure(long offset, uint value)
        {
            Write<BufferControllerRegisters>(bufferControllerRegistersCollection, "Buffer Controller (BUFC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("bufc_ns")]
        public void WriteByteToBufferControllerNonSecure(long offset, byte value)
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public uint ReadDoubleWordFromRfMailbox(long offset)
        {
            return Read<RfMailboxRegisters>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public byte ReadByteFromRfMailbox(long offset)
        {
            return ReadByte<RfMailboxRegisters>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public void WriteDoubleWordToRfMailbox(long offset, uint value)
        {   
            Write<RfMailboxRegisters>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_S)", offset, value);
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public void WriteByteToRfMailbox(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public uint ReadDoubleWordFromRfMailboxNonSecure(long offset)
        {
            
            return Read<RfMailboxRegisters>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public byte ReadByteFromRfMailboxNonSecure(long offset)
        {
            
            return ReadByte<RfMailboxRegisters>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public void WriteDoubleWordToRfMailboxNonSecure(long offset, uint value)
        {   
            Write<RfMailboxRegisters>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_NS)", offset, value);
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public void WriteByteToRfMailboxNonSecure(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public uint ReadDoubleWordFromFswMailbox(long offset)
        {
            return Read<FswMailboxRegisters>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public byte ReadByteFromFswMailbox(long offset)
        {
            return ReadByte<FswMailboxRegisters>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public void WriteDoubleWordToFswMailbox(long offset, uint value)
        {   
            Write<FswMailboxRegisters>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_S)", offset, value);
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public void WriteByteToFswMailbox(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public uint ReadDoubleWordFromFswMailboxNonSecure(long offset)
        {
            
            return Read<FswMailboxRegisters>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public byte ReadByteFromFswMailboxNonSecure(long offset)
        {
            
            return ReadByte<FswMailboxRegisters>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public void WriteDoubleWordToFswMailboxNonSecure(long offset, uint value)
        {   
            Write<FswMailboxRegisters>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_NS)", offset, value);
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public void WriteByteToFswMailboxNonSecure(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public uint ReadDoubleWordFromHostPortal(long offset)
        {
            return Read<HostPortalRegisters>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public byte ReadByteFromHostPortal(long offset)
        {
            return ReadByte<HostPortalRegisters>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public void WriteDoubleWordToHostPortal(long offset, uint value)
        {   
            Write<HostPortalRegisters>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_S)", offset, value);
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public void WriteByteToHostPortal(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public uint ReadDoubleWordFromHostPortalNonSecure(long offset)
        {
            
            return Read<HostPortalRegisters>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public byte ReadByteFromHostPortalNonSecure(long offset)
        {
            
            return ReadByte<HostPortalRegisters>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public void WriteDoubleWordToHostPortalNonSecure(long offset, uint value)
        {   
            Write<HostPortalRegisters>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_NS)", offset, value);
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public void WriteByteToHostPortalNonSecure(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public uint ReadDoubleWordFromLpw0Portal(long offset)
        {
            return Read<Lpw0PortalRegisters>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public byte ReadByteFromLpw0Portal(long offset)
        {
            return ReadByte<Lpw0PortalRegisters>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public void WriteDoubleWordToLpw0Portal(long offset, uint value)
        {   
            Write<Lpw0PortalRegisters>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_S)", offset, value);
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public void WriteByteToLpw0Portal(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public uint ReadDoubleWordFromLpw0PortalNonSecure(long offset)
        {
            
            return Read<Lpw0PortalRegisters>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public byte ReadByteFromLpw0PortalNonSecure(long offset)
        {
            
            return ReadByte<Lpw0PortalRegisters>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public void WriteDoubleWordToLpw0PortalNonSecure(long offset, uint value)
        {   
            Write<Lpw0PortalRegisters>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_NS)", offset, value);
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public void WriteByteToLpw0PortalNonSecure(long offset, byte value)
        {   
            // TODO: Single byte writes not implemented for now
        }
#endregion

        // Main core IRQs
        public GPIO FrameControllerPrioritizedIRQ { get; }
        public GPIO FrameControllerIRQ { get; }
        public GPIO ModulatorAndDemodulatorIRQ { get; }
        public GPIO RadioControllerSequencerIRQ { get; }
        public GPIO RadioControllerRadioStateMachineIRQ { get; }
        public GPIO BufferControllerIRQ { get; }
        public GPIO ProtocolTimerIRQ { get; }
        public GPIO SynthesizerIRQ { get; }
        public GPIO AutomaticGainControlIRQ { get; }
        public GPIO Lpw0PortalIRQ { get; }
        public GPIO RfTimerIRQ { get; }
        // Sequencer core IRQs
        public GPIO SeqOffIRQ { get; }
        public GPIO SeqRxWarmIRQ { get; }
        public GPIO SeqRxSearchIRQ { get; }
        public GPIO SeqRxFrameIRQ { get; }
        public GPIO SeqRxWrapUpIRQ { get; }
        public GPIO SeqTxWarmIRQ { get; }
        public GPIO SeqTxIRQ { get; }
        public GPIO SeqTxWrapUpIRQ { get; }
        public GPIO SeqShutdownIRQ { get; }
        public GPIO SeqFrameControllerIRQ { get; }
        public GPIO SeqRadioControllerIRQ { get; }
        public GPIO SeqBufferControllerIRQ { get; }
        public GPIO SeqProtocolTimerIRQ { get; }
        public GPIO SeqModulatorAndDemodulatorIRQ { get; }
        public GPIO SeqAutomaticGainControlIRQ { get; }
        public GPIO SeqSynthesizerIRQ { get; }
        public GPIO SeqHostPortalIRQ { get; }
        public GPIO SeqRfMailboxIRQ { get; }
        public event Action<IRadio, byte[]> FrameSent;
        public event Action<uint> PreCountOverflowsEvent;
        public event Action<uint> BaseCountOverflowsEvent;
        public event Action<uint> WrapCountOverflowsEvent;
        public event Action<uint> CaptureCompareEvent;
        private byte[] currentFrame;
        private uint currentFrameOffset;
        private int currentChannel = 0;
        public int Channel { 
            get
            {
                return currentChannel;
            }
            set
            {
                currentChannel = value;
            }
        }
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        private readonly Machine machine;
        private readonly CV32E40P sequencer;
        private readonly SiLabs_IRvConfig sequencerConfig;
        private static PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private readonly LimitTimer proTimer;
        private readonly LimitTimer paRampingTimer;
        private readonly LimitTimer rssiUpdateTimer;
        private readonly LimitTimer synthTimer;
        private readonly LimitTimer txTimer;
        private readonly LimitTimer rxTimer;
        private readonly DoubleWordRegisterCollection automaticGainControlRegistersCollection;
        private readonly DoubleWordRegisterCollection bufferControllerRegistersCollection;
        private readonly DoubleWordRegisterCollection cyclicRedundancyCheckRegistersCollection;
        private readonly DoubleWordRegisterCollection frameControllerRegistersCollection;
        private readonly DoubleWordRegisterCollection modulatorAndDemodulatorRegistersCollection;
        private readonly DoubleWordRegisterCollection protocolTimerRegistersCollection;
        private readonly DoubleWordRegisterCollection radioControllerRegistersCollection;
        private readonly DoubleWordRegisterCollection rfMailboxRegistersCollection;
        private readonly DoubleWordRegisterCollection fswMailboxRegistersCollection;
        private readonly DoubleWordRegisterCollection synthesizerRegistersCollection;
        private readonly DoubleWordRegisterCollection hostPortalRegistersCollection;
        private readonly DoubleWordRegisterCollection lpw0PortalRegistersCollection;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const long HfxoFrequency = 38400000L;
        private const long MicrosecondFrequency = 1000000L;
        private const long HalfMicrosecondFrequency = 2000000L;
        public bool LogInterrupts = false;
        public bool LogRegisterAccess = false;
        public bool LogBasicRadioActivityAsError = false;
        public bool ForceBusyRssi
        {
            set
            {
                InterferenceQueue.ForceBusyRssi = value;
            }
        }

#region PTI Fields
        public event Action<SiLabs_PacketTraceFrameType> PtiFrameStart;
        public event Action<byte[]> PtiDataOut;
        public event Action PtiFrameComplete;
        const int SNIFF_SYNCWORD_SERIAL_LEN = 4;
#endregion PTI Fields
#region PTI Methods
        private void FrcSnifferReceiveFrame(byte[] frame)
        {
            PtiFrameStart?.Invoke(SiLabs_PacketTraceFrameType.Receive);
            // NOTE the sync word makes up the first 4 bytes of the frame
            int syncWordBytes = Math.Min((int) MODEM_SyncWordBytes, SNIFF_SYNCWORD_SERIAL_LEN);
            var syncWord = frame.Take(syncWordBytes).ToArray();
            // NOTE the rest of the frame is the actual data
            var frameData = frame.Skip(syncWordBytes).ToArray();
            if (FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte) PacketTraceFrameDelimiters.RxStart });
            }
            if (FRC_ptiEmitSyncWord.Value)
            {
                var syncWordPadded = new byte[SNIFF_SYNCWORD_SERIAL_LEN];
                Array.Copy(syncWord, syncWordPadded, syncWord.Length);
                PtiDataOut?.Invoke(syncWordPadded);
            }
            if (FRC_ptiEmitRx.Value)
            {
                PtiDataOut?.Invoke(frameData);
            }
            if (FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.RxEndSuccess });
            }
            if (FRC_ptiEmitRssi.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)AGC_FrameRssiIntegerPart });
            }
        }

        private void FrcSnifferTransmitFrame(byte[] frame)
        {
            PtiFrameStart?.Invoke(SiLabs_PacketTraceFrameType.Transmit);
            // NOTE the sync word makes up the first 4 bytes of the frame
            int syncWordBytes = Math.Min((int)MODEM_SyncWordBytes, SNIFF_SYNCWORD_SERIAL_LEN);
            var syncWord = frame.Take(syncWordBytes).ToArray();
            // NOTE the rest of the frame is the actual data
            var frameData = frame.Skip(syncWordBytes).ToArray();
            if (FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.TxStart });
            }
            if (FRC_ptiEmitSyncWord.Value)
            {
                var syncWordPadded = new byte[SNIFF_SYNCWORD_SERIAL_LEN];
                Array.Copy(syncWord, syncWordPadded, syncWord.Length);
                PtiDataOut?.Invoke(syncWordPadded);
            }
            if (FRC_ptiEmitTx.Value)
            {
                PtiDataOut?.Invoke(frameData);
            }
            if (FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.TxEndSuccess });
            }
        }
        public UInt64 PacketTraceRadioTimestamp()
        {
            return (UInt64) GetTime().TotalMicroseconds * 1000;
        }
#endregion PTI Methods

#region Build Register Collections
        private DoubleWordRegisterCollection BuildFrameControllerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)FrameControllerRegisters.Status, new DoubleWordRegister(this)
                    .WithTag("SNIFFDCOUNT", 0, 5)
                    .WithFlag(5, out FRC_activeTransmitFrameDescriptor, FieldMode.Read, name: "ACTIVETXFCD")
                    .WithFlag(6, out FRC_activeReceiveFrameDescriptor, FieldMode.Read, name: "ACTIVERXFCD")
                    .WithTaggedFlag("SNIFFDFRAME", 7)
                    .WithFlag(8, out FRC_rxRawBlocked, FieldMode.Read, name: "RXRAWBLOCKED")
                    .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => true, name: "FRAMEOK")
                    .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "RXABORTINPROGRESS")
                    .WithTaggedFlag("TXWORD", 11)
                    .WithTaggedFlag("RXWORD", 12)
                    .WithTaggedFlag("CONVPAUSED", 13)
                    .WithTaggedFlag("TXSUBFRAMEPAUSED", 14)
                    .WithTaggedFlag("INTERLEAVEREADPAUSED", 15)
                    .WithTaggedFlag("INTERLEAVEWRITEPAUSED", 16)
                    .WithTaggedFlag("FRAMEDETPAUSED", 17)
                    .WithTaggedFlag("FRAMELENGTHERROR", 18)
                    .WithTaggedFlag("DEMODERROR", 19)
                    .WithValueField(20, 5, out FRC_fsmState, name: "FSMSTATE")
                    .WithTaggedFlag("RXWCNTMATCHPAUSED", 25)
                    .WithTaggedFlag("CRCERRORTOLERATED", 26)
                    .WithReservedBits(27, 5)
                },
                {(long)FrameControllerRegisters.DynamicFrameLengthControl, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, FRC_DynamicFrameLengthMode>(0, 3, out FRC_dynamicFrameLengthMode, name: "DFLMODE")
                    .WithEnumField<DoubleWordRegister, FRC_DynamicFrameLengthBitOrder>(3, 1, out FRC_dynamicFrameLengthBitOrder, name: "DFLBITORDER")
                    .WithValueField(4, 3, out FRC_dynamicFrameLengthBitShift, name: "DFLSHIFT")
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 4, out FRC_dynamicFrameLengthOffset, name: "DFLOFFSET")
                    .WithValueField(12, 4, out FRC_dynamicFrameLengthBits, name: "DFLBITS")
                    .WithValueField(16, 4, out FRC_minDecodedLength, name: "MINLENGTH")
                    .WithFlag(20, out FRC_dynamicFrameCrcIncluded, name: "DFLINCLUDECRC")
                    .WithTag("DFLBOIOFFSET", 21, 4)
                    .WithReservedBits(25, 7)
                },
                {(long)FrameControllerRegisters.MaximumFrameLength, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out FRC_maxDecodedLength, name: "MAXLENGTH")
                    .WithValueField(12, 4, out FRC_initialDecodedFrameLength, name: "INILENGTH")
                    .WithReservedBits(16, 16)
                },
                {(long)FrameControllerRegisters.WordCounter, new DoubleWordRegister(this)
                    .WithValueField(0 ,12, out FRC_wordCounter, FieldMode.Read, name: "WCNT")
                    .WithReservedBits(12, 20)
                },
                {(long)FrameControllerRegisters.WordCounterCompare0, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out FRC_frameLength, name: "FRAMELENGTH")
                    .WithReservedBits(12, 20)
                },
                {(long)FrameControllerRegisters.WordCounterCompare1, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out FRC_lengthFieldLocation, name: "LENGTHFIELDLOC")
                    .WithReservedBits(12, 20)
                },
                {(long)FrameControllerRegisters.WordCounterCompare2, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out FRC_addressFieldLocation, name: "ADDRFIELDLOC")
                    .WithReservedBits(12, 20)
                },
                {(long)FrameControllerRegisters.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => {if (value) FRC_RxAbortCommand(); }, name: "RXABORT")
                    .WithTaggedFlag("FRAMEDETRESUME", 1)
                    .WithTaggedFlag("INTERLEAVEWRITERESUME", 2)
                    .WithTaggedFlag("INTERLEAVEREADRESUME", 3)
                    .WithTaggedFlag("CONVRESUME", 4)
                    .WithTaggedFlag("CONVTERMINATE", 5)
                    .WithTaggedFlag("TXSUBFRAMERESUME", 6)
                    .WithTaggedFlag("INTERLEAVEINIT", 7)
                    .WithTaggedFlag("INTERLEAVECNTCLEAR", 8)
                    .WithTaggedFlag("CONVINIT", 9)
                    .WithTaggedFlag("BLOCKINIT", 10)
                    .WithTaggedFlag("STATEINIT", 11)
                    .WithFlag(12, FieldMode.Set, writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                FRC_rxRawBlocked.Value = false;
                                FRC_UpdateRawMode();
                            }
                        }, name: "RXRAWUNBLOCK")
                    .WithTaggedFlag("RXPAUSERESUME", 13)    
                    .WithReservedBits(14, 18)
                },
                {(long)FrameControllerRegisters.Control, new DoubleWordRegister(this)
                    .WithTaggedFlag("RANDOMTX", 0)
                    .WithTaggedFlag("UARTMODE", 1)
                    .WithTaggedFlag("BITORDER", 2)
                    .WithTaggedFlag("LSBFRTREVERT", 3)
                    .WithEnumField<DoubleWordRegister, FRC_FrameDescriptorMode>(4, 2, out FRC_txFrameDescriptorMode, name: "TXFCDMODE")
                    .WithEnumField<DoubleWordRegister, FRC_FrameDescriptorMode>(6, 2, out FRC_rxFrameDescriptorMode, name: "RXFCDMODE")
                    .WithTag("BITSPERWORD", 8, 3) // TODO: Assume 0x7 (first word in a frame is 8bit)
                    .WithTag("RATESELECT", 11, 2)
                    .WithTaggedFlag("TXPREFETCH", 13)
                    .WithTaggedFlag("TXFETCHBLOCKING", 14)
                    .WithTaggedFlag("RXABORTHWBEH", 15)
                    .WithTaggedFlag("SEQHANDSHAKE", 16)
                    .WithTaggedFlag("PRBSTEST", 17)
                    .WithTaggedFlag("LPMODEDIS", 18)
                    .WithTaggedFlag("WAITEOFEN", 19)
                    .WithTaggedFlag("RXABORTIGNOREDIS", 20)
                    .WithTaggedFlag("FRAMEENDAHEADDIS", 21)
                    .WithTag("RXABORTHWSEL", 22, 2)
                    .WithTaggedFlag("SKIPTXTRAILDATAWHITEN", 24)
                    .WithTaggedFlag("SKIPRXSUPSTATEWHITEN", 25)
                    .WithTaggedFlag("HOLDTXTRAILDATAACTIVE", 26)
                    .WithTag("LPMODEEXTEND", 27, 3)
                    .WithTaggedFlag("LPMODELRBLE", 30)
                    .WithTaggedFlag("RXABORTHWDIS", 31)
                },
                {(long)FrameControllerRegisters.RxControl, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_rxStoreCrc, name: "STORECRC")
                    .WithFlag(1, out FRC_rxAcceptCrcErrors, name: "ACCEPTCRCERRORS")
                    .WithTaggedFlag("ACCEPTBLOCKERRORS", 2)
                    .WithTaggedFlag("TRACKABFRAME", 3) /* TODO: RENODE-354 */
                    .WithFlag(4, out FRC_rxBufferClear, name: "BUFCLEAR")
                    .WithFlag(5, out FRC_rxBufferRestoreOnFrameError, name: "BUFRESTOREFRAMEERROR")
                    .WithFlag(6, out FRC_rxBufferRestoreOnRxAborted, name: "BUFRESTORERXABORTED")
                    .WithTag("RXFRAMEENDAHEADBYTES", 7, 4)
                    .WithTaggedFlag("ACCEPTUARTERRORS", 11)
                    .WithReservedBits(12, 4)
                    .WithTag("IFINPUTWIDTH", 16, 2)
                    .WithReservedBits(18, 14)
                },
                {(long)FrameControllerRegisters.TrailingRxData, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_rxAppendRssi, name: "RSSI")
                    .WithFlag(1, out FRC_rxAppendStatus, name: "CRCOK")
                    .WithFlag(2, out FRC_rxAppendProtimerCc0BaseLow, name: "PROTIMERCC0BASEL")
                    .WithFlag(3, out FRC_rxAppendProtimerCc0BaseHigh, name: "PROTIMERCC0BASEH")
                    .WithFlag(4, out FRC_rxAppendProtimerCc0WrapLow, name: "PROTIMERCC0WRAPL")
                    .WithFlag(5, out FRC_rxAppendProtimerCc0WrapHigh, name: "PROTIMERCC0WRAPH")
                    .WithTaggedFlag("RTCSTAMP", 6)
                    .WithReservedBits(7, 25)
                },
                {(long)FrameControllerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_txDoneInterrupt, name: "TXDONEIF")
                    .WithFlag(1, out FRC_txAfterFrameDoneInterrupt, name: "TXAFTERFRAMEDONEIF")
                    .WithTaggedFlag("TXABORTEDIF", 2)
                    .WithFlag(3, out FRC_txUnderflowInterrupt, name: "TXUFIF")
                    .WithFlag(4, out FRC_rxDoneInterrupt, name: "RXDONEIF")
                    .WithFlag(5, out FRC_rxAbortedInterrupt, name: "RXABORTEDIF")
                    .WithFlag(6, out FRC_frameErrorInterrupt, name: "FRAMEERRORIF")
                    .WithTaggedFlag("BLOCKERRORIF", 7)
                    .WithFlag(8, out FRC_rxOverflowInterrupt, name: "RXOFIF")
                    .WithTaggedFlag("WCNTCMP0IF", 9)
                    .WithTaggedFlag("WCNTCMP1IF", 10)
                    .WithTaggedFlag("WCNTCMP2IF", 11)
                    .WithTaggedFlag("ADDRERRORIF", 12)
                    .WithTaggedFlag("BUSERRORIF", 13)
                    .WithFlag(14, out FRC_rxRawEventInterrupt, name: "RXRAWEVENTIF")
                    .WithFlag(15, out FRC_txRawEventInterrupt, name: "TXRAWEVENTIF")
                    .WithTaggedFlag("SNIFFOFIF", 16)
                    .WithTaggedFlag("WCNTCMP3IF", 17)
                    .WithTaggedFlag("WCNTCMP4IF", 18)
                    .WithTaggedFlag("BOISETIF", 19)
                    .WithFlag(20, out FRC_packetBufferStartInterrupt, name: "PKTBUFSTARTIF")
                    .WithFlag(21, out FRC_packetBufferThresholdInterrupt, name: "PKTBUFTHRESHOLDIF")
                    .WithTaggedFlag("RXRAWOFIF", 22)
                    .WithTaggedFlag("WCNTCMP5IF", 23)
                    .WithTaggedFlag("FRAMEDETPAUSEDIF", 24)
                    .WithTaggedFlag("INTERLEAVEWRITEPAUSEDIF", 25)
                    .WithTaggedFlag("INTERLEAVEREADPAUSEDIF", 26)
                    .WithTaggedFlag("TXSUBFRAMEPAUSEDIF", 27)
                    .WithTaggedFlag("CONVPAUSEDIF", 28)
                    .WithTaggedFlag("RXWORDIF", 29)
                    .WithTaggedFlag("TXWORDIF", 30)
                    .WithTaggedFlag("UARTERRORIF", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_txDoneInterruptEnable, name: "TXDONEIEN")
                    .WithFlag(1, out FRC_txAfterFrameDoneInterruptEnable, name: "TXAFTERFRAMEDONEIEN")
                    .WithTaggedFlag("TXABORTEDIEN", 2)
                    .WithFlag(3, out FRC_txUnderflowInterruptEnable, name: "TXUFIEN")
                    .WithFlag(4, out FRC_rxDoneInterruptEnable, name: "RXDONEIEN")
                    .WithFlag(5, out FRC_rxAbortedInterruptEnable, name: "RXABORTEDIEN")
                    .WithFlag(6, out FRC_frameErrorInterruptEnable, name: "FRAMEERRORIEN")
                    .WithTaggedFlag("BLOCKERRORIEN", 7)
                    .WithFlag(8, out FRC_rxOverflowInterruptEnable, name: "RXOFIEN")
                    .WithTaggedFlag("WCNTCMP0IEN", 9)
                    .WithTaggedFlag("WCNTCMP1IEN", 10)
                    .WithTaggedFlag("WCNTCMP2IEN", 11)
                    .WithTaggedFlag("ADDRERRORIEN", 12)
                    .WithTaggedFlag("BUSERRORIEN", 13)
                    .WithFlag(14, out FRC_rxRawEventInterruptEnable, name: "RXRAWEVENTIEN")
                    .WithFlag(15, out FRC_txRawEventInterruptEnable, name: "TXRAWEVENTIEN")
                    .WithTaggedFlag("SNIFFOFIEN", 16)
                    .WithTaggedFlag("WCNTCMP3IEN", 17)
                    .WithTaggedFlag("WCNTCMP4IEN", 18)
                    .WithTaggedFlag("BOISETIEN", 19)
                    .WithFlag(20, out FRC_packetBufferStartInterruptEnable, name: "PKTBUFSTARTIEN")
                    .WithFlag(21, out FRC_packetBufferThresholdInterruptEnable, name: "PKTBUFTHRESHOLDIEN")
                    .WithTaggedFlag("RXRAWOFIEN", 22)
                    .WithTaggedFlag("WCNTCMP5IEN", 23)
                    .WithTaggedFlag("FRAMEDETPAUSEDIEN", 24)
                    .WithTaggedFlag("INTERLEAVEWRITEPAUSEDIEN", 25)
                    .WithTaggedFlag("INTERLEAVEREADPAUSEDIEN", 26)
                    .WithTaggedFlag("TXSUBFRAMEPAUSEDIEN", 27)
                    .WithTaggedFlag("CONVPAUSEDIEN", 28)
                    .WithTaggedFlag("RXWORDIEN", 29)
                    .WithTaggedFlag("TXWORDIEN", 30)
                    .WithTaggedFlag("UARTERRORIEN", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_seqTxDoneInterrupt, name: "TXDONESEQIF")
                    .WithFlag(1, out FRC_seqTxAfterFrameDoneInterrupt, name: "TXAFTERFRAMEDONESEQIF")
                    .WithTaggedFlag("TXABORTEDSEQIF", 2)
                    .WithFlag(3, out FRC_seqTxUnderflowInterrupt, name: "TXUFSEQIF")
                    .WithFlag(4, out FRC_seqRxDoneInterrupt, name: "RXDONESEQIF")
                    .WithFlag(5, out FRC_seqRxAbortedInterrupt, name: "RXABORTEDSEQIF")
                    .WithFlag(6, out FRC_seqFrameErrorInterrupt, name: "FRAMEERRORSEQIF")
                    .WithTaggedFlag("BLOCKERRORSEQIF", 7)
                    .WithFlag(8, out FRC_seqRxOverflowInterrupt, name: "RXOFSEQIF")
                    .WithTaggedFlag("WCNTCMP0SEQIF", 9)
                    .WithTaggedFlag("WCNTCMP1SEQIF", 10)
                    .WithTaggedFlag("WCNTCMP2SEQIF", 11)
                    .WithTaggedFlag("ADDRERRORSEQIF", 12)
                    .WithTaggedFlag("BUSERRORSEQIF", 13)
                    .WithFlag(14, out FRC_seqRxRawEventInterrupt, name: "RXRAWEVENTSEQIF")
                    .WithFlag(15, out FRC_seqTxRawEventInterrupt, name: "TXRAWEVENTSEQIF")
                    .WithTaggedFlag("SNIFFOFSEQIF", 16)
                    .WithTaggedFlag("WCNTCMP3SEQIF", 17)
                    .WithTaggedFlag("WCNTCMP4SEQIF", 18)
                    .WithTaggedFlag("BOISETSEQIF", 19)
                    .WithFlag(20, out FRC_seqPacketBufferStartInterrupt, name: "PKTBUFSTARTSEQIF")
                    .WithFlag(21, out FRC_seqPacketBufferThresholdInterrupt, name: "PKTBUFTHRESHOLDSEQIF")
                    .WithTaggedFlag("RXRAWOFSEQIF", 22)
                    .WithTaggedFlag("WCNTCMP5SEQIF", 23)
                    .WithTaggedFlag("FRAMEDETPAUSEDSEQIF", 24)
                    .WithTaggedFlag("INTERLEAVEWRITEPAUSEDSEQIF", 25)
                    .WithTaggedFlag("INTERLEAVEREADPAUSEDSEQIF", 26)
                    .WithTaggedFlag("TXSUBFRAMEPAUSEDSEQIF", 27)
                    .WithTaggedFlag("CONVPAUSEDSEQIF", 28)
                    .WithTaggedFlag("RXWORDSEQIF", 29)
                    .WithTaggedFlag("TXWORDSEQIF", 30)
                    .WithTaggedFlag("UARTERRORSEQIF", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_seqTxDoneInterruptEnable, name: "TXDONESEQIEN")
                    .WithFlag(1, out FRC_seqTxAfterFrameDoneInterruptEnable, name: "TXAFTERFRAMEDONESEQIEN")
                    .WithTaggedFlag("TXABORTEDSEQIEN", 2)
                    .WithFlag(3, out FRC_seqTxUnderflowInterruptEnable, name: "TXUFSEQIEN")
                    .WithFlag(4, out FRC_seqRxDoneInterruptEnable, name: "RXDONESEQIEN")
                    .WithFlag(5, out FRC_seqRxAbortedInterruptEnable, name: "RXABORTEDSEQIEN")
                    .WithFlag(6, out FRC_seqFrameErrorInterruptEnable, name: "FRAMEERRORSEQIEN")
                    .WithTaggedFlag("BLOCKERRORSEQIEN", 7)
                    .WithFlag(8, out FRC_seqRxOverflowInterruptEnable, name: "RXOFSEQIEN")
                    .WithTaggedFlag("WCNTCMP0SEQIEN", 9)
                    .WithTaggedFlag("WCNTCMP1SEQIEN", 10)
                    .WithTaggedFlag("WCNTCMP2SEQIEN", 11)
                    .WithTaggedFlag("ADDRERRORSEQIEN", 12)
                    .WithTaggedFlag("BUSERRORSEQIEN", 13)
                    .WithFlag(14, out FRC_seqRxRawEventInterruptEnable, name: "RXRAWEVENTSEQIEN")
                    .WithFlag(15, out FRC_seqTxRawEventInterruptEnable, name: "TXRAWEVENTSEQIEN")
                    .WithTaggedFlag("SNIFFOFSEQIEN", 16)
                    .WithTaggedFlag("WCNTCMP3SEQIEN", 17)
                    .WithTaggedFlag("WCNTCMP4SEQIEN", 18)
                    .WithTaggedFlag("BOISETSEQIEN", 19)
                    .WithFlag(20, out FRC_seqPacketBufferStartInterruptEnable, name: "PKTBUFSTARTSEQIEN")
                    .WithFlag(21, out FRC_seqPacketBufferThresholdInterruptEnable, name: "PKTBUFTHRESHOLDSEQIEN")
                    .WithTaggedFlag("RXRAWOFSEQIEN", 22)
                    .WithTaggedFlag("WCNTCMP5SEQIEN", 23)
                    .WithTaggedFlag("FRAMEDETPAUSEDSEQIEN", 24)
                    .WithTaggedFlag("INTERLEAVEWRITEPAUSEDSEQIEN", 25)
                    .WithTaggedFlag("INTERLEAVEREADPAUSEDSEQIEN", 26)
                    .WithTaggedFlag("TXSUBFRAMEPAUSEDSEQIEN", 27)
                    .WithTaggedFlag("CONVPAUSEDSEQIEN", 28)
                    .WithTaggedFlag("RXWORDSEQIEN", 29)
                    .WithTaggedFlag("TXWORDSEQIEN", 30)
                    .WithTaggedFlag("UARTERRORSEQIEN", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.RawDataControl, new DoubleWordRegister(this)
                    .WithTag("TXRAWMODE", 0, 2)
                    .WithEnumField<DoubleWordRegister, FRC_RxRawDataMode>(2, 3, out FRC_rxRawDataSelect, writeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case FRC_RxRawDataMode.Disable:
                                    FRC_rxRawBlocked.Value = false;
                                    break;
                                case FRC_RxRawDataMode.SingleItem:
                                    break;
                                default:
                                    this.Log(LogLevel.Error, "Unsupported RXRAWMODE value ({0}).", value);
                                    break;
                            }
                        }, name: "RXRAWMODE")
                    .WithFlag(5, out FRC_enableRawDataRandomNumberGenerator, name: "RXRAWRANDOM")
                    .WithReservedBits(6, 1)
                    .WithEnumField<DoubleWordRegister, FRC_RxRawDataTriggerMode>(7, 2, out FRC_rxRawDataTriggerSelect, writeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case FRC_RxRawDataTriggerMode.Immediate:
                                    break;
                                default:
                                    this.Log(LogLevel.Error, "Unsupported RXRAWTRIGGER value ({0}).", value);
                                    break;
                            }
                        }, name: "RXRAWTRIGGER")
                    .WithReservedBits(9, 4)
                    .WithTaggedFlag("DEMODRAWDATAMUX", 13)
                    .WithReservedBits(14, 18)
                    .WithChangeCallback((_, __) => FRC_UpdateRawMode())
                },
                {(long)FrameControllerRegisters.RxRawData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(FRC_rxRawDataSelect.Value == FRC_RxRawDataMode.SingleItem)
                        {
                            FRC_rxRawBlocked.Value = false;
                        }
                        FRC_UpdateRawMode();
                        return (uint)random.Next();
                    }, name: "RXRAWDATA")
                },
                {(long)FrameControllerRegisters.PacketCaptureBufferControl, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out FRC_packetBufferStartAddress, name: "PKTBUFSTARTLOC")
                    .WithValueField(12, 6, out FRC_packetBufferThreshold, writeCallback: (_, __) => { UpdateInterrupts(); }, name: "PKTBUFTHRESHOLD")
                    .WithReservedBits(18, 6)
                    .WithFlag(24, out FRC_packetBufferThresholdEnable, writeCallback: (_, __) => { UpdateInterrupts(); }, name: "PKTBUFTHRESHOLDEN")
                    .WithFlag(25, out FRC_packetBufferStop, FieldMode.Write, name: "PKTBUFSTOP")
                    .WithReservedBits(26, 6)
                },
                {(long)FrameControllerRegisters.PacketCaptureBufferStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 6, out FRC_packetBufferCount, FieldMode.Read, name: "PKTBUFCOUNT")
                    .WithReservedBits(6, 24)
                },
                {(long)FrameControllerRegisters.SnifferControl, new DoubleWordRegister(this)
                    .WithTag("SNIFFMODE", 0, 2)
                    .WithTaggedFlag("SNIFFBITS", 2)
                    .WithFlag(3, out FRC_ptiEmitRx, name: "SNIFFRXDATA")
                    .WithFlag(4, out FRC_ptiEmitTx, name: "SNIFFTXDATA")
                    .WithFlag(5, out FRC_ptiEmitRssi, name: "SNIFFRSSI")
                    .WithFlag(6, out FRC_ptiEmitState, name: "SNIFFSTATE")
                    .WithFlag(7, out FRC_ptiEmitAux, name: "SNIFFAUXDATA")
                    .WithTag("SNIFFAUXDATAMASK", 8, 8)
                    .WithTaggedFlag("SNIFFSLEEPCTRL", 16)
                    .WithFlag(17, out FRC_ptiEmitSyncWord, name: "SNIFFSYNCWORD")
                    .WithTaggedFlag("SNIFFRACSTATE", 18)
                    .WithTaggedFlag("SNIFFDFRAMECTRL", 19)
                    .WithTaggedFlag("SNIFFDFRAMEFORCE", 20)
                    .WithReservedBits(21, 3)
                    .WithTag("SNIFFBR", 24, 8)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.AuxiliarySnifferDataOutput, new DoubleWordRegister(this)
                    .WithValueField(0, 9, writeCallback: (_, data) => {
                        if (FRC_ptiEmitAux.Value)
                        {
                            this.Log(LogLevel.Noisy, "PTI AUX: {0}", data);
                            // chop off the top bit
                            byte b = (byte) (data & 0xFF);
                            PtiDataOut?.Invoke(new byte[] {b });
                        }
                        if ((data & 0x0100) == 0u) {
                            PtiFrameComplete?.Invoke();
                        }
                    }, name: "SNIFFAUXDATA")
                    .WithReservedBits(9, 23)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            var startOffset = (long)FrameControllerRegisters.FrameControlDescriptor0;
            var blockSize = (long)FrameControllerRegisters.FrameControlDescriptor1 - (long)FrameControllerRegisters.FrameControlDescriptor0;
            for(var index = 0; index < FRC_NumberOfFrameDescriptors; index++)
            {
                var i = index;
                
                registerDictionary.Add(startOffset + blockSize*i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, out FRC_frameDescriptor[i].words, name: "WORDS")
                        .WithValueField(8, 2, out FRC_frameDescriptor[i].buffer, name: "BUFFER")
                        .WithFlag(10, out FRC_frameDescriptor[i].includeCrc, name: "INCLUDECRC")
                        .WithFlag(11, out FRC_frameDescriptor[i].calculateCrc, name: "CALCCRC")
                        .WithValueField(12, 2, out FRC_frameDescriptor[i].crcSkipWords, name: "SKIPCRC")
                        .WithFlag(14, out FRC_frameDescriptor[i].skipWhitening, name: "SKIPWHITE")
                        .WithFlag(15, out FRC_frameDescriptor[i].addTrailData, name: "ADDTRAILTXDATA")
                        .WithFlag(16, out FRC_frameDescriptor[i].excludeSubframeFromWordCounter, name: "EXCLUDESUBFRAMEWCNT")
                        .WithReservedBits(17, 15)
                );
            }

            startOffset = (long)FrameControllerRegisters.PacketCaptureDataBuffer0;
            blockSize = (long)FrameControllerRegisters.PacketCaptureDataBuffer1 - (long)FrameControllerRegisters.PacketCaptureDataBuffer0;
            for(var index = 0; index < (FRC_PacketBufferCaptureSize / 4); index++)
            {
                var i = index;
                
                registerDictionary.Add(startOffset + blockSize*i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i*4], writeCallback: (_, value) => FRC_packetBufferCapture[i*4] = (byte)value, name: $"PKTBUF{i*4}")
                        .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i*4 + 1], writeCallback: (_, value) => FRC_packetBufferCapture[i*4 + 1] = (byte)value, name: $"PKTBUF{i*4 + 1}")
                        .WithValueField(16, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i*4 + 2], writeCallback: (_, value) => FRC_packetBufferCapture[i*4 + 2] = (byte)value, name: $"PKTBUF{i*4 + 2}")
                        .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i*4 + 3], writeCallback: (_, value) => FRC_packetBufferCapture[i*4 + 3] = (byte)value, name: $"PKTBUF{i*4 + 3}")
                );
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildBufferControllerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)BufferControllerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out BUFC_buffer[0].overflow, name: "BUF0OFIF")
                    .WithFlag(1, out BUFC_buffer[0].underflow, name: "BUF0UFIF")
                    .WithFlag(2, out BUFC_buffer[0].thresholdEvent, name: "BUF0THRIF")
                    .WithFlag(3, out BUFC_buffer[0].corrupt, name: "BUF0CORRIF")
                    .WithFlag(4, out BUFC_buffer[0].notWordAligned, name: "BUF0NWAIF")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out BUFC_buffer[1].overflow, name: "BUF1OFIF")
                    .WithFlag(9, out BUFC_buffer[1].underflow, name: "BUF1UFIF")
                    .WithFlag(10, out BUFC_buffer[1].thresholdEvent, name: "BUF1THRIF")
                    .WithFlag(11, out BUFC_buffer[1].corrupt, name: "BUF1CORRIF")
                    .WithFlag(12, out BUFC_buffer[1].notWordAligned, name: "BUF1NWAIF")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out BUFC_buffer[2].overflow, name: "BUF2OFIF")
                    .WithFlag(17, out BUFC_buffer[2].underflow, name: "BUF2UFIF")
                    .WithFlag(18, out BUFC_buffer[2].thresholdEvent, name: "BUF2THRIF")
                    .WithFlag(19, out BUFC_buffer[2].corrupt, name: "BUF2CORRIF")
                    .WithFlag(20, out BUFC_buffer[2].notWordAligned, name: "BUF2NWAIF")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out BUFC_buffer[3].overflow, name: "BUF3OFIF")
                    .WithFlag(25, out BUFC_buffer[3].underflow, name: "BUF3UFIF")
                    .WithFlag(26, out BUFC_buffer[3].thresholdEvent, name: "BUF3THRIF")
                    .WithFlag(27, out BUFC_buffer[3].corrupt, name: "BUF3CORRIF")
                    .WithFlag(28, out BUFC_buffer[3].notWordAligned, name: "BUF3NWAIF")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("BUSERRORIF", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)BufferControllerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out BUFC_buffer[0].overflowEnable, name: "BUF0OFIEN")
                    .WithFlag(1, out BUFC_buffer[0].underflowEnable, name: "BUF0UFIEN")
                    .WithFlag(2, out BUFC_buffer[0].thresholdEventEnable, name: "BUF0THRIEN")
                    .WithFlag(3, out BUFC_buffer[0].corruptEnable, name: "BUF0CORRIEN")
                    .WithFlag(4, out BUFC_buffer[0].notWordAlignedEnable, name: "BUF0NWAIEN")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out BUFC_buffer[1].overflowEnable, name: "BUF1OFIEN")
                    .WithFlag(9, out BUFC_buffer[1].underflowEnable, name: "BUF1UFIEN")
                    .WithFlag(10, out BUFC_buffer[1].thresholdEventEnable, name: "BUF1THRIEN")
                    .WithFlag(11, out BUFC_buffer[1].corruptEnable, name: "BUF1CORRIEN")
                    .WithFlag(12, out BUFC_buffer[1].notWordAlignedEnable, name: "BUF1NWAIEN")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out BUFC_buffer[2].overflowEnable, name: "BUF2OFIEN")
                    .WithFlag(17, out BUFC_buffer[2].underflowEnable, name: "BUF2UFIEN")
                    .WithFlag(18, out BUFC_buffer[2].thresholdEventEnable, name: "BUF2THRIEN")
                    .WithFlag(19, out BUFC_buffer[2].corruptEnable, name: "BUF2CORRIEN")
                    .WithFlag(20, out BUFC_buffer[2].notWordAlignedEnable, name: "BUF2NWAIEN")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out BUFC_buffer[3].overflowEnable, name: "BUF3OFIEN")
                    .WithFlag(25, out BUFC_buffer[3].underflowEnable, name: "BUF3UFIEN")
                    .WithFlag(26, out BUFC_buffer[3].thresholdEventEnable, name: "BUF3THRIEN")
                    .WithFlag(27, out BUFC_buffer[3].corruptEnable, name: "BUF3CORRIEN")
                    .WithFlag(28, out BUFC_buffer[3].notWordAlignedEnable, name: "BUF3NWAIEN")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("BUSERRORIEN", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)BufferControllerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out BUFC_buffer[0].seqOverflow, name: "BUF0OFSEQIF")
                    .WithFlag(1, out BUFC_buffer[0].seqUnderflow, name: "BUF0UFSEQIF")
                    .WithFlag(2, out BUFC_buffer[0].seqThresholdEvent, name: "BUF0THRSEQIF")
                    .WithFlag(3, out BUFC_buffer[0].seqCorrupt, name: "BUF0CORRSEQIF")
                    .WithFlag(4, out BUFC_buffer[0].seqNotWordAligned, name: "BUF0NWASEQIF")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out BUFC_buffer[1].seqOverflow, name: "BUF1OFSEQIF")
                    .WithFlag(9, out BUFC_buffer[1].seqUnderflow, name: "BUF1UFSEQIF")
                    .WithFlag(10, out BUFC_buffer[1].seqThresholdEvent, name: "BUF1THRSEQIF")
                    .WithFlag(11, out BUFC_buffer[1].seqCorrupt, name: "BUF1CORRSEQIF")
                    .WithFlag(12, out BUFC_buffer[1].seqNotWordAligned, name: "BUF1NWASEQIF")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out BUFC_buffer[2].seqOverflow, name: "BUF2OFSEQIF")
                    .WithFlag(17, out BUFC_buffer[2].seqUnderflow, name: "BUF2UFSEQIF")
                    .WithFlag(18, out BUFC_buffer[2].seqThresholdEvent, name: "BUF2THRSEQIF")
                    .WithFlag(19, out BUFC_buffer[2].seqCorrupt, name: "BUF2CORRSEQIF")
                    .WithFlag(20, out BUFC_buffer[2].seqNotWordAligned, name: "BUF2NWASEQIF")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out BUFC_buffer[3].seqOverflow, name: "BUF3OFSEQIF")
                    .WithFlag(25, out BUFC_buffer[3].seqUnderflow, name: "BUF3UFSEQIF")
                    .WithFlag(26, out BUFC_buffer[3].seqThresholdEvent, name: "BUF3THRSEQIF")
                    .WithFlag(27, out BUFC_buffer[3].seqCorrupt, name: "BUF3CORRSEQIF")
                    .WithFlag(28, out BUFC_buffer[3].seqNotWordAligned, name: "BUF3NWASEQIF")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("BUSERRORSEQIF", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)BufferControllerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out BUFC_buffer[0].seqOverflowEnable, name: "BUF0OFSEQIEN")
                    .WithFlag(1, out BUFC_buffer[0].seqUnderflowEnable, name: "BUF0UFSEQIEN")
                    .WithFlag(2, out BUFC_buffer[0].seqThresholdEventEnable, name: "BUF0THRSEQIEN")
                    .WithFlag(3, out BUFC_buffer[0].seqCorruptEnable, name: "BUF0CORRSEQIEN")
                    .WithFlag(4, out BUFC_buffer[0].seqNotWordAlignedEnable, name: "BUF0NWASEQIEN")
                    .WithReservedBits(5, 3)
                    .WithFlag(8, out BUFC_buffer[1].seqOverflowEnable, name: "BUF1OFSEQIEN")
                    .WithFlag(9, out BUFC_buffer[1].seqUnderflowEnable, name: "BUF1UFSEQIEN")
                    .WithFlag(10, out BUFC_buffer[1].seqThresholdEventEnable, name: "BUF1THRSEQIEN")
                    .WithFlag(11, out BUFC_buffer[1].seqCorruptEnable, name: "BUF1CORRSEQIEN")
                    .WithFlag(12, out BUFC_buffer[1].seqNotWordAlignedEnable, name: "BUF1NWASEQIEN")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, out BUFC_buffer[2].seqOverflowEnable, name: "BUF2OFSEQIEN")
                    .WithFlag(17, out BUFC_buffer[2].seqUnderflowEnable, name: "BUF2UFSEQIEN")
                    .WithFlag(18, out BUFC_buffer[2].seqThresholdEventEnable, name: "BUF2THRSEQIEN")
                    .WithFlag(19, out BUFC_buffer[2].seqCorruptEnable, name: "BUF2CORRSEQIEN")
                    .WithFlag(20, out BUFC_buffer[2].seqNotWordAlignedEnable, name: "BUF2NWASEQIEN")
                    .WithReservedBits(21, 3)
                    .WithFlag(24, out BUFC_buffer[3].seqOverflowEnable, name: "BUF3OFSEQIEN")
                    .WithFlag(25, out BUFC_buffer[3].seqUnderflowEnable, name: "BUF3UFSEQIEN")
                    .WithFlag(26, out BUFC_buffer[3].seqThresholdEventEnable, name: "BUF3THRSEQIEN")
                    .WithFlag(27, out BUFC_buffer[3].seqCorruptEnable, name: "BUF3CORRSEQIEN")
                    .WithFlag(28, out BUFC_buffer[3].seqNotWordAlignedEnable, name: "BUF3NWASEQIEN")
                    .WithReservedBits(29, 2)
                    .WithTaggedFlag("BUSERRORSEQIEN", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            var startOffset = (long)BufferControllerRegisters.Buffer0Control;
            var controlOffset = (long)BufferControllerRegisters.Buffer0Control - startOffset;
            var addrOffset = (long)BufferControllerRegisters.Buffer0Address - startOffset;
            var wOffOffset = (long)BufferControllerRegisters.Buffer0WriteOffset - startOffset;
            var rOffOffset = (long)BufferControllerRegisters.Buffer0ReadOffset - startOffset;
            var wStartOffset = (long)BufferControllerRegisters.Buffer0WriteStart - startOffset;
            var rDataOffset = (long)BufferControllerRegisters.Buffer0ReadData - startOffset;
            var wDataOffset = (long)BufferControllerRegisters.Buffer0WriteData - startOffset;
            var xWriteOffset = (long)BufferControllerRegisters.Buffer0XorWrite - startOffset;
            var statusOffset = (long)BufferControllerRegisters.Buffer0Status - startOffset;
            var thresOffset = (long)BufferControllerRegisters.Buffer0ThresholdControl - startOffset;
            var cmdOffset = (long)BufferControllerRegisters.Buffer0Command - startOffset;
            var rData32Offset = (long)BufferControllerRegisters.Buffer0ReadData32 - startOffset;
            var wData32Offset = (long)BufferControllerRegisters.Buffer0WriteData32 - startOffset;
            var xWrite32Offset = (long)BufferControllerRegisters.Buffer0XorWrite32 - startOffset;
            var blockSize = (long)BufferControllerRegisters.Buffer1Control - (long)BufferControllerRegisters.Buffer0Control;
            for(var index = 0; index < BUFC_NumberOfBuffers; index++)
            {
                var i = index;
                registerDictionary.Add(startOffset + blockSize*i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithEnumField<DoubleWordRegister, BUFC_SizeMode>(0, 3, out BUFC_buffer[i].sizeMode, name: "SIZE")
                        .WithReservedBits(3, 29));
                registerDictionary.Add(startOffset + blockSize*i + addrOffset,
                    new DoubleWordRegister(this, 0x8000000)
                        // Treat this as a whole 32 bit field and mask out the 2 LSBs instead of defining the 2 LSBs as reserved bits.
                        .WithValueField(0, 32, valueProviderCallback: _ => BUFC_buffer[i].Address, writeCallback: (_, value) => BUFC_buffer[i].Address = (uint)(value & 0xFFFFFFFC), name: "ADDR"));
                registerDictionary.Add(startOffset + blockSize*i + wOffOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, valueProviderCallback: _ => BUFC_buffer[i].WriteOffset, writeCallback: (_, value) => BUFC_buffer[i].WriteOffset = (uint)value, name: "WRITEOFFSET")
                        .WithReservedBits(13, 19));
                registerDictionary.Add(startOffset + blockSize*i + rOffOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, valueProviderCallback: _ => BUFC_buffer[i].ReadOffset, writeCallback: (_, value) => BUFC_buffer[i].ReadOffset = (uint)value, name: "READOFFSET")
                        .WithReservedBits(13, 19));
                registerDictionary.Add(startOffset + blockSize*i + wStartOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, out BUFC_buffer[i].writeStartOffset, FieldMode.Read, name: "WRITESTART")
                        .WithReservedBits(13, 19));
                registerDictionary.Add(startOffset + blockSize*i + rDataOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => BUFC_buffer[i].ReadData, name: "READDATA")
                        .WithReservedBits(8, 24));
                registerDictionary.Add(startOffset + blockSize*i + wDataOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => BUFC_buffer[i].WriteData = (uint)value, name: "WRITEDATA")
                        .WithReservedBits(8, 24));
                registerDictionary.Add(startOffset + blockSize*i + xWriteOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, value) => BUFC_buffer[i].XorWriteData = (uint)value, name: "XORWRITEDATA")
                        .WithReservedBits(8, 24));
                registerDictionary.Add(startOffset + blockSize*i + statusOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, FieldMode.Read, valueProviderCallback: _ => BUFC_buffer[i].BytesNumber, name: "BYTES")
                        .WithReservedBits(13, 3)
                        .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => BUFC_buffer[i].ReadReady, name: "RDATARDY")
                        .WithReservedBits(17, 3)
                        .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => BUFC_buffer[i].ThresholdFlag, name: "THRESHOLDFLAG")
                        .WithReservedBits(21, 3)
                        .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => BUFC_buffer[i].Read32Ready, name: "RDATA32RDY")
                        .WithReservedBits(25, 7));
                registerDictionary.Add(startOffset + blockSize*i + thresOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 13, out BUFC_buffer[i].threshold, name: "THRESHOLD")
                        .WithEnumField<DoubleWordRegister, BUFC_ThresholdMode>(13, 1, out BUFC_buffer[i].thresholdMode, name: "THRESHOLDMODE")
                        .WithReservedBits(14, 18)
                        .WithChangeCallback((_, __) => BUFC_buffer[i].UpdateThresholdFlag()));
                registerDictionary.Add(startOffset + blockSize*i + cmdOffset,
                    new DoubleWordRegister(this)
                        .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if(value) BUFC_buffer[i].Clear(); }, name: "CLEAR")
                        .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if(value) BUFC_buffer[i].Prefetch(); }, name: "PREFETCH")
                        .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if(value) BUFC_buffer[i].UpdateWriteStartOffset(); }, name: "UPDATEWRITESTART")
                        .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if(value) BUFC_buffer[i].RestoreWriteOffset(); }, name: "RESTOREWRITEOFFSET")
                        .WithReservedBits(4, 28));
                registerDictionary.Add(startOffset + blockSize*i + rData32Offset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => BUFC_buffer[i].ReadData32, name: "READDATA32"));
                registerDictionary.Add(startOffset + blockSize*i + wData32Offset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => BUFC_buffer[i].WriteData32 = (uint)value, name: "WRITEDATA32"));
                registerDictionary.Add(startOffset + blockSize*i + xWrite32Offset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => BUFC_buffer[i].XorWriteData32 = (uint)value, name: "XORWRITEDATA32"));
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildCyclicRedundancyCheckRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)CyclicRedundancyCheckRegisters.Control, new DoubleWordRegister(this)
                    .WithTaggedFlag("INPUTINV", 0)
                    .WithTaggedFlag("OUTPUTINV", 1)
                    .WithEnumField<DoubleWordRegister, CRC_CrcWidthMode>(2, 2, out CRC_crcWidthMode, name: "CRCWIDTH")
                    .WithReservedBits(4, 1)
                    .WithTaggedFlag("INPUTBITORDER", 5)
                    .WithFlag(6, out CRC_reverseCrcByteOrdering, name: "BYTEREVERSE")
                    .WithTaggedFlag("BITREVERSE", 7)
                    .WithValueField(8, 4, out CRC_crcBitsPerWord, name: "BITSPERWORD")
                    .WithTaggedFlag("PADCRCINPUT", 12)
                    .WithReservedBits(13, 19)
                },
            };
            
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildSynthesizerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)SynthesizerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithTaggedFlag("UNLOCKEDIF", 0)
                    .WithTaggedFlag("LOCKEDIF", 1)
                    .WithFlag(2, out SYNTH_readyInterrupt, name: "SYRDYIF")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("VCOHIGHIF", 4)
                    .WithTaggedFlag("VCOLOWIF", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("LOCNTDONEIF", 9)
                    .WithFlag(10, out SYNTH_calibrationDoneInterrupt, name: "FCALDONEIF")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)SynthesizerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithTaggedFlag("UNLOCKEDIEN", 0)
                    .WithTaggedFlag("LOCKEDIEN", 1)
                    .WithFlag(2, out SYNTH_readyInterruptEnable, name: "SYRDYIEN")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("VCOHIGHIEN", 4)
                    .WithTaggedFlag("VCOLOWIEN", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("LOCNTDONEIEN", 9)
                    .WithFlag(10, out SYNTH_calibrationDoneInterruptEnable, name: "FCALDONEIEN")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)SynthesizerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithTaggedFlag("UNLOCKEDSEQIF", 0)
                    .WithTaggedFlag("LOCKEDSEQIF", 1)
                    .WithFlag(2, out SYNTH_seqReadyInterrupt, name: "SYRDYSEQIF")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("VCOHIGHSEQIF", 4)
                    .WithTaggedFlag("VCOLOWSEQIF", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("LOCNTDONESEQIF", 9)
                    .WithFlag(10, out SYNTH_seqCalibrationDoneInterrupt, name: "FCALDONESEQIF")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)SynthesizerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithTaggedFlag("UNLOCKEDSEQIEN", 0)
                    .WithTaggedFlag("LOCKEDSEQIEN", 1)
                    .WithFlag(2, out SYNTH_seqReadyInterruptEnable, name: "SYRDYSEQIEN")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("VCOHIGHSEQIEN", 4)
                    .WithTaggedFlag("VCOLOWSEQIEN", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("LOCNTDONESEQIEN", 9)
                    .WithFlag(10, out SYNTH_seqCalibrationDoneInterruptEnable, name: "FCALDONESEQIEN")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)SynthesizerRegisters.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_Start(); }, name: "SYNTHSTART")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_Stop(); }, name: "SYNTHSTOP")
                    .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_EnableIf(); }, name: "ENABLEIF")
                    .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_DisableIf(); }, name: "DISABLEIF")
                    .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_CalibrationStart(); }, name: "CAPCALSTART")
                    .WithReservedBits(5, 4)
                    .WithTaggedFlag("VCOADDCAP", 9)
                    .WithTaggedFlag("VCOSUBCAP", 10)
                    .WithReservedBits(11, 21)
                },
                {(long)SynthesizerRegisters.Status, new DoubleWordRegister(this)
                    .WithTaggedFlag("INLOCK", 0)
                    .WithFlag(1, out SYNTH_ifFrequencyEnabled, FieldMode.Read, name: "IFFREQEN")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => (SYNTH_state == SYNTH_State.Ready), name: "SYNTHREADY")
                    .WithTaggedFlag("VCOFREQACTIVE", 3)
                    .WithTag("LMSSTATUS", 4, 13)
                    .WithReservedBits(17, 15)
                },
                // We currently store the logical channel in the channel spacing register for PTI/debug
                {(long)SynthesizerRegisters.ChannelSpacing, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => (ulong)Channel, writeCallback: (_, value) => { Channel = (int)value; }, name: "CHSP")
                    .WithReservedBits(16, 15)
                },
            };
            
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildRadioControllerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)RadioControllerRegisters.RXENSourceEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out RAC_softwareRxEnable, name: "SWRXEN")
                    .WithTaggedFlag("CHANNELBUSYEN", 8)
                    .WithTaggedFlag("TIMDETEN", 9)
                    .WithTaggedFlag("PREDETEN", 10)
                    .WithTaggedFlag("FRAMEDETEN", 11)
                    .WithTaggedFlag("DEMODRXREQEN", 12)
                    .WithTaggedFlag("PRSRXEN", 13)
                    .WithReservedBits(14, 18)
                    .WithWriteCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)RadioControllerRegisters.Status, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => RAC_RxEnableMask, name: "RXMASK")
                    .WithReservedBits(16, 3)
                    .WithFlag(19, out RAC_forceStateActive, FieldMode.Read, name: "FORCESTATEACTIVE")
                    .WithReservedBits(20, 2)
                    .WithFlag(22, out RAC_sequencerInSleeping, FieldMode.Read, name: "SEQSLEEPING")
                    .WithFlag(23, out RAC_sequencerInDeepSleep, FieldMode.Read, name: "SEQSLEEPDEEP")
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(24, 4, FieldMode.Read, valueProviderCallback: _ => RAC_currentRadioState, name: "STATE")
                    .WithFlag(28, out RAC_sequencerActive, FieldMode.Read, name: "SEQACTIVE")
                    .WithTaggedFlag("DEMODENS", 29)
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => RAC_TxEnable, name: "TXENS")
                    .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => RAC_RxEnable, name: "RXENS")
                },
                {(long)RadioControllerRegisters.Status1, new DoubleWordRegister(this)
                    .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: _ => RAC_TxEnableMask, name: "TXMASK")
                    .WithReservedBits(7, 25)
                }, 
                {(long)RadioControllerRegisters.Status2, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(0, 4, FieldMode.Read, valueProviderCallback: _ => RAC_previous1RadioState, name: "PREVSTATE1")
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(4, 4, FieldMode.Read, valueProviderCallback: _ => RAC_previous2RadioState, name: "PREVSTATE2")
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(8, 4, FieldMode.Read, valueProviderCallback: _ => RAC_previous3RadioState, name: "PREVSTATE3")
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(12, 4, FieldMode.Read, valueProviderCallback: _ => RAC_currentRadioState, name: "CURRSTATE")
                    .WithReservedBits(16, 16)
                }, 
                {(long)RadioControllerRegisters.Control, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_forceDisable, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine();} }, name: "FORCEDISABLE")
                    .WithTaggedFlag("PRSTXEN", 1)
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("PRSMODE", 3)
                    .WithTaggedFlag("FSMDEMODENSEARCH", 4)
                    .WithTaggedFlag("PRSCLR", 5)
                    .WithTaggedFlag("TXPOSTPONE", 6)
                    .WithTaggedFlag("ACTIVEPOL", 7)
                    .WithTaggedFlag("PAENPOL", 8)
                    .WithTaggedFlag("LNAENPOL", 9)
                    .WithTaggedFlag("PRSRXDIS", 10)
                    .WithTag("AHBSYNC0MODE", 11, 2)
                    .WithTaggedFlag("AHBSYNC0REQ", 13)
                    .WithTaggedFlag("AHBSYNC0ACK", 14)
                    .WithTaggedFlag("PRSFORCETX", 15)
                    .WithTaggedFlag("FSMRXABORTHW", 16)
                    .WithTag("FSMDEMODENWAIT", 17, 3)
                    .WithTaggedFlag("FSMDEMODEN", 21)
                    .WithTaggedFlag("FSMWRAPUPNEXTDIS", 22)
                    .WithTaggedFlag("HYDRARAMCLKDIS", 23)
                    .WithTaggedFlag("SEQRESET", 24)
                    .WithFlag(25, out RAC_exitShutdownDisable, FieldMode.Read, name: "EXITSHUTDOWNDIS")
                    .WithFlag(26, FieldMode.Set, writeCallback: (_, value) =>
                        {
                            if (value && sequencer.IsHalted)
                            {
                                sequencer.PC = sequencerConfig.BootAddress;
                                sequencer.IsHalted = false;
                                sequencer.Resume();
                                this.Log(LogLevel.Info, "Sequencer resumed, isHalted={0} SP={1:X} PC={2:X}.", sequencer.IsHalted, sequencer.SP, sequencer.PC);
                            }
                        }, name: "CPUWAITDIS")
                    .WithTaggedFlag("SEQCLKDIS", 27)
                    .WithTag("AHBSYNC1MODE", 28, 2)
                    .WithTaggedFlag("AHBSYNC1REQ", 30)
                    .WithTaggedFlag("AHBSYNC1ACK", 31)
                    .WithChangeCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)RadioControllerRegisters.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_TxEnable = true;} }, name: "TXEN")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.ForceTx);} }, name: "FORCETX")
                    .WithTaggedFlag("TXONCCA", 2)
                    .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_TxEnable = false;} }, name: "CLEARTXEN")
                    .WithReservedBits(4, 1)
                    .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) {RAC_TxEnable = false; RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxDisable);} }, name: "TXDIS")                    
                    .WithReservedBits(6, 1)
                    .WithFlag(7, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxCalibration);} }, name: "RXCAL")
                    .WithFlag(8, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxDisable);} }, name: "RXDIS")
                    .WithReservedBits(9, 1)
                    .WithTaggedFlag("FRCWR", 10)
                    .WithTaggedFlag("FRCRD", 11)
                    .WithTaggedFlag("PAENSET", 12)
                    .WithTaggedFlag("PAENCLEAR", 13)
                    .WithTaggedFlag("LNAENSET", 14)
                    .WithTaggedFlag("LNAENCLEAR", 15)
                    .WithReservedBits(16, 14)
                    .WithTaggedFlag("DEMODENSET", 30)
                    .WithTaggedFlag("DEMODENCLEAR", 31)
                },
                {(long)RadioControllerRegisters.ForceStateTransition, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(0, 3, out RAC_forceStateTransition, writeCallback: (_, __) =>
                        {
                            RAC_forceStateActive.Value = true;
                        }, name: "FORCESTATE")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)RadioControllerRegisters.Em1pControlAndStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { RAC_em1pAckPending = true; }, name: "RADIOEM1PMODE")
                    .WithTaggedFlag("RADIOEM1PMODE", 1)
                    .WithReservedBits(2, 2)
                    .WithTaggedFlag("MCUEM1PMODE", 4)
                    .WithTaggedFlag("MCUEM1PDISSWREQ", 5)
                    .WithReservedBits(6, 10)
                    .WithTaggedFlag("RADIOEM1PREQ", 16)
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => 
                        {
                            var retValue = RAC_em1pAckPending;
                            RAC_em1pAckPending = false;
                            return retValue;
                        }, name: "RADIOEM1PACK")
                    .WithTaggedFlag("RADIOEM1PHWREQ", 18)
                    .WithReservedBits(19, 13)
                },
                {(long)RadioControllerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_radioStateChangeInterrupt, name: "STATECHANGEIF")
                    .WithFlag(1, out RAC_stimerCompareEventInterrupt, name: "STIMCMPEVIF")
                    .WithTaggedFlag("SEQLOCKUPIF", 2)
                    .WithTaggedFlag("SEQRESETREQIF", 3)
                    .WithTaggedFlag("FSWLOCKUPIF", 4)
                    .WithTaggedFlag("FSWRESETREQIF", 5)
                    .WithTaggedFlag("PREREGBYPOUTIF", 6)
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 24, out RAC_mainCoreSeqInterrupts, name: "SEQIF")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RadioControllerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_radioStateChangeInterruptEnable, name: "STATECHANGEIEN")
                    .WithFlag(1, out RAC_stimerCompareEventInterruptEnable, name: "STIMCMPEVIEN")
                    .WithTaggedFlag("SEQLOCKUPIEN", 2)
                    .WithTaggedFlag("SEQRESETREQIEN", 3)
                    .WithTaggedFlag("FSWLOCKUPIEN", 4)
                    .WithTaggedFlag("FSWRESETREQIEN", 5)
                    .WithTaggedFlag("PREREGBYPOUTIEN", 6)
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 24, out RAC_mainCoreSeqInterruptsEnable, name: "SEQIEN")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RadioControllerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_seqRadioStateChangeInterrupt, name: "STATECHANGESEQIF")
                    .WithFlag(1, out RAC_seqStimerCompareEventInterrupt, name: "STIMCMPEVSEQIF")
                    .WithFlag(2, out RAC_seqDemodRxRequestClearInterrupt, name: "DEMODRXREQCLRSEQIF")
                    .WithFlag(3, out RAC_seqPrsEventInterrupt, name: "PRSEVENTSEQIF")
                    .WithReservedBits(4, 2)
                    .WithTaggedFlag("PREREGBYPOUTSEQIF", 6)
                    .WithReservedBits(7, 9)
                    .WithFlag(16, out RAC_seqStateOffInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.OffExit);}}, name: "STATEOFFSEQIF")
                    .WithFlag(17, out RAC_seqStateRxWarmInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxWarmExit);}}, name: "STATERXWARMSEQIF")
                    .WithFlag(18, out RAC_seqStateRxSearchInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxSearchExit);}}, name: "STATERXSEARCHSEQIF")
                    .WithFlag(19, out RAC_seqStateRxFrameInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxFrameExit);}}, name: "STATERXFRAMESEQIF")
                    .WithFlag(20, out RAC_seqStateRxWrapUpInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxWrapUpExit);}}, name: "STATERXWRAPUPSEQIF")
                    .WithFlag(21, out RAC_seqStateTxWarmInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxWarmExit);}}, name: "STATETXWARMSEQIF")
                    .WithFlag(22, out RAC_seqStateTxInterrupt, name: "STATETXSEQIF")
                    .WithFlag(23, out RAC_seqStateTxWrapUpInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxWrapUpExit);}}, name: "STATETXWRAPUPSEQIF")
                    .WithFlag(24, out RAC_seqStateShutDownInterrupt, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.ShutdownExit);}}, name: "STATESHUTDOWNSEQIF")
                    .WithReservedBits(25, 3)
                    .WithTaggedFlag("BRERRAHB2AHB0SEQIF", 28)
                    .WithTaggedFlag("BRERRAHB2AHB1SEQIF", 29)
                    .WithTaggedFlag("BRERRLPW2HOSTSEQIF", 30)
                    .WithTaggedFlag("BRERRLPW2SYSMBSEQIF", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RadioControllerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_seqRadioStateChangeInterruptEnable, name: "STATECHANGESEQIEN")
                    .WithFlag(1, out RAC_seqStimerCompareEventInterruptEnable, name: "STIMCMPEVSEQIEN")
                    .WithFlag(2, out RAC_seqDemodRxRequestClearInterruptEnable, name: "DEMODRXREQCLRSEQIEN")
                    .WithFlag(3, out RAC_seqPrsEventInterruptEnable, name: "PRSEVENTSEQIEN")
                    .WithReservedBits(4, 2)
                    .WithTaggedFlag("PREREGBYPOUTSEQIEN", 6)
                    .WithReservedBits(7, 9)
                    .WithFlag(16, out RAC_seqStateOffInterruptEnable, name: "STATEOFFIEN")
                    .WithFlag(17, out RAC_seqStateRxWarmInterruptEnable, name: "STATERXWARMIEN")
                    .WithFlag(18, out RAC_seqStateRxSearchInterruptEnable, name: "STATERXSEARCHIEN")
                    .WithFlag(19, out RAC_seqStateRxFrameInterruptEnable, name: "STATERXFRAMEIEN")
                    .WithFlag(20, out RAC_seqStateRxWrapUpInterruptEnable, name: "STATERXWRAPUPSEQIEN")
                    .WithFlag(21, out RAC_seqStateTxWarmInterruptEnable, name: "STATETXWARMIEN")
                    .WithFlag(22, out RAC_seqStateTxInterruptEnable, name: "STATETXIEN")
                    .WithFlag(23, out RAC_seqStateTxWrapUpInterruptEnable, name: "STATETXWRAPUPSEQIEN")
                    .WithFlag(24, out RAC_seqStateShutDownInterruptEnable, name: "STATESHUTDOWNIEN")
                    .WithReservedBits(25, 3)
                    .WithTaggedFlag("BRERRAHB2AHB0SEQIEN", 28)
                    .WithTaggedFlag("BRERRAHB2AHB1SEQIEN", 29)
                    .WithTaggedFlag("BRERRLPW2HOSTSEQIEN", 30)
                    .WithTaggedFlag("BRERRLPW2SYSMBSEQIEN", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

                // TODO: add FSW_IF and FSW_IEN when we introduce the FSW core

                {(long)RadioControllerRegisters.TxWrapUpNext, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(0, 4, out RAC_txWrapUpNext, name: "TXWRAPUPNEXT")
                    .WithReservedBits(4, 28)
                }, 
                {(long)RadioControllerRegisters.RxWrapUpNext, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(0, 4, out RAC_rxWrapUpNext, name: "RXWRAPUPNEXT")
                    .WithReservedBits(4, 28)
                }, 
                {(long)RadioControllerRegisters.SequencerEndControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithFlag(16, out RAC_seqStateOffEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.OffExit);}}, name: "STATEOFFSEQEND")
                    .WithFlag(17, out RAC_seqStateRxWarmEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxWarmExit);}}, name: "STATERXWARMSEQEND")
                    .WithFlag(18, out RAC_seqStateRxSearchEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxSearchExit);}}, name: "STATERXSEARCHSEQEND")
                    .WithFlag(19, out RAC_seqStateRxFrameEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxFrameExit);}}, name: "STATERXFRAMESEQEND")
                    .WithFlag(20, out RAC_seqStateRxWrapUpEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxWrapUpExit);}}, name: "STATERXWRAPUPSEQEND")
                    .WithFlag(21, out RAC_seqStateTxWarmEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxWarmExit);}}, name: "STATETXWARMSEQEND")
                    .WithFlag(22, out RAC_seqStateTxEnd, name: "STATETXSEQEND")
                    .WithFlag(23, out RAC_seqStateTxWrapUpEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxWrapUpExit);}}, name: "STATETXWRAPUPSEQEND")
                    .WithFlag(24, out RAC_seqStateShutdownEnd, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.ShutdownExit);}}, name: "STATESHUTDOWNSEQEND")
                    .WithReservedBits(25, 7)
                }, 
                {(long)RadioControllerRegisters.SequencerEndEnableControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithFlag(16, out RAC_seqStateOffEndEnable, name: "STATEOFFSEQENDEN")
                    .WithFlag(17, out RAC_seqStateRxWarmEndEnable, name: "STATERXWARMSEQENDEN")
                    .WithFlag(18, out RAC_seqStateRxSearchEndEnable, name: "STATERXSEARCHSEQENDEN")
                    .WithFlag(19, out RAC_seqStateRxFrameEndEnable, name: "STATERXFRAMESEQENDEN")
                    .WithFlag(20, out RAC_seqStateRxWrapUpEndEnable, name: "STATERXWRAPUPSEQENDEN")
                    .WithFlag(21, out RAC_seqStateTxWarmEndEnable, name: "STATETXWARMSEQENDEN")
                    .WithFlag(22, out RAC_seqStateTxEndEnable, name: "STATETXSEQENDEN")
                    .WithFlag(23, out RAC_seqStateTxWrapUpEndEnable, name: "STATETXWRAPUPSEQENDEN")
                    .WithFlag(24, out RAC_seqStateShutdownEndEnable, name: "STATESHUTDOWNSEQENDEN")
                    .WithReservedBits(25, 7)
                }, 
                {(long)RadioControllerRegisters.SequencerTimerValue, new DoubleWordRegister(this)
                    .WithTag("STIMER", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)RadioControllerRegisters.SequencerTimerCompareValue, new DoubleWordRegister(this)
                    .WithTag("STIMERCOMP", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)RadioControllerRegisters.SequencerControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("COMPACT", 0)
                    .WithTag("COMPINVALMODE", 1, 2)
                    .WithTaggedFlag("RELATIVE", 3)
                    .WithTaggedFlag("STIMERALWAYSRUN", 4)
                    .WithTaggedFlag("STIMERDEBUGRUN", 5)
                    .WithTaggedFlag("STATEDEBUGRUN", 6)
                    .WithReservedBits(7, 17)
                    .WithTag("SWIRQ", 24, 2)
                    .WithReservedBits(26, 6)
                },
                {(long)RadioControllerRegisters.SequencerPrescaler, new DoubleWordRegister(this, 0x7)
                    .WithTag("STIMERPRESC", 0, 7)
                    .WithReservedBits(7, 25)
                },
                {(long)RadioControllerRegisters.Storage0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_seqStorage[0], name: "SR0")
                },
                {(long)RadioControllerRegisters.Storage1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_seqStorage[1], name: "SR1")
                },
                {(long)RadioControllerRegisters.Storage2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_seqStorage[2], name: "SR2")
                },
                {(long)RadioControllerRegisters.Storage3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_seqStorage[3], name: "SR3")
                },
                {(long)RadioControllerRegisters.RadioFrequencyStatus, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_paRampingDone, FieldMode.Read, name: "MODRAMPUPDONE")
                    .WithReservedBits(1, 31)
                },
                {(long)RadioControllerRegisters.PowerAmplifierEnableControl, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => { return RAC_PaOutputLevelRamping; }, writeCallback: (_, value) => { RAC_PaOutputLevelRamping = value; }, name: "PARAMP")
                    .WithTaggedFlag("INVRAMPCLK", 1)
                    .WithTaggedFlag("DIV2RAMPCLK", 2)
                    .WithTaggedFlag("RSTDIV2RAMPCLK", 3)
                    .WithTaggedFlag("INVRFCLKTXDAC", 4)
                    .WithReservedBits(5, 27)
                },
                {(long)RadioControllerRegisters.Scratch0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[0], name: "SCRATCH0")
                },
                {(long)RadioControllerRegisters.Scratch1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[1], name: "SCRATCH1")
                },
                {(long)RadioControllerRegisters.Scratch2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[2], name: "SCRATCH2")
                },
                {(long)RadioControllerRegisters.Scratch3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[3], name: "SCRATCH3")
                },
                {(long)RadioControllerRegisters.Scratch4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[4], name: "SCRATCH4")
                },
                {(long)RadioControllerRegisters.Scratch5, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[5], name: "SCRATCH5")
                },
                {(long)RadioControllerRegisters.Scratch6, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[6], name: "SCRATCH6")
                },
                {(long)RadioControllerRegisters.Scratch7, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RAC_scratch[7], name: "SCRATCH7")
                },
                {(long)RadioControllerRegisters.RadioFrequencyLock0, new DoubleWordRegister(this, 0x80000000)
                    .WithTag("SYNTHLODIVFREQCTRL", 0, 10)
                    .WithReservedBits(10, 11)
                    .WithTaggedFlag("MODEMHADM", 21)
                    .WithTaggedFlag("FRCCONVMODE", 22)
                    .WithTaggedFlag("FRCPAUSING", 23)
                    .WithTaggedFlag("MODEMANTSWENABLE", 24)
                    .WithTaggedFlag("MODEMLRBLE", 25)
                    .WithTaggedFlag("MODEMDSSS", 26)
                    .WithReservedBits(27, 1)
                    .WithTaggedFlag("MODEMMODFORMAT", 28)
                    .WithTaggedFlag("MODEMDUALSYNC", 29)
                    .WithTaggedFlag("MODEMANTDIVMODE", 30)
                    .WithFlag(31, out RAC_unlocked, name: "UNLOCKED")
                },
                {(long)RadioControllerRegisters.RadioFrequencyLock1, new DoubleWordRegister(this, 0x00000FFF)
                    .WithTag("TX0DBMPOWERLIMIT", 0, 5)
                    .WithTag("TX10DBMPOWERLIMIT", 5, 7)
                    .WithReservedBits(12, 20)
                },
                {(long)RadioControllerRegisters.DigitalConverterControl, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) RAC_dcCalDone = true; }, name: "DCRUN")
                    .WithTaggedFlag("DCBUSY", 1)
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => RAC_dcCalDone, name: "DCDONE")
                    .WithTag("MAXITERS", 3, 4)
                    .WithTag("SETTLECNT", 7, 4)
                    .WithTaggedFlag("DCPKDINV", 11)
                    .WithTaggedFlag("DCCTRLOX", 12)
                    .WithReservedBits(13, 7)
                    .WithTag("OXICAL", 20, 6)
                    .WithTag("OXQCAL", 26, 6)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildProtocolTimerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)ProtocolTimerRegisters.Control, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithTaggedFlag("DEBUGRUN", 1)
                    .WithTaggedFlag("DMACLRACT", 2)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("OSMEN", 4)
                    .WithFlag(5, out PROTIMER_zeroStartEnable, name: "ZEROSTARTEN")
                    .WithEnumField<DoubleWordRegister, PROTIMER_PreCounterSource>(6, 2, out PROTIMER_preCounterSource, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case PROTIMER_PreCounterSource.Disabled:
                                    PROTIMER_Enabled = false;
                                    break;
                                case PROTIMER_PreCounterSource.Clock:
                                    // wait for the start command to actually start the proTimer
                                    break;
                                default:
                                    PROTIMER_Enabled = false;
                                    this.Log(LogLevel.Error, "Invalid PRECNTSRC value");
                                    break;
                            }
                        }, name: "PRECNTSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_BaseCounterSource>(8, 2, out PROTIMER_baseCounterSource, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case PROTIMER_BaseCounterSource.Unused0:
                                case PROTIMER_BaseCounterSource.Unused1:
                                    this.Log(LogLevel.Error, "Invalid BASECNTSRC value");
                                    break;
                            }
                        }, name: "BASECNTSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_WrapCounterSource>(10, 2, out PROTIMER_wrapCounterSource, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case PROTIMER_WrapCounterSource.Unused:
                                    this.Log(LogLevel.Error, "Invalid WRAPCNTSRC value");
                                    break;
                            }
                        }, name: "WRAPCNTSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(12, 2, out PROTIMER_timeoutCounter[0].source, name: "TOUT0SRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(14, 2, out PROTIMER_timeoutCounter[0].syncSource, name: "TOUT0SYNCSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(16, 2, out PROTIMER_timeoutCounter[1].source, name: "TOUT1SRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(18, 2, out PROTIMER_timeoutCounter[1].syncSource, name: "TOUT1SYNCSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(20, 2, out PROTIMER_timeoutCounter[2].source, name: "TOUT2SRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(22, 2, out PROTIMER_timeoutCounter[2].syncSource, name: "TOUT2SYNCSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_RepeatMode>(24, 1, out PROTIMER_timeoutCounter[0].mode, name: "TOUT0MODE")
                    .WithEnumField<DoubleWordRegister, PROTIMER_RepeatMode>(25, 1, out PROTIMER_timeoutCounter[1].mode, name: "TOUT1MODE")
                    .WithEnumField<DoubleWordRegister, PROTIMER_RepeatMode>(26, 1, out PROTIMER_timeoutCounter[2].mode, name: "TOUT2MODE")
                    .WithTag("BOWAITINACTLSB", 27, 2)
                    .WithReservedBits(29, 3)
                },
                {(long)ProtocolTimerRegisters.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => 
                        { 
                            if(PROTIMER_preCounterSource.Value == PROTIMER_PreCounterSource.Clock && value) 
                            { 
                                PROTIMER_Enabled = true;
                            } 
                        }, name: "START")
                    .WithTaggedFlag("RTCSYNCSTART", 1)
                    .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if(value) { PROTIMER_Enabled = false;} }, name: "STOP")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[0].Start(); }, name: "TOUT0START")
                    .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[0].Stop(); }, name: "TOUT0STOP")
                    .WithFlag(6, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[1].Start(); }, name: "TOUT1START")
                    .WithFlag(7, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[1].Stop(); }, name: "TOUT1STOP")
                    .WithFlag(8, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[2].Start(); }, name: "TOUT2START")
                    .WithFlag(9, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[2].Stop(); }, name: "TOUT2STOP")
                    .WithFlag(10, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Idle; RAC_UpdateRadioStateMachine(); }, name: "FORCETXIDLE")
                    .WithFlag(11, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle; RAC_UpdateRadioStateMachine(); }, name: "FORCERXIDLE")
                    .WithTaggedFlag("FORCERXRX", 12)
                    .WithReservedBits(13, 3)
                    .WithFlag(16, FieldMode.Set, writeCallback: (_, value) => { if (value) PROTIMER_ListenBeforeTalkStartCommand(); }, name: "LBTSTART")
                    .WithFlag(17, FieldMode.Set, writeCallback: (_, value) => { if (value) PROTIMER_ListenBeforeTalkPauseCommand(); }, name: "LBTPAUSE")
                    .WithFlag(18, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_ListenBeforeTalkStopCommand(); }, name: "LBTSTOP")
                    .WithTaggedFlag("WRAPCHECK", 19)
                    .WithReservedBits(20, 12)
                    .WithWriteCallback((_, __) => { PROTIMER_HandleChangedParams(); UpdateInterrupts(); })
                },
                {(long)ProtocolTimerRegisters.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => PROTIMER_Enabled, name: "RUNNING")
                    .WithFlag(1, out PROTIMER_listenBeforeTalkSync, FieldMode.Read, name: "LBTSYNC")
                    .WithFlag(2, out PROTIMER_listenBeforeTalkRunning, FieldMode.Read, name: "LBTRUNNING")
                    .WithFlag(3, out PROTIMER_listenBeforeTalkPaused, FieldMode.Read, name: "LBTPAUSED")
                    .WithFlag(4, out PROTIMER_timeoutCounter[0].running, FieldMode.Read, name: "TOUT0RUNNING")
                    .WithFlag(5, out PROTIMER_timeoutCounter[0].synchronizing, FieldMode.Read, name: "TOUT0SYNC")
                    .WithFlag(6, out PROTIMER_timeoutCounter[1].running, FieldMode.Read, name: "TOUT1RUNNING")
                    .WithFlag(7, out PROTIMER_timeoutCounter[1].synchronizing, FieldMode.Read, name: "TOUT1SYNC")
                    .WithFlag(8, out PROTIMER_timeoutCounter[2].running, FieldMode.Read, name: "TOUT2RUNNING")
                    .WithFlag(9, out PROTIMER_timeoutCounter[2].synchronizing, FieldMode.Read, name: "TOUT2SYNC")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[0].captureValid, FieldMode.Read, name: "ICV0")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[1].captureValid, FieldMode.Read, name: "ICV1")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[2].captureValid, FieldMode.Read, name: "ICV2")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[3].captureValid, FieldMode.Read, name: "ICV3")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[4].captureValid, FieldMode.Read, name: "ICV4")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[5].captureValid, FieldMode.Read, name: "ICV5")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[6].captureValid, FieldMode.Read, name: "ICV6")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[7].captureValid, FieldMode.Read, name: "ICV7")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[8].captureValid, FieldMode.Read, name: "ICV8")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[9].captureValid, FieldMode.Read, name: "ICV9")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[10].captureValid, FieldMode.Read, name: "ICV10")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[11].captureValid, FieldMode.Read, name: "ICV11")
                    .WithReservedBits(22, 10)
                },
                {(long)ProtocolTimerRegisters.PreCounterValue, new DoubleWordRegister(this)
                    // We don't tick the PRECNT value, so just always return 0
                    .WithValueField(0, 16, valueProviderCallback: _ => 0, name: "PRECNT") 
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.BaseCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => PROTIMER_BaseCounterValue, writeCallback: (_, value) => PROTIMER_BaseCounterValue = (uint)value, name: "BASECNT")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.WrapCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  PROTIMER_WrapCounterValue, writeCallback: (_, value) => PROTIMER_WrapCounterValue = (uint)value, name: "WRAPCNT")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.LatchedPreCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 
                        {
                            PROTIMER_latchedBaseCounterValue = PROTIMER_BaseCounterValue;
                            PROTIMER_latchedWrapCounterValue = PROTIMER_WrapCounterValue;
                            // We don't tick the PRECNT value, so just always return 0
                            return 0;
                        }, name: "LPRECNT") 
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.LatchedBaseCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_latchedBaseCounterValue, name: "LBASECNT")
                },
                {(long)ProtocolTimerRegisters.LatchedWrapCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_latchedWrapCounterValue, name: "LWRAPCNT")
                },
                {(long)ProtocolTimerRegisters.LatchedPreCounterValueSeq, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 
                        {
                            PROTIMER_seqLatchedBaseCounterValue = PROTIMER_BaseCounterValue;
                            PROTIMER_seqLatchedWrapCounterValue = PROTIMER_WrapCounterValue;
                            // We don't tick the PRECNT value, so just always return 0
                            return 0;
                        }, name: "LPRECNTSEQ") 
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.LatchedBaseCounterValueSeq, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_seqLatchedBaseCounterValue, name: "LBASECNTSEQ")
                },
                {(long)ProtocolTimerRegisters.LatchedWrapCounterValueSeq, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_seqLatchedWrapCounterValue, name: "LWRAPCNTSEQ")
                },
                {(long)ProtocolTimerRegisters.PreCounterTopAdjustValue, new DoubleWordRegister(this)
                    .WithTag("PRECNTTOPADJ", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.PreCounterTopValue, new DoubleWordRegister(this, 0xFFFF0000)
                    .WithValueField(0, 16, out PROTIMER_preCounterTopFractional, name: "PRECNTTOPFRAC")
                    .WithValueField(16, 16, out PROTIMER_preCounterTopInteger, name: "PRECNTTOP")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.BaseCounterTopValue, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 32, out PROTIMER_baseCounterTop, name: "BASECNTTOP")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.WrapCounterTopValue, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 32, out PROTIMER_wrapCounterTop, name: "WRAPCNTTOP")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.Timeout0Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].preCounter, name: "TOUT0PCNT")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].counter, name: "TOUT0CNT")
                },
                {(long)ProtocolTimerRegisters.Timeout0CounterTop, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].preCounterTop, name: "TOUT0CNTTOP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].counterTop, name: "TOUT0PCNTTOP")
                },
                {(long)ProtocolTimerRegisters.Timeout0Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].preCounterCompare, name: "TOUT0PCNTCOMP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].counterCompare, name: "TOUT0CNTCOMP")
                },
                {(long)ProtocolTimerRegisters.Timeout1Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].preCounter, name: "TOUT1PCNT")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].counter, name: "TOUT1CNT")
                },
                {(long)ProtocolTimerRegisters.Timeout1CounterTop, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].preCounterTop, name: "TOUT1CNTTOP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].counterTop, name: "TOUT1PCNTTOP")
                },
                {(long)ProtocolTimerRegisters.Timeout1Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].preCounterCompare, name: "TOUT1PCNTCOMP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].counterCompare, name: "TOUT1CNTCOMP")
                },
                {(long)ProtocolTimerRegisters.Timeout2Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[2].preCounter, name: "TOUT2PCNT")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[2].counter, name: "TOUT2CNT")
                },
                {(long)ProtocolTimerRegisters.Timeout2CounterTop, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[2].preCounterTop, name: "TOUT2CNTTOP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[2].counterTop, name: "TOUT2PCNTTOP")
                },
                {(long)ProtocolTimerRegisters.Timeout2Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[2].preCounterCompare, name: "TOUT2PCNTCOMP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[2].counterCompare, name: "TOUT2CNTCOMP")
                },
                {(long)ProtocolTimerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].interrupt, name: "CC0IF")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].interrupt, name: "CC1IF")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].interrupt, name: "CC2IF")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].interrupt, name: "CC3IF")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].interrupt, name: "CC4IF")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].interrupt, name: "CC5IF")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].interrupt, name: "CC6IF")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].interrupt, name: "CC7IF")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].interrupt, name: "CC8IF")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].interrupt, name: "CC9IF")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].interrupt, name: "CC10IF")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].interrupt, name: "CC11IF")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].overflowInterrupt, name: "COF0IF")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].overflowInterrupt, name: "COF1IF")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].overflowInterrupt, name: "COF2IF")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].overflowInterrupt, name: "COF3IF")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].overflowInterrupt, name: "COF4IF")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].overflowInterrupt, name: "COF5IF")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].overflowInterrupt, name: "COF6IF")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].overflowInterrupt, name: "COF7IF")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].overflowInterrupt, name: "COF8IF")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].overflowInterrupt, name: "COF9IF")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].overflowInterrupt, name: "COF10IF")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].overflowInterrupt, name: "COF11IF")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].underflowInterrupt, name: "TOUT0IF")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].underflowInterrupt, name: "TOUT1IF")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].underflowInterrupt, name: "TOUT2IF")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].matchInterrupt, name: "TOUT0MATCHIF")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].matchInterrupt, name: "TOUT1MATCHIF")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].matchInterrupt, name: "TOUT2MATCHIF")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].interruptEnable, name: "CC0IEN")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].interruptEnable, name: "CC1IEN")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].interruptEnable, name: "CC2IEN")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].interruptEnable, name: "CC3IEN")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].interruptEnable, name: "CC4IEN")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].interruptEnable, name: "CC5IEN")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].interruptEnable, name: "CC6IEN")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].interruptEnable, name: "CC7IEN")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].interruptEnable, name: "CC8IEN")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].interruptEnable, name: "CC9IEN")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].interruptEnable, name: "CC10IEN")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].interruptEnable, name: "CC11IEN")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].overflowInterruptEnable, name: "COF0IEN")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].overflowInterruptEnable, name: "COF1IEN")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].overflowInterruptEnable, name: "COF2IEN")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].overflowInterruptEnable, name: "COF3IEN")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].overflowInterruptEnable, name: "COF4IEN")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].overflowInterruptEnable, name: "COF5IEN")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].overflowInterruptEnable, name: "COF6IEN")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].overflowInterruptEnable, name: "COF7IEN")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].overflowInterruptEnable, name: "COF8IEN")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].overflowInterruptEnable, name: "COF9IEN")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].overflowInterruptEnable, name: "COF10IEN")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].overflowInterruptEnable, name: "COF11IEN")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].underflowInterruptEnable, name: "TOUT0IEN")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].underflowInterruptEnable, name: "TOUT1IEN")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].underflowInterruptEnable, name: "TOUT2IEN")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].matchInterruptEnable, name: "TOUT0MATCHIEN")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].matchInterruptEnable, name: "TOUT1MATCHIEN")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].matchInterruptEnable, name: "TOUT2MATCHIEN")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.InterruptFlags2, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_preCounterOverflowInterrupt, name: "PRECNTOFIF")
                    .WithFlag(1, out PROTIMER_baseCounterOverflowInterrupt, name: "BASECNTOFIF")
                    .WithFlag(2, out PROTIMER_wrapCounterOverflowInterrupt, name: "WRAPCNTOFIF")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out PROTIMER_listenBeforeTalkSuccessInterrupt, name: "LBTSUCCESSIF")
                    .WithFlag(27, out PROTIMER_listenBeforeTalkFailureInterrupt, name: "LBTFAILUREIF")
                    .WithTaggedFlag("LBTPAUSEDIF", 28)
                    .WithFlag(29, out PROTIMER_listenBeforeTalkRetryInterrupt, name: "LBTRETRYIF")
                    .WithTaggedFlag("RTCCSYNCHEDIF", 30)
                    .WithFlag(31, out PROTIMER_listenBeforeTalkTimeoutCounterMatchInterrupt, name: "TOUT0MATCHLBTIF")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.InterruptEnable2, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_preCounterOverflowInterruptEnable, name: "PRECNTOFIEN")
                    .WithFlag(1, out PROTIMER_baseCounterOverflowInterruptEnable, name: "BASECNTOFIEN")
                    .WithFlag(2, out PROTIMER_wrapCounterOverflowInterruptEnable, name: "WRAPCNTOFIEN")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out PROTIMER_listenBeforeTalkSuccessInterruptEnable, name: "LBTSUCCESSIEN")
                    .WithFlag(27, out PROTIMER_listenBeforeTalkFailureInterruptEnable, name: "LBTFAILUREIEN")
                    .WithTaggedFlag("LBTPAUSEDIEN", 28)
                    .WithFlag(29, out PROTIMER_listenBeforeTalkRetryInterruptEnable, name: "LBTRETRYIEN")
                    .WithTaggedFlag("RTCCSYNCHEDIEN", 30)
                    .WithFlag(31, out PROTIMER_listenBeforeTalkTimeoutCounterMatchInterruptEnable, name: "TOUT0MATCHLBTIEN")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].seqInterrupt, name: "CC0SEQIF")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].seqInterrupt, name: "CC1SEQIF")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].seqInterrupt, name: "CC2SEQIF")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].seqInterrupt, name: "CC3SEQIF")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].seqInterrupt, name: "CC4SEQIF")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].seqInterrupt, name: "CC5SEQIF")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].seqInterrupt, name: "CC6SEQIF")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].seqInterrupt, name: "CC7SEQIF")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].seqInterrupt, name: "CC8SEQIF")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].seqInterrupt, name: "CC9SEQIF")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].seqInterrupt, name: "CC10SEQIF")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].seqInterrupt, name: "CC11SEQIF")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].seqOverflowInterrupt, name: "COF0SEQIF")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].seqOverflowInterrupt, name: "COF1SEQIF")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].seqOverflowInterrupt, name: "COF2SEQIF")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].seqOverflowInterrupt, name: "COF3SEQIF")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].seqOverflowInterrupt, name: "COF4SEQIF")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].seqOverflowInterrupt, name: "COF5SEQIF")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].seqOverflowInterrupt, name: "COF6SEQIF")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].seqOverflowInterrupt, name: "COF7SEQIF")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].seqOverflowInterrupt, name: "COF8SEQIF")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].seqOverflowInterrupt, name: "COF9SEQIF")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].seqOverflowInterrupt, name: "COF10SEQIF")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].seqOverflowInterrupt, name: "COF11SEQIF")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].seqUnderflowInterrupt, name: "TOUT0SEQIF")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].seqUnderflowInterrupt, name: "TOUT1SEQIF")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].seqUnderflowInterrupt, name: "TOUT2SEQIF")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].seqMatchInterrupt, name: "TOUT0MATCHSEQIF")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].seqMatchInterrupt, name: "TOUT1MATCHSEQIF")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].seqMatchInterrupt, name: "TOUT2MATCHSEQIF")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].seqInterruptEnable, name: "CC0SEQIEN")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].seqInterruptEnable, name: "CC1SEQIEN")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].seqInterruptEnable, name: "CC2SEQIEN")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].seqInterruptEnable, name: "CC3SEQIEN")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].seqInterruptEnable, name: "CC4SEQIEN")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].seqInterruptEnable, name: "CC5SEQIEN")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].seqInterruptEnable, name: "CC6SEQIEN")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].seqInterruptEnable, name: "CC7SEQIEN")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].seqInterruptEnable, name: "CC8SEQIEN")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].seqInterruptEnable, name: "CC9SEQIEN")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].seqInterruptEnable, name: "CC10SEQIEN")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].seqInterruptEnable, name: "CC11SEQIEN")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].seqOverflowInterruptEnable, name: "COF0SEQIEN")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].seqOverflowInterruptEnable, name: "COF1SEQIEN")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].seqOverflowInterruptEnable, name: "COF2SEQIEN")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].seqOverflowInterruptEnable, name: "COF3SEQIEN")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].seqOverflowInterruptEnable, name: "COF4SEQIEN")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].seqOverflowInterruptEnable, name: "COF5SEQIEN")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].seqOverflowInterruptEnable, name: "COF6SEQIEN")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].seqOverflowInterruptEnable, name: "COF7SEQIEN")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].seqOverflowInterruptEnable, name: "COF8SEQIEN")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].seqOverflowInterruptEnable, name: "COF9SEQIEN")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].seqOverflowInterruptEnable, name: "COF10SEQIEN")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].seqOverflowInterruptEnable, name: "COF11SEQIEN")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].seqUnderflowInterruptEnable, name: "TOUT0SEQIEN")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].seqUnderflowInterruptEnable, name: "TOUT1SEQIEN")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].seqUnderflowInterruptEnable, name: "TOUT2SEQIEN")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].seqMatchInterruptEnable, name: "TOUT0MATCHSEQIEN")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].seqMatchInterruptEnable, name: "TOUT1MATCHSEQIEN")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].seqMatchInterruptEnable, name: "TOUT2MATCHSEQIEN")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.SequencerInterruptFlags2, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_seqPreCounterOverflowInterrupt, name: "PRECNTOFSEQIF")
                    .WithFlag(1, out PROTIMER_seqBaseCounterOverflowInterrupt, name: "BASECNTOFSEQIF")
                    .WithFlag(2, out PROTIMER_seqWrapCounterOverflowInterrupt, name: "WRAPCNTOFSEQIF")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out PROTIMER_seqListenBeforeTalkSuccessInterrupt, name: "LBTSUCCESSSEQIF")
                    .WithFlag(27, out PROTIMER_seqListenBeforeTalkFailureInterrupt, name: "LBTFAILURESEQIF")
                    .WithTaggedFlag("LBTPAUSEDSEQIF", 28)
                    .WithFlag(29, out PROTIMER_seqListenBeforeTalkRetryInterrupt, name: "LBTRETRYSEQIF")
                    .WithTaggedFlag("RTCCSYNCHEDSEQIF", 30)
                    .WithFlag(31, out PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterrupt, name: "TOUT0MATCHLBTSEQIF")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.SequencerInterruptEnable2, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_seqPreCounterOverflowInterruptEnable, name: "PRECNTOFSEQIEN")
                    .WithFlag(1, out PROTIMER_seqBaseCounterOverflowInterruptEnable, name: "BASECNTOFSEQIEN")
                    .WithFlag(2, out PROTIMER_seqWrapCounterOverflowInterruptEnable, name: "WRAPCNTOFSEQIEN")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out PROTIMER_seqListenBeforeTalkSuccessInterruptEnable, name: "LBTSUCCESSSEQIEN")
                    .WithFlag(27, out PROTIMER_seqListenBeforeTalkFailureInterruptEnable, name: "LBTFAILURESEQIEN")
                    .WithTaggedFlag("LBTPAUSEDSEQIEN", 28)
                    .WithFlag(29, out PROTIMER_seqListenBeforeTalkRetryInterruptEnable, name: "LBTRETRYSEQIEN")
                    .WithTaggedFlag("RTCCSYNCHEDSEQIEN", 30)
                    .WithFlag(31, out PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterruptEnable, name: "TOUT0MATCHLBTSEQIEN")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.RxControl, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(0, 6, out PROTIMER_rxSetEvent1, name: "RXSETEVENT1")
                    .WithReservedBits(6, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(8, 6, out PROTIMER_rxSetEvent2, name: "RXSETEVENT2")
                    .WithReservedBits(14, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(16, 6, out PROTIMER_rxClearEvent1, name: "RXCLREVENT1")
                    .WithReservedBits(22, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(24, 6, out PROTIMER_rxClearEvent2, name: "RXCLREVENT2")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => PROTIMER_UpdateRxRequestState())
                },
                {(long)ProtocolTimerRegisters.TxControl, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(0, 6, out PROTIMER_txSetEvent1, name: "TXSETEVENT1")
                    .WithReservedBits(6, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(8, 6, out PROTIMER_txSetEvent2, name: "TXSETEVENT2")
                    .WithReservedBits(14, 18)
                    .WithChangeCallback((_, __) => PROTIMER_UpdateTxRequestState())
                },
                {(long)ProtocolTimerRegisters.ListenBeforeTalkWaitControl, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out PROTIMER_listenBeforeTalkStartExponent, name: "STARTEXP")
                    .WithValueField(4, 4, out PROTIMER_listenBeforeTalkMaxExponent, name: "MAXEXP")
                    .WithValueField(8, 5, out PROTIMER_ccaDelay, name: "CCADELAY")
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 4, out PROTIMER_ccaRepeat, name: "CCAREPEAT")
                    .WithFlag(20, out PROTIMER_fixedBackoff, name: "FIXEDBACKOFF")
                    .WithReservedBits(21, 3)
                    .WithValueField(24, 4, out PROTIMER_retryLimit, name: "RETRYLIMIT")
                    .WithReservedBits(28, 4)
                },
                // TODO?
                // TOUT0PCNT: This is the TOUT counter prescalar value to be saved before switching to another LBT cycle 
                // or the value to be restored after switching.
                // TOUT0CNT: This is the TOUT counter value to be saved before switching to another LBT cycle or the value 
                // to be restored after switching.
                {(long)ProtocolTimerRegisters.ListenBeforeTalkState, new DoubleWordRegister(this)
                    .WithTag("TOUT0PCNT", 0, 16) 
                    .WithTag("TOUT0CNT", 16, 16)
                },
                {(long)ProtocolTimerRegisters.PseudoRandomGeneratorValue, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => {return (ushort)random.Next();}, name: "RANDOM")
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.ListenBeforeTalkState1, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out PROTIMER_ccaCounter, name: "CCACNT")
                    .WithValueField(4, 4, out PROTIMER_listenBeforeTalkExponent, name: "EXP")
                    .WithValueField(8, 4, out PROTIMER_listenBeforeTalkRetryCounter, name: "RETRYCNT")
                    .WithReservedBits(12, 20)
                },
                {(long)ProtocolTimerRegisters.LinearRandomValueGeneratedByFirmware0, new DoubleWordRegister(this)
                    .WithValueField(0, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[0], name: "RANDOM0")
                    .WithValueField(9, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[1], name: "RANDOM1")
                    .WithValueField(18, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[2], name: "RANDOM2")
                    .WithReservedBits(27, 5)
                },
                {(long)ProtocolTimerRegisters.LinearRandomValueGeneratedByFirmware1, new DoubleWordRegister(this)
                    .WithValueField(0, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[3], name: "RANDOM3")
                    .WithValueField(9, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[4], name: "RANDOM4")
                    .WithValueField(18, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[5], name: "RANDOM5")
                    .WithReservedBits(27, 5)
                },
                {(long)ProtocolTimerRegisters.LinearRandomValueGeneratedByFirmware2, new DoubleWordRegister(this)
                    .WithValueField(0, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[6], name: "RANDOM6")
                    .WithValueField(9, 9, out PROTIMER_ListenBeforeTalkRandomBackoffValue[7], name: "RANDOM7")
                    .WithReservedBits(18, 14)
                },
                {(long)ProtocolTimerRegisters.WrapAndBaseCounterWindow, new DoubleWordRegister(this)
                    .WithTag("WRAPBASESEL", 0, 5)
                    .WithReservedBits(5, 27)
                },
                {(long)ProtocolTimerRegisters.Timestamp, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(0, 9, out PROTIMER_racState0, name: "RACSTATE0")
                    .WithReservedBits(9, 7)
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(16, 9, out PROTIMER_racState1, name: "RACSTATE1")
                    .WithReservedBits(25, 7)
                },
            };

            var startOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0Control;
            var controlOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0Control - startOffset;
            var preOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0PreValue - startOffset;
            var baseOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0BaseValue - startOffset;
            var wrapOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0WrapValue - startOffset;
            var wrapLowLimitOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0WrapRangeLowLimit - startOffset;
            var wrapHighLimitOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0WrapRangeHighLimit - startOffset;
            var baseLowLimitOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0BaseRangeLowLimit - startOffset;
            var baseHighLimitOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0BaseRangeHighLimit - startOffset;
            var blockSize = (long)ProtocolTimerRegisters.CaptureCompareChannel1Control - (long)ProtocolTimerRegisters.CaptureCompareChannel0Control;
            for(var index = 0; index < PROTIMER_NumberOfCaptureCompareChannels; index++)
            {
                var i = index;
                // CaptureCompareChannel_n_Control
                registerDictionary.Add(startOffset + blockSize*i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithFlag(0, out PROTIMER_captureCompareChannel[i].enable, name: "ENABLE")
                        .WithEnumField<DoubleWordRegister, PROTIMER_CaptureCompareMode>(1, 2, out PROTIMER_captureCompareChannel[i].mode, name: "CCMODE")
                        .WithFlag(3, out PROTIMER_captureCompareChannel[i].preMatchEnable, name: "PREMATCHEN")
                        .WithFlag(4, out PROTIMER_captureCompareChannel[i].baseMatchEnable, name: "BASEMATCHEN")
                        .WithFlag(5, out PROTIMER_captureCompareChannel[i].wrapMatchEnable, name: "WRAPMATCHEN")
                        .WithTaggedFlag("OIST", 6)
                        .WithTaggedFlag("OUTINV", 7)
                        .WithTag("MOA", 8, 2)
                        .WithTag("OFOA", 10, 2)
                        .WithTag("OFSEL", 12, 2)
                        .WithTaggedFlag("PRSCONF", 14)
                        .WithReservedBits(15, 6)
                        .WithEnumField<DoubleWordRegister, PROTIMER_CaptureInputSource>(21, 4, out PROTIMER_captureCompareChannel[i].captureInputSource, name: "INSEL")
                        .WithTag("ICEDGE", 25, 2)
                        .WithReservedBits(27, 5)
                        .WithWriteCallback((_, __) => PROTIMER_UpdateCompareTimer(i))
                );
                // CaptureCompareChannel_n_Pre
                registerDictionary.Add(startOffset + blockSize*i + preOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 16, out PROTIMER_captureCompareChannel[i].preValue, 
                            writeCallback: (_, __) =>
                            {
                                PROTIMER_captureCompareChannel[i].captureValid.Value = false;
                                PROTIMER_UpdateCompareTimer(i);
                            },
                            valueProviderCallback: _ => 
                            {
                                PROTIMER_captureCompareChannel[i].captureValid.Value = false;
                                return PROTIMER_captureCompareChannel[i].preValue.Value;
                            }, name: "PRE")
                        .WithReservedBits(16, 16)
                );
                // CaptureCompareChannel_n_Base
                registerDictionary.Add(startOffset + blockSize*i + baseOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].baseValue, 
                            writeCallback: (_, __) => { PROTIMER_captureCompareChannel[i].captureValid.Value = false; },
                            valueProviderCallback: _ => 
                            {
                                PROTIMER_captureCompareChannel[i].captureValid.Value = false;
                                return PROTIMER_captureCompareChannel[i].baseValue.Value;
                            }, name: "BASE")
                );
                // CaptureCompareChannel_n_Wrap
                registerDictionary.Add(startOffset + blockSize*i + wrapOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].wrapValue, 
                            writeCallback: (_, __) => { PROTIMER_captureCompareChannel[i].captureValid.Value = false; },
                            valueProviderCallback: _ => 
                            {
                                PROTIMER_captureCompareChannel[i].captureValid.Value = false;
                                return PROTIMER_captureCompareChannel[i].wrapValue.Value;
                            }, name: "WRAP")
                );
                // CaptureCompareChannel_n_WrapRangeLowLimit
                registerDictionary.Add(startOffset + blockSize*i + wrapLowLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].wrapLowLimit, 
                            writeCallback: (_, __) =>
                            {
                                // TODO: RENODE-175
                            },
                            valueProviderCallback: _ => 
                            {
                                // TODO: RENODE-175
                                return 0;
                            }, name: "WRAPT1")
                );
                // CaptureCompareChannel_n_WrapRangeHighLimit
                registerDictionary.Add(startOffset + blockSize*i + wrapHighLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].wrapHighLimit, 
                            writeCallback: (_, __) =>
                            {
                                // TODO: RENODE-175
                            },
                            valueProviderCallback: _ => 
                            {
                                // TODO: RENODE-175
                                return 0;
                            }, name: "WRAPT2")
                );
                // CaptureCompareChannel_n_BaseRangeLowLimit
                registerDictionary.Add(startOffset + blockSize*i + baseLowLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].baseLowLimit, 
                            writeCallback: (_, __) =>
                            {
                                // TODO: RENODE-175
                            },
                            valueProviderCallback: _ => 
                            {
                                // TODO: RENODE-175
                                return 0;
                            }, name: "BASET1")
                );
                // CaptureCompareChannel_n_BaseRangeHighLimit
                registerDictionary.Add(startOffset + blockSize*i + baseHighLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].baseHighLimit, 
                            writeCallback: (_, __) =>
                            {
                                // TODO: RENODE-175
                            },
                            valueProviderCallback: _ => 
                            {
                                // TODO: RENODE-175
                                return 0;
                            }, name: "BASET2")
                );
            }
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildModulatorAndDemodulatorRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)ModulatorAndDemodulatorRegisters.InterruptFlags, new DoubleWordRegister(this, 0x00000008)
                    .WithFlag(0, out MODEM_txFrameSentInterrupt, name: "TXFRAMESENTIF")
                    .WithFlag(1, out MODEM_txSyncSentInterrupt, name: "TXSYNCSENTIF")
                    .WithFlag(2, out MODEM_txPreambleSentInterrupt, name: "TXPRESENTIF")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => MODEM_TxRampingDoneInterrupt, name: "TXRAMPDONEIF")
                    .WithFlag(4, out MODEM_rxFrameWithSyncWord2DetectedInterrupt, name: "RXFRAMEDET2IF")
                    .WithTaggedFlag("PHDSADETIF", 5)
                    .WithTaggedFlag("PHYUNCODEDETIF", 6)
                    .WithTaggedFlag("PHYCODEDETIF", 7)
                    .WithTaggedFlag("RXTIMDETIF", 8)
                    .WithFlag(9, out MODEM_rxPreambleDetectedInterrupt, name: "RXPREDETIF")
                    .WithFlag(10, out MODEM_rxFrameWithSyncWord0DetectedInterrupt, name: "RXFRAMEDET0IF")
                    .WithFlag(11, out MODEM_rxFrameWithSyncWord1DetectedInterrupt, name: "RXFRAMEDET1IF")
                    .WithTaggedFlag("RXTIMLOSTIF", 12)
                    .WithFlag(13, out MODEM_rxPreambleLostInterrupt, name: "RXPRELOSTIF")
                    .WithTaggedFlag("RXFRAMEDETOFIF", 14)
                    .WithTaggedFlag("RXTIMNFIF", 15)
                    .WithFlag(16, out MODEM_rxFrameWithSyncWord3DetectedInterrupt, name: "RXFRAMEDET3IF")
                    .WithTaggedFlag("ETSIF", 17)
                    .WithTaggedFlag("CFGANTPATTRDIF", 18)
                    .WithTaggedFlag("RXRESTARTRSSIMAPREIF", 19)
                    .WithTaggedFlag("RXRESTARTRSSIMASYNCIF", 20)
                    .WithTaggedFlag("SQDETIF", 21)
                    .WithTaggedFlag("SQNOTDETIF", 22)
                    .WithTaggedFlag("ANTDIVRDYIF", 23)
                    .WithTaggedFlag("SOFTRESETDONEIF", 24)
                    .WithTaggedFlag("SQPRENOTDETIF", 25)
                    .WithTaggedFlag("SQFRAMENOTDETIF", 26)
                    .WithTaggedFlag("SQAFCOUTOFBANDIF", 27)
                    .WithTaggedFlag("SIDETIF", 28)
                    .WithTaggedFlag("SIRESETIF", 29)
                    .WithTaggedFlag("HOPPINGIF", 30)
                    .WithTaggedFlag("NOISEDETIF", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.InterruptFlags2, new DoubleWordRegister(this)
                    .WithTaggedFlag("AMPAVERAGERDONEIF", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_txFrameSentInterruptEnable, name: "TXFRAMESENTIEN")
                    .WithFlag(1, out MODEM_txSyncSentInterruptEnable, name: "TXSYNCSENTIEN")
                    .WithFlag(2, out MODEM_txPreambleSentInterruptEnable, name: "TXPRESENTIEN")
                    .WithFlag(3, out MODEM_txRampingDoneInterruptEnable, name: "TXRAMPDONEIEN")
                    .WithFlag(4, out MODEM_rxFrameWithSyncWord2DetectedInterruptEnable, name: "RXFRAMEDET2IEN")
                    .WithTaggedFlag("PHDSADETIEN", 5)
                    .WithTaggedFlag("PHYUNCODEDETIEN", 6)
                    .WithTaggedFlag("PHYCODEDETIEN", 7)
                    .WithTaggedFlag("RXTIMDETIEN", 8)
                    .WithFlag(9, out MODEM_rxPreambleDetectedInterruptEnable, name: "RXPREDETIEN")
                    .WithFlag(10, out MODEM_rxFrameWithSyncWord0DetectedInterruptEnable, name: "RXFRAMEDET0IEN")
                    .WithFlag(11, out MODEM_rxFrameWithSyncWord1DetectedInterruptEnable, name: "RXFRAMEDET1IEN")
                    .WithTaggedFlag("RXTIMLOSTIEN", 12)
                    .WithFlag(13, out MODEM_rxPreambleLostInterruptEnable, name: "RXPRELOSTIEN")
                    .WithTaggedFlag("RXFRAMEDETOFIEN", 14)
                    .WithTaggedFlag("RXTIMNFIEN", 15)
                    .WithFlag(16, out MODEM_rxFrameWithSyncWord3DetectedInterruptEnable, name: "RXFRAMEDET3IEN")
                    .WithTaggedFlag("ETSIEN", 17)
                    .WithTaggedFlag("CFGANTPATTRDIEN", 18)
                    .WithTaggedFlag("RXRESTARTRSSIMAPREIEN", 19)
                    .WithTaggedFlag("RXRESTARTRSSIMASYNCIEN", 20)
                    .WithTaggedFlag("SQDETIEN", 21)
                    .WithTaggedFlag("SQNOTDETIEN", 22)
                    .WithTaggedFlag("ANTDIVRDYIEN", 23)
                    .WithTaggedFlag("SOFTRESETDONEIEN", 24)
                    .WithTaggedFlag("SQPRENOTDETIEN", 25)
                    .WithTaggedFlag("SQFRAMENOTDETIEN", 26)
                    .WithTaggedFlag("SQAFCOUTOFBANDIEN", 27)
                    .WithTaggedFlag("SIDETIEN", 28)
                    .WithTaggedFlag("SIRESETIEN", 29)
                    .WithTaggedFlag("HOPPINGIEN", 30)
                    .WithTaggedFlag("NOISEDETIEN", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.InterruptEnable2, new DoubleWordRegister(this)
                    .WithTaggedFlag("AMPAVERAGERDONEIEN", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.SequencerInterruptFlags, new DoubleWordRegister(this, 0x00000008)
                    .WithFlag(0, out MODEM_seqTxFrameSentInterrupt, name: "SEQTXFRAMESENTIF")
                    .WithFlag(1, out MODEM_seqTxSyncSentInterrupt, name: "SEQTXSYNCSENTIF")
                    .WithFlag(2, out MODEM_seqTxPreambleSentInterrupt, name: "SEQTXPRESENTIF")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => MODEM_TxRampingDoneInterrupt, name: "TXRAMPDONESEQIF")
                    .WithFlag(4, out MODEM_seqRxFrameWithSyncWord2DetectedInterrupt, name: "SEQRXFRAMEDET2IF")
                    .WithTaggedFlag("SEQPHDSADETIF", 5)
                    .WithTaggedFlag("SEQPHYUNCODEDETIF", 6)
                    .WithTaggedFlag("SEQPHYCODEDETIF", 7)
                    .WithTaggedFlag("SEQRXTIMDETIF", 8)
                    .WithFlag(9, out MODEM_seqRxPreambleDetectedInterrupt, name: "SEQRXPREDETIF")
                    .WithFlag(10, out MODEM_seqRxFrameWithSyncWord0DetectedInterrupt, name: "SEQRXFRAMEDET0IF")
                    .WithFlag(11, out MODEM_seqRxFrameWithSyncWord1DetectedInterrupt, name: "SEQRXFRAMEDET1IF")
                    .WithTaggedFlag("SEQRXTIMLOSTIF", 12)
                    .WithFlag(13, out MODEM_seqRxPreambleLostInterrupt, name: "SEQRXPRELOSTIF")
                    .WithTaggedFlag("SEQRXFRAMEDETOFIF", 14)
                    .WithTaggedFlag("SEQRXTIMNFIF", 15)
                    .WithFlag(16, out MODEM_seqRxFrameWithSyncWord3DetectedInterrupt, name: "SEQRXFRAMEDET3IF")
                    .WithTaggedFlag("SEQETSIF", 17)
                    .WithTaggedFlag("SEQCFGANTPATTRDIF", 18)
                    .WithTaggedFlag("SEQRXRESTARTRSSIMAPREIF", 19)
                    .WithTaggedFlag("SEQRXRESTARTRSSIMASYNCIF", 20)
                    .WithTaggedFlag("SEQSQDETIF", 21)
                    .WithTaggedFlag("SEQSQNOTDETIF", 22)
                    .WithTaggedFlag("SEQANTDIVRDYIF", 23)
                    .WithTaggedFlag("SEQSOFTRESETDONEIF", 24)
                    .WithTaggedFlag("SEQSQPRENOTDETIF", 25)
                    .WithTaggedFlag("SEQSQFRAMENOTDETIF", 26)
                    .WithTaggedFlag("SEQSQAFCOUTOFBANDIF", 27)
                    .WithTaggedFlag("SEQSIDETIF", 28)
                    .WithTaggedFlag("SEQSIRESETIF", 29)
                    .WithTaggedFlag("SEQHOPPINGIF", 30)
                    .WithTaggedFlag("SEQNOISEDETIF", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.SequencerInterruptFlags2, new DoubleWordRegister(this)
                    .WithTaggedFlag("SEQAMPAVERAGERDONEIF", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_seqTxFrameSentInterruptEnable, name: "SEQTXFRAMESENTIEN")
                    .WithFlag(1, out MODEM_seqTxSyncSentInterruptEnable, name: "SEQTXSYNCSENTIEN")
                    .WithFlag(2, out MODEM_seqTxPreambleSentInterruptEnable, name: "SEQTXPRESENTIEN")
                    .WithFlag(3, out MODEM_seqTxRampingDoneInterruptEnable, name: "SEQTXRAMPDONEIEN")
                    .WithFlag(4, out MODEM_seqRxFrameWithSyncWord2DetectedInterruptEnable, name: "SEQRXFRAMEDET2IEN")
                    .WithTaggedFlag("SEQPHDSADETIEN", 5)
                    .WithTaggedFlag("SEQPHYUNCODEDETIEN", 6)
                    .WithTaggedFlag("SEQPHYCODEDETIEN", 7)
                    .WithTaggedFlag("SEQRXTIMDETIEN", 8)
                    .WithFlag(9, out MODEM_seqRxPreambleDetectedInterruptEnable, name: "SEQRXPREDETIEN")
                    .WithFlag(10, out MODEM_seqRxFrameWithSyncWord0DetectedInterruptEnable, name: "SEQRXFRAMEDET0IEN")
                    .WithFlag(11, out MODEM_seqRxFrameWithSyncWord1DetectedInterruptEnable, name: "SEQRXFRAMEDET1IEN")
                    .WithTaggedFlag("SEQRXTIMLOSTIEN", 12)
                    .WithFlag(13, out MODEM_seqRxPreambleLostInterruptEnable, name: "SEQRXPRELOSTIEN")
                    .WithTaggedFlag("SEQRXFRAMEDETOFIEN", 14)
                    .WithTaggedFlag("SEQRXTIMNFIEN", 15)
                    .WithFlag(16, out MODEM_seqRxFrameWithSyncWord3DetectedInterruptEnable, name: "SEQRXFRAMEDET3IEN")
                    .WithTaggedFlag("SEQETSIEN", 17)
                    .WithTaggedFlag("SEQCFGANTPATTRDIEN", 18)
                    .WithTaggedFlag("SEQRXRESTARTRSSIMAPREIEN", 19)
                    .WithTaggedFlag("SEQRXRESTARTRSSIMASYNCIEN", 20)
                    .WithTaggedFlag("SEQSQDETIEN", 21)
                    .WithTaggedFlag("SEQSQNOTDETIEN", 22)
                    .WithTaggedFlag("SEQANTDIVRDYIEN", 23)
                    .WithTaggedFlag("SEQSOFTRESETDONEIEN", 24)
                    .WithTaggedFlag("SEQSQPRENOTDETIEN", 25)
                    .WithTaggedFlag("SEQSQFRAMENOTDETIEN", 26)
                    .WithTaggedFlag("SEQSQAFCOUTOFBANDIEN", 27)
                    .WithTaggedFlag("SEQSIDETIEN", 28)
                    .WithTaggedFlag("SEQSIRESETIEN", 29)
                    .WithTaggedFlag("SEQHOPPINGIEN", 30)
                    .WithTaggedFlag("SEQNOISEDETIEN", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.SequencerInterruptEnable2, new DoubleWordRegister(this)
                    .WithTaggedFlag("SEQAMPAVERAGERDONEIEN", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.TxControl, new DoubleWordRegister(this, 0x00000018)
                    .WithFlag(0, out MODEM_txModulatorMode, name: "TXMOD")
                    .WithFlag(1, out MODEM_baudRate2Mbps, name: "BR2M")
                    .WithTaggedFlag("FORCECLKEN", 2)
                    .WithTaggedFlag("TXINTPEN", 3)
                    .WithTaggedFlag("TXDSEN", 4)
                    .WithTaggedFlag("TXAFIFOBYP", 5)
                    .WithTaggedFlag("TXTIMETESTEN", 6)
                    .WithReservedBits(7, 23)
                    .WithTaggedFlag("TXDOFORCEI", 30)
                    .WithTaggedFlag("TXDOFORCEEQ", 31)
                },
                {(long)ModulatorAndDemodulatorRegisters.Control0, new DoubleWordRegister(this)
                    .WithTaggedFlag("FDM0DIFFDIS", 0)
                    .WithTag("MAPFSK", 1, 3)
                    .WithEnumField<DoubleWordRegister, MODEM_SymbolCoding>(4, 2, out MODEM_symbolCoding, name: "CODING")
                    .WithEnumField<DoubleWordRegister, MODEM_ModulationFormat>(6, 3, out MODEM_modulationFormat, name: "MODFORMAT")
                    .WithTaggedFlag("DUALCORROPTDIS", 9)
                    .WithTaggedFlag("OOKASYNCPIN", 10)
                    .WithValueField(11, 5, out MODEM_dsssLength, name: "DSSSLEN")
                    .WithValueField(16, 3, out MODEM_dsssShifts, name: "DSSSSHIFTS")
                    .WithEnumField<DoubleWordRegister, MODEM_DsssDoublingMode>(19, 2, out MODEM_dsssDoublingMode, name: "DSSSDOUBLE")
                    .WithTaggedFlag("DETDIS", 21)
                    .WithTag("DIFFENCMODE", 22, 3)
                    .WithTag("SHAPING", 25, 2)
                    .WithTag("DEMODRAWDATASEL", 27, 3)
                    .WithTag("FRAMEDETDEL", 30, 2)
                },
                {(long)ModulatorAndDemodulatorRegisters.Control1, new DoubleWordRegister(this)
                    .WithValueField(0, 5, out MODEM_syncBits, name: "SYNCBITS")
                    .WithTag("SYNCERRORS", 5, 4)
                    .WithReservedBits(9, 1)
                    .WithFlag(10, out MODEM_txSync, name: "TXSYNC")
                    .WithFlag(11, out MODEM_syncData, name: "SYNCDATA")
                    .WithTaggedFlag("SYNC1INV", 12)
                    .WithReservedBits(13, 1)
                    .WithTag("COMPMODE", 14, 2)
                    .WithTag("RESYNCPER", 16, 4)
                    .WithTag("PHASEDEMOD", 20, 2)
                    .WithTag("FREQOFFESTPER", 22, 3)
                    .WithTag("FREQOFFESTLIM", 25, 7)
                },
                {(long)ModulatorAndDemodulatorRegisters.TxBaudrate, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out MODEM_txBaudrateNumerator, name: "TXBRNUM")
                    .WithReservedBits(16, 16)
                },
                {(long)ModulatorAndDemodulatorRegisters.Preamble, new DoubleWordRegister(this)
                    .WithValueField(16, 16, out MODEM_txBases, name: "TXBASES")
                    .WithTag("PREWNDERRORS", 14, 2)
                    .WithTaggedFlag("PREAMBDETEN", 13)
                    .WithTaggedFlag("SYNCSYMB4FSK", 12)
                    .WithTaggedFlag("DSSSPRE", 11)
                    .WithTag("PREERRORS", 7, 4)
                    .WithTaggedFlag("PRESYMB4FSK", 6)
                    .WithValueField(4, 2, out MODEM_baseBits, name: "BASEBITS")
                    .WithTag("BASE", 0, 4)
                },
                {(long)ModulatorAndDemodulatorRegisters.SyncWord0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out MODEM_syncWord0, name: "SYNC0")
                },
                {(long)ModulatorAndDemodulatorRegisters.SyncWord1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out MODEM_syncWord1, name: "SYNC1")
                },
                {(long)ModulatorAndDemodulatorRegisters.SyncWord2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out MODEM_syncWord2, name: "SYNC2")
                },
                {(long)ModulatorAndDemodulatorRegisters.SyncWord3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out MODEM_syncWord3, name: "SYNC3")
                },
                {(long)ModulatorAndDemodulatorRegisters.SyncWordControl, new DoubleWordRegister(this)
                    .WithTag("SYNCBITS2TH", 0, 5)
                    .WithReservedBits(5, 3)
                    .WithTag("SYNC0ERRORS", 8, 3)
                    .WithTag("SYNC1ERRORS", 11, 3)
                    .WithTag("SYNC2ERRORS", 14, 3)
                    .WithTag("SYNC3ERRORS", 17, 3)
                    .WithReservedBits(20, 4)
                    .WithTag("SYNCSWFEC", 24, 2)
                    .WithReservedBits(26, 3)
                    .WithFlag(29, out MODEM_dualSync2Th, name: "DUALSYNC2TH")
                    .WithFlag(30, out MODEM_dualSync, name: "DUALSYNC")
                    .WithFlag(31, out MODEM_syncDetect2Th, name: "SYNCDET2TH")
                },

                {(long)ModulatorAndDemodulatorRegisters.Command, new DoubleWordRegister(this)
                    .WithTaggedFlag("PRESTOP", 0)
                    .WithTaggedFlag("CHPWRACCUCLR", 1)
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("AFCTXLOCK", 3)
                    .WithTaggedFlag("AFCTXCLEAR", 4)
                    .WithTaggedFlag("AFCRXCLEAR", 5)
                    .WithReservedBits(6, 25)
                    .WithTaggedFlag("HOPPINGSTART", 31)
                },
                {(long)ModulatorAndDemodulatorRegisters.Status, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, MODEM_DemodulatorState>(0, 3, out MODEM_demodulatorState, FieldMode.Read, name: "DEMODSTATE")
                    .WithTaggedFlag("BCRCFEDSADET", 3)
                    .WithValueField(4, 2, out MODEM_frameDetectedId, FieldMode.Read, name: "FRAMEDETID")
                    .WithTaggedFlag("ANTSEL", 6)
                    .WithTaggedFlag("TIMSEQINV", 7)
                    .WithTaggedFlag("TIMLOSTCAUSE", 8)
                    .WithTaggedFlag("DSADETECTED", 9)
                    .WithTaggedFlag("DSAFREQESTDONE", 10)
                    .WithTaggedFlag("VITERBIDEMODTIMDET", 11)
                    .WithTaggedFlag("VITERBIDEMODFRAMEDET", 12)
                    .WithTag("STAMPSTATE", 13, 3)
                    .WithTag("CORR", 16, 8)
                    .WithTag("WEAKSYMBOLS", 24, 8)
                },
                {(long)ModulatorAndDemodulatorRegisters.RampingControl, new DoubleWordRegister(this, 0x00000555)
                  .WithValueField(0, 4, out MODEM_rampRate0, name: "RAMPRATE0")
                  .WithValueField(4, 4, out MODEM_rampRate1, name: "RAMPRATE1")
                  .WithValueField(8, 4, out MODEM_rampRate2, name: "RAMPRATE2")
                  .WithFlag(12, out MODEM_rampDisable, name: "RAMPDIS")
                  .WithTaggedFlag("RAMPDISRST", 13)
                  .WithReservedBits(14, 2)
                  .WithValueField(16, 8, out MODEM_rampValue, name: "RAMPVAL")
                  .WithEnumField<DoubleWordRegister, MODEM_RampMode>(24, 1, out MODEM_rampMode, name: "RAMPMODE")
                  .WithReservedBits(25, 7)
                },
                {(long)ModulatorAndDemodulatorRegisters.RampingLevels, new DoubleWordRegister(this, 0x009F9F9F)
                  .WithValueField(0, 8, out MODEM_rampLevel0, name: "RAMPLEV0")
                  .WithValueField(8, 8, out MODEM_rampLevel1, name: "RAMPLEV1")
                  .WithValueField(16, 8, out MODEM_rampLevel2, name: "RAMPLEV2")
                  .WithValueField(24, 8, out MODEM_rampLevelOffset, name: "RAMPLEVOFFS")
                },
                {(long)ModulatorAndDemodulatorRegisters.ViterbiDemodulator, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_viterbiDemodulatorEnable, name: "VTDEMODEN")                
                    .WithTaggedFlag("HARDDECISION", 1)
                    .WithTag("VITERBIKSI1", 2, 7)
                    .WithTag("VITERBIKSI2", 9, 7)
                    .WithTag("VITERBIKSI3", 16, 7)
                    .WithTaggedFlag("SYNTHAFC", 23)
                    .WithTag("CORRCYCLE", 24, 4)
                    .WithTag("CORRSTPSIZE", 28, 4)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildAutomaticGainControlRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)AutomaticGainControlRegisters.Status0, new DoubleWordRegister(this)
                    .WithTag("GAININDEX", 0, 6)
                    .WithTaggedFlag("RFPKDLOWLAT", 6)
                    .WithTaggedFlag("RFPKDHILAT", 7)
                    .WithTaggedFlag("IFPKDLOLAT", 8)
                    .WithTaggedFlag("IFPKDHILAT", 9)
                    .WithFlag(10, out AGC_cca, FieldMode.Read, name: "CCA")
                    .WithTaggedFlag("GAINOK", 11)
                    .WithTag("PGAINDEX", 12, 4)
                    .WithTag("LNAINDEX", 16, 4)
                    .WithTag("PNIINDEX", 20, 5)
                    .WithTag("ADCINDEX", 25, 2)
                    .WithReservedBits(27, 5)
                },
                {(long)AutomaticGainControlRegisters.Status1, new DoubleWordRegister(this)
                    .WithTag("CHPWR", 0, 8)
                    .WithReservedBits(8, 1)
                    .WithTag("FASTLOOPSTATE", 9, 4)
                    .WithTag("CFLOOPSTATE", 13, 2)
                    .WithEnumField<DoubleWordRegister, AGC_RssiState>(15, 3, out AGC_rssiState, name: "RSSISTATE")
                    .WithTag("RFPKDLOWLATCNT", 18, 12)
                    .WithReservedBits(30, 2)
                },
                {(long)AutomaticGainControlRegisters.InterruptFlags, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_rssiValidInterrupt, name: "RSSIVALIDIF")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_ccaInterrupt, name: "CCAIF")
                  .WithTaggedFlag("RSSIPOSSTEPIF", 3)
                  .WithTaggedFlag("RSSINEGSTEPIF", 4)
                  .WithFlag(5, out AGC_rssiDoneInterrupt, name: "RSSIDONEIF")
                  .WithTaggedFlag("SHORTRSSIPOSSTEPIF", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("RFPKDPRDDONEIF", 8)
                  .WithTaggedFlag("RFPKDCNTDONEIF", 9)
                  .WithFlag(10, out AGC_rssiHighInterrupt, name: "RSSIHIGHIF")
                  .WithFlag(11, out AGC_rssiLowInterrupt, name: "RSSILOWIF")
                  .WithFlag(12, out AGC_ccaNotDetectedInterrupt, name: "CCANODETIF")
                  .WithTaggedFlag("GAINBELOWGAINTHDIF", 13)
                  .WithTaggedFlag("GAINUPDATEFRZIF", 14)
                  .WithTaggedFlag("PNATTENIF", 15)
                  .WithTaggedFlag("COLLDETRSSIMAPREIF", 16)
                  .WithTaggedFlag("COLLDETRSSIMASYNCIF", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)AutomaticGainControlRegisters.InterruptEnable, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_rssiValidInterruptEnable, name: "RSSIVALIDIEN")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_ccaInterruptEnable, name: "CCAIEN")
                  .WithTaggedFlag("RSSIPOSSTEPIEN", 3)
                  .WithTaggedFlag("RSSINEGSTEPIEN", 4)
                  .WithFlag(5, out AGC_rssiDoneInterruptEnable, name: "RSSIDONEIEN")
                  .WithTaggedFlag("SHORTRSSIPOSSTEPIEN", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("RFPKDPRDDONEIEN", 8)
                  .WithTaggedFlag("RFPKDCNTDONEIEN", 9)
                  .WithFlag(10, out AGC_rssiHighInterruptEnable, name: "RSSIHIGHIEN")
                  .WithFlag(11, out AGC_rssiLowInterruptEnable, name: "RSSILOWIEN")
                  .WithFlag(12, out AGC_ccaNotDetectedInterruptEnable, name: "CCANODETIEN")
                  .WithTaggedFlag("GAINBELOWGAINTHDIEN", 13)
                  .WithTaggedFlag("GAINUPDATEFRZIEN", 14)
                  .WithTaggedFlag("PNATTENIEN", 15)
                  .WithTaggedFlag("COLLDETRSSIMAPREIEN", 16)
                  .WithTaggedFlag("COLLDETRSSIMASYNCIEN", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)AutomaticGainControlRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_seqRssiValidInterrupt, name: "RSSIVALIDSEQIF")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_seqCcaInterrupt, name: "CCASEQIF")
                  .WithTaggedFlag("RSSIPOSSTEPSEQIF", 3)
                  .WithTaggedFlag("RSSINEGSTEPSEQIF", 4)
                  .WithFlag(5, out AGC_seqRssiDoneInterrupt, name: "RSSIDONESEQIF")
                  .WithTaggedFlag("SHORTRSSIPOSSTEPSEQIF", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("RFPKDPRDDONESEQIF", 8)
                  .WithTaggedFlag("RFPKDCNTDONESEQIF", 9)
                  .WithFlag(10, out AGC_seqRssiHighInterrupt, name: "RSSIHIGHSEQIF")
                  .WithFlag(11, out AGC_seqRssiLowInterrupt, name: "RSSILOWSEQIF")
                  .WithFlag(12, out AGC_seqCcaNotDetectedInterrupt, name: "CCANODETSEQIF")
                  .WithTaggedFlag("GAINBELOWGAINTHDSEQIF", 13)
                  .WithTaggedFlag("GAINUPDATEFRZSEQIF", 14)
                  .WithTaggedFlag("PNATTENSEQIF", 15)
                  .WithTaggedFlag("COLLDETRSSIMAPRESEQIF", 16)
                  .WithTaggedFlag("COLLDETRSSIMASYNCSEQIF", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)AutomaticGainControlRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_seqRssiValidInterruptEnable, name: "RSSIVALIDSEQIEN")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_seqCcaInterruptEnable, name: "CCASEQIEN")
                  .WithTaggedFlag("RSSIPOSSTEPSEQIEN", 3)
                  .WithTaggedFlag("RSSINEGSTEPSEQIEN", 4)
                  .WithFlag(5, out AGC_seqRssiDoneInterruptEnable, name: "RSSIDONESEQIEN")
                  .WithTaggedFlag("SHORTRSSIPOSSTEPSEQIEN", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("RFPKDPRDDONESEQIEN", 8)
                  .WithTaggedFlag("RFPKDCNTDONESEQIEN", 9)
                  .WithFlag(10, out AGC_seqRssiHighInterruptEnable, name: "RSSIHIGHSEQIEN")
                  .WithFlag(11, out AGC_seqRssiLowInterruptEnable, name: "RSSILOWSEQIEN")
                  .WithFlag(12, out AGC_seqCcaNotDetectedInterruptEnable, name: "CCANODETSEQIEN")
                  .WithTaggedFlag("GAINBELOWGAINTHDSEQIEN", 13)
                  .WithTaggedFlag("GAINUPDATEFRZSEQIEN", 14)
                  .WithTaggedFlag("PNATTENSEQIEN", 15)
                  .WithTaggedFlag("COLLDETRSSIMAPRESEQIEN", 16)
                  .WithTaggedFlag("COLLDETRSSIMASYNCSEQIEN", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)AutomaticGainControlRegisters.ListenBeforeTalkConfiguration, new DoubleWordRegister(this)
                  .WithValueField(0, 4, out AGC_ccaRssiPeriod, name: "CCARSSIPERIOD")
                  .WithFlag(4, out AGC_ccaRssiPeriodEnable, name: "ENCCARSSIPERIOD")
                  .WithTaggedFlag("ENCCAGAINREDUCED", 5)
                  .WithTaggedFlag("ENCCARSSIMAX", 6)
                  .WithReservedBits(7, 25)
                },
                {(long)AutomaticGainControlRegisters.Control0, new DoubleWordRegister(this, 0x2132727F)
                    .WithTag("PWRTARGET", 0, 8)
                    .WithTag("MODE", 8, 3)
                    .WithValueField(11, 8, out AGC_rssiShift, name: "RSSISHIFT")
                    .WithTaggedFlag("DISCFLOOPADJ", 19)
                    .WithTaggedFlag("CFLOOPNFADJ", 20)
                    .WithTaggedFlag("CFLOOPNEWCALC", 21)
                    .WithTaggedFlag("DISRESETCHPWR", 22)
                    .WithTaggedFlag("ADCATTENMODE", 23)
                    .WithTaggedFlag("FENOTCHMODESEL", 24)
                    .WithTag("ADCATTENCODE", 25, 2)
                    .WithTaggedFlag("ENRSSIRESET", 27)
                    .WithTaggedFlag("DSADISCFLOOP", 28)
                    .WithTaggedFlag("DISPNGAINUP", 29)
                    .WithTaggedFlag("DISPNDWNCOMP", 30)
                    .WithTaggedFlag("AGCRST", 31)
                },
                {(long)AutomaticGainControlRegisters.Control1, new DoubleWordRegister(this, 0x00001300)
                  .WithValueField(0, 8, out AGC_ccaThreshold, name: "CCATHRSH")
                  .WithValueField(8, 4, out AGC_rssiMeasurePeriod, name: "RSSIPERIOD")
                  .WithValueField(12, 3, out AGC_powerMeasurePeriod, name: "PWRPERIOD")
                  .WithEnumField<DoubleWordRegister, AGC_CcaMode>(15, 2, out AGC_ccaMode, name: "CCAMODE")
                  .WithEnumField<DoubleWordRegister, AGC_CcaMode3Logic>(17, 1, out AGC_ccaMode3Logic, name: "CCAMODE3LOGIC")
                  .WithFlag(18, out AGC_ccaSoftwareControl, name: "CCASWCTRL")
                  .WithTaggedFlag("DISRSTONPREDET", 19)
                  .WithTaggedFlag("CFLOOPINCREQMODE", 20)
                  .WithTag("ENRSSIINITGAINCHG", 21, 4)
                  .WithTaggedFlag("DISPWRERRCOMP", 25)
                  .WithTaggedFlag("ENAGCRSTALL", 26)
                  .WithReservedBits(27, 1)
                  .WithTag("RSSIINITGAINSTEPTHR", 28, 3)
                  .WithTaggedFlag("INRXRSSIGATING", 31)
                },
                {(long)AutomaticGainControlRegisters.Control7, new DoubleWordRegister(this)
                  .WithTag("SUBDEN", 0, 8)
                  .WithValueField(8, 8, out AGC_subPeriodInteger, name: "SUBINT")
                  .WithTag("SUBNUM", 16, 8)
                  .WithFlag(24, out AGC_subPeriod, name: "SUBPERIOD")
                  .WithReservedBits(25, 7)
                },
                {(long)AutomaticGainControlRegisters.ReceivedSignalStrengthIndicator, new DoubleWordRegister(this, 0x00008000)
                  .WithReservedBits(0, 6)
                  .WithValueField(6, 2, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_RssiFractionalPart, name: "RSSIFRAC")
                  .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_RssiIntegerPartAdjusted, name: "RSSIINT")
                  .WithReservedBits(16, 16)
                },
                {(long)AutomaticGainControlRegisters.FrameReceivedSignalStrengthIndicator, new DoubleWordRegister(this, 0x00008000)
                  .WithReservedBits(0, 6)
                  .WithValueField(6, 2, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_FrameRssiFractionalPart, name: "FRAMERSSIFRAC")
                  .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_FrameRssiIntegerPartAdjusted, name: "FRAMERSSIINT")
                  .WithReservedBits(16, 16)
                },
                {(long)AutomaticGainControlRegisters.Command, new DoubleWordRegister(this)
                  .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) {AGC_RssiStartCommand();} }, name: "RSSISTART")
                  .WithReservedBits(1, 31)
                },
                {(long)AutomaticGainControlRegisters.ReceivedSignalStrengthIndicatorAbsoluteThreshold, new DoubleWordRegister(this)
                  .WithValueField(0, 8, out AGC_rssiHighThreshold, name: "RSSIHIGHTHRSH")
                  .WithValueField(8, 8, out AGC_rssiLowThreshold, name: "RSSILOWTHRSH")
                  .WithTag("SIRSSIHIGHTHR", 16, 8)
                  .WithTag("SIRSSINEGSTEPTHR", 24, 8)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildFswMailboxRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)RfMailboxRegisters.MessagePointer0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out FSWMAILBOX_messagePointer[0], name: "MSGPTR0")
                },
                {(long)RfMailboxRegisters.MessagePointer1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out FSWMAILBOX_messagePointer[1], name: "MSGPTR1")
                },
                {(long)RfMailboxRegisters.MessagePointer2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out FSWMAILBOX_messagePointer[2], name: "MSGPTR2")
                },
                {(long)RfMailboxRegisters.MessagePointer3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out FSWMAILBOX_messagePointer[3], name: "MSGPTR3")
                },
                {(long)RfMailboxRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out FSWMAILBOX_messageInterrupt[0], name: "MBOXIF0")
                    .WithFlag(1, out FSWMAILBOX_messageInterrupt[1], name: "MBOXIF1")
                    .WithFlag(2, out FSWMAILBOX_messageInterrupt[2], name: "MBOXIF2")
                    .WithFlag(3, out FSWMAILBOX_messageInterrupt[3], name: "MBOXIF3")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RfMailboxRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out FSWMAILBOX_messageInterruptEnable[0], name: "MBOXIEN0")
                    .WithFlag(1, out FSWMAILBOX_messageInterruptEnable[1], name: "MBOXIEN1")
                    .WithFlag(2, out FSWMAILBOX_messageInterruptEnable[2], name: "MBOXIEN2")
                    .WithFlag(3, out FSWMAILBOX_messageInterruptEnable[3], name: "MBOXIEN3")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildRfMailboxRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)RfMailboxRegisters.MessagePointer0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[0], name: "MSGPTR0")
                },
                {(long)RfMailboxRegisters.MessagePointer1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[1], name: "MSGPTR1")
                },
                {(long)RfMailboxRegisters.MessagePointer2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[2], name: "MSGPTR2")
                },
                {(long)RfMailboxRegisters.MessagePointer3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[3], name: "MSGPTR3")
                },
                {(long)RfMailboxRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out RFMAILBOX_messageInterrupt[0], name: "MBOXIF0")
                    .WithFlag(1, out RFMAILBOX_messageInterrupt[1], name: "MBOXIF1")
                    .WithFlag(2, out RFMAILBOX_messageInterrupt[2], name: "MBOXIF2")
                    .WithFlag(3, out RFMAILBOX_messageInterrupt[3], name: "MBOXIF3")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RfMailboxRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out RFMAILBOX_messageInterruptEnable[0], name: "MBOXIEN0")
                    .WithFlag(1, out RFMAILBOX_messageInterruptEnable[1], name: "MBOXIEN1")
                    .WithFlag(2, out RFMAILBOX_messageInterruptEnable[2], name: "MBOXIEN2")
                    .WithFlag(3, out RFMAILBOX_messageInterruptEnable[3], name: "MBOXIEN3")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildHostPortalRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)HostPortalRegisters.Control, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => HOSTPORTAL_PowerUpRequest, writeCallback: (_, value) => {HOSTPORTAL_PowerUpRequest = value;}, name: "LPW0PWRUPREQ")
                    .WithReservedBits(1, 31)
                },
                {(long)HostPortalRegisters.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => HOSTPORTAL_PowerUpAck, name: "LPW0PWRUPACK")
                    .WithReservedBits(1, 31)
                },
                {(long)HostPortalRegisters.Mailbox0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[0], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.Mailbox1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[1], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.Mailbox2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[2], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.Mailbox3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[3], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.Mailbox4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[4], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.Mailbox5, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[5], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.Mailbox6, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[6], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.Mailbox7, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTPORTAL_mailboxRegister[7], name: "MESSAGE")
                },
                {(long)HostPortalRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out HOSTPORTAL_interrupt[0], name: "LPW0IF0")
                    .WithFlag(1, out HOSTPORTAL_interrupt[1], name: "LPW0IF1")
                    .WithFlag(2, out HOSTPORTAL_interrupt[2], name: "LPW0IF2")
                    .WithFlag(3, out HOSTPORTAL_interrupt[3], name: "LPW0IF3")
                    .WithFlag(4, out HOSTPORTAL_interrupt[4], name: "LPW0IF4")
                    .WithFlag(5, out HOSTPORTAL_interrupt[5], name: "LPW0IF5")
                    .WithFlag(6, out HOSTPORTAL_interrupt[6], name: "LPW0IF6")
                    .WithFlag(7, out HOSTPORTAL_interrupt[7], name: "LPW0IF7")
                    .WithFlag(8, out HOSTPORTAL_interrupt[8], name: "LPW0IF8")
                    .WithFlag(9, out HOSTPORTAL_interrupt[9], name: "LPW0IF9")
                    .WithFlag(10, out HOSTPORTAL_interrupt[10], name: "LPW0IF10")
                    .WithFlag(11, out HOSTPORTAL_interrupt[11], name: "LPW0IF11")
                    .WithFlag(12, out HOSTPORTAL_interrupt[12], name: "LPW0IF12")
                    .WithFlag(13, out HOSTPORTAL_interrupt[13], name: "LPW0IF13")
                    .WithFlag(14, out HOSTPORTAL_interrupt[14], name: "LPW0IF14")
                    .WithFlag(15, out HOSTPORTAL_interrupt[15], name: "LPW0IF15")
                    .WithFlag(16, out HOSTPORTAL_interrupt[16], name: "LPW0IF16")
                    .WithFlag(17, out HOSTPORTAL_interrupt[17], name: "LPW0IF17")
                    .WithFlag(18, out HOSTPORTAL_interrupt[18], name: "LPW0IF18")
                    .WithFlag(19, out HOSTPORTAL_interrupt[19], name: "LPW0IF19")
                    .WithFlag(20, out HOSTPORTAL_interrupt[20], name: "LPW0IF20")
                    .WithFlag(21, out HOSTPORTAL_interrupt[21], name: "LPW0IF21")
                    .WithFlag(22, out HOSTPORTAL_interrupt[22], name: "LPW0IF22")
                    .WithFlag(23, out HOSTPORTAL_interrupt[23], name: "LPW0IF23")
                    .WithFlag(24, out HOSTPORTAL_interrupt[24], name: "LPW0IF24")
                    .WithFlag(25, out HOSTPORTAL_interrupt[25], name: "LPW0IF25")
                    .WithFlag(26, out HOSTPORTAL_interrupt[26], name: "LPW0IF26")
                    .WithFlag(27, out HOSTPORTAL_interrupt[27], name: "LPW0IF27")
                    .WithFlag(28, out HOSTPORTAL_interrupt[28], name: "LPW0IF28")
                    .WithFlag(29, out HOSTPORTAL_interrupt[29], name: "LPW0IF29")
                    .WithFlag(30, out HOSTPORTAL_interrupt[30], name: "LPW0IF30")
                    .WithFlag(31, out HOSTPORTAL_interrupt[31], name: "LPW0IF31")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)HostPortalRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out HOSTPORTAL_interruptEnable[0], name: "LPW0IEN0")
                    .WithFlag(1, out HOSTPORTAL_interruptEnable[1], name: "LPW0IEN1")
                    .WithFlag(2, out HOSTPORTAL_interruptEnable[2], name: "LPW0IEN2")
                    .WithFlag(3, out HOSTPORTAL_interruptEnable[3], name: "LPW0IEN3")
                    .WithFlag(4, out HOSTPORTAL_interruptEnable[4], name: "LPW0IEN4")
                    .WithFlag(5, out HOSTPORTAL_interruptEnable[5], name: "LPW0IEN5")
                    .WithFlag(6, out HOSTPORTAL_interruptEnable[6], name: "LPW0IEN6")
                    .WithFlag(7, out HOSTPORTAL_interruptEnable[7], name: "LPW0IEN7")
                    .WithFlag(8, out HOSTPORTAL_interruptEnable[8], name: "LPW0IEN8")
                    .WithFlag(9, out HOSTPORTAL_interruptEnable[9], name: "LPW0IEN9")
                    .WithFlag(10, out HOSTPORTAL_interruptEnable[10], name: "LPW0IEN10")
                    .WithFlag(11, out HOSTPORTAL_interruptEnable[11], name: "LPW0IEN11")
                    .WithFlag(12, out HOSTPORTAL_interruptEnable[12], name: "LPW0IEN12")
                    .WithFlag(13, out HOSTPORTAL_interruptEnable[13], name: "LPW0IEN13")
                    .WithFlag(14, out HOSTPORTAL_interruptEnable[14], name: "LPW0IEN14")
                    .WithFlag(15, out HOSTPORTAL_interruptEnable[15], name: "LPW0IEN15")
                    .WithFlag(16, out HOSTPORTAL_interruptEnable[16], name: "LPW0IEN16")
                    .WithFlag(17, out HOSTPORTAL_interruptEnable[17], name: "LPW0IEN17")
                    .WithFlag(18, out HOSTPORTAL_interruptEnable[18], name: "LPW0IEN18")
                    .WithFlag(19, out HOSTPORTAL_interruptEnable[19], name: "LPW0IEN19")
                    .WithFlag(20, out HOSTPORTAL_interruptEnable[20], name: "LPW0IEN20")
                    .WithFlag(21, out HOSTPORTAL_interruptEnable[21], name: "LPW0IEN21")
                    .WithFlag(22, out HOSTPORTAL_interruptEnable[22], name: "LPW0IEN22")
                    .WithFlag(23, out HOSTPORTAL_interruptEnable[23], name: "LPW0IEN23")
                    .WithFlag(24, out HOSTPORTAL_interruptEnable[24], name: "LPW0IEN24")
                    .WithFlag(25, out HOSTPORTAL_interruptEnable[25], name: "LPW0IEN25")
                    .WithFlag(26, out HOSTPORTAL_interruptEnable[26], name: "LPW0IEN26")
                    .WithFlag(27, out HOSTPORTAL_interruptEnable[27], name: "LPW0IEN27")
                    .WithFlag(28, out HOSTPORTAL_interruptEnable[28], name: "LPW0IEN28")
                    .WithFlag(29, out HOSTPORTAL_interruptEnable[29], name: "LPW0IEN29")
                    .WithFlag(30, out HOSTPORTAL_interruptEnable[30], name: "LPW0IEN30")
                    .WithFlag(31, out HOSTPORTAL_interruptEnable[31], name: "LPW0IEN31")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildLpw0PortalRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Lpw0PortalRegisters.Control, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => LPW0PORTAL_PowerUpRequest, writeCallback: (_, value) => {LPW0PORTAL_PowerUpRequest = value;}, name: "HOSTPWRUPREQ")
                    .WithReservedBits(1, 31)
                },
                {(long)Lpw0PortalRegisters.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => LPW0PORTAL_PowerUpAck, name: "HOSTPWRUPACK")
                    .WithReservedBits(1, 31)
                },
                {(long)Lpw0PortalRegisters.Mailbox0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[0], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.Mailbox1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[1], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.Mailbox2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[2], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.Mailbox3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[3], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.Mailbox4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[4], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.Mailbox5, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[5], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.Mailbox6, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[6], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.Mailbox7, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out LPW0PORTAL_mailboxRegister[7], name: "MESSAGE")
                },
                {(long)Lpw0PortalRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out LPW0PORTAL_interrupt[0], name: "HOSTIF0")
                    .WithFlag(1, out LPW0PORTAL_interrupt[1], name: "HOSTIF1")
                    .WithFlag(2, out LPW0PORTAL_interrupt[2], name: "HOSTIF2")
                    .WithFlag(3, out LPW0PORTAL_interrupt[3], name: "HOSTIF3")
                    .WithFlag(4, out LPW0PORTAL_interrupt[4], name: "HOSTIF4")
                    .WithFlag(5, out LPW0PORTAL_interrupt[5], name: "HOSTIF5")
                    .WithFlag(6, out LPW0PORTAL_interrupt[6], name: "HOSTIF6")
                    .WithFlag(7, out LPW0PORTAL_interrupt[7], name: "HOSTIF7")
                    .WithFlag(8, out LPW0PORTAL_interrupt[8], name: "HOSTIF8")
                    .WithFlag(9, out LPW0PORTAL_interrupt[9], name: "HOSTIF9")
                    .WithFlag(10, out LPW0PORTAL_interrupt[10], name: "HOSTIF10")
                    .WithFlag(11, out LPW0PORTAL_interrupt[11], name: "HOSTIF11")
                    .WithFlag(12, out LPW0PORTAL_interrupt[12], name: "HOSTIF12")
                    .WithFlag(13, out LPW0PORTAL_interrupt[13], name: "HOSTIF13")
                    .WithFlag(14, out LPW0PORTAL_interrupt[14], name: "HOSTIF14")
                    .WithFlag(15, out LPW0PORTAL_interrupt[15], name: "HOSTIF15")
                    .WithFlag(16, out LPW0PORTAL_interrupt[16], name: "HOSTIF16")
                    .WithFlag(17, out LPW0PORTAL_interrupt[17], name: "HOSTIF17")
                    .WithFlag(18, out LPW0PORTAL_interrupt[18], name: "HOSTIF18")
                    .WithFlag(19, out LPW0PORTAL_interrupt[19], name: "HOSTIF19")
                    .WithFlag(20, out LPW0PORTAL_interrupt[20], name: "HOSTIF20")
                    .WithFlag(21, out LPW0PORTAL_interrupt[21], name: "HOSTIF21")
                    .WithFlag(22, out LPW0PORTAL_interrupt[22], name: "HOSTIF22")
                    .WithFlag(23, out LPW0PORTAL_interrupt[23], name: "HOSTIF23")
                    .WithFlag(24, out LPW0PORTAL_interrupt[24], name: "HOSTIF24")
                    .WithFlag(25, out LPW0PORTAL_interrupt[25], name: "HOSTIF25")
                    .WithFlag(26, out LPW0PORTAL_interrupt[26], name: "HOSTIF26")
                    .WithFlag(27, out LPW0PORTAL_interrupt[27], name: "HOSTIF27")
                    .WithFlag(28, out LPW0PORTAL_interrupt[28], name: "HOSTIF28")
                    .WithFlag(29, out LPW0PORTAL_interrupt[29], name: "HOSTIF29")
                    .WithFlag(30, out LPW0PORTAL_interrupt[30], name: "HOSTIF30")
                    .WithFlag(31, out LPW0PORTAL_interrupt[31], name: "HOSTIF31")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Lpw0PortalRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out LPW0PORTAL_interruptEnable[0], name: "HOSTIEN0")
                    .WithFlag(1, out LPW0PORTAL_interruptEnable[1], name: "HOSTIEN1")
                    .WithFlag(2, out LPW0PORTAL_interruptEnable[2], name: "HOSTIEN2")
                    .WithFlag(3, out LPW0PORTAL_interruptEnable[3], name: "HOSTIEN3")
                    .WithFlag(4, out LPW0PORTAL_interruptEnable[4], name: "HOSTIEN4")
                    .WithFlag(5, out LPW0PORTAL_interruptEnable[5], name: "HOSTIEN5")
                    .WithFlag(6, out LPW0PORTAL_interruptEnable[6], name: "HOSTIEN6")
                    .WithFlag(7, out LPW0PORTAL_interruptEnable[7], name: "HOSTIEN7")
                    .WithFlag(8, out LPW0PORTAL_interruptEnable[8], name: "HOSTIEN8")
                    .WithFlag(9, out LPW0PORTAL_interruptEnable[9], name: "HOSTIEN9")
                    .WithFlag(10, out LPW0PORTAL_interruptEnable[10], name: "HOSTIEN10")
                    .WithFlag(11, out LPW0PORTAL_interruptEnable[11], name: "HOSTIEN11")
                    .WithFlag(12, out LPW0PORTAL_interruptEnable[12], name: "HOSTIEN12")
                    .WithFlag(13, out LPW0PORTAL_interruptEnable[13], name: "HOSTIEN13")
                    .WithFlag(14, out LPW0PORTAL_interruptEnable[14], name: "HOSTIEN14")
                    .WithFlag(15, out LPW0PORTAL_interruptEnable[15], name: "HOSTIEN15")
                    .WithFlag(16, out LPW0PORTAL_interruptEnable[16], name: "HOSTIEN16")
                    .WithFlag(17, out LPW0PORTAL_interruptEnable[17], name: "HOSTIEN17")
                    .WithFlag(18, out LPW0PORTAL_interruptEnable[18], name: "HOSTIEN18")
                    .WithFlag(19, out LPW0PORTAL_interruptEnable[19], name: "HOSTIEN19")
                    .WithFlag(20, out LPW0PORTAL_interruptEnable[20], name: "HOSTIEN20")
                    .WithFlag(21, out LPW0PORTAL_interruptEnable[21], name: "HOSTIEN21")
                    .WithFlag(22, out LPW0PORTAL_interruptEnable[22], name: "HOSTIEN22")
                    .WithFlag(23, out LPW0PORTAL_interruptEnable[23], name: "HOSTIEN23")
                    .WithFlag(24, out LPW0PORTAL_interruptEnable[24], name: "HOSTIEN24")
                    .WithFlag(25, out LPW0PORTAL_interruptEnable[25], name: "HOSTIEN25")
                    .WithFlag(26, out LPW0PORTAL_interruptEnable[26], name: "HOSTIEN26")
                    .WithFlag(27, out LPW0PORTAL_interruptEnable[27], name: "HOSTIEN27")
                    .WithFlag(28, out LPW0PORTAL_interruptEnable[28], name: "HOSTIEN28")
                    .WithFlag(29, out LPW0PORTAL_interruptEnable[29], name: "HOSTIEN29")
                    .WithFlag(30, out LPW0PORTAL_interruptEnable[30], name: "HOSTIEN30")
                    .WithFlag(31, out LPW0PORTAL_interruptEnable[31], name: "HOSTIEN31")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }
#endregion

        private uint Read<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, bool internal_read = false)
        where T : struct, IComparable, IFormattable
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {  
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }

            try
            {
                if(registersCollection.TryRead(internal_offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "{0}: Read from {1} at offset 0x{2:X} ({3}), returned 0x{4:X}", 
                             this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), result);
                }
            }

            if (LogRegisterAccess && !internal_read)
            {
                this.Log(LogLevel.Warning, "Unhandled read from {0} at offset 0x{1:X} ({2}).", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"));
            }

            return 0;
        }

        private byte ReadByte<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, bool internal_read = false)
        where T : struct, IComparable, IFormattable
        {
            int byteOffset = (int)(offset & 0x3);
            // TODO: single byte reads are treated as internal reads for now to avoid flooding the log during debugging.
            uint registerValue = Read<T>(registersCollection, regionName, offset - byteOffset, true);
            byte result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
        }

        private void Write<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, uint value)
        where T : struct, IComparable, IFormattable
        {
            machine.ClockSource.ExecuteInLock(delegate {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value | value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value & ~value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value ^ value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                }

                if (LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "{0}: Write to {1} at offset 0x{2:X} ({3}), value 0x{4:X}", 
                            this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                }
                if(!registersCollection.TryWrite(internal_offset, internal_value) && LogRegisterAccess)
                {
                    this.Log(LogLevel.Warning, "Unhandled write to {0} at offset 0x{1:X} ({2}), value 0x{3:X}.", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                    return;
                }
            });
        }

        public void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                FRC_CheckPacketCaptureBufferThreshold();

                //-------------------------------
                // Main core interrupts
                //-------------------------------

                // RAC_RSM interrupts
                var irq = ((RAC_radioStateChangeInterrupt.Value && RAC_radioStateChangeInterruptEnable.Value)
                           || (RAC_stimerCompareEventInterrupt.Value && RAC_stimerCompareEventInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: IRQ RAC_RSM set (IF=0x{1:X}, IEN=0x{2:X})",
                             this.GetTime(), 
                             (uint)(RAC_radioStateChangeInterrupt.Value
                                    | RAC_stimerCompareEventInterrupt.Value ? 0x2 : 0), 
                             (uint)(RAC_radioStateChangeInterruptEnable.Value
                                    | RAC_stimerCompareEventInterruptEnable.Value ? 0x2 : 0));
                }
                RadioControllerRadioStateMachineIRQ.Set(irq);

                // RAC_SEQ interrupts
                irq = ((RAC_mainCoreSeqInterrupts.Value & RAC_mainCoreSeqInterruptsEnable.Value) > 0);
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: IRQ RAC_SEQ set (IF=0x{1:X}, IEN=0x{2:X})", 
                             this.GetTime(),
                             (uint)(RAC_mainCoreSeqInterrupts.Value << 16),
                             (uint)(RAC_mainCoreSeqInterruptsEnable.Value << 16));
                }
                RadioControllerSequencerIRQ.Set(irq);

                // FRC interrupt
                irq = ((FRC_txDoneInterrupt.Value && FRC_txDoneInterruptEnable.Value)
                       || (FRC_txAfterFrameDoneInterrupt.Value && FRC_txAfterFrameDoneInterruptEnable.Value)
                       || (FRC_txUnderflowInterrupt.Value && FRC_txUnderflowInterruptEnable.Value)
                       || (FRC_rxDoneInterrupt.Value && FRC_rxDoneInterruptEnable.Value)
                       || (FRC_rxAbortedInterrupt.Value && FRC_rxAbortedInterruptEnable.Value)
                       || (FRC_frameErrorInterrupt.Value && FRC_frameErrorInterruptEnable.Value)
                       || (FRC_rxOverflowInterrupt.Value && FRC_rxOverflowInterruptEnable.Value)
                       || (FRC_rxRawEventInterrupt.Value && FRC_rxRawEventInterruptEnable.Value)
                       || (FRC_txRawEventInterrupt.Value && FRC_txRawEventInterruptEnable.Value)
                       || (FRC_packetBufferStartInterrupt.Value && FRC_packetBufferStartInterruptEnable.Value)
                       || (FRC_packetBufferThresholdInterrupt.Value && FRC_packetBufferThresholdInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.InterruptFlags, out IF);
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: IRQ FRC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                FrameControllerIRQ.Set(irq);

                // BUFC interrupt
                irq = false;
                Array.ForEach(BUFC_buffer, x => irq |= x.Interrupt);
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    bufferControllerRegistersCollection.TryRead((long)BufferControllerRegisters.InterruptFlags, out IF);
                    bufferControllerRegistersCollection.TryRead((long)BufferControllerRegisters.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: IRQ BUFC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                BufferControllerIRQ.Set(irq);

                // PROTIMER interrupt
                irq = (PROTIMER_preCounterOverflowInterrupt.Value && PROTIMER_preCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_baseCounterOverflowInterrupt.Value && PROTIMER_baseCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_wrapCounterOverflowInterrupt.Value && PROTIMER_wrapCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkSuccessInterrupt.Value && PROTIMER_listenBeforeTalkSuccessInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkFailureInterrupt.Value && PROTIMER_listenBeforeTalkFailureInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkRetryInterrupt.Value && PROTIMER_listenBeforeTalkRetryInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkTimeoutCounterMatchInterrupt.Value && PROTIMER_listenBeforeTalkTimeoutCounterMatchInterruptEnable.Value);
                Array.ForEach(PROTIMER_timeoutCounter, x => irq |= x.Interrupt);
                Array.ForEach(PROTIMER_captureCompareChannel, x => irq |= x.Interrupt);
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IF2 = 0U;
                    var IEN = 0U;
                    var IEN2 = 0U;
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.InterruptFlags, out IF);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.InterruptFlags2, out IF2);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.InterruptEnable, out IEN);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.InterruptEnable2, out IEN2);
                    this.Log(LogLevel.Info, "{0}: IRQ PROTIMER set (IF=0x{1:X}, IF2=0x{2:X}, IEN=0x{3:X} IEN2=0x{4:X})", this.GetTime(), IF, IF2, IEN, IEN2);
                }
                ProtocolTimerIRQ.Set(irq);

                // MODEM interrupt
                irq = ((MODEM_txFrameSentInterrupt.Value && MODEM_txFrameSentInterruptEnable.Value)
                       || (MODEM_txSyncSentInterrupt.Value && MODEM_txSyncSentInterruptEnable.Value)
                       || (MODEM_txPreambleSentInterrupt.Value && MODEM_txPreambleSentInterruptEnable.Value)
                       || (MODEM_TxRampingDoneInterrupt && MODEM_txRampingDoneInterruptEnable.Value)
                       || (MODEM_rxPreambleDetectedInterrupt.Value && MODEM_rxPreambleDetectedInterruptEnable.Value)
                       || (MODEM_rxFrameWithSyncWord0DetectedInterrupt.Value && MODEM_rxFrameWithSyncWord0DetectedInterruptEnable.Value)
                       || (MODEM_rxFrameWithSyncWord1DetectedInterrupt.Value && MODEM_rxFrameWithSyncWord1DetectedInterruptEnable.Value)
                       || (MODEM_rxFrameWithSyncWord2DetectedInterrupt.Value && MODEM_rxFrameWithSyncWord2DetectedInterruptEnable.Value)
                       || (MODEM_rxFrameWithSyncWord3DetectedInterrupt.Value && MODEM_rxFrameWithSyncWord3DetectedInterruptEnable.Value)
                       || (MODEM_rxPreambleLostInterrupt.Value && MODEM_rxPreambleLostInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IF2 = 0U;
                    var IEN = 0U;
                    var IEN2 = 0U;
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.InterruptFlags, out IF);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.InterruptFlags2, out IF2);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.InterruptEnable, out IEN);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.InterruptEnable2, out IEN2);
                    this.Log(LogLevel.Info, "{0}: IRQ MODEM set (IF=0x{1:X}, IF2=0x{2:X}, IEN=0x{3:X} IEN=0x{4:X})", this.GetTime(), IF, IF2, IEN, IEN2);
                }
                ModulatorAndDemodulatorIRQ.Set(irq);

                // AGC interrupt
                irq = ((AGC_rssiValidInterrupt.Value && AGC_rssiValidInterruptEnable.Value)
                       || (AGC_ccaInterrupt.Value && AGC_ccaInterruptEnable.Value)
                       || (AGC_rssiDoneInterrupt.Value && AGC_rssiDoneInterruptEnable.Value)
                       || (AGC_rssiHighInterrupt.Value && AGC_rssiHighInterruptEnable.Value)
                       || (AGC_rssiLowInterrupt.Value && AGC_rssiLowInterruptEnable.Value)
                       || (AGC_ccaNotDetectedInterrupt.Value && AGC_ccaNotDetectedInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.InterruptFlags, out IF);
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: IRQ AGC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                AutomaticGainControlIRQ.Set(irq);

                // SYNTH interrupt
                irq = ((SYNTH_readyInterrupt.Value && SYNTH_readyInterruptEnable.Value)
                       || (SYNTH_calibrationDoneInterrupt.Value && SYNTH_calibrationDoneInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    synthesizerRegistersCollection.TryRead((long)SynthesizerRegisters.InterruptFlags, out IF);
                    synthesizerRegistersCollection.TryRead((long)SynthesizerRegisters.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: IRQ SYNTH set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SynthesizerIRQ.Set(irq);

                // LPW0PORTAL interrupt
                int index;
                irq = false;
                for(index = 0; index < LPW0PORTAL_NumberOfInterrupts; index++)
                {
                    if (LPW0PORTAL_interrupt[index].Value && LPW0PORTAL_interruptEnable[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    lpw0PortalRegistersCollection.TryRead((long)Lpw0PortalRegisters.InterruptFlags, out IF);
                    lpw0PortalRegistersCollection.TryRead((long)Lpw0PortalRegisters.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: IRQ LPW0PORTAL set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                Lpw0PortalIRQ.Set(irq);

                //-------------------------------
                // Sequencer core interrupts
                //-------------------------------

                if (sequencer == null || sequencer.IsHalted)
                {
                    return;
                }

                // RAC interrupt
                irq = ((RAC_seqRadioStateChangeInterrupt.Value && RAC_seqRadioStateChangeInterruptEnable.Value)
                       || (RAC_seqStimerCompareEventInterrupt.Value && RAC_seqStimerCompareEventInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ RAC set (IF=0x{1:X}, IEN=0x{2:X})", 
                             this.GetTime(),
                             ((RAC_seqRadioStateChangeInterrupt.Value ? 0x1 : 0) | (RAC_seqStimerCompareEventInterrupt.Value ? 0x2 : 0)),
                             ((RAC_seqRadioStateChangeInterruptEnable.Value ? 0x1 : 0) | (RAC_seqStimerCompareEventInterruptEnable.Value ? 0x2 : 0)));
                }
                SeqRadioControllerIRQ.Set(irq);

                // FRC interrupt
                irq = ((FRC_seqTxDoneInterrupt.Value && FRC_seqTxDoneInterruptEnable.Value)
                       || (FRC_seqTxAfterFrameDoneInterrupt.Value && FRC_seqTxAfterFrameDoneInterruptEnable.Value)
                       || (FRC_seqTxUnderflowInterrupt.Value && FRC_seqTxUnderflowInterruptEnable.Value)
                       || (FRC_seqRxDoneInterrupt.Value && FRC_seqRxDoneInterruptEnable.Value)
                       || (FRC_seqRxAbortedInterrupt.Value && FRC_seqRxAbortedInterruptEnable.Value)
                       || (FRC_seqFrameErrorInterrupt.Value && FRC_seqFrameErrorInterruptEnable.Value)
                       || (FRC_seqRxOverflowInterrupt.Value && FRC_seqRxOverflowInterruptEnable.Value)
                       || (FRC_seqRxRawEventInterrupt.Value && FRC_seqRxRawEventInterruptEnable.Value)
                       || (FRC_seqTxRawEventInterrupt.Value && FRC_seqTxRawEventInterruptEnable.Value)
                       || (FRC_seqPacketBufferStartInterrupt.Value && FRC_seqPacketBufferStartInterruptEnable.Value)
                       || (FRC_seqPacketBufferThresholdInterrupt.Value && FRC_seqPacketBufferThresholdInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.SequencerInterruptFlags, out IF);
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.SequencerInterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ FRC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SeqFrameControllerIRQ.Set(irq);

                // BUFC interrupt
                irq = false;
                Array.ForEach(BUFC_buffer, x => irq |= x.SeqInterrupt);
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    bufferControllerRegistersCollection.TryRead((long)BufferControllerRegisters.SequencerInterruptFlags, out IF);
                    bufferControllerRegistersCollection.TryRead((long)BufferControllerRegisters.SequencerInterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ BUFC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SeqBufferControllerIRQ.Set(irq);

                // PROTIMER interrupt
                irq = (PROTIMER_seqPreCounterOverflowInterrupt.Value && PROTIMER_seqPreCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_seqBaseCounterOverflowInterrupt.Value && PROTIMER_seqBaseCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_seqWrapCounterOverflowInterrupt.Value && PROTIMER_seqWrapCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkSuccessInterrupt.Value && PROTIMER_seqListenBeforeTalkSuccessInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkFailureInterrupt.Value && PROTIMER_seqListenBeforeTalkFailureInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkRetryInterrupt.Value && PROTIMER_seqListenBeforeTalkRetryInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterrupt.Value && PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterruptEnable.Value);
                Array.ForEach(PROTIMER_timeoutCounter, x => irq |= x.SeqInterrupt);
                Array.ForEach(PROTIMER_captureCompareChannel, x => irq |= x.SeqInterrupt);
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IF2 = 0U;
                    var IEN = 0U;
                    var IEN2 = 0U;
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.SequencerInterruptFlags, out IF);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.SequencerInterruptFlags2, out IF2);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.SequencerInterruptEnable, out IEN);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.SequencerInterruptEnable2, out IEN2);
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ PROTIMER set (IF=0x{1:X}, IF2=0x{2:X}, IEN=0x{3:X} IEN2=0x{4:X})", this.GetTime(), IF, IF2, IEN, IEN2);
                }
                SeqProtocolTimerIRQ.Set(irq);

                // MODEM interrupt
                irq = ((MODEM_seqTxFrameSentInterrupt.Value && MODEM_seqTxFrameSentInterruptEnable.Value)
                       || (MODEM_seqTxSyncSentInterrupt.Value && MODEM_seqTxSyncSentInterruptEnable.Value)
                       || (MODEM_seqTxPreambleSentInterrupt.Value && MODEM_seqTxPreambleSentInterruptEnable.Value)
                       || (MODEM_TxRampingDoneInterrupt && MODEM_seqTxRampingDoneInterruptEnable.Value)
                       || (MODEM_seqRxPreambleDetectedInterrupt.Value && MODEM_seqRxPreambleDetectedInterruptEnable.Value)
                       || (MODEM_seqRxFrameWithSyncWord0DetectedInterrupt.Value && MODEM_seqRxFrameWithSyncWord0DetectedInterruptEnable.Value)
                       || (MODEM_seqRxFrameWithSyncWord1DetectedInterrupt.Value && MODEM_seqRxFrameWithSyncWord1DetectedInterruptEnable.Value)
                       || (MODEM_seqRxFrameWithSyncWord2DetectedInterrupt.Value && MODEM_seqRxFrameWithSyncWord2DetectedInterruptEnable.Value)
                       || (MODEM_seqRxFrameWithSyncWord3DetectedInterrupt.Value && MODEM_seqRxFrameWithSyncWord3DetectedInterruptEnable.Value)
                       || (MODEM_seqRxPreambleLostInterrupt.Value && MODEM_seqRxPreambleLostInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IF2 = 0U;
                    var IEN = 0U;
                    var IEN2 = 0U;
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.SequencerInterruptFlags, out IF);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.SequencerInterruptFlags2, out IF2);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.SequencerInterruptEnable, out IEN);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.SequencerInterruptEnable2, out IEN2);
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ MODEM set (IF=0x{1:X}, IF2=0x{2:X}, IEN=0x{3:X} IEN=0x{4:X})", this.GetTime(), IF, IF2, IEN, IEN2);
                }
                SeqModulatorAndDemodulatorIRQ.Set(irq);

                // AGC interrupt
                irq = ((AGC_seqRssiValidInterrupt.Value && AGC_seqRssiValidInterruptEnable.Value)
                       || (AGC_seqCcaInterrupt.Value && AGC_seqCcaInterruptEnable.Value)
                       || (AGC_seqRssiDoneInterrupt.Value && AGC_seqRssiDoneInterruptEnable.Value)
                       || (AGC_seqRssiHighInterrupt.Value && AGC_seqRssiHighInterruptEnable.Value)
                       || (AGC_seqRssiLowInterrupt.Value && AGC_seqRssiLowInterruptEnable.Value)
                       || (AGC_seqCcaNotDetectedInterrupt.Value && AGC_seqCcaNotDetectedInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.SequencerInterruptFlags, out IF);
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.SequencerInterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ AGC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SeqAutomaticGainControlIRQ.Set(irq);

                // SYNTH interrupt
                irq = ((SYNTH_seqReadyInterrupt.Value && SYNTH_seqReadyInterruptEnable.Value)
                       || (SYNTH_seqCalibrationDoneInterrupt.Value && SYNTH_seqCalibrationDoneInterruptEnable.Value));
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    synthesizerRegistersCollection.TryRead((long)SynthesizerRegisters.SequencerInterruptFlags, out IF);
                    synthesizerRegistersCollection.TryRead((long)SynthesizerRegisters.SequencerInterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ SYNTH set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SeqSynthesizerIRQ.Set(irq);

                // HOSTPORTAL interrupt
                irq = false;
                for(index = 0; index < HOSTPORTAL_NumberOfInterrupts; index++)
                {
                    if (HOSTPORTAL_interrupt[index].Value && HOSTPORTAL_interruptEnable[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    hostPortalRegistersCollection.TryRead((long)HostPortalRegisters.InterruptFlags, out IF);
                    hostPortalRegistersCollection.TryRead((long)HostPortalRegisters.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: IRQ HOSTMAILBOX set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SeqHostPortalIRQ.Set(irq);

                // RFMAILBOX interrupt
                irq = false;
                for(index = 0; index < RFMAILBOX_MessageNumber; index++)
                {
                    if (RFMAILBOX_messageInterrupt[index].Value && RFMAILBOX_messageInterruptEnable[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if (LogInterrupts && irq)
                {
                    var IF = 0U;
                    var IEN = 0U;
                    rfMailboxRegistersCollection.TryRead((long)RfMailboxRegisters.InterruptFlags, out IF);
                    rfMailboxRegistersCollection.TryRead((long)RfMailboxRegisters.InterruptEnable, out IEN);
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ RFMAILBOX set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), IF, IEN);
                }
                SeqRfMailboxIRQ.Set(irq);

                // Sequencer Radio State Machine interrupts
                irq = RAC_seqStateOffInterrupt.Value && RAC_seqStateOffInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ OFF set", this.GetTime());
                }
                SeqOffIRQ.Set(irq);

                irq = RAC_seqStateRxWarmInterrupt.Value && RAC_seqStateRxWarmInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ RX_WARM set", this.GetTime());
                }
                SeqRxWarmIRQ.Set(irq);
            
                irq = RAC_seqStateRxSearchInterrupt.Value && RAC_seqStateRxSearchInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ RX_SEARCH set", this.GetTime());
                }
                SeqRxSearchIRQ.Set(irq);

                irq = RAC_seqStateRxFrameInterrupt.Value && RAC_seqStateRxFrameInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ RX_FRAME set", this.GetTime());
                }                
                SeqRxFrameIRQ.Set(irq);

                irq = RAC_seqStateRxWrapUpInterrupt.Value && RAC_seqStateRxWrapUpInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ RX_WRAPUP set", this.GetTime());
                }
                SeqRxWrapUpIRQ.Set(irq);

                irq = RAC_seqStateTxWarmInterrupt.Value && RAC_seqStateTxWarmInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ TX_WARM set", this.GetTime());
                }
                SeqTxWarmIRQ.Set(irq);

                irq = RAC_seqStateTxInterrupt.Value && RAC_seqStateTxInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ TX set", this.GetTime());
                }
                SeqTxIRQ.Set(irq);

                irq = RAC_seqStateTxWrapUpInterrupt.Value && RAC_seqStateTxWrapUpInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ TX_WRAPUP set", this.GetTime());
                }
                SeqTxWrapUpIRQ.Set(irq);

                irq = RAC_seqStateShutDownInterrupt.Value && RAC_seqStateShutDownInterruptEnable.Value;
                if (LogInterrupts && irq)
                {
                    this.Log(LogLevel.Info, "{0}: SEQ_IRQ SHUTDOWN set", this.GetTime());
                }
                SeqShutdownIRQ.Set(irq);
            });
        }

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

#region FRC methods
        private int FRC_ActiveTransmitFrameDescriptor
        {
            // ACTIVETXFCD == 0 => FCD0
            // ACTIVETXFCD == 1 => FCD1
            get
            {
                return FRC_activeTransmitFrameDescriptor.Value ? 1 : 0;
            }
            set
            {
                if(value != 0 && value != 1)
                {
                    throw new Exception("Setting illegal FRC_ActiveTransmitFrameDescriptor value.");
                }

                FRC_activeTransmitFrameDescriptor.Value = (value == 1);
            }
        }

        private int FRC_ActiveReceiveFrameDescriptor
        {
            // ACTIVERXFCD == 0 => FCD2
            // ACTIVERXFCD == 1 => FCD3
            get
            {
                return FRC_activeReceiveFrameDescriptor.Value ? 3 : 2;
            }
            set
            {
                if(value != 2 && value != 3)
                {
                    throw new Exception("Setting illegal FRC_ActiveReceiveFrameDescriptor value.");
                }

                FRC_activeReceiveFrameDescriptor.Value = (value == 3);
            }
        }

        private void FRC_UpdateRawMode()
        {
            if(!FRC_enableRawDataRandomNumberGenerator.Value || FRC_rxRawBlocked.Value || RAC_currentRadioState != RAC_RadioState.RxSearch)
            {
                return;
            }

            switch(FRC_rxRawDataSelect.Value) {
                case FRC_RxRawDataMode.SingleItem:
                    if(FRC_rxRawDataTriggerSelect.Value == FRC_RxRawDataTriggerMode.Immediate)
                    {
                        FRC_rxRawEventInterrupt.Value = true;
                        FRC_rxRawBlocked.Value = true;
                    }
                break;
                default:
                    return;
            }
            UpdateInterrupts();
        }

        private void FRC_CheckPacketCaptureBufferThreshold()
        {
            if (FRC_packetBufferThresholdEnable.Value 
                && FRC_packetBufferCount.Value >= FRC_packetBufferThreshold.Value)
            {
                FRC_packetBufferThresholdInterrupt.Value = true;
                FRC_seqPacketBufferThresholdInterrupt.Value = true;
            }
        }

        private void FRC_RxAbortCommand()
        {                                
            // CMD_RXABORT takes effect when FRC is active. When RAC in RXWARM and RXSEARCH state, 
            // since FRC is still in IDLE state this command doesn't do anything. 
            if (RAC_currentRadioState != RAC_RadioState.RxFrame)
            {
                return;
            }

            // When set, the active receive BUFC buffer is restored for received frames that trigger the RXABORTED interrupt flag. This
            // means that the WRITEOFFSET is restored to the value prior to receiving this frame. READOFFSET is not modified.
            if (FRC_rxBufferRestoreOnRxAborted.Value)
            {
                FRC_RestoreRxDescriptorsBufferWriteOffset();
            }

            // RENODE-354
            //if (TRACKABFRAME)
            //{
                // TODO
            //}
            
            FRC_rxAbortedInterrupt.Value = true;
            FRC_seqRxAbortedInterrupt.Value = true;
            RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxAbort);
            UpdateInterrupts();
        }

        private byte[] FRC_AssembleFrame()
        {
            var frame = Enumerable.Empty<byte>();

            // MODEM_CONTROL1->SYNCDATA Defines if the sync word is part of the transmit payload or not.
            // If not, modulator adds SYNC in transmit.
            if(!MODEM_syncData.Value)
            {
                frame = frame.Concat(BitHelper.GetBytesFromValue(MODEM_TxSyncWord, (int)MODEM_SyncWordBytes, reverse: true));
            }

            var descriptor = FRC_frameDescriptor[FRC_ActiveTransmitFrameDescriptor];
            var frameLength = 0u;
            var dynamicFrameLength = true;

            switch(FRC_dynamicFrameLengthMode.Value)
            {
                case FRC_DynamicFrameLengthMode.Disable:
                    dynamicFrameLength = false;
                    break;
                case FRC_DynamicFrameLengthMode.SingleByte:
                case FRC_DynamicFrameLengthMode.SingleByteMSB:
                    frameLength = BUFC_buffer[descriptor.BufferIndex].Peek((uint)FRC_lengthFieldLocation.Value);
                    break;
                case FRC_DynamicFrameLengthMode.DualByteLSBFirst:
                case FRC_DynamicFrameLengthMode.DualByteMSBFirst:
                    frameLength = ((BUFC_buffer[descriptor.BufferIndex].Peek((uint)FRC_lengthFieldLocation.Value + 1) << 8)
                                              | (BUFC_buffer[descriptor.BufferIndex].Peek((uint)FRC_lengthFieldLocation.Value)));

                    break;
                default:
                    this.Log(LogLevel.Error, "Unimplemented DFL mode.");
                    this.Log(LogLevel.Error, "Failed to assemble a frame, partial frame: {0}", BitConverter.ToString(frame.ToArray()));
                    return new byte[0];
            }
            
            if(dynamicFrameLength && !FRC_TrySetFrameLength(frameLength))
            {
                this.Log(LogLevel.Error, "Failed to assemble a frame, partial frame: {0}", BitConverter.ToString(frame.ToArray()));
                return new byte[0];
            }

            FRC_wordCounter.Value = 0;
            for(var subframe = 0; FRC_wordCounter.Value < FRC_FrameLength; ++subframe)
            {
                var crcLength = (descriptor.includeCrc.Value && dynamicFrameLength && FRC_dynamicFrameCrcIncluded.Value) ? CRC_CrcWidth : 0;
                // Assemble subframe
                var length = (uint)(FRC_FrameLength - FRC_wordCounter.Value);
                if(descriptor.Words.HasValue)
                {
                    length = Math.Min(length, descriptor.Words.Value);
                }

                length -= crcLength;
                if(length < 0)
                {
                    this.Log(LogLevel.Error, "adding crc would exceed DFL");
                    this.Log(LogLevel.Error, "Failed to assemble a frame, partial frame: {0}", BitConverter.ToString(frame.ToArray()));
                    return new byte[0];
                }
                if(!BUFC_buffer[descriptor.BufferIndex].TryReadBytes(length, out var payload))
                {
                    this.Log(LogLevel.Error, "Read only {0} bytes of {1}, total length={2}", payload.Length, length, FRC_FrameLength);
                    this.Log(LogLevel.Error, "Failed to assemble a frame, partial frame: {0}", BitConverter.ToString(frame.Concat(payload).ToArray()));
                    FRC_txUnderflowInterrupt.Value = true;
                    return new byte[0];
                }
                frame = frame.Concat(payload);
                FRC_wordCounter.Value += length;

                if(descriptor.includeCrc.Value)
                {
                    frame = frame.Concat(CRC_CalculateCRC());
                    FRC_wordCounter.Value += crcLength;
                }

                // Select next frame descriptor
                switch(FRC_txFrameDescriptorMode.Value)
                {
                    case FRC_FrameDescriptorMode.FrameDescriptorMode0:
                    case FRC_FrameDescriptorMode.FrameDescriptorMode3:
                        break;
                    case FRC_FrameDescriptorMode.FrameDescriptorMode1:
                        FRC_ActiveTransmitFrameDescriptor = subframe % 2;
                        break;
                    case FRC_FrameDescriptorMode.FrameDescriptorMode2:
                        FRC_ActiveTransmitFrameDescriptor = 1;
                        break;
                }
                descriptor = FRC_frameDescriptor[FRC_ActiveTransmitFrameDescriptor];
            }

            // Prepare for next frame
            switch(FRC_txFrameDescriptorMode.Value)
            {
                case FRC_FrameDescriptorMode.FrameDescriptorMode0:
                case FRC_FrameDescriptorMode.FrameDescriptorMode1:
                case FRC_FrameDescriptorMode.FrameDescriptorMode2:
                    FRC_ActiveTransmitFrameDescriptor = 0;
                    break;
                case FRC_FrameDescriptorMode.FrameDescriptorMode3:
                    FRC_ActiveTransmitFrameDescriptor = 1;
                    break;
            }

            this.Log(LogLevel.Noisy, "Frame assembled: {0}", BitConverter.ToString(frame.ToArray()));
            return frame.ToArray();
        }

        private void FRC_SaveRxDescriptorsBufferWriteOffset()
        {
            // Descriptors 2 and 3 are used for RX.
            BUFC_buffer[FRC_frameDescriptor[2].BufferIndex].UpdateWriteStartOffset();
            BUFC_buffer[FRC_frameDescriptor[3].BufferIndex].UpdateWriteStartOffset();
        }

        private void FRC_RestoreRxDescriptorsBufferWriteOffset()
        {
            // Descriptors 2 and 3 are used for RX.
            BUFC_buffer[FRC_frameDescriptor[2].BufferIndex].UpdateWriteStartOffset();
            BUFC_buffer[FRC_frameDescriptor[3].BufferIndex].UpdateWriteStartOffset();
        }

        private void FRC_DisassembleCurrentFrame(bool forceCrcError)
        {
            var frameLength = 0u;
            var dynamicFrameLength = true;
            switch(FRC_dynamicFrameLengthMode.Value)
            {
                case FRC_DynamicFrameLengthMode.Disable:
                    dynamicFrameLength = false;
                    break;
                case FRC_DynamicFrameLengthMode.SingleByte:
                case FRC_DynamicFrameLengthMode.SingleByteMSB:
                    frameLength = (uint)currentFrame[currentFrameOffset + FRC_lengthFieldLocation.Value];
                    break;
                case FRC_DynamicFrameLengthMode.DualByteLSBFirst:
                case FRC_DynamicFrameLengthMode.DualByteMSBFirst:
                    frameLength = (uint)currentFrame[currentFrameOffset + FRC_lengthFieldLocation.Value + 1] << 8
                                  | (uint)currentFrame[currentFrameOffset + FRC_lengthFieldLocation.Value];
                    break;
                default:
                    this.Log(LogLevel.Error, "Unimplemented DFL mode.");
                    return;
            }
            if(dynamicFrameLength && !FRC_TrySetFrameLength(frameLength))
            {
                this.Log(LogLevel.Error, "DisassembleFrame FRAMEERROR");
                return; // FRAMEERROR
            }

            FRC_wordCounter.Value = 0;
            for(var subframe = 0; FRC_wordCounter.Value < FRC_FrameLength; ++subframe)
            {
                var descriptor = FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor];
                var startingWriteOffset = BUFC_buffer[descriptor.BufferIndex].WriteOffset;

                // Assemble subframe
                var length = FRC_FrameLength - FRC_wordCounter.Value;
                if(descriptor.Words.HasValue)
                {
                    length = Math.Min(length, descriptor.Words.Value);
                }
                else if(dynamicFrameLength && descriptor.includeCrc.Value && FRC_dynamicFrameCrcIncluded.Value)
                {
                    length = checked(length - CRC_CrcWidth); // TODO: what happends when length < CrcWidth?
                }
                if(currentFrameOffset + length > (uint)currentFrame.Length)
                {
                    this.Log(LogLevel.Error, "frame too small, payload");
                    return;
                }
                var payload = currentFrame.Skip((int)currentFrameOffset).Take((int)length);
                var skipCount = (FRC_wordCounter.Value < FRC_packetBufferStartAddress.Value)
                                ? (FRC_packetBufferStartAddress.Value - FRC_wordCounter.Value) : 0;
                var pktCaptureBuff = currentFrame.Skip((int)(currentFrameOffset + skipCount)).Take((int)(length - skipCount));
                currentFrameOffset += (uint)length;
                FRC_wordCounter.Value += length;

                FRC_WritePacketCaptureBuffer(pktCaptureBuff.ToArray());

                if(!BUFC_buffer[descriptor.BufferIndex].TryWriteBytes(payload.ToArray(), out var written))
                {
                    this.Log(LogLevel.Error, "Written only {0} bytes from {1}", written, length);
                    if(BUFC_buffer[descriptor.BufferIndex].overflow.Value)
                    {
                        FRC_rxOverflowInterrupt.Value = true;
                        FRC_seqRxOverflowInterrupt.Value = true;
                        UpdateInterrupts();
                        RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxOverflow);
                    }
                }
                else if(descriptor.includeCrc.Value)
                {
                    var crc = currentFrame.Skip((int)currentFrameOffset).Take((int)CRC_CrcWidth).ToArray();
                    // TODO: Check CRC
                    // FRC_frameErrorInterrupt.Value |= crc check failed
                    currentFrameOffset += (uint)crc.Length;
                    if(crc.Length != CRC_CrcWidth)
                    {
                        this.Log(LogLevel.Error, "frame too small, crc");
                    }
                    if(dynamicFrameLength && FRC_dynamicFrameCrcIncluded.Value)
                    {
                        FRC_wordCounter.Value += (uint)crc.Length;
                    }
                    if(FRC_rxStoreCrc.Value)
                    {
                        if(!BUFC_buffer[descriptor.BufferIndex].TryWriteBytes(crc, out written))
                        {
                            this.Log(LogLevel.Error, "Written only {0} bytes from {1}", written, crc.Length);
                            if(BUFC_buffer[descriptor.BufferIndex].overflow.Value)
                            {
                                FRC_rxOverflowInterrupt.Value = true;
                                FRC_seqRxOverflowInterrupt.Value = true;
                                UpdateInterrupts();
                                RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxOverflow);
                            }
                        }
                    }
                }
                
                // Select next frame descriptor
                switch(FRC_rxFrameDescriptorMode.Value)
                {
                    case FRC_FrameDescriptorMode.FrameDescriptorMode0:
                    case FRC_FrameDescriptorMode.FrameDescriptorMode3:
                        break;
                    case FRC_FrameDescriptorMode.FrameDescriptorMode1:
                        FRC_ActiveReceiveFrameDescriptor = 2 + subframe % 2;
                        break;
                    case FRC_FrameDescriptorMode.FrameDescriptorMode2:
                        FRC_ActiveReceiveFrameDescriptor = 3;
                        break;
                }
            }

            // RX TRAIL DATA
            {
                // Appended ascending order of field's bit index
                var descriptor = FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor];
                if(FRC_rxAppendRssi.Value)
                {
                    // AGC RSSI register value? or RSSIINT field?
                    // RSSI value [...] represented as a signed 2-complement integer dB number.
                    BUFC_buffer[descriptor.BufferIndex].WriteData = 0x0;
                }
                if(FRC_rxAppendStatus.Value)
                {
                    BUFC_buffer[descriptor.BufferIndex].WriteData = (forceCrcError) ? 0x00U : 0x80U;
                }
                if (FRC_rxAppendProtimerCc0BaseLow.Value)
                {
                    BUFC_buffer[descriptor.BufferIndex].WriteData = ((uint)PROTIMER_captureCompareChannel[0].baseValue.Value & 0xFF);
                    BUFC_buffer[descriptor.BufferIndex].WriteData = (((uint)PROTIMER_captureCompareChannel[0].baseValue.Value >> 8) & 0xFF);
                }
                if (FRC_rxAppendProtimerCc0BaseHigh.Value)
                {
                    BUFC_buffer[descriptor.BufferIndex].WriteData = (((uint)PROTIMER_captureCompareChannel[0].baseValue.Value >> 16) & 0xFF);
                    BUFC_buffer[descriptor.BufferIndex].WriteData = (((uint)PROTIMER_captureCompareChannel[0].baseValue.Value >> 24) & 0xFF);
                }
                if(FRC_rxAppendProtimerCc0WrapLow.Value)
                {
                    BUFC_buffer[descriptor.BufferIndex].WriteData = ((uint)PROTIMER_captureCompareChannel[0].wrapValue.Value & 0xFF);
                    BUFC_buffer[descriptor.BufferIndex].WriteData = (((uint)PROTIMER_captureCompareChannel[0].wrapValue.Value >> 8) & 0xFF);
                }
                if(FRC_rxAppendProtimerCc0WrapHigh.Value)
                {
                    BUFC_buffer[descriptor.BufferIndex].WriteData = (((uint)PROTIMER_captureCompareChannel[0].wrapValue.Value >> 16) & 0xFF);
                    BUFC_buffer[descriptor.BufferIndex].WriteData = (((uint)PROTIMER_captureCompareChannel[0].wrapValue.Value >> 24) & 0xFF);
                }
            }

            // Prepare for next frame
            switch(FRC_rxFrameDescriptorMode.Value)
            {
                case FRC_FrameDescriptorMode.FrameDescriptorMode0:
                case FRC_FrameDescriptorMode.FrameDescriptorMode1:
                case FRC_FrameDescriptorMode.FrameDescriptorMode2:
                    FRC_ActiveReceiveFrameDescriptor = 2;
                    break;
                case FRC_FrameDescriptorMode.FrameDescriptorMode3:
                    FRC_ActiveReceiveFrameDescriptor = 3;
                    break;
            }

            this.Log(LogLevel.Noisy, "Frame disassembled, frame descriptor now is {0}", FRC_ActiveReceiveFrameDescriptor);
        }

        private bool FRC_TrySetFrameLength(uint frameLength)
        {
            switch(FRC_dynamicFrameLengthMode.Value)
            {
                case FRC_DynamicFrameLengthMode.SingleByte:
                case FRC_DynamicFrameLengthMode.DualByteLSBFirst:
                    break;
                case FRC_DynamicFrameLengthMode.SingleByteMSB:
                case FRC_DynamicFrameLengthMode.DualByteMSBFirst:
                    frameLength = (frameLength >> 8) | ((frameLength & 0xFF) << 8);
                    break;
                default:
                    this.Log(LogLevel.Error, "Invalid DFL mode.");
                    return false;
            }
            frameLength >>= (byte)FRC_dynamicFrameLengthBitShift.Value;
            frameLength &= (1u << (byte)FRC_dynamicFrameLengthBits.Value) - 1;
            frameLength += (uint)FRC_dynamicFrameLengthOffset.Value; // TODO signed 2's complement
            if(frameLength < FRC_minDecodedLength.Value || FRC_maxDecodedLength.Value < frameLength)
            {
                FRC_frameErrorInterrupt.Value = true;
                FRC_seqFrameErrorInterrupt.Value = true;
                UpdateInterrupts();
                return false;
            }

            FRC_frameLength.Value = frameLength;
            return true;
        }

        private bool FRC_CheckSyncWords(byte[] frame)
        {
            if(frame.Length < (int)MODEM_SyncWordBytes)
            {
                return false;
            }
            var syncWord = BitHelper.ToUInt32(frame, 0, (int)MODEM_SyncWordBytes, true);
            var foundSyncWord0 = (syncWord == MODEM_SyncWord0);
            var foundSyncWord1 = (MODEM_dualSync.Value && (syncWord == MODEM_SyncWord1));
            var foundSyncWord2 = (MODEM_syncDetect2Th.Value && (syncWord == MODEM_SyncWord2));
            var foundSyncWord3 = (MODEM_syncDetect2Th.Value && MODEM_dualSync2Th.Value && (syncWord == MODEM_SyncWord3));
            
            if(!foundSyncWord0 && !foundSyncWord1 && !foundSyncWord2 && !foundSyncWord3)
            {
                return false;
            }

            MODEM_rxPreambleDetectedInterrupt.Value = true;
            MODEM_seqRxPreambleDetectedInterrupt.Value = true;
            
            // PROTIMER request signals only have SYNC0/SYNC1 related events
            if(foundSyncWord0)
            {
                PROTIMER_TriggerEvent(PROTIMER_Event.Syncword0Detected);
            }
            if(foundSyncWord1)
            {
                PROTIMER_TriggerEvent(PROTIMER_Event.Syncword1Detected);
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.Syncword0Or1Detected);
            
            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].enable.Value && PROTIMER_captureCompareChannel[i].mode.Value == PROTIMER_CaptureCompareMode.Capture)
                {
                    var triggered = false;
                    switch(PROTIMER_captureCompareChannel[i].captureInputSource.Value)
                    {
                        case PROTIMER_CaptureInputSource.DemodulatorFoundSyncWord0:
                            triggered |= foundSyncWord0;
                            break;
                        case PROTIMER_CaptureInputSource.DemodulatorFoundSyncWord1:
                            triggered |= foundSyncWord1;
                            break;
                        case PROTIMER_CaptureInputSource.DemodulatorFoundSyncWord2:
                            triggered |= foundSyncWord2;
                            break;
                        case PROTIMER_CaptureInputSource.DemodulatorFoundSyncWord3:
                            triggered |= foundSyncWord3;
                            break;
                        case PROTIMER_CaptureInputSource.DemodulatorFoundAnySyncWord:
                            triggered = true;
                            break;
                    }
                    if(triggered)
                    {
                        PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                        PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                    }
                }
            }
            
            MODEM_rxFrameWithSyncWord0DetectedInterrupt.Value |= foundSyncWord0;
            MODEM_seqRxFrameWithSyncWord0DetectedInterrupt.Value = MODEM_rxFrameWithSyncWord0DetectedInterrupt.Value;
            MODEM_rxFrameWithSyncWord1DetectedInterrupt.Value |= foundSyncWord1;
            MODEM_seqRxFrameWithSyncWord1DetectedInterrupt.Value = MODEM_rxFrameWithSyncWord1DetectedInterrupt.Value;
            MODEM_rxFrameWithSyncWord2DetectedInterrupt.Value |= foundSyncWord2;
            MODEM_seqRxFrameWithSyncWord2DetectedInterrupt.Value = MODEM_rxFrameWithSyncWord2DetectedInterrupt.Value;
            MODEM_rxFrameWithSyncWord3DetectedInterrupt.Value |= foundSyncWord3;
            MODEM_seqRxFrameWithSyncWord3DetectedInterrupt.Value = MODEM_rxFrameWithSyncWord3DetectedInterrupt.Value;
            
            if (foundSyncWord0)
            {
                MODEM_frameDetectedId.Value = 0;
            }
            else if (foundSyncWord1)
            {
                MODEM_frameDetectedId.Value = 1;
            }
            else if (foundSyncWord2)
            {
                MODEM_frameDetectedId.Value = 2;
            }
            else if (foundSyncWord3)
            {
                MODEM_frameDetectedId.Value = 3;
            }
            UpdateInterrupts();

            return true;
        }

        private void FRC_WritePacketCaptureBuffer(byte[] data)
        {
            if (FRC_packetBufferCount.Value > FRC_PacketBufferCaptureSize)
            {
                throw new Exception("FRC_packetBufferCount exceeded max value!");
            }

            for(var i = 0; i < data.Length; i++)
            {
                if (FRC_packetBufferCount.Value == FRC_PacketBufferCaptureSize)
                {
                    break;
                }

                FRC_packetBufferCapture[FRC_packetBufferCount.Value] = data[i];
                FRC_packetBufferCount.Value++;

                if (FRC_packetBufferCount.Value == 1)
                {
                    FRC_packetBufferStartInterrupt.Value = true;
                    FRC_seqPacketBufferStartInterrupt.Value = true;
                    UpdateInterrupts();
                }
            }
        }
#endregion

#region RAC methods
        private void RAC_TxTimerLimitReached()
        {
            txTimer.Enabled = false;

            RAC_ClearOngoingTx();

            MODEM_txFrameSentInterrupt.Value = true;
            MODEM_seqTxFrameSentInterrupt.Value = true;
            FRC_txDoneInterrupt.Value = true;
            FRC_seqTxDoneInterrupt.Value = true;

            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].enable.Value && PROTIMER_captureCompareChannel[i].mode.Value == PROTIMER_CaptureCompareMode.Capture)
                {
                    switch(PROTIMER_captureCompareChannel[i].captureInputSource.Value)
                    {
                        case PROTIMER_CaptureInputSource.TxDone:
                        case PROTIMER_CaptureInputSource.TxOrRxDone:
                            PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                            PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                            break;
                    }
                }
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.TxDone);
            PROTIMER_TriggerEvent(PROTIMER_Event.TxOrRxDone);

            RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxDone);
        }

        private void RAC_RxTimerLimitReached()
        {
            rxTimer.Enabled = false;

            // We went through the preamble and syncword delay which was scheduled in ReceiveFrame(). 
            // We can now check radio state and syncword.
            if (RAC_internalRxState == RAC_InternalRxState.PreambleAndSyncWord)
            {
                if(RAC_currentRadioState != RAC_RadioState.RxSearch)
                {
                    RAC_ClearOngoingRx();
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping (not in RXSEARCH): at {0} (channel {1}): {2}", GetTime(), Channel, BitConverter.ToString(currentFrame));
                    return;
                }

                if (RAC_ongoingRxCollided)
                {
                    RAC_ClearOngoingRx();
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping (PRE/SYNC COLLISION): at {0} (channel {1}): {2}", GetTime(), Channel, BitConverter.ToString(currentFrame));
                    return;
                }

                if(!FRC_CheckSyncWords(currentFrame))
                {
                    RAC_ClearOngoingRx();
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping (SYNC MISMATCH): at {0} (channel {1}): {2}", GetTime(), Channel, BitConverter.ToString(currentFrame));
                    return;
                }

                RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.FrameDetected);
                RAC_internalRxState = RAC_InternalRxState.Frame;
                FRC_SaveRxDescriptorsBufferWriteOffset();
                currentFrameOffset = MODEM_SyncWordBytes;
                
                // After having transitioned to RX_FRAME as result of the "beginning" of a packet being 
                // received, we stay in RX_FRAME for the duration of the transmission.
                // We don't include the preamble time and sync word time here since we already delayed for that 
                // (and possibly some extra time indicated in RAC_rxTimeAlreadyPassedUs).
                // We add the rxDoneChainDelay here so we correctly delay the RXDONE signal.
                double overTheAirFrameTimeUs = MODEM_GetFrameOverTheAirTimeUs(currentFrame, false, false);
                double rxDoneDelayUs = MODEM_GetRxDoneDelayUs();
                if (overTheAirFrameTimeUs + rxDoneDelayUs > RAC_rxTimeAlreadyPassedUs
                    && PROTIMER_UsToPreCntOverflowTicks(overTheAirFrameTimeUs + rxDoneDelayUs - RAC_rxTimeAlreadyPassedUs) > 0)
                {
                    this.Log(LogLevel.Noisy, "scheduled rxTimer for FRAME: {0}us OTA={1} rxDoneDelay={2} rxTimeAlreadyPassed={3}", 
                             overTheAirFrameTimeUs + rxDoneDelayUs - RAC_rxTimeAlreadyPassedUs, overTheAirFrameTimeUs, rxDoneDelayUs, RAC_rxTimeAlreadyPassedUs);
                    rxTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
                    rxTimer.Limit = PROTIMER_UsToPreCntOverflowTicks(overTheAirFrameTimeUs + rxDoneDelayUs - RAC_rxTimeAlreadyPassedUs);
                    rxTimer.Enabled = true;
                    return;
                }

                // If the time we already spent in RX because of the quantum time already accounts 
                // for the whole frame, we fall through and process the end of RX immediately.
            }
            
            // Packet is fully received now and we have already delayed for the RX done chain delay.

            if (RAC_internalRxState != RAC_InternalRxState.Frame)
            {
                throw new Exception("RAC_RxTimerLimitReached: unexpected RX state");
            }

            RAC_internalRxState = RAC_InternalRxState.Idle;
            
            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].enable.Value && PROTIMER_captureCompareChannel[i].mode.Value == PROTIMER_CaptureCompareMode.Capture)
                {
                    switch(PROTIMER_captureCompareChannel[i].captureInputSource.Value)
                    {
                        case PROTIMER_CaptureInputSource.RxAtEndOfFrameFromDemodulator:
                        case PROTIMER_CaptureInputSource.RxDone:
                        case PROTIMER_CaptureInputSource.TxOrRxDone:
                            PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                            PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                            break;
                    }
                }
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.RxDone);
            PROTIMER_TriggerEvent(PROTIMER_Event.TxOrRxDone);

            if (RAC_ongoingRxCollided)
            {
                // The received frame collided with another one (that we already dropped). Here we are simulating a CRC error.
                FRC_frameErrorInterrupt.Value = true;
                FRC_seqFrameErrorInterrupt.Value = true;
                
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping at {0}: RX collision", GetTime());

                if(FRC_rxBufferRestoreOnFrameError.Value & !FRC_rxAcceptCrcErrors.Value)
                {
                    FRC_RestoreRxDescriptorsBufferWriteOffset();
                }
            }

            // When a CRC error is detected, the FRAMEERROR interrupt flag is set. Normally, frames with CRC errors are discarded. 
            // However, by setting ACCEPTCRCERRORS in FRC_RXCTRL, frames with CRC errors are still accepted and a RXDONE interrupt 
            // is generated at the end of the frame reception, in addition to the FRAMERROR interrupt. 
            if (!RAC_ongoingRxCollided || FRC_rxAcceptCrcErrors.Value)
            {
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Received at {0} on channel {1} (RSSI={2}, collided={3}): {4}", 
                         GetTime(), Channel, AGC_FrameRssiIntegerPart, RAC_ongoingRxCollided, BitConverter.ToString(currentFrame));

                // No collision detected or ACCEPTCRCERRORS is set: disassemble the incoming frame and raise the RXDONE interrupt.
                FRC_DisassembleCurrentFrame(RAC_ongoingRxCollided);
            
                FRC_rxDoneInterrupt.Value = true;
                FRC_seqRxDoneInterrupt.Value = true;
            }

            UpdateInterrupts();
            RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxDone);
        }

        private void RAC_ChangeRadioState(RAC_RadioState newState)
        {
            if (newState != RAC_currentRadioState)
            {
                RAC_previous3RadioState = RAC_previous2RadioState;
                RAC_previous2RadioState = RAC_previous1RadioState;
                RAC_previous1RadioState = RAC_currentRadioState;
                RAC_currentRadioState = newState;

                if (RAC_currentRadioState == RAC_RadioState.Tx)
                {
                    // As soon as state TX is entered, TXEN is reset.
                    RAC_TxEnable = false;
                }
            }
        }

        private void RAC_ClearOngoingTxOrRx()
        {
            RAC_ClearOngoingRx();
            RAC_ClearOngoingTx();
        }

        private void RAC_ClearOngoingRx()
        {
            FRC_rxFrameExitPending = false;
            FRC_rxDonePending = false;
            rxTimer.Enabled = false;
            RAC_ongoingRxCollided = false;
            RAC_internalRxState = RAC_InternalRxState.Idle;
        }

        private void RAC_ClearOngoingTx()
        {
            txTimer.Enabled = false;
            if (RAC_internalTxState != RAC_InternalTxState.Idle)
            {
                InterferenceQueue.Remove(this);
            }
            RAC_internalTxState = RAC_InternalTxState.Idle;
        }

        private void RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal signal = RAC_RadioStateMachineSignal.None)
        {
            this.Log(LogLevel.Info, "RAC_UpdateRadioStateMachine signal={0} current state={1} at {2}", signal, RAC_currentRadioState, GetTime());

            machine.ClockSource.ExecuteInLock(delegate {            
                RAC_RadioState previousState = RAC_currentRadioState;

                // Super State Transition Priority
                // 1. RESET
                // 2. Sequencer breakpoint triggered
                // 3. FORCESTATE
                // 4. FORCEDISABLE
                // 5. FORCETX
                if (signal == RAC_RadioStateMachineSignal.Reset)
                {
                    RAC_ClearOngoingTxOrRx();
                    // This should go through PowerOnReset->Shutdown but there is really no difference in going to Shutdown directly.
                    RAC_ChangeRadioState(RAC_RadioState.Shutdown);
                }
                //else if (sequencer breakpoint triggered)
                //{
                    // When a sequencer breakpoint is triggered, the RSM will not change state. This allows debugging and
                    // single stepping to be performed, without state transitions changing the sequencer program flow.
                //}
                else if (RAC_forceStateActive.Value)
                {
                    RAC_ClearOngoingTxOrRx();
                    RAC_ChangeRadioState(RAC_forceStateTransition.Value);
                    RAC_forceStateActive.Value = false;
                }
                else if (RAC_forceDisable.Value && RAC_currentRadioState == RAC_RadioState.Off)
                {
                    // The RSM remains in OFF as long as the FORCEDISABLE bit is set.
                } 
                else if (RAC_forceDisable.Value && RAC_currentRadioState != RAC_RadioState.Shutdown)
                {
                    RAC_ClearOngoingTxOrRx();
                    RAC_ChangeRadioState(RAC_RadioState.Shutdown);
                }
                
                // FORCETX will make the RSM enter TX. If already in TX, RSM is first entered to TXWRAPUP (with TXEN set which will then imply 
                // to have TX state afterwards). For any other state, the transition goes through the TXWARM state. The FORCETX is active by 
                // issuing the FORCETX command in RAC_CMD or by triggering the Peripheral Reflex System (PRS). PRS triggering is configured 
                // by setting RAC_CTRL_PRSFORCETX and selecting the RAC PRS consumer input FORCETX (bit 0 of RAC PRS input bus).
                else if (signal == RAC_RadioStateMachineSignal.ForceTx /* TODO: || (PRSFORCETX && PRSFORCETXSEL)*/)
                {
                    if (RAC_currentRadioState == RAC_RadioState.Tx)
                    {
                        RAC_ClearOngoingTx();
                        RAC_ChangeRadioState(RAC_RadioState.TxWrapUp);
                    }
                    else 
                    {
                        RAC_ChangeRadioState(RAC_RadioState.TxWarm);
                    }
                }
                else
                {
                    switch(RAC_currentRadioState)
                    {
                        case RAC_RadioState.Shutdown:
                        {
                            // This state is entered from POR state, or once the RAC_CTRL_FORCEDISABLE is set (and RSM is not already in OFF). In this state 
                            // all radio modules are abruptly shut down (e.g. PA down-ramping is not performed). Subsequently, the OFF state is entered if 
                            // RAC_CTRL_EXITSHUTDOWNDIS is cleared and the RSM will remain in OFF as long as the FORCEDISABLE bit is set. 
                            // Before exiting that state, RAC_SEQIF_STATESHUTDOWN needs to be cleared, and RAC_SEQEND_STATESHUTDOWN needs to be cleared also 
                            // (in case RAC_SEQENDEN_STATESHUTDOWN is set).                            
                            if (!RAC_seqStateShutDownInterrupt.Value
                                && (!RAC_seqStateShutdownEndEnable.Value || !RAC_seqStateShutdownEnd.Value)
                                && !RAC_exitShutdownDisable.Value)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Off);
                            }
                            break;
                        }
                        case RAC_RadioState.Off:
                        {
                            // In this state the radio is shut down. The crystal oscillator can be running, as it is controlled by the CMU. This state is normally exited 
                            // by enabling transmission (TXEN) or reception (RXEN).
                            // RAC_CTRL_FORCEDISABLE bit and RAC_SEQEND_STATEOFF (if RAC_SEQENDEN_STATEOFF is set) must be cleared to leave this state, 
                            // regardless of TXEN and RXEN level. FORCESTATE and FORCETX can also make the RSM leave the OFF state.
                            if (!RAC_forceDisable.Value
                                && (!RAC_seqStateOffEndEnable.Value || !RAC_seqStateOffEnd.Value))
                            {
                                if (signal == RAC_RadioStateMachineSignal.TxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.TxWarm);
                                } else if (RAC_RxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.RxWarm);
                                }
                            }
                            break;
                        }
                        case RAC_RadioState.TxWarm:
                        {
                            // In this state the transmitter is enabled and calibrated. Upon completion of the TXWARM state, the RSM proceeds automatically to TX, 
                            // unless the CLEARTXEN or TXDIS commands in RAC_CMD have been issued, in which case the RSM enters the TXWRAPUP state. 
                            // RAC_SEQIF_STATETXWARM and RAC_SEQEND_STATETXWARM (if RAC_SEQENDEN_STATETXWARM is set) needs to be cleared to leave this state.
                            // In the model clearing the related SEQIF flag and the SEQEND flag trigger the same TxWarmExit signal.
                            if (signal == RAC_RadioStateMachineSignal.TxWarmExit
                                && !RAC_seqStateTxWarmInterrupt.Value
                                && (!RAC_seqStateTxWarmEndEnable.Value || !RAC_seqStateTxWarmEnd.Value))
                            {
                                if (RAC_TxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.Tx);
                                }
                                else
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.TxWrapUp);
                                }
                            }
                            break;
                        }
                        case RAC_RadioState.Tx:
                        {
                            // In this state the data transmission is performed. Data is passed from the Buffer Controller through the Frame Controller (FRC) and into the 
                            // Modulator (MOD) which manipulates the Synthesizer to output the desired phase and frequency. The MOD controls the PA output power when using 
                            // amplitude modulation formats. FORCETX or a TXDIS command will abruptly terminate any ongoing transmission and generate state transition to TXWRAPUP. 
                            // When the FRC signals that the TX sequence completed without interruption, the RSM state will transition to the TXWRAPUP state. 
                            // RAC_SEQIF_STATETXWARM needs to be cleared before responding to those mentioned hardware signals. 
                            // RAC_SEQENDEN_STATETX is not expected to be set, as this state is "hardware-controlled".
                            // It also has to be mentioned that as soon as state TX is entered, TXEN is reset. In order that TX frames can be transmitted without returning 
                            // to OFF and TXWARM (e.g. without disabling end re-enabling the SYNTH and the PA), TXEN must be set back by the RAC Command when RSM is in state TX 
                            // and before going to TXWRAPUP.
                            if (!RAC_seqStateTxInterrupt.Value 
                                && (signal == RAC_RadioStateMachineSignal.TxDisable // TXDIS RAC command
                                    || signal == RAC_RadioStateMachineSignal.TxDone)) // FRC ends TX
                            {
                                RAC_ClearOngoingTx();
                                RAC_ChangeRadioState(RAC_RadioState.TxWrapUp);
                            }
                            break;
                        }
                        case RAC_RadioState.TxWrapUp:
                        {
                            // In the IRQ Handler of that state, the SW first checks the status of the frame transmitted by considering which of the FRC interruption is active: TXDONE, TXUF. 
                            // Considering this, it may have to perform some TX buffer cleanup in the BUFC. Then, depending on values of TXEN and RXEN at time of treating the IRQ Handler, 
                            // the TX may remain powered-up, or powered-down only, or powered-down followed by the RX powered-up to prepare transition to the next state: TX, RXSEARCH or OFF. 
                            // The SW will also set register RAC_TXWRAPUPNEXT as the next state expected (e.g TX, RXSEARCH, OFF). If at time of leaving the TXWRAPUP state, TXEN and RXEN 
                            // hardware values have become inconsistent with RAC_TXWRAPUPNEXT, then either state TXWRAPUP (when TX was expected) or RXWRAPUP (when RXSEARCH was expected) 
                            // will be forced by the HW as the next state. If there is no inconsistency, the RSM will go to the expected state RAC_TXWRAPUPNEXT.
                            // RAC_SEQIF_STATETXWRAPUP then RAC_SEQEND_STATETXWRAPUP needs to be cleared to leave this state. With current SW, it is mandatory to set RAC_SEQENDEN_STATETXWRAPUP, 
                            // to have rearm of the TXWRAPUP interruption correctly viewed and handled by the SEQR.
                            // If RAC_CTRL_FSMWRAPUPNEXTDIS is set, the RAC_TXWRAPUPNEXT is not considered, and RSM state will transition to TX if TXEN is set, RXSEARCH if RXEN is set, 
                            // else to OFF. This mode is not expected to be used in real application but possibly for test purpose.
                            // In the model clearing the related SEQIF flag and the SEQEND flag trigger the same TxWrapUpExit signal.
                            if (signal == RAC_RadioStateMachineSignal.TxWrapUpExit
                                && !RAC_seqStateRxWrapUpInterrupt.Value
                                && (!RAC_seqStateRxWrapUpEndEnable.Value || !RAC_seqStateRxWrapUpEnd.Value))
                                {
                                    if (RAC_txWrapUpNext.Value == RAC_RadioState.Tx
                                        && !RAC_TxEnable)
                                    {
                                        // Stay in TxWrapUp
                                    }
                                    else if (RAC_txWrapUpNext.Value == RAC_RadioState.RxSearch
                                             && !RAC_RxEnable)
                                    {
                                        RAC_ChangeRadioState(RAC_RadioState.RxWrapUp);
                                    }
                                    else if (RAC_txWrapUpNext.Value == RAC_RadioState.Tx
                                             && RAC_TxEnable)
                                    {
                                        RAC_ChangeRadioState(RAC_RadioState.Tx);
                                    }
                                    else if (RAC_txWrapUpNext.Value == RAC_RadioState.RxSearch
                                             && RAC_RxEnable)
                                    {
                                        RAC_ChangeRadioState(RAC_RadioState.RxSearch);
                                    }
                                    else 
                                    {
                                        RAC_ChangeRadioState(RAC_RadioState.Off);
                                    }
                                }
                            break;
                        }
                        case RAC_RadioState.RxWarm:
                        {
                            // This is a transitional state before reaching the RXSEARCH state. In this state the SYNTH and the RX chain are enabled and calibrated. 
                            // When the sequence completes, the RXSEARCH state is entered. If the RXCAL in RAC_CMD is set, this state is entered from RXSEARCH in order 
                            // to re-calibrate the SYNTH and RX chain. RAC_SEQIF_STATERXWARM and RAC_SEQEND_STATERXWARM (if RAC_SEQENDEN_STATERXWARM is set) needs to be 
                            // cleared to leave this state.
                            // In the model clearing the related SEQIF flag and the SEQEND flag trigger the same RxWarmExit signal.
                            if (signal == RAC_RadioStateMachineSignal.RxWarmExit
                                && !RAC_seqStateRxWarmInterrupt.Value
                                && (!RAC_seqStateRxWarmEndEnable.Value || !RAC_seqStateRxWarmEnd.Value))
                            {
                                RAC_ChangeRadioState(RAC_RadioState.RxSearch);
                            }
                            break;
                        }
                        case RAC_RadioState.RxSearch:
                        {
                            // In this state the demodulator (DEMOD) is continuously searching for a sync word. If detected, the RXFRAME state is entered. If a new calibration 
                            // of the RX chain is desired, the RXCAL command can be issued. This will make the RSM enter the RXWARM state which will re-calibrate the RX chain. 
                            // During this calibration the RX chain will not be operational, i.e. no frame reception is possible. 
                            // It is also possible to transition from RXSEARCH to RXWRAPUP, if no Frame is detected and RXEN is reset or TXEN is set. 
                            // RAC_SEQIF_STATERXSEARCH and RAC_SEQEND_STATERXSEARCH (if RAC_SEQENDEN_STATERXSEARCH is set) needs to be cleared before responding to mentioned 
                            // hardware signals and software commands.
                            if (!RAC_seqStateRxSearchInterrupt.Value
                                && (!RAC_seqStateRxSearchEndEnable.Value || !RAC_seqStateRxSearchEnd.Value))
                            {
                                if (signal == RAC_RadioStateMachineSignal.FrameDetected)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.RxFrame);
                                }
                                else if (signal == RAC_RadioStateMachineSignal.TxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.RxWrapUp);
                                }
                                else if (signal == RAC_RadioStateMachineSignal.RxCalibration)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.RxWarm);
                                }
                                else if (!RAC_RxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.RxWrapUp);
                                }
                            }
                            break;
                        }
                        case RAC_RadioState.RxFrame:
                        {
                            // In this state the frame is received. It is the responsibility of the FRC to determine when frame reception is complete.
                            // RAC_CTRL_FSMRXFRAMEEND selects which of the FRC termination hardware signal generates the transition to the RXWRAPUP state. 
                            // By default, it selects a signal occuring when the FRC state goes to IDLE, at the very end of FRC processing (including trailing bytes processing). 
                            // Else another signal can be selected, occuring sooner, after processing of the Payload and CRC bytes.
                            // RX frame HW abortion may be generated by the DEMOD to the FRC which will relay that signal to the RAC according to configuration of FRC_CTRL_RXABORTHWSEL. 
                            // When this event occurs and is applied to the RSM, RAC_CTRL_FSMRXABORTHW selects whether the RSM state will go back to RXSEARCH (default) or to RXWRAPUP.
                            // RX frame SW abortion can also be performed in the FRC through the RXABORT command. This will de-facto generate a FRC termination signal to the RSM which 
                            // will go to RXWRAPUP.
                            // If RAC_SEQENDEN_STATERXFRAME is set (although it is unlikely to be set), RAC_SEQEND_STATERXFRAME needs to be cleared before responding to those mentioned 
                            // hardware signals.                            
                            if (signal == RAC_RadioStateMachineSignal.RxAbort)
                            {
                                RAC_ClearOngoingRx();
                                RAC_ChangeRadioState(RAC_RadioState.RxSearch);
                            }
                            else if (signal == RAC_RadioStateMachineSignal.RxFrameExit
                                     || signal == RAC_RadioStateMachineSignal.RxDone)
                            {
                                // Here we should just move to the next state when the FRC fully received a frame.
                                // However, we have a race condition for which firing the RXFRAME_IrqHandler() with the radio state 
                                // already transitioned to the next state results in unexpected behavior from a software perspective 
                                // (which results in the  RX_Complete() firing from RXFRAME_IrqHandler()).
                                // To cope with this we progress to the next state only if both the RXFRAME IRQ flag has been cleared
                                // and the FRC has completed RXing a frame.
                                if (signal == RAC_RadioStateMachineSignal.RxFrameExit)
                                {
                                    FRC_rxFrameExitPending = true;
                                }
                                if (signal == RAC_RadioStateMachineSignal.RxDone)
                                {
                                    FRC_rxDonePending = true;
                                }
                                
                                if ((!RAC_seqStateRxFrameEndEnable.Value || !RAC_seqStateRxFrameEnd.Value)
                                    && FRC_rxFrameExitPending 
                                    && FRC_rxDonePending)

                                {
                                    RAC_ClearOngoingRx();
                                    RAC_ChangeRadioState(RAC_RadioState.RxWrapUp);
                                }
                            }                            
                            break;
                        }
                        case RAC_RadioState.RxWrapUp:
                        {
                            // In the IRQ Handler of that state, the SW first checks the status of the frame received by considering which of the FRC interruption is active: 
                            // RXDONE, FRAMEERROR, RXABORTED or RXOF. 
                            // Considering this, it may have to perform some RX buffer cleanup in the BUFC. Then, depending on values of TXEN and RXEN at time of treating the IRQ Handler, 
                            // the RX may remain powered-up, or powered-down only, or powered-down followed by the TX powered-up to prepare transition to the next state: TX, RXSEARCH or OFF. 
                            // The SW will also set register RAC_RXWRAPUPNEXT as the next state expected (e.g TX, RXSEARCH, OFF). If at time of leaving the RXWRAPUP state, 
                            // TXEN and RXEN hardware values have become inconsistent with RAC_RXWRAPUPNEXT, then either state TXWRAPUP (when TX was expected) or RXWRAPUP (when RXSEARCH 
                            // was expected) will be forced by the HW as the next state. If there is no inconsistency, the RSM will go to the expected state RAC_RXWRAPUPNEXT.
                            // RAC_SEQIF_STATERXWRAPUP then RAC_SEQEND_STATERXWRAPUP needs to be cleared to leave this state. With current SW, it is mandatory to set RAC_SEQENDEN_STATERXWRAPUP, 
                            // to have rearm of the RXWRAPUP interruption correctly viewed and handled by the SEQR.
                            if (signal == RAC_RadioStateMachineSignal.RxWrapUpExit
                                && !RAC_seqStateRxWrapUpInterrupt.Value
                                && (!RAC_seqStateRxWrapUpEndEnable.Value || !RAC_seqStateRxWrapUpEnd.Value))
                            {
                                if (RAC_rxWrapUpNext.Value == RAC_RadioState.RxSearch
                                    && !RAC_RxEnable)
                                {
                                    // Stay in RxWrapUp
                                }
                                else if (RAC_rxWrapUpNext.Value == RAC_RadioState.Tx
                                         && !RAC_TxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.TxWrapUp);
                                }
                                else if (RAC_rxWrapUpNext.Value == RAC_RadioState.RxSearch
                                         && RAC_RxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.RxSearch);
                                }
                                else if (RAC_rxWrapUpNext.Value == RAC_RadioState.Tx
                                         && RAC_TxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.Tx);
                                }
                                else
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.Off);
                                }
                            }
                            break;
                        }
                        default:
                        {
                            this.Log(LogLevel.Error, "RAC Radio State Machine Update, invalid state");
                            break;
                        }
                    }
                }

                if (previousState != RAC_currentRadioState)
                {
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "RSM Update at {0}, signal:{1} channel={2} ({3}), transition: {4}->{5} (TX={6} RX={7}) Lbt={8}",
                             GetTime(), signal, Channel, (MODEM_viterbiDemodulatorEnable.Value ? "BLE" : "802.15.4"), previousState, RAC_currentRadioState, RAC_internalTxState, RAC_internalRxState, PROTIMER_listenBeforeTalkState);
                    
                    uint currentStateBitmask = (1U << (int)RAC_currentRadioState);
                    
                    for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
                    {
                        if(PROTIMER_captureCompareChannel[i].enable.Value 
                           && PROTIMER_captureCompareChannel[i].mode.Value == PROTIMER_CaptureCompareMode.Capture
                           && ((PROTIMER_captureCompareChannel[i].captureInputSource.Value == PROTIMER_CaptureInputSource.RacState0
                                && ((uint)PROTIMER_racState0.Value & currentStateBitmask) > 0)
                               || (PROTIMER_captureCompareChannel[i].captureInputSource.Value == PROTIMER_CaptureInputSource.RacState1
                                   && ((uint)PROTIMER_racState1.Value & currentStateBitmask) > 0)))
                        {
                            this.Log(LogLevel.Noisy, "{0}: Saving state transition time for {1} on CC{2}", GetTime(), PROTIMER_captureCompareChannel[i].captureInputSource.Value, i);
                            PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                            PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                        }
                    }

                    switch(RAC_currentRadioState)
                    {
                        case RAC_RadioState.Off:
                            RAC_seqStateOffInterrupt.Value = true;
                            RAC_seqStateOffEnd.Value = true;
                            break;
                        case RAC_RadioState.RxWarm:
                            RAC_seqStateRxWarmInterrupt.Value = true;
                            RAC_seqStateRxWarmEnd.Value = true;
                            break;
                        case RAC_RadioState.RxSearch:
                            // Reset PKTBUFCOUNT when entering in RxSearch
                            FRC_packetBufferCount.Value = 0;
                            RAC_seqStateRxSearchInterrupt.Value = true;
                            RAC_seqStateRxSearchEnd.Value = true;
                            MODEM_demodulatorState.Value = MODEM_DemodulatorState.PreambleSearch;
                            FRC_UpdateRawMode();
                            break;
                        case RAC_RadioState.RxFrame:
                            RAC_seqStateRxFrameInterrupt.Value = true;
                            RAC_seqStateRxFrameEnd.Value = true;
                            MODEM_demodulatorState.Value = MODEM_DemodulatorState.RxFrame;
                            break;
                        case RAC_RadioState.RxWrapUp:
                            RAC_seqStateRxWrapUpInterrupt.Value = true;
                            RAC_seqStateRxWrapUpEnd.Value = true;
                            break;
                        case RAC_RadioState.TxWarm:
                            RAC_seqStateTxWarmInterrupt.Value = true;
                            RAC_seqStateTxWarmEnd.Value = true;
                            break;
                        case RAC_RadioState.Tx:                            
                            RAC_seqStateTxInterrupt.Value = true;
                            RAC_seqStateTxEnd.Value = true;
                            // We assemble and "transmit" the frame immediately so that receiver nodes 
                            // can transition from RX_SEARCH to RX_FRAME immediately. 
                            // We use timers to properly time the completion of the transmission process.
                            var frame = FRC_AssembleFrame();
                            TransmitFrame(frame);
                            break;
                        case RAC_RadioState.TxWrapUp:
                            RAC_seqStateTxWrapUpInterrupt.Value = true;
                            RAC_seqStateTxWrapUpEnd.Value = true;
                            break;
                        case RAC_RadioState.Shutdown:
                            RAC_seqStateShutDownInterrupt.Value = true;
                            RAC_seqStateShutdownEnd.Value = true;
                            break;
                        default:
                            this.Log(LogLevel.Error, "Invalid Radio State ({0}).", RAC_currentRadioState);
                            break;
                    }

                    if (RAC_currentRadioState != RAC_RadioState.RxSearch && RAC_currentRadioState != RAC_RadioState.RxFrame)
                    {
                        MODEM_demodulatorState.Value = MODEM_DemodulatorState.Off;
                    }
                    
                    AGC_UpdateRssiState();
                    
                    // If we just entered RxSearch state, we should restart RSSI sampling.
                    // However, for performance reasons, we rely on the InterferenceQueue notifications,
                    // so we simply update the RSSI here.
                    if (RAC_currentRadioState == RAC_RadioState.RxSearch)
                    {
                        AGC_UpdateRssi();
                    } 
                    // If entered a state other than RxSearch or RxFrame, we stop the Rssi sampling.
                    else if (RAC_currentRadioState != RAC_RadioState.RxSearch && RAC_currentRadioState != RAC_RadioState.RxFrame)
                    {
                        AGC_StopRssiTimer();
                    }
                }
                
                UpdateInterrupts();
            });
        }

        private void RAC_PaRampingTimerHandleLimitReached()
        {
            paRampingTimer.Enabled = false;
            this.Log(LogLevel.Noisy, "PA ramping up/down DONE at {0}", GetTime());
            // Done ramping the PA up or down, set TXRAMPDONE level interrupt back high.
            MODEM_TxRampingDoneInterrupt = true;
            RAC_paRampingDone.Value = RAC_paOutputLevelRamping;
            UpdateInterrupts();
        }

        private bool RAC_PaOutputLevelRamping
        {
            get
            {
                return RAC_paOutputLevelRamping;
            }

            set
            {
                // Ramping the PA up or down
                if (value != RAC_paOutputLevelRamping)
                {
                    RAC_paOutputLevelRamping = value;
                    
                    // TODO: if the MODEM PA ramping is disabled, we should ramp up using the MODEM->RAMPCTRL.RAMPVAL value.
                    if (!MODEM_rampDisable.Value)
                    {
                        // TXRAMPDONE is a level interrupt, it goes low during ramping, otherwise is always high.
                        MODEM_TxRampingDoneInterrupt = false;
                        UpdateInterrupts();

                        paRampingTimer.Enabled = false;
                        this.Log(LogLevel.Noisy, "Starting PA ramping up/down at {0}", GetTime());
                        // This is initially set 0 if ramping up, 1 if ramping down. Once PA ramping completed, it gets flipped.
                        RAC_paRampingDone.Value = !value;
                        paRampingTimer.Value = 0;
                        paRampingTimer.Limit = RAC_PowerAmplifierRampingTimeUs;
                        paRampingTimer.Enabled = true;
                    }
                }
            }
        }

        private bool RAC_PaOutputLevelRampingInProgress => paRampingTimer.Enabled;
#endregion

#region PROTIMER methods
        // TODOs: 
        // 1. The PROTIMER is started by either using the START command in the PROTIMER_CMD register or through 
        //    a PRS event on a selectable PRS channel. 
        // 2. PRECNTTOPADJ
        // 3. RTCC sync for EM2 support
        // 4. LBT/CSMA logic
        // 5. TX/RX requests
        // 6. Protocol timer Events
        private bool PROTIMER_Enabled
        {
            get
            {
                return proTimer.Enabled;
            }

            set
            {
                if (value)
                {
                    TrySyncTime();
                    bool isRunning = proTimer.Enabled;
                    ulong currentValue = proTimer.Value;

                    proTimer.Enabled = false;

                    if (!isRunning || PROTIMER_zeroStartEnable.Value)
                    {
                        // We assume that if the protimer restarts from zero, we need to reset BASECNT and WRAPCNT as well.
                        PROTIMER_BaseCounterValue = 0;
                        PROTIMER_WrapCounterValue = 0;
                    }
                    
                    // The proTimer timer is configured so that each tick corresponds to a PRECNT overflow.
                    proTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
                    proTimer.Limit = PROTIMER_ComputeTimerLimit();
                    proTimer.Enabled = true;
                    proTimer.Value = (!isRunning || PROTIMER_zeroStartEnable.Value) ? 0 : currentValue;
                }
                else
                {
                    proTimer.Enabled = false;
                }
            }
        }

        private uint PROTIMER_GetPreCntOverflowFrequency()
        {
            // The PRECNTTOP value is 1 less than the intended value.
            double frequency = (double)HfxoFrequency / (PROTIMER_preCounterTopInteger.Value + 1 + ((double)PROTIMER_preCounterTopFractional.Value / 65536));
            return Convert.ToUInt32(frequency);
        }

        private ulong PROTIMER_UsToPreCntOverflowTicks(double timeUs)
        {
            return Convert.ToUInt64((timeUs * (double)PROTIMER_GetPreCntOverflowFrequency()) / (double)MicrosecondFrequency);
        }

        public uint PROTIMER_BaseCounterValue
        {
            get
            {
                ulong ret = PROTIMER_baseCounterValue;
                if (PROTIMER_baseCounterSource.Value == PROTIMER_BaseCounterSource.PreCounterOverflow
                    && proTimer.Enabled)
                {
                    TrySyncTime();
                    ret += proTimer.Value;
                }
                return (uint)ret;
            }
            
            set
            {
                PROTIMER_baseCounterValue = value;
            }
        }

        public uint PROTIMER_WrapCounterValue
        {
            get
            {
                ulong ret = PROTIMER_wrapCounterValue;
                if (PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.PreCounterOverflow
                    && proTimer.Enabled)
                {
                    TrySyncTime();
                    ret += proTimer.Value;
                }
                return (uint)ret;
            }
            
            set
            {
                PROTIMER_wrapCounterValue = value;
            }
        }

        private bool PROTIMER_RxEnable => (PROTIMER_rxRequestState == PROTIMER_TxRxRequestState.Set
                                           || PROTIMER_rxRequestState == PROTIMER_TxRxRequestState.ClearEvent1);
        
        private bool PROTIMER_TxEnable
        {
            get => PROTIMER_txEnable;
            set
            {
                PROTIMER_txEnable = value;
                if (PROTIMER_txEnable)
                {
                    RAC_TxEnable = true;
                }
            }
        }

    public void PROTIMER_HandleChangedParams()
        {
            // Timer is not running, nothing to do
            if (!PROTIMER_Enabled)
            {
                return;
            }

            TrySyncTime();
            uint currentIncrement = (uint)proTimer.Value;
            proTimer.Enabled = false;
            proTimer.Value = 0;

            // First handle the current increment
            if (currentIncrement > 0)
            {
                PROTIMER_HandlePreCntOverflows(currentIncrement);
            }

            // Then restart the protimer
            proTimer.Limit = PROTIMER_ComputeTimerLimit();
            proTimer.Enabled = true;
        }

        private uint PROTIMER_ComputeTimerLimit()
        {            
            if (proTimer.Enabled)
            {
                throw new Exception("PROTIMER_ComputeTimerLimit invoked while the proTimer running");
            }

            uint limit = PROTIMER_DefaultTimerLimit;
            PROTIMER_preCounterSourcedBitmask = 0;

            if (PROTIMER_baseCounterSource.Value == PROTIMER_BaseCounterSource.PreCounterOverflow)
            {
                if (PROTIMER_baseCounterValue > PROTIMER_baseCounterTop.Value)
                {
                    this.Log(LogLevel.Error, "BASECNT > BASECNTTOP {0} {1}", PROTIMER_baseCounterValue, PROTIMER_baseCounterTop.Value);
                    throw new Exception("BASECNT > BASECNTTOP");
                }

                uint temp = (uint)PROTIMER_baseCounterTop.Value - PROTIMER_baseCounterValue;
                if (temp != 0 && temp < limit)
                {
                    limit = temp;
                }
                PROTIMER_preCounterSourcedBitmask |= (uint)PROTIMER_PreCountOverflowSourced.BaseCounter;
            }

            if (PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.PreCounterOverflow)
            {
                if (PROTIMER_wrapCounterValue > PROTIMER_wrapCounterTop.Value)
                {
                    this.Log(LogLevel.Error, "WRAPCNT > WRAPCNTTOP {0} {1}", PROTIMER_wrapCounterValue, PROTIMER_wrapCounterTop.Value);
                    throw new Exception("WRAPCNT > WRAPCNTTOP");
                }
                
                uint temp = (uint)PROTIMER_wrapCounterTop.Value - PROTIMER_wrapCounterValue;
                if (temp != 0 && temp < limit)
                {
                    limit = temp;
                }
                PROTIMER_preCounterSourcedBitmask |= (uint)PROTIMER_PreCountOverflowSourced.WrapCounter;
            }

            // RENODE-19: for now if a Timeout Timer is active and sourced by PRE overflow, 
            // we switch to a minimum ticks interval.
            // What we really want is to compute the number of PRE overflows to the timeout
            // or the match event.
            for(int i = 0; i < PROTIMER_NumberOfTimeoutCounters; i++)
            {
                if ((PROTIMER_timeoutCounter[i].synchronizing.Value
                     && PROTIMER_timeoutCounter[i].syncSource.Value == PROTIMER_TimeoutCounterSource.PreCounterOverflow)
                    || (PROTIMER_timeoutCounter[i].running.Value
                        && PROTIMER_timeoutCounter[i].source.Value == PROTIMER_TimeoutCounterSource.PreCounterOverflow))
                {
                    limit = PROTIMER_MinimumTimeoutCounterDelay;
                    PROTIMER_preCounterSourcedBitmask |= ((uint)PROTIMER_PreCountOverflowSourced.TimeoutCounter0 << i);
                }
            }

            // Check for Capture/Compare channels that are enabled and set in Compare mode
            for(int i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; i++)
            {
                if (PROTIMER_captureCompareChannel[i].enable.Value
                    && PROTIMER_captureCompareChannel[i].mode.Value == PROTIMER_CaptureCompareMode.Compare)
                {
                    // Base match enabled and base counter is sourced by pre counter overflows
                    if (PROTIMER_captureCompareChannel[i].baseMatchEnable.Value
                        && PROTIMER_baseCounterSource.Value == PROTIMER_BaseCounterSource.PreCounterOverflow
                        && PROTIMER_captureCompareChannel[i].baseValue.Value > PROTIMER_baseCounterValue)
                    {
                        uint temp = (uint)(PROTIMER_captureCompareChannel[i].baseValue.Value - PROTIMER_baseCounterValue);
                        if (temp < limit)
                        {
                            limit = temp;
                        }
                        PROTIMER_preCounterSourcedBitmask |= ((uint)PROTIMER_PreCountOverflowSourced.CaptureCompareChannel0 << i);
                    }

                    // Wrap match enabled and wrap counter is sourced by pre counter overflows
                    if (PROTIMER_captureCompareChannel[i].wrapMatchEnable.Value
                        && PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.PreCounterOverflow
                        && PROTIMER_captureCompareChannel[i].wrapValue.Value > PROTIMER_wrapCounterValue)
                    {
                        uint temp = (uint)(PROTIMER_captureCompareChannel[i].wrapValue.Value - PROTIMER_wrapCounterValue);
                        if (temp < limit)
                        {
                            limit = temp;
                        }
                        PROTIMER_preCounterSourcedBitmask |= ((uint)PROTIMER_PreCountOverflowSourced.CaptureCompareChannel0 << i);
                    }
                }
            }

            return limit;
        }

        private void PROTIMER_HandlePreCntOverflows(uint overflowCount)
        {
            if (proTimer.Enabled)
            {
                throw new Exception("PROTIMER_HandlePreCntOverflows invoked while the proTimer running");
            }

            // this.Log(LogLevel.Info, "PROTIMER_HandlePreCntOverflows cnt={0} mask=0x{1:X} base={2}", 
            //          overflowCount, PROTIMER_preCounterSourcedBitmask,
            //          (PROTIMER_preCounterSourcedBitmask & (uint)PROTIMER_PreCountOverflowSourced.BaseCounter));

            if((PROTIMER_preCounterSourcedBitmask & (uint)PROTIMER_PreCountOverflowSourced.BaseCounter) > 0)
            {
                PROTIMER_IncrementBaseCounter(overflowCount);
            }
            if((PROTIMER_preCounterSourcedBitmask & (uint)PROTIMER_PreCountOverflowSourced.WrapCounter) > 0)
            {
                PROTIMER_IncrementWrapCounter(overflowCount);
            }

            for(int i = 0; i < PROTIMER_NumberOfTimeoutCounters; i++)
            {
                if ((PROTIMER_preCounterSourcedBitmask & ((uint)PROTIMER_PreCountOverflowSourced.TimeoutCounter0 << i)) > 0)
                {
                    PROTIMER_timeoutCounter[i].Update(PROTIMER_TimeoutCounterSource.PreCounterOverflow, overflowCount);
                }
            }

            PreCountOverflowsEvent.Invoke(overflowCount);

            // TODO: for now we don't handle CaptureCompare channels being sourced by PreCount
        }
        
        private void PROTIMER_HandleTimerLimitReached()
        {
            proTimer.Enabled = false;

            // In lightweight mode the timer fires when N PRECNT overflows have occurred. 
            // The number N is set when we start/restart the proTimer
            
            //this.Log(LogLevel.Info, "proTimer overflow limit={0} baseTop={1} wrapTop={2}", proTimer.Limit, PROTIMER_baseCounterTop.Value, PROTIMER_wrapCounterTop.Value);

            proTimer.Value = 0;

            PROTIMER_HandlePreCntOverflows((uint)proTimer.Limit);

            proTimer.Limit = PROTIMER_ComputeTimerLimit();
            proTimer.Enabled = true;
        }

        private void PROTIMER_HandleBaseCounterOverflow()
        {
            // this.Log(LogLevel.Info, "PROTIMER_HandleBaseCounterOverflow baseValue={0} topValue={1} at {2}", 
            //          PROTIMER_BaseCounterValue, PROTIMER_baseCounterTop.Value, GetTime());
            
            PROTIMER_TriggerEvent(PROTIMER_Event.BaseCounterOverflow);
            
            if(PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.BaseCounterOverflow)
            {
                PROTIMER_IncrementWrapCounter();
            }

            Array.ForEach(PROTIMER_timeoutCounter, x => x.Update(PROTIMER_TimeoutCounterSource.BaseCounterOverflow));

            BaseCountOverflowsEvent.Invoke(1);
        }

        private void PROTIMER_HandleWrapCounterOverflow()
        {
            //this.Log(LogLevel.Info, "PROTIMER_HandleWrapCounterOverflow wrapValue={0} at {1}", PROTIMER_WrapCounterValue, GetTime());

            PROTIMER_TriggerEvent(PROTIMER_Event.WrapCounterOverflow);
            
            Array.ForEach(PROTIMER_timeoutCounter, x => x.Update(PROTIMER_TimeoutCounterSource.WrapCounterOverflow));
            
            WrapCountOverflowsEvent.Invoke(1);
        }

        private void PROTIMER_IncrementBaseCounter(uint increment = 1)
        {
            if (proTimer.Enabled)
            {
                throw new Exception("PROTIMER_IncrementBaseCounter invoked while the proTimer running");
            }

            PROTIMER_baseCounterValue += increment;

            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                var triggered = PROTIMER_captureCompareChannel[i].enable.Value
                    && PROTIMER_captureCompareChannel[i].baseMatchEnable.Value
                    && PROTIMER_captureCompareChannel[i].mode.Value == PROTIMER_CaptureCompareMode.Compare
                    && PROTIMER_baseCounterValue == PROTIMER_captureCompareChannel[i].baseValue.Value;
                
                if(triggered)
                {
                    PROTIMER_captureCompareChannel[i].interrupt.Value = true;
                    PROTIMER_captureCompareChannel[i].seqInterrupt.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                    CaptureCompareEvent.Invoke(i);
                }
            }

            if(PROTIMER_baseCounterValue >= PROTIMER_baseCounterTop.Value)
            {
                PROTIMER_HandleBaseCounterOverflow();
                PROTIMER_baseCounterValue = 0x0;
            }
        }

        private void PROTIMER_IncrementWrapCounter(uint increment = 1)
        {
            if (proTimer.Enabled)
            {
                throw new Exception("PROTIMER_IncrementWrapCounter invoked while the proTimer running");
            }

            PROTIMER_wrapCounterValue += increment;
            
            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                var triggered = PROTIMER_captureCompareChannel[i].enable.Value
                    && PROTIMER_captureCompareChannel[i].wrapMatchEnable.Value
                    && PROTIMER_captureCompareChannel[i].mode.Value == PROTIMER_CaptureCompareMode.Compare
                    && PROTIMER_wrapCounterValue == PROTIMER_captureCompareChannel[i].wrapValue.Value;
                
                if(triggered)
                {
                    PROTIMER_captureCompareChannel[i].interrupt.Value = true;
                    PROTIMER_captureCompareChannel[i].seqInterrupt.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                    CaptureCompareEvent.Invoke(i);
                }
            }

            if(PROTIMER_wrapCounterValue >= PROTIMER_wrapCounterTop.Value)
            {
                PROTIMER_HandleWrapCounterOverflow();
                PROTIMER_wrapCounterValue = 0x0;
            }
        }

        private void PROTIMER_UpdateCompareTimer(int index, bool syncTime = true)
        {   
            // We don't support preMatch in Compare Timers, instead we checks that preMatch is not enabled, 
            // and if base/wrap match are enabled, we recalculate the protimer limit.
            if (PROTIMER_captureCompareChannel[index].enable.Value
                && PROTIMER_captureCompareChannel[index].mode.Value == PROTIMER_CaptureCompareMode.Compare
                && PROTIMER_captureCompareChannel[index].preMatchEnable.Value)
            {
                this.Log(LogLevel.Error, "CC{0} PRE match enabled, NOT SUPPORTED!", index);
            }

            PROTIMER_HandleChangedParams();
        }

        private void PROTIMER_TimeoutCounter0HandleSynchronize()
        {
            if (PROTIMER_listenBeforeTalkSync.Value)
            {
                PROTIMER_listenBeforeTalkSync.Value = false;
                PROTIMER_listenBeforeTalkRunning.Value = true;
            }
        }

        private void PROTIMER_TimeoutCounter0HandleUnderflow()
        {   
            if (!PROTIMER_listenBeforeTalkRunning.Value)
            {
                return;
            }

            PROTIMER_timeoutCounter[0].Stop();
            
            switch(PROTIMER_listenBeforeTalkState)
            {
                case PROTIMER_ListenBeforeTalkState.Backoff:
                {
                    // CCACNT = 0
                    PROTIMER_ccaCounter.Value = 0;
                    
                    PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.CcaDelay;

                    // If the RSSI_START command failed to start (for example, radio is not in RX), we don't 
                    // start TOUT0 here, we expect to be back in backoff or to be done with retry attempts.
                    if (AGC_RssiStartCommand(true))
                    {
                        // Wait for CCDELAY+1 BASECNTOF events
                        PROTIMER_timeoutCounter[0].counterTop.Value = PROTIMER_ccaDelay.Value;
                        PROTIMER_timeoutCounter[0].Start();
                    }

                    break;
                }
                case PROTIMER_ListenBeforeTalkState.CcaDelay:
                {
                    // If we get here is because CCA was successful, otherwise we would have retried the backoff or failed LBT

                    // CCACNT == CCAREPEAT-1
                    if (PROTIMER_ccaCounter.Value == (PROTIMER_ccaRepeat.Value - 1))
                    {
                        PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Idle;
                        PROTIMER_listenBeforeTalkSync.Value = false;
                        PROTIMER_listenBeforeTalkRunning.Value = false;
                        PROTIMER_listenBeforeTalkSuccessInterrupt.Value = true;
                        PROTIMER_seqListenBeforeTalkSuccessInterrupt.Value = true;
                        UpdateInterrupts();
                        PROTIMER_TriggerEvent(PROTIMER_Event.ListenBeforeTalkSuccess);
                        // Trigger the CCA cmpleted event after the LBT success event so that the radio 
                        // does not leave RX and goes directly to RX2TX.
                        PROTIMER_TriggerEvent(PROTIMER_Event.ClearChannelAssessmentMeasurementCompleted);
                    }
                    else
                    {
                        // CCACNT++
                        PROTIMER_ccaCounter.Value += 1;

                        // If the RSSI_START command failed to start (for example, radio is not in RX), we don't 
                        // start TOUT0 here, we expect to be back in backoff or to be done with retry attempts.
                        if (AGC_RssiStartCommand(true))
                        {
                            // Wait for CCDELAY+1 BASECNTOF events
                            PROTIMER_timeoutCounter[0].counterTop.Value = PROTIMER_ccaDelay.Value;
                            PROTIMER_timeoutCounter[0].Start();
                        }
                    }

                    break;
                }
                default:
                {
                    throw new Exception("Unreachable. Invalid LBT state in PROTIMER_TimeoutCounter0HandleUnderflow");
                }
            }
        }

        private void PROTIMER_TimeoutCounter0HandleFinish()
        {
            if (PROTIMER_listenBeforeTalkPending)
            {
                PROTIMER_listenBeforeTalkPending = false;
                PROTIMER_ListenBeforeTalkStartCommand();
            }
        }

        private void PROTIMER_ListenBeforeTalkStartCommand()
        {
            if(PROTIMER_timeoutCounter[0].running.Value || PROTIMER_timeoutCounter[0].synchronizing.Value)
            {
                PROTIMER_listenBeforeTalkPending = true;
                return;
            }

            PROTIMER_listenBeforeTalkSync.Value = false;
            PROTIMER_listenBeforeTalkRunning.Value = false;
            PROTIMER_listenBeforeTalkPaused.Value = false;
            // EXP = STARTEXP 
            PROTIMER_listenBeforeTalkExponent.Value = PROTIMER_listenBeforeTalkStartExponent.Value;
            // RETRYCNT = 0
            PROTIMER_listenBeforeTalkRetryCounter.Value = 0;

            PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Backoff;

            if (PROTIMER_timeoutCounter[0].syncSource.Value == PROTIMER_TimeoutCounterSource.Disabled)
            {
                PROTIMER_listenBeforeTalkRunning.Value = true;
            }
            else 
            {
                PROTIMER_listenBeforeTalkSync.Value = true;
            }

            // TODO: implement FIXED BACKOFF here if needed:
            // "It is possible to have a fixed (non-random) backoff, by setting FIXEDBACKOFF in PROTIMER_LBTCTRL. 
            // This will prevent hardware from updating the EXP and RANDOM register fields when the LBT backoff 
            // value is calculated for each LBT attempt. Software can con- figure a fixed LBT backoff value by 
            // writing to the EXP (in PROTIMER_LBTSTATE) and RANDOM register fields.
            // Note: When using the FIXEDBACKOFF setting, the TOUT0CNT register is still decremented during the 
            // backoff period. The EXP and RANDOM register fields will remain constant for each retry."
            
            // BACKOFF = RANDOM & (2^EXP – 1)
            var rand = (uint)random.Next(); 
            var backoff = (rand & ((1u << (byte)PROTIMER_listenBeforeTalkExponent.Value) - 1));
            
            // Wait for BACKOFF+1 BASECNTOF events
            PROTIMER_timeoutCounter[0].counterTop.Value = backoff;
            PROTIMER_timeoutCounter[0].Start();
        }

        private void PROTIMER_ListenBeforeTalkPauseCommand()
        {
            throw new Exception("LBT Pausing not supported");
        }

        private void PROTIMER_ListenBeforeTalkStopCommand()
        {
            PROTIMER_timeoutCounter[0].Stop();
            PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Idle;
            PROTIMER_listenBeforeTalkSync.Value = false;
            PROTIMER_listenBeforeTalkRunning.Value = false;
        }

        private void PROTIMER_ListenBeforeTalkCcaCompleted(bool forceFailure = false)
        {
            if (PROTIMER_listenBeforeTalkState == PROTIMER_ListenBeforeTalkState.Idle)
            {
                throw new Exception("PROTIMER_ListenBeforeTalkCcaCompleted while LBT_STATE=idle");
            }

            if (forceFailure)
            {
                AGC_cca.Value = false;
            }
            else
            {
                AGC_cca.Value = (AGC_RssiIntegerPartAdjusted < (sbyte)AGC_ccaThreshold.Value);
            }

            // If the channel is clear, nothing to do here, we let CCADELAY complete.

            // Channel not clear    
            if (!AGC_cca.Value)
            {
                PROTIMER_timeoutCounter[0].Stop();
                PROTIMER_TriggerEvent(PROTIMER_Event.ClearChannelAssessmentMeasurementCompleted);

                // RETRYCNT == RETRYLIMIT?
                if (PROTIMER_listenBeforeTalkRetryCounter.Value == PROTIMER_retryLimit.Value)
                {
                    PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Idle;
                    PROTIMER_listenBeforeTalkSync.Value = false;
                    PROTIMER_listenBeforeTalkRunning.Value = false;
                    PROTIMER_listenBeforeTalkFailureInterrupt.Value = true;
                    PROTIMER_seqListenBeforeTalkFailureInterrupt.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_Event.ListenBeforeTalkFailure);
                }
                else
                {
                    PROTIMER_listenBeforeTalkRetryInterrupt.Value = true;
                    PROTIMER_seqListenBeforeTalkRetryInterrupt.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_Event.ListenBeforeTalkRetry);

                    PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Backoff;

                    // RETRYCNT++
                    PROTIMER_listenBeforeTalkRetryCounter.Value += 1;
                    
                    // EXP - min(EXP+1, MAXEXP)
                    if (PROTIMER_listenBeforeTalkExponent.Value + 1 <= PROTIMER_listenBeforeTalkMaxExponent.Value)
                    {
                        PROTIMER_listenBeforeTalkExponent.Value += 1;
                    }
                    else
                    {
                        PROTIMER_listenBeforeTalkExponent.Value = PROTIMER_listenBeforeTalkMaxExponent.Value;
                    }

                    // BACKOFF = RANDOM & (2^EXP – 1)
                    var rand = (uint)random.Next(); 
                    var backoff = (rand & ((1u << (byte)PROTIMER_listenBeforeTalkExponent.Value) - 1));

                    // Wait for BACKOFF+1 BASECNTOF events
                    PROTIMER_timeoutCounter[0].counterTop.Value = backoff;
                    PROTIMER_timeoutCounter[0].Start();                    
                }
            }
        }

        public void PROTIMER_TriggerEvent(PROTIMER_Event ev)
        {
            if(ev < PROTIMER_Event.PreCounterOverflow || ev > PROTIMER_Event.InternalTrigger)
            {
                throw new Exception("Unreachable. Invalid event value for PROTIMER_TriggerEvent.");
            }

            // if a Timeout Counter 0 match occurs during LBT, we change the event accordingly.
            if (ev == PROTIMER_Event.TimeoutCounter0Match && PROTIMER_listenBeforeTalkRunning.Value)
            {
                ev = PROTIMER_Event.TimeoutCounter0MatchListenBeforeTalk;
                PROTIMER_listenBeforeTalkTimeoutCounterMatchInterrupt.Value = true;
                PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterrupt.Value = true;
                UpdateInterrupts();
            }

            switch(PROTIMER_rxRequestState)
            {
                case PROTIMER_TxRxRequestState.Idle:
                {
                    if (PROTIMER_rxSetEvent1.Value == PROTIMER_Event.Always)
                    {
                        PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                        goto case PROTIMER_TxRxRequestState.SetEvent1;
                    }
                    if (PROTIMER_rxSetEvent1.Value == ev)
                    {
                        PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                    }
                    break;
                }
                case PROTIMER_TxRxRequestState.SetEvent1:
                {
                    if (PROTIMER_rxSetEvent2.Value == PROTIMER_Event.Always || PROTIMER_rxSetEvent2.Value == ev)
                    {
                        PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Set;
                        RAC_UpdateRadioStateMachine();
                    }
                    break;
                }
                case PROTIMER_TxRxRequestState.Set:
                {
                    if (PROTIMER_rxClearEvent1.Value == PROTIMER_Event.Always)
                    {
                        PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.ClearEvent1;
                        goto case PROTIMER_TxRxRequestState.ClearEvent1;
                    }
                    if (PROTIMER_rxClearEvent1.Value == ev)
                    {
                        PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.ClearEvent1;
                    }
                    break;
                }
                case PROTIMER_TxRxRequestState.ClearEvent1:
                {
                    if (PROTIMER_rxClearEvent2.Value == ev)
                    {
                        PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
                        RAC_UpdateRadioStateMachine();
                    }
                    break;
                }
                default:
                    throw new Exception("Unreachable. Invalid PROTIMER RX Request state.");
            }

            switch(PROTIMER_txRequestState)
            {
                case PROTIMER_TxRxRequestState.Idle:
                {
                    if (PROTIMER_txSetEvent1.Value == PROTIMER_Event.Always)
                    {
                        PROTIMER_txRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                        goto case PROTIMER_TxRxRequestState.SetEvent1;
                    }
                    if (PROTIMER_txSetEvent1.Value == ev)
                    {
                        PROTIMER_txRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                    }
                    break;
                }
                case PROTIMER_TxRxRequestState.SetEvent1:
                {
                    if (PROTIMER_txSetEvent2.Value == ev)
                    {
                        PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Set;
                        PROTIMER_TxEnable = true;
                        goto case PROTIMER_TxRxRequestState.Set;
                    }
                    break;
                }
                case PROTIMER_TxRxRequestState.Set:
                {
                    PROTIMER_TxEnable = false;
                    PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Idle;
                    break;
                }                
                default:
                    throw new Exception("Unreachable. Invalid PROTIMER TX Request state.");
            }
        }

        private void PROTIMER_UpdateRxRequestState()
        {
            if (PROTIMER_rxClearEvent1.Value == PROTIMER_Event.Always
                && PROTIMER_rxClearEvent2.Value == PROTIMER_Event.Always)
            {
                PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
            }
            else if (PROTIMER_rxSetEvent1.Value == PROTIMER_Event.Always
                     && PROTIMER_rxSetEvent2.Value == PROTIMER_Event.Always)
            {
                PROTIMER_TriggerEvent(PROTIMER_Event.InternalTrigger);
            }            
        }

        private void PROTIMER_UpdateTxRequestState()
        {
            if (PROTIMER_txSetEvent1.Value == PROTIMER_Event.Disabled
                && PROTIMER_txSetEvent2.Value == PROTIMER_Event.Disabled)
            {
                PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
            }
        }

        public PROTIMER_Event PROTIMER_GetCaptureCompareEventFromIndex(uint index)
        {
            if (index >= PROTIMER_NumberOfCaptureCompareChannels)
            {
                throw new Exception("PROTIMER_GetCaptureCompareEventFromIndex: invalid index");
            }

            if (index < 5)
            {
                return (PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel0Event + index);
            }
            else
            {
                return (PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel5Event + (index - 5));
            }
        }

        public PROTIMER_Event PROTIMER_GetTimeoutCounterEventFromIndex(uint index, PROTIMER_Event baseEvent)
        {
            if (baseEvent == PROTIMER_Event.TimeoutCounter0Match)
            {
                if (index < 2)
                {
                    return (PROTIMER_Event)((uint)PROTIMER_Event.TimeoutCounter0Match + index);
                }
                else if (index == 2)
                {
                    return PROTIMER_Event.TimeoutCounter2Match;
                }
            }
            else if (baseEvent == PROTIMER_Event.TimeoutCounter0Underflow)
            {
                if (index < 2)
                {
                    return (PROTIMER_Event)((uint)PROTIMER_Event.TimeoutCounter0Underflow + index);
                }
                else if (index == 2)
                {
                    return PROTIMER_Event.TimeoutCounter2Underflow;
                }
            }

            throw new Exception("PROTIMER_GetTimeoutCounterEventFromIndex: invalid param");
        }

        //-----------------------------------------------------------------------
        // SiLabs_IProtocolTimer interface methods
        public uint GetPreCountOverflowFrequency()
        {
            return PROTIMER_GetPreCntOverflowFrequency();
        }

        public void FlushCurrentPreCountOverflows()
        {
            PROTIMER_HandleChangedParams();
        }

        public uint GetCurrentPreCountOverflows()
        {
            TrySyncTime();
            return (uint)proTimer.Value;
        }

        public uint GetBaseCountValue()
        {
            return PROTIMER_BaseCounterValue;
        }
        
        public uint GetWrapCountValue()
        {
            return PROTIMER_WrapCounterValue;
        }
#endregion

#region MODEM methods
        private bool MODEM_TxRampingDoneInterrupt
        {
            set
            {
                MODEM_txRampingDoneInterrupt = value;
            }
            get
            {
                return MODEM_txRampingDoneInterrupt;
            }
        }

        private uint MODEM_GetPreambleLengthInBits()
        {
            uint preambleLength = (uint)((MODEM_baseBits.Value + 1)*MODEM_txBases.Value);
            return preambleLength;
        }

        private uint MODEM_GetSyncWordLengthInBits()
        {
            return MODEM_SyncWordLength;
        }

        private uint MODEM_GetDataRate()
        {
            double numerator = MODEM_txBaudrateNumerator.Value * 1.0;
            double ratio = numerator / Math.Pow(2 , 16);
            double interpFactor = (MODEM_baudRate2Mbps.Value) ? 2.0 : 4.0;
            double txBaudrate = (double)HfxoFrequency / interpFactor/ 8.0 * ratio;
            double chipsPerSymbol = MODEM_dsssLength.Value + 1; 
            double symbolsPerBit = 1.0; 
            uint dsssShiftedSymbols = 0;

            // Find shifted symbols
            if (MODEM_dsssShifts.Value == 1)
            {
                // Dsslen will be max 8 due to DSSS setup restrictions
                // A bitslice as is shown in dsssShiftedSymbols = dssslen[CORRINDEX_WIDTH-1:0]; where corrindex_width is 5
                dsssShiftedSymbols = (uint)MODEM_dsssLength.Value & 0x1F;
            }
            else if (MODEM_dsssShifts.Value > 1)
            {
                dsssShiftedSymbols = (uint)MODEM_dsssLength.Value >> (int)(MODEM_dsssShifts.Value - 1);
            }
            
            if ((MODEM_modulationFormat.Value == MODEM_ModulationFormat.FSK4))
            {
                symbolsPerBit = 2.0;
            }
            else if (MODEM_symbolCoding.Value == MODEM_SymbolCoding.Dsss)
            {
                // Convert to bits per symbol
                switch ((MODEM_DsssShiftedSymbols)dsssShiftedSymbols)
                {
                    case MODEM_DsssShiftedSymbols.ShiftedSymbol0:
                        {
                            symbolsPerBit = 1.0;
                            break;
                        }
                    case MODEM_DsssShiftedSymbols.ShiftedSymbol1:
                        {
                            if (MODEM_dsssDoublingMode.Value == MODEM_DsssDoublingMode.Disabled)
                            {
                                symbolsPerBit = 1.0;
                            }
                            else
                            {
                                symbolsPerBit = 2.0;
                            }
                            break;
                        }
                    case MODEM_DsssShiftedSymbols.ShiftedSymbol3:
                        {
                            symbolsPerBit = 2.0;
                            break;
                        }
                    case MODEM_DsssShiftedSymbols.ShiftedSymbol7:
                        {
                        symbolsPerBit = 4.0;
                        break;
                        }
                    default:
                        this.Log(LogLevel.Error, "MODEM_GetDataRate: default DSSS Shifted Symbol switch case");
                        symbolsPerBit = 1.0;
                        break;
                }
            }
            else
            {
                symbolsPerBit = 1.0;
            }

            double txBitrate = txBaudrate / (chipsPerSymbol / symbolsPerBit);
            
            this.Log(LogLevel.Noisy, "MODEM_GetDataRate: txBaudrate={0} chipsPerSymbol={1} symbolsPerBit={2} txBitrate={3}", 
                     txBaudrate, chipsPerSymbol, symbolsPerBit, txBitrate);
            
            return (uint)txBitrate;
        }

        private uint MODEM_GetRxChainDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_Ble1MbPhyRxChainDelayNanoS : MODEM_802154PhyRxChainDelayNanoS;
        }

        private double MODEM_GetRxChainDelayUs()
        {
            return ((double)MODEM_GetRxChainDelayNanoS()) / 1000;
        }

        private uint MODEM_GetRxDoneDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_Ble1MbPhyRxDoneDelayNanoS : MODEM_802154PhyRxDoneDelayNanoS;
        }

        private double MODEM_GetRxDoneDelayUs()
        {
            return ((double)MODEM_GetRxDoneDelayNanoS()) / 1000;
        }

        private uint MODEM_GetTxChainDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_Ble1MbPhyTxChainDelayNanoS : MODEM_802154PhyTxChainDelayNanoS;
        }

        private double MODEM_GetTxChainDelayUs()
        {
            return ((double)MODEM_GetTxChainDelayNanoS()) / 1000;
        }

        private uint MODEM_GetTxChainDoneDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_Ble1MbPhyTxDoneChainDelayNanoS : MODEM_802154PhyTxDoneChainDelayNanoS;
        }

        private double MODEM_GetTxChainDoneDelayUs()
        {
            return ((double)MODEM_GetTxChainDoneDelayNanoS()) / 1000;
        }

        private double MODEM_GetPreambleOverTheAirTimeUs()
        {
            return (double)MODEM_GetPreambleLengthInBits()*1000000/(double)MODEM_GetDataRate();
        }

        private double MODEM_GetSyncWordOverTheAirTimeUs()
        {
            return (double)MODEM_GetSyncWordLengthInBits()*1000000/(double)MODEM_GetDataRate();
        }

        // The passed frame is assumed to NOT include the preamble and to include the SYNC WORD.
        private double MODEM_GetFrameOverTheAirTimeUs(byte[] frame, bool includePreamble, bool includeSyncWord)
        {
            uint frameLengthInBits = (uint)frame.Length*8;

            if (includePreamble)
            {
                frameLengthInBits += MODEM_GetPreambleLengthInBits();
            }

            if (!includeSyncWord)
            {
                frameLengthInBits -= MODEM_GetSyncWordLengthInBits();
            }
            
            return ((double)frameLengthInBits)*1000000/(double)MODEM_GetDataRate();
        }

        // TODO: for now we are just able to distinguish between "BLE and non-BLE" by looking at the VTDEMODEN field.
        private RadioPhyId MODEM_GetCurrentPhy()
        {
            return (MODEM_viterbiDemodulatorEnable.Value ? RadioPhyId.Phy_BLE_2_4GHz_GFSK : RadioPhyId.Phy_802154_2_4GHz_OQPSK);
        }
#endregion

#region AGC methods
        private byte AGC_RssiFractionalPart
        {
            get
            {
                // TODO: for now AGC fractional part is always 0
                return 0;
            }
        }

        private sbyte AGC_RssiIntegerPart
        {
            get
            {
                return AGC_rssiIntegerPart;
            }
            set
            {
                AGC_rssiIntegerPart = value;
            }
        }
        
        private sbyte AGC_RssiIntegerPartAdjusted => (sbyte)(AGC_RssiIntegerPart + AGC_RssiWrapCompensationOffsetDbm);

        private byte AGC_FrameRssiFractionalPart
        {
            get
            {
                // TODO: for now Frame AGC fractional part is always 0
                return 0;
            }
        }

        private sbyte AGC_FrameRssiIntegerPart
        {
            set
            {
                AGC_frameRssiIntegerPart = value;

            }
            get
            {
                return AGC_frameRssiIntegerPart;
            }
        }

        private sbyte AGC_FrameRssiIntegerPartAdjusted => (sbyte)(AGC_FrameRssiIntegerPart + AGC_RssiWrapCompensationOffsetDbm);

        private uint AGC_RssiPeriodUs
        {
            get
            {
                // RSSI measure period is defined as 2^RSSIPERIOD sub-periods.
                // SUBPERIOD controls if the subperiod is one baud-period or separately specified. 
                // If SUBPERIOD is set, the sub-period is defined by SUBINT + SUBNUM/SUBDEM (TODO: for now we only consider SUBINT). 
                // Otherwise subperiod is equal to 1 baud.
                // If ENCCARSSIPERIOD is set, enable the use of a separate RSSI PERIOD (CCARSSIPERIOD) during CCA measurements.
                //var rssiPeriod = (AGC_ccaRssiPeriodEnable.Value && PROTIMER_listenBeforeTalkState == PROTIMER_ListenBeforeTalkState.CcaDelay) ? AGC_ccaRssiPeriod.Value : AGC_rssiMeasurePeriod.Value;
                //var subPeriod = AGC_subPeriod.Value ? AGC_subPeriodInteger.Value : 1;
                
                // TODO: we assume 250kbps OQPSK PHY baud period for now.
                //uint ret = (uint)(((1 << (int)rssiPeriod))*(double)subPeriod*AGC_OQPSK250KbpsPhyBaudPeriodUs);
                //return ret;

                // RENODE-371: Hard-coding RSSI measure period and CCA RSSI measure period until we get to the bottom of this.
                if ((AGC_ccaRssiPeriodEnable.Value && PROTIMER_listenBeforeTalkState == PROTIMER_ListenBeforeTalkState.CcaDelay))
                {
                    return AGC_OQPSK250KbpsPhyCcaRssiMeasurePeriodUs;
                }
                else if (AGC_rssiStartCommandOngoing)
                {
                    return AGC_OQPSK250KbpsPhyRssiMeasurePeriodUs;
                }
                else
                {
                    // For background RSSI sampling we use a longer period for performance reasons.
                    return AGC_OQPSK250KbpsPhyBackgroundRssiMeasurePeriodUs;
                }
            }
        }

        private void AGC_UpdateRssi()
        {
            AGC_RssiIntegerPart = (sbyte)InterferenceQueue.GetCurrentRssi(this, MODEM_GetCurrentPhy(), Channel);

            if (AGC_rssiFirstRead)
            {
                AGC_rssiFirstRead = false;
                AGC_rssiValidInterrupt.Value = true;
                AGC_seqRssiValidInterrupt.Value = true;
            }
            if (AGC_RssiIntegerPartAdjusted < (int)AGC_ccaThreshold.Value)
            {
                AGC_ccaInterrupt.Value = true;
                AGC_seqCcaInterrupt.Value = true;
            }
            else
            {
                AGC_ccaNotDetectedInterrupt.Value = true;
                AGC_seqCcaNotDetectedInterrupt.Value = true;
            }

            if (AGC_RssiIntegerPartAdjusted > (int)AGC_rssiHighThreshold.Value)
            {
                AGC_rssiHighInterrupt.Value = true;
                AGC_seqRssiHighInterrupt.Value = true;
            }
            else if (AGC_RssiIntegerPartAdjusted < (int)AGC_rssiLowThreshold.Value)
            {
                AGC_rssiLowInterrupt.Value = true;
                AGC_seqRssiLowInterrupt.Value = true;
            }

            UpdateInterrupts();
        }

        private bool AGC_RssiStartCommand(bool fromProtimer = false)
        {
            if (RAC_currentRadioState != RAC_RadioState.RxSearch && RAC_currentRadioState != RAC_RadioState.RxFrame)
            {
                AGC_RssiIntegerPart = AGC_RssiInvalid;
                if (fromProtimer && PROTIMER_listenBeforeTalkState != PROTIMER_ListenBeforeTalkState.Idle)
                {
                    // Radio is not in RX, fail CCA immediately.
                    PROTIMER_ListenBeforeTalkCcaCompleted(true);
                }
                return false;
            }
            else
            {
                AGC_rssiStartCommandOngoing = true;
                AGC_rssiStartCommandFromProtimer = fromProtimer;
                AGC_UpdateRssiState();
                AGC_RestartRssiTimer();
                return true;
            }
        }

        private void AGC_RestartRssiTimer()
        {
            rssiUpdateTimer.Enabled = false;
            rssiUpdateTimer.Value = 0;
            rssiUpdateTimer.Limit = AGC_RssiPeriodUs;
            rssiUpdateTimer.Enabled = true;
        }

        private void AGC_StopRssiTimer()
        {
            rssiUpdateTimer.Enabled = false;
            AGC_rssiStartCommandOngoing = false;
            AGC_UpdateRssiState();

            if (AGC_rssiStartCommandFromProtimer)
            {
                AGC_rssiStartCommandFromProtimer = false;
                if (PROTIMER_listenBeforeTalkState != PROTIMER_ListenBeforeTalkState.Idle)
                {
                    PROTIMER_ListenBeforeTalkCcaCompleted(true);
                }
            }
        }

        private void AGC_RssiUpdateTimerHandleLimitReached()
        {
            rssiUpdateTimer.Enabled = false;
            AGC_UpdateRssi();
            if (AGC_rssiStartCommandOngoing)
            {
                AGC_rssiDoneInterrupt.Value = true;
                AGC_seqRssiDoneInterrupt.Value = true;
                
                if (AGC_rssiStartCommandFromProtimer)
                {
                    if (PROTIMER_listenBeforeTalkState != PROTIMER_ListenBeforeTalkState.Idle)
                    {
                        PROTIMER_ListenBeforeTalkCcaCompleted();
                        AGC_RestartRssiTimer();
                    }
                    else
                    {
                        AGC_rssiStartCommandOngoing = false;
                        AGC_rssiStartCommandFromProtimer = false;
                    }
                }
                else
                {
                    AGC_rssiStartCommandOngoing = false;
                }

                AGC_UpdateRssiState();
            }
        }        

        private void AGC_UpdateRssiState()
        {
            if (AGC_rssiStartCommandOngoing)
            {
                AGC_rssiState.Value = AGC_RssiState.Command;
            }
            else if (RAC_currentRadioState == RAC_RadioState.RxSearch)
            {
                AGC_rssiState.Value = AGC_RssiState.Period;
            } 
            else if (RAC_currentRadioState == RAC_RadioState.RxFrame)
            {
                AGC_rssiState.Value = AGC_RssiState.FameDetection;
            } 
            else 
            {
                AGC_rssiState.Value = AGC_RssiState.Idle;
            }
        }
#endregion

#region SYNTH methods
        private void SYNTH_Start()
        {
            // For now we jump directly to the READY state
            if (SYNTH_state == SYNTH_State.CapacitorArrayCalibration)
            {
                SYNTH_state = SYNTH_State.Ready;
                SYNTH_readyInterrupt.Value = true;
                SYNTH_seqReadyInterrupt.Value = true;
                UpdateInterrupts();
            }
        }

        private void SYNTH_Stop()
        {
            SYNTH_state = SYNTH_State.Idle;
            synthTimer.Enabled = false;
        }

        private void SYNTH_EnableIf()
        {
            SYNTH_ifFrequencyEnabled.Value = true;
        }

        private void SYNTH_DisableIf()
        {
            SYNTH_ifFrequencyEnabled.Value = false;
        }

        private void SYNTH_CalibrationStart()
        {
            SYNTH_state = SYNTH_State.CapacitorArrayCalibration;

            synthTimer.Limit = SYNTH_CalibrationTimeUs;
            synthTimer.Enabled = true;
        }

        private void SYNTH_TimerLimitReached()
        {
            if (SYNTH_state == SYNTH_State.CapacitorArrayCalibration)
            {
                SYNTH_calibrationDoneInterrupt.Value = true;
                SYNTH_seqCalibrationDoneInterrupt.Value = true;
                UpdateInterrupts();
            }
        }
#endregion

#region CRC methods
        private byte[] CRC_CalculateCRC()
        {
            this.Log(LogLevel.Info, "CRC mocked with 0x0 bytes.");
            return Enumerable.Repeat<byte>(0x0, (int)CRC_CrcWidth).ToArray();
        }
#endregion

#region FRC fields
        private uint FRC_FrameLength => (uint)FRC_frameLength.Value + 1;
        private const uint FRC_NumberOfFrameDescriptors = 4;
        private const uint FRC_PacketBufferCaptureSize = 48;
        private byte[] FRC_packetBufferCapture;
        private FRC_FrameDescriptor[] FRC_frameDescriptor;
        private IFlagRegisterField FRC_activeTransmitFrameDescriptor;
        private IFlagRegisterField FRC_activeReceiveFrameDescriptor;
        private IFlagRegisterField FRC_rxRawBlocked;
        private IValueRegisterField FRC_fsmState;
        private IFlagRegisterField FRC_enableRawDataRandomNumberGenerator;
        private IEnumRegisterField<FRC_RxRawDataMode> FRC_rxRawDataSelect;
        private IEnumRegisterField<FRC_RxRawDataTriggerMode> FRC_rxRawDataTriggerSelect;
        private IEnumRegisterField<FRC_DynamicFrameLengthMode> FRC_dynamicFrameLengthMode;
        private IEnumRegisterField<FRC_DynamicFrameLengthBitOrder> FRC_dynamicFrameLengthBitOrder;
        private IValueRegisterField FRC_dynamicFrameLengthBitShift;
        private IValueRegisterField FRC_dynamicFrameLengthOffset;
        private IValueRegisterField FRC_dynamicFrameLengthBits;
        private IValueRegisterField FRC_minDecodedLength;
        private IFlagRegisterField FRC_dynamicFrameCrcIncluded;
        private IValueRegisterField FRC_maxDecodedLength;
        private IValueRegisterField FRC_initialDecodedFrameLength;
        private IValueRegisterField FRC_wordCounter;
        private IValueRegisterField FRC_frameLength;
        private IValueRegisterField FRC_lengthFieldLocation;
        private IValueRegisterField FRC_addressFieldLocation;
        private IEnumRegisterField<FRC_FrameDescriptorMode> FRC_txFrameDescriptorMode;
        private IEnumRegisterField<FRC_FrameDescriptorMode> FRC_rxFrameDescriptorMode;
        private IFlagRegisterField FRC_rxStoreCrc;
        private IFlagRegisterField FRC_rxAcceptCrcErrors;
        private IFlagRegisterField FRC_rxBufferClear;
        private IFlagRegisterField FRC_rxBufferRestoreOnFrameError;
        private IFlagRegisterField FRC_rxBufferRestoreOnRxAborted;
        private IFlagRegisterField FRC_rxAppendRssi;
        private IFlagRegisterField FRC_rxAppendStatus;
        private IFlagRegisterField FRC_rxAppendProtimerCc0BaseLow;
        private IFlagRegisterField FRC_rxAppendProtimerCc0BaseHigh;
        private IFlagRegisterField FRC_rxAppendProtimerCc0WrapLow;
        private IFlagRegisterField FRC_rxAppendProtimerCc0WrapHigh;
        private IValueRegisterField FRC_packetBufferStartAddress;
        private IValueRegisterField FRC_packetBufferThreshold;
        private IFlagRegisterField FRC_packetBufferThresholdEnable;
        private IFlagRegisterField FRC_packetBufferStop;
        private IValueRegisterField FRC_packetBufferCount;
        private IFlagRegisterField FRC_txDoneInterrupt;
        private IFlagRegisterField FRC_txAfterFrameDoneInterrupt;
        private IFlagRegisterField FRC_txUnderflowInterrupt;
        private IFlagRegisterField FRC_rxDoneInterrupt;
        private IFlagRegisterField FRC_rxAbortedInterrupt;
        private IFlagRegisterField FRC_frameErrorInterrupt;
        private IFlagRegisterField FRC_rxOverflowInterrupt;
        private IFlagRegisterField FRC_rxRawEventInterrupt;
        private IFlagRegisterField FRC_txRawEventInterrupt;
        private IFlagRegisterField FRC_packetBufferStartInterrupt;
        private IFlagRegisterField FRC_packetBufferThresholdInterrupt;
        private IFlagRegisterField FRC_txDoneInterruptEnable;
        private IFlagRegisterField FRC_txAfterFrameDoneInterruptEnable;
        private IFlagRegisterField FRC_txUnderflowInterruptEnable;
        private IFlagRegisterField FRC_rxDoneInterruptEnable;
        private IFlagRegisterField FRC_rxAbortedInterruptEnable;
        private IFlagRegisterField FRC_frameErrorInterruptEnable;
        private IFlagRegisterField FRC_rxOverflowInterruptEnable;
        private IFlagRegisterField FRC_rxRawEventInterruptEnable;
        private IFlagRegisterField FRC_txRawEventInterruptEnable;
        private IFlagRegisterField FRC_packetBufferStartInterruptEnable;
        private IFlagRegisterField FRC_packetBufferThresholdInterruptEnable;
        private IFlagRegisterField FRC_seqTxDoneInterrupt;
        private IFlagRegisterField FRC_seqTxAfterFrameDoneInterrupt;
        private IFlagRegisterField FRC_seqTxUnderflowInterrupt;
        private IFlagRegisterField FRC_seqRxDoneInterrupt;
        private IFlagRegisterField FRC_seqRxAbortedInterrupt;
        private IFlagRegisterField FRC_seqFrameErrorInterrupt;
        private IFlagRegisterField FRC_seqRxOverflowInterrupt;
        private IFlagRegisterField FRC_seqRxRawEventInterrupt;
        private IFlagRegisterField FRC_seqTxRawEventInterrupt;
        private IFlagRegisterField FRC_seqPacketBufferStartInterrupt;
        private IFlagRegisterField FRC_seqPacketBufferThresholdInterrupt;
        private IFlagRegisterField FRC_seqTxDoneInterruptEnable;
        private IFlagRegisterField FRC_seqTxAfterFrameDoneInterruptEnable;
        private IFlagRegisterField FRC_seqTxUnderflowInterruptEnable;
        private IFlagRegisterField FRC_seqRxDoneInterruptEnable;
        private IFlagRegisterField FRC_seqRxAbortedInterruptEnable;
        private IFlagRegisterField FRC_seqFrameErrorInterruptEnable;
        private IFlagRegisterField FRC_seqRxOverflowInterruptEnable;
        private IFlagRegisterField FRC_seqRxRawEventInterruptEnable;
        private IFlagRegisterField FRC_seqTxRawEventInterruptEnable;
        private IFlagRegisterField FRC_seqPacketBufferStartInterruptEnable;
        private IFlagRegisterField FRC_seqPacketBufferThresholdInterruptEnable;
        private bool FRC_rxFrameExitPending = false;
        private bool FRC_rxDonePending = false;

        // PTI Register fields
        private IFlagRegisterField FRC_ptiEmitRx;
        private IFlagRegisterField FRC_ptiEmitTx;
        private IFlagRegisterField FRC_ptiEmitRssi;
        private IFlagRegisterField FRC_ptiEmitState;
        private IFlagRegisterField FRC_ptiEmitAux;
        private IFlagRegisterField FRC_ptiEmitSyncWord;
#endregion

#region RAC fields
        private RAC_InternalTxState RAC_internalTxState = RAC_InternalTxState.Idle;
        private RAC_InternalRxState RAC_internalRxState = RAC_InternalRxState.Idle;
        private double RAC_rxTimeAlreadyPassedUs = 0;
        private bool RAC_ongoingRxCollided = false;
        private RAC_RadioState RAC_currentRadioState = RAC_RadioState.Off;
        private RAC_RadioState RAC_previous1RadioState = RAC_RadioState.Off;
        private RAC_RadioState RAC_previous2RadioState = RAC_RadioState.Off;
        private RAC_RadioState RAC_previous3RadioState = RAC_RadioState.Off;
        private IValueRegisterField RAC_softwareRxEnable;
        private IFlagRegisterField RAC_forceStateActive;
        private IFlagRegisterField RAC_sequencerInSleeping;
        private IFlagRegisterField RAC_sequencerInDeepSleep;
        private IFlagRegisterField RAC_sequencerActive;
        private IEnumRegisterField<RAC_RadioState> RAC_rxWrapUpNext;
        private IEnumRegisterField<RAC_RadioState> RAC_txWrapUpNext;
        private IEnumRegisterField<RAC_RadioState> RAC_forceStateTransition;
        private IFlagRegisterField RAC_forceDisable;
        private IFlagRegisterField RAC_exitShutdownDisable;
        private bool RAC_em1pAckPending;
        private bool RAC_dcCalDone = false;
        private IFlagRegisterField RAC_paRampingDone;
        // RENODE-53
        // TODO: calculate the ramping time from registers
        private const uint RAC_PowerAmplifierRampingTimeUs = 5;
        private bool RAC_paOutputLevelRamping = false;
        private IValueRegisterField RAC_mainCoreSeqInterrupts;
        private IFlagRegisterField RAC_radioStateChangeInterrupt;
        private IFlagRegisterField RAC_stimerCompareEventInterrupt;
        private IValueRegisterField RAC_mainCoreSeqInterruptsEnable;
        private IFlagRegisterField RAC_radioStateChangeInterruptEnable;
        private IFlagRegisterField RAC_stimerCompareEventInterruptEnable;
        // Sequencer Interrupt Flag
        private IFlagRegisterField RAC_seqRadioStateChangeInterrupt;
        private IFlagRegisterField RAC_seqStimerCompareEventInterrupt;
        private IFlagRegisterField RAC_seqDemodRxRequestClearInterrupt;
        private IFlagRegisterField RAC_seqPrsEventInterrupt;
        private IFlagRegisterField RAC_seqStateOffInterrupt;
        private IFlagRegisterField RAC_seqStateRxWarmInterrupt;
        private IFlagRegisterField RAC_seqStateRxSearchInterrupt;
        private IFlagRegisterField RAC_seqStateRxFrameInterrupt;
        private IFlagRegisterField RAC_seqStateRxWrapUpInterrupt;
        private IFlagRegisterField RAC_seqStateTxWarmInterrupt;
        private IFlagRegisterField RAC_seqStateTxInterrupt;
        private IFlagRegisterField RAC_seqStateTxWrapUpInterrupt;
        private IFlagRegisterField RAC_seqStateShutDownInterrupt;
        // Sequencer Interrupt Enable
        private IFlagRegisterField RAC_seqRadioStateChangeInterruptEnable;
        private IFlagRegisterField RAC_seqStimerCompareEventInterruptEnable;
        private IFlagRegisterField RAC_seqDemodRxRequestClearInterruptEnable;
        private IFlagRegisterField RAC_seqPrsEventInterruptEnable;
        private IFlagRegisterField RAC_seqStateOffInterruptEnable;
        private IFlagRegisterField RAC_seqStateRxWarmInterruptEnable;
        private IFlagRegisterField RAC_seqStateRxSearchInterruptEnable;
        private IFlagRegisterField RAC_seqStateRxFrameInterruptEnable;
        private IFlagRegisterField RAC_seqStateRxWrapUpInterruptEnable;
        private IFlagRegisterField RAC_seqStateTxWarmInterruptEnable;
        private IFlagRegisterField RAC_seqStateTxInterruptEnable;
        private IFlagRegisterField RAC_seqStateTxWrapUpInterruptEnable;
        private IFlagRegisterField RAC_seqStateShutDownInterruptEnable;
        // Sequencer "end" flags
        private IFlagRegisterField RAC_seqStateOffEnd;
        private IFlagRegisterField RAC_seqStateRxWarmEnd;
        private IFlagRegisterField RAC_seqStateRxSearchEnd;
        private IFlagRegisterField RAC_seqStateRxFrameEnd;
        private IFlagRegisterField RAC_seqStateRxWrapUpEnd;
        private IFlagRegisterField RAC_seqStateTxWarmEnd;
        private IFlagRegisterField RAC_seqStateTxEnd;
        private IFlagRegisterField RAC_seqStateTxWrapUpEnd;
        private IFlagRegisterField RAC_seqStateShutdownEnd;
        // Sequencer "end enable" flags
        private IFlagRegisterField RAC_seqStateOffEndEnable;
        private IFlagRegisterField RAC_seqStateRxWarmEndEnable;
        private IFlagRegisterField RAC_seqStateRxSearchEndEnable;
        private IFlagRegisterField RAC_seqStateRxFrameEndEnable;
        private IFlagRegisterField RAC_seqStateRxWrapUpEndEnable;
        private IFlagRegisterField RAC_seqStateTxWarmEndEnable;
        private IFlagRegisterField RAC_seqStateTxEndEnable;
        private IFlagRegisterField RAC_seqStateTxWrapUpEndEnable;
        private IFlagRegisterField RAC_seqStateShutdownEndEnable;
        private const uint RAC_NumberOfSequencerStorageRegisters = 4;
        private const uint RAC_NumberOfScratchRegisters = 8;
        private IValueRegisterField[] RAC_seqStorage = new IValueRegisterField[RAC_NumberOfSequencerStorageRegisters];
        private IValueRegisterField[] RAC_scratch = new IValueRegisterField[RAC_NumberOfScratchRegisters];
        private IFlagRegisterField RAC_unlocked;
        private bool RAC_txEnable;
        private bool RAC_TxEnable
        {
            get => RAC_txEnable;
            set
            {
                /*
                value |= ProtimerTxEnable;
                value &= !radioStateMachineForceDisable.Value;
                */
                var risingEdge = value && !RAC_txEnable;
                RAC_txEnable = value;

                if (risingEdge)
                {
                    RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxEnable);
                }
            }
        }
        
        // Bit 0: ptimer_txreq_suppress
        // Bit 1: ptimer_txreq
        // Bit 2: spi_txen
        // Bit 3: forcetx
        // Bit 4: cmd_txoncca
        // Bit 5: prstxen
        // Bit 6: cmd_txen
        private uint RAC_TxEnableMask
        {
            get => 0;
            set
            {
                // TODO
            }
        }

        // 0:13 correspond to RAC_RXENSRCEN[13:0]
        // Bit 14 indicates that the PROTIMER is requesting RXEN
        // Bit 15 indicates that SPI is requesting RXEN
        private uint RAC_RxEnableMask => ((uint)RAC_softwareRxEnable.Value
                                          /*
                                          | (RAC_channelBusyRxEnable.Value << 8)
                                          | (RAC_timingDetectedRxEnable.Value << 9)
                                          | (RAC_preambleDetectedRxEnable.Value << 10)
                                          | (RAC_frameDetectedRxEnable.Value << 11)
                                          | (RAC_demodRxRequestRxEnable.Value << 12)
                                          | (RAC_prsRxEnable.Value << 13)
                                          */
                                          | ((PROTIMER_RxEnable ? 1U : 0U) << 14)
                                          /*
                                          | (SPI_RxEnable << 15)
                                          */
                                        );
        private bool RAC_RxEnable => RAC_RxEnableMask != 0;        
#endregion

#region BUFC fields
        private const uint BUFC_NumberOfBuffers = 4;
        private BUFC_Buffer[] BUFC_buffer;
#endregion

#region PROTIMER fields
        private uint PROTIMER_preCounterSourcedBitmask = 0;
        private const uint PROTIMER_DefaultTimerLimit = 0xFFFFFFFF;
        private const uint PROTIMER_MinimumTimeoutCounterDelay = 8;
        private const uint PROTIMER_NumberOfTimeoutCounters = 3;
        private PROTIMER_TimeoutCounter[] PROTIMER_timeoutCounter;
        private const uint PROTIMER_NumberOfCaptureCompareChannels = 12;
        private PROTIMER_CaptureCompareChannel[] PROTIMER_captureCompareChannel;
        private IEnumRegisterField<PROTIMER_PreCounterSource> PROTIMER_preCounterSource;
        private IEnumRegisterField<PROTIMER_BaseCounterSource> PROTIMER_baseCounterSource;
        private IEnumRegisterField<PROTIMER_WrapCounterSource> PROTIMER_wrapCounterSource;
        private uint PROTIMER_baseCounterValue = 0;
        private uint PROTIMER_wrapCounterValue = 0;
        private uint PROTIMER_latchedBaseCounterValue = 0;
        private uint PROTIMER_latchedWrapCounterValue = 0;
        private uint PROTIMER_seqLatchedBaseCounterValue = 0;
        private uint PROTIMER_seqLatchedWrapCounterValue = 0;
        private IValueRegisterField PROTIMER_preCounterTopInteger;
        private IValueRegisterField PROTIMER_preCounterTopFractional;
        private IValueRegisterField PROTIMER_baseCounterTop;
        private IValueRegisterField PROTIMER_wrapCounterTop;
        private IFlagRegisterField PROTIMER_zeroStartEnable;
        private bool PROTIMER_txEnable = false;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxSetEvent1;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxSetEvent2;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxClearEvent1;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxClearEvent2;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_txSetEvent1;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_txSetEvent2;
        private PROTIMER_TxRxRequestState PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Idle;
        private PROTIMER_TxRxRequestState PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
        private PROTIMER_ListenBeforeTalkState PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Idle;
        private bool PROTIMER_listenBeforeTalkPending = false;
        private IFlagRegisterField PROTIMER_listenBeforeTalkSync;
        private IFlagRegisterField PROTIMER_listenBeforeTalkRunning;
        private IFlagRegisterField PROTIMER_listenBeforeTalkPaused;        
        private IValueRegisterField PROTIMER_listenBeforeTalkStartExponent;
        private IValueRegisterField PROTIMER_listenBeforeTalkMaxExponent;
        private IValueRegisterField PROTIMER_listenBeforeTalkExponent;
        private IValueRegisterField PROTIMER_listenBeforeTalkRetryCounter;
        private IValueRegisterField PROTIMER_ccaDelay;
        private IValueRegisterField PROTIMER_ccaRepeat;
        private IFlagRegisterField PROTIMER_fixedBackoff;
        private IValueRegisterField PROTIMER_retryLimit;
        private IValueRegisterField PROTIMER_ccaCounter;
        private const uint PROTIMER_NumberOfListenBeforeTalkRandomBackoffValues = 8;
        private IValueRegisterField[] PROTIMER_ListenBeforeTalkRandomBackoffValue;
        private IEnumRegisterField<RAC_RadioState> PROTIMER_racState0;
        private IEnumRegisterField<RAC_RadioState> PROTIMER_racState1;
        // Interrupt fields
        private IFlagRegisterField PROTIMER_preCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_baseCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_wrapCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkSuccessInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkFailureInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkRetryInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkTimeoutCounterMatchInterruptEnable;
        private IFlagRegisterField PROTIMER_preCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_baseCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_wrapCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkSuccessInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkFailureInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkRetryInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkTimeoutCounterMatchInterrupt;
        private IFlagRegisterField PROTIMER_seqPreCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_seqBaseCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_seqWrapCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkSuccessInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkFailureInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkRetryInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterruptEnable;
        private IFlagRegisterField PROTIMER_seqPreCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_seqBaseCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_seqWrapCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkSuccessInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkFailureInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkRetryInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterrupt;
#endregion

#region MODEM fields
        private const uint MODEM_Ble1MbPhyRxChainDelayNanoS = 50000;
        private const uint MODEM_Ble1MbPhyRxDoneDelayNanoS = 11000;
        private const uint MODEM_Ble1MbPhyTxChainDelayNanoS = 3500;
        private const uint MODEM_Ble1MbPhyTxDoneChainDelayNanoS = 750;
        private const uint MODEM_802154PhyRxChainDelayNanoS = 6625;
        private const uint MODEM_802154PhyRxDoneDelayNanoS = 6625;
        private const uint MODEM_802154PhyTxChainDelayNanoS = 500;
        private const uint MODEM_802154PhyTxDoneChainDelayNanoS = 0;
        private uint MODEM_SyncWordLength => (uint)MODEM_syncBits.Value + 1;
        private uint MODEM_SyncWordBytes => ((uint)MODEM_syncBits.Value >> 3) + 1;
        private uint MODEM_TxSyncWord => (MODEM_dualSync.Value && MODEM_txSync.Value) ? (uint)MODEM_syncWord1.Value : (uint)MODEM_syncWord0.Value;
        private uint MODEM_SyncWord0 => (uint)MODEM_syncWord0.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);
        private uint MODEM_SyncWord1 => (uint)MODEM_syncWord1.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);
        private uint MODEM_SyncWord2 => (uint)MODEM_syncWord2.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);
        private uint MODEM_SyncWord3 => (uint)MODEM_syncWord3.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);
        private bool MODEM_txRampingDoneInterrupt = true;
        private IFlagRegisterField MODEM_txFrameSentInterrupt;
        private IFlagRegisterField MODEM_txSyncSentInterrupt;
        private IFlagRegisterField MODEM_txPreambleSentInterrupt;
        private IFlagRegisterField MODEM_rxPreambleDetectedInterrupt;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord0DetectedInterrupt;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord1DetectedInterrupt;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord2DetectedInterrupt;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord3DetectedInterrupt;
        private IFlagRegisterField MODEM_rxPreambleLostInterrupt;
        private IFlagRegisterField MODEM_txFrameSentInterruptEnable;
        private IFlagRegisterField MODEM_txSyncSentInterruptEnable;
        private IFlagRegisterField MODEM_txPreambleSentInterruptEnable;
        private IFlagRegisterField MODEM_txRampingDoneInterruptEnable;
        private IFlagRegisterField MODEM_rxPreambleDetectedInterruptEnable;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord0DetectedInterruptEnable;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord1DetectedInterruptEnable;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord2DetectedInterruptEnable;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord3DetectedInterruptEnable;
        private IFlagRegisterField MODEM_rxPreambleLostInterruptEnable;
        private IFlagRegisterField MODEM_seqTxFrameSentInterrupt;
        private IFlagRegisterField MODEM_seqTxSyncSentInterrupt;
        private IFlagRegisterField MODEM_seqTxPreambleSentInterrupt;
        private IFlagRegisterField MODEM_seqRxPreambleDetectedInterrupt;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord0DetectedInterrupt;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord1DetectedInterrupt;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord2DetectedInterrupt;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord3DetectedInterrupt;
        private IFlagRegisterField MODEM_seqRxPreambleLostInterrupt;
        private IFlagRegisterField MODEM_seqTxFrameSentInterruptEnable;
        private IFlagRegisterField MODEM_seqTxSyncSentInterruptEnable;
        private IFlagRegisterField MODEM_seqTxPreambleSentInterruptEnable;
        private IFlagRegisterField MODEM_seqTxRampingDoneInterruptEnable;
        private IFlagRegisterField MODEM_seqRxPreambleDetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord0DetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord1DetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord2DetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord3DetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqRxPreambleLostInterruptEnable;
        private IValueRegisterField MODEM_baseBits;
        private IValueRegisterField MODEM_txBases;
        private IValueRegisterField MODEM_syncBits;
        private IValueRegisterField MODEM_txBaudrateNumerator;
        private IValueRegisterField MODEM_dsssLength;
        private IValueRegisterField MODEM_dsssShifts;
        private IEnumRegisterField<MODEM_ModulationFormat> MODEM_modulationFormat;
        private IEnumRegisterField<MODEM_SymbolCoding> MODEM_symbolCoding;
        private IEnumRegisterField<MODEM_DsssDoublingMode> MODEM_dsssDoublingMode;
        private IFlagRegisterField MODEM_txModulatorMode;
        private IFlagRegisterField MODEM_baudRate2Mbps;
        // There are 2 sync-word detection blocks. If this bit is set, dual-sync-word detection in the first sync-word detection block will
        // be enabled. Demodulator searches for SYNC0 and SYNC1 in parallel.
        private IFlagRegisterField MODEM_dualSync;
        // There are 2 sync-word detection blocks. If this bit is set, dual-sync-word detection in the second sync-word detection block will 
        // be enabled. Demodulator searches for SYNC2 and SYNC3 in parallel.
        private IFlagRegisterField MODEM_dualSync2Th;
        // There are 2 sync-word detection blocks. If this bit is set, the second sync-word detection block will be enabled. 
        // The first sync-word detection block is always enabled.
        private IFlagRegisterField MODEM_syncDetect2Th;
        private IFlagRegisterField MODEM_txSync;
        private IFlagRegisterField MODEM_syncData;
        private IValueRegisterField MODEM_syncWord0;
        private IValueRegisterField MODEM_syncWord1;
        private IValueRegisterField MODEM_syncWord2;
        private IValueRegisterField MODEM_syncWord3;
        private IValueRegisterField MODEM_frameDetectedId;
        private IValueRegisterField MODEM_rampRate0;
        private IValueRegisterField MODEM_rampRate1;
        private IValueRegisterField MODEM_rampRate2;
        private IEnumRegisterField<MODEM_RampMode> MODEM_rampMode;
        private IFlagRegisterField MODEM_rampDisable;
        private IValueRegisterField MODEM_rampValue;
        private IValueRegisterField MODEM_rampLevel0;
        private IValueRegisterField MODEM_rampLevel1;
        private IValueRegisterField MODEM_rampLevel2;
        private IValueRegisterField MODEM_rampLevelOffset;
        private IFlagRegisterField MODEM_viterbiDemodulatorEnable;
        private IEnumRegisterField<MODEM_DemodulatorState> MODEM_demodulatorState;
#endregion

#region AGC fields
        // The PHYs are configured in a way that the produced RSSI values are shifted by this value.
        // This is to avoid some wrap issue with very small values.
        // RAIL then subtracts this offset to all RSSI values coming from the hardware.
        // TODO: this value should be able to be inferred from the overall PHY Configurator registers setup,
        // for now we just hard-code the offset.
        private const uint AGC_RssiWrapCompensationOffsetDbm = 50;
        // RENODE-371: these values are a temporary work-around until the ticket is addressed.
        private const uint AGC_OQPSK250KbpsPhyCcaRssiMeasurePeriodUs = 128;
        private const uint AGC_OQPSK250KbpsPhyRssiMeasurePeriodUs = 8;
        private const uint AGC_OQPSK250KbpsPhyBackgroundRssiMeasurePeriodUs = 50;
        private const int AGC_RssiInvalid = -128;
        private bool AGC_rssiFirstRead = true;
        private bool AGC_rssiStartCommandOngoing = false;
        private bool AGC_rssiStartCommandFromProtimer = false;
        private sbyte AGC_rssiIntegerPart;
        private sbyte AGC_frameRssiIntegerPart;
        private IFlagRegisterField AGC_cca;
        private IValueRegisterField AGC_ccaThreshold;
        private IValueRegisterField AGC_rssiMeasurePeriod;
        private IValueRegisterField AGC_powerMeasurePeriod;
        private IEnumRegisterField<AGC_CcaMode> AGC_ccaMode;
        private IEnumRegisterField<AGC_CcaMode3Logic> AGC_ccaMode3Logic;
        private IEnumRegisterField<AGC_RssiState> AGC_rssiState;
        private IFlagRegisterField AGC_ccaSoftwareControl;
        private IValueRegisterField AGC_ccaRssiPeriod;
        private IFlagRegisterField AGC_ccaRssiPeriodEnable;
        private IValueRegisterField AGC_rssiHighThreshold;
        private IValueRegisterField AGC_rssiLowThreshold;
        private IValueRegisterField AGC_rssiShift;
        private IFlagRegisterField AGC_subPeriod;
        private IValueRegisterField AGC_subPeriodInteger;
        // Interrupt fields
        private IFlagRegisterField AGC_rssiValidInterrupt;
        private IFlagRegisterField AGC_ccaInterrupt;
        private IFlagRegisterField AGC_rssiDoneInterrupt;
        private IFlagRegisterField AGC_rssiHighInterrupt;
        private IFlagRegisterField AGC_rssiLowInterrupt;
        private IFlagRegisterField AGC_ccaNotDetectedInterrupt;
        private IFlagRegisterField AGC_rssiValidInterruptEnable;
        private IFlagRegisterField AGC_ccaInterruptEnable;
        private IFlagRegisterField AGC_rssiDoneInterruptEnable;
        private IFlagRegisterField AGC_rssiHighInterruptEnable;
        private IFlagRegisterField AGC_rssiLowInterruptEnable;
        private IFlagRegisterField AGC_ccaNotDetectedInterruptEnable;
        private IFlagRegisterField AGC_seqRssiValidInterrupt;
        private IFlagRegisterField AGC_seqCcaInterrupt;
        private IFlagRegisterField AGC_seqRssiDoneInterrupt;
        private IFlagRegisterField AGC_seqRssiHighInterrupt;
        private IFlagRegisterField AGC_seqRssiLowInterrupt;
        private IFlagRegisterField AGC_seqCcaNotDetectedInterrupt;
        private IFlagRegisterField AGC_seqRssiValidInterruptEnable;
        private IFlagRegisterField AGC_seqCcaInterruptEnable;
        private IFlagRegisterField AGC_seqRssiDoneInterruptEnable;
        private IFlagRegisterField AGC_seqRssiHighInterruptEnable;
        private IFlagRegisterField AGC_seqRssiLowInterruptEnable;
        private IFlagRegisterField AGC_seqCcaNotDetectedInterruptEnable;
#endregion

#region SYNTH fields
        private const uint SYNTH_CalibrationTimeUs = 5U;
        SYNTH_State SYNTH_state = SYNTH_State.Idle;
        private IFlagRegisterField SYNTH_ifFrequencyEnabled;
        // Interrupt fields
        private IFlagRegisterField SYNTH_readyInterrupt;
        private IFlagRegisterField SYNTH_calibrationDoneInterrupt;
        private IFlagRegisterField SYNTH_readyInterruptEnable;
        private IFlagRegisterField SYNTH_calibrationDoneInterruptEnable;
        private IFlagRegisterField SYNTH_seqReadyInterrupt;
        private IFlagRegisterField SYNTH_seqCalibrationDoneInterrupt;
        private IFlagRegisterField SYNTH_seqReadyInterruptEnable;
        private IFlagRegisterField SYNTH_seqCalibrationDoneInterruptEnable;
#endregion

#region CRC Fields
        private uint CRC_CrcWidth => (uint)CRC_crcWidthMode.Value + 1;        
        private IEnumRegisterField<CRC_CrcWidthMode> CRC_crcWidthMode;
        private IFlagRegisterField CRC_reverseCrcByteOrdering;
        private IValueRegisterField CRC_crcBitsPerWord;
#endregion

#region HOST Portal Fields
        private const uint HOSTPORTAL_NumberOfMailboxRegisters = 8;
        private const uint HOSTPORTAL_NumberOfInterrupts = 32;
        private IValueRegisterField[] HOSTPORTAL_mailboxRegister = new IValueRegisterField[HOSTPORTAL_NumberOfMailboxRegisters];
        private IFlagRegisterField[] HOSTPORTAL_interrupt = new IFlagRegisterField[HOSTPORTAL_NumberOfInterrupts];
        private IFlagRegisterField[] HOSTPORTAL_interruptEnable = new IFlagRegisterField[HOSTPORTAL_NumberOfInterrupts];
        private bool HOSTPORTAL_powerUpOngoing = false;
        private bool HOSTPORTAL_PowerUpRequest
        {
            set
            {
                if (value)
                {
                    HOSTPORTAL_powerUpOngoing = value;
                }
            }
            get
            {
                return HOSTPORTAL_powerUpOngoing;
            }
        }
        private bool HOSTPORTAL_PowerUpAck
        {
            get
            {
                if (HOSTPORTAL_powerUpOngoing)
                {
                    HOSTPORTAL_powerUpOngoing = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
#endregion

#region LPW0 Portal Fields
        private const uint LPW0PORTAL_NumberOfMailboxRegisters = 8;
        private const uint LPW0PORTAL_NumberOfInterrupts = 32;
        private IValueRegisterField[] LPW0PORTAL_mailboxRegister = new IValueRegisterField[LPW0PORTAL_NumberOfMailboxRegisters];
        private IFlagRegisterField[] LPW0PORTAL_interrupt = new IFlagRegisterField[LPW0PORTAL_NumberOfInterrupts];
        private IFlagRegisterField[] LPW0PORTAL_interruptEnable = new IFlagRegisterField[LPW0PORTAL_NumberOfInterrupts];
        private bool LPW0PORTAL_powerUpOngoing = false;
        private bool LPW0PORTAL_PowerUpRequest
        {
            set
            {
                if (value)
                {
                    LPW0PORTAL_powerUpOngoing = value;
                }
            }
            get
            {
                return LPW0PORTAL_powerUpOngoing;
            }
        }
        private bool LPW0PORTAL_PowerUpAck
        {
            get
            {
                if (LPW0PORTAL_powerUpOngoing)
                {
                    LPW0PORTAL_powerUpOngoing = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
#endregion

#region RF Mailbox Fields
private const uint RFMAILBOX_MessageNumber = 4;
private IValueRegisterField[] RFMAILBOX_messagePointer = new IValueRegisterField[RFMAILBOX_MessageNumber];
private IFlagRegisterField[] RFMAILBOX_messageInterrupt = new IFlagRegisterField[RFMAILBOX_MessageNumber];
private IFlagRegisterField[] RFMAILBOX_messageInterruptEnable = new IFlagRegisterField[RFMAILBOX_MessageNumber];
#endregion

#region FSW Mailbox Fields
private const uint FSWMAILBOX_MessageNumber = 4;
private IValueRegisterField[] FSWMAILBOX_messagePointer = new IValueRegisterField[FSWMAILBOX_MessageNumber];
private IFlagRegisterField[] FSWMAILBOX_messageInterrupt = new IFlagRegisterField[FSWMAILBOX_MessageNumber];
private IFlagRegisterField[] FSWMAILBOX_messageInterruptEnable = new IFlagRegisterField[FSWMAILBOX_MessageNumber];
#endregion

#region FRC enums
        private enum FRC_FSMState
        {
            Idle                = 0,
            RxInit              = 1,
            RxData              = 2,
            RxCrc               = 3,
            RxFcdUpdate         = 4,
            RxDiscard           = 5,
            RxTrail             = 6,
            RxDone              = 7,
            RxPauseInit         = 8,
            RxPaused            = 9,
            Undefined1          = 10,
            Undefined2          = 11,
            RxCrcZeroCheck      = 12,
            RxSup               = 13,
            RxWaitEof           = 14,
            Undefined3          = 15,
            TxInit              = 16,
            TxData              = 17,
            TxCrc               = 18,
            TxFdcUpdate         = 19,
            TxTrail             = 20,
            TxFlush             = 21,
            TxDone              = 22,
            TxDoneWait          = 23,
            TxRaw               = 24,
            TxPauseFlush        = 25,
        }

        private enum FRC_RxRawDataMode
        {
            Disable           = 0,
            SingleItem        = 1,
            SingleBuffer      = 2,
            SingleBufferFrame = 3,
            RepeatBuffer      = 4,
        }

        private enum FRC_RxRawDataTriggerMode
        {
            Immediate      = 0,
            PRS            = 1,
            InternalSignal = 2,
        }

        private enum FRC_DynamicFrameLengthMode
        {
            Disable          = 0,
            SingleByte       = 1,
            SingleByteMSB    = 2,
            DualByteLSBFirst = 3,
            DualByteMSBFirst = 4,
            Infinite         = 5,
            BlockError       = 6,
        }

        private enum FRC_DynamicFrameLengthBitOrder
        {
            Normal  = 0,
            Reverse = 1,
        }

        private enum FRC_FrameDescriptorMode
        {
            FrameDescriptorMode0 = 0,
            FrameDescriptorMode1 = 1,
            FrameDescriptorMode2 = 2,
            FrameDescriptorMode3 = 3,
        }
#endregion

#region RAC enums
        private enum RAC_RadioState
        {
            Off                 = 0,
            RxWarm              = 1,
            RxSearch            = 2,
            RxFrame             = 3,
            RxWrapUp            = 4,
            TxWarm              = 5,
            Tx                  = 6,
            TxWrapUp            = 7,
            Shutdown            = 8,
            PowerOnReset        = 9,
        }

        private enum RAC_RadioStateMachineSignal
        {
            None                    = 0,
            Reset                   = 1, 
            ForceDisable            = 2,
            ForceTx                 = 3,
            RxEnable                = 4,
            TxEnable                = 5,
            RxDone                  = 6,
            TxDone                  = 7,
            FrameDetected           = 8,
            RxCalibration           = 9,
            RxOverflow              = 10,
            SequencerDelay          = 11,
            SequencerEnd            = 12,
            RxDisable               = 13,
            TxAfterFrameActive      = 14,
            TxAfterFramePending     = 15,
            TxDisable               = 16,
            TxWarmExit              = 17,
            TxExit                  = 18,
            TxWrapUpExit            = 19,
            RxWarmExit              = 20,
            RxSearchExit            = 21,
            RxFrameExit             = 22,
            RxWrapUpExit            = 23,
            ShutdownExit            = 24,
            OffExit                 = 25,
            ClearRxOverflow         = 26,
            RxAbort                 = 27,
        }

        private enum RAC_InternalRxState
        {
            Idle                = 0,
            PreambleAndSyncWord = 1,
            Frame               = 2,
        }

        private enum RAC_InternalTxState
        {
            Idle         = 0,
            Tx           = 1,
        }
#endregion

#region BUFC enums
        public enum BUFC_SizeMode
        {
            Size64   = 0x0,
            Size128  = 0x1,
            Size256  = 0x2,
            Size512  = 0x3,
            Size1024 = 0x4,
            Size2048 = 0x5,
            Size4096 = 0x6,
        }

        public enum BUFC_ThresholdMode
        {
            Larger      = 0x0,
            LessOrEqual = 0x1,
        }
#endregion

#region PROTIMER enum
        private enum PROTIMER_PreCounterSource
        {
            Disabled  = 0x0,
            Clock     = 0x1,
            Unused0   = 0x2,
            Unused1   = 0x3,
        }

        private enum PROTIMER_BaseCounterSource
        {
            Disabled            = 0x0,
            PreCounterOverflow  = 0x1,
            Unused0             = 0x2,
            Unused1             = 0x3,
        }

        private enum PROTIMER_WrapCounterSource
        {
            Disabled            = 0x0,
            PreCounterOverflow  = 0x1,
            BaseCounterOverflow = 0x2,
            Unused              = 0x3,
        }

        private enum PROTIMER_TimeoutCounterSource
        {
            Disabled            = 0x0,
            PreCounterOverflow  = 0x1,
            BaseCounterOverflow = 0x2,
            WrapCounterOverflow = 0x3,
        }

        private enum PROTIMER_RepeatMode
        {
            Free    = 0x0,
            OneShot = 0x1,
        }

        public enum PROTIMER_Event
        {
            Disabled = 0,
            Always = 1,
            PreCounterOverflow = 2,
            BaseCounterOverflow = 3,
            WrapCounterOverflow = 4,
            TimeoutCounter0Underflow = 5,
            TimeoutCounter1Underflow = 6,
            TimeoutCounter0Match = 7,
            TimeoutCounter1Match = 8,
            CaptureCompareChannel0Event = 9,
            CaptureCompareChannel1Event = 10,
            CaptureCompareChannel2Event = 11,
            CaptureCompareChannel3Event = 12,
            CaptureCompareChannel4Event = 13,
            TxDone = 14,
            RxDone = 15,
            TxOrRxDone = 16,
            Syncword0Detected = 17,
            Syncword1Detected = 18,
            Syncword0Or1Detected = 19,
            ListenBeforeTalkSuccess = 20,
            ListenBeforeTalkRetry = 21,
            ListenBeforeTalkFailure = 22,
            AnyListenBeforeTalk = 23,
            ClearChannelAssessmentMeasurementCompleted = 24,
            ClearChannelAssessmentMeasurementCompletedChannelClear = 25,
            ClearChannelAssessmentMeasurementCompletedChannelBusy = 26,
            TimeoutCounter0MatchListenBeforeTalk = 27,
            CaptureCompareChannel5Event = 28,
            CaptureCompareChannel6Event = 29,
            CaptureCompareChannel7Event = 30,
            CaptureCompareChannel8Event = 31,
            CaptureCompareChannel9Event = 32,
            CaptureCompareChannel10Event = 33,
            CaptureCompareChannel11Event = 34,
            TimeoutCounter2Underflow = 35,
            TimeoutCounter2Match = 36,
            InternalTrigger = 37,
        }

        private enum PROTIMER_CaptureCompareMode
        {
            Compare   = 0,
            Capture   = 1,
            WrapRange = 2,
            None      = 3,
        }

        private enum PROTIMER_CaptureInputSource
        {
            PRS                             = 0,
            TxDone                          = 1,
            RxDone                          = 2,
            TxOrRxDone                      = 3,
            DemodulatorFoundSyncWord0       = 4,
            DemodulatorFoundSyncWord1       = 5,
            DemodulatorFoundSyncWord2       = 6,
            DemodulatorFoundSyncWord3       = 7,
            DemodulatorFoundAnySyncWord     = 8,
            ModulatorSyncWordSent           = 9,
            RxAtEndOfFrameFromDemodulator   = 10,
            ProRtcCaptureCompare0           = 11,
            ProRtcCaptureCompare1           = 12,
            RacState0                       = 13,
            RacState1                       = 14,
        }

        private enum PROTIMER_TxRxRequestState
        {
            Idle,
            SetEvent1,
            Set,
            ClearEvent1,
        }

        private enum PROTIMER_ListenBeforeTalkState
        {
            Idle        = 0,
            Backoff     = 1,
            CcaDelay    = 2,
        }

        private enum PROTIMER_PreCountOverflowSourced : uint
        {
            BaseCounter             = 0x00000001,
            WrapCounter             = 0x00000002,
            TimeoutCounter0         = 0x00000004,
            TimeoutCounter1         = 0x00000008,
            TimeoutCounter2         = 0x00000010,
            CaptureCompareChannel0  = 0x00000020,
            CaptureCompareChannel1  = 0x00000040,
            CaptureCompareChannel2  = 0x00000080,
            CaptureCompareChannel3  = 0x00000100,
            CaptureCompareChannel4  = 0x00000200,
            CaptureCompareChannel5  = 0x00000400,
            CaptureCompareChannel6  = 0x00000800,
            CaptureCompareChannel7  = 0x00001000,
            CaptureCompareChannel8  = 0x00002000,
            CaptureCompareChannel9  = 0x00004000,
            CaptureCompareChannel10 = 0x00008000,
            CaptureCompareChannel11 = 0x00010000,
        }
#endregion

#region MODEM enums
        private enum MODEM_RampMode
        {
            Linear = 0,
            Offset = 1,
        }

        public enum MODEM_DemodulatorState
        {
            Off = 0x0,
            TimingSearch = 0x1,
            PreambleSearch = 0x2,
            FrameSearch = 0x3,
            RxFrame = 0x4,
            TimingSearchWithSlidingWindow = 0x5,
        }

        private enum MODEM_DsssShiftedSymbols
        {
            ShiftedSymbol0 = 0,
            ShiftedSymbol1 = 1,
            ShiftedSymbol3 = 3,
            ShiftedSymbol7 = 7,
        }

        private enum MODEM_DsssDoublingMode
        {
            Disabled          = 0,
            Inverted          = 1,
            ComplexConjugated = 2,
        }

        private enum MODEM_ModulationFormat
        {
            FSK2     = 0,
            FSK4     = 1,
            BPSK     = 2,
            DBPSK    = 3,
            OQPSK    = 4,
            MSK      = 5,
            OOKASK   = 6,
            MOOK     = 7,
        }

        private enum MODEM_SymbolCoding
        {
            Nrz        = 0,
            Manchester = 1,
            Dsss       = 2,
            Linecode   = 3,
        }
#endregion

#region AGC enums
        private enum AGC_CcaMode
        {
            Mode1 = 0,
            Mode2 = 1,
            Mode3 = 2,
            Mode4 = 3,
        }

        private enum AGC_CcaMode3Logic
        {
            Or  = 0,
            And = 1,
        }

        private enum AGC_RssiState
        {
            Idle          = 0,
            Condition     = 1,
            Period        = 2,
            Command       = 3,
            FameDetection = 4,
        }
#endregion

#region SYNTH enums
        private enum SYNTH_State
        {
            Idle                      = 0,
            Ready                     = 1,
            CapacitorArrayCalibration = 2,
        }
#endregion

#region CRC enums
        private enum CRC_CrcWidthMode
        {
            Width8 = 0x0,
            Width16 = 0x1,
            Width24 = 0x2,
            Width32 = 0x3,
        }
#endregion

#region Register enums
        private enum FrameControllerRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            Status                                                          = 0x0008,
            DynamicFrameLengthControl                                       = 0x000C,
            MaximumFrameLength                                              = 0x0010,
            AddressFilterControl                                            = 0x0014,
            FrameControllerDataBuffer                                       = 0x0018,
            WordCounter                                                     = 0x001C,
            WordCounterCompare0                                             = 0x0020,
            WordCounterCompare1                                             = 0x0024,
            WordCounterCompare2                                             = 0x0028,
            Command                                                         = 0x002C,
            WhitenerControl                                                 = 0x0030,
            WhitenerPolynomial                                              = 0x0034,
            WhitenerInitialValue                                            = 0x0038,
            ForwardErrorCorrectionControl                                   = 0x003C,
            BlockDecodingRamAddress                                         = 0x0040,
            ConvolutionalDecodingRamAddress                                 = 0x0044,
            Control                                                         = 0x0048,
            RxControl                                                       = 0x004C,
            TrailingTxDataControl                                           = 0x0050,
            TrailingRxData                                                  = 0x0054,
            SubFrameCounterValue                                            = 0x0058,
            ConvolutionalCoderGeneratorPolynomials                          = 0x005C,
            PuncturingControl                                               = 0x0060,
            PauseControl                                                    = 0x0064,
            InterruptFlags                                                  = 0x0068,
            InterruptEnable                                                 = 0x006C,
            OverTheAirNumberOfBitsCounter                                   = 0x0070,
            BufferControl                                                   = 0x0078,
            SnifferControl                                                  = 0x0084,
            AuxiliarySnifferDataOutput                                      = 0x0088,
            RawDataControl                                                  = 0x008C,
            RxRawData                                                       = 0x0090,
            RxPauseData                                                     = 0x0094,
            MostLikelyConvolutionalDecoderState                             = 0x0098,
            InterleaverElementValue                                         = 0x009C,
            InterleaverWritePointer                                         = 0x00A0,
            InterleaverReadPointer                                          = 0x00A4,
            AutomaticClockGating                                            = 0x00A8,
            AutomaticClockGatingClockStop                                   = 0x00AC,
            SequencerInterruptFlags                                         = 0x00B4,
            SequencerInterruptEnable                                        = 0x00B8,
            WordCounterCompare3                                             = 0x00BC,
            BitOfInterestControl                                            = 0x00C0,
            DynamicSuppLengthControl                                        = 0x00C4,
            WordCounterCompare4                                             = 0x00C8,
            WordCounterCompare5                                             = 0x00CC,
            PacketCaptureBufferControl                                      = 0x00D0,
            PacketCaptureBufferStatus                                       = 0x00D4,
            PacketCaptureDataBuffer0                                        = 0x00D8,
            PacketCaptureDataBuffer1                                        = 0x00DC,
            PacketCaptureDataBuffer2                                        = 0x00E0,
            PacketCaptureDataBuffer3                                        = 0x00E4,
            PacketCaptureDataBuffer4                                        = 0x00E8,
            PacketCaptureDataBuffer5                                        = 0x00EC,
            PacketCaptureDataBuffer6                                        = 0x00F0,
            PacketCaptureDataBuffer7                                        = 0x00F4,
            PacketCaptureDataBuffer8                                        = 0x00F8,
            PacketCaptureDataBuffer9                                        = 0x00FC,
            PacketCaptureDataBuffer10                                       = 0x0100,
            PacketCaptureDataBuffer11                                       = 0x0104,
            FastSwitchInterruptFlags                                        = 0x0108,
            FastSwitchInterruptEnable                                       = 0x010C,
            FrameControlDescriptor0                                         = 0x0110,
            FrameControlDescriptor1                                         = 0x0114,
            FrameControlDescriptor2                                         = 0x0118,
            FrameControlDescriptor3                                         = 0x011C,
            InterleaverElementValue0                                        = 0x0140,
            InterleaverElementValue1                                        = 0x0144,
            InterleaverElementValue2                                        = 0x0148,
            InterleaverElementValue3                                        = 0x014C,
            InterleaverElementValue4                                        = 0x0150,
            InterleaverElementValue5                                        = 0x0154,
            InterleaverElementValue6                                        = 0x0158,
            InterleaverElementValue7                                        = 0x015C,
            InterleaverElementValue8                                        = 0x0160,
            InterleaverElementValue9                                        = 0x0164,
            InterleaverElementValue10                                       = 0x0168,
            InterleaverElementValue11                                       = 0x016C,
            InterleaverElementValue12                                       = 0x0170,
            InterleaverElementValue13                                       = 0x0174,
            InterleaverElementValue14                                       = 0x0178,
            InterleaverElementValue15                                       = 0x017C,
            AhbConfiguration                                                = 0x0180,
            Spare                                                           = 0x0184,
            // Set Registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            Status_Set                                                      = 0x1008,
            DynamicFrameLengthControl_Set                                   = 0x100C,
            MaximumFrameLength_Set                                          = 0x1010,
            AddressFilterControl_Set                                        = 0x1014,
            FrameControllerDataBuffer_Set                                   = 0x1018,
            WordCounter_Set                                                 = 0x101C,
            WordCounterCompare0_Set                                         = 0x1020,
            WordCounterCompare1_Set                                         = 0x1024,
            WordCounterCompare2_Set                                         = 0x1028,
            Command_Set                                                     = 0x102C,
            WhitenerControl_Set                                             = 0x1030,
            WhitenerPolynomial_Set                                          = 0x1034,
            WhitenerInitialValue_Set                                        = 0x1038,
            ForwardErrorCorrectionControl_Set                               = 0x103C,
            BlockDecodingRamAddress_Set                                     = 0x1040,
            ConvolutionalDecodingRamAddress_Set                             = 0x1044,
            Control_Set                                                     = 0x1048,
            RxControl_Set                                                   = 0x104C,
            TrailingTxDataControl_Set                                       = 0x1050,
            TrailingRxData_Set                                              = 0x1054,
            SubFrameCounterValue_Set                                        = 0x1058,
            ConvolutionalCoderGeneratorPolynomials_Set                      = 0x105C,
            PuncturingControl_Set                                           = 0x1060,
            PauseControl_Set                                                = 0x1064,
            InterruptFlags_Set                                              = 0x1068,
            InterruptEnable_Set                                             = 0x106C,
            OverTheAirNumberOfBitsCounter_Set                               = 0x1070,
            BufferControl_Set                                               = 0x1078,
            SnifferControl_Set                                              = 0x1084,
            AuxiliarySnifferDataOutput_Set                                  = 0x1088,
            RawDataControl_Set                                              = 0x108C,
            RxRawData_Set                                                   = 0x1090,
            RxPauseData_Set                                                 = 0x1094,
            MostLikelyConvolutionalDecoderState_Set                         = 0x1098,
            InterleaverElementValue_Set                                     = 0x109C,
            InterleaverWritePointer_Set                                     = 0x10A0,
            InterleaverReadPointer_Set                                      = 0x10A4,
            AutomaticClockGating_Set                                        = 0x10A8,
            AutomaticClockGatingClockStop_Set                               = 0x10AC,
            SequencerInterruptFlags_Set                                     = 0x10B4,
            SequencerInterruptEnable_Set                                    = 0x10B8,
            WordCounterCompare3_Set                                         = 0x10BC,
            BitOfInterestControl_Set                                        = 0x10C0,
            DynamicSuppLengthControl_Set                                    = 0x10C4,
            WordCounterCompare4_Set                                         = 0x10C8,
            WordCounterCompare5_Set                                         = 0x10CC,
            PacketCaptureBufferControl_Set                                  = 0x10D0,
            PacketCaptureBufferStatus_Set                                   = 0x10D4,
            PacketCaptureDataBuffer0_Set                                    = 0x10D8,
            PacketCaptureDataBuffer1_Set                                    = 0x10DC,
            PacketCaptureDataBuffer2_Set                                    = 0x10E0,
            PacketCaptureDataBuffer3_Set                                    = 0x10E4,
            PacketCaptureDataBuffer4_Set                                    = 0x10E8,
            PacketCaptureDataBuffer5_Set                                    = 0x10EC,
            PacketCaptureDataBuffer6_Set                                    = 0x10F0,
            PacketCaptureDataBuffer7_Set                                    = 0x10F4,
            PacketCaptureDataBuffer8_Set                                    = 0x10F8,
            PacketCaptureDataBuffer9_Set                                    = 0x10FC,
            PacketCaptureDataBuffer10_Set                                   = 0x1100,
            PacketCaptureDataBuffer11_Set                                   = 0x1104,
            FastSwitchInterruptFlags_Set                                    = 0x1108,
            FastSwitchInterruptEnable_Set                                   = 0x110C,
            FrameControlDescriptor0_Set                                     = 0x1110,
            FrameControlDescriptor1_Set                                     = 0x1114,
            FrameControlDescriptor2_Set                                     = 0x1118,
            FrameControlDescriptor3_Set                                     = 0x111C,
            InterleaverElementValue0_Set                                    = 0x1140,
            InterleaverElementValue1_Set                                    = 0x1144,
            InterleaverElementValue2_Set                                    = 0x1148,
            InterleaverElementValue3_Set                                    = 0x114C,
            InterleaverElementValue4_Set                                    = 0x1150,
            InterleaverElementValue5_Set                                    = 0x1154,
            InterleaverElementValue6_Set                                    = 0x1158,
            InterleaverElementValue7_Set                                    = 0x115C,
            InterleaverElementValue8_Set                                    = 0x1160,
            InterleaverElementValue9_Set                                    = 0x1164,
            InterleaverElementValue10_Set                                   = 0x1168,
            InterleaverElementValue11_Set                                   = 0x116C,
            InterleaverElementValue12_Set                                   = 0x1170,
            InterleaverElementValue13_Set                                   = 0x1174,
            InterleaverElementValue14_Set                                   = 0x1178,
            InterleaverElementValue15_Set                                   = 0x117C,
            AhbConfiguration_Set                                            = 0x1180,
            Spare_Set                                                       = 0x1184,
            // Clear Registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            Status_Clr                                                      = 0x2008,
            DynamicFrameLengthControl_Clr                                   = 0x200C,
            MaximumFrameLength_Clr                                          = 0x2010,
            AddressFilterControl_Clr                                        = 0x2014,
            FrameControllerDataBuffer_Clr                                   = 0x2018,
            WordCounter_Clr                                                 = 0x201C,
            WordCounterCompare0_Clr                                         = 0x2020,
            WordCounterCompare1_Clr                                         = 0x2024,
            WordCounterCompare2_Clr                                         = 0x2028,
            Command_Clr                                                     = 0x202C,
            WhitenerControl_Clr                                             = 0x2030,
            WhitenerPolynomial_Clr                                          = 0x2034,
            WhitenerInitialValue_Clr                                        = 0x2038,
            ForwardErrorCorrectionControl_Clr                               = 0x203C,
            BlockDecodingRamAddress_Clr                                     = 0x2040,
            ConvolutionalDecodingRamAddress_Clr                             = 0x2044,
            Control_Clr                                                     = 0x2048,
            RxControl_Clr                                                   = 0x204C,
            TrailingTxDataControl_Clr                                       = 0x2050,
            TrailingRxData_Clr                                              = 0x2054,
            SubFrameCounterValue_Clr                                        = 0x2058,
            ConvolutionalCoderGeneratorPolynomials_Clr                      = 0x205C,
            PuncturingControl_Clr                                           = 0x2060,
            PauseControl_Clr                                                = 0x2064,
            InterruptFlags_Clr                                              = 0x2068,
            InterruptEnable_Clr                                             = 0x206C,
            OverTheAirNumberOfBitsCounter_Clr                               = 0x2070,
            BufferControl_Clr                                               = 0x2078,
            SnifferControl_Clr                                              = 0x2084,
            AuxiliarySnifferDataOutput_Clr                                  = 0x2088,
            RawDataControl_Clr                                              = 0x208C,
            RxRawData_Clr                                                   = 0x2090,
            RxPauseData_Clr                                                 = 0x2094,
            MostLikelyConvolutionalDecoderState_Clr                         = 0x2098,
            InterleaverElementValue_Clr                                     = 0x209C,
            InterleaverWritePointer_Clr                                     = 0x20A0,
            InterleaverReadPointer_Clr                                      = 0x20A4,
            AutomaticClockGating_Clr                                        = 0x20A8,
            AutomaticClockGatingClockStop_Clr                               = 0x20AC,
            SequencerInterruptFlags_Clr                                     = 0x20B4,
            SequencerInterruptEnable_Clr                                    = 0x20B8,
            WordCounterCompare3_Clr                                         = 0x20BC,
            BitOfInterestControl_Clr                                        = 0x20C0,
            DynamicSuppLengthControl_Clr                                    = 0x20C4,
            WordCounterCompare4_Clr                                         = 0x20C8,
            WordCounterCompare5_Clr                                         = 0x20CC,
            PacketCaptureBufferControl_Clr                                  = 0x20D0,
            PacketCaptureBufferStatus_Clr                                   = 0x20D4,
            PacketCaptureDataBuffer0_Clr                                    = 0x20D8,
            PacketCaptureDataBuffer1_Clr                                    = 0x20DC,
            PacketCaptureDataBuffer2_Clr                                    = 0x20E0,
            PacketCaptureDataBuffer3_Clr                                    = 0x20E4,
            PacketCaptureDataBuffer4_Clr                                    = 0x20E8,
            PacketCaptureDataBuffer5_Clr                                    = 0x20EC,
            PacketCaptureDataBuffer6_Clr                                    = 0x20F0,
            PacketCaptureDataBuffer7_Clr                                    = 0x20F4,
            PacketCaptureDataBuffer8_Clr                                    = 0x20F8,
            PacketCaptureDataBuffer9_Clr                                    = 0x20FC,
            PacketCaptureDataBuffer10_Clr                                   = 0x2100,
            PacketCaptureDataBuffer11_Clr                                   = 0x2104,
            FastSwitchInterruptFlags_Clr                                    = 0x2108,
            FastSwitchInterruptEnable_Clr                                   = 0x210C,
            FrameControlDescriptor0_Clr                                     = 0x2110,
            FrameControlDescriptor1_Clr                                     = 0x2114,
            FrameControlDescriptor2_Clr                                     = 0x2118,
            FrameControlDescriptor3_Clr                                     = 0x211C,
            InterleaverElementValue0_Clr                                    = 0x2140,
            InterleaverElementValue1_Clr                                    = 0x2144,
            InterleaverElementValue2_Clr                                    = 0x2148,
            InterleaverElementValue3_Clr                                    = 0x214C,
            InterleaverElementValue4_Clr                                    = 0x2150,
            InterleaverElementValue5_Clr                                    = 0x2154,
            InterleaverElementValue6_Clr                                    = 0x2158,
            InterleaverElementValue7_Clr                                    = 0x215C,
            InterleaverElementValue8_Clr                                    = 0x2160,
            InterleaverElementValue9_Clr                                    = 0x2164,
            InterleaverElementValue10_Clr                                   = 0x2168,
            InterleaverElementValue11_Clr                                   = 0x216C,
            InterleaverElementValue12_Clr                                   = 0x2170,
            InterleaverElementValue13_Clr                                   = 0x2174,
            InterleaverElementValue14_Clr                                   = 0x2178,
            InterleaverElementValue15_Clr                                   = 0x217C,
            AhbConfiguration_Clr                                            = 0x2180,
            Spare_Clr                                                       = 0x2184,
            // Toggle Registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            Status_Tgl                                                      = 0x3008,
            DynamicFrameLengthControl_Tgl                                   = 0x300C,
            MaximumFrameLength_Tgl                                          = 0x3010,
            AddressFilterControl_Tgl                                        = 0x3014,
            FrameControllerDataBuffer_Tgl                                   = 0x3018,
            WordCounter_Tgl                                                 = 0x301C,
            WordCounterCompare0_Tgl                                         = 0x3020,
            WordCounterCompare1_Tgl                                         = 0x3024,
            WordCounterCompare2_Tgl                                         = 0x3028,
            Command_Tgl                                                     = 0x302C,
            WhitenerControl_Tgl                                             = 0x3030,
            WhitenerPolynomial_Tgl                                          = 0x3034,
            WhitenerInitialValue_Tgl                                        = 0x3038,
            ForwardErrorCorrectionControl_Tgl                               = 0x303C,
            BlockDecodingRamAddress_Tgl                                     = 0x3040,
            ConvolutionalDecodingRamAddress_Tgl                             = 0x3044,
            Control_Tgl                                                     = 0x3048,
            RxControl_Tgl                                                   = 0x304C,
            TrailingTxDataControl_Tgl                                       = 0x3050,
            TrailingRxData_Tgl                                              = 0x3054,
            SubFrameCounterValue_Tgl                                        = 0x3058,
            ConvolutionalCoderGeneratorPolynomials_Tgl                      = 0x305C,
            PuncturingControl_Tgl                                           = 0x3060,
            PauseControl_Tgl                                                = 0x3064,
            InterruptFlags_Tgl                                              = 0x3068,
            InterruptEnable_Tgl                                             = 0x306C,
            OverTheAirNumberOfBitsCounter_Tgl                               = 0x3070,
            BufferControl_Tgl                                               = 0x3078,
            SnifferControl_Tgl                                              = 0x3084,
            AuxiliarySnifferDataOutput_Tgl                                  = 0x3088,
            RawDataControl_Tgl                                              = 0x308C,
            RxRawData_Tgl                                                   = 0x3090,
            RxPauseData_Tgl                                                 = 0x3094,
            MostLikelyConvolutionalDecoderState_Tgl                         = 0x3098,
            InterleaverElementValue_Tgl                                     = 0x309C,
            InterleaverWritePointer_Tgl                                     = 0x30A0,
            InterleaverReadPointer_Tgl                                      = 0x30A4,
            AutomaticClockGating_Tgl                                        = 0x30A8,
            AutomaticClockGatingClockStop_Tgl                               = 0x30AC,
            SequencerInterruptFlags_Tgl                                     = 0x30B4,
            SequencerInterruptEnable_Tgl                                    = 0x30B8,
            WordCounterCompare3_Tgl                                         = 0x30BC,
            BitOfInterestControl_Tgl                                        = 0x30C0,
            DynamicSuppLengthControl_Tgl                                    = 0x30C4,
            WordCounterCompare4_Tgl                                         = 0x30C8,
            WordCounterCompare5_Tgl                                         = 0x30CC,
            PacketCaptureBufferControl_Tgl                                  = 0x30D0,
            PacketCaptureBufferStatus_Tgl                                   = 0x30D4,
            PacketCaptureDataBuffer0_Tgl                                    = 0x30D8,
            PacketCaptureDataBuffer1_Tgl                                    = 0x30DC,
            PacketCaptureDataBuffer2_Tgl                                    = 0x30E0,
            PacketCaptureDataBuffer3_Tgl                                    = 0x30E4,
            PacketCaptureDataBuffer4_Tgl                                    = 0x30E8,
            PacketCaptureDataBuffer5_Tgl                                    = 0x30EC,
            PacketCaptureDataBuffer6_Tgl                                    = 0x30F0,
            PacketCaptureDataBuffer7_Tgl                                    = 0x30F4,
            PacketCaptureDataBuffer8_Tgl                                    = 0x30F8,
            PacketCaptureDataBuffer9_Tgl                                    = 0x30FC,
            PacketCaptureDataBuffer10_Tgl                                   = 0x3100,
            PacketCaptureDataBuffer11_Tgl                                   = 0x3104,
            FastSwitchInterruptFlags_Tgl                                    = 0x3108,
            FastSwitchInterruptEnable_Tgl                                   = 0x310C,
            FrameControlDescriptor0_Tgl                                     = 0x3110,
            FrameControlDescriptor1_Tgl                                     = 0x3114,
            FrameControlDescriptor2_Tgl                                     = 0x3118,
            FrameControlDescriptor3_Tgl                                     = 0x311C,
            InterleaverElementValue0_Tgl                                    = 0x3140,
            InterleaverElementValue1_Tgl                                    = 0x3144,
            InterleaverElementValue2_Tgl                                    = 0x3148,
            InterleaverElementValue3_Tgl                                    = 0x314C,
            InterleaverElementValue4_Tgl                                    = 0x3150,
            InterleaverElementValue5_Tgl                                    = 0x3154,
            InterleaverElementValue6_Tgl                                    = 0x3158,
            InterleaverElementValue7_Tgl                                    = 0x315C,
            InterleaverElementValue8_Tgl                                    = 0x3160,
            InterleaverElementValue9_Tgl                                    = 0x3164,
            InterleaverElementValue10_Tgl                                   = 0x3168,
            InterleaverElementValue11_Tgl                                   = 0x316C,
            InterleaverElementValue12_Tgl                                   = 0x3170,
            InterleaverElementValue13_Tgl                                   = 0x3174,
            InterleaverElementValue14_Tgl                                   = 0x3178,
            InterleaverElementValue15_Tgl                                   = 0x317C,
            AhbConfiguration_Tgl                                            = 0x3180,
            Spare_Tgl                                                       = 0x3184,            
        }

        private enum BufferControllerRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            LowPowerMode                                                    = 0x0008,
            Buffer0Control                                                  = 0x000C,
            Buffer0Address                                                  = 0x0010,
            Buffer0WriteOffset                                              = 0x0014,
            Buffer0ReadOffset                                               = 0x0018,
            Buffer0WriteStart                                               = 0x001C,
            Buffer0ReadData                                                 = 0x0020,
            Buffer0WriteData                                                = 0x0024,
            Buffer0XorWrite                                                 = 0x0028,
            Buffer0Status                                                   = 0x002C,
            Buffer0ThresholdControl                                         = 0x0030,
            Buffer0Command                                                  = 0x0034,
            Buffer0ReadData32                                               = 0x003C,
            Buffer0WriteData32                                              = 0x0040,
            Buffer0XorWrite32                                               = 0x0044,
            Buffer1Control                                                  = 0x004C,
            Buffer1Address                                                  = 0x0050,
            Buffer1WriteOffset                                              = 0x0054,
            Buffer1ReadOffset                                               = 0x0058,
            Buffer1WriteStart                                               = 0x005C,
            Buffer1ReadData                                                 = 0x0060,
            Buffer1WriteData                                                = 0x0064,
            Buffer1XorWrite                                                 = 0x0068,
            Buffer1Status                                                   = 0x006C,
            Buffer1ThresholdControl                                         = 0x0070,
            Buffer1Command                                                  = 0x0074,
            Buffer1ReadData32                                               = 0x007C,
            Buffer1WriteData32                                              = 0x0080,
            Buffer1XorWrite32                                               = 0X0084,
            Buffer2Control                                                  = 0x008C,
            Buffer2Address                                                  = 0x0090,
            Buffer2WriteOffset                                              = 0x0094,
            Buffer2ReadOffset                                               = 0x0098,
            Buffer2WriteStart                                               = 0x009C,
            Buffer2ReadData                                                 = 0x00A0,
            Buffer2WriteData                                                = 0x00A4,
            Buffer2XorWrite                                                 = 0x00A8,
            Buffer2Status                                                   = 0x00AC,
            Buffer2ThresholdControl                                         = 0x00B0,
            Buffer2Command                                                  = 0x00B4,
            Buffer2ReadData32                                               = 0x00BC,
            Buffer2WriteData32                                              = 0x00C0,
            Buffer2XorWrite32                                               = 0x00C4,
            Buffer3Control                                                  = 0x00CC,
            Buffer3Address                                                  = 0x00D0,
            Buffer3WriteOffset                                              = 0x00D4,
            Buffer3ReadOffset                                               = 0x00D8,
            Buffer3WriteStart                                               = 0x00DC,
            Buffer3ReadData                                                 = 0x00E0,
            Buffer3WriteData                                                = 0x00E4,
            Buffer3XorWrite                                                 = 0x00E8,
            Buffer3Status                                                   = 0x00EC,
            Buffer3ThresholdControl                                         = 0x00F0,
            Buffer3Command                                                  = 0x00F4,
            Buffer3ReadData32                                               = 0x00FC,
            Buffer3WriteData32                                              = 0x0100,
            Buffer3XorWrite32                                               = 0x0104,
            InterruptFlags                                                  = 0x0114,
            InterruptEnable                                                 = 0x0118,
            SequencerInterruptFlags                                         = 0x011C,
            SequencerInterruptEnable                                        = 0x0120,
            SoftModemInterruptFlags                                         = 0x0124,
            SoftModemInterruptEnable                                        = 0x0128,
            FastSwitchInterruptFlags                                        = 0x012C,
            FastSwitchInterruptEnable                                       = 0x0130,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            LowPowerMode_Set                                                = 0x1008,
            Buffer0Control_Set                                              = 0x100C,
            Buffer0Address_Set                                              = 0x1010,
            Buffer0WriteOffset_Set                                          = 0x1014,
            Buffer0ReadOffset_Set                                           = 0x1018,
            Buffer0WriteStart_Set                                           = 0x101C,
            Buffer0ReadData_Set                                             = 0x1020,
            Buffer0WriteData_Set                                            = 0x1024,
            Buffer0XorWrite_Set                                             = 0x1028,
            Buffer0Status_Set                                               = 0x102C,
            Buffer0ThresholdControl_Set                                     = 0x1030,
            Buffer0Command_Set                                              = 0x1034,
            Buffer0ReadData32_Set                                           = 0x103C,
            Buffer0WriteData32_Set                                          = 0x1040,
            Buffer0XorWrite32_Set                                           = 0x1044,
            Buffer1Control_Set                                              = 0x104C,
            Buffer1Address_Set                                              = 0x1050,
            Buffer1WriteOffset_Set                                          = 0x1054,
            Buffer1ReadOffset_Set                                           = 0x1058,
            Buffer1WriteStart_Set                                           = 0x105C,
            Buffer1ReadData_Set                                             = 0x1060,
            Buffer1WriteData_Set                                            = 0x1064,
            Buffer1XorWrite_Set                                             = 0x1068,
            Buffer1Status_Set                                               = 0x106C,
            Buffer1ThresholdControl_Set                                     = 0x1070,
            Buffer1Command_Set                                              = 0x1074,
            Buffer1ReadData32_Set                                           = 0x107C,
            Buffer1WriteData32_Set                                          = 0x1080,
            Buffer1XorWrite32_Set                                           = 0X0084,
            Buffer2Control_Set                                              = 0x108C,
            Buffer2Address_Set                                              = 0x1090,
            Buffer2WriteOffset_Set                                          = 0x1094,
            Buffer2ReadOffset_Set                                           = 0x1098,
            Buffer2WriteStart_Set                                           = 0x109C,
            Buffer2ReadData_Set                                             = 0x10A0,
            Buffer2WriteData_Set                                            = 0x10A4,
            Buffer2XorWrite_Set                                             = 0x10A8,
            Buffer2Status_Set                                               = 0x10AC,
            Buffer2ThresholdControl_Set                                     = 0x10B0,
            Buffer2Command_Set                                              = 0x10B4,
            Buffer2ReadData32_Set                                           = 0x10BC,
            Buffer2WriteData32_Set                                          = 0x10C0,
            Buffer2XorWrite32_Set                                           = 0x10C4,
            Buffer3Control_Set                                              = 0x10CC,
            Buffer3Address_Set                                              = 0x10D0,
            Buffer3WriteOffset_Set                                          = 0x10D4,
            Buffer3ReadOffset_Set                                           = 0x10D8,
            Buffer3WriteStart_Set                                           = 0x10DC,
            Buffer3ReadData_Set                                             = 0x10E0,
            Buffer3WriteData_Set                                            = 0x10E4,
            Buffer3XorWrite_Set                                             = 0x10E8,
            Buffer3Status_Set                                               = 0x10EC,
            Buffer3ThresholdControl_Set                                     = 0x10F0,
            Buffer3Command_Set                                              = 0x10F4,
            Buffer3ReadData32_Set                                           = 0x10FC,
            Buffer3WriteData32_Set                                          = 0x1100,
            Buffer3XorWrite32_Set                                           = 0x1104,
            InterruptFlags_Set                                              = 0x1114,
            InterruptEnable_Set                                             = 0x1118,
            SequencerInterruptFlags_Set                                     = 0x111C,
            SequencerInterruptEnable_Set                                    = 0x1120,
            SoftModemInterruptFlags_Set                                     = 0x1124,
            SoftModemInterruptEnable_Set                                    = 0x1128,
            FastSwitchInterruptFlags_Set                                    = 0x112C,
            FastSwitchInterruptEnable_Set                                   = 0x1130,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            LowPowerMode_Clr                                                = 0x2008,
            Buffer0Control_Clr                                              = 0x200C,
            Buffer0Address_Clr                                              = 0x2010,
            Buffer0WriteOffset_Clr                                          = 0x2014,
            Buffer0ReadOffset_Clr                                           = 0x2018,
            Buffer0WriteStart_Clr                                           = 0x201C,
            Buffer0ReadData_Clr                                             = 0x2020,
            Buffer0WriteData_Clr                                            = 0x2024,
            Buffer0XorWrite_Clr                                             = 0x2028,
            Buffer0Status_Clr                                               = 0x202C,
            Buffer0ThresholdControl_Clr                                     = 0x2030,
            Buffer0Command_Clr                                              = 0x2034,
            Buffer0ReadData32_Clr                                           = 0x203C,
            Buffer0WriteData32_Clr                                          = 0x2040,
            Buffer0XorWrite32_Clr                                           = 0x2044,
            Buffer1Control_Clr                                              = 0x204C,
            Buffer1Address_Clr                                              = 0x2050,
            Buffer1WriteOffset_Clr                                          = 0x2054,
            Buffer1ReadOffset_Clr                                           = 0x2058,
            Buffer1WriteStart_Clr                                           = 0x205C,
            Buffer1ReadData_Clr                                             = 0x2060,
            Buffer1WriteData_Clr                                            = 0x2064,
            Buffer1XorWrite_Clr                                             = 0x2068,
            Buffer1Status_Clr                                               = 0x206C,
            Buffer1ThresholdControl_Clr                                     = 0x2070,
            Buffer1Command_Clr                                              = 0x2074,
            Buffer1ReadData32_Clr                                           = 0x207C,
            Buffer1WriteData32_Clr                                          = 0x2080,
            Buffer1XorWrite32_Clr                                           = 0X0084,
            Buffer2Control_Clr                                              = 0x208C,
            Buffer2Address_Clr                                              = 0x2090,
            Buffer2WriteOffset_Clr                                          = 0x2094,
            Buffer2ReadOffset_Clr                                           = 0x2098,
            Buffer2WriteStart_Clr                                           = 0x209C,
            Buffer2ReadData_Clr                                             = 0x20A0,
            Buffer2WriteData_Clr                                            = 0x20A4,
            Buffer2XorWrite_Clr                                             = 0x20A8,
            Buffer2Status_Clr                                               = 0x20AC,
            Buffer2ThresholdControl_Clr                                     = 0x20B0,
            Buffer2Command_Clr                                              = 0x20B4,
            Buffer2ReadData32_Clr                                           = 0x20BC,
            Buffer2WriteData32_Clr                                          = 0x20C0,
            Buffer2XorWrite32_Clr                                           = 0x20C4,
            Buffer3Control_Clr                                              = 0x20CC,
            Buffer3Address_Clr                                              = 0x20D0,
            Buffer3WriteOffset_Clr                                          = 0x20D4,
            Buffer3ReadOffset_Clr                                           = 0x20D8,
            Buffer3WriteStart_Clr                                           = 0x20DC,
            Buffer3ReadData_Clr                                             = 0x20E0,
            Buffer3WriteData_Clr                                            = 0x20E4,
            Buffer3XorWrite_Clr                                             = 0x20E8,
            Buffer3Status_Clr                                               = 0x20EC,
            Buffer3ThresholdControl_Clr                                     = 0x20F0,
            Buffer3Command_Clr                                              = 0x20F4,
            Buffer3ReadData32_Clr                                           = 0x20FC,
            Buffer3WriteData32_Clr                                          = 0x2100,
            Buffer3XorWrite32_Clr                                           = 0x2104,
            InterruptFlags_Clr                                              = 0x2114,
            InterruptEnable_Clr                                             = 0x2118,
            SequencerInterruptFlags_Clr                                     = 0x211C,
            SequencerInterruptEnable_Clr                                    = 0x2120,
            SoftModemInterruptFlags_Clr                                     = 0x2124,
            SoftModemInterruptEnable_Clr                                    = 0x2128,
            FastSwitchInterruptFlags_Clr                                    = 0x212C,
            FastSwitchInterruptEnable_Clr                                   = 0x2130,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            LowPowerMode_Tgl                                                = 0x3008,
            Buffer0Control_Tgl                                              = 0x300C,
            Buffer0Address_Tgl                                              = 0x3010,
            Buffer0WriteOffset_Tgl                                          = 0x3014,
            Buffer0ReadOffset_Tgl                                           = 0x3018,
            Buffer0WriteStart_Tgl                                           = 0x301C,
            Buffer0ReadData_Tgl                                             = 0x3020,
            Buffer0WriteData_Tgl                                            = 0x3024,
            Buffer0XorWrite_Tgl                                             = 0x3028,
            Buffer0Status_Tgl                                               = 0x302C,
            Buffer0ThresholdControl_Tgl                                     = 0x3030,
            Buffer0Command_Tgl                                              = 0x3034,
            Buffer0ReadData32_Tgl                                           = 0x303C,
            Buffer0WriteData32_Tgl                                          = 0x3040,
            Buffer0XorWrite32_Tgl                                           = 0x3044,
            Buffer1Control_Tgl                                              = 0x304C,
            Buffer1Address_Tgl                                              = 0x3050,
            Buffer1WriteOffset_Tgl                                          = 0x3054,
            Buffer1ReadOffset_Tgl                                           = 0x3058,
            Buffer1WriteStart_Tgl                                           = 0x305C,
            Buffer1ReadData_Tgl                                             = 0x3060,
            Buffer1WriteData_Tgl                                            = 0x3064,
            Buffer1XorWrite_Tgl                                             = 0x3068,
            Buffer1Status_Tgl                                               = 0x306C,
            Buffer1ThresholdControl_Tgl                                     = 0x3070,
            Buffer1Command_Tgl                                              = 0x3074,
            Buffer1ReadData32_Tgl                                           = 0x307C,
            Buffer1WriteData32_Tgl                                          = 0x3080,
            Buffer1XorWrite32_Tgl                                           = 0X0084,
            Buffer2Control_Tgl                                              = 0x308C,
            Buffer2Address_Tgl                                              = 0x3090,
            Buffer2WriteOffset_Tgl                                          = 0x3094,
            Buffer2ReadOffset_Tgl                                           = 0x3098,
            Buffer2WriteStart_Tgl                                           = 0x309C,
            Buffer2ReadData_Tgl                                             = 0x30A0,
            Buffer2WriteData_Tgl                                            = 0x30A4,
            Buffer2XorWrite_Tgl                                             = 0x30A8,
            Buffer2Status_Tgl                                               = 0x30AC,
            Buffer2ThresholdControl_Tgl                                     = 0x30B0,
            Buffer2Command_Tgl                                              = 0x30B4,
            Buffer2ReadData32_Tgl                                           = 0x30BC,
            Buffer2WriteData32_Tgl                                          = 0x30C0,
            Buffer2XorWrite32_Tgl                                           = 0x30C4,
            Buffer3Control_Tgl                                              = 0x30CC,
            Buffer3Address_Tgl                                              = 0x30D0,
            Buffer3WriteOffset_Tgl                                          = 0x30D4,
            Buffer3ReadOffset_Tgl                                           = 0x30D8,
            Buffer3WriteStart_Tgl                                           = 0x30DC,
            Buffer3ReadData_Tgl                                             = 0x30E0,
            Buffer3WriteData_Tgl                                            = 0x30E4,
            Buffer3XorWrite_Tgl                                             = 0x30E8,
            Buffer3Status_Tgl                                               = 0x30EC,
            Buffer3ThresholdControl_Tgl                                     = 0x30F0,
            Buffer3Command_Tgl                                              = 0x30F4,
            Buffer3ReadData32_Tgl                                           = 0x30FC,
            Buffer3WriteData32_Tgl                                          = 0x3100,
            Buffer3XorWrite32_Tgl                                           = 0x3104,
            InterruptFlags_Tgl                                              = 0x3114,
            InterruptEnable_Tgl                                             = 0x3118,
            SequencerInterruptFlags_Tgl                                     = 0x311C,
            SequencerInterruptEnable_Tgl                                    = 0x3120,
            SoftModemInterruptFlags_Tgl                                     = 0x3124,
            SoftModemInterruptEnable_Tgl                                    = 0x3128,
            FastSwitchInterruptFlags_Tgl                                    = 0x312C,
            FastSwitchInterruptEnable_Tgl                                   = 0x3130,
        }

        private enum ModulatorAndDemodulatorRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            InterruptFlags                                                  = 0x0008,
            InterruptFlags2                                                 = 0x000C,
            InterruptEnable                                                 = 0x0010,
            InterruptEnable2                                                = 0x0014,
            SequencerInterruptFlags                                         = 0x0018,
            SequencerInterruptFlags2                                        = 0x001C,
            SequencerInterruptEnable                                        = 0x0020,
            SequencerInterruptEnable2                                       = 0x0024,
            Status                                                          = 0x0028,
            Status2                                                         = 0x002C,
            Status3                                                         = 0x0030,
            Status4                                                         = 0x0034,
            Status5                                                         = 0x0038,
            Status6                                                         = 0x003C,
            Status7                                                         = 0x0040,
            TimingDetectionStatus                                           = 0x0044,
            DemodulatorFSMStatus                                            = 0x0048,
            FrequencyOffsetEstimate                                         = 0x004C,
            AutomaticFrequencyControlAdjustmentRx                           = 0x0050,
            AutomaticFrequencyControldAjustmentTx                           = 0x0054,
            AnalogMixerControl                                              = 0x0058,
            Control0                                                        = 0x005C,
            Control1                                                        = 0x0060,
            Control2                                                        = 0x0064,
            Control3                                                        = 0x0068,
            Control4                                                        = 0x006C,
            Control5                                                        = 0x0070,
            Control6                                                        = 0x0074,
            TxBaudrate                                                      = 0x0078,
            RxBaudrate                                                      = 0x007C,
            ChannelFilter                                                   = 0x0080,
            Preamble                                                        = 0x0084,
            Timing                                                          = 0x0088,
            DirectSequenceSpreadSpectrumSymbol0                             = 0x008C,
            ModulationIndex                                                 = 0x0090,
            AutomaticFrequencyControl                                       = 0x0094,
            AutomaticFrequencyControlAdjustmentLimit                        = 0x0098,
            ShapingCoefficients0                                            = 0x009C,
            ShapingCoefficients1                                            = 0x00A0,
            ShapingCoefficients2                                            = 0x00A4,
            ShapingCoefficients3                                            = 0x00A8,
            ShapingCoefficients4                                            = 0x00AC,
            ShapingCoefficients5                                            = 0x00B0,
            ShapingCoefficients6                                            = 0x00B4,
            ShapingCoefficients7                                            = 0x00B8,
            ShapingCoefficients8                                            = 0x00BC,
            ShapingCoefficients9                                            = 0x00C0,
            ShapingCoefficients10                                           = 0x00C4,
            ShapingCoefficients11                                           = 0x00C8,
            ShapingCoefficients12                                           = 0x00CC,
            ShapingCoefficients13                                           = 0x00D0,
            ShapingCoefficients14                                           = 0x00D4,
            ShapingCoefficients15                                           = 0x00D8,
            RampingControl                                                  = 0x00E0,
            RampingLevels                                                   = 0x00E4,
            DirectCurrentOffsetCompensationFilterSettings                   = 0x0118,
            DirectCurrentOffsetCompensationFilterInitialization             = 0x011C,
            DirectCurrentOffsetEstimatedValue                               = 0x0120,
            SampleRateConverterRatioValuesAndChannelFilter                  = 0x0124,
            InternalAutomaticFrequencyControl                               = 0x0128,
            DigitalMixerControl                                             = 0x013C,
            BaudrateEstimate                                                = 0x0154,
            AutomaticClockGating                                            = 0x0158,
            AutomaticClockGatingClockStop                                   = 0x015C,
            PhaseOffsetEstimate                                             = 0x0160,
            DirectModeControl                                               = 0x0164,
            BleLongRange                                                    = 0x0168,
            BleLongRangeSet1                                                = 0x016C,
            BleLongRangeSet2                                                = 0x0170,
            BleLongRangeSet3                                                = 0x0174,
            BleLongRangeSet4                                                = 0x0178,
            BleLongRangeSet5                                                = 0x017C,
            BleLongRangeSet6                                                = 0x0180,
            BleLongRangeFrameControllerInterface                            = 0x0184,
            CoherentDemodulatorSignals0                                     = 0x0188,
            CoherentDemodulatorSignals1                                     = 0x018C,
            CoherentDemodulatorSignals2                                     = 0x0190,
            CoherentDemodulatorSignals3                                     = 0x0194,
            Command                                                         = 0x0198,
            SyncWordProperties                                              = 0x01A4,
            DigitalGainControl                                              = 0x01A8,
            PeripheralReflexSystemControl                                   = 0x01AC,
            EarlyTimeStampControl                                           = 0x01B8,
            EarlyTimeStampTiming                                            = 0x01BC,
            AntennaSwitchControl                                            = 0x01C0,
            AntennaSwitchControl1                                           = 0x01C4,
            AntennaSwitchStart                                              = 0x01C8,
            AntennaSwitchEnd                                                = 0x01CC,
            ConfigureAntennaPattern                                         = 0x01DC,
            ConcurrentMode                                                  = 0x01E0,
            ChannelFilterCoeSet0Group0                                      = 0x01E4,
            ChannelFilterCoeSet0Group1                                      = 0x01E8,
            ChannelFilterCoeSet0Group2                                      = 0x01EC,
            ChannelFilterCoeSet0Group3                                      = 0x01F0,
            ChannelFilterCoeSet0Group4                                      = 0x01F4,
            ChannelFilterCoeSet0Group5                                      = 0x01F8,
            ChannelFilterCoeSet0Group6                                      = 0x01FC,
            ChannelFilterCoeSet1Group0                                      = 0x0200,
            ChannelFilterCoeSet1Group1                                      = 0x0204,
            ChannelFilterCoeSet1Group2                                      = 0x0208,
            ChannelFilterCoeSet1Group3                                      = 0x020C,
            ChannelFilterCoeSet1Group4                                      = 0x0210,
            ChannelFilterCoeSet1Group5                                      = 0x0214,
            ChannelFilterCoeSet1Group6                                      = 0x0218,
            ChannelFilterControl                                            = 0x021C,
            ChannelFilterLatencyControl                                     = 0x0220,
            FrameSchTimeoutLength                                           = 0x0224,
            PreambleFilterCoefficients                                      = 0x0228,
            CollisionRestartControl                                         = 0x022C,
            PreambleSenseMode                                               = 0x0230,
            PreambleSenseModeExtended                                       = 0x0234,
            SignalQualityIndicator                                          = 0x0238,
            AntennaDiversityModeControl                                     = 0x023C,
            PhaseDemodulatorFwMode                                          = 0x0240,
            PhaseDemodulatorAntennaDiversity                                = 0x0244,
            PhaseDemodulatorAntennaDiversityDecision                        = 0x0248,
            PhaseDemodulatorControl                                         = 0x024C,
            SignalIdentifierCorrelator                                      = 0x0250,
            SignalIdentifierControl0                                        = 0x0254,
            SignalIdentifierControl1                                        = 0x0258,
            SignalIdentifierStatus                                          = 0x025C,
            ConfigureAntennaPatternExtended                                 = 0x0260,
            SignalIdentifierControl2                                        = 0x0264,
            ChannelFilterSwitchTime                                         = 0x0268,
            FirmwareHoppingControl                                          = 0x0274,
            FastSwitchInterruptFlags                                        = 0x0278,
            FastSwitchInterruptEnable                                       = 0x027C,
            FastSwitchSpare                                                 = 0x0280,
            DirectSequenceSpreadSpectrumSymbol0ForSi                        = 0x0284,
            Decimal1Log2Times4                                              = 0x0288,
            EcaTraceControl                                                 = 0x028C,
            IrCalibrationControl                                            = 0x0290,
            IrCalCoefficientValues                                          = 0x0294,
            IrCalCoefficientWrPerAntenna                                    = 0x0298,
            AdControl1                                                      = 0x02A0,
            AdControl2                                                      = 0x02A4,
            AdQual0                                                         = 0x02A8,
            AdQual1                                                         = 0x02AC,
            AdQual2                                                         = 0x02B0,
            AdQual3                                                         = 0x02B4,
            AdQual4                                                         = 0x02B8,
            AdQual5                                                         = 0x02BC,
            AdQual6                                                         = 0x02C0,
            AdQual7                                                         = 0x02C4,
            AdQual8                                                         = 0x02C8,
            AdQual9                                                         = 0x02CC,
            AdQual10                                                        = 0x02D0,
            AdFsm0                                                          = 0x02D4,
            AdFsm1                                                          = 0x02D8,
            AdFsm2                                                          = 0x02DC,
            AdFsm3                                                          = 0x02E0,
            AdFsm4                                                          = 0x02E4,
            AdFsm5                                                          = 0x02E8,
            AdFsm6                                                          = 0x02EC,
            AdFsm7                                                          = 0x02F0,
            AdFsm8                                                          = 0x02F4,
            AdFsm9                                                          = 0x02F8,
            AdFsm10                                                         = 0x02FC,
            AdFsm11                                                         = 0x0300,
            AdFsm12                                                         = 0x0304,
            AdFsm13                                                         = 0x0308,
            AdFsm14                                                         = 0x030C,
            AdFsm15                                                         = 0x0310,
            AdFsm16                                                         = 0x0314,
            AdFsm17                                                         = 0x0318,
            AdFsm18                                                         = 0x031C,
            AdFsm19                                                         = 0x0320,
            AdFsm20                                                         = 0x0324,
            AdFsm21                                                         = 0x0328,
            AdFsm22                                                         = 0x032C,
            AdFsm23                                                         = 0x0330,
            AdFsm24                                                         = 0x0334,
            AdFsm25                                                         = 0x0338,
            AdFsm26                                                         = 0x033C,
            AdFsm27                                                         = 0x0340,
            AdFsm28                                                         = 0x0344,
            AdFsm29                                                         = 0x0348,
            AdFsm30                                                         = 0x034C,
            AdPc1                                                           = 0x0350,
            AdPc2                                                           = 0x0354,
            AdPc3                                                           = 0x0358,
            AdPc4                                                           = 0x035C,
            AdPc5                                                           = 0x0360,
            AdPc6                                                           = 0x0364,
            AdPc7                                                           = 0x0368,
            AdPc8                                                           = 0x036C,
            AdPc9                                                           = 0x0370,
            AdPc10                                                          = 0x0374,
            HadmControl0                                                    = 0x03C0,
            HadmControl1                                                    = 0x03C4,
            HadmStatus0                                                     = 0x03C8,
            HadmStatus1                                                     = 0x03CC,
            HadmStatus2                                                     = 0x03D0,
            HadmStatus3                                                     = 0x03D4,
            HadmStatus4                                                     = 0x03D8,
            HadmStatus5                                                     = 0x03DC,
            HadmStatus6                                                     = 0x03E0,
            HadmControl2                                                    = 0x03E4,
            HadmControl3                                                    = 0x03E8,
            HadmControl4                                                    = 0x03EC,
            HadmStatus7                                                     = 0x03F0,
            HadmControl5                                                    = 0x03F4,
            EhDsssControl                                                   = 0x0414,
            EhDsssConfig0                                                   = 0x0418,
            EhDsssConfig1                                                   = 0x041C,
            EhDsssConfig2                                                   = 0x0420,
            EhDsssConfig3                                                   = 0x0424,
            Symbol2Chip0                                                    = 0x0428,
            Symbol2Chip8                                                    = 0x0444,
            Spare                                                           = 0x0470,
            SyncWord0                                                       = 0x0480,
            SyncWord1                                                       = 0x0484,
            SyncWord2                                                       = 0x0488,
            SyncWord3                                                       = 0x048C,
            SyncWordControl                                                 = 0x0490,
            TxControl                                                       = 0x04A0,
            TxDacValues                                                     = 0x04A4,
            TxCorrStatic                                                    = 0x04A8,
            TxCorrPte                                                       = 0x04AC,
            ViterbiDemodulator                                              = 0x0500,
            ViterbiDemodulatorCorrelationConfiguration0                     = 0x0504,
            ViterbiDemodulatorCorrelationConfiguration1                     = 0x0508,
            ViterbiDemodulatorTrackingLoop                                  = 0x050C,
            ViterbiDemodulatorBleTimestampControl                           = 0x0510,
            RealTimeCostFunctionEngineControl                               = 0x0514,
            TrecsPreamblePattern                                            = 0x0518,
            TrecsPreambleDetectionControl                                   = 0x051C,
            TrecsConfiguration                                              = 0x0520,
            TrecsDualInitialTimingSearch                                    = 0x0528,
            ExpectedPatternForDualTim                                       = 0x052C,
            AuxiliaryAdcIfControl                                           = 0x0540,
            AuxiliaryAdcDataOutput                                          = 0x0544,
            Commands                                                        = 0x0580,
            AmpAverageControl                                               = 0x0584,
            Result                                                          = 0x0588,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            InterruptFlags_Set                                              = 0x1008,
            InterruptFlags2_Set                                             = 0x100C,
            InterruptEnable_Set                                             = 0x1010,
            InterruptEnable2_Set                                            = 0x1014,
            SequencerInterruptFlags_Set                                     = 0x1018,
            SequencerInterruptFlags2_Set                                    = 0x101C,
            SequencerInterruptEnable_Set                                    = 0x1020,
            SequencerInterruptEnable2_Set                                   = 0x1024,
            Status_Set                                                      = 0x1028,
            Status2_Set                                                     = 0x102C,
            Status3_Set                                                     = 0x1030,
            Status4_Set                                                     = 0x1034,
            Status5_Set                                                     = 0x1038,
            Status6_Set                                                     = 0x103C,
            Status7_Set                                                     = 0x1040,
            TimingDetectionStatus_Set                                       = 0x1044,
            DemodulatorFSMStatus_Set                                        = 0x1048,
            FrequencyOffsetEstimate_Set                                     = 0x104C,
            AutomaticFrequencyControlAdjustmentRx_Set                       = 0x1050,
            AutomaticFrequencyControldAjustmentTx_Set                       = 0x1054,
            AnalogMixerControl_Set                                          = 0x1058,
            Control0_Set                                                    = 0x105C,
            Control1_Set                                                    = 0x1060,
            Control2_Set                                                    = 0x1064,
            Control3_Set                                                    = 0x1068,
            Control4_Set                                                    = 0x106C,
            Control5_Set                                                    = 0x1070,
            Control6_Set                                                    = 0x1074,
            TxBaudrate_Set                                                  = 0x1078,
            RxBaudrate_Set                                                  = 0x107C,
            ChannelFilter_Set                                               = 0x1080,
            Preamble_Set                                                    = 0x1084,
            Timing_Set                                                      = 0x1088,
            DirectSequenceSpreadSpectrumSymbol0_Set                         = 0x108C,
            ModulationIndex_Set                                             = 0x1090,
            AutomaticFrequencyControl_Set                                   = 0x1094,
            AutomaticFrequencyControlAdjustmentLimit_Set                    = 0x1098,
            ShapingCoefficients0_Set                                        = 0x109C,
            ShapingCoefficients1_Set                                        = 0x10A0,
            ShapingCoefficients2_Set                                        = 0x10A4,
            ShapingCoefficients3_Set                                        = 0x10A8,
            ShapingCoefficients4_Set                                        = 0x10AC,
            ShapingCoefficients5_Set                                        = 0x10B0,
            ShapingCoefficients6_Set                                        = 0x10B4,
            ShapingCoefficients7_Set                                        = 0x10B8,
            ShapingCoefficients8_Set                                        = 0x10BC,
            ShapingCoefficients9_Set                                        = 0x10C0,
            ShapingCoefficients10_Set                                       = 0x10C4,
            ShapingCoefficients11_Set                                       = 0x10C8,
            ShapingCoefficients12_Set                                       = 0x10CC,
            ShapingCoefficients13_Set                                       = 0x10D0,
            ShapingCoefficients14_Set                                       = 0x10D4,
            ShapingCoefficients15_Set                                       = 0x10D8,
            RampingControl_Set                                              = 0x10E0,
            RampingLevels_Set                                               = 0x10E4,
            DirectCurrentOffsetCompensationFilterSettings_Set               = 0x1118,
            DirectCurrentOffsetCompensationFilterInitialization_Set         = 0x111C,
            DirectCurrentOffsetEstimatedValue_Set                           = 0x1120,
            SampleRateConverterRatioValuesAndChannelFilter_Set              = 0x1124,
            InternalAutomaticFrequencyControl_Set                           = 0x1128,
            DigitalMixerControl_Set                                         = 0x113C,
            BaudrateEstimate_Set                                            = 0x1154,
            AutomaticClockGating_Set                                        = 0x1158,
            AutomaticClockGatingClockStop_Set                               = 0x115C,
            PhaseOffsetEstimate_Set                                         = 0x1160,
            DirectModeControl_Set                                           = 0x1164,
            BleLongRange_Set                                                = 0x1168,
            BleLongRangeSet1_Set                                            = 0x116C,
            BleLongRangeSet2_Set                                            = 0x1170,
            BleLongRangeSet3_Set                                            = 0x1174,
            BleLongRangeSet4_Set                                            = 0x1178,
            BleLongRangeSet5_Set                                            = 0x117C,
            BleLongRangeSet6_Set                                            = 0x1180,
            BleLongRangeFrameControllerInterface_Set                        = 0x1184,
            CoherentDemodulatorSignals0_Set                                 = 0x1188,
            CoherentDemodulatorSignals1_Set                                 = 0x118C,
            CoherentDemodulatorSignals2_Set                                 = 0x1190,
            CoherentDemodulatorSignals3_Set                                 = 0x1194,
            Command_Set                                                     = 0x1198,
            SyncWordProperties_Set                                          = 0x11A4,
            DigitalGainControl_Set                                          = 0x11A8,
            PeripheralReflexSystemControl_Set                               = 0x11AC,
            EarlyTimeStampControl_Set                                       = 0x11B8,
            EarlyTimeStampTiming_Set                                        = 0x11BC,
            AntennaSwitchControl_Set                                        = 0x11C0,
            AntennaSwitchControl1_Set                                       = 0x11C4,
            AntennaSwitchStart_Set                                          = 0x11C8,
            AntennaSwitchEnd_Set                                            = 0x11CC,
            ConfigureAntennaPattern_Set                                     = 0x11DC,
            ConcurrentMode_Set                                              = 0x11E0,
            ChannelFilterCoeSet0Group0_Set                                  = 0x11E4,
            ChannelFilterCoeSet0Group1_Set                                  = 0x11E8,
            ChannelFilterCoeSet0Group2_Set                                  = 0x11EC,
            ChannelFilterCoeSet0Group3_Set                                  = 0x11F0,
            ChannelFilterCoeSet0Group4_Set                                  = 0x11F4,
            ChannelFilterCoeSet0Group5_Set                                  = 0x11F8,
            ChannelFilterCoeSet0Group6_Set                                  = 0x11FC,
            ChannelFilterCoeSet1Group0_Set                                  = 0x1200,
            ChannelFilterCoeSet1Group1_Set                                  = 0x1204,
            ChannelFilterCoeSet1Group2_Set                                  = 0x1208,
            ChannelFilterCoeSet1Group3_Set                                  = 0x120C,
            ChannelFilterCoeSet1Group4_Set                                  = 0x1210,
            ChannelFilterCoeSet1Group5_Set                                  = 0x1214,
            ChannelFilterCoeSet1Group6_Set                                  = 0x1218,
            ChannelFilterControl_Set                                        = 0x121C,
            ChannelFilterLatencyControl_Set                                 = 0x1220,
            FrameSchTimeoutLength_Set                                       = 0x1224,
            PreambleFilterCoefficients_Set                                  = 0x1228,
            CollisionRestartControl_Set                                     = 0x122C,
            PreambleSenseMode_Set                                           = 0x1230,
            PreambleSenseModeExtended_Set                                   = 0x1234,
            SignalQualityIndicator_Set                                      = 0x1238,
            AntennaDiversityModeControl_Set                                 = 0x123C,
            PhaseDemodulatorFwMode_Set                                      = 0x1240,
            PhaseDemodulatorAntennaDiversity_Set                            = 0x1244,
            PhaseDemodulatorAntennaDiversityDecision_Set                    = 0x1248,
            PhaseDemodulatorControl_Set                                     = 0x124C,
            SignalIdentifierCorrelator_Set                                  = 0x1250,
            SignalIdentifierControl0_Set                                    = 0x1254,
            SignalIdentifierControl1_Set                                    = 0x1258,
            SignalIdentifierStatus_Set                                      = 0x125C,
            ConfigureAntennaPatternExtended_Set                             = 0x1260,
            SignalIdentifierControl2_Set                                    = 0x1264,
            ChannelFilterSwitchTime_Set                                     = 0x1268,
            FirmwareHoppingControl_Set                                      = 0x1274,
            FastSwitchInterruptFlags_Set                                    = 0x1278,
            FastSwitchInterruptEnable_Set                                   = 0x127C,
            FastSwitchSpare_Set                                             = 0x1280,
            DirectSequenceSpreadSpectrumSymbol0ForSi_Set                    = 0x1284,
            Decimal1Log2Times4_Set                                          = 0x1288,
            EcaTraceControl_Set                                             = 0x128C,
            IrCalibrationControl_Set                                        = 0x1290,
            IrCalCoefficientValues_Set                                      = 0x1294,
            IrCalCoefficientWrPerAntenna_Set                                = 0x1298,
            AdControl1_Set                                                  = 0x12A0,
            AdControl2_Set                                                  = 0x12A4,
            AdQual0_Set                                                     = 0x12A8,
            AdQual1_Set                                                     = 0x12AC,
            AdQual2_Set                                                     = 0x12B0,
            AdQual3_Set                                                     = 0x12B4,
            AdQual4_Set                                                     = 0x12B8,
            AdQual5_Set                                                     = 0x12BC,
            AdQual6_Set                                                     = 0x12C0,
            AdQual7_Set                                                     = 0x12C4,
            AdQual8_Set                                                     = 0x12C8,
            AdQual9_Set                                                     = 0x12CC,
            AdQual10_Set                                                    = 0x12D0,
            AdFsm0_Set                                                      = 0x12D4,
            AdFsm1_Set                                                      = 0x12D8,
            AdFsm2_Set                                                      = 0x12DC,
            AdFsm3_Set                                                      = 0x12E0,
            AdFsm4_Set                                                      = 0x12E4,
            AdFsm5_Set                                                      = 0x12E8,
            AdFsm6_Set                                                      = 0x12EC,
            AdFsm7_Set                                                      = 0x12F0,
            AdFsm8_Set                                                      = 0x12F4,
            AdFsm9_Set                                                      = 0x12F8,
            AdFsm10_Set                                                     = 0x12FC,
            AdFsm11_Set                                                     = 0x1300,
            AdFsm12_Set                                                     = 0x1304,
            AdFsm13_Set                                                     = 0x1308,
            AdFsm14_Set                                                     = 0x130C,
            AdFsm15_Set                                                     = 0x1310,
            AdFsm16_Set                                                     = 0x1314,
            AdFsm17_Set                                                     = 0x1318,
            AdFsm18_Set                                                     = 0x131C,
            AdFsm19_Set                                                     = 0x1320,
            AdFsm20_Set                                                     = 0x1324,
            AdFsm21_Set                                                     = 0x1328,
            AdFsm22_Set                                                     = 0x132C,
            AdFsm23_Set                                                     = 0x1330,
            AdFsm24_Set                                                     = 0x1334,
            AdFsm25_Set                                                     = 0x1338,
            AdFsm26_Set                                                     = 0x133C,
            AdFsm27_Set                                                     = 0x1340,
            AdFsm28_Set                                                     = 0x1344,
            AdFsm29_Set                                                     = 0x1348,
            AdFsm30_Set                                                     = 0x134C,
            AdPc1_Set                                                       = 0x1350,
            AdPc2_Set                                                       = 0x1354,
            AdPc3_Set                                                       = 0x1358,
            AdPc4_Set                                                       = 0x135C,
            AdPc5_Set                                                       = 0x1360,
            AdPc6_Set                                                       = 0x1364,
            AdPc7_Set                                                       = 0x1368,
            AdPc8_Set                                                       = 0x136C,
            AdPc9_Set                                                       = 0x1370,
            AdPc10_Set                                                      = 0x1374,
            HadmControl0_Set                                                = 0x13C0,
            HadmControl1_Set                                                = 0x13C4,
            HadmStatus0_Set                                                 = 0x13C8,
            HadmStatus1_Set                                                 = 0x13CC,
            HadmStatus2_Set                                                 = 0x13D0,
            HadmStatus3_Set                                                 = 0x13D4,
            HadmStatus4_Set                                                 = 0x13D8,
            HadmStatus5_Set                                                 = 0x13DC,
            HadmStatus6_Set                                                 = 0x13E0,
            HadmControl2_Set                                                = 0x13E4,
            HadmControl3_Set                                                = 0x13E8,
            HadmControl4_Set                                                = 0x13EC,
            HadmStatus7_Set                                                 = 0x13F0,
            HadmControl5_Set                                                = 0x13F4,
            EhDsssControl_Set                                               = 0x1414,
            EhDsssConfig0_Set                                               = 0x1418,
            EhDsssConfig1_Set                                               = 0x141C,
            EhDsssConfig2_Set                                               = 0x1420,
            EhDsssConfig3_Set                                               = 0x1424,
            Symbol2Chip0_Set                                                = 0x1428,
            Symbol2Chip8_Set                                                = 0x1444,
            Spare_Set                                                       = 0x1470,
            SyncWord0_Set                                                   = 0x1480,
            SyncWord1_Set                                                   = 0x1484,
            SyncWord2_Set                                                   = 0x1488,
            SyncWord3_Set                                                   = 0x148C,
            SyncWordControl_Set                                             = 0x1490,
            TxControl_Set                                                   = 0x14A0,
            TxDacValues_Set                                                 = 0x14A4,
            TxCorrStatic_Set                                                = 0x14A8,
            TxCorrPte_Set                                                   = 0x14AC,
            ViterbiDemodulator_Set                                          = 0x1500,
            ViterbiDemodulatorCorrelationConfiguration0_Set                 = 0x1504,
            ViterbiDemodulatorCorrelationConfiguration1_Set                 = 0x1508,
            ViterbiDemodulatorTrackingLoop_Set                              = 0x150C,
            ViterbiDemodulatorBleTimestampControl_Set                       = 0x1510,
            RealTimeCostFunctionEngineControl_Set                           = 0x1514,
            TrecsPreamblePattern_Set                                        = 0x1518,
            TrecsPreambleDetectionControl_Set                               = 0x151C,
            TrecsConfiguration_Set                                          = 0x1520,
            TrecsDualInitialTimingSearch_Set                                = 0x1528,
            ExpectedPatternForDualTim_Set                                   = 0x152C,
            AuxiliaryAdcIfControl_Set                                       = 0x1540,
            AuxiliaryAdcDataOutput_Set                                      = 0x1544,
            Commands_Set                                                    = 0x1580,
            AmpAverageControl_Set                                           = 0x1584,
            Result_Set                                                      = 0x1588,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            InterruptFlags_Clr                                              = 0x2008,
            InterruptFlags2_Clr                                             = 0x200C,
            InterruptEnable_Clr                                             = 0x2010,
            InterruptEnable2_Clr                                            = 0x2014,
            SequencerInterruptFlags_Clr                                     = 0x2018,
            SequencerInterruptFlags2_Clr                                    = 0x201C,
            SequencerInterruptEnable_Clr                                    = 0x2020,
            SequencerInterruptEnable2_Clr                                   = 0x2024,
            Status_Clr                                                      = 0x2028,
            Status2_Clr                                                     = 0x202C,
            Status3_Clr                                                     = 0x2030,
            Status4_Clr                                                     = 0x2034,
            Status5_Clr                                                     = 0x2038,
            Status6_Clr                                                     = 0x203C,
            Status7_Clr                                                     = 0x2040,
            TimingDetectionStatus_Clr                                       = 0x2044,
            DemodulatorFSMStatus_Clr                                        = 0x2048,
            FrequencyOffsetEstimate_Clr                                     = 0x204C,
            AutomaticFrequencyControlAdjustmentRx_Clr                       = 0x2050,
            AutomaticFrequencyControldAjustmentTx_Clr                       = 0x2054,
            AnalogMixerControl_Clr                                          = 0x2058,
            Control0_Clr                                                    = 0x205C,
            Control1_Clr                                                    = 0x2060,
            Control2_Clr                                                    = 0x2064,
            Control3_Clr                                                    = 0x2068,
            Control4_Clr                                                    = 0x206C,
            Control5_Clr                                                    = 0x2070,
            Control6_Clr                                                    = 0x2074,
            TxBaudrate_Clr                                                  = 0x2078,
            RxBaudrate_Clr                                                  = 0x207C,
            ChannelFilter_Clr                                               = 0x2080,
            Preamble_Clr                                                    = 0x2084,
            Timing_Clr                                                      = 0x2088,
            DirectSequenceSpreadSpectrumSymbol0_Clr                         = 0x208C,
            ModulationIndex_Clr                                             = 0x2090,
            AutomaticFrequencyControl_Clr                                   = 0x2094,
            AutomaticFrequencyControlAdjustmentLimit_Clr                    = 0x2098,
            ShapingCoefficients0_Clr                                        = 0x209C,
            ShapingCoefficients1_Clr                                        = 0x20A0,
            ShapingCoefficients2_Clr                                        = 0x20A4,
            ShapingCoefficients3_Clr                                        = 0x20A8,
            ShapingCoefficients4_Clr                                        = 0x20AC,
            ShapingCoefficients5_Clr                                        = 0x20B0,
            ShapingCoefficients6_Clr                                        = 0x20B4,
            ShapingCoefficients7_Clr                                        = 0x20B8,
            ShapingCoefficients8_Clr                                        = 0x20BC,
            ShapingCoefficients9_Clr                                        = 0x20C0,
            ShapingCoefficients10_Clr                                       = 0x20C4,
            ShapingCoefficients11_Clr                                       = 0x20C8,
            ShapingCoefficients12_Clr                                       = 0x20CC,
            ShapingCoefficients13_Clr                                       = 0x20D0,
            ShapingCoefficients14_Clr                                       = 0x20D4,
            ShapingCoefficients15_Clr                                       = 0x20D8,
            RampingControl_Clr                                              = 0x20E0,
            RampingLevels_Clr                                               = 0x20E4,
            DirectCurrentOffsetCompensationFilterSettings_Clr               = 0x2118,
            DirectCurrentOffsetCompensationFilterInitialization_Clr         = 0x211C,
            DirectCurrentOffsetEstimatedValue_Clr                           = 0x2120,
            SampleRateConverterRatioValuesAndChannelFilter_Clr              = 0x2124,
            InternalAutomaticFrequencyControl_Clr                           = 0x2128,
            DigitalMixerControl_Clr                                         = 0x213C,
            BaudrateEstimate_Clr                                            = 0x2154,
            AutomaticClockGating_Clr                                        = 0x2158,
            AutomaticClockGatingClockStop_Clr                               = 0x215C,
            PhaseOffsetEstimate_Clr                                         = 0x2160,
            DirectModeControl_Clr                                           = 0x2164,
            BleLongRange_Clr                                                = 0x2168,
            BleLongRangeSet1_Clr                                            = 0x216C,
            BleLongRangeSet2_Clr                                            = 0x2170,
            BleLongRangeSet3_Clr                                            = 0x2174,
            BleLongRangeSet4_Clr                                            = 0x2178,
            BleLongRangeSet5_Clr                                            = 0x217C,
            BleLongRangeSet6_Clr                                            = 0x2180,
            BleLongRangeFrameControllerInterface_Clr                        = 0x2184,
            CoherentDemodulatorSignals0_Clr                                 = 0x2188,
            CoherentDemodulatorSignals1_Clr                                 = 0x218C,
            CoherentDemodulatorSignals2_Clr                                 = 0x2190,
            CoherentDemodulatorSignals3_Clr                                 = 0x2194,
            Command_Clr                                                     = 0x2198,
            SyncWordProperties_Clr                                          = 0x21A4,
            DigitalGainControl_Clr                                          = 0x21A8,
            PeripheralReflexSystemControl_Clr                               = 0x21AC,
            EarlyTimeStampControl_Clr                                       = 0x21B8,
            EarlyTimeStampTiming_Clr                                        = 0x21BC,
            AntennaSwitchControl_Clr                                        = 0x21C0,
            AntennaSwitchControl1_Clr                                       = 0x21C4,
            AntennaSwitchStart_Clr                                          = 0x21C8,
            AntennaSwitchEnd_Clr                                            = 0x21CC,
            ConfigureAntennaPattern_Clr                                     = 0x21DC,
            ConcurrentMode_Clr                                              = 0x21E0,
            ChannelFilterCoeSet0Group0_Clr                                  = 0x21E4,
            ChannelFilterCoeSet0Group1_Clr                                  = 0x21E8,
            ChannelFilterCoeSet0Group2_Clr                                  = 0x21EC,
            ChannelFilterCoeSet0Group3_Clr                                  = 0x21F0,
            ChannelFilterCoeSet0Group4_Clr                                  = 0x21F4,
            ChannelFilterCoeSet0Group5_Clr                                  = 0x21F8,
            ChannelFilterCoeSet0Group6_Clr                                  = 0x21FC,
            ChannelFilterCoeSet1Group0_Clr                                  = 0x2200,
            ChannelFilterCoeSet1Group1_Clr                                  = 0x2204,
            ChannelFilterCoeSet1Group2_Clr                                  = 0x2208,
            ChannelFilterCoeSet1Group3_Clr                                  = 0x220C,
            ChannelFilterCoeSet1Group4_Clr                                  = 0x2210,
            ChannelFilterCoeSet1Group5_Clr                                  = 0x2214,
            ChannelFilterCoeSet1Group6_Clr                                  = 0x2218,
            ChannelFilterControl_Clr                                        = 0x221C,
            ChannelFilterLatencyControl_Clr                                 = 0x2220,
            FrameSchTimeoutLength_Clr                                       = 0x2224,
            PreambleFilterCoefficients_Clr                                  = 0x2228,
            CollisionRestartControl_Clr                                     = 0x222C,
            PreambleSenseMode_Clr                                           = 0x2230,
            PreambleSenseModeExtended_Clr                                   = 0x2234,
            SignalQualityIndicator_Clr                                      = 0x2238,
            AntennaDiversityModeControl_Clr                                 = 0x223C,
            PhaseDemodulatorFwMode_Clr                                      = 0x2240,
            PhaseDemodulatorAntennaDiversity_Clr                            = 0x2244,
            PhaseDemodulatorAntennaDiversityDecision_Clr                    = 0x2248,
            PhaseDemodulatorControl_Clr                                     = 0x224C,
            SignalIdentifierCorrelator_Clr                                  = 0x2250,
            SignalIdentifierControl0_Clr                                    = 0x2254,
            SignalIdentifierControl1_Clr                                    = 0x2258,
            SignalIdentifierStatus_Clr                                      = 0x225C,
            ConfigureAntennaPatternExtended_Clr                             = 0x2260,
            SignalIdentifierControl2_Clr                                    = 0x2264,
            ChannelFilterSwitchTime_Clr                                     = 0x2268,
            FirmwareHoppingControl_Clr                                      = 0x2274,
            FastSwitchInterruptFlags_Clr                                    = 0x2278,
            FastSwitchInterruptEnable_Clr                                   = 0x227C,
            FastSwitchSpare_Clr                                             = 0x2280,
            DirectSequenceSpreadSpectrumSymbol0ForSi_Clr                    = 0x2284,
            Decimal1Log2Times4_Clr                                          = 0x2288,
            EcaTraceControl_Clr                                             = 0x228C,
            IrCalibrationControl_Clr                                        = 0x2290,
            IrCalCoefficientValues_Clr                                      = 0x2294,
            IrCalCoefficientWrPerAntenna_Clr                                = 0x2298,
            AdControl1_Clr                                                  = 0x22A0,
            AdControl2_Clr                                                  = 0x22A4,
            AdQual0_Clr                                                     = 0x22A8,
            AdQual1_Clr                                                     = 0x22AC,
            AdQual2_Clr                                                     = 0x22B0,
            AdQual3_Clr                                                     = 0x22B4,
            AdQual4_Clr                                                     = 0x22B8,
            AdQual5_Clr                                                     = 0x22BC,
            AdQual6_Clr                                                     = 0x22C0,
            AdQual7_Clr                                                     = 0x22C4,
            AdQual8_Clr                                                     = 0x22C8,
            AdQual9_Clr                                                     = 0x22CC,
            AdQual10_Clr                                                    = 0x22D0,
            AdFsm0_Clr                                                      = 0x22D4,
            AdFsm1_Clr                                                      = 0x22D8,
            AdFsm2_Clr                                                      = 0x22DC,
            AdFsm3_Clr                                                      = 0x22E0,
            AdFsm4_Clr                                                      = 0x22E4,
            AdFsm5_Clr                                                      = 0x22E8,
            AdFsm6_Clr                                                      = 0x22EC,
            AdFsm7_Clr                                                      = 0x22F0,
            AdFsm8_Clr                                                      = 0x22F4,
            AdFsm9_Clr                                                      = 0x22F8,
            AdFsm10_Clr                                                     = 0x22FC,
            AdFsm11_Clr                                                     = 0x2300,
            AdFsm12_Clr                                                     = 0x2304,
            AdFsm13_Clr                                                     = 0x2308,
            AdFsm14_Clr                                                     = 0x230C,
            AdFsm15_Clr                                                     = 0x2310,
            AdFsm16_Clr                                                     = 0x2314,
            AdFsm17_Clr                                                     = 0x2318,
            AdFsm18_Clr                                                     = 0x231C,
            AdFsm19_Clr                                                     = 0x2320,
            AdFsm20_Clr                                                     = 0x2324,
            AdFsm21_Clr                                                     = 0x2328,
            AdFsm22_Clr                                                     = 0x232C,
            AdFsm23_Clr                                                     = 0x2330,
            AdFsm24_Clr                                                     = 0x2334,
            AdFsm25_Clr                                                     = 0x2338,
            AdFsm26_Clr                                                     = 0x233C,
            AdFsm27_Clr                                                     = 0x2340,
            AdFsm28_Clr                                                     = 0x2344,
            AdFsm29_Clr                                                     = 0x2348,
            AdFsm30_Clr                                                     = 0x234C,
            AdPc1_Clr                                                       = 0x2350,
            AdPc2_Clr                                                       = 0x2354,
            AdPc3_Clr                                                       = 0x2358,
            AdPc4_Clr                                                       = 0x235C,
            AdPc5_Clr                                                       = 0x2360,
            AdPc6_Clr                                                       = 0x2364,
            AdPc7_Clr                                                       = 0x2368,
            AdPc8_Clr                                                       = 0x236C,
            AdPc9_Clr                                                       = 0x2370,
            AdPc10_Clr                                                      = 0x2374,
            HadmControl0_Clr                                                = 0x23C0,
            HadmControl1_Clr                                                = 0x23C4,
            HadmStatus0_Clr                                                 = 0x23C8,
            HadmStatus1_Clr                                                 = 0x23CC,
            HadmStatus2_Clr                                                 = 0x23D0,
            HadmStatus3_Clr                                                 = 0x23D4,
            HadmStatus4_Clr                                                 = 0x23D8,
            HadmStatus5_Clr                                                 = 0x23DC,
            HadmStatus6_Clr                                                 = 0x23E0,
            HadmControl2_Clr                                                = 0x23E4,
            HadmControl3_Clr                                                = 0x23E8,
            HadmControl4_Clr                                                = 0x23EC,
            HadmStatus7_Clr                                                 = 0x23F0,
            HadmControl5_Clr                                                = 0x23F4,
            EhDsssControl_Clr                                               = 0x2414,
            EhDsssConfig0_Clr                                               = 0x2418,
            EhDsssConfig1_Clr                                               = 0x241C,
            EhDsssConfig2_Clr                                               = 0x2420,
            EhDsssConfig3_Clr                                               = 0x2424,
            Symbol2Chip0_Clr                                                = 0x2428,
            Symbol2Chip8_Clr                                                = 0x2444,
            Spare_Clr                                                       = 0x2470,
            SyncWord0_Clr                                                   = 0x2480,
            SyncWord1_Clr                                                   = 0x2484,
            SyncWord2_Clr                                                   = 0x2488,
            SyncWord3_Clr                                                   = 0x248C,
            SyncWordControl_Clr                                             = 0x2490,
            TxControl_Clr                                                   = 0x24A0,
            TxDacValues_Clr                                                 = 0x24A4,
            TxCorrStatic_Clr                                                = 0x24A8,
            TxCorrPte_Clr                                                   = 0x24AC,
            ViterbiDemodulator_Clr                                          = 0x2500,
            ViterbiDemodulatorCorrelationConfiguration0_Clr                 = 0x2504,
            ViterbiDemodulatorCorrelationConfiguration1_Clr                 = 0x2508,
            ViterbiDemodulatorTrackingLoop_Clr                              = 0x250C,
            ViterbiDemodulatorBleTimestampControl_Clr                       = 0x2510,
            RealTimeCostFunctionEngineControl_Clr                           = 0x2514,
            TrecsPreamblePattern_Clr                                        = 0x2518,
            TrecsPreambleDetectionControl_Clr                               = 0x251C,
            TrecsConfiguration_Clr                                          = 0x2520,
            TrecsDualInitialTimingSearch_Clr                                = 0x2528,
            ExpectedPatternForDualTim_Clr                                   = 0x252C,
            AuxiliaryAdcIfControl_Clr                                       = 0x2540,
            AuxiliaryAdcDataOutput_Clr                                      = 0x2544,
            Commands_Clr                                                    = 0x2580,
            AmpAverageControl_Clr                                           = 0x2584,
            Result_Clr                                                      = 0x2588,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            InterruptFlags_Tgl                                              = 0x3008,
            InterruptFlags2_Tgl                                             = 0x300C,
            InterruptEnable_Tgl                                             = 0x3010,
            InterruptEnable2_Tgl                                            = 0x3014,
            SequencerInterruptFlags_Tgl                                     = 0x3018,
            SequencerInterruptFlags2_Tgl                                    = 0x301C,
            SequencerInterruptEnable_Tgl                                    = 0x3020,
            SequencerInterruptEnable2_Tgl                                   = 0x3024,
            Status_Tgl                                                      = 0x3028,
            Status2_Tgl                                                     = 0x302C,
            Status3_Tgl                                                     = 0x3030,
            Status4_Tgl                                                     = 0x3034,
            Status5_Tgl                                                     = 0x3038,
            Status6_Tgl                                                     = 0x303C,
            Status7_Tgl                                                     = 0x3040,
            TimingDetectionStatus_Tgl                                       = 0x3044,
            DemodulatorFSMStatus_Tgl                                        = 0x3048,
            FrequencyOffsetEstimate_Tgl                                     = 0x304C,
            AutomaticFrequencyControlAdjustmentRx_Tgl                       = 0x3050,
            AutomaticFrequencyControldAjustmentTx_Tgl                       = 0x3054,
            AnalogMixerControl_Tgl                                          = 0x3058,
            Control0_Tgl                                                    = 0x305C,
            Control1_Tgl                                                    = 0x3060,
            Control2_Tgl                                                    = 0x3064,
            Control3_Tgl                                                    = 0x3068,
            Control4_Tgl                                                    = 0x306C,
            Control5_Tgl                                                    = 0x3070,
            Control6_Tgl                                                    = 0x3074,
            TxBaudrate_Tgl                                                  = 0x3078,
            RxBaudrate_Tgl                                                  = 0x307C,
            ChannelFilter_Tgl                                               = 0x3080,
            Preamble_Tgl                                                    = 0x3084,
            Timing_Tgl                                                      = 0x3088,
            DirectSequenceSpreadSpectrumSymbol0_Tgl                         = 0x308C,
            ModulationIndex_Tgl                                             = 0x3090,
            AutomaticFrequencyControl_Tgl                                   = 0x3094,
            AutomaticFrequencyControlAdjustmentLimit_Tgl                    = 0x3098,
            ShapingCoefficients0_Tgl                                        = 0x309C,
            ShapingCoefficients1_Tgl                                        = 0x30A0,
            ShapingCoefficients2_Tgl                                        = 0x30A4,
            ShapingCoefficients3_Tgl                                        = 0x30A8,
            ShapingCoefficients4_Tgl                                        = 0x30AC,
            ShapingCoefficients5_Tgl                                        = 0x30B0,
            ShapingCoefficients6_Tgl                                        = 0x30B4,
            ShapingCoefficients7_Tgl                                        = 0x30B8,
            ShapingCoefficients8_Tgl                                        = 0x30BC,
            ShapingCoefficients9_Tgl                                        = 0x30C0,
            ShapingCoefficients10_Tgl                                       = 0x30C4,
            ShapingCoefficients11_Tgl                                       = 0x30C8,
            ShapingCoefficients12_Tgl                                       = 0x30CC,
            ShapingCoefficients13_Tgl                                       = 0x30D0,
            ShapingCoefficients14_Tgl                                       = 0x30D4,
            ShapingCoefficients15_Tgl                                       = 0x30D8,
            RampingControl_Tgl                                              = 0x30E0,
            RampingLevels_Tgl                                               = 0x30E4,
            DirectCurrentOffsetCompensationFilterSettings_Tgl               = 0x3118,
            DirectCurrentOffsetCompensationFilterInitialization_Tgl         = 0x311C,
            DirectCurrentOffsetEstimatedValue_Tgl                           = 0x3120,
            SampleRateConverterRatioValuesAndChannelFilter_Tgl              = 0x3124,
            InternalAutomaticFrequencyControl_Tgl                           = 0x3128,
            DigitalMixerControl_Tgl                                         = 0x313C,
            BaudrateEstimate_Tgl                                            = 0x3154,
            AutomaticClockGating_Tgl                                        = 0x3158,
            AutomaticClockGatingClockStop_Tgl                               = 0x315C,
            PhaseOffsetEstimate_Tgl                                         = 0x3160,
            DirectModeControl_Tgl                                           = 0x3164,
            BleLongRange_Tgl                                                = 0x3168,
            BleLongRangeSet1_Tgl                                            = 0x316C,
            BleLongRangeSet2_Tgl                                            = 0x3170,
            BleLongRangeSet3_Tgl                                            = 0x3174,
            BleLongRangeSet4_Tgl                                            = 0x3178,
            BleLongRangeSet5_Tgl                                            = 0x317C,
            BleLongRangeSet6_Tgl                                            = 0x3180,
            BleLongRangeFrameControllerInterface_Tgl                        = 0x3184,
            CoherentDemodulatorSignals0_Tgl                                 = 0x3188,
            CoherentDemodulatorSignals1_Tgl                                 = 0x318C,
            CoherentDemodulatorSignals2_Tgl                                 = 0x3190,
            CoherentDemodulatorSignals3_Tgl                                 = 0x3194,
            Command_Tgl                                                     = 0x3198,
            SyncWordProperties_Tgl                                          = 0x31A4,
            DigitalGainControl_Tgl                                          = 0x31A8,
            PeripheralReflexSystemControl_Tgl                               = 0x31AC,
            EarlyTimeStampControl_Tgl                                       = 0x31B8,
            EarlyTimeStampTiming_Tgl                                        = 0x31BC,
            AntennaSwitchControl_Tgl                                        = 0x31C0,
            AntennaSwitchControl1_Tgl                                       = 0x31C4,
            AntennaSwitchStart_Tgl                                          = 0x31C8,
            AntennaSwitchEnd_Tgl                                            = 0x31CC,
            ConfigureAntennaPattern_Tgl                                     = 0x31DC,
            ConcurrentMode_Tgl                                              = 0x31E0,
            ChannelFilterCoeSet0Group0_Tgl                                  = 0x31E4,
            ChannelFilterCoeSet0Group1_Tgl                                  = 0x31E8,
            ChannelFilterCoeSet0Group2_Tgl                                  = 0x31EC,
            ChannelFilterCoeSet0Group3_Tgl                                  = 0x31F0,
            ChannelFilterCoeSet0Group4_Tgl                                  = 0x31F4,
            ChannelFilterCoeSet0Group5_Tgl                                  = 0x31F8,
            ChannelFilterCoeSet0Group6_Tgl                                  = 0x31FC,
            ChannelFilterCoeSet1Group0_Tgl                                  = 0x3200,
            ChannelFilterCoeSet1Group1_Tgl                                  = 0x3204,
            ChannelFilterCoeSet1Group2_Tgl                                  = 0x3208,
            ChannelFilterCoeSet1Group3_Tgl                                  = 0x320C,
            ChannelFilterCoeSet1Group4_Tgl                                  = 0x3210,
            ChannelFilterCoeSet1Group5_Tgl                                  = 0x3214,
            ChannelFilterCoeSet1Group6_Tgl                                  = 0x3218,
            ChannelFilterControl_Tgl                                        = 0x321C,
            ChannelFilterLatencyControl_Tgl                                 = 0x3220,
            FrameSchTimeoutLength_Tgl                                       = 0x3224,
            PreambleFilterCoefficients_Tgl                                  = 0x3228,
            CollisionRestartControl_Tgl                                     = 0x322C,
            PreambleSenseMode_Tgl                                           = 0x3230,
            PreambleSenseModeExtended_Tgl                                   = 0x3234,
            SignalQualityIndicator_Tgl                                      = 0x3238,
            AntennaDiversityModeControl_Tgl                                 = 0x323C,
            PhaseDemodulatorFwMode_Tgl                                      = 0x3240,
            PhaseDemodulatorAntennaDiversity_Tgl                            = 0x3244,
            PhaseDemodulatorAntennaDiversityDecision_Tgl                    = 0x3248,
            PhaseDemodulatorControl_Tgl                                     = 0x324C,
            SignalIdentifierCorrelator_Tgl                                  = 0x3250,
            SignalIdentifierControl0_Tgl                                    = 0x3254,
            SignalIdentifierControl1_Tgl                                    = 0x3258,
            SignalIdentifierStatus_Tgl                                      = 0x325C,
            ConfigureAntennaPatternExtended_Tgl                             = 0x3260,
            SignalIdentifierControl2_Tgl                                    = 0x3264,
            ChannelFilterSwitchTime_Tgl                                     = 0x3268,
            FirmwareHoppingControl_Tgl                                      = 0x3274,
            FastSwitchInterruptFlags_Tgl                                    = 0x3278,
            FastSwitchInterruptEnable_Tgl                                   = 0x327C,
            FastSwitchSpare_Tgl                                             = 0x3280,
            DirectSequenceSpreadSpectrumSymbol0ForSi_Tgl                    = 0x3284,
            Decimal1Log2Times4_Tgl                                          = 0x3288,
            EcaTraceControl_Tgl                                             = 0x328C,
            IrCalibrationControl_Tgl                                        = 0x3290,
            IrCalCoefficientValues_Tgl                                      = 0x3294,
            IrCalCoefficientWrPerAntenna_Tgl                                = 0x3298,
            AdControl1_Tgl                                                  = 0x32A0,
            AdControl2_Tgl                                                  = 0x32A4,
            AdQual0_Tgl                                                     = 0x32A8,
            AdQual1_Tgl                                                     = 0x32AC,
            AdQual2_Tgl                                                     = 0x32B0,
            AdQual3_Tgl                                                     = 0x32B4,
            AdQual4_Tgl                                                     = 0x32B8,
            AdQual5_Tgl                                                     = 0x32BC,
            AdQual6_Tgl                                                     = 0x32C0,
            AdQual7_Tgl                                                     = 0x32C4,
            AdQual8_Tgl                                                     = 0x32C8,
            AdQual9_Tgl                                                     = 0x32CC,
            AdQual10_Tgl                                                    = 0x32D0,
            AdFsm0_Tgl                                                      = 0x32D4,
            AdFsm1_Tgl                                                      = 0x32D8,
            AdFsm2_Tgl                                                      = 0x32DC,
            AdFsm3_Tgl                                                      = 0x32E0,
            AdFsm4_Tgl                                                      = 0x32E4,
            AdFsm5_Tgl                                                      = 0x32E8,
            AdFsm6_Tgl                                                      = 0x32EC,
            AdFsm7_Tgl                                                      = 0x32F0,
            AdFsm8_Tgl                                                      = 0x32F4,
            AdFsm9_Tgl                                                      = 0x32F8,
            AdFsm10_Tgl                                                     = 0x32FC,
            AdFsm11_Tgl                                                     = 0x3300,
            AdFsm12_Tgl                                                     = 0x3304,
            AdFsm13_Tgl                                                     = 0x3308,
            AdFsm14_Tgl                                                     = 0x330C,
            AdFsm15_Tgl                                                     = 0x3310,
            AdFsm16_Tgl                                                     = 0x3314,
            AdFsm17_Tgl                                                     = 0x3318,
            AdFsm18_Tgl                                                     = 0x331C,
            AdFsm19_Tgl                                                     = 0x3320,
            AdFsm20_Tgl                                                     = 0x3324,
            AdFsm21_Tgl                                                     = 0x3328,
            AdFsm22_Tgl                                                     = 0x332C,
            AdFsm23_Tgl                                                     = 0x3330,
            AdFsm24_Tgl                                                     = 0x3334,
            AdFsm25_Tgl                                                     = 0x3338,
            AdFsm26_Tgl                                                     = 0x333C,
            AdFsm27_Tgl                                                     = 0x3340,
            AdFsm28_Tgl                                                     = 0x3344,
            AdFsm29_Tgl                                                     = 0x3348,
            AdFsm30_Tgl                                                     = 0x334C,
            AdPc1_Tgl                                                       = 0x3350,
            AdPc2_Tgl                                                       = 0x3354,
            AdPc3_Tgl                                                       = 0x3358,
            AdPc4_Tgl                                                       = 0x335C,
            AdPc5_Tgl                                                       = 0x3360,
            AdPc6_Tgl                                                       = 0x3364,
            AdPc7_Tgl                                                       = 0x3368,
            AdPc8_Tgl                                                       = 0x336C,
            AdPc9_Tgl                                                       = 0x3370,
            AdPc10_Tgl                                                      = 0x3374,
            HadmControl0_Tgl                                                = 0x33C0,
            HadmControl1_Tgl                                                = 0x33C4,
            HadmStatus0_Tgl                                                 = 0x33C8,
            HadmStatus1_Tgl                                                 = 0x33CC,
            HadmStatus2_Tgl                                                 = 0x33D0,
            HadmStatus3_Tgl                                                 = 0x33D4,
            HadmStatus4_Tgl                                                 = 0x33D8,
            HadmStatus5_Tgl                                                 = 0x33DC,
            HadmStatus6_Tgl                                                 = 0x33E0,
            HadmControl2_Tgl                                                = 0x33E4,
            HadmControl3_Tgl                                                = 0x33E8,
            HadmControl4_Tgl                                                = 0x33EC,
            HadmStatus7_Tgl                                                 = 0x33F0,
            HadmControl5_Tgl                                                = 0x33F4,
            EhDsssControl_Tgl                                               = 0x3414,
            EhDsssConfig0_Tgl                                               = 0x3418,
            EhDsssConfig1_Tgl                                               = 0x341C,
            EhDsssConfig2_Tgl                                               = 0x3420,
            EhDsssConfig3_Tgl                                               = 0x3424,
            Symbol2Chip0_Tgl                                                = 0x3428,
            Symbol2Chip8_Tgl                                                = 0x3444,
            Spare_Tgl                                                       = 0x3470,
            SyncWord0_Tgl                                                   = 0x3480,
            SyncWord1_Tgl                                                   = 0x3484,
            SyncWord2_Tgl                                                   = 0x3488,
            SyncWord3_Tgl                                                   = 0x348C,
            SyncWordControl_Tgl                                             = 0x3490,
            TxControl_Tgl                                                   = 0x34A0,
            TxDacValues_Tgl                                                 = 0x34A4,
            TxCorrStatic_Tgl                                                = 0x34A8,
            TxCorrPte_Tgl                                                   = 0x34AC,
            ViterbiDemodulator_Tgl                                          = 0x3500,
            ViterbiDemodulatorCorrelationConfiguration0_Tgl                 = 0x3504,
            ViterbiDemodulatorCorrelationConfiguration1_Tgl                 = 0x3508,
            ViterbiDemodulatorTrackingLoop_Tgl                              = 0x350C,
            ViterbiDemodulatorBleTimestampControl_Tgl                       = 0x3510,
            RealTimeCostFunctionEngineControl_Tgl                           = 0x3514,
            TrecsPreamblePattern_Tgl                                        = 0x3518,
            TrecsPreambleDetectionControl_Tgl                               = 0x351C,
            TrecsConfiguration_Tgl                                          = 0x3520,
            TrecsDualInitialTimingSearch_Tgl                                = 0x3528,
            ExpectedPatternForDualTim_Tgl                                   = 0x352C,
            AuxiliaryAdcIfControl_Tgl                                       = 0x3540,
            AuxiliaryAdcDataOutput_Tgl                                      = 0x3544,
            Commands_Tgl                                                    = 0x3580,
            AmpAverageControl_Tgl                                           = 0x3584,
            Result_Tgl                                                      = 0x3588,
        }

        private enum AutomaticGainControlRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            Status0                                                         = 0x0008,
            Status1                                                         = 0x000C,
            Status2                                                         = 0x0010,
            ReceivedSignalStrengthIndicator                                 = 0x0018,
            FrameReceivedSignalStrengthIndicator                            = 0x001C,
            Control0                                                        = 0x0020,
            Control1                                                        = 0x0024,
            Control2                                                        = 0x0028,
            Control3                                                        = 0x002C,
            Control4                                                        = 0x0030,
            Control5                                                        = 0x0034,
            Control6                                                        = 0x0038,
            Control7                                                        = 0x003C,
            ReceivedSignalStrengthIndicatorStepThreshold                    = 0x0040,
            ManualGain                                                      = 0x0044,
            InterruptFlags                                                  = 0x0048,
            InterruptEnable                                                 = 0x004C,
            Command                                                         = 0x0050,
            RxGainRange                                                     = 0x0054,
            AutomaticGainControlPeriod0                                     = 0x0058,
            AutomaticGainControlPeriod1                                     = 0x005C,
            HiCounterRegion0                                                = 0x0060,
            HiCounterRegion1                                                = 0x0064,
            HiCounterRegion2                                                = 0x0068,
            GainStepsLimits0                                                = 0x006C,
            GainStepsLimits1                                                = 0x0070,
            PnRfAttenuationCodeGroup0                                       = 0x0074,
            PnRfAttenuationCodeGroup1                                       = 0x0078,
            PnRfAttenuationCodeGroup2                                       = 0x007C,
            PnRfAttenuationCodeGroup3                                       = 0x0080,
            PnRfAttenuationCodeGroup4                                       = 0x0084,
            PnRfAttenuationCodeGroup5                                       = 0x0088,
            PnRfAttenuationCodeGroup6                                       = 0x008C,
            PnRfAttenuationCodeGroup7                                       = 0x0090,
            PnRfAttenuationCodeGroupAlternate                               = 0x00A4,
            LnaMixSliceCodeGroup0                                           = 0x00A8,
            LnaMixSliceCodeGroup1                                           = 0x00AC,
            ProgrammableGainAmplifierGainCodeGroup0                         = 0x00B0,
            ProgrammableGainAmplifierGainCodeGroup1                         = 0x00B4,
            ListenBeforeTalkConfiguration                                   = 0x00B8,
            MirrorInterruptFlags                                            = 0x00BC,
            SequencerInterruptFlags                                         = 0x00C0,
            SequencerInterruptEnable                                        = 0x00C4,
            ReceivedSignalStrengthIndicatorAbsoluteThreshold                = 0x00C8,
            AntennaDiversity                                                = 0x00D0,
            DualRfpkdThreshold0                                             = 0x00D4,
            DualRfpkdThreshold1                                             = 0x00D8,
            Spare                                                           = 0x00DC,
            Flare                                                           = 0x00E0,
            StepDownOfdmSafeMode                                            = 0x00E4,
            ClearChannelAssessmentDebug                                     = 0x00E8,
            TiaCompensationCodeGroup0                                       = 0x00EC,
            TiaCompensationCodeGroup1                                       = 0x00F0,
            LnaMixSliceCodeGroup2                                           = 0x00F4,
            FastSwitchInterruptFlags                                        = 0x00F8,
            FastSwitchInterruptEnable                                       = 0x00FC,
            ClearChannelAssessmentSub                                       = 0x0100,
            Control8                                                        = 0x0104,
            Status3                                                         = 0x0108,
            PnRssiGain1                                                     = 0x010C,
            PnRssiGain2                                                     = 0x0110,
            PnRssiGain3                                                     = 0x0114,
            PnRssiGain4                                                     = 0x0118,
            LnaGain1                                                        = 0x011C,
            LnaGain2                                                        = 0x0120,
            LnaGain3                                                        = 0x0124,
            TiaCompensationCodeGroup2                                       = 0x0128,
            TiaCompensationCodeGroup3                                       = 0x012C,
            TiaCompensationCodeGroup4                                       = 0x0130,
            PgaRssiGain1                                                    = 0x0134,
            PgaRssiGain2                                                    = 0x0138,
            PgaRssiGain3                                                    = 0x013C,
            PgaRssiGain4                                                    = 0x0140,
            AdcGain0                                                        = 0x0144,
            CollisionDetectionControl                                       = 0x0160,
            CollisionDetectionThresholds                                    = 0x0164,
            CollisionDetectionStatus                                        = 0x0168,
            SettlingIndicatorControl                                        = 0x0180,
            SettlingIndicatorPeriod                                         = 0x0184,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            Status0_Set                                                     = 0x1008,
            Status1_Set                                                     = 0x100C,
            Status2_Set                                                     = 0x1010,
            ReceivedSignalStrengthIndicator_Set                             = 0x1018,
            FrameReceivedSignalStrengthIndicator_Set                        = 0x101C,
            Control0_Set                                                    = 0x1020,
            Control1_Set                                                    = 0x1024,
            Control2_Set                                                    = 0x1028,
            Control3_Set                                                    = 0x102C,
            Control4_Set                                                    = 0x1030,
            Control5_Set                                                    = 0x1034,
            Control6_Set                                                    = 0x1038,
            Control7_Set                                                    = 0x103C,
            ReceivedSignalStrengthIndicatorStepThreshold_Set                = 0x1040,
            ManualGain_Set                                                  = 0x1044,
            InterruptFlags_Set                                              = 0x1048,
            InterruptEnable_Set                                             = 0x104C,
            Command_Set                                                     = 0x1050,
            RxGainRange_Set                                                 = 0x1054,
            AutomaticGainControlPeriod0_Set                                 = 0x1058,
            AutomaticGainControlPeriod1_Set                                 = 0x105C,
            HiCounterRegion0_Set                                            = 0x1060,
            HiCounterRegion1_Set                                            = 0x1064,
            HiCounterRegion2_Set                                            = 0x1068,
            GainStepsLimits0_Set                                            = 0x106C,
            GainStepsLimits1_Set                                            = 0x1070,
            PnRfAttenuationCodeGroup0_Set                                   = 0x1074,
            PnRfAttenuationCodeGroup1_Set                                   = 0x1078,
            PnRfAttenuationCodeGroup2_Set                                   = 0x107C,
            PnRfAttenuationCodeGroup3_Set                                   = 0x1080,
            PnRfAttenuationCodeGroup4_Set                                   = 0x1084,
            PnRfAttenuationCodeGroup5_Set                                   = 0x1088,
            PnRfAttenuationCodeGroup6_Set                                   = 0x108C,
            PnRfAttenuationCodeGroup7_Set                                   = 0x1090,
            PnRfAttenuationCodeGroupAlternate_Set                           = 0x10A4,
            LnaMixSliceCodeGroup0_Set                                       = 0x10A8,
            LnaMixSliceCodeGroup1_Set                                       = 0x10AC,
            ProgrammableGainAmplifierGainCodeGroup0_Set                     = 0x10B0,
            ProgrammableGainAmplifierGainCodeGroup1_Set                     = 0x10B4,
            ListenBeforeTalkConfiguration_Set                               = 0x10B8,
            MirrorInterruptFlags_Set                                        = 0x10BC,
            SequencerInterruptFlags_Set                                     = 0x10C0,
            SequencerInterruptEnable_Set                                    = 0x10C4,
            ReceivedSignalStrengthIndicatorAbsoluteThreshold_Set            = 0x10C8,
            AntennaDiversity_Set                                            = 0x10D0,
            DualRfpkdThreshold0_Set                                         = 0x10D4,
            DualRfpkdThreshold1_Set                                         = 0x10D8,
            Spare_Set                                                       = 0x10DC,
            Flare_Set                                                       = 0x10E0,
            StepDownOfdmSafeMode_Set                                        = 0x10E4,
            ClearChannelAssessmentDebug_Set                                 = 0x10E8,
            TiaCompensationCodeGroup0_Set                                   = 0x10EC,
            TiaCompensationCodeGroup1_Set                                   = 0x10F0,
            LnaMixSliceCodeGroup2_Set                                       = 0x10F4,
            FastSwitchInterruptFlags_Set                                    = 0x10F8,
            FastSwitchInterruptEnable_Set                                   = 0x10FC,
            ClearChannelAssessmentSub_Set                                   = 0x1100,
            Control8_Set                                                    = 0x1104,
            Status3_Set                                                     = 0x1108,
            PnRssiGain1_Set                                                 = 0x110C,
            PnRssiGain2_Set                                                 = 0x1110,
            PnRssiGain3_Set                                                 = 0x1114,
            PnRssiGain4_Set                                                 = 0x1118,
            LnaGain1_Set                                                    = 0x111C,
            LnaGain2_Set                                                    = 0x1120,
            LnaGain3_Set                                                    = 0x1124,
            TiaCompensationCodeGroup2_Set                                   = 0x1128,
            TiaCompensationCodeGroup3_Set                                   = 0x112C,
            TiaCompensationCodeGroup4_Set                                   = 0x1130,
            PgaRssiGain1_Set                                                = 0x1134,
            PgaRssiGain2_Set                                                = 0x1138,
            PgaRssiGain3_Set                                                = 0x113C,
            PgaRssiGain4_Set                                                = 0x1140,
            AdcGain0_Set                                                    = 0x1144,
            CollisionDetectionControl_Set                                   = 0x1160,
            CollisionDetectionThresholds_Set                                = 0x1164,
            CollisionDetectionStatus_Set                                    = 0x1168,
            SettlingIndicatorControl_Set                                    = 0x1180,
            SettlingIndicatorPeriod_Set                                     = 0x1184,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            Status0_Clr                                                     = 0x2008,
            Status1_Clr                                                     = 0x200C,
            Status2_Clr                                                     = 0x2010,
            ReceivedSignalStrengthIndicator_Clr                             = 0x2018,
            FrameReceivedSignalStrengthIndicator_Clr                        = 0x201C,
            Control0_Clr                                                    = 0x2020,
            Control1_Clr                                                    = 0x2024,
            Control2_Clr                                                    = 0x2028,
            Control3_Clr                                                    = 0x202C,
            Control4_Clr                                                    = 0x2030,
            Control5_Clr                                                    = 0x2034,
            Control6_Clr                                                    = 0x2038,
            Control7_Clr                                                    = 0x203C,
            ReceivedSignalStrengthIndicatorStepThreshold_Clr                = 0x2040,
            ManualGain_Clr                                                  = 0x2044,
            InterruptFlags_Clr                                              = 0x2048,
            InterruptEnable_Clr                                             = 0x204C,
            Command_Clr                                                     = 0x2050,
            RxGainRange_Clr                                                 = 0x2054,
            AutomaticGainControlPeriod0_Clr                                 = 0x2058,
            AutomaticGainControlPeriod1_Clr                                 = 0x205C,
            HiCounterRegion0_Clr                                            = 0x2060,
            HiCounterRegion1_Clr                                            = 0x2064,
            HiCounterRegion2_Clr                                            = 0x2068,
            GainStepsLimits0_Clr                                            = 0x206C,
            GainStepsLimits1_Clr                                            = 0x2070,
            PnRfAttenuationCodeGroup0_Clr                                   = 0x2074,
            PnRfAttenuationCodeGroup1_Clr                                   = 0x2078,
            PnRfAttenuationCodeGroup2_Clr                                   = 0x207C,
            PnRfAttenuationCodeGroup3_Clr                                   = 0x2080,
            PnRfAttenuationCodeGroup4_Clr                                   = 0x2084,
            PnRfAttenuationCodeGroup5_Clr                                   = 0x2088,
            PnRfAttenuationCodeGroup6_Clr                                   = 0x208C,
            PnRfAttenuationCodeGroup7_Clr                                   = 0x2090,
            PnRfAttenuationCodeGroupAlternate_Clr                           = 0x20A4,
            LnaMixSliceCodeGroup0_Clr                                       = 0x20A8,
            LnaMixSliceCodeGroup1_Clr                                       = 0x20AC,
            ProgrammableGainAmplifierGainCodeGroup0_Clr                     = 0x20B0,
            ProgrammableGainAmplifierGainCodeGroup1_Clr                     = 0x20B4,
            ListenBeforeTalkConfiguration_Clr                               = 0x20B8,
            MirrorInterruptFlags_Clr                                        = 0x20BC,
            SequencerInterruptFlags_Clr                                     = 0x20C0,
            SequencerInterruptEnable_Clr                                    = 0x20C4,
            ReceivedSignalStrengthIndicatorAbsoluteThreshold_Clr            = 0x20C8,
            AntennaDiversity_Clr                                            = 0x20D0,
            DualRfpkdThreshold0_Clr                                         = 0x20D4,
            DualRfpkdThreshold1_Clr                                         = 0x20D8,
            Spare_Clr                                                       = 0x20DC,
            Flare_Clr                                                       = 0x20E0,
            StepDownOfdmSafeMode_Clr                                        = 0x20E4,
            ClearChannelAssessmentDebug_Clr                                 = 0x20E8,
            TiaCompensationCodeGroup0_Clr                                   = 0x20EC,
            TiaCompensationCodeGroup1_Clr                                   = 0x20F0,
            LnaMixSliceCodeGroup2_Clr                                       = 0x20F4,
            FastSwitchInterruptFlags_Clr                                    = 0x20F8,
            FastSwitchInterruptEnable_Clr                                   = 0x20FC,
            ClearChannelAssessmentSub_Clr                                   = 0x2100,
            Control8_Clr                                                    = 0x2104,
            Status3_Clr                                                     = 0x2108,
            PnRssiGain1_Clr                                                 = 0x210C,
            PnRssiGain2_Clr                                                 = 0x2110,
            PnRssiGain3_Clr                                                 = 0x2114,
            PnRssiGain4_Clr                                                 = 0x2118,
            LnaGain1_Clr                                                    = 0x211C,
            LnaGain2_Clr                                                    = 0x2120,
            LnaGain3_Clr                                                    = 0x2124,
            TiaCompensationCodeGroup2_Clr                                   = 0x2128,
            TiaCompensationCodeGroup3_Clr                                   = 0x212C,
            TiaCompensationCodeGroup4_Clr                                   = 0x2130,
            PgaRssiGain1_Clr                                                = 0x2134,
            PgaRssiGain2_Clr                                                = 0x2138,
            PgaRssiGain3_Clr                                                = 0x213C,
            PgaRssiGain4_Clr                                                = 0x2140,
            AdcGain0_Clr                                                    = 0x2144,
            CollisionDetectionControl_Clr                                   = 0x2160,
            CollisionDetectionThresholds_Clr                                = 0x2164,
            CollisionDetectionStatus_Clr                                    = 0x2168,
            SettlingIndicatorControl_Clr                                    = 0x2180,
            SettlingIndicatorPeriod_Clr                                     = 0x2184,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            Status0_Tgl                                                     = 0x3008,
            Status1_Tgl                                                     = 0x300C,
            Status2_Tgl                                                     = 0x3010,
            ReceivedSignalStrengthIndicator_Tgl                             = 0x3018,
            FrameReceivedSignalStrengthIndicator_Tgl                        = 0x301C,
            Control0_Tgl                                                    = 0x3020,
            Control1_Tgl                                                    = 0x3024,
            Control2_Tgl                                                    = 0x3028,
            Control3_Tgl                                                    = 0x302C,
            Control4_Tgl                                                    = 0x3030,
            Control5_Tgl                                                    = 0x3034,
            Control6_Tgl                                                    = 0x3038,
            Control7_Tgl                                                    = 0x303C,
            ReceivedSignalStrengthIndicatorStepThreshold_Tgl                = 0x3040,
            ManualGain_Tgl                                                  = 0x3044,
            InterruptFlags_Tgl                                              = 0x3048,
            InterruptEnable_Tgl                                             = 0x304C,
            Command_Tgl                                                     = 0x3050,
            RxGainRange_Tgl                                                 = 0x3054,
            AutomaticGainControlPeriod0_Tgl                                 = 0x3058,
            AutomaticGainControlPeriod1_Tgl                                 = 0x305C,
            HiCounterRegion0_Tgl                                            = 0x3060,
            HiCounterRegion1_Tgl                                            = 0x3064,
            HiCounterRegion2_Tgl                                            = 0x3068,
            GainStepsLimits0_Tgl                                            = 0x306C,
            GainStepsLimits1_Tgl                                            = 0x3070,
            PnRfAttenuationCodeGroup0_Tgl                                   = 0x3074,
            PnRfAttenuationCodeGroup1_Tgl                                   = 0x3078,
            PnRfAttenuationCodeGroup2_Tgl                                   = 0x307C,
            PnRfAttenuationCodeGroup3_Tgl                                   = 0x3080,
            PnRfAttenuationCodeGroup4_Tgl                                   = 0x3084,
            PnRfAttenuationCodeGroup5_Tgl                                   = 0x3088,
            PnRfAttenuationCodeGroup6_Tgl                                   = 0x308C,
            PnRfAttenuationCodeGroup7_Tgl                                   = 0x3090,
            PnRfAttenuationCodeGroupAlternate_Tgl                           = 0x30A4,
            LnaMixSliceCodeGroup0_Tgl                                       = 0x30A8,
            LnaMixSliceCodeGroup1_Tgl                                       = 0x30AC,
            ProgrammableGainAmplifierGainCodeGroup0_Tgl                     = 0x30B0,
            ProgrammableGainAmplifierGainCodeGroup1_Tgl                     = 0x30B4,
            ListenBeforeTalkConfiguration_Tgl                               = 0x30B8,
            MirrorInterruptFlags_Tgl                                        = 0x30BC,
            SequencerInterruptFlags_Tgl                                     = 0x30C0,
            SequencerInterruptEnable_Tgl                                    = 0x30C4,
            ReceivedSignalStrengthIndicatorAbsoluteThreshold_Tgl            = 0x30C8,
            AntennaDiversity_Tgl                                            = 0x30D0,
            DualRfpkdThreshold0_Tgl                                         = 0x30D4,
            DualRfpkdThreshold1_Tgl                                         = 0x30D8,
            Spare_Tgl                                                       = 0x30DC,
            Flare_Tgl                                                       = 0x30E0,
            StepDownOfdmSafeMode_Tgl                                        = 0x30E4,
            ClearChannelAssessmentDebug_Tgl                                 = 0x30E8,
            TiaCompensationCodeGroup0_Tgl                                   = 0x30EC,
            TiaCompensationCodeGroup1_Tgl                                   = 0x30F0,
            LnaMixSliceCodeGroup2_Tgl                                       = 0x30F4,
            FastSwitchInterruptFlags_Tgl                                    = 0x30F8,
            FastSwitchInterruptEnable_Tgl                                   = 0x30FC,
            ClearChannelAssessmentSub_Tgl                                   = 0x3100,
            Control8_Tgl                                                    = 0x3104,
            Status3_Tgl                                                     = 0x3108,
            PnRssiGain1_Tgl                                                 = 0x310C,
            PnRssiGain2_Tgl                                                 = 0x3110,
            PnRssiGain3_Tgl                                                 = 0x3114,
            PnRssiGain4_Tgl                                                 = 0x3118,
            LnaGain1_Tgl                                                    = 0x311C,
            LnaGain2_Tgl                                                    = 0x3120,
            LnaGain3_Tgl                                                    = 0x3124,
            TiaCompensationCodeGroup2_Tgl                                   = 0x3128,
            TiaCompensationCodeGroup3_Tgl                                   = 0x312C,
            TiaCompensationCodeGroup4_Tgl                                   = 0x3130,
            PgaRssiGain1_Tgl                                                = 0x3134,
            PgaRssiGain2_Tgl                                                = 0x3138,
            PgaRssiGain3_Tgl                                                = 0x313C,
            PgaRssiGain4_Tgl                                                = 0x3140,
            AdcGain0_Tgl                                                    = 0x3144,
            CollisionDetectionControl_Tgl                                   = 0x3160,
            CollisionDetectionThresholds_Tgl                                = 0x3164,
            CollisionDetectionStatus_Tgl                                    = 0x3168,
            SettlingIndicatorControl_Tgl                                    = 0x3180,
            SettlingIndicatorPeriod_Tgl                                     = 0x3184,
        }

        private enum CyclicRedundancyCheckRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            Control                                                         = 0x0008,
            Status                                                          = 0x000C,
            Command                                                         = 0x0010,
            InputData                                                       = 0x0014,
            InitializationValue                                             = 0x0018,
            Data                                                            = 0x001C,
            PolynomialValue                                                 = 0x0020,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            Control_Set                                                     = 0x1008,
            Status_Set                                                      = 0x100C,
            Command_Set                                                     = 0x1010,
            InputData_Set                                                   = 0x1014,
            InitializationValue_Set                                         = 0x1018,
            Data_Set                                                        = 0x101C,
            PolynomialValue_Set                                             = 0x1020,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            Control_Clr                                                     = 0x2008,
            Status_Clr                                                      = 0x200C,
            Command_Clr                                                     = 0x2010,
            InputData_Clr                                                   = 0x2014,
            InitializationValue_Clr                                         = 0x2018,
            Data_Clr                                                        = 0x201C,
            PolynomialValue_Clr                                             = 0x2020,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            Control_Tgl                                                     = 0x3008,
            Status_Tgl                                                      = 0x300C,
            Command_Tgl                                                     = 0x3010,
            InputData_Tgl                                                   = 0x3014,
            InitializationValue_Tgl                                         = 0x3018,
            Data_Tgl                                                        = 0x301C,
            PolynomialValue_Tgl                                             = 0x3020,
        }

        private enum ProtocolTimerRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            Control                                                         = 0x0008,
            Command                                                         = 0x000C,
            PrescalerChannelSelection                                       = 0x0010,
            Status                                                          = 0x0014,
            PreCounterValue                                                 = 0x0018,
            BaseCounterValue                                                = 0x001C,
            WrapCounterValue                                                = 0x0020,
            LatchedPreCounterValue                                          = 0x0024,
            LatchedBaseCounterValue                                         = 0x0028,
            LatchedWrapCounterValue                                         = 0x002C,
            PreCounterTopAdjustValue                                        = 0x0030,
            PreCounterTopValue                                              = 0x0034,
            BaseCounterTopValue                                             = 0x0038,
            WrapCounterTopValue                                             = 0x003C,
            Timeout0Counter                                                 = 0x0040,
            Timeout0CounterTop                                              = 0x0044,
            Timeout0Compare                                                 = 0x0048,
            Timeout1Counter                                                 = 0x004C,
            Timeout1CounterTop                                              = 0x0050,
            Timeout1Compare                                                 = 0x0054,
            Timeout2Counter                                                 = 0x0058,
            Timeout2CounterTop                                              = 0x005C,
            Timeout2Compare                                                 = 0x0060,
            ListenBeforeTalkWaitControl                                     = 0x0064,
            ListenBeforeTalkPrescalerChannelSelection                       = 0x0068,
            ListenBeforeTalkState                                           = 0x006C,
            PseudoRandomGeneratorValue                                      = 0x0070,
            InterruptFlags2                                                 = 0x0074,
            InterruptEnable2                                                = 0x0078,
            InterruptFlags                                                  = 0x007C,
            InterruptEnable                                                 = 0x0080,
            RxControl                                                       = 0x0084,
            TxControl                                                       = 0x0088,
            ListenBeforeTalkETSIStandardSupport                             = 0x008C,
            ListenBeforeTalkState1                                          = 0x0090,
            LinearRandomValueGeneratedByFirmware0                           = 0x0094,
            LinearRandomValueGeneratedByFirmware1                           = 0x0098,
            LinearRandomValueGeneratedByFirmware2                           = 0x009C,
            SequencerInterruptFlags2                                        = 0x00A0,
            SequencerInterruptEnable2                                       = 0x00A4,
            SequencerInterruptFlags                                         = 0x00A8,
            SequencerInterruptEnable                                        = 0x00AC,
            WrapAndBaseCounterWindowSelect                                  = 0x00B0,
            WrapAndBaseCounterWindow                                        = 0x00B4,
            Timestamp                                                       = 0x00B8,
            LatchedPreCounterValueSeq                                       = 0x00BC,
            LatchedBaseCounterValueSeq                                      = 0x00C0,
            LatchedWrapCounterValueSeq                                      = 0x00C4,
            FastSwitchInterruptFlags2                                       = 0x00E0,
            FastSwitchInterruptEnable2                                      = 0x00E4,
            FastSwitchInterruptFlags                                        = 0x00E8,
            FastSwitchInterruptEnable                                       = 0x00EC,
            CaptureCompareChannel0Control                                   = 0x0100,
            CaptureCompareChannel0PreValue                                  = 0x0104,
            CaptureCompareChannel0BaseValue                                 = 0x0108,
            CaptureCompareChannel0WrapValue                                 = 0x010C,
            CaptureCompareChannel0WrapRangeLowLimit                         = 0x0110,
            CaptureCompareChannel0WrapRangeHighLimit                        = 0x0114,
            CaptureCompareChannel0BaseRangeLowLimit                         = 0x0118,
            CaptureCompareChannel0BaseRangeHighLimit                        = 0x011C,
            CaptureCompareChannel1Control                                   = 0x0120,
            CaptureCompareChannel1PreValue                                  = 0x0124,
            CaptureCompareChannel1BaseValue                                 = 0x0128,
            CaptureCompareChannel1WrapValue                                 = 0x012C,
            CaptureCompareChannel1WrapRangeLowLimit                         = 0x0130,
            CaptureCompareChannel1WrapRangeHighLimit                        = 0x0134,
            CaptureCompareChannel1BaseRangeLowLimit                         = 0x0138,
            CaptureCompareChannel1BaseRangeHighLimit                        = 0x013C,
            CaptureCompareChannel2Control                                   = 0x0140,
            CaptureCompareChannel2PreValue                                  = 0x0144,
            CaptureCompareChannel2BaseValue                                 = 0x0148,
            CaptureCompareChannel2WrapValue                                 = 0x014C,
            CaptureCompareChannel2WrapRangeLowLimit                         = 0x0150,
            CaptureCompareChannel2WrapRangeHighLimit                        = 0x0154,
            CaptureCompareChannel2BaseRangeLowLimit                         = 0x0158,
            CaptureCompareChannel2BaseRangeHighLimit                        = 0x015C,
            CaptureCompareChannel3Control                                   = 0x0160,
            CaptureCompareChannel3PreValue                                  = 0x0164,
            CaptureCompareChannel3BaseValue                                 = 0x0168,
            CaptureCompareChannel3WrapValue                                 = 0x016C,
            CaptureCompareChannel3WrapRangeLowLimit                         = 0x0170,
            CaptureCompareChannel3WrapRangeHighLimit                        = 0x0174,
            CaptureCompareChannel3BaseRangeLowLimit                         = 0x0178,
            CaptureCompareChannel3BaseRangeHighLimit                        = 0x017C,
            CaptureCompareChannel4Control                                   = 0x0180,
            CaptureCompareChannel4PreValue                                  = 0x0184,
            CaptureCompareChannel4BaseValue                                 = 0x0188,
            CaptureCompareChannel4WrapValue                                 = 0x018C,
            CaptureCompareChannel4WrapRangeLowLimit                         = 0x0190,
            CaptureCompareChannel4WrapRangeHighLimit                        = 0x0194,
            CaptureCompareChannel4BaseRangeLowLimit                         = 0x0198,
            CaptureCompareChannel4BaseRangeHighLimit                        = 0x019C,
            CaptureCompareChannel5Control                                   = 0x01A0,
            CaptureCompareChannel5PreValue                                  = 0x01A4,
            CaptureCompareChannel5BaseValue                                 = 0x01A8,
            CaptureCompareChannel5WrapValue                                 = 0x01AC,
            CaptureCompareChannel5WrapRangeLowLimit                         = 0x01B0,
            CaptureCompareChannel5WrapRangeHighLimit                        = 0x01B4,
            CaptureCompareChannel5BaseRangeLowLimit                         = 0x01B8,
            CaptureCompareChannel5BaseRangeHighLimit                        = 0x01BC,
            CaptureCompareChannel6Control                                   = 0x01C0,
            CaptureCompareChannel6PreValue                                  = 0x01C4,
            CaptureCompareChannel6BaseValue                                 = 0x01C8,
            CaptureCompareChannel6WrapValue                                 = 0x01CC,
            CaptureCompareChannel6WrapRangeLowLimit                         = 0x01D0,
            CaptureCompareChannel6WrapRangeHighLimit                        = 0x01D4,
            CaptureCompareChannel6BaseRangeLowLimit                         = 0x01D8,
            CaptureCompareChannel6BaseRangeHighLimit                        = 0x01DC,
            CaptureCompareChannel7Control                                   = 0x01E0,
            CaptureCompareChannel7PreValue                                  = 0x01E4,
            CaptureCompareChannel7BaseValue                                 = 0x01E8,
            CaptureCompareChannel7WrapValue                                 = 0x01EC,
            CaptureCompareChannel7WrapRangeLowLimit                         = 0x01F0,
            CaptureCompareChannel7WrapRangeHighLimit                        = 0x01F4,
            CaptureCompareChannel7BaseRangeLowLimit                         = 0x01F8,
            CaptureCompareChannel7BaseRangeHighLimit                        = 0x01FC,
            CaptureCompareChannel8Control                                   = 0x0200,
            CaptureCompareChannel8PreValue                                  = 0x0204,
            CaptureCompareChannel8BaseValue                                 = 0x0208,
            CaptureCompareChannel8WrapValue                                 = 0x020C,
            CaptureCompareChannel8WrapRangeLowLimit                         = 0x0210,
            CaptureCompareChannel8WrapRangeHighLimit                        = 0x0214,
            CaptureCompareChannel8BaseRangeLowLimit                         = 0x0218,
            CaptureCompareChannel8BaseRangeHighLimit                        = 0x021C,
            CaptureCompareChannel9Control                                   = 0x0220,
            CaptureCompareChannel9PreValue                                  = 0x0224,
            CaptureCompareChannel9BaseValue                                 = 0x0228,
            CaptureCompareChannel9WrapValue                                 = 0x022C,
            CaptureCompareChannel9WrapRangeLowLimit                         = 0x0230,
            CaptureCompareChannel9WrapRangeHighLimit                        = 0x0234,
            CaptureCompareChannel9BaseRangeLowLimit                         = 0x0238,
            CaptureCompareChannel9BaseRangeHighLimit                        = 0x023C,
            CaptureCompareChannel10Control                                  = 0x0240,
            CaptureCompareChannel10PreValue                                 = 0x0244,
            CaptureCompareChannel10BaseValue                                = 0x0248,
            CaptureCompareChannel10WrapValue                                = 0x024C,
            CaptureCompareChannel10WrapRangeLowLimit                        = 0x0250,
            CaptureCompareChannel10WrapRangeHighLimit                       = 0x0254,
            CaptureCompareChannel10BaseRangeLowLimit                        = 0x0258,
            CaptureCompareChannel10BaseRangeHighLimit                       = 0x025C,
            CaptureCompareChannel11Control                                  = 0x0260,
            CaptureCompareChannel11PreValue                                 = 0x0264,
            CaptureCompareChannel11BaseValue                                = 0x0268,
            CaptureCompareChannel11WrapValue                                = 0x026C,
            CaptureCompareChannel11WrapRangeLowLimit                        = 0x0270,
            CaptureCompareChannel11WrapRangeHighLimit                       = 0x0274,
            CaptureCompareChannel11BaseRangeLowLimit                        = 0x0278,
            CaptureCompareChannel11BaseRangeHighLimit                       = 0x027C,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            Control_Set                                                     = 0x1008,
            Command_Set                                                     = 0x100C,
            PrescalerChannelSelection_Set                                   = 0x1010,
            Status_Set                                                      = 0x1014,
            PreCounterValue_Set                                             = 0x1018,
            BaseCounterValue_Set                                            = 0x101C,
            WrapCounterValue_Set                                            = 0x1020,
            LatchedPreCounterValue_Set                                      = 0x1024,
            LatchedBaseCounterValue_Set                                     = 0x1028,
            LatchedWrapCounterValue_Set                                     = 0x102C,
            PreCounterTopAdjustValue_Set                                    = 0x1030,
            PreCounterTopValue_Set                                          = 0x1034,
            BaseCounterTopValue_Set                                         = 0x1038,
            WrapCounterTopValue_Set                                         = 0x103C,
            Timeout0Counter_Set                                             = 0x1040,
            Timeout0CounterTop_Set                                          = 0x1044,
            Timeout0Compare_Set                                             = 0x1048,
            Timeout1Counter_Set                                             = 0x104C,
            Timeout1CounterTop_Set                                          = 0x1050,
            Timeout1Compare_Set                                             = 0x1054,
            Timeout2Counter_Set                                             = 0x1058,
            Timeout2CounterTop_Set                                          = 0x105C,
            Timeout2Compare_Set                                             = 0x1060,
            ListenBeforeTalkWaitControl_Set                                 = 0x1064,
            ListenBeforeTalkPrescalerChannelSelection_Set                   = 0x1068,
            ListenBeforeTalkState_Set                                       = 0x106C,
            PseudoRandomGeneratorValue_Set                                  = 0x1070,
            InterruptFlags2_Set                                             = 0x1074,
            InterruptEnable2_Set                                            = 0x1078,
            InterruptFlags_Set                                              = 0x107C,
            InterruptEnable_Set                                             = 0x1080,
            RxControl_Set                                                   = 0x1084,
            TxControl_Set                                                   = 0x1088,
            ListenBeforeTalkETSIStandardSupport_Set                         = 0x108C,
            ListenBeforeTalkState1_Set                                      = 0x1090,
            LinearRandomValueGeneratedByFirmware0_Set                       = 0x1094,
            LinearRandomValueGeneratedByFirmware1_Set                       = 0x1098,
            LinearRandomValueGeneratedByFirmware2_Set                       = 0x109C,
            SequencerInterruptFlags2_Set                                    = 0x10A0,
            SequencerInterruptEnable2_Set                                   = 0x10A4,
            SequencerInterruptFlags_Set                                     = 0x10A8,
            SequencerInterruptEnable_Set                                    = 0x10AC,
            WrapAndBaseCounterWindowSelect_Set                              = 0x10B0,
            WrapAndBaseCounterWindow_Set                                    = 0x10B4,
            Timestamp_Set                                                   = 0x10B8,
            LatchedPreCounterValueSeq_Set                                   = 0x10BC,
            LatchedBaseCounterValueSeq_Set                                  = 0x10C0,
            LatchedWrapCounterValueSeq_Set                                  = 0x10C4,
            FastSwitchInterruptFlags2_Set                                   = 0x10E0,
            FastSwitchInterruptEnable2_Set                                  = 0x10E4,
            FastSwitchInterruptFlags_Set                                    = 0x10E8,
            FastSwitchInterruptEnable_Set                                   = 0x10EC,
            CaptureCompareChannel0Control_Set                               = 0x1100,
            CaptureCompareChannel0PreValue_Set                              = 0x1104,
            CaptureCompareChannel0BaseValue_Set                             = 0x1108,
            CaptureCompareChannel0WrapValue_Set                             = 0x110C,
            CaptureCompareChannel0WrapRangeLowLimit_Set                     = 0x1110,
            CaptureCompareChannel0WrapRangeHighLimit_Set                    = 0x1114,
            CaptureCompareChannel0BaseRangeLowLimit_Set                     = 0x1118,
            CaptureCompareChannel0BaseRangeHighLimit_Set                    = 0x111C,
            CaptureCompareChannel1Control_Set                               = 0x1120,
            CaptureCompareChannel1PreValue_Set                              = 0x1124,
            CaptureCompareChannel1BaseValue_Set                             = 0x1128,
            CaptureCompareChannel1WrapValue_Set                             = 0x112C,
            CaptureCompareChannel1WrapRangeLowLimit_Set                     = 0x1130,
            CaptureCompareChannel1WrapRangeHighLimit_Set                    = 0x1134,
            CaptureCompareChannel1BaseRangeLowLimit_Set                     = 0x1138,
            CaptureCompareChannel1BaseRangeHighLimit_Set                    = 0x113C,
            CaptureCompareChannel2Control_Set                               = 0x1140,
            CaptureCompareChannel2PreValue_Set                              = 0x1144,
            CaptureCompareChannel2BaseValue_Set                             = 0x1148,
            CaptureCompareChannel2WrapValue_Set                             = 0x114C,
            CaptureCompareChannel2WrapRangeLowLimit_Set                     = 0x1150,
            CaptureCompareChannel2WrapRangeHighLimit_Set                    = 0x1154,
            CaptureCompareChannel2BaseRangeLowLimit_Set                     = 0x1158,
            CaptureCompareChannel2BaseRangeHighLimit_Set                    = 0x115C,
            CaptureCompareChannel3Control_Set                               = 0x1160,
            CaptureCompareChannel3PreValue_Set                              = 0x1164,
            CaptureCompareChannel3BaseValue_Set                             = 0x1168,
            CaptureCompareChannel3WrapValue_Set                             = 0x116C,
            CaptureCompareChannel3WrapRangeLowLimit_Set                     = 0x1170,
            CaptureCompareChannel3WrapRangeHighLimit_Set                    = 0x1174,
            CaptureCompareChannel3BaseRangeLowLimit_Set                     = 0x1178,
            CaptureCompareChannel3BaseRangeHighLimit_Set                    = 0x117C,
            CaptureCompareChannel4Control_Set                               = 0x1180,
            CaptureCompareChannel4PreValue_Set                              = 0x1184,
            CaptureCompareChannel4BaseValue_Set                             = 0x1188,
            CaptureCompareChannel4WrapValue_Set                             = 0x118C,
            CaptureCompareChannel4WrapRangeLowLimit_Set                     = 0x1190,
            CaptureCompareChannel4WrapRangeHighLimit_Set                    = 0x1194,
            CaptureCompareChannel4BaseRangeLowLimit_Set                     = 0x1198,
            CaptureCompareChannel4BaseRangeHighLimit_Set                    = 0x119C,
            CaptureCompareChannel5Control_Set                               = 0x11A0,
            CaptureCompareChannel5PreValue_Set                              = 0x11A4,
            CaptureCompareChannel5BaseValue_Set                             = 0x11A8,
            CaptureCompareChannel5WrapValue_Set                             = 0x11AC,
            CaptureCompareChannel5WrapRangeLowLimit_Set                     = 0x11B0,
            CaptureCompareChannel5WrapRangeHighLimit_Set                    = 0x11B4,
            CaptureCompareChannel5BaseRangeLowLimit_Set                     = 0x11B8,
            CaptureCompareChannel5BaseRangeHighLimit_Set                    = 0x11BC,
            CaptureCompareChannel6Control_Set                               = 0x11C0,
            CaptureCompareChannel6PreValue_Set                              = 0x11C4,
            CaptureCompareChannel6BaseValue_Set                             = 0x11C8,
            CaptureCompareChannel6WrapValue_Set                             = 0x11CC,
            CaptureCompareChannel6WrapRangeLowLimit_Set                     = 0x11D0,
            CaptureCompareChannel6WrapRangeHighLimit_Set                    = 0x11D4,
            CaptureCompareChannel6BaseRangeLowLimit_Set                     = 0x11D8,
            CaptureCompareChannel6BaseRangeHighLimit_Set                    = 0x11DC,
            CaptureCompareChannel7Control_Set                               = 0x11E0,
            CaptureCompareChannel7PreValue_Set                              = 0x11E4,
            CaptureCompareChannel7BaseValue_Set                             = 0x11E8,
            CaptureCompareChannel7WrapValue_Set                             = 0x11EC,
            CaptureCompareChannel7WrapRangeLowLimit_Set                     = 0x11F0,
            CaptureCompareChannel7WrapRangeHighLimit_Set                    = 0x11F4,
            CaptureCompareChannel7BaseRangeLowLimit_Set                     = 0x11F8,
            CaptureCompareChannel7BaseRangeHighLimit_Set                    = 0x11FC,
            CaptureCompareChannel8Control_Set                               = 0x1200,
            CaptureCompareChannel8PreValue_Set                              = 0x1204,
            CaptureCompareChannel8BaseValue_Set                             = 0x1208,
            CaptureCompareChannel8WrapValue_Set                             = 0x120C,
            CaptureCompareChannel8WrapRangeLowLimit_Set                     = 0x1210,
            CaptureCompareChannel8WrapRangeHighLimit_Set                    = 0x1214,
            CaptureCompareChannel8BaseRangeLowLimit_Set                     = 0x1218,
            CaptureCompareChannel8BaseRangeHighLimit_Set                    = 0x121C,
            CaptureCompareChannel9Control_Set                               = 0x1220,
            CaptureCompareChannel9PreValue_Set                              = 0x1224,
            CaptureCompareChannel9BaseValue_Set                             = 0x1228,
            CaptureCompareChannel9WrapValue_Set                             = 0x122C,
            CaptureCompareChannel9WrapRangeLowLimit_Set                     = 0x1230,
            CaptureCompareChannel9WrapRangeHighLimit_Set                    = 0x1234,
            CaptureCompareChannel9BaseRangeLowLimit_Set                     = 0x1238,
            CaptureCompareChannel9BaseRangeHighLimit_Set                    = 0x123C,
            CaptureCompareChannel10Control_Set                              = 0x1240,
            CaptureCompareChannel10PreValue_Set                             = 0x1244,
            CaptureCompareChannel10BaseValue_Set                            = 0x1248,
            CaptureCompareChannel10WrapValue_Set                            = 0x124C,
            CaptureCompareChannel10WrapRangeLowLimit_Set                    = 0x1250,
            CaptureCompareChannel10WrapRangeHighLimit_Set                   = 0x1254,
            CaptureCompareChannel10BaseRangeLowLimit_Set                    = 0x1258,
            CaptureCompareChannel10BaseRangeHighLimit_Set                   = 0x125C,
            CaptureCompareChannel11Control_Set                              = 0x1260,
            CaptureCompareChannel11PreValue_Set                             = 0x1264,
            CaptureCompareChannel11BaseValue_Set                            = 0x1268,
            CaptureCompareChannel11WrapValue_Set                            = 0x126C,
            CaptureCompareChannel11WrapRangeLowLimit_Set                    = 0x1270,
            CaptureCompareChannel11WrapRangeHighLimit_Set                   = 0x1274,
            CaptureCompareChannel11BaseRangeLowLimit_Set                    = 0x1278,
            CaptureCompareChannel11BaseRangeHighLimit_Set                   = 0x127C,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            Control_Clr                                                     = 0x2008,
            Command_Clr                                                     = 0x200C,
            PrescalerChannelSelection_Clr                                   = 0x2010,
            Status_Clr                                                      = 0x2014,
            PreCounterValue_Clr                                             = 0x2018,
            BaseCounterValue_Clr                                            = 0x201C,
            WrapCounterValue_Clr                                            = 0x2020,
            LatchedPreCounterValue_Clr                                      = 0x2024,
            LatchedBaseCounterValue_Clr                                     = 0x2028,
            LatchedWrapCounterValue_Clr                                     = 0x202C,
            PreCounterTopAdjustValue_Clr                                    = 0x2030,
            PreCounterTopValue_Clr                                          = 0x2034,
            BaseCounterTopValue_Clr                                         = 0x2038,
            WrapCounterTopValue_Clr                                         = 0x203C,
            Timeout0Counter_Clr                                             = 0x2040,
            Timeout0CounterTop_Clr                                          = 0x2044,
            Timeout0Compare_Clr                                             = 0x2048,
            Timeout1Counter_Clr                                             = 0x204C,
            Timeout1CounterTop_Clr                                          = 0x2050,
            Timeout1Compare_Clr                                             = 0x2054,
            Timeout2Counter_Clr                                             = 0x2058,
            Timeout2CounterTop_Clr                                          = 0x205C,
            Timeout2Compare_Clr                                             = 0x2060,
            ListenBeforeTalkWaitControl_Clr                                 = 0x2064,
            ListenBeforeTalkPrescalerChannelSelection_Clr                   = 0x2068,
            ListenBeforeTalkState_Clr                                       = 0x206C,
            PseudoRandomGeneratorValue_Clr                                  = 0x2070,
            InterruptFlags2_Clr                                             = 0x2074,
            InterruptEnable2_Clr                                            = 0x2078,
            InterruptFlags_Clr                                              = 0x207C,
            InterruptEnable_Clr                                             = 0x2080,
            RxControl_Clr                                                   = 0x2084,
            TxControl_Clr                                                   = 0x2088,
            ListenBeforeTalkETSIStandardSupport_Clr                         = 0x208C,
            ListenBeforeTalkState1_Clr                                      = 0x2090,
            LinearRandomValueGeneratedByFirmware0_Clr                       = 0x2094,
            LinearRandomValueGeneratedByFirmware1_Clr                       = 0x2098,
            LinearRandomValueGeneratedByFirmware2_Clr                       = 0x209C,
            SequencerInterruptFlags2_Clr                                    = 0x20A0,
            SequencerInterruptEnable2_Clr                                   = 0x20A4,
            SequencerInterruptFlags_Clr                                     = 0x20A8,
            SequencerInterruptEnable_Clr                                    = 0x20AC,
            WrapAndBaseCounterWindowSelect_Clr                              = 0x20B0,
            WrapAndBaseCounterWindow_Clr                                    = 0x20B4,
            Timestamp_Clr                                                   = 0x20B8,
            LatchedPreCounterValueSeq_Clr                                   = 0x20BC,
            LatchedBaseCounterValueSeq_Clr                                  = 0x20C0,
            LatchedWrapCounterValueSeq_Clr                                  = 0x20C4,
            FastSwitchInterruptFlags2_Clr                                   = 0x20E0,
            FastSwitchInterruptEnable2_Clr                                  = 0x20E4,
            FastSwitchInterruptFlags_Clr                                    = 0x20E8,
            FastSwitchInterruptEnable_Clr                                   = 0x20EC,
            CaptureCompareChannel0Control_Clr                               = 0x2100,
            CaptureCompareChannel0PreValue_Clr                              = 0x2104,
            CaptureCompareChannel0BaseValue_Clr                             = 0x2108,
            CaptureCompareChannel0WrapValue_Clr                             = 0x210C,
            CaptureCompareChannel0WrapRangeLowLimit_Clr                     = 0x2110,
            CaptureCompareChannel0WrapRangeHighLimit_Clr                    = 0x2114,
            CaptureCompareChannel0BaseRangeLowLimit_Clr                     = 0x2118,
            CaptureCompareChannel0BaseRangeHighLimit_Clr                    = 0x211C,
            CaptureCompareChannel1Control_Clr                               = 0x2120,
            CaptureCompareChannel1PreValue_Clr                              = 0x2124,
            CaptureCompareChannel1BaseValue_Clr                             = 0x2128,
            CaptureCompareChannel1WrapValue_Clr                             = 0x212C,
            CaptureCompareChannel1WrapRangeLowLimit_Clr                     = 0x2130,
            CaptureCompareChannel1WrapRangeHighLimit_Clr                    = 0x2134,
            CaptureCompareChannel1BaseRangeLowLimit_Clr                     = 0x2138,
            CaptureCompareChannel1BaseRangeHighLimit_Clr                    = 0x213C,
            CaptureCompareChannel2Control_Clr                               = 0x2140,
            CaptureCompareChannel2PreValue_Clr                              = 0x2144,
            CaptureCompareChannel2BaseValue_Clr                             = 0x2148,
            CaptureCompareChannel2WrapValue_Clr                             = 0x214C,
            CaptureCompareChannel2WrapRangeLowLimit_Clr                     = 0x2150,
            CaptureCompareChannel2WrapRangeHighLimit_Clr                    = 0x2154,
            CaptureCompareChannel2BaseRangeLowLimit_Clr                     = 0x2158,
            CaptureCompareChannel2BaseRangeHighLimit_Clr                    = 0x215C,
            CaptureCompareChannel3Control_Clr                               = 0x2160,
            CaptureCompareChannel3PreValue_Clr                              = 0x2164,
            CaptureCompareChannel3BaseValue_Clr                             = 0x2168,
            CaptureCompareChannel3WrapValue_Clr                             = 0x216C,
            CaptureCompareChannel3WrapRangeLowLimit_Clr                     = 0x2170,
            CaptureCompareChannel3WrapRangeHighLimit_Clr                    = 0x2174,
            CaptureCompareChannel3BaseRangeLowLimit_Clr                     = 0x2178,
            CaptureCompareChannel3BaseRangeHighLimit_Clr                    = 0x217C,
            CaptureCompareChannel4Control_Clr                               = 0x2180,
            CaptureCompareChannel4PreValue_Clr                              = 0x2184,
            CaptureCompareChannel4BaseValue_Clr                             = 0x2188,
            CaptureCompareChannel4WrapValue_Clr                             = 0x218C,
            CaptureCompareChannel4WrapRangeLowLimit_Clr                     = 0x2190,
            CaptureCompareChannel4WrapRangeHighLimit_Clr                    = 0x2194,
            CaptureCompareChannel4BaseRangeLowLimit_Clr                     = 0x2198,
            CaptureCompareChannel4BaseRangeHighLimit_Clr                    = 0x219C,
            CaptureCompareChannel5Control_Clr                               = 0x21A0,
            CaptureCompareChannel5PreValue_Clr                              = 0x21A4,
            CaptureCompareChannel5BaseValue_Clr                             = 0x21A8,
            CaptureCompareChannel5WrapValue_Clr                             = 0x21AC,
            CaptureCompareChannel5WrapRangeLowLimit_Clr                     = 0x21B0,
            CaptureCompareChannel5WrapRangeHighLimit_Clr                    = 0x21B4,
            CaptureCompareChannel5BaseRangeLowLimit_Clr                     = 0x21B8,
            CaptureCompareChannel5BaseRangeHighLimit_Clr                    = 0x21BC,
            CaptureCompareChannel6Control_Clr                               = 0x21C0,
            CaptureCompareChannel6PreValue_Clr                              = 0x21C4,
            CaptureCompareChannel6BaseValue_Clr                             = 0x21C8,
            CaptureCompareChannel6WrapValue_Clr                             = 0x21CC,
            CaptureCompareChannel6WrapRangeLowLimit_Clr                     = 0x21D0,
            CaptureCompareChannel6WrapRangeHighLimit_Clr                    = 0x21D4,
            CaptureCompareChannel6BaseRangeLowLimit_Clr                     = 0x21D8,
            CaptureCompareChannel6BaseRangeHighLimit_Clr                    = 0x21DC,
            CaptureCompareChannel7Control_Clr                               = 0x21E0,
            CaptureCompareChannel7PreValue_Clr                              = 0x21E4,
            CaptureCompareChannel7BaseValue_Clr                             = 0x21E8,
            CaptureCompareChannel7WrapValue_Clr                             = 0x21EC,
            CaptureCompareChannel7WrapRangeLowLimit_Clr                     = 0x21F0,
            CaptureCompareChannel7WrapRangeHighLimit_Clr                    = 0x21F4,
            CaptureCompareChannel7BaseRangeLowLimit_Clr                     = 0x21F8,
            CaptureCompareChannel7BaseRangeHighLimit_Clr                    = 0x21FC,
            CaptureCompareChannel8Control_Clr                               = 0x2200,
            CaptureCompareChannel8PreValue_Clr                              = 0x2204,
            CaptureCompareChannel8BaseValue_Clr                             = 0x2208,
            CaptureCompareChannel8WrapValue_Clr                             = 0x220C,
            CaptureCompareChannel8WrapRangeLowLimit_Clr                     = 0x2210,
            CaptureCompareChannel8WrapRangeHighLimit_Clr                    = 0x2214,
            CaptureCompareChannel8BaseRangeLowLimit_Clr                     = 0x2218,
            CaptureCompareChannel8BaseRangeHighLimit_Clr                    = 0x221C,
            CaptureCompareChannel9Control_Clr                               = 0x2220,
            CaptureCompareChannel9PreValue_Clr                              = 0x2224,
            CaptureCompareChannel9BaseValue_Clr                             = 0x2228,
            CaptureCompareChannel9WrapValue_Clr                             = 0x222C,
            CaptureCompareChannel9WrapRangeLowLimit_Clr                     = 0x2230,
            CaptureCompareChannel9WrapRangeHighLimit_Clr                    = 0x2234,
            CaptureCompareChannel9BaseRangeLowLimit_Clr                     = 0x2238,
            CaptureCompareChannel9BaseRangeHighLimit_Clr                    = 0x223C,
            CaptureCompareChannel10Control_Clr                              = 0x2240,
            CaptureCompareChannel10PreValue_Clr                             = 0x2244,
            CaptureCompareChannel10BaseValue_Clr                            = 0x2248,
            CaptureCompareChannel10WrapValue_Clr                            = 0x224C,
            CaptureCompareChannel10WrapRangeLowLimit_Clr                    = 0x2250,
            CaptureCompareChannel10WrapRangeHighLimit_Clr                   = 0x2254,
            CaptureCompareChannel10BaseRangeLowLimit_Clr                    = 0x2258,
            CaptureCompareChannel10BaseRangeHighLimit_Clr                   = 0x225C,
            CaptureCompareChannel11Control_Clr                              = 0x2260,
            CaptureCompareChannel11PreValue_Clr                             = 0x2264,
            CaptureCompareChannel11BaseValue_Clr                            = 0x2268,
            CaptureCompareChannel11WrapValue_Clr                            = 0x226C,
            CaptureCompareChannel11WrapRangeLowLimit_Clr                    = 0x2270,
            CaptureCompareChannel11WrapRangeHighLimit_Clr                   = 0x2274,
            CaptureCompareChannel11BaseRangeLowLimit_Clr                    = 0x2278,
            CaptureCompareChannel11BaseRangeHighLimit_Clr                   = 0x227C,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            Control_Tgl                                                     = 0x3008,
            Command_Tgl                                                     = 0x300C,
            PrescalerChannelSelection_Tgl                                   = 0x3010,
            Status_Tgl                                                      = 0x3014,
            PreCounterValue_Tgl                                             = 0x3018,
            BaseCounterValue_Tgl                                            = 0x301C,
            WrapCounterValue_Tgl                                            = 0x3020,
            LatchedPreCounterValue_Tgl                                      = 0x3024,
            LatchedBaseCounterValue_Tgl                                     = 0x3028,
            LatchedWrapCounterValue_Tgl                                     = 0x302C,
            PreCounterTopAdjustValue_Tgl                                    = 0x3030,
            PreCounterTopValue_Tgl                                          = 0x3034,
            BaseCounterTopValue_Tgl                                         = 0x3038,
            WrapCounterTopValue_Tgl                                         = 0x303C,
            Timeout0Counter_Tgl                                             = 0x3040,
            Timeout0CounterTop_Tgl                                          = 0x3044,
            Timeout0Compare_Tgl                                             = 0x3048,
            Timeout1Counter_Tgl                                             = 0x304C,
            Timeout1CounterTop_Tgl                                          = 0x3050,
            Timeout1Compare_Tgl                                             = 0x3054,
            Timeout2Counter_Tgl                                             = 0x3058,
            Timeout2CounterTop_Tgl                                          = 0x305C,
            Timeout2Compare_Tgl                                             = 0x3060,
            ListenBeforeTalkWaitControl_Tgl                                 = 0x3064,
            ListenBeforeTalkPrescalerChannelSelection_Tgl                   = 0x3068,
            ListenBeforeTalkState_Tgl                                       = 0x306C,
            PseudoRandomGeneratorValue_Tgl                                  = 0x3070,
            InterruptFlags2_Tgl                                             = 0x3074,
            InterruptEnable2_Tgl                                            = 0x3078,
            InterruptFlags_Tgl                                              = 0x307C,
            InterruptEnable_Tgl                                             = 0x3080,
            RxControl_Tgl                                                   = 0x3084,
            TxControl_Tgl                                                   = 0x3088,
            ListenBeforeTalkETSIStandardSupport_Tgl                         = 0x308C,
            ListenBeforeTalkState1_Tgl                                      = 0x3090,
            LinearRandomValueGeneratedByFirmware0_Tgl                       = 0x3094,
            LinearRandomValueGeneratedByFirmware1_Tgl                       = 0x3098,
            LinearRandomValueGeneratedByFirmware2_Tgl                       = 0x309C,
            SequencerInterruptFlags2_Tgl                                    = 0x30A0,
            SequencerInterruptEnable2_Tgl                                   = 0x30A4,
            SequencerInterruptFlags_Tgl                                     = 0x30A8,
            SequencerInterruptEnable_Tgl                                    = 0x30AC,
            WrapAndBaseCounterWindowSelect_Tgl                              = 0x30B0,
            WrapAndBaseCounterWindow_Tgl                                    = 0x30B4,
            Timestamp_Tgl                                                   = 0x30B8,
            LatchedPreCounterValueSeq_Tgl                                   = 0x30BC,
            LatchedBaseCounterValueSeq_Tgl                                  = 0x30C0,
            LatchedWrapCounterValueSeq_Tgl                                  = 0x30C4,
            FastSwitchInterruptFlags2_Tgl                                   = 0x30E0,
            FastSwitchInterruptEnable2_Tgl                                  = 0x30E4,
            FastSwitchInterruptFlags_Tgl                                    = 0x30E8,
            FastSwitchInterruptEnable_Tgl                                   = 0x30EC,
            CaptureCompareChannel0Control_Tgl                               = 0x3100,
            CaptureCompareChannel0PreValue_Tgl                              = 0x3104,
            CaptureCompareChannel0BaseValue_Tgl                             = 0x3108,
            CaptureCompareChannel0WrapValue_Tgl                             = 0x310C,
            CaptureCompareChannel0WrapRangeLowLimit_Tgl                     = 0x3110,
            CaptureCompareChannel0WrapRangeHighLimit_Tgl                    = 0x3114,
            CaptureCompareChannel0BaseRangeLowLimit_Tgl                     = 0x3118,
            CaptureCompareChannel0BaseRangeHighLimit_Tgl                    = 0x311C,
            CaptureCompareChannel1Control_Tgl                               = 0x3120,
            CaptureCompareChannel1PreValue_Tgl                              = 0x3124,
            CaptureCompareChannel1BaseValue_Tgl                             = 0x3128,
            CaptureCompareChannel1WrapValue_Tgl                             = 0x312C,
            CaptureCompareChannel1WrapRangeLowLimit_Tgl                     = 0x3130,
            CaptureCompareChannel1WrapRangeHighLimit_Tgl                    = 0x3134,
            CaptureCompareChannel1BaseRangeLowLimit_Tgl                     = 0x3138,
            CaptureCompareChannel1BaseRangeHighLimit_Tgl                    = 0x313C,
            CaptureCompareChannel2Control_Tgl                               = 0x3140,
            CaptureCompareChannel2PreValue_Tgl                              = 0x3144,
            CaptureCompareChannel2BaseValue_Tgl                             = 0x3148,
            CaptureCompareChannel2WrapValue_Tgl                             = 0x314C,
            CaptureCompareChannel2WrapRangeLowLimit_Tgl                     = 0x3150,
            CaptureCompareChannel2WrapRangeHighLimit_Tgl                    = 0x3154,
            CaptureCompareChannel2BaseRangeLowLimit_Tgl                     = 0x3158,
            CaptureCompareChannel2BaseRangeHighLimit_Tgl                    = 0x315C,
            CaptureCompareChannel3Control_Tgl                               = 0x3160,
            CaptureCompareChannel3PreValue_Tgl                              = 0x3164,
            CaptureCompareChannel3BaseValue_Tgl                             = 0x3168,
            CaptureCompareChannel3WrapValue_Tgl                             = 0x316C,
            CaptureCompareChannel3WrapRangeLowLimit_Tgl                     = 0x3170,
            CaptureCompareChannel3WrapRangeHighLimit_Tgl                    = 0x3174,
            CaptureCompareChannel3BaseRangeLowLimit_Tgl                     = 0x3178,
            CaptureCompareChannel3BaseRangeHighLimit_Tgl                    = 0x317C,
            CaptureCompareChannel4Control_Tgl                               = 0x3180,
            CaptureCompareChannel4PreValue_Tgl                              = 0x3184,
            CaptureCompareChannel4BaseValue_Tgl                             = 0x3188,
            CaptureCompareChannel4WrapValue_Tgl                             = 0x318C,
            CaptureCompareChannel4WrapRangeLowLimit_Tgl                     = 0x3190,
            CaptureCompareChannel4WrapRangeHighLimit_Tgl                    = 0x3194,
            CaptureCompareChannel4BaseRangeLowLimit_Tgl                     = 0x3198,
            CaptureCompareChannel4BaseRangeHighLimit_Tgl                    = 0x319C,
            CaptureCompareChannel5Control_Tgl                               = 0x31A0,
            CaptureCompareChannel5PreValue_Tgl                              = 0x31A4,
            CaptureCompareChannel5BaseValue_Tgl                             = 0x31A8,
            CaptureCompareChannel5WrapValue_Tgl                             = 0x31AC,
            CaptureCompareChannel5WrapRangeLowLimit_Tgl                     = 0x31B0,
            CaptureCompareChannel5WrapRangeHighLimit_Tgl                    = 0x31B4,
            CaptureCompareChannel5BaseRangeLowLimit_Tgl                     = 0x31B8,
            CaptureCompareChannel5BaseRangeHighLimit_Tgl                    = 0x31BC,
            CaptureCompareChannel6Control_Tgl                               = 0x31C0,
            CaptureCompareChannel6PreValue_Tgl                              = 0x31C4,
            CaptureCompareChannel6BaseValue_Tgl                             = 0x31C8,
            CaptureCompareChannel6WrapValue_Tgl                             = 0x31CC,
            CaptureCompareChannel6WrapRangeLowLimit_Tgl                     = 0x31D0,
            CaptureCompareChannel6WrapRangeHighLimit_Tgl                    = 0x31D4,
            CaptureCompareChannel6BaseRangeLowLimit_Tgl                     = 0x31D8,
            CaptureCompareChannel6BaseRangeHighLimit_Tgl                    = 0x31DC,
            CaptureCompareChannel7Control_Tgl                               = 0x31E0,
            CaptureCompareChannel7PreValue_Tgl                              = 0x31E4,
            CaptureCompareChannel7BaseValue_Tgl                             = 0x31E8,
            CaptureCompareChannel7WrapValue_Tgl                             = 0x31EC,
            CaptureCompareChannel7WrapRangeLowLimit_Tgl                     = 0x31F0,
            CaptureCompareChannel7WrapRangeHighLimit_Tgl                    = 0x31F4,
            CaptureCompareChannel7BaseRangeLowLimit_Tgl                     = 0x31F8,
            CaptureCompareChannel7BaseRangeHighLimit_Tgl                    = 0x31FC,
            CaptureCompareChannel8Control_Tgl                               = 0x3200,
            CaptureCompareChannel8PreValue_Tgl                              = 0x3204,
            CaptureCompareChannel8BaseValue_Tgl                             = 0x3208,
            CaptureCompareChannel8WrapValue_Tgl                             = 0x320C,
            CaptureCompareChannel8WrapRangeLowLimit_Tgl                     = 0x3210,
            CaptureCompareChannel8WrapRangeHighLimit_Tgl                    = 0x3214,
            CaptureCompareChannel8BaseRangeLowLimit_Tgl                     = 0x3218,
            CaptureCompareChannel8BaseRangeHighLimit_Tgl                    = 0x321C,
            CaptureCompareChannel9Control_Tgl                               = 0x3220,
            CaptureCompareChannel9PreValue_Tgl                              = 0x3224,
            CaptureCompareChannel9BaseValue_Tgl                             = 0x3228,
            CaptureCompareChannel9WrapValue_Tgl                             = 0x322C,
            CaptureCompareChannel9WrapRangeLowLimit_Tgl                     = 0x3230,
            CaptureCompareChannel9WrapRangeHighLimit_Tgl                    = 0x3234,
            CaptureCompareChannel9BaseRangeLowLimit_Tgl                     = 0x3238,
            CaptureCompareChannel9BaseRangeHighLimit_Tgl                    = 0x323C,
            CaptureCompareChannel10Control_Tgl                              = 0x3240,
            CaptureCompareChannel10PreValue_Tgl                             = 0x3244,
            CaptureCompareChannel10BaseValue_Tgl                            = 0x3248,
            CaptureCompareChannel10WrapValue_Tgl                            = 0x324C,
            CaptureCompareChannel10WrapRangeLowLimit_Tgl                    = 0x3250,
            CaptureCompareChannel10WrapRangeHighLimit_Tgl                   = 0x3254,
            CaptureCompareChannel10BaseRangeLowLimit_Tgl                    = 0x3258,
            CaptureCompareChannel10BaseRangeHighLimit_Tgl                   = 0x325C,
            CaptureCompareChannel11Control_Tgl                              = 0x3260,
            CaptureCompareChannel11PreValue_Tgl                             = 0x3264,
            CaptureCompareChannel11BaseValue_Tgl                            = 0x3268,
            CaptureCompareChannel11WrapValue_Tgl                            = 0x326C,
            CaptureCompareChannel11WrapRangeLowLimit_Tgl                    = 0x3270,
            CaptureCompareChannel11WrapRangeHighLimit_Tgl                   = 0x3274,
            CaptureCompareChannel11BaseRangeLowLimit_Tgl                    = 0x3278,
            CaptureCompareChannel11BaseRangeHighLimit_Tgl                   = 0x327C,            
        }

        private enum RadioControllerRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            RXENSourceEnable                                                = 0x0008,
            Status                                                          = 0x000C,
            Command                                                         = 0x0010,
            Control                                                         = 0x0014,
            ForceStateTransition                                            = 0x0018,
            InterruptFlags                                                  = 0x001C,
            InterruptEnable                                                 = 0x0020,
            TestControl                                                     = 0x0024,
            SequencerInterruptFlags                                         = 0x0028,
            SequencerInterruptEnable                                        = 0x002C,
            Status1                                                         = 0x0030,
            FastSwitchInterruptFlags                                        = 0x0034,
            FastSwitchInterruptEnable                                       = 0x0038,
            TxWrapUpNext                                                    = 0x003C,
            RxWrapUpNext                                                    = 0x0040,
            SequencerEndControl                                             = 0x0044,
            SequencerEndEnableControl                                       = 0x0048,
            FrameControllerClockDisable                                     = 0x004C,
            BufferControllerClockDisable                                    = 0x0050,
            RadioControllerSpare                                            = 0x0054,
            PrsControl                                                      = 0x0058,
            SoftwareDebug                                                   = 0x005C,
            SequencerTimerValue                                             = 0x0060,
            SequencerTimerCompareValue                                      = 0x0064,
            SequencerControl                                                = 0x0068,
            SequencerPrescaler                                              = 0x006C,
            Storage0                                                        = 0x0070,
            Storage1                                                        = 0x0074,
            Storage2                                                        = 0x0078,
            Storage3                                                        = 0x007C,
            FrameControllerWordBufferWrite                                  = 0x0084,
            FrameControllerWordBufferRead                                   = 0x0088,
            Em1pControlAndStatus                                            = 0x008C,
            HydraRamRemapControl                                            = 0x0090,
            SynthesizerEnableControl                                        = 0x0098,
            RadioFrequencyStatus                                            = 0x00AC,
            Status2                                                         = 0x00B0,
            IntermediateFrequencyProgrammableGainAmplifierControl           = 0x00B4,
            PowerAmplifierEnableControl                                     = 0x00B8,
            AutomaticPowerControl                                           = 0x00BC,
            AntennaDiversity                                                = 0x00C0,
            DigitalConverterControl                                         = 0x00C4,
            AuxiliaryAnalogToDigitalConverterEnable                         = 0x00CC,
            AuxiliaryAnalogToDigitalConverterControl1                       = 0x00D4,
            ClockMultEnable0                                                = 0x00DC,
            ClockMultEnable1                                                = 0x00E0,
            ClockMultControl                                                = 0x00E4,
            ClockMultStatus                                                 = 0x00E8,
            AnalogToDigitalConverterStatus                                  = 0x00FC,
            LowNoiseAmplifierMixerTrim0                                     = 0x0104,
            LowNoiseAmplifierMixerTrim1                                     = 0x0108,
            LowNoiseAmplifierMixerCalibration                               = 0x0118,
            PreambleControl                                                 = 0x0120,
            RadioEnable                                                     = 0x0160,
            RadioFrequencyPathEnable0                                       = 0x0164,
            RadioFrequencyPathEnable1                                       = 0x0168,
            Rx                                                              = 0x016C,
            Tx                                                              = 0x0170,
            SyDebug                                                         = 0x0174,
            SyTrim0                                                         = 0x0178,
            SyTrim1                                                         = 0x017C,
            SyloEnable                                                      = 0x0188,
            SymmdControl                                                    = 0x018C,
            DigitalClockRetimeControl                                       = 0x0194,
            DigitalClockRetimeStatus                                        = 0x0198,
            XoRetimeControl                                                 = 0x019C,
            XoRetimeStatus                                                  = 0x01A0,
            AutomaticGainControlOverwrite0                                  = 0x01A4,
            AutomaticGainControlOverwrite1                                  = 0x01A8,
            Spare                                                           = 0x01C8,
            MixerDigitalToAnalogConverterTrim                               = 0x01D8,
            SyTrim2                                                         = 0x01E0,
            SyDlfControl1                                                   = 0x01E8,
            SyStatus                                                        = 0x01EC,
            SyControl2                                                      = 0x01F0,
            TiaTrim0                                                        = 0x01F4,
            TiaEnable                                                       = 0x01FC,
            VtrcControl0                                                    = 0x0200,
            AuxiliaryAnalogToDigitalConverterCalibration                    = 0x0204,
            AuxiliaryAnalogToDigitalConverterControl2                       = 0x020C,
            LowNoiseAmplifierMixerEnable0                                   = 0x0210,
            MixerDigitalToAnalogConverterEnable                             = 0x0214,
            SyControl1                                                      = 0x0218,
            Tx0DbmEnable                                                    = 0x0224,
            Tx0DbmControl                                                   = 0x0228,
            Tx0DbmTrim1                                                     = 0x022C,
            Tx0DbmTrim0                                                     = 0x0230,
            Tx10DbmEnable                                                   = 0x0234,
            Tx10DbmControl0                                                 = 0x0238,
            Tx10DbmTrim1                                                    = 0x0244,
            Tx10DbmTrim2                                                    = 0x0248,
            PreRegTrim                                                      = 0x024C,
            MixerDigitalToAnalogConverterTrim1                              = 0x0250,
            SyDlf1                                                          = 0x0254,
            Spare1                                                          = 0x025C,
            PreRegStatus                                                    = 0x0260,
            VtrTrim                                                         = 0x0264,
            AnalogToDigitalConverterControl0                                = 0x0268,
            AnalogToDigitalConverterControl1                                = 0x026C,
            AnalogToDigitalConverterEnable0                                 = 0x0270,
            AnalogToDigitalConverterTrim0                                   = 0x0274,
            AnalogToDigitalConverterControl2                                = 0x0278,
            LowNoiseAmplifierMixerEnable1                                   = 0x027C,
            LowNoiseAmplifierMixerControl0                                  = 0x0280,
            LowNoiseAmplifierMixerControl1                                  = 0x0284,
            PreRegEnable                                                    = 0x0288,
            PreDebug                                                        = 0x028C,
            SyDlfControl0                                                   = 0x0290,
            SyEnable0                                                       = 0x0294,
            SyEnable1                                                       = 0x0298,
            SyEnable2                                                       = 0x029C,
            SyControl0                                                      = 0x02A0,
            SyloEnable1                                                     = 0x02A4,
            SyloEnable2                                                     = 0x02A8,
            SyloControl0                                                    = 0x02AC,
            SyloControl1                                                    = 0x02B0,
            SyloTrim0                                                       = 0x02B4,
            SyTrim3                                                         = 0x02B8,
            TiaControl0                                                     = 0x02BC,
            Tx10DbmTrim0                                                    = 0x02C8,
            SyControlTx0                                                    = 0x02CC,
            SyloControlTx0                                                  = 0x02D0,
            SyloControlTx1                                                  = 0x02D4,
            ClockMultEnable2                                                = 0x02D8,
            HfxoControl                                                     = 0x02DC,
            HfrcoRetimeControl                                              = 0x02E0,
            HfrcoRetimeStatus                                               = 0x02E4,
            AnalogToDigitalConverterOverwrite2                              = 0x02E8,
            Scratch0                                                        = 0x03E0,
            Scratch1                                                        = 0x03E4,
            Scratch2                                                        = 0x03E8,
            Scratch3                                                        = 0x03EC,
            Scratch4                                                        = 0x03F0,
            Scratch5                                                        = 0x03F4,
            Scratch6                                                        = 0x03F8,
            Scratch7                                                        = 0x03FC,
            FastSwitchControl                                               = 0x0600,
            ThermisterControl                                               = 0x07E8,
            DiagaAlternateEnable                                            = 0x07EC,
            DiagaAlternateRfBlocksAndTpSelect                               = 0x07F0,
            DiagaAlternateBridgeControl                                     = 0x07F4,
            RadioFrequencyLock0                                             = 0x07F8,
            RadioFrequencyLock1                                             = 0x07FC,
            // Set Registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            RXENSourceEnable_Set                                            = 0x1008,
            Status_Set                                                      = 0x100C,
            Command_Set                                                     = 0x1010,
            Control_Set                                                     = 0x1014,
            ForceStateTransition_Set                                        = 0x1018,
            InterruptFlags_Set                                              = 0x101C,
            InterruptEnable_Set                                             = 0x1020,
            TestControl_Set                                                 = 0x1024,
            SequencerInterruptFlags_Set                                     = 0x1028,
            SequencerInterruptEnable_Set                                    = 0x102C,
            Status1_Set                                                     = 0x1030,
            FastSwitchInterruptFlags_Set                                    = 0x1034,
            FastSwitchInterruptEnable_Set                                   = 0x1038,
            TxWrapUpNext_Set                                                = 0x103C,
            RxWrapUpNext_Set                                                = 0x1040,
            SequencerEndControl_Set                                         = 0x1044,
            SequencerEndEnableControl_Set                                   = 0x1048,
            FrameControllerClockDisable_Set                                 = 0x104C,
            BufferControllerClockDisable_Set                                = 0x1050,
            RadioControllerSpare_Set                                        = 0x1054,
            PrsControl_Set                                                  = 0x1058,
            SoftwareDebug_Set                                               = 0x105C,
            SequencerTimerValue_Set                                         = 0x1060,
            SequencerTimerCompareValue_Set                                  = 0x1064,
            SequencerControl_Set                                            = 0x1068,
            SequencerPrescaler_Set                                          = 0x106C,
            Storage0_Set                                                    = 0x1070,
            Storage1_Set                                                    = 0x1074,
            Storage2_Set                                                    = 0x1078,
            Storage3_Set                                                    = 0x107C,
            FrameControllerWordBufferWrite_Set                              = 0x1084,
            FrameControllerWordBufferRead_Set                               = 0x1088,
            Em1pControlAndStatus_Set                                        = 0x108C,
            HydraRamRemapControl_Set                                        = 0x1090,
            SynthesizerEnableControl_Set                                    = 0x1098,
            RadioFrequencyStatus_Set                                        = 0x10AC,
            Status2_Set                                                     = 0x10B0,
            IntermediateFrequencyProgrammableGainAmplifierControl_Set       = 0x10B4,
            PowerAmplifierEnableControl_Set                                 = 0x10B8,
            AutomaticPowerControl_Set                                       = 0x10BC,
            AntennaDiversity_Set                                            = 0x10C0,
            DigitalConverterControl_Set                                     = 0x10C4,
            AuxiliaryAnalogToDigitalConverterEnable_Set                     = 0x10CC,
            AuxiliaryAnalogToDigitalConverterControl1_Set                   = 0x10D4,
            ClockMultEnable0_Set                                            = 0x10DC,
            ClockMultEnable1_Set                                            = 0x10E0,
            ClockMultControl_Set                                            = 0x10E4,
            ClockMultStatus_Set                                             = 0x10E8,
            AnalogToDigitalConverterStatus_Set                              = 0x10FC,
            LowNoiseAmplifierMixerTrim0_Set                                 = 0x1104,
            LowNoiseAmplifierMixerTrim1_Set                                 = 0x1108,
            LowNoiseAmplifierMixerCalibration_Set                           = 0x1118,
            PreambleControl_Set                                             = 0x1120,
            RadioEnable_Set                                                 = 0x1160,
            RadioFrequencyPathEnable0_Set                                   = 0x1164,
            RadioFrequencyPathEnable1_Set                                   = 0x1168,
            Rx_Set                                                          = 0x116C,
            Tx_Set                                                          = 0x1170,
            SyDebug_Set                                                     = 0x1174,
            SyTrim0_Set                                                     = 0x1178,
            SyTrim1_Set                                                     = 0x117C,
            SyloEnable_Set                                                  = 0x1188,
            SymmdControl_Set                                                = 0x118C,
            DigitalClockRetimeControl_Set                                   = 0x1194,
            DigitalClockRetimeStatus_Set                                    = 0x1198,
            XoRetimeControl_Set                                             = 0x119C,
            XoRetimeStatus_Set                                              = 0x11A0,
            AutomaticGainControlOverwrite0_Set                              = 0x11A4,
            AutomaticGainControlOverwrite1_Set                              = 0x11A8,
            Spare_Set                                                       = 0x11C8,
            MixerDigitalToAnalogConverterTrim_Set                           = 0x11D8,
            SyTrim2_Set                                                     = 0x11E0,
            SyDlfControl1_Set                                               = 0x11E8,
            SyStatus_Set                                                    = 0x11EC,
            SyControl2_Set                                                  = 0x11F0,
            TiaTrim0_Set                                                    = 0x11F4,
            TiaEnable_Set                                                   = 0x11FC,
            VtrcControl0_Set                                                = 0x1200,
            AuxiliaryAnalogToDigitalConverterCalibration_Set                = 0x1204,
            AuxiliaryAnalogToDigitalConverterControl2_Set                   = 0x120C,
            LowNoiseAmplifierMixerEnable0_Set                               = 0x1210,
            MixerDigitalToAnalogConverterEnable_Set                         = 0x1214,
            SyControl1_Set                                                  = 0x1218,
            Tx0DbmEnable_Set                                                = 0x1224,
            Tx0DbmControl_Set                                               = 0x1228,
            Tx0DbmTrim1_Set                                                 = 0x122C,
            Tx0DbmTrim0_Set                                                 = 0x1230,
            Tx10DbmEnable_Set                                               = 0x1234,
            Tx10DbmControl0_Set                                             = 0x1238,
            Tx10DbmTrim1_Set                                                = 0x1244,
            Tx10DbmTrim2_Set                                                = 0x1248,
            PreRegTrim_Set                                                  = 0x124C,
            MixerDigitalToAnalogConverterTrim1_Set                          = 0x1250,
            SyDlf1_Set                                                      = 0x1254,
            Spare1_Set                                                      = 0x125C,
            PreRegStatus_Set                                                = 0x1260,
            VtrTrim_Set                                                     = 0x1264,
            AnalogToDigitalConverterControl0_Set                            = 0x1268,
            AnalogToDigitalConverterControl1_Set                            = 0x126C,
            AnalogToDigitalConverterEnable0_Set                             = 0x1270,
            AnalogToDigitalConverterTrim0_Set                               = 0x1274,
            AnalogToDigitalConverterControl2_Set                            = 0x1278,
            LowNoiseAmplifierMixerEnable1_Set                               = 0x127C,
            LowNoiseAmplifierMixerControl0_Set                              = 0x1280,
            LowNoiseAmplifierMixerControl1_Set                              = 0x1284,
            PreRegEnable_Set                                                = 0x1288,
            PreDebug_Set                                                    = 0x128C,
            SyDlfControl0_Set                                               = 0x1290,
            SyEnable0_Set                                                   = 0x1294,
            SyEnable1_Set                                                   = 0x1298,
            SyEnable2_Set                                                   = 0x129C,
            SyControl0_Set                                                  = 0x12A0,
            SyloEnable1_Set                                                 = 0x12A4,
            SyloEnable2_Set                                                 = 0x12A8,
            SyloControl0_Set                                                = 0x12AC,
            SyloControl1_Set                                                = 0x12B0,
            SyloTrim0_Set                                                   = 0x12B4,
            SyTrim3_Set                                                     = 0x12B8,
            TiaControl0_Set                                                 = 0x12BC,
            Tx10DbmTrim0_Set                                                = 0x12C8,
            SyControlTx0_Set                                                = 0x12CC,
            SyloControlTx0_Set                                              = 0x12D0,
            SyloControlTx1_Set                                              = 0x12D4,
            ClockMultEnable2_Set                                            = 0x12D8,
            HfxoControl_Set                                                 = 0x12DC,
            HfrcoRetimeControl_Set                                          = 0x12E0,
            HfrcoRetimeStatus_Set                                           = 0x12E4,
            AnalogToDigitalConverterOverwrite2_Set                          = 0x12E8,
            Scratch0_Set                                                    = 0x13E0,
            Scratch1_Set                                                    = 0x13E4,
            Scratch2_Set                                                    = 0x13E8,
            Scratch3_Set                                                    = 0x13EC,
            Scratch4_Set                                                    = 0x13F0,
            Scratch5_Set                                                    = 0x13F4,
            Scratch6_Set                                                    = 0x13F8,
            Scratch7_Set                                                    = 0x13FC,
            FastSwitchControl_Set                                           = 0x1600,
            ThermisterControl_Set                                           = 0x17E8,
            DiagaAlternateEnable_Set                                        = 0x17EC,
            DiagaAlternateRfBlocksAndTpSelect_Set                           = 0x17F0,
            DiagaAlternateBridgeControl_Set                                 = 0x17F4,
            RadioFrequencyLock0_Set                                         = 0x17F8,
            RadioFrequencyLock1_Set                                         = 0x17FC,
            // Clear Registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            RXENSourceEnable_Clr                                            = 0x2008,
            Status_Clr                                                      = 0x200C,
            Command_Clr                                                     = 0x2010,
            Control_Clr                                                     = 0x2014,
            ForceStateTransition_Clr                                        = 0x2018,
            InterruptFlags_Clr                                              = 0x201C,
            InterruptEnable_Clr                                             = 0x2020,
            TestControl_Clr                                                 = 0x2024,
            SequencerInterruptFlags_Clr                                     = 0x2028,
            SequencerInterruptEnable_Clr                                    = 0x202C,
            Status1_Clr                                                     = 0x2030,
            FastSwitchInterruptFlags_Clr                                    = 0x2034,
            FastSwitchInterruptEnable_Clr                                   = 0x2038,
            TxWrapUpNext_Clr                                                = 0x203C,
            RxWrapUpNext_Clr                                                = 0x2040,
            SequencerEndControl_Clr                                         = 0x2044,
            SequencerEndEnableControl_Clr                                   = 0x2048,
            FrameControllerClockDisable_Clr                                 = 0x204C,
            BufferControllerClockDisable_Clr                                = 0x2050,
            RadioControllerSpare_Clr                                        = 0x2054,
            PrsControl_Clr                                                  = 0x2058,
            SoftwareDebug_Clr                                               = 0x205C,
            SequencerTimerValue_Clr                                         = 0x2060,
            SequencerTimerCompareValue_Clr                                  = 0x2064,
            SequencerControl_Clr                                            = 0x2068,
            SequencerPrescaler_Clr                                          = 0x206C,
            Storage0_Clr                                                    = 0x2070,
            Storage1_Clr                                                    = 0x2074,
            Storage2_Clr                                                    = 0x2078,
            Storage3_Clr                                                    = 0x207C,
            FrameControllerWordBufferWrite_Clr                              = 0x2084,
            FrameControllerWordBufferRead_Clr                               = 0x2088,
            Em1pControlAndStatus_Clr                                        = 0x208C,
            HydraRamRemapControl_Clr                                        = 0x2090,
            SynthesizerEnableControl_Clr                                    = 0x2098,
            RadioFrequencyStatus_Clr                                        = 0x20AC,
            Status2_Clr                                                     = 0x20B0,
            IntermediateFrequencyProgrammableGainAmplifierControl_Clr       = 0x20B4,
            PowerAmplifierEnableControl_Clr                                 = 0x20B8,
            AutomaticPowerControl_Clr                                       = 0x20BC,
            AntennaDiversity_Clr                                            = 0x20C0,
            DigitalConverterControl_Clr                                     = 0x20C4,
            AuxiliaryAnalogToDigitalConverterEnable_Clr                     = 0x20CC,
            AuxiliaryAnalogToDigitalConverterControl1_Clr                   = 0x20D4,
            ClockMultEnable0_Clr                                            = 0x20DC,
            ClockMultEnable1_Clr                                            = 0x20E0,
            ClockMultControl_Clr                                            = 0x20E4,
            ClockMultStatus_Clr                                             = 0x20E8,
            AnalogToDigitalConverterStatus_Clr                              = 0x20FC,
            LowNoiseAmplifierMixerTrim0_Clr                                 = 0x2104,
            LowNoiseAmplifierMixerTrim1_Clr                                 = 0x2108,
            LowNoiseAmplifierMixerCalibration_Clr                           = 0x2118,
            PreambleControl_Clr                                             = 0x2120,
            RadioEnable_Clr                                                 = 0x2160,
            RadioFrequencyPathEnable0_Clr                                   = 0x2164,
            RadioFrequencyPathEnable1_Clr                                   = 0x2168,
            Rx_Clr                                                          = 0x216C,
            Tx_Clr                                                          = 0x2170,
            SyDebug_Clr                                                     = 0x2174,
            SyTrim0_Clr                                                     = 0x2178,
            SyTrim1_Clr                                                     = 0x217C,
            SyloEnable_Clr                                                  = 0x2188,
            SymmdControl_Clr                                                = 0x218C,
            DigitalClockRetimeControl_Clr                                   = 0x2194,
            DigitalClockRetimeStatus_Clr                                    = 0x2198,
            XoRetimeControl_Clr                                             = 0x219C,
            XoRetimeStatus_Clr                                              = 0x21A0,
            AutomaticGainControlOverwrite0_Clr                              = 0x21A4,
            AutomaticGainControlOverwrite1_Clr                              = 0x21A8,
            Spare_Clr                                                       = 0x21C8,
            MixerDigitalToAnalogConverterTrim_Clr                           = 0x21D8,
            SyTrim2_Clr                                                     = 0x21E0,
            SyDlfControl1_Clr                                               = 0x21E8,
            SyStatus_Clr                                                    = 0x21EC,
            SyControl2_Clr                                                  = 0x21F0,
            TiaTrim0_Clr                                                    = 0x21F4,
            TiaEnable_Clr                                                   = 0x21FC,
            VtrcControl0_Clr                                                = 0x2200,
            AuxiliaryAnalogToDigitalConverterCalibration_Clr                = 0x2204,
            AuxiliaryAnalogToDigitalConverterControl2_Clr                   = 0x220C,
            LowNoiseAmplifierMixerEnable0_Clr                               = 0x2210,
            MixerDigitalToAnalogConverterEnable_Clr                         = 0x2214,
            SyControl1_Clr                                                  = 0x2218,
            Tx0DbmEnable_Clr                                                = 0x2224,
            Tx0DbmControl_Clr                                               = 0x2228,
            Tx0DbmTrim1_Clr                                                 = 0x222C,
            Tx0DbmTrim0_Clr                                                 = 0x2230,
            Tx10DbmEnable_Clr                                               = 0x2234,
            Tx10DbmControl0_Clr                                             = 0x2238,
            Tx10DbmTrim1_Clr                                                = 0x2244,
            Tx10DbmTrim2_Clr                                                = 0x2248,
            PreRegTrim_Clr                                                  = 0x224C,
            MixerDigitalToAnalogConverterTrim1_Clr                          = 0x2250,
            SyDlf1_Clr                                                      = 0x2254,
            Spare1_Clr                                                      = 0x225C,
            PreRegStatus_Clr                                                = 0x2260,
            VtrTrim_Clr                                                     = 0x2264,
            AnalogToDigitalConverterControl0_Clr                            = 0x2268,
            AnalogToDigitalConverterControl1_Clr                            = 0x226C,
            AnalogToDigitalConverterEnable0_Clr                             = 0x2270,
            AnalogToDigitalConverterTrim0_Clr                               = 0x2274,
            AnalogToDigitalConverterControl2_Clr                            = 0x2278,
            LowNoiseAmplifierMixerEnable1_Clr                               = 0x227C,
            LowNoiseAmplifierMixerControl0_Clr                              = 0x2280,
            LowNoiseAmplifierMixerControl1_Clr                              = 0x2284,
            PreRegEnable_Clr                                                = 0x2288,
            PreDebug_Clr                                                    = 0x228C,
            SyDlfControl0_Clr                                               = 0x2290,
            SyEnable0_Clr                                                   = 0x2294,
            SyEnable1_Clr                                                   = 0x2298,
            SyEnable2_Clr                                                   = 0x229C,
            SyControl0_Clr                                                  = 0x22A0,
            SyloEnable1_Clr                                                 = 0x22A4,
            SyloEnable2_Clr                                                 = 0x22A8,
            SyloControl0_Clr                                                = 0x22AC,
            SyloControl1_Clr                                                = 0x22B0,
            SyloTrim0_Clr                                                   = 0x22B4,
            SyTrim3_Clr                                                     = 0x22B8,
            TiaControl0_Clr                                                 = 0x22BC,
            Tx10DbmTrim0_Clr                                                = 0x22C8,
            SyControlTx0_Clr                                                = 0x22CC,
            SyloControlTx0_Clr                                              = 0x22D0,
            SyloControlTx1_Clr                                              = 0x22D4,
            ClockMultEnable2_Clr                                            = 0x22D8,
            HfxoControl_Clr                                                 = 0x22DC,
            HfrcoRetimeControl_Clr                                          = 0x22E0,
            HfrcoRetimeStatus_Clr                                           = 0x22E4,
            AnalogToDigitalConverterOverwrite2_Clr                          = 0x22E8,
            Scratch0_Clr                                                    = 0x23E0,
            Scratch1_Clr                                                    = 0x23E4,
            Scratch2_Clr                                                    = 0x23E8,
            Scratch3_Clr                                                    = 0x23EC,
            Scratch4_Clr                                                    = 0x23F0,
            Scratch5_Clr                                                    = 0x23F4,
            Scratch6_Clr                                                    = 0x23F8,
            Scratch7_Clr                                                    = 0x23FC,
            FastSwitchControl_Clr                                           = 0x2600,
            ThermisterControl_Clr                                           = 0x27E8,
            DiagaAlternateEnable_Clr                                        = 0x27EC,
            DiagaAlternateRfBlocksAndTpSelect_Clr                           = 0x27F0,
            DiagaAlternateBridgeControl_Clr                                 = 0x27F4,
            RadioFrequencyLock0_Clr                                         = 0x27F8,
            RadioFrequencyLock1_Clr                                         = 0x27FC,
            // Toggle Registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            RXENSourceEnable_Tgl                                            = 0x3008,
            Status_Tgl                                                      = 0x300C,
            Command_Tgl                                                     = 0x3010,
            Control_Tgl                                                     = 0x3014,
            ForceStateTransition_Tgl                                        = 0x3018,
            InterruptFlags_Tgl                                              = 0x301C,
            InterruptEnable_Tgl                                             = 0x3020,
            TestControl_Tgl                                                 = 0x3024,
            SequencerInterruptFlags_Tgl                                     = 0x3028,
            SequencerInterruptEnable_Tgl                                    = 0x302C,
            Status1_Tgl                                                     = 0x3030,
            FastSwitchInterruptFlags_Tgl                                    = 0x3034,
            FastSwitchInterruptEnable_Tgl                                   = 0x3038,
            TxWrapUpNext_Tgl                                                = 0x303C,
            RxWrapUpNext_Tgl                                                = 0x3040,
            SequencerEndControl_Tgl                                         = 0x3044,
            SequencerEndEnableControl_Tgl                                   = 0x3048,
            FrameControllerClockDisable_Tgl                                 = 0x304C,
            BufferControllerClockDisable_Tgl                                = 0x3050,
            RadioControllerSpare_Tgl                                        = 0x3054,
            PrsControl_Tgl                                                  = 0x3058,
            SoftwareDebug_Tgl                                               = 0x305C,
            SequencerTimerValue_Tgl                                         = 0x3060,
            SequencerTimerCompareValue_Tgl                                  = 0x3064,
            SequencerControl_Tgl                                            = 0x3068,
            SequencerPrescaler_Tgl                                          = 0x306C,
            Storage0_Tgl                                                    = 0x3070,
            Storage1_Tgl                                                    = 0x3074,
            Storage2_Tgl                                                    = 0x3078,
            Storage3_Tgl                                                    = 0x307C,
            FrameControllerWordBufferWrite_Tgl                              = 0x3084,
            FrameControllerWordBufferRead_Tgl                               = 0x3088,
            Em1pControlAndStatus_Tgl                                        = 0x308C,
            HydraRamRemapControl_Tgl                                        = 0x3090,
            SynthesizerEnableControl_Tgl                                    = 0x3098,
            RadioFrequencyStatus_Tgl                                        = 0x30AC,
            Status2_Tgl                                                     = 0x30B0,
            IntermediateFrequencyProgrammableGainAmplifierControl_Tgl       = 0x30B4,
            PowerAmplifierEnableControl_Tgl                                 = 0x30B8,
            AutomaticPowerControl_Tgl                                       = 0x30BC,
            AntennaDiversity_Tgl                                            = 0x30C0,
            DigitalConverterControl_Tgl                                     = 0x30C4,
            AuxiliaryAnalogToDigitalConverterEnable_Tgl                     = 0x30CC,
            AuxiliaryAnalogToDigitalConverterControl1_Tgl                   = 0x30D4,
            ClockMultEnable0_Tgl                                            = 0x30DC,
            ClockMultEnable1_Tgl                                            = 0x30E0,
            ClockMultControl_Tgl                                            = 0x30E4,
            ClockMultStatus_Tgl                                             = 0x30E8,
            AnalogToDigitalConverterStatus_Tgl                              = 0x30FC,
            LowNoiseAmplifierMixerTrim0_Tgl                                 = 0x3104,
            LowNoiseAmplifierMixerTrim1_Tgl                                 = 0x3108,
            LowNoiseAmplifierMixerCalibration_Tgl                           = 0x3118,
            PreambleControl_Tgl                                             = 0x3120,
            RadioEnable_Tgl                                                 = 0x3160,
            RadioFrequencyPathEnable0_Tgl                                   = 0x3164,
            RadioFrequencyPathEnable1_Tgl                                   = 0x3168,
            Rx_Tgl                                                          = 0x316C,
            Tx_Tgl                                                          = 0x3170,
            SyDebug_Tgl                                                     = 0x3174,
            SyTrim0_Tgl                                                     = 0x3178,
            SyTrim1_Tgl                                                     = 0x317C,
            SyloEnable_Tgl                                                  = 0x3188,
            SymmdControl_Tgl                                                = 0x318C,
            DigitalClockRetimeControl_Tgl                                   = 0x3194,
            DigitalClockRetimeStatus_Tgl                                    = 0x3198,
            XoRetimeControl_Tgl                                             = 0x319C,
            XoRetimeStatus_Tgl                                              = 0x31A0,
            AutomaticGainControlOverwrite0_Tgl                              = 0x31A4,
            AutomaticGainControlOverwrite1_Tgl                              = 0x31A8,
            Spare_Tgl                                                       = 0x31C8,
            MixerDigitalToAnalogConverterTrim_Tgl                           = 0x31D8,
            SyTrim2_Tgl                                                     = 0x31E0,
            SyDlfControl1_Tgl                                               = 0x31E8,
            SyStatus_Tgl                                                    = 0x31EC,
            SyControl2_Tgl                                                  = 0x31F0,
            TiaTrim0_Tgl                                                    = 0x31F4,
            TiaEnable_Tgl                                                   = 0x31FC,
            VtrcControl0_Tgl                                                = 0x3200,
            AuxiliaryAnalogToDigitalConverterCalibration_Tgl                = 0x3204,
            AuxiliaryAnalogToDigitalConverterControl2_Tgl                   = 0x320C,
            LowNoiseAmplifierMixerEnable0_Tgl                               = 0x3210,
            MixerDigitalToAnalogConverterEnable_Tgl                         = 0x3214,
            SyControl1_Tgl                                                  = 0x3218,
            Tx0DbmEnable_Tgl                                                = 0x3224,
            Tx0DbmControl_Tgl                                               = 0x3228,
            Tx0DbmTrim1_Tgl                                                 = 0x322C,
            Tx0DbmTrim0_Tgl                                                 = 0x3230,
            Tx10DbmEnable_Tgl                                               = 0x3234,
            Tx10DbmControl0_Tgl                                             = 0x3238,
            Tx10DbmTrim1_Tgl                                                = 0x3244,
            Tx10DbmTrim2_Tgl                                                = 0x3248,
            PreRegTrim_Tgl                                                  = 0x324C,
            MixerDigitalToAnalogConverterTrim1_Tgl                          = 0x3250,
            SyDlf1_Tgl                                                      = 0x3254,
            Spare1_Tgl                                                      = 0x325C,
            PreRegStatus_Tgl                                                = 0x3260,
            VtrTrim_Tgl                                                     = 0x3264,
            AnalogToDigitalConverterControl0_Tgl                            = 0x3268,
            AnalogToDigitalConverterControl1_Tgl                            = 0x326C,
            AnalogToDigitalConverterEnable0_Tgl                             = 0x3270,
            AnalogToDigitalConverterTrim0_Tgl                               = 0x3274,
            AnalogToDigitalConverterControl2_Tgl                            = 0x3278,
            LowNoiseAmplifierMixerEnable1_Tgl                               = 0x327C,
            LowNoiseAmplifierMixerControl0_Tgl                              = 0x3280,
            LowNoiseAmplifierMixerControl1_Tgl                              = 0x3284,
            PreRegEnable_Tgl                                                = 0x3288,
            PreDebug_Tgl                                                    = 0x328C,
            SyDlfControl0_Tgl                                               = 0x3290,
            SyEnable0_Tgl                                                   = 0x3294,
            SyEnable1_Tgl                                                   = 0x3298,
            SyEnable2_Tgl                                                   = 0x329C,
            SyControl0_Tgl                                                  = 0x32A0,
            SyloEnable1_Tgl                                                 = 0x32A4,
            SyloEnable2_Tgl                                                 = 0x32A8,
            SyloControl0_Tgl                                                = 0x32AC,
            SyloControl1_Tgl                                                = 0x32B0,
            SyloTrim0_Tgl                                                   = 0x32B4,
            SyTrim3_Tgl                                                     = 0x32B8,
            TiaControl0_Tgl                                                 = 0x32BC,
            Tx10DbmTrim0_Tgl                                                = 0x32C8,
            SyControlTx0_Tgl                                                = 0x32CC,
            SyloControlTx0_Tgl                                              = 0x32D0,
            SyloControlTx1_Tgl                                              = 0x32D4,
            ClockMultEnable2_Tgl                                            = 0x32D8,
            HfxoControl_Tgl                                                 = 0x32DC,
            HfrcoRetimeControl_Tgl                                          = 0x32E0,
            HfrcoRetimeStatus_Tgl                                           = 0x32E4,
            AnalogToDigitalConverterOverwrite2_Tgl                          = 0x32E8,
            Scratch0_Tgl                                                    = 0x33E0,
            Scratch1_Tgl                                                    = 0x33E4,
            Scratch2_Tgl                                                    = 0x33E8,
            Scratch3_Tgl                                                    = 0x33EC,
            Scratch4_Tgl                                                    = 0x33F0,
            Scratch5_Tgl                                                    = 0x33F4,
            Scratch6_Tgl                                                    = 0x33F8,
            Scratch7_Tgl                                                    = 0x33FC,
            FastSwitchControl_Tgl                                           = 0x3600,
            ThermisterControl_Tgl                                           = 0x37E8,
            DiagaAlternateEnable_Tgl                                        = 0x37EC,
            DiagaAlternateRfBlocksAndTpSelect_Tgl                           = 0x37F0,
            DiagaAlternateBridgeControl_Tgl                                 = 0x37F4,
            RadioFrequencyLock0_Tgl                                         = 0x37F8,
            RadioFrequencyLock1_Tgl                                         = 0x37FC,
        }

        private enum SynthesizerRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            InterruptFlags                                                  = 0x0008,
            InterruptEnable                                                 = 0x000C,
            SequencerInterruptFlags                                         = 0x0010,
            SequencerInterruptEnable                                        = 0x0014,
            FastSwitchInterruptFlags                                        = 0x0018,
            FastSwitchInterruptEnable                                       = 0x001C,
            Status                                                          = 0x0020,
            Command                                                         = 0x0024,
            Control                                                         = 0x0028,
            VcDacControl                                                    = 0x002C,
            Frequency                                                       = 0x0030,
            IntermediateFrequency                                           = 0x0034,
            FrequencyDivisionControl                                        = 0x0038,
            ChannelControl                                                  = 0x003C,
            ChannelSpacing                                                  = 0x0040,
            CalibrationOffset                                               = 0x0044,
            VoltageControlledOscillatorFrequencyTuning                      = 0x0048,
            VoltageControlledOscillatorFrequencyRangeControl                = 0x004C,
            VoltageControlledOscillatorGainCalibration                      = 0x0050,
            QncControl                                                      = 0x0054,
            QncDacControl                                                   = 0x0058,
            VoltageControlledOscillatorForceCalibrationCycleCount           = 0x005C,
            LoCounterControl                                                = 0x0060,
            LoCounterStatus                                                 = 0x0064,
            LoCounterTarget                                                 = 0x0068,
            MmdDenominatorInit                                              = 0x006C,
            GlmsControl                                                     = 0x0070,
            PlmsControl                                                     = 0x0074,
            LmsOverride                                                     = 0x0078,
            DlfControlTx                                                    = 0x007C,
            DlfControlRx                                                    = 0x0080,
            DlfControl                                                      = 0x0084,
            DlfCoefficientKF                                                = 0x0088,
            DlfCoefficientKIZP                                              = 0x008C,
            DsmControlTx                                                    = 0x0090,
            DsmControlRx                                                    = 0x0094,
            HoppingControl                                                  = 0x0098,
            FcalCompanion                                                   = 0x009C,
            FcalControl                                                     = 0x00A0,
            FcalStepWait                                                    = 0x00A4,
            Spare                                                           = 0x00A8,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            InterruptFlags_Set                                              = 0x1008,
            InterruptEnable_Set                                             = 0x100C,
            SequencerInterruptFlags_Set                                     = 0x1010,
            SequencerInterruptEnable_Set                                    = 0x1014,
            FastSwitchInterruptFlags_Set                                    = 0x1018,
            FastSwitchInterruptEnable_Set                                   = 0x101C,
            Status_Set                                                      = 0x1020,
            Command_Set                                                     = 0x1024,
            Control_Set                                                     = 0x1028,
            VcDacControl_Set                                                = 0x102C,
            Frequency_Set                                                   = 0x1030,
            IntermediateFrequency_Set                                       = 0x1034,
            FrequencyDivisionControl_Set                                    = 0x1038,
            ChannelControl_Set                                              = 0x103C,
            ChannelSpacing_Set                                              = 0x1040,
            CalibrationOffset_Set                                           = 0x1044,
            VoltageControlledOscillatorFrequencyTuning_Set                  = 0x1048,
            VoltageControlledOscillatorFrequencyRangeControl_Set            = 0x104C,
            VoltageControlledOscillatorGainCalibration_Set                  = 0x1050,
            QncControl_Set                                                  = 0x1054,
            QncDacControl_Set                                               = 0x1058,
            VoltageControlledOscillatorForceCalibrationCycleCount_Set       = 0x105C,
            LoCounterControl_Set                                            = 0x1060,
            LoCounterStatus_Set                                             = 0x1064,
            LoCounterTarget_Set                                             = 0x1068,
            MmdDenominatorInit_Set                                          = 0x106C,
            GlmsControl_Set                                                 = 0x1070,
            PlmsControl_Set                                                 = 0x1074,
            LmsOverride_Set                                                 = 0x1078,
            DlfControlTx_Set                                                = 0x107C,
            DlfControlRx_Set                                                = 0x1080,
            DlfControl_Set                                                  = 0x1084,
            DlfCoefficientKF_Set                                            = 0x1088,
            DlfCoefficientKIZP_Set                                          = 0x108C,
            DsmControlTx_Set                                                = 0x1090,
            DsmControlRx_Set                                                = 0x1094,
            HoppingControl_Set                                              = 0x1098,
            FcalCompanion_Set                                               = 0x109C,
            FcalControl_Set                                                 = 0x10A0,
            FcalStepWait_Set                                                = 0x10A4,
            Spare_Set                                                       = 0x10A8,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            InterruptFlags_Clr                                              = 0x2008,
            InterruptEnable_Clr                                             = 0x200C,
            SequencerInterruptFlags_Clr                                     = 0x2010,
            SequencerInterruptEnable_Clr                                    = 0x2014,
            FastSwitchInterruptFlags_Clr                                    = 0x2018,
            FastSwitchInterruptEnable_Clr                                   = 0x201C,
            Status_Clr                                                      = 0x2020,
            Command_Clr                                                     = 0x2024,
            Control_Clr                                                     = 0x2028,
            VcDacControl_Clr                                                = 0x202C,
            Frequency_Clr                                                   = 0x2030,
            IntermediateFrequency_Clr                                       = 0x2034,
            FrequencyDivisionControl_Clr                                    = 0x2038,
            ChannelControl_Clr                                              = 0x203C,
            ChannelSpacing_Clr                                              = 0x2040,
            CalibrationOffset_Clr                                           = 0x2044,
            VoltageControlledOscillatorFrequencyTuning_Clr                  = 0x2048,
            VoltageControlledOscillatorFrequencyRangeControl_Clr            = 0x204C,
            VoltageControlledOscillatorGainCalibration_Clr                  = 0x2050,
            QncControl_Clr                                                  = 0x2054,
            QncDacControl_Clr                                               = 0x2058,
            VoltageControlledOscillatorForceCalibrationCycleCount_Clr       = 0x205C,
            LoCounterControl_Clr                                            = 0x2060,
            LoCounterStatus_Clr                                             = 0x2064,
            LoCounterTarget_Clr                                             = 0x2068,
            MmdDenominatorInit_Clr                                          = 0x206C,
            GlmsControl_Clr                                                 = 0x2070,
            PlmsControl_Clr                                                 = 0x2074,
            LmsOverride_Clr                                                 = 0x2078,
            DlfControlTx_Clr                                                = 0x207C,
            DlfControlRx_Clr                                                = 0x2080,
            DlfControl_Clr                                                  = 0x2084,
            DlfCoefficientKF_Clr                                            = 0x2088,
            DlfCoefficientKIZP_Clr                                          = 0x208C,
            DsmControlTx_Clr                                                = 0x2090,
            DsmControlRx_Clr                                                = 0x2094,
            HoppingControl_Clr                                              = 0x2098,
            FcalCompanion_Clr                                               = 0x209C,
            FcalControl_Clr                                                 = 0x20A0,
            FcalStepWait_Clr                                                = 0x20A4,
            Spare_Clr                                                       = 0x20A8,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            InterruptFlags_Tgl                                              = 0x3008,
            InterruptEnable_Tgl                                             = 0x300C,
            SequencerInterruptFlags_Tgl                                     = 0x3010,
            SequencerInterruptEnable_Tgl                                    = 0x3014,
            FastSwitchInterruptFlags_Tgl                                    = 0x3018,
            FastSwitchInterruptEnable_Tgl                                   = 0x301C,
            Status_Tgl                                                      = 0x3020,
            Command_Tgl                                                     = 0x3024,
            Control_Tgl                                                     = 0x3028,
            VcDacControl_Tgl                                                = 0x302C,
            Frequency_Tgl                                                   = 0x3030,
            IntermediateFrequency_Tgl                                       = 0x3034,
            FrequencyDivisionControl_Tgl                                    = 0x3038,
            ChannelControl_Tgl                                              = 0x303C,
            ChannelSpacing_Tgl                                              = 0x3040,
            CalibrationOffset_Tgl                                           = 0x3044,
            VoltageControlledOscillatorFrequencyTuning_Tgl                  = 0x3048,
            VoltageControlledOscillatorFrequencyRangeControl_Tgl            = 0x304C,
            VoltageControlledOscillatorGainCalibration_Tgl                  = 0x3050,
            QncControl_Tgl                                                  = 0x3054,
            QncDacControl_Tgl                                               = 0x3058,
            VoltageControlledOscillatorForceCalibrationCycleCount_Tgl       = 0x305C,
            LoCounterControl_Tgl                                            = 0x3060,
            LoCounterStatus_Tgl                                             = 0x3064,
            LoCounterTarget_Tgl                                             = 0x3068,
            MmdDenominatorInit_Tgl                                          = 0x306C,
            GlmsControl_Tgl                                                 = 0x3070,
            PlmsControl_Tgl                                                 = 0x3074,
            LmsOverride_Tgl                                                 = 0x3078,
            DlfControlTx_Tgl                                                = 0x307C,
            DlfControlRx_Tgl                                                = 0x3080,
            DlfControl_Tgl                                                  = 0x3084,
            DlfCoefficientKF_Tgl                                            = 0x3088,
            DlfCoefficientKIZP_Tgl                                          = 0x308C,
            DsmControlTx_Tgl                                                = 0x3090,
            DsmControlRx_Tgl                                                = 0x3094,
            HoppingControl_Tgl                                              = 0x3098,
            FcalCompanion_Tgl                                               = 0x309C,
            FcalControl_Tgl                                                 = 0x30A0,
            FcalStepWait_Tgl                                                = 0x30A4,
            Spare_Tgl                                                       = 0x30A8,            
        }

        private enum FswMailboxRegisters : long
        {
            MessagePointer0                                                 = 0x0000,
            MessagePointer1                                                 = 0x0004,
            MessagePointer2                                                 = 0x0008,
            MessagePointer3                                                 = 0x000C,
            InterruptFlags                                                  = 0x0040,
            InterruptEnable                                                 = 0x0044,
            // Set Registers
            MessagePointer0_Set                                             = 0x1000,
            MessagePointer1_Set                                             = 0x1004,
            MessagePointer2_Set                                             = 0x1008,
            MessagePointer3_Set                                             = 0x100C,
            InterruptFlags_Set                                              = 0x1040,
            InterruptEnable_Set                                             = 0x1044,
            // Clear Registers
            MessagePointer0_Clr                                             = 0x2000,
            MessagePointer1_Clr                                             = 0x2004,
            MessagePointer2_Clr                                             = 0x2008,
            MessagePointer3_Clr                                             = 0x200C,
            InterruptFlags_Clr                                              = 0x2040,
            InterruptEnable_Clr                                             = 0x2044,
            // Toggle Registers
            MessagePointer0_Tgl                                             = 0x3000,
            MessagePointer1_Tgl                                             = 0x3004,
            MessagePointer2_Tgl                                             = 0x3008,
            MessagePointer3_Tgl                                             = 0x300C,
            InterruptFlags_Tgl                                              = 0x3040,
            InterruptEnable_Tgl                                             = 0x3044,
        }

        private enum RfMailboxRegisters : long
        {
            MessagePointer0                                                 = 0x0000,
            MessagePointer1                                                 = 0x0004,
            MessagePointer2                                                 = 0x0008,
            MessagePointer3                                                 = 0x000C,
            InterruptFlags                                                  = 0x0040,
            InterruptEnable                                                 = 0x0044,
            // Set Registers
            MessagePointer0_Set                                             = 0x1000,
            MessagePointer1_Set                                             = 0x1004,
            MessagePointer2_Set                                             = 0x1008,
            MessagePointer3_Set                                             = 0x100C,
            InterruptFlags_Set                                              = 0x1040,
            InterruptEnable_Set                                             = 0x1044,
            // Clear Registers
            MessagePointer0_Clr                                             = 0x2000,
            MessagePointer1_Clr                                             = 0x2004,
            MessagePointer2_Clr                                             = 0x2008,
            MessagePointer3_Clr                                             = 0x200C,
            InterruptFlags_Clr                                              = 0x2040,
            InterruptEnable_Clr                                             = 0x2044,
            // Toggle Registers
            MessagePointer0_Tgl                                             = 0x3000,
            MessagePointer1_Tgl                                             = 0x3004,
            MessagePointer2_Tgl                                             = 0x3008,
            MessagePointer3_Tgl                                             = 0x300C,
            InterruptFlags_Tgl                                              = 0x3040,
            InterruptEnable_Tgl                                             = 0x3044,
        }

        private enum HostPortalRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Control                                                         = 0x0004,
            Status                                                          = 0x0008,
            InterruptFlags                                                  = 0x000C,
            InterruptEnable                                                 = 0x0010,
            Mailbox0                                                        = 0x0014,
            Mailbox1                                                        = 0x0018,
            Mailbox2                                                        = 0x001C,
            Mailbox3                                                        = 0x0020,
            Mailbox4                                                        = 0x0024,
            Mailbox5                                                        = 0x0028,
            Mailbox6                                                        = 0x002C,
            Mailbox7                                                        = 0x0030,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Control_Set                                                     = 0x1004,
            Status_Set                                                      = 0x1008,
            InterruptFlags_Set                                              = 0x100C,
            InterruptEnable_Set                                             = 0x1010,
            Mailbox0_Set                                                    = 0x1014,
            Mailbox1_Set                                                    = 0x1018,
            Mailbox2_Set                                                    = 0x101C,
            Mailbox3_Set                                                    = 0x1020,
            Mailbox4_Set                                                    = 0x1024,
            Mailbox5_Set                                                    = 0x1028,
            Mailbox6_Set                                                    = 0x102C,
            Mailbox7_Set                                                    = 0x1030,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Control_Clr                                                     = 0x2004,
            Status_Clr                                                      = 0x2008,
            InterruptFlags_Clr                                              = 0x200C,
            InterruptEnable_Clr                                             = 0x2010,
            Mailbox0_Clr                                                    = 0x2014,
            Mailbox1_Clr                                                    = 0x2018,
            Mailbox2_Clr                                                    = 0x201C,
            Mailbox3_Clr                                                    = 0x2020,
            Mailbox4_Clr                                                    = 0x2024,
            Mailbox5_Clr                                                    = 0x2028,
            Mailbox6_Clr                                                    = 0x202C,
            Mailbox7_Clr                                                    = 0x2030,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Control_Tgl                                                     = 0x3004,
            Status_Tgl                                                      = 0x3008,
            InterruptFlags_Tgl                                              = 0x300C,
            InterruptEnable_Tgl                                             = 0x3010,
            Mailbox0_Tgl                                                    = 0x3014,
            Mailbox1_Tgl                                                    = 0x3018,
            Mailbox2_Tgl                                                    = 0x301C,
            Mailbox3_Tgl                                                    = 0x3020,
            Mailbox4_Tgl                                                    = 0x3024,
            Mailbox5_Tgl                                                    = 0x3028,
            Mailbox6_Tgl                                                    = 0x302C,
            Mailbox7_Tgl                                                    = 0x3030,
        }

        private enum Lpw0PortalRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Control                                                         = 0x0004,
            Status                                                          = 0x0008,
            InterruptFlags                                                  = 0x000C,
            InterruptEnable                                                 = 0x0010,
            Mailbox0                                                        = 0x0014,
            Mailbox1                                                        = 0x0018,
            Mailbox2                                                        = 0x001C,
            Mailbox3                                                        = 0x0020,
            Mailbox4                                                        = 0x0024,
            Mailbox5                                                        = 0x0028,
            Mailbox6                                                        = 0x002C,
            Mailbox7                                                        = 0x0030,
            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Control_Set                                                     = 0x1004,
            Status_Set                                                      = 0x1008,
            InterruptFlags_Set                                              = 0x100C,
            InterruptEnable_Set                                             = 0x1010,
            Mailbox0_Set                                                    = 0x1014,
            Mailbox1_Set                                                    = 0x1018,
            Mailbox2_Set                                                    = 0x101C,
            Mailbox3_Set                                                    = 0x1020,
            Mailbox4_Set                                                    = 0x1024,
            Mailbox5_Set                                                    = 0x1028,
            Mailbox6_Set                                                    = 0x102C,
            Mailbox7_Set                                                    = 0x1030,
            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Control_Clr                                                     = 0x2004,
            Status_Clr                                                      = 0x2008,
            InterruptFlags_Clr                                              = 0x200C,
            InterruptEnable_Clr                                             = 0x2010,
            Mailbox0_Clr                                                    = 0x2014,
            Mailbox1_Clr                                                    = 0x2018,
            Mailbox2_Clr                                                    = 0x201C,
            Mailbox3_Clr                                                    = 0x2020,
            Mailbox4_Clr                                                    = 0x2024,
            Mailbox5_Clr                                                    = 0x2028,
            Mailbox6_Clr                                                    = 0x202C,
            Mailbox7_Clr                                                    = 0x2030,
            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Control_Tgl                                                     = 0x3004,
            Status_Tgl                                                      = 0x3008,
            InterruptFlags_Tgl                                              = 0x300C,
            InterruptEnable_Tgl                                             = 0x3010,
            Mailbox0_Tgl                                                    = 0x3014,
            Mailbox1_Tgl                                                    = 0x3018,
            Mailbox2_Tgl                                                    = 0x301C,
            Mailbox3_Tgl                                                    = 0x3020,
            Mailbox4_Tgl                                                    = 0x3024,
            Mailbox5_Tgl                                                    = 0x3028,
            Mailbox6_Tgl                                                    = 0x302C,
            Mailbox7_Tgl                                                    = 0x3030,
        }
#endregion

        private class FRC_FrameDescriptor
        {
            public IValueRegisterField words;
            public IValueRegisterField buffer;
            public IFlagRegisterField includeCrc;
            public IFlagRegisterField calculateCrc;
            public IValueRegisterField crcSkipWords;
            public IFlagRegisterField skipWhitening;
            public IFlagRegisterField addTrailData;
            // TODO: New field: to be factored in in the frame assembling/disassembling
            public IFlagRegisterField excludeSubframeFromWordCounter;

            // Magic FCD Words value of 0xFF means subframe length is infinite
            public uint? Words => words.Value == 0xFF ? null : (uint?)(words.Value + 1);
            public uint BufferIndex => (uint)buffer.Value;
        }

        private class BUFC_Buffer
        {
            public BUFC_Buffer(SiLabs_xG301_LPW parent, Machine machine, uint index)
            {
                this.parent = parent;
                this.machine = machine;
                this.index = index;
            }

            public bool ReadReady => true;
            public bool Read32Ready => true;
            public uint Size => 64u << (int)sizeMode.Value;

            public uint Address
            {
                get
                {
                    return address;
                }
                set
                {
                    address = value;
                }
            }

            public uint ReadOffset
            {
                get
                {
                    return readOffset;
                }
                set
                {
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: Set ReadOffset={1}", index, value); 
                    readOffset = value;
                    UpdateThresholdFlag();
                }
            }

            public uint WriteOffset
            {
                get
                {
                    return writeOffset;
                }
                set
                {
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: Set WriteOffset={1}", index, value); 
                    writeOffset = value;
                    UpdateThresholdFlag();
                }
            }

            public uint ReadData
            {
                get
                {
                    if (BytesNumber == 0)
                    {
                        underflow.Value = true;
                        seqUnderflow.Value = true;
                        parent.Log(LogLevel.Noisy, "Buffer #{0}: Reading underflow Size={1} writeOffset={2} readOffset={3}", 
                                   index, Size, WriteOffset, ReadOffset);
                        parent.UpdateInterrupts();
                        return 0;
                    }

                    var offset = (ulong)(Address + (ReadOffset % Size));
                    var value = machine.SystemBus.ReadByte(offset);
                    var newOffset = (ReadOffset + 1) % (2 * Size);
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: Reading value 0x{1:X} at ram's offset 0x{2:X}, oldOffset={3} newOffset{4}", 
                               index, value, offset, ReadOffset, newOffset);
                    ReadOffset = newOffset;                    
                    return value;
                }
            }

            public uint ReadData32
            {
                get
                {
                    var offset = (ulong)(Address + (ReadOffset % Size));
                    uint value = 0;

                    // If we have less than 4 bytes available we still read the bytes we have.
                    // ReadOffset does not advance.
                    if (BytesNumber < 4)
                    {
                        underflow.Value = true;
                        seqUnderflow.Value = true;
                        var bytesRead = BytesNumber;
                        for(uint i = 0; i < bytesRead; i++)
                        {
                            value = value | ((uint)machine.SystemBus.ReadByte(offset + i) << 8*(int)i);
                        }
                        parent.Log(LogLevel.Noisy, "Buffer #{0} underflow: Reading value (32bit) (partial {1} bytes) 0x{2:X} at ram's offset 0x{2:X}", 
                                   index, bytesRead, value, offset);
                        parent.UpdateInterrupts();
                    }
                    else
                    {
                        value = machine.SystemBus.ReadDoubleWord(offset);
                        var newOffset = (ReadOffset + 4) % (2 * Size);
                        parent.Log(LogLevel.Noisy, "Buffer #{0}: Reading value (32bit) 0x{1:X} at ram's offset 0x{2:X}, oldOffset={3} newOffset={4}", 
                                   index, value, offset, ReadOffset, newOffset);
                        ReadOffset = newOffset;
                    }
                    
                    return value;
                }
            }

            public uint WriteData
            {
                set
                {
                    if (BytesNumber == Size)
                    {
                        overflow.Value = true;
                        seqOverflow.Value = true;
                        parent.Log(LogLevel.Noisy, "Buffer #{0}: Writing overflow Size={1} writeOffset={2} readOffset={3}", 
                                                   index, Size, WriteOffset, ReadOffset);
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    machine.SystemBus.WriteByte(offset, (byte)value);
                    var newOffset = (WriteOffset + 1) % (2 * Size);
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: Writing value 0x{1:X} at ram's offset 0x{2:X}, oldOffset={3} newOffset={4}, readOffset={5}", 
                                               index, value, offset, WriteOffset, newOffset, ReadOffset);
                    WriteOffset = newOffset;
                }
            }

            public uint WriteData32
            {
                set
                {
                    if (BytesNumber > Size - 4)
                    {
                        overflow.Value = true;
                        seqOverflow.Value = true;
                        parent.Log(LogLevel.Noisy, "Buffer #{0}: Writing overflow (32bit) Size={1} writeOffset={2} readOffset={3}", 
                                                   index, Size, WriteOffset, ReadOffset);
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    machine.SystemBus.WriteDoubleWord(offset, value);
                    var newOffset = (WriteOffset + 4) % (2 * Size);
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: Writing value (32bit) 0x{1:X} at ram's offset 0x{2:X}, oldOffset={3} newOffset={4} readOffset={5}", 
                                               index, value, offset, WriteOffset, newOffset, ReadOffset);
                    WriteOffset = newOffset;                    
                }
            }

            public uint XorWriteData
            {
                set
                {
                    if (BytesNumber == Size)
                    {
                        overflow.Value = true;
                        seqOverflow.Value = true;
                        parent.Log(LogLevel.Noisy, "Buffer #{0}: XOR Writing overflow Size={1} writeOffset={2} readOffset={3}", 
                                                   index, Size, WriteOffset, ReadOffset);
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    var oldData = machine.SystemBus.ReadByte(offset);
                    var newData = (byte)(oldData ^ (byte)value);
                    machine.SystemBus.WriteByte(offset, newData);
                    var newOffset = (WriteOffset + 1) % (2* Size);
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: XOR Writing value 0x{1:X} (0x{2:X} ^ 0x{3:X}) at ram's offset 0x{4:X}, oldOffset={5} newOffset={6} readOffset={7}", 
                                               index, newData, oldData, value, offset, WriteOffset, newOffset, ReadOffset);
                    WriteOffset = newOffset;
                }
            }

            public uint XorWriteData32
            {
                set
                {
                    if (BytesNumber > (Size - 4))
                    {
                        overflow.Value = true;
                        seqOverflow.Value = true;
                        parent.Log(LogLevel.Noisy, "Buffer #{0}: XOR Writing overflow (32bit) Size={1} writeOffset={2} readOffset={3}", 
                                                   index, Size, WriteOffset, ReadOffset);
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    var oldData = machine.SystemBus.ReadDoubleWord(offset);
                    var newData = oldData ^ value;
                    machine.SystemBus.WriteDoubleWord(offset, newData);
                    var newOffset = (WriteOffset + 4) % (2 * Size);
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: XOR Writing value (32bit) 0x{1:X} (0x{2:X} ^ 0x{3:X}) at ram's offset 0x{4:X}, oldOffset={5} newOffset={6} readOffset={7}", 
                                               index, newData, oldData, value, offset, WriteOffset, newOffset, ReadOffset);
                    WriteOffset = newOffset;
                }
            }

            public uint BytesNumber
            {
                get
                {
                    return (uint)((WriteOffset - ReadOffset) % (2 * Size));
                }
            }

            public bool ThresholdFlag
            {
                get
                {
                    bool flag = false;

                    switch(thresholdMode.Value)
                    {
                        case BUFC_ThresholdMode.Larger:
                            if (BytesNumber > threshold.Value)
                            {
                                flag = true;
                            }
                            break;
                        case BUFC_ThresholdMode.LessOrEqual:
                            if (BytesNumber <= threshold.Value)
                            {
                                flag = true;
                            }
                            break;
                    }

                    return flag;
                }
            }

            public bool Interrupt => ((corrupt.Value && corruptEnable.Value) 
                                      || (thresholdEvent.Value && thresholdEventEnable.Value)
                                      || (underflow.Value && underflowEnable.Value)
                                      || (overflow.Value && overflowEnable.Value)
                                      || (notWordAligned.Value && notWordAlignedEnable.Value));

            public bool SeqInterrupt => ((seqCorrupt.Value && seqCorruptEnable.Value) 
                                         || (seqThresholdEvent.Value && seqThresholdEventEnable.Value)
                                         || (seqUnderflow.Value && seqUnderflowEnable.Value)
                                         || (seqOverflow.Value && seqOverflowEnable.Value)
                                         || (seqNotWordAligned.Value && seqNotWordAlignedEnable.Value));
            
            public uint Peek(uint index)
            {
                var offset = (ulong)(Address + ((ReadOffset + index) % Size));

                return machine.SystemBus.ReadByte(offset);
            }

            public bool TryReadBytes(uint length, out byte[] data)
            {
                bool savedUnderflowValue = underflow.Value;
                underflow.Value = false;
                bool retValue = true;
                data = new byte[length];
                var i = 0;
                for(; i < data.Length; ++i)
                {
                    var value = (byte)ReadData;
                    if(underflow.Value)
                    {
                        retValue = false;
                        break;
                    }
                    data[i] = value;
                }
                if(i != length)
                {
                    Array.Resize(ref data, i);
                }
                if (savedUnderflowValue)
                {
                    underflow.Value = true;
                }
                return retValue;
            }

            public bool TryWriteBytes(byte[] data, out uint i)
            {
                bool savedOverflowValue = overflow.Value;
                overflow.Value = false;
                bool retValue = true;
                for(i = 0; i < data.Length; ++i)
                {
                    WriteData = data[i];
                    if(overflow.Value)
                    {
                        retValue = false;
                        break;
                    }
                }
                if (savedOverflowValue)
                {
                    overflow.Value = true;
                }
                return retValue;
            }

            public void Clear()
            {
                parent.Log(LogLevel.Noisy, "Buffer #{0}: Clear", index);

                // TODO: clear UF and OF interrupts?
                readOffset = 0;
                writeOffset = 0;
                UpdateThresholdFlag();
            }

            public void Prefetch()
            {
                // Issuing the PREFETCH command in an empty buffer results in an underflow.
                if (BytesNumber == 0)
                {
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: Prefetch while BytesNumber==0, underflow", index);

                    underflow.Value = true;
                    seqUnderflow.Value = true;
                    parent.UpdateInterrupts();
                }
            }

            public void UpdateWriteStartOffset()
            {
                parent.Log(LogLevel.Noisy, "Buffer #{0}: Saving write offset {1}", index, writeOffset);
                writeStartOffset.Value = WriteOffset;
            }

            public void RestoreWriteOffset()
            {
                parent.Log(LogLevel.Noisy, "Buffer #{0}: Restoring write offset {1}", index, writeStartOffset.Value);
                WriteOffset = (uint)writeStartOffset.Value;
                UpdateThresholdFlag();
            }

            public void UpdateThresholdFlag()
            {
                parent.Log(LogLevel.Noisy, "Buffer #{0}: Updating Threshold flag", index);
                if (ThresholdFlag)
                {
                    parent.Log(LogLevel.Noisy, "Buffer #{0}: Threshold flag set, bytes {1} threshold {2}", index, BytesNumber, threshold.Value);
                    thresholdEvent.Value = true;
                    seqThresholdEvent.Value = true;

                    parent.UpdateInterrupts();
                }
            }

            public IFlagRegisterField corrupt;
            public IFlagRegisterField corruptEnable;
            public IFlagRegisterField thresholdEvent;
            public IFlagRegisterField thresholdEventEnable;
            public IFlagRegisterField underflow;
            public IFlagRegisterField underflowEnable;
            public IFlagRegisterField overflow;
            public IFlagRegisterField overflowEnable;
            public IFlagRegisterField notWordAligned;
            public IFlagRegisterField notWordAlignedEnable;
            public IFlagRegisterField seqCorrupt;
            public IFlagRegisterField seqCorruptEnable;
            public IFlagRegisterField seqThresholdEvent;
            public IFlagRegisterField seqThresholdEventEnable;
            public IFlagRegisterField seqUnderflow;
            public IFlagRegisterField seqUnderflowEnable;
            public IFlagRegisterField seqOverflow;
            public IFlagRegisterField seqOverflowEnable;
            public IFlagRegisterField seqNotWordAligned;
            public IFlagRegisterField seqNotWordAlignedEnable;
            public IEnumRegisterField<BUFC_SizeMode> sizeMode;
            public IValueRegisterField writeStartOffset;
            public IEnumRegisterField<BUFC_ThresholdMode> thresholdMode;
            public IValueRegisterField threshold;
            
            private readonly SiLabs_xG301_LPW parent;
            private readonly Machine machine;
            private readonly uint index;
            private uint address;
            private uint readOffset;
            private uint writeOffset;
        }

        private class PROTIMER_TimeoutCounter
        {
            public PROTIMER_TimeoutCounter(SiLabs_xG301_LPW parent, uint index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void Start()
            {
                if(syncSource.Value != PROTIMER_TimeoutCounterSource.Disabled)
                {
                    synchronizing.Value = true;
                    running.Value = false;
                }
                else
                {
                    running.Value = true;
                    synchronizing.Value = false;
                    counter.Value = counterTop.Value;
                    preCounter.Value = preCounterTop.Value;
                }
                parent.PROTIMER_HandleChangedParams();
            }

            public void Stop()
            {
                running.Value = false;
                synchronizing.Value = false;
                Finished?.Invoke();
                parent.PROTIMER_HandleChangedParams();
            }

            public void Update(PROTIMER_TimeoutCounterSource evt, uint evtCount = 1)
            {
                // TODO: handle evtCount > PROTIMER_MinimumTimeoutCounterDelay
                if ((running.Value || synchronizing.Value) && evtCount > PROTIMER_MinimumTimeoutCounterDelay)
                {
                    parent.Log(LogLevel.Error, "TOUT{0} Update() passed an evtCount > PROTIMER_MinimumTimeoutCounterDelay ({1})", index, evtCount);
                }

                while(evtCount > 0)
                {
                    if(running.Value && source.Value == evt)
                    {
                        if(preCounter.Value == 0)
                        {
                            preCounter.Value = preCounterTop.Value;

                            if(counter.Value == 0)
                            {
                                underflowInterrupt.Value = true;
                                seqUnderflowInterrupt.Value = true;

                                if(mode.Value == PROTIMER_RepeatMode.OneShot)
                                {
                                    running.Value = false;
                                    Finished?.Invoke();
                                }
                                else
                                {
                                    counter.Value = counterTop.Value;
                                }
                                parent.PROTIMER_TriggerEvent(parent.PROTIMER_GetTimeoutCounterEventFromIndex(this.index, PROTIMER_Event.TimeoutCounter0Underflow));
                                Underflowed?.Invoke();
                            }
                            else
                            {
                                counter.Value -= 1;
                            }
                        }
                        else
                        {
                            preCounter.Value -= 1;
                        }

                        bool match = (counter.Value == counterCompare.Value && preCounter.Value == preCounterCompare.Value);

                        if (match)
                        {
                            matchInterrupt.Value |= match;
                            seqMatchInterrupt.Value |= match;
                            parent.UpdateInterrupts();
                            parent.PROTIMER_TriggerEvent(parent.PROTIMER_GetTimeoutCounterEventFromIndex(this.index, PROTIMER_Event.TimeoutCounter0Match));
                        }
                    }

                    if(synchronizing.Value && syncSource.Value == evt)
                    {
                        synchronizing.Value = false;
                        running.Value = true;
                        counter.Value = counterTop.Value;
                        preCounter.Value = preCounterTop.Value;
                        Synchronized?.Invoke();
                    }
                    
                    evtCount--;
                }
            }

            private uint index;
            private SiLabs_xG301_LPW parent;
            public bool Interrupt => (underflowInterrupt.Value && underflowInterruptEnable.Value)
                                      || (matchInterrupt.Value && matchInterruptEnable.Value);
            public bool SeqInterrupt => (seqUnderflowInterrupt.Value && seqUnderflowInterruptEnable.Value)
                                         || (seqMatchInterrupt.Value && seqMatchInterruptEnable.Value);
            public event Action Synchronized;
            public event Action Underflowed;
            public event Action Finished;
            public IFlagRegisterField synchronizing;
            public IFlagRegisterField running;
            public IFlagRegisterField underflowInterrupt;
            public IFlagRegisterField underflowInterruptEnable;
            public IFlagRegisterField matchInterrupt;
            public IFlagRegisterField matchInterruptEnable;
            public IFlagRegisterField seqUnderflowInterrupt;
            public IFlagRegisterField seqUnderflowInterruptEnable;
            public IFlagRegisterField seqMatchInterrupt;
            public IFlagRegisterField seqMatchInterruptEnable;
            public IValueRegisterField counter;
            public IValueRegisterField counterCompare;
            public IValueRegisterField preCounter;
            public IValueRegisterField preCounterCompare;
            public IValueRegisterField counterTop;
            public IValueRegisterField preCounterTop;
            public IEnumRegisterField<PROTIMER_TimeoutCounterSource> source;
            public IEnumRegisterField<PROTIMER_TimeoutCounterSource> syncSource;
            public IEnumRegisterField<PROTIMER_RepeatMode> mode;
        }

        private class PROTIMER_CaptureCompareChannel
        {
            public PROTIMER_CaptureCompareChannel(SiLabs_xG301_LPW parent, uint index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void Capture(uint baseVal, uint wrapVal)
            {
                if (captureValid.Value)
                {
                    overflowInterrupt.Value = true;
                    seqOverflowInterrupt.Value = true;
                }

                captureValid.Value = true;
                interrupt.Value = true;
                seqInterrupt.Value = true;
                
                preValue.Value = 0;
                baseValue.Value = baseVal;
                wrapValue.Value = wrapVal;

                parent.UpdateInterrupts();
            }

            public bool Interrupt => ((interrupt.Value && interruptEnable.Value)
                                      || (overflowInterrupt.Value && overflowInterruptEnable.Value));

            public bool SeqInterrupt => ((seqInterrupt.Value && seqInterruptEnable.Value)
                                         || (seqOverflowInterrupt.Value && seqOverflowInterruptEnable.Value));

            private SiLabs_xG301_LPW parent;
            private uint index;
            public IFlagRegisterField interrupt;
            public IFlagRegisterField interruptEnable;
            public IFlagRegisterField seqInterrupt;
            public IFlagRegisterField seqInterruptEnable;
            public IFlagRegisterField overflowInterrupt;
            public IFlagRegisterField overflowInterruptEnable;
            public IFlagRegisterField seqOverflowInterrupt;
            public IFlagRegisterField seqOverflowInterruptEnable;
            public IFlagRegisterField enable;
            public IEnumRegisterField<PROTIMER_CaptureCompareMode> mode;
            public IFlagRegisterField preMatchEnable;
            public IFlagRegisterField baseMatchEnable;
            public IFlagRegisterField wrapMatchEnable;
            public IEnumRegisterField<PROTIMER_CaptureInputSource> captureInputSource;
            public IValueRegisterField preValue;
            public IValueRegisterField baseValue;
            public IValueRegisterField wrapValue;
            public IValueRegisterField baseLowLimit;
            public IValueRegisterField baseHighLimit;
            public IValueRegisterField wrapLowLimit;
            public IValueRegisterField wrapHighLimit;
            public IFlagRegisterField captureValid;
        }
    }
}