//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    public class Infineon_SCBUART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public Infineon_SCBUART(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            var registersMap = new Dictionary<long, DoubleWordRegister>();
            registersMap.Add((long)Registers.TxFifoWrite, new DoubleWordRegister(this)
                .WithValueField(0, 8, FieldMode.Write, name: "DATA - Data",
                        writeCallback: (_, v) => TransmitCharacter((byte)v))
                    .WithReservedBits(8, 24)
                );

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; }

        public override uint BaudRate => 115200;
        public override Parity ParityBit => Parity.None;
        public override Bits StopBits => Bits.One;

        public long Size => 0x10000;

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            Control = 0x0,
            UartControl = 0x40,
            TxControl = 0x200,
            TxFifoControl = 0x204,
            TxFifoStatus = 0x208,
            TxFifoWrite = 0x240,
            RxControl = 0x300,
            RxFifoControl = 0x304,
            RxMatch = 0x310,
            IntrTxMask = 0xF88,
            IntrRxMask = 0xFC8,
        }
    }
}
