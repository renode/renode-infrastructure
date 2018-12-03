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
            for(var i = 0; i < innerTimer.Length; i++)
            {
                var j = i;
                innerTimer[i] = new LimitTimer(machine.ClockSource, frequency, eventEnabled: true, autoUpdate: true);
                innerTimer[i].LimitReached += delegate
                {
                    innerTimer[j].Limit = reloadValue[j].Value;
                    UpdateTimerEnabled();

                    irqPending.Value = true;
                    UpdateInterrupts();
                };
            }
        }

        public GPIO IRQ { get; }

        public long Size => 0x44;

        protected override void DefineRegisters()
        {
            for(var i = 0; i < TimersCount; i++)
            {
                var j = i;

                ((Registers)(Registers.Timer0Load + i * 0x4)).Define32(this)
                    .WithValueField(0, 32, name: "LOAD",
                        valueProviderCallback: _ => checked((uint)innerTimer[j].Value),
                        writeCallback: (_, val) =>
                        {
                            // as `AutoUpdate` is set, changing `Limit` automatically sets `Value` as well
                            innerTimer[j].Limit = val;
                            UpdateTimerEnabled();
                        })
                ;

                ((Registers)(Registers.Timer0Reload + i * 0x4)).Define32(this)
                    .WithValueField(0, 32, out reloadValue[j], name: "RELOAD")
                ;
            }

            Registers.TimerEnable.Define32(this)
                .WithFlag(0, out timerEnabled, name: "ENABLE", writeCallback: (_, val) => UpdateTimerEnabled())
            ;

            Registers.EventPending.Define32(this)
                .WithFlag(0, out irqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "EV_PENDING", changeCallback: (_, __) => UpdateInterrupts())
            ;

            Registers.EventEnable.Define32(this)
                .WithFlag(0, out irqEnabled, name: "EV_ENABLE", changeCallback: (_, __) => UpdateInterrupts())
            ;
        }

        private void UpdateTimerEnabled()
        {
            foreach(var timer in innerTimer)
            {
                timer.Enabled = timerEnabled.Value && (timer.Limit != 0);
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(irqPending.Value && irqEnabled.Value);
        }

        // TODO: this field be set here (and not in the constructor), as `DefineRegisters` method uses it; think about the architecture of `BasicDoubleWordPeripheral`
        private IValueRegisterField[] reloadValue = new IValueRegisterField[TimersCount];
        private IFlagRegisterField irqEnabled;
        private IFlagRegisterField irqPending;
        private IFlagRegisterField timerEnabled;

        private LimitTimer[] innerTimer;

        private const int TimersCount = 4;

        private enum Registers
        {
            Timer0Load = 0x0,
            Timer0Reload = 0x10,
            TimerEnable = 0x20,
            EventPending = 0x3c,
            EventEnable = 0x40
        }
    }
}