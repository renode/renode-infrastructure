//
// Copyright (c) 2010-2018 Antmicro
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
        public OffsetAttribute(int bytes = 0, int bits = 0)
        {
            OffsetInBytes = bytes;
            OffsetInBits = bits;
        }

        public int OffsetInBytes { get; }
        public int OffsetInBits { get; }
    }
}