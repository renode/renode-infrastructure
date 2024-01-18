//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class RenesasDA14_UART : UARTBase, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasDA14_UART(IMachine machine, uint systemClockFrequency = 32000000) : base(machine)
        {
            IRQ = new GPIO();
            this.systemClockFrequency = systemClockFrequency;
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            divider = 0x0;
            dividerFraction = 0x0;
            readMode = false;

            interruptIdentification = InterruptLevel.NoInterruptsPending;

            base.Reset();
            RegistersCollection.Reset();
            IRQ.Unset();
        }

        public uint ReadDoubleWord(long offset)
        {
            readMode = true;
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            readMode = false;
            RegistersCollection.Write(offset, value);
        }

        public GPIO IRQ { get; }

        public long Size => 0x100;

        public override Bits StopBits
        {
            get
            {
                if(!stopBits.Value)
                {
                    return Bits.One;
                }
                // is word length is equal to 5? then 1.5 else 2
                return dataLength.Value == LineControlDataLength.FiveBits ? Bits.OneAndAHalf : Bits.Two;
            }
        }

        public override Parity ParityBit
        {
            get
            {
                if(!enableParity.Value)
                {
                    return Parity.None;
                }
                if(!forceParity.Value)
                {
                    return !evenParity.Value ? Parity.Odd : Parity.Even;
                }
                return !evenParity.Value ? Parity.Forced1 : Parity.Forced0;
            }
        }

        public override uint BaudRate
        {
            get
            {
                float divisor = (16 * divider) + (dividerFraction / 16);
                return divisor == 0 ? 0 : (uint)(systemClockFrequency / divisor);
            }
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        protected override void QueueEmptied()
        {
            interruptIndicator &= ~(InterruptIndicator.DataReady);
            UpdateInterrupts();
        }

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        private byte ReadFifo()
        {
            if(!TryGetCharacter(out var character))
            {
                this.Log(LogLevel.Warning, "Trying to read from an empty Rx FIFO.");
            }
            ClearLineStatusIndicators();
            UpdateInterrupts();

            return character;
        }

        private void WriteFifo(byte value)
        {
            if(fifoEnable.Value)
            {
                TransmitCharacter(value);
                interruptIndicator |= InterruptIndicator.DataReady;
            }
            else
            {
                if(interruptIndicator.HasFlag(InterruptIndicator.DataReady))
                {
                    interruptIndicator |= InterruptIndicator.OverrunError;
                    // FIFO is disabled, in this mode, if we still have data, it is overwritten
                    ClearBuffer();
                }
                TransmitCharacter(value);
                interruptIndicator |= (InterruptIndicator.DataReady | InterruptIndicator.Break);
            }
            UpdateInterrupts();
        }

        private void ClearLineStatusIndicators()
        {
            if(!fifoEnable.Value)
            {
                interruptIndicator &= ~InterruptIndicator.DataReady;
                interruptIndicator |= InterruptIndicator.Break;
            }
            else
            {
                interruptIndicator &= ~InterruptIndicator.Break;
            }
            interruptIndicator &= ~(InterruptIndicator.OverrunError | InterruptIndicator.ParityError | InterruptIndicator.FramingError);
        }

        private void UpdateInterrupts()
        {
            var interruptId = InterruptLevel.NoInterruptsPending;
            if(interruptRLSFlagEnable.Value && interruptIndicator > InterruptIndicator.DataReady)
            {
                interruptId = InterruptLevel.ReceiverLineStatusIrq;
            }
            else if(InterriuptReceiverDataFlagEnable.Value && (interruptIndicator.HasFlag(InterruptIndicator.DataReady)) && RecvTriggerThresholdReached)
            {
                interruptId = InterruptLevel.ReceiverDataIrq;
            }

            interruptIdentification = interruptId;

            IRQ.Set(interruptId != InterruptLevel.NoInterruptsPending);
        }

        private void DefineRegisters()
        {
            Registers.Data.DefineConditional(this, () => !divisorLatchAccess.Value)
                .WithValueField(0, 8, name: "RBR_THR_DLL",
                        valueProviderCallback: _ => ReadFifo(),
                        writeCallback: (_, value) =>
                        {
                            if(loopback.Value)
                            {
                                base.WriteChar((byte)value);
                                return;
                            }
                            WriteFifo((byte)value);
                        })
                .WithReservedBits(8, 24);

            Registers.DivisorLatchL.DefineConditional(this, () => divisorLatchAccess.Value, 0xc)
                .WithValueField(0, 8, name: "RBR_THR_DLL",
                        valueProviderCallback: _ => BitHelper.GetValue(divider, 0, 8),
                        writeCallback: (_, value) => BitHelper.SetBitsFrom(divider, value, 0, 8))
                .WithReservedBits(8, 24);

            Registers.InterruptEnable.DefineConditional(this, () => !divisorLatchAccess.Value)
                .WithFlag(0, out InterriuptReceiverDataFlagEnable, name: "ERBFI_DLH0")
                .WithTaggedFlag("ETBEI_DLH1", 1)
                .WithFlag(2, out interruptRLSFlagEnable, name: "ELSI_DLH2")
                .WithTaggedFlag("ELSI_DLH2", 3)
                .WithTaggedFlag("ELCOLR_DLH4", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("PTIME_DLH7", 7)
                .WithReservedBits(8, 24)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.DivisorLatchH.DefineConditional(this, () => divisorLatchAccess.Value)
                .WithValueField(0, 8, name: "ERBFI_DLH0",
                        valueProviderCallback: _ => BitHelper.GetValue(divider, 8, 8),
                        writeCallback: (_, value) => BitHelper.SetBitsFrom(divider, value, 8, 8))
                .WithReservedBits(8, 24);

            Registers.FIFOControl.DefineConditional(this, () => readMode)
                .WithValueField(0, 4, FieldMode.Read, name: "IID",
                        valueProviderCallback: _ => (ulong)interruptIndicator)
                .WithReservedBits(4, 2)
                .WithValueField(6, 2, FieldMode.Read, name: "FIFOSE",
                        valueProviderCallback: _ => (ulong)(fifoEnable.Value ? 0b11 : 0b00))
                .WithReservedBits(8, 24);

            Registers.FIFOControl.DefineConditional(this, () => !readMode)
                .WithFlag(0, out fifoEnable, FieldMode.Write, name: "FIFOE")
                .WithFlag(1, FieldMode.Write, name: "RFIFOR",
                        writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                ClearBuffer();
                            }
                        })
                .WithFlag(2, FieldMode.Write, name: "XFIFOR")
                .WithFlag(3, out dmaMode, FieldMode.Write, name: "DMAM")
                .WithEnumField<DoubleWordRegister, TriggerLevel>(4, 2, out txEmptyTrigger, FieldMode.Write, name: "TET")
                .WithEnumField<DoubleWordRegister, TriggerLevel>(6, 2, out recvTrigger, FieldMode.Write, name: "RT")
                .WithReservedBits(8, 24);

            Registers.LineControl.Define(this)
                .WithEnumField<DoubleWordRegister, LineControlDataLength>(0, 2, out dataLength, name: "UART_DLS")
                .WithFlag(2, out stopBits, name: "UART_STOP")
                .WithFlag(3, out enableParity, name: "UART_PEN")
                .WithFlag(4, out evenParity, name: "UART_EPS")
                .WithReservedBits(5, 1)
                .WithFlag(6, out forceParity, name: "UART_BC")
                .WithFlag(7, out divisorLatchAccess, name: "UART_DLAB")
                .WithReservedBits(8, 24);

            Registers.ModemControl.Define(this)
                .WithReservedBits(0, 4)
                .WithFlag(4, out loopback, name: "UART_LB")
                .WithReservedBits(5, 27);

            Registers.LineStatus.Define(this)
                .WithFlag(0, FieldMode.Read, name: "UART_DR",
                        valueProviderCallback: _ => interruptIndicator.HasFlag(InterruptIndicator.DataReady))
                .WithFlag(1, FieldMode.ReadToClear, name: "UART_OE",
                        valueProviderCallback: _ => interruptIndicator.HasFlag(InterruptIndicator.OverrunError))
                .WithFlag(2, FieldMode.ReadToClear, name: "UART_PE",
                        valueProviderCallback: _ => interruptIndicator.HasFlag(InterruptIndicator.ParityError))
                .WithFlag(3, FieldMode.ReadToClear, name: "UART_FE",
                        valueProviderCallback: _ => interruptIndicator.HasFlag(InterruptIndicator.FramingError))
                .WithFlag(4, FieldMode.ReadToClear, name: "UART_BI",
                        valueProviderCallback: _ => interruptIndicator.HasFlag(InterruptIndicator.Break))
                .WithFlag(5, FieldMode.Read, name: "UART_THRE",
                        valueProviderCallback: _ => true)
                .WithFlag(6, FieldMode.Read, name: "UART_TEMT",
                        valueProviderCallback: _ => true)
                .WithFlag(7, FieldMode.Read, name: "UART_RFE",
                        valueProviderCallback: _ => false)
                .WithTaggedFlag("UART_ADDR_RCVD", 8)
                .WithReservedBits(9, 23)
                .WithReadCallback((_, __) =>
                        {
                            interruptIndicator = InterruptIndicator.None | (interruptIndicator & InterruptIndicator.DataReady);
                            if(!fifoEnable.Value)
                            {
                                interruptIndicator |= InterruptIndicator.Break;
                            }
                        });

            Registers.ModemStatus.Define(this, 0x4)
                .WithFlag(0, out deltaClearToSend, name: "UART_DCTS")
                .WithReservedBits(1, 3)
                .WithFlag(4, out clearToSend, name: "UART_CTS")
                .WithReservedBits(5, 27);

            Registers.Scratchpad.Define(this)
                .WithValueField(0, 8, name: "UART_SCRATCH_PAD")
                .WithReservedBits(8, 24);

            Registers.Status.Define(this, 0x6)
                .WithFlag(0, FieldMode.Read, name: "UART_BUSY",
                        valueProviderCallback: _ => false) // Operations are instantaneous
                .WithFlag(1, FieldMode.Read, name: "UART_TFNF",
                        valueProviderCallback: _ => true) // Transmit fifo not full
                .WithFlag(2, FieldMode.Read, name: "UART_TFE",
                        valueProviderCallback: _ => true) // Transmit fifo empty
                .WithFlag(3, FieldMode.Read, name: "UART_RFNE",
                        valueProviderCallback: _ => Count != 0) // 0 FIFO is empty, 1 FIFO is not empty
                .WithFlag(4, FieldMode.Read, name: "UART_RFF",
                        valueProviderCallback: _ => Count >= ReceiveFIFOSize) // 0 FIFO is not full, 1 FIFO is full
                .WithReservedBits(5, 27);

            Registers.TransmitFifoLevel.Define(this)
                .WithValueField(0, 5, FieldMode.Read, name: "UART_TRANSMIT_FIFO_LEVEL",
                        valueProviderCallback: _ => 0)
                .WithReservedBits(5, 27);

            Registers.ReceiveFifoLevel.Define(this)
                .WithValueField(0, 5, FieldMode.Read, name: "UART_RECEIVE_FIFO_LEVEL",
                        valueProviderCallback: _ => (ulong)Count)
                .WithReservedBits(5, 27);

            Registers.SoftwareReset.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "UART_UR",
                        changeCallback: (_, __) => Reset())
                .WithFlag(1, FieldMode.WriteOneToClear, name: "UART_RFR",
                        changeCallback: (_, __) => ClearBuffer())
                .WithFlag(2, FieldMode.WriteOneToClear, name: "UART_XFR")
                .WithReservedBits(3, 28);

            Registers.DmaModeShadow.Define(this)
                .WithFlag(0, out dmaMode, name: "UART_SHADOW_DMA_MODE")
                .WithReservedBits(1, 31);

            Registers.FIFOEnableShadow.Define(this)
                .WithFlag(0, out fifoEnable, name: "UART_SHADOW_FIFO_ENABLE")
                .WithReservedBits(1, 31);

            Registers.RCVRTriggerShadow.Define(this)
                .WithEnumField<DoubleWordRegister, TriggerLevel>(0, 2, out recvTrigger, name: "UART_SHADOW_RCVR_TRIGGER")
                .WithReservedBits(2, 30);

            Registers.TXEmptyTriggerShadow.Define(this)
                .WithEnumField<DoubleWordRegister, TriggerLevel>(0, 2, out txEmptyTrigger, name: "UART_SHADOW_TX_EMPTY_TRIGGER")
                .WithReservedBits(2, 30);

            Registers.DivisorLatchFraction.Define(this)
                .WithValueField(0, 4, name: "UART_DLF",
                        writeCallback: (_, value) => dividerFraction = (byte)value)
                .WithReservedBits(5, 27);

            Registers.ComponentVersion.Define(this, 0x3430312A)
                .WithValueField(0, 32, FieldMode.Read, name: "UART_UCV");

            Registers.ComponentType.Define(this, 0x44570110)
                .WithValueField(0, 32, FieldMode.Read, name: "UART_CTR");

            Registers.DataShadow0.DefineMany(this, DataShadowRegistersCount, (register, idx) =>
            {
                // Datasheet states that reading/writing to these registers is the same as reading/writing to the head of FIFO
                register
                    .WithValueField(0, 8, name: $"SRBR_STHR{idx}",
                            valueProviderCallback: _ => ReadFifo(),
                            writeCallback: (_, value) => WriteFifo((byte)value))
                    .WithReservedBits(8, 24);
            });
        }

        private uint RecvTriggerThreshold
        {
            get
            {
                switch(recvTrigger.Value)
                {
                    case TriggerLevel.FifoEmpty:
                        return 0;
                    case TriggerLevel.OneFourthFull:
                        return ReceiveFIFOSize / 4;
                    case TriggerLevel.OneHalfFull:
                        return ReceiveFIFOSize / 2;
                    case TriggerLevel.TwoLessThanFull:
                        return ReceiveFIFOSize - 2;
                    default:
                        throw new Exception("Unreachable");
                }
            }
        }

        private bool RecvTriggerThresholdReached => !fifoEnable.Value || Count > RecvTriggerThreshold;

        /* Flag used to determine FIFOControl register mode */
        private bool readMode;
        /* Interrupt Enable Flags */
        private IFlagRegisterField InterriuptReceiverDataFlagEnable;
        private IFlagRegisterField interruptRLSFlagEnable;
        /* Line Control Flags */
        private IEnumRegisterField<LineControlDataLength> dataLength;
        private IFlagRegisterField stopBits;
        private IFlagRegisterField forceParity;
        private IFlagRegisterField evenParity;
        private IFlagRegisterField enableParity;
        private IFlagRegisterField divisorLatchAccess;
        /* Modem Control Flags */
        private IFlagRegisterField loopback;
        /* Line Status Flags */
        private InterruptIndicator interruptIndicator;
        /* Modem Status Flags */
        private IFlagRegisterField deltaClearToSend;
        private IFlagRegisterField clearToSend;
        /* Fifo Control Flags */
        private IFlagRegisterField fifoEnable;
        private IFlagRegisterField dmaMode;
        private IEnumRegisterField<TriggerLevel> txEmptyTrigger;
        private IEnumRegisterField<TriggerLevel> recvTrigger;
        private ushort divider;
        private ushort dividerFraction;
        private InterruptLevel interruptIdentification;
        private readonly uint systemClockFrequency;

        private const int ReceiveFIFOSize = 16;
        private const int DataShadowRegistersCount = 16;

        private enum Registers
        {
            Data = 0x00,
            DivisorLatchL = 0x00,
            // the same as Data but accessible only when DLAB bit is set
            InterruptEnable = 0x04,
            DivisorLatchH = 0x04,
            // the same as Interrupt enable but accessible only when DLAB bit is set
            FIFOControl = 0x08,
            LineControl = 0x0C,
            ModemControl = 0x10,
            LineStatus = 0x14,
            ModemStatus = 0x18,
            Scratchpad = 0x1C,
            DataShadow0 = 0x30,
            DataShadow1 = 0x34,
            DataShadow2 = 0x38,
            DataShadow3 = 0x3C,
            DataShadow4 = 0x40,
            DataShadow5 = 0x44,
            DataShadow6 = 0x48,
            DataShadow7 = 0x4C,
            DataShadow8 = 0x50,
            DataShadow9 = 0x54,
            DataShadow10 = 0x58,
            DataShadow11 = 0x5C,
            DataShadow12 = 0x60,
            DataShadow13 = 0x64,
            DataShadow14 = 0x68,
            DataShadow15 = 0x6C,
            Status = 0x7C,
            TransmitFifoLevel = 0x80,
            ReceiveFifoLevel = 0x84,
            SoftwareReset = 0x88,
            BreakControlShadow = 0x90,
            DmaModeShadow = 0x94,
            FIFOEnableShadow = 0x98,
            RCVRTriggerShadow = 0x9C,
            TXEmptyTriggerShadow = 0xA0,
            TXHalt = 0xA4,
            DMAAck = 0xA8,
            DivisorLatchFraction = 0xC0,
            ComponentVersion = 0xF8,
            ComponentType = 0xFC,
        }

        [Flags]
        private enum InterruptLevel : byte
        {
            NoInterruptsPending         = 0b0001,
            TransmitterHoldingRegEmpty  = 0b0010,
            ReceiverDataIrq             = 0b0100,
            ReceiverLineStatusIrq       = 0b0110,
            BusyDetect                  = 0b0111,
            CharacterTimeoutIndication  = 0b1100,
        }

        [Flags]
        private enum InterruptIndicator : byte
        {
            None            = 0b00000,
            DataReady       = 0b00001,
            OverrunError    = 0b00010,
            ParityError     = 0b00100,
            FramingError    = 0b01000,
            Break           = 0b10000,
        }

        private enum LineControlDataLength : byte
        {
            FiveBits = 0x0,
            SixBits,
            SevenBits,
            EightBits,
        }

        private enum TriggerLevel : byte
        {
            FifoEmpty = 0x0,
            OneFourthFull,
            OneHalfFull,
            TwoLessThanFull,
        }
    }
}

