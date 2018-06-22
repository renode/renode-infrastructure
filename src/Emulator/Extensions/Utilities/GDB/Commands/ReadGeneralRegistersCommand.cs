//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Text;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class ReadGeneralRegistersCommand : Command
    {
        public ReadGeneralRegistersCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("g")]
        public PacketData Execute()
        {
            var registers = new StringBuilder();
            foreach(var i in manager.Cpu.GetRegisters().Where(x => x.IsGeneral))
            {
                var value = manager.Cpu.GetRegisterUnsafe(i.Index);
                foreach(var b in value.GetBytes())
                {
                    registers.AppendFormat("{0:x2}", b);
                }
            }

            return new PacketData(registers.ToString());
        }
    }
}

