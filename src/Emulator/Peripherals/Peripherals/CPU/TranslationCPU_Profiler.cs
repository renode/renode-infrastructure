//
// Copyright (c) 2010-2025 Antmicro
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
using Antmicro.Renode.Peripherals.CPU.GuestProfiling;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract partial class TranslationCPU
    {
        public void EnableProfiler(ProfilerType type, string filename, bool flushInstantly = false, bool enableMultipleTracks = true, long? fileSizeLimit = null, int? maximumNestedContexts = null, bool enableFrameTracking = false)
        {
            // Remove the old profiler if it was enabled
            if(profiler != null)
            {
                ConfigureProfilerInterruptHooks(false);
                profiler.Dispose();
            }

            try
            {
                File.WriteAllText(filename, string.Empty);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"There was an error when preparing the profiler output file {filename}: {e.Message}");
            }

            switch(type)
            {
                case ProfilerType.Perfetto:
                    profiler = new PerfettoProfiler(this, filename, flushInstantly, enableMultipleTracks, fileSizeLimit, maximumNestedContexts);
                    break;
                case ProfilerType.CollapsedStack:
                    if(enableFrameTracking)
                    {
                        if(SupportsFrameTracking(type))
                        {
                            profiler = new FrameTrackingCollapsedStackProfiler(this, filename, flushInstantly, fileSizeLimit, maximumNestedContexts);
                        }
                        else
                        {
                            throw new RecoverableException("Frame tracking method is not supported for this set of options.");
                        }
                    }
                    else
                    {
                        profiler = new ControlTrackingCollapsedStackProfiler(this, filename, flushInstantly, fileSizeLimit, maximumNestedContexts);
                    }
                    break;
                default:
                    throw new RecoverableException($"{type} is not a valid profiler output type");
            }

            FrameProfilerIgnoredSymbols = new HashSet<string>();
            InitFrameProfilerIgnoredSymbols();

            ConfigureProfilerInterruptHooks(true);
            TlibEnableGuestProfiler(1);
        }

        public void EnableProfilerCollapsedStack(string filename, bool flushInstantly = false, long? fileSizeLimit = null, int? maximumNestedContexts = null, bool enableFrameTracking = false)
        {
            // CollapsedStack format doesn't support multiple tracks
            EnableProfiler(ProfilerType.CollapsedStack, filename, flushInstantly, false, fileSizeLimit, maximumNestedContexts, enableFrameTracking);
        }

        public void EnableProfilerPerfetto(string filename, bool flushInstantly = false, bool enableMultipleTracks = true, long? fileSizeLimit = null, int? maximumNestedContexts = null)
        {
            EnableProfiler(ProfilerType.Perfetto, filename, flushInstantly, enableMultipleTracks, fileSizeLimit, maximumNestedContexts);
        }

        public void DisableProfiler()
        {
            if(profiler == null)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }

            profiler.Dispose();
            ConfigureProfilerInterruptHooks(false);
            TlibEnableGuestProfiler(0);
            profiler = null;
        }

        public void FlushProfiler()
        {
            if(profiler == null)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }

            profiler.FlushBuffer();
        }

        public string GetProfilerStack()
        {
            if(profiler == null)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }

            return profiler.GetCurrentStack();
        }

        public enum ProfilerType
        {
            CollapsedStack,
            Perfetto
        }

        public BaseProfiler Profiler => profiler;

        protected virtual void InitFrameProfilerIgnoredSymbols()
        {
        }

        [Export]
        protected void OnStackChange(ulong currentAddress, ulong returnAddress, ulong instructionsCount, int isFrameAdd)
        {
            // It is possible to execute stack change announcement from a currently executed translation block
            // even after disabling the profiler and flushing the translation cache
            if(profiler == null)
            {
                return;
            }
            if(isFrameAdd != 0)
            {
                profiler.StackFrameAdd(currentAddress, returnAddress, instructionsCount);
            }
            else
            {
                profiler.StackFramePop(currentAddress, returnAddress, instructionsCount);
            }
        }

        [Export]
        protected void OnContextChange(ulong threadId)
        {
            if(profiler == null)
            {
                return;
            }
            profiler.OnContextChange(threadId);
        }

        [Export]
        protected void OnStackPointerChange(ulong address, ulong oldPointerValue, ulong newPointerValue, ulong instructionsCount)
        {
            if(profiler == null)
            {
                return;
            }
            profiler.OnStackPointerChange(address, oldPointerValue, newPointerValue, instructionsCount);
        }

        private void ConfigureProfilerInterruptHooks(bool enabled)
        {
            if(enabled)
            {
                this.AddHookAtInterruptBegin(profiler.InterruptEnter);
                this.AddHookAtInterruptEnd(profiler.InterruptExit);
            }
            else
            {
                this.RemoveHookAtInterruptBegin(profiler.InterruptEnter);
                this.RemoveHookAtInterruptEnd(profiler.InterruptExit);
            }
        }

        private bool SupportsFrameTracking(ProfilerType outputFormat)
        {
            bool isArm = Architecture.Contains("arm");
            bool isSparc = Architecture.Contains("sparc");
            bool isRiscV = Architecture.Contains("riscv");
            return outputFormat == ProfilerType.CollapsedStack && (isArm || isSparc || isRiscV);
        }

        public HashSet<string> FrameProfilerIgnoredSymbols;

        private BaseProfiler profiler;

#pragma warning disable 649
        [Import]
        private Action<int> TlibEnableGuestProfiler;
#pragma warning restore 649
    }
}
