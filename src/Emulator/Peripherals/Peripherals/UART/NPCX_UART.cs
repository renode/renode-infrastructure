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

namespace Antmicro.Renode.Peripherals.UART
{
    public class NPCX_UART : UARTBase, IBytePeripheral, IKnownSize
    {
        public NPCX_UART(Machine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.TransmitBuffer, new ByteRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) => this.TransmitCharacter((byte)value), name: "txdata")
                },
                {(long)Registers.ReceiveBuffer, new ByteRegister(this)
                    .WithValueField(0, 8,
                        valueProviderCallback: _ =>
                        {
                            if(!TryGetCharacter(out var character))
                            {
                                return 0xFF;
                            }
                            return character;
                        }, name: "rxdata")
                },
                {(long)Registers.TransmitStatus, new ByteRegister(this)
                    .WithValueField(0, 8, valueProviderCallback: _ => 0x1)
                },
            };
            registers = new ByteRegisterCollection(this, registersMap);
        }

        public byte ReadByte(long offset)
        {
            return registers.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            IRQ.Unset();
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; set;} = new GPIO();

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

        private readonly ByteRegisterCollection registers;

        private enum Registers : long
        {
            TransmitBuffer = 0x00,
            ReceiveBuffer = 0x02,
            TransmitStatus = 0x20,
            ReceiveStatus = 0x22,
        }
    }
}
