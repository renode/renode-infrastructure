//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public abstract class BaseProfiler : IDisposable
    {
        public BaseProfiler(TranslationCPU cpu, string filename, bool flushInstantly, bool enableMultipleTracks)
        {
            this.cpu = cpu;
            this.filename = filename;
            this.flushInstantly = flushInstantly;
            this.enableMultipleTracks = enableMultipleTracks;
            threadId = 0;

            currentStack = new Stack<string>();
            wholeExecution = new Dictionary<ulong, Stack<Stack<string>>>();
        }

        public virtual void Dispose()
        {
            FlushBuffer();
            currentStack = new Stack<string>();
            wholeExecution.Clear();
        }

        public abstract void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount);
        public abstract void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount);
        public abstract void ThreadChange(ulong threadId);
        public abstract void InterruptEnter(ulong interruptIndex);
        public abstract void InterruptExit(ulong interruptIndex);
        public abstract void FlushBuffer();

        protected string GetSymbolFromAddr(ulong address)
        {
            string outSymbol;
            if (cpu.Bus.TryFindSymbolAt(address, out var name, out var symbol))
            {
                outSymbol = name;
            }
            else
            {
                // Symbol not found - return address must serve as a symbol
                outSymbol = $"0x{address:X}";
            }
            return outSymbol;
        }

        protected readonly TranslationCPU cpu;
        protected readonly string filename;
        protected readonly bool flushInstantly;
        protected readonly bool enableMultipleTracks;

        protected Stack<string> currentStack;
        // Keeps track of the stacks prior to the current exception
        protected readonly Dictionary<ulong, Stack<Stack<string>>> wholeExecution;
        protected ulong threadId;

        public enum PorfilerType
        {
            SPEEDSCOPE,
            PERFETTO
        }
    }
}
