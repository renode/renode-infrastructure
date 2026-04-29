//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.CAN
{
    public class STM32_FDCAN : BasicDoubleWordPeripheral, ICAN, IKnownSize
    {
        public STM32_FDCAN(IMachine machine, STM32Series series, ArrayMemory messageRam) : base(machine)
        {
            switch(series)
            {
            case STM32Series.L5:
                break;
            default:
                throw new ConstructionException($"FDCAN model currently only supports the L5 series variant, {series} provided");
            }
            this.messageRam = messageRam;
            Int0 = new GPIO();
            Int1 = new GPIO();
            // TODO: Frequency is the default for one of the possibly clocks (48Mhz), should be configured by the RCC peripheral
            kernelFrequency = 48000000;
            // Actual timestamp frequency is set in `UpdateTimerConfiguration()`
            timestampCounter = new LimitTimer(machine.ClockSource, 1, this, nameof(timestampCounter), limit: UInt16.MaxValue, direction: Time.Direction.Ascending, workMode: Time.WorkMode.OneShot);
            timestampCounter.LimitReached += () => OnTimestampWraparound();
            timeoutCounter = new LimitTimer(machine.ClockSource, 1, this, nameof(timeoutCounter), limit: UInt16.MaxValue, direction: Time.Direction.Descending, workMode: Time.WorkMode.OneShot);
            timeoutCounter.LimitReached += () => OnTimeout();
            rxFIFOFull = new IFlagRegisterField[2];
            rxFIFOGetIndex = new IValueRegisterField[2];
            rxFIFOPutIndex = new IValueRegisterField[2];
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            timestampCounter.Reset();
            UpdateTimerConfiguration();
            UpdateInterrupts();
            lastHighPrioBufferIndex.Value = 2;
        }

        public void OnFrameReceived(CANMessageFrame message)
        {
            // Clear last error code on new message
            lastErrorCode.Value = 0;
            // FDCAN protocol status flags are updated independant of acceptance filtering
            if(message.FDFormat)
            {
                receivedFDCANMessageFlag.Value = true;
                receivedBRSFlag.Value = message.BitRateSwitch;
                receivedESIFlag.Value = false; // ESI flag not implemented in Renode
            }

            this.DebugLog("Processing message with id: {0}, length: {1}", message.Id, message.Data.Length);
            var shouldRejectRemoteFrames = message.ExtendedFormat ? rejectRemoteFramesExtended.Value : rejectRemoteFramesStandard.Value;
            if(message.RemoteFrame && shouldRejectRemoteFrames)
            {
                this.DebugLog("Received remote frame while remote frames are being rejected, dropping");
                return;
            }

            var filterIndex = 0;
            IFilterElement filter;
            if(!(message.ExtendedFormat ? AcceptanceFilterMessage<ExtendedFilterElement>(message, out filterIndex, out filter) : AcceptanceFilterMessage<StandardFilterElement>(message, out filterIndex, out filter)))
            {
                // Check config registers for how unmatched frames are handled
                var nonMatchingFramesMode = message.ExtendedFormat ? acceptNonMatchingFramesExtended.Value : acceptNonMatchingFramesStandard.Value;
                switch(nonMatchingFramesMode)
                {
                case AcceptMode.AcceptInRxFIFO0:
                    TryStoreReceivedMessage(message, 0, filterIndex, out var _, nonMatchingFrame: true);
                    break;
                case AcceptMode.AcceptInRxFIFO1:
                    TryStoreReceivedMessage(message, 1, filterIndex, out var __, nonMatchingFrame: true);
                    break;
                default:
                    this.DebugLog("Frame not matching any filter and non-matching frames are configured to be dropped");
                    return;
                }
            }
            else
            {
                MessageStorageIndicator? highPriorityStored = null;
                uint storedIndex = 0;
                var config = message.ExtendedFormat ? ((ExtendedFilterElement)filter).Config : ((StandardFilterElement)filter).Config;
                switch(config)
                {
                case FilterElementConfig.Reject:
                    this.DebugLog("Frame matched a rejecting rule ({0}), dropping", filter);
                    return;
                case FilterElementConfig.StoreInRxFIFO0:
                    TryStoreReceivedMessage(message, 0, filterIndex, out storedIndex);
                    break;
                case FilterElementConfig.StoreInRxFIFO1:
                    TryStoreReceivedMessage(message, 1, filterIndex, out storedIndex);
                    break;
                case FilterElementConfig.SetPriority:
                    this.DebugLog("Received frame matches set priority only rule, setting fields and dropping");
                    highPriorityStored = MessageStorageIndicator.NoFIFOSelected;
                    break;
                case FilterElementConfig.SetPriorityAndStoreInRxFIFO0:
                    this.DebugLog("Received high-priority frame");
                    highPriorityStored = TryStoreReceivedMessage(message, 0, filterIndex, out storedIndex) ? MessageStorageIndicator.MessageStoredInFIFO0 : MessageStorageIndicator.FIFOOverrun;
                    break;
                case FilterElementConfig.SetPriorityAndStoreInRxFIFO1:
                    this.DebugLog("Received high-priority frame");
                    highPriorityStored = TryStoreReceivedMessage(message, 1, filterIndex, out storedIndex) ? MessageStorageIndicator.MessageStoredInFIFO1 : MessageStorageIndicator.FIFOOverrun;
                    break;
                default:
                    throw new UnreachableException($"Unexpected FilterElementConfig variant {config}");
                }
                if(highPriorityStored != null)
                {
                    // High priority frame received, update HPMS register
                    lastHighPrioFilterList.Value = false;
                    lastHighPrioFilterIndex.Value = (ulong)filterIndex;
                    lastHighPrioMessageStorage.Value = highPriorityStored.Value;
                    lastHighPrioBufferIndex.Value = storedIndex;
                    interruptStatusFlags[(long)Interrupts.HighPriorityMessage].Value = true;
                    UpdateInterrupts();
                }
            }
        }

        public long Size => 0x400;

        public GPIO Int0 { get; }

        public GPIO Int1 { get; }

        public event Action<CANMessageFrame> FrameSent;

        private void DefineRegisters()
        {
            Registers.CoreReleaseRegister.Define(this, 0x32141218, name: "FDCAN_CREL")
                .WithValueField(0, 32, mode: FieldMode.Read, name: "FDCAN_CREL");
            Registers.EndianRegister.Define(this, 0x87654321, name: "FDCAN_ENDN")
                .WithValueField(0, 32, mode: FieldMode.Read, name: "ETV");
            Registers.DataBitTimingAndPrescalerRegister.Define(this, 0x00000A33, name: "FDCAN_DBTP")
                .WithTag("FDCAN_DBTP", 0, 32);
            Registers.TestRegister.Define(this, name: "FDCAN_TEST")
                .WithReservedBits(8, 24)
                .WithTaggedFlag("RX", 7)
                .WithTag("TX", 5, 2)
                .WithTaggedFlag("LBCK", 4)
                .WithReservedBits(0, 4);
            Registers.RAMWatchdogRegister.Define(this, name: "FDCAN_RWD")
                .WithReservedBits(16, 16)
                .WithTag("WDV", 8, 8)
                .WithConditionallyWritableValueField(0, 8, out ramWatchdogResetValue, ConfigChangeEnabled, name: "WDC");
            Registers.CCControlRegister.Define(this, 0x1, name: "FDCAN_CCCR")
                .WithReservedBits(16, 16)
                .WithTaggedFlag("NISO", 15) // Non-ISO Operation
                .WithTaggedFlag("TXP", 14) // Transmit pause enable
                .WithTaggedFlag("EFBI", 13) // Edge filtering during bus integration
                .WithFlag(12, out protocolExceptionHandlingDisable, name: "PXHD")
                .WithReservedBits(10, 2)
                .WithFlag(9, out bitRateSwitching, name: "BRSE")
                .WithFlag(8, out fdOperationEnable, name: "FDOE")
                .WithTaggedFlag("TEST", 7)
                .WithTaggedFlag("DAR", 6) // Disable Automatic Retransmission
                .WithTaggedFlag("MON", 5) // Bus monitoring mode
                .WithFlag(4, out clockStopRequested, changeCallback: (_, val) => RequestClockStopCallback(val), name: "CSR")
                .WithFlag(3, out clockStopAcknowledge, mode: FieldMode.Read, name: "CSA")
                .WithTaggedFlag("ASM", 2) // ASM restricted mode
                .WithConditionallyWritableFlag(1, out configurationChangeEnable, () => initFlag.Value,
                    changeCallback: (_, val) => CCEBitChangedCallback(val),
                    name: "CCE")
                .WithFlag(0, out initFlag, name: "INIT", changeCallback: (_, val) => InitBitChangedCallback(val));
            Registers.NominalBitTimingAndPrescalerRegister.Define(this, 0x06000A03, name: "FDCAN_NBTP")
                .WithTag("NSJW", 25, 7) // Nominal sync jump width, not simulated in renode
                .WithReservedBits(7, 1)
                .WithConditionallyWritableValueField(16, 9, out bitRatePrescaler, ConfigChangeEnabled, name: "NBRP")
                .WithConditionallyWritableValueField(8, 8, out timeSegmentBeforeSamplePoint, ConfigChangeEnabled, name: "NTSEG")
                .WithValueField(0, 7, out timeSegmentAfterSamplePoint, name: "NTSEG2")
                .WithChangeCallback((_, __) => UpdateTimerConfiguration());
            Registers.TimestampCounterConfigRegister.Define(this, name: "FDCAN_TSCC")
                .WithReservedBits(20, 12)
                .WithReservedBits(2, 14)
                .WithConditionallyWritableValueField(16, 4, out timestampCounterPrescaler, ConfigChangeEnabled, name: "TCP")
                .WithConditionallyWritableEnumField<DoubleWordRegister, TimestampMode>(0, 2, out timestampMode, ConfigChangeEnabled, name: "TSS")
                .WithChangeCallback((_, __) => UpdateTimerConfiguration());
            Registers.TimestampCounterValueRegister.Define(this, name: "FDCAN_TSCV")
                .WithReservedBits(16, 16)
                .WithValueField(0, 16, valueProviderCallback: _ => ReadTimestampCounter(), writeCallback: (_, __) => ResetTimestampCounter());
            Registers.TimeoutCounterConfigRegister.Define(this, 0xFFFF0000, name: "FDCAN_TOCC")
                .WithReservedBits(3, 13)
                .WithConditionallyWritableValueField(16, 16, out timeoutPeriod, ConfigChangeEnabled, name: "TOP")
                .WithConditionallyWritableEnumField<DoubleWordRegister, TimeoutSelect>(1, 2, out timeoutSelect, ConfigChangeEnabled, name: "TOS")
                .WithConditionallyWritableFlag(0, out timeoutCounterEnable, ConfigChangeEnabled, name: "ETOC")
                .WithChangeCallback((_, __) => UpdateTimerConfiguration());
            Registers.TimeoutCounterValueRegister.Define(this, 0x0000FFFF, name: "FDCAN_TOCV")
                .WithReservedBits(16, 16)
                .WithValueField(0, 16, valueProviderCallback: _ => timeoutCounter.Value, writeCallback: (_, __) => OnTimeoutCounterWritten());
            Registers.ErrorCounterRegister.Define(this, name: "FDCAN_ECR")
                .WithReservedBits(24, 8)
                .WithValueField(16, 8, out canErrorLogCounter, mode: FieldMode.ReadToClear, name: "CEL")
                .WithFlag(15, out reciveErrorPassiveFlag, mode: FieldMode.Read, name: "RP")
                .WithValueField(8, 7, out reciveErrorCounter, mode: FieldMode.Read, name: "REC")
                .WithValueField(0, 8, out transmitErrorCount, mode: FieldMode.Read, name: "TEC");
            Registers.ProtocolStatusRegister.Define(this, 0x00000707, name: "FDCAN_PSR")
                .WithReservedBits(23, 9)
                .WithTag("TDCV", 16, 7)
                .WithReservedBits(15, 1)
                .WithFlag(14, out protocolExceptionEventFlag, mode: FieldMode.ReadToClear, name: "PXE")
                .WithFlag(13, out receivedFDCANMessageFlag, mode: FieldMode.ReadToClear, name: "REDL")
                .WithFlag(12, out receivedBRSFlag, mode: FieldMode.ReadToClear, name: "RBRS")
                .WithFlag(11, out receivedESIFlag, mode: FieldMode.ReadToClear, name: "RESI")
                .WithValueField(8, 3, out dataLastErrorCode, mode: FieldMode.ReadToSet, name: "DLEC")
                .WithFlag(7, out busOffStatus, mode: FieldMode.Read, name: "BO")
                .WithFlag(6, mode: FieldMode.Read, valueProviderCallback: _ => reciveErrorCounter.Value < 96 && transmitErrorCount.Value < 96, name: "EW")
                .WithFlag(5, out errorPassiveFlag, mode: FieldMode.Read, name: "EP")
                .WithValueField(3, 2, mode: FieldMode.Read, valueProviderCallback: _ => 0b01, name: "ACT") // Since transmissions are instant in renode, only idle activity status is observable
                .WithValueField(0, 3, out lastErrorCode, mode: FieldMode.ReadToSet, name: "LEC");
            // Transmitter delay is not modeled in renode, but driver expects it to keep its values
            Registers.TransmitterDelayCompensationRegister.Define(this, name: "FDCAN_TDCR")
                .WithReservedBits(15, 17)
                .WithReservedBits(7, 1)
                .WithConditionallyWritableValueField(8, 7, out transmitterDelayCompensationEffect, ConfigChangeEnabled, name: "TDCO")
                .WithConditionallyWritableValueField(0, 7, out transmitterDelayCompensationFilter, ConfigChangeEnabled, name: "TDCF");
            Registers.InterruptRegister.Define(this, name: "FDCAN_IR")
                .WithReservedBits(24, 8)
                .WithFlags(0, 24, out interruptStatusFlags, mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithChangeCallback((_, __) => UpdateInterrupts());
            Registers.InterruptEnableRegister.Define(this, name: "FDCAN_IE")
                .WithReservedBits(24, 8)
                .WithFlags(0, 24, out interruptEnableFlags)
                .WithChangeCallback((_, __) => UpdateInterrupts());
            Registers.InterruptLineSelectRegister.Define(this, name: "FDCAN_ILS")
                .WithReservedBits(7, 23)
                .WithFlag(6, out protocolErrorLineSelect, name: "PERR")
                .WithFlag(5, out bitAndLineErrorLineSelect, name: "BERR")
                .WithFlag(4, out miscInterruptLineSelect, name: "MISC")
                .WithFlag(3, out txFIFOErrorLineSelect, name: "TFERR")
                .WithFlag(2, out statusMessageLineSelect, name: "SMSG")
                .WithFlag(1, out rxFIFO1MessageLineSelect, name: "RXFIFO1")
                .WithFlag(0, out rxFIFO0MessageLineSelect, name: "RXFIFO0")
                .WithChangeCallback((_, __) => UpdateInterrupts());
            Registers.InterruptLineEnableRegister.Define(this, name: "FDCAN_ILE")
                .WithReservedBits(2, 30)
                .WithFlag(1, out line1InterruptEnable, name: "EINT1")
                .WithFlag(0, out line0InterruptEnable, name: "EINT0")
                .WithChangeCallback((_, __) => UpdateInterrupts());
            Registers.GlobalFilterConfigRegister.Define(this, name: "FDCAN_RXGFC")
                .WithReservedBits(28, 4)
                .WithConditionallyWritableValueField(24, 4, out numberOfExtendedFilters, ConfigChangeEnabled, name: "LSE")
                .WithReservedBits(21, 3)
                .WithConditionallyWritableValueField(16, 5, out numberOfStandardFilters, ConfigChangeEnabled, name: "LSS")
                .WithReservedBits(10, 6)
                .WithConditionallyWritableFlag(9, out fifo0Overwrite, ConfigChangeEnabled, name: "F0OM")
                .WithConditionallyWritableFlag(8, out fifo1Overwrite, ConfigChangeEnabled, name: "F1OM")
                .WithReservedBits(6, 2)
                .WithConditionallyWritableEnumField<DoubleWordRegister, AcceptMode>(4, 2, out acceptNonMatchingFramesStandard, ConfigChangeEnabled, name: "ANFS")
                .WithConditionallyWritableEnumField<DoubleWordRegister, AcceptMode>(2, 2, out acceptNonMatchingFramesExtended, ConfigChangeEnabled, name: "ANFE")
                .WithConditionallyWritableFlag(1, out rejectRemoteFramesStandard, ConfigChangeEnabled, name: "RRFS")
                .WithConditionallyWritableFlag(0, out rejectRemoteFramesExtended, ConfigChangeEnabled, name: "RRFE");
            Registers.ExtendedIDAndMaskRegister.Define(this, 0x1FFFFFFF, name: "FDCAN_XIDAM")
                .WithReservedBits(29, 3)
                .WithConditionallyWritableValueField(0, 29, out extendedIDMask, ConfigChangeEnabled, name: "EIDM");
            Registers.HighPriorityMessageStatusRegister.Define(this, name: "FDCAN_HPMS")
                .WithReservedBits(16, 16)
                .WithFlag(15, out lastHighPrioFilterList, mode: FieldMode.Read, name: "FLST")
                .WithReservedBits(13, 2)
                .WithValueField(8, 5, out lastHighPrioFilterIndex, mode: FieldMode.Read, name: "FIDX")
                .WithEnumField<DoubleWordRegister, MessageStorageIndicator>(6, 2, out lastHighPrioMessageStorage, mode: FieldMode.Read, name: "MSI")
                .WithReservedBits(3, 3)
                .WithValueField(0, 3, out lastHighPrioBufferIndex, mode: FieldMode.Read, name: "BIDX");
            Registers.RxFIFO0StatusRegister.DefineMany(this, 2, (reg, i) =>
            {
                reg
                .WithReservedBits(26, 6)
                .WithFlag(25, mode: FieldMode.Read, valueProviderCallback: _ => interruptStatusFlags[i == 0 ? (long)Interrupts.RxFIFO0MessageLost : (long)Interrupts.RxFIFO1MessageLost].Value, name: $"RF{i}L")
                .WithFlag(24, out rxFIFOFull[i], mode: FieldMode.Read, name: $"F{i}F")
                .WithReservedBits(18, 6)
                .WithValueField(16, 2, out rxFIFOPutIndex[i], mode: FieldMode.Read, name: $"F{i}PI")
                .WithReservedBits(10, 6)
                .WithValueField(8, 2, out rxFIFOGetIndex[i], mode: FieldMode.Read, name: $"F{i}GI")
                .WithReservedBits(4, 4)
                .WithValueField(0, 4,
                    valueProviderCallback: _ => rxFIFOFull[i].Value ? 3 : ((rxFIFOPutIndex[i].Value - rxFIFOGetIndex[i].Value) + 3) % 3,
                    mode: FieldMode.Read,
                    name: $"F{i}FL");
            }, Registers.RxFIFO1StatusRegister - Registers.RxFIFO0StatusRegister);
            Registers.RxFIFO0AcknowlegeRegister.DefineMany(this, 2, (reg, i) =>
            {
                reg
                .WithReservedBits(3, 29)
                .WithValueField(0, 3,
                    writeCallback: (_, value) =>
                    {
                        rxFIFOGetIndex[i].Value = (value + 1) % 3;
                        rxFIFOFull[i].Value = false;
                    },
                    name: $"F{i}AI");
            }, Registers.RxFIFO1AcknowlegeRegister - Registers.RxFIFO0AcknowlegeRegister);
            Registers.TxBufferConfigurationRegister.Define(this, name: "FXCAN_TXBC")
                .WithReservedBits(25, 7)
                .WithConditionallyWritableFlag(24, out txFIFOQueueModeFlag, ConfigChangeEnabled, name: "TFQM")
                .WithReservedBits(0, 24);
            Registers.TxFIFOStatusRegister.Define(this, 0x00000003, name: "FDCAN_TXFQS")
                .WithReservedBits(22, 10)
                .WithFlag(21, mode: FieldMode.Read, valueProviderCallback: _ => false, name: "TFQF")
                .WithReservedBits(18, 3)
                .WithValueField(16, 2, mode: FieldMode.Read, valueProviderCallback: _ => 0, name: "TFQPI")
                .WithReservedBits(10, 6)
                .WithValueField(8, 2, mode: FieldMode.Read, valueProviderCallback: _ => 0, name: "TFGI")
                .WithReservedBits(3, 5)
                .WithValueField(0, 3,
                    valueProviderCallback: _ => txFIFOQueueModeFlag.Value ? 0 : (ulong)3,
                    name: "TFFL");
            Registers.TxBufferRequestPendingRegister.Define(this, name: "FDCAN_TXBRP")
                .WithReservedBits(3, 29)
                .WithValueField(0, 3, out requestPendingFlags, name: "TRP");
            Registers.TxBufferAddRequestRegister.Define(this, name: "FDCAN_TXBAR")
                .WithReservedBits(3, 29)
                .WithValueField(0, 3, valueProviderCallback: _ => 0, writeCallback: (_, val) =>
                {
                    if(val != 0)
                    {
                        if(initFlag.Value)
                        {
                            this.WarningLog("Tried to start transfer while in bus off state");
                            requestPendingFlags.Value = val;
                            return;
                        }
                        BitHelper.ForeachActiveBit(val, SendBuffer);
                        interruptStatusFlags[(long)Interrupts.TxFIFOEmpty].Value = true;
                        interruptStatusFlags[(long)Interrupts.TransmissionCompleted].Value = true;
                        UpdateInterrupts();
                    }
                });
            Registers.TxBufferCancellationRequestRegister.Define(this, name: "FDCAN_TXBCR")
                .WithReservedBits(3, 29)
                .WithTag("CR", 0, 3); // Transfers are never pending in Renode, so they can't be canceled
            Registers.TxBufferTransmissionOccuredRegister.Define(this, name: "FDCAN_TXBTO")
                .WithReservedBits(3, 29)
                .WithFlags(0, 3, out transmissionOccurredFlags, mode: FieldMode.Read, name: "TO");
            Registers.TxBufferCancellationFinishedRegister.Define(this, name: "FDCAN_TXBCF")
                .WithReservedBits(3, 29)
                .WithTag("CF", 0, 3);
            Registers.TxBufferTransmissionInterruptEnableRegister.Define(this, name: "FDCAN_TXBTIE")
                .WithReservedBits(3, 29)
                .WithFlags(0, 3, out transmissionInterruptEnableFlags, name: "TIE");
            Registers.TxBufferCancellationFinishedInterruptEnableRegister.Define(this, name: "FDCAN_TXBCIE")
                .WithReservedBits(3, 29)
                .WithTag("CFIE", 0, 3);
            Registers.TxEventFIFOStatusRegister.Define(this, name: "FDCAN_TXEFS")
                .WithReservedBits(26, 6)
                .WithFlag(25, mode: FieldMode.Read, valueProviderCallback: _ => interruptStatusFlags[(long)Interrupts.TxEventFIFOElementLost].Value, name: "TEFL")
                .WithFlag(24, out txEventFIFOFull, mode: FieldMode.Read, name: "EFF")
                .WithReservedBits(18, 6)
                .WithValueField(16, 2, out txEventFIFOPutIndex, mode: FieldMode.Read, name: "EFPI")
                .WithReservedBits(10, 6)
                .WithValueField(8, 2, out txEventFIFOGetIndex, mode: FieldMode.Read, name: "EFGI")
                .WithReservedBits(4, 4)
                .WithValueField(0, 4,
                    valueProviderCallback: _ => txEventFIFOFull.Value ? 3 : ((txEventFIFOPutIndex.Value - txEventFIFOGetIndex.Value) + 3) % 3,
                    mode: FieldMode.Read,
                    name: "EFFL");
            Registers.TxEventFIFOAcknowledgeRegister.Define(this, name: "FDCAN_TXEFA")
                .WithReservedBits(3, 29)
                .WithValueField(0, 3,
                    writeCallback: (_, value) =>
                    {
                        txEventFIFOGetIndex.Value = (value + 1) % 3;
                        txEventFIFOFull.Value = false;
                    },
                    name: "EFAI");
            Registers.ConfigClockDividerRegister.Define(this, name: "FDCAN_CKDIV")
                .WithReservedBits(4, 28)
                .WithConditionallyWritableValueField(0, 4, out inputClockDivider, ConfigChangeEnabled,
                    changeCallback: (_, __) => UpdateTimerConfiguration(),
                    name: "PDIV");
        }

        private void SendBuffer(byte bufferIndex)
        {
            DebugHelper.Assert(bufferIndex < 3, "RequestTransfer called with invalid index");
            // Clear the transmission occurred flag
            transmissionOccurredFlags[bufferIndex].Value = false;
            var buffer = messageRam.ReadBytes((long)MessageRAMOffsets.TxBuffers + (MessageBufferElementSize * bufferIndex), MessageBufferElementSize, context: this);
            var txElement = Packet.Decode<TxMessageElement>(buffer);
            // Turn off disabled features
            if(!fdOperationEnable.Value)
            {
                txElement.FDFormat = false;
                txElement.BitRateSwitching = false;
            }
            else if(!bitRateSwitching.Value)
            {
                txElement.BitRateSwitching = false;
            }

            var frame = (CANMessageFrame)txElement;
            if(FrameSent == null)
            {
                this.WarningLog("Attempted to send CAN frame while not connected to a CAN network");
            }
            else
            {
                FrameSent(frame);
            }

            transmissionOccurredFlags[bufferIndex].Value = true;

            if(txElement.EventFIFOControl)
            {
                StoreTxEvent(txElement);
            }
        }

        private void StoreTxEvent(TxMessageElement message)
        {
            if(txEventFIFOFull.Value)
            {
                this.DebugLog("Tried to store TxEvent while the FIFO is full");
                interruptStatusFlags[(long)Interrupts.TxEventFIFOElementLost].Value = true;
                UpdateInterrupts();
                return;
            }
            var txEvent = new TxEventElement(message, false, ReadTimestampCounter());
            messageRam.WriteBytes((long)MessageRAMOffsets.TxEventFIFO + ((long)Packet.CalculateLength<TxEventElement>() * (long)txEventFIFOPutIndex.Value), Packet.Encode(txEvent), 0, (int)Packet.CalculateLength<TxEventElement>(), this);
            interruptStatusFlags[(long)Interrupts.TxEventFIFONewEntry].Value = true;
            txEventFIFOPutIndex.Value = (txEventFIFOPutIndex.Value + 1) % 3;
            if(txEventFIFOPutIndex.Value == txEventFIFOGetIndex.Value)
            {
                txEventFIFOFull.Value = true;
                interruptStatusFlags[(long)Interrupts.TxEventFIFOFull].Value = true;
            }
            UpdateInterrupts();
        }

        private bool AcceptanceFilterMessage<T>(CANMessageFrame message, out int filterIndex, out IFilterElement matchingFilter)
        where T : IFilterElement
        {
            var filterLength = Packet.CalculateLength<T>();
            filterIndex = 0;
            matchingFilter = default(T);
            for(filterIndex = 0; filterIndex < (int)(message.ExtendedFormat ? numberOfExtendedFilters.Value : numberOfStandardFilters.Value); filterIndex++)
            {
                // Filters are parsed in order, and stop at first matching
                var filterBase = message.ExtendedFormat ? MessageRAMOffsets.ExtendedFilters : MessageRAMOffsets.StandardFilters;
                matchingFilter = Packet.Decode<T>(messageRam.ReadBytes((long)filterBase + filterIndex * filterLength, filterLength));
                if(matchingFilter.MatchesId((ushort)message.Id, (uint)extendedIDMask.Value))
                {
                    this.DebugLog("Standard message accepted by filter {0}, ({1})", filterIndex, matchingFilter);
                    return true;
                }
            }
            return false;
        }

        private bool TryStoreReceivedMessage(CANMessageFrame message, int rxFifo, int filterIndex, out uint storedIndex, bool nonMatchingFrame = false)
        {
            DebugHelper.Assert(rxFifo == 0 || rxFifo == 1);
            // Handle overflow condition
            if(rxFIFOFull[rxFifo].Value)
            {
                if((fifo0Overwrite.Value && rxFifo == 0) || (fifo1Overwrite.Value && rxFifo == 1))
                {
                    // When an overwrite happens, both the Get and Put pointers need to be incremented
                    // Put pointer is incremented later, so just increment the get pointer now
                    rxFIFOGetIndex[rxFifo].Value = (rxFIFOGetIndex[rxFifo].Value + 1) % 3;
                }
                else
                {
                    // FIFO is full, and overwrite is not set. Drop frame and set apropriate flags
                    interruptStatusFlags[rxFifo == 0 ? (long)Interrupts.RxFIFO0MessageLost : (long)Interrupts.RxFIFO1MessageLost].Value = true;
                    storedIndex = 0;
                    UpdateInterrupts();
                    return false;
                }
            }

            storedIndex = (uint)rxFIFOPutIndex[rxFifo].Value;

            var messageElement = new RxMessageElement();
            messageElement.ExtendedId = message.ExtendedFormat;
            messageElement.RemoteTransmissionRequest = message.RemoteFrame;
            if(message.ExtendedFormat)
            {
                messageElement.Identifier = message.Id;
            }
            else
            {
                messageElement.StardardId = message.Id;
            }
            messageElement.AcceptedNonMatchingFrame = nonMatchingFrame;
            messageElement.FilterIndex = (byte)filterIndex;
            messageElement.FDFormat = message.FDFormat;
            messageElement.BitRateSwitching = message.BitRateSwitch;
            messageElement.DataLength = message.Data.Length;
            messageElement.RxTimestamp = ReadTimestampCounter();
            messageElement.DataBytes = message.Data;
            // Resize the data array to always be the maximum size
            // This makes the logic around Packet encoding simpler
            Array.Resize(ref messageElement.DataBytes, 64);

            // Store the message in memory
            var fifoBaseOffset = rxFifo == 0 ? (long)MessageRAMOffsets.RxFIFO0 : (long)MessageRAMOffsets.RxFIFO1;
            messageRam.WriteBytes(fifoBaseOffset + ((long)rxFIFOPutIndex[rxFifo].Value * Packet.CalculateLength<RxMessageElement>()), Packet.Encode<RxMessageElement>(messageElement), 0, Packet.CalculateLength<RxMessageElement>(), this);
            rxFIFOPutIndex[rxFifo].Value = (rxFIFOPutIndex[rxFifo].Value + 1) % 3;
            if(rxFIFOPutIndex[rxFifo].Value == rxFIFOGetIndex[rxFifo].Value)
            {
                rxFIFOFull[rxFifo].Value = true;
                interruptStatusFlags[rxFifo == 0 ? (long)Interrupts.RxFIFO0Full : (long)Interrupts.RxFIFO1Full].Value = true;
            }
            interruptStatusFlags[rxFifo == 0 ? (long)Interrupts.RxFIFO0NewMessage : (long)Interrupts.RxFIFO1NewMessage].Value = true;
            UpdateInterrupts();
            return true;
        }

        private void UpdateInterrupts()
        {
            // Mask out disabled interrupts
            var interrupts = interruptStatusFlags.Zip(interruptEnableFlags, (st, en) => st.Value && en.Value);
            // Calculate IRQ for each interrupt group
            var rxFIFO0 = interrupts.Where((_, index) => SelectInterruptRange(Interrupts.RxFIFO0NewMessage, Interrupts.RxFIFO0MessageLost, index)).Any(x => x);
            var rxFIFO1 = interrupts.Where((_, index) => SelectInterruptRange(Interrupts.RxFIFO1NewMessage, Interrupts.RxFIFO1MessageLost, index)).Any(x => x);
            var status = interrupts.Where((_, index) => SelectInterruptRange(Interrupts.HighPriorityMessage, Interrupts.TransmissionCancellationFinished, index)).Any(x => x);
            var txFIFO = interrupts.Where((_, index) => SelectInterruptRange(Interrupts.TxFIFOEmpty, Interrupts.TxEventFIFOElementLost, index)).Any(x => x);
            var misc = interrupts.Where((_, index) => SelectInterruptRange(Interrupts.TimestampWraparound, Interrupts.TimeoutOccured, index)).Any(x => x);
            var bitError = interrupts.Where((_, index) => SelectInterruptRange(Interrupts.ErrorLoggingOverflow, Interrupts.ErrorPassive, index)).Any(x => x);
            var protocolError = interrupts.Where((_, index) => SelectInterruptRange(Interrupts.WarningStatus, Interrupts.AccessToReservedAddress, index)).Any(x => x);
            // Finally calculate each interrupt line based on group line select register and line enable flags
            Int1.Set(line1InterruptEnable.Value && (
                (rxFIFO0 && rxFIFO0MessageLineSelect.Value) ||
                (rxFIFO1 && rxFIFO1MessageLineSelect.Value) ||
                (status && statusMessageLineSelect.Value) ||
                (txFIFO && txFIFOErrorLineSelect.Value) ||
                (misc && miscInterruptLineSelect.Value) ||
                (bitError && bitAndLineErrorLineSelect.Value) ||
                (protocolError && protocolErrorLineSelect.Value)
            ));
            Int0.Set(line0InterruptEnable.Value && (
                (rxFIFO0 && !rxFIFO0MessageLineSelect.Value) ||
                (rxFIFO1 && !rxFIFO1MessageLineSelect.Value) ||
                (status && !statusMessageLineSelect.Value) ||
                (txFIFO && !txFIFOErrorLineSelect.Value) ||
                (misc && !miscInterruptLineSelect.Value) ||
                (bitError && !bitAndLineErrorLineSelect.Value) ||
                (protocolError && !protocolErrorLineSelect.Value)
            ));
        }

        private bool SelectInterruptRange(Interrupts firstInterrupt, Interrupts lastInterrupt, int index)
        {
            DebugHelper.Assert(firstInterrupt < lastInterrupt, $"{firstInterrupt} must be smaller than {lastInterrupt}");
            return ((uint)firstInterrupt <= index) && ((uint)lastInterrupt >= index);
        }

        private void OnTimeoutCounterWritten()
        {
            // In continuous mode, writing anything to the counter register resets the counter to the TOP value, and starts the counter (if it is enabled)
            // In other modes writing does nothing
            if(timeoutSelect.Value == TimeoutSelect.Continuous)
            {
                timeoutCounter.Enabled = timeoutCounterEnable.Value;
            }
        }

        private void OnTimestampWraparound()
        {
            interruptStatusFlags[(int)Interrupts.TimestampWraparound].Value = true;
            UpdateInterrupts();
        }

        private void OnTimeout()
        {
            interruptStatusFlags[(int)Interrupts.TimeoutOccured].Value = true;
            UpdateInterrupts();
        }

        private double GetCANTimeQuantum()
        {
            var dividedKernelFrequency = inputClockDivider.Value == 0 ? kernelFrequency : kernelFrequency / (inputClockDivider.Value * 2);
            return ((uint)bitRatePrescaler.Value + 1) / (double)dividedKernelFrequency;
        }

        private double GetBitTime()
        {
            return (timeSegmentBeforeSamplePoint.Value + 3 + timeSegmentAfterSamplePoint.Value) * GetCANTimeQuantum();
        }

        private uint GetTCPFrequency()
        {
            // The timestamp and timeout counters time unit is the bit time * the prescaler bits in `FDCAN_TSCC`
            return (uint)(1 / (GetBitTime() * (timestampCounterPrescaler.Value + 1)));
        }

        private void UpdateTimerConfiguration()
        {
            // Called whenever some prescaler or frequency changes
            timestampCounter.Frequency = GetTCPFrequency();
            timeoutCounter.Frequency = GetTCPFrequency();
            if(timestampMode.Value == TimestampMode.TCP)
            {
                timestampCounter.Enabled = true;
            }
            else
            {
                timestampCounter.Enabled = false;
            }
        }

        private void ResetTimestampCounter()
        {
            // Reset only has meaning in TCP mode
            if(timestampMode.Value == TimestampMode.TCP)
            {
                timestampCounter.Reset();
            }
        }

        private ushort ReadTimestampCounter()
        {
            switch(timestampMode.Value)
            {
            case TimestampMode.AlwaysZero:
                return 0;
            case TimestampMode.TCP:
                return (ushort)timestampCounter.Value;
            case TimestampMode.TIM3:
                throw new NotImplementedException("External timer mode not implemented");
            case TimestampMode.AlwaysZeroAlternative:
                return 0;
            default:
                throw new UnreachableException("Unexpected enum variant");
            }
        }

        private void CCEBitChangedCallback(bool value)
        {
            if(value)
            {
                timeoutCounter.Limit = timeoutPeriod.Value;
                timeoutCounter.Reset();
                // FDCAN_HPMS: High priority message status
                RegistersCollection.ResetRegister((long)Registers.HighPriorityMessageStatusRegister);
                // FDCAN_RXF0S: Rx FIFO 0 status
                RegistersCollection.ResetRegister((long)Registers.RxFIFO0StatusRegister);
                interruptStatusFlags[(long)Interrupts.RxFIFO0MessageLost].Value = false;
                // FDCAN_RXF1S: Rx FIFO 1 status
                RegistersCollection.ResetRegister((long)Registers.RxFIFO1StatusRegister);
                interruptStatusFlags[(long)Interrupts.RxFIFO1MessageLost].Value = false;
                // FDCAN_TXFQS: Tx FIFO/queue status
                RegistersCollection.ResetRegister((long)Registers.TxFIFOStatusRegister);
                // FDCAN_TXBRP: Tx buffer request pending
                RegistersCollection.ResetRegister((long)Registers.TxBufferRequestPendingRegister);
                // FDCAN_TXBTO: Tx buffer transmission occurred
                RegistersCollection.ResetRegister((long)Registers.TxBufferTransmissionOccuredRegister);
                // FDCAN_TXBCF: Tx buffer cancellation finished
                RegistersCollection.ResetRegister((long)Registers.TxBufferCancellationFinishedRegister);
                // FDCAN_TXEFS: Tx event FIFO status
                RegistersCollection.ResetRegister((long)Registers.TxEventFIFOStatusRegister);
                interruptStatusFlags[(long)Interrupts.TxEventFIFOElementLost].Value = false;
                UpdateInterrupts();
            }
        }

        private void InitBitChangedCallback(bool value)
        {
            if(!value)
            {
                // When the init bit is cleared the CCE bit is also cleared
                configurationChangeEnable.Value = false;
                // In continuous mode the timeout counter is started when Init is cleared
                if(timeoutSelect.Value == TimeoutSelect.Continuous)
                {
                    timeoutCounter.Enabled = timeoutCounterEnable.Value;
                }
                // If any tx requests where started while in bus-off state, execute them here
                if(requestPendingFlags.Value != 0)
                {
                    BitHelper.ForeachActiveBit(requestPendingFlags.Value, SendBuffer);
                    requestPendingFlags.Value = 0;
                    interruptStatusFlags[(long)Interrupts.TxFIFOEmpty].Value = true;
                    interruptStatusFlags[(long)Interrupts.TransmissionCompleted].Value = true;
                    UpdateInterrupts();
                }
            }
        }

        private void RequestClockStopCallback(bool value)
        {
            if(value && !initFlag.Value)
            {
                // Transfers are instant, so no need to wait
                initFlag.Value = true;
                initFlag.ChangeCallback(false, true);
                clockStopAcknowledge.Value = true;
            }
        }

        private Func<bool> ConfigChangeEnabled => () => initFlag.Value && configurationChangeEnable.Value;

        private IFlagRegisterField protocolExceptionHandlingDisable;
        private IFlagRegisterField fdOperationEnable;
        private IFlagRegisterField clockStopRequested;
        private IFlagRegisterField clockStopAcknowledge;
        private IFlagRegisterField configurationChangeEnable;
        private IFlagRegisterField initFlag;
        private IValueRegisterField timestampCounterPrescaler;
        private IEnumRegisterField<TimestampMode> timestampMode;
        private IValueRegisterField ramWatchdogResetValue;
        private IValueRegisterField timeSegmentAfterSamplePoint;
        private IValueRegisterField bitRatePrescaler;
        private IValueRegisterField timeSegmentBeforeSamplePoint;
        private IValueRegisterField timeoutPeriod;
        private IEnumRegisterField<TimeoutSelect> timeoutSelect;
        private IFlagRegisterField timeoutCounterEnable;
        private IValueRegisterField canErrorLogCounter;
        private IFlagRegisterField reciveErrorPassiveFlag;
        private IValueRegisterField reciveErrorCounter;
        private IValueRegisterField transmitErrorCount;
        private IFlagRegisterField protocolExceptionEventFlag;
        private IFlagRegisterField receivedFDCANMessageFlag;
        private IFlagRegisterField receivedBRSFlag;
        private IFlagRegisterField receivedESIFlag;
        private IValueRegisterField dataLastErrorCode;
        private IFlagRegisterField busOffStatus;
        private IFlagRegisterField errorPassiveFlag;
        private IValueRegisterField lastErrorCode;
        private IValueRegisterField transmitterDelayCompensationEffect;
        private IValueRegisterField transmitterDelayCompensationFilter;
        private IFlagRegisterField[] interruptStatusFlags;
        private IFlagRegisterField[] interruptEnableFlags;
        private IFlagRegisterField protocolErrorLineSelect;
        private IFlagRegisterField bitAndLineErrorLineSelect;
        private IFlagRegisterField miscInterruptLineSelect;
        private IFlagRegisterField txFIFOErrorLineSelect;
        private IFlagRegisterField statusMessageLineSelect;
        private IFlagRegisterField rxFIFO1MessageLineSelect;
        private IFlagRegisterField rxFIFO0MessageLineSelect;
        private IFlagRegisterField line1InterruptEnable;
        private IFlagRegisterField line0InterruptEnable;
        private IValueRegisterField numberOfExtendedFilters;
        private IValueRegisterField numberOfStandardFilters;
        private IFlagRegisterField fifo0Overwrite;
        private IFlagRegisterField fifo1Overwrite;
        private IEnumRegisterField<AcceptMode> acceptNonMatchingFramesStandard;
        private IEnumRegisterField<AcceptMode> acceptNonMatchingFramesExtended;
        private IFlagRegisterField rejectRemoteFramesStandard;
        private IFlagRegisterField rejectRemoteFramesExtended;
        private IValueRegisterField extendedIDMask;
        private IFlagRegisterField lastHighPrioFilterList;
        private IValueRegisterField lastHighPrioFilterIndex;
        private IEnumRegisterField<MessageStorageIndicator> lastHighPrioMessageStorage;
        private IValueRegisterField lastHighPrioBufferIndex;
        private IFlagRegisterField txFIFOQueueModeFlag;
        private IFlagRegisterField[] transmissionOccurredFlags;
        private IFlagRegisterField[] transmissionInterruptEnableFlags;
        private IFlagRegisterField txEventFIFOFull;
        private IValueRegisterField txEventFIFOPutIndex;
        private IValueRegisterField txEventFIFOGetIndex;
        private IValueRegisterField inputClockDivider;
        private IFlagRegisterField bitRateSwitching;
        private IValueRegisterField requestPendingFlags;
        private readonly IFlagRegisterField[] rxFIFOFull;
        private readonly IValueRegisterField[] rxFIFOPutIndex;
        private readonly IValueRegisterField[] rxFIFOGetIndex;
        private readonly ArrayMemory messageRam;
        private readonly LimitTimer timestampCounter;
        private readonly LimitTimer timeoutCounter;
        private readonly uint kernelFrequency;

        private const int MessageBufferHeaderSize = 8;
        private const int MessageBufferMaxDataBytes = 64;
        private const int MessageBufferElementSize = MessageBufferHeaderSize + MessageBufferMaxDataBytes;

        [LeastSignificantByteFirst]
        private class RxMessageElement : MessageElement
        {
            [PacketField, Offset(doubleWords: 1, bits:  31), Width(bits: 1)]
            public bool AcceptedNonMatchingFrame;
            [PacketField, Offset(doubleWords: 1, bits:  24), Width(bits: 7)]
            public byte FilterIndex;
            [PacketField, Offset(doubleWords: 1, bits:  0), Width(bits: 16)]
            public ushort RxTimestamp;
        }

        [LeastSignificantByteFirst]
        private class TxMessageElement : MessageElement
        {
            public static explicit operator CANMessageFrame(TxMessageElement element)
            {
                return CANMessageFrame.CreateWithExtendedId(
                    element.Identifier,
                    element.DataBytes.Take(element.DataLength).ToArray(),
                    element.ExtendedId, element.RemoteTransmissionRequest,
                    element.FDFormat,
                    element.BitRateSwitching);
            }

#pragma warning disable CS0649 // Fields are assigned via `Packet.Decode()`
            [PacketField, Offset(doubleWords: 1, bits:  24), Width(bits: 8)]
            public byte MessageMarker;
            [PacketField, Offset(doubleWords: 1, bits:  23), Width(bits: 1)]
            public bool EventFIFOControl;
#pragma warning restore CS0649
        }

        [LeastSignificantByteFirst]
        private class MessageElement
        {
            public uint StardardId
            {
                // Standard length IDs are stored in bits 18-29 of the Identifier field
                get => BitHelper.GetValue(Identifier, 18, 11);
                set
                {
                    Identifier = value << 18;
                }
            }

            public int DataLength
            {
                get
                {
                    if(DataLengthCode <= 8)
                    {
                        return DataLengthCode;
                    }
                    if(FDFormat)
                    {
                        switch(DataLengthCode)
                        {
                        // Mappings taken directly from TRM
                        case 9:
                            return 12;
                        case 10:
                            return 16;
                        case 11:
                            return 20;
                        case 12:
                            return 24;
                        case 13:
                            return 32;
                        case 14:
                            return 48;
                        case 15:
                            return 64;
                        default:
                            throw new UnreachableException("Impossible DataLengthCode");
                        }
                    }
                    else
                    {
                        // All other encodings mean 8 in classic mode
                        return 8;
                    }
                }

                set
                {
                    if(!(0 <= value && value <= 64))
                    {
                        throw new ArgumentOutOfRangeException("DataLenght must be a value 0-64");
                    }
                    if(value <= 8)
                    {
                        DataLengthCode = (byte)value;
                    }
                    else if(FDFormat)
                    {
                        switch(value)
                        {
                        case 12:
                            DataLengthCode = 9;
                            break;
                        case 16:
                            DataLengthCode = 10;
                            break;
                        case 20:
                            DataLengthCode = 11;
                            break;
                        case 24:
                            DataLengthCode = 12;
                            break;
                        case 32:
                            DataLengthCode = 13;
                            break;
                        case 48:
                            DataLengthCode = 14;
                            break;
                        case 64:
                            DataLengthCode = 15;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Invalid DataLegth for FDCan ({value})");
                        }
                    }
                    else
                    {
                        DataLengthCode = 8;
                    }
                }
            }

#pragma warning disable CS0649 // Field is assigned via `Packet.Decode()`
            [PacketField, Offset(doubleWords: 0, bits:  31), Width(bits: 1)]
            public bool ErrorStateIndicator;
#pragma warning restore CS0649
            [PacketField, Offset(doubleWords: 0, bits:  30), Width(bits: 1)]
            public bool ExtendedId;
            [PacketField, Offset(doubleWords: 0, bits:  29), Width(bits: 1)]
            public bool RemoteTransmissionRequest;
            [PacketField, Offset(doubleWords: 0, bits:  0), Width(bits: 29)]
            public uint Identifier;
            [PacketField, Offset(doubleWords: 1, bits:  21), Width(bits: 1)]
            public bool FDFormat;
            [PacketField, Offset(doubleWords: 1, bits:  20), Width(bits: 1)]
            public bool BitRateSwitching;
            [PacketField, Offset(doubleWords: 1, bits:  16), Width(bits: 4)]
            public byte DataLengthCode;
            [PacketField, Offset(doubleWords: 2, bits:  0), Width(bytes: 64)]
            public byte[] DataBytes;
        }

        [LeastSignificantByteFirst]
        private class TxEventElement
        {
            public TxEventElement(TxMessageElement message, bool wasCancelled, ushort timestamp)
            {
                this.ErrorStateIndicator = message.ErrorStateIndicator;
                this.ExtendedId = message.ExtendedId;
                this.RemoteTransmissionRequest = message.RemoteTransmissionRequest;
                this.Identifier = message.Identifier;
                this.MessageMarker = message.MessageMarker;
                this.Type = wasCancelled ? EventType.TransmissionInSpiteOfCancellation : EventType.TxEvent;
                this.FDFormat = message.FDFormat;
                this.BitRateSwitching = message.BitRateSwitching;
                this.DataLenghtCode = message.DataLengthCode;
                this.TxTimestamp = timestamp;
            }

            [PacketField, Offset(doubleWords: 0, bits:  31), Width(bits: 1)]
            public bool ErrorStateIndicator;
            [PacketField, Offset(doubleWords: 0, bits:  30), Width(bits: 1)]
            public bool ExtendedId;
            [PacketField, Offset(doubleWords: 0, bits:  29), Width(bits: 1)]
            public bool RemoteTransmissionRequest;
            [PacketField, Offset(doubleWords: 0, bits:  0), Width(bits: 29)]
            public uint Identifier;
            [PacketField, Offset(doubleWords: 1, bits:  24), Width(bits: 8)]
            public byte MessageMarker;
            [PacketField, Offset(doubleWords: 1, bits:  22), Width(bits: 2)]
            public EventType Type;
            [PacketField, Offset(doubleWords: 1, bits:  21), Width(bits: 1)]
            public bool FDFormat;
            [PacketField, Offset(doubleWords: 1, bits:  20), Width(bits: 1)]
            public bool BitRateSwitching;
            [PacketField, Offset(doubleWords: 1, bits:  16), Width(bits: 4)]
            public byte DataLenghtCode;
            [PacketField, Offset(doubleWords: 1, bits:  0), Width(bits: 16)]
            public ushort TxTimestamp;

            public enum EventType
            {
                Reserved = 0b00,
                TxEvent = 0b01,
                TransmissionInSpiteOfCancellation = 0b10,
                ReservedAlternative = 0b11
            }
        }

        [LeastSignificantByteFirst]
        private class StandardFilterElement : IFilterElement
        {
            public bool MatchesId(uint id, uint _)
            {
                if(Config == FilterElementConfig.Disabled || Config == FilterElementConfig.DisabledAlternative)
                {
                    return false;
                }
                switch(this.FilterType)
                {
                case StandardFilterType.RangeFilter:
                    return (FilterId1 <= id && id <= FilterId2);
                case StandardFilterType.DualIDFilter:
                    return (id == FilterId1 || id == FilterId2);
                case StandardFilterType.ClassicFilter:
                    return ((id & FilterId2) == (FilterId1 & FilterId2));
                case StandardFilterType.Disabled:
                    return false;
                default:
                    throw new UnreachableException($"Unexpected StandardFilterType variant {this.FilterType}");
                }
            }

#pragma warning disable CS0649 // Fields are assigned via `Packet.Decode()`
            [PacketField, Offset(doubleWords: 0, bits:  29), Width(bits: 2)]
            public StandardFilterType FilterType;
            [PacketField, Offset(doubleWords: 0, bits: 27), Width(bits:3)]
            public FilterElementConfig Config;
            [PacketField, Offset(doubleWords: 0, bits: 16), Width(bits:11)]
            public ushort FilterId1;
            [PacketField, Offset(doubleWords: 0, bits: 0), Width(bits:11)]
            public ushort FilterId2;
#pragma warning restore CS0649

            public enum StandardFilterType
            {
                RangeFilter = 0b00,
                DualIDFilter = 0b01,
                ClassicFilter = 0b10,
                Disabled = 0b11
            }
        }

        [LeastSignificantByteFirst]
        private class ExtendedFilterElement : IFilterElement
        {
            public bool MatchesId(uint id, uint xidMask)
            {
                if(Config == FilterElementConfig.Disabled || Config == FilterElementConfig.DisabledAlternative)
                {
                    return false;
                }
                switch(this.FilterType)
                {
                case ExtendedFilterType.RangeFilter:
                    id = id & xidMask;
                    return (FilterId1 <= id && id <= FilterId2);
                case ExtendedFilterType.DualIDFilter:
                    return (id == FilterId1 || id == FilterId2);
                case ExtendedFilterType.ClassicFilter:
                    return ((id & FilterId2) == (FilterId1 & FilterId2));
                case ExtendedFilterType.RangeFilterWIthoutXIDAM:
                    return (FilterId1 <= id && id <= FilterId2);
                default:
                    throw new UnreachableException("Unexpected ExtendedFilterType variant");
                }
            }

#pragma warning disable CS0649 // Fields are assigned via `Packet.Decode()`
            [PacketField, Offset(doubleWords: 0, bits: 29), Width(bits:3)]
            public FilterElementConfig Config;
            [PacketField, Offset(doubleWords: 0, bits: 0), Width(bits:29)]
            public uint FilterId1;
            [PacketField, Offset(doubleWords: 1, bits: 30), Width(bits:2)]
            public ExtendedFilterType FilterType;
            [PacketField, Offset(doubleWords: 1, bits: 0), Width(bits:29)]
            public uint FilterId2;
#pragma warning restore CS0649

            public enum ExtendedFilterType
            {
                RangeFilter = 0b00,
                DualIDFilter = 0b01,
                ClassicFilter = 0b10,
                RangeFilterWIthoutXIDAM = 0b11,
            }
        }

        private interface IFilterElement
        {
            bool MatchesId(uint id, uint xidMask);
        }

        private enum FilterElementConfig
        {
            Disabled = 0b000,
            StoreInRxFIFO0 = 0b001,
            StoreInRxFIFO1 = 0b010,
            Reject = 0b011,
            SetPriority = 0b100,
            SetPriorityAndStoreInRxFIFO0 = 0b101,
            SetPriorityAndStoreInRxFIFO1 = 0b110,
            DisabledAlternative = 0b111
        }

        private enum TimestampMode
        {
            AlwaysZero = 0b00,
            TCP = 0b01,
            TIM3 = 0b10,
            AlwaysZeroAlternative = 0b11
        }

        private enum TimeoutSelect
        {
            Continuous = 0b00,
            TxFIFO = 0b01,
            RxFIFO0 = 0b10,
            RxFIFO1 = 0b11
        }

        private enum AcceptMode
        {
            AcceptInRxFIFO0 = 0b00,
            AcceptInRxFIFO1 = 0b01,
            Reject = 0b10,
            RejectAlternative = 0b11
        }

        private enum MessageStorageIndicator
        {
            NoFIFOSelected = 0b00,
            FIFOOverrun = 0b01,
            MessageStoredInFIFO0 = 0b10,
            MessageStoredInFIFO1 = 0b11
        }

        private enum MessageRAMOffsets
        {
            StandardFilters = 0x0,
            ExtendedFilters = 0x70,
            RxFIFO0 = 0xB0,
            RxFIFO1 = 0x188,
            TxEventFIFO = 0x260,
            TxBuffers = 0x278,
        }

        private enum Interrupts
        {
            RxFIFO0NewMessage = 0,
            RxFIFO0Full = 1,
            RxFIFO0MessageLost = 2,
            RxFIFO1NewMessage = 3,
            RxFIFO1Full = 4,
            RxFIFO1MessageLost = 5,
            HighPriorityMessage = 6,
            TransmissionCompleted = 7,
            TransmissionCancellationFinished = 8,
            TxFIFOEmpty = 9,
            TxEventFIFONewEntry = 10,
            TxEventFIFOFull = 11,
            TxEventFIFOElementLost = 12,
            TimestampWraparound = 13,
            MessageRAMAccessFailure = 14,
            TimeoutOccured = 15,
            ErrorLoggingOverflow = 16,
            ErrorPassive = 17,
            WarningStatus = 18,
            BusOffStatus = 19,
            WatchdogInterrupt = 20,
            ProtocolErrorInArbitrationPhase = 21,
            ProtocolErrorInDataPhase = 22,
            AccessToReservedAddress = 23
        }

        private enum Registers
        {
            CoreReleaseRegister = 0x0,
            EndianRegister = 0x4,
            DataBitTimingAndPrescalerRegister = 0xC,
            TestRegister = 0x10,
            RAMWatchdogRegister = 0x14,
            CCControlRegister = 0x18,
            NominalBitTimingAndPrescalerRegister = 0x1C,
            TimestampCounterConfigRegister = 0x20,
            TimestampCounterValueRegister = 0x24,
            TimeoutCounterConfigRegister = 0x28,
            TimeoutCounterValueRegister = 0x2C,
            ErrorCounterRegister = 0x40,
            ProtocolStatusRegister = 0x44,
            TransmitterDelayCompensationRegister = 0x48,
            InterruptRegister = 0x50,
            InterruptEnableRegister = 0x54,
            InterruptLineSelectRegister = 0x58,
            InterruptLineEnableRegister = 0x5C,
            GlobalFilterConfigRegister = 0x80,
            ExtendedIDAndMaskRegister = 0x84,
            HighPriorityMessageStatusRegister = 0x88,
            RxFIFO0StatusRegister = 0x90,
            RxFIFO0AcknowlegeRegister = 0x94,
            RxFIFO1StatusRegister = 0x98,
            RxFIFO1AcknowlegeRegister = 0x9C,
            TxBufferConfigurationRegister = 0xC0,
            TxFIFOStatusRegister = 0xC4,
            TxBufferRequestPendingRegister = 0xC8,
            TxBufferAddRequestRegister = 0xCC,
            TxBufferCancellationRequestRegister = 0xD0,
            TxBufferTransmissionOccuredRegister = 0xD4,
            TxBufferCancellationFinishedRegister = 0xD8,
            TxBufferTransmissionInterruptEnableRegister = 0xDC,
            TxBufferCancellationFinishedInterruptEnableRegister = 0xE0,
            TxEventFIFOStatusRegister = 0xE4,
            TxEventFIFOAcknowledgeRegister = 0xE8,
            ConfigClockDividerRegister = 0x100,
        }
    }
}
