//
// Copyright (c) 2010-2018 Antmicro
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
    public class PicoSoC_SimpleUART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public PicoSoC_SimpleUART(IMachine machine) : base(machine)
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ClockDivider, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "clkdiv")
                },
                {(long)Registers.RxTx, new DoubleWordRegister(this)
                    .WithValueField(0, 32, writeCallback: (_, value) => this.TransmitCharacter((byte)value),
                        valueProviderCallback: _ =>
                        {
                            if(!TryGetCharacter(out var character))
                            {
                                return 0xFFFFFFFF;
                            }
                            return character;
                        }, name: "data")
                },
             };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
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
            ClockDivider = 0x0,
            RxTx = 0x04,
        }
    }
}
