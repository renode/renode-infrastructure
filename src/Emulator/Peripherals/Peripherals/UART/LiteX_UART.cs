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
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class LiteX_UART : UARTBase, IDoubleWordPeripheral, IKnownSize
    {
        public LiteX_UART(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.RxTx, new DoubleWordRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) => this.TransmitCharacter((byte)value),
                        valueProviderCallback: _ => {
                            if(!TryGetCharacter(out var character))
                            {
                                this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                            }
                            return character;
                        })
                },
                {(long)Registers.TxFull, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read) //tx is never full
                },
                {(long)Registers.RxEmpty, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => Count == 0)
                },
                {(long)Registers.EventPending, new DoubleWordRegister(this)
                    .WithFlag(1, valueProviderCallback: _ => Count != 0, writeCallback: (_, value) => { if(value) IRQ.Unset(); })
                },
                {(long)Registers.EventEnable, new DoubleWordRegister(this)
                    .WithTag("tx_event_enabled", 0, 1)
                    .WithFlag(1, out rxEventEnabled, changeCallback: (_, value) => { if(!value) IRQ.Unset(); } )
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
            IRQ.Unset();
            registers.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x100;

        public GPIO IRQ { get; private set; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            if(rxEventEnabled.Value)
            {
                // we do not filter IRQ.Unset, as it should not really influence anything.
                IRQ.Set();
            }
        }

        protected override void QueueEmptied()
        {
            IRQ.Unset(); // I'm not certain about this
        }

        private IFlagRegisterField rxEventEnabled;
        private readonly DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            RxTx = 0x0,
            TxFull = 0x04,
            RxEmpty = 0x08,
            EventStatus = 0x0c,
            EventPending = 0x10,
            EventEnable = 0x14,
        }
    }
}
