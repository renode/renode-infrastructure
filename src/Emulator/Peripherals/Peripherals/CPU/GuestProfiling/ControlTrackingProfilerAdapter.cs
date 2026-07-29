//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Migrant;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU.GuestProfiling
{
    [Transient]
    public class ControlTrackingProfilerAdapter : BaseProfilerAdapter
    {
        public ControlTrackingProfilerAdapter(TranslationCPU cpu, BaseProfiler profiler)
            : base(cpu, profiler) { }

        public override void StackFrameAdd(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            if(isFirstFrame)
            {
                // We have to add the first frame, from which the first jump happened
                // To infer this frame we use the current PC value, which should point to the jump instruction that triggered this event
                var firstSymbol = GetSymbolName(cpu.PC.RawValue);
                cpu.DebugLog("Profiler: Pushing first frame from address: 0x{0:X}", cpu.PC.RawValue);
                profiler.StackFramePush(firstSymbol, OffsetInstructionsCount(0));
                isFirstFrame = false;
            }

            var currentSymbol = GetSymbolName(currentAddress);
            cpu.DebugLog("Profiler: Pushing new frame at address: 0x{0:X}", currentAddress);
            profiler.StackFramePush(currentSymbol, OffsetInstructionsCount(instructionsCount));
        }

        public override void StackFramePop(ulong currentAddress, ulong returnAddress, ulong instructionsCount)
        {
            isFirstFrame = false;
            cpu.DebugLog("Profiler: Trying to pop a frame at address 0x{0:X}", currentAddress);
            profiler.StackFramePop(OffsetInstructionsCount(instructionsCount));
        }

        public override void OnContextChange(ulong newContextId)
        {
            isFirstFrame = false;
            cpu.DebugLog("Profiler: Changing context to context of index: {0}", newContextId);
            profiler.ContextChange(newContextId, OffsetInstructionsCount(cpu.ExecutedInstructions));
        }

        public override void InterruptEnter(ulong interruptIndex)
        {
            isFirstFrame = false;
            cpu.DebugLog("Profiler: Entering interrupt of index: {0}", interruptIndex);
            profiler.InterruptEnter(OffsetInstructionsCount(cpu.ExecutedInstructions));
        }

        public override void InterruptExit(ulong interruptIndex)
        {
            isFirstFrame = false;
            cpu.DebugLog("Profiler: Exiting interrupt of index: {0}", interruptIndex);
            profiler.InterruptExit(OffsetInstructionsCount(cpu.ExecutedInstructions));
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
    }
}