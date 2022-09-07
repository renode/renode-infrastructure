//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class ExecutionTracerExtensions
    {
        public static void EnableExecutionTracing(this TranslationCPU @this, string fileName, TraceFormat format, bool isBinary = false, bool compress = false)
        {
            var writerBuilder = new TraceWriterBuilder(@this, fileName, format, isBinary, compress);
            var tracer = new ExecutionTracer(@this, writerBuilder);

            try
            {
                // we keep it as external to dispose/flush on quit
                EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal(tracer, $"executionTracer-{@this.GetName()}");
            }
            catch(Exception)
            {
                tracer.Dispose();
                throw new RecoverableException("ExecutionTracer is already running");
            }

            tracer.Start();
        }

        public static void DisableExecutionTracing(this TranslationCPU @this)
        {
            var em = EmulationManager.Instance.CurrentEmulation.ExternalsManager;
            var tracers = em.GetExternalsOfType<ExecutionTracer>().Where(t => t.AttachedCPU == @this).ToList();
            foreach(var tracer in tracers)
            {
                tracer.Stop();
                em.RemoveExternal(tracer);
            }
        }
    }

    public class ExecutionTracer : IDisposable, IExternal
    {
        public ExecutionTracer(TranslationCPU cpu, TraceWriterBuilder traceWriterBuilder)
        {
            AttachedCPU = cpu;
            writerBuilder = traceWriterBuilder;

            AttachedCPU.SetHookAtBlockEnd(HandleBlockEndHook);
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            if(underlyingThread != null)
            {
                throw new RecoverableException("ExecutionTracer is already running");
            }

            writer = writerBuilder.CreateWriter();
            writer.WriteHeader();

            blocks = new BlockingCollection<Block>();

            underlyingThread = new Thread(WriterThreadBody);
            underlyingThread.IsBackground = true;
            underlyingThread.Name = "Execution tracer worker";
            underlyingThread.Start();

            wasStarted = true;
            wasStoped = false;
        }

        public void Stop()
        {
            wasStoped = true;
            if(underlyingThread == null)
            {
                return;
            }

            this.Log(LogLevel.Info, "Stopping the execution tracer worker and dumping the trace to a file...");

            blocks.CompleteAdding();
            underlyingThread.Join();
            underlyingThread = null;

            this.Log(LogLevel.Info, "Execution tracer stopped");
        }

        public TranslationCPU AttachedCPU { get; }

        [PreSerialization]
        private void PreSerializationHook()
        {
            Stop();
        }

        [PostDeserialization]
        private void PostDeserializationHook()
        {
            if(wasStarted)
            {
                Start();
            }
        }

        private void WriterThreadBody()
        {
            while(true)
            {
                try
                {
                    var block = blocks.Take();
                    do
                    {
                        writer.Write(block);
                    }
                    while(blocks.TryTake(out block));
                    writer.Flush();
                }
                catch(InvalidOperationException)
                {
                    // this happens when the blocking collection is empty and is marked as completed - i.e., we are sure there will be no more elements
                    break;
                }
            }
            writer.Dispose();
        }

        private void HandleBlockEndHook(ulong pc, uint instructionsInBlock)
        {
            if(instructionsInBlock == 0 || wasStoped)
            {
                // ignore
                return;
            }

            var translatedAddress = AttachedCPU.TranslateAddress(pc, MpuAccess.InstructionFetch);
            if(translatedAddress != ulong.MaxValue)
            {
                pc = translatedAddress;
            }

            try
            {
                blocks.Add(new Block
                {
                    FirstInstructionPC = pc,
                    InstructionsCount = instructionsInBlock,
                    DisassemblyFlags = AttachedCPU.CurrentBlockDisassemblyFlags,
                });
            }
            catch(Exception)
            {
                this.Log(LogLevel.Warning, "The translation block that started at 0x{0:X} will not be traced and saved to file. The ExecutionTracer isn't ready yet.", pc);
            }
        }

        [Transient]
        private TraceWriter writer;
        [Transient]
        private Thread underlyingThread;

        private BlockingCollection<Block> blocks;
        private bool wasStarted;
        private bool wasStoped;

        private readonly TraceWriterBuilder writerBuilder;

        public struct Block
        {
            public ulong FirstInstructionPC;
            public ulong InstructionsCount;
            public uint DisassemblyFlags;

            public override string ToString()
            {
                return $"[Block: starting at 0x{FirstInstructionPC:X} with {InstructionsCount} instructions, flags: 0x{DisassemblyFlags:X}]";
            }
        }
    }
}
