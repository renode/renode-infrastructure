//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    public class Potato_UART : UARTBase, IQuadWordPeripheral, IKnownSize
    {
        public Potato_UART(IMachine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, QuadWordRegister>
            {
                {(long)Registers.TransmitRx, new QuadWordRegister(this)
                    .WithValueField(0, 64, writeCallback: (_, value) => this.TransmitCharacter((byte)value), name: "TransmitRx")
                },
                {(long)Registers.ReceiveRx, new QuadWordRegister(this)
                    .WithValueField(0, 64, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                            return 0x0;
                        }
                        return character;
                    }, name: "ReceiveRx")
                },
                {(long)Registers.StatusRx, new QuadWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => Count == 0, name: "ReceiveRxEmpty")
                    .WithFlag(1, valueProviderCallback: _ => Count == 0, name: "TransmitRxEmpty")
                    .WithFlag(2, valueProviderCallback: _ => false, name: "ReceiveRxFull")
                    .WithFlag(3, valueProviderCallback: _ => false, name: "TransmitRxFull")
                    .WithReservedBits(4, 60)
                 },
                {(long)Registers.ClockDivider, new QuadWordRegister(this)
                    .WithValueField(0, 64, name: "ClkDiv")
                },
                {(long)Registers.InterruptEnableRx, new QuadWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => false, name: "Interrupt_Receive")
                    .WithFlag(1, valueProviderCallback: _ => false, name: "Interrupt_Transmit")
                    .WithReservedBits(2, 62)
                 },
             };
            registers = new QuadWordRegisterCollection(this, registersMap);
        }

        public ulong ReadQuadWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteQuadWord(long offset, ulong value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public long Size => 0x100;

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private readonly QuadWordRegisterCollection registers;

        private enum Registers : long
        {
            TransmitRx = 0x0,
            ReceiveRx = 0x08,
            StatusRx = 0x10,
            ClockDivider = 0x18,
            InterruptEnableRx = 0x20,
        }
    }
}
