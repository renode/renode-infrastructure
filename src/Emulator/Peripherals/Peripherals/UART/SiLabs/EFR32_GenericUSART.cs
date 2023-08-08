//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART.Silabs
{
    public abstract class EFR32_GenericUSART : UARTBase, IUARTWithBufferState, IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>
    {
        public EFR32_GenericUSART(IMachine machine, uint clockFrequency) : base(machine)
        {
            TransmitIRQ = new GPIO();
            ReceiveIRQ = new GPIO();
            RxDataAvailableRequest = new GPIO();
            RxDataAvailableSingleRequest = new GPIO();
            TxBufferLowRequest = new GPIO();
            TxBufferLowSingleRequest = new GPIO();
            TxEmptyRequest = new GPIO();
            RxDataAvailableRightRequest = new GPIO();
            RxDataAvailableRightSingleRequest = new GPIO();
            TxBufferLowRightRequest = new GPIO();
            TxBufferLowRightSingleRequest = new GPIO();
            TxEmptyRequest.Set(true);
            TxBufferLowRequest.Set(true);

            interruptsManager = new InterruptManager<Interrupt>(this);

            uartClockFrequency = clockFrequency;
        }

        public override void Reset()
        {
            base.Reset();
            interruptsManager.Reset();
            spiSlaveDevice = null;
            TxEmptyRequest.Set(true);
            TxBufferLowRequest.Set(true);
        }

        public void Register(ISPIPeripheral peripheral, NullRegistrationPoint registrationPoint)
        {
            if(spiSlaveDevice != null)
            {
                throw new RegistrationException("Cannot register more than one peripheral.");
            }
            Machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            spiSlaveDevice = peripheral;
        }

        public void Unregister(ISPIPeripheral peripheral)
        {
            if(peripheral != spiSlaveDevice)
            {
                throw new RegistrationException("Trying to unregister not registered device.");
            }

            Machine.UnregisterAsAChildOf(this, peripheral);
            spiSlaveDevice = null;
        }

        public IEnumerable<NullRegistrationPoint> GetRegistrationPoints(ISPIPeripheral peripheral)
        {
            if(peripheral != spiSlaveDevice)
            {
                throw new RegistrationException("Trying to obtain a registration point for a not registered device.");
            }

            return new[] { NullRegistrationPoint.Instance };
        }

        public override void WriteChar(byte value)
        {
            if(BufferState == BufferState.Full)
            {
                this.Log(LogLevel.Warning, "RX buffer is full. Dropping incoming byte (0x{0:X})", value);
                return;
            }
            base.WriteChar(value);
        }

        IEnumerable<IRegistered<ISPIPeripheral, NullRegistrationPoint>> IPeripheralContainer<ISPIPeripheral, NullRegistrationPoint>.Children
        {
            get
            {
                return new[] { Registered.Create(spiSlaveDevice, NullRegistrationPoint.Instance) };
            }
        }

        [IrqProvider("transmit irq", 0)]
        public GPIO TransmitIRQ { get; }

        [IrqProvider("receive irq", 1)]
        public GPIO ReceiveIRQ { get; }

        public GPIO RxDataAvailableRequest { get; }
        public GPIO RxDataAvailableSingleRequest { get; }
        public GPIO TxBufferLowRequest { get; }
        public GPIO TxBufferLowSingleRequest { get; }
        public GPIO TxEmptyRequest { get; }
        public GPIO RxDataAvailableRightRequest { get; }
        public GPIO RxDataAvailableRightSingleRequest { get; }
        public GPIO TxBufferLowRightRequest { get; }
        public GPIO TxBufferLowRightSingleRequest { get; }

        public override Parity ParityBit { get { return parityBitModeField.Value; } }

        public override Bits StopBits { get { return stopBitsModeField.Value; } }

        public override uint BaudRate
        {
            get
            {
                //This calculation, according to the documentation, could be written as:
                //return uartClockFrequency / (multiplier * (1 + (fractionalClockDividerField.Value << 3) / 256));
                //But the result differs much from the one calculated by the driver, most probably due to
                //invalid integer division.
                //This code mimics the one in emlib driver.
                var oversample = 1u;
                var factor = 0u;
                switch(oversamplingField.Value)
                {
                    case OversamplingMode.Times16:
                        oversample = 1;
                        factor = 256 / 16;
                        break;
                    case OversamplingMode.Times8:
                        oversample = 1;
                        factor = 256 / 8;
                        break;
                    case OversamplingMode.Times6:
                        oversample = 3;
                        factor = 256 / 2;
                        break;
                    case OversamplingMode.Times4:
                        oversample = 1;
                        factor = 256 / 4;
                        break;
                }
                var divisor = oversample * (256 + ((uint)fractionalClockDividerField.Value << 3));
                var quotient = uartClockFrequency / divisor;
                var remainder = uartClockFrequency % divisor;
                return (factor * quotient) + (factor * remainder) / divisor;
            }
        }

        public BufferState BufferState
        {
            get
            {
                return bufferState;
            }

            private set
            {
                if(bufferState == value)
                {
                    return;
                }
                bufferState = value;
                BufferStateChanged?.Invoke(value);
                switch(bufferState)
                {
                    case BufferState.Empty:
                        RxDataAvailableRequest.Set(false);
                        RxDataAvailableSingleRequest.Set(false);
                        break;
                    case BufferState.Ready:
                        RxDataAvailableRequest.Set(false);
                        RxDataAvailableSingleRequest.Set(true);
                        break;
                    case BufferState.Full:
                        RxDataAvailableRequest.Set(true);
                        break;
                    default:
                        throw new Exception("Unreachable code. Invalid BufferState value.");
                }
            }
        }

        public event Action<BufferState> BufferStateChanged;

        protected override void CharWritten()
        {
            interruptsManager.SetInterrupt(Interrupt.ReceiveDataValid);
            receiveDataValidFlag.Value = true;
            BufferState = Count == BufferSize ? BufferState.Full : BufferState.Ready;
        }

        protected override void QueueEmptied()
        {
            interruptsManager.ClearInterrupt(Interrupt.ReceiveDataValid);
            receiveDataValidFlag.Value = false;
            BufferState = BufferState.Empty;
        }

        protected override bool IsReceiveEnabled => receiverEnableFlag.Value;

        protected DoubleWordRegister GenerateControlRegister() => new DoubleWordRegister(this)
            .WithEnumField(0, 1, out operationModeField, name: "SYNC")
            .WithTaggedFlag("LOOPBK", 1)
            .WithTaggedFlag("CCEN", 2)
            .WithTaggedFlag("MPM", 3)
            .WithTaggedFlag("MPAB", 4)
            .WithEnumField(5, 2, out oversamplingField, name: "OVS")
            .WithReservedBits(7, 1)
            .WithTaggedFlag("CLKPOL", 8)
            .WithTaggedFlag("CLKPHA", 9)
            .WithTaggedFlag("MSBF", 10)
            .WithTaggedFlag("CSMA", 11)
            .WithTaggedFlag("TXBIL", 12)
            .WithTaggedFlag("RXINV", 13)
            .WithTaggedFlag("TXINV", 14)
            .WithTaggedFlag("CSINV", 15)
            .WithTaggedFlag("AUTOCS", 16)
            .WithTaggedFlag("AUTOTRI", 17)
            .WithTaggedFlag("SCMODE", 18)
            .WithTaggedFlag("SCRETRANS", 19)
            .WithTaggedFlag("SKIPPERRF", 20)
            .WithTaggedFlag("BIT8DV", 21)
            .WithTaggedFlag("ERRSDMA", 22)
            .WithTaggedFlag("ERRSRX", 23)
            .WithTaggedFlag("ERRSTX", 24)
            .WithTaggedFlag("SSSEARLY", 25)
            .WithReservedBits(26, 2)
            .WithTaggedFlag("BYTESWAP", 28)
            .WithTaggedFlag("AUTOTX", 29)
            .WithTaggedFlag("MVDIS", 30)
            .WithTaggedFlag("SMSDELAY", 31);

        protected DoubleWordRegister GenerateFrameFormatRegister() => new DoubleWordRegister(this, 0x1005)
            .WithTag("DATABITS", 0, 4)
            .WithReservedBits(4, 4)
            .WithEnumField(8, 2, out parityBitModeField, name: "PARITY")
            .WithReservedBits(10, 2)
            .WithEnumField(12, 2, out stopBitsModeField, name: "STOPBITS")
            .WithReservedBits(14, 18);

        protected DoubleWordRegister GenerateTriggerControlRegister() => new DoubleWordRegister(this)
            .WithReservedBits(0, 4)
            .WithTaggedFlag("RXTEN", 4)
            .WithTaggedFlag("TXTEN", 5)
            .WithTaggedFlag("AUTOTXTEN", 6)
            .WithTaggedFlag("TXARX0EN", 7)
            .WithTaggedFlag("TXARX1EN", 8)
            .WithTaggedFlag("TXARX2EN", 9)
            .WithTaggedFlag("RXATX0EN", 10)
            .WithTaggedFlag("RXATX1EN", 11)
            .WithTaggedFlag("RXATX2EN", 12)
            .WithReservedBits(13, 3)
            .WithTag("TSEL", 16, 4)
            .WithReservedBits(20, 12);

        protected DoubleWordRegister GenerateCommandRegister() => new DoubleWordRegister(this)
            .WithFlag(0, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) receiverEnableFlag.Value = true; }, name: "RXEN")
            .WithFlag(1, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) receiverEnableFlag.Value = false; }, name: "RXDIS")
            .WithFlag(2, FieldMode.Set, writeCallback: (_, newValue) =>
            {
                if(newValue)
                {
                    transmitterEnableFlag.Value = true;
                    interruptsManager.SetInterrupt(Interrupt.TransmitBufferLevel);
                }
            }, name: "TXEN")
            .WithFlag(3, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) transmitterEnableFlag.Value = false; }, name: "TXDIS")
            .WithTaggedFlag("MASTEREN", 4)
            .WithTaggedFlag("MASTERDIS", 5)
            .WithTaggedFlag("RXBLOCKEN", 6)
            .WithTaggedFlag("RXBLOCKDIS", 7)
            .WithTaggedFlag("TXTRIEN", 8)
            .WithTaggedFlag("TXTRIDIS", 9)
            .WithTaggedFlag("CLEARTX", 10)
            .WithFlag(11, FieldMode.Set, writeCallback: (_, newValue) => { if(newValue) ClearBuffer(); }, name: "CLEARRX")
            .WithReservedBits(12, 20);

        protected DoubleWordRegister GenerateStatusRegister() => new DoubleWordRegister(this, 0x40)
            .WithFlag(0, out receiverEnableFlag, FieldMode.Read, name: "RXENS")
            .WithFlag(1, out transmitterEnableFlag, FieldMode.Read, name: "TXENS")
            .WithTaggedFlag("MASTER", 2)
            .WithTaggedFlag("RXBLOCK", 3)
            .WithTaggedFlag("TXTRI", 4)
            .WithFlag(5, out transferCompleteFlag, FieldMode.Read, name: "TXC")
            .WithTaggedFlag("TXBL", 6)
            .WithFlag(7, out receiveDataValidFlag, FieldMode.Read, name: "RXDATAV")
            .WithFlag(8, FieldMode.Read, valueProviderCallback: _ => Count == BufferSize, name: "RXFULL")
            .WithTaggedFlag("TXBDRIGHT", 9)
            .WithTaggedFlag("TXBSRIGHT", 10)
            .WithTaggedFlag("RXDATAVRIGHT", 11)
            .WithTaggedFlag("RXFULLRIGHT", 12)
            .WithFlag(13, FieldMode.Read, valueProviderCallback: _ => true, name: "TXIDLE")
            .WithTaggedFlag("TIMERRESTARTED", 14)
            .WithReservedBits(15, 1)
            .WithValueField(16, 2, FieldMode.Read, valueProviderCallback: _ => 0, name: "TXBUFCNT")
            .WithReservedBits(18, 14);

        protected DoubleWordRegister GenerateClockControlRegister() => new DoubleWordRegister(this)
            .WithReservedBits(0, 3)
            .WithValueField(3, 20, out fractionalClockDividerField, name: "DIV")
            .WithReservedBits(23, 8)
            .WithTaggedFlag("AUTOBAUDEN", 31);

        protected DoubleWordRegister GenerateRxBufferDataExtendedRegister() => new DoubleWordRegister(this)
            .WithTag("RXDATA", 0, 8)
            .WithReservedBits(8, 5)
            .WithTaggedFlag("PERR", 14)
            .WithTaggedFlag("FERR", 15)
            .WithReservedBits(16, 16);

        protected DoubleWordRegister GenerateRxBufferDataRegister() => new DoubleWordRegister(this)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: (_) => ReadBuffer(), name: "RXDATA")
            .WithReservedBits(8, 24);

        protected DoubleWordRegister GenerateRxBufferDoubleDataExtendedRegister() => new DoubleWordRegister(this)
            .WithTag("RXDATA0", 0, 8)
            .WithReservedBits(8, 5)
            .WithTaggedFlag("PERR0", 14)
            .WithTaggedFlag("FERR0", 15)
            .WithTag("RXDATA1", 16, 8)
            .WithReservedBits(24, 5)
            .WithTaggedFlag("PERR1", 30)
            .WithTaggedFlag("FERR1", 31);

        protected DoubleWordRegister GenerateRxBufferDoubleDataRegister() => new DoubleWordRegister(this)
            .WithTag("RXDATA0", 0, 8)
            .WithTag("RXDATA1", 8, 8)
            .WithReservedBits(16, 16);

        protected DoubleWordRegister GenerateRxBufferDataExtendedPeekRegister() => new DoubleWordRegister(this)
            .WithTag("RXDATAP", 0, 8)
            .WithReservedBits(8, 5)
            .WithTaggedFlag("PERRP", 14)
            .WithTaggedFlag("FERRP", 15)
            .WithReservedBits(16, 16);

        protected DoubleWordRegister GenerateRxBufferDoubleDataExtendedPeekRegister() => new DoubleWordRegister(this)
            .WithTag("RXDATAP0", 0, 8)
            .WithReservedBits(8, 5)
            .WithTaggedFlag("PERRP0", 14)
            .WithTaggedFlag("FERRP0", 15)
            .WithTag("RXDATAP1", 16, 8)
            .WithReservedBits(24, 5)
            .WithTaggedFlag("PERRP1", 30)
            .WithTaggedFlag("FERRP1", 31);

        protected DoubleWordRegister GenerateTxBufferDataExtendedRegister() => new DoubleWordRegister(this)
            .WithTag("TXDATAX", 0, 8)
            .WithReservedBits(8, 2)
            .WithTaggedFlag("UBRXAT", 11)
            .WithTaggedFlag("TXTRIAT", 12)
            .WithTaggedFlag("TXBREAK", 13)
            .WithTaggedFlag("TXDISAT", 14)
            .WithTaggedFlag("RXENAT", 15)
            .WithReservedBits(16, 16);

        protected DoubleWordRegister GenerateTxBufferDataRegister() => new DoubleWordRegister(this)
            .WithValueField(0, 8, FieldMode.Write, writeCallback: (_, v) => HandleTxBufferData((byte)v), name: "TXDATA")
            .WithReservedBits(8, 24);

        protected DoubleWordRegister GenerateTxBufferDoubleDataExtendedRegister() => new DoubleWordRegister(this)
            .WithTag("TXDATA0", 0, 8)
            .WithReservedBits(8, 2)
            .WithTaggedFlag("UBRXAT0", 11)
            .WithTaggedFlag("TXTRIAT0", 12)
            .WithTaggedFlag("TXBREAK0", 13)
            .WithTaggedFlag("TXDISAT0", 14)
            .WithTaggedFlag("RXENAT0", 15)
            .WithTag("TXDATA1", 16, 8)
            .WithReservedBits(24, 2)
            .WithTaggedFlag("UBRXAT1", 27)
            .WithTaggedFlag("TXTRIAT1", 28)
            .WithTaggedFlag("TXBREAK1", 29)
            .WithTaggedFlag("TXDISAT1", 30)
            .WithTaggedFlag("RXENAT1", 31);

        protected DoubleWordRegister GenerateTxBufferDoubleDataRegister() => new DoubleWordRegister(this)
            .WithTag("TXDATA0", 0, 8)
            .WithTag("TXDATA1", 8, 8)
            .WithReservedBits(16, 16);

        protected DoubleWordRegister GenerateIrDAControlRegister() => new DoubleWordRegister(this)
            .WithTaggedFlag("IREN", 0)
            .WithTag("IRPW", 1, 2)
            .WithTaggedFlag("IRFILT", 3)
            .WithReservedBits(4, 3)
            .WithTaggedFlag("IRPRSEN", 7)
            .WithTag("IRPRSSEL", 8, 4)
            .WithReservedBits(12, 20);

        protected DoubleWordRegister GenerateUSARTInputRegister() => new DoubleWordRegister(this)
            .WithTag("RXPRSSEL", 0, 4)
            .WithReservedBits(4, 3)
            .WithTaggedFlag("RXPRS", 7)
            .WithTag("CLKPRSSEL", 8, 4)
            .WithReservedBits(12, 3)
            .WithTaggedFlag("CLKPRS", 15)
            .WithReservedBits(16, 16);

        protected DoubleWordRegister GenerateI2SControlRegister() => new DoubleWordRegister(this)
            .WithTaggedFlag("EN", 0)
            .WithTaggedFlag("MONO", 1)
            .WithTaggedFlag("JUSTIFY", 2)
            .WithTaggedFlag("DMASPLIT", 3)
            .WithTaggedFlag("DELAY", 4)
            .WithReservedBits(5, 3)
            .WithTag("FORMAT", 8, 3)
            .WithReservedBits(11, 21);

        protected DoubleWordRegister GenerateTimingRegister() => new DoubleWordRegister(this)
            .WithReservedBits(0, 16)
            .WithTag("TXDELAY", 16, 2)
            .WithReservedBits(19, 1)
            .WithTag("CSSETUP", 20, 3)
            .WithReservedBits(23, 1)
            .WithTag("ICS", 24, 3)
            .WithReservedBits(27, 1)
            .WithTag("CSHOLD", 28, 3)
            .WithReservedBits(31, 1);

        protected DoubleWordRegister GenerateControlExtendedRegister() => new DoubleWordRegister(this)
            .WithTaggedFlag("DBHALT", 0)
            .WithTaggedFlag("CTSINV", 1)
            .WithTaggedFlag("CTSEN", 2)
            .WithTaggedFlag("RTSINV", 3)
            .WithReservedBits(4, 27)
            .WithTaggedFlag("GPIODELAYXOREN", 31);

        protected DoubleWordRegister GenerateTimeCompare0Register() => new DoubleWordRegister(this)
            .WithTag("TCMPVAL", 0, 8)
            .WithReservedBits(8, 8)
            .WithTag("TSTART", 16, 3)
            .WithReservedBits(19, 1)
            .WithTag("TSTOP", 20, 3)
            .WithReservedBits(23, 1)
            .WithTaggedFlag("RESTARTEN", 24)
            .WithReservedBits(25, 7);

        protected DoubleWordRegister GenerateTimeCompare1Register() => new DoubleWordRegister(this)
            .WithTag("TCMPVAL", 0, 8)
            .WithReservedBits(8, 8)
            .WithTag("TSTART", 16, 3)
            .WithReservedBits(19, 1)
            .WithTag("TSTOP", 20, 3)
            .WithReservedBits(23, 1)
            .WithTaggedFlag("RESTARTEN", 24)
            .WithReservedBits(25, 7);

        protected DoubleWordRegister GenerateTimeCompare2Register() => new DoubleWordRegister(this)
            .WithTag("TCMPVAL", 0, 8)
            .WithReservedBits(8, 8)
            .WithTag("TSTART", 16, 3)
            .WithReservedBits(19, 1)
            .WithTag("TSTOP", 20, 3)
            .WithReservedBits(23, 1)
            .WithTaggedFlag("RESTARTEN", 24)
            .WithReservedBits(25, 7);

        protected DoubleWordRegister GenerateTestRegister() => new DoubleWordRegister(this)
            .WithTaggedFlag("GPIODELAYSTABLE", 0)
            .WithTaggedFlag("GPIODELAYXOR", 1)
            .WithReservedBits(2, 30);

        protected DoubleWordRegister GenerateInterruptFlagRegister() => interruptsManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>();

        protected DoubleWordRegister GenerateInterruptEnableRegister() => interruptsManager.GetInterruptEnableRegister<DoubleWordRegister>();

        protected DoubleWordRegister GenerateInterruptFlagSetRegister() => interruptsManager.GetInterruptSetRegister<DoubleWordRegister>();

        protected DoubleWordRegister GenerateInterruptFlagClearRegister() => interruptsManager.GetInterruptClearRegister<DoubleWordRegister>();

        private void HandleTxBufferData(byte data)
        {
            if(!transmitterEnableFlag.Value)
            {
                this.Log(LogLevel.Warning, "Trying to send data, but the transmitter is disabled: 0x{0:X}", data);
                return;
            }

            transferCompleteFlag.Value = false;
            if(operationModeField.Value == OperationMode.Synchronous)
            {
                if(spiSlaveDevice != null)
                {
                    var result = spiSlaveDevice.Transmit(data);
                    WriteChar(result);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Writing data in synchronous mode, but no device is currently connected.");
                    WriteChar(0x0);
                }
            }
            else
            {
                interruptsManager.SetInterrupt(Interrupt.TransmitBufferLevel);
                TransmitCharacter(data);
                interruptsManager.SetInterrupt(Interrupt.TransmitComplete);
            }
            transferCompleteFlag.Value = true;
        }

        private byte ReadBuffer()
        {
            byte character;
            return TryGetCharacter(out character) ? character : (byte)0;
        }

        private readonly InterruptManager<Interrupt> interruptsManager;
        private IEnumRegisterField<OversamplingMode> oversamplingField;
        private IEnumRegisterField<OperationMode> operationModeField;
        private IEnumRegisterField<Parity> parityBitModeField;
        private IEnumRegisterField<Bits> stopBitsModeField;
        private IValueRegisterField fractionalClockDividerField;
        private IFlagRegisterField transferCompleteFlag;
        private IFlagRegisterField receiveDataValidFlag;
        private IFlagRegisterField receiverEnableFlag;
        private IFlagRegisterField transmitterEnableFlag;
        private readonly uint uartClockFrequency;
        private ISPIPeripheral spiSlaveDevice;
        private BufferState bufferState;

        private const int BufferSize = 3; // with shift register

        private enum OperationMode
        {
            Asynchronous,
            Synchronous
        }

        private enum OversamplingMode
        {
            Times16,
            Times8,
            Times6,
            Times4
        }

        private enum Interrupt
        {
            [Subvector(0)]
            TransmitComplete,
            [Subvector(0), NotSettable]
            TransmitBufferLevel,
            [Subvector(1), NotSettable]
            ReceiveDataValid,
            [Subvector(1)]
            ReceiveBufferFull,
            [Subvector(1)]
            ReceiveOverflow,
            [Subvector(1)]
            ReceiveUnderflow,
            [Subvector(0)]
            TransmitOverflow,
            [Subvector(0)]
            TransmitUnderflow,
            [Subvector(1)]
            ParityError,
            [Subvector(1)]
            FramingError,
            [Subvector(1)]
            MultiProcessorAddressFrame,
            [Subvector(1)]
            SlaveSelectInMasterMode,
            [Subvector(0)]
            CollisionCheckFail,
            [Subvector(0)]
            TransmitIdle,
            [Subvector(1)]
            TimerComparator0,
            [Subvector(1)]
            TimerComparator1,
            [Subvector(1)]
            TimerComparator2
        }
    }
}