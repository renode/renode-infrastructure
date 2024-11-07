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
    public struct RootSystemDescriptionTable
    {
        public RootSystemDescriptionTable(uint pointerToOtherSystemDescriptionTable0, uint pointerToOtherSystemDescriptionTable1)
        {
            Header = new SystemDescriptionTableHeader {
                Signature = Encoding.ASCII.GetBytes("RSDT"),
                TableLength = (uint)Packet.CalculateLength<RootSystemDescriptionTable>()
            };
            PointerToOtherSystemDescriptionTable0 = pointerToOtherSystemDescriptionTable0;
            PointerToOtherSystemDescriptionTable1 = pointerToOtherSystemDescriptionTable1;
        }

        [PacketField]
        public SystemDescriptionTableHeader Header;
        [PacketField]
        public uint PointerToOtherSystemDescriptionTable0;
        [PacketField]
        public uint PointerToOtherSystemDescriptionTable1;
    }
}
