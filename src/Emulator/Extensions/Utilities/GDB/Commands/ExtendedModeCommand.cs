//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities.GDB;

namespace Antmicro.Renode.Extensions.Utilities.GDB.Commands
{
    internal class ExtendedModeCommand(CommandsManager manager) : Command(manager)
    {
        [Execute("!")]
        public PacketData Execute()
        {
            // Extended mode is supported
            return PacketData.Success;
        }
    }
}
