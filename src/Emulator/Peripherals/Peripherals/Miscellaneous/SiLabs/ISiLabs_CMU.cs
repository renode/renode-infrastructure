//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    // Common encoding of the WDOG{n}CLKCTRL.CLKSEL register field. The
    // numeric values match every per-CMU WDOG0CLKCTRL_CLKSEL /
    // WDOG1CLKCTRL_CLKSEL enum we have generated, so CMU implementations can
    // cast their typed enum value through int into this shared enum.
    public enum SiLabs_WdogClksel
    {
        Disabled    = 0,
        Lfrco       = 1,
        Lfxo        = 2,
        Ulfrco      = 3,
        HclkDiv1024 = 4,
    }

    // Helper constants and conversions shared across CMU implementations.
    public static class SiLabs_CMU_Constants
    {
        // Convert a CLKSEL value into the corresponding clock frequency in
        // Hz. The HCLKDIV1024 source is approximated as HFXO / 1024, which
        // matches the default xG24/xG26 configurations where HCLK is sourced
        // from HFXO. If a CMU has a more accurate notion of HCLK it can
        // bypass this helper for that case.
        public static ulong WdogFrequencyFromClksel(SiLabs_WdogClksel clksel, ulong hfxoFrequency)
        {
            switch(clksel)
            {
            case SiLabs_WdogClksel.Disabled: return 0UL;
            case SiLabs_WdogClksel.Lfrco: return LfrcoFrequency;
            case SiLabs_WdogClksel.Lfxo: return LfxoFrequency;
            case SiLabs_WdogClksel.Ulfrco: return UlfrcoFrequency;
            case SiLabs_WdogClksel.HclkDiv1024: return hfxoFrequency / 1024UL;
            default: return 0UL;
            }
        }

        public const ulong LfrcoFrequency  = 32768UL;
        public const ulong LfxoFrequency   = 32768UL;
        public const ulong UlfrcoFrequency = 1000UL;
    }

    public interface ISiLabs_CMU
    {
        bool GetPeripheralClockEnable(IPeripheral peripheral);

        ulong HfxoFrequency { get; }

        // Returns the frequency, in Hz, that the WDOG instance is currently 
        // clocked at. A return value of 0 means the selected source is 
        // DISABLED (the WDOG should not be running).
        ulong WdogClockFrequency(uint instance);

        bool OscLfrcoRequested { get; }

        bool OscLfrcoEnabled { get; }

        bool OscHfrcoRequested(uint instance);

        bool OscHfrcoEnabled(uint instance);

        bool OscHfrcoEM23Requested { get; }

        bool OscHfrcoEM23Enabled { get; }

        bool OscHfxoRequested { get; }

        bool OscHfxoEnabled { get; }

        bool OscLfxoRequested { get; }

        bool OscLfxoEnabled { get; }

        bool OscSocpllRequested(uint instance);

        bool OscSocpllEnabled(uint instance);

        bool OscPerpllRequested(uint instance);

        bool OscPerpllEnabled(uint instance);

        bool OscDpllEnabled { get; }

        ulong Dpll_N { get; set; }

        ulong Dpll_M { get; set; }
    }
}
