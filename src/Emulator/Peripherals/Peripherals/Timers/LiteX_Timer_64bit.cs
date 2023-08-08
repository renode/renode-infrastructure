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
    // this is a model of LiteX timer with register layout to simulate 64 bit bus read/write access
    // * width: 64 bits
    // * csr data width: 8 bit
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class LiteX_Timer64 : BasicDoubleWordPeripheral, IKnownSize
    {
        public LiteX_Timer64(IMachine machine, long frequency) : base(machine)
        {
            innerTimer = new LimitTimer(machine.ClockSource, frequency, this, nameof(innerTimer), eventEnabled: true, autoUpdate: true);
            innerTimer.LimitReached += delegate
            {
                this.Log(LogLevel.Noisy, "Limit reached");
                irqPending.Value = true;
                UpdateInterrupts();

                if(reloadValue == 0)
                {
                    this.Log(LogLevel.Noisy, "No reload value - disabling the timer");
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
            latchedValue = 0;
            loadValue = 0;
            reloadValue = 0;

            UpdateInterrupts();
        }

        public GPIO IRQ { get; } = new GPIO();

        public long Size => 0x88;

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
            }, stepInBytes: 8);

            // RELOAD0 contains most significant 8 bits
            // RELOAD3 contains least significant 8 bits
            Registers.Reload0.DefineMany(this, SubregistersCount, (reg, idx) =>
            {
                reg.WithValueField(0, 8, name: $"RELOAD{idx}", writeCallback: (_, val) =>
                {
                    BitHelper.ReplaceBits(ref reloadValue, width: 8, source: (uint)val, destinationPosition: 24 - idx * 8);
                });
            }, stepInBytes: 8);

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
            }, stepInBytes: 8);

            Registers.EventStatus.Define32(this)
                .WithFlag(0, FieldMode.Read, name: "EV_STATUS", valueProviderCallback: _ => innerTimer.Value == 0)
            ;

            Registers.EventPending.Define32(this)
                .WithFlag(0, out irqPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "EV_PENDING", changeCallback: (_, __) => UpdateInterrupts())
            ;

            Registers.EventEnable.Define32(this)
                .WithFlag(0, out irqEnabled, name: "EV_ENABLE", changeCallback: (_, __) => UpdateInterrupts())
            ;
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

        private readonly LimitTimer innerTimer;

        private const int SubregistersCount = 4;

        private enum Registers
        {
            /*
            LoadX, ReloadX, and ValueX are all 64 bit, but only 8 bits from these per register are affected by read/write operations.
            So the only change in their implementation from the 'generic' LiteX Timer is the increase of stepInBytes.
            The value they all hold with the current LiteX implementation of the timer will still not exceed 32 bits.
            */
            Load0 = 0x0,
            Load1 = 0x8,
            Load2 = 0x10,
            Load3 = 0x18,

            Reload0 = 0x20,
            Reload1 = 0x28,
            Reload2 = 0x30,
            Reload3 = 0x38,

            TimerEnable = 0x40,
            TimerUpdateValue = 0x48,

            Value0 = 0x50,
            Value1 = 0x58,
            Value2 = 0x60,
            Value3 = 0x68,

            EventStatus = 0x70,
            EventPending = 0x78,
            EventEnable = 0x80
        }
    }
}
