//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using Antmicro.Renode.Logging;

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

            if(IsAccessAcrossPages(address, length))
            {
                return PacketData.ErrorReply();
            }

            if(!TryTranslateAddress(address, out var translatedAddress, write: false))
            {
                return PacketData.ErrorReply(Error.BadAddress);
            }

            foreach(var b in manager.Machine.SystemBus.ReadBytes(translatedAddress, (int)length))
            {
                content.AppendFormat("{0:x2}", b);
            }

            return new PacketData(content.ToString());
        }
    }
}
