//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using Antmicro.Renode.Logging;
using System.Numerics;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Miscellaneous;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Antmicro.Renode.Peripherals.Bus;
using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class OpenTitan_BigNumberAcceleratorCore : RiscV32, IOpenTitan_BigNumberAcceleratorCore
    {
        public OpenTitan_BigNumberAcceleratorCore(OpenTitan_BigNumberAccelerator parent, OpenTitan_ScrambledMemory instructionsMemory, OpenTitan_ScrambledMemory dataMemory)
            : base(timeProvider: null, cpuType: "rv32im_zicsr", machine: null, hartId: 0, privilegedArchitecture: PrivilegedArchitecture.Priv1_10, endianness: Endianess.LittleEndian)
        {
            this.parent = parent;
            this.instructionsMemory = instructionsMemory;
            this.dataMemory = dataMemory;

            this.random = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            this.loopStack = new Stack<LoopContext>();

            foreach(var segment in instructionsMemory.MappedSegments)
            {
                this.MapMemory(segment);
            }

            this.EnableExternalWindowMmu(true);
            var insnFetchWindow = (uint)this.AcquireExternalMmuWindow((int)ExternalMmuBase.Privilege.Execute); //Insn fetch only
            this.SetMmuWindowStart(insnFetchWindow, 0x0);
            this.SetMmuWindowEnd(insnFetchWindow, (ulong)instructionsMemory.Size);
            this.SetMmuWindowAddend(insnFetchWindow, 0);
            this.SetMmuWindowPrivileges(insnFetchWindow, (int)ExternalMmuBase.Privilege.Execute);
            this.AddHookAtInterruptBegin(HandleException);

            foreach(var segment in dataMemory.MappedSegments)
            {
                var wrappedSegment = new SystemBus.MappedSegmentWrapper(segment, VirtualDataOffset, segment.Size, this);
                this.MapMemory(wrappedSegment);
            }

            var dataWindow = (uint)this.AcquireExternalMmuWindow((int)ExternalMmuBase.Privilege.ReadAndWrite); // Data read and write
            this.SetMmuWindowStart(dataWindow, 0x0);
            this.SetMmuWindowEnd(dataWindow, (ulong)dataMemory.Size);
            this.SetMmuWindowAddend(dataWindow, VirtualDataOffset);
            this.SetMmuWindowPrivileges(dataWindow, (int)ExternalMmuBase.Privilege.ReadAndWrite);

            // Add the X1 register handling
            this.EnablePostGprAccessHooks(1);
            this.InstallPostGprAccessHookOn(1, HandleX1Access, 1);
            this.x1Stack = new Stack<uint>();

            this.flagsGroup = new CustomFlags[FlagsGroupsCount];

            wideDataRegisters = new WideRegister[WideDataRegistersCount];
            for(var index = 0; index < WideDataRegistersCount; index++)
            {
                wideDataRegisters[index] = new WideRegister();
            }

            wideSpecialPurposeRegisters = new WideRegister[WideSpecialPurposeRegistersCount];
            wideSpecialPurposeRegisters[(int)WideSPR.Mod] = new WideRegister();
            wideSpecialPurposeRegisters[(int)WideSPR.Rnd] = new WideRegister(readOnly: true);
            wideSpecialPurposeRegisters[(int)WideSPR.URnd] = new WideRegister(readOnly: true);
            wideSpecialPurposeRegisters[(int)WideSPR.Acc] = new WideRegister();
            wideSpecialPurposeRegisters[(int)WideSPR.KeyShare0Low] = new WideRegister(readOnly: true);
            wideSpecialPurposeRegisters[(int)WideSPR.KeyShare0High] = new WideRegister(readOnly: true);
            wideSpecialPurposeRegisters[(int)WideSPR.KeyShare1Low] = new WideRegister(readOnly: true);
            wideSpecialPurposeRegisters[(int)WideSPR.KeyShare1High] = new WideRegister(readOnly: true);

            RegisterCustomCSRs();
            RegisterCustomOpcodes();
        }

        public override void Reset()
        {
            base.Reset();

            PC = 0x0;
            loopStack.Clear();
            x1Stack.Clear();

            // clear WDRs
            foreach(var wdr in wideDataRegisters)
            {
                wdr.Clear();
            }

            // clear all RW WSPRs
            foreach(var wspr in wideSpecialPurposeRegisters.Where(x => !x.ReadOnly))
            {
                wspr.Clear();
            }

            // clear flags
            for(var index = 0; index < flagsGroup.Length; index++)
            {
                flagsGroup[index] = default(CustomFlags);
            }
        }

        public BigInteger GetWideRegister(int index, bool special = false)
        {
            if(special)
            {
                return wideSpecialPurposeRegisters[index].AsBigInteger;
            }
            else
            {
                return wideDataRegisters[index].AsBigInteger;
            }
        }

        public void SetWideRegister(int index, BigInteger value, bool special = false)
        {
            if(special)
            {
                wideSpecialPurposeRegisters[index].SetTo(value);
            }
            else
            {
                wideDataRegisters[index].SetTo(value);
            }
        }

        public CoreError LastError { get; private set; }

        public string FixedRandomPattern
        {
            get => fixedRandomPattern;
            set
            {
                fixedRandomPattern = value;
                fixedRandomBytes = (value != null)
                    ? ParseHexPattern(value, 256)
                    : null;
            }
        }

        public string KeyShare0
        {
            get => keyShare0;
            set
            {
                keyShare0 = value;
                UpdateKeyShare(value, (int)WideSPR.KeyShare0Low, (int)WideSPR.KeyShare0High);
            }
        }

        public string KeyShare1
        {
            get => keyShare1;
            set
            {
                keyShare1 = value;
                UpdateKeyShare(value, (int)WideSPR.KeyShare1Low, (int)WideSPR.KeyShare1High);
            }
        }

        private void UpdateKeyShare(string value, int lowRegisterId, int highRegisterId)
        {
            if(value != null)
            {
                var bytes = ParseHexPattern(value, 64);
                if(bytes.Length > 64)
                {
                    throw new RecoverableException($"Provided key share is too long (expected up to 64 bytes): {value}");
                }

                wideSpecialPurposeRegisters[lowRegisterId].SetTo(new BigInteger(bytes.Take(32).ToArray()));

                if(bytes.Length > 32)
                {
                    wideSpecialPurposeRegisters[highRegisterId].SetTo(new BigInteger(bytes.Skip(32).ToArray()));
                }
            }
            else
            {
                wideSpecialPurposeRegisters[lowRegisterId].Clear();
                wideSpecialPurposeRegisters[highRegisterId].Clear();
            }
        }

        private void HandleException(ulong exceptionType)
        {
            Log(LogLevel.Debug, $"Handling exception of type 0x{exceptionType:X}");
            switch(exceptionType)
            {
                case 0x0: // RISCV_EXCP_INST_ADDR_MIS
                case 0x1: // RISCV_EXCP_INST_ACCESS_FAULT
                case 0xc: // RISCV_EXCP_INST_PAGE_FAULT          /* since: priv-1.10.0 */
                    ThrowError(CoreError.BadInstructionAddress, false);
                    break;
                case 0x2: // RISCV_EXCP_ILLEGAL_INST
                    ThrowError(CoreError.IllegalInstruction, false);
                    break;
                case 0x4: // RISCV_EXCP_LOAD_ADDR_MIS
                case 0x5: // RISCV_EXCP_LOAD_ACCESS_FAULT
                case 0x6: // RISCV_EXCP_STORE_AMO_ADDR_MIS
                case 0x7: // RISCV_EXCP_STORE_AMO_ACCESS_FAULT
                case 0xd: // RISCV_EXCP_LOAD_PAGE_FAULT          /* since: priv-1.10.0 */
                case 0xf: // RISCV_EXCP_STORE_PAGE_FAULT         /* since: priv-1.10.0 */
                    ThrowError(CoreError.BadDataAddress, false);
                    break;
                case 0x3: // RISCV_EXCP_BREAKPOINT
                case 0x8: // RISCV_EXCP_U_ECALL
                case 0x9: // RISCV_EXCP_S_ECALL
                case 0xa: // RISCV_EXCP_H_ECALL
                case 0xb: // RISCV_EXCP_M_ECALL
                    // just ignore
                    break;
            }
        }

        private void Log(LogLevel logLevel, string message)
        {
            parent.Log(logLevel, "OTBN Core: {0}", message);
        }

        private void RegisterCustomCSRs()
        {
            RegisterCSR((ulong)CustomCSR.FlagGroup0,
                        () => (ulong)flagsGroup[0],
                        val => { flagsGroup[0] = (CustomFlags)val; },
                        name: "FG0");

            RegisterCSR((ulong)CustomCSR.FlagGroup1,
                        () => (ulong)flagsGroup[1],
                        val => { flagsGroup[1] = (CustomFlags)val; },
                        name: "FG1");

            RegisterCSR((ulong)CustomCSR.Flags,
                        () => (ulong)((uint)flagsGroup[0] | ((uint)flagsGroup[1] << 4)),
                        val => { flagsGroup[0] = (CustomFlags)(val & 0xF); flagsGroup[1] = (CustomFlags)(val >> 4); },
                        name: "FLAGS");

            for(var x = 0; x < 8; x++)
            {
                var index = x;
                RegisterCSR((ulong)(CustomCSR.Mod0 + index),
                            () => (ulong)(wideSpecialPurposeRegisters[(int)WideSPR.Mod].PartialGet(index * sizeof(uint), sizeof(uint))),
                            val => { wideSpecialPurposeRegisters[(int)WideSPR.Mod].PartialSet(new BigInteger(val), index * sizeof(uint), 4); },
                            name: $"MOD{index}");
            }

            RegisterCSR((ulong)CustomCSR.RndPrefetch,
                        () => 0,
                        val => { }, // there is no need for any prefetch action in the simulation
                        name: "RND_PREFETCH");

            // both RND and URND are implemented the same way in the simulation
            RegisterCSR((ulong)CustomCSR.Rnd,
                        () => GetPseudoRandom(),
                        val => { },
                        name: "RND");

            RegisterCSR((ulong)CustomCSR.URnd,
                        () => GetPseudoRandom(),
                        val => { },
                        name: "URND");
        }

        private void ThrowError(CoreError error, bool stopExecution = true)
        {
            LastError = error;
            if(stopExecution)
            {
                this.TlibRequestTranslationBlockInterrupt(0);
            }
        }

        private void HandleX1Access(bool isWrite)
        {
            Log(LogLevel.Debug, string.Format("Handling X1 {0} hook; current depth of the stack is {1}", isWrite ? "write" : "read", x1Stack.Count));

            if(isWrite)
            {
                if(x1Stack.Count == MaximumStackCapacity)
                {
                    Log(LogLevel.Error, "x1 register: stack overflow");
                    ThrowError(CoreError.CallStack);
                    return;
                }
                x1Stack.Push(this.GetRegister(1));
            }
            else
            {
                if(x1Stack.Count == 0)
                {
                    Log(LogLevel.Error, "x1 register: trying to pop value from empty stack");
                    ThrowError(CoreError.CallStack);
                    return;
                }

                this.SetRegister(1, x1Stack.Pop());
            }
        }

        private void RegisterCustomOpcodes()
        {
            InstallCustomInstruction(BnAddIPattern, BnImmHandler, name: "BN.ADD.I");
            InstallCustomInstruction(BnSubIPattern, BnImmHandler, name: "BN.SUB.I");
            InstallCustomInstruction(BnRShiPattern, BnRShiftIHandler, name: "BN.RSHI");
            InstallCustomInstruction(BnAddPattern, BnAddSubHandler, name: "BN.ADD");
            InstallCustomInstruction(BnAddCPattern, BnAddSubHandler, name: "BN.ADD.C");
            InstallCustomInstruction(BnAddMPattern, BnAddSubHandler, name: "BN.ADD.M");
            InstallCustomInstruction(BnSubPattern, BnAddSubHandler, name: "BN.SUB");
            InstallCustomInstruction(BnSubBPattern, BnAddSubHandler, name: "BN.SUB.B");
            InstallCustomInstruction(BnSubMPattern, BnAddSubHandler, name: "BN.SUB.M");
            InstallCustomInstruction(BnMulQAccPattern, BnMulQAccHandler, name: "BN.MUL.QACC");
            InstallCustomInstruction(BnMulQAccWoPattern, BnMulQAccWithWriteHandler, name: "BN.MUL.QACC.WO");
            InstallCustomInstruction(BnMulQAccSoPattern, BnMulQAccWithWriteHandler, name: "BN.MUL.QACC.SO");
            InstallCustomInstruction(BnAndPattern, BnBitwiseHelper, name: "BN.AND");
            InstallCustomInstruction(BnOrPattern, BnBitwiseHelper, name: "BN.OR");
            InstallCustomInstruction(BnNotPattern, BnBitwiseHelper, name: "BN.NOT");
            InstallCustomInstruction(BnXOrPattern, BnBitwiseHelper, name: "BN.XOR");
            InstallCustomInstruction(BnSelPattern, BnSelHandler, name: "BN.SEL");
            InstallCustomInstruction(BnCmpPattern, BnCmpHandler, name: "BN.CMP");
            InstallCustomInstruction(BnCmpBPattern, BnCmpHandler, name: "BN.CMP.B");
            InstallCustomInstruction(BnMovPattern, BnMovHandler, name: "BN.MOV");
            InstallCustomInstruction(BnLidPattern, BnLoadStoreHandler, name: "BN.LID");
            InstallCustomInstruction(BnSidPattern, BnLoadStoreHandler, name: "BN.SID");
            InstallCustomInstruction(BnMovrPattern, BnMovrHandler, name: "BN.MOVR");
            InstallCustomInstruction(BnWsrrPattern, WsrWriteReadHandler, name: "BN.WSRR");
            InstallCustomInstruction(BnWsrwPattern, WsrWriteReadHandler, name: "BN.WSRW");
            InstallCustomInstruction(ECallPattern, ECallHandler, name: "ECALL");

            InstallCustomInstruction(LoopPattern, LoopHandler, name: "LOOP");
            InstallCustomInstruction(LoopiPattern, LoopiHandler, name: "LOOPI");
        }

        public ExecutionResult ExecuteInstructions(int numberOfInstructionsToExecute, out ulong numberOfExecutedInstructions)
        {
            Log(LogLevel.Debug, string.Format("Executing #{0} instruction(s) at {1}", numberOfInstructionsToExecute, PC));
            LastError = CoreError.None;
            executionFinished = false;

            if(loopStack.Count != 0)
            {
                ExecuteLoop(out numberOfExecutedInstructions);
            }
            else
            {
                // normal execution
                base.ExecuteInstructions((ulong)numberOfInstructionsToExecute, out numberOfExecutedInstructions);
            }

            if(LastError != CoreError.None)
            {
                return ExecutionResult.Aborted;
            }

            return executionFinished
                ? ExecutionResult.Interrupted
                : ExecutionResult.Ok;
        }

        private void ExecuteLoop(out ulong numberOfExecutedInstructions)
        {
            // handle loop - since we must detect the end of the loop, we switch to stepping
            var currentLoopContext = loopStack.Peek();
            Log(LogLevel.Debug, string.Format("Handling loop at 0x{0:X}, current context: {1}", PC, currentLoopContext));

            if(currentLoopContext.NumberOfIterations == 0)
            {
                Log(LogLevel.Error, "Unexpected end of loop");
                loopStack.Pop();
                numberOfExecutedInstructions = 0;

                ThrowError(CoreError.Loop);
                return;
            }

            var isLastInstructionInTheIteration = (PC == currentLoopContext.EndPC);
            var result = base.ExecuteInstructions(1, out numberOfExecutedInstructions);

            if(isLastInstructionInTheIteration)
            {
                currentLoopContext.NumberOfIterations--;
                if(currentLoopContext.NumberOfIterations == 0)
                {
                    loopStack.Pop();
                    Log(LogLevel.Debug, string.Format("Finished the loop; loop stack depth is: {0}", loopStack.Count));
                }
                else
                {
                    Log(LogLevel.Debug, string.Format("Reached the end of loop body, decreasing number of iterations to {0}; loop stack depth is: {1}", currentLoopContext.NumberOfIterations, loopStack.Count));
                    PC = currentLoopContext.StartPC;
                }
            }
        }

        private byte[] ParseHexPattern(string input, int width)
        {
            if(input.StartsWith("0x"))
            {
                input = input.Substring(2);
            }

            // fill with leading 0's
            if(input.Length < width * 2)
            {
                input = new string('0', (width * 2) - input.Length) + input;
            }

            try
            {
                return Misc.HexStringToByteArray(input, reverse: true);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Couldn't parse hex pattern: {e.Message}");
            }
        }

        private void LoopHandler(ulong opcode)
        {
            // decode parameters
            var grs = (int)BitHelper.GetValue(opcode, 15, 5);

            var bodySize = (uint)BitHelper.GetValue(opcode, 20, 12) + 1;
            var numberOfIterations = (uint)this.GetRegister(grs).RawValue;

            InnerLoopHandler(bodySize, numberOfIterations);
        }

        private void LoopiHandler(ulong opcode)
        {
            // decode parameters
            var iterations1 = (uint)BitHelper.GetValue(opcode, 15, 5);
            var iterations0 = (uint)BitHelper.GetValue(opcode, 7, 5);

            var numberOfIterations = (iterations1 << 5) + iterations0;
            var bodySize = (uint)BitHelper.GetValue(opcode, 20, 12) + 1;

            InnerLoopHandler(bodySize, numberOfIterations);
        }

        private void InnerLoopHandler(uint bodySize, uint numberOfIterations)
        {
            if(numberOfIterations == 0)
            {
                Log(LogLevel.Error, "Number of loop iterations cannot be 0");
                ThrowError(CoreError.Loop);
                return;
            }

            if(loopStack.Count == MaxLoopStackHeight)
            {
                Log(LogLevel.Error, "Maximum loop stack height");
                ThrowError(CoreError.Loop);
                return;
            }

            if(loopStack.Any(x => x.EndPC == PC))
            {
                Log(LogLevel.Error, "Loop instruction cannot appear as the last instruction of a loop body");
                ThrowError(CoreError.Loop);
                return;
            }

            var newContext = new LoopContext
            {
                // each instruction is encoded on 4 bytes
                StartPC = PC + 4u,
                EndPC = PC + bodySize * 4,
                NumberOfIterations = numberOfIterations
            };

            loopStack.Push(newContext);
            Log(LogLevel.Debug, string.Format("Added new loop context at 0: {1}", PC, newContext));
        }

        private void WsrWriteReadHandler(ulong opcode)
        {
            var isWrite = BitHelper.IsBitSet((uint)opcode, 31);
            var wsr = (int)BitHelper.GetValue(opcode, 20, 8);
            var wrd = (int)BitHelper.GetValue(opcode, 7, 5);
            var wrs = (int)BitHelper.GetValue(opcode, 15, 5);

            if(wsr > wideSpecialPurposeRegisters.Length)
            {
                Log(LogLevel.Error, $"The wsr is argument is too high: 0x{wsr:X}");
                ThrowError(CoreError.IllegalInstruction);
                return;
            }

            if(isWrite)
            {
                if(wideSpecialPurposeRegisters[wsr].ReadOnly)
                {
                    // ignore writes to RO WSRS
                    return;
                }
                wideSpecialPurposeRegisters[wsr].SetTo(wideDataRegisters[wrs].AsBigInteger);
            }
            else
            {
                if(wsr == (int)WideSPR.Rnd || wsr == (int)WideSPR.URnd)
                {
                    byte[] data;
                    if(fixedRandomBytes != null)
                    {
                        data = fixedRandomBytes;
                    }
                    else
                    {
                        data = new byte[256];
                        random.NextBytes(data);
                    }
                    wideDataRegisters[wrd].SetTo(new BigInteger(data));
                }
                else
                {
                    wideDataRegisters[wrd].SetTo(wideSpecialPurposeRegisters[wsr].AsBigInteger);
                }
            }
        }

        private void BnMulQAccHandler(ulong opcode)
        {
            ParseRTypeFields(opcode, out var _, out var __, out var wrs1, out var wrs2, out var ___, out var ____, out var _____);

            var zeroAccumulator = BitHelper.IsBitSet(opcode, 12);
            var shift = (int)BitHelper.GetValue(opcode, 13, 2) << 6;
            var rs1QuarterWord = (int)BitHelper.GetValue(opcode, 25, 2);
            var rs2QuarterWord = (int)BitHelper.GetValue(opcode, 27, 2);

            var rs1 = wideDataRegisters[wrs1].PartialGet(rs1QuarterWord * sizeof(ulong), sizeof(ulong));
            var rs2 = wideDataRegisters[wrs2].PartialGet(rs2QuarterWord * sizeof(ulong), sizeof(ulong));
            var result = rs1 * rs2;
            result = (result << shift) & WideRegister.MaxValueMask;
            if(!zeroAccumulator)
            {
                var acc = wideSpecialPurposeRegisters[(int)WideSPR.Acc].AsBigInteger;
                result = result + acc;
            }
            wideSpecialPurposeRegisters[(int)WideSPR.Acc].SetTo(result);
        }

        private void BnMulQAccWithWriteHandler(ulong opcode)
        {
            ParseRTypeFields(opcode, out var f3, out var wrd, out var wrs1, out var wrs2, out var _, out var __, out var flagGroup);

            var zeroAccumulator = BitHelper.IsBitSet(opcode, 12);
            var shift = (int)BitHelper.GetValue(opcode, 13, 2) << 6;
            var rs1QuarterWord = (int)BitHelper.GetValue(opcode, 25, 2);
            var rs2QuarterWord = (int)BitHelper.GetValue(opcode, 27, 2);
            var halfwordSelect = BitHelper.GetValue(opcode, 29, 1);
            var fullWordWriteback = !BitHelper.IsBitSet(opcode, 30);

            if(zeroAccumulator)
            {
                wideSpecialPurposeRegisters[(int)WideSPR.Acc].Clear();
            }

            var rs1 = wideDataRegisters[wrs1].PartialGet(rs1QuarterWord * sizeof(ulong), sizeof(ulong));
            var rs2 = wideDataRegisters[wrs2].PartialGet(rs2QuarterWord * sizeof(ulong), sizeof(ulong));
            var result = rs1 * rs2;
            result = (result << shift) & WideRegister.MaxValueMask;
            result += wideSpecialPurposeRegisters[(int)WideSPR.Acc].AsBigInteger;

            if(fullWordWriteback)
            {
                wideSpecialPurposeRegisters[(int)WideSPR.Acc].SetTo(result);
                wideDataRegisters[wrd].SetTo(result);
                flagsGroup[flagGroup] = GetFlags(result);
            }
            else
            {
                var mask = (new BigInteger(1ul) << 128) - 1;
                var lowPart = result & mask;
                var highPart = (result >> 128) & mask;
                var oldWord = wideDataRegisters[wrd].AsBigInteger;

                var hwShift = 128 * halfwordSelect;
                var hwMask = mask << (int)hwShift;
                var newWord = (oldWord & ~hwMask) | (lowPart << (int)hwShift);
                wideDataRegisters[wrd].SetTo(newWord);
                wideSpecialPurposeRegisters[(int)WideSPR.Acc].SetTo(highPart);

                var oldFlags = flagsGroup[flagGroup];
                flagsGroup[flagGroup] = GetMulWithWriteCustomFlags(oldFlags, lowPart, halfwordSelect);
            }
        }

        private CustomFlags GetMulWithWriteCustomFlags(CustomFlags oldFlags, BigInteger lowPart, ulong halfwordSelect)
        {
            var newFlags = CustomFlags.Empty;

            if(oldFlags.HasFlag(CustomFlags.Carry))
            {
                newFlags |= CustomFlags.Carry;
            }
            if(halfwordSelect == 0)
            {
                if(oldFlags.HasFlag(CustomFlags.Msb))
                {
                    newFlags |= CustomFlags.Msb;
                }
                if((lowPart & 0b1) == 1)
                {
                    newFlags |= CustomFlags.Lsb;
                }
                if(lowPart == 0)
                {
                    newFlags |= CustomFlags.Zero;
                }
            }
            else
            {
                if(((lowPart >> 127) & 0b1) == 1)
                {
                    newFlags |= CustomFlags.Msb;
                }
                if(oldFlags.HasFlag(CustomFlags.Lsb))
                {
                    newFlags |= CustomFlags.Lsb;
                }
                if(oldFlags.HasFlag(CustomFlags.Zero) & (lowPart == 0))
                {
                    newFlags |= CustomFlags.Zero;
                }
            }
            return newFlags;
        }

        private void BnCmpHandler(ulong opcode)
        {
            ParseRTypeFields(opcode, out var f3, out var _, out var wrs1, out var wrs2, out var shiftBits, out var shiftRight, out var flagGroup);

            var rs1 = wideDataRegisters[wrs1].AsBigInteger;
            var rs2 = wideDataRegisters[wrs2].AsBigInteger;
            var isBorrowVariant = (f3 == 0b011);

            var result = rs1 - rs2;
            if(isBorrowVariant)
            {
                var carryVal = flagsGroup[flagGroup].HasFlag(CustomFlags.Carry) ? 1 : 0;
                result -= carryVal;
            }

            flagsGroup[flagGroup] = GetFlags(result);
        }

        private void BnSelHandler(ulong opcode)
        {
            ParseRTypeFields(opcode, out var _, out var wrd, out var wrs1, out var wrs2, out var __, out var ___, out var flagGroup);
            var selFlag = (int)BitHelper.GetValue(opcode, 25, 2);
            var flagType = (CustomFlags)(1 << selFlag);
            var flagSet = flagsGroup[flagGroup].HasFlag(flagType);

            var wrs = flagSet ? wrs1 : wrs2;
            wideDataRegisters[wrd].SetTo(wideDataRegisters[wrs].AsBigInteger);
        }

        private void BnAddSubHandler(ulong opcode)
        {
            ParseRTypeFieldsAndShiftR2(opcode, out var f3, out var wrd, out var rs1, out var rs2, out var flagGroup);

            var isAdd = ((f3 == 0b101) && !BitHelper.IsBitSet(opcode, 30)) || ((f3 & 0b1) == 0);
            var modulo = (f3 >> 1) == 0b10;
            var carry = (f3 >> 1) == 0b01;

            var result = isAdd ? rs1 + rs2 : rs1 - rs2;

            if(modulo)
            {
                var modVal = wideSpecialPurposeRegisters[(int)WideSPR.Mod].AsBigInteger;
                if(isAdd && (result >= modVal))
                {
                    result -= modVal;
                }
                else if(!isAdd && (result < BigInteger.Zero))
                {
                    result += modVal;
                }
            }
            else if(carry)
            {
                var carryVal = flagsGroup[flagGroup].HasFlag(CustomFlags.Carry) ? 1 : 0;
                result = isAdd ? result + carryVal : result - carryVal;
            }
            wideDataRegisters[wrd].SetTo(result);
            if(!modulo)
            {
                flagsGroup[flagGroup] = GetFlags(result);
            }
        }

        private CustomFlags GetFlags(BigInteger value)
        {
            var flags = CustomFlags.Empty;
            if(((value >> 256) & 1) != 0)
            {
                flags |= CustomFlags.Carry;
            }
            if(value == 0)
            {
                flags |= CustomFlags.Zero;
            }
            else
            {
                if(((value >> 255) & 1) != 0)
                {
                    flags |= CustomFlags.Msb;
                }
                if((value & 1) != 0)
                {
                    flags |= CustomFlags.Lsb;
                }
            }
            return flags;
        }

        private void ECallHandler(ulong opcode)
        {
            Log(LogLevel.Debug, "ECALL triggered");

            executionFinished = true;
            // Not an error, we just need it to cut the block and exit
            ThrowError(CoreError.None);
        }

        private void BnRShiftIHandler(ulong opcode)
        {
            ParseRTypeFields(opcode, out var _, out var wrd, out var wrs1, out var wrs2, out var __, out var ___, out var ____);
            var imm = (int)((BitHelper.GetValue(opcode, 25, 7) << 1) | BitHelper.GetValue(opcode, 14, 1));
            var rs1 = wideDataRegisters[wrs1].AsBigInteger;
            var rs2 = wideDataRegisters[wrs2].AsBigInteger;
            var result = ((rs1 << 256) ^ rs2) >> imm;
            wideDataRegisters[wrd].SetTo(result);
        }

        private void BnImmHandler(ulong opcode)
        {
            ParseRTypeFields(opcode, out var f3, out var wrd, out var wrs1, out var _, out var  __, out var ___, out var flagGroup);
            var imm = (int)BitHelper.GetValue(opcode, 20, 10);
            var isAdd = !BitHelper.IsBitSet(opcode, 30);
            var rs = wideDataRegisters[wrs1].AsBigInteger;
            var result = isAdd ? rs + imm : rs - imm;
            flagsGroup[flagGroup] = GetFlags(result);
            wideDataRegisters[wrd].SetTo(result);
        }

        void BnMovrHandler(ulong opcode)
        {
            ParseSTypeFields(opcode, out var _, out var grs, out var grd, out var __, out var ___, out var incrementRd);
            var incrementRs = BitHelper.IsBitSet(opcode, 9);

            var grsVal = (int)this.GetRegister(grs).RawValue;
            var grdVal = (int)this.GetRegister(grd).RawValue;

            if((incrementRs && incrementRd) || grdVal > 31 || grsVal > 31)
            {
                ThrowError(CoreError.IllegalInstruction);
                return;
            }

            if(incrementRd)
            {
                this.SetRegister(grd, grdVal + 1);
            }
            else if(incrementRs)
            {
                this.SetRegister(grs, grsVal + 1);
            }

            wideDataRegisters[grdVal].SetTo(wideDataRegisters[grsVal].AsBigInteger);
        }

        private void BnLoadStoreHandler(ulong opcode)
        {
            ParseSTypeFields(opcode, out var f3, out var grs1, out var grd, out var offset, out var incrementRs1, out var incrementRd);

            var isLoad = (f3 == 0b100);
            var grs1Val = (int)this.GetRegister(grs1).RawValue;
            var grdVal = (int)this.GetRegister(grd).RawValue;
            var addr = (long)(grs1Val + offset);

            if((incrementRs1 && incrementRd) || grdVal > 31)
            {
                ThrowError(CoreError.IllegalInstruction);
                return;
            }

            if(incrementRd)
            {
                this.SetRegister(grd, grdVal + 1);
            }
            if(incrementRs1)
            {
                this.SetRegister(grs1, grs1Val + 32);
            }

            if(isLoad)
            {
                var array = new byte[WideDataRegisterWidthInBytes];
                dataMemory.ReadBytes(addr, array.Length, array, 0);
                wideDataRegisters[grdVal].SetTo(new BigInteger(array));
            }
            else
            {
                var array = wideDataRegisters[grdVal].AsByteArray;
                dataMemory.WriteBytes(addr, array);
            }
        }

        void BnMovHandler(ulong opcode)
        {
            ParseRTypeFields(opcode, out var _, out var wrd, out var wrs, out var __, out var ___, out var ____, out var _____);
            wideDataRegisters[wrd].SetTo(wideDataRegisters[wrs].AsBigInteger);
        }

        private void BnBitwiseHelper(ulong opcode)
        {
            ParseRTypeFieldsAndShiftR2(opcode, out var f3, out var wrd, out var rs1, out var rs2, out var flagGroup);

            BigInteger result;
            switch(f3)
            {
                case 0b100:
                    result = rs1 | rs2;
                    break;
                case 0b010:
                    result = rs1 & rs2;
                    break;
                case 0b110:
                    result = rs1 ^ rs2;
                    break;
                case 0b101:
                    result = ~rs2;
                    break;
                default:
                    ThrowError(CoreError.IllegalInstruction, false);
                    return;
            }
            wideDataRegisters[wrd].SetTo(result);
            flagsGroup[flagGroup] = GetFlags(result);
        }

        private void ParseRTypeFieldsAndShiftR2(ulong opcode, out ulong f3, out int wrd, out BigInteger rs1, out BigInteger rs2, out int flagGroup)
        {
            ParseRTypeFields(opcode, out f3, out wrd, out var wsr1, out var wsr2, out var shiftBits, out var shiftRight, out flagGroup);
            rs1 = wideDataRegisters[wsr1].AsBigInteger;
            rs2 = wideDataRegisters[wsr2].AsBigInteger;
            if(shiftBits > 0)
            {
                rs2 = shiftRight ? rs2 >> shiftBits : rs2 << shiftBits;
                rs2 &= WideRegister.MaxValueMask;
            }
        }
        private void ParseRTypeFields(ulong opcode, out ulong f3, out int wrd, out int wrs1, out int wrs2, out int shiftBits, out bool shiftRight, out int flagGroup)
        {
            f3 = BitHelper.GetValue(opcode, 12, 3);
            wrd = (int)BitHelper.GetValue(opcode, 7, 5);
            wrs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            wrs2 = (int)BitHelper.GetValue(opcode, 20, 5);
            // The resolution of the shift is 8 bits
            shiftBits = (int)BitHelper.GetValue(opcode, 25, 5) * 8;
            shiftRight = BitHelper.IsBitSet(opcode, 30);
            flagGroup = (int)BitHelper.GetValue(opcode, 31, 1);
        }

        private void ParseSTypeFields(ulong opcode, out ulong f3, out int grs1, out int grd, out int offset, out bool incrementR1, out bool incrementRd)
        {
            grd = (int)BitHelper.GetValue(opcode, 20, 5);
            grs1 = (int)BitHelper.GetValue(opcode, 15, 5);
            f3 = BitHelper.GetValue(opcode, 12, 3);
            incrementR1 = BitHelper.IsBitSet(opcode, 8);
            incrementRd = BitHelper.IsBitSet(opcode, 7);
            offset = (int)((BitHelper.GetValue(opcode, 25, 7) << 3) | BitHelper.GetValue(opcode, 9, 3)) << 5;
            offset /= 8;
        }

        private ulong GetPseudoRandom()
        {
            var result = ((ulong)random.Next() << 32) + (ulong)random.Next();
            Log(LogLevel.Noisy, $"Generating random value of 0x{result:X}");
            return result;
        }

        private string keyShare0;
        private string keyShare1;
        private string fixedRandomPattern;
        private byte[] fixedRandomBytes;
        private bool executionFinished;

        private readonly WideRegister[] wideDataRegisters;
        private readonly WideRegister[] wideSpecialPurposeRegisters;

        private readonly OpenTitan_ScrambledMemory instructionsMemory;
        private readonly OpenTitan_ScrambledMemory dataMemory;

        private readonly CustomFlags[] flagsGroup;
        private readonly Stack<uint> x1Stack;
        private readonly Stack<LoopContext> loopStack;

        private readonly PseudorandomNumberGenerator random;
        private readonly OpenTitan_BigNumberAccelerator parent;

        // Big Number opcodes
        /* I-type
                                                   30        20   15 12    7      0
                                                 add|      Imm| WRS|f3| WRD|opcode| */
        private const string BnAddIPattern =      "-0---------------100-----0101011";
        private const string BnSubIPattern =      "-1---------------100-----0101011";
        /* I2-type
                                                        25   20   15 12    7      0
                                                      Imm|Wrs2|Wrs1|f3| WRD|opcode| */
        private const string BnRShiPattern =      "------------------11-----1111011";
        /*  R-type
                                                        25   20   15 12    7      0
                                                      add| rs2| rs1|f3|  rd|opcode| */
        private const string BnAddPattern =       "-----------------000-----0101011";
        private const string BnAddCPattern =      "-----------------010-----0101011";
        private const string BnAddMPattern =      "-0---------------101-----0101011";
        private const string BnSubPattern =       "-----------------001-----0101011";
        private const string BnSubBPattern =      "-----------------011-----0101011";
        private const string BnSubMPattern =      "-1---------------101-----0101011";
        private const string BnMulQAccPattern =   "-00----------------------0111011";
        private const string BnMulQAccWoPattern = "-01----------------------0111011";
        private const string BnMulQAccSoPattern = "-1-----------------------0111011";
        private const string LoopPattern =        "-----------------000-----1111011";
        private const string LoopiPattern =       "-----------------001-----1111011";
        private const string BnAndPattern =       "-----------------010-----1111011";
        private const string BnOrPattern =        "-----------------100-----1111011";
        private const string BnNotPattern =       "-----------------101-----1111011";
        private const string BnXOrPattern =       "-----------------110-----1111011";
        private const string BnSelPattern =       "-----------------000-----0001011";
        private const string BnCmpPattern =       "-----------------001-----0001011";
        private const string BnCmpBPattern =      "-----------------011-----0001011";
        private const string BnMovPattern =       "0----------------110-----0001011";
        /* S-type
                                                       25   20    15 12    7      0
                                                     off| Grd| Grs1|f3| add|opcode| */
        private const string BnLidPattern =       "-----------------100-----0001011";
        private const string BnSidPattern =       "-----------------101-----0001011";
        private const string BnMovrPattern =      "1----------------110-----0001011";
        /* WSR reg-type
                                                     28     20    15 12    7      0
                                                   add|   Wsr|  Wrs|f3| Wrd|opcode| */
        private const string BnWsrrPattern =      "0----------------111-----0001011";
        private const string BnWsrwPattern =      "1----------------111-----0001011";
        private const string ECallPattern =       "00000000000000000000000001110011";

        private const ulong VirtualDataOffset = 0x2000;
        private const int WideDataRegistersCount = 32;
        private const int WideSpecialPurposeRegistersCount = 8;
        private const int WideDataRegisterWidthInBytes = 32;
        private const int FlagsGroupsCount = 2;
        private const int MaximumStackCapacity = 8;
        private const int MaxLoopStackHeight = 8;

        private enum CustomCSR
        {
            FlagGroup0 = 0x7c0,
            FlagGroup1 = 0x7c1,
            Flags = 0x7c8,
            Mod0 = 0x7d0,
            Mod1 = 0x7d1,
            Mod2 = 0x7d2,
            Mod3 = 0x7d3,
            Mod4 = 0x7d4,
            Mod5 = 0x7d5,
            Mod6 = 0x7d6,
            Mod7 = 0x7d7,
            RndPrefetch = 0x7d8,
            Rnd = 0xfc0,
            URnd = 0xfc1,
        }

        private enum WideSPR
        {
            Mod = 0x0,
            Rnd = 0x1,
            URnd = 0x2,
            Acc = 0x3,
            KeyShare0Low = 0x4,
            KeyShare0High = 0x5,
            KeyShare1Low = 0x6,
            KeyShare1High = 0x7,
        }

        [Flags]
        private enum CustomFlags
        {
            Empty = 0x0,
            Carry = 0x1,
            Msb = 0x2,
            Lsb = 0x4,
            Zero = 0x8,
        }

        private class LoopContext
        {
            public uint StartPC { get; set; }
            public uint EndPC { get; set; }
            public uint NumberOfIterations { get; set; }

            public override string ToString()
            {
                return $"StartPC = 0x{StartPC:X}, EndPC = 0x{EndPC:X}, NumberOfIterations = {NumberOfIterations}";
            }
        }

        private class WideRegister
        {
            public static readonly BigInteger MaxValueMask = ((new BigInteger(1)) << 256) - 1;

            public WideRegister(bool readOnly = false)
            {
                underlyingValue = 0;
                ReadOnly = readOnly;
            }

            public void Clear()
            {
                underlyingValue = 0;
            }

            /* Cuts the BigInteger to 256 bits and sets the register to it */
            public void SetTo(BigInteger bigValue)
            {
                underlyingValue = bigValue & MaxValueMask;
            }

            public void PartialSet(BigInteger value, int byteOffset, int bytesCount)
            {
                var mask = (new BigInteger(0x1) << (bytesCount * 8)) - 1;
                var shiftedValue = (value & mask) << (byteOffset * 8);

                underlyingValue &= ~(mask << (byteOffset * 8));
                underlyingValue |= shiftedValue;
            }

            public BigInteger PartialGet(int byteOffset, int bytesCount)
            {
                var mask = (new BigInteger(0x1) << (bytesCount * 8)) - 1;
                return (underlyingValue >> (byteOffset * 8)) & mask;
            }

            public bool EqualsTo(WideRegister r)
            {
                return r.AsBigInteger.Equals(this.AsBigInteger);
            }

            public bool ReadOnly { get; }

            public BigInteger AsBigInteger => underlyingValue & MaxValueMask;

            public byte[] AsByteArray => underlyingValue.ToByteArray(ByteArrayLength);

            public override string ToString()
            {
                return underlyingValue.ToLongString(ByteArrayLength);
            }

            private BigInteger underlyingValue;

            private readonly int ByteArrayLength = 32;
        }
    }
}
