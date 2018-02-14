//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Timers
{
    // NOTE: this timer is not yet ready for multicore operation
    public sealed class CortexAGlobalTimer : ComparingTimer, IDoubleWordPeripheral, IKnownSize
    {
        public CortexAGlobalTimer(Machine machine, long frequency) : base(machine.ClockSource, frequency)
        {
            sysbus = machine.SystemBus;
            IRQ = new GPIO();
            controlRegister = new DoubleWordRegister(this);
            controlRegister.DefineFlagField(0, name: "Timer enable", writeCallback: (oldValue, newValue) => Enabled = newValue,
                valueProviderCallback: (oldValue) => Enabled);
            comparatorEnabled = controlRegister.DefineFlagField(1);
            interruptEnabled = controlRegister.DefineFlagField(2);
            autoIncrementEnabled = controlRegister.DefineFlagField(3);
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.Control:
                return controlRegister.Read();
            case Registers.Counter1:
                return (uint)Value;
            case Registers.Counter2:
                return (uint)(Value >> 32);
            case Registers.InterruptStatus:
                var valueToReturn = IRQ.IsSet;
                IRQ.Unset();
                return valueToReturn ? 1u : 0u;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.Control:
                controlRegister.Write(offset, value);
                break;
            case Registers.Counter1:
                Value = (Value & 0x7FFFFFFF00000000) | value;
                break;
            case Registers.Counter2:
                Value = (Value & 0x00000000FFFFFFFF) | ((ulong)value << 32);
                break;
            case Registers.ComparatorValue1:
                Compare = (Compare & 0x7FFFFFFF00000000) | value;
                break;
            case Registers.ComparatorValue2:
                Compare = (Compare & 0x00000000FFFFFFFF) | ((ulong)value << 32);
                break;
            case Registers.AutoIncrement:
                int currentCpuId;
                if(sysbus.TryGetCurrentCPUId(out currentCpuId))
                {
                    if(lastCpuId.HasValue && lastCpuId.Value != currentCpuId)
                    {
                        this.Log(LogLevel.Error, "Current version of Cortex A global timer does not support multicore operation.");
                    }
                    lastCpuId = currentCpuId;
                }
                autoIncrementValue = value;
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public long Size
        {
            get
            {
                return 0x100;
            }
        }

        public override void Reset()
        {
            base.Reset();
            autoIncrementValue = 0;
            lastCpuId = null;
            controlRegister.Reset();
        }

        public GPIO IRQ { get; private set; }

        protected override void OnCompareReached()
        {
            if(!comparatorEnabled.Value)
            {
                return;
            }
            if(autoIncrementEnabled.Value)
            {
                Compare += autoIncrementValue;
            }
            if(interruptEnabled.Value)
            {
                IRQ.Set();
            }
        }
            
        private int? lastCpuId;
        private uint autoIncrementValue;
        private readonly DoubleWordRegister controlRegister;
        private readonly IFlagRegisterField comparatorEnabled;
        private readonly IFlagRegisterField interruptEnabled;
        private readonly IFlagRegisterField autoIncrementEnabled;
        private readonly SystemBus sysbus;

        private enum Registers
        {
            Counter1 = 0x00,
            Counter2 = 0x04,
            Control = 0x08,
            InterruptStatus = 0x0C,
            ComparatorValue1 = 0x10,
            ComparatorValue2 = 0x14,
            AutoIncrement = 0x18
        }
    }
}

