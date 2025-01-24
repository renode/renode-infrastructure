//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;
using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class MSP430X : BaseCPU, IGPIOReceiver, ICpuSupportingGdb
    {
        public MSP430X(IMachine machine, string cpuType) : base(0, cpuType, machine, Endianess.LittleEndian)
        {
            // NOTE: Track all ArrayMemory instances for the direct access
            machine.PeripheralsChanged += (_, ev) =>
            {
                if(ev.Peripheral is ArrayMemory arrayMemory)
                {
                    if(ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.Removal ||
                       ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.CompleteRemoval ||
                       ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.Moved)
                    {
                        foreach(var startingPoint in arrayMemoryList.Where(keyValue => keyValue.Value == arrayMemory).Select(keyValue => keyValue.Key).ToList())
                        {
                            arrayMemoryList.Remove(startingPoint);
                        }
                    }

                    if(ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.Addition ||
                       ev.Operation == PeripheralsChangedEventArgs.PeripheralChangeType.Moved)
                    {
                        foreach(IBusRegistration registrationPoint in machine.GetPeripheralRegistrationPoints(machine.SystemBus, arrayMemory))
                        {
                            arrayMemoryList[registrationPoint.StartingPoint] = arrayMemory;
                        }
                    }
                }
            };
        }

        public override void Reset()
        {
            base.Reset();

            foreach(var register in Enum.GetValues(typeof(Registers)).Cast<Registers>())
            {
                SetRegisterValue(register, 0);
            }

            hooks.Clear();
            pendingWatchpoints.Clear();
            pendingInterrupt.Clear();
        }

        public void OnGPIO(int number, bool value)
        {
            if(value)
            {
                pendingInterrupt.Add(number);
            }
            else
            {
                pendingInterrupt.Remove(number);
            }
        }

        public void AddHookAtInterruptBegin(Action<ulong> hook)
        {
            throw new RecoverableException("This feature is not implemented yet");
        }

        public void AddHookAtInterruptEnd(Action<ulong> hook)
        {
            throw new RecoverableException("This feature is not implemented yet");
        }

        public void AddHookAtWfiStateChange(Action<bool> hook)
        {
            throw new RecoverableException("This feature is not implemented yet");
        }

        public void AddHook(ulong addr, Action<ICpuSupportingGdb, ulong> hook)
        {
            if(!hooks.ContainsKey(addr))
            {
                hooks.Add(addr, new HashSet<Action<ICpuSupportingGdb, ulong>>());
            }
            hooks[addr].Add(hook);
        }

        public void RemoveHook(ulong addr, Action<ICpuSupportingGdb, ulong> hook)
        {
            if(!hooks.ContainsKey(addr))
            {
                return;
            }

            hooks[addr].Remove(hook);
        }

        public void RemoveHooksAt(ulong addr)
        {
            if(!hooks.ContainsKey(addr))
            {
                return;
            }

            hooks[addr].Clear();
        }

        public void RemoveAllHooks()
        {
            hooks.Clear();
        }

        public void SetRegister(int register, RegisterValue value)
        {
            SetRegisterValue((Registers)register, (uint)value.RawValue);
        }

        public void SetRegisterUnsafe(int register, RegisterValue value)
        {
            SetRegister(register, value);
        }

        public RegisterValue GetRegister(int register)
        {
            return RegisterValue.Create(GetRegisterValue((Registers)register), 32);
        }

        public RegisterValue GetRegisterUnsafe(int register)
        {
            return GetRegister(register);
        }

        public IEnumerable<CPURegister> GetRegisters()
        {
            return Enumerable.Range(0, 16).Select(idx => new CPURegister(idx, 32, isGeneral: true, isReadonly: false));
        }

        public void EnterSingleStepModeSafely(HaltArguments args)
        {
            ExecutionMode = ExecutionMode.SingleStep;
        }

        public override string Architecture => "msp430x";

        public override RegisterValue PC { get; set; }

        public override ulong ExecutedInstructions => executedInstructions;

        public event Action<int> InterruptAcknowledged;

        public string GDBArchitecture => "MSP430X";
        public List<GDBFeatureDescriptor> GDBFeatures => new List<GDBFeatureDescriptor>();

        public int StackDumpLength { get; set; } = 15;

        public RegisterValue SP { get; set; }
        public RegisterValue SR
        {
            get => (uint)statusRegister;
            set => statusRegister = (StatusFlags)value.RawValue;
        }

        public RegisterValue R0 => PC;
        public RegisterValue R1 => SP;
        public RegisterValue R2 => SR;
        // NOTE: Skipping R3/CG register as it's value depends on the opcode
        public RegisterValue R4 { get; set; }
        public RegisterValue R5 { get; set; }
        public RegisterValue R6 { get; set; }
        public RegisterValue R7 { get; set; }
        public RegisterValue R8 { get; set; }
        public RegisterValue R9 { get; set; }
        public RegisterValue R10 { get; set; }
        public RegisterValue R11 { get; set; }
        public RegisterValue R12 { get; set; }
        public RegisterValue R13 { get; set; }
        public RegisterValue R14 { get; set; }
        public RegisterValue R15 { get; set; }

        public string DumpRegisters()
        {
            StringBuilder sb = new StringBuilder();
            for(var i = 0; i < 16; ++i)
            {
                if(i > 0)
                {
                    sb.Append(", ");
                }
                var val = GetRegisterValue((Registers)i);
                sb.AppendFormat("{0}=0x{1:X05}", Enum.GetName(typeof(Registers), i), val);
            }
            return sb.ToString();
        }

        public string[] DumpStack(int length)
        {
            for(var i = 0; i < length; ++i)
            {
                this.Log(LogLevel.Debug, "0x{0:X05}: {1:X04}", SP + 2 * i, machine.SystemBus.ReadWord(SP + 2 * (ulong)i));
            }

            return Enumerable
                .Range(0, length)
                .Select(i => "0x{0:X05}: {1:X04}".FormatWith(SP + 2 * i, machine.SystemBus.ReadWord(SP + 2 * (ulong)i)))
                .ToArray()
            ;
        }

        public override ExecutionResult ExecuteInstructions(ulong numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            numberOfExecutedInstructions = 0;
            while(numberOfInstructionsToExecute-- > 0)
            {
                var result = EvaluateNextOpcode();
                if(result == ExecutionResult.Aborted)
                {
                    this.Log(LogLevel.Error, "Execution aborted");
                    this.Log(LogLevel.Debug, "Register dump: {0}", DumpRegisters());
                    this.Log(LogLevel.Debug, "Stack dump (last {0} words)", StackDumpLength);
                    foreach(var dumpLine in DumpStack(StackDumpLength))
                    {
                        this.Log(LogLevel.Debug, dumpLine);
                    }

                    return ExecutionResult.Aborted;
                }

                numberOfExecutedInstructions++;
                executedInstructions++;

                if(statusRegister.HasFlag(StatusFlags.GeneralInterruptEnable) && pendingInterrupt.Count > 0)
                {
                    HandleInterrupt(pendingInterrupt.Min);
                }

                if(TryHandleWatchpoints())
                {
                    pendingWatchpoints.Clear();
                    return ExecutionResult.StoppedAtWatchpoint;
                }

                if(hooks.ContainsKey(PC.RawValue))
                {
                    return ExecutionResult.StoppedAtBreakpoint;
                }
            }

            return ExecutionResult.Ok;
        }

        protected override bool ExecutionFinished(ExecutionResult result)
        {
            if(result == ExecutionResult.StoppedAtBreakpoint)
            {
                this.Log(LogLevel.Noisy, "Executing hooks @ {0}", PC);
                foreach(var hook in hooks[PC.RawValue])
                {
                    hook(this, PC.RawValue);
                }
                return true;
            }

            return false;
        }

        private bool TryHandleWatchpoints()
        {
            foreach(var watchpoint in pendingWatchpoints)
            {
                machine.SystemBus.TryGetWatchpointsAt(watchpoint.Address, watchpoint.Value.HasValue ? Access.Write : Access.Read, out var watchpoints);
                foreach(var hook in watchpoints)
                {
                    hook.Invoke(this, watchpoint.Address, watchpoint.SysbusAccessWidth, watchpoint.Value ?? 0);
                }
            }

            return pendingWatchpoints.Count > 0;
        }

        private void SetStatusFlag(StatusFlags flag, bool set)
        {
            statusRegister = set ? statusRegister | flag : statusRegister & ~flag;
        }

        private uint GetRegisterValue(Registers register, AddressingMode addressingMode = AddressingMode.Register)
        {
            switch(register)
            {
                case Registers.PC:
                    return PC;
                case Registers.SP:
                    return SP;
                case Registers.SR:
                    switch(addressingMode)
                    {
                        case AddressingMode.Register:
                            return SR;

                        case AddressingMode.Indexed:
                            return 0;

                        case AddressingMode.IndirectRegister:
                            return 0x0004;

                        case AddressingMode.IndirectAutoincrement:
                            return 0x0008;

                        default:
                            throw new Exception("unreachable");
                    }
                case Registers.R3:
                    switch(addressingMode)
                    {
                        case AddressingMode.Register:
                            return 0;

                        case AddressingMode.Indexed:
                            return 0x0001;

                        case AddressingMode.IndirectRegister:
                            return 0x0002;

                        case AddressingMode.IndirectAutoincrement:
                            return 0xFFFFF;

                        default:
                            throw new Exception("unreachable");
                    }
                case Registers.R4:
                    return R4;
                case Registers.R5:
                    return R5;
                case Registers.R6:
                    return R6;
                case Registers.R7:
                    return R7;
                case Registers.R8:
                    return R8;
                case Registers.R9:
                    return R9;
                case Registers.R10:
                    return R10;
                case Registers.R11:
                    return R11;
                case Registers.R12:
                    return R12;
                case Registers.R13:
                    return R13;
                case Registers.R14:
                    return R14;
                case Registers.R15:
                    return R15;
                default:
                    throw new Exception($"{register} is not a valid register");
            }
        }

        private void SetRegisterValue(Registers register, uint value)
        {
            this.Log(LogLevel.Debug, "{0}: 0x{1:X05} -> 0x{2:X05}", register, GetRegisterValue(register), value);
            switch(register)
            {
                case Registers.PC:
                    PC = value;
                    break;
                case Registers.SP:
                    SP = value;
                    break;
                case Registers.SR:
                    SR = value;
                    break;
                case Registers.R3:
                    // NOTE: Write to this register does nothing
                    // NOTE: Compiler uses it to emulate NOP
                    break;
                case Registers.R4:
                    R4 = value;
                    break;
                case Registers.R5:
                    R5 = value;
                    break;
                case Registers.R6:
                    R6 = value;
                    break;
                case Registers.R7:
                    R7 = value;
                    break;
                case Registers.R8:
                    R8 = value;
                    break;
                case Registers.R9:
                    R9 = value;
                    break;
                case Registers.R10:
                    R10 = value;
                    break;
                case Registers.R11:
                    R11 = value;
                    break;
                case Registers.R12:
                    R12 = value;
                    break;
                case Registers.R13:
                    R13 = value;
                    break;
                case Registers.R14:
                    R14 = value;
                    break;
                case Registers.R15:
                    R15 = value;
                    break;
                default:
                    throw new Exception($"{register} is not a valid register");
            }
        }

        private void HandleInterrupt(int interruptNumber)
        {
            var interruptVector = InterruptVectorStart - (ulong)interruptNumber * 2U;
            var interruptAddress = (ushort)PerformMemoryRead(interruptVector, AccessWidth._16bit);

            var statusAndPC = ((PC & 0xF0000U) >> 8) | SR;

            SP -= 2U;
            PerformMemoryWrite(SP, PC, AccessWidth._16bit);
            SP -= 2U;
            PerformMemoryWrite(SP, statusAndPC, AccessWidth._16bit);

            statusRegister &= StatusFlags.SystemClockGenerator0;
            PC = interruptAddress;

            InterruptAcknowledged?.Invoke(interruptNumber);
        }

        private uint GetOperandValue(Registers register, AddressingMode addressingMode, out ulong address, AccessWidth accessWidth = AccessWidth._16bit, uint addressExtension = 0)
        {
            address = 0UL;

            // NOTE: Handle CG1 generator
            if(register == Registers.SR)
            {
                switch(addressingMode)
                {
                    case AddressingMode.IndirectRegister:
                        return 0x00004;

                    case AddressingMode.IndirectAutoincrement:
                        return 0x00008;

                    default:
                        break;
                }
            }

            // NOTE: Handle CG2 generator
            if(register == Registers.R3)
            {
                switch(addressingMode)
                {
                    case AddressingMode.Register:
                        return 0x00000;

                    case AddressingMode.Indexed:
                        return 0x00001;

                    case AddressingMode.IndirectRegister:
                        return 0x00002;

                    case AddressingMode.IndirectAutoincrement:
                        return 0xFFFFF;

                    default:
                        throw new Exception("unreachable");
                }
            }

            switch(addressingMode)
            {
                case AddressingMode.Register:
                    return GetRegisterValue(register);

                case AddressingMode.Indexed:
                {
                    var registerValue = GetRegisterValue(register, addressingMode);
                    var index = (short)PerformMemoryRead(PC, AccessWidth._16bit);
                    PC += 2U;

                    var memoryAddress = (uint)(registerValue + index);
                    if(registerValue < 64.KB())
                    {
                        // NOTE: If register value points to lower 64KB, we should truncate the address
                        memoryAddress &= 0xFFFF;
                    }
                    memoryAddress |= addressExtension;

                    address = (ulong)memoryAddress;
                    return PerformMemoryRead(address, accessWidth);
                }

                case AddressingMode.IndirectRegister:
                {
                    uint registerValue = GetRegisterValue(register, addressingMode);
                    registerValue |= addressExtension;
                    address = (ulong)registerValue;
                    return PerformMemoryRead(address, accessWidth);
                }

                case AddressingMode.IndirectAutoincrement:
                {
                    uint registerValue = GetRegisterValue(register, addressingMode);
                    var offset = (accessWidth != AccessWidth._8bit) || register == Registers.PC ? 2U : 1U;
                    address = (ulong)registerValue | addressExtension;
                    address &= 0xFFFFF;
                    SetRegisterValue(register, (uint)(registerValue + offset) & 0xFFFFF);
                    return PerformMemoryRead(address, accessWidth);
                }

                default:
                    throw new Exception("unreachable");
            }
        }

        private ExecutionResult EvaluateOpcodeJump(ushort instr)
        {
            // NOTE: Jump instructions
            var opcodeCondition = (instr & 0x1C00) >> 10;

            // NOTE: Offset is sign extended
            var offset = (uint)(instr & 0x3FF);
            offset |= (offset & 0x200) > 0 ? offset | 0xFFFFFC00 : 0;
            var offsetSigned = (int)offset * 2;
            var shouldJump = false;

            switch(opcodeCondition)
            {
                case 0x00:
                    // NOTE: JNE, JNZ
                    shouldJump = !statusRegister.HasFlag(StatusFlags.Zero);
                    break;

                case 0x01:
                    // NOTE: JEQ, JZ
                    shouldJump = statusRegister.HasFlag(StatusFlags.Zero);
                    break;

                case 0x02:
                    // NOTE: JNC
                    shouldJump = !statusRegister.HasFlag(StatusFlags.Carry);
                    break;

                case 0x03:
                    // NOTE: JC
                    shouldJump = statusRegister.HasFlag(StatusFlags.Carry);
                    break;

                case 0x04:
                    // NOTE: JN
                    shouldJump = statusRegister.HasFlag(StatusFlags.Negative);
                    break;

                case 0x05:
                    // NOTE: JGE
                    // NOTE: Negative ^ Overflow == False
                    shouldJump = !(statusRegister.HasFlag(StatusFlags.Negative) ^ statusRegister.HasFlag(StatusFlags.Overflow));
                    break;

                case 0x06:
                    // NOTE: JL
                    // NOTE: Negative ^ Overflow == True
                    shouldJump = statusRegister.HasFlag(StatusFlags.Negative) ^ statusRegister.HasFlag(StatusFlags.Overflow);
                    break;

                case 0x07:
                    // NOTE: JMP
                    // NOTE: Jump unconditionally
                    shouldJump = true;
                    break;

                default:
                    return ExecutionResult.Aborted;
            }

            if(shouldJump)
            {
                PC = (uint)((long)PC + (long)offsetSigned);
            }
            return ExecutionResult.Ok;
        }

        private bool TryEvaluateSingleOperand(ushort instr, int destination, AccessWidth accessWidth, AddressingMode addressingMode, out ExecutionResult executionResult, uint addressExtension = 0, int repetition = 1, bool resetCarry = false)
        {
            executionResult = ExecutionResult.Aborted;

            if((instr & 0xFC00) >= 0x2000)
            {
                return false;
            }

            for(; repetition > 0; --repetition)
            {
                if(resetCarry)
                {
                    SetStatusFlag(StatusFlags.Carry, false);
                }

                var opc = instr & 0xFC00;
                if(opc < 0x1000)
                {
                    var funcIdentifier = (instr & 0x00F0) >> 4;
                    // NOTE: First eight instructions implements different variants of MOV.A, and bit-shift functions
                    // Second half implements CMP.A, ADD.A, SUB.A and MOV.A

                    switch(funcIdentifier)
                    {
                        case 0:
                        {
                            // NOTE: MOVA @Rsrc
                            var sourceRegister = (Registers)((instr & 0x0F00) >> 8);
                            var memoryAddress = GetRegisterValue(sourceRegister, AddressingMode.Register);
                            var memoryValue = PerformMemoryRead(memoryAddress, AccessWidth._20bit);

                            SetRegisterValue((Registers)destination, memoryValue);
                            continue;
                        }
                        case 1:
                        {
                            // NOTE: MOVA @Rsrc+
                            var sourceRegister = (Registers)((instr & 0x0F00) >> 8);
                            var memoryAddress = GetRegisterValue(sourceRegister, AddressingMode.Register);
                            SetRegisterValue(sourceRegister, memoryAddress + 4);

                            var memoryValue = PerformMemoryRead(memoryAddress, AccessWidth._20bit);
                            SetRegisterValue((Registers)destination, memoryValue);
                            continue;
                        }
                        case 2:
                        {
                            // NOTE: MOVA &abs20
                            var absoluteAddress = (uint)GetOperandValue(Registers.PC, AddressingMode.IndirectAutoincrement, out _);
                            absoluteAddress |= (uint)(instr & 0x0F00) << 8;

                            var memoryValue = PerformMemoryRead(absoluteAddress, AccessWidth._20bit);
                            SetRegisterValue((Registers)destination, memoryValue);
                            continue;
                        }
                        case 3:
                        {
                            // NOTE: MOVA z16(Rsrc)
                            var sourceRegister = (Registers)((instr & 0x0F00) >> 8);
                            var offset = (short)GetOperandValue(Registers.PC, AddressingMode.IndirectAutoincrement, out _);
                            var memoryAddress = (ulong)(GetRegisterValue(sourceRegister) + offset);

                            var memoryValue = PerformMemoryRead(memoryAddress, AccessWidth._20bit);
                            SetRegisterValue((Registers)destination, memoryValue);
                            continue;
                        }
                        case 4:
                        case 5:
                        {
                            // NOTE: RRCM/RRAM/RLAM/RRUM
                            var instructionWidth = (instr & 0x0010) > 0 ? AccessWidth._16bit : AccessWidth._20bit;
                            var bitLocation = ((instr & 0x0C00) >> 10) + 1;
                            var func = (instr & 0x0300) >> 8;
                            var registerValue = GetRegisterValue((Registers)destination);
                            var width = GetAccessWidthInBits(instructionWidth);

                            switch(func)
                            {
                                case 0:
                                {
                                    // NOTE: RRCM
                                    var shouldCarry = (registerValue & (1 << (bitLocation - 1))) > 0;

                                    registerValue >>= bitLocation;
                                    registerValue |= statusRegister.HasFlag(StatusFlags.Carry) ? (1U << (width - bitLocation)) : 0U;

                                    SetStatusFlag(StatusFlags.Carry, shouldCarry);
                                    TruncateWithFlags((uint)registerValue, accessWidth);
                                    SetRegisterValue((Registers)destination, registerValue);
                                    break;
                                }
                                case 1:
                                {
                                    // NOTE: RRAM
                                    var signExtension = ((1U << (bitLocation + 1)) - 1) << (width - bitLocation);
                                    signExtension = (registerValue & GetAccessWidthMSB(instructionWidth)) > 0 ? signExtension : 0;

                                    var shouldCarry = (registerValue & (1 << (bitLocation - 1))) > 0;
                                    registerValue >>= bitLocation;
                                    registerValue |= signExtension;
                                    TruncateWithFlags(registerValue);
                                    SetStatusFlag(StatusFlags.Carry, shouldCarry);
                                    SetRegisterValue((Registers)destination, registerValue);
                                    break;
                                }
                                case 2:
                                {
                                    // NOTE: RLAM
                                    var shouldCarry = (registerValue & (1 << (width - bitLocation))) > 0;
                                    registerValue <<= bitLocation;
                                    TruncateWithFlags(registerValue);
                                    SetStatusFlag(StatusFlags.Carry, shouldCarry);
                                    SetRegisterValue((Registers)destination, registerValue);
                                    break;
                                }
                                case 3:
                                {
                                    // NOTE: RRUM
                                    var shouldCarry = (registerValue & (1 << (bitLocation - 1))) > 0;
                                    registerValue >>= bitLocation;
                                    TruncateWithFlags(registerValue);
                                    SetStatusFlag(StatusFlags.Carry, shouldCarry);
                                    SetRegisterValue((Registers)destination, registerValue);
                                    break;
                                }
                            }

                            continue;
                        }
                        case 6:
                        {
                            // NOTE: MOVA @Rsrc, &abs20
                            var sourceRegister = (Registers)((instr & 0x0F00) >> 8);
                            var value = GetRegisterValue(sourceRegister, AddressingMode.Register);
                            var offset = (short)GetOperandValue(Registers.PC, AddressingMode.IndirectAutoincrement, out _);
                            var memoryAddress = (ulong)(destination + offset);
                            PerformMemoryWrite(memoryAddress, value, AccessWidth._20bit);
                            continue;
                        }
                        case 7:
                        {
                            // NOTE: MOVA @Rsrc, z16(Rdst)
                            var sourceRegister = (Registers)((instr & 0x0F00) >> 8);
                            var value = GetRegisterValue(sourceRegister, AddressingMode.Register);
                            var offset = (short)GetOperandValue(Registers.PC, AddressingMode.IndirectAutoincrement, out _);
                            var memoryAddress = (ulong)(GetRegisterValue((Registers)destination) + offset);
                            PerformMemoryWrite(memoryAddress, value, AccessWidth._20bit);
                            continue;
                        }
                }

                // NOTE: Third bit defines addressing mode
                var immediateValue = ((funcIdentifier & 0x4) >> 2) == 0;

                uint sourceValue;
                if(immediateValue)
                {
                    sourceValue = GetOperandValue(Registers.PC, AddressingMode.IndirectAutoincrement, out _);
                    sourceValue |= (uint)(instr & 0x0F00) << 8;
                }
                else
                {
                    var sourceRegister = (Registers)((instr & 0x0F00) >> 8);
                    sourceValue = GetRegisterValue(sourceRegister);
                }

                switch(funcIdentifier & 0x3)
                {
                        case 0:
                        {
                            // NOTE: MOVA #imm20
                            // NOTE: MOVA @Rsrc
                            SetRegisterValue((Registers)destination, sourceValue);
                            break;
                        }
                        case 1:
                        {
                            // NOTE: CMPA #imm20
                            // NOTE: CMPA @Rsrc
                            var destinationValue = GetRegisterValue((Registers)destination);

                            var cmpTemp = (sourceValue ^ 0xFFFFF) & 0xFFFFF;
                            cmpTemp = cmpTemp + destinationValue + 1;
                            cmpTemp = TruncateWithFlags(cmpTemp, AccessWidth._20bit);
                            CheckForOverflow(sourceValue, cmpTemp, destinationValue, AccessWidth._20bit);
                            break;
                        }
                        case 2:
                        {
                            // NOTE: ADDA #imm20
                            // NOTE: ADDA @Rsrc
                            var destinationValue = GetRegisterValue((Registers)destination);

                            var calculatedValue = sourceValue + destinationValue;
                            calculatedValue = TruncateWithFlags(calculatedValue, AccessWidth._20bit);
                            CheckForOverflow(sourceValue, destinationValue, calculatedValue, AccessWidth._20bit);
                            SetRegisterValue((Registers)destination, calculatedValue);
                            break;
                        }
                        case 3:
                        {
                            // NOTE: SUBA #imm20
                            // NOTE: SUBA @Rsrc
                            var destinationValue = GetRegisterValue((Registers)destination);

                            sourceValue = (sourceValue ^ 0xFFFFF) & 0xFFFFF;
                            sourceValue += 1;

                            var calculatedValue = sourceValue + destinationValue;
                            calculatedValue = TruncateWithFlags(calculatedValue, AccessWidth._20bit);
                            CheckForOverflow(sourceValue, destinationValue, calculatedValue, AccessWidth._20bit);
                            SetRegisterValue((Registers)destination, calculatedValue);
                            break;
                        }
                        default:
                            return true;
                    }

                    continue;
                }
                else if(opc < 0x1400)
                {
                    // NOTE: Single operand instructions
                    var fullOpcode = (instr & 0x0380) >> 7;

                    // NOTE: CALLA (20-bit), handle this separately
                    switch(fullOpcode)
                    {
                        case 0x06:
                        {
                            uint newPC;
                            // NOTE: RETI / CALLA
                            if((Registers)destination == Registers.PC)
                            {
                                // NOTE: RETI
                                var statusAndPC = PerformMemoryRead(SP, AccessWidth._16bit);
                                statusRegister = (StatusFlags)(statusAndPC & 0x1FF);
                                SP += 2U;

                                newPC = PerformMemoryRead(SP, AccessWidth._16bit);
                                SP += 2U;
                                newPC |= (ushort)((statusAndPC & 0xF000) << 4);
                                PC = newPC;
                            }
                            else
                            {
                                // NOTE: CALLA
                                // NOTE: Decrement SP before reading the address
                                SP -= 2;
                                newPC = GetOperandValue((Registers)destination, addressingMode, out  _, accessWidth: AccessWidth._20bit, addressExtension: addressExtension);

                                SP -= 2;
                                PerformMemoryWrite(SP, PC, AccessWidth._20bit);
                                PC = (uint)newPC;
                            }
                            continue;
                        }

                        case 0x07:
                        {
                            // NOTE: `register` contains part of the address
                            var fullAddress = (uint)destination << 16;
                            SP -= 2U;
                            var imm = GetOperandValue(Registers.PC, AddressingMode.IndirectAutoincrement, out var _);
                            fullAddress |= imm;

                            SP -= 2U;
                            PerformMemoryWrite(SP, PC, AccessWidth._20bit);

                            switch(addressingMode)
                            {
                                case AddressingMode.Register:
                                    // NOTE: Absolute addressing
                                    fullAddress = PerformMemoryRead(fullAddress, AccessWidth._20bit);
                                    PC = (uint)fullAddress;
                                    break;

                                case AddressingMode.Indexed:
                                    // TODO: Indexed addressing
                                    this.Log(LogLevel.Error, "CALLA indexed addressing is not supported");
                                    return true;

                                case AddressingMode.IndirectAutoincrement:
                                    // NOTE: Immediate addressing
                                    PC = (uint)fullAddress;
                                    break;

                                default:
                                    return true;
                            }

                            continue;
                        }

                    }

                    switch(fullOpcode)
                    {
                        case 0x04: // NOTE: PUSH
                        case 0x05: // NOTE: CALL
                            // NOTE: PUSH and CALL decrement stack pointer before operand evaluation
                            SP -= 2U;
                            break;

                        default:
                            // NOTE: Do nothing
                            break;
                    }

                    var operand = GetOperandValue((Registers)destination, addressingMode, out var address, accessWidth: accessWidth, addressExtension: addressExtension);
                    switch(fullOpcode)
                    {
                        case 0x00:
                        {
                            // NOTE: RRC
                            var msb = statusRegister.HasFlag(StatusFlags.Carry) ? GetAccessWidthMSB(accessWidth) : 0;
                            SetStatusFlag(StatusFlags.Carry, (operand & 0x1) > 0);
                            operand = (operand >> 1) | msb;
                            TruncateWithFlags((uint)operand, accessWidth);
                            break;
                        }

                        case 0x01:
                            // NOTE: SWPB
                            // NOTE: Status bits are not affected
                            operand = ((operand >> 8) | (operand << 8)) & 0xFFFF;
                            operand &= GetAccessWidthMask(accessWidth);
                            break;

                        case 0x02:
                        {
                            // NOTE: RRA
                            var msb = GetAccessWidthMSB(accessWidth);
                            msb = (uint)(operand & msb);
                            SetStatusFlag(StatusFlags.Carry, (operand & 0x1) > 0);
                            operand = msb | (operand >> 1);
                            TruncateWithFlags((uint)operand, accessWidth);
                            break;
                        }

                        case 0x03:
                        {
                            // NOTE: SXT
                            var msb = GetAccessWidthMSB(accessWidth);
                            msb = (uint)(operand & msb);
                            operand |= msb > 0 ? 0xFFFF00U : 0U;
                            TruncateWithFlags((uint)operand, accessWidth);
                            SetStatusFlag(StatusFlags.Carry, !statusRegister.HasFlag(StatusFlags.Zero));
                            break;
                        }

                        case 0x04:
                            // NOTE: PUSH
                            PerformMemoryWrite(SP, operand, accessWidth);
                            continue;

                        case 0x05:
                            // NOTE: CALL
                            PerformMemoryWrite(SP, PC, AccessWidth._16bit);
                            PC = (uint)operand;
                            continue;

                        default:
                            return true;
                    }

                    if(addressingMode == AddressingMode.Register)
                    {
                        SetRegisterValue((Registers)destination, (uint)operand);
                    }
                    else
                    {
                        PerformMemoryWrite(address, operand, accessWidth);
                    }
                }
                else if(opc < 0x1800)
                {
                    // NOTE: MSP430X stack instructions
                    var n = 1 + ((instr & 0x00F0) >> 4);

                    switch((instr & 0x0300) >> 8)
                    {
                        case 0:
                            // NOTE: PUSHM.A
                            // NOTE: Check if the instruction is correct, otherwise abort CPU
                            if(destination < n - 1)
                            {
                                this.Log(LogLevel.Error, "Tried to push {0} registers, starting from {1} which is illegal; compilator bug?", n, (Registers)destination);
                                executionResult = ExecutionResult.Aborted;
                                return true;
                            }

                            for(var reg = destination; n != 0; n--, reg--)
                            {
                                var registerValue = GetRegisterValue((Registers)reg, AddressingMode.Register);
                                SP -= 2U;
                                PerformMemoryWrite(SP, (ushort)(registerValue >> 16), AccessWidth._16bit);
                                SP -= 2U;
                                PerformMemoryWrite(SP, (ushort)registerValue, AccessWidth._16bit);
                            }
                            break;
                        case 1:
                            // NOTE: PUSHM.W
                            // NOTE: Check if the instruction is correct, otherwise abort CPU
                            if(destination < n - 1)
                            {
                                this.Log(LogLevel.Error, "Tried to push {0} registers, starting from {1} which is illegal; compilator bug?", n, (Registers)destination);
                                executionResult = ExecutionResult.Aborted;
                                return true;
                            }

                            for(var reg = destination; n != 0; n--, reg--)
                            {
                                var registerValue = GetRegisterValue((Registers)reg, AddressingMode.Register);
                                SP -= 2U;
                                PerformMemoryWrite(SP, (ushort)registerValue, AccessWidth._16bit);
                            }
                            break;
                        case 2:
                            // NOTE: POPM.A
                            // NOTE: Check if the instruction is correct, otherwise abort CPU
                            if(destination + n - 1 > 16)
                            {
                                this.Log(LogLevel.Error, "Tried to pop {0} registers, starting from {1} which is illegal; compilator bug?", n, (Registers)destination);
                                executionResult = ExecutionResult.Aborted;
                                return true;
                            }

                            for(var reg = destination; n != 0; n--, reg++)
                            {
                                var registerValue = PerformMemoryRead(SP, AccessWidth._16bit);
                                SP += 2U;
                                registerValue |= PerformMemoryRead(SP, AccessWidth._16bit) << 16;
                                SP += 2U;
                                SetRegisterValue((Registers)reg, registerValue);
                            }
                            break;
                        case 3:
                            // NOTE: POPM.W
                            // NOTE: Check if the instruction is correct, otherwise abort CPU
                            if(destination + n - 1 > 16)
                            {
                                this.Log(LogLevel.Error, "Tried to pop {0} registers, starting from {1} which is illegal; compilator bug?", n, (Registers)destination);
                                executionResult = ExecutionResult.Aborted;
                                return true;
                            }

                            for(var reg = destination; n != 0; n--, reg++)
                            {
                                var registerValue = PerformMemoryRead(SP, AccessWidth._16bit);
                                SP += 2U;
                                SetRegisterValue((Registers)reg, registerValue);
                                this.Log(LogLevel.Noisy, "POPM.W={0:X}", registerValue);
                            }
                            break;
                    }
                }
                else if(opc < 0x2000)
                {
                    // NOTE: Extension words
                    executionResult = EvaluateNextOpcode(extensionWord: instr);
                    return true;
                }
            }

            executionResult = ExecutionResult.Ok;
            return true;
        }

        private bool TryEvaluateDoubleOperand(uint instr, int destination, AccessWidth accessWidth, AddressingMode addressingMode, out ExecutionResult executionResult, uint destinationExtension = 0, uint sourceExtension = 0, int repetition = 1, bool resetCarry = false)
        {
            var opcode = (instr & 0xF000) >> 12;
            var source = (instr & 0x0F00) >> 8;

            executionResult = ExecutionResult.Aborted;

            for(; repetition > 0; --repetition)
            {
                if(resetCarry)
                {
                    SetStatusFlag(StatusFlags.Carry, false);
                }

                var operand1 = GetOperandValue((Registers)source, addressingMode, out var sourceAddress, accessWidth: accessWidth, addressExtension: sourceExtension);

                var destinationAddressing = (instr >> 7) & 0x1;
                var destinationAddress = 0UL;
                var operand2 = destinationAddressing == 1 ?
                    GetOperandValue((Registers)destination, AddressingMode.Indexed, out destinationAddress, accessWidth: accessWidth, addressExtension: destinationExtension) :
                    GetOperandValue((Registers)destination, AddressingMode.Register, out var _)
                ;

                var temporaryValue = 0U;

                this.Log(LogLevel.Debug, "Operand1=0x{0:X} Operand2=0x{1:X} AddressingMode={2}", operand1, operand2, addressingMode);
                this.Log(LogLevel.Debug, "Operand1 address=0x{0:X} extension=0x{1:X} Operand2 address=0x{2:X} extension=0x{3:X}", sourceAddress, sourceExtension, destinationAddress, destinationExtension);

                switch(opcode)
                {
                    case 0x4:
                        // NOTE: MOV, MOV.B
                        operand1 &= GetAccessWidthMask(accessWidth);
                        break;

                    case 0x5:
                        // NOTE: ADD, ADD.B
                        temporaryValue = operand1 + operand2;
                        temporaryValue = TruncateWithFlags(temporaryValue, accessWidth);
                        CheckForOverflow(operand1, operand2, temporaryValue, accessWidth);
                        operand1 = temporaryValue; // XXX: Just use this variable instead of operand1
                        break;

                    case 0x6:
                        // NOTE: ADDC, ADDC.B
                        temporaryValue = operand1 + operand2 + (statusRegister.HasFlag(StatusFlags.Carry) ? 1U : 0U);
                        temporaryValue = TruncateWithFlags(temporaryValue, accessWidth);
                        CheckForOverflow(operand1, operand2, temporaryValue, accessWidth);
                        operand1 = temporaryValue;
                        break;

                    case 0x7:
                        // NOTE: SUBC, SUBC.B
                        operand1 = (operand1 ^ GetAccessWidthMask(accessWidth)) & GetAccessWidthMask(accessWidth);
                        operand1 += statusRegister.HasFlag(StatusFlags.Carry) ? 1U : 0U;
                        temporaryValue = operand1 + operand2;
                        temporaryValue = TruncateWithFlags(temporaryValue, accessWidth);
                        CheckForOverflow(operand1, operand2, temporaryValue, accessWidth);
                        operand1 = temporaryValue;
                        break;

                    case 0x8:
                        // NOTE: SUB, SUB.B
                        operand1 = (operand1 ^ GetAccessWidthMask(accessWidth)) & GetAccessWidthMask(accessWidth);
                        operand1 += 1;
                        temporaryValue = operand1 + operand2;
                        temporaryValue = TruncateWithFlags(temporaryValue, accessWidth);
                        CheckForOverflow(operand1, operand2, temporaryValue, accessWidth);
                        operand1 = temporaryValue;
                        break;

                    case 0x9:
                        // NOTE: CMP, CMP.B
                        operand1 = (operand1 ^ GetAccessWidthMask(accessWidth)) & GetAccessWidthMask(accessWidth);
                        operand1 += 1;
                        var cmpTemp = operand1 + operand2;
                        cmpTemp = TruncateWithFlags((uint)cmpTemp, accessWidth);
                        CheckForOverflow(operand1, operand2, cmpTemp, accessWidth);
                        continue;

                    case 0xA:
                        // NOTE: DADD, DADD.B

                        // NOTE: Convert operands to binary
                        operand1 = BCDToBinary(operand1 & GetAccessWidthMask(accessWidth));
                        operand2 = BCDToBinary(operand2 & GetAccessWidthMask(accessWidth));

                        // NOTE: Add and convert back to BCD
                        operand1 = operand1 + operand2 + (statusRegister.HasFlag(StatusFlags.Carry) ? 1U : 0U);

                        var maximumWidth = accessWidth == AccessWidth._20bit ? 99999 : (accessWidth == AccessWidth._16bit ? 9999 : 99);
                        SetStatusFlag(StatusFlags.Carry, operand1 > maximumWidth);
                        SetStatusFlag(StatusFlags.Zero, operand1 == 0);

                        operand1 = BinaryToBCD(operand1);
                        SetStatusFlag(StatusFlags.Overflow, (operand1 & GetAccessWidthMask(accessWidth)) > 0);

                        break;

                    case 0xB:
                        // NOTE: BIT, BIT.B
                        var bitTemp = operand1 & operand2;
                        TruncateWithFlags((uint)bitTemp, accessWidth);
                        SetStatusFlag(StatusFlags.Carry, !statusRegister.HasFlag(StatusFlags.Zero));
                        continue;

                    case 0xC:
                        // NOTE: BIC, BIC.B
                        operand1 = ~operand1 & operand2;
                        operand1 &= GetAccessWidthMask(accessWidth);
                        break;

                    case 0xD:
                        // NOTE: BIS, BIS.B
                        operand1 |= operand2;
                        operand1 &= GetAccessWidthMask(accessWidth);
                        break;

                    case 0xE:
                        // NOTE: XOR, XOR.B
                        operand1 ^= operand2;

                        operand1 = TruncateWithFlags((uint)operand1, accessWidth);
                        SetStatusFlag(StatusFlags.Carry, !statusRegister.HasFlag(StatusFlags.Zero));
                        break;

                    case 0xF:
                        // NOTE: AND, AND.B
                        operand1 &= operand2;

                        operand1 = TruncateWithFlags((uint)operand1, accessWidth);
                        SetStatusFlag(StatusFlags.Overflow, false);
                        SetStatusFlag(StatusFlags.Carry, !statusRegister.HasFlag(StatusFlags.Zero));
                        break;

                    default:
                        // NOTE: Now we have handled all possible instructions, throw error if we are here
                        this.Log(LogLevel.Error, "Unhandled instruction: 0x{0:X04}", instr);
                        return false;
                }

                if(destinationAddressing == 0)
                {
                    SetRegisterValue((Registers)destination, (uint)operand1);
                }
                else
                {
                    PerformMemoryWrite(destinationAddress, operand1, accessWidth);
                }
            }

            executionResult = ExecutionResult.Ok;
            return true;
        }

        private ExecutionResult EvaluateNextOpcode(ushort extensionWord = 0)
        {
            var instr = (ushort)PerformMemoryRead((uint)PC, AccessWidth._16bit);
            this.Log(LogLevel.Debug, "{0}: 0x{1:X04} @ {2}", PC, instr, ExecutedInstructions);
            PC += 2U;

            // NOTE: Jump instruction start with either 2XXXh or 3XXXh
            var opcode = (instr & 0xF000) >> 12;
            if(opcode == 0x2 || opcode == 0x3)
            {
                return EvaluateOpcodeJump(instr);
            }

            var accessWidth = ((instr >> 6) & 0x1) > 0 ? AccessWidth._8bit : AccessWidth._16bit;
            var sourceAddressing = (instr >> 4) & 0x3;
            var addressingMode = (AddressingMode)sourceAddressing;
            var destination = instr & 0x000F;
            var repetition = 1;

            var sourceExtension = 0U;
            var destinationExtension = 0U;
            var resetCarry = false;

            if(extensionWord != 0)
            {
                this.Log(LogLevel.Noisy, "Current extensions word 0x{0:X}", extensionWord);
                var extendedAccess = ((extensionWord >> 6) & 0x1) == 0;
                if(extendedAccess && accessWidth == AccessWidth._16bit)
                {
                    this.Log(LogLevel.Warning, "Current instruction has invalid access width configuration (both `A/L` and `B/W` are unset); bug in compilator?");
                    return ExecutionResult.Aborted;
                }

                accessWidth = extendedAccess ? AccessWidth._20bit : accessWidth;
                if(addressingMode == AddressingMode.Register)
                {
                    // NOTE: When using Register mode, we have to check for amount of repetition
                    var repetitionSource = ((extensionWord >> 7) & 0x1) > 0;
                    if(repetitionSource)
                    {
                        // NOTE: Number of `n - 1` repetition is in register
                        var register = (Registers)(extensionWord & 0x000F);
                        repetition = (int)GetRegisterValue(register, AddressingMode.Register) & 0xF;
                    }
                    else
                    {
                        // NOTE: Number of `n - 1` repetition in low 4 bits of the opcode
                        repetition = extensionWord & 0x000F;
                    }
                    repetition += 1;
                    resetCarry = ((extensionWord >> 8) & 0x1) > 0;

                    this.Log(LogLevel.Debug, "Repetitions: {0}, sourced from register?: {1}", repetition, repetitionSource);
                }
                else
                {
                    sourceExtension = ((uint)extensionWord & 0x0780) << 9;
                    destinationExtension = ((uint)extensionWord & 0x000F) << 16;
                }
            }

            // NOTE: Handle single-operand (Format II) instructions first
            if(TryEvaluateSingleOperand(instr, destination, accessWidth, addressingMode, out var executionResult, addressExtension: destinationExtension, repetition: repetition, resetCarry: resetCarry))
            {
                return executionResult;
            }

            // NOTE: Handle double-operand (Format I) instructions
            TryEvaluateDoubleOperand(instr, destination, accessWidth, addressingMode, out executionResult, destinationExtension: destinationExtension, sourceExtension: sourceExtension, repetition: repetition, resetCarry: resetCarry);

            return executionResult;
        }

        private uint BinaryToBCD(uint binary)
        {
            return (((binary /     1) % 10) <<  0) |
                   (((binary /    10) % 10) <<  4) |
                   (((binary /   100) % 10) <<  8) |
                   (((binary /  1000) % 10) << 12) |
                   (((binary / 10000) % 10) << 16);
        }

        private uint BCDToBinary(uint bcd)
        {
            return ((bcd >>  0) & 0xf) *     1 +
                   ((bcd >>  4) & 0xf) *    10 +
                   ((bcd >>  8) & 0xf) *   100 +
                   ((bcd >> 12) & 0xf) *  1000 +
                   ((bcd >> 16) & 0xf) * 10000;
        }

        private uint TruncateWithFlags(uint value, AccessWidth accessWidth = AccessWidth._16bit)
        {
            var mask = GetAccessWidthMask(accessWidth);

            SetStatusFlag(StatusFlags.Carry, value > mask);
            value &= mask;
            SetStatusFlag(StatusFlags.Zero, value == 0);
            SetStatusFlag(StatusFlags.Negative, (value & GetAccessWidthMSB(accessWidth)) > 0);
            return value;
        }

        private void CheckForOverflow(uint operand1, uint operand2, uint result, AccessWidth accessWidth = AccessWidth._16bit)
        {
            var mask = GetAccessWidthMSB(accessWidth);
            SetStatusFlag(StatusFlags.Overflow, ((operand1 ^ operand2) & mask) == 0 && ((operand1 ^ result) & mask) > 0);
        }

        private bool TryPerformDirectWrite(ulong address, uint value, AccessWidth accessWidth)
        {
            var len = (ulong)GetAccessWidthInBytes(accessWidth);
            var keyValue = arrayMemoryList.Where(e => address >= e.Key && address + len < e.Key + (ulong)e.Value.Size).FirstOrDefault();
            if(keyValue.Value == null)
            {
                return false;
            }

            var underlyingMemory = keyValue.Value;
            address -= keyValue.Key;

            switch(accessWidth)
            {
                case AccessWidth._8bit:
                    underlyingMemory.WriteByte((long)address, (byte)value);
                    break;

                case AccessWidth._16bit:
                    underlyingMemory.WriteWord((long)address, (ushort)value);
                    break;

                case AccessWidth._20bit:
                    underlyingMemory.WriteDoubleWord((long)address, value & GetAccessWidthMask(accessWidth));
                    break;

                default:
                    throw new Exception("unreachable");
            }

            return true;
        }

        private void PerformMemoryWrite(ulong address, uint value, AccessWidth accessWidth)
        {
            if(machine.SystemBus.TryGetWatchpointsAt(address, Access.Write, out var _))
            {
                pendingWatchpoints.Add(new PendingWatchpoint(address, accessWidth, value));
            }

            if(TryPerformDirectWrite(address, value, accessWidth))
            {
                return;
            }

            switch(accessWidth)
            {
                case AccessWidth._8bit:
                    machine.SystemBus.WriteByte(address, (byte)value);
                    break;

                case AccessWidth._16bit:
                    machine.SystemBus.WriteWord(address, (ushort)value);
                    break;

                case AccessWidth._20bit:
                    machine.SystemBus.WriteWord(address, (ushort)value);
                    machine.SystemBus.WriteWord(address + 2, (ushort)((value >> 16) & 0xF));
                    break;

                default:
                    throw new Exception("unreachable");
            }
        }

        private bool TryPerfrormDirectRead(ulong address, AccessWidth accessWidth, out uint value)
        {
            var len = (ulong)GetAccessWidthInBytes(accessWidth);
            var keyValue = arrayMemoryList.Where(e => address >= e.Key && address + len < e.Key + (ulong)e.Value.Size).FirstOrDefault();
            if(keyValue.Value == null)
            {
                value = 0;
                return false;
            }

            var underlyingMemory = keyValue.Value;
            address -= keyValue.Key;

            switch(accessWidth)
            {
                case AccessWidth._8bit:
                    value = underlyingMemory.ReadByte((long)address);
                    break;

                case AccessWidth._16bit:
                    value = underlyingMemory.ReadWord((long)address);
                    break;

                case AccessWidth._20bit:
                    value = underlyingMemory.ReadDoubleWord((long)address);
                    value &= GetAccessWidthMask(accessWidth);
                    break;

                default:
                    throw new Exception("unreachable");
            }

            return true;
        }

        private uint PerformMemoryRead(ulong address, AccessWidth accessWidth)
        {
            if(machine.SystemBus.TryGetWatchpointsAt(address, Access.Read, out var _))
            {
                pendingWatchpoints.Add(new PendingWatchpoint(address, accessWidth));
            }

            if(TryPerfrormDirectRead(address, accessWidth, out var directValue))
            {
                return directValue;
            }

            switch(accessWidth)
            {
                case AccessWidth._8bit:
                    return machine.SystemBus.ReadByte(address);

                case AccessWidth._16bit:
                    return machine.SystemBus.ReadWord(address);

                case AccessWidth._20bit:
                    var value = (uint)machine.SystemBus.ReadWord(address);
                    value |= (uint)(machine.SystemBus.ReadWord(address + 2) << 16);
                    value &= GetAccessWidthMask(accessWidth);
                    return value;

                default:
                    throw new Exception("unreachable");
            }
        }

        private static int GetAccessWidthInBits(AccessWidth accessWidth)
        {
            switch(accessWidth)
            {
                case AccessWidth._8bit: return 8;
                case AccessWidth._16bit: return 16;
                case AccessWidth._20bit: return 20;
                default: throw new Exception("unreachable");
            }
        }

        private static int GetAccessWidthInBytes(AccessWidth accessWidth)
        {
            return (int)accessWidth;
        }

        private static uint GetAccessWidthMask(AccessWidth accessWidth)
        {
            return (1U << GetAccessWidthInBits(accessWidth)) - 1;
        }

        private static uint GetAccessWidthMSB(AccessWidth accessWidth)
        {
            return (1U << (GetAccessWidthInBits(accessWidth) - 1));
        }

        private StatusFlags statusRegister;
        private ulong executedInstructions;

        private readonly List<PendingWatchpoint> pendingWatchpoints = new List<PendingWatchpoint>();
        private readonly SortedSet<int> pendingInterrupt = new SortedSet<int>();
        private readonly IDictionary<ulong, HashSet<Action<ICpuSupportingGdb, ulong>>> hooks =
            new Dictionary<ulong, HashSet<Action<ICpuSupportingGdb, ulong>>>();
        private readonly SortedList<ulong, ArrayMemory> arrayMemoryList = new SortedList<ulong, ArrayMemory>();

        private const uint InterruptVectorStart = 0xFFFE;

        private sealed class PendingWatchpoint
        {
            public PendingWatchpoint(ulong address, AccessWidth accessWidth, uint? value = null)
            {
                Address = address;
                AccessWidth = accessWidth;
                Value = value;
            }

            public SysbusAccessWidth SysbusAccessWidth
            {
                get
                {
                    switch(AccessWidth)
                    {
                        case AccessWidth._8bit:
                            return SysbusAccessWidth.Byte;
                        case AccessWidth._16bit:
                            return SysbusAccessWidth.Word;
                        case AccessWidth._20bit:
                            return SysbusAccessWidth.DoubleWord;
                        default:
                            throw new Exception("unreachable");
                    }
                }
            }

            public ulong Address { get; }
            public AccessWidth AccessWidth { get; }
            public uint? Value { get; }
        }

        [Flags]
        private enum StatusFlags : ushort
        {
            Carry = (1 << 0),
            Zero = (1 << 1),
            Negative = (1 << 2),
            GeneralInterruptEnable = (1 << 3),
            CPUOff = (1 << 4),
            OscillatorOff = (1 << 5),
            SystemClockGenerator0 = (1 << 6),
            SystemClockGenerator1 = (1 << 7),
            Overflow = (1 << 8)
        }

        // NOTE: Enum value stores a byte width
        private enum AccessWidth
        {
            _8bit = 1,
            _16bit = 2,
            _20bit = 4,
        }

        private enum AddressingMode
        {
            Register,
            Indexed,
            IndirectRegister,
            IndirectAutoincrement,
        }

        public enum Registers : int
        {
            PC,
            SP,
            SR,
            R3,
            R4,
            R5,
            R6,
            R7,
            R8,
            R9,
            R10,
            R11,
            R12,
            R13,
            R14,
            R15,
        }
    }
}
