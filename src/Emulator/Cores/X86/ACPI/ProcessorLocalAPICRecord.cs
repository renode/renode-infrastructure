//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.ACPI
{
    // Processor Local APIC Record - record of Multiple APIC Description Table
    [LeastSignificantByteFirst]
    public struct ProcessorLocalAPICRecord
    {
        [PacketField]
        public byte EntryType;
        [PacketField]
        public byte RecordLength;
        [PacketField]
        public byte ACPIProcessorID;
        [PacketField]
        public byte APICID;
        [PacketField]
        public uint Flags;
    }
}
