//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2021 Google LLC
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.IO;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.MTD
{
    public class OpenTitan_FlashController : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_FlashController(IMachine machine, MappedMemory flash) : base(machine)
        {
            ProgramEmptyIRQ = new GPIO();
            ProgramLevelIRQ = new GPIO();
            ReadFullIRQ = new GPIO();
            ReadLevelIRQ = new GPIO();
            OperationDoneIRQ = new GPIO();
            CorrectableErrorIRQ = new GPIO();

            RecoverableAlert = new GPIO();
            FatalStandardAlert = new GPIO();
            FatalAlert = new GPIO();
            FatalPrimitiveFlashAlert = new GPIO();
            RecoverablePrimitiveFlashAlert = new GPIO();

            mpRegionEnabled = new IEnumRegisterField<MultiBitBool4>[NumberOfMpRegions];
            mpRegionBase = new IValueRegisterField[NumberOfMpRegions];
            mpRegionSize = new IValueRegisterField[NumberOfMpRegions];
            mpRegionReadEnabled = new IEnumRegisterField<MultiBitBool4>[NumberOfMpRegions];
            mpRegionProgEnabled = new IEnumRegisterField<MultiBitBool4>[NumberOfMpRegions];
            mpRegionEraseEnabled = new IEnumRegisterField<MultiBitBool4>[NumberOfMpRegions];
            mpRegionScrambleEnabled = new IEnumRegisterField<MultiBitBool4>[NumberOfMpRegions];
            mpRegionEccEnabled = new IEnumRegisterField<MultiBitBool4>[NumberOfMpRegions];
            mpRegionHighEnduranceEnabled = new IEnumRegisterField<MultiBitBool4>[NumberOfMpRegions];

            bankInfoPageEnabled = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageReadEnabled = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageProgramEnabled = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageEraseEnabled = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageScrambleEnabled = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageEccEnabled = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageHighEnduranceEnabled = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfBanks, FlashNumberOfInfoTypes][];

            dataFlash = flash;
            // This is required as part of the tests use full address, while the other use just flash offset
            // Ex.
            //    this one uses just offset: https://github.com/lowRISC/opentitan/blob/1e86ba2a238dc26c2111d325ee7645b0e65058e5/sw/device/tests/flash_ctrl_test.c#L70
            //    this one - full address:   https://github.com/lowRISC/opentitan/blob/1e86ba2a238dc26c2111d325ee7645b0e65058e5/sw/device/tests/flash_ctrl_test.c#L224
            flashAddressMask = (uint)(flash.Size - 1);

            for(var bankNumber = 0; bankNumber < FlashNumberOfBanks; ++bankNumber)
            {
                for(var infoType = 0; infoType < FlashNumberOfInfoTypes; ++infoType)
                {
                    bankInfoPageEnabled[bankNumber, infoType] = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageReadEnabled[bankNumber, infoType] = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageProgramEnabled[bankNumber, infoType] = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageEraseEnabled[bankNumber, infoType] = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageScrambleEnabled[bankNumber, infoType] = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageEccEnabled[bankNumber, infoType] = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageHighEnduranceEnabled[bankNumber, infoType] = new IEnumRegisterField<MultiBitBool4>[FlashNumberOfPagesInInfo[infoType]];
                }
            }

            Registers.InterruptState.Define(this)
                .WithFlag(0, out interruptStatusProgramEmpty, FieldMode.Read | FieldMode.WriteOneToClear, name: "prog_empty")
                .WithFlag(1, out interruptStatusProgramLevel, FieldMode.Read | FieldMode.WriteOneToClear, name: "prog_lvl")
                .WithFlag(2, out interruptStatusReadFull, FieldMode.Read | FieldMode.WriteOneToClear, name: "rd_full")
                .WithFlag(3, out interruptStatusReadLevel, FieldMode.Read | FieldMode.WriteOneToClear, name: "rd_lvl")
                .WithFlag(4, out interruptStatusOperationDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "op_done")
                .WithFlag(5, out interruptStatusCorrectableError, FieldMode.Read | FieldMode.WriteOneToClear, name: "corr_err")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out interruptEnableProgramEmpty, name: "prog_empty")
                .WithFlag(1, out interruptEnableProgramLevel, name: "prog_lvl")
                .WithFlag(2, out interruptEnableReadFull, name: "rd_full")
                .WithFlag(3, out interruptEnableReadLevel, name: "rd_lvl")
                .WithFlag(4, out interruptEnableOperationDone, name: "op_done")
                .WithFlag(5, out interruptEnableCorrectableError, name: "corr_err")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { interruptStatusProgramEmpty.Value |= val; }, name: "prog_empty")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { interruptStatusProgramLevel.Value |= val; }, name: "prog_lvl")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { interruptStatusReadFull.Value |= val; }, name: "rd_full")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { interruptStatusReadLevel.Value |= val; }, name: "rd_lvl")
                .WithFlag(4, FieldMode.Write, writeCallback: (_, val) => { interruptStatusOperationDone.Value |= val; }, name: "op_done")
                .WithFlag(5, FieldMode.Write, writeCallback: (_, val) => { interruptStatusCorrectableError.Value |= val; }, name: "corr_err")
                .WithReservedBits(6, 26)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverableAlert.Blink(); }, name:"recov_err")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalStandardAlert.Blink(); }, name:"fatal_std_err")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name:"fatal_err")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalPrimitiveFlashAlert.Blink(); }, name:"fatal_prim_flash_alert")
                .WithFlag(4, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverablePrimitiveFlashAlert.Blink(); }, name:"recov_prim_flash_alert")
                .WithReservedBits(5, 27);

            Registers.DisableFlashFunctionality.Define(this)
                .WithTag("VAL", 0, 4)
                .WithReservedBits(4, 28);

            Registers.ExecutionFetchesEnabled.Define(this)
                // this is a field, not a tag to hush warnings
                // TODO: this should control if execution from flash is possible
                .WithValueField(0, 32, name: "EN");

            Registers.ControllerInit.Define(this)
                .WithTaggedFlag("VAL", 0)
                .WithReservedBits(1, 31);

            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            Registers.ControlEnable.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "EN")
                .WithReservedBits(1, 31);

            Registers.Control.Define(this)
                .WithFlag(0, name: "START", writeCallback: (o, n) => {
                    if(n)
                    {
                        StartOperation();
                    }
                })
                .WithReservedBits(1, 3)
                .WithEnumField<DoubleWordRegister, ControlOp>(4, 2, out operation, name: "OP")
                .WithTaggedFlag("PROG_SEL", 6)
                .WithFlag(7, out flashSelectBankEraseMode, name: "ERASE_SEL")
                .WithFlag(8, out flashSelectPartition, name: "PARTITION_SEL")
                .WithValueField(9, 2, out flashSelectInfo, name: "INFO_SEL")
                .WithReservedBits(11, 5)
                .WithValueField(16, 12, out controlNum, name: "NUM")
                .WithReservedBits(28, 4);

            Registers.AddressForFlashOperation.Define(this)
                .WithValueField(0, 32, out address, changeCallback: (_, address) => {
                    // This is required as the tests use full address or just flash offset
                    this.address.Value = address & flashAddressMask;
                    flashAddress = null;
                    var addresses = machine.SystemBus.GetRegistrationPoints(dataFlash)
                        .Select(pint => pint.Range.StartAddress);

                    if(!addresses.Any())
                    {
                        this.Log(LogLevel.Warning, "Underlying data flash is not registered on the system bus, so it cannot be accessed");
                        return;
                    }

                    flashAddress = (long)addresses.First();
                }, name: "START");

            Registers.EnableDifferentProgramTypes.Define(this)
                .WithTaggedFlag("NORMAL", 0)
                .WithTaggedFlag("REPAIR", 1)
                .WithReservedBits(2, 30);

            // Erase is performed immediately so write to SuspendErase will never happen during erasing process.
            // Cleared immediately.
            Registers.SuspendErase.Define(this)
                .WithFlag(0, valueProviderCallback: _ => false, name: "REQ")
                .WithReservedBits(1, 31);

            for(var i = 0; i < NumberOfMpRegions; i++)
            {
                // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
                RegistersCollection.AddRegister((long)Registers.RegionConfigurationEnable0 + 0x4 * i, new DoubleWordRegister(this, 0x1)
                    .WithTaggedFlag($"REGION_{i}", 0)
                    .WithReservedBits(1, 31));

                RegistersCollection.AddRegister((long)(Registers.RegionConfiguration0 + 0x4 * i), new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, MultiBitBool4>(0, 4, out mpRegionEnabled[i], name: $"EN_{i}")
                    .WithEnumField<DoubleWordRegister, MultiBitBool4>(4, 4, out mpRegionReadEnabled[i], name: $"RD_EN_{i}")
                    .WithEnumField<DoubleWordRegister, MultiBitBool4>(8, 4, out mpRegionProgEnabled[i], name: $"PROG_EN_{i}")
                    .WithEnumField<DoubleWordRegister, MultiBitBool4>(12, 4, out mpRegionEraseEnabled[i], name: $"ERASE_EN_{i}")
                    .WithEnumField<DoubleWordRegister, MultiBitBool4>(16, 4, out mpRegionScrambleEnabled[i], name: $"SCRAMBLE_EN_{i}")
                    .WithEnumField<DoubleWordRegister, MultiBitBool4>(20, 4, out mpRegionEccEnabled[i], name: $"ECC_EN_{i}")
                    .WithEnumField<DoubleWordRegister, MultiBitBool4>(24, 4, out mpRegionHighEnduranceEnabled[i], name: $"HE_EN_{i}")
                    .WithReservedBits(28, 4));

                RegistersCollection.AddRegister((long)(Registers.RegionBaseAndSizeConfiguration0 + 0x4 * i), new DoubleWordRegister(this)
                    .WithValueField(0, 9, out mpRegionBase[i], name: $"BASE_{i}")
                    .WithValueField(9, 10, out mpRegionSize[i], name: $"SIZE_{i}")
                    .WithReservedBits(19, 13));
            }

            Registers.DefaultRegionConfiguration.Define(this)
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(0, 4, out defaultMpRegionReadEnabled, name: "RD_EN")
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(4, 4, out defaultMpRegionProgEnabled, name: "PROG_EN")
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(8, 4, out defaultMpRegionEraseEnabled, name: "ERASE_EN")
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(12, 4, out defaultMpRegionScrambleEnabled, name: "SCRAMBLE_EN")
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(16, 4, out defaultMpRegionEccEnabled, name: "ECC_EN")
                .WithEnumField<DoubleWordRegister, MultiBitBool4>(20, 4, out defaultMpRegionHighEnduranceEnabled, name: "HE_EN")
                .WithReservedBits(24, 8);

            var registerOffset = Registers.Bank0Info0Enable0;
            for(var bankNumber = 0; bankNumber < FlashNumberOfBanks; ++bankNumber)
            {
                for(var infoType = 0; infoType < FlashNumberOfInfoTypes; ++infoType)
                {
                    // For each info type, first are defined configuration enabling registers and then
                    // configuration registers.
                    for(var pageNumber = 0; pageNumber < FlashNumberOfPagesInInfo[infoType]; ++pageNumber)
                    {
                        // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
                        registerOffset.Define(this, 0x1)
                            .WithTaggedFlag($"REGION_{pageNumber}", 0)
                            .WithReservedBits(1, 31);
                        registerOffset += 0x4;
                    }

                    for(var pageNumber = 0; pageNumber < FlashNumberOfPagesInInfo[infoType]; ++pageNumber)
                    {
                        registerOffset.Define(this)
                            .WithEnumField<DoubleWordRegister, MultiBitBool4>(0, 4, out bankInfoPageEnabled[bankNumber, infoType][pageNumber], name: $"EN_{pageNumber}")
                            .WithEnumField<DoubleWordRegister, MultiBitBool4>(4, 4, out bankInfoPageReadEnabled[bankNumber, infoType][pageNumber], name: $"RD_EN_{pageNumber}")
                            .WithEnumField<DoubleWordRegister, MultiBitBool4>(8, 4, out bankInfoPageProgramEnabled[bankNumber, infoType][pageNumber], name: $"PROG_EN_{pageNumber}")
                            .WithEnumField<DoubleWordRegister, MultiBitBool4>(12, 4, out bankInfoPageEraseEnabled[bankNumber, infoType][pageNumber], name: $"ERASE_EN_{pageNumber}")
                            .WithEnumField<DoubleWordRegister, MultiBitBool4>(16, 4, out bankInfoPageScrambleEnabled[bankNumber, infoType][pageNumber], name: $"SCRAMBLE_EN_{pageNumber}")
                            .WithEnumField<DoubleWordRegister, MultiBitBool4>(20, 4, out bankInfoPageEccEnabled[bankNumber, infoType][pageNumber], name: $"ECC_EN_{pageNumber}")
                            .WithEnumField<DoubleWordRegister, MultiBitBool4>(24, 4, out bankInfoPageHighEnduranceEnabled[bankNumber, infoType][pageNumber], name: $"HE_EN_{pageNumber}")
                            .WithReservedBits(28, 4);
                        registerOffset += 0x4;
                    }
                }
            }

            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            Registers.BankConfigurationEnable.Define(this, 0x1)
                .WithTaggedFlag("BANK", 0)
                .WithReservedBits(1, 31);
            Registers.BankConfiguration.Define(this)
                .WithFlag(0, out eraseBank0, name: "ERASE_EN_0")
                .WithFlag(1, out eraseBank1, name: "ERASE_EN_1")
                .WithReservedBits(2, 30);

            Registers.FlashOperationStatus.Define(this)
                .WithFlag(0, out opStatusRegisterDoneFlag, name: "done")
                .WithFlag(1, out opStatusRegisterErrorFlag, name: "err")
                .WithReservedBits(2, 30);

            Registers.Status.Define(this, 0xa)
                .WithFlag(0, out statusReadFullFlag, FieldMode.Read, name: "rd_full")
                .WithFlag(1, out statusReadEmptyFlag, FieldMode.Read, name: "rd_empty")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "prog_full")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "prog_empty")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "init_wip")
                .WithReservedBits(5, 27);

            Registers.ErrorCode.Define(this)
                .WithFlag(0, out errorCodeOutOfBoundsError, FieldMode.Read | FieldMode.WriteOneToClear, name: "op_err")
                .WithFlag(1, out errorCodeMemoryProtectionError, FieldMode.Read | FieldMode.WriteOneToClear, name: "mp_err")
                .WithTaggedFlag("rd_err", 2)
                .WithTaggedFlag("prog_err", 3)
                .WithTaggedFlag("prog_win_err", 4)
                .WithTaggedFlag("prog_type_err", 5)
                .WithTaggedFlag("flash_macro", 6)
                .WithTaggedFlag("update_err", 7)
                .WithReservedBits(8, 24);

            Registers.FaultStatus.Define(this)
                .WithTaggedFlag("op_err", 0)
                .WithTaggedFlag("mp_err", 1)
                .WithTaggedFlag("rd_err", 2)
                .WithTaggedFlag("prog_err", 3)
                .WithTaggedFlag("prog_win_err", 4)
                .WithTaggedFlag("prog_type_err", 5)
                .WithTaggedFlag("flash_macro_err", 6)
                .WithTaggedFlag("seed_err", 7)
                .WithTaggedFlag("phy_relbl_err", 8)
                .WithTaggedFlag("phy_storage_err", 9)
                .WithTaggedFlag("spurious_ack", 10)
                .WithTaggedFlag("arb_err", 11)
                .WithTaggedFlag("host_gnt_err", 12)
                .WithReservedBits(13, 19);

            Registers.ErrorAddress.Define(this)
                .WithValueField(0, 32, out errorAddress, FieldMode.Read, name: "ERR_ADDR");

            Registers.ECCSingleErrorCount.Define(this)
                .WithTag("ECC_SINGLE_ERR_CNT_0", 0, 8)
                .WithTag("ECC_SINGLE_ERR_CNT_1", 8, 8)
                .WithReservedBits(16, 16);

            Registers.ECCSingleErrorAddress0.Define(this)
                .WithTag("ECC_SINGLE_ERR_ADDR_0", 0, 20)
                .WithReservedBits(20, 12);

            Registers.ECCSingleErrorAddress1.Define(this)
                .WithTag("ECC_SINGLE_ERR_ADDR_1", 0, 20)
                .WithReservedBits(20, 12);

            Registers.PhyAlertConfiguration.Define(this)
                .WithTaggedFlag("alert_ack", 0)
                .WithTaggedFlag("alert_trig", 1)
                .WithReservedBits(2, 30);

            Registers.PhyStatus.Define(this, 0x6)
                .WithTaggedFlag("init_wip", 0)
                .WithTaggedFlag("prog_normal_avail", 1)
                .WithTaggedFlag("prog_repair_avail", 2)
                .WithReservedBits(3, 29);

            Registers.Scratch.Define(this)
                .WithTag("data", 0, 32);

            Registers.FifoLevel.Define(this, 0xf0f)
                .WithValueField(0, 5, out programFifoLevel, name: "PROG")
                .WithReservedBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 12 - 8, out readFifoLevel, name: "RD")
                .WithReservedBits(13, 19);

            // TODO(julianmb): implement fifo reset. There isnt any unittest for this currently.
            Registers.FifoReset.Define(this)
                .WithTaggedFlag("EN", 0)
                .WithReservedBits(1, 31);

            Registers.ProgramFifo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, writeCallback: (_, data) =>
                {
                    var isInBounds = flashAddress.HasValue && IsOffsetInBounds(programOffset);
                    var isAllowed = isInBounds && IsOperationAllowed(OperationType.ProgramData, programOffset);

                    if(isInBounds && isAllowed)
                    {
                        var oldData = ReadFlashDoubleWord(programOffset);
                        WriteFlashDoubleWord(programOffset, oldData & (uint)data);
                        programOffset += 4;
                    }
                    else
                    {
                        opStatusRegisterErrorFlag.Value = true;
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusOperationDone.Value = true;

                        if(isInBounds)
                        {
                            errorCodeMemoryProtectionError.Value = true;
                            errorAddress.Value = (uint)(programOffset + flashAddress.Value);
                        }
                        else
                        {
                            errorCodeOutOfBoundsError.Value = true;
                        }
                        return;
                    }

                    var offset = address.Value;
                    if(programOffset > (long)(offset + 4 * controlNum.Value))
                    {
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusOperationDone.Value = true;
                    }
                    else
                    {
                        interruptStatusProgramLevel.Value = true;
                        interruptStatusProgramEmpty.Value = true;
                    }
                })
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.ReadFifo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, valueProviderCallback: _ =>
                {
                    uint value = 0;
                    var isInBounds = flashAddress.HasValue && IsOffsetInBounds(readOffset);
                    var isAllowed = isInBounds && IsOperationAllowed(OperationType.ReadData, readOffset);

                    if(isInBounds && isAllowed)
                    {
                        value = ReadFlashDoubleWord(readOffset);
                        readOffset += 4;
                    }
                    else
                    {
                        opStatusRegisterErrorFlag.Value = true;
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusOperationDone.Value = true;

                        if(isInBounds)
                        {
                            errorCodeMemoryProtectionError.Value = true;
                            errorAddress.Value = (uint)(readOffset + flashAddress.Value);
                        }
                        else
                        {
                            errorCodeOutOfBoundsError.Value = true;
                        }
                        return value;
                    }

                    var offset = address.Value;
                    if(readOffset > (long)(offset + 4 * controlNum.Value))
                    {
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusOperationDone.Value = true;
                        UpdateReadFifoSignals(false);
                    }
                    else
                    {
                        UpdateReadFifoSignals(true);
                    }

                    return value;
                })
                .WithWriteCallback((_, __) => UpdateInterrupts());

            infoFlash = new ArrayMemory[FlashNumberOfBanks, FlashNumberOfInfoTypes];
            for(var bankNumber = 0; bankNumber < FlashNumberOfBanks; ++bankNumber)
            {
                for(var infoType = 0; infoType < FlashNumberOfInfoTypes; ++infoType)
                {
                    infoFlash[bankNumber, infoType] = new ArrayMemory(FlashNumberOfPagesInInfo[infoType] * BytesPerPage);
                }
            }
            this.Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            statusReadFullFlag.Value = false;
            statusReadEmptyFlag.Value = true;

            readOffset = 0;
            programOffset = 0;
            RecoverableAlert.Unset();
            FatalStandardAlert.Unset();
            FatalAlert.Unset();
            FatalPrimitiveFlashAlert.Unset();
            RecoverablePrimitiveFlashAlert.Unset();
            UpdateInterrupts();
        }

        public void LoadFlashInfoPartitionFromBinary(uint bankNumber, uint infoType, long offset, ReadFilePath fileName)
        {
            if (bankNumber >= FlashNumberOfBanks || infoType >= FlashNumberOfInfoTypes)
            {
                throw new RecoverableException("Invalid bank number or info type.");
            }

            var partitionSize = infoFlash[bankNumber, infoType].Size;
            if(partitionSize < offset)
            {
                throw new RecoverableException($"Specified offset {offset} is bigger than partition size {partitionSize}.");
            }
            var bufferSize = partitionSize - offset;

            try
            {
                using(var reader = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[bufferSize];
                    var written = 0;
                    var read = 0;
                    while((read = reader.Read(buffer, 0, buffer.Length)) > 0 && written < bufferSize)
                    {
                        infoFlash[bankNumber, infoType].WriteBytes(offset + written, buffer, 0, read);
                        written += read;
                    }
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException($"Exception while loading file {fileName}: {e.Message}");
            }
        }

        public long Size => 0x1000;

        public GPIO ProgramEmptyIRQ { get; }
        public GPIO ProgramLevelIRQ { get; }
        public GPIO ReadFullIRQ { get; }
        public GPIO ReadLevelIRQ { get; }
        public GPIO OperationDoneIRQ { get; }
        public GPIO CorrectableErrorIRQ { get; }

        public GPIO RecoverableAlert { get; }
        public GPIO FatalStandardAlert { get; }
        public GPIO FatalAlert { get; }
        public GPIO FatalPrimitiveFlashAlert { get; }
        public GPIO RecoverablePrimitiveFlashAlert { get; }

        private void StartOperation()
        {
            switch(operation.Value)
            {
                case ControlOp.FlashRead:
                {
                    this.Log(
                        LogLevel.Noisy,
                        "OpenTitan_FlashController/StartOperation: Read");
                    StartReadOperation();
                    break;
                }

                case ControlOp.FlashProgram:
                {
                    this.Log(
                        LogLevel.Noisy,
                        "OpenTitan_FlashController/StartOperation: Program");
                    StartProgramOperation();
                    break;
                }

                case ControlOp.FlashErase:
                {
                    this.Log(
                        LogLevel.Noisy,
                        "OpenTitan_FlashController/StartOperation: Erase");
                    StartEraseOperation();
                    break;
                }

                default:
                {
                    this.Log(
                        LogLevel.Warning,
                        "OpenTitan_FlashController/StartOperation: invalid controlOpValue: 0x{0:X}", operation.Value);
                    break;
                }
            }
        }

        private void StartReadOperation()
        {
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartReadOperation: address = 0x{0:X}",
                address.Value);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartReadOperation: reading {0}",
                flashSelectPartition.Value ? "InfoPartition" : "DataPartition");

            readOffset = (long)address.Value;
            UpdateReadFifoSignals(true);
        }

        private void StartProgramOperation()
        {
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartProgramOperation: address = 0x{0:X}",
                address.Value);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartProgramOperation: programming {0}",
                flashSelectPartition.Value ? "InfoPartition" : "DataPartition");

            programOffset = (long)address.Value;
        }

        private void StartEraseOperation()
        {
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartEraseOperation: address = 0x{0:X}",
                address.Value);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartEraseOperation: erasing {0}",
                flashSelectPartition.Value ? "InfoPartition" : "DataPartition");

            var offset = (uint)address.Value;
            var size = flashSelectBankEraseMode.Value ? BytesPerBank : BytesPerPage;
            var truncatedOffset = offset & ~(size - 1);
            var bankNumber = truncatedOffset / BytesPerBank;

            if(!IsOffsetInBounds(truncatedOffset))
            {
                errorCodeOutOfBoundsError.Value = true;
                opStatusRegisterErrorFlag.Value = true;
                opStatusRegisterDoneFlag.Value = true;
                interruptStatusOperationDone.Value = true;
                UpdateInterrupts();
                return;
            }

            var notAllowed = false;
            if(flashSelectBankEraseMode.Value)
            {
                switch(bankNumber)
                {
                    case 0:
                        notAllowed = !eraseBank0.Value;
                        break;
                    case 1:
                        notAllowed = !eraseBank1.Value;
                        break;
                    default:
                        notAllowed = true;
                        break;
                }
            }
            else
            {
                notAllowed = !IsOperationAllowed(OperationType.EraseDataPage, truncatedOffset);
            }

            if(notAllowed)
            {
                opStatusRegisterErrorFlag.Value = true;
                opStatusRegisterDoneFlag.Value = true;
                interruptStatusOperationDone.Value = true;
                errorCodeMemoryProtectionError.Value = true;
                UpdateInterrupts();
                return;
            }

            for(var i = 0 ; i < size ; i += 4)
            {
                WriteFlashDoubleWord(truncatedOffset + i, 0xffffffff);
            }
            opStatusRegisterDoneFlag.Value = true;
            interruptStatusOperationDone.Value = true;
            UpdateInterrupts();
        }

        private void UpdateReadFifoSignals(bool readInProgress)
        {
            var flashOffset = (uint)address.Value;
            if(readInProgress)
            {
                var wordsLeft = (readOffset - flashOffset) / 4 + 1;
                statusReadFullFlag.Value = wordsLeft >= 16;
                interruptStatusReadFull.Value |= statusReadFullFlag.Value;
                interruptStatusReadLevel.Value |= wordsLeft >= (long)readFifoLevel.Value;
                statusReadEmptyFlag.Value = false;
            }
            else
            {
                statusReadFullFlag.Value = false;
                statusReadEmptyFlag.Value = true;
            }
            UpdateInterrupts();
        }

        private bool IsOffsetInBounds(long offset)
        {
            if(offset < 0)
            {
                return false;
            }

            if(flashSelectPartition.Value)
            {
                var infoType = flashSelectInfo.Value;
                return (offset % BytesPerBank) < BytesPerPage * FlashNumberOfPagesInInfo[infoType];
            }
            else
            {
                return offset < BytesPerBank * FlashNumberOfBanks;
            }
        }

        private bool IsOperationAllowed(OperationType opType, long operationOffset)
        {
            return flashSelectPartition.Value
                ? IsOperationAllowedInfo(opType, operationOffset)
                : IsOperationAllowedData(opType, operationOffset);
        }

        private bool IsOperationAllowedInfo(OperationType opType, long operationOffset)
        {
            var bankNumber = operationOffset / BytesPerBank;
            var infoType = flashSelectInfo.Value;
            var pageNumber = (operationOffset % BytesPerBank) / BytesPerPage;

            if(bankInfoPageEnabled[bankNumber, infoType][pageNumber].Value == MultiBitBool4.False)
            {
                return false;
            }

            var ret = false;
            switch(opType)
            {
                case OperationType.ReadData:
                    ret = (bankInfoPageReadEnabled[bankNumber, infoType][pageNumber].Value == MultiBitBool4.True);
                    break;
                case OperationType.ProgramData:
                    ret = (bankInfoPageProgramEnabled[bankNumber, infoType][pageNumber].Value == MultiBitBool4.True);
                    break;
                case OperationType.EraseDataPage:
                    ret = (bankInfoPageEraseEnabled[bankNumber, infoType][pageNumber].Value == MultiBitBool4.True);
                    break;
                case OperationType.ScrambleData:
                    ret = (bankInfoPageScrambleEnabled[bankNumber, infoType][pageNumber].Value == MultiBitBool4.True);
                    break;
                case OperationType.Ecc:
                    ret = (bankInfoPageEccEnabled[bankNumber, infoType][pageNumber].Value == MultiBitBool4.True);
                    break;
                case OperationType.HighEndurance:
                    ret = (bankInfoPageHighEnduranceEnabled[bankNumber, infoType][pageNumber].Value == MultiBitBool4.True);
                    break;
                default:
                    break;
            }

            if(!ret)
            {
                this.Log(
                    LogLevel.Debug, "OpenTitan_FlashController/IsOperationAllowedInfo: Operation not allowed!");
            }

            return ret;
        }

        private bool IsOperationAllowedInDefaultRegion(OperationType opType)
        {
            var ret = false;
            switch(opType)
            {
                case OperationType.ReadData:
                    ret = (defaultMpRegionReadEnabled.Value == MultiBitBool4.True);
                    break;
                case OperationType.ProgramData:
                    ret = (defaultMpRegionProgEnabled.Value == MultiBitBool4.True);
                    break;
                case OperationType.EraseDataPage:
                    ret = (defaultMpRegionEraseEnabled.Value == MultiBitBool4.True);
                    break;
                case OperationType.ScrambleData:
                    ret = (defaultMpRegionScrambleEnabled.Value == MultiBitBool4.True);
                    break;
                case OperationType.Ecc:
                    ret = (defaultMpRegionEccEnabled.Value == MultiBitBool4.True);
                    break;
                case OperationType.HighEndurance:
                    ret = (defaultMpRegionHighEnduranceEnabled.Value == MultiBitBool4.True);
                    break;
                default:
                    break;
            }

            if(!ret)
            {
                this.Log(
                LogLevel.Debug,
                    "OpenTitan_FlashController/IsOperationAllowedInDefaultRegion: Operation not allowed in the default region");
            }

            return ret;
        }

        private bool IsOperationAllowedData(OperationType opType, long operationOffset)
        {
            var ret = false;
            var matched = false;

            for(var i = 0; i < NumberOfMpRegions; i++)
            {
                if(MpRegionRegisterAppliesToOperation(i, operationOffset))
                {
                    matched = true;
                    ret = IsOperationAllowedInMpRegion(opType, i);
                    break;
                }
            }
            if(!matched)
            {
                ret = IsOperationAllowedInDefaultRegion(opType);
            }

            if(!ret)
            {
                this.Log(
                    LogLevel.Debug, "OpenTitan_FlashController/IsOperationAllowedData: Operation not allowed!");
            }

            return ret;
        }

        private bool IsOperationAllowedInMpRegion(OperationType opType, int regionId)
        {
            if(mpRegionEnabled[regionId].Value == MultiBitBool4.False)
            {
                return false;
            }

            var ret = false;
            switch(opType)
            {
                case OperationType.ReadData:
                    ret = (mpRegionReadEnabled[regionId].Value == MultiBitBool4.True);
                    break;
                case OperationType.ProgramData:
                    ret = (mpRegionProgEnabled[regionId].Value == MultiBitBool4.True);
                    break;
                case OperationType.EraseDataPage:
                    ret = (mpRegionEraseEnabled[regionId].Value == MultiBitBool4.True);
                    break;
                case OperationType.ScrambleData:
                    ret = (mpRegionScrambleEnabled[regionId].Value == MultiBitBool4.True);
                    break;
                case OperationType.Ecc:
                    ret = (mpRegionEccEnabled[regionId].Value == MultiBitBool4.True);
                    break;
                case OperationType.HighEndurance:
                    ret = (mpRegionHighEnduranceEnabled[regionId].Value == MultiBitBool4.True);
                    break;
                default:
                    break;
            }

            if(!ret)
            {
                this.Log(
                    LogLevel.Debug, "OpenTitan_FlashController/IsOperationAllowedInMpRegion: Operation not allowed!");
            }

            return ret;
        }

        private bool MpRegionRegisterAppliesToOperation(int regionId, long operationOffset)
        {
            if(mpRegionEnabled[regionId].Value == MultiBitBool4.False)
            {
                return false;
            }

            var operationPage = operationOffset / BytesPerPage;
            var regionStart = (uint)mpRegionBase[regionId].Value;
            var regionEnd = regionStart + (uint)mpRegionSize[regionId].Value;

            return regionStart <= operationPage && operationPage < regionEnd;
        }

        private void WriteFlashDoubleWord(long offset, uint value)
        {
            if(flashSelectPartition.Value)
            {
                var bankNumber = offset / BytesPerBank;
                var bankOffset = offset % BytesPerBank;

                infoFlash[bankNumber, flashSelectInfo.Value].WriteDoubleWord(bankOffset, value);
            }
            else
            {
                dataFlash.WriteDoubleWord(offset, value);
            }
        }

        private uint ReadFlashDoubleWord(long offset)
        {
            if(flashSelectPartition.Value)
            {
                var bankNumber = offset / BytesPerBank;
                var bankOffset = offset % BytesPerBank;

                return infoFlash[bankNumber, flashSelectInfo.Value].ReadDoubleWord(bankOffset);
            }
            else
            {
                return dataFlash.ReadDoubleWord(offset);
            }
        }

        private void UpdateInterrupts()
        {
            ProgramEmptyIRQ.Set(interruptStatusProgramEmpty.Value && interruptEnableProgramEmpty.Value);
            ProgramLevelIRQ.Set(interruptStatusProgramLevel.Value && interruptEnableProgramLevel.Value);
            ReadFullIRQ.Set(interruptStatusReadFull.Value && interruptEnableReadFull.Value);
            ReadLevelIRQ.Set(interruptStatusReadLevel.Value && interruptEnableReadLevel.Value);
            OperationDoneIRQ.Set(interruptStatusOperationDone.Value && interruptEnableOperationDone.Value);
            CorrectableErrorIRQ.Set(interruptStatusCorrectableError.Value && interruptEnableCorrectableError.Value);
        }

        private long readOffset;
        private long programOffset;
        private long? flashAddress;

        private readonly IFlagRegisterField interruptStatusProgramEmpty;
        private readonly IFlagRegisterField interruptStatusProgramLevel;
        private readonly IFlagRegisterField interruptStatusReadFull;
        private readonly IFlagRegisterField interruptStatusReadLevel;
        private readonly IFlagRegisterField interruptStatusOperationDone;
        private readonly IFlagRegisterField interruptStatusCorrectableError;

        private readonly IFlagRegisterField interruptEnableProgramEmpty;
        private readonly IFlagRegisterField interruptEnableProgramLevel;
        private readonly IFlagRegisterField interruptEnableReadFull;
        private readonly IFlagRegisterField interruptEnableReadLevel;
        private readonly IFlagRegisterField interruptEnableOperationDone;
        private readonly IFlagRegisterField interruptEnableCorrectableError;

        private readonly MappedMemory dataFlash;
        private readonly ArrayMemory[,] infoFlash;
        // This is required as the tests use full address or just flash offset
        private readonly uint flashAddressMask;
        private readonly IFlagRegisterField opStatusRegisterDoneFlag;
        private readonly IFlagRegisterField opStatusRegisterErrorFlag;
        private readonly IFlagRegisterField statusReadFullFlag;
        private readonly IFlagRegisterField statusReadEmptyFlag;

        private readonly IFlagRegisterField errorCodeOutOfBoundsError;
        private readonly IFlagRegisterField errorCodeMemoryProtectionError;

        private readonly IEnumRegisterField<MultiBitBool4> defaultMpRegionReadEnabled;
        private readonly IEnumRegisterField<MultiBitBool4> defaultMpRegionProgEnabled;
        private readonly IEnumRegisterField<MultiBitBool4> defaultMpRegionEraseEnabled;
        private readonly IEnumRegisterField<MultiBitBool4> defaultMpRegionScrambleEnabled;
        private readonly IEnumRegisterField<MultiBitBool4> defaultMpRegionEccEnabled;
        private readonly IEnumRegisterField<MultiBitBool4> defaultMpRegionHighEnduranceEnabled;

        private readonly IEnumRegisterField<MultiBitBool4>[] mpRegionEnabled;
        private readonly IValueRegisterField[] mpRegionBase;
        private readonly IValueRegisterField[] mpRegionSize;
        private readonly IEnumRegisterField<MultiBitBool4>[] mpRegionReadEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[] mpRegionProgEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[] mpRegionEraseEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[] mpRegionScrambleEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[] mpRegionEccEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[] mpRegionHighEnduranceEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[,][] bankInfoPageEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[,][] bankInfoPageReadEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[,][] bankInfoPageProgramEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[,][] bankInfoPageEraseEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[,][] bankInfoPageScrambleEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[,][] bankInfoPageEccEnabled;
        private readonly IEnumRegisterField<MultiBitBool4>[,][] bankInfoPageHighEnduranceEnabled;

        private readonly IFlagRegisterField flashSelectBankEraseMode;
        private readonly IFlagRegisterField flashSelectPartition;
        private readonly IValueRegisterField flashSelectInfo;

        private readonly IFlagRegisterField eraseBank0;
        private readonly IFlagRegisterField eraseBank1;

        private readonly IValueRegisterField address;

        private readonly IEnumRegisterField<ControlOp> operation;

        private readonly IValueRegisterField controlNum;

        private readonly IValueRegisterField errorAddress;

        private readonly IValueRegisterField programFifoLevel;
        private readonly IValueRegisterField readFifoLevel;

        private readonly uint[] FlashNumberOfPagesInInfo = { 10, 1, 2 };

        private const uint ReadFifoDepth = 16;
        private const uint ProgramFifoDepth = 16;
        private const uint FlashWordsPerPage = 256;
        private const uint FlashWordSize = 8;
        private const uint FlashPagesPerBank = 256;
        private const uint FlashNumberOfBanks = 2;
        private const uint FlashNumberOfInfoTypes = 3;

        private const int NumberOfMpRegions = 8;
        private const uint BytesPerPage = FlashWordsPerPage * FlashWordSize;
        private const uint BytesPerBank = FlashPagesPerBank * BytesPerPage;

        #pragma warning restore format
        private enum Registers : long
        {
            InterruptState                  = 0x000,
            InterruptEnable                 = 0x004,
            InterruptTest                   = 0x008,
            AlertTest                       = 0x00C,
            DisableFlashFunctionality       = 0x010,
            ExecutionFetchesEnabled         = 0x014,
            ControllerInit                  = 0x018,
            ControlEnable                   = 0x01C,
            Control                         = 0x020,
            AddressForFlashOperation        = 0x024,
            EnableDifferentProgramTypes     = 0x028,
            SuspendErase                    = 0x02C,
            RegionConfigurationEnable0      = 0x030,
            RegionConfigurationEnable1      = 0x034,
            RegionConfigurationEnable2      = 0x038,
            RegionConfigurationEnable3      = 0x03C,
            RegionConfigurationEnable4      = 0x040,
            RegionConfigurationEnable5      = 0x044,
            RegionConfigurationEnable6      = 0x048,
            RegionConfigurationEnable7      = 0x04C,
            RegionConfiguration0            = 0x050,
            RegionConfiguration1            = 0x054,
            RegionConfiguration2            = 0x058,
            RegionConfiguration3            = 0x05C,
            RegionConfiguration4            = 0x060,
            RegionConfiguration5            = 0x064,
            RegionConfiguration6            = 0x068,
            RegionConfiguration7            = 0x06C,
            RegionBaseAndSizeConfiguration0 = 0x070,
            RegionBaseAndSizeConfiguration1 = 0x074,
            RegionBaseAndSizeConfiguration2 = 0x078,
            RegionBaseAndSizeConfiguration3 = 0x07C,
            RegionBaseAndSizeConfiguration4 = 0x080,
            RegionBaseAndSizeConfiguration5 = 0x084,
            RegionBaseAndSizeConfiguration6 = 0x088,
            RegionBaseAndSizeConfiguration7 = 0x08C,
            DefaultRegionConfiguration      = 0x090,
            Bank0Info0Enable0               = 0x094,
            Bank0Info0Enable1               = 0x098,
            Bank0Info0Enable2               = 0x09C,
            Bank0Info0Enable3               = 0x0A0,
            Bank0Info0Enable4               = 0x0A4,
            Bank0Info0Enable5               = 0x0A8,
            Bank0Info0Enable6               = 0x0AC,
            Bank0Info0Enable7               = 0x0B0,
            Bank0Info0Enable8               = 0x0B4,
            Bank0Info0Enable9               = 0x0B8,
            Bank0Info0PageConfiguration0    = 0x0BC,
            Bank0Info0PageConfiguration1    = 0x0C0,
            Bank0Info0PageConfiguration2    = 0x0C4,
            Bank0Info0PageConfiguration3    = 0x0C8,
            Bank0Info0PageConfiguration4    = 0x0CC,
            Bank0Info0PageConfiguration5    = 0x0D0,
            Bank0Info0PageConfiguration6    = 0x0D4,
            Bank0Info0PageConfiguration7    = 0x0D8,
            Bank0Info0PageConfiguration8    = 0x0DC,
            Bank0Info0PageConfiguration9    = 0x0E0,
            Bank0Info1Enable                = 0x0E4,
            Bank0Info1PageConfiguration     = 0x0E8,
            Bank0Info2Enable0               = 0x0EC,
            Bank0Info2Enable1               = 0x0F0,
            Bank0Info2PageConfiguration0    = 0x0F4,
            Bank0Info2PageConfiguration1    = 0x0F8,
            Bank1Info0Enable0               = 0x0FC,
            Bank1Info0Enable1               = 0x100,
            Bank1Info0Enable2               = 0x104,
            Bank1Info0Enable3               = 0x108,
            Bank1Info0Enable4               = 0x10C,
            Bank1Info0Enable5               = 0x110,
            Bank1Info0Enable6               = 0x114,
            Bank1Info0Enable7               = 0x118,
            Bank1Info0Enable8               = 0x11C,
            Bank1Info0Enable9               = 0x120,
            Bank1Info0PageConfiguration0    = 0x124,
            Bank1Info0PageConfiguration1    = 0x128,
            Bank1Info0PageConfiguration2    = 0x12C,
            Bank1Info0PageConfiguration3    = 0x130,
            Bank1Info0PageConfiguration4    = 0x134,
            Bank1Info0PageConfiguration5    = 0x138,
            Bank1Info0PageConfiguration6    = 0x13C,
            Bank1Info0PageConfiguration7    = 0x140,
            Bank1Info0PageConfiguration8    = 0x144,
            Bank1Info0PageConfiguration9    = 0x148,
            Bank1Info1Enable                = 0x14C,
            Bank1Info1PageConfiguration     = 0x150,
            Bank1Info2Enable0               = 0x154,
            Bank1Info2Enable1               = 0x158,
            Bank1Info2PageConfiguration0    = 0x15C,
            Bank1Info2PageConfiguration1    = 0x160,
            HardwareInfoConfigurationOverride = 0x164,
            BankConfigurationEnable         = 0x164+0x4,
            BankConfiguration               = 0x168+0x4,
            FlashOperationStatus            = 0x16C+0x4,
            Status                          = 0x170+0x4,
            ErrorCode                       = 0x174+0x4,
            FaultStatus                     = 0x178+0x4,
            ErrorAddress                    = 0x17C+0x4,
            ECCSingleErrorCount             = 0x180+0x4,
            ECCSingleErrorAddress0          = 0x184+0x4,
            ECCSingleErrorAddress1          = 0x188+0x4,
            PhyErrorConfigurationEnable     = 0x18C+0x4,
            PhyErrorConfiguration           = 0x190+0x4,
            PhyAlertConfiguration           = 0x194+0x4,
            PhyStatus                       = 0x198+0x4,
            Scratch                         = 0x19C+0x4,
            FifoLevel                       = 0x1A0+0x4,
            FifoReset                       = 0x1A4+0x4,
            CurrengFifoLevel                = 0x1A8+0x4,
            ProgramFifo                     = 0x1AC+0x4, // 1 item wo window. Byte writes are not supported
            ReadFifo                        = 0x1B0+0x4, // 1 item ro window. Byte writes are not supported
        }

        private enum ControlOp : uint
        {
            FlashRead = 0x0,
            FlashProgram = 0x1,
            FlashErase = 0x2
        }

        private enum OperationType
        {
            ReadData,
            ProgramData,
            EraseDataPage,
            ScrambleData,
            Ecc,
            HighEndurance
        }
        #pragma warning restore format
    } // class
} // namespace
