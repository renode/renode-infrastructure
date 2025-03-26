//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if DEBUG
    // Uncomment for detailed log messages
    // #define GUEST_PROFILER_VERBOSE_DEBUG
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;


namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    public class FrameTrackingCollapsedStackProfiler : BaseProfiler
    {
        public FrameTrackingCollapsedStackProfiler(TranslationCPU cpu, string filename, bool flushInstantly, long? fileSizeLimit = null, int? maximumNestedContexts = null)
            : base(cpu, flushInstantly, maximumNestedContexts)
        {
            this.fileSizeLimit = fileSizeLimit;
            stringBuffer = new StringBuilder();
            fileStream = new StreamWriter(filename);
            profilerContext = new FrameProfilerContext(cpu);
        }

        public override void Dispose()
        {
            base.Dispose();
            fileStream.Close();
        }

        public override void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
        }

        public override void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
        }

        public override void OnContextChange(ulong newContextId)
        {
        }

        public override void OnStackPointerChange(ulong address, ulong oldPointerValue, ulong newPointerValue, ulong instructionsCount)
        {
            if(oldPointerValue == newPointerValue)
            {
                return;
            }

            var currentSymbol = GetSymbol(address);

            if(cpu.FrameProfilerIgnoredSymbols.Contains(currentSymbol.Name))
            {
#if GUEST_PROFILER_VERBOSE_DEBUG
                cpu.NoisyLog($"Guest profiler: Skipping ignored symbol {currentSymbol.Name}");
#endif
                return;
            }

            var isPush = newPointerValue < oldPointerValue;
            var isPop = !isPush;
#if GUEST_PROFILER_VERBOSE_DEBUG
            cpu.NoisyLog("Guest profiler: {0} {1}: address 0x{2:X}, old: 0x{3:X}, new: 0x{4:X}, inscount: 0x{5:X}",
                        isPush ? "PUSH" : "POP", currentSymbol.Name, address, oldPointerValue, newPointerValue, instructionsCount);
#endif

            var currentStack = profilerContext.FindCurrentStack(oldPointerValue, newPointerValue, isPush);

            if (isPush)
            {
                if(currentStack.PushFrame(currentSymbol, newPointerValue))
                {
                    var instructionsElapsed = GetInstructionsDelta(instructionsCount);
                    AddStackToBufferWithDelta(instructionsElapsed);
                }
            }
            else if (isPop && (currentStack != null) && currentStack.Count != 0) {
                if(currentStack.PopFrame(currentSymbol, newPointerValue))
                {
                    var instructionsElapsed = GetInstructionsDelta(instructionsCount);
                    AddStackToBufferWithDelta(instructionsElapsed);
                }

                profilerContext.RemoveEmptyStacks();
            }

            profilerContext.DumpAll();
        }

        public override void InterruptEnter(ulong interruptIndex)
        {
        }

        public override void InterruptExit(ulong interruptIndex)
        {
        }

        public override string GetCurrentStack()
        {
            var result = FormatCollapsedStackString(profilerContext.CurrentStack);

            return result;
        }

        public override void FlushBuffer()
        {
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

        private ulong GetInstructionsDelta(ulong currentInstructionsCount)
        {
            var instructionsElapsed = checked(currentInstructionsCount - lastInstructionsCount);
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

            var collapsedStack = FormatCollapsedStackString(profilerContext.CurrentStack);
            AddToBuffer($"{collapsedStack} {instructionDelta}");
        }

        private void AddToBuffer(string stringToAdd)
        {
            lock(bufferLock)
            {
                stringBuffer.AppendLine(stringToAdd);
                if(flushInstantly || stringBuffer.Length > BufferFlushLevel)
                {
                    FlushBuffer();
                }
            }
        }

        private string FormatCollapsedStackString(ProfilerStack stack)
        {
            var stackArr = stack.ToArray();
            var symbols = stackArr.Select(x => x.symbol.Name);
            return string.Join(";", symbols.Reverse());
        }

        private readonly StringBuilder stringBuffer;
        private readonly StreamWriter fileStream;
        private readonly long? fileSizeLimit;
        private long fileWrittenBytes;
        private bool isDisposing;
        private const int BufferFlushLevel = 1000000;
        private ulong lastInstructionsCount;

        private FrameProfilerContext profilerContext;

        private Symbol GetSymbol(ulong address)
        {
            if(!cpu.Bus.TryFindSymbolAt(address, out var name, out var symbol))
            {
                // Symbol not found - address must serve as the symbol
                symbol = new Symbol(address, address, $"0x{address:X}");
            }
            return symbol;
        }

        private class FrameProfilerContext
        {
            public FrameProfilerContext(TranslationCPU cpu)
            {
                profiledCPU = cpu;
                stacks = new List<ProfilerStack>();
                CurrentStack = new ProfilerStack(profiledCPU);
                stacks.Add(CurrentStack);
            }

            public void RemoveEmptyStacks()
            {
                for(int i = stacks.Count - 1; i >= 0; i--)
                {
                    if(stacks[i].Count == 0)
                    {
                        if(stacks[i] == CurrentStack) {
                            CurrentStack = null;
                        }
#if GUEST_PROFILER_VERBOSE_DEBUG
                        profiledCPU.DebugLog($"Guest profiler: removed stack {i}");
#endif
                        stacks.RemoveAt(i);
                    }
                }
            }

            public ProfilerStack FindCurrentStack(ulong oldSPValue, ulong spValue, bool isPush)
            {
                foreach(var stack in stacks)
                {
                    if(stack.BottomSP >= spValue && stack.TopSP <= spValue)
                    {
                        CurrentStack = stack;
                        return CurrentStack;
                    }
                }

                // No suitable stack found.
                if(isPush)
                {
#if GUEST_PROFILER_VERBOSE_DEBUG
                    profiledCPU.DebugLog("Guest profiler: creating a new stack.");
#endif
                    CurrentStack = new ProfilerStack(profiledCPU);
                    stacks.Add(CurrentStack);
                    return CurrentStack;
                }
                return null;
            }

            public void DumpAll()
            {
#if GUEST_PROFILER_VERBOSE_DEBUG
                profiledCPU.NoisyLog("========= ALL STACKS ===========");
                for(int i = 0; i < stacks.Count; i++)
                {
                    if(CurrentStack == stacks[i])
                    {
                        profiledCPU.NoisyLog("Guest profiler: stack #{0} [ACTIVE]", i);
                    }
                    else
                    {
                        profiledCPU.NoisyLog("Guest profiler: stack #{0}", i);
                    }
                    stacks[i].Dump();
                }
                profiledCPU.NoisyLog("================================");
#endif
            }

            public ProfilerStack CurrentStack;

            private readonly List<ProfilerStack> stacks;
            private TranslationCPU profiledCPU;
        }

        public class ProfilerStackFrame
        {
            public ProfilerStackFrame(Symbol symbol, ulong bottom, ulong top)
            {
                this.symbol = symbol;
                this.bottom = bottom;
                this.top = top;
            }

            public Symbol symbol;
            public ulong bottom;
            public ulong top;
        }

        private class ProfilerStack : Stack<ProfilerStackFrame>
        {
            public ProfilerStack(TranslationCPU cpu)
            {
                profiledCPU = cpu;
            }

            public bool PushFrame(Symbol symbol, ulong newSP)
            {
                var isCurrentStackInNewSymbol = Count == 0 || !this.Peek().symbol.Overlaps(symbol);
                if(!isCurrentStackInNewSymbol)
                {
                    return false;
                }

                if(Count == 0)
                {
                    BottomSP = newSP;
                    TopSP = BottomSP - GlueArea;
                }

                var bottom = Count == 0 ? newSP : this.Peek().top;
                if(bottom < newSP)
                {
                    return false;
                }
                this.Push(new ProfilerStackFrame(symbol, bottom, newSP));
                TopSP = newSP - GlueArea;

                return true;
            }

            public bool PopFrame(Symbol symbol, ulong newSP)
            {
                if(this.Count == 0)
                {
                    return false;
                }

                if(newSP < this.Peek().top || newSP > this.BottomSP)
                {
                    return false;
                }

                while(this.Count != 0 && this.Peek().bottom <= newSP)
                {
                    this.Pop();
                }

                this.TopSP = this.Count > 0 ? this.Peek().top - GlueArea : this.BottomSP - GlueArea;

                return true;
            }

            public void Dump()
            {
                profiledCPU.NoisyLog("Guest profiler: TopSP: 0x{0:X}", this.TopSP);
                foreach(var entry in this)
                {
                    profiledCPU.NoisyLog("Guest profiler: [0x{0:X},0x{1:X}]: {2}", entry.bottom, entry.top, entry.symbol);
                }
                profiledCPU.NoisyLog("Guest profiler: BottomSP: 0x{0:X}", this.BottomSP);
            }

            public ulong TopSP;
            public ulong BottomSP;
            private TranslationCPU profiledCPU;
            private const int GlueArea = 0x100;
        }
    }
}
