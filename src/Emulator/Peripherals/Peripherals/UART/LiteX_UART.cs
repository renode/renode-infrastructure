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
    public class LiteX_UART : UARTBase, IDoubleWordPeripheral, IBytePeripheral, IKnownSize
    {
        public LiteX_UART(IMachine machine) : base(machine)
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
                    // `txEventPending` implements `WriteOneToClear` semantics to avoid fake warnings
                    // `txEventPending` is generated on the falling edge of TxFull; in our case it means never
                    .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, valueProviderCallback: _ => false, name: "txEventPending")
                    .WithFlag(1, out rxEventPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "rxEventPending")
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.EventEnable, new DoubleWordRegister(this)
                    .WithFlag(0, name: "txEventEnabled")
                    .WithFlag(1, out rxEventEnabled)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public byte ReadByte(long offset)
        {
            if(offset % 4 != 0)
            {
                // in the current configuration, only the lowest byte
                // contains a meaningful data
                return 0;
            }
            return (byte)ReadDoubleWord(offset);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();

            UpdateInterrupts();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }
         
        public void WriteByte(long offset, byte value)
        {
            if(offset % 4 != 0)
            {
                // in the current configuration, only the lowest byte
                // contains a meaningful data
                return;
            }

            WriteDoubleWord(offset, value);
        }
        
        public long Size => 0x100;

        public GPIO IRQ { get; private set; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            // rxEventPending is latched
            rxEventPending.Value = (Count != 0);

            // tx fifo is never full, so `txEventPending` is always false
            var eventPending = (rxEventEnabled.Value && rxEventPending.Value);
            IRQ.Set(eventPending);
        }

        private IFlagRegisterField rxEventEnabled;
        private IFlagRegisterField rxEventPending;
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
