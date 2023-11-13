using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public interface ICmu
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

        bool OscSocpllRequested { get; }
        bool OscSocpllEnabled { get; }

        bool OscDpllEnabled { get; }

        ulong Dpll_N { get; set; }
        ulong Dpll_M { get; set; }
    }
}
