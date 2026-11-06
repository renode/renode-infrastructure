//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class TraceEventProfiler : BaseProfiler
    {
        public TraceEventProfiler(TranslationCPU cpu, string filename, bool flushInstantly, ulong? fileSizeLimit = null, uint? maximumNestedContexts = null, bool enableMultipleTracks = false)
            : base(cpu, filename, flushInstantly, fileSizeLimit, maximumNestedContexts)
        {
            this.enableMultipleTracks = enableMultipleTracks;
            writer = new Utf8JsonWriter(
                        outputStream,
                        new JsonWriterOptions() { Indented = false }
                    );
            writer.WriteStartArray();
            writer.Flush(); // Write start of the array to the file

            if(enableMultipleTracks)
            {
                createdContexts = new HashSet<ulong>();
                WriteNewContextIfNeeded(currentContextId);
            }
            else
            {
                AddProcessName(cpu.GetName(), MainTid);
            }
        }

        public override void Dispose()
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }

                if(aboveSizeLimit)
                {
                    // In case where we are above the limit, we just force write the array end character to output file
                    // In rare case when our trace is perfectly the size of the limit, this write might increase the size of the file above the limit by one byte.

                    // Create new writer with no validation to write the finishing array end
                    var finishWriter = new Utf8JsonWriter(
                        outputStream,
                        new JsonWriterOptions() { Indented = false , SkipValidation = true}
                    );

                    finishWriter.WriteEndArray();
                    finishWriter.Flush();
                    // Reset the writer but don't Dispose as it'll close the stream and we want to handle it ourselves in parent Dispose
                    finishWriter.Reset();
                }
                else
                {
                    // In normal case just finish the trace with array end character and let parent dispose handle the write
                    writer.WriteEndArray();
                }

                base.Dispose();
                // Reset the writer but don't Dispose because it'll try to close the output stream that we already closed
                writer.Reset();
            }
        }

        protected override void WriteAfterFramePush(ulong instructionsCount)
        {
            CreateDurationBeginEvent(CurrentStack.Peek(), instructionsCount);
        }

        protected override void WriteBeforeFramePop(ulong instructionsCount)
        {
            CreateDurationEndEvent(CurrentStack.Peek(), instructionsCount);
        }

        protected override void WriteStackFinish(ulong instructionsCount)
        {
            foreach(var frame in CurrentStack)
            {
                CreateDurationEndEvent(frame, instructionsCount);
            }
        }

        protected override void WriteStackLoad(ulong instructionsCount)
        {
            foreach(var frame in CurrentStack.Reverse())
            {
                CreateDurationBeginEvent(frame, instructionsCount);
            }
        }

        protected override void FlushToOutputStreamInner()
        {
            writer.Flush();
        }

        protected override ulong BytesPending => (ulong)writer.BytesPending;

        protected override bool ShouldFlush => writer.BytesPending > BufferFlushLevel;

        private decimal InstructionCountToUs(ulong instructionCount)
        {
            return (decimal)TimeInterval.FromCPUCycles(instructionCount, cpu.PerformanceInMips, out var _).Ticks / TimeInterval.TicksPerMicrosecond;
        }

        private void WriteNewContextIfNeeded(ulong id)
        {
            if(createdContexts != null && !createdContexts.Contains(id))
            {
                createdContexts.Add(id);
                AddProcessName($"Context {id}", id);
            }
        }

        private void CreateDurationBeginEvent(string frame, ulong instructionsCount)
        {
            CreateDurationEvent(isBegin: true, frame, instructionsCount);
        }

        private void CreateDurationEndEvent(string frame, ulong instructionsCount)
        {
            CreateDurationEvent(isBegin: false, frame, instructionsCount);
        }

        private void CreateDurationEvent(bool isBegin, string frame, ulong instructionsCount)
        {
            WriteNewContextIfNeeded(CurrentTid);
            writer.WriteStartObject();
            writer.WriteString("ph", ((char)(isBegin ? EventType.DurationBegin : EventType.DurationEnd)).ToString());
            writer.WriteString("name", frame);
            writer.WriteNumber("ts", InstructionCountToUs(instructionsCount));
            writer.WriteNumber("pid", MainPid);
            writer.WriteNumber("tid", CurrentTid);
            writer.WriteEndObject();
        }

        private void AddProcessName(string name, ulong id)
        {
            writer.WriteStartObject();
            writer.WriteString("ph", ((char)EventType.Metadata).ToString());
            writer.WriteString("name", "thread_name");
            writer.WriteNumber("pid", MainPid);
            writer.WriteNumber("tid", id);
            writer.WritePropertyName("args");
            writer.WriteRawValue(JsonSerializer.Serialize(new Dictionary<string, string> { { "name", name } }));
            writer.WriteEndObject();
        }

        private void FinishTrace()
        {
            writer.WriteEndArray();
        }

        // Thread and Process ID start from 1, so increase received indices to accommodate for that.
        private ulong CurrentTid
        {
            get
            {
                if(enableMultipleTracks)
                {
                    return currentContextId;
                }
                return MainTid;
            }
        }

        private readonly HashSet<ulong> createdContexts;
        private readonly bool enableMultipleTracks;
        private readonly Utf8JsonWriter writer;

        private const int MainPid = 0;
        private const int MainTid = 0;
        private const int BufferFlushLevel = 1000000;

        enum EventType
        {
            DurationBegin = 'B',
            DurationEnd = 'E',
            Metadata = 'M',
        }
    }
}