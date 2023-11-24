//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class RenesasRA_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasRA_GPIO(IMachine machine, int numberOfConnections) : base(machine, numberOfConnections)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            PinConfigurationRegistersCollection = new DoubleWordRegisterCollection(this);

            pinDirection = new IEnumRegisterField<Direction>[numberOfConnections];
            usedAsIRQ = new IFlagRegisterField[numberOfConnections];

            IRQ = new GPIO();

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

        [ConnectionRegion("pinConfiguration")]
        public uint ReadDoubleWordFromPinConfiguration(long offset)
        {
            return PinConfigurationRegistersCollection.Read(offset);
        }

        [ConnectionRegion("pinConfiguration")]
        public void WriteDoubleWordToPinConfiguration(long offset, uint value)
        {
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

            if(usedAsIRQ[number].Value)
            {
                IRQ.Set(value);
            }
        }

        public IGPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public DoubleWordRegisterCollection PinConfigurationRegistersCollection { get; }

        public long Size => 0x20;

        private void UpdateIRQOutput()
        {
            var outputValue = false;
            var activeOutputs = new List<int>();

            for(var i = 0; i < NumberOfConnections; ++i)
            {
                if(pinDirection[i].Value != Direction.Input)
                {
                    continue;
                }

                outputValue |= usedAsIRQ[i].Value && State[i];
                if(usedAsIRQ[i].Value)
                {
                    activeOutputs.Add(i);
                }
            }

            IRQ.Set(outputValue);

            if(activeOutputs.Count > 1)
            {
                this.Log(LogLevel.Warning, "More than one pin is used as IRQ output: {0}", string.Join(", ", activeOutputs));
            }
        }

        private void DefineRegisters()
        {
            Registers.DirectionOutput.Define(this)
                .WithEnumFields(0, 1, NumberOfConnections, out pinDirection, name: "PDR",
                    changeCallback: (i, _, value) => { if(value == Direction.Input) UpdateIRQOutput(); })
                .WithFlags(16, NumberOfConnections, name: "PODR",
                    valueProviderCallback: (i, _) => Connections[i].IsSet,
                    writeCallback: (i, _, value) => SetOutput(i, value))
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
                var offset = i * 0x4;
                var register = new DoubleWordRegister(this)
                    .WithFlag(0, name: "PODR",
                        valueProviderCallback: _ => Connections[idx].IsSet,
                        writeCallback: (_, value) => SetOutput(idx, value))
                    .WithFlag(1, FieldMode.Read, name: "PIDR",
                        valueProviderCallback: _ => GetInput(i))
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

        private IEnumRegisterField<Direction>[] pinDirection;
        private IFlagRegisterField[] usedAsIRQ;

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
