//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;

using Range = Antmicro.Renode.Core.Range;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithMappedMemory: ICPU
    {
        void MapMemory(IMappedSegment segment);
        void RegisterAccessFlags(ulong startAddress, ulong size, bool isIoMemory = false);
        void SetMappedMemoryEnabled(Range range, bool enabled);
        void UnmapMemory(Range range);
        void SetPageAccessViaIo(ulong address);
        void ClearPageAccessViaIo(ulong address);
        void SetBroadcastDirty(bool enable);
    }
}

