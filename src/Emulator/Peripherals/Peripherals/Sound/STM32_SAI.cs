//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Gerzain Mata <leftger@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Sound
{
    public class STM32_SAI : IDoubleWordPeripheral, IKnownSize
    {
        private readonly DoubleWordRegisterCollection registers;
        public GPIO IRQ { get; } = new GPIO();

        private readonly Queue<uint> fifoA = new Queue<uint>();
        private readonly Queue<uint> fifoB = new Queue<uint>();

        private bool blockAEnabled;
        private bool blockBEnabled;
        private bool irqAEnabled;
        private bool irqBEnabled;

        public STM32_SAI(IMachine machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                // GCR at 0x00
                [0x00] = new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "SYNCIN")
                    .WithValueField(4, 2, name: "SYNCOUT"),

                // --- Sub-block A (0x04 - 0x20) ---
                [0x04] = new DoubleWordRegister(this) // CR1
                    .WithValueField(0, 2, name: "MODE")
                    .WithValueField(2, 2, name: "PRTCFG")
                    .WithValueField(5, 3, name: "DS")
                    .WithFlag(16, name: "SAIAEN", valueProviderCallback: _ => blockAEnabled, changeCallback: (_, val) => blockAEnabled = val)
                    .WithFlag(17, name: "DMAEN"),

                [0x08] = new DoubleWordRegister(this) // CR2
                    .WithValueField(0, 3, name: "FTH")
                    .WithFlag(3, FieldMode.Write, name: "FFLUSH", writeCallback: (_, val) => { if(val) fifoA.Clear(); }),

                [0x0C] = new DoubleWordRegister(this) // FRCR
                    .WithValueField(0, 8, name: "FRL")
                    .WithValueField(8, 7, name: "FSALL"),

                [0x10] = new DoubleWordRegister(this) // SLOTR
                    .WithValueField(0, 5, name: "FBOFF")
                    .WithValueField(8, 4, name: "NBSLOT")
                    .WithValueField(16, 16, name: "SLOTEN"),

                [0x14] = new DoubleWordRegister(this) // IMR
                    .WithFlag(0, name: "OVRUDRIE")
                    .WithFlag(3, name: "FREQIE", valueProviderCallback: _ => irqAEnabled, changeCallback: (_, val) => {
                        irqAEnabled = val;
                        UpdateInterrupts();
                    }),

                [0x18] = new DoubleWordRegister(this) // SR
                    .WithFlag(3, FieldMode.Read, name: "FREQ", valueProviderCallback: _ => fifoA.Count < 8)
                    .WithValueField(16, 3, FieldMode.Read, name: "FLVL", valueProviderCallback: _ => (uint)(fifoA.Count & 0x7)),

                [0x1C] = new DoubleWordRegister(this) // CLRFR
                    .WithFlag(0, FieldMode.Write, name: "COVRUDR")
                    .WithFlag(1, FieldMode.Write, name: "CMUTEDET"),

                [0x20] = new DoubleWordRegister(this) // DR
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => fifoA.Count > 0 ? fifoA.Dequeue() : 0u,
                        writeCallback: (_, val) => {
                            if(fifoA.Count < 16) fifoA.Enqueue((uint)val);
                            UpdateInterrupts();
                        }),

                // --- Sub-block B (0x24 - 0x40) ---
                [0x24] = new DoubleWordRegister(this) // CR1
                    .WithValueField(0, 2, name: "MODE")
                    .WithValueField(2, 2, name: "PRTCFG")
                    .WithValueField(5, 3, name: "DS")
                    .WithFlag(16, name: "SAIBEN", valueProviderCallback: _ => blockBEnabled, changeCallback: (_, val) => blockBEnabled = val)
                    .WithFlag(17, name: "DMAEN"),

                [0x28] = new DoubleWordRegister(this) // CR2
                    .WithValueField(0, 3, name: "FTH")
                    .WithFlag(3, FieldMode.Write, name: "FFLUSH", writeCallback: (_, val) => { if(val) fifoB.Clear(); }),

                [0x2C] = new DoubleWordRegister(this) // FRCR
                    .WithValueField(0, 8, name: "FRL")
                    .WithValueField(8, 7, name: "FSALL"),

                [0x30] = new DoubleWordRegister(this) // SLOTR
                    .WithValueField(0, 5, name: "FBOFF")
                    .WithValueField(8, 4, name: "NBSLOT")
                    .WithValueField(16, 16, name: "SLOTEN"),

                [0x34] = new DoubleWordRegister(this) // IMR
                    .WithFlag(0, name: "OVRUDRIE")
                    .WithFlag(3, name: "FREQIE", valueProviderCallback: _ => irqBEnabled, changeCallback: (_, val) => {
                        irqBEnabled = val;
                        UpdateInterrupts();
                    }),

                [0x38] = new DoubleWordRegister(this) // SR
                    .WithFlag(3, FieldMode.Read, name: "FREQ", valueProviderCallback: _ => fifoB.Count < 8)
                    .WithValueField(16, 3, FieldMode.Read, name: "FLVL", valueProviderCallback: _ => (uint)(fifoB.Count & 0x7)),

                [0x3C] = new DoubleWordRegister(this) // CLRFR
                    .WithFlag(0, FieldMode.Write, name: "COVRUDR"),

                [0x40] = new DoubleWordRegister(this) // DR
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => fifoB.Count > 0 ? fifoB.Dequeue() : 0u,
                        writeCallback: (_, val) => {
                            if(fifoB.Count < 16) fifoB.Enqueue((uint)val);
                            UpdateInterrupts();
                        }),

                // PDM registers (0x44 - 0x48)
                [0x44] = new DoubleWordRegister(this) // PDMCR
                    .WithFlag(0, name: "PDMEN")
                    .WithValueField(4, 2, name: "MICNBR"),

                [0x48] = new DoubleWordRegister(this) // PDMDLY
                    .WithValueField(0, 3, name: "DLYM1")
            };

            registers = new DoubleWordRegisterCollection(this, map);
        }

        private void UpdateInterrupts()
        {
            var pendingA = irqAEnabled && blockAEnabled && (fifoA.Count < 8);
            var pendingB = irqBEnabled && blockBEnabled && (fifoB.Count < 8);
            IRQ.Set(pendingA || pendingB);
        }

        public uint ReadDoubleWord(long offset) => registers.Read(offset);
        public void WriteDoubleWord(long offset, uint value) => registers.Write(offset, value);
        public void Reset()
        {
            fifoA.Clear();
            fifoB.Clear();
            blockAEnabled = false;
            blockBEnabled = false;
            irqAEnabled = false;
            irqBEnabled = false;
            IRQ.Set(false);
            registers.Reset();
        }

        public long Size => 0x400;
    }
}
