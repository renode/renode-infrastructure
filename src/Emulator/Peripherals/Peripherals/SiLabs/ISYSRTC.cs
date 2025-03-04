//
// Copyright (c) 2010-2025 Silicon Labs
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

namespace Antmicro.Renode.Peripherals.Silabs
{
    public interface ISysrtc
    {
        /// <summary>
        /// Event which is invoked upon a compare match for group 0 channel 0.
        /// </summary>
        event Action CompareMatchGroup0Channel0;

        /// <summary>
        /// Event which is invoked upon a compare match for group 0 channel 1.
        /// </summary>
        event Action CompareMatchGroup0Channel1;

        /// <summary>
        /// Method which can be called by the PRS (or other infrastructure peripherals) to 
        /// save the current count to the Group0Capture0 register.
        /// </summary>
        void CaptureGroup0();
    }
}