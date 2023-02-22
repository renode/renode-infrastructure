//
// Copyright (c) 2010-2023 Antmicro
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

        public virtual void FlushBuffer() { }

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
                FlushBuffer();
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
            stream.WriteByte((byte)(IncludeOpcode ? 1 : 0));
            if(IncludeOpcode)
            {
                AttachedCPU.Disassembler.GetTripleAndModelKey(0, out var triple, out var model);
                var tripleAndModelString = $"{triple} {model}";
                usesThumbFlag = tripleAndModelString.Contains("armv7a");
                var byteCount = Encoding.ASCII.GetByteCount(tripleAndModelString);

                stream.WriteByte((byte)(usesThumbFlag ? 1 : 0));
                stream.WriteByte((byte)byteCount);
                stream.Write(Encoding.ASCII.GetBytes(tripleAndModelString), 0, byteCount);
            }
        }

        public override void Write(ExecutionTracer.Block block)
        {
            var pc = block.FirstInstructionPC;
            var counter = 0u;
    
            var hasAdditionalData = block.AdditionalDataInTheBlock.TryDequeue(out var insnAdditionalData);

            if(usesThumbFlag)
            {
                // if the CPU supports thumb, we need to mark translation blocks 
                // with a flag and length of the block, that is needed to properly disassemble the trace
                var isThumb = block.DisassemblyFlags > 0;
                WriteByteToBuffer((byte)(isThumb ? 1 : 0));
                WriteInstructionsCountToBuffer(block.InstructionsCount);
            }
                                            
            while(counter < block.InstructionsCount)
            {
                if(!TryReadAndDecodeInstruction(pc, block.DisassemblyFlags, out var opcode))
                {
                    break;
                }

                if(IncludePC)
                {
                    WritePCToBuffer(pc);
                }
                if(IncludeOpcode)
                {
                    WriteByteToBuffer((byte)opcode.Length);
                    WriteBytesToBuffer(opcode);
                }
                while(hasAdditionalData && insnAdditionalData.PC == pc)
                {
                    WriteByteToBuffer((byte)insnAdditionalData.Type);
                    WriteBytesToBuffer(insnAdditionalData.GetBinaryRepresentation());
                    hasAdditionalData = block.AdditionalDataInTheBlock.TryDequeue(out insnAdditionalData);
                }
                WriteByteToBuffer((byte)AdditionalDataType.None);

                pc += (ulong)opcode.Length;
                counter++;

                if(bufferPosition >= BufferFlushLevel)
                {
                    FlushBuffer();
                }
            }
        }

        private bool usesThumbFlag;

        private bool IncludeOpcode => this.format != TraceFormat.PC;

        private bool IncludePC => this.format != TraceFormat.Opcode;

        private void WriteBytesToBuffer(byte[] data)
        {
            Buffer.BlockCopy(data, 0, buffer, bufferPosition, data.Length);
            bufferPosition += data.Length;
        }

        private void WriteByteToBuffer(byte data)
        {
            buffer[bufferPosition] = data;
            bufferPosition += 1;
        }

        private void WritePCToBuffer(ulong pc)
        {
            BitHelper.GetBytesFromValue(buffer, bufferPosition, pc, pcWidth, true);
            bufferPosition += pcWidth;
        }

        private void WriteInstructionsCountToBuffer(ulong count)
        {
            BitHelper.GetBytesFromValue(buffer, bufferPosition, count, 8, true);
            bufferPosition += 8;
        }

        public override void FlushBuffer()
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
        private const byte FormatVersion = 2;

        private const int CacheSize = 100000;
        private const int BufferSize = 10000;
        private const int BufferFlushLevel = 9000;
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
            var hasAdditionalData = block.AdditionalDataInTheBlock.TryDequeue(out var nextAdditionalData);

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
                    while(hasAdditionalData && (nextAdditionalData.PC == result.PC))
                    {
                        stringBuilder.AppendFormat("{0}\n", nextAdditionalData.GetStringRepresentation());
                        hasAdditionalData = block.AdditionalDataInTheBlock.TryDequeue(out nextAdditionalData);
                    }
                    pc += (ulong)result.OpcodeSize;
                    counter++;
                }
                FlushIfNecessary();
            }
        }

        public override void FlushBuffer()
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
                FlushBuffer();
                textWriter?.Dispose();
                stream?.Dispose();
            }
            disposed = true;
        }

        private void FlushIfNecessary()
        {
            if(stringBuilder.Length > BufferFlushLevel)
            {
                FlushBuffer();
            }
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
