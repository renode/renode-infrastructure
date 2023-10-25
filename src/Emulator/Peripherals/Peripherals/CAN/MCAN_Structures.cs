//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Hooks;
using Antmicro.Renode.Utilities.Packets;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.CAN
{
    public partial class MCAN
    {
        private void BuildStructuredViews()
        {
            rxFIFO0 = new RxFIFOView
            {
                StartAddress = rv.RxFIFO0Configuration.RxFIFO0StartAddress,
                Size = rv.RxFIFO0Configuration.RxFIFO0Size,
                Watermark = rv.RxFIFO0Configuration.RxFIFO0Watermark,
                OperationMode = rv.RxFIFO0Configuration.FIFO0OperationMode,
                GetIndexRaw = rv.RxFIFO0Status.RxFIFO0GetIndex,
                PutIndexRaw = rv.RxFIFO0Status.RxFIFO0PutIndex,
                Full = rv.RxFIFO0Status.RxFIFO0Full,
                MessageLost = rv.RxFIFO0Status.RxFIFO0MessageLost,
                AcknowledgeIndex = rv.RxFIFO0Acknowledge.RxFIFO0AcknowledgeIndex,
                DataFieldSize = rv.RxBufferFIFOElementSizeConfiguration.RxFIFO0DataFieldSize,
                InterruptMessageLost = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO0MessageLost],
                InterruptWatermarkReached = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO0WatermarkReached],
                InterruptFull = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO0Full],
                InterruptNewMessage = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO0NewMessage]
            };

            rxFIFO1 = new RxFIFOView
            {
                StartAddress = rv.RxFIFO1Configuration.RxFIFO1StartAddress,
                Size = rv.RxFIFO1Configuration.RxFIFO1Size,
                Watermark = rv.RxFIFO1Configuration.RxFIFO1Watermark,
                OperationMode = rv.RxFIFO1Configuration.FIFO1OperationMode,
                GetIndexRaw = rv.RxFIFO1Status.RxFIFO1GetIndex,
                PutIndexRaw = rv.RxFIFO1Status.RxFIFO1PutIndex,
                Full = rv.RxFIFO1Status.RxFIFO1Full,
                MessageLost = rv.RxFIFO1Status.RxFIFO1MessageLost,
                AcknowledgeIndex = rv.RxFIFO1Acknowledge.RxFIFO1AcknowledgeIndex,
                DataFieldSize = rv.RxBufferFIFOElementSizeConfiguration.RxFIFO1DataFieldSize,
                InterruptMessageLost = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO1MessageLost],
                InterruptWatermarkReached = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO1WatermarkReached],
                InterruptFull = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO1Full],
                InterruptNewMessage = rv.InterruptRegister.InterruptFlags[(int)Interrupt.RxFIFO1NewMessage]
            };

            filterConfigurationStandard = new FilterConfigurationView
            {
                RejectRemoteFrames = rv.GlobalFilterConfiguration.RejectRemoteFramesStandard,
                AcceptNonMatchingFrames = rv.GlobalFilterConfiguration.AcceptNonMatchingFramesStandard,
                FilterListStartAddress = rv.StandardIDFilterConfiguration.FilterListStandardStartAddress,
                ListSize = rv.StandardIDFilterConfiguration.ListSizeStandard
            };

            filterConfigurationExtended = new FilterConfigurationView
            {
                RejectRemoteFrames = rv.GlobalFilterConfiguration.RejectRemoteFramesExtended,
                AcceptNonMatchingFrames = rv.GlobalFilterConfiguration.AcceptNonMatchingFramesExtended,
                FilterListStartAddress = rv.ExtendedIDFilterConfiguration.FilterListExtendedStartAddress,
                ListSize = rv.ExtendedIDFilterConfiguration.ListSizeExtended
            };

            rxBuffer = new RxBufferView
            {
                StartAddress = rv.RxBufferConfiguration.RxBufferStartAddress,
                DataFieldSize = rv.RxBufferFIFOElementSizeConfiguration.RxBufferDataFieldSize
            };

            txBuffers = new TxBufferView
            {
                StartAddress = rv.TxBufferConfiguration.TxBuffersStartAddress,
                NumberOfDedicatedTxBuffers = rv.TxBufferConfiguration.NumberOfDedicatedTxBuffers,
                TxFIFOQueueSize = rv.TxBufferConfiguration.TxFIFOQueueSize,
                TxFIFOQueueMode = rv.TxBufferConfiguration.TxFIFOQueueMode,
                DataFieldSize = rv.TxBufferElementSizeConfiguration.TxBufferDataFieldSize
            };

            txFIFOQueue = new TxFIFOQueueView
            {
                Offset = rv.TxBufferConfiguration.NumberOfDedicatedTxBuffers,
                Size = rv.TxBufferConfiguration.TxFIFOQueueSize,
                QueueMode = rv.TxBufferConfiguration.TxFIFOQueueMode,
                GetIndexRaw = rv.TxFIFOQueueStatus.TxFIFOGetIndex,
                PutIndexRaw = rv.TxFIFOQueueStatus.TxFIFOQueuePutIndex,
                FullRaw = rv.TxFIFOQueueStatus.TxFIFOQueueFull,
                TransmissionRequestPendingFlags = rv.TxBufferRequestPending.TransmissionRequestPendingFlags
            };

            txEventFIFO = new TxEventFIFOView
            {
                StartAddress = rv.TxEventFIFOConfiguration.EventFIFOStartAddress,
                Size = rv.TxEventFIFOConfiguration.EventFIFOSize,
                Watermark = rv.TxEventFIFOConfiguration.EventFIFOWatermark,
                GetIndexRaw = rv.TxEventFIFOStatus.EventFIFOGetIndex,
                PutIndexRaw = rv.TxEventFIFOStatus.EventFIFOPutIndex,
                Full = rv.TxEventFIFOStatus.EventFIFOFull,
                ElementLost = rv.TxEventFIFOStatus.TxEventFIFOElementLost,
                AcknowledgeIndex = rv.TxEventFIFOAcknowledge.EventFIFOAcknowledgeIndex,
                InterruptNewEntry = rv.InterruptRegister.InterruptFlags[(int)Interrupt.TxEventFIFONewEntry],
                InterruptWatermarkReached = rv.InterruptRegister.InterruptFlags[(int)Interrupt.TxEventFIFOWatermarkReached],
                InterruptFull = rv.InterruptRegister.InterruptFlags[(int)Interrupt.TxEventFIFOFull],
                InterruptElementLost = rv.InterruptRegister.InterruptFlags[(int)Interrupt.TxEventFIFOElementLost],
            };
        }

        private void BuildRegisterMapView()
        {
            rv = new RegisterMapView
            {
                TestRegister = new TestRegister(),
                CCControlRegister = new CCControlRegister(),
                ProtocolStatusRegister = new ProtocolStatusRegister(),
                InterruptRegister = new InterruptRegister(),
                InterruptEnable = new InterruptEnable(),
                InterruptLineSelect = new InterruptLineSelect(),
                InterruptLineEnable = new InterruptLineEnable(),
                GlobalFilterConfiguration = new GlobalFilterConfiguration(),
                StandardIDFilterConfiguration = new StandardIDFilterConfiguration(),
                ExtendedIDFilterConfiguration = new ExtendedIDFilterConfiguration(),
                ExtendedIdANDMask = new ExtendedIdANDMask(),
                HighPriorityMessageStatus = new HighPriorityMessageStatus(),
                NewData1 = new NewData1(),
                NewData2 = new NewData2(),
                RxFIFO0Configuration = new RxFIFO0Configuration(),
                RxFIFO0Status = new RxFIFO0Status(),
                RxFIFO0Acknowledge = new RxFIFO0Acknowledge(),
                RxBufferConfiguration = new RxBufferConfiguration(),
                RxFIFO1Configuration = new RxFIFO1Configuration(),
                RxFIFO1Status = new RxFIFO1Status(),
                RxFIFO1Acknowledge = new RxFIFO1Acknowledge(),
                RxBufferFIFOElementSizeConfiguration = new RxBufferFIFOElementSizeConfiguration(),
                TxBufferConfiguration = new TxBufferConfiguration(),
                TxFIFOQueueStatus = new TxFIFOQueueStatus(),
                TxBufferElementSizeConfiguration = new TxBufferElementSizeConfiguration(),
                TxBufferRequestPending = new TxBufferRequestPending(),
                TxBufferAddRequest = new TxBufferAddRequest(),
                TxBufferCancellationRequest = new TxBufferCancellationRequest(),
                TxBufferTransmissionOccurred = new TxBufferTransmissionOccurred(),
                TxBufferCancellationFinished = new TxBufferCancellationFinished(),
                TxBufferTransmissionInterruptEnable = new TxBufferTransmissionInterruptEnable(),
                TxBufferCancellationFinishedInterruptEnable = new TxBufferCancellationFinishedInterruptEnable(),
                TxEventFIFOConfiguration = new TxEventFIFOConfiguration(),
                TxEventFIFOStatus = new TxEventFIFOStatus(),
                TxEventFIFOAcknowledge = new TxEventFIFOAcknowledge()
            };
        }

        public long Size => 0x400;

        public GPIO Line0 { get; private set; }
        public GPIO Line1 { get; private set; }
        public GPIO Calibration { get; private set; }
        public event Action<CANMessageFrame> FrameSent;

        private bool IsProtectedWrite => rv.CCControlRegister.ControlFields[(int)Control.Initialization].Value && rv.CCControlRegister.ControlFields[(int)Control.ConfigurationChangeEnable].Value;

        private readonly IMachine machine;
        private IMultibyteWritePeripheral messageRAM;
        private DoubleWordRegisterCollection registers;
        private RegisterMapView rv;
        private RxFIFOView rxFIFO0;
        private RxFIFOView rxFIFO1;
        private FilterConfigurationView filterConfigurationStandard;
        private FilterConfigurationView filterConfigurationExtended;
        private RxBufferView rxBuffer;
        private TxBufferView txBuffers;
        private TxFIFOQueueView txFIFOQueue;
        private TxEventFIFOView txEventFIFO;

        private const int ExtendedFilterSizeInBytes = 8;
        private const int StandardFilterSizeInBytes = 4;
        private const int BufferElementHeaderSizeInBytes = 8;
        private const int TxEventFIFOElementSizeInBytes = 8;
        private const int NumberOfRxFIFOs = 2;
        private const byte PaddingByte = 0xcc;

        private static readonly IReadOnlyDictionary<ulong, int> DataFieldSizeToBytesCountMap = new Dictionary<ulong, int>
        {
            {0b000, 8},
            {0b001, 12},
            {0b010, 16},
            {0b011, 20},
            {0b100, 24},
            {0b101, 32},
            {0b110, 48},
            {0b111, 64},
        };

        private static readonly IReadOnlyDictionary<int, byte> FDBytesCountToDataLengthCodeMap = new Dictionary<int, byte>
        {
            {12, 9},
            {16, 10},
            {20, 11},
            {24, 12},
            {32, 13},
            {48, 14},
            {64, 15},
        };

        private static readonly IReadOnlyDictionary<int, byte> DataLengthCodeToFDBytesCountMap = new Dictionary<int, byte>
        {
            {9, 12},
            {10, 16},
            {11, 20},
            {12, 24},
            {13, 32},
            {14, 48},
            {15, 64},
        };

        private enum TxScanModeInternal
        {
            Dedicated,
            FIFO,
            Queue,
            MixedDedicatedFIFO,
            MixedDedicatedQueue,
        }

        private enum RxBufferOrDebugDestination
        {
            StoreInRxBuffer = 0b00,
            DebugMessageA = 0b01,
            DebugMessageB = 0b10,
            DebugMessageC = 0b11
        }

        private enum LastErrorCode
        {
            NoError = 0,
            StuffError = 1,
            FormError = 2,
            AckError = 3,
            Bit1Error = 4,
            Bit0Error = 5,
            CRCError = 6,
            NoChange = 7
        }

        private enum Activity
        {
            Synchronizing = 0b00,
            Idle = 0b01,
            Receiver = 0b10,
            Transmitter = 0b11
        }

        private enum FIFOOperationMode
        {
            Blocking = 0,
            Overwrite = 1
        }

        private enum DebugMessageStatus
        {
            IdleState = 0b00,
            DebugMessageA = 0b01,
            DebugMessageAB = 0b10,
            DebugMessageABC = 0b11
        }

        private enum NonMatchingFrameTarget
        {
            AcceptInRxFIFO0 = 0b00,
            AcceptInRxFIFO1 = 0b01,
            Reject_ = 0b10,
            Reject__ = 0b11
        }

        private enum MessageStorageIndicator
        {
            NoFIFOselected = 0b00,
            FIFOMessageLost = 0b01,
            MessageInFIFO0 = 0b10,
            MessageInFIFO1 = 0b11,
        }

        private enum Interrupt
        {
            RxFIFO0NewMessage = 0,                                  // RF0N
            RxFIFO0WatermarkReached = 1,                            // RF0W
            RxFIFO0Full = 2,                                        // RF0F
            RxFIFO0MessageLost = 3,                                 // RF0L
            RxFIFO1NewMessage = 4,                                  // RF1N
            RxFIFO1WatermarkReached = 5,                            // RF1W
            RxFIFO1Full = 6,                                        // RF1F
            RxFIFO1MessageLost = 7,                                 // RF1L
            HighPriorityMessage = 8,                                // HPM
            TransmissionCompleted = 9,                              // TC
            TransmissionCancellationFinished = 10,                  // TCF
            TxFIFOEmpty = 11,                                       // TFE
            TxEventFIFONewEntry = 12,                               // TEFN
            TxEventFIFOWatermarkReached = 13,                       // TEFW
            TxEventFIFOFull = 14,                                   // TEFF
            TxEventFIFOElementLost = 15,                            // TEFL
            TimestampWraparound = 16,                               // TSW
            MessageRAMAccessFailure = 17,                           // MRAF
            TimeoutOccurred = 18,                                   // TOO
            MessageStoredToDedicatedRxBuffer = 19,                  // DRX
            BitErrorCorrected = 20,                                 // BEC
            BitErrorUncorrected = 21,                               // BEU
            ErrorLoggingOverflow = 22,                              // ELO
            ErrorPassive = 23,                                      // EP
            WarningStatus = 24,                                     // EW
            BusOffStatus = 25,                                      // BO
            WatchdogInterrupt = 26,                                 // WDI
            ProtocolErrorInArbitrationPhase = 27,                   // PEA
            ProtocolErrorInDataPhase = 28,                          // PED
            AccessToReservedAddress = 29                            // ARA
        }

        private enum Control
        {
            Initialization = 0,                                     // INIT
            ConfigurationChangeEnable = 1,                          // CCE
            RestrictedOperationMode = 2,                            // ASM
            ClockStopAcknowledge = 3,                               // CSA
            ClockStopRequest = 4,                                   // CSR
            BusMonitoringMode = 5,                                  // MON
            DisableAutomaticRetransmission = 6,                     // DAR
            TestModeEnable = 7,                                     // TEST
            FDOperationEnable = 8,                                  // FDOE
            BitRateSwitchEnable = 9,                                // BRSE
            UseTimestampingUnit = 10,                               // UTSU
            WideMessageMarker = 11,                                 // WMM
            ProtocolExceptionHandlingDisable = 12,                  // PXHD
            EdgeFilteringDuringBusIntegration = 13,                 // EFBI
            TransmitPause = 14,                                     // TXP
            NonISOOperation = 15                                    // NISO
        }
        
        private enum Register
        {
            CoreReleaseRegister = 0x000,                            // CREL
            EndianRegister = 0x004,                                 // ENDN
            CustomerRegister = 0x008,                               // CUST
            DataBitTimingAndPrescalerRegister = 0x00c,              // DBTP
            TestRegister = 0x010,                                   // TEST
            RAMWatchdog = 0x014,                                    // RWD
            CCControlRegister = 0x018,                              // CCCR
            NominalBitTimingAndPrescalerRegister = 0x01C,           // NBTP
            TimestampCounterConfiguration = 0x020,                  // TSCC
            TimestampCounterValue = 0x024,                          // TSCV
            TimeoutCounterConfiguration = 0x028,                    // TOCC
            TimeoutCounterValue = 0x02C,                            // TOCV
            ErrorCounterRegister = 0x040,                           // ECR
            ProtocolStatusRegister = 0x044,                         // PSR
            TransmitterDelayCompensationRegister = 0x048,           // TDCR
            InterruptRegister = 0x050,                              // IR
            InterruptEnable = 0x054,                                // IE
            InterruptLineSelect = 0x058,                            // ILS
            InterruptLineEnable = 0x05C,                            // ILE
            GlobalFilterConfiguration = 0x080,                      // GFC
            StandardIDFilterConfiguration = 0x084,                  // SIDFC
            ExtendedIDFilterConfiguration = 0x088,                  // XIDFC
            ExtendedIdANDMask = 0x090,                              // XIDAM
            HighPriorityMessageStatus = 0x094,                      // HPMS
            NewData1 = 0x098,                                       // NDAT1
            NewData2 = 0x09C,                                       // NDAT2
            RxFIFO0Configuration = 0x0A0,                           // RXF0C
            RxFIFO0Status = 0x0A4,                                  // RXF0S
            RxFIFO0Acknowledge = 0x0A8,                             // RXF0A
            RxBufferConfiguration = 0x0AC,                          // RXBC
            RxFIFO1Configuration = 0x0B0,                           // RXF1C
            RxFIFO1Status = 0x0B4,                                  // RXF1S
            RxFIFO1Acknowledge = 0x0B8,                             // RXF1A
            RxBufferFIFOElementSizeConfiguration = 0x0BC,           // RXESC
            TxBufferConfiguration = 0x0C0,                          // TXBC
            TxFIFOQueueStatus = 0x0C4,                              // TXFQS
            TxBufferElementSizeConfiguration = 0x0C8,               // TXESC
            TxBufferRequestPending = 0x0CC,                         // TXBRP
            TxBufferAddRequest = 0x0D0,                             // TXBAR
            TxBufferCancellationRequest = 0x0D4,                    // TXBCR
            TxBufferTransmissionOccurred = 0x0D8,                   // TXBTO
            TxBufferCancellationFinished = 0x0DC,                   // TXBCF
            TxBufferTransmissionInterruptEnable = 0x0E0,            // TXBTIE
            TxBufferCancellationFinishedInterruptEnable = 0x0E4,    // TXBCIE
            TxEventFIFOConfiguration = 0x0F0,                       // TXEFC
            TxEventFIFOStatus = 0x0F4,                              // TXEFS
            TxEventFIFOAcknowledge = 0x0F8                          // TXEFA
        }

        private enum FilterType
        {
            Range = 0b00,
            DualID = 0b01,
            Classic = 0b10,
            RangeWithoutMask = 0b11 // valid only for Extended Filter
        }

        private enum FilterElementConfiguration
        {
            DisableFilter = 0b000,
            RxFIFO0OnMatch = 0b001,
            RxFIFO1OnMatch = 0b010,
            RejectIDOnMatch = 0b011,
            SetPriorityOnMatch = 0b100,
            SetPriorityAndRxFIFO0OnMatch = 0b101,
            SetPriorityAndRxFIFO1OnMatch = 0b110,
            RxBufferOrDebugMessageOnMatch = 0b111
        }
