//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CAN;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace Antmicro.Renode.PeripheralsTests
{
    [TestFixture]
    public class STMCANTest
    {
        [Test]
        public void InitTest()
        {
            var stmcan = new STMCAN();
            stmcan.Reset();
            // Registers can only be accessed by words (32 bits)
            // --> check this  
            // #####################################
            // ### Check reset values of CAN control and status registers
            // --- CAN_MCR
            Assert.AreEqual(stmcan.ReadDoubleWord(0x00), 0x00010002);
            // --- CAN_MSR
            Assert.AreEqual(stmcan.ReadDoubleWord(0x04), 0x00000C02);
            // --- CAN_TSR
            Assert.AreEqual(stmcan.ReadDoubleWord(0x08), 0x1C000000);
            // --- CAN_RF0R
            Assert.AreEqual(stmcan.ReadDoubleWord(0x0C), 0x00000000);
            // --- CAN_RF1R
            Assert.AreEqual(stmcan.ReadDoubleWord(0x10), 0x00000000);
            // --- CAN_IER
            Assert.AreEqual(stmcan.ReadDoubleWord(0x14), 0x00000000);
            // --- CAN_ESR
            Assert.AreEqual(stmcan.ReadDoubleWord(0x18), 0x00000000);
            // --- CAN_BTR (Can only be accessed during Initialization mode)
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1C), 0x00000000); // Cannot read
            // Change to Initialization mode by setting INRQ (bit0) in CAN_MCR register 
            stmcan.WriteDoubleWord(0x00, 0x00010001);
            // Verify Initialization mode by checking INAK bit set in CAN_MSR 
            // and that SLAK bit is cleared
            Assert.AreEqual(stmcan.ReadDoubleWord(0x04), 0x00000C01);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1C), 0x01230000);
            // #####################################
            // ### Check CAN mailbox registers
            stmcan.Reset();
            // --- CAN_TIxR - TXRQ reset value 0 (bit0)
            Assert.AreEqual(stmcan.ReadDoubleWord(0x180) & 0x1, 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x190) & 0x1, 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1A0) & 0x1, 0x0);
            // --- CAN_TDTxR - bits 15:9, 7:4 are reserved
            Assert.AreEqual(stmcan.ReadDoubleWord(0x184), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x194), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1A4), 0x0);
            // --- CAN_TDLxR - reset value undefined, model should reset to 0
            Assert.AreEqual(stmcan.ReadDoubleWord(0x188), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x198), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1A8), 0x0);
            // --- CAN_TDHxR - reset value undefined, model should reset to 0
            Assert.AreEqual(stmcan.ReadDoubleWord(0x18C), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x19C), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1AC), 0x0);
            // All RX registers are write protected
            // --- CAN_RIxR - reset value undefined, bit0 reserved
            stmcan.WriteDoubleWord(0x1B0, 0xFF0000FF);
            stmcan.WriteDoubleWord(0x1C0, 0xFF0000FF);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1B0) & 0x1, 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1C0) & 0x1, 0x0);
            // --- CAN_RDTxR - reset value undefined, bit7:4 reserved
            stmcan.WriteDoubleWord(0x1B4, 0xFF0000FF);
            stmcan.WriteDoubleWord(0x1C4, 0xFF0000FF);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1B4) & 0x10, 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1C4) & 0x10, 0x0);
            // --- CAN_RDLxR - reset value undefined, model should reset to 0
            stmcan.WriteDoubleWord(0x1B8, 0xFF0000FF);
            stmcan.WriteDoubleWord(0x1C8, 0xFF0000FF);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1B8), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1C8), 0x0);
            // --- CAN_RDHxR - reset value undefined, model should reset to 0
            stmcan.WriteDoubleWord(0x1BC, 0xFF0000FF);
            stmcan.WriteDoubleWord(0x1CC, 0xFF0000FF);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1BC), 0x0);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1CC), 0x0);
            // #####################################
            // ### Check CAN filter registers
            stmcan.Reset();
            // --- CAN_FMR
            Assert.AreEqual(stmcan.ReadDoubleWord(0x200), 0x2A1C0E01);
            // --- CAN_FM1R - can only be written in filter initialization mode 
            Assert.AreEqual(stmcan.ReadDoubleWord(0x204), 0x00000000);
            stmcan.WriteDoubleWord(0x204, 0xFF0000FF);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x204), 0xFF0000FF);
            stmcan.WriteDoubleWord(0x200, 0x2A1C0E00);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x200), 0x2A1C0E00);
            stmcan.WriteDoubleWord(0x204, 0x00FFFF00);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x204), 0xFF0000FF);
            stmcan.WriteDoubleWord(0x200, 0x2A1C0E01);
            // --- CAN_FS1R
            Assert.AreEqual(stmcan.ReadDoubleWord(0x20C), 0x00000000);
            stmcan.WriteDoubleWord(0x20C, 0xFF0000FF);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x20C), 0xFF0000FF);
            stmcan.WriteDoubleWord(0x200, 0x2A1C0E00);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x200), 0x2A1C0E00);
            stmcan.WriteDoubleWord(0x20C, 0x00FFFF00);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x20C), 0xFF0000FF);
            stmcan.WriteDoubleWord(0x200, 0x2A1C0E01);
            // --- CAN_FFA1R
            Assert.AreEqual(stmcan.ReadDoubleWord(0x214), 0x00000000);
            stmcan.WriteDoubleWord(0x214, 0xFF0000FF);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x214), 0xFF0000FF);
            stmcan.WriteDoubleWord(0x200, 0x2A1C0E00);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x200), 0x2A1C0E00);
            stmcan.WriteDoubleWord(0x214, 0x00FFFF00);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x214), 0xFF0000FF);
            stmcan.WriteDoubleWord(0x200, 0x2A1C0E01);
            // --- CAN_FA1R - bit 31:28 reserved
            Assert.AreEqual(stmcan.ReadDoubleWord(0x21C), 0x00000000);
            stmcan.WriteDoubleWord(0x21C, 0xF0000000);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x21C), 0x00000000);
            // --- CAN_FiRx - 28 filter banks: reset value undefined, model should reset to 0
            // Filter bank registers can only be modified when FACTx bit of CAN_FAxR register is cleared
            // or when FINIT bit in CAN_FMR is set
            int filterIndex = 0;
            do
            {
                Assert.AreEqual(stmcan.ReadDoubleWord(0x240 + filterIndex * 0x8), 0x00000000);
                stmcan.WriteDoubleWord((0x240 + filterIndex * 0x8), 0xFF0000FF);
                Assert.AreEqual(stmcan.ReadDoubleWord(0x240 + filterIndex * 0x8), 0xFF0000FF);
                stmcan.WriteDoubleWord(0x200, 0x2A1C0E00);
                Assert.AreEqual(stmcan.ReadDoubleWord(0x200), 0x2A1C0E00);
                stmcan.WriteDoubleWord((0x240 + filterIndex * 0x8), 0x00FFFF00);
                Assert.AreEqual(stmcan.ReadDoubleWord(0x240 + filterIndex * 0x8), 0x00FFFF00);
                filterIndex++;
            }
            while(filterIndex < 28);

        }

        [Test, Ignore("Ignored")]
        public void LoopbackMessageTest()
        {
            // #####################################
            // ### Declarations
            // CAN TX mailbox identifier register
            uint can_tixr = 0;
            // CAN TX mailbox data length control and time stamp register
            uint can_tdtxr = 0;
            // CAN TX mailbox data low register
            uint can_tdlxr = 0;
            // CAN TX mailbox data high register
            uint can_tdhxr = 0;
            // Message components
            uint stdid = 0x321;
            uint ide = 0; // std = 0; ext = 4
            uint rtr = 0; // data frame = 0 ; remote frame = 2
            uint txrq = 1;
            uint time = 0;
            uint tgt = 0;
            uint dlc = 8; // data length, default to 8 bytes
            byte[] txData = new byte[8] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8 };
            // #####################################
            // ### Setup CAN device
            var stmcan = new STMCAN();
            stmcan.Reset();
            // Init mode 
            stmcan.WriteDoubleWord(0x00, 0x00010000);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x04), 0x00000C00);

            // ### Set filter for message
            // --- CAN_FMR - init mode
            var can_fmr = stmcan.ReadDoubleWord(0x200);
            can_fmr |= 0x1;
            stmcan.WriteDoubleWord(0x200, can_fmr);
            // --- CAN_FA1R - deactivate filter nr 1
            var can_fa1r = stmcan.ReadDoubleWord(0x21C);
            can_fa1r &= 0xFFFFFFFD;
            stmcan.WriteDoubleWord(0x21C, can_fa1r);
            // Filter scale: 32-bit filter 1 identifier
            var can_fs1r = stmcan.ReadDoubleWord(0x20C);
            can_fs1r |= 0x1;
            stmcan.WriteDoubleWord(0x20C, can_fs1r);
            // Filter 1 identifiers = 0x321, 0x123 : set 0,0 currently
            var can_fxr1 = stmcan.ReadDoubleWord(0x248);
            can_fxr1 |= 0x0;
            stmcan.WriteDoubleWord(0x248, can_fxr1);
            var can_fxr2 = stmcan.ReadDoubleWord(0x250);
            can_fxr2 |= 0x0;
            stmcan.WriteDoubleWord(0x250, can_fxr2);
            // --- CAN_FM1R - filter 1 is two 32-bit id list
            var can_fm1r = stmcan.ReadDoubleWord(0x204);
            can_fm1r |= 0x1;
            stmcan.WriteDoubleWord(0x204, can_fm1r);
            // --- CAN_FFA1R - assign FIFO 0
            var can_ffa1r = stmcan.ReadDoubleWord(0x214);
            can_ffa1r &= 0xFFFFFFFD;
            stmcan.WriteDoubleWord(0x214, can_ffa1r);
            // --- CAN_FA1R - activate filter nr 1
            can_fa1r = stmcan.ReadDoubleWord(0x21C);
            can_fa1r |= 0x1;
            stmcan.WriteDoubleWord(0x21C, can_fa1r);

            // Enable loopback, set LBKM, bit30, in CAN_BTR
            uint can_btr = stmcan.ReadDoubleWord(0x01C);
            can_btr |= (1 << 30);
            stmcan.WriteDoubleWord(0x01C, can_btr);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x01C), can_btr);

            // Set Normal mode
            stmcan.WriteDoubleWord(0x00, 0x00010000);
            Assert.AreEqual(stmcan.ReadDoubleWord(0x04), 0x00000C00);

            // #####################################
            // ### Standard Identifier
            // Setup message data registers and write to mailbox 0
            can_tdtxr = (time << 16) | (tgt << 8) | dlc;
            can_tdlxr = (uint)((txData[3] << 24) | (txData[2] << 16) | (txData[1] << 8) | txData[0]);
            can_tdhxr = (uint)((txData[7] << 24) | (txData[6] << 16) | (txData[5] << 8) | txData[4]);
            stmcan.WriteDoubleWord(0x184, can_tdtxr);
            stmcan.WriteDoubleWord(0x188, can_tdlxr);
            stmcan.WriteDoubleWord(0x18C, can_tdhxr);
            // Setup id register and request transmission
            can_tixr = (stdid << 21) | (ide << 2) | (rtr << 1) | txrq;
            stmcan.WriteDoubleWord(0x180, can_tixr);
            // Check RX FIFO 0, fetch message and release fifo
            // CAN_RF0R bit 1:0 indicates pending RX messages
            Assert.AreEqual(stmcan.ReadDoubleWord(0x0C) & 0x3, 0x1);
            //Assert.AreEqual (stmcan.ReadDoubleWord(0x10) & 0x3, 0x1);
            // read id from receive fifo 0 and verify 0x321 in bit 31:21
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1B0) & 0xFFE00000, (stdid << 21));
            // read data from receive fifo 0 and verify 0x321 in bit 31:21
            Assert.AreEqual(stmcan.ReadDoubleWord(0x1B8) & 0xFF, txData[0]);
            // release RFOM0 fifo in CAN_RFR
            // #####################################
            // Extended Identifier
            ide = 4;
            // #####################################
            // Remote Frame
            rtr = 2;
            // #####################################
            // Error
        }

        [Test, Ignore("Ignored")]
        public void AnotherTest()
        {
            var stmcan = new STMCAN();
            stmcan.Reset();
        }
    }
}

