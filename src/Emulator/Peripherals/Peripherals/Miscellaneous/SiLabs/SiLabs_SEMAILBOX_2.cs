//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_SEMAILBOX_2 : SiLabsPeripheral
    {
        public SiLabs_SEMAILBOX_2(Machine machine, uint flashSize, uint flashPageSize, uint flashRegionSize,
                                  uint flashCodeRegionStart, uint flashCodeRegionEnd, uint flashDataRegionStart,
                                  SiLabs_IKeyStorage ksu) : base(machine)
        {
            txFifo = new Queue<uint>();
            rxFifo = new Queue<uint>();

            RxIRQ = new GPIO();
            TxIRQ = new GPIO();

            secureElement = new SiLabs_SecureElement(machine, this, txFifo, rxFifo, true, flashSize, flashPageSize, flashRegionSize,
                                                     flashCodeRegionStart, flashCodeRegionEnd, flashDataRegionStart, ksu);
        }

        public override void Reset()
        {
            base.Reset();

            txFifo.Clear();
            rxFifo.Clear();
            secureElement.Reset();
            rxHeaderAvailable = false;
        }

        public GPIO RxIRQ { get; }

        public GPIO TxIRQ { get; }

        protected override void UpdateInterrupts()
        {
            machine.ClockSource.ExecuteInLock(delegate
            {
                // TXINT: Interrupt status (same value as interrupt signal). 
                // High when TX FIFO is not almost-full (enough available space to start sending a message).
                var irq = txInterruptEnable.Value && !TxFifoIsAlmostFull;
                if(irq)
                {
                    this.Log(LogLevel.Noisy, "IRQ TX set");
                }
                TxIRQ.Set(irq);

                // RXINT: Interrupt status (same value as interrupt signal). High when RX FIFO is not almost-empty 
                // or when the end of the message is ready in the FIFO (enough data available to start reading).
                irq = rxInterruptEnable.Value && rxInterrupt.Value;
                if(irq)
                {
                    this.Log(LogLevel.Noisy, "IRQ RX set");
                }
                RxIRQ.Set(irq);
            });
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
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
                    .WithTaggedFlag("UNPROTECTED", 24)
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.RxStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)RxFifoWordsCount, name: "REMBYTES")
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "MSGINFO")
                    .WithFlag(20, out rxInterrupt, FieldMode.Read, name: "RXINT")
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => RxFifoIsEmpty, name: "RXEMPTY")
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => RxHeaderAvailable, name: "RXHDR")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "RXERROR")
                    .WithTaggedFlag("UNPROTECTED", 24)
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.TxProtection, new DoubleWordRegister(this)
                    .WithTag("USER", 0, 30)
                    .WithTaggedFlag("PRIVILEGED", 30)
                    .WithTaggedFlag("NONSECURE", 31)
                },
                {(long)Registers.RxProtection, new DoubleWordRegister(this)
                    .WithTag("USER", 0, 30)
                    .WithTaggedFlag("PRIVILEGED", 30)
                    .WithTaggedFlag("NONSECURE", 31)
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
                registerDictionary.Add(startOffset + blockSize * i,
                    new DoubleWordRegister(this)
                        .WithValueField(0, 32, valueProviderCallback: _ => RxFifoDequeue(), writeCallback: (_, value) => { TxFifoEnqueue((uint)value); }, name: $"FIFO{i}")
                );
            }
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        private uint TxFifoDequeue()
        {
            uint ret = 0;

            if(!TxFifoIsEmpty)
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

        private void TxFifoEnqueue(uint value)
        {
            if(!TxFifoIsFull)
            {
                txFifo.Enqueue(value);

                // If true, a command was processed and a response was added to the RX queue.
                if(secureElement.TxFifoEnqueueCallback(value))
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

        private void RxFifoEnqueue(uint value)
        {
            if(!RxFifoIsFull)
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

            if(!RxFifoIsEmpty)
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

        private bool RxFifoIsFull => (RxFifoWordsCount == FifoWordSize);

        private bool TxFifoIsEmpty => (TxFifoWordsCount == 0);

        private bool RxFifoIsEmpty => (RxFifoWordsCount == 0);

        private bool TxFifoIsAlmostFull => (TxFifoWordsCount >= TxFifoAlmostFullThreshold);

        private uint TxHeader
        {
            set
            {
                secureElement.TxHeaderSetCallback(value);
                TxFifoEnqueue(value);
            }
        }

        private uint RxHeader
        {
            get
            {
                uint retValue;
                if(RxHeaderAvailable)
                {
                    retValue = RxFifoDequeue();
                    RxHeaderAvailable = false;
                }
                else
                {
                    // Return an error response code in case the RXHEADER is not available.
                    retValue = secureElement.GetDefaultErrorStatus();
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

        private int RxFifoWordsCount => rxFifo.Count;

        private int TxFifoWordsCount => txFifo.Count;

        private bool TxFifoIsFull => (TxFifoWordsCount == FifoWordSize);

        private IFlagRegisterField txInterruptEnable;
        private IFlagRegisterField rxInterruptEnable;
        private IFlagRegisterField rxInterrupt;
        private bool rxHeaderAvailable = false;
        private readonly Queue<uint> rxFifo;
        private readonly Queue<uint> txFifo;
        private readonly SiLabs_SecureElement secureElement;
        private const uint FifoWordSize = 16;
        // TODO: according to the design book, TXSTATUS.TXINT field: "Interrupt status (same value as interrupt signal). 
        // High when TX FIFO is not almost-full (enough available space to start sending a message)."
        // As of now I don't know what "enough available space to send a message" means, so for now I assume a message
        // needs the whole FIFO.
        private const uint TxFifoAlmostFullThreshold = 1;

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
    }
}