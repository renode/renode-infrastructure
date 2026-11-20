using System;
using System.Collections.Generic;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class MH1903_UART : BasicDoubleWordPeripheral, IKnownSize, IUART
    {
        public MH1903_UART(IMachine machine, uint pclkFrequency = 8000000) : base(machine)
        {
            this.pclkFrequency = pclkFrequency;
            DefineRegisters();
        }

        public void WriteChar(byte value)
        {
            receiveFifo.Enqueue(value);
            DR.Value = true;
        }

        public long Size => 256;

        public Parity ParityBit
        {
            get
            {
                if(!PEN.Value)
                    return Parity.None;

                if(EPS.Value && SP.Value)
                    return Parity.Forced0;

                if(SP.Value)
                    return Parity.Forced1;

                return EPS.Value ? Parity.Even : Parity.Odd;
            }
        }

        public Bits StopBits
        {
            get
            {
                var b = STOPBITS.Value ? Bits.OneAndAHalf : Bits.One;
                if(DLS.Value == 0 && b == Bits.OneAndAHalf)
                    b = Bits.Two;
                return b;
            }
        }

        public uint BaudRate
        {
            get
            {
                uint divisor = (uint)((DLH.Value << 8) | DLL.Value);
                if(divisor == 0)
                    return 0;

                // Baud rate = PCLK / (16 * Divisor)
                return pclkFrequency / (16 * divisor);
            }
        }

        public GPIO IRQ { get; } = new GPIO();

        [field: Transient]
        public event Action<byte> CharReceived;

        public override void Reset()
        {
            base.Reset();
            receiveFifo.Clear();
            IRQ.Set(false);
        }

        private void DefineRegisters()
        {
            // Depends on DLAB
            Registers.DivisorLatchLow_TransmitHoldingRegister_ReceiveBufferRegister.Define(this)
                .WithValueField(0, 32, name: "DivisorLatchLow_TransmitHoldingRegister_ReceiveBufferRegister",
                    valueProviderCallback: ReadDLL_THR_RBR,
                    writeCallback: WriteDLL_THR_RBR);

            Registers.DivisorLatchHigh_InterruptEnableRegister.Define(this)
                .WithValueField(0, 32, name: "DivisorLatchHigh_InterruptEnableRegister",
                    valueProviderCallback: ReadDLH_IER,
                    writeCallback: WriteDLH_IER);

            Registers.InterruptIdentificationRegister_FifoControlRegister.Define(this)
                .WithValueField(0, 32, name: "InterruptIdentificationRegister_FifoControlRegister",
                    valueProviderCallback: ReadIIR_FCR,
                    writeCallback: WriteIIR_FCR);

            Registers.LineControlRegister.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithFlag(7, out DLAB, name: "DivisorLatchAccessBit") // Divisor Latch Access Bit
                .WithFlag(6, out BC, name: "BreakControl") // Break Control
                .WithFlag(5, out SP, name: "StickParity") // Stick Parity
                .WithFlag(4, out EPS, name: "EvenParitySelect") // Even Parity Select
                .WithFlag(3, out PEN, name: "ParityEnable") // Parity Enable
                .WithFlag(2, out STOPBITS, name: "StopBits") // Number of Stop Bits
                .WithValueField(0, 2, out DLS, name: "DataLengthSelect"); // Data Length Select

            Registers.ModemControlRegister.Define(this)
                .WithReservedBits(7, 25) // Bits 31:7 are reserved
                .WithFlag(6, name: "SerialInfraredEnable") // SIR mode enable
                .WithFlag(5, name: "AutoFlowControlEnable") // Auto Flow Control Enable
                .WithFlag(4, name: "Loopback") // Loopback
                .WithFlag(3, name: "Output2") // OUT2
                .WithFlag(2, name: "Output1") // OUT1
                .WithFlag(1, name: "RequestToSend") // Request to Send
                .WithFlag(0, name: "DataTerminalReady");      // Data Terminal Ready

            Registers.LineStatusRegister.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithFlag(7, name: "ReceiverFifoError") // Receiver FIFO Error
                .WithFlag(6, name: "TransmitterEmpty", valueProviderCallback: b => true) // Transmitter Empty
                .WithFlag(5, name: "TransmitterHoldingRegisterEmpty") // Transmitter Holding Register Empty
                .WithFlag(4, name: "BreakInterrupt") // Break Interrupt
                .WithFlag(3, name: "FramingError") // Framing Error
                .WithFlag(2, name: "ParityError") // Parity Error
                .WithFlag(1, name: "OverrunError") // Overrun Error
                .WithFlag(0, out DR, name: "DataReady");       // Data Ready

            Registers.ModemStatusRegister.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithFlag(7, name: "DataCarrierDetect") // Data Carrier Detect
                .WithFlag(6, name: "RingIndicator") // Ring Indicator
                .WithFlag(5, name: "DataSetReady") // Data Set Ready
                .WithFlag(4, name: "ClearToSend") // Clear To Send
                .WithFlag(3, name: "DeltaDataCarrierDetect", mode: FieldMode.ReadToClear) // Delta Data Carrier Detect
                .WithFlag(2, name: "TrailingEdgeRingIndicator", mode: FieldMode.ReadToClear) // Trailing Edge Ring Indicator
                .WithFlag(1, name: "DeltaDataSetReady", mode: FieldMode.ReadToClear) // Delta Data Set Ready
                .WithFlag(0, name: "DeltaClearToSend", mode: FieldMode.ReadToClear);    // Delta Clear To Send

            Registers.ScratchRegister.Define(this)
                .WithValueField(0, 32, name: "ScratchRegister"); // Scratch Register

            // This bit is used for FIFO testing to control whether the FIFO can be accessed by the user.
            // When enabled, the user can write to the receive FIFO and read from the transmit FIFO. When disabled,
            // the user can only access the FIFO through RBR and THR.
            // 0 = FIFO access disabled
            // 1 = FIFO access enabled
            Registers.FifoAccessRegister.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "FifoAccessRegister");     // FIFO Access Register

            Registers.TransmitFifoReadRegister.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithValueField(0, 8, name: "TransmitFifoReadData", mode: FieldMode.Read);  // Transmit FIFO Read Data

            Registers.ReceiveFifoWriteRegister.Define(this)
                .WithReservedBits(10, 22) // Bits 31:10 are reserved
                .WithFlag(9, name: "ReceiveFifoFramingError", mode: FieldMode.Write) // Receive FIFO Framing Error
                .WithFlag(8, name: "ReceiveFifoParityError", mode: FieldMode.Write) // Receive FIFO Parity Error
                .WithValueField(0, 8, name: "ReceiveFifoWriteData", mode: FieldMode.Write);  // Receive FIFO Write Data

            Registers.UartStatusRegister.Define(this)
                .WithReservedBits(5, 27) // Bits 31:5 are reserved
                .WithFlag(4, name: "ReceiveFifoFull", mode: FieldMode.Read) // Receive FIFO Full
                .WithFlag(3, name: "ReceiveFifoNotEmpty", mode: FieldMode.Read) // Receive FIFO Not Empty
                .WithFlag(2, name: "TransmitFifoEmpty", mode: FieldMode.Read) // Transmit FIFO Empty
                .WithFlag(1, name: "TransmitFifoNotFull", mode: FieldMode.Read) // Transmit FIFO Not Full
                .WithFlag(0, name: "UartBusy", mode: FieldMode.Read);   // UART Busy

            Registers.TransmitFifoLevel.Define(this)
                .WithReservedBits(4, 28) // Bits 31:4 are reserved
                .WithValueField(0, 4, name: "TransmitFifoLevel", mode: FieldMode.Read);  // Transmit FIFO Level

            Registers.ReceiveFifoLevel.Define(this)
                .WithReservedBits(4, 28) // Bits 31:4 are reserved
                .WithValueField(0, 4, name: "ReceiveFifoLevel", mode: FieldMode.Read);  // Receive FIFO Level

            Registers.SoftwareResetRegister.Define(this)
                .WithReservedBits(3, 29) // Bits 31:3 are reserved
                .WithFlag(2, name: "TransmitFifoReset", mode: FieldMode.Write) // XMIT FIFO Reset
                .WithFlag(1, name: "ReceiverFifoReset", mode: FieldMode.Write) // RCVR FIFO Reset
                .WithFlag(0, name: "UartReset", mode: FieldMode.Write);    // UART Reset

            Registers.ShadowRequestToSend.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "ShadowRequestToSend");  // Shadow Request to Send

            Registers.ShadowBreakControlRegister.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "ShadowBreakControlBit");  // Shadow Break Control Bit

            Registers.ShadowDmaMode.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "ShadowDmaMode");  // Shadow DMA Mode

            Registers.ShadowFifoEnable.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "ShadowFifoEnable");  // Shadow FIFO Enable

            Registers.ShadowReceiverTrigger.Define(this)
                .WithReservedBits(2, 30) // Bits 31:1 are reserved
                .WithValueField(0, 2, name: "ShadowReceiverTrigger");  // Shadow RCVR Trigger

            Registers.ShadowTransmitterEmptyTrigger.Define(this)
                .WithReservedBits(2, 30) // Bits 31:1 are reserved
                .WithFlag(0, name: "ShadowFifoEnable");  // Shadow FIFO Enable

            Registers.HaltTransmit.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "HaltTransmit");  // Halt TX

            Registers.DmaSoftwareAcknowledge.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "DmaSoftwareAcknowledge", mode: FieldMode.Write);  // DMA Software Acknowledge

            THR = new DoubleWordRegister(this)
                .WithValueField(0, 8, mode: FieldMode.Write, name: "TransmitHoldingRegister", writeCallback: (offset, value) =>
                {
                    CharReceived?.Invoke((byte)value);
                })
                .WithReservedBits(8, 24);

            RBR = new DoubleWordRegister(this)
                .WithValueField(0, 8, mode: FieldMode.Read, name: "ReceiveBufferRegister", valueProviderCallback: (arg) =>
                {
                    uint value = 0;
                    if(receiveFifo.Count > 0)
                        value = receiveFifo.Dequeue();

                    DR.Value = receiveFifo.Count > 0;
                    return value;
                })
                .WithReservedBits(8, 24);

            DLH = new DoubleWordRegister(this)
                .WithValueField(0, 8, name: "DivisorLatchHigh")
                .WithReservedBits(8, 24);

            DLL = new DoubleWordRegister(this)
                .WithValueField(0, 8, name: "DivisorLatchLow")
                .WithReservedBits(8, 24);

            IER = new DoubleWordRegister(this)
                .WithFlag(7, name: "ProgrammableThreInterruptModeEnable") // Programmable THRE Interrupt Mode Enable
                .WithReservedBits(4, 3) // Bits 6:4 are reserved
                .WithFlag(3, name: "EnableModemStatusInterrupt") // Enable Modem Status Interrupt
                .WithFlag(2, name: "EnableReceiverLineStatusInterrupt") // Enable Receiver Line Status Interrupt
                .WithFlag(1, name: "EnableTransmitHoldingRegisterEmptyInterrupt") // Enable Transmit Holding Register Empty Interrupt
                .WithFlag(0, name: "EnableReceivedDataAvailableInterrupt"); // Enable Received Data Available Interrupt

            FCR = new DoubleWordRegister(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithValueField(6, 2, name: "ReceiverTriggerLevel") // Receiver trigger level
                .WithValueField(4, 2, name: "TransmitterEmptyTrigger") // TX Empty trigger
                .WithFlag(3, name: "DmaMode") // DMA Mode
                .WithFlag(2, name: "TransmitFifoReset") // XMIT FIFO Reset
                .WithFlag(1, name: "ReceiveFifoReset") // RCVR FIFO Reset
                .WithFlag(0, name: "FifoEnable"); // FIFO Enable

            IIR = new DoubleWordRegister(this, resetValue: 0x00000001)
                .WithFlag(0, name: "InterruptIdentificationRegister")
                .WithReservedBits(1, 31);
        }

        private void WriteIIR_FCR(ulong offset, ulong value)
        {
            if(DLAB.Value)
                FCR.Write((uint)offset, (uint)value);
            else
                IIR.Write((uint)offset, (uint)value);
        }

        private ulong ReadIIR_FCR(ulong offset)
        {
            return DLAB.Value ? FCR.Read() : IIR.Read();
        }

        private void WriteDLH_IER(ulong offset, ulong value)
        {
            if(DLAB.Value)
                DLH.Write((uint)offset, (uint)value);
            else
                IER.Write((uint)offset, (uint)value);
        }

        private ulong ReadDLH_IER(ulong offset)
        {
            return DLAB.Value ? DLH.Read() : IER.Read();
        }

        private void WriteDLL_THR_RBR(ulong offset, ulong value)
        {
            if(DLAB.Value)
                DLL.Write((uint)offset, (uint)value);
            else
                THR.Write((uint)offset, (uint)value);
        }

        private ulong ReadDLL_THR_RBR(ulong offset)
        {
            return DLAB.Value ? DLL.Read() : RBR.Read();
        }

        private DoubleWordRegister THR;
        private DoubleWordRegister RBR;
        private DoubleWordRegister DLH;
        private DoubleWordRegister DLL;
        private DoubleWordRegister IER;
        private DoubleWordRegister IIR;
        private DoubleWordRegister FCR;

        private IValueRegisterField DLS;
        private IFlagRegisterField DLAB;
        private IFlagRegisterField BC;
        private IFlagRegisterField SP;
        private IFlagRegisterField EPS;
        private IFlagRegisterField PEN;
        private IFlagRegisterField STOPBITS;
        private IFlagRegisterField DR;

        private readonly Queue<byte> receiveFifo = new Queue<byte>();

        private readonly uint pclkFrequency;

        private enum Registers : long
        {
            // Main registers
            DivisorLatchLow_TransmitHoldingRegister_ReceiveBufferRegister = 0x00,   // Divisor Latch Low / Transmit Holding Register / Receive Buffer Register
            DivisorLatchHigh_InterruptEnableRegister = 0x04,       // Divisor Latch High / Interrupt Enable Register
            InterruptIdentificationRegister_FifoControlRegister = 0x08,       // Interrupt Identification Register / FIFO Control Register
            LineControlRegister = 0x0C,           // Line Control Register
            ModemControlRegister = 0x10,           // Modem Control Register
            LineStatusRegister = 0x14,           // Line Status Register
            ModemStatusRegister = 0x18,           // Modem Status Register
            ScratchRegister = 0x1C,           // Scratch Register

            // Reserved registers at 0x20, 0x24, 0x28, 0x2C

            // Shadow registers - just defining the first one (rest will be array-indexed)
            // SRBR_STHR = 0x30,     // Shadow Receive/Transmit Buffer Register

            // Advanced feature registers
            FifoAccessRegister = 0x70,           // FIFO Access Register
            TransmitFifoReadRegister = 0x74,           // Transmit FIFO Read
            ReceiveFifoWriteRegister = 0x78,           // Receive FIFO Write
            UartStatusRegister = 0x7C,           // UART Status Register
            TransmitFifoLevel = 0x80,           // Transmit FIFO Level
            ReceiveFifoLevel = 0x84,           // Receive FIFO Level
            SoftwareResetRegister = 0x88,           // Software Reset Register
            ShadowRequestToSend = 0x8C,          // Shadow Request to Send
            ShadowBreakControlRegister = 0x90,          // Shadow Break Control Register
            ShadowDmaMode = 0x94,         // Shadow DMA Mode
            ShadowFifoEnable = 0x98,           // Shadow FIFO Enable
            ShadowReceiverTrigger = 0x9C,           // Shadow RCVR Trigger
            ShadowTransmitterEmptyTrigger = 0xA0,          // Shadow TX Empty Trigger
            HaltTransmit = 0xA4,           // Halt TX FIFO Trigger
            DmaSoftwareAcknowledge = 0xA8,         // DMA Software Acknowledge Register
        }
    }
}