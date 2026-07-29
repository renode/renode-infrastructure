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

using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    [Transient]
    public abstract class BaseProfiler : IDisposable
    {
        public BaseProfiler(TranslationCPU cpu, string filename, bool flushInstantly, ulong? fileSizeLimit = null, uint? maximumNestedContexts = null)
        {
            this.cpu = cpu;
            this.flushInstantly = flushInstantly;
            this.fileSizeLimit = fileSizeLimit;
            this.maximumNestedContexts = maximumNestedContexts;

            outputStream = File.Open(filename, FileMode.Create);
            MainLock = new Object();

            wholeExecution = new Dictionary<ulong, ProfilerContext>();
            wholeExecution.Add(currentContextId, new ProfilerContext());
        }

        public virtual void Dispose()
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }

                if(!aboveSizeLimit)
                {
                    Flush(); // Flush can dispose the stream, return early if so
                    if(Disposed)
                    {
                        return;
                    }
                }
                outputStream.Close();
                outputStream = null;

                wholeExecution.Clear();
            }
        }

        public virtual void CpuReset(ulong instructionsCount)
        {
            lock(MainLock)
            {
                if(instructionCountOnInterruptExit == null)
                {
                    WriteStackFinish(instructionsCount);
                }

                instructionCountOnInterruptExit = null;

                currentContextId = 0;

                wholeExecution.Clear();
                wholeExecution.Add(currentContextId, new ProfilerContext());

                FlushIfNeeded();
            }
        }

        public virtual void StackFramePush(string frame, ulong instructionsCount)
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }
                RestoreFrameIfAfterInterruptExit();

                CurrentStack.Push(frame);
                WriteAfterFramePush(instructionsCount);
                cpu.DebugLog("Profiler: Added a frame with symbol: {0}", frame);
                FlushIfNeeded();
            }
        }

        public virtual void StackFramePop(ulong instructionsCount)
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }

                if(CurrentStack.Count == 0)
                {
                    cpu.ErrorLog("Profiler: Failed to return from frame while internal stack tracking is empty");
                    return;
                }

                RestoreFrameIfAfterInterruptExit();

                WriteBeforeFramePop(instructionsCount);
                var frame = CurrentStack.Pop();
                cpu.DebugLog("Profiler: Returned from frame with symbol: {0}", frame);
                FlushIfNeeded();
            }
        }

        public virtual void ContextChange(ulong newContextId, ulong instructionsCount)
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }

                if(currentContextId == newContextId)
                {
                    return;
                }

                RestoreFrameIfAfterInterruptExit();

                // Get the new thread's execution and restore the events
                if(!wholeExecution.ContainsKey(newContextId))
                {
                    wholeExecution.Add(newContextId, new ProfilerContext());
                }

                WriteStackFinish(instructionsCount);
                var previousContextId = currentContextId;
                currentContextId = newContextId;
                WriteStackLoad(instructionsCount);

                cpu.DebugLog("Profiler: Changed context from: {0} to {1}", previousContextId, newContextId);
                FlushIfNeeded();
            }
        }

        public virtual void InterruptEnter(ulong instructionsCount)
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }

                if(instructionCountOnInterruptExit != instructionsCount)
                {
                    RestoreFrameIfAfterInterruptExit();
                }

                // If we left an interrupt and immediately entered a new one there's no stack to push
                if(instructionCountOnInterruptExit == null)
                {
                    WriteStackFinish(instructionsCount);
                    if(!TryPushCurrentContext())
                    {
                        // Profiler got disposed, return without flushing
                        return;
                    }
                    cpu.DebugLog("Profiler: Entered an interrupt");
                    FlushIfNeeded();
                }
                else
                {
                    if(!TryPushCurrentContext())
                    {
                        // Profiler got disposed, return
                        return;
                    }
                    instructionCountOnInterruptExit = null;
                    cpu.DebugLog("Profiler: Entered an interrupt just after interrupt exit");
                }
            }
        }

        public virtual void InterruptExit(ulong instructionsCount)
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }

                WriteStackFinish(instructionsCount);
                CurrentContext.PopCurrentStack();
                instructionCountOnInterruptExit = instructionsCount;

                cpu.DebugLog("Profiler: Exited an interrupt");
                FlushIfNeeded();
            }
        }

        public virtual string GetCurrentStack()
        {
            throw new RecoverableException($"Functionality is not supported by the currently selected profiler");
        }

        public virtual bool TryFlushToOutputStream()
        {
            if(fileSizeLimit.HasValue && (ulong)outputStream.Length + BytesPending > fileSizeLimit)
            {
                cpu.WarningLog("Profiler: Maximum file size exceeded, removing profiler");
                aboveSizeLimit = true;
                // Disable the profiler on the cpu to make sure no further writes from tlib happen
                cpu.DisableProfiler();
                return false;
            }
            FlushToOutputStreamInner();
            return true;
        }

        public void Flush()
        {
            lock(MainLock)
            {
                if(Disposed)
                {
                    return;
                }

                if(TryFlushToOutputStream())
                {
                    outputStream.Flush();
                }
            }
        }

        public bool Disposed => outputStream == null;

        public readonly object MainLock;

        protected virtual void FlushIfNeeded()
        {
            if(flushInstantly || ShouldFlush)
            {
                TryFlushToOutputStream();
            }
        }

        protected bool TryPushCurrentContext()
        {
            // Warning: the method can dispose of the profiler
            if(maximumNestedContexts.HasValue && CurrentContext.Count >= maximumNestedContexts)
            {
                cpu.WarningLog("Profiler: maximum nested contexts exceeded, disabling profiler");
                // Disable the profiler on the cpu to make sure no further writes from tlib happen
                cpu.DisableProfiler();
                return false;
            }
            CurrentContext.PushCurrentStack();
            return true;
        }

        protected void RestoreFrameIfAfterInterruptExit()
        {
            // Load frames into profiler only after we left an interrupt
            if(instructionCountOnInterruptExit != null)
            {
                cpu.DebugLog("Profiler: Restoring the frame after interrupt exit");
                WriteStackLoad(instructionCountOnInterruptExit.Value);
                instructionCountOnInterruptExit = null;
            }
        }

        protected abstract void WriteAfterFramePush(ulong instructionsCount);

        protected abstract void WriteBeforeFramePop(ulong instructionsCount);

        protected abstract void WriteStackLoad(ulong instructionsCount);

        protected abstract void WriteStackFinish(ulong instructionsCount);

        protected abstract void FlushToOutputStreamInner();

        protected ProfilerContext CurrentContext => wholeExecution[currentContextId];

        protected Stack<string> CurrentStack => CurrentContext.CurrentStack;

        protected abstract ulong BytesPending { get; }

        protected abstract bool ShouldFlush { get; }

        protected ulong currentContextId;
        protected ulong? instructionCountOnInterruptExit;

        protected FileStream outputStream;
        protected bool aboveSizeLimit;

        // Keeps track of all of the created contexts. Key is the id of the thread
        protected readonly Dictionary<ulong, ProfilerContext> wholeExecution;

        protected readonly uint? maximumNestedContexts;

        protected readonly bool flushInstantly;
        protected readonly TranslationCPU cpu;
        protected readonly ulong? fileSizeLimit;

        protected class ProfilerContext
        {
            public ProfilerContext()
            {
                context = new Stack<Stack<string>>();
                currentStack = new Stack<string>();
            }

            public void PopCurrentStack()
            {
                if(context.Count == 0)
                {
                    currentStack = new Stack<string>();
                }
                else
                {
                    currentStack = context.Pop();
                }
            }

            public void PushCurrentStack()
            {
                context.Push(currentStack);
                currentStack = new Stack<string>();
            }

            public void Clear()
            {
                context.Clear();
            }

            public int Count => context.Count;

            public Stack<string> CurrentStack => currentStack;

            private Stack<string> currentStack;

            // We need to keep a stack of stacks as each context has its main execution and (potentially nested) interrupts
            private readonly Stack<Stack<string>> context;
        }
    }
}
