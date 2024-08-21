//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class LPC_Clock0 : BasicDoubleWordPeripheral, IKnownSize
    {
        public LPC_Clock0(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.FreeRunningOscillatorControl.Define(this)
                .WithValueField(0, 16, out freeRunningOscillatorExpectedCount, name: "EXP_COUNT")
                .WithValueField(16, 5, name: "THRESH_RANGE_UP")
                .WithValueField(21, 5, name: "THRESH_RANGE_LOW")
                .WithReservedBits(26, 5)
                .WithFlag(31, name: "ENA_TUNE");

            Registers.FreeRunningOscillatorCapturedValue.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "CAPVAL",
                        valueProviderCallback: _ => freeRunningOscillatorExpectedCount.Value)
                .WithReservedBits(16, 15)
                // It's set to true at the end of each measurement cycle.
                // We set it on read, to handle situtaions, where software is waiting on new measurement.
                .WithFlag(31, out freeRunningOscillatorCapvalDataValid, FieldMode.ReadToSet, name: "DATA_VALID");

            Registers.FreeRunningOscillatorTrim.Define(this, 0x3bf)
                .WithValueField(0, 6, name: "TRIM",
                        writeCallback: (_, __) => freeRunningOscillatorCapvalDataValid.Value = false)
                .WithReservedBits(6, 26);

            Registers.FreeRunningOscillatorScTrim.Define(this)
                .WithValueField(0, 6, name: "TRIM")
                .WithReservedBits(6, 26);

            Registers.FreeRunningOscillatorClockStatus.Define(this)
                .WithFlag(0, name: "CLK_OK", valueProviderCallback: _ => true)
                .WithReservedBits(1, 31);

            Registers.FreeRunningOscillatorEnable.Define(this)
                .WithFlag(0, name: "FRO_DIV1_O_EN")
                .WithFlag(1, name: "FRO_DIV2_O_EN")
                .WithFlag(2, name: "FRO_DIV4_O_EN")
                .WithFlag(3, name: "FRO_DIV8_O_EN")
                .WithFlag(4, name: "FRO_DIV16_O_EN")
                .WithReservedBits(5, 27);

            Registers.SystemOscillatorControl0.Define(this)
                .WithFlag(0, name: "LP_ENABLE")
                .WithFlag(1, name: "BYPASS_ENABLE")
                .WithReservedBits(2, 30);

            Registers.OscillatorClockSource.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.LowPowerOscilatorControl0.Define(this)
                .WithReservedBits(0, 31)
                .WithFlag(31, name: "CLKRDY", valueProviderCallback: _ => true);

            Registers.SystemPll0ClockSelect.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.SystemPll0ClockControl0.Define(this)
                .WithTaggedFlag("BYPASS", 0)
                .WithTaggedFlag("RESET", 1)
                .WithReservedBits(2, 11)
                .WithFlag(13, name: "HOLDRINGOFF_ENA")
                .WithReservedBits(14, 2)
                .WithValueField(16, 8, name: "MULT")
                .WithReservedBits(24, 8);

            Registers.SystemPll0Numerator.Define(this)
                .WithValueField(0, 30, name: "NUM")
                .WithReservedBits(30, 2);

            Registers.SystemPll0Denominator.Define(this)
                .WithValueField(0, 30, name: "DENOM")
                .WithReservedBits(30, 2);

            Registers.SystemPll0Pfd.Define(this)
                .WithValueField(0, 6, name: "PFD0")
                .WithFlag(6, name: "PFD0_CLKRDY", valueProviderCallback: _ => true)
                .WithFlag(7, name: "PFD0_CLKGATE")
                .WithValueField(8, 6, name: "PFD1")
                .WithFlag(14, name: "PFD1_CLKRDY", valueProviderCallback: _ => true)
                .WithFlag(15, name: "PFD1_CLKGATE")
                .WithValueField(16, 6, name: "PFD2")
                .WithFlag(22, name: "PFD2_CLKRDY", valueProviderCallback: _ => true)
                .WithFlag(23, name: "PFD2_CLKGATE")
                .WithValueField(24, 6, name: "PFD3")
                .WithFlag(30, name: "PFD3_CLKRDY", valueProviderCallback: _ => true)
                .WithFlag(31, name: "PFD3_CLKGATE");

            Registers.Aux0PllClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.Aux1PllClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.SystemCpuAhbClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 23)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.MainClockSelectA.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.MainClockSelectB.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.HighSpeedUsbClockDivider0.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.HighSpeedUsbClockDivider1.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.FlexSpi0ClockSelect.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.FlexSpi0ClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.FlexSpi1ClockSelect.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.FlexSpi1ClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.Sdio0FunctionalClockSelect.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.Sdio0FunctionalClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.Sdio1FunctionalClockSelect.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.Sdio1FunctionalClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.Adc0ClockSelect0.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.Adc0ClockSelect1.Define(this)
                .WithValueField(0, 3, name: "SEL")
                .WithReservedBits(3, 29);

            Registers.Adc0ClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);

            Registers.SystickFunctionalClockDivider.Define(this)
                .WithValueField(0, 8, name: "DIV")
                .WithReservedBits(8, 21)
                .WithTaggedFlag("RESET", 29)
                .WithTaggedFlag("HALT", 30)
                .WithTaggedFlag("REQFLAG", 31);
        }

        private IValueRegisterField freeRunningOscillatorExpectedCount;
        private IFlagRegisterField freeRunningOscillatorCapvalDataValid;

        private enum Registers
        {
            FreeRunningOscillatorControl        = 0x080,
            FreeRunningOscillatorCapturedValue  = 0x084,
            FreeRunningOscillatorTrim           = 0x08c,
            FreeRunningOscillatorScTrim         = 0x090,
            FreeRunningOscillatorClockStatus    = 0x10c,
            FreeRunningOscillatorEnable         = 0x110,
            SystemOscillatorControl0            = 0x160,
            OscillatorClockSource               = 0x168,
            LowPowerOscilatorControl0           = 0x190,
            SystemPll0ClockSelect               = 0x200,
            SystemPll0ClockControl0             = 0x204,
            SystemPll0Numerator                 = 0x210,
            SystemPll0Denominator               = 0x214,
            SystemPll0Pfd                       = 0x218,
            Aux0PllClockDivider                 = 0x248,
            Aux1PllClockDivider                 = 0x24c,
            SystemCpuAhbClockDivider            = 0x400,
            MainClockSelectA                    = 0x430,
            MainClockSelectB                    = 0x434,
            HighSpeedUsbClockDivider0           = 0x500,
            HighSpeedUsbClockDivider1           = 0x504,
            FlexSpi0ClockSelect                 = 0x620,
            FlexSpi0ClockDivider                = 0x624,
            FlexSpi1ClockSelect                 = 0x630,
            FlexSpi1ClockDivider                = 0x634,
            Sdio0FunctionalClockSelect          = 0x680,
            Sdio0FunctionalClockDivider         = 0x684,
            Sdio1FunctionalClockSelect          = 0x690,
            Sdio1FunctionalClockDivider         = 0x694,
            Adc0ClockSelect0                    = 0x6d0,
            Adc0ClockSelect1                    = 0x6d4,
            Adc0ClockDivider                    = 0x6d8,
            SystickFunctionalClockDivider       = 0x764,
        }
    }
}
