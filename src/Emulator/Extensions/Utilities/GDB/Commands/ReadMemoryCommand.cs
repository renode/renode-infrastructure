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
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]int length)
        {
            var content = new StringBuilder();

            if(IsAccessAcrossPages(address, (ulong)length))
            {
                return PacketData.ErrorReply(0);
            }

            if(!TryTranslateAddress(address, out var translatedAddress, write: false))
            {
                return PacketData.ErrorReply(14);
            }

            foreach(var b in manager.Machine.SystemBus.ReadBytes(translatedAddress, length))
            {
                content.AppendFormat("{0:x2}", b);
            }

            return new PacketData(content.ToString());
        }
    }
}
