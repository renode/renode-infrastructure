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
            Registers.IntrState.Define(this)
                .WithFlag(0, name: "prog_empty", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(1, name: "prog_lvl", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(2, name: "rd_full", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(3, name: "rd_lvl", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(4, out interruptStatusRegisterOpDoneFlag, name: "op_done",
                    mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(5, out interruptStatusRegisterOpErrorFlag, name: "op_error",
                    mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithIgnoredBits(6, 1 + 31 - 6);

            // TODO(julianmb): support interrupts. no unittests exist though.
            Registers.IntrEnable.Define(this)
                .WithFlag(0, name: "prog_empty")
                .WithFlag(1, name: "prog_lvl")
                .WithFlag(2, name: "rd_full")
                .WithFlag(3, name: "rd_lvl")
                .WithFlag(4, name: "op_done")
                .WithFlag(5, name: "op_error")
                .WithIgnoredBits(6, 1 + 31 - 6);

            // TODO(julianmb): support interrupts. no unittests exist though.
            Registers.IntrTest.Define(this)
                .WithFlag(0, name: "prog_empty", mode: FieldMode.Write)
                .WithFlag(1, name: "prog_lvl", mode: FieldMode.Write)
                .WithFlag(2, name: "rd_full", mode: FieldMode.Write)
                .WithFlag(3, name: "rd_lvl", mode: FieldMode.Write)
                .WithFlag(4, name: "op_done", mode: FieldMode.Write)
                .WithIgnoredBits(5, 1 + 31 - 5);

            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            Registers.CtrlRegwen.Define(this, 0x1)
                .WithFlag(0, name: "EN", mode: FieldMode.Read)
                .WithIgnoredBits(1, 31);

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
                .WithFlag(7, out flashErasePage, name: "ERASE_SEL")
                .WithFlag(8, out flashErasePartition, name: "PARTITION_SEL")
                .WithValueField(9, 2, name: "INFO_SEL")
                .WithIgnoredBits(11, 1 + 15 - 11)
                .WithValueField(16, 1 + 27 - 16, out controlNum, name: "NUM")
                .WithIgnoredBits(28, 1 + 31 - 28);

            Registers.Addr.Define(this)
                .WithValueField(0, 32, out address, name: "START");

            for(var i = 0; i < NumberOfMpRegions; i++)
            {
                // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
                RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen0 + 0x4 * i, new DoubleWordRegister(this, 0x1)
                    .WithFlag(0, name: $"REGION_{i}", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                    .WithIgnoredBits(1, 31));

                RegistersCollection.AddRegister((long)(Registers.MpRegionCfg0 + 0x4 * i), new DoubleWordRegister(this)
                    .WithFlag(0, out mpRegionEnabled[i], name: $"EN_{i}")
                    .WithFlag(1, out mpRegionReadEnabled[i], name: $"RD_EN_{i}")
                    .WithFlag(2, out mpRegionProgEnabled[i], name: $"PROG_EN_{i}")
                    .WithFlag(3, out mpRegionEraseEnabled[i], name: $"ERASE_EN_{i}")
                    .WithTaggedFlag($"SCRAMBLE_EN_{i}", 4)
                    .WithIgnoredBits(5, 1 + 7 - 5)
                    .WithValueField(8, 1 + 16 - 8, out mpRegionBase[i], name: $"BASE_{i}")
                    .WithIgnoredBits(17, 1 + 19 - 17)
                    .WithValueField(20, 1 + 29 - 20, out mpRegionSize[i], name: $"SIZE_{i}")
                    .WithIgnoredBits(30, 1 + 31 - 30));
            }

            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            Registers.Bank0Info0Regwen0.Define(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank0Info0Regwen1.Define(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank0Info0Regwen2.Define(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank0Info0Regwen3.Define(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank0Info1Regwen0.Define(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank0Info1Regwen1.Define(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank0Info1Regwen2.Define(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank0Info1Regwen3.Define(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            
            Registers.Bank0Info0PageCfg0.Define(this)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank0Info0PageCfg1.Define(this)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank0Info0PageCfg2.Define(this)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank0Info0PageCfg3.Define(this)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank0Info1PageCfg0.Define(this)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank0Info1PageCfg1.Define(this)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank0Info1PageCfg2.Define(this)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank0Info1PageCfg3.Define(this)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5);
            
            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            Registers.Bank1Info0Regwen0.Define(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank1Info0Regwen1.Define(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank1Info0Regwen2.Define(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank1Info0Regwen3.Define(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank1Info1Regwen0.Define(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank1Info1Regwen1.Define(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank1Info1Regwen2.Define(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.Bank1Info1Regwen3.Define(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);

            Registers.Bank1Info0PageCfg0.Define(this)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank1Info0PageCfg1.Define(this)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank1Info0PageCfg2.Define(this)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank1Info0PageCfg3.Define(this)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank1Info1PageCfg0.Define(this)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank1Info1PageCfg1.Define(this)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank1Info1PageCfg2.Define(this)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5);
            Registers.Bank1Info1PageCfg3.Define(this)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5);
            
            Registers.DefaultRegion.Define(this)
                .WithFlag(0, out defaultMpRegionReadEnabled, name: "RD_EN")
                .WithFlag(1, out defaultMpRegionProgEnabled, name: "PROG_EN")
                .WithFlag(2, out defaultMpRegionEraseEnabled, name: "ERASE_EN")
                .WithTaggedFlag("SCRAMBLE_EN", 3)
                .WithIgnoredBits(4, 1 + 31 - 4);
            
            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            Registers.BankCfgRegwen.Define(this, 0x1)
                .WithFlag(0, name: "BANK", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31);
            Registers.MpBankCfg.Define(this)
                .WithFlag(0, out eraseBank0, name: "ERASE_EN_0")
                .WithFlag(1, out eraseBank1, name: "ERASE_EN_1")
                .WithIgnoredBits(2, 1 + 31 - 2);
            
            Registers.OpStatus.Define(this)
                .WithFlag(0, out opStatusRegisterDoneFlag, name: "done")
                .WithFlag(1, out opStatusRegisterErrorFlag, name: "err")
                .WithIgnoredBits(2, 1 + 31 - 2);
            Registers.Status.Define(this, 0xa)
                .WithFlag(0, name: "rd_full", mode: FieldMode.Read)
                .WithFlag(1, out statusRegisterReadEmptyFlag, name: "rd_empty", mode: FieldMode.Read)
                .WithFlag(2, name: "prog_full", mode: FieldMode.Read)
                .WithFlag(3, name: "prog_empty", mode: FieldMode.Read)
                .WithFlag(4, name: "init_wip", mode: FieldMode.Read)
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "error_addr", mode: FieldMode.Read)
                .WithIgnoredBits(17, 1 + 31 - 17);
            Registers.PhyStatus.Define(this, 0x6)
                .WithFlag(0, name: "init_wip", mode: FieldMode.Read)
                .WithFlag(1, name: "prog_normal_avail", mode: FieldMode.Read)
                .WithFlag(2, name: "prog_repair_avail", mode: FieldMode.Read)
                .WithIgnoredBits(3, 1 + 31 - 3);
            
            Registers.Scratch.Define(this)
                .WithValueField(0, 32, name: "data");
            Registers.FifoLevel.Define(this, 0xf0f)
                .WithValueField(0, 5, name: "PROG")
                .WithReservedBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 12 - 8, name: "RD")
                .WithIgnoredBits(13, 1 + 31 - 13);

            // TODO(julianmb): implement fifo reset. There isnt any unittest for this currently.
            Registers.FifoRst.Define(this)
                .WithFlag(0, name: "EN")
                .WithIgnoredBits(1, 31);
            // TODO(julianmb): handle writes while fifo is full. There isnt any unittest for this currently.
            Registers.ProgramFifo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, writeCallback: (_, n) => {
                    if(!flashErasePartition.Value)
                    {
                        if(IsOperationAllowed(OperationType.ProgramData, programAddress))
                        {
                            dataFlash.WriteDoubleWord(programAddress - FlashMemBaseAddr, n);
                            programAddress += 4;
                        }
                        else
                        {
                            interruptStatusRegisterOpErrorFlag.Value |= true;
                            opStatusRegisterErrorFlag.Value |= true;
                        }
                    }
                    else
                    {
                        /*
                        ** TODO(julianmb): memory protection should be implemented.
                        ** It currently doesnt have a unit test though.
                        ** BANK*_INFO*_PAGE_CFG_* registers are of interest
                        */
                        infoFlash.WriteDoubleWord(programAddress - FlashMemBaseAddr, n);
                        programAddress += 4;
                    }

                    if(programAddress > (address.Value + 4 * (controlNum.Value)) || opStatusRegisterErrorFlag.Value)
                    {
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusRegisterOpDoneFlag.Value = true;
                    }
                });
            Registers.ReadFifo.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, valueProviderCallback: _ => {
                    uint value = 0;
                    if(!flashErasePartition.Value)
                    {
                        if(IsOperationAllowed(OperationType.ReadData, readAddress))
                        {
                            value = dataFlash.ReadDoubleWord(readAddress - FlashMemBaseAddr);
                            readAddress += 4;
                        }
                        else
                        {
                            interruptStatusRegisterOpErrorFlag.Value |= true;
                            opStatusRegisterErrorFlag.Value |= true;
                        }
                    }
                    else
                    {
                        /*
                        ** TODO(julianmb): memory protection should be implemented.
                        ** It currently doesnt have a unit test though.
                        ** BANK*_INFO*_PAGE_CFG_* registers are of interest
                        */
                        value = infoFlash.ReadDoubleWord(readAddress - FlashMemBaseAddr);
                        readAddress += 4;
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
                "OpenTitan_FlashController/StartReadOperation: reading {0}",
                !flashErasePartition.Value ? "DataPartition" : "InfoPartition");

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
                !flashErasePartition.Value ? "DataPartition" : "InfoPartition");

            programAddress = address.Value;
        }

        private void StartEraseOperation()
        {
            bool flashEraseBank0 = flashErasePage.Value && eraseBank0.Value;
            bool flashEraseBank1 = flashErasePage.Value && eraseBank1.Value;

            if(!flashErasePage.Value)
            {
                uint addrValue = RegistersCollection.Read((long)Registers.Addr);
                if(!flashErasePartition.Value)
                {
                    if(IsOperationAllowed(OperationType.EraseDataPage, addrValue))
                    {
                        long pageOffset = addrValue - FlashMemBaseAddr;
                        long bytesPerPage = FlashWordsPerPage * FlashWordSize;
                        for(long i = 0 ; i < bytesPerPage ; i++)
                        {
                            dataFlash.WriteByte(pageOffset + i, 0xff);
                        }
                    }
                    else
                    {
                        interruptStatusRegisterOpErrorFlag.Value = true;
                        opStatusRegisterErrorFlag.Value = true;
                    }
                }
                else // Erase info page
                {
                    long pageOffset = addrValue - FlashMemBaseAddr;
                    long bytesPerPage = FlashWordsPerPage * FlashWordSize;
                    for(long i = 0 ; i < bytesPerPage ; i++)
                    {
                        infoFlash.WriteByte(pageOffset + i, 0xff);
                    }
                }
            }
            else if(flashEraseBank0 || flashEraseBank1)
            {
                /*
                ** TODO(julianmb): memory protection should be implemented.
                ** It currently doesnt have a unit test though.
                ** MpBankCfg register is of interest
                */
                int bankNumber = flashEraseBank0 ? 0 : 1;

                long bytesPerBank = FlashPagesPerBank * FlashWordsPerPage * FlashWordSize;
                long bankFlashOffset = bankNumber * bytesPerBank;

                for(long i = 0 ; i < bytesPerBank ; i++)
                {
                    dataFlash.WriteByte(bankFlashOffset + i, 0xff);
                }
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
        private readonly IFlagRegisterField interruptStatusRegisterOpErrorFlag;
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

        private readonly IFlagRegisterField flashErasePage;
        private readonly IFlagRegisterField flashErasePartition;
        
        private readonly IFlagRegisterField eraseBank0;
        private readonly IFlagRegisterField eraseBank1;

        private readonly IValueRegisterField address;

        private readonly IEnumRegisterField<ControlOp> operation;

        private readonly IValueRegisterField controlNum;

        private const uint ReadFifoDepth = 16;
        private const uint ProgramFifoDepth = 16;
        private const uint FlashWordsPerPage = 128;
        private const uint FlashWordSize = 8;
        private const uint FlashPagesPerBank = 256;
        private const long FlashMemBaseAddr = 0x20000000;

        private const int NumberOfMpRegions = 8;

        private enum Registers : long
        {
            IntrState = 0x00, // Reset default = 0x0, mask 0x1f
            IntrEnable = 0x04, // Reset default = 0x0, mask 0x1f
            IntrTest = 0x08, // Reset default = 0x0, mask 0x1f
            CtrlRegwen = 0x0c, // Reset default = 0x1, mask 0x1
            Control = 0x10, // Reset default = 0x0, mask 0xfff07f1
            Addr = 0x14, // Reset default = 0x0, mask 0xffffffff
            RegionCfgRegwen0 = 0x18, // Reset default = 0x1, mask 0x1
            RegionCfgRegwen1 = 0x1c, // Reset default = 0x1, mask 0x1
            RegionCfgRegwen2 = 0x20, // Reset default = 0x1, mask 0x1
            RegionCfgRegwen3 = 0x24, // Reset default = 0x1, mask 0x1
            RegionCfgRegwen4 = 0x28, // Reset default = 0x1, mask 0x1
            RegionCfgRegwen5 = 0x2c, // Reset default = 0x1, mask 0x1
            RegionCfgRegwen6 = 0x30, // Reset default = 0x1, mask 0x1
            RegionCfgRegwen7 = 0x34, // Reset default = 0x1, mask 0x1
            MpRegionCfg0 = 0x38, // Reset default = 0x0, mask 0x7ffff7f
            MpRegionCfg1 = 0x3c, // Reset default = 0x0, mask 0x7ffff7f
            MpRegionCfg2 = 0x40, // Reset default = 0x0, mask 0x7ffff7f
            MpRegionCfg3 = 0x44, // Reset default = 0x0, mask 0x7ffff7f
            MpRegionCfg4 = 0x48, // Reset default = 0x0, mask 0x7ffff7f
            MpRegionCfg5 = 0x4c, // Reset default = 0x0, mask 0x7ffff7f
            MpRegionCfg6 = 0x50, // Reset default = 0x0, mask 0x7ffff7f
            MpRegionCfg7 = 0x54, // Reset default = 0x0, mask 0x7ffff7f
            Bank0Info0Regwen0 = 0x58, // Reset default = 0x1, mask 0x1
            Bank0Info0Regwen1 = 0x5c, // Reset default = 0x1, mask 0x1
            Bank0Info0Regwen2 = 0x60, // Reset default = 0x1, mask 0x1
            Bank0Info0Regwen3 = 0x64, // Reset default = 0x1, mask 0x1
            Bank0Info1Regwen0 = 0x68, // Reset default = 0x1, mask 0x1
            Bank0Info1Regwen1 = 0x6c, // Reset default = 0x1, mask 0x1
            Bank0Info1Regwen2 = 0x70, // Reset default = 0x1, mask 0x1
            Bank0Info1Regwen3 = 0x74, // Reset default = 0x1, mask 0x1
            Bank0Info0PageCfg0 = 0x78, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageCfg1 = 0x7c, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageCfg2 = 0x80, // Reset default = 0x0, mask 0x7f
            Bank0Info0PageCfg3 = 0x84, // Reset default = 0x0, mask 0x7f
            Bank0Info1PageCfg0 = 0x88, // Reset default = 0x0, mask 0x7f
            Bank0Info1PageCfg1 = 0x8c, // Reset default = 0x0, mask 0x7f
            Bank0Info1PageCfg2 = 0x90, // Reset default = 0x0, mask 0x7f
            Bank0Info1PageCfg3 = 0x94, // Reset default = 0x0, mask 0x7f
            Bank1Info0Regwen0 = 0x98, // Reset default = 0x1, mask 0x1
            Bank1Info0Regwen1 = 0x9c, // Reset default = 0x1, mask 0x1
            Bank1Info0Regwen2 = 0xa0, // Reset default = 0x1, mask 0x1
            Bank1Info0Regwen3 = 0xa4, // Reset default = 0x1, mask 0x1
            Bank1Info1Regwen0 = 0xa8, // Reset default = 0x1, mask 0x1
            Bank1Info1Regwen1 = 0xac, // Reset default = 0x1, mask 0x1
            Bank1Info1Regwen2 = 0xb0, // Reset default = 0x1, mask 0x1
            Bank1Info1Regwen3 = 0xb4, // Reset default = 0x1, mask 0x1
            Bank1Info0PageCfg0 = 0xb8, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageCfg1 = 0xbc, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageCfg2 = 0xc0, // Reset default = 0x0, mask 0x7f
            Bank1Info0PageCfg3 = 0xc4, // Reset default = 0x0, mask 0x7f
            Bank1Info1PageCfg0 = 0xc8, // Reset default = 0x0, mask 0x7f
            Bank1Info1PageCfg1 = 0xcc, // Reset default = 0x0, mask 0x7f
            Bank1Info1PageCfg2 = 0xd0, // Reset default = 0x0, mask 0x7f
            Bank1Info1PageCfg3 = 0xd4, // Reset default = 0x0, mask 0x7f
            DefaultRegion = 0xd8, // Reset default = 0x0, mask 0x3f
            BankCfgRegwen = 0xdc, // Reset default = 0x1, mask 0x1
            MpBankCfg = 0xe0, // Reset default = 0x0, mask 0x3
            OpStatus = 0xe4, // Reset default = 0x0, mask 0x3
            Status = 0xe8, // Reset default = 0xa, mask 0x1f
            PhyStatus = 0xec, // Reset default = 0x6, mask 0x7
            Scratch = 0xf0, // Reset default = 0x0, mask 0xffffffff
            FifoLevel = 0xf4, // Reset default = 0xf0f, mask 0x1f1f
            FifoRst = 0xf8, // Reset default = 0x0, mask 0x1
            ProgramFifo = 0xfc, // 1 item wo window. Byte writes are not supported
            ReadFifo = 0x100, // 1 item ro window. Byte writes are not supported
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
