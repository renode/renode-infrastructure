//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class RenesasDA14_I2C : SimpleContainer<II2CPeripheral>, IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public RenesasDA14_I2C(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; set; }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.I2CControl.Define(this, 0x0000007f)
                .WithTaggedFlag("I2C_MASTER_MODE", 0)
                .WithTag("I2C_SPEED", 1, 2)
                .WithTaggedFlag("I2C_10BITADDR_SLAVE", 3)
                .WithTaggedFlag("I2C_10BITADDR_MASTER", 4)
                .WithTaggedFlag("I2C_RESTART_EN", 5)
                .WithTaggedFlag("I2C_SLAVE_DISABLE", 6)
                .WithTaggedFlag("I2C_STOP_DET_IFADDRESSED", 7)
                .WithTaggedFlag("I2C_TX_EMPTY_CTRL", 8)
                .WithTaggedFlag("I2C_RX_FIFO_FULL_HLD_CTRL", 9)
                .WithTaggedFlag("I2C_STOP_DET_IF_MASTER_ACTIVE", 10)
                .WithReservedBits(11, 21)
            ;

            Registers.I2CTargetAddress.Define(this, 0x00000055)
                .WithTag("IC_TAR", 0, 10)
                .WithTaggedFlag("GC_OR_START", 10)
                .WithTaggedFlag("SPECIAL", 11)
                .WithReservedBits(12, 20)
            ;

            Registers.I2CSlaveAddress.Define(this, 0x00000055)
                .WithTag("IC_SAR", 0, 10)
                .WithReservedBits(10, 22)
            ;

            Registers.I2CHighSpeedMasterModeCodeAddress.Define(this, 0x00000001)
                .WithTag("I2C_IC_HS_MAR", 0, 3)
                .WithReservedBits(3, 29)
            ;

            Registers.I2CRx_TxDataBufferAndCommand.Define(this)
                .WithTag("I2C_DAT", 0, 8)
                .WithTaggedFlag("I2C_CMD", 8)
                .WithTaggedFlag("I2C_STOP", 9)
                .WithTaggedFlag("I2C_RESTART", 10)
                .WithReservedBits(11, 21)
            ;

            Registers.StandardSpeedI2CClockSCLHighCount.Define(this, 0x00000091)
                .WithTag("IC_SS_SCL_HCNT", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.StandardSpeedI2CClockSCLLowCount.Define(this, 0x000000ab)
                .WithTag("IC_SS_SCL_LCNT", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.FastSpeedI2CClockSCLHighCount.Define(this, 0x0000001a)
                .WithTag("IC_FS_SCL_HCNT", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.FastSpeedI2CClockSCLLowCount.Define(this, 0x00000032)
                .WithTag("IC_FS_SCL_LCNT", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.HighSpeedI2CClockSCLHighCount.Define(this, 0x00000006)
                .WithTag("IC_HS_SCL_HCNT", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.HighSpeedI2CClockSCLLowCount.Define(this, 0x00000010)
                .WithTag("IC_HS_SCL_LCNT", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.I2CInterruptStatus.Define(this)
                .WithTaggedFlag("R_RX_UNDER", 0)
                .WithTaggedFlag("R_RX_OVER", 1)
                .WithTaggedFlag("R_RX_FULL", 2)
                .WithTaggedFlag("R_TX_OVER", 3)
                .WithTaggedFlag("R_TX_EMPTY", 4)
                .WithTaggedFlag("R_RD_REQ", 5)
                .WithTaggedFlag("R_TX_ABRT", 6)
                .WithTaggedFlag("R_RX_DONE", 7)
                .WithTaggedFlag("R_ACTIVITY", 8)
                .WithTaggedFlag("R_STOP_DET", 9)
                .WithTaggedFlag("R_START_DET", 10)
                .WithTaggedFlag("R_GEN_CALL", 11)
                .WithTaggedFlag("R_RESTART_DET", 12)
                .WithTaggedFlag("R_MASTER_ON_HOLD", 13)
                .WithTaggedFlag("R_SCL_STUCK_AT_LOW", 14)
                .WithReservedBits(15, 17)
            ;

            Registers.I2CInterruptMask.Define(this, 0x000008ff)
                .WithTaggedFlag("M_RX_UNDER", 0)
                .WithTaggedFlag("M_RX_OVER", 1)
                .WithTaggedFlag("M_RX_FULL", 2)
                .WithTaggedFlag("M_TX_OVER", 3)
                .WithTaggedFlag("M_TX_EMPTY", 4)
                .WithTaggedFlag("M_RD_REQ", 5)
                .WithTaggedFlag("M_TX_ABRT", 6)
                .WithTaggedFlag("M_RX_DONE", 7)
                .WithTaggedFlag("M_ACTIVITY", 8)
                .WithTaggedFlag("M_STOP_DET", 9)
                .WithTaggedFlag("M_START_DET", 10)
                .WithTaggedFlag("M_GEN_CALL", 11)
                .WithTaggedFlag("M_RESTART_DET", 12)
                .WithTaggedFlag("M_MASTER_ON_HOLD", 13)
                .WithTaggedFlag("M_SCL_STUCK_AT_LOW", 14)
                .WithReservedBits(15, 17)
            ;

            Registers.I2CRawInterruptStatus.Define(this)
                .WithTaggedFlag("RX_UNDER", 0)
                .WithTaggedFlag("RX_OVER", 1)
                .WithTaggedFlag("RX_FULL", 2)
                .WithTaggedFlag("TX_OVER", 3)
                .WithTaggedFlag("TX_EMPTY", 4)
                .WithTaggedFlag("RD_REQ", 5)
                .WithTaggedFlag("TX_ABRT", 6)
                .WithTaggedFlag("RX_DONE", 7)
                .WithTaggedFlag("ACTIVITY", 8)
                .WithTaggedFlag("STOP_DET", 9)
                .WithTaggedFlag("START_DET", 10)
                .WithTaggedFlag("GEN_CALL", 11)
                .WithTaggedFlag("RESTART_DET", 12)
                .WithTaggedFlag("MASTER_ON_HOLD", 13)
                .WithTaggedFlag("SCL_STUCK_AT_LOW", 14)
                .WithReservedBits(15, 17)
            ;

            Registers.I2CReceiveFIFOThreshold.Define(this)
                .WithTag("RX_TL", 0, 5)
                .WithReservedBits(5, 27)
            ;

            Registers.I2CTransmitFIFOThreshold.Define(this)
                .WithTag("TX_TL", 0, 5)
                .WithReservedBits(5, 27)
            ;

            Registers.ClearCombinedAndIndividualInterrupt.Define(this)
                .WithTaggedFlag("CLR_INTR", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearRX_UNDERInterrupt.Define(this)
                .WithTaggedFlag("CLR_RX_UNDER", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearRX_OVERInterrupt.Define(this)
                .WithTaggedFlag("CLR_RX_OVER", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearTX_OVERInterrupt.Define(this)
                .WithTaggedFlag("CLR_TX_OVER", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearRD_REQInterrupt.Define(this)
                .WithTaggedFlag("CLR_RD_REQ", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearTX_ABRTInterrupt.Define(this)
                .WithTaggedFlag("CLR_TX_ABRT", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearRX_DONEInterrupt.Define(this)
                .WithTaggedFlag("CLR_RX_DONE", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearACTIVITYInterrupt.Define(this)
                .WithTaggedFlag("CLR_ACTIVITY", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearSTOP_DETInterrupt.Define(this)
                .WithTaggedFlag("CLR_STOP_DET", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearSTART_DETInterrupt.Define(this)
                .WithTaggedFlag("CLR_START_DET", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.ClearGEN_CALLInterrupt.Define(this)
                .WithTaggedFlag("CLR_GEN_CALL", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.I2CEnable.Define(this)
                .WithTaggedFlag("I2C_EN", 0)
                .WithTaggedFlag("I2C_ABORT", 1)
                .WithTaggedFlag("I2C_TX_CMD_BLOCK", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.I2CStatus.Define(this, 0x00000006)
                .WithTaggedFlag("I2C_ACTIVITY", 0)
                .WithTaggedFlag("TFNF", 1)
                .WithTaggedFlag("TFE", 2)
                .WithTaggedFlag("RFNE", 3)
                .WithTaggedFlag("RFF", 4)
                .WithTaggedFlag("MST_ACTIVITY", 5)
                .WithTaggedFlag("SLV_ACTIVITY", 6)
                .WithTaggedFlag("MST_HOLD_TX_FIFO_EMPTY", 7)
                .WithTaggedFlag("MST_HOLD_RX_FIFO_FULL", 8)
                .WithTaggedFlag("SLV_HOLD_TX_FIFO_EMPTY", 9)
                .WithTaggedFlag("LV_HOLD_RX_FIFO_FULL", 10)
                .WithReservedBits(11, 21)
            ;

            Registers.I2CTransmitFIFOLevel.Define(this)
                .WithTag("TXFLR", 0, 6)
                .WithReservedBits(6, 26)
            ;

            Registers.I2CReceiveFIFOLevel.Define(this)
                .WithTag("RXFLR", 0, 6)
                .WithReservedBits(6, 26)
            ;

            Registers.I2CSDAHoldTimeLength.Define(this, 0x00000001)
                .WithTag("I2C_SDA_TX_HOLD", 0, 16)
                .WithTag("I2C_SDA_RX_HOLD", 16, 8)
                .WithReservedBits(24, 8)
            ;

            Registers.I2CTransmitAbortSource.Define(this)
                .WithTaggedFlag("ABRT_7B_ADDR_NOACK", 0)
                .WithTaggedFlag("ABRT_10ADDR1_NOACK", 1)
                .WithTaggedFlag("ABRT_10ADDR2_NOACK", 2)
                .WithTaggedFlag("ABRT_TXDATA_NOACK", 3)
                .WithTaggedFlag("ABRT_GCALL_NOACK", 4)
                .WithTaggedFlag("ABRT_GCALL_READ", 5)
                .WithTaggedFlag("ABRT_HS_ACKDET", 6)
                .WithTaggedFlag("ABRT_SBYTE_ACKDET", 7)
                .WithTaggedFlag("ABRT_HS_NORSTRT", 8)
                .WithTaggedFlag("ABRT_SBYTE_NORSTRT", 9)
                .WithTaggedFlag("ABRT_10B_RD_NORSTRT", 10)
                .WithTaggedFlag("ABRT_MASTER_DIS", 11)
                .WithTaggedFlag("ARB_LOST", 12)
                .WithTaggedFlag("ABRT_SLVFLUSH_TXFIFO", 13)
                .WithTaggedFlag("ABRT_SLV_ARBLOST", 14)
                .WithTaggedFlag("ABRT_SLVRD_INTX", 15)
                .WithTaggedFlag("ABRT_USER_ABRT", 16)
                .WithReservedBits(17, 15)
            ;

            Registers.DMAControl.Define(this)
                .WithTaggedFlag("RDMAE", 0)
                .WithTaggedFlag("TDMAE", 1)
                .WithReservedBits(2, 30)
            ;

            Registers.DMATransmitDataLevel.Define(this)
                .WithTag("DMATDL", 0, 5)
                .WithReservedBits(5, 27)
            ;

            Registers.I2CReceiveDataLevel.Define(this)
                .WithTag("DMARDL", 0, 5)
                .WithReservedBits(5, 27)
            ;

            Registers.I2CSDASetup.Define(this, 0x00000064)
                .WithTag("SDA_SETUP", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.I2CACKGeneralCall.Define(this)
                .WithTaggedFlag("ACK_GEN_CALL", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.I2CEnableStatus.Define(this)
                .WithTaggedFlag("IC_EN", 0)
                .WithTaggedFlag("SLV_DISABLED_WHILE_BUSY", 1)
                .WithTaggedFlag("SLV_RX_DATA_LOST", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.I2CSSAndFSspikesuppressionlimitSize.Define(this, 0x00000001)
                .WithTag("I2C_FS_SPKLEN", 0, 8)
                .WithReservedBits(8, 24)
            ;

            Registers.I2CHSspikesuppressionlimitSize.Define(this, 0x00000001)
                .WithTag("I2C_HS_SPKLEN", 0, 8)
                .WithReservedBits(8, 24)
            ;
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(false);
        }

        private enum Registers
        {
            I2CControl = 0x0,
            I2CTargetAddress = 0x4,
            I2CSlaveAddress = 0x8,
            I2CHighSpeedMasterModeCodeAddress = 0xC,
            I2CRx_TxDataBufferAndCommand = 0x10,
            StandardSpeedI2CClockSCLHighCount = 0x14,
            StandardSpeedI2CClockSCLLowCount = 0x18,
            FastSpeedI2CClockSCLHighCount = 0x1C,
            FastSpeedI2CClockSCLLowCount = 0x20,
            HighSpeedI2CClockSCLHighCount = 0x24,
            HighSpeedI2CClockSCLLowCount = 0x28,
            I2CInterruptStatus = 0x2C,
            I2CInterruptMask = 0x30,
            I2CRawInterruptStatus = 0x34,
            I2CReceiveFIFOThreshold = 0x38,
            I2CTransmitFIFOThreshold = 0x3C,
            ClearCombinedAndIndividualInterrupt = 0x40,
            ClearRX_UNDERInterrupt = 0x44,
            ClearRX_OVERInterrupt = 0x48,
            ClearTX_OVERInterrupt = 0x4C,
            ClearRD_REQInterrupt = 0x50,
            ClearTX_ABRTInterrupt = 0x54,
            ClearRX_DONEInterrupt = 0x58,
            ClearACTIVITYInterrupt = 0x5C,
            ClearSTOP_DETInterrupt = 0x60,
            ClearSTART_DETInterrupt = 0x64,
            ClearGEN_CALLInterrupt = 0x68,
            I2CEnable = 0x6C,
            I2CStatus = 0x70,
            I2CTransmitFIFOLevel = 0x74,
            I2CReceiveFIFOLevel = 0x78,
            I2CSDAHoldTimeLength = 0x7C,
            I2CTransmitAbortSource = 0x80,
            DMAControl = 0x88,
            DMATransmitDataLevel = 0x8C,
            I2CReceiveDataLevel = 0x90,
            I2CSDASetup = 0x94,
            I2CACKGeneralCall = 0x98,
            I2CEnableStatus = 0x9C,
            I2CSSAndFSspikesuppressionlimitSize = 0xA0,
            I2CHSspikesuppressionlimitSize = 0xA4,
        }
    }
}
