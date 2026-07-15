//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public sealed class DesignWare_APB_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>,
        INumberedGPIOOutput, IGPIOReceiver, IKnownSize
    {
        public DesignWare_APB_GPIO(IMachine machine, int numberOfGPIOS = 32) : base(machine, numberOfGPIOS)
        {
            if(numberOfGPIOS > 32)
            {
                throw new ConstructionException($"`{nameof(numberOfGPIOS)}` cannnot be greater than 32");
            }
            this.numberOfGPIOS = numberOfGPIOS;
            internalLock = new object();
            previousState = new bool[numberOfGPIOS];
            PortDataDirection = new PinDirection[numberOfGPIOS];
            InterruptEnable = new bool[numberOfGPIOS];
            InterruptMask = new bool[numberOfGPIOS];
            interruptType = new InterruptTrigger[numberOfGPIOS];
            activeInterrupts = new bool[numberOfGPIOS];
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(internalLock)
            {
                return RegistersCollection.Read(offset);
            }
        }

        public override void Reset()
        {
            lock(internalLock)
            {
                base.Reset();
                for(int i = 0; i < numberOfGPIOS; i++)
                {
                    previousState[i] = false;
                    activeInterrupts[i] = false;
                    PortDataDirection[i] = PinDirection.Input;
                    InterruptEnable[i] = false;
                    InterruptMask[i] = false;
                    interruptType[i] = InterruptTrigger.ActiveLow;
                }
                IRQ.Unset();
                foreach(var irq in IRQs)
                {
                    irq.Set(false);
                }
                RegistersCollection.Reset();
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(internalLock)
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= numberOfGPIOS)
            {
                throw new ArgumentOutOfRangeException(string.Format("Gpio #{0} called, but only {1} lines are available", number, numberOfGPIOS));
            }

            lock(internalLock)
            {
                if(PortDataDirection[number] == PinDirection.Output)
                {
                    this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                    return;
                }

                base.OnGPIO(number, value);
                UpdateInterrupts();
            }
        }

        public void SetInterruptType(byte pinId, InterruptTrigger trigger)
        {
            lock(internalLock)
            {
                interruptType[pinId] = trigger;
                switch(trigger)
                {
                case InterruptTrigger.BothEdges:
                    interruptBothEdgeField.SetBit(pinId, true);
                    // interruptType and interruptPolarity are not considered when this bit is set
                    break;
                case InterruptTrigger.RisingEdge:
                    interruptBothEdgeField.SetBit(pinId, false);
                    interruptTypeField.SetBit(pinId, true);
                    interruptPolarityField.SetBit(pinId, true);
                    break;
                case InterruptTrigger.FallingEdge:
                    interruptBothEdgeField.SetBit(pinId, false);
                    interruptTypeField.SetBit(pinId, true);
                    interruptPolarityField.SetBit(pinId, false);
                    break;
                case InterruptTrigger.ActiveHigh:
                    interruptBothEdgeField.SetBit(pinId, false);
                    interruptTypeField.SetBit(pinId, false);
                    interruptPolarityField.SetBit(pinId, true);
                    break;
                case InterruptTrigger.ActiveLow:
                    interruptBothEdgeField.SetBit(pinId, false);
                    interruptTypeField.SetBit(pinId, false);
                    interruptPolarityField.SetBit(pinId, false);
                    break;
                }
                UpdateInterrupts();
            }
        }

        [DefaultInterrupt]
        public GPIO IRQ { get; } = new GPIO();

        public GPIO IRQ0 { get; } = new GPIO();

        public GPIO IRQ1 { get; } = new GPIO();

        public GPIO IRQ2 { get; } = new GPIO();

        public GPIO IRQ3 { get; } = new GPIO();

        public GPIO IRQ4 { get; } = new GPIO();

        public GPIO IRQ5 { get; } = new GPIO();

        public GPIO IRQ6 { get; } = new GPIO();

        public GPIO IRQ7 { get; } = new GPIO();

        public GPIO IRQ8 { get; } = new GPIO();

        public GPIO IRQ9 { get; } = new GPIO();

        public GPIO IRQ10 { get; } = new GPIO();

        public GPIO IRQ11 { get; } = new GPIO();

        public GPIO IRQ12 { get; } = new GPIO();

        public GPIO IRQ13 { get; } = new GPIO();

        public GPIO IRQ14 { get; } = new GPIO();

        public GPIO IRQ15 { get; } = new GPIO();

        public GPIO IRQ16 { get; } = new GPIO();

        public GPIO IRQ17 { get; } = new GPIO();

        public GPIO IRQ18 { get; } = new GPIO();

        public GPIO IRQ19 { get; } = new GPIO();

        public GPIO IRQ20 { get; } = new GPIO();

        public GPIO IRQ21 { get; } = new GPIO();

        public GPIO IRQ22 { get; } = new GPIO();

        public GPIO IRQ23 { get; } = new GPIO();

        public GPIO IRQ24 { get; } = new GPIO();

        public GPIO IRQ25 { get; } = new GPIO();

        public GPIO IRQ26 { get; } = new GPIO();

        public GPIO IRQ27 { get; } = new GPIO();

        public GPIO IRQ28 { get; } = new GPIO();

        public GPIO IRQ29 { get; } = new GPIO();

        public GPIO IRQ30 { get; } = new GPIO();

        public GPIO IRQ31 { get; } = new GPIO();

        public GPIO[] IRQs => new GPIO[]
        {
            IRQ0, IRQ1, IRQ2, IRQ3, IRQ4, IRQ5, IRQ6, IRQ7,
            IRQ8, IRQ9, IRQ10, IRQ11, IRQ12, IRQ13, IRQ14, IRQ15,
            IRQ16, IRQ17, IRQ18, IRQ19, IRQ20, IRQ21, IRQ22, IRQ23,
            IRQ24, IRQ25, IRQ26, IRQ27, IRQ28, IRQ29, IRQ30, IRQ31
        };

        public PinDirection[] PortDataDirection { get; }

        public bool[] InterruptEnable { get; }

        public IReadOnlyCollection<InterruptTrigger> InterruptType => interruptType;

        public bool[] InterruptMask { get; }

        // setting state using this array directly will not raise any interrupts!
        public new bool[] State => base.State;

        public long Size => 0x78;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.PortAData.Define(this)
                .WithValueField(0, numberOfGPIOS, name: "SWPORTA_DR",
                    writeCallback: (_, val) =>
                    {
                        var bits = BitHelper.GetBits((uint)val);
                        for(int i = 0; i < bits.Length; i++)
                        {
                            if(PortDataDirection[i] == PinDirection.Output)
                            {
                                Connections[i].Set(bits[i]);
                                State[i] = bits[i];
                            }
                        }
                        UpdateInterrupts();
                    },
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State)
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.PortADataDirection.Define(this)
                .WithValueField(0, numberOfGPIOS, name: "SWPORTA_DDR",
                    writeCallback: (_, val) =>
                    {
                        var newDataDirection = BitHelper.GetBits((uint)val).Select(x => x ? PinDirection.Output : PinDirection.Input).ToArray();
                        Array.Copy(newDataDirection, PortDataDirection, numberOfGPIOS);
                    },
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(PortDataDirection.Select(x => x == PinDirection.Output))
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.PortADataSource.Define(this)
                .WithTaggedFlag("SWPORTA_CTL", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.InterruptEnable.Define(this)
                .WithValueField(0, numberOfGPIOS, name: "INTEN",
                    writeCallback: (_, val) =>
                    {
                        Array.Copy(BitHelper.GetBits((uint)val), InterruptEnable, numberOfGPIOS);
                        UpdateInterrupts();
                    },
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(InterruptEnable)
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.InterruptMask.Define(this)
                .WithValueField(0, numberOfGPIOS, name: "INTMASK",
                    writeCallback: (_, val) =>
                    {
                        Array.Copy(BitHelper.GetBits((uint)val), InterruptMask, numberOfGPIOS);
                        UpdateInterrupts();
                    },
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(InterruptMask)
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.InterruptType.Define(this)
                // true = edge sensitive; false = level sensitive
                .WithValueField(0, numberOfGPIOS, out interruptTypeField, name: "INTYPE_LEVEL",
                    writeCallback: (_, val) => CalculateInterruptTypes()
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.InterruptPolarity.Define(this)
                // true = rising edge / active high; false = falling edge / active low
                .WithValueField(0, numberOfGPIOS, out interruptPolarityField, name: "INT_POLARITY",
                    writeCallback: (_, val) => CalculateInterruptTypes()
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.InterruptStatus.Define(this)
                .WithValueField(0, numberOfGPIOS, FieldMode.Read, name: "INTSTATUS",
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(activeInterrupts.Zip(InterruptMask, (isActive, isMasked) => isActive && !isMasked))
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.RawInterruptStatus.Define(this)
                .WithValueField(0, numberOfGPIOS, FieldMode.Read, name: "RAW_INTSTATUS",
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(activeInterrupts)
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.DebounceEnable.Define(this)
                .WithValueField(0, numberOfGPIOS, name: "DEBOUNCE")
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.ClearInterrupt.Define(this)
                .WithValueField(0, numberOfGPIOS, FieldMode.Write, name: "PORTA_EOI",
                    writeCallback: (_, val) =>
                    {
                        foreach(var bit in BitHelper.GetSetBits(val))
                        {
                            activeInterrupts[bit] = false;
                        }
                        UpdateInterrupts();
                    }
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.PortAExternalPort.Define(this)
                .WithValueField(0, numberOfGPIOS, FieldMode.Read, name: "EXT_PORTA",
                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State)
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;

            Registers.SynchronizationLevel.Define(this)
                .WithTaggedFlag("LS_SYNC", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.InterruptBothEdgeType.Define(this)
                .WithValueField(0, numberOfGPIOS, out interruptBothEdgeField, name: "PWIDTH_A",
                    writeCallback: (_, val) => CalculateInterruptTypes()
                )
                .WithReservedBits(numberOfGPIOS, 32 - numberOfGPIOS)
            ;
        }

        private void CalculateInterruptTypes()
        {
            lock(internalLock)
            {
                var isBothEdgesSensitive = BitHelper.GetBits((uint)interruptBothEdgeField.Value);
                var isEdgeSensitive = BitHelper.GetBits((uint)interruptTypeField.Value);
                var isActiveHighOrRisingEdge = BitHelper.GetBits((uint)interruptPolarityField.Value);
                for(int i = 0; i < interruptType.Length; i++)
                {
                    if(isBothEdgesSensitive[i])
                    {
                        interruptType[i] = InterruptTrigger.BothEdges;
                        continue;
                    }

                    if(isEdgeSensitive[i])
                    {
                        interruptType[i] = isActiveHighOrRisingEdge[i]
                            ? InterruptTrigger.RisingEdge
                            : InterruptTrigger.FallingEdge;
                    }
                    else
                    {
                        interruptType[i] = isActiveHighOrRisingEdge[i]
                            ? InterruptTrigger.ActiveHigh
                            : InterruptTrigger.ActiveLow;
                    }
                }
                UpdateInterrupts();
            }
        }

        private bool IsInterruptTriggered(int i)
        {
            var isEdge = State[i] != previousState[i];
            switch(interruptType[i])
            {
            case InterruptTrigger.ActiveHigh:
                return State[i];
            case InterruptTrigger.ActiveLow:
                return !State[i];
            case InterruptTrigger.RisingEdge:
                return isEdge && State[i];
            case InterruptTrigger.FallingEdge:
                return isEdge && !State[i];
            case InterruptTrigger.BothEdges:
                return isEdge;
            default:
                throw new UnreachableException();
            }
        }

        private void UpdateInterrupts()
        {
            var wasIrqSet = false;
            var anyIrqSet = false;
            var irqs = IRQs;

            for(int i = 0; i < numberOfGPIOS; i++)
            {
                if(!InterruptEnable[i] || PortDataDirection[i] == PinDirection.Output)
                {
                    continue;
                }
                activeInterrupts[i] |= IsInterruptTriggered(i);
                var irqSet = activeInterrupts[i] && !InterruptMask[i];
                anyIrqSet |= irqSet;

                wasIrqSet = irqs[i].IsSet;
                irqs[i].Set(irqSet);
                if(wasIrqSet != irqSet)
                {
                    this.NoisyLog("IRQ{0}: {1}set", i, wasIrqSet ? "un" : "");
                }
            }

            Array.Copy(State, previousState, State.Length);

            wasIrqSet = IRQ.IsSet;
            IRQ.Set(anyIrqSet);
            if(wasIrqSet != anyIrqSet)
            {
                this.NoisyLog("IRQ: {0}set", wasIrqSet ? "un" : "");
            }
        }

        private IValueRegisterField interruptPolarityField;
        private IValueRegisterField interruptTypeField;
        private IValueRegisterField interruptBothEdgeField;

        private readonly InterruptTrigger[] interruptType;
        private readonly bool[] activeInterrupts;
        private readonly bool[] previousState;

        private readonly object internalLock;
        private readonly int numberOfGPIOS;

        public enum PinDirection
        {
            Input,
            Output
        }

        public enum InterruptTrigger
        {
            ActiveLow,
            ActiveHigh,
            FallingEdge,
            RisingEdge,
            BothEdges
        }

        public enum Registers
        {
            PortAData = 0x0,
            PortADataDirection = 0x4,
            PortADataSource = 0x8,
            InterruptEnable = 0x30,
            InterruptMask = 0x34,
            InterruptType = 0x38,
            InterruptPolarity = 0x3C,
            InterruptStatus = 0x40,
            RawInterruptStatus = 0x44,
            DebounceEnable = 0x48,
            ClearInterrupt = 0x4C,
            PortAExternalPort = 0x50,
            SynchronizationLevel = 0x60,
            InterruptBothEdgeType = 0x68
        }
    }
}
