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
        public OffsetAttribute(uint bytes = 0, uint bits = 0)
        {
            if(bytes != 0 && bits != 0)
            {
                throw new ArgumentException("Setting both offsets is currently not supported");
            }

            OffsetInBytes = bytes;
            OffsetInBits = bits;
        }

        public uint OffsetInBytes { get; }
        public uint OffsetInBits { get; }
    }
}