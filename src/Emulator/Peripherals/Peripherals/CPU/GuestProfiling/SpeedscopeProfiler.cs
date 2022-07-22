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

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class SpeedscopeProfiler : BaseProfiler
    {
        public SpeedscopeProfiler(TranslationCPU cpu, string filename, bool flushInstantly, bool enableMultipleTracks) 
            : base(cpu, filename, flushInstantly, enableMultipleTracks)
        {
            stringBuffer = new StringBuilder();
            bufferLock = new Object();
            lastInstructionsCount = UInt32.MaxValue;
            execution = new Stack<Stack<string>>();
            wholeExecution.Add(threadId, execution);
        }

        public override void Dispose()
        {
            base.Dispose();
            lastInstructionsCount = 0;
        }

        public override void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            var currentSymbol = GetSymbolFromAddr(currentAddress);
            cpu.Log(LogLevel.Debug, "Profiler: Pushing new frame with symbol: {0}; returnAddress: 0x{1:X}; currentAddress: 0x{2:X}", currentSymbol, returnAddress, currentAddress);

            if(lastInstructionsCount == UInt32.MaxValue)
            {
                lastInstructionsCount = 0;
                // used only to add the first frame which we cannot infer by the jump insn
                var prevSymbol = GetSymbolFromAddr(returnAddress - 4);
                currentStack.Push(prevSymbol);
            }

            var instructionsElapsed = GetInstructionsDelta(instructionsCount);
            AddStackToBufferWithDelta(instructionsElapsed);

            currentStack.Push(currentSymbol);
        }

        public override void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            cpu.Log(LogLevel.Debug, "Profiler: Trying to pop frame with returnAddress: 0x{0:X} ({1}); currentAddress: 0x{2:X}", returnAddress, GetSymbolFromAddr(returnAddress), currentAddress);
            if(currentStack.Count == 0)
            {
                cpu.Log(LogLevel.Error, "Profiler: Trying to return from frame while internal stack tracking is empty");
                return;
            }

            var instructionsElapsed = GetInstructionsDelta(instructionsCount);
            var collapsedStack = FormatCollapsedStackString(currentStack);
            AddToBuffer($"{collapsedStack} {instructionsElapsed}");

            currentStack.Pop();
        }

        public override void ThreadChange(ulong newThreadId)
        {
            if (newThreadId == threadId)
            {
                return;
            }

            cpu.Log(LogLevel.Debug, $"Profiler: Changind thread from: 0x{threadId:X} to 0x{newThreadId:X}");

            if (!wholeExecution.ContainsKey(newThreadId))
            {
                wholeExecution.Add(newThreadId, new Stack<Stack<string>>());
            }

            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);
            execution.Push(currentStack);

            execution = wholeExecution[newThreadId];

            if (execution.Count > 0)
            {
                currentStack = execution.Pop();
            }
            else
            {
                currentStack = new Stack<string>();
            }

            threadId = newThreadId;
        }

        public override void InterruptEnter(ulong interruptIndex)
        {
            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);

            execution.Push(currentStack);
            currentStack = new Stack<string>();
            cpu.Log(LogLevel.Debug, "Profiler: Interrupt entry (pc 0x{0:X})- saving the stack", cpu.PC);
        }

        public override void InterruptExit(ulong interruptIndex)
        {
            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);

            cpu.Log(LogLevel.Debug, "Profiler: Interrupt exit - restoring the stack");
            if (execution.Count > 0)
            {
                currentStack = execution.Pop();
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
                File.AppendAllText(filename, stringBuffer.ToString());
                stringBuffer.Clear();
            }
        }

        private ulong GetInstructionsDelta(ulong currentInstructionsCount)
        {
            ulong instructionsElapsed = checked(currentInstructionsCount - lastInstructionsCount);
            lastInstructionsCount = currentInstructionsCount;
            return instructionsElapsed;
        }

        private void AddStackToBufferWithDelta(ulong instructionDelta)
        {
            var collapsedStack = FormatCollapsedStackString(currentStack);
            AddToBuffer($"{collapsedStack} {instructionDelta}");
        }

        private void AddToBuffer(string stringToAdd)
        {
            lock(bufferLock)
            {
                stringBuffer.AppendFormat("{0}\n", stringToAdd);
            }
            if(flushInstantly || stringBuffer.Length > BufferFlushLevel)
            {
                FlushBuffer();
            }
        }

        private string FormatCollapsedStackString(Stack<string> stack)
        {
            return string.Join(";", stack.Reverse());
        }

        private readonly StringBuilder stringBuffer;
        private ulong lastInstructionsCount;
        private Stack<Stack<string>> execution;
        private Object bufferLock;
        private const int BufferFlushLevel = 1000000;
    }
}
