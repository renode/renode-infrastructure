//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#if DEBUG
// Uncomment for detailed log messages
// #define GUEST_PROFILER_VERBOSE_DEBUG
#endif

using System.Collections.Generic;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    [Transient]
    public class FrameTrackingProfilerAdapter : BaseProfilerAdapter
    {
        public FrameTrackingProfilerAdapter(TranslationCPU cpu, BaseProfiler profiler)
            : base(cpu, profiler)
        {
            profilerContext = new FrameProfilerContext(cpu);
        }

        public override void OnCpuReset()
        {
            base.OnCpuReset();
            profilerContext = new FrameProfilerContext(cpu);
        }

        public override void OnStackPointerChange(ulong address, ulong oldPointerValue, ulong newPointerValue, ulong instructionsCount)
        {
            if(oldPointerValue == newPointerValue)
            {
                return;
            }

            var isPush = newPointerValue < oldPointerValue;
            var isPop = !isPush;
            var currentSymbol = GetSymbol(address);

            instructionsCount = OffsetInstructionsCount(instructionsCount);

            if(cpu.FrameProfilerIgnoredSymbols.Contains(currentSymbol.Name))
            {
#if GUEST_PROFILER_VERBOSE_DEBUG
                cpu.NoisyLog($"Guest profiler: Skipping ignored symbol {currentSymbol.Name}");
#endif
                return;
            }
#if GUEST_PROFILER_VERBOSE_DEBUG
            cpu.DebugLog("Profiler: {0} {1}: address 0x{2:X}, old: 0x{3:X}, new: 0x{4:X}, inscount: 0x{5:X}",
                        isPush ? "PUSH" : "POP", currentSymbol.Name, address, oldPointerValue, newPointerValue, instructionsCount);
#endif
            if(isFirstFrame)
            {
                // Initialize the profiler and push the first frame
                cpu.DebugLog("Profiler: Pushing first frame from address: 0x{0:X}", cpu.PC.RawValue);
                profiler.StackFramePush(currentSymbol.Name, OffsetInstructionsCount(0));
                profilerContext.InitializeStack(newPointerValue);
                isFirstFrame = false;
                profilerContext.DumpAll();
                return;
            }

            var previousStackId = profilerContext.CurrentStackId;
            var previousStack = profilerContext.CurrentStack;
            var (currentStackId, currentStack) = profilerContext.FindCurrentStack(newPointerValue);

            if(previousStackId != currentStackId)
            {
                cpu.DebugLog("Profiler: Changing context to context of index: {0}", currentStackId);
                profiler.ContextChange((ulong)currentStackId, instructionsCount);
            }
            else if(isPush)
            {
                if(currentStack.CanPushFrame(currentSymbol, newPointerValue))
                {
                    cpu.DebugLog("Profiler: Pushing new frame at address: 0x{0:X}", address);
                    profiler.StackFramePush(currentSymbol.Name, instructionsCount);
                    currentStack.PushFrame(currentSymbol, newPointerValue);
                }
            }
            else if(isPop && currentStack.Count != 0)
            {
                while(currentStack.CanPopFrame(newPointerValue))
                {
                    cpu.DebugLog("Profiler: Trying to pop a frame at address 0x{0:X}", address);
                    profiler.StackFramePop(instructionsCount);
                    currentStack.PopFrame();
                }
            }

            profilerContext.DumpAll();
        }

        protected Symbol GetSymbol(ulong address)
        {
            if(!cpu.Bus.TryFindSymbolAt(address, out var name, out var symbol, context: cpu))
            {
                // Symbol not found - address must serve as the symbol
                symbol = new Symbol(address, address, $"0x{address:X}");
            }
            else
            {
                // Name delivered by the name output variable is different to one in symbol variable
                symbol = new Symbol(symbol.Start, symbol.End, name);
            }
            return symbol;
        }

        private FrameProfilerContext profilerContext;

        public class ProfilerStackFrame
        {
            public ProfilerStackFrame(Symbol symbol, ulong bottom, ulong top)
            {
                this.Symbol = symbol;
                this.Bottom = bottom;
                this.Top = top;
            }

            public Symbol Symbol;
            public ulong Bottom;
            public ulong Top;
        }

        private class FrameProfilerContext
        {
            public FrameProfilerContext(TranslationCPU cpu)
            {
                profiledCPU = cpu;
                stacks = new List<ProfilerStack>();
            }

            public void InitializeStack(ulong spValue)
            {
                stacks.Add(new ProfilerStack(profiledCPU, spValue));
                UpdateCurrentStack(stacks.Count - 1);
            }

            public (int, ProfilerStack) FindCurrentStack(ulong spValue)
            {
                for(var i = 0; i < stacks.Count; i++)
                {
                    if(stacks[i].BottomSP >= spValue && stacks[i].TopSP <= spValue)
                    {
                        return UpdateCurrentStack(i);
                    }
                }

                // Create new stack if current stack wasn't found
                stacks.Add(new ProfilerStack(profiledCPU, spValue));
#if GUEST_PROFILER_VERBOSE_DEBUG
                profiledCPU.NoisyLog("Profiler: creating a new stack of id: {0}", stacks.Count - 1);
#endif
                return UpdateCurrentStack(stacks.Count - 1);
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
            public int CurrentStackId;

            private (int, ProfilerStack) UpdateCurrentStack(int index)
            {
                CurrentStack = stacks[index];
                CurrentStackId = index;
                return (CurrentStackId, CurrentStack);
            }

            private readonly TranslationCPU profiledCPU;

            private readonly List<ProfilerStack> stacks;
        }

        private class ProfilerStack : Stack<ProfilerStackFrame>
        {
            public ProfilerStack(TranslationCPU cpu)
            {
                profiledCPU = cpu;
            }

            public ProfilerStack(TranslationCPU cpu, ulong spValue)
            {
                profiledCPU = cpu;
                BottomSP = spValue + GlueArea;
                TopSP = spValue - GlueArea;
            }

            public bool CanPushFrame(Symbol symbol, ulong newSP)
            {
                var isCurrentStackInNewSymbol = Count == 0 || !this.Peek().Symbol.Overlaps(symbol);
                if(!isCurrentStackInNewSymbol)
                {
                    return false;
                }

                var bottom = Count == 0 ? BottomSP : this.Peek().Top;
                if(bottom < newSP)
                {
                    return false;
                }

                return true;
            }

            public bool PushFrame(Symbol symbol, ulong newSP)
            {
                if(Count == 0)
                {
                    BottomSP = newSP + GlueArea;
                }

                var bottom = Count == 0 ? BottomSP : this.Peek().Top;
                this.Push(new ProfilerStackFrame(symbol, bottom, newSP));
                TopSP = newSP - GlueArea;

                return true;
            }

            public bool CanPopFrame(ulong newSP)
            {
                if(this.Count == 0)
                {
                    return false;
                }

                if(newSP < this.Peek().Bottom || newSP > this.BottomSP)
                {
                    return false;
                }

                return true;
            }

            public ProfilerStackFrame PopFrame()
            {
                var frame = this.Pop();
                this.TopSP = this.Count > 0 ? this.Peek().Top - GlueArea : this.BottomSP - 2 * GlueArea;
                return frame;
            }

            public void Dump()
            {
                profiledCPU.NoisyLog("Profiler: TopSP: 0x{0:X}", this.TopSP);
                foreach(var entry in this)
                {
                    profiledCPU.NoisyLog("Profiler: [0x{0:X},0x{1:X}]: {2}", entry.Bottom, entry.Top, entry.Symbol);
                }
                profiledCPU.NoisyLog("Profiler: BottomSP: 0x{0:X}", this.BottomSP);
            }

            public ulong TopSP;
            public ulong BottomSP;
            private readonly TranslationCPU profiledCPU;
            private const int GlueArea = 0x150;
        }
    }
}