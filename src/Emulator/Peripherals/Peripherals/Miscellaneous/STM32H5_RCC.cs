//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

// Known limitation, deliberate for this version:
// The peripheral clock enable (*ENR) and peripheral reset (*RSTR) register blocks are
// storage only. Writes are preserved for read-back, but the model does not gate the clock
// of the addressed peripheral and does not assert its reset.
//
// Modelling this would mean propagating enable and reset state from each *ENR/*RSTR bit to
// the corresponding modelled peripheral instance, so that accessing a clock-gated
// peripheral is diagnosable and a reset request actually resets the peripheral's state.
// Nothing in the currently supported firmware depends on it.

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32H5_RCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public STM32H5_RCC(IMachine machine,
            IHasFrequency nvic = null,
            IHasFrequency usart3 = null,
            ulong hseFrequency = DefaultHseFrequency,
            ulong lseFrequency = DefaultLseFrequency,
            ulong lsiFrequency = DefaultLsiFrequency) : base(machine)
        {
            this.nvic = nvic;
            this.usart3 = usart3;
            this.hseFrequency = hseFrequency;
            this.lseFrequency = lseFrequency;
            this.lsiFrequency = lsiFrequency;
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            UpdateClocks();
        }

        public long Size => 0x400;

        // Clock propagation targets:
        //   nvic   — HCLK, so SysTick (HAL_GetTick) follows the real clock tree
        //   usart3 — PCLK1, for baud rate generation (wired in task 9.2 once STM32F7_USART
        //            implements IHasFrequency; null until then — staged, not forgotten)
        //
        // Deliberately excluded:
        //   IWDG — LSI-clocked at a fixed 32 kHz; its constructor frequency is already correct.

        private static void TrySetFrequency(IHasFrequency peripheral, ulong frequency)
        {
            if(peripheral != null)
            {
                peripheral.Frequency = frequency;
            }
        }

        private void UpdateClocks()
        {
            TrySetFrequency(nvic, HclkFrequency);
            TrySetFrequency(usart3, Pclk1Frequency);
        }

        private void DefineRegisters()
        {
            // Reset values from RM0481 Rev 4, Section 11.8 (RCC register descriptions):
            //   CR        = 0x0000_0023 (HSION, HSIRDY, HSIDIVF set)
            //   CFGR1     = 0x0000_0000
            //   CFGR2     = 0x0000_0000
            //   PLL1CFGR  = 0x0000_0000
            //   PLL1DIVR  = 0x0101_0280 (PLL1N=128, PLL1P=1, PLL1Q=1, PLL1R=1)

            Registers.ClockControl.Define(this, 0x23)
                .WithFlag(0, out var hsiOn, name: "HSION")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => hsiOn.Value, name: "HSIRDY")
                .WithFlag(2, name: "HSIKERON")
                .WithValueField(3, 2, out hsidiv, name: "HSIDIV", writeCallback: (_, __) => { hsidivf.Value = true; })
                .WithFlag(5, out hsidivf, name: "HSIDIVF")
                .WithReservedBits(6, 2)
                .WithFlag(8, out var csiOn, name: "CSION")
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => csiOn.Value, name: "CSIRDY")
                .WithFlag(10, name: "CSIKERON")
                .WithReservedBits(11, 1)
                .WithFlag(12, out var hsi48On, name: "HSI48ON")
                .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => hsi48On.Value, name: "HSI48RDY")
                .WithReservedBits(14, 2)
                .WithFlag(16, out var hseOn, name: "HSEON")
                .WithFlag(17, FieldMode.Read, valueProviderCallback: _ => hseOn.Value, name: "HSERDY")
                .WithFlag(18, name: "HSEBYP")
                .WithFlag(19, name: "HSECSSON")
                .WithFlag(20, name: "HSEEXT")
                .WithReservedBits(21, 3)
                .WithFlag(24, out var pll1On, name: "PLL1ON")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => pll1On.Value, name: "PLL1RDY")
                .WithFlag(26, out var pll2On, name: "PLL2ON")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => pll2On.Value, name: "PLL2RDY")
                .WithFlag(28, out var pll3On, name: "PLL3ON")
                .WithFlag(29, FieldMode.Read, valueProviderCallback: _ => pll3On.Value, name: "PLL3RDY")
                .WithReservedBits(30, 2)
                .WithChangeCallback((_, __) => UpdateClocks());

            Registers.HSICalibration.Define(this)
                .WithTag("HSICFGR", 0, 32);

            Registers.ClockRecoveryRC.Define(this)
                .WithTag("CRRCR", 0, 32);

            Registers.CSICalibration.Define(this)
                .WithTag("CSICFGR", 0, 32);

            Registers.ClockConfiguration1.Define(this)
                .WithValueField(0, 3, out systemClockSwitch, name: "SW")
                .WithValueField(3, 3, FieldMode.Read, valueProviderCallback: _ => systemClockSwitch.Value, name: "SWS")
                .WithFlag(6, name: "STOPWUCK")
                .WithFlag(7, name: "STOPKERWUCK")
                .WithValueField(8, 6, name: "RTCPRE")
                .WithReservedBits(14, 1)
                .WithFlag(15, name: "TIMPRE")
                .WithReservedBits(16, 2)
                .WithValueField(18, 4, name: "MCO1PRE")
                .WithValueField(22, 3, name: "MCO1")
                .WithValueField(25, 4, name: "MCO2PRE")
                .WithValueField(29, 3, name: "MCO2")
                .WithChangeCallback((_, __) => UpdateClocks());

            Registers.ClockConfiguration2.Define(this)
                .WithValueField(0, 4, out hpre, name: "HPRE")
                .WithValueField(4, 3, out ppre1, name: "PPRE1")
                .WithReservedBits(7, 1)
                .WithValueField(8, 3, name: "PPRE2")
                .WithReservedBits(11, 1)
                .WithValueField(12, 3, name: "PPRE3")
                .WithReservedBits(15, 1)
                .WithFlag(16, name: "AHB1DIS")
                .WithFlag(17, name: "AHB2DIS")
                .WithReservedBits(18, 1)
                .WithFlag(19, name: "AHB4DIS")
                .WithFlag(20, name: "APB1DIS")
                .WithFlag(21, name: "APB2DIS")
                .WithFlag(22, name: "APB3DIS")
                .WithReservedBits(23, 9)
                .WithChangeCallback((_, __) => UpdateClocks());

            Registers.PLL1Configuration.Define(this)
                .WithValueField(0, 2, out pll1Src, name: "PLL1SRC")
                .WithValueField(2, 2, name: "PLL1RGE")
                .WithFlag(4, out pll1FracEn, name: "PLL1FRACEN")
                .WithFlag(5, name: "PLL1VCOSEL")
                .WithReservedBits(6, 2)
                .WithValueField(8, 6, out pll1M, name: "PLL1M")
                .WithReservedBits(14, 2)
                .WithFlag(16, name: "PLL1PEN")
                .WithFlag(17, name: "PLL1QEN")
                .WithFlag(18, name: "PLL1REN")
                .WithReservedBits(19, 13)
                .WithChangeCallback((_, __) => UpdateClocks());

            Registers.PLL2Configuration.Define(this)
                .WithValueField(0, 2, name: "PLL2SRC")
                .WithValueField(2, 2, name: "PLL2RGE")
                .WithFlag(4, name: "PLL2FRACEN")
                .WithFlag(5, name: "PLL2VCOSEL")
                .WithReservedBits(6, 2)
                .WithValueField(8, 6, name: "PLL2M")
                .WithReservedBits(14, 2)
                .WithFlag(16, name: "PLL2PEN")
                .WithFlag(17, name: "PLL2QEN")
                .WithFlag(18, name: "PLL2REN")
                .WithReservedBits(19, 13);

            Registers.PLL3Configuration.Define(this)
                .WithValueField(0, 2, name: "PLL3SRC")
                .WithValueField(2, 2, name: "PLL3RGE")
                .WithFlag(4, name: "PLL3FRACEN")
                .WithFlag(5, name: "PLL3VCOSEL")
                .WithReservedBits(6, 2)
                .WithValueField(8, 6, name: "PLL3M")
                .WithReservedBits(14, 2)
                .WithFlag(16, name: "PLL3PEN")
                .WithFlag(17, name: "PLL3QEN")
                .WithFlag(18, name: "PLL3REN")
                .WithReservedBits(19, 13);

            Registers.PLL1Dividers.Define(this, 0x01010280)
                .WithValueField(0, 9, out pll1N, name: "PLL1N")
                .WithValueField(9, 7, out pll1P, name: "PLL1P")
                .WithValueField(16, 7, name: "PLL1Q")
                .WithReservedBits(23, 1)
                .WithValueField(24, 7, name: "PLL1R")
                .WithReservedBits(31, 1)
                .WithChangeCallback((_, __) => UpdateClocks());

            Registers.PLL1Fractional.Define(this)
                .WithReservedBits(0, 3)
                .WithValueField(3, 13, out pll1FracN, name: "PLL1FRACN")
                .WithReservedBits(16, 16)
                .WithChangeCallback((_, __) => UpdateClocks());

            Registers.PLL2Dividers.Define(this)
                .WithValueField(0, 9, name: "PLL2N")
                .WithValueField(9, 7, name: "PLL2P")
                .WithValueField(16, 7, name: "PLL2Q")
                .WithReservedBits(23, 1)
                .WithValueField(24, 7, name: "PLL2R")
                .WithReservedBits(31, 1);

            Registers.PLL2Fractional.Define(this)
                .WithReservedBits(0, 3)
                .WithValueField(3, 13, name: "PLL2FRACN")
                .WithReservedBits(16, 16);

            Registers.PLL3Dividers.Define(this)
                .WithValueField(0, 9, name: "PLL3N")
                .WithValueField(9, 7, name: "PLL3P")
                .WithValueField(16, 7, name: "PLL3Q")
                .WithReservedBits(23, 1)
                .WithValueField(24, 7, name: "PLL3R")
                .WithReservedBits(31, 1);

            Registers.PLL3Fractional.Define(this)
                .WithReservedBits(0, 3)
                .WithValueField(3, 13, name: "PLL3FRACN")
                .WithReservedBits(16, 16);

            // CIER/CIFR/CICR: storage only, no interrupt generation
            Registers.ClockInterruptEnable.Define(this)
                .WithTag("CIER", 0, 32);

            Registers.ClockInterruptFlag.Define(this)
                .WithTag("CIFR", 0, 32);

            Registers.ClockInterruptClear.Define(this)
                .WithTag("CICR", 0, 32);

            // Reset registers: storage only (see file header comment)
            Registers.AHB1Reset.Define(this)
                .WithTag("AHB1RSTR", 0, 32);

            Registers.AHB2Reset.Define(this)
                .WithTag("AHB2RSTR", 0, 32);

            Registers.AHB4Reset.Define(this)
                .WithTag("AHB4RSTR", 0, 32);

            Registers.APB1LowReset.Define(this)
                .WithTag("APB1LRSTR", 0, 32);

            Registers.APB1HighReset.Define(this)
                .WithTag("APB1HRSTR", 0, 32);

            Registers.APB2Reset.Define(this)
                .WithTag("APB2RSTR", 0, 32);

            Registers.APB3Reset.Define(this)
                .WithTag("APB3RSTR", 0, 32);

            // Enable registers: storage only (see file header comment)
            Registers.AHB1Enable.Define(this)
                .WithTag("AHB1ENR", 0, 32);

            Registers.AHB2Enable.Define(this)
                .WithTag("AHB2ENR", 0, 32);

            Registers.AHB4Enable.Define(this)
                .WithTag("AHB4ENR", 0, 32);

            Registers.APB1LowEnable.Define(this)
                .WithTag("APB1LENR", 0, 32);

            Registers.APB1HighEnable.Define(this)
                .WithTag("APB1HENR", 0, 32);

            Registers.APB2Enable.Define(this)
                .WithTag("APB2ENR", 0, 32);

            Registers.APB3Enable.Define(this)
                .WithTag("APB3ENR", 0, 32);

            // Low-power enable registers: storage only
            Registers.AHB1LowPowerEnable.Define(this)
                .WithTag("AHB1LPENR", 0, 32);

            Registers.AHB2LowPowerEnable.Define(this)
                .WithTag("AHB2LPENR", 0, 32);

            Registers.AHB4LowPowerEnable.Define(this)
                .WithTag("AHB4LPENR", 0, 32);

            Registers.APB1LowLowPowerEnable.Define(this)
                .WithTag("APB1LLPENR", 0, 32);

            Registers.APB1HighLowPowerEnable.Define(this)
                .WithTag("APB1HLPENR", 0, 32);

            Registers.APB2LowPowerEnable.Define(this)
                .WithTag("APB2LPENR", 0, 32);

            Registers.APB3LowPowerEnable.Define(this)
                .WithTag("APB3LPENR", 0, 32);

            // Clock configuration input peripheral registers: storage for now
            Registers.ClockConfigurationInput1.Define(this)
                .WithValueField(0, 32, name: "CCIPR1");

            Registers.ClockConfigurationInput2.Define(this)
                .WithValueField(0, 32, name: "CCIPR2");

            Registers.ClockConfigurationInput3.Define(this)
                .WithValueField(0, 32, name: "CCIPR3");

            Registers.ClockConfigurationInput4.Define(this)
                .WithValueField(0, 32, name: "CCIPR4");

            Registers.ClockConfigurationInput5.Define(this)
                .WithValueField(0, 32, name: "CCIPR5");

            Registers.BackupDomainControl.Define(this)
                .WithFlag(0, out var lseOn, name: "LSEON")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => lseOn.Value, name: "LSERDY")
                .WithFlag(2, name: "LSEBYP")
                .WithValueField(3, 2, name: "LSEDRV")
                .WithFlag(5, name: "LSECSSON")
                .WithFlag(6, name: "LSECSSD")
                .WithFlag(7, name: "LSESYSEN")
                .WithValueField(8, 2, name: "RTCSEL")
                .WithReservedBits(10, 3)
                .WithFlag(13, name: "LSESYSRDY")
                .WithFlag(14, name: "LSEGFON")
                .WithFlag(15, name: "RTCEN")
                .WithFlag(16, name: "VSWRST")
                .WithReservedBits(17, 9)
                .WithFlag(26, out var lsiOn, name: "LSION")
                .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => lsiOn.Value, name: "LSIRDY")
                .WithReservedBits(28, 4);

            Registers.ResetStatus.Define(this)
                .WithTag("RSR", 0, 32);

            Registers.SecurityConfiguration.Define(this)
                .WithTag("SECCFGR", 0, 32);

            Registers.PrivilegeConfiguration.Define(this)
                .WithTag("PRIVCFGR", 0, 32);
        }

        public ulong SysclkFrequency
        {
            get
            {
                switch((uint)systemClockSwitch.Value)
                {
                    case 0: // HSI
                        return hsidivf.Value ? HsiFrequency >> (int)hsidiv.Value : HsiFrequency;
                    case 1: // CSI
                        return CsiFrequency;
                    case 2: // HSE
                        return hseFrequency;
                    case 3: // PLL1P
                        return ComputePll1PFrequency();
                    default:
                        return HsiFrequency;
                }
            }
        }

        public ulong HclkFrequency
        {
            get
            {
                return SysclkFrequency >> AHBPrescTable[(int)hpre.Value];
            }
        }

        public ulong Pclk1Frequency
        {
            get
            {
                return HclkFrequency >> APBPrescTable[(int)ppre1.Value];
            }
        }

        private ulong ComputePll1PFrequency()
        {
            var m = (uint)pll1M.Value;
            if(m == 0)
            {
                return 0;
            }

            ulong source;
            switch((uint)pll1Src.Value)
            {
                case 1: // HSI (after HSIDIV)
                    source = hsidivf.Value ? HsiFrequency >> (int)hsidiv.Value : HsiFrequency;
                    break;
                case 2: // CSI
                    source = CsiFrequency;
                    break;
                case 3: // HSE
                    source = hseFrequency;
                    break;
                default: // 0 = no clock
                    return 0;
            }

            var fracContribution = pll1FracEn.Value ? (ulong)pll1FracN.Value : 0UL;
            var n = (ulong)pll1N.Value + (fracContribution * 1UL / 0x2000UL) + 1UL;
            var p = (ulong)pll1P.Value + 1UL;

            return (source / m) * n / p;
        }

        private readonly IHasFrequency nvic;
        private readonly IHasFrequency usart3;
        private readonly ulong hseFrequency;
        private readonly ulong lseFrequency;
        private readonly ulong lsiFrequency;

        private IFlagRegisterField hsidivf;
        private IValueRegisterField hsidiv;
        private IValueRegisterField systemClockSwitch;
        private IValueRegisterField hpre;
        private IValueRegisterField ppre1;
        private IValueRegisterField pll1Src;
        private IFlagRegisterField pll1FracEn;
        private IValueRegisterField pll1M;
        private IValueRegisterField pll1N;
        private IValueRegisterField pll1P;
        private IValueRegisterField pll1FracN;

        private const ulong HsiFrequency = 64000000;
        private const ulong CsiFrequency = 4000000;
        private const ulong DefaultHseFrequency = 8000000;
        private const ulong DefaultLseFrequency = 32768;
        private const ulong DefaultLsiFrequency = 32000;

        private static readonly int[] AHBPrescTable = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 6, 7, 8, 9 };
        private static readonly int[] APBPrescTable = { 0, 0, 0, 0, 1, 2, 3, 4 };

        private enum Registers
        {
            ClockControl = 0x00,
            // 0x04, 0x08, 0x0C reserved
            HSICalibration = 0x10,
            ClockRecoveryRC = 0x14,
            CSICalibration = 0x18,
            ClockConfiguration1 = 0x1C,
            ClockConfiguration2 = 0x20,
            // 0x24 reserved
            PLL1Configuration = 0x28,
            PLL2Configuration = 0x2C,
            PLL3Configuration = 0x30,
            PLL1Dividers = 0x34,
            PLL1Fractional = 0x38,
            PLL2Dividers = 0x3C,
            PLL2Fractional = 0x40,
            PLL3Dividers = 0x44,
            PLL3Fractional = 0x48,
            // 0x4C reserved
            ClockInterruptEnable = 0x50,
            ClockInterruptFlag = 0x54,
            ClockInterruptClear = 0x58,
            // 0x5C reserved
            AHB1Reset = 0x60,
            AHB2Reset = 0x64,
            // 0x68 reserved
            AHB4Reset = 0x6C,
            // 0x70 reserved
            APB1LowReset = 0x74,
            APB1HighReset = 0x78,
            APB2Reset = 0x7C,
            APB3Reset = 0x80,
            // 0x84 reserved
            AHB1Enable = 0x88,
            AHB2Enable = 0x8C,
            // 0x90 reserved
            AHB4Enable = 0x94,
            // 0x98 reserved
            APB1LowEnable = 0x9C,
            APB1HighEnable = 0xA0,
            APB2Enable = 0xA4,
            APB3Enable = 0xA8,
            // 0xAC reserved
            AHB1LowPowerEnable = 0xB0,
            AHB2LowPowerEnable = 0xB4,
            // 0xB8 reserved
            AHB4LowPowerEnable = 0xBC,
            // 0xC0 reserved
            APB1LowLowPowerEnable = 0xC4,
            APB1HighLowPowerEnable = 0xC8,
            APB2LowPowerEnable = 0xCC,
            APB3LowPowerEnable = 0xD0,
            // 0xD4 reserved
            ClockConfigurationInput1 = 0xD8,
            ClockConfigurationInput2 = 0xDC,
            ClockConfigurationInput3 = 0xE0,
            ClockConfigurationInput4 = 0xE4,
            ClockConfigurationInput5 = 0xE8,
            // 0xEC reserved
            BackupDomainControl = 0xF0,
            ResetStatus = 0xF4,
            // 0xF8 - 0x10C reserved
            SecurityConfiguration = 0x110,
            PrivilegeConfiguration = 0x114,
        }
    }
}
