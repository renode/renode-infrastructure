//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class RenesasDA14_GPIO : BaseGPIOPort, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasDA14_GPIO(IMachine machine) : base(machine, NumberOfPorts * PinsPerPort)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            ports = new Port[NumberOfPorts];
            for(var portNumber = 0; portNumber < ports.Length; ++portNumber)
            {
                ports[portNumber] = new Port(portNumber, this);
            }

            DefineCommonRegisters();

            Reset();
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
            RegistersCollection.Reset();
        }

        private void DefineCommonRegisters()
        {
            Registers.ClockSel.Define(this)
                .WithTag("FUNC_CLOCK_SEL", 0, 3)
                .WithTaggedFlag("FUNC_CLOCK_EN", 3)
                .WithReservedBits(4, 3)
                .WithTaggedFlag("XTAL32M_OUTPUT_EN", 8)
                .WithTaggedFlag("RC32M_OUTPUT_EN", 9)
                .WithTaggedFlag("DIVN_OUTPUT_EN", 10);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x200;

        private readonly Port[] ports;

        private const int NumberOfPorts = 2;
        private const int PinsPerPort = 16;

        private class Port
        {
            public Port(int portNumber, RenesasDA14_GPIO parent)
            {
                this.portNumber = portNumber;
                this.parent = parent;

                parent.RegistersCollection.DefineRegister((long)Registers.DataPort0 + (4 * portNumber), 0x142)
                    .WithValueField(0, 16, name: $"P{portNumber}_DATA",
                        valueProviderCallback: _ => GetStateValue(),
                        writeCallback: (_, value) => SetStateValue(value));
                parent.RegistersCollection.DefineRegister((long)Registers.SetDataPort0 + (4 * portNumber))
                    .WithValueField(0, 16, name: $"P{portNumber}_SET",
                        valueProviderCallback: _ => 0,
                        writeCallback: (_, value) => SetStateValue(GetStateValue() | value));
                parent.RegistersCollection.DefineRegister((long)Registers.ResetDataPort0 + (4 * portNumber))
                    .WithValueField(0, 16, name: $"P{portNumber}_RESET",
                        valueProviderCallback: _ => 0,
                        writeCallback: (_, value) => SetStateValue(GetStateValue() | ~value));
                parent.RegistersCollection.DefineRegister((long)Registers.WeakControlPort0 + (4 * portNumber))
                    .WithValueField(0, 16, out strength, name: $"P{portNumber}_LOWDRV");

                direction = new IEnumRegisterField<Directions>[PinsPerPort];

                for(var pin = 0; pin < PinsPerPort; ++pin)
                {
                    parent.RegistersCollection.DefineRegister((long)Registers.ModePort0Pin00 + (4 * pin) + (4 * PinsPerPort * portNumber), 0x200)
                            .WithTag("PID", 0, 6) // pin function
                            .WithReservedBits(6, 2)
                            .WithEnumField<DoubleWordRegister, Directions>(8, 2, out direction[pin], name: "PUPD",
                                writeCallback: (_, __) => RefreshConnectionsState())
                            .WithTaggedFlag("PPOD", 10);
                }
            }

            private bool IsOutputPin(int pinNumber)
            {
                return direction[pinNumber].Value == Directions.Output;
            }

            private ulong GetStateValue()
            {
                ulong result = 0;

                for(byte bitIndex = 0; bitIndex < PinsPerPort; bitIndex++)
                {
                    var idx = PinsPerPort * portNumber + bitIndex;

                    BitHelper.SetBit(ref result, bitIndex, IsOutputPin(bitIndex)
                        ? parent.Connections[idx].IsSet
                        : parent.State[idx]);
                }

                return result;
            }

            private void SetStateValue(ulong value)
            {
                state = value;
                RefreshConnectionsState();
            }

            private void RefreshConnectionsState()
            {
                for(byte bitIndex = 0; bitIndex < PinsPerPort; bitIndex++)
                {
                    if(IsOutputPin(bitIndex))
                    {
                        var connection = parent.Connections[PinsPerPort * portNumber + bitIndex];
                        var pinState = BitHelper.IsBitSet(state, bitIndex);

                        connection.Set(pinState);
                    }
                }
            }

            private ulong state;

            private readonly IEnumRegisterField<Directions>[] direction;
            private readonly IValueRegisterField strength;

            private readonly int portNumber;
            private readonly RenesasDA14_GPIO parent;

            private enum Directions
            {
                InputNoResistors    = 0b00,
                InputPullUp         = 0b01,
                InputPullDown       = 0b10,
                Output              = 0b11
            }
        }

        private enum Registers
        {
            DataPort0 = 0x0,
            DataPort1 = 0x4,
            SetDataPort0 = 0x8,
            SetDataPort1 = 0xC,
            ResetDataPort0 = 0x10,
            ResetDataPort1 = 0x14,

            ModePort0Pin00 = 0x18,
            ModePort0Pin01 = 0x1C,
            ModePort0Pin02 = 0x20,
            ModePort0Pin03 = 0x24,
            ModePort0Pin04 = 0x28,
            ModePort0Pin05 = 0x2C,
            ModePort0Pin06 = 0x30,
            ModePort0Pin07 = 0x34,
            ModePort0Pin08 = 0x38,
            ModePort0Pin09 = 0x3c,
            ModePort0Pin10 = 0x40,
            ModePort0Pin11 = 0x44,
            ModePort0Pin12 = 0x48,
            ModePort0Pin13 = 0x4c,
            ModePort0Pin14 = 0x50,
            ModePort0Pin15 = 0x54,

            ModePort1Pin00 = 0x58,
            ModePort1Pin01 = 0x5c,
            ModePort1Pin02 = 0x60,
            ModePort1Pin03 = 0x64,
            ModePort1Pin04 = 0x68,
            ModePort1Pin05 = 0x6c,
            ModePort1Pin06 = 0x70,
            ModePort1Pin07 = 0x74,
            ModePort1Pin08 = 0x78,
            ModePort1Pin09 = 0x7c,
            ModePort1Pin10 = 0x80,
            ModePort1Pin11 = 0x84,
            ModePort1Pin12 = 0x88,
            ModePort1Pin13 = 0x8c,
            ModePort1Pin14 = 0x90,
            ModePort1Pin15 = 0x94,

            ClockSel = 0xA0,
            WeakControlPort0 = 0xA4,
            WeakControlPort1 = 0xA8,
        }
    }
}
