//
// Copyright (c) 2010-2017 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class CoreLevelInterruptor : IDoubleWordPeripheral, IKnownSize
    {
        public CoreLevelInterruptor(Machine machine, long frequency)
        {
            this.machine = machine;
            IRQ = new GPIO();
            SoftwareIRQ = new GPIO();

            innerTimer = new LocalComparingTimer(machine, frequency, CompareAction);

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.MSipHart0, new DoubleWordRegister(this).WithFlag(0, writeCallback: (_, value) => { SoftwareIRQ.Set(value); })},
                {(long)Registers.MTimeCmpHart0Lo, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                    {
                        var limit = innerTimer.Compare;
                        limit &= ~0xffffffffUL;
                        limit |= value;

                        IRQ.Set(false);
                        innerTimer.Compare = limit;
                    })
                },
                {(long)Registers.MTimeCmpHart0Hi, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                    {
                        var limit = innerTimer.Compare;
                        limit &= 0xffffffffUL;
                        limit |= (ulong)value << 32;

                        IRQ.Set(false);
                        innerTimer.Compare = limit;
                    })
                },
                {(long)Registers.MTimeLo, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)innerTimer.Value, writeCallback: (_, value) =>
                    {
                        var timerValue = innerTimer.Value;
                        timerValue &= ~0xffffffffUL;
                        timerValue |= value;
                        innerTimer.Value = timerValue;
                    })
                },
                {(long)Registers.MTimeHi, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)(innerTimer.Value >> 32), writeCallback: (_, value) =>
                    {
                        var timerValue = innerTimer.Value;
                        timerValue &= 0xffffffffUL;
                        timerValue |= (ulong)value << 32;
                        innerTimer.Value = timerValue;
                    })
                }
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public void Reset()
        {
            registers.Reset();
            IRQ.Set(false);
            SoftwareIRQ.Set(false);
            innerTimer.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x10000;

        public GPIO IRQ { get; private set; }

        public GPIO SoftwareIRQ { get; private set; }

        private void CompareAction(ulong currentValue)
        {
            IRQ.Set(true);
        }

        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly LocalComparingTimer innerTimer;

        private enum Registers : long
        {
            MSipHart0 = 0x0,
            MTimeCmpHart0Lo = 0x4000,
            MTimeCmpHart0Hi = 0x4004,
            MTimeLo = 0xBFF8,
            MTimeHi = 0xBFFC
        }

        private class LocalComparingTimer : ComparingTimer
        {
            public LocalComparingTimer(Machine machine, long frequency, Action<ulong> compareAction)
                : base(machine, frequency, enabled: true)
            {
                this.compareAction = compareAction;
            }

            protected override void OnCompare()
            {
                compareAction(Value);
            }

            private readonly Action<ulong> compareAction;
        }
    }
}
