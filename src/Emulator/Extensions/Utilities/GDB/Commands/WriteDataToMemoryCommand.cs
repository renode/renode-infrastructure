//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Logging;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class WriteDataToMemoryCommand : Command
    {
        public WriteDataToMemoryCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("M")]
        public PacketData WriteHexData(
           [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong address,
           [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint length,
           [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexBytesString)]byte[] data)
        {
            if(data.Length != length)
            {
                Logger.LogAs(this, LogLevel.Warning, "length argument does not match the size of sent data.");
                return PacketData.ErrorReply(Error.InvalidArgument);
            }
            return WriteData(address, data);
        }

        [Execute("X")]
        public PacketData WriteBinaryData(
           [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong address,
           [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint length,
           [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.BinaryBytes)]byte[] data)
        {
            if(data.Length != length)
            {
                Logger.LogAs(this, LogLevel.Warning, "length argument does not match the size of sent data.");
                return PacketData.ErrorReply(Error.InvalidArgument);
            }
            return WriteData(address, data);
        }

        private PacketData WriteData(ulong address, byte[] data)
        {
            var accesses = GetTranslatedAccesses(address, (ulong)data.Length, write: true);

            if(accesses == null)
            {
                return PacketData.ErrorReply(Error.BadAddress);
            }

            int startingIndex = 0;
            foreach(var access in accesses)
            {
                var val = BitHelper.ToUInt64(data, startingIndex, (int)access.Length, reverse: manager.Cpu.Endianness == Endianess.LittleEndian);
                switch(access.Length)
                {
                    case 1:
                        manager.Machine.SystemBus.WriteByte(access.Address, (byte)val, context: manager.Cpu);
                        break;
                    case 2:
                        manager.Machine.SystemBus.WriteWord(access.Address, (ushort)val, context: manager.Cpu);
                        break;
                    case 4:
                        manager.Machine.SystemBus.WriteDoubleWord(access.Address, (uint)val, context: manager.Cpu);
                        break;
                    case 8:
                        manager.Machine.SystemBus.WriteQuadWord(access.Address, (ulong)val, context: manager.Cpu);
                        break;
                    default:
                        manager.Machine.SystemBus.WriteBytes(data, access.Address, startingIndex, (long)access.Length, context: manager.Cpu);
                        break;
                }
                startingIndex += (int)access.Length;
            }

            return PacketData.Success;
        }
    }
}

