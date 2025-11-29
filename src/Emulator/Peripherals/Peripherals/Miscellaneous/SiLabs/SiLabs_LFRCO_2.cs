//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_LFRCO_2
    {
        partial void LFRCO_Reset()
        {
            oscillatorOnDemand = false;
            oscillatorForce = false;

            registers.Reset();
        }

        partial void Ctrl_Forceen_Write(bool a, bool b)
        {
            oscillatorForce = (b == true);
        }

        partial void Ctrl_Disondemand_Write(bool a, bool b)
        {
            oscillatorOnDemand = (b != true);
        }

        partial void Status_Rdy_ValueProvider(bool a)
        {
            status_rdy_bit.Value = IsUsed;
        }

        partial void Status_Ens_ValueProvider(bool a)
        {
            status_ens_bit.Value = IsUsed;
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if(b == 0xF93)
            {
                status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
            }
            else
            {
                status_lock_bit.Value = STATUS_LOCK.LOCKED;
            }
        }

        private bool IsUsed
        {
            get { return oscillatorForce || (oscillatorOnDemand && cmu.OscLfrcoRequested); }
        }

        private bool IsEnabled
        {
            get { return IsUsed; }
        }

        private bool oscillatorOnDemand = false;
        private bool oscillatorForce = false;
    }
}