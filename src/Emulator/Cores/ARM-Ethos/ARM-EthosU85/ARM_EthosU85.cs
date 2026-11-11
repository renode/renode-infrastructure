//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

// The M55 executes firmware; this device consumes the NPU queue submitted through MMIO.
public partial class ARM_EthosU85 : ARM_EthosBase
{
    public ARM_EthosU85(IMachine machine, int macsPerCycle = 256) : base(machine, macsPerCycle)
    {
    }

    public override long Size => 0x2000;

    private record U85Config(ulong MacsLog2, ulong AxiSramLog2, ulong AxiExtLog2, ulong NumWdLog2);

    private static readonly Dictionary<int, U85Config> Configs = new()
    {
        [128]  = new(7,  1, 0, 0),
        [256]  = new(8,  1, 0, 0),
        [512]  = new(9,  1, 0, 1),
        [1024] = new(10, 1, 1, 2),
        [2048] = new(11, 2, 1, 2),
    };

    protected override void SetConfig()
    {
        if(!Configs.TryGetValue(macsPerCycle, out var configs))
        {
            throw new ConstructionException("Ethos-U85 supports 128, 256, 512, 1024, or 2048 MACs per cycle");
        }
        this.configs = configs;
    }

    protected override void QueueUpdated()
    {
        base.QueueUpdated();
        if(!isRunning.Value)
        {
            resumeAfterStop = false;
        }
    }

    protected override void ResetNPU()
    {
        base.ResetNPU();
        // All registers and internal RAMs are cleared on reset (U85 TRM 2.2, 2.3.5)
        Array.Clear(lookupTable);
        commandRegisters = new U85CommandRegisterState();
        resumeAfterStop = false;
    }

    protected override bool RaisesIrqOnParseError => true; // U85 TRM 3.3.1.2

    protected override bool Supports64BitActivations => true;

    private bool resumeAfterStop;
    private U85Config configs = null!;
    private readonly byte[] lookupTable = new byte[2048]; // U85 TRM 2.6.2
}
