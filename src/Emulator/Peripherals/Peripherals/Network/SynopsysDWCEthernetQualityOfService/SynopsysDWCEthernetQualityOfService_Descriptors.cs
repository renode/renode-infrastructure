//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.Network
{
    public partial class SynopsysDWCEthernetQualityOfService
    {
        private abstract class Descriptor
        {
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

            protected void UpdateProperties(IDescriptorStruct structure)
            {
                IsOwnedByDMA = structure.Owner == DescriptorOwner.DMA;
                isContextDescriptor = structure.ContextType;
            }

            public Type Type => type;
            public ulong Address { get; }
            public bool? IsOwnedByDMA { get; protected set; }
            public static uint Size => 0x10;

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
                public bool ContextType => contextType;
                public DescriptorOwner Owner => owner;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool contextType;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner owner;
#pragma warning restore 649
            }
        }

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
    buffer1Address: 0x{buffer1Address:X},
    buffer2Address: 0x{buffer2Address:X},
    buffer1AddressValid: {buffer1AddressValid},
    buffer2AddressValid: {buffer2AddressValid},
    interruptOnCompletion: {interruptOnCompletion},
    owner: {owner}
}}";
                public bool ContextType => false;
                public DescriptorOwner Owner => owner;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(32)] // BUF1AP
                public uint buffer1Address;
                // 2nd double word is reserved
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(32)] // BUF2AP
                public uint buffer2Address;
                // bits 0:23 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 24), Width(1)] // BUF1V
                public bool buffer1AddressValid;
                [PacketField, Offset(doubleWords: 3, bits: 25), Width(1)] // BUF2V
                public bool buffer2AddressValid;
                // bits 26:29 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // IOC
                public bool interruptOnCompletion;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner owner;
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
    outerVlanTag: 0x{outerVlanTag:X},
    innerVlanTag: 0x{innerVlanTag:X},
    payloadType: {payloadType},
    ipHeaderError: {ipHeaderError},
    ipv4HeaderPresent: {ipv4HeaderPresent},
    ipv6HeaderPresent: {ipv6HeaderPresent},
    ipChecksumBypassed: {ipChecksumBypassed},
    ipPayloadError: {ipPayloadError},
    ptpMessageType: {ptpMessageType},
    ptpPacketType: {ptpPacketType},
    ptpVersion: {ptpVersion},
    timestampAvailable: {timestampAvailable},
    timestampDropped: {timestampDropped},
    oamSubtypeCodeOrMACControlPacketOpcode: 0x{oamSubtypeCodeOrMACControlPacketOpcode:X} or {(EtherType)oamSubtypeCodeOrMACControlPacketOpcode},
    arpReplyNotGenerated: {arpReplyNotGenerated},
    vlanFiletrStatus: {vlanFiletrStatus},
    sourceAddressFilterFail: {sourceAddressFilterFail},
    destinaltionAddressFilterFail: {destinaltionAddressFilterFail},
    hashFilterStatus: {hashFilterStatus},
    macAddressMatchOrHashValue: {macAddressMatchOrHashValue},
    layer3FilterMatch: {layer3FilterMatch},
    layer4FilterMatch: {layer4FilterMatch},
    matchedFilterNumber: {matchedFilterNumber},
    packetLength: {packetLength},
    errorSummary: {errorSummary},
    lengthTypeField: {lengthTypeField},
    dribbleBitError: {dribbleBitError},
    receiveError: {receiveError},
    overflowError: {overflowError},
    receiveWatchdogTimeout: {receiveWatchdogTimeout},
    giantPacket: {giantPacket},
    crcError: {crcError},
    receiveStatusSegment0Valid: {receiveStatusSegment0Valid},
    receiveStatusSegment1Valid: {receiveStatusSegment1Valid},
    receiveStatusSegment2Valid: {receiveStatusSegment2Valid},
    lastDescriptor: {lastDescriptor},
    firstDescriptor: {firstDescriptor},
    contextType: {contextType},
    owner: {owner}
}}";
                public bool ContextType => contextType;
                public DescriptorOwner Owner => owner;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(16)] // OVT
                public uint outerVlanTag;
                [PacketField, Offset(doubleWords: 0, bits: 16), Width(16)] // IVT
                public uint innerVlanTag;
                [PacketField, Offset(doubleWords: 1, bits: 0), Width(3)] // PT
                public PayloadType payloadType;
                [PacketField, Offset(doubleWords: 1, bits: 3), Width(1)] // IPHE
                public bool ipHeaderError;
                [PacketField, Offset(doubleWords: 1, bits: 4), Width(1)] // IPV4
                public bool ipv4HeaderPresent;
                [PacketField, Offset(doubleWords: 1, bits: 5), Width(1)] // IPV6
                public bool ipv6HeaderPresent;
                [PacketField, Offset(doubleWords: 1, bits: 6), Width(1)] // IPCB
                public bool ipChecksumBypassed;
                [PacketField, Offset(doubleWords: 1, bits: 7), Width(1)] // IPCE
                public bool ipPayloadError;
                [PacketField, Offset(doubleWords: 1, bits: 8), Width(4)] // PMT
                public PTPMessageType ptpMessageType;
                [PacketField, Offset(doubleWords: 1, bits: 12), Width(1)] // PFT
                public bool ptpPacketType;
                [PacketField, Offset(doubleWords: 1, bits: 13), Width(1)] // PV
                public PTPVersion ptpVersion;
                [PacketField, Offset(doubleWords: 1, bits: 14), Width(1)] // TSA
                public bool timestampAvailable;
                [PacketField, Offset(doubleWords: 1, bits: 15), Width(1)] // TD
                public bool timestampDropped;
                [PacketField, Offset(doubleWords: 1, bits: 16), Width(16)] // OPC
                public uint oamSubtypeCodeOrMACControlPacketOpcode;
                // bits 0:9 of 3rd double word are reserved
                [PacketField, Offset(doubleWords: 2, bits: 10), Width(1)] // ARPNR
                public bool arpReplyNotGenerated;
                // bits 11:14 of 3rd double word are reserved
                [PacketField, Offset(doubleWords: 2, bits: 15), Width(1)] // VF
                public bool vlanFiletrStatus;
                [PacketField, Offset(doubleWords: 2, bits: 16), Width(1)] // SAF
                public bool sourceAddressFilterFail;
                [PacketField, Offset(doubleWords: 2, bits: 17), Width(1)] // DAF
                public bool destinaltionAddressFilterFail;
                [PacketField, Offset(doubleWords: 2, bits: 18), Width(1)] // HF
                public bool hashFilterStatus;
                [PacketField, Offset(doubleWords: 2, bits: 19), Width(8)] // MADRM
                public bool macAddressMatchOrHashValue;
                [PacketField, Offset(doubleWords: 2, bits: 27), Width(1)] // L3FM
                public bool layer3FilterMatch;
                [PacketField, Offset(doubleWords: 2, bits: 28), Width(1)] // L4FM
                public bool layer4FilterMatch;
                [PacketField, Offset(doubleWords: 2, bits: 29), Width(3)] // L3L4FM
                public uint matchedFilterNumber;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(15)] // PL
                public uint packetLength;
                [PacketField, Offset(doubleWords: 3, bits: 15), Width(1)] // ES
                public bool errorSummary;
                [PacketField, Offset(doubleWords: 3, bits: 16), Width(3)] // LT
                public PacketKind lengthTypeField;
                [PacketField, Offset(doubleWords: 3, bits: 19), Width(1)] // DE
                public bool dribbleBitError;
                [PacketField, Offset(doubleWords: 3, bits: 20), Width(1)] // RE
                public bool receiveError;
                [PacketField, Offset(doubleWords: 3, bits: 21), Width(1)] // OE
                public bool overflowError;
                [PacketField, Offset(doubleWords: 3, bits: 22), Width(1)] // RWT
                public bool receiveWatchdogTimeout;
                [PacketField, Offset(doubleWords: 3, bits: 23), Width(1)] // GP
                public bool giantPacket;
                [PacketField, Offset(doubleWords: 3, bits: 24), Width(1)] // CE
                public bool crcError;
                [PacketField, Offset(doubleWords: 3, bits: 25), Width(1)] // RS0V
                public bool receiveStatusSegment0Valid;
                [PacketField, Offset(doubleWords: 3, bits: 26), Width(1)] // RS1V
                public bool receiveStatusSegment1Valid;
                [PacketField, Offset(doubleWords: 3, bits: 27), Width(1)] // RS2V
                public bool receiveStatusSegment2Valid;
                [PacketField, Offset(doubleWords: 3, bits: 28), Width(1)] // LD
                public bool lastDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 29), Width(1)] // FD
                public bool firstDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool contextType;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner owner;
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
    receivePacketTimestamp: 0x{receivePacketTimestamp:X},
    contextType: {contextType},
    owner: {owner}
}}";
                public bool ContextType => contextType;
                public DescriptorOwner Owner => owner;

