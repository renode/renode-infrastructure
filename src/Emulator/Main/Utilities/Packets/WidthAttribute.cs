//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.Packets
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class WidthAttribute : Attribute
    {
        public WidthAttribute(uint value)
        {
            Value = value;
        }

        public uint Value { get; }
    }
}