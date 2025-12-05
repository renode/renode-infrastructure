//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

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