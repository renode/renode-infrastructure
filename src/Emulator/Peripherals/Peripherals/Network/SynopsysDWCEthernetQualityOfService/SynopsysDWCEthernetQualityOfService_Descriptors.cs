//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Network
{
    public partial class SynopsysDWCEthernetQualityOfService
    {
        private class RxDescriptor : Descriptor
        {
            public RxDescriptor(IBusController bus, ulong address, ICPU cpuContext = null) : base(bus, address, cpuContext)
            {
            }

            public override void Fetch()
            {
                base.Fetch();
                type = typeof(NormalReadDescriptor);
            }

            public NormalReadDescriptor GetNormalReadDescriptor()
            {
                return Packet.Decode<NormalReadDescriptor>(data);
            }

            public NormalWriteBackDescriptor GetAsNormalWriteBackDescriptor()
            {
                return Packet.Decode<NormalWriteBackDescriptor>(data);
            }

            public ContextDescriptor GetAsContextDescriptor()
            {
                return Packet.Decode<ContextDescriptor>(data);
            }

            [LeastSignificantByteFirst]
            public struct NormalReadDescriptor : IDescriptorStruct
            {
                public override string ToString()
                {
                    return PrettyString;
                }

                public string PrettyString => $@"NormalReadRxDescriptor {{
    buffer1Address: 0x{Buffer1Address:X},
    buffer2Address: 0x{Buffer2Address:X},
    buffer1AddressValid: {Buffer1AddressValid},
    buffer2AddressValid: {Buffer2AddressValid},
    interruptOnCompletion: {InterruptOnCompletion},
    owner: {Owner}
}}";

                public bool ContextType => false;

                public DescriptorOwner Owner => OwnerField;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(32)] // BUF1AP
                public uint Buffer1Address;
                // 2nd double word is reserved
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(32)] // BUF2AP
                public uint Buffer2Address;
                // bits 0:23 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 24), Width(1)] // BUF1V
                public bool Buffer1AddressValid;
                [PacketField, Offset(doubleWords: 3, bits: 25), Width(1)] // BUF2V
                public bool Buffer2AddressValid;
                // bits 26:29 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // IOC
                public bool InterruptOnCompletion;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner OwnerField;