#pragma warning disable 649, 169
        [LeastSignificantByteFirst]
        private struct RxBufferElementHeader
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(29)]
            public uint Identifier;                                 // ID
            [PacketField, Offset(doubleWords:0, bits: 29), Width(1)]
            public bool RemoteTransmissionRequest;                  // RTR
            [PacketField, Offset(doubleWords:0, bits: 30), Width(1)]
            public bool ExtendedIdentifier;                         // XTD
            [PacketField, Offset(doubleWords:0, bits: 31), Width(1)]
            public bool ErrorStateIndicator;                        // ESI
            [PacketField, Offset(doubleWords:1, bits: 0), Width(16)]
            public ushort RxTimestamp;                              // RXTS
            [PacketField, Offset(doubleWords:1, bits: 16), Width(4)]
            public byte DataLengthCode;                             // DLC
            [PacketField, Offset(doubleWords:1, bits: 20), Width(1)]
            public bool BitRateSwitch;                              // BRS
            [PacketField, Offset(doubleWords:1, bits: 21), Width(1)]
            public bool FDFormat;                                   // FDF
            [PacketField, Offset(doubleWords:1, bits: 22), Width(2)]
            private byte Reserved;
            [PacketField, Offset(doubleWords:1, bits: 24), Width(7)]
            public byte FilterIndex;                                // FIDX
            [PacketField, Offset(doubleWords:1, bits: 31), Width(1)]
            public bool AcceptedNonMatchingFrame;                   // ANMF
        }

        [LeastSignificantByteFirst]
        private struct RxBufferElementHeaderTSU
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(29)]
            public uint Identifier;                                 // ID
            [PacketField, Offset(doubleWords:0, bits: 29), Width(1)]
            public bool RemoteTransmissionRequest;                  // RTR
            [PacketField, Offset(doubleWords:0, bits: 30), Width(1)]
            public bool ExtendedIdentifier;                         // XTD
            [PacketField, Offset(doubleWords:0, bits: 31), Width(1)]
            public bool ErrorStateIndicator;                        // ESI
            [PacketField, Offset(doubleWords:1, bits: 0), Width(4)]
            public byte RxTimestampPointer;                         // RXTSP
            [PacketField, Offset(doubleWords:1, bits: 4), Width(1)]
            public bool TimestampCaptured;                          // TSC
            [PacketField, Offset(doubleWords:1, bits: 5), Width(11)]
            private ushort Reserved0;
            [PacketField, Offset(doubleWords:1, bits: 16), Width(4)]
            public byte DataLengthCode;                             // DLC
            [PacketField, Offset(doubleWords:1, bits: 20), Width(1)]
            public bool BitRateSwitch;                              // BRS
            [PacketField, Offset(doubleWords:1, bits: 21), Width(1)]
            public bool FDFormat;                                   // FDF
            [PacketField, Offset(doubleWords:1, bits: 22), Width(2)]
            private byte Reserved1;
            [PacketField, Offset(doubleWords:1, bits: 24), Width(7)]
            public byte FilterIndex;                                // FIDX
            [PacketField, Offset(doubleWords:1, bits: 31), Width(1)]
            public bool AcceptedNonMatchingFrame;                   // ANMF
        }

        [LeastSignificantByteFirst]
        private struct TxScanBufferAndEventFIFOCommonHeader
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(29)]
            public uint Identifier;                                 // ID
            [PacketField, Offset(doubleWords:0, bits: 29), Width(1)]
            public bool RemoteTransmissionRequest;                  // RTR
            [PacketField, Offset(doubleWords:0, bits: 30), Width(1)]
            public bool ExtendedIdentifier;                         // XTD
            [PacketField, Offset(doubleWords:0, bits: 31), Width(1)]
            public bool ErrorStateIndicator;                        // ESI
        }

        [LeastSignificantByteFirst]
        private struct TxBufferElementHeader
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(29)]
            public uint Identifier;                                 // ID
            [PacketField, Offset(doubleWords:0, bits: 29), Width(1)]
            public bool RemoteTransmissionRequest;                  // RTR
            [PacketField, Offset(doubleWords:0, bits: 30), Width(1)]
            public bool ExtendedIdentifier;                         // XTD
            [PacketField, Offset(doubleWords:0, bits: 31), Width(1)]
            public bool ErrorStateIndicator;                        // ESI
            [PacketField, Offset(doubleWords:1, bits: 0), Width(8)]
            private byte Reserved;
            [PacketField, Offset(doubleWords:1, bits: 8), Width(8)]
            public byte MessageMarkerHigh;                          // MM
            [PacketField, Offset(doubleWords:1, bits: 16), Width(4)]
            public byte DataLengthCode;                             // DLC
            [PacketField, Offset(doubleWords:1, bits: 20), Width(1)]
            public bool BitRateSwitch;                              // BRS
            [PacketField, Offset(doubleWords:1, bits: 21), Width(1)]
            public bool FDFormat;                                   // FDF
            [PacketField, Offset(doubleWords:1, bits: 22), Width(1)]
            public bool TimeStampCaptureEnable;                     // TSCE
            [PacketField, Offset(doubleWords:1, bits: 23), Width(1)]
            public bool EventFIFOControl;                           // EFC
            [PacketField, Offset(doubleWords:1, bits: 24), Width(8)]
            public byte MessageMarkerLow;                           // MM
        }

        [LeastSignificantByteFirst]
        private struct TxEventFIFOElement
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(29)]
            public uint Identifier;                                 // ID
            [PacketField, Offset(doubleWords:0, bits: 29), Width(1)]
            public bool RemoteTransmissionRequest;                  // RTR
            [PacketField, Offset(doubleWords:0, bits: 30), Width(1)]
            public bool ExtendedIdentifier;                         // XTD
            [PacketField, Offset(doubleWords:0, bits: 31), Width(1)]
            public bool ErrorStateIndicator;                        // ESI
            [PacketField, Offset(doubleWords:1, bits: 0), Width(16)]
            public ushort TxTimestamp;                              // TXTS
            [PacketField, Offset(doubleWords:1, bits: 16), Width(4)]
            public byte DataLengthCode;                             // DLC
            [PacketField, Offset(doubleWords:1, bits: 20), Width(1)]
            public bool BitRateSwitch;                              // BRS
            [PacketField, Offset(doubleWords:1, bits: 21), Width(1)]
            public bool FDFormat;                                   // FDF
            [PacketField, Offset(doubleWords:1, bits: 22), Width(2)]
            public byte EventType;                                  // ET
            [PacketField, Offset(doubleWords:1, bits: 24), Width(8)]
            public byte MessageMarker;                              // MM
        }

        [LeastSignificantByteFirst]
        private struct TxEventFIFOElementTSU
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(29)]
            public uint Identifier;                                 // ID
            [PacketField, Offset(doubleWords:0, bits: 29), Width(1)]
            public bool RemoteTransmissionRequest;                  // RTR
            [PacketField, Offset(doubleWords:0, bits: 30), Width(1)]
            public bool ExtendedIdentifier;                         // XTD
            [PacketField, Offset(doubleWords:0, bits: 31), Width(1)]
            public bool ErrorStateIndicator;                        // ESI
            [PacketField, Offset(doubleWords:1, bits: 0), Width(4)]
            public byte TxTimestampPointer;                          // TXTSP
            [PacketField, Offset(doubleWords:1, bits: 4), Width(1)]
            public bool TimestampCaptured;                           // TSC
            [PacketField, Offset(doubleWords:1, bits: 5), Width(3)]
            private bool Reserved;
            [PacketField, Offset(doubleWords:1, bits: 8), Width(8)]
            public byte MessageMarkerHigh;                           // MM
            [PacketField, Offset(doubleWords:1, bits: 16), Width(4)]
            public byte DataLengthCode;                              // DLC
            [PacketField, Offset(doubleWords:1, bits: 20), Width(1)]
            public bool BitRateSwitch;                               // BRS
            [PacketField, Offset(doubleWords:1, bits: 21), Width(1)]
            public bool FDFormat;                                    // FDF
            [PacketField, Offset(doubleWords:1, bits: 22), Width(2)]
            public byte EventType;                                   // ET
            [PacketField, Offset(doubleWords:1, bits: 24), Width(8)]
            public byte MessageMarkerLow;                            // MM
        }

        [LeastSignificantByteFirst]
        private struct StandardMessageIDFilterElement
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(11)]
            public ushort StandardFilterID2;                         // SFID2
            [PacketField, Offset(doubleWords:0, bits: 11), Width(4)]
            private byte Reserved;
            [PacketField, Offset(doubleWords:0, bits: 15), Width(1)]
            public bool StandardSyncMessage;                         // SSYNC
            [PacketField, Offset(doubleWords:0, bits: 16), Width(11)]
            public ushort StandardFilterID1;                         // SFID1
            [PacketField, Offset(doubleWords:0, bits: 27), Width(3)]
            public FilterElementConfiguration StandardFilterElementConfiguration; // SFEC
            [PacketField, Offset(doubleWords:0, bits: 30), Width(2)]
            public FilterType StandardFilterType;                    // SFT
        }

        [LeastSignificantByteFirst]
        private struct ExtendedMessageIDFilterElement
        {
            [PacketField, Offset(doubleWords:0, bits: 0), Width(29)]
            public uint ExtendedFilterID1;                           // EFID1
            [PacketField, Offset(doubleWords:0, bits: 29), Width(3)]
            public FilterElementConfiguration ExtendedFilterElementConfiguration; // EFEC
            [PacketField, Offset(doubleWords:1, bits: 0), Width(29)]
            public uint ExtendedFilterID2;                           // EFID2
            [PacketField, Offset(doubleWords:1, bits: 29), Width(1)]
            public bool ExtendedSyncMessage;                         // ESYNC
            [PacketField, Offset(doubleWords:1, bits: 30), Width(2)]
            public FilterType ExtendedFilterType;                    // EFT
        }
