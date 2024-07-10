//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    public class KB1200_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public KB1200_UART(IMachine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.Configuration, new DoubleWordRegister(this)
                .WithTaggedFlag(name: "SERIE_RX_ENABLE", 0)
                .WithTaggedFlag(name: "SERIE_TX_ENABLE", 1)
                .WithTag("Parity", 2, 2)
                .WithReservedBits(6, 10)
                .WithTag("Baud Rate", 16, 16)
            );
            
            registersMap.Add((long)Registers.InterruptEnable, new DoubleWordRegister(this)
                .WithTag("SERIE", 0, 3)
                .WithReservedBits(4, 28)
            );
            
            registersMap.Add((long)Registers.PendingFlag, new DoubleWordRegister(this)
                .WithTag("SERPF", 0, 3)
                .WithReservedBits(4, 28)
            );
            
            registersMap.Add((long)Registers.Status, new DoubleWordRegister(this)
                .WithTaggedFlag("TX_FULL", 0)
                .WithTaggedFlag("TX_OVERRUN", 1)
                .WithTaggedFlag("TX_BUSY", 2)
                .WithReservedBits(3, 12)
                .WithTaggedFlag("RX_EMPTY", 16)
                .WithTaggedFlag("RX_OVERRUN", 17)
                .WithTaggedFlag("RX_BUSY", 18)
                .WithTaggedFlag("RX_TIMEOUT", 19)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("PARITY_ERROR", 24)
                .WithTaggedFlag("FRAME_ERROR", 25)
                .WithReservedBits(26, 6)
            );
            
            registersMap.Add((long)Registers.RxDataBuffer, new DoubleWordRegister(this)
                .WithTag("SERTBUF", 0, 8)
                .WithReservedBits(8, 24)
            );

            registersMap.Add((long)Registers.TxDataBuffer, new DoubleWordRegister(this)
                .WithValueField(0, 8, FieldMode.Write, name: "SERRBUF", writeCallback: (_, v) => TransmitCharacter((byte)v))
                .WithReservedBits(8, 24)
            );
            
            registersMap.Add((long)Registers.Control, new DoubleWordRegister(this)
                .WithTag("SERCTRL", 0, 3)
                .WithReservedBits(3, 29)
            );

            TxInterrupt = new GPIO();
            RxInterrupt = new GPIO();

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            TxInterrupt.Set(false);
            RxInterrupt.Set(false);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint val)
        {
            registers.Write(offset, val);
        }

        public GPIO TxInterrupt { get; }
        public GPIO RxInterrupt { get; }

        public override Bits StopBits { get; }

        public override Parity ParityBit { get; }

        public override uint BaudRate => 115200;

        public long Size => 0x1C;

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
            Configuration = 0x00,
            InterruptEnable = 0x04,
            PendingFlag = 0x08,
            Status = 0x0C,
            RxDataBuffer = 0x10,
            TxDataBuffer = 0x14,
            Control = 0x18
        }
    }
}
