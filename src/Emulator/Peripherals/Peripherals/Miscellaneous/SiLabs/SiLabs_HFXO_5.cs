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
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    // Allows for the viewing of register contents when debugging
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class SiLabs_HFXO_5 : ISiLabs_HFXO, ISiLabs_Retention, ISiLabs_PartialRetention
    {
        public SiLabs_HFXO_5(Machine machine, uint startupDelayTicks) : this(machine)
        {
            this.delayTicks = startupDelayTicks;
        }

        public void PowerDown()
        {
            // Full retention in EM2.
        }

        public List<DoubleWordRegister> GetPowerDownRegisters()
        {
            // Full retention in EM2.
            return new List<DoubleWordRegister>();
        }

        public List<Range> AddRangesToPowerDown()
        {
            // Full retention in EM2.
            return new List<Range>();
        }

        public void OnRequest(HFXO_REQUESTER requester)
        {
            this.Log(LogLevel.Debug, "Reached HFXO after request");

            if(wakeUpRequester != WAKEUP_SOURCE.NONE)
            {
                // Return if a wakeup is already in progress
                return;
            }

            switch(requester)
            {
            case HFXO_REQUESTER.PRS:
                this.Log(LogLevel.Debug, "HFXO requested by PRS");
                // TODO: Behaviour after PRS request here
                OnEm2Wakeup();
                break;
            case HFXO_REQUESTER.FORCEEN:
                this.Log(LogLevel.Debug, "HFXO requested by ForceEn");
                // TODO: Behaviour after ForceEn request here
                break;
            case HFXO_REQUESTER.HWREQ:
                this.Log(LogLevel.Debug, "HFXO requested by HWREQ");
                // TODO: Behavour after HWREQ here
                OnClksel();
                break;
            case HFXO_REQUESTER.SYSRTC:
                this.Log(LogLevel.Debug, "HFXO requested by SYSRTC");
                if(ctrl_disondemandsysrtc_bit.Value)
                {
                    //return if sysrtc request interface is disabled
                    return;
                }

                wakeUpRequester = WAKEUP_SOURCE.SYSRTC;
                status_sysrtchwreq_bit.Value = true;
                StartDelayTimer();
                status_ens_bit.Value = true;
                break;
            // Add more cases as necessary
            default:
                this.Log(LogLevel.Error, "Invalid HFXO Requester");
                break;
            }

            this.Log(LogLevel.Debug, "Start-up delay timer began at: {0}", machine.ElapsedVirtualTime);
        }

        public void OnEm2Wakeup()
        {
            this.Log(LogLevel.Debug, "Reached HFXO for early wakeup");
            wakeUpRequester = WAKEUP_SOURCE.PRS;

            // Set status bits indicating wakeup was due to a HW request from PRS, and set ENS
            status_prshwreq_bit.Value = true;
            status_ens_bit.Value = true;

            // Configure delay timer
            StartDelayTimer();

            // Notify subscribers that the Hfxo is enabled and de-assert the PRSHWREQ bit
            this.Log(LogLevel.Debug, "Start-up delay timer began at: {0}", machine.ElapsedVirtualTime);
            HfxoEnabled?.Invoke();
            status_prshwreq_bit.Value = false;
        }

        public void OnClksel()
        {
            // This function is only used for the PRS usecase for now, but I do not think the PRS needs to have
            // requested the HFXO to wakeup for the CMU to be used to finish the wake-up process, thus
            // we simply check if a wakeup is in progress by ensuring there is a wakeup requester.
            if(wakeUpRequester != WAKEUP_SOURCE.NONE)
            {
                this.Log(LogLevel.Debug, "HFXO selected as a source by the CMU");

                // Complete wakeup after receiving a HW signal by triggering rdy interrupt
                wakeUpRequester = WAKEUP_SOURCE.NONE;
                status_rdy_bit.Value = true;
                status_prsrdy_bit.Value = false;
                status_sysrtcrdy_bit.Value = false;
                if_rdy_bit.Value = true;
                UpdateInterrupts();
            }
        }

        public GPIO IRQ { get; private set; }

        public event Action HfxoEnabled;

        partial void SiLabs_HFXO_5_Constructor()
        {
            // This limit timer is used to emulate the HFXO startup delay
            timer = new LimitTimer(machine.ClockSource, 32768, this, "hfxodelay", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            timer.LimitReached += OnStartUpTimerExpired;

            wakeuptimer = new LimitTimer(machine.ClockSource, 32768, this, "hfxodelay", 0xFFFFFFFFUL, direction: Direction.Ascending,
                                   enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
            wakeuptimer.LimitReached += OnWakeUpTimerExpired;

            IRQ = new GPIO();   // IRQ object handles all HFXO interrupts
        }

        partial void HFXO_Reset()
        {
            temp_incr = 0;
            oscillatorOnDemand = false;
            oscillatorForceEn = false;
            wakeUpRequester = WAKEUP_SOURCE.NONE;
            timer.Enabled = false;
            wakeuptimer.Enabled = false;
            wakeup = true;

            registers.Reset();
            UpdateInterrupts();
        }

        partial void Ctrl_Forceen_Write(bool a, bool b)
        {
            status_corebiasoptrdy_bit.Value = false;

            if(!a && b)
            {
                // If we are setting Forceen from 0 to 1, begin start-up delay process
                wakeUpRequester = WAKEUP_SOURCE.FORCE;
                StartDelayTimer();
            }
            else if(!b)    // Add !b condition to avoid scenario where two sequential writes to forceen cause us to skip the wakeup delay
            {
                oscillatorForceEn = b;
            }
        }

        partial void Ctrl_Disondemand_Write(bool a, bool b)
        {
            oscillatorOnDemand = (b != true);
            status_corebiasoptrdy_bit.Value = false;
        }

        partial void Cmd_Corebiasopt_Write(bool a, bool b)
        {
            if(b)
            {
                xtalctrl_corebiasana_field.Value = 0x3D;
            }
            else
            {
                xtalctrl_corebiasana_field.Value = 0x3C;
            }
        }

        partial void Status_Rdy_ValueProvider(bool a)
        {
            if(!emuHookInitialized)
            {
                EnteringDeepSleep = () => EnterDeepSleep();
                ExitingDeepSleep = () => ExitDeepSleep();
                Emu.AddEnterDeepSleepHook(EnteringDeepSleep);
                Emu.AddExitDeepSleepHook(ExitingDeepSleep);
                emuHookInitialized = true;
            }
            status_rdy_bit.Value = IsUsed;
        }

        partial void Status_Corebiasoptrdy_ValueProvider(bool a)
        {
            if(a == false)
            {
                status_corebiasoptrdy_bit.Value = true;
            }
            else if(a == true && temp_incr > 10)
            {
                status_corebiasoptrdy_bit.Value = false;
                temp_incr = 0;
            }
            else
            {
                temp_incr++;
            }
        }

        partial void Status_Ens_ValueProvider(bool a)
        {
            status_ens_bit.Value = IsUsed;
        }

        partial void Status_Hwreq_ValueProvider(bool a)
        {
            status_hwreq_bit.Value = cmu.OscHfxoRequested && cmu.OscHfxoEnabled;
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if(b == 0x580E)
            {
                status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
            }
            else
            {
                status_lock_bit.Value = STATUS_LOCK.LOCKED;
            }
        }

        partial void If_Write(uint a, uint b)
        {
            UpdateInterrupts();
        }

        partial void Ien_Write(uint a, uint b)
        {
            UpdateInterrupts();
        }

        private void EnterDeepSleep()
        {
            // Set the HFXO to not ready
            wakeup = false;
            status_prsrdy_bit.Value = false;
            status_sysrtcrdy_bit.Value = false;
            wakeuptimer.Enabled = false;
        }

        private void ExitDeepSleep()
        {
            // Function which starts the start-up delay timer
            wakeuptimer.Enabled = false;
            wakeuptimer.Value = 0;
            wakeuptimer.Limit = delayTicks;
            wakeuptimer.Enabled = true;
        }

        private void OnWakeUpTimerExpired()
        {
            if(!wakeup)
            {
                // Stop the wakeup timers
                wakeuptimer.Enabled = false;
                timer.Enabled = false;
                wakeUpRequester = WAKEUP_SOURCE.NONE;

                // Set the HFXO to ready
                wakeup = true;
                if_rdy_bit.Value = true;
                UpdateInterrupts();
            }
        }

        private void StartDelayTimer(uint startValue = 0)
        {
            // Function which starts the start-up delay timer
            this.Log(LogLevel.Debug, "Starting delay timer with delay ticks = {0}", delayTicks);

            timer.Enabled = false;
            timer.Value = startValue;
            timer.Limit = delayTicks;
            timer.Enabled = true;
        }

        private void OnStartUpTimerExpired()
        {
            this.Log(LogLevel.Debug, "Start-up delay timer expired at: {0}", machine.ElapsedVirtualTime);
            this.Log(LogLevel.Debug, "Wakeup Requester = {0}", wakeUpRequester);

            switch(wakeUpRequester)
            {
            case WAKEUP_SOURCE.PRS:
                // Set PRSRDY bit in Status to true, stop the delay timer and raise an interrupt.
                // This is assumed to only be entered in an early wakeup scenario
                status_prsrdy_bit.Value = true;
                if_prsrdy_bit.Value = true;
                break;
            case WAKEUP_SOURCE.FORCE:
                // Set status rdy and ens bits to 1
                oscillatorForceEn = true;

                // In the case of a forced wakeup (not early wakeup) we want the oscillator to be ready immediately
                // and do not wait for a subsequent hardware request, thus the wakeup ends here and we raise rdy interrupt
                wakeUpRequester = WAKEUP_SOURCE.NONE;
                if_rdy_bit.Value = true;
                break;
            case WAKEUP_SOURCE.SYSRTC:
                status_sysrtcrdy_bit.Value = true;
                if_sysrtcrdy_bit.Value = true;

                wakeUpRequester = WAKEUP_SOURCE.NONE;
                status_sysrtchwreq_bit.Value = false;   // Reset field after we service request
                                                        // Rdy will only be raised when the Hfxo is requested by the digital clock interface (HWREQ)
                break;
            default:
                this.Log(LogLevel.Error, "Invalid wakeup requester upon timer expiry");
                break;
            }
            timer.Enabled = false;
            wakeup = true;
            wakeuptimer.Enabled = false;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                var irq = (registers.Read((long)Registers.Ien) & registers.Read((long)Registers.If)) != 0;
                IRQ.Set(irq);
            });
        }

        private ISiLabs_Emu Emu
        {
            get
            {
                if(Object.ReferenceEquals(emu, null))
                {
                    foreach(var peripheral in machine.GetPeripheralsOfType<ISiLabs_Emu>())
                    {
                        emu = peripheral;
                        break;
                    }
                }
                return emu;
            }

            set
            {
                emu = value;
            }
        }

        private bool IsEnabled
        {
            get { return cmu.OscHfxoEnabled; }
        }

        private bool IsUsed
        {
            get { return oscillatorForceEn || (oscillatorOnDemand && cmu.OscHfxoRequested && wakeup); }
        }

        private Action EnteringDeepSleep;
        private Action ExitingDeepSleep;
        private ISiLabs_Emu emu;
        private bool emuHookInitialized = false;
        private bool wakeup = true;

        // Variable which represents which source could have requested the wakeup (and if a wakeup is in progress)
        private WAKEUP_SOURCE wakeUpRequester = WAKEUP_SOURCE.NONE;
        private ulong temp_incr = 0;
        private bool oscillatorOnDemand = false;
        private bool oscillatorForceEn = false;
        private LimitTimer timer;
        private LimitTimer wakeuptimer;
        private readonly uint delayTicks;    // Amount of ticks we delay for simulated start-up time

        private enum WAKEUP_SOURCE
        {
            NONE = 0,   // No source has requested a wakeup (there is no wakeup in progress)
            PRS = 1,    // PRS has requested a wakeup (wakeup is in progress)
            FORCE = 2,  // Force has requested a wakeup (wakeup is in progress)
            SYSRTC = 3, // Wakeup requested by SYSRTC pretrigger

            // Can be expanded with more wakeup sources as needed
        }
    }
}
