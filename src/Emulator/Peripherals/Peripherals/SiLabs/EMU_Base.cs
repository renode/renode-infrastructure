//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public class EmuBase : IEmulationElement, IEmu
    {
        public EmuBase(IMachine machine)
        {
            this.machine = machine;

            this.WFIStateChanged = this.OnWFIStateChanged;
            this.isWFIHookAdded = false;
        }

        public void AddEnterDeepSleepHook(Action hook)
        {
            AddWFIHook();
            EnterDeepSleep += hook;
        }

        public void AddExitDeepSleepHook(Action hook)
        {
            AddWFIHook();
            ExitDeepSleep += hook;
        }

        private Action<bool> WFIStateChanged;
        private void OnWFIStateChanged(bool WFI)
        {
            if (((dynamic)nvic).DeepSleepEnabled) {
                if (WFI) {
                    EnterDeepSleep?.Invoke();
                } else {
                    ExitDeepSleep?.Invoke();
                }
            }
        }

        public void AddWFIHook() {
            if (!isWFIHookAdded) {
                cpuWithHooks.AddHookAtWfiStateChange(WFIStateChanged);
            }
            isWFIHookAdded = true;
        }

        // A reference to the CPU to hook to the WFI instruction.
        private ICPUWithHooks _cpuWithHooks;
        private ICPUWithHooks cpuWithHooks
        {
            get
            {
                if (Object.ReferenceEquals(_cpuWithHooks, null))
                {
                    foreach(var cpuWithHooks in machine.SystemBus.GetCPUs())
                    {
                        if (cpuWithHooks.GetType().FullName == "Antmicro.Renode.Peripherals.CPU.CortexM") {
                            _cpuWithHooks = (ICPUWithHooks)cpuWithHooks;
                        }
                    }
                }
                return _cpuWithHooks;
            }
            set
            {
                _cpuWithHooks = value;
            }
        }

        // A reference to the NVIC.
        private IDoubleWordPeripheral _nvic;
        private IDoubleWordPeripheral nvic
        {
            get
            {
                if (Object.ReferenceEquals(_nvic, null))
                {
                    foreach(var nvic in machine.GetPeripheralsOfType<IDoubleWordPeripheral>())
                    {
                        if (nvic.GetType().FullName == "Antmicro.Renode.Peripherals.IRQControllers.NVIC") {
                            _nvic = nvic;
                        }
                    }
                }
                return _nvic;
            }
            set
            {
                _nvic = value;
            }
        }

        private bool isWFIHookAdded;
        protected event Action EnterDeepSleep;
        protected event Action ExitDeepSleep;
        private readonly IMachine machine;
    }
}