#pragma warning restore 649
            }

            [LeastSignificantByteFirst]
            public struct NormalWriteBackDescriptor : IDescriptorStruct
            {
                public override string ToString()
                {
                    return PrettyString;
                }

                public string PrettyString => $@"NormalWriteBackRxDescriptor {{
    outerVlanTag: 0x{OuterVlanTag:X},
    innerVlanTag: 0x{InnerVlanTag:X},
    payloadType: {PayloadType},
    ipHeaderError: {IpHeaderError},
    ipv4HeaderPresent: {Ipv4HeaderPresent},
    ipv6HeaderPresent: {Ipv6HeaderPresent},
    ipChecksumBypassed: {IpChecksumBypassed},
    ipPayloadError: {IpPayloadError},
    ptpMessageType: {PtpMessageType},
    ptpPacketType: {PtpPacketType},
    ptpVersion: {PtpVersion},
    timestampAvailable: {TimestampAvailable},
    timestampDropped: {TimestampDropped},
    oamSubtypeCodeOrMACControlPacketOpcode: 0x{OamSubtypeCodeOrMACControlPacketOpcode:X} or {(EtherType)OamSubtypeCodeOrMACControlPacketOpcode},
    arpReplyNotGenerated: {ArpReplyNotGenerated},
    vlanFiletrStatus: {VlanFiletrStatus},
    sourceAddressFilterFail: {SourceAddressFilterFail},
    destinaltionAddressFilterFail: {DestinaltionAddressFilterFail},
    hashFilterStatus: {HashFilterStatus},
    macAddressMatchOrHashValue: {MacAddressMatchOrHashValue},
    layer3FilterMatch: {Layer3FilterMatch},
    layer4FilterMatch: {Layer4FilterMatch},
    matchedFilterNumber: {MatchedFilterNumber},
    packetLength: {PacketLength},
    errorSummary: {ErrorSummary},
    lengthTypeField: {LengthTypeField},
    dribbleBitError: {DribbleBitError},
    receiveError: {ReceiveError},
    overflowError: {OverflowError},
    receiveWatchdogTimeout: {ReceiveWatchdogTimeout},
    giantPacket: {GiantPacket},
    crcError: {CrcError},
    receiveStatusSegment0Valid: {ReceiveStatusSegment0Valid},
    receiveStatusSegment1Valid: {ReceiveStatusSegment1Valid},
    receiveStatusSegment2Valid: {ReceiveStatusSegment2Valid},
    lastDescriptor: {LastDescriptor},
    firstDescriptor: {FirstDescriptor},
    contextType: {ContextType},
    owner: {Owner}
}}";

                public bool ContextType => ContextTypeField;

                public DescriptorOwner Owner => OwnerField;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(16)] // OVT
                public uint OuterVlanTag;
                [PacketField, Offset(doubleWords: 0, bits: 16), Width(16)] // IVT
                public uint InnerVlanTag;
                [PacketField, Offset(doubleWords: 1, bits: 0), Width(3)] // PT
                public PayloadType PayloadType;
                [PacketField, Offset(doubleWords: 1, bits: 3), Width(1)] // IPHE
                public bool IpHeaderError;
                [PacketField, Offset(doubleWords: 1, bits: 4), Width(1)] // IPV4
                public bool Ipv4HeaderPresent;
                [PacketField, Offset(doubleWords: 1, bits: 5), Width(1)] // IPV6
                public bool Ipv6HeaderPresent;
                [PacketField, Offset(doubleWords: 1, bits: 6), Width(1)] // IPCB
                public bool IpChecksumBypassed;
                [PacketField, Offset(doubleWords: 1, bits: 7), Width(1)] // IPCE
                public bool IpPayloadError;
                [PacketField, Offset(doubleWords: 1, bits: 8), Width(4)] // PMT
                public PTPMessageType PtpMessageType;
                [PacketField, Offset(doubleWords: 1, bits: 12), Width(1)] // PFT
                public bool PtpPacketType;
                [PacketField, Offset(doubleWords: 1, bits: 13), Width(1)] // PV
                public PTPVersion PtpVersion;
                [PacketField, Offset(doubleWords: 1, bits: 14), Width(1)] // TSA
                public bool TimestampAvailable;
                [PacketField, Offset(doubleWords: 1, bits: 15), Width(1)] // TD
                public bool TimestampDropped;
                [PacketField, Offset(doubleWords: 1, bits: 16), Width(16)] // OPC
                public uint OamSubtypeCodeOrMACControlPacketOpcode;
                // bits 0:9 of 3rd double word are reserved
                [PacketField, Offset(doubleWords: 2, bits: 10), Width(1)] // ARPNR
                public bool ArpReplyNotGenerated;
                // bits 11:14 of 3rd double word are reserved
                [PacketField, Offset(doubleWords: 2, bits: 15), Width(1)] // VF
                public bool VlanFiletrStatus;
                [PacketField, Offset(doubleWords: 2, bits: 16), Width(1)] // SAF
                public bool SourceAddressFilterFail;
                [PacketField, Offset(doubleWords: 2, bits: 17), Width(1)] // DAF
                public bool DestinaltionAddressFilterFail;
                [PacketField, Offset(doubleWords: 2, bits: 18), Width(1)] // HF
                public bool HashFilterStatus;
                [PacketField, Offset(doubleWords: 2, bits: 19), Width(8)] // MADRM
                public bool MacAddressMatchOrHashValue;
                [PacketField, Offset(doubleWords: 2, bits: 27), Width(1)] // L3FM
                public bool Layer3FilterMatch;
                [PacketField, Offset(doubleWords: 2, bits: 28), Width(1)] // L4FM
                public bool Layer4FilterMatch;
                [PacketField, Offset(doubleWords: 2, bits: 29), Width(3)] // L3L4FM
                public uint MatchedFilterNumber;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(15)] // PL
                public uint PacketLength;
                [PacketField, Offset(doubleWords: 3, bits: 15), Width(1)] // ES
                public bool ErrorSummary;
                [PacketField, Offset(doubleWords: 3, bits: 16), Width(3)] // LT
                public PacketKind LengthTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 19), Width(1)] // DE
                public bool DribbleBitError;
                [PacketField, Offset(doubleWords: 3, bits: 20), Width(1)] // RE
                public bool ReceiveError;
                [PacketField, Offset(doubleWords: 3, bits: 21), Width(1)] // OE
                public bool OverflowError;
                [PacketField, Offset(doubleWords: 3, bits: 22), Width(1)] // RWT
                public bool ReceiveWatchdogTimeout;
                [PacketField, Offset(doubleWords: 3, bits: 23), Width(1)] // GP
                public bool GiantPacket;
                [PacketField, Offset(doubleWords: 3, bits: 24), Width(1)] // CE
                public bool CrcError;
                [PacketField, Offset(doubleWords: 3, bits: 25), Width(1)] // RS0V
                public bool ReceiveStatusSegment0Valid;
                [PacketField, Offset(doubleWords: 3, bits: 26), Width(1)] // RS1V
                public bool ReceiveStatusSegment1Valid;
                [PacketField, Offset(doubleWords: 3, bits: 27), Width(1)] // RS2V
                public bool ReceiveStatusSegment2Valid;
                [PacketField, Offset(doubleWords: 3, bits: 28), Width(1)] // LD
                public bool LastDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 29), Width(1)] // FD
                public bool FirstDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool ContextTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner OwnerField;
