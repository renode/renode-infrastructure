//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    public class MAX32655_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public MAX32655_UART(IMachine machine) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            IRQ = new GPIO();
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
            IRQ.Unset();
        }

        public override uint BaudRate => 9600;

	public override Bits StopBits => Bits.One;

	public override Parity ParityBit => Parity.Even;

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        protected override void CharWritten()
        {
            // intentionally left empty
        }

        protected override void QueueEmptied()
        {
            // intentionally left empty
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.UartFIFO, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => TransmitCharacter((byte)val))
                }
            };

            return registersMap;
        }

        private void UpdateInterrupts()
        {
            // IRQ currently not supported
        }

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            UartCtrl = 0x00,
            UartStatus = 0x04,
            UartIntEn = 0x08,
            UartIntFl = 0x0C,
            UartClkDiv = 0x10,
            UartOSR = 0x14,
            UartTXPeek = 0x18,
            UartPWR = 0x1C,
            UartFIFO = 0x20,
            Reserved0 = 0x24,
            Reserved1 = 0x28,
            Reserved2 = 0x2C,
            DMA = 0x30,
            WkEn = 0x34,
            WkFl = 0x38,
        }
    }
}
