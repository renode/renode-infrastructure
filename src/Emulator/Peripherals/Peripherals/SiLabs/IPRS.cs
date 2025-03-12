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
    public interface IPrs
    {
        /// <summary>
        /// Reference to SYSRTC to be able to subscribe to events it produces
        /// </summary>
        ISysrtc sysrtc { get; set; }

        /// <summary>
        /// Reference to HFXO to be able to subscribe to events it produces
        /// </summary>
        IHfxo hfxo { get; set; }

        /// <summary>
        /// Forces implementers of IPrs to implement a function which triggers HFXO early wakeup
        /// </summary>
        void HfxoTriggerEarlyWakeup();

        /// <summary>
        /// Forces implementers of IPrs to implement a function which triggers a sysrtc capture for group 0
        /// </summary>
        void SysrtcCaptureGroup0();
    }
}