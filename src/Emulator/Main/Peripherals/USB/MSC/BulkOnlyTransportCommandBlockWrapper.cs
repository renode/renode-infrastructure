//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.USB.MSC
{
    public struct BulkOnlyTransportCommandBlockWrapper
    {
        public static bool TryParse(byte[] bytes, out BulkOnlyTransportCommandBlockWrapper cbw)
        {
            if(bytes == null
                || bytes.Length != 31
                || BitConverter.ToUInt32(bytes, 0) != Signature)
            {
                cbw = default(BulkOnlyTransportCommandBlockWrapper);
                return false;
            }

            cbw = Packet.Decode<BulkOnlyTransportCommandBlockWrapper>(bytes);

            return true;
        }

        [PacketField, LeastSignificantByteFirst]
        public uint Tag { get; private set; }

        [PacketField, LeastSignificantByteFirst]
        public uint DataTransferLength  { get; private set; }

        [PacketField]
        public byte Flags { get; private set; }

        [PacketField, Offset(bytes: 13), Width(4)]
        public byte LogicalUnitNumber { get; private set; }

        [PacketField, Offset(bytes: 14), Width(5)]
        public byte Length { get; private set; }

        public const int CommandOffset = 15;

        private const uint Signature = 0x43425355;
    }
}