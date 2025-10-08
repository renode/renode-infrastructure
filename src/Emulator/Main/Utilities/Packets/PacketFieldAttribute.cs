//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities.Packets
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class PacketFieldAttribute : Attribute
    {
        public PacketFieldAttribute([System.Runtime.CompilerServices.CallerLineNumber] int order = 0)
        {
            this.order = order;
        }

        public int Order { get { return order; } }

        private readonly int order;
    }
}