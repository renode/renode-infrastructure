//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.ARM_Ethos;

public partial class ARM_EthosU55
{
    private void DefineRegisters()
    {
        Registers.Id.Define(this, Identifier);

        Registers.Status.Define(this)
            .WithValueField(16, 16, out irqHistoryMask, FieldMode.Read, name: "irq_history_mask")
            .WithTag("faulting_channel", 12, 4)
            .WithTag("faulting_interface", 11, 1)
            .WithReservedBits(9, 2)
            .WithTaggedFlag("ecc_fault", 8)
            .WithTaggedFlag("wd_fault", 7)
            .WithTaggedFlag("pmu_irq_raised", 6)
            .WithFlag(5, out commandEndReached, FieldMode.Read, name: "cmd_end_reached")
            .WithFlag(4, out commandParseError, FieldMode.Read, name: "cmd_parse_error")
            .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "reset_status")
            .WithFlag(2, out busStatus, FieldMode.Read, name: "bus_status")
            .WithFlag(1, out irqRaised, FieldMode.Read, name: "irq_raised")
            .WithFlag(0, out isRunning, FieldMode.Read, name: "state");

        Registers.Command.Define(this, 0xC)
            .WithValueField(16, 16, writeCallback: (_, value) => irqHistoryMask.Value &= ~value, name: "clear_irq_history")
            .WithReservedBits(4, 12)
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
            .WithWriteCallback((_, _) => SoftReset());

        Registers.QueueBase0.Define(this)
            .WithValueField(0, 32, out queueBase0, writeCallback: (_, _) => QueueUpdated(), name: "QBASE0");

        Registers.QueueBase1.Define(this)
            .WithValueField(0, 32, out queueBase1, writeCallback: (_, _) => QueueUpdated(), name: "QBASE1");

        Registers.QueueRead.Define(this)
            .WithValueField(0, 32, out queueReadOffset, FieldMode.Read, name: "QREAD");

        Registers.QueueConfig.Define(this)
            .WithValueField(0, 32, name: "QCONFIG");

        Registers.QueueSize.Define(this)
            .WithValueField(0, 32, out queueSize, writeCallback: (_, _) => QueueUpdated(), name: "QSIZE");

        Registers.Protection.Define(this)
            .WithReservedBits(2, 30)
            .WithEnumField(1, 1, out activeSecurityLevel, FieldMode.Read, name: "active_CSL")
            .WithEnumField(0, 1, out activePrivilegeLevel, FieldMode.Read, name: "active_CPL");

        Registers.Config.Define(this)
            .WithValueField(28, 4, FieldMode.Read, name: "product")
            .WithFlag(27, FieldMode.Read, name: "custom_dma")
            .WithReservedBits(16, 11)
            .WithValueField(8, 8, FieldMode.Read, valueProviderCallback: _ => SharedBufferSizeInKilobytes, name: "shram_size")
            .WithValueField(4, 4, FieldMode.Read, name: "cmd_stream_version")
            .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => macsPerCycle == 128 ? 7u : 8u, name: "macs_per_cc");

        Registers.Lock.Define(this)
            .WithValueField(0, 32, out lockValue, writeCallback: (value, _) =>
            {
                if(value == 0 || lockValue.Value == 0)
                {
                    lockValue.Value = value;
                }
            }, name: "LOCK");

        Registers.PeripheralId4.Define(this, 0x04);
        Registers.PeripheralId5.Define(this, 0x00);
        Registers.PeripheralId6.Define(this, 0x00);
        Registers.PeripheralId7.Define(this, 0x00);
        Registers.PeripheralId0.Define(this, 0x80);
        Registers.PeripheralId1.Define(this, 0xB5);
        Registers.PeripheralId2.Define(this, 0x0B);
        Registers.PeripheralId3.Define(this, 0x00);
        Registers.ComponentId0.Define(this, 0x0D);
        Registers.ComponentId1.Define(this, 0xF0);
        Registers.ComponentId2.Define(this, 0x05);
        Registers.ComponentId3.Define(this, 0xB1);

        Registers.BasePointer0.DefineMany(this, BasePointerCount,
            (register, index) =>
            {
                register.WithValueField(0, 32, out basePointers[index], name: $"BASEP{index}");
            }, stepInBytes: 4);
    }

    private const uint Identifier = 0x10104201;
    private const uint SharedBufferSizeInKilobytes = 48;
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
        Lock = 0x2C,
        RegionConfig = 0x3C,
        AxiLimit0 = 0x40,
        AxiLimit1 = 0x44,
        AxiLimit2 = 0x48,
        AxiLimit3 = 0x4C,
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
