//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class LitexTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public LitexTimer(Machine machine, long frequency = 1000000) : base(machine)
        {
            IRQ = new GPIO();
            innerTimer = new LimitTimer[TimersCount];
            // reloadValue = new IValueRegisterField[TimersCount];
            for(var i = 0; i < innerTimer.Length; i++)
            {
                var j = i;
                innerTimer[i] = new LimitTimer(machine.ClockSource, frequency, eventEnabled: true);
                innerTimer[i].LimitReached += delegate
                {
                    if(reloadValue[j].Value != 0)
                    {
                        innerTimer[j].Value = reloadValue[j].Value;
                        innerTimer[j].Limit = reloadValue[j].Value;
                    }
                    if(irqEnabled.Value)
                    {
                        IRQ.Set();
                    }
                };
            }
        }

        public void ToggleIRQ()
        {
            IRQ.Toggle();
        }

        public GPIO IRQ { get; }

        public long Size => 0x44;


        protected override void DefineRegisters()
        {
            Registers.TIMER_EV_PENDING_ADDR.Define32(this)
                .WithFlag(0, writeCallback: (_, val) =>
                    {
                        if(!val)
                        {
                            return;
                        }

                        IRQ.Set(false);
                    })
            ;

            Registers.TIMER_EV_ENABLE_ADDR.Define32(this)
                .WithFlag(0, out irqEnabled, name: "EV_ENABLE");

            for(var i = 0; i < TimersCount; i++)
            {
                var j = i;

                ((Registers)(Registers.Timer0Load + i * 0x4)).Define32(this)
                    .WithValueField(0, 32, name: "LOAD",
                    writeCallback: (_, val) =>
                    {
                        innerTimer[j].Value = val;
                        innerTimer[j].Limit = val;
                    });

                ((Registers)(Registers.Timer0Reload + i * 0x4)).Define32(this)
                    .WithValueField(0, 32, out reloadValue[j], name: "RELOAD");

            }

            Registers.Timer0Enable.Define32(this)
                .WithFlag(0, writeCallback: (_, val) =>
                {
                    foreach(var timer in innerTimer)
                    {
                        if(timer.Value != 0 && timer.Limit != 0)
                        {
                            timer.Enabled = val;
                        }
                    }
                });
        }

        private IValueRegisterField[] reloadValue = new IValueRegisterField[TimersCount];
        private IFlagRegisterField irqEnabled;

        private LimitTimer[] innerTimer;

        private const int TimersCount = 4;

        private enum Registers
        {
            Timer0Load = 0x0,
            Timer0Reload = 0x10,
            Timer0Enable = 0x20,
            TIMER_EV_PENDING_ADDR = 0x3c,
            TIMER_EV_ENABLE_ADDR = 0x40
        }
    }
}