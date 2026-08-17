//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_WDOG_1
    {
        public SiLabs_WDOG_1(Machine machine, uint instance = 0) : base(machine)
        {
            this.instance = instance;

            IRQ = new GPIO();

            timer = new LimitTimer(machine.ClockSource, WdogDefaultFrequency, this, $"wdog{instance}",
                                   limit: ulong.MaxValue, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot,
                                   eventEnabled: true, autoUpdate: true);
            timer.LimitReached += OnTimerLimitReached;
        }

        public GPIO IRQ { get; }

        partial void WDOG_Reset()
        {
            timer.Reset();
            currentStage = Stage.Warning;
            cyclesBeforeStage = 0;
            suspendedInEnergyMode = false;
            IRQ.Unset();
        }

        partial void En_En_Write(bool a, bool b)
        {
            // Per design book, WDOG_EN is locked while STATUS.LOCK is set.
            if(status_lock_bit.Value == STATUS_LOCK.LOCKED && a != b)
            {
                this.Log(LogLevel.Warning, "WDOG{0}: write to EN ignored, register is locked", instance);
                en_en_bit.Value = a;
                return;
            }

            if(!a && b)
            {
                this.DebugLog("WDOG{0} enabled", instance);
                InitializeEnergyModeHooks();
                ScheduleNextEvent();
            }
            else if(a && !b)
            {
                // EN: 1 -> 0 enters the DISABLING state, which resets the WDOG.
                // The DISABLING window is not modelled, so we transition immediately.
                this.DebugLog("WDOG{0} disabled", instance);
                timer.Enabled = false;
                cyclesBeforeStage = 0;
                currentStage = Stage.Warning;
                suspendedInEnergyMode = false;
                ClearInterruptFlags();
                UpdateInterrupts();
            }
        }

        partial void Cmd_Clear_Write(CMD_CLEAR a, CMD_CLEAR b)
        {
            if(b != CMD_CLEAR.CLEARED)
            {
                return;
            }

            if(!Enabled)
            {
                return;
            }

            if(cfg_clrsrc_bit.Value != CFG_CLRSRC.SW)
            {
                this.Log(LogLevel.Warning, "WDOG{0}: SW clear ignored, CLRSRC is PRSSRC0 (PRS clear is not modelled)", instance);
                return;
            }

            // Window violation: a clear arriving before the window threshold is
            // crossed should set the WIN interrupt flag without stopping the timer.
            if(cfg_winsel_field.Value != CFG_WINSEL.DIS)
            {
                ulong counter = GetCurrentCounter();
                ulong windowCycles = GetWindowCycles();
                if(counter < windowCycles)
                {
                    this.DebugLog("WDOG{0} window interrupt (counter={1} < window={2})", instance, counter, windowCycles);
                    if_win_bit.Value = true;
                    UpdateInterrupts();
                }
            }

            this.DebugLog("WDOG{0} cleared by SW", instance);
            ScheduleNextEvent();
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            status_lock_bit.Value = (b == UnlockKey) ? STATUS_LOCK.UNLOCKED : STATUS_LOCK.LOCKED;
        }

        partial void If_Write(uint a, uint b)
        {
            if(a != b)
            {
                UpdateInterrupts();
            }
        }

        partial void Ien_Write(uint a, uint b)
        {
            if(a != b)
            {
                UpdateInterrupts();
            }
        }

        protected override void UpdateInterrupts()
        {
            bool irq = (if_tout_bit.Value && ien_tout_bit.Value
                        || if_warn_bit.Value && ien_warn_bit.Value
                        || if_win_bit.Value && ien_win_bit.Value
                        || if_pem0_bit.Value && ien_pem0_bit.Value
                        || if_pem1_bit.Value && ien_pem1_bit.Value);
            IRQ.Set(irq);
        }

        private void ClearInterruptFlags()
        {
            if_tout_bit.Value = false;
            if_warn_bit.Value = false;
            if_win_bit.Value = false;
            if_pem0_bit.Value = false;
            if_pem1_bit.Value = false;
        }

        private void OnTimerLimitReached()
        {
            if(!Enabled)
            {
                return;
            }

            if(currentStage == Stage.Warning)
            {
                this.DebugLog("WDOG{0} warning interrupt", instance);
                if_warn_bit.Value = true;
                UpdateInterrupts();

                // Continue counting up to the full timeout.
                ulong timeoutCycles = GetTimeoutCycles();
                ulong warnCycles = GetWarnCycles();
                cyclesBeforeStage = warnCycles;
                currentStage = Stage.Timeout;
                StartTimer(timeoutCycles - warnCycles);
            }
            else
            {
                this.DebugLog("WDOG{0} timeout reached", instance);
                if_tout_bit.Value = true;
                UpdateInterrupts();

                timer.Enabled = false;

                if(cfg_wdogrstdis_bit.Value == CFG_WDOGRSTDIS.EN)
                {
                    this.Log(LogLevel.Warning, "WDOG{0} timeout expired: requesting machine reset", instance);
                    machine.RequestReset();
                }
            }
        }

        private void ScheduleNextEvent()
        {
            if(!Enabled)
            {
                return;
            }

            // Pick up the current CMU clock selection. Firmware can reconfigure
            // WDOG{n}CLKCTRL.CLKSEL between disable/enable cycles, so we always
            // refresh the frequency at the start of a new countdown.
            if(!RefreshClockFrequency())
            {
                this.Log(LogLevel.Warning,
                    "WDOG{0}: enabled while the CMU has WDOG{0}CLKCTRL.CLKSEL=DISABLED; the watchdog will not tick",
                    instance);
                timer.Enabled = false;
                return;
            }

            ulong timeoutCycles = GetTimeoutCycles();
            ulong warnCycles = GetWarnCycles();

            cyclesBeforeStage = 0;

            if(warnCycles > 0 && warnCycles < timeoutCycles)
            {
                currentStage = Stage.Warning;
                StartTimer(warnCycles);
            }
            else
            {
                currentStage = Stage.Timeout;
                StartTimer(timeoutCycles);
            }
        }

        private void StartTimer(ulong cycles)
        {
            timer.Enabled = false;
            timer.Limit = cycles;
            timer.Value = 0;
            timer.Enabled = true;
        }

        private void InitializeEnergyModeHooks()
        {
            if(energyModeHooksInitialized || Emu == null)
            {
                return;
            }

            Emu.AddEnterSleepHook(() => EnterEnergyMode(EnergyMode.EM1));
            Emu.AddExitSleepHook(() => ExitEnergyMode(EnergyMode.EM1));
            Emu.AddEnterDeepSleepHook(() => EnterEnergyMode(EnergyMode.EM2));
            Emu.AddExitDeepSleepHook(() => ExitEnergyMode(EnergyMode.EM2));
            energyModeHooksInitialized = true;
        }

        private void EnterEnergyMode(EnergyMode energyMode)
        {
            if(!Enabled || !timer.Enabled || RunsInEnergyMode(energyMode))
            {
                return;
            }

            TrySyncTime();
            timer.Enabled = false;
            suspendedInEnergyMode = true;
            suspendedEnergyMode = energyMode;
            this.DebugLog("WDOG{0} frozen in {1}", instance, energyMode);
        }

        private void ExitEnergyMode(EnergyMode energyMode)
        {
            if(!suspendedInEnergyMode || suspendedEnergyMode != energyMode)
            {
                return;
            }

            suspendedInEnergyMode = false;
            if(!Enabled)
            {
                return;
            }

            timer.Enabled = true;
            this.DebugLog("WDOG{0} resumed after {1}", instance, energyMode);
        }

        private bool RunsInEnergyMode(EnergyMode energyMode)
        {
            switch(energyMode)
            {
            case EnergyMode.EM1:
                return cfg_em1run_bit.Value == CFG_EM1RUN.ENABLE;
            case EnergyMode.EM2:
                return cfg_em2run_bit.Value == CFG_EM2RUN.ENABLE;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(energyMode));
            }
        }

        private ulong GetCurrentCounter()
        {
            if(!timer.Enabled)
            {
                return 0;
            }
            return cyclesBeforeStage + timer.Value;
        }

        private ulong GetTimeoutCycles()
        {
            // T_timeout = 2^(3 + PERSEL) + 1 (in WDOG clock cycles)
            int persel = (int)cfg_persel_field.Value;
            return ((ulong)1 << (3 + persel)) + 1;
        }

        private ulong GetWarnCycles()
        {
            // T_warn = 2^(3 + PERSEL) * (WARNSEL / 4) + 1
            var warnSel = cfg_warnsel_field.Value;
            if(warnSel == CFG_WARNSEL.DIS)
            {
                return 0;
            }
            int persel = (int)cfg_persel_field.Value;
            ulong baseCycles = (ulong)1 << (3 + persel);
            return (baseCycles * (uint)warnSel) / 4 + 1;
        }

        private ulong GetWindowCycles()
        {
            // T_window = 2^(3 + PERSEL) * (WINSEL / 8) + 1
            var winSel = cfg_winsel_field.Value;
            if(winSel == CFG_WINSEL.DIS)
            {
                return 0;
            }
            int persel = (int)cfg_persel_field.Value;
            ulong baseCycles = (ulong)1 << (3 + persel);
            return (baseCycles * (uint)winSel) / 8 + 1;
        }

        // Apply the WDOG clock frequency currently selected by the CMU to the
        // backing LimitTimer. Returns true if a non-zero frequency was found
        // (the timer can run), or false if the selected source is DISABLED.
        //
        private bool RefreshClockFrequency()
        {
            ulong frequency = (Cmu != null)
                ? Cmu.WdogClockFrequency(instance)
                : WdogDefaultFrequency;

            if(frequency == 0UL)
            {
                return false;
            }

            if(frequency != timer.Frequency)
            {
                this.DebugLog("WDOG{0} clock frequency changed: {1} Hz -> {2} Hz",
                    instance, timer.Frequency, frequency);
                timer.Frequency = frequency;
            }
            return true;
        }

        private Stage currentStage;
        private ulong cyclesBeforeStage;
        private bool energyModeHooksInitialized;
        private bool suspendedInEnergyMode;
        private EnergyMode suspendedEnergyMode;

        private readonly uint instance;
        private readonly LimitTimer timer;

        // Fallback frequency used when no CMU is attached to the machine.
        private const ulong WdogDefaultFrequency = SiLabs_CMU_Constants.LfrcoFrequency;
        private const ulong UnlockKey = 0xABE8UL;

        private enum Stage
        {
            Warning,
            Timeout,
        }

        private enum EnergyMode
        {
            EM1,
            EM2,
        }
    }
}
