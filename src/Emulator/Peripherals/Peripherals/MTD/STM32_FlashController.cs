//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MTD
{
    public abstract class STM32_FlashController : BasicDoubleWordPeripheral
    {
        public STM32_FlashController(IMachine machine) : base(machine)
        {
        }

        protected class LockRegister
        {
            public LockRegister(STM32_FlashController owner, string name, uint[] keys, bool unlockedAfterReset = false)
            {
                this.owner = owner;
                this.name = name;
                this.keys = keys;
                this.unlockedAfterReset = unlockedAfterReset;
                IsLocked = this.unlockedAfterReset ? false : true;
            }

            public void ConsumeValue(uint value)
            {
                owner.Log(LogLevel.Debug, "Lock {0} received 0x{1:x8}", name, value);
                if(DisabledUntilReset)
                {
                    owner.Log(LogLevel.Debug, "Lock {0} is disabled until reset, ignoring", name);
                    return;
                }

                if(keyIndex >= keys.Length || keys[keyIndex] != value)
                {
                    owner.Log(LogLevel.Debug, "Lock {0} now disabled until reset after bad write", name);
                    Lock();
                    DisabledUntilReset = true;
                    return;
                }

                if(++keyIndex == keys.Length)
                {
                    IsLocked = false;
                    owner.Log(LogLevel.Debug, "Lock {0} unlocked", name);
                }
            }

            public void Lock()
            {
                if(IsLocked)
                {
                    return;
                }

                owner.Log(LogLevel.Debug, "Lock {0} locked", name);
                IsLocked = true;
                keyIndex = 0;
                Locked?.Invoke();
            }

            public void Reset()
            {
                if(unlockedAfterReset)
                {
                    IsLocked = false;
                }
                else
                {
                    Lock();
                }
                DisabledUntilReset = false;
            }

            public bool IsLocked { get; private set; }
            public bool DisabledUntilReset { get; private set; }
            public event Action Locked;

            private readonly STM32_FlashController owner;
            private readonly string name;
            private readonly uint[] keys;
            private readonly bool unlockedAfterReset;
            private int keyIndex;
        }
    }
}
