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
    public sealed class STM32F4_RCC : IDoubleWordPeripheral, IKnownSize
    {
        public STM32F4_RCC(Machine machine, STM32F4_RTC rtcPeripheral)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ClockControl, new DoubleWordRegister(this, 0x83)
                    .WithTag("HSION", 0, 1)
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "HSIRDY")
                    .WithReservedBits(2, 1)
                    .WithTag("HSITRIM", 3, 5)
                    .WithTag("HSICAL", 8, 8)
                    .WithTag("HSEON", 16, 1)
                    .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => true, name: "HSERDY")
                    .WithTag("HSEBYP", 18, 1)
                    .WithTag("CSSON", 19, 1)
                    .WithReservedBits(20, 4)
                    .WithTag("PLLON", 24, 1)
                    .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => true, name: "PLLRDY")
                    .WithTag("PLLI2SON", 26, 1)
                    .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => true, name: "PLLI2SRDY")
                    .WithReservedBits(28, 4)
                },
                {(long)Registers.PLLConfiguration, new DoubleWordRegister(this, 0x24003010)
                    .WithTag("PLLM", 0, 6)
                    .WithTag("PLLN", 6, 9)
                    .WithReservedBits(15, 1)
                    .WithTag("PLLP", 16, 2)
                    .WithReservedBits(18, 4)
                    .WithTag("PLLSRC", 22, 1)
                    .WithReservedBits(23, 1)
                    .WithTag("PLLQ", 24, 4)
                    .WithReservedBits(28, 4)
                },
                {(long)Registers.ClockConfiguration, new DoubleWordRegister(this)
                    .WithValueField(0, 2, out var systemClockSwitch, name: "SW")
                    .WithValueField(2, 2, FieldMode.Read, name: "SWS", valueProviderCallback: _ => systemClockSwitch.Value)
                    .WithTag("HPRE", 4, 4)
                    .WithReservedBits(8, 2)
                    .WithTag("PPRE1", 10, 3)
                    .WithTag("PPRE2", 13, 3)
                    .WithTag("RTCPRE", 16, 5)
                    .WithTag("MCO1", 21, 2)
                    .WithTag("I2SSCR", 23, 1)
                    .WithTag("MCO1PRE", 24, 3)
                    .WithTag("MCO2PRE", 27, 3)
                    .WithTag("MCO2", 30, 2)
                },
                {(long)Registers.BackupDomainControl, new DoubleWordRegister(this)
                    .WithTag("LSEON", 0, 1)
                    .WithTag("LSERDY", 1, 1)
                    .WithTag("LSEBYP", 2, 1)
                    .WithReservedBits(3, 5)
                    .WithTag("RTCSEL", 8, 2)
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
                    .WithTag("BDRST", 16, 1)
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.ClockControlAndStatus, new DoubleWordRegister(this, 0x0E000000)
                    .WithTag("LSION", 0, 1)
                    .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => true, name: "LSIRDY")
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
            BackupDomainControl = 0x70,
            ClockControlAndStatus = 0x74,
        }
    }
}
