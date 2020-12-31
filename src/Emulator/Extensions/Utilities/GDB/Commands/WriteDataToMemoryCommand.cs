//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
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
                return PacketData.ErrorReply(22);
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
                return PacketData.ErrorReply(22);
            }
            return WriteData(address, data);
        }

        private PacketData WriteData(ulong address, byte[] data)
        {
            if(IsAccessAcrossPages(address, (ulong)data.Length))
            {
                return PacketData.ErrorReply(0);
            }

            if(!TryTranslateAddress(address, out var translatedAddress, write: true))
            {
                return PacketData.ErrorReply(14);
            }

            manager.Machine.SystemBus.WriteBytes(data, translatedAddress);

            return PacketData.Success;
        }
    }
}

