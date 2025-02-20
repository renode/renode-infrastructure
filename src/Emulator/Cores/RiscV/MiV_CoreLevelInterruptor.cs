//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class MiV_CoreLevelInterruptor : CoreLevelInterruptor, IKnownSize
    {
        public MiV_CoreLevelInterruptor(IMachine machine, long frequency, uint prescaler = 1, int numberOfTargets = 1)
            : base(machine, frequency, numberOfTargets, prescaler)
        {
            // we are extending the existing register map defined by the base class
            AddRegister((long)Registers.Prescaler,
                new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => prescaler));
        }

        private enum Registers : long
        {
            Prescaler = 0x5000
        }
    }
}
