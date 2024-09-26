//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class RenesasDA14_DMA : RenesasDA_DMABase, IKnownSize, IGPIOReceiver
    {
        public RenesasDA14_DMA(IMachine machine) : base(machine, ChannelCount, ChannelCount / 2)
        {
            Registers.PeripheralsMapping.Define(this, 0x00000fff)
                .WithValueField(0, 4, out peripheralSelect[0], name: "DMA01_SEL")
                .WithValueField(4, 4, out peripheralSelect[1], name: "DMA23_SEL")
                .WithValueField(8, 4, out peripheralSelect[2], name: "DMA45_SEL")
                .WithReservedBits(ChannelCount * 2, 32 - ChannelCount * 2);

            RegistersCollection.AddRegister((long)Registers.InterruptStatus, interruptsManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.InterruptClear, interruptsManager.GetInterruptClearRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.InterruptMask, interruptsManager.GetInterruptEnableRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.SetInterruptMask, interruptsManager.GetInterruptEnableSetRegister<DoubleWordRegister>());
            RegistersCollection.AddRegister((long)Registers.ResetInterruptMask, interruptsManager.GetInterruptEnableClearRegister<DoubleWordRegister>());

            Reset();
        }

        public long Size => 0x118;

        private const int ChannelCount = 6;

        private enum Registers
        {
            PeripheralsMapping = 0x100,
            InterruptStatus = 0x104,
            InterruptClear = 0x108,
            InterruptMask = 0x10C,
            SetInterruptMask = 0x110,
            ResetInterruptMask = 0x114,
        }
    }
}

