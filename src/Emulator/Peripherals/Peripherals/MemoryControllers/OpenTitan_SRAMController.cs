//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class OpenTitan_SRAMController: BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_SRAMController(IMachine machine): base(machine)
        {
            FatalError = new GPIO();
            DefineRegisters();
            Reset();
        }

        public long Size => 0x20;

        public GPIO FatalError { get; }

        public void DefineRegisters()
        {
            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalError.Blink(); }, name: "FATAL_ERROR")
                .WithReservedBits(1, 31);

            Registers.Status.Define(this)
                .WithTaggedFlag("BUS_INTEG_ERROR", 0)
                .WithTaggedFlag("INIT_ERROR", 1)
                .WithTaggedFlag("ESCALATED", 2)
                .WithTaggedFlag("SCR_KEY_VALID", 3)
                .WithTaggedFlag("SCR_KEY_SEED_VALID", 4)
                .WithTaggedFlag("INIT_DONE", 5)
                .WithReservedBits(6, 26);

            Registers.ExecutionEnableWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("EXEC_REGWEN", 0)
                .WithReservedBits(1, 31);

            Registers.ExecutionEnable.Define(this, 0x9)
                .WithTag("EN", 0, 4)
                .WithReservedBits(4, 28);

            Registers.ControlWriteEnable.Define(this, 0x1)
                .WithTaggedFlag("CTRL_REGWEN", 0)
                .WithReservedBits(1, 31);

            Registers.Control.Define(this)
                .WithTaggedFlag("RENEW_SCR_KEY", 0)
                .WithTaggedFlag("INIT", 1)
                .WithReservedBits(2, 30);
        }

        public enum Registers
        {
            AlertTest = 0x0,
            Status = 0x4,
            ExecutionEnableWriteEnable = 0x8,
            ExecutionEnable = 0xc,
            ControlWriteEnable = 0x10,
            Control = 0x14,
        }
    }
}