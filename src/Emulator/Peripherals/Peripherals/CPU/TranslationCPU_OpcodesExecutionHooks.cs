//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Binding;

namespace Antmicro.Renode.Peripherals.CPU
{
    public abstract partial class TranslationCPU
    {
        public void EnablePreOpcodeExecutionHooks(bool enable = true)
        {
            CheckArchitectureForOpcodeExecutionHooks();
            TlibEnablePreOpcodeExecutionHooks(enable == true ? 1u : 0u);
        }

        public void EnablePostOpcodeExecutionHooks(bool enable = true)
        {
            CheckArchitectureForOpcodeExecutionHooks();
            TlibEnablePostOpcodeExecutionHooks(enable == true ? 1u : 0u);
        }

        public void AddPreOpcodeExecutionHook(UInt64 mask, UInt64 value, Action<ulong, ulong> action)
        {
            var index = TlibInstallPreOpcodeExecutionHook(mask, value);
            if(index == UInt32.MaxValue)
            {
                throw new RecoverableException("Unable to register opcode hook. Maximum number of hooks already installed");
            }
            // Assert that the list index will match the one returned from the core
            if(index != preOpcodeExecutionHooks.Count)
            {
                throw new ApplicationException("Mismatch in the pre-execution opcode hooks on the C# and C side." +
                                                " One of them miss at least one element");
            }
            preOpcodeExecutionHooks.Add(action);
        }

        public void AddPostOpcodeExecutionHook(UInt64 mask, UInt64 value, Action<ulong, ulong> action)
        {
            var index = TlibInstallPostOpcodeExecutionHook(mask, value);
            if(index == UInt32.MaxValue)
            {
                throw new RecoverableException("Unable to register opcode hook. Maximum number of hooks already installed");
            }
            // Assert that the list index will match the one returned from the core
            if(index != postOpcodeExecutionHooks.Count)
            {
                throw new ApplicationException("Mismatch in the post-execution opcode hooks on the C# and C side." +
                                                " One of them miss at least one element");
            }
            postOpcodeExecutionHooks.Add(action);
        }

        private void CheckArchitectureForOpcodeExecutionHooks()
        {
            if((!Architecture.Contains("riscv") && !Architecture.Contains("arm")) || Architecture.Contains("arm64"))
            {
                throw new RecoverableException($"{Architecture} doesn't support opcode execution hooks");
            }
        }

        [Export]
        private void HandlePreOpcodeExecutionHook(UInt32 id, UInt64 pc, UInt64 opcode)
        {
            this.NoisyLog($"Got pre-opcode hook for opcode `0x{opcode:X}` with id {id} from PC {pc}");
            if(id < (uint)preOpcodeExecutionHooks.Count)
            {
                preOpcodeExecutionHooks[(int)id].Invoke(pc, opcode);
            }
            else
            {
                this.ErrorLog("Received pre-opcode hook for opcode `0x{0:X}` with non-existing id = {1}", opcode, id);
            }
        }

        [Export]
        private void HandlePostOpcodeExecutionHook(UInt32 id, UInt64 pc, UInt64 opcode)
        {
            this.NoisyLog($"Got post-opcode hook for opcode `0x{opcode:X}` with id {id} from PC {pc}");
            if(id < (uint)postOpcodeExecutionHooks.Count)
            {
                postOpcodeExecutionHooks[(int)id].Invoke(pc, opcode);
            }
            else
            {
                this.ErrorLog("Received post-opcode hook for opcode `0x{0:X}` with non-existing id = {1}", opcode, id);
            }
        }

        [Transient]
        private readonly List<Action<ulong, ulong>> preOpcodeExecutionHooks = new List<Action<ulong, ulong>>();

        [Transient]
        private readonly List<Action<ulong, ulong>> postOpcodeExecutionHooks = new List<Action<ulong, ulong>>();

#pragma warning disable 649
        [Import]
        private readonly Action<uint> TlibEnablePreOpcodeExecutionHooks;

        [Import]
        private readonly Func<ulong, ulong, uint> TlibInstallPreOpcodeExecutionHook;

        [Import]
        private readonly Action<uint> TlibEnablePostOpcodeExecutionHooks;

        [Import]
        private readonly Func<ulong, ulong, uint> TlibInstallPostOpcodeExecutionHook;
#pragma warning restore 649
    }
}