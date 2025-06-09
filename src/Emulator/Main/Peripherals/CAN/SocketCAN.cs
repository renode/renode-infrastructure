//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
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

            if(header.extendedFrameLengthFrame)
            {
                return buffer.TryDecode<XLSocketCANFrame>(out frame);
            }

            if(header.flexibleDataRateFrame)
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
            public bool flexibleDataRateFrame;
            [PacketField, Offset(doubleWords: 1, bytes: 0, bits: 7), Width(1)]
            public bool extendedFrameLengthFrame;
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
                id = msg.Id,
                errorMessageFrame = false,
                remoteTransmissionRequest = msg.RemoteFrame,
                extendedFrameFormat = msg.ExtendedFormat,
                length = msg.Data.Length,
                data = msg.Data.CopyAndResize(MaxDataLength)
            };
        }

        public override string ToString() => $@"ClassicalSocketCANFrame {{
    id: 0x{id:X},
    errorMessageFrame: {errorMessageFrame},
    remoteTransmissionRequest: {remoteTransmissionRequest},
    extendedFrameFormat: {extendedFrameFormat},
    length: {length},
    data: {Misc.PrettyPrintCollectionHex(data)}
}}";

        int ISocketCANFrame.Size => ClassicalSocketCANFrame.Size;

#pragma warning disable 649
        // can_id
        [PacketField, Offset(doubleWords: 0, bits:  0), Width(29)]
        public uint id;
        [PacketField, Offset(doubleWords: 0, bits:  29), Width(1)]
        public bool errorMessageFrame;
        [PacketField, Offset(doubleWords: 0, bits:  30), Width(1)]
        public bool remoteTransmissionRequest;
        [PacketField, Offset(doubleWords: 0, bits:  31), Width(1)]
        public bool extendedFrameFormat;

        // len
        [PacketField, Offset(doubleWords: 1, bits:  0), Width(8)]
        public int length;

        // data
        [PacketField, Offset(quadWords: 1), Width(MaxDataLength)]
        public byte[] data;
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
                id = msg.Id,
                errorMessageFrame = false,
                remoteTransmissionRequest = msg.RemoteFrame,
                extendedFrameFormat = msg.ExtendedFormat,
                length = msg.Data.Length,
                bitRateSwitch = msg.BitRateSwitch,
                errorStateIndicator = false,
                flexibleDataRateFrame = true,
                data = msg.Data.CopyAndResize(MaxDataLength)
            };
        }

        public override string ToString() => $@"FlexibleSocketCANFrame {{
    id: 0x{id:X},
    errorMessageFrame: {errorMessageFrame},
    remoteTransmissionRequest: {remoteTransmissionRequest},
    extendedFrameFormat: {extendedFrameFormat},
    length: {length},
    bitRateSwitch: {bitRateSwitch},
    errorStateIndicator: {errorStateIndicator},
    flexibleDataRateFrame: {flexibleDataRateFrame},
    data: {Misc.PrettyPrintCollectionHex(data)}
}}";

        int ISocketCANFrame.Size => FlexibleSocketCANFrame.Size;

#pragma warning disable 649
        // can_id
        [PacketField, Offset(doubleWords: 0, bits:  0), Width(29)]
        public uint id;
        [PacketField, Offset(doubleWords: 0, bits:  29), Width(1)]
        public bool errorMessageFrame;
        [PacketField, Offset(doubleWords: 0, bits:  30), Width(1)]
        public bool remoteTransmissionRequest;
        [PacketField, Offset(doubleWords: 0, bits:  31), Width(1)]
        public bool extendedFrameFormat;

        // len
        [PacketField, Offset(doubleWords: 1, bytes: 0), Width(8)]
        public int length;

        // flags
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  0), Width(1)]
        public bool bitRateSwitch;
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  1), Width(1)]
        public bool errorStateIndicator;
        // should always be set for FD CAN frame
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  2), Width(1)]
        public bool flexibleDataRateFrame;

        // data
        [PacketField, Offset(quadWords: 1), Width(MaxDataLength)]
        public byte[] data;
#pragma warning restore 649

        public const int MaxDataLength = 64;
        public const int Size = MaxDataLength + 8;
    }

    [LeastSignificantByteFirst]
    public struct XLSocketCANFrame : ISocketCANFrame
    {
        public override string ToString() => $@"XLSocketCANFrame {{
    priority: 0x{priority:X},
    virtualCANNetworkId: 0x{virtualCANNetworkId:X},
    simpleExtendedContent: {simpleExtendedContent},
    extendedFrameLengthFrame: {extendedFrameLengthFrame},
    serviceDataUnit: 0x{serviceDataUnit:X},
    length: {length},
    acceptanceField: 0x{acceptanceField:X},
    data: {Misc.PrettyPrintCollectionHex(data)}
}}";

        int ISocketCANFrame.Size => XLSocketCANFrame.Size;

#pragma warning disable 649
        // prio
        [PacketField, Offset(doubleWords: 0, bits: 0), Width(11)]
        public uint priority;
        [PacketField, Offset(doubleWords: 0, bits: 16), Width(8)]
        public byte virtualCANNetworkId;

        // flags
        [PacketField, Offset(doubleWords: 1, bytes: 0, bits:  0), Width(1)]
        public bool simpleExtendedContent;
        [PacketField, Offset(doubleWords: 1, bytes: 0, bits:  7), Width(1)]
        public bool extendedFrameLengthFrame;

        // sdt
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits: 0), Width(8)]
        public byte serviceDataUnit;

        // len
        [PacketField, Offset(doubleWords: 1, words: 1), Width(16)]
        public int length;

        // af
        [PacketField, Offset(doubleWords: 2), Width(32)]
        public uint acceptanceField;

        // data
        [PacketField, Offset(doubleWords: 3), Width(MaxDataLength)]
        public byte[] data;
#pragma warning restore 649

        public const int MaxDataLength = 2048;
        public const int Size = MaxDataLength + 12;
    }
}
