//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.IO.Compression;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using Antmicro.Renode.Utilities;

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

        protected bool TryReadAndDisassembleInstruction(ulong pc, uint flags, out DisassemblyResult result)
        {
            // here we read only 4-bytes as it should cover most cases
            var key = AttachedCPU.Bus.ReadDoubleWord(pc, context: AttachedCPU);
            if(!disassemblyCache.TryGetValue(key, out result))
            {
                // here we are prepared for longer opcodes
                var mem = AttachedCPU.Bus.ReadBytes(pc, MaxOpcodeBytes, context: AttachedCPU);
                if(!AttachedCPU.Disassembler.TryDisassembleInstruction(pc, mem, flags, out result))
                {
                    result = new DisassemblyResult();
                    // mark this as an invalid opcode
                    disassemblyCache.Add(key, result);
                }
                else
                {
                    if(result.OpcodeSize <= 4)
                    {
                        disassemblyCache.Add(key, result);
                    }
                }
            }

            if(result.OpcodeSize == 0)
            {
                AttachedCPU.Log(LogLevel.Warning, "ExecutionTracer: couldn't disassemble opcode at PC 0x{0:X}", pc);
                return false;
            }

            result.PC = pc;
            return true;
        }

        protected readonly TraceFormat format;
        protected readonly Stream stream;
        protected LRUCache<uint, Disassembler.DisassemblyResult> disassemblyCache;

        protected const int MaxOpcodeBytes = 16;

        private bool disposed;
    }
}
