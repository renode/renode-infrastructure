//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_CMU_8 : SiLabs_ICMU
    {
        partial void SiLabs_CMU_8_Constructor()
        {
            CMU_Reset();
        }

        private void BusFault(uint exception)
        {
            this.Log(LogLevel.Error, "CMU is locked, BusFault!!");
            if (machine.SystemBus.TryGetCurrentCPU(out var cpu)
                && cpu is TranslationCPU translationCPU)
            {
                translationCPU.RaiseException(exception);
            }
        }

        partial void CMU_Reset()
        {
            sysclkctrl_clksel_field.Value = SYSCLKCTRL_CLKSEL.FSRCO;
            status_calrdy_bit.Value = true;
            status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
        }

        partial void Sysclkctrl_Write(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                BusFault(EXCP_PREFETCH_ABORT);
            }
        }

        partial void Sysclkctrl_Clksel_Write(SYSCLKCTRL_CLKSEL a, SYSCLKCTRL_CLKSEL b)
        {
            if (b == SYSCLKCTRL_CLKSEL.HFXO)
            {
                // Notify the Hfxo it has been selected as a source
                hfxo.OnClksel();
            }
        }

        partial void Calcnt_Calcnt_ValueProvider(ulong a)
        {
            // A dictionnary to get get the Upsel frequency based on the selected clock source.
            Dictionary<CALCTRL_UPSEL, ulong> GetUpselFrequency =
                new Dictionary<CALCTRL_UPSEL, ulong>();
            GetUpselFrequency[CALCTRL_UPSEL.DISABLED] = 0;
            GetUpselFrequency[CALCTRL_UPSEL.PRS] = 32768UL;
            GetUpselFrequency[CALCTRL_UPSEL.HFXO] = 39000000UL;
            GetUpselFrequency[CALCTRL_UPSEL.LFXO] = 32768UL;
            GetUpselFrequency[CALCTRL_UPSEL.HFRCODPLL] = 1000000UL;
            GetUpselFrequency[CALCTRL_UPSEL.HFRCOEM23] = 19000000UL;
            GetUpselFrequency[CALCTRL_UPSEL.FSRCO] = 20000000UL;
            GetUpselFrequency[CALCTRL_UPSEL.LFRCO] = 32768UL;
            GetUpselFrequency[CALCTRL_UPSEL.ULFRCO] = 1000UL;

            // A dictionnary to get get the Downsel frequency based on the selected clock source.
            Dictionary<CALCTRL_DOWNSEL, ulong> GetDownselFrequency =
                new Dictionary<CALCTRL_DOWNSEL, ulong>();
            GetDownselFrequency[CALCTRL_DOWNSEL.DISABLED] = 0;
            GetDownselFrequency[CALCTRL_DOWNSEL.HCLK] = 1000000UL;
            GetDownselFrequency[CALCTRL_DOWNSEL.PRS] = 39000000UL;
            GetDownselFrequency[CALCTRL_DOWNSEL.HFXO] = (ulong)(
                39000000UL * (Dpll_N / (float)Dpll_M)
            );
            GetDownselFrequency[CALCTRL_DOWNSEL.LFXO] = 32768UL;
            GetDownselFrequency[CALCTRL_DOWNSEL.HFRCODPLL] = 19000000UL;
            GetDownselFrequency[CALCTRL_DOWNSEL.HFRCOEM23] = 1000000UL;
            GetDownselFrequency[CALCTRL_DOWNSEL.FSRCO] = 20000000UL;
            GetDownselFrequency[CALCTRL_DOWNSEL.LFRCO] = 32768UL;
            GetDownselFrequency[CALCTRL_DOWNSEL.ULFRCO] = 1000UL;

            ulong temp = (
                (caltop_caltop_field.Value * GetUpselFrequency[calctrl_upsel_field.Value])
                / GetDownselFrequency[calctrl_downsel_field.Value]
            );

            calcnt_calcnt_field.Value = Math.Min(temp, 0xFFFFFUL);
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if (b == 0x93F7)
            {
                status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
            }
            else
            {
                status_lock_bit.Value = STATUS_LOCK.LOCKED;
            }
        }

        partial void Wdoglock_Lockkey_Write(ulong a, ulong b)
        {
            if (b == 0x93F7)
            {
                status_wdoglock_bit.Value = STATUS_WDOGLOCK.UNLOCKED;
            }
            else
            {
                status_wdoglock_bit.Value = STATUS_WDOGLOCK.LOCKED;
            }
        }

        // Method used to check if this group is enabled
        private bool isEm01GrpaClkEnabled()
        {
            // A dictionnary to get get the EM01GRPACLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> EM01GRPACLK_LUT = new Dictionary<uint, bool>();
            EM01GRPACLK_LUT[0] =
                AdcEnabled
                && clken0_adc0_bit.Value
                && adcclkctrl_clksel_field.Value == ADCCLKCTRL_CLKSEL.EM01GRPACLK;
            EM01GRPACLK_LUT[1] = Timer0Enabled && clken0_timer0_bit.Value;
            EM01GRPACLK_LUT[2] = Timer1Enabled && clken0_timer1_bit.Value;
            EM01GRPACLK_LUT[3] = Timer2Enabled && clken0_timer2_bit.Value;
            EM01GRPACLK_LUT[4] = Timer3Enabled && clken0_timer3_bit.Value;
            // missing PD0B
            // missing KEYPAD

            return EM01GRPACLK_LUT.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool isEm01GrpcClkEnabled()
        {
            // A dictionnary to get get the EM01GRPCCLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> EM01GRPCCLK_LUT = new Dictionary<uint, bool>();
            EM01GRPCCLK_LUT[0] =
                Eusart0Enabled
                && clken1_eusart0_bit.Value
                && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.EM01GRPCCLK;
            EM01GRPCCLK_LUT[1] = Eusart1Enabled && clken1_eusart1_bit.Value;
            // missing EUSART2
            // missing PD0D

            return EM01GRPCCLK_LUT.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool isEm01GrpdClkEnabled()
        {
            // A dictionnary to get get the EM01GRPDCLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> EM01GRPDCLK_LUT = new Dictionary<uint, bool>();
            EM01GRPDCLK_LUT[0] =
                I2c0Enabled
                && clken0_i2c0_bit.Value
                && i2c0clkctrl_clksel_field.Value == I2C0CLKCTRL_CLKSEL.EM01GRPDCLK;
            EM01GRPDCLK_LUT[1] = I2c1Enabled && clken0_i2c1_bit.Value;

            return EM01GRPDCLK_LUT.ContainsValue(true);
        }        

        // Method used to check if this group is enabled
        private bool isEm23GrpaClkEnabled()
        {
            // A dictionnary to get get the EM23GRPACLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> EM23GRPACLK_LUT = new Dictionary<uint, bool>();
            EM23GRPACLK_LUT[0] = Letimer0Enabled && clken0_letimer0_bit.Value;
            EM23GRPACLK_LUT[1] =
                Pcnt0Enabled
                && clken1_pcnt0_bit.Value
                && pcnt0clkctrl_clksel_field.Value == PCNT0CLKCTRL_CLKSEL.EM23GRPACLK;
            // missing PD1
            // missing SYSTICK

            return EM23GRPACLK_LUT.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool isEm4GrpaClkEnabled()
        {
            // A dictionnary to get get the EM4GRPACLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> EM4GRPACLK_LUT = new Dictionary<uint, bool>();
            EM4GRPACLK_LUT[0] = Burtc0Enabled && clken0_burtc_bit.Value;
            EM4GRPACLK_LUT[1] = EtampdetEnabled && clken1_etampdet_bit.Value;

            return EM4GRPACLK_LUT.ContainsValue(true);
        }

        public bool OscLfrcoRequested
        {
            get
            {
                return exportclkctrl_clkoutsel0_field.Value == EXPORTCLKCTRL_CLKOUTSEL0.LFRCO
                    || exportclkctrl_clkoutsel1_field.Value == EXPORTCLKCTRL_CLKOUTSEL1.LFRCO
                    || exportclkctrl_clkoutsel2_field.Value == EXPORTCLKCTRL_CLKOUTSEL2.LFRCO
                    || calctrl_upsel_field.Value == CALCTRL_UPSEL.LFRCO
                    || calctrl_downsel_field.Value == CALCTRL_DOWNSEL.LFRCO
                    || (
                        em23grpaclkctrl_clksel_field.Value == EM23GRPACLKCTRL_CLKSEL.LFRCO
                        && isEm23GrpaClkEnabled()
                    )
                    || (
                        em4grpaclkctrl_clksel_field.Value == EM4GRPACLKCTRL_CLKSEL.LFRCO
                        && isEm4GrpaClkEnabled()
                    )
                    || (
                        Wdog0Enabled
                        && clken0_wdog0_bit.Value
                        && wdog0clkctrl_clksel_field.Value == WDOG0CLKCTRL_CLKSEL.LFRCO
                    )
                    || (
                        Eusart0Enabled
                        && clken1_eusart0_bit.Value
                        && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.LFRCO
                    )
                    || (
                        Sysrtc0Enabled
                        && clkenhv_sysrtc0_bit.Value
                        && sysrtc0clkctrl_clksel_field.Value == SYSRTC0CLKCTRL_CLKSEL.LFRCO
                    )
                    || (
                        I2c0Enabled
                        && clken0_i2c0_bit.Value
                        && i2c0clkctrl_clksel_field.Value == I2C0CLKCTRL_CLKSEL.LFRCO
                    );
            }
        }

        public bool OscLfrcoEnabled
        {
            get { return clken0_lfrco_bit.Value; }
        }

        public bool OscHfrcoRequested
        {
            get
            {
                return exportclkctrl_clkoutsel0_field.Value == EXPORTCLKCTRL_CLKOUTSEL0.HFRCODPLL
                    || exportclkctrl_clkoutsel1_field.Value == EXPORTCLKCTRL_CLKOUTSEL1.HFRCODPLL
                    || exportclkctrl_clkoutsel2_field.Value == EXPORTCLKCTRL_CLKOUTSEL2.HFRCODPLL
                    || calctrl_upsel_field.Value == CALCTRL_UPSEL.HFRCODPLL
                    || calctrl_downsel_field.Value == CALCTRL_DOWNSEL.HFRCODPLL
                    || traceclkctrl_clksel_field.Value == TRACECLKCTRL_CLKSEL.HFRCODPLLRT                    
                    || sysclkctrl_clksel_field.Value == SYSCLKCTRL_CLKSEL.HFRCODPLL
                    || (
                        (em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFRCODPLL
                        || em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFRCODPLLRT)
                        && isEm01GrpaClkEnabled()
                    )
                    || (
                        (em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFRCODPLL
                        || em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFRCODPLLRT)
                        && isEm01GrpcClkEnabled()
                    )
                    || (
                        em01grpdclkctrl_clksel_field.Value == EM01GRPDCLKCTRL_CLKSEL.HFRCODPLL
                        && isEm01GrpdClkEnabled()
                    )
                    || (
                        pixelrzclkctrl_clksel_field.Value == PIXELRZCLKCTRL_CLKSEL.HFXO
                        && clken1_pixelrz0_bit.Value
                        && Pixelrz0Enabled
                    );
                    // missing QSPIPLL reference clock
            }
        }

        public bool OscHfrcoEnabled
        {
            get { return clken0_hfrco0_bit.Value; }
        }

        public bool OscHfrcoEM23Requested
        {
            get
            {
                return exportclkctrl_clkoutsel0_field.Value == EXPORTCLKCTRL_CLKOUTSEL0.HFRCOEM23
                    || exportclkctrl_clkoutsel1_field.Value == EXPORTCLKCTRL_CLKOUTSEL1.HFRCOEM23
                    || exportclkctrl_clkoutsel2_field.Value == EXPORTCLKCTRL_CLKOUTSEL2.HFRCOEM23
                    || calctrl_upsel_field.Value == CALCTRL_UPSEL.HFRCOEM23
                    || calctrl_downsel_field.Value == CALCTRL_DOWNSEL.HFRCOEM23
                    || traceclkctrl_clksel_field.Value == TRACECLKCTRL_CLKSEL.HFRCOEM23
                    || (
                        em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFRCOEM23
                        && isEm01GrpaClkEnabled()
                    )
                    || (
                        em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFRCOEM23
                        && isEm01GrpcClkEnabled()
                    )
                    || (
                        em01grpdclkctrl_clksel_field.Value == EM01GRPDCLKCTRL_CLKSEL.HFRCOEM23
                        && isEm01GrpdClkEnabled()
                    )
                    || (
                        AdcEnabled
                        && clken0_adc0_bit.Value
                        && adcclkctrl_clksel_field.Value == ADCCLKCTRL_CLKSEL.HFRCOEM23
                    )
                    || (
                        Eusart0Enabled
                        && clken1_eusart0_bit.Value
                        && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.HFRCOEM23
                    )
                    || (
                        pixelrzclkctrl_clksel_field.Value == PIXELRZCLKCTRL_CLKSEL.HFXO
                        && clken1_pixelrz0_bit.Value
                        && Pixelrz0Enabled
                    );
            }
        }

        public bool OscHfrcoEM23Enabled
        {
            get { return clken0_hfrcoem23_bit.Value; }
        }

        public bool OscHfxoRequested
        {
            get
            {
                return exportclkctrl_clkoutsel0_field.Value == EXPORTCLKCTRL_CLKOUTSEL0.HFXO
                    || exportclkctrl_clkoutsel1_field.Value == EXPORTCLKCTRL_CLKOUTSEL1.HFXO
                    || exportclkctrl_clkoutsel2_field.Value == EXPORTCLKCTRL_CLKOUTSEL2.HFXO
                    || calctrl_upsel_field.Value == CALCTRL_UPSEL.HFXO
                    || calctrl_downsel_field.Value == CALCTRL_DOWNSEL.HFXO
                    || sysclkctrl_clksel_field.Value == SYSCLKCTRL_CLKSEL.HFXO
                    || (
                        Dpll0Enabled
                        && clken0_dpll0_bit.Value
                        && dpllrefclkctrl_clksel_field.Value == DPLLREFCLKCTRL_CLKSEL.HFXO
                    )
                    || (
                        OscSocpllEnabled(0)
                        && OscSocpllRequested(0)
                        && socpll0.IsUsingHfxo
                    )
                    || (
                        (em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFXO
                        || em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFXORT)
                        && isEm01GrpaClkEnabled()
                    )
                    || (
                        (em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFXO
                        || em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFXORT)
                        && isEm01GrpcClkEnabled()
                    )
                    || (
                        em01grpdclkctrl_clksel_field.Value == EM01GRPDCLKCTRL_CLKSEL.HFXO
                        && isEm01GrpdClkEnabled()
                    )
                    || (
                        pixelrzclkctrl_clksel_field.Value == PIXELRZCLKCTRL_CLKSEL.HFXO
                        && clken1_pixelrz0_bit.Value
                        && Pixelrz0Enabled
                    );
            }
        }

        public bool OscHfxoEnabled
        {
            get { return clken0_hfxo0_bit.Value; }
        }

        public bool OscLfxoRequested
        {
            get
            {
                return exportclkctrl_clkoutsel0_field.Value == EXPORTCLKCTRL_CLKOUTSEL0.LFXO
                    || exportclkctrl_clkoutsel1_field.Value == EXPORTCLKCTRL_CLKOUTSEL1.LFXO
                    || exportclkctrl_clkoutsel2_field.Value == EXPORTCLKCTRL_CLKOUTSEL2.LFXO
                    || calctrl_upsel_field.Value == CALCTRL_UPSEL.LFXO
                    || calctrl_downsel_field.Value == CALCTRL_DOWNSEL.LFXO
                    || (
                        Dpll0Enabled
                        && clken0_dpll0_bit.Value
                        && dpllrefclkctrl_clksel_field.Value == DPLLREFCLKCTRL_CLKSEL.LFXO
                    )
                    || (
                        em23grpaclkctrl_clksel_field.Value == EM23GRPACLKCTRL_CLKSEL.LFXO
                        && isEm23GrpaClkEnabled()
                    )
                    || (
                        em4grpaclkctrl_clksel_field.Value == EM4GRPACLKCTRL_CLKSEL.LFXO
                        && isEm4GrpaClkEnabled()
                    )
                    || (
                        Wdog0Enabled
                        && clken0_wdog0_bit.Value
                        && wdog0clkctrl_clksel_field.Value == WDOG0CLKCTRL_CLKSEL.LFXO
                    )
                    || (
                        Eusart0Enabled
                        && clken1_eusart0_bit.Value
                        && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.LFXO
                    )
                    || (
                        Sysrtc0Enabled
                        && clkenhv_sysrtc0_bit.Value
                        && sysrtc0clkctrl_clksel_field.Value == SYSRTC0CLKCTRL_CLKSEL.LFXO
                    )
                    || (
                        I2c0Enabled
                        && clken0_i2c0_bit.Value
                        && i2c0clkctrl_clksel_field.Value == I2C0CLKCTRL_CLKSEL.LFRCO
                    );
            }
        }

        public bool OscLfxoEnabled
        {
            get { return clken0_lfxo_bit.Value; }
        }

        public bool OscDpllEnabled
        {
            get { return clken0_dpll0_bit.Value; }
        }

        public ulong Dpll_N { get; set; } = 1;
        public ulong Dpll_M { get; set; } = 1;

        // Implementation to check if a peripheral is enabled
        private bool Dpll0Enabled
        {
            get
            {
                // TODO: use a better way to determine if DPLL is present
                SiLabs_DPLL_1 dpll1 = (SiLabs_DPLL_1)machine["sysbus.dpll"];
                if (Object.ReferenceEquals(dpll1, null))
                {
                    this.Log(LogLevel.Error, "Non existing Peripheral : {0}", dpll1);
                    return false;
                }
                return dpll1.Enabled;
            }
        }

        public bool OscSocpllRequested(uint instance)
        {
            return exportclkctrl_clkoutsel0_field.Value == EXPORTCLKCTRL_CLKOUTSEL0.SOCPLL
                || exportclkctrl_clkoutsel1_field.Value == EXPORTCLKCTRL_CLKOUTSEL1.SOCPLL
                || exportclkctrl_clkoutsel2_field.Value == EXPORTCLKCTRL_CLKOUTSEL2.SOCPLL
                || calctrl_upsel_field.Value == CALCTRL_UPSEL.SOCPLL
                || calctrl_downsel_field.Value == CALCTRL_DOWNSEL.SOCPLL
                || sysclkctrl_clksel_field.Value == SYSCLKCTRL_CLKSEL.SOCPLL;
                // missing QSPIPLL reference clock
        }

        public bool OscSocpllEnabled(uint instance)
        {
            return clken1_socpll0_bit.Value;
        }

        public bool OscPerpllRequested(uint instance)
        {
            // PERPLL is not present on CMU_8
            return false;
        }

        public bool OscPerpllEnabled(uint instance)
        {
            // PERPLL is not present on CMU_8
            return false;
        }

        private SiLabs_IHFXO _hfxo;
        public SiLabs_IHFXO hfxo
        {
            get
            {
                if (Object.ReferenceEquals(_hfxo, null))
                {
                    foreach(var hfxo in machine.GetPeripheralsOfType<SiLabs_IHFXO>())
                    {
                        _hfxo = hfxo;
                    }
                }
                return _hfxo;
            }
            set
            {
                _hfxo = value;
            }
        }

        private SiLabs_ISOCPLL _socpll0;
        private SiLabs_ISOCPLL socpll0
        {
            get
            {
                if (Object.ReferenceEquals(_socpll0, null))
                {
                    foreach(var socpll0 in machine.GetPeripheralsOfType<SiLabs_ISOCPLL>())
                    {
                        if (socpll0.Instance == 0)
                        {
                            _socpll0 = socpll0;
                        }
                    }
                }
                return _socpll0;
            }
            set
            {
                _socpll0 = value;
            }
        }

        // Temporary implementation to check if a peripheral is enabled as those peripherals are not used
        public bool Burtc0Enabled { get; set; } = true;
        public bool Eusart0Enabled { get; set; } = true;
        public bool Eusart1Enabled { get; set; } = true;
        public bool AdcEnabled { get; set; } = true;
        public bool Letimer0Enabled { get; set; } = true;
        public bool Pcnt0Enabled { get; set; } = true;
        public bool Sysrtc0Enabled { get; set; } = true;
        public bool Timer0Enabled { get; set; } = true;
        public bool Timer1Enabled { get; set; } = true;
        public bool Timer2Enabled { get; set; } = true;
        public bool Timer3Enabled { get; set; } = true;
        public bool Wdog0Enabled { get; set; } = true;
        public bool Wdog1Enabled { get; set; } = true;
        public bool EtampdetEnabled { get; set; } = true;
        public bool Pixelrz0Enabled { get; set; } = true;        
        public bool I2c0Enabled { get; set; } = true;
        public bool I2c1Enabled { get; set; } = true;
        
        // See constant definitions in src/Infrastructure/src/Emulator/Cores/tlib/arch/arm/cpu.h
        private const uint EXCP_PREFETCH_ABORT = 3;
    }
}
