//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.USB.MSC.BOT
{
    [LeastSignificantByteFirst]
    public class CommandStatusWrapper
    {
        public CommandStatusWrapper(uint tag, uint dataResidue, CommandStatus status)
        {
            Tag = tag;
            DataResidue = dataResidue;
            Status = status;
        }

        [PacketField, Offset(bytes: 4)]
        public uint Tag { get; }

        [PacketField]
        public uint DataResidue { get; }

        [PacketField]
        public CommandStatus Status { get; }

        [PacketField, Offset(bytes: 0)]
        private const uint Signature = 0x53425355;
    }
}