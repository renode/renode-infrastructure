//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32L0_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32L0_RCC(Machine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            // Keep in mind that these registers do not affect other
            // peripherals or their clocks.
            Registers.ClockControl.Define(this, 0x300)
                .WithFlag(0, out var hsi16on, name: "HSI16ON")
                .WithTaggedFlag("HSI16KERON", 1)
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => hsi16on.Value, name: "HSI16RDYF")
                .WithFlag(3, out var hsi16diven, name: "HSI16DIVEN")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => hsi16diven.Value, name: "HSI16DIVF")
                .WithTaggedFlag("HSI16OUTEN", 5)
                .WithReservedBits(6, 2)
                .WithFlag(8, out var msion, name: "MSION")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => msion.Value, name: "MSIRDY")
                .WithReservedBits(10, 6)
                .WithFlag(16, out var hseon, name: "HSEON")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseon.Value, name: "HSERDY")
                .WithTag("HSEBYP", 18, 1)
                .WithTag("CSSHSEON", 19, 1)
                .WithValueField(20, 2, name: "RTCPRE")
                .WithReservedBits(22, 2)
                .WithFlag(24, out var pllon, name: "PLLON")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pllon.Value, name: "PLLRDY")
                .WithReservedBits(26, 6)
                ;

            Registers.InternalClockSourcesCalibration.Define(this)
                .WithValueField(0, 8, name: "HSI16CAL")
                .WithValueField(8, 5, name: "HSI16TRIM")
                .WithValueField(13, 3, name: "MSIRANGE")
                .WithValueField(16, 8, name: "MSICAL")
                .WithValueField(24, 8, name: "MSITRIM")
                ;

            Registers.ClockRecoveryRc.Define(this)
                .WithFlag(0, out var hsi48on, name: "HSI48ON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => hsi48on.Value, name: "HSI48RDY")
                .WithTaggedFlag("HSI48DIV6EN", 2)
                .WithReservedBits(3, 5)
                .WithValueField(8, 8, name: "HSI48CAL")
                .WithReservedBits(16, 16)
                ;

            Registers.ClockConfigurationCfgr.Define(this)
                .WithValueField(0, 2, out var systemClockSwitch, name: "SW")
                .WithValueField(2, 2, FieldMode.Read, name: "SWS", valueProviderCallback: _ => systemClockSwitch.Value)
                .WithValueField(4, 4, name: "HPRE")
                .WithValueField(8, 3, name: "PPRE1")
                .WithValueField(11, 3, name: "PPRE2")
                .WithReservedBits(14, 1)
                .WithTaggedFlag("STOPWUCK", 15)
                .WithTaggedFlag("PLLSRC", 16)
                .WithReservedBits(17, 1)
                .WithValueField(18, 4, name: "PLLMUL")
                .WithValueField(22, 2, name: "PLLDIV")
                .WithValueField(24, 4, name: "MCOSEL")
                .WithValueField(28, 3, name: "MCOPRE")
                ;

            Registers.ClockInterruptEnable.Define(this)
                .WithTaggedFlag("LSIRDYIE", 0)
                .WithTaggedFlag("LSERDYIE", 1)
                .WithTaggedFlag("HSI16RDYIE", 2)
                .WithTaggedFlag("HSERDYIE", 3)
                .WithTaggedFlag("PLLRDYIE", 4)
                .WithTaggedFlag("MSIRDYIE", 5)
                .WithTaggedFlag("HSI48RDYIE", 6)
                .WithTaggedFlag("CSSLSE", 7)
                .WithReservedBits(8, 24)
                ;

            Registers.ClockInterruptFlag.Define(this)
                .WithTaggedFlag("LSIRDYF", 0)
                .WithTaggedFlag("LSERDYF", 1)
                .WithTaggedFlag("HSI16RDYF", 2)
                .WithTaggedFlag("HSERDYF", 3)
                .WithTaggedFlag("PLLRDYF", 4)
                .WithTaggedFlag("MSIRDYF", 5)
                .WithTaggedFlag("HSI48RDYF", 6)
                .WithTaggedFlag("CSSLSEF", 7)
                .WithTaggedFlag("CSSHSEF", 8)
                .WithReservedBits(9, 23)
                ;

            Registers.ClockInterruptClear.Define(this)
                .WithTaggedFlag("LSIRDYC", 0)
                .WithTaggedFlag("LSERDYC", 1)
                .WithTaggedFlag("HSI16RDYC", 2)
                .WithTaggedFlag("HSERDYC", 3)
                .WithTaggedFlag("PLLRDYC", 4)
                .WithTaggedFlag("MSIRDYC", 5)
                .WithTaggedFlag("HSI48RDYC", 6)
                .WithTaggedFlag("CSSLSEC", 7)
                .WithTaggedFlag("CSSHSEC", 8)
                .WithReservedBits(9, 23)
                ;

            Registers.IoPortReset.Define(this)
                .WithTaggedFlag("IOPARST", 0)
                .WithTaggedFlag("IOPBRST", 1)
                .WithTaggedFlag("IOPCRST", 2)
                .WithTaggedFlag("IOPDRST", 3)
                .WithTaggedFlag("IOPERST", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("IOPHRST", 7)
                ;

            Registers.AhbPeripheralReset.Define(this)
                .WithTaggedFlag("DMARST", 0)
                .WithReservedBits(1, 7)
                .WithTaggedFlag("MIFRST", 8)
                .WithReservedBits(9, 3)
                .WithTaggedFlag("CRCRST", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("TSCRST", 16)
                .WithReservedBits(17, 3)
                .WithTaggedFlag("RNGRST", 20)
                .WithReservedBits(21, 3)
                .WithTaggedFlag("CRYPTRST", 24)
                .WithReservedBits(25, 7)
                ;

            Registers.Apb2PeripheralReset.Define(this)
                .WithTaggedFlag("SYSCFGRST", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("TIM21RST", 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("TIM22RST", 5)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("ADCRST", 9)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("SPI1RST", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1RST", 14)
                .WithReservedBits(15, 7)
                .WithTaggedFlag("DBGRST", 22)
                .WithReservedBits(23, 9)
                ;

            Registers.Apb1PeripheralReset.Define(this)
                .WithTaggedFlag("TIM2RST", 0)
                .WithTaggedFlag("TIM3RST", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("TIM6RST", 4)
                .WithTaggedFlag("TIM7RST", 5)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("LCDRST", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("WWDGRST", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("SPI2RST", 14)
                .WithReservedBits(15, 2)
                .WithTaggedFlag("USART2RST", 17)
                .WithTaggedFlag("LPUART1RST", 18)
                .WithTaggedFlag("USART4RST", 19)
                .WithTaggedFlag("USART5RST", 20)
                .WithTaggedFlag("I2C1RST", 21)
                .WithTaggedFlag("I2C2RST", 22)
                .WithTaggedFlag("USBRST", 23)
                .WithReservedBits(24, 3)
                .WithTaggedFlag("CRSRST", 27)
                .WithTaggedFlag("PWRRST", 28)
                .WithTaggedFlag("DACRST", 29)
                .WithTaggedFlag("I2C3RST", 30)
                .WithTaggedFlag("LPTIM1RST", 31)
                ;

            Registers.IoPortClockEnable.Define(this)
                .WithTaggedFlag("IOPAEN", 0)
                .WithTaggedFlag("IOPBEN", 1)
                .WithTaggedFlag("IOPCEN", 2)
                .WithTaggedFlag("IOPDEN", 3)
                .WithTaggedFlag("IOPEEN", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("IOPHEN", 7)
                ;

            Registers.AhbPeripheralClockEnable.Define(this, 0x100)
                .WithTaggedFlag("DMAEN", 0)
                .WithReservedBits(1, 7)
                .WithTaggedFlag("MIFEN", 8)
                .WithReservedBits(9, 3)
                .WithTaggedFlag("CRCEN", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("TSCEN", 16)
                .WithReservedBits(17, 3)
                .WithTaggedFlag("RNGEN", 20)
                .WithReservedBits(21, 3)
                .WithTaggedFlag("CRYPEN", 24)
                .WithReservedBits(25, 7)
                ;

            Registers.Apb2PeripheralClockEnable.Define(this)
                .WithTaggedFlag("SYSCFEN", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("TIM21EN", 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("TIM22EN", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("FWEN", 7)
                .WithReservedBits(8, 1)
                .WithTaggedFlag("ADCEN", 9)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("SPI1EN", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1EN", 14)
                .WithReservedBits(15, 7)
                .WithTaggedFlag("DBGEN", 22)
                .WithReservedBits(23, 9)
                ;

            Registers.Apb1PeripheralClockEnable.Define(this)
                .WithTaggedFlag("TIM2EN", 0)
                .WithTaggedFlag("TIM3EN", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("TIM6EN", 4)
                .WithTaggedFlag("TIM7EN", 5)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("LCDEN", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("WWDGEN", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("SPI2EN", 14)
                .WithReservedBits(15, 2)
                .WithTaggedFlag("USART2EN", 17)
                .WithTaggedFlag("LPUART1EN", 18)
                .WithTaggedFlag("USART4EN", 19)
                .WithTaggedFlag("USART5EN", 20)
                .WithTaggedFlag("I2C1EN", 21)
                .WithTaggedFlag("I2C2EN", 22)
                .WithTaggedFlag("USBEN", 23)
                .WithReservedBits(24, 3)
                .WithTaggedFlag("CRSEN", 27)
                .WithTaggedFlag("PWREN", 28)
                .WithTaggedFlag("DACEN", 29)
                .WithTaggedFlag("I2C3EN", 30)
                .WithTaggedFlag("LPTIM1EN", 31)
                ;

            Registers.IoPortClockEnableInSleepMode.Define(this)
                .WithTaggedFlag("IOPASMEN", 0)
                .WithTaggedFlag("IOPBSMEN", 1)
                .WithTaggedFlag("IOPCSMEN", 2)
                .WithTaggedFlag("IOPDSMEN", 3)
                .WithTaggedFlag("IOPESMEN", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("IOPHSMEN", 7)
                ;

            Registers.AhbPeripheralClockEnableInSleepMode.Define(this, 0x100)
                .WithTaggedFlag("DMASMEN", 0)
                .WithReservedBits(1, 7)
                .WithTaggedFlag("MIFSMEN", 8)
                .WithReservedBits(9, 3)
                .WithTaggedFlag("CRCSMEN", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("TSCSMEN", 16)
                .WithReservedBits(17, 3)
                .WithTaggedFlag("RNGSMEN", 20)
                .WithReservedBits(21, 3)
                .WithTaggedFlag("CRYPSMEN", 24)
                .WithReservedBits(25, 7)
                ;

            Registers.Apb2PeripheralClockEnableInSleepMode.Define(this)
                .WithTaggedFlag("SYSCFSMEN", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("TIM21SMEN", 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("TIM22SMEN", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("FWSMEN", 7)
                .WithReservedBits(8, 1)
                .WithTaggedFlag("ADCSMEN", 9)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("SPI1SMEN", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1SMEN", 14)
                .WithReservedBits(15, 7)
                .WithTaggedFlag("DBGSMEN", 22)
                .WithReservedBits(23, 9)
                ;

            Registers.Apb1PeripheralClockEnableInSleepMode.Define(this)
                .WithTaggedFlag("TIM2SMEN", 0)
                .WithTaggedFlag("TIM3SMEN", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("TIM6SMEN", 4)
                .WithTaggedFlag("TIM7SMEN", 5)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("LCDSMEN", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("WWDGSMEN", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("SPI2SMEN", 14)
                .WithReservedBits(15, 2)
                .WithTaggedFlag("USART2SMEN", 17)
                .WithTaggedFlag("LPUART1SMEN", 18)
                .WithTaggedFlag("USART4SMEN", 19)
                .WithTaggedFlag("USART5SMEN", 20)
                .WithTaggedFlag("I2C1SMEN", 21)
                .WithTaggedFlag("I2C2SMEN", 22)
                .WithTaggedFlag("USBSMEN", 23)
                .WithReservedBits(24, 3)
                .WithTaggedFlag("CRSSMEN", 27)
                .WithTaggedFlag("PWRSMEN", 28)
                .WithTaggedFlag("DACSMEN", 29)
                .WithTaggedFlag("I2C3SMEN", 30)
                .WithTaggedFlag("LPTIM1SMEN", 31)
                ;

            Registers.ClockConfigurationCcipr.Define(this)
                .WithValueField(0, 2, name: "USART1SEL")
                .WithValueField(2, 2, name: "USART2SEL")
                .WithReservedBits(4, 6)
                .WithValueField(10, 2, name: "LPUART1SEL")
                .WithValueField(12, 2, name: "I2C1SEL")
                .WithReservedBits(14, 2)
                .WithValueField(16, 2, name: "I2C3SEL")
                .WithValueField(18, 2, name: "LPTIM1SEL")
                .WithReservedBits(20, 6)
                .WithTaggedFlag("HSI48SEL", 26)
                .WithReservedBits(27, 5)
                ;

            Registers.ControlStatus.Define(this, 0x0C000000)
                .WithFlag(0, out var lsion, name: "LSION")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lsion.Value, name: "LSIRDY")
                .WithReservedBits(2, 6)
                .WithFlag(8, out var lseon, name: "LSEON")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => lseon.Value, name: "LSERDY")
                .WithTaggedFlag("LSEBYP", 10)
                .WithValueField(11, 2, name: "LSEDRV")
                .WithTaggedFlag("CSSLSEON", 13)
                .WithTaggedFlag("CSSLSED", 14)
                .WithValueField(16, 2, name: "RTCSEL")
                .WithTaggedFlag("RTCEN", 18)
                .WithTaggedFlag("RTCRST", 19)
                .WithReservedBits(20, 3)
                .WithTaggedFlag("RMVF", 23)
                .WithTaggedFlag("FWRSTF", 24)
                .WithTaggedFlag("OBLRSTF", 25)
                .WithTaggedFlag("PINRSTF", 26)
                .WithTaggedFlag("PORRSTF", 27)
                .WithTaggedFlag("SFTRSTF", 28)
                .WithTaggedFlag("IWDGRSTF", 29)
                .WithTaggedFlag("WWDGRSTF", 30)
                .WithTaggedFlag("LPWRRSTF", 31)
                ;
        }

        private enum Registers
        {
            ClockControl = 0x0,
            InternalClockSourcesCalibration = 0x4,
            ClockRecoveryRc = 0x8,
            ClockConfigurationCfgr = 0xC,
            ClockInterruptEnable = 0x10,
            ClockInterruptFlag = 0x14,
            ClockInterruptClear = 0x18,
            IoPortReset = 0x1C,
            AhbPeripheralReset = 0x20,
            Apb2PeripheralReset = 0x24,
            Apb1PeripheralReset = 0x28,
            IoPortClockEnable = 0x2C,
            AhbPeripheralClockEnable = 0x30,
            Apb2PeripheralClockEnable = 0x34,
            Apb1PeripheralClockEnable = 0x38,
            IoPortClockEnableInSleepMode = 0x3C,
            AhbPeripheralClockEnableInSleepMode = 0x40,
            Apb2PeripheralClockEnableInSleepMode = 0x44,
            Apb1PeripheralClockEnableInSleepMode = 0x48,
            ClockConfigurationCcipr = 0x4C,
            ControlStatus = 0x50,
        }
    }
}
