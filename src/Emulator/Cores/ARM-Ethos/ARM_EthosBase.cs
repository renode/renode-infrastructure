//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.DMA;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

// The M55 executes firmware; this device consumes the NPU queue submitted through MMIO.
public abstract partial class ARM_EthosBase : BasicDoubleWordPeripheral, IKnownSize
{
    public override void Reset()
    {
        ResetNPU();
    }

    public GPIO IRQ { get; }

    public abstract long Size { get; }

    protected ARM_EthosBase(IMachine machine, int macsPerCycle = 256) : base(machine)
    {
        this.macsPerCycle = macsPerCycle;
        IRQ = new GPIO();
        dmaEngine = new DmaEngine(machine.GetSystemBus(this));
        SetConfig();
        DefineRegisters();
        Reset();
    }

    protected virtual void ResetNPU()
    {
        base.Reset();
        // All registers and internal RAMs are cleared on reset (U85 TRM 2.2, 2.3.5).
        UpdateInterrupts();
    }

    protected void StopAndRaiseIrq(ushort irqMask = 0)
    {
        isRunning.Value = false;
        irqHistoryMask.Value |= irqMask;
        irqRaised.Value = true;
        UpdateInterrupts();
    }

    protected void ParseError(string reason)
    {
        commandParseError.Value = true;
        this.ErrorLog("Command parse error at +0x{0:X}: {1}", queueReadOffset.Value, reason);
        if(RaisesIrqOnParseError)
        {
            StopAndRaiseIrq();
        }
        else
        {
            isRunning.Value = false;
        }
    }

    protected virtual void QueueUpdated()
    {
        if(!isRunning.Value)
        {
            commandEndReached.Value = false;
        }
    }

    protected void UpdateInterrupts()
    {
        IRQ.Set(irqRaised.Value);
    }

    protected void SoftReset()
    {
        var privilege = pendingPrivilegeLevel.Value;
        var security = pendingSecurityLevel.Value;
        ResetNPU();
        // RESET keeps the pending levels written by this access (U85 TRM 3.3.1.4).
        pendingPrivilegeLevel.Value = privilege;
        pendingSecurityLevel.Value = security;
        activePrivilegeLevel.Value = privilege;
        activeSecurityLevel.Value = security;
    }

    protected abstract void SetConfig();

    protected ulong QueueBaseAddress => queueBase0.Value | (queueBase1.Value << 32);

    protected abstract bool RaisesIrqOnParseError { get; }

    protected abstract bool Supports64BitActivations { get; }

    protected readonly int macsPerCycle;

    protected readonly DmaEngine dmaEngine;

    protected enum SecurityLevel
    {
        Secure = 0,
        NonSecure = 1,
    }

    protected enum PrivilegeLevel
    {
        User = 0,
        Privileged = 1,
    }
}
