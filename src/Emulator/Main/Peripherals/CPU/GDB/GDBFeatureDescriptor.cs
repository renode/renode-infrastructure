//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct GDBFeatureDescriptor
    {
        public GDBFeatureDescriptor(string name) : this()
        {
            this.Name = name;
            this.Registers = new List<GDBRegisterDescriptor>();
            this.Types = new List<GDBCustomType>();
        }

        public string Name { get; }
        public List<GDBRegisterDescriptor> Registers { get; }
        public List<GDBCustomType> Types { get; }
    }
}

