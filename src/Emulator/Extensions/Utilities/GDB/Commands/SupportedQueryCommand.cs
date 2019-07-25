//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Text;

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
            var command = new StringBuilder();
            command.Append(string.Format("PacketSize={0:x4};qXfer:features:read+;swbreak+;hwbreak+", 4096));
            if(manager.Machine.SystemBus.IsMultiCore)
            {
                command.Append(";qXfer:threads:read+;vContSupported+");
            }
            return new PacketData(command.ToString());
        }
    }
}

