//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class WriteRegisterCommand : Command
    {
        public WriteRegisterCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("P")]
        public PacketData Execute(
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber, Separator = '=')]int registerNumber,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)]byte[] value)
        {
            var reg = manager.Cpu.GetRegisters().SingleOrDefault(x => x.Index == registerNumber);
            if(reg.Width == 0)
            {
                return PacketData.ErrorReply(0);
            }

            manager.Cpu.SetRegisterUnsafe(registerNumber, reg.ValueFromBytes(value));
            return PacketData.Success;
        }
    }
}

