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
    [LeastSignificantByteFirst]
    public struct BulkOnlyTransportCommandBlockWrapper
    {
        public static bool TryParse(byte[] bytes, out BulkOnlyTransportCommandBlockWrapper cbw)
        {
            if(bytes == null
                || bytes.Length != 31
                || BitConverter.ToUInt32(bytes, 0) != SignatureValue)
            {
                cbw = default(BulkOnlyTransportCommandBlockWrapper);
                return false;
            }

            cbw = Packet.Decode<BulkOnlyTransportCommandBlockWrapper>(bytes);

            return true;
        }

        public override string ToString()
        {
            return $"Tag: 0x{Tag:x}, DataTransferLength: {DataTransferLength}, Flags: 0x{Flags:x}, LogicalUnitNumber: {LogicalUnitNumber}, Length: {Length}";
        }

        [PacketField]
        public uint Signature => SignatureValue;

        [PacketField]
        public uint Tag { get; private set; }

        [PacketField]
        public uint DataTransferLength  { get; private set; }

        [PacketField]
        public byte Flags { get; private set; }

        [PacketField, Offset(bytes: 13), Width(4)]
        public byte LogicalUnitNumber { get; private set; }

        [PacketField, Offset(bytes: 14), Width(5)]
        public byte Length { get; private set; }

        public const int CommandOffset = 15;
        public const uint SignatureValue = 0x43425355;
    }
}