//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    public class LPC_USART : UARTBase, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public LPC_USART(IMachine machine) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        // IRQ is not supported yet
        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x1000;

        public override uint BaudRate => 0;
        public override Parity ParityBit => Parity.None;
        public override Bits StopBits => Bits.None;

        protected override void CharWritten()
        {
        }

        protected override void QueueEmptied()
        {
        }

        private void DefineRegisters()
        {
            Registers.Status.Define(this)
                .WithReservedBits(0, 1)
                .WithTaggedFlag("RXIDLE", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("TXIDLE", 3)
                .WithFlag(4, FieldMode.Read, name: "CTS - Clear To Send", valueProviderCallback: _ => true)
                .WithTaggedFlag("DELTACTS", 5)
                .WithTaggedFlag("TXDISSTAT", 6)
                .WithReservedBits(7, 2)
                .WithTaggedFlag("RXBRK", 10)
                .WithTaggedFlag("DELTARXBRK", 11)
                .WithTaggedFlag("START", 12)
                .WithTaggedFlag("FRAMERRINT", 13)
                .WithTaggedFlag("PARITYERRINT", 14)
                .WithTaggedFlag("RXNOSEINT", 15)
                .WithTaggedFlag("ABERR", 16)
                .WithReservedBits(17, 15);

            Registers.FifoStatus.Define(this)
                .WithTaggedFlag("TXERR", 0)
                .WithTaggedFlag("RXERR", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("PERINT", 3)
                .WithFlag(4, FieldMode.Read, name: "TXEMPTY", valueProviderCallback: _ => true)
                .WithFlag(5, FieldMode.Read, name: "TXNOTFULL", valueProviderCallback: _ => true)
                .WithTaggedFlag("RXNOTEMPTY", 6)
                .WithTaggedFlag("RXFULL", 7)
                .WithValueField(8, 5, FieldMode.Read, name: "TXLVL", valueProviderCallback: _ => 0)
                .WithReservedBits(13, 3)
                .WithValueField(16, 5, FieldMode.Read, name: "RXLVL", valueProviderCallback: _ => (ulong)Count)
                .WithReservedBits(21, 11);

            Registers.FifoWriteData.Define(this)
                .WithValueField(0, 8, FieldMode.Write, name: "TXDATA",
                    writeCallback: (_, value) => TransmitCharacter((byte)value))
                .WithReservedBits(8, 24);
        }

        private enum Registers
        {
	    Status = 0x8,
            FifoStatus = 0xE04,
            FifoWriteData = 0xE20,
        }
    }
}
