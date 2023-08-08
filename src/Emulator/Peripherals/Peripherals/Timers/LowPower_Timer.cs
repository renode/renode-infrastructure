//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.UART
{
    public class LowPower_Timer : LimitTimer, IDoubleWordPeripheral, IKnownSize
    {
        public LowPower_Timer(IMachine machine, long frequency = 8000000) : base(machine.ClockSource, frequency, null, "", eventEnabled: true)
        {
            IRQ = new GPIO();

            this.LimitReached += () =>
            {
                this.Log(LogLevel.Noisy, "IRQ set");
                IRQ.Set();
            };

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, name: "TimerEnable",
                            valueProviderCallback: _ => this.Enabled,
                            writeCallback: (_, val) =>
                            {
                                this.Enabled = val;
                            })
                    .WithFlag(7, name: "TimerCompareFlag",
                            writeCallback: (_, val) =>
                            {
                                if(val)
                                {
                                    IRQ.Set(false);
                                    this.Log(LogLevel.Noisy, "IRQ cleared");
                                }
                            })
                },

                {(long)Registers.Compare, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => (uint)this.Limit,
                            writeCallback: (_, val) =>
                            {
                                this.Limit = val;
                                this.Value = 0;
                            })
                },

                {(long)Registers.Counter, new DoubleWordRegister(this)
                    .WithValueField(0, 32, valueProviderCallback: _ => (uint)this.Value,
                            writeCallback: (_, val) =>
                            {
                                this.Value = val;
                            })
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Set(false);
            registers.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public GPIO IRQ { get; set; }

        public long Size => 0x10;

        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            Control = 0x0,
            Prescale = 0x4,
            Compare = 0x8,
            Counter = 0xC
        }
    }
}
