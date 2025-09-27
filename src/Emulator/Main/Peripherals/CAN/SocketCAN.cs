//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.CAN
{
    public static class ISocketCANFrameExtentions
    {
        public static byte[] Encode<T>(this T @this, bool useNetworkByteOrder = false) where T : ISocketCANFrame
        {
            var frame = Packet.Encode<T>(@this);
            if(useNetworkByteOrder)
            {
                // Only the ID and flags field is byte swapped
                // For historical reasons this is also done for CAN XL
                // see: https://github.com/wireshark/wireshark/blob/master/epan/dissectors/packet-socketcan.c
                Misc.EndiannessSwapInPlace(frame, width: 4, offset: 0, length: 4);
            }
            return frame;
        }

        public static bool TryDecode<T>(this IList<byte> buffer, out T frame)
            where T : ISocketCANFrame
        {
            return Packet.TryDecode<T>(buffer, out frame);
        }

        public static bool TryDecodeAsSocketCANFrame(this IList<byte> buffer, out ISocketCANFrame frame)
        {
            if(!Packet.TryDecode<SocketCANFrameHeader>(buffer, out var header))
            {
                frame = default(ISocketCANFrame);
                return false;
            }

            if(header.ExtendedFrameLengthFrame)
            {
                return buffer.TryDecode<XLSocketCANFrame>(out frame);
            }

            if(header.FlexibleDataRateFrame)
            {
                return buffer.TryDecode<FlexibleSocketCANFrame>(out frame);
            }

            return buffer.TryDecode<ClassicalSocketCANFrame>(out frame);
        }

        private static bool TryDecode<T>(this IList<byte> buffer, out ISocketCANFrame frame)
            where T : ISocketCANFrame
        {
            if(buffer.TryDecode<T>(out T tFrame))
            {
                frame = tFrame;
                return true;
            }
            frame = default(ISocketCANFrame);
            return false;
        }

        [LeastSignificantByteFirst]
        private struct SocketCANFrameHeader
        {
#pragma warning disable 649
            [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  2), Width(1)]
            public bool FlexibleDataRateFrame;
            [PacketField, Offset(doubleWords: 1, bytes: 0, bits: 7), Width(1)]
            public bool ExtendedFrameLengthFrame;
#pragma warning restore 649

            public const int Size = 8;
        }
    }

    public interface ISocketCANFrame
    {
        int Size { get; }
    }

    [LeastSignificantByteFirst]
    public struct ClassicalSocketCANFrame : ISocketCANFrame
    {
        public static ClassicalSocketCANFrame FromCANMessageFrame(CANMessageFrame msg)
        {
            return new ClassicalSocketCANFrame
            {
                Id = msg.Id,
                ErrorMessageFrame = false,
                RemoteTransmissionRequest = msg.RemoteFrame,
                ExtendedFrameFormat = msg.ExtendedFormat,
                Length = msg.Data.Length,
                Data = msg.Data.CopyAndResize(MaxDataLength)
            };
        }

        public override string ToString() => $@"ClassicalSocketCANFrame {{
    id: 0x{Id:X},
    errorMessageFrame: {ErrorMessageFrame},
    remoteTransmissionRequest: {RemoteTransmissionRequest},
    extendedFrameFormat: {ExtendedFrameFormat},
    length: {Length},
    data: {Misc.PrettyPrintCollectionHex(Data)}
}}";

        int ISocketCANFrame.Size => ClassicalSocketCANFrame.Size;

#pragma warning disable 649
        // can_id
        [PacketField, Offset(doubleWords: 0, bits:  0), Width(29)]
        public uint Id;
        [PacketField, Offset(doubleWords: 0, bits:  29), Width(1)]
        public bool ErrorMessageFrame;
        [PacketField, Offset(doubleWords: 0, bits:  30), Width(1)]
        public bool RemoteTransmissionRequest;
        [PacketField, Offset(doubleWords: 0, bits:  31), Width(1)]
        public bool ExtendedFrameFormat;

        // len
        [PacketField, Offset(doubleWords: 1, bits:  0), Width(8)]
        public int Length;

        // data
        [PacketField, Offset(quadWords: 1), Width(bytes: MaxDataLength)]
        public byte[] Data;
