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
    public struct MultipleAPICDescriptionTable
    {
        public MultipleAPICDescriptionTable(uint tableLength)
        {
            Header = new SystemDescriptionTableHeader
            {
                Signature = Encoding.ASCII.GetBytes("APIC"),
                TableLength = tableLength
            };
            LocalAPICAddress = 0;
            Flags = 0;
        }

        [PacketField]
        public SystemDescriptionTableHeader Header;
        [PacketField]
        public uint LocalAPICAddress;
        [PacketField]
        public uint Flags;
    }
}
