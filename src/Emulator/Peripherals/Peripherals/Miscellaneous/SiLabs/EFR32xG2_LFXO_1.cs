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
    public partial class EFR32xG2_LFXO_1
    {
        partial void EFR32xG2_LFXO_1_Constructor()
        {
        }

        private void BusFault(uint exception)
        {
            this.Log(LogLevel.Error, "LFXO is locked, BusFault!!");
            if (
                machine.SystemBus.TryGetCurrentCPU(out var cpu)
                && cpu is TranslationCPU translationCPU
            )
            {
                translationCPU.RaiseException(exception);
            }
        }

        partial void Ctrl_Write(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                BusFault(EXCP_PREFETCH_ABORT);
            }
        }

        partial void Cfg_Write(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                BusFault(EXCP_PREFETCH_ABORT);
            }
        }

        partial void Cfg1_Write(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                BusFault(EXCP_PREFETCH_ABORT);
            }
        }

        partial void Cal_Write(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                BusFault(EXCP_PREFETCH_ABORT);
            }
        }

        partial void Ctrl_Forceen_Write(bool a, bool b)
        {
            oscillatorForce = (b == true);
        }

        partial void Ctrl_Disondemand_Write(bool a, bool b)
        {
            oscillatorOnDemand = (b != true);
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if (b == 0x1A20)
            {
                status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
            }
            else
            {
                status_lock_bit.Value = STATUS_LOCK.LOCKED;
            }
        }

        partial void Status_Rdy_ValueProvider(bool a)
        {
            status_rdy_bit.Value = oscillatorUsed;
        }

        partial void Status_Ens_ValueProvider(bool a)
        {
            status_ens_bit.Value = oscillatorUsed;
        }

        partial void If_Rdy_ValueProvider(bool a)
        {
            if_rdy_bit.Value = oscillatorUsed && ien_rdy_bit.Value;
        }

        private bool oscillatorEnabled
        {
            get { return cmu.OscLfxoEnabled; }
        }

        private bool oscillatorUsed
        {
            get { return oscillatorForce || oscillatorOnDemand; }
        }

        private bool oscillatorOnDemand = false;
        private bool oscillatorForce = false;
        // See constant definitions in src/Infrastructure/src/Emulator/Cores/tlib/arch/arm/cpu.h
        private const uint EXCP_PREFETCH_ABORT = 3;
    }
}
