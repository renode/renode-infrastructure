//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_CMU_3 : SiLabs_ICMU
    {
        public SiLabs_CMU_3(Machine machine, SiLabs_IHFXO hfxo, SiLabs_DPLL_1 dpll = null) : this(machine)
        {
            this.dpll = dpll;
            this.hfxo = hfxo;
        }

        partial void SiLabs_CMU_3_Constructor()
        {
            CMU_Reset();
        }

        partial void CMU_Reset()
        {
            sysclkctrl_clksel_field.Value = SYSCLKCTRL_CLKSEL.FSRCO;
            status_calrdy_bit.Value = true;
            status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
        }

        public bool OscPerpllEnabled(uint instance)
        {
            // PERPLL is not present on CMU_3
            return false;
        }

        public bool OscSocpllEnabled(uint instance)
        {
            // SOCPLL is not present on CMU_3
            return false;
        }

        public bool OscPerpllRequested(uint instance)
        {
            // PERPLL is not present on CMU_3
            return false;
        }

        public bool OscSocpllRequested(uint instance)
        {
            // SOCPLL is not present on CMU_3
            return false;
        }

        // Temporary implementation to check if a peripheral is enabled as those peripherals are not used
        public bool Burtc0Enabled { get; set; } = true;

        public bool Eusart0Enabled { get; set; } = true;

        public bool Eusart1Enabled { get; set; } = true;

        public bool Iadc0Enabled { get; set; } = true;

        public bool Letimer0Enabled { get; set; } = true;

        public bool Pcnt0Enabled { get; set; } = true;

        public bool Sysrtc0Enabled { get; set; } = true;

        public bool Timer0Enabled { get; set; } = true;

        public bool Timer1Enabled { get; set; } = true;

        public bool Timer2Enabled { get; set; } = true;

        public bool Timer3Enabled { get; set; } = true;

        public bool Timer4Enabled { get; set; } = true;

        public bool Vdac0Enabled { get; set; } = true;

        public bool Vdac1Enabled { get; set; } = true;

        public bool Wdog0Enabled { get; set; } = true;

        public bool Wdog1Enabled { get; set; } = true;

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
                        && IsEm23GrpaClkEnabled()
                    )
                    || (
                        em4grpaclkctrl_clksel_field.Value == EM4GRPACLKCTRL_CLKSEL.LFRCO
                        && IsEm4GrpaClkEnabled()
                    )
                    || (
                        Wdog0Enabled
                        && clken0_wdog0_bit.Value
                        && wdog0clkctrl_clksel_field.Value == WDOG0CLKCTRL_CLKSEL.LFRCO
                    )
                    || (
                        Wdog1Enabled
                        && clken1_wdog1_bit.Value
                        && wdog1clkctrl_clksel_field.Value == WDOG1CLKCTRL_CLKSEL.LFRCO
                    )
                    || (
                        Eusart0Enabled
                        && clken1_eusart0_bit.Value
                        && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.LFRCO
                    )
                    || (
                        Sysrtc0Enabled
                        && clken0_sysrtc0_bit.Value
                        && sysrtc0clkctrl_clksel_field.Value == SYSRTC0CLKCTRL_CLKSEL.LFRCO
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
                    || sysclkctrl_clksel_field.Value == SYSCLKCTRL_CLKSEL.HFRCODPLL
                    || (
                        em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFRCODPLL
                        && IsEm01GrpaClkEnabled()
                    )
                    || (
                        em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFRCODPLL
                        && IsEm01GrpcClkEnabled()
                    );
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
                        && IsEm01GrpaClkEnabled()
                    )
                    || (
                        em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFRCOEM23
                        && IsEm01GrpcClkEnabled()
                    )
                    || (
                        Iadc0Enabled
                        && clken0_iadc0_bit.Value
                        && iadcclkctrl_clksel_field.Value == IADCCLKCTRL_CLKSEL.HFRCOEM23
                    )
                    || (
                        Eusart0Enabled
                        && clken1_eusart0_bit.Value
                        && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.HFRCOEM23
                    )
                    || (
                        Vdac0Enabled
                        && clken1_vdac0_bit.Value
                        && vdac0clkctrl_clksel_field.Value == VDAC0CLKCTRL_CLKSEL.HFRCOEM23
                    )
                    || (
                        Vdac1Enabled
                        && clken1_vdac1_bit.Value
                        && vdac1clkctrl_clksel_field.Value == VDAC1CLKCTRL_CLKSEL.HFRCOEM23
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
                        em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFXO
                        && IsEm01GrpaClkEnabled()
                    )
                    || (
                        em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFXO
                        && IsEm01GrpcClkEnabled()
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
                        && IsEm23GrpaClkEnabled()
                    )
                    || (
                        em4grpaclkctrl_clksel_field.Value == EM4GRPACLKCTRL_CLKSEL.LFXO
                        && IsEm4GrpaClkEnabled()
                    )
                    || (
                        Wdog0Enabled
                        && clken0_wdog0_bit.Value
                        && wdog0clkctrl_clksel_field.Value == WDOG0CLKCTRL_CLKSEL.LFXO
                    )
                    || (
                        Wdog1Enabled
                        && clken1_wdog1_bit.Value
                        && wdog1clkctrl_clksel_field.Value == WDOG1CLKCTRL_CLKSEL.LFXO
                    )
                    || (
                        Eusart0Enabled
                        && clken1_eusart0_bit.Value
                        && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.LFXO
                    )
                    || (
                        Sysrtc0Enabled
                        && clken0_sysrtc0_bit.Value
                        && sysrtc0clkctrl_clksel_field.Value == SYSRTC0CLKCTRL_CLKSEL.LFXO
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

        private void BusFault(uint exception)
        {
            this.Log(LogLevel.Error, "CMU is locked, BusFault!!");
            if(
                machine.SystemBus.TryGetCurrentCPU(out var cpu)
                && cpu is TranslationCPU translationCPU
            )
            {
                translationCPU.RaiseException(exception);
            }
        }

        partial void Sysclkctrl_Write(uint a, uint b)
        {
            if(status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                BusFault(EXCP_PREFETCH_ABORT);
            }
        }

        partial void Sysclkctrl_Clksel_Write(SYSCLKCTRL_CLKSEL a, SYSCLKCTRL_CLKSEL b)
        {
            if(b == SYSCLKCTRL_CLKSEL.HFXO)
            {
                // Notify the Hfxo it has been selected as a source
                hfxo.OnClksel();
            }
        }

        partial void Calcnt_Calcnt_ValueProvider(ulong a)
        {
            // A dictionnary to get get the Upsel frequency based on the selected clock source.
            Dictionary<CALCTRL_UPSEL, ulong> getUpselFrequency =
                new Dictionary<CALCTRL_UPSEL, ulong>();
            getUpselFrequency[CALCTRL_UPSEL.DISABLED] = 0;
            getUpselFrequency[CALCTRL_UPSEL.PRS] = 32768UL;
            getUpselFrequency[CALCTRL_UPSEL.HFXO] = 39000000UL;
            getUpselFrequency[CALCTRL_UPSEL.LFXO] = 32768UL;
            getUpselFrequency[CALCTRL_UPSEL.HFRCODPLL] = 1000000UL;
            getUpselFrequency[CALCTRL_UPSEL.HFRCOEM23] = 19000000UL;
            getUpselFrequency[CALCTRL_UPSEL.FSRCO] = 20000000UL;
            getUpselFrequency[CALCTRL_UPSEL.LFRCO] = 32768UL;
            getUpselFrequency[CALCTRL_UPSEL.ULFRCO] = 1000UL;

            // A dictionnary to get get the Downsel frequency based on the selected clock source.
            Dictionary<CALCTRL_DOWNSEL, ulong> getDownselFrequency =
                new Dictionary<CALCTRL_DOWNSEL, ulong>();
            getDownselFrequency[CALCTRL_DOWNSEL.DISABLED] = 0;
            getDownselFrequency[CALCTRL_DOWNSEL.HCLK] = 1000000UL;
            getDownselFrequency[CALCTRL_DOWNSEL.PRS] = 39000000UL;
            getDownselFrequency[CALCTRL_DOWNSEL.HFXO] = (ulong)(
                39000000UL * (Dpll_N / (float)Dpll_M)
            );
            getDownselFrequency[CALCTRL_DOWNSEL.LFXO] = 32768UL;
            getDownselFrequency[CALCTRL_DOWNSEL.HFRCODPLL] = 19000000UL;
            getDownselFrequency[CALCTRL_DOWNSEL.HFRCOEM23] = 1000000UL;
            getDownselFrequency[CALCTRL_DOWNSEL.FSRCO] = 20000000UL;
            getDownselFrequency[CALCTRL_DOWNSEL.LFRCO] = 32768UL;
            getDownselFrequency[CALCTRL_DOWNSEL.ULFRCO] = 1000UL;

            ulong temp = (
                (calctrl_caltop_field.Value * getUpselFrequency[calctrl_upsel_field.Value])
                / getDownselFrequency[calctrl_downsel_field.Value]
            );

            calcnt_calcnt_field.Value = Math.Min(temp, 0xFFFFFUL);
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if(b == 0x93F7)
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
            if(b == 0x93F7)
            {
                status_wdoglock_bit.Value = STATUS_WDOGLOCK.UNLOCKED;
            }
            else
            {
                status_wdoglock_bit.Value = STATUS_WDOGLOCK.LOCKED;
            }
        }

        // Method used to check if this group is enabled
        private bool IsEm01GrpaClkEnabled()
        {
            // A dictionnary to get get the EM01GRPACLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> temp = new Dictionary<uint, bool>();
            temp[0] =
                Iadc0Enabled
                && clken0_iadc0_bit.Value
                && iadcclkctrl_clksel_field.Value == IADCCLKCTRL_CLKSEL.EM01GRPACLK;
            temp[1] =
                Vdac0Enabled
                && clken1_vdac0_bit.Value
                && vdac0clkctrl_clksel_field.Value == VDAC0CLKCTRL_CLKSEL.EM01GRPACLK;
            temp[2] =
                Vdac1Enabled
                && clken1_vdac1_bit.Value
                && vdac1clkctrl_clksel_field.Value == VDAC1CLKCTRL_CLKSEL.EM01GRPACLK;
            temp[3] = Timer0Enabled && clken0_timer0_bit.Value;
            temp[4] = Timer1Enabled && clken0_timer1_bit.Value;
            temp[5] = Timer2Enabled && clken0_timer2_bit.Value;
            temp[6] = Timer3Enabled && clken0_timer3_bit.Value;
            temp[7] = Timer4Enabled && clken0_timer4_bit.Value;
            // missing PD0B
            // missing KEYPAD

            return temp.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool IsEm01GrpcClkEnabled()
        {
            // A dictionnary to get get the EM01GRPCCLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> temp = new Dictionary<uint, bool>();
            temp[0] =
                Eusart0Enabled
                && clken1_eusart0_bit.Value
                && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.EM01GRPCCLK;
            temp[1] = Eusart1Enabled && clken1_eusart1_bit.Value;
            // missing EUSART2
            // missing PD0D

            return temp.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool IsEm23GrpaClkEnabled()
        {
            // A dictionnary to get get the EM23GRPACLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> temp = new Dictionary<uint, bool>();
            temp[0] = Letimer0Enabled && clken0_letimer0_bit.Value;
            temp[1] =
                Pcnt0Enabled
                && clken1_pcnt0_bit.Value
                && pcnt0clkctrl_clksel_field.Value == PCNT0CLKCTRL_CLKSEL.EM23GRPACLK;
            temp[2] =
                Vdac0Enabled
                && clken1_vdac0_bit.Value
                && vdac0clkctrl_clksel_field.Value == VDAC0CLKCTRL_CLKSEL.EM23GRPACLK;
            temp[2] =
                Vdac1Enabled
                && clken1_vdac1_bit.Value
                && vdac1clkctrl_clksel_field.Value == VDAC1CLKCTRL_CLKSEL.EM23GRPACLK;
            // missing PD1
            // missing SYSTICK

            return temp.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool IsEm4GrpaClkEnabled()
        {
            // A dictionnary to get get the EM4GRPACLK Look Up Table of the selected clock source.
            Dictionary<uint, bool> temp = new Dictionary<uint, bool>();
            temp[0] = Burtc0Enabled && clken0_burtc_bit.Value;

            return temp.ContainsValue(true);
        }

        // Implementation to check if a peripheral is enabled
        private bool Dpll0Enabled
        {
            get
            {
                if(dpll == null)
                {
                    return false;
                }
                return dpll.Enabled;
            }
        }

        // Reference to dpll to check if a peripheral is enabled
        private readonly SiLabs_DPLL_1 dpll;
        // Reference to hfxo to be able to call events it defines
        private readonly SiLabs_IHFXO hfxo;

        // See constant definitions in src/Infrastructure/src/Emulator/Cores/tlib/arch/arm/cpu.h
        private const uint EXCP_PREFETCH_ABORT = 3;
    }
}