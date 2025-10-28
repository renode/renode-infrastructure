//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.CPU
{
    abstract class HookDescriptorBase
    {
        public HookDescriptorBase(ICpuSupportingGdb cpu)
        {
            this.cpu = cpu;
            hooks = new Dictionary<ulong, Hook>();
        }

        public void AddHook(ulong addr, CpuAddressHook callback)
        {
            lock(hooks)
            {
                if(!hooks.ContainsKey(addr))
                {
                    hooks[addr] = new Hook(addr, callback);
                }

                hooks[addr].Callbacks.Add(callback);
                Logger.DebugLog(cpu, "Added hook @ 0x{0:X}", addr);
            }
        }

        public void ActivateNewHooks()
        {
            lock(hooks)
            {
                foreach(var newHook in hooks.Where(x => x.Value.IsNew))
                {
                    Activate(newHook.Value);
                }
            }
        }

        public void DeactivateHooks(ulong address)
        {
            lock(hooks)
            {
                Hook hook;
                if(!hooks.TryGetValue(address, out hook))
                {
                    return;
                }
                Deactivate(hook);
                isAnyInactive = true;
                HookStateChangedCallback();
            }
        }

        public void RemoveHook(ulong addr, CpuAddressHook callback)
        {
            lock(hooks)
            {
                Hook hook;
                if(!hooks.TryGetValue(addr, out hook) || !RemoveCallback(hook, callback))
                {
                    Logger.WarningLog(cpu, "Tried to remove a nonexistent hook from address 0x{0:x}", addr);
                    return;
                }
                if(!hook.Callbacks.Any())
                {
                    hooks.Remove(addr);
                }
                if(!hooks.Any(x => !x.Value.IsActive))
                {
                    isAnyInactive = false;
                }

                HookStateChangedCallback();
            }
        }

        public void RemoveHooksAt(ulong addr)
        {
            lock(hooks)
            {
                if(hooks.Remove(addr))
                {
                    RemoveBreakpoint(addr);
                }
                if(!hooks.Any(x => !x.Value.IsActive))
                {
                    isAnyInactive = false;
                }

                HookStateChangedCallback();
            }
        }

        public void RemoveHooks(CpuAddressHook callback)
        {
            lock(hooks)
            {
                foreach(var kvp in hooks.ToList())
                {
                    var addr = kvp.Key;
                    var hook = kvp.Value;

                    RemoveCallback(hook, callback);
                    if(!hook.Callbacks.Any())
                    {
                        hooks.Remove(addr);
                    }
                }
                if(!hooks.Any(x => !x.Value.IsActive))
                {
                    isAnyInactive = false;
                }
                HookStateChangedCallback();
            }
        }

        public void RemoveAllHooks()
        {
            lock(hooks)
            {
                foreach(var hook in hooks)
                {
                    RemoveBreakpoint(hook.Key);
                }
                isAnyInactive = false;
                hooks.Clear();
                HookStateChangedCallback();
            }
        }

        public void Reactivate()
        {
            lock(hooks)
            {
                foreach(var inactive in hooks.Where(x => !x.Value.IsActive))
                {
                    Activate(inactive.Value);
                }
                isAnyInactive = false;
                HookStateChangedCallback();
            }
        }

        public void Execute(ulong address)
        {
            lock(hooks)
            {
                Hook hook;
                if(!hooks.TryGetValue(address, out hook))
                {
                    return;
                }

                Logger.DebugLog(cpu, "Executing hooks registered at address 0x{0:X8}", address);
                foreach(var callback in hook.Callbacks.ToList())
                {
                    callback(cpu, address);
                }
            }
        }

        public bool IsAnyInactive => isAnyInactive;

        // Called with hook lock after removal, deactivation or re-enabling of a hook
        protected virtual void HookStateChangedCallback() { }

        protected abstract void AddBreakpoint(ulong address);

        protected abstract void RemoveBreakpoint(ulong address);

        protected readonly ICpuSupportingGdb cpu;

        private bool RemoveCallback(Hook hook, CpuAddressHook action)
        {
            var result = hook.Callbacks.Remove(action);
            if(result && !hook.Callbacks.Any())
            {
                Deactivate(hook);
            }
            return result;
        }

        private void Activate(Hook hook)
        {
            if(hook.IsActive)
            {
                return;
            }

            AddBreakpoint(hook.Address);
            hook.IsActive = true;
            hook.IsNew = false;
        }

        private void Deactivate(Hook hook)
        {
            if(!hook.IsActive)
            {
                return;
            }

            RemoveBreakpoint(hook.Address);
            hook.IsActive = false;
        }

        private readonly Dictionary<ulong, Hook> hooks;

        bool isAnyInactive;

        protected class Hook
        {
            public Hook(ulong address, CpuAddressHook callback)
            {
                IsActive = false;
                IsNew = true;
                Address = address;
                Callbacks = new HashSet<CpuAddressHook>();
                Callbacks.Add(callback);
            }

            public bool IsActive;
            public bool IsNew;
            public ulong Address;
            public readonly HashSet<CpuAddressHook> Callbacks;
        }
    }
}
