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
    public class LINFlexD_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public LINFlexD_UART(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();

            var registersMap = new Dictionary<long, DoubleWordRegister>();
            registersMap.Add((long)Registers.BufferDataLS, new DoubleWordRegister(this)
                .WithValueField(0, 8, FieldMode.Write, name: "DATA - Data",
                        writeCallback: (_, v) => TransmitCharacter((byte)v))
                    .WithReservedBits(8, 24)
                );
            registersMap.Add((long)Registers.UARTModeStatus, new DoubleWordRegister(this)
                .WithReservedBits(0,1)
                .WithFlag(1, FieldMode.Read, name: "DTFTFF", valueProviderCallback: _ => true)
                .WithReservedBits(2,30));

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

        public long Size => 0x1000;

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
            LINControl1 = 0x0,
            LINInterruptEnable = 0x4,
            LINStatus = 0x8,
            LINErrorStatus = 0xc,
            UARTModeControl = 0x10,
            UARTModeStatus = 0x14,
            LINTimeOutControlStatus = 0x18,
            LINOutputCompare = 0x1c,
            LINTimeOutControl = 0x20,
            LINFractionalBaudRate = 0x24,
            LINIntegerBaudRate = 0x28,
            LINChecksumField = 0x2c,
            LINControl2 = 0x30,
            BufferIdentifier = 0x34,
            BufferDataLS = 0x38,
            BufferDataMS = 0x3c,
            GlobalControl = 0x4c,
            UARTPresetTimeout = 0x50,
            UARTCurrentTimeout = 0x54,
            DMATXEnable = 0x58,
            DMARXEnable = 0x5c
        }
    }
}
