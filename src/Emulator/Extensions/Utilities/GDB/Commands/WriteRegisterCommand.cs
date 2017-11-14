//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Linq;
using System;

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
            if(!manager.Cpu.GetRegisters().Contains(registerNumber))
            {
                return PacketData.ErrorReply(0);
            }

            manager.Cpu.SetRegisterUnsafe(registerNumber, BitConverter.ToUInt32(value, 0));
            return PacketData.Success;
        }
    }
}

