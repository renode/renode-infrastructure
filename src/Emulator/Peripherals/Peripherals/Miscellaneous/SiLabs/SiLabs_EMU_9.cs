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
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_EMU_9 : BasicDoubleWordPeripheral, IKnownSize, ISiLabs_Emu
    {
        public SiLabs_EMU_9(Machine machine) : base(machine)
        {
            Define_Registers();
            this.emuBase = new SiLabs_EMU_Base(machine, this);
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
        }

        public List<IPeripheral> GetNoRetainPeripherals()
        {
            return emuBase.GetNoRetainPeripherals();
        }

        public SiLabs_EnergyMode CurrentEnergyMode => emuBase.CurrentEnergyMode;

        public bool HostClocksEnabled => emuBase.HostClocksEnabled;

        public bool HostAllClocksEnabled => emuBase.HostAllClocksEnabled;

        private readonly SiLabs_EMU_Base emuBase;
    }
}
