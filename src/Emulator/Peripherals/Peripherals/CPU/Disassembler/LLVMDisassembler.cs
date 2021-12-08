//
// Copyright (c) 2010-2021 Antmicro
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

        private IDisassembler GetDisassembler(uint flags) 
        {
            var triple = SupportedArchitectures[cpu.Architecture];
            if(triple == "armv7a" && flags > 0)
            {
                triple = "thumb";
            }

            var key = string.Format("{0} {1}", triple, cpu.Model);
            if(!cache.ContainsKey(key))
            {
                if(!ModelTranslations.TryGetValue(cpu.Model, out var model))
                {
                    model = cpu.Model;
                }

                IDisassembler disas = new LLVMDisasWrapper(model, triple);
                if(cpu.Architecture == "arm-m")
                {
                    disas = new CortexMDisassemblerWrapper(disas);
                }

                cache.Add(key, disas);
            }

            return cache[key];
        }
        
        private static readonly Dictionary<string, string> ModelTranslations = new Dictionary<string, string>
        {
            { "x86"   , "i386"       },
            // this case is included because of #3250
            { "arm926", "arm926ej-s" },
            { "e200z6", "ppc32"      },
            { "gr716" , "leon3"      }
        };

        private static readonly Dictionary<string, string> SupportedArchitectures = new Dictionary<string, string>
        {
            { "arm",    "armv7a"    },
            { "arm-m",  "thumb"     },
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
                    HexFormatter = FormatHexForARM;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("cpu", "CPU not supported.");
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
                    OpcodeString = strBldr.ToString(),
                    DisassemblyString = Marshal.PtrToStringAnsi(strBuf)
                };
                
                Marshal.FreeHGlobal(strBuf);
                Marshal.FreeHGlobal(marshalledData);
                return true;
            }

            #region Hex Formatters

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

            #endregion

            #region IDisposable implementation

            public void Dispose()
            {
                if(context != IntPtr.Zero)
                {
                    llvm_disasm_dispose(context);
                }
            }

            #endregion

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
            
            private readonly IDisassembler underlyingDisassembler;
        }
    }
}
