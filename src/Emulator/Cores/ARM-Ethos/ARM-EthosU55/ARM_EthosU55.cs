//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;

#nullable enable

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

// The M55 executes firmware; this device consumes the NPU queue submitted through MMIO.
public partial class ARM_EthosU55 : ARM_EthosBase
{
    public ARM_EthosU55(IMachine machine, int macsPerCycle = 256) : base(machine, macsPerCycle)
    {
    }

    public override void Reset()
    {
        base.Reset();
        ClearWeightCache();
    }

    public override long Size => 0x1000;

    protected override void ResetNPU()
    {
        base.ResetNPU();
        // The shared buffer RAMs are cleared on reset (U55 TRM 3.3.4.1).
        commandRegisters = new CommandRegisterState();
        Array.Clear(sharedBuffer);
    }

    protected override void SetConfig()
    {
        if(macsPerCycle != 128 && macsPerCycle != 256)
        {
            throw new ConstructionException("Ethos-U55 supports 128 or 256 MACs per cycle");
        }
        sharedBuffer = new byte[(macsPerCycle == 128 ? 24 : 48) * 1024];
    }

    private byte[] sharedBuffer = null!;
}
