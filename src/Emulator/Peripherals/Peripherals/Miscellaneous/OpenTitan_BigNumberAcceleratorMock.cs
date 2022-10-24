//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_BigNumberAcceleratorMock : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_BigNumberAcceleratorMock(Machine machine) : base(machine)
        {
            dataMemory = new byte[sizeof(uint) * DataMemoryWindowsCount];
            instructionsMemory = new byte[sizeof(uint) * InstructionsMemoryWindowsCount];

            DoneIRQ = new GPIO();
            FatalAlert = new GPIO();
            RecoverableAlert = new GPIO();

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            Array.Clear(dataMemory, 0, dataMemory.Length);
            Array.Clear(instructionsMemory, 0, instructionsMemory.Length);

            // DoneIRQ is unset via the registers callbacks
            // Alerts need to be unset manually
            FatalAlert.Unset();
            RecoverableAlert.Unset();

            base.Reset();
        }

        public long Size => 0x10000;

        public GPIO DoneIRQ { get; }
        public GPIO FatalAlert { get; }
        public GPIO RecoverableAlert { get; }

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out doneInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "done")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out doneInterruptEnable, name: "done")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) doneInterruptState.Value = true; }, name: "done")
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, val) => { if(val != 0) UpdateInterrupts(); });

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverableAlert.Blink(); }, name: "recov")
                .WithReservedBits(2, 30);

            Registers.Command.Define(this)
                .WithEnumField<DoubleWordRegister, Command>(0, 8, FieldMode.Write, writeCallback: (_, command) => HandleCommand(command), name: "cmd")
                .WithReservedBits(8, 24);

            Registers.Control.Define(this)
                .WithTaggedFlag("software_errs_fatal", 0)
                .WithReservedBits(1, 31);

            Registers.Status.Define(this)
                .WithEnumField<DoubleWordRegister, Status>(0, 8, out status, FieldMode.Read, name: "status")
                .WithReservedBits(8, 24);

            Registers.OperationResult.Define(this)
                .WithTaggedFlag("bad_data_addr", 0)
                .WithTaggedFlag("bad_insn_addr", 1)
                .WithTaggedFlag("call_stack", 2)
                .WithTaggedFlag("illegal_insn", 3)
                .WithTaggedFlag("loop", 4)
                .WithTaggedFlag("key_invalid", 5)
                .WithTaggedFlag("rnd_rep_chk_fail", 6)
                .WithTaggedFlag("rnd_fips_chk_fail", 7)
                .WithTaggedFlag("imem_intg_violation", 16)
                .WithTaggedFlag("dmem_intg_violation", 17)
                .WithTaggedFlag("reg_intg_violation", 18)
                .WithTaggedFlag("bus_intg_violation", 19)
                .WithTaggedFlag("bad_internal_state", 20)
                .WithTaggedFlag("illegal_bus_access", 21)
                .WithTaggedFlag("lifecycle_escalation", 22)
                .WithTaggedFlag("fatal_software", 23)
                .WithReservedBits(24, 8);

            Registers.FatalAlertCause.Define(this)
                .WithTaggedFlag("imem_intg_violation", 0)
                .WithTaggedFlag("dmem_intg_violation", 1)
                .WithTaggedFlag("reg_intg_violation", 2)
                .WithTaggedFlag("bus_intg_violation", 3)
                .WithTaggedFlag("bad_internal_state", 4)
                .WithTaggedFlag("illegal_bus_access", 5)
                .WithTaggedFlag("lifecycle_escalation", 6)
                .WithTaggedFlag("fatal_software", 7)
                .WithReservedBits(8, 24);

            Registers.InstructionCount.Define(this)
                .WithTag("insn_cnt", 0, 32);

            Registers.A32bitCRCchecksumofdatawrittentomemory.Define(this)
                .WithTag("checksum", 0, 32);

            Registers.InstructionMemoryAccess.DefineMany(this, InstructionsMemoryWindowsCount, (register, regId) =>
            {
                register
                    .WithValueField(0, 32,
                                    valueProviderCallback: _ => Misc.ByteArrayRead(regId * sizeof(uint), instructionsMemory),
                                    writeCallback: (_, val) => Misc.ByteArrayWrite(regId * sizeof(uint), val, instructionsMemory),
                                    name: $"InstructionMemoryAccess{regId}");
            });

            Registers.DataMemoryAccess.DefineMany(this, DataMemoryWindowsCount, (register, regId) =>
            {
                register
                    .WithValueField(0, 32,
                                    valueProviderCallback: _ => Misc.ByteArrayRead(regId * sizeof(uint), dataMemory),
                                    writeCallback: (_, val) => Misc.ByteArrayWrite(regId * sizeof(uint), val, dataMemory),
                                    name: $"DataMemoryAccess{regId}");
            });
        }

        private void UpdateInterrupts()
        {
            var done = doneInterruptEnable.Value && doneInterruptState.Value;
            DoneIRQ.Set(done);
        }

        private void HandleCommand(Command command)
        {
            switch(command)
            {
                case Command.Execute:
                    status.Value = Status.BusyExecute;
                    Execute();
                    break;
                case Command.WipeDMem:
                    status.Value = Status.BusySecureWipeDMem;
                    Array.Clear(dataMemory, 0, dataMemory.Length);
                    break;
                case Command.WipeIMem:
                    status.Value = Status.BusySecureWipeIMem;
                    Array.Clear(instructionsMemory, 0, instructionsMemory.Length);
                    break;
                default:
                    this.Log(LogLevel.Error, "Unrecognized command : 0x{0:X}", command);
                    return;
            }
            doneInterruptState.Value = true;
            status.Value = Status.Idle;
            UpdateInterrupts();
        }

        private void Execute()
        {
            this.Log(LogLevel.Warning, "This peripheral is just a mock. No instructions will be executed");
        }

        private const int DataMemoryWindowsCount = 512;
        private const int InstructionsMemoryWindowsCount = 1024;

        private readonly byte[] dataMemory;
        private readonly byte[] instructionsMemory;

        private IFlagRegisterField doneInterruptEnable;
        private IFlagRegisterField doneInterruptState;
        private IEnumRegisterField<Status> status;

        public enum Command
        {
            Execute = 0xd8,
            WipeDMem = 0xc3,
            WipeIMem = 0x1e,
        }

        public enum Status
        {
            Idle = 0x0,
            BusyExecute = 0x1,
            BusySecureWipeDMem = 0x2,
            BusySecureWipeIMem = 0x3,
            BusySecureWipeInternal = 0x4,
            Locked = 0xFF,
        }

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            Command = 0x10,
            Control = 0x14,
            Status = 0x18,
            OperationResult = 0x1c,
            FatalAlertCause = 0x20,
            InstructionCount = 0x24,
            A32bitCRCchecksumofdatawrittentomemory = 0x28,
            InstructionMemoryAccess = 0x4000,
            DataMemoryAccess = 0x8000,
        }
    } // End class OpenTitan_OtbnMock
}
