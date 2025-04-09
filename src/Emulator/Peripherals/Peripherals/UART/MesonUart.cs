//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Migrant;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class MesonUart : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public MesonUart(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
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

        private void DefineRegisters()
        {
            Registers.ReadFifo.Define(this)
                .WithValueField(0, 7, FieldMode.Read, name: "RFIFO",
                valueProviderCallback: _ =>
                {
                    if(!TryGetCharacter(out byte value))
                    {
                        this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                        return 0;
                    }
                    return value;
                })
                .WithReservedBits(8, 24)
            ;

            Registers.WriteFifo.Define(this)
                .WithValueField(0, 7, FieldMode.Write, name: "WFIFO",
                writeCallback: (_, value) =>
                {
                    TransmitCharacter((byte)value);
                })
                .WithReservedBits(8, 24)
            ;

            Registers.Control.Define(this)
                .WithValueField(0, 12, FieldMode.Read, name: "BaudRate", valueProviderCallback: _ => BaudRate)
                .WithFlag(12, name: "TX Enabled", valueProviderCallback: _ => true)
                .WithFlag(13, name: "RX Enabled", valueProviderCallback: _ => true)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("Two Wire mode", 15)
                .WithTag("Stop bit length", 16, 2)
                .WithTaggedFlag("Parity type", 18)
                .WithTaggedFlag("Parity enable", 19)
                .WithTag("Character length", 20, 2)
                .WithTaggedFlag("TX Reset", 22)
                .WithTaggedFlag("RX Reset", 23)
                .WithTaggedFlag("Clear Error", 24)
                .WithTaggedFlag("Invert RX", 25)
                .WithTaggedFlag("Invert TX", 26)
                .WithTaggedFlag("RX Byte Interrupt", 27)
                .WithTaggedFlag("TX Byte Interrupt", 28)
                .WithTaggedFlag("Invert CTS", 29)
                .WithTaggedFlag("Mask Error", 30)
                .WithTaggedFlag("Invert RTS", 31)
            ;

            Registers.Status.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "Receive FIFO count", valueProviderCallback: _ => (ulong)Count)
                .WithTag("Transmit FIFO count", 8, 7)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("Parity error", 16)
                .WithTaggedFlag("Frame error", 17)
                .WithTaggedFlag("Write error", 18)
                .WithTaggedFlag("Receive FIFO full", 19)
                .WithFlag(20, FieldMode.Read, name: "Receive FIFO empty", valueProviderCallback: _ => Count == 0)
                .WithTaggedFlag("Transmit FIFO full", 21)
                .WithTaggedFlag("Transmit FIFO empty", 22)
                .WithTaggedFlag("CTS Level", 23)
                .WithTaggedFlag("Receive FIFO overflow", 24)
                .WithTaggedFlag("Transmit busy", 25)
                .WithTaggedFlag("Receive busy", 26)
                .WithReservedBits(27, 5)
            ;
        }

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }
        public long Size => 0x18;
        public override Bits StopBits => Bits.One;
        public override Parity ParityBit => Parity.None;
        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            // Intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // Intentionally left blank
        }

        private enum Registers
        {
           WriteFifo = 0x0,
           ReadFifo = 0x4,
           Control = 0x8,
           Status = 0xC,
           Misc = 0x10,
           Reg5 = 0x14,
        }
    }
}

