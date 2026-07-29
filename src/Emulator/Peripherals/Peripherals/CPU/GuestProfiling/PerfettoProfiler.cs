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

using Antmicro.Renode.Time;

using PerfettoTraceWriter = Antmicro.Renode.Peripherals.CPU.GuestProfiling.ProtoBuf.PerfettoTraceWriter;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class PerfettoProfiler : BaseProfiler
    {
        public PerfettoProfiler(TranslationCPU cpu, string filename, bool flushInstantly, ulong? fileSizeLimit = null, uint? maximumNestedContexts = null, bool enableMultipleTracks = false)
            : base(cpu, filename, flushInstantly, fileSizeLimit, maximumNestedContexts)
        {
            this.enableMultipleTracks = enableMultipleTracks;
            writer = new PerfettoTraceWriter();

            buffer = new MemoryStream();
            if(enableMultipleTracks)
            {
                createdContexts = new HashSet<ulong>();
                WriteNewContextIfNeeded(currentContextId);
            }
            else
            {
                writer.CreateTrack(cpu.GetName(), MainTrack);
            }
        }

        protected override void WriteAfterFramePush(ulong instructionsCount)
        {
            var time = InstructionCountToNs(instructionsCount);
            WriteNewContextIfNeeded(CurrentTrackId);
            writer.CreateEventBegin(time, CurrentStack.Peek(), CurrentTrackId);
        }

        protected override void WriteBeforeFramePop(ulong instructionsCount)
        {
            var time = InstructionCountToNs(instructionsCount);
            WriteNewContextIfNeeded(CurrentTrackId);
            writer.CreateEventEnd(time, CurrentTrackId);
        }

        protected override void WriteStackLoad(ulong instructionsCount)
        {
            foreach(var frame in CurrentStack.Reverse())
            {
                WriteNewContextIfNeeded(CurrentTrackId);
                writer.CreateEventBegin(InstructionCountToNs(instructionsCount), frame, CurrentTrackId);
            }
        }

        protected override void WriteStackFinish(ulong instructionsCount)
        {
            foreach(var frame in CurrentStack)
            {
                WriteNewContextIfNeeded(CurrentTrackId);
                writer.CreateEventEnd(InstructionCountToNs(instructionsCount), CurrentTrackId);
            }
        }

        protected override void FlushToOutputStreamInner()
        {
            if(buffer.Length > 0)
            {
                buffer.WriteTo(outputStream);
                buffer.SetLength(0);
            }
            if(writer.PacketCount > 0)
            {
                writer.FlushBuffer(outputStream);
            }
        }

        protected override bool ShouldFlush => writer.PacketCount > BufferFlushLevel;

        protected override ulong BytesPending
        {
            get
            {
                writer.FlushBuffer(buffer);
                return (ulong)buffer.Length;
            }
        }

        private ulong InstructionCountToNs(ulong instructionCount)
        {
            return TimeInterval.FromCPUCycles(instructionCount, cpu.PerformanceInMips, out var _).TotalNanoseconds;
        }

        private void WriteNewContextIfNeeded(ulong id)
        {
            if(createdContexts != null && !createdContexts.Contains(id))
            {
                createdContexts.Add(id);
                writer.CreateTrack($"Context {id}", id);
            }
        }

        private ulong CurrentTrackId
        {
            get
            {
                if(enableMultipleTracks)
                {
                    return currentContextId;
                }
                return MainTrack;
            }
        }

        private readonly MemoryStream buffer;
        private readonly HashSet<ulong> createdContexts;
        private readonly PerfettoTraceWriter writer;
        private readonly bool enableMultipleTracks;
        private const ulong MainTrack = 0;
        private const int BufferFlushLevel = 10000;
    }
}
