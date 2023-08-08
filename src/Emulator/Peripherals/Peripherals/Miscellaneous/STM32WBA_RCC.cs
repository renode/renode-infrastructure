//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32WBA_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32WBA_RCC(IMachine machine, IHasFrequency nvic = null, IHasFrequency lptim1 = null, IHasFrequency lptim2 = null,
            long lsiFrequency = DefaultLsiFrequency, long lseFrequency = DefaultLseFrequency, long hseFrequency = DefaultHseFreqeuency) : base(machine)
        {
            this.nvic = nvic;
            this.lptim1 = lptim1;
            this.lptim2 = lptim2;
            this.lsiFrequency = lsiFrequency;
            this.lseFrequency = lseFrequency;
            this.hseFrequency = hseFrequency;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            UpdateClocks();
        }

        public long Size => 0x400;

        private static void TrySetFrequency(IHasFrequency timer, long frequency)
        {
            if(timer != null)
            {
                timer.Frequency = frequency;
            }
        }

        private void UpdateClocks()
        {
            TrySetFrequency(nvic, SystemClock);
            TrySetFrequency(lptim1, LpTimer1Clock);
            TrySetFrequency(lptim2, LpTimer2Clock);
        }

        private void DefineRegisters()
        {
            // Keep in mind that most of these registers do not affect other
            // peripherals or their clocks.
            Registers.ClockControl.Define(this, 0x500)
                .WithReservedBits(0, 8)
                .WithFlag(8, out var hsi16ClockEnable, name: "HSION")
                .WithFlag(9, name: "HSIKERON")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => hsi16ClockEnable.Value, name: "HSIRDY")
                .WithReservedBits(11, 5)
                .WithFlag(16, out var hseClockEnable, name: "HSEON")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseClockEnable.Value, name: "HSERDY")
                .WithReservedBits(18, 1)
                .WithTaggedFlag("HSECSSON", 19)
                .WithFlag(20, out hsePrescaler, name: "HSEPRE")
                .WithReservedBits(21, 3)
                .WithFlag(24, out var pll1Enable, name: "PLL1ON")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pll1Enable.Value)
                .WithReservedBits(26, 6)
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.InternalClockSourcesCalibration3.Define(this, 0x100000)
                .WithTag("HSICAL", 0, 12)
                .WithReservedBits(12, 4)
                .WithTag("HSITRIM", 16, 5)
                .WithReservedBits(21, 11)
                ;

            Registers.ClockConfiguration1.Define(this)
                .WithEnumField(0, 2, out systemClockSwitch, name: "SW")
                .WithEnumField<DoubleWordRegister, SystemClockSource>(2, 2, FieldMode.Read, name: "SWS",
                    valueProviderCallback: _ => systemClockSwitch.Value)
                .WithReservedBits(4, 20)
                .WithValueField(24, 4, name: "MCOSEL")
                .WithValueField(28, 3, name: "MCOPRE")
                .WithReservedBits(31, 1)
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.ClockConfiguration2.Define(this)
                .WithValueField(0, 3, out ahbPrescaler, name: "HPRE")
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, out apb1Prescaler, name: "PPRE1")
                .WithReservedBits(7, 1)
                .WithValueField(8, 3, out apb2Prescaler, name: "PPRE2")
                .WithReservedBits(11, 21)
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.ClockConfiguration3.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 3, out apb7Prescaler, name: "PPRE7")
                .WithReservedBits(7, 25)
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.Pll1Configuration.Define(this)
                .WithEnumField(0, 2, out pll1Source, name: "PLL1SRC")
                .WithValueField(2, 2, name: "PLL1RGE")
                .WithFlag(4, name: "PLL1FRACEN")
                .WithReservedBits(5, 3)
                .WithValueField(8, 3, out pll1Prescaler, name: "PLL1M")
                .WithReservedBits(11, 5)
                .WithFlag(16, name: "PLL1PEN")
                .WithFlag(17, name: "PLL1QEN")
                .WithFlag(18, name: "PLL1REN")
                .WithReservedBits(19, 1)
                .WithFlag(20, out var pll1SysclkDivide, name: "PLL1RCLKPRE") // 1 - divided
                .WithFlag(21, name: "PLL1RCLKPRESTEP")
                .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => !pll1SysclkDivide.Value, name: "PLL1RCLKPRERDY") // 1 - not divided
                .WithReservedBits(23, 9)
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.Pll1Dividers.Define(this, 0x01010280)
                .WithValueField(0, 9, out pll1Multiplier, name: "PLL1N")
                .WithValueField(9, 7, out pll1DividerP, name: "PLL1P")
                .WithValueField(16, 7, out pll1DividerQ, name: "PLL1Q")
                .WithReservedBits(23, 1)
                .WithValueField(24, 7, out pll1DividerR, name: "PLL1R")
                .WithReservedBits(31, 1)
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.Pll1FractionalDivider.Define(this)
                .WithReservedBits(0, 3)
                .WithTag("PLL1FRACN", 3, 13)
                .WithReservedBits(16, 16)
                ;

            Registers.ClockInterruptEnable.Define(this)
                .WithFlag(0, out var lsi1ReadyInterruptEnable, name: "LSI1RDYIE")
                .WithFlag(1, out var lseReadyInterruptEnable, name: "LSERDYIE")
                .WithReservedBits(2, 1)
                .WithFlag(3, out var hsiReadyInterruptEnable, name: "HSIRDYIE")
                .WithFlag(4, out var hseReadyInterruptEnable, name: "HSERDYIE")
                .WithReservedBits(5, 1)
                .WithFlag(6, out var pll1ReadyInterruptEnable, name: "PLL1RDYIE")
                .WithReservedBits(7, 25)
                ;

            Registers.ClockInterruptFlag.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => lsi1ReadyInterruptEnable.Value, name: "LSI1RDYF")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lseReadyInterruptEnable.Value, name: "LSERDYF")
                .WithReservedBits(2, 1)
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => hsiReadyInterruptEnable.Value, name: "HSIRDYF")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => hseReadyInterruptEnable.Value, name: "HSERDYF")
                .WithReservedBits(5, 1)
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => pll1ReadyInterruptEnable.Value, name: "PLL1RDYF")
                .WithReservedBits(7, 3)
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "HSECSSF")
                .WithReservedBits(11, 21)
                ;

            Registers.ClockInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { lsi1ReadyInterruptEnable.Value = false; } }, name: "LSI1RDYC")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if(value) { lseReadyInterruptEnable.Value = false; } }, name: "LSERDYC")
                .WithReservedBits(2, 1)
                .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if(value) { hsiReadyInterruptEnable.Value = false; } }, name: "HSIRDYC")
                .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if(value) { hseReadyInterruptEnable.Value = false; } }, name: "HSERDYC")
                .WithReservedBits(5, 1)
                .WithFlag(6, FieldMode.Write, writeCallback: (_, value) => { if(value) { pll1ReadyInterruptEnable.Value = false; } }, name: "PLL1RDYC")
                .WithReservedBits(7, 3)
                .WithFlag(10, FieldMode.Write, name: "HSECSSC")
                .WithReservedBits(11, 21)
                ;

            Registers.Ahb1PeripheralReset.Define(this)
                .WithTaggedFlag("GPDMA1RST", 0)
                .WithReservedBits(1, 11)
                .WithTaggedFlag("CRCRST", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("TSCRST", 16)
                .WithReservedBits(17, 15)
                ;

            Registers.Ahb2PeripheralReset.Define(this)
                .WithTaggedFlag("GPIOARST", 0)
                .WithTaggedFlag("GPIOBRST", 1)
                .WithTaggedFlag("GPIOCRST", 2)
                .WithReservedBits(3, 4)
                .WithTaggedFlag("GPIOHRST", 7)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("AESRST", 16)
                .WithTaggedFlag("HASHRST", 17)
                .WithTaggedFlag("RNGRST", 18)
                .WithTaggedFlag("SAESRST", 19)
                .WithTaggedFlag("HSEMRST", 20)
                .WithTaggedFlag("PKARST", 21)
                .WithReservedBits(22, 10)
                ;

            Registers.Ahb4PeripheralReset.Define(this)
                .WithReservedBits(0, 5)
                .WithTaggedFlag("ADC4RST", 5)
                .WithReservedBits(6, 26)
                ;

            Registers.Ahb5PeripheralReset.Define(this)
                .WithTaggedFlag("RADIORST", 0)
                .WithReservedBits(1, 31)
                ;

            Registers.Apb1PeripheralReset1.Define(this)
                .WithTaggedFlag("TIM2RST", 0)
                .WithTaggedFlag("TIM3RST", 1)
                .WithReservedBits(2, 15)
                .WithTaggedFlag("USART2RST", 17)
                .WithReservedBits(18, 3)
                .WithTaggedFlag("I2C1RST", 21)
                .WithReservedBits(22, 10)
                ;

            Registers.Apb1PeripheralReset2.Define(this)
                .WithReservedBits(0, 5)
                .WithTaggedFlag("LPTIM2RST", 5)
                .WithReservedBits(6, 26)
                ;

            Registers.Apb2PeripheralReset.Define(this)
                .WithReservedBits(0, 11)
                .WithTaggedFlag("TIM1RST", 11)
                .WithTaggedFlag("SPI1RST", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1RST", 14)
                .WithReservedBits(15, 2)
                .WithTaggedFlag("TIM16RST", 17)
                .WithTaggedFlag("TIM17RST", 18)
                .WithReservedBits(19, 13)
                ;

            Registers.Apb7PeripheralReset.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("SYSCFGRST", 1)
                .WithReservedBits(2, 3)
                .WithTaggedFlag("SPI3RST", 5)
                .WithTaggedFlag("LPUART1RST", 6)
                .WithTaggedFlag("I2C3RST", 7)
                .WithTaggedFlag("LPTIM1RST", 11)
                .WithReservedBits(12, 20)
                ;

            Registers.Ahb1PeripheralClockEnable.Define(this, 0x80000100)
                .WithFlag(0, name: "GPDMA1EN")
                .WithReservedBits(1, 7)
                .WithFlag(8, name: "FLASHEN")
                .WithReservedBits(9, 3)
                .WithFlag(12, name: "CRCEN")
                .WithReservedBits(13, 3)
                .WithFlag(16, name: "TSCEN")
                .WithFlag(17, name: "RAMCFGEN")
                .WithReservedBits(18, 6)
                .WithFlag(24, name: "GTZC1EN")
                .WithReservedBits(25, 6)
                .WithFlag(31, name: "SRAM1EN")
                ;

            Registers.Ahb2PeripheralClockEnable.Define(this, 0x40000000)
                .WithFlag(0, name: "GPIOAEN")
                .WithFlag(1, name: "GPIOBEN")
                .WithFlag(2, name: "GPIOCEN")
                .WithReservedBits(3, 4)
                .WithFlag(7, name: "GPIOHEN")
                .WithReservedBits(8, 8)
                .WithFlag(16, name: "AESEN")
                .WithFlag(17, name: "HASHEN")
                .WithFlag(18, name: "RNGEN")
                .WithFlag(19, name: "SAESEN")
                .WithFlag(20, name: "HSEMEN")
                .WithFlag(21, name: "PKAEN")
                .WithReservedBits(22, 8)
                .WithFlag(30, name: "SRAM2EN")
                .WithReservedBits(31, 1)
                ;

            Registers.Ahb4PeripheralClockEnable.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, name: "PWREN")
                .WithReservedBits(3, 2)
                .WithFlag(5, name: "ADC4EN")
                .WithReservedBits(6, 26)
                ;

            Registers.Ahb5PeripheralClockEnable.Define(this)
                .WithFlag(0, name: "RADIOEN")
                .WithReservedBits(1, 31)
                ;

            Registers.Apb1PeripheralClockEnable1.Define(this)
                .WithFlag(0, name: "TIM2EN")
                .WithFlag(1, name: "TIM3EN")
                .WithReservedBits(2, 9)
                .WithFlag(11, name: "WWDGEN")
                .WithReservedBits(12, 5)
                .WithFlag(17, name: "USART2EN")
                .WithReservedBits(18, 3)
                .WithFlag(21, name: "I2C1EN")
                .WithReservedBits(22, 10)
                ;

            Registers.Apb1PeripheralClockEnable2.Define(this)
                .WithReservedBits(0, 5)
                .WithFlag(5, name: "LPTIM2EN")
                .WithReservedBits(6, 26)
                ;

            Registers.Apb2PeripheralClockEnable.Define(this)
                .WithReservedBits(0, 11)
                .WithFlag(11, name: "TIM1EN")
                .WithFlag(12, name: "SPI1EN")
                .WithReservedBits(13, 1)
                .WithFlag(14, name: "USART1EN")
                .WithReservedBits(15, 2)
                .WithFlag(17, name: "TIM16EN")
                .WithFlag(18, name: "TIM17EN")
                .WithReservedBits(19, 13)
                ;

            Registers.Apb7PeripheralClockEnable.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, name: "SYSCFGEN")
                .WithReservedBits(2, 3)
                .WithFlag(5, name: "SPI3EN")
                .WithFlag(6, name: "LPUART1EN")
                .WithFlag(7, name: "I2C3EN")
                .WithReservedBits(8, 3)
                .WithFlag(11, name: "LPTIM1EN")
                .WithReservedBits(12, 9)
                .WithFlag(21, name: "RTCAPBEN")
                .WithReservedBits(22, 10)
                ;

            Registers.Ahb1PeripheralClockEnableInSleepMode.Define(this, 0xffffffff)
                .WithFlag(0, name: "GPDMA1SMEN")
                .WithReservedBits(1, 7)
                .WithFlag(8, name: "FLASHSMEN")
                .WithReservedBits(9, 3)
                .WithFlag(12, name: "CRCSMEN")
                .WithReservedBits(13, 3)
                .WithFlag(16, name: "TSCSMEN")
                .WithFlag(17, name: "RAMCFGSMEN")
                .WithReservedBits(18, 6)
                .WithFlag(24, name: "GTZC1SMEN")
                .WithReservedBits(25, 4)
                .WithFlag(29, name: "ICACHESMEN")
                .WithReservedBits(30, 1)
                .WithFlag(31, name: "SRAM1SMEN")
                ;

            Registers.Ahb2PeripheralClockEnableInSleepMode.Define(this, 0xffffffff)
                .WithFlag(0, name: "GPIOASMEN")
                .WithFlag(1, name: "GPIOBSMEN")
                .WithFlag(2, name: "GPIOCSMEN")
                .WithReservedBits(3, 4)
                .WithFlag(7, name: "GPIOHSMEN")
                .WithReservedBits(8, 8)
                .WithFlag(16, name: "AESSMEN")
                .WithFlag(17, name: "HASHSMEN")
                .WithFlag(18, name: "RNGSMEN")
                .WithFlag(19, name: "SAESSMEN")
                .WithReservedBits(20, 1)
                .WithFlag(21, name: "PKASMEN")
                .WithReservedBits(22, 8)
                .WithFlag(30, name: "SRAM2SMEN")
                .WithReservedBits(31, 1)
                ;

            Registers.Ahb4PeripheralClockEnableInSleepMode.Define(this, 0xffffffff)
                .WithReservedBits(0, 2)
                .WithFlag(2, name: "PWRSMEN")
                .WithReservedBits(3, 2)
                .WithFlag(5, name: "ADC4SMEN")
                .WithReservedBits(6, 26)
                ;

            Registers.Ahb5PeripheralClockEnableInSleepMode.Define(this, 0xffffffff)
                .WithFlag(0, name: "RADIOSMEN")
                .WithReservedBits(1, 31)
                ;

            Registers.Apb1PeripheralClockEnableInSleepMode1.Define(this)
                .WithFlag(0, name: "TIM2SMEN")
                .WithFlag(1, name: "TIM3SMEN")
                .WithReservedBits(2, 9)
                .WithFlag(11, name: "WWDGSMEN")
                .WithReservedBits(12, 5)
                .WithFlag(17, name: "USART2SMEN")
                .WithReservedBits(18, 3)
                .WithFlag(21, name: "I2C1SMEN")
                .WithReservedBits(22, 10)
                ;

            Registers.Apb1PeripheralClockEnableInSleepMode2.Define(this)
                .WithReservedBits(0, 5)
                .WithFlag(5, name: "LPTIM2SMEN")
                .WithReservedBits(6, 26)
                ;

            Registers.Apb2PeripheralClockEnableInSleepMode.Define(this, 0xffffffff)
                .WithReservedBits(0, 11)
                .WithFlag(11, name: "TIM1SMEN")
                .WithFlag(12, name: "SPI1SMEN")
                .WithReservedBits(13, 1)
                .WithFlag(14, name: "USART1SMEN")
                .WithReservedBits(15, 2)
                .WithFlag(17, name: "TIM16SMEN")
                .WithFlag(18, name: "TIM17SMEN")
                .WithReservedBits(19, 13)
                ;

            Registers.Apb7PeripheralClockEnableInSleepMode.Define(this, 0xffffffff)
                .WithReservedBits(0, 1)
                .WithFlag(1, name: "SYSCFGSMEN")
                .WithReservedBits(2, 3)
                .WithFlag(5, name: "SPI3SMEN")
                .WithFlag(6, name: "LPUART1SMEN")
                .WithFlag(7, name: "I2C3SMEN")
                .WithReservedBits(8, 3)
                .WithFlag(11, name: "LPTIM1SMEN")
                .WithReservedBits(12, 9)
                .WithFlag(21, name: "RTCAPBSMEN")
                .WithReservedBits(22, 10)
                ;

            Registers.PeripheralsIndependentClockConfiguration1.Define(this)
                .WithValueField(0, 2, name: "USART1SEL")
                .WithValueField(2, 2, name: "USART2SEL")
                .WithReservedBits(4, 6)
                .WithValueField(10, 2, name: "I2C1SEL")
                .WithReservedBits(12, 6)
                .WithEnumField(18, 2, out lpTimer2Clock, name: "LPTIM2SEL")
                .WithValueField(20, 2, name: "SPI1SEL")
                .WithTag("SYSTICKSEL", 22, 2)
                .WithReservedBits(24, 7)
                .WithFlag(31, name: "TIMICSEL")
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.PeripheralsIndependentClockConfiguration2.Define(this)
                .WithReservedBits(0, 12)
                .WithValueField(12, 2, name: "RNGSEL")
                .WithReservedBits(14, 8)
                ;

            Registers.PeripheralsIndependentClockConfiguration3.Define(this)
                .WithValueField(0, 2, name: "LPUART1SEL")
                .WithReservedBits(2, 1)
                .WithValueField(3, 2, name: "SPI3SEL")
                .WithReservedBits(5, 1)
                .WithValueField(6, 2, name: "I2C3SEL")
                .WithReservedBits(8, 2)
                .WithEnumField(10, 2, out lpTimer1Clock, name: "LPTIM1SEL")
                .WithValueField(12, 3, name: "ADCSEL")
                .WithReservedBits(15, 17)
                .WithChangeCallback((_, __) => UpdateClocks());
                ;

            Registers.BackupDomainControl1.Define(this, 0x8)
                .WithFlag(0, out var lseEnable, name: "LSEON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lseEnable.Value, name: "LSERDY")
                .WithFlag(2, name: "LSEBYP")
                .WithValueField(3, 2, name: "LSEDRV")
                .WithFlag(5, name: "LSECSSON")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => false, name: "LSECSSD")
                .WithFlag(7, out var lseSystemClockEnable, name: "LSESYSEN")
                .WithValueField(8, 2, name: "RTCSEL")
                .WithReservedBits(10, 1)
                .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => lseSystemClockEnable.Value, name: "LSESYSRDY")
                .WithFlag(12, name: "LSEGFON")
                .WithValueField(13, 2, name: "LSETRIM")
                .WithReservedBits(15, 1)
                .WithFlag(16, name: "BDRST")
                .WithReservedBits(17, 1)
                .WithTag("RADIOSTSEL", 18, 2)
                .WithReservedBits(20, 4)
                .WithFlag(24, name: "LSCOEN")
                .WithFlag(25, name: "LSCOSEL")
                .WithFlag(26, out var lsi1Enable, name: "LSI1ON")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => lsi1Enable.Value, name: "LSI1RDY")
                .WithFlag(28, name: "LSI1PREDIV")
                .WithReservedBits(29, 3)
                ;

            Registers.ControlStatus.Define(this, 0xc000000)
                .WithReservedBits(0, 23)
                .WithTaggedFlag("RMVF", 23)
                .WithReservedBits(24, 1)
                .WithTaggedFlag("OBLRSTF", 25)
                .WithTaggedFlag("PINRSTF", 26)
                .WithTaggedFlag("BORRSTF", 27)
                .WithTaggedFlag("SFTRSTF", 28)
                .WithTaggedFlag("IWDGRSTF", 29)
                .WithTaggedFlag("WWDGRSTF", 30)
                .WithTaggedFlag("LPWRRSTF", 31)
                ;

            Registers.BackupDomainControl2.Define(this)
                .WithValueField(0, 3, name: "LSI2MODE")
                .WithReservedBits(3, 1)
                .WithValueField(4, 4, name: "LSI2CFG")
                .WithReservedBits(8, 24)
                ;

            Registers.SecureConfiguration.Define(this)
                .WithFlag(0, name: "HSISEC")
                .WithFlag(1, name: "HSESEC")
                .WithReservedBits(2, 1)
                .WithFlag(3, name: "LSISEC")
                .WithFlag(4, name: "LSESEC")
                .WithFlag(5, name: "SYSCLKSEC")
                .WithFlag(6, name: "PRESCSEC")
                .WithFlag(7, name: "PLL1SEC")
                .WithReservedBits(8, 4)
                .WithFlag(12, name: "RMVFSEC")
                .WithReservedBits(13, 19)
                ;

            Registers.PrivilegeConfiguration.Define(this)
                .WithFlag(0, name: "SPRIV")
                .WithFlag(1, name: "NSPRIV")
                .WithReservedBits(2, 30)
                ;

            Registers.ClockConfiguration4.Define(this, 0x10)
                .WithValueField(0, 3, name: "HPRE5")
                .WithReservedBits(3, 1)
                .WithFlag(4, name: "HDIV5")
                .WithReservedBits(5, 27)
                ;

            Registers.RadioPeripheralClockEnable.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, out var basebandClockEnable, name: "BBCLKEN")
                .WithReservedBits(2, 14)
                .WithTaggedFlag("STRADIOCLKON", 16)
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => basebandClockEnable.Value, name: "RADIOCLKRDY")
                .WithReservedBits(18, 14)
                ;

            Registers.ExternalClockSourcesCalibration.Define(this, 0x200000)
                .WithReservedBits(0, 16)
                .WithValueField(16, 6, name: "HSETRIM")
                .WithReservedBits(22, 10)
                ;

        }

        private long PrescaleApbAhb(long input, IValueRegisterField prescaler)
        {
            var ppre = (int)prescaler.Value;
            // 0xx - no division
            if((ppre & 4) == 0)
            {
                return input;
            }
            // 1xx - divided by 2^(xx + 1), i.e. 00 -> 2, 01 -> 4, ...
            var logDivisor = (ppre & 3) + 1;
            return input >> logDivisor;
        }

        private long GetLpTimerClock(LpTimerClockSource source, long apbFrequency)
        {
            switch(source)
            {
                case LpTimerClockSource.Apb:
                    return apbFrequency;
                case LpTimerClockSource.Lsi:
                    return lsiFrequency;
                case LpTimerClockSource.Hsi16:
                    return Hsi16Frequency;
                case LpTimerClockSource.Lse:
                    return lseFrequency;
                default:
                    throw new ArgumentException("Unreachable: Invalid LpTimer clock source");
            }
        }

        // Clock tree
        private long DividedHse32 => hseFrequency / (hsePrescaler.Value ? 2 : 1);
        private long Pll1Source
        {
            get
            {
                switch(pll1Source.Value)
                {
                    default:
                        this.Log(LogLevel.Warning, "PLL1 frequency was required without a valid PLL source configured");
                        goto case PllEntryClockSource.Hsi16;
                    case PllEntryClockSource.None:
                        return 0;
                    case PllEntryClockSource.DividedHse32:
                        return DividedHse32;
                    case PllEntryClockSource.Hsi16:
                        return Hsi16Frequency;
                }
            }
        }
        private long Pll1VcoInput => Pll1Source / ((long)pll1Prescaler.Value + 1);
        private long Pll1VcoOutput => Pll1VcoInput * ((long)pll1Multiplier.Value + 1);
        private long Pll1Pclk => Pll1VcoOutput / ((long)pll1DividerP.Value + 1);
        private long Pll1Qclk => Pll1VcoOutput / ((long)pll1DividerQ.Value + 1);
        private long Pll1Rclk => Pll1VcoOutput / ((long)pll1DividerR.Value + 1);
        private long SystemClock
        {
            get
            {
                switch(systemClockSwitch.Value)
                {
                    default:
                    case SystemClockSource.Hsi16:
                        return Hsi16Frequency;
                    case SystemClockSource.DividedHse32:
                        return DividedHse32;
                    case SystemClockSource.Pll1Rclk:
                        return Pll1Rclk;
                }
            }
        }
        private long AhbClock => PrescaleApbAhb(SystemClock, ahbPrescaler); // hclk1
        private long Apb1Clock => PrescaleApbAhb(AhbClock, apb1Prescaler); // pclk1
        private long Apb2Clock => PrescaleApbAhb(AhbClock, apb2Prescaler); // pclk2
        private long Apb7Clock => PrescaleApbAhb(AhbClock, apb7Prescaler); // pclk7
        private long LpTimer1Clock => GetLpTimerClock(lpTimer1Clock.Value, Apb7Clock);
        private long LpTimer2Clock => GetLpTimerClock(lpTimer2Clock.Value, Apb1Clock);

        private IFlagRegisterField hsePrescaler;
        private IEnumRegisterField<SystemClockSource> systemClockSwitch;
        private IValueRegisterField ahbPrescaler;
        private IValueRegisterField apb1Prescaler;
        private IValueRegisterField apb2Prescaler;
        private IValueRegisterField apb7Prescaler;
        private IEnumRegisterField<PllEntryClockSource> pll1Source;
        private IValueRegisterField pll1Prescaler; // PLL1M
        private IValueRegisterField pll1Multiplier; // PLL1N
        private IValueRegisterField pll1DividerP; // PLL1P
        private IValueRegisterField pll1DividerQ; // PLL1Q
        private IValueRegisterField pll1DividerR; // PLL1R
        private IEnumRegisterField<LpTimerClockSource> lpTimer1Clock;
        private IEnumRegisterField<LpTimerClockSource> lpTimer2Clock;

        private readonly IHasFrequency nvic;
        private readonly IHasFrequency lptim1;
        private readonly IHasFrequency lptim2;
        private readonly long lsiFrequency;
        private readonly long lseFrequency;
        private readonly long hseFrequency;

        private const long DefaultLsiFrequency = 32000;
        private const long DefaultLseFrequency = 32768;
        private const long DefaultHseFreqeuency = 32000000;
        private const long Hsi16Frequency = 16000000;

        private enum PllEntryClockSource
        {
            None = 0,
            // 1 = reserved
            Hsi16 = 2,
            DividedHse32 = 3,
        }

        private enum SystemClockSource
        {
            Hsi16 = 0,
            // 1 = reserved
            DividedHse32 = 2,
            Pll1Rclk = 3,
        }

        private enum LpTimerClockSource
        {
            Apb = 0,
            Lsi = 1,
            Hsi16 = 2,
            Lse = 3,
        }

        private enum Registers
        {
            ClockControl = 0x0, // CR
            // gap intended
            InternalClockSourcesCalibration3 = 0x10, // ICSCR3
            // gap intended
            ClockConfiguration1 = 0x1c, // CFGR1
            ClockConfiguration2 = 0x20, // CFGR2
            ClockConfiguration3 = 0x24, // CFGR3
            Pll1Configuration = 0x28, // PLL1CFGR
            // gap intended
            Pll1Dividers = 0x34, // PLL1DIVR
            Pll1FractionalDivider = 0x38, // PLL1FRACR
            // gap intended
            ClockInterruptEnable = 0x50, // CIER
            ClockInterruptFlag = 0x54, // CIFR
            ClockInterruptClear = 0x58, // CICR
            // gap intended
            Ahb1PeripheralReset = 0x60, // AHB1RSTR
            Ahb2PeripheralReset = 0x64, // AHB2RSTR
            // gap intended
            Ahb4PeripheralReset = 0x6c, // AHB4RSTR
            Ahb5PeripheralReset = 0x70, // AHB5RSTR
            Apb1PeripheralReset1 = 0x74, // APB1RSTR1
            Apb1PeripheralReset2 = 0x78, // APB1RSTR2
            Apb2PeripheralReset = 0x7c, // APB2RSTR
            Apb7PeripheralReset = 0x80, // APB7RSTR
            // gap intended
            Ahb1PeripheralClockEnable = 0x88, // AHB1ENR
            Ahb2PeripheralClockEnable = 0x8c, // AHB2ENR
            // gap intended
            Ahb4PeripheralClockEnable = 0x94, // AHB4ENR
            Ahb5PeripheralClockEnable = 0x98, // AHB5ENR
            Apb1PeripheralClockEnable1 = 0x9c, // APB1ENR1
            Apb1PeripheralClockEnable2 = 0xa0, // APB1ENR2
            Apb2PeripheralClockEnable = 0xa4, // APB2ENR
            Apb7PeripheralClockEnable = 0xa8, // APB7ENR
            // gap intended
            Ahb1PeripheralClockEnableInSleepMode = 0xb0, // AHB1SMENR
            Ahb2PeripheralClockEnableInSleepMode = 0xb4, // AHB2SMENR
            // gap intended
            Ahb4PeripheralClockEnableInSleepMode = 0xbc, // AHB4SMENR
            Ahb5PeripheralClockEnableInSleepMode = 0xc0, // AHB5SMENR
            Apb1PeripheralClockEnableInSleepMode1 = 0xc4, // APB1SMENR1
            Apb1PeripheralClockEnableInSleepMode2 = 0xc8, // APB1SMENR2
            Apb2PeripheralClockEnableInSleepMode = 0xcc, // APB2SMENR
            Apb7PeripheralClockEnableInSleepMode = 0xd0, // APB7SMENR
            // gap intended
            PeripheralsIndependentClockConfiguration1 = 0xe0, // CCIPR1
            PeripheralsIndependentClockConfiguration2 = 0xe4, // CCIPR2
            PeripheralsIndependentClockConfiguration3 = 0xe8, // CCIPR3
            // gap intended
            BackupDomainControl1 = 0xf0, // BDCR1
            ControlStatus = 0xf4, // CSR
            BackupDomainControl2 = 0xf8, // BDCR2
            // gap intended
            SecureConfiguration = 0x110, // SECCFGR
            PrivilegeConfiguration = 0x114, // PRIVCFGR
            // gap intended
            ClockConfiguration4 = 0x200, // CFGR4
            // gap intended
            RadioPeripheralClockEnable = 0x208, // RADIOENR
            // gap intended
            ExternalClockSourcesCalibration = 0x210, // ECSCR1
        }
    }
}
