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
    public class GD32_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public GD32_UART(IMachine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();
            registersMap.Add((long)Registers.Status0, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Read, name: "PERR - Parity error flag", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "FERR - Frame error flag", valueProviderCallback: _ => false)
                .WithFlag(2, FieldMode.Read, name: "NERR - Noise error flag", valueProviderCallback: _ => false)
                .WithFlag(3, FieldMode.Read, name: "ORERR - Overrun error", valueProviderCallback: _ => false)
                .WithFlag(4, FieldMode.Read, name: "IDLEF - IDLE frame detected flag", valueProviderCallback: _ => false)
                .WithFlag(5, FieldMode.Read, name: "RBNE - Read data buffer not empty", valueProviderCallback: _ => Count > 0)
                .WithFlag(6, FieldMode.Read, name: "TC - Transmission complete", valueProviderCallback: _ => false)
                .WithFlag(7, FieldMode.Read, name: "TBE - Transmit data buffer empty", valueProviderCallback: _ => true)
                .WithFlag(8, FieldMode.Read, name: "LBDF - LIN break detection flag", valueProviderCallback: _ => false)
                .WithFlag(9, FieldMode.Read, name: "CTSF - CTS change flag", valueProviderCallback: _ => false)
                .WithReservedBits(10, 22)
            );
           registersMap.Add((long)Registers.Data, new DoubleWordRegister(this)
                .WithValueField(0, 8, name: "DATA - Data",
                    valueProviderCallback: _ =>
                    {
                        if(!TryGetCharacter(out var c))
                        {
                            this.Log(LogLevel.Warning, "Tried to read from an empty FIFO");
                            return 0;
                        }
                        return c;
                    },
                    writeCallback: (_, v) => TransmitCharacter((byte)v))
                .WithReservedBits(8, 24)
           );

           IRQ = new GPIO();

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

        public long Size => 0x400;

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
            Status0 = 0x0,
            Data = 0x4,
            BaudRate = 0x8,
            Control0 = 0xC,
            Control1 = 0x10,
            Control2 = 0x14,
            GuardTimePrescaler = 0x18,
            Control3 = 0x80,
            ReceiverTimeout = 0x84,
            Status1 = 0x88,
            CoherenceControl = 0xC0,
        }
    }
}

