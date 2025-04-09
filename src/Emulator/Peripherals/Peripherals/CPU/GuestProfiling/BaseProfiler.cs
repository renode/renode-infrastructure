//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    [Transient]
    public abstract class BaseProfiler : IDisposable
    {
        public BaseProfiler(TranslationCPU cpu, bool flushInstantly, int? maximumNestedContexts)
        {
            this.cpu = cpu;
            this.flushInstantly = flushInstantly;

            if(maximumNestedContexts <= 0) {
                throw new ConstructionException("Optional maximumNestedContexts parameter must be a non-zero, positive integer.");
            }
            this.maximumNestedContexts = maximumNestedContexts;

            isFirstFrame = true;
            bufferLock = new Object();
            wholeExecution = new Dictionary<ulong, ProfilerContext>();
            wholeExecution.Add(currentContextId, new ProfilerContext());
        }

        public virtual void Dispose()
        {
            FlushBuffer();
            currentStack.Clear();
            currentContext.Clear();
            wholeExecution.Clear();
        }

        public abstract void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount);
        public abstract void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount);
        public abstract void OnContextChange(ulong newContextId);
        public abstract void OnStackPointerChange(ulong address, ulong oldSPValue, ulong newSPValue, ulong instructionsCount);
        public abstract void InterruptEnter(ulong interruptIndex);
        public abstract void InterruptExit(ulong interruptIndex);
        public abstract void FlushBuffer();

        public virtual string GetCurrentStack()
        {
            throw new RecoverableException($"Functionality is not supported by the currently selected profiler");
        }

        protected string GetSymbolName(ulong address)
        {
            if(!cpu.Bus.TryFindSymbolAt(address, out var name, out var _, cpu))
            {
                // Symbol not found - return address must serve as a symbol
                name = $"0x{address:X}";
            }
            return name;
        }

        protected void PushCurrentContextSafe()
        {
            if(maximumNestedContexts.HasValue && currentContext.Count >= maximumNestedContexts)
            {
                cpu.Log(LogLevel.Warning, "Profiler: maximum nested contexts exceeded, disabling profiler");
                cpu.DisableProfiler();
                return;
            }
            currentContext.PushCurrentStack();
        }

        protected readonly TranslationCPU cpu;
        protected readonly bool flushInstantly;
        protected readonly Object bufferLock;
        // Keeps track of all of the created contexts. Key is the id of the thread
        protected readonly Dictionary<ulong, ProfilerContext> wholeExecution;

        protected ProfilerContext currentContext => wholeExecution[currentContextId];
        protected Stack<string> currentStack => currentContext.CurrentStack;
        protected bool isFirstFrame;
        protected ulong currentContextId;

        private readonly int? maximumNestedContexts;

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

            // We need to keep a stack of stacks as each context has its main execution and (potentially nested) interrupts
            private readonly Stack<Stack<string>> context;
            private Stack<string> currentStack;
        }
    }
}
