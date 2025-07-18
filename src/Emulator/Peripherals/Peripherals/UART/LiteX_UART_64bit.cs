//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.UART
{
    // this is a model of LiteX UART with register layout to simulate 64 bit bus read/write access
    public class LiteX_UART64 : LiteX_UART
    {
        public LiteX_UART64(IMachine machine, uint txFifoCapacity = DefaultTxFifoCapacity, ulong? flushDelayNs = null, ulong? timeoutNs = null)
            : base(machine, txFifoCapacity, flushDelayNs, timeoutNs)
        {
        }

        public override byte ReadByte(long offset)
        {
            if(offset % 8 != 0)
            {
                // in the current configuration, only the lowest byte
                // contains a meaningful data
                return 0;
            }
            return (byte)ReadDoubleWord(offset);
        }

        public override void WriteByte(long offset, byte value)
        {
            if(offset % 8 != 0)
            {
                // in the current configuration, only the lowest byte
                // contains a meaningful data
                return;
            }

            WriteDoubleWord(offset, value);
        }

        protected override Dictionary<long, DoubleWordRegister> CreateRegisterMap()
        {
            return new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.RxTx, new DoubleWordRegister(this)
                    .WithValueField(0, 8,
                        writeCallback: (_, value) => WriteData(value),
                        valueProviderCallback: _ =>
                        {
                            if(!TryGetCharacter(out var character))
                            {
                                this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
                            }
                            return character;
                        }
                    )
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.RxTxHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32) // simulating an upper half of a 64bit register, never used bits
                },
                {(long)Registers.TxFull, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read,
                        valueProviderCallback: _ => txFifo?.Count >= txFifoCapacity
                    )
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.TxFullHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32) // simulating an upper half of a 64bit register, never used bits
                },
                {(long)Registers.RxEmpty, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => Count == 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.RxEmptyHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32) // simulating an upper half of a 64bit register, never used bits
                },
                {(long)Registers.EventPending, new DoubleWordRegister(this)
                    .WithFlag(0, out txEventPending, FieldMode.Read | FieldMode.WriteOneToClear, valueProviderCallback: _ => false, name: "txEventPending")
                    .WithFlag(1, out rxEventPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "rxEventPending")
                    .WithReservedBits(2, 30)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.EventPendingHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32) // simulating an upper half of a 64bit register, never used bits
                },
                {(long)Registers.EventEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txEventEnabled, name: "txEventEnabled")
                    .WithFlag(1, out rxEventEnabled, name: "rxEventEnabled")
                    .WithReservedBits(2, 30)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.EventEnableHi, new DoubleWordRegister(this)
                    .WithReservedBits(0, 32) // simulating an upper half of a 64bit register, never used bits
                },
            };
        }

        private enum Registers : long
        {
            RxTx = 0x0,
            RxTxHi = 0x04,
            TxFull = 0x08,
            TxFullHi = 0x0C,
            RxEmpty = 0x10,
            RxEmptyHi = 0x14,
            EventStatus = 0x18,
            EventStatusHi = 0x1C,
            EventPending = 0x20,
            EventPendingHi = 0x24,
            EventEnable = 0x28,
            EventEnableHi = 0x3C
        }
    }
}
