//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

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
            manager.Machine.SystemBus.WriteBytes(data, address);
            return PacketData.Success;
        }

        [Execute("X")]
        public PacketData WriteBinaryData(
           [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]ulong address,
           [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]uint length,
           [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.BinaryBytes)]byte[] data)
        {
            manager.Machine.SystemBus.WriteBytes(data, address);
            return PacketData.Success;
        }
    }
}

