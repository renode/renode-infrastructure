//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.GDB.Commands
{
    internal class QueryCommand : Command
    {
        public QueryCommand(CommandsManager manager) : base(manager)
        {
        }

        [Execute("qXfer")]
        public PacketData SendQueryXml(
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.String)]string command,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.String)]string objectType,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.String)]string operation,
            [Argument(Separator = ':', Encoding = ArgumentAttribute.ArgumentEncoding.String)]string annex,
            [Argument(Separator = ',', Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]int offset,
            [Argument(Encoding = ArgumentAttribute.ArgumentEncoding.HexNumber)]int length
        )
        {
            if(objectType != "features" || operation != "read")
            {
                return PacketData.Empty;
            }
            if(annex != "target.xml" || offset > length)
            {
                return PacketData.ErrorReply(0);
            }
          
            var xmlFile = $"<?xml version=\"1.0\"?>\n<!DOCTYPE target SYSTEM \"gdb-target.dtd\">\n<target version=\"1.0\">\n<architecture>{manager.Cpu.GDBArchitecture}</architecture>\n</target>";
            var prefix = offset + length >= xmlFile.Length ? "l" : "m";
            var xmlSubstring = xmlFile.Substring(offset, Math.Min(length, xmlFile.Length - offset));
            return new PacketData(prefix + xmlSubstring);
        }
    }
}

