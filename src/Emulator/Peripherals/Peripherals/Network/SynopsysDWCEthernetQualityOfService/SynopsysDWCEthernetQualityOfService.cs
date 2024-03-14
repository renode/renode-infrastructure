//
// Copyright (c) 2010-2024 Antmicro
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
    public partial class SynopsysDWCEthernetQualityOfService : NetworkWithPHY, IMACInterface, IKnownSize
    {
        public SynopsysDWCEthernetQualityOfService(IMachine machine, long systemClockFrequency, ICPU cpuContext = null) : base(machine)
        {
            IRQ = new GPIO();
            MAC = EmulationManager.Instance.CurrentEmulation.MACRepository.GenerateUniqueMAC();
            Bus = machine.GetSystemBus(this);
            this.CpuContext = cpuContext;

            incomingFrames = new Queue<EthernetFrame>();
            rxIpcPacketCounterInterruptEnable = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcByteCounterInterruptEnable = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcPacketCounterInterrupt = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcByteCounterInterrupt = new IFlagRegisterField[NumberOfIpcCounters];
            rxIpcPacketCounter = new IValueRegisterField[NumberOfIpcCounters];
            rxIpcByteCounter = new IValueRegisterField[NumberOfIpcCounters];
            macAndMmcRegisters = new DoubleWordRegisterCollection(this, CreateRegisterMap());
            mtlRegisters = new DoubleWordRegisterCollection(this, CreateMTLRegisterMap());
            dmaRegisters = new DoubleWordRegisterCollection(this, CreateDMARegisterMap());
            rxWatchdog = new LimitTimer(machine.ClockSource, systemClockFrequency, this, "rx-watchdog", enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true, divider: RxWatchdogDivider);
            rxWatchdog.LimitReached += delegate
            {
                this.Log(LogLevel.Noisy, "Receive: Watchdog reached limit.");
                rxInterrupt.Value = true;
                UpdateInterrupts();
            };
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if(!rxEnable.Value)
            {
                this.Log(LogLevel.Debug, "Receive: Dropping frame {0}", frame);
                return;
            }
            if(rxQueueLength + frame.Length > RxQueueSize)
            {
                IncrementPacketCounter(rxFifoPacketCounter, rxFifoPacketCounterInterrupt);
                this.Log(LogLevel.Debug, "Receive: Dropping overflow frame {0}", frame);
                UpdateInterrupts();
                return;
            }
            this.Log(LogLevel.Debug, "Receive: Incoming frame {0}", frame);
            incomingFrames.Enqueue(frame);
            rxQueueLength += frame.Bytes.Length;
            StartRxDMA();
        }

        public override void Reset()
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
            ResetRegisters();
            UpdateInterrupts();
        }

        public long Size => 0xC00;
        [DefaultInterrupt]
        public GPIO IRQ { get; }
        public MACAddress MAC { get; set; }

        public event Action<EthernetFrame> FrameReady;

        private void StartRxDMA()
        {
            if(!rxEnable.Value)
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
            while(!rxFinishedRing && rxEnable.Value && startRx.Value)
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
                    UpdateInterrupts();
                }

                if(rxOffset >= (ulong)bytes.Length)
                {
                    if(enableTimestamp.Value && (enableTimestampForAll.Value /* || is PTP */))
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
                Bus.WriteBytes(bytes, bufferAddress, (int)rxOffset, (long)bytesWritten, true, CpuContext);
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

                writeBackStructure.timestampAvailable = enableTimestamp.Value;
                writeBackStructure.timestampDropped = false;
                writeBackStructure.dribbleBitError = false;
                writeBackStructure.receiveError = false;
                writeBackStructure.overflowError = false;
                writeBackStructure.receiveWatchdogTimeout = false;
                writeBackStructure.giantPacket = false;
                writeBackStructure.crcError = crcCheckDisable.Value ? false : !EthernetFrame.CheckCRC(bytes);
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
                UpdateRxCounters(frame, writeBackStructure);
                this.Log(LogLevel.Noisy, "Receive: Frame fully processed.");
            }
            if(!rxEnable.Value || !startRx.Value)
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
            UpdateInterrupts();
        }

        private void SendFrame(EthernetFrame frame)
        {
            FrameReady?.Invoke(frame);
            IncrementPacketCounter(txGoodPacketCounter, txGoodPacketCounterInterrupt);
            IncrementPacketCounter(txPacketCounter, txPacketCounterInterrupt);
            // one added to account for Start of Frame Delimiter (SFD)
            var byteCount = 1 + (uint)frame.Bytes.Length;
            IncrementByteCounter(txByteCounter, txByteCounterInterrupt, byteCount);
            IncrementByteCounter(txGoodByteCounter, txGoodByteCounterInterrupt, byteCount);
            if(frame.DestinationMAC.IsBroadcast)
            {
                IncrementPacketCounter(txBroadcastPacketCounter, txBroadcastPacketCounterInterrupt);
            }
            else if(frame.DestinationMAC.IsMulticast)
            {
                IncrementPacketCounter(txMulticastPacketCounter, txMulticastPacketCounterInterrupt);
            }
            else
            {
                IncrementPacketCounter(txUnicastPacketCounter, txUnicastPacketCounterInterrupt);
            }
#if DEBUG
            this.Log(LogLevel.Noisy, "Transmission: frame {0}", Misc.PrettyPrintCollectionHex(frame.Bytes));
            this.Log(LogLevel.Debug, "Transmission: frame {0}", frame);
#endif
        }

        private void StartTxDMA()
        {
            if(!txEnable.Value)
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

            while(!txFinishedRing && txEnable.Value && startTx.Value)
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

                    var buffer = structure.FetchBuffer1OrHeader(Bus, CpuContext);
                    txCurrentBuffer.Value = structure.buffer1OrHeaderAddress;
                    var tsoEnabled = structure.tcpSegmentationEnable && tcpSegmentationEnable.Value;
                    if(structure.firstDescriptor)
                    {
                        if(tsoEnabled)
                        {
                            frameAssembler = new FrameAssembler(
                                this,
                                buffer,
                                (uint)maximumSegmentSize.Value,
                                latestTxContext,
                                checksumOffloadEnable.Value,
                                SendFrame
                            );
                            buffer = structure.FetchBuffer2OrBuffer1(Bus, CpuContext);
                            txCurrentBuffer.Value = structure.buffer2orBuffer1Address;
                        }
                        else
                        {
                            frameAssembler = new FrameAssembler(
                                this,
                                structure.crcPadControl,
                                structure.checksumControl,
                                checksumOffloadEnable.Value,
                                SendFrame
                            );
                        }
                    }
                    if(structure.buffer2Length != 0 && !tsoEnabled)
                    {
                        // Though it's not clearly stated in the documentation, the STM driver
                        // expects buffer2 to be valid even with TSO disabled. In this case,
                        // concatenate two buffers when assembling frame to be sent.
                        frameAssembler.PushPayload(buffer);
                        buffer = structure.FetchBuffer2OrBuffer1(Bus, CpuContext);
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
                    if(structure.transmitTimestampEnable && enableTimestamp.Value)
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
            if(!txEnable.Value || !startTx.Value)
            {
                txState = DMAState.Stopped;
                txProcessStopped.Value = true;
                this.Log(LogLevel.Debug, "Transmission: Stopping Tx DMA at 0x{0:X}.", txDescriptorRingCurrent.Value);
            }
            UpdateInterrupts();
        }

        private void UpdateRxCounters(EthernetFrame frame, RxDescriptor.NormalWriteBackDescriptor writeBackStructure)
        {
            if(writeBackStructure.crcError)
            {
                IncrementPacketCounter(rxCrcErrorPacketCounter, rxCrcErrorPacketCounterInterrupt);
            }
            IncrementPacketCounter(rxPacketCounter, rxPacketCounterInterrupt);
            // byte count excludes preamble, one added to account for Start of Frame Delimiter (SFD)
            var byteCount = 1 + writeBackStructure.packetLength;
            IncrementByteCounter(rxByteCounter, rxByteCounterInterrupt, byteCount);

            var isRuntPacket = writeBackStructure.packetLength <= EthernetFrame.RuntPacketMaximumSize;
            var lengthOutOfRange = frame.Length > EthernetFrame.MaximumFrameSize;
            var isNontypePacket = (writeBackStructure.lengthTypeField != PacketKind.TypePacket);
            var lengthMismatch = (frame.Length != writeBackStructure.packetLength);

            var lengthError = isNontypePacket && lengthMismatch;
            var outOfRange = isNontypePacket && lengthOutOfRange;
            if(writeBackStructure.crcError || isRuntPacket || lengthError || outOfRange)
            {
                return;
            }

            IncrementByteCounter(rxGoodByteCounter, rxGoodByteCounterInterrupt, byteCount);
            if(frame.DestinationMAC.IsBroadcast)
            {
                IncrementPacketCounter(rxBroadcastPacketCounter, rxBroadcastPacketCounterInterrupt);
            }
            else if(frame.DestinationMAC.IsMulticast)
            {
                IncrementPacketCounter(rxMulticastPacketCounter, rxMulticastPacketCounterInterrupt);
            }
            else
            {
                IncrementPacketCounter(rxUnicastPacketCounter, rxUnicastPacketCounterInterrupt);
            }

            if(writeBackStructure.ipv4HeaderPresent)
            {
                IncreaseIpcCounter(IpcCounter.IpV4Good, byteCount);
                if(writeBackStructure.ipHeaderError)
                {
                    IncreaseIpcCounter(IpcCounter.IpV4HeaderError, byteCount);
                }
            }
            if(writeBackStructure.ipv6HeaderPresent)
            {
                IncreaseIpcCounter(IpcCounter.IpV6Good, byteCount);
                if(writeBackStructure.ipHeaderError)
                {
                    IncreaseIpcCounter(IpcCounter.IpV6HeaderError, byteCount);
                }
            }
            if(writeBackStructure.payloadType ==  PayloadType.UDP)
            {
                IncreaseIpcCounter(IpcCounter.UdpGood, byteCount);
            }
            else if(writeBackStructure.payloadType == PayloadType.TCP)
            {
                IncreaseIpcCounter(IpcCounter.TcpGood, byteCount);
            }
            else if(writeBackStructure.payloadType == PayloadType.ICMP)
            {
                IncreaseIpcCounter(IpcCounter.IcmpGood, byteCount);
            }
        }

        private void IncrementPacketCounter(IValueRegisterField counter, IFlagRegisterField status)
        {
            counter.Value += 1;
            status.Value |= EqualsHalfOrMaximumCounterValue(counter);
        }

        private void IncrementByteCounter(IValueRegisterField counter, IFlagRegisterField status, uint increment)
        {
            status.Value |= WouldExceedHalfOrMaximumCounterValue(counter, increment);
            counter.Value += increment;
        }

        private void IncreaseIpcCounter(IpcCounter type, uint byteCount)
        {
            var index = (int)type;
            IncrementPacketCounter(rxIpcPacketCounter[index], rxIpcPacketCounterInterrupt[index]);
            IncrementByteCounter(rxIpcByteCounter[index], rxIpcByteCounterInterrupt[index], byteCount);
        }

        private bool CheckAnySetIpcCounterInterrupts()
        {
            for(var i = 0; i < NumberOfIpcCounters; i++)
            {
                if((rxIpcPacketCounterInterrupt[i].Value && rxIpcPacketCounterInterruptEnable[i].Value) || (rxIpcByteCounterInterrupt[i].Value && rxIpcByteCounterInterruptEnable[i].Value))
                {
                    return true;
                }
            }
            return false;
        }

        private void TriggerRxWatchdog()
        {
            rxWatchdog.Value = rxWatchdogCounter.Value;
            rxWatchdog.Enabled = rxWatchdogCounter.Value != 0 || rxWatchdogCounterUnit.Value != 0;
        }

        private bool EqualsHalfOrMaximumCounterValue(IValueRegisterField counter)
        {
            return (counter.Value == CounterMaxValue) || (counter.Value == (CounterMaxValue / 2));
        }

        private bool WouldExceedHalfOrMaximumCounterValue(IValueRegisterField counter, uint increment)
        {
            return (counter.Value < CounterMaxValue && counter.Value + increment >= CounterMaxValue)
                || (counter.Value < CounterMaxValue / 2 && counter.Value + increment >= CounterMaxValue / 2);
        }

        private void UpdateInterrupts()
        {
            normalInterruptSummary.Value |= NormalInterruptSummary;
            abnormalInterruptSummary.Value |= AbnormalInterruptSummary;

            var irq = (ptpMessageTypeInterrupt.Value && ptpMessageTypeInterruptEnable.Value)   ||
                      (lowPowerIdleInterrupt.Value && lowPowerIdleInterruptEnable.Value)       ||
                      (timestampInterrupt.Value && timestampInterruptEnable.Value)             ||
                      (rxWatchdogTimeout.Value && rxWatchdogTimeoutEnable.Value)               ||
                      (abnormalInterruptSummary.Value && abnormalInterruptSummaryEnable.Value) ||
                      (normalInterruptSummary.Value && normalInterruptSummaryEnable.Value)     ||
                      MMCTxInterruptStatus                                                     ||
                      MMCRxInterruptStatus                                                     ||
                      CheckAnySetIpcCounterInterrupts();
            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", irq);
            IRQ.Set(irq);
        }

        private IBusController Bus { get; }
        private ICPU CpuContext { get; }

        private bool DMAInterrupts =>
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

        private bool NormalInterruptSummary =>
            (txInterrupt.Value && txInterruptEnable.Value) ||
            (txBufferUnavailable.Value && txBufferUnavailableEnable.Value) ||
            (rxInterrupt.Value && rxInterruptEnable.Value) ||
            (earlyRxInterrupt.Value && earlyRxInterruptEnable.Value);

        private bool AbnormalInterruptSummary =>
            (txProcessStopped.Value && txProcessStoppedEnable.Value) ||
            (rxBufferUnavailable.Value && rxBufferUnavailableEnable.Value) ||
            (rxProcessStopped.Value && rxProcessStoppedEnable.Value) ||
            (earlyTxInterrupt.Value && earlyTxInterruptEnable.Value) ||
            (fatalBusError.Value && fatalBusErrorEnable.Value) ||
            (contextDescriptorError.Value && contextDescriptorErrorEnable.Value);

        private bool MMCTxInterruptStatus =>
            (txGoodPacketCounterInterrupt.Value && txGoodPacketCounterInterruptEnable.Value)           ||
            (txGoodByteCounterInterrupt.Value && txGoodByteCounterInterruptEnable.Value)               ||
            (txBroadcastPacketCounterInterrupt.Value && txBroadcastPacketCounterInterruptEnable.Value) ||
            (txMulticastPacketCounterInterrupt.Value && txMulticastPacketCounterInterruptEnable.Value) ||
            (txUnicastPacketCounterInterrupt.Value && txUnicastPacketCounterInterruptEnable.Value)     ||
            (txPacketCounterInterrupt.Value && txPacketCounterInterruptEnable.Value)                   ||
            (txByteCounterInterrupt.Value && txByteCounterInterruptEnable.Value);

        private bool MMCRxInterruptStatus =>
            (rxFifoPacketCounterInterrupt.Value && rxFifoPacketCounterInterruptEnable.Value)           ||
            (rxUnicastPacketCounterInterrupt.Value && rxUnicastPacketCounterInterruptEnable.Value)     ||
            (rxCrcErrorPacketCounterInterrupt.Value && rxCrcErrorPacketCounterInterruptEnable.Value)   ||
            (rxMulticastPacketCounterInterrupt.Value && rxMulticastPacketCounterInterruptEnable.Value) ||
            (rxBroadcastPacketCounterInterrupt.Value && rxBroadcastPacketCounterInterruptEnable.Value) ||
            (rxGoodByteCounterInterrupt.Value && rxGoodByteCounterInterruptEnable.Value)               ||
            (rxByteCounterInterrupt.Value && rxByteCounterInterruptEnable.Value)                       ||
            (rxPacketCounterInterrupt.Value && rxPacketCounterInterruptEnable.Value);

        private ulong RxBuffer1Size => alternateRxBufferSize.Value == 0 ? rxBufferSize.Value : alternateRxBufferSize.Value;
        private ulong RxBuffer2Size => rxBufferSize.Value;

        private ulong ProgrammableBurstLengthMultiplier => programableBurstLengthTimes8.Value ? 8UL : 1UL;
        private ulong TxProgrammableBurstLength => txProgramableBurstLength.Value * ProgrammableBurstLengthMultiplier;
        private ulong RxProgrammableBurstLength => rxProgramableBurstLength.Value * ProgrammableBurstLengthMultiplier;

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

        private const ulong CounterMaxValue = UInt32.MaxValue;
        private const int RxWatchdogDivider = 256;
        private const uint EtherTypeMinimalValue = 0x600;
        private const int RxQueueSize = 8192;

        private enum DMAState
        {
            Stopped = 0,
            Running = 1,
            ProcessingIntermediate = 2,
            ProcessingSecond = 4,
            Suspended = 8,
        }
    }
}
