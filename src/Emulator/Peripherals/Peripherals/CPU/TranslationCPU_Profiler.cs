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

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract partial class TranslationCPU
    {
        public void EnableProfiler(bool enable, string filename, bool flushInstantly = false)
        {
            if(enable)
            {
                profiler = new Profiler(this, filename, flushInstantly);
            }
            else
            {
                FlushProfiler();
            }
            TlibEnableGuestProfiler(enable ? 1 : 0);
        }

        public void FlushProfiler()
        {
            if(profiler == null)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }
            else
            {
                profiler.FlushBuffer();
            }
        }

        [Export]
        protected void OnStackChange(ulong currentAddress, ulong returnAddress, ulong instructionsCount, int isFrameAdd)
        {
            if(isFrameAdd != 0)
            {
                profiler.StackFrameAdd(currentAddress, returnAddress, instructionsCount);
            }
            else
            {
                profiler.StackFramePopAndLog(currentAddress, returnAddress, instructionsCount);
            }
        }

        private Profiler profiler;

#pragma warning disable 649
        [Import]
        private ActionInt32 TlibEnableGuestProfiler;
#pragma warning restore 649
    }

    public class Profiler: IDisposable
    {
        public Profiler(ICPUWithHooks cpu, string filename, bool flushInstantly)
        {
            this.cpu = cpu;
            this.filename = filename;
            this.flushInstantly = flushInstantly;

            currentStack = new Stack<StackFrame>();
            wholeExecution = new Stack<Stack<StackFrame>>();
            stringBuffer = new StringBuilder();
            lastInstructionsCount = UInt32.MaxValue;
            bufferLock = new Object();

            try
            {
                File.WriteAllText(filename, string.Empty);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"There was an error when preparing the profiler output file {filename}: {e.Message}");
            }

            cpu.AddHookAtInterruptBegin(_ => InterruptStackEnter());
            cpu.AddHookAtInterruptEnd(_ => InterruptStackExit());
        }

        public void Dispose()
        {
            FlushBuffer();
            currentStack = new Stack<StackFrame>();
            lastInstructionsCount = 0;
            wholeExecution.Clear();
        }

        public void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            var currentSymbol = GetSymbolFromAddr(currentAddress);
            cpu.Log(LogLevel.Debug, "Profiler: Pushing new frame with symbol: {0}; returnAddress: 0x{1:X}; currentAddress: 0x{2:X}", currentSymbol, returnAddress, currentAddress);

            if(lastInstructionsCount == UInt32.MaxValue)
            {
                lastInstructionsCount = 0;
                // used only to add the first frame which we cannot infer by the jump insn
                var prevSymbol = GetSymbolFromAddr(returnAddress - 4);
                var prevFrame = new StackFrame(prevSymbol, 0x0, instructionsCount);
                currentStack.Push(prevFrame);
            }

            var instructionsElapsed = GetInstructionsDelta(instructionsCount);
            AddStackToBufferWithDelta(instructionsElapsed);

            StackFrame newFrame = new StackFrame(currentSymbol, currentAddress, instructionsElapsed);
            currentStack.Push(newFrame);
        }

        public void StackFramePopAndLog(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
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

        public void FlushBuffer()
        {
            lock(bufferLock)
            {
                File.AppendAllText(filename, stringBuffer.ToString());
                stringBuffer.Clear();
            }
        }

        private void InterruptStackEnter()
        {
            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);

            wholeExecution.Push(currentStack);
            currentStack = new Stack<StackFrame>();
            cpu.Log(LogLevel.Debug, "Profiler: Interrupt entry (pc 0x{0:X})- saving the stack", cpu.PC);
        }

        private void InterruptStackExit()
        {
            var instructionsElapsed = GetInstructionsDelta(cpu.ExecutedInstructions);
            AddStackToBufferWithDelta(instructionsElapsed);

            cpu.Log(LogLevel.Debug, "Profiler: Interrupt exit - restoring the stack");
            if(wholeExecution.Count > 0)
            {
                currentStack = wholeExecution.Pop();
            }
            else
            {
                // In Zephyr the platform initialization in multithreaded apps ends with interrupt exit, some other RTOS'es may do the same
                currentStack = new Stack<StackFrame>();
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

        private string GetSymbolFromAddr(ulong address)
        {
            string outSymbol;
            if(cpu.Bus.TryFindSymbolAt(address, out var name, out var symbol))
            {
                outSymbol = symbol.ToStringRelative(address);
            }
            else
            {
                // Symbol not found - return address must serve as a symbol
                outSymbol = $"0x{address:X}";
            }
            return outSymbol;
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

        private string FormatCollapsedStackString(Stack<StackFrame> stack)
        {
            return string.Join(";", stack.Select(x => x.Symbol).Reverse());
        }

        private Stack<StackFrame> currentStack;
        private ulong lastInstructionsCount;

        // Keeps track of the stacks prior to the current exception
        private readonly Stack<Stack<StackFrame>> wholeExecution;
        private readonly ICPUWithHooks cpu;
        private readonly StringBuilder stringBuffer;
        private readonly string filename;
        private readonly bool flushInstantly;
        private Object bufferLock;

        private const int BufferFlushLevel = 1000000;

        private struct StackFrame
        {
            public StackFrame(string symbol, ulong returnAddress, ulong entryInstructionsCount)
            {
                Symbol = symbol;
                ReturnAddress = returnAddress;
                EntryInstructionsCount = entryInstructionsCount;
            }

            public string Symbol { get; }
            public ulong ReturnAddress { get; }
            public ulong EntryInstructionsCount { get; }
        }
    }
}

