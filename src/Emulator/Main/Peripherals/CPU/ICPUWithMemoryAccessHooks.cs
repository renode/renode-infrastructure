//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Logging.Profiling;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithMemoryAccessHooks : ICPU
    {
        void SetHookAtMemoryAccess(MemoryAccessHook hook);
    }

    public delegate void MemoryAccessHook(
        ulong virtualPC,
        MemoryOperation operation,
        ulong virtualAddress,
        ulong physicalAddress,
        uint width,
        ulong value
    );
}