//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.UART
{
    public class RenesasRA_SCI : BasicDoubleWordPeripheral
    {
        public RenesasRA_SCI(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        private void DefineRegisters()
        {
            Registers.SerialMode.Define(this)  //  for Non-Smart Card Interface Mode (SCMR.SMIF = 0)

                .WithTag("CKS", 0, 2)
                .WithTaggedFlag("MP", 2)
                .WithTaggedFlag("STOP", 3)
                .WithTaggedFlag("PM", 4)
                .WithTaggedFlag("PE", 5)
                .WithTaggedFlag("CHR", 6)
                .WithTaggedFlag("CM", 7)
                .WithReservedBits(8, 24);

            /*            Registers.SerialMode.Define(this)  //  for Smart Card Interface Mode (SCMR.SMIF = 1)

                            .WithTag("CKS", 0, 2)
                            .WithTag("BCP", 2, 2)
                            .WithTaggedFlag("PM", 4)
                            .WithTaggedFlag("PE", 5)
                            .WithTaggedFlag("BLK", 6)
                            .WithTaggedFlag("GM", 7)
                            .WithReservedBits(8, 24);
            */
            Registers.BitRate.Define(this, 0xff)
                .WithTag("BRR", 0, 8)
                .WithReservedBits(8, 24);

            Registers.SerialControl.Define(this)  //  for Non-Smart Card Interface Mode (SCMR.SMIF = 0)

                .WithTag("CKE", 0, 2)
                .WithTaggedFlag("TEIE", 2)
                .WithTaggedFlag("MPIE", 3)
                .WithTaggedFlag("RE", 4)
                .WithTaggedFlag("TE", 5)
                .WithTaggedFlag("RIE", 6)
                .WithTaggedFlag("TIE", 7)
                .WithReservedBits(8, 24);

            /*            Registers.SerialControl.Define(this)  //  for Smart Card Interface Mode (SCMR.SMIF = 1)
                            .WithTag("CKE", 0, 2)
                            .WithTaggedFlag("TEIE", 2)
                            .WithTaggedFlag("MPIE", 3)
                            .WithTaggedFlag("RE", 4)
                            .WithTaggedFlag("TE", 5)
                            .WithTaggedFlag("RIE", 6)
                            .WithTaggedFlag("TIE", 7)
                            .WithReservedBits(8, 24);
            */
            Registers.TransmitData.Define(this, 0xff)
                .WithTag("TDR", 0, 8)
                .WithReservedBits(8, 24);

            Registers.SerialStatus.Define(this, 0x84)  //  for Non-Smart Card Interface and Non-FIFO Mode (SCMR.SMIF = 0, FCR.FM = 0, and MMR.MANEN = 0)

                .WithTaggedFlag("MPBT", 0)
                .WithTaggedFlag("MPB", 1)
                .WithTaggedFlag("TEND", 2)
                .WithTaggedFlag("PER", 3)
                .WithTaggedFlag("FER", 4)
                .WithTaggedFlag("ORER", 5)
                .WithTaggedFlag("RDRF", 6)
                .WithTaggedFlag("TDRE", 7)
                .WithReservedBits(8, 24);

            /*            Registers.SerialStatus.Define(this, 0x80)  //  for Non-Smart Card Interface and FIFO Mode (SCMR.SMIF = 0, FCR.FM = 1, and MMR.MANEN = 0)

                            .WithTaggedFlag("DR", 0)
                            .WithReservedBits(1, 1)
                            .WithTaggedFlag("TEND", 2)
                            .WithTaggedFlag("PER", 3)
                            .WithTaggedFlag("FER", 4)
                            .WithTaggedFlag("ORER", 5)
                            .WithTaggedFlag("RDF", 6)
                            .WithTaggedFlag("TDFE", 7)
                            .WithReservedBits(8, 24);
            */
            /*            Registers.SerialStatus.Define(this, 0x84)  //  for Smart Card Interface Mode (SCMR.SMIF = 1, and MMR.MANEN = 0)

                            .WithTaggedFlag("MPBT", 0)
                            .WithTaggedFlag("MPB", 1)
                            .WithTaggedFlag("TEND", 2)
                            .WithTaggedFlag("PER", 3)
                            .WithTaggedFlag("ERS", 4)
                            .WithTaggedFlag("ORER", 5)
                            .WithTaggedFlag("RDRF", 6)
                            .WithTaggedFlag("TDRE", 7)
                            .WithReservedBits(8, 24);
            */
            Registers.ReceiveData.Define(this)
                .WithTag("RDR", 0, 8)
                .WithReservedBits(8, 24);

            Registers.SmartCardMode.Define(this, 0xf2)
                .WithTaggedFlag("SMIF", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("SINV", 2)
                .WithTaggedFlag("SDIR", 3)
                .WithTaggedFlag("CHR1", 4)
                .WithReservedBits(5, 2)
                .WithTaggedFlag("BCP2", 7)
                .WithReservedBits(8, 24);

            Registers.SerialExtendedMode.Define(this)
                .WithTaggedFlag("ACS0", 0)
                .WithTaggedFlag("PADIS", 1)
                .WithTaggedFlag("BRME", 2)
                .WithTaggedFlag("ABCSE", 3)
                .WithTaggedFlag("ABCS", 4)
                .WithTaggedFlag("NFEN", 5)
                .WithTaggedFlag("BGDM", 6)
                .WithTaggedFlag("RXDESEL", 7)
                .WithReservedBits(8, 24);

            Registers.NoiseFilterSetting.Define(this)
                .WithTag("NFCS", 0, 3)
                .WithReservedBits(3, 29);

            Registers.IICMode1.Define(this)
                .WithTaggedFlag("IICM", 0)
                .WithReservedBits(1, 2)
                .WithTag("IICDL", 3, 5)
                .WithReservedBits(8, 24);

            Registers.IICMode2.Define(this)
                .WithTaggedFlag("IICINTM", 0)
                .WithTaggedFlag("IICCSC", 1)
                .WithReservedBits(2, 3)
                .WithTaggedFlag("IICACKT", 5)
                .WithReservedBits(6, 26);

            Registers.IICMode3.Define(this)
                .WithTaggedFlag("IICSTAREQ", 0)
                .WithTaggedFlag("IICRSTAREQ", 1)
                .WithTaggedFlag("IICSTPREQ", 2)
                .WithTaggedFlag("IICSTIF", 3)
                .WithTag("IICSDAS", 4, 2)
                .WithTag("IICSCLS", 6, 2)
                .WithReservedBits(8, 24);

            Registers.IICStatus.Define(this)
                .WithTaggedFlag("IICACKR", 0)
                .WithReservedBits(1, 31);

            Registers.SPIMode.Define(this)
                .WithTaggedFlag("SSE", 0)
                .WithTaggedFlag("CTSE", 1)
                .WithTaggedFlag("MSS", 2)
                .WithTaggedFlag("CTSPEN", 3)
                .WithTaggedFlag("MFF", 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("CKPOL", 6)
                .WithTaggedFlag("CKPH", 7)
                .WithReservedBits(8, 24);

            /*            Registers.TransmitData.Define(this, 0xffff)  //  for Non-Manchester mode (MMR.MANEN = 0)
                            .WithTag("TDAT", 0, 9)
                            .WithReservedBits(9, 23);
            */
            /*            Registers.TransmitFIFOData.Define(this, 0xffff)
                            .WithTag("TDAT", 0, 9)
                            .WithTaggedFlag("MPBT", 9)
                            .WithReservedBits(10, 22);
            */
            /*            Registers.TransmitFIFOData.Define(this, 0xff)
                            .WithReservedBits(0, 1)
                            .WithTaggedFlag("MPBT", 1)
                            .WithReservedBits(2, 30);
            */
            Registers.TransmitFIFOData.Define(this, 0xff)
                .WithTag("TDAT", 0, 8)
                .WithReservedBits(8, 24);


            /*            Registers.ReceiveData.Define(this)  //  for Non-Manchester mode (MMR.MANEN = 0)
                            .WithTag("RDAT", 0, 9)
                            .WithReservedBits(9, 23);
            */

            /*            Registers.ReceiveFIFOData.Define(this)
                            .WithTag("RDAT", 0, 9)
                            .WithTaggedFlag("MPB", 9)
                            .WithTaggedFlag("DR", 10)
                            .WithTaggedFlag("PER", 11)
                            .WithTaggedFlag("FER", 12)
                            .WithTaggedFlag("ORER", 13)
                            .WithTaggedFlag("RDF", 14)
                            .WithReservedBits(15, 17);

                        Registers.ReceiveFIFOData.Define(this)
                            .WithReservedBits(0, 1)
                            .WithTaggedFlag("MPB", 1)
                            .WithTaggedFlag("DR", 2)
                            .WithTaggedFlag("PER", 3)
                            .WithTaggedFlag("FER", 4)
                            .WithTaggedFlag("ORER", 5)
                            .WithTaggedFlag("RDF", 6)
                            .WithReservedBits(7, 25);

                        Registers.ReceiveFIFOData.Define(this)
                            .WithTag("RDAT", 0, 8)
                            .WithReservedBits(8, 24);
            */

            Registers.ModulationDuty.Define(this, 0xff)
                .WithTag("MDDR", 0, 8)
                .WithReservedBits(8, 24);

            Registers.DataCompareMatchControl.Define(this, 0x40)
                .WithTaggedFlag("DCMF", 0)
                .WithReservedBits(1, 2)
                .WithTaggedFlag("DPER", 3)
                .WithTaggedFlag("DFER", 4)
                .WithReservedBits(5, 1)
                .WithTaggedFlag("IDSEL", 6)
                .WithTaggedFlag("DCME", 7)
                .WithReservedBits(8, 24);

            Registers.FIFOControl.Define(this, 0xf800)
                .WithTaggedFlag("FM", 0)
                .WithTaggedFlag("RFRST", 1)
                .WithTaggedFlag("TFRST", 2)
                .WithTaggedFlag("DRES", 3)
                .WithTag("TTRG", 4, 4)
                .WithTag("RTRG", 8, 4)
                .WithTag("RSTRG", 12, 4)
                .WithReservedBits(16, 16);

            Registers.FIFODataCount.Define(this)
                .WithTag("R", 0, 5)
                .WithReservedBits(5, 3)
                .WithTag("T", 8, 5)
                .WithReservedBits(13, 19);

            Registers.LineStatus.Define(this)
                .WithTaggedFlag("ORER", 0)
                .WithReservedBits(1, 1)
                .WithTag("FNUM", 2, 5)
                .WithReservedBits(7, 1)
                .WithTag("PNUM", 8, 5)
                .WithReservedBits(13, 19);

            Registers.CompareMatchData.Define(this)
                .WithTag("CMPD", 0, 9)
                .WithReservedBits(9, 23);

            Registers.SerialPort.Define(this, 0x03)
                .WithTaggedFlag("RXDMON", 0)
                .WithTaggedFlag("SPB2DT", 1)
                .WithTaggedFlag("SPB2IO", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("RINV", 4)
                .WithTaggedFlag("TINV", 5)
                .WithTaggedFlag("ASEN", 6)
                .WithTaggedFlag("ATEN", 7)
                .WithReservedBits(8, 24);

            Registers.AdjustmentCommunicationTiming.Define(this)
                .WithTag("AST", 0, 3)
                .WithTaggedFlag("AJD", 3)
                .WithTag("ATT", 4, 3)
                .WithTaggedFlag("AET", 7)
                .WithReservedBits(8, 24);
        }

        private enum Registers
        {
            SerialMode = 0x0,
            BitRate = 0x4,
            SerialControl = 0x8,
            TransmitData = 0xc,
            SerialStatus = 0x10,
            ReceiveData = 0x14,
            SmartCardMode = 0x18,
            SerialExtendedMode = 0x1c,
            NoiseFilterSetting = 0x20,
            IICMode1 = 0x24,
            IICMode2 = 0x28,
            IICMode3 = 0x2c,
            IICStatus = 0x30,
            SPIMode = 0x34,
            // TransmitData = 0x38,
            // TransmitFIFOData = 0x38,
            TransmitFIFOData = 0x3c,
            ReceiveFIFOData = 0x40,
            // ReceiveData = 0x40,
            // ReceiveFIFOData = 0x40,
            // ReceiveFIFOData = 0x44,
            ModulationDuty = 0x48,
            DataCompareMatchControl = 0x4c,
            FIFOControl = 0x50,
            FIFODataCount = 0x58,
            LineStatus = 0x60,
            CompareMatchData = 0x68,
            SerialPort = 0x70,
            AdjustmentCommunicationTiming = 0x74,
        }
    }
}

