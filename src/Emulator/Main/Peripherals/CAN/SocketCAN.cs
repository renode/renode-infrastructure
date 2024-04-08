//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.CAN
{
    public static class ISocketCANFrameExtentions
    {
        public static byte[] Encode<T>(this T @this, bool useNetworkByteOrder) where T : ISocketCANFrame
        {
            var frame = Packet.Encode<T>(@this);
            if(useNetworkByteOrder)
            {
                @this.ByteSwap(frame);
            }
            return frame;
        }

        public static void ByteSwap(this ISocketCANFrame @this, byte[] frame)
        {
            if(frame.Length != @this.Size)
            {
                throw new ArgumentException($"Number of bytes in {nameof(frame)} must match the size of a SocketCAN structure", nameof(frame));
            }
            foreach(var marker in @this.MultibyteFields)
            {
                Misc.EndiannessSwapInPlace(frame, marker.size, marker.offset, marker.size);
            }
        }
    }

    public interface ISocketCANFrame
    {
        int Size { get; }
        IEnumerable<FieldMarker> MultibyteFields { get; }
    }

    public struct FieldMarker
    {
        public static FieldMarker Create(int size, int offset) =>
            new FieldMarker { size = size, offset = offset };

        public int size;
        public int offset;
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

        public IEnumerable<FieldMarker> MultibyteFields => multibyteFields;

        public int Size => MaxDataLength + 8;

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

        private readonly static FieldMarker[] multibyteFields = new FieldMarker[]
        {
            FieldMarker.Create(size: 4, offset: 0)
        };
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

        public IEnumerable<FieldMarker> MultibyteFields => multibyteFields;

        public int Size => MaxDataLength + 8;

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
        [PacketField, Offset(doubleWords: 1, bytes: 1, bits:  2), Width(1)]
        public bool flexibleDataRateFrame;

        // data
        [PacketField, Offset(quadWords: 1), Width(MaxDataLength)]
        public byte[] data;
#pragma warning restore 649

        public const int MaxDataLength = 64;

        private readonly static FieldMarker[] multibyteFields = new FieldMarker[]
        {
            FieldMarker.Create(size: 4, offset: 0)
        };
    }
}
