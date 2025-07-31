//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public class LiteX_UART : UARTBase, IDoubleWordPeripheral, IBytePeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public LiteX_UART(IMachine machine, uint txFifoCapacity = DefaultTxFifoCapacity, ulong? flushDelayNs = null, ulong? timeoutNs = null) : base(machine)
        {
            this.flushDelayTicks = TimeInterval.TicksPerNanosecond * flushDelayNs ?? DefaultFlushDelayNs;
            this.timeoutTicks = TimeInterval.TicksPerNanosecond * timeoutNs ?? DefaultTimeoutNs;

            this.txFifoCapacity = txFifoCapacity;

            IRQ = new GPIO();

            if(txFifoCapacity > 0)
            {
                if(flushDelayNs == 0)
                {
                    throw new ConstructionException($"'{nameof(flushDelayNs)}' must be greater than zero when '{nameof(txFifoCapacity)}' is non-zero");
                }
                if(timeoutNs == 0)
                {
                    throw new ConstructionException($"'{nameof(timeoutTicks)}' must be greater than zero when '{nameof(txFifoCapacity)}' is non-zero");
                }

                txFifo = new Queue<byte>();
                machine.ClockSource.AddClockEntry(new ClockEntry(
                    this.timeoutTicks,
                    (long)TimeInterval.TicksPerSecond,
                    FlushTransmissionBuffer,
                    this,
                    "UART flush",
                    false
                ));
            }
            else // unbuffered
            {    
                if(flushDelayNs.HasValue)
                {
                    throw new ConstructionException($"'{nameof(flushDelayNs)}' must not be specified when '{nameof(txFifoCapacity)}' is zero");
                }

                if(timeoutNs.HasValue)
                {
                    throw new ConstructionException($"'{nameof(timeoutNs)}' must not be specified when '{nameof(txFifoCapacity)}' is zero");
                }
            }

            RegistersCollection = new DoubleWordRegisterCollection(this, CreateRegisterMap());
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public virtual byte ReadByte(long offset)
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
            RegistersCollection.Reset();
            txFifo?.Clear();

            UpdateInterrupts();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public virtual void WriteByte(long offset, byte value)
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

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public override Bits StopBits => Bits.One;

        public override Parity ParityBit => Parity.None;

        public override uint BaudRate => 115200;

        protected virtual Dictionary<long, DoubleWordRegister> CreateRegisterMap()
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
                {(long)Registers.TxFull, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read,
                        valueProviderCallback: _ => txFifo?.Count >= txFifoCapacity
                    )
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.RxEmpty, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => Count == 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.EventPending, new DoubleWordRegister(this)
                    .WithFlag(0, out txEventPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "txEventPending")
                    .WithFlag(1, out rxEventPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "rxEventPending")
                    .WithReservedBits(2, 30)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.EventEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out txEventEnabled, name: "txEventEnabled")
                    .WithFlag(1, out rxEventEnabled, name: "rxEventEnabled")
                    .WithReservedBits(2, 30)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
            };
        }

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        protected void WriteData(ulong value)
        {
            if(txFifo == null)
            {
                TransmitCharacter((byte)value);
                return;
            }

            if(txFifo.Count == txFifoCapacity)
            {
                this.Log(LogLevel.Warning, "Attempted write to full buffer, ignoring 0x{0:X}", value);
                return;
            }

            txFifo.Enqueue((byte)value);

            if(txFifo.Count < txFifoCapacity)
            {
                Machine.ClockSource.ExchangeClockEntryWith(
                    FlushTransmissionBuffer,
                    oldClock => oldClock.With(enabled: true, period: timeoutTicks)
                );
            }
            else
            {
                txEventPending.Value = false;
                Machine.ClockSource.ExchangeClockEntryWith(
                    FlushTransmissionBuffer,
                    oldClock => oldClock.With(enabled: true, period: flushDelayTicks)
                );
            }
        }

        protected void FlushTransmissionBuffer()
        {
            this.Machine.ClockSource.ExchangeClockEntryWith(
                FlushTransmissionBuffer,
                oldClock => oldClock.With(enabled: false)
            );
            Array.ForEach(txFifo.DequeueAll<byte>(), TransmitCharacter);
            txEventPending.Value = true;
            UpdateInterrupts();
        }

        protected void UpdateInterrupts()
        {
            // rxEventPending is latched
            rxEventPending.Value = (Count != 0);

            var eventPending = (rxEventEnabled.Value && rxEventPending.Value)
                || (txEventEnabled.Value && txEventPending.Value);
            IRQ.Set(eventPending);
        }

        protected readonly ulong timeoutTicks;
        protected readonly ulong flushDelayTicks;
        protected readonly uint txFifoCapacity;
        protected readonly Queue<byte> txFifo;

        protected IFlagRegisterField txEventEnabled;
        protected IFlagRegisterField rxEventEnabled;
        protected IFlagRegisterField txEventPending;
        protected IFlagRegisterField rxEventPending;

        protected const uint DefaultTxFifoCapacity = 8;
        protected const ulong DefaultFlushDelayNs = 100;
        protected const ulong DefaultTimeoutNs = 200 * 1000 * 1000;

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
