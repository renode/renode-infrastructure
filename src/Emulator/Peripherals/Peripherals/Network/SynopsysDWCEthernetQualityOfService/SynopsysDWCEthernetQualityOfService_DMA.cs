//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using PacketDotNet;

using IPProtocolType = PacketDotNet.IPProtocolType;

namespace Antmicro.Renode.Peripherals.Network
{
    public partial class SynopsysDWCEthernetQualityOfService
    {
        protected readonly DMAChannel[] dmaChannels;

        public enum BusWidth
        {
            Bits32  = 4,
            Bits64  = 8,
            Bits128 = 16,
        }

        protected enum DMAChannelInterruptMode
        {
            Pulse            = 0b00,
            Level            = 0b01,
            LevelAndReassert = 0b10,
            Reserved         = 0b11,
        }

        private enum DMAState
        {
            Stopped = 0,
            Running = 1,
            ProcessingIntermediate = 2,
            ProcessingSecond = 4,
            Suspended = 8,
        }

        protected class DMAChannel
        {
            public DMAChannel(SynopsysDWCEthernetQualityOfService parent, int channelNumber, long systemClockFrequency, bool hasInterrupts)
            {
                this.parent = parent;
                this.channelNumber = channelNumber;
                this.hasInterrupts = hasInterrupts;

                if(hasInterrupts)
                {
                    TxIRQ = new GPIO();
                    RxIRQ = new GPIO();
                }

                incomingFrames = new Queue<EthernetFrame>();
                rxWatchdog = new LimitTimer(parent.machine.ClockSource, systemClockFrequency, parent, $"DMA Channel {channelNumber}: rx-watchdog", enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true, divider: RxWatchdogDivider);
                rxWatchdog.LimitReached += delegate
                {
                    this.Log(LogLevel.Noisy, "Receive: Watchdog reached limit.");
                    rxInterrupt.Value = true;
                    parent.UpdateInterrupts();
                };
            }

            public void Reset()
            {
                rxFinishedRing = true;
                txFinishedRing = true;
                txState = DMAState.Stopped;
                rxState = DMAState.Stopped;
                rxOffset = 0;
                latestTxContext = null;
                rxWatchdog.Reset();
                incomingFrames.Clear();
                rxQueueLength = 0;
                frameAssembler = null;
            }

