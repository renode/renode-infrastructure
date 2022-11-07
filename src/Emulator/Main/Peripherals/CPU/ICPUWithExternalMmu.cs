//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithExternalMmu: ICPU
    {
        void EnableExternalWindowMmu(bool value);
        int AcquireExternalMmuWindow(uint type);
        void ResetMmuWindow(uint index);
        void SetMmuWindowStart(uint index, ulong startAddress);
        void SetMmuWindowEnd(uint index, ulong endAddress);
        void SetMmuWindowAddend(uint index, ulong addend);
        void SetMmuWindowPrivileges(uint index, uint privileges);
        void AddHookOnMmuFault(Action<ulong, AccessType, int> hook);

        ulong GetMmuWindowStart(uint index);
        ulong GetMmuWindowEnd(uint index);
        ulong GetMmuWindowAddend(uint index);
        uint GetMmuWindowPrivileges(uint index);

        uint ExternalMmuWindowsCount { get; }
    }

    public enum AccessType
    {
        Read = 0,
        Write = 1,
        Execute = 2,
    }
}