#pragma warning disable 649
                [PacketField, Offset(quadWords: 0), Width(64)] // RTS
                public ulong receivePacketTimestamp;
                // 3nd double word and bits 0:29 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool contextType;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner owner;
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
                    var data = new byte[headerOrBuffer1Length];
                    bus.ReadBytes((ulong)buffer1OrHeaderAddress, (int)headerOrBuffer1Length, data, 0, true, cpuContext);
                    return data;
                }

                public byte[] FetchBuffer2OrBuffer1(IBusController bus, ICPU cpuContext = null)
                {
                    var data = new byte[buffer2Length];
                    bus.ReadBytes((ulong)buffer2orBuffer1Address, (int)buffer2Length, data, 0, true, cpuContext);
                    return data;
                }

                public uint PayloadLength
                {
                    get
                    {
                        return payloadLengthLower15bits | payloadLength15bit << 15 | (uint)checksumControl << 16;
                    }
                }

                public string PrettyString => $@"NormalReadTxDescriptor {{
    buffer1OrHeaderAddress: 0x{buffer1OrHeaderAddress:X},
    buffer2orBuffer1Address: 0x{buffer2orBuffer1Address:X},
    headerOrBuffer1Length: 0x{headerOrBuffer1Length:X},
    vlanTagControl: {vlanTagControl},
    buffer2Length: 0x{buffer2Length:X},
    transmitTimestampEnable: {transmitTimestampEnable},
    interruptOnCompletion: {interruptOnCompletion},
    payloadLengthLower15bits: {payloadLengthLower15bits},
    payloadLength15bit: {payloadLength15bit},
    checksumControl: {checksumControl},
    tcpSegmentationEnable: {tcpSegmentationEnable},
    headerLength: {headerLength},
    sourceAddressControl: {sourceAddressControl},
    crcPadControl: {crcPadControl},
    lastDescriptor: {lastDescriptor},
    firstDescriptor: {firstDescriptor},
    contextType: {contextType},
    owner: {owner}
}}";
                public bool ContextType => contextType;
                public DescriptorOwner Owner => owner;

