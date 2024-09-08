//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.MTD;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class SynopsysSSI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        SynopsysSSI(IMachine machine): base(machine)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size
        {
            get
            {
                return 0x118;
            }
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.Control0.Define(this)
                .WithTag("Data Frame Size", 0, 5)
                .WithReservedBits(5, 1)
                .WithTag("Frame Format", 6, 2)
                .WithTaggedFlag("Serial Clock Phase", 8)
                .WithTaggedFlag("Serial Clock Polarity", 9)
                .WithTag("Transfer Mode", 10, 2)
                .WithTaggedFlag("Slave Output Enable", 12)
                .WithTaggedFlag("Shift Register Loop", 13)
                .WithTaggedFlag("Slave Select Toggle Enable", 14)
                .WithReservedBits(15, 1)
                .WithTag("Control Frame Size", 16, 4) 
                .WithReservedBits(20, 2)
                .WithTag("SPI Frame Format", 22, 2)
                .WithTaggedFlag("SPI Hyperbus Frame format enable", 24)
                .WithReservedBits(25, 6)
            ;

            Registers.Control1.Define(this)
                .WithTag("Number of Data Frames", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.SSIEnable.Define(this)
                .WithTaggedFlag("SSI Enable", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.MicrowireControl.Define(this)
                .WithTaggedFlag("Microwire Transfer Mode", 0)
                .WithTaggedFlag("Microwire Control", 1)
                .WithTaggedFlag("Microwire Handshaking", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.SlaveEnable.Define(this)
                .WithTaggedFlags("Slave Select Enable", 0, 32)
            ;

            Registers.BaudRateSelect.Define(this)
                .WithTag("SSI Clock Divider", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.TransmitFIFOThreshold.Define(this)
                .WithTag("Transmit FIFO Threshold", 0, 16)
                .WithTag("Transfer start FIFO level", 16, 16)
            ;

            Registers.ReceiveFIFOThreshold.Define(this)
                .WithTag("Receive FIFO Threshold", 0, 32)
            ;

            Registers.TransmitFIFOLevel.Define(this)
                .WithTag("Transmit FIFO Level", 0, 32)
            ;

            Registers.ReceiveFIFOLevel.Define(this)
                .WithTag("Receive FIFO Level", 0, 32)
            ;

            Registers.Status.Define(this)
                .WithTaggedFlag("SSI Busy", 0)
                .WithTaggedFlag("Transmit FIFO Not Full", 1)
                .WithTaggedFlag("Transmit FIFO Empty", 2)
                .WithTaggedFlag("Receive FIFO Not Empty", 3) 
                .WithTaggedFlag("Receive FIFO Full", 4)
                .WithTaggedFlag("Transmission Error", 5) 
                .WithTaggedFlag("Data Collision Error", 6)
                .WithReservedBits(7, 26)
            ;

            Registers.InterruptMask.Define(this)
                .WithTaggedFlag("Transmit FIFO Empty Interrupt Mask", 0)
                .WithTaggedFlag("Transmit FIFO Overflow Interrupt Mask", 1)
                .WithTaggedFlag("Receive FIFO Underflow Interrupt Mask", 2)
                .WithTaggedFlag("Receive FIFO Overflow Interrupt Mask", 3)
                .WithTaggedFlag("Receive FIFO Full Interrupt Mask", 4)
                .WithTaggedFlag("Multi-Master Contention Interrupt Mask", 5)
                .WithTaggedFlag("XIR Receive FIFO Overflow Interrupt Mask", 6)
                .WithReservedBits(7, 26)
            ;

            Registers.InterruptStatus.Define(this)
                .WithTaggedFlag("Transmit FIFO Empty Interrupt Status", 0)
                .WithTaggedFlag("Transmit FIFO Overflow Interrupt Status", 1)
                .WithTaggedFlag("Receive FIFO Underflow Interrupt Status", 2)
                .WithTaggedFlag("Receive FIFO Overflow Interrupt Status", 3)
                .WithTaggedFlag("Receive FIFO Full Interrupt Status", 4)
                .WithTaggedFlag("Multi-Master Contention Interrupt Status", 5)
                .WithTaggedFlag("XIP Receive FIFO Overflow Interrupt Status", 6) 
                .WithReservedBits(7, 26)
            ;

            Registers.RawInterruptStatus.Define(this)
                .WithTaggedFlag("Transmit FIFO Empty Raw Interrupt Status", 0)
                .WithTaggedFlag("Transmit FIFO Overflow Raw Interrupt Status", 1)
                .WithTaggedFlag("Receive FIFO Underflow Raw Interrupt Status", 2)
                .WithTaggedFlag("Receive FIFO Overflow Raw Interrupt Status", 3)
                .WithTaggedFlag("Receive FIFO Full Raw Interrupt Status", 4)
                .WithTaggedFlag("Multi-Master Contention Raw Interrupt Status", 5) 
                .WithTaggedFlag("XIP Receive FIFO Overflow Raw Interrupt Status", 6)
                .WithReservedBits(7, 26)
            ;

            Registers.TransmitFIFOOverflowInterruptClear.Define(this)
                .WithTaggedFlag("Clear Transmit FIFO Overflow Interrupt", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ReceiveFIFOOverflowInterruptClear.Define(this)
                .WithTaggedFlag("Clear Receive FIFO Overflow Interrupt", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ReceiveFIFOUnderflowInterruptClear.Define(this)
                .WithTaggedFlag("Clear Receive FIFO Underflow Interrupt", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.MultiMasterInterruptClear.Define(this)
                .WithTaggedFlag("Clear Multi-Master Contention Interrupt", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.InterruptClear.Define(this)
                .WithTaggedFlag("Clear Interrupts", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.DMAControl.Define(this)
                .WithTaggedFlag("Receive DMA Enable", 0)
                .WithTaggedFlag("Transmit DMA Enable", 1)
                .WithReservedBits(2, 30)
            ;

            Registers.DMATransmitDataLevel.Define(this)
                .WithTag("Transmit Data Level", 0, 32)
            ;

            Registers.DMAReceiveDataLevel.Define(this)
                .WithTag("Receive Data Level", 0, 32)
            ;

            Registers.Identification.Define(this)
                .WithTag("Identification Code", 0, 32)
            ;

            Registers.VersionID.Define(this)
                .WithTag("Synopsys Component Version", 0, 32)
            ;

            Registers.Data_0.DefineMany(this, 36,
                (register, registerIndex) => 
                    register.WithTag($"Data {registerIndex}", 0, 32)
            );

            Registers.RXSampleDelay.Define(this)
                .WithTag("Receive Data (rxd) Sample Delay", 0, 8)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("Receive Data (rxd) Sampling Edge", 16)
                .WithReservedBits(17, 15)
            ;

            Registers.SPIControl.Define(this)
                .WithTag("Transfer format", 0, 2)
                .WithTag("Length of Address", 2, 4)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("XIP Mode Bits Enable", 7)
                .WithTag("D/Q/O instruction length", 8, 2)
                .WithReservedBits(10, 1)
                .WithTag("D/Q/O Wait Cycles", 11, 5)
                .WithTaggedFlag("SPI DDR Enable", 16)
                .WithTaggedFlag("Instruction DDR Enable", 17)
                .WithTaggedFlag("Read Data Strobe Enable", 18)
                .WithTaggedFlag("Fix DFS for XIP Transfers", 19)
                .WithTaggedFlag("XIP Instruction Enable", 20)
                .WithTaggedFlag("XIP Continuous Transfer Enable", 21)
                .WithReservedBits(22, 2)
                .WithTaggedFlag("SPI Data Mast Enable", 24)
                .WithTaggedFlag("Hypebus Enable rxds Signaling", 25)
                .WithTag("XIP Mode Bits Length", 26, 2)
                .WithReservedBits(28, 1)
                .WithTaggedFlag("Enable XIP Pre-fetch", 29)
                .WithTaggedFlag("Enable Clock Stretching in SPI", 30)
                .WithReservedBits(31, 1)
            ;

            Registers.TransmitDriveEdge.Define(this)
                .WithTag("TXD Drive Edge", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.XIPModeBits.Define(this)
                .WithTag("XIP Mode Bits To Send", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.XIPINCRTransferOpcode.Define(this)
                .WithTag("XIP INCR Transfer Opcode", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.XIPWRAPTransferOpcode.Define(this)
                .WithTag("XIP WRAP Transfer Opcode", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.XIPControl.Define(this)
                .WithTag("SPI Frame Format", 0, 2)
                .WithTag("Address and Instruction Transfer Format", 2, 2)
                .WithTag("Length of Address", 4, 4)
                .WithReservedBits(8, 1)
                .WithTag("D/Q/O Mode Instruction Length", 9, 2)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("XIP Mode Bits Enable", 12)
                .WithTag("D/Q/O Wait Cycles", 13, 5)
                .WithTaggedFlag("XIP Transfers DFS Fix", 18)
                .WithTaggedFlag("SPI DDR Enable", 19)
                .WithTaggedFlag("Instruction DDR Enable", 20)
                .WithTaggedFlag("Read Data Strobe Enable", 21)
                .WithTaggedFlag("XIP Instruction Enable", 22)
                .WithTaggedFlag("XIP Continuous Transfer Enable", 23)
                .WithTaggedFlag("XIP SPI Hybperbus Frame Format Enable", 24)
                .WithTaggedFlag("Hypebus Enable rxds Signaling", 25)
                .WithTag("XIP Mode Bits Length", 26, 2)
                .WithReservedBits(27, 1)
                .WithTaggedFlag("XIP Pre-fetch Enable", 29)
                .WithReservedBits(30, 2)
            ;

            Registers.XIPSlaveEnable.Define(this)
                .WithTaggedFlags("Slave Select Enable", 0, 32)
            ;

            Registers.XIPReceiveFifoOverflowInterruptClear.Define(this)
                .WithTaggedFlag("Clear XIP Receive FIFO Overflow Interrupt", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.XIPTimeOut.Define(this)
                .WithTag("XIP Time Out", 0, 8)
                .WithReservedBits(9, 23)
            ;
        }

        private enum Registers : long
        {
            Control0 = 0x0, // CTRLR0
            Control1 = 0x4, // CTRLR1
            SSIEnable = 0x8, // SSIENR
            MicrowireControl = 0xC, // MWCR
            SlaveEnable = 0x10, // SER
            BaudRateSelect = 0x14, // BAUDR
            TransmitFIFOThreshold = 0x18, // TXFTLR
            ReceiveFIFOThreshold = 0x1C, // RXFTLR
            TransmitFIFOLevel = 0x20, // TXFLR
            ReceiveFIFOLevel = 0x24, // RXFLR
            Status = 0x28, // SR
            InterruptMask = 0x2C, // IMR
            InterruptStatus = 0x30, // ISR
            RawInterruptStatus = 0x34, // RISR
            TransmitFIFOOverflowInterruptClear = 0x38, // TXOICR
            ReceiveFIFOOverflowInterruptClear = 0x3C, // RXOICR
            ReceiveFIFOUnderflowInterruptClear = 0x40, // RXUICR
            MultiMasterInterruptClear = 0x44, // MSTICR
            InterruptClear = 0x48, // ICR
            DMAControl = 0x4C, // DMACR
            DMATransmitDataLevel = 0x50, // DMATDLR
            DMAReceiveDataLevel = 0x54, // DMARDLR
            Identification = 0x58, // IDR
            VersionID = 0x5C, // SSIC_VERSION_ID
            Data_0 = 0x60, // DRx (0x60 + i*0x4, 0 <= i <= 35)
            RXSampleDelay = 0xF0, // RX_SAMPLE_DELAY
            SPIControl = 0xF4, // SPI_CTRLR0
            TransmitDriveEdge = 0xF8, // DDR_DRIVE_EDGE
            // XIP
            XIPModeBits = 0xFC, // XIP_MODE_BITS
            XIPINCRTransferOpcode = 0x100, // XIP_INCR_INST
            XIPWRAPTransferOpcode = 0x104, // XIP_WRAP_INST
            XIPControl = 0x108, // XIP_CTRL
            XIPSlaveEnable = 0x10C, // XIP_SER
            XIPReceiveFifoOverflowInterruptClear = 0x110, // XRXOICR
            XIPTimeOut = 0x114, // XIP_CNT_TIME_OUT
        }
    }
}
