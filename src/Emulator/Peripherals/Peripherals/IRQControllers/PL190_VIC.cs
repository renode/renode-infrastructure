//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

using Collections = Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class PL190_VIC : BasicDoubleWordPeripheral, IIRQController, IKnownSize
    {
        public PL190_VIC(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            FIQ = new GPIO();
            interrupts = new Interrupt[NumberOfInputLines];
            for(var i = 0; i < interrupts.Length; i++)
            {
                interrupts[i] = new Interrupt(i);
            }

            activeInterrupts = new Collections.PriorityQueue<Interrupt, int>();
            servicedInterrupts = new Stack<Interrupt>();

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            activeInterrupts.Clear();
            servicedInterrupts.Clear();
            IRQ.Set(false);
            FIQ.Set(false);
            foreach(var interrupt in interrupts)
            {
                interrupt.Reset();
            }
            base.Reset();
        }

        public void OnGPIO(int id, bool state)
        {
            if(id < 0 || id >= NumberOfInputLines)
            {
                this.Log(LogLevel.Error, "GPIO number {0} is out of range [0; {1})", id, NumberOfInputLines);
                return;
            }

            this.Log(LogLevel.Debug, "GPIO #{0} state changed to {1}", id, state);
            interrupts[id].PinState = state;
            UpdateInterrupts(id);
        }

        public GPIO IRQ { get; }
        public GPIO FIQ { get; }

        public long Size => 0x1000;

        private Interrupt FinishCurrentInterrupt()
        {
            var result = servicedInterrupts.Pop();
            ClearInactiveInterrupts();
            return result;
        }

        private void ClearInactiveInterrupts()
        {
            // clear all inactive interrupts (might have been disabled in the meantime)
            while(activeInterrupts.Count > 0 && !activeInterrupts.Peek().IsActive)
            {
                activeInterrupts.Dequeue();
            }
            while(servicedInterrupts.Count > 0 && !servicedInterrupts.Peek().IsActive)
            {
                servicedInterrupts.Pop();
            }
        }

        private void UpdateInterrupts(int id)
        {
            lock(activeInterrupts)
            {
                var lineStatus = interrupts[id].IsActive;

                // IRQ line deactivated
                if(lineStatus == false)
                {
                    ClearInactiveInterrupts();
                    RefreshIrqFiqState();
                }
                else
                {
                    // FIQ is handled separately, the vector address register is not used
                    if(interrupts[id].IsIrq)
                    {
                        activeInterrupts.Enqueue(interrupts[id], interrupts[id].Priority);
                    }
                    RefreshIrqFiqState();
                }
            }
        }

        private void RefreshIrqFiqState()
        {
            var irq = activeInterrupts.TryPeek(out var interrupt, out _) && interrupt.IsActive;
            var fiq = interrupts.Any(intr => intr.IsFiq && intr.IsActive);
            this.Log(LogLevel.Noisy, "Setting outputs: IRQ={0}, FIQ={1}", irq, fiq);
            IRQ.Set(irq);
            FIQ.Set(fiq);
        }

        private void DefineRegisters()
        {
            Registers.IrqStatus.Define(this)
                .WithFlags(0, 32, FieldMode.Read, valueProviderCallback: (idx, _) => interrupts[idx].IsActive && interrupts[idx].IsIrq);

            Registers.FiqStatus.Define(this)
                .WithFlags(0, 32, FieldMode.Read, valueProviderCallback: (idx, _) => interrupts[idx].IsActive && interrupts[idx].IsFiq);

            Registers.InterruptSelect.Define(this)
                .WithFlags(0, 32, name: "IntSelect",
                    valueProviderCallback: (idx, _) => interrupts[idx].IsFiq,
                    writeCallback: (idx, _, value) =>
                    {
                        interrupts[idx].IsIrq = !value;
                        this.Log(LogLevel.Debug, "Interrupt #{0} configured as {1}", idx, value ? "FIQ" : "IRQ");
                        UpdateInterrupts(idx);
                    });

            Registers.InterruptEnable.Define(this)
                .WithFlags(0, 32, FieldMode.Set, name: "IntEnable",
                    writeCallback: (idx, _, value) =>
                    {
                        if(value)
                        {
                            interrupts[idx].Enabled = true;
                            this.Log(LogLevel.Debug, "Interrupt #{0} enabled", idx);
                            UpdateInterrupts(idx);
                        }
                    });

            Registers.InterruptEnableClear.Define(this)
                .WithFlags(0, 32, FieldMode.Set, name: "IntEnableClear",
                    writeCallback: (idx, _, value) =>
                    {
                        if(value)
                        {
                            interrupts[idx].Enabled = false;
                            this.Log(LogLevel.Debug, "Interrupt #{0} disabled", idx);
                            UpdateInterrupts(idx);
                        }
                    });

            Registers.DefaultVectorAddress.Define(this)
                .WithValueField(0, 32, out defaultVectorAddress, name: "Default VectorAddr");

            Registers.VectorAddress0.DefineMany(this, 16, (register, idx) =>
            {
                register.WithValueField(0, 32, out vectorAddress[idx], name: "VectorAddr");
            });

            Registers.VectorControl0.DefineMany(this, 16, (register, idx) =>
            {
                register
                    .WithValueField(0, 5, out irqSource[idx], name: "IntSource")
                    .WithFlag(5, out irqSourceEnabled[idx], name: "E")
                    .WithReservedBits(6, 26)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateVectorMapping(idx);
                        UpdateInterrupts(idx);
                    });
            });

            Registers.ActiveInterruptVectorAddress.Define(this)
                .WithValueField(0, 32, out activeVectorAddress, name: "VectorAddr",
                    valueProviderCallback: _ =>
                    {
                        lock(activeInterrupts)
                        {
                            ulong activeVectorAddress = 0;
                            if(activeInterrupts.TryDequeue(out var interrupt, out var __))
                            {
                                servicedInterrupts.Push(interrupt);
                                activeVectorAddress = (interrupt.VectorId != -1)
                                    ? vectorAddress[interrupt.VectorId].Value
                                    : defaultVectorAddress.Value;
                            }
                            RefreshIrqFiqState();
                            return activeVectorAddress;
                        }
                    },
                    writeCallback: (_, __) =>
                    {
                        lock(activeInterrupts)
                        {
                            if(servicedInterrupts.Count == 0)
                            {
                                this.Log(LogLevel.Warning, "Tried to finish a vectored exception, but there is none active");
                                return;
                            }
                            var irqId = FinishCurrentInterrupt().Id;
                            this.Log(LogLevel.Debug, "Finished IRQ #{0}", irqId);
                            RefreshIrqFiqState();
                        }
                    });

            Registers.PeripheralIdentification0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Partnumber0", valueProviderCallback: _ => 0x90)
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification1.Define(this)
                .WithValueField(0, 4, FieldMode.Read, name: "Partnumber1", valueProviderCallback: _ => 0x01)
                .WithValueField(4, 4, FieldMode.Read, name: "Designer0", valueProviderCallback: _ => 0x01)
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification2.Define(this)
                .WithValueField(0, 4, FieldMode.Read, name: "Designer1", valueProviderCallback: _ => 0x00)
                .WithValueField(4, 4, FieldMode.Read, name: "Revision", valueProviderCallback: _ => 0x01)
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification3.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Configuration", valueProviderCallback: _ => 0x00)
                .WithReservedBits(8, 24);

            Registers.SoftwareInterrupt.Define(this)
                .WithFlags(0, 32, FieldMode.Set, name: "SoftInt", writeCallback: (id, _, value) =>
                {
                    if(value)
                    {
                        this.Log(LogLevel.Debug, "Setting soft IRQ {0}", id);
                        interrupts[id].SoftwareState = true;
                        UpdateInterrupts(id);
                    }
                });

            Registers.SoftwareInterruptClear.Define(this)
                .WithFlags(0, 32, FieldMode.Set, name: "SoftIntClear", writeCallback: (id, _, value) =>
                {
                    if(value)
                    {
                        this.Log(LogLevel.Debug, "Clearing soft IRQ {0}", id);
                        interrupts[id].SoftwareState = false;
                        UpdateInterrupts(id);
                    }
                });
        }

        private void UpdateVectorMapping(int id)
        {
            if(irqSourceEnabled[id].Value)
            {
                interrupts[irqSource[id].Value].VectorId = id;
            }
            else
            {
                foreach(var interrupt in interrupts)
                {
                    if(interrupt.VectorId == id)
                    {
                        interrupt.VectorId = -1;
                    }
                }
            }
        }

        private readonly Interrupt[] interrupts;
        private readonly Collections.PriorityQueue<Interrupt, int> activeInterrupts;
        private readonly Stack<Interrupt> servicedInterrupts;

        private IFlagRegisterField[] irqSourceEnabled = new IFlagRegisterField[NumberOfInputLines];
        private IValueRegisterField[] irqSource = new IValueRegisterField[NumberOfInputLines];
        private IValueRegisterField[] vectorAddress = new IValueRegisterField[NumberOfInputLines];
        private IValueRegisterField activeVectorAddress;
        private IValueRegisterField defaultVectorAddress;

        private const int NumberOfInputLines = 32;

        private class Interrupt
        {
            public Interrupt(int id)
            {
                Id = id;
                IsIrq = true;
            }

            public void Reset()
            {
                IsIrq = true;
                Enabled = false;
                PinState = false;
                SoftwareState = false;
                VectorId = -1;
            }

            public int Id { get; }
            public bool IsIrq { get; set; }
            public bool Enabled { get; set; }
            public bool PinState { get; set; }
            public bool SoftwareState { get; set; }
            public int VectorId { get; set; }

            public bool IsActive => Enabled && (PinState || SoftwareState);
            public int Priority => VectorId != -1 ? VectorId : int.MaxValue;
            public bool IsFiq => !IsIrq;
        }

        private enum Registers
        {
            IrqStatus = 0x0,  // VICIRQSTATUS, RO
            FiqStatus = 0x4,  // VICFIQSTATUS, RO
            RawInterruptStatus = 0x8, // VICRAWINTR, RO
            InterruptSelect = 0xC, // VICINTSELECT, R/W
            InterruptEnable = 0x10, // VICINTENABLE, R/W
            InterruptEnableClear = 0x14, // VICINTENCLEAR, W
            SoftwareInterrupt = 0x18, // VICSOFTINT, R/W
            SoftwareInterruptClear = 0x1C, // VICSOFTINTCLEAR, W
            ProtectionEnable = 0x20, // VICPROTECTION, R/W
            ActiveInterruptVectorAddress = 0x30, // VICVECTADDR, R/W
            DefaultVectorAddress = 0x34, // VICDEFVECTADDR, R/W
            VectorAddress0 = 0x100, // VICVECTADDR0, R/W
            VectorAddress1 = 0x104, // VICVECTADDR1, R/W
            VectorAddress2 = 0x108, // VICVECTADDR2, R/W
            VectorAddress3 = 0x10C, // VICVECTADDR3, R/W
            VectorAddress4 = 0x110, // VICVECTADDR4, R/W
            VectorAddress5 = 0x114, // VICVECTADDR5, R/W
            VectorAddress6 = 0x118, // VICVECTADDR6, R/W
            VectorAddress7 = 0x11C, // VICVECTADDR7, R/W
            VectorAddress8 = 0x120, // VICVECTADDR8, R/W
            VectorAddress9 = 0x124, // VICVECTADDR9, R/W
            VectorAddress10 = 0x128, // VICVECTADDR10, R/W
            VectorAddress11 = 0x12C, // VICVECTADDR11, R/W
            VectorAddress12 = 0x130, // VICVECTADDR12, R/W
            VectorAddress13 = 0x134, // VICVECTADDR13, R/W
            VectorAddress14 = 0x138, // VICVECTADDR14, R/W
            VectorAddress15 = 0x13C, // VICVECTADDR15, R/W
            VectorControl0 = 0x200, // VICVECTCNTL0, R/W
            VectorControl1 = 0x204, // VICVECTCNTL1, R/W
            VectorControl2 = 0x208, // VICVECTCNTL2, R/W
            VectorControl3 = 0x20C, // VICVECTCNTL3, R/W
            VectorControl4 = 0x210, // VICVECTCNTL4, R/W
            VectorControl5 = 0x214, // VICVECTCNTL5, R/W
            VectorControl6 = 0x218, // VICVECTCNTL6, R/W
            VectorControl7 = 0x21C, // VICVECTCNTL7, R/W
            VectorControl8 = 0x220, // VICVECTCNTL8, R/W
            VectorControl9 = 0x224, // VICVECTCNTL9, R/W
            VectorControl10 = 0x228, // VICVECTCNTL10, R/W
            VectorControl11 = 0x22C, // VICVECTCNTL11, R/W
            VectorControl12 = 0x230, // VICVECTCNTL12, R/W
            VectorControl13 = 0x234, // VICVECTCNTL13, R/W
            VectorControl14 = 0x238, // VICVECTCNTL14, R/W
            VectorControl15 = 0x23C, // VICVECTCNTL15, R/W
            PeripheralIdentification0 = 0xFE0, // VICPERIPHID0, RO
            PeripheralIdentification1 = 0xFE4, // VICPERIPHID1, RO
            PeripheralIdentification2 = 0xFE8, // VICPERIPHID2, RO
            PeripheralIdentification3 = 0xFEC, // VICPERIPHID3, RO
            PrimeCellIdentification0 = 0xFF0, // VICPCELLID0, RO
            PrimeCellIdentification1 = 0xFF4, // VICPCELLID1, RO
            PrimeCellIdentification2 = 0xFF8, // VICPCELLID2, RO
            PrimeCellIdentification3 = 0xFFC, // VICPCELLID3, RO
        }
    }
}
