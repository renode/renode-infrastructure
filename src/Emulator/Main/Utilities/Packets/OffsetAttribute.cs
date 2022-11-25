//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.Packets
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class OffsetAttribute : Attribute
    {
        public OffsetAttribute(uint bytes = 0, uint bits = 0)
        {
            OffsetInBytes = bytes + (bits >> 3);
            OffsetInBits = bits & 0x7;
        }

        public uint OffsetInBytes { get; }
        public uint OffsetInBits { get; }
    }
}