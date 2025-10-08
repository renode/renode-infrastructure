//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Text;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
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

        public override void FlushBuffer()
        {
            stream.Write(buffer, 0, bufferPosition);
            stream.Flush();
            bufferPosition = 0;
        }

        public override void WriteHeader()
        {
            stream.Write(Encoding.ASCII.GetBytes(FormatSignature), 0, Encoding.ASCII.GetByteCount(FormatSignature));
            stream.WriteByte(FormatVersion);
            stream.WriteByte((byte)pcWidth);
            stream.WriteByte((byte)(IncludeOpcode ? 1 : 0));
            if(IncludeOpcode)
            {
                var flags = 0u;
                LLVMArchitectureMapping.GetTripleAndModelKey(AttachedCPU, ref flags, out var triple, out var model);
                var tripleAndModelString = $"{triple} {model}";
                usesMultipleInstructionSets = tripleAndModelString.Contains("armv7a") || tripleAndModelString.Contains("arm64");
                var byteCount = Encoding.ASCII.GetByteCount(tripleAndModelString);

                stream.WriteByte((byte)(usesMultipleInstructionSets ? 1 : 0));
                stream.WriteByte((byte)byteCount);
                stream.Write(Encoding.ASCII.GetBytes(tripleAndModelString), 0, byteCount);
            }
        }

        public override void Write(ExecutionTracer.Block block)
        {
            var pc = block.FirstInstructionPC;
            var pcVirtual = block.FirstInstructionVirtualPC;
            var counter = 0u;

            var hasAdditionalData = block.AdditionalDataInTheBlock.TryDequeue(out var insnAdditionalData);

            if(usesMultipleInstructionSets)
            {
                // if the CPU supports multiple instruction sets, we need to mark translation blocks
                // with a flag and length of the block, that is needed to properly disassemble the trace
                var instructionSet = (byte)block.DisassemblyFlags;
                WriteByteToBuffer(instructionSet);
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
                while(hasAdditionalData && insnAdditionalData.PC == pcVirtual)
                {
                    WriteByteToBuffer((byte)insnAdditionalData.Type);
                    WriteBytesToBuffer(insnAdditionalData.GetBinaryRepresentation());
                    hasAdditionalData = block.AdditionalDataInTheBlock.TryDequeue(out insnAdditionalData);
                }
                WriteByteToBuffer((byte)AdditionalDataType.None);

                pc += (ulong)opcode.Length;
                pcVirtual += (ulong)opcode.Length;
                counter++;

                if(bufferPosition >= BufferFlushLevel)
                {
                    FlushBuffer();
                }
            }
        }

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

        private bool TryReadAndDecodeInstruction(ulong pc, uint flags, out byte[] opcode)
        {
            // here we read only 4-bytes as it should cover most cases
            var key = AttachedCPU.Bus.ReadDoubleWord(pc, context: AttachedCPU);
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
                AttachedCPU.Log(LogLevel.Warning, "ExecutionTracer: Couldn't disassemble opcode at PC 0x{0:X}", pc);
                return false;
            }

            return true;
        }

        private bool IncludeOpcode => this.format != TraceFormat.PC;

        private bool IncludePC => this.format != TraceFormat.Opcode;

        private bool usesMultipleInstructionSets;

        private int bufferPosition;

        private readonly LRUCache<uint, byte[]> cache;

        private readonly byte[] buffer;
        private readonly int pcWidth;

        private const string FormatSignature = "ReTrace";
        private const byte FormatVersion = 4;

        private const int CacheSize = 100000;
        private const int BufferSize = 10000;
        private const int BufferFlushLevel = 9000;
    }
}