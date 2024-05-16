//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class EmptyCPU : BaseCPU
    {
        public EmptyCPU(IMachine machine, string model = "emptyCPU") : base(0, model, machine, ELFSharp.ELF.Endianess.LittleEndian)
        {
        }

        public virtual void Load(PrimitiveReader reader)
        {
        }

        public virtual void Save(PrimitiveWriter writer)
        {
        }

        public override ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            numberOfExecutedInstructions = 0;
            return ExecutionResult.Interrupted;
        }

        public override string Architecture => "empty";

        public override ulong ExecutedInstructions => 0;

        public override RegisterValue PC
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }
    }
}

