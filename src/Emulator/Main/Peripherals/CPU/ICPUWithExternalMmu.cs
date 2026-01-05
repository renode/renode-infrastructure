//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using static Antmicro.Renode.Peripherals.Miscellaneous.ExternalMmuBase;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUWithExternalMmu : ICPU
    {
        void EnableExternalWindowMmu(bool value);

        void EnableExternalWindowMmu(ExternalMmuPosition position);

        ulong AcquireExternalMmuWindow(Privilege type);

        void ResetMmuWindow(ulong id);

        void ResetMmuWindowsCoveringAddress(ulong address);

        void ResetAllMmuWindows();

        void SetMmuWindowStart(ulong id, ulong startAddress);

        void SetMmuWindowEnd(ulong id, ulong endAddress);

        void SetMmuWindowAddend(ulong id, ulong addend);

        void SetMmuWindowPrivileges(ulong id, Privilege privileges);

        void AddHookOnMmuFault(ExternalMmuFaultHook hook);

        void RemoveHookOnMmuFault(ExternalMmuFaultHook hook);

        ulong GetMmuWindowStart(ulong id);

        ulong GetMmuWindowEnd(ulong id);

        ulong GetMmuWindowAddend(ulong id);

        uint GetMmuWindowPrivileges(ulong id);

        void FlushTlb();

        void FlushTlbPage(ulong address);

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

    // Must match the same enum on the C side
    public enum ExternalMmuResult
    {
        NoFault = 0,
        Fault = 1,
        ExternalAbort = 2,
    }

    // windowId is null if the address was not found in any of the defined windows
    public delegate ExternalMmuResult ExternalMmuFaultHook(ulong address, AccessType accessType, ulong? windowId, bool firstTry);
}
