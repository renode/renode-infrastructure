//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Text;

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.ACPI
{
    [LeastSignificantByteFirst]
    public struct DifferentiatedSystemDescriptionTable
    {
        public DifferentiatedSystemDescriptionTable(uint tableLength)
        {
            Header = new SystemDescriptionTableHeader
            {
                Signature = Encoding.ASCII.GetBytes("DSDT"),
                TableLength = tableLength
            };
        }

        [PacketField]
        public SystemDescriptionTableHeader Header;
    }
}