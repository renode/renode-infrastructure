//
// Copyright (c) 2010-2024 Antmicro
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
    public sealed class STM32H7_RCC : IDoubleWordPeripheral, IKnownSize
    {
        public STM32H7_RCC(IMachine machine)
        {
            //  Based on https://stm32-rs.github.io/stm32-rs/STM32H743.html#RCC
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ClockControl, new DoubleWordRegister(this, 0x83)
                    .WithFlag(0, out var hsion, name: "HSION")
                    .WithTag("HSIKERON", 1, 1)
                    .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => hsion.Value, name: "HSIRDY")
                    .WithTag("HSIDIV", 3, 2)
                    .WithTag("HSIDIVF", 5, 1)
                    .WithReservedBits(6, 1)
                    .WithFlag(7, out var csion, name: "CSION")
                    .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => csion.Value, name: "CSIRDY")
                    .WithTag("CSIKERON", 9, 1)
                    .WithReservedBits(10, 2)
                    .WithFlag(12, out var hsi48on, name: "HSI48ON")
                    .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => hsi48on.Value, name: "HSI48RDY")
                    .WithTag("D1CKRDY", 14, 1)
                    .WithTag("D2CKRDY", 15, 1)
                    .WithFlag(16, out var hseon, name: "HSEON")
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseon.Value, name: "HSERDY")
                    .WithTag("HSEBYP", 18, 1)
                    .WithTag("HSECSSON", 19, 1)
                    .WithReservedBits(20, 4)
                    .WithFlag(24, out var pll1on, name: "PLL1ON")
                    .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pll1on.Value, name: "PLL1RDY")
                    .WithFlag(26, out var pll2on, name: "PLL2ON")
                    .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => pll2on.Value, name: "PLL2RDY")
                    .WithFlag(28, out var pll3on, name: "PLL3ON")
                    .WithFlag(29, FieldMode.Read, valueProviderCallback: _ => pll3on.Value, name: "PLL3RDY")
                    .WithReservedBits(30, 2)
                },
                {(long)Registers.ClockConfiguration, new DoubleWordRegister(this, 0x0)
                    .WithValueField(0, 3, out var sw, name: "SW")
                    .WithValueField(3, 3, FieldMode.Read, valueProviderCallback: _ => sw.Value, name: "SWS")
                    .WithTaggedFlag("STOPWUCK", 6)
                    .WithTaggedFlag("STOPKERWUCK", 7)
                    .WithTag("RTCPRE", 8, 6)
                    .WithTaggedFlag("HRTIMSEL", 14)
                    .WithTaggedFlag("TIMPRE", 15)
                    .WithReservedBits(16, 2)
                    .WithTag("MCO1PRE", 18, 4)
                    .WithTag("MCO1", 22, 3)
                    .WithTag("MCO2PRE", 25, 4)
                    .WithTag("MCO2", 29, 3)
                },
                {(long)Registers.PLLClockSourceSelect, new DoubleWordRegister(this, 0x02020200)
                    .WithValueField(0, 2, name: "PLLSRC")
                    .WithReservedBits(2, 2)
                    .WithValueField(4, 6, name: "DIVM1")
                    .WithReservedBits(10, 2)
                    .WithValueField(12, 6, name: "DIVM2")
                    .WithReservedBits(18, 2)
                    .WithValueField(20, 6, name: "DIVM3")
                    .WithReservedBits(26, 6)
                },
                {(long)Registers.PLLConfigurationRegister, new DoubleWordRegister(this, 0x01FF0000)
                },
                {(long)Registers.BackupDomainControl, new DoubleWordRegister(this)
                    .WithFlag(0, out var lseon, name: "LSEON")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lseon.Value, name: "LSERDY")
                    .WithValueField(2, 1, name: "LSEBYP")
                    .WithReservedBits(3, 5)
                    .WithValueField(8, 2, name: "RTCSEL")
                    .WithReservedBits(10, 5)
                    .WithTaggedFlag("RTCEN", 15)
                    .WithValueField(16, 1, name: "BDRST")
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.ClockControlAndStatus, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, out var lsion, name: "LSION")
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lsion.Value, name: "LSIRDY")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.AHB4Enable, new DoubleWordRegister(this, 0x0)
                    .WithFlag(0, name: "GPIOAEN")
                    .WithFlag(1, name: "GPIOBEN")
                    .WithFlag(2, name: "GPIOCEN")
                    .WithFlag(3, name: "GPIODEN")
                    .WithFlag(4, name: "GPIOEEN")
                    .WithFlag(5, name: "GPIOFEN")
                    .WithFlag(6, name: "GPIOGEN")
                    .WithFlag(7, name: "GPIOHEN")
                    .WithFlag(8, name: "GPIOIEN")
                    .WithFlag(9, name: "GPIOJEN")
                    .WithFlag(10, name: "GPIOKEN")
                    .WithReservedBits(11, 8)
                    .WithFlag(19, name: "CRCEN")
                    .WithReservedBits(20, 1)
                    .WithFlag(21, name: "BDMAEN")
                    .WithReservedBits(22, 2)
                    .WithFlag(24, name: "ADC3EN")
                    .WithFlag(25, name: "HSEMEN")
                    .WithReservedBits(26, 2)
                    .WithFlag(28, name: "BKPRAMEN")
                    .WithReservedBits(29, 3)
                }
            };

            for(var i = 0; i < 3; ++i)
            {
                registersMap.Add((long)Registers.PLL1FractionalDivider + i * 0x8, new DoubleWordRegister(this, 0x0)
                    .WithReservedBits(0, 3)
                    .WithValueField(3, 13, name: $"FRACN{i + 1}")
                    .WithReservedBits(16, 16)
                );
            }

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
            InternalClockSourceCalibration = 0x4,
            ClockConfiguration = 0x10,
            PLLClockSourceSelect = 0x28,
            PLLConfigurationRegister = 0x2c,
            PLL1DividersConfiguration = 0x30,
            PLL1FractionalDivider = 0x34,
            PLL2DividersConfiguration = 0x38,
            PLL2FractionalDivider = 0x3C,
            PLL3DividersConfiguration = 0x40,
            PLL3FractionalDivider = 0x44,
            // ...
            BackupDomainControl = 0x70,
            ClockControlAndStatus = 0x74,
            // ...
            AHB4Enable = 0xE0
        }
    }
}