#pragma warning restore 649
            }

            [LeastSignificantByteFirst]
            public struct ContextDescriptor : IDescriptorStruct
            {
                public override string ToString()
                {
                    return PrettyString;
                }

                public string PrettyString => $@"ContextRxDescriptor {{
    receivePacketTimestamp: 0x{ReceivePacketTimestamp:X},
    contextType: {ContextTypeField},
    owner: {OwnerField}
}}";

                public bool ContextType => ContextTypeField;

                public DescriptorOwner Owner => OwnerField;

#pragma warning disable 649
                [PacketField, Offset(quadWords: 0), Width(64)] // RTS
                public ulong ReceivePacketTimestamp;
                // 3nd double word and bits 0:29 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool ContextTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner OwnerField;
#pragma warning restore 649
            }
        }

        private class TxDescriptor : Descriptor
        {
            public TxDescriptor(IBusController bus, ulong address, ICPU cpuContext = null) : base(bus, address, cpuContext)
            {
            }

            public override void Fetch()
            {
                base.Fetch();
                type = isContextDescriptor.Value ? typeof(ContextDescriptor) : typeof(NormalReadDescriptor);
            }

            public NormalReadDescriptor GetNormalReadDescriptor()
            {
                return Packet.Decode<NormalReadDescriptor>(data);
            }

            public ContextDescriptor GetContextDescriptor()
            {
                return Packet.Decode<ContextDescriptor>(data);
            }

            [LeastSignificantByteFirst]
            public struct NormalReadDescriptor : IDescriptorStruct
            {
                public override string ToString()
                {
                    return PrettyString;
                }

                public byte[] FetchBuffer1OrHeader(IBusController bus, ICPU cpuContext = null)
                {
                    var data = new byte[HeaderOrBuffer1Length];
                    bus.ReadBytes((ulong)Buffer1OrHeaderAddress, (int)HeaderOrBuffer1Length, data, 0, true, cpuContext);
                    return data;
                }

                public byte[] FetchBuffer2OrBuffer1(IBusController bus, ICPU cpuContext = null)
                {
                    var data = new byte[Buffer2Length];
                    bus.ReadBytes((ulong)Buffer2orBuffer1Address, (int)Buffer2Length, data, 0, true, cpuContext);
                    return data;
                }

                public uint PayloadLength
                {
                    get
                    {
                        return PayloadLengthLower15bits | PayloadLength15bit << 15 | (uint)ChecksumControl << 16;
                    }
                }

                public string PrettyString => $@"NormalReadTxDescriptor {{
    buffer1OrHeaderAddress: 0x{Buffer1OrHeaderAddress:X},
    buffer2orBuffer1Address: 0x{Buffer2orBuffer1Address:X},
    headerOrBuffer1Length: 0x{HeaderOrBuffer1Length:X},
    vlanTagControl: {VlanTagControl},
    buffer2Length: 0x{Buffer2Length:X},
    transmitTimestampEnable: {TransmitTimestampEnable},
    interruptOnCompletion: {InterruptOnCompletion},
    payloadLengthLower15bits: {PayloadLengthLower15bits},
    payloadLength15bit: {PayloadLength15bit},
    checksumControl: {ChecksumControl},
    tcpSegmentationEnable: {TcpSegmentationEnable},
    headerLength: {HeaderLength},
    sourceAddressControl: {SourceAddressControl},
    crcPadControl: {CrcPadControl},
    lastDescriptor: {LastDescriptor},
    firstDescriptor: {FirstDescriptor},
    contextType: {ContextTypeField},
    owner: {OwnerField}
}}";

                public bool ContextType => ContextTypeField;

                public DescriptorOwner Owner => OwnerField;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(32)] // BUF1AP
                public uint Buffer1OrHeaderAddress;
                [PacketField, Offset(doubleWords: 1, bits: 0), Width(32)] // BUF2AP
                public uint Buffer2orBuffer1Address;
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(14)] // HR/B1L
                public uint HeaderOrBuffer1Length;
                [PacketField, Offset(doubleWords: 2, bits: 14), Width(2)] // VTIR
                public VLANTagOperation VlanTagControl;
                [PacketField, Offset(doubleWords: 2, bits: 16), Width(14)] // B2L
                public uint Buffer2Length;
                [PacketField, Offset(doubleWords: 2, bits: 30), Width(1)] // TTSE
                public bool TransmitTimestampEnable;
                [PacketField, Offset(doubleWords: 2, bits: 31), Width(1)] // IOC
                public bool InterruptOnCompletion;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(15)] // FL/TPL
                public uint PayloadLengthLower15bits;
                [PacketField, Offset(doubleWords: 3, bits: 15), Width(1)] // TPL
                public uint PayloadLength15bit;
                [PacketField, Offset(doubleWords: 3, bits: 16), Width(2)] // CIC/TPL
                public ChecksumOperation ChecksumControl;
                [PacketField, Offset(doubleWords: 3, bits: 18), Width(1)] // TSE
                public bool TcpSegmentationEnable;
                [PacketField, Offset(doubleWords: 3, bits: 19), Width(4)] // THL
                public uint HeaderLength;
                [PacketField, Offset(doubleWords: 3, bits: 23), Width(3)] // SAIC
                public DescriptorSourceAddressOperation SourceAddressControl;
                [PacketField, Offset(doubleWords: 3, bits: 26), Width(2)] // CPC
                public CRCPadOperation CrcPadControl;
                [PacketField, Offset(doubleWords: 3, bits: 28), Width(1)] // LD
                public bool LastDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 29), Width(1)] // FD
                public bool FirstDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool ContextTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner OwnerField;
