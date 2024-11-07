//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.ACPI
{
    [LeastSignificantByteFirst]
    public struct SystemDescriptionTableHeader
    {
        [PacketField, Width(4)]
        public byte[] Signature;
        [PacketField]
        public uint TableLength;
        [PacketField]
        public byte Revision;
        [PacketField]
        public byte Checksum;
        [PacketField, Width(6)]
        public byte[] OEMID;
        [PacketField, Width(8)]
        public byte[] OEMTableID;
        [PacketField]
        public uint OEMRevision;
        [PacketField]
        public uint CreatorID;
        [PacketField]
        public uint CreatorRevision;
    }
}
