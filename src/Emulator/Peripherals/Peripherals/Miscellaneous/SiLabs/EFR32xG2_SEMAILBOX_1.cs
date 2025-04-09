//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Digests;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class EFR32xG2_SEMAILBOX_1 : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_SEMAILBOX_1(Machine machine)
        {
            this.machine = machine;

            txFifo = new Queue<uint>();
            rxFifo = new Queue<uint>();
            
            RxIRQ = new GPIO();
            TxIRQ = new GPIO();

            Silabs_SecureElement = new Silabs_SecureElement(machine, this, txFifo, rxFifo, false);
            
            registersCollection = BuildRegistersCollection();
        }

        public void Reset()
        {
            txFifo.Clear();
            rxFifo.Clear();
            Silabs_SecureElement.Reset();
            rxHeaderAvailable = false;
        }

        public uint ReadDoubleWord(long offset)
        {
            var result = 0U;

            if(!registersCollection.TryRead(offset, out result))
            {
                this.Log(LogLevel.Noisy, "Unhandled read at offset 0x{0:X} ({1}).", offset, (Registers)offset);
            }
            else
            {
                this.Log(LogLevel.Noisy, "Read at offset 0x{0:X} ({1}), returned 0x{2:X}.", offset, (Registers)offset, result);
            }

            return result;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Noisy, "Write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
            if(!registersCollection.TryWrite(offset, value))
            {
                this.Log(LogLevel.Noisy, "Unhandled write at offset 0x{0:X} ({1}), value 0x{2:X}.", offset, (Registers)offset, value);
                return;
            }
        }

        private DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.TxStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)TxFifoWordsCount, name: "REMBYTES")
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "MSGINFO")
                    .WithFlag(20, FieldMode.Read, valueProviderCallback: _ => !TxFifoIsAlmostFull, name: "TXINT")
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => TxFifoIsFull, name: "TXFULL")
                    .WithReservedBits(22, 1)
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "TXERROR")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.RxStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)RxFifoWordsCount, name: "REMBYTES")
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "MSGINFO")
                    .WithFlag(20, out rxInterrupt, FieldMode.Read, name: "RXINT")
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => RxFifoIsEmpty, name: "RXEMPTY")
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => RxHeaderAvailable, name: "RXHDR")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "RXERROR")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.TxProtection, new DoubleWordRegister(this)
                    .WithReservedBits(0, 21)
                    .WithTaggedFlag("UNPROTECTED", 21)
                    .WithTaggedFlag("PRIVILGED", 22)
                    .WithTaggedFlag("NONSECURE", 23)
                    .WithTag("USER", 24, 8)
                },
                {(long)Registers.RxProtection, new DoubleWordRegister(this)
                    .WithReservedBits(0, 21)
                    .WithTaggedFlag("UNPROTECTED", 21)
                    .WithTaggedFlag("PRIVILGED", 22)
                    .WithTaggedFlag("NONSECURE", 23)
                    .WithTag("USER", 24, 8)
                },
                {(long)Registers.TxHeader, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => { TxHeader = (uint)value; }, name: "TXHEADER")
                },
                {(long)Registers.RxHeader, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)RxHeader, name: "RXHEADER")
                },
                {(long)Registers.Config, new DoubleWordRegister(this)
                    .WithFlag(0, out txInterruptEnable, name: "TXINTEN")
                    .WithFlag(1, out rxInterruptEnable, name: "RXINTEN")
                    .WithReservedBits(2, 30)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
            };

            var startOffset = (long)Registers.Fifo0;
            var blockSize = (long)Registers.Fifo1 - (long)Registers.Fifo0;

            for(var index = 0; index < FifoWordSize; index++)
            {
                var i = index;
                
                registerDictionary.Add(startOffset + blockSize*i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, valueProviderCallback: _ => RxFifoDequeue(), writeCallback: (_, value) => { TxFifoEnqueue((uint)value); }, name: $"FIFO{i}")
                );
            }
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO RxIRQ { get; }
        public GPIO TxIRQ { get; }

