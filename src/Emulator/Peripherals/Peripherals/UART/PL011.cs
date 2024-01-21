//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class PL011 : UARTBase, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public PL011(IMachine machine, uint fifoSize = 1, uint frequency = 24000000) : base(machine)
        {
            hardwareFifoSize = fifoSize;
            uartClockFrequency = frequency;

            IRQ = new GPIO();
            interruptRawStatuses = new bool[InterruptsCount];
            interruptMasks = new bool[InterruptsCount];

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();

            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(innerLock)
            {
                return RegistersCollection.Read(offset);
            }
        }

        public override void Reset()
        {
            base.Reset();
            lock(innerLock)
            {
                RegistersCollection.Reset();

                // receiveFifoSize and receiveInterruptTriggerPoint depend on register values.
                UpdateReceiveFifoSize();
                UpdateReceiveInterruptTriggerPoint();

                System.Array.ForEach(interruptRawStatuses, status => status = false);
                System.Array.ForEach(interruptMasks, mask => mask = false);
                UpdateInterrupts();
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(innerLock)
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public override uint BaudRate
        {
            get
            {
                var divisor = 16 * (integerBaudRate.Value + (fractionalBaudRate.Value / 64));
                return (divisor > 0) ? (uartClockFrequency / (uint)divisor) : 0;
            }
        }

        public GPIO IRQ { get; }

        public override Parity ParityBit
        {
            get
            {
                if(!parityEnable.Value)
                {
                    return Parity.None;
                }
                else
                {
                    if(!evenParitySelect.Value)
                    {
                        return stickParitySelect.Value ? Parity.Forced1 : Parity.Odd;
                    }
                    else
                    {
                        return stickParitySelect.Value ? Parity.Forced0 : Parity.Even;
                    }
                }
            }
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x1000;

        public override Bits StopBits => twoStopBitsSelect.Value ? Bits.Two : Bits.One;

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override bool IsReceiveEnabled
        {
            get
            {
                return AssertFlagEnabled(uartEnable, "Character cannot be received by UART; UARTEN is disabled!")
                    && AssertFlagEnabled(receiveEnable, "Character cannot be received by UART; RXE is disabled!");
            }
        }

        protected override void QueueEmptied()
        {
            UpdateInterrupts();
        }

        private bool AssertFlagEnabled(IFlagRegisterField flag, string errorMessage)
        {
            if(!flag.Value)
            {
                this.Log(LogLevel.Error, errorMessage);
                return false;
            }
            return true;
        }

        private void ClearInterrupt(int interrupt)
        {
            this.Log(LogLevel.Noisy, "Clearing {0} interrupt.", (Interrupts)interrupt);
            interruptRawStatuses[interrupt] = false;
            UpdateInterrupts();
        }

        private void DefineRegisters()
        {
            Registers.Data.Define(this)
                .WithValueField(0, 8, name: "DATA - Receive (read) / Transmit (write) data character",
                        valueProviderCallback: _ => ReadDataRegister(),
                        writeCallback: (_, newValue) => WriteDataRegister((uint)newValue))
                .WithTaggedFlag("FE - Framing error", 8)
                .WithTaggedFlag("PE - Parity error", 9)
                .WithTaggedFlag("BE - Break error", 10)
                .WithTaggedFlag("OE - Overrun error", 11)
                .WithReservedBits(12, 4)
                ;

            Registers.Control.Define(this, 0x300)
                .WithFlag(0, out uartEnable, name: "UARTEN - UART enable",
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            // Documentation states that the Transmit interrupt should be only set upon
                            // crossing the threshold and enabling/disabling FIFO, but some software
                            // requires it to be set after enabling UART
                            interruptRawStatuses[(int)Interrupts.Transmit] = true;
                            UpdateInterrupts();
                        }
                    })
                .WithTaggedFlag("SIREN - SIR enable", 1)
                .WithTaggedFlag("SIRLP - IrDA SIR low power mode", 2)
                // These 4 bits can be written/read by software but there's no logic associated.
                .WithFlags(3, 4, name: "Vendor specific")
                .WithFlag(7, out loopbackEnable, name: "LBE - Loop back enable")
                .WithFlag(8, out transmitEnable, name: "TXE - Transmit enable")
                .WithFlag(9, out receiveEnable, name: "RXE - Receive enable")
                // No logic needed for DTR/RTS so these are flags just to hush write warnings.
                .WithFlag(10, name: "DTR - Data transmit ready")
                .WithFlag(11, name: "RTS - Request to send")
                .WithTaggedFlag("Out1", 12)
                .WithTaggedFlag("Out2", 13)
                .WithTaggedFlag("RTSEn - RTS hardware flow control enable", 14)
                .WithTaggedFlag("CTSEn - CTS hardware flow control enable", 15)
                ;

            Registers.Flag.Define(this)  // Doesn't need to have a reset value; it's 0b10010000 nevertheless.
                .WithTaggedFlag("CTS - Clear to send", 0)
                .WithTaggedFlag("DSR - Data set ready", 1)
                .WithTaggedFlag("DCD - Data carrier detect", 2)
                .WithTaggedFlag("BUSY - UART busy", 3)
                .WithFlag(4, FieldMode.Read, name: "RXFE - Receive FIFO empty", valueProviderCallback: _ => Count == 0)
                .WithTaggedFlag("TXFF - Transmit FIFO full", 5)
                .WithFlag(6, FieldMode.Read, name: "RXFF - Receive FIFO full", valueProviderCallback: _ => Count >= receiveFifoSize)
                .WithFlag(7, FieldMode.Read, name: "TXFE - Transmit FIFO empty", valueProviderCallback: _ => true)  // Always set.
                .WithTaggedFlag("RI - Ring indicator", 8)
                .WithReservedBits(9, 7)
                ;

            Registers.RawInterruptStatus.Define(this)
                .WithFlags(0, 11, FieldMode.Read, valueProviderCallback: (interrupt, _) => interruptRawStatuses[interrupt])
                .WithReservedBits(11, 5)
                ;

            Registers.LineControl.Define(this)
                .WithTaggedFlag("BRK - Send break", 0)
                .WithFlag(1, out parityEnable, name: "PEN - Parity enable")
                .WithFlag(2, out evenParitySelect, name: "EPS - Even parity select")
                .WithFlag(3, out twoStopBitsSelect, name: "STP2 - Two stop bits select")
                .WithFlag(4, out enableFifoBuffers, name: "FEN - Enable FIFOs", changeCallback: (_, __) => UpdateReceiveFifoSize())
                .WithEnumField(5, 2, out wordLength, name: "WLEN - Word length")
                .WithFlag(7, out stickParitySelect, name: "SPS - Stick parity select")
                .WithReservedBits(8, 8)
                ;

            Registers.IrDALowPowerCounter.Define(this)
                .WithTag("ILPDVSR - 8-bit low-power divisor value.", 0, 8)
                .WithReservedBits(8, 24)
                ;

            Registers.InterruptMask.Define(this)
                .WithFlags(0, 11, changeCallback: (interrupt, _, newValue) => { interruptMasks[interrupt] = newValue; UpdateInterrupts(); })
                .WithReservedBits(11, 5)
                ;

            Registers.IntegerBaudRate.Define(this)
                .WithValueField(0, 16, out integerBaudRate, name: "BAUD DIVINT - The integer baud rate divisor.")
                ;

            Registers.FractionalBaudRate.Define(this)
                .WithValueField(0, 6, out fractionalBaudRate, name: "BAUD DIVFRAC - The fractional baud rate divisor")
                .WithReservedBits(6, 10)
                ;

            Registers.DMAControl.Define(this)
                .WithTaggedFlag("RXDMAE - Receive DMA enable", 0)
                .WithTaggedFlag("TXDMAE - Transmit DMA enable", 1)
                .WithTaggedFlag("DMAONERR - DMA on error", 2)
                .WithReservedBits(3, 13)
                ;

            Registers.InterruptFIFOLevel.Define(this, 0b010010)  // The reset value is 2 for both fields.
                .WithValueField(0, 3, name: "TXIFLSEL - Transmit interrupt FIFO level select")  // Hush write warnings. Transmit interrupts are never triggered.
                .WithValueField(3, 3, out receiveInterruptFifoLevelSelect, name: "RXIFLSEL - Receive interrupt FIFO level select",
                        changeCallback: (_, __) => UpdateReceiveInterruptTriggerPoint())
                .WithReservedBits(6, 10)
                ;

            Registers.InterruptClear.Define(this)
                .WithFlags(0, 11, FieldMode.Write, writeCallback: (interrupt, _, newValue) => { if(newValue) ClearInterrupt(interrupt); })
                .WithReservedBits(11, 5)
                ;

            // Any write to this 8-bit register should clear all the errors if they're ever set.
            Registers.ReceiveStatus.Define(this)
                .WithFlag(0, name: "FE - Framing error", valueProviderCallback: _ => false)
                .WithFlag(1, name: "PE - Parity error", valueProviderCallback: _ => false)
                .WithFlag(2, name: "BE - Break error", valueProviderCallback: _ => false)
                .WithFlag(3, name: "OE - Overrun error", valueProviderCallback: _ => false)
                .WithFlags(4, 4, FieldMode.Write)
                ;

            Registers.MaskedInterruptStatus.Define(this)
                .WithValueField(0, 11, FieldMode.Read, name: "Masked interrupt status", valueProviderCallback: _ => MaskedInterruptStatus)
                .WithReservedBits(11, 5)
                ;

            Registers.UARTPeriphID0.DefineMany(this, 4, (register, idx) =>
            {
                register.WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => peripheralID[idx])
                    .WithReservedBits(8, 8);
            }, name: "Peripheral identification registers n");

            Registers.UARTPCellID0.DefineMany(this, 4, (register, idx) =>
            {
                register.WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => primeCellID[idx])
                    .WithReservedBits(8, 8);
            }, name: "PrimeCell identification registers n");
        }

        private byte ReadDataRegister()
        {
            // DATA register can be read to check errors so reading from an empty queue isn't a problem.
            if(TryGetCharacter(out byte character))
            {
                WarnIfWordLengthIncorrect();
                UpdateInterrupts();
            }
            return character;
        }

        private void UpdateReceiveInterruptTriggerPoint()
        {
            var levelSelect = receiveInterruptFifoLevelSelect.Value;
            switch(levelSelect)
            {
                case 0b000: receiveInterruptTriggerPoint = 1d / 8d * receiveFifoSize; break;
                case 0b001: receiveInterruptTriggerPoint = 1d / 4d * receiveFifoSize; break;
                case 0b010: receiveInterruptTriggerPoint = 1d / 2d * receiveFifoSize; break;
                case 0b011: receiveInterruptTriggerPoint = 3d / 4d * receiveFifoSize; break;
                case 0b100: receiveInterruptTriggerPoint = 7d / 8d * receiveFifoSize; break;
                default:
                    this.Log(LogLevel.Warning, "Receive interrupt FIFO level select written with invalid value: {0}", levelSelect);
                    return;
            }
            this.Log(LogLevel.Debug, "Receive Interrupt Trigger Point set to: {0} (level select = {1}; fifo size = {2}{3})",
                receiveInterruptTriggerPoint, levelSelect, receiveFifoSize, enableFifoBuffers.Value ? "" : " (FIFO buffers disabled)");

            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            interruptRawStatuses[(int)Interrupts.Receive] = Count >= receiveInterruptTriggerPoint;
            IRQ.Set(MaskedInterruptStatus != 0);
        }

        private void UpdateReceiveFifoSize()
        {
            if(enableFifoBuffers.Value)
            {
                receiveFifoSize = hardwareFifoSize;
                this.Log(LogLevel.Debug, "FIFO buffers enabled.");
            }
            else
            {
                receiveFifoSize = 1;
                this.Log(LogLevel.Debug, "FIFO buffers disabled.");
                interruptRawStatuses[(int)Interrupts.Transmit] = true;
                UpdateInterrupts();
            }
            UpdateReceiveInterruptTriggerPoint();
        }

        private void WarnIfWordLengthIncorrect()
        {
            if(wordLength.Value != WordLength.EightBits && wordLength.Value != WordLength.SevenBits)
            {
                this.Log(LogLevel.Warning, "DATA read or written while {0}-bit word length is set (WLEN={1}). Only 7-bit and 8-bit words are fully supported.",
                    wordLength.Value == WordLength.FiveBits ? "5" : "6", (uint)wordLength.Value);
            }
        }

        private void WriteDataRegister(uint value)
        {
            if(!AssertFlagEnabled(uartEnable, "DATA register cannot be written to; UARTEN is disabled!")
                || !AssertFlagEnabled(transmitEnable, "DATA register cannot be written to; TXE is disabled!"))
            {
                return;
            }
            WarnIfWordLengthIncorrect();

            if(!loopbackEnable.Value)
            {
                TransmitCharacter((byte)value);
            }
            interruptRawStatuses[(int)Interrupts.Transmit] = true;
            UpdateInterrupts();
        }

        private uint InterruptMask => Renode.Utilities.BitHelper.GetValueFromBitsArray(interruptMasks);

        private uint MaskedInterruptStatus => RawInterruptStatus & InterruptMask;

        private uint RawInterruptStatus => Renode.Utilities.BitHelper.GetValueFromBitsArray(interruptRawStatuses);

        private IFlagRegisterField enableFifoBuffers;
        private IFlagRegisterField evenParitySelect;
        private IValueRegisterField fractionalBaudRate;
        private IValueRegisterField integerBaudRate;
        private IFlagRegisterField loopbackEnable;
        private IFlagRegisterField parityEnable;
        private IFlagRegisterField receiveEnable;
        private IValueRegisterField receiveInterruptFifoLevelSelect;
        private IFlagRegisterField stickParitySelect;
        private IFlagRegisterField transmitEnable;
        private IFlagRegisterField twoStopBitsSelect;
        private IFlagRegisterField uartEnable;
        private IEnumRegisterField<WordLength> wordLength;

        private readonly uint hardwareFifoSize;
        private readonly bool[] interruptMasks;
        private readonly bool[] interruptRawStatuses;
        private readonly uint[] peripheralID = { 0x11, 0x10, 0x34, 0x0 };
        private readonly uint[] primeCellID = { 0x0D, 0xF0, 0x05, 0xB1 };
        private readonly uint uartClockFrequency;

        private uint receiveFifoSize;
        private double receiveInterruptTriggerPoint;

        private const uint InterruptsCount = 11;

        private enum Interrupts
        {
            ModemRingIndicator,
            ModemClearToSend,
            ModemDataCarrierDetect,
            ModemDataSetReady,
            Receive,
            Transmit,
            ReceiveTimeout,
            FramingError,
            ParityError,
            BreakError,
            OverrunError,
        }

        private enum Registers : long
        {
            Data                            = 0x000,
            ReceiveStatus                   = 0x004, //aka ErrorClear
            Flag                            = 0x018,
            IrDALowPowerCounter             = 0x020,
            IntegerBaudRate                 = 0x024,
            FractionalBaudRate              = 0x028,
            LineControl                     = 0x02c,
            Control                         = 0x030,
            InterruptFIFOLevel              = 0x034,
            InterruptMask                   = 0x038,
            RawInterruptStatus              = 0x03c,
            MaskedInterruptStatus           = 0x040,
            InterruptClear                  = 0x044,
            DMAControl                      = 0x048,
            UARTPeriphID0                   = 0xFE0,
            UARTPeriphID1                   = 0xFE4,
            UARTPeriphID2                   = 0xFE8,
            UARTPeriphID3                   = 0xFEC,
            UARTPCellID0                    = 0xFF0,
            UARTPCellID1                    = 0xFF4,
            UARTPCellID2                    = 0xFF8,
            UARTPCellID3                    = 0xFFC
        }

        private enum WordLength
        {
            FiveBits,
            SixBits,
            SevenBits,
            EightBits,
        }
    }
}

