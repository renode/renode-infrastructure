//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public sealed class STM32F4_RCC : IDoubleWordPeripheral, IKnownSize
    {
        public STM32F4_RCC(Machine machine, STM32F4_RTC rtcPeripheral)
        {
            // Renode, in general, does not include clock control peripherals.
            // While this is doable, it seldom benefits real software development
            // and is very cumbersome to maintain.
            //
            // To properly support the RTC peripheral, we need to add this stub class.
            // It is common in Renode that whenever a register is implemented, it
            // either contains actual logic or tags, indicating not implemented fields.
            //
            // Here, however, we want to fake most of the registers as r/w values.
            // Usually we implemented this logic with Python peripherals.
            //
            // Keep in mind that most of these registers do not affect other
            // peripherals or their clocks.
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ClockControl, new DoubleWordRegister(this, 0x483)
                    .WithFlag(0, out var hsion, name: "HSION")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => hsion.Value, name: "HSIRDY")
                    .WithReservedBits(2, 1)
                    .WithValueField(3, 5, name: "HSITRIM")
                    .WithTag("HSICAL", 8, 8)
                    .WithFlag(16, out var hseon, name: "HSEON")
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseon.Value, name: "HSERDY")
                    .WithTag("HSEBYP", 18, 1)
                    .WithTag("CSSON", 19, 1)
                    .WithReservedBits(20, 4)
                    .WithFlag(24, out var pllon, name: "PLLON")
                    .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pllon.Value, name: "PLLRDY")
                    .WithFlag(26, out var plli2son, name: "PLLI2SON")
                    .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => plli2son.Value, name: "PLLI2SRDY")
                    .WithFlag(28, out var pllsaion, name: "PLLSAION")
                    .WithFlag(29, FieldMode.Read, valueProviderCallback: _ => pllsaion.Value, name: "PLLSAIRDY")
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.PLLConfiguration, new DoubleWordRegister(this, 0x24003010)
                    .WithValueField(0, 6, name: "PLLM")
                    .WithValueField(6, 9, name: "PLLN")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 2, name: "PLLP")
                    .WithReservedBits(18, 4)
                    .WithValueField(22, 1, name: "PLLSRC")
                    .WithReservedBits(23, 1)
                    .WithValueField(24, 4, name: "PLLQ")
                    .WithReservedBits(28, 4)
                },
                {(long)Registers.ClockConfiguration, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out var systemClockSwitch, name: "SW")
                    .WithValueField(2, 2, FieldMode.Read, name: "SWS", valueProviderCallback: _ => systemClockSwitch.Value)
                    .WithValueField(4, 4, name: "HPRE")
                    .WithReservedBits(8, 2)
                    .WithValueField(10, 3, name: "PPRE1")
                    .WithValueField(13, 3, name: "PPRE2")
                    .WithValueField(16, 5, name: "RTCPRE")
                    .WithValueField(21, 2, name: "MCO1")
                    .WithValueField(23, 1, name: "I2SSCR")
                    .WithValueField(24, 3, name: "MCO1PRE")
                    .WithValueField(27, 3, name: "MCO2PRE")
                    .WithValueField(30, 2, name: "MCO2")
                },
                //ClockInterrupt not implemented
                {(long)Registers.AHB1PeripheralReset, new DoubleWordRegister(this)
                    .WithValueField(0, 11, name: "GPIOxRST")
                    .WithReservedBits(11, 1)
                    .WithFlag(12, name: "CRCRST")
                    .WithReservedBits(13, 8)
                    .WithValueField(21, 3, name: "DMAxRST")
                    .WithReservedBits(24, 1)
                    .WithFlag(25, name: "ETHMACRST")
                    .WithReservedBits(26, 3)
                    .WithFlag(29, name: "OTGHSRST")
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.AHB2PeripheralReset, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DCMIRST")
                    .WithReservedBits(1, 3)
                    .WithFlag(4, name: "CRYPRST")
                    .WithFlag(5, name: "HASHRST")
                    .WithFlag(6, name: "RNGRST")
                    .WithFlag(7, name: "OTGFSRST")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.AHB3PeripheralReset, new DoubleWordRegister(this)
                    .WithFlag(0, name: "FMCRST")
                    .WithReservedBits(1, 30)
                },
                {(long)Registers.APB1PeripheralReset, new DoubleWordRegister(this)
                    .WithValueField(0, 9, name: "TIMxRST")
                    .WithReservedBits(9, 2)
                    .WithFlag(11, name: "WWDGRST")
                    .WithReservedBits(12, 2)
                    .WithValueField(14, 2, name: "SPIxRST")
                    .WithReservedBits(16, 1)
                    .WithValueField(17, 4, name: "UARTxRST")
                    .WithValueField(21, 3, name: "I2CxRST")
                    .WithReservedBits(24, 1)
                    .WithValueField(25, 2, name: "CANxRST")
                    .WithReservedBits(27, 1)
                    .WithFlag(28, name: "PWRRST")
                    .WithFlag(29, name: "DACRST")
                    .WithValueField(30, 2, name: "UARTxRST")
                },
                {(long)Registers.APB2PeripheralReset, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "TIMxRST")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 2, name: "USARTxRST")
                    .WithReservedBits(6, 2)
                    .WithFlag(8, name: "ADCRST")
                    .WithReservedBits(9, 2)
                    .WithFlag(11, name: "SDIORST")
                    .WithValueField(12, 2, name: "SPIxRST")
                    .WithFlag(14, name: "SYSCFGRST")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 3, name: "TIMxRST")
                    .WithReservedBits(19, 1)
                    .WithValueField(20, 2, name: "SPIxRST")
                    .WithFlag(22, name: "SAI1RST")
                    .WithReservedBits(23, 3)
                    .WithFlag(26, name: "LTDCRST")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.AHB1PeripheralClockEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 11, name: "GPIOxEN")
                    .WithReservedBits(11, 1)
                    .WithFlag(12, name: "CRCEN")
                    .WithReservedBits(13, 5)
                    .WithFlag(18, name: "BKPSRAMEN")
                    .WithReservedBits(19, 1)
                    .WithFlag(20, name: "CCMDATARAMEN")
                    .WithValueField(21, 3, name: "DMAxEN")
                    .WithReservedBits(24, 1)
                    .WithValueField(25, 4, name: "ETHMACxEN")
                    .WithValueField(29, 2, name: "OTGHSxEN")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.AHB2PeripheralClockEnable, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DCMIEN")
                    .WithReservedBits(1, 3)
                    .WithFlag(4, name: "CRYPEN")
                    .WithFlag(5, name: "HASHEN")
                    .WithFlag(6, name: "RNGEN")
                    .WithFlag(7, name: "OTGFSEN")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.AHB3PeripheralClockEnable, new DoubleWordRegister(this)
                    .WithFlag(0, name: "FMCEN")
                    .WithReservedBits(1, 30)
                },
                {(long)Registers.APB1PeripheralClockEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 9, name: "TIMxEN")
                    .WithReservedBits(9, 2)
                    .WithFlag(11, name: "WWDGEN")
                    .WithReservedBits(12, 2)
                    .WithValueField(14, 2, name: "SPIxEN")
                    .WithReservedBits(16, 1)
                    .WithValueField(17, 4, name: "UARTxEN")
                    .WithValueField(21, 3, name: "I2CxEN")
                    .WithReservedBits(24, 1)
                    .WithValueField(25, 2, name: "CANxEN")
                    .WithReservedBits(27, 1)
                    .WithFlag(28, name: "PWREN")
                    .WithFlag(29, name: "DACEN")
                    .WithValueField(30, 2, name: "UARTxEN")
                },
                {(long)Registers.APB2PeripheralClockEnable, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "TIMxEN")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 2, name: "USARTxEN")
                    .WithReservedBits(6, 2)
                    .WithValueField(8, 3, name: "ADCxEN")
                    .WithFlag(11, name: "SDIOEN")
                    .WithValueField(12, 2, name: "SPIxEN")
                    .WithFlag(14, name: "SYSCFGEN")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 3, name: "TIMxEN")
                    .WithReservedBits(19, 1)
                    .WithValueField(20, 2, name: "SPIxEN")
                    .WithFlag(22, name: "SAI1EN")
                    .WithReservedBits(23, 3)
                    .WithFlag(26, name: "LTDCEN")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.AHB1PeripheralClockEnableInLowPowerMode, new DoubleWordRegister(this)
                    .WithValueField(0, 11, name: "GPIOxLPEN")
                    .WithReservedBits(11, 1)
                    .WithFlag(12, name: "CRCLPEN")
                    .WithReservedBits(13, 2)
                    .WithFlag(15, name: "FLITFLPEN")
                    .WithValueField(16, 2, name: "SRAMxLPEN")
                    .WithFlag(18, name: "BKPSRAMLPEN")
                    .WithFlag(19, name: "SRAM3LPEN")
                    .WithReservedBits(20, 1)
                    .WithValueField(21, 3, name: "DMAxLPEN")
                    .WithReservedBits(24, 1)
                    .WithValueField(25, 4, name: "ETHMACxLPEN")
                    .WithValueField(29, 2, name: "OTGHSxLPEN")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.AHB2PeripheralClockEnableInLowPowerMode, new DoubleWordRegister(this)
                    .WithFlag(0, name: "DCMILPEN")
                    .WithReservedBits(1, 3)
                    .WithFlag(4, name: "CRYPLPEN")
                    .WithFlag(5, name: "HASHLPEN")
                    .WithFlag(6, name: "RNGLPEN")
                    .WithFlag(7, name: "OTGFSLPEN")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.AHB3PeripheralClockEnableInLowPowerMode, new DoubleWordRegister(this)
                    .WithFlag(0, name: "FMCLPEN")
                    .WithReservedBits(1, 30)
                },
                {(long)Registers.APB1PeripheralClockEnableInLowPowerMode, new DoubleWordRegister(this)
                    .WithValueField(0, 9, name: "TIMxLPEN")
                    .WithReservedBits(9, 2)
                    .WithFlag(11, name: "WWDGLPEN")
                    .WithReservedBits(12, 2)
                    .WithValueField(14, 2, name: "SPIxLPEN")
                    .WithReservedBits(16, 1)
                    .WithValueField(17, 4, name: "UARTxLPEN")
                    .WithValueField(21, 3, name: "I2CxLPEN")
                    .WithReservedBits(24, 1)
                    .WithValueField(25, 2, name: "CANxLPEN")
                    .WithReservedBits(27, 1)
                    .WithFlag(28, name: "PWRLPEN")
                    .WithFlag(29, name: "DACLPEN")
                    .WithValueField(30, 2, name: "UARTxLPEN")
                },
                {(long)Registers.APB2PeripheralClockEnableInLowPowerMode, new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "TIMxLPEN")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 2, name: "USARTxLPEN")
                    .WithReservedBits(6, 2)
                    .WithValueField(8, 3, name: "ADCxLPEN")
                    .WithFlag(11, name: "SDIOLPEN")
                    .WithValueField(12, 2, name: "SPIxLPEN")
                    .WithFlag(14, name: "SYSCFGLPEN")
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 3, name: "TIMxLPEN")
                    .WithReservedBits(19, 1)
                    .WithValueField(20, 2, name: "SPIxLPEN")
                    .WithFlag(22, name: "SAI1LPEN")
                    .WithReservedBits(23, 3)
                    .WithFlag(26, name: "LTDCLPEN")
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.BackupDomainControl, new DoubleWordRegister(this)
                    .WithFlag(0, out var lseon, name: "LSEON")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lseon.Value, name: "LSERDY")
                    .WithValueField(2, 1, name: "LSEBYP")
                    .WithReservedBits(3, 5)
                    .WithValueField(8, 2, name: "RTCSEL")
                    .WithReservedBits(10, 5)
                    .WithFlag(15, name: "RTCEN",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                machine.SystemBus.EnablePeripheral(rtcPeripheral);
                            }
                            else
                            {
                                machine.SystemBus.DisablePeripheral(rtcPeripheral);
                            }
                        })
                    .WithValueField(16, 1, name: "BDRST")
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.ClockControlAndStatus, new DoubleWordRegister(this, 0x0E000000)
                    .WithFlag(0, out var lsion, name: "LSION")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lsion.Value, name: "LSIRDY")
                    .WithReservedBits(2, 21)
                    .WithTag("RMVF", 24, 1)
                    .WithTag("BORRSTF", 25, 1)
                    .WithTag("PINRSTF", 26, 1)
                    .WithTag("PORRSTF", 27, 1)
                    .WithTag("SFTRSTF", 28, 1)
                    .WithTag("IWDGRSTF", 29, 1)
                    .WithTag("WWDGRSTF", 30, 1)
                    .WithTag("LPWRRSTF", 31, 1)
                },
                {(long)Registers.SpreadSpectrumClockGeneration, new DoubleWordRegister(this)
                    .WithValueField(0, 13, name: "MODPER")
                    .WithValueField(13, 15, name: "INCSTEP")
                    .WithReservedBits(28, 2)
                    .WithFlag(30, name: "SPREADSEL")
                    .WithFlag(31, name: "SSCGEN")
                },
                {(long)Registers.PLLI2SConfiguration, new DoubleWordRegister(this, 0x24003000)
                    .WithReservedBits(0, 6)
                    .WithValueField(6, 9, name: "PLLI2SNx")
                    .WithReservedBits(15, 9)
                    .WithValueField(24, 4, name: "PLLI2SQ")
                    .WithValueField(28, 3, name: "PLLI2SRx")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.PLLSAIConfiguration, new DoubleWordRegister(this, 0x24003000)
                    .WithReservedBits(0, 6)
                    .WithValueField(6, 9, name: "PLLSAIN")
                    .WithReservedBits(15, 9)
                    .WithValueField(24, 4, name: "PLLSAIQ")
                    .WithValueField(28, 3, name: "PLLSAIR")
                    .WithReservedBits(31, 1)
                },
                {(long)Registers.DedicatedClockConfiguration, new DoubleWordRegister(this)
                    .WithValueField(0, 5, name: "PLLI2SDIVQ")
                    .WithReservedBits(5, 3)
                    .WithValueField(8, 5, name: "PLLSAIDIVQ")
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 2, name: "PLLSAIDIVR")
                    .WithReservedBits(18, 2)
                    .WithValueField(20, 2, name: "SAI1ASRC")
                    .WithValueField(22, 2, name: "SAI1BSRC")
                    .WithFlag(24, name: "TIMPRE")
                    .WithReservedBits(25, 7)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public long Size => 0x400;

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            ClockControl = 0x0,
            PLLConfiguration = 0x4,
            ClockConfiguration = 0x8,
            ClockInterrupt = 0xC,
            AHB1PeripheralReset = 0x10,
            AHB2PeripheralReset = 0x14,
            AHB3PeripheralReset = 0x18,
            //gap
            APB1PeripheralReset = 0x20,
            APB2PeripheralReset = 0x24,
            //gap
            AHB1PeripheralClockEnable = 0x30,
            AHB2PeripheralClockEnable = 0x34,
            AHB3PeripheralClockEnable = 0x38,
            APB1PeripheralClockEnable = 0x40,
            APB2PeripheralClockEnable = 0x44,
            AHB1PeripheralClockEnableInLowPowerMode = 0x50,
            AHB2PeripheralClockEnableInLowPowerMode = 0x54,
            AHB3PeripheralClockEnableInLowPowerMode = 0x58,
            APB1PeripheralClockEnableInLowPowerMode = 0x60,
            APB2PeripheralClockEnableInLowPowerMode = 0x64,
            BackupDomainControl = 0x70,
            ClockControlAndStatus = 0x74,
            SpreadSpectrumClockGeneration = 0x80,
            PLLI2SConfiguration = 0x84,
            PLLSAIConfiguration = 0x88,
            DedicatedClockConfiguration = 0x8C,
        }
    }
}