#pragma warning restore 649
            }

            [LeastSignificantByteFirst]
            public struct NormalWriteBackDescriptor : IDescriptorStruct
            {
                public override string ToString()
                {
                    return PrettyString;
                }

                public string PrettyString => $@"NormalWriteBackTxDescriptor {{
    txPacketTimestamp: 0x{TxPacketTimestamp:X},
    ipHeaderError: {IpHeaderError},
    deferredBit: {DeferredBit},
    underflowError: {UnderflowError},
    excessiveDeferral: {ExcessiveDeferral},
    collisionCount: {CollisionCount},
    excessiveCollision: {ExcessiveCollision},
    lateCollision: {LateCollision},
    noCarrier: {NoCarrier},
    lossOfCarrier: {LossOfCarrier},
    payloadChecksumError: {PayloadChecksumError},
    packetFlushed: {PacketFlushed},
    jabberTimeout: {JabberTimeout},
    errorSummary: {ErrorSummary},
    txTimestampCaptured: {TxTimestampCaptured},
    lastDescriptor: {LastDescriptor},
    firstDescriptor: {FirstDescriptor},
    contextType: {ContextTypeField},
    owner: {OwnerField}
}}";

                public bool ContextType => ContextTypeField;

                public DescriptorOwner Owner => OwnerField;

