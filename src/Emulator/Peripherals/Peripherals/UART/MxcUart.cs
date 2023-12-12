//
// Copyright (c) 2010-2023 Antmicro
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
    public class MxcUart : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public MxcUart(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();            
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Receive, new DoubleWordRegister(this)
                    .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                            return 0x0;
                        }
                        return character;
                    }, name: "RX_DATA")
                    .WithReservedBits(8, 2)
                    .WithTaggedFlag("PRERR", 10)
                    .WithTaggedFlag("BRK", 11)
                    .WithTaggedFlag("FRMERR", 12)
                    .WithTaggedFlag("OVRRUN", 13)
                    .WithTaggedFlag("ERR", 14)
                    .WithFlag(15, FieldMode.Read, valueProviderCallback: _ => Count > 0, name: "CHARRDY")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Transmit, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) => this.TransmitCharacter((byte)value), name: "TX_DATA")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.Control1, new DoubleWordRegister(this)
                    .WithTaggedFlag("UARTEN", 0)
                    .WithTaggedFlag("DOZE", 1)
                    .WithTaggedFlag("ATDMAEN", 2)
                    .WithTaggedFlag("TXDMAEN", 3)
                    .WithTaggedFlag("SNDBRK", 4)
                    .WithTaggedFlag("RTSDEN", 5)
                    .WithTaggedFlag("TXMPTYEN", 6)
                    .WithTaggedFlag("IREN", 7)
                    .WithTaggedFlag("RXDMAEN", 8)
                    .WithTaggedFlag("RRDYEN", 9)
                    .WithTag("ICD", 10, 2)
                    .WithTaggedFlag("IDEN", 12)
                    .WithTaggedFlag("TRDYEN", 13)
                    .WithTaggedFlag("ADBR", 14)
                    .WithTaggedFlag("ADEN", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Control2, new DoubleWordRegister(this, 0x00000001)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "SRST")
                    .WithTaggedFlag("RXEN", 1)
                    .WithTaggedFlag("TXEN", 2)
                    .WithTaggedFlag("ATEN", 3)
                    .WithTaggedFlag("RTSEN", 4)
                    .WithTaggedFlag("WS", 5)
                    .WithTaggedFlag("STPB", 6)
                    .WithTaggedFlag("PROE", 7)
                    .WithTaggedFlag("PREN", 8)
                    .WithTag("RTEC", 9, 2)
                    .WithTaggedFlag("ESCEN", 11)
                    .WithTaggedFlag("CTS", 12)
                    .WithTaggedFlag("CTSC", 13)
                    .WithTaggedFlag("IRTS", 14)
                    .WithTaggedFlag("ESCI", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Control3, new DoubleWordRegister(this, 0x00000700)
                    .WithTaggedFlag("ACIEN", 0)
                    .WithTaggedFlag("INVT", 1)
                    .WithTaggedFlag("RXDMUXSEL", 2)
                    .WithTaggedFlag("DTRDEN", 3)
                    .WithTaggedFlag("AWAKEN", 4)
                    .WithTaggedFlag("AIRINTEN", 5)
                    .WithTaggedFlag("RXDSEN", 6)
                    .WithTaggedFlag("ADNIMP", 7)
                    .WithTaggedFlag("RI", 8)
                    .WithTaggedFlag("DCD", 9)
                    .WithTaggedFlag("DSR", 10)
                    .WithTaggedFlag("FRAERREN", 11)
                    .WithTaggedFlag("PARERREN", 12)
                    .WithTaggedFlag("DTREN", 13)
                    .WithTag("DPEC", 14, 2)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Control4, new DoubleWordRegister(this, 0x00008000)
                    .WithTaggedFlag("DREN", 0)
                    .WithTaggedFlag("OREN", 1)
                    .WithTaggedFlag("BKEN", 2)
                    .WithTaggedFlag("TCEN", 3)
                    .WithTaggedFlag("LPBYP", 4)
                    .WithTaggedFlag("IRSC", 5)
                    .WithTaggedFlag("IDDMAEN", 6)
                    .WithTaggedFlag("WKEN", 7)
                    .WithTaggedFlag("ENIRI", 8)
                    .WithTaggedFlag("INVR", 9)
                    .WithTag("CTSTL", 10, 6)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.FifoControl, new DoubleWordRegister(this, 0x00008001)
                    .WithTag("RXTL", 0, 6)
                    .WithTaggedFlag("DCEDTE", 6)
                    .WithTag("RFDIV", 7, 3)
                    .WithTag("TXTL", 10, 6)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Status1, new DoubleWordRegister(this, 0x00002040)
                    .WithReservedBits(0, 4)
                    .WithTaggedFlag("AWAKE", 4)
                    .WithTaggedFlag("AIRINT", 5)
                    .WithTaggedFlag("RXDS", 6)
                    .WithTaggedFlag("DTRD", 7)
                    .WithTaggedFlag("AGTIM", 8)
                    .WithTaggedFlag("RRDY", 9)
                    .WithTaggedFlag("FRAMERR", 10)
                    .WithTaggedFlag("ESCF", 11)
                    .WithTaggedFlag("RTSD", 12)
                    .WithTaggedFlag("TRDY", 13)
                    .WithTaggedFlag("RTSS", 14)
                    .WithTaggedFlag("PARITYERR", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Status2, new DoubleWordRegister(this, 0x00004028)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => Count > 0, name: "RDR")
                    .WithTaggedFlag("ORE", 1)
                    .WithTaggedFlag("BRCD", 2)
                    .WithTaggedFlag("TXDC", 3)
                    .WithTaggedFlag("RTSF", 4)
                    .WithTaggedFlag("DCDIN", 5)
                    .WithTaggedFlag("DCDDELT", 6)
                    .WithTaggedFlag("WAKE", 7)
                    .WithTaggedFlag("IRINT", 8)
                    .WithTaggedFlag("RIIN", 9)
                    .WithTaggedFlag("RIDELT", 10)
                    .WithTaggedFlag("ACST", 11)
                    .WithTaggedFlag("IDLE", 12)
                    .WithTaggedFlag("DTRF", 13)
                    .WithTaggedFlag("TXFE", 14)
                    .WithTaggedFlag("ADET", 15)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.EscapeCharacter, new DoubleWordRegister(this, 0x0000002b)
                    .WithTag("ESC_CHAR", 0, 8)
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.EscapeTimer, new DoubleWordRegister(this)
                    .WithTag("TIM", 0, 12)
                    .WithReservedBits(12, 20)
                },
                {(long)Registers.BrmIncremental, new DoubleWordRegister(this)
                    .WithTag("INC", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.BrmModulator, new DoubleWordRegister(this)
                    .WithTag("MOD", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.BaudRateCount, new DoubleWordRegister(this, 0x00000004)
                    .WithTag("BCNT", 0, 16)
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.OneMillisecond, new DoubleWordRegister(this)
                    .WithTag("ONEMS", 0, 24)
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.Test, new DoubleWordRegister(this, 0x00000060)
                    .WithTaggedFlag("SOFTRST", 0)
                    .WithReservedBits(1, 2)
                    .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "RXFULL")
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "TXFULL")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => Count == 0, name: "RXEMPTY")
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ => true, name: "TXEMPTY")
                    .WithReservedBits(7, 2)
                    .WithTaggedFlag("RXDBG", 9)
                    .WithTaggedFlag("LOOPIR", 10)
                    .WithTaggedFlag("DEBGN", 11)
                    .WithTaggedFlag("LOOP", 12)
                    .WithTaggedFlag("FRCPERR", 13)
                    .WithReservedBits(14, 18)
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
        
        public GPIO IRQ { get; }

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
            Receive = 0x00,
            // gap
            Transmit = 0x40,
            // gap
            Control1 = 0x80,
            Control2 = 0x84,
            Control3 = 0x88,
            Control4 = 0x8c,
            FifoControl = 0x90,
            Status1 = 0x94,
            Status2 = 0x98,
            EscapeCharacter = 0x9c,
            EscapeTimer = 0xa0,
            BrmIncremental = 0xa4,
            BrmModulator = 0xa8,
            BaudRateCount = 0xac,
            OneMillisecond = 0xb0,
            Test = 0xb4,
        }
    }
}
