//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

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
            return $"TransferBufferLength = 0x{TransferBufferLength:X}, StartFrame = 0x{StartFrame:X}, NumberOfPackets = 0x{NumberOfPackets:X}, Interval = 0x{Interval:X}, Setup = 0x{Setup:X}";
        }
    }
}
