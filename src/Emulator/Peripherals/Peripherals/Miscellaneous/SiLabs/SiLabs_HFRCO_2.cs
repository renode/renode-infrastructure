//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_HFRCO_2
    {
        partial void HFRCO_Reset()
        {
            oscillatorEM23OnDemand = false;
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

        partial void Ctrl_Em23ondemand_Write(bool a, bool b)
        {
            oscillatorEM23OnDemand = (b != true);
        }

        partial void Lock_Lockkey_Write(ulong a, ulong b)
        {
            if(b == 0x8195)
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
            status_rdy_bit.Value = IsUsed;
        }

        partial void Status_Ens_ValueProvider(bool a)
        {
            status_ens_bit.Value = IsUsed;
        }

        partial void If_Rdy_ValueProvider(bool a)
        {
            if_rdy_bit.Value = IsUsed && ien_rdy_bit.Value;
        }

        private bool IsUsed
        {
            get
            {
                return oscillatorForce
                    || ((oscillatorOnDemand || oscillatorEM23OnDemand) || cmu.OscHfrcoRequested);
            }
        }

        private bool IsEnabled
        {
            get { return cmu.OscHfrcoEnabled; }
        }

        private bool oscillatorEM23OnDemand = false;
        private bool oscillatorOnDemand = false;
        private bool oscillatorForce = false;
    }
}