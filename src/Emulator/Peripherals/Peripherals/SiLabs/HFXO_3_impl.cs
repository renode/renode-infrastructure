using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public partial class Hfxo_3
    {
        partial void Ctrl_Forceen_Write(bool a, bool b)
        {
            oscillatorForce = (b == true);
            status_corebiasoptrdy_bit.Value = false;
        }

        partial void Ctrl_Disondemand_Write(bool a, bool b)
        {
            oscillatorOnDemand = (b != true);
            status_corebiasoptrdy_bit.Value = false;
        }

        partial void Cmd_Corebiasopt_Write(bool a, bool b)
        {
            if (b)
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
            status_rdy_bit.Value = isUsed;
        }

        partial void Status_Corebiasoptrdy_ValueProvider(bool a)
        {
            if (a == false)
            {
                status_corebiasoptrdy_bit.Value = true;
            }
            else if (a == true && temp_incr > 10)
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
            status_ens_bit.Value = isUsed;
        }

        partial void Status_Hwreq_ValueProvider(bool a)
        {
            status_hwreq_bit.Value = cmu.OscHfxoRequested && cmu.OscHfxoEnabled;
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if (b == 0x580E)
            {
                status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
            }
            else
            {
                status_lock_bit.Value = STATUS_LOCK.LOCKED;
            }
        }

        private bool isEnabled
        {
            get { return cmu.OscHfxoEnabled; }
        }

        private bool isUsed
        {
            get { return oscillatorForce || (oscillatorOnDemand && cmu.OscHfxoRequested); }
        }

        private ulong temp_incr = 0;
        private bool oscillatorOnDemand = false;
        private bool oscillatorForce = false;
    }
}
