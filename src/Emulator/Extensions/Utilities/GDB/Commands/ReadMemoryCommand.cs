//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using Antmicro.Renode.Exceptions;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class ReadMemoryCommand : Command
    {
        public ReadMemoryCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("m")]
        public PacketData Execute(
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong address,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong length)
        {
            var content = new StringBuilder();
            var accesses = GetTranslatedAccesses(address, length, write: false);

            if(accesses == null)
            {
                return PacketData.ErrorReply(Error.BadAddress);
            }

            foreach(var access in accesses)
            {
                if(manager.Machine.SystemBus.WhatIsAt(access.Address, context: manager.Cpu) == null)
                {
                    return PacketData.ErrorReply(Error.BadAddress);
                }

                byte[] data;
                ulong val;
                try
                {
                    switch(access.Length)
                    {
                        case 1:
                            val = manager.Machine.SystemBus.ReadByte(access.Address, context: manager.Cpu);
                            data = BytesFromValue(val, access.Length);
                            break;
                        case 2:
                            val = manager.Machine.SystemBus.ReadWord(access.Address, context: manager.Cpu);
                            data = BytesFromValue(val, access.Length);
                            break;
                        case 4:
                            val = manager.Machine.SystemBus.ReadDoubleWord(access.Address, context: manager.Cpu);
                            data = BytesFromValue(val, access.Length);
                            break;
                        case 8:
                            val = manager.Machine.SystemBus.ReadQuadWord(access.Address, context: manager.Cpu);
                            data = BytesFromValue(val, access.Length);
                            break;
                        default:
                            data = manager.Machine.SystemBus.ReadBytes(access.Address, (int)access.Length, context: manager.Cpu);
                            break;
                    }
                }
                catch(RecoverableException)
                {
                    return PacketData.ErrorReply(Error.BadAddress);
                }

                foreach(var b in data)
                {
                    content.AppendFormat("{0:x2}", b);
                }
            }

            return new PacketData(content.ToString());
        }

        private byte [] BytesFromValue(ulong val, ulong length)
        {
            return BitHelper.GetBytesFromValue(val, (int)length, manager.Cpu.Endianness == Endianess.LittleEndian);
        }
    }
}
