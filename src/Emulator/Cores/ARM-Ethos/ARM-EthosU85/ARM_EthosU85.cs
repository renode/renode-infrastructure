//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.DMA;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

// The M55 executes firmware; this device consumes the NPU queue submitted through MMIO.
public partial class ARM_EthosU85 : BasicDoubleWordPeripheral, IKnownSize
{
    public ARM_EthosU85(IMachine machine, int macsPerCycle = 256) : base(machine)
    {
        if(!Configs.TryGetValue(macsPerCycle, out var configs))
        {
            throw new ConstructionException("Ethos-U85 supports 128, 256, 512, 1024, or 2048 MACs per cycle");
        }
        this.configs = configs;
        this.macsPerCycle = macsPerCycle;
        IRQ = new GPIO();
        dmaEngine = new DmaEngine(machine.GetSystemBus(this));
        lookupTable = new byte[2048]; // U85 TRM 2.6.2

        DefineRegisters();
        Reset();
    }

    public override void Reset()
    {
        base.Reset();
        commandRegisters = new CommandRegisterState();
        Array.Fill<byte>(lookupTable, 0);
        // All registers and internal RAMs are cleared on reset (U85 TRM 2.2, 2.3.5).
        UpdateInterrupts();
        resumeAfterStop = false;
    }

    public long Size => 0x2000;

    public GPIO IRQ { get; }

    private void SoftReset()
    {
        var privilege = pendingPrivilegeLevel.Value;
        var security = pendingSecurityLevel.Value;
        Reset();
        // RESET keeps the pending levels written by this access (U85 TRM 3.3.1.4).
        pendingPrivilegeLevel.Value = privilege;
        pendingSecurityLevel.Value = security;
        activePrivilegeLevel.Value = privilege;
        activeSecurityLevel.Value = security;
    }

    private record U85Config(ulong MacsLog2, ulong AxiSramLog2, ulong AxiExtLog2, ulong NumWdLog2);

    private static readonly Dictionary<int, U85Config> Configs = new()
    {
        [128]  = new(7,  1, 0, 0),
        [256]  = new(8,  1, 0, 0),
        [512]  = new(9,  1, 0, 1),
        [1024] = new(10, 1, 1, 2),
        [2048] = new(11, 2, 1, 2),
    };

    private void QueueUpdated()
    {
        if(!isRunning.Value)
        {
            commandEndReached.Value = false;
            resumeAfterStop = false;
        }
    }

    private void UpdateInterrupts()
    {
        IRQ.Set(irqRaised.Value);
    }

    private ulong QueueBaseAddress => queueBase0.Value | (queueBase1.Value << 32);

    private bool resumeAfterStop;

    private IFlagRegisterField isRunning;
    private IValueRegisterField irqHistoryMask;
    private IFlagRegisterField busStatus;
    private IValueRegisterField queueBase0;
    private IValueRegisterField queueBase1;
    private IValueRegisterField queueSize;
    private IEnumRegisterField<SecurityLevel> pendingSecurityLevel;
    private IEnumRegisterField<PrivilegeLevel> pendingPrivilegeLevel;
    private IEnumRegisterField<SecurityLevel> activeSecurityLevel;
    private IEnumRegisterField<PrivilegeLevel> activePrivilegeLevel;
    private IFlagRegisterField commandParseError;
    private IValueRegisterField queueReadOffset;
    private IFlagRegisterField commandEndReached;
    private IFlagRegisterField irqRaised;
    private readonly int macsPerCycle;

    private readonly U85Config configs;
    private readonly DmaEngine dmaEngine;
    private readonly byte[] lookupTable;
}
