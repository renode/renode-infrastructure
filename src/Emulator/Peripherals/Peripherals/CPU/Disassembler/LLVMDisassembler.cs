//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using ELFSharp.ELF;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public class LLVMDisassembler
    {
        public static void ValidateTriple(ICPUSupportingLLVMDisas cpu, ref string triple)
        {
            if(triple == null)
            {
                triple = cpu.AllLLVMTriples[0];
                return;
            }
            if(Array.IndexOf(cpu.AllLLVMTriples, triple) == -1)
            {
                throw new RecoverableException($"Invalid triple {triple} for CPU. Supported triples are: {String.Join(", ", cpu.AllLLVMTriples)}");
            }
        }

        public LLVMDisassembler(ICPUSupportingLLVMDisas cpu)
        {
            this.cpu = cpu;
            cache = new Dictionary<string, IFlaglessDisassembler>();
        }

        public bool TryDisassembleInstruction(ulong pc, byte[] data, uint flags, bool alternateDialect, out DisassemblyResult result, int memoryOffset = 0)
        {
            return GetDisassembler(flags, alternateDialect).TryDisassembleInstruction(pc, data, out result, memoryOffset);
        }

        public bool TryDecodeInstruction(ulong pc, byte[] memory, uint flags, out byte[] opcode, int memoryOffset = 0)
        {
            return GetDisassembler(flags, false).TryDecodeInstruction(pc, memory, out opcode, memoryOffset);
        }

        public int DisassembleBlock(ulong pc, byte[] memory, string triple, bool alternateDialect, out string text)
        {
            var disas = GetDisassembler(triple, alternateDialect);
            return DisassembleBlockInner(disas, pc, memory, out text);
        }

        public int DisassembleBlock(ulong pc, byte[] memory, uint flags, bool alternateDialect, out string text)
        {
            var disas = GetDisassembler(flags, alternateDialect);
            return DisassembleBlockInner(disas, pc, memory, out text);
        }

        private static bool xtensaSupportWarningIssued = false;

        private int DisassembleBlockInner(IFlaglessDisassembler disas, ulong pc, byte[] memory, out string text)
        {
            var sofar = 0;
            var strBldr = new StringBuilder();

            while(sofar < (int)memory.Length)
            {
                if(!disas.TryDisassembleInstruction(pc, memory, out var result, memoryOffset: sofar))
                {
                    strBldr.AppendFormat("Disassembly error detected. The rest of the output ({0}) will be truncated.", memory.Skip(sofar).ToLazyHexString());
                    break;
                }

                if(result.OpcodeSize == 0)
                {
                    strBldr.AppendFormat("0x{0:x8}:  ", pc).AppendLine("No valid instruction, disassembling stopped.");
                    break;
                }
                else
                {
                    strBldr.AppendLine(result.ToString());
                }

                sofar += result.OpcodeSize;
                pc += (ulong)result.OpcodeSize;
            }

            text = strBldr.ToString();
            return sofar;
        }

        private IFlaglessDisassembler GetDisassembler(string triple, bool alternateDialect)
        {
            ValidateTriple(cpu, ref triple);
            var model = cpu.LLVMModel;

            var key = $"{triple} {model} {alternateDialect} {cpu.DisassemblyHexFormatting}";
            if(!cache.ContainsKey(key))
            {
                IFlaglessDisassembler disas = new LLVMDisasWrapper(model, triple, alternateDialect, cpu.DisassemblyHexFormatting);
                Logger.Info($"Created new disassembler for triple {triple}, cpu {model}{(alternateDialect ? " with alternate dialect" : "")}");
                if(!xtensaSupportWarningIssued && triple == "xtensa")
                {
                    Logger.Log(LogLevel.Warning, "The disassembler for Xtensa is currently an experimental feature in Renode");
                    xtensaSupportWarningIssued = true;
                }
                if(cpu.Architecture == "arm-m")
                {
                    disas = new CortexMDisassemblerWrapper(disas);
                }
                else if(cpu.Architecture == "riscv" || cpu.Architecture == "riscv64")
                {
                    disas = new RiscVDisassemblerWrapper(disas);
                }

                cache.Add(key, disas);
            }

            return cache[key];
        }

        private IFlaglessDisassembler GetDisassembler(uint translationFlags, bool alternateDialect)
        {
            var triple = cpu.GetLLVMTriple(translationFlags);
            return GetDisassembler(triple, alternateDialect);
        }

        private readonly Dictionary<string, IFlaglessDisassembler> cache;
        private readonly ICPUSupportingLLVMDisas cpu;

        private class LLVMDisasWrapper : IDisposable, IFlaglessDisassembler
        {
            public LLVMDisasWrapper(string cpu, string triple, bool alternateDialect, Endianess hexFormatting)
            {
                try
                {
                    context = llvm_create_disasm_cpu_with_flags(triple, cpu, alternateDialect ? 1 : 0u);
                }
                catch(DllNotFoundException)
                {
                    throw new RecoverableException("Could not find libllvm-disas. Please check in current output directory.");
                }
                catch(EntryPointNotFoundException)
                {
                    context = llvm_create_disasm_cpu(triple, cpu);
                    Logger.Warning("Old version of libllvm-disas is in use, unable to specify disassembly flags");
                }
                if(context == IntPtr.Zero)
                {
                    throw new ArgumentOutOfRangeException("cpu", "CPU or triple name not detected by LLVM. Disassembling will not be possible.");
                }
                isThumb = triple.Contains("thumb");

                HexEndianess = hexFormatting;
            }

            public void Dispose()
            {
                if(context != IntPtr.Zero)
                {
                    llvm_disasm_dispose(context);
                }
            }

            public bool TryDisassembleInstruction(ulong pc, byte[] data, out DisassemblyResult result, int memoryOffset = 0)
            {
                var strBuf = Marshal.AllocHGlobal(1024);
                var marshalledData = Marshal.AllocHGlobal(data.Length - memoryOffset);

                Marshal.Copy(data, memoryOffset, marshalledData, data.Length - memoryOffset);

                var bytes = llvm_disasm_instruction(context, marshalledData, (ulong)(data.Length - memoryOffset), strBuf, 1024);
                if(bytes == 0)
                {
                    result = default(DisassemblyResult);
                    return false;
                }

                var strBldr = new StringBuilder();
                if(!FormatHex(strBldr, bytes, memoryOffset, data))
                {
                    result = default(DisassemblyResult);
                    return false;
                }

                result = new DisassemblyResult
                {
                    PC = pc,
                    OpcodeSize = bytes,
                    OpcodeString = strBldr.ToString(),
                    DisassemblyString = Marshal.PtrToStringAnsi(strBuf)
                };

                Marshal.FreeHGlobal(strBuf);
                Marshal.FreeHGlobal(marshalledData);
                return true;
            }

            public bool TryDecodeInstruction(ulong pc, byte[] memory, out byte[] opcode, int memoryOffset = 0)
            {
                if(!TryDisassembleInstruction(pc, memory, out var result, memoryOffset))
                {
                    opcode = new byte[0];
                    return false;
                }

                opcode = new byte[result.OpcodeSize];
                Array.Copy(memory, memoryOffset, opcode, 0, result.OpcodeSize);
                return true;
            }

            [DllImport("libllvm-disas")]
            private static extern int llvm_disasm_instruction(IntPtr dc, IntPtr bytes, UInt64 bytesSize, IntPtr outString, UInt32 outStringSize);

            [DllImport("libllvm-disas")]
            private static extern IntPtr llvm_create_disasm_cpu_with_flags(string tripleName, string cpu, uint flags);

            // Fallback in case a new version of Renode is used with an old version of libllvm-disas
            [DllImport("libllvm-disas")]
            private static extern IntPtr llvm_create_disasm_cpu(string tripleName, string cpu);

            [DllImport("libllvm-disas")]
            private static extern void llvm_disasm_dispose(IntPtr disasm);

            private bool FormatHex(StringBuilder strBldr, int bytes, int position, byte[] data)
            {
                if(isThumb && bytes == 4)
                {
                    return FormatHex(strBldr, 2, position, data) && FormatHex(strBldr, 2, position + 2, data);
                }
                if(position > data.Length - bytes) return false;

                for(int offset = 0; offset < bytes; offset += 1)
                {
                    var bytePos = HexEndianess == Endianess.BigEndian ? offset : bytes - offset - 1;
                    strBldr.AppendFormat("{0:x2}", data[position + bytePos]);
                }

                return true;
            }

            private readonly Endianess HexEndianess;

            private readonly bool isThumb;

            private readonly IntPtr context;
        }

        private class CortexMDisassemblerWrapper : IFlaglessDisassembler
        {
            public CortexMDisassemblerWrapper(IFlaglessDisassembler actualDisassembler)
            {
                underlyingDisassembler = actualDisassembler;
            }

            public bool TryDisassembleInstruction(ulong pc, byte[] memory, out DisassemblyResult result, int memoryOffset = 0)
            {
                switch(pc)
                {
                case 0xFFFFFFF0:
                case 0xFFFFFFF1:
                    // Return to Handler mode, exception return uses non-floating-point state from the MSP and execution uses MSP after return.
                    result = new DisassemblyResult
                    {
                        PC = pc,
                        OpcodeSize = 4,
                        OpcodeString = pc.ToString("X"),
                        DisassemblyString = "Handler mode: non-floating-point state, MSP/MSP"
                    };
                    return true;
                case 0xFFFFFFF8:
                case 0xFFFFFFF9:
                    // Return to Thread mode, exception return uses non-floating-point state from the MSP and execution uses MSP after return.
                    result = new DisassemblyResult
                    {
                        PC = pc,
                        OpcodeSize = 4,
                        OpcodeString = pc.ToString("X"),
                        DisassemblyString = "Thread mode: non-floating-point state, MSP/MSP"
                    };
                    return true;
                case 0xFFFFFFFC:
                case 0xFFFFFFFD:
                    // Return to Thread mode, exception return uses non-floating-point state from the PSP and execution uses PSP after return.
                    result = new DisassemblyResult
                    {
                        PC = pc,
                        OpcodeSize = 4,
                        OpcodeString = pc.ToString("X"),
                        DisassemblyString = "Thread mode: non-floating-point state, PSP/PSP"
                    };
                    return true;
                case 0xFFFFFFE0:
                case 0xFFFFFFE1:
                    // Return to Handler mode, exception return uses floating-point state from the MSP and execution uses MSP after return.
                    result = new DisassemblyResult
                    {
                        PC = pc,
                        OpcodeSize = 4,
                        OpcodeString = pc.ToString("X"),
                        DisassemblyString = "Handler mode: floating-point state, MSP/MSP"
                    };
                    return true;
                case 0xFFFFFFE8:
                case 0xFFFFFFE9:
                    // Return to Thread mode, exception return uses floating-point state from the MSP and execution uses MSP after return.
                    result = new DisassemblyResult
                    {
                        PC = pc,
                        OpcodeSize = 4,
                        OpcodeString = pc.ToString("X"),
                        DisassemblyString = "Thread mode: floating-point state, MSP/MSP"
                    };
                    return true;
                case 0xFFFFFFEC:
                case 0xFFFFFFED:
                    // Return to Thread mode, exception return uses floating-point state from the PSP and execution uses PSP after return.
                    result = new DisassemblyResult
                    {
                        PC = pc,
                        OpcodeSize = 4,
                        OpcodeString = pc.ToString("X"),
                        DisassemblyString = "Thread mode: floating-point state, PSP/PSP"
                    };
                    return true;
                default:
                    return underlyingDisassembler.TryDisassembleInstruction(pc, memory, out result, memoryOffset);
                }
            }

            public bool TryDecodeInstruction(ulong pc, byte[] memory, out byte[] opcode, int memoryOffset = 0)
            {
                if(!TryDisassembleInstruction(pc, memory, out var result, memoryOffset))
                {
                    opcode = new byte[0];
                    return false;
                }

                opcode = new byte[result.OpcodeSize];
                Array.Copy(memory, memoryOffset, opcode, 0, result.OpcodeSize);
                return true;
            }

            private readonly IFlaglessDisassembler underlyingDisassembler;
        }

        private class RiscVDisassemblerWrapper : IFlaglessDisassembler
        {
            public RiscVDisassemblerWrapper(IFlaglessDisassembler actualDisassembler)
            {
                underlyingDisassembler = actualDisassembler;
            }

            public bool TryDecodeInstruction(ulong pc, byte[] memory, out byte[] opcode, int memoryOffset = 0)
            {
                var opcodeLength = DecodeRiscVOpcodeLength(memory, memoryOffset);
                if(opcodeLength == 0)
                {
                    opcode = new byte[0];
                    return false;
                }

                opcode = new byte[opcodeLength];
                Array.Copy(memory, memoryOffset, opcode, 0, opcodeLength);
                return true;
            }

            public bool TryDisassembleInstruction(ulong pc, byte[] data, out DisassemblyResult result, int memoryOffset = 0)
            {
                return underlyingDisassembler.TryDisassembleInstruction(pc, data, out result, memoryOffset);
            }

            private int DecodeRiscVOpcodeLength(byte[] memory, int memoryOffset)
            {
                var lengthEncoder = memory[memoryOffset] & 0x7F;
                if(lengthEncoder == 0x7F)
                {
                    // opcodes longer than 64-bits - currently not supported
                    return 0;
                }

                lengthEncoder &= 0x3F;
                if(lengthEncoder == 0x3F)
                {
                    return 8;
                }
                else if(lengthEncoder == 0x1F)
                {
                    return 3;
                }
                else if((lengthEncoder & 0x3) == 0x3)
                {
                    return 4;
                }
                else
                {
                    return 2;
                }
            }

            private readonly IFlaglessDisassembler underlyingDisassembler;
        }
    }
}