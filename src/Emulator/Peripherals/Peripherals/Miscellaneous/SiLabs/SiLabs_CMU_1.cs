//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public partial class SiLabs_CMU_1 : BasicDoubleWordPeripheral, IKnownSize, SiLabs_ICMU
    {
        public SiLabs_CMU_1(Machine machine, SiLabs_IHFXO hfxo, SiLabs_DPLL_0 dpll = null) : base(machine)
        {
            this.dpll = dpll;
            this.hfxo = hfxo;

            Define_Registers();
            CMU_Reset();
        }

        partial void CMU_Reset()
        {
            sysclkctrl_clksel_field.Value = SYSCLKCTRL_CLKSEL.FSRCO;
            status_calrdy_bit.Value = true;
            status_lock_bit.Value = STATUS_LOCK.UNLOCKED;
        }

        public bool OscSocpllRequested(uint instance)
        {
            return false;
        }

        public bool OscSocpllEnabled(uint instance)
        {
            return false;
        }

        public bool OscPerpllRequested(uint instance)
        {
            return false;
        }

        public bool OscPerpllEnabled(uint instance)
        {
            return false;
        }

        public bool OscLfrcoRequested { get; }

        public bool OscLfrcoEnabled { get; }

        public bool OscHfrcoRequested { get; }

        public bool OscHfrcoEnabled { get; }

        public bool OscHfrcoEM23Requested { get; }

        public bool OscHfrcoEM23Enabled { get; }

        public bool OscHfxoRequested { get; }

        public bool OscHfxoEnabled { get; }

        public bool OscLfxoRequested { get; }

        public bool OscLfxoEnabled { get; }

        public bool OscDpllEnabled { get; }

        public ulong Dpll_N { get; set; }

        public ulong Dpll_M { get; set; }

        // Reference to dpll to check if a peripheral is enabled
        private readonly SiLabs_DPLL_0 dpll;
        // Reference to hfxo to be able to call events it defines
        private readonly SiLabs_IHFXO hfxo;
    }
}