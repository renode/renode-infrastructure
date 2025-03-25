//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class EFR32xG2_DPLL_1
    {
        partial void En_En_ValueProvider(bool a)
        {
            en_en_bit.Value = moduleEnabled;
        }

        partial void En_En_Write(bool a, bool b)
        {
            moduleEnabled = b;
        }

        partial void If_Lock_ValueProvider(bool a)
        {
            if_lock_bit.Value = moduleEnabled;
        }

        partial void If_Lock_Write(bool a, bool b)
        {
            moduleEnabled = b;
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if (b == 0x7102)
            {
                status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
            }
            else
            {
                status_lock_bit.Value = STATUS_LOCK.LOCKED;
            }
        }

        partial void Status_Ens_ValueProvider(bool a)
        {
            status_ens_bit.Value = moduleEnabled;
        }

        partial void Status_Rdy_ValueProvider(bool a)
        {
            status_rdy_bit.Value = moduleEnabled;
        }

        partial void Cfg1_N_Write(ulong a, ulong b)
        {
            cmu.Dpll_N = b + 1;
        }

        partial void Cfg1_M_Write(ulong a, ulong b)
        {
            cmu.Dpll_M = b + 1;
        }

        private bool oscillatorEnabled
        {
            get { return cmu.OscDpllEnabled; }
        }

        private bool moduleEnabled = false;
    }
}
