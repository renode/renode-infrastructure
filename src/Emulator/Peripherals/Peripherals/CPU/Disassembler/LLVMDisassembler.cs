//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU.Disassembler
{
    public class LLVMDisassembler : IDisassembler
    {
        public LLVMDisassembler(ICPU cpu)
        {
            if(!LLVMArchitectureMapping.IsSupported(cpu))
            {
                throw new ArgumentOutOfRangeException("cpu");
            }

            this.cpu = cpu;
            cache = new Dictionary<string, IDisassembler>();
        }

        public bool TryDisassembleInstruction(ulong pc, byte[] data, uint flags, out DisassemblyResult result, int memoryOffset = 0)
        {
            return GetDisassembler(flags).TryDisassembleInstruction(pc, data, flags, out result, memoryOffset);
        }

        public bool TryDecodeInstruction(ulong pc, byte[] memory, uint flags, out byte[] opcode, int memoryOffset = 0)
        {
            return GetDisassembler(flags).TryDecodeInstruction(pc, memory, flags, out opcode, memoryOffset);
        }

        public int DisassembleBlock(ulong pc, byte[] memory, uint flags, out string text)
        {
            var sofar = 0;
            var strBldr = new StringBuilder();

            while(sofar < (int)memory.Length)
            {
                if(!TryDisassembleInstruction(pc, memory, flags, out var result, memoryOffset: sofar))
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

        private IDisassembler GetDisassembler(uint flags)
        {
            LLVMArchitectureMapping.GetTripleAndModelKey(cpu, ref flags, out var triple, out var model);
            var key = $"{triple} {model} {flags}";
            if(!cache.ContainsKey(key))
            {
                IDisassembler disas = new LLVMDisasWrapper(model, triple, flags);
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

        private readonly Dictionary<string, IDisassembler> cache;
        private readonly ICPU cpu;

        private class LLVMDisasWrapper : IDisposable, IDisassembler
        {
            public LLVMDisasWrapper(string cpu, string triple, uint flags)
            {
                try
                {
                    context = llvm_create_disasm_cpu_with_flags(triple, cpu, flags);
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

                switch(triple)
                {
                case "ppc":
                case "ppc64le":
                case "sparc":
                case "i386":
                case "x86_64":
                    HexFormatter = FormatHexForx86;
                    break;
                case "riscv64":
                case "riscv32":
                case "thumb":
                case "arm":
                case "armv7a":
                case "arm64":
                case "msp430":
                case "msp430x":
                    HexFormatter = FormatHexForARM;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("cpu", "CPU not supported.");
                }
            }

            public void Dispose()
            {
                if(context != IntPtr.Zero)
                {
                    llvm_disasm_dispose(context);
                }
            }

            public bool TryDisassembleInstruction(ulong pc, byte[] data, uint flags, out DisassemblyResult result, int memoryOffset = 0)
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
                if(!HexFormatter(strBldr, bytes, memoryOffset, data))
                {
                    result = default(DisassemblyResult);
                    return false;
                }

                result = new DisassemblyResult
                {
                    PC = pc,
                    OpcodeSize = bytes,
                    OpcodeString = strBldr.ToString().Replace(" ", ""),
                    DisassemblyString = Marshal.PtrToStringAnsi(strBuf)
                };

                Marshal.FreeHGlobal(strBuf);
                Marshal.FreeHGlobal(marshalledData);
                return true;
            }

            public bool TryDecodeInstruction(ulong pc, byte[] memory, uint flags, out byte[] opcode, int memoryOffset = 0)
            {
                if(!TryDisassembleInstruction(pc, memory, flags, out var result, memoryOffset))
                {
                    opcode = new byte[0];
                    return false;
                }

                opcode = Misc.HexStringToByteArray(result.OpcodeString.Trim(), true);
                return true;
            }

            private bool FormatHexForx86(StringBuilder strBldr, int bytes, int position, byte[] data)
            {
                int i;
                for(i = 0; i < bytes && position + i < data.Length; i++)
                {
                    strBldr.AppendFormat("{0:x2} ", data[position + i]);
                }

                //This is a sane minimal length, based on some different binaries for quark.
                //X86 instructions do not have the upper limit of lenght, so we have to approximate.
                for(var j = i; j < 7; ++j)
                {
                    strBldr.Append("   ");
                }

                return i == bytes;
            }

            private bool FormatHexForARM(StringBuilder strBldr, int bytes, int position, byte[] data)
            {
                if(isThumb)
                {
                    if(bytes == 4 && position + 3 < data.Length)
                    {
                        strBldr.AppendFormat("{0:x2}{1:x2} {2:x2}{3:x2}", data[position + 1], data[position], data[position + 3], data[position + 2]);
                    }
                    else if(bytes == 2 && position + 1 < data.Length)
                    {
                        strBldr.AppendFormat("{0:x2}{1:x2}     ", data[position + 1], data[position]);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    for(int i = bytes - 1; i >= 0; i--)
                    {
                        if(position + i < data.Length)
                        {
                            strBldr.AppendFormat("{0:x2}", data[position + i]);
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            private readonly Func<StringBuilder, int, int, byte[], bool> HexFormatter;

            private readonly bool isThumb;

            [DllImport("libllvm-disas")]
            private static extern int llvm_disasm_instruction(IntPtr dc, IntPtr bytes, UInt64 bytesSize, IntPtr outString, UInt32 outStringSize);

            [DllImport("libllvm-disas")]
            private static extern IntPtr llvm_create_disasm_cpu_with_flags(string tripleName, string cpu, uint flags);

            // Fallback in case a new version of Renode is used with an old version of libllvm-disas
            [DllImport("libllvm-disas")]
            private static extern IntPtr llvm_create_disasm_cpu(string tripleName, string cpu);

            [DllImport("libllvm-disas")]
            private static extern void llvm_disasm_dispose(IntPtr disasm);

            private readonly IntPtr context;
        }

        private class CortexMDisassemblerWrapper : IDisassembler
        {
            public CortexMDisassemblerWrapper(IDisassembler actualDisassembler)
            {
                underlyingDisassembler = actualDisassembler;
            }

            public bool TryDisassembleInstruction(ulong pc, byte[] memory, uint flags, out DisassemblyResult result, int memoryOffset = 0)
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
                    return underlyingDisassembler.TryDisassembleInstruction(pc, memory, flags, out result, memoryOffset);
                }
            }

            public bool TryDecodeInstruction(ulong pc, byte[] memory, uint flags, out byte[] opcode, int memoryOffset = 0)
            {
                if(!TryDisassembleInstruction(pc, memory, flags, out var result, memoryOffset))
                {
                    opcode = new byte[0];
                    return false;
                }

                opcode = Misc.HexStringToByteArray(result.OpcodeString, true);
                return true;
            }

            private readonly IDisassembler underlyingDisassembler;
        }

        private class RiscVDisassemblerWrapper : IDisassembler
        {
            public RiscVDisassemblerWrapper(IDisassembler actualDisassembler)
            {
                underlyingDisassembler = actualDisassembler;
            }

            public bool TryDecodeInstruction(ulong pc, byte[] memory, uint flags, out byte[] opcode, int memoryOffset = 0)
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

            public bool TryDisassembleInstruction(ulong pc, byte[] data, uint flags, out DisassemblyResult result, int memoryOffset = 0)
            {
                return underlyingDisassembler.TryDisassembleInstruction(pc, data, flags, out result, memoryOffset);
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

            private readonly IDisassembler underlyingDisassembler;
        }
    }
}
