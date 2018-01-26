//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class CoreLevelInterruptor : IDoubleWordPeripheral, IKnownSize
    {
        public CoreLevelInterruptor(Machine machine, RiscV cpu)
        {
            this.machine = machine;
            this.cpu = cpu;
            IRQ = new GPIO();
            SoftwareIRQ = new GPIO();

            this.cpu.InnerTimer.CompareReached += () => IRQ.Set(true);

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.MSipHart0, new DoubleWordRegister(this).WithFlag(0, writeCallback: (_, value) => { SoftwareIRQ.Set(value); })},
                {(long)Registers.MTimeCmpHart0Lo, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                    {
                        var limit = cpu.InnerTimer.Compare;
                        limit &= ~0xffffffffUL;
                        limit |= value;

                        IRQ.Set(false);
                        cpu.InnerTimer.Compare = limit;
                    })
                },
                {(long)Registers.MTimeCmpHart0Hi, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                    {
                        var limit = cpu.InnerTimer.Compare;
                        limit &= 0xffffffffUL;
                        limit |= (ulong)value << 32;

                        IRQ.Set(false);
                        cpu.InnerTimer.Compare = limit;
                    })
                },
                {(long)Registers.MTimeLo, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)this.cpu.InnerTimer.Value, writeCallback: (_, value) =>
                    {
                        var timerValue = cpu.InnerTimer.Value;
                        timerValue &= ~0xffffffffUL;
                        timerValue |= value;
                        cpu.InnerTimer.Value = timerValue;
                    })
                },
                {(long)Registers.MTimeHi, new DoubleWordRegister(this).WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => (uint)(this.cpu.InnerTimer.Value >> 32), writeCallback: (_, value) =>
                    {
                        var timerValue = cpu.InnerTimer.Value;
                        timerValue &= 0xffffffffUL;
                        timerValue |= (ulong)value << 32;
                        cpu.InnerTimer.Value = timerValue;
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
            cpu.InnerTimer.Reset();
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

        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly RiscV cpu;

        private enum Registers : long
        {
            MSipHart0 = 0x0,
            MTimeCmpHart0Lo = 0x4000,
            MTimeCmpHart0Hi = 0x4004,
            MTimeLo = 0xBFF8,
            MTimeHi = 0xBFFC
        }
    }
}
