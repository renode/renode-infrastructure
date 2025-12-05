//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

namespace Antmicro.Renode.Utilities.Packets
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class WidthAttribute : Attribute
    {
#pragma warning disable IDE0060
        public WidthAttribute(object[] doNotUsePositionalArguments = null, uint bits = 0, uint bytes = 0, uint words = 0, uint doubleWords = 0, uint quadWords = 0, uint elements = 0)
#pragma warning restore IDE0060
        {
            Value = new[]
            {
                (quadWords << 6),
                (doubleWords << 5),
                (words << 4),
                (bytes << 3),
                bits,
            }.Aggregate((a, b) => a + b);
            Elements = elements;
        }

        public uint Value { get; }

        public uint Elements { get; }
    }
}
