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

            // if register exists in emulated core return current value of this
            if(manager.Cpu.GetRegisters().Any(x => x.Index == registerNumber))
            { 
                foreach(var b in manager.Cpu.GetRegisterUnsafe(registerNumber).GetBytes())
                {
                    content.AppendFormat("{0:x2}", b);
                }
                return new PacketData(content.ToString());
            }

            // if register no exists in emulated core but is defined in a feature
            // return a cero value with number of bytes defined in the feature
            var selectedRegisters = manager.Cpu.GDBFeatures.SelectMany(f => f.Registers)
                .Where(r => r.Number == registerNumber);

            if(selectedRegisters.Any())
            {
                for(var b = 0; b < selectedRegisters.First().Size / 8; b++)
                {
                    content.AppendFormat("00");
                }
                return new PacketData(content.ToString());
            }

            // else return error
            return PacketData.ErrorReply(1);

        }
    }
}

