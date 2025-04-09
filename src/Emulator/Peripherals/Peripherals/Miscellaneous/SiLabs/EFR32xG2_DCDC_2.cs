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
    public partial class EFR32xG2_DCDC_2
    {

        partial void Status_Bypsw_ValueProvider(bool a)
        {
            // Enable BYPASS switch.
            status_bypsw_bit.Value = isEnabled;
        }

        partial void If_Regulation_ValueProvider(bool a)
        {
            // Complete DCDC startup
            if_regulation_bit.Value = isEnabled;
        }

        private bool isEnabled = true;
    }
}
