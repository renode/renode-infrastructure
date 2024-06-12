//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;

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
            if(objectType == "features" && annex != "target.xml")
            {
                return PacketData.ErrorReply();
            }
            var xmlFile = new StringBuilder();
            if(objectType == "features")
            {
                xmlFile.Append("<?xml version=\"1.0\"?>\n<!DOCTYPE feature SYSTEM \"gdb-target.dtd\">\n<target version=\"1.0\">\n");
                xmlFile.Append($"<architecture>{manager.Cpu.GDBArchitecture}</architecture>\n");
                foreach(var feature in manager.GetCompiledFeatures())
                {
                    AppendFeature(ref xmlFile, feature);
                }
                xmlFile.Append("</target>\n");
            }
            else if(objectType == "threads")
            {
                xmlFile.Append("<?xml version=\"1.0\"?>\n<threads>\n");
                foreach(var gdbCpuId in manager.ManagedCpus.GdbCpuIds)
                {
                    xmlFile.Append($"<thread id=\"{gdbCpuId:x}\" core=\"{gdbCpuId - 1}\" name=\"{manager.ManagedCpus[gdbCpuId].GetName()}\"></thread>\n");
                }
                xmlFile.Append("</threads>\n");
            }

            var prefix = offset + length >= xmlFile.Length ? "l" : "m";
            var xmlSubstring = xmlFile.ToString().Substring(offset, Math.Min(length, xmlFile.Length - offset));
            return new PacketData(prefix + xmlSubstring);
        }

        private static void AppendFeature(ref StringBuilder xmlFile, GDBFeatureDescriptor feature)
        {
            xmlFile.Append($"<feature name=\"{feature.Name}\">\n");
            foreach(var type in feature.Types)
            {
                if(type.Fields?.Any() ?? false)
                {
                    var tagName = type.Type == "enum" ? "evalue" : "field";
                    AppendTag(ref xmlFile, type.Type, type.Attributes, false);
                    foreach(var field in type.Fields)
                    {
                        AppendTag(ref xmlFile, tagName, field);

                    }
                    xmlFile.Append($"</{type.Type}>\n");
                }
                else
                {
                    AppendTag(ref xmlFile, type.Type, type.Attributes);
                }
            }
            foreach(var register in feature.Registers)
            {
                xmlFile.Append($"<reg name=\"{register.Name}\" bitsize=\"{register.Size}\" regnum=\"{register.Number}\" ");
                if(!String.IsNullOrEmpty(register.Type))
                {
                    xmlFile.Append($"type=\"{register.Type}\" ");
                }
                if(!String.IsNullOrEmpty(register.Group))
                {
                    xmlFile.Append($"group=\"{register.Group}\" ");
                }
                xmlFile.Append("/>\n");
            }
            xmlFile.Append("</feature>\n");
        }

        private static void AppendTag(ref StringBuilder xmlFile, string name, IReadOnlyDictionary<string, string> attributes, bool closed = true)
        {
            xmlFile.Append($"<{name}");
            foreach(var pair in attributes)
            {
                xmlFile.Append($" {pair.Key}=\"{pair.Value}\"");
            }
            xmlFile.Append(closed ? "/>\n" : ">\n");
        }
    }
}

