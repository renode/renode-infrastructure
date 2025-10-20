//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using static Antmicro.Renode.Peripherals.Miscellaneous.ExternalMmuBase;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithExternalMmu : ICPU
    {
        void EnableExternalWindowMmu(bool value);

        void EnableExternalWindowMmu(ExternalMmuPosition position);

        ulong AcquireExternalMmuWindow(Privilege type);

        void ResetMmuWindow(ulong id);

        void SetMmuWindowStart(ulong id, ulong startAddress);

        void SetMmuWindowEnd(ulong id, ulong endAddress);

        void SetMmuWindowAddend(ulong id, ulong addend);

        void SetMmuWindowPrivileges(ulong id, Privilege privileges);

        void AddHookOnMmuFault(Action<ulong, AccessType, ulong> hook);

        ulong GetMmuWindowStart(ulong id);

        ulong GetMmuWindowEnd(ulong id);

        ulong GetMmuWindowAddend(ulong id);

        uint GetMmuWindowPrivileges(ulong id);

        uint ExternalMmuWindowsCount { get; }
    }

    public enum AccessType
    {
        Read = 0,
        Write = 1,
        Execute = 2,
    }

    public enum ExternalMmuPosition
    {
        None = 0,
        Replace = 1,
        BeforeInternal = 2,
        AfterInternal = 3,
    }
}