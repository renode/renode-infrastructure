//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    abstract public class RenesasRA_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasRA_GPIO(IMachine machine, int portNumber, int numberOfConnections, RenesasRA_GPIOMisc pfsMisc) : base(machine, numberOfConnections)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            PinConfigurationRegistersCollection = new DoubleWordRegisterCollection(this);

            pinDirection = new IEnumRegisterField<Direction>[numberOfConnections];
            usedAsIRQ = new IFlagRegisterField[numberOfConnections];

            this.pfsMisc = pfsMisc;
            this.portNumber = portNumber;

            IRQ0 = new GPIO();
            IRQ1 = new GPIO();
            IRQ2 = new GPIO();
            IRQ3 = new GPIO();
            IRQ4 = new GPIO();
            IRQ5 = new GPIO();
            IRQ6 = new GPIO();
            IRQ7 = new GPIO();
            IRQ8 = new GPIO();
            IRQ9 = new GPIO();
            IRQ10 = new GPIO();
            IRQ11 = new GPIO();
            IRQ12 = new GPIO();
            IRQ13 = new GPIO();
            IRQ14 = new GPIO();
            IRQ15 = new GPIO();

            DefineRegisters();
            DefinePinConfigurationRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();

            IRQ0.Unset();
            IRQ1.Unset();
            IRQ2.Unset();
            IRQ3.Unset();
            IRQ4.Unset();
            IRQ5.Unset();
            IRQ6.Unset();
            IRQ7.Unset();
            IRQ8.Unset();
            IRQ9.Unset();
            IRQ10.Unset();
            IRQ11.Unset();
            IRQ12.Unset();
            IRQ13.Unset();
            IRQ14.Unset();
            IRQ15.Unset();
        }

        [ConnectionRegion("pinConfiguration")]
        public uint ReadDoubleWordFromPinConfiguration(long offset)
        {
            return PinConfigurationRegistersCollection.Read(offset);
        }

        [ConnectionRegion("pinConfiguration")]
        public void WriteDoubleWordToPinConfiguration(long offset, uint value)
        {
            if(!pfsMisc.PFSWriteEnabled)
            {
                this.Log(LogLevel.Warning, "Trying to write to pin configuration registers (PFS) when PFSWE is deasserted");
                return;
            }

            PinConfigurationRegistersCollection.Write(offset, value);
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            base.OnGPIO(number, value);

            if(pinDirection[number].Value != Direction.Input)
            {
                this.Log(LogLevel.Warning, "Writing to an output GPIO pin #{0}", number);
                return;
            }

            if(TryGetInterruptOutput(number, out var irq))
            {
                irq.Set(value);
            }
        }

        public GPIO IRQ0 { get; }
        public GPIO IRQ1 { get; }
        public GPIO IRQ2 { get; }
        public GPIO IRQ3 { get; }
        public GPIO IRQ4 { get; }
        public GPIO IRQ5 { get; }
        public GPIO IRQ6 { get; }
        public GPIO IRQ7 { get; }
        public GPIO IRQ8 { get; }
        public GPIO IRQ9 { get; }
        public GPIO IRQ10 { get; }
        public GPIO IRQ11 { get; }
        public GPIO IRQ12 { get; }
        public GPIO IRQ13 { get; }
        public GPIO IRQ14 { get; }
        public GPIO IRQ15 { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public DoubleWordRegisterCollection PinConfigurationRegistersCollection { get; }

        public long Size => 0x20;

        private void UpdateIRQOutput()
        {
            for(var i = 0; i < NumberOfConnections; ++i)
            {
                if(pinDirection[i].Value != Direction.Input)
                {
                    continue;
                }

                if(TryGetInterruptOutput(i, out var irq) && irq.IsSet != State[i])
                {
                    irq.Set(State[i]);
                }
            }
        }

        private void DefineRegisters()
        {
            Registers.DirectionOutput.Define(this)
                .WithEnumFields(0, 1, NumberOfConnections, out pinDirection, name: "PDR",
                    changeCallback: (i, _, value) => { if(value == Direction.Input) UpdateIRQOutput(); })
                .WithFlags(16, NumberOfConnections, name: "PODR",
                    valueProviderCallback: (i, _) => Connections[i].IsSet,
                    changeCallback: (i, _, value) => SetOutput(i, value))
            ;

            Registers.StateEventInput.Define(this)
                .WithFlags(0, NumberOfConnections, FieldMode.Read, name: "PIDR",
                    valueProviderCallback: (i, _) => GetInput(i))
                .WithTag("EIDR", 16, NumberOfConnections)
            ;

            Registers.Output.Define(this)
                .WithFlags(0, NumberOfConnections, FieldMode.Write, name: "POSR",
                    writeCallback: (i, _, value) => { if(value) SetOutput(i, true); })
                .WithFlags(16, NumberOfConnections, FieldMode.Write, name: "PORR",
                    writeCallback: (i, _, value) => { if(value) SetOutput(i, false); })
            ;
        }

        private void DefinePinConfigurationRegisters()
        {
            for(var i = 0; i < NumberOfConnections; ++i)
            {
                var idx = i;
                var offset = idx * 0x4;
                var register = new DoubleWordRegister(this)
                    .WithFlag(0, name: "PODR",
                        valueProviderCallback: _ => Connections[idx].IsSet,
                        changeCallback: (_, value) => SetOutput(idx, value))
                    .WithFlag(1, FieldMode.Read, name: "PIDR",
                        valueProviderCallback: _ => GetInput(idx))
                    .WithEnumField(2, 1, out pinDirection[idx], name: "PDR",
                        changeCallback: (_, value) => { if(value == Direction.Input) UpdateIRQOutput(); })
                    .WithReservedBits(3, 1)
                    .WithFlag(4, name: "PCR")
                    .WithReservedBits(5, 1)
                    .WithFlag(6, name: "NCODR")
                    .WithReservedBits(7, 3)
                    .WithTag("DSCR", 10, 2)
                    .WithTag("EOFR", 12, 2)
                    .WithFlag(14, out usedAsIRQ[idx], name: "ISEL",
                        changeCallback: (_, value) => UpdateIRQOutput())
                    .WithTaggedFlag("ASEL", 15)
                    .WithTaggedFlag("PMR", 16)
                    .WithReservedBits(17, 7)
                    .WithTag("PSEL", 24, 5)
                    .WithReservedBits(29, 3)
                ;
                PinConfigurationRegistersCollection.AddRegister(offset, register);
            }
        }

        private bool GetInput(int index)
        {
            var value = State[index];
            if(pinDirection[index].Value == Direction.Output)
            {
                value |= Connections[index].IsSet;
            }
            return value;
        }

        private void SetOutput(int index, bool value)
        {
            if(pinDirection[index].Value != Direction.Output)
            {
                this.Log(LogLevel.Warning, "Trying to set pin level, but pin is not in output mode, ignoring");
                return;
            }

            Connections[index].Set(value);
        }

        private bool TryGetInterruptOutput(int number, out GPIO irq)
        {
            irq = null;
            if(!usedAsIRQ[number].Value)
            {
                return false;
            }

            var interruptOutput = PinInterruptOutputs[portNumber].SingleOrDefault(e => e.PinNumber == number);
            if(interruptOutput == null)
            {
                this.Log(LogLevel.Warning, "Trying to use pin#{0} as interrupt, but it's not associated with any IRQn output", number);
                return false;
            }

            irq = interruptOutput.IRQ;
            return true;
        }

        abstract protected List<InterruptOutput>[] PinInterruptOutputs { get; }

        private readonly RenesasRA_GPIOMisc pfsMisc;
        private readonly int portNumber;

        private IEnumRegisterField<Direction>[] pinDirection;
        private IFlagRegisterField[] usedAsIRQ;

        protected class InterruptOutput
        {
            public InterruptOutput(int pinNumber, GPIO irq)
            {
                PinNumber = pinNumber;
                IRQ = irq;
            }

            public int PinNumber { get; }
            public GPIO IRQ { get; }
        }

        private enum Direction
        {
            Input,
            Output,
        }

        private enum Registers
        {
            DirectionOutput = 0x00,
            StateEventInput = 0x04,
            Output = 0x08,
            EventOutput = 0x0C,
        }
    }
}
