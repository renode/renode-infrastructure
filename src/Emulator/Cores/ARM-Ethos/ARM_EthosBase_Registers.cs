//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Diagnostics.CodeAnalysis;

using Antmicro.Renode.Core.Structure.Registers;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public abstract partial class ARM_EthosBase
{
    [MemberNotNull(
        nameof(isRunning),
        nameof(irqHistoryMask),
        nameof(busStatus),
        nameof(queueBase0),
        nameof(queueBase1),
        nameof(queueSize),
        nameof(pendingSecurityLevel),
        nameof(pendingPrivilegeLevel),
        nameof(activeSecurityLevel),
        nameof(activePrivilegeLevel),
        nameof(commandParseError),
        nameof(queueReadOffset),
        nameof(commandEndReached),
        nameof(irqRaised))]
    protected abstract void DefineRegisters();

    protected IFlagRegisterField isRunning;
    protected IValueRegisterField irqHistoryMask;
    protected IFlagRegisterField busStatus;
    protected IValueRegisterField queueBase0;
    protected IValueRegisterField queueBase1;
    protected IValueRegisterField queueSize;
    protected IEnumRegisterField<SecurityLevel> pendingSecurityLevel;
    protected IEnumRegisterField<PrivilegeLevel> pendingPrivilegeLevel;
    protected IEnumRegisterField<SecurityLevel> activeSecurityLevel;
    protected IEnumRegisterField<PrivilegeLevel> activePrivilegeLevel;
    protected IFlagRegisterField commandParseError;
    protected IValueRegisterField queueReadOffset;
    protected IFlagRegisterField commandEndReached;
    protected IFlagRegisterField irqRaised;

    protected readonly IValueRegisterField[] basePointers = new IValueRegisterField[BasePointerCount];
    protected const uint BasePointerCount = 16;
}
