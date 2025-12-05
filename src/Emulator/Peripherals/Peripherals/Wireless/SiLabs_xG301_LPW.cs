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
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public class SiLabs_xG301_LPW : IBusPeripheral, IRadio, SiLabs_IProtocolTimer, SiLabs_IPacketTraceSniffer
    {
        public SiLabs_xG301_LPW(Machine machine, CV32E40P sequencer = null, SiLabs_IRvConfig sequencerConfig = null, SiLabs_BUFC_5 bufferController = null)
        {
            this.machine = machine;
            this.sequencer = sequencer;
            this.sequencerConfig = sequencerConfig;
            this.bufferController = bufferController;

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

            FrameControllerPrioritizedIRQ = new GPIO();
            FrameControllerIRQ = new GPIO();
            ModulatorAndDemodulatorIRQ = new GPIO();
            RadioControllerSequencerIRQ = new GPIO();
            RadioControllerRadioStateMachineIRQ = new GPIO();
            ProtocolTimerIRQ = new GPIO();
            SynthesizerIRQ = new GPIO();
            AutomaticGainControlIRQ = new GPIO();
            Lpw0PortalIRQ = new GPIO();
            RfTimerIRQ = new GPIO();

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
            SeqProtocolTimerIRQ = new GPIO();
            SeqModulatorAndDemodulatorIRQ = new GPIO();
            SeqSynthesizerIRQ = new GPIO();
            SeqAutomaticGainControlIRQ = new GPIO();
            SeqHostPortalIRQ = new GPIO();
            SeqRfMailboxIRQ = new GPIO();

            FRC_frameDescriptor = new FRC_FrameDescriptor[FRC_NumberOfFrameDescriptors];
            for(var idx = 0u; idx < FRC_NumberOfFrameDescriptors; ++idx)
            {
                FRC_frameDescriptor[idx] = new FRC_FrameDescriptor();
            }
            FRC_packetBufferCapture = new byte[FRC_PacketBufferCaptureSize];

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
            field_234 = new IValueRegisterField[PROTIMER_NumberOfListenBeforeTalkRandomBackoffValues];

            frameControllerRegistersCollection = BuildFrameControllerRegistersCollection();
            cyclicRedundancyCheckRegistersCollection = BuildCyclicRedundancyCheckRegistersCollection();
            synthesizerRegistersCollection = BuildSynthesizerRegistersCollection();
            radioControllerRegistersCollection = BuildRadioControllerRegistersCollection();
            protocolTimerRegistersCollection = BuildProtocolTimerRegistersCollection();
            modulatorAndDemodulatorRegistersCollection = BuildModulatorAndDemodulatorRegistersCollection();
            automaticGainControlRegistersCollection = BuildAutomaticGainControlRegistersCollection();
            hostPortalRegistersCollection = BuildHostPortalRegistersCollection();
            lpw0PortalRegistersCollection = BuildLpw0PortalRegistersCollection();
            rfMailboxRegistersCollection = BuildRfMailboxRegistersCollection();
            fswMailboxRegistersCollection = BuildFswMailboxRegistersCollection();

            InterferenceQueue.InterferenceQueueChanged += InteferenceQueueChangedCallback;
        }

        [ConnectionRegionAttribute("frc_s")]
        public uint ReadDoubleWordFromFrameController(long offset)
        {
            return Read<Registers_C>(frameControllerRegistersCollection, "Frame Controller (FRC_S)", offset);
        }

        [ConnectionRegionAttribute("frc_s")]
        public byte ReadByteFromFrameController(long offset)
        {
            return ReadByte<Registers_C>(frameControllerRegistersCollection, "Frame Controller (FRC_S)", offset);
        }

        [ConnectionRegionAttribute("frc_s")]
        public void WriteDoubleWordToFrameController(long offset, uint value)
        {
            Write<Registers_C>(frameControllerRegistersCollection, "Frame Controller (FRC_S)", offset, value);
        }

        [ConnectionRegionAttribute("frc_s")]
        public void WriteByteToFrameController(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("frc_ns")]
        public uint ReadDoubleWordFromFrameControllerNonSecure(long offset)
        {
            return Read<Registers_C>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset);
        }

        [ConnectionRegionAttribute("frc_ns")]
        public byte ReadByteFromFrameControllerNonSecure(long offset)
        {
            return ReadByte<Registers_C>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset);
        }

        [ConnectionRegionAttribute("frc_ns")]
        public void WriteDoubleWordToFrameControllerNonSecure(long offset, uint value)
        {
            Write<Registers_C>(frameControllerRegistersCollection, "Frame Controller (FRC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("frc_ns")]
        public void WriteByteToFrameControllerNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("agc_s")]
        public uint ReadDoubleWordFromAutomaticGainController(long offset)
        {
            return Read<Registers_A>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset);
        }

        [ConnectionRegionAttribute("agc_s")]
        public byte ReadByteFromAutomaticGainController(long offset)
        {
            return ReadByte<Registers_A>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset);
        }

        [ConnectionRegionAttribute("agc_s")]
        public void WriteDoubleWordToAutomaticGainController(long offset, uint value)
        {
            Write<Registers_A>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset, value);
        }

        [ConnectionRegionAttribute("agc_s")]
        public void WriteByteToAutomaticGainController(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("agc_ns")]
        public uint ReadDoubleWordFromAutomaticGainControllerNonSecure(long offset)
        {
            return Read<Registers_A>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_NS)", offset);
        }

        [ConnectionRegionAttribute("agc_ns")]
        public byte ReadByteFromAutomaticGainControllerNonSecure(long offset)
        {
            return ReadByte<Registers_A>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_S)", offset);
        }

        [ConnectionRegionAttribute("agc_ns")]
        public void WriteDoubleWordToAutomaticGainControllerNonSecure(long offset, uint value)
        {
            Write<Registers_A>(automaticGainControlRegistersCollection, "Automatic Gain Control (AGC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("agc_ns")]
        public void WriteByteToAutomaticGainControllerNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("crc_s")]
        public uint ReadDoubleWordFromCyclicRedundancyCheck(long offset)
        {
            return Read<Registers_B>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_S)", offset);
        }

        [ConnectionRegionAttribute("crc_s")]
        public byte ReadByteFromCyclicRedundancyCheck(long offset)
        {
            return ReadByte<Registers_B>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_S)", offset);
        }

        [ConnectionRegionAttribute("crc_s")]
        public void WriteDoubleWordToCyclicRedundancyCheck(long offset, uint value)
        {
            Write<Registers_B>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_S)", offset, value);
        }

        [ConnectionRegionAttribute("crc_s")]
        public void WriteByteToCyclicRedundancyCheck(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("crc_ns")]
        public uint ReadDoubleWordFromCyclicRedundancyCheckNonSecure(long offset)
        {
            return Read<Registers_B>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset);
        }

        [ConnectionRegionAttribute("crc_ns")]
        public byte ReadByteFromCyclicRedundancyCheckNonSecure(long offset)
        {
            return ReadByte<Registers_B>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset);
        }

        [ConnectionRegionAttribute("crc_ns")]
        public void WriteDoubleWordToCyclicRedundancyCheckNonSecure(long offset, uint value)
        {
            Write<Registers_B>(cyclicRedundancyCheckRegistersCollection, "Cyclic Redundancy Check (CRC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("crc_ns")]
        public void WriteByteToCyclicRedundancyCheckNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("modem_s")]
        public uint ReadDoubleWordFromModulatorAndDemodulator(long offset)
        {
            return Read<Registers_G>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_S)", offset);
        }

        [ConnectionRegionAttribute("modem_s")]
        public byte ReadByteFromModulatorAndDemodulator(long offset)
        {
            return ReadByte<Registers_G>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_S)", offset);
        }

        [ConnectionRegionAttribute("modem_s")]
        public void WriteDoubleWordToModulatorAndDemodulator(long offset, uint value)
        {
            Write<Registers_G>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_S)", offset, value);
        }

        [ConnectionRegionAttribute("modem_s")]
        public void WriteByteToModulatorAndDemodulator(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("modem_ns")]
        public uint ReadDoubleWordFromModulatorAndDemodulatorNonSecure(long offset)
        {
            return Read<Registers_G>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset);
        }

        [ConnectionRegionAttribute("modem_ns")]
        public byte ReadByteFromModulatorAndDemodulatorNonSecure(long offset)
        {
            return ReadByte<Registers_G>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset);
        }

        [ConnectionRegionAttribute("modem_ns")]
        public void WriteDoubleWordToModulatorAndDemodulatorNonSecure(long offset, uint value)
        {
            Write<Registers_G>(modulatorAndDemodulatorRegistersCollection, "Modulator And Demodulator (MODEM_NS)", offset, value);
        }

        [ConnectionRegionAttribute("modem_ns")]
        public void WriteByteToModulatorAndDemodulatorNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("synth_s")]
        public uint ReadDoubleWordFromSynthesizer(long offset)
        {
            return Read<Registers_K>(synthesizerRegistersCollection, "Synthesizer (SYNTH_S)", offset);
        }

        [ConnectionRegionAttribute("synth_s")]
        public byte ReadByteFromSynthesizer(long offset)
        {
            return ReadByte<Registers_K>(synthesizerRegistersCollection, "Synthesizer (SYNTH_S)", offset);
        }

        [ConnectionRegionAttribute("synth_s")]
        public void WriteDoubleWordToSynthesizer(long offset, uint value)
        {
            Write<Registers_K>(synthesizerRegistersCollection, "Synthesizer (SYNTH_S)", offset, value);
        }

        [ConnectionRegionAttribute("synth_s")]
        public void WriteByteToSynthesizer(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("synth_ns")]
        public uint ReadDoubleWordFromSynthesizerNonSecure(long offset)
        {
            return Read<Registers_K>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset);
        }

        [ConnectionRegionAttribute("synth_ns")]
        public byte ReadByteFromSynthesizerNonSecure(long offset)
        {
            return ReadByte<Registers_K>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset);
        }

        [ConnectionRegionAttribute("synth_ns")]
        public void WriteDoubleWordToSynthesizerNonSecure(long offset, uint value)
        {
            Write<Registers_K>(synthesizerRegistersCollection, "Synthesizer (SYNTH_NS)", offset, value);
        }

        [ConnectionRegionAttribute("synth_ns")]
        public void WriteByteToSynthesizerNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("protimer_s")]
        public uint ReadDoubleWordFromProtocolTimer(long offset)
        {
            return Read<Registers_H>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_S)", offset);
        }

        [ConnectionRegionAttribute("protimer_s")]
        public byte ReadByteFromProtocolTimer(long offset)
        {
            return ReadByte<Registers_H>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_S)", offset);
        }

        [ConnectionRegionAttribute("protimer_s")]
        public void WriteDoubleWordToProtocolTimer(long offset, uint value)
        {
            Write<Registers_H>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_S)", offset, value);
        }

        [ConnectionRegionAttribute("protimer_s")]
        public void WriteByteToProtocolTimer(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public uint ReadDoubleWordFromProtocolTimerNonSecure(long offset)
        {
            return Read<Registers_H>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset);
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public byte ReadByteFromProtocolTimerNonSecure(long offset)
        {
            return ReadByte<Registers_H>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset);
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public void WriteDoubleWordToProtocolTimerNonSecure(long offset, uint value)
        {
            Write<Registers_H>(protocolTimerRegistersCollection, "Protocol Timer (PROTIMER_NS)", offset, value);
        }

        [ConnectionRegionAttribute("protimer_ns")]
        public void WriteByteToProtocolTimerNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("rac_s")]
        public uint ReadDoubleWordFromRadioController(long offset)
        {
            return Read<Registers_I>(radioControllerRegistersCollection, "Radio Controller (RAC_S)", offset);
        }

        [ConnectionRegionAttribute("rac_s")]
        public byte ReadByteFromRadioController(long offset)
        {
            return ReadByte<Registers_I>(radioControllerRegistersCollection, "Radio Controller (RAC_S)", offset);
        }

        [ConnectionRegionAttribute("rac_s")]
        public void WriteDoubleWordToRadioController(long offset, uint value)
        {
            Write<Registers_I>(radioControllerRegistersCollection, "Radio Controller (RAC_S)", offset, value);
        }

        [ConnectionRegionAttribute("rac_s")]
        public void WriteByteToRadioController(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("rac_ns")]
        public uint ReadDoubleWordFromRadioControllerNonSecure(long offset)
        {
            return Read<Registers_I>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset);
        }

        [ConnectionRegionAttribute("rac_ns")]
        public byte ReadByteFromRadioControllerNonSecure(long offset)
        {
            return ReadByte<Registers_I>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset);
        }

        [ConnectionRegionAttribute("rac_ns")]
        public void WriteDoubleWordToRadioControllerNonSecure(long offset, uint value)
        {
            Write<Registers_I>(radioControllerRegistersCollection, "Radio Controller (RAC_NS)", offset, value);
        }

        [ConnectionRegionAttribute("rac_ns")]
        public void WriteByteToRadioControllerNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public uint ReadDoubleWordFromRfMailbox(long offset)
        {
            return Read<Registers_J>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public byte ReadByteFromRfMailbox(long offset)
        {
            return ReadByte<Registers_J>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public void WriteDoubleWordToRfMailbox(long offset, uint value)
        {
            Write<Registers_J>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_S)", offset, value);
        }

        [ConnectionRegionAttribute("rfmailbox_s")]
        public void WriteByteToRfMailbox(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public uint ReadDoubleWordFromRfMailboxNonSecure(long offset)
        {
            return Read<Registers_J>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public byte ReadByteFromRfMailboxNonSecure(long offset)
        {
            return ReadByte<Registers_J>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public void WriteDoubleWordToRfMailboxNonSecure(long offset, uint value)
        {
            Write<Registers_J>(rfMailboxRegistersCollection, "RF Mailbox (RFMAILBOX_NS)", offset, value);
        }

        [ConnectionRegionAttribute("rfmailbox_ns")]
        public void WriteByteToRfMailboxNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public uint ReadDoubleWordFromFswMailbox(long offset)
        {
            return Read<Registers_D>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public byte ReadByteFromFswMailbox(long offset)
        {
            return ReadByte<Registers_D>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_S)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public void WriteDoubleWordToFswMailbox(long offset, uint value)
        {
            Write<Registers_D>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_S)", offset, value);
        }

        [ConnectionRegionAttribute("fswmailbox_s")]
        public void WriteByteToFswMailbox(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public uint ReadDoubleWordFromFswMailboxNonSecure(long offset)
        {
            return Read<Registers_D>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public byte ReadByteFromFswMailboxNonSecure(long offset)
        {
            return ReadByte<Registers_D>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_NS)", offset);
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public void WriteDoubleWordToFswMailboxNonSecure(long offset, uint value)
        {
            Write<Registers_D>(fswMailboxRegistersCollection, "FSW Mailbox (FSWMAILBOX_NS)", offset, value);
        }

        [ConnectionRegionAttribute("fswmailbox_ns")]
        public void WriteByteToFswMailboxNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public uint ReadDoubleWordFromHostPortal(long offset)
        {
            return Read<Registers_E>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public byte ReadByteFromHostPortal(long offset)
        {
            return ReadByte<Registers_E>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public void WriteDoubleWordToHostPortal(long offset, uint value)
        {
            Write<Registers_E>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_S)", offset, value);
        }

        [ConnectionRegionAttribute("hostportal_s")]
        public void WriteByteToHostPortal(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public uint ReadDoubleWordFromHostPortalNonSecure(long offset)
        {
            return Read<Registers_E>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public byte ReadByteFromHostPortalNonSecure(long offset)
        {
            return ReadByte<Registers_E>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public void WriteDoubleWordToHostPortalNonSecure(long offset, uint value)
        {
            Write<Registers_E>(hostPortalRegistersCollection, "Host Portal (HOSTPORTAL_NS)", offset, value);
        }

        [ConnectionRegionAttribute("hostportal_ns")]
        public void WriteByteToHostPortalNonSecure(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public uint ReadDoubleWordFromLpw0Portal(long offset)
        {
            return Read<Registers_F>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public byte ReadByteFromLpw0Portal(long offset)
        {
            return ReadByte<Registers_F>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_S)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public void WriteDoubleWordToLpw0Portal(long offset, uint value)
        {
            Write<Registers_F>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_S)", offset, value);
        }

        [ConnectionRegionAttribute("lpw0portal_s")]
        public void WriteByteToLpw0Portal(long offset, byte value)
        {
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public uint ReadDoubleWordFromLpw0PortalNonSecure(long offset)
        {
            return Read<Registers_F>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public byte ReadByteFromLpw0PortalNonSecure(long offset)
        {
            return ReadByte<Registers_F>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_NS)", offset);
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public void WriteDoubleWordToLpw0PortalNonSecure(long offset, uint value)
        {
            Write<Registers_F>(lpw0PortalRegistersCollection, "LPW0 Portal (LPW0PORTAL_NS)", offset, value);
        }

        [ConnectionRegionAttribute("lpw0portal_ns")]
        public void WriteByteToLpw0PortalNonSecure(long offset, byte value)
        {
        }

        public void Reset()
        {
            currentFrame = null;
            currentFrameOffset = 0;
            currentChannel = 0;

            FRC_rxFrameExitPending = false;
            FRC_rxDonePending = false;

            RAC_rxTimeAlreadyPassedUs = 0;
            RAC_ongoingRxCollided = false;
            RAC_currentRadioState = Enumeration_AB.EnumerationABValue0;
            RAC_previous1RadioState = Enumeration_AB.EnumerationABValue0;
            RAC_previous2RadioState = Enumeration_AB.EnumerationABValue0;
            RAC_previous3RadioState = Enumeration_AB.EnumerationABValue0;
            RAC_dcCalDone = false;
            RAC_paOutputLevelRamping = false;

            PROTIMER_baseCounterValue = 0;
            PROTIMER_wrapCounterValue = 0;
            PROTIMER_latchedBaseCounterValue = 0;
            PROTIMER_latchedWrapCounterValue = 0;
            PROTIMER_seqLatchedBaseCounterValue = 0;
            PROTIMER_seqLatchedWrapCounterValue = 0;
            PROTIMER_txEnable = false;
            PROTIMER_txRequestState = Enumeration_X.EnumerationXValue0;
            PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue0;
            PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue0;
            PROTIMER_listenBeforeTalkPending = false;

            MODEM_txRampingDoneInterrupt = true;

            AGC_rssiFirstRead = true;
            AGC_rssiStartCommandOngoing = false;
            AGC_rssiStartCommandFromProtimer = false;

            SYNTH_state = Enumeration_AD.EnumerationADValue0;

            HOSTPORTAL_powerUpOngoing = false;

            LPW0PORTAL_powerUpOngoing = false;

            proTimer.Enabled = false;
            paRampingTimer.Enabled = false;
            rssiUpdateTimer.Enabled = false;
            synthTimer.Enabled = false;
            txTimer.Enabled = false;
            rxTimer.Enabled = false;

            sequencer.IsHalted = true;
            sequencer.Reset();


            RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue1);

            frameControllerRegistersCollection.Reset();
            cyclicRedundancyCheckRegistersCollection.Reset();
            synthesizerRegistersCollection.Reset();
            radioControllerRegistersCollection.Reset();
            protocolTimerRegistersCollection.Reset();
            modulatorAndDemodulatorRegistersCollection.Reset();
            automaticGainControlRegistersCollection.Reset();
            hostPortalRegistersCollection.Reset();
            lpw0PortalRegistersCollection.Reset();
            rfMailboxRegistersCollection.Reset();
            fswMailboxRegistersCollection.Reset();

            UpdateInterrupts();
        }

        public void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                FRC_CheckPacketCaptureBufferThreshold();


                var irq = ((field_303.Value && field_304.Value)
                           || (field_356.Value && field_357.Value));
                if(LogInterrupts && irq)
                {
                }
                RadioControllerRadioStateMachineIRQ.Set(irq);

                irq = ((field_300.Value & field_301.Value) > 0);
                if(LogInterrupts && irq)
                {
                }
                RadioControllerSequencerIRQ.Set(irq);

                irq = ((field_138.Value && field_139.Value)
                       || (field_136.Value && field_137.Value)
                       || (field_143.Value && field_144.Value)
                       || (field_103.Value && field_104.Value)
                       || (field_91.Value && field_92.Value)
                       || (field_68.Value && field_69.Value)
                       || (field_106.Value && field_107.Value)
                       || (field_111.Value && field_112.Value)
                       || (field_141.Value && field_142.Value)
                       || (field_78.Value && field_79.Value)
                       || (field_83.Value && field_84.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    frameControllerRegistersCollection.TryRead((long)Registers_C.RegC_27, out interruptFlag);
                    frameControllerRegistersCollection.TryRead((long)Registers_C.RegC_28, out interruptEnable);
                }
                FrameControllerIRQ.Set(irq);

                irq = (field_258.Value && field_259.Value)
                       || (field_235.Value && field_236.Value)
                       || (field_286.Value && field_287.Value)
                       || (field_253.Value && field_254.Value)
                       || (field_244.Value && field_245.Value)
                       || (field_249.Value && field_250.Value)
                       || (field_256.Value && field_257.Value);
                Array.ForEach(PROTIMER_timeoutCounter, x => irq |= x.Interrupt);
                Array.ForEach(PROTIMER_captureCompareChannel, x => irq |= x.Interrupt);
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptFlag2 = 0U;
                    var interruptEnable = 0U;
                    var interruptEnable2 = 0U;
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_32, out interruptFlag);
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_30, out interruptFlag2);
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_33, out interruptEnable);
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_31, out interruptEnable2);
                }
                ProtocolTimerIRQ.Set(irq);

                irq = ((field_219.Value && field_220.Value)
                       || (field_226.Value && field_227.Value)
                       || (field_222.Value && field_223.Value)
                       || (MODEM_TxRampingDoneInterrupt && field_224.Value)
                       || (field_186.Value && field_187.Value)
                       || (field_178.Value && field_179.Value)
                       || (field_180.Value && field_181.Value)
                       || (field_182.Value && field_183.Value)
                       || (field_184.Value && field_185.Value)
                       || (field_188.Value && field_189.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptFlag2 = 0U;
                    var interruptEnable = 0U;
                    var interruptEnable2 = 0U;
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_3, out interruptFlag);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_4, out interruptFlag2);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_5, out interruptEnable);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_6, out interruptEnable2);
                }
                ModulatorAndDemodulatorIRQ.Set(irq);

                irq = ((field_24.Value && field_25.Value)
                       || (field_2.Value && field_3.Value)
                       || (field_13.Value && field_14.Value)
                       || (field_15.Value && field_16.Value)
                       || (field_18.Value && field_19.Value)
                       || (field_6.Value && field_7.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    automaticGainControlRegistersCollection.TryRead((long)Registers_A.RegA_18, out interruptFlag);
                    automaticGainControlRegistersCollection.TryRead((long)Registers_A.RegA_19, out interruptEnable);
                }
                AutomaticGainControlIRQ.Set(irq);

                irq = ((field_367.Value && field_368.Value)
                       || (field_364.Value && field_365.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    synthesizerRegistersCollection.TryRead((long)Registers_K.RegK_3, out interruptFlag);
                    synthesizerRegistersCollection.TryRead((long)Registers_K.RegK_4, out interruptEnable);
                }
                SynthesizerIRQ.Set(irq);

                int index;
                irq = false;
                for(index = 0; index < LPW0PORTAL_NumberOfInterrupts; index++)
                {
                    if(field_155[index].Value && field_156[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    lpw0PortalRegistersCollection.TryRead((long)Registers_F.RegF_4, out interruptFlag);
                    lpw0PortalRegistersCollection.TryRead((long)Registers_F.RegF_5, out interruptEnable);
                }
                Lpw0PortalIRQ.Set(irq);


                if(sequencer == null || sequencer.IsHalted)
                {
                    return;
                }

                irq = ((field_311.Value && field_312.Value)
                       || (field_349.Value && field_350.Value));
                if(LogInterrupts && irq)
                {
                }
                SeqRadioControllerIRQ.Set(irq);

                irq = ((field_130.Value && field_131.Value)
                       || (field_128.Value && field_129.Value)
                       || (field_134.Value && field_135.Value)
                       || (field_122.Value && field_123.Value)
                       || (field_120.Value && field_121.Value)
                       || (field_114.Value && field_115.Value)
                       || (field_124.Value && field_125.Value)
                       || (field_126.Value && field_127.Value)
                       || (field_132.Value && field_133.Value)
                       || (field_116.Value && field_117.Value)
                       || (field_118.Value && field_119.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    frameControllerRegistersCollection.TryRead((long)Registers_C.RegC_42, out interruptFlag);
                    frameControllerRegistersCollection.TryRead((long)Registers_C.RegC_43, out interruptEnable);
                }
                SeqFrameControllerIRQ.Set(irq);

                irq = (field_280.Value && field_281.Value)
                       || (field_270.Value && field_271.Value)
                       || (field_282.Value && field_283.Value)
                       || (field_276.Value && field_277.Value)
                       || (field_272.Value && field_273.Value)
                       || (field_274.Value && field_275.Value)
                       || (field_278.Value && field_279.Value);
                Array.ForEach(PROTIMER_timeoutCounter, x => irq |= x.SeqInterrupt);
                Array.ForEach(PROTIMER_captureCompareChannel, x => irq |= x.SeqInterrupt);
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptFlag2 = 0U;
                    var interruptEnable = 0U;
                    var interruptEnable2 = 0U;
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_43, out interruptFlag);
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_41, out interruptFlag2);
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_44, out interruptEnable);
                    protocolTimerRegistersCollection.TryRead((long)Registers_H.RegH_42, out interruptEnable2);
                }
                SeqProtocolTimerIRQ.Set(irq);

                irq = ((field_202.Value && field_203.Value)
                       || (field_207.Value && field_208.Value)
                       || (field_204.Value && field_205.Value)
                       || (MODEM_TxRampingDoneInterrupt && field_206.Value)
                       || (field_198.Value && field_199.Value)
                       || (field_190.Value && field_191.Value)
                       || (field_192.Value && field_193.Value)
                       || (field_194.Value && field_195.Value)
                       || (field_196.Value && field_197.Value)
                       || (field_200.Value && field_201.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptFlag2 = 0U;
                    var interruptEnable = 0U;
                    var interruptEnable2 = 0U;
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_7, out interruptFlag);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_8, out interruptFlag2);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_9, out interruptEnable);
                    modulatorAndDemodulatorRegistersCollection.TryRead((long)Registers_G.RegG_10, out interruptEnable2);
                }
                SeqModulatorAndDemodulatorIRQ.Set(irq);

                irq = ((field_36.Value && field_37.Value)
                       || (field_26.Value && field_27.Value)
                       || (field_30.Value && field_31.Value)
                       || (field_32.Value && field_33.Value)
                       || (field_34.Value && field_35.Value)
                       || (field_28.Value && field_29.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    automaticGainControlRegistersCollection.TryRead((long)Registers_A.RegA_44, out interruptFlag);
                    automaticGainControlRegistersCollection.TryRead((long)Registers_A.RegA_45, out interruptEnable);
                }
                SeqAutomaticGainControlIRQ.Set(irq);

                irq = ((field_371.Value && field_372.Value)
                       || (field_369.Value && field_370.Value));
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    synthesizerRegistersCollection.TryRead((long)Registers_K.RegK_5, out interruptFlag);
                    synthesizerRegistersCollection.TryRead((long)Registers_K.RegK_6, out interruptEnable);
                }
                SeqSynthesizerIRQ.Set(irq);

                irq = false;
                for(index = 0; index < HOSTPORTAL_NumberOfInterrupts; index++)
                {
                    if(field_149[index].Value && field_150[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    hostPortalRegistersCollection.TryRead((long)Registers_E.RegE_4, out interruptFlag);
                    hostPortalRegistersCollection.TryRead((long)Registers_E.RegE_5, out interruptEnable);
                }
                SeqHostPortalIRQ.Set(irq);

                irq = false;
                for(index = 0; index < RFMAILBOX_MessageNumber; index++)
                {
                    if(field_360[index].Value && field_361[index].Value)
                    {
                        irq = true;
                        break;
                    }
                }
                if(LogInterrupts && irq)
                {
                    var interruptFlag = 0U;
                    var interruptEnable = 0U;
                    rfMailboxRegistersCollection.TryRead((long)Registers_J.RegJ_5, out interruptFlag);
                    rfMailboxRegistersCollection.TryRead((long)Registers_J.RegJ_6, out interruptEnable);
                }
                SeqRfMailboxIRQ.Set(irq);

                irq = field_315.Value && field_316.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqOffIRQ.Set(irq);

                irq = field_327.Value && field_328.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqRxWarmIRQ.Set(irq);

                irq = field_323.Value && field_324.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqRxSearchIRQ.Set(irq);

                irq = field_319.Value && field_320.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqRxFrameIRQ.Set(irq);

                irq = field_331.Value && field_332.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqRxWrapUpIRQ.Set(irq);

                irq = field_343.Value && field_344.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqTxWarmIRQ.Set(irq);

                irq = field_339.Value && field_340.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqTxIRQ.Set(irq);

                irq = field_347.Value && field_348.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqTxWrapUpIRQ.Set(irq);

                irq = field_333.Value && field_334.Value;
                if(LogInterrupts && irq)
                {
                }
                SeqShutdownIRQ.Set(irq);
            });
        }

        public void InteferenceQueueChangedCallback()
        {
            if(RAC_currentRadioState == Enumeration_AB.EnumerationABValue2 || RAC_currentRadioState == Enumeration_AB.EnumerationABValue3)
            {
                AGC_UpdateRssi();
            }
        }

        public UInt64 PacketTraceRadioTimestamp()
        {
            return (UInt64)GetTime().TotalMicroseconds * 1000;
        }

        public uint GetWrapCountValue()
        {
            return PROTIMER_WrapCounterValue;
        }

        public uint GetBaseCountValue()
        {
            return PROTIMER_BaseCounterValue;
        }

        public uint GetCurrentPreCountOverflows()
        {
            TrySyncTime();
            return (uint)proTimer.Value;
        }

        public void FlushCurrentPreCountOverflows()
        {
            PROTIMER_HandleChangedParams();
        }

        public uint GetPreCountOverflowFrequency()
        {
            return PROTIMER_GetPreCntOverflowFrequency();
        }

        public PROTIMER_Event PROTIMER_GetTimeoutCounterEventFromIndex(uint index, PROTIMER_Event baseEvent)
        {
            if(baseEvent == PROTIMER_Event.TimeoutCounter0Match)
            {
                if(index < 2)
                {
                    return (PROTIMER_Event)((uint)PROTIMER_Event.TimeoutCounter0Match + index);
                }
                else if(index == 2)
                {
                    return PROTIMER_Event.TimeoutCounter2Match;
                }
            }
            else if(baseEvent == PROTIMER_Event.TimeoutCounter0Underflow)
            {
                if(index < 2)
                {
                    return (PROTIMER_Event)((uint)PROTIMER_Event.TimeoutCounter0Underflow + index);
                }
                else if(index == 2)
                {
                    return PROTIMER_Event.TimeoutCounter2Underflow;
                }
            }

            throw new Exception("PROTIMER_GetTimeoutCounterEventFromIndex: invalid param");
        }

        public PROTIMER_Event PROTIMER_GetCaptureCompareEventFromIndex(uint index)
        {
            if(index >= PROTIMER_NumberOfCaptureCompareChannels)
            {
                throw new Exception("PROTIMER_GetCaptureCompareEventFromIndex: invalid index");
            }

            if(index < 5)
            {
                return (PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel0Event + index);
            }
            else
            {
                return (PROTIMER_Event)((uint)PROTIMER_Event.CaptureCompareChannel5Event + (index - 5));
            }
        }

        public void PROTIMER_TriggerEvent(PROTIMER_Event ev)
        {
            if(ev < PROTIMER_Event.PreCounterOverflow || ev > PROTIMER_Event.InternalTrigger)
            {
                throw new Exception("Unreachable. Invalid event value for PROTIMER_TriggerEvent.");
            }

            if(ev == PROTIMER_Event.TimeoutCounter0Match && field_251.Value)
            {
                ev = PROTIMER_Event.TimeoutCounter0MatchListenBeforeTalk;
                field_256.Value = true;
                field_278.Value = true;
                UpdateInterrupts();
            }

            switch(PROTIMER_rxRequestState)
            {
            case Enumeration_X.EnumerationXValue0:
            {
                if(field_268.Value == PROTIMER_Event.Always)
                {
                    PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue1;
                    goto case Enumeration_X.EnumerationXValue1;
                }
                if(field_268.Value == ev)
                {
                    PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue1;
                }
                break;
            }
            case Enumeration_X.EnumerationXValue1:
            {
                if(field_269.Value == PROTIMER_Event.Always || field_269.Value == ev)
                {
                    PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue2;
                    RAC_UpdateRadioStateMachine();
                }
                break;
            }
            case Enumeration_X.EnumerationXValue2:
            {
                if(field_266.Value == PROTIMER_Event.Always)
                {
                    PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue3;
                    goto case Enumeration_X.EnumerationXValue3;
                }
                if(field_266.Value == ev)
                {
                    PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue3;
                }
                break;
            }
            case Enumeration_X.EnumerationXValue3:
            {
                if(field_267.Value == ev)
                {
                    PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue0;
                    RAC_UpdateRadioStateMachine();
                }
                break;
            }
            default:
                throw new Exception("Unreachable. Invalid PROTIMER RX Request state.");
            }

            switch(PROTIMER_txRequestState)
            {
            case Enumeration_X.EnumerationXValue0:
            {
                if(field_284.Value == PROTIMER_Event.Always)
                {
                    PROTIMER_txRequestState = Enumeration_X.EnumerationXValue1;
                    goto case Enumeration_X.EnumerationXValue1;
                }
                if(field_284.Value == ev)
                {
                    PROTIMER_txRequestState = Enumeration_X.EnumerationXValue1;
                }
                break;
            }
            case Enumeration_X.EnumerationXValue1:
            {
                if(field_285.Value == ev)
                {
                    PROTIMER_txRequestState = Enumeration_X.EnumerationXValue2;
                    PROTIMER_TxEnable = true;
                    goto case Enumeration_X.EnumerationXValue2;
                }
                break;
            }
            case Enumeration_X.EnumerationXValue2:
            {
                PROTIMER_TxEnable = false;
                PROTIMER_txRequestState = Enumeration_X.EnumerationXValue0;
                break;
            }
            default:
                throw new Exception("Unreachable. Invalid PROTIMER TX Request state.");
            }
        }

        public void PROTIMER_HandleChangedParams()
        {
            if(!PROTIMER_Enabled)
            {
                return;
            }

            TrySyncTime();
            uint currentIncrement = (uint)proTimer.Value;
            proTimer.Enabled = false;
            proTimer.Value = 0;

            if(currentIncrement > 0)
            {
                PROTIMER_HandlePreCntOverflows(currentIncrement);
            }

            proTimer.Limit = PROTIMER_ComputeTimerLimit();
            proTimer.Enabled = true;
        }

        public void ReceiveFrame(byte[] frame, IRadio sender)
        {
            TimeInterval txStartTime = InterferenceQueue.GetTxStartTime(sender);
            if(txStartTime == TimeInterval.Empty)
            {
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "ReceiveFrame() at {0} on channel {1} ({2}): Dropping (TX was aborted)",
                         GetTime(), Channel, MODEM_GetCurrentPhy());
                return;
            }

            var txRxSimulatorDelayUs = (GetTime() - txStartTime).TotalMicroseconds;

            this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "ReceiveFrame() at {0} on channel {1} ({2}), TX started at {3} (diff: {4})",
                     GetTime(), Channel, MODEM_GetCurrentPhy(), txStartTime, txRxSimulatorDelayUs);

            if(RAC_internalRxState != Enumeration_Z.EnumerationZValue0)
            {
                RAC_ongoingRxCollided = true;
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping: (RX already ongoing)");
                return;
            }


            double delayUs = MODEM_GetTxChainDelayUs() + MODEM_GetPreambleOverTheAirTimeUs() + MODEM_GetSyncWordOverTheAirTimeUs();
            RAC_internalRxState = Enumeration_Z.EnumerationZValue1;
            field_160.Value = MODEM_DemodulatorState.FrameSearch;

            currentFrame = frame;

            AGC_FrameRssiIntegerPart = (sbyte)InterferenceQueue.GetCurrentRssi(this, MODEM_GetCurrentPhy(), Channel);

            if(delayUs > txRxSimulatorDelayUs && PROTIMER_UsToPreCntOverflowTicks(delayUs - txRxSimulatorDelayUs) > 0)
            {
                RAC_rxTimeAlreadyPassedUs = 0;
                rxTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
                rxTimer.Limit = PROTIMER_UsToPreCntOverflowTicks(delayUs - txRxSimulatorDelayUs);
                rxTimer.Enabled = true;
            }
            else
            {
                RAC_rxTimeAlreadyPassedUs = txRxSimulatorDelayUs - delayUs;
                RAC_RxTimerLimitReached();
            }
            FrcSnifferReceiveFrame(frame);
        }

        public uint PROTIMER_BaseCounterValue
        {
            get
            {
                ulong ret = PROTIMER_baseCounterValue;
                if(field_237.Value == Enumeration_P.EnumerationPValue1
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
                if(field_288.Value == Enumeration_Y.EnumerationYValue1
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

        public bool ForceBusyRssi
        {
            set
            {
                InterferenceQueue.ForceBusyRssi = value;
            }
        }

        public int Channel
        {
            get
            {
                return currentChannel;
            }

            set
            {
                currentChannel = value;
            }
        }

        public GPIO ModulatorAndDemodulatorIRQ { get; }

        public GPIO RadioControllerRadioStateMachineIRQ { get; }

        public GPIO ProtocolTimerIRQ { get; }

        public GPIO SynthesizerIRQ { get; }

        public GPIO AutomaticGainControlIRQ { get; }

        public GPIO FrameControllerIRQ { get; }

        public GPIO Lpw0PortalIRQ { get; }

        public GPIO RfTimerIRQ { get; }

        public GPIO FrameControllerPrioritizedIRQ { get; }

        public GPIO RadioControllerSequencerIRQ { get; }

        public GPIO SeqRadioControllerIRQ { get; }

        public GPIO SeqRxSearchIRQ { get; }

        public GPIO SeqRxWarmIRQ { get; }

        public GPIO SeqFrameControllerIRQ { get; }

        public GPIO SeqRxFrameIRQ { get; }

        public GPIO SeqShutdownIRQ { get; }

        public GPIO SeqTxWrapUpIRQ { get; }

        public GPIO SeqTxIRQ { get; }

        public GPIO SeqTxWarmIRQ { get; }

        public GPIO SeqModulatorAndDemodulatorIRQ { get; }

        public GPIO SeqRfMailboxIRQ { get; }

        public GPIO SeqHostPortalIRQ { get; }

        public GPIO SeqSynthesizerIRQ { get; }

        public GPIO SeqAutomaticGainControlIRQ { get; }

        public GPIO SeqProtocolTimerIRQ { get; }

        public GPIO SeqOffIRQ { get; }

        public GPIO SeqRxWrapUpIRQ { get; }

        public event Action<IRadio, byte[]> FrameSent;

        public event Action<uint> PreCountOverflowsEvent;

        public event Action<uint> BaseCountOverflowsEvent;

        public event Action<uint> WrapCountOverflowsEvent;

        public event Action<uint> CaptureCompareEvent;

        public event Action PtiFrameComplete;

        public event Action<byte[]> PtiDataOut;

        public event Action<SiLabs_PacketTraceFrameType> PtiFrameStart;

        public bool LogBasicRadioActivityAsError = false;
        public bool LogInterrupts = false;
        public bool LogRegisterAccess = false;

        private void Write<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, uint value)
        where T : struct, IComparable, IFormattable
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var internal_offset = offset;
                var internal_value = value;

                if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
                {
                    internal_offset = offset - SetRegisterOffset;
                    var old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value | value;
                    if(LogRegisterAccess)
                    {
                    }
                }
                else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
                {
                    internal_offset = offset - ClearRegisterOffset;
                    var old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value & ~value;
                    if(LogRegisterAccess)
                    {
                    }
                }
                else if(offset >= ToggleRegisterOffset)
                {
                    internal_offset = offset - ToggleRegisterOffset;
                    var old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value ^ value;
                    if(LogRegisterAccess)
                    {
                    }
                }

                if(LogRegisterAccess)
                {
                }
                if(!registersCollection.TryWrite(internal_offset, internal_value) && LogRegisterAccess)
                {
                    return;
                }
            });
        }

        private byte ReadByte<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset)
        where T : struct, IComparable, IFormattable
        {
            var byteOffset = (int)(offset & 0x3);
            var registerValue = Read<T>(registersCollection, regionName, offset - byteOffset, true);
            var result = (byte)((registerValue >> byteOffset*8) & 0xFF);
            return result;
        }

        private uint Read<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, bool internal_read = false)
        where T : struct, IComparable, IFormattable
        {
            var result = 0U;
            var internal_offset = offset;

            if(offset >= SetRegisterOffset && offset < ClearRegisterOffset)
            {
                internal_offset = offset - SetRegisterOffset;
                if(LogRegisterAccess && !internal_read)
                {
                }
            }
            else if(offset >= ClearRegisterOffset && offset < ToggleRegisterOffset)
            {
                internal_offset = offset - ClearRegisterOffset;
                if(LogRegisterAccess && !internal_read)
                {
                }
            }
            else if(offset >= ToggleRegisterOffset)
            {
                internal_offset = offset - ToggleRegisterOffset;
                if(LogRegisterAccess && !internal_read)
                {
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
                if(LogRegisterAccess && !internal_read)
                {
                }
            }

            if(LogRegisterAccess && !internal_read)
            {
            }

            return 0;
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

        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;

        private DoubleWordRegisterCollection BuildLpw0PortalRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_F.RegF_2, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => LPW0PORTAL_PowerUpRequest, writeCallback: (_, value) => {LPW0PORTAL_PowerUpRequest = value;}, name: "REG_F_2_FIELD_1")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers_F.RegF_3, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => LPW0PORTAL_PowerUpAck, name: "REG_F_3_FIELD_1")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers_F.RegF_6, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[0], name: "REG_F_6_FIELD_1")
                },
                {(long)Registers_F.RegF_7, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[1], name: "REG_F_7_FIELD_1")
                },
                {(long)Registers_F.RegF_8, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[2], name: "REG_F_8_FIELD_1")
                },
                {(long)Registers_F.RegF_9, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[3], name: "REG_F_9_FIELD_1")
                },
                {(long)Registers_F.RegF_10, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[4], name: "REG_F_10_FIELD_1")
                },
                {(long)Registers_F.RegF_11, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[5], name: "REG_F_11_FIELD_1")
                },
                {(long)Registers_F.RegF_12, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[6], name: "REG_F_12_FIELD_1")
                },
                {(long)Registers_F.RegF_13, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_157[7], name: "REG_F_13_FIELD_1")
                },
                {(long)Registers_F.RegF_4, new DoubleWordRegister(this)
                    .WithFlag(0, out field_155[0], name: "REG_F_4_FIELD_1")
                    .WithFlag(1, out field_155[1], name: "REG_F_4_FIELD_2")
                    .WithFlag(2, out field_155[2], name: "REG_F_4_FIELD_3")
                    .WithFlag(3, out field_155[3], name: "REG_F_4_FIELD_4")
                    .WithFlag(4, out field_155[4], name: "REG_F_4_FIELD_5")
                    .WithFlag(5, out field_155[5], name: "REG_F_4_FIELD_6")
                    .WithFlag(6, out field_155[6], name: "REG_F_4_FIELD_7")
                    .WithFlag(7, out field_155[7], name: "REG_F_4_FIELD_8")
                    .WithFlag(8, out field_155[8], name: "REG_F_4_FIELD_9")
                    .WithFlag(9, out field_155[9], name: "REG_F_4_FIELD_10")
                    .WithFlag(10, out field_155[10], name: "REG_F_4_FIELD_11")
                    .WithFlag(11, out field_155[11], name: "REG_F_4_FIELD_12")
                    .WithFlag(12, out field_155[12], name: "REG_F_4_FIELD_13")
                    .WithFlag(13, out field_155[13], name: "REG_F_4_FIELD_14")
                    .WithFlag(14, out field_155[14], name: "REG_F_4_FIELD_15")
                    .WithFlag(15, out field_155[15], name: "REG_F_4_FIELD_16")
                    .WithFlag(16, out field_155[16], name: "REG_F_4_FIELD_17")
                    .WithFlag(17, out field_155[17], name: "REG_F_4_FIELD_18")
                    .WithFlag(18, out field_155[18], name: "REG_F_4_FIELD_19")
                    .WithFlag(19, out field_155[19], name: "REG_F_4_FIELD_20")
                    .WithFlag(20, out field_155[20], name: "REG_F_4_FIELD_21")
                    .WithFlag(21, out field_155[21], name: "REG_F_4_FIELD_22")
                    .WithFlag(22, out field_155[22], name: "REG_F_4_FIELD_23")
                    .WithFlag(23, out field_155[23], name: "REG_F_4_FIELD_24")
                    .WithFlag(24, out field_155[24], name: "REG_F_4_FIELD_25")
                    .WithFlag(25, out field_155[25], name: "REG_F_4_FIELD_26")
                    .WithFlag(26, out field_155[26], name: "REG_F_4_FIELD_27")
                    .WithFlag(27, out field_155[27], name: "REG_F_4_FIELD_28")
                    .WithFlag(28, out field_155[28], name: "REG_F_4_FIELD_29")
                    .WithFlag(29, out field_155[29], name: "REG_F_4_FIELD_30")
                    .WithFlag(30, out field_155[30], name: "REG_F_4_FIELD_31")
                    .WithFlag(31, out field_155[31], name: "REG_F_4_FIELD_32")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_F.RegF_5, new DoubleWordRegister(this)
                    .WithFlag(0, out field_156[0], name: "REG_F_5_FIELD_1")
                    .WithFlag(1, out field_156[1], name: "REG_F_5_FIELD_2")
                    .WithFlag(2, out field_156[2], name: "REG_F_5_FIELD_3")
                    .WithFlag(3, out field_156[3], name: "REG_F_5_FIELD_4")
                    .WithFlag(4, out field_156[4], name: "REG_F_5_FIELD_5")
                    .WithFlag(5, out field_156[5], name: "REG_F_5_FIELD_6")
                    .WithFlag(6, out field_156[6], name: "REG_F_5_FIELD_7")
                    .WithFlag(7, out field_156[7], name: "REG_F_5_FIELD_8")
                    .WithFlag(8, out field_156[8], name: "REG_F_5_FIELD_9")
                    .WithFlag(9, out field_156[9], name: "REG_F_5_FIELD_10")
                    .WithFlag(10, out field_156[10], name: "REG_F_5_FIELD_11")
                    .WithFlag(11, out field_156[11], name: "REG_F_5_FIELD_12")
                    .WithFlag(12, out field_156[12], name: "REG_F_5_FIELD_13")
                    .WithFlag(13, out field_156[13], name: "REG_F_5_FIELD_14")
                    .WithFlag(14, out field_156[14], name: "REG_F_5_FIELD_15")
                    .WithFlag(15, out field_156[15], name: "REG_F_5_FIELD_16")
                    .WithFlag(16, out field_156[16], name: "REG_F_5_FIELD_17")
                    .WithFlag(17, out field_156[17], name: "REG_F_5_FIELD_18")
                    .WithFlag(18, out field_156[18], name: "REG_F_5_FIELD_19")
                    .WithFlag(19, out field_156[19], name: "REG_F_5_FIELD_20")
                    .WithFlag(20, out field_156[20], name: "REG_F_5_FIELD_21")
                    .WithFlag(21, out field_156[21], name: "REG_F_5_FIELD_22")
                    .WithFlag(22, out field_156[22], name: "REG_F_5_FIELD_23")
                    .WithFlag(23, out field_156[23], name: "REG_F_5_FIELD_24")
                    .WithFlag(24, out field_156[24], name: "REG_F_5_FIELD_25")
                    .WithFlag(25, out field_156[25], name: "REG_F_5_FIELD_26")
                    .WithFlag(26, out field_156[26], name: "REG_F_5_FIELD_27")
                    .WithFlag(27, out field_156[27], name: "REG_F_5_FIELD_28")
                    .WithFlag(28, out field_156[28], name: "REG_F_5_FIELD_29")
                    .WithFlag(29, out field_156[29], name: "REG_F_5_FIELD_30")
                    .WithFlag(30, out field_156[30], name: "REG_F_5_FIELD_31")
                    .WithFlag(31, out field_156[31], name: "REG_F_5_FIELD_32")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildHostPortalRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_E.RegE_2, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => HOSTPORTAL_PowerUpRequest, writeCallback: (_, value) => {HOSTPORTAL_PowerUpRequest = value;}, name: "REG_E_2_FIELD_1")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers_E.RegE_3, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => HOSTPORTAL_PowerUpAck, name: "REG_E_3_FIELD_1")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers_E.RegE_6, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[0], name: "REG_E_6_FIELD_1")
                },
                {(long)Registers_E.RegE_7, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[1], name: "REG_E_7_FIELD_1")
                },
                {(long)Registers_E.RegE_8, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[2], name: "REG_E_8_FIELD_1")
                },
                {(long)Registers_E.RegE_9, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[3], name: "REG_E_9_FIELD_1")
                },
                {(long)Registers_E.RegE_10, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[4], name: "REG_E_10_FIELD_1")
                },
                {(long)Registers_E.RegE_11, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[5], name: "REG_E_11_FIELD_1")
                },
                {(long)Registers_E.RegE_12, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[6], name: "REG_E_12_FIELD_1")
                },
                {(long)Registers_E.RegE_13, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_151[7], name: "REG_E_13_FIELD_1")
                },
                {(long)Registers_E.RegE_4, new DoubleWordRegister(this)
                    .WithFlag(0, out field_149[0], name: "REG_E_4_FIELD_1")
                    .WithFlag(1, out field_149[1], name: "REG_E_4_FIELD_2")
                    .WithFlag(2, out field_149[2], name: "REG_E_4_FIELD_3")
                    .WithFlag(3, out field_149[3], name: "REG_E_4_FIELD_4")
                    .WithFlag(4, out field_149[4], name: "REG_E_4_FIELD_5")
                    .WithFlag(5, out field_149[5], name: "REG_E_4_FIELD_6")
                    .WithFlag(6, out field_149[6], name: "REG_E_4_FIELD_7")
                    .WithFlag(7, out field_149[7], name: "REG_E_4_FIELD_8")
                    .WithFlag(8, out field_149[8], name: "REG_E_4_FIELD_9")
                    .WithFlag(9, out field_149[9], name: "REG_E_4_FIELD_10")
                    .WithFlag(10, out field_149[10], name: "REG_E_4_FIELD_11")
                    .WithFlag(11, out field_149[11], name: "REG_E_4_FIELD_12")
                    .WithFlag(12, out field_149[12], name: "REG_E_4_FIELD_13")
                    .WithFlag(13, out field_149[13], name: "REG_E_4_FIELD_14")
                    .WithFlag(14, out field_149[14], name: "REG_E_4_FIELD_15")
                    .WithFlag(15, out field_149[15], name: "REG_E_4_FIELD_16")
                    .WithFlag(16, out field_149[16], name: "REG_E_4_FIELD_17")
                    .WithFlag(17, out field_149[17], name: "REG_E_4_FIELD_18")
                    .WithFlag(18, out field_149[18], name: "REG_E_4_FIELD_19")
                    .WithFlag(19, out field_149[19], name: "REG_E_4_FIELD_20")
                    .WithFlag(20, out field_149[20], name: "REG_E_4_FIELD_21")
                    .WithFlag(21, out field_149[21], name: "REG_E_4_FIELD_22")
                    .WithFlag(22, out field_149[22], name: "REG_E_4_FIELD_23")
                    .WithFlag(23, out field_149[23], name: "REG_E_4_FIELD_24")
                    .WithFlag(24, out field_149[24], name: "REG_E_4_FIELD_25")
                    .WithFlag(25, out field_149[25], name: "REG_E_4_FIELD_26")
                    .WithFlag(26, out field_149[26], name: "REG_E_4_FIELD_27")
                    .WithFlag(27, out field_149[27], name: "REG_E_4_FIELD_28")
                    .WithFlag(28, out field_149[28], name: "REG_E_4_FIELD_29")
                    .WithFlag(29, out field_149[29], name: "REG_E_4_FIELD_30")
                    .WithFlag(30, out field_149[30], name: "REG_E_4_FIELD_31")
                    .WithFlag(31, out field_149[31], name: "REG_E_4_FIELD_32")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_E.RegE_5, new DoubleWordRegister(this)
                    .WithFlag(0, out field_150[0], name: "REG_E_5_FIELD_1")
                    .WithFlag(1, out field_150[1], name: "REG_E_5_FIELD_2")
                    .WithFlag(2, out field_150[2], name: "REG_E_5_FIELD_3")
                    .WithFlag(3, out field_150[3], name: "REG_E_5_FIELD_4")
                    .WithFlag(4, out field_150[4], name: "REG_E_5_FIELD_5")
                    .WithFlag(5, out field_150[5], name: "REG_E_5_FIELD_6")
                    .WithFlag(6, out field_150[6], name: "REG_E_5_FIELD_7")
                    .WithFlag(7, out field_150[7], name: "REG_E_5_FIELD_8")
                    .WithFlag(8, out field_150[8], name: "REG_E_5_FIELD_9")
                    .WithFlag(9, out field_150[9], name: "REG_E_5_FIELD_10")
                    .WithFlag(10, out field_150[10], name: "REG_E_5_FIELD_11")
                    .WithFlag(11, out field_150[11], name: "REG_E_5_FIELD_12")
                    .WithFlag(12, out field_150[12], name: "REG_E_5_FIELD_13")
                    .WithFlag(13, out field_150[13], name: "REG_E_5_FIELD_14")
                    .WithFlag(14, out field_150[14], name: "REG_E_5_FIELD_15")
                    .WithFlag(15, out field_150[15], name: "REG_E_5_FIELD_16")
                    .WithFlag(16, out field_150[16], name: "REG_E_5_FIELD_17")
                    .WithFlag(17, out field_150[17], name: "REG_E_5_FIELD_18")
                    .WithFlag(18, out field_150[18], name: "REG_E_5_FIELD_19")
                    .WithFlag(19, out field_150[19], name: "REG_E_5_FIELD_20")
                    .WithFlag(20, out field_150[20], name: "REG_E_5_FIELD_21")
                    .WithFlag(21, out field_150[21], name: "REG_E_5_FIELD_22")
                    .WithFlag(22, out field_150[22], name: "REG_E_5_FIELD_23")
                    .WithFlag(23, out field_150[23], name: "REG_E_5_FIELD_24")
                    .WithFlag(24, out field_150[24], name: "REG_E_5_FIELD_25")
                    .WithFlag(25, out field_150[25], name: "REG_E_5_FIELD_26")
                    .WithFlag(26, out field_150[26], name: "REG_E_5_FIELD_27")
                    .WithFlag(27, out field_150[27], name: "REG_E_5_FIELD_28")
                    .WithFlag(28, out field_150[28], name: "REG_E_5_FIELD_29")
                    .WithFlag(29, out field_150[29], name: "REG_E_5_FIELD_30")
                    .WithFlag(30, out field_150[30], name: "REG_E_5_FIELD_31")
                    .WithFlag(31, out field_150[31], name: "REG_E_5_FIELD_32")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildRfMailboxRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_J.RegJ_1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_362[0], name: "REG_J_1_FIELD_1")
                },
                {(long)Registers_J.RegJ_2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_362[1], name: "REG_J_2_FIELD_1")
                },
                {(long)Registers_J.RegJ_3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_362[2], name: "REG_J_3_FIELD_1")
                },
                {(long)Registers_J.RegJ_4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_362[3], name: "REG_J_4_FIELD_1")
                },
                {(long)Registers_J.RegJ_5, new DoubleWordRegister(this)
                    .WithFlag(0, out field_360[0], name: "REG_J_5_FIELD_1")
                    .WithFlag(1, out field_360[1], name: "REG_J_5_FIELD_2")
                    .WithFlag(2, out field_360[2], name: "REG_J_5_FIELD_3")
                    .WithFlag(3, out field_360[3], name: "REG_J_5_FIELD_4")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_J.RegJ_6, new DoubleWordRegister(this)
                    .WithFlag(0, out field_361[0], name: "REG_J_6_FIELD_1")
                    .WithFlag(1, out field_361[1], name: "REG_J_6_FIELD_2")
                    .WithFlag(2, out field_361[2], name: "REG_J_6_FIELD_3")
                    .WithFlag(3, out field_361[3], name: "REG_J_6_FIELD_4")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildFswMailboxRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_J.RegJ_1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_148[0], name: "REG_J_1_FIELD_1")
                },
                {(long)Registers_J.RegJ_2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_148[1], name: "REG_J_2_FIELD_1")
                },
                {(long)Registers_J.RegJ_3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_148[2], name: "REG_J_3_FIELD_1")
                },
                {(long)Registers_J.RegJ_4, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_148[3], name: "REG_J_4_FIELD_1")
                },
                {(long)Registers_J.RegJ_5, new DoubleWordRegister(this)
                    .WithFlag(0, out field_146[0], name: "REG_J_5_FIELD_1")
                    .WithFlag(1, out field_146[1], name: "REG_J_5_FIELD_2")
                    .WithFlag(2, out field_146[2], name: "REG_J_5_FIELD_3")
                    .WithFlag(3, out field_146[3], name: "REG_J_5_FIELD_4")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_J.RegJ_6, new DoubleWordRegister(this)
                    .WithFlag(0, out field_147[0], name: "REG_J_6_FIELD_1")
                    .WithFlag(1, out field_147[1], name: "REG_J_6_FIELD_2")
                    .WithFlag(2, out field_147[2], name: "REG_J_6_FIELD_3")
                    .WithFlag(3, out field_147[3], name: "REG_J_6_FIELD_4")
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
                {(long)Registers_A.RegA_3, new DoubleWordRegister(this)
                    .WithTag("REG_A_3_FIELD_1", 0, 6)
                    .WithTaggedFlag("REG_A_3_FIELD_2", 6)
                    .WithTaggedFlag("REG_A_3_FIELD_3", 7)
                    .WithTaggedFlag("REG_A_3_FIELD_4", 8)
                    .WithTaggedFlag("REG_A_3_FIELD_5", 9)
                    .WithFlag(10, out field_1, FieldMode.Read, name: "REG_A_3_FIELD_6")
                    .WithTaggedFlag("REG_A_3_FIELD_7", 11)
                    .WithTag("REG_A_3_FIELD_8", 12, 4)
                    .WithTag("REG_A_3_FIELD_9", 16, 4)
                    .WithTag("REG_A_3_FIELD_10", 20, 5)
                    .WithTag("REG_A_3_FIELD_11", 25, 2)
                    .WithReservedBits(27, 5)
                },
                {(long)Registers_A.RegA_4, new DoubleWordRegister(this)
                    .WithTag("REG_A_4_FIELD_1", 0, 8)
                    .WithReservedBits(8, 1)
                    .WithTag("REG_A_4_FIELD_2", 9, 4)
                    .WithTag("REG_A_4_FIELD_3", 13, 2)
                    .WithEnumField<DoubleWordRegister, Enumeration_C>(15, 3, out field_23, name: "REG_A_4_FIELD_4")
                    .WithTag("REG_A_4_FIELD_5", 18, 12)
                    .WithReservedBits(30, 2)
                },
                {(long)Registers_A.RegA_18, new DoubleWordRegister(this)
                  .WithFlag(0, out field_24, name: "REG_A_18_FIELD_1")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out field_2, name: "REG_A_18_FIELD_2")
                  .WithTaggedFlag("REG_A_18_FIELD_3", 3)
                  .WithTaggedFlag("REG_A_18_FIELD_4", 4)
                  .WithFlag(5, out field_13, name: "REG_A_18_FIELD_5")
                  .WithTaggedFlag("REG_A_18_FIELD_6", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("REG_A_18_FIELD_7", 8)
                  .WithTaggedFlag("REG_A_18_FIELD_8", 9)
                  .WithFlag(10, out field_15, name: "REG_A_18_FIELD_9")
                  .WithFlag(11, out field_18, name: "REG_A_18_FIELD_10")
                  .WithFlag(12, out field_6, name: "REG_A_18_FIELD_11")
                  .WithTaggedFlag("REG_A_18_FIELD_12", 13)
                  .WithTaggedFlag("REG_A_18_FIELD_13", 14)
                  .WithTaggedFlag("REG_A_18_FIELD_14", 15)
                  .WithTaggedFlag("REG_A_18_FIELD_15", 16)
                  .WithTaggedFlag("REG_A_18_FIELD_16", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)Registers_A.RegA_19, new DoubleWordRegister(this)
                  .WithFlag(0, out field_25, name: "REG_A_19_FIELD_1")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out field_3, name: "REG_A_19_FIELD_2")
                  .WithTaggedFlag("REG_A_19_FIELD_3", 3)
                  .WithTaggedFlag("REG_A_19_FIELD_4", 4)
                  .WithFlag(5, out field_14, name: "REG_A_19_FIELD_5")
                  .WithTaggedFlag("REG_A_19_FIELD_6", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("REG_A_19_FIELD_7", 8)
                  .WithTaggedFlag("REG_A_19_FIELD_8", 9)
                  .WithFlag(10, out field_16, name: "REG_A_19_FIELD_9")
                  .WithFlag(11, out field_19, name: "REG_A_19_FIELD_10")
                  .WithFlag(12, out field_7, name: "REG_A_19_FIELD_11")
                  .WithTaggedFlag("REG_A_19_FIELD_12", 13)
                  .WithTaggedFlag("REG_A_19_FIELD_13", 14)
                  .WithTaggedFlag("REG_A_19_FIELD_14", 15)
                  .WithTaggedFlag("REG_A_19_FIELD_15", 16)
                  .WithTaggedFlag("REG_A_19_FIELD_16", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)Registers_A.RegA_44, new DoubleWordRegister(this)
                  .WithFlag(0, out field_36, name: "REG_A_44_FIELD_1")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out field_26, name: "REG_A_44_FIELD_2")
                  .WithTaggedFlag("REG_A_44_FIELD_3", 3)
                  .WithTaggedFlag("REG_A_44_FIELD_4", 4)
                  .WithFlag(5, out field_30, name: "REG_A_44_FIELD_5")
                  .WithTaggedFlag("REG_A_44_FIELD_6", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("REG_A_44_FIELD_7", 8)
                  .WithTaggedFlag("REG_A_44_FIELD_8", 9)
                  .WithFlag(10, out field_32, name: "REG_A_44_FIELD_9")
                  .WithFlag(11, out field_34, name: "REG_A_44_FIELD_10")
                  .WithFlag(12, out field_28, name: "REG_A_44_FIELD_11")
                  .WithTaggedFlag("REG_A_44_FIELD_12", 13)
                  .WithTaggedFlag("REG_A_44_FIELD_13", 14)
                  .WithTaggedFlag("REG_A_44_FIELD_14", 15)
                  .WithTaggedFlag("REG_A_44_FIELD_15", 16)
                  .WithTaggedFlag("REG_A_44_FIELD_16", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)Registers_A.RegA_45, new DoubleWordRegister(this)
                  .WithFlag(0, out field_37, name: "REG_A_45_FIELD_1")
                  .WithReservedBits(1, 1)
                  .WithFlag(2, out field_27, name: "REG_A_45_FIELD_2")
                  .WithTaggedFlag("REG_A_45_FIELD_3", 3)
                  .WithTaggedFlag("REG_A_45_FIELD_4", 4)
                  .WithFlag(5, out field_31, name: "REG_A_45_FIELD_5")
                  .WithTaggedFlag("REG_A_45_FIELD_6", 6)
                  .WithReservedBits(7, 1)
                  .WithTaggedFlag("REG_A_45_FIELD_7", 8)
                  .WithTaggedFlag("REG_A_45_FIELD_8", 9)
                  .WithFlag(10, out field_33, name: "REG_A_45_FIELD_9")
                  .WithFlag(11, out field_35, name: "REG_A_45_FIELD_10")
                  .WithFlag(12, out field_29, name: "REG_A_45_FIELD_11")
                  .WithTaggedFlag("REG_A_45_FIELD_12", 13)
                  .WithTaggedFlag("REG_A_45_FIELD_13", 14)
                  .WithTaggedFlag("REG_A_45_FIELD_14", 15)
                  .WithTaggedFlag("REG_A_45_FIELD_15", 16)
                  .WithTaggedFlag("REG_A_45_FIELD_16", 17)
                  .WithReservedBits(18, 14)
                },
                {(long)Registers_A.RegA_42, new DoubleWordRegister(this)
                  .WithValueField(0, 4, out field_8, name: "REG_A_42_FIELD_1")
                  .WithFlag(4, out field_9, name: "REG_A_42_FIELD_2")
                  .WithTaggedFlag("REG_A_42_FIELD_3", 5)
                  .WithTaggedFlag("REG_A_42_FIELD_4", 6)
                  .WithReservedBits(7, 25)
                },
                {(long)Registers_A.RegA_8, new DoubleWordRegister(this, 0x2132727F)
                    .WithTag("REG_A_8_FIELD_1", 0, 8)
                    .WithTag("REG_A_8_FIELD_2", 8, 3)
                    .WithValueField(11, 8, out field_22, name: "REG_A_8_FIELD_3")
                    .WithTaggedFlag("REG_A_8_FIELD_4", 19)
                    .WithTaggedFlag("REG_A_8_FIELD_5", 20)
                    .WithTaggedFlag("REG_A_8_FIELD_6", 21)
                    .WithTaggedFlag("REG_A_8_FIELD_7", 22)
                    .WithTaggedFlag("REG_A_8_FIELD_8", 23)
                    .WithTaggedFlag("REG_A_8_FIELD_9", 24)
                    .WithTag("REG_A_8_FIELD_10", 25, 2)
                    .WithTaggedFlag("REG_A_8_FIELD_11", 27)
                    .WithTaggedFlag("REG_A_8_FIELD_12", 28)
                    .WithTaggedFlag("REG_A_8_FIELD_13", 29)
                    .WithTaggedFlag("REG_A_8_FIELD_14", 30)
                    .WithTaggedFlag("REG_A_8_FIELD_15", 31)
                },
                {(long)Registers_A.RegA_9, new DoubleWordRegister(this, 0x00001300)
                  .WithValueField(0, 8, out field_11, name: "REG_A_9_FIELD_1")
                  .WithValueField(8, 4, out field_21, name: "REG_A_9_FIELD_2")
                  .WithValueField(12, 3, out field_12, name: "REG_A_9_FIELD_3")
                  .WithEnumField<DoubleWordRegister, Enumeration_A>(15, 2, out field_4, name: "REG_A_9_FIELD_4")
                  .WithEnumField<DoubleWordRegister, Enumeration_B>(17, 1, out field_5, name: "REG_A_9_FIELD_5")
                  .WithFlag(18, out field_10, name: "REG_A_9_FIELD_6")
                  .WithTaggedFlag("REG_A_9_FIELD_7", 19)
                  .WithTaggedFlag("REG_A_9_FIELD_8", 20)
                  .WithTag("REG_A_9_FIELD_9", 21, 4)
                  .WithTaggedFlag("REG_A_9_FIELD_10", 25)
                  .WithTaggedFlag("REG_A_9_FIELD_11", 26)
                  .WithReservedBits(27, 1)
                  .WithTag("REG_A_9_FIELD_12", 28, 3)
                  .WithTaggedFlag("REG_A_9_FIELD_13", 31)
                },
                {(long)Registers_A.RegA_15, new DoubleWordRegister(this)
                  .WithTag("REG_A_15_FIELD_1", 0, 8)
                  .WithValueField(8, 8, out field_39, name: "REG_A_15_FIELD_2")
                  .WithTag("REG_A_15_FIELD_3", 16, 8)
                  .WithFlag(24, out field_38, name: "REG_A_15_FIELD_4")
                  .WithReservedBits(25, 7)
                },
                {(long)Registers_A.RegA_6, new DoubleWordRegister(this, 0x00008000)
                  .WithReservedBits(0, 6)
                  .WithValueField(6, 2, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_RssiFractionalPart, name: "REG_A_6_FIELD_1")
                  .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_RssiIntegerPartAdjusted, name: "REG_A_6_FIELD_2")
                  .WithReservedBits(16, 16)
                },
                {(long)Registers_A.RegA_7, new DoubleWordRegister(this, 0x00008000)
                  .WithReservedBits(0, 6)
                  .WithValueField(6, 2, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_FrameRssiFractionalPart, name: "REG_A_7_FIELD_1")
                  .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => (uint)AGC_FrameRssiIntegerPartAdjusted, name: "REG_A_7_FIELD_2")
                  .WithReservedBits(16, 16)
                },
                {(long)Registers_A.RegA_20, new DoubleWordRegister(this)
                  .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) {AGC_RssiStartCommand();} }, name: "REG_A_20_FIELD_1")
                  .WithReservedBits(1, 31)
                },
                {(long)Registers_A.RegA_46, new DoubleWordRegister(this)
                  .WithValueField(0, 8, out field_17, name: "REG_A_46_FIELD_1")
                  .WithValueField(8, 8, out field_20, name: "REG_A_46_FIELD_2")
                  .WithTag("REG_A_46_FIELD_3", 16, 8)
                  .WithTag("REG_A_46_FIELD_4", 24, 8)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildModulatorAndDemodulatorRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_G.RegG_3, new DoubleWordRegister(this, 0x00000008)
                    .WithFlag(0, out field_219, name: "REG_G_3_FIELD_1")
                    .WithFlag(1, out field_226, name: "REG_G_3_FIELD_2")
                    .WithFlag(2, out field_222, name: "REG_G_3_FIELD_3")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => MODEM_TxRampingDoneInterrupt, name: "REG_G_3_FIELD_4")
                    .WithFlag(4, out field_182, name: "REG_G_3_FIELD_5")
                    .WithTaggedFlag("REG_G_3_FIELD_6", 5)
                    .WithTaggedFlag("REG_G_3_FIELD_7", 6)
                    .WithTaggedFlag("REG_G_3_FIELD_8", 7)
                    .WithTaggedFlag("REG_G_3_FIELD_9", 8)
                    .WithFlag(9, out field_186, name: "REG_G_3_FIELD_10")
                    .WithFlag(10, out field_178, name: "REG_G_3_FIELD_11")
                    .WithFlag(11, out field_180, name: "REG_G_3_FIELD_12")
                    .WithTaggedFlag("REG_G_3_FIELD_13", 12)
                    .WithFlag(13, out field_188, name: "REG_G_3_FIELD_14")
                    .WithTaggedFlag("REG_G_3_FIELD_15", 14)
                    .WithTaggedFlag("REG_G_3_FIELD_16", 15)
                    .WithFlag(16, out field_184, name: "REG_G_3_FIELD_17")
                    .WithTaggedFlag("REG_G_3_FIELD_18", 17)
                    .WithTaggedFlag("REG_G_3_FIELD_19", 18)
                    .WithTaggedFlag("REG_G_3_FIELD_20", 19)
                    .WithTaggedFlag("REG_G_3_FIELD_21", 20)
                    .WithTaggedFlag("REG_G_3_FIELD_22", 21)
                    .WithTaggedFlag("REG_G_3_FIELD_23", 22)
                    .WithTaggedFlag("REG_G_3_FIELD_24", 23)
                    .WithTaggedFlag("REG_G_3_FIELD_25", 24)
                    .WithTaggedFlag("REG_G_3_FIELD_26", 25)
                    .WithTaggedFlag("REG_G_3_FIELD_27", 26)
                    .WithTaggedFlag("REG_G_3_FIELD_28", 27)
                    .WithTaggedFlag("REG_G_3_FIELD_29", 28)
                    .WithTaggedFlag("REG_G_3_FIELD_30", 29)
                    .WithTaggedFlag("REG_G_3_FIELD_31", 30)
                    .WithTaggedFlag("REG_G_3_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_4, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_G_4_FIELD_1", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_5, new DoubleWordRegister(this)
                    .WithFlag(0, out field_220, name: "REG_G_5_FIELD_1")
                    .WithFlag(1, out field_227, name: "REG_G_5_FIELD_2")
                    .WithFlag(2, out field_223, name: "REG_G_5_FIELD_3")
                    .WithFlag(3, out field_224, name: "REG_G_5_FIELD_4")
                    .WithFlag(4, out field_183, name: "REG_G_5_FIELD_5")
                    .WithTaggedFlag("REG_G_5_FIELD_6", 5)
                    .WithTaggedFlag("REG_G_5_FIELD_7", 6)
                    .WithTaggedFlag("REG_G_5_FIELD_8", 7)
                    .WithTaggedFlag("REG_G_5_FIELD_9", 8)
                    .WithFlag(9, out field_187, name: "REG_G_5_FIELD_10")
                    .WithFlag(10, out field_179, name: "REG_G_5_FIELD_11")
                    .WithFlag(11, out field_181, name: "REG_G_5_FIELD_12")
                    .WithTaggedFlag("REG_G_5_FIELD_13", 12)
                    .WithFlag(13, out field_189, name: "REG_G_5_FIELD_14")
                    .WithTaggedFlag("REG_G_5_FIELD_15", 14)
                    .WithTaggedFlag("REG_G_5_FIELD_16", 15)
                    .WithFlag(16, out field_185, name: "REG_G_5_FIELD_17")
                    .WithTaggedFlag("REG_G_5_FIELD_18", 17)
                    .WithTaggedFlag("REG_G_5_FIELD_19", 18)
                    .WithTaggedFlag("REG_G_5_FIELD_20", 19)
                    .WithTaggedFlag("REG_G_5_FIELD_21", 20)
                    .WithTaggedFlag("REG_G_5_FIELD_22", 21)
                    .WithTaggedFlag("REG_G_5_FIELD_23", 22)
                    .WithTaggedFlag("REG_G_5_FIELD_24", 23)
                    .WithTaggedFlag("REG_G_5_FIELD_25", 24)
                    .WithTaggedFlag("REG_G_5_FIELD_26", 25)
                    .WithTaggedFlag("REG_G_5_FIELD_27", 26)
                    .WithTaggedFlag("REG_G_5_FIELD_28", 27)
                    .WithTaggedFlag("REG_G_5_FIELD_29", 28)
                    .WithTaggedFlag("REG_G_5_FIELD_30", 29)
                    .WithTaggedFlag("REG_G_5_FIELD_31", 30)
                    .WithTaggedFlag("REG_G_5_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_6, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_G_6_FIELD_1", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_7, new DoubleWordRegister(this, 0x00000008)
                    .WithFlag(0, out field_202, name: "REG_G_7_FIELD_1")
                    .WithFlag(1, out field_207, name: "REG_G_7_FIELD_2")
                    .WithFlag(2, out field_204, name: "REG_G_7_FIELD_3")
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => MODEM_TxRampingDoneInterrupt, name: "REG_G_7_FIELD_4")
                    .WithFlag(4, out field_194, name: "REG_G_7_FIELD_5")
                    .WithTaggedFlag("REG_G_7_FIELD_6", 5)
                    .WithTaggedFlag("REG_G_7_FIELD_7", 6)
                    .WithTaggedFlag("REG_G_7_FIELD_8", 7)
                    .WithTaggedFlag("REG_G_7_FIELD_9", 8)
                    .WithFlag(9, out field_198, name: "REG_G_7_FIELD_10")
                    .WithFlag(10, out field_190, name: "REG_G_7_FIELD_11")
                    .WithFlag(11, out field_192, name: "REG_G_7_FIELD_12")
                    .WithTaggedFlag("REG_G_7_FIELD_13", 12)
                    .WithFlag(13, out field_200, name: "REG_G_7_FIELD_14")
                    .WithTaggedFlag("REG_G_7_FIELD_15", 14)
                    .WithTaggedFlag("REG_G_7_FIELD_16", 15)
                    .WithFlag(16, out field_196, name: "REG_G_7_FIELD_17")
                    .WithTaggedFlag("REG_G_7_FIELD_18", 17)
                    .WithTaggedFlag("REG_G_7_FIELD_19", 18)
                    .WithTaggedFlag("REG_G_7_FIELD_20", 19)
                    .WithTaggedFlag("REG_G_7_FIELD_21", 20)
                    .WithTaggedFlag("REG_G_7_FIELD_22", 21)
                    .WithTaggedFlag("REG_G_7_FIELD_23", 22)
                    .WithTaggedFlag("REG_G_7_FIELD_24", 23)
                    .WithTaggedFlag("REG_G_7_FIELD_25", 24)
                    .WithTaggedFlag("REG_G_7_FIELD_26", 25)
                    .WithTaggedFlag("REG_G_7_FIELD_27", 26)
                    .WithTaggedFlag("REG_G_7_FIELD_28", 27)
                    .WithTaggedFlag("REG_G_7_FIELD_29", 28)
                    .WithTaggedFlag("REG_G_7_FIELD_30", 29)
                    .WithTaggedFlag("REG_G_7_FIELD_31", 30)
                    .WithTaggedFlag("REG_G_7_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_8, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_G_8_FIELD_1", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_9, new DoubleWordRegister(this)
                    .WithFlag(0, out field_203, name: "REG_G_9_FIELD_1")
                    .WithFlag(1, out field_208, name: "REG_G_9_FIELD_2")
                    .WithFlag(2, out field_205, name: "REG_G_9_FIELD_3")
                    .WithFlag(3, out field_206, name: "REG_G_9_FIELD_4")
                    .WithFlag(4, out field_195, name: "REG_G_9_FIELD_5")
                    .WithTaggedFlag("REG_G_9_FIELD_6", 5)
                    .WithTaggedFlag("REG_G_9_FIELD_7", 6)
                    .WithTaggedFlag("REG_G_9_FIELD_8", 7)
                    .WithTaggedFlag("REG_G_9_FIELD_9", 8)
                    .WithFlag(9, out field_199, name: "REG_G_9_FIELD_10")
                    .WithFlag(10, out field_191, name: "REG_G_9_FIELD_11")
                    .WithFlag(11, out field_193, name: "REG_G_9_FIELD_12")
                    .WithTaggedFlag("REG_G_9_FIELD_13", 12)
                    .WithFlag(13, out field_201, name: "REG_G_9_FIELD_14")
                    .WithTaggedFlag("REG_G_9_FIELD_15", 14)
                    .WithTaggedFlag("REG_G_9_FIELD_16", 15)
                    .WithFlag(16, out field_197, name: "REG_G_9_FIELD_17")
                    .WithTaggedFlag("REG_G_9_FIELD_18", 17)
                    .WithTaggedFlag("REG_G_9_FIELD_19", 18)
                    .WithTaggedFlag("REG_G_9_FIELD_20", 19)
                    .WithTaggedFlag("REG_G_9_FIELD_21", 20)
                    .WithTaggedFlag("REG_G_9_FIELD_22", 21)
                    .WithTaggedFlag("REG_G_9_FIELD_23", 22)
                    .WithTaggedFlag("REG_G_9_FIELD_24", 23)
                    .WithTaggedFlag("REG_G_9_FIELD_25", 24)
                    .WithTaggedFlag("REG_G_9_FIELD_26", 25)
                    .WithTaggedFlag("REG_G_9_FIELD_27", 26)
                    .WithTaggedFlag("REG_G_9_FIELD_28", 27)
                    .WithTaggedFlag("REG_G_9_FIELD_29", 28)
                    .WithTaggedFlag("REG_G_9_FIELD_30", 29)
                    .WithTaggedFlag("REG_G_9_FIELD_31", 30)
                    .WithTaggedFlag("REG_G_9_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_10, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_G_10_FIELD_1", 0)
                    .WithReservedBits(1, 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_G.RegG_218, new DoubleWordRegister(this, 0x00000018)
                    .WithFlag(0, out field_221, name: "REG_G_218_FIELD_1")
                    .WithFlag(1, out field_159, name: "REG_G_218_FIELD_2")
                    .WithTaggedFlag("REG_G_218_FIELD_3", 2)
                    .WithTaggedFlag("REG_G_218_FIELD_4", 3)
                    .WithTaggedFlag("REG_G_218_FIELD_5", 4)
                    .WithTaggedFlag("REG_G_218_FIELD_6", 5)
                    .WithTaggedFlag("REG_G_218_FIELD_7", 6)
                    .WithReservedBits(7, 23)
                    .WithTaggedFlag("REG_G_218_FIELD_8", 30)
                    .WithTaggedFlag("REG_G_218_FIELD_9", 31)
                },
                {(long)Registers_G.RegG_24, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_G_24_FIELD_1", 0)
                    .WithTag("REG_G_24_FIELD_2", 1, 3)
                    .WithEnumField<DoubleWordRegister, Enumeration_O>(4, 2, out field_209, name: "REG_G_24_FIELD_3")
                    .WithEnumField<DoubleWordRegister, Enumeration_M>(6, 3, out field_167, name: "REG_G_24_FIELD_4")
                    .WithTaggedFlag("REG_G_24_FIELD_5", 9)
                    .WithTaggedFlag("REG_G_24_FIELD_6", 10)
                    .WithValueField(11, 5, out field_162, name: "REG_G_24_FIELD_7")
                    .WithValueField(16, 3, out field_163, name: "REG_G_24_FIELD_8")
                    .WithEnumField<DoubleWordRegister, Enumeration_K>(19, 2, out field_161, name: "REG_G_24_FIELD_9")
                    .WithTaggedFlag("REG_G_24_FIELD_10", 21)
                    .WithTag("REG_G_24_FIELD_11", 22, 3)
                    .WithTag("REG_G_24_FIELD_12", 25, 2)
                    .WithTag("REG_G_24_FIELD_13", 27, 3)
                    .WithTag("REG_G_24_FIELD_14", 30, 2)
                },
                {(long)Registers_G.RegG_25, new DoubleWordRegister(this)
                    .WithValueField(0, 5, out field_210, name: "REG_G_25_FIELD_1")
                    .WithTag("REG_G_25_FIELD_2", 5, 4)
                    .WithReservedBits(9, 1)
                    .WithFlag(10, out field_225, name: "REG_G_25_FIELD_3")
                    .WithFlag(11, out field_211, name: "REG_G_25_FIELD_4")
                    .WithTaggedFlag("REG_G_25_FIELD_5", 12)
                    .WithReservedBits(13, 1)
                    .WithTag("REG_G_25_FIELD_6", 14, 2)
                    .WithTag("REG_G_25_FIELD_7", 16, 4)
                    .WithTag("REG_G_25_FIELD_8", 20, 2)
                    .WithTag("REG_G_25_FIELD_9", 22, 3)
                    .WithTag("REG_G_25_FIELD_10", 25, 7)
                },
                {(long)Registers_G.RegG_31, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out field_218, name: "REG_G_31_FIELD_1")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_G.RegG_34, new DoubleWordRegister(this)
                    .WithValueField(16, 16, out field_217, name: "REG_G_34_FIELD_1")
                    .WithTag("REG_G_34_FIELD_2", 14, 2)
                    .WithTaggedFlag("REG_G_34_FIELD_3", 13)
                    .WithTaggedFlag("REG_G_34_FIELD_4", 12)
                    .WithTaggedFlag("REG_G_34_FIELD_5", 11)
                    .WithTag("REG_G_34_FIELD_6", 7, 4)
                    .WithTaggedFlag("REG_G_34_FIELD_7", 6)
                    .WithValueField(4, 2, out field_158, name: "REG_G_34_FIELD_8")
                    .WithTag("REG_G_34_FIELD_9", 0, 4)
                },
                {(long)Registers_G.RegG_213, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_213, name: "REG_G_213_FIELD_1")
                },
                {(long)Registers_G.RegG_214, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_214, name: "REG_G_214_FIELD_1")
                },
                {(long)Registers_G.RegG_215, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_215, name: "REG_G_215_FIELD_1")
                },
                {(long)Registers_G.RegG_216, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_216, name: "REG_G_216_FIELD_1")
                },
                {(long)Registers_G.RegG_217, new DoubleWordRegister(this)
                    .WithTag("REG_G_217_FIELD_1", 0, 5)
                    .WithReservedBits(5, 3)
                    .WithTag("REG_G_217_FIELD_2", 8, 3)
                    .WithTag("REG_G_217_FIELD_3", 11, 3)
                    .WithTag("REG_G_217_FIELD_4", 14, 3)
                    .WithTag("REG_G_217_FIELD_5", 17, 3)
                    .WithReservedBits(20, 4)
                    .WithTag("REG_G_217_FIELD_6", 24, 2)
                    .WithReservedBits(26, 3)
                    .WithFlag(29, out field_165, name: "REG_G_217_FIELD_7")
                    .WithFlag(30, out field_164, name: "REG_G_217_FIELD_8")
                    .WithFlag(31, out field_212, name: "REG_G_217_FIELD_9")
                },
                {(long)Registers_G.RegG_81, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_G_81_FIELD_1", 0)
                    .WithTaggedFlag("REG_G_81_FIELD_2", 1)
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("REG_G_81_FIELD_3", 3)
                    .WithTaggedFlag("REG_G_81_FIELD_4", 4)
                    .WithTaggedFlag("REG_G_81_FIELD_5", 5)
                    .WithReservedBits(6, 25)
                    .WithTaggedFlag("REG_G_81_FIELD_6", 31)
                },
                {(long)Registers_G.RegG_11, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, MODEM_DemodulatorState>(0, 3, out field_160, FieldMode.Read, name: "REG_G_11_FIELD_1")
                    .WithTaggedFlag("REG_G_11_FIELD_2", 3)
                    .WithValueField(4, 2, out field_166, FieldMode.Read, name: "REG_G_11_FIELD_3")
                    .WithTaggedFlag("REG_G_11_FIELD_4", 6)
                    .WithTaggedFlag("REG_G_11_FIELD_5", 7)
                    .WithTaggedFlag("REG_G_11_FIELD_6", 8)
                    .WithTaggedFlag("REG_G_11_FIELD_7", 9)
                    .WithTaggedFlag("REG_G_11_FIELD_8", 10)
                    .WithTaggedFlag("REG_G_11_FIELD_9", 11)
                    .WithTaggedFlag("REG_G_11_FIELD_10", 12)
                    .WithTag("REG_G_11_FIELD_11", 13, 3)
                    .WithTag("REG_G_11_FIELD_12", 16, 8)
                    .WithTag("REG_G_11_FIELD_13", 24, 8)
                },
                {(long)Registers_G.RegG_56, new DoubleWordRegister(this, 0x00000555)
                  .WithValueField(0, 4, out field_174, name: "REG_G_56_FIELD_1")
                  .WithValueField(4, 4, out field_175, name: "REG_G_56_FIELD_2")
                  .WithValueField(8, 4, out field_176, name: "REG_G_56_FIELD_3")
                  .WithFlag(12, out field_168, name: "REG_G_56_FIELD_4")
                  .WithTaggedFlag("REG_G_56_FIELD_5", 13)
                  .WithReservedBits(14, 2)
                  .WithValueField(16, 8, out field_177, name: "REG_G_56_FIELD_6")
                  .WithEnumField<DoubleWordRegister, Enumeration_N>(24, 1, out field_173, name: "REG_G_56_FIELD_7")
                  .WithReservedBits(25, 7)
                },
                {(long)Registers_G.RegG_57, new DoubleWordRegister(this, 0x009F9F9F)
                  .WithValueField(0, 8, out field_169, name: "REG_G_57_FIELD_1")
                  .WithValueField(8, 8, out field_170, name: "REG_G_57_FIELD_2")
                  .WithValueField(16, 8, out field_171, name: "REG_G_57_FIELD_3")
                  .WithValueField(24, 8, out field_172, name: "REG_G_57_FIELD_4")
                },
                {(long)Registers_G.RegG_222, new DoubleWordRegister(this)
                    .WithFlag(0, out field_228, name: "REG_G_222_FIELD_1")
                    .WithTaggedFlag("REG_G_222_FIELD_2", 1)
                    .WithTag("REG_G_222_FIELD_3", 2, 7)
                    .WithTag("REG_G_222_FIELD_4", 9, 7)
                    .WithTag("REG_G_222_FIELD_5", 16, 7)
                    .WithTaggedFlag("REG_G_222_FIELD_6", 23)
                    .WithTag("REG_G_222_FIELD_7", 24, 4)
                    .WithTag("REG_G_222_FIELD_8", 28, 4)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildProtocolTimerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_H.RegH_3, new DoubleWordRegister(this)
                    .WithReservedBits(0, 1)
                    .WithTaggedFlag("REG_H_3_FIELD_1", 1)
                    .WithTaggedFlag("REG_H_3_FIELD_2", 2)
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("REG_H_3_FIELD_3", 4)
                    .WithFlag(5, out field_290, name: "REG_H_3_FIELD_4")
                    .WithEnumField<DoubleWordRegister, Enumeration_U>(6, 2, out field_260, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case Enumeration_U.EnumerationUValue0:
                                    PROTIMER_Enabled = false;
                                    break;
                                case Enumeration_U.EnumerationUValue1:
                                    break;
                                default:
                                    PROTIMER_Enabled = false;
                                    this.Log(LogLevel.Error, "Invalid PRECNTSRC value");
                                    break;
                            }
                        }, name: "REG_H_3_FIELD_5")
                    .WithEnumField<DoubleWordRegister, Enumeration_P>(8, 2, out field_237, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case Enumeration_P.EnumerationPValue2:
                                case Enumeration_P.EnumerationPValue3:
                                    this.Log(LogLevel.Error, "Invalid BASECNTSRC value");
                                    break;
                            }
                        }, name: "REG_H_3_FIELD_6")
                    .WithEnumField<DoubleWordRegister, Enumeration_Y>(10, 2, out field_288, changeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case Enumeration_Y.EnumerationYValue3:
                                    this.Log(LogLevel.Error, "Invalid WRAPCNTSRC value");
                                    break;
                            }
                        }, name: "REG_H_3_FIELD_7")
                    .WithEnumField<DoubleWordRegister, Enumeration_W>(12, 2, out PROTIMER_timeoutCounter[0].Field_382, name: "REG_H_3_FIELD_8")
                    .WithEnumField<DoubleWordRegister, Enumeration_W>(14, 2, out PROTIMER_timeoutCounter[0].Field_383, name: "REG_H_3_FIELD_9")
                    .WithEnumField<DoubleWordRegister, Enumeration_W>(16, 2, out PROTIMER_timeoutCounter[1].Field_382, name: "REG_H_3_FIELD_10")
                    .WithEnumField<DoubleWordRegister, Enumeration_W>(18, 2, out PROTIMER_timeoutCounter[1].Field_383, name: "REG_H_3_FIELD_11")
                    .WithEnumField<DoubleWordRegister, Enumeration_W>(20, 2, out PROTIMER_timeoutCounter[2].Field_382, name: "REG_H_3_FIELD_12")
                    .WithEnumField<DoubleWordRegister, Enumeration_W>(22, 2, out PROTIMER_timeoutCounter[2].Field_383, name: "REG_H_3_FIELD_13")
                    .WithEnumField<DoubleWordRegister, Enumeration_V>(24, 1, out PROTIMER_timeoutCounter[0].Field_231, name: "REG_H_3_FIELD_14")
                    .WithEnumField<DoubleWordRegister, Enumeration_V>(25, 1, out PROTIMER_timeoutCounter[1].Field_231, name: "REG_H_3_FIELD_15")
                    .WithEnumField<DoubleWordRegister, Enumeration_V>(26, 1, out PROTIMER_timeoutCounter[2].Field_231, name: "REG_H_3_FIELD_16")
                    .WithTag("REG_H_3_FIELD_17", 27, 2)
                    .WithReservedBits(29, 3)
                },
                {(long)Registers_H.RegH_4, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) =>
                        {
                            if(field_260.Value == Enumeration_U.EnumerationUValue1 && value)
                            {
                                PROTIMER_Enabled = true;
                            }
                        }, name: "REG_H_4_FIELD_1")
                    .WithTaggedFlag("REG_H_4_FIELD_2", 1)
                    .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if(value) { PROTIMER_Enabled = false;} }, name: "REG_H_4_FIELD_3")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[0].Start(); }, name: "REG_H_4_FIELD_4")
                    .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[0].Stop(); }, name: "REG_H_4_FIELD_5")
                    .WithFlag(6, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[1].Start(); }, name: "REG_H_4_FIELD_6")
                    .WithFlag(7, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[1].Stop(); }, name: "REG_H_4_FIELD_7")
                    .WithFlag(8, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[2].Start(); }, name: "REG_H_4_FIELD_8")
                    .WithFlag(9, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_timeoutCounter[2].Stop(); }, name: "REG_H_4_FIELD_9")
                    .WithFlag(10, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_txRequestState = Enumeration_X.EnumerationXValue0; RAC_UpdateRadioStateMachine(); }, name: "REG_H_4_FIELD_10")
                    .WithFlag(11, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue0; RAC_UpdateRadioStateMachine(); }, name: "REG_H_4_FIELD_11")
                    .WithTaggedFlag("REG_H_4_FIELD_12", 12)
                    .WithReservedBits(13, 3)
                    .WithFlag(16, FieldMode.Set, writeCallback: (_, value) => { if (value) PROTIMER_ListenBeforeTalkStartCommand(); }, name: "REG_H_4_FIELD_13")
                    .WithFlag(17, FieldMode.Set, writeCallback: (_, value) => { if (value) PROTIMER_ListenBeforeTalkPauseCommand(); }, name: "REG_H_4_FIELD_14")
                    .WithFlag(18, FieldMode.Set, writeCallback: (_, value) => { if(value) PROTIMER_ListenBeforeTalkStopCommand(); }, name: "REG_H_4_FIELD_15")
                    .WithTaggedFlag("REG_H_4_FIELD_16", 19)
                    .WithReservedBits(20, 12)
                    .WithWriteCallback((_, __) => { PROTIMER_HandleChangedParams(); UpdateInterrupts(); })
                },
                {(long)Registers_H.RegH_6, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => PROTIMER_Enabled, name: "REG_H_6_FIELD_1")
                    .WithFlag(1, out field_255, FieldMode.Read, name: "REG_H_6_FIELD_2")
                    .WithFlag(2, out field_251, FieldMode.Read, name: "REG_H_6_FIELD_3")
                    .WithFlag(3, out field_247, FieldMode.Read, name: "REG_H_6_FIELD_4")
                    .WithFlag(4, out PROTIMER_timeoutCounter[0].Field_363, FieldMode.Read, name: "REG_H_6_FIELD_5")
                    .WithFlag(5, out PROTIMER_timeoutCounter[0].Field_384, FieldMode.Read, name: "REG_H_6_FIELD_6")
                    .WithFlag(6, out PROTIMER_timeoutCounter[1].Field_363, FieldMode.Read, name: "REG_H_6_FIELD_7")
                    .WithFlag(7, out PROTIMER_timeoutCounter[1].Field_384, FieldMode.Read, name: "REG_H_6_FIELD_8")
                    .WithFlag(8, out PROTIMER_timeoutCounter[2].Field_363, FieldMode.Read, name: "REG_H_6_FIELD_9")
                    .WithFlag(9, out PROTIMER_timeoutCounter[2].Field_384, FieldMode.Read, name: "REG_H_6_FIELD_10")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[0].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_11")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[1].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_12")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[2].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_13")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[3].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_14")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[4].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_15")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[5].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_16")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[6].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_17")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[7].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_18")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[8].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_19")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[9].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_20")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[10].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_21")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[11].Field_51, FieldMode.Read, name: "REG_H_6_FIELD_22")
                    .WithReservedBits(22, 10)
                },
                {(long)Registers_H.RegH_7, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => 0, name: "REG_H_7_FIELD_1")
                    .WithReservedBits(16, 16)
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)Registers_H.RegH_8, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => PROTIMER_BaseCounterValue, writeCallback: (_, value) => PROTIMER_BaseCounterValue = (uint)value, name: "REG_H_8_FIELD_1")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)Registers_H.RegH_9, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ =>  PROTIMER_WrapCounterValue, writeCallback: (_, value) => PROTIMER_WrapCounterValue = (uint)value, name: "REG_H_9_FIELD_1")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)Registers_H.RegH_10, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            PROTIMER_latchedBaseCounterValue = PROTIMER_BaseCounterValue;
                            PROTIMER_latchedWrapCounterValue = PROTIMER_WrapCounterValue;
                            return 0;
                        }, name: "REG_H_10_FIELD_1")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_H.RegH_11, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_latchedBaseCounterValue, name: "REG_H_11_FIELD_1")
                },
                {(long)Registers_H.RegH_12, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_latchedWrapCounterValue, name: "REG_H_12_FIELD_1")
                },
                {(long)Registers_H.RegH_48, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            PROTIMER_seqLatchedBaseCounterValue = PROTIMER_BaseCounterValue;
                            PROTIMER_seqLatchedWrapCounterValue = PROTIMER_WrapCounterValue;
                            return 0;
                        }, name: "REG_H_48_FIELD_1")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_H.RegH_49, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_seqLatchedBaseCounterValue, name: "REG_H_49_FIELD_1")
                },
                {(long)Registers_H.RegH_50, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PROTIMER_seqLatchedWrapCounterValue, name: "REG_H_50_FIELD_1")
                },
                {(long)Registers_H.RegH_13, new DoubleWordRegister(this)
                    .WithTag("REG_H_13_FIELD_1", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_H.RegH_14, new DoubleWordRegister(this, 0xFFFF0000)
                    .WithValueField(0, 16, out field_261, name: "REG_H_14_FIELD_1")
                    .WithValueField(16, 16, out field_262, name: "REG_H_14_FIELD_2")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)Registers_H.RegH_15, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 32, out field_238, name: "REG_H_15_FIELD_1")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)Registers_H.RegH_16, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithValueField(0, 32, out field_289, name: "REG_H_16_FIELD_1")
                    .WithWriteCallback((_, __) => PROTIMER_HandleChangedParams())
                },
                {(long)Registers_H.RegH_17, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].Field_291, name: "REG_H_17_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].Field_52, name: "REG_H_17_FIELD_2")
                },
                {(long)Registers_H.RegH_18, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].Field_293, name: "REG_H_18_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].Field_54, name: "REG_H_18_FIELD_2")
                },
                {(long)Registers_H.RegH_19, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[0].Field_292, name: "REG_H_19_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[0].Field_53, name: "REG_H_19_FIELD_2")
                },
                {(long)Registers_H.RegH_20, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].Field_291, name: "REG_H_20_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].Field_52, name: "REG_H_20_FIELD_2")
                },
                {(long)Registers_H.RegH_21, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].Field_293, name: "REG_H_21_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].Field_54, name: "REG_H_21_FIELD_2")
                },
                {(long)Registers_H.RegH_22, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[1].Field_292, name: "REG_H_22_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[1].Field_53, name: "REG_H_22_FIELD_2")
                },
                {(long)Registers_H.RegH_23, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[2].Field_291, name: "REG_H_23_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[2].Field_52, name: "REG_H_23_FIELD_2")
                },
                {(long)Registers_H.RegH_24, new DoubleWordRegister(this, 0x00FF00FF)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[2].Field_293, name: "REG_H_24_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[2].Field_54, name: "REG_H_24_FIELD_2")
                },
                {(long)Registers_H.RegH_25, new DoubleWordRegister(this)
                    .WithValueField(0, 16, out PROTIMER_timeoutCounter[2].Field_292, name: "REG_H_25_FIELD_1")
                    .WithValueField(16, 16, out PROTIMER_timeoutCounter[2].Field_53, name: "REG_H_25_FIELD_2")
                },
                {(long)Registers_H.RegH_32, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].Field_154, name: "REG_H_32_FIELD_1")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].Field_154, name: "REG_H_32_FIELD_2")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].Field_154, name: "REG_H_32_FIELD_3")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].Field_154, name: "REG_H_32_FIELD_4")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].Field_154, name: "REG_H_32_FIELD_5")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].Field_154, name: "REG_H_32_FIELD_6")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].Field_154, name: "REG_H_32_FIELD_7")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].Field_154, name: "REG_H_32_FIELD_8")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].Field_154, name: "REG_H_32_FIELD_9")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].Field_154, name: "REG_H_32_FIELD_10")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].Field_154, name: "REG_H_32_FIELD_11")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].Field_154, name: "REG_H_32_FIELD_12")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].Field_232, name: "REG_H_32_FIELD_13")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].Field_232, name: "REG_H_32_FIELD_14")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].Field_232, name: "REG_H_32_FIELD_15")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].Field_232, name: "REG_H_32_FIELD_16")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].Field_232, name: "REG_H_32_FIELD_17")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].Field_232, name: "REG_H_32_FIELD_18")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].Field_232, name: "REG_H_32_FIELD_19")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].Field_232, name: "REG_H_32_FIELD_20")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].Field_232, name: "REG_H_32_FIELD_21")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].Field_232, name: "REG_H_32_FIELD_22")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].Field_232, name: "REG_H_32_FIELD_23")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].Field_232, name: "REG_H_32_FIELD_24")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].Field_385, name: "REG_H_32_FIELD_25")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].Field_385, name: "REG_H_32_FIELD_26")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].Field_385, name: "REG_H_32_FIELD_27")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].Field_229, name: "REG_H_32_FIELD_28")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].Field_229, name: "REG_H_32_FIELD_29")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].Field_229, name: "REG_H_32_FIELD_30")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_33, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].Field_153, name: "REG_H_33_FIELD_1")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].Field_153, name: "REG_H_33_FIELD_2")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].Field_153, name: "REG_H_33_FIELD_3")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].Field_153, name: "REG_H_33_FIELD_4")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].Field_153, name: "REG_H_33_FIELD_5")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].Field_153, name: "REG_H_33_FIELD_6")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].Field_153, name: "REG_H_33_FIELD_7")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].Field_153, name: "REG_H_33_FIELD_8")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].Field_153, name: "REG_H_33_FIELD_9")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].Field_153, name: "REG_H_33_FIELD_10")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].Field_153, name: "REG_H_33_FIELD_11")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].Field_153, name: "REG_H_33_FIELD_12")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].Field_233, name: "REG_H_33_FIELD_13")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].Field_233, name: "REG_H_33_FIELD_14")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].Field_233, name: "REG_H_33_FIELD_15")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].Field_233, name: "REG_H_33_FIELD_16")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].Field_233, name: "REG_H_33_FIELD_17")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].Field_233, name: "REG_H_33_FIELD_18")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].Field_233, name: "REG_H_33_FIELD_19")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].Field_233, name: "REG_H_33_FIELD_20")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].Field_233, name: "REG_H_33_FIELD_21")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].Field_233, name: "REG_H_33_FIELD_22")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].Field_233, name: "REG_H_33_FIELD_23")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].Field_233, name: "REG_H_33_FIELD_24")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].Field_386, name: "REG_H_33_FIELD_25")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].Field_386, name: "REG_H_33_FIELD_26")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].Field_386, name: "REG_H_33_FIELD_27")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].Field_230, name: "REG_H_33_FIELD_28")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].Field_230, name: "REG_H_33_FIELD_29")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].Field_230, name: "REG_H_33_FIELD_30")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_30, new DoubleWordRegister(this)
                    .WithFlag(0, out field_258, name: "REG_H_30_FIELD_1")
                    .WithFlag(1, out field_235, name: "REG_H_30_FIELD_2")
                    .WithFlag(2, out field_286, name: "REG_H_30_FIELD_3")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out field_253, name: "REG_H_30_FIELD_4")
                    .WithFlag(27, out field_244, name: "REG_H_30_FIELD_5")
                    .WithTaggedFlag("REG_H_30_FIELD_6", 28)
                    .WithFlag(29, out field_249, name: "REG_H_30_FIELD_7")
                    .WithTaggedFlag("REG_H_30_FIELD_8", 30)
                    .WithFlag(31, out field_256, name: "REG_H_30_FIELD_9")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_31, new DoubleWordRegister(this)
                    .WithFlag(0, out field_259, name: "REG_H_31_FIELD_1")
                    .WithFlag(1, out field_236, name: "REG_H_31_FIELD_2")
                    .WithFlag(2, out field_287, name: "REG_H_31_FIELD_3")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out field_254, name: "REG_H_31_FIELD_4")
                    .WithFlag(27, out field_245, name: "REG_H_31_FIELD_5")
                    .WithTaggedFlag("REG_H_31_FIELD_6", 28)
                    .WithFlag(29, out field_250, name: "REG_H_31_FIELD_7")
                    .WithTaggedFlag("REG_H_31_FIELD_8", 30)
                    .WithFlag(31, out field_257, name: "REG_H_31_FIELD_9")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_43, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].Field_374, name: "REG_H_43_FIELD_1")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].Field_374, name: "REG_H_43_FIELD_2")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].Field_374, name: "REG_H_43_FIELD_3")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].Field_374, name: "REG_H_43_FIELD_4")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].Field_374, name: "REG_H_43_FIELD_5")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].Field_374, name: "REG_H_43_FIELD_6")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].Field_374, name: "REG_H_43_FIELD_7")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].Field_374, name: "REG_H_43_FIELD_8")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].Field_374, name: "REG_H_43_FIELD_9")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].Field_374, name: "REG_H_43_FIELD_10")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].Field_374, name: "REG_H_43_FIELD_11")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].Field_374, name: "REG_H_43_FIELD_12")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].Field_377, name: "REG_H_43_FIELD_13")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].Field_377, name: "REG_H_43_FIELD_14")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].Field_377, name: "REG_H_43_FIELD_15")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].Field_377, name: "REG_H_43_FIELD_16")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].Field_377, name: "REG_H_43_FIELD_17")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].Field_377, name: "REG_H_43_FIELD_18")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].Field_377, name: "REG_H_43_FIELD_19")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].Field_377, name: "REG_H_43_FIELD_20")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].Field_377, name: "REG_H_43_FIELD_21")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].Field_377, name: "REG_H_43_FIELD_22")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].Field_377, name: "REG_H_43_FIELD_23")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].Field_377, name: "REG_H_43_FIELD_24")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].Field_379, name: "REG_H_43_FIELD_25")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].Field_379, name: "REG_H_43_FIELD_26")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].Field_379, name: "REG_H_43_FIELD_27")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].Field_375, name: "REG_H_43_FIELD_28")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].Field_375, name: "REG_H_43_FIELD_29")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].Field_375, name: "REG_H_43_FIELD_30")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_44, new DoubleWordRegister(this)
                    .WithFlag(0, out PROTIMER_captureCompareChannel[0].Field_373, name: "REG_H_44_FIELD_1")
                    .WithFlag(1, out PROTIMER_captureCompareChannel[1].Field_373, name: "REG_H_44_FIELD_2")
                    .WithFlag(2, out PROTIMER_captureCompareChannel[2].Field_373, name: "REG_H_44_FIELD_3")
                    .WithFlag(3, out PROTIMER_captureCompareChannel[3].Field_373, name: "REG_H_44_FIELD_4")
                    .WithFlag(4, out PROTIMER_captureCompareChannel[4].Field_373, name: "REG_H_44_FIELD_5")
                    .WithFlag(5, out PROTIMER_captureCompareChannel[5].Field_373, name: "REG_H_44_FIELD_6")
                    .WithFlag(6, out PROTIMER_captureCompareChannel[6].Field_373, name: "REG_H_44_FIELD_7")
                    .WithFlag(7, out PROTIMER_captureCompareChannel[7].Field_373, name: "REG_H_44_FIELD_8")
                    .WithFlag(8, out PROTIMER_captureCompareChannel[8].Field_373, name: "REG_H_44_FIELD_9")
                    .WithFlag(9, out PROTIMER_captureCompareChannel[9].Field_373, name: "REG_H_44_FIELD_10")
                    .WithFlag(10, out PROTIMER_captureCompareChannel[10].Field_373, name: "REG_H_44_FIELD_11")
                    .WithFlag(11, out PROTIMER_captureCompareChannel[11].Field_373, name: "REG_H_44_FIELD_12")
                    .WithFlag(12, out PROTIMER_captureCompareChannel[0].Field_378, name: "REG_H_44_FIELD_13")
                    .WithFlag(13, out PROTIMER_captureCompareChannel[1].Field_378, name: "REG_H_44_FIELD_14")
                    .WithFlag(14, out PROTIMER_captureCompareChannel[2].Field_378, name: "REG_H_44_FIELD_15")
                    .WithFlag(15, out PROTIMER_captureCompareChannel[3].Field_378, name: "REG_H_44_FIELD_16")
                    .WithFlag(16, out PROTIMER_captureCompareChannel[4].Field_378, name: "REG_H_44_FIELD_17")
                    .WithFlag(17, out PROTIMER_captureCompareChannel[5].Field_378, name: "REG_H_44_FIELD_18")
                    .WithFlag(18, out PROTIMER_captureCompareChannel[6].Field_378, name: "REG_H_44_FIELD_19")
                    .WithFlag(19, out PROTIMER_captureCompareChannel[7].Field_378, name: "REG_H_44_FIELD_20")
                    .WithFlag(20, out PROTIMER_captureCompareChannel[8].Field_378, name: "REG_H_44_FIELD_21")
                    .WithFlag(21, out PROTIMER_captureCompareChannel[9].Field_378, name: "REG_H_44_FIELD_22")
                    .WithFlag(22, out PROTIMER_captureCompareChannel[10].Field_378, name: "REG_H_44_FIELD_23")
                    .WithFlag(23, out PROTIMER_captureCompareChannel[11].Field_378, name: "REG_H_44_FIELD_24")
                    .WithFlag(24, out PROTIMER_timeoutCounter[0].Field_380, name: "REG_H_44_FIELD_25")
                    .WithFlag(25, out PROTIMER_timeoutCounter[1].Field_380, name: "REG_H_44_FIELD_26")
                    .WithFlag(26, out PROTIMER_timeoutCounter[2].Field_380, name: "REG_H_44_FIELD_27")
                    .WithFlag(27, out PROTIMER_timeoutCounter[0].Field_376, name: "REG_H_44_FIELD_28")
                    .WithFlag(28, out PROTIMER_timeoutCounter[1].Field_376, name: "REG_H_44_FIELD_29")
                    .WithFlag(29, out PROTIMER_timeoutCounter[2].Field_376, name: "REG_H_44_FIELD_30")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_41, new DoubleWordRegister(this)
                    .WithFlag(0, out field_280, name: "REG_H_41_FIELD_1")
                    .WithFlag(1, out field_270, name: "REG_H_41_FIELD_2")
                    .WithFlag(2, out field_282, name: "REG_H_41_FIELD_3")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out field_276, name: "REG_H_41_FIELD_4")
                    .WithFlag(27, out field_272, name: "REG_H_41_FIELD_5")
                    .WithTaggedFlag("REG_H_41_FIELD_6", 28)
                    .WithFlag(29, out field_274, name: "REG_H_41_FIELD_7")
                    .WithTaggedFlag("REG_H_41_FIELD_8", 30)
                    .WithFlag(31, out field_278, name: "REG_H_41_FIELD_9")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_42, new DoubleWordRegister(this)
                    .WithFlag(0, out field_281, name: "REG_H_42_FIELD_1")
                    .WithFlag(1, out field_271, name: "REG_H_42_FIELD_2")
                    .WithFlag(2, out field_283, name: "REG_H_42_FIELD_3")
                    .WithReservedBits(3, 23)
                    .WithFlag(26, out field_277, name: "REG_H_42_FIELD_4")
                    .WithFlag(27, out field_273, name: "REG_H_42_FIELD_5")
                    .WithTaggedFlag("REG_H_42_FIELD_6", 28)
                    .WithFlag(29, out field_275, name: "REG_H_42_FIELD_7")
                    .WithTaggedFlag("REG_H_42_FIELD_8", 30)
                    .WithFlag(31, out field_279, name: "REG_H_42_FIELD_9")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_H.RegH_34, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(0, 6, out field_268, name: "REG_H_34_FIELD_1")
                    .WithReservedBits(6, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(8, 6, out field_269, name: "REG_H_34_FIELD_2")
                    .WithReservedBits(14, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(16, 6, out field_266, name: "REG_H_34_FIELD_3")
                    .WithReservedBits(22, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(24, 6, out field_267, name: "REG_H_34_FIELD_4")
                    .WithReservedBits(30, 2)
                    .WithChangeCallback((_, __) => PROTIMER_UpdateRxRequestState())
                },
                {(long)Registers_H.RegH_35, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(0, 6, out field_284, name: "REG_H_35_FIELD_1")
                    .WithReservedBits(6, 2)
                    .WithEnumField<DoubleWordRegister, PROTIMER_Event>(8, 6, out field_285, name: "REG_H_35_FIELD_2")
                    .WithReservedBits(14, 18)
                    .WithChangeCallback((_, __) => PROTIMER_UpdateTxRequestState())
                },
                {(long)Registers_H.RegH_26, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out field_252, name: "REG_H_26_FIELD_1")
                    .WithValueField(4, 4, out field_246, name: "REG_H_26_FIELD_2")
                    .WithValueField(8, 5, out field_240, name: "REG_H_26_FIELD_3")
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 4, out field_241, name: "REG_H_26_FIELD_4")
                    .WithFlag(20, out field_242, name: "REG_H_26_FIELD_5")
                    .WithReservedBits(21, 3)
                    .WithValueField(24, 4, out field_265, name: "REG_H_26_FIELD_6")
                    .WithReservedBits(28, 4)
                },
                {(long)Registers_H.RegH_28, new DoubleWordRegister(this)
                    .WithTag("REG_H_28_FIELD_1", 0, 16)
                    .WithTag("REG_H_28_FIELD_2", 16, 16)
                },
                {(long)Registers_H.RegH_29, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => {return (ushort)random.Next();}, name: "REG_H_29_FIELD_1")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_H.RegH_37, new DoubleWordRegister(this)
                    .WithValueField(0, 4, out field_239, name: "REG_H_37_FIELD_1")
                    .WithValueField(4, 4, out field_243, name: "REG_H_37_FIELD_2")
                    .WithValueField(8, 4, out field_248, name: "REG_H_37_FIELD_3")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers_H.RegH_38, new DoubleWordRegister(this)
                    .WithValueField(0, 9, out field_234[0], name: "REG_H_38_FIELD_1")
                    .WithValueField(9, 9, out field_234[1], name: "REG_H_38_FIELD_2")
                    .WithValueField(18, 9, out field_234[2], name: "REG_H_38_FIELD_3")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers_H.RegH_39, new DoubleWordRegister(this)
                    .WithValueField(0, 9, out field_234[3], name: "REG_H_39_FIELD_1")
                    .WithValueField(9, 9, out field_234[4], name: "REG_H_39_FIELD_2")
                    .WithValueField(18, 9, out field_234[5], name: "REG_H_39_FIELD_3")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers_H.RegH_40, new DoubleWordRegister(this)
                    .WithValueField(0, 9, out field_234[6], name: "REG_H_40_FIELD_1")
                    .WithValueField(9, 9, out field_234[7], name: "REG_H_40_FIELD_2")
                    .WithReservedBits(18, 14)
                },
                {(long)Registers_H.RegH_46, new DoubleWordRegister(this)
                    .WithTag("REG_H_46_FIELD_1", 0, 5)
                    .WithReservedBits(5, 27)
                },
                {(long)Registers_H.RegH_47, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(0, 9, out field_263, name: "REG_H_47_FIELD_1")
                    .WithReservedBits(9, 7)
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(16, 9, out field_264, name: "REG_H_47_FIELD_2")
                    .WithReservedBits(25, 7)
                },
            };

            var startOffset = (long)Registers_H.RegH_55;
            var controlOffset = (long)Registers_H.RegH_55 - startOffset;
            var preOffset = (long)Registers_H.RegH_56 - startOffset;
            var baseOffset = (long)Registers_H.RegH_57 - startOffset;
            var wrapOffset = (long)Registers_H.RegH_58 - startOffset;
            var wrapLowLimitOffset = (long)Registers_H.RegH_59 - startOffset;
            var wrapHighLimitOffset = (long)Registers_H.RegH_60 - startOffset;
            var baseLowLimitOffset = (long)Registers_H.RegH_61 - startOffset;
            var baseHighLimitOffset = (long)Registers_H.RegH_62 - startOffset;
            var blockSize = (long)Registers_H.RegH_63 - (long)Registers_H.RegH_55;
            for(var index = 0; index < PROTIMER_NumberOfCaptureCompareChannels; index++)
            {
                var i = index;
                registerDictionary.Add(startOffset + blockSize * i + controlOffset,
                    new DoubleWordRegister(this)
                        .WithFlag(0, out PROTIMER_captureCompareChannel[i].Field_56, name: "REG_H_47_FIELD_3")
                        .WithEnumField<DoubleWordRegister, Enumeration_Q>(1, 2, out PROTIMER_captureCompareChannel[i].Field_231, name: "REG_H_47_FIELD_4")
                        .WithFlag(3, out PROTIMER_captureCompareChannel[i].Field_294, name: "REG_H_47_FIELD_5")
                        .WithFlag(4, out PROTIMER_captureCompareChannel[i].Field_43, name: "REG_H_47_FIELD_6")
                        .WithFlag(5, out PROTIMER_captureCompareChannel[i].Field_390, name: "REG_H_47_FIELD_7")
                        .WithTaggedFlag("REG_H_47_FIELD_8", 6)
                        .WithTaggedFlag("REG_H_47_FIELD_9", 7)
                        .WithTag("REG_H_47_FIELD_10", 8, 2)
                        .WithTag("REG_H_47_FIELD_11", 10, 2)
                        .WithTag("REG_H_47_FIELD_12", 12, 2)
                        .WithTaggedFlag("REG_H_47_FIELD_13", 14)
                        .WithReservedBits(15, 6)
                        .WithEnumField<DoubleWordRegister, Enumeration_R>(21, 4, out PROTIMER_captureCompareChannel[i].Field_50, name: "REG_H_47_FIELD_14")
                        .WithTag("REG_H_47_FIELD_15", 25, 2)
                        .WithReservedBits(27, 5)
                        .WithWriteCallback((_, __) => PROTIMER_UpdateCompareTimer(i))
                );
                registerDictionary.Add(startOffset + blockSize * i + preOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 16, out PROTIMER_captureCompareChannel[i].Field_295,
                            writeCallback: (_, __) =>
                            {
                                PROTIMER_captureCompareChannel[i].Field_51.Value = false;
                                PROTIMER_UpdateCompareTimer(i);
                            },
                            valueProviderCallback: _ =>
                            {
                                PROTIMER_captureCompareChannel[i].Field_51.Value = false;
                                return PROTIMER_captureCompareChannel[i].Field_295.Value;
                            }, name: "REG_H_47_FIELD_16")
                        .WithReservedBits(16, 16)
                );
                registerDictionary.Add(startOffset + blockSize * i + baseOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].Field_44,
                            writeCallback: (_, __) => { PROTIMER_captureCompareChannel[i].Field_51.Value = false; },
                            valueProviderCallback: _ =>
                            {
                                PROTIMER_captureCompareChannel[i].Field_51.Value = false;
                                return PROTIMER_captureCompareChannel[i].Field_44.Value;
                            }, name: "REG_H_47_FIELD_17")
                );
                registerDictionary.Add(startOffset + blockSize * i + wrapOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].Field_391,
                            writeCallback: (_, __) => { PROTIMER_captureCompareChannel[i].Field_51.Value = false; },
                            valueProviderCallback: _ =>
                            {
                                PROTIMER_captureCompareChannel[i].Field_51.Value = false;
                                return PROTIMER_captureCompareChannel[i].Field_391.Value;
                            }, name: "REG_H_47_FIELD_18")
                );
                registerDictionary.Add(startOffset + blockSize * i + wrapLowLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].Field_389,
                            writeCallback: (_, __) =>
                            {
                            },
                            valueProviderCallback: _ =>
                            {
                                return 0;
                            }, name: "REG_H_47_FIELD_19")
                );
                registerDictionary.Add(startOffset + blockSize * i + wrapHighLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].Field_388,
                            writeCallback: (_, __) =>
                            {
                            },
                            valueProviderCallback: _ =>
                            {
                                return 0;
                            }, name: "REG_H_47_FIELD_20")
                );
                registerDictionary.Add(startOffset + blockSize * i + baseLowLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].Field_42,
                            writeCallback: (_, __) =>
                            {
                            },
                            valueProviderCallback: _ =>
                            {
                                return 0;
                            }, name: "REG_H_47_FIELD_21")
                );
                registerDictionary.Add(startOffset + blockSize * i + baseHighLimitOffset,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, out PROTIMER_captureCompareChannel[i].Field_41,
                            writeCallback: (_, __) =>
                            {
                            },
                            valueProviderCallback: _ =>
                            {
                                return 0;
                            }, name: "REG_H_47_FIELD_22")
                );
            }
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildRadioControllerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_I.RegI_3, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out field_355, name: "REG_I_3_FIELD_1")
                    .WithTaggedFlag("REG_I_3_FIELD_2", 8)
                    .WithTaggedFlag("REG_I_3_FIELD_3", 9)
                    .WithTaggedFlag("REG_I_3_FIELD_4", 10)
                    .WithTaggedFlag("REG_I_3_FIELD_5", 11)
                    .WithTaggedFlag("REG_I_3_FIELD_6", 12)
                    .WithTaggedFlag("REG_I_3_FIELD_7", 13)
                    .WithReservedBits(14, 18)
                    .WithWriteCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)Registers_I.RegI_4, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => RAC_RxEnableMask, name: "REG_I_4_FIELD_1")
                    .WithReservedBits(16, 3)
                    .WithFlag(19, out field_298, FieldMode.Read, name: "REG_I_4_FIELD_2")
                    .WithReservedBits(20, 2)
                    .WithFlag(22, out field_354, FieldMode.Read, name: "REG_I_4_FIELD_3")
                    .WithFlag(23, out field_353, FieldMode.Read, name: "REG_I_4_FIELD_4")
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(24, 4, FieldMode.Read, valueProviderCallback: _ => RAC_currentRadioState, name: "REG_I_4_FIELD_5")
                    .WithFlag(28, out field_352, FieldMode.Read, name: "REG_I_4_FIELD_6")
                    .WithTaggedFlag("REG_I_4_FIELD_7", 29)
                    .WithFlag(30, FieldMode.Read, valueProviderCallback: _ => RAC_TxEnable, name: "REG_I_4_FIELD_8")
                    .WithFlag(31, FieldMode.Read, valueProviderCallback: _ => RAC_RxEnable, name: "REG_I_4_FIELD_9")
                },
                {(long)Registers_I.RegI_13, new DoubleWordRegister(this)
                    .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: _ => RAC_TxEnableMask, name: "REG_I_13_FIELD_1")
                    .WithReservedBits(7, 25)
                },
                {(long)Registers_I.RegI_39, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(0, 4, FieldMode.Read, valueProviderCallback: _ => RAC_previous1RadioState, name: "REG_I_39_FIELD_1")
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(4, 4, FieldMode.Read, valueProviderCallback: _ => RAC_previous2RadioState, name: "REG_I_39_FIELD_2")
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(8, 4, FieldMode.Read, valueProviderCallback: _ => RAC_previous3RadioState, name: "REG_I_39_FIELD_3")
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(12, 4, FieldMode.Read, valueProviderCallback: _ => RAC_currentRadioState, name: "REG_I_39_FIELD_4")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_I.RegI_6, new DoubleWordRegister(this)
                    .WithFlag(0, out field_297, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine();} }, name: "REG_I_6_FIELD_1")
                    .WithTaggedFlag("REG_I_6_FIELD_2", 1)
                    .WithReservedBits(2, 1)
                    .WithTaggedFlag("REG_I_6_FIELD_3", 3)
                    .WithTaggedFlag("REG_I_6_FIELD_4", 4)
                    .WithTaggedFlag("REG_I_6_FIELD_5", 5)
                    .WithTaggedFlag("REG_I_6_FIELD_6", 6)
                    .WithTaggedFlag("REG_I_6_FIELD_7", 7)
                    .WithTaggedFlag("REG_I_6_FIELD_8", 8)
                    .WithTaggedFlag("REG_I_6_FIELD_9", 9)
                    .WithTaggedFlag("REG_I_6_FIELD_10", 10)
                    .WithTag("REG_I_6_FIELD_11", 11, 2)
                    .WithTaggedFlag("REG_I_6_FIELD_12", 13)
                    .WithTaggedFlag("REG_I_6_FIELD_13", 14)
                    .WithTaggedFlag("REG_I_6_FIELD_14", 15)
                    .WithTaggedFlag("REG_I_6_FIELD_15", 16)
                    .WithTag("REG_I_6_FIELD_16", 17, 3)
                    .WithTaggedFlag("REG_I_6_FIELD_17", 21)
                    .WithTaggedFlag("REG_I_6_FIELD_18", 22)
                    .WithTaggedFlag("REG_I_6_FIELD_19", 23)
                    .WithTaggedFlag("REG_I_6_FIELD_20", 24)
                    .WithFlag(25, out field_296, FieldMode.Read, name: "REG_I_6_FIELD_21")
                    .WithFlag(26, FieldMode.Set, writeCallback: (_, value) =>
                        {
                            if (value && sequencer.IsHalted)
                            {
                                sequencer.PC = sequencerConfig.BootAddress;
                                sequencer.IsHalted = false;
                                sequencer.Resume();
                            }
                        }, name: "REG_I_6_FIELD_22")
                    .WithTaggedFlag("REG_I_6_FIELD_23", 27)
                    .WithTag("REG_I_6_FIELD_24", 28, 2)
                    .WithTaggedFlag("REG_I_6_FIELD_25", 30)
                    .WithTaggedFlag("REG_I_6_FIELD_26", 31)
                    .WithChangeCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)Registers_I.RegI_5, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_TxEnable = true;} }, name: "REG_I_5_FIELD_1")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue3);} }, name: "REG_I_5_FIELD_2")
                    .WithTaggedFlag("REG_I_5_FIELD_3", 2)
                    .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_TxEnable = false;} }, name: "REG_I_5_FIELD_4")
                    .WithReservedBits(4, 1)
                    .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => { if(value) {RAC_TxEnable = false; RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue16);} }, name: "REG_I_5_FIELD_5")
                    .WithReservedBits(6, 1)
                    .WithFlag(7, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue9);} }, name: "REG_I_5_FIELD_6")
                    .WithFlag(8, FieldMode.Set, writeCallback: (_, value) => { if (value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue13);} }, name: "REG_I_5_FIELD_7")
                    .WithReservedBits(9, 1)
                    .WithTaggedFlag("REG_I_5_FIELD_8", 10)
                    .WithTaggedFlag("REG_I_5_FIELD_9", 11)
                    .WithTaggedFlag("REG_I_5_FIELD_10", 12)
                    .WithTaggedFlag("REG_I_5_FIELD_11", 13)
                    .WithTaggedFlag("REG_I_5_FIELD_12", 14)
                    .WithTaggedFlag("REG_I_5_FIELD_13", 15)
                    .WithReservedBits(16, 14)
                    .WithTaggedFlag("REG_I_5_FIELD_14", 30)
                    .WithTaggedFlag("REG_I_5_FIELD_15", 31)
                },
                {(long)Registers_I.RegI_7, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(0, 3, out field_299, writeCallback: (_, __) =>
                        {
                            field_298.Value = true;
                        }, name: "REG_I_7_FIELD_1")
                    .WithReservedBits(4, 28)
                    .WithChangeCallback((_, __) => RAC_UpdateRadioStateMachine())
                },
                {(long)Registers_I.RegI_35, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { RAC_em1pAckPending = true; }, name: "REG_I_35_FIELD_1")
                    .WithTaggedFlag("REG_I_35_FIELD_2", 1)
                    .WithReservedBits(2, 2)
                    .WithTaggedFlag("REG_I_35_FIELD_3", 4)
                    .WithTaggedFlag("REG_I_35_FIELD_4", 5)
                    .WithReservedBits(6, 10)
                    .WithTaggedFlag("REG_I_35_FIELD_5", 16)
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ =>
                        {
                            var retValue = RAC_em1pAckPending;
                            RAC_em1pAckPending = false;
                            return retValue;
                        }, name: "REG_I_35_FIELD_6")
                    .WithTaggedFlag("REG_I_35_FIELD_7", 18)
                    .WithReservedBits(19, 13)
                },
                {(long)Registers_I.RegI_8, new DoubleWordRegister(this)
                    .WithFlag(0, out field_303, name: "REG_I_8_FIELD_1")
                    .WithFlag(1, out field_356, name: "REG_I_8_FIELD_2")
                    .WithTaggedFlag("REG_I_8_FIELD_3", 2)
                    .WithTaggedFlag("REG_I_8_FIELD_4", 3)
                    .WithTaggedFlag("REG_I_8_FIELD_5", 4)
                    .WithTaggedFlag("REG_I_8_FIELD_6", 5)
                    .WithTaggedFlag("REG_I_8_FIELD_7", 6)
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 24, out field_300, name: "REG_I_8_FIELD_8")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_I.RegI_9, new DoubleWordRegister(this)
                    .WithFlag(0, out field_304, name: "REG_I_9_FIELD_1")
                    .WithFlag(1, out field_357, name: "REG_I_9_FIELD_2")
                    .WithTaggedFlag("REG_I_9_FIELD_3", 2)
                    .WithTaggedFlag("REG_I_9_FIELD_4", 3)
                    .WithTaggedFlag("REG_I_9_FIELD_5", 4)
                    .WithTaggedFlag("REG_I_9_FIELD_6", 5)
                    .WithTaggedFlag("REG_I_9_FIELD_7", 6)
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 24, out field_301, name: "REG_I_9_FIELD_8")
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_I.RegI_11, new DoubleWordRegister(this)
                    .WithFlag(0, out field_311, name: "REG_I_11_FIELD_1")
                    .WithFlag(1, out field_349, name: "REG_I_11_FIELD_2")
                    .WithFlag(2, out field_307, name: "REG_I_11_FIELD_3")
                    .WithFlag(3, out field_309, name: "REG_I_11_FIELD_4")
                    .WithReservedBits(4, 2)
                    .WithTaggedFlag("REG_I_11_FIELD_5", 6)
                    .WithReservedBits(7, 9)
                    .WithFlag(16, out field_315, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue25);}}, name: "REG_I_11_FIELD_6")
                    .WithFlag(17, out field_327, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue20);}}, name: "REG_I_11_FIELD_7")
                    .WithFlag(18, out field_323, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue21);}}, name: "REG_I_11_FIELD_8")
                    .WithFlag(19, out field_319, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue22);}}, name: "REG_I_11_FIELD_9")
                    .WithFlag(20, out field_331, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue23);}}, name: "REG_I_11_FIELD_10")
                    .WithFlag(21, out field_343, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue17);}}, name: "REG_I_11_FIELD_11")
                    .WithFlag(22, out field_339, name: "REG_I_11_FIELD_12")
                    .WithFlag(23, out field_347, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue19);}}, name: "REG_I_11_FIELD_13")
                    .WithFlag(24, out field_333, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue24);}}, name: "REG_I_11_FIELD_14")
                    .WithReservedBits(25, 3)
                    .WithTaggedFlag("REG_I_11_FIELD_15", 28)
                    .WithTaggedFlag("REG_I_11_FIELD_16", 29)
                    .WithTaggedFlag("REG_I_11_FIELD_17", 30)
                    .WithTaggedFlag("REG_I_11_FIELD_18", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_I.RegI_12, new DoubleWordRegister(this)
                    .WithFlag(0, out field_312, name: "REG_I_12_FIELD_1")
                    .WithFlag(1, out field_350, name: "REG_I_12_FIELD_2")
                    .WithFlag(2, out field_308, name: "REG_I_12_FIELD_3")
                    .WithFlag(3, out field_310, name: "REG_I_12_FIELD_4")
                    .WithReservedBits(4, 2)
                    .WithTaggedFlag("REG_I_12_FIELD_5", 6)
                    .WithReservedBits(7, 9)
                    .WithFlag(16, out field_316, name: "REG_I_12_FIELD_6")
                    .WithFlag(17, out field_328, name: "REG_I_12_FIELD_7")
                    .WithFlag(18, out field_324, name: "REG_I_12_FIELD_8")
                    .WithFlag(19, out field_320, name: "REG_I_12_FIELD_9")
                    .WithFlag(20, out field_332, name: "REG_I_12_FIELD_10")
                    .WithFlag(21, out field_344, name: "REG_I_12_FIELD_11")
                    .WithFlag(22, out field_340, name: "REG_I_12_FIELD_12")
                    .WithFlag(23, out field_348, name: "REG_I_12_FIELD_13")
                    .WithFlag(24, out field_334, name: "REG_I_12_FIELD_14")
                    .WithReservedBits(25, 3)
                    .WithTaggedFlag("REG_I_12_FIELD_15", 28)
                    .WithTaggedFlag("REG_I_12_FIELD_16", 29)
                    .WithTaggedFlag("REG_I_12_FIELD_17", 30)
                    .WithTaggedFlag("REG_I_12_FIELD_18", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },

                {(long)Registers_I.RegI_16, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(0, 4, out field_358, name: "REG_I_16_FIELD_1")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers_I.RegI_17, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Enumeration_AB>(0, 4, out field_305, name: "REG_I_17_FIELD_1")
                    .WithReservedBits(4, 28)
                },
                {(long)Registers_I.RegI_18, new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithFlag(16, out field_313, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue25);}}, name: "REG_I_18_FIELD_1")
                    .WithFlag(17, out field_325, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue20);}}, name: "REG_I_18_FIELD_2")
                    .WithFlag(18, out field_321, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue21);}}, name: "REG_I_18_FIELD_3")
                    .WithFlag(19, out field_317, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue22);}}, name: "REG_I_18_FIELD_4")
                    .WithFlag(20, out field_329, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue23);}}, name: "REG_I_18_FIELD_5")
                    .WithFlag(21, out field_341, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue17);}}, name: "REG_I_18_FIELD_6")
                    .WithFlag(22, out field_337, name: "REG_I_18_FIELD_7")
                    .WithFlag(23, out field_345, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue19);}}, name: "REG_I_18_FIELD_8")
                    .WithFlag(24, out field_335, changeCallback: (_, value) => {if (!value) {RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue24);}}, name: "REG_I_18_FIELD_9")
                    .WithReservedBits(25, 7)
                },
                {(long)Registers_I.RegI_19, new DoubleWordRegister(this)
                    .WithReservedBits(0, 16)
                    .WithFlag(16, out field_314, name: "REG_I_19_FIELD_1")
                    .WithFlag(17, out field_326, name: "REG_I_19_FIELD_2")
                    .WithFlag(18, out field_322, name: "REG_I_19_FIELD_3")
                    .WithFlag(19, out field_318, name: "REG_I_19_FIELD_4")
                    .WithFlag(20, out field_330, name: "REG_I_19_FIELD_5")
                    .WithFlag(21, out field_342, name: "REG_I_19_FIELD_6")
                    .WithFlag(22, out field_338, name: "REG_I_19_FIELD_7")
                    .WithFlag(23, out field_346, name: "REG_I_19_FIELD_8")
                    .WithFlag(24, out field_336, name: "REG_I_19_FIELD_9")
                    .WithReservedBits(25, 7)
                },
                {(long)Registers_I.RegI_25, new DoubleWordRegister(this)
                    .WithTag("REG_I_25_FIELD_1", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_I.RegI_26, new DoubleWordRegister(this)
                    .WithTag("REG_I_26_FIELD_1", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_I.RegI_27, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_I_27_FIELD_1", 0)
                    .WithTag("REG_I_27_FIELD_2", 1, 2)
                    .WithTaggedFlag("REG_I_27_FIELD_3", 3)
                    .WithTaggedFlag("REG_I_27_FIELD_4", 4)
                    .WithTaggedFlag("REG_I_27_FIELD_5", 5)
                    .WithTaggedFlag("REG_I_27_FIELD_6", 6)
                    .WithReservedBits(7, 17)
                    .WithTag("REG_I_27_FIELD_7", 24, 2)
                    .WithReservedBits(26, 6)
                },
                {(long)Registers_I.RegI_28, new DoubleWordRegister(this, 0x7)
                    .WithTag("REG_I_28_FIELD_1", 0, 7)
                    .WithReservedBits(7, 25)
                },
                {(long)Registers_I.RegI_29, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_351[0], name: "REG_I_29_FIELD_1")
                },
                {(long)Registers_I.RegI_30, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_351[1], name: "REG_I_30_FIELD_1")
                },
                {(long)Registers_I.RegI_31, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_351[2], name: "REG_I_31_FIELD_1")
                },
                {(long)Registers_I.RegI_32, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_351[3], name: "REG_I_32_FIELD_1")
                },
                {(long)Registers_I.RegI_38, new DoubleWordRegister(this)
                    .WithFlag(0, out field_302, FieldMode.Read, name: "REG_I_38_FIELD_1")
                    .WithReservedBits(1, 31)
                },
                {(long)Registers_I.RegI_41, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => { return RAC_PaOutputLevelRamping; }, writeCallback: (_, value) => { RAC_PaOutputLevelRamping = value; }, name: "REG_I_41_FIELD_1")
                    .WithTaggedFlag("REG_I_41_FIELD_2", 1)
                    .WithTaggedFlag("REG_I_41_FIELD_3", 2)
                    .WithTaggedFlag("REG_I_41_FIELD_4", 3)
                    .WithTaggedFlag("REG_I_41_FIELD_5", 4)
                    .WithReservedBits(5, 27)
                },
                {(long)Registers_I.RegI_131, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[0], name: "REG_I_131_FIELD_1")
                },
                {(long)Registers_I.RegI_132, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[1], name: "REG_I_132_FIELD_1")
                },
                {(long)Registers_I.RegI_133, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[2], name: "REG_I_133_FIELD_1")
                },
                {(long)Registers_I.RegI_134, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[3], name: "REG_I_134_FIELD_1")
                },
                {(long)Registers_I.RegI_135, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[4], name: "REG_I_135_FIELD_1")
                },
                {(long)Registers_I.RegI_136, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[5], name: "REG_I_136_FIELD_1")
                },
                {(long)Registers_I.RegI_137, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[6], name: "REG_I_137_FIELD_1")
                },
                {(long)Registers_I.RegI_138, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out field_306[7], name: "REG_I_138_FIELD_1")
                },
                {(long)Registers_I.RegI_144, new DoubleWordRegister(this, 0x80000000)
                    .WithTag("REG_I_144_FIELD_1", 0, 10)
                    .WithReservedBits(10, 11)
                    .WithTaggedFlag("REG_I_144_FIELD_2", 21)
                    .WithTaggedFlag("REG_I_144_FIELD_3", 22)
                    .WithTaggedFlag("REG_I_144_FIELD_4", 23)
                    .WithTaggedFlag("REG_I_144_FIELD_5", 24)
                    .WithTaggedFlag("REG_I_144_FIELD_6", 25)
                    .WithTaggedFlag("REG_I_144_FIELD_7", 26)
                    .WithReservedBits(27, 1)
                    .WithTaggedFlag("REG_I_144_FIELD_8", 28)
                    .WithTaggedFlag("REG_I_144_FIELD_9", 29)
                    .WithTaggedFlag("REG_I_144_FIELD_10", 30)
                    .WithFlag(31, out field_359, name: "REG_I_144_FIELD_11")
                },
                {(long)Registers_I.RegI_145, new DoubleWordRegister(this, 0x00000FFF)
                    .WithTag("REG_I_145_FIELD_1", 0, 5)
                    .WithTag("REG_I_145_FIELD_2", 5, 7)
                    .WithReservedBits(12, 20)
                },
                {(long)Registers_I.RegI_44, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) RAC_dcCalDone = true; }, name: "REG_I_44_FIELD_1")
                    .WithTaggedFlag("REG_I_44_FIELD_2", 1)
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => RAC_dcCalDone, name: "REG_I_44_FIELD_3")
                    .WithTag("REG_I_44_FIELD_4", 3, 4)
                    .WithTag("REG_I_44_FIELD_5", 7, 4)
                    .WithTaggedFlag("REG_I_44_FIELD_6", 11)
                    .WithTaggedFlag("REG_I_44_FIELD_7", 12)
                    .WithReservedBits(13, 7)
                    .WithTag("REG_I_44_FIELD_8", 20, 6)
                    .WithTag("REG_I_44_FIELD_9", 26, 6)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildSynthesizerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_K.RegK_3, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_K_3_FIELD_1", 0)
                    .WithTaggedFlag("REG_K_3_FIELD_2", 1)
                    .WithFlag(2, out field_367, name: "REG_K_3_FIELD_3")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("REG_K_3_FIELD_4", 4)
                    .WithTaggedFlag("REG_K_3_FIELD_5", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("REG_K_3_FIELD_6", 9)
                    .WithFlag(10, out field_364, name: "REG_K_3_FIELD_7")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_K.RegK_4, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_K_4_FIELD_1", 0)
                    .WithTaggedFlag("REG_K_4_FIELD_2", 1)
                    .WithFlag(2, out field_368, name: "REG_K_4_FIELD_3")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("REG_K_4_FIELD_4", 4)
                    .WithTaggedFlag("REG_K_4_FIELD_5", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("REG_K_4_FIELD_6", 9)
                    .WithFlag(10, out field_365, name: "REG_K_4_FIELD_7")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_K.RegK_5, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_K_5_FIELD_1", 0)
                    .WithTaggedFlag("REG_K_5_FIELD_2", 1)
                    .WithFlag(2, out field_371, name: "REG_K_5_FIELD_3")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("REG_K_5_FIELD_4", 4)
                    .WithTaggedFlag("REG_K_5_FIELD_5", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("REG_K_5_FIELD_6", 9)
                    .WithFlag(10, out field_369, name: "REG_K_5_FIELD_7")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_K.RegK_6, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_K_6_FIELD_1", 0)
                    .WithTaggedFlag("REG_K_6_FIELD_2", 1)
                    .WithFlag(2, out field_372, name: "REG_K_6_FIELD_3")
                    .WithReservedBits(3, 1)
                    .WithTaggedFlag("REG_K_6_FIELD_4", 4)
                    .WithTaggedFlag("REG_K_6_FIELD_5", 5)
                    .WithReservedBits(6, 3)
                    .WithTaggedFlag("REG_K_6_FIELD_6", 9)
                    .WithFlag(10, out field_370, name: "REG_K_6_FIELD_7")
                    .WithReservedBits(11, 21)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_K.RegK_10, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_Start(); }, name: "REG_K_10_FIELD_1")
                    .WithFlag(1, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_Stop(); }, name: "REG_K_10_FIELD_2")
                    .WithFlag(2, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_EnableIf(); }, name: "REG_K_10_FIELD_3")
                    .WithFlag(3, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_DisableIf(); }, name: "REG_K_10_FIELD_4")
                    .WithFlag(4, FieldMode.Set, writeCallback: (_, value) => { if (value) SYNTH_CalibrationStart(); }, name: "REG_K_10_FIELD_5")
                    .WithReservedBits(5, 4)
                    .WithTaggedFlag("REG_K_10_FIELD_6", 9)
                    .WithTaggedFlag("REG_K_10_FIELD_7", 10)
                    .WithReservedBits(11, 21)
                },
                {(long)Registers_K.RegK_9, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_K_9_FIELD_1", 0)
                    .WithFlag(1, out field_366, FieldMode.Read, name: "REG_K_9_FIELD_2")
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => (SYNTH_state == Enumeration_AD.EnumerationADValue1), name: "REG_K_9_FIELD_3")
                    .WithTaggedFlag("REG_K_9_FIELD_4", 3)
                    .WithTag("REG_K_9_FIELD_5", 4, 13)
                    .WithReservedBits(17, 15)
                },
                {(long)Registers_K.RegK_17, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => (ulong)Channel, writeCallback: (_, value) => { Channel = (int)value; }, name: "REG_K_17_FIELD_1")
                    .WithReservedBits(16, 15)
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildCyclicRedundancyCheckRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_B.RegB_3, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_B_3_FIELD_1", 0)
                    .WithTaggedFlag("REG_B_3_FIELD_2", 1)
                    .WithEnumField<DoubleWordRegister, Enumeration_D>(2, 2, out field_47, name: "REG_B_3_FIELD_3")
                    .WithReservedBits(4, 1)
                    .WithTaggedFlag("REG_B_3_FIELD_4", 5)
                    .WithFlag(6, out field_48, name: "REG_B_3_FIELD_5")
                    .WithTaggedFlag("REG_B_3_FIELD_6", 7)
                    .WithValueField(8, 4, out field_46, name: "REG_B_3_FIELD_7")
                    .WithTaggedFlag("REG_B_3_FIELD_8", 12)
                    .WithReservedBits(13, 19)
                },
            };

            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildFrameControllerRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers_C.RegC_3, new DoubleWordRegister(this)
                    .WithTag("REG_C_3_FIELD_1", 0, 5)
                    .WithFlag(5, out field_59, FieldMode.Read, name: "REG_C_3_FIELD_2")
                    .WithFlag(6, out field_58, FieldMode.Read, name: "REG_C_3_FIELD_3")
                    .WithTaggedFlag("REG_C_3_FIELD_4", 7)
                    .WithFlag(8, out field_108, FieldMode.Read, name: "REG_C_3_FIELD_5")
                    .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => true, name: "REG_C_3_FIELD_6")
                    .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "REG_C_3_FIELD_7")
                    .WithTaggedFlag("REG_C_3_FIELD_8", 11)
                    .WithTaggedFlag("REG_C_3_FIELD_9", 12)
                    .WithTaggedFlag("REG_C_3_FIELD_10", 13)
                    .WithTaggedFlag("REG_C_3_FIELD_11", 14)
                    .WithTaggedFlag("REG_C_3_FIELD_12", 15)
                    .WithTaggedFlag("REG_C_3_FIELD_13", 16)
                    .WithTaggedFlag("REG_C_3_FIELD_14", 17)
                    .WithTaggedFlag("REG_C_3_FIELD_15", 18)
                    .WithTaggedFlag("REG_C_3_FIELD_16", 19)
                    .WithValueField(20, 5, out field_71, name: "REG_C_3_FIELD_17")
                    .WithTaggedFlag("REG_C_3_FIELD_18", 25)
                    .WithTaggedFlag("REG_C_3_FIELD_19", 26)
                    .WithReservedBits(27, 5)
                },
                {(long)Registers_C.RegC_4, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Enumeration_F>(0, 3, out field_65, name: "REG_C_4_FIELD_1")
                    .WithEnumField<DoubleWordRegister, Enumeration_E>(3, 1, out field_62, name: "REG_C_4_FIELD_2")
                    .WithValueField(4, 3, out field_63, name: "REG_C_4_FIELD_3")
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 4, out field_66, name: "REG_C_4_FIELD_4")
                    .WithValueField(12, 4, out field_64, name: "REG_C_4_FIELD_5")
                    .WithValueField(16, 4, out field_75, name: "REG_C_4_FIELD_6")
                    .WithFlag(20, out field_61, name: "REG_C_4_FIELD_7")
                    .WithTag("REG_C_4_FIELD_8", 21, 4)
                    .WithReservedBits(25, 7)
                },
                {(long)Registers_C.RegC_5, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out field_74, name: "REG_C_5_FIELD_1")
                    .WithValueField(12, 4, out field_72, name: "REG_C_5_FIELD_2")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers_C.RegC_8, new DoubleWordRegister(this)
                    .WithValueField(0 ,12, out field_145, FieldMode.Read, name: "REG_C_8_FIELD_1")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers_C.RegC_9, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out field_70, name: "REG_C_9_FIELD_1")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers_C.RegC_10, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out field_73, name: "REG_C_10_FIELD_1")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers_C.RegC_11, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out field_60, name: "REG_C_11_FIELD_1")
                    .WithReservedBits(12, 20)
                },
                {(long)Registers_C.RegC_12, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => {if (value) FRC_RxAbortCommand(); }, name: "REG_C_12_FIELD_1")
                    .WithTaggedFlag("REG_C_12_FIELD_2", 1)
                    .WithTaggedFlag("REG_C_12_FIELD_3", 2)
                    .WithTaggedFlag("REG_C_12_FIELD_4", 3)
                    .WithTaggedFlag("REG_C_12_FIELD_5", 4)
                    .WithTaggedFlag("REG_C_12_FIELD_6", 5)
                    .WithTaggedFlag("REG_C_12_FIELD_7", 6)
                    .WithTaggedFlag("REG_C_12_FIELD_8", 7)
                    .WithTaggedFlag("REG_C_12_FIELD_9", 8)
                    .WithTaggedFlag("REG_C_12_FIELD_10", 9)
                    .WithTaggedFlag("REG_C_12_FIELD_11", 10)
                    .WithTaggedFlag("REG_C_12_FIELD_12", 11)
                    .WithFlag(12, FieldMode.Set, writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                field_108.Value = false;
                                FRC_UpdateRawMode();
                            }
                        }, name: "REG_C_12_FIELD_13")
                    .WithTaggedFlag("REG_C_12_FIELD_14", 13)
                    .WithReservedBits(14, 18)
                },
                {(long)Registers_C.RegC_19, new DoubleWordRegister(this)
                    .WithTaggedFlag("REG_C_19_FIELD_1", 0)
                    .WithTaggedFlag("REG_C_19_FIELD_2", 1)
                    .WithTaggedFlag("REG_C_19_FIELD_3", 2)
                    .WithTaggedFlag("REG_C_19_FIELD_4", 3)
                    .WithEnumField<DoubleWordRegister, Enumeration_H>(4, 2, out field_140, name: "REG_C_19_FIELD_5")
                    .WithEnumField<DoubleWordRegister, Enumeration_H>(6, 2, out field_105, name: "REG_C_19_FIELD_6")
                    .WithTag("REG_C_19_FIELD_7", 8, 3) 
                    .WithTag("REG_C_19_FIELD_8", 11, 2)
                    .WithTaggedFlag("REG_C_19_FIELD_9", 13)
                    .WithTaggedFlag("REG_C_19_FIELD_10", 14)
                    .WithTaggedFlag("REG_C_19_FIELD_11", 15)
                    .WithTaggedFlag("REG_C_19_FIELD_12", 16)
                    .WithTaggedFlag("REG_C_19_FIELD_13", 17)
                    .WithTaggedFlag("REG_C_19_FIELD_14", 18)
                    .WithTaggedFlag("REG_C_19_FIELD_15", 19)
                    .WithTaggedFlag("REG_C_19_FIELD_16", 20)
                    .WithTaggedFlag("REG_C_19_FIELD_17", 21)
                    .WithTag("REG_C_19_FIELD_18", 22, 2)
                    .WithTaggedFlag("REG_C_19_FIELD_19", 24)
                    .WithTaggedFlag("REG_C_19_FIELD_20", 25)
                    .WithTaggedFlag("REG_C_19_FIELD_21", 26)
                    .WithTag("REG_C_19_FIELD_22", 27, 3)
                    .WithTaggedFlag("REG_C_19_FIELD_23", 30)
                    .WithTaggedFlag("REG_C_19_FIELD_24", 31)
                },
                {(long)Registers_C.RegC_20, new DoubleWordRegister(this)
                    .WithFlag(0, out field_113, name: "REG_C_20_FIELD_1")
                    .WithFlag(1, out field_93, name: "REG_C_20_FIELD_2")
                    .WithTaggedFlag("REG_C_20_FIELD_3", 2)
                    .WithTaggedFlag("REG_C_20_FIELD_4", 3) 
                    .WithFlag(4, out field_100, name: "REG_C_20_FIELD_5")
                    .WithFlag(5, out field_101, name: "REG_C_20_FIELD_6")
                    .WithFlag(6, out field_102, name: "REG_C_20_FIELD_7")
                    .WithTag("REG_C_20_FIELD_8", 7, 4)
                    .WithTaggedFlag("REG_C_20_FIELD_9", 11)
                    .WithReservedBits(12, 4)
                    .WithTag("REG_C_20_FIELD_10", 16, 2)
                    .WithReservedBits(18, 14)
                },
                {(long)Registers_C.RegC_22, new DoubleWordRegister(this)
                    .WithFlag(0, out field_98, name: "REG_C_22_FIELD_1")
                    .WithFlag(1, out field_99, name: "REG_C_22_FIELD_2")
                    .WithFlag(2, out field_95, name: "REG_C_22_FIELD_3")
                    .WithFlag(3, out field_94, name: "REG_C_22_FIELD_4")
                    .WithFlag(4, out field_97, name: "REG_C_22_FIELD_5")
                    .WithFlag(5, out field_96, name: "REG_C_22_FIELD_6")
                    .WithTaggedFlag("REG_C_22_FIELD_7", 6)
                    .WithReservedBits(7, 25)
                },
                {(long)Registers_C.RegC_27, new DoubleWordRegister(this)
                    .WithFlag(0, out field_138, name: "REG_C_27_FIELD_1")
                    .WithFlag(1, out field_136, name: "REG_C_27_FIELD_2")
                    .WithTaggedFlag("REG_C_27_FIELD_3", 2)
                    .WithFlag(3, out field_143, name: "REG_C_27_FIELD_4")
                    .WithFlag(4, out field_103, name: "REG_C_27_FIELD_5")
                    .WithFlag(5, out field_91, name: "REG_C_27_FIELD_6")
                    .WithFlag(6, out field_68, name: "REG_C_27_FIELD_7")
                    .WithTaggedFlag("REG_C_27_FIELD_8", 7)
                    .WithFlag(8, out field_106, name: "REG_C_27_FIELD_9")
                    .WithTaggedFlag("REG_C_27_FIELD_10", 9)
                    .WithTaggedFlag("REG_C_27_FIELD_11", 10)
                    .WithTaggedFlag("REG_C_27_FIELD_12", 11)
                    .WithTaggedFlag("REG_C_27_FIELD_13", 12)
                    .WithTaggedFlag("REG_C_27_FIELD_14", 13)
                    .WithFlag(14, out field_111, name: "REG_C_27_FIELD_15")
                    .WithFlag(15, out field_141, name: "REG_C_27_FIELD_16")
                    .WithTaggedFlag("REG_C_27_FIELD_17", 16)
                    .WithTaggedFlag("REG_C_27_FIELD_18", 17)
                    .WithTaggedFlag("REG_C_27_FIELD_19", 18)
                    .WithTaggedFlag("REG_C_27_FIELD_20", 19)
                    .WithFlag(20, out field_78, name: "REG_C_27_FIELD_21")
                    .WithFlag(21, out field_83, name: "REG_C_27_FIELD_22")
                    .WithTaggedFlag("REG_C_27_FIELD_23", 22)
                    .WithTaggedFlag("REG_C_27_FIELD_24", 23)
                    .WithTaggedFlag("REG_C_27_FIELD_25", 24)
                    .WithTaggedFlag("REG_C_27_FIELD_26", 25)
                    .WithTaggedFlag("REG_C_27_FIELD_27", 26)
                    .WithTaggedFlag("REG_C_27_FIELD_28", 27)
                    .WithTaggedFlag("REG_C_27_FIELD_29", 28)
                    .WithTaggedFlag("REG_C_27_FIELD_30", 29)
                    .WithTaggedFlag("REG_C_27_FIELD_31", 30)
                    .WithTaggedFlag("REG_C_27_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_C.RegC_28, new DoubleWordRegister(this)
                    .WithFlag(0, out field_139, name: "REG_C_28_FIELD_1")
                    .WithFlag(1, out field_137, name: "REG_C_28_FIELD_2")
                    .WithTaggedFlag("REG_C_28_FIELD_3", 2)
                    .WithFlag(3, out field_144, name: "REG_C_28_FIELD_4")
                    .WithFlag(4, out field_104, name: "REG_C_28_FIELD_5")
                    .WithFlag(5, out field_92, name: "REG_C_28_FIELD_6")
                    .WithFlag(6, out field_69, name: "REG_C_28_FIELD_7")
                    .WithTaggedFlag("REG_C_28_FIELD_8", 7)
                    .WithFlag(8, out field_107, name: "REG_C_28_FIELD_9")
                    .WithTaggedFlag("REG_C_28_FIELD_10", 9)
                    .WithTaggedFlag("REG_C_28_FIELD_11", 10)
                    .WithTaggedFlag("REG_C_28_FIELD_12", 11)
                    .WithTaggedFlag("REG_C_28_FIELD_13", 12)
                    .WithTaggedFlag("REG_C_28_FIELD_14", 13)
                    .WithFlag(14, out field_112, name: "REG_C_28_FIELD_15")
                    .WithFlag(15, out field_142, name: "REG_C_28_FIELD_16")
                    .WithTaggedFlag("REG_C_28_FIELD_17", 16)
                    .WithTaggedFlag("REG_C_28_FIELD_18", 17)
                    .WithTaggedFlag("REG_C_28_FIELD_19", 18)
                    .WithTaggedFlag("REG_C_28_FIELD_20", 19)
                    .WithFlag(20, out field_79, name: "REG_C_28_FIELD_21")
                    .WithFlag(21, out field_84, name: "REG_C_28_FIELD_22")
                    .WithTaggedFlag("REG_C_28_FIELD_23", 22)
                    .WithTaggedFlag("REG_C_28_FIELD_24", 23)
                    .WithTaggedFlag("REG_C_28_FIELD_25", 24)
                    .WithTaggedFlag("REG_C_28_FIELD_26", 25)
                    .WithTaggedFlag("REG_C_28_FIELD_27", 26)
                    .WithTaggedFlag("REG_C_28_FIELD_28", 27)
                    .WithTaggedFlag("REG_C_28_FIELD_29", 28)
                    .WithTaggedFlag("REG_C_28_FIELD_30", 29)
                    .WithTaggedFlag("REG_C_28_FIELD_31", 30)
                    .WithTaggedFlag("REG_C_28_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_C.RegC_42, new DoubleWordRegister(this)
                    .WithFlag(0, out field_130, name: "REG_C_42_FIELD_1")
                    .WithFlag(1, out field_128, name: "REG_C_42_FIELD_2")
                    .WithTaggedFlag("REG_C_42_FIELD_3", 2)
                    .WithFlag(3, out field_134, name: "REG_C_42_FIELD_4")
                    .WithFlag(4, out field_122, name: "REG_C_42_FIELD_5")
                    .WithFlag(5, out field_120, name: "REG_C_42_FIELD_6")
                    .WithFlag(6, out field_114, name: "REG_C_42_FIELD_7")
                    .WithTaggedFlag("REG_C_42_FIELD_8", 7)
                    .WithFlag(8, out field_124, name: "REG_C_42_FIELD_9")
                    .WithTaggedFlag("REG_C_42_FIELD_10", 9)
                    .WithTaggedFlag("REG_C_42_FIELD_11", 10)
                    .WithTaggedFlag("REG_C_42_FIELD_12", 11)
                    .WithTaggedFlag("REG_C_42_FIELD_13", 12)
                    .WithTaggedFlag("REG_C_42_FIELD_14", 13)
                    .WithFlag(14, out field_126, name: "REG_C_42_FIELD_15")
                    .WithFlag(15, out field_132, name: "REG_C_42_FIELD_16")
                    .WithTaggedFlag("REG_C_42_FIELD_17", 16)
                    .WithTaggedFlag("REG_C_42_FIELD_18", 17)
                    .WithTaggedFlag("REG_C_42_FIELD_19", 18)
                    .WithTaggedFlag("REG_C_42_FIELD_20", 19)
                    .WithFlag(20, out field_116, name: "REG_C_42_FIELD_21")
                    .WithFlag(21, out field_118, name: "REG_C_42_FIELD_22")
                    .WithTaggedFlag("REG_C_42_FIELD_23", 22)
                    .WithTaggedFlag("REG_C_42_FIELD_24", 23)
                    .WithTaggedFlag("REG_C_42_FIELD_25", 24)
                    .WithTaggedFlag("REG_C_42_FIELD_26", 25)
                    .WithTaggedFlag("REG_C_42_FIELD_27", 26)
                    .WithTaggedFlag("REG_C_42_FIELD_28", 27)
                    .WithTaggedFlag("REG_C_42_FIELD_29", 28)
                    .WithTaggedFlag("REG_C_42_FIELD_30", 29)
                    .WithTaggedFlag("REG_C_42_FIELD_31", 30)
                    .WithTaggedFlag("REG_C_42_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_C.RegC_43, new DoubleWordRegister(this)
                    .WithFlag(0, out field_131, name: "REG_C_43_FIELD_1")
                    .WithFlag(1, out field_129, name: "REG_C_43_FIELD_2")
                    .WithTaggedFlag("REG_C_43_FIELD_3", 2)
                    .WithFlag(3, out field_135, name: "REG_C_43_FIELD_4")
                    .WithFlag(4, out field_123, name: "REG_C_43_FIELD_5")
                    .WithFlag(5, out field_121, name: "REG_C_43_FIELD_6")
                    .WithFlag(6, out field_115, name: "REG_C_43_FIELD_7")
                    .WithTaggedFlag("REG_C_43_FIELD_8", 7)
                    .WithFlag(8, out field_125, name: "REG_C_43_FIELD_9")
                    .WithTaggedFlag("REG_C_43_FIELD_10", 9)
                    .WithTaggedFlag("REG_C_43_FIELD_11", 10)
                    .WithTaggedFlag("REG_C_43_FIELD_12", 11)
                    .WithTaggedFlag("REG_C_43_FIELD_13", 12)
                    .WithTaggedFlag("REG_C_43_FIELD_14", 13)
                    .WithFlag(14, out field_127, name: "REG_C_43_FIELD_15")
                    .WithFlag(15, out field_133, name: "REG_C_43_FIELD_16")
                    .WithTaggedFlag("REG_C_43_FIELD_17", 16)
                    .WithTaggedFlag("REG_C_43_FIELD_18", 17)
                    .WithTaggedFlag("REG_C_43_FIELD_19", 18)
                    .WithTaggedFlag("REG_C_43_FIELD_20", 19)
                    .WithFlag(20, out field_117, name: "REG_C_43_FIELD_21")
                    .WithFlag(21, out field_119, name: "REG_C_43_FIELD_22")
                    .WithTaggedFlag("REG_C_43_FIELD_23", 22)
                    .WithTaggedFlag("REG_C_43_FIELD_24", 23)
                    .WithTaggedFlag("REG_C_43_FIELD_25", 24)
                    .WithTaggedFlag("REG_C_43_FIELD_26", 25)
                    .WithTaggedFlag("REG_C_43_FIELD_27", 26)
                    .WithTaggedFlag("REG_C_43_FIELD_28", 27)
                    .WithTaggedFlag("REG_C_43_FIELD_29", 28)
                    .WithTaggedFlag("REG_C_43_FIELD_30", 29)
                    .WithTaggedFlag("REG_C_43_FIELD_31", 30)
                    .WithTaggedFlag("REG_C_43_FIELD_32", 31)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_C.RegC_33, new DoubleWordRegister(this)
                    .WithTag("REG_C_33_FIELD_1", 0, 2)
                    .WithEnumField<DoubleWordRegister, Enumeration_I>(2, 3, out field_109, writeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case Enumeration_I.EnumerationIValue0:
                                    field_108.Value = false;
                                    break;
                                case Enumeration_I.EnumerationIValue1:
                                    break;
                                default:
                                    this.Log(LogLevel.Error, "Unsupported RXRAWMODE value ({0}).", value);
                                    break;
                            }
                        }, name: "REG_C_33_FIELD_2")
                    .WithFlag(5, out field_67, name: "REG_C_33_FIELD_3")
                    .WithReservedBits(6, 1)
                    .WithEnumField<DoubleWordRegister, Enumeration_J>(7, 2, out field_110, writeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case Enumeration_J.EnumerationJValue0:
                                    break;
                                default:
                                    this.Log(LogLevel.Error, "Unsupported RXRAWTRIGGER value ({0}).", value);
                                    break;
                            }
                        }, name: "REG_C_33_FIELD_4")
                    .WithReservedBits(9, 4)
                    .WithTaggedFlag("REG_C_33_FIELD_5", 13)
                    .WithReservedBits(14, 18)
                    .WithChangeCallback((_, __) => FRC_UpdateRawMode())
                },
                {(long)Registers_C.RegC_34, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(field_109.Value == Enumeration_I.EnumerationIValue1)
                        {
                            field_108.Value = false;
                        }
                        FRC_UpdateRawMode();
                        return (uint)random.Next();
                    }, name: "REG_C_34_FIELD_1")
                },
                {(long)Registers_C.RegC_49, new DoubleWordRegister(this)
                    .WithValueField(0, 12, out field_77, name: "REG_C_49_FIELD_1")
                    .WithValueField(12, 6, out field_81, writeCallback: (_, __) => { UpdateInterrupts(); }, name: "REG_C_49_FIELD_2")
                    .WithReservedBits(18, 6)
                    .WithFlag(24, out field_82, writeCallback: (_, __) => { UpdateInterrupts(); }, name: "REG_C_49_FIELD_3")
                    .WithFlag(25, out field_80, FieldMode.Write, name: "REG_C_49_FIELD_4")
                    .WithReservedBits(26, 6)
                },
                {(long)Registers_C.RegC_50, new DoubleWordRegister(this)
                    .WithValueField(0, 6, out field_76, FieldMode.Read, name: "REG_C_50_FIELD_1")
                    .WithReservedBits(6, 24)
                },
                {(long)Registers_C.RegC_31, new DoubleWordRegister(this)
                    .WithTag("REG_C_31_FIELD_1", 0, 2)
                    .WithTaggedFlag("REG_C_31_FIELD_2", 2)
                    .WithFlag(3, out field_87, name: "REG_C_31_FIELD_3")
                    .WithFlag(4, out field_90, name: "REG_C_31_FIELD_4")
                    .WithFlag(5, out field_86, name: "REG_C_31_FIELD_5")
                    .WithFlag(6, out field_88, name: "REG_C_31_FIELD_6")
                    .WithFlag(7, out field_85, name: "REG_C_31_FIELD_7")
                    .WithTag("REG_C_31_FIELD_8", 8, 8)
                    .WithTaggedFlag("REG_C_31_FIELD_9", 16)
                    .WithFlag(17, out field_89, name: "REG_C_31_FIELD_10")
                    .WithTaggedFlag("REG_C_31_FIELD_11", 18)
                    .WithTaggedFlag("REG_C_31_FIELD_12", 19)
                    .WithTaggedFlag("REG_C_31_FIELD_13", 20)
                    .WithReservedBits(21, 3)
                    .WithTag("REG_C_31_FIELD_14", 24, 8)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers_C.RegC_32, new DoubleWordRegister(this)
                    .WithValueField(0, 9, writeCallback: (_, data) => {
                        if (field_85.Value)
                        {
                            byte b = (byte) (data & 0xFF);
                            PtiDataOut?.Invoke(new byte[] {b });
                        }
                        if ((data & 0x0100) == 0u) {
                            PtiFrameComplete?.Invoke();
                        }
                    }, name: "REG_C_32_FIELD_1")
                    .WithReservedBits(9, 23)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            var startOffset = (long)Registers_C.RegC_65;
            var blockSize = (long)Registers_C.RegC_66 - (long)Registers_C.RegC_65;
            for(var index = 0; index < FRC_NumberOfFrameDescriptors; index++)
            {
                var i = index;

                registerDictionary.Add(startOffset + blockSize * i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 8, out FRC_frameDescriptor[i].Field_387, name: "REG_C_32_FIELD_2")
                        .WithValueField(8, 2, out FRC_frameDescriptor[i].Field_45, name: "REG_C_32_FIELD_3")
                        .WithFlag(10, out FRC_frameDescriptor[i].Field_152, name: "REG_C_32_FIELD_4")
                        .WithFlag(11, out FRC_frameDescriptor[i].Field_49, name: "REG_C_32_FIELD_5")
                        .WithValueField(12, 2, out FRC_frameDescriptor[i].Field_55, name: "REG_C_32_FIELD_6")
                        .WithFlag(14, out FRC_frameDescriptor[i].Field_381, name: "REG_C_32_FIELD_7")
                        .WithFlag(15, out FRC_frameDescriptor[i].Field_40, name: "REG_C_32_FIELD_8")
                        .WithFlag(16, out FRC_frameDescriptor[i].Field_57, name: "REG_C_32_FIELD_9")
                        .WithReservedBits(17, 15)
                );
            }

            startOffset = (long)Registers_C.RegC_51;
            blockSize = (long)Registers_C.RegC_52 - (long)Registers_C.RegC_51;
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
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private void PROTIMER_UpdateTxRequestState()
        {
            if(field_284.Value == PROTIMER_Event.Disabled
                && field_285.Value == PROTIMER_Event.Disabled)
            {
                PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue0;
            }
        }

        private void PROTIMER_TimeoutCounter0HandleSynchronize()
        {
            if(field_255.Value)
            {
                field_255.Value = false;
                field_251.Value = true;
            }
        }

        private void PROTIMER_ListenBeforeTalkCcaCompleted(bool forceFailure = false)
        {
            if(PROTIMER_listenBeforeTalkState == Enumeration_S.EnumerationSValue0)
            {
                throw new Exception("PROTIMER_ListenBeforeTalkCcaCompleted while LBT_STATE=idle");
            }

            if(forceFailure)
            {
                field_1.Value = false;
            }
            else
            {
                field_1.Value = (AGC_RssiIntegerPartAdjusted < (sbyte)field_11.Value);
            }


            if(!field_1.Value)
            {
                PROTIMER_timeoutCounter[0].Stop();
                PROTIMER_TriggerEvent(PROTIMER_Event.ClearChannelAssessmentMeasurementCompleted);

                if(field_248.Value == field_265.Value)
                {
                    PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue0;
                    field_255.Value = false;
                    field_251.Value = false;
                    field_244.Value = true;
                    field_272.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_Event.ListenBeforeTalkFailure);
                }
                else
                {
                    field_249.Value = true;
                    field_274.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_Event.ListenBeforeTalkRetry);

                    PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue1;

                    field_248.Value += 1;

                    if(field_243.Value + 1 <= field_246.Value)
                    {
                        field_243.Value += 1;
                    }
                    else
                    {
                        field_243.Value = field_246.Value;
                    }

                    var rand = (uint)random.Next();
                    var backoff = (rand & ((1u << (byte)field_243.Value) - 1));

                    PROTIMER_timeoutCounter[0].Field_54.Value = backoff;
                    PROTIMER_timeoutCounter[0].Start();
                }
            }
        }

        private void PROTIMER_ListenBeforeTalkStopCommand()
        {
            PROTIMER_timeoutCounter[0].Stop();
            PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue0;
            field_255.Value = false;
            field_251.Value = false;
        }

        private void PROTIMER_ListenBeforeTalkPauseCommand()
        {
            throw new Exception("LBT Pausing not supported");
        }

        private void PROTIMER_ListenBeforeTalkStartCommand()
        {
            if(PROTIMER_timeoutCounter[0].Field_363.Value || PROTIMER_timeoutCounter[0].Field_384.Value)
            {
                PROTIMER_listenBeforeTalkPending = true;
                return;
            }

            field_255.Value = false;
            field_251.Value = false;
            field_247.Value = false;
            field_243.Value = field_252.Value;
            field_248.Value = 0;

            PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue1;

            if(PROTIMER_timeoutCounter[0].Field_383.Value == Enumeration_W.EnumerationWValue0)
            {
                field_251.Value = true;
            }
            else
            {
                field_255.Value = true;
            }


            var rand = (uint)random.Next();
            var backoff = (rand & ((1u << (byte)field_243.Value) - 1));

            PROTIMER_timeoutCounter[0].Field_54.Value = backoff;
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

        private void PROTIMER_TimeoutCounter0HandleUnderflow()
        {
            if(!field_251.Value)
            {
                return;
            }

            PROTIMER_timeoutCounter[0].Stop();

            switch(PROTIMER_listenBeforeTalkState)
            {
            case Enumeration_S.EnumerationSValue1:
            {
                field_239.Value = 0;

                PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue2;

                if(AGC_RssiStartCommand(true))
                {
                    PROTIMER_timeoutCounter[0].Field_54.Value = field_240.Value;
                    PROTIMER_timeoutCounter[0].Start();
                }

                break;
            }
            case Enumeration_S.EnumerationSValue2:
            {

                if(field_239.Value == (field_241.Value - 1))
                {
                    PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue0;
                    field_255.Value = false;
                    field_251.Value = false;
                    field_253.Value = true;
                    field_276.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_Event.ListenBeforeTalkSuccess);
                    PROTIMER_TriggerEvent(PROTIMER_Event.ClearChannelAssessmentMeasurementCompleted);
                }
                else
                {
                    field_239.Value += 1;

                    if(AGC_RssiStartCommand(true))
                    {
                        PROTIMER_timeoutCounter[0].Field_54.Value = field_240.Value;
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

        private void PROTIMER_IncrementBaseCounter(uint increment = 1)
        {
            if(proTimer.Enabled)
            {
                throw new Exception("PROTIMER_IncrementBaseCounter invoked while the proTimer running");
            }

            PROTIMER_baseCounterValue += increment;

            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                var triggered = PROTIMER_captureCompareChannel[i].Field_56.Value
                    && PROTIMER_captureCompareChannel[i].Field_43.Value
                    && PROTIMER_captureCompareChannel[i].Field_231.Value == Enumeration_Q.EnumerationQValue0
                    && PROTIMER_baseCounterValue == PROTIMER_captureCompareChannel[i].Field_44.Value;

                if(triggered)
                {
                    PROTIMER_captureCompareChannel[i].Field_154.Value = true;
                    PROTIMER_captureCompareChannel[i].Field_374.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                    CaptureCompareEvent.Invoke(i);
                }
            }

            if(PROTIMER_baseCounterValue >= field_238.Value)
            {
                PROTIMER_HandleBaseCounterOverflow();
                PROTIMER_baseCounterValue = 0x0;
            }
        }

        private void PROTIMER_HandleWrapCounterOverflow()
        {

            PROTIMER_TriggerEvent(PROTIMER_Event.WrapCounterOverflow);

            Array.ForEach(PROTIMER_timeoutCounter, x => x.Update(Enumeration_W.EnumerationWValue3));

            WrapCountOverflowsEvent.Invoke(1);
        }

        private uint MODEM_GetPreambleLengthInBits()
        {
            uint preambleLength = (uint)((field_158.Value + 1)*field_217.Value);
            return preambleLength;
        }

        private void PROTIMER_UpdateCompareTimer(int index)
        {
            if(PROTIMER_captureCompareChannel[index].Field_56.Value
                && PROTIMER_captureCompareChannel[index].Field_231.Value == Enumeration_Q.EnumerationQValue0
                && PROTIMER_captureCompareChannel[index].Field_294.Value)
            {
                this.Log(LogLevel.Error, "CC{0} PRE match enabled, NOT SUPPORTED!", index);
            }

            PROTIMER_HandleChangedParams();
        }

        private void PROTIMER_IncrementWrapCounter(uint increment = 1)
        {
            if(proTimer.Enabled)
            {
                throw new Exception("PROTIMER_IncrementWrapCounter invoked while the proTimer running");
            }

            PROTIMER_wrapCounterValue += increment;

            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                var triggered = PROTIMER_captureCompareChannel[i].Field_56.Value
                    && PROTIMER_captureCompareChannel[i].Field_390.Value
                    && PROTIMER_captureCompareChannel[i].Field_231.Value == Enumeration_Q.EnumerationQValue0
                    && PROTIMER_wrapCounterValue == PROTIMER_captureCompareChannel[i].Field_391.Value;

                if(triggered)
                {
                    PROTIMER_captureCompareChannel[i].Field_154.Value = true;
                    PROTIMER_captureCompareChannel[i].Field_374.Value = true;
                    UpdateInterrupts();
                    PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                    CaptureCompareEvent.Invoke(i);
                }
            }

            if(PROTIMER_wrapCounterValue >= field_289.Value)
            {
                PROTIMER_HandleWrapCounterOverflow();
                PROTIMER_wrapCounterValue = 0x0;
            }
        }

        private void PROTIMER_UpdateRxRequestState()
        {
            if(field_266.Value == PROTIMER_Event.Always
                && field_267.Value == PROTIMER_Event.Always)
            {
                PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue0;
            }
            else if(field_268.Value == PROTIMER_Event.Always
                     && field_269.Value == PROTIMER_Event.Always)
            {
                PROTIMER_TriggerEvent(PROTIMER_Event.InternalTrigger);
            }
        }

        private void PROTIMER_HandleTimerLimitReached()
        {
            proTimer.Enabled = false;



            proTimer.Value = 0;

            PROTIMER_HandlePreCntOverflows((uint)proTimer.Limit);

            proTimer.Limit = PROTIMER_ComputeTimerLimit();
            proTimer.Enabled = true;
        }

        private void PROTIMER_HandleBaseCounterOverflow()
        {

            PROTIMER_TriggerEvent(PROTIMER_Event.BaseCounterOverflow);

            if(field_288.Value == Enumeration_Y.EnumerationYValue2)
            {
                PROTIMER_IncrementWrapCounter();
            }

            Array.ForEach(PROTIMER_timeoutCounter, x => x.Update(Enumeration_W.EnumerationWValue2));

            BaseCountOverflowsEvent.Invoke(1);
        }

        private ulong PROTIMER_UsToPreCntOverflowTicks(double timeUs)
        {
            return Convert.ToUInt64((timeUs * (double)PROTIMER_GetPreCntOverflowFrequency()) / (double)MicrosecondFrequency);
        }

        private uint PROTIMER_GetPreCntOverflowFrequency()
        {
            double frequency = (double)HfxoFrequency / (field_262.Value + 1 + ((double)field_261.Value / 65536));
            return Convert.ToUInt32(frequency);
        }

        private uint PROTIMER_ComputeTimerLimit()
        {
            if(proTimer.Enabled)
            {
                throw new Exception("PROTIMER_ComputeTimerLimit invoked while the proTimer running");
            }

            uint limit = PROTIMER_DefaultTimerLimit;
            PROTIMER_preCounterSourcedBitmask = 0;

            if(field_237.Value == Enumeration_P.EnumerationPValue1)
            {
                if(PROTIMER_baseCounterValue > field_238.Value)
                {
                    this.Log(LogLevel.Error, "BASECNT > BASECNTTOP {0} {1}", PROTIMER_baseCounterValue, field_238.Value);
                    throw new Exception("BASECNT > BASECNTTOP");
                }

                uint temp = (uint)field_238.Value - PROTIMER_baseCounterValue;
                if(temp != 0 && temp < limit)
                {
                    limit = temp;
                }
                PROTIMER_preCounterSourcedBitmask |= (uint)Enumeration_T.EnumerationTValue0;
            }

            if(field_288.Value == Enumeration_Y.EnumerationYValue1)
            {
                if(PROTIMER_wrapCounterValue > field_289.Value)
                {
                    this.Log(LogLevel.Error, "WRAPCNT > WRAPCNTTOP {0} {1}", PROTIMER_wrapCounterValue, field_289.Value);
                    throw new Exception("WRAPCNT > WRAPCNTTOP");
                }

                uint temp = (uint)field_289.Value - PROTIMER_wrapCounterValue;
                if(temp != 0 && temp < limit)
                {
                    limit = temp;
                }
                PROTIMER_preCounterSourcedBitmask |= (uint)Enumeration_T.EnumerationTValue1;
            }

            for(int i = 0; i < PROTIMER_NumberOfTimeoutCounters; i++)
            {
                if((PROTIMER_timeoutCounter[i].Field_384.Value
                     && PROTIMER_timeoutCounter[i].Field_383.Value == Enumeration_W.EnumerationWValue1)
                    || (PROTIMER_timeoutCounter[i].Field_363.Value
                        && PROTIMER_timeoutCounter[i].Field_382.Value == Enumeration_W.EnumerationWValue1))
                {
                    limit = PROTIMER_MinimumTimeoutCounterDelay;
                    PROTIMER_preCounterSourcedBitmask |= ((uint)Enumeration_T.EnumerationTValue2 << i);
                }
            }

            for(int i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; i++)
            {
                if(PROTIMER_captureCompareChannel[i].Field_56.Value
                    && PROTIMER_captureCompareChannel[i].Field_231.Value == Enumeration_Q.EnumerationQValue0)
                {
                    if(PROTIMER_captureCompareChannel[i].Field_43.Value
                        && field_237.Value == Enumeration_P.EnumerationPValue1
                        && PROTIMER_captureCompareChannel[i].Field_44.Value > PROTIMER_baseCounterValue)
                    {
                        uint temp = (uint)(PROTIMER_captureCompareChannel[i].Field_44.Value - PROTIMER_baseCounterValue);
                        if(temp < limit)
                        {
                            limit = temp;
                        }
                        PROTIMER_preCounterSourcedBitmask |= ((uint)Enumeration_T.EnumerationTValue5 << i);
                    }

                    if(PROTIMER_captureCompareChannel[i].Field_390.Value
                        && field_288.Value == Enumeration_Y.EnumerationYValue1
                        && PROTIMER_captureCompareChannel[i].Field_391.Value > PROTIMER_wrapCounterValue)
                    {
                        uint temp = (uint)(PROTIMER_captureCompareChannel[i].Field_391.Value - PROTIMER_wrapCounterValue);
                        if(temp < limit)
                        {
                            limit = temp;
                        }
                        PROTIMER_preCounterSourcedBitmask |= ((uint)Enumeration_T.EnumerationTValue5 << i);
                    }
                }
            }

            return limit;
        }

        private double MODEM_GetSyncWordOverTheAirTimeUs()
        {
            return (double)MODEM_GetSyncWordLengthInBits() * 1000000 / (double)MODEM_GetDataRate();
        }

        private void PROTIMER_HandlePreCntOverflows(uint overflowCount)
        {
            if(proTimer.Enabled)
            {
                throw new Exception("PROTIMER_HandlePreCntOverflows invoked while the proTimer running");
            }


            if((PROTIMER_preCounterSourcedBitmask & (uint)Enumeration_T.EnumerationTValue0) > 0)
            {
                PROTIMER_IncrementBaseCounter(overflowCount);
            }
            if((PROTIMER_preCounterSourcedBitmask & (uint)Enumeration_T.EnumerationTValue1) > 0)
            {
                PROTIMER_IncrementWrapCounter(overflowCount);
            }

            for(int i = 0; i < PROTIMER_NumberOfTimeoutCounters; i++)
            {
                if((PROTIMER_preCounterSourcedBitmask & ((uint)Enumeration_T.EnumerationTValue2 << i)) > 0)
                {
                    PROTIMER_timeoutCounter[i].Update(Enumeration_W.EnumerationWValue1, overflowCount);
                }
            }

            PreCountOverflowsEvent.Invoke(overflowCount);

        }

        private uint MODEM_GetSyncWordLengthInBits()
        {
            return MODEM_SyncWordLength;
        }

        private double MODEM_GetTxChainDoneDelayUs()
        {
            return ((double)MODEM_GetTxChainDoneDelayNanoS()) / 1000;
        }

        private uint MODEM_GetRxChainDelayNanoS()
        {
            return field_228.Value ? MODEM_Ble1MbPhyRxChainDelayNanoS : MODEM_802154PhyRxChainDelayNanoS;
        }

        private uint MODEM_GetDataRate()
        {
            double numerator = field_218.Value * 1.0;
            double ratio = numerator / Math.Pow(2 , 16);
            double interpFactor = (field_159.Value) ? 2.0 : 4.0;
            double txBaudrate = (double)HfxoFrequency / interpFactor/ 8.0 * ratio;
            double chipsPerSymbol = field_162.Value + 1;
            double symbolsPerBit = 1.0;
            uint dsssShiftedSymbols = 0;

            if(field_163.Value == 1)
            {
                dsssShiftedSymbols = (uint)field_162.Value & 0x1F;
            }
            else if(field_163.Value > 1)
            {
                dsssShiftedSymbols = (uint)field_162.Value >> (int)(field_163.Value - 1);
            }

            if((field_167.Value == Enumeration_M.EnumerationMValue1))
            {
                symbolsPerBit = 2.0;
            }
            else if(field_209.Value == Enumeration_O.EnumerationOValue2)
            {
                switch((Enumeration_L)dsssShiftedSymbols)
                {
                case Enumeration_L.EnumerationLValue0:
                {
                    symbolsPerBit = 1.0;
                    break;
                }
                case Enumeration_L.EnumerationLValue1:
                {
                    if(field_161.Value == Enumeration_K.EnumerationKValue0)
                    {
                        symbolsPerBit = 1.0;
                    }
                    else
                    {
                        symbolsPerBit = 2.0;
                    }
                    break;
                }
                case Enumeration_L.EnumerationLValue2:
                {
                    symbolsPerBit = 2.0;
                    break;
                }
                case Enumeration_L.EnumerationLValue3:
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


            return (uint)txBitrate;
        }

        private RadioPhyId MODEM_GetCurrentPhy()
        {
            return (field_228.Value ? RadioPhyId.Phy_BLE_2_4GHz_GFSK : RadioPhyId.Phy_802154_2_4GHz_OQPSK);
        }

        private double MODEM_GetFrameOverTheAirTimeUs(byte[] frame, bool includePreamble, bool includeSyncWord)
        {
            uint frameLengthInBits = (uint)frame.Length*8;

            if(includePreamble)
            {
                frameLengthInBits += MODEM_GetPreambleLengthInBits();
            }

            if(!includeSyncWord)
            {
                frameLengthInBits -= MODEM_GetSyncWordLengthInBits();
            }

            return ((double)frameLengthInBits) * 1000000 / (double)MODEM_GetDataRate();
        }

        private double MODEM_GetPreambleOverTheAirTimeUs()
        {
            return (double)MODEM_GetPreambleLengthInBits() * 1000000 / (double)MODEM_GetDataRate();
        }

        private uint MODEM_GetTxChainDoneDelayNanoS()
        {
            return field_228.Value ? MODEM_Ble1MbPhyTxDoneChainDelayNanoS : MODEM_802154PhyTxDoneChainDelayNanoS;
        }

        private double MODEM_GetTxChainDelayUs()
        {
            return ((double)MODEM_GetTxChainDelayNanoS()) / 1000;
        }

        private uint MODEM_GetTxChainDelayNanoS()
        {
            return field_228.Value ? MODEM_Ble1MbPhyTxChainDelayNanoS : MODEM_802154PhyTxChainDelayNanoS;
        }

        private double MODEM_GetRxDoneDelayUs()
        {
            return ((double)MODEM_GetRxDoneDelayNanoS()) / 1000;
        }

        private uint MODEM_GetRxDoneDelayNanoS()
        {
            return field_228.Value ? MODEM_Ble1MbPhyRxDoneDelayNanoS : MODEM_802154PhyRxDoneDelayNanoS;
        }

        private double MODEM_GetRxChainDelayUs()
        {
            return ((double)MODEM_GetRxChainDelayNanoS()) / 1000;
        }

        private byte[] CRC_CalculateCRC()
        {
            return Enumerable.Repeat<byte>(0x0, (int)CRC_CrcWidth).ToArray();
        }

        private void SYNTH_TimerLimitReached()
        {
            if(SYNTH_state == Enumeration_AD.EnumerationADValue2)
            {
                field_364.Value = true;
                field_369.Value = true;
                UpdateInterrupts();
            }
        }

        private void SYNTH_CalibrationStart()
        {
            SYNTH_state = Enumeration_AD.EnumerationADValue2;

            synthTimer.Limit = SYNTH_CalibrationTimeUs;
            synthTimer.Enabled = true;
        }

        private void SYNTH_DisableIf()
        {
            field_366.Value = false;
        }

        private void SYNTH_EnableIf()
        {
            field_366.Value = true;
        }

        private void SYNTH_Stop()
        {
            SYNTH_state = Enumeration_AD.EnumerationADValue0;
            synthTimer.Enabled = false;
        }

        private void SYNTH_Start()
        {
            if(SYNTH_state == Enumeration_AD.EnumerationADValue2)
            {
                SYNTH_state = Enumeration_AD.EnumerationADValue1;
                field_367.Value = true;
                field_371.Value = true;
                UpdateInterrupts();
            }
        }

        private void AGC_UpdateRssiState()
        {
            if(AGC_rssiStartCommandOngoing)
            {
                field_23.Value = Enumeration_C.EnumerationCValue3;
            }
            else if(RAC_currentRadioState == Enumeration_AB.EnumerationABValue2)
            {
                field_23.Value = Enumeration_C.EnumerationCValue2;
            }
            else if(RAC_currentRadioState == Enumeration_AB.EnumerationABValue3)
            {
                field_23.Value = Enumeration_C.EnumerationCValue4;
            }
            else
            {
                field_23.Value = Enumeration_C.EnumerationCValue0;
            }
        }

        private void AGC_RssiUpdateTimerHandleLimitReached()
        {
            rssiUpdateTimer.Enabled = false;
            AGC_UpdateRssi();
            if(AGC_rssiStartCommandOngoing)
            {
                field_13.Value = true;
                field_30.Value = true;

                if(AGC_rssiStartCommandFromProtimer)
                {
                    if(PROTIMER_listenBeforeTalkState != Enumeration_S.EnumerationSValue0)
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

        private void AGC_StopRssiTimer()
        {
            rssiUpdateTimer.Enabled = false;
            AGC_rssiStartCommandOngoing = false;
            AGC_UpdateRssiState();

            if(AGC_rssiStartCommandFromProtimer)
            {
                AGC_rssiStartCommandFromProtimer = false;
                if(PROTIMER_listenBeforeTalkState != Enumeration_S.EnumerationSValue0)
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

        private void AGC_UpdateRssi()
        {
            AGC_RssiIntegerPart = (sbyte)InterferenceQueue.GetCurrentRssi(this, MODEM_GetCurrentPhy(), Channel);

            if(AGC_rssiFirstRead)
            {
                AGC_rssiFirstRead = false;
                field_24.Value = true;
                field_36.Value = true;
            }
            if(AGC_RssiIntegerPartAdjusted < (int)field_11.Value)
            {
                field_2.Value = true;
                field_26.Value = true;
            }
            else
            {
                field_6.Value = true;
                field_28.Value = true;
            }

            if(AGC_RssiIntegerPartAdjusted > (int)field_17.Value)
            {
                field_15.Value = true;
                field_32.Value = true;
            }
            else if(AGC_RssiIntegerPartAdjusted < (int)field_20.Value)
            {
                field_18.Value = true;
                field_34.Value = true;
            }

            UpdateInterrupts();
        }

        private bool AGC_RssiStartCommand(bool fromProtimer = false)
        {
            if(RAC_currentRadioState != Enumeration_AB.EnumerationABValue2 && RAC_currentRadioState != Enumeration_AB.EnumerationABValue3)
            {
                AGC_RssiIntegerPart = AGC_RssiInvalid;
                if(fromProtimer && PROTIMER_listenBeforeTalkState != Enumeration_S.EnumerationSValue0)
                {
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

        private void FRC_RxAbortCommand()
        {
            if(RAC_currentRadioState != Enumeration_AB.EnumerationABValue3)
            {
                return;
            }

            if(field_102.Value)
            {
                FRC_RestoreRxDescriptorsBufferWriteOffset();
            }


            field_91.Value = true;
            field_120.Value = true;
            RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue27);
            UpdateInterrupts();
        }

        private void FRC_CheckPacketCaptureBufferThreshold()
        {
            if(field_82.Value
                && field_76.Value >= field_81.Value)
            {
                field_83.Value = true;
                field_118.Value = true;
            }
        }

        private void FRC_UpdateRawMode()
        {
            if(!field_67.Value || field_108.Value || RAC_currentRadioState != Enumeration_AB.EnumerationABValue2)
            {
                return;
            }

            switch(field_109.Value)
            {
            case Enumeration_I.EnumerationIValue1:
                if(field_110.Value == Enumeration_J.EnumerationJValue0)
                {
                    field_111.Value = true;
                    field_108.Value = true;
                }
                break;
            default:
                return;
            }
            UpdateInterrupts();
        }

        private void FrcSnifferReceiveFrame(byte[] frame)
        {
            PtiFrameStart?.Invoke(SiLabs_PacketTraceFrameType.Receive);
            int syncWordBytes = Math.Min((int) MODEM_SyncWordBytes, SNIFF_SYNCWORD_SERIAL_LEN);
            var syncWord = frame.Take(syncWordBytes).ToArray();
            var frameData = frame.Skip(syncWordBytes).ToArray();
            if(field_88.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.RxStart });
            }
            if(field_89.Value)
            {
                var syncWordPadded = new byte[SNIFF_SYNCWORD_SERIAL_LEN];
                Array.Copy(syncWord, syncWordPadded, syncWord.Length);
                PtiDataOut?.Invoke(syncWordPadded);
            }
            if(field_87.Value)
            {
                PtiDataOut?.Invoke(frameData);
            }
            if(field_88.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.RxEndSuccess });
            }
            if(field_86.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)AGC_FrameRssiIntegerPart });
            }
        }

        private byte[] FRC_AssembleFrame()
        {
            var frame = Enumerable.Empty<byte>();

            if(!field_211.Value)
            {
                frame = frame.Concat(BitHelper.GetBytesFromValue(MODEM_TxSyncWord, (int)MODEM_SyncWordBytes, reverse: true));
            }

            var descriptor = FRC_frameDescriptor[FRC_ActiveTransmitFrameDescriptor];
            var frameLength = 0u;
            var dynamicFrameLength = true;

            switch(field_65.Value)
            {
            case Enumeration_F.EnumerationFValue0:
                dynamicFrameLength = false;
                break;
            case Enumeration_F.EnumerationFValue1:
            case Enumeration_F.EnumerationFValue2:
                frameLength = bufferController.Peek(descriptor.BufferIndex, (uint)field_73.Value);
                break;
            case Enumeration_F.EnumerationFValue3:
            case Enumeration_F.EnumerationFValue4:
                frameLength = ((bufferController.Peek(descriptor.BufferIndex, (uint)field_73.Value + 1) << 8)
                               | (bufferController.Peek(descriptor.BufferIndex, (uint)field_73.Value)));

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

            field_145.Value = 0;
            for(var subframe = 0; field_145.Value < FRC_FrameLength; ++subframe)
            {
                var crcLength = (descriptor.Field_152.Value && dynamicFrameLength && field_61.Value) ? CRC_CrcWidth : 0;
                var length = (uint)(FRC_FrameLength - field_145.Value);
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
                if(!bufferController.TryReadBytes(descriptor.BufferIndex, length, out var payload))
                {
                    this.Log(LogLevel.Error, "Read only {0} bytes of {1}, total length={2}", payload.Length, length, FRC_FrameLength);
                    this.Log(LogLevel.Error, "Failed to assemble a frame, partial frame: {0}", BitConverter.ToString(frame.Concat(payload).ToArray()));
                    field_143.Value = true;
                    return new byte[0];
                }
                frame = frame.Concat(payload);
                field_145.Value += length;

                if(descriptor.Field_152.Value)
                {
                    frame = frame.Concat(CRC_CalculateCRC());
                    field_145.Value += crcLength;
                }

                switch(field_140.Value)
                {
                case Enumeration_H.EnumerationHValue0:
                case Enumeration_H.EnumerationHValue3:
                    break;
                case Enumeration_H.EnumerationHValue1:
                    FRC_ActiveTransmitFrameDescriptor = subframe % 2;
                    break;
                case Enumeration_H.EnumerationHValue2:
                    FRC_ActiveTransmitFrameDescriptor = 1;
                    break;
                }
                descriptor = FRC_frameDescriptor[FRC_ActiveTransmitFrameDescriptor];
            }

            switch(field_140.Value)
            {
            case Enumeration_H.EnumerationHValue0:
            case Enumeration_H.EnumerationHValue1:
            case Enumeration_H.EnumerationHValue2:
                FRC_ActiveTransmitFrameDescriptor = 0;
                break;
            case Enumeration_H.EnumerationHValue3:
                FRC_ActiveTransmitFrameDescriptor = 1;
                break;
            }

            return frame.ToArray();
        }

        private void FRC_SaveRxDescriptorsBufferWriteOffset()
        {
            bufferController.UpdateWriteStartOffset(FRC_frameDescriptor[2].BufferIndex);
            bufferController.UpdateWriteStartOffset(FRC_frameDescriptor[3].BufferIndex);
        }

        private void FrcSnifferTransmitFrame(byte[] frame)
        {
            PtiFrameStart?.Invoke(SiLabs_PacketTraceFrameType.Transmit);
            int syncWordBytes = Math.Min((int)MODEM_SyncWordBytes, SNIFF_SYNCWORD_SERIAL_LEN);
            var syncWord = frame.Take(syncWordBytes).ToArray();
            var frameData = frame.Skip(syncWordBytes).ToArray();
            if(field_88.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.TxStart });
            }
            if(field_89.Value)
            {
                var syncWordPadded = new byte[SNIFF_SYNCWORD_SERIAL_LEN];
                Array.Copy(syncWord, syncWordPadded, syncWord.Length);
                PtiDataOut?.Invoke(syncWordPadded);
            }
            if(field_90.Value)
            {
                PtiDataOut?.Invoke(frameData);
            }
            if(field_88.Value)
            {
                PtiDataOut?.Invoke(new byte[] { (byte)PacketTraceFrameDelimiters.TxEndSuccess });
            }
        }

        private void FRC_DisassembleCurrentFrame(bool forceCrcError)
        {
            var frameLength = 0u;
            var dynamicFrameLength = true;
            switch(field_65.Value)
            {
            case Enumeration_F.EnumerationFValue0:
                dynamicFrameLength = false;
                break;
            case Enumeration_F.EnumerationFValue1:
            case Enumeration_F.EnumerationFValue2:
                frameLength = (uint)currentFrame[currentFrameOffset + field_73.Value];
                break;
            case Enumeration_F.EnumerationFValue3:
            case Enumeration_F.EnumerationFValue4:
                frameLength = (uint)currentFrame[currentFrameOffset + field_73.Value + 1] << 8
                              | (uint)currentFrame[currentFrameOffset + field_73.Value];
                break;
            default:
                this.Log(LogLevel.Error, "Unimplemented DFL mode.");
                return;
            }
            if(dynamicFrameLength && !FRC_TrySetFrameLength(frameLength))
            {
                this.Log(LogLevel.Error, "DisassembleFrame FRAMEERROR");
                return; 
            }

            field_145.Value = 0;
            for(var subframe = 0; field_145.Value < FRC_FrameLength; ++subframe)
            {
                var descriptor = FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor];
                var startingWriteOffset = bufferController.WriteOffset(descriptor.BufferIndex);

                var length = FRC_FrameLength - field_145.Value;
                if(descriptor.Words.HasValue)
                {
                    length = Math.Min(length, descriptor.Words.Value);
                }
                else if(dynamicFrameLength && descriptor.Field_152.Value && field_61.Value)
                {
                    length = checked(length - CRC_CrcWidth); 
                }
                if(currentFrameOffset + length > (uint)currentFrame.Length)
                {
                    this.Log(LogLevel.Error, "frame too small, payload");
                    return;
                }
                var payload = currentFrame.Skip((int)currentFrameOffset).Take((int)length);
                var skipCount = (field_145.Value < field_77.Value)
                                ? (field_77.Value - field_145.Value) : 0;
                var pktCaptureBuff = currentFrame.Skip((int)(currentFrameOffset + skipCount)).Take((int)(length - skipCount));
                currentFrameOffset += (uint)length;
                field_145.Value += length;

                FRC_WritePacketCaptureBuffer(pktCaptureBuff.ToArray());

                if(!bufferController.TryWriteBytes(descriptor.BufferIndex, payload.ToArray(), out var written))
                {
                    this.Log(LogLevel.Error, "Written only {0} bytes from {1}", written, length);
                    if(bufferController.Overflow(descriptor.BufferIndex))
                    {
                        field_106.Value = true;
                        field_124.Value = true;
                        UpdateInterrupts();
                        RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue10);
                    }
                }
                else if(descriptor.Field_152.Value)
                {
                    var crc = currentFrame.Skip((int)currentFrameOffset).Take((int)CRC_CrcWidth).ToArray();
                    currentFrameOffset += (uint)crc.Length;
                    if(crc.Length != CRC_CrcWidth)
                    {
                        this.Log(LogLevel.Error, "frame too small, crc");
                    }
                    if(dynamicFrameLength && field_61.Value)
                    {
                        field_145.Value += (uint)crc.Length;
                    }
                    if(field_113.Value)
                    {
                        if(!bufferController.TryWriteBytes(descriptor.BufferIndex, crc, out written))
                        {
                            this.Log(LogLevel.Error, "Written only {0} bytes from {1}", written, crc.Length);
                            if(bufferController.Overflow(descriptor.BufferIndex))
                            {
                                field_106.Value = true;
                                field_124.Value = true;
                                UpdateInterrupts();
                                RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue10);
                            }
                        }
                    }
                }

                switch(field_105.Value)
                {
                case Enumeration_H.EnumerationHValue0:
                case Enumeration_H.EnumerationHValue3:
                    break;
                case Enumeration_H.EnumerationHValue1:
                    FRC_ActiveReceiveFrameDescriptor = 2 + subframe % 2;
                    break;
                case Enumeration_H.EnumerationHValue2:
                    FRC_ActiveReceiveFrameDescriptor = 3;
                    break;
                }
            }

            {
                var descriptor = FRC_frameDescriptor[FRC_ActiveReceiveFrameDescriptor];
                if(field_98.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, 0x0);
                }
                if(field_99.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, (forceCrcError) ? 0x00U : 0x80U);
                }
                if(field_95.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, ((uint)PROTIMER_captureCompareChannel[0].Field_44.Value & 0xFF));
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].Field_44.Value >> 8) & 0xFF));
                }
                if(field_94.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].Field_44.Value >> 16) & 0xFF));
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].Field_44.Value >> 24) & 0xFF));
                }
                if(field_97.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, ((uint)PROTIMER_captureCompareChannel[0].Field_391.Value & 0xFF));
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].Field_391.Value >> 8) & 0xFF));
                }
                if(field_96.Value)
                {
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].Field_391.Value >> 16) & 0xFF));
                    bufferController.WriteData(descriptor.BufferIndex, (((uint)PROTIMER_captureCompareChannel[0].Field_391.Value >> 24) & 0xFF));
                }
            }

            switch(field_105.Value)
            {
            case Enumeration_H.EnumerationHValue0:
            case Enumeration_H.EnumerationHValue1:
            case Enumeration_H.EnumerationHValue2:
                FRC_ActiveReceiveFrameDescriptor = 2;
                break;
            case Enumeration_H.EnumerationHValue3:
                FRC_ActiveReceiveFrameDescriptor = 3;
                break;
            }

        }

        private void FRC_RestoreRxDescriptorsBufferWriteOffset()
        {
            bufferController.RestoreWriteOffset(FRC_frameDescriptor[2].BufferIndex);
            bufferController.RestoreWriteOffset(FRC_frameDescriptor[3].BufferIndex);
        }

        private bool FRC_TrySetFrameLength(uint frameLength)
        {
            switch(field_65.Value)
            {
            case Enumeration_F.EnumerationFValue1:
            case Enumeration_F.EnumerationFValue3:
                break;
            case Enumeration_F.EnumerationFValue2:
            case Enumeration_F.EnumerationFValue4:
                frameLength = (frameLength >> 8) | ((frameLength & 0xFF) << 8);
                break;
            default:
                this.Log(LogLevel.Error, "Invalid DFL mode.");
                return false;
            }
            frameLength >>= (byte)field_63.Value;
            frameLength &= (1u << (byte)field_64.Value) - 1;
            int offset = (field_66.Value & 0x8) != 0
                ? (int)(field_66.Value | unchecked((uint)~0xF))
                : (int)field_66.Value;
            frameLength += (uint)offset;
            if(frameLength < field_75.Value || field_74.Value < frameLength)
            {
                field_68.Value = true;
                field_114.Value = true;
                UpdateInterrupts();
                return false;
            }

            field_70.Value = frameLength;
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
            var foundSyncWord1 = (field_164.Value && (syncWord == MODEM_SyncWord1));
            var foundSyncWord2 = (field_212.Value && (syncWord == MODEM_SyncWord2));
            var foundSyncWord3 = (field_212.Value && field_165.Value && (syncWord == MODEM_SyncWord3));

            if(!foundSyncWord0 && !foundSyncWord1 && !foundSyncWord2 && !foundSyncWord3)
            {
                return false;
            }

            field_186.Value = true;
            field_198.Value = true;

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
                if(PROTIMER_captureCompareChannel[i].Field_56.Value && PROTIMER_captureCompareChannel[i].Field_231.Value == Enumeration_Q.EnumerationQValue1)
                {
                    var triggered = false;
                    switch(PROTIMER_captureCompareChannel[i].Field_50.Value)
                    {
                    case Enumeration_R.EnumerationRValue4:
                        triggered |= foundSyncWord0;
                        break;
                    case Enumeration_R.EnumerationRValue5:
                        triggered |= foundSyncWord1;
                        break;
                    case Enumeration_R.EnumerationRValue6:
                        triggered |= foundSyncWord2;
                        break;
                    case Enumeration_R.EnumerationRValue7:
                        triggered |= foundSyncWord3;
                        break;
                    case Enumeration_R.EnumerationRValue8:
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

            field_178.Value |= foundSyncWord0;
            field_190.Value = field_178.Value;
            field_180.Value |= foundSyncWord1;
            field_192.Value = field_180.Value;
            field_182.Value |= foundSyncWord2;
            field_194.Value = field_182.Value;
            field_184.Value |= foundSyncWord3;
            field_196.Value = field_184.Value;

            if(foundSyncWord0)
            {
                field_166.Value = 0;
            }
            else if(foundSyncWord1)
            {
                field_166.Value = 1;
            }
            else if(foundSyncWord2)
            {
                field_166.Value = 2;
            }
            else if(foundSyncWord3)
            {
                field_166.Value = 3;
            }
            UpdateInterrupts();

            return true;
        }

        private void FRC_WritePacketCaptureBuffer(byte[] data)
        {
            if(field_76.Value > FRC_PacketBufferCaptureSize)
            {
                throw new Exception("field_76 exceeded max value!");
            }

            for(var i = 0; i < data.Length; i++)
            {
                if(field_76.Value == FRC_PacketBufferCaptureSize)
                {
                    break;
                }

                FRC_packetBufferCapture[field_76.Value] = data[i];
                field_76.Value++;

                if(field_76.Value == 1)
                {
                    field_78.Value = true;
                    field_116.Value = true;
                    UpdateInterrupts();
                }
            }
        }

        private void RAC_PaRampingTimerHandleLimitReached()
        {
            paRampingTimer.Enabled = false;
            MODEM_TxRampingDoneInterrupt = true;
            field_302.Value = RAC_paOutputLevelRamping;
            UpdateInterrupts();
        }

        private void RAC_UpdateRadioStateMachine(Enumeration_AC signal = Enumeration_AC.EnumerationACValue0)
        {

            machine.ClockSource.ExecuteInLock(delegate
            {
                Enumeration_AB previousState = RAC_currentRadioState;

                if(signal == Enumeration_AC.EnumerationACValue1)
                {
                    RAC_ClearOngoingTxOrRx();
                    RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue8);
                }
                else if(field_298.Value)
                {
                    RAC_ClearOngoingTxOrRx();
                    RAC_ChangeRadioState(field_299.Value);
                    field_298.Value = false;
                }
                else if(field_297.Value && RAC_currentRadioState == Enumeration_AB.EnumerationABValue0)
                {
                }
                else if(field_297.Value && RAC_currentRadioState != Enumeration_AB.EnumerationABValue8)
                {
                    RAC_ClearOngoingTxOrRx();
                    RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue8);
                }

                else if(signal == Enumeration_AC.EnumerationACValue3 )
                {
                    if(RAC_currentRadioState == Enumeration_AB.EnumerationABValue6)
                    {
                        RAC_ClearOngoingTx();
                        RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue7);
                    }
                    else
                    {
                        RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue5);
                    }
                }
                else
                {
                    switch(RAC_currentRadioState)
                    {
                    case Enumeration_AB.EnumerationABValue8:
                    {
                        if(!field_333.Value
                            && (!field_336.Value || !field_335.Value)
                            && !field_296.Value)
                        {
                            RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue0);
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue0:
                    {
                        if(!field_297.Value
                            && (!field_314.Value || !field_313.Value))
                        {
                            if(signal == Enumeration_AC.EnumerationACValue5)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue5);
                            }
                            else if(RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue1);
                            }
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue5:
                    {
                        if(signal == Enumeration_AC.EnumerationACValue17
                            && !field_343.Value
                            && (!field_342.Value || !field_341.Value))
                        {
                            if(RAC_TxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue6);
                            }
                            else
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue7);
                            }
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue6:
                    {
                        if(!field_339.Value
                            && (signal == Enumeration_AC.EnumerationACValue16 
                                || signal == Enumeration_AC.EnumerationACValue7)) 
                        {
                            RAC_ClearOngoingTx();
                            RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue7);
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue7:
                    {
                        if(signal == Enumeration_AC.EnumerationACValue19
                            && !field_331.Value
                            && (!field_330.Value || !field_329.Value))
                        {
                            if(field_358.Value == Enumeration_AB.EnumerationABValue6
                                && !RAC_TxEnable)
                            {
                            }
                            else if(field_358.Value == Enumeration_AB.EnumerationABValue2
                                     && !RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue4);
                            }
                            else if(field_358.Value == Enumeration_AB.EnumerationABValue6
                                     && RAC_TxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue6);
                            }
                            else if(field_358.Value == Enumeration_AB.EnumerationABValue2
                                     && RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue2);
                            }
                            else
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue0);
                            }
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue1:
                    {
                        if(signal == Enumeration_AC.EnumerationACValue20
                            && !field_327.Value
                            && (!field_326.Value || !field_325.Value))
                        {
                            RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue2);
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue2:
                    {
                        if(!field_323.Value
                            && (!field_322.Value || !field_321.Value))
                        {
                            if(signal == Enumeration_AC.EnumerationACValue8)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue3);
                            }
                            else if(signal == Enumeration_AC.EnumerationACValue5)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue4);
                            }
                            else if(signal == Enumeration_AC.EnumerationACValue9)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue1);
                            }
                            else if(!RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue4);
                            }
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue3:
                    {
                        if(signal == Enumeration_AC.EnumerationACValue27)
                        {
                            RAC_ClearOngoingRx();
                            RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue2);
                        }
                        else if(signal == Enumeration_AC.EnumerationACValue22
                                 || signal == Enumeration_AC.EnumerationACValue6)
                        {
                            if(signal == Enumeration_AC.EnumerationACValue22)
                            {
                                FRC_rxFrameExitPending = true;
                            }
                            if(signal == Enumeration_AC.EnumerationACValue6)
                            {
                                FRC_rxDonePending = true;
                            }

                            if((!field_318.Value || !field_317.Value)
                                && FRC_rxFrameExitPending
                                && FRC_rxDonePending)
                            {
                                RAC_ClearOngoingRx();
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue4);
                            }
                        }
                        break;
                    }
                    case Enumeration_AB.EnumerationABValue4:
                    {
                        if(signal == Enumeration_AC.EnumerationACValue23
                            && !field_331.Value
                            && (!field_330.Value || !field_329.Value))
                        {
                            if(field_305.Value == Enumeration_AB.EnumerationABValue2
                                && !RAC_RxEnable)
                            {
                            }
                            else if(field_305.Value == Enumeration_AB.EnumerationABValue6
                                     && !RAC_TxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue7);
                            }
                            else if(field_305.Value == Enumeration_AB.EnumerationABValue2
                                     && RAC_RxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue2);
                            }
                            else if(field_305.Value == Enumeration_AB.EnumerationABValue6
                                     && RAC_TxEnable)
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue6);
                            }
                            else
                            {
                                RAC_ChangeRadioState(Enumeration_AB.EnumerationABValue0);
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
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "RSM Update at {0}, signal:{1} channel={2} ({3}), transition: {4}->{5} (TX={6} RX={7}) Lbt={8}",
                             GetTime(), signal, Channel, (field_228.Value ? "BLE" : "802.15.4"), previousState, RAC_currentRadioState, RAC_internalTxState, RAC_internalRxState, PROTIMER_listenBeforeTalkState);

                    uint currentStateBitmask = (1U << (int)RAC_currentRadioState);

                    for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
                    {
                        if(PROTIMER_captureCompareChannel[i].Field_56.Value
                           && PROTIMER_captureCompareChannel[i].Field_231.Value == Enumeration_Q.EnumerationQValue1
                           && ((PROTIMER_captureCompareChannel[i].Field_50.Value == Enumeration_R.EnumerationRValue13
                                && ((uint)field_263.Value & currentStateBitmask) > 0)
                               || (PROTIMER_captureCompareChannel[i].Field_50.Value == Enumeration_R.EnumerationRValue14
                                   && ((uint)field_264.Value & currentStateBitmask) > 0)))
                        {
                            PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                            PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                        }
                    }

                    switch(RAC_currentRadioState)
                    {
                    case Enumeration_AB.EnumerationABValue0:
                        field_315.Value = true;
                        field_313.Value = true;
                        break;
                    case Enumeration_AB.EnumerationABValue1:
                        field_327.Value = true;
                        field_325.Value = true;
                        break;
                    case Enumeration_AB.EnumerationABValue2:
                        field_76.Value = 0;
                        field_323.Value = true;
                        field_321.Value = true;
                        field_160.Value = MODEM_DemodulatorState.PreambleSearch;
                        FRC_UpdateRawMode();
                        break;
                    case Enumeration_AB.EnumerationABValue3:
                        field_319.Value = true;
                        field_317.Value = true;
                        field_160.Value = MODEM_DemodulatorState.RxFrame;
                        break;
                    case Enumeration_AB.EnumerationABValue4:
                        field_331.Value = true;
                        field_329.Value = true;
                        break;
                    case Enumeration_AB.EnumerationABValue5:
                        field_343.Value = true;
                        field_341.Value = true;
                        break;
                    case Enumeration_AB.EnumerationABValue6:
                        field_339.Value = true;
                        field_337.Value = true;
                        var frame = FRC_AssembleFrame();
                        TransmitFrame(frame);
                        break;
                    case Enumeration_AB.EnumerationABValue7:
                        field_347.Value = true;
                        field_345.Value = true;
                        break;
                    case Enumeration_AB.EnumerationABValue8:
                        field_333.Value = true;
                        field_335.Value = true;
                        break;
                    default:
                        this.Log(LogLevel.Error, "Invalid Radio State ({0}).", RAC_currentRadioState);
                        break;
                    }

                    if(RAC_currentRadioState != Enumeration_AB.EnumerationABValue2 && RAC_currentRadioState != Enumeration_AB.EnumerationABValue3)
                    {
                        field_160.Value = MODEM_DemodulatorState.Off;
                    }

                    AGC_UpdateRssiState();

                    if(RAC_currentRadioState == Enumeration_AB.EnumerationABValue2)
                    {
                        AGC_UpdateRssi();
                    }
                    else if(RAC_currentRadioState != Enumeration_AB.EnumerationABValue2 && RAC_currentRadioState != Enumeration_AB.EnumerationABValue3)
                    {
                        AGC_StopRssiTimer();
                    }
                }

                UpdateInterrupts();
            });
        }

        private void RAC_ClearOngoingTx()
        {
            txTimer.Enabled = false;
            if(RAC_internalTxState != Enumeration_AA.EnumerationAAValue0)
            {
                InterferenceQueue.Remove(this);
            }
            RAC_internalTxState = Enumeration_AA.EnumerationAAValue0;
        }

        private void RAC_ClearOngoingRx()
        {
            FRC_rxFrameExitPending = false;
            FRC_rxDonePending = false;
            rxTimer.Enabled = false;
            RAC_ongoingRxCollided = false;
            RAC_internalRxState = Enumeration_Z.EnumerationZValue0;
        }

        private void RAC_ClearOngoingTxOrRx()
        {
            RAC_ClearOngoingRx();
            RAC_ClearOngoingTx();
        }

        private void RAC_ChangeRadioState(Enumeration_AB newState)
        {
            if(newState != RAC_currentRadioState)
            {
                RAC_previous3RadioState = RAC_previous2RadioState;
                RAC_previous2RadioState = RAC_previous1RadioState;
                RAC_previous1RadioState = RAC_currentRadioState;
                RAC_currentRadioState = newState;

                if(RAC_currentRadioState == Enumeration_AB.EnumerationABValue6)
                {
                    RAC_TxEnable = false;
                }
            }
        }

        private void RAC_RxTimerLimitReached()
        {
            rxTimer.Enabled = false;

            if(RAC_internalRxState == Enumeration_Z.EnumerationZValue1)
            {
                if(RAC_currentRadioState != Enumeration_AB.EnumerationABValue2)
                {
                    RAC_ClearOngoingRx();
                    this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping (not in RXSEARCH): at {0} (channel {1}): {2}", GetTime(), Channel, BitConverter.ToString(currentFrame));
                    return;
                }

                if(RAC_ongoingRxCollided)
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

                RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue8);
                RAC_internalRxState = Enumeration_Z.EnumerationZValue2;
                FRC_SaveRxDescriptorsBufferWriteOffset();
                currentFrameOffset = MODEM_SyncWordBytes;

                double overTheAirFrameTimeUs = MODEM_GetFrameOverTheAirTimeUs(currentFrame, false, false);
                double rxDoneDelayUs = MODEM_GetRxDoneDelayUs();
                if(overTheAirFrameTimeUs + rxDoneDelayUs > RAC_rxTimeAlreadyPassedUs
                    && PROTIMER_UsToPreCntOverflowTicks(overTheAirFrameTimeUs + rxDoneDelayUs - RAC_rxTimeAlreadyPassedUs) > 0)
                {
                    rxTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
                    rxTimer.Limit = PROTIMER_UsToPreCntOverflowTicks(overTheAirFrameTimeUs + rxDoneDelayUs - RAC_rxTimeAlreadyPassedUs);
                    rxTimer.Enabled = true;
                    return;
                }

            }


            if(RAC_internalRxState != Enumeration_Z.EnumerationZValue2)
            {
                throw new Exception("RAC_RxTimerLimitReached: unexpected RX state");
            }

            RAC_internalRxState = Enumeration_Z.EnumerationZValue0;

            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].Field_56.Value && PROTIMER_captureCompareChannel[i].Field_231.Value == Enumeration_Q.EnumerationQValue1)
                {
                    switch(PROTIMER_captureCompareChannel[i].Field_50.Value)
                    {
                    case Enumeration_R.EnumerationRValue10:
                    case Enumeration_R.EnumerationRValue2:
                    case Enumeration_R.EnumerationRValue3:
                        PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                        PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                        break;
                    }
                }
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.RxDone);
            PROTIMER_TriggerEvent(PROTIMER_Event.TxOrRxDone);

            if(RAC_ongoingRxCollided)
            {
                field_68.Value = true;
                field_114.Value = true;

                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Dropping at {0}: RX collision", GetTime());

                if(field_101.Value & !field_93.Value)
                {
                    FRC_RestoreRxDescriptorsBufferWriteOffset();
                }
            }

            if(!RAC_ongoingRxCollided || field_93.Value)
            {
                this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Received at {0} on channel {1} (RSSI={2}, collided={3}): {4}",
                         GetTime(), Channel, AGC_FrameRssiIntegerPart, RAC_ongoingRxCollided, BitConverter.ToString(currentFrame));

                FRC_DisassembleCurrentFrame(RAC_ongoingRxCollided);

                field_103.Value = true;
                field_122.Value = true;
            }

            UpdateInterrupts();
            RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue6);
        }

        private void RAC_TxTimerLimitReached()
        {
            txTimer.Enabled = false;

            RAC_ClearOngoingTx();

            field_219.Value = true;
            field_202.Value = true;
            field_138.Value = true;
            field_130.Value = true;

            for(uint i = 0; i < PROTIMER_NumberOfCaptureCompareChannels; ++i)
            {
                if(PROTIMER_captureCompareChannel[i].Field_56.Value && PROTIMER_captureCompareChannel[i].Field_231.Value == Enumeration_Q.EnumerationQValue1)
                {
                    switch(PROTIMER_captureCompareChannel[i].Field_50.Value)
                    {
                    case Enumeration_R.EnumerationRValue1:
                    case Enumeration_R.EnumerationRValue3:
                        PROTIMER_captureCompareChannel[i].Capture(PROTIMER_BaseCounterValue, PROTIMER_WrapCounterValue);
                        PROTIMER_TriggerEvent(PROTIMER_GetCaptureCompareEventFromIndex(i));
                        break;
                    }
                }
            }
            PROTIMER_TriggerEvent(PROTIMER_Event.TxDone);
            PROTIMER_TriggerEvent(PROTIMER_Event.TxOrRxDone);

            RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue7);
        }

        private bool TransmitFrame(byte[] frame)
        {

            if(RAC_internalTxState != Enumeration_AA.EnumerationAAValue0)
            {
                throw new Exception("TransmitFrame(): state not IDLE");
            }

            RAC_TxEnable = false;

            if(frame.Length == 0)
            {
                return false;
            }

            var timerDelayUs = MODEM_GetFrameOverTheAirTimeUs(frame, true, true) + MODEM_GetTxChainDelayUs() - MODEM_GetTxChainDoneDelayUs();

            this.Log(LogBasicRadioActivityAsError ? LogLevel.Error : LogLevel.Info, "Sending frame at {0} on channel {1} ({2}): {3}",
                     GetTime(), Channel, MODEM_GetCurrentPhy(), BitConverter.ToString(frame));


            InterferenceQueue.Add(this, MODEM_GetCurrentPhy(), Channel, 0 , frame);
            FrcSnifferTransmitFrame(frame);
            FrameSent?.Invoke(this, frame);

            field_222.Value = true;
            field_204.Value = true;
            field_226.Value |= !field_211.Value;
            field_207.Value = field_226.Value;

            UpdateInterrupts();

            RAC_internalTxState = Enumeration_AA.EnumerationAAValue1;
            txTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
            txTimer.Limit = PROTIMER_UsToPreCntOverflowTicks(timerDelayUs);
            txTimer.Enabled = true;

            return true;
        }

        private uint RAC_TxEnableMask
        {
            get => 0;
            set
            {
            }
        }

        private uint RAC_RxEnableMask => ((uint)field_355.Value
                                          
                                          | ((PROTIMER_RxEnable ? 1U : 0U) << 14)
                                        
                                        );

        private bool RAC_RxEnable => RAC_RxEnableMask != 0;

        private bool RAC_PaOutputLevelRampingInProgress => paRampingTimer.Enabled;

        private bool RAC_TxEnable
        {
            get => RAC_txEnable;
            set
            {
                
                var risingEdge = value && !RAC_txEnable;
                RAC_txEnable = value;

                if(risingEdge)
                {
                    RAC_UpdateRadioStateMachine(Enumeration_AC.EnumerationACValue5);
                }
            }
        }

        private bool RAC_PaOutputLevelRamping
        {
            get
            {
                return RAC_paOutputLevelRamping;
            }

            set
            {
                if(value != RAC_paOutputLevelRamping)
                {
                    RAC_paOutputLevelRamping = value;

                    if(!field_168.Value)
                    {
                        MODEM_TxRampingDoneInterrupt = false;
                        UpdateInterrupts();

                        paRampingTimer.Enabled = false;
                        field_302.Value = !value;
                        paRampingTimer.Value = 0;
                        paRampingTimer.Limit = RAC_PowerAmplifierRampingTimeUs;
                        paRampingTimer.Enabled = true;
                    }
                }
            }
        }

        private bool LPW0PORTAL_PowerUpAck
        {
            get
            {
                if(LPW0PORTAL_powerUpOngoing)
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

        private bool LPW0PORTAL_PowerUpRequest
        {
            set
            {
                if(value)
                {
                    LPW0PORTAL_powerUpOngoing = value;
                }
            }

            get
            {
                return LPW0PORTAL_powerUpOngoing;
            }
        }

        private bool HOSTPORTAL_PowerUpAck
        {
            get
            {
                if(HOSTPORTAL_powerUpOngoing)
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

        private bool HOSTPORTAL_PowerUpRequest
        {
            set
            {
                if(value)
                {
                    HOSTPORTAL_powerUpOngoing = value;
                }
            }

            get
            {
                return HOSTPORTAL_powerUpOngoing;
            }
        }

        private uint MODEM_SyncWord3 => (uint)field_216.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);

        private uint MODEM_SyncWord0 => (uint)field_213.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);

        private uint MODEM_SyncWordBytes => ((uint)field_210.Value >> 3) + 1;

        private uint MODEM_SyncWord2 => (uint)field_215.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);

        private uint MODEM_SyncWord1 => (uint)field_214.Value & (uint)((1UL << (byte)MODEM_SyncWordLength) - 1);

        private uint MODEM_SyncWordLength => (uint)field_210.Value + 1;

        private uint MODEM_TxSyncWord => (field_164.Value && field_225.Value) ? (uint)field_214.Value : (uint)field_213.Value;

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

        private uint CRC_CrcWidth => (uint)field_47.Value + 1;

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

        private bool PROTIMER_RxEnable => (PROTIMER_rxRequestState == Enumeration_X.EnumerationXValue2
                                           || PROTIMER_rxRequestState == Enumeration_X.EnumerationXValue3);

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
                    TrySyncTime();
                    bool isRunning = proTimer.Enabled;
                    ulong currentValue = proTimer.Value;

                    proTimer.Enabled = false;

                    if(!isRunning || field_290.Value)
                    {
                        PROTIMER_BaseCounterValue = 0;
                        PROTIMER_WrapCounterValue = 0;
                    }

                    proTimer.Frequency = PROTIMER_GetPreCntOverflowFrequency();
                    proTimer.Limit = PROTIMER_ComputeTimerLimit();
                    proTimer.Enabled = true;
                    proTimer.Value = (!isRunning || field_290.Value) ? 0 : currentValue;
                }
                else
                {
                    proTimer.Enabled = false;
                }
            }
        }

        private int FRC_ActiveReceiveFrameDescriptor
        {
            get
            {
                return field_58.Value ? 3 : 2;
            }

            set
            {
                if(value != 2 && value != 3)
                {
                    throw new Exception("Setting illegal FRC_ActiveReceiveFrameDescriptor value.");
                }

                field_58.Value = (value == 3);
            }
        }

        private int FRC_ActiveTransmitFrameDescriptor
        {
            get
            {
                return field_59.Value ? 1 : 0;
            }

            set
            {
                if(value != 0 && value != 1)
                {
                    throw new Exception("Setting illegal FRC_ActiveTransmitFrameDescriptor value.");
                }

                field_59.Value = (value == 1);
            }
        }

        private uint FRC_FrameLength => (uint)field_70.Value + 1;

        private byte AGC_RssiFractionalPart
        {
            get
            {
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


                if((field_9.Value && PROTIMER_listenBeforeTalkState == Enumeration_S.EnumerationSValue2))
                {
                    return AGC_OQPSK250KbpsPhyCcaRssiMeasurePeriodUs;
                }
                else if(AGC_rssiStartCommandOngoing)
                {
                    return AGC_OQPSK250KbpsPhyRssiMeasurePeriodUs;
                }
                else
                {
                    return AGC_OQPSK250KbpsPhyBackgroundRssiMeasurePeriodUs;
                }
            }
        }

        private IFlagRegisterField field_190;
        private IFlagRegisterField field_180;
        private IFlagRegisterField field_203;
        private IFlagRegisterField field_178;
        private IFlagRegisterField field_186;
        private IFlagRegisterField field_222;
        private IFlagRegisterField field_226;
        private IFlagRegisterField field_219;
        private bool MODEM_txRampingDoneInterrupt = true;
        private IFlagRegisterField field_208;
        private IFlagRegisterField field_205;
        private IFlagRegisterField field_199;
        private IFlagRegisterField field_206;
        private IFlagRegisterField field_278;
        private IFlagRegisterField field_182;
        private IFlagRegisterField field_184;
        private IFlagRegisterField field_188;
        private IFlagRegisterField field_220;
        private IFlagRegisterField field_198;
        private IFlagRegisterField field_204;
        private IFlagRegisterField field_207;
        private IFlagRegisterField field_202;
        private IFlagRegisterField field_189;
        private IFlagRegisterField field_185;
        private IFlagRegisterField field_183;
        private IFlagRegisterField field_192;
        private IFlagRegisterField field_181;
        private IFlagRegisterField field_187;
        private IFlagRegisterField field_194;
        private IFlagRegisterField field_196;
        private IFlagRegisterField field_200;
        private IFlagRegisterField field_224;
        private IFlagRegisterField field_223;
        private IFlagRegisterField field_227;
        private IFlagRegisterField field_179;
        private IFlagRegisterField field_191;
        private IFlagRegisterField field_193;
        private IValueRegisterField field_170;
        private IFlagRegisterField field_197;
        private IFlagRegisterField field_195;
        private IValueRegisterField field_215;
        private IValueRegisterField field_214;
        private IValueRegisterField field_213;
        private IFlagRegisterField field_211;
        private IFlagRegisterField field_225;
        private IFlagRegisterField field_212;
        private IFlagRegisterField field_165;
        private IFlagRegisterField field_164;
        private IFlagRegisterField field_159;
        private IValueRegisterField field_216;
        private IFlagRegisterField field_221;
        private IEnumRegisterField<Enumeration_O> field_209;
        private IEnumRegisterField<Enumeration_M> field_167;
        private IValueRegisterField field_163;
        private IValueRegisterField field_162;
        private IValueRegisterField field_218;
        private IValueRegisterField field_210;
        private IValueRegisterField field_217;
        private IValueRegisterField field_158;
        private IFlagRegisterField field_201;
        private IEnumRegisterField<Enumeration_K> field_161;
        private IValueRegisterField field_166;
        private IValueRegisterField field_175;
        private IValueRegisterField field_174;
        private IEnumRegisterField<MODEM_DemodulatorState> field_160;
        private IFlagRegisterField field_228;
        private IValueRegisterField field_172;
        private IValueRegisterField field_171;
        private IValueRegisterField field_169;
        private IValueRegisterField field_177;
        private IFlagRegisterField field_168;
        private IEnumRegisterField<Enumeration_N> field_173;
        private IValueRegisterField field_176;

        private IFlagRegisterField field_274;
        private IValueRegisterField field_239;
        private IFlagRegisterField field_276;
        private IFlagRegisterField field_259;
        private IEnumRegisterField<Enumeration_AB> field_264;
        private IEnumRegisterField<Enumeration_AB> field_263;
        private IValueRegisterField field_265;
        private IFlagRegisterField field_242;
        private IValueRegisterField field_241;
        private IValueRegisterField field_240;
        private IValueRegisterField field_248;
        private IValueRegisterField field_243;
        private IValueRegisterField field_246;
        private IValueRegisterField field_252;
        private IFlagRegisterField field_247;
        private IFlagRegisterField field_251;
        private IFlagRegisterField field_255;
        private IEnumRegisterField<PROTIMER_Event> field_284;
        private IEnumRegisterField<PROTIMER_Event> field_285;
        private IEnumRegisterField<PROTIMER_Event> field_266;
        private IEnumRegisterField<PROTIMER_Event> field_268;
        private IEnumRegisterField<PROTIMER_Event> field_269;
        private IEnumRegisterField<PROTIMER_Event> field_267;
        private bool PROTIMER_listenBeforeTalkPending = false;
        private Enumeration_S PROTIMER_listenBeforeTalkState = Enumeration_S.EnumerationSValue0;
        private Enumeration_X PROTIMER_rxRequestState = Enumeration_X.EnumerationXValue0;
        private Enumeration_X PROTIMER_txRequestState = Enumeration_X.EnumerationXValue0;
        private IFlagRegisterField field_236;
        private IFlagRegisterField field_272;
        private IFlagRegisterField field_287;
        private IFlagRegisterField field_245;
        private IFlagRegisterField field_282;
        private IFlagRegisterField field_270;
        private IFlagRegisterField field_280;
        private IFlagRegisterField field_279;
        private IFlagRegisterField field_275;
        private IFlagRegisterField field_273;
        private IFlagRegisterField field_277;
        private IFlagRegisterField field_283;
        private IFlagRegisterField field_271;
        private IFlagRegisterField field_281;
        private IFlagRegisterField field_256;
        private IFlagRegisterField field_249;
        private IFlagRegisterField field_244;
        private IFlagRegisterField field_253;
        private IFlagRegisterField field_286;
        private IFlagRegisterField field_235;
        private IFlagRegisterField field_258;
        private IFlagRegisterField field_257;
        private IFlagRegisterField field_250;
        private IFlagRegisterField field_254;
        private bool PROTIMER_txEnable = false;
        private IFlagRegisterField field_290;
        private IValueRegisterField field_289;
        private IValueRegisterField field_238;
        private IValueRegisterField field_261;
        private IValueRegisterField field_262;
        private uint PROTIMER_seqLatchedWrapCounterValue = 0;
        private uint PROTIMER_seqLatchedBaseCounterValue = 0;
        private uint PROTIMER_latchedWrapCounterValue = 0;
        private uint PROTIMER_latchedBaseCounterValue = 0;
        private uint PROTIMER_wrapCounterValue = 0;
        private uint PROTIMER_baseCounterValue = 0;
        private IEnumRegisterField<Enumeration_Y> field_288;
        private IEnumRegisterField<Enumeration_P> field_237;
        private IEnumRegisterField<Enumeration_U> field_260;
        private uint PROTIMER_preCounterSourcedBitmask = 0;

        private IFlagRegisterField field_30;
        private IFlagRegisterField field_26;
        private IFlagRegisterField field_36;
        private IFlagRegisterField field_7;
        private IFlagRegisterField field_19;
        private IFlagRegisterField field_16;
        private IFlagRegisterField field_14;
        private IFlagRegisterField field_3;
        private IFlagRegisterField field_25;
        private IFlagRegisterField field_32;
        private IFlagRegisterField field_6;
        private IFlagRegisterField field_15;
        private IFlagRegisterField field_13;
        private IFlagRegisterField field_2;
        private IFlagRegisterField field_24;
        private IValueRegisterField field_39;
        private IFlagRegisterField field_38;
        private IValueRegisterField field_22;
        private IValueRegisterField field_20;
        private IValueRegisterField field_17;
        private IFlagRegisterField field_18;
        private IFlagRegisterField field_9;
        private IFlagRegisterField field_34;
        private IFlagRegisterField field_37;
        private IFlagRegisterField field_28;
        private IFlagRegisterField field_29;
        private IFlagRegisterField field_35;
        private IFlagRegisterField field_33;
        private IFlagRegisterField field_31;
        private IFlagRegisterField field_27;
        private IValueRegisterField field_8;
        private IEnumRegisterField<Enumeration_C> field_23;
        private IFlagRegisterField field_10;
        private IEnumRegisterField<Enumeration_B> field_5;
        private IEnumRegisterField<Enumeration_A> field_4;
        private IValueRegisterField field_12;
        private IValueRegisterField field_21;
        private IValueRegisterField field_11;
        private IFlagRegisterField field_1;
        private sbyte AGC_frameRssiIntegerPart;
        private sbyte AGC_rssiIntegerPart;
        private bool AGC_rssiStartCommandFromProtimer = false;
        private bool AGC_rssiStartCommandOngoing = false;
        private bool AGC_rssiFirstRead = true;

        private bool LPW0PORTAL_powerUpOngoing = false;

        private bool HOSTPORTAL_powerUpOngoing = false;

        private IValueRegisterField field_46;
        private IFlagRegisterField field_48;
        private IEnumRegisterField<Enumeration_D> field_47;

        private IFlagRegisterField field_370;
        private IFlagRegisterField field_372;
        private IFlagRegisterField field_369;
        private IFlagRegisterField field_371;
        private IFlagRegisterField field_368;
        private IFlagRegisterField field_364;
        private IFlagRegisterField field_367;
        private IFlagRegisterField field_366;
        private IFlagRegisterField field_365;
        private Enumeration_AD SYNTH_state = Enumeration_AD.EnumerationADValue0;

        private IFlagRegisterField field_127;
        private IFlagRegisterField field_124;
        private IFlagRegisterField field_114;
        private IFlagRegisterField field_120;
        private IFlagRegisterField field_122;
        private IFlagRegisterField field_134;
        private IFlagRegisterField field_128;
        private IFlagRegisterField field_130;
        private IFlagRegisterField field_84;
        private IFlagRegisterField field_79;
        private IFlagRegisterField field_142;
        private IFlagRegisterField field_112;
        private IFlagRegisterField field_107;
        private IFlagRegisterField field_69;
        private IFlagRegisterField field_92;
        private IFlagRegisterField field_104;
        private IFlagRegisterField field_144;
        private IFlagRegisterField field_137;
        private IFlagRegisterField field_139;
        private IFlagRegisterField field_83;
        private IFlagRegisterField field_126;
        private IFlagRegisterField field_132;
        private IFlagRegisterField field_116;
        private IFlagRegisterField field_118;
        private IFlagRegisterField field_89;
        private IFlagRegisterField field_85;
        private IFlagRegisterField field_88;
        private IFlagRegisterField field_86;
        private IFlagRegisterField field_90;
        private bool FRC_rxDonePending = false;
        private IFlagRegisterField field_78;
        private bool FRC_rxFrameExitPending = false;
        private IFlagRegisterField field_117;
        private IFlagRegisterField field_133;
        private IFlagRegisterField field_125;
        private IFlagRegisterField field_115;
        private IFlagRegisterField field_121;
        private IFlagRegisterField field_123;
        private IFlagRegisterField field_135;
        private IFlagRegisterField field_129;
        private IFlagRegisterField field_131;
        private IFlagRegisterField field_119;
        private IFlagRegisterField field_141;
        private IFlagRegisterField field_111;
        private IFlagRegisterField field_106;
        private IValueRegisterField field_60;
        private IValueRegisterField field_73;
        private IValueRegisterField field_70;
        private IValueRegisterField field_145;
        private IValueRegisterField field_72;
        private IValueRegisterField field_74;
        private IFlagRegisterField field_61;
        private IValueRegisterField field_75;
        private IValueRegisterField field_64;
        private IEnumRegisterField<Enumeration_H> field_140;
        private IValueRegisterField field_66;
        private IEnumRegisterField<Enumeration_E> field_62;
        private IEnumRegisterField<Enumeration_F> field_65;
        private IEnumRegisterField<Enumeration_J> field_110;
        private IEnumRegisterField<Enumeration_I> field_109;
        private IFlagRegisterField field_67;
        private IValueRegisterField field_71;
        private IFlagRegisterField field_108;
        private IFlagRegisterField field_58;
        private IFlagRegisterField field_59;
        private IValueRegisterField field_63;
        private IEnumRegisterField<Enumeration_H> field_105;
        private IFlagRegisterField field_93;
        private IFlagRegisterField field_68;
        private IFlagRegisterField field_91;
        private IFlagRegisterField field_103;
        private IFlagRegisterField field_143;
        private IFlagRegisterField field_136;
        private IFlagRegisterField field_138;
        private IValueRegisterField field_76;
        private IFlagRegisterField field_80;
        private IFlagRegisterField field_82;
        private IFlagRegisterField field_113;
        private IValueRegisterField field_81;
        private IFlagRegisterField field_96;
        private IFlagRegisterField field_97;
        private IFlagRegisterField field_94;
        private IFlagRegisterField field_95;
        private IFlagRegisterField field_99;
        private IFlagRegisterField field_98;
        private IFlagRegisterField field_102;
        private IFlagRegisterField field_101;
        private IFlagRegisterField field_100;
        private IValueRegisterField field_77;
        private IFlagRegisterField field_87;

        private Enumeration_Z RAC_internalRxState = Enumeration_Z.EnumerationZValue0;
        private Enumeration_AA RAC_internalTxState = Enumeration_AA.EnumerationAAValue0;
        private double RAC_rxTimeAlreadyPassedUs = 0;
        private bool RAC_ongoingRxCollided = false;
        private Enumeration_AB RAC_currentRadioState = Enumeration_AB.EnumerationABValue0;
        private Enumeration_AB RAC_previous1RadioState = Enumeration_AB.EnumerationABValue0;
        private Enumeration_AB RAC_previous2RadioState = Enumeration_AB.EnumerationABValue0;
        private Enumeration_AB RAC_previous3RadioState = Enumeration_AB.EnumerationABValue0;
        private IFlagRegisterField field_342;
        private IFlagRegisterField field_330;
        private IFlagRegisterField field_318;
        private IFlagRegisterField field_322;
        private IFlagRegisterField field_326;
        private IFlagRegisterField field_314;
        private IFlagRegisterField field_335;
        private IFlagRegisterField field_345;
        private IFlagRegisterField field_338;
        private IFlagRegisterField field_337;
        private IFlagRegisterField field_329;
        private IFlagRegisterField field_317;
        private IFlagRegisterField field_321;
        private IFlagRegisterField field_325;
        private IFlagRegisterField field_313;
        private IFlagRegisterField field_334;
        private IFlagRegisterField field_348;
        private IFlagRegisterField field_340;
        private IFlagRegisterField field_346;
        private IFlagRegisterField field_336;
        private IFlagRegisterField field_359;
        private bool RAC_txEnable;
        private IFlagRegisterField field_344;
        private IFlagRegisterField field_332;
        private IFlagRegisterField field_341;
        private IFlagRegisterField field_324;
        private IFlagRegisterField field_303;
        private IValueRegisterField field_300;
        private bool RAC_paOutputLevelRamping = false;
        private IFlagRegisterField field_302;
        private IFlagRegisterField field_320;
        private bool RAC_em1pAckPending;
        private IFlagRegisterField field_296;
        private IFlagRegisterField field_297;
        private IFlagRegisterField field_356;
        private IEnumRegisterField<Enumeration_AB> field_299;
        private IEnumRegisterField<Enumeration_AB> field_305;
        private IFlagRegisterField field_352;
        private IFlagRegisterField field_353;
        private IFlagRegisterField field_354;
        private IFlagRegisterField field_298;
        private IValueRegisterField field_355;
        private IEnumRegisterField<Enumeration_AB> field_358;
        private IValueRegisterField field_301;
        private bool RAC_dcCalDone = false;
        private IFlagRegisterField field_357;
        private IFlagRegisterField field_316;
        private IFlagRegisterField field_310;
        private IFlagRegisterField field_308;
        private IFlagRegisterField field_304;
        private IFlagRegisterField field_350;
        private IFlagRegisterField field_312;
        private IFlagRegisterField field_333;
        private IFlagRegisterField field_347;
        private IFlagRegisterField field_339;
        private IFlagRegisterField field_343;
        private IFlagRegisterField field_331;
        private IFlagRegisterField field_328;
        private IFlagRegisterField field_319;
        private IFlagRegisterField field_327;
        private IFlagRegisterField field_315;
        private IFlagRegisterField field_309;
        private IFlagRegisterField field_307;
        private IFlagRegisterField field_349;
        private IFlagRegisterField field_311;
        private IFlagRegisterField field_323;

        private byte[] currentFrame;
        private uint currentFrameOffset;
        private int currentChannel = 0;

        private readonly IValueRegisterField[] field_362 = new IValueRegisterField[RFMAILBOX_MessageNumber];
        private readonly IFlagRegisterField[] field_360 = new IFlagRegisterField[RFMAILBOX_MessageNumber];
        private readonly IFlagRegisterField[] field_361 = new IFlagRegisterField[RFMAILBOX_MessageNumber];
        private readonly IValueRegisterField[] field_148 = new IValueRegisterField[FSWMAILBOX_MessageNumber];
        private readonly IFlagRegisterField[] field_146 = new IFlagRegisterField[FSWMAILBOX_MessageNumber];
        private readonly IFlagRegisterField[] field_147 = new IFlagRegisterField[FSWMAILBOX_MessageNumber];
        private readonly IValueRegisterField[] field_151 = new IValueRegisterField[HOSTPORTAL_NumberOfMailboxRegisters];
        private readonly IValueRegisterField[] field_157 = new IValueRegisterField[LPW0PORTAL_NumberOfMailboxRegisters];
        private readonly IFlagRegisterField[] field_155 = new IFlagRegisterField[LPW0PORTAL_NumberOfInterrupts];
        private readonly IFlagRegisterField[] field_156 = new IFlagRegisterField[LPW0PORTAL_NumberOfInterrupts];
        private readonly IFlagRegisterField[] field_149 = new IFlagRegisterField[HOSTPORTAL_NumberOfInterrupts];
        private readonly IFlagRegisterField[] field_150 = new IFlagRegisterField[HOSTPORTAL_NumberOfInterrupts];
        private readonly IValueRegisterField[] field_234;
        private readonly PROTIMER_CaptureCompareChannel[] PROTIMER_captureCompareChannel;
        private readonly PROTIMER_TimeoutCounter[] PROTIMER_timeoutCounter;
        private readonly IValueRegisterField[] field_306 = new IValueRegisterField[RAC_NumberOfScratchRegisters];
        private readonly IValueRegisterField[] field_351 = new IValueRegisterField[RAC_NumberOfSequencerStorageRegisters];
        private readonly byte[] FRC_packetBufferCapture;
        private readonly FRC_FrameDescriptor[] FRC_frameDescriptor;
        private readonly Machine machine;
        private readonly CV32E40P sequencer;
        private readonly SiLabs_IRvConfig sequencerConfig;
        private static readonly PseudorandomNumberGenerator random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
        private readonly SiLabs_BUFC_5 bufferController;
        private readonly LimitTimer proTimer;
        private readonly LimitTimer paRampingTimer;
        private readonly LimitTimer rssiUpdateTimer;
        private readonly LimitTimer synthTimer;
        private readonly LimitTimer txTimer;
        private readonly LimitTimer rxTimer;
        private readonly DoubleWordRegisterCollection automaticGainControlRegistersCollection;
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
        const int SNIFF_SYNCWORD_SERIAL_LEN = 4;
        private const uint FRC_NumberOfFrameDescriptors = 4;
        private const uint FRC_PacketBufferCaptureSize = 48;
        private const uint RAC_PowerAmplifierRampingTimeUs = 5;
        private const uint RAC_NumberOfSequencerStorageRegisters = 4;
        private const uint RAC_NumberOfScratchRegisters = 8;
        private const uint PROTIMER_DefaultTimerLimit = 0xFFFFFFFF;
        private const uint PROTIMER_MinimumTimeoutCounterDelay = 8;
        private const uint PROTIMER_NumberOfTimeoutCounters = 3;
        private const uint PROTIMER_NumberOfCaptureCompareChannels = 12;
        private const uint PROTIMER_NumberOfListenBeforeTalkRandomBackoffValues = 8;
        private const uint MODEM_Ble1MbPhyRxChainDelayNanoS = 50000;
        private const uint MODEM_Ble1MbPhyRxDoneDelayNanoS = 11000;
        private const uint MODEM_Ble1MbPhyTxChainDelayNanoS = 3500;
        private const uint MODEM_Ble1MbPhyTxDoneChainDelayNanoS = 750;
        private const uint MODEM_802154PhyRxChainDelayNanoS = 6625;
        private const uint MODEM_802154PhyRxDoneDelayNanoS = 6625;
        private const uint MODEM_802154PhyTxChainDelayNanoS = 500;
        private const uint MODEM_802154PhyTxDoneChainDelayNanoS = 0;
        private const uint AGC_RssiWrapCompensationOffsetDbm = 50;
        private const uint AGC_OQPSK250KbpsPhyCcaRssiMeasurePeriodUs = 128;
        private const uint AGC_OQPSK250KbpsPhyRssiMeasurePeriodUs = 8;
        private const uint AGC_OQPSK250KbpsPhyBackgroundRssiMeasurePeriodUs = 50;
        private const int AGC_RssiInvalid = -128;
        private const uint SYNTH_CalibrationTimeUs = 5U;
        private const uint HOSTPORTAL_NumberOfMailboxRegisters = 8;
        private const uint HOSTPORTAL_NumberOfInterrupts = 32;
        private const uint LPW0PORTAL_NumberOfMailboxRegisters = 8;
        private const uint LPW0PORTAL_NumberOfInterrupts = 32;
        private const uint RFMAILBOX_MessageNumber = 4;
        private const uint FSWMAILBOX_MessageNumber = 4;

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

        public enum MODEM_DemodulatorState
        {
            Off = 0x0,
            TimingSearch = 0x1,
            PreambleSearch = 0x2,
            FrameSearch = 0x3,
            RxFrame = 0x4,
            TimingSearchWithSlidingWindow = 0x5,
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
                if(Field_383.Value != Enumeration_W.EnumerationWValue0)
                {
                    Field_384.Value = true;
                    Field_363.Value = false;
                }
                else
                {
                    Field_363.Value = true;
                    Field_384.Value = false;
                    Field_52.Value = Field_54.Value;
                    Field_291.Value = Field_293.Value;
                }
                parent.PROTIMER_HandleChangedParams();
            }

            public void Stop()
            {
                Field_363.Value = false;
                Field_384.Value = false;
                Finished?.Invoke();
                parent.PROTIMER_HandleChangedParams();
            }

            public void Update(Enumeration_W evt, uint evtCount = 1)
            {
                if((Field_363.Value || Field_384.Value) && evtCount > PROTIMER_MinimumTimeoutCounterDelay)
                {
                    parent.Log(LogLevel.Error, "TOUT{0} Update() passed an evtCount > PROTIMER_MinimumTimeoutCounterDelay ({1})", index, evtCount);
                }

                while(evtCount > 0)
                {
                    if(Field_363.Value && Field_382.Value == evt)
                    {
                        if(Field_291.Value == 0)
                        {
                            Field_291.Value = Field_293.Value;

                            if(Field_52.Value == 0)
                            {
                                Field_385.Value = true;
                                Field_379.Value = true;

                                if(Field_231.Value == Enumeration_V.EnumerationVValue1)
                                {
                                    Field_363.Value = false;
                                    Finished?.Invoke();
                                }
                                else
                                {
                                    Field_52.Value = Field_54.Value;
                                }
                                parent.PROTIMER_TriggerEvent(parent.PROTIMER_GetTimeoutCounterEventFromIndex(this.index, PROTIMER_Event.TimeoutCounter0Underflow));
                                Underflowed?.Invoke();
                            }
                            else
                            {
                                Field_52.Value -= 1;
                            }
                        }
                        else
                        {
                            Field_291.Value -= 1;
                        }

                        var match = (Field_52.Value == Field_53.Value && Field_291.Value == Field_292.Value);

                        if(match)
                        {
                            Field_229.Value |= match;
                            Field_375.Value |= match;
                            parent.UpdateInterrupts();
                            parent.PROTIMER_TriggerEvent(parent.PROTIMER_GetTimeoutCounterEventFromIndex(this.index, PROTIMER_Event.TimeoutCounter0Match));
                        }
                    }

                    if(Field_384.Value && Field_383.Value == evt)
                    {
                        Field_384.Value = false;
                        Field_363.Value = true;
                        Field_52.Value = Field_54.Value;
                        Field_291.Value = Field_293.Value;
                        Synchronized?.Invoke();
                    }

                    evtCount--;
                }
            }

            public bool Interrupt => (Field_385.Value && Field_386.Value)
                                      || (Field_229.Value && Field_230.Value);

            public bool SeqInterrupt => (Field_379.Value && Field_380.Value)
                                         || (Field_375.Value && Field_376.Value);

            public event Action Synchronized;

            public event Action Underflowed;

            public event Action Finished;

            public IEnumRegisterField<Enumeration_W> Field_382;
            public IValueRegisterField Field_293;
            public IValueRegisterField Field_54;
            public IValueRegisterField Field_292;
            public IValueRegisterField Field_291;
            public IValueRegisterField Field_53;
            public IValueRegisterField Field_52;
            public IFlagRegisterField Field_376;
            public IFlagRegisterField Field_379;
            public IFlagRegisterField Field_380;
            public IEnumRegisterField<Enumeration_W> Field_383;
            public IFlagRegisterField Field_230;
            public IFlagRegisterField Field_229;
            public IFlagRegisterField Field_386;
            public IFlagRegisterField Field_385;
            public IFlagRegisterField Field_363;
            public IFlagRegisterField Field_384;
            public IFlagRegisterField Field_375;
            public IEnumRegisterField<Enumeration_V> Field_231;

            private readonly uint index;
            private readonly SiLabs_xG301_LPW parent;
        }

        private class FRC_FrameDescriptor
        {
            public uint? Words => Field_387.Value == 0xFF ? null : (uint?)(Field_387.Value + 1);

            public uint BufferIndex => (uint)Field_45.Value;

            public IValueRegisterField Field_387;
            public IValueRegisterField Field_45;
            public IFlagRegisterField Field_152;
            public IFlagRegisterField Field_49;
            public IValueRegisterField Field_55;
            public IFlagRegisterField Field_381;
            public IFlagRegisterField Field_40;
            public IFlagRegisterField Field_57;
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
                if(Field_51.Value)
                {
                    Field_232.Value = true;
                    Field_377.Value = true;
                }

                Field_51.Value = true;
                Field_154.Value = true;
                Field_374.Value = true;

                Field_295.Value = 0;
                Field_44.Value = baseVal;
                Field_391.Value = wrapVal;

                parent.UpdateInterrupts();
            }

            public bool Interrupt => ((Field_154.Value && Field_153.Value)
                                      || (Field_232.Value && Field_233.Value));

            public bool SeqInterrupt => ((Field_374.Value && Field_373.Value)
                                         || (Field_377.Value && Field_378.Value));

            public IFlagRegisterField Field_154;
            public IValueRegisterField Field_389;
            public IValueRegisterField Field_41;
            public IValueRegisterField Field_42;
            public IValueRegisterField Field_391;
            public IValueRegisterField Field_44;
            public IValueRegisterField Field_295;
            public IEnumRegisterField<Enumeration_R> Field_50;
            public IFlagRegisterField Field_390;
            public IFlagRegisterField Field_43;
            public IFlagRegisterField Field_294;
            public IEnumRegisterField<Enumeration_Q> Field_231;
            public IFlagRegisterField Field_56;
            public IFlagRegisterField Field_378;
            public IFlagRegisterField Field_377;
            public IFlagRegisterField Field_233;
            public IFlagRegisterField Field_232;
            public IFlagRegisterField Field_373;
            public IFlagRegisterField Field_374;
            public IFlagRegisterField Field_153;
            public IValueRegisterField Field_388;
            public IFlagRegisterField Field_51;

            private readonly SiLabs_xG301_LPW parent;
            private readonly uint index;
        }

        private enum Enumeration_G
        {
            EnumerationGValue0    = 0,
            EnumerationGValue1    = 1,
            EnumerationGValue2    = 2,
            EnumerationGValue3    = 3,
            EnumerationGValue4    = 4,
            EnumerationGValue5    = 5,
            EnumerationGValue6    = 6,
            EnumerationGValue7    = 7,
            EnumerationGValue8    = 8,
            EnumerationGValue9    = 9,
            EnumerationGValue10   = 10,
            EnumerationGValue11   = 11,
            EnumerationGValue12   = 12,
            EnumerationGValue13   = 13,
            EnumerationGValue14   = 14,
            EnumerationGValue15   = 15,
            EnumerationGValue16   = 16,
            EnumerationGValue17   = 17,
            EnumerationGValue18   = 18,
            EnumerationGValue19   = 19,
            EnumerationGValue20   = 20,
            EnumerationGValue21   = 21,
            EnumerationGValue22   = 22,
            EnumerationGValue23   = 23,
            EnumerationGValue24   = 24,
            EnumerationGValue25   = 25,
        }

        private enum Enumeration_I
        {
            EnumerationIValue0    = 0,
            EnumerationIValue1    = 1,
            EnumerationIValue2    = 2,
            EnumerationIValue3    = 3,
            EnumerationIValue4    = 4,
        }

        private enum Enumeration_J
        {
            EnumerationJValue0    = 0,
            EnumerationJValue1    = 1,
            EnumerationJValue2    = 2,
        }

        private enum Enumeration_F
        {
            EnumerationFValue0    = 0,
            EnumerationFValue1    = 1,
            EnumerationFValue2    = 2,
            EnumerationFValue3    = 3,
            EnumerationFValue4    = 4,
            EnumerationFValue5    = 5,
            EnumerationFValue6    = 6,
        }

        private enum Enumeration_E
        {
            EnumerationEValue0    = 0,
            EnumerationEValue1    = 1,
        }

        private enum Enumeration_H
        {
            EnumerationHValue0    = 0,
            EnumerationHValue1    = 1,
            EnumerationHValue2    = 2,
            EnumerationHValue3    = 3,
        }

        private enum Enumeration_AB
        {
            EnumerationABValue0   = 0,
            EnumerationABValue1   = 1,
            EnumerationABValue2   = 2,
            EnumerationABValue3   = 3,
            EnumerationABValue4   = 4,
            EnumerationABValue5   = 5,
            EnumerationABValue6   = 6,
            EnumerationABValue7   = 7,
            EnumerationABValue8   = 8,
            EnumerationABValue9   = 9,
        }

        private enum Enumeration_AC
        {
            EnumerationACValue0   = 0,
            EnumerationACValue1   = 1,
            EnumerationACValue2   = 2,
            EnumerationACValue3   = 3,
            EnumerationACValue4   = 4,
            EnumerationACValue5   = 5,
            EnumerationACValue6   = 6,
            EnumerationACValue7   = 7,
            EnumerationACValue8   = 8,
            EnumerationACValue9   = 9,
            EnumerationACValue10  = 10,
            EnumerationACValue11  = 11,
            EnumerationACValue12  = 12,
            EnumerationACValue13  = 13,
            EnumerationACValue14  = 14,
            EnumerationACValue15  = 15,
            EnumerationACValue16  = 16,
            EnumerationACValue17  = 17,
            EnumerationACValue18  = 18,
            EnumerationACValue19  = 19,
            EnumerationACValue20  = 20,
            EnumerationACValue21  = 21,
            EnumerationACValue22  = 22,
            EnumerationACValue23  = 23,
            EnumerationACValue24  = 24,
            EnumerationACValue25  = 25,
            EnumerationACValue26  = 26,
            EnumerationACValue27  = 27,
        }

        private enum Enumeration_Z
        {
            EnumerationZValue0    = 0,
            EnumerationZValue1    = 1,
            EnumerationZValue2    = 2,
        }

        private enum Enumeration_AA
        {
            EnumerationAAValue0   = 0,
            EnumerationAAValue1   = 1,
        }

        private enum Enumeration_U
        {
            EnumerationUValue0    = 0x0,
            EnumerationUValue1    = 0x1,
            EnumerationUValue2    = 0x2,
            EnumerationUValue3    = 0x3,
        }

        private enum Enumeration_P
        {
            EnumerationPValue0    = 0x0,
            EnumerationPValue1    = 0x1,
            EnumerationPValue2    = 0x2,
            EnumerationPValue3    = 0x3,
        }

        private enum Enumeration_Y
        {
            EnumerationYValue0    = 0x0,
            EnumerationYValue1    = 0x1,
            EnumerationYValue2    = 0x2,
            EnumerationYValue3    = 0x3,
        }

        private enum Enumeration_W
        {
            EnumerationWValue0    = 0x0,
            EnumerationWValue1    = 0x1,
            EnumerationWValue2    = 0x2,
            EnumerationWValue3    = 0x3,
        }

        private enum Enumeration_V
        {
            EnumerationVValue0    = 0x0,
            EnumerationVValue1    = 0x1,
        }

        private enum Enumeration_Q
        {
            EnumerationQValue0    = 0,
            EnumerationQValue1    = 1,
            EnumerationQValue2    = 2,
            EnumerationQValue3    = 3,
        }

        private enum Enumeration_R
        {
            EnumerationRValue0    = 0,
            EnumerationRValue1    = 1,
            EnumerationRValue2    = 2,
            EnumerationRValue3    = 3,
            EnumerationRValue4    = 4,
            EnumerationRValue5    = 5,
            EnumerationRValue6    = 6,
            EnumerationRValue7    = 7,
            EnumerationRValue8    = 8,
            EnumerationRValue9    = 9,
            EnumerationRValue10   = 10,
            EnumerationRValue11   = 11,
            EnumerationRValue12   = 12,
            EnumerationRValue13   = 13,
            EnumerationRValue14   = 14,
        }

        private enum Enumeration_X
        {
            EnumerationXValue0,
            EnumerationXValue1,
            EnumerationXValue2,
            EnumerationXValue3,
        }

        private enum Enumeration_S
        {
            EnumerationSValue0    = 0,
            EnumerationSValue1    = 1,
            EnumerationSValue2    = 2,
        }

        private enum Enumeration_T : uint
        {
            EnumerationTValue0    = 0x00000001,
            EnumerationTValue1    = 0x00000002,
            EnumerationTValue2    = 0x00000004,
            EnumerationTValue3    = 0x00000008,
            EnumerationTValue4    = 0x00000010,
            EnumerationTValue5    = 0x00000020,
            EnumerationTValue6    = 0x00000040,
            EnumerationTValue7    = 0x00000080,
            EnumerationTValue8    = 0x00000100,
            EnumerationTValue9    = 0x00000200,
            EnumerationTValue10   = 0x00000400,
            EnumerationTValue11   = 0x00000800,
            EnumerationTValue12   = 0x00001000,
            EnumerationTValue13   = 0x00002000,
            EnumerationTValue14   = 0x00004000,
            EnumerationTValue15   = 0x00008000,
            EnumerationTValue16   = 0x00010000,
        }

        private enum Enumeration_N
        {
            EnumerationNValue0    = 0,
            EnumerationNValue1    = 1,
        }

        private enum Enumeration_L
        {
            EnumerationLValue0    = 0,
            EnumerationLValue1    = 1,
            EnumerationLValue2    = 3,
            EnumerationLValue3    = 7,
        }

        private enum Enumeration_K
        {
            EnumerationKValue0    = 0,
            EnumerationKValue1    = 1,
            EnumerationKValue2    = 2,
        }

        private enum Enumeration_M
        {
            EnumerationMValue0    = 0,
            EnumerationMValue1    = 1,
            EnumerationMValue2    = 2,
            EnumerationMValue3    = 3,
            EnumerationMValue4    = 4,
            EnumerationMValue5    = 5,
            EnumerationMValue6    = 6,
            EnumerationMValue7    = 7,
        }

        private enum Enumeration_O
        {
            EnumerationOValue0    = 0,
            EnumerationOValue1    = 1,
            EnumerationOValue2    = 2,
            EnumerationOValue3    = 3,
        }

        private enum Enumeration_A
        {
            EnumerationAValue0    = 0,
            EnumerationAValue1    = 1,
            EnumerationAValue2    = 2,
            EnumerationAValue3    = 3,
        }

        private enum Enumeration_B
        {
            EnumerationBValue0    = 0,
            EnumerationBValue1    = 1,
        }

        private enum Enumeration_C
        {
            EnumerationCValue0    = 0,
            EnumerationCValue1    = 1,
            EnumerationCValue2    = 2,
            EnumerationCValue3    = 3,
            EnumerationCValue4    = 4,
        }

        private enum Enumeration_AD
        {
            EnumerationADValue0   = 0,
            EnumerationADValue1   = 1,
            EnumerationADValue2   = 2,
        }

        private enum Enumeration_D
        {
            EnumerationDValue0    = 0x0,
            EnumerationDValue1    = 0x1,
            EnumerationDValue2    = 0x2,
            EnumerationDValue3    = 0x3,
        }

        private enum Registers_C : long
        {
            RegC_1                = 0x0000,
            RegC_2                = 0x0004,
            RegC_3                = 0x0008,
            RegC_4                = 0x000C,
            RegC_5                = 0x0010,
            RegC_6                = 0x0014,
            RegC_7                = 0x0018,
            RegC_8                = 0x001C,
            RegC_9                = 0x0020,
            RegC_10               = 0x0024,
            RegC_11               = 0x0028,
            RegC_12               = 0x002C,
            RegC_13               = 0x0030,
            RegC_14               = 0x0034,
            RegC_15               = 0x0038,
            RegC_16               = 0x003C,
            RegC_17               = 0x0040,
            RegC_18               = 0x0044,
            RegC_19               = 0x0048,
            RegC_20               = 0x004C,
            RegC_21               = 0x0050,
            RegC_22               = 0x0054,
            RegC_23               = 0x0058,
            RegC_24               = 0x005C,
            RegC_25               = 0x0060,
            RegC_26               = 0x0064,
            RegC_27               = 0x0068,
            RegC_28               = 0x006C,
            RegC_29               = 0x0070,
            RegC_30               = 0x0078,
            RegC_31               = 0x0084,
            RegC_32               = 0x0088,
            RegC_33               = 0x008C,
            RegC_34               = 0x0090,
            RegC_35               = 0x0094,
            RegC_36               = 0x0098,
            RegC_37               = 0x009C,
            RegC_38               = 0x00A0,
            RegC_39               = 0x00A4,
            RegC_40               = 0x00A8,
            RegC_41               = 0x00AC,
            RegC_42               = 0x00B4,
            RegC_43               = 0x00B8,
            RegC_44               = 0x00BC,
            RegC_45               = 0x00C0,
            RegC_46               = 0x00C4,
            RegC_47               = 0x00C8,
            RegC_48               = 0x00CC,
            RegC_49               = 0x00D0,
            RegC_50               = 0x00D4,
            RegC_51               = 0x00D8,
            RegC_52               = 0x00DC,
            RegC_53               = 0x00E0,
            RegC_54               = 0x00E4,
            RegC_55               = 0x00E8,
            RegC_56               = 0x00EC,
            RegC_57               = 0x00F0,
            RegC_58               = 0x00F4,
            RegC_59               = 0x00F8,
            RegC_60               = 0x00FC,
            RegC_61               = 0x0100,
            RegC_62               = 0x0104,
            RegC_63               = 0x0108,
            RegC_64               = 0x010C,
            RegC_65               = 0x0110,
            RegC_66               = 0x0114,
            RegC_67               = 0x0118,
            RegC_68               = 0x011C,
            RegC_69               = 0x0140,
            RegC_70               = 0x0144,
            RegC_71               = 0x0148,
            RegC_72               = 0x014C,
            RegC_73               = 0x0150,
            RegC_74               = 0x0154,
            RegC_75               = 0x0158,
            RegC_76               = 0x015C,
            RegC_77               = 0x0160,
            RegC_78               = 0x0164,
            RegC_79               = 0x0168,
            RegC_80               = 0x016C,
            RegC_81               = 0x0170,
            RegC_82               = 0x0174,
            RegC_83               = 0x0178,
            RegC_84               = 0x017C,
            RegC_85               = 0x0180,
            RegC_86               = 0x0184,
            RegC_87               = 0x1000,
            RegC_88               = 0x1004,
            RegC_89               = 0x1008,
            RegC_90               = 0x100C,
            RegC_91               = 0x1010,
            RegC_92               = 0x1014,
            RegC_93               = 0x1018,
            RegC_94               = 0x101C,
            RegC_95               = 0x1020,
            RegC_96               = 0x1024,
            RegC_97               = 0x1028,
            RegC_98               = 0x102C,
            RegC_99               = 0x1030,
            RegC_100              = 0x1034,
            RegC_101              = 0x1038,
            RegC_102              = 0x103C,
            RegC_103              = 0x1040,
            RegC_104              = 0x1044,
            RegC_105              = 0x1048,
            RegC_106              = 0x104C,
            RegC_107              = 0x1050,
            RegC_108              = 0x1054,
            RegC_109              = 0x1058,
            RegC_110              = 0x105C,
            RegC_111              = 0x1060,
            RegC_112              = 0x1064,
            RegC_113              = 0x1068,
            RegC_114              = 0x106C,
            RegC_115              = 0x1070,
            RegC_116              = 0x1078,
            RegC_117              = 0x1084,
            RegC_118              = 0x1088,
            RegC_119              = 0x108C,
            RegC_120              = 0x1090,
            RegC_121              = 0x1094,
            RegC_122              = 0x1098,
            RegC_123              = 0x109C,
            RegC_124              = 0x10A0,
            RegC_125              = 0x10A4,
            RegC_126              = 0x10A8,
            RegC_127              = 0x10AC,
            RegC_128              = 0x10B4,
            RegC_129              = 0x10B8,
            RegC_130              = 0x10BC,
            RegC_131              = 0x10C0,
            RegC_132              = 0x10C4,
            RegC_133              = 0x10C8,
            RegC_134              = 0x10CC,
            RegC_135              = 0x10D0,
            RegC_136              = 0x10D4,
            RegC_137              = 0x10D8,
            RegC_138              = 0x10DC,
            RegC_139              = 0x10E0,
            RegC_140              = 0x10E4,
            RegC_141              = 0x10E8,
            RegC_142              = 0x10EC,
            RegC_143              = 0x10F0,
            RegC_144              = 0x10F4,
            RegC_145              = 0x10F8,
            RegC_146              = 0x10FC,
            RegC_147              = 0x1100,
            RegC_148              = 0x1104,
            RegC_149              = 0x1108,
            RegC_150              = 0x110C,
            RegC_151              = 0x1110,
            RegC_152              = 0x1114,
            RegC_153              = 0x1118,
            RegC_154              = 0x111C,
            RegC_155              = 0x1140,
            RegC_156              = 0x1144,
            RegC_157              = 0x1148,
            RegC_158              = 0x114C,
            RegC_159              = 0x1150,
            RegC_160              = 0x1154,
            RegC_161              = 0x1158,
            RegC_162              = 0x115C,
            RegC_163              = 0x1160,
            RegC_164              = 0x1164,
            RegC_165              = 0x1168,
            RegC_166              = 0x116C,
            RegC_167              = 0x1170,
            RegC_168              = 0x1174,
            RegC_169              = 0x1178,
            RegC_170              = 0x117C,
            RegC_171              = 0x1180,
            RegC_172              = 0x1184,
            RegC_173              = 0x2000,
            RegC_174              = 0x2004,
            RegC_175              = 0x2008,
            RegC_176              = 0x200C,
            RegC_177              = 0x2010,
            RegC_178              = 0x2014,
            RegC_179              = 0x2018,
            RegC_180              = 0x201C,
            RegC_181              = 0x2020,
            RegC_182              = 0x2024,
            RegC_183              = 0x2028,
            RegC_184              = 0x202C,
            RegC_185              = 0x2030,
            RegC_186              = 0x2034,
            RegC_187              = 0x2038,
            RegC_188              = 0x203C,
            RegC_189              = 0x2040,
            RegC_190              = 0x2044,
            RegC_191              = 0x2048,
            RegC_192              = 0x204C,
            RegC_193              = 0x2050,
            RegC_194              = 0x2054,
            RegC_195              = 0x2058,
            RegC_196              = 0x205C,
            RegC_197              = 0x2060,
            RegC_198              = 0x2064,
            RegC_199              = 0x2068,
            RegC_200              = 0x206C,
            RegC_201              = 0x2070,
            RegC_202              = 0x2078,
            RegC_203              = 0x2084,
            RegC_204              = 0x2088,
            RegC_205              = 0x208C,
            RegC_206              = 0x2090,
            RegC_207              = 0x2094,
            RegC_208              = 0x2098,
            RegC_209              = 0x209C,
            RegC_210              = 0x20A0,
            RegC_211              = 0x20A4,
            RegC_212              = 0x20A8,
            RegC_213              = 0x20AC,
            RegC_214              = 0x20B4,
            RegC_215              = 0x20B8,
            RegC_216              = 0x20BC,
            RegC_217              = 0x20C0,
            RegC_218              = 0x20C4,
            RegC_219              = 0x20C8,
            RegC_220              = 0x20CC,
            RegC_221              = 0x20D0,
            RegC_222              = 0x20D4,
            RegC_223              = 0x20D8,
            RegC_224              = 0x20DC,
            RegC_225              = 0x20E0,
            RegC_226              = 0x20E4,
            RegC_227              = 0x20E8,
            RegC_228              = 0x20EC,
            RegC_229              = 0x20F0,
            RegC_230              = 0x20F4,
            RegC_231              = 0x20F8,
            RegC_232              = 0x20FC,
            RegC_233              = 0x2100,
            RegC_234              = 0x2104,
            RegC_235              = 0x2108,
            RegC_236              = 0x210C,
            RegC_237              = 0x2110,
            RegC_238              = 0x2114,
            RegC_239              = 0x2118,
            RegC_240              = 0x211C,
            RegC_241              = 0x2140,
            RegC_242              = 0x2144,
            RegC_243              = 0x2148,
            RegC_244              = 0x214C,
            RegC_245              = 0x2150,
            RegC_246              = 0x2154,
            RegC_247              = 0x2158,
            RegC_248              = 0x215C,
            RegC_249              = 0x2160,
            RegC_250              = 0x2164,
            RegC_251              = 0x2168,
            RegC_252              = 0x216C,
            RegC_253              = 0x2170,
            RegC_254              = 0x2174,
            RegC_255              = 0x2178,
            RegC_256              = 0x217C,
            RegC_257              = 0x2180,
            RegC_258              = 0x2184,
            RegC_259              = 0x3000,
            RegC_260              = 0x3004,
            RegC_261              = 0x3008,
            RegC_262              = 0x300C,
            RegC_263              = 0x3010,
            RegC_264              = 0x3014,
            RegC_265              = 0x3018,
            RegC_266              = 0x301C,
            RegC_267              = 0x3020,
            RegC_268              = 0x3024,
            RegC_269              = 0x3028,
            RegC_270              = 0x302C,
            RegC_271              = 0x3030,
            RegC_272              = 0x3034,
            RegC_273              = 0x3038,
            RegC_274              = 0x303C,
            RegC_275              = 0x3040,
            RegC_276              = 0x3044,
            RegC_277              = 0x3048,
            RegC_278              = 0x304C,
            RegC_279              = 0x3050,
            RegC_280              = 0x3054,
            RegC_281              = 0x3058,
            RegC_282              = 0x305C,
            RegC_283              = 0x3060,
            RegC_284              = 0x3064,
            RegC_285              = 0x3068,
            RegC_286              = 0x306C,
            RegC_287              = 0x3070,
            RegC_288              = 0x3078,
            RegC_289              = 0x3084,
            RegC_290              = 0x3088,
            RegC_291              = 0x308C,
            RegC_292              = 0x3090,
            RegC_293              = 0x3094,
            RegC_294              = 0x3098,
            RegC_295              = 0x309C,
            RegC_296              = 0x30A0,
            RegC_297              = 0x30A4,
            RegC_298              = 0x30A8,
            RegC_299              = 0x30AC,
            RegC_300              = 0x30B4,
            RegC_301              = 0x30B8,
            RegC_302              = 0x30BC,
            RegC_303              = 0x30C0,
            RegC_304              = 0x30C4,
            RegC_305              = 0x30C8,
            RegC_306              = 0x30CC,
            RegC_307              = 0x30D0,
            RegC_308              = 0x30D4,
            RegC_309              = 0x30D8,
            RegC_310              = 0x30DC,
            RegC_311              = 0x30E0,
            RegC_312              = 0x30E4,
            RegC_313              = 0x30E8,
            RegC_314              = 0x30EC,
            RegC_315              = 0x30F0,
            RegC_316              = 0x30F4,
            RegC_317              = 0x30F8,
            RegC_318              = 0x30FC,
            RegC_319              = 0x3100,
            RegC_320              = 0x3104,
            RegC_321              = 0x3108,
            RegC_322              = 0x310C,
            RegC_323              = 0x3110,
            RegC_324              = 0x3114,
            RegC_325              = 0x3118,
            RegC_326              = 0x311C,
            RegC_327              = 0x3140,
            RegC_328              = 0x3144,
            RegC_329              = 0x3148,
            RegC_330              = 0x314C,
            RegC_331              = 0x3150,
            RegC_332              = 0x3154,
            RegC_333              = 0x3158,
            RegC_334              = 0x315C,
            RegC_335              = 0x3160,
            RegC_336              = 0x3164,
            RegC_337              = 0x3168,
            RegC_338              = 0x316C,
            RegC_339              = 0x3170,
            RegC_340              = 0x3174,
            RegC_341              = 0x3178,
            RegC_342              = 0x317C,
            RegC_343              = 0x3180,
            RegC_344              = 0x3184,
        }

        private enum Registers_G : long
        {
            RegG_1                = 0x0000,
            RegG_2                = 0x0004,
            RegG_3                = 0x0008,
            RegG_4                = 0x000C,
            RegG_5                = 0x0010,
            RegG_6                = 0x0014,
            RegG_7                = 0x0018,
            RegG_8                = 0x001C,
            RegG_9                = 0x0020,
            RegG_10               = 0x0024,
            RegG_11               = 0x0028,
            RegG_12               = 0x002C,
            RegG_13               = 0x0030,
            RegG_14               = 0x0034,
            RegG_15               = 0x0038,
            RegG_16               = 0x003C,
            RegG_17               = 0x0040,
            RegG_18               = 0x0044,
            RegG_19               = 0x0048,
            RegG_20               = 0x004C,
            RegG_21               = 0x0050,
            RegG_22               = 0x0054,
            RegG_23               = 0x0058,
            RegG_24               = 0x005C,
            RegG_25               = 0x0060,
            RegG_26               = 0x0064,
            RegG_27               = 0x0068,
            RegG_28               = 0x006C,
            RegG_29               = 0x0070,
            RegG_30               = 0x0074,
            RegG_31               = 0x0078,
            RegG_32               = 0x007C,
            RegG_33               = 0x0080,
            RegG_34               = 0x0084,
            RegG_35               = 0x0088,
            RegG_36               = 0x008C,
            RegG_37               = 0x0090,
            RegG_38               = 0x0094,
            RegG_39               = 0x0098,
            RegG_40               = 0x009C,
            RegG_41               = 0x00A0,
            RegG_42               = 0x00A4,
            RegG_43               = 0x00A8,
            RegG_44               = 0x00AC,
            RegG_45               = 0x00B0,
            RegG_46               = 0x00B4,
            RegG_47               = 0x00B8,
            RegG_48               = 0x00BC,
            RegG_49               = 0x00C0,
            RegG_50               = 0x00C4,
            RegG_51               = 0x00C8,
            RegG_52               = 0x00CC,
            RegG_53               = 0x00D0,
            RegG_54               = 0x00D4,
            RegG_55               = 0x00D8,
            RegG_56               = 0x00E0,
            RegG_57               = 0x00E4,
            RegG_58               = 0x0118,
            RegG_59               = 0x011C,
            RegG_60               = 0x0120,
            RegG_61               = 0x0124,
            RegG_62               = 0x0128,
            RegG_63               = 0x013C,
            RegG_64               = 0x0154,
            RegG_65               = 0x0158,
            RegG_66               = 0x015C,
            RegG_67               = 0x0160,
            RegG_68               = 0x0164,
            RegG_69               = 0x0168,
            RegG_70               = 0x016C,
            RegG_71               = 0x0170,
            RegG_72               = 0x0174,
            RegG_73               = 0x0178,
            RegG_74               = 0x017C,
            RegG_75               = 0x0180,
            RegG_76               = 0x0184,
            RegG_77               = 0x0188,
            RegG_78               = 0x018C,
            RegG_79               = 0x0190,
            RegG_80               = 0x0194,
            RegG_81               = 0x0198,
            RegG_82               = 0x01A4,
            RegG_83               = 0x01A8,
            RegG_84               = 0x01AC,
            RegG_85               = 0x01B8,
            RegG_86               = 0x01BC,
            RegG_87               = 0x01C0,
            RegG_88               = 0x01C4,
            RegG_89               = 0x01C8,
            RegG_90               = 0x01CC,
            RegG_91               = 0x01DC,
            RegG_92               = 0x01E0,
            RegG_93               = 0x01E4,
            RegG_94               = 0x01E8,
            RegG_95               = 0x01EC,
            RegG_96               = 0x01F0,
            RegG_97               = 0x01F4,
            RegG_98               = 0x01F8,
            RegG_99               = 0x01FC,
            RegG_100              = 0x0200,
            RegG_101              = 0x0204,
            RegG_102              = 0x0208,
            RegG_103              = 0x020C,
            RegG_104              = 0x0210,
            RegG_105              = 0x0214,
            RegG_106              = 0x0218,
            RegG_107              = 0x021C,
            RegG_108              = 0x0220,
            RegG_109              = 0x0224,
            RegG_110              = 0x0228,
            RegG_111              = 0x022C,
            RegG_112              = 0x0230,
            RegG_113              = 0x0234,
            RegG_114              = 0x0238,
            RegG_115              = 0x023C,
            RegG_116              = 0x0240,
            RegG_117              = 0x0244,
            RegG_118              = 0x0248,
            RegG_119              = 0x024C,
            RegG_120              = 0x0250,
            RegG_121              = 0x0254,
            RegG_122              = 0x0258,
            RegG_123              = 0x025C,
            RegG_124              = 0x0260,
            RegG_125              = 0x0264,
            RegG_126              = 0x0268,
            RegG_127              = 0x0274,
            RegG_128              = 0x0278,
            RegG_129              = 0x027C,
            RegG_130              = 0x0280,
            RegG_131              = 0x0284,
            RegG_132              = 0x0288,
            RegG_133              = 0x028C,
            RegG_134              = 0x0290,
            RegG_135              = 0x0294,
            RegG_136              = 0x0298,
            RegG_137              = 0x02A0,
            RegG_138              = 0x02A4,
            RegG_139              = 0x02A8,
            RegG_140              = 0x02AC,
            RegG_141              = 0x02B0,
            RegG_142              = 0x02B4,
            RegG_143              = 0x02B8,
            RegG_144              = 0x02BC,
            RegG_145              = 0x02C0,
            RegG_146              = 0x02C4,
            RegG_147              = 0x02C8,
            RegG_148              = 0x02CC,
            RegG_149              = 0x02D0,
            RegG_150              = 0x02D4,
            RegG_151              = 0x02D8,
            RegG_152              = 0x02DC,
            RegG_153              = 0x02E0,
            RegG_154              = 0x02E4,
            RegG_155              = 0x02E8,
            RegG_156              = 0x02EC,
            RegG_157              = 0x02F0,
            RegG_158              = 0x02F4,
            RegG_159              = 0x02F8,
            RegG_160              = 0x02FC,
            RegG_161              = 0x0300,
            RegG_162              = 0x0304,
            RegG_163              = 0x0308,
            RegG_164              = 0x030C,
            RegG_165              = 0x0310,
            RegG_166              = 0x0314,
            RegG_167              = 0x0318,
            RegG_168              = 0x031C,
            RegG_169              = 0x0320,
            RegG_170              = 0x0324,
            RegG_171              = 0x0328,
            RegG_172              = 0x032C,
            RegG_173              = 0x0330,
            RegG_174              = 0x0334,
            RegG_175              = 0x0338,
            RegG_176              = 0x033C,
            RegG_177              = 0x0340,
            RegG_178              = 0x0344,
            RegG_179              = 0x0348,
            RegG_180              = 0x034C,
            RegG_181              = 0x0350,
            RegG_182              = 0x0354,
            RegG_183              = 0x0358,
            RegG_184              = 0x035C,
            RegG_185              = 0x0360,
            RegG_186              = 0x0364,
            RegG_187              = 0x0368,
            RegG_188              = 0x036C,
            RegG_189              = 0x0370,
            RegG_190              = 0x0374,
            RegG_191              = 0x03C0,
            RegG_192              = 0x03C4,
            RegG_193              = 0x03C8,
            RegG_194              = 0x03CC,
            RegG_195              = 0x03D0,
            RegG_196              = 0x03D4,
            RegG_197              = 0x03D8,
            RegG_198              = 0x03DC,
            RegG_199              = 0x03E0,
            RegG_200              = 0x03E4,
            RegG_201              = 0x03E8,
            RegG_202              = 0x03EC,
            RegG_203              = 0x03F0,
            RegG_204              = 0x03F4,
            RegG_205              = 0x0414,
            RegG_206              = 0x0418,
            RegG_207              = 0x041C,
            RegG_208              = 0x0420,
            RegG_209              = 0x0424,
            RegG_210              = 0x0428,
            RegG_211              = 0x0444,
            RegG_212              = 0x0470,
            RegG_213              = 0x0480,
            RegG_214              = 0x0484,
            RegG_215              = 0x0488,
            RegG_216              = 0x048C,
            RegG_217              = 0x0490,
            RegG_218              = 0x04A0,
            RegG_219              = 0x04A4,
            RegG_220              = 0x04A8,
            RegG_221              = 0x04AC,
            RegG_222              = 0x0500,
            RegG_223              = 0x0504,
            RegG_224              = 0x0508,
            RegG_225              = 0x050C,
            RegG_226              = 0x0510,
            RegG_227              = 0x0514,
            RegG_228              = 0x0518,
            RegG_229              = 0x051C,
            RegG_230              = 0x0520,
            RegG_231              = 0x0528,
            RegG_232              = 0x052C,
            RegG_233              = 0x0540,
            RegG_234              = 0x0544,
            RegG_235              = 0x0580,
            RegG_236              = 0x0584,
            RegG_237              = 0x0588,
            RegG_238              = 0x1000,
            RegG_239              = 0x1004,
            RegG_240              = 0x1008,
            RegG_241              = 0x100C,
            RegG_242              = 0x1010,
            RegG_243              = 0x1014,
            RegG_244              = 0x1018,
            RegG_245              = 0x101C,
            RegG_246              = 0x1020,
            RegG_247              = 0x1024,
            RegG_248              = 0x1028,
            RegG_249              = 0x102C,
            RegG_250              = 0x1030,
            RegG_251              = 0x1034,
            RegG_252              = 0x1038,
            RegG_253              = 0x103C,
            RegG_254              = 0x1040,
            RegG_255              = 0x1044,
            RegG_256              = 0x1048,
            RegG_257              = 0x104C,
            RegG_258              = 0x1050,
            RegG_259              = 0x1054,
            RegG_260              = 0x1058,
            RegG_261              = 0x105C,
            RegG_262              = 0x1060,
            RegG_263              = 0x1064,
            RegG_264              = 0x1068,
            RegG_265              = 0x106C,
            RegG_266              = 0x1070,
            RegG_267              = 0x1074,
            RegG_268              = 0x1078,
            RegG_269              = 0x107C,
            RegG_270              = 0x1080,
            RegG_271              = 0x1084,
            RegG_272              = 0x1088,
            RegG_273              = 0x108C,
            RegG_274              = 0x1090,
            RegG_275              = 0x1094,
            RegG_276              = 0x1098,
            RegG_277              = 0x109C,
            RegG_278              = 0x10A0,
            RegG_279              = 0x10A4,
            RegG_280              = 0x10A8,
            RegG_281              = 0x10AC,
            RegG_282              = 0x10B0,
            RegG_283              = 0x10B4,
            RegG_284              = 0x10B8,
            RegG_285              = 0x10BC,
            RegG_286              = 0x10C0,
            RegG_287              = 0x10C4,
            RegG_288              = 0x10C8,
            RegG_289              = 0x10CC,
            RegG_290              = 0x10D0,
            RegG_291              = 0x10D4,
            RegG_292              = 0x10D8,
            RegG_293              = 0x10E0,
            RegG_294              = 0x10E4,
            RegG_295              = 0x1118,
            RegG_296              = 0x111C,
            RegG_297              = 0x1120,
            RegG_298              = 0x1124,
            RegG_299              = 0x1128,
            RegG_300              = 0x113C,
            RegG_301              = 0x1154,
            RegG_302              = 0x1158,
            RegG_303              = 0x115C,
            RegG_304              = 0x1160,
            RegG_305              = 0x1164,
            RegG_306              = 0x1168,
            RegG_307              = 0x116C,
            RegG_308              = 0x1170,
            RegG_309              = 0x1174,
            RegG_310              = 0x1178,
            RegG_311              = 0x117C,
            RegG_312              = 0x1180,
            RegG_313              = 0x1184,
            RegG_314              = 0x1188,
            RegG_315              = 0x118C,
            RegG_316              = 0x1190,
            RegG_317              = 0x1194,
            RegG_318              = 0x1198,
            RegG_319              = 0x11A4,
            RegG_320              = 0x11A8,
            RegG_321              = 0x11AC,
            RegG_322              = 0x11B8,
            RegG_323              = 0x11BC,
            RegG_324              = 0x11C0,
            RegG_325              = 0x11C4,
            RegG_326              = 0x11C8,
            RegG_327              = 0x11CC,
            RegG_328              = 0x11DC,
            RegG_329              = 0x11E0,
            RegG_330              = 0x11E4,
            RegG_331              = 0x11E8,
            RegG_332              = 0x11EC,
            RegG_333              = 0x11F0,
            RegG_334              = 0x11F4,
            RegG_335              = 0x11F8,
            RegG_336              = 0x11FC,
            RegG_337              = 0x1200,
            RegG_338              = 0x1204,
            RegG_339              = 0x1208,
            RegG_340              = 0x120C,
            RegG_341              = 0x1210,
            RegG_342              = 0x1214,
            RegG_343              = 0x1218,
            RegG_344              = 0x121C,
            RegG_345              = 0x1220,
            RegG_346              = 0x1224,
            RegG_347              = 0x1228,
            RegG_348              = 0x122C,
            RegG_349              = 0x1230,
            RegG_350              = 0x1234,
            RegG_351              = 0x1238,
            RegG_352              = 0x123C,
            RegG_353              = 0x1240,
            RegG_354              = 0x1244,
            RegG_355              = 0x1248,
            RegG_356              = 0x124C,
            RegG_357              = 0x1250,
            RegG_358              = 0x1254,
            RegG_359              = 0x1258,
            RegG_360              = 0x125C,
            RegG_361              = 0x1260,
            RegG_362              = 0x1264,
            RegG_363              = 0x1268,
            RegG_364              = 0x1274,
            RegG_365              = 0x1278,
            RegG_366              = 0x127C,
            RegG_367              = 0x1280,
            RegG_368              = 0x1284,
            RegG_369              = 0x1288,
            RegG_370              = 0x128C,
            RegG_371              = 0x1290,
            RegG_372              = 0x1294,
            RegG_373              = 0x1298,
            RegG_374              = 0x12A0,
            RegG_375              = 0x12A4,
            RegG_376              = 0x12A8,
            RegG_377              = 0x12AC,
            RegG_378              = 0x12B0,
            RegG_379              = 0x12B4,
            RegG_380              = 0x12B8,
            RegG_381              = 0x12BC,
            RegG_382              = 0x12C0,
            RegG_383              = 0x12C4,
            RegG_384              = 0x12C8,
            RegG_385              = 0x12CC,
            RegG_386              = 0x12D0,
            RegG_387              = 0x12D4,
            RegG_388              = 0x12D8,
            RegG_389              = 0x12DC,
            RegG_390              = 0x12E0,
            RegG_391              = 0x12E4,
            RegG_392              = 0x12E8,
            RegG_393              = 0x12EC,
            RegG_394              = 0x12F0,
            RegG_395              = 0x12F4,
            RegG_396              = 0x12F8,
            RegG_397              = 0x12FC,
            RegG_398              = 0x1300,
            RegG_399              = 0x1304,
            RegG_400              = 0x1308,
            RegG_401              = 0x130C,
            RegG_402              = 0x1310,
            RegG_403              = 0x1314,
            RegG_404              = 0x1318,
            RegG_405              = 0x131C,
            RegG_406              = 0x1320,
            RegG_407              = 0x1324,
            RegG_408              = 0x1328,
            RegG_409              = 0x132C,
            RegG_410              = 0x1330,
            RegG_411              = 0x1334,
            RegG_412              = 0x1338,
            RegG_413              = 0x133C,
            RegG_414              = 0x1340,
            RegG_415              = 0x1344,
            RegG_416              = 0x1348,
            RegG_417              = 0x134C,
            RegG_418              = 0x1350,
            RegG_419              = 0x1354,
            RegG_420              = 0x1358,
            RegG_421              = 0x135C,
            RegG_422              = 0x1360,
            RegG_423              = 0x1364,
            RegG_424              = 0x1368,
            RegG_425              = 0x136C,
            RegG_426              = 0x1370,
            RegG_427              = 0x1374,
            RegG_428              = 0x13C0,
            RegG_429              = 0x13C4,
            RegG_430              = 0x13C8,
            RegG_431              = 0x13CC,
            RegG_432              = 0x13D0,
            RegG_433              = 0x13D4,
            RegG_434              = 0x13D8,
            RegG_435              = 0x13DC,
            RegG_436              = 0x13E0,
            RegG_437              = 0x13E4,
            RegG_438              = 0x13E8,
            RegG_439              = 0x13EC,
            RegG_440              = 0x13F0,
            RegG_441              = 0x13F4,
            RegG_442              = 0x1414,
            RegG_443              = 0x1418,
            RegG_444              = 0x141C,
            RegG_445              = 0x1420,
            RegG_446              = 0x1424,
            RegG_447              = 0x1428,
            RegG_448              = 0x1444,
            RegG_449              = 0x1470,
            RegG_450              = 0x1480,
            RegG_451              = 0x1484,
            RegG_452              = 0x1488,
            RegG_453              = 0x148C,
            RegG_454              = 0x1490,
            RegG_455              = 0x14A0,
            RegG_456              = 0x14A4,
            RegG_457              = 0x14A8,
            RegG_458              = 0x14AC,
            RegG_459              = 0x1500,
            RegG_460              = 0x1504,
            RegG_461              = 0x1508,
            RegG_462              = 0x150C,
            RegG_463              = 0x1510,
            RegG_464              = 0x1514,
            RegG_465              = 0x1518,
            RegG_466              = 0x151C,
            RegG_467              = 0x1520,
            RegG_468              = 0x1528,
            RegG_469              = 0x152C,
            RegG_470              = 0x1540,
            RegG_471              = 0x1544,
            RegG_472              = 0x1580,
            RegG_473              = 0x1584,
            RegG_474              = 0x1588,
            RegG_475              = 0x2000,
            RegG_476              = 0x2004,
            RegG_477              = 0x2008,
            RegG_478              = 0x200C,
            RegG_479              = 0x2010,
            RegG_480              = 0x2014,
            RegG_481              = 0x2018,
            RegG_482              = 0x201C,
            RegG_483              = 0x2020,
            RegG_484              = 0x2024,
            RegG_485              = 0x2028,
            RegG_486              = 0x202C,
            RegG_487              = 0x2030,
            RegG_488              = 0x2034,
            RegG_489              = 0x2038,
            RegG_490              = 0x203C,
            RegG_491              = 0x2040,
            RegG_492              = 0x2044,
            RegG_493              = 0x2048,
            RegG_494              = 0x204C,
            RegG_495              = 0x2050,
            RegG_496              = 0x2054,
            RegG_497              = 0x2058,
            RegG_498              = 0x205C,
            RegG_499              = 0x2060,
            RegG_500              = 0x2064,
            RegG_501              = 0x2068,
            RegG_502              = 0x206C,
            RegG_503              = 0x2070,
            RegG_504              = 0x2074,
            RegG_505              = 0x2078,
            RegG_506              = 0x207C,
            RegG_507              = 0x2080,
            RegG_508              = 0x2084,
            RegG_509              = 0x2088,
            RegG_510              = 0x208C,
            RegG_511              = 0x2090,
            RegG_512              = 0x2094,
            RegG_513              = 0x2098,
            RegG_514              = 0x209C,
            RegG_515              = 0x20A0,
            RegG_516              = 0x20A4,
            RegG_517              = 0x20A8,
            RegG_518              = 0x20AC,
            RegG_519              = 0x20B0,
            RegG_520              = 0x20B4,
            RegG_521              = 0x20B8,
            RegG_522              = 0x20BC,
            RegG_523              = 0x20C0,
            RegG_524              = 0x20C4,
            RegG_525              = 0x20C8,
            RegG_526              = 0x20CC,
            RegG_527              = 0x20D0,
            RegG_528              = 0x20D4,
            RegG_529              = 0x20D8,
            RegG_530              = 0x20E0,
            RegG_531              = 0x20E4,
            RegG_532              = 0x2118,
            RegG_533              = 0x211C,
            RegG_534              = 0x2120,
            RegG_535              = 0x2124,
            RegG_536              = 0x2128,
            RegG_537              = 0x213C,
            RegG_538              = 0x2154,
            RegG_539              = 0x2158,
            RegG_540              = 0x215C,
            RegG_541              = 0x2160,
            RegG_542              = 0x2164,
            RegG_543              = 0x2168,
            RegG_544              = 0x216C,
            RegG_545              = 0x2170,
            RegG_546              = 0x2174,
            RegG_547              = 0x2178,
            RegG_548              = 0x217C,
            RegG_549              = 0x2180,
            RegG_550              = 0x2184,
            RegG_551              = 0x2188,
            RegG_552              = 0x218C,
            RegG_553              = 0x2190,
            RegG_554              = 0x2194,
            RegG_555              = 0x2198,
            RegG_556              = 0x21A4,
            RegG_557              = 0x21A8,
            RegG_558              = 0x21AC,
            RegG_559              = 0x21B8,
            RegG_560              = 0x21BC,
            RegG_561              = 0x21C0,
            RegG_562              = 0x21C4,
            RegG_563              = 0x21C8,
            RegG_564              = 0x21CC,
            RegG_565              = 0x21DC,
            RegG_566              = 0x21E0,
            RegG_567              = 0x21E4,
            RegG_568              = 0x21E8,
            RegG_569              = 0x21EC,
            RegG_570              = 0x21F0,
            RegG_571              = 0x21F4,
            RegG_572              = 0x21F8,
            RegG_573              = 0x21FC,
            RegG_574              = 0x2200,
            RegG_575              = 0x2204,
            RegG_576              = 0x2208,
            RegG_577              = 0x220C,
            RegG_578              = 0x2210,
            RegG_579              = 0x2214,
            RegG_580              = 0x2218,
            RegG_581              = 0x221C,
            RegG_582              = 0x2220,
            RegG_583              = 0x2224,
            RegG_584              = 0x2228,
            RegG_585              = 0x222C,
            RegG_586              = 0x2230,
            RegG_587              = 0x2234,
            RegG_588              = 0x2238,
            RegG_589              = 0x223C,
            RegG_590              = 0x2240,
            RegG_591              = 0x2244,
            RegG_592              = 0x2248,
            RegG_593              = 0x224C,
            RegG_594              = 0x2250,
            RegG_595              = 0x2254,
            RegG_596              = 0x2258,
            RegG_597              = 0x225C,
            RegG_598              = 0x2260,
            RegG_599              = 0x2264,
            RegG_600              = 0x2268,
            RegG_601              = 0x2274,
            RegG_602              = 0x2278,
            RegG_603              = 0x227C,
            RegG_604              = 0x2280,
            RegG_605              = 0x2284,
            RegG_606              = 0x2288,
            RegG_607              = 0x228C,
            RegG_608              = 0x2290,
            RegG_609              = 0x2294,
            RegG_610              = 0x2298,
            RegG_611              = 0x22A0,
            RegG_612              = 0x22A4,
            RegG_613              = 0x22A8,
            RegG_614              = 0x22AC,
            RegG_615              = 0x22B0,
            RegG_616              = 0x22B4,
            RegG_617              = 0x22B8,
            RegG_618              = 0x22BC,
            RegG_619              = 0x22C0,
            RegG_620              = 0x22C4,
            RegG_621              = 0x22C8,
            RegG_622              = 0x22CC,
            RegG_623              = 0x22D0,
            RegG_624              = 0x22D4,
            RegG_625              = 0x22D8,
            RegG_626              = 0x22DC,
            RegG_627              = 0x22E0,
            RegG_628              = 0x22E4,
            RegG_629              = 0x22E8,
            RegG_630              = 0x22EC,
            RegG_631              = 0x22F0,
            RegG_632              = 0x22F4,
            RegG_633              = 0x22F8,
            RegG_634              = 0x22FC,
            RegG_635              = 0x2300,
            RegG_636              = 0x2304,
            RegG_637              = 0x2308,
            RegG_638              = 0x230C,
            RegG_639              = 0x2310,
            RegG_640              = 0x2314,
            RegG_641              = 0x2318,
            RegG_642              = 0x231C,
            RegG_643              = 0x2320,
            RegG_644              = 0x2324,
            RegG_645              = 0x2328,
            RegG_646              = 0x232C,
            RegG_647              = 0x2330,
            RegG_648              = 0x2334,
            RegG_649              = 0x2338,
            RegG_650              = 0x233C,
            RegG_651              = 0x2340,
            RegG_652              = 0x2344,
            RegG_653              = 0x2348,
            RegG_654              = 0x234C,
            RegG_655              = 0x2350,
            RegG_656              = 0x2354,
            RegG_657              = 0x2358,
            RegG_658              = 0x235C,
            RegG_659              = 0x2360,
            RegG_660              = 0x2364,
            RegG_661              = 0x2368,
            RegG_662              = 0x236C,
            RegG_663              = 0x2370,
            RegG_664              = 0x2374,
            RegG_665              = 0x23C0,
            RegG_666              = 0x23C4,
            RegG_667              = 0x23C8,
            RegG_668              = 0x23CC,
            RegG_669              = 0x23D0,
            RegG_670              = 0x23D4,
            RegG_671              = 0x23D8,
            RegG_672              = 0x23DC,
            RegG_673              = 0x23E0,
            RegG_674              = 0x23E4,
            RegG_675              = 0x23E8,
            RegG_676              = 0x23EC,
            RegG_677              = 0x23F0,
            RegG_678              = 0x23F4,
            RegG_679              = 0x2414,
            RegG_680              = 0x2418,
            RegG_681              = 0x241C,
            RegG_682              = 0x2420,
            RegG_683              = 0x2424,
            RegG_684              = 0x2428,
            RegG_685              = 0x2444,
            RegG_686              = 0x2470,
            RegG_687              = 0x2480,
            RegG_688              = 0x2484,
            RegG_689              = 0x2488,
            RegG_690              = 0x248C,
            RegG_691              = 0x2490,
            RegG_692              = 0x24A0,
            RegG_693              = 0x24A4,
            RegG_694              = 0x24A8,
            RegG_695              = 0x24AC,
            RegG_696              = 0x2500,
            RegG_697              = 0x2504,
            RegG_698              = 0x2508,
            RegG_699              = 0x250C,
            RegG_700              = 0x2510,
            RegG_701              = 0x2514,
            RegG_702              = 0x2518,
            RegG_703              = 0x251C,
            RegG_704              = 0x2520,
            RegG_705              = 0x2528,
            RegG_706              = 0x252C,
            RegG_707              = 0x2540,
            RegG_708              = 0x2544,
            RegG_709              = 0x2580,
            RegG_710              = 0x2584,
            RegG_711              = 0x2588,
            RegG_712              = 0x3000,
            RegG_713              = 0x3004,
            RegG_714              = 0x3008,
            RegG_715              = 0x300C,
            RegG_716              = 0x3010,
            RegG_717              = 0x3014,
            RegG_718              = 0x3018,
            RegG_719              = 0x301C,
            RegG_720              = 0x3020,
            RegG_721              = 0x3024,
            RegG_722              = 0x3028,
            RegG_723              = 0x302C,
            RegG_724              = 0x3030,
            RegG_725              = 0x3034,
            RegG_726              = 0x3038,
            RegG_727              = 0x303C,
            RegG_728              = 0x3040,
            RegG_729              = 0x3044,
            RegG_730              = 0x3048,
            RegG_731              = 0x304C,
            RegG_732              = 0x3050,
            RegG_733              = 0x3054,
            RegG_734              = 0x3058,
            RegG_735              = 0x305C,
            RegG_736              = 0x3060,
            RegG_737              = 0x3064,
            RegG_738              = 0x3068,
            RegG_739              = 0x306C,
            RegG_740              = 0x3070,
            RegG_741              = 0x3074,
            RegG_742              = 0x3078,
            RegG_743              = 0x307C,
            RegG_744              = 0x3080,
            RegG_745              = 0x3084,
            RegG_746              = 0x3088,
            RegG_747              = 0x308C,
            RegG_748              = 0x3090,
            RegG_749              = 0x3094,
            RegG_750              = 0x3098,
            RegG_751              = 0x309C,
            RegG_752              = 0x30A0,
            RegG_753              = 0x30A4,
            RegG_754              = 0x30A8,
            RegG_755              = 0x30AC,
            RegG_756              = 0x30B0,
            RegG_757              = 0x30B4,
            RegG_758              = 0x30B8,
            RegG_759              = 0x30BC,
            RegG_760              = 0x30C0,
            RegG_761              = 0x30C4,
            RegG_762              = 0x30C8,
            RegG_763              = 0x30CC,
            RegG_764              = 0x30D0,
            RegG_765              = 0x30D4,
            RegG_766              = 0x30D8,
            RegG_767              = 0x30E0,
            RegG_768              = 0x30E4,
            RegG_769              = 0x3118,
            RegG_770              = 0x311C,
            RegG_771              = 0x3120,
            RegG_772              = 0x3124,
            RegG_773              = 0x3128,
            RegG_774              = 0x313C,
            RegG_775              = 0x3154,
            RegG_776              = 0x3158,
            RegG_777              = 0x315C,
            RegG_778              = 0x3160,
            RegG_779              = 0x3164,
            RegG_780              = 0x3168,
            RegG_781              = 0x316C,
            RegG_782              = 0x3170,
            RegG_783              = 0x3174,
            RegG_784              = 0x3178,
            RegG_785              = 0x317C,
            RegG_786              = 0x3180,
            RegG_787              = 0x3184,
            RegG_788              = 0x3188,
            RegG_789              = 0x318C,
            RegG_790              = 0x3190,
            RegG_791              = 0x3194,
            RegG_792              = 0x3198,
            RegG_793              = 0x31A4,
            RegG_794              = 0x31A8,
            RegG_795              = 0x31AC,
            RegG_796              = 0x31B8,
            RegG_797              = 0x31BC,
            RegG_798              = 0x31C0,
            RegG_799              = 0x31C4,
            RegG_800              = 0x31C8,
            RegG_801              = 0x31CC,
            RegG_802              = 0x31DC,
            RegG_803              = 0x31E0,
            RegG_804              = 0x31E4,
            RegG_805              = 0x31E8,
            RegG_806              = 0x31EC,
            RegG_807              = 0x31F0,
            RegG_808              = 0x31F4,
            RegG_809              = 0x31F8,
            RegG_810              = 0x31FC,
            RegG_811              = 0x3200,
            RegG_812              = 0x3204,
            RegG_813              = 0x3208,
            RegG_814              = 0x320C,
            RegG_815              = 0x3210,
            RegG_816              = 0x3214,
            RegG_817              = 0x3218,
            RegG_818              = 0x321C,
            RegG_819              = 0x3220,
            RegG_820              = 0x3224,
            RegG_821              = 0x3228,
            RegG_822              = 0x322C,
            RegG_823              = 0x3230,
            RegG_824              = 0x3234,
            RegG_825              = 0x3238,
            RegG_826              = 0x323C,
            RegG_827              = 0x3240,
            RegG_828              = 0x3244,
            RegG_829              = 0x3248,
            RegG_830              = 0x324C,
            RegG_831              = 0x3250,
            RegG_832              = 0x3254,
            RegG_833              = 0x3258,
            RegG_834              = 0x325C,
            RegG_835              = 0x3260,
            RegG_836              = 0x3264,
            RegG_837              = 0x3268,
            RegG_838              = 0x3274,
            RegG_839              = 0x3278,
            RegG_840              = 0x327C,
            RegG_841              = 0x3280,
            RegG_842              = 0x3284,
            RegG_843              = 0x3288,
            RegG_844              = 0x328C,
            RegG_845              = 0x3290,
            RegG_846              = 0x3294,
            RegG_847              = 0x3298,
            RegG_848              = 0x32A0,
            RegG_849              = 0x32A4,
            RegG_850              = 0x32A8,
            RegG_851              = 0x32AC,
            RegG_852              = 0x32B0,
            RegG_853              = 0x32B4,
            RegG_854              = 0x32B8,
            RegG_855              = 0x32BC,
            RegG_856              = 0x32C0,
            RegG_857              = 0x32C4,
            RegG_858              = 0x32C8,
            RegG_859              = 0x32CC,
            RegG_860              = 0x32D0,
            RegG_861              = 0x32D4,
            RegG_862              = 0x32D8,
            RegG_863              = 0x32DC,
            RegG_864              = 0x32E0,
            RegG_865              = 0x32E4,
            RegG_866              = 0x32E8,
            RegG_867              = 0x32EC,
            RegG_868              = 0x32F0,
            RegG_869              = 0x32F4,
            RegG_870              = 0x32F8,
            RegG_871              = 0x32FC,
            RegG_872              = 0x3300,
            RegG_873              = 0x3304,
            RegG_874              = 0x3308,
            RegG_875              = 0x330C,
            RegG_876              = 0x3310,
            RegG_877              = 0x3314,
            RegG_878              = 0x3318,
            RegG_879              = 0x331C,
            RegG_880              = 0x3320,
            RegG_881              = 0x3324,
            RegG_882              = 0x3328,
            RegG_883              = 0x332C,
            RegG_884              = 0x3330,
            RegG_885              = 0x3334,
            RegG_886              = 0x3338,
            RegG_887              = 0x333C,
            RegG_888              = 0x3340,
            RegG_889              = 0x3344,
            RegG_890              = 0x3348,
            RegG_891              = 0x334C,
            RegG_892              = 0x3350,
            RegG_893              = 0x3354,
            RegG_894              = 0x3358,
            RegG_895              = 0x335C,
            RegG_896              = 0x3360,
            RegG_897              = 0x3364,
            RegG_898              = 0x3368,
            RegG_899              = 0x336C,
            RegG_900              = 0x3370,
            RegG_901              = 0x3374,
            RegG_902              = 0x33C0,
            RegG_903              = 0x33C4,
            RegG_904              = 0x33C8,
            RegG_905              = 0x33CC,
            RegG_906              = 0x33D0,
            RegG_907              = 0x33D4,
            RegG_908              = 0x33D8,
            RegG_909              = 0x33DC,
            RegG_910              = 0x33E0,
            RegG_911              = 0x33E4,
            RegG_912              = 0x33E8,
            RegG_913              = 0x33EC,
            RegG_914              = 0x33F0,
            RegG_915              = 0x33F4,
            RegG_916              = 0x3414,
            RegG_917              = 0x3418,
            RegG_918              = 0x341C,
            RegG_919              = 0x3420,
            RegG_920              = 0x3424,
            RegG_921              = 0x3428,
            RegG_922              = 0x3444,
            RegG_923              = 0x3470,
            RegG_924              = 0x3480,
            RegG_925              = 0x3484,
            RegG_926              = 0x3488,
            RegG_927              = 0x348C,
            RegG_928              = 0x3490,
            RegG_929              = 0x34A0,
            RegG_930              = 0x34A4,
            RegG_931              = 0x34A8,
            RegG_932              = 0x34AC,
            RegG_933              = 0x3500,
            RegG_934              = 0x3504,
            RegG_935              = 0x3508,
            RegG_936              = 0x350C,
            RegG_937              = 0x3510,
            RegG_938              = 0x3514,
            RegG_939              = 0x3518,
            RegG_940              = 0x351C,
            RegG_941              = 0x3520,
            RegG_942              = 0x3528,
            RegG_943              = 0x352C,
            RegG_944              = 0x3540,
            RegG_945              = 0x3544,
            RegG_946              = 0x3580,
            RegG_947              = 0x3584,
            RegG_948              = 0x3588,
        }

        private enum Registers_A : long
        {
            RegA_1                = 0x0000,
            RegA_2                = 0x0004,
            RegA_3                = 0x0008,
            RegA_4                = 0x000C,
            RegA_5                = 0x0010,
            RegA_6                = 0x0018,
            RegA_7                = 0x001C,
            RegA_8                = 0x0020,
            RegA_9                = 0x0024,
            RegA_10               = 0x0028,
            RegA_11               = 0x002C,
            RegA_12               = 0x0030,
            RegA_13               = 0x0034,
            RegA_14               = 0x0038,
            RegA_15               = 0x003C,
            RegA_16               = 0x0040,
            RegA_17               = 0x0044,
            RegA_18               = 0x0048,
            RegA_19               = 0x004C,
            RegA_20               = 0x0050,
            RegA_21               = 0x0054,
            RegA_22               = 0x0058,
            RegA_23               = 0x005C,
            RegA_24               = 0x0060,
            RegA_25               = 0x0064,
            RegA_26               = 0x0068,
            RegA_27               = 0x006C,
            RegA_28               = 0x0070,
            RegA_29               = 0x0074,
            RegA_30               = 0x0078,
            RegA_31               = 0x007C,
            RegA_32               = 0x0080,
            RegA_33               = 0x0084,
            RegA_34               = 0x0088,
            RegA_35               = 0x008C,
            RegA_36               = 0x0090,
            RegA_37               = 0x00A4,
            RegA_38               = 0x00A8,
            RegA_39               = 0x00AC,
            RegA_40               = 0x00B0,
            RegA_41               = 0x00B4,
            RegA_42               = 0x00B8,
            RegA_43               = 0x00BC,
            RegA_44               = 0x00C0,
            RegA_45               = 0x00C4,
            RegA_46               = 0x00C8,
            RegA_47               = 0x00D0,
            RegA_48               = 0x00D4,
            RegA_49               = 0x00D8,
            RegA_50               = 0x00DC,
            RegA_51               = 0x00E0,
            RegA_52               = 0x00E4,
            RegA_53               = 0x00E8,
            RegA_54               = 0x00EC,
            RegA_55               = 0x00F0,
            RegA_56               = 0x00F4,
            RegA_57               = 0x00F8,
            RegA_58               = 0x00FC,
            RegA_59               = 0x0100,
            RegA_60               = 0x0104,
            RegA_61               = 0x0108,
            RegA_62               = 0x010C,
            RegA_63               = 0x0110,
            RegA_64               = 0x0114,
            RegA_65               = 0x0118,
            RegA_66               = 0x011C,
            RegA_67               = 0x0120,
            RegA_68               = 0x0124,
            RegA_69               = 0x0128,
            RegA_70               = 0x012C,
            RegA_71               = 0x0130,
            RegA_72               = 0x0134,
            RegA_73               = 0x0138,
            RegA_74               = 0x013C,
            RegA_75               = 0x0140,
            RegA_76               = 0x0144,
            RegA_77               = 0x0160,
            RegA_78               = 0x0164,
            RegA_79               = 0x0168,
            RegA_80               = 0x0180,
            RegA_81               = 0x0184,
            RegA_82               = 0x1000,
            RegA_83               = 0x1004,
            RegA_84               = 0x1008,
            RegA_85               = 0x100C,
            RegA_86               = 0x1010,
            RegA_87               = 0x1018,
            RegA_88               = 0x101C,
            RegA_89               = 0x1020,
            RegA_90               = 0x1024,
            RegA_91               = 0x1028,
            RegA_92               = 0x102C,
            RegA_93               = 0x1030,
            RegA_94               = 0x1034,
            RegA_95               = 0x1038,
            RegA_96               = 0x103C,
            RegA_97               = 0x1040,
            RegA_98               = 0x1044,
            RegA_99               = 0x1048,
            RegA_100              = 0x104C,
            RegA_101              = 0x1050,
            RegA_102              = 0x1054,
            RegA_103              = 0x1058,
            RegA_104              = 0x105C,
            RegA_105              = 0x1060,
            RegA_106              = 0x1064,
            RegA_107              = 0x1068,
            RegA_108              = 0x106C,
            RegA_109              = 0x1070,
            RegA_110              = 0x1074,
            RegA_111              = 0x1078,
            RegA_112              = 0x107C,
            RegA_113              = 0x1080,
            RegA_114              = 0x1084,
            RegA_115              = 0x1088,
            RegA_116              = 0x108C,
            RegA_117              = 0x1090,
            RegA_118              = 0x10A4,
            RegA_119              = 0x10A8,
            RegA_120              = 0x10AC,
            RegA_121              = 0x10B0,
            RegA_122              = 0x10B4,
            RegA_123              = 0x10B8,
            RegA_124              = 0x10BC,
            RegA_125              = 0x10C0,
            RegA_126              = 0x10C4,
            RegA_127              = 0x10C8,
            RegA_128              = 0x10D0,
            RegA_129              = 0x10D4,
            RegA_130              = 0x10D8,
            RegA_131              = 0x10DC,
            RegA_132              = 0x10E0,
            RegA_133              = 0x10E4,
            RegA_134              = 0x10E8,
            RegA_135              = 0x10EC,
            RegA_136              = 0x10F0,
            RegA_137              = 0x10F4,
            RegA_138              = 0x10F8,
            RegA_139              = 0x10FC,
            RegA_140              = 0x1100,
            RegA_141              = 0x1104,
            RegA_142              = 0x1108,
            RegA_143              = 0x110C,
            RegA_144              = 0x1110,
            RegA_145              = 0x1114,
            RegA_146              = 0x1118,
            RegA_147              = 0x111C,
            RegA_148              = 0x1120,
            RegA_149              = 0x1124,
            RegA_150              = 0x1128,
            RegA_151              = 0x112C,
            RegA_152              = 0x1130,
            RegA_153              = 0x1134,
            RegA_154              = 0x1138,
            RegA_155              = 0x113C,
            RegA_156              = 0x1140,
            RegA_157              = 0x1144,
            RegA_158              = 0x1160,
            RegA_159              = 0x1164,
            RegA_160              = 0x1168,
            RegA_161              = 0x1180,
            RegA_162              = 0x1184,
            RegA_163              = 0x2000,
            RegA_164              = 0x2004,
            RegA_165              = 0x2008,
            RegA_166              = 0x200C,
            RegA_167              = 0x2010,
            RegA_168              = 0x2018,
            RegA_169              = 0x201C,
            RegA_170              = 0x2020,
            RegA_171              = 0x2024,
            RegA_172              = 0x2028,
            RegA_173              = 0x202C,
            RegA_174              = 0x2030,
            RegA_175              = 0x2034,
            RegA_176              = 0x2038,
            RegA_177              = 0x203C,
            RegA_178              = 0x2040,
            RegA_179              = 0x2044,
            RegA_180              = 0x2048,
            RegA_181              = 0x204C,
            RegA_182              = 0x2050,
            RegA_183              = 0x2054,
            RegA_184              = 0x2058,
            RegA_185              = 0x205C,
            RegA_186              = 0x2060,
            RegA_187              = 0x2064,
            RegA_188              = 0x2068,
            RegA_189              = 0x206C,
            RegA_190              = 0x2070,
            RegA_191              = 0x2074,
            RegA_192              = 0x2078,
            RegA_193              = 0x207C,
            RegA_194              = 0x2080,
            RegA_195              = 0x2084,
            RegA_196              = 0x2088,
            RegA_197              = 0x208C,
            RegA_198              = 0x2090,
            RegA_199              = 0x20A4,
            RegA_200              = 0x20A8,
            RegA_201              = 0x20AC,
            RegA_202              = 0x20B0,
            RegA_203              = 0x20B4,
            RegA_204              = 0x20B8,
            RegA_205              = 0x20BC,
            RegA_206              = 0x20C0,
            RegA_207              = 0x20C4,
            RegA_208              = 0x20C8,
            RegA_209              = 0x20D0,
            RegA_210              = 0x20D4,
            RegA_211              = 0x20D8,
            RegA_212              = 0x20DC,
            RegA_213              = 0x20E0,
            RegA_214              = 0x20E4,
            RegA_215              = 0x20E8,
            RegA_216              = 0x20EC,
            RegA_217              = 0x20F0,
            RegA_218              = 0x20F4,
            RegA_219              = 0x20F8,
            RegA_220              = 0x20FC,
            RegA_221              = 0x2100,
            RegA_222              = 0x2104,
            RegA_223              = 0x2108,
            RegA_224              = 0x210C,
            RegA_225              = 0x2110,
            RegA_226              = 0x2114,
            RegA_227              = 0x2118,
            RegA_228              = 0x211C,
            RegA_229              = 0x2120,
            RegA_230              = 0x2124,
            RegA_231              = 0x2128,
            RegA_232              = 0x212C,
            RegA_233              = 0x2130,
            RegA_234              = 0x2134,
            RegA_235              = 0x2138,
            RegA_236              = 0x213C,
            RegA_237              = 0x2140,
            RegA_238              = 0x2144,
            RegA_239              = 0x2160,
            RegA_240              = 0x2164,
            RegA_241              = 0x2168,
            RegA_242              = 0x2180,
            RegA_243              = 0x2184,
            RegA_244              = 0x3000,
            RegA_245              = 0x3004,
            RegA_246              = 0x3008,
            RegA_247              = 0x300C,
            RegA_248              = 0x3010,
            RegA_249              = 0x3018,
            RegA_250              = 0x301C,
            RegA_251              = 0x3020,
            RegA_252              = 0x3024,
            RegA_253              = 0x3028,
            RegA_254              = 0x302C,
            RegA_255              = 0x3030,
            RegA_256              = 0x3034,
            RegA_257              = 0x3038,
            RegA_258              = 0x303C,
            RegA_259              = 0x3040,
            RegA_260              = 0x3044,
            RegA_261              = 0x3048,
            RegA_262              = 0x304C,
            RegA_263              = 0x3050,
            RegA_264              = 0x3054,
            RegA_265              = 0x3058,
            RegA_266              = 0x305C,
            RegA_267              = 0x3060,
            RegA_268              = 0x3064,
            RegA_269              = 0x3068,
            RegA_270              = 0x306C,
            RegA_271              = 0x3070,
            RegA_272              = 0x3074,
            RegA_273              = 0x3078,
            RegA_274              = 0x307C,
            RegA_275              = 0x3080,
            RegA_276              = 0x3084,
            RegA_277              = 0x3088,
            RegA_278              = 0x308C,
            RegA_279              = 0x3090,
            RegA_280              = 0x30A4,
            RegA_281              = 0x30A8,
            RegA_282              = 0x30AC,
            RegA_283              = 0x30B0,
            RegA_284              = 0x30B4,
            RegA_285              = 0x30B8,
            RegA_286              = 0x30BC,
            RegA_287              = 0x30C0,
            RegA_288              = 0x30C4,
            RegA_289              = 0x30C8,
            RegA_290              = 0x30D0,
            RegA_291              = 0x30D4,
            RegA_292              = 0x30D8,
            RegA_293              = 0x30DC,
            RegA_294              = 0x30E0,
            RegA_295              = 0x30E4,
            RegA_296              = 0x30E8,
            RegA_297              = 0x30EC,
            RegA_298              = 0x30F0,
            RegA_299              = 0x30F4,
            RegA_300              = 0x30F8,
            RegA_301              = 0x30FC,
            RegA_302              = 0x3100,
            RegA_303              = 0x3104,
            RegA_304              = 0x3108,
            RegA_305              = 0x310C,
            RegA_306              = 0x3110,
            RegA_307              = 0x3114,
            RegA_308              = 0x3118,
            RegA_309              = 0x311C,
            RegA_310              = 0x3120,
            RegA_311              = 0x3124,
            RegA_312              = 0x3128,
            RegA_313              = 0x312C,
            RegA_314              = 0x3130,
            RegA_315              = 0x3134,
            RegA_316              = 0x3138,
            RegA_317              = 0x313C,
            RegA_318              = 0x3140,
            RegA_319              = 0x3144,
            RegA_320              = 0x3160,
            RegA_321              = 0x3164,
            RegA_322              = 0x3168,
            RegA_323              = 0x3180,
            RegA_324              = 0x3184,
        }

        private enum Registers_B : long
        {
            RegB_1                = 0x0000,
            RegB_2                = 0x0004,
            RegB_3                = 0x0008,
            RegB_4                = 0x000C,
            RegB_5                = 0x0010,
            RegB_6                = 0x0014,
            RegB_7                = 0x0018,
            RegB_8                = 0x001C,
            RegB_9                = 0x0020,
            RegB_10               = 0x1000,
            RegB_11               = 0x1004,
            RegB_12               = 0x1008,
            RegB_13               = 0x100C,
            RegB_14               = 0x1010,
            RegB_15               = 0x1014,
            RegB_16               = 0x1018,
            RegB_17               = 0x101C,
            RegB_18               = 0x1020,
            RegB_19               = 0x2000,
            RegB_20               = 0x2004,
            RegB_21               = 0x2008,
            RegB_22               = 0x200C,
            RegB_23               = 0x2010,
            RegB_24               = 0x2014,
            RegB_25               = 0x2018,
            RegB_26               = 0x201C,
            RegB_27               = 0x2020,
            RegB_28               = 0x3000,
            RegB_29               = 0x3004,
            RegB_30               = 0x3008,
            RegB_31               = 0x300C,
            RegB_32               = 0x3010,
            RegB_33               = 0x3014,
            RegB_34               = 0x3018,
            RegB_35               = 0x301C,
            RegB_36               = 0x3020,
        }

        private enum Registers_H : long
        {
            RegH_1                = 0x0000,
            RegH_2                = 0x0004,
            RegH_3                = 0x0008,
            RegH_4                = 0x000C,
            RegH_5                = 0x0010,
            RegH_6                = 0x0014,
            RegH_7                = 0x0018,
            RegH_8                = 0x001C,
            RegH_9                = 0x0020,
            RegH_10               = 0x0024,
            RegH_11               = 0x0028,
            RegH_12               = 0x002C,
            RegH_13               = 0x0030,
            RegH_14               = 0x0034,
            RegH_15               = 0x0038,
            RegH_16               = 0x003C,
            RegH_17               = 0x0040,
            RegH_18               = 0x0044,
            RegH_19               = 0x0048,
            RegH_20               = 0x004C,
            RegH_21               = 0x0050,
            RegH_22               = 0x0054,
            RegH_23               = 0x0058,
            RegH_24               = 0x005C,
            RegH_25               = 0x0060,
            RegH_26               = 0x0064,
            RegH_27               = 0x0068,
            RegH_28               = 0x006C,
            RegH_29               = 0x0070,
            RegH_30               = 0x0074,
            RegH_31               = 0x0078,
            RegH_32               = 0x007C,
            RegH_33               = 0x0080,
            RegH_34               = 0x0084,
            RegH_35               = 0x0088,
            RegH_36               = 0x008C,
            RegH_37               = 0x0090,
            RegH_38               = 0x0094,
            RegH_39               = 0x0098,
            RegH_40               = 0x009C,
            RegH_41               = 0x00A0,
            RegH_42               = 0x00A4,
            RegH_43               = 0x00A8,
            RegH_44               = 0x00AC,
            RegH_45               = 0x00B0,
            RegH_46               = 0x00B4,
            RegH_47               = 0x00B8,
            RegH_48               = 0x00BC,
            RegH_49               = 0x00C0,
            RegH_50               = 0x00C4,
            RegH_51               = 0x00E0,
            RegH_52               = 0x00E4,
            RegH_53               = 0x00E8,
            RegH_54               = 0x00EC,
            RegH_55               = 0x0100,
            RegH_56               = 0x0104,
            RegH_57               = 0x0108,
            RegH_58               = 0x010C,
            RegH_59               = 0x0110,
            RegH_60               = 0x0114,
            RegH_61               = 0x0118,
            RegH_62               = 0x011C,
            RegH_63               = 0x0120,
            RegH_64               = 0x0124,
            RegH_65               = 0x0128,
            RegH_66               = 0x012C,
            RegH_67               = 0x0130,
            RegH_68               = 0x0134,
            RegH_69               = 0x0138,
            RegH_70               = 0x013C,
            RegH_71               = 0x0140,
            RegH_72               = 0x0144,
            RegH_73               = 0x0148,
            RegH_74               = 0x014C,
            RegH_75               = 0x0150,
            RegH_76               = 0x0154,
            RegH_77               = 0x0158,
            RegH_78               = 0x015C,
            RegH_79               = 0x0160,
            RegH_80               = 0x0164,
            RegH_81               = 0x0168,
            RegH_82               = 0x016C,
            RegH_83               = 0x0170,
            RegH_84               = 0x0174,
            RegH_85               = 0x0178,
            RegH_86               = 0x017C,
            RegH_87               = 0x0180,
            RegH_88               = 0x0184,
            RegH_89               = 0x0188,
            RegH_90               = 0x018C,
            RegH_91               = 0x0190,
            RegH_92               = 0x0194,
            RegH_93               = 0x0198,
            RegH_94               = 0x019C,
            RegH_95               = 0x01A0,
            RegH_96               = 0x01A4,
            RegH_97               = 0x01A8,
            RegH_98               = 0x01AC,
            RegH_99               = 0x01B0,
            RegH_100              = 0x01B4,
            RegH_101              = 0x01B8,
            RegH_102              = 0x01BC,
            RegH_103              = 0x01C0,
            RegH_104              = 0x01C4,
            RegH_105              = 0x01C8,
            RegH_106              = 0x01CC,
            RegH_107              = 0x01D0,
            RegH_108              = 0x01D4,
            RegH_109              = 0x01D8,
            RegH_110              = 0x01DC,
            RegH_111              = 0x01E0,
            RegH_112              = 0x01E4,
            RegH_113              = 0x01E8,
            RegH_114              = 0x01EC,
            RegH_115              = 0x01F0,
            RegH_116              = 0x01F4,
            RegH_117              = 0x01F8,
            RegH_118              = 0x01FC,
            RegH_119              = 0x0200,
            RegH_120              = 0x0204,
            RegH_121              = 0x0208,
            RegH_122              = 0x020C,
            RegH_123              = 0x0210,
            RegH_124              = 0x0214,
            RegH_125              = 0x0218,
            RegH_126              = 0x021C,
            RegH_127              = 0x0220,
            RegH_128              = 0x0224,
            RegH_129              = 0x0228,
            RegH_130              = 0x022C,
            RegH_131              = 0x0230,
            RegH_132              = 0x0234,
            RegH_133              = 0x0238,
            RegH_134              = 0x023C,
            RegH_135              = 0x0240,
            RegH_136              = 0x0244,
            RegH_137              = 0x0248,
            RegH_138              = 0x024C,
            RegH_139              = 0x0250,
            RegH_140              = 0x0254,
            RegH_141              = 0x0258,
            RegH_142              = 0x025C,
            RegH_143              = 0x0260,
            RegH_144              = 0x0264,
            RegH_145              = 0x0268,
            RegH_146              = 0x026C,
            RegH_147              = 0x0270,
            RegH_148              = 0x0274,
            RegH_149              = 0x0278,
            RegH_150              = 0x027C,
            RegH_151              = 0x1000,
            RegH_152              = 0x1004,
            RegH_153              = 0x1008,
            RegH_154              = 0x100C,
            RegH_155              = 0x1010,
            RegH_156              = 0x1014,
            RegH_157              = 0x1018,
            RegH_158              = 0x101C,
            RegH_159              = 0x1020,
            RegH_160              = 0x1024,
            RegH_161              = 0x1028,
            RegH_162              = 0x102C,
            RegH_163              = 0x1030,
            RegH_164              = 0x1034,
            RegH_165              = 0x1038,
            RegH_166              = 0x103C,
            RegH_167              = 0x1040,
            RegH_168              = 0x1044,
            RegH_169              = 0x1048,
            RegH_170              = 0x104C,
            RegH_171              = 0x1050,
            RegH_172              = 0x1054,
            RegH_173              = 0x1058,
            RegH_174              = 0x105C,
            RegH_175              = 0x1060,
            RegH_176              = 0x1064,
            RegH_177              = 0x1068,
            RegH_178              = 0x106C,
            RegH_179              = 0x1070,
            RegH_180              = 0x1074,
            RegH_181              = 0x1078,
            RegH_182              = 0x107C,
            RegH_183              = 0x1080,
            RegH_184              = 0x1084,
            RegH_185              = 0x1088,
            RegH_186              = 0x108C,
            RegH_187              = 0x1090,
            RegH_188              = 0x1094,
            RegH_189              = 0x1098,
            RegH_190              = 0x109C,
            RegH_191              = 0x10A0,
            RegH_192              = 0x10A4,
            RegH_193              = 0x10A8,
            RegH_194              = 0x10AC,
            RegH_195              = 0x10B0,
            RegH_196              = 0x10B4,
            RegH_197              = 0x10B8,
            RegH_198              = 0x10BC,
            RegH_199              = 0x10C0,
            RegH_200              = 0x10C4,
            RegH_201              = 0x10E0,
            RegH_202              = 0x10E4,
            RegH_203              = 0x10E8,
            RegH_204              = 0x10EC,
            RegH_205              = 0x1100,
            RegH_206              = 0x1104,
            RegH_207              = 0x1108,
            RegH_208              = 0x110C,
            RegH_209              = 0x1110,
            RegH_210              = 0x1114,
            RegH_211              = 0x1118,
            RegH_212              = 0x111C,
            RegH_213              = 0x1120,
            RegH_214              = 0x1124,
            RegH_215              = 0x1128,
            RegH_216              = 0x112C,
            RegH_217              = 0x1130,
            RegH_218              = 0x1134,
            RegH_219              = 0x1138,
            RegH_220              = 0x113C,
            RegH_221              = 0x1140,
            RegH_222              = 0x1144,
            RegH_223              = 0x1148,
            RegH_224              = 0x114C,
            RegH_225              = 0x1150,
            RegH_226              = 0x1154,
            RegH_227              = 0x1158,
            RegH_228              = 0x115C,
            RegH_229              = 0x1160,
            RegH_230              = 0x1164,
            RegH_231              = 0x1168,
            RegH_232              = 0x116C,
            RegH_233              = 0x1170,
            RegH_234              = 0x1174,
            RegH_235              = 0x1178,
            RegH_236              = 0x117C,
            RegH_237              = 0x1180,
            RegH_238              = 0x1184,
            RegH_239              = 0x1188,
            RegH_240              = 0x118C,
            RegH_241              = 0x1190,
            RegH_242              = 0x1194,
            RegH_243              = 0x1198,
            RegH_244              = 0x119C,
            RegH_245              = 0x11A0,
            RegH_246              = 0x11A4,
            RegH_247              = 0x11A8,
            RegH_248              = 0x11AC,
            RegH_249              = 0x11B0,
            RegH_250              = 0x11B4,
            RegH_251              = 0x11B8,
            RegH_252              = 0x11BC,
            RegH_253              = 0x11C0,
            RegH_254              = 0x11C4,
            RegH_255              = 0x11C8,
            RegH_256              = 0x11CC,
            RegH_257              = 0x11D0,
            RegH_258              = 0x11D4,
            RegH_259              = 0x11D8,
            RegH_260              = 0x11DC,
            RegH_261              = 0x11E0,
            RegH_262              = 0x11E4,
            RegH_263              = 0x11E8,
            RegH_264              = 0x11EC,
            RegH_265              = 0x11F0,
            RegH_266              = 0x11F4,
            RegH_267              = 0x11F8,
            RegH_268              = 0x11FC,
            RegH_269              = 0x1200,
            RegH_270              = 0x1204,
            RegH_271              = 0x1208,
            RegH_272              = 0x120C,
            RegH_273              = 0x1210,
            RegH_274              = 0x1214,
            RegH_275              = 0x1218,
            RegH_276              = 0x121C,
            RegH_277              = 0x1220,
            RegH_278              = 0x1224,
            RegH_279              = 0x1228,
            RegH_280              = 0x122C,
            RegH_281              = 0x1230,
            RegH_282              = 0x1234,
            RegH_283              = 0x1238,
            RegH_284              = 0x123C,
            RegH_285              = 0x1240,
            RegH_286              = 0x1244,
            RegH_287              = 0x1248,
            RegH_288              = 0x124C,
            RegH_289              = 0x1250,
            RegH_290              = 0x1254,
            RegH_291              = 0x1258,
            RegH_292              = 0x125C,
            RegH_293              = 0x1260,
            RegH_294              = 0x1264,
            RegH_295              = 0x1268,
            RegH_296              = 0x126C,
            RegH_297              = 0x1270,
            RegH_298              = 0x1274,
            RegH_299              = 0x1278,
            RegH_300              = 0x127C,
            RegH_301              = 0x2000,
            RegH_302              = 0x2004,
            RegH_303              = 0x2008,
            RegH_304              = 0x200C,
            RegH_305              = 0x2010,
            RegH_306              = 0x2014,
            RegH_307              = 0x2018,
            RegH_308              = 0x201C,
            RegH_309              = 0x2020,
            RegH_310              = 0x2024,
            RegH_311              = 0x2028,
            RegH_312              = 0x202C,
            RegH_313              = 0x2030,
            RegH_314              = 0x2034,
            RegH_315              = 0x2038,
            RegH_316              = 0x203C,
            RegH_317              = 0x2040,
            RegH_318              = 0x2044,
            RegH_319              = 0x2048,
            RegH_320              = 0x204C,
            RegH_321              = 0x2050,
            RegH_322              = 0x2054,
            RegH_323              = 0x2058,
            RegH_324              = 0x205C,
            RegH_325              = 0x2060,
            RegH_326              = 0x2064,
            RegH_327              = 0x2068,
            RegH_328              = 0x206C,
            RegH_329              = 0x2070,
            RegH_330              = 0x2074,
            RegH_331              = 0x2078,
            RegH_332              = 0x207C,
            RegH_333              = 0x2080,
            RegH_334              = 0x2084,
            RegH_335              = 0x2088,
            RegH_336              = 0x208C,
            RegH_337              = 0x2090,
            RegH_338              = 0x2094,
            RegH_339              = 0x2098,
            RegH_340              = 0x209C,
            RegH_341              = 0x20A0,
            RegH_342              = 0x20A4,
            RegH_343              = 0x20A8,
            RegH_344              = 0x20AC,
            RegH_345              = 0x20B0,
            RegH_346              = 0x20B4,
            RegH_347              = 0x20B8,
            RegH_348              = 0x20BC,
            RegH_349              = 0x20C0,
            RegH_350              = 0x20C4,
            RegH_351              = 0x20E0,
            RegH_352              = 0x20E4,
            RegH_353              = 0x20E8,
            RegH_354              = 0x20EC,
            RegH_355              = 0x2100,
            RegH_356              = 0x2104,
            RegH_357              = 0x2108,
            RegH_358              = 0x210C,
            RegH_359              = 0x2110,
            RegH_360              = 0x2114,
            RegH_361              = 0x2118,
            RegH_362              = 0x211C,
            RegH_363              = 0x2120,
            RegH_364              = 0x2124,
            RegH_365              = 0x2128,
            RegH_366              = 0x212C,
            RegH_367              = 0x2130,
            RegH_368              = 0x2134,
            RegH_369              = 0x2138,
            RegH_370              = 0x213C,
            RegH_371              = 0x2140,
            RegH_372              = 0x2144,
            RegH_373              = 0x2148,
            RegH_374              = 0x214C,
            RegH_375              = 0x2150,
            RegH_376              = 0x2154,
            RegH_377              = 0x2158,
            RegH_378              = 0x215C,
            RegH_379              = 0x2160,
            RegH_380              = 0x2164,
            RegH_381              = 0x2168,
            RegH_382              = 0x216C,
            RegH_383              = 0x2170,
            RegH_384              = 0x2174,
            RegH_385              = 0x2178,
            RegH_386              = 0x217C,
            RegH_387              = 0x2180,
            RegH_388              = 0x2184,
            RegH_389              = 0x2188,
            RegH_390              = 0x218C,
            RegH_391              = 0x2190,
            RegH_392              = 0x2194,
            RegH_393              = 0x2198,
            RegH_394              = 0x219C,
            RegH_395              = 0x21A0,
            RegH_396              = 0x21A4,
            RegH_397              = 0x21A8,
            RegH_398              = 0x21AC,
            RegH_399              = 0x21B0,
            RegH_400              = 0x21B4,
            RegH_401              = 0x21B8,
            RegH_402              = 0x21BC,
            RegH_403              = 0x21C0,
            RegH_404              = 0x21C4,
            RegH_405              = 0x21C8,
            RegH_406              = 0x21CC,
            RegH_407              = 0x21D0,
            RegH_408              = 0x21D4,
            RegH_409              = 0x21D8,
            RegH_410              = 0x21DC,
            RegH_411              = 0x21E0,
            RegH_412              = 0x21E4,
            RegH_413              = 0x21E8,
            RegH_414              = 0x21EC,
            RegH_415              = 0x21F0,
            RegH_416              = 0x21F4,
            RegH_417              = 0x21F8,
            RegH_418              = 0x21FC,
            RegH_419              = 0x2200,
            RegH_420              = 0x2204,
            RegH_421              = 0x2208,
            RegH_422              = 0x220C,
            RegH_423              = 0x2210,
            RegH_424              = 0x2214,
            RegH_425              = 0x2218,
            RegH_426              = 0x221C,
            RegH_427              = 0x2220,
            RegH_428              = 0x2224,
            RegH_429              = 0x2228,
            RegH_430              = 0x222C,
            RegH_431              = 0x2230,
            RegH_432              = 0x2234,
            RegH_433              = 0x2238,
            RegH_434              = 0x223C,
            RegH_435              = 0x2240,
            RegH_436              = 0x2244,
            RegH_437              = 0x2248,
            RegH_438              = 0x224C,
            RegH_439              = 0x2250,
            RegH_440              = 0x2254,
            RegH_441              = 0x2258,
            RegH_442              = 0x225C,
            RegH_443              = 0x2260,
            RegH_444              = 0x2264,
            RegH_445              = 0x2268,
            RegH_446              = 0x226C,
            RegH_447              = 0x2270,
            RegH_448              = 0x2274,
            RegH_449              = 0x2278,
            RegH_450              = 0x227C,
            RegH_451              = 0x3000,
            RegH_452              = 0x3004,
            RegH_453              = 0x3008,
            RegH_454              = 0x300C,
            RegH_455              = 0x3010,
            RegH_456              = 0x3014,
            RegH_457              = 0x3018,
            RegH_458              = 0x301C,
            RegH_459              = 0x3020,
            RegH_460              = 0x3024,
            RegH_461              = 0x3028,
            RegH_462              = 0x302C,
            RegH_463              = 0x3030,
            RegH_464              = 0x3034,
            RegH_465              = 0x3038,
            RegH_466              = 0x303C,
            RegH_467              = 0x3040,
            RegH_468              = 0x3044,
            RegH_469              = 0x3048,
            RegH_470              = 0x304C,
            RegH_471              = 0x3050,
            RegH_472              = 0x3054,
            RegH_473              = 0x3058,
            RegH_474              = 0x305C,
            RegH_475              = 0x3060,
            RegH_476              = 0x3064,
            RegH_477              = 0x3068,
            RegH_478              = 0x306C,
            RegH_479              = 0x3070,
            RegH_480              = 0x3074,
            RegH_481              = 0x3078,
            RegH_482              = 0x307C,
            RegH_483              = 0x3080,
            RegH_484              = 0x3084,
            RegH_485              = 0x3088,
            RegH_486              = 0x308C,
            RegH_487              = 0x3090,
            RegH_488              = 0x3094,
            RegH_489              = 0x3098,
            RegH_490              = 0x309C,
            RegH_491              = 0x30A0,
            RegH_492              = 0x30A4,
            RegH_493              = 0x30A8,
            RegH_494              = 0x30AC,
            RegH_495              = 0x30B0,
            RegH_496              = 0x30B4,
            RegH_497              = 0x30B8,
            RegH_498              = 0x30BC,
            RegH_499              = 0x30C0,
            RegH_500              = 0x30C4,
            RegH_501              = 0x30E0,
            RegH_502              = 0x30E4,
            RegH_503              = 0x30E8,
            RegH_504              = 0x30EC,
            RegH_505              = 0x3100,
            RegH_506              = 0x3104,
            RegH_507              = 0x3108,
            RegH_508              = 0x310C,
            RegH_509              = 0x3110,
            RegH_510              = 0x3114,
            RegH_511              = 0x3118,
            RegH_512              = 0x311C,
            RegH_513              = 0x3120,
            RegH_514              = 0x3124,
            RegH_515              = 0x3128,
            RegH_516              = 0x312C,
            RegH_517              = 0x3130,
            RegH_518              = 0x3134,
            RegH_519              = 0x3138,
            RegH_520              = 0x313C,
            RegH_521              = 0x3140,
            RegH_522              = 0x3144,
            RegH_523              = 0x3148,
            RegH_524              = 0x314C,
            RegH_525              = 0x3150,
            RegH_526              = 0x3154,
            RegH_527              = 0x3158,
            RegH_528              = 0x315C,
            RegH_529              = 0x3160,
            RegH_530              = 0x3164,
            RegH_531              = 0x3168,
            RegH_532              = 0x316C,
            RegH_533              = 0x3170,
            RegH_534              = 0x3174,
            RegH_535              = 0x3178,
            RegH_536              = 0x317C,
            RegH_537              = 0x3180,
            RegH_538              = 0x3184,
            RegH_539              = 0x3188,
            RegH_540              = 0x318C,
            RegH_541              = 0x3190,
            RegH_542              = 0x3194,
            RegH_543              = 0x3198,
            RegH_544              = 0x319C,
            RegH_545              = 0x31A0,
            RegH_546              = 0x31A4,
            RegH_547              = 0x31A8,
            RegH_548              = 0x31AC,
            RegH_549              = 0x31B0,
            RegH_550              = 0x31B4,
            RegH_551              = 0x31B8,
            RegH_552              = 0x31BC,
            RegH_553              = 0x31C0,
            RegH_554              = 0x31C4,
            RegH_555              = 0x31C8,
            RegH_556              = 0x31CC,
            RegH_557              = 0x31D0,
            RegH_558              = 0x31D4,
            RegH_559              = 0x31D8,
            RegH_560              = 0x31DC,
            RegH_561              = 0x31E0,
            RegH_562              = 0x31E4,
            RegH_563              = 0x31E8,
            RegH_564              = 0x31EC,
            RegH_565              = 0x31F0,
            RegH_566              = 0x31F4,
            RegH_567              = 0x31F8,
            RegH_568              = 0x31FC,
            RegH_569              = 0x3200,
            RegH_570              = 0x3204,
            RegH_571              = 0x3208,
            RegH_572              = 0x320C,
            RegH_573              = 0x3210,
            RegH_574              = 0x3214,
            RegH_575              = 0x3218,
            RegH_576              = 0x321C,
            RegH_577              = 0x3220,
            RegH_578              = 0x3224,
            RegH_579              = 0x3228,
            RegH_580              = 0x322C,
            RegH_581              = 0x3230,
            RegH_582              = 0x3234,
            RegH_583              = 0x3238,
            RegH_584              = 0x323C,
            RegH_585              = 0x3240,
            RegH_586              = 0x3244,
            RegH_587              = 0x3248,
            RegH_588              = 0x324C,
            RegH_589              = 0x3250,
            RegH_590              = 0x3254,
            RegH_591              = 0x3258,
            RegH_592              = 0x325C,
            RegH_593              = 0x3260,
            RegH_594              = 0x3264,
            RegH_595              = 0x3268,
            RegH_596              = 0x326C,
            RegH_597              = 0x3270,
            RegH_598              = 0x3274,
            RegH_599              = 0x3278,
            RegH_600              = 0x327C,
        }

        private enum Registers_I : long
        {
            RegI_1                = 0x0000,
            RegI_2                = 0x0004,
            RegI_3                = 0x0008,
            RegI_4                = 0x000C,
            RegI_5                = 0x0010,
            RegI_6                = 0x0014,
            RegI_7                = 0x0018,
            RegI_8                = 0x001C,
            RegI_9                = 0x0020,
            RegI_10               = 0x0024,
            RegI_11               = 0x0028,
            RegI_12               = 0x002C,
            RegI_13               = 0x0030,
            RegI_14               = 0x0034,
            RegI_15               = 0x0038,
            RegI_16               = 0x003C,
            RegI_17               = 0x0040,
            RegI_18               = 0x0044,
            RegI_19               = 0x0048,
            RegI_20               = 0x004C,
            RegI_21               = 0x0050,
            RegI_22               = 0x0054,
            RegI_23               = 0x0058,
            RegI_24               = 0x005C,
            RegI_25               = 0x0060,
            RegI_26               = 0x0064,
            RegI_27               = 0x0068,
            RegI_28               = 0x006C,
            RegI_29               = 0x0070,
            RegI_30               = 0x0074,
            RegI_31               = 0x0078,
            RegI_32               = 0x007C,
            RegI_33               = 0x0084,
            RegI_34               = 0x0088,
            RegI_35               = 0x008C,
            RegI_36               = 0x0090,
            RegI_37               = 0x0098,
            RegI_38               = 0x00AC,
            RegI_39               = 0x00B0,
            RegI_40               = 0x00B4,
            RegI_41               = 0x00B8,
            RegI_42               = 0x00BC,
            RegI_43               = 0x00C0,
            RegI_44               = 0x00C4,
            RegI_45               = 0x00CC,
            RegI_46               = 0x00D4,
            RegI_47               = 0x00DC,
            RegI_48               = 0x00E0,
            RegI_49               = 0x00E4,
            RegI_50               = 0x00E8,
            RegI_51               = 0x00FC,
            RegI_52               = 0x0104,
            RegI_53               = 0x0108,
            RegI_54               = 0x0118,
            RegI_55               = 0x0120,
            RegI_56               = 0x0160,
            RegI_57               = 0x0164,
            RegI_58               = 0x0168,
            RegI_59               = 0x016C,
            RegI_60               = 0x0170,
            RegI_61               = 0x0174,
            RegI_62               = 0x0178,
            RegI_63               = 0x017C,
            RegI_64               = 0x0188,
            RegI_65               = 0x018C,
            RegI_66               = 0x0194,
            RegI_67               = 0x0198,
            RegI_68               = 0x019C,
            RegI_69               = 0x01A0,
            RegI_70               = 0x01A4,
            RegI_71               = 0x01A8,
            RegI_72               = 0x01C8,
            RegI_73               = 0x01D8,
            RegI_74               = 0x01E0,
            RegI_75               = 0x01E8,
            RegI_76               = 0x01EC,
            RegI_77               = 0x01F0,
            RegI_78               = 0x01F4,
            RegI_79               = 0x01FC,
            RegI_80               = 0x0200,
            RegI_81               = 0x0204,
            RegI_82               = 0x020C,
            RegI_83               = 0x0210,
            RegI_84               = 0x0214,
            RegI_85               = 0x0218,
            RegI_86               = 0x0224,
            RegI_87               = 0x0228,
            RegI_88               = 0x022C,
            RegI_89               = 0x0230,
            RegI_90               = 0x0234,
            RegI_91               = 0x0238,
            RegI_92               = 0x0244,
            RegI_93               = 0x0248,
            RegI_94               = 0x024C,
            RegI_95               = 0x0250,
            RegI_96               = 0x0254,
            RegI_97               = 0x025C,
            RegI_98               = 0x0260,
            RegI_99               = 0x0264,
            RegI_100              = 0x0268,
            RegI_101              = 0x026C,
            RegI_102              = 0x0270,
            RegI_103              = 0x0274,
            RegI_104              = 0x0278,
            RegI_105              = 0x027C,
            RegI_106              = 0x0280,
            RegI_107              = 0x0284,
            RegI_108              = 0x0288,
            RegI_109              = 0x028C,
            RegI_110              = 0x0290,
            RegI_111              = 0x0294,
            RegI_112              = 0x0298,
            RegI_113              = 0x029C,
            RegI_114              = 0x02A0,
            RegI_115              = 0x02A4,
            RegI_116              = 0x02A8,
            RegI_117              = 0x02AC,
            RegI_118              = 0x02B0,
            RegI_119              = 0x02B4,
            RegI_120              = 0x02B8,
            RegI_121              = 0x02BC,
            RegI_122              = 0x02C8,
            RegI_123              = 0x02CC,
            RegI_124              = 0x02D0,
            RegI_125              = 0x02D4,
            RegI_126              = 0x02D8,
            RegI_127              = 0x02DC,
            RegI_128              = 0x02E0,
            RegI_129              = 0x02E4,
            RegI_130              = 0x02E8,
            RegI_131              = 0x03E0,
            RegI_132              = 0x03E4,
            RegI_133              = 0x03E8,
            RegI_134              = 0x03EC,
            RegI_135              = 0x03F0,
            RegI_136              = 0x03F4,
            RegI_137              = 0x03F8,
            RegI_138              = 0x03FC,
            RegI_139              = 0x0600,
            RegI_140              = 0x07E8,
            RegI_141              = 0x07EC,
            RegI_142              = 0x07F0,
            RegI_143              = 0x07F4,
            RegI_144              = 0x07F8,
            RegI_145              = 0x07FC,
            RegI_146              = 0x1000,
            RegI_147              = 0x1004,
            RegI_148              = 0x1008,
            RegI_149              = 0x100C,
            RegI_150              = 0x1010,
            RegI_151              = 0x1014,
            RegI_152              = 0x1018,
            RegI_153              = 0x101C,
            RegI_154              = 0x1020,
            RegI_155              = 0x1024,
            RegI_156              = 0x1028,
            RegI_157              = 0x102C,
            RegI_158              = 0x1030,
            RegI_159              = 0x1034,
            RegI_160              = 0x1038,
            RegI_161              = 0x103C,
            RegI_162              = 0x1040,
            RegI_163              = 0x1044,
            RegI_164              = 0x1048,
            RegI_165              = 0x104C,
            RegI_166              = 0x1050,
            RegI_167              = 0x1054,
            RegI_168              = 0x1058,
            RegI_169              = 0x105C,
            RegI_170              = 0x1060,
            RegI_171              = 0x1064,
            RegI_172              = 0x1068,
            RegI_173              = 0x106C,
            RegI_174              = 0x1070,
            RegI_175              = 0x1074,
            RegI_176              = 0x1078,
            RegI_177              = 0x107C,
            RegI_178              = 0x1084,
            RegI_179              = 0x1088,
            RegI_180              = 0x108C,
            RegI_181              = 0x1090,
            RegI_182              = 0x1098,
            RegI_183              = 0x10AC,
            RegI_184              = 0x10B0,
            RegI_185              = 0x10B4,
            RegI_186              = 0x10B8,
            RegI_187              = 0x10BC,
            RegI_188              = 0x10C0,
            RegI_189              = 0x10C4,
            RegI_190              = 0x10CC,
            RegI_191              = 0x10D4,
            RegI_192              = 0x10DC,
            RegI_193              = 0x10E0,
            RegI_194              = 0x10E4,
            RegI_195              = 0x10E8,
            RegI_196              = 0x10FC,
            RegI_197              = 0x1104,
            RegI_198              = 0x1108,
            RegI_199              = 0x1118,
            RegI_200              = 0x1120,
            RegI_201              = 0x1160,
            RegI_202              = 0x1164,
            RegI_203              = 0x1168,
            RegI_204              = 0x116C,
            RegI_205              = 0x1170,
            RegI_206              = 0x1174,
            RegI_207              = 0x1178,
            RegI_208              = 0x117C,
            RegI_209              = 0x1188,
            RegI_210              = 0x118C,
            RegI_211              = 0x1194,
            RegI_212              = 0x1198,
            RegI_213              = 0x119C,
            RegI_214              = 0x11A0,
            RegI_215              = 0x11A4,
            RegI_216              = 0x11A8,
            RegI_217              = 0x11C8,
            RegI_218              = 0x11D8,
            RegI_219              = 0x11E0,
            RegI_220              = 0x11E8,
            RegI_221              = 0x11EC,
            RegI_222              = 0x11F0,
            RegI_223              = 0x11F4,
            RegI_224              = 0x11FC,
            RegI_225              = 0x1200,
            RegI_226              = 0x1204,
            RegI_227              = 0x120C,
            RegI_228              = 0x1210,
            RegI_229              = 0x1214,
            RegI_230              = 0x1218,
            RegI_231              = 0x1224,
            RegI_232              = 0x1228,
            RegI_233              = 0x122C,
            RegI_234              = 0x1230,
            RegI_235              = 0x1234,
            RegI_236              = 0x1238,
            RegI_237              = 0x1244,
            RegI_238              = 0x1248,
            RegI_239              = 0x124C,
            RegI_240              = 0x1250,
            RegI_241              = 0x1254,
            RegI_242              = 0x125C,
            RegI_243              = 0x1260,
            RegI_244              = 0x1264,
            RegI_245              = 0x1268,
            RegI_246              = 0x126C,
            RegI_247              = 0x1270,
            RegI_248              = 0x1274,
            RegI_249              = 0x1278,
            RegI_250              = 0x127C,
            RegI_251              = 0x1280,
            RegI_252              = 0x1284,
            RegI_253              = 0x1288,
            RegI_254              = 0x128C,
            RegI_255              = 0x1290,
            RegI_256              = 0x1294,
            RegI_257              = 0x1298,
            RegI_258              = 0x129C,
            RegI_259              = 0x12A0,
            RegI_260              = 0x12A4,
            RegI_261              = 0x12A8,
            RegI_262              = 0x12AC,
            RegI_263              = 0x12B0,
            RegI_264              = 0x12B4,
            RegI_265              = 0x12B8,
            RegI_266              = 0x12BC,
            RegI_267              = 0x12C8,
            RegI_268              = 0x12CC,
            RegI_269              = 0x12D0,
            RegI_270              = 0x12D4,
            RegI_271              = 0x12D8,
            RegI_272              = 0x12DC,
            RegI_273              = 0x12E0,
            RegI_274              = 0x12E4,
            RegI_275              = 0x12E8,
            RegI_276              = 0x13E0,
            RegI_277              = 0x13E4,
            RegI_278              = 0x13E8,
            RegI_279              = 0x13EC,
            RegI_280              = 0x13F0,
            RegI_281              = 0x13F4,
            RegI_282              = 0x13F8,
            RegI_283              = 0x13FC,
            RegI_284              = 0x1600,
            RegI_285              = 0x17E8,
            RegI_286              = 0x17EC,
            RegI_287              = 0x17F0,
            RegI_288              = 0x17F4,
            RegI_289              = 0x17F8,
            RegI_290              = 0x17FC,
            RegI_291              = 0x2000,
            RegI_292              = 0x2004,
            RegI_293              = 0x2008,
            RegI_294              = 0x200C,
            RegI_295              = 0x2010,
            RegI_296              = 0x2014,
            RegI_297              = 0x2018,
            RegI_298              = 0x201C,
            RegI_299              = 0x2020,
            RegI_300              = 0x2024,
            RegI_301              = 0x2028,
            RegI_302              = 0x202C,
            RegI_303              = 0x2030,
            RegI_304              = 0x2034,
            RegI_305              = 0x2038,
            RegI_306              = 0x203C,
            RegI_307              = 0x2040,
            RegI_308              = 0x2044,
            RegI_309              = 0x2048,
            RegI_310              = 0x204C,
            RegI_311              = 0x2050,
            RegI_312              = 0x2054,
            RegI_313              = 0x2058,
            RegI_314              = 0x205C,
            RegI_315              = 0x2060,
            RegI_316              = 0x2064,
            RegI_317              = 0x2068,
            RegI_318              = 0x206C,
            RegI_319              = 0x2070,
            RegI_320              = 0x2074,
            RegI_321              = 0x2078,
            RegI_322              = 0x207C,
            RegI_323              = 0x2084,
            RegI_324              = 0x2088,
            RegI_325              = 0x208C,
            RegI_326              = 0x2090,
            RegI_327              = 0x2098,
            RegI_328              = 0x20AC,
            RegI_329              = 0x20B0,
            RegI_330              = 0x20B4,
            RegI_331              = 0x20B8,
            RegI_332              = 0x20BC,
            RegI_333              = 0x20C0,
            RegI_334              = 0x20C4,
            RegI_335              = 0x20CC,
            RegI_336              = 0x20D4,
            RegI_337              = 0x20DC,
            RegI_338              = 0x20E0,
            RegI_339              = 0x20E4,
            RegI_340              = 0x20E8,
            RegI_341              = 0x20FC,
            RegI_342              = 0x2104,
            RegI_343              = 0x2108,
            RegI_344              = 0x2118,
            RegI_345              = 0x2120,
            RegI_346              = 0x2160,
            RegI_347              = 0x2164,
            RegI_348              = 0x2168,
            RegI_349              = 0x216C,
            RegI_350              = 0x2170,
            RegI_351              = 0x2174,
            RegI_352              = 0x2178,
            RegI_353              = 0x217C,
            RegI_354              = 0x2188,
            RegI_355              = 0x218C,
            RegI_356              = 0x2194,
            RegI_357              = 0x2198,
            RegI_358              = 0x219C,
            RegI_359              = 0x21A0,
            RegI_360              = 0x21A4,
            RegI_361              = 0x21A8,
            RegI_362              = 0x21C8,
            RegI_363              = 0x21D8,
            RegI_364              = 0x21E0,
            RegI_365              = 0x21E8,
            RegI_366              = 0x21EC,
            RegI_367              = 0x21F0,
            RegI_368              = 0x21F4,
            RegI_369              = 0x21FC,
            RegI_370              = 0x2200,
            RegI_371              = 0x2204,
            RegI_372              = 0x220C,
            RegI_373              = 0x2210,
            RegI_374              = 0x2214,
            RegI_375              = 0x2218,
            RegI_376              = 0x2224,
            RegI_377              = 0x2228,
            RegI_378              = 0x222C,
            RegI_379              = 0x2230,
            RegI_380              = 0x2234,
            RegI_381              = 0x2238,
            RegI_382              = 0x2244,
            RegI_383              = 0x2248,
            RegI_384              = 0x224C,
            RegI_385              = 0x2250,
            RegI_386              = 0x2254,
            RegI_387              = 0x225C,
            RegI_388              = 0x2260,
            RegI_389              = 0x2264,
            RegI_390              = 0x2268,
            RegI_391              = 0x226C,
            RegI_392              = 0x2270,
            RegI_393              = 0x2274,
            RegI_394              = 0x2278,
            RegI_395              = 0x227C,
            RegI_396              = 0x2280,
            RegI_397              = 0x2284,
            RegI_398              = 0x2288,
            RegI_399              = 0x228C,
            RegI_400              = 0x2290,
            RegI_401              = 0x2294,
            RegI_402              = 0x2298,
            RegI_403              = 0x229C,
            RegI_404              = 0x22A0,
            RegI_405              = 0x22A4,
            RegI_406              = 0x22A8,
            RegI_407              = 0x22AC,
            RegI_408              = 0x22B0,
            RegI_409              = 0x22B4,
            RegI_410              = 0x22B8,
            RegI_411              = 0x22BC,
            RegI_412              = 0x22C8,
            RegI_413              = 0x22CC,
            RegI_414              = 0x22D0,
            RegI_415              = 0x22D4,
            RegI_416              = 0x22D8,
            RegI_417              = 0x22DC,
            RegI_418              = 0x22E0,
            RegI_419              = 0x22E4,
            RegI_420              = 0x22E8,
            RegI_421              = 0x23E0,
            RegI_422              = 0x23E4,
            RegI_423              = 0x23E8,
            RegI_424              = 0x23EC,
            RegI_425              = 0x23F0,
            RegI_426              = 0x23F4,
            RegI_427              = 0x23F8,
            RegI_428              = 0x23FC,
            RegI_429              = 0x2600,
            RegI_430              = 0x27E8,
            RegI_431              = 0x27EC,
            RegI_432              = 0x27F0,
            RegI_433              = 0x27F4,
            RegI_434              = 0x27F8,
            RegI_435              = 0x27FC,
            RegI_436              = 0x3000,
            RegI_437              = 0x3004,
            RegI_438              = 0x3008,
            RegI_439              = 0x300C,
            RegI_440              = 0x3010,
            RegI_441              = 0x3014,
            RegI_442              = 0x3018,
            RegI_443              = 0x301C,
            RegI_444              = 0x3020,
            RegI_445              = 0x3024,
            RegI_446              = 0x3028,
            RegI_447              = 0x302C,
            RegI_448              = 0x3030,
            RegI_449              = 0x3034,
            RegI_450              = 0x3038,
            RegI_451              = 0x303C,
            RegI_452              = 0x3040,
            RegI_453              = 0x3044,
            RegI_454              = 0x3048,
            RegI_455              = 0x304C,
            RegI_456              = 0x3050,
            RegI_457              = 0x3054,
            RegI_458              = 0x3058,
            RegI_459              = 0x305C,
            RegI_460              = 0x3060,
            RegI_461              = 0x3064,
            RegI_462              = 0x3068,
            RegI_463              = 0x306C,
            RegI_464              = 0x3070,
            RegI_465              = 0x3074,
            RegI_466              = 0x3078,
            RegI_467              = 0x307C,
            RegI_468              = 0x3084,
            RegI_469              = 0x3088,
            RegI_470              = 0x308C,
            RegI_471              = 0x3090,
            RegI_472              = 0x3098,
            RegI_473              = 0x30AC,
            RegI_474              = 0x30B0,
            RegI_475              = 0x30B4,
            RegI_476              = 0x30B8,
            RegI_477              = 0x30BC,
            RegI_478              = 0x30C0,
            RegI_479              = 0x30C4,
            RegI_480              = 0x30CC,
            RegI_481              = 0x30D4,
            RegI_482              = 0x30DC,
            RegI_483              = 0x30E0,
            RegI_484              = 0x30E4,
            RegI_485              = 0x30E8,
            RegI_486              = 0x30FC,
            RegI_487              = 0x3104,
            RegI_488              = 0x3108,
            RegI_489              = 0x3118,
            RegI_490              = 0x3120,
            RegI_491              = 0x3160,
            RegI_492              = 0x3164,
            RegI_493              = 0x3168,
            RegI_494              = 0x316C,
            RegI_495              = 0x3170,
            RegI_496              = 0x3174,
            RegI_497              = 0x3178,
            RegI_498              = 0x317C,
            RegI_499              = 0x3188,
            RegI_500              = 0x318C,
            RegI_501              = 0x3194,
            RegI_502              = 0x3198,
            RegI_503              = 0x319C,
            RegI_504              = 0x31A0,
            RegI_505              = 0x31A4,
            RegI_506              = 0x31A8,
            RegI_507              = 0x31C8,
            RegI_508              = 0x31D8,
            RegI_509              = 0x31E0,
            RegI_510              = 0x31E8,
            RegI_511              = 0x31EC,
            RegI_512              = 0x31F0,
            RegI_513              = 0x31F4,
            RegI_514              = 0x31FC,
            RegI_515              = 0x3200,
            RegI_516              = 0x3204,
            RegI_517              = 0x320C,
            RegI_518              = 0x3210,
            RegI_519              = 0x3214,
            RegI_520              = 0x3218,
            RegI_521              = 0x3224,
            RegI_522              = 0x3228,
            RegI_523              = 0x322C,
            RegI_524              = 0x3230,
            RegI_525              = 0x3234,
            RegI_526              = 0x3238,
            RegI_527              = 0x3244,
            RegI_528              = 0x3248,
            RegI_529              = 0x324C,
            RegI_530              = 0x3250,
            RegI_531              = 0x3254,
            RegI_532              = 0x325C,
            RegI_533              = 0x3260,
            RegI_534              = 0x3264,
            RegI_535              = 0x3268,
            RegI_536              = 0x326C,
            RegI_537              = 0x3270,
            RegI_538              = 0x3274,
            RegI_539              = 0x3278,
            RegI_540              = 0x327C,
            RegI_541              = 0x3280,
            RegI_542              = 0x3284,
            RegI_543              = 0x3288,
            RegI_544              = 0x328C,
            RegI_545              = 0x3290,
            RegI_546              = 0x3294,
            RegI_547              = 0x3298,
            RegI_548              = 0x329C,
            RegI_549              = 0x32A0,
            RegI_550              = 0x32A4,
            RegI_551              = 0x32A8,
            RegI_552              = 0x32AC,
            RegI_553              = 0x32B0,
            RegI_554              = 0x32B4,
            RegI_555              = 0x32B8,
            RegI_556              = 0x32BC,
            RegI_557              = 0x32C8,
            RegI_558              = 0x32CC,
            RegI_559              = 0x32D0,
            RegI_560              = 0x32D4,
            RegI_561              = 0x32D8,
            RegI_562              = 0x32DC,
            RegI_563              = 0x32E0,
            RegI_564              = 0x32E4,
            RegI_565              = 0x32E8,
            RegI_566              = 0x33E0,
            RegI_567              = 0x33E4,
            RegI_568              = 0x33E8,
            RegI_569              = 0x33EC,
            RegI_570              = 0x33F0,
            RegI_571              = 0x33F4,
            RegI_572              = 0x33F8,
            RegI_573              = 0x33FC,
            RegI_574              = 0x3600,
            RegI_575              = 0x37E8,
            RegI_576              = 0x37EC,
            RegI_577              = 0x37F0,
            RegI_578              = 0x37F4,
            RegI_579              = 0x37F8,
            RegI_580              = 0x37FC,
        }

        private enum Registers_K : long
        {
            RegK_1                = 0x0000,
            RegK_2                = 0x0004,
            RegK_3                = 0x0008,
            RegK_4                = 0x000C,
            RegK_5                = 0x0010,
            RegK_6                = 0x0014,
            RegK_7                = 0x0018,
            RegK_8                = 0x001C,
            RegK_9                = 0x0020,
            RegK_10               = 0x0024,
            RegK_11               = 0x0028,
            RegK_12               = 0x002C,
            RegK_13               = 0x0030,
            RegK_14               = 0x0034,
            RegK_15               = 0x0038,
            RegK_16               = 0x003C,
            RegK_17               = 0x0040,
            RegK_18               = 0x0044,
            RegK_19               = 0x0048,
            RegK_20               = 0x004C,
            RegK_21               = 0x0050,
            RegK_22               = 0x0054,
            RegK_23               = 0x0058,
            RegK_24               = 0x005C,
            RegK_25               = 0x0060,
            RegK_26               = 0x0064,
            RegK_27               = 0x0068,
            RegK_28               = 0x006C,
            RegK_29               = 0x0070,
            RegK_30               = 0x0074,
            RegK_31               = 0x0078,
            RegK_32               = 0x007C,
            RegK_33               = 0x0080,
            RegK_34               = 0x0084,
            RegK_35               = 0x0088,
            RegK_36               = 0x008C,
            RegK_37               = 0x0090,
            RegK_38               = 0x0094,
            RegK_39               = 0x0098,
            RegK_40               = 0x009C,
            RegK_41               = 0x00A0,
            RegK_42               = 0x00A4,
            RegK_43               = 0x00A8,
            RegK_44               = 0x1000,
            RegK_45               = 0x1004,
            RegK_46               = 0x1008,
            RegK_47               = 0x100C,
            RegK_48               = 0x1010,
            RegK_49               = 0x1014,
            RegK_50               = 0x1018,
            RegK_51               = 0x101C,
            RegK_52               = 0x1020,
            RegK_53               = 0x1024,
            RegK_54               = 0x1028,
            RegK_55               = 0x102C,
            RegK_56               = 0x1030,
            RegK_57               = 0x1034,
            RegK_58               = 0x1038,
            RegK_59               = 0x103C,
            RegK_60               = 0x1040,
            RegK_61               = 0x1044,
            RegK_62               = 0x1048,
            RegK_63               = 0x104C,
            RegK_64               = 0x1050,
            RegK_65               = 0x1054,
            RegK_66               = 0x1058,
            RegK_67               = 0x105C,
            RegK_68               = 0x1060,
            RegK_69               = 0x1064,
            RegK_70               = 0x1068,
            RegK_71               = 0x106C,
            RegK_72               = 0x1070,
            RegK_73               = 0x1074,
            RegK_74               = 0x1078,
            RegK_75               = 0x107C,
            RegK_76               = 0x1080,
            RegK_77               = 0x1084,
            RegK_78               = 0x1088,
            RegK_79               = 0x108C,
            RegK_80               = 0x1090,
            RegK_81               = 0x1094,
            RegK_82               = 0x1098,
            RegK_83               = 0x109C,
            RegK_84               = 0x10A0,
            RegK_85               = 0x10A4,
            RegK_86               = 0x10A8,
            RegK_87               = 0x2000,
            RegK_88               = 0x2004,
            RegK_89               = 0x2008,
            RegK_90               = 0x200C,
            RegK_91               = 0x2010,
            RegK_92               = 0x2014,
            RegK_93               = 0x2018,
            RegK_94               = 0x201C,
            RegK_95               = 0x2020,
            RegK_96               = 0x2024,
            RegK_97               = 0x2028,
            RegK_98               = 0x202C,
            RegK_99               = 0x2030,
            RegK_100              = 0x2034,
            RegK_101              = 0x2038,
            RegK_102              = 0x203C,
            RegK_103              = 0x2040,
            RegK_104              = 0x2044,
            RegK_105              = 0x2048,
            RegK_106              = 0x204C,
            RegK_107              = 0x2050,
            RegK_108              = 0x2054,
            RegK_109              = 0x2058,
            RegK_110              = 0x205C,
            RegK_111              = 0x2060,
            RegK_112              = 0x2064,
            RegK_113              = 0x2068,
            RegK_114              = 0x206C,
            RegK_115              = 0x2070,
            RegK_116              = 0x2074,
            RegK_117              = 0x2078,
            RegK_118              = 0x207C,
            RegK_119              = 0x2080,
            RegK_120              = 0x2084,
            RegK_121              = 0x2088,
            RegK_122              = 0x208C,
            RegK_123              = 0x2090,
            RegK_124              = 0x2094,
            RegK_125              = 0x2098,
            RegK_126              = 0x209C,
            RegK_127              = 0x20A0,
            RegK_128              = 0x20A4,
            RegK_129              = 0x20A8,
            RegK_130              = 0x3000,
            RegK_131              = 0x3004,
            RegK_132              = 0x3008,
            RegK_133              = 0x300C,
            RegK_134              = 0x3010,
            RegK_135              = 0x3014,
            RegK_136              = 0x3018,
            RegK_137              = 0x301C,
            RegK_138              = 0x3020,
            RegK_139              = 0x3024,
            RegK_140              = 0x3028,
            RegK_141              = 0x302C,
            RegK_142              = 0x3030,
            RegK_143              = 0x3034,
            RegK_144              = 0x3038,
            RegK_145              = 0x303C,
            RegK_146              = 0x3040,
            RegK_147              = 0x3044,
            RegK_148              = 0x3048,
            RegK_149              = 0x304C,
            RegK_150              = 0x3050,
            RegK_151              = 0x3054,
            RegK_152              = 0x3058,
            RegK_153              = 0x305C,
            RegK_154              = 0x3060,
            RegK_155              = 0x3064,
            RegK_156              = 0x3068,
            RegK_157              = 0x306C,
            RegK_158              = 0x3070,
            RegK_159              = 0x3074,
            RegK_160              = 0x3078,
            RegK_161              = 0x307C,
            RegK_162              = 0x3080,
            RegK_163              = 0x3084,
            RegK_164              = 0x3088,
            RegK_165              = 0x308C,
            RegK_166              = 0x3090,
            RegK_167              = 0x3094,
            RegK_168              = 0x3098,
            RegK_169              = 0x309C,
            RegK_170              = 0x30A0,
            RegK_171              = 0x30A4,
            RegK_172              = 0x30A8,
        }

        private enum Registers_D : long
        {
            RegD_1                = 0x0000,
            RegD_2                = 0x0004,
            RegD_3                = 0x0008,
            RegD_4                = 0x000C,
            RegD_5                = 0x0040,
            RegD_6                = 0x0044,
            RegD_7                = 0x1000,
            RegD_8                = 0x1004,
            RegD_9                = 0x1008,
            RegD_10               = 0x100C,
            RegD_11               = 0x1040,
            RegD_12               = 0x1044,
            RegD_13               = 0x2000,
            RegD_14               = 0x2004,
            RegD_15               = 0x2008,
            RegD_16               = 0x200C,
            RegD_17               = 0x2040,
            RegD_18               = 0x2044,
            RegD_19               = 0x3000,
            RegD_20               = 0x3004,
            RegD_21               = 0x3008,
            RegD_22               = 0x300C,
            RegD_23               = 0x3040,
            RegD_24               = 0x3044,
        }

        private enum Registers_J : long
        {
            RegJ_1                = 0x0000,
            RegJ_2                = 0x0004,
            RegJ_3                = 0x0008,
            RegJ_4                = 0x000C,
            RegJ_5                = 0x0040,
            RegJ_6                = 0x0044,
            RegJ_7                = 0x1000,
            RegJ_8                = 0x1004,
            RegJ_9                = 0x1008,
            RegJ_10               = 0x100C,
            RegJ_11               = 0x1040,
            RegJ_12               = 0x1044,
            RegJ_13               = 0x2000,
            RegJ_14               = 0x2004,
            RegJ_15               = 0x2008,
            RegJ_16               = 0x200C,
            RegJ_17               = 0x2040,
            RegJ_18               = 0x2044,
            RegJ_19               = 0x3000,
            RegJ_20               = 0x3004,
            RegJ_21               = 0x3008,
            RegJ_22               = 0x300C,
            RegJ_23               = 0x3040,
            RegJ_24               = 0x3044,
        }

        private enum Registers_E : long
        {
            RegE_1                = 0x0000,
            RegE_2                = 0x0004,
            RegE_3                = 0x0008,
            RegE_4                = 0x000C,
            RegE_5                = 0x0010,
            RegE_6                = 0x0014,
            RegE_7                = 0x0018,
            RegE_8                = 0x001C,
            RegE_9                = 0x0020,
            RegE_10               = 0x0024,
            RegE_11               = 0x0028,
            RegE_12               = 0x002C,
            RegE_13               = 0x0030,
            RegE_14               = 0x1000,
            RegE_15               = 0x1004,
            RegE_16               = 0x1008,
            RegE_17               = 0x100C,
            RegE_18               = 0x1010,
            RegE_19               = 0x1014,
            RegE_20               = 0x1018,
            RegE_21               = 0x101C,
            RegE_22               = 0x1020,
            RegE_23               = 0x1024,
            RegE_24               = 0x1028,
            RegE_25               = 0x102C,
            RegE_26               = 0x1030,
            RegE_27               = 0x2000,
            RegE_28               = 0x2004,
            RegE_29               = 0x2008,
            RegE_30               = 0x200C,
            RegE_31               = 0x2010,
            RegE_32               = 0x2014,
            RegE_33               = 0x2018,
            RegE_34               = 0x201C,
            RegE_35               = 0x2020,
            RegE_36               = 0x2024,
            RegE_37               = 0x2028,
            RegE_38               = 0x202C,
            RegE_39               = 0x2030,
            RegE_40               = 0x3000,
            RegE_41               = 0x3004,
            RegE_42               = 0x3008,
            RegE_43               = 0x300C,
            RegE_44               = 0x3010,
            RegE_45               = 0x3014,
            RegE_46               = 0x3018,
            RegE_47               = 0x301C,
            RegE_48               = 0x3020,
            RegE_49               = 0x3024,
            RegE_50               = 0x3028,
            RegE_51               = 0x302C,
            RegE_52               = 0x3030,
        }

        private enum Registers_F : long
        {
            RegF_1                = 0x0000,
            RegF_2                = 0x0004,
            RegF_3                = 0x0008,
            RegF_4                = 0x000C,
            RegF_5                = 0x0010,
            RegF_6                = 0x0014,
            RegF_7                = 0x0018,
            RegF_8                = 0x001C,
            RegF_9                = 0x0020,
            RegF_10               = 0x0024,
            RegF_11               = 0x0028,
            RegF_12               = 0x002C,
            RegF_13               = 0x0030,
            RegF_14               = 0x1000,
            RegF_15               = 0x1004,
            RegF_16               = 0x1008,
            RegF_17               = 0x100C,
            RegF_18               = 0x1010,
            RegF_19               = 0x1014,
            RegF_20               = 0x1018,
            RegF_21               = 0x101C,
            RegF_22               = 0x1020,
            RegF_23               = 0x1024,
            RegF_24               = 0x1028,
            RegF_25               = 0x102C,
            RegF_26               = 0x1030,
            RegF_27               = 0x2000,
            RegF_28               = 0x2004,
            RegF_29               = 0x2008,
            RegF_30               = 0x200C,
            RegF_31               = 0x2010,
            RegF_32               = 0x2014,
            RegF_33               = 0x2018,
            RegF_34               = 0x201C,
            RegF_35               = 0x2020,
            RegF_36               = 0x2024,
            RegF_37               = 0x2028,
            RegF_38               = 0x202C,
            RegF_39               = 0x2030,
            RegF_40               = 0x3000,
            RegF_41               = 0x3004,
            RegF_42               = 0x3008,
            RegF_43               = 0x300C,
            RegF_44               = 0x3010,
            RegF_45               = 0x3014,
            RegF_46               = 0x3018,
            RegF_47               = 0x301C,
            RegF_48               = 0x3020,
            RegF_49               = 0x3024,
            RegF_50               = 0x3028,
            RegF_51               = 0x302C,
            RegF_52               = 0x3030,
        }
    }
}