#pragma warning restore 649, 169

        // StandardMessageIDFilterElement and ExtendedMessageIDFilterElement have different layout in memory,
        // but their fields have the same meaning, so we store them in a common structure for universal access.
        private struct MessageIDFilterElement
        {
            public uint ID1;
            public uint ID2;
            public bool SyncMessage;
            public FilterElementConfiguration FilterElementConfiguration;
            public FilterType FilterType;
            public bool IsExtended;
        }

        // RxFIFOnConfiguration + RxFIFOnStatus + RxFIFOnAcknowledge + RxBufferFIFOElementSizeConfiguration
        // InterruptFlags: RxFIFOnMessageLost + RxFIFOnWatermarkReached + RxFIFOnFull + RxFIFOnNewMessage
        private struct RxFIFOView
        {
            public IValueRegisterField StartAddress;
            public IValueRegisterField Size;
            public IValueRegisterField Watermark;
            public IEnumRegisterField<FIFOOperationMode> OperationMode;
            
            public IValueRegisterField GetIndexRaw;
            public IValueRegisterField PutIndexRaw;
            public IFlagRegisterField Full;
            public IFlagRegisterField MessageLost;

            public IValueRegisterField AcknowledgeIndex;

            public IValueRegisterField DataFieldSize;

            public IFlagRegisterField InterruptMessageLost;
            public IFlagRegisterField InterruptWatermarkReached;
            public IFlagRegisterField InterruptFull;
            public IFlagRegisterField InterruptNewMessage;

            public ulong RxFIFOFillLevel
            {
                get
                {
                    if(Full.Value)
                    {
                        return Size.Value;
                    }
                    else if(PutIndexRaw.Value >= GetIndexRaw.Value)
                    {
                        return PutIndexRaw.Value - GetIndexRaw.Value;
                    }
                    else
                    {
                        return Size.Value - (GetIndexRaw.Value - PutIndexRaw.Value);
                    }
                }
            }

            public ulong RxFIFOPutIndex
            {
                get
                {
                    return PutIndexRaw.Value;
                }

                set
                {
                    PutIndexRaw.Value = value % Size.Value; // circular queue
                }
            }

            public ulong RxFIFOGetIndex
            {
                get
                {
                    return GetIndexRaw.Value;
                }

                set
                {
                    GetIndexRaw.Value = value % Size.Value; // circular queue
                }
            }
        }

        private struct FilterConfigurationView
        {
            public IFlagRegisterField RejectRemoteFrames;
            public IEnumRegisterField<NonMatchingFrameTarget> AcceptNonMatchingFrames;
            public IValueRegisterField FilterListStartAddress;
            public IValueRegisterField ListSize;
        }

        private struct RxBufferView 
        {
            public IValueRegisterField StartAddress;
            public IValueRegisterField DataFieldSize;
        }

        private struct TxFIFOQueueView
        {
            public IValueRegisterField Offset;
            public IValueRegisterField Size;
            public IFlagRegisterField QueueMode;
            public IValueRegisterField GetIndexRaw;
            public IValueRegisterField PutIndexRaw;
            public IFlagRegisterField FullRaw;
            public IFlagRegisterField[] TransmissionRequestPendingFlags;

            public bool Full
            {
                get
                {
                    return FillLevel == Size.Value;
                }
            }

            public ulong FillLevel
            {
                get
                {
                    if(QueueMode.Value)
                    {
                        return Size.Value - FreeLevel;
                    }
                    else if(FullRaw.Value)
                    {
                        return Size.Value;
                    }
                    else if(PutIndexRaw.Value >= GetIndexRaw.Value)
                    {
                        return PutIndexRaw.Value - GetIndexRaw.Value;
                    }
                    else
                    {
                        return Size.Value - (GetIndexRaw.Value - PutIndexRaw.Value);
                    }
                }
            }

            public ulong FreeLevel
            {
                get
                {
                    if(QueueMode.Value)
                    {
                        var offset = (int)Offset.Value;
                        var size = (int)Size.Value;
                        var freeLevel = 0;
                        for(var i = offset; i < offset + size; i++)
                        {
                            if(!TransmissionRequestPendingFlags[i].Value)
                            {
                                freeLevel++;
                            }
                        }
                        return (ulong)freeLevel;
                    }
                    else
                    {
                        return Size.Value - FillLevel;
                    }
                }
            }

            public ulong PutIndex
            {
                get
                {
                    if(QueueMode.Value)
                    {
                        var offset = (int)Offset.Value;
                        var size = (int)Size.Value;
                        var putIndex = 0;
                        for(var i = offset; i < offset + size; i++)
                        {
                            if(!TransmissionRequestPendingFlags[i].Value)
                            {
                                putIndex = i;
                                break;
                            }
                        }
                        return (ulong)putIndex;
                    }
                    else
                    {
                        return PutIndexRaw.Value;
                    }
                }

                set
                {
                    PutIndexRaw.Value = (value - Offset.Value) % Size.Value + Offset.Value;
                }
            }

            public ulong GetIndex
            {
                get
                {
                    return QueueMode.Value ? 0 : GetIndexRaw.Value;
                }

                set
                {
                    GetIndexRaw.Value = (value - Offset.Value) % Size.Value + Offset.Value;
                }
            }
        }

        private struct TxBufferView
        {
            public IValueRegisterField StartAddress;
            public IValueRegisterField NumberOfDedicatedTxBuffers;
            public IValueRegisterField TxFIFOQueueSize;
            public IFlagRegisterField TxFIFOQueueMode;
            public IValueRegisterField DataFieldSize;
        }

        private struct TxEventFIFOView
        {
            public IValueRegisterField StartAddress;
            public IValueRegisterField Size;
            public IValueRegisterField Watermark;
            public IValueRegisterField GetIndexRaw;
            public IValueRegisterField PutIndexRaw;
            public IFlagRegisterField Full;
            public IFlagRegisterField ElementLost;
            public IValueRegisterField AcknowledgeIndex;

            public IFlagRegisterField InterruptNewEntry;
            public IFlagRegisterField InterruptWatermarkReached;
            public IFlagRegisterField InterruptFull;
            public IFlagRegisterField InterruptElementLost;

            public ulong FillLevel
            {
                get
                {
                    if(Full.Value)
                    {
                        return Size.Value;
                    }
                    else if(PutIndexRaw.Value >= GetIndexRaw.Value)
                    {
                        return PutIndexRaw.Value - GetIndexRaw.Value;
                    }
                    else
                    {
                        return Size.Value - (GetIndexRaw.Value - PutIndexRaw.Value);
                    }
                }
            }

            public ulong PutIndex
            {
                get
                {
                    return PutIndexRaw.Value;
                }

                set
                {
                    PutIndexRaw.Value = value % Size.Value; // circular queue
                }
            }

            public ulong GetIndex
            {
                get
                {
                    return GetIndexRaw.Value;
                }

                set
                {
                    GetIndexRaw.Value = value % Size.Value; // circular queue
                }
            }
        }

        private struct RegisterMapView
        {
            public TestRegister TestRegister;
            public CCControlRegister CCControlRegister;
            public ProtocolStatusRegister ProtocolStatusRegister;
            public InterruptRegister InterruptRegister;
            public InterruptEnable InterruptEnable;
            public InterruptLineSelect InterruptLineSelect;
            public InterruptLineEnable InterruptLineEnable;
            public GlobalFilterConfiguration GlobalFilterConfiguration;
            public StandardIDFilterConfiguration StandardIDFilterConfiguration;
            public ExtendedIDFilterConfiguration ExtendedIDFilterConfiguration;
            public ExtendedIdANDMask ExtendedIdANDMask;
            public HighPriorityMessageStatus HighPriorityMessageStatus;
            public NewData1 NewData1;
            public NewData2 NewData2;
            public RxFIFO0Configuration RxFIFO0Configuration;
            public RxFIFO0Status RxFIFO0Status;
            public RxFIFO0Acknowledge RxFIFO0Acknowledge;
            public RxBufferConfiguration RxBufferConfiguration;
            public RxFIFO1Configuration RxFIFO1Configuration;
            public RxFIFO1Status RxFIFO1Status;
            public RxFIFO1Acknowledge RxFIFO1Acknowledge;
            public RxBufferFIFOElementSizeConfiguration RxBufferFIFOElementSizeConfiguration;
            public TxBufferConfiguration TxBufferConfiguration;
            public TxFIFOQueueStatus TxFIFOQueueStatus;
            public TxBufferElementSizeConfiguration TxBufferElementSizeConfiguration;
            public TxBufferRequestPending TxBufferRequestPending;
            public TxBufferAddRequest TxBufferAddRequest;
            public TxBufferCancellationRequest TxBufferCancellationRequest;
            public TxBufferTransmissionOccurred TxBufferTransmissionOccurred;
            public TxBufferCancellationFinished TxBufferCancellationFinished;
            public TxBufferTransmissionInterruptEnable TxBufferTransmissionInterruptEnable;
            public TxBufferCancellationFinishedInterruptEnable TxBufferCancellationFinishedInterruptEnable;
            public TxEventFIFOConfiguration TxEventFIFOConfiguration;
            public TxEventFIFOStatus TxEventFIFOStatus;
            public TxEventFIFOAcknowledge TxEventFIFOAcknowledge;
        }

        private struct TestRegister
        {
            public IFlagRegisterField LoopBackMode;
            public IValueRegisterField TxBufferNumberPrepared;
            public IFlagRegisterField PreparedValid;
            public IValueRegisterField TxBufferNumberStarted;
            public IFlagRegisterField StartedValid;
        }

        private struct CCControlRegister {
            public IFlagRegisterField[] ControlFields;
        }

        private struct ProtocolStatusRegister
        {
            public IEnumRegisterField<LastErrorCode> LastErrorCode;
            public IEnumRegisterField<Activity> Activity;
            public IFlagRegisterField ReceivedCANFDMessage;
        }

        private struct InterruptRegister
        {
            public IFlagRegisterField[] InterruptFlags;
        }

        private struct InterruptEnable
        {
            public IFlagRegisterField[] InterruptEnableFlags;
        }

        private struct InterruptLineSelect
        {
            public IFlagRegisterField[] InterruptLineSelectFlags;
        }

        private struct InterruptLineEnable
        {
            public IFlagRegisterField EnableInterruptLine0;
            public IFlagRegisterField EnableInterruptLine1;
        }

        private struct GlobalFilterConfiguration
        {
            public IFlagRegisterField RejectRemoteFramesExtended;
            public IFlagRegisterField RejectRemoteFramesStandard;
            public IEnumRegisterField<NonMatchingFrameTarget> AcceptNonMatchingFramesExtended;
            public IEnumRegisterField<NonMatchingFrameTarget> AcceptNonMatchingFramesStandard;
        }

        private struct StandardIDFilterConfiguration
        {
            public IValueRegisterField FilterListStandardStartAddress;
            public IValueRegisterField ListSizeStandard;
        }

        private struct ExtendedIDFilterConfiguration
        {
            public IValueRegisterField FilterListExtendedStartAddress;
            public IValueRegisterField ListSizeExtended;
        }

        private struct ExtendedIdANDMask
        {
            public IValueRegisterField ExtendedIDANDMask;
        }

        private struct HighPriorityMessageStatus
        {
            public IValueRegisterField BufferIndex;
            public IEnumRegisterField<MessageStorageIndicator> MessageStorageIndicator;
            public IValueRegisterField FilterIndex;
            public IFlagRegisterField FilterList;
        }

        private struct NewData1
        {
            public IFlagRegisterField[] NewData1Flags;
        }

        private struct NewData2
        {
            public IFlagRegisterField[] NewData2Flags;
        }

        private struct RxFIFO0Configuration
        {
            public IValueRegisterField RxFIFO0StartAddress;
            public IValueRegisterField RxFIFO0Size;
            public IValueRegisterField RxFIFO0Watermark;
            public IEnumRegisterField<FIFOOperationMode> FIFO0OperationMode;
        }

        private struct RxFIFO0Status
        {
            public IValueRegisterField RxFIFO0GetIndex;
            public IValueRegisterField RxFIFO0PutIndex;
            public IFlagRegisterField RxFIFO0Full;
            public IFlagRegisterField RxFIFO0MessageLost;
        }

        private struct RxFIFO0Acknowledge
        {
            public IValueRegisterField RxFIFO0AcknowledgeIndex;
        }

        private struct RxBufferConfiguration
        {
            public IValueRegisterField RxBufferStartAddress;
        }

        private struct RxFIFO1Configuration
        {
            public IValueRegisterField RxFIFO1StartAddress;
            public IValueRegisterField RxFIFO1Size;
            public IValueRegisterField RxFIFO1Watermark;
            public IEnumRegisterField<FIFOOperationMode> FIFO1OperationMode;
        }

        private struct RxFIFO1Status
        {
            public IValueRegisterField RxFIFO1GetIndex;
            public IValueRegisterField RxFIFO1PutIndex;
            public IFlagRegisterField RxFIFO1Full;
            public IFlagRegisterField RxFIFO1MessageLost;
            public IEnumRegisterField<DebugMessageStatus> DebugMessageStatus;
        }

        private struct RxFIFO1Acknowledge
        {
            public IValueRegisterField RxFIFO1AcknowledgeIndex;
        }

        private struct RxBufferFIFOElementSizeConfiguration
        {
            public IValueRegisterField RxFIFO0DataFieldSize;
            public IValueRegisterField RxFIFO1DataFieldSize;
            public IValueRegisterField RxBufferDataFieldSize;
        }

        private struct TxBufferConfiguration
        {
            public IValueRegisterField TxBuffersStartAddress;
            public IValueRegisterField NumberOfDedicatedTxBuffers;
            public IValueRegisterField TxFIFOQueueSize;
            public IFlagRegisterField TxFIFOQueueMode;
        }

        private struct TxFIFOQueueStatus
        {
            public IValueRegisterField TxFIFOGetIndex;
            public IValueRegisterField TxFIFOQueuePutIndex;
            public IFlagRegisterField TxFIFOQueueFull;
        }

        private struct TxBufferElementSizeConfiguration
        {
            public IValueRegisterField TxBufferDataFieldSize;
        }

        private struct TxBufferRequestPending
        {
            public IFlagRegisterField[] TransmissionRequestPendingFlags;
        }

        private struct TxBufferAddRequest
        {
            public IFlagRegisterField[] AddRequestFlags;
        }

        private struct TxBufferCancellationRequest
        {
            public IFlagRegisterField[] CancellationRequestFlags;
        }

        private struct TxBufferTransmissionOccurred
        {
            public IFlagRegisterField[] TransmissionOccurredFlags;
        }

        private struct TxBufferCancellationFinished
        {
            public IFlagRegisterField[] CancellationFinishedFlags;
        }

        private struct TxBufferTransmissionInterruptEnable
        {
            public IFlagRegisterField[] TransmissionInterruptEnableFlags;
        }

        private struct TxBufferCancellationFinishedInterruptEnable
        {
            public IFlagRegisterField[] CancellationFinishedInterruptEnableFlags;
        }

        private struct TxEventFIFOConfiguration
        {
            public IValueRegisterField EventFIFOStartAddress;
            public IValueRegisterField EventFIFOSize;
            public IValueRegisterField EventFIFOWatermark;
        }

        private struct TxEventFIFOStatus
        {
            public IValueRegisterField EventFIFOGetIndex;
            public IValueRegisterField EventFIFOPutIndex;
            public IFlagRegisterField EventFIFOFull;
            public IFlagRegisterField TxEventFIFOElementLost;
        }

        private struct TxEventFIFOAcknowledge
        {
            public IValueRegisterField EventFIFOAcknowledgeIndex;
        }
    }
}
