//
// Copyright (c) 2020 LabMICRO FACET UNT
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class LPC43xx_GPIO : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public LPC43xx_GPIO(IMachine machine) : base(machine, PinsPerPort * NumberOfPorts)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            ports = new Port[NumberOfPorts];
            for(var portNumber = 0; portNumber < ports.Length; portNumber++)
            {
                ports[portNumber] = new Port(portNumber, this);
            }

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

        public long Size => 0x2380;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private readonly Port[] ports;

        private const int NumberOfPorts = 8;
        private const int PinsPerPort = 32;

        private class Port
        {
            public Port(int portNumber, LPC43xx_GPIO parent)
            {
                this.portNumber = portNumber;
                this.parent = parent;

                parent.RegistersCollection.DefineRegister((uint)Registers.Direction + 4 * portNumber)
                    .WithValueField(0, PinsPerPort, out direction, name: $"GPIO_DIR{portNumber}",
                        writeCallback: (_, value) => RefreshConnectionsState());

                parent.RegistersCollection.DefineRegister((uint)Registers.Mask + 4 * portNumber)
                    .WithValueField(0, PinsPerPort, out mask, name: $"GPIO_MASK{portNumber}");

                parent.RegistersCollection.DefineRegister((uint)Registers.Pin + 4 * portNumber)
                    .WithValueField(0, PinsPerPort, out state, name: $"GPIO_PIN{portNumber}",
                        writeCallback: (_, value) => RefreshConnectionsState(),
                        valueProviderCallback: _ => GetStateValue());

                parent.RegistersCollection.DefineRegister((uint)Registers.MaskedPin + 4 * portNumber)
                    .WithValueField(0, PinsPerPort, name: $"GPIO_MPIN{portNumber}",
                        writeCallback: (_, value) => SetStateValue((uint)(state.Value & mask.Value | value & ~mask.Value)),
                        valueProviderCallback: _ => GetStateValue() & ~mask.Value);

                parent.RegistersCollection.DefineRegister((uint)Registers.SetPin + 4 * portNumber)
                    .WithValueField(0, PinsPerPort, name: $"GPIO_SET{portNumber}",
                        writeCallback: (_, value) => SetStateValue((uint)(state.Value | value)),
                        valueProviderCallback: _ => GetStateValue());

                parent.RegistersCollection.DefineRegister((uint)Registers.ClearPin + 4 * portNumber)
                    .WithValueField(0, PinsPerPort, FieldMode.Write, name: $"GPIO_CLR{portNumber}",
                        writeCallback: (_, value) => SetStateValue((uint)(state.Value & ~value)));

                parent.RegistersCollection.DefineRegister((uint)Registers.NegatePin + 4 * portNumber)
                    .WithValueField(0, PinsPerPort, FieldMode.Write, name: $"GPIO_NOT{portNumber}",
                        writeCallback: (_, value) => SetStateValue((uint)(state.Value ^ value)));
            }

            private UInt32 GetStateValue()
            {
                UInt32 result = 0;

                for(byte bitIndex = 0; bitIndex < PinsPerPort; bitIndex++)
                {
                    var idx = PinsPerPort * portNumber + bitIndex;
                    var isOutputPin = BitHelper.IsBitSet(direction.Value, bitIndex);

                    BitHelper.SetBit(ref result, bitIndex, isOutputPin
                        ? parent.Connections[idx].IsSet
                        : parent.State[idx]);
                }

                return result;
            }

            private void SetStateValue(UInt32 value)
            {
                state.Value = value;
                RefreshConnectionsState();
            }

            private void RefreshConnectionsState()
            {
                for(byte bitIndex = 0; bitIndex < PinsPerPort; bitIndex++)
                {
                    if(BitHelper.IsBitSet(direction.Value, bitIndex))
                    {
                        var connection = parent.Connections[PinsPerPort * portNumber + bitIndex];
                        var pinState = BitHelper.IsBitSet(state.Value, bitIndex);

                        connection.Set(pinState);
                    }
                }
            }

            private readonly int portNumber;
            private readonly IValueRegisterField direction;
            private readonly IValueRegisterField mask;
            private readonly IValueRegisterField state;

            private readonly LPC43xx_GPIO parent;
        }

        private enum Registers
        {
            Direction = 0x2000,
            Mask = 0x2080,
            Pin = 0x2100,
            MaskedPin = 0x2180,
            SetPin = 0x2200,
            ClearPin = 0x2280,
            NegatePin = 0x2300,
        }
    }
}
