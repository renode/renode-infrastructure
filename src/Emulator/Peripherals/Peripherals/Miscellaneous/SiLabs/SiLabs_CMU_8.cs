//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
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
    public partial class SiLabs_CMU_8 : ISiLabs_CMU
    {
        public SiLabs_CMU_8(Machine machine, ulong hfxoFrequency) : base(machine)
        {
            HfxoFrequency = hfxoFrequency;
            Define_Registers();
            CMU_Reset();
        }

        public bool OscHfrcoEnabled(uint instance)
        {
            if(instance != 0)
            {
                this.Log(LogLevel.Error, "{0} is an invalid instance for HFRCO", instance);
                return false;
            }
            return clken0_hfrco0_bit.Value;
        }

        public bool OscHfrcoRequested(uint instance)
        {
            if(instance != 0)
            {
                this.Log(LogLevel.Error, "{0} is an invalid instance for HFRCO", instance);
                return false;
            }
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
                    && IsEm01GrpaClkEnabled()
                )
                || (
                    (em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFRCODPLL
                    || em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFRCODPLLRT)
                    && IsEm01GrpcClkEnabled()
                )
                || (
                    em01grpdclkctrl_clksel_field.Value == EM01GRPDCLKCTRL_CLKSEL.HFRCODPLL
                    && IsEm01GrpdClkEnabled()
                )
                // TODO: Looks copied from the HFXO check, should likely test HFRCODPLL/HFRCODPLLRT.
                || (
                    pixelrzclkctrl_clksel_field.Value == PIXELRZCLKCTRL_CLKSEL.HFXO
                    && clken1_pixelrz0_bit.Value
                    && Pixelrz0Enabled
                );
            // missing QSPIPLL reference clock
        }

        public ulong WdogClockFrequency(uint instance)
        {
            SiLabs_WdogClksel clksel;
            switch(instance)
            {
            case 0:
                clksel = (SiLabs_WdogClksel)wdog0clkctrl_clksel_field.Value;
                break;
            case 1:
                clksel = SiLabs_WdogClksel.HclkDiv1024;
                break;
            default:
                this.Log(LogLevel.Warning, "WdogClockFrequency: unknown WDOG instance {0}", instance);
                return 0UL;
            }
            return SiLabs_CMU_Constants.WdogFrequencyFromClksel(clksel, HfxoFrequency);
        }

        // stub

        public bool GetPeripheralClockEnable(IPeripheral peripheral)
        {
            return true; // Default to true for peripherals not handled by this method
        }

        public bool OscPerpllEnabled(uint instance)
        {
            // PERPLL is not present on CMU_8
            return false;
        }

        public bool OscPerpllRequested(uint instance)
        {
            // PERPLL is not present on CMU_8
            return false;
        }

        public bool OscSocpllEnabled(uint instance)
        {
            return clken1_socpll0_bit.Value;
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

        public ulong HfxoFrequency { get; }

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

        public bool AdcEnabled { get; set; } = true;

        public bool Letimer0Enabled { get; set; } = true;

        public bool Pcnt0Enabled { get; set; } = true;

        public bool Eusart1Enabled { get; set; } = true;

        public bool Sysrtc0Enabled { get; set; } = true;

        public bool Timer1Enabled { get; set; } = true;

        public bool Timer2Enabled { get; set; } = true;

        public bool Timer3Enabled { get; set; } = true;

        public bool Wdog0Enabled { get; set; } = true;

        public bool Wdog1Enabled { get; set; } = true;

        public bool EtampdetEnabled { get; set; } = true;

        public bool Pixelrz0Enabled { get; set; } = true;

        public bool Timer0Enabled { get; set; } = true;

        public bool Eusart0Enabled { get; set; } = true;

        public ISiLabs_HFXO Hfxo
        {
            get
            {
                if(Object.ReferenceEquals(hfxo, null))
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_HFXO>())
                    {
                        hfxo = peripheral;
                        break;
                    }
                }
                return hfxo;
            }

            set
            {
                hfxo = value;
            }
        }

        public bool I2c0Enabled { get; set; } = true;

        public ulong Dpll_M { get; set; } = 1;

        public ulong Dpll_N { get; set; } = 1;

        public bool OscDpllEnabled
        {
            get { return clken0_dpll0_bit.Value; }
        }

        public bool OscLfxoEnabled
        {
            get { return clken0_lfxo_bit.Value; }
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

        public bool OscHfxoEnabled
        {
            get { return clken0_hfxo0_bit.Value; }
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
                        Socpll0.Enabled
                        && Socpll0.IsReady
                        && Socpll0.IsUsingHfxo
                    )
                    || (
                        (em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFXO
                        || em01grpaclkctrl_clksel_field.Value == EM01GRPACLKCTRL_CLKSEL.HFXORT)
                        && IsEm01GrpaClkEnabled()
                    )
                    || (
                        (em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFXO
                        || em01grpcclkctrl_clksel_field.Value == EM01GRPCCLKCTRL_CLKSEL.HFXORT)
                        && IsEm01GrpcClkEnabled()
                    )
                    || (
                        em01grpdclkctrl_clksel_field.Value == EM01GRPDCLKCTRL_CLKSEL.HFXO
                        && IsEm01GrpdClkEnabled()
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
                        em01grpdclkctrl_clksel_field.Value == EM01GRPDCLKCTRL_CLKSEL.HFRCOEM23
                        && IsEm01GrpdClkEnabled()
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
                    // TODO: Looks copied from the HFXO check, should likely test HFRCOEM23.
                    || (
                        pixelrzclkctrl_clksel_field.Value == PIXELRZCLKCTRL_CLKSEL.HFXO
                        && clken1_pixelrz0_bit.Value
                        && Pixelrz0Enabled
                    );
            }
        }

        public bool OscLfrcoEnabled
        {
            get { return clken0_lfrco_bit.Value; }
        }

        // Temporary implementation to check if a peripheral is enabled as those peripherals are not used
        public bool Burtc0Enabled { get; set; } = true;

        public bool I2c1Enabled { get; set; } = true;

        private void BusFault(uint exception)
        {
            this.Log(LogLevel.Error, "CMU is locked, BusFault!!");
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu)
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
                Hfxo.OnClksel();
            }
        }

        partial void Calcnt_Calcnt_ValueProvider(ulong a)
        {
            // A dictionnary to get get the Upsel frequency based on the selected clock source.
            var getUpselFrequency =
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
            var getDownselFrequency =
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
                (caltop_caltop_field.Value * getUpselFrequency[calctrl_upsel_field.Value])
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
            var em01GrpaClkLut = new Dictionary<uint, bool>();
            em01GrpaClkLut[0] =
                AdcEnabled
                && clken0_adc0_bit.Value
                && adcclkctrl_clksel_field.Value == ADCCLKCTRL_CLKSEL.EM01GRPACLK;
            em01GrpaClkLut[1] = Timer0Enabled && clken0_timer0_bit.Value;
            em01GrpaClkLut[2] = Timer1Enabled && clken0_timer1_bit.Value;
            em01GrpaClkLut[3] = Timer2Enabled && clken0_timer2_bit.Value;
            em01GrpaClkLut[4] = Timer3Enabled && clken0_timer3_bit.Value;
            // missing PD0B
            // missing KEYPAD

            return em01GrpaClkLut.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool IsEm01GrpcClkEnabled()
        {
            // A dictionnary to get get the EM01GRPCCLK Look Up Table of the selected clock source.
            var em01GrpcClkLut = new Dictionary<uint, bool>();
            em01GrpcClkLut[0] =
                Eusart0Enabled
                && clken1_eusart0_bit.Value
                && eusart0clkctrl_clksel_field.Value == EUSART0CLKCTRL_CLKSEL.EM01GRPCCLK;
            em01GrpcClkLut[1] = Eusart1Enabled && clken1_eusart1_bit.Value;
            // missing EUSART2
            // missing PD0D

            return em01GrpcClkLut.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool IsEm01GrpdClkEnabled()
        {
            // A dictionnary to get get the EM01GRPDCLK Look Up Table of the selected clock source.
            var em01GrpdClkLut = new Dictionary<uint, bool>();
            em01GrpdClkLut[0] =
                I2c0Enabled
                && clken0_i2c0_bit.Value
                && i2c0clkctrl_clksel_field.Value == I2C0CLKCTRL_CLKSEL.EM01GRPDCLK;
            em01GrpdClkLut[1] = I2c1Enabled && clken0_i2c1_bit.Value;

            return em01GrpdClkLut.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool IsEm23GrpaClkEnabled()
        {
            // A dictionnary to get get the EM23GRPACLK Look Up Table of the selected clock source.
            var em23GrpaClkLut = new Dictionary<uint, bool>();
            em23GrpaClkLut[0] = Letimer0Enabled && clken0_letimer0_bit.Value;
            em23GrpaClkLut[1] =
                Pcnt0Enabled
                && clken1_pcnt0_bit.Value
                && pcnt0clkctrl_clksel_field.Value == PCNT0CLKCTRL_CLKSEL.EM23GRPACLK;
            // missing PD1
            // missing SYSTICK

            return em23GrpaClkLut.ContainsValue(true);
        }

        // Method used to check if this group is enabled
        private bool IsEm4GrpaClkEnabled()
        {
            // A dictionnary to get get the EM4GRPACLK Look Up Table of the selected clock source.
            var em4GrpaClkLut = new Dictionary<uint, bool>();
            em4GrpaClkLut[0] = Burtc0Enabled && clken0_burtc_bit.Value;
            em4GrpaClkLut[1] = EtampdetEnabled && clken1_etampdet_bit.Value;

            return em4GrpaClkLut.ContainsValue(true);
        }

        private ISiLabs_SOCPLL Socpll0
        {
            get
            {
                if(Object.ReferenceEquals(socpll0, null))
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_SOCPLL>())
                    {
                        if(peripheral.Instance == 0)
                        {
                            socpll0 = peripheral;
                            break;
                        }
                    }
                }
                return socpll0;
            }

            set
            {
                socpll0 = value;
            }
        }

        private ISiLabs_DPLL Dpll0
        {
            get
            {
                if(dpll0 is null)
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_DPLL>())
                    {
                        if(peripheral.Instance == 0)
                        {
                            dpll0 = peripheral;
                            break;
                        }
                    }
                }
                return dpll0;
            }
        }

        private bool Dpll0Enabled
        {
            get
            {
                return Dpll0?.Enabled ?? false;
            }
        }

        // Reference to dpll to check if a peripheral is enabled
        private ISiLabs_DPLL dpll0;
        private ISiLabs_HFXO hfxo;

        private ISiLabs_SOCPLL socpll0;

        // See constant definitions in src/Infrastructure/src/Emulator/Cores/tlib/arch/arm/cpu.h
        private const uint EXCP_PREFETCH_ABORT = 3;
    }
}
