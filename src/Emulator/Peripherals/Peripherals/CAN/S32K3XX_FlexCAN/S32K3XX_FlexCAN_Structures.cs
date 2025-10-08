//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.CAN
{
    public partial class S32K3XX_FlexCAN
    {
        private static uint PacketLengthToDataLengthCode(uint length)
        {
            if(length <= 8)
            {
                return (byte)length;
            }

            switch(length)
            {
            case 12:
                return 9;
            case 15:
                return 10;
            case 20:
                return 11;
            case 24:
                return 12;
            case 32:
                return 13;
            case 48:
                return 14;
            case 64:
                return 15;
            default:
                Logger.Log(LogLevel.Error, "{0} is invalid data length for CAN message", length);
                return 0;
            }
        }

        private static uint DataLengthCodeToPacketLength(uint dataLengthCode)
        {
            switch(dataLengthCode)
            {
            case 9:
                return 12;
            case 10:
                return 15;
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
                return dataLengthCode;
            }
        }

        private struct MessageBufferIteratorEntry
        {
            public MessageBufferIteratorEntry(ulong offset, int region, MessageBufferStructure buffer)
            {
                Offset = offset;
                Region = region;
                MessageBuffer = buffer;
            }

            public override string ToString()
            {
                return $@"{nameof(MessageBufferIteratorEntry)} {{
    {nameof(Offset)}: 0x{Offset:X}
    {nameof(Region)}: 0x{Region}
    {nameof(MessageBuffer)}: {MessageBuffer}
}}";
            }

            public ulong Offset { get; }

            public int Region { get; }

            public MessageBufferStructure MessageBuffer { get; }
        }

        private struct MessageBufferMatcher
        {
            public MessageBufferMatcher(ulong mask)
            {
                RawMask = mask;
            }

            public bool IsMatching(CANMessageFrame frame, MessageBufferStructure messageBuffer)
            {
                if(MatchRTR && frame.RemoteFrame != messageBuffer.RemoteTransmissionRequest)
                {
                    return false;
                }

                if(MatchIDE && frame.ExtendedFormat != messageBuffer.IdExtendedBit)
                {
                    return false;
                }

                return (frame.Id & Mask) == (messageBuffer.Id & Mask);
            }

            public ulong RawMask { get; }

            public ulong Mask => RawMask & AddressMask;

            public bool MatchRTR => (RawMask & MatchRTRMask) > 0;

            public bool MatchIDE => (RawMask & MatchIDEMask) > 0;

            private const ulong AddressMask = 0x3FFFFFFF;
            private const ulong MatchRTRMask = 0x80000000;
            private const ulong MatchIDEMask = 0x40000000;
        }

        [LeastSignificantByteFirst]
        private struct MessageBufferStructure
        {
            public static MessageBufferStructure FetchMetadata(IMultibyteWritePeripheral buffer, ulong offset)
            {
                var data = buffer.ReadBytes((long)offset, (int)MetaSize);
                var structure = Packet.Decode<MessageBufferStructure>(data);
                return structure;
            }

            public override string ToString()
            {
                return PrettyString;
            }

            public CANMessageFrame ToCANMessageFrame()
            {
                return new CANMessageFrame(StandardId, ExtensionId, Data.ToArray(), IdExtendedBit, RemoteTransmissionRequest, ExtendedDataLength, BitRateSwitch);
            }

            public void FillReceivedFrame(IMultibyteWritePeripheral buffer, ulong offset, CANMessageFrame frame)
            {
                Data = frame.Data;
                MessageBufferCode = RxMessageCode != RxCode.Empty ? (byte)RxMessageBufferCode.Overrun : (byte)RxMessageBufferCode.Full;

                var dataToBeWritten = Packet.Encode<MessageBufferStructure>(this);
                buffer.WriteBytes((long)offset, dataToBeWritten, 0, (int)MetaSize);
                buffer.WriteBytes((long)offset + MetaSize, data, 0, (int)data.Length);
            }

            public void FetchData(IMultibyteWritePeripheral buffer, ulong offset)
            {
                // NOTE: Data is stored in double words, therefore we have to make sure
                // we always use data length that is multiple of 4.
                data = buffer.ReadBytes((long)offset + MetaSize, (int)(PacketDataLength + 3) & ~3);
            }

            public void Finalize(IMultibyteWritePeripheral buffer, ulong offset)
            {
                switch(TxMessageCode)
                {
                case TxCode.Data:
                    MessageBufferCode = (byte)TxMessageBufferCode.Inactive;
                    break;
                case TxCode.Remote:
                    MessageBufferCode = (byte)RxMessageBufferCode.Empty;
                    break;
                case TxCode.TAnswer:
                    MessageBufferCode = (byte)RxMessageBufferCode.RAnswer;
                    break;
                default:
                    throw new Exception("Unreachable");
                }
                var data = Packet.Encode<MessageBufferStructure>(this);
                buffer.WriteBytes((long)offset, data, 0, (int)MetaSize);
            }

            public string PrettyString => $@"{nameof(MessageBufferStructure)} {{
    {nameof(Timestamp)}: 0x{Timestamp:X},
    {nameof(DataLength)}: {DataLength},
    {nameof(RemoteTransmissionRequest)}: {RemoteTransmissionRequest},
    {nameof(IdExtendedBit)}: {IdExtendedBit},
    {nameof(SubstituteRemoteRequest)}: {SubstituteRemoteRequest},
    {nameof(MessageBufferCode)}: {TxMessageCode?.ToString() ?? RxMessageCode.ToString()} (0x{MessageBufferCode:X}),
    {nameof(ErrorStateIndicator)}: {ErrorStateIndicator},
    {nameof(BitRateSwitch)}: {BitRateSwitch},
    {nameof(ExtendedDataLength)}: {ExtendedDataLength},
    {nameof(ExtensionId)}: {ExtensionId},
    {nameof(StandardId)}: {StandardId},
    {nameof(LocalPriority)}: {LocalPriority},
    {nameof(data)}: {DataString}
}}";

            public string DataString => data != null ? Misc.PrettyPrintCollectionHex(data) : "<data not fetched>";

            public RxCode? RxMessageCode
            {
                get
                {
                    if((MessageBufferCode & 0b1) != 0)
                    {
                        return RxCode.Busy;
                    }
                    switch((RxMessageBufferCode)MessageBufferCode)
                    {
                    case RxMessageBufferCode.Inactive:
                        return RxCode.Inactive;
                    case RxMessageBufferCode.Empty:
                        return RxCode.Empty;
                    case RxMessageBufferCode.Full:
                        return RxCode.Full;
                    case RxMessageBufferCode.Overrun:
                        return RxCode.Overrun;
                    case RxMessageBufferCode.RAnswer:
                        return RxCode.RAnswer;
                    default:
                        return null;
                    }
                }
            }

            public TxCode? TxMessageCode
            {
                get
                {
                    switch((TxMessageBufferCode)MessageBufferCode)
                    {
                    case TxMessageBufferCode.Inactive:
                        return TxCode.Inactive;
                    case TxMessageBufferCode.Abort:
                        return TxCode.Abort;
                    case TxMessageBufferCode.Data: // or TxMessageBufferCode.Remote
                        return RemoteTransmissionRequest ? TxCode.Remote : TxCode.Data;
                    case TxMessageBufferCode.TAnswer:
                        return TxCode.TAnswer;
                    default:
                        return null;
                    }
                }
            }

            // MB is ready for TX when code is set to Data or Remate (same code value)
            public bool ReadyForTransmission => (TxMessageBufferCode)MessageBufferCode == TxMessageBufferCode.Data;

            public bool ReadyForReception =>
                (RxMessageBufferCode)MessageBufferCode == RxMessageBufferCode.Empty ||
                (RxMessageBufferCode)MessageBufferCode == RxMessageBufferCode.Full ||
                (RxMessageBufferCode)MessageBufferCode == RxMessageBufferCode.Overrun;

            public uint ExtendedId => StandardId << StandardIdOffset | ExtensionId;

            public uint Id => IdExtendedBit ? ExtendedId : StandardId;

            public uint PacketDataLength
            {
                get => DataLengthCodeToPacketLength(DataLength);
                set => DataLength = (byte)PacketLengthToDataLengthCode(value);
            }

            public uint Size => MetaSize + PacketDataLength;

            public uint Priority => LocalPriority;

            public IEnumerable<byte> Data
            {
                // NOTE: Data is send as big-endian double words, so we have to do conversion
                // before interpreting it as array of bytes
                get => data.Chunk(4).SelectMany(chunk => chunk.Reverse()).Take((int)PacketDataLength);
                set
                {
                    var length = value.Count();
                    // NOTE: Data is stored in double words, therefore we have to make sure
                    // we always use data length that is multiple of 4.
                    var properLength = (length + 3) & ~3;
                    data = Enumerable.Concat(value, Enumerable.Repeat((byte)0, properLength - length))
                        .Chunk(4)
                        .SelectMany(chunk => chunk.Reverse())
                        .ToArray();
                    PacketDataLength = (uint)length;
                }
            }

