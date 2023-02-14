//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

namespace Antmicro.Renode.Utilities.Packets
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class OffsetAttribute : Attribute
    {
        public OffsetAttribute(uint quadWords = 0, uint doubleWords = 0, uint words = 0, uint bytes = 0, uint bits = 0)
        {
            OffsetInBytes = new[]
            {
                (quadWords << 3),
                (doubleWords << 2),
                (words << 1),
                bytes,
                (bits >> 3)
            }.Aggregate((a, b) => a + b);
            OffsetInBits = bits & 0x7;
        }

        public uint OffsetInBytes { get; }
        public uint OffsetInBits { get; }
    }
}