            public void DefineChannelRegisters(ref Dictionary<long, DoubleWordRegister> map)
            {
                var offset = parent.DMAChannelOffsets[channelNumber];
                map = map.Concat(new Dictionary<long, DoubleWordRegister>()
                {
                    {(long)RegistersDMAChannel.Control + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 14, out maximumSegmentSize, name: "DMACCR.MSS (Maximum Segment Size)")
                        .WithReservedBits(14, 2)
                        .WithFlag(16, out programmableBurstLengthTimes8, name: "DMACCR.PBLX8 (8xPBL mode)")
                        .WithReservedBits(17, 1)
                        .WithValueField(18, 3, out descriptorSkipLength, name: "DMACCR.DSL (Descriptor Skip Length)")
                        .WithReservedBits(21, 11)
                    },
                    {(long)RegistersDMAChannel.TransmitControl + offset, new DoubleWordRegister(parent)
                        .WithFlag(0, out startTx, changeCallback: (_, __) =>
                        {
                            if(startTx.Value)
                            {
                                txFinishedRing = txDescriptorRingCurrent.Value == txDescriptorRingTail.Value;
                                StartTx();
                            }
                        },
                        name: "DMACTxCR.ST (Start or Stop Transmission Command)")
                        .WithReservedBits(1, 3)
                        .WithFlag(4, out operateOnSecondPacket, name: "DMACTxCR.OSF (Operate on Second Packet)")
                        .WithReservedBits(5, 7)
                        .WithFlag(12, out tcpSegmentationEnable, name: "DMACTxCR.TSE (TCP Segmentation Enabled)")
                        .WithReservedBits(13, 3)
                        .WithValueField(16, 6, out txProgrammableBurstLength, name: "DMACTxCR.TXPBL (Transmit Programmable Burst Length)")
                        .WithReservedBits(22, 10)
                    },
                    {(long)RegistersDMAChannel.ReceiveControl + offset, new DoubleWordRegister(parent)
                        .WithFlag(0, out startRx, changeCallback: (_, __) =>
                        {
                            if(startRx.Value)
                            {
                                rxFinishedRing = rxDescriptorRingCurrent.Value == rxDescriptorRingTail.Value;
                                StartRx();
                            }
                        },
                        name: "DMACRxCR.SR (Start or Stop Receive Command)")
                        .WithValueField(1, 14, out rxBufferSize, writeCallback: (_, __) =>
                        {
                            if(rxBufferSize.Value % 4 != 0)
                            {
                                this.Log(LogLevel.Warning, "Receive buffer size must be a multiple of 4. Ignoring LSBs, but this behavior is undefined.");
                                rxBufferSize.Value &= ~0x3UL;
                            }
                        },
                        name: "DMACRxCR.RBSZ (Receive Buffer size)")
                        .WithReservedBits(15, 1)
                        .WithValueField(16, 6, out rxProgrammableBurstLength, name: "DMACRxCR.RXPBL (RXPBL)")
                        .WithReservedBits(22, 9)
                        .WithTaggedFlag("DMACRxCR.RPF (DMA Rx Channel Packet Flush)", 31)
                    },
                    {(long)RegistersDMAChannel.TxDescriptorListAddress  + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out txDescriptorRingStart, writeCallback: (_, __) =>
                        {
                            txDescriptorRingCurrent.Value = txDescriptorRingStart.Value;
                        },
                        name: "DMACTxDLAR.TDESLA (Start of Transmit List)")
                    },
                    {(long)RegistersDMAChannel.RxDescriptorListAddress + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out rxDescriptorRingStart, writeCallback: (_, __) =>
                        {
                            rxDescriptorRingCurrent.Value = rxDescriptorRingStart.Value;
                        },
                        name: "DMACRxDLAR.RDESLA (Start of Receive List)")
                    },
                    {(long)RegistersDMAChannel.TxDescriptorTailPointer + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out txDescriptorRingTail, changeCallback: (previousValue, _) =>
                        {
                            var clearTxFinishedRing = txDescriptorRingTail.Value != txDescriptorRingCurrent.Value;
                            if((txState & DMAState.Suspended) != 0 || clearTxFinishedRing)
                            {
                                txFinishedRing &= !clearTxFinishedRing;
                                StartTx();
                            }
                            this.Log(LogLevel.Debug, "Transmit Tail register (DMACTxDTPR.TDT) set to: 0x{0:X}", txDescriptorRingTail.Value);
                        }, name: "DMACTxDTPR.TDT (Transmit Descriptor Tail Pointer)")
                    },
                    {(long)RegistersDMAChannel.RxDescriptorTailPointer + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out rxDescriptorRingTail, changeCallback: (previousValue, _) =>
                        {
                            var clearRxFinishedRing = rxDescriptorRingTail.Value != rxDescriptorRingCurrent.Value;
                            if((rxState & DMAState.Suspended) != 0 || clearRxFinishedRing)
                            {
                                rxFinishedRing &= !clearRxFinishedRing;
                                StartRx();
                            }
                            this.Log(LogLevel.Debug, "Receive Tail register (DMACRxDTPR.RDT) set to: 0x{0:X}", rxDescriptorRingTail.Value);
                        }, name: "DMACRxDTPR.RDT (Receive Descriptor Tail Pointer)")
                    },
                    {(long)RegistersDMAChannel.TxDescriptorRingLength + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 10, out txDescriptorRingLength, name: "DMACTxRLR.TDRL (Transmit Descriptor Ring Length)")
                        .WithReservedBits(10, 22)
                    },
                    {(long)RegistersDMAChannel.RxDescriptorRingLength + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 10, out rxDescriptorRingLength, name: "DMACRxRLR.RDRL (Receive Descriptor Ring Length)")
                        .WithReservedBits(10, 6)
                        .WithValueField(16, 8, out alternateRxBufferSize, name: "DMACRxRLR.ARBS (Alternate Receive Buffer Size)")
                        .WithReservedBits(24, 8)
                    },
                    {(long)RegistersDMAChannel.InterruptEnable + offset, new DoubleWordRegister(parent)
                        .WithFlag(0, out txInterruptEnable, name: "DMACIER.TIE (Transmit Interrupt Enable)")
                        .WithFlag(1, out txProcessStoppedEnable, name: "DMACIER.TXSE (Transmit Stopped Enable)")
                        .WithFlag(2, out txBufferUnavailableEnable, name: "DMACIER.TBUE (Transmit Buffer Unavailable Enable)")
                        .WithReservedBits(3, 3)
                        .WithFlag(6, out rxInterruptEnable, name: "DMACIER.RIE (Receive Interrupt Enable)")
                        .WithFlag(7, out rxBufferUnavailableEnable, name: "DMACIER.RBUE (Receive Buffer Unavailable Enable)")
                        .WithFlag(8, out rxProcessStoppedEnable, name: "DMACIER.RSE (Receive Stopped Enable)")
                        .WithFlag(9, out rxWatchdogTimeoutEnable, name: "DMACIER.RWTE (Receive Watchdog Timeout Enable)")
                        .WithFlag(10, out earlyTxInterruptEnable, name: "DMACIER.ETIE (Early Transmit Interrupt Enable)")
                        .WithFlag(11, out earlyRxInterruptEnable, name: "DMACIER.ERIE (Early Receive Interrupt Enable)")
                        .WithFlag(12, out fatalBusErrorEnable, name: "DMACIER.FBEE (Fatal Bus Error Enable)")
                        .WithFlag(13, out contextDescriptorErrorEnable, name: "DMACIER.CDEE (Context Descriptor Error Enable)")
                        .WithFlag(14, out abnormalInterruptSummaryEnable, name: "DMACIER.AIE (Abnormal Interrupt Summary Enable)")
                        .WithFlag(15, out normalInterruptSummaryEnable, name: "DMACIER.NIE (Normal Interrupt Summary Enable)")
                        .WithReservedBits(16, 16)
                        .WithChangeCallback((_, __) => parent.UpdateInterrupts())
                    },
                    {(long)RegistersDMAChannel.RxInterruptWatchdogTimer + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 8, out rxWatchdogCounter, changeCallback: (_, __) =>
                        {
                            rxWatchdog.Limit = rxWatchdogCounter.Value;
                        },
                        name: "DMACRxIWTR.RWT (Receive Interrupt Watchdog Timer Count)")
                        .WithReservedBits(8, 8)
                        .WithValueField(16, 2, out rxWatchdogCounterUnit, changeCallback: (_, __) =>
                        {
                            rxWatchdog.Divider = RxWatchdogDivider << (byte)rxWatchdogCounterUnit.Value;
                        },
                        name: "DMACRxIWTR.RWTU (Receive Interrupt Watchdog Timer Count Units)")
                        .WithReservedBits(18, 14)
                    },
                    {(long)RegistersDMAChannel.CurrentApplicationTransmitDescriptor + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out txDescriptorRingCurrent, FieldMode.Read, name: "DMACCATxDR.CURTDESAPTR (Application Transmit Descriptor Address Pointer)")
                    },
                    {(long)RegistersDMAChannel.CurrentApplicationReceiveDescriptor + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out rxDescriptorRingCurrent, FieldMode.Read, name: "DMACCARxDR.CURRDESAPTR (Application Receive Descriptor Address Pointer)")
                    },
                    {(long)RegistersDMAChannel.CurrentApplicationTransmitBuffer + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out txCurrentBuffer, FieldMode.Read, name: "DMACCATxBR.CURTBUFAPTR (Application Transmit Buffer Address Pointer)")
                    },
                    {(long)RegistersDMAChannel.CurrentApplicationReceiveBuffer + offset, new DoubleWordRegister(parent)
                        .WithValueField(0, 32, out rxCurrentBuffer, FieldMode.Read, name: "DMACCARxBR.CURRBUFAPTR (Application Receive Buffer Address Pointer)")
                    },
                    {(long)RegistersDMAChannel.Status + offset, new DoubleWordRegister(parent)
                        .WithFlag(0, out txInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.TI (Transmit Interrupt)")
                        .WithFlag(1, out txProcessStopped, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.TPS (Transmit Process Stopped)")
                        .WithFlag(2, out txBufferUnavailable, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.TBU (Transmit Buffer Unavailable)")
                        .WithReservedBits(3, 3)
                        .WithFlag(6, out rxInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RI (Receive Interrupt)")
                        .WithFlag(7, out rxBufferUnavailable, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RBU (Receive Buffer Unavailable)")
                        .WithFlag(8, out rxProcessStopped, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RPS (Receive Process Stopped)")
                        .WithFlag(9, out rxWatchdogTimeout, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.RWT (Receive Watchdog Timeout)")
                        .WithFlag(10, out earlyTxInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.ET (Early Transmit Interrupt)")
                        .WithFlag(11, out earlyRxInterrupt, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.ER (Early Receive Interrupt)")
                        .WithFlag(12, out fatalBusError, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.FBE (Fatal Bus Error)")
                        .WithFlag(13, out contextDescriptorError, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.CDE (Context Descriptor Error)")
                        .WithFlag(14, out abnormalInterruptSummary, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.AIS (Abnormal Interrupt Summary)")
                        .WithFlag(15, out normalInterruptSummary, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMACSR.NIS (Normal Interrupt Summary)")
                        .WithTag("DMACSR.TEB (Tx DMA Error Bits)", 16, 3)
                        .WithTag("DMACSR.REB (Rx DMA Error Bits)", 19, 3)
                        .WithReservedBits(22, 10)
                        .WithChangeCallback((_, __) => parent.UpdateInterrupts())
                    },
                    {(long)RegistersDMAChannel.MissedFrameCount + offset, new DoubleWordRegister(parent)
                        .WithTag("DMACMFCR.MFC (Dropped Packet Counters)", 0, 11)
                        .WithReservedBits(11, 4)
                        .WithTaggedFlag("DMACMFCR.MFCO (Overflow status of the MFC Counter)", 15)
                        .WithReservedBits(16, 16)
                    }
                }).ToDictionary(x => x.Key, x => x.Value);
            }

            public void ReceiveFrame(EthernetFrame frame)
            {
                if(rxQueueLength + frame.Length > parent.RxQueueSize)
                {
                    parent.IncrementPacketCounter(parent.rxFifoPacketCounter, parent.rxFifoPacketCounterInterrupt);
                    this.Log(LogLevel.Debug, "Receive: Dropping overflow frame {0}", frame);
                    parent.UpdateInterrupts();
                    return;
                }

                this.Log(LogLevel.Debug, "Receive: Incoming frame {0}", frame);
                incomingFrames.Enqueue(frame);
                rxQueueLength += frame.Bytes.Length;
                StartRx();
            }

            public bool UpdateInterrupts()
            {
                // Depending on the DMA interrupt mode transmit/receive completed condition may not
                // contribute to the common interrupt. Those masks are used to block those conditions from
                // influencing the common interrupt state.
                var txMask = true;
                var rxMask = true;

                if(hasInterrupts)
                {
                    switch(parent.dmaInterruptMode.Value)
                    {
                        case DMAChannelInterruptMode.Pulse:
                            if(txInterrupt.Value)
                            {
                                TxIRQ.Blink();
                                this.Log(LogLevel.Debug, "Blinking TxIRQ");
                            }
                            if(rxInterrupt.Value)
                            {
                                RxIRQ.Blink();
                                this.Log(LogLevel.Debug, "Blinking RxIRQ");
                            }
                            break;
                        case DMAChannelInterruptMode.Level:
                        case DMAChannelInterruptMode.LevelAndReassert:
                        {
                            var txState = txInterrupt.Value && txInterruptEnable.Value && normalInterruptSummaryEnable.Value;
                            var rxState = rxInterrupt.Value && rxInterruptEnable.Value && normalInterruptSummaryEnable.Value;
                            TxIRQ.Set(txState);
                            RxIRQ.Set(rxState);
                            this.Log(LogLevel.Debug, "TxIRQ: {0}, RxIRQ: {1}", txState ? "setting" : "unsetting", rxState ? "setting" : "unsetting");
                            // In both Level and LevelAndReassert transmit/receive completed conditions are only used for channel specific interrupts
                            // and don't contribute to the common interrupt. Mask their influence
                            txMask = false;
                            rxMask = false;
                            break;
                        }
                        default:
                            this.Log(LogLevel.Warning, "Invalid interrupt mode value {0}", parent.dmaInterruptMode.Value);
                            break;
                    }
                }

                normalInterruptSummary.Value |= GetNormalInterruptSummary();
                abnormalInterruptSummary.Value |= (txProcessStopped.Value && txProcessStoppedEnable.Value) ||
                                                  (rxBufferUnavailable.Value && rxBufferUnavailableEnable.Value) ||
                                                  (rxProcessStopped.Value && rxProcessStoppedEnable.Value) ||
                                                  (earlyTxInterrupt.Value && earlyTxInterruptEnable.Value) ||
                                                  (fatalBusError.Value && fatalBusErrorEnable.Value) ||
                                                  (contextDescriptorError.Value && contextDescriptorErrorEnable.Value);

                return (rxWatchdogTimeout.Value && rxWatchdogTimeoutEnable.Value)               ||
                       (abnormalInterruptSummary.Value && abnormalInterruptSummaryEnable.Value) ||
                       (GetNormalInterruptSummary(txMask, rxMask) && normalInterruptSummaryEnable.Value);
            }

            public DMATxProcessState DmaTxState => (txState == DMAState.Stopped) ? DMATxProcessState.Stopped : DMATxProcessState.Suspended;
            public DMARxProcessState DmaRxState => (rxState == DMAState.Stopped) ? DMARxProcessState.Stopped :
                                            ((incomingFrames.Count == 0) ? DMARxProcessState.WaitingForPacket : DMARxProcessState.Suspended);

            public bool Interrupts =>
                txInterrupt.Value ||
                txProcessStopped.Value ||
                txBufferUnavailable.Value ||
                rxInterrupt.Value ||
                rxBufferUnavailable.Value ||
                rxProcessStopped.Value ||
                rxWatchdogTimeout.Value ||
                earlyTxInterrupt.Value ||
                earlyRxInterrupt.Value ||
                fatalBusError.Value ||
                abnormalInterruptSummary.Value ||
                normalInterruptSummary.Value;

            public GPIO TxIRQ { get; }
            public GPIO RxIRQ { get; }

            private TxDescriptor GetTxDescriptor(ulong index = 0)
            {
                var descriptor = new TxDescriptor(parent.Bus, txDescriptorRingCurrent.Value, parent.CpuContext);
                descriptor.Fetch();
                return descriptor;
            }

            private RxDescriptor GetRxDescriptor()
            {
                var descriptor = new RxDescriptor(parent.Bus, rxDescriptorRingCurrent.Value, parent.CpuContext);
                descriptor.Fetch();
                return descriptor;
            }

            private void IncreaseTxDescriptorPointer()
            {
                IncreaseDescriptorPointer(txDescriptorRingCurrent, txDescriptorRingStart, txDescriptorRingLength, "TX");
                txFinishedRing = txDescriptorRingCurrent.Value == txDescriptorRingTail.Value;
            }

            private void IncreaseRxDescriptorPointer()
            {
                IncreaseDescriptorPointer(rxDescriptorRingCurrent, rxDescriptorRingStart, rxDescriptorRingLength, "RX");
                rxFinishedRing = rxDescriptorRingCurrent.Value == rxDescriptorRingTail.Value;
            }

            private void IncreaseDescriptorPointer(IValueRegisterField current, IValueRegisterField start, IValueRegisterField length, string name)
            {
                var size = descriptorSkipLength.Value * (ulong)parent.DMABusWidth + Descriptor.Size;
                var offset = current.Value - start.Value;
                offset += size;
                // The docs state that: "If you want to have 10 descriptors, program it to a value of 0x9" - so it always should be +1 descriptor than obtained from the register
                offset %= (length.Value + 1) * size;
                this.Log(LogLevel.Noisy, "{0} Descriptor pointer was 0x{1:X}, now is 0x{2:X}, size 0x{3:X}, ring length 0x{4:x}", name, current.Value, start.Value + offset, size, length.Value);
                current.Value = start.Value + offset;
            }

            private void TriggerRxWatchdog()
            {
                rxWatchdog.Value = rxWatchdogCounter.Value;
                rxWatchdog.Enabled = rxWatchdogCounter.Value != 0 || rxWatchdogCounterUnit.Value != 0;
            }

            private void StartRx()
            {
                if(!parent.rxEnable.Value)
                {
                    rxState = DMAState.Stopped;
                    this.Log(LogLevel.Noisy, "Receive: Rx DMA is not enabled.");
                    return;
                }
                if(!startRx.Value)
                {
                    rxState = DMAState.Stopped;
                    this.Log(LogLevel.Noisy, "Receive: Rx DMA is not started.");
                    return;
                }
                if(rxState == DMAState.Stopped)
                {
                    rxState = DMAState.Running;
                    rxDescriptorRingCurrent.Value = rxDescriptorRingStart.Value;
                    this.Log(LogLevel.Debug, "Receive: Starting DMA at 0x{0:X}.", rxDescriptorRingCurrent.Value);
                }
                else
                {
                    this.Log(LogLevel.Debug, "Receive: Resuming DMA at 0x{0:X}.", rxDescriptorRingCurrent.Value);
                }

                if(incomingFrames.Count == 0)
                {
                    this.Log(LogLevel.Noisy, "Receive: No frames to process.");
                    rxState |= DMAState.Suspended;
                    return;
                }
                var frame = incomingFrames.Peek();
                var bytes = frame.Bytes;
                var isFirst = true;
                while(!rxFinishedRing && parent.rxEnable.Value && startRx.Value)
                {
                    var descriptor = GetRxDescriptor();

                    if(!descriptor.IsOwnedByDMA.Value)
                    {
                        this.Log(LogLevel.Debug, "Receive: Loaded descriptor is not owned by DMA.");
                        rxBufferUnavailable.Value = true;
                        rxState |= DMAState.Suspended;
                        break;
                    }
                    rxState &= ~DMAState.Suspended;
                    var structure = descriptor.GetNormalReadDescriptor();
    #if DEBUG
                    this.Log(LogLevel.Noisy, "Receive: Loaded {0} from 0x{1:X}.", structure, descriptor.Address);
    #endif

                    var bufferAddress = 0UL;
                    var bufferSize = 0UL;
                    var invalidDescriptor = structure.buffer1Address == UInt32.MaxValue || structure.buffer2Address == UInt32.MaxValue;
                    if(!invalidDescriptor && structure.buffer1Address != 0 && structure.buffer1AddressValid)
                    {
                        bufferAddress = structure.buffer1Address;
                        bufferSize = RxBuffer1Size;
                    }
                    else if(!invalidDescriptor && structure.buffer2Address != 0 && structure.buffer2AddressValid)
                    {
                        bufferAddress = structure.buffer2Address;
                        bufferSize = RxBuffer2Size;
                    }
                    else
                    {
                        contextDescriptorError.Value |= invalidDescriptor;
                        this.Log(LogLevel.Debug, "Receive: Loaded descriptor doesn't provide a valid buffer.");
                        structure.owner = DescriptorOwner.Application;
    #if DEBUG
                        this.Log(LogLevel.Noisy, "Receive: Writing {0} to 0x{1:X}.", structure, descriptor.Address);
    #endif
                        descriptor.SetDescriptor(structure);
                        descriptor.Write();
                        IncreaseRxDescriptorPointer();
                        continue;
                    }
                    rxCurrentBuffer.Value = bufferAddress;

                    if(isFirst)
                    {
                        earlyRxInterrupt.Value = true;
                        parent.UpdateInterrupts();
                    }

                    if(rxOffset >= (ulong)bytes.Length)
                    {
                        if(parent.enableTimestamp.Value && (parent.enableTimestampForAll.Value /* || is PTP */))
                        {
                            this.Log(LogLevel.Error, "Receive: Timestamping is not supported.");
                            var contextStructure = descriptor.GetAsContextDescriptor();
                            contextStructure.contextType = true;
                            contextStructure.owner = DescriptorOwner.Application;
    #if DEBUG
                            this.Log(LogLevel.Noisy, "Receive: Writing {0} to 0x{1:X}.", contextStructure, descriptor.Address);
    #endif
                            descriptor.SetDescriptor(contextStructure);
                            descriptor.Write();
                            IncreaseRxDescriptorPointer();
                        }
                        rxOffset = 0;
                        incomingFrames.Dequeue();
                        rxQueueLength -= bytes.Length;

                        if(incomingFrames.Count == 0)
                        {
                            this.Log(LogLevel.Noisy, "Receive: Finished handling frame, no more frames to process.");
                            break;
                        }
                        this.Log(LogLevel.Noisy, "Receive: Finished handling frame, processing next frame.");
                        frame = incomingFrames.Peek();
                        isFirst = true;
                        bytes = frame.Bytes;
                        continue;
                    }

                    var bytesWritten = Math.Min((ulong)bytes.Length - rxOffset, bufferSize);
                    parent.Bus.WriteBytes(bytes, bufferAddress, (int)rxOffset, (long)bytesWritten, true, parent.CpuContext);
                    this.Log(LogLevel.Noisy, "Receive: Writing frame[0x{0:X}, 0x{1:X}) at 0x{2:X}.", rxOffset, rxOffset + bytesWritten, bufferAddress);
                    rxOffset += bytesWritten;

                    var writeBackStructure = descriptor.GetAsNormalWriteBackDescriptor();
                    writeBackStructure.owner = DescriptorOwner.Application;
                    writeBackStructure.firstDescriptor = isFirst;
                    writeBackStructure.lastDescriptor = rxOffset == (ulong)bytes.Length;
                    writeBackStructure.contextType = false;;
                    writeBackStructure.receiveStatusSegment0Valid = true;
                    writeBackStructure.receiveStatusSegment1Valid = true;
                    writeBackStructure.receiveStatusSegment2Valid = true;
                    isFirst = false;

                    writeBackStructure.packetLength = (uint)bytes.Length;
                    writeBackStructure.outerVlanTag = 0x0;
                    writeBackStructure.innerVlanTag = 0x0;
                    writeBackStructure.oamSubtypeCodeOrMACControlPacketOpcode = (uint)frame.UnderlyingPacket.Type;
                    writeBackStructure.ipHeaderError = false;
                    writeBackStructure.ipv4HeaderPresent = frame.UnderlyingPacket.Type == EthernetPacketType.IpV4;
                    writeBackStructure.ipv6HeaderPresent = frame.UnderlyingPacket.Type == EthernetPacketType.IpV6;
                    if(writeBackStructure.ipv4HeaderPresent || writeBackStructure.ipv6HeaderPresent)
                    {
                        switch(((IpPacket)frame.UnderlyingPacket.PayloadPacket).NextHeader)
                        {
                            case IPProtocolType.UDP:
                                writeBackStructure.payloadType = PayloadType.UDP;
                                break;
                            case IPProtocolType.TCP:
                                writeBackStructure.payloadType = PayloadType.TCP;
                                break;
                            case IPProtocolType.ICMP:
                            case IPProtocolType.ICMPV6:
                                writeBackStructure.payloadType = PayloadType.ICMP;
                                break;
                            case IPProtocolType.IGMP:
                                if(!writeBackStructure.ipv4HeaderPresent)
                                {
                                    goto default;
                                }
                                writeBackStructure.payloadType = PayloadType.IGMPIPV4;
                                break;
                            default:
                                writeBackStructure.payloadType = PayloadType.Unknown;
                                break;
                        }
                    }

                    // NOTE: VLAN tagging is not supported by PacketDotNet, the `Type` may contain a VLAN tag
                    switch(frame.UnderlyingPacket.Type)
                    {
                        case EthernetPacketType.Arp:
                            writeBackStructure.lengthTypeField = PacketKind.ARPRequest;
                            break;
                        case EthernetPacketType.MacControl:
                            writeBackStructure.lengthTypeField = PacketKind.MACControlPacket;
                            break;
                        case EthernetPacketType.VLanTaggedFrame:
                            writeBackStructure.lengthTypeField = PacketKind.TypePacketWithVLANTag;
                            break;
                        case EthernetPacketType.ProviderBridging:
                            writeBackStructure.lengthTypeField = PacketKind.TypePacketWithDoubleVLANTag;
                            break;
                        case EthernetPacketType.ConnectivityFaultManagementOrOperationsAdministrationManagement:
                            writeBackStructure.lengthTypeField = PacketKind.OAMPacket;
                            break;
                        default:
                            writeBackStructure.lengthTypeField = (uint)frame.UnderlyingPacket.Type < EtherTypeMinimalValue ? PacketKind.LengthPacket : PacketKind.TypePacket;
                            break;
                    }

                    writeBackStructure.timestampAvailable = parent.enableTimestamp.Value;
                    writeBackStructure.timestampDropped = false;
                    writeBackStructure.dribbleBitError = false;
                    writeBackStructure.receiveError = false;
                    writeBackStructure.overflowError = false;
                    writeBackStructure.receiveWatchdogTimeout = false;
                    writeBackStructure.giantPacket = false;
                    writeBackStructure.crcError = parent.crcCheckDisable.Value ? false : !EthernetFrame.CheckCRC(bytes);
                    writeBackStructure.errorSummary = new bool[]
                    {
                        writeBackStructure.dribbleBitError,
                        writeBackStructure.receiveError,
                        writeBackStructure.overflowError,
                        writeBackStructure.receiveWatchdogTimeout,
                        writeBackStructure.giantPacket,
                        writeBackStructure.crcError,
                    }.Any(x => x);
    #if DEBUG
                    this.Log(LogLevel.Noisy, "Receive: Writing {0} to 0x{1:X}.", writeBackStructure, descriptor.Address);
    #endif
                    descriptor.SetDescriptor(writeBackStructure);
                    descriptor.Write();
                    IncreaseRxDescriptorPointer();

                    if(!writeBackStructure.lastDescriptor)
                    {
                        continue;
                    }

                    if(structure.interruptOnCompletion)
                    {
                        rxInterrupt.Value = true;
                        rxWatchdog.Enabled = false;
                    }
                    else
                    {
                        TriggerRxWatchdog();
                    }
                    earlyRxInterrupt.Value = false;
                    parent.UpdateRxCounters(frame, writeBackStructure);
                    this.Log(LogLevel.Noisy, "Receive: Frame fully processed.");
                }
                if(!parent.rxEnable.Value || !startRx.Value)
                {
                    rxProcessStopped.Value = true;
                    rxState = DMAState.Stopped;
                    this.Log(LogLevel.Debug, "Receive: Stopping Rx DMA at 0x{0:X}.", rxDescriptorRingCurrent.Value);
                }
                else
                {
                    if(rxFinishedRing)
                    {
                        this.Log(LogLevel.Noisy, "Receive: Descriptor ring is empty.");
                    }
                    rxBufferUnavailable.Value |= rxFinishedRing || incomingFrames.Count != 0;
                    rxState |= DMAState.Suspended;
                    this.Log(LogLevel.Debug, "Receive: Suspending Rx DMA at 0x{0:X}.", rxDescriptorRingCurrent.Value);
                }
                parent.UpdateInterrupts();
            }

            private void StartTx()
            {
                if(!parent.txEnable.Value)
                {
                    txState = DMAState.Stopped;
                    this.Log(LogLevel.Noisy, "Transmission: Tx DMA is not enabled.");
                    return;
                }
                if(!startTx.Value)
                {
                    txState = DMAState.Stopped;
                    this.Log(LogLevel.Noisy, "Transmission: Tx DMA is not started.");
                    return;
                }
                if(txState == DMAState.Stopped)
                {
                    txState |= DMAState.Running;
                    txDescriptorRingCurrent.Value = txDescriptorRingStart.Value;
                    this.Log(LogLevel.Debug, "Transmission: Starting Tx DMA at 0x{0:X}.", txDescriptorRingCurrent.Value);
                }
                else
                {
                    this.Log(LogLevel.Debug, "Transmission: Resuming Tx DMA at 0x{0:X}.", txDescriptorRingCurrent.Value);
                }

                while(!txFinishedRing && parent.txEnable.Value && startTx.Value)
                {
                    var descriptor = GetTxDescriptor();

                    if(!descriptor.IsOwnedByDMA.Value)
                    {
                        this.Log(LogLevel.Debug, "Transmission: Loaded descriptor is not owned by DMA.");
                        txProcessStopped.Value = true;
                        txBufferUnavailable.Value = true;
                        txState |= DMAState.Suspended;
                        this.Log(LogLevel.Debug, "Transmission: Suspending Tx DMA at 0x{0:X}.", txDescriptorRingCurrent.Value);
                        break;
                    }
                    txState &= ~DMAState.Suspended;
                    if(descriptor.Type.Is<TxDescriptor.NormalReadDescriptor>())
                    {
                        var structure = descriptor.GetNormalReadDescriptor();
    #if DEBUG
                        this.Log(LogLevel.Noisy, "Transmission: Loaded {0} from 0x{1:X}.", structure, descriptor.Address);
    #endif
                        if(frameAssembler == null && !structure.firstDescriptor)
                        {
                            this.Log(LogLevel.Warning, "Transmission: Building frame without first descriptor.");
                            break;
                        }
                        else if(frameAssembler != null && structure.firstDescriptor)
                        {
                            this.Log(LogLevel.Warning, "Transmission: Building new frame without clearing last frame.");
                        }

                        var buffer = structure.FetchBuffer1OrHeader(parent.Bus, parent.CpuContext);
                        txCurrentBuffer.Value = structure.buffer1OrHeaderAddress;
                        var tsoEnabled = structure.tcpSegmentationEnable && tcpSegmentationEnable.Value;

                        MACAddress? sourceAddress = null;
                        switch(structure.sourceAddressControl)
                        {
                            case DescriptorSourceAddressOperation.MACAddressRegister0Insert:
                            case DescriptorSourceAddressOperation.MACAddressRegister0Replace:
                                sourceAddress = parent.MAC0;
                                break;
                            case DescriptorSourceAddressOperation.MACAddressRegister1Insert:
                            case DescriptorSourceAddressOperation.MACAddressRegister1Replace:
                                sourceAddress = parent.MAC1;
                                break;
                            default:
                                sourceAddress = null;
                                break;
                        }

                        if(!sourceAddress.HasValue)
                        {
                            switch(parent.sourceAddressOperation.Value)
                            {
                                case RegisterSourceAddressOperation.MACAddressRegister0Insert:
                                case RegisterSourceAddressOperation.MACAddressRegister0Replace:
                                    sourceAddress = parent.MAC0;
                                    break;
                                case RegisterSourceAddressOperation.MACAddressRegister1Insert:
                                case RegisterSourceAddressOperation.MACAddressRegister1Replace:
                                    sourceAddress = parent.MAC1;
                                    break;
                                default:
                                    this.Log(LogLevel.Error, "Using a reserved value in ETH_MACCR.SARC register.");
                                    break;
                            }
                        }

                        if(structure.firstDescriptor)
                        {
                            if(tsoEnabled)
                            {
                                frameAssembler = new FrameAssembler(
                                    parent,
                                    buffer,
                                    (uint)maximumSegmentSize.Value,
                                    latestTxContext,
                                    parent.checksumOffloadEnable.Value,
                                    parent.SendFrame,
                                    sourceAddress
                                );
                                buffer = structure.FetchBuffer2OrBuffer1(parent.Bus, parent.CpuContext);
                                txCurrentBuffer.Value = structure.buffer2orBuffer1Address;
                            }
                            else
                            {
                                frameAssembler = new FrameAssembler(
                                    parent,
                                    structure.crcPadControl,
                                    parent.checksumOffloadEnable.Value ? structure.checksumControl : ChecksumOperation.None,
                                    parent.SendFrame
                                );
                            }
                        }
                        if(structure.buffer2Length != 0 && !tsoEnabled)
                        {
                            // Though it's not clearly stated in the documentation, the STM driver
                            // expects buffer2 to be valid even with TSO disabled. In this case,
                            // concatenate two buffers when assembling frame to be sent.
                            frameAssembler.PushPayload(buffer);
                            buffer = structure.FetchBuffer2OrBuffer1(parent.Bus, parent.CpuContext);
                            txCurrentBuffer.Value = structure.buffer2orBuffer1Address;
                        }
                        frameAssembler.PushPayload(buffer);
                        earlyTxInterrupt.Value = true;

                        if(!structure.lastDescriptor)
                        {
                            txState |= DMAState.ProcessingIntermediate;
                            var writeBackIntermediateStructure = new TxDescriptor.NormalWriteBackDescriptor();
                            writeBackIntermediateStructure.owner = DescriptorOwner.Application;
    #if DEBUG
                            this.Log(LogLevel.Noisy, "Transmission: Writing intermediate {0} to 0x{1:X}.", writeBackIntermediateStructure, descriptor.Address);
    #endif
                            descriptor.SetDescriptor(writeBackIntermediateStructure);
                            descriptor.Write();
                            IncreaseTxDescriptorPointer();
                            continue;
                        }

                        frameAssembler.FinalizeAssembly();
                        frameAssembler = null;

                        if((txState & DMAState.ProcessingSecond) == 0 && operateOnSecondPacket.Value)
                        {
                            txState |= DMAState.ProcessingSecond;
                            continue;
                        }

                        var writeBackStructure = new TxDescriptor.NormalWriteBackDescriptor();
                        writeBackStructure.ipHeaderError = false;
                        writeBackStructure.deferredBit = false;
                        writeBackStructure.underflowError = structure.buffer1OrHeaderAddress == 0x0 || structure.headerOrBuffer1Length == 0x0;
                        writeBackStructure.excessiveDeferral = false;
                        writeBackStructure.collisionCount = false;
                        writeBackStructure.excessiveCollision = false;
                        writeBackStructure.lateCollision = false;
                        writeBackStructure.noCarrier = false;
                        writeBackStructure.lossOfCarrier = false;
                        writeBackStructure.payloadChecksumError = false;
                        writeBackStructure.packetFlushed = false;
                        writeBackStructure.jabberTimeout = false;
                        writeBackStructure.errorSummary = new bool[]
                        {
                            writeBackStructure.ipHeaderError,
                            writeBackStructure.jabberTimeout,
                            writeBackStructure.packetFlushed,
                            writeBackStructure.payloadChecksumError,
                            writeBackStructure.lossOfCarrier,
                            writeBackStructure.noCarrier,
                            writeBackStructure.lateCollision,
                            writeBackStructure.excessiveCollision,
                            writeBackStructure.excessiveDeferral,
                            writeBackStructure.underflowError,
                        }.Any(x => x);
                        writeBackStructure.txTimestampCaptured = false;
                        writeBackStructure.owner = DescriptorOwner.Application;
                        writeBackStructure.firstDescriptor = structure.firstDescriptor;
                        writeBackStructure.lastDescriptor = structure.lastDescriptor;

                        if(structure.transmitTimestampEnable && parent.enableTimestamp.Value)
                        {
                            this.Log(LogLevel.Error, "Transmission: Timestamping is not supported.");
                        }
    #if DEBUG
                        this.Log(LogLevel.Noisy, "Transmission: Writing {0} to 0x{1:X}.", writeBackStructure, descriptor.Address);
    #endif
                        descriptor.SetDescriptor(writeBackStructure);

                        if(structure.interruptOnCompletion)
                        {
                            txInterrupt.Value = true;
                        }
                    }
                    else if(descriptor.Type.Is<TxDescriptor.ContextDescriptor>())
                    {
                        var structure = descriptor.GetContextDescriptor();
                        latestTxContext = structure;
    #if DEBUG
                        this.Log(LogLevel.Noisy, "Transmission: Loaded {0} from 0x{1:X}.", structure, descriptor.Address);
    #endif
                        if(structure.oneStepTimestampCorrectionEnable && structure.oneStepTimestampCorrectionInputOrMaximumSegmentSizeValid)
                        {
                            this.Log(LogLevel.Error, "Transmission: Timestamping is not supported. One Step Timestamp Correction failed.");
                        }
                        structure.owner = DescriptorOwner.Application;
    #if DEBUG
                        this.Log(LogLevel.Noisy, "Transmission: Writing {0} to 0x{1:X}.", structure, descriptor.Address);
    #endif
                        descriptor.SetDescriptor(structure);
                    }
                    else
                    {
                        throw new RecoverableException("Unreachable");
                    }
                    descriptor.Write();
                    IncreaseTxDescriptorPointer();
                }

                if(txFinishedRing)
                {
                    txBufferUnavailable.Value = true;
                    txState |= DMAState.Suspended;
                    this.Log(LogLevel.Debug, "Transmission: Descriptor ring is empty.");
                }
                if(!parent.txEnable.Value || !startTx.Value)
                {
                    txState = DMAState.Stopped;
                    txProcessStopped.Value = true;
                    this.Log(LogLevel.Debug, "Transmission: Stopping Tx DMA at 0x{0:X}.", txDescriptorRingCurrent.Value);
                }
                parent.UpdateInterrupts();
            }

            private void Log(LogLevel level, string format, params object[] args)
            {
                parent.Log(level, $"DMA Channel {channelNumber}: {format}", args);
            }

            private bool GetNormalInterruptSummary(bool txMask = true, bool rxMask = true)
            {
                return (txMask && txInterrupt.Value && txInterruptEnable.Value) ||
                       (txBufferUnavailable.Value && txBufferUnavailableEnable.Value) ||
                       (rxMask && rxInterrupt.Value && rxInterruptEnable.Value) ||
                       (earlyRxInterrupt.Value && earlyRxInterruptEnable.Value);
            }

            private ulong RxBuffer1Size => alternateRxBufferSize.Value == 0 ? rxBufferSize.Value : alternateRxBufferSize.Value;
            private ulong RxBuffer2Size => rxBufferSize.Value;

            // TODO: Maybe remove those:
            private ulong ProgrammableBurstLengthMultiplier => programmableBurstLengthTimes8.Value ? 8UL : 1UL;
            private ulong TxProgrammableBurstLength => txProgrammableBurstLength.Value * ProgrammableBurstLengthMultiplier;
            private ulong RxProgrammableBurstLength => rxProgrammableBurstLength.Value * ProgrammableBurstLengthMultiplier;

            private IValueRegisterField maximumSegmentSize;
            private IFlagRegisterField programmableBurstLengthTimes8;
            private IValueRegisterField descriptorSkipLength;
            private IFlagRegisterField startTx;
            private IFlagRegisterField operateOnSecondPacket;
            private IFlagRegisterField tcpSegmentationEnable;
            private IValueRegisterField txProgrammableBurstLength;
            private IFlagRegisterField startRx;
            private IValueRegisterField rxBufferSize;
            private IValueRegisterField rxProgrammableBurstLength;
            private IValueRegisterField txDescriptorRingStart;
            private IValueRegisterField rxDescriptorRingStart;
            private IValueRegisterField txDescriptorRingTail;
            private IValueRegisterField rxDescriptorRingTail;
            private IValueRegisterField txDescriptorRingLength;
            private IValueRegisterField rxDescriptorRingLength;
            private IValueRegisterField alternateRxBufferSize;
            private IValueRegisterField txDescriptorRingCurrent;
            private IValueRegisterField rxDescriptorRingCurrent;
            private IValueRegisterField txCurrentBuffer;
            private IValueRegisterField rxCurrentBuffer;

            private IFlagRegisterField txInterruptEnable;
            private IFlagRegisterField txProcessStoppedEnable;
            private IFlagRegisterField txBufferUnavailableEnable;
            private IFlagRegisterField rxInterruptEnable;
            private IFlagRegisterField rxBufferUnavailableEnable;
            private IFlagRegisterField rxProcessStoppedEnable;
            private IFlagRegisterField rxWatchdogTimeoutEnable;
            private IFlagRegisterField earlyTxInterruptEnable;
            private IFlagRegisterField earlyRxInterruptEnable;
            private IFlagRegisterField fatalBusErrorEnable;
            private IFlagRegisterField contextDescriptorErrorEnable;
            private IFlagRegisterField abnormalInterruptSummaryEnable;
            private IValueRegisterField rxWatchdogCounter;
            private IValueRegisterField rxWatchdogCounterUnit;
            private IFlagRegisterField normalInterruptSummaryEnable;
            private IFlagRegisterField txInterrupt;
            private IFlagRegisterField txProcessStopped;
            private IFlagRegisterField txBufferUnavailable;
            private IFlagRegisterField rxInterrupt;
            private IFlagRegisterField rxBufferUnavailable;
            private IFlagRegisterField rxProcessStopped;
            private IFlagRegisterField rxWatchdogTimeout;
            private IFlagRegisterField earlyTxInterrupt;
            private IFlagRegisterField earlyRxInterrupt;
            private IFlagRegisterField fatalBusError;
            private IFlagRegisterField contextDescriptorError;
            private IFlagRegisterField abnormalInterruptSummary;
            private IFlagRegisterField normalInterruptSummary;

            private FrameAssembler frameAssembler;

            private bool rxFinishedRing = true;
            private bool txFinishedRing = true;
            private DMAState txState = DMAState.Stopped;
            private DMAState rxState = DMAState.Stopped;
            private ulong rxOffset;
            private TxDescriptor.ContextDescriptor? latestTxContext;
            private int rxQueueLength;

            private readonly Queue<EthernetFrame> incomingFrames;
            private readonly LimitTimer rxWatchdog;

            private readonly SynopsysDWCEthernetQualityOfService parent;
            private readonly int channelNumber;
            private readonly bool hasInterrupts;
        }
    }
}