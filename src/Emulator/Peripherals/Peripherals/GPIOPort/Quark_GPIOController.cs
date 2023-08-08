//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.X86
{
    public sealed class Quark_GPIOController : BaseGPIOPort, IDoubleWordPeripheral, IGPIOReceiver, IKnownSize
    {
        public Quark_GPIOController(IMachine machine) : base(machine, NumberOfGPIOS)
        {
            internalLock = new object();
            previousState = new bool[NumberOfGPIOS];
            PortDataDirection = new PinDirection[NumberOfGPIOS];
            InterruptEnable = new bool[NumberOfGPIOS];
            InterruptMask = new bool[NumberOfGPIOS];
            interruptType = new InterruptTrigger[NumberOfGPIOS];
            activeInterrupts = new bool[NumberOfGPIOS];
            IRQ = new GPIO();
            PrepareRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(internalLock)
            {
                return registers.Read(offset);
            }
        }

        public override void Reset()
        {
            lock(internalLock)
            {
                base.Reset();
                for(int i = 0; i < NumberOfGPIOS; i++)
                {
                    previousState[i] = false;
                    activeInterrupts[i] = false;
                    PortDataDirection[i] = PinDirection.Input;
                    InterruptEnable[i] = false;
                    InterruptMask[i] = false;
                    interruptType[i] = InterruptTrigger.ActiveLow;
                }
                IRQ.Unset();
                registers.Reset();
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(internalLock)
            {
                registers.Write(offset, value);
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= NumberOfGPIOS)
            {
                throw new ArgumentOutOfRangeException(string.Format("Gpio #{0} called, but only {1} lines are available", number, NumberOfGPIOS));
            }

            lock(internalLock)
            {
                if(PortDataDirection[number] == PinDirection.Output)
                {
                    this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                    return;
                }

                base.OnGPIO(number, value);
                RefreshInterrupts();
            }
        }

        public void SetInterruptType(byte pinId, InterruptTrigger trigger)
        {
            lock(internalLock)
            {
                interruptType[pinId] = trigger;
                switch (trigger)
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
                RefreshInterrupts();
            }
        }

        public GPIO IRQ { get; private set; }
        public PinDirection[] PortDataDirection { get; private set; }
        public bool[] InterruptEnable { get; private set; }
        public IReadOnlyCollection<InterruptTrigger> InterruptType { get { return interruptType; } }
        public bool[] InterruptMask { get; private set; }
        // setting state using this array directly will not raise any interrupts!
        public new bool[] State { get { return base.State; } }
        public long Size { get { return 0x78; } }

        private void PrepareRegisters()
        {
            registers = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.PortAData, new DoubleWordRegister(this)
                                .WithValueField(0, 32, writeCallback: (_, val) =>
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
                    }, valueProviderCallback: _ => { return BitHelper.GetValueFromBitsArray(State); })
                },
                {(long)Registers.PortADataDirection, new DoubleWordRegister(this)
                                .WithValueField(0, 32, writeCallback: (_, val) => Array.Copy(BitHelper.GetBits((uint)val).Select(x => x ? PinDirection.Output : PinDirection.Input).ToArray() , PortDataDirection, 32),
                                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(PortDataDirection.Select(x => x == PinDirection.Output)))
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                                .WithValueField(0, 32, writeCallback: (_, val) => {
                                            Array.Copy(BitHelper.GetBits((uint)val), InterruptEnable, 32);
                                            RefreshInterrupts();
                                        },
                                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(InterruptEnable))
                },
                {(long)Registers.InterruptType, new DoubleWordRegister(this)
                                // true = edge sensitive; false = level sensitive
                                .WithValueField(0, 32, out interruptTypeField, writeCallback: (_, val) => CalculateInterruptTypes())
                },
                {(long)Registers.InterruptPolarity, new DoubleWordRegister(this)
                                // true = rising edge / active high; false = falling edge / active low
                                .WithValueField(0, 32, out interruptPolarityField, writeCallback: (_, val) => CalculateInterruptTypes())
                },
                {(long)Registers.InterruptBothEdgeType, new DoubleWordRegister(this)
                                .WithValueField(0, 32, out interruptBothEdgeField, writeCallback: (_, val) => CalculateInterruptTypes())
                },
                {(long)Registers.InterruptMask, new DoubleWordRegister(this)
                                .WithValueField(0, 32, writeCallback: (_, val) => {
                                        Array.Copy(BitHelper.GetBits((uint)val), InterruptMask, 32);
                                        RefreshInterrupts();
                                    },
                                    valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(InterruptMask))
                },
                {(long)Registers.PortAExternalPort, new DoubleWordRegister(this)
                                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(State))
                },
                {(long)Registers.ClearInterrupt, new DoubleWordRegister(this)
                                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) =>
                                {
                                    foreach(var bit in BitHelper.GetSetBits(val))
                                    {
                                        activeInterrupts[bit] = false;
                                    }
                                    RefreshInterrupts();
                                })
                },
                {(long)Registers.InterruptStatus, new DoubleWordRegister(this)
                                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(activeInterrupts.Zip(InterruptMask, (isActive, isMasked) => isActive && !isMasked)))
                },
                {(long)Registers.RawInterruptStatus, new DoubleWordRegister(this)
                                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => BitHelper.GetValueFromBitsArray(activeInterrupts))
                }
            });
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
                    }
                    else
                    {
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
                }
                RefreshInterrupts();
            }
        }

        private void RefreshInterrupts()
        {
            var irqState = false;
            for(int i = 0; i < NumberOfGPIOS; i++)
            {
                if(!InterruptEnable[i])
                {
                    continue;
                }
                var isEdge = State[i] != previousState[i];
                switch(interruptType[i])
                {
                    case InterruptTrigger.ActiveHigh:
                        irqState |= (State[i] && !InterruptMask[i]);
                        break;
                    case InterruptTrigger.ActiveLow:
                        irqState |= (!State[i] && !InterruptMask[i]);
                        break;
                    case InterruptTrigger.RisingEdge:
                        if(isEdge && State[i])
                        {
                            irqState |= !InterruptMask[i];
                            activeInterrupts[i] = true;
                        }
                    break;
                    case InterruptTrigger.FallingEdge:
                        if(isEdge && !State[i])
                        {
                            irqState |= !InterruptMask[i];
                            activeInterrupts[i] = true;
                        }
                    break;
                    case InterruptTrigger.BothEdges:
                        if(isEdge)
                        {
                            irqState |= !InterruptMask[i];
                            activeInterrupts[i] = true;
                        }
                    break;
                }
            }
            Array.Copy(State, previousState, State.Length);
            if(irqState)
            {
                IRQ.Set();
            }
            else if(!activeInterrupts.Any(x => x))
            {
                IRQ.Unset();
            }
        }

        private DoubleWordRegisterCollection registers;
        private InterruptTrigger[] interruptType;
        private IValueRegisterField interruptPolarityField;
        private IValueRegisterField interruptTypeField;
        private IValueRegisterField interruptBothEdgeField;
        private readonly bool[] activeInterrupts;
        private readonly bool[] previousState;

        private readonly object internalLock;

        private const int NumberOfGPIOS = 32;

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

        internal enum Registers : long
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
