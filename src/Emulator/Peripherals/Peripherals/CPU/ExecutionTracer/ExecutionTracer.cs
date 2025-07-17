//
// Copyright (c) 2010-2025 Antmicro
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
        public static void CreateExecutionTracing(this TranslationCPU @this, string name, string fileName, TraceFormat format, bool isBinary = false, bool compress = false, bool isSynchronous = false)
        {
            var writerBuilder = new TraceWriterBuilder(@this, fileName, format, isBinary, compress);
            var tracer = new ExecutionTracer(@this, writerBuilder, isSynchronous);

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

        public static void CreateExecutionTracingSynchronous(this TranslationCPU @this, string name, string fileName, TraceFormat format, bool isBinary = false, bool compress = false)
        {
            CreateExecutionTracing(@this, name, fileName, format, isBinary, compress, true);
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
        public ExecutionTracer(TranslationCPU cpu, TraceWriterBuilder traceWriterBuilder, bool isSynchronous = false)
        {
            AttachedCPU = cpu;
            writerBuilder = traceWriterBuilder;
            this.isSynchronous = isSynchronous;

            AttachedCPU.SetHookAtBlockEnd(HandleBlockEndHook);
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            if(IsRunning)
            {
                throw new RecoverableException("ExecutionTracer is already running");
            }

            writer = writerBuilder.CreateWriter();
            writer.WriteHeader();

            currentAdditionalData = new Queue<AdditionalData>();

            if(!isSynchronous)
            {
                blocks = new BlockingCollection<Block>();

                underlyingThread = new Thread(WriterThreadBody);
                underlyingThread.IsBackground = true;
                underlyingThread.Name = "Execution tracer worker";
                underlyingThread.Start();
            }

            wasStarted = true;
            wasStopped = false;
        }

        public void Stop()
        {
            if(!IsRunning)
            {
                return;
            }
            wasStopped = true;

            if(!isSynchronous)
            {
                this.Log(LogLevel.Info, "Stopping the execution tracer worker and dumping the trace to a file...");
                blocks.CompleteAdding();
                underlyingThread.Join();
                underlyingThread = null;
            }
            else
            {
                writer.Dispose();
            }

            this.Log(LogLevel.Info, "Execution tracer stopped, output file: {0}", writerBuilder.Path);
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
            if(instructionsInBlock == 0 || wasStopped)
            {
                // ignore
                return;
            }

            // We don't care if translation fails here (the address is unchanged in this case)
            AttachedCPU.TryTranslateAddress(pc, MpuAccess.InstructionFetch, out var pcPhysical);

            var block = new Block
            {
                FirstInstructionPC = pcPhysical,
                FirstInstructionVirtualPC = pc,
                InstructionsCount = instructionsInBlock,
                DisassemblyFlags = AttachedCPU.CurrentBlockDisassemblyFlags,
                AdditionalDataInTheBlock = currentAdditionalData,
            };

            if(!isSynchronous)
            {
                try
                {
                    blocks.Add(block);
                }
                catch(Exception e)
                {
                    this.Log(LogLevel.Warning, "The translation block that started at 0x{0:X} will not be traced and saved to file. The ExecutionTracer isn't ready yet.\n Underlying error : {1}", pc, e);
                }
            }
            else
            {
                writer.Write(block);
            }

            currentAdditionalData = new Queue<AdditionalData>();
        }

        [Transient]
        private TraceWriter writer;
        [Transient]
        private Thread underlyingThread;

        private bool IsRunning => wasStarted && !wasStopped;

        private BlockingCollection<Block> blocks;
        private Queue<AdditionalData> currentAdditionalData;
        private bool wasStarted;
        private bool wasStopped;
        private readonly bool isSynchronous;

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
