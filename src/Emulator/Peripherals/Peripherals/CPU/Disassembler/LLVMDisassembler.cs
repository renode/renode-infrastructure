//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Disassembler.LLVM
{
    public class LLVMDisassembler : IDisassembler
    {
        public LLVMDisassembler(IDisassemblable cpu)
        {
            if(!SupportedArchitectures.ContainsKey(cpu.Architecture))
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
                    strBldr.AppendLine("Disassembly error detected. The rest of the output will be truncated.");
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

        public void GetTripleAndModelKey(uint flags, out string triple, out string model)
        {
            triple = SupportedArchitectures[cpu.Architecture];
            if(triple == "armv7a" && flags > 0)
            {
                triple = "thumb";
            }

            if(!ModelTranslations.TryGetValue(cpu.Model, out model))
            {
                model = cpu.Model;
            }

            if(model == "cortex-r52")
            {
                triple = "arm";
            }

            // RISC-V extensions Zicsr and Zifencei are not supported in LLVM yet:
            // https://discourse.llvm.org/t/support-for-zicsr-and-zifencei-extensions/68369
            // https://reviews.llvm.org/D143924
            // The LLVM version used by Renode (at the time of adding this logic) is 14.0.0-rc1
            if(model.Contains("_zicsr"))
            {
                model = model.Replace("_zicsr", "");
            }

            if(model.Contains("_zifencei"))
            {
                model = model.Replace("_zifencei", "");
            }
        }
        
        private IDisassembler GetDisassembler(uint flags) 
        {
            GetTripleAndModelKey(flags, out var triple, out var model);
            var key = $"{triple} {model}";
            if(!cache.ContainsKey(key))
            {
                IDisassembler disas = new LLVMDisasWrapper(model, triple);
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
        
        private static readonly Dictionary<string, string> ModelTranslations = new Dictionary<string, string>
        {
            { "x86"       , "i386"       },
            // this case is included because of #3250
            { "arm926"    , "arm926ej-s" },
            { "cortex-m4f", "cortex-m4"  },
            { "e200z6"    , "ppc32"      },
            { "gr716"     , "leon3"      }
        };

        private static readonly Dictionary<string, string> SupportedArchitectures = new Dictionary<string, string>
        {
            { "arm",    "armv7a"    },
            { "arm-m",  "thumb"     },
            { "arm64",  "arm64"     },
            { "mips",   "mipsel"    },
            { "riscv",  "riscv32"   },
            { "riscv64","riscv64"   },
            { "ppc",    "ppc"       },
            { "ppc64",  "ppc64le"   },
            { "sparc",  "sparc"     },
            { "i386",   "i386"      }
        };

        private readonly Dictionary<string, IDisassembler> cache;
        private readonly IDisassemblable cpu;
        
        private class LLVMDisasWrapper : IDisposable, IDisassembler
        {
            public LLVMDisasWrapper(string cpu, string triple)
            {
                try
                {
                    context = llvm_create_disasm_cpu(triple, cpu);
                }
                catch(DllNotFoundException)
                {
                    throw new RecoverableException("Could not find libllvm-disas. Please check in current output directory.");
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
                    HexFormatter = FormatHexForx86;
                    break;
                case "riscv64":
                case "riscv32":
                case "thumb":
                case "arm":
                case "armv7a":
                case "arm64":
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