#pragma warning disable 649
            [PacketField, Offset(doubleWords: 0, bits:  0), Width(16)] // TIMESTAMP
            public ushort Timestamp; // Free-Running Counter Timestamp
            [PacketField, Offset(doubleWords: 0, bits: 16), Width( 4)] // DLC
            public byte DataLength; // Length of Data in Bytes
            [PacketField, Offset(doubleWords: 0, bits: 20), Width( 1)] // RTR
            public bool RemoteTransmissionRequest;
            [PacketField, Offset(doubleWords: 0, bits: 21), Width( 1)] // IDE
            public bool IdExtendedBit;
            [PacketField, Offset(doubleWords: 0, bits: 22), Width( 1)] // SRR
            public bool SubstituteRemoteRequest;
            // bit 23 of 1st double word is reserved
            [PacketField, Offset(doubleWords: 0, bits: 24), Width( 4)] // CODE
            public byte MessageBufferCode;
            // bit 28 of 1st double word is reserved
            [PacketField, Offset(doubleWords: 0, bits: 29), Width( 1)] // ESI
            public bool ErrorStateIndicator;
            [PacketField, Offset(doubleWords: 0, bits: 30), Width( 1)] // BRS
            public bool BitRateSwitch;
            [PacketField, Offset(doubleWords: 0, bits: 31), Width( 1)] // EDL
            public bool ExtendedDataLength;
            [PacketField, Offset(doubleWords: 1, bits:  0), Width(18)] // ID (extended)
            public uint ExtensionId;
            [PacketField, Offset(doubleWords: 1, bits: 18), Width(11)] // ID (standard/extended)
            public uint StandardId;
            [PacketField, Offset(doubleWords: 1, bits: 29), Width( 3)] // PRIO
            public byte LocalPriority;
