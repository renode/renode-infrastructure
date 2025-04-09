//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities.Binding;
using PerfettoTraceWriter = Antmicro.Renode.Peripherals.CPU.GuestProfiling.ProtoBuf.PerfettoTraceWriter;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class PerfettoProfiler : BaseProfiler
    {
        public PerfettoProfiler(TranslationCPU cpu, string filename, bool flushInstantly, bool enableMultipleTracks, long? fileSizeLimit = null, int? maximumNestedContexts = null)
            : base(cpu, flushInstantly, maximumNestedContexts)
        {
            this.fileSizeLimit = fileSizeLimit;
            this.enableMultipleTracks = enableMultipleTracks;
            lastInterruptExitTime = ulong.MaxValue;
            fileStream = File.Open(filename, FileMode.OpenOrCreate);
            writer = new PerfettoTraceWriter();
            currentTrack = MainTrack;
            writer.CreateTrack(cpu.GetName(), currentTrack);
        }

        public override void Dispose()
        {
            base.Dispose();
            fileStream.Close();
        }

        public override void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            var currentSymbol = GetSymbolName(currentAddress);
            cpu.Log(LogLevel.Debug, "Profiler: Pushing new frame with symbol: {0}; returnAddress: 0x{1:X}; currentAddress: 0x{2:X}", currentSymbol, returnAddress, currentAddress);

            if(isFirstFrame)
            {
                isFirstFrame = false;
                // We have to add the first frame, form which the first jump happened
                // To infer this frame we use the current PC value, which should point to the jump instruction that triggered this event
                var prevSymbol = GetSymbolName(cpu.PC.RawValue);
                currentStack.Push(prevSymbol);
                writer.CreateEventBegin(0, prevSymbol, currentTrack);
            }

            currentStack.Push(currentSymbol);
            ulong time = InstructionCountToNs(instructionsCount);
            writer.CreateEventBegin(time, currentSymbol, currentTrack);
            CheckAndFlush(time);
        }

        public override void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            cpu.Log(LogLevel.Debug, "Profiler: Trying to pop frame with returnAddress: 0x{0:X} ({1}); currentAddress: 0x{2:X}", returnAddress, GetSymbolName(returnAddress), currentAddress);
            if(currentStack.Count == 0)
            {
                cpu.Log(LogLevel.Error, "Profiler: Trying to return from frame while internal stack tracking is empty");
                return;
            }

            ulong time = InstructionCountToNs(instructionsCount);
            writer.CreateEventEnd(time, currentTrack);
            currentStack.Pop();
            CheckAndFlush(time);
        }

        public override void OnStackPointerChange(ulong address, ulong oldSPValue, ulong newSPValue, ulong instructionsCount)
        {
        }

        public override void OnContextChange(ulong newContextId)
        {
            if(newContextId == currentContextId)
            {
                return;
            }

            cpu.Log(LogLevel.Debug, "Profiler: Changing context from: 0x{0:X} to 0x{1:X}", currentContextId, newContextId);

            ulong track = enableMultipleTracks ? currentContextId : MainTrack;
            ulong time = InstructionCountToNs(cpu.ExecutedInstructions);

            // End the current thread's stack frame
            for(int i = 0; i < currentStack.Count; i++)
            {
                writer.CreateEventEnd(time, track);
            }
            PushCurrentContextSafe();

            // Get the new thread's execution and restore the events
            if(!wholeExecution.ContainsKey(newContextId))
            {
                wholeExecution.Add(newContextId, new ProfilerContext());
                if(enableMultipleTracks)
                {
                    writer.CreateTrack($"Track: 0x{newContextId:X}", newContextId);
                }
            }

            currentContextId = newContextId;

            ulong newTrack = enableMultipleTracks ? newContextId : MainTrack;
            if(currentContext.Count > 0)
            {
                currentContext.PopCurrentStack();
                // Restore events from the previous visit to this context
                foreach(var frame in currentStack.Reverse())
                {
                    writer.CreateEventBegin(time, frame, newTrack);
                }
            }
            else
            {
                currentContext.PopCurrentStack();
            }

            currentTrack = newTrack;

            CheckAndFlush(time);
        }

        public override void InterruptEnter(ulong interruptIndex)
        {
            ulong time = InstructionCountToNs(cpu.ExecutedInstructions);
            if(lastInterruptExitTime == time)
            {
                // If we entered another interrupt just after exiting the previous one
                // We have to remove the start packets so the trace doesn't show a stack
                // with the duration of 0
                writer.RemoveLastNPackets(currentStack.Count);
            }
            else
            {
                // If there is no need to remove the events then we want to finish
                // the current stack before creating a new one for the interrupt
                FinishCurrentStack(time, currentTrack);
            }

            PushCurrentContextSafe();
            cpu.Log(LogLevel.Debug, "Profiler: Interrupt entry (pc 0x{0:X})- saving the stack", cpu.PC);
            CheckAndFlush(time);
        }

        public override void InterruptExit(ulong interruptIndex)
        {
            ulong time = InstructionCountToNs(cpu.ExecutedInstructions);
            FinishCurrentStack(time, currentTrack);
            lastInterruptExitTime = time;
            // We have to temporarily disable flushing to a file, since the next few packets can be removed
            blockFlush = true;

            cpu.Log(LogLevel.Debug, "Profiler: Interrupt exit - restoring the stack");
            if(currentContext.Count > 0)
            {
                currentContext.PopCurrentStack();
                // Restart functions on the current stack
                foreach(var frame in currentStack.Reverse())
                {
                    writer.CreateEventBegin(time, frame, currentTrack);
                }
            }
            else
            {
                currentContext.PopCurrentStack();
            }
            CheckAndFlush(time);
        }

        public override void FlushBuffer()
        {
            // DisableProfiler() calls Dispose() which calls FlushBuffer()
            // `isDisposing` flag is required to prevent an infinite loop
            if(blockFlush || isDisposing)
            {
                return;
            }

            lock(bufferLock)
            {
                // Write to MemoryStream first so we can check the serialized length against the limit
                var memoryStream = new MemoryStream();
                writer.FlushBuffer(memoryStream);

                if(fileSizeLimit.HasValue && (fileStream.Length + memoryStream.Length) > fileSizeLimit)
                {
                    isDisposing = true;
                    cpu.Log(LogLevel.Warning, "Profiler: Maximum file size exceeded, removing profiler");
                    cpu.DisableProfiler();
                    return;
                }

                memoryStream.WriteTo(fileStream);
            }
        }

        private void FinishCurrentStack(ulong time, ulong trackId)
        {
            for(int i = 0; i < currentStack.Count; i++)
            {
                writer.CreateEventEnd(time, trackId);
            }
        }

        private void CheckAndFlush(ulong time)
        {
            if(blockFlush && time > lastInterruptExitTime)
            {
                // If the time advanced pass the last interrupt exit the flushing can be turned back on
                blockFlush = false;
            }

            if(flushInstantly || writer.PacketCount > BufferFlushLevel)
            {
                FlushBuffer();
            }
        }

        private ulong InstructionCountToNs(ulong instructionCount)
        {
            // 1 ins takes 1s / (MIPS * 10^6)
            // So 1 ins = 10^9ns / (MIPS * 10^6) => (1000 / MIPS) ns
            return (1000 * instructionCount) / cpu.PerformanceInMips;
        }

        private readonly PerfettoTraceWriter writer;
        private readonly bool enableMultipleTracks;
        private readonly FileStream fileStream;
        private readonly long? fileSizeLimit;

        private bool isDisposing;
        private bool blockFlush;
        private ulong lastInterruptExitTime;
        private ulong currentTrack;
        
        private const ulong MainTrack = 0;
        private const int BufferFlushLevel = 10000;
    }
}
