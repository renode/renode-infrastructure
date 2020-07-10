//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord)]
    public class NS16550 :  IBytePeripheral, IDoubleWordPeripheral, IUART, IKnownSize
    {
        public NS16550(bool wideRegisters = false)
        {
            mode32 = wideRegisters;
            IRQ = new GPIO();
            Reset();
        }

        public GPIO IRQ { get; private set; }

        public long Size
        {
            get
            {
                return 0x100;
            }
        }

        public void WriteChar(byte value)
        {
            lock(UARTLock)
            {
                if((fifoControl & FifoControl.Enable) != 0 || true)//HACK : fifo always enabled
                {
                    recvFifo.Enqueue(value);
                    lineStatus |= LineStatus.DataReady;
                }
                else
                {
                    if((lineStatus & LineStatus.DataReady) != 0)
                    {
                        lineStatus |= LineStatus.OverrunErrorIndicator;
                    }
                    receiverBuffer = value;
                    lineStatus |= LineStatus.DataReady;
                }
                Update();
            }
        }

        public void WriteByte(long offset, byte value)
        {
            var originalOffset = offset;
            if(mode32 && ((offset % 4) == 0))
            {
                offset = offset / 4;
            }
            lock(UARTLock)
            {
                if((lineControl & LineControl.DivisorLatchAccess) != 0)
                {
                    switch((Register)offset)
                    {
                    case Register.DivisorLatchL:
                        divider = (ushort)((divider & 0xff00) | value);
                        return;

                    case Register.DivisorLatchH:
                        divider = (ushort)((divider & 0x00ff) | (value << 8));
                        return;

                    case Register.PrescalerDivision:
                        prescaler = (byte)(value & 0x0f);
                        return;
                    }
                }

                switch((Register)offset)
                {
                case Register.Data:
                    var handler = CharReceived;
                    if(handler != null)
                    {
                        handler((byte)(value & 0xFF));
                    }

                    transmitNotPending = 0;
                    lineStatus &= ~LineStatus.TransmitHoldEmpty;
                    if((fifoControl & FifoControl.IsEnabled) != 0)
                    {
                        lineStatus &= ~LineStatus.TransmitterEmpty;
                    }
                    Update();

                    if((fifoControl & FifoControl.IsEnabled) == 0 || (interruptEnable & InterruptEnableLevel.ProgrammableTransmitHoldEmptyInterruptMode) == 0)
                    {
                        lineStatus |= LineStatus.TransmitHoldEmpty;
                    }
                    lineStatus |= LineStatus.TransmitterEmpty;
                    transmitNotPending = 1;
                    Update();
                    break;

                case Register.InterruptEnable:
                    interruptEnable = (InterruptEnableLevel)value;
                    if((fifoControl & FifoControl.IsEnabled) != 0 && (interruptEnable & InterruptEnableLevel.ProgrammableTransmitHoldEmptyInterruptMode) != 0)
                    {
                        lineStatus &= ~LineStatus.TransmitHoldEmpty;
                    }

                    if((lineStatus & LineStatus.TransmitHoldEmpty) != 0)
                    {
                        transmitNotPending = 1;
                        Update();
                    }
                    break;

                case Register.FIFOControl:
                    var val = (FifoControl)value;
                    if(fifoControl == val)
                    {
                        break;
                    }
                    /* Did the enable/disable flag change? If so, make sure FIFOs get flushed */
                    if(((val ^ fifoControl) & FifoControl.Enable) != 0)
                    {
                        val |= (FifoControl.TransmitReset | FifoControl.ReceiveReset);
                    }

                    /* FIFO clear */
                    if((val & FifoControl.ReceiveReset) != 0)
                    {
                        recvFifo.Clear();

                    }
                    if((val & FifoControl.TransmitReset) != 0)
                    {
                        //clear xmit
                    }

                    if((val & FifoControl.Enable) != 0)
                    {
                        interruptIdentification |= (InterruptLevel)FifoControl.IsEnabled;
                        /* Set RECV_FIFO trigger Level */
                        switch(val & (FifoControl)0XC0)
                        {
                        case FifoControl.IrqTriggerLevel1:
                            interruptTriggerLevel = 1;
                            break;
                        case FifoControl.IrqTriggerLevel2:
                            interruptTriggerLevel = 4;
                            break;
                        case FifoControl.IrqTriggerLevel3:
                            interruptTriggerLevel = 8;
                            break;
                        case FifoControl.IrqTriggerLevel4:
                            interruptTriggerLevel = 14;
                            break;
                        }
                    }
                    else
                    {
                        interruptIdentification &= (InterruptLevel)unchecked((byte)(~FifoControl.IsEnabled));
                    }
                    /* Set fifoControl - or at least the bits in it that are supposed to "stick" */
                    fifoControl = (val & (FifoControl)0xC9);
                    Update();
                    break;

                case Register.LineControl:
                    lineControl = (LineControl)value;
                    break;

                case Register.ModemControl:
                    modemControl = (ModemControl)(value & 0x1F);
                    break;

                case Register.LineStatus:
                    //Linux should not write here, but it does
                    break;

                case Register.TriggerLevelScratchpad:
                    scratchRegister = value;
                    break;

                case Register.MultiModeControl0:
                    this.Log(LogLevel.Warning, "Unsupported write to LIN mode configuration register at offset 0x{0:X}.", originalOffset);
                    break;
                case Register.MultiModeControl1:
                    this.Log(LogLevel.Warning, "Unsupported write to RZI mode configuration register at offset 0x{0:X}.", originalOffset);
                    break;
                case Register.MultiModeControl2:
                case Register.MultiModeInterruptEnable:
                case Register.TransmitterTimeGuard:
                case Register.ReceiverTimeOut:
                    this.Log(LogLevel.Warning, "Unsupported write to multi-mode configuration register at offset 0x{0:X}.", originalOffset);
                    break;
                case Register.FractionalDivisor:
                case Register.GlitchFilter:
                    this.Log(LogLevel.Warning, "Unsupported write to configuration register at offset 0x{0:X}.", originalOffset);
                    break;

                default:
                    this.LogUnhandledWrite(originalOffset, value);
                    break;
                }

            }
        }

        public byte ReadByte(long offset)
        {
            var originalOffset = offset;
            if(mode32 && ((offset % 4) == 0))
            {
                offset = offset / 4;
            }
            lock(UARTLock)
            {
                byte value = 0x0;
                if((lineControl & LineControl.DivisorLatchAccess) != 0)
                {
                    switch((Register)offset)
                    {
                    case Register.DivisorLatchL:
                        value = (byte)(divider & 0xFF);
                        goto ret;

                    case Register.DivisorLatchH:
                        value = (byte)((divider >> 8) & 0xFF);
                        goto ret;
                    }
                }
                else
                {
                    switch((Register)offset)
                    {
                    case Register.Data:
                        if((fifoControl & FifoControl.Enable) != 0 || true /*HACK*/)
                        {
                            if(recvFifo.Count > 0)
                            {
                                value = recvFifo.Dequeue();
                            }
                            else
                            {
                                value = 0;
                            }
                            if(recvFifo.Count == 0)
                            {
                                lineStatus &= ~(LineStatus.DataReady | LineStatus.BreakIrqIndicator);
                            }
                        }
                        else
                        {
                            value = receiverBuffer;
                            lineStatus &= ~(LineStatus.DataReady | LineStatus.BreakIrqIndicator);
                        }
                        Update();
                        if((modemControl & ModemControl.Loopback) == 0)
                        {
                            /* in loopback mode, don't receive any data */
                        }

                        break;

                    case Register.InterruptEnable:
                        value = (byte)interruptEnable;
                        break;

                    case Register.InterruptIdentification:
                        value = (byte)interruptIdentification;
                        if((value & MaskInterruptId) == (byte)InterruptLevel.TransmitterHoldingRegEmpty)
                        {
                            transmitNotPending = 0;
                            Update();
                        }
                        break;

                    case Register.LineControl:
                        value = (byte)lineControl;
                        break;

                    case Register.ModemControl:
                        //this.DebugLog("modem control read");
                        value = (byte)modemControl;
                        break;

                    case Register.LineStatusHack: //TODO: HACK! Why does it work?
                        goto case Register.LineStatus;

                    case Register.LineStatus:
                        value = (byte)lineStatus;
                        if((lineStatus & (LineStatus.BreakIrqIndicator | LineStatus.OverrunErrorIndicator)) != 0)
                        {
                            lineStatus &= ~(LineStatus.BreakIrqIndicator | LineStatus.OverrunErrorIndicator);
                            Update();
                        }
                        lineStatus &= ~(LineStatus.OverrunErrorIndicator | LineStatus.ParityErrorIndicator | LineStatus.FrameErrorIndicator | LineStatus.BreakIrqIndicator | LineStatus.ReceiverFIFOError);
                        break;

                    case Register.ModemStatusRegister:
                //    this.DebugLog("6 modem control read");
                        if((modemControl & ModemControl.Loopback) != 0)
                        {
                            /* in loopback, the modem output pins are connected to the inputs */
                            value = (byte)(((byte)modemControl & 0x0c) << 4);
                            value |= (byte)(((byte)modemControl & 0x02) << 3);
                            value |= (byte)(((byte)modemControl & 0x01) << 5);
                        }
                        else
                        {
                            value = (byte)modemStatus;
                            /* Clear delta bits & msr int after read, if they were set */
                            if((modemStatus & ModemStatus.AnyDelta) != 0)
                            {
                                modemStatus &= (ModemStatus)0xF0;
                                Update();
                            }
                        }
                        break;

                    case Register.TriggerLevelScratchpad:
                        value = scratchRegister;
                        break;

                    case Register.MultiModeControl0:
                        this.Log(LogLevel.Warning, "Unsupported read from LIN configuration register at offset 0x{0:X}.", originalOffset);
                        break;
                    case Register.MultiModeControl1:
                        this.Log(LogLevel.Warning, "Unsupported read from RZI configuration register at offset 0x{0:X}.", originalOffset);
                        break;
                    case Register.MultiModeControl2:
                    case Register.MultiModeInterruptEnable:
                    case Register.TransmitterTimeGuard:
                    case Register.ReceiverTimeOut:
                        this.Log(LogLevel.Warning, "Unsupported read from multi-mode configuration register at offset 0x{0:X}.", originalOffset);
                        break;
                    case Register.FractionalDivisor:
                    case Register.GlitchFilter:
                        this.Log(LogLevel.Warning, "Unsupported read from configuration register at offset 0x{0:X}.", originalOffset);
                        break;

                    default:
                    this.LogUnhandledRead(originalOffset);
                    break;
                    }
                }
                ret:
                return value;
            }
        }

        public void Reset()
        {
            lock(UARTLock)
            {
                receiverBuffer = 0;
                interruptEnable = 0;
                interruptIdentification = InterruptLevel.NoInterruptsPending;
                lineControl = 0;
                lineStatus = LineStatus.TransmitterEmpty | LineStatus.TransmitHoldEmpty;
                modemStatus = ModemStatus.DataCarrierDetect | ModemStatus.DataSetReady | ModemStatus.ClearToSend;
                divider = 0x0C;
                modemControl = ModemControl.ForceDataCarrierDetect;
                scratchRegister = 0;
                transmitNotPending = 0;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return (uint)ReadByte(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteByte(offset, (byte)(value & 0xFF));
        }

        private void Update()
        {
            var interruptId = InterruptLevel.NoInterruptsPending;
            if(((interruptEnable & InterruptEnableLevel.ReceiverLineStatus) != 0) && ((lineStatus & LineStatus.InterruptAny) != 0))
            {
                interruptId = InterruptLevel.ReceiverLineStatusIrq;
            }
            else if(((interruptEnable & InterruptEnableLevel.ReceiverData) != 0) && ((lineStatus & LineStatus.DataReady) != 0) && (((fifoControl & FifoControl.Enable) == 0) || true /* HACK */ || recvFifo.Count > interruptTriggerLevel))
            {
                interruptId = InterruptLevel.ReceiverDataIrq;
            }
            else if(((interruptEnable & InterruptEnableLevel.TransmitterHoldingReg) != 0) && (transmitNotPending != 0))
            {
                interruptId = InterruptLevel.TransmitterHoldingRegEmpty;
            }
            else if(((interruptEnable & InterruptEnableLevel.ModemStatus) != 0) && ((modemStatus & ModemStatus.AnyDelta) != 0))
            {
                interruptId = InterruptLevel.ModemStatusIrq;
            }

            interruptIdentification = interruptId | (interruptIdentification & (InterruptLevel)0xF0);

            if(interruptId != InterruptLevel.NoInterruptsPending)
            {
                this.NoisyLog("IRQ true");
                IRQ.Set(true);
            }
            else
            {
                this.NoisyLog("IRQ false");
                IRQ.Set(false);
            }
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        private Queue<byte> recvFifo = new Queue<byte>();
        private object UARTLock = new object();

        private InterruptEnableLevel interruptEnable;
        private InterruptLevel interruptIdentification;
        /* read only */
        private LineControl lineControl;
        private ModemControl modemControl;
        private LineStatus lineStatus;
        /* read only */
        private ModemStatus modemStatus;
        /* read only */
        private FifoControl fifoControl;
        private bool mode32;
        private byte scratchRegister;

        private byte interruptTriggerLevel;
        private ushort divider;
        private byte prescaler;
        private byte receiverBuffer;
        /* receive register */
        private int transmitNotPending;

        private const int ReceiveFIFOSize = 16;
        private const byte MaskInterruptId = 0x06;
        /* Mask for the interrupt ID */


        private enum Register:uint
        {
            Data = 0x00,
            DivisorLatchL = 0x00,
            // the same as Data but accessible only when DLAB bit is set
            InterruptEnable = 0x01,
            DivisorLatchH = 0x01,
            // the same as Interrupt enabel but accessible only when DLAB bit is set
            InterruptIdentification = 0x02,
            FIFOControl = 0x02,
            LineControl = 0x03,
            ModemControl = 0x04,
            LineStatus = 0x05,
            PrescalerDivision = 0x05,
            // the same as Line Status but accessible only when DLAB bit is set
            LineStatusHack = 0x14,
            ModemStatusRegister = 0x06,
            TriggerLevelScratchpad = 0x07,

            MultiModeInterruptEnable = 0x09,
            MultiModeControl0 = 0x0C,
            MultiModeControl1 = 0x0D,
            MultiModeControl2 = 0x0E,
            FractionalDivisor = 0x0F,
            GlitchFilter = 0x11,
            TransmitterTimeGuard = 0x12,
            ReceiverTimeOut = 0x13
        }

        [Flags]
        private enum LineControl : byte
        {
            DivisorLatchAccess = 0x80,
            ParityEnable = 0x08,
            EvenParity = 0x10,
            ForceParity = 0x20,
            StopBits = 0x04,
            WordLengthH = 0x02,
            WordLengthL = 0x01
        }

        /*
         * Interrupt trigger levels. The byte-counts are for 16550A - in newer UARTs the byte-count for each ITL is higher.
         */
        [Flags]
        private enum InterruptLevel : byte
        {
            NoInterruptsPending = 0x01,
            TransmitterHoldingRegEmpty = 0x02,
            ReceiverDataIrq = 0x04,
            ReceiverLineStatusIrq = 0x06,
            CharacterTimeoutIndication = 0x0C,
            /*not used at the moment*/
            ModemStatusIrq = 0x00
            /* "Other" irq - NoInterrupts is disabled, but no other bit is enabled, so check in another register */
        }

        [Flags]
        private enum InterruptEnableLevel : byte
        {
            ProgrammableTransmitHoldEmptyInterruptMode = 0x80,
            ModemStatus = 0x08,
            ReceiverLineStatus = 0x04,
            TransmitterHoldingReg = 0x02,
            ReceiverData = 0x01
        }

        /*
         * These are the definitions for the Modem Control Register
         * KKRU: This comment is useful as hell.
         */
        [Flags]
        private enum ModemControl : byte
        {
            Loopback = 0x10,
            ForceDataCarrierDetect = 0x08,
            ForceRingIndicator = 0x04,
            ForceRequestToSend = 0x02,
            ForceDataTerminalReady = 0x01

        }

        /*
         * These are the definitions for the Modem Status Register
         */
        [Flags]
        private enum ModemStatus : byte
        {
            DataCarrierDetect = 0x80,
            RingIndicator = 0x40,
            DataSetReady = 0x20,
            ClearToSend = 0x10,
            DeltaDataCarrierDetect = 0x08,
            TrailingEdgeRingIndicator = 0x04,
            DeltaDataSetReady = 0x02,
            DeltaClearToSend = 0x01,
            AnyDelta = 0x0F
        }


        /*
         * These are the definitions for the Line Status Register
         */
        [Flags]
        private enum LineStatus : byte
        {
            ReceiverFIFOError = 0x80,
            TransmitterEmpty = 0x40,
            TransmitHoldEmpty = 0x20,
            BreakIrqIndicator = 0x10,
            FrameErrorIndicator = 0x08,
            ParityErrorIndicator = 0x04,
            OverrunErrorIndicator = 0x02,
            DataReady = 0x01,
            InterruptAny = 0x1E
        }

        /*
         * These are the definitions for the FIFO Control Register
         */
        [Flags]
        private enum FifoControl : byte
        {
            Enable = 0x01,
            ReceiveReset = 0x02,
            TransmitReset = 0x04,
            DMAModeSelect = 0x08,

            IsEnabledNotFunc = 0x80,
            IsEnabled = 0xC0,

            IrqTriggerLevel1 = 0x00,
            IrqTriggerLevel2 = 0x40,
            IrqTriggerLevel3 = 0x80,
            IrqTriggerLevel4 = 0xC0
        }

        public Bits StopBits
        {
            get
            {
                if((lineControl & LineControl.StopBits) == 0)
                {
                    return Bits.One;
                }
                // is word length is equal to 5? then 1.5 else 2
                return ((byte)lineControl & 3u) == 0 ? Bits.OneAndAHalf : Bits.Two;
            }
        }

        public Parity ParityBit
        {
            get
            {
                if((lineControl & LineControl.ParityEnable) == 0)
                {
                    return Parity.None;
                }
                if((lineControl & LineControl.ForceParity) == 0)
                {
                    return (lineControl & LineControl.EvenParity) == 0 ? Parity.Odd : Parity.Even;
                }
                return (lineControl & LineControl.EvenParity) == 0 ? Parity.Forced1 : Parity.Forced0;
            }
        }

        public uint BaudRate
        {
            get
            {
                var divisor = (16 * (prescaler + 1) * divider);
                return divisor == 0 ? 0 : (uint)(SystemClockFrequency / divisor);
            }
        }

        private const uint SystemClockFrequency = 0;
    }
}

