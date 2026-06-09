//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_PLLDIG : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_PLLDIG(IMachine machine, S32K3XX_MC_CGM clockGenerationModule, ulong fxoscFrequency = 16_000_000) : base(machine)
        {
            this.clockGenerationModule = clockGenerationModule;
            dividerEnables = new IFlagRegisterField[2];
            dividers = new IValueRegisterField[2];
            this.fxoscFrequency = fxoscFrequency;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x88;

        private void UpdateFrequency()
        {
            // Divider registers use both 0 and 1 to denote 1 so normalize before doing any calculations
            var outputDivider2Value = outputDivider2.Value == 0 ? 1 : outputDivider2.Value;
            var inputPredividerValue = inputPredivider.Value == 0 ? 1 : inputPredivider.Value;
            var outputMultiplierValue = outputMultiplier.Value == 0 ? 1 : outputMultiplier.Value;
            var divider0Value = dividerEnables[0].Value ? (dividers[0].Value + 1) : 1;

            var vcoFreq = ((fxoscFrequency / inputPredividerValue) * outputMultiplierValue) / outputDivider2Value;
            var phi0Freq = vcoFreq / divider0Value;
            // Update peripherals which use the PLL, currently only the CGM
            clockGenerationModule.PhaseLockedLoop0Frequency = phi0Freq;
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this, 0x8000_0000)
                .WithTaggedFlag("PLLPD", 31)
                .WithReservedBits(0, 31);
            Registers.Status.Define(this, 0x0000_0300)
                .WithReservedBits(4, 28)
                .WithTaggedFlag("LOL", 3)
                .WithFlag(2, mode: FieldMode.Read, name: "LOCK", valueProviderCallback: _ => true) // Always show the status as locked and ready
                .WithReservedBits(0, 2);
            Registers.Divider.Define(this, 0x0C3F_1032)
                .WithReservedBits(31, 1)
                .WithValueField(25, 6, out outputDivider2, name: "ODIV2")
                .WithReservedBits(15, 10)
                .WithValueField(12, 3, out inputPredivider, name: "RDIV")
                .WithReservedBits(8, 4)
                .WithValueField(0, 8, out outputMultiplier, name: "MFI")
                .WithChangeCallback((_, _) => UpdateFrequency());
            Registers.FrequencyModulation.Define(this, 0x4000_000)
                .WithTag("PLLFM", 0, 32);
            Registers.FractionalDivider.Define(this)
                .WithReservedBits(31, 1)
                .WithTag("SDMEN", 28, 3)
                .WithReservedBits(15, 13)
                .WithTag("MFN", 0, 15);
            Registers.Calibration2.Define(this, 0x0006_0000)
                .WithTag("PLLCAL2", 0, 32);
            Registers.OutputDivider0.Define32Many(this, 2, (reg, i) =>
            {
                reg
                    .WithFlag(31, out dividerEnables[i], name: $"DE{i}")
                    .WithReservedBits(24, 7)
                    .WithValueField(16, 8, out dividers[i])
                    .WithReservedBits(0, 16)
                    .WithChangeCallback((_, _) => UpdateFrequency());
            });
        }

        private IValueRegisterField outputDivider2;
        private IValueRegisterField inputPredivider;
        private IValueRegisterField outputMultiplier;
        private readonly IFlagRegisterField[] dividerEnables;
        private readonly IValueRegisterField[] dividers;
        private readonly S32K3XX_MC_CGM clockGenerationModule;
        private readonly ulong fxoscFrequency;

        private enum Registers
        {
            Control = 0x00,
            Status = 0x04,
            Divider = 0x08,
            FrequencyModulation = 0x0C,
            FractionalDivider = 0x10,
            Calibration2 = 0x18,
            // Gap intentional
            OutputDivider0 = 0x80,
            OutputDivider1 = 0x84,
        }
    }
}