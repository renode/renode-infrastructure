//
// Copyright (c) 2010-2025 Antmicro
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
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public partial class MCAN : IDoubleWordPeripheral, IKnownSize, ICAN
    {
        public MCAN(IMachine machine, IMultibyteWritePeripheral messageRAM)
        {
            this.machine = machine;
            this.messageRAM = messageRAM;
            Line0 = new GPIO();
            Line1 = new GPIO();
            Calibration = new GPIO();
            BuildRegisterMapView();
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            BuildStructuredViews();
            UpdateInterrupts();
        }

        public void OnFrameReceived(CANMessageFrame rxMessage)
        {
            HandleRxInner(rxMessage);
            UpdateInterrupts();
        }

        public void Reset()
        {
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>();

            registerMap[(long)Register.CoreReleaseRegister] = new DoubleWordRegister(this, resetValue: 0x33191121)
                .WithTag("DAY", 0, 8)
                .WithTag("MON", 8, 8)
                .WithTag("YEAR", 16, 4)
                .WithTag("SUBSTEP", 20, 4)
                .WithTag("STEP", 24, 4)
                .WithTag("REL", 28, 4);

            registerMap[(long)Register.EndianRegister] = new DoubleWordRegister(this, resetValue: 0x87654321)
                .WithTag("ETV", 0, 32);

            registerMap[(long)Register.CustomerRegister] = new DoubleWordRegister(this)
                .WithReservedBits(0, 32);

            registerMap[(long)Register.DataBitTimingAndPrescalerRegister] = new DoubleWordRegister(this, resetValue: 0x00000A33)
                .WithTag("DSJW", 0, 4)
                .WithTag("DTSEG", 4, 4)
                .WithTag("DTSEG1", 8, 5)
                .WithReservedBits(13, 3)
                .WithTag("DBRP", 16, 5)
                .WithReservedBits(21, 2)
                .WithTaggedFlag("TDC", 23)
                .WithReservedBits(24, 8);

            registerMap[(long)Register.TestRegister] = new DoubleWordRegister(this)
                .WithReservedBits(0, 4)
                .WithFlag(4, out rv.TestRegister.LoopBackMode, writeCallback: (oldVal, newVal) =>
                {
                    if(!IsProtectedWrite && newVal != oldVal)
                    {
                        this.Log(LogLevel.Warning, "Trying to write to protected field. Ignoring.");
                        rv.TestRegister.LoopBackMode.Value = oldVal;
                        return;
                    }
                }, name: "LBCK")
                .WithTag("TX", 5, 2)
                .WithTaggedFlag("RX", 7)
                .WithValueField(8, 5, out rv.TestRegister.TxBufferNumberPrepared, FieldMode.Read, name: "TXBNP")
                .WithFlag(13, out rv.TestRegister.PreparedValid, FieldMode.Read, name: "PVAL")
                .WithReservedBits(14, 2)
                .WithValueField(16, 5, out rv.TestRegister.TxBufferNumberStarted, FieldMode.Read, name: "TXBNS")
                .WithFlag(21, out rv.TestRegister.StartedValid, FieldMode.Read, name: "SVAL")
                .WithReservedBits(22, 10);

            registerMap[(long)Register.RAMWatchdog] = new DoubleWordRegister(this)
                .WithTag("WDC", 0, 8)
                .WithTag("WDV", 8, 8)
                .WithReservedBits(16, 16);

            registerMap[(long)Register.CCControlRegister] = new DoubleWordRegister(this, resetValue: 0x1)
                .WithFlags(0, 16, out rv.CCControlRegister.ControlFields, writeCallback: (idx, oldVal, newVal) =>
                {
                    // Handle fields that have meaningful side effects from the point of emulation. Other fields are treated as flags.
                    switch((Control)idx)
                    {
                        case Control.Initialization:
                        {
                            if(!newVal)
                            {
                                rv.CCControlRegister.ControlFields[(int)Control.ConfigurationChangeEnable].Value = false;
                                this.Log(LogLevel.Debug, "Software initialization is finished");
                            }
                            break;
                        }
                        case Control.ConfigurationChangeEnable:
                        {
                            if(!rv.CCControlRegister.ControlFields[(int)Control.Initialization].Value && rv.CCControlRegister.ControlFields[(int)Control.ConfigurationChangeEnable].Value) // oldVal was reset through resetting INIT
                            {
                                rv.CCControlRegister.ControlFields[idx].Value = oldVal;
                                return;
                            }
                            if(newVal)
                            {
                                registerMap[(long)Register.HighPriorityMessageStatus].Reset();
                                registerMap[(long)Register.RxFIFO0Status].Reset();
                                registerMap[(long)Register.RxFIFO1Status].Reset();
                                registerMap[(long)Register.TxFIFOQueueStatus].Reset();
                                registerMap[(long)Register.TxBufferRequestPending].Reset();
                                registerMap[(long)Register.TxBufferTransmissionOccurred].Reset();
                                registerMap[(long)Register.TxBufferCancellationFinished].Reset();
                                registerMap[(long)Register.TxEventFIFOStatus].Reset();
                            }
                            break;
                        }
                        case Control.BusMonitoringMode:
                        case Control.TestModeEnable:
                        {
                            if(newVal)
                            {
                                if(!IsProtectedWrite && newVal != oldVal)
                                {
                                    this.Log(LogLevel.Warning, "Trying to write to protected field. Ignoring.");
                                    rv.CCControlRegister.ControlFields[idx].Value = oldVal;
                                    return;
                                }
                            }
                            else
                            {
                                if((Control)idx == Control.TestModeEnable)
                                {
                                    registerMap[(long)Register.TestRegister].Reset();
                                }
                            }
                            break;
                        }
                        case Control.DisableAutomaticRetransmission:
                        {
                            if(!IsProtectedWrite && newVal != oldVal)
                            {
                                this.Log(LogLevel.Warning, "Trying to write to protected field. Ignoring.");
                                rv.CCControlRegister.ControlFields[idx].Value = oldVal;
                                return;
                            }
                            break;
                        }
                    }
                })
                .WithReservedBits(16, 16);

            registerMap[(long)Register.NominalBitTimingAndPrescalerRegister] = new DoubleWordRegister(this, resetValue: 0x06000A03)
                .WithTag("NTSEG2", 0, 7)
                .WithReservedBits(7, 1)
                .WithTag("NTSEG1", 8, 8)
                .WithTag("NBRP", 16, 9)
                .WithTag("NSJW", 25, 7);

            registerMap[(long)Register.TimestampCounterConfiguration] = new DoubleWordRegister(this)
                .WithTag("TSS", 0, 2)
                .WithReservedBits(2, 14)
                .WithTag("TCP", 16, 4)
                .WithReservedBits(20, 12);

            registerMap[(long)Register.TimestampCounterValue] = new DoubleWordRegister(this)
                .WithTag("TSC", 0, 16)
                .WithReservedBits(16, 16);

            registerMap[(long)Register.TimeoutCounterConfiguration] = new DoubleWordRegister(this, resetValue: 0xFFFF0000)
                .WithTaggedFlag("ETOC", 0)
                .WithTag("TOS", 1, 2)
                .WithReservedBits(3, 13)
                .WithTag("TOP", 16, 16);

            registerMap[(long)Register.TimeoutCounterValue] = new DoubleWordRegister(this, resetValue: 0x0000FFFF)
                .WithTag("TOC", 0, 16)
                .WithReservedBits(16, 16);

            registerMap[(long)Register.ErrorCounterRegister] = new DoubleWordRegister(this)
                .WithTag("TEC", 0, 8)
                .WithTag("REC", 8, 7)
                .WithTaggedFlag("RP", 15)
                .WithTag("CEL", 16, 8)
                .WithReservedBits(24, 8);

            registerMap[(long)Register.ProtocolStatusRegister] = new DoubleWordRegister(this, resetValue: 0x00000707)
                .WithEnumField(0, 3, out rv.ProtocolStatusRegister.LastErrorCode, readCallback: (_, __) =>
                {
                    rv.ProtocolStatusRegister.LastErrorCode.Value = LastErrorCode.NoChange; // Reset on read
                }, name: "LEC")
                .WithEnumField(3, 2, out rv.ProtocolStatusRegister.Activity, name: "ACT")
                .WithTaggedFlag("EP", 5)
                .WithTaggedFlag("EW", 6)
                .WithTaggedFlag("BO", 7)
                .WithTag("DLEC", 8, 3)
                .WithTaggedFlag("RESI", 11)
                .WithTaggedFlag("RBRS", 12)
                .WithFlag(13, out rv.ProtocolStatusRegister.ReceivedCANFDMessage, readCallback: (_, __) =>
                {
                    rv.ProtocolStatusRegister.ReceivedCANFDMessage.Value = false; // Reset on read
                }, name: "RFDF")
                .WithTaggedFlag("PXE", 14)
                .WithReservedBits(15, 1)
                .WithTag("TDCV", 16, 7)
                .WithReservedBits(23, 9);

            registerMap[(long)Register.TransmitterDelayCompensationRegister] = new DoubleWordRegister(this)
                .WithTag("TDCF", 0, 7)
                .WithReservedBits(7, 1)
                .WithTag("TDCO", 8, 7)
                .WithReservedBits(15, 17);

            registerMap[(long)Register.InterruptRegister] = new DoubleWordRegister(this)
                .WithFlags(0, 30, out rv.InterruptRegister.InterruptFlags, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (idx, oldVal, newVal) =>
                {
                    if(!newVal)
                    {
                        return;
                    }

                    switch((Interrupt)idx)
                    {
                        case Interrupt.RxFIFO0MessageLost:
                        {
                            rxFIFO0.MessageLost.Value = false;
                            break;
                        }
                        case Interrupt.RxFIFO1MessageLost:
                        {
                            rxFIFO1.MessageLost.Value = false;
                            break;
                        }
                        case Interrupt.TxEventFIFOElementLost:
                        {
                            txEventFIFO.ElementLost.Value = false;
                            break;
                        }
                    }
                })
                .WithWriteCallback((_, __) => UpdateInterrupts());

            registerMap[(long)Register.InterruptEnable] = new DoubleWordRegister(this)
                .WithFlags(0, 30, out rv.InterruptEnable.InterruptEnableFlags)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            registerMap[(long)Register.InterruptLineSelect] = new DoubleWordRegister(this)
                .WithFlags(0, 30, out rv.InterruptLineSelect.InterruptLineSelectFlags)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            registerMap[(long)Register.InterruptLineEnable] = new DoubleWordRegister(this)
                .WithFlag(0, out rv.InterruptLineEnable.EnableInterruptLine0, name: "EINT0")
                .WithFlag(1, out rv.InterruptLineEnable.EnableInterruptLine1, name: "EINT1")
                .WithWriteCallback((_, __) => UpdateInterrupts());

            registerMap[(long)Register.GlobalFilterConfiguration] = new DoubleWordRegister(this)
                .WithFlag(0, out rv.GlobalFilterConfiguration.RejectRemoteFramesExtended, name: "RRFE")
                .WithFlag(1, out rv.GlobalFilterConfiguration.RejectRemoteFramesStandard, name: "RRFS")
                .WithEnumField(2, 2, out rv.GlobalFilterConfiguration.AcceptNonMatchingFramesExtended, name: "ANFE")
                .WithEnumField(4, 2, out rv.GlobalFilterConfiguration.AcceptNonMatchingFramesStandard, name: "ANFS");

            registerMap[(long)Register.StandardIDFilterConfiguration] = new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 14, out rv.StandardIDFilterConfiguration.FilterListStandardStartAddress, name: "FLSSA")
                .WithValueField(16, 8, out rv.StandardIDFilterConfiguration.ListSizeStandard, name: "LSS")
                .WithReservedBits(24, 8);

            registerMap[(long)Register.ExtendedIDFilterConfiguration] = new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 14, out rv.ExtendedIDFilterConfiguration.FilterListExtendedStartAddress, name: "FLESA")
                .WithValueField(16, 7, out rv.ExtendedIDFilterConfiguration.ListSizeExtended, name: "LSE")
                .WithReservedBits(23, 9);

            registerMap[(long)Register.ExtendedIdANDMask] = new DoubleWordRegister(this, resetValue: 0x1FFFFFFF)
                .WithValueField(0, 29, out rv.ExtendedIdANDMask.ExtendedIDANDMask, name: "EIDM")
                .WithReservedBits(29, 3);

            registerMap[(long)Register.HighPriorityMessageStatus] = new DoubleWordRegister(this)
                .WithValueField(0, 6, valueField: out rv.HighPriorityMessageStatus.BufferIndex, name: "BIDX")
                .WithEnumField(6, 2, out rv.HighPriorityMessageStatus.MessageStorageIndicator, name: "MSI")
                .WithValueField(8, 7, out rv.HighPriorityMessageStatus.FilterIndex, name: "FIDX")
                .WithFlag(15, out rv.HighPriorityMessageStatus.FilterList, name: "FLST")
                .WithReservedBits(16, 16);

            registerMap[(long)Register.NewData1] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.NewData1.NewData1Flags, FieldMode.Read | FieldMode.WriteOneToClear, name: "newData1x");

            registerMap[(long)Register.NewData2] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.NewData2.NewData2Flags, FieldMode.Read | FieldMode.WriteOneToClear, name: "newData2x");

            registerMap[(long)Register.RxFIFO0Configuration] = new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 14, out rv.RxFIFO0Configuration.RxFIFO0StartAddress, name: "F0SA")
                .WithValueField(16, 7, out rv.RxFIFO0Configuration.RxFIFO0Size, name: "F0S")
                .WithReservedBits(23, 1)
                .WithValueField(24, 7, out rv.RxFIFO0Configuration.RxFIFO0Watermark, name: "F0WM")
                .WithEnumField(31, 1, out rv.RxFIFO0Configuration.FIFO0OperationMode, name: "F0OM");

            registerMap[(long)Register.RxFIFO0Status] = new DoubleWordRegister(this)
                .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: _ => rxFIFO0.RxFIFOFillLevel, name: "F0FL")
                .WithReservedBits(7, 1)
                .WithValueField(8, 6, out rv.RxFIFO0Status.RxFIFO0GetIndex, FieldMode.Read, name: "F0GI")
                .WithReservedBits(14, 2)
                .WithValueField(16, 6, out rv.RxFIFO0Status.RxFIFO0PutIndex, FieldMode.Read, name: "F0PI")
                .WithReservedBits(22, 2)
                .WithFlag(24, out rv.RxFIFO0Status.RxFIFO0Full, FieldMode.Read, name: "F0F")
                .WithFlag(25, out rv.RxFIFO0Status.RxFIFO0MessageLost, FieldMode.Read, name: "RF0L")
                .WithReservedBits(26, 6);

            registerMap[(long)Register.RxFIFO0Acknowledge] = new DoubleWordRegister(this)
                .WithValueField(0, 6, out rv.RxFIFO0Acknowledge.RxFIFO0AcknowledgeIndex, writeCallback: (_, newVal) =>
                {
                    rxFIFO0.RxFIFOGetIndex = newVal + 1;
                    rxFIFO0.Full.Value = false;
                }, name: "F0AI")
                .WithReservedBits(6, 26);

            registerMap[(long)Register.RxBufferConfiguration] = new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 14, out rv.RxBufferConfiguration.RxBufferStartAddress, name: "RBSA")
                .WithReservedBits(16, 16);

            registerMap[(long)Register.RxFIFO1Configuration] = new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 14, out rv.RxFIFO1Configuration.RxFIFO1StartAddress, name: "F1SA")
                .WithValueField(16, 7, out rv.RxFIFO1Configuration.RxFIFO1Size, name: "F1S")
                .WithReservedBits(23, 1)
                .WithValueField(24, 7, out rv.RxFIFO1Configuration.RxFIFO1Watermark, name: "F1WM")
                .WithEnumField(31, 1, out rv.RxFIFO1Configuration.FIFO1OperationMode, name: "F1OM");

            registerMap[(long)Register.RxFIFO1Status] = new DoubleWordRegister(this)
                .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: _ => rxFIFO1.RxFIFOFillLevel, name: "F0FL")
                .WithReservedBits(7, 1)
                .WithValueField(8, 6, out rv.RxFIFO1Status.RxFIFO1GetIndex, FieldMode.Read, name: "F0GI")
                .WithReservedBits(14, 2)
                .WithValueField(16, 6, out rv.RxFIFO1Status.RxFIFO1PutIndex, FieldMode.Read, name: "F0PI")
                .WithReservedBits(22, 2)
                .WithFlag(24, out rv.RxFIFO1Status.RxFIFO1Full, FieldMode.Read, name: "F0F")
                .WithFlag(25, out rv.RxFIFO1Status.RxFIFO1MessageLost, FieldMode.Read, name: "RF0L")
                .WithReservedBits(26, 4)
                .WithEnumField(30, 2, out rv.RxFIFO1Status.DebugMessageStatus, FieldMode.Read, name: "DMS");

            registerMap[(long)Register.RxFIFO1Acknowledge] = new DoubleWordRegister(this)
                .WithValueField(0, 6, out rv.RxFIFO1Acknowledge.RxFIFO1AcknowledgeIndex, writeCallback: (_, newVal) =>
                {
                    rxFIFO1.RxFIFOGetIndex = newVal + 1;
                    rxFIFO1.Full.Value = false;
                }, name: "F1AI")
                .WithReservedBits(6, 26);

            registerMap[(long)Register.RxBufferFIFOElementSizeConfiguration] = new DoubleWordRegister(this)
                .WithValueField(0, 3, out rv.RxBufferFIFOElementSizeConfiguration.RxFIFO0DataFieldSize, name: "F0DS")
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, out rv.RxBufferFIFOElementSizeConfiguration.RxFIFO1DataFieldSize, name: "F1DS")
                .WithReservedBits(7, 1)
                .WithValueField(8, 3, out rv.RxBufferFIFOElementSizeConfiguration.RxBufferDataFieldSize, name: "RBDS")
                .WithReservedBits(11, 21);

            registerMap[(long)Register.TxBufferConfiguration] = new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 14, out rv.TxBufferConfiguration.TxBuffersStartAddress, name: "TBSA") // start address is aligned to four bytes, hence the lowest two bits of register are reserved
                .WithValueField(16, 6, out rv.TxBufferConfiguration.NumberOfDedicatedTxBuffers, name: "NDTB")
                .WithReservedBits(22, 2)
                .WithValueField(24, 6, out rv.TxBufferConfiguration.TxFIFOQueueSize, name: "TFQS")
                .WithFlag(30, out rv.TxBufferConfiguration.TxFIFOQueueMode, name: "TFQM")
                .WithReservedBits(31, 1);

            registerMap[(long)Register.TxFIFOQueueStatus] = new DoubleWordRegister(this)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => txFIFOQueue.FreeLevel, name: "TFFL")
                .WithReservedBits(6, 2)
                .WithValueField(8, 5, out rv.TxFIFOQueueStatus.TxFIFOGetIndex, FieldMode.Read, valueProviderCallback: _ => txFIFOQueue.GetIndex, name: "TFGI")
                .WithReservedBits(13, 3)
                .WithValueField(16, 5, out rv.TxFIFOQueueStatus.TxFIFOQueuePutIndex, FieldMode.Read, valueProviderCallback: _ => txFIFOQueue.PutIndex, name: "TFQPI")
                .WithFlag(21, out rv.TxFIFOQueueStatus.TxFIFOQueueFull, FieldMode.Read, valueProviderCallback: _ => txFIFOQueue.Full, name: "TFQF")
                .WithReservedBits(22, 10);

            registerMap[(long)Register.TxBufferElementSizeConfiguration] = new DoubleWordRegister(this)
                .WithValueField(0, 3, out rv.TxBufferElementSizeConfiguration.TxBufferDataFieldSize, name: "TBDS")
                .WithReservedBits(3, 29);

            registerMap[(long)Register.TxBufferRequestPending] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.TxBufferRequestPending.TransmissionRequestPendingFlags, FieldMode.Read, name: "TRPx");

            registerMap[(long)Register.TxBufferAddRequest] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.TxBufferAddRequest.AddRequestFlags, writeCallback: (idx, oldVal, newVal) =>
                {
                    if(!newVal || rv.CCControlRegister.ControlFields[(int)Control.ConfigurationChangeEnable].Value || idx >= (int)(rv.TxBufferConfiguration.NumberOfDedicatedTxBuffers.Value + rv.TxBufferConfiguration.TxFIFOQueueSize.Value))
                    {
                        rv.TxBufferAddRequest.AddRequestFlags[idx].Value = oldVal;
                        return;
                    }
                    rv.TxBufferRequestPending.TransmissionRequestPendingFlags[idx].Value = true;
                    rv.TxBufferCancellationFinished.CancellationFinishedFlags[idx].Value = false;
                    rv.TxBufferTransmissionOccurred.TransmissionOccurredFlags[idx].Value = false;

                    if(!txFIFOQueue.QueueMode.Value && idx >= (int)txFIFOQueue.Offset.Value)
                    {
                        // Message added Tx FIFO
                        txFIFOQueue.PutIndex++;
                        if(txFIFOQueue.PutIndex == txFIFOQueue.GetIndex)
                        {
                            txFIFOQueue.FullRaw.Value = true;
                            this.Log(LogLevel.Warning, "Tx FIFO is full");
                        }
                    }
                }, name: "ARx")
                .WithWriteCallback((oldVal, newVal) =>
                {
                    TxScan();
                });

            registerMap[(long)Register.TxBufferCancellationRequest] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.TxBufferCancellationRequest.CancellationRequestFlags, writeCallback: (idx, oldVal, newVal) =>
                {
                    if(!newVal || rv.CCControlRegister.ControlFields[(int)Control.ConfigurationChangeEnable].Value || idx >= (int)(rv.TxBufferConfiguration.NumberOfDedicatedTxBuffers.Value + rv.TxBufferConfiguration.TxFIFOQueueSize.Value))
                    {
                        rv.TxBufferCancellationRequest.CancellationRequestFlags[idx].Value = oldVal;
                        return;
                    }
                    rv.TxBufferRequestPending.TransmissionRequestPendingFlags[idx].Value = false;
                    rv.TxBufferCancellationRequest.CancellationRequestFlags[idx].Value = false;

                    if(!txFIFOQueue.QueueMode.Value && idx == (int)txFIFOQueue.GetIndex)
                    {
                        txFIFOQueue.GetIndex++;
                        txFIFOQueue.FullRaw.Value = false;
                    }

                    rv.TxBufferCancellationFinished.CancellationFinishedFlags[idx].Value = true;
                }, name: "CRx")
                .WithWriteCallback((oldVal, newVal) =>
                {
                    UpdateInterrupts();
                    TxScan();
                });

            registerMap[(long)Register.TxBufferTransmissionOccurred] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.TxBufferTransmissionOccurred.TransmissionOccurredFlags, name: "TOx");

            registerMap[(long)Register.TxBufferCancellationFinished] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.TxBufferCancellationFinished.CancellationFinishedFlags, name: "CFx");

            registerMap[(long)Register.TxBufferTransmissionInterruptEnable] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.TxBufferTransmissionInterruptEnable.TransmissionInterruptEnableFlags, name: "TIEx");

            registerMap[(long)Register.TxBufferCancellationFinishedInterruptEnable] = new DoubleWordRegister(this)
                .WithFlags(0, 32, out rv.TxBufferCancellationFinishedInterruptEnable.CancellationFinishedInterruptEnableFlags, name: "CFIEx");

            registerMap[(long)Register.TxEventFIFOConfiguration] = new DoubleWordRegister(this)
                .WithReservedBits(0, 2)
                .WithValueField(2, 14, out rv.TxEventFIFOConfiguration.EventFIFOStartAddress, name: "EFSA")
                .WithValueField(16, 6, out rv.TxEventFIFOConfiguration.EventFIFOSize, name: "EFS")
                .WithReservedBits(22, 2)
                .WithValueField(24, 6, out rv.TxEventFIFOConfiguration.EventFIFOWatermark, name: "EFWM")
                .WithReservedBits(30, 2);

            registerMap[(long)Register.TxEventFIFOStatus] = new DoubleWordRegister(this)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => txEventFIFO.FillLevel, name: "EFFL")
                .WithReservedBits(6, 2)
                .WithValueField(8, 5, out rv.TxEventFIFOStatus.EventFIFOGetIndex, FieldMode.Read, name: "EFGI")
                .WithReservedBits(13, 3)
                .WithValueField(16, 5, out rv.TxEventFIFOStatus.EventFIFOPutIndex, FieldMode.Read, name: "EFPI")
                .WithReservedBits(21, 3)
                .WithFlag(24, out rv.TxEventFIFOStatus.EventFIFOFull, FieldMode.Read, name: "EFF")
                .WithFlag(25, out rv.TxEventFIFOStatus.TxEventFIFOElementLost, FieldMode.Read, name: "TEFL")
                .WithReservedBits(26, 6);

            registerMap[(long)Register.TxEventFIFOAcknowledge] = new DoubleWordRegister(this)
                .WithValueField(0, 5, out rv.TxEventFIFOAcknowledge.EventFIFOAcknowledgeIndex, writeCallback: (_, newVal) =>
                {
                    txEventFIFO.GetIndex = newVal + 1;
                    txEventFIFO.Full.Value = false;
                    if(txEventFIFO.FillLevel == 0)
                    {
                        rv.InterruptRegister.InterruptFlags[(int)Interrupt.TxFIFOEmpty].Value = true;
                        UpdateInterrupts();
                    }
                }, name: "EFAI")
                .WithReservedBits(5, 27);

            return registerMap;
        }

        private void TxScan()
        {
            var bufferIdx = FindPrioritizedBuffer(out var messageID);
            if(bufferIdx == -1)
            {
                return;
            }
            this.Log(LogLevel.Debug, "Buffer {0} with Message ID {1} will be transmitted", bufferIdx, messageID);
            TransmitBuffer(bufferIdx);
        }

        // It scans Tx Buffers section in the Message RAM and returns index of the buffer to be transmitted next according to Tx prioritization rules.
        private int FindPrioritizedBuffer(out uint messageID)
        {
            var numberOfDedicatedTxBuffers = txBuffers.NumberOfDedicatedTxBuffers.Value;
            var txFIFOQueueSize = txBuffers.TxFIFOQueueSize.Value;
            var txFIFOQueueMode = txBuffers.TxFIFOQueueMode.Value;

            var txScanMode = TxScanModeInternal.Dedicated;

            if(txFIFOQueueSize == 0)
            {
                txScanMode = TxScanModeInternal.Dedicated;
            }
            else if(numberOfDedicatedTxBuffers == 0)
            {
                if(txFIFOQueueMode)
                {
                    txScanMode = TxScanModeInternal.Queue;
                }
                else
                {
                    txScanMode = TxScanModeInternal.FIFO;
                }
            }
            else
            {
                if(txFIFOQueueMode)
                {
                    txScanMode = TxScanModeInternal.MixedDedicatedQueue;
                }
                else
                {
                    txScanMode = TxScanModeInternal.MixedDedicatedFIFO;
                }
            }

            messageID = uint.MaxValue; // Message ID of selected buffer
            var bufferNumber = -1; // Index of selected buffer

            switch(txScanMode)
            {
                case TxScanModeInternal.Dedicated:
                {
                    bufferNumber = ScanDedicatedTxBuffers(out messageID);
                    break;
                }
                case TxScanModeInternal.FIFO:
                {
                    bufferNumber = ScanTxFIFO(out messageID);
                    break;
                }
                case TxScanModeInternal.Queue:
                {
                    bufferNumber = ScanTxQueue(out messageID);
                    break;
                }
                case TxScanModeInternal.MixedDedicatedFIFO:
                {
                    var bufferNumber0 = ScanDedicatedTxBuffers(out var messageID0);
                    var bufferNumber1 = ScanTxFIFO(out var messageID1);
                    messageID = messageID0 <= messageID1 ? messageID0 : messageID1;
                    bufferNumber = messageID0 <= messageID1 ? bufferNumber0 : bufferNumber1;
                    break;
                }
                case TxScanModeInternal.MixedDedicatedQueue:
                {
                    var bufferNumber0 = ScanDedicatedTxBuffers(out var messageID0);
                    var bufferNumber1 = ScanTxQueue(out var messageID1);
                    messageID = messageID0 <= messageID1 ? messageID0 : messageID1;
                    bufferNumber = messageID0 <= messageID1 ? bufferNumber0 : bufferNumber1;
                    break;
                }
            }

            return bufferNumber;
        }

        private int ScanDedicatedTxBuffers(out uint messageID)
        {
            var startAddress = (int)(txBuffers.StartAddress.Value << 2);
            var dataSizeInBytes = MapDataFieldSizeToDataBytes(txBuffers.DataFieldSize.Value);
            var bufferSizeInBytes = BufferElementHeaderSizeInBytes + dataSizeInBytes;

            messageID = uint.MaxValue; // Message ID of selected buffer
            var bufferNumber = -1; // Index of selected buffer

            for(var i = 0; i < (int)txBuffers.NumberOfDedicatedTxBuffers.Value; i++)
            {
                if(!rv.TxBufferRequestPending.TransmissionRequestPendingFlags[i].Value)
                {
                    continue;
                }

                var offset = startAddress + i * bufferSizeInBytes;
                byte[] scanBytes = messageRAM.ReadBytes((long)offset, 4);
                var scanRecord = Packet.Decode<TxScanBufferAndEventFIFOCommonHeader>(scanBytes);

                var id = scanRecord.Identifier;
                id = scanRecord.ExtendedIdentifier ? id : (id >> 18);

                if(id < messageID || (i == 0 && id == uint.MaxValue))
                {
                    // Found a message with higher priority
                    messageID = id;
                    bufferNumber = i;
                }
            }

            return bufferNumber;
        }

        private int ScanTxFIFO(out uint messageID)
        {
            var startAddress = (int)(txBuffers.StartAddress.Value << 2);
            var dataSizeInBytes = MapDataFieldSizeToDataBytes(txBuffers.DataFieldSize.Value);
            var bufferSizeInBytes = BufferElementHeaderSizeInBytes + dataSizeInBytes;

            messageID = uint.MaxValue; // Message ID of selected buffer
            var bufferNumber = -1; // Index of selected buffer

            var getIndex = (int)txFIFOQueue.GetIndex;

            if(!rv.TxBufferRequestPending.TransmissionRequestPendingFlags[getIndex].Value)
            {
                return bufferNumber;
            }

            var offset = startAddress + getIndex * bufferSizeInBytes;
            byte[] scanBytes = messageRAM.ReadBytes((long)offset, 4);
            var scanRecord = Packet.Decode<TxScanBufferAndEventFIFOCommonHeader>(scanBytes);

            var id = scanRecord.Identifier;
            id = scanRecord.ExtendedIdentifier ? id : (id >> 18);

            messageID = id;
            bufferNumber = getIndex;

            return bufferNumber;
        }

        private int ScanTxQueue(out uint messageID)
        {
            var startAddress = (int)(txBuffers.StartAddress.Value << 2);
            var dataSizeInBytes = MapDataFieldSizeToDataBytes(txBuffers.DataFieldSize.Value);
            var bufferSizeInBytes = BufferElementHeaderSizeInBytes + dataSizeInBytes;

            messageID = uint.MaxValue; // Message ID of selected buffer
            var bufferNumber = -1; // Index of selected buffer

            for(var i = (int)txFIFOQueue.Offset.Value; i < (int)(txFIFOQueue.Offset.Value + txFIFOQueue.Size.Value); i++)
            {
                if(!rv.TxBufferRequestPending.TransmissionRequestPendingFlags[i].Value)
                {
                    continue;
                }

                var offset = startAddress + i * bufferSizeInBytes;
                byte[] scanBytes = messageRAM.ReadBytes((long)offset, 4);
                var scanRecord = Packet.Decode<TxScanBufferAndEventFIFOCommonHeader>(scanBytes);

                var id = scanRecord.Identifier;
                id = scanRecord.ExtendedIdentifier ? id : (id >> 18);

                if(id < messageID || (i == 0 && id == uint.MaxValue))
                {
                    // Found a message with higher priority
                    messageID = id;
                    bufferNumber = i;
                }
            }

            return bufferNumber;
        }

        private void TransmitBuffer(int i)
        {
            var txBufferDataSize = MapDataFieldSizeToDataBytes(rv.TxBufferElementSizeConfiguration.TxBufferDataFieldSize.Value);
            var bufferSizeInBytes = BufferElementHeaderSizeInBytes + txBufferDataSize;

            if(!rv.TxBufferRequestPending.TransmissionRequestPendingFlags[i].Value)
            {
                return; // it was cancelled
            }

            var addr = (rv.TxBufferConfiguration.TxBuffersStartAddress.Value << 2) + (ulong)(i * bufferSizeInBytes);
            var frameBytes = messageRAM.ReadBytes((long)addr, (int)bufferSizeInBytes);
            var frame = Packet.Decode<TxBufferElementHeader>(frameBytes);
            var data = frameBytes.Skip(BufferElementHeaderSizeInBytes).Take((int)txBufferDataSize).ToArray();

            var dlcBufferSizeInBytes = MapDataLengthCodeToDataBytesCount(frame.DataLengthCode);
            Array.Resize(ref data, dlcBufferSizeInBytes);

            if(txBufferDataSize < dlcBufferSizeInBytes)
            {
                // The bytes not defined by the Tx Buffer are transmitted as “0xCC” (padding bytes).
                for(var j = (int)txBufferDataSize; j < dlcBufferSizeInBytes; j++)
                {
                    data[j] = PaddingByte;
                }
            }

            var canMessage = CANMessageFrame.CreateWithExtendedId(frame.Identifier, data, frame.ExtendedIdentifier, frame.RemoteTransmissionRequest, frame.FDFormat, frame.BitRateSwitch);

            var wasTransmitted = TransmitMessage(canMessage);
            if(wasTransmitted)
            {
                HandleTransmitSuccess(i, frame);
            }

            machine.LocalTimeSource.ExecuteInNearestSyncedState(__ =>
            {
                if(wasTransmitted)
                {
                    UpdateInterrupts();
                    TxScan();
                }
            }, true);
        }

        private void HandleTransmitSuccess(int i, TxBufferElementHeader frame)
        {
            rv.TxBufferRequestPending.TransmissionRequestPendingFlags[i].Value = false;
            rv.ProtocolStatusRegister.LastErrorCode.Value = LastErrorCode.NoError;
            HandleTxEvent(frame);

            if(!txFIFOQueue.QueueMode.Value && i >= (int)txFIFOQueue.Offset.Value)
            {
                // Transmitted message belonged to Tx FIFO
                txFIFOQueue.GetIndex++;
                txFIFOQueue.FullRaw.Value = false;
            }

            rv.TxBufferTransmissionOccurred.TransmissionOccurredFlags[i].Value = true;
        }

        private void HandleTxEvent(TxBufferElementHeader txHeader)
        {
            if(rv.CCControlRegister.ControlFields[(int)Control.WideMessageMarker].Value)
            {
                this.Log(LogLevel.Warning, "Wide Message Marker requires an external Time Stamping Unit (TSU) that is not available");
                return;
            }

            var txFIFOElement = new TxEventFIFOElement
            {
                Identifier = txHeader.Identifier,
                RemoteTransmissionRequest = txHeader.RemoteTransmissionRequest,
                ExtendedIdentifier = txHeader.ExtendedIdentifier,
                ErrorStateIndicator = txHeader.ErrorStateIndicator,
                TxTimestamp = 0,
                DataLengthCode = txHeader.DataLengthCode,
                BitRateSwitch = txHeader.BitRateSwitch,
                FDFormat = txHeader.FDFormat,
                EventType = 0b10, // Tx event
                MessageMarker = txHeader.MessageMarkerLow
            };

            StoreInTxEventFIFO(txFIFOElement);
        }

        private void StoreInTxEventFIFO(TxEventFIFOElement txFIFOElement)
        {
            if(txEventFIFO.Full.Value || txEventFIFO.Size.Value == 0)
            {
                txEventFIFO.InterruptElementLost.Value = true;
                txEventFIFO.ElementLost.Value = true;
                return;
            }

            var addr = (txEventFIFO.StartAddress.Value << 2) + txEventFIFO.PutIndex * (ulong)TxEventFIFOElementSizeInBytes;
            var txFIFOElementBytes = Packet.Encode(txFIFOElement);

            messageRAM.WriteBytes((long)addr, txFIFOElementBytes, 0, txFIFOElementBytes.Length);

            txEventFIFO.PutIndex += 1;
            txEventFIFO.Full.Value = txEventFIFO.PutIndexRaw == txEventFIFO.GetIndexRaw;

            var watermarkReached = (txEventFIFO.Watermark.Value > 0) && (txEventFIFO.FillLevel >= txEventFIFO.Watermark.Value);
            if(watermarkReached)
            {
                txEventFIFO.InterruptWatermarkReached.Value = true;
            }

            if(txEventFIFO.Full.Value)
            {
                txEventFIFO.InterruptFull.Value = true;
            }

            txEventFIFO.InterruptNewEntry.Value = true;
        }

        private bool TransmitMessage(CANMessageFrame canMessage)
        {
            var transmitted = true;
            var fs = FrameSent;
            if(fs != null)
            {
                fs.Invoke(canMessage);
            }
            else
            {
                transmitted = false;
                this.Log(LogLevel.Warning, "FrameSent is not initialized. Is the controller connected to medium?");
            }

            return transmitted;
        }

        private void HandleRxInner(CANMessageFrame rxMessage)
        {
            var match = FilterMessage(rxMessage);
            if(!match)
            {
                this.Log(LogLevel.Debug, "Received CAN message discarded");
            }
        }

        private bool FilterMessage(CANMessageFrame rxMessage)
        {
            var isExtended = rxMessage.ExtendedFormat;
            var isRemote = rxMessage.RemoteFrame;
            var id = rxMessage.Id;

            rv.ProtocolStatusRegister.ReceivedCANFDMessage.Value = rxMessage.FDFormat;

            var filterConfig = isExtended ? filterConfigurationExtended : filterConfigurationStandard;

            var rejectRemoteFrames = filterConfig.RejectRemoteFrames.Value;
            if(isRemote && rejectRemoteFrames)
            {
                return false; // discard remote frame
            }

            var listSize = filterConfig.ListSize.Value;
            var receiveFilterListEnabled = listSize > 0;

            var acceptNonMatchingFrames = filterConfig.AcceptNonMatchingFrames.Value;
            var rejectNonMatchingFrames = acceptNonMatchingFrames != NonMatchingFrameTarget.AcceptInRxFIFO0 && acceptNonMatchingFrames != NonMatchingFrameTarget.AcceptInRxFIFO1;
            if(!receiveFilterListEnabled && rejectNonMatchingFrames)
            {
                return false; // discard non-matching frame
            }

            if(receiveFilterListEnabled)
            {
                var startAddress = filterConfig.FilterListStartAddress.Value;
                var filterSizeInBytes = isExtended ? ExtendedFilterSizeInBytes : StandardFilterSizeInBytes;

                var match = false;
                for(var i = 0; i < (int)listSize; i++)
                {
                    var addr = (long)(startAddress << 2) + i * filterSizeInBytes;
                    var filterBytes = messageRAM.ReadBytes(addr, filterSizeInBytes);
                    var filter = DecodeFilterElement(isExtended, filterBytes);

                    if(filter.FilterElementConfiguration == FilterElementConfiguration.DisableFilter)
                    {
                        continue; // filter element disabled
                    }
                    else if(filter.FilterElementConfiguration == FilterElementConfiguration.RxBufferOrDebugMessageOnMatch)
                    {
                        var rxBufferIdx = BitHelper.GetValue(filter.ID2, 0, 6);
                        var newDataFlags = rxBufferIdx < 32 ? rv.NewData1.NewData1Flags : rv.NewData2.NewData2Flags;
                        if(newDataFlags[rxBufferIdx % 32].Value)
                        {
                            // While an Rx Buffer’s New Data flag is set, a Message ID Filter Element
                            // referencing this specific RxBuffer will not match.
                            continue;
                        }
                    }

                    match = MatchFilterElement(isExtended, id, filter);

                    if(!match)
                    {
                        continue;
                    }

                    HandleMatchedFilter(filter, i, rxMessage);

                    return true;
                }

                if(!match && rejectNonMatchingFrames)
                {
                    return false; // discard non-matching frame
                }
            }

            AcceptNonMatchingFrame(acceptNonMatchingFrames, rxMessage);

            return true;
        }

        private MessageIDFilterElement DecodeFilterElement(bool extended, byte[] filterBytes)
        {
            if(extended)
            {
                var filter = Packet.Decode<ExtendedMessageIDFilterElement>(filterBytes);
                return new MessageIDFilterElement
                {
                    ID1 = filter.ExtendedFilterID1,
                    ID2 = filter.ExtendedFilterID2,
                    SyncMessage = filter.ExtendedSyncMessage,
                    FilterElementConfiguration = filter.ExtendedFilterElementConfiguration,
                    FilterType = filter.ExtendedFilterType,
                    IsExtended = true
                };
            }
            else
            {
                var filter = Packet.Decode<StandardMessageIDFilterElement>(filterBytes);
                return new MessageIDFilterElement
                {
                    ID1 = filter.StandardFilterID1,
                    ID2 = filter.StandardFilterID2,
                    SyncMessage = filter.StandardSyncMessage,
                    FilterElementConfiguration = filter.StandardFilterElementConfiguration,
                    FilterType = filter.StandardFilterType,
                    IsExtended = false
                };
            }
        }

        private bool MatchFilterElement(bool isExtended, uint id, MessageIDFilterElement filter)
        {
            switch(filter.FilterType)
            {
                case FilterType.Range:
                {
                    if(!isExtended)
                    {
                        if(id >= filter.ID1 && id <= filter.ID2)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        var idMasked = id & (uint)rv.ExtendedIdANDMask.ExtendedIDANDMask.Value;
                        if(idMasked >= filter.ID1 && idMasked <= filter.ID2)
                        {
                            return true;
                        }
                    }
                    break;
                }
                case FilterType.DualID:
                {
                    if((id == filter.ID1) || (id == filter.ID2))
                    {
                        return true;
                    }
                    break;
                }
                case FilterType.Classic:
                {
                    if((id & filter.ID2) == (filter.ID1 & filter.ID2))
                    {
                        return true;
                    }
                    break;
                }
                case FilterType.RangeWithoutMask:
                {
                    if(!isExtended)
                    {
                        break;
                    }
                    if(id >= filter.ID1 && id <= filter.ID2)
                    {
                        return true;
                    }
                    break;
                }
                default:
                {
                    this.Log(LogLevel.Warning, "Invalid filter type.");
                    break;
                }
            }
            return false;
        }

        private void HandleMatchedFilter(MessageIDFilterElement filter, int idx, CANMessageFrame rxMessage)
        {
            var rxBufferElementHeader = new RxBufferElementHeader
            {
                Identifier = rxMessage.ExtendedId,
                RemoteTransmissionRequest = rxMessage.RemoteFrame,
                ExtendedIdentifier = rxMessage.ExtendedFormat,
                ErrorStateIndicator = false,
                RxTimestamp = 0,
                DataLengthCode = MapDataBytesCountToDataLengthCode(k: rxMessage.Data.Length),
                BitRateSwitch = rxMessage.BitRateSwitch,
                FDFormat = rxMessage.FDFormat,
                FilterIndex = (byte)idx,
                AcceptedNonMatchingFrame = false
            };

            switch(filter.FilterElementConfiguration)
            {
                case FilterElementConfiguration.RxFIFO0OnMatch:
                {
                    var wasStored = StoreInRxFIFO(0, rxBufferElementHeader, rxMessage.Data);
                    break;
                }
                case FilterElementConfiguration.RxFIFO1OnMatch:
                {
                    var wasStored = StoreInRxFIFO(1, rxBufferElementHeader, rxMessage.Data);
                    break;
                }
                case FilterElementConfiguration.RejectIDOnMatch:
                {
                    if(filter.SyncMessage)
                    {
                        this.Log(LogLevel.Warning, "Reject ID if filter matches is not intended to be used with Sync messages");
                    }
                    break;
                }
                case FilterElementConfiguration.SetPriorityOnMatch:
                {
                    if(filter.SyncMessage)
                    {
                        this.Log(LogLevel.Warning, "Setting priority on filter match is not intended to be used with Sync messages");
                        return;
                    }

                    rv.HighPriorityMessageStatus.MessageStorageIndicator.Value = MessageStorageIndicator.NoFIFOselected;
                    rv.HighPriorityMessageStatus.FilterIndex.Value = (ulong)idx;
                    rv.HighPriorityMessageStatus.FilterList.Value = filter.IsExtended;

                    rv.InterruptRegister.InterruptFlags[(int)Interrupt.HighPriorityMessage].Value = true;
                    break;
                }
                case FilterElementConfiguration.SetPriorityAndRxFIFO0OnMatch:
                {
                    var wasStored = StoreInRxFIFO(0, rxBufferElementHeader, rxMessage.Data);

                    if(wasStored)
                    {
                        rv.HighPriorityMessageStatus.BufferIndex.Value = rv.RxFIFO0Status.RxFIFO0PutIndex.Value - 1; // Put Index was already incremented for new message so subtract one
                        rv.HighPriorityMessageStatus.MessageStorageIndicator.Value = MessageStorageIndicator.MessageInFIFO0;
                    }
                    else
                    {
                        rv.HighPriorityMessageStatus.MessageStorageIndicator.Value = MessageStorageIndicator.FIFOMessageLost;
                    }
                    rv.HighPriorityMessageStatus.FilterIndex.Value = (ulong)idx;
                    rv.HighPriorityMessageStatus.FilterList.Value = filter.IsExtended;

                    rv.InterruptRegister.InterruptFlags[(int)Interrupt.HighPriorityMessage].Value = true;
                    break;
                }
                case FilterElementConfiguration.SetPriorityAndRxFIFO1OnMatch:
                {
                    var wasStored = StoreInRxFIFO(1, rxBufferElementHeader, rxMessage.Data);

                    if(wasStored)
                    {
                        rv.HighPriorityMessageStatus.BufferIndex.Value = rv.RxFIFO1Status.RxFIFO1PutIndex.Value - 1; // Put Index was already incremented for new message so subtract one
                        rv.HighPriorityMessageStatus.MessageStorageIndicator.Value = MessageStorageIndicator.MessageInFIFO1;
                    }
                    else
                    {
                        rv.HighPriorityMessageStatus.MessageStorageIndicator.Value = MessageStorageIndicator.FIFOMessageLost;
                    }
                    rv.HighPriorityMessageStatus.FilterIndex.Value = (ulong)idx;
                    rv.HighPriorityMessageStatus.FilterList.Value = filter.IsExtended;

                    rv.InterruptRegister.InterruptFlags[(int)Interrupt.HighPriorityMessage].Value = true;
                    break;
                }
                case FilterElementConfiguration.RxBufferOrDebugMessageOnMatch:
                {
                    StoreInRxBuffer(rxBufferElementHeader, rxMessage.Data, filter);
                    break;
                }
                default:
                {
                    this.Log(LogLevel.Warning, "Invalid Filter Element Configuration");
                    break;
                }
            }
        }

        private void AcceptNonMatchingFrame(NonMatchingFrameTarget frameTarget, CANMessageFrame rxMessage)
        {
            var rxBufferElementHeader = new RxBufferElementHeader
            {
                Identifier = rxMessage.ExtendedId,
                RemoteTransmissionRequest = rxMessage.RemoteFrame,
                ExtendedIdentifier = rxMessage.ExtendedFormat,
                ErrorStateIndicator = false,
                RxTimestamp = 0,
                DataLengthCode = MapDataBytesCountToDataLengthCode(k: rxMessage.Data.Length),
                BitRateSwitch = rxMessage.BitRateSwitch,
                FDFormat = rxMessage.FDFormat,
                FilterIndex = 0,
                AcceptedNonMatchingFrame = true
            };

            switch(frameTarget)
            {
                case NonMatchingFrameTarget.AcceptInRxFIFO0:
                {
                    StoreInRxFIFO(0, rxBufferElementHeader, rxMessage.Data);
                    break;
                }
                case NonMatchingFrameTarget.AcceptInRxFIFO1:
                {
                    StoreInRxFIFO(1, rxBufferElementHeader, rxMessage.Data);
                    break;
                }
                default:
                {
                    this.Log(LogLevel.Warning, "Non-matching frame was rejected");
                    break;
                }
            }
        }

        private bool StoreInRxFIFO(uint idx, RxBufferElementHeader rxHeader, byte[] rxData)
        {
            if(idx >= NumberOfRxFIFOs)
            {
                this.Log(LogLevel.Warning, "Only FIFO0 or FIFO1 are available");
            }

            var rxFIFO = idx == 0 ? rxFIFO0 : rxFIFO1;

            if(rxFIFO.Size.Value == 0)
            {
                rxFIFO.InterruptMessageLost.Value = true;
                rxFIFO.MessageLost.Value = true;
                return false; // Message is discarded
            }

            var dataFieldInBytes = MapDataFieldSizeToDataBytes(rxFIFO.DataFieldSize.Value);
            var fifoElementSizeInBytes = BufferElementHeaderSizeInBytes + dataFieldInBytes;

            var addr = (rxFIFO.StartAddress.Value << 2) + rxFIFO.RxFIFOPutIndex * (ulong)fifoElementSizeInBytes;
            var rxHeaderBytes = Packet.Encode(rxHeader);

            switch(rxFIFO.OperationMode.Value)
            {
                case FIFOOperationMode.Overwrite:
                {
                    if(rxFIFO.Full.Value)
                    {
                        rxFIFO.RxFIFOGetIndex += 1;
                    }
                    break;
                }
                case FIFOOperationMode.Blocking:
                default:
                {
                    if(rxFIFO.Full.Value)
                    {
                        rxFIFO.InterruptMessageLost.Value = true;
                        rxFIFO.MessageLost.Value = true;
                        return false; // Message is discarded
                    }
                    break;
                }
            }

            messageRAM.WriteBytes((long)addr, rxHeaderBytes, 0, rxHeaderBytes.Length);
            messageRAM.WriteBytes((long)addr + rxHeaderBytes.Length, rxData, 0, rxData.Length);

            rxFIFO.RxFIFOPutIndex += 1;
            rxFIFO.Full.Value = rxFIFO.RxFIFOPutIndex == rxFIFO.RxFIFOGetIndex;

            var watermarkReached = (rxFIFO.Watermark.Value > 0) && (rxFIFO.RxFIFOFillLevel >= rxFIFO.Watermark.Value);
            if(watermarkReached)
            {
                rxFIFO.InterruptWatermarkReached.Value = true;
            }

            if(rxFIFO.Full.Value)
            {
                rxFIFO.InterruptFull.Value = true;
            }

            rxFIFO.InterruptNewMessage.Value = true;

            return true;
        }

        private void StoreInRxBuffer(RxBufferElementHeader rxHeader, byte[] rxData, MessageIDFilterElement filter)
        {
            var id2 = filter.ID2;

            var rxBufferIdx = BitHelper.GetValue(id2, 0, 6);
            var filterEventPins = BitHelper.GetValue(id2, 6, 3);
            var target = (RxBufferOrDebugDestination)BitHelper.GetValue(id2, 9, 2);

            var startAddress = rxBuffer.StartAddress.Value << 2;
            var dataFieldSize = rxBuffer.DataFieldSize.Value;

            var dataFieldInBytes = MapDataFieldSizeToDataBytes(dataFieldSize);
            var bufferElementSizeInBytes = BufferElementHeaderSizeInBytes + dataFieldInBytes;

            var addr = startAddress + rxBufferIdx * (ulong)bufferElementSizeInBytes;
            var rxHeaderBytes = Packet.Encode(rxHeader);

            messageRAM.WriteBytes((long)addr, rxHeaderBytes, 0, rxHeaderBytes.Length);
            messageRAM.WriteBytes((long)addr + rxHeaderBytes.Length, rxData, 0, rxData.Length);

            switch(target)
            {
                case RxBufferOrDebugDestination.StoreInRxBuffer:
                {
                    var newDataFlags = rxBufferIdx < 32 ? rv.NewData1.NewData1Flags : rv.NewData2.NewData2Flags;
                    newDataFlags[rxBufferIdx % 32].Value = true;

                    rv.InterruptRegister.InterruptFlags[(int)Interrupt.MessageStoredToDedicatedRxBuffer].Value = true;
                    break;
                }
                case RxBufferOrDebugDestination.DebugMessageA:
                case RxBufferOrDebugDestination.DebugMessageB:
                case RxBufferOrDebugDestination.DebugMessageC:
                default:
                {
                    this.Log(LogLevel.Warning, "DMU add-on is required to activate DMA request output after debug message is stored");
                    break;
                }
            }
        }

        private int MapDataFieldSizeToDataBytes(ulong k)
        {
            if(k >= 8)
            {
                this.Log(LogLevel.Warning, "Invalid Data Field Size");
                return 0;
            }

            DataFieldSizeToBytesCountMap.TryGetValue(k, out var fieldSize);
            return fieldSize;
        }

        private byte MapDataBytesCountToDataLengthCode(int k)
        {
            if(k <= 8)
            {
                return (byte)k;
            }

            if(k > 64)
            {
                this.Log(LogLevel.Warning, "Received frame has more than 64 bytes");
                return 0;
            }
            var success = FDBytesCountToDataLengthCodeMap.TryGetValue(k, out var datalengthCode);

            if(!success)
            {
                this.Log(LogLevel.Warning, "Invalid length of received frame");
                return 0;
            }

            return datalengthCode;
        }

        private byte MapDataLengthCodeToDataBytesCount(int k)
        {
            if(k <= 8)
            {
                return (byte)k;
            }

            if(k > 15)
            {
                this.Log(LogLevel.Warning, "Frame specfied an invalid Data Length Code");
                return 0;
            }

            DataLengthCodeToFDBytesCountMap.TryGetValue(k, out var fdBytesCount);
            return fdBytesCount;
        }

        private void UpdateInterrupts()
        {
            var flag0 = false;
            var flag1 = false;

            for(int i = 0; i < rv.TxBufferTransmissionInterruptEnable.TransmissionInterruptEnableFlags.Length; i++)
            {
                if(rv.TxBufferTransmissionInterruptEnable.TransmissionInterruptEnableFlags[i].Value && rv.TxBufferTransmissionOccurred.TransmissionOccurredFlags[i].Value)
                {
                    rv.InterruptRegister.InterruptFlags[(int)Interrupt.TransmissionCompleted].Value = true;
                }
            }

            for(int i = 0; i < rv.TxBufferCancellationFinishedInterruptEnable.CancellationFinishedInterruptEnableFlags.Length; i++)
            {
                if(rv.TxBufferCancellationFinishedInterruptEnable.CancellationFinishedInterruptEnableFlags[i].Value && rv.TxBufferCancellationFinished.CancellationFinishedFlags[i].Value)
                {
                    rv.InterruptRegister.InterruptFlags[(int)Interrupt.TransmissionCancellationFinished].Value = true;
                }
            }

            for(int i = 0; i < rv.InterruptRegister.InterruptFlags.Length; i++)
            {
                if(rv.InterruptEnable.InterruptEnableFlags[i].Value && rv.InterruptRegister.InterruptFlags[i].Value)
                {
                    flag0 |= rv.InterruptLineEnable.EnableInterruptLine0.Value && !rv.InterruptLineSelect.InterruptLineSelectFlags[i].Value;
                    flag1 |= rv.InterruptLineEnable.EnableInterruptLine1.Value && rv.InterruptLineSelect.InterruptLineSelectFlags[i].Value;
                }
            }

            Line0.Set(flag0);
            Line1.Set(flag1);
        }
    }
}
