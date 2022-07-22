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
using Antmicro.Renode.Peripherals.CPU.GuestProfiling;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract partial class TranslationCPU
    {
        public void EnableProfiler(BaseProfiler.PorfilerType type, string filename, bool enableMultipleTracks = true, bool flushInstantly = false)
        {
            try
            {
                File.WriteAllText(filename, string.Empty);
            }
            catch (Exception e)
            {
                throw new RecoverableException($"There was an error when preparing the profiler output file {filename}: {e.Message}");
            }

            // Remove the old profiler if it was enabled
            if (profiler != null)
            {
                profiler.Dispose();
                ToggleProfilerInterruptHooks(false);
            }

            switch(type)
            {
                case BaseProfiler.PorfilerType.SPEEDSCOPE:
                    profiler = new SpeedscopeProfiler(this, filename, flushInstantly, enableMultipleTracks);
                    break;
                case BaseProfiler.PorfilerType.PERFETTO:
                    profiler = new PerfettoProfiler(this, filename, flushInstantly, enableMultipleTracks);
                    break;
                default:
                    throw new RecoverableException($"{type} is not a valid profiler output type");
            }

            ToggleProfilerInterruptHooks(true);
            TlibEnableGuestProfiler(1);
        }

        public void DisableProfiler()
        {
            if (profiler == null)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }
            else
            {
                profiler.Dispose();
                ToggleProfilerInterruptHooks(false);
                TlibEnableGuestProfiler(0);
            }
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
                profiler.StackFramePop(currentAddress, returnAddress, instructionsCount);
            }
        }

        [Export]
        protected void OnThreadChange(ulong threadId)
        {
            profiler.ThreadChange(threadId);
        }

        private void ToggleProfilerInterruptHooks(bool enabled)
        {
            if (profiler == null)
            {
                return;
            }

            if (enabled)
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
        private ActionInt32 TlibEnableGuestProfiler;
#pragma warning restore 649
    }
}

