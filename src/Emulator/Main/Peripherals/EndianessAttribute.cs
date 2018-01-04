//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EndianessAttribute : Attribute
    {
        public EndianessAttribute(Endianess endianess)
        {
            this.endianess = endianess;
        }

        public Endianess Endianess
        {
            get
            {
                return endianess;
            }
        }

        private readonly Endianess endianess;
    }
}

