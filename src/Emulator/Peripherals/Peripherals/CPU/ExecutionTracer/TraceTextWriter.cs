//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.IO;
using System.Text;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU.Disassembler;

namespace Antmicro.Renode.Peripherals.CPU
{
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
            disassemblyCache = new LRUCache<uint, Disassembler.DisassemblyResult>(CacheSize);
            stringBuilder = new StringBuilder();
            textWriter = new StreamWriter(stream, Encoding.ASCII);
        }

        public override void Write(ExecutionTracer.Block block)
        {
            var pc = block.FirstInstructionPC;
            var pcVirtual = block.FirstInstructionVirtualPC;
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
                            var symbol = AttachedCPU.Bus.FindSymbolAt(pc, AttachedCPU);
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
                    while(hasAdditionalData && (nextAdditionalData.PC == pcVirtual))
                    {
                        stringBuilder.AppendFormat("{0}\n", nextAdditionalData.GetStringRepresentation());
                        hasAdditionalData = block.AdditionalDataInTheBlock.TryDequeue(out nextAdditionalData);
                    }
                    pc += (ulong)result.OpcodeSize;
                    pcVirtual += (ulong)result.OpcodeSize;
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

        private bool disposed;

        private readonly TextWriter textWriter;
        private readonly StringBuilder stringBuilder;

        private const int CacheSize = 100000;
        private const int BufferFlushLevel = 1000000;
    }
}
