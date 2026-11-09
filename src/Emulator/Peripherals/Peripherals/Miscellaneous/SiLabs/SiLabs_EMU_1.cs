//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    // xG22 has no STATUS.EM1P bit. The energy mode is still tracked here so a radio EM1P
    // request keeps PD1 powered while the host is in deep sleep, and peripherals can read
    // CurrentEnergyMode.
    public partial class SiLabs_EMU_1 : BasicDoubleWordPeripheral, IKnownSize, ISiLabs_Emu
    {
        public SiLabs_EMU_1(Machine machine) : base(machine)
        {
            Define_Registers();
            emuBase = new SiLabs_EMU_Base(machine, this);
        }

        partial void EMU_Reset()
        {
            registers.Reset();
            // RstCause.Sysreq: upon reset we set the Sysreq bit. 
            // It can be cleared by SW using the Rstcauseclr command.
            rstcause_sysreq_bit.Value = true;
        }

        partial void Cmd_Rstcauseclr_Write(bool a, bool b)
        {
            if(b)
            {
                rstcause_sysreq_bit.Value = false;
            }
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

        private readonly SiLabs_EMU_Base emuBase;
    }
}