#pragma warning restore 649
            private byte[] data;

            public const uint MetaSize = 0x8;
            public const byte StandardIdOffset = 18;
        }

        [LeastSignificantByteFirst]
        private struct LegacyRxFifoStructure
        {
            public static LegacyRxFifoStructure FromCANFrame(CANMessageFrame frame, int filterIndex)
            {
                var @this = new LegacyRxFifoStructure();

                @this.PacketDataLength = (uint)frame.Data.Length;
                @this.RemoteFrame = frame.RemoteFrame;
                @this.ExtendedFrame = frame.ExtendedFormat;
                @this.IdentifierAcceptanceFilterHitIndicator = (ushort)filterIndex;
                @this.ExtendedId = frame.ExtendedIdPart;
                @this.StandardId = frame.StandardIdPart;
                @this.Data = frame.Data.ToArray();
                return @this;
            }

            public void CommitToMemory(IMultibyteWritePeripheral buffer, uint offset)
            {
                var dataToBeWritten = Packet.Encode<LegacyRxFifoStructure>(this);
                buffer.WriteBytes((long)offset, dataToBeWritten, 0, (int)MetaSize);
                buffer.WriteBytes((long)offset + MetaSize, Data, 0, (int)Data.Length);
            }

            public override string ToString()
            {
                return PrettyString;
            }

            public uint PacketDataLength
            {
                get => DataLengthCodeToPacketLength(DataLength);
                set => DataLength = (byte)PacketLengthToDataLengthCode(value);
            }

            // NOTE: Data is send as big-endian double words, so we have to do conversion
            // before interpreting it as array of bytes
            public IEnumerable<byte> PacketData
            {
                get => Data.Chunk(4).SelectMany(chunk => chunk.Reverse()).Take((int)PacketDataLength);
                set
                {
                    var length = value.Count();
                    // NOTE: Data is stored in double words, therefore we have to make sure
                    // we always use data length that is multiple of 4.
                    var properLength = (length + 3) & ~3;
                    Data = Enumerable.Concat(value, Enumerable.Repeat((byte)0, properLength - length))
                        .Chunk(4)
                        .SelectMany(chunk => chunk.Reverse())
                        .ToArray();
                    PacketDataLength = (uint)length;
                }
            }

            public string PrettyString => $@"{nameof(LegacyRxFifoStructure)} {{
    {nameof(Timestamp)}: 0x{Timestamp:X},
    {nameof(DataLength)}: {DataLength},
    {nameof(RemoteFrame)}: {RemoteFrame},
    {nameof(ExtendedFrame)}: {ExtendedFrame},
    {nameof(SubstituteRemoteRequest)}: {SubstituteRemoteRequest},
    {nameof(IdentifierAcceptanceFilterHitIndicator)}: {IdentifierAcceptanceFilterHitIndicator},
    {nameof(ExtendedId)}: {ExtendedId},
    {nameof(StandardId)}: {StandardId},
    {nameof(Data)}: {Misc.PrettyPrintCollectionHex(Data)}
}}";

