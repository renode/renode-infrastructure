//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
    // this is a model of LiteX timer in the default configuration:
    // * width: 32 bits
    // * csr data width: 32 bit
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class LiteX_Timer_CSR32 : BasicDoubleWordPeripheral, IKnownSize
    {
        public LiteX_Timer_CSR32(IMachine machine, long frequency) : base(machine)
        {
            uptimeTimer = new LimitTimer(machine.ClockSource, frequency, this, nameof(uptimeTimer), direction: Antmicro.Renode.Time.Direction.Ascending, enabled: true);
            innerTimer = new LimitTimer(machine.ClockSource, frequency, this, nameof(innerTimer), eventEnabled: true, autoUpdate: true);
            innerTimer.LimitReached += delegate
            {
                this.Log(LogLevel.Noisy, "Limit reached");
                irqPending.Value = true;
                UpdateInterrupts();

                if(reloadValue.Value == 0)
                {
                    this.Log(LogLevel.Noisy, "No realod value - disabling the timer");
                    innerTimer.Enabled = false;
                }
                innerTimer.Limit = reloadValue.Value;
            };
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            innerTimer.Reset();
            uptimeTimer.Reset();

            latchedValue = 0;
            uptimeLatchedValue = 0;

            UpdateInterrupts();
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x2c;

        private void DefineRegisters()
        {
            Registers.Load.Define(this)
                .WithValueField(0, 32, out loadValue, name: "LOAD")
            ;

            Registers.Reload.Define(this)
                .WithValueField(0, 32, out reloadValue, name: "RELOAD")
            ;

            Registers.TimerEnable.Define32(this)
                .WithFlag(0, name: "ENABLE", writeCallback: (_, val) =>
                {
                    if(innerTimer.Enabled == val)
                    {
                        return;
                    }

                    if(val)
                    {
                        innerTimer.Limit = loadValue.Value;
                        this.Log(LogLevel.Noisy, "Enabling timer. Load value: 0x{0:X}, reload value: 0x{1:X}", loadValue.Value, reloadValue.Value);
                    }

                    innerTimer.Enabled = val;
                })
            ;

            Registers.TimerUpdateValue.Define32(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "UPDATE_VALUE", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            // being here means we are on the CPU thread
                            cpu.SyncTime();
                        }

                        latchedValue = (uint)innerTimer.Value;
                    }
                });
            ;

            Registers.Value.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: $"VALUE", valueProviderCallback: _ => latchedValue)
            ;

            Registers.EventStatus.Define32(this)
                .WithFlag(0, FieldMode.Read, name: "EV_STATUS", valueProviderCallback: _ => innerTimer.Value == 0)
            ;

            Registers.EventPending.Define32(this)
                .WithFlag(0, out irqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "EV_PENDING", changeCallback: (_, __) => UpdateInterrupts())
            ;

            Registers.EventEnable.Define32(this)
                .WithFlag(0, out irqEnabled, name: "EV_ENABLE", changeCallback: (_, __) => UpdateInterrupts())
            ;

            Registers.UptimeLatch.Define32(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "UPTIME_LATCH", writeCallback: (_, val) =>
                {
                    if(val)
                    {
                        if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                        {
                            // being here means we are on the CPU thread
                            cpu.SyncTime();
                        }

                        uptimeLatchedValue = uptimeTimer.Value;
                    }
                });


             // UPTIME_CYCLES0 contains most significant 32 bits
             // UPTIME_CYCLES1 contains least significant 32 bits
             Registers.UptimeCycles0.DefineMany(this, 2, (reg, idx) =>
             {
                 reg.WithValueField(0, 32, FieldMode.Read, name: $"UPTIME_CYCLES{idx}", valueProviderCallback: _ =>
                 {
                     return (uint)BitHelper.GetValue(uptimeLatchedValue, 32 - idx * 32, 32);
                 }); 
             });
        }

        private void UpdateInterrupts()
        {
            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", irqPending.Value && irqEnabled.Value);
            IRQ.Set(irqPending.Value && irqEnabled.Value);
        }

        private IFlagRegisterField irqEnabled;
        private IFlagRegisterField irqPending;
        private IValueRegisterField loadValue;
        private IValueRegisterField reloadValue;

        private uint latchedValue;
        private ulong uptimeLatchedValue;

        private readonly LimitTimer innerTimer;
        private readonly LimitTimer uptimeTimer;

        private enum Registers
        {
            Load = 0x0,
            Reload = 0x4,
            TimerEnable = 0x08,
            TimerUpdateValue = 0x0C,
            Value = 0x10,
            EventStatus = 0x14,
            EventPending = 0x18,
            EventEnable = 0x1C,

            UptimeLatch = 0x20,

            UptimeCycles0 = 0x24,
            UptimeCycles1 = 0x28,
        }
    }
}
