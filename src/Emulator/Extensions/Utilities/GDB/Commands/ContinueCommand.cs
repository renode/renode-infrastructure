//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class ContinueCommand : Command
    {
        public ContinueCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("c")]
        public PacketData Execute()
        {
            manager.Cpu.ExecutionMode = ExecutionMode.Continuous;
            manager.Cpu.Resume();

            return null;
        }
    }
}

