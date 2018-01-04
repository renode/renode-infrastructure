//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Mono.Cecil;
using System.Linq;
using Antmicro.Renode.Plugins;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Utilities
{
    public class PluginDescriptor
    {
        public PluginDescriptor(TypeDefinition td, bool hidden)
        {
            ThisType = td;
            IsHidden = hidden;
            var pluginAttribute = td.CustomAttributes.SingleOrDefault(x => x.AttributeType.GetFullNameOfMember() == typeof(PluginAttribute).FullName);
            if(pluginAttribute != null)
            {
                Version = Version.Parse((string)pluginAttribute.Properties.Single(x => x.Name == "Version").Argument.Value);
                Name = (string)pluginAttribute.Properties.Single(x => x.Name == "Name").Argument.Value;
                Description = (string)pluginAttribute.Properties.Single(x => x.Name == "Description").Argument.Value;
                Vendor = (string)pluginAttribute.Properties.Single(x => x.Name == "Vendor").Argument.Value;
                var dependencies = pluginAttribute.Properties.SingleOrDefault(x => x.Name == "Dependencies").Argument.Value;
                if(dependencies != null)
                {
                    Dependencies = ((CustomAttributeArgument[])dependencies).Select(x => ((TypeReference)x.Value).Resolve()).ToArray();
                }
                var modes = pluginAttribute.Properties.SingleOrDefault(x => x.Name == "Modes").Argument.Value;
                Modes = modes != null ? ((CustomAttributeArgument[])modes).Select(x => x.Value).Cast<string>().ToArray() : new string[0];
            }
        }

        public string FullName
        {
            get
            {
                return "{0}:{1}:{2}".FormatWith(Name, Version, Vendor);
            }
        }

        public string Name { get; private set; }
        public Version Version { get; private set; }
        public string Description { get; private set; }
        public string Vendor { get; private set; }
        public TypeDefinition ThisType { get; private set; }
        public TypeDefinition[] Dependencies { get; private set; }
        public string[] Modes { get; private set; }
        public bool IsHidden { get; private set; }

        public object CreatePlugin()
        {
            var type = TypeManager.Instance.GetTypeByName(ThisType.GetFullNameOfMember());
            return ObjectCreator.Instance.Spawn(type);
        }

        public override bool Equals(object obj)
        {
            var objAsPluginDescriptor = obj as PluginDescriptor;
            if(objAsPluginDescriptor != null)
            {
                return Name == objAsPluginDescriptor.Name &&
                    Version.Equals(objAsPluginDescriptor.Version) &&
                    Description == objAsPluginDescriptor.Description &&
                    Vendor == objAsPluginDescriptor.Vendor &&
                    ((Modes == null && objAsPluginDescriptor.Modes == null) ||
                        Enumerable.SequenceEqual(Modes, objAsPluginDescriptor.Modes)) &&
                    ((Dependencies == null && objAsPluginDescriptor.Dependencies == null) ||
                        Enumerable.SequenceEqual(Dependencies, objAsPluginDescriptor.Dependencies));
            }

            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Name.GetHashCode();
                hash = (hash * 397) ^ Version.GetHashCode();
                hash = (hash * 397) ^ Description.GetHashCode();
                hash = (hash * 397) ^ Vendor.GetHashCode();
                if(Modes != null)
                {
                    foreach(var mode in Modes)
                    {
                        hash = (hash * 397) ^ mode.GetHashCode();
                    }
                }
                if(Dependencies != null)
                {
                    foreach(var dependency in Dependencies)
                    {
                        hash = (hash * 397) ^ dependency.GetHashCode();
                    }
                }
                return hash;
            }
        }
    }
}

