using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32F1_RCC : IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public STM32F1_RCC() : base()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ClockControl, new DoubleWordRegister(this, 0x00000483)
                    .WithFlag(0, out var hsion, name: "HSION")
                    .WithFlag(1, FieldMode.Read,
                        valueProviderCallback: _ => hsion.Value, name: "HSIRDY")
                    .WithReservedBits(2, 1)
                    .WithValueField(3, 5, name: "HSITRIM")
                    .WithTag("HSICAL", 8, 8)
                    .WithFlag(16, out var hseon, name: "HSEON")
                    .WithFlag(17, FieldMode.Read,
                        valueProviderCallback: _ => hseon.Value, name: "HSERDY")
                    .WithTag("HSEBYP", 18, 1)
                    .WithTag("CSSON", 19, 1)
                    .WithReservedBits(20, 4)
                    .WithFlag(24, out var pllon, name: "PLLON")
                    .WithFlag(25, FieldMode.Read,
                        valueProviderCallback: _ => pllon.Value, name: "PLLRDY")
                    .WithReservedBits(26, 6)
                },
                {(long)Registers.ClockConfiguration, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out var systemClockSwitch, name: "SW")
                    .WithValueField(2, 2, FieldMode.Read, name: "SWS",
                        valueProviderCallback: _ => systemClockSwitch.Value)
                    .WithValueField(4, 4, name: "HPRE")
                    .WithValueField(8, 3, name: "PPRE1")
                    .WithValueField(11, 3, name: "PPRE2")
                    .WithValueField(14, 2, name: "ADCPRE")
                    .WithFlag(16, name: "PLLSRC")
                    .WithFlag(17, name: "PLLXTPRE")
                    .WithValueField(18, 4, name: "PLLMUL")
                    .WithFlag(22, name: "USBPRE")
                    .WithReservedBits(23, 1)
                    .WithValueField(24, 3, name: "MCO")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.ClockInterrupt, new DoubleWordRegister(this)
                    .WithFlag(0, name: "LSIRDYF")
                    .WithFlag(1, name: "LSERDYF")
                    .WithFlag(2, name: "HSIRDYF")
                    .WithFlag(3, name: "HSERDYF")
                    .WithFlag(4, name: "PLLRDYF")
                    .WithReservedBits(5, 2)
                    .WithFlag(7, name: "CSSF")
                    .WithFlag(8, name: "LSIRDYIE")
                    .WithFlag(9, name: "LSERDYIE")
                    .WithFlag(10, name: "HSIRDYIE")
                    .WithFlag(11, name: "HSERDYIE")
                    .WithFlag(12, name: "PLLRDYIE")
                    .WithReservedBits(13, 3)
                    .WithFlag(16, name: "LSIRDYC")
                    .WithFlag(17, name: "LSERDYC")
                    .WithFlag(18, name: "HSIRDYC")
                    .WithFlag(19, name: "HSERDYC")
                    .WithFlag(20, name: "PLLRDYC")
                    .WithReservedBits(21, 2)
                    .WithFlag(23, name: "CSSC")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.APB2PeripheralReset, new DoubleWordRegister(this)
                    .WithFlag(0, name: "AFIORST")
                    .WithReservedBits(1, 1)
                    .WithValueField(2, 7, name: "IOPxRST")
                    .WithValueField(9, 2, name: "ADCxRST")
                    .WithFlag(11, name: "TIM1RST")
                    .WithFlag(12, name: "SPI1RST")
                    .WithFlag(13, name: "TIM8RST")
                    .WithFlag(14, name: "USART1RST")
                    .WithFlag(15, name: "ADC3RST")
                    .WithReservedBits(16, 3)
                    .WithValueField(19, 3, name: "TIMxRST")
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.APB1PeripheralReset, new DoubleWordRegister(this)
                    .WithValueField(0, 9, name: "TIMxRST")
                    .WithReservedBits(9, 2)
                    .WithFlag(11, name: "WWDGRST")
                    .WithReservedBits(12, 2)
                    .WithValueField(14, 2, name: "SPIxRST")
                    .WithReservedBits(16, 1)
                    .WithValueField(17, 2, name: "USARTxRST")
                    .WithValueField(19, 2, name: "UARTxRST")
                    .WithValueField(21, 2, name: "I2CxRST")
                    .WithFlag(23, name: "USBRST")
                    .WithReservedBits(24, 1)
                    .WithFlag(25, name: "CANRST")
                    .WithReservedBits(26, 1)
                    .WithFlag(27, name: "BKPRST")
                    .WithFlag(28, name: "PWRRST")
                    .WithFlag(29, name: "DACRST")
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.AHBPeripheralClockEnable, new DoubleWordRegister(this, 0x00000014)
                    .WithValueField(0, 2, name: "DMAxEN")
                    .WithFlag(2, name: "SRAMEN")
                    .WithReservedBits(3, 1)
                    .WithFlag(4, name: "FLITFEN")
                    .WithReservedBits(5, 1)
                    .WithFlag(6, name: "CRCEN")
                    .WithReservedBits(7, 1)
                    .WithFlag(8, name: "FSMCEN")
                    .WithReservedBits(9, 1)
                    .WithFlag(10, name: "SDIOEN")
                    .WithReservedBits(11, 21)
                },
                {(long)Registers.APB2PeripheralClockEnable, new DoubleWordRegister(this)
                    .WithFlag(0, name: "AFIOEN")
                    .WithReservedBits(1, 1)
                    .WithValueField(2, 7, name: "IOPxEN")
                    .WithValueField(9, 2, name: "ADCxEN")
                    .WithFlag(11, name: "TIM1EN")
                    .WithFlag(12, name: "SPI1EN")
                    .WithFlag(13, name: "TIM8EN")
                    .WithFlag(14, name: "USART1EN")
                    .WithFlag(15, name: "ADC3EN")
                    .WithReservedBits(16, 3)
                    .WithValueField(19, 3, name: "TIMxEN")
                    .WithReservedBits(22, 10)
                },
                {(long)Registers.APB1PeripheralClockEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 9, name: "TIMxEN")
                    .WithReservedBits(9, 2)
                    .WithFlag(11, name: "WWDGEN")
                    .WithReservedBits(12, 2)
                    .WithValueField(14, 2, name: "SPIxEN")
                    .WithReservedBits(16, 1)
                    .WithValueField(17, 2, name: "USARTxEN")
                    .WithValueField(19, 2, name: "UARTxEN")
                    .WithValueField(21, 2, name: "I2CxEN")
                    .WithFlag(23, name: "USBEN")
                    .WithReservedBits(24, 1)
                    .WithFlag(25, name: "CANEN")
                    .WithReservedBits(26, 1)
                    .WithFlag(27, name: "BKPEN")
                    .WithFlag(28, name: "PWREN")
                    .WithFlag(29, name: "DACEN")
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.BackupDomainControl, new DoubleWordRegister(this)
                    .WithFlag(0, out var lseon, name: "LSEON")
                    .WithFlag(1, FieldMode.Read,
                        valueProviderCallback: _ => lseon.Value, name: "LSERDY")
                    .WithFlag(2, name: "LSEBYP")
                    .WithReservedBits(3, 5)
                    .WithValueField(8, 2, name: "RTCSEL")
                    .WithReservedBits(10, 5)
                    .WithFlag(15, name: "RTCEN")
                    .WithFlag(16, name: "BDRST")
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.ControlAndStatus, new DoubleWordRegister(this, 0x0C000000)
                    .WithFlag(0, out var lsion, name: "LSION")
                    .WithFlag(1, FieldMode.Read,
                        valueProviderCallback: _ => lsion.Value, name: "LSIRDY")
                    .WithReservedBits(2, 22)
                    .WithFlag(24, name: "RTCEN")
                    .WithReservedBits(25, 1)
                    .WithFlag(26, name: "PINRSTF")
                    .WithFlag(27, name: "PORRSTF")
                    .WithFlag(28, name: "SFTRSTF")
                    .WithFlag(29, name: "IWDGRSTF")
                    .WithFlag(30, name: "WWDGRSTF")
                    .WithFlag(31, name: "LPWRRSTF")
                },
            };

            RegistersCollection = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public void Reset()
        {
            RegistersCollection.Reset();
        }

        public long Size => 0x400;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private enum Registers
        {
            ClockControl = 0x0,
            ClockConfiguration = 0x4,
            ClockInterrupt = 0x8,
            APB2PeripheralReset = 0xC,
            APB1PeripheralReset = 0x10,
            AHBPeripheralClockEnable = 0x14,
            APB2PeripheralClockEnable = 0x18,
            APB1PeripheralClockEnable = 0x1C,
            BackupDomainControl = 0x20,
            ControlAndStatus = 0x24,
        }
    }
}