#pragma warning disable 649
            [PacketField, Offset(doubleWords: 0, bits:  0), Width(16)] // TIMESTAMP
            public ushort Timestamp; // Free-Running Counter Timestamp
            [PacketField, Offset(doubleWords: 0, bits: 16), Width( 4)] // DLC
            public byte DataLength; // Length of Data in Bytes
            [PacketField, Offset(doubleWords: 0, bits: 20), Width( 1)] // RTR
            public bool RemoteFrame;
            [PacketField, Offset(doubleWords: 0, bits: 21), Width( 1)] // IDE
            public bool ExtendedFrame;
            [PacketField, Offset(doubleWords: 0, bits: 22), Width( 1)] // SRR
            public bool SubstituteRemoteRequest;
            [PacketField, Offset(doubleWords: 0, bits: 23), Width( 9)] // IDHIT
            public ushort IdentifierAcceptanceFilterHitIndicator;
            [PacketField, Offset(doubleWords: 1, bits:  0), Width(18)] // ID (extended)
            public uint ExtendedId;
            [PacketField, Offset(doubleWords: 1, bits: 18), Width(11)] // ID (standard/extended)
            public uint StandardId;
            // bits 29:31 of 2nd double word are reserved
            [PacketField, Offset(doubleWords: 2), Width(bytes: 8)] // Data bytes
            public byte[] Data;
            // double words from 4th to 23th are reserved
            // There is a table of ID filters with 128 double words elements starting at 24th
