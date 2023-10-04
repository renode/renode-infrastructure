//
// Copyright (c) 2010-2023 Antmicro
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
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using PacketDotNet;
using IPProtocolType = Antmicro.Renode.Network.IPProtocolType;

namespace Antmicro.Renode.Peripherals.Network
{
    public class K6xF_Ethernet : NetworkWithPHY, IDoubleWordPeripheral, IMACInterface, IKnownSize
    {
        public K6xF_Ethernet(IMachine machine) : base(machine)
        {
            sysbus = machine.GetSystemBus(this);
            RxIRQ = new GPIO();
            TxIRQ = new GPIO();
            PtpIRQ = new GPIO();
            MiscIRQ = new GPIO();
            TimerIRQ = new GPIO();

            innerLock = new object();

            interruptManager = new InterruptManager<Interrupts>(this);

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptEvent, interruptManager.GetRegister<DoubleWordRegister>(
                    valueProviderCallback: (interrupt, oldValue) =>
                    {
                        return interruptManager.IsSet(interrupt);
                    },
                    writeCallback: (interrupt, oldValue, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.ClearInterrupt(interrupt);
                        }
                    })
                },
                {(long)Registers.InterruptMask, interruptManager.GetRegister<DoubleWordRegister>(
                    valueProviderCallback: (interrupt, oldValue) =>
                    {
                        return interruptManager.IsEnabled(interrupt);
                    },
                    writeCallback: (interrupt, oldValue, newValue) =>
                    {
                        if(newValue)
                        {
                            interruptManager.EnableInterrupt(interrupt);
                        }
                        else
                        {
                            interruptManager.DisableInterrupt(interrupt);
                            interruptManager.ClearInterrupt(interrupt);
                        }
                    })
                },
                {(long)Registers.ReceiveDescriptorActive, new DoubleWordRegister(this)
                    .WithReservedBits(25, 7)
                    .WithFlag(24, out receiverEnabled, name: "RDAR")
                    .WithReservedBits(0, 24)
                },
                {(long)Registers.TransmitDescriptorActive, new DoubleWordRegister(this)
                    .WithReservedBits(25, 7)
                    .WithFlag(24, FieldMode.Read | FieldMode.WriteOneToClear, name: "TDAR",
                        writeCallback: (_, value) =>
                        {
                            // transmission should be started on *any* write to this field (i.e., including 0)
                            isTransmissionStarted = true;
                            this.Log(LogLevel.Debug, "Sending Frames");
                            SendFrames();
                        })
                    .WithReservedBits(0, 24)
                },
                {(long)Registers.EthernetControl, new DoubleWordRegister(this)
                    .WithReservedBits(17, 15)
                    .WithReservedBits(12, 5)
                    .WithReservedBits(11, 1)
                    .WithReservedBits(10, 1)
                    .WithReservedBits(9, 1)
                    .WithTaggedFlag("DBSWP", 8)
                    .WithTaggedFlag("STOPEN", 7)
                    .WithTaggedFlag("DBGEN", 6)
                    .WithReservedBits(5, 1)
                    .WithFlag(4, out extendedMode, name: "EN1588")
                    .WithTaggedFlag("SLEEP", 3)
                    .WithTaggedFlag("MAGICEN", 2)
                    .WithTaggedFlag("ETHEREN", 1)
                    .WithFlag(0, FieldMode.Write,
                        writeCallback: (_, b) =>
                        {
                            if(b)
                            {
                                Reset();
                            }
                        },name: "RESET")
                },
                {(long)Registers.MIIManagementFrame, new DoubleWordRegister(this)
                    .WithValueField(0, 31, name: "phy_management", writeCallback: (_, value) => HandlePhyWrite((uint)value), valueProviderCallback: _ => HandlePhyRead())
                },
                {(long)Registers.ReceiveControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("GRS", 31)
                    .WithTaggedFlag("NLC", 30)
                    .WithValueField(16, 14, name: "MAX_FL")
                    .WithTaggedFlag("CFEN", 15)
                    .WithTaggedFlag("CRCFWD", 14)
                    .WithTaggedFlag("PAUFWD", 13)
                    .WithTaggedFlag("PADEN", 12)
                    .WithReservedBits(10, 2)
                    .WithTaggedFlag("RMII_10T", 9)
                    .WithTaggedFlag("RMII_MODE", 8)
                    .WithReservedBits(7, 1)
                    .WithReservedBits(6, 1)
                    .WithTaggedFlag("FCE", 5)
                    .WithTaggedFlag("BC_REJ", 4)
                    .WithTaggedFlag("PROM", 3)
                    .WithTaggedFlag("MII_MODE", 2)
                    .WithTaggedFlag("DRT", 1)
                    .WithTaggedFlag("LOOP", 0)
                },
                {(long)Registers.TransmitControl, new DoubleWordRegister(this)
                    .WithReservedBits(11, 21)
                    .WithReservedBits(10, 1)
                    .WithFlag(9, out forwardCRCFromApplication, name: "CRCFWD")
                    .WithTaggedFlag("ADDINS", 8)
                    .WithValueField(5, 3, name: "ADDSEL")
                    .WithTaggedFlag("RFC_PAUSE", 4)
                    .WithTaggedFlag("TFC_PAUSE", 3)
                    .WithTaggedFlag("FDEN", 2)
                    .WithReservedBits(1, 1)
                    .WithTaggedFlag("GTS", 0)
                },
                {(long)Registers.PhysicalAddressLower, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out lowerMAC, writeCallback: (_, value) =>
                    {
                        UpdateMac();
                    }, name: "PADDR1")
                },
                {(long)Registers.PhysicalAddressUpper, new DoubleWordRegister(this)
                    .WithValueField(16, 16, out upperMAC, writeCallback: (_, value) =>
                    {
                        UpdateMac();
                    },name: "PADDR2")
                    .WithValueField(0, 16, name: "TYPE")
                },
                {(long)Registers.TransmitFIFOWatermark, new DoubleWordRegister(this)
                    .WithReservedBits(9, 22)
                    .WithTaggedFlag("STRFWD", 8)
                    .WithReservedBits(6, 2)
                    .WithValueField(0, 6, name:"TFWR")
                },
                {(long)Registers.ReceiveDescriptorRingStart, new DoubleWordRegister(this)
                    .WithValueField(2, 30, name:"R_DES_START",
                        writeCallback: (oldValue, value) =>
                        {
                            if(receiverEnabled.Value)
                            {
                                this.Log(LogLevel.Warning, "Changing value of receive buffer queue base address while reception is enabled is illegal");
                                return;
                            }
                            rxDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaRxBufferDescriptor>(sysbus, (uint)value << 2, (sb, addr) => new DmaRxBufferDescriptor(sb, addr, extendedMode.Value));
                        })
                    .WithReservedBits(1, 1)
                    .WithReservedBits(0, 1)
                },
                {(long)Registers.TransmitBufferDescriptorRingStart, new DoubleWordRegister(this)
                    .WithValueField(2, 30, name:"X_DES_START",
                        writeCallback: (oldValue, value) =>
                        {
                            if(isTransmissionStarted)
                            {
                                this.Log(LogLevel.Warning, "Changing value of transmit buffer descriptor ring start address while transmission is started is illegal");
                                return;
                            }
                            txDescriptorsQueue = new DmaBufferDescriptorsQueue<DmaTxBufferDescriptor>(sysbus, (uint)value << 2, (sb, addr) => new DmaTxBufferDescriptor(sb, addr, extendedMode.Value));
                        })
                    .WithReservedBits(1, 1)
                    .WithReservedBits(0, 1)
                },
                {(long)Registers.TransmitAcceleratorFunctionConfiguration, new DoubleWordRegister(this)
                    .WithTaggedFlag("SHIFT16", 0)
                    .WithReservedBits(1, 2)
                    .WithFlag(3, out insertIPHeaderChecksum, name: "IPCHK")
                    .WithFlag(4, out insertProtocolChecksum, name: "PROCHK")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.ReceiveAcceleratorFunctionConfiguration, new DoubleWordRegister(this)
                    .WithTaggedFlag("PADREM", 0)
                    .WithFlag(1, out discardIPHeaderInvalidChecksum, name: "IPDIS")
                    .WithFlag(2, out discardProtocolInvalidChecksum, name: "PRODIS")
                    .WithReservedBits(3, 3)
                    .WithFlag(6, out discardWithMACLayerError, name: "LINEDIS")
                    .WithTaggedFlag("SHIFT16", 7)
                    .WithReservedBits(8, 24)
                }
            };

            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            lock(innerLock)
            {
                this.Log(LogLevel.Debug, "Received packet, length {0}", frame.Bytes.Length);
                if(!receiverEnabled.Value)
                {
                    this.Log(LogLevel.Info, "Receiver not enabled, dropping frame");
                    return;
                }

                if(discardWithMACLayerError.Value && !EthernetFrame.CheckCRC(frame.Bytes))
                {
                    this.Log(LogLevel.Info, "Invalid CRC, packet discarded");
                    return;
                }

                if(discardIPHeaderInvalidChecksum.Value)
                {
                    var packet = (IPv4Packet)frame.UnderlyingPacket.Extract(typeof(IPv4Packet));
                    if(packet != null && !packet.ValidChecksum)
                    {
                        this.Log(LogLevel.Info, "Invalid IpV4 checksum, packet discarded");
                        return;
                    }
                }

                if(discardProtocolInvalidChecksum.Value)
                {
                    var tcpPacket = (TcpPacket)frame.UnderlyingPacket.Extract(typeof(TcpPacket));
                    if(tcpPacket != null && !tcpPacket.ValidChecksum)
                    {
                        this.Log(LogLevel.Info, "Invalid TCP checksum, packet discarded");
                        return;
                    }

                    var udpPacket = (UdpPacket)frame.UnderlyingPacket.Extract(typeof(UdpPacket));
                    if(udpPacket != null && !udpPacket.ValidChecksum)
                    {
                        this.Log(LogLevel.Info, "Invalid UDP checksum, packet discarded");
                        return;
                    }

                    var icmpv4Packet = (ICMPv4Packet)frame.UnderlyingPacket.Extract(typeof(ICMPv4Packet));
                    if(icmpv4Packet != null)
                    {
                        var checksum = icmpv4Packet.Checksum;
                        icmpv4Packet.Checksum = 0x0;
                        icmpv4Packet.UpdateCalculatedValues();
                        if(checksum != icmpv4Packet.Checksum)
                        {
                            this.Log(LogLevel.Info, "Invalid ICMPv4 checksum, packet discarded");
                            return;
                        }
                    }

                    var icmpv6Packet = (ICMPv6Packet)frame.UnderlyingPacket.Extract(typeof(ICMPv6Packet));
                    if(icmpv6Packet != null)
                    {
                        var checksum = icmpv6Packet.Checksum;
                        icmpv6Packet.Checksum = 0x0;
                        icmpv6Packet.UpdateCalculatedValues();
                        if(checksum != icmpv6Packet.Checksum)
                        {
                            this.Log(LogLevel.Info, "Invalid ICMPv6 checksum, packet discarded");
                            return;
                        }
                    }
                }

                rxDescriptorsQueue.CurrentDescriptor.Read();
                if(rxDescriptorsQueue.CurrentDescriptor.IsEmpty)
                {
                    if(!rxDescriptorsQueue.CurrentDescriptor.WriteBuffer(frame.Bytes, (uint)frame.Bytes.Length))
                    {
                        // The current implementation doesn't handle packets that do not fit into a single buffer.
                        // In case we encounter this error, we probably should implement partitioning/scattering procedure.
                        this.Log(LogLevel.Warning, "Could not write the incoming packet to the DMA buffer: maximum packet length exceeded.");
                        return;
                    }

                    rxDescriptorsQueue.CurrentDescriptor.Length = (ushort)frame.Bytes.Length;
                    // Packets going over several buffers not supported
                    rxDescriptorsQueue.CurrentDescriptor.IsLast = true;
                    rxDescriptorsQueue.CurrentDescriptor.IsEmpty = false;
                    // write this back to memory
                    rxDescriptorsQueue.CurrentDescriptor.Update();

                    rxDescriptorsQueue.GoToNextDescriptor();

                    interruptManager.SetInterrupt(Interrupts.ReceiveBufferInterrupt);
                    interruptManager.SetInterrupt(Interrupts.ReceiveFrameInterrupt);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Receive DMA buffer overflow");
                }
            }
        }

        public override void Reset()
        {
            isTransmissionStarted = false;
            registers.Reset();
            txDescriptorsQueue = null;
            rxDescriptorsQueue = null;
            interruptManager.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public MACAddress MAC { get; set; }

        public long Size => 0x1000;

        public event Action<EthernetFrame> FrameReady;

        [IrqProvider("timer irq", 4)]
        public GPIO TimerIRQ { get; }

        [IrqProvider("ptp irq", 3)]
        public GPIO PtpIRQ { get; }

        [IrqProvider("misc irq", 2)]
        public GPIO MiscIRQ { get; }

        [IrqProvider("receive irq", 1)]
        public GPIO RxIRQ { get; }

        [IrqProvider("transmit irq", 0)]
        public GPIO TxIRQ { get; }

        private void UpdateMac()
        {
            var finalMac = (ulong)((lowerMAC.Value << 32) + upperMAC.Value);
            this.Log(LogLevel.Info, "Setting MAC to {0:X}", finalMac);
            MAC = new MACAddress(finalMac);
        }

        private void SendSingleFrame(IEnumerable<byte> bytes, bool isCRCIncluded)
        {
            if(forwardCRCFromApplication.Value && !isCRCIncluded)
            {
                this.Log(LogLevel.Error, "CRC needs to be provided by the application but is missing");
                return;
            }

            var bytesArray = bytes.ToArray();
            var newLength = isCRCIncluded ? 64 : 60;
            if(bytesArray.Length < newLength)
            {
                Array.Resize(ref bytesArray, newLength);
            }

            var addCrc = !isCRCIncluded && !forwardCRCFromApplication.Value;
            if(!Misc.TryCreateFrameOrLogWarning(this, bytesArray, out var frame, addCrc))
            {
                return;
            }

            if(insertProtocolChecksum.Value)
            {
                frame.FillWithChecksums(new EtherType[] {}, new [] { IPProtocolType.ICMP, IPProtocolType.ICMPV6, IPProtocolType.TCP, IPProtocolType.UDP });
            }
            if(insertIPHeaderChecksum.Value)
            {
                frame.FillWithChecksums(new [] { EtherType.IpV4 }, new IPProtocolType[] {});
            }

            this.Log(LogLevel.Debug, "Sending packet, length {0}", frame.Bytes.Length);
            FrameReady?.Invoke(frame);
        }

        private void SendFrames()
        {
            lock(innerLock)
            {
                var packetBytes = new List<byte>();
                txDescriptorsQueue.CurrentDescriptor.Read();
                while(txDescriptorsQueue.CurrentDescriptor.IsReady)
                {
                    // fill packet with data from memory
                    this.Log(LogLevel.Debug, "Buffer Length {0}", txDescriptorsQueue.CurrentDescriptor.Length);
                    packetBytes.AddRange(txDescriptorsQueue.CurrentDescriptor.ReadBuffer());
                    if(txDescriptorsQueue.CurrentDescriptor.IsLast)
                    {
                        // IncludeCRC is only valid for the buffer with IsLast set
                        SendSingleFrame(packetBytes, !txDescriptorsQueue.CurrentDescriptor.IncludeCRC);
                        packetBytes.Clear();
                    }

                    // Need to update the ready flag after processing
                    txDescriptorsQueue.CurrentDescriptor.IsReady = false; // free it up
                    txDescriptorsQueue.CurrentDescriptor.Update(); // write back to memory
                    txDescriptorsQueue.GoToNextDescriptor();
                }

                this.Log(LogLevel.Debug, "Transmission completed");
                isTransmissionStarted = false;

                interruptManager.SetInterrupt(Interrupts.TransmitFrameInterrupt);
                interruptManager.SetInterrupt(Interrupts.TransmitBufferInterrupt);
            }
        }

        private uint HandlePhyRead()
        {
            return phyDataRead;
        }

        private void HandlePhyWrite(uint value)
        {
            lock(innerLock)
            {
                var data = (ushort)(value & 0xFFFF);
                var reg = (ushort)((value >> 18) & 0x1F);
                var addr = (uint)((value >> 23) & 0x1F);
                var op = (PhyOperation)((value >> 28) & 0x3);

                if(!TryGetPhy<ushort>(addr, out var phy))
                {
                    this.Log(LogLevel.Warning, "Write to PHY with unknown address {0}", addr);
                    phyDataRead = 0xFFFFU;
                    return;
                }

                switch(op)
                {
                    case PhyOperation.Read:
                        phyDataRead = phy.Read(reg);
                        break;
                    case PhyOperation.Write:
                        phy.Write(reg, data);
                        break;
                    default:
                        this.Log(LogLevel.Warning, "Unknown PHY operation code 0x{0:X}", op);
                        break;
                }

                interruptManager.SetInterrupt(Interrupts.MIIInterrupt);
            }
        }
        private uint phyDataRead;
        private DmaBufferDescriptorsQueue<DmaTxBufferDescriptor> txDescriptorsQueue;
        private DmaBufferDescriptorsQueue<DmaRxBufferDescriptor> rxDescriptorsQueue;

        private readonly IBusController sysbus;
        private readonly InterruptManager<Interrupts> interruptManager;
        private readonly DoubleWordRegisterCollection registers;
        private readonly object innerLock;

        // Fields needed for the internal logic
        private bool isTransmissionStarted = false;

        //EthernetControl
        private readonly IFlagRegisterField extendedMode;

        //TransmitControl
        private readonly IFlagRegisterField forwardCRCFromApplication;

        //ReceiveControl
        private readonly IFlagRegisterField receiverEnabled;

        //TransmitAcceleratorFunctionConfiguration
        private readonly IFlagRegisterField insertIPHeaderChecksum;
        private readonly IFlagRegisterField insertProtocolChecksum;

        //ReceiveAcceleratorFunctionConfiguration
        private readonly IFlagRegisterField discardIPHeaderInvalidChecksum;
        private readonly IFlagRegisterField discardProtocolInvalidChecksum;
        private readonly IFlagRegisterField discardWithMACLayerError;

        private readonly IValueRegisterField lowerMAC;
        private readonly IValueRegisterField upperMAC;

        private enum Registers
        {
            InterruptEvent = 0x0004,
            InterruptMask = 0x0008,
            ReceiveDescriptorActive = 0x0010,
            TransmitDescriptorActive = 0x0014,
            EthernetControl = 0x0024,
            MIIManagementFrame = 0x0040,
            MIISpeedControl = 0x0044,
            MIBControl = 0x0064,
            ReceiveControl = 0x0084,
            TransmitControl = 0x00C4,
            PhysicalAddressLower = 0x00E4,
            PhysicalAddressUpper = 0x00E8,
            OpcodePauseDuration = 0x00EC,
            TransmitInterruptCoalescing = 0x00F0,
            ReceiveInterruptCoalescing = 0x0100,
            DescriptorIndividualUpperAddress = 0x0118,
            DescriptorIndividualLowerAddress = 0x011C,
            DescriptorGroupUpperAddress = 0x0120,
            DescriptorGroupLowerAddress = 0x0124,
            TransmitFIFOWatermark = 0x0144,
            ReceiveDescriptorRingStart = 0x0180,
            TransmitBufferDescriptorRingStart = 0x0184,
            MaximumReceiveBufferSize = 0x0188,
            ReceiveFIFOSectionFullThreshold = 0x0190,
            ReceiveFIFOSectionEmptyThreshold = 0x0194,
            ReceiveFIFOAlmostEmptyThreshold = 0x0198,
            ReceiveFIFOAlmostFullThreshold = 0x019C,
            TransmitFIFOSectionEmptyThreshold = 0x01A0,
            TransmitFIFOAlmostEmptyThreshold = 0x01A4,
            TransmitFIFOAlmostFullThreshold = 0x01A8,
            TransmitInterPacketGap = 0x01AC,
            FrameTruncationLength = 0x01B0,
            TransmitAcceleratorFunctionConfiguration = 0x01C0,
            ReceiveAcceleratorFunctionConfiguration = 0x01C4,
            TxPacketCountStatistic = 0x0204,
            TxBroadcastPacketsStatistic = 0x0208,
            TxMulticastPacketsStatistic = 0x020C,
            TxPacketswithCRCAlignErrorStatistic = 0x0210,
            TxPacketsLessThanBytesandGoodCRCStatistic = 0x0214,
            TxPacketsGTMAX_FLbytesandGoodCRCStatistic = 0x0218,
            TxPacketsLessThan64BytesandBadCRCStatistic = 0x021C,
            TxPacketsGreaterThanMAX_FLbytesandBadCRC = 0x0220,
            TxCollisionCountStatistic = 0x0224,
            Tx64BytePacketsStatistic = 0x0228,
            Tx65to127bytePacketsStatistic = 0x022C,
            Tx128to255bytePacketsStatistic = 0x0230,
            Tx256to511bytePacketsStatistic = 0x0234,
            Tx512to1023bytePacketsStatistic = 0x0238,
            Tx1024to2047bytePacketsStatistic = 0x023C,
            TxPacketsGreaterThan2048BytesStatistic = 0x0240,
            TxOctetsStatistic = 0x0244,
            FramesTransmittedOKStatistic = 0x024C,
            FramesTransmittedwithSingleCollisionStatistic = 0x0250,
            FramesTransmittedwithMultipleCollisionsStatistic = 0x0254,
            FramesTransmittedafterDeferralDelayStatistic = 0x0258,
            FramesTransmittedwithLateCollisionStatistic = 0x025C,
            FramesTransmittedwithExcessiveCollisionsStatistic = 0x0260,
            FramesTransmittedwithTxFIFOUnderrunStatistic = 0x0264,
            FramesTransmittedwithCarrierSenseErrorStatistic = 0x0268,
            FlowControlPauseFramesTransmittedStatistic = 0x0270,
            OctetCountforFramesTransmittedwoErrorStatistic = 0x0274,
            RxPacketCountStatistic = 0x0284,
            RxBroadcastPacketsStatistic = 0x0288,
            RxMulticastPacketsStatistic = 0x028C,
            RxPacketswithCRCAlignErrorStatistic = 0x0290,
            RxPacketswithLessThan64BytesandGoodCRC = 0x0294,
            RxPacketsGreaterThanMAX_FLandGoodCRCStatistic = 0x0298,
            RxPacketsLessThan64BytesandBadCRCStatistic = 0x029C,
            RxPacketsGreaterThanMAX_FLBytesandBadCRC = 0x02A0,
            Rx64BytePacketsStatistic = 0x02A8,
            Rx65to127BytePacketsStatistic = 0x02AC,
            Rx128to255BytePacketsStatistic = 0x02B0,
            Rx256to511BytePacketsStatistic = 0x02B4,
            Rx512to1023BytePacketsStatistic = 0x02B8,
            Rx1024to2047BytePacketsStatistic = 0x02BC,
            RxPacketsGreaterthan2048BytesStatistic = 0x02C0,
            RxOctetsStatistic = 0x02C4,
            FramesnotCountedCorrectlyStatistic = 0x02C8,
            FramesReceivedOKStatistic = 0x02CC,
            FramesReceivedwithCRCErrorStatistic = 0x02D0,
            FramesReceivedwithAlignmentErrorStatistic = 0x02D4,
            ReceiveFIFOOverflowCountStatistic = 0x02D8,
            FlowControlPauseFramesReceivedStatistic = 0x02DC,
            OctetCountforFramesReceivedwithoutErrorStatistic = 0x02E0,
            AdjustableTimerControl = 0x0400,
            TimerValue = 0x0404,
            TimerOffset = 0x0408,
            TimerPeriod = 0x040C,
            TimerCorrection = 0x0410,
            TimeStampingClockPeriod = 0x0414,
            TimestampofLastTransmittedFrame = 0x0418,
            TimerGlobalStatus = 0x0604,
            TimerControlStatusR0 = 0x0608,
            TimerCompareCaptureR0 = 0x060C,
            TimerControlStatusR1 = 0x0610,
            TimerCompareCaptureR1 = 0x0614,
            TimerControlStatusR2 = 0x0618,
            TimerCompareCaptureR2 = 0x061C,
            TimerControlStatusR3 = 0x0620,
            TimerCompareCaptureR3 = 0x0624
        }

        private enum Interrupts
        {
            [Subvector(1)]
            BabblingReceiveError = 30,
            [Subvector(0)]
            BabblingTransmitError = 29,
            [Subvector(2)]
            GracefulStopComplete = 28,
            [Subvector(0)]
            TransmitFrameInterrupt = 27,
            [Subvector(0)]
            TransmitBufferInterrupt = 26,
            [Subvector(1)]
            ReceiveFrameInterrupt = 25,
            [Subvector(1)]
            ReceiveBufferInterrupt = 24,
            [Subvector(2)]
            MIIInterrupt = 23,
            [Subvector(2)]
            EthernetBusError = 22,
            [Subvector(2)]
            LateCollision = 21,
            [Subvector(2)]
            CollisionRetryLimit = 20,
            [Subvector(2)]
            TransmitFIFOUnderrun = 19,
            [Subvector(2)]
            PayloadReceiveError = 18,
            [Subvector(2)]
            NodeWakeupRequestIndication = 17,
            [Subvector(3)]
            TransmitTimestampAvailable = 16,
            [Subvector(4)]
            TimestampTimer = 15
        }

        private enum PhyOperation
        {
            Write = 0x1,
            Read = 0x2
        }

        private class DmaBufferDescriptorsQueue<T> where T : DmaBufferDescriptor
        {
            public DmaBufferDescriptorsQueue(IBusController bus, uint baseAddress, Func<IBusController, uint, T> creator)
            {
                this.bus = bus;
                this.creator = creator;
                this.baseAddress = baseAddress;
                descriptors = new List<T>();
                GoToNextDescriptor();
            }

            public void GoToNextDescriptor()
            {
                if(descriptors.Count == 0)
                {
                    // this is the first descriptor - read it from baseAddress
                    descriptors.Add(creator(bus, baseAddress));
                    currentDescriptorIndex = 0;
                }
                else
                {
                    // If wrap is set, we have reached end of ring and need to start from the beginning
                    if(CurrentDescriptor.Wrap)
                    {
                        currentDescriptorIndex = 0;
                    }
                    else
                    {
                        descriptors.Add(creator(bus, CurrentDescriptor.DescriptorAddress + CurrentDescriptor.SizeInBytes));
                        currentDescriptorIndex++;
                    }
                }
                CurrentDescriptor.Read();
            }

            public void GoToBaseAddress()
            {
                currentDescriptorIndex = 0;
                CurrentDescriptor.Read();
            }

            public T CurrentDescriptor => descriptors[currentDescriptorIndex];

            private int currentDescriptorIndex;

            private readonly List<T> descriptors;
            private readonly uint baseAddress;
            private readonly IBusController bus;
            private readonly Func<IBusController, uint, T> creator;
        }

        private class DmaBufferDescriptor
        {
            protected DmaBufferDescriptor(IBusController bus, uint address, bool isExtendedModeEnabled)
            {
                Bus = bus;
                DescriptorAddress = address;
                IsExtendedModeEnabled = isExtendedModeEnabled;
                SizeInBytes = InitWords();
            }

            public void Read()
            {
                var tempOffset = 0UL;
                for(var i = 0; i < words.Length; ++i)
                {
                    words[i] = Bus.ReadDoubleWord(DescriptorAddress + tempOffset);
                    tempOffset += 2;
                }
            }

            public void Update()
            {
                var tempOffset = 0UL;
                foreach(var word in words)
                {
                    Bus.WriteDoubleWord(DescriptorAddress + tempOffset, word);
                    tempOffset += 2;
                }
            }

            public IBusController Bus { get; }
            public uint SizeInBytes { get; }
            public bool IsExtendedModeEnabled { get; }
            public uint DescriptorAddress { get; }

            public bool Wrap => BitHelper.IsBitSet(words[1], 13);

            public uint DataBufferAddress => (words[3] << 16) | words[2];

            public bool IsLast => BitHelper.IsBitSet(words[1], 11);

            protected uint[] words;

            private uint InitWords()
            {
                if(IsExtendedModeEnabled)
                {
                    words = new uint[16];
                }
                else
                {
                    words = new uint[4];
                }
                return (uint)words.Length * 2;
            }
        }

        /// Legacy Transmit Buffer
        ///
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| R  | TO1| W  | TO2| L  | TC |ABC | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +4|                       Tx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Tx Data Buffer Pointer -- high halfword                 |
        ///          =================================================================================
        ///
        ///
        /// Enhanced Transmit Buffer
        ///
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| R  | TO1| W  | TO2| L  | TC | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +4|                       Tx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Tx Data Buffer Pointer -- high halfword                 |
        /// Offset +8| TXE| -- | UE | EE | FE | LCE| OE | TSE| -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +A| -- | INT| TS |PINS|IINS| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +C| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset +E| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+10| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+12| BDU| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+14|                       1588 timestamp - low halfword                           |
        /// Offset+16|                       1588 timestamp - high halfword                          |
        /// Offset+18| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1A| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1C| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1E| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        ///          =================================================================================
        private class DmaTxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaTxBufferDescriptor(IBusController bus, uint address, bool isExtendedModeEnabled) :
                base(bus, address, isExtendedModeEnabled)
            {
            }

            public byte[] ReadBuffer()
            {
                return Bus.ReadBytes(DataBufferAddress, Length, true);
            }

            public ushort Length => (ushort)words[0];

            public bool IncludeCRC => BitHelper.IsBitSet(words[1], 10);

            public bool IsReady
            {
                get
                {
                    return BitHelper.IsBitSet(words[1], 15);
                }
                set
                {
                    BitHelper.SetBit(ref words[1], 15, value);
                }
            }
        }

        /// Legacy Receive Buffer
        ///
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| E  | RO1| W  | RO2| L  | -- | -- | M  | BC | MC | LG | NO | -- | -- | -- | TR |
        /// Offset +4|                       Rx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Rx Data Buffer Pointer -- high halfword                 |
        ///          =================================================================================
        ///
        ///
        /// Enhanced Receive Buffer
        ///
        ///          =================================================================================
        ///          |             Byte 1                    |           Byte 0                      |
        ///          | 15 | 14 | 13 | 12 | 11 | 10 | 09 | 08 | 07 | 06 | 05 | 04 | 03 | 02 | 01 | 00 |
        ///          =================================================================================
        /// Offset +0|                              Data Length                                      |
        /// Offset +2| E  | RO1| W  | RO2| L  | -- | -- | M  | BC | MC | LG | NO | -- | CR | OV | TR |
        /// Offset +4|                       Rx Data Buffer Pointer -- low halfword                  |
        /// Offset +6|                       Rx Data Buffer Pointer -- high halfword                 |
        /// Offset +8|    VPCP      | -- | -- | -- | -- | -- | -- | -- | ICE| PCR| -- |VLAN|IPV6|FRAG|
        /// Offset +A| ME | -- | -- | -- | -- | PE | CE | UC | INT| -- | -- | -- | -- | -- | -- | -- |
        /// Offset +C|                              Payload Checksum                                 |
        /// Offset +E|     Header length      | -- | -- | -- |              Protocol Type            |
        /// Offset+10| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+12| BDU| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+14|                       1588 timestamp - low halfword                           |
        /// Offset+16|                       1588 timestamp - high halfword                          |
        /// Offset+18| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1A| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1C| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        /// Offset+1E| -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- | -- |
        ///          =================================================================================
        private class DmaRxBufferDescriptor : DmaBufferDescriptor
        {
            public DmaRxBufferDescriptor(IBusController bus, uint address, bool isExtendedModeEnabled) :
                base(bus, address, isExtendedModeEnabled)
            {
            }

            public byte[] ReadBuffer()
            {
                return Bus.ReadBytes(DataBufferAddress, Length, true);
            }

            public ushort Length
            {
                get
                {
                    return (ushort)words[0];
                }
                set
                {
                    words[0] = value;
                }
            }

            public bool IsEmpty
            {
                get
                {
                    return BitHelper.IsBitSet(words[1], 15);
                }
                set
                {
                    BitHelper.SetBit(ref words[1], 15, value);
                }
            }

            public new bool IsLast
            {
                set
                {
                    BitHelper.SetBit(ref words[1], 11, value);
                }
            }

            public bool WriteBuffer(byte[] bytes, uint length)
            {
                if(bytes.Length > MaximumBufferLength)
                {
                    return false;
                }

                Length = (ushort)Length;
                Bus.WriteBytes(bytes, DataBufferAddress, true);

                return true;
            }

            private const int MaximumBufferLength = (1 << 13) - 1;
        }
    }
}
