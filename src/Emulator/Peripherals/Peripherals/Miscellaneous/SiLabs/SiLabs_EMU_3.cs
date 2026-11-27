//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_EMU_3 : ISiLabs_Emu
    {
        partial void SiLabs_EMU_3_Constructor()
        {
            emuBase = new SiLabs_EMU_Base(machine, this);
            // Power-on reset on initial cold boot.
            rstcause_por_bit.Value = true;
        }

        partial void EMU_Reset()
        {
            registers.Reset();
            emuBase.Reset();
            switch(pendingResetCause)
            {
            case PendingResetCause.Wdog0:
                rstcause_wdog0_bit.Value = true;
                break;
            case PendingResetCause.Wdog1:
                rstcause_wdog1_bit.Value = true;
                break;
            default:
                // RstCause.Sysreq: upon reset we set the Sysreq bit.
                // It can be cleared by SW using the Rstcauseclr command.
                rstcause_sysreq_bit.Value = true;
                break;
            }
            pendingResetCause = PendingResetCause.None;
        }

        partial void Cmd_Rstcauseclr_Write(bool a, bool b)
        {
            if(b)
            {
                rstcause_por_bit.Value = false;
                rstcause_pin_bit.Value = false;
                rstcause_em4_bit.Value = false;
                rstcause_wdog0_bit.Value = false;
                rstcause_wdog1_bit.Value = false;
                rstcause_lockup_bit.Value = false;
                rstcause_sysreq_bit.Value = false;
                rstcause_dvddbod_bit.Value = false;
                rstcause_dvddlebod_bit.Value = false;
                rstcause_decbod_bit.Value = false;
                rstcause_avddbod_bit.Value = false;
                rstcause_iovdd0bod_bit.Value = false;
                rstcause_iovdd1bod_bit.Value = false;
                rstcause_tamper_bit.Value = false;
                rstcause_m0sysreq_bit.Value = false;
                rstcause_m0lockup_bit.Value = false;
                rstcause_sem0sysreq_bit.Value = false;
                rstcause_sem0lockup_bit.Value = false;
                rstcause_vregin_bit.Value = false;
            }
        }

        public void SetPendingWatchdogReset(uint wdogInstance)
        {
            pendingResetCause = wdogInstance == 1 ? PendingResetCause.Wdog1 : PendingResetCause.Wdog0;
        }

        partial void Cmd_Em01vscale0_Write(bool a, bool b)
        {
            if(b)
            {
                ApplyEm01VScale(STATUS_VSCALE.VSCALE0);
            }
        }

        partial void Cmd_Em01vscale1_Write(bool a, bool b)
        {
            if(b)
            {
                ApplyEm01VScale(STATUS_VSCALE.VSCALE1);
            }
        }

        partial void Cmd_Em01vscale2_Write(bool a, bool b)
        {
            if(b)
            {
                ApplyEm01VScale(STATUS_VSCALE.VSCALE2);
            }
        }

        partial void Status_Em1p_ValueProvider(bool a)
        {
            status_em1p_bit.Value = emuBase.CurrentEnergyMode == SiLabs_EnergyMode.EM1P;
        }

        public void AddEnterSleepHook(Action hook)
        {
            emuBase.AddEnterSleepHook(hook);
        }

        public void AddExitSleepHook(Action hook)
        {
            emuBase.AddExitSleepHook(hook);
        }

        public void AddEnterDeepSleepHook(Action hook)
        {
            emuBase.AddEnterDeepSleepHook(hook);
        }

        public void AddExitDeepSleepHook(Action hook)
        {
            emuBase.AddExitDeepSleepHook(hook);
        }

        public void AddEnterEm1pHook(Action hook)
        {
            emuBase.AddEnterEm1pHook(hook);
        }

        public void AddExitEm1pHook(Action hook)
        {
            emuBase.AddExitEm1pHook(hook);
        }

        public void RegisterRadioSequencerCpu(CortexM cpu)
        {
            emuBase.RegisterRadioSequencerCpu(cpu);
        }

        public void SetEm1pRequest(object requester, bool request)
        {
            emuBase.SetEm1pRequest(requester, request);
        }

        public void AddEnterHostClockGateHook(Action hook)
        {
            emuBase.AddEnterHostClockGateHook(hook);
        }

        public void AddExitHostClockGateHook(Action hook)
        {
            emuBase.AddExitHostClockGateHook(hook);
        }

        public void AddEnterHostAllClockGateHook(Action hook)
        {
            emuBase.AddEnterHostAllClockGateHook(hook);
        }

        public void AddExitHostAllClockGateHook(Action hook)
        {
            emuBase.AddExitHostAllClockGateHook(hook);
        }

        public void SetEm1pHostClockRequest(object requester, bool request)
        {
            emuBase.SetEm1pHostClockRequest(requester, request);
        }

        public void SetQChannelBusy(object peripheral, bool busy)
        {
            emuBase.SetQChannelBusy(peripheral, busy);
        }

        public void UpdatePeripheralEnabledStatus(IPeripheral peripheral, bool enabled)
        {
            emuBase.UpdatePeripheralEnabledStatus(peripheral, enabled);
        }

        public List<IPeripheral> GetNoRetainPeripherals()
        {
            return emuBase.GetNoRetainPeripherals();
        }

        public SiLabs_EnergyMode CurrentEnergyMode => emuBase.CurrentEnergyMode;

        public bool HostClocksEnabled => emuBase.HostClocksEnabled;

        public bool HostAllClocksEnabled => emuBase.HostAllClocksEnabled;

        private void ApplyEm01VScale(STATUS_VSCALE level)
        {
            status_vscalebusy_bit.Value = false;
            status_vscalefailed_bit.Value = false;
            status_vscale_field.Value = level;
            if_vscaledone_bit.Value = true;
        }

        private SiLabs_EMU_Base emuBase;
        private PendingResetCause pendingResetCause;

        private enum PendingResetCause
        {
            None,
            Wdog0,
            Wdog1,
        }
    }
}
