//
// Copyright (c) 2010-2018 Antmicro
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
                        LLVMInitializeARMDisassembler();
                        LLVMInitializeARMTargetMC();
                        LLVMInitializeARMTargetInfo();
                        LLVMInitializeMipsDisassembler();
                        LLVMInitializeMipsTargetMC();
                        LLVMInitializeMipsTargetInfo();
                        LLVMInitializeX86Disassembler();
                        LLVMInitializeX86TargetMC();
                        LLVMInitializeX86TargetInfo();
                        _llvm_initialized = true;
                    }
                    catch(DllNotFoundException)
                    {
                        throw new RecoverableException("Could not find libLLVM.so. Please check in current output directory.");
                    }
                }
            }
            context = LLVMCreateDisasmCPU(triple, cpu, IntPtr.Zero, 0, new LLVMOpInfoCallback(OpInfoCallback), new LLVMSymbolLookupCallback(SymbolLoopbackCallback));
            if(context == IntPtr.Zero)
            {
                throw new ArgumentOutOfRangeException("cpu", "CPU or triple name not detected by LLVM. Disassembling will not be possible.");
            }
            isThumb = triple.Contains("thumb");


            switch(triple)
            {
            case "i386":
                HexFormatter = FormatHexForx86;
                break;
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
                var bytes = LLVMDisasmInstruction(context, data, sz, pc & 0xFFFFFFFF, strBuf, 1024);
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
                    strBldr.Append(" ").AppendLine(Marshal.PtrToStringAuto(strBuf));
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
                LLVMDisasmDispose(context);
            }
        }

        #endregion

        private static int OpInfoCallback(IntPtr disInfo, UInt64 pc, UInt64 offset, UInt64 size, int tagType, IntPtr tagBuf)
        {
            var OpInfo = new LLVMOpInfo1(tagBuf);
            OpInfo.AddSymbol.Present = 0;
            OpInfo.SubtractSymbol.Present = 0;
            OpInfo.VariantKind = LLVMDisassembler_VariantKind_None;
            return 0;
        }

        private static IntPtr SymbolLoopbackCallback(IntPtr sisInfo, UInt64 refVal, ref IntPtr refType, UInt64 refPC, IntPtr refName)
        {
            refType = LLVMDisassembler_ReferenceType_InOut_None;
            return IntPtr.Zero;
        }

        private readonly Func<StringBuilder, int, int, byte[], bool> HexFormatter;

        private readonly bool isThumb;

        private static bool _llvm_initialized = false;
        private static readonly object _init_locker = new object();

        [DllImport("libLLVM")]
        private static extern int LLVMDisasmInstruction(IntPtr dc, IntPtr bytes, UInt64 bytesSize, UInt64 PC, IntPtr outString, UInt32 outStringSize);

        [DllImport("libLLVM")]
        private static extern IntPtr LLVMCreateDisasmCPU(string tripleName, string cpu, IntPtr disInfo, int tagType, LLVMOpInfoCallback getOpInfo, LLVMSymbolLookupCallback symbolLookUp);

        [DllImport("libLLVM")]
        private static extern void LLVMDisasmDispose(IntPtr disasm);

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeARMDisassembler();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeARMTargetMC();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeARMTargetInfo();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeMipsDisassembler();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeMipsTargetMC();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeMipsTargetInfo();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeX86Disassembler();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeX86TargetMC();

        [DllImport("libLLVM")]
        private static extern void LLVMInitializeX86TargetInfo();

        private static readonly ulong LLVMDisassembler_VariantKind_None = 0;

        private static readonly IntPtr LLVMDisassembler_ReferenceType_InOut_None = IntPtr.Zero;

        private readonly IntPtr context;

        private class LLVMOpInfo1
        {
            private readonly IntPtr Ptr;

            private readonly LLVMOpInfoSymbol1 addSymbol;
            public LLVMOpInfoSymbol1 AddSymbol { get { return addSymbol; } }

            private readonly LLVMOpInfoSymbol1 subtractSymbol;
            public LLVMOpInfoSymbol1 SubtractSymbol { get { return subtractSymbol; } }

            public UInt64 Value
            {
                get { return MarshalExtensions.ReadUInt64(Ptr, 2 * LLVMOpInfoSymbol1.Size); }
                set { MarshalExtensions.WriteUInt64(Ptr, 2 * LLVMOpInfoSymbol1.Size, value); }
            }

            public UInt64 VariantKind
            {
                get { return MarshalExtensions.ReadUInt64(Ptr, 2 * LLVMOpInfoSymbol1.Size + 8); }
                set { MarshalExtensions.WriteUInt64(Ptr, 2 * LLVMOpInfoSymbol1.Size + 8, value); }
            }

            public LLVMOpInfo1(IntPtr ptr)
            {
                Ptr = ptr;
                addSymbol = new LLVMOpInfoSymbol1(ptr);
                subtractSymbol = new LLVMOpInfoSymbol1(ptr + LLVMOpInfoSymbol1.Size);
            }
        }

        private class LLVMOpInfoSymbol1
        {
            private readonly IntPtr Ptr;

            public UInt64 Present
            {
                get { return MarshalExtensions.ReadUInt64(Ptr, 0); }
                set { MarshalExtensions.WriteUInt64(Ptr, 0, value); }
            }

            public IntPtr Name
            {
                get { return Marshal.ReadIntPtr(Ptr, 8); }
            }

            public UInt64 Value
            {
                get { return MarshalExtensions.ReadUInt64(Ptr, 8 + IntPtr.Size); }
                set { MarshalExtensions.WriteUInt64(Ptr, 8 + IntPtr.Size, value); }
            }

            public static int Size { get { return 16 + IntPtr.Size; } }

            public LLVMOpInfoSymbol1(IntPtr ptr)
            {
                Ptr = ptr;
            }
        }

        private delegate int LLVMOpInfoCallback(IntPtr disInfo, UInt64 pc, UInt64 offset, UInt64 size, int tagType, IntPtr tagBuf);

        private delegate IntPtr LLVMSymbolLookupCallback(IntPtr disInfo, UInt64 referenceValue, ref IntPtr referenceType, UInt64 referencePC, IntPtr referenceName);
    }

    public static class MarshalExtensions
    {
        public static UInt64 ReadUInt64(IntPtr ptr, int offset)
        {
            var bytes = new byte[8];
            Marshal.Copy(ptr + offset, bytes, 0, 8);

            return BitConverter.ToUInt64(bytes, 0);
        }

        public static void WriteUInt64(IntPtr ptr, int offset, ulong value)
        {
            var bytes = BitConverter.GetBytes(value);
            Marshal.Copy(bytes, 0, ptr + offset, 8);
        }
    }
}
