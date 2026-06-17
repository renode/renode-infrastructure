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
    /* This model is a stub to match the firmware needs: reading previously set values and reading
     * ready bits. It does not have any real impact on other devices.
     * For this reasons, the frequencies must be set in the repl file.
     */
    public sealed class STM32L5_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32L5_RCC(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.Control.Define(this, name: "Control")
                .WithFlag(16, out hseOn, name: "HSEON")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseOn.Value, name: "HSERDY")
                .WithFlag(24, out pllOn, name: "PLLON")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pllOn.Value, name: "PLLRDY")
                .WithFlag(26, out pllSAI1On, name: "PLLSAI1ON")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => pllSAI1On.Value, name: "PLLSAI1RDY");
            Registers.Configuration.Define(this, name: "Configuration")
                .WithValueField(0, 2, out systemClockSwitch, name: "SW")
                .WithValueField(2, 2, FieldMode.Read, valueProviderCallback: _ => systemClockSwitch.Value, name: "SWS")
                .WithValueField(4, 4, name: "HPRE")
                .WithValueField(8, 3, name: "PPRE1")
                .WithValueField(11, 3, name: "PPRE2");
            Registers.PLLConfiguration.Define(this, name: "PLL Configuration")
                .WithValueField(0, 2, name: "PLLSRC")
                .WithValueField(4, 4, name: "PLLM")
                .WithValueField(8, 7, name: "PLLN")
                .WithFlag(16, name: "PLLPEN")
                .WithFlag(17, name: "PLLP")
                .WithFlag(20, name: "PLLQEN")
                .WithValueField(21, 2, name: "PLLQ")
                .WithFlag(24, name: "PLLREN")
                .WithValueField(25, 2, name: "PLLR")
                .WithValueField(27, 5, name: "PLLPDIV");
            Registers.PLLSAI1Configuration.Define(this, name: "PLLSAI1 configuration");
            Registers.APB1PeripheralReset.Define(this, name: "APB1 peripheral reset");
            Registers.AHB1PeripheralClockEnable.Define(this, name: "AHB2 clock enable");
            Registers.AHB2PeripheralClockEnable.Define(this, name: "AHB2 clock enable");
            Registers.APB1PeripheralClockEnable.Define(this, name: "APB1 clock enable");
            Registers.APB1PeripheralClockEnable2.Define(this, name: "APB1 clock enable 2");
            Registers.APB2PeripheralClockEnable.Define(this, name: "APB2 clock enable");
            Registers.IndependentCLockConfiguration1.Define(this, name: "Independent clock configuration 1");
            Registers.ControlStatus.Define(this, name: "Control/Status")
                .WithFlag(0, out lsiOn, name: "LSION")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lsiOn.Value, name: "LSIRDY");
            Registers.ClockRecovery.Define(this, name: "Clock recovery")
                .WithFlag(0, out hsi48On, name: "HSI48ON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => hsi48On.Value, name: "HSI48RDY");
        }

        private IFlagRegisterField hseOn;
        private IFlagRegisterField pllOn;
        private IFlagRegisterField pllSAI1On;
        private IFlagRegisterField lsiOn;
        private IFlagRegisterField hsi48On;
        private IValueRegisterField systemClockSwitch;

        private enum Registers
        {
            Control = 0x0,
            Configuration = 0x8,
            PLLConfiguration = 0xC,
            PLLSAI1Configuration = 0x10,
            APB1PeripheralReset = 0x38,
            AHB1PeripheralClockEnable = 0x48,
            AHB2PeripheralClockEnable = 0x4C,
            APB1PeripheralClockEnable = 0x58,
            APB1PeripheralClockEnable2 = 0x5C,
            APB2PeripheralClockEnable = 0x60,
            IndependentCLockConfiguration1 = 0x88,
            ControlStatus = 0x94,
            ClockRecovery = 0x98,
        }
    }
}
