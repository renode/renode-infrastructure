//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Utilities.Packets;
using Antmicro.Renode.Core.USB;

namespace Antmicro.Renode.Extensions.Utilities.USBIP
{
    // the actual packet
    // is prepended with
    // the USBIP.URBHeader
    public struct URBRequest
    {
        [PacketField]
        public uint TransferBufferLength;
        [PacketField]
        public uint StartFrame;
        [PacketField]
        public uint NumberOfPackets;
        [PacketField]
        public uint Interval;
        [PacketField]
        public ulong Setup;

        public override string ToString()
        {
            var bytes = BitConverter.GetBytes(Setup);
            Array.Reverse(bytes, 0, bytes.Length);
            var decodedSetup = Packet.Decode<SetupPacket>(bytes);
            return $"TransferBufferLength = 0x{TransferBufferLength:X}, StartFrame = 0x{StartFrame:X}, NumberOfPackets = 0x{NumberOfPackets:X}, Interval = 0x{Interval:X}, Setup = 0x{Setup:X} [{decodedSetup}]";
        }
    }
}
