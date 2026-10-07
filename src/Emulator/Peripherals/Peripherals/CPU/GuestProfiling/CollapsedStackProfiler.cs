//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class CollapsedStackProfiler : BaseProfiler
    {
        public CollapsedStackProfiler(TranslationCPU cpu, string filename, bool flushInstantly, ulong? fileSizeLimit = null, uint? maximumNestedContexts = null)
            : base(cpu, filename, flushInstantly, fileSizeLimit, maximumNestedContexts)
        {
            buffer = new MemoryStream();
            streamWriter = new StreamWriter(buffer);
            streamWriter.AutoFlush = true;
        }

        protected override void WriteAfterFramePush(ulong instructionCount)
        {
            AddStackToBufferWithDelta(CurrentStack.Skip(1), GetInstructionsDelta(instructionCount));
        }

        protected override void WriteBeforeFramePop(ulong instructionCount)
        {
            AddStackToBufferWithDelta(CurrentStack, GetInstructionsDelta(instructionCount));
        }

        protected override void WriteStackLoad(ulong instructionCount)
        {
            // Collapsed Stack only writes profiling information after specific stack ends, so we don't need to do any writing action yet.
            lastInstructionsCount = instructionCount;
        }

        protected override void WriteStackFinish(ulong instructionCount)
        {
            AddStackToBufferWithDelta(CurrentStack, GetInstructionsDelta(instructionCount));
        }

        protected override void FlushToOutputStreamInner()
        {
            buffer.WriteTo(outputStream);
            buffer.SetLength(0);
        }

        protected override bool ShouldFlush => buffer.Length > BufferFlushLevel;

        protected override ulong BytesPending
        {
            get
            {
                return (ulong)buffer.Length;
            }
        }

        private ulong GetInstructionsDelta(ulong currentInstructionsCount)
        {
            currentInstructionsCount += cpu.SkipInstructions + cpu.SkippedInstructions;
            var instructionsElapsed = checked(currentInstructionsCount - lastInstructionsCount);
            lastInstructionsCount = currentInstructionsCount;
            return instructionsElapsed;
        }

        private void AddStackToBufferWithDelta(IEnumerable<string> stack, ulong instructionDelta)
        {
            if(instructionDelta == 0)
            {
                // Speedscope doesn't draw segments with length of 0
                // We can just skip them to save some file space
                return;
            }

            var collapsedStack = FormatStack(stack);
            AddToBuffer($"{collapsedStack} {instructionDelta}");
        }

        private string FormatStack(IEnumerable<string> stack)
        {
            return string.Join(";", stack.Reverse());
        }

        private void AddToBuffer(string stringToAdd)
        {
            streamWriter.WriteLine(stringToAdd);
        }

        private ulong lastInstructionsCount;
        private readonly StreamWriter streamWriter;
        private readonly MemoryStream buffer;
        private const int BufferFlushLevel = 1000000;
    }
}