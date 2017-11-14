//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Text;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class ReadRegisterCommand : Command
    {
        public ReadRegisterCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("p")]
        public PacketData Execute(
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]int registerNumber)
        {
            var content = new StringBuilder();
            var value = manager.Cpu.GetRegisters().Contains(registerNumber) ? manager.Cpu.GetRegisterUnsafe(registerNumber) : 0;
            foreach(var b in BitConverter.GetBytes(value))
            {
                content.AppendFormat("{0:x2}", b);
            }
            return new PacketData(content.ToString());
        }
    }
}