#pragma warning disable 649
                [PacketField, Offset(quadWords: 0), Width(64)] // TTS
                public ulong TxPacketTimestamp;
                // 3nd double word is reserved
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(1)] // IHE
                public bool IpHeaderError;
                [PacketField, Offset(doubleWords: 3, bits: 1), Width(1)] // DB
                public bool DeferredBit;
                [PacketField, Offset(doubleWords: 3, bits: 2), Width(1)] // UF
                public bool UnderflowError;
                [PacketField, Offset(doubleWords: 3, bits: 3), Width(1)] // ED
                public bool ExcessiveDeferral;
                [PacketField, Offset(doubleWords: 3, bits: 4), Width(4)] // CC
                public bool CollisionCount;
                [PacketField, Offset(doubleWords: 3, bits: 8), Width(1)] // EC
                public bool ExcessiveCollision;
                [PacketField, Offset(doubleWords: 3, bits: 9), Width(1)] // LC
                public bool LateCollision;
                [PacketField, Offset(doubleWords: 3, bits: 10), Width(1)] // NC
                public bool NoCarrier;
                [PacketField, Offset(doubleWords: 3, bits: 11), Width(1)] // LoC
                public bool LossOfCarrier;
                [PacketField, Offset(doubleWords: 3, bits: 12), Width(1)] // PCE
                public bool PayloadChecksumError;
                [PacketField, Offset(doubleWords: 3, bits: 13), Width(1)] // FF
                public bool PacketFlushed;
                [PacketField, Offset(doubleWords: 3, bits: 14), Width(1)] // JT
                public bool JabberTimeout;
                [PacketField, Offset(doubleWords: 3, bits: 15), Width(1)] // ES
                public bool ErrorSummary;
                // bit 16 of 4th double word is reserved
                [PacketField, Offset(doubleWords: 3, bits: 17), Width(1)] // TTSS
                public bool TxTimestampCaptured;
                // bits 18:27 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 28), Width(1)] // LD
                public bool LastDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 29), Width(1)] // FD
                public bool FirstDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool ContextTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner OwnerField;
#pragma warning restore 649
            }

            [LeastSignificantByteFirst]
            public struct ContextDescriptor : IDescriptorStruct
            {
                public override string ToString()
                {
                    return PrettyString;
                }

                public string PrettyString => $@"ContextTxDescriptor {{
    txPacketTimestamp: {TxPacketTimestamp},
    maximumSegmentSize: {MaximumSegmentSize},
    innerVlanTag: {InnerVlanTag},
    vlanTag: {VlanTag},
    vlanTagValid: {VlanTagValid},
    innerVlanTagValid: {InnerVlanTagValid},
    innerVlanTagControl: {InnerVlanTagControl},
    contextDescriptorError: {ContextDescriptorError},
    oneStepTimestampCorrectionInputOrMaximumSegmentSizeValid: {OneStepTimestampCorrectionInputOrMaximumSegmentSizeValid},
    oneStepTimestampCorrectionEnable: {OneStepTimestampCorrectionEnable},
    contextType: {ContextTypeField},
    owner: {OwnerField}
}}";

                public bool ContextType => ContextTypeField;

                public DescriptorOwner Owner => OwnerField;

#pragma warning disable 649
                [PacketField, Offset(quadWords: 0), Width(64)] // TTS
                public ulong TxPacketTimestamp;
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(14)] // MSS
                public uint MaximumSegmentSize;
                // bits 14:15 of 3rd double word are reserved
                [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)] // IVT
                public uint InnerVlanTag;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(16)] // VT
                public uint VlanTag;
                [PacketField, Offset(doubleWords: 3, bits: 16), Width(1)] // VLTV
                public bool VlanTagValid;
                [PacketField, Offset(doubleWords: 3, bits: 17), Width(1)] // IVLTV
                public bool InnerVlanTagValid;
                [PacketField, Offset(doubleWords: 3, bits: 18), Width(2)] // IVTIR
                public VLANTagOperation InnerVlanTagControl;
                // bits 20:22 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 23), Width(1)] // CDE
                public bool ContextDescriptorError;
                // bits 24:25 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 26), Width(1)] // TCMSSV
                public bool OneStepTimestampCorrectionInputOrMaximumSegmentSizeValid;
                [PacketField, Offset(doubleWords: 3, bits: 27), Width(1)] // OSTC
                public bool OneStepTimestampCorrectionEnable;
                // bits 28:29 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool ContextTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner OwnerField;
