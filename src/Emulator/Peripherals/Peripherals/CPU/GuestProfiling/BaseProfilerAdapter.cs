//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    /// <summary>
    /// Adapter class takes care of translating data from tlib and passing it to the profiler.
    /// Tlib announces many different kinds of stack changes not all have to be handled by the adapter.
    /// Because of that all the possible announcements from tlib are already handled by default to do nothing to not break emulation.
    /// </summary>
    [Transient]
    public abstract class BaseProfilerAdapter
    {
        public BaseProfilerAdapter(TranslationCPU cpu, BaseProfiler profiler)
        {
            this.cpu = cpu;
            this.profiler = profiler;
            isFirstFrame = true;
        }

        public virtual void OnCpuReset()
        {
            instructionsOffset += cpu.ExecutedInstructions;
            isFirstFrame = true;
            profiler.CpuReset(instructionsOffset);
        }

        public virtual void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount) { }

        public virtual void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount) { }

        public virtual void OnContextChange(ulong newContextId) { }

        public virtual void OnStackPointerChange(ulong address, ulong oldPointerValue, ulong newPointerValue, ulong instructionsCount) { }

        public virtual void InterruptEnter(ulong interruptIndex) { }

        public virtual void InterruptExit(ulong interruptIndex) { }

        protected ulong OffsetInstructionsCount(ulong instructionsCount)
        {
            return instructionsOffset + instructionsCount;
        }

        protected bool isFirstFrame;

        protected ulong instructionsOffset;

        protected readonly TranslationCPU cpu;
        protected readonly BaseProfiler profiler;
    }
}