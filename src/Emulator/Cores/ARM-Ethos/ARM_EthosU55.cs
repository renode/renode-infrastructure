//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.DMA;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

// The M55 executes firmware; this device consumes the NPU queue submitted through MMIO.
public partial class ARM_EthosU55 : BasicDoubleWordPeripheral, IKnownSize
{
    public ARM_EthosU55(IMachine machine, int macsPerCycle = 256) : base(machine)
    {
        if(macsPerCycle != 128 && macsPerCycle != 256)
        {
            throw new ConstructionException("Ethos-U55 supports 128 or 256 MACs per cycle");
        }

        this.macsPerCycle = macsPerCycle;
        IRQ = new GPIO();
        dmaEngine = new DmaEngine(machine.GetSystemBus(this));

        DefineRegisters();
        Reset();
    }

    public override void Reset()
    {
        base.Reset();
        commandRegisters = new CommandRegisterState();
        UpdateInterrupts();
    }

    public long Size => 0x1000;

    public GPIO IRQ { get; }

    private void SoftReset()
    {
        var security = pendingSecurityLevel.Value;
        var privilege = pendingPrivilegeLevel.Value;
        Reset();
        pendingSecurityLevel.Value = security;
        pendingPrivilegeLevel.Value = privilege;
        activeSecurityLevel.Value = security;
        activePrivilegeLevel.Value = privilege;
    }

    private void QueueUpdated()
    {
        if(!isRunning.Value)
        {
            commandEndReached.Value = false;
        }
    }

    private void UpdateInterrupts()
    {
        IRQ.Set(irqRaised.Value);
    }

    private ulong QueueBaseAddress => queueBase0.Value | (queueBase1.Value << 32);

    private IValueRegisterField irqHistoryMask;
    private IFlagRegisterField commandEndReached;
    private IFlagRegisterField commandParseError;
    private IFlagRegisterField busStatus;
    private IFlagRegisterField irqRaised;
    private IFlagRegisterField isRunning;
    private IValueRegisterField lockValue;
    private IValueRegisterField queueBase0;
    private IValueRegisterField queueBase1;
    private IValueRegisterField queueReadOffset;
    private IValueRegisterField queueSize;
    private IEnumRegisterField<SecurityLevel> pendingSecurityLevel;
    private IEnumRegisterField<PrivilegeLevel> pendingPrivilegeLevel;
    private IEnumRegisterField<SecurityLevel> activeSecurityLevel;
    private IEnumRegisterField<PrivilegeLevel> activePrivilegeLevel;

    private readonly IValueRegisterField[] basePointers = new IValueRegisterField[BasePointerCount];

    private readonly int macsPerCycle;
    private readonly DmaEngine dmaEngine;
}
