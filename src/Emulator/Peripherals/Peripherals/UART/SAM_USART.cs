//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.DMA;

namespace Antmicro.Renode.Peripherals.UART
{
    public class SAM_USART : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize,
        ISamPdcBytePeripheral
    {
        public SAM_USART(IMachine machine, bool uartOnlyMode = false, bool enablePdc = false) : base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters(uartOnlyMode);
            pdc = enablePdc ? new SAM_PDC(machine, this, (long)Registers.PdcReceivePointer, UpdateInterrupts) : null;
            Size = enablePdc ? 0x128 : 0x100;
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            pdc?.Reset();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(innerLock)
            {
                return RegistersCollection.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(innerLock)
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public byte? DmaByteRead() => ReadBuffer();
        public void DmaByteWrite(byte data) => Transmit(data);

        public long Size { get; }
        public GPIO IRQ { get; }
        public DoubleWordRegisterCollection RegistersCollection { get; }

        public TransferType DmaReadAccessWidth => TransferType.Byte;
        public TransferType DmaWriteAccessWidth => TransferType.Byte;

        public override uint BaudRate => 115200;

        public override Bits StopBits
        {
            get
            {
                switch(numberOfStopBits.Value)
                {
                case NumberOfStopBitsValues.One:
                    return Bits.One;
                case NumberOfStopBitsValues.Half:
                    return Bits.Half;
                case NumberOfStopBitsValues.Two:
                    return Bits.Two;
                case NumberOfStopBitsValues.OneAndAHalf:
                    return Bits.OneAndAHalf;
                default:
                    throw new ArgumentException("Invalid number of stop bits");
                }
            }
        }

        public override Parity ParityBit
        {
            get
            {
                switch(parityType.Value)
                {
                case ParityTypeValues.Even:
                    return Parity.Even;
                case ParityTypeValues.Odd:
                    return Parity.Odd;
                case ParityTypeValues.Space:
                    return Parity.Forced0;
                case ParityTypeValues.Mark:
                    return Parity.Forced1;
                case ParityTypeValues.No:
                    return Parity.None;
                case ParityTypeValues.Multidrop:
                case ParityTypeValues.AlsoMultidrop:
                    return Parity.Multidrop;
                default:
                    throw new ArgumentException("Invalid parity type");
                }
            }
        }

        protected override void CharWritten()
        {
            receiverReady.Value = true;
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private void DefineRegisters(bool uartOnlyMode)
        {
            // NOTE: Registers are assumed not to be in SPI mode
            Func<IFlagRegisterField, Action<bool, bool>> writeOneToClearFlag = flag => (_, value) =>
            {
                if(value)
                {
                    flag.Value = false;
                }
            };

            Registers.Control.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, out var resetReceiver, FieldMode.Write, name: "RSTRX")
                .WithFlag(3, out var resetTransmitter, FieldMode.Write, name: "RSTTX")
                .WithFlag(4, out var enableReceiver, FieldMode.Write, name: "RXEN")
                .WithFlag(5, out var disableReceiver, FieldMode.Write, name: "RXDIS")
                .WithFlag(6, out var enableTransmitter, FieldMode.Write, name: "TXEN")
                .WithFlag(7, out var disableTransmitter, FieldMode.Write, name: "TXDIS")
                .WithTaggedFlag("RSTSTA", 8)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(9, 11)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("STTBRK", 9)
                        .WithTaggedFlag("STPBRK", 10)
                        .WithTaggedFlag("STTTO", 11)
                        .WithTaggedFlag("SENDA", 12)
                        .WithTaggedFlag("RSTIT", 13)
                        .WithTaggedFlag("RSTNACK", 14)
                        .WithTaggedFlag("RETTO", 15)
                        .WithTaggedFlag("DTREN", 16)
                        .WithTaggedFlag("DTRDIS", 17)
                        .WithTaggedFlag("RTSEN", 18)
                        .WithTaggedFlag("RTSDIS", 19)
                    )
                .WithReservedBits(20, 12)
                .WithWriteCallback((_, __) =>
                {
                    if(resetReceiver.Value)
                    {
                        /* Clear FIFO */
                        ClearBuffer();
                        receiverEnabled = false;
                    }

                    /* Determine what to do with the Receiver */
                    if(disableReceiver.Value)
                    {
                        receiverEnabled = false;
                    }
                    else if(enableReceiver.Value)
                    {
                        receiverEnabled = true;
                    }

                    /* Determine what to do with the Transmitter */
                    if(disableTransmitter.Value || (resetTransmitter.Value && !enableTransmitter.Value))
                    {
                        transmitterEnabled = false;
                    }
                    else if(enableTransmitter.Value)
                    {
                        transmitterEnabled = true;
                    }
                })
            ;

            Registers.Mode.Define(this)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(0, 9)
                    )
                    .Else(reg => reg
                        .WithValueField(0, 4, valueProviderCallback: _ => 0, writeCallback: (_, value) =>
                        {
                            if(value != 0)
                            {
                                this.Log(LogLevel.Warning, "Trying to configure the device to an unsupported mode!");
                            }
                        }, name: "USART_MODE")
                        .WithTag("USCLKS", 4, 2)
                        .WithTag("CHRL", 6, 2)
                        .WithTaggedFlag("SYNC", 8)
                    )
                .WithEnumField(9, 3, out parityType, name: "PAR")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(12, 2)
                    )
                    .Else(reg => reg
                        .WithEnumField(12, 2, out numberOfStopBits, name: "NBSTOP")
                    )
                .WithTag("CHMODE", 14, 2)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(16, 16)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("MSBF", 16)
                        .WithTaggedFlag("MODE9", 17)
                        .WithTaggedFlag("CLKO", 18)
                        .WithTaggedFlag("OVER", 19)
                        .WithTaggedFlag("INACK", 20)
                        .WithTaggedFlag("DSNACK", 21)
                        .WithTaggedFlag("VAR_SYNC", 22)
                        .WithTaggedFlag("INVDATA", 23)
                        .WithTag("MAX_ITERATION", 24, 3)
                        .WithReservedBits(27, 1)
                        .WithTaggedFlag("FILTER", 28)
                        .WithTaggedFlag("MAN", 29)
                        .WithTaggedFlag("MODSYNC", 30)
                        .WithTaggedFlag("ONEBIT", 31)
                    )
            ;

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out receiverReadyIrqEnabled, FieldMode.Set, name: "IER_RXRDY")
                .WithFlag(1, out transmitterReadyIrqEnabled, FieldMode.Set, name: "IER_TXRDY")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(2, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("RXBRK", 2)
                    )
                .WithFlag(3, out endOfRxBufferIrqEnabled, FieldMode.Set, name: "ENDRX")
                .WithFlag(4, out endOfTxBufferIrqEnabled, FieldMode.Set, name: "ENDTX")
                .WithTaggedFlag("OVRE", 5)
                .WithTaggedFlag("FRAME", 6)
                .WithTaggedFlag("PARE", 7)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(8, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("TIMEOUT", 8)
                    )
                .WithTaggedFlag("TXEMPTY", 9)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(10, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("ITER", 10)
                    )
                .WithFlag(11, out txBufferEmptyIrqEnabled, FieldMode.Set, name: "TXBUFE")
                .WithFlag(12, out rxBufferFullIrqEnabled, FieldMode.Set, name: "RXBUFF")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(13, 12)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("NACK", 13)
                        .WithReservedBits(14, 2)
                        .WithTaggedFlag("RIIC", 16)
                        .WithTaggedFlag("DSRIC", 17)
                        .WithTaggedFlag("DCDIC", 18)
                        .WithTaggedFlag("CTSIC", 19)
                        .WithReservedBits(20, 4)
                        .WithTaggedFlag("MANE", 24)
                    )
                .WithReservedBits(25, 7)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptDisable.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: writeOneToClearFlag(receiverReadyIrqEnabled), name: "IDR_RXRDY")
                .WithFlag(1, FieldMode.Write, writeCallback: writeOneToClearFlag(transmitterReadyIrqEnabled), name: "IDR_TXRDY")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(2, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("RXBRK", 2)
                    )
                .WithFlag(3, FieldMode.Write, writeCallback: writeOneToClearFlag(endOfRxBufferIrqEnabled), name: "ENDRX")
                .WithFlag(4, FieldMode.Write, writeCallback: writeOneToClearFlag(endOfTxBufferIrqEnabled), name: "ENDTX")
                .WithTaggedFlag("OVRE", 5)
                .WithTaggedFlag("FRAME", 6)
                .WithTaggedFlag("PARE", 7)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(8, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("TIMEOUT", 8)
                    )
                .WithTaggedFlag("TXEMPTY", 9)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(10, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("ITER", 10)
                    )
                .WithFlag(11, FieldMode.Write, writeCallback: writeOneToClearFlag(txBufferEmptyIrqEnabled), name: "TXBUFE")
                .WithFlag(12, FieldMode.Write, writeCallback: writeOneToClearFlag(rxBufferFullIrqEnabled), name: "RXBUFF")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(13, 12)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("NACK", 13)
                        .WithReservedBits(14, 2)
                        .WithTaggedFlag("RIIC", 16)
                        .WithTaggedFlag("DSRIC", 17)
                        .WithTaggedFlag("DCDIC", 18)
                        .WithTaggedFlag("CTSIC", 19)
                        .WithReservedBits(20, 4)
                        .WithTaggedFlag("MANE", 24)
                    )
                .WithReservedBits(25, 7)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.InterruptMask.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => receiverReadyIrqEnabled.Value, name: "IMR_RXRDY")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => transmitterReadyIrqEnabled.Value, name: "IMR_TXRDY")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(2, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("RXBRK", 2)
                    )
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => endOfRxBufferIrqEnabled.Value, name: "ENDRX")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => endOfTxBufferIrqEnabled.Value, name: "ENDTX")
                .WithTaggedFlag("OVRE", 5)
                .WithTaggedFlag("FRAME", 6)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(8, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("TIMEOUT", 8)
                    )
                .WithTaggedFlag("TXEMPTY", 9)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(10, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("ITER", 10)
                    )
                .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => txBufferEmptyIrqEnabled.Value, name: "TXBUFE")
                .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => rxBufferFullIrqEnabled.Value, name: "RXBUFF")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(13, 12)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("NACK", 13)
                        .WithReservedBits(14, 2)
                        .WithTaggedFlag("RIIC", 16)
                        .WithTaggedFlag("DSRIC", 17)
                        .WithTaggedFlag("DCDIC", 18)
                        .WithTaggedFlag("CTSIC", 19)
                        .WithReservedBits(20, 4)
                        .WithTaggedFlag("MANE", 24)
                    )
                .WithReservedBits(25, 7)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Registers.ChannelStatus.Define(this)
                .WithFlag(0, out receiverReady, FieldMode.Read, name: "RXRDY")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ =>
                {
                    return transmitterEnabled;
                }, name: "TXRDY")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(2, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("RXBRK", 2)
                    )
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => pdc?.EndOfRxBuffer ?? false, name: "ENDRX")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => pdc?.EndOfTxBuffer ?? false, name: "ENDTX")
                .WithTaggedFlag("OVRE", 5)
                .WithTaggedFlag("FRAME", 6)
                .WithTaggedFlag("PARE", 7)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(8, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("TIMEOUT", 8)
                    )
                .WithFlag(9, FieldMode.Read, valueProviderCallback: _ => true, name: "TXEMPTY")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(10, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("ITER", 10)
                    )
                .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => pdc?.TxBufferEmpty ?? false, name: "TXBUFE")
                .WithFlag(12, FieldMode.Read, valueProviderCallback: _ => pdc?.RxBufferFull ?? false, name: "RXBUFF")
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(13, 12)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("NACK", 13)
                        .WithReservedBits(14, 2)
                        .WithTaggedFlag("RIIC", 16)
                        .WithTaggedFlag("DSRIC", 17)
                        .WithTaggedFlag("DCDIC", 18)
                        .WithTaggedFlag("CTSIC", 19)
                        .WithTaggedFlag("RI", 20)
                        .WithTaggedFlag("DSR", 21)
                        .WithTaggedFlag("DCD", 22)
                        .WithTaggedFlag("CTS", 23)
                        .WithTaggedFlag("MANERR", 24)
                    )
                .WithReservedBits(25, 7)
            ;

            Registers.ReceiveHolding.Define(this)
                .WithValueField(0, 9, FieldMode.Read, valueProviderCallback: _ => ReadBuffer() ?? 0x0, name: "RXCHR")
                .WithReservedBits(9, 6)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(15, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("RXSYNH", 15)
                    )
                .WithReservedBits(16, 16)
            ;

            Registers.TransmitHolding.Define(this)
                .WithValueField(0, 9, FieldMode.Write, writeCallback: (_, b) => Transmit((byte)b), name: "TXCHR")
                .WithReservedBits(9, 6)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(15, 1)
                    )
                    .Else(reg => reg
                        .WithTaggedFlag("TXSYNH", 15)
                    )
                .WithReservedBits(16, 16)
            ;

            Registers.BaudRateGenerator.Define(this)
                .WithTag("CD", 0, 16)
                .If(uartOnlyMode)
                    .Then(reg => reg
                        .WithReservedBits(16, 3)
                    )
                    .Else(reg => reg
                        .WithTag("FP", 16, 3)
                    )
                .WithReservedBits(19, 13)
            ;

            if(uartOnlyMode)
            {
                return;
            }

            Registers.ReceiveTimeout.Define(this)
                .WithTag("TO", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.TransmitterTimeguard.Define(this)
                .WithTag("TG", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.FIDIRatio.Define(this)
                .WithTag("FI_DI_RATIO", 0, 11)
                .WithReservedBits(11, 21)
            ;

            Registers.NumberOfErrors.Define(this)
                .WithTag("NB_ERRORS", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.IrDAFilter.Define(this)
                .WithTag("IRDA_FILTER", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.ManchesterConfiguration.Define(this)
                .WithTag("TX_PL", 0, 4)
                .WithReservedBits(4, 4)
                .WithTag("TX_PP", 8, 2)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("TX_MPOL", 12)
                .WithReservedBits(13, 3)
                .WithTag("RX_PL", 16, 4)
                .WithReservedBits(20, 4)
                .WithTag("RX_PP", 24, 2)
                .WithReservedBits(26, 2)
                .WithTaggedFlag("RX_MPOL", 28)
                .WithTaggedFlag("ONE", 29)
                .WithTaggedFlag("DRIFT", 30)
                .WithReservedBits(31, 1)
            ;

            Registers.WriteProtectionMode.Define(this)
                .WithTaggedFlag("WPEN", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPKEY", 8, 24)
            ;

            Registers.WriteProtectionStatus.Define(this)
                .WithTaggedFlag("WPVS", 0)
                .WithReservedBits(1, 7)
                .WithTag("WPVSRC", 8, 16)
                .WithReservedBits(24, 8)
            ;
        }

        private void Transmit(byte data)
        {
            if(!transmitterEnabled)
            {
                return;
            }

            this.TransmitCharacter((byte)data);
            UpdateInterrupts();
        }

        private byte? ReadBuffer()
        {
            if(!receiverEnabled)
            {
                return null;
            }

            if(!TryGetCharacter(out var character))
            {
                this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                return null;
            }
            if(Count == 0)
            {
                receiverReady.Value = false;
            }
            UpdateInterrupts();
            return character;
        }

        private void UpdateInterrupts()
        {
            var state = false;
            state |= receiverEnabled && receiverReadyIrqEnabled.Value && receiverReady.Value;
            state |= transmitterEnabled && transmitterReadyIrqEnabled.Value;
            state |= (pdc?.EndOfRxBuffer ?? false) && endOfRxBufferIrqEnabled.Value;
            state |= (pdc?.EndOfTxBuffer ?? false) && endOfTxBufferIrqEnabled.Value;
            state |= (pdc?.TxBufferEmpty ?? false) && txBufferEmptyIrqEnabled.Value;
            state |= (pdc?.RxBufferFull ?? false) && rxBufferFullIrqEnabled.Value;
            this.DebugLog("IRQ {0}", state ? "set" : "unset");
            IRQ.Set(state);
        }

        private IFlagRegisterField receiverReady;

        private IFlagRegisterField receiverReadyIrqEnabled;
        private IFlagRegisterField transmitterReadyIrqEnabled;
        private IFlagRegisterField endOfRxBufferIrqEnabled;
        private IFlagRegisterField endOfTxBufferIrqEnabled;
        private IFlagRegisterField txBufferEmptyIrqEnabled;
        private IFlagRegisterField rxBufferFullIrqEnabled;

        private IEnumRegisterField<ParityTypeValues> parityType;
        private IEnumRegisterField<NumberOfStopBitsValues> numberOfStopBits;

        private bool receiverEnabled;
        private bool transmitterEnabled;

        private readonly SAM_PDC pdc;

        private enum ParityTypeValues
        {
            Even = 0,
            Odd = 1,
            Space = 2,
            Mark = 3,
            No = 4,
            Multidrop = 7,
            AlsoMultidrop = 6
        }

        private enum NumberOfStopBitsValues
        {
            One = 0,
            Half = 1,
            Two = 2,
            OneAndAHalf = 3
        }

        private enum Registers
        {
            Control = 0x0,
            Mode = 0x04,
            InterruptEnable = 0x08,
            InterruptDisable = 0x0C,
            InterruptMask = 0x10,
            ChannelStatus = 0x14,
            ReceiveHolding = 0x18,
            TransmitHolding = 0x1C,
            BaudRateGenerator = 0x20,
            ReceiveTimeout = 0x24,
            TransmitterTimeguard = 0x28,
            FIDIRatio = 0x40,
            NumberOfErrors = 0x44,
            IrDAFilter = 0x4C,
            ManchesterConfiguration = 0x50,
            LINMode = 0x54,
            LINIdentifier = 0x58,
            LINBaudRate = 0x5C,
            LONMode = 0x60,
            LONPreamble = 0x64,
            LONDataLength = 0x68,
            LONL2HDR = 0x6C,
            LONBacklog = 0x70,
            LONBeta1TX = 0x74,
            LONBeta1RX = 0x78,
            LONPriority = 0x7C,
            LONIntermediateTimeAfterTransmission = 0x80,
            LONIntermediateTimeAfterReception = 0x84,
            ICDifferentiator = 0x88,
            WriteProtectionMode = 0xE4,
            WriteProtectionStatus = 0xE8,
            PdcReceivePointer = 0x100,
            PdcReceiveCounter = 0x104,
            PdcTransmitPointer = 0x108,
            PdcTransmitCounter = 0x10C,
            PdcReceiveNextPointer = 0x110,
            PdcReceiveNextCounter = 0x114,
            PdcTransmitNextPointer = 0x118,
            PdcTransmitNextCounter = 0x11C,
            PdcTransferControl = 0x120,
            PdcTransferStatus = 0x124,
        }
    }
}
