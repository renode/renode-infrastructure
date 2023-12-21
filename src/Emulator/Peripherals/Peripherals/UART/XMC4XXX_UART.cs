//
// Copyright (c) 2010-2023 Antmicro
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
    public class XMC4XXX_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public XMC4XXX_UART(IMachine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.KernelStateConfiguration, new DoubleWordRegister(this)
                .WithFlag(0, name: "MODEN - Module Enable")
                .WithTaggedFlag("BPMODEN - Bit Protection for MODEN", 1)
                .WithReservedBits(2, 2)
                .WithTag("NOMCFG - Normal Operation Mode Configuration", 4, 2)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("BPNOM - Bit Protection for NOMCFG", 7)
                .WithTag("SUMCFG - Suspend Mode Configuration", 8, 2)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("BPSUM - Bit Protection for SUMCFG", 11)
                .WithReservedBits(12, 20)
            );

            registersMap.Add((long)Registers.TransmitBufferInput0, new DoubleWordRegister(this)
                .WithValueField(0, 16, name: "TDATA - Transmit Data", writeCallback: (_, v) => TransmitCharacter((byte)v))
                .WithReservedBits(16, 16)
            );

            TxInterrupt = new GPIO();
            RxInterrupt = new GPIO();

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

        public void WriteDoubleWord(long offset, uint val)
        {
            registers.Write(offset, val);
        }

        public GPIO TxInterrupt { get; }
        public GPIO RxInterrupt { get; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        public long Size => 0x200;

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
            KernelStateConfiguration = 0xC,
            TransmitBufferInput0 = 0x80,
        }
    }
}
