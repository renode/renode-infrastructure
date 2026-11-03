//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU85
{
    private void DefineRegisters()
    {
        Registers.Id.Define(this, Identifier);

        Registers.Status.Define(this)
            .WithValueField(16, 16, out irqHistoryMask, FieldMode.Read, name: "irq_history_mask")
            .WithTag("faulting_channel", 12, 4)
            .WithTag("faulting_interface", 11, 1)
            .WithReservedBits(10, 1)
            .WithFlag(9, out branchFault, FieldMode.Read, name: "branch_fault")
            .WithTaggedFlag("ecc_fault", 8)
            .WithReservedBits(7, 1)
            .WithTaggedFlag("pmu_irq_raised", 6)
            .WithFlag(5, out commandEndReached, FieldMode.Read, name: "cmd_end_reached")
            .WithFlag(4, out commandParseError, FieldMode.Read, name: "cmd_parse_error")
            .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "reset_status")
            .WithFlag(2, out busStatus, FieldMode.Read, name: "bus_status")
            .WithFlag(1, out irqRaised, FieldMode.Read, name: "irq_raised")
            .WithFlag(0, out isRunning, FieldMode.Read, name: "state");

        Registers.Command.Define(this, 0xC)
            .WithValueField(16, 16, writeCallback: (_, value) => irqHistoryMask.Value &= ~value, name: "clear_irq_history")
            .WithReservedBits(5, 11)
            .WithFlag(4, writeCallback: (_, value) =>
            {
                // The stream runs to completion inside the CMD write, so the NPU is
                // never running when this arrives. In stopped state it has no effect.
                if(value && isRunning.Value)
                {
                    this.WarningLog("stop_request while running is not supported");
                }
            }, name: "stop_request")
            .WithFlag(3, name: "power_q_enable")
            .WithFlag(2, name: "clock_q_enable")
            .WithFlag(1, writeCallback: (_, value) =>
            {
                if(value)
                {
                    irqRaised.Value = false;
                    UpdateInterrupts();
                }
            }, name: "clear_irq")
            .WithFlag(0, writeCallback: (_, value) =>
            {
                if(value)
                {
                    ExecuteCommandStream();
                }
            }, name: "transition_to_running_state");

        Registers.Reset.Define(this)
            .WithReservedBits(2, 30)
            .WithEnumField(1, 1, out pendingSecurityLevel, name: "pending_CSL")
            .WithEnumField(0, 1, out pendingPrivilegeLevel, name: "pending_CPL")
            .WithWriteCallback((_, __) => SoftReset());

        Registers.QueueBase0.Define(this)
            .WithValueField(0, 32, out queueBase0, writeCallback: (_, _) => QueueUpdated(), name: "QBASE[31:0]");

        Registers.QueueBase1.Define(this)
            .WithReservedBits(8, 24)
            .WithValueField(0, 8, out queueBase1, writeCallback: (_, _) => QueueUpdated(), name: "QBASE[39:32]");

        Registers.QueueRead.Define(this)
            .WithValueField(0, 32, out queueReadOffset, FieldMode.Read, name: "QREAD");

        Registers.QueueConfig.Define(this)
            .WithReservedBits(2, 30)
            .WithValueField(0, 2, out queueConfig, name: "cmd_region0"); // selects MEM_ATTR0-3 for command fetch

        Registers.QueueSize.Define(this)
            .WithValueField(0, 32, out queueSize, writeCallback: (_, _) => QueueUpdated(), name: "QSIZE");

        Registers.Protection.Define(this)
            .WithReservedBits(2, 30)
            .WithEnumField(1, 1, out activeSecurityLevel, FieldMode.Read, name: "active_CSL")
            .WithEnumField(0, 1, out activePrivilegeLevel, FieldMode.Read, name: "active_CPL");

        Registers.Config.Define(this)
            .WithValueField(28, 4, FieldMode.Read, valueProviderCallback: _ => ProductConfiguration, name: "product")
            .WithFlag(27, FieldMode.Read, valueProviderCallback: _ => false, name: "custom_dma")
            .WithReservedBits(14, 13)
            .WithValueField(12, 2, FieldMode.Read, valueProviderCallback: _ => configs.NumWdLog2, name: "num_wd")
            .WithReservedBits(11, 1)
            .WithValueField(10, 1, FieldMode.Read, valueProviderCallback: _ => configs.AxiExtLog2, name: "num_axi_ext")
            .WithValueField(8, 2, FieldMode.Read, valueProviderCallback: _ => configs.AxiSramLog2, name: "num_axi_sram")
            .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => CommandStreamVersion, name: "cmd_stream_version")
            .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => configs.MacsLog2, name: "macs_per_cc");

        Registers.ConditionStatus.Define(this)
            .WithReservedBits(1, 31)
            .WithFlag(0, out conditionResultFlag, FieldMode.Read, name: "result_flag");

        Registers.PowerControl.Define(this)
            .WithReservedBits(6, 26)
            .WithValueField(0, 6, name: "mac_step_cycles");

        var regionConfigRegister = Registers.RegionConfig.Define(this)
            .WithReservedBits(16, 16);
        for(var i = 0; i < RegionCount; i++)
        {
            regionConfigRegister.WithValueField(i * 2, 2, out regionConfig[i], name: $"region{i}"); // MEM_ATTR index
        }

        Registers.MemoryAttribute0.DefineMany(this, MemoryAttributeCount, (register, index) =>
        {
            register
                .WithReservedBits(8, 24)
                .WithValueField(4, 4, out memoryType[index], name: "memtype") // AxCACHE encoding
                .WithReservedBits(3, 1)
                .WithFlag(2, out axiPortExternal[index], name: "axi_port") // 0 = SRAM, 1 = EXT
                .WithValueField(0, 2, out memoryDomain[index], name: "mem_domain"); // AxDOMAIN
        }, stepInBytes: 4);

        // AXI_SRAM (0x50) and AXI_EXT (0x54) share a layout.
        Registers.AxiSram.DefineMany(this, 2, (register, index) =>
        {
            register
                .WithReservedBits(18, 14)
                .WithValueField(16, 2, name: "max_beats") // 0 = 64 B, 1 = 128 B, 2 = 256 B
                .WithReservedBits(13, 3)
                .WithValueField(8, 5, name: "max_outstanding_write_m1")
                .WithReservedBits(6, 2)
                .WithValueField(0, 6, name: "max_outstanding_read_m1");
        }, stepInBytes: 4);

        // CFG_SRAM_CAP (0x60) and CFG_EXT_CAP (0x64): read-only copies of the CFGSRAMCAP/CFGEXTCAP pins.
        Registers.ConfigSramCap.DefineMany(this, 2, (register, index) =>
        {
            var pins = index == 0 ? configSramCapPins : configExtCapPins;
            register
                .WithReservedBits(18, 14)
                .WithValueField(16, 2, FieldMode.Read, valueProviderCallback: _ => (pins >> 16) & 0x3, name: "max_beats")
                .WithReservedBits(13, 3)
                .WithValueField(8, 5, FieldMode.Read, valueProviderCallback: _ => (pins >> 8) & 0x1F, name: "max_outstanding_write_m1")
                .WithReservedBits(6, 2)
                .WithValueField(0, 6, FieldMode.Read, valueProviderCallback: _ => pins & 0x3F, name: "max_outstanding_read_m1");
        }, stepInBytes: 4);

        // CFG_SRAM_HASH0/1 and CFG_EXT_HASH0: 40-bit pin values, bits 5:0 always zero.
        DefineHashRegisters(Registers.ConfigSramHash0Low, Registers.ConfigSramHash0High, configSramHash0Pins, "CFG_SRAM_HASH0");
        DefineHashRegisters(Registers.ConfigSramHash1Low, Registers.ConfigSramHash1High, configSramHash1Pins, "CFG_SRAM_HASH1");
        DefineHashRegisters(Registers.ConfigExtHash0Low, Registers.ConfigExtHash0High, configExtHash0Pins, "CFG_EXT_HASH0");

        // BASEP[0..7]: eight 64-bit pointers seen as 16 words. Only bits 39:0 are used.
        Registers.BasePointer0.DefineMany(this, BasePointerCount, (register, index) =>
        {
            if(index % 2 == 0)
            {
                register.WithValueField(0, 32, out basePointers[index], name: $"BASEP{index / 2}[31:0]");
            }
            else
            {
                register
                    .WithReservedBits(8, 24)
                    .WithValueField(0, 8, out basePointers[index], name: $"BASEP{index / 2}[39:32]");
            }
        }, stepInBytes: 4);

        Registers.PeripheralId4.Define(this, 0x04);
        Registers.PeripheralId5.Define(this, 0x00);
        Registers.PeripheralId6.Define(this, 0x00);
        Registers.PeripheralId7.Define(this, 0x00);
        Registers.PeripheralId0.Define(this, 0x82);
        Registers.PeripheralId1.Define(this, 0xB5);
        Registers.PeripheralId2.Define(this, 0x0B);
        Registers.PeripheralId3.Define(this, 0x00);
        Registers.ComponentId0.Define(this, 0x0D);
        Registers.ComponentId1.Define(this, 0xF0);
        Registers.ComponentId2.Define(this, 0x05);
        Registers.ComponentId3.Define(this, 0xB1);
    }

    private void DefineHashRegisters(Registers low, Registers high, ulong pins, string name)
    {
        low.Define(this)
            .WithValueField(6, 26, FieldMode.Read, valueProviderCallback: _ => (pins >> 6) & 0x3FFFFFF, name: $"{name}[31:6]")
            .WithReservedBits(0, 6);
        high.Define(this)
            .WithReservedBits(8, 24)
            .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => (pins >> 32) & 0xFF, name: $"{name}[39:32]");
    }

    private IFlagRegisterField branchFault;
    private IFlagRegisterField conditionResultFlag;
    private IValueRegisterField queueConfig;
    private readonly IValueRegisterField[] basePointers = new IValueRegisterField[BasePointerCount];
    private readonly IValueRegisterField[] regionConfig = new IValueRegisterField[RegionCount];
    private readonly IValueRegisterField[] memoryType = new IValueRegisterField[MemoryAttributeCount];
    private readonly IFlagRegisterField[] axiPortExternal = new IFlagRegisterField[MemoryAttributeCount];
    private readonly IValueRegisterField[] memoryDomain = new IValueRegisterField[MemoryAttributeCount];

    // Integration pins: 0 means a single port of each type, with limits from AXI_SRAM/AXI_EXT only.
    private readonly ulong configSramCapPins = 0;
    private readonly ulong configExtCapPins = 0;
    private readonly ulong configSramHash0Pins = 0;
    private readonly ulong configSramHash1Pins = 0;
    private readonly ulong configExtHash0Pins = 0;

    private const uint Identifier = 0x20007001;
    private const ulong ProductConfiguration = 2; // CONFIG.product reset value
    private const ulong CommandStreamVersion = 1; // CONFIG.cmd_stream_version reset value
    private const int RegionCount = 8;
    private const int MemoryAttributeCount = 4;
    private const uint BasePointerCount = 16;

    private enum Registers : long
    {
        Id = 0x00,
        Status = 0x04,
        Command = 0x08,
        Reset = 0x0C,
        QueueBase0 = 0x10,
        QueueBase1 = 0x14,
        QueueRead = 0x18,
        QueueConfig = 0x1C,
        QueueSize = 0x20,
        Protection = 0x24,
        Config = 0x28,
        ConditionStatus = 0x30,
        PowerControl = 0x38,
        RegionConfig = 0x3C,
        MemoryAttribute0 = 0x40,
        MemoryAttribute1 = 0x44,
        MemoryAttribute2 = 0x48,
        MemoryAttribute3 = 0x4C,
        AxiSram = 0x50,
        AxiExt = 0x54,
        ConfigSramCap = 0x60,
        ConfigExtCap = 0x64,
        ConfigSramHash0Low = 0x68,
        ConfigSramHash0High = 0x6C,
        ConfigSramHash1Low = 0x70,
        ConfigSramHash1High = 0x74,
        ConfigExtHash0Low = 0x78,
        ConfigExtHash0High = 0x7C,
        BasePointer0 = 0x80,
        BasePointer1 = 0x84,
        BasePointer2 = 0x88,
        BasePointer3 = 0x8C,
        BasePointer4 = 0x90,
        BasePointer5 = 0x94,
        BasePointer6 = 0x98,
        BasePointer7 = 0x9C,
        BasePointer8 = 0xA0,
        BasePointer9 = 0xA4,
        BasePointer10 = 0xA8,
        BasePointer11 = 0xAC,
        BasePointer12 = 0xB0,
        BasePointer13 = 0xB4,
        BasePointer14 = 0xB8,
        BasePointer15 = 0xBC,
        PeripheralId4 = 0xFD0,
        PeripheralId5 = 0xFD4,
        PeripheralId6 = 0xFD8,
        PeripheralId7 = 0xFDC,
        PeripheralId0 = 0xFE0,
        PeripheralId1 = 0xFE4,
        PeripheralId2 = 0xFE8,
        PeripheralId3 = 0xFEC,
        ComponentId0 = 0xFF0,
        ComponentId1 = 0xFF4,
        ComponentId2 = 0xFF8,
        ComponentId3 = 0xFFC,
    }

    private enum SecurityLevel
    {
        Secure = 0,
        NonSecure = 1,
    }

    private enum PrivilegeLevel
    {
        User = 0,
        Privileged = 1,
    }
}