#pragma warning restore 649
            }
        }

        private abstract class Descriptor
        {
            public static uint Size => 0x10;

            public Descriptor(IBusController bus, ulong address, ICPU cpuContext = null)
            {
                this.bus = bus;
                this.cpuContext = cpuContext;
                Address = address;
                this.data = new byte[Size];
            }

            public virtual void Fetch()
            {
                bus.ReadBytes(Address, (int)Size, data, 0, true, cpuContext);
                var structure = Packet.Decode<MinimalCommonDescriptor>(data);
                UpdateProperties(structure);
            }

            public void Write()
            {
                bus.WriteBytes(data, Address, context: cpuContext);
            }

            public void SetDescriptor<T>(T structure) where T : IDescriptorStruct
            {
                type = typeof(T);
                data = Packet.Encode<T>(structure);
                UpdateProperties(structure);
            }

            public Type Type => type;

            public ulong Address { get; }

            public bool? IsOwnedByDMA { get; protected set; }

            protected void UpdateProperties(IDescriptorStruct structure)
            {
                IsOwnedByDMA = structure.Owner == DescriptorOwner.DMA;
                isContextDescriptor = structure.ContextType;
            }

            protected bool? isContextDescriptor;
            protected Type type;
            protected byte[] data;

            private readonly IBusController bus;
            private readonly ICPU cpuContext;

            public interface IDescriptorStruct
            {
                bool ContextType { get; }

                DescriptorOwner Owner { get; }
            }

            [LeastSignificantByteFirst]
            private struct MinimalCommonDescriptor : IDescriptorStruct
            {
                public bool ContextType => ContextTypeField;

                public DescriptorOwner Owner => OwnerField;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool ContextTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner OwnerField;
#pragma warning restore 649
            }
        }

        private enum DescriptorOwner : byte
        {
            DMA = 1,
            Application = 0,
        }

        private enum PayloadType : byte
        {
            Unknown  = 0b000,
            UDP      = 0b001,
            TCP      = 0b010,
            ICMP     = 0b011,
            IGMPIPV4 = 0b100,
        }

        private enum PTPMessageType : byte
        {
            NoPTPMessageReceived             = 0b0000,
            Sync                             = 0b0001,
            FollowUp                         = 0b0010,
            DelayRequest                     = 0b0011,
            DelayResponse                    = 0b0100,
            PdelayRequest                    = 0b0101,
            PdelayResponse                   = 0b0110,
            PdelayResponseFollowUp           = 0b0111,
            Announce                         = 0b1000,
            Management                       = 0b1001,
            Signaling                        = 0b1010,
            // Reserved                      = 0b1011..0b1110,
            PTPPacketWithReservedMessageType = 0b1111,
        }

        private enum PTPVersion : byte
        {
            IEEE1588version2 = 1,
            IEEE1588version1 = 0,
        }

        private enum PacketKind : byte
        {
            LengthPacket                = 0b000,
            TypePacket                  = 0b001,
            // Reserved                 = 0b010,
            ARPRequest                  = 0b011,
            TypePacketWithVLANTag       = 0b100,
            TypePacketWithDoubleVLANTag = 0b101,
            MACControlPacket            = 0b110,
            OAMPacket                   = 0b111,
        }

        private enum VLANTagOperation : byte
        {
            None    = 0b00,
            Remove  = 0b01,
            Insert  = 0b10,
            Replace = 0b11,
        }

        private enum ChecksumOperation : byte
        {
            None                                       = 0b00,
            InsertHeaderChecksum                       = 0b01,
            InsertHeaderAndPayloadChecksum             = 0b10,
            InsertHeaderPayloadAndPseudoHeaderChecksum = 0b11,
        }

        private enum DescriptorSourceAddressOperation : byte
        {
            MACAddressRegister0None     = 0b000,
            MACAddressRegister0Insert   = 0b001,
            MACAddressRegister0Replace  = 0b010,
            MACAddressRegister0Reserved = 0b011,
            MACAddressRegister1None     = 0b100,
            MACAddressRegister1Insert   = 0b101,
            MACAddressRegister1Replace  = 0b110,
            MACAddressRegister1Reserved = 0b111,
        }

        private enum CRCPadOperation : byte
        {
            InsetCRCAndPad = 0b00,
            InsertCRC      = 0b01,
            None           = 0b10,
            ReplaceCRC     = 0b11,
        }
    }
}