#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(32)] // BUF1AP
                public uint buffer1OrHeaderAddress;
                [PacketField, Offset(doubleWords: 1, bits: 0), Width(32)] // BUF2AP
                public uint buffer2orBuffer1Address;
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(14)] // HR/B1L
                public uint headerOrBuffer1Length;
                [PacketField, Offset(doubleWords: 2, bits: 14), Width(2)] // VTIR
                public VLANTagOperation vlanTagControl;
                [PacketField, Offset(doubleWords: 2, bits: 16), Width(14)] // B2L
                public uint buffer2Length;
                [PacketField, Offset(doubleWords: 2, bits: 30), Width(1)] // TTSE
                public bool transmitTimestampEnable;
                [PacketField, Offset(doubleWords: 2, bits: 31), Width(1)] // IOC
                public bool interruptOnCompletion;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(15)] // FL/TPL
                public uint payloadLengthLower15bits;
                [PacketField, Offset(doubleWords: 3, bits: 15), Width(1)] // TPL
                public uint payloadLength15bit;
                [PacketField, Offset(doubleWords: 3, bits: 16), Width(2)] // CIC/TPL
                public ChecksumOperation checksumControl;
                [PacketField, Offset(doubleWords: 3, bits: 18), Width(1)] // TSE
                public bool tcpSegmentationEnable;
                [PacketField, Offset(doubleWords: 3, bits: 19), Width(4)] // THL
                public uint headerLength;
                [PacketField, Offset(doubleWords: 3, bits: 23), Width(3)] // SAIC
                public DescriptorSourceAddressOperation sourceAddressControl;
                [PacketField, Offset(doubleWords: 3, bits: 26), Width(2)] // CPC
                public CRCPadOperation crcPadControl;
                [PacketField, Offset(doubleWords: 3, bits: 28), Width(1)] // LD
                public bool lastDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 29), Width(1)] // FD
                public bool firstDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool contextType;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner owner;
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
    txPacketTimestamp: 0x{txPacketTimestamp:X},
    ipHeaderError: {ipHeaderError},
    deferredBit: {deferredBit},
    underflowError: {underflowError},
    excessiveDeferral: {excessiveDeferral},
    collisionCount: {collisionCount},
    excessiveCollision: {excessiveCollision},
    lateCollision: {lateCollision},
    noCarrier: {noCarrier},
    lossOfCarrier: {lossOfCarrier},
    payloadChecksumError: {payloadChecksumError},
    packetFlushed: {packetFlushed},
    jabberTimeout: {jabberTimeout},
    errorSummary: {errorSummary},
    txTimestampCaptured: {txTimestampCaptured},
    lastDescriptor: {lastDescriptor},
    firstDescriptor: {firstDescriptor},
    contextType: {contextType},
    owner: {owner}
}}";
                public bool ContextType => contextType;
                public DescriptorOwner Owner => owner;

