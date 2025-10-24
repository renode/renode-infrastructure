//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_EMU_3 : ISiLabs_Emu
    {
        partial void SiLabs_EMU_3_Constructor()
        {
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

        public void UpdatePeripheralEnabledStatus(IPeripheral peripheral, bool enabled)
        {
            emuBase.UpdatePeripheralEnabledStatus(peripheral, enabled);
        }

        public List<IPeripheral> GetNoRetainPeripherals()
        {
            return emuBase.GetNoRetainPeripherals();
        }

        private SiLabs_EMU_Base emuBase;
    }
}
