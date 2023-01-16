//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithMappedMemory: ICPU
    {
        void MapMemory(IMappedSegment segment);
        void UnmapMemory(Range range);
        void SetPageAccessViaIo(ulong address);
        void ClearPageAccessViaIo(ulong address);
        void SetBroadcastDirty(bool enable);
    }
}

