//
// Copyright (c) 2025 Philippe Michaud-Boudreault <philmb3487@proton.me>
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32L4_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32L4_RCC(IMachine machine, IPeripheral rtc = null, ITimer lptimer1 = null, ITimer lptimer2 = null, IHasDivisibleFrequency systick = null,
            long apbFrequency = DefaultApbFrequency, long lsiFrequency = DefaultLsiFrequency, long lseFrequency = DefaultLseFrequency,
            long hseFrequency = DefaultHseFrequency) : base(machine)
        {
            if(systick == null)
            {
                this.Log(LogLevel.Warning, "Systick not passed in the RCC constructor. Changes to the system clock will be ignored");
            }
            this.systick = systick;
            if(rtc == null)
            {
                this.Log(LogLevel.Warning, "RTC not passed in the RCC constructor. Changes to the real-time clock will be ignored");
            }
            this.rtc = rtc;
            if(lptimer1 == null)
            {
                this.Log(LogLevel.Warning, "Lptimer1 not passed in the RCC constructor. Changes to the low-power timer clock will be ignored");
            }
            if(lptimer2 == null)
            {
                this.Log(LogLevel.Warning, "Lptimer2 not passed in the RCC constructor. Changes to the low-power timer clock will be ignored");
            }
            this.lptimer1 = lptimer1;
            this.lptimer2 = lptimer2;

            this.apbFrequency = apbFrequency;
            this.lsiFrequency = lsiFrequency;
            this.lseFrequency = lseFrequency;
            this.hseFrequency = hseFrequency;

            this.MsiFrequency_0 = DefaultMsiFrequency;
            this.MsiFrequency_1 = DefaultMsiFrequency;

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            pllDivisor = 1;
            pllMultiplier = 16;
            msiMultiplier = 64;
            pll48Divisor = 2;
            if (systick != null)
            {
                systick.Divider = 1;
            }
            base.Reset();
            UpdateClocks();
        }

        public long Size => 0x400;

        // Update frequencies and divisors of all clocks connected to the RCC.
        // Make sure it's called after any configuration register is touched
        private void UpdateClocks()
        {
            UpdateSystemClock();
            UpdateLpTimer1Clock();
            UpdateLpTimer2Clock();
        }

        private void UpdateSystemClock()
        {
            if(systick == null)
            {
                return;
            }
            
            var old = systick.Frequency;
            switch(systemClockSwitch.Value)
            {
                case SystemClockSourceSelection.Msi:
                    systick.Frequency = MsiFrequency;
                    break;
                case SystemClockSourceSelection.Hsi16:
                    systick.Frequency = Hsi16Frequency;
                    break;
                case SystemClockSourceSelection.Hse:
                    systick.Frequency = hseFrequency;
                    break;
                case SystemClockSourceSelection.Pll:
                    if(!pllOn.Value)
                    {
                        this.Log(LogLevel.Error, "Systick source set to PLL when PLL is disabled.");
                    }
                    systick.Frequency = PllFrequency;
                    break;
                default:
                    throw new Exception("unreachable code");
            }
            if(old != systick.Frequency)
            {
                this.Log(
                    LogLevel.Debug,
                    "systick clock frequency changed to {0}. Current effective frequency: {1}",
                    systick.Frequency,
                    systick.Frequency / systick.Divider
                );
            }
        }

        private void UpdateLpTimer1Clock()
        {
            if(lptimer1 == null) 
            {
                return;
            }

            var old = lptimer1.Frequency;
            switch(lpTimer1Selection.Value)
            {
                case LpTimerClockSourceSelection.Apb:
                    lptimer1.Frequency = apbFrequency;
                    break;
                case LpTimerClockSourceSelection.Lsi:
                    lptimer1.Frequency = lsiFrequency;
                    break;
                case LpTimerClockSourceSelection.Hsi16:
                    lptimer1.Frequency = Hsi16Frequency;
                    break;
                case LpTimerClockSourceSelection.Lse:
                    lptimer1.Frequency = lseFrequency;
                    break;
                default:
                    throw new Exception("unreachable code");
            }
            if(old != lptimer1.Frequency)
            {
                this.Log(LogLevel.Debug, "LpTimer1 clock frequency changed to {0}", lptimer1.Frequency);
            }
        }

        private void UpdateLpTimer2Clock()
        {
            if(lptimer2 == null) 
            {
                return;
            }

            var old = lptimer2.Frequency;
            switch(lpTimer2Selection.Value)
            {
                case LpTimerClockSourceSelection.Apb:
                    lptimer2.Frequency = apbFrequency;
                    break;
                case LpTimerClockSourceSelection.Lsi:
                    lptimer2.Frequency = lsiFrequency;
                    break;
                case LpTimerClockSourceSelection.Hsi16:
                    lptimer2.Frequency = Hsi16Frequency;
                    break;
                case LpTimerClockSourceSelection.Lse:
                    lptimer2.Frequency = lseFrequency;
                    break;
                default:
                    throw new Exception("unreachable code");
            }
            if(old != lptimer2.Frequency)
            {
                this.Log(LogLevel.Debug, "LpTimer2 clock frequency changed to {0}", lptimer2.Frequency);
            }
        }

        private void DefineRegisters()
        {
            // Keep in mind that most of these registers do not affect other
            // peripherals or their clocks.
            Registers.BackupDomainControl.Define(this)
                .WithFlag(0, out var lseon, name: "LSEON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lseon.Value, name: "LSERDY")
                .WithTaggedFlag("LSEBYP", 2)
                .WithValueField(3, 2, name: "LSEDRV")
                .WithTaggedFlag("LSECSSON", 5)
                .WithTaggedFlag("LSECSSD", 6)
                .WithFlag(7, name: "LSESYSDIS")
                .WithValueField(8, 2, name: "RTCSEL")
                .WithReservedBits(10, 5)
                .WithFlag(15, writeCallback: (_, value) =>
                    {
                        if (rtc == null)
                        {
                            return;
                        }
                        sysbus.SetPeripheralEnabled(rtc, value);
                    },
                    valueProviderCallback: _ => rtc == null ? false : sysbus.IsPeripheralEnabled(rtc), name: "RTCEN")
                .WithFlag(16, name: "BDRST")
                .WithReservedBits(17, 7)
                .WithFlag(24, name: "LSCOEN")
                .WithFlag(25, name: "LSCOSEL")
                .WithReservedBits(26, 6)
                ;

            Registers.ClockControl.Define(this, 0x00000003)
                .WithFlag(0, out var msion, name: "MSION")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => msion.Value, name: "MSIRDY")
                .WithFlag(2, out var msipllen, name: "MSIPLLEN",
                    writeCallback: (previous, value) =>
                    {
                        if (!previous && value && lseon.Value == false)
                        {
                            this.Log(LogLevel.Error, "MSIPLL enabled without enabling LSE");
                        }
                    })
                .WithFlag(3, out msirgsel, name: "MSIRGSEL")
                .WithValueField(4, 4, name: "MSIRANGE",
                    writeCallback: (previous, value) =>
                    {
                        if (msion.Value == true && previous != value)
                        {
                            this.Log(LogLevel.Error, "MSIRANGE modified when MSION");
                            return;
                        }

                        switch (value)
                        {
                            case 0:
                                MsiFrequency_0 = 100000;
                                break;
                            case 1:
                                MsiFrequency_0 = 200000;
                                break;
                            case 2:
                                MsiFrequency_0 = 400000;
                                break;
                            case 3:
                                MsiFrequency_0 = 800000;
                                break;
                            case 4:
                                MsiFrequency_0 = 1000000;
                                break;
                            case 5:
                                MsiFrequency_0 = 2000000;
                                break;
                            case 6:
                                MsiFrequency_0 = 4000000;  // Default
                                break;
                            case 7:
                                MsiFrequency_0 = 8000000;
                                break;
                            case 8:
                                MsiFrequency_0 = 1600000;
                                break;
                            case 9:
                                MsiFrequency_0 = 2400000;
                                break;
                            case 10:
                                MsiFrequency_0 = 3200000;
                                break;
                            case 11:
                                MsiFrequency_0 = 4800000;
                                break;
                            default:
                                this.Log(LogLevel.Error, "Invalid MSI range: {0}", value);
                                break;
                        }
                    })
                .WithFlag(8, out var hsion, name: "HSION")
                .WithFlag(9, name: "HSIKERON")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => hsion.Value, name: "HSIRDY")
                .WithFlag(11, name: "HSIASFS")
                .WithReservedBits(12, 3)
                .WithFlag(16, out var hseon, name: "HSEON")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseon.Value, name: "HSERDY")
                .WithFlag(18, name: "HSEBYP")
                .WithTag("CSSON", 19, 1)
                .WithReservedBits(20, 4)
                .WithFlag(24, out pllOn, name: "PLLON")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pllOn.Value, name: "PLLRDY")
                .WithFlag(26, out var pllsai1on, name: "PLLSAI1ON")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => pllsai1on.Value, name: "PLLSAI1RDY")
                .WithReservedBits(28, 4)
                .WithWriteCallback((_, __) => UpdateClocks())
                ;

            Registers.InternalClockSourcesCalibration.Define(this, 0x40000000)
                .WithValueField(0, 8, name: "MSICAL")
                .WithValueField(8, 8, name: "MSITRIM")
                .WithValueField(16, 8, name: "HSICAL")
                .WithValueField(24, 7, name: "HSITRIM")
                .WithReservedBits(31, 1)
                ;

            Registers.ClockConfigurationCfgr.Define(this)
                .WithEnumField<DoubleWordRegister, SystemClockSourceSelection>(0, 2, out systemClockSwitch, name: "SW")
                .WithValueField(2, 2, FieldMode.Read, name: "SWS", valueProviderCallback: _ => (ulong)systemClockSwitch.Value)
                .WithValueField(4, 4, name: "HPRE",
                    writeCallback: (previous, value) =>
                    {
                        if (systick == null || previous == value)
                        {
                            return;
                        }

                        // SYSCLK is not divided unless HPRE is set to 0b1000 or higher,
                        // in which case it's divided by consecutive powers of 2.
                        if ((0b1000 & value) == 0)
                        {
                            systick.Divider = 1;
                        }
                        else
                        {
                            var power = (0b111 & value) + 1;
                            systick.Divider = (int)Math.Pow(2, power);
                        }
                        this.Log(
                            LogLevel.Debug,
                            "systick clock divisor changed to {0}. Current effective frequency: {1}",
                            systick.Divider,
                            systick.Frequency / systick.Divider
                        );
                    })
                .WithValueField(8, 3, name: "PPRE1")
                .WithValueField(11, 3, name: "PPRE2")
                .WithReservedBits(14, 1)
                .WithFlag(15, name: "STOPWUCK")
                .WithReservedBits(16, 8)
                .WithValueField(24, 4, name: "MCOSEL")
                .WithValueField(28, 3, name: "MCOPRE")
                .WithReservedBits(31, 1)
                .WithWriteCallback((_, __) => UpdateClocks())
                ;

            Registers.PLLConfiguration.Define(this, 0x00001000)
                .WithEnumField<DoubleWordRegister, PllSourceSelection>(0, 2, out pllSource, name: "PLLSRC",
                    writeCallback: (previous, value) =>
                    {
                        if ((pllOn.Value || pllsai1on.Value) && previous != value)
                        {
                            this.Log(LogLevel.Error, "PLLSRC modified while PLL is enabled");
                        }
                    })
                .WithReservedBits(2, 2)
                .WithValueField(4, 2, name: "PLLM",
                    writeCallback: (previous, value) =>
                    {
                        if (pllOn.Value && previous != value)
                        {
                            this.Log(LogLevel.Error, "PLLM modified while PLL is enabled");
                        }
                        pllDivisor = (long)(1 + value);
                    })
                .WithReservedBits(7, 1)
                .WithValueField(8, 7, name: "PLLN",
                    writeCallback: (previous, value) =>
                    {
                        if (pllOn.Value && previous != value)
                        {
                            this.Log(LogLevel.Error, "PLLN modified while PLL is enabled");
                        }

                        switch (value)
                        {
                            case ulong n when (n >= 8 && n <= 86):
                                pllMultiplier = (long)n;
                                break;
                            default:
                                // We don't need a special check here, compared to PLLDIV,
                                // as here the reset value is valid and the only invalid values
                                // are ones that'd be deliberately set by the software
                                this.Log(LogLevel.Error, "Invalid PLLN: {0}", value);
                                break;
                        }
                    })
                .WithReservedBits(15, 1)
                .WithFlag(16, name: "PLLPEN")
                .WithFlag(17, name: "PLLP")
                .WithReservedBits(18, 2)
                .WithFlag(20, name: "PLLQEN")
                .WithValueField(21, 2, name: "PLLQ",
                    writeCallback: (previous, value) =>
                    {
                        if (pllOn.Value && previous != value)
                        {
                            this.Log(LogLevel.Error, "PLLQ modified while PLL is enabled");
                        }

                        switch (value)
                        {
                            case 0b00:
                                pll48Divisor = 2;
                                break;
                            case 0b01:
                                pll48Divisor = 4;
                                break;
                            case 0b10:
                                pll48Divisor = 6;
                                break;
                            case 0b11:
                                pll48Divisor = 8;
                                break;
                        }
                    })
                .WithReservedBits(23, 1)
                .WithFlag(24, name: "PLLREN")
                .WithValueField(25, 2, name: "PLLR")
                .WithValueField(27, 5, name: "PLLPDIV")
                ;

            Registers.PLLSAI1Configuration.Define(this, 0x00001000)
                .WithReservedBits(0, 8)
                ;

            Registers.ClockInterruptEnable.Define(this)
                .WithFlag(0, out var lsirdyie, name: "LSIRDYIE")
                .WithFlag(1, out var lserdyie, name: "LSERDYIE")
                .WithFlag(2, out var msirdyie, name: "MSIRDYIE")
                .WithFlag(3, out var hsirdyie, name: "HSIRDYIE")
                .WithFlag(4, out var hserdyie, name: "HSERDYIE")
                .WithFlag(5, out var pllrdyie, name: "PLLRDYIE")
                .WithFlag(6, out var pllsai1rdyie, name: "PLLSAI1RDYIE")
                .WithReservedBits(7, 2)
                .WithFlag(9, out var lsecssie, name: "LSECSSIE")
                .WithFlag(10, out var hsi48rdyie, name: "HSI48RDYIE")
                .WithReservedBits(11, 21)
                ;

            Registers.ClockInterruptFlag.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => lsirdyie.Value, name: "LSIRDYF")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lserdyie.Value, name: "LSERDYF")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => msirdyie.Value, name: "MSIRDYF")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => hsirdyie.Value, name: "HSIRDYF")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => hserdyie.Value, name: "HSERDYF")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => pllrdyie.Value, name: "PLLRDYF")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => pllsai1rdyie.Value, name: "PLLSAI1RDYF")
                .WithReservedBits(7, 1)
                .WithTaggedFlag("CSSF", 8)
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => lsecssie.Value, name: "LSECSSF")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => hsi48rdyie.Value, name: "HSI48RDYF")
                .WithReservedBits(11, 21)
                ;

            Registers.ClockInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if (value) { lsirdyie.Value = false; } }, name: "LSIRDYC")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if (value) { lserdyie.Value = false; } }, name: "LSERDYC")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if (value) { msirdyie.Value = false; } }, name: "MSIRDYC")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if (value) { hsirdyie.Value = false; } }, name: "HSIRDYC")
                .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if (value) { hserdyie.Value = false; } }, name: "HSERDYC")
                .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if (value) { pllrdyie.Value = false; } }, name: "PLLRDYC")
                .WithFlag(6, FieldMode.Write, writeCallback: (_, value) => { if (value) { pllsai1rdyie.Value = false; } }, name: "PLLSAI1RDYC")
                .WithReservedBits(7, 1)
                .WithTaggedFlag("CSSC", 8)
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => lsecssie.Value, name: "LSECSSC")
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => hsi48rdyie.Value, name: "HSI48RDYC")
                .WithReservedBits(11, 21)
                ;

            Registers.Ahb1PeripheralReset.Define(this)
                .WithTaggedFlag("DMA1RST", 0)
                .WithTaggedFlag("DMA2RST", 1)
                .WithReservedBits(2, 6)
                .WithTaggedFlag("FLASHRST", 8)
                .WithReservedBits(9, 3)
                .WithTaggedFlag("CRCRST", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("TSCRST", 16)
                .WithReservedBits(17, 15)
                ;

            Registers.Ahb2PeripheralReset.Define(this)
                .WithTaggedFlag("GPIOARST", 0)
                .WithTaggedFlag("GPIOBRST", 1)
                .WithTaggedFlag("GPIOCRST", 2)
                .WithTaggedFlag("GPIODRST", 3)
                .WithTaggedFlag("GPIOERST", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("GPIOHRST", 7)
                .WithReservedBits(8, 5)
                .WithTaggedFlag("ADCRST", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("AESRST", 16)
                .WithReservedBits(17, 1)
                .WithTaggedFlag("RNGRST", 18)
                .WithReservedBits(19, 13)
                ;

            Registers.Ahb3PeripheralReset.Define(this)
                .WithReservedBits(0, 8)
                .WithTaggedFlag("QSPIRST", 8)
                .WithReservedBits(9, 23)
                ;

            Registers.Apb1PeripheralReset1.Define(this)
                .WithTaggedFlag("TIM2RST", 0)
                .WithTaggedFlag("TIM3RST", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("TIM6RST", 4)
                .WithTaggedFlag("TIM7RST", 5)
                .WithReservedBits(6, 3)
                .WithTaggedFlag("LCDRST", 9)
                .WithReservedBits(10, 4)
                .WithTaggedFlag("SPI2RST", 14)
                .WithTaggedFlag("SPI3RST", 15)
                .WithReservedBits(16, 1)
                .WithTaggedFlag("USART2RST", 17)
                .WithTaggedFlag("USART3RST", 18)
                .WithTaggedFlag("UART4RST", 19)
                .WithReservedBits(20, 1)
                .WithTaggedFlag("I2C1RST", 21)
                .WithTaggedFlag("I2C2RST", 22)
                .WithTaggedFlag("I2C3RST", 23)
                .WithTaggedFlag("CRSRST", 24)
                .WithTaggedFlag("CAN1RST", 25)
                .WithTaggedFlag("USBFSRST", 26)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("PWRRST", 28)
                .WithTaggedFlag("DAC1RST", 29)
                .WithTaggedFlag("OPAMPRST", 30)
                .WithFlag(31, writeCallback: (_, value) =>
                {
                    if (value)
                    {
                        (lptimer1 as IPeripheral)?.Reset();
                    }
                }, name: "LPTIM1RST")
                ;

            Registers.Apb1PeripheralReset2.Define(this)
                .WithTaggedFlag("LPUART1RST", 0)
                .WithTaggedFlag("I2C4RST", 1)
                .WithTaggedFlag("SWPMI1RST", 2)
                .WithReservedBits(3, 2)
                .WithFlag(5, writeCallback: (_, value) =>
                {
                    if (value)
                    {
                        (lptimer2 as IPeripheral)?.Reset();
                    }
                }, name: "LPTIM2RST")
                .WithReservedBits(6, 26)
                ;

            Registers.Apb2PeripheralReset.Define(this)
                .WithTaggedFlag("SYSCFGRST", 0)
                .WithReservedBits(1, 9)
                .WithTaggedFlag("SDMMC1RST", 10)
                .WithTaggedFlag("TIM1RST", 11)
                .WithTaggedFlag("SPI1RST", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("USART1RST", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("TIM15RST", 16)
                .WithTaggedFlag("TIM16RST", 17)
                .WithReservedBits(18, 3)
                .WithTaggedFlag("SAI1RST", 21)
                .WithReservedBits(22, 2)
                .WithTaggedFlag("DFSDM1RST", 24)
                .WithReservedBits(25, 7)
                ;

            Registers.Ahb1PeripheralClockEnable.Define(this, 0x00000100)
                .WithFlag(0, name: "DMA1EN")
                .WithFlag(1, name: "DMA2EN")
                .WithReservedBits(2, 6)
                .WithFlag(8, name: "FLASHEN")
                .WithReservedBits(9, 3)
                .WithFlag(12, name: "CRCEN")
                .WithReservedBits(13, 3)
                .WithFlag(16, name: "TSCEN")
                .WithReservedBits(17, 15)
                ;

            Registers.Ahb2PeripheralClockEnable.Define(this)
                .WithFlag(0, name: "GPIOAEN")
                .WithFlag(1, name: "GPIOBEN")
                .WithFlag(2, name: "GPIOCEN")
                .WithFlag(3, name: "GPIODEN")
                .WithFlag(4, name: "GPIOEEN")
                .WithReservedBits(5, 2)
                .WithFlag(7, name: "GPIOHEN")
                .WithReservedBits(8, 5)
                .WithFlag(13, name: "ADCEN")
                .WithReservedBits(14, 2)
                .WithFlag(16, name: "AESEN")
                .WithReservedBits(17, 1)
                .WithFlag(18, name: "RNGEN")
                .WithReservedBits(19, 13)
                ;

            Registers.Ahb3PeripheralClockEnable.Define(this)
                .WithReservedBits(0, 8)
                .WithFlag(8, name: "QSPIEN")
                .WithReservedBits(9, 23)
                ;

            Registers.Apb1PeripheralClockEnable1.Define(this, 0x00000400)
                .WithFlag(0, name: "TIM2EN")
                .WithFlag(1, name: "TIM3EN")
                .WithReservedBits(2, 2)
                .WithFlag(4, name: "TIM6EN")
                .WithFlag(5, name: "TIM7EN")
                .WithReservedBits(6, 3)
                .WithFlag(9, name: "LCDEN")
                .WithFlag(10, name: "RTCAPBEN")
                .WithFlag(11, name: "WWDGEN")
                .WithReservedBits(12, 2)
                .WithFlag(14, name: "SPI2EN")
                .WithFlag(15, name: "SPI3EN")
                .WithReservedBits(16, 1)
                .WithFlag(17, name: "USART2EN")
                .WithFlag(18, name: "USART3EN")
                .WithFlag(19, name: "UART4EN")
                .WithReservedBits(20, 1)
                .WithFlag(21, name: "I2C1EN")
                .WithFlag(22, name: "I2C2EN")
                .WithFlag(23, name: "I2C3EN")
                .WithFlag(24, name: "CRSEN")
                .WithFlag(25, name: "CAN1EN")
                .WithFlag(26, name: "USBFSEN")
                .WithReservedBits(27, 1)
                .WithFlag(28, name: "PWREN")
                .WithFlag(29, name: "DAC1EN")
                .WithFlag(30, name: "OPAMPEN")
                .WithFlag(31, name: "LPTIM1EN")
                ;

            Registers.Apb1PeripheralClockEnable2.Define(this)
                .WithFlag(0, name: "LPUART1EN")
                .WithFlag(1, name: "I2C4EN")
                .WithFlag(2, name: "SWPMI1EN")
                .WithReservedBits(3, 2)
                .WithFlag(5, name: "LPTIM2EN")
                .WithReservedBits(6, 26)
                ;

            Registers.Apb2PeripheralClockEnable.Define(this)
                .WithFlag(0, name: "SYSCFGEN")
                .WithReservedBits(1, 6)
                .WithFlag(7, name: "FWEN")
                .WithReservedBits(8, 2)
                .WithFlag(10, name: "SDMMC1EN")
                .WithFlag(11, name: "TIM1EN")
                .WithFlag(12, name: "SPI1EN")
                .WithReservedBits(13, 1)
                .WithFlag(14, name: "USART1EN")
                .WithReservedBits(15, 1)
                .WithFlag(16, name: "TIM15EN")
                .WithFlag(17, name: "TIM16EN")
                .WithReservedBits(18, 3)
                .WithFlag(21, name: "SAI1EN")
                .WithReservedBits(22, 2)
                .WithFlag(24, name: "DFSDM1EN")
                .WithReservedBits(25, 7)
                ;

            Registers.Ahb1PeripheralClockEnableInSleepMode.Define(this, 0x00011303)
                .WithFlag(0, name: "DMA1SMEN")
                .WithFlag(1, name: "DMA2SMEN")
                .WithReservedBits(2, 6)
                .WithFlag(8, name: "FLASHSMEN")
                .WithFlag(9, name: "SRAM1SMEN")
                .WithReservedBits(10, 2)
                .WithFlag(12, name: "CRCSMEN")
                .WithReservedBits(13, 3)
                .WithFlag(16, name: "TSCSMEN")
                .WithReservedBits(17, 15)
                ;

            Registers.Ahb2PeripheralClockEnableInSleepMode.Define(this, 0x0005229F)
                .WithFlag(0, name: "GPIOASMEN")
                .WithFlag(1, name: "GPIOBSMEN")
                .WithFlag(2, name: "GPIOCSMEN")
                .WithFlag(3, name: "GPIODSMEN")
                .WithFlag(4, name: "GPIOESMEN")
                .WithReservedBits(5, 2)
                .WithFlag(7, name: "GPIOHSMEN")
                .WithReservedBits(8, 5)
                .WithFlag(13, name: "ADCSMEN")
                .WithReservedBits(14, 2)
                .WithFlag(16, name: "AESSMEN")
                .WithReservedBits(17, 1)
                .WithFlag(18, name: "RNGSMEN")
                .WithReservedBits(19, 13)
                ;

            Registers.Ahb3PeripheralClockEnableInSleepMode.Define(this, 0x0000100)
                .WithReservedBits(0, 8)
                .WithTaggedFlag("QSPISMEN", 8)
                .WithReservedBits(9, 23)
                ;

            Registers.Apb1PeripheralClockEnableInSleepMode1.Define(this, 0xF7E6CE31)
                .WithFlag(0, name: "TIM2SMEN")
                .WithFlag(1, name: "TIM3SMEN")
                .WithReservedBits(2, 2)
                .WithFlag(4, name: "TIM6SMEN")
                .WithFlag(5, name: "TIM7SMEN")
                .WithReservedBits(6, 3)
                .WithFlag(9, name: "LCDSMEN")
                .WithFlag(10, name: "RTCAPBSMEN")
                .WithFlag(11, name: "WWDGSMEN")
                .WithReservedBits(12, 2)
                .WithFlag(14, name: "SPI2SMEN")
                .WithFlag(15, name: "SPI3SMEN")
                .WithReservedBits(16, 1)
                .WithFlag(17, name: "USART2SMEN")
                .WithFlag(18, name: "USART3SMEN")
                .WithFlag(19, name: "UART4SMEN")
                .WithReservedBits(20, 1)
                .WithFlag(21, name: "I2C1SMEN")
                .WithFlag(22, name: "I2C2SMEN")
                .WithFlag(23, name: "I2C3SMEN")
                .WithFlag(24, name: "CRSSMEN")
                .WithFlag(25, name: "CAN1SMEN")
                .WithFlag(26, name: "USBFSSMEN")
                .WithReservedBits(27, 1)
                .WithFlag(28, name: "PWRSMEN")
                .WithFlag(29, name: "DAC1SMEN")
                .WithFlag(30, name: "OPAMPSMEN")
                .WithFlag(31, name: "LPTIM1SMEN")
                ;

            Registers.Apb1PeripheralClockEnableInSleepMode2.Define(this, 0x00000025)
                .WithFlag(0, name: "LPUART1SMEN")
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "SWPMI1SMEN")
                .WithReservedBits(3, 2)
                .WithFlag(5, name: "LPTIM2SMEN")
                .WithReservedBits(6, 26)
                ;

            Registers.Apb2PeripheralClockEnableInSleepMode.Define(this, 0x02357C01)
                .WithFlag(0, name: "SYSCFGSMEN")
                .WithReservedBits(1, 9)
                .WithFlag(10, name: "SDMMC1SMEN")
                .WithFlag(11, name: "TIM1SMEN")
                .WithFlag(12, name: "SPI1SMEN")
                .WithReservedBits(13, 1)
                .WithFlag(14, name: "USART1SMEN")
                .WithReservedBits(15, 1)
                .WithFlag(16, name: "TIM15SMEN")
                .WithFlag(17, name: "TIM16SMEN")
                .WithReservedBits(18, 3)
                .WithFlag(21, name: "SAI1SMEN")
                .WithReservedBits(22, 10)
                ;

            Registers.ClockConfigurationCcipr.Define(this)
                .WithValueField(0, 2, name: "USART1SEL")
                .WithValueField(2, 2, name: "USART2SEL")
                .WithValueField(4, 2, name: "USART3SEL")
                .WithValueField(6, 2, name: "UART4SEL")
                .WithReservedBits(8, 2)
                .WithValueField(10, 2, name: "LPUART1SEL")
                .WithValueField(12, 2, name: "I2C1SEL")
                .WithValueField(14, 2, name: "I2C2SEL")
                .WithValueField(16, 2, name: "I2C3SEL")
                .WithEnumField<DoubleWordRegister, LpTimerClockSourceSelection>(18, 2, out lpTimer1Selection, name: "LPTIM1SEL")
                .WithEnumField<DoubleWordRegister, LpTimerClockSourceSelection>(20, 2, out lpTimer2Selection, name: "LPTIM2SEL")
                .WithTaggedFlag("SAI1SEL0", 22)
                .WithTaggedFlag("SAI1SEL1", 23)
                .WithReservedBits(24, 2)
                .WithTaggedFlag("CLK48SEL0", 26)
                .WithTaggedFlag("CLK48SEL1", 27)
                .WithValueField(28, 2, name: "ADCSEL")
                .WithValueField(30, 1, name: "SWPMI1SEL")
                .WithReservedBits(31, 1)
                ;

            Registers.ControlStatus.Define(this, 0x0C000600)
                .WithFlag(0, out var lsion, name: "LSION")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lsion.Value, name: "LSIRDY")
                .WithReservedBits(2, 2)
                .WithFlag(4, name: "LSIPREDIV")
                .WithReservedBits(5, 3)
                .WithValueField(8, 4, name: "MSISRANGE",
                    writeCallback: (previous, value) =>
                    {
                        if (msion.Value == true && previous != value)
                        {
                            this.Log(LogLevel.Error, "MSISRANGE modified when MSION");
                            return;
                        }

                        if (msirgsel.Value != false)
                        {
                            this.Log(LogLevel.Error, "MSISRANGE can only be modified when MSIRGSEL=0");
                            return;
                        }

                        switch (value)
                        {
                            case 4:
                                MsiFrequency_1 = 1000000;
                                break;
                            case 5:
                                MsiFrequency_1 = 2000000;
                                break;
                            case 6:
                                MsiFrequency_1 = 4000000;  // Default
                                break;
                            case 7:
                                MsiFrequency_1 = 8000000;
                                break;
                            default:
                                this.Log(LogLevel.Error, "Invalid MSI srange: {0}", value);
                                break;
                        }
                    })
                .WithReservedBits(12, 11)
                .WithTaggedFlag("RMVF", 23)
                .WithTaggedFlag("FWRSTF", 24)
                .WithTaggedFlag("OBLRSTF", 25)
                .WithTaggedFlag("PINRSTF", 26)
                .WithTaggedFlag("BORRSTF", 27)
                .WithTaggedFlag("SFTRSTF", 28)
                .WithTaggedFlag("IWDGRSTF", 29)
                .WithTaggedFlag("WWDGRSTF", 30)
                .WithTaggedFlag("LPWRRSTF", 31)
                ;

            Registers.ClockRecoveryRc.Define(this)
                .WithFlag(0, out var hsi48on, name: "HSI48ON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => hsi48on.Value, name: "HSI48RDY")
                .WithReservedBits(2, 5)
                .WithValueField(8, 8, name: "HSI48CAL")
                .WithReservedBits(16, 16)
                .WithWriteCallback((_, __) => UpdateClocks())
                ;

            Registers.ClockConfigurationCcipr2.Define(this)
                .WithValueField(0, 2, name: "I2C4SEL")
                .WithReservedBits(2, 30)
                ;
        }

        private long Hsi16Frequency { get => 16000000; }
        private long PllFrequency
        {
            get
            {
                switch (pllSource.Value)
                {
                    default:
                    case PllSourceSelection.None:
                        return 0;
                    case PllSourceSelection.Msi:
                        return MsiFrequency * pllMultiplier / pllDivisor;
                    case PllSourceSelection.Hsi16:
                        return Hsi16Frequency * pllMultiplier / pllDivisor;
                    case PllSourceSelection.Hse:
                        return hseFrequency * pllMultiplier / pllDivisor;
                }
            }
        }
        private long MsiFrequency {
            get => msirgsel.Value ? MsiFrequency_0 : MsiFrequency_1;
        }
        private long MsiFrequency_0;   // from MSIRANGE
        private long MsiFrequency_1;   // from MSISRANGE
        private IFlagRegisterField msirgsel;

        private IEnumRegisterField<PllSourceSelection> pllSource;
        private IEnumRegisterField<SystemClockSourceSelection> systemClockSwitch;
        private IEnumRegisterField<LpTimerClockSourceSelection> lpTimer1Selection;
        private IEnumRegisterField<LpTimerClockSourceSelection> lpTimer2Selection;
        private IFlagRegisterField pllOn;

        private long pllMultiplier;
        private long pllDivisor;
        private long msiMultiplier;
        private long pll48Divisor;    // for USB, RNG, SDMMC

        private readonly long apbFrequency;
        private readonly long lsiFrequency;
        private readonly long lseFrequency;
        private readonly long hseFrequency;

        private readonly IHasDivisibleFrequency systick;
        private readonly IPeripheral rtc;
        private readonly ITimer lptimer1;
        private readonly ITimer lptimer2;

        private const long DefaultApbFrequency = 32000000;
        private const long DefaultLsiFrequency = 37000;
        private const long DefaultLseFrequency = 32768;
        private const long DefaultHseFrequency = 16000000;
        private const long DefaultMsiFrequency = 4000000;

        // There can't be one common ClockSourceSelection enum because different peripherals
        // have different sets of possible values:
        // I2C has APB, system clock, HSI16, reserved;
        // UARTs have APB, system clock, HSI16, LSE.
        // The system clock can be HSI16, HSE, PLL, MSI (default)
        private enum LpTimerClockSourceSelection
        {
            Apb,
            Lsi,
            Hsi16,
            Lse,
        }

        private enum SystemClockSourceSelection
        {
            Msi,
            Hsi16,
            Hse,
            Pll,
        }

        private enum PllSourceSelection
        {
            None,
            Msi,
            Hsi16,
            Hse,
        }

        private enum Registers
        {
            ClockControl = 0x0,                     // Done
            InternalClockSourcesCalibration = 0x4,  // Done
            ClockConfigurationCfgr = 0x8,           // Done
            PLLConfiguration = 0xC,                 // Done
            PLLSAI1Configuration = 0x10,
            ClockInterruptEnable = 0x18,            // Done
            ClockInterruptFlag = 0x1C,              // Done
            ClockInterruptClear = 0x20,             // Done
            Ahb1PeripheralReset = 0x28,             // Done
            Ahb2PeripheralReset = 0x2C,             // Done
            Ahb3PeripheralReset = 0x30,             // Done
            Apb1PeripheralReset1 = 0x38,            // Done
            Apb1PeripheralReset2 = 0x3C,            // Done
            Apb2PeripheralReset = 0x40,             // Done
            Ahb1PeripheralClockEnable = 0x48,       // Done
            Ahb2PeripheralClockEnable = 0x4C,       // Done
            Ahb3PeripheralClockEnable = 0x50,       // Done
            Apb1PeripheralClockEnable1 = 0x58,      // Done
            Apb1PeripheralClockEnable2 = 0x5C,      // Done
            Apb2PeripheralClockEnable = 0x60,       // Done
            Ahb1PeripheralClockEnableInSleepMode = 0x68, // Done
            Ahb2PeripheralClockEnableInSleepMode = 0x6C, // Done
            Ahb3PeripheralClockEnableInSleepMode = 0x70, // Done
            Apb1PeripheralClockEnableInSleepMode1 = 0x78, // Done
            Apb1PeripheralClockEnableInSleepMode2 = 0x7C, // Done
            Apb2PeripheralClockEnableInSleepMode = 0x80,  // Done
            ClockConfigurationCcipr = 0x88,         // Done
            BackupDomainControl = 0x90,             // Done
            ControlStatus = 0x94,                   // Done
            ClockRecoveryRc = 0x98,                 // Done
            ClockConfigurationCcipr2 = 0x9C,        // Done
        }
    }
}
