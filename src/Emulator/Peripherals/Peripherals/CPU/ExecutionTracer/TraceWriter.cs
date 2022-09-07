//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU.Disassembler;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract class TraceWriter : IDisposable
    {
        public TraceWriter(TranslationCPU cpu, string path, TraceFormat format, bool compress)
        {
            AttachedCPU = cpu;
            this.format = format;

            try
            {
                stream = File.Open(path, FileMode.CreateNew);
                if(compress)
                {
                    stream = new GZipStream(stream, CompressionLevel.Fastest);
                }
            }
            catch(Exception e)
            {
                throw new RecoverableException($"There was an error when preparing the execution trace output file {path}: {e.Message}");
            }
        }

        public abstract void Write(ExecutionTracer.Block block);

        public virtual void WriteHeader() { }

        public virtual void Flush() { }

        public TranslationCPU AttachedCPU { get; }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if(disposed)
            {
                return;
            }

            if(disposing)
            {
                Flush();
                stream?.Dispose();
            }
            disposed = true;
        }

        protected readonly TraceFormat format;
        protected readonly Stream stream;

        protected const int MaxOpcodeBytes = 16;

        private bool disposed;
    }

    public class TraceBinaryWriter : TraceWriter
    {
        public static IReadOnlyList<TraceFormat> SupportedFormats = new List<TraceFormat>
        {
            TraceFormat.PC,
            TraceFormat.Opcode,
            TraceFormat.PCAndOpcode
        }.AsReadOnly();

        public TraceBinaryWriter(TranslationCPU cpu, string path, TraceFormat format, bool compress)
            : base(cpu, path, format, compress)
        {
            this.pcWidth = this.format == TraceFormat.Opcode ? 0 : (int)(cpu.PC.Bits + 7) / 8;

            cache = new LRUCache<uint, byte[]>(CacheSize);
            buffer = new byte[BufferSize];
        }

        public override void WriteHeader()
        {
            stream.Write(Encoding.ASCII.GetBytes(FormatSignature), 0, Encoding.ASCII.GetByteCount(FormatSignature));
            stream.WriteByte(FormatVersion);
            stream.WriteByte((byte)pcWidth);
            stream.WriteByte((byte)(format != TraceFormat.PC ? 1 : 0));
        }

        public override void Write(ExecutionTracer.Block block)
        {
            var pc = block.FirstInstructionPC;
            var counter = 0;

            while(counter < (int)block.InstructionsCount)
            {
                if(!TryReadAndDecodeInstruction(pc, block.DisassemblyFlags, out var opcode))
                {
                    break;
                }

                if(pcWidth > 0)
                {
                    BitHelper.GetBytesFromValue(buffer, bufferPosition, pc, pcWidth, true);
                    bufferPosition += pcWidth;
                }
                if(format != TraceFormat.PC)
                {
                    buffer[bufferPosition] = (byte)opcode.Length;
                    bufferPosition += 1;
                    opcode.CopyTo(buffer, bufferPosition);
                    bufferPosition += opcode.Length;
                }

                pc += (ulong)opcode.Length;
                counter++;

                if(bufferPosition + pcWidth + (format != TraceFormat.PC ? Byte.MaxValue + 1 : 0) >= buffer.Length)
                {
                    Flush();
                }
            }
        }

        public override void Flush()
        {
            stream.Write(buffer, 0, bufferPosition);
            stream.Flush();
            bufferPosition = 0;
        }

        private bool TryReadAndDecodeInstruction(ulong pc, uint flags, out byte[] opcode)
        {
            // here we read only 4-bytes as it should cover most cases
            var key = AttachedCPU.Bus.ReadDoubleWord(pc);
            if(!cache.TryGetValue(key, out opcode))
            {
                // here we are prepared for longer opcodes
                var mem = AttachedCPU.Bus.ReadBytes(pc, MaxOpcodeBytes, context: AttachedCPU);
                if(!AttachedCPU.Disassembler.TryDecodeInstruction(pc, mem, flags, out var decodedOpcode))
                {
                    opcode = new byte[0] { };
                    // mark this as an invalid opcode
                    cache.Add(key, opcode);
                }
                else
                {
                    opcode = decodedOpcode;
                    if(opcode.Length <= 4)
                    {
                        cache.Add(key, opcode);
                    }
                }
            }

            if(opcode.Length == 0)
            {
                AttachedCPU.Log(LogLevel.Warning, "ExecutionTracer: Couldn't disassemble opcode at PC 0x{0:X}\n", pc);
                return false;
            }

            return true;
        }

        private int bufferPosition;

        private readonly LRUCache<uint, byte[]> cache;

        private readonly byte[] buffer;
        private readonly int pcWidth;

        private const string FormatSignature = "ReTrace";
        private const byte FormatVersion = 1;

        private const int CacheSize = 100000;
        private const int BufferSize = 10000;
    }

    public class TraceTextWriter : TraceWriter
    {
        public static IReadOnlyList<TraceFormat> SupportedFormats = new List<TraceFormat>
        {
            TraceFormat.PC,
            TraceFormat.Opcode,
            TraceFormat.PCAndOpcode,
            TraceFormat.Disassembly
        }.AsReadOnly();

        public TraceTextWriter(TranslationCPU cpu, string path, TraceFormat format, bool compress)
            : base(cpu, path, format, compress)
        {
            cache = new LRUCache<uint, Disassembler.DisassemblyResult>(CacheSize);
            stringBuilder = new StringBuilder();
            textWriter = new StreamWriter(stream, Encoding.ASCII);
        }

        public override void Write(ExecutionTracer.Block block)
        {
            var pc = block.FirstInstructionPC;
            var counter = 0;

            while(counter < (int)block.InstructionsCount)
            {
                if(!TryReadAndDisassembleInstruction(pc, block.DisassemblyFlags, out var result))
                {
                    stringBuilder.AppendFormat("Couldn't disassemble opcode at PC 0x{0:X}\n", pc);
                    break;
                }
                else
                {
                    switch(format)
                    {
                        case TraceFormat.PC:
                            stringBuilder.AppendFormat("0x{0:X}\n", result.PC);
                            break;

                        case TraceFormat.Opcode:
                            stringBuilder.AppendFormat("0x{0}\n", result.OpcodeString.ToUpper());
                            break;

                        case TraceFormat.PCAndOpcode:
                            stringBuilder.AppendFormat("0x{0:X}: 0x{1}\n", result.PC, result.OpcodeString.ToUpper());
                            break;

                        case TraceFormat.Disassembly:
                            var symbol = AttachedCPU.Bus.FindSymbolAt(pc);
                            var disassembly = result.ToString().Replace("\t", " ");
                            if(symbol != null)
                            {
                                stringBuilder.AppendFormat("{0, -60} [{1}]\n", disassembly, symbol);
                            }
                            else
                            {
                                stringBuilder.AppendFormat("{0}\n", disassembly);
                            }
                            break;
                    }

                    pc += (ulong)result.OpcodeSize;
                    counter++;
                }

                if(stringBuilder.Length > BufferFlushLevel)
                {
                    Flush();
                }
            }
        }

        public override void Flush()
        {
            textWriter.Write(stringBuilder);
            textWriter.Flush();
            stream.Flush();
            stringBuilder.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if(disposed)
            {
                return;
            }

            if(disposing)
            {
                Flush();
                textWriter?.Dispose();
                stream?.Dispose();
            }
            disposed = true;
        }

        private bool TryReadAndDisassembleInstruction(ulong pc, uint flags, out DisassemblyResult result)
        {
            // here we read only 4-bytes as it should cover most cases
            var key = AttachedCPU.Bus.ReadDoubleWord(pc);
            if(!cache.TryGetValue(key, out result))
            {
                // here we are prepared for longer opcodes
                var mem = AttachedCPU.Bus.ReadBytes(pc, MaxOpcodeBytes, context: AttachedCPU);
                if(!AttachedCPU.Disassembler.TryDisassembleInstruction(pc, mem, flags, out result))
                {
                    result = new DisassemblyResult();
                    // mark this as an invalid opcode
                    cache.Add(key, result);
                }
                else
                {
                    if(result.OpcodeSize <= 4)
                    {
                        cache.Add(key, result);
                    }
                }
            }

            if(result.OpcodeSize == 0)
            {
                var message = $"Couldn't disassemble opcode at PC 0x{pc:X}\n";
                AttachedCPU.Log(LogLevel.Warning, "ExecutionTracer: couldn't disassemble opcode at PC 0x{0:X}", pc);
                return false;
            }

            result.PC = pc;
            return true;
        }

        private bool disposed;

        private readonly TextWriter textWriter;
        private readonly LRUCache<uint, Disassembler.DisassemblyResult> cache;
        private readonly StringBuilder stringBuilder;

        private const int CacheSize = 100000;
        private const int BufferFlushLevel = 1000000;
    }
}