#region fields
        protected readonly Machine machine;
        protected readonly DoubleWordRegisterCollection registersCollection;
        private readonly Silabs_SecureElement Silabs_SecureElement;
        private const uint FifoWordSize = 16;
        // TODO: according to the design book, TXSTATUS.TXINT field: "Interrupt status (same value as interrupt signal). 
        // High when TX FIFO is not almost-full (enough available space to start sending a message)."
        // As of now I don't know what "enough available space to send a message" means, so for now I assume a message
        // needs the whole FIFO.
        private const uint TxFifoAlmostFullThreshold = 1;
        private Queue<uint> txFifo;
        private Queue<uint> rxFifo;
        private int TxFifoWordsCount => txFifo.Count;
        private int RxFifoWordsCount => rxFifo.Count;
        private bool TxFifoIsAlmostFull => (TxFifoWordsCount >= TxFifoAlmostFullThreshold);
        private bool TxFifoIsFull => (TxFifoWordsCount == FifoWordSize);
        private bool RxFifoIsFull => (RxFifoWordsCount == FifoWordSize);
        private bool TxFifoIsEmpty => (TxFifoWordsCount == 0);
        private bool RxFifoIsEmpty => (RxFifoWordsCount == 0);
        private bool rxHeaderAvailable = false;
        private IFlagRegisterField txInterruptEnable;
        private IFlagRegisterField rxInterruptEnable;
        private IFlagRegisterField rxInterrupt;

        private uint TxHeader
        {
            set
            {
                Silabs_SecureElement.TxHeaderSetCallback(value);
                TxFifoEnqueue(value);
            }
        }

        private uint RxHeader
        {
            get
            {
                uint retValue; 
                if (RxHeaderAvailable)
                {
                    retValue = RxFifoDequeue();
                    RxHeaderAvailable = false;
                }
                else
                {
                    // Return an error response code in case the RXHEADER is not available.
                    retValue = Silabs_SecureElement.GetDefaultErrorStatus();
                }
                rxInterrupt.Value = false;
                UpdateInterrupts();
                return retValue;
            }
        }

        private bool RxHeaderAvailable
        {
            get
            {
                return rxHeaderAvailable;
            }

            set
            {
                rxHeaderAvailable = value;
            }
        }
#endregion

#region system methods
        private void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate {
                // TXINT: Interrupt status (same value as interrupt signal). 
                // High when TX FIFO is not almost-full (enough available space to start sending a message).
                var irq = txInterruptEnable.Value && !TxFifoIsAlmostFull;
                if (irq)
                {
                    this.Log(LogLevel.Noisy, "IRQ TX set");
                }
                TxIRQ.Set(irq);

                // RXINT: Interrupt status (same value as interrupt signal). High when RX FIFO is not almost-empty 
                // or when the end of the message is ready in the FIFO (enough data available to start reading).
                irq = rxInterruptEnable.Value && rxInterrupt.Value;
                if (irq)
                {
                    this.Log(LogLevel.Noisy, "IRQ RX set");
                }
                RxIRQ.Set(irq);
            });
        }

        private void TxFifoEnqueue(uint value)
        {
            if (!TxFifoIsFull)
            {
                txFifo.Enqueue(value);

                // If true, a command was processed and a response was added to the RX queue.
                if (Silabs_SecureElement.TxFifoEnqueueCallback(value))
                {
                    rxInterrupt.Value = true;
                    RxHeaderAvailable = true;
                }

                UpdateInterrupts();
            }
            else
            {
                this.Log(LogLevel.Error, "TxFifoEnqueue(): queue is FULL!");
            }
        }

        private uint TxFifoDequeue()
        {
            uint ret = 0;

            if (!TxFifoIsEmpty)
            {
                ret = txFifo.Dequeue();
                this.Log(LogLevel.Info, "TxFifo Dequeued: {0:X}", ret);
                UpdateInterrupts();
            }
            else
            {
                this.Log(LogLevel.Error, "TxFifoDequeue(): queue is EMPTY!");
            }

            return ret;
        }

        private void RxFifoEnqueue(uint value)
        {
            if (!RxFifoIsFull)
            {
                rxFifo.Enqueue(value);
            }
            else
            {
                this.Log(LogLevel.Error, "RxFifoEnqueue(): queue is FULL!");
            }
        }

        private uint RxFifoDequeue()
        {
            uint ret = 0;

            if (!RxFifoIsEmpty)
            {
                ret = rxFifo.Dequeue();
                this.Log(LogLevel.Info, "RxFifo Dequeued: {0:X}", ret);
            }
            else
            {
                this.Log(LogLevel.Error, "RxFifoDequeue(): queue is EMPTY!");
            }

            return ret;
        }
#endregion

#region enums
        private enum Registers
        {
            Fifo0           = 0x00,
            Fifo1           = 0x04,
            Fifo2           = 0x08,
            Fifo3           = 0x0C,
            Fifo4           = 0x10,
            Fifo5           = 0x14,
            Fifo6           = 0x18,
            Fifo7           = 0x1C,
            Fifo8           = 0x20,
            Fifo9           = 0x24,
            Fifo10          = 0x28,
            Fifo11          = 0x2C,
            Fifo12          = 0x30,
            Fifo13          = 0x34,
            Fifo14          = 0x38,
            Fifo15          = 0x3C,
            TxStatus        = 0x40,
            RxStatus        = 0x44,
            TxProtection    = 0x48,
            RxProtection    = 0x4C,
            TxHeader        = 0x50,
            RxHeader        = 0x54,
            Config          = 0x58,
        }
#endregion        
    }
}