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
    public class STM32F0_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32F0_RCC(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.ClockControlRegister.Define(this, resetValue: 0x0000_0083)
                .WithTaggedFlag("HSION", 0)
                .WithTaggedFlag("HSIRDY", 1)
                .WithReservedBits(2, 1)
                .WithTag("HSITRIM", 3, 5)
                .WithTag("HSICAL", 8, 8)
                .WithFlag(16, out var hseEnabled, name: "HSEON")
                .WithFlag(17, FieldMode.Read, name: "HSERDY",
                    valueProviderCallback: _ => hseEnabled.Value)
                .WithTaggedFlag("HSEBYP", 18)
                .WithTaggedFlag("CSSON", 19)
                .WithReservedBits(20, 4)
                .WithFlag(24, out var pllEnabled, name: "PLLON")
                .WithFlag(25, FieldMode.Read, name: "PPLRDY",
                    valueProviderCallback: _ => pllEnabled.Value)
                .WithReservedBits(26, 6)
            ;

            Registers.ClockConfigurationRegister.Define(this)
                .WithValueField(0, 2, out var systemClockSwitch, name: "SW")
                .WithValueField(2, 2, FieldMode.Read, name: "SWS",
                    valueProviderCallback: _ => systemClockSwitch.Value)
                .WithTag("HPRE", 4, 4)
                .WithTag("PPRE", 8, 3)
                .WithReservedBits(11, 3)
                .WithTaggedFlag("ADCPRE", 14)
                .WithTag("PLLSRC", 15, 2)
                .WithTaggedFlag("PLLXTPRE", 17)
                .WithTag("PLLMUL", 18, 4)
                .WithReservedBits(22, 2)
                .WithTag("MCO", 24, 4)
                .WithTag("MCOPRE", 28, 3)
                .WithTaggedFlag("PLLNODIV", 31)
            ;

            Registers.ClockInterruptRegister.Define(this)
                .WithTaggedFlag("LSIRDYF", 0)
                .WithTaggedFlag("LSERDYF", 1)
                .WithTaggedFlag("HSIRDYF", 2)
                .WithTaggedFlag("HSERDYF", 3)
                .WithTaggedFlag("PLLRDYF", 4)
                .WithTaggedFlag("HSI14RDYF", 5)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("CSSF", 7)
                .WithTaggedFlag("LSIRDYIE", 8)
                .WithTaggedFlag("LSERDYIE", 9)
                .WithTaggedFlag("HSIRDYIE", 10)
                .WithTaggedFlag("HSERDYIE", 11)
                .WithTaggedFlag("PLLRDYIE", 12)
                .WithTaggedFlag("HSI14RDYIE", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("LSIRDYC", 16)
                .WithTaggedFlag("LSERDYC", 17)
                .WithTaggedFlag("HSIRDYC", 18)
                .WithTaggedFlag("HSERDYC", 19)
                .WithTaggedFlag("PLLRDYC", 20)
                .WithTaggedFlag("HSI14RDYC", 21)
                .WithReservedBits(22, 1)
                .WithTaggedFlag("CSSC", 23)
                .WithReservedBits(24, 8)
            ;

            Registers.APBPeripheralResetRegister2.Define(this)
                .WithTaggedFlag("SYSCFGCOMPRST", 0)
                .WithReservedBits(1, 4)
                .WithTaggedFlag("USART6RST", 5)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("ADCRST", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("TIM1RST", 11)
                .WithTaggedFlag("SPI1RST", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1RST", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("TIM15RST", 16)
                .WithTaggedFlag("TIM16RST", 17)
                .WithTaggedFlag("TIM17RST", 18)
                .WithReservedBits(19, 3)
                .WithTaggedFlag("DBGMCURST", 22)
                .WithReservedBits(23, 9)
            ;

            Registers.APBPeripheralResetRegister1.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("TIM3RST", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("TIM6RST", 4)
                .WithTaggedFlag("TIM7RST", 5)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("TIM14RST", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("WWDGRST", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("SPI2RST", 14)
                .WithReservedBits(15, 2)
                .WithTaggedFlag("USART2RST", 17)
                .WithTaggedFlag("USART3RST", 18)
                .WithTaggedFlag("USART4RST", 19)
                .WithTaggedFlag("USART5RST", 20)
                .WithTaggedFlag("I2C1RST", 21)
                .WithTaggedFlag("I2C2RST", 22)
                .WithTaggedFlag("USBRST", 23)
                .WithReservedBits(24, 4)
                .WithTaggedFlag("PWRRST", 28)
                .WithReservedBits(29, 3)
            ;

            Registers.AHBPeripheralClockEnableRegister.Define(this, resetValue: 0x0000_0014)
                .WithTaggedFlag("DMAEN", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("SRAMEN", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("FLITFEN", 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("CRCEN", 6)
                .WithReservedBits(7, 10)
                .WithTaggedFlag("GPIOAEN", 17)
                .WithTaggedFlag("GPIOBEN", 18)
                .WithTaggedFlag("GPIOCEN", 19)
                .WithTaggedFlag("GPIODEN", 20)
                .WithReservedBits(21, 1)
                .WithTaggedFlag("GPIOFEN", 22)
                .WithReservedBits(23, 9)
            ;

            Registers.APBPeripheralClockEnableRegister2.Define(this)
                .WithTaggedFlag("SYSCFGCOMPEN", 0)
                .WithReservedBits(1, 4)
                .WithTaggedFlag("USART6EN", 5)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("ADCEN", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("TIM1EN", 11)
                .WithTaggedFlag("SPI1EN", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1EN", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("TIM15EN", 16)
                .WithTaggedFlag("TIM16EN", 17)
                .WithTaggedFlag("TIM17EN", 18)
                .WithReservedBits(19, 3)
                .WithTaggedFlag("DBGMCUEN", 22)
                .WithReservedBits(23, 9)
            ;

            Registers.APBPeripheralClockEnableRegister1.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("TIM3EN", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("TIM6EN", 4)
                .WithTaggedFlag("TIM7EN", 5)
                .WithReservedBits(6, 2)
                .WithTaggedFlag("TIM14EN", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("WWDGEN", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("SPI2EN", 14)
                .WithReservedBits(15, 2)
                .WithTaggedFlag("USART2EN", 17)
                .WithTaggedFlag("USART3EN", 18)
                .WithTaggedFlag("USART4EN", 19)
                .WithTaggedFlag("USART5EN", 20)
                .WithTaggedFlag("I2C1EN", 21)
                .WithTaggedFlag("I2C2EN", 22)
                .WithTaggedFlag("USBEN", 23)
                .WithReservedBits(24, 4)
                .WithTaggedFlag("PWREN", 28)
                .WithReservedBits(29, 3)
            ;

            Registers.RTCDomainControlRegister.Define(this, resetValue: 0x0000_0018)
                .WithTaggedFlag("LSEON", 0)
                .WithTaggedFlag("LSERDY", 1)
                .WithTaggedFlag("LSEBYP", 2)
                .WithTag("LSEDRV", 3, 2)
                .WithReservedBits(5, 3)
                .WithTag("RTCSEL", 8, 2)
                .WithReservedBits(10, 5)
                .WithTaggedFlag("RTCEN", 15)
                .WithTaggedFlag("BDRST", 16)
                .WithReservedBits(17, 15)
            ;

            Registers.ControlStatusRegister.Define(this)
                .WithTaggedFlag("LSION", 0)
                .WithTaggedFlag("LSIRDY", 1)
                .WithReservedBits(2, 21)
                .WithTaggedFlag("V18PWRRSTF", 23)
                .WithTaggedFlag("RMVF", 24)
                .WithTaggedFlag("OBLRSTF", 25)
                .WithTaggedFlag("PINRSTF", 26)
                .WithTaggedFlag("PORRSTF", 27)
                .WithTaggedFlag("SFTRSTF", 28)
                .WithTaggedFlag("IWDGRSTF", 29)
                .WithTaggedFlag("WWDGRSTF", 30)
                .WithTaggedFlag("LPWRRSTF", 31)
            ;

            Registers.AHBPeripheralResetRegister.Define(this)
                .WithReservedBits(0, 17)
                .WithTaggedFlag("GPIOARST", 17)
                .WithTaggedFlag("GPIOBRST", 18)
                .WithTaggedFlag("GPIOCRST", 19)
                .WithTaggedFlag("GPIODRST", 20)
                .WithReservedBits(21, 1)
                .WithTaggedFlag("GPIOFRST", 22)
                .WithReservedBits(23, 9)
            ;

            Registers.ClockConfigurationRegister2.Define(this)
                .WithTag("PREDIV", 0, 4)
                .WithReservedBits(4, 28)
            ;

            Registers.ClockConfigurationRegister3.Define(this)
                .WithTag("USART1SW", 0, 2)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("I2C1SW", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("USBSW", 7)
                .WithTaggedFlag("ADCSW", 8)
                .WithReservedBits(9, 23)
            ;

            Registers.ClockControlRegister2.Define(this)
                .WithFlag(0, out var hsi14Enabled, name: "HSI14ON")
                .WithFlag(1, FieldMode.Read, name: "HSI14RDY",
                    valueProviderCallback: _ => hsi14Enabled.Value)
                .WithTaggedFlag("HSI14DIS", 2)
                .WithTag("HSI14TRIM", 3, 5)
                .WithTag("HSI14CAL", 8, 8)
                .WithReservedBits(16, 16)
            ;
        }

        public enum Registers
        {
            ClockControlRegister = 0x00,
            ClockConfigurationRegister = 0x04,
            ClockInterruptRegister = 0x08,
            APBPeripheralResetRegister2 = 0x0C,
            APBPeripheralResetRegister1 = 0x10,
            AHBPeripheralClockEnableRegister = 0x14,
            APBPeripheralClockEnableRegister2 = 0x18,
            APBPeripheralClockEnableRegister1 = 0x1C,
            RTCDomainControlRegister = 0x20,
            ControlStatusRegister = 0x24,
            AHBPeripheralResetRegister = 0x28,
            ClockConfigurationRegister2 = 0x2C,
            ClockConfigurationRegister3 = 0x30,
            ClockControlRegister2 = 0x34
        }
    }
}