#pragma warning restore 649

        public const int MaxDataLength = 8;
        public const int Size = MaxDataLength + 8;
    }

    [LeastSignificantByteFirst]
    public struct FlexibleSocketCANFrame : ISocketCANFrame
    {
        public static FlexibleSocketCANFrame FromCANMessageFrame(CANMessageFrame msg)
        {
            return new FlexibleSocketCANFrame
            {
                Id = msg.Id,
                ErrorMessageFrame = false,
                RemoteTransmissionRequest = msg.RemoteFrame,
                ExtendedFrameFormat = msg.ExtendedFormat,
                Length = msg.Data.Length,
                BitRateSwitch = msg.BitRateSwitch,
                ErrorStateIndicator = false,
                FlexibleDataRateFrame = true,
                Data = msg.Data.CopyAndResize(MaxDataLength)
            };
        }

        public override string ToString() => $@"FlexibleSocketCANFrame {{
    id: 0x{Id:X},
    errorMessageFrame: {ErrorMessageFrame},
    remoteTransmissionRequest: {RemoteTransmissionRequest},
    extendedFrameFormat: {ExtendedFrameFormat},
    length: {Length},
    bitRateSwitch: {BitRateSwitch},
    errorStateIndicator: {ErrorStateIndicator},
    flexibleDataRateFrame: {FlexibleDataRateFrame},
    data: {Misc.PrettyPrintCollectionHex(Data)}
}}";

        int ISocketCANFrame.Size => FlexibleSocketCANFrame.Size;

#pragma warning disable 649
        // can_id
        [PacketField, Offset(doubleWords: 0, bits:  0), Width(29)]
        public uint Id;
        [PacketField, Offset(doubleWords: 0, bits:  29), Width(1)]
        public bool ErrorMessageFrame;
        [PacketField, Offset(doubleWords: 0, bits:  30), Width(1)]
        public bool RemoteTransmissionRequest;
        [PacketField, Offset(doubleWords: 0, bits:  31), Width(1)]
        public bool ExtendedFrameFormat;

        // len
        [PacketField, Offset(doubleWords: 1, bytes: 0), Width(8)]
        public int Length;

        // flags
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  0), Width(1)]
        public bool BitRateSwitch;
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  1), Width(1)]
        public bool ErrorStateIndicator;
        // should always be set for FD CAN frame
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  2), Width(1)]
        public bool FlexibleDataRateFrame;

        // data
        [PacketField, Offset(quadWords: 1), Width(bytes: MaxDataLength)]
        public byte[] Data;
#pragma warning restore 649

        public const int MaxDataLength = 64;
        public const int Size = MaxDataLength + 8;
    }

    [LeastSignificantByteFirst]
    public struct XLSocketCANFrame : ISocketCANFrame
    {
        public override string ToString() => $@"XLSocketCANFrame {{
    priority: 0x{Priority:X},
    virtualCANNetworkId: 0x{VirtualCANNetworkId:X},
    simpleExtendedContent: {SimpleExtendedContent},
    extendedFrameLengthFrame: {ExtendedFrameLengthFrame},
    serviceDataUnit: 0x{ServiceDataUnit:X},
    length: {Length},
    acceptanceField: 0x{AcceptanceField:X},
    data: {Misc.PrettyPrintCollectionHex(Data)}
}}";

        int ISocketCANFrame.Size => XLSocketCANFrame.Size;

#pragma warning disable 649
        // prio
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(11)]
        public uint Priority;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte VirtualCANNetworkId;

        // flags
        [PacketField, Offset(doubleWords: 1, bytes: 0, bits:  0), Width(1)]
        public bool SimpleExtendedContent;
        [PacketField, Offset(doubleWords: 1, bytes: 0, bits:  7), Width(1)]
        public bool ExtendedFrameLengthFrame;

        // sdt
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits: 0), Width(8)]
        public byte ServiceDataUnit;

        // len
        [PacketField, Offset(doubleWords: 1, words: 1), Width(16)]
        public int Length;

        // af
        [PacketField, Offset(doubleWords: 2), Width(32)]
        public uint AcceptanceField;

        // data
        [PacketField, Offset(doubleWords: 3), Width(bytes: MaxDataLength)]
        public byte[] Data;
#pragma warning restore 649

        public const int MaxDataLength = 2048;
        public const int Size = MaxDataLength + 12;
    }
}