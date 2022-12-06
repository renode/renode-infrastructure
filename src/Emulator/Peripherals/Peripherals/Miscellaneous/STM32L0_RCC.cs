//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32L0_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32L0_RCC(Machine machine, IPeripheral rtc = null, ITimer lptimer = null, long apbFrequency = DefaultApbFrequency, long lsiFrequency = DefaultLsiFrequency, long lseFrequency = DefaultLseFrequency) : base(machine)
        {
            this.rtc = rtc;
            this.lptimer = lptimer;
            this.apbFrequency = apbFrequency;
            this.lsiFrequency = lsiFrequency;
            this.lseFrequency = lseFrequency;
            DefineRegisters();
            Reset();
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            // Keep in mind that most of these registers do not affect other
            // peripherals or their clocks.
            Registers.ClockControl.Define(this, 0x300)
                .WithFlag(0, out var hsi16on, name: "HSI16ON")
                .WithFlag(1, name: "HSI16KERON")
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
                .WithFlag(18, name: "HSEBYP")
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
                .WithFlag(15, name: "STOPWUCK")
                .WithTaggedFlag("PLLSRC", 16)
                .WithReservedBits(17, 1)
                .WithValueField(18, 4, name: "PLLMUL")
                .WithValueField(22, 2, name: "PLLDIV")
                .WithValueField(24, 4, name: "MCOSEL")
                .WithValueField(28, 3, name: "MCOPRE")
                ;

            Registers.ClockInterruptEnable.Define(this)
                .WithFlag(0, out var lsirdyie, name: "LSIRDYIE")
                .WithFlag(1, out var lserdyie, name: "LSERDYIE")
                .WithFlag(2, out var hsi16rdyie, name: "HSI16RDYIE")
                .WithFlag(3, out var hserdyie, name: "HSERDYIE")
                .WithFlag(4, out var pllrdyie, name: "PLLRDYIE")
                .WithFlag(5, out var msirdyie, name: "MSIRDYIE")
                .WithFlag(6, out var hsi48rdyie, name: "HSI48RDYIE")
                .WithFlag(7, name: "CSSLSE")
                .WithReservedBits(8, 24)
                ;

            Registers.ClockInterruptFlag.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => lsirdyie.Value, name: "LSIRDYF")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lserdyie.Value, name: "LSERDYF")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => hsi16rdyie.Value, name: "HSI16RDYF")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => hserdyie.Value, name: "HSERDYF")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => pllrdyie.Value, name: "PLLRDYF")
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => msirdyie.Value, name: "MSIRDYF")
                .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => hsi48rdyie.Value, name: "HSI48RDYF")
                .WithTaggedFlag("CSSLSEF", 7)
                .WithTaggedFlag("CSSHSEF", 8)
                .WithReservedBits(9, 23)
                ;

            Registers.ClockInterruptClear.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, value) => { if(value) { lsirdyie.Value = false; } }, name: "LSIRDYC")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, value) => { if(value) { lserdyie.Value = false; } }, name: "LSERDYC")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, value) => { if(value) { hsi16rdyie.Value = false;  } }, name: "HSI16RDYC")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, value) => { if(value) { hserdyie.Value = false;  } }, name: "HSERDYC")
                .WithFlag(4, FieldMode.Write, writeCallback: (_, value) => { if(value) { pllrdyie.Value = false; } }, name: "PLLRDYC")
                .WithFlag(5, FieldMode.Write, writeCallback: (_, value) => { if(value) { msirdyie.Value = false; } }, name: "MSIRDYC")
                .WithFlag(6, FieldMode.Write, writeCallback: (_, value) => { if(value) { hsi48rdyie.Value = false; } }, name: "HSI48RDYC")
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

            // Most clock enable bits are flags instead of tags to reduce the number of warnings in the log
            Registers.IoPortClockEnable.Define(this)
                .WithFlag(0, name: "IOPAEN")
                .WithFlag(1, name: "IOPBEN")
                .WithFlag(2, name: "IOPCEN")
                .WithFlag(3, name: "IOPDEN")
                .WithFlag(4, name: "IOPEEN")
                .WithReservedBits(5, 2)
                .WithFlag(7, name: "IOPHEN")
                ;

            Registers.AhbPeripheralClockEnable.Define(this, 0x100)
                .WithFlag(0, name: "DMAEN")
                .WithReservedBits(1, 7)
                .WithFlag(8, name: "MIFEN")
                .WithReservedBits(9, 3)
                .WithFlag(12, name: "CRCEN")
                .WithReservedBits(13, 3)
                .WithFlag(16, name: "TSCEN")
                .WithReservedBits(17, 3)
                .WithFlag(20, name: "RNGEN")
                .WithReservedBits(21, 3)
                .WithFlag(24, name: "CRYPEN")
                .WithReservedBits(25, 7)
                ;

            Registers.Apb2PeripheralClockEnable.Define(this)
                .WithFlag(0, name: "SYSCFEN")
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "TIM21EN")
                .WithReservedBits(3, 2)
                .WithFlag(5, name: "TIM22EN")
                .WithReservedBits(6, 1)
                .WithFlag(7, name: "FWEN")
                .WithReservedBits(8, 1)
                .WithFlag(9, name: "ADCEN")
                .WithReservedBits(10, 2)
                .WithFlag(12, name: "SPI1EN")
                .WithReservedBits(13, 1)
                .WithFlag(14, name: "USART1EN")
                .WithReservedBits(15, 7)
                .WithFlag(22, name: "DBGEN")
                .WithReservedBits(23, 9)
                ;

            Registers.Apb1PeripheralClockEnable.Define(this)
                .WithFlag(0, name: "TIM2EN")
                .WithFlag(1, name: "TIM3EN")
                .WithReservedBits(2, 2)
                .WithFlag(4, name: "TIM6EN")
                .WithFlag(5, name: "TIM7EN")
                .WithReservedBits(6, 3)
                .WithFlag(9, name: "LCDEN")
                .WithReservedBits(10, 1)
                .WithFlag(11, name: "WWDGEN")
                .WithReservedBits(12, 2)
                .WithFlag(14, name: "SPI2EN")
                .WithReservedBits(15, 2)
                .WithFlag(17, name: "USART2EN")
                .WithFlag(18, name: "LPUART1EN")
                .WithFlag(19, name: "USART4EN")
                .WithFlag(20, name: "USART5EN")
                .WithFlag(21, name: "I2C1EN")
                .WithFlag(22, name: "I2C2EN")
                .WithFlag(23, name: "USBEN")
                .WithReservedBits(24, 3)
                .WithFlag(27, name: "CRSEN")
                .WithFlag(28, name: "PWREN")
                .WithFlag(29, name: "DACEN")
                .WithFlag(30, name: "I2C3EN")
                .WithFlag(31, name: "LPTIM1EN")
                ;

            Registers.IoPortClockEnableInSleepMode.Define(this)
                .WithFlag(0, name: "IOPASMEN")
                .WithFlag(1, name: "IOPBSMEN")
                .WithFlag(2, name: "IOPCSMEN")
                .WithFlag(3, name: "IOPDSMEN")
                .WithFlag(4, name: "IOPESMEN")
                .WithReservedBits(5, 2)
                .WithFlag(7, name: "IOPHSMEN")
                ;

            Registers.AhbPeripheralClockEnableInSleepMode.Define(this, 0x100)
                .WithFlag(0, name: "DMASMEN")
                .WithReservedBits(1, 7)
                .WithFlag(8, name: "MIFSMEN")
                .WithReservedBits(9, 3)
                .WithFlag(12, name: "CRCSMEN")
                .WithReservedBits(13, 3)
                .WithFlag(16, name: "TSCSMEN")
                .WithReservedBits(17, 3)
                .WithFlag(20, name: "RNGSMEN")
                .WithReservedBits(21, 3)
                .WithFlag(24, name: "CRYPSMEN")
                .WithReservedBits(25, 7)
                ;

            Registers.Apb2PeripheralClockEnableInSleepMode.Define(this)
                .WithFlag(0, name: "SYSCFSMEN")
                .WithReservedBits(1, 1)
                .WithFlag(2, name: "TIM21SMEN")
                .WithReservedBits(3, 2)
                .WithFlag(5, name: "TIM22SMEN")
                .WithReservedBits(6, 1)
                .WithFlag(7, name: "FWSMEN")
                .WithReservedBits(8, 1)
                .WithFlag(9, name: "ADCSMEN")
                .WithReservedBits(10, 2)
                .WithFlag(12, name: "SPI1SMEN")
                .WithReservedBits(13, 1)
                .WithFlag(14, name: "USART1SMEN")
                .WithReservedBits(15, 7)
                .WithFlag(22, name: "DBGSMEN")
                .WithReservedBits(23, 9)
                ;

            Registers.Apb1PeripheralClockEnableInSleepMode.Define(this)
                .WithFlag(0, name: "TIM2SMEN")
                .WithFlag(1, name: "TIM3SMEN")
                .WithReservedBits(2, 2)
                .WithFlag(4, name: "TIM6SMEN")
                .WithFlag(5, name: "TIM7SMEN")
                .WithReservedBits(6, 3)
                .WithFlag(9, name: "LCDSMEN")
                .WithReservedBits(10, 1)
                .WithFlag(11, name: "WWDGSMEN")
                .WithReservedBits(12, 2)
                .WithFlag(14, name: "SPI2SMEN")
                .WithReservedBits(15, 2)
                .WithFlag(17, name: "USART2SMEN")
                .WithFlag(18, name: "LPUART1SMEN")
                .WithFlag(19, name: "USART4SMEN")
                .WithFlag(20, name: "USART5SMEN")
                .WithFlag(21, name: "I2C1SMEN")
                .WithFlag(22, name: "I2C2SMEN")
                .WithFlag(23, name: "USBSMEN")
                .WithReservedBits(24, 3)
                .WithFlag(27, name: "CRSSMEN")
                .WithFlag(28, name: "PWRSMEN")
                .WithFlag(29, name: "DACSMEN")
                .WithFlag(30, name: "I2C3SMEN")
                .WithFlag(31, name: "LPTIM1SMEN")
                ;

            Registers.ClockConfigurationCcipr.Define(this)
                .WithValueField(0, 2, name: "USART1SEL")
                .WithValueField(2, 2, name: "USART2SEL")
                .WithReservedBits(4, 6)
                .WithValueField(10, 2, name: "LPUART1SEL")
                .WithValueField(12, 2, name: "I2C1SEL")
                .WithReservedBits(14, 2)
                .WithValueField(16, 2, name: "I2C3SEL")
                .WithEnumField<DoubleWordRegister, LpTimerClockSourceSelection>(18, 2, changeCallback: (_, value) =>
                    {
                        if(lptimer == null) 
                        {
                            this.Log(LogLevel.Error, "Trying to change LPTimer frequency, but it was not passed in the RCC constructor, ignoring");
                            return;
                        }
                        switch(value)
                        {
                            case LpTimerClockSourceSelection.Apb:
                                lptimer.Frequency = apbFrequency;
                                break;
                            case LpTimerClockSourceSelection.Lsi:
                                lptimer.Frequency = lsiFrequency;
                                break;
                            case LpTimerClockSourceSelection.Hsi16:
                                lptimer.Frequency = Hsi16Frequency;
                                break;
                            case LpTimerClockSourceSelection.Lse:
                                lptimer.Frequency = lseFrequency;
                                break;
                        }
                        this.Log(LogLevel.Debug, "LpTimer clock frequency changed to {0}", lptimer.Frequency);
                    }, name: "LPTIM1SEL")
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
                .WithFlag(18, writeCallback: (_, value) =>
                    {
                        if(rtc == null && value)
                        {
                            this.Log(LogLevel.Error, "Trying to enable RTC, but it was not passed int the RCC constructor, ignoring");
                            return;
                        }
                        machine.SystemBus.SetPeripheralEnabled(rtc, value);
                    },
                    valueProviderCallback: _ => rtc == null ? false : machine.SystemBus.IsPeripheralEnabled(rtc), name: "RTCEN")
                .WithFlag(19, writeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            if(rtc == null)
                            {
                                this.Log(LogLevel.Error, "Trying to disable RTC, but it was not passed int the RCC constructor, ignoring");
                                return;
                            }
                            rtc.Reset();
                            machine.SystemBus.DisablePeripheral(rtc);
                        }
                    }, name: "RTCRST")
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

        private const long DefaultApbFrequency = 32000000;
        private const long DefaultLsiFrequency = 37000;
        private const long DefaultLseFrequency = 32768;
        private const long Hsi16Frequency = 16000000;

        private readonly IPeripheral rtc;
        private readonly ITimer lptimer;
        private readonly long apbFrequency;
        private readonly long lsiFrequency;
        private readonly long lseFrequency;

        // There can't be one common ClockSourceSelection enum because different peripherals
        // have different sets of possible values:
        // I2C has APB, system clock, HSI16, reserved;
        // UARTs have APB, system clock, HSI16, LSE.
        // The system clock can be HSI16, HSE, PLL, MSI (default)
        public enum LpTimerClockSourceSelection
        {
            Apb,
            Lsi,
            Hsi16,
            Lse,
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
