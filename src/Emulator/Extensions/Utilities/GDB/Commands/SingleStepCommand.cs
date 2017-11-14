//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class SingleStepCommand : Command
    {
        public SingleStepCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("s")]
        public PacketData Execute()
        {
            manager.Cpu.ExecutionMode = ExecutionMode.SingleStep;
            manager.Cpu.Step(wait: false);
            return null;
        }
    }
}

