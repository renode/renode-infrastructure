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
    public interface ICMU_EFR32xG2
    {
        bool OscLfrcoRequested { get; }
        bool OscLfrcoEnabled { get; }

        bool OscHfrcoRequested { get; }
        bool OscHfrcoEnabled { get; }

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
