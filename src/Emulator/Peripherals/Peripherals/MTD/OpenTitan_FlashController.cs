//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2021 Google LLC
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.MTD
{
    public class OpenTitan_FlashController : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_FlashController(Machine machine, MappedMemory flash) : base(machine)
        {
            ProgramEmptyIRQ = new GPIO();
            ProgramLevelIRQ = new GPIO();
            ReadFullIRQ = new GPIO();
            ReadLevelIRQ = new GPIO();
            OperationDoneIRQ = new GPIO();
            CorrectableErrorIRQ = new GPIO();

            mpRegionEnabled = new IFlagRegisterField[NumberOfMpRegions];
            mpRegionBase = new IValueRegisterField[NumberOfMpRegions];
            mpRegionSize = new IValueRegisterField[NumberOfMpRegions];
            mpRegionReadEnabled = new IFlagRegisterField[NumberOfMpRegions];
            mpRegionProgEnabled = new IFlagRegisterField[NumberOfMpRegions];
            mpRegionEraseEnabled = new IFlagRegisterField[NumberOfMpRegions];

            bankInfoPageEnabled = new IFlagRegisterField[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageReadEnabled = new IFlagRegisterField[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageProgramEnabled = new IFlagRegisterField[FlashNumberOfBanks, FlashNumberOfInfoTypes][];
            bankInfoPageEraseEnabled = new IFlagRegisterField[FlashNumberOfBanks, FlashNumberOfInfoTypes][];

            for(var bankNumber = 0; bankNumber < FlashNumberOfBanks; ++bankNumber)
            {
                for(var infoType = 0; infoType < FlashNumberOfInfoTypes; ++infoType)
                {
                    bankInfoPageEnabled[bankNumber, infoType] = new IFlagRegisterField[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageReadEnabled[bankNumber, infoType] = new IFlagRegisterField[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageProgramEnabled[bankNumber, infoType] = new IFlagRegisterField[FlashNumberOfPagesInInfo[infoType]];
                    bankInfoPageEraseEnabled[bankNumber, infoType] = new IFlagRegisterField[FlashNumberOfPagesInInfo[infoType]];
                }
            }

            Registers.InterruptState.Define(this)
                .WithFlag(0, out interruptStatusProgramEmpty, FieldMode.Read | FieldMode.WriteOneToClear, name: "prog_empty")
                .WithFlag(1, out interruptStatusProgramLevel, FieldMode.Read | FieldMode.WriteOneToClear, name: "prog_lvl")
                .WithFlag(2, out interruptStatusReadFull, FieldMode.Read | FieldMode.WriteOneToClear, name: "rd_full")
                .WithFlag(3, out interruptStatusReadLevel, FieldMode.Read | FieldMode.WriteOneToClear, name: "rd_lvl")
                .WithFlag(4, out interruptStatusOperationDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "op_done")
                .WithFlag(5, out interruptStatusCorrectableError, FieldMode.Read | FieldMode.WriteOneToClear, name: "corr_err")
                .WithReservedBits(6, 1 + 31 - 6)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out interruptEnableProgramEmpty, name: "prog_empty")
                .WithFlag(1, out interruptEnableProgramLevel, name: "prog_lvl")
                .WithFlag(2, out interruptEnableReadFull, name: "rd_full")
                .WithFlag(3, out interruptEnableReadLevel, name: "rd_lvl")
                .WithFlag(4, out interruptEnableOperationDone, name: "op_done")
                .WithFlag(5, out interruptEnableCorrectableError, name: "corr_err")
                .WithReservedBits(6, 1 + 31 - 6)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { interruptStatusProgramEmpty.Value |= val; }, name: "prog_empty")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { interruptStatusProgramLevel.Value |= val; }, name: "prog_lvl")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { interruptStatusReadFull.Value |= val; }, name: "rd_full")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { interruptStatusReadLevel.Value |= val; }, name: "rd_lvl")
                .WithFlag(4, FieldMode.Write, writeCallback: (_, val) => { interruptStatusOperationDone.Value |= val; }, name: "op_done")
                .WithFlag(5, FieldMode.Write, writeCallback: (_, val) => { interruptStatusCorrectableError.Value |= val; }, name: "corr_err")
                .WithReservedBits(6, 1 + 31 - 6)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.AlertTest.Define(this)
                .WithTaggedFlag("recov_err", 0)
                .WithTaggedFlag("fatal_err", 1)
                .WithReservedBits(2, 30);

            Registers.DisableFlashFunctionality.Define(this)
                .WithTag("VAL", 0, 4)
                .WithReservedBits(4, 28);

            Registers.ExecutionFetchesEnabled.Define(this)
                .WithTag("EN", 0, 4)
                .WithReservedBits(4, 28);

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
                .WithFlag(7, out flashSelectEraseMode, name: "ERASE_SEL")
                .WithFlag(8, out flashSelectPartition, name: "PARTITION_SEL")
                .WithValueField(9, 2, out flashSelectInfo, name: "INFO_SEL")
                .WithReservedBits(11, 1 + 15 - 11)
                .WithValueField(16, 1 + 27 - 16, out controlNum, name: "NUM")
                .WithReservedBits(28, 1 + 31 - 28);

            Registers.AddressForFlashOperation.Define(this)
                .WithValueField(0, 32, out address, changeCallback: (_, address) => {
                    flashAddress = null;
                    var addresses = machine.SystemBus.GetRegistrationPoints(dataFlash)
                        .Select(pint => pint.Range)
                        .Where(range => range.Contains(address))
                        .Select(range => range.StartAddress);

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
                    .WithFlag(0, out mpRegionEnabled[i], name: $"EN_{i}")
                    .WithFlag(1, out mpRegionReadEnabled[i], name: $"RD_EN_{i}")
                    .WithFlag(2, out mpRegionProgEnabled[i], name: $"PROG_EN_{i}")
                    .WithFlag(3, out mpRegionEraseEnabled[i], name: $"ERASE_EN_{i}")
                    .WithTaggedFlag($"SCRAMBLE_EN_{i}", 4)
                    .WithTaggedFlag($"ECC_EN_{i}", 5)
                    .WithTaggedFlag($"HE_EN_{i}", 6)
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 1 + 16 - 8, out mpRegionBase[i], name: $"BASE_{i}")
                    .WithValueField(17, 1 + 26 - 17, out mpRegionSize[i], name: $"SIZE_{i}")
                    .WithReservedBits(27, 1 + 31 - 27));
            }
            
            Registers.DefaultRegionConfiguration.Define(this)
                .WithFlag(0, out defaultMpRegionReadEnabled, name: "RD_EN")
                .WithFlag(1, out defaultMpRegionProgEnabled, name: "PROG_EN")
                .WithFlag(2, out defaultMpRegionEraseEnabled, name: "ERASE_EN")
                .WithTaggedFlag("SCRAMBLE_EN", 3)
                .WithTaggedFlag("ECC_EN", 4)
                .WithTaggedFlag("HE_EN", 5)
                .WithReservedBits(6, 1 + 31 - 6);
            
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
                            .WithFlag(0, out bankInfoPageEnabled[bankNumber, infoType][pageNumber], name: $"EN_{pageNumber}")
                            .WithFlag(1, out bankInfoPageReadEnabled[bankNumber, infoType][pageNumber], name: $"RD_EN_{pageNumber}")
                            .WithFlag(2, out bankInfoPageProgramEnabled[bankNumber, infoType][pageNumber], name: $"PROG_EN_{pageNumber}")
                            .WithFlag(3, out bankInfoPageEraseEnabled[bankNumber, infoType][pageNumber], name: $"ERASE_EN_{pageNumber}")
                            .WithTaggedFlag($"SCRAMBLE_EN_{pageNumber}", 4)
                            .WithTaggedFlag($"ECC_EN_{pageNumber}", 5)
                            .WithTaggedFlag($"HE_EN_{pageNumber}", 6)
                            .WithReservedBits(7, 1 + 31 - 7);
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
                .WithReservedBits(2, 1 + 31 - 2);
            
            Registers.FlashOperationStatus.Define(this)
                .WithFlag(0, out opStatusRegisterDoneFlag, name: "done")
                .WithFlag(1, out opStatusRegisterErrorFlag, name: "err")
                .WithReservedBits(2, 1 + 31 - 2);

            Registers.Status.Define(this, 0xa)
                .WithFlag(0, out statusReadFullFlag, FieldMode.Read, name: "rd_full")
                .WithFlag(1, out statusReadEmptyFlag, FieldMode.Read, name: "rd_empty")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => false, name: "prog_full")
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => true, name: "prog_empty")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "init_wip")
                .WithReservedBits(5, 1 + 31 - 5);

            Registers.ErrorCode.Define(this)
                .WithFlag(0, out outOfBoundsError, FieldMode.Read | FieldMode.WriteOneToClear, name: "oob_err")
                .WithFlag(1, out memoryProtectionError, FieldMode.Read | FieldMode.WriteOneToClear, name: "mp_err")
                .WithTaggedFlag("rd_err", 2)
                .WithTaggedFlag("prog_win_err", 3)
                .WithTaggedFlag("prog_type_err", 4)
                .WithTaggedFlag("flash_phy_err", 5)
                .WithTaggedFlag("update_err", 6)
                .WithReservedBits(7, 1 + 31 - 7);

            Registers.FaultStatus.Define(this)
                .WithTaggedFlag("oob_err", 0)
                .WithTaggedFlag("mp_err", 1)
                .WithTaggedFlag("rd_err", 2)
                .WithTaggedFlag("prog_win_err", 3)
                .WithTaggedFlag("prog_type_err", 4)
                .WithTaggedFlag("flash_phy_err", 5)
                .WithTaggedFlag("reg_intg_err", 6)
                .WithTaggedFlag("phy_intg_err", 7)
                .WithTaggedFlag("lcmgr_err", 8)
                .WithTaggedFlag("storage_err", 9)
                .WithReservedBits(10, 1 + 31 - 10);

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

            Registers.PhyErrorConfigurationEnable.Define(this)
                .WithTaggedFlag("EN", 0)
                .WithReservedBits(1, 31);

            Registers.PhyErrorConfiguration.Define(this)
                .WithTaggedFlag("ECC_MULTI_ERR_DATA_EN", 0)
                .WithReservedBits(1, 31);

            Registers.PhyAlertConfiguration.Define(this)
                .WithTaggedFlag("alert_ack", 0)
                .WithTaggedFlag("alert_trig", 1)
                .WithReservedBits(2, 30);

            Registers.PhyStatus.Define(this, 0x6)
                .WithTaggedFlag("init_wip", 0)
                .WithTaggedFlag("prog_normal_avail", 1)
                .WithTaggedFlag("prog_repair_avail", 2)
                .WithReservedBits(3, 1 + 31 - 3);
            
            Registers.Scratch.Define(this)
                .WithTag("data", 0, 32);

            Registers.FifoLevel.Define(this, 0xf0f)
                .WithValueField(0, 5, out programFifoLevel, name: "PROG")
                .WithReservedBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 12 - 8, out readFifoLevel, name: "RD")
                .WithReservedBits(13, 1 + 31 - 13);

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
                        WriteFlashDoubleWord(programOffset, oldData & data);
                        programOffset += 4;
                    }
                    else
                    {
                        opStatusRegisterErrorFlag.Value = true;
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusOperationDone.Value = true;

                        if(isInBounds)
                        {
                            memoryProtectionError.Value = true;
                            errorAddress.Value = (uint)(programOffset + flashAddress.Value);
                        }
                        else
                        {
                            outOfBoundsError.Value = true;
                        }
                        return;
                    }

                    if(TryGetOffset(out var offset))
                    {
                        if(programOffset > (offset + 4 * controlNum.Value))
                        {
                            opStatusRegisterDoneFlag.Value = true;
                            interruptStatusOperationDone.Value = true;
                        }
                        else
                        {
                            interruptStatusProgramLevel.Value = true;
                            interruptStatusProgramEmpty.Value = true;
                        }
                    }
                    else
                    {
                        outOfBoundsError.Value = true;
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
                            memoryProtectionError.Value = true;
                            errorAddress.Value = (uint)(readOffset + flashAddress.Value);
                        }
                        else
                        {
                            outOfBoundsError.Value = true;
                        }
                        return value;
                    }

                    if(TryGetOffset(out var offset))
                    {
                        if(readOffset > (offset + 4 * controlNum.Value))
                        {
                            opStatusRegisterDoneFlag.Value = true;
                            interruptStatusOperationDone.Value = true;
                            UpdateReadFifoSignals(false);
                        }
                        else
                        {
                            UpdateReadFifoSignals(true);
                        }
                    }
                    else
                    {
                        outOfBoundsError.Value = true;
                    }

                    return value;
                })
                .WithWriteCallback((_, __) => UpdateInterrupts());
            
            dataFlash = flash;
            infoFlash = new ArrayMemory[FlashNumberOfBanks, FlashNumberOfInfoTypes];
            for(var bankNumber = 0; bankNumber < FlashNumberOfBanks; ++bankNumber)
            {
                for(var infoType = 0; infoType < FlashNumberOfInfoTypes; ++infoType)
                {
                    infoFlash[bankNumber, infoType] = new ArrayMemory((int)(FlashNumberOfPagesInInfo[infoType] * BytesPerPage));
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
            UpdateInterrupts();
        }

        public long Size => 0x1000;

        public GPIO ProgramEmptyIRQ { get; }
        public GPIO ProgramLevelIRQ { get; }
        public GPIO ReadFullIRQ { get; }
        public GPIO ReadLevelIRQ { get; }
        public GPIO OperationDoneIRQ { get; }
        public GPIO CorrectableErrorIRQ { get; }

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

            if(TryGetOffset(out var offset))
            {
                readOffset = offset;
                UpdateReadFifoSignals(true);
            }
            else
            {
                outOfBoundsError.Value = true;
            }
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

            if(TryGetOffset(out var offset))
            {
                programOffset = offset;
            }
            else
            {
                outOfBoundsError.Value = true;
            }
        }

        private void StartEraseOperation()
        {
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartEraseOperation: address = 0x{0:X}",
                address.Value);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartEraseOperation: eraseing {0}",
                flashSelectPartition.Value ? "InfoPartition" : "DataPartition");
            
            if(!TryGetOffset(out var offset))
            {
                outOfBoundsError.Value = true;
                return;
            }

            var size = flashSelectEraseMode.Value ? BytesPerBank : BytesPerPage;
            var truncatedOffset = offset & ~(size - 1);
            var bankNumber = truncatedOffset / BytesPerBank;

            if(!IsOffsetInBounds(truncatedOffset))
            {
                outOfBoundsError.Value = true;
                opStatusRegisterErrorFlag.Value = true;
                opStatusRegisterDoneFlag.Value = true;
                interruptStatusOperationDone.Value = true;
                UpdateInterrupts();
                return;
            }

            var notAllowed = false;
            if(flashSelectEraseMode.Value)
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
                memoryProtectionError.Value = true;
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
            if(readInProgress && TryGetOffset(out var flashOffset))
            {
                var wordsLeft = (readOffset - flashOffset) / 4 + 1;
                statusReadFullFlag.Value = wordsLeft >= 16;
                interruptStatusReadFull.Value |= statusReadFullFlag.Value;
                interruptStatusReadLevel.Value |= wordsLeft >= readFifoLevel.Value;
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

            if(!bankInfoPageEnabled[bankNumber, infoType][pageNumber].Value)
            {
                return false;
            }

            var ret = false;
            switch(opType)
            {
                case OperationType.ReadData:
                    ret = bankInfoPageReadEnabled[bankNumber, infoType][pageNumber].Value;
                    break;
                case OperationType.ProgramData:
                    ret = bankInfoPageProgramEnabled[bankNumber, infoType][pageNumber].Value;
                    break;
                case OperationType.EraseDataPage:
                    ret = bankInfoPageEraseEnabled[bankNumber, infoType][pageNumber].Value;
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
                    ret = defaultMpRegionReadEnabled.Value;
                    break;
                case OperationType.ProgramData:
                    ret = defaultMpRegionProgEnabled.Value;
                    break;
                case OperationType.EraseDataPage:
                    ret = defaultMpRegionEraseEnabled.Value;
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
            if(!mpRegionEnabled[regionId].Value)
            {
                return false;
            }

            var ret = false;
            switch(opType)
            {
                case OperationType.ReadData:
                    ret = mpRegionReadEnabled[regionId].Value;
                    break;
                case OperationType.ProgramData:
                    ret = mpRegionProgEnabled[regionId].Value;
                    break;
                case OperationType.EraseDataPage:
                    ret = mpRegionEraseEnabled[regionId].Value;
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
            if(!mpRegionEnabled[regionId].Value)
            {
                return false;
            }

            var operationPage = operationOffset / BytesPerPage;
            var regionStart = mpRegionBase[regionId].Value;
            var regionEnd = regionStart + mpRegionSize[regionId].Value;

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

        private bool TryGetOffset(out long offset)
        {
            if(flashAddress.HasValue)
            {
                offset = (address.Value & ~(FlashWordSize - 1)) - flashAddress.Value;
                return true;
            }
            else
            {
                offset = default(long);
                return false;
            }
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
        private readonly IFlagRegisterField opStatusRegisterDoneFlag;
        private readonly IFlagRegisterField opStatusRegisterErrorFlag;
        private readonly IFlagRegisterField statusReadFullFlag;
        private readonly IFlagRegisterField statusReadEmptyFlag;

        private readonly IFlagRegisterField outOfBoundsError;
        private readonly IFlagRegisterField memoryProtectionError;

        private readonly IFlagRegisterField defaultMpRegionReadEnabled;
        private readonly IFlagRegisterField defaultMpRegionProgEnabled;
        private readonly IFlagRegisterField defaultMpRegionEraseEnabled;

        private readonly IFlagRegisterField[] mpRegionEnabled;
        private readonly IValueRegisterField[] mpRegionBase;
        private readonly IValueRegisterField[] mpRegionSize;
        private readonly IFlagRegisterField[] mpRegionReadEnabled;
        private readonly IFlagRegisterField[] mpRegionProgEnabled;
        private readonly IFlagRegisterField[] mpRegionEraseEnabled;
        private readonly IFlagRegisterField[,][] bankInfoPageEnabled;
        private readonly IFlagRegisterField[,][] bankInfoPageReadEnabled;
        private readonly IFlagRegisterField[,][] bankInfoPageProgramEnabled;
        private readonly IFlagRegisterField[,][] bankInfoPageEraseEnabled;

        private readonly IFlagRegisterField flashSelectEraseMode;
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

        private enum Registers : long
        {
            InterruptState                  = 0x000, // Reset default = 0x0, mask 0x3f
            InterruptEnable                 = 0x004, // Reset default = 0x0, mask 0x3f
            InterruptTest                   = 0x008, // Reset default = 0x0, mask 0x3f
            AlertTest                       = 0x00C, // Reset default = 0x0, mask 0x3
            DisableFlashFunctionality       = 0x010, // Reset default = 0x0, mask 0x1
            ExecutionFetchesEnabled         = 0x014, // Reset default = 0x5, mask 0xf
            ControllerInit                  = 0x018, // Reset default = 0x0, mask 0x1
            ControlEnable                   = 0x01C, // Reset default = 0x1, mask 0x1
            Control                         = 0x020, // Reset default = 0x0, mask 0xfff07f1
            AddressForFlashOperation        = 0x024, // Reset default = 0x0, mask 0xffffffff
            EnableDifferentProgramTypes     = 0x028, // Reset default = 0x3, mask 0x3
            SuspendErase                    = 0x02C, // Reset default = 0x0, mask 0x1
            RegionConfigurationEnable0      = 0x030, // Reset default = 0x1, mask 0x1
            RegionConfigurationEnable1      = 0x034, // Reset default = 0x1, mask 0x1
            RegionConfigurationEnable2      = 0x038, // Reset default = 0x1, mask 0x1
            RegionConfigurationEnable3      = 0x03C, // Reset default = 0x1, mask 0x1
            RegionConfigurationEnable4      = 0x040, // Reset default = 0x1, mask 0x1
            RegionConfigurationEnable5      = 0x044, // Reset default = 0x1, mask 0x1
            RegionConfigurationEnable6      = 0x048, // Reset default = 0x1, mask 0x1
            RegionConfigurationEnable7      = 0x04C, // Reset default = 0x1, mask 0x1
            RegionConfiguration0            = 0x050, // Reset default = 0x0, mask 0x7ffff7f
            RegionConfiguration1            = 0x054, // Reset default = 0x0, mask 0x7ffff7f
            RegionConfiguration2            = 0x058, // Reset default = 0x0, mask 0x7ffff7f
            RegionConfiguration3            = 0x05C, // Reset default = 0x0, mask 0x7ffff7f
            RegionConfiguration4            = 0x060, // Reset default = 0x0, mask 0x7ffff7f
            RegionConfiguration5            = 0x064, // Reset default = 0x0, mask 0x7ffff7f
            RegionConfiguration6            = 0x068, // Reset default = 0x0, mask 0x7ffff7f
            RegionConfiguration7            = 0x06C, // Reset default = 0x0, mask 0x7ffff7f
            DefaultRegionConfiguration      = 0x070, // Reset default = 0x0, mask 0x3f
            Bank0Info0Enable0               = 0x074, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable1               = 0x078, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable2               = 0x07C, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable3               = 0x080, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable4               = 0x084, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable5               = 0x088, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable6               = 0x08C, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable7               = 0x090, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable8               = 0x094, // Reset default = 0x1, mask 0x1
            Bank0Info0Enable9               = 0x098, // Reset default = 0x1, mask 0x1
            Bank0Info0PageConfiguration0    = 0x09C, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration1    = 0x0A0, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration2    = 0x0A4, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration3    = 0x0A8, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration4    = 0x0AC, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration5    = 0x0B0, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration6    = 0x0B4, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration7    = 0x0B8, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration8    = 0x0BC, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageConfiguration9    = 0x0C0, // Reset default = 0x0, mask 0x7f
            Bank0Info1Enable                = 0x0C4, // Reset default = 0x1, mask 0x1
            Bank0Info1PageConfiguration     = 0x0C8, // Reset default = 0x0, mask 0x7f
            Bank0Info2Enable0               = 0x0CC, // Reset default = 0x1, mask 0x1
            Bank0Info2Enable1               = 0x0D0, // Reset default = 0x1, mask 0x1
            Bank0Info2PageConfiguration0    = 0x0D4, // Reset default = 0x0, mask 0x7f
            Bank0Info2PageConfiguration1    = 0x0D8, // Reset default = 0x0, mask 0x7f
            Bank1Info0Enable0               = 0x0DC, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable1               = 0x0E0, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable2               = 0x0E4, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable3               = 0x0E8, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable4               = 0x0EC, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable5               = 0x0F0, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable6               = 0x0F4, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable7               = 0x0F8, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable8               = 0x0FC, // Reset default = 0x1, mask 0x1
            Bank1Info0Enable9               = 0x100, // Reset default = 0x1, mask 0x1
            Bank1Info0PageConfiguration0    = 0x104, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration1    = 0x108, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration2    = 0x10C, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration3    = 0x110, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration4    = 0x114, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration5    = 0x118, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration6    = 0x11C, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration7    = 0x120, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration8    = 0x124, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageConfiguration9    = 0x128, // Reset default = 0x0, mask 0x7f
            Bank1Info1Enable                = 0x12C, // Reset default = 0x1, mask 0x1
            Bank1Info1PageConfiguration     = 0x130, // Reset default = 0x0, mask 0x7f
            Bank1Info2Enable0               = 0x134, // Reset default = 0x1, mask 0x1
            Bank1Info2Enable1               = 0x138, // Reset default = 0x1, mask 0x1
            Bank1Info2PageConfiguration0    = 0x13C, // Reset default = 0x0, mask 0x7f
            Bank1Info2PageConfiguration1    = 0x140, // Reset default = 0x0, mask 0x7f
            BankConfigurationEnable         = 0x144, // Reset default = 0x1, mask 0x1
            BankConfiguration               = 0x148, // Reset default = 0x1, mask 0x1
            FlashOperationStatus            = 0x14C, // Reset default = 0x0, mask 0x3
            Status                          = 0x150, // Reset default = 0xa, mask 0x1f
            ErrorCode                       = 0x154, // Reset default = 0x0, mask 0x7f
            FaultStatus                     = 0x158, // Reset default = 0x0, mask 0x3ff
            ErrorAddress                    = 0x15C, // Reset default = 0x0, mask 0xffffffff
            ECCSingleErrorCount             = 0x160, // Reset default = 0x0, mask 0xffff
            ECCSingleErrorAddress0          = 0x164, // Reset default = 0x0, mask 0xfffff
            ECCSingleErrorAddress1          = 0x168, // Reset default = 0x0, mask 0xfffff
            PhyErrorConfigurationEnable     = 0x16C, // Reset default = 0x1, mask 0x1
            PhyErrorConfiguration           = 0x170, // Reset default = 0x1, mask 0x1
            PhyAlertConfiguration           = 0x174, // Reset default = 0x0, mask 0x3
            PhyStatus                       = 0x178, // Reset default = 0x6, mask 0x7
            Scratch                         = 0x17C, // Reset default = 0x0, mask 0xffffffff
            FifoLevel                       = 0x180, // Reset default = 0xf0f, mask 0x1f1f
            FifoReset                       = 0x184, // Reset default = 0x0, mask 0x1
            ProgramFifo                     = 0x188, // 1 item wo window. Byte writes are not supported
            ReadFifo                        = 0x18C, // 1 item ro window. Byte writes are not supported
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
            EraseDataPage
        }
    } // class
} // namespace
