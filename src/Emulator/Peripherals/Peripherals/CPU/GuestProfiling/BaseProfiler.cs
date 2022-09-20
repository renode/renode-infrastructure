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
    public abstract class BaseProfiler : IDisposable
    {
        public BaseProfiler(TranslationCPU cpu, string filename, bool flushInstantly)
        {
            this.cpu = cpu;
            this.flushInstantly = flushInstantly;

            isFirstFrame = true;
            bufferLock = new Object();
            currentStack = new Stack<string>();
            wholeExecution = new Stack<Stack<string>>();
        }

        public virtual void Dispose()
        {
            FlushBuffer();
            currentStack.Clear();
            wholeExecution.Clear();
        }

        public abstract void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount);
        public abstract void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount);
        public abstract void InterruptEnter(ulong interruptIndex);
        public abstract void InterruptExit(ulong interruptIndex);
        public abstract void FlushBuffer();

        protected string GetSymbolName(ulong address)
        {
            if(!cpu.Bus.TryFindSymbolAt(address, out var name, out var _))
            {
                // Symbol not found - return address must serve as a symbol
                name = $"0x{address:X}";
            }
            return name;
        }

        protected readonly TranslationCPU cpu;
        protected readonly bool flushInstantly;
        protected readonly Object bufferLock;
        // Keeps track of the stacks prior to the current exception
        protected readonly Stack<Stack<string>> wholeExecution;

        protected Stack<string> currentStack;
        protected bool isFirstFrame;
    }
}
