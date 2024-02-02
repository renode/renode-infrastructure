//
// Copyright (c) 2010-2024 Antmicro
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
            // Trace32 extensions aren't supported by all CPUs but it shouldn't break anything.
            command.Append(string.Format("PacketSize={0};qXfer:features:read+;swbreak+;hwbreak+;t32extensions+", 1024));
            if(manager.Machine.SystemBus.IsMultiCore)
            {
                command.Append(";qXfer:threads:read+;vContSupported+");
            }
            return new PacketData(command.ToString());
        }
    }
}

