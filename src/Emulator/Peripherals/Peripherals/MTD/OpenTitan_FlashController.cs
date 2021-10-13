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
            mpRegionEnabled = new IFlagRegisterField[NumberOfMpRegions];
            mpRegionBase = new IValueRegisterField[NumberOfMpRegions];
            mpRegionSize = new IValueRegisterField[NumberOfMpRegions];
            mpRegionReadEnabled = new IFlagRegisterField[NumberOfMpRegions];
            mpRegionProgEnabled = new IFlagRegisterField[NumberOfMpRegions];
            mpRegionEraseEnabled = new IFlagRegisterField[NumberOfMpRegions];

            // TODO(julianmb): support interrupts. no unittests exist though.
            Registers.InterruptState.Define(this)
                .WithFlag(0, name: "prog_empty", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(1, name: "prog_lvl", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(2, name: "rd_full", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(3, name: "rd_lvl", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(4, out interruptStatusRegisterOpDoneFlag, name: "op_done",
                    mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithTaggedFlag("corr_err", 5)
                .WithReservedBits(6, 1 + 31 - 6);

            // TODO(julianmb): support interrupts. no unittests exist though.
            Registers.InterruptEnable.Define(this)
                .WithFlag(0, name: "prog_empty")
                .WithFlag(1, name: "prog_lvl")
                .WithFlag(2, name: "rd_full")
                .WithFlag(3, name: "rd_lvl")
                .WithFlag(4, name: "op_done")
                .WithTaggedFlag("corr_err", 5)
                .WithReservedBits(6, 1 + 31 - 6);

            // TODO(julianmb): support interrupts. no unittests exist though.
            Registers.InterruptTest.Define(this)
                .WithFlag(0, name: "prog_empty", mode: FieldMode.Write)
                .WithFlag(1, name: "prog_lvl", mode: FieldMode.Write)
                .WithFlag(2, name: "rd_full", mode: FieldMode.Write)
                .WithFlag(3, name: "rd_lvl", mode: FieldMode.Write)
                .WithFlag(4, name: "op_done", mode: FieldMode.Write)
                .WithTaggedFlag("corr_err", 5)
                .WithReservedBits(6, 1 + 31 - 6);

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
                .WithFlag(6, name: "PROG_SEL")
                .WithFlag(7, out flashSelectEraseMode, name: "ERASE_SEL")
                .WithFlag(8, out flashSelectPartition, name: "PARTITION_SEL")
                .WithValueField(9, 2, name: "INFO_SEL")
                .WithReservedBits(11, 1 + 15 - 11)
                .WithValueField(16, 1 + 27 - 16, out controlNum, name: "NUM")
                .WithReservedBits(28, 1 + 31 - 28);

            Registers.AddressForFlashOperation.Define(this)
                .WithValueField(0, 32, out address, name: "START");

            Registers.EnableDifferentProgramTypes.Define(this)
                .WithTaggedFlag("NORMAL", 0)
                .WithTaggedFlag("REPAIR", 1)
                .WithReservedBits(2, 30);

            // Erase is performed immediately so write to SuspendErase will never happen during erasing process.
            // Cleared immediately.
            Registers.SuspendErase.Define(this)
                .WithTaggedFlag("REQ", 0)
                .WithReservedBits(1, 31);

            for(var i = 0; i < NumberOfMpRegions; i++)
            {
                // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
                RegistersCollection.AddRegister((long)Registers.RegionConfigurationEnable0 + 0x4 * i, new DoubleWordRegister(this, 0x1)
                    .WithFlag(0, name: $"REGION_{i}", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
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
            
            // TODO: move magic numbers to constants
            for(var bankNumber = 0; bankNumber < 10; bankNumber++)
            {
                var bankOffset = (Registers.Bank1Info0Enable0 - Registers.Bank0Info0Enable0) * bankNumber;

                for(var i = 0; i < 10; i++)
                {
                    // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
                    (Registers.Bank0Info0Enable0 + bankOffset + 0x4 * i).Define(this, 0x1)
                        .WithFlag(0, name: $"REGION_{i}", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                        .WithReservedBits(1, 31);

                    (Registers.Bank0Info0PageConfiguration0 + bankOffset + 0x4 * i).Define(this)
                        .WithFlag(0, name: $"EN_{i}")
                        .WithFlag(1, name: $"RD_EN_{i}")
                        .WithFlag(2, name: $"PROG_EN_{i}")
                        .WithFlag(3, name: $"ERASE_EN_{i}")
                        .WithFlag(4, name: $"SCRAMBLE_EN_{i}")
                        .WithTaggedFlag($"ECC_EN_{i}", 5)
                        .WithTaggedFlag($"HE_EN_{i}", 6)
                        .WithReservedBits(7, 1 + 31 - 7);
                }

                (Registers.Bank0Info1Enable + bankOffset).Define(this, 0x1)
                    .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                    .WithReservedBits(1, 31);

                (Registers.Bank0Info1PageConfiguration + bankOffset).Define(this)
                    .WithFlag(0, name: "EN_0")
                    .WithFlag(1, name: "RD_EN_0")
                    .WithFlag(2, name: "PROG_EN_0")
                    .WithFlag(3, name: "ERASE_EN_0")
                    .WithFlag(4, name: "SCRAMBLE_EN_0")
                    .WithTaggedFlag($"ECC_EN_0", 5)
                    .WithTaggedFlag($"HE_EN_0", 6)
                    .WithReservedBits(7, 1 + 31 - 7);

                for(var i = 0; i < 2; i++)
                {
                    (Registers.Bank0Info2Enable0 + bankOffset + 0x4 * i).Define(this, 0x1)
                        .WithFlag(0, name: $"REGION_{i}", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                        .WithReservedBits(1, 31);

                    (Registers.Bank0Info2PageConfiguration0 + bankOffset + 0x4 * i).Define(this)
                        .WithFlag(0, name: $"EN_{i}")
                        .WithFlag(1, name: $"RD_EN_{i}")
                        .WithFlag(2, name: $"PROG_EN_{i}")
                        .WithFlag(3, name: $"ERASE_EN_{i}")
                        .WithFlag(4, name: $"SCRAMBLE_EN_{i}")
                        .WithTaggedFlag($"ECC_EN_{i}", 5)
                        .WithTaggedFlag($"HE_EN_{i}", 6)
                        .WithReservedBits(7, 1 + 31 - 7);
                }
            }
            
            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            Registers.BankConfigurationEnable.Define(this, 0x1)
                .WithFlag(0, name: "BANK", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
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
                .WithFlag(0, name: "rd_full", mode: FieldMode.Read)
                .WithFlag(1, out statusRegisterReadEmptyFlag, name: "rd_empty", mode: FieldMode.Read)
                .WithFlag(2, name: "prog_full", mode: FieldMode.Read)
                .WithFlag(3, name: "prog_empty", mode: FieldMode.Read)
                .WithFlag(4, name: "init_wip", mode: FieldMode.Read)
                .WithReservedBits(5, 1 + 31 - 5);

            Registers.ErrorCode.Define(this)
                .WithTaggedFlag("oob_err", 0)
                .WithTaggedFlag("mp_err", 1)
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
                .WithTag("ERR_ADDR", 0, 32);

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
                .WithFlag(0, name: "init_wip", mode: FieldMode.Read)
                .WithFlag(1, name: "prog_normal_avail", mode: FieldMode.Read)
                .WithFlag(2, name: "prog_repair_avail", mode: FieldMode.Read)
                .WithReservedBits(3, 1 + 31 - 3);
            
            Registers.Scratch.Define(this)
                .WithValueField(0, 32, name: "data");

            Registers.FifoLevel.Define(this, 0xf0f)
                .WithValueField(0, 5, name: "PROG")
                .WithReservedBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 12 - 8, name: "RD")
                .WithReservedBits(13, 1 + 31 - 13);

            // TODO(julianmb): implement fifo reset. There isnt any unittest for this currently.
            Registers.FifoReset.Define(this)
                .WithFlag(0, name: "EN")
                .WithReservedBits(1, 31);
            // TODO(julianmb): handle writes while fifo is full. There isnt any unittest for this currently.
            Registers.ProgramFifo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, writeCallback: (_, n) =>
                {
                    var programFlash = flashSelectPartition.Value ? infoFlash : (IDoubleWordPeripheral)dataFlash;

                    /*
                    ** TODO(julianmb): memory protection should be implemented.
                    ** It currently doesnt have a unit test though.
                    ** BANK*_INFO*_PAGE_CFG_* registers are of interest
                    */
                    bool blockOperation = !flashSelectPartition.Value &&
                        !IsOperationAllowed(OperationType.ProgramData, programAddress);

                    if(!blockOperation)
                    {
                        programFlash.WriteDoubleWord(programAddress - FlashMemBaseAddr, n);
                        programAddress += 4;
                    }
                    else
                    {
                        opStatusRegisterErrorFlag.Value = true;
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusRegisterOpDoneFlag.Value = true;
                    }

                    if(programAddress > (address.Value + 4 * (controlNum.Value)) || opStatusRegisterErrorFlag.Value)
                    {
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusRegisterOpDoneFlag.Value = true;
                    }
                });
            Registers.ReadFifo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, valueProviderCallback: _ =>
                {
                    uint value = 0;
                    var readFlash = flashSelectPartition.Value ? infoFlash : (IDoubleWordPeripheral)dataFlash;

                    /*
                    ** TODO(julianmb): memory protection should be implemented.
                    ** It currently doesnt have a unit test though.
                    ** BANK*_INFO*_PAGE_CFG_* registers are of interest
                    */
                    bool blockOperation = !flashSelectPartition.Value &&
                        !IsOperationAllowed(OperationType.ReadData, readAddress);

                    if(!blockOperation)
                    {
                        value = readFlash.ReadDoubleWord(readAddress - FlashMemBaseAddr);
                        readAddress += 4;
                    }
                    else
                    {
                        opStatusRegisterErrorFlag.Value = true;
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusRegisterOpDoneFlag.Value = true;
                    }

                    if(readAddress > (address.Value + 4 * (controlNum.Value)) || opStatusRegisterErrorFlag.Value){
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusRegisterOpDoneFlag.Value = true;
                        statusRegisterReadEmptyFlag.Value = true;
                    }

                    return value;
                });
            
            dataFlash = flash;
            infoFlash = new ArrayMemory((int)dataFlash.Size);
            this.Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            statusRegisterReadEmptyFlag.Value = true;

            readAddress = 0;
            programAddress = 0;
        }

        public long Size => 0x1000;

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
                "OpenTitan_FlashController/StartReadOperation: addrValue = 0x{0:X}",
                address.Value);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartReadOperation: reading {0}",
                flashSelectPartition.Value ? "InfoPartition" : "DataPartition");

            statusRegisterReadEmptyFlag.Value = false;
            readAddress = address.Value;
        }

        private void StartProgramOperation()
        {
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartProgramOperation: addrValue = 0x{0:X}",
                address.Value);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartProgramOperation: programming {0}",
                flashSelectPartition.Value ? "InfoPartition" : "DataPartition");

            programAddress = address.Value;
        }

        private void StartEraseOperation()
        {
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartEraseOperation: addrValue = 0x{0:X}",
                address.Value);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartEraseOperation: eraseing {0}",
                flashSelectPartition.Value ? "InfoPartition" : "DataPartition");

            bool flashEraseBank0 = !flashSelectEraseMode.Value && eraseBank0.Value;
            bool flashEraseBank1 = !flashSelectEraseMode.Value && eraseBank1.Value;

            var eraseFlash = flashSelectPartition.Value ? infoFlash : (IBytePeripheral)dataFlash;
            /*
            ** TODO(julianmb): bank level memory protection should be implemented.
            ** It currently doesnt have a unit test though.
            ** BankConfiguration register is of interest
            */
            bool blockOperation = !flashSelectEraseMode.Value && !flashSelectPartition.Value &&
                !IsOperationAllowed(OperationType.EraseDataPage, address.Value);
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartEraseOperation: blockOperation = {0}",
                blockOperation);

            long eraseOffset = 0;
            long eraseBytes = 0;
            if(!flashSelectEraseMode.Value)
            {
                eraseOffset = address.Value - FlashMemBaseAddr;
                eraseBytes = FlashWordsPerPage * FlashWordSize;
            }
            else if(flashEraseBank0 || flashEraseBank1)
            {
                int bankNumber = flashEraseBank0 ? 0 : 1;
                long bytesPerBank = FlashPagesPerBank * FlashWordsPerPage * FlashWordSize;
                eraseOffset = bankNumber * bytesPerBank;
                eraseBytes = bytesPerBank;
            }

            if(!blockOperation)
            {
                for(long i = 0 ; i < eraseBytes ; i++)
                {
                    eraseFlash.WriteByte(eraseOffset + i, 0xff);
                }
            }
            else
            {
                opStatusRegisterErrorFlag.Value = true;
            }
            opStatusRegisterDoneFlag.Value = true;
            interruptStatusRegisterOpDoneFlag.Value = true;
        }

        private bool IsOperationAllowedInDefaultRegion(OperationType opType)
        {
            bool ret = (opType == OperationType.ReadData && defaultMpRegionReadEnabled.Value)
                || (opType == OperationType.ProgramData && defaultMpRegionProgEnabled.Value)
                || (opType == OperationType.EraseDataPage && defaultMpRegionEraseEnabled.Value);

            if(!ret)
            {
                this.Log(
                LogLevel.Debug,
                    "OpenTitan_FlashController/IsOperationAllowedInDefaultRegion: " +
                    "Operation not allowed in the default region");
            }

            return ret;
        }

        private bool IsOperationAllowed(OperationType opType, long operationAddress)
        {
            bool ret = IsOperationAllowedInDefaultRegion(opType);

            for(var i = 0; i < NumberOfMpRegions; i++)
            {
                if(MpRegionRegisterAppliesToOperation(i, operationAddress)
                    && IsOperationAllowedInMpRegion(opType, i))
                {
                    ret = true;
                    break;
                }
            }

            if(!ret)
            {
                this.Log(
                    LogLevel.Debug, "OpenTitan_FlashController/IsOperationAllowed: " +
                    "Operation not allowed!");
            }

            return ret;
        }

        private bool IsOperationAllowedInMpRegion(OperationType opType, int regionId)
        {
            bool ret = (opType == OperationType.ReadData && mpRegionReadEnabled[regionId].Value)
                || (opType == OperationType.ProgramData && mpRegionProgEnabled[regionId].Value)
                || (opType == OperationType.EraseDataPage && mpRegionEraseEnabled[regionId].Value);

            ret = ret && mpRegionEnabled[regionId].Value;

            if(mpRegionEnabled[regionId].Value && !ret)
            {
                this.Log(
                    LogLevel.Debug, "OpenTitan_FlashController/IsOperationAllowedInMpRegion: " +
                    "Operation not allowed!");
            }

            return ret;
        }

        private bool MpRegionRegisterAppliesToOperation(int regionId, long operationAddress)
        {
            long regionStart = FlashMemBaseAddr + (mpRegionBase[regionId].Value * FlashWordsPerPage * FlashWordSize);
            long regionEnd = regionStart + (mpRegionSize[regionId].Value * FlashWordsPerPage * FlashWordSize);

            // The bus is 4 bytes wide. Should that effect the border logic?
            return (mpRegionEnabled[regionId].Value
                && operationAddress >= regionStart
                && operationAddress < regionEnd);
        }

        private long readAddress;
        private long programAddress;

        private readonly MappedMemory dataFlash;
        private readonly ArrayMemory infoFlash;
        private readonly IFlagRegisterField opStatusRegisterDoneFlag;
        private readonly IFlagRegisterField opStatusRegisterErrorFlag;
        private readonly IFlagRegisterField interruptStatusRegisterOpDoneFlag;
        private readonly IFlagRegisterField statusRegisterReadEmptyFlag;

        private readonly IFlagRegisterField defaultMpRegionReadEnabled;
        private readonly IFlagRegisterField defaultMpRegionProgEnabled;
        private readonly IFlagRegisterField defaultMpRegionEraseEnabled;

        private readonly IFlagRegisterField[] mpRegionEnabled;
        private readonly IValueRegisterField[] mpRegionBase;
        private readonly IValueRegisterField[] mpRegionSize;
        private readonly IFlagRegisterField[] mpRegionReadEnabled;
        private readonly IFlagRegisterField[] mpRegionProgEnabled;
        private readonly IFlagRegisterField[] mpRegionEraseEnabled;

        private readonly IFlagRegisterField flashSelectEraseMode;
        private readonly IFlagRegisterField flashSelectPartition;
        
        private readonly IFlagRegisterField eraseBank0;
        private readonly IFlagRegisterField eraseBank1;

        private readonly IValueRegisterField address;

        private readonly IEnumRegisterField<ControlOp> operation;

        private readonly IValueRegisterField controlNum;

        private const uint ReadFifoDepth = 16;
        private const uint ProgramFifoDepth = 16;
        private const uint FlashWordsPerPage = 256;
        private const uint FlashWordSize = 8;
        private const uint FlashPagesPerBank = 256;
        private const long FlashMemBaseAddr = 0x20000000;

        private const int NumberOfMpRegions = 8;

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
