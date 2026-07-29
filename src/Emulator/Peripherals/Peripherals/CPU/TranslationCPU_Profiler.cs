//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU.GuestProfiling;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract partial class TranslationCPU
    {
        public void EnableProfiler(ProfilerType type, string filename, bool flushInstantly = false, bool enableMultipleTracks = true, ulong? fileSizeLimit = null, uint? maximumNestedContexts = null, bool enableFrameTracking = false)
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
                profiler = new PerfettoProfiler(this, filename, flushInstantly, fileSizeLimit, maximumNestedContexts, enableMultipleTracks);
                profilerAdapter = new ControlTrackingProfilerAdapter(this, profiler);
                break;
            case ProfilerType.CollapsedStack:
                if(enableFrameTracking)
                {
                    if(SupportsFrameTracking(type))
                    {
                        profiler = new CollapsedStackProfiler(this, filename, flushInstantly, fileSizeLimit, maximumNestedContexts);
                        profilerAdapter = new FrameTrackingProfilerAdapter(this, profiler);
                    }
                    else
                    {
                        throw new RecoverableException("Frame tracking method is not supported for this set of options.");
                    }
                }
                else
                {
                    profilerAdapter = new ControlTrackingProfilerAdapter(this, profiler);
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

        public void EnableProfilerCollapsedStack(string filename, bool flushInstantly = false, ulong? fileSizeLimit = null, uint? maximumNestedContexts = null, bool enableFrameTracking = false)
        {
            // CollapsedStack format doesn't support multiple tracks
            EnableProfiler(
                ProfilerType.CollapsedStack,
                filename,
                flushInstantly,
                enableMultipleTracks: false,
                fileSizeLimit,
                maximumNestedContexts,
                enableFrameTracking
            );
        }

        public void EnableProfilerPerfetto(string filename, bool flushInstantly = false, bool enableMultipleTracks = true, ulong? fileSizeLimit = null, uint? maximumNestedContexts = null, bool enableFrameTracking = false)
        {
            EnableProfiler(
                ProfilerType.Perfetto,
                filename,
                flushInstantly,
                enableMultipleTracks,
                fileSizeLimit,
                maximumNestedContexts,
                enableFrameTracking
            );
        }

        public void DisableProfiler()
        {
            if(!GuestProfilerEnabled)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }

            lock(profiler.MainLock)
            {
                // Other thread disposed of the profiler while waiting on a lock, return
                if(!GuestProfilerEnabled)
                {
                    return;
                }

                ConfigureProfilerInterruptHooks(false);
                TlibEnableGuestProfiler(0);
                profiler.Dispose();
                profiler = null;
                profilerAdapter = null;
            }
        }

        public void FlushProfiler()
        {
            if(profiler == null)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }

            profiler.Flush();
        }

        public string GetProfilerStack()
        {
            if(profiler == null)
            {
                throw new RecoverableException("The profiler is not enabled on this core");
            }

            return profiler.GetCurrentStack();
        }

        public bool GuestProfilerEnabled => profiler != null;

        public BaseProfiler Profiler => profiler;

        public BaseProfilerAdapter ProfilerAdapter => profilerAdapter;

        public HashSet<string> FrameProfilerIgnoredSymbols;

        protected virtual void InitFrameProfilerIgnoredSymbols()
        {
        }

        [Export]
        protected void OnStackChange(ulong currentAddress, ulong returnAddress, ulong instructionsCount, int isFrameAdd)
        {
            // It is possible to execute stack change announcement from a currently executed translation block
            // even after disabling the profiler and flushing the translation cache
            if(profilerAdapter == null)
            {
                return;
            }
            if(isFrameAdd != 0)
            {
                profilerAdapter.StackFrameAdd(currentAddress, returnAddress, instructionsCount);
            }
            else
            {
                profilerAdapter.StackFramePop(currentAddress, returnAddress, instructionsCount);
            }
        }

        [Export]
        protected void OnContextChange(ulong threadId)
        {
            if(profilerAdapter == null)
            {
                return;
            }
            profilerAdapter.OnContextChange(threadId);
        }

        [Export]
        protected void OnStackPointerChange(ulong address, ulong oldPointerValue, ulong newPointerValue, ulong instructionsCount)
        {
            if(profilerAdapter == null)
            {
                return;
            }
            profilerAdapter.OnStackPointerChange(address, oldPointerValue, newPointerValue, instructionsCount);
        }

        private void ConfigureProfilerInterruptHooks(bool enabled)
        {
            if(enabled)
            {
                this.AddHookAtInterruptBegin(profilerAdapter.InterruptEnter);
                this.AddHookAtInterruptEnd(profilerAdapter.InterruptExit);
            }
            else
            {
                this.RemoveHookAtInterruptBegin(profilerAdapter.InterruptEnter);
                this.RemoveHookAtInterruptEnd(profilerAdapter.InterruptExit);
            }
        }

        private bool SupportsFrameTracking(ProfilerType outputFormat)
        {
            bool isArm = Architecture.Contains("arm");
            bool isSparc = Architecture.Contains("sparc");
            bool isRiscV = Architecture.Contains("riscv");
            return outputFormat == ProfilerType.CollapsedStack && (isArm || isSparc || isRiscV);
        }

        private BaseProfiler profiler;
        private BaseProfilerAdapter profilerAdapter;

#pragma warning disable 649
        [Import]
        private readonly Action<int> TlibEnableGuestProfiler;
#pragma warning restore 649

        public enum ProfilerType
        {
            CollapsedStack,
            Perfetto
        }
    }
}