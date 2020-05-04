//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;
using System.Text;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Disassembler.LLVM
{
    public class LLVMDisasWrapper : IDisposable
    {
        public LLVMDisasWrapper(string cpu, string triple)
        {
            lock(_init_locker)
            {
                if(!_llvm_initialized)
                {
                    try
                    {
                        llvm_disasm_ARM_init();
                        llvm_disasm_PowerPC_init();
                        llvm_disasm_Sparc_init();
                        llvm_disasm_RISCV_init();
                        llvm_disasm_X86_init();
                        _llvm_initialized = true;
                    }
                    catch(DllNotFoundException)
                    {
                        throw new RecoverableException("Could not find libllvm-disas. Please check in current output directory.");
                    }
                }
            }
            context = llvm_create_disasm_cpu(triple, cpu);
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

        public int Disassemble(IntPtr data, UInt64 sz, UInt64 pc, IntPtr buf, UInt32 bufSz)
        {
            var sofar = 0;
            var strBuf = Marshal.AllocHGlobal(1024);
            var strBldr = new StringBuilder();

            var dataBytes = new byte[sz];
            Marshal.Copy(data, dataBytes, 0, dataBytes.Length);

            while(sofar < (int)sz)
            {
                var bytes = llvm_disasm_instruction(context, data, sz, strBuf, 1024);
                if(bytes == 0)
                {
                    strBldr.AppendFormat("0x{0:x8}:  ", pc).AppendLine("No valid instruction, disassembling stopped.");
                    break;
                }
                else
                {
                    strBldr.AppendFormat("0x{0:x8}:  ", pc);
                    if(!HexFormatter(strBldr, bytes, sofar, dataBytes))
                    {
                        strBldr.AppendLine("Disassembly error detected. The rest of the output will be truncated.");
                        break;
                    }
                    strBldr.Append(" ").AppendLine(Marshal.PtrToStringAnsi(strBuf));
                }

                sofar += bytes;
                pc += (ulong)bytes;
                data += bytes;
            }

            Marshal.FreeHGlobal(strBuf);
            var sstr = Encoding.ASCII.GetBytes(strBldr.ToString());
            Marshal.Copy(sstr, 0, buf, (int)Math.Min(bufSz, sstr.Length));
            Marshal.Copy(new[] { 0 }, 0, buf + (int)Math.Min(bufSz - 1, sstr.Length), 1);

            return sofar;
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
                    strBldr.AppendFormat("{0:x2}", data[position + i]);
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

        private static bool _llvm_initialized = false;
        private static readonly object _init_locker = new object();

        [DllImport("libllvm-disas")]
        private static extern int llvm_disasm_instruction(IntPtr dc, IntPtr bytes, UInt64 bytesSize, IntPtr outString, UInt32 outStringSize);

        [DllImport("libllvm-disas")]
        private static extern IntPtr llvm_create_disasm_cpu(string tripleName, string cpu);

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_dispose(IntPtr disasm);

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_AArch64_init();

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_ARM_init();

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_Mips_init();

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_PowerPC_init();

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_RISCV_init();

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_Sparc_init();

        [DllImport("libllvm-disas")]
        private static extern void llvm_disasm_X86_init();

        private readonly IntPtr context;
    }
}