#pragma warning disable 649
                [PacketField, Offset(quadWords: 0), Width(64)] // TTS
                public ulong txPacketTimestamp;
                // 3nd double word is reserved
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(1)] // IHE
                public bool ipHeaderError;
                [PacketField, Offset(doubleWords: 3, bits: 1), Width(1)] // DB
                public bool deferredBit;
                [PacketField, Offset(doubleWords: 3, bits: 2), Width(1)] // UF
                public bool underflowError;
                [PacketField, Offset(doubleWords: 3, bits: 3), Width(1)] // ED
                public bool excessiveDeferral;
                [PacketField, Offset(doubleWords: 3, bits: 4), Width(4)] // CC
                public bool collisionCount;
                [PacketField, Offset(doubleWords: 3, bits: 8), Width(1)] // EC
                public bool excessiveCollision;
                [PacketField, Offset(doubleWords: 3, bits: 9), Width(1)] // LC
                public bool lateCollision;
                [PacketField, Offset(doubleWords: 3, bits: 10), Width(1)] // NC
                public bool noCarrier;
                [PacketField, Offset(doubleWords: 3, bits: 11), Width(1)] // LoC
                public bool lossOfCarrier;
                [PacketField, Offset(doubleWords: 3, bits: 12), Width(1)] // PCE
                public bool payloadChecksumError;
                [PacketField, Offset(doubleWords: 3, bits: 13), Width(1)] // FF
                public bool packetFlushed;
                [PacketField, Offset(doubleWords: 3, bits: 14), Width(1)] // JT
                public bool jabberTimeout;
                [PacketField, Offset(doubleWords: 3, bits: 15), Width(1)] // ES
                public bool errorSummary;
                // bit 16 of 4th double word is reserved
                [PacketField, Offset(doubleWords: 3, bits: 17), Width(1)] // TTSS
                public bool txTimestampCaptured;
                // bits 18:27 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 28), Width(1)] // LD
                public bool lastDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 29), Width(1)] // FD
                public bool firstDescriptor;
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool contextType;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner owner;
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
    txPacketTimestamp: {txPacketTimestamp},
    maximumSegmentSize: {maximumSegmentSize},
    innerVlanTag: {innerVlanTag},
    vlanTag: {vlanTag},
    vlanTagValid: {vlanTagValid},
    innerVlanTagValid: {innerVlanTagValid},
    innerVlanTagControl: {innerVlanTagControl},
    contextDescriptorError: {contextDescriptorError},
    oneStepTimestampCorrectionInputOrMaximumSegmentSizeValid: {oneStepTimestampCorrectionInputOrMaximumSegmentSizeValid},
    oneStepTimestampCorrectionEnable: {oneStepTimestampCorrectionEnable},
    contextType: {contextType},
    owner: {owner}
}}";
                public bool ContextType => contextType;
                public DescriptorOwner Owner => owner;

#pragma warning disable 649
                [PacketField, Offset(quadWords: 0), Width(64)] // TTS
                public ulong txPacketTimestamp;
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(14)] // MSS
                public uint maximumSegmentSize;
                // bits 14:15 of 3rd double word are reserved
                [PacketField, Offset(doubleWords: 2, bits: 16), Width(16)] // IVT
                public uint innerVlanTag;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(16)] // VT
                public uint vlanTag;
                [PacketField, Offset(doubleWords: 3, bits: 16), Width(1)] // VLTV
                public bool vlanTagValid;
                [PacketField, Offset(doubleWords: 3, bits: 17), Width(1)] // IVLTV
                public bool innerVlanTagValid;
                [PacketField, Offset(doubleWords: 3, bits: 18), Width(2)] // IVTIR
                public VLANTagOperation innerVlanTagControl;
                // bits 20:22 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 23), Width(1)] // CDE
                public bool contextDescriptorError;
                // bits 24:25 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 26), Width(1)] // TCMSSV
                public bool oneStepTimestampCorrectionInputOrMaximumSegmentSizeValid;
                [PacketField, Offset(doubleWords: 3, bits: 27), Width(1)] // OSTC
                public bool oneStepTimestampCorrectionEnable;
                // bits 28:29 of 4th double word are reserved
                [PacketField, Offset(doubleWords: 3, bits: 30), Width(1)] // CTXT
                public bool contextType;
                [PacketField, Offset(doubleWords: 3, bits: 31), Width(1)] // OWN
                public DescriptorOwner owner;
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
