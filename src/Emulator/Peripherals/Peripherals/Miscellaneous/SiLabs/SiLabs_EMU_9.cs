//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_EMU_9 : BasicDoubleWordPeripheral, IKnownSize, SiLabs_IEmu
    {
        public SiLabs_EMU_9(Machine machine) : base(machine)
        {
            Define_Registers();
            this.emuBase = new EmuBase(machine);
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

        public void AddEnterDeepSleepHook(Action hook)
        {
            emuBase.AddEnterDeepSleepHook(hook);
        }

        public void AddExitDeepSleepHook(Action hook)
        {
            emuBase.AddExitDeepSleepHook(hook);
        }

        private readonly EmuBase emuBase;
    }
}