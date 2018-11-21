//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Storage.SCSI.Commands
{
    public interface IReadWrite10Command
    {
        [PacketField, Offset(bytes: 2)]
        uint LogicalBlockAddress { get; set; }

        [PacketField, Offset(bytes: 7)]
        ushort TransferLength { get; set; }
    }
}