#pragma warning restore 649

            public const uint MetaSize = 0x8;
        }

        [LeastSignificantByteFirst]
        private struct LegacyRxFifoFilterAStructure : ILegacyRxFifoMatcher
        {
            public static LegacyRxFifoFilterAStructure Fetch(IMultibyteWritePeripheral buffer, int offset)
            {
                var data = buffer.ReadBytes((long)offset, 4);
                var structure = Packet.Decode<LegacyRxFifoFilterAStructure>(data);
                return structure;
            }

            public bool IsMatching(CANMessageFrame frame) =>
                frame.ExtendedFormat == IdExtendedBit &&
                frame.RemoteFrame == RemoteTransmissionRequest &&
                frame.ExtendedId == (IdExtendedBit ? RxFrameIdentifier : (RxFrameIdentifier & StandardIdMask))
            ;

#pragma warning disable 649
            [PacketField, Offset(bits:  1), Width(28)]
            public uint RxFrameIdentifier;
            [PacketField, Offset(bits: 30), Width( 1)]
            public bool IdExtendedBit;
            [PacketField, Offset(bits: 31), Width( 1)]
            public bool RemoteTransmissionRequest;
#pragma warning restore 649

            private const byte StandardIdOffset = 18;
            private const byte StandardIdWidth = 11;
            private const uint StandardIdMask = ((1 << StandardIdWidth) - 1) << StandardIdOffset;
        }

        [LeastSignificantByteFirst]
        private struct LegacyRxFifoFilterBStructure : ILegacyRxFifoMatcher
        {
            public static LegacyRxFifoFilterBStructure Fetch(IMultibyteWritePeripheral buffer, int offset)
            {
                var data = buffer.ReadBytes((long)offset, 4);
                var structure = Packet.Decode<LegacyRxFifoFilterBStructure>(data);
                return structure;
            }

            public bool IsMatching(CANMessageFrame frame) =>
                IsMatchingPartial(frame, RxFrameIdentifier0, IdExtendedBit0, RemoteTransmissionRequest0) ||
                IsMatchingPartial(frame, RxFrameIdentifier1, IdExtendedBit1, RemoteTransmissionRequest1)
            ;

#pragma warning disable 649
            [PacketField, Offset(bits:  0), Width(14)]
            public uint RxFrameIdentifier1;
            [PacketField, Offset(bits: 14), Width( 1)]
            public bool IdExtendedBit1;
            [PacketField, Offset(bits: 15), Width( 1)]
            public bool RemoteTransmissionRequest1;
            [PacketField, Offset(bits: 16), Width(14)]
            public uint RxFrameIdentifier0;
            [PacketField, Offset(bits: 30), Width( 1)]
            public bool IdExtendedBit0;
            [PacketField, Offset(bits: 31), Width( 1)]
            public bool RemoteTransmissionRequest0;
#pragma warning restore 649

            private static bool IsMatchingPartial(CANMessageFrame frame, uint rxFrameIdentifier, bool idExtendedBit, bool remoteTransmissionRequest) =>
                frame.ExtendedFormat == idExtendedBit &&
                frame.RemoteFrame == remoteTransmissionRequest &&
                (frame.ExtendedId >> IgnoredIdBits) == (idExtendedBit ? rxFrameIdentifier : (rxFrameIdentifier & StandardIdMask))
            ;

            private const byte IdBits = 29;
            private const byte ComparedIdBits = 14;
            private const byte IgnoredIdBits = IdBits - ComparedIdBits;
            private const byte StandardIdWidth = 11;
            private const byte StandardIdOffset = ComparedIdBits - StandardIdWidth;
            private const uint StandardIdMask = ((1 << StandardIdWidth) - 1) << StandardIdOffset;
        }

        [LeastSignificantByteFirst]
        private struct LegacyRxFifoFilterCStructure : ILegacyRxFifoMatcher
        {
            public static LegacyRxFifoFilterCStructure Fetch(IMultibyteWritePeripheral buffer, int offset)
            {
                var data = buffer.ReadBytes((long)offset, 4);
                var structure = Packet.Decode<LegacyRxFifoFilterCStructure>(data);
                return structure;
            }

            public bool IsMatching(CANMessageFrame frame)
            {
                var partialId = frame.ExtendedId >> IgnoredIdBits;
                return partialId == RxFrameIdentifier0
                    || partialId == RxFrameIdentifier1
                    || partialId == RxFrameIdentifier2
                    || partialId == RxFrameIdentifier3
                ;
            }

#pragma warning disable 649
            [PacketField, Offset(bits:  0), Width(8)]
            public uint RxFrameIdentifier3;
            [PacketField, Offset(bits:  8), Width(8)]
            public uint RxFrameIdentifier2;
            [PacketField, Offset(bits: 16), Width(8)]
            public uint RxFrameIdentifier1;
            [PacketField, Offset(bits: 24), Width(8)]
            public uint RxFrameIdentifier0;
#pragma warning restore 649

            private const byte IdBits = 29;
            private const byte ComparedIdBits = 8;
            private const byte IgnoredIdBits = IdBits - ComparedIdBits;
        }

        private enum MessageBufferSize
        {
            _8bytes,
            _16bytes,
            _32bytes,
            _64bytes
        }

        private enum TxCode
        {
            Inactive,
            Abort,
            Data,
            Remote,
            TAnswer,
        }

        private enum RxCode
        {
            Inactive,
            Empty,
            Full,
            Overrun,
            RAnswer,
            Busy,
        }

        private enum LegacyFilterFormat
        {
            A = 0b00, // One full ID (standard and extended) per ID filter table element
            B = 0b01, // Two full standard IDs or two partial 14-bit (standard and extended) IDs per ID filter table element
            C = 0b10, // Four partial 8-bit standard IDs per ID filter table element
            D = 0b11, // All frames rejected
        }

        private interface ILegacyRxFifoMatcher
        {
            bool IsMatching(CANMessageFrame frame);
        }

        private enum RxMessageBufferCode : byte
        {
            Inactive = 0b0000,
            Empty    = 0b0100,
            Full     = 0b0010,
            Overrun  = 0b0110,
            RAnswer  = 0b1010,
            Busy     = 0b0001, // 0bxxx1
        }

        private enum TxMessageBufferCode : byte
        {
            Inactive = 0b1000,
            Abort    = 0b1001,
            Data     = 0b1100, // RTR = 0
            Remote   = 0b1100, // RTR = 1
            TAnswer  = 0b1110,
        }
    }
}