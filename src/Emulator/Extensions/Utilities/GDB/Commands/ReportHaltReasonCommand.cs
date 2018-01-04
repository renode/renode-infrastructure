//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class ReportHaltReasonCommand : Command
    {
        public ReportHaltReasonCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("?")]
        public PacketData Execute()
        {
            return PacketData.StopReply(0);
        }
    }
}

