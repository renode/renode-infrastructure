//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Plugins
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAttribute : Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Vendor { get; set; }
        public Type[] Dependencies { get; set; }
        public string[] Modes { get; set; }
    }
}

