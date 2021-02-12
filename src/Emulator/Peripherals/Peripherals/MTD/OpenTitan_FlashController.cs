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
            // TODO(julianmb): support interrupts. no unittests exist though.
            RegistersCollection.AddRegister((long)Registers.IntrState, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "prog_empty", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(1, name: "prog_lvl", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(2, name: "rd_full", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(3, name: "rd_lvl", mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(4, out interruptStatusRegisterOpDoneFlag, name: "op_done",
                    mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithFlag(5, out interruptStatusRegisterOpErrorFlag, name: "op_error",
                    mode: FieldMode.Read | FieldMode.WriteOneToClear)
                .WithIgnoredBits(6, 1 + 31 - 6));

            // TODO(julianmb): support interrupts. no unittests exist though.
            RegistersCollection.AddRegister((long)Registers.IntrEnable, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "prog_empty")
                .WithFlag(1, name: "prog_lvl")
                .WithFlag(2, name: "rd_full")
                .WithFlag(3, name: "rd_lvl")
                .WithFlag(4, name: "op_done")
                .WithFlag(5, name: "op_error")
                .WithIgnoredBits(6, 1 + 31 - 6));

            // TODO(julianmb): support interrupts. no unittests exist though.
            RegistersCollection.AddRegister((long)Registers.IntrTest, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "prog_empty", mode: FieldMode.Write)
                .WithFlag(1, name: "prog_lvl", mode: FieldMode.Write)
                .WithFlag(2, name: "rd_full", mode: FieldMode.Write)
                .WithFlag(3, name: "rd_lvl", mode: FieldMode.Write)
                .WithFlag(4, name: "op_done", mode: FieldMode.Write)
                .WithIgnoredBits(5, 1 + 31 - 5));

            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            RegistersCollection.AddRegister((long)Registers.CtrlRegwen, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "EN", mode: FieldMode.Read)
                .WithIgnoredBits(1, 31));

            RegistersCollection.AddRegister((long)Registers.Control, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "START", writeCallback: (o, n) => {
                    if(n)
                    {
                        StartOperation();
                    }
                })
                .WithReservedBits(1, 3)
                .WithValueField(4, 2, name: "OP")
                .WithFlag(6, name: "PROG_SEL")
                .WithFlag(7, name: "ERASE_SEL")
                .WithFlag(8, name: "PARTITION_SEL")
                .WithValueField(9, 2, name: "INFO_SEL")
                .WithIgnoredBits(11, 1 + 15 - 11)
                .WithValueField(16, 1 + 27 - 16, name: "NUM")
                .WithIgnoredBits(28, 1 + 31 - 28));

            RegistersCollection.AddRegister((long)Registers.Addr, new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "START"));

            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen0, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen1, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen2, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen3, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen4, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_4", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen5, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_5", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen6, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_6", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.RegionCfgRegwen7, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_7", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));

            RegistersCollection.AddRegister((long)Registers.MpRegionCfg0, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_0")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_0")
                .WithIgnoredBits(30, 1 + 31 - 30));
            RegistersCollection.AddRegister((long)Registers.MpRegionCfg1, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_1")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_1")
                .WithIgnoredBits(30, 1 + 31 - 30));
            RegistersCollection.AddRegister((long)Registers.MpRegionCfg2, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_2")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_2")
                .WithIgnoredBits(30, 1 + 31 - 30));
            RegistersCollection.AddRegister((long)Registers.MpRegionCfg3, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_3")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_3")
                .WithIgnoredBits(30, 1 + 31 - 30));
            RegistersCollection.AddRegister((long)Registers.MpRegionCfg4, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_4")
                .WithFlag(1, name: "RD_EN_4")
                .WithFlag(2, name: "PROG_EN_4")
                .WithFlag(3, name: "ERASE_EN_4")
                .WithFlag(4, name: "SCRAMBLE_EN_4")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_4")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_4")
                .WithIgnoredBits(30, 1 + 31 - 30));
            RegistersCollection.AddRegister((long)Registers.MpRegionCfg5, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_5")
                .WithFlag(1, name: "RD_EN_5")
                .WithFlag(2, name: "PROG_EN_5")
                .WithFlag(3, name: "ERASE_EN_5")
                .WithFlag(4, name: "SCRAMBLE_EN_5")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_5")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_5")
                .WithIgnoredBits(30, 1 + 31 - 30));
            RegistersCollection.AddRegister((long)Registers.MpRegionCfg6, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_6")
                .WithFlag(1, name: "RD_EN_6")
                .WithFlag(2, name: "PROG_EN_6")
                .WithFlag(3, name: "ERASE_EN_6")
                .WithFlag(4, name: "SCRAMBLE_EN_6")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_6")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_6")
                .WithIgnoredBits(30, 1 + 31 - 30));
            RegistersCollection.AddRegister((long)Registers.MpRegionCfg7, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_7")
                .WithFlag(1, name: "RD_EN_7")
                .WithFlag(2, name: "PROG_EN_7")
                .WithFlag(3, name: "ERASE_EN_7")
                .WithFlag(4, name: "SCRAMBLE_EN_7")
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "BASE_7")
                .WithIgnoredBits(17, 1 + 19 - 17)
                .WithValueField(20, 1 + 29 - 20, name: "SIZE_7")
                .WithIgnoredBits(30, 1 + 31 - 30));

            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            RegistersCollection.AddRegister((long)Registers.Bank0Info0Regwen0, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank0Info0Regwen1, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank0Info0Regwen2, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank0Info0Regwen3, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1Regwen0, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1Regwen1, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1Regwen2, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1Regwen3, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            
            RegistersCollection.AddRegister((long)Registers.Bank0Info0PageCfg0, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank0Info0PageCfg1, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank0Info0PageCfg2, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank0Info0PageCfg3, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1PageCfg0, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1PageCfg1, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1PageCfg2, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank0Info1PageCfg3, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5));
            
            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            RegistersCollection.AddRegister((long)Registers.Bank1Info0Regwen0, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank1Info0Regwen1, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank1Info0Regwen2, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank1Info0Regwen3, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1Regwen0, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_0", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1Regwen1, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_1", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1Regwen2, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_2", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1Regwen3, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "REGION_3", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));

            RegistersCollection.AddRegister((long)Registers.Bank1Info0PageCfg0, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank1Info0PageCfg1, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank1Info0PageCfg2, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank1Info0PageCfg3, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1PageCfg0, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_0")
                .WithFlag(1, name: "RD_EN_0")
                .WithFlag(2, name: "PROG_EN_0")
                .WithFlag(3, name: "ERASE_EN_0")
                .WithFlag(4, name: "SCRAMBLE_EN_0")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1PageCfg1, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_1")
                .WithFlag(1, name: "RD_EN_1")
                .WithFlag(2, name: "PROG_EN_1")
                .WithFlag(3, name: "ERASE_EN_1")
                .WithFlag(4, name: "SCRAMBLE_EN_1")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1PageCfg2, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_2")
                .WithFlag(1, name: "RD_EN_2")
                .WithFlag(2, name: "PROG_EN_2")
                .WithFlag(3, name: "ERASE_EN_2")
                .WithFlag(4, name: "SCRAMBLE_EN_2")
                .WithIgnoredBits(5, 1 + 31 - 5));
            RegistersCollection.AddRegister((long)Registers.Bank1Info1PageCfg3, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN_3")
                .WithFlag(1, name: "RD_EN_3")
                .WithFlag(2, name: "PROG_EN_3")
                .WithFlag(3, name: "ERASE_EN_3")
                .WithFlag(4, name: "SCRAMBLE_EN_3")
                .WithIgnoredBits(5, 1 + 31 - 5));
            
            RegistersCollection.AddRegister((long)Registers.DefaultRegion, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "RD_EN")
                .WithFlag(1, name: "PROG_EN")
                .WithFlag(2, name: "ERASE_EN")
                .WithFlag(3, name: "SCRAMBLE_EN")
                .WithIgnoredBits(4, 1 + 31 - 4));
            
            // TODO(julianmb): support register write enable. this isnt tested in the unittests currently
            RegistersCollection.AddRegister((long)Registers.BankCfgRegwen, new DoubleWordRegister(this, 0x1)
                .WithFlag(0, name: "BANK", mode: FieldMode.Read | FieldMode.WriteZeroToClear)
                .WithIgnoredBits(1, 31));
            RegistersCollection.AddRegister((long)Registers.MpBankCfg, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "ERASE_EN_0")
                .WithFlag(1, name: "ERASE_EN_1")
                .WithIgnoredBits(2, 1 + 31 - 2));
            
            RegistersCollection.AddRegister((long)Registers.OpStatus, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, out opStatusRegisterDoneFlag, name: "done")
                .WithFlag(1, out opStatusRegisterErrorFlag, name: "err")
                .WithIgnoredBits(2, 1 + 31 - 2));
            RegistersCollection.AddRegister((long)Registers.Status, new DoubleWordRegister(this, 0xa)
                .WithFlag(0, name: "rd_full", mode: FieldMode.Read)
                .WithFlag(1, out statusRegisterReadEmptyFlag, name: "rd_empty", mode: FieldMode.Read)
                .WithFlag(2, name: "prog_full", mode: FieldMode.Read)
                .WithFlag(3, name: "prog_empty", mode: FieldMode.Read)
                .WithFlag(4, name: "init_wip", mode: FieldMode.Read)
                .WithIgnoredBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 16 - 8, name: "error_addr", mode: FieldMode.Read)
                .WithIgnoredBits(17, 1 + 31 - 17));
            RegistersCollection.AddRegister((long)Registers.PhyStatus, new DoubleWordRegister(this, 0x6)
                .WithFlag(0, name: "init_wip", mode: FieldMode.Read)
                .WithFlag(1, name: "prog_normal_avail", mode: FieldMode.Read)
                .WithFlag(2, name: "prog_repair_avail", mode: FieldMode.Read)
                .WithIgnoredBits(3, 1 + 31 - 3));
            
            RegistersCollection.AddRegister((long)Registers.Scratch, new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, name: "data"));
            RegistersCollection.AddRegister((long)Registers.FifoLevel, new DoubleWordRegister(this, 0xf0f)
                .WithValueField(0, 5, name: "PROG")
                .WithReservedBits(5, 1 + 7 - 5)
                .WithValueField(8, 1 + 12 - 8, name: "RD")
                .WithIgnoredBits(13, 1 + 31 - 13));

            // TODO(julianmb): implement fifo reset. There isnt any unittest for this currently.
            RegistersCollection.AddRegister((long)Registers.FifoRst, new DoubleWordRegister(this, 0x0)
                .WithFlag(0, name: "EN")
                .WithIgnoredBits(1, 31));
            // TODO(julianmb): handle writes while fifo is full. There isnt any unittest for this currently.
            RegistersCollection.AddRegister((long)Registers.ProgramFifo, new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, mode: FieldMode.Write, writeCallback: (_, n) => {
                    uint controlValue = RegistersCollection.Read((long)Registers.Control);
                    bool flashProgramDataPartition = !BitHelper.IsBitSet(controlValue, 8);
                    uint controlNumValue = BitHelper.GetValue(controlValue, 16, 12);
                    uint addrValue = RegistersCollection.Read((long)Registers.Addr);
                    if(flashProgramDataPartition)
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

                    if(programAddress > (addrValue + 4 * (controlNumValue)) || opStatusRegisterErrorFlag.Value)
                    {
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusRegisterOpDoneFlag.Value = true;
                    }
                }));
            RegistersCollection.AddRegister((long)Registers.ReadFifo, new DoubleWordRegister(this, 0x0)
                .WithValueField(0, 32, mode: FieldMode.Read, valueProviderCallback: _ => {
                    uint addrValue = RegistersCollection.Read((long)Registers.Addr);
                    uint controlValue = RegistersCollection.Read((long)Registers.Control);
                    bool flashReadDataPartition = !BitHelper.IsBitSet(controlValue, 8);
                    uint controlNumValue = BitHelper.GetValue(controlValue, 16, 12);
                    uint value = 0;
                    if(flashReadDataPartition)
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

                    if(readAddress > (addrValue + 4 * (controlNumValue)) || opStatusRegisterErrorFlag.Value){
                        opStatusRegisterDoneFlag.Value = true;
                        interruptStatusRegisterOpDoneFlag.Value = true;
                        statusRegisterReadEmptyFlag.Value = true;
                    }

                    return value;
                }));
            dataFlash = flash;
            infoFlash = new ArrayMemory((int)dataFlash.Size);
            this.Reset();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            statusRegisterReadEmptyFlag.Value = true;
        }

        private void StartOperation()
        {
            uint controlOpValue = BitHelper.GetValue(
                RegistersCollection.Read((long)Registers.Control),
                4, 2);

            bool flashReadOp = controlOpValue == (uint)ControlOp.FlashRead;
            bool flashProgramOp = controlOpValue == (uint)ControlOp.FlashProgram;
            bool flashEraseOp = controlOpValue == (uint)ControlOp.FlashErase;

            if(flashReadOp)
            {
                this.Log(
                    LogLevel.Noisy,
                    "OpenTitan_FlashController/StartOperation: Read");
                StartReadOperation();
            }
            else if(flashProgramOp)
            {
                this.Log(
                    LogLevel.Noisy,
                    "OpenTitan_FlashController/StartOperation: Program");
                StartProgramOperation();
            }
            else if(flashEraseOp)
            {
                this.Log(
                    LogLevel.Noisy,
                    "OpenTitan_FlashController/StartOperation: Erase");
                StartEraseOperation();
            }
            else
            {
                this.Log(
                    LogLevel.Warning,
                    "OpenTitan_FlashController/StartOperation: invalid controlOpValue");
            }
        }

        private void StartReadOperation()
        {
            uint addrValue = RegistersCollection.Read((long)Registers.Addr);

            uint controlValue = RegistersCollection.Read((long)Registers.Control);
            bool flashReadDataPartition = !BitHelper.IsBitSet(controlValue, 8);
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartReadOperation: reading {0}",
                flashReadDataPartition ? "DataPartition" : "InfoPartition");

            statusRegisterReadEmptyFlag.Value = false;
            readAddress = addrValue;
        }

        private void StartProgramOperation()
        {
            uint addrValue = RegistersCollection.Read((long)Registers.Addr);

            uint controlValue = RegistersCollection.Read((long)Registers.Control);
            bool flashProgramDataPartition = !BitHelper.IsBitSet(controlValue, 8);
            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartProgramOperation: addrValue = 0x{0:X}",
                addrValue);

            this.Log(
                LogLevel.Noisy,
                "OpenTitan_FlashController/StartProgramOperation: programming {0}",
                flashProgramDataPartition ? "DataPartition" : "InfoPartition");

            programAddress = addrValue;
        }

        private void StartEraseOperation()
        {
            uint controlValue = RegistersCollection.Read((long)Registers.Control);
            bool flashErasePage = !BitHelper.IsBitSet(controlValue, 7);
            bool flashEraseDataPartition = !BitHelper.IsBitSet(controlValue, 8);

            uint MpBankCfgValue = RegistersCollection.Read((long)Registers.MpBankCfg);
            bool flashEraseBank0 = !flashErasePage && BitHelper.IsBitSet(MpBankCfgValue, 0);
            bool flashEraseBank1 = !flashErasePage && BitHelper.IsBitSet(MpBankCfgValue, 1);

            if(flashErasePage)
            {
                uint addrValue = RegistersCollection.Read((long)Registers.Addr);
                if(flashEraseDataPartition)
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
            uint defaultRegionValue = RegistersCollection.Read((long)Registers.DefaultRegion);
            bool defaultRegionReadEn = BitHelper.IsBitSet(defaultRegionValue, 0);
            bool defaultRegionProgEn = BitHelper.IsBitSet(defaultRegionValue, 1);
            bool defaultRegionEraseEn = BitHelper.IsBitSet(defaultRegionValue, 2);

            bool ret = (opType == OperationType.ReadData && defaultRegionReadEn)
                || (opType == OperationType.ProgramData && defaultRegionProgEn)
                || (opType == OperationType.EraseDataPage && defaultRegionEraseEn);

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

            long[] protectionRegionRegisters = new long[] {
                (long)Registers.MpRegionCfg0,
                (long)Registers.MpRegionCfg1,
                (long)Registers.MpRegionCfg2,
                (long)Registers.MpRegionCfg3,
                (long)Registers.MpRegionCfg4,
                (long)Registers.MpRegionCfg5,
                (long)Registers.MpRegionCfg6,
                (long)Registers.MpRegionCfg7
            };

            foreach(long mpRegisterOffset in protectionRegionRegisters)
            {
                if(MpRegionRegisterAppliesToOperation(mpRegisterOffset, operationAddress)
                    && IsOperationAllowedInMpRegion(opType, mpRegisterOffset))
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

        private bool IsOperationAllowedInMpRegion(OperationType opType, long mpRegisterOffset)
        {
            uint mpRegionCfgNValue = RegistersCollection.Read(mpRegisterOffset);
            bool mpRegionCfgNEn = BitHelper.IsBitSet(mpRegionCfgNValue, 0);
            bool mpRegionCfgNRdEnFlag = BitHelper.IsBitSet(mpRegionCfgNValue, 1);
            bool mpRegionCfgNProgEnFlag = BitHelper.IsBitSet(mpRegionCfgNValue, 2);
            bool mpRegionCfgNEraseEnFlag = BitHelper.IsBitSet(mpRegionCfgNValue, 3);

            bool ret = (opType == OperationType.ReadData && mpRegionCfgNRdEnFlag)
                || (opType == OperationType.ProgramData && mpRegionCfgNProgEnFlag)
                || (opType == OperationType.EraseDataPage && mpRegionCfgNEraseEnFlag);
            ret = ret && mpRegionCfgNEn;

            if(mpRegionCfgNEn && !ret)
            {
                this.Log(
                    LogLevel.Debug, "OpenTitan_FlashController/IsOperationAllowedInMpRegion: " +
                    "Operation not allowed!");
            }

            return ret;
        }

        private bool MpRegionRegisterAppliesToOperation(long mpRegisterOffset, long operationAddress)
        {
            uint mpRegionCfgNValue = RegistersCollection.Read(mpRegisterOffset);
            bool mpRegionCfgNEn = BitHelper.IsBitSet(mpRegionCfgNValue, 0);
            uint mpRegionCfgNBase = BitHelper.GetValue(mpRegionCfgNValue, 8, 9);
            uint mpRegionCfgNSize = BitHelper.GetValue(mpRegionCfgNValue, 20, 10);

            long regionStart = FlashMemBaseAddr + (mpRegionCfgNBase * FlashWordsPerPage * FlashWordSize);
            long regionEnd = regionStart + (mpRegionCfgNSize * FlashWordsPerPage * FlashWordSize);

            // The bus is 4 bytes wide. Should that effect the border logic?
            return (mpRegionCfgNEn
                && operationAddress >= regionStart
                && operationAddress < regionEnd);
        }

        private readonly uint FlashWordsPerPage = 128;
        private readonly uint FlashWordSize = 8;
        private readonly uint FlashPagesPerBank = 256;
        private readonly long FlashMemBaseAddr = 0x20000000;
        public long Size => 0x1000;
        private readonly MappedMemory dataFlash;
        private readonly ArrayMemory infoFlash;
        private readonly uint ReadFifoDepth = 16;
        private readonly uint ProgramFifoDepth = 16;
        private IFlagRegisterField opStatusRegisterDoneFlag;
        private IFlagRegisterField opStatusRegisterErrorFlag;
        private IFlagRegisterField interruptStatusRegisterOpDoneFlag;
        private IFlagRegisterField interruptStatusRegisterOpErrorFlag;
        private IFlagRegisterField statusRegisterReadEmptyFlag;
        private long readAddress;
        private long programAddress;

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
