//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Logging;

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
                manager.Machine.SystemBus.WriteBytes(data, access.Address, startingIndex, (long)access.Length);
                startingIndex += (int)access.Length;
            }

            return PacketData.Success;
        }
    }
}

