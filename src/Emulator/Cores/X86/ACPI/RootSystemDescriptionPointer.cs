//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Text;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.ACPI
{
    [LeastSignificantByteFirst]
    public struct RootSystemDescriptionPointer
    {
        public RootSystemDescriptionPointer(uint address)
        {
            Signature = Encoding.ASCII.GetBytes("RSD PTR ");
            Checksum = 0;
            OEMID = null;
            Revision = 0;
            RsdtAddress = address;
        }

        [PacketField, Width(8)]
        public byte[] Signature;
        [PacketField]
        public byte Checksum;
        [PacketField, Width(6)]
        public byte[] OEMID;
        [PacketField]
        public byte Revision;
        [PacketField]
        public uint RsdtAddress;
    }
}
