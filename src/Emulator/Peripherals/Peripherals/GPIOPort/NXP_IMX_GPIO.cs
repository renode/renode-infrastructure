//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class NXP_IMX_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public NXP_IMX_GPIO(IMachine machine) : base(machine, numberOfConnections: 32)
        {
            gpioLines = new GPIOLine[NumberOfConnections];
            for(var index = 0; index < NumberOfConnections; index++)
            {
                var i = index;
                gpioLines[i] = new GPIOLine(this, i);
                gpioLines[i].StateChangeAction = value =>
                {
                    this.SetConnectionStateBit(i, value);
                };
            }

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            IRQ0 = new GPIO();
            IRQ1 = new GPIO();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ0.Unset();
            IRQ1.Unset();
            RegistersCollection.Reset();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            gpioLines[number].LineState = value;
            this.DebugLog("IO {0}: State driven to {1}", number, value);
            UpdateInterrupts();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public long Size => 0x10000;

        public GPIO IRQ0 { get; }

        public GPIO IRQ1 { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Data.Define(this)
                .WithFlags(0, NumberOfConnections, name: "DR",
                    valueProviderCallback: (i, _) => gpioLines[i].Data,
                    writeCallback: (i, _, value) => { gpioLines[i].Data = value; });

            Registers.Direction.Define(this)
                .WithFlags(0, NumberOfConnections, name: "GDIR",
                    valueProviderCallback: (i, _) => gpioLines[i].IsOutput,
                    writeCallback: (i, _, value) => { gpioLines[i].IsOutput = value; })
                .WithChangeCallback((_, _) => UpdateInterrupts());

            Registers.PadStatus.Define(this)
                .WithFlags(0, NumberOfConnections, FieldMode.Read, name: "PSR",
                    valueProviderCallback: (i, _) => gpioLines[i].PadStatus);

            Registers.Configuration1.Define(this)
                .WithValueFields(0, 2, 16, name: "ICR",
                    valueProviderCallback: (i, _) => (uint)gpioLines[i].IntType,
                    writeCallback: (i, _, value) => { gpioLines[i].IntType = (GPIOLine.InterruptType)value; })
                .WithChangeCallback((_, _) => UpdateInterrupts());

            Registers.Configuration2.Define(this)
                .WithValueFields(0, 2, 16, name: "ICR2",
                    valueProviderCallback: (i, _) => (uint)gpioLines[i + 16].IntType,
                    writeCallback: (i, _, value) => { gpioLines[i + 16].IntType = (GPIOLine.InterruptType)value; })
                .WithChangeCallback((_, _) => UpdateInterrupts());

            Registers.InterruptMask.Define(this)
                .WithFlags(0, NumberOfConnections, name: "IMR",
                    valueProviderCallback: (i, _) => gpioLines[i].InterruptEnabled,
                    writeCallback: (i, _, value) => { gpioLines[i].InterruptEnabled = value; })
                .WithChangeCallback((_, _) => UpdateInterrupts());

            Registers.InterruptStatus.Define(this)
                .WithFlags(0, NumberOfConnections, name: "ISR",
                   valueProviderCallback: (i, _) => gpioLines[i].InterruptFlag,
                   writeCallback: (i, _, value) => { if(value) gpioLines[i].InterruptFlag = false; })
                .WithChangeCallback((_, _) => UpdateInterrupts());

            Registers.EdgeSelect.Define(this)
                .WithFlags(0, NumberOfConnections, name: "GPIO_EDGE_SEL",
                    valueProviderCallback: (i, _) => gpioLines[i].EdgeSelectOverride,
                    writeCallback: (i, _, value) => { gpioLines[i].EdgeSelectOverride = value; })
                .WithChangeCallback((_, _) => UpdateInterrupts());
        }

        private void UpdateInterrupts()
        {
            var irq0 = gpioLines.Take(16).Any(x => x.InterruptFlag);
            var irq1 = gpioLines.Skip(16).Take(16).Any(x => x.InterruptFlag);
            if(irq0 || irq1)
            {
                this.DebugLog("Setting IRQs to [{0}, {1}]", irq0, irq1);
            }
            IRQ0.Set(irq0);
            IRQ1.Set(irq1);
        }

        private readonly GPIOLine[] gpioLines;

        private class GPIOLine
        {
            public GPIOLine(BaseGPIOPort parent, int index)
            {
                this.parent = parent;
                this.index = index;
            }

            public bool Data
            {
                get
                {
                    return this.LineState;
                }

                set
                {
                    if(IsOutput)
                    {
                        this.LineState = value;
                        parent.DebugLog("IO {0}: Changing state to {1}", index, value);
                    }
                    else
                    {
                        if(value)
                        {
                            parent.WarningLog("IO {0}: Trying to set DR bit state to high while the pin is configured as input.This will be ignored", index);
                        }
                    }
                }
            }

            public bool LineState
            {
                get
                {
                    return this.state;
                }

                set
                {
                    var stateChange = value != state;
                    if(stateChange)
                    {
                        this.lastState = this.state;
                        this.state = value;
                        StateChangeAction?.Invoke(value);
                    }
                }
            }

            public bool InterruptFlag
            {
                get
                {
                    CheckInterruptConditions();
                    return interruptFlag;
                }

                set
                {
                    if(value == false)
                    {
                        interruptStateAcknowledged = true;
                        interruptFlag = false;
                    }
                }
            }

            public bool PadStatus
            {
                get
                {
                    return !IsOutput && LineState;
                }
            }

            public bool InterruptEnabled;
            public bool EdgeSelectOverride;
            public bool IsOutput;
            public InterruptType IntType;
            public Action<bool> StateChangeAction = null;

            private void CheckInterruptConditions()
            {
                // Assert state won't be changed until the interrupt is acknowledged
                if(interruptFlag && !interruptStateAcknowledged)
                {
                    return;
                }

                if(!InterruptEnabled || IsOutput)
                {
                    interruptFlag = false;
                    return;
                }

                if(EdgeSelectOverride)
                {
                    interruptFlag = lastState.HasValue && state != lastState;
                    parent.NoisyLog("IO {0}:Current interrupt mode is edgeOverride", index);
                }
                else
                {
                    switch(IntType)
                    {
                    case InterruptType.HighLevel:
                        interruptFlag = state;
                        break;
                    case InterruptType.LowLevel:
                        interruptFlag = !state;
                        break;
                    case InterruptType.RisingEdge:
                        interruptFlag = lastState.HasValue && state && !lastState.Value;
                        break;
                    case InterruptType.FallingEdge:
                        interruptFlag = lastState.HasValue && !state && lastState.Value;
                        break;
                    default:
                        throw new System.NotImplementedException();
                    }
                    parent.NoisyLog("IO {0}: Current interrupt mode is {1}", index, IntType);
                }

                if(interruptFlag)
                {
                    this.interruptStateAcknowledged = false;
                    parent.DebugLog("IO {0}: Interrupt set", index);
                }

                lastState = null;
                interruptStateAcknowledged = false;
            }

            private bool state;
            private bool? lastState;
            private bool interruptStateAcknowledged;
            private bool interruptFlag;
            private readonly BaseGPIOPort parent;
            private readonly int index;

            public enum InterruptType
            {
                LowLevel = 0b00,
                HighLevel = 0b01,
                RisingEdge = 0b10,
                FallingEdge = 0b11,
            }
        }

        private enum Registers
        {
            Data = 0x0,
            Direction = 0x4,
            PadStatus = 0x8,
            Configuration1 = 0xC,
            Configuration2 = 0x10,
            InterruptMask = 0x14,
            InterruptStatus = 0x18,
            EdgeSelect = 0x1C,
        }
    }
}
