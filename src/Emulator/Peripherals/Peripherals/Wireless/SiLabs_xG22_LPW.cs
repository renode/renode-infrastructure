//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public class SiLabs_xG22_LPW : IBusPeripheral, IRadio, ISiLabs_PacketTraceSniffer
    {
        public SiLabs_xG22_LPW(Machine machine, CortexM sequencer, SiLabs_BUFC_1 bufferController, SiLabs_RTCC_1 prortc = null)
        {
            this.machine = machine;
            this.sequencer = sequencer;
            this.bufferController = bufferController;

            var initialTimerLimit = 0xFFFFUL;

            seqTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "seqtimer", initialTimerLimit, direction: Direction.Ascending,
                                      enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            seqTimer.LimitReached += RAC_SeqTimerHandleLimitReached;

            proTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "protimer", initialTimerLimit, direction: Direction.Ascending,
                                      enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            proTimer.LimitReached += PROTIMER_HandleTimerLimitReached;

            paRampingTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "parampingtimer", initialTimerLimit, direction: Direction.Ascending,
                                            enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            paRampingTimer.LimitReached += RAC_PaRampingTimerHandleLimitReached;

            rssiUpdateTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "rssiupdatetimer", initialTimerLimit, direction: Direction.Ascending,
                                             enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            rssiUpdateTimer.LimitReached += AGC_RssiUpdateTimerHandleLimitReached;

            txTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "txtimer", initialTimerLimit, direction: Direction.Ascending,
                                             enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            txTimer.LimitReached += RAC_TxTimerLimitReached;

            rxTimer = new LimitTimer(machine.ClockSource, MicrosecondFrequency, this, "rxtimer", initialTimerLimit, direction: Direction.Ascending,
                                             enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            rxTimer.LimitReached += RAC_RxTimerLimitReached;

            FrameControllerPrioritizedIRQ = new GPIO();
            FrameControllerIRQ = new GPIO();
            ModulatorAndDemodulatorIRQ = new GPIO();
            RadioControllerSequencerIRQ = new GPIO();
            RadioControllerRadioStateMachineIRQ = new GPIO();
            ProtocolTimerIRQ = new GPIO();
            SynthesizerIRQ = new GPIO();
            AutomaticGainControlIRQ = new GPIO();
            HostMailboxIRQ = new GPIO();
            SeqOffIRQ = new GPIO();
            SeqRxWarmIRQ = new GPIO();
            SeqRxSearchIRQ = new GPIO();
            SeqRxFrameIRQ = new GPIO();
            SeqRxPoweringDownIRQ = new GPIO();
            SeqRx2RxIRQ = new GPIO();
            SeqRxOverflowIRQ = new GPIO();
            SeqRx2TxIRQ = new GPIO();
            SeqTxWarmIRQ = new GPIO();
            SeqTxIRQ = new GPIO();
            SeqTxPoweringDownIRQ = new GPIO();
            SeqTx2RxIRQ = new GPIO();
            SeqTx2TxIRQ = new GPIO();
            SeqShutdownIRQ = new GPIO();
            SeqRadioControllerIRQ = new GPIO();
            SeqFrameControllerIRQ = new GPIO();
            SeqFrameControllerPriorityIRQ = new GPIO();
            SeqModulatorAndDemodulatorIRQ = new GPIO();
            SeqAutomaticGainControlIRQ = new GPIO();
            SeqProtocolTimerIRQ = new GPIO();
            SeqSynthesizerIRQ = new GPIO();
            SeqRfMailboxIRQ = new GPIO();
            SeqSoftware0IRQ = new GPIO();
            SeqSoftware1IRQ = new GPIO();

            sequencer.CpuWaitSignal.Set(true);

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

            if(prortc != null)
            {
                prortc.CompareMatchChannel[0] += () => PROTIMER_RtcCapture(0);
                prortc.CompareMatchChannel[1] += () => PROTIMER_RtcCapture(1);
            }

            // FRC stuff
            FRC_frameDescriptor = new FRC_FrameDescriptor[FRC_NumberOfFrameDescriptors];
            for(var idx = 0u; idx < FRC_NumberOfFrameDescriptors; ++idx)
            {
                FRC_frameDescriptor[idx] = new FRC_FrameDescriptor();
            }
            FRC_packetBufferCapture = new byte[FRC_PacketBufferCaptureSize];

            // AGC stuff
            AGC_FrameRssiIntegerPart = -100;

            frameControllerRegistersCollection = BuildFrameControllerRegistersCollection();
            cyclicRedundancyCheckRegistersCollection = BuildCyclicRedundancyCheckRegistersCollection();
            synthesizerRegistersCollection = BuildSynthesizerRegistersCollection();
            radioControllerRegistersCollection = BuildRadioControllerRegistersCollection();
            protocolTimerRegistersCollection = BuildProtocolTimerRegistersCollection();
            modulatorAndDemodulatorRegistersCollection = BuildModulatorAndDemodulatorRegistersCollection();
            automaticGainControlRegistersCollection = BuildAutomaticGainControlRegistersCollection();
            hostMailboxRegistersCollection = BuildHostMailboxRegistersCollection();
            radioMailboxRegistersCollection = BuildRadioMailboxRegistersCollection();

            InterferenceQueue.InterferenceQueueChanged += InteferenceQueueChangedCallback;
        }

        [ConnectionRegion("frc")]
        public uint ReadDoubleWordFromFrameController(long offset)
        {
            return Read<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC)", offset);
        }

        [ConnectionRegion("frc")]
        public byte ReadByteFromFrameController(long offset)
        {
            return ReadByte<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC)", offset);
        }

        [ConnectionRegion("frc")]
        public void WriteDoubleWordToFrameController(long offset, uint value)
        {
            Write<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC)", offset, value);
        }

        [ConnectionRegion("frc")]
#pragma warning disable IDE0060
        public void WriteByteToFrameController(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("frc_ns")]
        public uint ReadDoubleWordFromFrameControllerNonSecure(long offset)
        {
            return Read<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset);
        }

        [ConnectionRegion("frc_ns")]
        public byte ReadByteFromFrameControllerNonSecure(long offset)
        {
            return ReadByte<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset);
        }

        [ConnectionRegion("frc_ns")]
        public void WriteDoubleWordToFrameControllerNonSecure(long offset, uint value)
        {
            Write<FrameControllerRegisters>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset, value);
        }

        [ConnectionRegion("frc_ns")]
#pragma warning disable IDE0060
        public void WriteByteToFrameControllerNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("agc")]
        public uint ReadDoubleWordFromAutomaticGainController(long offset)
        {
            return Read<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC)", offset);
        }

        [ConnectionRegion("agc")]
        public byte ReadByteFromAutomaticGainController(long offset)
        {
            return ReadByte<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC)", offset);
        }

        [ConnectionRegion("agc")]
        public void WriteDoubleWordToAutomaticGainController(long offset, uint value)
        {
            Write<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC)", offset, value);
        }

        [ConnectionRegion("agc")]
#pragma warning disable IDE0060
        public void WriteByteToAutomaticGainController(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("agc_ns")]
        public uint ReadDoubleWordFromAutomaticGainControllerNonSecure(long offset)
        {
            return Read<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_NS)", offset);
        }

        [ConnectionRegion("agc_ns")]
        public byte ReadByteFromAutomaticGainControllerNonSecure(long offset)
        {
            return ReadByte<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC)", offset);
        }

        [ConnectionRegion("agc_ns")]
        public void WriteDoubleWordToAutomaticGainControllerNonSecure(long offset, uint value)
        {
            Write<AutomaticGainControlRegisters>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_NS)", offset, value);
        }

        [ConnectionRegion("agc_ns")]
#pragma warning disable IDE0060
        public void WriteByteToAutomaticGainControllerNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("crc")]
        public uint ReadDoubleWordFromCyclicRedundancyCheck(long offset)
        {
            return Read<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC)", offset);
        }

        [ConnectionRegion("crc")]
        public byte ReadByteFromCyclicRedundancyCheck(long offset)
        {
            return ReadByte<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC)", offset);
        }

        [ConnectionRegion("crc")]
        public void WriteDoubleWordToCyclicRedundancyCheck(long offset, uint value)
        {
            Write<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC)", offset, value);
        }

        [ConnectionRegion("crc")]
#pragma warning disable IDE0060
        public void WriteByteToCyclicRedundancyCheck(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("crc_ns")]
        public uint ReadDoubleWordFromCyclicRedundancyCheckNonSecure(long offset)
        {
            return Read<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset);
        }

        [ConnectionRegion("crc_ns")]
        public byte ReadByteFromCyclicRedundancyCheckNonSecure(long offset)
        {
            return ReadByte<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset);
        }

        [ConnectionRegion("crc_ns")]
        public void WriteDoubleWordToCyclicRedundancyCheckNonSecure(long offset, uint value)
        {
            Write<CyclicRedundancyCheckRegisters>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset, value);
        }

        [ConnectionRegion("crc_ns")]
#pragma warning disable IDE0060
        public void WriteByteToCyclicRedundancyCheckNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("modem")]
        public uint ReadDoubleWordFromModulatorAndDemodulator(long offset)
        {
            return Read<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM)", offset);
        }

        [ConnectionRegion("modem")]
        public byte ReadByteFromModulatorAndDemodulator(long offset)
        {
            return ReadByte<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM)", offset);
        }

        [ConnectionRegion("modem")]
        public void WriteDoubleWordToModulatorAndDemodulator(long offset, uint value)
        {
            Write<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM)", offset, value);
        }

        [ConnectionRegion("modem")]
#pragma warning disable IDE0060
        public void WriteByteToModulatorAndDemodulator(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("modem_ns")]
        public uint ReadDoubleWordFromModulatorAndDemodulatorNonSecure(long offset)
        {
            return Read<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset);
        }

        [ConnectionRegion("modem_ns")]
        public byte ReadByteFromModulatorAndDemodulatorNonSecure(long offset)
        {
            return ReadByte<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset);
        }

        [ConnectionRegion("modem_ns")]
        public void WriteDoubleWordToModulatorAndDemodulatorNonSecure(long offset, uint value)
        {
            Write<ModulatorAndDemodulatorRegisters>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset, value);
        }

        [ConnectionRegion("modem_ns")]
#pragma warning disable IDE0060
        public void WriteByteToModulatorAndDemodulatorNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("synth")]
        public uint ReadDoubleWordFromSynthesizer(long offset)
        {
            return Read<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH)", offset);
        }

        [ConnectionRegion("synth")]
        public byte ReadByteFromSynthesizer(long offset)
        {
            return ReadByte<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH)", offset);
        }

        [ConnectionRegion("synth")]
        public void WriteDoubleWordToSynthesizer(long offset, uint value)
        {
            Write<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH)", offset, value);
        }

        [ConnectionRegion("synth")]
#pragma warning disable IDE0060
        public void WriteByteToSynthesizer(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("synth_ns")]
        public uint ReadDoubleWordFromSynthesizerNonSecure(long offset)
        {
            return Read<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset);
        }

        [ConnectionRegion("synth_ns")]
        public byte ReadByteFromSynthesizerNonSecure(long offset)
        {
            return ReadByte<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset);
        }

        [ConnectionRegion("synth_ns")]
        public void WriteDoubleWordToSynthesizerNonSecure(long offset, uint value)
        {
            Write<SynthesizerRegisters>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset, value);
        }

        [ConnectionRegion("synth_ns")]
#pragma warning disable IDE0060
        public void WriteByteToSynthesizerNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("protimer")]
        public uint ReadDoubleWordFromProtocolTimer(long offset)
        {
            return Read<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER)", offset);
        }

        [ConnectionRegion("protimer")]
        public byte ReadByteFromProtocolTimer(long offset)
        {
            return ReadByte<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER)", offset);
        }

        [ConnectionRegion("protimer")]
        public void WriteDoubleWordToProtocolTimer(long offset, uint value)
        {
            Write<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER)", offset, value);
        }

        [ConnectionRegion("protimer")]
#pragma warning disable IDE0060
        public void WriteByteToProtocolTimer(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("protimer_ns")]
        public uint ReadDoubleWordFromProtocolTimerNonSecure(long offset)
        {
            return Read<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset);
        }

        [ConnectionRegion("protimer_ns")]
        public byte ReadByteFromProtocolTimerNonSecure(long offset)
        {
            return ReadByte<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset);
        }

        [ConnectionRegion("protimer_ns")]
        public void WriteDoubleWordToProtocolTimerNonSecure(long offset, uint value)
        {
            Write<ProtocolTimerRegisters>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset, value);
        }

        [ConnectionRegion("protimer_ns")]
#pragma warning disable IDE0060
        public void WriteByteToProtocolTimerNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("rac")]
        public uint ReadDoubleWordFromRadioController(long offset)
        {
            return Read<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC)", offset);
        }

        [ConnectionRegion("rac")]
        public byte ReadByteFromRadioController(long offset)
        {
            return ReadByte<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC)", offset);
        }

        [ConnectionRegion("rac")]
        public void WriteDoubleWordToRadioController(long offset, uint value)
        {
            Write<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC)", offset, value);
        }

        [ConnectionRegion("rac")]
#pragma warning disable IDE0060
        public void WriteByteToRadioController(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("rac_ns")]
        public uint ReadDoubleWordFromRadioControllerNonSecure(long offset)
        {
            return Read<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset);
        }

        [ConnectionRegion("rac_ns")]
        public byte ReadByteFromRadioControllerNonSecure(long offset)
        {
            return ReadByte<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset);
        }

        [ConnectionRegion("rac_ns")]
        public void WriteDoubleWordToRadioControllerNonSecure(long offset, uint value)
        {
            Write<RadioControllerRegisters>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset, value);
        }

        [ConnectionRegion("rac_ns")]
#pragma warning disable IDE0060
        public void WriteByteToRadioControllerNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("rfmailbox")]
        public uint ReadDoubleWordFromRadioMailbox(long offset)
        {
            return Read<RadioMailboxRegisters>(radioMailboxRegistersCollection, "Radio Mailbox (RFMAILBOX)", offset);
        }

        [ConnectionRegion("rfmailbox")]
        public byte ReadByteFromRadioMailbox(long offset)
        {
            return ReadByte<RadioMailboxRegisters>(radioMailboxRegistersCollection, "Radio Mailbox (RFMAILBOX)", offset);
        }

        [ConnectionRegion("rfmailbox")]
        public void WriteDoubleWordToRadioMailbox(long offset, uint value)
        {
            Write<RadioMailboxRegisters>(radioMailboxRegistersCollection, "Radio Mailbox (RFMAILBOX)", offset, value);
        }

        [ConnectionRegion("rfmailbox")]
#pragma warning disable IDE0060
        public void WriteByteToRadioMailbox(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("rfmailbox_ns")]
        public uint ReadDoubleWordFromRadioMailboxNonSecure(long offset)
        {
            return Read<RadioMailboxRegisters>(radioMailboxRegistersCollection, "Radio Mailbox (RFMAILBOX_NS)", offset);
        }

        [ConnectionRegion("rfmailbox_ns")]
        public byte ReadByteFromRadioMailboxNonSecure(long offset)
        {
            return ReadByte<RadioMailboxRegisters>(radioMailboxRegistersCollection, "Radio Mailbox (RFMAILBOX_NS)", offset);
        }

        [ConnectionRegion("rfmailbox_ns")]
        public void WriteDoubleWordToRadioMailboxNonSecure(long offset, uint value)
        {
            Write<RadioMailboxRegisters>(radioMailboxRegistersCollection, "Radio Mailbox (RFMAILBOX_NS)", offset, value);
        }

        [ConnectionRegion("rfmailbox_ns")]
#pragma warning disable IDE0060
        public void WriteByteToRadioMailboxNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("hostmailbox")]
        public uint ReadDoubleWordFromHostMailbox(long offset)
        {
            return Read<HostMailboxRegisters>(hostMailboxRegistersCollection, "Host Mailbox (HOSTMAILBOX)", offset);
        }

        [ConnectionRegion("hostmailbox")]
        public byte ReadByteFromHostMailbox(long offset)
        {
            return ReadByte<HostMailboxRegisters>(hostMailboxRegistersCollection, "Host Mailbox (HOSTMAILBOX)", offset);
        }

        [ConnectionRegion("hostmailbox")]
        public void WriteDoubleWordToHostMailbox(long offset, uint value)
        {
            Write<HostMailboxRegisters>(hostMailboxRegistersCollection, "Host Mailbox (HOSTMAILBOX)", offset, value);
        }

        [ConnectionRegion("hostmailbox")]
#pragma warning disable IDE0060
        public void WriteByteToHostMailbox(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        [ConnectionRegion("hostmailbox_ns")]
        public uint ReadDoubleWordFromHostMailboxNonSecure(long offset)
        {
            return Read<HostMailboxRegisters>(hostMailboxRegistersCollection, "Host Mailbox (HOSTMAILBOX_NS)", offset);
        }

        [ConnectionRegion("hostmailbox_ns")]
        public byte ReadByteFromHostMailboxNonSecure(long offset)
        {
            return ReadByte<HostMailboxRegisters>(hostMailboxRegistersCollection, "Host Mailbox (HOSTMAILBOX_NS)", offset);
        }

        [ConnectionRegion("hostmailbox_ns")]
        public void WriteDoubleWordToHostMailboxNonSecure(long offset, uint value)
        {
            Write<HostMailboxRegisters>(hostMailboxRegistersCollection, "Host Mailbox (HOSTMAILBOX_NS)", offset, value);
        }

        [ConnectionRegion("hostmailbox_ns")]
#pragma warning disable IDE0060
        public void WriteByteToHostMailboxNonSecure(long offset, byte value)
#pragma warning restore IDE0060
        {
            // TODO: Single byte writes not implemented for now
        }

        public void Reset()
        {
            // Reset frame and channel variables
            currentFrame = null;
            currentFrameOffset = 0;
            currentDisassembleSubFrame = 0;
            currentDisassembleSubFrameStartWord = 0;
            currentDisassembleSubFrameBytes = 0;
            currentFrameByteOverTheAirTimeUs = 0;
            currentFrameElapsedAirtimeUs = 0;

            // Reset FRC variables
            FRC_rxDonePending = false;
            FRC_rxFrameIrqClearedPending = false;

            // Reset RAC variables
            RAC_ongoingRxCollided = false;
            RAC_previous1RadioState = RAC_RadioState.Off;
            RAC_previous2RadioState = RAC_RadioState.Off;
            RAC_previous3RadioState = RAC_RadioState.Off;
            RAC_paOutputLevelRamping = false;
            RAC_seqTimerLimit = 0xFFFF;

            // Reset PROTIMER variables
            PROTIMER_preCounterSourcedBitmask = 0;
            PROTIMER_baseCounterValue = 0;
            PROTIMER_wrapCounterValue = 0;
            PROTIMER_rtcWait = false;
            PROTIMER_txEnable = false;
            PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Idle;
            PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
            PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Idle;
            PROTIMER_listenBeforeTalkPending = false;

            // Reset MODEM variables
            MODEM_txRampingDoneInterrupt = true;

            // Reset AGC variables
            AGC_rssiStartCommandOngoing = false;
            AGC_rssiStartCommandFromProtimer = false;
            AGC_rssiLastRead = AGC_RssiInvalid;
            AGC_rssiSecondLastRead = AGC_RssiInvalid;

            // Disable all timers
            seqTimer.Enabled = false;
            paRampingTimer.Enabled = false;
            rssiUpdateTimer.Enabled = false;
            proTimer.Enabled = false;
            txTimer.Enabled = false;
            rxTimer.Enabled = false;

            // Reset the sequencer and put it back in halted state.
            sequencer.CpuWaitSignal.Set(true);
            sequencer.Reset();

            // TODO: clean up interference queue?

            RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.Reset);

            // Reset registers
            frameControllerRegistersCollection.Reset();
            cyclicRedundancyCheckRegistersCollection.Reset();
            synthesizerRegistersCollection.Reset();
            radioControllerRegistersCollection.Reset();
            protocolTimerRegistersCollection.Reset();
            modulatorAndDemodulatorRegistersCollection.Reset();
            automaticGainControlRegistersCollection.Reset();
            hostMailboxRegistersCollection.Reset();
            radioMailboxRegistersCollection.Reset();

            UpdateInterrupts();
        }

        public void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                FRC_CheckPacketCaptureBufferThreshold();

                //-------------------------------
                // Main core interrupts
                //-------------------------------

                // RAC_RSM interrupts
                var irq = ((RAC_radioStateChangeInterrupt.Value && RAC_radioStateChangeInterruptEnable.Value)
                           || (RAC_stimerCompareEventInterrupt.Value && RAC_stimerCompareEventInterruptEnable.Value));
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: IRQ RAC_RSM set (IF=0x{1:X}, IEN=0x{2:X})",
                             this.GetTime(),
                             (uint)(RAC_radioStateChangeInterrupt.Value
                                    | RAC_stimerCompareEventInterrupt.Value ? 0x2 : 0),
                             (uint)(RAC_radioStateChangeInterruptEnable.Value
                                    | RAC_stimerCompareEventInterruptEnable.Value ? 0x2 : 0));
                }
                RadioControllerRadioStateMachineIRQ.Set(irq);

                irq = ((RAC_mainCoreSeqInterrupts.Value & RAC_mainCoreSeqInterruptsEnable.Value) > 0);
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: IRQ RAC_SEQ set (IF=0x{1:X}, IEN=0x{2:X})",
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
                for(var channel = 0; channel < FRC_NumberOfWordCounterCompareChannels; ++channel)
                {
                    irq |= (FRC_wordCounterCompareInterrupt[channel].Value && FRC_wordCounterCompareInterruptEnable[channel].Value);
                }
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.InterruptFlags, out interruptFlag);
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: IRQ FRC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                FrameControllerIRQ.Set(irq);

                // MODEM interrupt
                irq = ((MODEM_txFrameSentInterrupt.Value && MODEM_txFrameSentInterruptEnable.Value)
                       || (MODEM_txSyncSentInterrupt.Value && MODEM_txSyncSentInterruptEnable.Value)
                       || (MODEM_txPreambleSentInterrupt.Value && MODEM_txPreambleSentInterruptEnable.Value)
                       || (MODEM_TxRampingDoneInterrupt && MODEM_txRampingDoneInterruptEnable.Value)
                       || (MODEM_rxPreambleDetectedInterrupt.Value && MODEM_rxPreambleDetectedInterruptEnable.Value)
                       || (MODEM_rxFrameWithSyncWord0DetectedInterrupt.Value && MODEM_rxFrameWithSyncWord0DetectedInterruptEnable.Value)
                       || (MODEM_rxFrameWithSyncWord1DetectedInterrupt.Value && MODEM_rxFrameWithSyncWord1DetectedInterruptEnable.Value)
                       || (MODEM_rxPreambleLostInterrupt.Value && MODEM_rxPreambleLostInterruptEnable.Value));
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.InterruptFlags, out interruptFlag);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: IRQ MODEM set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                ModulatorAndDemodulatorIRQ.Set(irq);

                // PROTIMER interrupt
                irq = (PROTIMER_preCounterOverflowInterrupt.Value && PROTIMER_preCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_baseCounterOverflowInterrupt.Value && PROTIMER_baseCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_wrapCounterOverflowInterrupt.Value && PROTIMER_wrapCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkSuccessInterrupt.Value && PROTIMER_listenBeforeTalkSuccessInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkFailureInterrupt.Value && PROTIMER_listenBeforeTalkFailureInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkRetryInterrupt.Value && PROTIMER_listenBeforeTalkRetryInterruptEnable.Value)
                       || (PROTIMER_listenBeforeTalkTimeoutCounterMatchInterrupt.Value && PROTIMER_listenBeforeTalkTimeoutCounterMatchInterruptEnable.Value)
                       || (PROTIMER_rtccSynchedInterrupt.Value && PROTIMER_rtccSynchedInterruptEnable.Value);
                Array.ForEach(PROTIMER_timeoutCounter, x => irq |= x.Interrupt);
                Array.ForEach(PROTIMER_captureCompareChannel, x => irq |= x.Interrupt);
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.InterruptFlags, out interruptFlag);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: IRQ PROTIMER set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                ProtocolTimerIRQ.Set(irq);

                // AGC interrupt
                irq = ((AGC_rssiValidInterrupt.Value && AGC_rssiValidInterruptEnable.Value)
                       || (AGC_ccaInterrupt.Value && AGC_ccaInterruptEnable.Value)
                       || (AGC_rssiDoneInterrupt.Value && AGC_rssiDoneInterruptEnable.Value)
                       || (AGC_rssiPositiveStepInterrupt.Value && AGC_rssiPositiveStepInterruptEnable.Value)
                       || (AGC_rssiNegativeStepInterrupt.Value && AGC_rssiNegativeStepInterruptEnable.Value));
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.InterruptFlags, out interruptFlag);
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: IRQ AGC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                AutomaticGainControlIRQ.Set(irq);

                // HOSTMAILBOX interrupt
                int index;
                irq = false;
                for(index = 0; index < MailboxMessageNumber; index++)
                {
                    if(HOSTMAILBOX_messageInterrupt[index].Value && HOSTMAILBOX_messageInterruptEnable[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    hostMailboxRegistersCollection.TryRead((long)HostMailboxRegisters.InterruptFlags, out interruptFlag);
                    hostMailboxRegistersCollection.TryRead((long)HostMailboxRegisters.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: IRQ HOSTMAILBOX set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                HostMailboxIRQ.Set(irq);

                //-------------------------------
                // Sequencer interrupts
                //-------------------------------

                // RAC interrupt
                irq = ((RAC_seqRadioStateChangeInterrupt.Value && RAC_seqRadioStateChangeInterruptEnable.Value)
                       || (RAC_seqStimerCompareEventInterrupt.Value && RAC_seqStimerCompareEventInterruptEnable.Value));
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RAC set (IF=0x{1:X}, IEN=0x{2:X})",
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
                for(var channel = 0; channel < FRC_NumberOfWordCounterCompareChannels; ++channel)
                {
                    irq |= (FRC_seqWordCounterCompareInterrupt[channel].Value && FRC_seqWordCounterCompareInterruptEnable[channel].Value);
                }
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.SequencerInterruptFlags, out interruptFlag);
                    frameControllerRegistersCollection.TryRead((long)FrameControllerRegisters.SequencerInterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ FRC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                SeqFrameControllerIRQ.Set(irq);

                // MODEM interrupt
                irq = ((MODEM_seqTxFrameSentInterrupt.Value && MODEM_seqTxFrameSentInterruptEnable.Value)
                       || (MODEM_seqTxSyncSentInterrupt.Value && MODEM_seqTxSyncSentInterruptEnable.Value)
                       || (MODEM_seqTxPreambleSentInterrupt.Value && MODEM_seqTxPreambleSentInterruptEnable.Value)
                       || (MODEM_TxRampingDoneInterrupt && MODEM_seqTxRampingDoneInterruptEnable.Value)
                       || (MODEM_seqRxPreambleDetectedInterrupt.Value && MODEM_seqRxPreambleDetectedInterruptEnable.Value)
                       || (MODEM_seqRxFrameWithSyncWord0DetectedInterrupt.Value && MODEM_seqRxFrameWithSyncWord0DetectedInterruptEnable.Value)
                       || (MODEM_seqRxFrameWithSyncWord1DetectedInterrupt.Value && MODEM_seqRxFrameWithSyncWord1DetectedInterruptEnable.Value)
                       || (MODEM_seqRxPreambleLostInterrupt.Value && MODEM_seqRxPreambleLostInterruptEnable.Value));
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.SequencerInterruptFlags, out interruptFlag);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)ModulatorAndDemodulatorRegisters.SequencerInterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ MODEM set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                SeqModulatorAndDemodulatorIRQ.Set(irq);

                // PROTIMER interrupt
                irq = (PROTIMER_seqPreCounterOverflowInterrupt.Value && PROTIMER_seqPreCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_seqBaseCounterOverflowInterrupt.Value && PROTIMER_seqBaseCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_seqWrapCounterOverflowInterrupt.Value && PROTIMER_seqWrapCounterOverflowInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkSuccessInterrupt.Value && PROTIMER_seqListenBeforeTalkSuccessInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkFailureInterrupt.Value && PROTIMER_seqListenBeforeTalkFailureInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkRetryInterrupt.Value && PROTIMER_seqListenBeforeTalkRetryInterruptEnable.Value)
                       || (PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterrupt.Value && PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterruptEnable.Value)
                       || (PROTIMER_seqRtccSynchedInterrupt.Value && PROTIMER_seqRtccSynchedInterruptEnable.Value);
                Array.ForEach(PROTIMER_timeoutCounter, x => irq |= x.SeqInterrupt);
                Array.ForEach(PROTIMER_captureCompareChannel, x => irq |= x.SeqInterrupt);
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.SequencerInterruptFlags, out interruptFlag);
                    protocolTimerRegistersCollection.TryRead((long)ProtocolTimerRegisters.SequencerInterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ PROTIMER set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                SeqProtocolTimerIRQ.Set(irq);

                // AGC interrupt
                irq = ((AGC_seqRssiValidInterrupt.Value && AGC_seqRssiValidInterruptEnable.Value)
                       || (AGC_seqCcaInterrupt.Value && AGC_seqCcaInterruptEnable.Value)
                       || (AGC_seqRssiDoneInterrupt.Value && AGC_seqRssiDoneInterruptEnable.Value)
                       || (AGC_seqRssiPositiveStepInterrupt.Value && AGC_seqRssiPositiveStepInterruptEnable.Value)
                       || (AGC_seqRssiNegativeStepInterrupt.Value && AGC_seqRssiNegativeStepInterruptEnable.Value));
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.SequencerInterruptFlags, out interruptFlag);
                    automaticGainControlRegistersCollection.TryRead((long)AutomaticGainControlRegisters.SequencerInterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ AGC set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                SeqAutomaticGainControlIRQ.Set(irq);

                // RFMAILBOX interrupt
                irq = false;
                for(index = 0; index < MailboxMessageNumber; index++)
                {
                    if(RFMAILBOX_messageInterrupt[index].Value && RFMAILBOX_messageInterruptEnable[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if(irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    radioMailboxRegistersCollection.TryRead((long)RadioMailboxRegisters.InterruptFlags, out interruptFlag);
                    radioMailboxRegistersCollection.TryRead((long)RadioMailboxRegisters.InterruptEnable, out interruptEnable);
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RFMAILBOX set (IF=0x{1:X}, IEN=0x{2:X})", this.GetTime(), interruptFlag, interruptEnable);
                }
                SeqRfMailboxIRQ.Set(irq);

                // SW0 interrupt
                irq = ((RAC_softwareIrq.Value & 0x1) > 0);
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ SW0 set", this.GetTime());
                }
                SeqSoftware0IRQ.Set(irq);

                // SW1 interrupt
                irq = ((RAC_softwareIrq.Value & 0x2) > 0);
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ SW1 set", this.GetTime());
                }
                SeqSoftware1IRQ.Set(irq);

                // Sequencer Radio State Machine interrupts
                irq = RAC_seqStateOffInterrupt.Value && RAC_seqStateOffInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ OFF set", this.GetTime());
                }
                SeqOffIRQ.Set(irq);

                irq = RAC_seqStateRxWarmInterrupt.Value && RAC_seqStateRxWarmInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RX_WARM set", this.GetTime());
                }
                SeqRxWarmIRQ.Set(irq);

                irq = RAC_seqStateRxSearchInterrupt.Value && RAC_seqStateRxSearchInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RX_SEARCH set", this.GetTime());
                }
                SeqRxSearchIRQ.Set(irq);

                irq = RAC_seqStateRxFrameInterrupt.Value && RAC_seqStateRxFrameInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RX_FRAME set", this.GetTime());
                }
                SeqRxFrameIRQ.Set(irq);

                irq = RAC_seqStateRxPoweringDownInterrupt.Value && RAC_seqStateRxPoweringDownInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RX_PD set", this.GetTime());
                }
                SeqRxPoweringDownIRQ.Set(irq);

                irq = RAC_seqStateRx2RxInterrupt.Value && RAC_seqStateRx2RxInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RX_2_RX set", this.GetTime());
                }
                SeqRx2RxIRQ.Set(irq);

                irq = RAC_seqStateRxOverflowInterrupt.Value && RAC_seqStateRxOverflowInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RX_OVERFLOW set", this.GetTime());
                }
                SeqRxOverflowIRQ.Set(irq);

                irq = RAC_seqStateRx2TxInterrupt.Value && RAC_seqStateRx2TxInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ RX_2_TX set", this.GetTime());
                }
                SeqRx2TxIRQ.Set(irq);

                irq = RAC_seqStateTxWarmInterrupt.Value && RAC_seqStateTxWarmInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ TX_WARM set", this.GetTime());
                }
                SeqTxWarmIRQ.Set(irq);

                irq = RAC_seqStateTxInterrupt.Value && RAC_seqStateTxInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ TX set", this.GetTime());
                }
                SeqTxIRQ.Set(irq);

                irq = RAC_seqStateTxPoweringDownInterrupt.Value && RAC_seqStateTxPoweringDownInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ TX_PD set", this.GetTime());
                }
                SeqTxPoweringDownIRQ.Set(irq);

                irq = RAC_seqStateTx2RxInterrupt.Value && RAC_seqStateTx2RxInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ TX_2_RX set", this.GetTime());
                }
                SeqTx2RxIRQ.Set(irq);

                irq = RAC_seqStateTx2TxInterrupt.Value && RAC_seqStateTx2TxInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ TX_2_TX set", this.GetTime());
                }
                SeqTx2TxIRQ.Set(irq);

                irq = RAC_seqStateShutDownInterrupt.Value && RAC_seqStateShutDownInterruptEnable.Value;
                if(irq)
                {
                    this.Log(LogLevel.Debug, "{0}: SEQ_IRQ SHUTDOWN set", this.GetTime());
                }
                SeqShutdownIRQ.Set(irq);
            });
        }

        public void InteferenceQueueChangedCallback()
        {
            if(RAC_currentRadioState == RAC_RadioState.RxSearch || RAC_currentRadioState == RAC_RadioState.RxFrame)
            {
                AGC_UpdateRssi();
            }
        }

        public UInt64 PacketTraceRadioTimestamp()
        {
            TrySyncTime();
            return (UInt64)GetTime().TotalMicroseconds * 1000;
        }

        public void ReceiveFrame(byte[] frame, IRadio sender)
        {
            var txStartTime = InterferenceQueue.GetTxStartTime(sender);

            if(txStartTime == TimeInterval.Empty)
            {
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "ReceiveFrame() at {0} on channel {1} ({2}) ({3}): Dropping (TX was aborted)",
                         GetTime(), LogicalChannel, MODEM_GetPhySignature(), SYNTH_GetRfFrequencyString());
                return;
            }

            TrySyncTime();
            var txRxSimulatorDelayUs = (GetTime() - txStartTime).TotalMicroseconds;

            this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "ReceiveFrame() at {0} on channel {1} ({2}) ({3}), TX started at {4} (diff: {5})",
                     GetTime(), LogicalChannel, MODEM_GetPhySignature(), SYNTH_GetRfFrequencyString(), txStartTime, txRxSimulatorDelayUs);

            // If we are already receiving a frame, this co-channel transmission interferes with the
            // ongoing reception. Energy-based: any co-channel signal corrupts it, regardless of whether
            // either frame is decode-compatible. We mark the ongoing RX as collided and drop the newcomer.
            if(RAC_internalRxState != RAC_InternalRxState.Idle)
            {
                // TODO: for now we always consider this a collision. In the future we will want to take into account
                // the RSSI of both packets and determine if we could hear one of them.
                // The ongoing RX is marked as "interfered", which can result in either a preamble not heard
                // at all or a frame that fails CRC.
                RAC_ongoingRxCollided = true;
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping: (RX already ongoing - collision)");
                return;
            }

            // The txStartTime guard above already returned if the sender had no over-the-air entry, so a
            // missing signature here is an internal invariant violation rather than a normal drop.
            if(!InterferenceQueue.TryGetTxSignature(sender, out var senderSignature))
            {
                throw new Antmicro.Renode.Exceptions.RecoverableException(
                    "ReceiveFrame: no TX signature found for sender although a TX start time was resolved");
            }

            // No reception is in progress. Drop frames whose PHY we cannot decode (the medium only matched
            // carrier frequency). With no RX to corrupt there is nothing to flag as a collision; we simply
            // cannot lock onto a PHY we are not configured for. The sync word is still checked later by the
            // RX pipeline for decode-compatible frames.
            if(!MODEM_GetPhySignature().IsDecodeCompatibleWith(senderSignature))
            {
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping: (incompatible PHY: sender={0}, local={1})",
                         senderSignature, MODEM_GetPhySignature());
                return;
            }

            // Adopt the sender's coding sub-rate for the whole reception so our modelled airtime matches what
            // the transmitter actually spent on air.
            currentFrameCodedSymbolsPerInfoBit = senderSignature.CodedSymbolsPerInfoBit;

            // Ideally we would schedule a first RX step that accounts for the txChainDelay and some portion of the preamble
            // to still allow reception of frames for which we only heard some portion of the preamble.
            // For simplicity, we just skip this step and check the radio state after both preamble and sync word have been received.

            // We first schedule the RX timer to the point that the preamble and sych word are fully transmitted.
            // That must also include the txChainDelay since at the transmitter we don't delay the FrameTxStart event.
            // The RX state is set before computing the delay so the coded airtime uses the sender's sub-rate
            // (see MODEM_GetCodedSymbolsPerInfoBit's RX override).
            RAC_internalRxState = RAC_InternalRxState.PreambleAndSyncWord;
            double delayUs = MODEM_GetTxChainDelayUs() + MODEM_GetPreambleOverTheAirTimeUs() + MODEM_GetSyncWordOverTheAirTimeUs();
            MODEM_demodulatorState.Value = MODEM_DemodulatorState.FrameSearch;

            currentFrame = frame;

            // We set the RSSIFRAME here to make sure the sender hasn't removed the packet from the interference queue
            // by the time the receiver has completed its RX delay.
            AGC_FrameRssiIntegerPart = (sbyte)InterferenceQueue.GetCurrentRssi(this, Channel);

            // If another transmission is already on air on our channel as we begin this reception, the two
            // overlap in time: mark the reception as collided from the start (energy-based interference).
            if(InterferenceQueue.AnyOtherOnAir(sender, Channel))
            {
                RAC_ongoingRxCollided = true;
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "RX starting with co-channel interference already on air -> collision");
            }

            if(delayUs > txRxSimulatorDelayUs && PROTIMER_UsToPreCntOverflowTicks(delayUs - txRxSimulatorDelayUs) > 0)
            {
                currentFrameElapsedAirtimeUs = 0;
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
                currentFrameElapsedAirtimeUs = txRxSimulatorDelayUs - delayUs;
                RAC_RxTimerLimitReached();
            }
            FRC_SnifferReceiveFrame(frame);
        }

        public void PROTIMER_PrsRtcTrigger()
        {
            // Ignore the trigger if we are not in the RTCWAIT state
            if(PROTIMER_rtcWait && PROTIMER_rtccTriggerFromPrsEnable.Value)
            {
                this.Log(LogLevel.Debug, "PROTIMER RTC trigger at {0}, restarting PROTIMER", GetTime());
                PROTIMER_rtccSynchedInterrupt.Value = true;
                PROTIMER_seqRtccSynchedInterrupt.Value = true;
                PROTIMER_rtcWait = false;
                PROTIMER_Enabled = true;
                UpdateInterrupts();
            }
        }

        public void PROTIMER_PrsLbtStart()
        {
            if(PROTIMER_listenBeforeTalkPrsStartEnable.Value)
            {
                this.Log(LogLevel.Debug, "PROTIMER PRS LBT start at {0}", GetTime());
                PROTIMER_ListenBeforeTalkStartCommand();
            }
        }

        public void PROTIMER_PrsLbtStop()
        {
            if(PROTIMER_listenBeforeTalkPrsStopEnable.Value)
            {
                this.Log(LogLevel.Debug, "PROTIMER PRS LBT stop at {0}", GetTime());
                PROTIMER_ListenBeforeTalkStopCommand();
            }
        }

        public void PROTIMER_PrsLbtPause()
        {
            if(PROTIMER_listenBeforeTalkPrsPauseEnable.Value)
            {
                this.Log(LogLevel.Debug, "PROTIMER PRS LBT pause at {0}", GetTime());
                PROTIMER_ListenBeforeTalkPauseCommand();
            }
        }

        public void PROTIMER_TriggerEvent(PROTIMER_Event ev)
        {
            if(ev < PROTIMER_Event.PreCounterOverflow || ev > PROTIMER_Event.InternalTrigger)
            {
                this.Log(LogLevel.Error, "Unreachable. Invalid event value for PROTIMER_TriggerEvent.");
                return;
            }

            if(ev >= PROTIMER_Event.CaptureCompareChannel0Event && ev <= PROTIMER_Event.CaptureCompareChannel4Event)
            {
                uint cc = (uint)ev - (uint)PROTIMER_Event.CaptureCompareChannel0Event;
                this.Log(LogLevel.Noisy, "PROTIMER Triggering PRS Capture Compare {0} event at {1}", cc, GetTime());
                PROTIMER_CaptureComparePrsEvent[cc]?.Invoke();
            }

            // if a Timeout Counter 0 match occurs during LBT, we change the event accordingly.
            if(ev == PROTIMER_Event.TimeoutCounter0Match && PROTIMER_listenBeforeTalkRunning.Value)
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
                if(PROTIMER_rxSetEvent1.Value == PROTIMER_Event.Always)
                {
                    PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                    goto case PROTIMER_TxRxRequestState.SetEvent1;
                }
                if(PROTIMER_rxSetEvent1.Value == ev)
                {
                    PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                }
                break;
            }
            case PROTIMER_TxRxRequestState.SetEvent1:
            {
                if(PROTIMER_rxSetEvent2.Value == PROTIMER_Event.Always || PROTIMER_rxSetEvent2.Value == ev)
                {
                    PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Set;
                    RAC_UpdateRadioStateMachine();
                }
                break;
            }
            case PROTIMER_TxRxRequestState.Set:
            {
                if(PROTIMER_rxClearEvent1.Value == PROTIMER_Event.Always)
                {
                    PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.ClearEvent1;
                    goto case PROTIMER_TxRxRequestState.ClearEvent1;
                }
                if(PROTIMER_rxClearEvent1.Value == ev)
                {
                    PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.ClearEvent1;
                }
                break;
            }
            case PROTIMER_TxRxRequestState.ClearEvent1:
            {
                if(PROTIMER_rxClearEvent2.Value == ev)
                {
                    PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
                    RAC_UpdateRadioStateMachine();
                }
                break;
            }
            default:
                this.Log(LogLevel.Error, "Unreachable. Invalid PROTIMER RX Request state.");
                return;
            }

            switch(PROTIMER_txRequestState)
            {
            case PROTIMER_TxRxRequestState.Idle:
            {
                if(PROTIMER_txSetEvent1.Value == PROTIMER_Event.Always)
                {
                    PROTIMER_txRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                    goto case PROTIMER_TxRxRequestState.SetEvent1;
                }
                if(PROTIMER_txSetEvent1.Value == ev)
                {
                    PROTIMER_txRequestState = PROTIMER_TxRxRequestState.SetEvent1;
                }
                break;
            }
            case PROTIMER_TxRxRequestState.SetEvent1:
            {
                if(PROTIMER_txSetEvent2.Value == ev)
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
                this.Log(LogLevel.Error, "Unreachable. Invalid PROTIMER TX Request state.");
                return;
            }
        }

        public void PROTIMER_HandleChangedParams()
        {
            // Timer is not running, nothing to do
            if(!PROTIMER_Enabled)
            {
                return;
            }

            TrySyncTime();
            uint currentIncrement = (uint)proTimer.Value;
            proTimer.Enabled = false;

            // First handle the current increment
            if(currentIncrement > 0)
            {
                PROTIMER_HandlePreCntOverflows(currentIncrement);
            }

            // Then restart the protimer
            proTimer.Value = 0;
            proTimer.Limit = PROTIMER_ComputeTimerLimit();
            proTimer.Enabled = true;
        }

        // Main core IRQs
        public GPIO FrameControllerPrioritizedIRQ { get; }

        public GPIO FrameControllerIRQ { get; }

        public GPIO ModulatorAndDemodulatorIRQ { get; }

        public GPIO RadioControllerSequencerIRQ { get; }

        public GPIO RadioControllerRadioStateMachineIRQ { get; }

        public GPIO ProtocolTimerIRQ { get; }

        public GPIO SynthesizerIRQ { get; }

        public GPIO AutomaticGainControlIRQ { get; }

        public GPIO HostMailboxIRQ { get; }

        // Sequencer core IRQs
        public GPIO SeqOffIRQ { get; }

        public GPIO SeqRxWarmIRQ { get; }

        public GPIO SeqRxSearchIRQ { get; }

        public GPIO SeqRxFrameIRQ { get; }

        public GPIO SeqRxPoweringDownIRQ { get; }

        public GPIO SeqRx2RxIRQ { get; }

        public GPIO SeqRxOverflowIRQ { get; }

        public GPIO SeqRx2TxIRQ { get; }

        public GPIO SeqTxWarmIRQ { get; }

        public GPIO SeqTxIRQ { get; }

        public GPIO SeqTxPoweringDownIRQ { get; }

        public GPIO SeqTx2RxIRQ { get; }

        public GPIO SeqTx2TxIRQ { get; }

        public GPIO SeqShutdownIRQ { get; }

        public GPIO SeqRadioControllerIRQ { get; }

        public GPIO SeqFrameControllerIRQ { get; }

        public GPIO SeqFrameControllerPriorityIRQ { get; }

        public GPIO SeqModulatorAndDemodulatorIRQ { get; }

        public GPIO SeqAutomaticGainControlIRQ { get; }

        public GPIO SeqProtocolTimerIRQ { get; }

        public GPIO SeqSynthesizerIRQ { get; }

        public GPIO SeqRfMailboxIRQ { get; }

        public GPIO SeqSoftware0IRQ { get; }

        public GPIO SeqSoftware1IRQ { get; }

        // Routing equality key used by WirelessMedium and InterferenceQueue. Derived from
        // the LPW SYNTH/RAC registers (FCW + LO divider) and quantized to 10 kHz so that
        // two radios tuned to the same RF report the same Channel id, even if the FCW
        // is rounded slightly differently.
        public int Channel
        {
            get { return (int)(SYNTH_GetQuantizedRfFrequency() / 10_000UL); }

            set { }
        }

        public bool ForceBusyRssi
        {
            set
            {
                InterferenceQueue.ForceBusyRssi = value;
            }
        }

        public uint PROTIMER_WrapCounterValue
        {
            get
            {
                ulong ret = PROTIMER_wrapCounterValue;
                if(PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.PreCounterOverflow
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

        public ushort PROTIMER_BaseCounterValue
        {
            get
            {
                ulong ret = PROTIMER_baseCounterValue;
                if(PROTIMER_baseCounterSource.Value == PROTIMER_BaseCounterSource.PreCounterOverflow
                    && proTimer.Enabled)
                {
                    TrySyncTime();
                    ret += proTimer.Value;
                }
                return (ushort)ret;
            }

            set
            {
                PROTIMER_baseCounterValue = value;
            }
        }

        public event Action<IRadio, byte[]> FrameSent;

        public event Action<byte[]> PtiDataOut;

        public event Action<SiLabs_PacketTraceFrameType> PtiFrameStart;

        public event Action PtiFrameComplete;

        public Action[] PROTIMER_CaptureComparePrsEvent = new Action[PROTIMER_NumberOfCaptureCompareChannelPrsEvents];

        public bool LogBasicRadioActivityAsError = false;

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private bool SequencerIsRunning()
        {
            return (SeqOffIRQ.IsSet || SeqRxWarmIRQ.IsSet || SeqRxSearchIRQ.IsSet || SeqRxFrameIRQ.IsSet
                    || SeqRxPoweringDownIRQ.IsSet || SeqRx2RxIRQ.IsSet || SeqRxOverflowIRQ.IsSet || SeqRx2TxIRQ.IsSet
                    || SeqTxWarmIRQ.IsSet || SeqTxIRQ.IsSet || SeqTxPoweringDownIRQ.IsSet || SeqTx2RxIRQ.IsSet
                    || SeqTx2TxIRQ.IsSet || SeqShutdownIRQ.IsSet);
        }

        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        private void Write<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, uint value)
        where T : struct, IComparable, IFormattable
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var internal_offset = offset;
                var internal_value = value;

                if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    var old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value | value;
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                }
                else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    var old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value & ~value;
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                }
                else if(offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    var old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value ^ value;
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                }

                this.Log(LogLevel.Debug, "{0}: Write to {1} at offset 0x{2:X} ({3}), value 0x{4:X}",
                        this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);

                if(!registersCollection.TryWrite(internal_offset, internal_value))
                {
                    this.Log(LogLevel.Warning, "Unhandled write to {0} at offset 0x{1:X} ({2}), value 0x{3:X}.", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                    return;
                }
            });
        }

        private byte ReadByte<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset)
        where T : struct, IComparable, IFormattable
        {
            var byteOffset = (int)(offset & 0x3);
            // TODO: single byte reads are treated as internal reads for now to avoid flooding the log during debugging.
            var registerValue = Read<T>(registersCollection, regionName, offset - byteOffset, true);
            var result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
        }

        private DoubleWordRegisterCollection BuildSynthesizerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)SynthesizerRegisters.Frequency, new DoubleWordRegister(this)
                    .WithValueField(0, 28, out SYNTH_frequency, name: "FREQ")
                    .WithReservedBits(28, 4)
                },
                {(long)SynthesizerRegisters.FrequencyDivisionControl, new DoubleWordRegister(this, 0x001)
                    .WithEnumField<DoubleWordRegister, SYNTH_LoDivFreqCtrl>(0, 9, out SYNTH_loDivFreqCtrl, name: "LODIVFREQCTRL")
                    .WithReservedBits(9, 23)
                },
                // RAIL writes the protocol's logical channel index here (e.g. 11..26 for 802.15.4).
                // The simulator exposes that value via LogicalChannel for human-readable log strings.
                {(long)SynthesizerRegisters.ChannelSpacing, new DoubleWordRegister(this)
                    .WithValueField(0, 18, out SYNTH_channelSpacing, name: "CHSP")
                    .WithReservedBits(18, 14)
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private uint Read<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, bool internal_read = false)
        where T : struct, IComparable, IFormattable
        {
            var result = 0U;
            var internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }
            else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }
            else if(offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if(!internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }

            if(!registersCollection.TryRead(internal_offset, out result))
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Warning, "Unhandled read from {0} at offset 0x{1:X} ({2}).", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"));
                }
            }
            else
            {
                if(!internal_read)
                {
                    this.Log(LogLevel.Debug, "{0}: Read from {1} at offset 0x{2:X} ({3}), returned 0x{4:X}",
                             this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), result);
                }
            }

            return result;
        }

        private DoubleWordRegisterCollection BuildHostMailboxRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)HostMailboxRegisters.MessagePointer0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTMAILBOX_messagePointer[0], name: "MSGPTR0")
                },
                {(long)HostMailboxRegisters.MessagePointer1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTMAILBOX_messagePointer[1], name: "MSGPTR1")
                },
                {(long)HostMailboxRegisters.MessagePointer2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTMAILBOX_messagePointer[2], name: "MSGPTR2")
                },
                {(long)HostMailboxRegisters.MessagePointer3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out HOSTMAILBOX_messagePointer[3], name: "MSGPTR3")
                },
                {(long)HostMailboxRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out HOSTMAILBOX_messageInterrupt[0], name: "MBOXIF0")
                    .WithFlag(1, out HOSTMAILBOX_messageInterrupt[1], name: "MBOXIF1")
                    .WithFlag(2, out HOSTMAILBOX_messageInterrupt[2], name: "MBOXIF2")
                    .WithFlag(3, out HOSTMAILBOX_messageInterrupt[3], name: "MBOXIF3")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)HostMailboxRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out HOSTMAILBOX_messageInterruptEnable[0], name: "MBOXIEN0")
                    .WithFlag(1, out HOSTMAILBOX_messageInterruptEnable[1], name: "MBOXIEN1")
                    .WithFlag(2, out HOSTMAILBOX_messageInterruptEnable[2], name: "MBOXIEN2")
                    .WithFlag(3, out HOSTMAILBOX_messageInterruptEnable[3], name: "MBOXIEN3")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildAutomaticGainControlRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)AutomaticGainControlRegisters.Status0, new DoubleWordRegister(this)
                    .WithValueField(0, 6, name: "GAININDEX")
                    .WithFlag(6, name: "RFPKDLOWLAT")
                    .WithFlag(7, name: "IFPKDLOLAT")
                    .WithFlag(8, name: "IFPKDHILAT")
                    .WithFlag(9, out AGC_cca, FieldMode.Read, name: "CCA")
                    .WithFlag(10, name: "GAINOK")
                    .WithValueField(11, 4, name: "PGAINDEX")
                    .WithValueField(15, 4, name: "LNAINDEX")
                    .WithValueField(19, 5, name: "PNIINDEX")
                    .WithValueField(24, 2, name: "ADCINDEX")
                    .WithReservedBits(26, 6)
                },
                {(long)AutomaticGainControlRegisters.Status1, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "CHPWR")
                    .WithReservedBits(8, 1)
                    .WithValueField(9, 4, name: "FASTLOOPSTATE")
                    .WithValueField(13, 2, name: "CFLOOPSTATE")
                    .WithEnumField<DoubleWordRegister, AGC_RssiState>(15, 3, out AGC_rssiState, name: "RSSISTATE")
                    .WithReservedBits(18, 14)
                },
                {(long)AutomaticGainControlRegisters.InterruptFlags, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_rssiValidInterrupt, name: "RSSIVALIDIF")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_ccaInterrupt, name: "CCAIF")
                  .WithFlag(3, out AGC_rssiPositiveStepInterrupt, name: "RSSIPOSSTEPIF")
                  .WithFlag(4, out AGC_rssiNegativeStepInterrupt, name: "RSSIBEGSTEPIF")
                  .WithFlag(5, out AGC_rssiDoneInterrupt, name: "RSSIDONEIF")
                  .WithFlag(6, name: "SHORTRSSIPOSSTEPIF")
                  .WithReservedBits(7, 1)
                  .WithFlag(8, name: "RFPKDPRDDONEIF")
                  .WithFlag(9, name: "RFPKDCNTDONEIF")
                  .WithReservedBits(10, 22)
                },
                {(long)AutomaticGainControlRegisters.InterruptEnable, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_rssiValidInterruptEnable, name: "RSSIVALIDIEN")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_ccaInterruptEnable, name: "CCAIEN")
                  .WithFlag(3, out AGC_rssiPositiveStepInterruptEnable, name: "RSSIPOSSTEPIEN")
                  .WithFlag(4, out AGC_rssiNegativeStepInterruptEnable, name: "RSSIBEGSTEPIEN")
                  .WithFlag(5, out AGC_rssiDoneInterruptEnable, name: "RSSIDONEIEN")
                  .WithFlag(6, name: "SHORTRSSIPOSSTEPIEN")
                  .WithReservedBits(7, 1)
                  .WithFlag(8, name: "RFPKDPRDDONEIEN")
                  .WithFlag(9, name: "RFPKDCNTDONEIEN")
                  .WithReservedBits(10, 22)
                },
                {(long)AutomaticGainControlRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_seqRssiValidInterrupt, name: "RSSIVALIDSEQIF")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_seqCcaInterrupt, name: "CCASEQIF")
                  .WithFlag(3, out AGC_seqRssiPositiveStepInterrupt, name: "RSSIPOSSTEPSEQIF")
                  .WithFlag(4, out AGC_seqRssiNegativeStepInterrupt, name: "RSSIBEGSTEPSEQIF")
                  .WithFlag(5, out AGC_seqRssiDoneInterrupt, name: "RSSIDONESEQIF")
                  .WithFlag(6, name: "SHORTRSSIPOSSTEPSEQIF")
                  .WithReservedBits(7, 1)
                  .WithFlag(8, name: "RFPKDPRDDONESEQIF")
                  .WithFlag(9, name: "RFPKDCNTDONESEQIF")
                  .WithReservedBits(10, 22)
                },
                {(long)AutomaticGainControlRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                  .WithFlag(0, out AGC_seqRssiValidInterruptEnable, name: "RSSIVALIDSEQIEN")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out AGC_seqCcaInterruptEnable, name: "CCASEQIEN")
                  .WithFlag(3, out AGC_seqRssiPositiveStepInterruptEnable, name: "RSSIPOSSTEPSEQIEN")
                  .WithFlag(4, out AGC_seqRssiNegativeStepInterruptEnable, name: "RSSIBEGSTEPSEQIEN")
                  .WithFlag(5, out AGC_seqRssiDoneInterruptEnable, name: "RSSIDONESEQIEN")
                  .WithFlag(6, name: "SHORTRSSIPOSSTEPSEQIEN")
                  .WithReservedBits(7, 1)
                  .WithFlag(8, name: "RFPKDPRDDONESEQIEN")
                  .WithFlag(9, name: "RFPKDCNTDONESEQIEN")
                  .WithReservedBits(10, 22)
                },
                {(long)AutomaticGainControlRegisters.ListenBeforeTalkConfiguration, new DoubleWordRegister(this)
                  .WithValueField(0, 4, out AGC_ccaRssiPeriod, name: "CCARSSIPERIOD")
                  .WithFlag(4, out AGC_ccaRssiPeriodEnable, name: "ENCCARSSIPERIOD")
                  .WithFlag(5, name: "ENCCAGAINREDUCED")
                  .WithFlag(6, name: "ENCCARSSIMAX")
                  .WithReservedBits(7, 25)
                },
                {(long)AutomaticGainControlRegisters.Control0, new DoubleWordRegister(this, 0x2132727F)
                    .WithValueField(0, 8, name: "PWRTARGET")
                    .WithValueField(8, 3, name: "MODE")
                    .WithValueField(11, 8, out AGC_rssiShift, name: "RSSISHIFT")
                    .WithFlag(19, name: "DISCFLOOPADJ")
                    .WithReservedBits(20, 2)
                    .WithFlag(22, name: "DISRESETCHPWR")
                    .WithFlag(23, name: "ADCATTENMODE")
                    .WithReservedBits(24, 1)
                    .WithValueField(25, 2, name: "ADCATTENCODE")
                    .WithFlag(27, name: "ENRSSIRESET")
                    .WithFlag(28, name: "DSADISCFLOOP")
                    .WithFlag(29, name: "DISPNGAINUP")
                    .WithFlag(30, name: "DISPNDWNCOMP")
                    .WithFlag(31, name: "AGCRST")
                },
                {(long)AutomaticGainControlRegisters.Control1, new DoubleWordRegister(this, 0x00001300)
                  .WithValueField(0, 8, out AGC_ccaThreshold, name: "CCATHRSH")
                  .WithValueField(8, 4, out AGC_rssiMeasurePeriod, name: "RSSIPERIOD")
                  .WithValueField(12, 3, out AGC_powerMeasurePeriod, name: "PWRPERIOD")
                  .WithFlag(15, out AGC_subPeriod, name: "SUBPERIOD")
                  .WithValueField(16, 5, name: "SUBNUM")
                  .WithValueField(21, 5, name: "SUBDEN")
                  .WithValueField(26, 6, out AGC_subPeriodInteger, name: "SUBINT")
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
                { (long)AutomaticGainControlRegisters.ReceivedSignalStrengthIndicatorStepThreshold, new DoubleWordRegister(this)
                  .WithValueField(0, 8, out AGC_rssiPositiveStepThreshold, name: "POSSTEPTHR")
                  .WithValueField(8, 8, out AGC_rssiNegativeStepThreshold, name: "NEGSTEPTHR")
                  .WithFlag(16, out AGC_rssiStepPeriod, name: "STEPPER")
                  .WithValueField(17, 4, name: "DEMODRESTARTPER")
                  .WithValueField(21, 8, name: "DEMODRESTARTTHR")
                  .WithFlag(29, name: "RSSIFAST")
                  .WithReservedBits(30, 2)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildModulatorAndDemodulatorRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)ModulatorAndDemodulatorRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_txFrameSentInterrupt, name: "TXFRAMESENTIF")
                    .WithFlag(1, out MODEM_txSyncSentInterrupt, name: "TXSYNCSENTIF")
                    .WithFlag(2, out MODEM_txPreambleSentInterrupt, name: "TXPRESENTIF")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => MODEM_TxRampingDoneInterrupt, name: "TXRAMPDONEIF")
                    .WithFlag(4, name: "LDTNOARRIF")
                    .WithFlag(5, name: "PHDSADETIF")
                    .WithFlag(6, name: "PHYUNCODEDETIF")
                    .WithFlag(7, name: "PHYCODEDETIF")
                    .WithFlag(8, name: "RXTIMDETIF")
                    .WithFlag(9, out MODEM_rxPreambleDetectedInterrupt, name: "RXPREDETIF")
                    .WithFlag(10, out MODEM_rxFrameWithSyncWord0DetectedInterrupt, name: "RXFRAMEDET0IF")
                    .WithFlag(11, out MODEM_rxFrameWithSyncWord1DetectedInterrupt, name: "RXFRAMEDET1IF")
                    .WithFlag(12, name: "RXTIMLOSTIF")
                    .WithFlag(13, out MODEM_rxPreambleLostInterrupt, name: "RXPRELOSTIF")
                    .WithFlag(14, name: "RXFRAMEDETOFIF")
                    .WithFlag(15, name: "RXTIMNFIF")
                    .WithFlag(16, name: "FRCTIMOUTIF")
                    .WithFlag(17, name: "ETSIF")
                    .WithFlag(18, name: "CFGANTPATTRDIF")
                    .WithReservedBits(19, 12)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_txFrameSentInterruptEnable, name: "TXFRAMESENTIEN")
                    .WithFlag(1, out MODEM_txSyncSentInterruptEnable, name: "TXSYNCSENTIEN")
                    .WithFlag(2, out MODEM_txPreambleSentInterruptEnable, name: "TXPRESENTIEN")
                    .WithFlag(3, out MODEM_txRampingDoneInterruptEnable, name: "TXRAMPDONEIEN")
                    .WithFlag(4, name: "LDTNOARRIEN")
                    .WithFlag(5, name: "PHDSADETIEN")
                    .WithFlag(6, name: "PHYUNCODEDETIEN")
                    .WithFlag(7, name: "PHYCODEDETIEN")
                    .WithFlag(8, name: "RXTIMDETIEN")
                    .WithFlag(9, out MODEM_rxPreambleDetectedInterruptEnable, name: "RXPREDETIEN")
                    .WithFlag(10, out MODEM_rxFrameWithSyncWord0DetectedInterruptEnable, name: "RXFRAMEDET0IEN")
                    .WithFlag(11, out MODEM_rxFrameWithSyncWord1DetectedInterruptEnable, name: "RXFRAMEDET1IEN")
                    .WithFlag(12, name: "RXTIMLOSTIEN")
                    .WithFlag(13, out MODEM_rxPreambleLostInterruptEnable, name: "RXPRELOSTIEN")
                    .WithFlag(14, name: "RXFRAMEDETOFIEN")
                    .WithFlag(15, name: "RXTIMNFIEN")
                    .WithFlag(16, name: "FRCTIMOUTIEN")
                    .WithFlag(17, name: "ETSIEN")
                    .WithFlag(18, name: "CFGANTPATTRDIEN")
                    .WithReservedBits(19, 12)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_seqTxFrameSentInterrupt, name: "TXFRAMESENTSEQIF")
                    .WithFlag(1, out MODEM_seqTxSyncSentInterrupt, name: "TXSYNCSENTSEQIF")
                    .WithFlag(2, out MODEM_seqTxPreambleSentInterrupt, name: "TXPRESENTSEQIF")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => MODEM_TxRampingDoneInterrupt, name: "TXRAMPDONESEQIF")
                    .WithFlag(4, name: "LDTNOARRSEQIF")
                    .WithFlag(5, name: "PHDSADETSEQIF")
                    .WithFlag(6, name: "PHYUNCODEDETSEQIF")
                    .WithFlag(7, name: "PHYCODEDETSEQIF")
                    .WithFlag(8, name: "RXTIMDETSEQIF")
                    .WithFlag(9, out MODEM_seqRxPreambleDetectedInterrupt, name: "RXPREDETSEQIF")
                    .WithFlag(10, out MODEM_seqRxFrameWithSyncWord0DetectedInterrupt, name: "RXFRAMEDET0SEQIF")
                    .WithFlag(11, out MODEM_seqRxFrameWithSyncWord1DetectedInterrupt, name: "RXFRAMEDET1SEQIF")
                    .WithFlag(12, name: "RXTIMLOSTSEQIF")
                    .WithFlag(13, out MODEM_seqRxPreambleLostInterrupt, name: "RXPRELOSTSEQIF")
                    .WithFlag(14, name: "RXFRAMEDETOFSEQIF")
                    .WithFlag(15, name: "RXTIMNFSEQIF")
                    .WithFlag(16, name: "FRCTIMOUTSEQIF")
                    .WithFlag(17, name: "ETSSEQIF")
                    .WithFlag(18, name: "CFGANTPATTRDSEQIF")
                    .WithReservedBits(19, 12)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_seqTxFrameSentInterruptEnable, name: "TXFRAMESENTSEQIEN")
                    .WithFlag(1, out MODEM_seqTxSyncSentInterruptEnable, name: "TXSYNCSENTSEQIEN")
                    .WithFlag(2, out MODEM_seqTxPreambleSentInterruptEnable, name: "TXPRESENTSEQIEN")
                    .WithFlag(3, out MODEM_seqTxRampingDoneInterruptEnable, name: "TXRAMPDONESEQIEN")
                    .WithFlag(4, name: "LDTNOARRSEQIEN")
                    .WithFlag(5, name: "PHDSADETSEQIEN")
                    .WithFlag(6, name: "PHYUNCODEDETSEQIEN")
                    .WithFlag(7, name: "PHYCODEDETSEQIEN")
                    .WithFlag(8, name: "RXTIMDETSEQIEN")
                    .WithFlag(9, out MODEM_seqRxPreambleDetectedInterruptEnable, name: "RXPREDETSEQIEN")
                    .WithFlag(10, out MODEM_seqRxFrameWithSyncWord0DetectedInterruptEnable, name: "RXFRAMEDET0SEQIEN")
                    .WithFlag(11, out MODEM_seqRxFrameWithSyncWord1DetectedInterruptEnable, name: "RXFRAMEDET1SEQIEN")
                    .WithFlag(12, name: "RXTIMLOSTSEQIEN")
                    .WithFlag(13, out MODEM_seqRxPreambleLostInterruptEnable, name: "RXPRELOSTSEQIEN")
                    .WithFlag(14, name: "RXFRAMEDETOFSEQIEN")
                    .WithFlag(15, name: "RXTIMNFSEQIEN")
                    .WithFlag(16, name: "FRCTIMOUTSEQIEN")
                    .WithFlag(17, name: "ETSSEQIEN")
                    .WithFlag(18, name: "CFGANTPATTRDSEQIEN")
                    .WithReservedBits(19, 12)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ModulatorAndDemodulatorRegisters.Control0, new DoubleWordRegister(this)
                    .WithFlag(0, name: "FDM0DIFFDIS")
                    .WithValueField(1, 3, name: "MAPFSK")
                    .WithEnumField<DoubleWordRegister, MODEM_SymbolCoding>(4, 2, out MODEM_symbolCoding, name: "CODING")
                    .WithEnumField<DoubleWordRegister, MODEM_ModulationFormat>(6, 3, out MODEM_modulationFormat, name: "MODFORMAT")
                    .WithFlag(9, name: "DUALCORROPTDIS")
                    .WithFlag(10, name: "OOKASYNCPIN")
                    .WithValueField(11, 5, out MODEM_dsssLength, name: "DSSSLEN")
                    .WithValueField(16, 3, out MODEM_dsssShifts, name: "DSSSSHIFTS")
                    .WithEnumField<DoubleWordRegister, MODEM_DsssDoublingMode>(19, 2, out MODEM_dsssDoublingMode, name: "DSSSDOUBLE")
                    .WithFlag(21, name: "DETDIS")
                    .WithValueField(22, 3, name: "DIFFENCMODE")
                    .WithValueField(25, 2, name: "SHAPING")
                    .WithValueField(27, 3, name: "DEMODRAWDATASEL")
                    .WithValueField(30, 2, name: "FRAMEDETDEL")
                },
                {(long)ModulatorAndDemodulatorRegisters.Control1, new DoubleWordRegister(this)
                    .WithValueField(0, 5, out MODEM_syncBits, name: "SYNCBITS")
                    .WithValueField(5, 4, name: "SYNCERRORS")
                    .WithFlag(9, out MODEM_dualSync, name: "DUALSYNC")
                    .WithFlag(10, out MODEM_txSync, name: "TXSYNC")
                    .WithFlag(11, out MODEM_syncData, name: "SYNCDATA")
                    .WithFlag(12, name: "SYNC1INV")
                    .WithReservedBits(13, 1)
                    .WithValueField(14, 2, name: "COMPMODE")
                    .WithValueField(16, 4, name: "RESYNCPER")
                    .WithValueField(20, 2, name: "PHASEDEMOD")
                    .WithValueField(22, 3, name: "FREQOFFESTPER")
                    .WithValueField(25, 7, name: "FREQOFFESTLIM")
                },
                {(long)ModulatorAndDemodulatorRegisters.Control2, new DoubleWordRegister(this, 0x00001000)
                    .WithValueField(0, 8, name: "SQITHRESH")
                    .WithValueField(8, 1, name: "RXFRCDIS")
                    .WithValueField(9, 1, name: "RXPINMODE")
                    .WithValueField(10, 2, name: "TXPINMODE")
                    .WithValueField(12, 3, name: "DATAFILTER")
                    .WithValueField(15, 4, out MODEM_baudrateDivisionFactorA, name: "BRDIVA")
                    .WithValueField(19, 4, out MODEM_baudrateDivisionFactorB, name: "BRDIVB")
                    .WithValueField(23, 2, name: "DEVMULA")
                    .WithValueField(25, 2, name: "DEVMULB")
                    .WithEnumField<DoubleWordRegister, MODEM_RateSelectMode>(27, 2, out MODEM_rateSelectMode, name: "RATESELMODE")
                    .WithValueField(29, 1, name: "DEVWEIGHTDIS")
                    .WithValueField(30, 2, name: "DMASEL")
                },
                 {(long)ModulatorAndDemodulatorRegisters.TxBaudrate, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out MODEM_txBaudrateNumerator, name: "TXBRNUM")
                    .WithValueField(16, 8, out MODEM_txBaudrateDenominator, name: "TXBRDEN")
                    .WithReservedBits(24, 8)
                },
                {(long)ModulatorAndDemodulatorRegisters.Preamble, new DoubleWordRegister(this)
                    .WithValueField(0, 4, name: "BASE")
                    .WithValueField(4, 2, out MODEM_baseBits, name: "BASEBITS")
                    .WithFlag(6, name: "PRESYMB4FSK")
                    .WithValueField(7, 4, name: "PREERRORS")
                    .WithFlag(11, name: "DSSSPRE")
                    .WithFlag(12, name: "SYNCSYMB4FSK")
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 16, out MODEM_txBases, name: "TXBASES")
                },
                {(long)ModulatorAndDemodulatorRegisters.SyncWord0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out MODEM_syncWord0, name: "SYNC0")
                },
                {(long)ModulatorAndDemodulatorRegisters.SyncWord1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out MODEM_syncWord1, name: "SYNC1")
                },
                {(long)ModulatorAndDemodulatorRegisters.Command, new DoubleWordRegister(this)
                    .WithFlag(0, name: "PRESTOP")
                    .WithReservedBits(1, 2)
                    .WithFlag(3, name: "AFCTXLOCK")
                    .WithFlag(4, name: "AFCTXCLEAR")
                    .WithFlag(5, name: "AFCRXCLEAR")
                    .WithReservedBits(6, 26)
                },
                {(long)ModulatorAndDemodulatorRegisters.Status, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, MODEM_DemodulatorState>(0, 3, out MODEM_demodulatorState, FieldMode.Read, name: "DEMODSTATE")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out MODEM_frameDetectedId, FieldMode.Read, name: "FRAMEDETID")
                    .WithFlag(5, name: "ANTSEL")
                    .WithFlag(6, name: "TIMSEQINV")
                    .WithFlag(7, name: "TIMLOSTCAUSE")
                    .WithFlag(8, name: "DSADETECTED")
                    .WithFlag(9, name: "DSAFREQESTDONE")
                    .WithFlag(10, name: "VITERBIDEMODTIMDET")
                    .WithFlag(11, name: "VITERBIDEMODFRAMEDET")
                    .WithValueField(12, 3, name: "STAMPSTATE")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 8, name: "CORR")
                    .WithValueField(24, 8, name: "WEAKSYMBOLS")
                },
                {(long)ModulatorAndDemodulatorRegisters.Status2, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, name: "CHPWRACCUMUX")
                    .WithValueField(8, 4, FieldMode.Read, name: "BBSSMUX")
                    // (S=2 => 500k => 1, S=8 => 125k => 0)
                    .WithValueField(12, 2, FieldMode.Read, valueProviderCallback: _ => MODEM_GetCodedSymbolsPerInfoBit() == 2u ? 1u : 0u, name: "LRBLECI")
                    .WithFlag(14, FieldMode.Read, valueProviderCallback: _ => MODEM_symbolCoding.Value != MODEM_SymbolCoding.Linecode, name: "UNCODEDPHY")
                    .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => MODEM_symbolCoding.Value == MODEM_SymbolCoding.Linecode, name: "CODEDPHY")
                    .WithReservedBits(16, 2)
                    .WithValueField(18, 14, FieldMode.Read, name: "RTCOST")
                },
                {(long)ModulatorAndDemodulatorRegisters.BleLongRange, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "LONGRANGE")
                },
                {(long)ModulatorAndDemodulatorRegisters.RampingControl, new DoubleWordRegister(this)
                  .WithValueField(0, 4, out MODEM_rampRate0, name: "RAMPRATE0")
                  .WithValueField(4, 4, out MODEM_rampRate1, name: "RAMPRATE1")
                  .WithValueField(8, 4, out MODEM_rampRate2, name: "RAMPRATE2")
                  .WithReservedBits(12, 11)
                  .WithFlag(23, out MODEM_rampDisable, name: "RAMPDIS")
                  .WithValueField(24, 8, out MODEM_rampValue, name: "RAMPVAL")
                },
                {(long)ModulatorAndDemodulatorRegisters.ViterbiDemodulator, new DoubleWordRegister(this)
                    .WithFlag(0, out MODEM_viterbiDemodulatorEnable, name: "VTDEMODEN")
                    .WithFlag(1, name: "HARDDECISION")
                    .WithValueField(2, 7, name: "VITERBIKSI1")
                    .WithValueField(9, 7, name: "VITERBIKSI2")
                    .WithValueField(16, 6, name: "VITERBIKSI3")
                    .WithFlag(22, name: "SYNTHAFC")
                    .WithValueField(23, 4, name: "CORRCYCLE")
                    .WithValueField(27, 4, name: "CORRSTPSIZE")
                    .WithFlag(31, name: "DISDEMODOF")
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
                    .WithFlag(1, name: "DEBUGRUN")
                    .WithFlag(2, name: "DMACLRACT")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, name: "OSMEN")
                    .WithFlag(5, name: "ZEROSTARTEN")
                    .WithReservedBits(6, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_PreCounterSource>(8, 2, out PROTIMER_preCounterSource, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case PROTIMER_PreCounterSource.None:
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
                    .WithReservedBits(10, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_BaseCounterSource>(12, 2, out PROTIMER_baseCounterSource, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case PROTIMER_BaseCounterSource.Unused0:
                                case PROTIMER_BaseCounterSource.Unused1:
                                    this.Log(LogLevel.Error, "Invalid BASECNTSRC value");
                                    break;
                            }
                        }, name: "BASECNTSRC")
                    .WithReservedBits(14, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_WrapCounterSource>(16, 2, out PROTIMER_wrapCounterSource, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case PROTIMER_WrapCounterSource.Unused:
                                    this.Log(LogLevel.Error, "Invalid WRAPCNTSRC value");
                                    break;
                            }
                        }, name: "WRAPCNTSRC")
                    .WithReservedBits(18, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(20, 2, out PROTIMER_timeoutCounter[0].Source, name: "TOUT0SRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(22, 2, out PROTIMER_timeoutCounter[0].SyncSource, name: "TOUT0SYNCSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(24, 2, out PROTIMER_timeoutCounter[1].Source, name: "TOUT1SRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_TimeoutCounterSource>(26, 2, out PROTIMER_timeoutCounter[1].SyncSource, name: "TOUT1SYNCSRC")
                    .WithEnumField<DoubleWordRegister, PROTIMER_RepeatMode>(28, 1, out PROTIMER_timeoutCounter[0].Mode, name: "TOUT0MODE")
                    .WithEnumField<DoubleWordRegister, PROTIMER_RepeatMode>(29, 1, out PROTIMER_timeoutCounter[1].Mode, name: "TOUT1MODE")
                    .WithReservedBits(30, 2)
                },
                {(long)ProtocolTimerRegisters.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if(PROTIMER_preCounterSource.Value == PROTIMER_PreCounterSource.Clock && value) { PROTIMER_Enabled = true;} }, name: "START")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_RtcSyncStart(); }, name: "RTCSYNCSTART")
                    .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_Enabled = false; }, name: "STOP")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[0].Start(); }, name: "TOUT0START")
                    .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[0].Stop(); }, name: "TOUT0STOP")
                    .WithFlag(6, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[1].Start(); }, name: "TOUT1START")
                    .WithFlag(7, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[1].Stop(); }, name: "TOUT1STOP")
                    .WithFlag(8, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Idle; RAC_UpdateRadioStateMachine(); }, name: "FORCETXIDLE")
                    .WithFlag(9, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle; RAC_UpdateRadioStateMachine(); }, name: "FORCERXIDLE")
                    .WithFlag(10, name: "FORCERXRX")
                    .WithReservedBits(11, 5)
                    .WithFlag(16, FieldMode.Set, writeCallback: (_, value) => { if (value) PROTIMER_ListenBeforeTalkStartCommand(); }, name: "LBTSTART")
                    .WithFlag(17, FieldMode.Set, writeCallback: (_, value) => { if (value) PROTIMER_ListenBeforeTalkPauseCommand(); }, name: "LBTPAUSE")
                    .WithFlag(18, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_ListenBeforeTalkStopCommand(); }, name: "LBTSTOP")
                    .WithReservedBits(19, 13)
                    .WithWriteCallback((_, __) => { PROTIMER_HandleChangedParams(); UpdateInterrupts(); })
                },
                {(long)ProtocolTimerRegisters.PrsControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithFlag(1, name: "STARTPRSEN")
                    .WithValueField(2, 2, name: "STARTEDGE")
                    .WithReservedBits(4, 5)
                    .WithFlag(9, name: "STOPPRSEN")
                    .WithValueField(10, 2, name: "STOPEDGE")
                    .WithReservedBits(12, 5)
                    .WithFlag(17, out PROTIMER_rtccTriggerFromPrsEnable, name: "RTCCTRIGGERPRSEN")
                    .WithValueField(18, 2, name: "RTCCTRIGGEREDGE")
                    .WithReservedBits(20, 12)
                },
                { (long)ProtocolTimerRegisters.Status, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => PROTIMER_Enabled, name: "RUNNING")
                    .WithFlag(1, out PROTIMER_listenBeforeTalkSync, FieldMode.Read, name: "LBTSYNC")
                    .WithFlag(2, out PROTIMER_listenBeforeTalkRunning, FieldMode.Read, name: "LBTRUNNING")
                    .WithFlag(3, out PROTIMER_listenBeforeTalkPaused, FieldMode.Read, name: "LBTPAUSED")
                    .WithFlag(4, out PROTIMER_timeoutCounter[0].Running, FieldMode.Read, name: "TOUT0RUNNING")
                    .WithFlag(5, out PROTIMER_timeoutCounter[0].Synchronizing, FieldMode.Read, name: "TOUT0SYNC")
                    .WithFlag(6, out PROTIMER_timeoutCounter[1].Running, FieldMode.Read, name: "TOUT1RUNNING")
                    .WithFlag(7, out PROTIMER_timeoutCounter[1].Synchronizing, FieldMode.Read, name: "TOUT1SYNC")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[0].CaptureValid, FieldMode.Read, name: "ICV0")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[1].CaptureValid, FieldMode.Read, name: "ICV1")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[2].CaptureValid, FieldMode.Read, name: "ICV2")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[3].CaptureValid, FieldMode.Read, name: "ICV3")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[4].CaptureValid, FieldMode.Read, name: "ICV4")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[5].CaptureValid, FieldMode.Read, name: "ICV5")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[6].CaptureValid, FieldMode.Read, name: "ICV6")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[7].CaptureValid, FieldMode.Read, name: "ICV7")
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.PreCounterValue, new DoubleWordRegister(this)
                    // We don't tick the PRECNT value, so just always return 0
                    .WithValueField(0, 16, valueProviderCallback: _ => 0, name: "PRECNT")
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.BaseCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => PROTIMER_BaseCounterValue, writeCallback: (_, value) => PROTIMER_BaseCounterValue = (ushort)value, name: "BASECNT")
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.WrapCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  PROTIMER_WrapCounterValue, writeCallback: (_, value) => PROTIMER_WrapCounterValue = (uint)value, name: "WRAPCNT")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.BaseAndPreCounterValues, new DoubleWordRegister(this)
                    // We don't tick the PRECNT value, so just always return 0
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => 0, name: "PRECNTV")
                    .WithValueField(16, 16, FieldMode.Read, valueProviderCallback: _ => PROTIMER_BaseCounterValue, name: "BASECNTV")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.PreCounterTopValue, new DoubleWordRegister(this, 0xFFFF00)
                    .WithValueField(0, 8, out PROTIMER_preCounterTopFractional, name: "PRECNTTOPFRAC")
                    .WithValueField(8, 16, out PROTIMER_preCounterTopInteger, name: "PRECNTTOP")
                    .WithReservedBits(24, 8)
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.BaseCounterTopValue, new DoubleWordRegister(this, 0xFFFF)
                    .WithValueField(0, 16, out PROTIMER_baseCounterTop, name: "BASECNTTOP")
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.WrapCounterTopValue, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 32, out PROTIMER_wrapCounterTop, name: "WRAPCNTTOP")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)ProtocolTimerRegisters.LatchedWrapCounterValue, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "LWRAPCNT")
                },
                {(long)ProtocolTimerRegisters.PreCounterTopAdjustValue, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "PRECNTTOPADJ")
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.Timeout0Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].PreCounter, name: "TOUT0PCNT")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].Counter, name: "TOUT0CNT")
                },
                {(long)ProtocolTimerRegisters.Timeout0CounterTop, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].PreCounterTop, name: "TOUT0CNTTOP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].CounterTop, name: "TOUT0PCNTTOP")
                },
                {(long)ProtocolTimerRegisters.Timeout0Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].PreCounterCompare, name: "TOUT0PCNTCOMP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].CounterCompare, name: "TOUT0CNTCOMP")
                },
                {(long)ProtocolTimerRegisters.Timeout1Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].PreCounter, name: "TOUT1PCNT")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].Counter, name: "TOUT1CNT")
                },
                {(long)ProtocolTimerRegisters.Timeout1CounterTop, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].PreCounterTop, name: "TOUT1CNTTOP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].CounterTop, name: "TOUT1PCNTTOP")
                },
                {(long)ProtocolTimerRegisters.Timeout1Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].PreCounterCompare, name: "TOUT1PCNTCOMP")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].CounterCompare, name: "TOUT1CNTCOMP")
                },
                {(long)ProtocolTimerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_preCounterOverflowInterrupt, name: "PRECNTOFIF")
                    .WithFlag(1, out PROTIMER_baseCounterOverflowInterrupt, name: "BASECNTOFIF")
                    .WithFlag(2, out PROTIMER_wrapCounterOverflowInterrupt, name: "WRAPCNTOFIF")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out PROTIMER_timeoutCounter[0].UnderflowInterrupt, name: "TOUT0IF")
                    .WithFlag(5, out PROTIMER_timeoutCounter[1].UnderflowInterrupt, name: "TOUT1IF")
                    .WithFlag(6, out PROTIMER_timeoutCounter[0].MatchInterrupt, name: "TOUT0MATCHIF")
                    .WithFlag(7, out PROTIMER_timeoutCounter[1].MatchInterrupt, name: "TOUT1MATCHIF")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[0].InterruptField, name: "CC0IF")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[1].InterruptField, name: "CC1IF")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[2].InterruptField, name: "CC2IF")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[3].InterruptField, name: "CC3IF")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[4].InterruptField, name: "CC4IF")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[5].InterruptField, name: "CC5IF")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[6].InterruptField, name: "CC6IF")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[7].InterruptField, name: "CC7IF")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[0].OverflowInterrupt, name: "COF0IF")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[1].OverflowInterrupt, name: "COF1IF")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[2].OverflowInterrupt, name: "COF2IF")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[3].OverflowInterrupt, name: "COF3IF")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[4].OverflowInterrupt, name: "COF4IF")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[5].OverflowInterrupt, name: "COF5IF")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[6].OverflowInterrupt, name: "COF6IF")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[7].OverflowInterrupt, name: "COF7IF")
                    .WithFlag(24, out PROTIMER_listenBeforeTalkSuccessInterrupt, name: "LBTSUCCESSIF")
                    .WithFlag(25, out PROTIMER_listenBeforeTalkFailureInterrupt, name: "LBTFAILUREIF")
                    .WithFlag(26, name: "LBTPAUSEDIF")
                    .WithFlag(27, out PROTIMER_listenBeforeTalkRetryInterrupt, name: "LBTRETRYIF")
                    .WithFlag(28, out PROTIMER_rtccSynchedInterrupt, name: "RTCCSYNCHEDIF")
                    .WithFlag(29, out PROTIMER_listenBeforeTalkTimeoutCounterMatchInterrupt, name: "TOUT0MATCHLBTIF")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_preCounterOverflowInterruptEnable, name: "PRECNTOFIEN")
                    .WithFlag(1, out PROTIMER_baseCounterOverflowInterruptEnable, name: "BASECNTOFIEN")
                    .WithFlag(2, out PROTIMER_wrapCounterOverflowInterruptEnable, name: "WRAPCNTOFIEN")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out PROTIMER_timeoutCounter[0].UnderflowInterruptEnable, name: "TOUT0IEN")
                    .WithFlag(5, out PROTIMER_timeoutCounter[1].UnderflowInterruptEnable, name: "TOUT1IEN")
                    .WithFlag(6, out PROTIMER_timeoutCounter[0].MatchInterruptEnable, name: "TOUT0MATCHIEN")
                    .WithFlag(7, out PROTIMER_timeoutCounter[1].MatchInterruptEnable, name: "TOUT1MATCHIEN")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[0].InterruptEnable, name: "CC0IEN")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[1].InterruptEnable, name: "CC1IEN")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[2].InterruptEnable, name: "CC2IEN")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[3].InterruptEnable, name: "CC3IEN")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[4].InterruptEnable, name: "CC4IEN")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[5].InterruptEnable, name: "CC5IEN")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[6].InterruptEnable, name: "CC6IEN")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[7].InterruptEnable, name: "CC7IEN")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[0].OverflowInterruptEnable, name: "COF0IEN")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[1].OverflowInterruptEnable, name: "COF1IEN")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[2].OverflowInterruptEnable, name: "COF2IEN")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[3].OverflowInterruptEnable, name: "COF3IEN")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[4].OverflowInterruptEnable, name: "COF4IEN")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[5].OverflowInterruptEnable, name: "COF5IEN")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[6].OverflowInterruptEnable, name: "COF6IEN")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[7].OverflowInterruptEnable, name: "COF7IEN")
                    .WithFlag(24, out PROTIMER_listenBeforeTalkSuccessInterruptEnable, name: "LBTSUCCESSIEN")
                    .WithFlag(25, out PROTIMER_listenBeforeTalkFailureInterruptEnable, name: "LBTFAILUREIEN")
                    .WithFlag(26, name: "LBTPAUSEDIEN")
                    .WithFlag(27, out PROTIMER_listenBeforeTalkRetryInterruptEnable, name: "LBTRETRYIEN")
                    .WithFlag(28, out PROTIMER_rtccSynchedInterruptEnable, name: "RTCCSYNCHEDIEN")
                    .WithFlag(29, out PROTIMER_listenBeforeTalkTimeoutCounterMatchInterruptEnable, name: "TOUT0MATCHLBTIEN")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_seqPreCounterOverflowInterrupt, name: "PRECNTOFSEQIF")
                    .WithFlag(1, out PROTIMER_seqBaseCounterOverflowInterrupt, name: "BASECNTOFSEQIF")
                    .WithFlag(2, out PROTIMER_seqWrapCounterOverflowInterrupt, name: "WRAPCNTOFSEQIF")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out PROTIMER_timeoutCounter[0].SeqUnderflowInterrupt, name: "TOUT0SEQIF")
                    .WithFlag(5, out PROTIMER_timeoutCounter[1].SeqUnderflowInterrupt, name: "TOUT1SEQIF")
                    .WithFlag(6, out PROTIMER_timeoutCounter[0].SeqMatchInterrupt, name: "TOUT0MATCHSEQIF")
                    .WithFlag(7, out PROTIMER_timeoutCounter[1].SeqMatchInterrupt, name: "TOUT1MATCHSEQIF")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[0].SeqInterruptField, name: "CC0SEQIF")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[1].SeqInterruptField, name: "CC1SEQIF")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[2].SeqInterruptField, name: "CC2SEQIF")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[3].SeqInterruptField, name: "CC3SEQIF")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[4].SeqInterruptField, name: "CC4SEQIF")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[5].SeqInterruptField, name: "CC5SEQIF")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[6].SeqInterruptField, name: "CC6SEQIF")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[7].SeqInterruptField, name: "CC7SEQIF")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[0].SeqOverflowInterrupt, name: "COF0SEQIF")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[1].SeqOverflowInterrupt, name: "COF1SEQIF")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[2].SeqOverflowInterrupt, name: "COF2SEQIF")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[3].SeqOverflowInterrupt, name: "COF3SEQIF")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[4].SeqOverflowInterrupt, name: "COF4SEQIF")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[5].SeqOverflowInterrupt, name: "COF5SEQIF")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[6].SeqOverflowInterrupt, name: "COF6SEQIF")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[7].SeqOverflowInterrupt, name: "COF7SEQIF")
                    .WithFlag(24, out PROTIMER_seqListenBeforeTalkSuccessInterrupt, name: "LBTSUCCESSSEQIF")
                    .WithFlag(25, out PROTIMER_seqListenBeforeTalkFailureInterrupt, name: "LBTFAILURESEQIF")
                    .WithFlag(26, name: "LBTPAUSEDIF")
                    .WithFlag(27, out PROTIMER_seqListenBeforeTalkRetryInterrupt, name: "LBTRETRYSEQIF")
                    .WithFlag(28, out PROTIMER_seqRtccSynchedInterrupt, name: "RTCCSYNCHEDSEQIF")
                    .WithFlag(29, out PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterrupt, name: "TOUT0MATCHLBTSEQIF")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_seqPreCounterOverflowInterruptEnable, name: "PRECNTOFSEQIEN")
                    .WithFlag(1, out PROTIMER_seqBaseCounterOverflowInterruptEnable, name: "BASECNTOFSEQIEN")
                    .WithFlag(2, out PROTIMER_seqWrapCounterOverflowInterruptEnable, name: "WRAPCNTOFSEQIEN")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, out PROTIMER_timeoutCounter[0].SeqUnderflowInterruptEnable, name: "TOUT0SEQIEN")
                    .WithFlag(5, out PROTIMER_timeoutCounter[1].SeqUnderflowInterruptEnable, name: "TOUT1SEQIEN")
                    .WithFlag(6, out PROTIMER_timeoutCounter[0].SeqMatchInterruptEnable, name: "TOUT0MATCHSEQIEN")
                    .WithFlag(7, out PROTIMER_timeoutCounter[1].SeqMatchInterruptEnable, name: "TOUT1MATCHSEQIEN")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[0].SeqInterruptEnable, name: "CC0SEQIEN")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[1].SeqInterruptEnable, name: "CC1SEQIEN")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[2].SeqInterruptEnable, name: "CC2SEQIEN")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[3].SeqInterruptEnable, name: "CC3SEQIEN")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[4].SeqInterruptEnable, name: "CC4SEQIEN")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[5].SeqInterruptEnable, name: "CC5SEQIEN")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[6].SeqInterruptEnable, name: "CC6SEQIEN")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[7].SeqInterruptEnable, name: "CC7SEQIEN")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[0].SeqOverflowInterruptEnable, name: "COF0SEQIEN")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[1].SeqOverflowInterruptEnable, name: "COF1SEQIEN")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[2].SeqOverflowInterruptEnable, name: "COF2SEQIEN")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[3].SeqOverflowInterruptEnable, name: "COF3SEQIEN")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[4].SeqOverflowInterruptEnable, name: "COF4SEQIEN")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[5].SeqOverflowInterruptEnable, name: "COF5SEQIEN")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[6].SeqOverflowInterruptEnable, name: "COF6SEQIEN")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[7].SeqOverflowInterruptEnable, name: "COF7SEQIEN")
                    .WithFlag(24, out PROTIMER_seqListenBeforeTalkSuccessInterruptEnable, name: "LBTSUCCESSSEQIEN")
                    .WithFlag(25, out PROTIMER_seqListenBeforeTalkFailureInterruptEnable, name: "LBTFAILURESEQIEN")
                    .WithFlag(26, name: "LBTPAUSEDIEN")
                    .WithFlag(27, out PROTIMER_seqListenBeforeTalkRetryInterruptEnable, name: "LBTRETRYSEQIEN")
                    .WithFlag(28, out PROTIMER_seqRtccSynchedInterruptEnable, name: "RTCCSYNCHEDSEQIEN")
                    .WithFlag(29, out PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterruptEnable, name: "TOUT0MATCHLBTSEQIEN")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)ProtocolTimerRegisters.RxControl, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(0, 5, out PROTIMER_rxSetEvent1, name: "RXSETEVENT1")
                    .WithReservedBits(5, 3)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(8, 5, out PROTIMER_rxSetEvent2, name: "RXSETEVENT2")
                    .WithReservedBits(13, 3)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(16, 5, out PROTIMER_rxClearEvent1, name: "RXCLREVENT1")
                    .WithReservedBits(21, 3)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(24, 5, out PROTIMER_rxClearEvent2, name: "RXCLREVENT2")
                    .WithReservedBits(29, 3)
                    .WithChangeCallback((_, __) => PROTIMER_UpdateRxRequestState())
                },
                {(long)ProtocolTimerRegisters.TxControl, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(0, 5, out PROTIMER_txSetEvent1, name: "TXSETEVENT1")
                    .WithReservedBits(5, 3)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(8, 5, out PROTIMER_txSetEvent2, name: "TXSETEVENT2")
                    .WithReservedBits(13, 19)
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
                {(long)ProtocolTimerRegisters.ListenBeforeTalkState, new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "TOUT0PCNT")
                    .WithValueField(16, 16, name: "TOUT0CNT")
                },
                {(long)ProtocolTimerRegisters.PseudoRandomGeneratorValue, new DoubleWordRegister(this)
                    .WithValueField(0, 16,
                        // When FIXEDBACKOFF is set, hardware freezes the RANDOM register: it neither
                        // regenerates on read nor accepts updates. Outside fixed backoff it behaves as an
                        // LFSR (fresh value each read).
                        valueProviderCallback: _ => { return PROTIMER_fixedBackoff.Value ? (ushort)PROTIMER_fixedBackoffRandomValue : (ushort)random.Next(); },
                        writeCallback: (_, value) => { if(!PROTIMER_fixedBackoff.Value) { PROTIMER_fixedBackoffRandomValue = (uint)value; } },
                        name: "RANDOM")
                    .WithReservedBits(16, 16)
                },
                {(long)ProtocolTimerRegisters.ListenBeforeTalkState1, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out PROTIMER_ccaCounter, name: "CCACNT")
                    .WithValueField(4, 4, out PROTIMER_listenBeforeTalkExponent, name: "EXP")
                    .WithValueField(8, 4, out PROTIMER_listenBeforeTalkRetryCounter, name: "RETRYCNT")
                    .WithReservedBits(12, 20)
                },
                {(long)ProtocolTimerRegisters.ListenBeforeTalkPrsControl, new DoubleWordRegister(this)
                    .WithReservedBits(0, 8)
                    .WithFlag(8, out PROTIMER_listenBeforeTalkPrsStartEnable, name: "LBTSTARTPRSEN")
                    .WithReservedBits(9, 7)
                    .WithFlag(16, out PROTIMER_listenBeforeTalkPrsPauseEnable, name: "LBTPAUSEPRSEN")
                    .WithReservedBits(17, 7)
                    .WithFlag(24, out PROTIMER_listenBeforeTalkPrsStopEnable, name: "LBTSTOPPRSEN")
                    .WithReservedBits(25, 7)
                },
            };

            var startOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0Control;
            var controlOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0Control - startOffset;
            var preOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0PreValue - startOffset;
            var baseOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0BaseValue - startOffset;
            var wrapOffset = (long)ProtocolTimerRegisters.CaptureCompareChannel0WrapValue - startOffset;
            var blockSize = (long)ProtocolTimerRegisters.CaptureCompareChannel1Control - (long)ProtocolTimerRegisters.CaptureCompareChannel0Control;
            for(var index = 0; index < PROTIMER_NumberOfCaptureCompareChannels; index++)
            {
                var i = index;
                // CaptureCompareChannel_n_Control
                registerDictionary.Add(startOffset + blockSize * i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithFlag(0, out PROTIMER_captureCompareChannel[i].Enable, name: "ENABLE")
                        .WithEnumField<DoubleWordRegister, PROTIMER_CaptureCompareMode>(1, 1, out PROTIMER_captureCompareChannel[i].Mode, name: "CCMODE")
                        .WithFlag(2, out PROTIMER_captureCompareChannel[i].PreMatchEnable, name: "PREMATCHEN")
                        .WithFlag(3, out PROTIMER_captureCompareChannel[i].BaseMatchEnable, name: "BASEMATCHEN")
                        .WithFlag(4, out PROTIMER_captureCompareChannel[i].WrapMatchEnable, name: "WRAPMATCHEN")
                        .WithFlag(5, name: "OIST")
                        .WithFlag(6, name: "OUTINV")
                        .WithReservedBits(7, 1)
                        .WithValueField(8, 2, name: "MOA")
                        .WithValueField(10, 2, name: "OFOA")
                        .WithValueField(12, 2, name: "OFSEL")
                        .WithFlag(14, name: "PRSCONF")
                        .WithReservedBits(15, 6)
                        .WithEnumField<DoubleWordRegister, PROTIMER_CaptureInputSource>(21, 4, out PROTIMER_captureCompareChannel[i].CaptureInputSource, name: "INSEL")
                        .WithValueField(25, 2, name: "ICEDGE")
                        .WithReservedBits(27, 5)
                        .WithWriteCallback((_, __) => PROTIMER_UpdateCompareTimer(i))
                );
                // CaptureCompareChannel_n_Pre
                registerDictionary.Add(startOffset + blockSize * i + preOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 16, out PROTIMER_captureCompareChannel[i].PreValue,
                            writeCallback: (_, __) =>
                            {
                                PROTIMER_captureCompareChannel[i].CaptureValid.Value = false;
                                PROTIMER_UpdateCompareTimer(i);
                            },
                            valueProviderCallback: _ =>
                            {
                                PROTIMER_captureCompareChannel[i].CaptureValid.Value = false;
                                return PROTIMER_captureCompareChannel[i].PreValue.Value;
                            }, name: "PRE")
                        .WithReservedBits(16, 16)
                );
                // CaptureCompareChannel_n_Base
                registerDictionary.Add(startOffset + blockSize * i + baseOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 16, out PROTIMER_captureCompareChannel[i].BaseValue,
                            writeCallback: (_, __) => { PROTIMER_captureCompareChannel[i].CaptureValid.Value = false; },
                            valueProviderCallback: _ =>
                            {
                                PROTIMER_captureCompareChannel[i].CaptureValid.Value = false;
                                return PROTIMER_captureCompareChannel[i].BaseValue.Value;
                            }, name: "BASE")
                        .WithReservedBits(16, 16)
                );
                // CaptureCompareChannel_n_Wrap
                registerDictionary.Add(startOffset + blockSize * i + wrapOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].WrapValue,
                            writeCallback: (_, __) => { PROTIMER_captureCompareChannel[i].CaptureValid.Value = false; },
                            valueProviderCallback: _ =>
                            {
                                PROTIMER_captureCompareChannel[i].CaptureValid.Value = false;
                                return PROTIMER_captureCompareChannel[i].WrapValue.Value;
                            }, name: "WRAP")
                );
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildRadioControllerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)RadioControllerRegisters.RXENSourceEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out RAC_softwareRxEnable, name: "SWRXEN")
                    .WithFlag(8, name: "CHANNELBUSYEN")
                    .WithFlag(9, name: "TIMDETEN")
                    .WithFlag(10, name: "PREDETEN")
                    .WithFlag(11, name: "FRAMEDETEN")
                    .WithFlag(12, name: "DEMODRXREQEN")
                    .WithFlag(13, name: "PRSRXEN")
                    .WithReservedBits(14, 18)
                    .WithWriteCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)RadioControllerRegisters.Status, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => RAC_RxEnableMask, name: "RXMASK")
                    .WithReservedBits(16, 3)
                    .WithFlag(19, out RAC_forceStateActive, FieldMode.Read, name: "FORCESTATEACTIVE")
                    .WithFlag(20, out RAC_txAfterFramePending, FieldMode.Read, name: "TXAFTERFRAMEPEND")
                    .WithFlag(21, out RAC_txAfterFrameActive, FieldMode.Read, name: "TXAFTERFRAMEACTIVE")
                    .WithFlag(22, out RAC_sequencerInSleeping, FieldMode.Read, name: "SEQSLEEPING")
                    .WithFlag(23, out RAC_sequencerInDeepSleep, FieldMode.Read, name: "SEQSLEEPDEEP")
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(24, 4, FieldMode.Read, valueProviderCallback: _ => RAC_currentRadioState, name: "STATE")
                    .WithFlag(28, out RAC_sequencerActive, FieldMode.Read, name: "SEQACTIVE")
                    .WithFlag(29, name: "DEMODENS")
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => RAC_TxEnable, name: "TXENS")
                    .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => RAC_RxEnable, name: "RXENS")
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
                    .WithFlag(1, name: "PRSTXEN")
                    .WithFlag(2, out RAC_txAfterRx, name: "TXAFTERRX")
                    .WithFlag(3, name: "PRSMODE")
                    .WithReservedBits(4, 1)
                    .WithFlag(5, name: "PRSCLR")
                    .WithFlag(6, name: "TXPOSTPONE")
                    .WithFlag(7, name: "ACTIVEPOL")
                    .WithFlag(8, name: "PAENPOL")
                    .WithFlag(9, name: "LNAENPOL")
                    .WithFlag(10, name: "PRSRXDIS")
                    .WithReservedBits(11, 5)
                    .WithFlag(16, out RAC_prsForceTx, name: "PRSFORCETX")
                    .WithReservedBits(17, 7)
                    .WithFlag(24, name: "SEQRESET")
                    .WithFlag(25, out RAC_exitShutdownDisable, FieldMode.Read, name: "EXITSHUTDOWNDIS")
                    .WithFlag(26, FieldMode.Set, writeCallback: (_, value) =>
                        {
                            if(value && sequencer.CpuWaitSignal.IsSet)
                            {
                                sequencer.VectorTableOffset = SequencerMemoryBaseAddress;
                                sequencer.SP = machine.SystemBus.ReadDoubleWord(0x0, sequencer);
                                sequencer.PC = machine.SystemBus.ReadDoubleWord(0x4, sequencer);
                                RAC_SeqTimerStart();
                                sequencer.CpuWaitSignal.Set(false);
                                this.Log(LogLevel.Noisy, "Sequencer resumed, isHalted={0} VTOR={1:X} SP={2:X} PC={3:X} CPUWAIT={4}.", sequencer.IsHalted, sequencer.VectorTableOffset, sequencer.SP, sequencer.PC, sequencer.CpuWaitSignal.IsSet);
                            }
                        }, name: "CPUWAITDIS")
                    .WithFlag(27, name: "SEQCLKDIS")
                    .WithFlag(28, name: "RXOFDIS")
                    .WithReservedBits(29, 3)
                    .WithChangeCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)RadioControllerRegisters.Command, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_TxEnable = true;} }, name: "TXEN")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.ForceTx);} }, name: "FORCETX")
                    .WithFlag(2, name: "TXONCCA")
                    .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_TxEnable = false;} }, name: "CLEARTXEN")
                    .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_HandleTxAfterFrameCommand();} }, name: "TXAFTERFRAME")
                    .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) {RAC_TxEnable = false; RAC_txAfterFramePending.Value = false; RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxDisable);} }, name: "TXDIS")
                    .WithFlag(6, FieldMode.Set, writeCallback: (_, value) => { if (value && !RAC_seqStateRxOverflowInterrupt.Value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.ClearRxOverflow);} }, name: "CLEARRXOVERFLOW")
                    .WithFlag(7, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxCalibration);} }, name: "RXCAL")
                    .WithFlag(8, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxDisable);} }, name: "RXDIS")
                    .WithReservedBits(9, 1)
                    .WithFlag(10, name: "FRCWR")
                    .WithFlag(11, name: "FRCRD")
                    .WithFlag(12, name: "PAENSET")
                    .WithFlag(13, name: "PAENCLEAR")
                    .WithFlag(14, name: "LNAENSET")
                    .WithFlag(15, name: "LNAENCLEAR")
                    .WithReservedBits(16, 14)
                    .WithFlag(30, name: "DEMODENSET")
                    .WithFlag(31, name: "DEMODENCLEAR")
                },
                {(long)RadioControllerRegisters.ForceStateTransition, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, RAC_RadioState>(0, 4, out RAC_forceStateTransition, writeCallback: (_, __) =>
                        {
                            RAC_forceStateActive.Value = true;
                        }, name: "FORCESTATE")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)RadioControllerRegisters.Em1pControlAndStatus, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { RAC_em1pAckPending = true; }, name: "RADIOEM1PMODE")
                    .WithFlag(1, name: "RADIOEM1PMODE")
                    .WithReservedBits(2, 2)
                    .WithFlag(4, name: "MCUEM1PMODE")
                    .WithFlag(5, name: "MCUEM1PDISSWREQ")
                    .WithReservedBits(6, 10)
                    .WithFlag(16, name: "RADIOEM1PREQ")
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            var retValue = RAC_em1pAckPending;
                            RAC_em1pAckPending = false;
                            return retValue;
                        }, name: "RADIOEM1PACK")
                    .WithFlag(18, name: "RADIOEM1PHWREQ")
                    .WithReservedBits(19, 13)
                },
                {(long)RadioControllerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_radioStateChangeInterrupt, name: "STATECHANGEIF")
                    .WithFlag(1, out RAC_stimerCompareEventInterrupt, name: "STIMCMPEVIF")
                    .WithFlag(2, name: "SEQLOCKUPIF")
                    .WithFlag(3, name: "SEQRESETREQIF")
                    .WithReservedBits(4, 12)
                    .WithValueField(16, 8, out RAC_mainCoreSeqInterrupts, name: "SEQIF")
                    .WithReservedBits(24, 8)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RadioControllerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_radioStateChangeInterruptEnable, name: "STATECHANGEIEN")
                    .WithFlag(1, out RAC_stimerCompareEventInterruptEnable, name: "STIMCMPEVIEN")
                    .WithFlag(2, name: "SEQLOCKUPIEN")
                    .WithFlag(3, name: "SEQRESETREQIEN")
                    .WithReservedBits(4, 12)
                    .WithValueField(16, 8, out RAC_mainCoreSeqInterruptsEnable, name: "SEQIEN")
                    .WithReservedBits(24, 8)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RadioControllerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_seqRadioStateChangeInterrupt, name: "STATECHANGESEQIF")
                    .WithFlag(1, out RAC_seqStimerCompareEventInterrupt, name: "STIMCMPEVSEQIF")
                    .WithFlag(2, out RAC_seqDemodRxRequestClearInterrupt, name: "DEMODRXREQCLRSEQIF")
                    .WithFlag(3, out RAC_seqPrsEventInterrupt, name: "PRSEVENTSEQIF")
                    .WithReservedBits(4, 12)
                    .WithFlag(16, out RAC_seqStateOffInterrupt, name: "STATEOFFIF")
                    .WithFlag(17, out RAC_seqStateRxWarmInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxWarmIrqCleared); } }, name: "STATERXWARMIF")
                    .WithFlag(18, out RAC_seqStateRxSearchInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxSearchIrqCleared); } }, name: "STATERXSEARCHIF")
                    .WithFlag(19, out RAC_seqStateRxFrameInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxFrameIrqCleared); } }, name: "STATERXFRAMEIF")
                    .WithFlag(20, out RAC_seqStateRxPoweringDownInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxPowerDownIrqCleared); } }, name: "STATERXPDIF")
                    .WithFlag(21, out RAC_seqStateRx2RxInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.Rx2RxIrqCleared); } }, name: "STATERX2RXIF")
                    .WithFlag(22, out RAC_seqStateRxOverflowInterrupt, name: "STATERXOVERFLOWIF")
                    .WithFlag(23, out RAC_seqStateRx2TxInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.Rx2TxIrqCleared); } }, name: "STATERX2TXIF")
                    .WithFlag(24, out RAC_seqStateTxWarmInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxWarmIrqCleared);} }, name: "STATETXWARMIF")
                    .WithFlag(25, out RAC_seqStateTxInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxIrqCleared); } }, name: "STATETXIF")
                    .WithFlag(26, out RAC_seqStateTxPoweringDownInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxPowerDownIrqCleared); } }, name: "STATETXPDIF")
                    .WithFlag(27, out RAC_seqStateTx2RxInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.Tx2RxIrqCleared); } }, name: "STATETX2RXIF")
                    .WithFlag(28, out RAC_seqStateTx2TxInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.Tx2TxIrqCleared); } }, name: "STATETX2TXIF")
                    .WithFlag(29, out RAC_seqStateShutDownInterrupt, changeCallback: (_, value) => { if (!value) {RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.ShutdownIrqCleared); } }, name: "STATESHUTDOWNIF")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RadioControllerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out RAC_seqRadioStateChangeInterruptEnable, name: "STATECHANGESEQIEN")
                    .WithFlag(1, out RAC_seqStimerCompareEventInterruptEnable, name: "STIMCMPEVSEQIEN")
                    .WithFlag(2, out RAC_seqDemodRxRequestClearInterruptEnable, name: "DEMODRXREQCLRSEQIEN")
                    .WithFlag(3, out RAC_seqPrsEventInterruptEnable, name: "PRSEVENTSEQIEN")
                    .WithReservedBits(4, 12)
                    .WithFlag(16, out RAC_seqStateOffInterruptEnable, name: "STATEOFFIEN")
                    .WithFlag(17, out RAC_seqStateRxWarmInterruptEnable, name: "STATERXWARMIEN")
                    .WithFlag(18, out RAC_seqStateRxSearchInterruptEnable, name: "STATERXSEARCHIEN")
                    .WithFlag(19, out RAC_seqStateRxFrameInterruptEnable, name: "STATERXFRAMEIEN")
                    .WithFlag(20, out RAC_seqStateRxPoweringDownInterruptEnable, name: "STATERXPDIEN")
                    .WithFlag(21, out RAC_seqStateRx2RxInterruptEnable, name: "STATERX2RXIEN")
                    .WithFlag(22, out RAC_seqStateRxOverflowInterruptEnable, name: "STATERXOVERFLOWIEN")
                    .WithFlag(23, out RAC_seqStateRx2TxInterruptEnable, name: "STATERX2TXIEN")
                    .WithFlag(24, out RAC_seqStateTxWarmInterruptEnable, name: "STATETXWARMIEN")
                    .WithFlag(25, out RAC_seqStateTxInterruptEnable, name: "STATETXIEN")
                    .WithFlag(26, out RAC_seqStateTxPoweringDownInterruptEnable, name: "STATETXPDIEN")
                    .WithFlag(27, out RAC_seqStateTx2RxInterruptEnable, name: "STATETX2RXIEN")
                    .WithFlag(28, out RAC_seqStateTx2TxInterruptEnable, name: "STATETX2TXIEN")
                    .WithFlag(29, out RAC_seqStateShutDownInterruptEnable, name: "STATESHUTDOWNIEN")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)RadioControllerRegisters.SequencerControl, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, RAC_SeqTimerCompareAction>(0, 1, out RAC_seqTimerCompareAction, name: "COMPACT")
                    .WithEnumField<DoubleWordRegister, RAC_SeqTimerCompareInvalidMode>(1, 2, out RAC_seqTimerCompareInvalidMode, name: "COMPINVALMODE")
                    .WithFlag(3, out RAC_seqTimerCompareRelative, name: "RELATIVE")
                    .WithFlag(4, out RAC_seqTimerAlwaysRun, name: "STIMERALWAYSRUN")
                    .WithFlag(5, name: "STIMERDEBUGRUN")
                    .WithFlag(6, name: "STATEDEBUGRUN")
                    .WithReservedBits(7, 17)
                    .WithValueField(24, 2, out RAC_softwareIrq, writeCallback: (_, __) => UpdateInterrupts(), name: "SWIRQ")
                    .WithReservedBits(26, 6)
                },
                {(long)RadioControllerRegisters.SequencerTimerValue, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => RAC_SeqTimerValue, name: "STIMER")
                    .WithReservedBits(16, 16)
                },
                {(long)RadioControllerRegisters.SequencerTimerCompareValue, new DoubleWordRegister(this)
                    .WithValueField(0, 15, out RAC_seqTimerCompareValue, writeCallback: (_, value) => { RAC_SeqTimerLimit = (ushort)value; }, name: "STIMERCOMP")
                    .WithReservedBits(16, 16)
                },
                {(long)RadioControllerRegisters.SequencerPrescaler, new DoubleWordRegister(this, 0x7)
                    .WithValueField(0, 7, out RAC_seqTimerPrescaler, name: "STIMERPRESC")
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
                    .WithReservedBits(0, 8)
                    .WithFlag(8, valueProviderCallback: _ => { return RAC_PaOutputLevelRamping; }, writeCallback: (_, value) => { RAC_PaOutputLevelRamping = value; }, name: "PARAMP")
                    .WithReservedBits(9, 7)
                    .WithFlag(16, name: "INVRAMPCLK")
                    .WithReservedBits(17, 11)
                    .WithFlag(28, name: "PARAMPMODE")
                    .WithReservedBits(29, 3)
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
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildFrameControllerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)FrameControllerRegisters.Status, new DoubleWordRegister(this)
                    .WithValueField(0, 5, name: "SNIFFDCOUNT")
                    .WithFlag(5, out FRC_activeTransmitFrameDescriptor, FieldMode.Read, name: "ACTIVETXFCD")
                    .WithFlag(6, out FRC_activeReceiveFrameDescriptor, FieldMode.Read, name: "ACTIVERXFCD")
                    .WithFlag(7, name: "SNIFFDFRAME")
                    .WithFlag(8, out FRC_rxRawBlocked, FieldMode.Read, name: "RXRAWBLOCKED")
                    .WithFlag(9, name: "FRAMEOK")
                    .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "RXABORTINPROGRESS")
                    .WithFlag(11, name: "TXWORD")
                    .WithFlag(12, name: "RXWORD")
                    .WithFlag(13, name: "CONVPAUSED")
                    .WithFlag(14, name: "TXSUBFRAMEPAUSED")
                    .WithFlag(15, name: "INTERLEAVEREADPAUSED")
                    .WithFlag(16, name: "INTERLEAVEWRITEPAUSED")
                    .WithFlag(17, name: "FRAMEDETPAUSED")
                    .WithFlag(18, name: "FRAMELENGTHERROR")
                    .WithFlag(19, name: "DEMODERROR")
                    .WithValueField(20, 5, out FRC_fsmState, name: "FSMSTATE")
                    .WithReservedBits(25, 7)
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
                    .WithValueField(21, 4, name: "DFLBOIOFFSET")
                    .WithReservedBits(25, 7)
                },
                {(long)FrameControllerRegisters.MaximumFrameLength, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out FRC_maxDecodedLength, name: "MAXLENGTH")
                    .WithValueField(12, 4, out FRC_initialDecodedFrameLength, name: "INILENGTH")
                    .WithReservedBits(16, 16)
                },
                {(long)FrameControllerRegisters.AddressFilterControl, new DoubleWordRegister(this)
                    .WithFlag(0, name: "EN")
                    .WithFlag(1, name: "BRDCST00EN")
                    .WithFlag(2, name: "BRDCSTFFEN")
                    .WithReservedBits(3, 5)
                    .WithValueField(8, 8, name: "ADDRESS")
                    .WithReservedBits(16, 16)
                },
                {(long)FrameControllerRegisters.FrameControllerDataBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "DATABUFFER")
                    .WithReservedBits(8, 24)
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
                    .WithFlag(1, name: "FRAMEDETRESUME")
                    .WithFlag(2, name: "INTERLEAVEWRITERESUME")
                    .WithFlag(3, name: "INTERLEAVEREADRESUME")
                    .WithFlag(4, name: "CONVRESUME")
                    .WithFlag(5, name: "CONVTERMINATE")
                    .WithFlag(6, name: "TXSUBFRAMERESUME")
                    .WithFlag(7, name: "INTERLEAVEINIT")
                    .WithFlag(8, name: "INTERLEAVECNTCLEAR")
                    .WithFlag(9, name: "CONVINIT")
                    .WithFlag(10, name: "BLOCKINIT")
                    .WithFlag(11, name: "STATEINIT")
                    .WithFlag(12, FieldMode.Set, writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                FRC_rxRawBlocked.Value = false;
                                FRC_UpdateRawMode();
                            }
                        }, name: "RXRAWUNBLOCK")
                    .WithReservedBits(13, 19)
                },
                {(long)FrameControllerRegisters.Control, new DoubleWordRegister(this, 0x00000700)
                    .WithFlag(0, name: "RANDOMTX")
                    .WithFlag(1, name: "UARTMODE")
                    .WithFlag(2, name: "BITORDER")
                    .WithReservedBits(3, 1)
                    .WithEnumField<DoubleWordRegister, FRC_FrameDescriptorMode>(4, 2, out FRC_txFrameDescriptorMode, name: "TXFCDMODE")
                    .WithEnumField<DoubleWordRegister, FRC_FrameDescriptorMode>(6, 2, out FRC_rxFrameDescriptorMode, name: "RXFCDMODE")
                    .WithValueField(8, 3, name: "BITSPERWORD")
                    .WithValueField(11, 2, out FRC_rateSelect, name: "RATESELECT")
                    .WithFlag(13, name: "TXPREFETCH")
                    .WithReservedBits(14, 2)
                    .WithFlag(16, name: "SEQHANDSHAKE")
                    .WithFlag(17, name: "PRBSTEST")
                    .WithFlag(18, name: "LPMODEDIS")
                    .WithFlag(19, name: "WAITEOFEN")
                    .WithFlag(20, name: "RXABORTIGNOREDIS")
                    .WithReservedBits(21, 11)
                },
                {(long)FrameControllerRegisters.RxControl, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_rxStoreCrc, name: "STORECRC")
                    .WithFlag(1, out FRC_rxAcceptCrcErrors, name: "ACCEPTCRCERRORS")
                    .WithFlag(2, name: "ACCEPTBLOCKERRORS")
                    .WithFlag(3, name: "TRACKABFRAME") /* TODO: RENODE-354 */
                    .WithFlag(4, out FRC_rxBufferClear, name: "BUFCLEAR")
                    .WithFlag(5, out FRC_rxBufferRestoreOnFrameError, name: "BUFRESTOREFRAMEERROR")
                    .WithFlag(6, out FRC_rxBufferRestoreOnRxAborted, name: "BUFRESTORERXABORTED")
                    .WithValueField(7, 4, name: "RXFRAMEENDAHEADBYTES")
                    .WithReservedBits(11, 21)
                },
                {(long)FrameControllerRegisters.TrailingRxData, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_rxAppendRssi, name: "RSSI")
                    .WithFlag(1, out FRC_rxAppendCrcOk, name: "CRCOK")
                    .WithFlag(2, out FRC_rxAppendProtimerCc0base, name: "PROTIMERCC0BASE")
                    .WithFlag(3, out FRC_rxAppendProtimerCc0LowWrap, name: "PROTIMERCC0WRAPL")
                    .WithFlag(4, out FRC_rxAppendProtimerCc0HighWrap, name: "PROTIMERCC0WRAPH")
                    .WithFlag(5, out FRC_rxAppendRtcStamp, name: "RTCSTAMP")
                    .WithReservedBits(6, 26)
                },
                {(long)FrameControllerRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_txDoneInterrupt, name: "TXDONEIF")
                    .WithFlag(1, out FRC_txAfterFrameDoneInterrupt, name: "TXAFTERFRAMEDONEIF")
                    .WithFlag(2, name: "TXABORTEDIF")
                    .WithFlag(3, out FRC_txUnderflowInterrupt, name: "TXUFIF")
                    .WithFlag(4, out FRC_rxDoneInterrupt, name: "RXDONEIF")
                    .WithFlag(5, out FRC_rxAbortedInterrupt, name: "RXABORTEDIF")
                    .WithFlag(6, out FRC_frameErrorInterrupt, name: "FRAMEERRORIF")
                    .WithFlag(7, name: "BLOCKERRORIF")
                    .WithFlag(8, out FRC_rxOverflowInterrupt, name: "RXOFIF")
                    .WithFlag(9, out FRC_wordCounterCompareInterrupt[0], name: "WCNTCMP0IF")
                    .WithFlag(10, out FRC_wordCounterCompareInterrupt[1], name: "WCNTCMP1IF")
                    .WithFlag(11, out FRC_wordCounterCompareInterrupt[2], name: "WCNTCMP2IF")
                    .WithFlag(12, name: "ADDRERRORIF")
                    .WithFlag(13, name: "BUSERRORIF")
                    .WithFlag(14, out FRC_rxRawEventInterrupt, name: "RXRAWEVENTIF")
                    .WithFlag(15, out FRC_txRawEventInterrupt, name: "TXRAWEVENTIF")
                    .WithFlag(16, name: "SNIFFOFIF")
                    .WithFlag(17, name: "WCNTCMP3IF")
                    .WithFlag(18, name: "WCNTCMP4IF")
                    .WithFlag(19, name: "BOISETIF")
                    .WithFlag(20, out FRC_packetBufferStartInterrupt, name: "PKTBUFSTARTIF")
                    .WithFlag(21, out FRC_packetBufferThresholdInterrupt, name: "PKTBUFTHRESHOLDIF")
                    .WithFlag(22, name: "RXRAWOFIF")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, name: "FRAMEDETPAUSEDIF")
                    .WithFlag(25, name: "INTERLEAVEWRITEPAUSEDIF")
                    .WithFlag(26, name: "INTERLEAVEREADPAUSEDIF")
                    .WithFlag(27, name: "TXSUBFRAMEPAUSEDIF")
                    .WithFlag(28, name: "CONVPAUSEDIF")
                    .WithFlag(29, name: "RXWORDIF")
                    .WithFlag(30, name: "TXWORDIF")
                    .WithReservedBits(31, 1)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_txDoneInterruptEnable, name: "TXDONEIEN")
                    .WithFlag(1, out FRC_txAfterFrameDoneInterruptEnable, name: "TXAFTERFRAMEDONEIEN")
                    .WithFlag(2, name: "TXABORTEDIEN")
                    .WithFlag(3, out FRC_txUnderflowInterruptEnable, name: "TXUFIEN")
                    .WithFlag(4, out FRC_rxDoneInterruptEnable, name: "RXDONEIEN")
                    .WithFlag(5, out FRC_rxAbortedInterruptEnable, name: "RXABORTEDIEN")
                    .WithFlag(6, out FRC_frameErrorInterruptEnable, name: "FRAMEERRORIEN")
                    .WithFlag(7, name: "BLOCKERRORIEN")
                    .WithFlag(8, out FRC_rxOverflowInterruptEnable, name: "RXOFIEN")
                    .WithFlag(9, out FRC_wordCounterCompareInterruptEnable[0], name: "WCNTCMP0IEN")
                    .WithFlag(10, out FRC_wordCounterCompareInterruptEnable[1], name: "WCNTCMP1IEN")
                    .WithFlag(11, out FRC_wordCounterCompareInterruptEnable[2], name: "WCNTCMP2IEN")
                    .WithFlag(12, name: "ADDRERRORIEN")
                    .WithFlag(13, name: "BUSERRORIEN")
                    .WithFlag(14, out FRC_rxRawEventInterruptEnable, name: "RXRAWEVENTIEN")
                    .WithFlag(15, out FRC_txRawEventInterruptEnable, name: "TXRAWEVENTIEN")
                    .WithFlag(16, name: "SNIFFOFIEN")
                    .WithFlag(17, name: "WCNTCMP3IEN")
                    .WithFlag(18, name: "WCNTCMP4IEN")
                    .WithFlag(19, name: "BOISETIEN")
                    .WithFlag(20, out FRC_packetBufferStartInterruptEnable, name: "PKTBUFSTARTIEN")
                    .WithFlag(21, out FRC_packetBufferThresholdInterruptEnable, name: "PKTBUFTHRESHOLDIEN")
                    .WithFlag(22, name: "RXRAWOFIEN")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, name: "FRAMEDETPAUSEDIEN")
                    .WithFlag(25, name: "INTERLEAVEWRITEPAUSEDIEN")
                    .WithFlag(26, name: "INTERLEAVEREADPAUSEDIEN")
                    .WithFlag(27, name: "TXSUBFRAMEPAUSEDIEN")
                    .WithFlag(28, name: "CONVPAUSEDIEN")
                    .WithFlag(29, name: "RXWORDIEN")
                    .WithFlag(30, name: "TXWORDIEN")
                    .WithReservedBits(31, 1)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.SequencerInterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_seqTxDoneInterrupt, name: "TXDONESEQIF")
                    .WithFlag(1, out FRC_seqTxAfterFrameDoneInterrupt, name: "TXAFTERFRAMEDONESEQIF")
                    .WithFlag(2, name: "TXABORTEDSEQIF")
                    .WithFlag(3, out FRC_seqTxUnderflowInterrupt, name: "TXUFSEQIF")
                    .WithFlag(4, out FRC_seqRxDoneInterrupt, name: "RXDONESEQIF")
                    .WithFlag(5, out FRC_seqRxAbortedInterrupt, name: "RXABORTEDSEQIF")
                    .WithFlag(6, out FRC_seqFrameErrorInterrupt, name: "FRAMEERRORSEQIF")
                    .WithFlag(7, name: "BLOCKERRORSEQIF")
                    .WithFlag(8, out FRC_seqRxOverflowInterrupt, name: "RXOFSEQIF")
                    .WithFlag(9, out FRC_seqWordCounterCompareInterrupt[0], name: "WCNTCMP0SEQIF")
                    .WithFlag(10, out FRC_seqWordCounterCompareInterrupt[1], name: "WCNTCMP1SEQIF")
                    .WithFlag(11, out FRC_seqWordCounterCompareInterrupt[2], name: "WCNTCMP2SEQIF")
                    .WithFlag(12, name: "ADDRERRORSEQIF")
                    .WithFlag(13, name: "BUSERRORSEQIF")
                    .WithFlag(14, out FRC_seqRxRawEventInterrupt, name: "RXRAWEVENTSEQIF")
                    .WithFlag(15, out FRC_seqTxRawEventInterrupt, name: "TXRAWEVENTSEQIF")
                    .WithFlag(16, name: "SNIFFOFSEQIF")
                    .WithFlag(17, name: "WCNTCMP3SEQIF")
                    .WithFlag(18, name: "WCNTCMP4SEQIF")
                    .WithFlag(19, name: "BOISETSEQIF")
                    .WithFlag(20, out FRC_seqPacketBufferStartInterrupt, name: "PKTBUFSTARTSEQIF")
                    .WithFlag(21, out FRC_seqPacketBufferThresholdInterrupt, name: "PKTBUFTHRESHOLDSEQIF")
                    .WithFlag(22, name: "RXRAWOFSEQIF")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, name: "FRAMEDETPAUSEDSEQIF")
                    .WithFlag(25, name: "INTERLEAVEWRITEPAUSEDSEQIF")
                    .WithFlag(26, name: "INTERLEAVEREADPAUSEDSEQIF")
                    .WithFlag(27, name: "TXSUBFRAMEPAUSEDSEQIF")
                    .WithFlag(28, name: "CONVPAUSEDSEQIF")
                    .WithFlag(29, name: "RXWORDSEQIF")
                    .WithFlag(30, name: "TXWORDSEQIF")
                    .WithReservedBits(31, 1)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.SequencerInterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out FRC_seqTxDoneInterruptEnable, name: "TXDONESEQIEN")
                    .WithFlag(1, out FRC_seqTxAfterFrameDoneInterruptEnable, name: "TXAFTERFRAMEDONESEQIEN")
                    .WithFlag(2, name: "TXABORTEDSEQIEN")
                    .WithFlag(3, out FRC_seqTxUnderflowInterruptEnable, name: "TXUFSEQIEN")
                    .WithFlag(4, out FRC_seqRxDoneInterruptEnable, name: "RXDONESEQIEN")
                    .WithFlag(5, out FRC_seqRxAbortedInterruptEnable, name: "RXABORTEDSEQIEN")
                    .WithFlag(6, out FRC_seqFrameErrorInterruptEnable, name: "FRAMEERRORSEQIEN")
                    .WithFlag(7, name: "BLOCKERRORSEQIEN")
                    .WithFlag(8, out FRC_seqRxOverflowInterruptEnable, name: "RXOFSEQIEN")
                    .WithFlag(9, out FRC_seqWordCounterCompareInterruptEnable[0], name: "WCNTCMP0SEQIEN")
                    .WithFlag(10, out FRC_seqWordCounterCompareInterruptEnable[1], name: "WCNTCMP1SEQIEN")
                    .WithFlag(11, out FRC_seqWordCounterCompareInterruptEnable[2], name: "WCNTCMP2SEQIEN")
                    .WithFlag(12, name: "ADDRERRORSEQIEN")
                    .WithFlag(13, name: "BUSERRORSEQIEN")
                    .WithFlag(14, out FRC_seqRxRawEventInterruptEnable, name: "RXRAWEVENTSEQIEN")
                    .WithFlag(15, out FRC_seqTxRawEventInterruptEnable, name: "TXRAWEVENTSEQIEN")
                    .WithFlag(16, name: "SNIFFOFSEQIEN")
                    .WithFlag(17, name: "WCNTCMP3SEQIEN")
                    .WithFlag(18, name: "WCNTCMP4SEQIEN")
                    .WithFlag(19, name: "BOISETSEQIEN")
                    .WithFlag(20, out FRC_seqPacketBufferStartInterruptEnable, name: "PKTBUFSTARTSEQIEN")
                    .WithFlag(21, out FRC_seqPacketBufferThresholdInterruptEnable, name: "PKTBUFTHRESHOLDSEQIEN")
                    .WithFlag(22, name: "RXRAWOFSEQIEN")
                    .WithReservedBits(23, 1)
                    .WithFlag(24, name: "FRAMEDETPAUSEDSEQIEN")
                    .WithFlag(25, name: "INTERLEAVEWRITEPAUSEDSEQIEN")
                    .WithFlag(26, name: "INTERLEAVEREADPAUSEDSEQIEN")
                    .WithFlag(27, name: "TXSUBFRAMEPAUSEDSEQIEN")
                    .WithFlag(28, name: "CONVPAUSEDSEQIEN")
                    .WithFlag(29, name: "RXWORDSEQIEN")
                    .WithFlag(30, name: "TXWORDSEQIEN")
                    .WithReservedBits(31, 1)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.RawDataControl, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "TXRAWMODE")
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
                    .WithFlag(13, name: "DEMODRAWDATAMUX")
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
                    .WithReservedBits(25, 7)
                },
                {(long)FrameControllerRegisters.PacketCaptureBufferStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 6, out FRC_packetBufferCount, FieldMode.Read, name: "PKTBUFCOUNT")
                    .WithReservedBits(6, 24)
                },
                {(long)FrameControllerRegisters.SnifferControl, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "SNIFFMODE")
                    .WithFlag(2, name: "SNIFFBITS")
                    .WithFlag(3, out FRC_ptiEmitRx, name: "SNIFFRXDATA")
                    .WithFlag(4, out FRC_ptiEmitTx, name: "SNIFFTXDATA")
                    .WithFlag(5, out FRC_ptiEmitRssi, name: "SNIFFRSSI")
                    .WithFlag(6, out FRC_ptiEmitState, name: "SNIFFSTATE")
                    .WithFlag(7, out FRC_ptiEmitAux, name: "SNIFFAUXDATA")
                    .WithValueField(8, 8, name: "SNIFFBR")
                    .WithFlag(16, name: "SNIFFSLEEPCTRL")
                    .WithFlag(17, out FRC_ptiEmitSyncWord, name: "SNIFFSYNCWORD")
                    .WithReservedBits(18, 14)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)FrameControllerRegisters.AuxiliarySnifferDataOutput, new DoubleWordRegister(this)
                    .WithValueField(0, 9, writeCallback: (_, data) => {
                        if (FRC_ptiEmitAux.Value)
                        {
                            this.Log(LogLevel.Noisy, "PTI AUX: {0}", data);
                            // chop off the top bit
                            byte b = (byte) (data & 0xFF);
                            PtiDataOut?.Invoke(new byte[] { b });
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

                registerDictionary.Add(startOffset + blockSize * i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, out FRC_frameDescriptor[i].WordsField, name: "WORDS")
                        .WithValueField(8, 2, out FRC_frameDescriptor[i].BufferField, name: "BUFFER")
                        .WithFlag(10, out FRC_frameDescriptor[i].IncludeCrc, name: "INCLUDECRC")
                        .WithFlag(11, out FRC_frameDescriptor[i].CalculateCrc, name: "CALCCRC")
                        .WithValueField(12, 2, out FRC_frameDescriptor[i].CrcSkipWords, name: "SKIPCRC")
                        .WithFlag(14, out FRC_frameDescriptor[i].SkipWhitening, name: "SKIPWHITE")
                        .WithFlag(15, out FRC_frameDescriptor[i].AddTrailData, name: "ADDTRAILTXDATA")
                        .WithFlag(16, out FRC_frameDescriptor[i].ExcludeSubframeFromWcnt, name: "EXCLUDESUBFRAMEWCNT")
                        .WithReservedBits(17, 15)
                );
            }

            startOffset = (long)FrameControllerRegisters.PacketCaptureDataBuffer0;
            blockSize = (long)FrameControllerRegisters.PacketCaptureDataBuffer1 - (long)FrameControllerRegisters.PacketCaptureDataBuffer0;
            for(var index = 0; index < (FRC_PacketBufferCaptureSize / 4); index++)
            {
                var i = index;

                registerDictionary.Add(startOffset + blockSize * i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i * 4], writeCallback: (_, value) => FRC_packetBufferCapture[i * 4] = (byte)value, name: $"PKTBUF{i * 4}")
                        .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i * 4 + 1], writeCallback: (_, value) => FRC_packetBufferCapture[i * 4 + 1] = (byte)value, name: $"PKTBUF{i * 4 + 1}")
                        .WithValueField(16, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i * 4 + 2], writeCallback: (_, value) => FRC_packetBufferCapture[i * 4 + 2] = (byte)value, name: $"PKTBUF{i * 4 + 2}")
                        .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ => FRC_packetBufferCapture[i * 4 + 3], writeCallback: (_, value) => FRC_packetBufferCapture[i * 4 + 3] = (byte)value, name: $"PKTBUF{i * 4 + 3}")
                );
            }

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildCyclicRedundancyCheckRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)CyclicRedundancyCheckRegisters.Control, new DoubleWordRegister(this)
                    .WithFlag(0, name: "INPUTINV")
                    .WithFlag(1, name: "OUTPUTINV")
                    .WithEnumField<DoubleWordRegister, CRC_CrcWidthMode>(2, 2, out CRC_crcWidthMode, name: "CRCWIDTH")
                    .WithReservedBits(4, 1)
                    .WithFlag(5, name: "INPUTBITORDER")
                    .WithFlag(6, out CRC_reverseCrcByteOrdering, name: "BYTEREVERSE")
                    .WithFlag(7, name: "BITREVERSE")
                    .WithValueField(8, 4, out CRC_crcBitsPerWord, name: "BITSPERWORD")
                    .WithFlag(12, name: "PADCRCINPUT")
                    .WithReservedBits(13, 19)
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildRadioMailboxRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)RadioMailboxRegisters.MessagePointer0, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[0], name: "MSGPTR0")
                },
                {(long)RadioMailboxRegisters.MessagePointer1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[1], name: "MSGPTR1")
                },
                {(long)RadioMailboxRegisters.MessagePointer2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[2], name: "MSGPTR2")
                },
                {(long)RadioMailboxRegisters.MessagePointer3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out RFMAILBOX_messagePointer[3], name: "MSGPTR3")
                },
                {(long)RadioMailboxRegisters.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out RFMAILBOX_messageInterrupt[0], name: "MBOXIF0")
                    .WithFlag(1, out RFMAILBOX_messageInterrupt[1], name: "MBOXIF1")
                    .WithFlag(2, out RFMAILBOX_messageInterrupt[2], name: "MBOXIF2")
                    .WithFlag(3, out RFMAILBOX_messageInterrupt[3], name: "MBOXIF3")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)HostMailboxRegisters.InterruptEnable, new DoubleWordRegister(this)
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

        //-------------------------------------------------------------
        // PROTIMER private methods

        private void PROTIMER_ListenBeforeTalkCcaCompleted(bool forceFailure = false)
        {
            if(PROTIMER_listenBeforeTalkState == PROTIMER_ListenBeforeTalkState.Idle)
            {
                this.Log(LogLevel.Error, "PROTIMER_ListenBeforeTalkCcaCompleted while LBT_STATE=idle");
                return;
            }

            if(forceFailure)
            {
                AGC_cca.Value = false;
            }
            else
            {
                // While the radio is receiving a preamble/sync word or a frame the channel is, by definition,
                // busy: real hardware's AGC/RSSI is locked onto that signal. The interference-queue RSSI is
                // reports the channel clear once the sender's over-the-air entry goes stale (the sender marks 
                // it stale at its own TX-end). Because each node runs on an independent time source that only 
                // syncs at quantum boundaries, the sender's entry can go stale while this receiver is still 
                // in the RX process, so the RSSI snapshot reads below the CCA threshold mid-frame. 
                // Gating CCA on the internal Rx state restores the "RX in progress => busy" invariant.
                AGC_cca.Value = (AGC_RssiIntegerPartAdjusted < (sbyte)AGC_ccaThreshold.Value)
                                && (RAC_internalRxState == RAC_InternalRxState.Idle);
            }

            // If the channel is clear, nothing to do here, we let CCADELAY complete.

            // Channel not clear    
            if(!AGC_cca.Value)
            {
                PROTIMER_timeoutCounter[0].Stop();
                PROTIMER_TriggerEvent(PROTIMER_Event.ClearChannelAssessmentMeasurementCompleted);

                // RETRYCNT == RETRYLIMIT?
                if(PROTIMER_listenBeforeTalkRetryCounter.Value == PROTIMER_retryLimit.Value)
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

                    // EXP - min(EXP+1, MAXEXP). With FIXEDBACKOFF, hardware does not update EXP.
                    if(!PROTIMER_fixedBackoff.Value)
                    {
                        if(PROTIMER_listenBeforeTalkExponent.Value + 1 <= PROTIMER_listenBeforeTalkMaxExponent.Value)
                        {
                            PROTIMER_listenBeforeTalkExponent.Value += 1;
                        }
                        else
                        {
                            PROTIMER_listenBeforeTalkExponent.Value = PROTIMER_listenBeforeTalkMaxExponent.Value;
                        }
                    }

                    // BACKOFF = RANDOM & (2^EXP – 1)
                    var rand = PROTIMER_fixedBackoff.Value ? PROTIMER_fixedBackoffRandomValue : (uint)random.Next();
                    var backoff = (rand & ((1u << (byte)PROTIMER_listenBeforeTalkExponent.Value) - 1));

                    // Wait for BACKOFF+1 BASECNTOF events
                    PROTIMER_timeoutCounter[0].CounterTop.Value = backoff;
                    PROTIMER_timeoutCounter[0].Start();
                }
            }
        }

        private void PROTIMER_ListenBeforeTalkStopCommand()
        {
            PROTIMER_timeoutCounter[0].Stop();
            PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Idle;
            PROTIMER_listenBeforeTalkSync.Value = false;
            PROTIMER_listenBeforeTalkRunning.Value = false;
        }

        private void PROTIMER_ListenBeforeTalkPauseCommand()
        {
            this.Log(LogLevel.Error, "LBT Pausing not supported");
        }

        private void PROTIMER_ListenBeforeTalkStartCommand()
        {
            if(PROTIMER_timeoutCounter[0].Running.Value || PROTIMER_timeoutCounter[0].Synchronizing.Value)
            {
                PROTIMER_listenBeforeTalkPending = true;
                return;
            }

            PROTIMER_listenBeforeTalkSync.Value = false;
            PROTIMER_listenBeforeTalkRunning.Value = false;
            PROTIMER_listenBeforeTalkPaused.Value = false;
            // EXP = STARTEXP. With FIXEDBACKOFF, hardware does not load EXP from STARTEXP.
            if(!PROTIMER_fixedBackoff.Value)
            {
                PROTIMER_listenBeforeTalkExponent.Value = PROTIMER_listenBeforeTalkStartExponent.Value;
            }
            // RETRYCNT = 0
            PROTIMER_listenBeforeTalkRetryCounter.Value = 0;

            PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Backoff;

            if(PROTIMER_timeoutCounter[0].SyncSource.Value == PROTIMER_TimeoutCounterSource.Disabled)
            {
                PROTIMER_listenBeforeTalkRunning.Value = true;
            }
            else
            {
                PROTIMER_listenBeforeTalkSync.Value = true;
            }

            // BACKOFF = RANDOM & (2^EXP – 1)
            var rand = PROTIMER_fixedBackoff.Value ? PROTIMER_fixedBackoffRandomValue : (uint)random.Next();
            var backoff = (rand & ((1u << (byte)PROTIMER_listenBeforeTalkExponent.Value) - 1));

            // Wait for BACKOFF+1 BASECNTOF events
            PROTIMER_timeoutCounter[0].CounterTop.Value = backoff;
            PROTIMER_timeoutCounter[0].Start();
        }

        private void PROTIMER_TimeoutCounter0HandleFinish()
        {
            if(PROTIMER_listenBeforeTalkPending)
            {
                PROTIMER_listenBeforeTalkPending = false;
                PROTIMER_ListenBeforeTalkStartCommand();
            }
        }

        // A compare match requires every comparator enabled in CCn_CTRL to match on the same tick.
        // BASE and WRAP are the low and high words of a single 64-bit compare, not two independent
        // compares: sequencer firmware arms a channel from a 64-bit ptick (PROTIMER_CCTimerStartPtick
        // writes WRAP=ptick>>32, BASE=ptick&0xFFFFFFFF with WRAPMATCHEN|BASEMATCHEN), so matching on
        // either half on its own fires up to a full BASECNT cycle early.
        // PREMATCHEN is not modelled here, see PROTIMER_UpdateCompareTimer.
        private bool PROTIMER_CaptureCompareMatches(uint index)
        {
            var channel = PROTIMER_captureCompareChannel[index];

            if(!channel.Enable.Value || channel.Mode.Value != PROTIMER_CaptureCompareMode.Compare)
            {
                return false;
            }

            // With no comparator enabled there is nothing to match against.
            if(!channel.BaseMatchEnable.Value && !channel.WrapMatchEnable.Value)
            {
                return false;
            }

            if(channel.BaseMatchEnable.Value && channel.BaseValue.Value != PROTIMER_baseCounterValue)
            {
                return false;
            }

            if(channel.WrapMatchEnable.Value && channel.WrapValue.Value != PROTIMER_wrapCounterValue)
            {
                return false;
            }

            return true;
        }

        // Latches the live BASECNT/WRAPCNT into every enabled capture channel whose input source matches the
        // detected sync word.
#pragma warning disable IDE0060
        private void PROTIMER_CaptureOnSyncWordsDetected(bool foundSyncWord0, bool foundSyncWord1, bool foundSyncWord2, bool foundSyncWord3)
#pragma warning restore IDE0060
        {
            for(var i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                var channel = PROTIMER_captureCompareChannel[i];
                if(!channel.Enable.Value || channel.Mode.Value != PROTIMER_CaptureCompareMode.Capture)
                {
                    continue;
                }

                var triggered = false;
                switch(channel.CaptureInputSource.Value)
                {
                case PROTIMER_CaptureInputSource.DemodulatorFoundSyncWord0:
                    triggered = foundSyncWord0;
                    break;
                case PROTIMER_CaptureInputSource.DemodulatorFoundSyncWord1:
                    triggered = foundSyncWord1;
                    break;
                case PROTIMER_CaptureInputSource.DemodulatorFoundSyncWord0or1:
                    triggered = foundSyncWord0 || foundSyncWord1;
                    break;
                }

                if(triggered)
                {
                    channel.Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                    PROTIMER_TriggerEvent((PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel0Event + i));
                }
            }
        }

        // Evaluates the channels whose compare includes BASECNT. Compound BASE+WRAP compares are
        // only evaluated here (and never from PROTIMER_IncrementWrapCounter) because BASECNT is the
        // comparator that advances last: WRAPCNT is a qualifier that has to already hold the target
        // value by the time BASECNT reaches it.
        private void PROTIMER_CheckCaptureCompareBaseMatches()
        {
            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].BaseMatchEnable.Value && PROTIMER_CaptureCompareMatches(i))
                {
                    PROTIMER_HandleCaptureCompareMatch(i);
                }
            }
        }

        private void PROTIMER_HandleCaptureCompareMatch(uint index)
        {
            PROTIMER_captureCompareChannel[index].InterruptField.Value = true;
            PROTIMER_captureCompareChannel[index].SeqInterruptField.Value = true;
            UpdateInterrupts();
            PROTIMER_TriggerEvent((PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel0Event + index));
        }

        private void PROTIMER_IncrementBaseCounter(uint increment = 1)
        {
            if(proTimer.Enabled)
            {
                this.Log(LogLevel.Error, "PROTIMER_IncrementBaseCounter invoked while the proTimer running");
                return;
            }

            PROTIMER_baseCounterValue += (ushort)increment;

            // BASECNT == BASECNTTOP is only observable here, before the counter rolls over.
            PROTIMER_CheckCaptureCompareBaseMatches();

            if(PROTIMER_baseCounterValue >= PROTIMER_baseCounterTop.Value)
            {
                PROTIMER_HandleBaseCounterOverflow();
                PROTIMER_baseCounterValue = 0x0;

                // Re-evaluate against the rolled-over BASECNT and the WRAPCNT the overflow advanced,
                // so a target sitting at the start of a wrap is not missed.
                PROTIMER_CheckCaptureCompareBaseMatches();
            }
        }

        private void PROTIMER_TimeoutCounter0HandleSynchronize()
        {
            if(PROTIMER_listenBeforeTalkSync.Value)
            {
                PROTIMER_listenBeforeTalkSync.Value = false;
                PROTIMER_listenBeforeTalkRunning.Value = true;
            }
        }

        private void PROTIMER_UpdateCompareTimer(int index)
        {
            // We don't support preMatch in Compare Timers, instead we checks that preMatch is not enabled, 
            // and if base/wrap match are enabled, we recalculate the protimer limit.
            if(PROTIMER_captureCompareChannel[index].Enable.Value
                && PROTIMER_captureCompareChannel[index].Mode.Value == PROTIMER_CaptureCompareMode.Compare
                && PROTIMER_captureCompareChannel[index].PreMatchEnable.Value)
            {
                this.Log(LogLevel.Error, "CC{0} PRE match enabled, NOT SUPPORTED!", index);
            }

            PROTIMER_HandleChangedParams();
        }

        private void PROTIMER_IncrementWrapCounter(uint increment = 1)
        {
            if(proTimer.Enabled)
            {
                this.Log(LogLevel.Error, "PROTIMER_IncrementWrapCounter invoked while the proTimer running");
                return;
            }

            PROTIMER_wrapCounterValue += increment;

            for(var i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                // Channels that also compare BASECNT are handled by PROTIMER_CheckCaptureCompareBaseMatches().
                if(!PROTIMER_captureCompareChannel[i].BaseMatchEnable.Value
                    && PROTIMER_captureCompareChannel[i].WrapMatchEnable.Value
                    && PROTIMER_CaptureCompareMatches((uint)i))
                {
                    PROTIMER_HandleCaptureCompareMatch((uint)i);
                }
            }

            if(PROTIMER_wrapCounterValue >= PROTIMER_wrapCounterTop.Value)
            {
                PROTIMER_HandleWrapCounterOverflow();
                PROTIMER_wrapCounterValue = 0x0;
            }
        }

        private void PROTIMER_UpdateRxRequestState()
        {
            if(PROTIMER_rxClearEvent1.Value == PROTIMER_Event.Always
                && PROTIMER_rxClearEvent2.Value == PROTIMER_Event.Always)
            {
                PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
            }
            else if(PROTIMER_rxSetEvent1.Value == PROTIMER_Event.Always
                     && PROTIMER_rxSetEvent2.Value == PROTIMER_Event.Always)
            {
                PROTIMER_TriggerEvent(PROTIMER_Event.InternalTrigger);
            }
        }

        private void PROTIMER_HandleWrapCounterOverflow()
        {
            //this.Log(LogLevel.Debug, "PROTIMER_HandleWrapCounterOverflow wrapValue={0} at {1}", PROTIMER_WrapCounterValue, GetTime());

            PROTIMER_TriggerEvent(PROTIMER_Event.WrapCounterOverflow);

            Array.ForEach(PROTIMER_timeoutCounter, x => x.Update(PROTIMER_TimeoutCounterSource.WrapCounterOverflow));
        }

        private void PROTIMER_HandleBaseCounterOverflow()
        {
            // this.Log(LogLevel.Debug, "PROTIMER_HandleBaseCounterOverflow baseValue={0} topValue={1} at {2}", 
            //          PROTIMER_BaseCounterValue, PROTIMER_baseCounterTop.Value, GetTime());

            PROTIMER_TriggerEvent(PROTIMER_Event.BaseCounterOverflow);

            if(PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.BaseCounterOverflow)
            {
                PROTIMER_IncrementWrapCounter();
            }

            Array.ForEach(PROTIMER_timeoutCounter, x => x.Update(PROTIMER_TimeoutCounterSource.BaseCounterOverflow));
        }

        private void PROTIMER_HandlePreCounterOverflow()
        {
            PROTIMER_TriggerEvent(PROTIMER_Event.PreCounterOverflow);

            if(PROTIMER_baseCounterSource.Value == PROTIMER_BaseCounterSource.PreCounterOverflow)
            {
                PROTIMER_IncrementBaseCounter();
            }
            if(PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.PreCounterOverflow)
            {
                PROTIMER_IncrementWrapCounter();
            }

            Array.ForEach(PROTIMER_timeoutCounter, x => x.Update(PROTIMER_TimeoutCounterSource.PreCounterOverflow));

            UpdateInterrupts();
        }

        private void PROTIMER_HandleTimerLimitReached()
        {
            proTimer.Enabled = false;

            // In lightweight mode the timer fires when N PRECNT overflows have occurred. 
            // The number N is set when we start/restart the proTimer

            //this.Log(LogLevel.Debug, "proTimer overflow limit={0} baseTop={1} wrapTop={2}", proTimer.Limit, PROTIMER_baseCounterTop.Value, PROTIMER_wrapCounterTop.Value);

            PROTIMER_HandlePreCntOverflows((uint)proTimer.Limit);

            proTimer.Value = 0;
            proTimer.Limit = PROTIMER_ComputeTimerLimit();
            proTimer.Enabled = true;
        }

        private void PROTIMER_HandlePreCntOverflows(uint overflowCount)
        {
            if(proTimer.Enabled)
            {
                this.Log(LogLevel.Error, "PROTIMER_HandlePreCntOverflows invoked while the proTimer running");
                return;
            }

            // this.Log(LogLevel.Debug, "PROTIMER_HandlePreCntOverflows cnt={0} mask=0x{1:X} base={2}", 
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
                if((PROTIMER_preCounterSourcedBitmask & ((uint)PROTIMER_PreCountOverflowSourced.TimeoutCounter0 << i)) > 0)
                {
                    PROTIMER_timeoutCounter[i].Update(PROTIMER_TimeoutCounterSource.PreCounterOverflow, overflowCount);
                }
            }

            // TODO: for now we don't handle CaptureCompare channels being sourced by PreCount
        }

        private uint PROTIMER_ComputeTimerLimit()
        {
            if(proTimer.Enabled)
            {
                this.Log(LogLevel.Error, "PROTIMER_ComputeTimerLimit invoked while the proTimer running");
                return uint.MaxValue;
            }

            uint limit = PROTIMER_DefaultLightWeightTimerLimit;
            PROTIMER_preCounterSourcedBitmask = 0;

            if(PROTIMER_baseCounterSource.Value == PROTIMER_BaseCounterSource.PreCounterOverflow)
            {
                if(PROTIMER_baseCounterValue > PROTIMER_baseCounterTop.Value)
                {
                    this.Log(LogLevel.Error, "BASECNT > BASECNTTOP {0} {1}", PROTIMER_baseCounterValue, PROTIMER_baseCounterTop.Value);
                    return uint.MaxValue;
                }

                uint temp = (uint)PROTIMER_baseCounterTop.Value - PROTIMER_baseCounterValue;
                if(temp != 0 && temp < limit)
                {
                    limit = temp;
                }
                PROTIMER_preCounterSourcedBitmask |= (uint)PROTIMER_PreCountOverflowSourced.BaseCounter;
            }

            if(PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.PreCounterOverflow)
            {
                if(PROTIMER_wrapCounterValue > PROTIMER_wrapCounterTop.Value)
                {
                    this.Log(LogLevel.Error, "WRAPCNT > WRAPCNTTOP {0} {1}", PROTIMER_wrapCounterValue, PROTIMER_wrapCounterTop.Value);
                    return uint.MaxValue;
                }

                uint temp = (uint)PROTIMER_wrapCounterTop.Value - PROTIMER_wrapCounterValue;
                if(temp != 0 && temp < limit)
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
                if((PROTIMER_timeoutCounter[i].Synchronizing.Value
                     && PROTIMER_timeoutCounter[i].SyncSource.Value == PROTIMER_TimeoutCounterSource.PreCounterOverflow)
                    || (PROTIMER_timeoutCounter[i].Running.Value
                        && PROTIMER_timeoutCounter[i].Source.Value == PROTIMER_TimeoutCounterSource.PreCounterOverflow))
                {
                    limit = PROTIMER_MinimumTimeoutCounterDelay;
                    PROTIMER_preCounterSourcedBitmask |= ((uint)PROTIMER_PreCountOverflowSourced.TimeoutCounter0 << i);
                }
            }

            // Check for Capture/Compare channels that are enabled and set in Compare mode
            for(int i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; i++)
            {
                var channel = PROTIMER_captureCompareChannel[i];

                if(!channel.Enable.Value || channel.Mode.Value != PROTIMER_CaptureCompareMode.Compare)
                {
                    continue;
                }

                var compoundCompare = channel.BaseMatchEnable.Value && channel.WrapMatchEnable.Value;

                // A compound BASE+WRAP compare cannot match before WRAPCNT reaches the target wrap,
                // so while the wrap words differ the BASE target is not an instant of interest. The
                // limit is still bounded by the BASECNT overflow that advances WRAPCNT, which brings
                // us back here once the wrap words match.
                if(compoundCompare && channel.WrapValue.Value != PROTIMER_wrapCounterValue)
                {
                    continue;
                }

                // Base match enabled and base counter is sourced by pre counter overflows
                if(channel.BaseMatchEnable.Value
                    && PROTIMER_baseCounterSource.Value == PROTIMER_BaseCounterSource.PreCounterOverflow
                    && channel.BaseValue.Value > PROTIMER_baseCounterValue)
                {
                    uint temp = (uint)(channel.BaseValue.Value - PROTIMER_baseCounterValue);
                    if(temp < limit)
                    {
                        limit = temp;
                    }
                    PROTIMER_preCounterSourcedBitmask |= ((uint)PROTIMER_PreCountOverflowSourced.CaptureCompareChannel0 << i);
                }

                // Wrap match enabled and wrap counter is sourced by pre counter overflows. For a
                // compound compare the BASE target above already covers the match instant.
                if(channel.WrapMatchEnable.Value && !compoundCompare
                    && PROTIMER_wrapCounterSource.Value == PROTIMER_WrapCounterSource.PreCounterOverflow
                    && channel.WrapValue.Value > PROTIMER_wrapCounterValue)
                {
                    uint temp = (uint)(channel.WrapValue.Value - PROTIMER_wrapCounterValue);
                    if(temp < limit)
                    {
                        limit = temp;
                    }
                    PROTIMER_preCounterSourcedBitmask |= ((uint)PROTIMER_PreCountOverflowSourced.CaptureCompareChannel0 << i);
                }
            }

            return limit;
        }

        private void PROTIMER_TimeoutCounter0HandleUnderflow()
        {
            if(!PROTIMER_listenBeforeTalkRunning.Value)
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
                if(AGC_RssiStartCommand(true))
                {
                    // Wait for CCDELAY+1 BASECNTOF events
                    PROTIMER_timeoutCounter[0].CounterTop.Value = PROTIMER_ccaDelay.Value;
                    PROTIMER_timeoutCounter[0].Start();
                }

                break;
            }
            case PROTIMER_ListenBeforeTalkState.CcaDelay:
            {
                // If we get here is because CCA was successful, otherwise we would have retried the backoff or failed LBT

                // RX in progress => channel busy. The RSSI-based CcaCompleted path already enforces this
                // invariant (gating CCA on RxFrame), but the CCADELAY TOUT0 underflow can race ahead of it.
                if(RAC_internalRxState != RAC_InternalRxState.Idle)
                {
                    PROTIMER_ListenBeforeTalkCcaCompleted(forceFailure: true);
                    break;
                }

                // CCACNT == CCAREPEAT-1
                if(PROTIMER_ccaCounter.Value == (PROTIMER_ccaRepeat.Value - 1))
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
                    if(AGC_RssiStartCommand(true))
                    {
                        // Wait for CCDELAY+1 BASECNTOF events
                        PROTIMER_timeoutCounter[0].CounterTop.Value = PROTIMER_ccaDelay.Value;
                        PROTIMER_timeoutCounter[0].Start();
                    }
                }

                break;
            }
            default:
            {
                this.Log(LogLevel.Error, "Unreachable. Invalid LBT state in PROTIMER_TimeoutCounter0HandleUnderflow");
                break;
            }
            }
        }

        private void PROTIMER_UpdateTxRequestState()
        {
            if(PROTIMER_txSetEvent1.Value == PROTIMER_Event.Disabled
                && PROTIMER_txSetEvent2.Value == PROTIMER_Event.Disabled)
            {
                PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Idle;
            }
        }

        private void PROTIMER_RtcSyncStart()
        {
            this.Log(LogLevel.Debug, "PROTIMER RTC Start at {0}, halting PROTIMER and entering RTCWAIT state", GetTime());
            PROTIMER_Enabled = false;
            PROTIMER_rtcWait = true;
        }

        private void PROTIMER_RtcCapture(uint channel)
        {
            if(channel > 1)
            {
                this.Log(LogLevel.Error, "PROTIMER_RtcCapture: invalid channel {0} at {1}", channel, GetTime());
                return;
            }

            for(var i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].Enable.Value
                    && PROTIMER_captureCompareChannel[i].Mode.Value == PROTIMER_CaptureCompareMode.Capture
                    && PROTIMER_captureCompareChannel[i].CaptureInputSource.Value == (PROTIMER_CaptureInputSource)((uint)PROTIMER_CaptureInputSource.ProRtcCaptureCompare0 + channel))
                {
                    PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                    PROTIMER_TriggerEvent((PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel0Event + i));
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

        //-------------------------------------------------------------
        // MODEM private methods

        private uint MODEM_GetTxChainDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_BlePhyTxChainDelayNanoS : MODEM_802154PhyTxChainDelayNanoS;
        }

        private double MODEM_GetRxDoneDelayUs()
        {
            return ((double)MODEM_GetRxDoneDelayNanoS()) / 1000;
        }

        private uint MODEM_GetDataRate()
        {
            double numerator = MODEM_txBaudrateNumerator.Value * 1.0;
            double denominator = MODEM_txBaudrateDenominator.Value * 1.0;
            double ratio = numerator / denominator;
            double txBaudrate = (double)HfxoFrequency /  (8.0 * ratio);
            double chipsPerSymbol = MODEM_dsssLength.Value + 1;
            double symbolsPerBit = 1.0;
            uint dsssShiftedSymbols = 0;

            // TODO - If want to add Dynamic rate selection support:
            // Case statement selecting brdiv to be brdiva + 1 or brdivb + 1 from control1 register.
            // The condition to make distinction upon is based on Modem_rateSelectMode also in control1.
            // May need to find a way to identify whether data is header or payload and there is one situation where the brdiv can switch per symbol.
            // In ocelot radio calculator they do not do this:
            // double txBaudrate = (double)HfxoFrequency /  (8.0 * ratio * brdiv);

            // Find shifted symbols
            if(MODEM_dsssShifts.Value == 1)
            {
                // Dsslen will be max 8 due to DSSS setup restrictions
                // A bitslice as is shown in dsssShiftedSymbols = dssslen[CORRINDEX_WIDTH-1:0]; where corrindex_width is 5
                dsssShiftedSymbols = (uint)MODEM_dsssLength.Value & 0x1F;
            }
            else if(MODEM_dsssShifts.Value > 1)
            {
                dsssShiftedSymbols = (uint)MODEM_dsssLength.Value >> (int)(MODEM_dsssShifts.Value - 1);
            }

            if((MODEM_modulationFormat.Value == MODEM_ModulationFormat.FSK4))
            {
                symbolsPerBit = 2.0;
            }
            else if(MODEM_symbolCoding.Value == MODEM_SymbolCoding.Dsss)
            {
                // Convert to bits per symbol
                switch((MODEM_DsssShiftedSymbols)dsssShiftedSymbols)
                {
                case MODEM_DsssShiftedSymbols.ShiftedSymbol0:
                {
                    symbolsPerBit = 1.0;
                    break;
                }
                case MODEM_DsssShiftedSymbols.ShiftedSymbol1:
                {
                    if(MODEM_dsssDoublingMode.Value == MODEM_DsssDoublingMode.Disabled)
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

        // Register-derived PHY signature used for over-the-air sensing/decoding compatibility.
        private PhySignature MODEM_GetPhySignature()
        {
            var coding = MODEM_GetRadioSymbolCoding();
            bool dsss = coding == RadioSymbolCoding.Dsss;
            uint dsssLength = dsss ? (uint)MODEM_dsssLength.Value : 0u;
            uint dsssShifts = dsss ? (uint)MODEM_dsssShifts.Value : 0u;
            uint dsssDoublingMode = dsss ? (uint)MODEM_dsssDoublingMode.Value : 0u;
            return new PhySignature(MODEM_GetModulationFamily(), coding, MODEM_GetDataRate(),
                dsssLength, dsssShifts, dsssDoublingMode, MODEM_GetCodedSymbolsPerInfoBit());
        }

        private RadioSymbolCoding MODEM_GetRadioSymbolCoding()
        {
            switch(MODEM_symbolCoding.Value)
            {
            case MODEM_SymbolCoding.Nrz:
                return RadioSymbolCoding.Nrz;
            case MODEM_SymbolCoding.Manchester:
                return RadioSymbolCoding.Manchester;
            case MODEM_SymbolCoding.Dsss:
                return RadioSymbolCoding.Dsss;
            case MODEM_SymbolCoding.Linecode:
                return RadioSymbolCoding.Linecode;
            default:
                return RadioSymbolCoding.Nrz;
            }
        }

        private RadioModulationFamily MODEM_GetModulationFamily()
        {
            switch(MODEM_modulationFormat.Value)
            {
            case MODEM_ModulationFormat.FSK2:
            case MODEM_ModulationFormat.FSK4:
                return RadioModulationFamily.Fsk;
            case MODEM_ModulationFormat.MSK:
                return RadioModulationFamily.Msk;
            case MODEM_ModulationFormat.OQPSK:
                return RadioModulationFamily.Oqpsk;
            case MODEM_ModulationFormat.BPSK:
            case MODEM_ModulationFormat.DBPSK:
                return RadioModulationFamily.Bpsk;
            case MODEM_ModulationFormat.OOKASK:
                return RadioModulationFamily.Ook;
            default:
                return RadioModulationFamily.Unknown;
            }
        }

        // BLE LE Coded (Long Range) spreads each information bit into C on-air symbols (the FEC convolutional
        // encoder plus the CI-selected pattern mapper): C=8 for 125 kbps (S=8) and C=2 for 500 kbps (S=2).
        // The symbol/baud rate stays at 1 Msym/s so the information rate is baud_rate / C and the on-air
        // duration of the FEC-coded part of the frame is C times longer than the uncoded byte count would suggest.
        // Both sub-rates share an identical MODEM analog config; they are distinguished only by the FRC, which
        // selects the alternate FEC spreading path (CODINGB) for S=2 via FRC.CTRL.RATESELECT (0 => S=8, 2 => S=2).
        private uint MODEM_GetCodedSymbolsPerInfoBit()
        {
            if(MODEM_GetRadioSymbolCoding() != RadioSymbolCoding.Linecode)
            {
                return 1u;
            }
            // While receiving, mirror the coding sub-rate the sender actually used. The receiver's own
            // FRC.CTRL.RATESELECT does not reflect the incoming frame's sub-rate (the receiver only learns
            // it from the on-air CI).
            if(RAC_internalRxState != RAC_InternalRxState.Idle && currentFrameCodedSymbolsPerInfoBit != 0)
            {
                return currentFrameCodedSymbolsPerInfoBit;
            }

            return FRC_rateSelect.Value == FRC_RateSelectCodingB ? 2u : 8u;
        }

        // The passed frame is assumed to NOT include the preamble and to include the SYNC WORD.
        private double MODEM_GetFrameOverTheAirTimeUs(byte[] frame, bool includePreamble, bool includeSyncWord)
        {
            // For the LE Coded PHY the frame bytes (SYNC WORD + PDU + CRC) are FEC-coded, so each information
            // bit occupies C on-air symbols. The preamble is not FEC-coded and stays at the symbol rate.
            uint payloadCodingFactor = MODEM_GetCodedSymbolsPerInfoBit();
            uint syncBits = MODEM_GetSyncWordLengthInBits();
            uint payloadBits = (uint)frame.Length*8 - syncBits;

            double symbols = (double)payloadBits * payloadCodingFactor;

            if(includeSyncWord)
            {
                symbols += MODEM_GetSyncWordOverTheAirTimeUs() * MODEM_GetDataRate() / 1000000.0;
            }

            if(includePreamble)
            {
                symbols += MODEM_GetPreambleOverTheAirTimeUs() * MODEM_GetDataRate() / 1000000.0;
            }

            return symbols * 1000000 / (double)MODEM_GetDataRate();
        }

        private double MODEM_GetByteOverTheAirTimeUs()
        {
            return 8.0 * MODEM_GetCodedSymbolsPerInfoBit() * 1000000 / MODEM_GetDataRate();
        }

        private double MODEM_GetSyncWordOverTheAirTimeUs()
        {
            // LE Coded PHY: the access address (sync word) is part of FEC Block 1, which is ALWAYS coded at
            // S=8, independent of the payload sub-rate (S=2 for 500k, S=8 for 125k).
            uint syncCodingFactor = MODEM_GetRadioSymbolCoding() == RadioSymbolCoding.Linecode
                ? 8u
                : MODEM_GetCodedSymbolsPerInfoBit();
            return (double)MODEM_GetSyncWordLengthInBits() * syncCodingFactor * 1000000 / (double)MODEM_GetDataRate();
        }

        private double MODEM_GetPreambleOverTheAirTimeUs()
        {
            // LE Coded PHY: the preamble is a fixed 80us, sent at the 1 Msym/s base rate and independent
            // of the S=2/S=8 payload sub-rate (BLE spec).
            if(MODEM_GetRadioSymbolCoding() == RadioSymbolCoding.Linecode)
            {
                return MODEM_BleCodedPhyPreambleOverTheAirTimeUs;
            }
            return (double)MODEM_GetPreambleLengthInBits() * 1000000 / (double)MODEM_GetDataRate();
        }

        private double MODEM_GetTxChainDoneDelayUs()
        {
            return ((double)MODEM_GetTxChainDoneDelayNanoS()) / 1000;
        }

        private uint MODEM_GetTxChainDoneDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_BlePhyTxDoneChainDelayNanoS : MODEM_802154PhyTxDoneChainDelayNanoS;
        }

        private double MODEM_GetTxChainDelayUs()
        {
            return ((double)MODEM_GetTxChainDelayNanoS()) / 1000;
        }

        private uint MODEM_GetRxDoneDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_BlePhyRxDoneDelayNanoS : MODEM_802154PhyRxDoneDelayNanoS;
        }

        private double MODEM_GetRxChainDelayUs()
        {
            return ((double)MODEM_GetRxChainDelayNanoS()) / 1000;
        }

        private uint MODEM_GetRxChainDelayNanoS()
        {
            return MODEM_viterbiDemodulatorEnable.Value ? MODEM_BlePhyRxChainDelayNanoS : MODEM_802154PhyRxChainDelayNanoS;
        }

        private uint MODEM_GetSyncWordLengthInBits()
        {
            return MODEM_SyncWordLength;
        }

        private uint MODEM_GetPreambleLengthInBits()
        {
            uint preambleLength = (uint)((MODEM_baseBits.Value + 1)*MODEM_txBases.Value);
            return preambleLength;
        }

        //-------------------------------------------------------------
        // AGC private methods

        private void AGC_UpdateRssiState()
        {
            if(AGC_rssiStartCommandOngoing)
            {
                AGC_rssiState.Value = AGC_RssiState.Command;
            }
            else if(RAC_currentRadioState == RAC_RadioState.RxSearch)
            {
                AGC_rssiState.Value = AGC_RssiState.Period;
            }
            else if(RAC_currentRadioState == RAC_RadioState.RxFrame)
            {
                AGC_rssiState.Value = AGC_RssiState.FameDetection;
            }
            else
            {
                AGC_rssiState.Value = AGC_RssiState.Idle;
            }
        }

        private void AGC_RssiUpdateTimerHandleLimitReached()
        {
            rssiUpdateTimer.Enabled = false;
            AGC_UpdateRssi();
            if(AGC_rssiStartCommandOngoing)
            {
                AGC_rssiDoneInterrupt.Value = true;
                AGC_seqRssiDoneInterrupt.Value = true;

                if(AGC_rssiStartCommandFromProtimer)
                {
                    if(PROTIMER_listenBeforeTalkState != PROTIMER_ListenBeforeTalkState.Idle)
                    {
                        PROTIMER_ListenBeforeTalkCcaCompleted();
                        // Only keep the RSSI timer free-running while the CCA measurement is still
                        // in progress (LBT in CCADELAY). If the channel was busy, CcaCompleted moved
                        // the LBT back to BACKOFF and re-armed the randomized CSMA-CA backoff (TOUT0);
                        // the next CCA must wait for that backoff to expire (driven by
                        // PROTIMER_TimeoutCounter0HandleUnderflow -> AGC_RssiStartCommand). Restarting
                        // the RSSI timer here would re-trigger CCA every RSSI period and bypass the
                        // backoff entirely, collapsing all retries into a few microseconds.
                        if(PROTIMER_listenBeforeTalkState == PROTIMER_ListenBeforeTalkState.CcaDelay)
                        {
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

        private void AGC_StopRssiTimer()
        {
            rssiUpdateTimer.Enabled = false;
            AGC_rssiStartCommandOngoing = false;
            AGC_UpdateRssiState();

            if(AGC_rssiStartCommandFromProtimer)
            {
                AGC_rssiStartCommandFromProtimer = false;
                if(PROTIMER_listenBeforeTalkState != PROTIMER_ListenBeforeTalkState.Idle)
                {
                    PROTIMER_ListenBeforeTalkCcaCompleted(true);
                }
            }
        }

        private void AGC_RestartRssiTimer()
        {
            rssiUpdateTimer.Enabled = false;
            rssiUpdateTimer.Value = 0;
            rssiUpdateTimer.Limit = AGC_RssiPeriodUs;
            rssiUpdateTimer.Enabled = true;
        }

        private bool AGC_RssiStartCommand(bool fromProtimer = false)
        {
            if(RAC_currentRadioState != RAC_RadioState.RxSearch && RAC_currentRadioState != RAC_RadioState.RxFrame)
            {
                AGC_RssiIntegerPart = AGC_RssiInvalid;
                if(fromProtimer && PROTIMER_listenBeforeTalkState != PROTIMER_ListenBeforeTalkState.Idle)
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

        private void AGC_UpdateRssi()
        {
            this.Log(LogLevel.Noisy, "AGC_UpdateRssi: updating RSSI value at {0}", GetTime());

            // First RSSI read
            if(AGC_rssiLastRead == AGC_RssiInvalid)
            {
                AGC_rssiValidInterrupt.Value = true;
                AGC_seqRssiValidInterrupt.Value = true;
            }

            // Update the RSSI values
            AGC_rssiSecondLastRead = AGC_rssiLastRead;
            AGC_rssiLastRead = AGC_RssiIntegerPart;
            AGC_RssiIntegerPart = (sbyte)InterferenceQueue.GetCurrentRssi(this, Channel);

            if(AGC_RssiIntegerPartAdjusted < (int)AGC_ccaThreshold.Value)
            {
                AGC_ccaInterrupt.Value = true;
                AGC_seqCcaInterrupt.Value = true;
            }

            int compareValue = (int)((AGC_rssiStepPeriod.Value) ? AGC_rssiSecondLastRead : AGC_rssiLastRead);
            if(AGC_RssiIntegerPartAdjusted > (compareValue + (int)AGC_rssiPositiveStepThreshold.Value))
            {
                AGC_rssiPositiveStepInterrupt.Value = true;
                AGC_seqRssiPositiveStepInterrupt.Value = true;
            }
            else if(AGC_RssiIntegerPartAdjusted < (compareValue - (int)AGC_rssiNegativeStepThreshold.Value))
            {
                AGC_rssiNegativeStepInterrupt.Value = true;
                AGC_seqRssiNegativeStepInterrupt.Value = true;
            }
            UpdateInterrupts();
        }

        //-------------------------------------------------------------
        // CRC private methods

        private byte[] CRC_CalculateCRC()
        {
            this.Log(LogLevel.Debug, "CRC mocked with 0x0 bytes.");
            return Enumerable.Repeat<byte>(0x0, (int)CRC_CrcWidth).ToArray();
        }

        //-------------------------------------------------------------
        // FRC private methods

        // BUFRESTOREFRAMEERROR/BUFRESTORERXABORTED save and restore the active receive buffer
        // selected by ACTIVERXFCD (FCD2 or FCD3).
        private void FRC_RestoreActiveRxDescriptorBufferWriteOffset()
        {
            bufferController.RestoreWriteOffset(FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor].BufferIndex);
        }

        // FRC_RXCTRL.BUFCLEAR: the BUFC buffer selected by an RX descriptor is cleared before any frame data is
        // written to it. Both descriptors of the RX pair are cleared, since either can be selected while the
        // frame is disassembled, and this runs before the write offsets are saved so that a BUFRESTORE on a
        // frame error rewinds to the cleared buffer rather than to a stale offset.
        private void FRC_ClearRxDescriptorsBuffers()
        {
            if(!FRC_rxBufferClear.Value)
            {
                return;
            }

            for(var index = FRC_FirstReceiveFrameDescriptor; index < FRC_NumberOfFrameDescriptors; ++index)
            {
                bufferController.Clear(FRC_frameDescriptor[index].BufferIndex);
            }
        }

        private void FRC_SaveActiveRxDescriptorBufferWriteOffset()
        {
            bufferController.UpdateWriteStartOffset(FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor].BufferIndex);
        }

        // Descriptor sequencing within a frame, per the FCD modes in the design book: mode 0 always uses the
        // low descriptor of the pair, mode 3 always the high one, mode 1 alternates between them, and mode 2
        // uses the low descriptor for the first subframe and the high one for all the remaining subframes.
        // 'firstDescriptor' selects the pair (FCD0/FCD1 for TX, FCD2/FCD3 for RX) and 'completedSubframes' is
        // the number of subframes already handled in the frame, so passing 0 yields the descriptor a frame
        // starts on.
        private int FRC_SequenceFrameDescriptor(FRC_FrameDescriptorMode mode, int firstDescriptor, int completedSubframes)
        {
            switch(mode)
            {
            case FRC_FrameDescriptorMode.FrameDescriptorMode0:
                return firstDescriptor;
            case FRC_FrameDescriptorMode.FrameDescriptorMode1:
                return firstDescriptor + (completedSubframes % 2);
            case FRC_FrameDescriptorMode.FrameDescriptorMode2:
                return firstDescriptor + ((completedSubframes > 0) ? 1 : 0);
            case FRC_FrameDescriptorMode.FrameDescriptorMode3:
                return firstDescriptor + 1;
            default:
                this.Log(LogLevel.Error, "Invalid FCD mode ({0}).", mode);
                return firstDescriptor;
            }
        }

        // Resets WCNT at the start of a frame. A compare channel programmed to 0 matches immediately, which is
        // the usual setting for LENGTHFIELDLOC when the length field is the first word of the frame.
        private void FRC_ResetWordCounter()
        {
            FRC_wordCounter.Value = 0;
            FRC_UpdateWordCounterCompareInterrupts(0, 0);
        }

        // Advances WCNT by a number of words and raises the compare interrupts of every value the counter
        // reaches on the way. Frame assembly and disassembly move WCNT a subframe or a fragment at a time rather
        // than word by word, so a single advance can cross several compare values.
        private void FRC_AdvanceWordCounter(uint words)
        {
            var previous = (uint)FRC_wordCounter.Value;
            FRC_wordCounter.Value = previous + words;
            FRC_UpdateWordCounterCompareInterrupts(previous + 1, previous + words);
        }

        // The WCNT compare channels hold FRAMELENGTH, LENGTHFIELDLOC and ADDRFIELDLOC, and their flags let
        // software (or the sequencer) act on the corresponding frame boundary: the end of the frame, the arrival
        // of the length field, and the arrival of the address field -- the last one doubling as a "N bytes
        // received" trigger for software frame filtering when hardware address filtering is not used.
        private void FRC_UpdateWordCounterCompareInterrupts(uint firstWordReached, uint lastWordReached)
        {
            var compareValues = new uint[]
            {
                (uint)FRC_frameLength.Value,
                (uint)FRC_lengthFieldLocation.Value,
                (uint)FRC_addressFieldLocation.Value,
            };

            var raised = false;
            for(var channel = 0; channel < FRC_NumberOfWordCounterCompareChannels; ++channel)
            {
                if(compareValues[channel] < firstWordReached || compareValues[channel] > lastWordReached)
                {
                    continue;
                }

                FRC_wordCounterCompareInterrupt[channel].Value = true;
                FRC_seqWordCounterCompareInterrupt[channel].Value = true;
                raised = true;
            }

            if(raised)
            {
                UpdateInterrupts();
            }
        }

        // FRC_FCDn.EXCLUDESUBFRAMEWCNT keeps a subframe's words out of the word counter, so that FRAMELENGTH and
        // LENGTHFIELDLOC do not count them -- the register description gives the sync word sent as the first
        // subframe as the example. Such a subframe is bounded by WORDS alone, which makes the unlimited WORDS
        // sentinel (0xFF, "the rest of the frame") meaningless: it would never terminate, so it is reported and
        // the subframe is counted normally instead.
        private bool FRC_SubframeExcludedFromWordCounter(FRC_FrameDescriptor descriptor, int index)
        {
            if(!descriptor.ExcludeSubframeFromWcnt.Value)
            {
                return false;
            }

            if(descriptor.UnlimitedWords)
            {
                this.Log(LogLevel.Error, "FCD{0} sets EXCLUDESUBFRAMEWCNT together with an unlimited WORDS, counting the subframe in WCNT instead.", index);
                return false;
            }

            return true;
        }

        // True when frame descriptor 'index' is the dedicated leading sync FCD of the BLE LE Coded layout rather
        // than a payload descriptor: the sync word travels in the data stream (SYNCDATA=1) yet is emitted through
        // the modulator sync path because the FRC generates the access address for the demodulator in RXSEARCH
        // (Linecode), TXFCDMODE=2 (FCD0 once, then FCD1...), and the subframe is CRC-less with a length equal to
        // the sync word.
        private bool FRC_TxLeadingSyncDescriptor(int index)
        {
            return index == 0
                   && MODEM_syncData.Value
                   && MODEM_GetRadioSymbolCoding() == RadioSymbolCoding.Linecode
                   && FRC_txFrameDescriptorMode.Value == FRC_FrameDescriptorMode.FrameDescriptorMode2
                   && !FRC_frameDescriptor[0].IncludeCrc.Value
                   && FRC_frameDescriptor[0].Words == MODEM_SyncWordBytes;
        }

        // True when the leading descriptor of a frame cannot contribute any transmit words of its own, so
        // hardware moves straight on to the next descriptor in the FCD sequence. Two cases occur in practice:
        // the descriptor holds the BLE LE Coded access address, which the modulator sync path emits instead, or
        // the PHY points it at a BUFC buffer the stack never fills because both the payload and the dynamic
        // length field live in the next descriptor's buffer. This only applies while resolving the first
        // descriptor of a frame: an empty buffer encountered once assembly is under way is a real underflow.
        private bool FRC_TxLeadingDescriptorContributesNoData(int index)
        {
            return FRC_TxLeadingSyncDescriptor(index)
                   || bufferController.BytesAvailable(FRC_frameDescriptor[index].BufferIndex) == 0;
        }

        // Advances the active TX descriptor past any leading descriptors that contribute no data, so that both
        // the dynamic frame length decoding and the subframe loop start at the descriptor which actually holds
        // the frame. Bounded by the number of descriptors so a mode that cycles between them cannot spin.
        private void FRC_ResolveLeadingTransmitFrameDescriptor()
        {
            for(var skipped = 0; skipped < FRC_NumberOfFrameDescriptors; ++skipped)
            {
                var current = FRC_ActiveTransmitFrameDescriptor;
                if(!FRC_TxLeadingDescriptorContributesNoData(current))
                {
                    return;
                }

                var next = FRC_SequenceFrameDescriptor(FRC_txFrameDescriptorMode.Value, FRC_FirstTransmitFrameDescriptor, skipped + 1);
                if(next == current)
                {
                    return;
                }

                this.Log(LogLevel.Debug, "FRC assemble: FCD{0} contributes no data, starting the frame at FCD{1}", current, next);
                FRC_ActiveTransmitFrameDescriptor = next;
            }
        }

        // RX counterpart of FRC_TxLeadingSyncDescriptor: the sync word travels in the data stream (SYNCDATA=1)
        // but the Demodulator has already removed it before the FRC sees the frame, so a leading descriptor that
        // only describes that sync word must not consume payload bytes into its buffer. Hardware marks such a
        // subframe by keeping it out of the word counter (FRC_FCDn.EXCLUDESUBFRAMEWCNT names this exact case) or,
        // for BLE LE Coded, by having the FRC generate the access address itself.
        private bool FRC_RxLeadingSyncDescriptor(int index)
        {
            if(index != FRC_FirstReceiveFrameDescriptor
               || FRC_rxFrameDescriptorMode.Value != FRC_FrameDescriptorMode.FrameDescriptorMode2
               || !MODEM_syncData.Value)
            {
                return false;
            }

            var descriptor = FRC_frameDescriptor[index];
            return !descriptor.IncludeCrc.Value
                   && descriptor.Words == MODEM_SyncWordBytes
                   && (descriptor.ExcludeSubframeFromWcnt.Value
                       || MODEM_GetRadioSymbolCoding() == RadioSymbolCoding.Linecode);
        }

        // Advances the active RX descriptor past a leading descriptor that receives nothing, so the first
        // payload bytes are written to the buffer that is meant to hold them. Bounded like the transmit
        // counterpart so a mode that cycles between descriptors cannot spin.
        private void FRC_ResolveLeadingReceiveFrameDescriptor()
        {
            for(var skipped = 0; skipped < FRC_NumberOfFrameDescriptors; ++skipped)
            {
                var current = FRC_ActiveReceiveFrameDescriptor;
                if(!FRC_RxLeadingSyncDescriptor(current))
                {
                    return;
                }

                var next = FRC_SequenceFrameDescriptor(FRC_rxFrameDescriptorMode.Value, FRC_FirstReceiveFrameDescriptor, skipped + 1);
                if(next == current)
                {
                    return;
                }

                this.Log(LogLevel.Debug, "FRC disassemble: FCD{0} receives no data, starting the frame at FCD{1}", current, next);
                FRC_ActiveReceiveFrameDescriptor = next;
            }
        }

        private byte[] FRC_AssembleFrame()
        {
            var frame = Enumerable.Empty<byte>();

            // Per the FRC design the FRC operates on payload data only: insertion of the sync word on the
            // transmit path is owned by the Modulator (and removal on RX by the Demodulator). This model
            // represents that modulator sync insertion by emitting the sync word as a prefix here, and
            // the RX side correspondingly checks/strips MODEM_SyncWordBytes.
            //
            // MODEM_CONTROL1->SYNCDATA selects how the sync reaches the air: SYNCDATA=0 means the modulator
            // inserts it (so it is not in the FRC buffer), while SYNCDATA=1 means it is carried within the data
            // stream. BLE LE Coded ("LongRange" / "Linecode") uses SYNCDATA=1 but is the documented special case:
            // in RXSEARCH the FRC itself generates the access address for the demodulator, so the coded TX PHY 
            // is programmed with a dedicated leading sync FCD ahead of the payload FCD. We still surface the
            // access address through the modulator sync path below, so it is emitted exactly once.
            var prependSync = !MODEM_syncData.Value || MODEM_GetRadioSymbolCoding() == RadioSymbolCoding.Linecode;
            if(prependSync)
            {
                frame = frame.Concat(BitHelper.GetBytesFromValue(MODEM_TxSyncWord, (int)MODEM_SyncWordBytes, reverse: true));
            }

            // The access address of the BLE LE Coded layout lives in a dedicated leading sync FCD, not in the
            // payload, and a PHY may equally point the leading FCD at a buffer the stack never fills. Either way
            // assembly must start at the descriptor that actually holds the frame instead of re-emitting the
            // sync FCD as data or underflowing on an empty buffer. This also makes the dynamic length field and
            // payload read from the payload buffer, matching the hardware WCNT / FRAMELENGTH which span the
            // payload only (DFLOFFSET accounts for the PDU header, not the access address).
            FRC_ResolveLeadingTransmitFrameDescriptor();

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
                frameLength = bufferController.Peek(descriptor.BufferIndex, (uint)FRC_lengthFieldLocation.Value);
                break;
            case FRC_DynamicFrameLengthMode.DualByteLSBFirst:
            case FRC_DynamicFrameLengthMode.DualByteMSBFirst:
                frameLength = ((bufferController.Peek(descriptor.BufferIndex, (uint)FRC_lengthFieldLocation.Value + 1) << 8)
                               | (bufferController.Peek(descriptor.BufferIndex, (uint)FRC_lengthFieldLocation.Value)));

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

            FRC_ResetWordCounter();
            for(var subframe = 0; FRC_wordCounter.Value < FRC_FrameLength; ++subframe)
            {
                // Per FRC_FCDn, INCLUDECRC is the bit that appends the calculated CRC after this subframe's
                // data, while CALCCRC only selects whether the subframe feeds the CRC accumulator. A subframe
                // is (WORDS+1) words excluding any CRC it includes, so the CRC is reserved out of the remaining
                // FRAMELENGTH budget rather than out of WORDS, and only when DFLINCLUDECRC says FRAMELENGTH
                // counts the CRC bytes at all.
                var crcLength = descriptor.IncludeCrc.Value ? CRC_CrcWidth : 0;
                var crcCountsInFrameLength = (crcLength > 0) && dynamicFrameLength && FRC_dynamicFrameCrcIncluded.Value;
                var remaining = (uint)(FRC_FrameLength - FRC_wordCounter.Value);
                if(crcCountsInFrameLength && remaining < crcLength)
                {
                    this.Log(LogLevel.Error, "Failed to assemble a frame (CRC would exceed DFL), partial frame: {0}", BitConverter.ToString(frame.ToArray()));
                    return new byte[0];
                }

                var excludeFromWordCounter = FRC_SubframeExcludedFromWordCounter(descriptor, FRC_ActiveTransmitFrameDescriptor);
                // Assemble subframe. An excluded subframe is sized by WORDS alone, since FRAMELENGTH does not
                // account for its words.
                var length = excludeFromWordCounter
                             ? descriptor.Words
                             : Math.Min(crcCountsInFrameLength ? (remaining - crcLength) : remaining, descriptor.Words);
                if(!bufferController.TryReadBytes(descriptor.BufferIndex, length, out var payload))
                {
                    this.Log(LogLevel.Error, "Failed to assemble a frame, partial frame: {0}", BitConverter.ToString(frame.Concat(payload).ToArray()));
                    FRC_txUnderflowInterrupt.Value = true;
                    return new byte[0];
                }
                frame = frame.Concat(payload);
                if(!excludeFromWordCounter)
                {
                    FRC_AdvanceWordCounter(length);
                }

                if(descriptor.IncludeCrc.Value)
                {
                    frame = frame.Concat(CRC_CalculateCRC());
                    if(crcCountsInFrameLength)
                    {
                        FRC_AdvanceWordCounter(crcLength);
                    }
                }

                // Select next frame descriptor
                var nextDescriptor = FRC_SequenceFrameDescriptor(FRC_txFrameDescriptorMode.Value, FRC_FirstTransmitFrameDescriptor, subframe + 1);
                if(excludeFromWordCounter && nextDescriptor == FRC_ActiveTransmitFrameDescriptor)
                {
                    this.Log(LogLevel.Error, "FCD{0} is excluded from WCNT but the FCD mode never advances past it, aborting frame assembly.",
                             FRC_ActiveTransmitFrameDescriptor);
                    return new byte[0];
                }
                FRC_ActiveTransmitFrameDescriptor = nextDescriptor;
                descriptor = FRC_frameDescriptor[FRC_ActiveTransmitFrameDescriptor];
            }

            // Prepare for next frame
            FRC_ActiveTransmitFrameDescriptor = FRC_SequenceFrameDescriptor(FRC_txFrameDescriptorMode.Value, FRC_FirstTransmitFrameDescriptor, 0);

            this.Log(LogLevel.Noisy, "Frame assembled: {0}", BitConverter.ToString(frame.ToArray()));
            return frame.ToArray();
        }

        private void FRC_UpdateRawMode()
        {
            if(!FRC_enableRawDataRandomNumberGenerator.Value || FRC_rxRawBlocked.Value || RAC_currentRadioState != RAC_RadioState.RxSearch)
            {
                return;
            }

            switch(FRC_rxRawDataSelect.Value)
            {
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

        private void FRC_SnifferReceiveFrame(byte[] frame)
        {
            PtiFrameStart?.Invoke(SiLabs_PacketTraceFrameType.Receive);
            // NOTE the sync word makes up the first 4 bytes of the frame
            int syncWordBytes = Math.Min((int) MODEM_SyncWordBytes, SNIFF_SYNCWORD_SERIAL_LEN);
            var syncWord = frame.Take(syncWordBytes).ToArray();
            // NOTE the rest of the frame is the actual data
            var frameData = frame.Skip(syncWordBytes).ToArray();
            if(FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.RxStart });
            }
            if(FRC_ptiEmitSyncWord.Value)
            {
                var syncWordPadded = new byte[SNIFF_SYNCWORD_SERIAL_LEN];
                Array.Copy(syncWord, syncWordPadded, syncWord.Length);
                PtiDataOut?.Invoke(syncWordPadded);
            }
            if(FRC_ptiEmitRx.Value)
            {
                PtiDataOut?.Invoke(frameData);
            }
            if(FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.RxEndSuccess });
            }
            if(FRC_ptiEmitRssi.Value)
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
            if(FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.TxStart });
            }
            if(FRC_ptiEmitSyncWord.Value)
            {
                var syncWordPadded = new byte[SNIFF_SYNCWORD_SERIAL_LEN];
                Array.Copy(syncWord, syncWordPadded, syncWord.Length);
                PtiDataOut?.Invoke(syncWordPadded);
            }
            if(FRC_ptiEmitTx.Value)
            {
                PtiDataOut?.Invoke(frameData);
            }
            if(FRC_ptiEmitState.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.TxEndSuccess });
            }
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
            // Signed 2's complement
            int offset = (FRC_dynamicFrameLengthOffset.Value & 0x8) != 0
                ? (int)(FRC_dynamicFrameLengthOffset.Value | unchecked((uint)~0xF))
                : (int)FRC_dynamicFrameLengthOffset.Value;
            frameLength += (uint)offset;
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

        // Disassembles a slice of the received frame into the host buffer (BUFC) and the FRC packet-capture
        // buffer. The slice is [startOffset, startOffset + numBytes) within currentFrame (offsets include
        // the leading sync word). The method derives the frame boundaries from currentFrame itself:
        //   - beginning of frame (startOffset at the first MAC byte, i.e. just past the sync word): parse
        //     the dynamic frame length and reset the per-frame disassemble state (WCNT, subframe index);
        //   - end of frame (the slice reaches currentFrame.Length): append the RX trailer and prepare the
        //     descriptors for the next frame.
        // Called once with the whole payload range it reproduces the original one-shot behaviour; called
        // repeatedly with successive ranges it delivers the frame incrementally (BUFC, packet-capture
        // buffer and WCNT advance in lockstep), mirroring how hardware fills the buffers as words arrive.
        private void FRC_DisassembleCurrentFrame(bool forceCrcError, uint startOffset, uint numBytes)
        {
            var dynamicFrameLength = (FRC_dynamicFrameLengthMode.Value != FRC_DynamicFrameLengthMode.Disable);
            var isFrameStart = (startOffset <= MODEM_SyncWordBytes);
            var endOffset = Math.Min(startOffset + numBytes, (uint)currentFrame.Length);
            var isFrameEnd = (endOffset >= (uint)currentFrame.Length);

            if(isFrameStart)
            {
                var frameLength = 0u;
                switch(FRC_dynamicFrameLengthMode.Value)
                {
                case FRC_DynamicFrameLengthMode.Disable:
                    break;
                case FRC_DynamicFrameLengthMode.SingleByte:
                case FRC_DynamicFrameLengthMode.SingleByteMSB:
                    frameLength = (uint)currentFrame[startOffset + FRC_lengthFieldLocation.Value];
                    break;
                case FRC_DynamicFrameLengthMode.DualByteLSBFirst:
                case FRC_DynamicFrameLengthMode.DualByteMSBFirst:
                    frameLength = (uint)currentFrame[startOffset + FRC_lengthFieldLocation.Value + 1] << 8
                                  | (uint)currentFrame[startOffset + FRC_lengthFieldLocation.Value];
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

                FRC_ResetWordCounter();
                currentDisassembleSubFrame = 0;
                currentDisassembleSubFrameStartWord = 0;
                currentDisassembleSubFrameBytes = 0;
                currentFrameOffset = startOffset;

                // The Demodulator has already stripped the sync word from the bytes handed to us, so a leading
                // descriptor that only describes that sync word must not swallow the first payload bytes.
                FRC_ResolveLeadingReceiveFrameDescriptor();
            }

            // Deliver the payload of this slice: from currentFrameOffset up to endOffset, or until the whole
            // frame has been disassembled, whichever comes first. A slice may end in the middle of a subframe;
            // in that case we keep our place (currentDisassembleSubFrameStartWord / currentDisassembleSubFrame
            // persist) and resume on the next call.
            while(FRC_wordCounter.Value < FRC_FrameLength && currentFrameOffset < endOffset)
            {
                var descriptor = FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor];

                // Disassemble subframe.
                // The loop is symmetric with FRC_AssembleFrame() on the TX side: INCLUDECRC is the bit that
                // places the CRC after this subframe's data and CALCCRC only feeds the CRC accumulator. A
                // subframe is (WORDS+1) words excluding that CRC, so the CRC bytes are not part of the payload
                // written into the host buffer -- they are read separately below -- and they are reserved out of
                // the remaining FRAMELENGTH budget only when DFLINCLUDECRC says FRAMELENGTH counts them.
                var crcLength = descriptor.IncludeCrc.Value ? CRC_CrcWidth : 0;
                var crcCountsInFrameLength = (crcLength > 0) && dynamicFrameLength && FRC_dynamicFrameCrcIncluded.Value;
                var remaining = FRC_FrameLength - currentDisassembleSubFrameStartWord;
                if(crcCountsInFrameLength && remaining < crcLength)
                {
                    this.Log(LogLevel.Error, "frame too small to contain CRC");
                    return;
                }

                var excludeFromWordCounter = FRC_SubframeExcludedFromWordCounter(descriptor, FRC_ActiveReceiveFrameDescriptor);
                // An excluded subframe is sized by WORDS alone, since FRAMELENGTH does not account for its words.
                var payloadTotal = excludeFromWordCounter
                                   ? descriptor.Words
                                   : Math.Min(crcCountsInFrameLength ? (remaining - crcLength) : remaining, descriptor.Words);
                var bytesToWrite = (uint)Math.Min((long)(payloadTotal - currentDisassembleSubFrameBytes), (long)(endOffset - currentFrameOffset));
                if(currentFrameOffset + bytesToWrite > (uint)currentFrame.Length)
                {
                    this.Log(LogLevel.Error, "frame too small, payload");
                    return;
                }

                if(bytesToWrite > 0)
                {
                    var payload = currentFrame.Skip((int)currentFrameOffset).Take((int)bytesToWrite);
                    var skipCount = (FRC_wordCounter.Value < FRC_packetBufferStartAddress.Value)
                                    ? Math.Min(bytesToWrite, FRC_packetBufferStartAddress.Value - FRC_wordCounter.Value) : 0;
                    var pktCaptureBuff = currentFrame.Skip((int)(currentFrameOffset + skipCount)).Take((int)(bytesToWrite - skipCount));
                    // Position of these captured bytes within the capture window (WCNT - PKTBUFSTARTLOC),
                    // computed before WCNT advances so chunked delivery lands at the right offset.
                    var captureOffset = (FRC_wordCounter.Value + skipCount) - FRC_packetBufferStartAddress.Value;
                    currentFrameOffset += bytesToWrite;
                    if(!excludeFromWordCounter)
                    {
                        FRC_AdvanceWordCounter(bytesToWrite);
                    }
                    currentDisassembleSubFrameBytes += bytesToWrite;

                    FRC_WritePacketCaptureBuffer(pktCaptureBuff.ToArray(), (uint)captureOffset);

                    if(!bufferController.TryWriteBytes(descriptor.BufferIndex, payload.ToArray(), out var written))
                    {
                        this.Log(LogLevel.Error, "Written only {0} bytes from {1}", written, bytesToWrite);
                        if(bufferController.Overflow(descriptor.BufferIndex))
                        {
                            FRC_rxOverflowInterrupt.Value = true;
                            FRC_seqRxOverflowInterrupt.Value = true;
                            UpdateInterrupts();
                            RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxOverflow);
                        }
                    }
                }

                if(currentDisassembleSubFrameBytes < payloadTotal)
                {
                    // The slice ends inside this subframe: resume from here on the next call.
                    break;
                }

                // The subframe payload is fully delivered: read (and optionally store) its CRC.
                if(descriptor.IncludeCrc.Value)
                {
                    var crc = currentFrame.Skip((int)currentFrameOffset).Take((int)CRC_CrcWidth).ToArray();
                    // TODO: Check CRC
                    // FRC_frameErrorInterrupt.Value |= crc check failed
                    currentFrameOffset += (uint)crc.Length;
                    if(crc.Length != CRC_CrcWidth)
                    {
                        this.Log(LogLevel.Error, "frame too small, crc");
                    }
                    if(crcCountsInFrameLength)
                    {
                        FRC_AdvanceWordCounter((uint)crc.Length);
                    }
                    if(FRC_rxStoreCrc.Value)
                    {
                        if(!bufferController.TryWriteBytes(descriptor.BufferIndex, crc, out var written))
                        {
                            this.Log(LogLevel.Error, "Written only {0} bytes from {1}", written, crc.Length);
                            if(bufferController.Overflow(descriptor.BufferIndex))
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
                var nextDescriptor = FRC_SequenceFrameDescriptor(FRC_rxFrameDescriptorMode.Value, FRC_FirstReceiveFrameDescriptor, currentDisassembleSubFrame + 1);
                if(excludeFromWordCounter && nextDescriptor == FRC_ActiveReceiveFrameDescriptor)
                {
                    this.Log(LogLevel.Error, "FCD{0} is excluded from WCNT but the FCD mode never advances past it, aborting frame disassembly.",
                             FRC_ActiveReceiveFrameDescriptor);
                    return;
                }
                FRC_ActiveReceiveFrameDescriptor = nextDescriptor;
                currentDisassembleSubFrame++;
                currentDisassembleSubFrameStartWord = (uint)FRC_wordCounter.Value;
                currentDisassembleSubFrameBytes = 0;
            }

            if(!isFrameEnd)
            {
                // More of the frame is still to come on a later call: the trailer and descriptor
                // preparation happen only once, at the true end of frame.
                return;
            }

            // RX TRAIL DATA
            {
                // Appended ascending order of field's bit index
                var descriptor = FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor];
                if(FRC_rxAppendRssi.Value)
                {
                    // The RSSI appended to the frame is the frame RSSI latched at sync detection, i.e. the same
                    // value AGC_FRAMERSSI.FRAMERSSIINT reports, as a signed two's complement dBm integer.
                    bufferController.WriteData(descriptor.BufferIndex, (byte)AGC_FrameRssiIntegerPartAdjusted);
                }
                if(FRC_rxAppendCrcOk.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, (forceCrcError) ? 0x00U : 0x80U);
                }
                if(FRC_rxAppendProtimerCc0base.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, ((uint)PROTIMER_captureCompareChannel[0].BaseValue.Value & 0xFF));
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].BaseValue.Value >> 8) & 0xFF));
                }
                if(FRC_rxAppendProtimerCc0LowWrap.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, ((uint)PROTIMER_captureCompareChannel[0].WrapValue.Value & 0xFF));
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].WrapValue.Value >> 8) & 0xFF));
                }
                if(FRC_rxAppendProtimerCc0HighWrap.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].WrapValue.Value >> 16) & 0xFF));
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].WrapValue.Value >> 24) & 0xFF));
                }
                if(FRC_rxAppendRtcStamp.Value)
                {
                    // TODO: RENODE-489
                }
            }

            // Prepare for next frame
            FRC_ActiveReceiveFrameDescriptor = FRC_SequenceFrameDescriptor(FRC_rxFrameDescriptorMode.Value, FRC_FirstReceiveFrameDescriptor, 0);

            this.Log(LogLevel.Noisy, "Frame disassembled, frame descriptor now is {0}", FRC_ActiveReceiveFrameDescriptor);
        }

        // Writes data into the FRC packet-capture buffer starting at the given offset (the byte position
        // within the capture window, i.e. WCNT - PKTBUFSTARTLOC). PKTBUFCOUNT tracks the highest captured
        // position so that incremental (chunked) delivery fills the buffer at the correct location instead
        // of always at the start.
        private void FRC_WritePacketCaptureBuffer(byte[] data, uint offset)
        {
            var wroteAny = false;
            for(var i = 0; i < data.Length; i++)
            {
                var position = offset + (uint)i;
                if(position >= FRC_PacketBufferCaptureSize)
                {
                    break;
                }

                FRC_packetBufferCapture[position] = data[i];
                if(position + 1 > FRC_packetBufferCount.Value)
                {
                    FRC_packetBufferCount.Value = position + 1;
                }
                wroteAny = true;

                if(FRC_packetBufferCount.Value == 1)
                {
                    FRC_packetBufferStartInterrupt.Value = true;
                    FRC_seqPacketBufferStartInterrupt.Value = true;
                }
            }

            if(wroteAny)
            {
                // Re-evaluate interrupts as the capture buffer fills so PKTBUFSTART (first byte) and
                // PKTBUFTHRESHOLD (PKTBUFCOUNT reached the configured threshold) are raised promptly,
                // including when the frame is delivered incrementally. UpdateInterrupts() runs
                // FRC_CheckPacketCaptureBufferThreshold(), which sets PKTBUFTHRESHOLDIF/SEQIF.
                UpdateInterrupts();
            }
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

            if(!foundSyncWord0 && !foundSyncWord1)
            {
                return false;
            }

            MODEM_rxPreambleDetectedInterrupt.Value = true;
            MODEM_seqRxPreambleDetectedInterrupt.Value = true;

            if(foundSyncWord0)
            {
                PROTIMER_TriggerEvent(PROTIMER_Event.Syncword0Detected);
            }

            if(foundSyncWord1)
            {
                PROTIMER_TriggerEvent(PROTIMER_Event.Syncword1Detected);
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.Syncword0Or1Detected);

            PROTIMER_CaptureOnSyncWordsDetected(foundSyncWord0, foundSyncWord1, false, false);

            MODEM_rxFrameWithSyncWord0DetectedInterrupt.Value |= foundSyncWord0;
            MODEM_seqRxFrameWithSyncWord0DetectedInterrupt.Value = MODEM_rxFrameWithSyncWord0DetectedInterrupt.Value;
            MODEM_rxFrameWithSyncWord1DetectedInterrupt.Value |= foundSyncWord1;
            MODEM_seqRxFrameWithSyncWord1DetectedInterrupt.Value = MODEM_rxFrameWithSyncWord1DetectedInterrupt.Value;
            MODEM_frameDetectedId.Value = !foundSyncWord0 && foundSyncWord1;
            UpdateInterrupts();

            return true;
        }

        private void FRC_RxAbortCommand()
        {
            // CMD_RXABORT takes effect when FRC is active. When RAC in RXWARM and RXSEARCH state, 
            // since FRC is still in IDLE state this command doesn't do anything. 
            if(RAC_currentRadioState != RAC_RadioState.RxFrame)
            {
                return;
            }

            // When set, the active receive BUFC buffer is restored for received frames that trigger the RXABORTED interrupt flag. This
            // means that the WRITEOFFSET is restored to the value prior to receiving this frame. READOFFSET is not modified.
            if(FRC_rxBufferRestoreOnRxAborted.Value)
            {
                FRC_RestoreActiveRxDescriptorBufferWriteOffset();
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

        private void FRC_CheckPacketCaptureBufferThreshold()
        {
            if(FRC_packetBufferThresholdEnable.Value
                && FRC_packetBufferCount.Value >= FRC_packetBufferThreshold.Value)
            {
                FRC_packetBufferThresholdInterrupt.Value = true;
                FRC_seqPacketBufferThresholdInterrupt.Value = true;
            }
        }

        private void RAC_ClearOngoingTx(bool transmissionCompleted = false)
        {
            txTimer.Enabled = false;
            if(RAC_internalTxState != RAC_InternalTxState.Idle)
            {
                InterferenceQueue.Remove(this, transmissionCompleted);
            }
            RAC_internalTxState = RAC_InternalTxState.Idle;
        }

        //-------------------------------------------------------------
        // RAC private methods

        private void RAC_RxTimerLimitReached()
        {
            rxTimer.Enabled = false;

            // We went through the preamble delay which was scheduled in ReceiveFrame(). 
            // We can now check radio state and sync word.
            if(RAC_internalRxState == RAC_InternalRxState.PreambleAndSyncWord)
            {
                if(RAC_currentRadioState != RAC_RadioState.RxSearch)
                {
                    RAC_ClearOngoingRx();
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping (not in RXSEARCH): at {0} (channel {1}) ({2}): {3}", GetTime(), LogicalChannel, SYNTH_GetRfFrequencyString(), BitConverter.ToString(currentFrame));
                    return;
                }

                if(RAC_ongoingRxCollided)
                {
                    RAC_ClearOngoingRx();
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping (PRE/SYNC COLLISION): at {0} (channel {1}) ({2}): {3}", GetTime(), LogicalChannel, SYNTH_GetRfFrequencyString(), BitConverter.ToString(currentFrame));
                    return;
                }

                if(!FRC_CheckSyncWords(currentFrame))
                {
                    RAC_ClearOngoingRx();
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping (SYNC MISMATCH): at {0} (channel {1}) ({2}): {3}", GetTime(), LogicalChannel, SYNTH_GetRfFrequencyString(), BitConverter.ToString(currentFrame));
                    return;
                }

                // After having transitioned to RX_FRAME as result of the "beginning" of a packet being
                // received, we stay in RX_FRAME for the duration of the transmission. We deliver the frame to
                // BUFC + the FRC capture buffer in fixed-size fragments (FRC_RxFragmentLengthInBytes) spaced by
                // their airtime, approximating the hardware delivering the frame byte-by-byte.
                // The per-byte airtime is measured from the first payload byte: we don't include the preamble
                // and sync word time since we already delayed for it in ReceiveFrame(). We also account for
                // any extra quantum slack in currentFrameElapsedAirtimeUs).
                RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.FrameDetected);
                RAC_internalRxState = RAC_InternalRxState.Frame;
                FRC_ClearRxDescriptorsBuffers();
                FRC_SaveActiveRxDescriptorBufferWriteOffset();
                currentFrameOffset = MODEM_SyncWordBytes;
                currentFrameByteOverTheAirTimeUs = MODEM_GetByteOverTheAirTimeUs();

                // Arm the timer for the first delivery boundary (the first fragment, or the end of the frame if
                // it fits in a single fragment). Only if the quantum slack already covers the whole frame's
                // airtime do we skip the else branches and fall through to the end-of-frame code below to
                // finish RX immediately (delivering the entire frame at once).
                if(RAC_TryArmRxDeliveryTimer())
                {
                    return;
                }
            }
            else if(RAC_internalRxState == RAC_InternalRxState.Frame)
            {
                // A delivery boundary elapsed. Unless this is the final fragment, deliver one fragment from the
                // current read cursor and arm the timer for the next boundary.
                // currentFrameOffset advances by the bytes consumed (payload plus any CRC bytes read at a
                // subframe boundary); reaching the final fragment falls through to finish below.
                if(((uint)currentFrame.Length - currentFrameOffset) > FRC_RxFragmentLengthInBytes)
                {
                    FRC_DisassembleCurrentFrame(RAC_ongoingRxCollided, currentFrameOffset, FRC_RxFragmentLengthInBytes);
                }
                if(RAC_TryArmRxDeliveryTimer())
                {
                    return;
                }
            }
            else
            {
                this.Log(LogLevel.Error, "RAC_RxTimerLimitReached: unexpected RX state");
                return;
            }

            // Packet is fully received now and we have already delayed for the RX done chain delay.
            RAC_internalRxState = RAC_InternalRxState.Idle;

            for(var i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].Enable.Value && PROTIMER_captureCompareChannel[i].Mode.Value == PROTIMER_CaptureCompareMode.Capture)
                {
                    switch(PROTIMER_captureCompareChannel[i].CaptureInputSource.Value)
                    {
                    case PROTIMER_CaptureInputSource.RxAtEndOfFrameFromDemodulator:
                    case PROTIMER_CaptureInputSource.RxDone:
                    case PROTIMER_CaptureInputSource.TxOrRxDone:
                        PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                        PROTIMER_TriggerEvent((PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel0Event + i));
                        break;
                    }
                }
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.RxDone);
            PROTIMER_TriggerEvent(PROTIMER_Event.TxOrRxDone);

            if(RAC_ongoingRxCollided)
            {
                // The received frame collided with another one (that we already dropped). Here we are simulating a CRC error.
                FRC_frameErrorInterrupt.Value = true;
                FRC_seqFrameErrorInterrupt.Value = true;

                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping at {0}: RX collision", GetTime());

                if(FRC_rxBufferRestoreOnFrameError.Value & !FRC_rxAcceptCrcErrors.Value)
                {
                    FRC_RestoreActiveRxDescriptorBufferWriteOffset();
                }
            }

            // When a CRC error is detected, the FRAMEERROR interrupt flag is set. Normally, frames with CRC errors are discarded. 
            // However, by setting ACCEPTCRCERRORS in FRC_RXCTRL, frames with CRC errors are still accepted and a RXDONE interrupt 
            // is generated at the end of the frame reception, in addition to the FRAMERROR interrupt. 
            if(!RAC_ongoingRxCollided || FRC_rxAcceptCrcErrors.Value)
            {
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Received at {0} on channel {1} ({2}) (RSSI={3}, collided={4}): {5}",
                         GetTime(), LogicalChannel, SYNTH_GetRfFrequencyString(), AGC_FrameRssiIntegerPart, RAC_ongoingRxCollided, BitConverter.ToString(currentFrame));

                // No collision detected or ACCEPTCRCERRORS is set: disassemble the remaining bytes of the
                // frame and raise the RXDONE interrupt. Earlier slices were already delivered while chunking;
                // currentFrameOffset points at the first byte not yet delivered (it equals MODEM_SyncWordBytes
                // when chunking did not run, so the whole frame is delivered here). Reaching the end of
                // currentFrame also appends the RX trailer, now that the RxDone PROTIMER captures above have run.
                FRC_DisassembleCurrentFrame(RAC_ongoingRxCollided, currentFrameOffset, (uint)currentFrame.Length - currentFrameOffset);

                FRC_rxDoneInterrupt.Value = true;
                FRC_seqRxDoneInterrupt.Value = true;
            }

            UpdateInterrupts();
            RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.RxDone);
        }

        // Arms rxTimer for the next staggered RX delivery boundary, relative to the airtime already elapsed
        // (currentFrameElapsedAirtimeUs). While more than one fragment of payload remains, that boundary is the end
        // of the next fragment; for the final fragment it is the end of the frame, so RXDONE lands at
        // OTA + rxDoneDelay. Returns false (timer not armed) when the target has already elapsed, i.e. RX
        // should finish immediately.
        private bool RAC_TryArmRxDeliveryTimer()
        {
            uint frameEnd = (uint)currentFrame.Length;
            if((frameEnd - currentFrameOffset) > FRC_RxFragmentLengthInBytes)
            {
                double fragmentTargetUs = ((currentFrameOffset + FRC_RxFragmentLengthInBytes) - MODEM_SyncWordBytes) * currentFrameByteOverTheAirTimeUs;
                if(RAC_TryArmRxTimerAt(fragmentTargetUs))
                {
                    return true;
                }
            }
            double endOfFrameUs = MODEM_GetFrameOverTheAirTimeUs(currentFrame, false, false) + MODEM_GetRxDoneDelayUs();
            return RAC_TryArmRxTimerAt(endOfFrameUs);
        }

        private bool RAC_TryArmRxTimerAt(double targetUs)
        {
            double delayUs = targetUs - currentFrameElapsedAirtimeUs;
            if(delayUs > 0 && PROTIMER_UsToPreCntOverflowTicks(delayUs) > 0)
            {
                currentFrameElapsedAirtimeUs = targetUs;
                rxTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
                rxTimer.Limit = PROTIMER_UsToPreCntOverflowTicks(delayUs);
                rxTimer.Enabled = true;
                return true;
            }
            return false;
        }

        private void RAC_TxTimerLimitReached()
        {
            txTimer.Enabled = false;

            // Normal TX completion: the whole frame went on air. Retain the queue entry briefly (stale) so a
            // receiver whose deferred ReceiveFrame runs just after we finished can still resolve us.
            RAC_ClearOngoingTx(transmissionCompleted: true);

            MODEM_txFrameSentInterrupt.Value = true;
            MODEM_seqTxFrameSentInterrupt.Value = true;
            // TODO: how to check if TXAFTERFRAMEDONE IRQ should be set instead, TXAFTERFRAME command cannot be set (TX is instantaneous)
            FRC_txDoneInterrupt.Value = true;
            FRC_seqTxDoneInterrupt.Value = true;
            if(RAC_txAfterFramePending.Value)
            {
                FRC_txAfterFrameDoneInterrupt.Value = true;
                FRC_seqTxAfterFrameDoneInterrupt.Value = true;
            }

            for(var i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].Enable.Value && PROTIMER_captureCompareChannel[i].Mode.Value == PROTIMER_CaptureCompareMode.Capture)
                {
                    switch(PROTIMER_captureCompareChannel[i].CaptureInputSource.Value)
                    {
                    case PROTIMER_CaptureInputSource.TxDone:
                    case PROTIMER_CaptureInputSource.TxOrRxDone:
                        PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                        PROTIMER_TriggerEvent((PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel0Event + i));
                        break;
                    }
                }
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.TxDone);
            PROTIMER_TriggerEvent(PROTIMER_Event.TxOrRxDone);

            RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxDone);
        }

        private void RAC_HandleTxAfterFrameCommand()
        {
            if(RAC_currentRadioState == RAC_RadioState.Tx || RAC_currentRadioState == RAC_RadioState.RxFrame)
            {
                RAC_txAfterFramePending.Value = true;
            }
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

        private void RAC_SeqTimerRestart(ushort startValue, ushort limit)
        {
            // Little hack: when the limit is set to 0, we actually set to 0xFFFF.
            // STimer triggers on value transition, so it the event will fire when a full wrap around occurs.
            // In practice, by setting the limit to 0xFFFF, it will expire one tick earlier.
            if(limit == 0)
            {
                limit = 0xFFFF;
            }

            seqTimer.Enabled = false;

            seqTimer.Frequency = HfxoFrequency;
            seqTimer.Divider = RAC_seqTimerPrescaler.Value + 1UL;
            seqTimer.Limit = limit;
            seqTimer.Enabled = true;
            seqTimer.Value = startValue;
        }

        private void RAC_SeqTimerStart() => RAC_SeqTimerRestart(0, RAC_SeqTimerLimit);

        // Returns true while the current radio state is a sequencer-serviced transitional state
        // whose STATExxxIF has not yet been cleared.
        private bool RAC_TransitionalStateSeqIfPending()
        {
            switch(RAC_currentRadioState)
            {
            case RAC_RadioState.RxWarm:
                return RAC_seqStateRxWarmInterrupt.Value;
            case RAC_RadioState.Rx2Rx:
                return RAC_seqStateRx2RxInterrupt.Value;
            case RAC_RadioState.Rx2Tx:
                return RAC_seqStateRx2TxInterrupt.Value;
            case RAC_RadioState.RxPoweringDown:
                return RAC_seqStateRxPoweringDownInterrupt.Value;
            case RAC_RadioState.TxWarm:
                return RAC_seqStateTxWarmInterrupt.Value;
            case RAC_RadioState.Tx2Rx:
                return RAC_seqStateTx2RxInterrupt.Value;
            case RAC_RadioState.Tx2Tx:
                return RAC_seqStateTx2TxInterrupt.Value;
            case RAC_RadioState.TxPoweringDown:
                return RAC_seqStateTxPoweringDownInterrupt.Value;
            default:
                return false;
            }
        }

        private void RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal signal = RAC_RadioStateMachineSignal.None)
        {
            this.Log(LogLevel.Debug, "RAC_UpdateRadioStateMachine signal={0} current state={1} at {2}", signal, RAC_currentRadioState, GetTime());

            machine.ClockSource.ExecuteInLock(delegate
            {
                var previousState = RAC_currentRadioState;

                // Super State Transition Priority
                // 1. RESET
                // 2. Sequencer breakpoint triggered
                // 3. FORCESTATE
                // 4. FORCEDISABLE
                // 5. FORCETX
                if(signal == RAC_RadioStateMachineSignal.Reset)
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
                else if(RAC_forceStateActive.Value)
                {
                    RAC_ClearOngoingTxOrRx();
                    RAC_ChangeRadioState(RAC_forceStateTransition.Value);
                    RAC_forceStateActive.Value = false;
                }
                else if(RAC_forceDisable.Value && RAC_currentRadioState == RAC_RadioState.Off)
                {
                    // The RSM remains in OFF as long as the FORCEDISABLE bit is set.
                }
                else if(RAC_forceDisable.Value && RAC_currentRadioState != RAC_RadioState.Shutdown)
                {
                    // Like every normal state-exit in this RSM, the FORCEDISABLE-driven shutdown must
                    // wait for the sequencer to clear a transitional state's handshake before tearing 
                    // the radio down.
                    if(!RAC_TransitionalStateSeqIfPending())
                    {
                        RAC_ClearOngoingTxOrRx();
                        RAC_ChangeRadioState(RAC_RadioState.Shutdown);
                    }
                }
                // FORCETX will make the RSM enter TX. If already in TX, TX is entered through TX2TX. For any other state, 
                // the transition goes through the TXWARM state. The FORCETX is active by issuing the FORCETX command in RAC_CMD 
                // or by triggering by Peripheral Reflex System (PRS). PRS triggering is configured by setting the PRSFORCETX 
                // and setting the PRSFORCETXSEL, both in RAC_CTRL.
                else if(signal == RAC_RadioStateMachineSignal.ForceTx /* TODO: || (PRSFORCETX && PRSFORCETXSEL)*/)
                {
                    if(RAC_currentRadioState == RAC_RadioState.Tx)
                    {
                        RAC_ClearOngoingTx();
                        RAC_ChangeRadioState(RAC_RadioState.Tx2Tx);
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
                        // Like every normal state-exit in this RSM, the Shutdown->Off transition must wait
                        // for the sequencer to service (clear) its STATESHUTDOWN handshake.
                        if(!RAC_exitShutdownDisable.Value
                           && !(RAC_seqStateShutDownInterruptEnable.Value && RAC_seqStateShutDownInterrupt.Value))
                        {
                            RAC_ChangeRadioState(RAC_RadioState.Off);
                        }
                        break;
                    }
                    case RAC_RadioState.Off:
                    {
                        if(signal == RAC_RadioStateMachineSignal.TxEnable)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.TxWarm);
                        }
                        else if(RAC_RxEnable)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.RxWarm);
                        }
                        break;
                    }
                    case RAC_RadioState.TxWarm:
                    {
                        if(signal == RAC_RadioStateMachineSignal.TxWarmIrqCleared)
                        {
                            if(RAC_TxEnable)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx);
                            }
                            else
                            {
                                RAC_ChangeRadioState(RAC_RadioState.TxPoweringDown);
                            }
                        }
                        break;
                    }
                    case RAC_RadioState.RxWarm:
                    {
                        if(signal == RAC_RadioStateMachineSignal.RxWarmIrqCleared)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.RxSearch);
                        }
                        break;
                    }
                    case RAC_RadioState.RxPoweringDown:
                    {
                        if(signal == RAC_RadioStateMachineSignal.RxPowerDownIrqCleared)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.Off);
                        }
                        break;
                    }
                    case RAC_RadioState.TxPoweringDown:
                    {
                        if(signal == RAC_RadioStateMachineSignal.TxPowerDownIrqCleared)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.Off);
                        }
                        break;
                    }
                    case RAC_RadioState.RxOverflow:
                    {
                        if(signal == RAC_RadioStateMachineSignal.ClearRxOverflow)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.Off);
                        }
                        break;
                    }
                    case RAC_RadioState.Rx2Rx:
                    {
                        if(signal == RAC_RadioStateMachineSignal.Rx2RxIrqCleared)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.RxSearch);
                        }
                        break;
                    }
                    case RAC_RadioState.Rx2Tx:
                    {
                        if(signal == RAC_RadioStateMachineSignal.RxOverflow)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.RxOverflow);
                        }
                        else if(signal == RAC_RadioStateMachineSignal.Rx2TxIrqCleared)
                        {
                            if(RAC_TxEnable || RAC_txAfterFrameActive.Value)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx);
                            }
                            else if(RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx2Rx);
                            }
                            else
                            {
                                RAC_ChangeRadioState(RAC_RadioState.TxPoweringDown);
                            }
                        }
                        break;
                    }
                    case RAC_RadioState.Tx2Rx:
                    {
                        if(signal == RAC_RadioStateMachineSignal.Tx2RxIrqCleared)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.RxSearch);
                        }
                        break;
                    }
                    case RAC_RadioState.Tx2Tx:
                    {
                        if(signal == RAC_RadioStateMachineSignal.Tx2TxIrqCleared)
                        {
                            if(RAC_TxEnable)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx);
                            }
                            else if(RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx2Rx);
                            }
                            else
                            {
                                RAC_ChangeRadioState(RAC_RadioState.TxPoweringDown);
                            }
                        }
                        break;
                    }
                    case RAC_RadioState.RxSearch:
                    {
                        if(signal == RAC_RadioStateMachineSignal.FrameDetected)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.RxFrame);
                        }
                        else if(signal == RAC_RadioStateMachineSignal.TxEnable)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.Rx2Tx);
                        }
                        else if(signal == RAC_RadioStateMachineSignal.RxCalibration)
                        {
                            RAC_ChangeRadioState(RAC_RadioState.RxWarm);
                        }
                        else
                        {
                            // RAC->CMD.RSDIS causes the RXEN signal to pulse low for a clock cycle
                            if(!RAC_RxEnable || signal == RAC_RadioStateMachineSignal.RxDisable)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.RxPoweringDown);
                            }
                        }
                        break;
                    }
                    case RAC_RadioState.RxFrame:
                    {
                        if(signal == RAC_RadioStateMachineSignal.RxOverflow)
                        {
                            RAC_ClearOngoingRx();
                            RAC_ChangeRadioState(RAC_RadioState.RxOverflow);
                        }
                        // Here we should just move to the next state when the FRC fully received a frame.
                        // However, we have a race condition for which firing the RXFRAME_IrqHandler() with the radio state 
                        // already transitioned to the next state results in unexpected behavior from a software perspective 
                        // (which results in the  RX_Complete() firing from RXFRAME_IrqHandler()).
                        // To cope with this we progress to the next state only if both the RXFRAME IRQ flag has been cleared
                        // and the FRC has completed RXing a frame.
                        else if(signal == RAC_RadioStateMachineSignal.RxFrameIrqCleared
                                 || signal == RAC_RadioStateMachineSignal.RxDone
                                 || signal == RAC_RadioStateMachineSignal.RxAbort)
                        {
                            if(signal == RAC_RadioStateMachineSignal.RxFrameIrqCleared)
                            {
                                FRC_rxFrameIrqClearedPending = true;
                            }
                            if(signal == RAC_RadioStateMachineSignal.RxDone)
                            {
                                FRC_rxDonePending = true;
                            }

                            if(signal == RAC_RadioStateMachineSignal.RxAbort
                                || (FRC_rxFrameIrqClearedPending && FRC_rxDonePending))
                            {
                                RAC_ClearOngoingRx();

                                if(RAC_TxEnable || RAC_txAfterFramePending.Value)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.Rx2Tx);
                                }
                                else if(RAC_RxEnable)
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.Rx2Rx);
                                }
                                else
                                {
                                    RAC_ChangeRadioState(RAC_RadioState.RxPoweringDown);
                                }
                            }
                        }
                        break;
                    }
                    case RAC_RadioState.Tx:
                    {
                        if(signal == RAC_RadioStateMachineSignal.TxDisable)
                        {
                            RAC_ClearOngoingTx();

                            if(RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx2Rx);
                            }
                            else
                            {
                                RAC_ChangeRadioState(RAC_RadioState.TxPoweringDown);
                            }
                        }
                        else if(signal == RAC_RadioStateMachineSignal.TxDone) // FRC ends TX
                        {
                            if(RAC_TxEnable || RAC_txAfterFramePending.Value)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx2Tx);
                            }
                            else if(RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(RAC_RadioState.Tx2Rx);
                            }
                            else
                            {
                                RAC_ChangeRadioState(RAC_RadioState.TxPoweringDown);
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

                if(previousState != RAC_currentRadioState)
                {
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "RSM Update at {0}, signal:{1} seqTimer={2} channel={3} ({4}), transition: {5}->{6} (TX={7} RX={8}) Lbt={9}",
                             GetTime(), signal, seqTimer.Value, LogicalChannel, MODEM_GetPhySignature(), previousState, RAC_currentRadioState, RAC_internalTxState, RAC_internalRxState, PROTIMER_listenBeforeTalkState);

                    RAC_seqRadioStateChangeInterrupt.Value = true;

                    // On a state transition, the STimer is always restarted from 0
                    RAC_SeqTimerRestart(0, RAC_seqTimerLimit);

                    switch(RAC_currentRadioState)
                    {
                    case RAC_RadioState.Off:
                        RAC_seqStateOffInterrupt.Value = true;
                        break;
                    case RAC_RadioState.RxWarm:
                        RAC_seqStateRxWarmInterrupt.Value = true;
                        break;
                    case RAC_RadioState.RxSearch:
                        // Reset PKTBUFCOUNT when entering in RxSearch
                        FRC_packetBufferCount.Value = 0;
                        RAC_seqStateRxSearchInterrupt.Value = true;
                        MODEM_demodulatorState.Value = MODEM_DemodulatorState.PreambleSearch;
                        FRC_UpdateRawMode();
                        break;
                    case RAC_RadioState.RxFrame:
                        RAC_seqStateRxFrameInterrupt.Value = true;
                        MODEM_demodulatorState.Value = MODEM_DemodulatorState.RxFrame;
                        break;
                    case RAC_RadioState.RxPoweringDown:
                        RAC_seqStateRxPoweringDownInterrupt.Value = true;
                        break;
                    case RAC_RadioState.Rx2Rx:
                        RAC_seqStateRx2RxInterrupt.Value = true;
                        break;
                    case RAC_RadioState.RxOverflow:
                        RAC_seqStateRxOverflowInterrupt.Value = true;
                        break;
                    case RAC_RadioState.Rx2Tx:
                        RAC_seqStateRx2TxInterrupt.Value = true;
                        break;
                    case RAC_RadioState.TxWarm:
                        RAC_seqStateTxWarmInterrupt.Value = true;
                        break;
                    case RAC_RadioState.Tx:
                        RAC_seqStateTxInterrupt.Value = true;
                        // We assemble and "transmit" the frame immediately so that receiver nodes 
                        // can transition from RX_SEARCH to RX_FRAME immediately. 
                        // We use timers to properly time the completion of the transmission process.
                        var frame = FRC_AssembleFrame();
                        TransmitFrame(frame);
                        break;
                    case RAC_RadioState.TxPoweringDown:
                        RAC_seqStateTxPoweringDownInterrupt.Value = true;
                        break;
                    case RAC_RadioState.Tx2Rx:
                        RAC_seqStateTx2RxInterrupt.Value = true;
                        break;
                    case RAC_RadioState.Tx2Tx:
                        RAC_seqStateTx2TxInterrupt.Value = true;
                        break;
                    case RAC_RadioState.Shutdown:
                        RAC_seqStateShutDownInterrupt.Value = true;
                        break;
                    default:
                        this.Log(LogLevel.Error, "Invalid Radio State ({0}).", RAC_currentRadioState);
                        break;
                    }

                    if(RAC_currentRadioState != RAC_RadioState.RxSearch && RAC_currentRadioState != RAC_RadioState.RxFrame)
                    {
                        MODEM_demodulatorState.Value = MODEM_DemodulatorState.Off;
                    }

                    AGC_UpdateRssiState();

                    // If we just entered RxSearch state, we should restart RSSI sampling.
                    // However, for performance reasons, we rely on the InterferenceQueue notifications,
                    // so we simply update the RSSI here.
                    if(RAC_currentRadioState == RAC_RadioState.RxSearch)
                    {
                        AGC_UpdateRssi();
                    }
                    // If entered a state other than RxSearch or RxFrame, we stop the Rssi sampling.
                    else if(RAC_currentRadioState != RAC_RadioState.RxSearch && RAC_currentRadioState != RAC_RadioState.RxFrame)
                    {
                        AGC_StopRssiTimer();
                    }
                }

                UpdateInterrupts();
            });
        }

        private void RAC_SeqTimerHandleLimitReached()
        {
            // Handle 16-bit wrap around
            if(seqTimer.Limit == 0xFFFF)
            {
                RAC_SeqTimerRestart(0, RAC_seqTimerLimit);

                // We actually hit the compare value, fire the interrupt.
                if(RAC_seqTimerLimit == 0 || RAC_seqTimerLimit == 0xFFFF)
                {
                    RAC_seqStimerCompareEventInterrupt.Value = true;
                    UpdateInterrupts();
                }
            }
            else
            {
                if(RAC_seqTimerCompareAction.Value == RAC_SeqTimerCompareAction.Continue)
                {
                    RAC_SeqTimerRestart((ushort)seqTimer.Limit, 0xFFFF);
                }
                else if(RAC_seqTimerCompareAction.Value == RAC_SeqTimerCompareAction.Wrap)
                {
                    RAC_SeqTimerRestart(0, RAC_seqTimerLimit);
                }
                else
                {
                    this.Log(LogLevel.Error, "RAC_SeqTimerHandleLimitReached(), invalid compare action mode {0}", RAC_seqTimerCompareAction.Value);
                    return;
                }

                RAC_seqStimerCompareEventInterrupt.Value = true;
                UpdateInterrupts();
            }
        }

        private void RAC_ClearOngoingRx()
        {
            FRC_rxFrameIrqClearedPending = false;
            FRC_rxDonePending = false;
            rxTimer.Enabled = false;
            RAC_ongoingRxCollided = false;
            RAC_internalRxState = RAC_InternalRxState.Idle;
        }

        private void RAC_ClearOngoingTxOrRx()
        {
            RAC_ClearOngoingRx();
            RAC_ClearOngoingTx();
        }

        private void RAC_ChangeRadioState(RAC_RadioState newState)
        {
            if(newState != RAC_currentRadioState)
            {
                RAC_previous3RadioState = RAC_previous2RadioState;
                RAC_previous2RadioState = RAC_previous1RadioState;
                RAC_previous1RadioState = RAC_currentRadioState;
                RAC_currentRadioState = newState;
            }
        }

        // SYNTH-tuned RF center frequency in Hz, quantized to the nearest 10 kHz
        // or 0 if unprogrammed.
        private ulong SYNTH_GetQuantizedRfFrequency()
        {
            var loDiv = SYNTH_DecodeLoDiv(SYNTH_loDivFreqCtrl.Value);
            if(loDiv == 0)
            {
                return 0;
            }
            var frequency = SYNTH_frequency.Value * HfxoFrequency / (1UL << 19) / loDiv;
            return (frequency + 5_000UL) / 10_000UL * 10_000UL;
        }

        private string SYNTH_GetRfFrequencyString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F2}MHz", SYNTH_GetQuantizedRfFrequency() / 1_000_000.0);
        }

        private uint SYNTH_DecodeLoDiv(SYNTH_LoDivFreqCtrl encoded)
        {
            switch(encoded)
            {
            case SYNTH_LoDivFreqCtrl.LoDiv1: return 1;
            case SYNTH_LoDivFreqCtrl.LoDiv2: return 2;
            case SYNTH_LoDivFreqCtrl.LoDiv3: return 3;
            case SYNTH_LoDivFreqCtrl.LoDiv4: return 4;
            case SYNTH_LoDivFreqCtrl.LoDiv5: return 5;
            case SYNTH_LoDivFreqCtrl.LoDiv6: return 6;
            case SYNTH_LoDivFreqCtrl.LoDiv7: return 7;
            case SYNTH_LoDivFreqCtrl.LoDiv8: return 8;
            case SYNTH_LoDivFreqCtrl.LoDiv9: return 9;
            case SYNTH_LoDivFreqCtrl.LoDiv10: return 10;
            case SYNTH_LoDivFreqCtrl.LoDiv12: return 12;
            case SYNTH_LoDivFreqCtrl.LoDiv14: return 14;
            case SYNTH_LoDivFreqCtrl.LoDiv15: return 15;
            case SYNTH_LoDivFreqCtrl.LoDiv16: return 16;
            case SYNTH_LoDivFreqCtrl.LoDiv18: return 18;
            case SYNTH_LoDivFreqCtrl.LoDiv20: return 20;
            case SYNTH_LoDivFreqCtrl.LoDiv24: return 24;
            default: return 0; // unknown / not yet programmed
            }
        }

        // TransmitFrame() is invoked as soon as the radio state machine transitions to the TX state.
        private bool TransmitFrame(byte[] frame)
        {
            TrySyncTime();

            if(RAC_internalTxState != RAC_InternalTxState.Idle)
            {
                this.Log(LogLevel.Error, "TransmitFrame(): state not IDLE");
                return false;
            }

            RAC_TxEnable = false;

            if(frame.Length == 0)
            {
                return false;
            }

            // We schedule the TX timer to include the whole frame (including the preamble and SYNC word) plus the 
            // TxChainDelay and TxChainDoneDelay, so that when the timer expires, we can simply complete the TX process.
            // Note, we subtract the TxDoneDelay since that signal occurs BEFORE the last bit of the frame actually went over the air.
            var timerDelayUs = MODEM_GetFrameOverTheAirTimeUs(frame, true, true) + MODEM_GetTxChainDelayUs() - MODEM_GetTxChainDoneDelayUs();

            this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Sending frame at {0} on channel {1} ({2}) ({3}): {4}",
                     GetTime(), LogicalChannel, MODEM_GetPhySignature(), SYNTH_GetRfFrequencyString(), BitConverter.ToString(frame));

            this.Log(LogLevel.Noisy, "TX timer delay (us)={0} (PRECNT overflows={1}) (OTA frame time (us)={2})",
                     timerDelayUs, PROTIMER_UsToPreCntOverflowTicks(timerDelayUs), MODEM_GetFrameOverTheAirTimeUs(frame, true, true));

            InterferenceQueue.Add(this, MODEM_GetPhySignature(), Channel, 0 /*TODO: TxPower*/, frame);

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

        private ulong HfxoFrequency
        {
            get
            {
                if(Object.ReferenceEquals(cmu, null))
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_CMU>())
                    {
                        cmu = peripheral;
                        break;
                    }
                }

                return cmu.HfxoFrequency;
            }
        }

        // Last value RAIL wrote to SYNTH.CHSP. RAIL stores the protocol's logical channel
        // index there (e.g. 11..26 for IEEE 802.15.4). Used only for human-readable log
        // strings; routing equality goes through Channel.
        private int LogicalChannel => (int)SYNTH_channelSpacing.Value;

        private uint FRC_FrameLength => (uint)FRC_frameLength.Value + 1;

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
                    this.Log(LogLevel.Error, "Setting illegal FRC_ActiveTransmitFrameDescriptor value.");
                    return;
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
                    this.Log(LogLevel.Error, "Setting illegal FRC_ActiveReceiveFrameDescriptor value.");
                    return;
                }

                FRC_activeReceiveFrameDescriptor.Value = (value == 3);
            }
        }

        private bool RAC_RxEnable => RAC_RxEnableMask != 0;

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

                if(risingEdge)
                {
                    RAC_UpdateRadioStateMachine(RAC_RadioStateMachineSignal.TxEnable);
                }
            }
        }

        private uint MODEM_SyncWord1 => (uint)MODEM_syncWord1.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);

        private uint MODEM_SyncWord0 => (uint)MODEM_syncWord0.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);

        private uint MODEM_TxSyncWord => (MODEM_dualSync.Value && MODEM_txSync.Value) ? (uint)MODEM_syncWord1.Value : (uint)MODEM_syncWord0.Value;

        private uint MODEM_SyncWordBytes => ((uint)MODEM_syncBits.Value >> 3) + 1;

        private uint MODEM_SyncWordLength => (uint)MODEM_syncBits.Value + 1;

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
                if((AGC_ccaRssiPeriodEnable.Value && PROTIMER_listenBeforeTalkState == PROTIMER_ListenBeforeTalkState.CcaDelay))
                {
                    return AGC_OQPSK250KbpsPhyCcaRssiMeasurePeriodUs;
                }
                else if(AGC_rssiStartCommandOngoing)
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

        private sbyte AGC_FrameRssiIntegerPartAdjusted => (sbyte)(AGC_FrameRssiIntegerPart + AGC_RssiWrapCompensationOffsetDbm);

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

        private byte AGC_FrameRssiFractionalPart
        {
            get
            {
                // TODO: for now Frame AGC fractional part is always 0
                return 0;
            }
        }

        private sbyte AGC_RssiIntegerPartAdjusted => (sbyte)(AGC_RssiIntegerPart + AGC_RssiWrapCompensationOffsetDbm);

        private sbyte AGC_RssiIntegerPart { get; set; }

        private byte AGC_RssiFractionalPart
        {
            get
            {
                // TODO: for now AGC fractional part is always 0
                return 0;
            }
        }

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
                if(value)
                {
                    // First stop the timer
                    proTimer.Enabled = false;

                    // TODO: If it is already running, do we need to update the BASECNT,WRAPCNT,etc?

                    // The proTimer timer is configured so that each tick corresponds to a PRECNT overflow.
                    // The PRECNTTOP value is 1 less than the intended value.
                    double frequency = (double)HfxoFrequency / (PROTIMER_preCounterTopInteger.Value + 1 + ((double)PROTIMER_preCounterTopFractional.Value / 256));

                    proTimer.Frequency = (uint)frequency;
                    proTimer.Limit = PROTIMER_ComputeTimerLimit();
                    proTimer.Value = 0;
                    proTimer.Enabled = true;
                }
                else
                {
                    if(proTimer.Enabled)
                    {
                        TrySyncTime();
                        uint currentIncrement = (uint)proTimer.Value;
                        proTimer.Enabled = false;

                        // Handle the current increment
                        if(currentIncrement > 0)
                        {
                            PROTIMER_HandlePreCntOverflows(currentIncrement);
                        }
                    }
                    else
                    {
                        proTimer.Enabled = false;
                    }
                }
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
                if(PROTIMER_txEnable)
                {
                    RAC_TxEnable = true;
                }
            }
        }

        private bool RAC_PaOutputLevelRampingInProgress => paRampingTimer.Enabled;

        private bool RAC_PaOutputLevelRamping
        {
            get
            {
                return RAC_paOutputLevelRamping;
            }

            set
            {
                // Ramping the PA up or down
                if(value != RAC_paOutputLevelRamping)
                {
                    RAC_paOutputLevelRamping = value;

                    // TODO: if the MODEM PA ramping is disabled, we should ramp up using the MODEM->RAMPCTRL.RAMPVAL value.
                    if(!MODEM_rampDisable.Value)
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

        private ushort RAC_SeqTimerLimit
        {
            get
            {
                return RAC_seqTimerLimit;
            }

            set
            {
                RAC_seqTimerLimit = value;
                ushort limit;

                TrySyncTime();

                if(RAC_seqTimerLimit == 0 || RAC_seqTimerLimit < seqTimer.Value)
                {
                    limit = 0xFFFF;
                }
                else
                {
                    limit = RAC_seqTimerLimit;
                }

                RAC_SeqTimerRestart((ushort)seqTimer.Value, limit);
            }
        }

        private uint RAC_SeqTimerValue
        {
            get
            {
                TrySyncTime();
                return (uint)seqTimer.Value;
            }
        }

        private uint CRC_CrcWidth => (uint)CRC_crcWidthMode.Value + 1;

        private IFlagRegisterField PROTIMER_listenBeforeTalkSuccessInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkFailureInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkRetryInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkTimeoutCounterMatchInterrupt;
        private IFlagRegisterField PROTIMER_rtccSynchedInterrupt;
        private IFlagRegisterField PROTIMER_seqPreCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_seqBaseCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_seqWrapCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkSuccessInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkFailureInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkRetryInterruptEnable;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterruptEnable;
        private IFlagRegisterField PROTIMER_seqRtccSynchedInterruptEnable;
        private IFlagRegisterField PROTIMER_seqPreCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_seqBaseCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_seqWrapCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_seqRtccSynchedInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkTimeoutCounterMatchInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkRetryInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkSuccessInterrupt;
        private IFlagRegisterField PROTIMER_wrapCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_seqListenBeforeTalkFailureInterrupt;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxClearEvent2;
        private IFlagRegisterField PROTIMER_preCounterOverflowInterrupt;
        private bool PROTIMER_listenBeforeTalkPending = false;
        private PROTIMER_ListenBeforeTalkState PROTIMER_listenBeforeTalkState = PROTIMER_ListenBeforeTalkState.Idle;
        private PROTIMER_TxRxRequestState PROTIMER_rxRequestState = PROTIMER_TxRxRequestState.Idle;
        private PROTIMER_TxRxRequestState PROTIMER_txRequestState = PROTIMER_TxRxRequestState.Idle;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_txSetEvent2;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_txSetEvent1;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxClearEvent1;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxSetEvent2;
        private IEnumRegisterField<PROTIMER_Event> PROTIMER_rxSetEvent1;
        private bool PROTIMER_txEnable = false;
        private IValueRegisterField PROTIMER_wrapCounterTop;
        private IValueRegisterField PROTIMER_baseCounterTop;
        private IValueRegisterField PROTIMER_preCounterTopFractional;
        private IValueRegisterField PROTIMER_preCounterTopInteger;
        private bool PROTIMER_rtcWait = false;
        private uint PROTIMER_wrapCounterValue = 0;
        private IFlagRegisterField PROTIMER_listenBeforeTalkSync;
        private IFlagRegisterField PROTIMER_baseCounterOverflowInterrupt;
        private IFlagRegisterField PROTIMER_listenBeforeTalkRunning;
        private IValueRegisterField PROTIMER_listenBeforeTalkStartExponent;
        private IFlagRegisterField PROTIMER_rtccSynchedInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkTimeoutCounterMatchInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkRetryInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkFailureInterruptEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkSuccessInterruptEnable;
        private IFlagRegisterField PROTIMER_wrapCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_baseCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_preCounterOverflowInterruptEnable;
        private IFlagRegisterField PROTIMER_rtccTriggerFromPrsEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkPrsStartEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkPrsPauseEnable;
        private IFlagRegisterField PROTIMER_listenBeforeTalkPrsStopEnable;
        private IValueRegisterField PROTIMER_ccaCounter;
        private IValueRegisterField PROTIMER_retryLimit;
        private IFlagRegisterField PROTIMER_fixedBackoff;
        // Last value software wrote to the PROTIMER RANDOM register. While FIXEDBACKOFF is set this is
        // frozen (writes are ignored) to mirror hardware, so the deterministic LBT backoff used for
        // scheduled CSL transmissions cannot be corrupted by later PRNG reseeds.
        private uint PROTIMER_fixedBackoffRandomValue = 1;
        private IValueRegisterField PROTIMER_ccaRepeat;
        private IValueRegisterField PROTIMER_ccaDelay;
        private IValueRegisterField PROTIMER_listenBeforeTalkRetryCounter;
        private IValueRegisterField PROTIMER_listenBeforeTalkExponent;
        private IValueRegisterField PROTIMER_listenBeforeTalkMaxExponent;
        private IFlagRegisterField PROTIMER_listenBeforeTalkPaused;
        private IEnumRegisterField<PROTIMER_PreCounterSource> PROTIMER_preCounterSource;
        private IEnumRegisterField<PROTIMER_BaseCounterSource> PROTIMER_baseCounterSource;
        private IEnumRegisterField<PROTIMER_WrapCounterSource> PROTIMER_wrapCounterSource;
        private ushort PROTIMER_baseCounterValue = 0;
        private uint PROTIMER_preCounterSourcedBitmask = 0;

        private IFlagRegisterField MODEM_txPreambleSentInterrupt;
        private IFlagRegisterField MODEM_txSyncSentInterrupt;
        private IFlagRegisterField MODEM_txFrameSentInterrupt;
        private IFlagRegisterField MODEM_txPreambleSentInterruptEnable;
        private bool MODEM_txRampingDoneInterrupt = true;
        private IFlagRegisterField MODEM_rxPreambleDetectedInterrupt;
        private IFlagRegisterField MODEM_txRampingDoneInterruptEnable;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord1DetectedInterrupt;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord0DetectedInterrupt;
        private IFlagRegisterField MODEM_rxPreambleLostInterrupt;
        private IFlagRegisterField MODEM_txFrameSentInterruptEnable;
        private IFlagRegisterField MODEM_txSyncSentInterruptEnable;
        private IFlagRegisterField MODEM_rxPreambleDetectedInterruptEnable;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord0DetectedInterruptEnable;
        private IFlagRegisterField MODEM_rampDisable;
        private IFlagRegisterField MODEM_rxPreambleLostInterruptEnable;
        private IValueRegisterField MODEM_syncBits;
        private IValueRegisterField MODEM_txBases;
        private IValueRegisterField MODEM_baseBits;
        private IFlagRegisterField MODEM_seqRxPreambleLostInterruptEnable;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord1DetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord0DetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqRxPreambleDetectedInterruptEnable;
        private IFlagRegisterField MODEM_seqTxRampingDoneInterruptEnable;
        private IFlagRegisterField MODEM_dualSync;
        private IFlagRegisterField MODEM_seqTxPreambleSentInterruptEnable;
        private IFlagRegisterField MODEM_seqTxFrameSentInterruptEnable;
        private IFlagRegisterField MODEM_seqRxPreambleLostInterrupt;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord1DetectedInterrupt;
        private IFlagRegisterField MODEM_seqRxFrameWithSyncWord0DetectedInterrupt;
        private IFlagRegisterField MODEM_seqRxPreambleDetectedInterrupt;
        private IFlagRegisterField MODEM_seqTxPreambleSentInterrupt;
        private IFlagRegisterField MODEM_seqTxSyncSentInterrupt;
        private IFlagRegisterField MODEM_seqTxFrameSentInterrupt;
        private IFlagRegisterField MODEM_seqTxSyncSentInterruptEnable;
        private IFlagRegisterField MODEM_rxFrameWithSyncWord1DetectedInterruptEnable;
        private IFlagRegisterField MODEM_txSync;
        private IValueRegisterField MODEM_syncWord0;
        private IEnumRegisterField<MODEM_DsssDoublingMode> MODEM_dsssDoublingMode;
        private IEnumRegisterField<MODEM_SymbolCoding> MODEM_symbolCoding;
        private IEnumRegisterField<MODEM_ModulationFormat> MODEM_modulationFormat;
        private IValueRegisterField MODEM_dsssShifts;
        private IValueRegisterField MODEM_dsssLength;
        private IValueRegisterField MODEM_baudrateDivisionFactorB;
        private IEnumRegisterField<MODEM_RateSelectMode> MODEM_rateSelectMode;
        private IValueRegisterField MODEM_baudrateDivisionFactorA;
        private IFlagRegisterField MODEM_syncData;
        private IValueRegisterField MODEM_txBaudrateDenominator;
        private IEnumRegisterField<MODEM_DemodulatorState> MODEM_demodulatorState;
        private IFlagRegisterField MODEM_viterbiDemodulatorEnable;
        private IValueRegisterField MODEM_rampValue;
        private IValueRegisterField MODEM_rampRate2;
        private IValueRegisterField MODEM_rampRate1;
        private IValueRegisterField MODEM_rampRate0;
        private IFlagRegisterField MODEM_frameDetectedId;
        private IValueRegisterField MODEM_syncWord1;
        private IValueRegisterField MODEM_txBaudrateNumerator;

        private IValueRegisterField AGC_rssiNegativeStepThreshold;
        private IValueRegisterField AGC_rssiPositiveStepThreshold;
        private IValueRegisterField AGC_subPeriodInteger;
        private IFlagRegisterField AGC_subPeriod;
        private IValueRegisterField AGC_rssiShift;
        private IFlagRegisterField AGC_ccaRssiPeriodEnable;
        private IValueRegisterField AGC_ccaRssiPeriod;
        private IEnumRegisterField<AGC_RssiState> AGC_rssiState;
        private IValueRegisterField AGC_powerMeasurePeriod;
        private IValueRegisterField AGC_rssiMeasurePeriod;
        private IValueRegisterField AGC_ccaThreshold;
        private IFlagRegisterField AGC_cca;
        private sbyte AGC_frameRssiIntegerPart;
        private sbyte AGC_rssiSecondLastRead = AGC_RssiInvalid;
        private sbyte AGC_rssiLastRead = AGC_RssiInvalid;
        private bool AGC_rssiStartCommandFromProtimer = false;
        private IFlagRegisterField AGC_rssiStepPeriod;
        private IFlagRegisterField AGC_rssiValidInterrupt;
        private IFlagRegisterField AGC_ccaInterrupt;
        private IFlagRegisterField AGC_rssiDoneInterrupt;
        private IFlagRegisterField AGC_seqRssiNegativeStepInterruptEnable;
        private IFlagRegisterField AGC_seqRssiPositiveStepInterruptEnable;
        private IFlagRegisterField AGC_seqRssiDoneInterruptEnable;
        private IFlagRegisterField AGC_seqCcaInterruptEnable;
        private IFlagRegisterField AGC_seqRssiValidInterruptEnable;
        private IFlagRegisterField AGC_seqRssiNegativeStepInterrupt;
        private IFlagRegisterField AGC_seqRssiPositiveStepInterrupt;
        private bool AGC_rssiStartCommandOngoing = false;
        private IFlagRegisterField AGC_seqRssiDoneInterrupt;
        private IFlagRegisterField AGC_seqRssiValidInterrupt;
        private IFlagRegisterField AGC_rssiNegativeStepInterruptEnable;
        private IFlagRegisterField AGC_rssiPositiveStepInterruptEnable;
        private IFlagRegisterField AGC_rssiDoneInterruptEnable;
        private IFlagRegisterField AGC_ccaInterruptEnable;
        private IFlagRegisterField AGC_rssiValidInterruptEnable;
        private IFlagRegisterField AGC_rssiNegativeStepInterrupt;
        private IFlagRegisterField AGC_rssiPositiveStepInterrupt;
        private IFlagRegisterField AGC_seqCcaInterrupt;

        private IValueRegisterField CRC_crcBitsPerWord;
        private IFlagRegisterField CRC_reverseCrcByteOrdering;
        private IEnumRegisterField<CRC_CrcWidthMode> CRC_crcWidthMode;

        private IFlagRegisterField FRC_seqRxAbortedInterrupt;
        private IFlagRegisterField FRC_packetBufferStartInterruptEnable;
        private IFlagRegisterField FRC_txRawEventInterruptEnable;
        private IFlagRegisterField FRC_rxRawEventInterruptEnable;
        private IFlagRegisterField FRC_rxOverflowInterruptEnable;
        private IFlagRegisterField FRC_frameErrorInterruptEnable;
        private IFlagRegisterField FRC_rxAbortedInterruptEnable;
        private IFlagRegisterField FRC_rxDoneInterruptEnable;
        private IFlagRegisterField FRC_txUnderflowInterruptEnable;
        private IFlagRegisterField FRC_txAfterFrameDoneInterruptEnable;
        private IFlagRegisterField FRC_txDoneInterruptEnable;
        private IFlagRegisterField FRC_packetBufferThresholdInterrupt;
        private IFlagRegisterField FRC_packetBufferStartInterrupt;
        private IFlagRegisterField FRC_txRawEventInterrupt;
        private IFlagRegisterField FRC_rxRawEventInterrupt;
        private IFlagRegisterField FRC_rxOverflowInterrupt;
        private IFlagRegisterField FRC_frameErrorInterrupt;
        private IFlagRegisterField FRC_rxAbortedInterrupt;
        private IFlagRegisterField FRC_packetBufferThresholdInterruptEnable;
        private IFlagRegisterField FRC_rxDoneInterrupt;
        private IFlagRegisterField FRC_seqTxDoneInterrupt;
        private IFlagRegisterField FRC_seqTxUnderflowInterrupt;
        private IFlagRegisterField FRC_seqPacketBufferStartInterruptEnable;
        private IFlagRegisterField FRC_seqTxRawEventInterruptEnable;
        private IFlagRegisterField FRC_seqRxRawEventInterruptEnable;
        private IFlagRegisterField FRC_seqFrameErrorInterruptEnable;
        private IFlagRegisterField FRC_seqRxAbortedInterruptEnable;
        private IFlagRegisterField FRC_seqRxDoneInterruptEnable;
        private IFlagRegisterField FRC_seqTxUnderflowInterruptEnable;
        private IFlagRegisterField FRC_seqTxAfterFrameDoneInterruptEnable;
        private IFlagRegisterField FRC_seqTxDoneInterruptEnable;
        private IFlagRegisterField FRC_seqPacketBufferThresholdInterrupt;
        private IFlagRegisterField FRC_seqPacketBufferStartInterrupt;
        private IFlagRegisterField FRC_seqTxRawEventInterrupt;
        private IFlagRegisterField FRC_seqRxRawEventInterrupt;
        private IFlagRegisterField FRC_seqRxOverflowInterrupt;
        private IFlagRegisterField FRC_seqFrameErrorInterrupt;
        private IFlagRegisterField FRC_seqRxDoneInterrupt;
        private IFlagRegisterField FRC_seqTxAfterFrameDoneInterrupt;
        private IFlagRegisterField FRC_seqPacketBufferThresholdInterruptEnable;
        private IFlagRegisterField FRC_txUnderflowInterrupt;
        private IFlagRegisterField FRC_txDoneInterrupt;
        private IValueRegisterField FRC_maxDecodedLength;
        private IFlagRegisterField FRC_dynamicFrameCrcIncluded;
        private IValueRegisterField FRC_minDecodedLength;
        private IValueRegisterField FRC_dynamicFrameLengthBits;
        private IValueRegisterField FRC_dynamicFrameLengthOffset;
        private IValueRegisterField FRC_dynamicFrameLengthBitShift;
        private IEnumRegisterField<FRC_DynamicFrameLengthBitOrder> FRC_dynamicFrameLengthBitOrder;
        private IEnumRegisterField<FRC_DynamicFrameLengthMode> FRC_dynamicFrameLengthMode;
        private IEnumRegisterField<FRC_RxRawDataTriggerMode> FRC_rxRawDataTriggerSelect;
        private IEnumRegisterField<FRC_RxRawDataMode> FRC_rxRawDataSelect;
        private IFlagRegisterField FRC_enableRawDataRandomNumberGenerator;
        private IValueRegisterField FRC_fsmState;
        private IFlagRegisterField FRC_rxRawBlocked;
        private IFlagRegisterField FRC_activeReceiveFrameDescriptor;
        private IFlagRegisterField FRC_activeTransmitFrameDescriptor;
        private bool FRC_rxFrameIrqClearedPending = false;
        private bool FRC_rxDonePending = false;
        private IValueRegisterField FRC_initialDecodedFrameLength;
        private IFlagRegisterField FRC_txAfterFrameDoneInterrupt;
        private IValueRegisterField FRC_wordCounter;
        private IValueRegisterField FRC_lengthFieldLocation;
        private IValueRegisterField FRC_addressFieldLocation;
        private IValueRegisterField FRC_packetBufferCount;
        private IFlagRegisterField FRC_packetBufferThresholdEnable;
        private IValueRegisterField FRC_packetBufferThreshold;
        private IValueRegisterField FRC_packetBufferStartAddress;
        private IFlagRegisterField FRC_rxAppendRtcStamp;
        private IFlagRegisterField FRC_rxAppendProtimerCc0HighWrap;
        private IFlagRegisterField FRC_rxAppendProtimerCc0LowWrap;
        private IFlagRegisterField FRC_rxAppendProtimerCc0base;
        private IFlagRegisterField FRC_rxAppendCrcOk;
        private IFlagRegisterField FRC_rxAppendRssi;
        private IFlagRegisterField FRC_rxBufferRestoreOnRxAborted;
        private IFlagRegisterField FRC_rxBufferRestoreOnFrameError;
        private IFlagRegisterField FRC_rxBufferClear;
        private IFlagRegisterField FRC_rxAcceptCrcErrors;
        private IFlagRegisterField FRC_rxStoreCrc;
        private IEnumRegisterField<FRC_FrameDescriptorMode> FRC_rxFrameDescriptorMode;
        private IEnumRegisterField<FRC_FrameDescriptorMode> FRC_txFrameDescriptorMode;
        private IValueRegisterField FRC_rateSelect;
        private IValueRegisterField FRC_frameLength;
        private IFlagRegisterField FRC_ptiEmitRx;
        private IFlagRegisterField FRC_seqRxOverflowInterruptEnable;
        private IFlagRegisterField FRC_ptiEmitRssi;
        private IFlagRegisterField FRC_ptiEmitTx;
        private IFlagRegisterField FRC_ptiEmitSyncWord;
        private IFlagRegisterField FRC_ptiEmitAux;
        private IFlagRegisterField FRC_ptiEmitState;

        private IFlagRegisterField RAC_seqStateRxWarmInterruptEnable;
        private IFlagRegisterField RAC_seqStateOffInterruptEnable;
        private IFlagRegisterField RAC_seqPrsEventInterruptEnable;
        private IFlagRegisterField RAC_seqDemodRxRequestClearInterruptEnable;
        private IFlagRegisterField RAC_seqStimerCompareEventInterruptEnable;
        private IFlagRegisterField RAC_seqRadioStateChangeInterruptEnable;
        private IFlagRegisterField RAC_seqStateShutDownInterrupt;
        private IFlagRegisterField RAC_seqStateRxSearchInterruptEnable;
        private IFlagRegisterField RAC_seqStateTx2TxInterrupt;
        private IFlagRegisterField RAC_seqStateTxPoweringDownInterrupt;
        private IFlagRegisterField RAC_seqStateTxWarmInterrupt;
        private IFlagRegisterField RAC_seqStateRx2TxInterrupt;
        private IFlagRegisterField RAC_seqStateRxOverflowInterrupt;
        private IFlagRegisterField RAC_seqStateRx2RxInterrupt;
        private IFlagRegisterField RAC_seqStateRxPoweringDownInterrupt;
        private IFlagRegisterField RAC_seqStateTx2RxInterrupt;
        private IFlagRegisterField RAC_seqStateRxFrameInterruptEnable;
        private IFlagRegisterField RAC_seqStateRxPoweringDownInterruptEnable;
        private IFlagRegisterField RAC_seqStateRx2RxInterruptEnable;
        private bool RAC_txEnable;
        private ushort RAC_seqTimerLimit = 0xFFFF;
        private IFlagRegisterField RAC_seqTimerAlwaysRun;
        private IFlagRegisterField RAC_seqTimerCompareRelative;
        private IEnumRegisterField<RAC_SeqTimerCompareInvalidMode> RAC_seqTimerCompareInvalidMode;
        private IEnumRegisterField<RAC_SeqTimerCompareAction> RAC_seqTimerCompareAction;
        private IValueRegisterField RAC_seqTimerCompareValue;
        private IValueRegisterField RAC_seqTimerPrescaler;
        private IFlagRegisterField RAC_seqStateShutDownInterruptEnable;
        private IFlagRegisterField RAC_seqStateTx2TxInterruptEnable;
        private IFlagRegisterField RAC_seqStateTx2RxInterruptEnable;
        private IFlagRegisterField RAC_seqStateTxPoweringDownInterruptEnable;
        private IFlagRegisterField RAC_seqStateTxInterruptEnable;
        private IFlagRegisterField RAC_seqStateTxWarmInterruptEnable;
        private IFlagRegisterField RAC_seqStateRx2TxInterruptEnable;
        private IFlagRegisterField RAC_seqStateRxOverflowInterruptEnable;
        private IFlagRegisterField RAC_seqStateRxFrameInterrupt;
        private IFlagRegisterField RAC_seqStateRxSearchInterrupt;
        private IFlagRegisterField RAC_seqStateTxInterrupt;
        private IFlagRegisterField RAC_seqStateOffInterrupt;
        private IFlagRegisterField RAC_sequencerInSleeping;
        private IFlagRegisterField RAC_txAfterFrameActive;
        private IFlagRegisterField RAC_txAfterFramePending;
        private IFlagRegisterField RAC_forceStateActive;
        private IValueRegisterField RAC_softwareRxEnable;
        private IFlagRegisterField RAC_seqStateRxWarmInterrupt;
        private RAC_RadioState RAC_previous2RadioState = RAC_RadioState.Off;
        private IFlagRegisterField RAC_sequencerInDeepSleep;
        private RAC_RadioState RAC_previous1RadioState = RAC_RadioState.Off;
        private bool RAC_ongoingRxCollided = false;
        private RAC_InternalRxState RAC_internalRxState = RAC_InternalRxState.Idle;
        private RAC_InternalTxState RAC_internalTxState = RAC_InternalTxState.Idle;
        private RAC_RadioState RAC_currentRadioState = RAC_RadioState.Off;
        private IFlagRegisterField RAC_sequencerActive;
        private RAC_RadioState RAC_previous3RadioState = RAC_RadioState.Off;
        private IFlagRegisterField RAC_seqStimerCompareEventInterrupt;
        private IFlagRegisterField RAC_seqPrsEventInterrupt;
        private IFlagRegisterField RAC_seqDemodRxRequestClearInterrupt;
        private IEnumRegisterField<RAC_RadioState> RAC_forceStateTransition;
        // Sequencer Radio State Machine Interrupt Flag
        private IFlagRegisterField RAC_seqRadioStateChangeInterrupt;
        private IFlagRegisterField RAC_stimerCompareEventInterruptEnable;
        private IFlagRegisterField RAC_radioStateChangeInterruptEnable;
        private IFlagRegisterField RAC_stimerCompareEventInterrupt;
        private IFlagRegisterField RAC_radioStateChangeInterrupt;
        private IValueRegisterField RAC_mainCoreSeqInterruptsEnable;
        private bool RAC_paOutputLevelRamping = false;
        private IFlagRegisterField RAC_paRampingDone;
        private bool RAC_em1pAckPending;
        private IFlagRegisterField RAC_txAfterRx;
        private IFlagRegisterField RAC_exitShutdownDisable;
        private IFlagRegisterField RAC_forceDisable;
        private IFlagRegisterField RAC_prsForceTx;
        private IValueRegisterField RAC_mainCoreSeqInterrupts;
        private IValueRegisterField RAC_softwareIrq;

        private IValueRegisterField SYNTH_frequency;
        private IEnumRegisterField<SYNTH_LoDivFreqCtrl> SYNTH_loDivFreqCtrl;
        private IValueRegisterField SYNTH_channelSpacing;

        private byte[] currentFrame;
        private uint currentFrameOffset = 0;
        private double currentFrameByteOverTheAirTimeUs = 0;
        private double currentFrameElapsedAirtimeUs = 0;
        private uint currentFrameCodedSymbolsPerInfoBit = 0;
        private int currentDisassembleSubFrame = 0;
        private uint currentDisassembleSubFrameStartWord = 0;
        // Payload bytes of the current RX subframe already delivered. Tracked separately from WCNT because a
        // subframe excluded from the word counter would otherwise never appear to make progress.
        private uint currentDisassembleSubFrameBytes = 0;
        private ISiLabs_CMU cmu;

        private readonly IFlagRegisterField[] RFMAILBOX_messageInterruptEnable = new IFlagRegisterField[MailboxMessageNumber];
        private readonly IFlagRegisterField[] RFMAILBOX_messageInterrupt = new IFlagRegisterField[MailboxMessageNumber];
        private readonly IValueRegisterField[] RFMAILBOX_messagePointer = new IValueRegisterField[MailboxMessageNumber];
        private readonly IFlagRegisterField[] HOSTMAILBOX_messageInterruptEnable = new IFlagRegisterField[MailboxMessageNumber];
        private readonly IFlagRegisterField[] HOSTMAILBOX_messageInterrupt = new IFlagRegisterField[MailboxMessageNumber];
        private readonly IValueRegisterField[] HOSTMAILBOX_messagePointer = new IValueRegisterField[MailboxMessageNumber];
        private readonly IValueRegisterField[] RAC_seqStorage = new IValueRegisterField[RAC_NumberOfSequencerStorageRegisters];
        private readonly IValueRegisterField[] RAC_scratch = new IValueRegisterField[RAC_NumberOfScratchRegisters];
        private readonly byte[] FRC_packetBufferCapture;
        private readonly FRC_FrameDescriptor[] FRC_frameDescriptor;
        private readonly IFlagRegisterField[] FRC_wordCounterCompareInterrupt = new IFlagRegisterField[FRC_NumberOfWordCounterCompareChannels];
        private readonly IFlagRegisterField[] FRC_wordCounterCompareInterruptEnable = new IFlagRegisterField[FRC_NumberOfWordCounterCompareChannels];
        private readonly IFlagRegisterField[] FRC_seqWordCounterCompareInterrupt = new IFlagRegisterField[FRC_NumberOfWordCounterCompareChannels];
        private readonly IFlagRegisterField[] FRC_seqWordCounterCompareInterruptEnable = new IFlagRegisterField[FRC_NumberOfWordCounterCompareChannels];
        private readonly PROTIMER_CaptureCompareChannel[] PROTIMER_captureCompareChannel;
        private readonly PROTIMER_TimeoutCounter[] PROTIMER_timeoutCounter;
        private readonly Machine machine;
        private readonly CortexM sequencer;
        private static readonly PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private readonly SiLabs_BUFC_1 bufferController;
        private readonly LimitTimer seqTimer;
        private readonly LimitTimer proTimer;
        private readonly LimitTimer paRampingTimer;
        private readonly LimitTimer rssiUpdateTimer;
        private readonly LimitTimer txTimer;
        private readonly LimitTimer rxTimer;
        private readonly DoubleWordRegisterCollection automaticGainControlRegistersCollection;
        private readonly DoubleWordRegisterCollection cyclicRedundancyCheckRegistersCollection;
        private readonly DoubleWordRegisterCollection frameControllerRegistersCollection;
        private readonly DoubleWordRegisterCollection modulatorAndDemodulatorRegistersCollection;
        private readonly DoubleWordRegisterCollection protocolTimerRegistersCollection;
        private readonly DoubleWordRegisterCollection radioControllerRegistersCollection;
        private readonly DoubleWordRegisterCollection radioMailboxRegistersCollection;
        private readonly DoubleWordRegisterCollection hostMailboxRegistersCollection;
        private readonly DoubleWordRegisterCollection synthesizerRegistersCollection;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const uint SequencerMemoryBaseAddress = 0xB0000000;
        private const uint MailboxMessageNumber = 4;
        private const ulong MicrosecondFrequency = 1000000UL;
        private const ulong HalfMicrosecondFrequency = 2000000UL;
        private const int SNIFF_SYNCWORD_SERIAL_LEN = 4;
        private const uint FRC_NumberOfFrameDescriptors = 4;
        // FCD0/FCD1 form the transmit descriptor pair, FCD2/FCD3 the receive pair.
        private const int FRC_FirstTransmitFrameDescriptor = 0;
        private const int FRC_FirstReceiveFrameDescriptor = 2;
        // Only the compare channels backed by a register in this model: WCNTCMP0/1/2.
        private const int FRC_NumberOfWordCounterCompareChannels = 3;
        private const uint FRC_PacketBufferCaptureSize = 48;
        private const uint FRC_RxFragmentLengthInBytes = 4;
        // FRC.CTRL.RATESELECT value that selects the alternate convolutional coding (CODINGB => BLE S=2 / 500 kbps).
        private const uint FRC_RateSelectCodingB = 2u;
        // RENODE-53
        // TODO: calculate the ramping time from registers
        private const uint RAC_PowerAmplifierRampingTimeUs = 5;
        private const uint RAC_NumberOfSequencerStorageRegisters = 4;
        private const uint RAC_NumberOfScratchRegisters = 8;
        private const uint PROTIMER_DefaultLightWeightTimerLimit = 0xFFFFFFFF;
        private const uint PROTIMER_MinimumTimeoutCounterDelay = 2;
        private const uint PROTIMER_NumberOfTimeoutCounters = 2;
        private const uint PROTIMER_NumberOfCaptureCompareChannels = 8;
        private const uint PROTIMER_NumberOfCaptureCompareChannelPrsEvents = 5;
        // LE Coded PHY preamble is a fixed 80us (sent at the 1 Msym/s base rate, independent of S=2/S=8).
        private const double MODEM_BleCodedPhyPreambleOverTheAirTimeUs = 80.0;
        private const uint MODEM_BlePhyRxChainDelayNanoS = 50000;
        private const uint MODEM_BlePhyRxDoneDelayNanoS = 11250;
        private const uint MODEM_BlePhyTxChainDelayNanoS = 750;
        // TODO: verify this
        private const uint MODEM_BlePhyTxDoneChainDelayNanoS = 750;
        private const uint MODEM_802154PhyRxChainDelayNanoS = 6625;
        private const uint MODEM_802154PhyRxDoneDelayNanoS = 6625;
        private const uint MODEM_802154PhyTxChainDelayNanoS = 600;
        // TODO: verify this
        private const uint MODEM_802154PhyTxDoneChainDelayNanoS = 0;

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
            InternalTrigger = 28,
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

        private class PROTIMER_CaptureCompareChannel
        {
            public PROTIMER_CaptureCompareChannel(SiLabs_xG22_LPW parent, uint index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void Capture(ushort baseVal, uint wrapVal)
            {
                if(CaptureValid.Value)
                {
                    OverflowInterrupt.Value = true;
                    SeqOverflowInterrupt.Value = true;
                }

                CaptureValid.Value = true;
                InterruptField.Value = true;
                SeqInterruptField.Value = true;

                PreValue.Value = 0;
                BaseValue.Value = baseVal;
                WrapValue.Value = wrapVal;

                parent.UpdateInterrupts();
            }

            public bool Interrupt => ((InterruptField.Value && InterruptEnable.Value)
                                      || (OverflowInterrupt.Value && OverflowInterruptEnable.Value));

            public bool SeqInterrupt => ((SeqInterruptField.Value && SeqInterruptEnable.Value)
                                         || (SeqOverflowInterrupt.Value && SeqOverflowInterruptEnable.Value));

            public IFlagRegisterField InterruptField;
            public IValueRegisterField BaseValue;
            public IValueRegisterField PreValue;
            public IEnumRegisterField<PROTIMER_CaptureInputSource> CaptureInputSource;
            public IFlagRegisterField WrapMatchEnable;
            public IFlagRegisterField BaseMatchEnable;
            public IFlagRegisterField PreMatchEnable;
            public IEnumRegisterField<PROTIMER_CaptureCompareMode> Mode;
            public IFlagRegisterField Enable;
            public IFlagRegisterField SeqOverflowInterruptEnable;
            public IFlagRegisterField SeqOverflowInterrupt;
            public IFlagRegisterField OverflowInterruptEnable;
            public IFlagRegisterField OverflowInterrupt;
            public IFlagRegisterField SeqInterruptEnable;
            public IFlagRegisterField SeqInterruptField;
            public IFlagRegisterField InterruptEnable;
            public IValueRegisterField WrapValue;
            public IFlagRegisterField CaptureValid;

            private readonly SiLabs_xG22_LPW parent;
            private readonly uint index;
        }

        private class PROTIMER_TimeoutCounter
        {
            public PROTIMER_TimeoutCounter(SiLabs_xG22_LPW parent, uint index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void Start()
            {
                if(SyncSource.Value != PROTIMER_TimeoutCounterSource.Disabled)
                {
                    Synchronizing.Value = true;
                    Running.Value = false;
                }
                else
                {
                    Running.Value = true;
                    Synchronizing.Value = false;
                    // Hardware underflows after exactly CounterTop precounter cycles. The Update()
                    // logic below requires an extra full precounter cycle to detect Counter==0, so we
                    // pre-decrement the loaded count by one to remove that spurious cycle while keeping
                    // the match-at-(0,0) event firing one precounter cycle before the underflow.
                    Counter.Value = CounterTop.Value > 0 ? CounterTop.Value - 1 : 0;
                    PreCounter.Value = PreCounterTop.Value;
                }
                parent.PROTIMER_HandleChangedParams();
            }

            public void Stop()
            {
                Running.Value = false;
                Synchronizing.Value = false;
                Finished?.Invoke();
                parent.PROTIMER_HandleChangedParams();
            }

            public void Update(PROTIMER_TimeoutCounterSource evt, uint evtCount = 1)
            {
                // TODO: handle evtCount > PROTIMER_MinimumTimeoutCounterDelay
                if((Running.Value || Synchronizing.Value) && evtCount > PROTIMER_MinimumTimeoutCounterDelay)
                {
                    parent.Log(LogLevel.Error, "TOUT{0} Update() passed an evtCount > PROTIMER_MinimumTimeoutCounterDelay ({1})", index, evtCount);
                }

                while(evtCount > 0)
                {
                    if(Running.Value && Source.Value == evt)
                    {
                        if(PreCounter.Value == 0)
                        {
                            PreCounter.Value = PreCounterTop.Value;

                            if(Counter.Value == 0)
                            {
                                UnderflowInterrupt.Value = true;
                                SeqUnderflowInterrupt.Value = true;

                                if(Mode.Value == PROTIMER_RepeatMode.OneShot)
                                {
                                    Running.Value = false;
                                    Finished?.Invoke();
                                }
                                else
                                {
                                    // See note in Start(): pre-decrement to remove the spurious extra precounter cycle.
                                    Counter.Value = CounterTop.Value > 0 ? CounterTop.Value - 1 : 0;
                                }
                                parent.PROTIMER_TriggerEvent((PROTIMER_Event)((uint)PROTIMER_Event.TimeoutCounter0Underflow + this.index));
                                Underflowed?.Invoke();
                            }
                            else
                            {
                                Counter.Value -= 1;
                            }
                        }
                        else
                        {
                            PreCounter.Value -= 1;
                        }

                        var match = (Counter.Value == CounterCompare.Value && PreCounter.Value == PreCounterCompare.Value);

                        if(match)
                        {
                            MatchInterrupt.Value |= match;
                            SeqMatchInterrupt.Value |= match;
                            parent.UpdateInterrupts();
                            parent.PROTIMER_TriggerEvent((PROTIMER_Event)((uint)PROTIMER_Event.TimeoutCounter0Match + this.index));
                        }
                    }

                    if(Synchronizing.Value && SyncSource.Value == evt)
                    {
                        Synchronizing.Value = false;
                        Running.Value = true;
                        // See note in Start(): pre-decrement to remove the spurious extra precounter cycle.
                        Counter.Value = CounterTop.Value > 0 ? CounterTop.Value - 1 : 0;
                        PreCounter.Value = PreCounterTop.Value;
                        Synchronized?.Invoke();
                    }

                    evtCount--;
                }
            }

            public bool Interrupt => (UnderflowInterrupt.Value && UnderflowInterruptEnable.Value)
                                      || (MatchInterrupt.Value && MatchInterruptEnable.Value);

            public bool SeqInterrupt => (SeqUnderflowInterrupt.Value && SeqUnderflowInterruptEnable.Value)
                                         || (SeqMatchInterrupt.Value && SeqMatchInterruptEnable.Value);

            public event Action Synchronized;

            public event Action Underflowed;

            public event Action Finished;

            public IEnumRegisterField<PROTIMER_TimeoutCounterSource> Source;
            public IValueRegisterField PreCounterTop;
            public IValueRegisterField CounterTop;
            public IValueRegisterField PreCounterCompare;
            public IValueRegisterField PreCounter;
            public IValueRegisterField CounterCompare;
            public IValueRegisterField Counter;
            public IFlagRegisterField SeqMatchInterruptEnable;
            public IFlagRegisterField SeqUnderflowInterrupt;
            public IFlagRegisterField SeqUnderflowInterruptEnable;
            public IEnumRegisterField<PROTIMER_TimeoutCounterSource> SyncSource;
            public IFlagRegisterField MatchInterruptEnable;
            public IFlagRegisterField MatchInterrupt;
            public IFlagRegisterField UnderflowInterruptEnable;
            public IFlagRegisterField UnderflowInterrupt;
            public IFlagRegisterField Running;
            public IFlagRegisterField Synchronizing;
            public IFlagRegisterField SeqMatchInterrupt;
            public IEnumRegisterField<PROTIMER_RepeatMode> Mode;

            private readonly uint index;
            private readonly SiLabs_xG22_LPW parent;
        }

        private class FRC_FrameDescriptor
        {
            // Hardware semantics: WORDS is the number of words consumed by the subframe minus one
            // (an 8-bit field on this part). The FRC sequencer clamps each subframe to (WORDS+1) and
            // keeps iterating until WCNT reaches FRAMELENGTH, so the unlimited setting used by RAIL on
            // the data subframe degenerates into "remaining frame words" via the Math.Min() in the
            // assemble/disassemble loops.
            public uint Words => (uint)(WordsField.Value + 1);

            // WORDS=0xFF selects an unlimited subframe: it runs to the end of the frame and the subframe
            // counter is not used.
            public bool UnlimitedWords => WordsField.Value == 0xFF;

            public uint BufferIndex => (uint)BufferField.Value;

            public IValueRegisterField WordsField;
            public IValueRegisterField BufferField;
            // INCLUDECRC inserts (TX) or extracts (RX) the CRC at the end of the subframe, while CALCCRC
            // only selects whether the subframe's words feed the CRC accumulator. CALCCRC has no observable
            // effect while CRC_CalculateCRC() is mocked.
            public IFlagRegisterField IncludeCrc;
            public IFlagRegisterField CalculateCrc;
            public IValueRegisterField CrcSkipWords;
            public IFlagRegisterField SkipWhitening;
            public IFlagRegisterField AddTrailData;
            public IFlagRegisterField ExcludeSubframeFromWcnt;
        }

        private enum FRC_FSMState
        {
            Idle                = 0x00,
            RxInit              = 0x01,
            RxData              = 0x02,
            RxCrc               = 0x03,
            RxFcdUpdate         = 0x04,
            RxDiscard           = 0x05,
            RxTrail             = 0x06,
            RxDone              = 0x07,
            RxPauseInit         = 0x08,
            RxPaused            = 0x09,
            Undefined1          = 0x0A,
            Undefined2          = 0x0B,
            RxCrcZeroCheck      = 0x0C,
            RxSup               = 0x0D,
            RxWaitEof           = 0x0E,
            Undefined3          = 0x0F,
            TxInit              = 0x10,
            TxData              = 0x11,
            TxCrc               = 0x12,
            TxFdcUpdate         = 0x13,
            TxTrail             = 0x14,
            TxFlush             = 0x15,
            TxDone              = 0x16,
            TxDoneWait          = 0x17,
            TxRaw               = 0x18,
            TxPauseFlush        = 0x19,
        }

        private enum FRC_RxRawDataMode
        {
            Disable = 0x0,
            SingleItem = 0x1,
            SingleBuffer = 0x2,
            SingleBufferFrame = 0x3,
            RepeatBuffer = 0x4,
        }

        private enum FRC_RxRawDataTriggerMode
        {
            Immediate = 0x0,
            PRS = 0x1,
            InternalSignal = 0x2,
        }

        private enum FRC_DynamicFrameLengthMode
        {
            Disable = 0x0,
            SingleByte = 0x1,
            SingleByteMSB = 0x2,
            DualByteLSBFirst = 0x3,
            DualByteMSBFirst = 0x4,
            Infinite = 0x5,
            BlockError = 0x6,
        }

        private enum FRC_DynamicFrameLengthBitOrder
        {
            Normal = 0x0,
            Reverse = 0x1,
        }

        private enum FRC_FrameDescriptorMode
        {
            FrameDescriptorMode0 = 0x0,
            FrameDescriptorMode1 = 0x1,
            FrameDescriptorMode2 = 0x2,
            FrameDescriptorMode3 = 0x3,
        }

        private enum RAC_RadioState
        {
            Off                 = 0,
            RxWarm              = 1,
            RxSearch            = 2,
            RxFrame             = 3,
            RxPoweringDown      = 4,
            Rx2Rx               = 5,
            RxOverflow          = 6,
            Rx2Tx               = 7,
            TxWarm              = 8,
            Tx                  = 9,
            TxPoweringDown      = 10,
            Tx2Rx               = 11,
            Tx2Tx               = 12,
            Shutdown            = 13,
            PowerOnReset        = 14,
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
            TxWarmIrqCleared        = 17,
            RxWarmIrqCleared        = 18,
            RxPowerDownIrqCleared   = 19,
            RxOverflowIrqCleared    = 20,
            Rx2RxIrqCleared         = 21,
            Rx2TxIrqCleared         = 22,
            Tx2RxIrqCleared         = 23,
            Tx2TxIrqCleared         = 24,
            TxIrqCleared            = 25,
            TxPowerDownIrqCleared   = 26,
            RxSearchIrqCleared      = 27,
            RxFrameIrqCleared       = 28,
            ShutdownIrqCleared      = 29,
            ClearRxOverflow         = 30,
            RxAbort                 = 31,
        }

        private enum RAC_RxEnableSource
        {
            Software0           = 0x0001,
            Software1           = 0x0002,
            Software2           = 0x0004,
            Software3           = 0x0008,
            Software4           = 0x0010,
            Software5           = 0x0020,
            Software6           = 0x0040,
            Software7           = 0x0080,
            ChannelBusy         = 0x0100,
            TimingDetected      = 0x0200,
            PreambleDetected    = 0x0400,
            FrameDetected       = 0x0800,
            DemodRxRequest      = 0x1000,
            PRS                 = 0x2000,
        }

        private enum RAC_SeqTimerCompareAction
        {
            Wrap                = 0x0,
            Continue            = 0x1,
        }

        // Determines when the STIMERCOMP value is invalid.
        private enum RAC_SeqTimerCompareInvalidMode
        {
            Never                       = 0x0,
            StateChange                 = 0x1,
            CompareEvent                = 0x2,
            StateChangeAndCompareEvent  = 0x3,
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

        private enum PROTIMER_PreCounterSource
        {
            None    = 0x0,
            Clock   = 0x1,
            Unused0 = 0x2,
            Unused1 = 0x3,
        }

        private enum PROTIMER_BaseCounterSource
        {
            None                = 0x0,
            PreCounterOverflow  = 0x1,
            Unused0             = 0x2,
            Unused1             = 0x3,
        }

        private enum PROTIMER_WrapCounterSource
        {
            None                = 0x0,
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

        private enum PROTIMER_CaptureCompareMode
        {
            Compare = 0x0,
            Capture = 0x1,
        }

        private enum PROTIMER_CaptureInputSource
        {
            PRS                             = 0x0,
            TxDone                          = 0x1,
            RxDone                          = 0x2,
            TxOrRxDone                      = 0x3,
            DemodulatorFoundSyncWord0       = 0x4,
            DemodulatorFoundSyncWord1       = 0x5,
            DemodulatorFoundSyncWord0or1    = 0x6,
            ModulatorSyncWordSent           = 0x7,
            RxAtEndOfFrameFromDemodulator   = 0x8,
            ProRtcCaptureCompare0           = 0x9,
            ProRtcCaptureCompare1           = 0xA,
        }

        private enum PROTIMER_CaptureInputEdge
        {
            Rising      = 0x0,
            Falling     = 0x1,
            Both        = 0x2,
            Disabled    = 0x3,
        }

        private enum PROTIMER_OutputAction
        {
            Disabled    = 0x0,
            Toggle      = 0x1,
            Clear       = 0x2,
            Set         = 0x3,
        }

        private enum PROTIMER_OverflowOutputActionCounter
        {
            PreCounter  = 0x0,
            BaseCounter = 0x1,
            WrapCounrer = 0x2,
            Disabled    = 0x3,
        }

        private enum PROTIMER_ListenBeforeTalkState
        {
            Idle        = 0,
            Backoff     = 1,
            CcaDelay    = 2,
        }

        private enum PROTIMER_TxRxRequestState
        {
            Idle,
            SetEvent1,
            Set,
            ClearEvent1,
        }

        private enum PROTIMER_PreCountOverflowSourced : uint
        {
            BaseCounter            = 0x0001,
            WrapCounter            = 0x0002,
            TimeoutCounter0        = 0x0004,
            TimeoutCounter1        = 0x0008,
            CaptureCompareChannel0 = 0x0010,
            CaptureCompareChannel1 = 0x0020,
            CaptureCompareChannel2 = 0x0040,
            CaptureCompareChannel3 = 0x0080,
            CaptureCompareChannel4 = 0x0100,
            CaptureCompareChannel5 = 0x0200,
            CaptureCompareChannel6 = 0x0400,
            CaptureCompareChannel7 = 0x0800,
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
        }

        private enum MODEM_SymbolCoding
        {
            Nrz        = 0,
            Manchester = 1,
            Dsss       = 2,
            Linecode   = 3,
        }

        private enum MODEM_RateSelectMode
        {
            NOCHANGE = 0,
            PAYLOAD = 1,
            FRC = 2,
            SYNC = 3,
        }

        private enum CRC_CrcWidthMode
        {
            Width8 = 0x0,
            Width16 = 0x1,
            Width24 = 0x2,
            Width32 = 0x3,
        }

        private enum AGC_RssiState
        {
            Idle          = 0x0,
            Condition     = 0x1,
            Period        = 0x2,
            Command       = 0x3,
            FameDetection = 0x4,
        }

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
            PacketCaptureBufferControl                                      = 0x00CC,
            PacketCaptureBufferStatus                                       = 0x00D0,
            PacketCaptureDataBuffer0                                        = 0x00D4,
            PacketCaptureDataBuffer1                                        = 0x00D8,
            PacketCaptureDataBuffer2                                        = 0x00DC,
            PacketCaptureDataBuffer3                                        = 0x00E0,
            PacketCaptureDataBuffer4                                        = 0x00E4,
            PacketCaptureDataBuffer5                                        = 0x00E8,
            PacketCaptureDataBuffer6                                        = 0x00EC,
            PacketCaptureDataBuffer7                                        = 0x00F0,
            PacketCaptureDataBuffer8                                        = 0x00F4,
            PacketCaptureDataBuffer9                                        = 0x00F8,
            PacketCaptureDataBuffer10                                       = 0x00FC,
            PacketCaptureDataBuffer11                                       = 0x0100,
            FrameControlDescriptor0                                         = 0x0104,
            FrameControlDescriptor1                                         = 0x0108,
            FrameControlDescriptor2                                         = 0x010C,
            FrameControlDescriptor3                                         = 0x0110,
            InterleaverElementValue0                                        = 0x0120,
            InterleaverElementValue1                                        = 0x0124,
            InterleaverElementValue2                                        = 0x0128,
            InterleaverElementValue3                                        = 0x012C,
            InterleaverElementValue4                                        = 0x0130,
            InterleaverElementValue5                                        = 0x0134,
            InterleaverElementValue6                                        = 0x0138,
            InterleaverElementValue7                                        = 0x013C,
            InterleaverElementValue8                                        = 0x0140,
            InterleaverElementValue9                                        = 0x0144,
            InterleaverElementValue10                                       = 0x0148,
            InterleaverElementValue11                                       = 0x014C,
            InterleaverElementValue12                                       = 0x0150,
            InterleaverElementValue13                                       = 0x0154,
            InterleaverElementValue14                                       = 0x0158,
            InterleaverElementValue15                                       = 0x015C,

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
            PacketCaptureBufferControl_Set                                  = 0x10CC,
            PacketCaptureBufferStatus_Set                                   = 0x10D0,
            PacketCaptureDataBuffer0_Set                                    = 0x10D4,
            PacketCaptureDataBuffer1_Set                                    = 0x10D8,
            PacketCaptureDataBuffer2_Set                                    = 0x10DC,
            PacketCaptureDataBuffer3_Set                                    = 0x10E0,
            PacketCaptureDataBuffer4_Set                                    = 0x10E4,
            PacketCaptureDataBuffer5_Set                                    = 0x10E8,
            PacketCaptureDataBuffer6_Set                                    = 0x10EC,
            PacketCaptureDataBuffer7_Set                                    = 0x10F0,
            PacketCaptureDataBuffer8_Set                                    = 0x10F4,
            PacketCaptureDataBuffer9_Set                                    = 0x10F8,
            PacketCaptureDataBuffer10_Set                                   = 0x10FC,
            PacketCaptureDataBuffer11_Set                                   = 0x1100,
            FrameControlDescriptor0_Set                                     = 0x1104,
            FrameControlDescriptor1_Set                                     = 0x1108,
            FrameControlDescriptor2_Set                                     = 0x110C,
            FrameControlDescriptor3_Set                                     = 0x1110,
            InterleaverElementValue0_Set                                    = 0x1120,
            InterleaverElementValue1_Set                                    = 0x1124,
            InterleaverElementValue2_Set                                    = 0x1128,
            InterleaverElementValue3_Set                                    = 0x112C,
            InterleaverElementValue4_Set                                    = 0x1130,
            InterleaverElementValue5_Set                                    = 0x1134,
            InterleaverElementValue6_Set                                    = 0x1138,
            InterleaverElementValue7_Set                                    = 0x113C,
            InterleaverElementValue8_Set                                    = 0x1140,
            InterleaverElementValue9_Set                                    = 0x1144,
            InterleaverElementValue10_Set                                   = 0x1148,
            InterleaverElementValue11_Set                                   = 0x114C,
            InterleaverElementValue12_Set                                   = 0x1150,
            InterleaverElementValue13_Set                                   = 0x1154,
            InterleaverElementValue14_Set                                   = 0x1158,
            InterleaverElementValue15_Set                                   = 0x115C,

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
            PacketCaptureBufferControl_Clr                                  = 0x20CC,
            PacketCaptureBufferStatus_Clr                                   = 0x20D0,
            PacketCaptureDataBuffer0_Clr                                    = 0x20D4,
            PacketCaptureDataBuffer1_Clr                                    = 0x20D8,
            PacketCaptureDataBuffer2_Clr                                    = 0x20DC,
            PacketCaptureDataBuffer3_Clr                                    = 0x20E0,
            PacketCaptureDataBuffer4_Clr                                    = 0x20E4,
            PacketCaptureDataBuffer5_Clr                                    = 0x20E8,
            PacketCaptureDataBuffer6_Clr                                    = 0x20EC,
            PacketCaptureDataBuffer7_Clr                                    = 0x20F0,
            PacketCaptureDataBuffer8_Clr                                    = 0x20F4,
            PacketCaptureDataBuffer9_Clr                                    = 0x20F8,
            PacketCaptureDataBuffer10_Clr                                   = 0x20FC,
            PacketCaptureDataBuffer11_Clr                                   = 0x2100,
            FrameControlDescriptor0_Clr                                     = 0x2104,
            FrameControlDescriptor1_Clr                                     = 0x2108,
            FrameControlDescriptor2_Clr                                     = 0x210C,
            FrameControlDescriptor3_Clr                                     = 0x2110,
            InterleaverElementValue0_Clr                                    = 0x2120,
            InterleaverElementValue1_Clr                                    = 0x2124,
            InterleaverElementValue2_Clr                                    = 0x2128,
            InterleaverElementValue3_Clr                                    = 0x212C,
            InterleaverElementValue4_Clr                                    = 0x2130,
            InterleaverElementValue5_Clr                                    = 0x2134,
            InterleaverElementValue6_Clr                                    = 0x2138,
            InterleaverElementValue7_Clr                                    = 0x213C,
            InterleaverElementValue8_Clr                                    = 0x2140,
            InterleaverElementValue9_Clr                                    = 0x2144,
            InterleaverElementValue10_Clr                                   = 0x2148,
            InterleaverElementValue11_Clr                                   = 0x214C,
            InterleaverElementValue12_Clr                                   = 0x2150,
            InterleaverElementValue13_Clr                                   = 0x2154,
            InterleaverElementValue14_Clr                                   = 0x2158,
            InterleaverElementValue15_Clr                                   = 0x215C,

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
            PacketCaptureBufferControl_Tgl                                  = 0x30CC,
            PacketCaptureBufferStatus_Tgl                                   = 0x30D0,
            PacketCaptureDataBuffer0_Tgl                                    = 0x30D4,
            PacketCaptureDataBuffer1_Tgl                                    = 0x30D8,
            PacketCaptureDataBuffer2_Tgl                                    = 0x30DC,
            PacketCaptureDataBuffer3_Tgl                                    = 0x30E0,
            PacketCaptureDataBuffer4_Tgl                                    = 0x30E4,
            PacketCaptureDataBuffer5_Tgl                                    = 0x30E8,
            PacketCaptureDataBuffer6_Tgl                                    = 0x30EC,
            PacketCaptureDataBuffer7_Tgl                                    = 0x30F0,
            PacketCaptureDataBuffer8_Tgl                                    = 0x30F4,
            PacketCaptureDataBuffer9_Tgl                                    = 0x30F8,
            PacketCaptureDataBuffer10_Tgl                                   = 0x30FC,
            PacketCaptureDataBuffer11_Tgl                                   = 0x3100,
            FrameControlDescriptor0_Tgl                                     = 0x3104,
            FrameControlDescriptor1_Tgl                                     = 0x3108,
            FrameControlDescriptor2_Tgl                                     = 0x310C,
            FrameControlDescriptor3_Tgl                                     = 0x3110,
            InterleaverElementValue0_Tgl                                    = 0x3120,
            InterleaverElementValue1_Tgl                                    = 0x3124,
            InterleaverElementValue2_Tgl                                    = 0x3128,
            InterleaverElementValue3_Tgl                                    = 0x312C,
            InterleaverElementValue4_Tgl                                    = 0x3130,
            InterleaverElementValue5_Tgl                                    = 0x3134,
            InterleaverElementValue6_Tgl                                    = 0x3138,
            InterleaverElementValue7_Tgl                                    = 0x313C,
            InterleaverElementValue8_Tgl                                    = 0x3140,
            InterleaverElementValue9_Tgl                                    = 0x3144,
            InterleaverElementValue10_Tgl                                   = 0x3148,
            InterleaverElementValue11_Tgl                                   = 0x314C,
            InterleaverElementValue12_Tgl                                   = 0x3150,
            InterleaverElementValue13_Tgl                                   = 0x3154,
            InterleaverElementValue14_Tgl                                   = 0x3158,
            InterleaverElementValue15_Tgl                                   = 0x315C,
        }

        private enum ModulatorAndDemodulatorRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            Status                                                          = 0x0008,
            TimingDetectionStatus                                           = 0x000C,
            FrequencyOffsetEstimate                                         = 0x0010,
            AutomaticFrequencyControlAdjustmentRx                           = 0x0014,
            AutomaticFrequencyControldAjustmentTx                           = 0x0018,
            AnalogMixerControl                                              = 0x001C,
            Control0                                                        = 0x0020,
            Control1                                                        = 0x0024,
            Control2                                                        = 0x0028,
            Control3                                                        = 0x002C,
            Control4                                                        = 0x0030,
            Control5                                                        = 0x0034,
            Control6                                                        = 0x0038,
            TxBaudrate                                                      = 0x0058,
            RxBaudrate                                                      = 0x005C,
            ChannelFilter                                                   = 0x0060,
            Preamble                                                        = 0x0064,
            SyncWord0                                                       = 0x0068,
            SyncWord1                                                       = 0x006C,
            Timing                                                          = 0x0080,
            DirectSequenceSpreadSpectrumSymbol0                             = 0x0084,
            ModulationIndex                                                 = 0x0088,
            AutomaticFrequencyControl                                       = 0x008C,
            AutomaticFrequencyControlAdjustmentLimit                        = 0x0090,
            ShapingCoefficients0                                            = 0x0094,
            ShapingCoefficients1                                            = 0x0098,
            ShapingCoefficients2                                            = 0x009C,
            ShapingCoefficients3                                            = 0x00A0,
            ShapingCoefficients4                                            = 0x00A4,
            ShapingCoefficients5                                            = 0x00A8,
            ShapingCoefficients6                                            = 0x00AC,
            ShapingCoefficients7                                            = 0x00B0,
            ShapingCoefficients8                                            = 0x00B4,
            ShapingCoefficients9                                            = 0x00B8,
            ShapingCoefficients10                                           = 0x00BC,
            ShapingCoefficients11                                           = 0x00C0,
            RampingControl                                                  = 0x00C4,
            RampingLevels                                                   = 0x00CC,
            DirectCurrentOffsetCompensationFilterSettings                   = 0x00E0,
            DirectCurrentOffsetCompensationFilterInitialization             = 0x00E4,
            DirectCurrentOffsetEstimatedValue                               = 0x00E8,
            SampleRateConverterRatioValuesAndChannelFilter                  = 0x00EC,
            InternalAutomaticFrequencyControl                               = 0x00F0,
            DetectionOfSignalArrivalThreshold0                              = 0x00F4,
            DetectionOfSignalArrivalThreshold1                              = 0x00F8,
            DetectionOfSignalArrivalMode                                    = 0x00FC,
            ViterbiDemodulator                                              = 0x0100,
            ViterbiDemodulatorCorrelationConfiguration0                     = 0x0104,
            DigitalMixerControl                                             = 0x010C,
            ViterbiDemodulatorCorrelationConfiguration1                     = 0x0110,
            ViterbiDemodulatorTrackingLoop                                  = 0x0114,
            BaudrateEstimate                                                = 0x0118,
            AutomaticClockGating                                            = 0x0124,
            AutomaticClockGatingClockStop                                   = 0x0128,
            PhaseOffsetEstimate                                             = 0x012C,
            DetectionOfSignalArrivalThreshold2                              = 0x0130,
            DirectModeControl                                               = 0x0134,
            BleLongRange                                                    = 0x0138,
            BleLongRangeSet1                                                = 0x013C,
            BleLongRangeSet2                                                = 0x0140,
            BleLongRangeSet3                                                = 0x0144,
            BleLongRangeSet4                                                = 0x0148,
            BleLongRangeSet5                                                = 0x014C,
            BleLongRangeSet6                                                = 0x0150,
            BleLongRangeFrameControllerInterface                            = 0x0154,
            CoherentDemodulatorSignals0                                     = 0x0158,
            CoherentDemodulatorSignals1                                     = 0x014C,
            CoherentDemodulatorSignals2                                     = 0x0160,
            CoherentDemodulatorSignals3                                     = 0x0164,
            DetectionOfSignalArrivalThreshold3                              = 0x0168,
            DetectionOfSignalArrivalThreshold4                              = 0x016C,
            ViterbiBleTimingStampControl                                    = 0x0170,
            InterruptFlags                                                  = 0x0208,
            InterruptEnable                                                 = 0x020C,
            Command                                                         = 0x0218,
            DemodulatorFSMStatus                                            = 0x021C,
            Status2                                                         = 0x0220,
            Status3                                                         = 0x0224,
            IrCalibrationControl                                            = 0x0228,
            IrCalCoefficientValues                                          = 0x022C,
            BleIqDetectionOfSignalArrival                                   = 0x0230,
            BleIqDetectionOfSignalArrivalExtension1                         = 0x0234,
            SyncWordProperties                                              = 0x0238,
            DigitalGainControl                                              = 0x023C,
            PeripheralReflexSystemControl                                   = 0x0240,
            PowerAmplifierDebug                                             = 0x0244,
            RealTimeCostFunctionEngineControl                               = 0x0248,
            SequencerInterruptFlags                                         = 0x024C,
            SequencerInterruptEnable                                        = 0x0250,
            EarlyTimeStampControl                                           = 0x0254,
            AntennaSwitchControl                                            = 0x0258,
            AntennaSwitchStart                                              = 0x025C,
            AntennaSwitchEnd                                                = 0x0260,
            TrecsPreamblePattern                                            = 0x0264,
            TrecsPreambleDetectionControl                                   = 0x0268,
            ConfigureAntennaPattern                                         = 0x026C,
            EarlyTimeStampTiming                                            = 0x0270,
            AntennaSwitchControl1                                           = 0x0274,
            ConcurrentMode                                                  = 0x0278,
            AntennaDiversityModeControl                                     = 0x027C,
            BleIqDetectionOfSignalArrivalExtension2                         = 0x0280,
            Spare                                                           = 0x0284,
            IrCalCoefficientWrPerAntenna0                                   = 0x0288,
            IrCalCoefficientWrPerAntenna1                                   = 0x028C,

            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            Status_Set                                                      = 0x1008,
            TimingDetectionStatus_Set                                       = 0x100C,
            FrequencyOffsetEstimate_Set                                     = 0x1010,
            AutomaticFrequencyControlAdjustmentRx_Set                       = 0x1014,
            AutomaticFrequencyControldAjustmentTx_Set                       = 0x1018,
            AnalogMixerControl_Set                                          = 0x101C,
            Control0_Set                                                    = 0x1020,
            Control1_Set                                                    = 0x1024,
            Control2_Set                                                    = 0x1028,
            Control3_Set                                                    = 0x102C,
            Control4_Set                                                    = 0x1030,
            Control5_Set                                                    = 0x1034,
            Control6_Set                                                    = 0x1038,
            TxBaudrate_Set                                                  = 0x1058,
            RxBaudrate_Set                                                  = 0x105C,
            ChannelFilter_Set                                               = 0x1060,
            Preamble_Set                                                    = 0x1064,
            SyncWord0_Set                                                   = 0x1068,
            SyncWord1_Set                                                   = 0x106C,
            Timing_Set                                                      = 0x1080,
            DirectSequenceSpreadSpectrumSymbol0_Set                         = 0x1084,
            ModulationIndex_Set                                             = 0x1088,
            AutomaticFrequencyControl_Set                                   = 0x108C,
            AutomaticFrequencyControlAdjustmentLimit_Set                    = 0x1090,
            ShapingCoefficients0_Set                                        = 0x1094,
            ShapingCoefficients1_Set                                        = 0x1098,
            ShapingCoefficients2_Set                                        = 0x109C,
            ShapingCoefficients3_Set                                        = 0x10A0,
            ShapingCoefficients4_Set                                        = 0x10A4,
            ShapingCoefficients5_Set                                        = 0x10A8,
            ShapingCoefficients6_Set                                        = 0x10AC,
            ShapingCoefficients7_Set                                        = 0x10B0,
            ShapingCoefficients8_Set                                        = 0x10B4,
            ShapingCoefficients9_Set                                        = 0x10B8,
            ShapingCoefficients10_Set                                       = 0x10BC,
            ShapingCoefficients11_Set                                       = 0x10C0,
            RampingControl_Set                                              = 0x10C4,
            RampingLevels_Set                                               = 0x10CC,
            DirectCurrentOffsetCompensationFilterSettings_Set               = 0x10E0,
            DirectCurrentOffsetCompensationFilterInitialization_Set         = 0x10E4,
            DirectCurrentOffsetEstimatedValue_Set                           = 0x10E8,
            SampleRateConverterRatioValuesAndChannelFilter_Set              = 0x10EC,
            InternalAutomaticFrequencyControl_Set                           = 0x10F0,
            DetectionOfSignalArrivalThreshold0_Set                          = 0x10F4,
            DetectionOfSignalArrivalThreshold1_Set                          = 0x10F8,
            DetectionOfSignalArrivalMode_Set                                = 0x10FC,
            ViterbiDemodulator_Set                                          = 0x1100,
            ViterbiDemodulatorCorrelationConfiguration0_Set                 = 0x1104,
            DigitalMixerControl_Set                                         = 0x110C,
            ViterbiDemodulatorCorrelationConfiguration1_Set                 = 0x1110,
            ViterbiDemodulatorTrackingLoop_Set                              = 0x1114,
            BaudrateEstimate_Set                                            = 0x1118,
            AutomaticClockGating_Set                                        = 0x1124,
            AutomaticClockGatingClockStop_Set                               = 0x1128,
            PhaseOffsetEstimate_Set                                         = 0x112C,
            DetectionOfSignalArrivalThreshold2_Set                          = 0x1130,
            DirectModeControl_Set                                           = 0x1134,
            BleLongRange_Set                                                = 0x1138,
            BleLongRangeSet1_Set                                            = 0x113C,
            BleLongRangeSet2_Set                                            = 0x1140,
            BleLongRangeSet3_Set                                            = 0x1144,
            BleLongRangeSet4_Set                                            = 0x1148,
            BleLongRangeSet5_Set                                            = 0x114C,
            BleLongRangeSet6_Set                                            = 0x1150,
            BleLongRangeFrameControllerInterface_Set                        = 0x1154,
            CoherentDemodulatorSignals0_Set                                 = 0x1158,
            CoherentDemodulatorSignals1_Set                                 = 0x114C,
            CoherentDemodulatorSignals2_Set                                 = 0x1160,
            CoherentDemodulatorSignals3_Set                                 = 0x1164,
            DetectionOfSignalArrivalThreshold3_Set                          = 0x1168,
            DetectionOfSignalArrivalThreshold4_Set                          = 0x116C,
            ViterbiBleTimingStampControl_Set                                = 0x1170,
            InterruptFlags_Set                                              = 0x1208,
            InterruptEnable_Set                                             = 0x120C,
            Command_Set                                                     = 0x1218,
            DemodulatorFSMStatus_Set                                        = 0x121C,
            Status2_Set                                                     = 0x1220,
            Status3_Set                                                     = 0x1224,
            IrCalibrationControl_Set                                        = 0x1228,
            IrCalCoefficientValues_Set                                      = 0x122C,
            BleIqDetectionOfSignalArrival_Set                               = 0x1230,
            BleIqDetectionOfSignalArrivalExtension1_Set                     = 0x1234,
            SyncWordProperties_Set                                          = 0x1238,
            DigitalGainControl_Set                                          = 0x123C,
            PeripheralReflexSystemControl_Set                               = 0x1240,
            PowerAmplifierDebug_Set                                         = 0x1244,
            RealTimeCostFunctionEngineControl_Set                           = 0x1248,
            SequencerInterruptFlags_Set                                     = 0x124C,
            SequencerInterruptEnable_Set                                    = 0x1250,
            EarlyTimeStampControl_Set                                       = 0x1254,
            AntennaSwitchControl_Set                                        = 0x1258,
            AntennaSwitchStart_Set                                          = 0x125C,
            AntennaSwitchEnd_Set                                            = 0x1260,
            TrecsPreamblePattern_Set                                        = 0x1264,
            TrecsPreambleDetectionControl_Set                               = 0x1268,
            ConfigureAntennaPattern_Set                                     = 0x126C,
            EarlyTimeStampTiming_Set                                        = 0x1270,
            AntennaSwitchControl1_Set                                       = 0x1274,
            ConcurrentMode_Set                                              = 0x1278,
            AntennaDiversityModeControl_Set                                 = 0x127C,
            BleIqDetectionOfSignalArrivalExtension2_Set                     = 0x1280,
            Spare_Set                                                       = 0x1284,
            IrCalCoefficientWrPerAntenna0_Set                               = 0x1288,
            IrCalCoefficientWrPerAntenna1_Set                               = 0x128C,

            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            Status_Clr                                                      = 0x2008,
            TimingDetectionStatus_Clr                                       = 0x200C,
            FrequencyOffsetEstimate_Clr                                     = 0x2010,
            AutomaticFrequencyControlAdjustmentRx_Clr                       = 0x2014,
            AutomaticFrequencyControldAjustmentTx_Clr                       = 0x2018,
            AnalogMixerControl_Clr                                          = 0x201C,
            Control0_Clr                                                    = 0x2020,
            Control1_Clr                                                    = 0x2024,
            Control2_Clr                                                    = 0x2028,
            Control3_Clr                                                    = 0x202C,
            Control4_Clr                                                    = 0x2030,
            Control5_Clr                                                    = 0x2034,
            Control6_Clr                                                    = 0x2038,
            TxBaudrate_Clr                                                  = 0x2058,
            RxBaudrate_Clr                                                  = 0x205C,
            ChannelFilter_Clr                                               = 0x2060,
            Preamble_Clr                                                    = 0x2064,
            SyncWord0_Clr                                                   = 0x2068,
            SyncWord1_Clr                                                   = 0x206C,
            Timing_Clr                                                      = 0x2080,
            DirectSequenceSpreadSpectrumSymbol0_Clr                         = 0x2084,
            ModulationIndex_Clr                                             = 0x2088,
            AutomaticFrequencyControl_Clr                                   = 0x208C,
            AutomaticFrequencyControlAdjustmentLimit_Clr                    = 0x2090,
            ShapingCoefficients0_Clr                                        = 0x2094,
            ShapingCoefficients1_Clr                                        = 0x2098,
            ShapingCoefficients2_Clr                                        = 0x209C,
            ShapingCoefficients3_Clr                                        = 0x20A0,
            ShapingCoefficients4_Clr                                        = 0x20A4,
            ShapingCoefficients5_Clr                                        = 0x20A8,
            ShapingCoefficients6_Clr                                        = 0x20AC,
            ShapingCoefficients7_Clr                                        = 0x20B0,
            ShapingCoefficients8_Clr                                        = 0x20B4,
            ShapingCoefficients9_Clr                                        = 0x20B8,
            ShapingCoefficients10_Clr                                       = 0x20BC,
            ShapingCoefficients11_Clr                                       = 0x20C0,
            RampingControl_Clr                                              = 0x20C4,
            RampingLevels_Clr                                               = 0x20CC,
            DirectCurrentOffsetCompensationFilterSettings_Clr               = 0x20E0,
            DirectCurrentOffsetCompensationFilterInitialization_Clr         = 0x20E4,
            DirectCurrentOffsetEstimatedValue_Clr                           = 0x20E8,
            SampleRateConverterRatioValuesAndChannelFilter_Clr              = 0x20EC,
            InternalAutomaticFrequencyControl_Clr                           = 0x20F0,
            DetectionOfSignalArrivalThreshold0_Clr                          = 0x20F4,
            DetectionOfSignalArrivalThreshold1_Clr                          = 0x20F8,
            DetectionOfSignalArrivalMode_Clr                                = 0x20FC,
            ViterbiDemodulator_Clr                                          = 0x2100,
            ViterbiDemodulatorCorrelationConfiguration0_Clr                 = 0x2104,
            DigitalMixerControl_Clr                                         = 0x210C,
            ViterbiDemodulatorCorrelationConfiguration1_Clr                 = 0x2110,
            ViterbiDemodulatorTrackingLoop_Clr                              = 0x2114,
            BaudrateEstimate_Clr                                            = 0x2118,
            AutomaticClockGating_Clr                                        = 0x2124,
            AutomaticClockGatingClockStop_Clr                               = 0x2128,
            PhaseOffsetEstimate_Clr                                         = 0x212C,
            DetectionOfSignalArrivalThreshold2_Clr                          = 0x2130,
            DirectModeControl_Clr                                           = 0x2134,
            BleLongRange_Clr                                                = 0x2138,
            BleLongRangeSet1_Clr                                            = 0x213C,
            BleLongRangeSet2_Clr                                            = 0x2140,
            BleLongRangeSet3_Clr                                            = 0x2144,
            BleLongRangeSet4_Clr                                            = 0x2148,
            BleLongRangeSet5_Clr                                            = 0x214C,
            BleLongRangeSet6_Clr                                            = 0x2150,
            BleLongRangeFrameControllerInterface_Clr                        = 0x2154,
            CoherentDemodulatorSignals0_Clr                                 = 0x2158,
            CoherentDemodulatorSignals1_Clr                                 = 0x214C,
            CoherentDemodulatorSignals2_Clr                                 = 0x2160,
            CoherentDemodulatorSignals3_Clr                                 = 0x2164,
            DetectionOfSignalArrivalThreshold3_Clr                          = 0x2168,
            DetectionOfSignalArrivalThreshold4_Clr                          = 0x216C,
            ViterbiBleTimingStampControl_Clr                                = 0x2170,
            InterruptFlags_Clr                                              = 0x2208,
            InterruptEnable_Clr                                             = 0x220C,
            Command_Clr                                                     = 0x2218,
            DemodulatorFSMStatus_Clr                                        = 0x221C,
            Status2_Clr                                                     = 0x2220,
            Status3_Clr                                                     = 0x2224,
            IrCalibrationControl_Clr                                        = 0x2228,
            IrCalCoefficientValues_Clr                                      = 0x222C,
            BleIqDetectionOfSignalArrival_Clr                               = 0x2230,
            BleIqDetectionOfSignalArrivalExtension1_Clr                     = 0x2234,
            SyncWordProperties_Clr                                          = 0x2238,
            DigitalGainControl_Clr                                          = 0x223C,
            PeripheralReflexSystemControl_Clr                               = 0x2240,
            PowerAmplifierDebug_Clr                                         = 0x2244,
            RealTimeCostFunctionEngineControl_Clr                           = 0x2248,
            SequencerInterruptFlags_Clr                                     = 0x224C,
            SequencerInterruptEnable_Clr                                    = 0x2250,
            EarlyTimeStampControl_Clr                                       = 0x2254,
            AntennaSwitchControl_Clr                                        = 0x2258,
            AntennaSwitchStart_Clr                                          = 0x225C,
            AntennaSwitchEnd_Clr                                            = 0x2260,
            TrecsPreamblePattern_Clr                                        = 0x2264,
            TrecsPreambleDetectionControl_Clr                               = 0x2268,
            ConfigureAntennaPattern_Clr                                     = 0x226C,
            EarlyTimeStampTiming_Clr                                        = 0x2270,
            AntennaSwitchControl1_Clr                                       = 0x2274,
            ConcurrentMode_Clr                                              = 0x2278,
            AntennaDiversityModeControl_Clr                                 = 0x227C,
            BleIqDetectionOfSignalArrivalExtension2_Clr                     = 0x2280,
            Spare_Clr                                                       = 0x2284,
            IrCalCoefficientWrPerAntenna0_Clr                               = 0x2288,
            IrCalCoefficientWrPerAntenna1_Clr                               = 0x228C,

            // Toggle registers
            IpVersion_Tgl = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            Status_Tgl                                                      = 0x3008,
            TimingDetectionStatus_Tgl                                       = 0x300C,
            FrequencyOffsetEstimate_Tgl                                     = 0x3010,
            AutomaticFrequencyControlAdjustmentRx_Tgl                       = 0x3014,
            AutomaticFrequencyControldAjustmentTx_Tgl                       = 0x3018,
            AnalogMixerControl_Tgl                                          = 0x301C,
            Control0_Tgl                                                    = 0x3020,
            Control1_Tgl                                                    = 0x3024,
            Control2_Tgl                                                    = 0x3028,
            Control3_Tgl                                                    = 0x302C,
            Control4_Tgl                                                    = 0x3030,
            Control5_Tgl                                                    = 0x3034,
            Control6_Tgl                                                    = 0x3038,
            TxBaudrate_Tgl                                                  = 0x3058,
            RxBaudrate_Tgl                                                  = 0x305C,
            ChannelFilter_Tgl                                               = 0x3060,
            Preamble_Tgl                                                    = 0x3064,
            SyncWord0_Tgl                                                   = 0x3068,
            SyncWord1_Tgl                                                   = 0x306C,
            Timing_Tgl                                                      = 0x3080,
            DirectSequenceSpreadSpectrumSymbol0_Tgl                         = 0x3084,
            ModulationIndex_Tgl                                             = 0x3088,
            AutomaticFrequencyControl_Tgl                                   = 0x308C,
            AutomaticFrequencyControlAdjustmentLimit_Tgl                    = 0x3090,
            ShapingCoefficients0_Tgl                                        = 0x3094,
            ShapingCoefficients1_Tgl                                        = 0x3098,
            ShapingCoefficients2_Tgl                                        = 0x309C,
            ShapingCoefficients3_Tgl                                        = 0x30A0,
            ShapingCoefficients4_Tgl                                        = 0x30A4,
            ShapingCoefficients5_Tgl                                        = 0x30A8,
            ShapingCoefficients6_Tgl                                        = 0x30AC,
            ShapingCoefficients7_Tgl                                        = 0x30B0,
            ShapingCoefficients8_Tgl                                        = 0x30B4,
            ShapingCoefficients9_Tgl                                        = 0x30B8,
            ShapingCoefficients10_Tgl                                       = 0x30BC,
            ShapingCoefficients11_Tgl                                       = 0x30C0,
            RampingControl_Tgl                                              = 0x30C4,
            RampingLevels_Tgl                                               = 0x30CC,
            DirectCurrentOffsetCompensationFilterSettings_Tgl               = 0x30E0,
            DirectCurrentOffsetCompensationFilterInitialization_Tgl         = 0x30E4,
            DirectCurrentOffsetEstimatedValue_Tgl                           = 0x30E8,
            SampleRateConverterRatioValuesAndChannelFilter_Tgl              = 0x30EC,
            InternalAutomaticFrequencyControl_Tgl                           = 0x30F0,
            DetectionOfSignalArrivalThreshold0_Tgl                          = 0x30F4,
            DetectionOfSignalArrivalThreshold1_Tgl                          = 0x30F8,
            DetectionOfSignalArrivalMode_Tgl                                = 0x30FC,
            ViterbiDemodulator_Tgl                                          = 0x3100,
            ViterbiDemodulatorCorrelationConfiguration0_Tgl                 = 0x3104,
            DigitalMixerControl_Tgl                                         = 0x310C,
            ViterbiDemodulatorCorrelationConfiguration1_Tgl                 = 0x3110,
            ViterbiDemodulatorTrackingLoop_Tgl                              = 0x3114,
            BaudrateEstimate_Tgl                                            = 0x3118,
            AutomaticClockGating_Tgl                                        = 0x3124,
            AutomaticClockGatingClockStop_Tgl                               = 0x3128,
            PhaseOffsetEstimate_Tgl                                         = 0x312C,
            DetectionOfSignalArrivalThreshold2_Tgl                          = 0x3130,
            DirectModeControl_Tgl                                           = 0x3134,
            BleLongRange_Tgl                                                = 0x3138,
            BleLongRangeSet1_Tgl                                            = 0x313C,
            BleLongRangeSet2_Tgl                                            = 0x3140,
            BleLongRangeSet3_Tgl                                            = 0x3144,
            BleLongRangeSet4_Tgl                                            = 0x3148,
            BleLongRangeSet5_Tgl                                            = 0x314C,
            BleLongRangeSet6_Tgl                                            = 0x3150,
            BleLongRangeFrameControllerInterface_Tgl                        = 0x3154,
            CoherentDemodulatorSignals0_Tgl                                 = 0x3158,
            CoherentDemodulatorSignals1_Tgl                                 = 0x314C,
            CoherentDemodulatorSignals2_Tgl                                 = 0x3160,
            CoherentDemodulatorSignals3_Tgl                                 = 0x3164,
            DetectionOfSignalArrivalThreshold3_Tgl                          = 0x3168,
            DetectionOfSignalArrivalThreshold4_Tgl                          = 0x316C,
            ViterbiBleTimingStampControl_Tgl                                = 0x3170,
            InterruptFlags_Tgl                                              = 0x3208,
            InterruptEnable_Tgl                                             = 0x320C,
            Command_Tgl                                                     = 0x3218,
            DemodulatorFSMStatus_Tgl                                        = 0x321C,
            Status2_Tgl                                                     = 0x3220,
            Status3_Tgl                                                     = 0x3224,
            IrCalibrationControl_Tgl                                        = 0x3228,
            IrCalCoefficientValues_Tgl                                      = 0x322C,
            BleIqDetectionOfSignalArrival_Tgl                               = 0x3230,
            BleIqDetectionOfSignalArrivalExtension1_Tgl                     = 0x3234,
            SyncWordProperties_Tgl                                          = 0x3238,
            DigitalGainControl_Tgl                                          = 0x323C,
            PeripheralReflexSystemControl_Tgl                               = 0x3240,
            PowerAmplifierDebug_Tgl                                         = 0x3244,
            RealTimeCostFunctionEngineControl_Tgl                           = 0x3248,
            SequencerInterruptFlags_Tgl                                     = 0x324C,
            SequencerInterruptEnable_Tgl                                    = 0x3250,
            EarlyTimeStampControl_Tgl                                       = 0x3254,
            AntennaSwitchControl_Tgl                                        = 0x3258,
            AntennaSwitchStart_Tgl                                          = 0x325C,
            AntennaSwitchEnd_Tgl                                            = 0x3260,
            TrecsPreamblePattern_Tgl                                        = 0x3264,
            TrecsPreambleDetectionControl_Tgl                               = 0x3268,
            ConfigureAntennaPattern_Tgl                                     = 0x326C,
            EarlyTimeStampTiming_Tgl                                        = 0x3270,
            AntennaSwitchControl1_Tgl                                       = 0x3274,
            ConcurrentMode_Tgl                                              = 0x3278,
            AntennaDiversityModeControl_Tgl                                 = 0x327C,
            BleIqDetectionOfSignalArrivalExtension2_Tgl                     = 0x3280,
            Spare_Tgl                                                       = 0x3284,
            IrCalCoefficientWrPerAntenna0_Tgl                               = 0x3288,
            IrCalCoefficientWrPerAntenna1_Tgl                               = 0x328C,
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
            ReceivedSignalStrengthIndicatorStepThreshold                    = 0x003C,
            ManualGain                                                      = 0x0040,
            InterruptFlags                                                  = 0x0044,
            InterruptEnable                                                 = 0x0048,
            Command                                                         = 0x004C,
            RxGainRange                                                     = 0x0050,
            AutomaticGainControlPeriod                                      = 0x0054,
            HiCounterRegion                                                 = 0x0058,
            HiCounterRegion2                                                = 0x005C,
            GainStepsLimits                                                 = 0x0064,
            PnRfAttenuationCodeGroup0                                       = 0x0068,
            PnRfAttenuationCodeGroup1                                       = 0x006C,
            PnRfAttenuationCodeGroup2                                       = 0x0070,
            PnRfAttenuationCodeGroup3                                       = 0x0074,
            LnaMixSliceCodeGroup0                                           = 0x0078,
            LnaMixSliceCodeGroup1                                           = 0x007C,
            ProgrammableGainAmplifierGainCodeGroup0                         = 0x0080,
            ProgrammableGainAmplifierGainCodeGroup1                         = 0x0084,
            ListenBeforeTalkConfiguration                                   = 0x0088,
            MirrorInterruptFlags                                            = 0x008C,
            SequencerInterruptFlags                                         = 0x0090,
            SequencerInterruptEnable                                        = 0x0094,

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
            ReceivedSignalStrengthIndicatorStepThreshold_Set                = 0x103C,
            ManualGain_Set                                                  = 0x1040,
            InterruptFlags_Set                                              = 0x1044,
            InterruptEnable_Set                                             = 0x1048,
            Command_Set                                                     = 0x104C,
            RxGainRange_Set                                                 = 0x1050,
            AutomaticGainControlPeriod_Set                                  = 0x1054,
            HiCounterRegion_Set                                             = 0x1058,
            HiCounterRegion2_Set                                            = 0x105C,
            GainStepsLimits_Set                                             = 0x1064,
            PnRfAttenuationCodeGroup0_Set                                   = 0x1068,
            PnRfAttenuationCodeGroup1_Set                                   = 0x106C,
            PnRfAttenuationCodeGroup2_Set                                   = 0x1070,
            PnRfAttenuationCodeGroup3_Set                                   = 0x1074,
            LnaMixSliceCodeGroup0_Set                                       = 0x1078,
            LnaMixSliceCodeGroup1_Set                                       = 0x107C,
            ProgrammableGainAmplifierGainCodeGroup0_Set                     = 0x1080,
            ProgrammableGainAmplifierGainCodeGroup1_Set                     = 0x1084,
            ListenBeforeTalkConfiguration_Set                               = 0x1088,
            MirrorInterruptFlags_Set                                        = 0x108C,
            SequencerInterruptFlags_Set                                     = 0x1090,
            SequencerInterruptEnable_Set                                    = 0x1094,

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
            ReceivedSignalStrengthIndicatorStepThreshold_Clr                = 0x203C,
            ManualGain_Clr                                                  = 0x2040,
            InterruptFlags_Clr                                              = 0x2044,
            InterruptEnable_Clr                                             = 0x2048,
            Command_Clr                                                     = 0x204C,
            RxGainRange_Clr                                                 = 0x2050,
            AutomaticGainControlPeriod_Clr                                  = 0x2054,
            HiCounterRegion_Clr                                             = 0x2058,
            HiCounterRegion2_Clr                                            = 0x205C,
            GainStepsLimits_Clr                                             = 0x2064,
            PnRfAttenuationCodeGroup0_Clr                                   = 0x2068,
            PnRfAttenuationCodeGroup1_Clr                                   = 0x206C,
            PnRfAttenuationCodeGroup2_Clr                                   = 0x2070,
            PnRfAttenuationCodeGroup3_Clr                                   = 0x2074,
            LnaMixSliceCodeGroup0_Clr                                       = 0x2078,
            LnaMixSliceCodeGroup1_Clr                                       = 0x207C,
            ProgrammableGainAmplifierGainCodeGroup0_Clr                     = 0x2080,
            ProgrammableGainAmplifierGainCodeGroup1_Clr                     = 0x2084,
            ListenBeforeTalkConfiguration_Clr                               = 0x2088,
            MirrorInterruptFlags_Clr                                        = 0x208C,
            SequencerInterruptFlags_Clr                                     = 0x2090,
            SequencerInterruptEnable_Clr                                    = 0x2094,

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
            ReceivedSignalStrengthIndicatorStepThreshold_Tgl                = 0x303C,
            ManualGain_Tgl                                                  = 0x3040,
            InterruptFlags_Tgl                                              = 0x3044,
            InterruptEnable_Tgl                                             = 0x3048,
            Command_Tgl                                                     = 0x304C,
            RxGainRange_Tgl                                                 = 0x3050,
            AutomaticGainControlPeriod_Tgl                                  = 0x3054,
            HiCounterRegion_Tgl                                             = 0x3058,
            HiCounterRegion2_Tgl                                            = 0x305C,
            GainStepsLimits_Tgl                                             = 0x3064,
            PnRfAttenuationCodeGroup0_Tgl                                   = 0x3068,
            PnRfAttenuationCodeGroup1_Tgl                                   = 0x306C,
            PnRfAttenuationCodeGroup2_Tgl                                   = 0x3070,
            PnRfAttenuationCodeGroup3_Tgl                                   = 0x3074,
            LnaMixSliceCodeGroup0_Tgl                                       = 0x3078,
            LnaMixSliceCodeGroup1_Tgl                                       = 0x307C,
            ProgrammableGainAmplifierGainCodeGroup0_Tgl                     = 0x3080,
            ProgrammableGainAmplifierGainCodeGroup1_Tgl                     = 0x3084,
            ListenBeforeTalkConfiguration_Tgl                               = 0x3088,
            MirrorInterruptFlags_Tgl                                        = 0x308C,
            SequencerInterruptFlags_Tgl                                     = 0x3090,
            SequencerInterruptEnable_Tgl                                    = 0x3094,
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
            PrsControl                                                      = 0x0010,
            Status                                                          = 0x0014,
            PreCounterValue                                                 = 0x0018,
            BaseCounterValue                                                = 0x001C,
            WrapCounterValue                                                = 0x0020,
            BaseAndPreCounterValues                                         = 0x0024,
            LatchedWrapCounterValue                                         = 0x0028,
            PreCounterTopAdjustValue                                        = 0x002C,
            PreCounterTopValue                                              = 0x0030,
            BaseCounterTopValue                                             = 0x0034,
            WrapCounterTopValue                                             = 0x0038,
            Timeout0Counter                                                 = 0x003C,
            Timeout0CounterTop                                              = 0x0040,
            Timeout0Compare                                                 = 0x0044,
            Timeout1Counter                                                 = 0x0048,
            Timeout1CounterTop                                              = 0x004C,
            Timeout1Compare                                                 = 0x0050,
            ListenBeforeTalkWaitControl                                     = 0x0054,
            ListenBeforeTalkPrsControl                         = 0x0058,
            ListenBeforeTalkState                                           = 0x005C,
            PseudoRandomGeneratorValue                                      = 0x0060,
            InterruptFlags                                                  = 0x0064,
            InterruptEnable                                                 = 0x0070,
            RxControl                                                       = 0x0074,
            TxControl                                                       = 0x0078,
            ListenBeforeTalkETSIStandardSupport                             = 0x007C,
            ListenBeforeTalkState1                                          = 0x0080,
            LinearRandomValueGeneratedByFirmware0                           = 0x0084,
            LinearRandomValueGeneratedByFirmware1                           = 0x0088,
            LinearRandomValueGeneratedByFirmware2                           = 0x008C,
            SequencerInterruptFlags                                         = 0x0090,
            SequencerInterruptEnable                                        = 0x0094,
            CaptureCompareChannel0Control                                   = 0x0100,
            CaptureCompareChannel0PreValue                                  = 0x0104,
            CaptureCompareChannel0BaseValue                                 = 0x0108,
            CaptureCompareChannel0WrapValue                                 = 0x010C,
            CaptureCompareChannel1Control                                   = 0x0110,
            CaptureCompareChannel1PreValue                                  = 0x0114,
            CaptureCompareChannel1BaseValue                                 = 0x0118,
            CaptureCompareChannel1WrapValue                                 = 0x011C,
            CaptureCompareChannel2Control                                   = 0x0120,
            CaptureCompareChannel2PreValue                                  = 0x0124,
            CaptureCompareChannel2BaseValue                                 = 0x0128,
            CaptureCompareChannel2WrapValue                                 = 0x012C,
            CaptureCompareChannel3Control                                   = 0x0130,
            CaptureCompareChannel3PreValue                                  = 0x0134,
            CaptureCompareChannel3BaseValue                                 = 0x0138,
            CaptureCompareChannel3WrapValue                                 = 0x013C,
            CaptureCompareChannel4Control                                   = 0x0140,
            CaptureCompareChannel4PreValue                                  = 0x0144,
            CaptureCompareChannel4BaseValue                                 = 0x0148,
            CaptureCompareChannel4WrapValue                                 = 0x014C,
            CaptureCompareChannel5Control                                   = 0x0150,
            CaptureCompareChannel5PreValue                                  = 0x0154,
            CaptureCompareChannel5BaseValue                                 = 0x0158,
            CaptureCompareChannel5WrapValue                                 = 0x015C,
            CaptureCompareChannel6Control                                   = 0x0160,
            CaptureCompareChannel6PreValue                                  = 0x0164,
            CaptureCompareChannel6BaseValue                                 = 0x0168,
            CaptureCompareChannel6WrapValue                                 = 0x016C,
            CaptureCompareChannel7Control                                   = 0x0170,
            CaptureCompareChannel7PreValue                                  = 0x0174,
            CaptureCompareChannel7BaseValue                                 = 0x0178,
            CaptureCompareChannel7WrapValue                                 = 0x017C,

            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            Control_Set                                                     = 0x1008,
            Command_Set                                                     = 0x100C,
            PrsControl_Set                                                  = 0x1010,
            Status_Set                                                      = 0x1014,
            PreCounterValue_Set                                             = 0x1018,
            BaseCounterValue_Set                                            = 0x101C,
            WrapCounterValue_Set                                            = 0x1020,
            BaseAndPreCounterValues_Set                                     = 0x1024,
            LatchedWrapCounterValue_Set                                     = 0x1028,
            PreCounterTopAdjustValue_Set                                    = 0x102C,
            PreCounterTopValue_Set                                          = 0x1030,
            BaseCounterTopValue_Set                                         = 0x1034,
            WrapCounterTopValue_Set                                         = 0x1038,
            Timeout0Counter_Set                                             = 0x103C,
            Timeout0CounterTop_Set                                          = 0x1040,
            Timeout0Compare_Set                                             = 0x1044,
            Timeout1Counter_Set                                             = 0x1048,
            Timeout1CounterTop_Set                                          = 0x104C,
            Timeout1Compare_Set                                             = 0x1050,
            ListenBeforeTalkWaitControl_Set                                 = 0x1054,
            ListenBeforeTalkPrsControl_Set                                  = 0x1058,
            ListenBeforeTalkState_Set                                       = 0x105C,
            PseudoRandomGeneratorValue_Set                                  = 0x1060,
            InterruptFlags_Set                                              = 0x1064,
            InterruptEnable_Set                                             = 0x1070,
            RxControl_Set                                                   = 0x1074,
            TxControl_Set                                                   = 0x1078,
            ListenBeforeTalkETSIStandardSupport_Set                         = 0x107C,
            ListenBeforeTalkState1_Set                                      = 0x1080,
            LinearRandomValueGeneratedByFirmware0_Set                       = 0x1084,
            LinearRandomValueGeneratedByFirmware1_Set                       = 0x1088,
            LinearRandomValueGeneratedByFirmware2_Set                       = 0x108C,
            SequencerInterruptFlags_Set                                     = 0x1090,
            SequencerInterruptEnable_Set                                    = 0x1094,
            CaptureCompareChannel0Control_Set                               = 0x1100,
            CaptureCompareChannel0PreValue_Set                              = 0x1104,
            CaptureCompareChannel0BaseValue_Set                             = 0x1108,
            CaptureCompareChannel0WrapValue_Set                             = 0x110C,
            CaptureCompareChannel1Control_Set                               = 0x1110,
            CaptureCompareChannel1PreValue_Set                              = 0x1114,
            CaptureCompareChannel1BaseValue_Set                             = 0x1118,
            CaptureCompareChannel1WrapValue_Set                             = 0x111C,
            CaptureCompareChannel2Control_Set                               = 0x1120,
            CaptureCompareChannel2PreValue_Set                              = 0x1124,
            CaptureCompareChannel2BaseValue_Set                             = 0x1128,
            CaptureCompareChannel2WrapValue_Set                             = 0x112C,
            CaptureCompareChannel3Control_Set                               = 0x1130,
            CaptureCompareChannel3PreValue_Set                              = 0x1134,
            CaptureCompareChannel3BaseValue_Set                             = 0x1138,
            CaptureCompareChannel3WrapValue_Set                             = 0x113C,
            CaptureCompareChannel4Control_Set                               = 0x1140,
            CaptureCompareChannel4PreValue_Set                              = 0x1144,
            CaptureCompareChannel4BaseValue_Set                             = 0x1148,
            CaptureCompareChannel4WrapValue_Set                             = 0x114C,
            CaptureCompareChannel5Control_Set                               = 0x1150,
            CaptureCompareChannel5PreValue_Set                              = 0x1154,
            CaptureCompareChannel5BaseValue_Set                             = 0x1158,
            CaptureCompareChannel5WrapValue_Set                             = 0x115C,
            CaptureCompareChannel6Control_Set                               = 0x1160,
            CaptureCompareChannel6PreValue_Set                              = 0x1164,
            CaptureCompareChannel6BaseValue_Set                             = 0x1168,
            CaptureCompareChannel6WrapValue_Set                             = 0x116C,
            CaptureCompareChannel7Control_Set                               = 0x1170,
            CaptureCompareChannel7PreValue_Set                              = 0x1174,
            CaptureCompareChannel7BaseValue_Set                             = 0x1178,
            CaptureCompareChannel7WrapValue_Set                             = 0x117C,

            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            Control_Clr                                                     = 0x2008,
            Command_Clr                                                     = 0x200C,
            PrsControl_Clr                                                  = 0x2010,
            Status_Clr                                                      = 0x2014,
            PreCounterValue_Clr                                             = 0x2018,
            BaseCounterValue_Clr                                            = 0x201C,
            WrapCounterValue_Clr                                            = 0x2020,
            BaseAndPreCounterValues_Clr                                     = 0x2024,
            LatchedWrapCounterValue_Clr                                     = 0x2028,
            PreCounterTopAdjustValue_Clr                                    = 0x202C,
            PreCounterTopValue_Clr                                          = 0x2030,
            BaseCounterTopValue_Clr                                         = 0x2034,
            WrapCounterTopValue_Clr                                         = 0x2038,
            Timeout0Counter_Clr                                             = 0x203C,
            Timeout0CounterTop_Clr                                          = 0x2040,
            Timeout0Compare_Clr                                             = 0x2044,
            Timeout1Counter_Clr                                             = 0x2048,
            Timeout1CounterTop_Clr                                          = 0x204C,
            Timeout1Compare_Clr                                             = 0x2050,
            ListenBeforeTalkWaitControl_Clr                                 = 0x2054,
            ListenBeforeTalkPrsControl_Clr                                  = 0x2058,
            ListenBeforeTalkState_Clr                                       = 0x205C,
            PseudoRandomGeneratorValue_Clr                                  = 0x2060,
            InterruptFlags_Clr                                              = 0x2064,
            InterruptEnable_Clr                                             = 0x2070,
            RxControl_Clr                                                   = 0x2074,
            TxControl_Clr                                                   = 0x2078,
            ListenBeforeTalkETSIStandardSupport_Clr                         = 0x207C,
            ListenBeforeTalkState1_Clr                                      = 0x2080,
            LinearRandomValueGeneratedByFirmware0_Clr                       = 0x2084,
            LinearRandomValueGeneratedByFirmware1_Clr                       = 0x2088,
            LinearRandomValueGeneratedByFirmware2_Clr                       = 0x208C,
            SequencerInterruptFlags_Clr                                     = 0x2090,
            SequencerInterruptEnable_Clr                                    = 0x2094,
            CaptureCompareChannel0Control_Clr                               = 0x2100,
            CaptureCompareChannel0PreValue_Clr                              = 0x2104,
            CaptureCompareChannel0BaseValue_Clr                             = 0x2108,
            CaptureCompareChannel0WrapValue_Clr                             = 0x210C,
            CaptureCompareChannel1Control_Clr                               = 0x2110,
            CaptureCompareChannel1PreValue_Clr                              = 0x2114,
            CaptureCompareChannel1BaseValue_Clr                             = 0x2118,
            CaptureCompareChannel1WrapValue_Clr                             = 0x211C,
            CaptureCompareChannel2Control_Clr                               = 0x2120,
            CaptureCompareChannel2PreValue_Clr                              = 0x2124,
            CaptureCompareChannel2BaseValue_Clr                             = 0x2128,
            CaptureCompareChannel2WrapValue_Clr                             = 0x212C,
            CaptureCompareChannel3Control_Clr                               = 0x2130,
            CaptureCompareChannel3PreValue_Clr                              = 0x2134,
            CaptureCompareChannel3BaseValue_Clr                             = 0x2138,
            CaptureCompareChannel3WrapValue_Clr                             = 0x213C,
            CaptureCompareChannel4Control_Clr                               = 0x2140,
            CaptureCompareChannel4PreValue_Clr                              = 0x2144,
            CaptureCompareChannel4BaseValue_Clr                             = 0x2148,
            CaptureCompareChannel4WrapValue_Clr                             = 0x214C,
            CaptureCompareChannel5Control_Clr                               = 0x2150,
            CaptureCompareChannel5PreValue_Clr                              = 0x2154,
            CaptureCompareChannel5BaseValue_Clr                             = 0x2158,
            CaptureCompareChannel5WrapValue_Clr                             = 0x215C,
            CaptureCompareChannel6Control_Clr                               = 0x2160,
            CaptureCompareChannel6PreValue_Clr                              = 0x2164,
            CaptureCompareChannel6BaseValue_Clr                             = 0x2168,
            CaptureCompareChannel6WrapValue_Clr                             = 0x216C,
            CaptureCompareChannel7Control_Clr                               = 0x2170,
            CaptureCompareChannel7PreValue_Clr                              = 0x2174,
            CaptureCompareChannel7BaseValue_Clr                             = 0x2178,
            CaptureCompareChannel7WrapValue_Clr                             = 0x217C,

            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            Control_Tgl                                                     = 0x3008,
            Command_Tgl                                                     = 0x300C,
            PrsControl_Tgl                                                  = 0x3010,
            Status_Tgl                                                      = 0x3014,
            PreCounterValue_Tgl                                             = 0x3018,
            BaseCounterValue_Tgl                                            = 0x301C,
            WrapCounterValue_Tgl                                            = 0x3020,
            BaseAndPreCounterValues_Tgl                                     = 0x3024,
            LatchedWrapCounterValue_Tgl                                     = 0x3028,
            PreCounterTopAdjustValue_Tgl                                    = 0x302C,
            PreCounterTopValue_Tgl                                          = 0x3030,
            BaseCounterTopValue_Tgl                                         = 0x3034,
            WrapCounterTopValue_Tgl                                         = 0x3038,
            Timeout0Counter_Tgl                                             = 0x303C,
            Timeout0CounterTop_Tgl                                          = 0x3040,
            Timeout0Compare_Tgl                                             = 0x3044,
            Timeout1Counter_Tgl                                             = 0x3048,
            Timeout1CounterTop_Tgl                                          = 0x304C,
            Timeout1Compare_Tgl                                             = 0x3050,
            ListenBeforeTalkWaitControl_Tgl                                 = 0x3054,
            ListenBeforeTalkPrsControl_Tgl                                  = 0x3058,
            ListenBeforeTalkState_Tgl                                       = 0x305C,
            PseudoRandomGeneratorValue_Tgl                                  = 0x3060,
            InterruptFlags_Tgl                                              = 0x3064,
            InterruptEnable_Tgl                                             = 0x3070,
            RxControl_Tgl                                                   = 0x3074,
            TxControl_Tgl                                                   = 0x3078,
            ListenBeforeTalkETSIStandardSupport_Tgl                         = 0x307C,
            ListenBeforeTalkState1_Tgl                                      = 0x3080,
            LinearRandomValueGeneratedByFirmware0_Tgl                       = 0x3084,
            LinearRandomValueGeneratedByFirmware1_Tgl                       = 0x3088,
            LinearRandomValueGeneratedByFirmware2_Tgl                       = 0x308C,
            SequencerInterruptFlags_Tgl                                     = 0x3090,
            SequencerInterruptEnable_Tgl                                    = 0x3094,
            CaptureCompareChannel0Control_Tgl                               = 0x3100,
            CaptureCompareChannel0PreValue_Tgl                              = 0x3104,
            CaptureCompareChannel0BaseValue_Tgl                             = 0x3108,
            CaptureCompareChannel0WrapValue_Tgl                             = 0x310C,
            CaptureCompareChannel1Control_Tgl                               = 0x3110,
            CaptureCompareChannel1PreValue_Tgl                              = 0x3114,
            CaptureCompareChannel1BaseValue_Tgl                             = 0x3118,
            CaptureCompareChannel1WrapValue_Tgl                             = 0x311C,
            CaptureCompareChannel2Control_Tgl                               = 0x3120,
            CaptureCompareChannel2PreValue_Tgl                              = 0x3124,
            CaptureCompareChannel2BaseValue_Tgl                             = 0x3128,
            CaptureCompareChannel2WrapValue_Tgl                             = 0x312C,
            CaptureCompareChannel3Control_Tgl                               = 0x3130,
            CaptureCompareChannel3PreValue_Tgl                              = 0x3134,
            CaptureCompareChannel3BaseValue_Tgl                             = 0x3138,
            CaptureCompareChannel3WrapValue_Tgl                             = 0x313C,
            CaptureCompareChannel4Control_Tgl                               = 0x3140,
            CaptureCompareChannel4PreValue_Tgl                              = 0x3144,
            CaptureCompareChannel4BaseValue_Tgl                             = 0x3148,
            CaptureCompareChannel4WrapValue_Tgl                             = 0x314C,
            CaptureCompareChannel5Control_Tgl                               = 0x3150,
            CaptureCompareChannel5PreValue_Tgl                              = 0x3154,
            CaptureCompareChannel5BaseValue_Tgl                             = 0x3158,
            CaptureCompareChannel5WrapValue_Tgl                             = 0x315C,
            CaptureCompareChannel6Control_Tgl                               = 0x3160,
            CaptureCompareChannel6PreValue_Tgl                              = 0x3164,
            CaptureCompareChannel6BaseValue_Tgl                             = 0x3168,
            CaptureCompareChannel6WrapValue_Tgl                             = 0x316C,
            CaptureCompareChannel7Control_Tgl                               = 0x3170,
            CaptureCompareChannel7PreValue_Tgl                              = 0x3174,
            CaptureCompareChannel7BaseValue_Tgl                             = 0x3178,
            CaptureCompareChannel7WrapValue_Tgl                             = 0x317C,
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
            SequencerTimerValue                                             = 0x0030,
            SequencerTimerCompareValue                                      = 0x0034,
            SequencerControl                                                = 0x0038,
            SequencerPrescaler                                              = 0x003C,
            Storage0                                                        = 0x0040,
            Storage1                                                        = 0x0044,
            Storage2                                                        = 0x0048,
            Storage3                                                        = 0x004C,
            SystemTickTimerControl                                          = 0x0050,
            FrameControlWordBufferWrite                                     = 0x0054,
            FrameControlWordBufferRead                                      = 0x0058,
            Em1pControlAndStatus                                            = 0x005C,
            SynthesizerEnableControl                                        = 0x0094,
            SynthesizerRegulatorEnableControl                               = 0x0098,
            VoltageControlledOscillatorControl                              = 0x009C,
            ChargePumpControl                                               = 0x00A0,
            SynthControl                                                    = 0x00A4,
            RadioFrequencyStatus                                            = 0x00A8,
            Status2                                                         = 0x00AC,
            IntermediateFrequencyProgrammableGainAmplifierControl           = 0x00B0,
            PowerAmplifierEnableControl                                     = 0x00B4,
            AutomaticPowerControl                                           = 0x00B8,
            AuxiliaryAnalogToDigitalConverterTrim                           = 0x00BC,
            AuxiliaryAnalogToDigitalConverterEnable                         = 0x00C0,
            AuxiliaryAnalogToDigitalConverterControl0                       = 0x00C4,
            AuxiliaryAnalogToDigitalConverterControl1                       = 0x00C8,
            AuxiliaryAnalogToDigitalConverterDigitalOutput                  = 0x00CC,
            ClockMultEnable0                                                = 0x00D0,
            ClockMultEnable1                                                = 0x00D4,
            ClockMultControl                                                = 0x00D8,
            ClockMultStatus                                                 = 0x00DC,
            IntermediateFrequencyAnalogToDigitalConverterDebug              = 0x00E0,
            IntermediateFrequencyAnalogToDigitalConverterTrim0              = 0x00E4,
            IntermediateFrequencyAnalogToDigitalConverterTrim1              = 0x00E8,
            IntermediateFrequencyAnalogToDigitalConverterCalibration        = 0x00EC,
            IntermediateFrequencyAnalogToDigitalConverterStatus             = 0x00F0,
            LowNoiseAmplifierMixerDebug                                     = 0x00F4,
            LowNoiseAmplifierMixerTrim0                                     = 0x00F8,
            LowNoiseAmplifierMixerTrim1                                     = 0x00FC,
            LowNoiseAmplifierMixerCalibration                               = 0x0104,
            LowNoiseAmplifierMixerEnable                                    = 0x0108,
            PreCounterOverflow                                              = 0x010C,
            PowerAmplifierTrim0                                             = 0x0110,
            PowerAmplifierTrim1                                             = 0x0114,
            PowerAmplifierTrim2                                             = 0x0118,
            PowerAmplifierTrim3                                             = 0x011C,
            PowerAmplifierControl                                           = 0x0120,
            ProgrammableGainAmplifierDebug                                  = 0x0124,
            ProgrammableGainAmplifierTrim                                   = 0x0128,
            ProgrammableGainAmplifierCalibration                            = 0x012C,
            ProgrammableGainAmplifierControl                                = 0x0130,
            RadioFrequencyBiasCalibration                                   = 0x0134,
            RadioFrequencyBiasControl                                       = 0x0138,
            RadioEnable                                                     = 0x013C,
            RadioFrequencyPathEnable                                        = 0x0140,
            Rx                                                              = 0x0144,
            Tx                                                              = 0x0148,
            SyDebug                                                         = 0x014C,
            SyTrim0                                                         = 0x0150,
            SyTrim1                                                         = 0x0154,
            SyCalibration                                                   = 0x0158,
            SyEnable                                                        = 0x015C,
            SyloEnable                                                      = 0x0160,
            SymmControl                                                     = 0x0168,
            DigitalClockRetimeControl                                       = 0x016C,
            DigitalClockRetimeStatus                                        = 0x0170,
            XoRetimeControl                                                 = 0x0174,
            XoRetimeStatus                                                  = 0x0178,
            XoSqBuffFilt                                                    = 0x017C,
            Spare                                                           = 0x0180,
            AutomaticGainControlOverwrite                                   = 0x0188,
            Scratch0                                                        = 0x03E0,
            Scratch1                                                        = 0x03E4,
            Scratch2                                                        = 0x03E8,
            Scratch3                                                        = 0x03EC,
            Scratch4                                                        = 0x03F0,
            Scratch5                                                        = 0x03F4,
            Scratch6                                                        = 0x03F8,
            Scratch7                                                        = 0x03FC,
            ThermisterControl                                               = 0x07E8,
            DiagaAlternateEnable                                            = 0x07EC,
            DiagaAlternateRfBlocksAndTpSelect                               = 0x07F0,
            DiagaAlternateBridgeControl                                     = 0x07F4,

            // Set registers
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
            SequencerTimerValue_Set                                         = 0x1030,
            SequencerTimerCompareValue_Set                                  = 0x1034,
            SequencerControl_Set                                            = 0x1038,
            SequencerPrescaler_Set                                          = 0x103C,
            Storage0_Set                                                    = 0x1040,
            Storage1_Set                                                    = 0x1044,
            Storage2_Set                                                    = 0x1048,
            Storage3_Set                                                    = 0x104C,
            SystemTickTimerControl_Set                                      = 0x1050,
            FrameControlWordBufferWrite_Set                                 = 0x1054,
            FrameControlWordBufferRead_Set                                  = 0x1058,
            Em1pControlAndStatus_Set                                        = 0x105C,
            SynthesizerEnableControl_Set                                    = 0x1094,
            SynthesizerRegulatorEnableControl_Set                           = 0x1098,
            VoltageControlledOscillatorControl_Set                          = 0x109C,
            ChargePumpControl_Set                                           = 0x10A0,
            SynthControl_Set                                                = 0x10A4,
            RadioFrequencyStatus_Set                                        = 0x10A8,
            Status2_Set                                                     = 0x10AC,
            IntermediateFrequencyProgrammableGainAmplifierControl_Set       = 0x10B0,
            PowerAmplifierEnableControl_Set                                 = 0x10B4,
            AutomaticPowerControl_Set                                       = 0x10B8,
            AuxiliaryAnalogToDigitalConverterTrim_Set                       = 0x10BC,
            AuxiliaryAnalogToDigitalConverterEnable_Set                     = 0x10C0,
            AuxiliaryAnalogToDigitalConverterControl0_Set                   = 0x10C4,
            AuxiliaryAnalogToDigitalConverterControl1_Set                   = 0x10C8,
            AuxiliaryAnalogToDigitalConverterDigitalOutput_Set              = 0x10CC,
            ClockMultEnable0_Set                                            = 0x10D0,
            ClockMultEnable1_Set                                            = 0x10D4,
            ClockMultControl_Set                                            = 0x10D8,
            ClockMultStatus_Set                                             = 0x10DC,
            IntermediateFrequencyAnalogToDigitalConverterDebug_Set          = 0x10E0,
            IntermediateFrequencyAnalogToDigitalConverterTrim0_Set          = 0x10E4,
            IntermediateFrequencyAnalogToDigitalConverterTrim1_Set          = 0x10E8,
            IntermediateFrequencyAnalogToDigitalConverterCalibration_Set    = 0x10EC,
            IntermediateFrequencyAnalogToDigitalConverterStatus_Set         = 0x10F0,
            LowNoiseAmplifierMixerDebug_Set                                 = 0x10F4,
            LowNoiseAmplifierMixerTrim0_Set                                 = 0x10F8,
            LowNoiseAmplifierMixerTrim1_Set                                 = 0x10FC,
            LowNoiseAmplifierMixerCalibration_Set                           = 0x1104,
            LowNoiseAmplifierMixerEnable_Set                                = 0x1108,
            PreCounterOverflow_Set                                          = 0x110C,
            PowerAmplifierTrim0_Set                                         = 0x1110,
            PowerAmplifierTrim1_Set                                         = 0x1114,
            PowerAmplifierTrim2_Set                                         = 0x1118,
            PowerAmplifierTrim3_Set                                         = 0x111C,
            PowerAmplifierControl_Set                                       = 0x1120,
            ProgrammableGainAmplifierDebug_Set                              = 0x1124,
            ProgrammableGainAmplifierTrim_Set                               = 0x1128,
            ProgrammableGainAmplifierCalibration_Set                        = 0x112C,
            ProgrammableGainAmplifierControl_Set                            = 0x1130,
            RadioFrequencyBiasCalibration_Set                               = 0x1134,
            RadioFrequencyBiasControl_Set                                   = 0x1138,
            RadioEnable_Set                                                 = 0x113C,
            RadioFrequencyPathEnable_Set                                    = 0x1140,
            Rx_Set                                                          = 0x1144,
            Tx_Set                                                          = 0x1148,
            SyDebug_Set                                                     = 0x114C,
            SyTrim0_Set                                                     = 0x1150,
            SyTrim1_Set                                                     = 0x1154,
            SyCalibration_Set                                               = 0x1158,
            SyEnable_Set                                                    = 0x115C,
            SyloEnable_Set                                                  = 0x1160,
            SymmControl_Set                                                 = 0x1168,
            DigitalClockRetimeControl_Set                                   = 0x116C,
            DigitalClockRetimeStatus_Set                                    = 0x1170,
            XoRetimeControl_Set                                             = 0x1174,
            XoRetimeStatus_Set                                              = 0x1178,
            XoSqBuffFilt_Set                                                = 0x117C,
            Spare_Set                                                       = 0x1180,
            AutomaticGainControlOverwrite_Set                               = 0x1188,
            Scratch0_Set                                                    = 0x13E0,
            Scratch1_Set                                                    = 0x13E4,
            Scratch2_Set                                                    = 0x13E8,
            Scratch3_Set                                                    = 0x13EC,
            Scratch4_Set                                                    = 0x13F0,
            Scratch5_Set                                                    = 0x13F4,
            Scratch6_Set                                                    = 0x13F8,
            Scratch7_Set                                                    = 0x13FC,
            ThermisterControl_Set                                           = 0x17E8,
            DiagaAlternateEnable_Set                                        = 0x17EC,
            DiagaAlternateRfBlocksAndTpSelect_Set                           = 0x17F0,
            DiagaAlternateBridgeControl_Set                                 = 0x17F4,

            // Clear registers
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
            SequencerTimerValue_Clr                                         = 0x2030,
            SequencerTimerCompareValue_Clr                                  = 0x2034,
            SequencerControl_Clr                                            = 0x2038,
            SequencerPrescaler_Clr                                          = 0x203C,
            Storage0_Clr                                                    = 0x2040,
            Storage1_Clr                                                    = 0x2044,
            Storage2_Clr                                                    = 0x2048,
            Storage3_Clr                                                    = 0x204C,
            SystemTickTimerControl_Clr                                      = 0x2050,
            FrameControlWordBufferWrite_Clr                                 = 0x2054,
            FrameControlWordBufferRead_Clr                                  = 0x2058,
            Em1pControlAndStatus_Clr                                        = 0x205C,
            SynthesizerEnableControl_Clr                                    = 0x2094,
            SynthesizerRegulatorEnableControl_Clr                           = 0x2098,
            VoltageControlledOscillatorControl_Clr                          = 0x209C,
            ChargePumpControl_Clr                                           = 0x20A0,
            SynthControl_Clr                                                = 0x20A4,
            RadioFrequencyStatus_Clr                                        = 0x20A8,
            Status2_Clr                                                     = 0x20AC,
            IntermediateFrequencyProgrammableGainAmplifierControl_Clr       = 0x20B0,
            PowerAmplifierEnableControl_Clr                                 = 0x20B4,
            AutomaticPowerControl_Clr                                       = 0x20B8,
            AuxiliaryAnalogToDigitalConverterTrim_Clr                       = 0x20BC,
            AuxiliaryAnalogToDigitalConverterEnable_Clr                     = 0x20C0,
            AuxiliaryAnalogToDigitalConverterControl0_Clr                   = 0x20C4,
            AuxiliaryAnalogToDigitalConverterControl1_Clr                   = 0x20C8,
            AuxiliaryAnalogToDigitalConverterDigitalOutput_Clr              = 0x20CC,
            ClockMultEnable0_Clr                                            = 0x20D0,
            ClockMultEnable1_Clr                                            = 0x20D4,
            ClockMultControl_Clr                                            = 0x20D8,
            ClockMultStatus_Clr                                             = 0x20DC,
            IntermediateFrequencyAnalogToDigitalConverterDebug_Clr          = 0x20E0,
            IntermediateFrequencyAnalogToDigitalConverterTrim0_Clr          = 0x20E4,
            IntermediateFrequencyAnalogToDigitalConverterTrim1_Clr          = 0x20E8,
            IntermediateFrequencyAnalogToDigitalConverterCalibration_Clr    = 0x20EC,
            IntermediateFrequencyAnalogToDigitalConverterStatus_Clr         = 0x20F0,
            LowNoiseAmplifierMixerDebug_Clr                                 = 0x20F4,
            LowNoiseAmplifierMixerTrim0_Clr                                 = 0x20F8,
            LowNoiseAmplifierMixerTrim1_Clr                                 = 0x20FC,
            LowNoiseAmplifierMixerCalibration_Clr                           = 0x2104,
            LowNoiseAmplifierMixerEnable_Clr                                = 0x2108,
            PreCounterOverflow_Clr                                          = 0x210C,
            PowerAmplifierTrim0_Clr                                         = 0x2110,
            PowerAmplifierTrim1_Clr                                         = 0x2114,
            PowerAmplifierTrim2_Clr                                         = 0x2118,
            PowerAmplifierTrim3_Clr                                         = 0x211C,
            PowerAmplifierControl_Clr                                       = 0x2120,
            ProgrammableGainAmplifierDebug_Clr                              = 0x2124,
            ProgrammableGainAmplifierTrim_Clr                               = 0x2128,
            ProgrammableGainAmplifierCalibration_Clr                        = 0x212C,
            ProgrammableGainAmplifierControl_Clr                            = 0x2130,
            RadioFrequencyBiasCalibration_Clr                               = 0x2134,
            RadioFrequencyBiasControl_Clr                                   = 0x2138,
            RadioEnable_Clr                                                 = 0x213C,
            RadioFrequencyPathEnable_Clr                                    = 0x2140,
            Rx_Clr                                                          = 0x2144,
            Tx_Clr                                                          = 0x2148,
            SyDebug_Clr                                                     = 0x214C,
            SyTrim0_Clr                                                     = 0x2150,
            SyTrim1_Clr                                                     = 0x2154,
            SyCalibration_Clr                                               = 0x2158,
            SyEnable_Clr                                                    = 0x215C,
            SyloEnable_Clr                                                  = 0x2160,
            SymmControl_Clr                                                 = 0x2168,
            DigitalClockRetimeControl_Clr                                   = 0x216C,
            DigitalClockRetimeStatus_Clr                                    = 0x2170,
            XoRetimeControl_Clr                                             = 0x2174,
            XoRetimeStatus_Clr                                              = 0x2178,
            XoSqBuffFilt_Clr                                                = 0x217C,
            Spare_Clr                                                       = 0x2180,
            AutomaticGainControlOverwrite_Clr                               = 0x2188,
            Scratch0_Clr                                                    = 0x23E0,
            Scratch1_Clr                                                    = 0x23E4,
            Scratch2_Clr                                                    = 0x23E8,
            Scratch3_Clr                                                    = 0x23EC,
            Scratch4_Clr                                                    = 0x23F0,
            Scratch5_Clr                                                    = 0x23F4,
            Scratch6_Clr                                                    = 0x23F8,
            Scratch7_Clr                                                    = 0x23FC,
            ThermisterControl_Clr                                           = 0x27E8,
            DiagaAlternateEnable_Clr                                        = 0x27EC,
            DiagaAlternateRfBlocksAndTpSelect_Clr                           = 0x27F0,
            DiagaAlternateBridgeControl_Clr                                 = 0x27F4,

            // Toggle registers
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
            SequencerTimerValue_Tgl                                         = 0x3030,
            SequencerTimerCompareValue_Tgl                                  = 0x3034,
            SequencerControl_Tgl                                            = 0x3038,
            SequencerPrescaler_Tgl                                          = 0x303C,
            Storage0_Tgl                                                    = 0x3040,
            Storage1_Tgl                                                    = 0x3044,
            Storage2_Tgl                                                    = 0x3048,
            Storage3_Tgl                                                    = 0x304C,
            SystemTickTimerControl_Tgl                                      = 0x3050,
            FrameControlWordBufferWrite_Tgl                                 = 0x3054,
            FrameControlWordBufferRead_Tgl                                  = 0x3058,
            Em1pControlAndStatus_Tgl                                        = 0x305C,
            SynthesizerEnableControl_Tgl                                    = 0x3094,
            SynthesizerRegulatorEnableControl_Tgl                           = 0x3098,
            VoltageControlledOscillatorControl_Tgl                          = 0x309C,
            ChargePumpControl_Tgl                                           = 0x30A0,
            SynthControl_Tgl                                                = 0x30A4,
            RadioFrequencyStatus_Tgl                                        = 0x30A8,
            Status2_Tgl                                                     = 0x30AC,
            IntermediateFrequencyProgrammableGainAmplifierControl_Tgl       = 0x30B0,
            PowerAmplifierEnableControl_Tgl                                 = 0x30B4,
            AutomaticPowerControl_Tgl                                       = 0x30B8,
            AuxiliaryAnalogToDigitalConverterTrim_Tgl                       = 0x30BC,
            AuxiliaryAnalogToDigitalConverterEnable_Tgl                     = 0x30C0,
            AuxiliaryAnalogToDigitalConverterControl0_Tgl                   = 0x30C4,
            AuxiliaryAnalogToDigitalConverterControl1_Tgl                   = 0x30C8,
            AuxiliaryAnalogToDigitalConverterDigitalOutput_Tgl              = 0x30CC,
            ClockMultEnable0_Tgl                                            = 0x30D0,
            ClockMultEnable1_Tgl                                            = 0x30D4,
            ClockMultControl_Tgl                                            = 0x30D8,
            ClockMultStatus_Tgl                                             = 0x30DC,
            IntermediateFrequencyAnalogToDigitalConverterDebug_Tgl          = 0x30E0,
            IntermediateFrequencyAnalogToDigitalConverterTrim0_Tgl          = 0x30E4,
            IntermediateFrequencyAnalogToDigitalConverterTrim1_Tgl          = 0x30E8,
            IntermediateFrequencyAnalogToDigitalConverterCalibration_Tgl    = 0x30EC,
            IntermediateFrequencyAnalogToDigitalConverterStatus_Tgl         = 0x30F0,
            LowNoiseAmplifierMixerDebug_Tgl                                 = 0x30F4,
            LowNoiseAmplifierMixerTrim0_Tgl                                 = 0x30F8,
            LowNoiseAmplifierMixerTrim1_Tgl                                 = 0x30FC,
            LowNoiseAmplifierMixerCalibration_Tgl                           = 0x3104,
            LowNoiseAmplifierMixerEnable_Tgl                                = 0x3108,
            PreCounterOverflow_Tgl                                          = 0x310C,
            PowerAmplifierTrim0_Tgl                                         = 0x3110,
            PowerAmplifierTrim1_Tgl                                         = 0x3114,
            PowerAmplifierTrim2_Tgl                                         = 0x3118,
            PowerAmplifierTrim3_Tgl                                         = 0x311C,
            PowerAmplifierControl_Tgl                                       = 0x3120,
            ProgrammableGainAmplifierDebug_Tgl                              = 0x3124,
            ProgrammableGainAmplifierTrim_Tgl                               = 0x3128,
            ProgrammableGainAmplifierCalibration_Tgl                        = 0x312C,
            ProgrammableGainAmplifierControl_Tgl                            = 0x3130,
            RadioFrequencyBiasCalibration_Tgl                               = 0x3134,
            RadioFrequencyBiasControl_Tgl                                   = 0x3138,
            RadioEnable_Tgl                                                 = 0x313C,
            RadioFrequencyPathEnable_Tgl                                    = 0x3140,
            Rx_Tgl                                                          = 0x3144,
            Tx_Tgl                                                          = 0x3148,
            SyDebug_Tgl                                                     = 0x314C,
            SyTrim0_Tgl                                                     = 0x3150,
            SyTrim1_Tgl                                                     = 0x3154,
            SyCalibration_Tgl                                               = 0x3158,
            SyEnable_Tgl                                                    = 0x315C,
            SyloEnable_Tgl                                                  = 0x3160,
            SymmControl_Tgl                                                 = 0x3168,
            DigitalClockRetimeControl_Tgl                                   = 0x316C,
            DigitalClockRetimeStatus_Tgl                                    = 0x3170,
            XoRetimeControl_Tgl                                             = 0x3174,
            XoRetimeStatus_Tgl                                              = 0x3178,
            XoSqBuffFilt_Tgl                                                = 0x317C,
            Spare_Tgl                                                       = 0x3180,
            AutomaticGainControlOverwrite_Tgl                               = 0x3188,
            Scratch0_Tgl                                                    = 0x33E0,
            Scratch1_Tgl                                                    = 0x33E4,
            Scratch2_Tgl                                                    = 0x33E8,
            Scratch3_Tgl                                                    = 0x33EC,
            Scratch4_Tgl                                                    = 0x33F0,
            Scratch5_Tgl                                                    = 0x33F4,
            Scratch6_Tgl                                                    = 0x33F8,
            Scratch7_Tgl                                                    = 0x33FC,
            ThermisterControl_Tgl                                           = 0x37E8,
            DiagaAlternateEnable_Tgl                                        = 0x37EC,
            DiagaAlternateRfBlocksAndTpSelect_Tgl                           = 0x37F0,
            DiagaAlternateBridgeControl_Tgl                                 = 0x37F4,
        }

        private enum SYNTH_LoDivFreqCtrl
        {
            LoDiv1  = 0x01,
            LoDiv2  = 0x02,
            LoDiv3  = 0x03,
            LoDiv4  = 0x04,
            LoDiv5  = 0x05,
            LoDiv7  = 0x07,
            LoDiv6  = 0x13,
            LoDiv8  = 0x14,
            LoDiv10 = 0x15,
            LoDiv14 = 0x17,
            LoDiv9  = 0x1B,
            LoDiv12 = 0x1C,
            LoDiv15 = 0x1D,
            LoDiv16 = 0x24,
            LoDiv20 = 0x25,
            LoDiv18 = 0x9B,
            LoDiv24 = 0x9C,
        }

        private enum SynthesizerRegisters : long
        {
            IpVersion                                                       = 0x0000,
            Enable                                                          = 0x0004,
            Status                                                          = 0x0008,
            Command                                                         = 0x000C,
            Control                                                         = 0x0010,
            CalibrationControl                                              = 0x0014,
            VCDACControl                                                    = 0x002C,
            Frequency                                                       = 0x0034,
            IntermediateFrequency                                           = 0x0038,
            FrequencyDivisionControl                                        = 0x003C,
            ChannelControl                                                  = 0x0040,
            ChannelSpacing                                                  = 0x0044,
            CalibrationOffset                                               = 0x0048,
            VoltageControlledOscillatorFrequencyTuning                      = 0x004C,
            VoltageControlledOscillatorFrequencyRangeControl                = 0x0054,
            VoltageControlledOscillatorGainCalibration                      = 0x0058,
            ChargePumpDigitalToAnalogConverterControl                       = 0x0068,
            CapacitorArrayCalibrationCycleCount                             = 0x006C,
            VoltageControlledOscillatorForceCalibrationCycleCount           = 0x0070,
            InterruptFlags                                                  = 0x0078,
            InterruptEnable                                                 = 0x0084,
            LoCounterControl                                                = 0x0088,
            LoCounterStatus                                                 = 0x008C,
            LoCounterTargetValue                                            = 0x0090,
            MmdDenominatorInitialValue                                      = 0x0094,
            ChargePumpDigitalToAnalogConverterInitialValue                  = 0x0098,
            LowPassFilterCalModeControl                                     = 0x009C,
            LowPassFilterRxModeControl1                                     = 0x00A0,
            LowPassFilterTxModeControl1                                     = 0x00A4,
            LowPassFilterRxModeControl2                                     = 0x00A8,
            LowPassFilterTxModeControl2                                     = 0x00AC,
            DsmRxModeControl                                                = 0x00B0,
            DsmTxModeControl                                                = 0x00B4,
            SequencerInterruptFlags                                         = 0x00B8,
            SequencerInterruptEnable                                        = 0x00BC,

            // Set registers
            IpVersion_Set                                                   = 0x1000,
            Enable_Set                                                      = 0x1004,
            Status_Set                                                      = 0x1008,
            Command_Set                                                     = 0x100C,
            Control_Set                                                     = 0x1010,
            CalibrationControl_Set                                          = 0x1014,
            VCDACControl_Set                                                = 0x102C,
            Frequency_Set                                                   = 0x1034,
            IntermediateFrequency_Set                                       = 0x1038,
            FrequencyDivisionControl_Set                                    = 0x103C,
            ChannelControl_Set                                              = 0x1040,
            ChannelSpacing_Set                                              = 0x1044,
            CalibrationOffset_Set                                           = 0x1048,
            VoltageControlledOscillatorFrequencyTuning_Set                  = 0x104C,
            VoltageControlledOscillatorFrequencyRangeControl_Set            = 0x1054,
            VoltageControlledOscillatorGainCalibration_Set                  = 0x1058,
            ChargePumpDigitalToAnalogConverterControl_Set                   = 0x1068,
            CapacitorArrayCalibrationCycleCount_Set                         = 0x106C,
            VoltageControlledOscillatorForceCalibrationCycleCount_Set       = 0x1070,
            InterruptFlags_Set                                              = 0x1078,
            InterruptEnable_Set                                             = 0x1084,
            LoCounterControl_Set                                            = 0x1088,
            LoCounterStatus_Set                                             = 0x108C,
            LoCounterTargetValue_Set                                        = 0x1090,
            MmdDenominatorInitialValue_Set                                  = 0x1094,
            ChargePumpDigitalToAnalogConverterInitialValue_Set              = 0x1098,
            LowPassFilterCalModeControl_Set                                 = 0x109C,
            LowPassFilterRxModeControl1_Set                                 = 0x10A0,
            LowPassFilterTxModeControl1_Set                                 = 0x10A4,
            LowPassFilterRxModeControl2_Set                                 = 0x10A8,
            LowPassFilterTxModeControl2_Set                                 = 0x10AC,
            DsmRxModeControl_Set                                            = 0x10B0,
            DsmTxModeControl_Set                                            = 0x10B4,
            SequencerInterruptFlags_Set                                     = 0x10B8,
            SequencerInterruptEnable_Set                                    = 0x10BC,

            // Clear registers
            IpVersion_Clr                                                   = 0x2000,
            Enable_Clr                                                      = 0x2004,
            Status_Clr                                                      = 0x2008,
            Command_Clr                                                     = 0x200C,
            Control_Clr                                                     = 0x2010,
            CalibrationControl_Clr                                          = 0x2014,
            VCDACControl_Clr                                                = 0x202C,
            Frequency_Clr                                                   = 0x2034,
            IntermediateFrequency_Clr                                       = 0x2038,
            FrequencyDivisionControl_Clr                                    = 0x203C,
            ChannelControl_Clr                                              = 0x2040,
            ChannelSpacing_Clr                                              = 0x2044,
            CalibrationOffset_Clr                                           = 0x2048,
            VoltageControlledOscillatorFrequencyTuning_Clr                  = 0x204C,
            VoltageControlledOscillatorFrequencyRangeControl_Clr            = 0x2054,
            VoltageControlledOscillatorGainCalibration_Clr                  = 0x2058,
            ChargePumpDigitalToAnalogConverterControl_Clr                   = 0x2068,
            CapacitorArrayCalibrationCycleCount_Clr                         = 0x206C,
            VoltageControlledOscillatorForceCalibrationCycleCount_Clr       = 0x2070,
            InterruptFlags_Clr                                              = 0x2078,
            InterruptEnable_Clr                                             = 0x2084,
            LoCounterControl_Clr                                            = 0x2088,
            LoCounterStatus_Clr                                             = 0x208C,
            LoCounterTargetValue_Clr                                        = 0x2090,
            MmdDenominatorInitialValue_Clr                                  = 0x2094,
            ChargePumpDigitalToAnalogConverterInitialValue_Clr              = 0x2098,
            LowPassFilterCalModeControl_Clr                                 = 0x209C,
            LowPassFilterRxModeControl1_Clr                                 = 0x20A0,
            LowPassFilterTxModeControl1_Clr                                 = 0x20A4,
            LowPassFilterRxModeControl2_Clr                                 = 0x20A8,
            LowPassFilterTxModeControl2_Clr                                 = 0x20AC,
            DsmRxModeControl_Clr                                            = 0x20B0,
            DsmTxModeControl_Clr                                            = 0x20B4,
            SequencerInterruptFlags_Clr                                     = 0x20B8,
            SequencerInterruptEnable_Clr                                    = 0x20BC,

            // Toggle registers
            IpVersion_Tgl                                                   = 0x3000,
            Enable_Tgl                                                      = 0x3004,
            Status_Tgl                                                      = 0x3008,
            Command_Tgl                                                     = 0x300C,
            Control_Tgl                                                     = 0x3010,
            CalibrationControl_Tgl                                          = 0x3014,
            VCDACControl_Tgl                                                = 0x302C,
            Frequency_Tgl                                                   = 0x3034,
            IntermediateFrequency_Tgl                                       = 0x3038,
            FrequencyDivisionControl_Tgl                                    = 0x303C,
            ChannelControl_Tgl                                              = 0x3040,
            ChannelSpacing_Tgl                                              = 0x3044,
            CalibrationOffset_Tgl                                           = 0x3048,
            VoltageControlledOscillatorFrequencyTuning_Tgl                  = 0x304C,
            VoltageControlledOscillatorFrequencyRangeControl_Tgl            = 0x3054,
            VoltageControlledOscillatorGainCalibration_Tgl                  = 0x3058,
            ChargePumpDigitalToAnalogConverterControl_Tgl                   = 0x3068,
            CapacitorArrayCalibrationCycleCount_Tgl                         = 0x306C,
            VoltageControlledOscillatorForceCalibrationCycleCount_Tgl       = 0x3070,
            InterruptFlags_Tgl                                              = 0x3078,
            InterruptEnable_Tgl                                             = 0x3084,
            LoCounterControl_Tgl                                            = 0x3088,
            LoCounterStatus_Tgl                                             = 0x308C,
            LoCounterTargetValue_Tgl                                        = 0x3090,
            MmdDenominatorInitialValue_Tgl                                  = 0x3094,
            ChargePumpDigitalToAnalogConverterInitialValue_Tgl              = 0x3098,
            LowPassFilterCalModeControl_Tgl                                 = 0x309C,
            LowPassFilterRxModeControl1_Tgl                                 = 0x30A0,
            LowPassFilterTxModeControl1_Tgl                                 = 0x30A4,
            LowPassFilterRxModeControl2_Tgl                                 = 0x30A8,
            LowPassFilterTxModeControl2_Tgl                                 = 0x30AC,
            DsmRxModeControl_Tgl                                            = 0x30B0,
            DsmTxModeControl_Tgl                                            = 0x30B4,
            SequencerInterruptFlags_Tgl                                     = 0x30B8,
            SequencerInterruptEnable_Tgl                                    = 0x30BC,
        }

        private enum HostMailboxRegisters : long
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

        private enum RadioMailboxRegisters : long
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
    }
}
