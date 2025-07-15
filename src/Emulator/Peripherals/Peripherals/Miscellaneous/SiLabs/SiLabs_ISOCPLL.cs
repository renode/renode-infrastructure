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
    public interface SiLabs_ISOCPLL
    {
        /// <summary>
        /// Instance number for this SOCPLL instance.
        /// </summary>
        uint Instance { get; }

        /// <summary>
        /// Returns true if the HFXO is selected by the SOCPLL as the reference clock, false otherwise.
        /// </summary>
        bool IsUsingHfxo { get; }
    }
}