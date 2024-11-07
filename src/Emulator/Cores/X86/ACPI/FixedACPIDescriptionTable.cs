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
    public struct FixedACPIDescriptionTable
    {
        public FixedACPIDescriptionTable(uint tableLength, uint extendedDSDTPointer)
        {
            Header = new SystemDescriptionTableHeader {
                Signature = Encoding.ASCII.GetBytes("FACP"),
                TableLength = tableLength
            };

            ExtendedDSDTPointer = extendedDSDTPointer;
        }

        [PacketField]
        public SystemDescriptionTableHeader Header;

        // Rest of the fields are omitted, as they aren't currently needed.

        [PacketField, Offset(bytes: 0x8c)]
        public uint ExtendedDSDTPointer;
    }
}
