//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_SOCPLL_1 : SiLabs_ISOCPLL
    {
        partial void SOCPLL_Reset()
        {
            oscillatorOnDemand = false;
            oscillatorForce = false;

            registers.Reset();
        }

        partial void Ctrl_Socpll_Forceen_Write(bool a, bool b)
        {
            oscillatorForce = (b == true);
        }

        partial void Ctrl_Socpll_Disondemand_Write(bool a, bool b)
        {
            oscillatorOnDemand = (b != true);
        }

        partial void Status_Socpll_Rdy_ValueProvider(bool a)
        {
            status_socpll_rdy_bit.Value = IsReady;
        }

        partial void Status_Socpll_Plllock_ValueProvider(bool a)
        {
            status_socpll_plllock_bit.Value = IsReady;
        }

        partial void Status_Socpll_Ens_ValueProvider(bool a)
        {
            status_socpll_ens_bit.Value = IsReady;
        }

        partial void Lock_Socpll_Lockkey_Write(ulong a, ulong b)
        {
            if(b == 0x81A6)
            {
                status_socpll_lock_bit.Value = STATUS_SOCPLL_LOCK.UNLOCKED;
            }
            else
            {
                status_socpll_lock_bit.Value = STATUS_SOCPLL_LOCK.LOCKED;
            }
        }

        public bool IsReady
        {
            get { return oscillatorForce || (oscillatorOnDemand && cmu.OscSocpllRequested(0)); }
        }

        public uint Instance
        {
            get { return 0; }
        }

        public bool IsUsingHfxo
        {
            get { return (ctrl_socpll_refclksel_field.Value == CTRL_SOCPLL_REFCLKSEL.REF_HFXO || ctrl_socpll_refclksel_field.Value == CTRL_SOCPLL_REFCLKSEL.DEFAULT_HFXO); }
        }

        private bool isEnabled
        {
            get { return cmu.OscSocpllEnabled(0); }
        }

        private bool oscillatorOnDemand = false;
        private bool oscillatorForce = false;
    }
}