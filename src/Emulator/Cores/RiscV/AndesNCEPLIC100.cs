//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.QuadWordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class AndesNCEPLIC100 : PlatformLevelInterruptController, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public AndesNCEPLIC100(int numberOfSources, int numberOfContexts, bool prioritiesEnabled = true)
            : base(numberOfSources, numberOfContexts, prioritiesEnabled)
        {
            DefineRegisters(numberOfSources);
        }

        public DoubleWordRegisterCollection RegistersCollection => base.registers;

        private void DefineRegisters(int numberOfSources)
        {
            // Each 32-bit register contains 32 individual 1-bit flags,
            // indicating the trigger type of that source.
            var numberOfTriggerTypeRegisters = (uint)Math.Ceiling( numberOfSources / 32f );
            Registers.StartOfTriggerTypeArray.Define32Many(this, numberOfTriggerTypeRegisters, (register, index) =>
            {
                // Every interrupt source occupies 1 bit.
                register.WithFlags(0, 32, mode: FieldMode.Read, valueProviderCallback: (_, __) => IsEdgeTriggered);
            }, name: "Trigger type array");
        }

        private const bool IsEdgeTriggered = false;

        private enum Registers : long
        {
            StartOfTriggerTypeArray = 0x1080,
        }
    }
}
