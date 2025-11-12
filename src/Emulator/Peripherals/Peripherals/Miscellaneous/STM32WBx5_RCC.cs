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
    public class STM32WBx5_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32WBx5_RCC(IMachine machine,
                IHasFrequency nvic = null,
                long hsi16Frequency = DefaultHsi16Frequency,
                long hse32Frequency = DefaultHse32Frequency,
                long lsi1Frequency = DefaultLsi1Frequency,
                long lsi2Frequency = DefaultLsi2Frequency,
                long lseFrequency = DefaultLseFrequency,
                long hsi48Frequency = DefaultHsi48Frequency) : base(machine)
        {
            this.nvic = nvic;
            this.hsi16Frequency = hsi16Frequency;
            this.hse32Frequency = hse32Frequency;
            this.lsi1Frequency = lsi1Frequency;
            this.lsi2Freqency = lsi2Frequency;
            this.lseFrequency = lseFrequency;
            this.hsi48Frequency = hsi48Frequency;
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
            if (timer != null)
            {
                timer.Frequency = frequency;
            }
        }

        private void UpdateClocks()
        {
            TrySetFrequency(nvic, SystemClock);
        }

        private void DefineRegisters()
        {
            // Keep in mind that most of these registers do not affect other
            // peripherals or their clocks.
            // NOTE: reset value is different whether wakeup or POR
            Registers.ClockControl.Define(this, 0x00000061)
                .WithFlag(0, out var msiClockEnabled, name: "MSION")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => msiClockEnabled.Value, name: "MSIRDY")
                .WithTaggedFlag("MSIPLLEN", 2)
                .WithReservedBits(3, 1)
                .WithEnumField(4, 4, out msiRangeSwitch, name: "MSIRANGE")
                .WithFlag(8, out var hsi16ClockEnabled, name: "HSION")
                .WithTaggedFlag("HSIKERON", 9)
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => hsi16ClockEnabled.Value, name: "HSIRDY")
                .WithTaggedFlag("HSIASFS", 11)
                .WithFlag(12, FieldMode.Read, name: "HSIKERDY")
                .WithReservedBits(13, 3)
                .WithFlag(16, out var hseClockEnabled, name: "HSEON")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseClockEnabled.Value, name: "HSERDY")
                .WithReservedBits(18, 1)
                .WithTaggedFlag("CSSON", 19)
                .WithTaggedFlag("HSEPRE", 20)
                .WithReservedBits(21, 3)
                .WithFlag(24, out var pllClockEnabled, name: "PLLON")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pllClockEnabled.Value, name: "PLLRDY")
                .WithFlag(26, out var pllSai1ClockEnabled, name: "PLLSAI1ON")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => pllSai1ClockEnabled.Value, name: "PLLSAI1RDY")
                .WithChangeCallback((_, val) => UpdateClocks());

            Registers.ClockConfiguration.Define(this, 0x00070000)
                .WithEnumField(0, 2, out systemClockSwitch, name: "SW")
                .WithEnumField<DoubleWordRegister, SystemClockSource>(2, 2, FieldMode.Read, valueProviderCallback: _ => systemClockSwitch.Value, name: "SWS")
                .WithValueField(4, 4, name: "HPRE")
                .WithValueField(8, 3, name: "PPRE1")
                .WithValueField(11, 3, name: "PPRE2")
                .WithReservedBits(14, 1)
                .WithTaggedFlag("STOPWUCK", 15)
                .WithTaggedFlag("HPREF", 16)
                .WithTaggedFlag("PPRE1F", 17)
                .WithTaggedFlag("PPRE2F", 18)
                .WithReservedBits(19, 5)
                .WithValueField(24, 4, name: "MCOSEL")
                .WithValueField(28, 3, name: "MCOPRE")
                .WithReservedBits(31, 1)
                .WithChangeCallback((_, val) => UpdateClocks());

            Registers.Apb1PeripheralReset2.Define(this)
                .WithTaggedFlag("LPUART1RST", 0) // TODO: hook up LPUART clock
                .WithReservedBits(1, 4)
                .WithTaggedFlag("LPTIM2RST", 5) // TODO: hook up LPTIM2 clock
                .WithReservedBits(6, 26);

            Registers.Apb2PeripheralReset.Define(this)
                .WithReservedBits(0, 11)
                .WithTaggedFlag("TIM1RST", 11)
                .WithTaggedFlag("SPI1RST", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1RST", 14)
                .WithReservedBits(16, 1)
                .WithTaggedFlag("TIM16RST", 17)
                .WithTaggedFlag("TIM17RST", 18)
                .WithReservedBits(19, 2)
                .WithTaggedFlag("SAI1RST", 21)
                .WithReservedBits(22, 10);

            // TODO: hook up GPIO and ADC clocks
            Registers.Ahb2PeripheralClockEnable.Define(this)
                .WithTaggedFlag("GPIOAEN", 0)
                .WithTaggedFlag("GPIOBEN", 1)
                .WithTaggedFlag("GPIOCEN", 2)
                .WithTaggedFlag("GPIODEN", 3)
                .WithTaggedFlag("GPIOEEN", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("GPIOHEN", 7)
                .WithReservedBits(8, 5)
                .WithTaggedFlag("ADCEN", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("AES1EN", 16)
                .WithReservedBits(17, 15);

            Registers.Ahb3Ahb4PeripheralClockEnable.Define(this, 0x02080000)
                .WithReservedBits(0, 8)
                .WithTaggedFlag("QUADSPIEN", 8)
                .WithReservedBits(9, 7)
                .WithTaggedFlag("PKAEN", 16)
                .WithTaggedFlag("AES2EN", 17)
                .WithTaggedFlag("RNGEN", 18)
                .WithTaggedFlag("HSEMEN", 19)
                .WithTaggedFlag("IPCCEN", 20)
                .WithReservedBits(21, 4)
                .WithFlag(25, name: "FLASHEN")
                .WithReservedBits(26, 6);

            Registers.Apb1PeripheralClockEnable2.Define(this)
                .WithTaggedFlag("LPUART1EN", 0) // TODO: hook up LPUART clock
                .WithReservedBits(1, 4)
                .WithTaggedFlag("LPTIM2EN", 5) // TODO: hook up LPTIM2 clock
                .WithReservedBits(6, 26);

            Registers.Apb2PeripheralClockEnable.Define(this)
                .WithReservedBits(0, 11)
                .WithTaggedFlag("TIM1EN", 11)
                .WithTaggedFlag("SPI1EN", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1EN", 14)
                .WithReservedBits(16, 1)
                .WithTaggedFlag("TIM16EN", 17)
                .WithTaggedFlag("TIM17EN", 18)
                .WithReservedBits(19, 2)
                .WithTaggedFlag("SAI1EN", 21)
                .WithReservedBits(22, 10);

            // TODO: this register should only be writable when DBP in RCC has been set along with other
            // restrictions. See 8.4.30
            Registers.BackupDomainControl.Define(this, 0x00000000)
                .WithFlag(0, out var lseEnabled, name: "LSEON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lseEnabled.Value, name: "LSERDY")
                .WithTaggedFlag("LSEBYP", 2)
                .WithValueField(3, 2, name: "LSEDRV")
                .WithTaggedFlag("LSECSSON", 5)
                .WithFlag(6, FieldMode.Read, name: "LSECSSD")
                .WithReservedBits(7, 1)
                .WithValueField(8, 2, name: "RTCSEL")
                .WithReservedBits(10, 5)
                .WithTaggedFlag("RTCEN", 15)
                .WithTaggedFlag("BDRST", 16)
                .WithReservedBits(17, 7)
                .WithTaggedFlag("LSCOEN", 24)
                .WithTaggedFlag("LSCOSEL", 25)
                .WithReservedBits(26, 6);

            Registers.ControlStatus.Define(this, 0x0c000000)
                .WithFlag(0, out var lsi1ClockEnabled, name: "LSI1ON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lsi1ClockEnabled.Value, name: "LSI1RDY")
                .WithFlag(2, out var lsi2ClockEnabled, name: "LSI2ON")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => lsi2ClockEnabled.Value, name: "LSI2RDY")
                .WithReservedBits(4, 8)
                .WithReservedBits(12, 4)
                .WithFlag(16, FieldMode.Read, name: "RFRSTS")
                .WithReservedBits(17, 6)
                .WithFlag(23, name: "RMVF")
                .WithFlag(25, FieldMode.Read, name: "OBLRSTF")
                .WithFlag(26, FieldMode.Read, name: "PINRSTF")
                .WithFlag(27, FieldMode.Read, name: "BORRSTF")
                .WithFlag(28, FieldMode.Read, name: "SFTRSTF")
                .WithFlag(29, FieldMode.Read, name: "IWWGRSTF")
                .WithFlag(30, FieldMode.Read, name: "WWDGRSTF")
                .WithFlag(31, FieldMode.Read, name: "LPWRRSTF");

            Registers.ClockRecoveryRc.Define(this, 0x00000000)
                .WithFlag(0, out var hsi48Enabled, name: "HSI48ON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => hsi48Enabled.Value, name: "HSI48RDY")
                .WithReservedBits(2, 5)
                .WithValueField(7, 9, FieldMode.Read, name: "HSI48CAL")
                .WithReservedBits(16, 16);

            Registers.ExtendedClockRecovery.Define(this, 0x00030000)
                .WithValueField(0, 4, name: "SHDHPRE")
                .WithValueField(4, 4, name: "C2HPRE")
                .WithReservedBits(8, 8)
                .WithTaggedFlag("SNDHPREF", 16)
                .WithTaggedFlag("C2HPREF", 17)
                .WithReservedBits(18, 2)
                .WithTaggedFlag("RFCSS", 20)
                .WithReservedBits(21, 11);
        }

        private readonly IHasFrequency nvic;
        private readonly long hsi16Frequency;
        private readonly long hse32Frequency;
        private readonly long lsi1Frequency;
        private readonly long lsi2Freqency;
        private readonly long lseFrequency;
        private readonly long hsi48Frequency;
        private long msiFrequency
        {
            get
            {
                switch(msiRangeSwitch.Value)
                {
                    default:
                    case MsiRange.KHZ_100:
                        return 100000;
                    case MsiRange.KHZ_200:
                        return 200000;
                    case MsiRange.KHZ_400:
                        return 400000;
                    case MsiRange.KHZ_800:
                        return 800000;
                    case MsiRange.MHZ_1:
                        return 1000000;
                    case MsiRange.MHZ_2:
                        return 2000000;
                    case MsiRange.MHZ_4:
                        return 4000000;
                    case MsiRange.MHZ_8:
                        return 8000000;
                    case MsiRange.MHZ_16:
                        return 16000000;
                    case MsiRange.MHZ_24:
                        return 24000000;
                    case MsiRange.MHZ_32:
                        return 32000000;
                    case MsiRange.MHZ_48:
                        return 48000000;
                }
            }
        }

        private const long DefaultHsi16Frequency = 16000000;
        private const long DefaultMsiFrequency = 4000000;
        private const long DefaultHse32Frequency = 32000000;
        private const long DefaultLsi1Frequency = 32000;
        private const long DefaultLsi2Frequency = 32000;
        private const long DefaultLseFrequency = 32768;
        private const long DefaultHsi48Frequency = 48000000;

        private long SystemClock
        {
            get
            {
                switch(systemClockSwitch.Value)
                {
                    // TODO: actually support PLL clock
                    default:
                    case SystemClockSource.Pll:
                    case SystemClockSource.Msi:
                        return msiFrequency;
                    case SystemClockSource.Hsi16:
                        return hsi16Frequency;
                    case SystemClockSource.Hse:
                        return hse32Frequency;
                }
            }
        }

        private IEnumRegisterField<MsiRange> msiRangeSwitch;
        private IEnumRegisterField<SystemClockSource> systemClockSwitch;

        private enum MsiRange
        {
            KHZ_100 = 0,
            KHZ_200 = 1,
            KHZ_400 = 2,
            KHZ_800 = 3,
            MHZ_1 = 4,
            MHZ_2 = 5,
            MHZ_4 = 6,
            MHZ_8 = 7,
            MHZ_16 = 8,
            MHZ_24 = 9,
            MHZ_32 = 10,
            MHZ_48 = 11,
        }

        private enum SystemClockSource
        {
            Msi = 0,
            Hsi16 = 1,
            Hse = 2,
            Pll = 3,
        }

        private enum Registers
        {
            ClockControl = 0x0, // CR
            ClockConfiguration = 0x8, // CFGR
            Apb1PeripheralReset2 = 0x3c, // APB1RSTR2
            Apb2PeripheralReset = 0x40, // APB2RSTR
            Ahb2PeripheralClockEnable = 0x4c, // AHB2ENR
            Ahb3Ahb4PeripheralClockEnable = 0x50, // AHB3ENR
            Apb1PeripheralClockEnable2 = 0x5c, // APB1ENR2
            Apb2PeripheralClockEnable = 0x60, // APB2ENR
            BackupDomainControl = 0x90, // BDCR
            ControlStatus = 0x94, // CSR
            ClockRecoveryRc = 0x98, // CRRCR
            ExtendedClockRecovery = 0x108, // EXTCFGR
        }
    }
}
