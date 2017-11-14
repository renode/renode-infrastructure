//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class SupportedQueryCommand : Command
    {
        public SupportedQueryCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("qSupported")]
        public PacketData Execute()
        {
            return new PacketData(string.Format("PacketSize={0:x4};swbreak+;hwbreak+", 4096));
        }
    }
}

