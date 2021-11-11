//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Timers;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public partial class Xtensa : TranslationCPU
    {
        public Xtensa(string cpuType, Machine machine)
                : base(cpuType, machine, Endianess.LittleEndian)
        {
        }

        public override string Architecture { get { return "xtensa"; } }

        public override string GDBArchitecture { get { return "xtensa"; } }
        
        public override List<GDBFeatureDescriptor> GDBFeatures => new List<GDBFeatureDescriptor>();
        
        protected override Interrupt DecodeInterrupt(int number)
        {
            return Interrupt.Hard;
        }
    }
}

