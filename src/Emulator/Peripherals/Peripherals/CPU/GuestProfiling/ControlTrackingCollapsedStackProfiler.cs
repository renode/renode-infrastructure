//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class ControlTrackingCollapsedStackProfiler : BaseProfiler
    {
        public ControlTrackingCollapsedStackProfiler(TranslationCPU cpu, string filename, bool flushInstantly, long? fileSizeLimit = null, int? maximumNestedContexts = null)
            : base(cpu, flushInstantly, maximumNestedContexts)
        {
            this.fileSizeLimit = fileSizeLimit;
            stringBuffer = new StringBuilder();
            fileStream = new StreamWriter(filename);
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
            }

            var instructionsElapsed = GetInstructionsDelta(instructionsCount);
            AddStackToBufferWithDelta(instructionsElapsed);

            currentStack.Push(currentSymbol);

            CheckAndFlushBuffer();
        }

        public override void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            cpu.Log(LogLevel.Debug, "Profiler: Trying to pop frame with returnAddress: 0x{0:X} ({1}); currentAddress: 0x{2:X}", returnAddress, GetSymbolName(returnAddress), currentAddress);
            if(currentStack.Count == 0)
            {
                cpu.Log(LogLevel.Error, "Profiler: Trying to return from frame while internal stack tracking is empty");
                return;
            }

            var instructionsElapsed = GetInstructionsDelta(instructionsCount);
            AddStackToBufferWithDelta(instructionsElapsed);

            currentStack.Pop();

            CheckAndFlushBuffer();
        }

        public override void OnStackPointerChange(ulong address, ulong oldPointerValue, ulong newPointerValue, ulong instructionsCount)
        {
        }

        public override void OnContextChange(ulong newContextId)
        {
            if(newContextId == currentContextId)
            {
                return;
            }

            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);
            PushCurrentContextSafe();

            cpu.Log(LogLevel.Debug, "Profiler: Changing context from: 0x{0:X} to 0x{1:X}", currentContextId, newContextId);

            if(!wholeExecution.ContainsKey(newContextId))
            {
                wholeExecution.Add(newContextId, new ProfilerContext());
            }

            currentContextId = newContextId;
            currentContext.PopCurrentStack();

            CheckAndFlushBuffer();
        }

        public override void InterruptEnter(ulong interruptIndex)
        {
            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);

            cpu.Log(LogLevel.Debug, "Profiler: Interrupt entry (pc 0x{0:X})- saving the stack", cpu.PC);
            PushCurrentContextSafe();

            CheckAndFlushBuffer();
        }

        public override void InterruptExit(ulong interruptIndex)
        {
            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);

            cpu.Log(LogLevel.Debug, "Profiler: Interrupt exit - restoring the stack");
            currentContext.PopCurrentStack();

            CheckAndFlushBuffer();
        }

        public override void FlushBuffer()
        {
            // DisableProfiler() calls Dispose() which calls FlushBuffer()
            // This flag is required to prevent an infinite loop
            if(isDisposing)
            {
                return;
            }

            lock(bufferLock)
            {
                if(fileSizeLimit.HasValue && (fileWrittenBytes + stringBuffer.Length) > fileSizeLimit)
                {
                    isDisposing = true;
                    cpu.Log(LogLevel.Warning, "Profiler: Maximum file size exceeded, removing profiler");
                    cpu.DisableProfiler();
                    return;
                }

                fileStream.Write(stringBuffer.ToString());
                fileWrittenBytes += stringBuffer.Length;
                stringBuffer.Clear();
            }
        }

        public override string GetCurrentStack()
        {
            var result = FormatCollapsedStackString(currentStack);

            return result;
        }

        private ulong GetInstructionsDelta(ulong currentInstructionsCount)
        {
            currentInstructionsCount += cpu.SkipInstructions + cpu.SkippedInstructions;
            ulong instructionsElapsed = checked(currentInstructionsCount - lastInstructionsCount);
            lastInstructionsCount = currentInstructionsCount;
            return instructionsElapsed;
        }

        private void AddStackToBufferWithDelta(ulong instructionDelta)
        {
            if(instructionDelta == 0)
            {
                // Speedscope doesn't draw segments with length of 0
                // We can just skip them to save some file space
                return;
            }

            var collapsedStack = FormatCollapsedStackString(currentStack);
            AddToBuffer($"{collapsedStack} {instructionDelta}");
        }

        private void AddToBuffer(string stringToAdd)
        {
            lock(bufferLock)
            {
                stringBuffer.AppendLine(stringToAdd);
            }
        }

        private void CheckAndFlushBuffer()
        {
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
        private readonly StreamWriter fileStream;
        private readonly long? fileSizeLimit;

        private ulong lastInstructionsCount;
        private long fileWrittenBytes;
        private bool isDisposing;

        private const int BufferFlushLevel = 1000000;
    }
}
