//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;

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
            if((objectType != "features" && objectType != "threads") || operation != "read")
            {
                return PacketData.Empty;
            }
            if((objectType == "features" && annex != "target.xml") || offset > length)
            {
                return PacketData.ErrorReply(0);
            }
            var xmlFile = new StringBuilder();
            if(objectType == "features")
            {
                xmlFile.Append("<?xml version=\"1.0\"?>\n<!DOCTYPE feature SYSTEM \"gdb-target.dtd\">\n<target version=\"1.0\">\n");
                xmlFile.Append($"<architecture>{manager.Cpu.GDBArchitecture}</architecture>\n");
                foreach(var feature in manager.Cpu.GDBFeatures)
                {
                    xmlFile.Append($"<feature name=\"{feature.Name}\">\n");
                    foreach(var register in feature.Registers)
                    {
                        xmlFile.Append($"<reg name=\"{register.Name}\" bitsize=\"{register.Size}\" regnum=\"{register.Number}\" type=\"{register.Type}\" group=\"{register.Group}\"/>\n");
                    }
                    xmlFile.Append("</feature>\n");
                }
                xmlFile.Append("</target>\n");
            }
            else if(objectType == "threads")
            {
                xmlFile.Append("<?xml version=\"1.0\"?>\n<threads>\n");
                foreach(var cpu in manager.ManagedCpus)
                {
                    xmlFile.Append($"<thread id=\"{cpu.Key}\" core=\"{cpu.Key - 1}\" name=\"{cpu.Value.Name}\"></thread>\n");
                }
                xmlFile.Append("</threads>\n");
            }

            var prefix = offset + length >= xmlFile.Length ? "l" : "m";
            var xmlSubstring = xmlFile.ToString().Substring(offset, Math.Min(length, xmlFile.Length - offset));
            return new PacketData(prefix + xmlSubstring);
        }
    }
}

