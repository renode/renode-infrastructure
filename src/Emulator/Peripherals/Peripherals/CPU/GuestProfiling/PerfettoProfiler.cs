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
using TraceWriter = Antmicro.Renode.Peripherals.CPU.GuestProfiling.ProtoBuf.TraceWriter;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class PerfettoProfiler : BaseProfiler
    {
        public PerfettoProfiler(TranslationCPU cpu, string filename, bool flushInstantly, bool enableMultipleTracks)
            : base(cpu, filename, flushInstantly, enableMultipleTracks)
        {
            bufferLock = new Object();
            isFirstFrame = true;
            writer = new TraceWriter();
            writer.CreateTrack(cpu.GetName(), MainTrack);
            threadId = MainTrack;
            currentTrack = MainTrack;
            execution = new Stack<Stack<string>>();
            wholeExecution.Add(threadId, execution);
        }

        public override void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            var currentSymbol = GetSymbolFromAddr(currentAddress);
            cpu.Log(LogLevel.Debug, "Profiler: Pushing new frame with symbol: {0}; returnAddress: 0x{1:X}; currentAddress: 0x{2:X}", currentSymbol, returnAddress, currentAddress);

            if (isFirstFrame)
            {
                isFirstFrame = false;
                // used only to add the first frame which we cannot infer by the jump insn
                var prevSymbol = GetSymbolFromAddr(returnAddress - 4);
                currentStack.Push(prevSymbol);
                writer.CreateEventBegin(0, prevSymbol, currentTrack);
            }

            currentStack.Push(currentSymbol);
            writer.CreateEventBegin(InstructionCountToNs(instructionsCount), currentSymbol, currentTrack);
            CheckAndFlush();
        }

        public override void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            cpu.Log(LogLevel.Debug, "Profiler: Trying to pop frame with returnAddress: 0x{0:X} ({1}); currentAddress: 0x{2:X}", returnAddress, GetSymbolFromAddr(returnAddress), currentAddress);
            if (currentStack.Count == 0)
            {
                cpu.Log(LogLevel.Error, "Profiler: Trying to return from frame while internal stack tracking is empty");
                return;
            }

            writer.CreateEventEnd(InstructionCountToNs(instructionsCount), currentTrack);
            currentStack.Pop();
            CheckAndFlush();
        }

        public override void ThreadChange(ulong newThreadId)
        {
            if (newThreadId == threadId)
            {
                return;
            }

            cpu.Log(LogLevel.Debug, $"Profiler: Changind thread from: 0x{threadId:X} to 0x{newThreadId:X}");

            ulong time = InstructionCountToNs(cpu.ExecutedInstructions);

            if (!wholeExecution.ContainsKey(newThreadId))
            {
                // Create a track + execution trace
                wholeExecution.Add(newThreadId, new Stack<Stack<string>>());
                if (enableMultipleTracks)
                {
                    writer.CreateTrack($"Track: 0x{newThreadId:X}", newThreadId);
                }
            }

            ulong track = enableMultipleTracks ? threadId : currentTrack;

            // End the current thread's stack frame
            int length = currentStack.Count;
            for (int i = 0; i < length; i++)
            {
                writer.CreateEventEnd(time, track);
            }
            execution.Push(currentStack);

            track = enableMultipleTracks ? newThreadId : currentTrack;

            // Get the new thread's execution and restore the events
            execution = wholeExecution[newThreadId];
            if (execution.Count > 0)
            {
                currentStack = execution.Pop();
                foreach (var frame in currentStack.Reverse())
                {
                    writer.CreateEventBegin(time, frame, track);
                }
            }
            else
            {
                currentStack = new Stack<string>();
            }

            threadId = newThreadId;
            if (enableMultipleTracks)
            {
                currentTrack = threadId;
            }
        }

        public override void InterruptEnter(ulong interruptIndex)
        {
            ulong time = InstructionCountToNs(cpu.ExecutedInstructions);
            int size = currentStack.Count;
            // Finish the current stack
            for (int i = 0; i < size; i++)
            {
                writer.CreateEventEnd(time, currentTrack);
            }
            execution.Push(currentStack);
            currentStack = new Stack<string>();
            cpu.Log(LogLevel.Debug, "Profiler: Interrupt entry (pc 0x{0:X})- saving the stack", cpu.PC);
        }

        public override void InterruptExit(ulong interruptIndex)
        {
            ulong time = InstructionCountToNs(cpu.ExecutedInstructions);
            int size = currentStack.Count;
            // Finish the interrupt stack
            for (int i = 0; i < size; i++)
            {
                writer.CreateEventEnd(time, currentTrack);
            }

            cpu.Log(LogLevel.Debug, "Profiler: Interrupt exit - restoring the stack");
            if (execution.Count > 0)
            {
                currentStack = execution.Pop();
                // Restart functions on the current stack
                foreach (var frame in currentStack.Reverse())
                {
                    writer.CreateEventBegin(time, frame, currentTrack);
                }
            }
            else
            {
                // In Zephyr the platform initialization in multithreaded apps ends with interrupt exit, some other RTOS'es may do the same
                currentStack = new Stack<string>();
            }
        }

        public override void FlushBuffer()
        {
            lock(bufferLock)
            {
                writer.FlushBuffer(filename);
            }
        }

        private void CheckAndFlush()
        {
            if (flushInstantly || writer.GetPacketCount() > BufferFlushLevel)
            {
                FlushBuffer();
            }
        }

        private ulong InstructionCountToNs(ulong instructionCount)
        {
            // 1 ins takes 1s / (MIPS * 10^6)
            // So 1 ins = 10^9ns / (MIPS * 10^6) => (1000 / MIPS) ns
            return  (1000 * instructionCount) / cpu.PerformanceInMips;
        }

        private readonly TraceWriter writer;
        private Stack<Stack<string>> execution;
        private bool isFirstFrame;
        private ulong currentTrack;
        private Object bufferLock;
        private const ulong MainTrack = 0;
        private const int BufferFlushLevel = 10000;
    }
}
