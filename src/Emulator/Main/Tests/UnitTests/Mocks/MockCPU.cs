//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class MockCPU : EmptyCPU
    {
        public MockCPU(IMachine machine) : base(machine, "mock")
        {
        }

        public string Placeholder { get; set; }

        public TwoStateEnum EnumValue { get; set; }

        public ICPU OtherCpu { get; set; }

        public override void Load(PrimitiveReader reader)
        {
            var present = reader.ReadBoolean();
            if(present)
            {
                Placeholder = reader.ReadString();
            }
        }

        public override void Save(PrimitiveWriter writer)
        {
            var present = Placeholder != null;
            writer.Write(present);
            if(present)
            {
                writer.Write(Placeholder);
            }
        }
    }
}

