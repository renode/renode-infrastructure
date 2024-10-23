//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Numerics;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public interface IOpenTitan_BigNumberAcceleratorCore
    {
        ExecutionResult ExecuteInstructions(int instructionCount, out ulong executedInstructions);
        void Reset();
        RegisterValue GetRegister(int id);
        BigInteger GetWideRegister(int number, bool special);
        string FixedRandomPattern { get; set; }
        string KeyShare0 { get; set; }
        string KeyShare1 { get; set; }
        CoreError LastError { get; }
    }

    public static class BigIntegerHelpers
    {
        public static byte[] ToByteArray(this BigInteger bi, int width)
        {
            var array = bi.ToByteArray();
            if(array.Length > width)
            {
                array = array.Take(width).ToArray();
            }
            else if(array.Length < width)
            {
                var fill = width - array.Length;
                var lsb = array[array.Length - 1];
                array = array.Concat(Enumerable.Repeat(lsb >= 0x80 ? (byte)0xff : (byte)0, fill)).ToArray();
            }
            return array;
        }

        public static string ToLongString(this BigInteger bi, int width)
        {
            var s = string.Join("", bi.ToByteArray(width).Reverse().Select(x => x.ToString("x2")));
            return $"0x{s}";
        }
    }

    public class OpenTitan_BigNumberAccelerator : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_BigNumberAccelerator(IMachine machine) : base(machine)
        {
            ulong instructionMemorySize = sizeof(uint) * DataMemoryWindowsCount;
            ulong dataMemorySize = sizeof(uint) * InstructionsMemoryWindowsCount;

            dataMemory = new OpenTitan_ScrambledMemory(machine, (long)dataMemorySize);
            instructionsMemory = new OpenTitan_ScrambledMemory(machine, (long)instructionMemorySize);

            DoneIRQ = new GPIO();
            FatalAlert = new GPIO();
            RecoverableAlert = new GPIO();
            core = InitOtbnCore(instructionsMemory, dataMemory);

            DefineRegisters();
            Reset();
        }

        public void LoadELF(string filePath)
        {
            using(var elf = ELFUtils.LoadELF(filePath))
            {
                if(elf.TryGetSection(".text", out var iMemSection))
                {
                    instructionsMemory.WriteBytes(0, iMemSection.GetContents());
                }
                else
                {
                    var availableSections = String.Join(", ", elf.Sections.Select(x => x.Name).ToArray());
                    throw new RecoverableException($".text section not found. Available sections: {availableSections}");
                }

                if(elf.TryGetSection(".data", out var dMemSection))
                {
                    dataMemory.WriteBytes(0, dMemSection.GetContents());
                }
                else
                {
                    var availableSections = String.Join(", ", elf.Sections.Select(x => x.Name).ToArray());
                    throw new RecoverableException($".data section not found. Available sections: {availableSections}");
                }
            }
        }

        public override void Reset()
        {
            dataMemory.ZeroAll();
            instructionsMemory.ZeroAll();
            core.Reset();

            // DoneIRQ is unset via the registers callbacks
            // Alerts need to be unset manually
            FatalAlert.Unset();
            RecoverableAlert.Unset();
            executedInstructionsTotal = 0;

            base.Reset();
        }

        public ulong GetCoreRegister(int number)
        {
            return this.core.GetRegister(number);
        }

        public string GetWideRegister(int number, bool special)
        {
            return core.GetWideRegister(number, special).ToLongString(32);
        }

        public long Size => 0x10000;

        public GPIO DoneIRQ { get; }
        public GPIO FatalAlert { get; }
        public GPIO RecoverableAlert { get; }

        public bool IsIdle => (status.Value == Status.Idle);

        public string FixedRandomPattern
        {
            get => core.FixedRandomPattern;
            set
            {
                core.FixedRandomPattern = value;
            }
        }

        public string KeyShare0
        {
            get => core.KeyShare0;
            set
            {
                core.KeyShare0 = value;
            }
        }

        public string KeyShare1
        {
            get => core.KeyShare1;
            set
            {
                core.KeyShare1 = value;
            }
        }

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
                .WithEnumField<DoubleWordRegister, Command>(0, 8, FieldMode.Write, writeCallback: (_, command) =>
                {
                    // enter the busy state now, but execute the command (and clear the busy state later)
                    // this is required by some tests
                    EnterState(command);
                    machine.LocalTimeSource.ExecuteInNearestSyncedState(__ =>
                    {
                        HandleCommand(command);
                    });
                }, name: "cmd")
                .WithReservedBits(8, 24);

            IFlagRegisterField softwareErrorsFatal = null;
            Registers.Control.Define(this)
                .WithFlag(0, out softwareErrorsFatal, name: "software_errs_fatal",
                    writeCallback: (prevVal, newVal) =>
                    {
                        var s = status.Value;
                        if(s != Status.Idle)
                        {
                            // according to the documentation: "Writes are ignored if OTBN is not idle."
                            softwareErrorsFatal.Value = prevVal;

                            this.Log(LogLevel.Warning, "Ignoring software_errs_fatal write, as the device is in {0} mode.", s);
                        }
                    })
                .WithReservedBits(1, 31);

            Registers.Status.Define(this)
                .WithEnumField<DoubleWordRegister, Status>(0, 8, out status, FieldMode.Read, name: "status")
                .WithReservedBits(8, 24);

            Registers.OperationResult.Define(this)
                .WithFlag(0, out badDataAddressError, name: "bad_data_addr")
                .WithFlag(1, out badInstructionAddressError, name: "bad_insn_addr")
                .WithFlag(2, out callStackError, name: "call_stack")
                .WithFlag(3, out illegalInstructionError, name: "illegal_insn")
                .WithFlag(4, out loopError, name: "loop")
                .WithTaggedFlag("key_invalid", 5)
                .WithTaggedFlag("rnd_rep_chk_fail", 6)
                .WithTaggedFlag("rnd_fips_chk_fail", 7)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("imem_intg_violation", 16)
                .WithTaggedFlag("dmem_intg_violation", 17)
                .WithTaggedFlag("reg_intg_violation", 18)
                .WithTaggedFlag("bus_intg_violation", 19)
                .WithTaggedFlag("bad_internal_state", 20)
                .WithTaggedFlag("illegal_bus_access", 21)
                .WithTaggedFlag("lifecycle_escalation", 22)
                .WithTaggedFlag("fatal_software", 23)
                .WithReservedBits(24, 8)
                .WithWriteCallback((_, __) =>
                {
                    // The host CPU can clear this register when OTBN is not running, by writing any value. Write attempts while OTBN is running are ignored.
                    if(!IsIdle)
                    {
                        badDataAddressError.Value = false;
                        badInstructionAddressError.Value = false;
                        callStackError.Value = false;
                        illegalInstructionError.Value = false;
                        loopError.Value = false;
                    }
                });

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
                .WithValueField(0,32, FieldMode.Read, valueProviderCallback: (_) => (uint)executedInstructionsTotal, name: "insn_cnt");

            Registers.A32bitCRCchecksumofdatawrittentomemory.Define(this)
                .WithTag("checksum", 0, 32);

            Registers.InstructionMemoryAccess.DefineMany(this, InstructionsMemoryWindowsCount, (register, regId) =>
            {
                register
                    .WithValueField(0, 32,
                                    valueProviderCallback: _ => instructionsMemory.ReadDoubleWord(regId * sizeof(uint)),
                                    writeCallback: (_, val) => instructionsMemory.WriteDoubleWord(regId * sizeof(uint), (uint)val),
                                    name: $"InstructionMemoryAccess{regId}");
            });

            Registers.DataMemoryAccess.DefineMany(this, DataMemoryWindowsCount, (register, regId) =>
            {
                register
                    .WithValueField(0, 32,
                                    valueProviderCallback: _ => dataMemory.ReadDoubleWord(regId * sizeof(uint)),
                                    writeCallback: (_, val) => dataMemory.WriteDoubleWord(regId * sizeof(uint), (uint)val),
                                    name: $"DataMemoryAccess{regId}");
            });
        }

        private void UpdateInterrupts()
        {
            var done = doneInterruptEnable.Value && doneInterruptState.Value;
            DoneIRQ.Set(done);
        }

        private void EnterState(Command command)
        {
            switch(command)
            {
                case Command.Execute:
                    status.Value = Status.BusyExecute;
                    break;
                case Command.WipeDMem:
                    status.Value = Status.BusySecureWipeDMem;
                    break;
                case Command.WipeIMem:
                    status.Value = Status.BusySecureWipeIMem;
                    break;
                default:
                    this.Log(LogLevel.Error, "Unrecognized command : 0x{0:X}", command);
                    return;
            }
        }

        private void HandleCommand(Command command)
        {
            switch(command)
            {
                case Command.Execute:
                    Execute();
                    break;
                case Command.WipeDMem:
                    dataMemory.ZeroAll();
                    break;
                case Command.WipeIMem:
                    instructionsMemory.ZeroAll();
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
            this.Log(LogLevel.Debug, "Starting execution");
            var result = ExecutionResult.Ok;
            executedInstructionsTotal = 0;

            core.Reset();
            while(result == ExecutionResult.Ok)
            {
                result = core.ExecuteInstructions(1, out var executedInstructions);
                this.Log(LogLevel.Debug, "Core executed {0} instructions and returned {1}", executedInstructions, result);

                if(result == ExecutionResult.Aborted)
                {
                    switch((CoreError)core.LastError)
                    {
                        case CoreError.BadDataAddress:
                            badDataAddressError.Value = true;
                            break;

                        case CoreError.BadInstructionAddress:
                            badInstructionAddressError.Value = true;
                            break;

                        case CoreError.CallStack:
                            callStackError.Value = true;
                            break;

                        case CoreError.IllegalInstruction:
                            illegalInstructionError.Value = true;
                            break;

                        case CoreError.Loop:
                            loopError.Value = true;
                            break;
                    }
                    break;
                }

                executedInstructionsTotal += executedInstructions;
            }

            this.Log(LogLevel.Debug, "Execution finished");
        }

        private IOpenTitan_BigNumberAcceleratorCore InitOtbnCore(OpenTitan_ScrambledMemory iMem, OpenTitan_ScrambledMemory dMem)
        {
            // As the binary with needed type is included during the runtime we must use reflection
            Type coreType = null;
            foreach(Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if(asm.FullName.StartsWith("cores-riscv"))
                {
                    coreType = asm.GetType("Antmicro.Renode.Peripherals.CPU.OpenTitan_BigNumberAcceleratorCore", false);
                    if(coreType != null)
                    {
                        break;
                    }
                }
            }

            if(coreType == null)
            {
                throw new ConstructionException($"Couldn't find the OpenTitan_BigNumberAcceleratorCore class. Check your Renode installation");
            }

            var constructor = coreType.GetConstructor(new Type[] { typeof(OpenTitan_BigNumberAccelerator), typeof(OpenTitan_ScrambledMemory), typeof(OpenTitan_ScrambledMemory) });
            return (IOpenTitan_BigNumberAcceleratorCore)constructor.Invoke(new object [] { this, iMem, dMem });
        }

        private IFlagRegisterField badDataAddressError;
        private IFlagRegisterField badInstructionAddressError;
        private IFlagRegisterField illegalInstructionError;
        private IFlagRegisterField callStackError;
        private IFlagRegisterField loopError;

        private IFlagRegisterField doneInterruptEnable;
        private IFlagRegisterField doneInterruptState;
        private IEnumRegisterField<Status> status;

        private ulong executedInstructionsTotal;
        private IOpenTitan_BigNumberAcceleratorCore core;

        private readonly OpenTitan_ScrambledMemory dataMemory;
        private readonly OpenTitan_ScrambledMemory instructionsMemory;

        private const int DataMemoryWindowsCount = 1024;
        private const int InstructionsMemoryWindowsCount = 1024;

        private enum Command
        {
            Execute = 0xd8,
            WipeDMem = 0xc3,
            WipeIMem = 0x1e,
        }

        private enum Status
        {
            Idle = 0x0,
            BusyExecute = 0x1,
            BusySecureWipeDMem = 0x2,
            BusySecureWipeIMem = 0x3,
            BusySecureWipeInternal = 0x4,
            Locked = 0xFF,
        }

        private enum Registers
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
    }

    public enum CoreError
    {
        None =                  0b00000,
        BadDataAddress =        0b00001,
        BadInstructionAddress = 0b00010,
        CallStack =             0b00100,
        IllegalInstruction =    0b01000,
        Loop =                  0b10000,
        // Other errors not implemented yet
    }
}
