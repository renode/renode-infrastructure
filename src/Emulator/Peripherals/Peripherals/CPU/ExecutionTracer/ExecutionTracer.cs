//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class ExecutionTracerExtensions
    {
        public static void CreateExecutionTracing(this TranslationCPU @this, string name, string fileName, TraceFormat format, bool isBinary = false, bool compress = false)
        {
            var writerBuilder = new TraceWriterBuilder(@this, fileName, format, isBinary, compress);
            var tracer = new ExecutionTracer(@this, writerBuilder);

            try
            {
                // we keep it as external to dispose/flush on quit
                EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal(tracer, name);
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
            currentAdditionalData = new Queue<AdditionalData>();

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
                    writer.FlushBuffer();
                }
                catch(InvalidOperationException)
                {
                    // this happens when the blocking collection is empty and is marked as completed - i.e., we are sure there will be no more elements
                    break;
                }
            }
            writer.Dispose();
        }

        public void TrackMemoryAccesses()
        {
            if(AttachedCPU.Architecture == "i386")
            {
                throw new RecoverableException("This feature is not yet available on the X86 platforms.");
            }
            AttachedCPU.SetHookAtMemoryAccess((pc, operation, virtualAddress, physicalAddress, value) =>
            {
                if(operation != MemoryOperation.InsnFetch)
                {
                    currentAdditionalData.Enqueue(new MemoryAccessAdditionalData(pc, operation, virtualAddress, physicalAddress, value));
                }
            });
        }

        public void TrackVectorConfiguration()
        {
            if(!(AttachedCPU is ICPUWithPostOpcodeExecutionHooks) || !(AttachedCPU.Architecture.StartsWith("riscv")))
            {
                throw new RecoverableException("This feature is not available on this platform");
            }
            var cpuWithPostOpcodeExecutionHooks = AttachedCPU as ICPUWithPostOpcodeExecutionHooks;
            cpuWithPostOpcodeExecutionHooks.EnablePostOpcodeExecutionHooks(1u);
            // 0x7057 it the fixed part of the vcfg opcodes
            cpuWithPostOpcodeExecutionHooks.AddPostOpcodeExecutionHook(0x7057, 0x7057, (pc) =>
            {
                var vl = AttachedCPU.GetRegister(RiscVVlRegisterIndex);
                var vtype = AttachedCPU.GetRegister(RiscVVtypeRegisterIndex);
                currentAdditionalData.Enqueue(new RiscVVectorConfigurationData(pc, vl.RawValue, vtype.RawValue));
            });
        }

        private void HandleBlockEndHook(ulong pc, uint instructionsInBlock)
        {
            if(instructionsInBlock == 0 || wasStoped)
            {
                // ignore
                return;
            }

            // We don't care if translation fails here (the address is unchanged in this case)
            AttachedCPU.TryTranslateAddress(pc, MpuAccess.InstructionFetch, out var pcPhysical);

            try
            {
                blocks.Add(new Block
                {
                    FirstInstructionPC = pcPhysical,
                    FirstInstructionVirtualPC = pc,
                    InstructionsCount = instructionsInBlock,
                    DisassemblyFlags = AttachedCPU.CurrentBlockDisassemblyFlags,
                    AdditionalDataInTheBlock = currentAdditionalData,
                });
            }
            catch(Exception e)
            {
                this.Log(LogLevel.Warning, "The translation block that started at 0x{0:X} will not be traced and saved to file. The ExecutionTracer isn't ready yet.\n Underlying error : {1}", pc, e);
            }
            currentAdditionalData = new Queue<AdditionalData>();
        }

        [Transient]
        private TraceWriter writer;
        [Transient]
        private Thread underlyingThread;

        private BlockingCollection<Block> blocks;
        private Queue<AdditionalData> currentAdditionalData;
        private bool wasStarted;
        private bool wasStoped;

        private readonly TraceWriterBuilder writerBuilder;

        private const int RiscVVlRegisterIndex = 3104;
        private const int RiscVVtypeRegisterIndex = 3105;

        public struct Block
        {
            public ulong FirstInstructionPC;
            public ulong FirstInstructionVirtualPC;
            public ulong InstructionsCount;
            public uint DisassemblyFlags;
            public Queue<AdditionalData> AdditionalDataInTheBlock;

            public override string ToString()
            {
                return $"[Block: starting at 0x{FirstInstructionPC:X} with {InstructionsCount} instructions and {AdditionalDataInTheBlock.Count} additional data, flags: 0x{DisassemblyFlags:X}]";
            }
        }
    }
}
