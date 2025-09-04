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

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public interface SiLabs_IProtocolTimer
    {
        event Action<uint> PreCountOverflowsEvent;
        event Action<uint> BaseCountOverflowsEvent;
        event Action<uint> WrapCountOverflowsEvent;
        event Action<uint> CaptureCompareEvent;
        uint GetCurrentPreCountOverflows();
        void FlushCurrentPreCountOverflows();
        uint GetPreCountOverflowFrequency();
        uint GetBaseCountValue();
        uint GetWrapCountValue();
    }
}