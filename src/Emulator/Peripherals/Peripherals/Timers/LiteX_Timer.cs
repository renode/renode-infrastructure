//
// Copyright (c) 2010-2023 Antmicro
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
    // * csr data width: 8 bit
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class LiteX_Timer : BasicDoubleWordPeripheral, IKnownSize
    {
        public LiteX_Timer(IMachine machine, long frequency) : base(machine)
        {
            uptimeTimer = new LimitTimer(machine.ClockSource, frequency, this, nameof(uptimeTimer), direction: Antmicro.Renode.Time.Direction.Ascending, enabled: true);
            innerTimer = new LimitTimer(machine.ClockSource, frequency, this, nameof(innerTimer), eventEnabled: true, autoUpdate: true);
            innerTimer.LimitReached += delegate
            {
                this.Log(LogLevel.Noisy, "Limit reached");
                irqPending.Value = true;
                UpdateInterrupts();

                if(reloadValue == 0)
                {
                    this.Log(LogLevel.Noisy, "No realod value - disabling the timer");
                    innerTimer.Enabled = false;
                }
                innerTimer.Limit = reloadValue;
            };
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            innerTimer.Reset();
            uptimeTimer.Reset();
            latchedValue = 0;
            loadValue = 0;
            reloadValue = 0;
            uptimeLatchedValue = 0;

            UpdateInterrupts();
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x68;

        private void DefineRegisters()
        {
            // LOAD0 contains most significant 8 bits
            // LOAD3 contains least significant 8 bits
            Registers.Load0.DefineMany(this, SubregistersCount, (reg, idx) =>
            {
                reg.WithValueField(0, 8, name: $"LOAD{idx}", writeCallback: (_, val) =>
                {
                    BitHelper.ReplaceBits(ref loadValue, width: 8, source: (uint)val, destinationPosition: 24 - idx * 8);
                });
            });

            // RELOAD0 contains most significant 8 bits
            // RELOAD3 contains least significant 8 bits
            Registers.Reload0.DefineMany(this, SubregistersCount, (reg, idx) =>
            {
                reg.WithValueField(0, 8, name: $"RELOAD{idx}", writeCallback: (_, val) =>
                {
                    BitHelper.ReplaceBits(ref reloadValue, width: 8, source: (uint)val, destinationPosition: 24 - idx * 8);
                });
            });

            Registers.TimerEnable.Define32(this)
                .WithFlag(0, name: "ENABLE", writeCallback: (_, val) =>
                {
                    if(innerTimer.Enabled == val)
                    {
                        return;
                    }

                    if(val)
                    {
                        innerTimer.Limit = loadValue;
                        this.Log(LogLevel.Noisy, "Enabling timer. Load value: 0x{0:X}, reload value: 0x{1:X}", loadValue, reloadValue);
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

            // VALUE0 contains most significant 8 bits
            // VALUE3 contains least significant 8 bits
            Registers.Value0.DefineMany(this, SubregistersCount, (reg, idx) =>
            {
                reg.WithValueField(0, 8, FieldMode.Read, name: $"VALUE{idx}", valueProviderCallback: _ =>
                {
                    return BitHelper.GetValue(latchedValue, 24 - idx * 8, 8);
                });
            });

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

            // UPTIME_CYCLES0 contains most significant 8 bits
            // UPTIME_CYCLES7 contains least significant 8 bits
            Registers.UptimeCycles0.DefineMany(this, 8, (reg, idx) =>
            {
                reg.WithValueField(0, 8, FieldMode.Read, name: $"UPTIME_CYCLES{idx}", valueProviderCallback: _ =>
                {
                    return (byte)BitHelper.GetValue(uptimeLatchedValue, 56 - idx * 8, 8);
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

        private uint latchedValue;
        private uint loadValue;
        private uint reloadValue;

        private ulong uptimeLatchedValue;

        private readonly LimitTimer innerTimer;
        private readonly LimitTimer uptimeTimer;

        private const int SubregistersCount = 4;

        private enum Registers
        {
            Load0 = 0x0,
            Load1 = 0x4,
            Load2 = 0x8,
            Load3 = 0xC,

            Reload0 = 0x10,
            Reload1 = 0x14,
            Reload2 = 0x18,
            Reload3 = 0x1C,

            TimerEnable = 0x20,
            TimerUpdateValue = 0x24,

            Value0 = 0x28,
            Value1 = 0x2C,
            Value2 = 0x30,
            Value3 = 0x34,

            EventStatus = 0x38,
            EventPending = 0x3c,
            EventEnable = 0x40,

            UptimeLatch = 0x44,

            UptimeCycles0 = 0x48,
            UptimeCycles1 = 0x4C,
            UptimeCycles2 = 0x50,
            UptimeCycles3 = 0x54,
            UptimeCycles4 = 0x58,
            UptimeCycles5 = 0x5C,
            UptimeCycles6 = 0x60,
            UptimeCycles7 = 0x64
        }
    }
}
