//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

using SDCardCommand = Antmicro.Renode.Peripherals.SD.SDCard.SdCardCommand;

namespace Antmicro.Renode.Peripherals.SD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class NXP_uSDHC : NullRegistrationPointPeripheralContainer<SDCard>,
        IDoubleWordPeripheral, IKnownSize,
        IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public NXP_uSDHC(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            sysbus = machine.GetSystemBus(this);
            RegistersCollection = new DoubleWordRegisterCollection(this);
            InitializeRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            UpdateInterrupts();
            RegisteredPeripheral?.Reset();
        }

        public GPIO IRQ { get; }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public long Size => 0x10000;

        private void InitializeRegisters()
        {
            Registers.DsAddr.Define(this)
                .WithValueField(0, 32, out dsAddr, name: "DSADDR");

            Registers.BlkAtt.Define(this)
                .WithValueField(0, 13, out transferBlockSize, name: "BLKSIZE")
                .WithReservedBits(13, 3)
                .WithValueField(16, 16, out transferBlockCount, name: "BLKCNT");

            Registers.CmdArg.Define(this)
                .WithValueField(0, 32, out commandArgument, name: "CMDARG");

            Registers.CmdXfrTyp.Define(this)
                .WithReservedBits(0, 16)
                .WithValueField(16, 2, out responseType, name: "RSPTYP")
                .WithReservedBits(18, 1)
                .WithFlag(19, out commandCrcCheckEnable, name: "CCCEN")
                .WithFlag(20, out commandIndexCheckEnable, name: "CICEN")
                .WithFlag(21, out dataPresentSelect, name: "DPSEL")
                .WithValueField(22, 2, out commandType, name: "CMDTYP")
                .WithValueField(24, 6, out commandIndex, name: "CMDINX")
                .WithReservedBits(30, 2)
                .WithWriteCallback((_, __) => ExecuteCommand());

            Registers.CmdRsp0.Define(this)
                .WithValueField(0, 32, out responseFields[0], FieldMode.Read, name: "CMDRSP0");
            Registers.CmdRsp1.Define(this)
                .WithValueField(0, 32, out responseFields[1], FieldMode.Read, name: "CMDRSP1");
            Registers.CmdRsp2.Define(this)
                .WithValueField(0, 32, out responseFields[2], FieldMode.Read, name: "CMDRSP2");
            Registers.CmdRsp3.Define(this)
                .WithValueField(0, 32, out responseFields[3], FieldMode.Read, name: "CMDRSP3");

            Registers.DataBuff.Define(this)
                .WithValueField(0, 32, name: "DATPORT");

            Registers.PresState.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "CIDHB")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false, name: "CICHB")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "DLA")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "SDSTB")
                .WithReservedBits(4, 3)
                .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => !forceSDCardClockEnable.Value, name: "SDOFF")
                .WithReservedBits(8, 2)
                .WithFlag(10, FieldMode.Read, valueProviderCallback: _ => false, name: "BWEN")
                .WithFlag(11, FieldMode.Read, valueProviderCallback: _ => false, name: "BREN")
                .WithReservedBits(12, 4)
                .WithFlag(16, FieldMode.Read, valueProviderCallback: _ => RegisteredPeripheral != null, name: "CINS")
                .WithReservedBits(17, 2)
                .WithFlag(19, FieldMode.Read, valueProviderCallback: _ => RegisteredPeripheral != null, name: "WPSPL")
                .WithReservedBits(20, 4)
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => RegisteredPeripheral != null, name: "DAT0")
                .WithReservedBits(25, 7);

            Registers.ProtCtrl.Define(this)
                .WithValueField(0, 32, name: "PROCTL");

            Registers.SysCtrl.Define(this)
                .WithTaggedFlag("IPGEN", 0)
                .WithTaggedFlag("HCKEN", 1)
                .WithTaggedFlag("PEREN", 2)
                .WithTaggedFlag("SDCLKEN", 3)
                .WithIgnoredBits(4, 4) // SDCLKFS
                .WithTag("DVS", 8, 4)
                .WithReservedBits(12, 4)
                .WithIgnoredBits(16, 8) // DTOCV
                .WithFlag(24, FieldMode.Write, name: "RSTA")
                .WithFlag(25, FieldMode.Write, name: "RSTC")
                .WithFlag(26, FieldMode.Write, name: "RSTD")
                .WithFlag(27, FieldMode.Write, name: "INITA")
                .WithFlag(28, FieldMode.Write, name: "RSTT")
                .WithReservedBits(29, 3);

            Registers.IntStatus.Define(this)
                .WithFlag(0, out commandCompleteStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "CC")
                .WithFlag(1, out transferCompleteStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "TC")
                .WithFlag(2, valueProviderCallback: _ => false) // BGE interrupt not implemented
                .WithFlag(3, out dmaInterruptStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "DINT")
                .WithValueField(4, 12, valueProviderCallback: _ => 0) // Interrupts not implemented
                .WithFlag(16, out commandTimeoutStatus, FieldMode.Read | FieldMode.WriteOneToClear, name: "CTOE")
                .WithValueField(17, 15, valueProviderCallback: _ => 0) // Interrupts not implemented
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.IntStatusEn.Define(this, resetValue: 0xFFFFFFFF)
                .WithValueField(0, 32, name: "IRQSTATEN");

            Registers.IntSignalEn.Define(this)
                .WithFlag(0, out commandCompleteStatusEnable, name: "CCSEN")
                .WithFlag(1, out transferCompleteStatusEnable, name: "TCSEN")
                .WithReservedBits(2, 1)
                .WithFlag(3, out dmaInterruptStatusEnable, name: "DINTSEN")
                .WithReservedBits(4, 12)
                .WithFlag(16, out commandTimeoutStatusEnable, name: "CTOESEN")
                .WithReservedBits(17, 15)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.Autocmd12ErrStatus.Define(this)
                .WithValueField(0, 32, name: "AUTOCM12_ERR_STATUS");

            Registers.HostCtrlCap.Define(this)
                .WithReservedBits(0, 21)
                .WithFlag(21, FieldMode.Read, valueProviderCallback: _ => true, name: "HSSUP")
                .WithFlag(22, FieldMode.Read, valueProviderCallback: _ => false, name: "DMASUP")
                .WithFlag(23, FieldMode.Read, valueProviderCallback: _ => false, name: "SRSUP")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "VS33")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => true, name: "VS30")
                .WithFlag(26, FieldMode.Read, valueProviderCallback: _ => false, name: "VS18")
                .WithReservedBits(27, 5);

            Registers.WtmkLvl.Define(this)
                .WithValueField(0, 32, name: "WML");

            Registers.MixCtrl.Define(this)
                .WithValueField(0, 32, name: "MIXCTRL");

            Registers.VendSpec.Define(this, resetValue: 0x20007809)
                .WithTaggedFlag("EXT_DMA_EN", 0)
                .WithTaggedFlag("VSELECT", 1)
                .WithTaggedFlag("CONFLICT_CHK_EN", 2)
                .WithTaggedFlag("AC12_WR_CHKBUSY_EN", 3)
                .WithIgnoredBits(4, 4)
                .WithFlag(8, out forceSDCardClockEnable, name: "FRC_SDCLK_ON")
                .WithIgnoredBits(9, 6)
                .WithTaggedFlag("CRC_CHK_DIS", 15)
                .WithIgnoredBits(16, 15)
                .WithTaggedFlag("CMD_BYTE_EN", 31);

            Registers.DllCtrl.Define(this)
                .WithValueField(0, 32, name: "DLL_CTRL");

            Registers.DllStatus.Define(this)
                .WithValueField(0, 32, name: "DLL_STATUS");

            Registers.ClkTuneCtrlStatus.Define(this)
                .WithValueField(0, 32, name: "CLK_TUNE_CTRL_STATUS");

            Registers.MmcBoot.Define(this)
                .WithValueField(0, 32, name: "MMC_BOOT");

            Registers.VendSpec2.Define(this)
                .WithValueField(0, 32, name: "VENDORSPEC2");

            Registers.TuningCtrl.Define(this)
                .WithValueField(0, 32, name: "TUNING_CTRL");
        }

        private void ExecuteCommand()
        {
            var commandIndex = (uint)this.commandIndex.Value;
            var commandArgument = (uint)this.commandArgument.Value;
            var responseType = (ResponseType)this.responseType.Value;

            var sdCard = RegisteredPeripheral;
            if(sdCard == null)
            {
                this.WarningLog("Command {0} issued but no SD card attached", commandIndex);
                commandTimeoutStatus.Value = true;
                UpdateInterrupts();
                return;
            }

            var response = sdCard.HandleCommand(commandIndex, commandArgument);

            switch(responseType)
            {
            case ResponseType.Response136:
                PackR2Response(response);
                break;
            case ResponseType.Response48:
            case ResponseType.Response48CheckBusy:
                // Match U-Boot driver: for non-R2 responses, only cmdrsp0 is returned.
                // Upper bits are ignored as they are not reconstructed by the driver.
                responseFields[0].Value = response.AsUInt32(0);
                break;
            case ResponseType.NoResponse:
                break;
            }

            commandCompleteStatus.Value = true;
            UpdateInterrupts();

            if(dataPresentSelect.Value)
            {
                ProcessDataCommand(sdCard, (SDCardCommand)commandIndex);
            }
        }

        // The eSDHC stores the 136-bit R2 frame left-aligned across RSP3:RSP2:RSP1:RSP0.
        // RSP3[31:24] holds the header byte (0x3F - the SD bus start/direction/reserved bits);
        // CSD[127:8] fills the remaining 120 bits; CSD[7:0] falls off the bottom and is discarded
        // (on a real bus those bits carry the CRC7 + end-bit, always 0x01, and are not part of the payload).
        // Software reconstructs CSD[127:0] by un-shifting: CSD_word[n] = (RSP[3-n] << 8) | (RSP[2-n] >> 24).
        private void PackR2Response(BitStream response)
        {
            var c3 = response.AsUInt32(96);  // CSD[127:96]
            var c2 = response.AsUInt32(64);  // CSD[95:64]
            var c1 = response.AsUInt32(32);  // CSD[63:32]
            var c0 = response.AsUInt32(0);   // CSD[31:0]
            responseFields[3].Value = (0x3Fu << 24) | (c3 >> 8);  // RSP3: {0x3F, CSD[127:104]}
            responseFields[2].Value = (c3 << 24) | (c2 >> 8);  // RSP2: CSD[103:72]
            responseFields[1].Value = (c2 << 24) | (c1 >> 8);  // RSP1: CSD[71:40]
            responseFields[0].Value = (c1 << 24) | (c0 >> 8);  // RSP0: CSD[39:8]
        }

        private void ProcessDataCommand(SDCard sdCard, SDCardCommand command)
        {
            var size  = BlockSize;
            var count = BlockCount;
            var dest  = (ulong)dsAddr.Value;

            switch(command)
            {
            case SDCardCommand.ReadSingleBlock_CMD17:
                DmaRead(sdCard, dest, size);
                break;
            case SDCardCommand.ReadMultipleBlocks_CMD18:
                DmaRead(sdCard, dest, size * count);
                break;
            case SDCardCommand.WriteSingleBlock_CMD24:
                DmaWrite(sdCard, dest, size);
                break;
            case SDCardCommand.WriteMultipleBlocks_CMD25:
                DmaWrite(sdCard, dest, size * count);
                break;
            default:
                break;
            }

            transferCompleteStatus.Value = true;
            dmaInterruptStatus.Value = true;
            UpdateInterrupts();
        }

        private void DmaRead(SDCard sdCard, ulong dest, uint size)
        {
            var data = sdCard.ReadData(size);
            sysbus.WriteBytes(data, dest);
        }

        private void DmaWrite(SDCard sdCard, ulong dest, uint size)
        {
            var buf = new byte[size];
            sysbus.ReadBytes(dest, (int)size, buf, 0);
            sdCard.WriteData(buf);
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(
                   (commandCompleteStatus.Value && commandCompleteStatusEnable.Value)
                || (transferCompleteStatus.Value && transferCompleteStatusEnable.Value)
                || (dmaInterruptStatus.Value && dmaInterruptStatusEnable.Value)
                || (commandTimeoutStatus.Value && commandTimeoutStatusEnable.Value)
            );
        }

        private uint BlockSize => Math.Max(1u, (uint)transferBlockSize.Value);

        private uint BlockCount => Math.Max(1u, (uint)transferBlockCount.Value);

        private IValueRegisterField dsAddr;
        private IValueRegisterField transferBlockSize;
        private IValueRegisterField transferBlockCount;
        private IValueRegisterField commandArgument;
        private IFlagRegisterField commandCompleteStatus;
        private IFlagRegisterField transferCompleteStatus;
        private IFlagRegisterField dmaInterruptStatus;
        private IFlagRegisterField commandTimeoutStatus;

        private IFlagRegisterField commandCompleteStatusEnable;
        private IFlagRegisterField transferCompleteStatusEnable;
        private IFlagRegisterField dmaInterruptStatusEnable;
        private IFlagRegisterField commandTimeoutStatusEnable;

        private IValueRegisterField responseType;
        private IFlagRegisterField commandCrcCheckEnable;
        private IFlagRegisterField commandIndexCheckEnable;
        private IFlagRegisterField dataPresentSelect;
        private IValueRegisterField commandType;
        private IValueRegisterField commandIndex;

        private IFlagRegisterField forceSDCardClockEnable;

        private readonly IBusController sysbus;
        private readonly IValueRegisterField[] responseFields = new IValueRegisterField[4];

        private enum ResponseType
        {
            NoResponse          = 0,
            Response136         = 1, // R2
            Response48          = 2, // R1 / R3 / R4 / R6 / R7
            Response48CheckBusy = 3, // R1b
        }

        private enum Registers
        {
            DsAddr             = 0x00, // DS_ADDR
            BlkAtt             = 0x04, // BLK_ATT
            CmdArg             = 0x08, // CMD_ARG
            CmdXfrTyp          = 0x0C, // CMD_XFR_TYP
            CmdRsp0            = 0x10, // CMD_RSP0
            CmdRsp1            = 0x14, // CMD_RSP1
            CmdRsp2            = 0x18, // CMD_RSP2
            CmdRsp3            = 0x1C, // CMD_RSP3
            DataBuff           = 0x20, // DATA_BUFF_ACC_PORT
            PresState          = 0x24, // PRES_STATE
            ProtCtrl           = 0x28, // PROT_CTRL
            SysCtrl            = 0x2C, // SYS_CTRL
            IntStatus          = 0x30, // INT_STATUS
            IntStatusEn        = 0x34, // INT_STATUS_EN
            IntSignalEn        = 0x38, // INT_SIGNAL_EN
            Autocmd12ErrStatus = 0x3C, // AUTOCMD12_ERR_STATUS
            HostCtrlCap        = 0x40, // HOST_CTRL_CAP
            WtmkLvl            = 0x44, // WTMK_LVL
            MixCtrl            = 0x48, // MIX_CTRL
                                       // FORCE_EVENT
                                       // ADMA_ERR_STATUS
                                       // ADMA_SYS_ADDR
            DllCtrl            = 0x60, // DLL_CTRL
            DllStatus          = 0x64, // DLL_STATUS
            ClkTuneCtrlStatus  = 0x68, // CLK_TUNE_CTRL_STATUS
            VendSpec           = 0xC0, // VEND_SPEC
            MmcBoot            = 0xC4, // MCC_BOOT
            VendSpec2          = 0xC8, // VEND_SPEC2
            TuningCtrl         = 0xCC, // TUNING_CTRL
        }
    }
}
