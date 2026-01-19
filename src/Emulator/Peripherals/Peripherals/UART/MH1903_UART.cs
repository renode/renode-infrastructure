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
            Registers.DLL_THR_RBR.Define(this)
                .WithValueField(0, 32, name: "DLL_THR_RBR",
                    valueProviderCallback: ReadDLL_THR_RBR,
                    writeCallback: WriteDLL_THR_RBR);

            Registers.DLH_IER.Define(this)
                .WithValueField(0, 32, name: "DLH_IER",
                    valueProviderCallback: ReadDLH_IER,
                    writeCallback: WriteDLH_IER);

            Registers.IIR_FCR.Define(this)
                .WithValueField(0, 32, name: "IIR_FCR",
                    valueProviderCallback: ReadIIR_FCR,
                    writeCallback: WriteIIR_FCR);

            Registers.LCR.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithFlag(7, out DLAB, name: "DLAB") // Divisor Latch Access Bit
                .WithFlag(6, out BC, name: "BC") // Break Control
                .WithFlag(5, out SP, name: "SP") // Stick Parity
                .WithFlag(4, out EPS, name: "EPS") // Even Parity Select
                .WithFlag(3, out PEN, name: "PEN") // Parity Enable
                .WithFlag(2, out STOPBITS, name: "STOP") // Number of Stop Bits
                .WithValueField(0, 2, out DLS, name: "DLS"); // Data Length Select

            Registers.MCR.Define(this)
                .WithReservedBits(7, 25) // Bits 31:7 are reserved
                .WithFlag(6, name: "SIRE") // SIR mode enable
                .WithFlag(5, name: "AFCE") // Auto Flow Control Enable
                .WithFlag(4, name: "LB") // Loopback
                .WithFlag(3, name: "OUT2") // OUT2
                .WithFlag(2, name: "OUT1") // OUT1
                .WithFlag(1, name: "RTS") // Request to Send
                .WithFlag(0, name: "DTR");      // Data Terminal Ready

            Registers.LSR.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithFlag(7, name: "RFE") // Receiver FIFO Error
                .WithFlag(6, name: "TEMT", valueProviderCallback: b => true) // Transmitter Empty
                .WithFlag(5, name: "THRE") // Transmitter Holding Register Empty
                .WithFlag(4, name: "BI") // Break Interrupt
                .WithFlag(3, name: "FE") // Framing Error
                .WithFlag(2, name: "PE") // Parity Error
                .WithFlag(1, name: "OE") // Overrun Error
                .WithFlag(0, out DR, name: "DR");       // Data Ready

            Registers.MSR.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithFlag(7, name: "DCD") // Data Carrier Detect
                .WithFlag(6, name: "RI") // Ring Indicator
                .WithFlag(5, name: "DSR") // Data Set Ready
                .WithFlag(4, name: "CTS") // Clear To Send
                .WithFlag(3, name: "DDCD", mode: FieldMode.ReadToClear) // Delta Data Carrier Detect
                .WithFlag(2, name: "TERI", mode: FieldMode.ReadToClear) // Trailing Edge Ring Indicator
                .WithFlag(1, name: "DDSR", mode: FieldMode.ReadToClear) // Delta Data Set Ready
                .WithFlag(0, name: "DCTS", mode: FieldMode.ReadToClear);    // Delta Clear To Send

            Registers.SCR.Define(this)
                .WithValueField(0, 32, name: "SCR"); // Scratch Register

            // This bit is used for FIFO testing to control whether the FIFO can be accessed by the user.
            // When enabled, the user can write to the receive FIFO and read from the transmit FIFO. When disabled,
            // the user can only access the FIFO through RBR and THR.
            // 0 = FIFO access disabled
            // 1 = FIFO access enabled
            Registers.FAR.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "FAR");     // FIFO Access Register

            Registers.TFR.Define(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithValueField(0, 8, name: "TFRD", mode: FieldMode.Read);  // Transmit FIFO Read Data

            Registers.RFW.Define(this)
                .WithReservedBits(10, 22) // Bits 31:10 are reserved
                .WithFlag(9, name: "RFFE", mode: FieldMode.Write) // Receive FIFO Framing Error
                .WithFlag(8, name: "RFPE", mode: FieldMode.Write) // Receive FIFO Parity Error
                .WithValueField(0, 8, name: "RFWD", mode: FieldMode.Write);  // Receive FIFO Write Data

            Registers.USR.Define(this)
                .WithReservedBits(5, 27) // Bits 31:5 are reserved
                .WithFlag(4, name: "RFF", mode: FieldMode.Read) // Receive FIFO Full
                .WithFlag(3, name: "RFNE", mode: FieldMode.Read) // Receive FIFO Not Empty
                .WithFlag(2, name: "TFE", mode: FieldMode.Read) // Transmit FIFO Empty
                .WithFlag(1, name: "TFNF", mode: FieldMode.Read) // Transmit FIFO Not Full
                .WithFlag(0, name: "BUSY", mode: FieldMode.Read);   // UART Busy

            Registers.TFL.Define(this)
                .WithReservedBits(4, 28) // Bits 31:4 are reserved
                .WithValueField(0, 4, name: "TFL", mode: FieldMode.Read);  // Transmit FIFO Level

            Registers.RFL.Define(this)
                .WithReservedBits(4, 28) // Bits 31:4 are reserved
                .WithValueField(0, 4, name: "RFL", mode: FieldMode.Read);  // Receive FIFO Level

            Registers.SRR.Define(this)
                .WithReservedBits(3, 29) // Bits 31:3 are reserved
                .WithFlag(2, name: "XFR", mode: FieldMode.Write) // XMIT FIFO Reset
                .WithFlag(1, name: "RFR", mode: FieldMode.Write) // RCVR FIFO Reset
                .WithFlag(0, name: "UR", mode: FieldMode.Write);    // UART Reset

            Registers.SRTS.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "SRTS");  // Shadow Request to Send

            Registers.SBCR.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "SBCR");  // Shadow Break Control Bit

            Registers.SDMAM.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "SBCR");  // Shadow DMA Mode

            Registers.SFE.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "SFE");  // Shadow FIFO Enable

            Registers.SRT.Define(this)
                .WithReservedBits(2, 30) // Bits 31:1 are reserved
                .WithValueField(0, 2, name: "SRT");  // Shadow RCVR Trigger

            Registers.STET.Define(this)
                .WithReservedBits(2, 30) // Bits 31:1 are reserved
                .WithFlag(0, name: "SFE");  // Shadow FIFO Enable

            Registers.HTX.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "HTX");  // Halt TX

            Registers.DMASA.Define(this)
                .WithReservedBits(1, 31) // Bits 31:1 are reserved
                .WithFlag(0, name: "DMASA", mode: FieldMode.Write);  // DMA Software Acknowledge

            THR = new DoubleWordRegister(this)
                .WithValueField(0, 8, mode: FieldMode.Write, name: "THR", writeCallback: (offset, value) =>
                {
                    CharReceived?.Invoke((byte)value);
                })
                .WithReservedBits(8, 24);

            RBR = new DoubleWordRegister(this)
                .WithValueField(0, 8, mode: FieldMode.Read, name: "RBR", valueProviderCallback: (arg) =>
                {
                    uint value = 0;
                    if(receiveFifo.Count > 0)
                        value = receiveFifo.Dequeue();

                    DR.Value = receiveFifo.Count > 0;
                    return value;
                })
                .WithReservedBits(8, 24);

            DLH = new DoubleWordRegister(this)
                .WithValueField(0, 8, name: "DLH")
                .WithReservedBits(8, 24);

            DLL = new DoubleWordRegister(this)
                .WithValueField(0, 8, name: "DLL")
                .WithReservedBits(8, 24);

            IER = new DoubleWordRegister(this)
                .WithFlag(7, name: "PTIME") // Programmable THRE Interrupt Mode Enable
                .WithReservedBits(4, 3) // Bits 6:4 are reserved
                .WithFlag(3, name: "EDSSI") // Enable Modem Status Interrupt
                .WithFlag(2, name: "ELSI") // Enable Receiver Line Status Interrupt
                .WithFlag(1, name: "ETBEI") // Enable Transmit Holding Register Empty Interrupt
                .WithFlag(0, name: "ERBFI"); // Enable Received Data Available Interrupt

            FCR = new DoubleWordRegister(this)
                .WithReservedBits(8, 24) // Bits 31:8 are reserved
                .WithValueField(6, 2, name: "RCVR") // Receiver trigger level
                .WithValueField(4, 2, name: "TET") // TX Empty trigger
                .WithFlag(3, name: "DMAM") // DMA Mode
                .WithFlag(2, name: "XFIFOR") // XMIT FIFO Reset
                .WithFlag(1, name: "RFIFOR") // RCVR FIFO Reset
                .WithFlag(0, name: "FIFOE"); // FIFO Enable

            IIR = new DoubleWordRegister(this, resetValue: 0x00000001)
                .WithFlag(0, name: "IIR")
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
            DLL_THR_RBR = 0x00,   // Divisor Latch Low / Transmit Holding Register / Receive Buffer Register
            DLH_IER = 0x04,       // Divisor Latch High / Interrupt Enable Register
            IIR_FCR = 0x08,       // Interrupt Identification Register / FIFO Control Register
            LCR = 0x0C,           // Line Control Register
            MCR = 0x10,           // Modem Control Register
            LSR = 0x14,           // Line Status Register
            MSR = 0x18,           // Modem Status Register
            SCR = 0x1C,           // Scratch Register

            // Reserved registers at 0x20, 0x24, 0x28, 0x2C

            // Shadow registers - just defining the first one (rest will be array-indexed)
            // SRBR_STHR = 0x30,     // Shadow Receive/Transmit Buffer Register

            // Advanced feature registers
            FAR = 0x70,           // FIFO Access Register
            TFR = 0x74,           // Transmit FIFO Read
            RFW = 0x78,           // Receive FIFO Write
            USR = 0x7C,           // UART Status Register
            TFL = 0x80,           // Transmit FIFO Level
            RFL = 0x84,           // Receive FIFO Level
            SRR = 0x88,           // Software Reset Register
            SRTS = 0x8C,          // Shadow Request to Send
            SBCR = 0x90,          // Shadow Break Control Register
            SDMAM = 0x94,         // Shadow DMA Mode
            SFE = 0x98,           // Shadow FIFO Enable
            SRT = 0x9C,           // Shadow RCVR Trigger
            STET = 0xA0,          // Shadow TX Empty Trigger
            HTX = 0xA4,           // Half TX FIFO Trigger
            DMASA = 0xA8,         // DMA Software Access Register
        }
    }
}