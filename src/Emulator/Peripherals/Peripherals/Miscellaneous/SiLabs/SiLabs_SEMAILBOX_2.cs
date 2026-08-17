//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
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
        // Constructor for SecureElementEmulated mode
        public SiLabs_SEMAILBOX_2(Machine machine, uint flashSize, uint flashPageSize, uint flashRegionSize,
                                  uint flashCodeRegionStart, uint flashCodeRegionEnd, uint flashDataRegionStart,
                                  ISiLabs_KeyStorage ksu, SiLabs_SEMAILBOX_2 peerMailbox = null)
            : this(machine, MailboxMode.SecureElementEmulated, flashSize, flashPageSize, flashRegionSize,
                   flashCodeRegionStart, flashCodeRegionEnd, flashDataRegionStart, ksu, isHostSide: true, peerMailbox)
        {
        }

        // Constructor for SecureElementFull mode (SE side)
        public SiLabs_SEMAILBOX_2(Machine machine, bool isHostSide = false, SiLabs_SEMAILBOX_2 peerMailbox = null)
            : this(machine, MailboxMode.SecureElementFull, 0, 0, 0, 0, 0, 0, null, isHostSide, peerMailbox)
        {
        }

        public override void Reset()
        {
            base.Reset();

            if(mode == MailboxMode.SecureElementEmulated)
            {
                txFifo.Clear();
                rxFifo.Clear();
                emulatedSecureElement.Reset();
                rxHeaderAvailable = false;
            }
            else // SecureElementFull mode
            {
                txFifo.Clear();
                rxFifo.Clear();
                expectedRxMessageSize = 0;
                rxMessageSizeSet = false;
                rxHeaderAvailable = false;
            }
        }

        public int GetTxFifoCountForPeer()
        {
            if(mode != MailboxMode.SecureElementFull)
            {
                return 0;
            }

            lock(fifoLock)
            {
                return txFifo.Count;
            }
        }

        public bool TryDequeueTxForPeer(out uint value)
        {
            if(mode != MailboxMode.SecureElementFull)
            {
                value = 0;
                return false;
            }

            lock(fifoLock)
            {
                if(txFifo.Count == 0)
                {
                    value = 0;
                    return false;
                }

                value = txFifo.Dequeue();
                UpdateInterruptsInternal();
                return true;
            }
        }

        /// <summary>
        /// Called by peer mailbox when it enqueues data to our RX FIFO (SecureElementFull mode only).
        /// This allows tracking message size from the header.
        /// </summary>
        public void NotifyRxFifoEnqueue(uint value)
        {
            if(mode == MailboxMode.SecureElementFull)
            {
                UpdateRxTrackingOnPeerEnqueue(value);
            }
        }

        /// <summary>
        /// Public method to update interrupts (called by peer mailbox in SecureElementFull mode).
        /// </summary>
        public void UpdateInterruptsInternal()
        {
            UpdateInterrupts();
        }

        public GPIO RxIRQ { get; }

        public GPIO TxIRQ { get; }

        protected override void UpdateInterrupts()
        {
            // Prevent recursive calls that could cause infinite loops
            if(updatingInterrupts)
            {
                return;
            }

            updatingInterrupts = true;
            try
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
                    if(mode == MailboxMode.SecureElementEmulated)
                    {
                        irq = rxInterruptEnable.Value && rxInterrupt.Value;
                    }
                    else // SecureElementFull mode
                    {
                        // In SecureElementFull mode, RX interrupt logic is handled by IsRxInterruptCondition()
                        // which checks appropriate conditions for both Host and SE sides
                        var interruptConditionMet = IsRxInterruptCondition();
                        irq = rxInterruptEnable.Value && interruptConditionMet;
                    }

                    if(irq)
                    {
                        this.Log(LogLevel.Noisy, "IRQ RX set");
                    }
                    RxIRQ.Set(irq);
                });
            }
            finally
            {
                updatingInterrupts = false;
            }
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
                    .WithFlag(24, name: "UNPROTECTED")
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.RxStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)RxFifoWordsCount, name: "REMBYTES")
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => 0 /* TODO */, name: "MSGINFO")
                    .WithFlag(20, out rxInterrupt, FieldMode.Read, name: "RXINT")
                    .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => RxFifoIsEmpty, name: "RXEMPTY")
                    .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => (mode == MailboxMode.SecureElementFull && !isHostSide) ? false : RxHeaderAvailable, name: "RXHDR")
                    .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false /* TODO */, name: "RXERROR")
                    .WithFlag(24, name: "UNPROTECTED")
                    .WithReservedBits(25, 7)
                },
                {(long)Registers.TxProtection, new DoubleWordRegister(this)
                    .WithValueField(0, 30, name: "USER")
                    .WithFlag(30, name: "PRIVILEGED")
                    .WithFlag(31, name: "NONSECURE")
                },
                {(long)Registers.RxProtection, new DoubleWordRegister(this)
                    .WithValueField(0, 30, name: "USER")
                    .WithFlag(30, name: "PRIVILEGED")
                    .WithFlag(31, name: "NONSECURE")
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
                        .WithValueField(0, 32,
                            valueProviderCallback: _ => RxFifoDequeue(),
                            writeCallback: (_, value) => { TxFifoEnqueue((uint)value); },
                            name: $"FIFO{i}")
                );
            }
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        // Unified constructor
        private SiLabs_SEMAILBOX_2(Machine machine, MailboxMode mode, uint flashSize, uint flashPageSize, uint flashRegionSize,
                                   uint flashCodeRegionStart, uint flashCodeRegionEnd, uint flashDataRegionStart,
                                   ISiLabs_KeyStorage ksu, bool isHostSide, SiLabs_SEMAILBOX_2 peerMailbox = null) : base(machine)
        {
            this.mode = mode;

            if(mode == MailboxMode.SecureElementEmulated)
            {
                // SecureElementEmulated mode: use local FIFOs
                txFifo = new Queue<uint>();
                rxFifo = new Queue<uint>();
                this.isHostSide = true; // Not used in SecureElementEmulated mode

                emulatedSecureElement = new SiLabs_SecureElement(machine, this, txFifo, rxFifo, true, flashSize, flashPageSize, flashRegionSize,
                                                                 flashCodeRegionStart, flashCodeRegionEnd, flashDataRegionStart, ksu);
            }
            else // SecureElementFull mode
            {
                this.isHostSide = isHostSide;
                txFifo = new Queue<uint>();
                rxFifo = new Queue<uint>();
                emulatedSecureElement = null; // Not used in SecureElementFull mode
            }

            RxIRQ = new GPIO();
            TxIRQ = new GPIO();

            // Handle peerMailbox if provided
            if(peerMailbox != null)
            {
                if(mode == MailboxMode.SecureElementEmulated)
                {
                    SwitchToSecureElementFull();
                }
                SetPeerMailbox(peerMailbox);
            }
        }

        private void SwitchToSecureElementFull()
        {
            mode = MailboxMode.SecureElementFull;
            isHostSide = true;

            lock(fifoLock)
            {
                txFifo.Clear();
                rxFifo.Clear();
            }

            expectedRxMessageSize = 0;
            rxMessageSizeSet = false;
            rxHeaderAvailable = false;
            UpdateInterrupts();
        }

        private void SetPeerMailbox(SiLabs_SEMAILBOX_2 value)
        {
            if(_peerMailbox == value)
            {
                return;
            }

            _peerMailbox = value;

            if(_peerMailbox != null && _peerMailbox._peerMailbox != this)
            {
                _peerMailbox.SetPeerMailbox(this);
            }
        }

        /// <summary>
        /// Check if RX interrupt condition is met (SecureElementFull mode only).
        /// Handles both Host and SE sides with appropriate logic.
        /// </summary>
        private bool IsRxInterruptCondition()
        {
            if(mode != MailboxMode.SecureElementFull)
            {
                return true; // SecureElementEmulated mode uses rxInterrupt.Value directly
            }

            var count = RxFifoWordsCount;
            var messageInProgress = count > 0;  // REMBYTES is not zero

            if(isHostSide)
            {
                // Host RX interrupt requires:
                // - Threshold condition: >= 4 words available
                // - Message complete: rxInterrupt.Value is true
                // - Header available: RXHDR bit is set (rxHeaderAvailable)
                var thresholdMet = count >= HostRxFifoInterruptThreshold;
                var messageComplete = rxInterrupt.Value;
                var headerAvailable = RxHeaderAvailable;
                return messageInProgress && thresholdMet && messageComplete && headerAvailable;
            }
            else
            {
                // SE RX interrupt: RXINT bit is high when:
                // - At least 16 words are available in the RX FIFO, OR
                // - The end of the message is available (word count matches header-encoded size)
                // The "byte remaining" field (REMBYTES) is not zero when a message is being received
                // The header (bits 15-0) encodes the message size in bytes (must be multiple of 4 bytes)
                var fifoThresholdMet = count >= FifoWordSize;  // At least 16 words available
                var messageComplete = rxInterrupt.Value;
                return messageInProgress && (fifoThresholdMet || messageComplete);
            }
        }

        private uint TxFifoDequeue()
        {
            lock(fifoLock)
            {
                uint ret = 0;

                if(!TxFifoIsEmpty)
                {
                    ret = txFifo.Dequeue();
                    this.Log(LogLevel.Debug, "TxFifo Dequeued: 0x{0:X}", ret);
                    UpdateInterrupts();
                }
                else
                {
                    this.Log(LogLevel.Error, "TxFifoDequeue(): queue is EMPTY!");
                }

                return ret;
            }
        }

        private void TxFifoEnqueue(uint value)
        {
            if(mode == MailboxMode.SecureElementEmulated)
            {
                if(!TxFifoIsFull)
                {
                    txFifo.Enqueue(value);

                    // If true, a command was processed and a response was added to the RX queue.
                    if(emulatedSecureElement.TxFifoEnqueueCallback(value))
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
            else // SecureElementFull mode
            {
                lock(fifoLock)
                {
                    if(!TxFifoIsFull)
                    {
                        txFifo.Enqueue(value);

                        // Notify peer mailbox that data was enqueued to its RX FIFO
                        _peerMailbox?.NotifyRxFifoEnqueue(value);

                        UpdateInterruptsInternal();
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "TxFifoEnqueue(): queue is FULL!");
                    }
                }
            }
        }

        private void RxFifoEnqueue(uint value)
        {
            if(mode == MailboxMode.SecureElementEmulated)
            {
                lock(fifoLock)
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
            }
            else // SecureElementFull mode
            {
                UpdateRxTrackingOnPeerEnqueue(value);
            }
        }

        /// <summary>
        /// Update RX interrupt status register (SecureElementFull mode only).
        /// The rxInterrupt register is set when the received word count (including header) matches 
        /// the size encoded in the header (bits 0-15, in bytes, converted to words).
        /// </summary>
        private void UpdateRxInterrupt()
        {
            if(mode != MailboxMode.SecureElementFull)
            {
                return; // Only used in SecureElementFull mode
            }

            var wordCount = RxFifoWordsCount;

            if(wordCount == 0)
            {
                // Clear interrupt and reset message tracking when FIFO is empty
                rxInterrupt.Value = false;
                rxMessageSizeSet = false;
                expectedRxMessageSize = 0;
            }
            else if(rxMessageSizeSet && wordCount == expectedRxMessageSize)
            {
                // Trigger interrupt when received size matches expected size from header
                rxInterrupt.Value = true;
                rxHeaderAvailable = true;
                this.DebugLog("RX: Complete message received ({0} words)", wordCount);
            }
            else if(!rxMessageSizeSet)
            {
                // Fallback: if header not yet processed, use threshold-based interrupt
                // Host side: >= 4 words, SE side: >= 16 words (FIFO full)
                var threshold = isHostSide ? HostRxFifoInterruptThreshold : FifoWordSize;
                if(wordCount >= threshold)
                {
                    rxInterrupt.Value = true;
                }
            }

            UpdateInterruptsInternal();
        }

        private void UpdateRxTrackingOnPeerEnqueue(uint value)
        {
            if(mode != MailboxMode.SecureElementFull)
            {
                return;
            }

            var currentCount = RxFifoWordsCount;

            // Check if this is the first word (header) to extract message size
            if(!rxMessageSizeSet && currentCount == 1)
            {
                // Extract size from header bits 0-15 (in bytes, including header)
                // Convert bytes to words (each word is 4 bytes)
                var sizeInBytes = value & 0xFFFF;
                expectedRxMessageSize = (sizeInBytes + 3) / 4; // Round up to nearest word
                rxMessageSizeSet = true;
                this.DebugLog("RX: Header received, message size: {0} bytes ({1} words)", sizeInBytes, expectedRxMessageSize);
            }

            // Update interrupt status: trigger when received size matches expected size
            UpdateRxInterrupt();
        }

        private uint RxFifoDequeue()
        {
            if(mode == MailboxMode.SecureElementEmulated)
            {
                uint ret = 0;

                lock(fifoLock)
                {
                    if(!RxFifoIsEmpty)
                    {
                        ret = rxFifo.Dequeue();
                        this.Log(LogLevel.Debug, "RxFifo Dequeued: 0x{0:X}", ret);
                    }
                    else
                    {
                        this.Log(LogLevel.Error, "RxFifoDequeue(): queue is EMPTY!");
                    }
                }

                return ret;
            }
            else // SecureElementFull mode
            {
                if(_peerMailbox == null)
                {
                    this.Log(LogLevel.Error, "RxFifoDequeue(): peer mailbox is not set!");
                    return 0;
                }

                uint ret;
                if(_peerMailbox.TryDequeueTxForPeer(out ret))
                {
                    // Reset message tracking when FIFO becomes empty
                    if(RxFifoWordsCount == 0)
                    {
                        rxMessageSizeSet = false;
                        expectedRxMessageSize = 0;
                    }

                    // Update interrupts on both sides after dequeue
                    UpdateRxInterrupt();
                    _peerMailbox.UpdateInterruptsInternal();
                }
                else
                {
                    this.Log(LogLevel.Error, "RxFifoDequeue(): queue is EMPTY!");
                    ret = 0;
                }

                return ret;
            }
        }

        private bool RxFifoIsFull => (RxFifoWordsCount == FifoWordSize);

        private bool TxFifoIsEmpty => (TxFifoWordsCount == 0);

        private bool RxFifoIsEmpty => (RxFifoWordsCount == 0);

        private bool TxFifoIsAlmostFull => (TxFifoWordsCount >= TxFifoAlmostFullThreshold);

        private uint TxHeader
        {
            set
            {
                if(mode == MailboxMode.SecureElementEmulated)
                {
                    emulatedSecureElement.TxHeaderSetCallback(value);
                    TxFifoEnqueue(value);
                }
                else // SecureElementFull mode
                {
                    // In SecureElementFull mode, header is just enqueued to TX FIFO
                    TxFifoEnqueue(value);
                }
            }
        }

        private uint RxHeader
        {
            get
            {
                if(mode == MailboxMode.SecureElementEmulated)
                {
                    uint retValue;
                    if(!RxFifoIsEmpty)
                    {
                        retValue = RxFifoDequeue();
                        if(RxFifoIsEmpty)
                        {
                            RxHeaderAvailable = false;
                        }
                    }
                    else
                    {
                        // Return an error response code when there is no RX data.
                        retValue = emulatedSecureElement.GetDefaultErrorStatus();
                    }
                    rxInterrupt.Value = false;
                    UpdateInterrupts();
                    return retValue;
                }
                else // SecureElementFull mode
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
                        retValue = 0xFFFFFFFF;
                    }
                    // Clear interrupt and update based on remaining words
                    rxInterrupt.Value = false;
                    UpdateRxInterrupt();
                    return retValue;
                }
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

        private int RxFifoWordsCount
        {
            get
            {
                if(mode == MailboxMode.SecureElementEmulated)
                {
                    return rxFifo.Count;
                }
                else // SecureElementFull mode
                {
                    return _peerMailbox?.GetTxFifoCountForPeer() ?? 0;
                }
            }
        }

        private int TxFifoWordsCount
        {
            get
            {
                return txFifo.Count;
            }
        }

        private bool TxFifoIsFull => (TxFifoWordsCount == FifoWordSize);

        private SiLabs_SEMAILBOX_2 _peerMailbox; // Reference to peer mailbox for interrupt coordination
        private uint expectedRxMessageSize = 0;  // Expected message size from header (bits 0-15)
        private bool rxMessageSizeSet = false;   // Whether we've extracted the size from the header
        private bool isHostSide; // true for Host side, false for SE side (SecureElementFull mode only)
        private bool updatingInterrupts = false; // Guard flag to prevent recursive interrupt updates

        private MailboxMode mode;

        private IFlagRegisterField txInterruptEnable;
        private IFlagRegisterField rxInterruptEnable;
        private IFlagRegisterField rxInterrupt;
        private bool rxHeaderAvailable = false;

        private readonly Queue<uint> rxFifo;
        private readonly Queue<uint> txFifo;
        private readonly SiLabs_SecureElement emulatedSecureElement;
        private readonly object fifoLock = new object();
        private const uint FifoWordSize = 16;
        private const uint HostRxFifoInterruptThreshold = 4;  // Threshold for RX interrupt in SecureElementFull mode (Host side)
        // TODO: according to the design book, TXSTATUS.TXINT field: "Interrupt status (same value as interrupt signal). 
        // High when TX FIFO is not almost-full (enough available space to start sending a message)."
        // As of now I don't know what "enough available space to send a message" means, so for now I assume a message
        // needs the whole FIFO.
        private const uint TxFifoAlmostFullThreshold = 1;

        private enum MailboxMode
        {
            /// <summary>
            /// Secure Element Emulated mode: Uses local FIFOs and SiLabs_SecureElement for command processing.
            /// This is the default mode for Host-side mailboxes when no SE is present.
            /// </summary>
            SecureElementEmulated,

            /// <summary>
            /// Secure Element Full mode: Uses peer mailboxes to exchange FIFO data between the Host and the SE.
            /// </summary>
            SecureElementFull
        }

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
