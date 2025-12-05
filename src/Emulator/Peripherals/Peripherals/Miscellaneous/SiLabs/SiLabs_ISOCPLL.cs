//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public interface SiLabs_ISOCPLL
    {
        /// <summary>
        /// Instance number for this SOCPLL instance.
        /// </summary>
        uint Instance { get; }

        /// <summary>
        /// Returns true if the SOCPLL is enabled, false otherwise.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Returns true if the SOCPLL is ready, false otherwise.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Returns true if the HFXO is selected by the SOCPLL as the reference clock, false otherwise.
        /// </summary>
        bool IsUsingHfxo { get; }
    }
}