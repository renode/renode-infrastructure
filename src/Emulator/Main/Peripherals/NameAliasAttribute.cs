//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals
{
    /// <summary>
    /// This attribute provides an alternative name that a constructor
    /// parameter or enum type can be referred to in REPL.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class NameAliasAttribute : Attribute
    {
        public NameAliasAttribute(string name, bool warnOnUsage = true)
        {
            Name = name;
            WarnOnUsage = warnOnUsage;
        }

        public string Name { get; }
        public bool WarnOnUsage { get; }
    }
}
