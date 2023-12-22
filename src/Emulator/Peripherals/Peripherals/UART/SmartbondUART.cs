//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    public class SmartbondUART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public SmartbondUART(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.TransmitReceiveBuffer, new DoubleWordRegister(this)
                .WithValueField(0, 8, name: "RBR_THR_DLL", writeCallback: (_, v) => TransmitCharacter((byte)v))
                .WithReservedBits(8, 24)
            );

            registersMap.Add((long)Registers.StatusRegister, new DoubleWordRegister(this)
                .WithFlag(0, FieldMode.Read, name: "UART_BUSY - UART Busy", valueProviderCallback: _ => false)
                .WithFlag(1, FieldMode.Read, name: "UART_TFNF - Transmit FIFO Not Full", valueProviderCallback: _ => true)
                .WithFlag(2, FieldMode.Read, name: "UART_TFE - Transmit FIFO Empty", valueProviderCallback: _ => true)
                .WithFlag(3, FieldMode.Read, name: "UART_RFNE - Receive FIFO Not Empty", valueProviderCallback: _ => Count > 0)
                .WithFlag(4, FieldMode.Read, name: "UART_RFFE - Receive FIFO Full", valueProviderCallback: _ => Count >= MaxFifoCount)
                .WithReservedBits(5, 27)
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
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override uint BaudRate => 0;
        public override Parity ParityBit => Parity.None;
        public override Bits StopBits => Bits.None;

        public long Size => 0x100;

        public GPIO IRQ { get; }

        protected override void CharWritten()
        {
            // intentionally left blank
        }

        protected override void QueueEmptied()
        {
            // intentionally left blank
        }

        private readonly DoubleWordRegisterCollection registers;

        private const int MaxFifoCount = 16;

        private enum Registers
        {
            TransmitReceiveBuffer = 0x0,
            StatusRegister = 0x7C
        }
    }
}

