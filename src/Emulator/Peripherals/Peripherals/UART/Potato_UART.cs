//
// Copyright (c) 2010-2022 Antmicro
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
    [AllowedTranslations(AllowedTranslation.QuadWordToDoubleWord)]
    public class Potato_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public Potato_UART(Machine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TransmitRxLo, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => this.TransmitCharacter((byte)value), name: "TransmitRxLo")
                },
                {(long)Registers.TransmitRxHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32) // simulating 64bit register, just here to silence the warning
                },
                {(long)Registers.ReceiveRxLo, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                            return 0x0;
                        }
                        return character;
                    }, name: "ReceiveRxLo")
                },
                {(long)Registers.ReceiveRxHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32) // simulating 64bit register, just here to silence the warning
                },
                {(long)Registers.StatusRxLo, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => Count == 0, name: "ReceiveRxEmpty")
                    .WithFlag(1, valueProviderCallback: _ => Count == 0, name: "TransmitRxEmpty")
                    .WithFlag(2, valueProviderCallback: _ => false, name: "ReceiveRxFull")
                    .WithFlag(3, valueProviderCallback: _ => false, name: "TransmitRxFull")
                    .WithReservedBits(4, 28)
                 },
                {(long)Registers.StatusRxHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32)
                 },
                {(long)Registers.ClockDividerLo, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "ClkDivLo")
                },
                {(long)Registers.ClockDividerHi, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "ClkDivHi")
                },
                {(long)Registers.InterruptEnableRxLo, new DoubleWordRegister(this)
                    .WithFlag(0, valueProviderCallback: _ => false, name: "Interrupt_Receive")
                    .WithFlag(1, valueProviderCallback: _ => false, name: "Interrupt_Transmit")
                    .WithReservedBits(2, 30)
                 },
                {(long)Registers.InterruptEnableRxHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32)
                 },
             };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
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

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            TransmitRxLo = 0x0,
            TransmitRxHi = 0x4,
            ReceiveRxLo = 0x08,
            ReceiveRxHi = 0xC,
            StatusRxLo = 0x10,
            StatusRxHi = 0x14,
            ClockDividerLo = 0x18,
            ClockDividerHi = 0x1C,
            InterruptEnableRxLo = 0x20,
            InterruptEnableRxHi = 0x24,
        }
    }
}
