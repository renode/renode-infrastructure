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
        public void EnableProfiler(ProfilerType type, string filename, bool flushInstantly = false, bool enableMultipleTracks = true)
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
                case ProfilerType.CollapsedStack:
                    profiler = new CollapsedStackProfiler(this, filename, flushInstantly);
                    break;
                case ProfilerType.Perfetto:
                    profiler = new PerfettoProfiler(this, filename, flushInstantly, enableMultipleTracks);
                    break;
                default:
                    throw new RecoverableException($"{type} is not a valid profiler output type");
            }

            ConfigureProfilerInterruptHooks(true);
            TlibEnableGuestProfiler(1);
        }

        public void EnableProfilerCollapsedStack(string filename, bool flushInstantly = false)
        {
            // CollapsedStack format doesn't support multiple tracks
            EnableProfiler(ProfilerType.CollapsedStack, filename, flushInstantly, false);
        }

        public void EnableProfilerPerfetto(string filename, bool flushInstantly = false, bool enableMultipleTracks = true)
        {
            EnableProfiler(ProfilerType.Perfetto, filename, flushInstantly, enableMultipleTracks);
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

        public enum ProfilerType
        {
            CollapsedStack,
            Perfetto
        }

        public BaseProfiler Profiler => profiler;

        [Export]
        protected void OnStackChange(ulong currentAddress, ulong returnAddress, ulong instructionsCount, int isFrameAdd)
        {
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
            profiler.OnContextChange(threadId);
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

        private BaseProfiler profiler;

#pragma warning disable 649
        [Import]
        private Action<int> TlibEnableGuestProfiler;
#pragma warning restore 649
    }
}
