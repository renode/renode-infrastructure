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
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class PaddingBeforeAttribute : Attribute
    {
        public PaddingBeforeAttribute(uint quadWords = 0, uint doubleWords = 0, uint words = 0, uint bytes = 0)
        {
            PaddingInBytes = new[]
            {
                (quadWords << 3),
                (doubleWords << 2),
                (words << 1),
                bytes,
            }.Aggregate((a, b) => a + b);
        }

        public uint PaddingInBytes { get; }
    }
}