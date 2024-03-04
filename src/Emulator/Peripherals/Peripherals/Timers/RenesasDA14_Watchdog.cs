//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public sealed class RenesasDA14_Watchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        public RenesasDA14_Watchdog(IMachine machine, long frequency, IDoubleWordPeripheral nvic) : base(machine)
        {
            IRQ = new GPIO();
            // Type comparison like this is required due to NVIC model being in another project
            if(nvic.GetType().FullName != "Antmicro.Renode.Peripherals.IRQControllers.NVIC")
            {
                throw new ConstructionException($"{nvic.GetType()} is invalid type for NVIC");
            }
            this.nvic = nvic;

            ticker = new LimitTimer(machine.ClockSource, frequency, this, "watchdog", TickerDefaultValue, Direction.Descending, enabled: true, eventEnabled: true);
            ticker.Divider = 320;

            ticker.LimitReached += LimitReachedAction;

            Registers.Value.Define(this, resetValue: 0x1FFF)
                .WithValueField(14, 18, out writeLockFilter, name: "WDOG_WEN")
                .WithFlag(13, FieldMode.Read, name: "WDOG_VAL_NEG",
                    valueProviderCallback: _ => 
                    {
                        if(nonMaskableInterruptReset.Value)
                        {
                            return false;
                        }
                        // else was incremented by TickerShift to mimic negative values.
                        return ticker.Value < TickerShift;
                    })
                .WithValueField(0, 13, name: "WDOG_VAL",
                    valueProviderCallback: _ =>
                    {
                        if(nonMaskableInterruptReset.Value)
                        {
                            return ticker.Value;
                        }
                        // Underflow the number. The sign is stored in another field (WDOG_VAL_NEG).
                        return unchecked(ticker.Value - TickerShift);
                    },
                    writeCallback: (_, value) =>
                    {
                        // Any value other than 0 in writeLockFilter forbids setting the value.
                        if(writeLockFilter.Value == WriteEnabled)
                        {
                            SetTickerValue(value);
                        }
                    }
                );

            Registers.Control.Define(this, resetValue: 0x6)
                .WithReservedBits(4, 28)
                .WithFlag(3, FieldMode.Read, name: "WRITE_BUSY",
                    valueProviderCallback: _ => false // Not implemented. For now it's never busy.
                )
                .WithTaggedFlag("WDOG_FREEZE_EN", 2)
                .WithReservedBits(1, 1)
                .WithFlag(0, out nonMaskableInterruptReset, name: "NMI_RST");
        }

        public override void Reset()
        {
            IRQ.Unset();
            ticker.Reset();
            base.Reset();
        }

        // Start/Stop is performed on registers that don't belong to this peripheral.
        // 'Enabled' should be used by the peripheral that handles the start/stop.
        public bool Enabled
        {
            get => ticker.Enabled;
            set => ticker.Enabled = value;
        }

        public long Size => 0x10;
        public GPIO IRQ { get; }

        private void SetTickerValue(ulong value)
        {
            IRQ.Unset();
            if(nonMaskableInterruptReset.Value)
            {
                // Only one trigger (at 0x0) is needed and it generates a reset.
                ticker.Value = value;
                ticker.Limit = 0x0;
            }
            else 
            {
                // By default two triggers are needed, at 0x0 and negative 0x10.
                // Shift the value by TickerShift to handle both cases as non-negative values.
                // `TickerShift` generates a NMI and 0x0 generates a reset.
                ticker.Value = value + TickerShift;
                ticker.Limit = TickerShift;
            }
            this.Log(LogLevel.Noisy, "Ticker value set to: 0x{0:X}", value);
        }

        private void LimitReachedAction()
        {
            this.Log(LogLevel.Noisy, "Limit reached");
            if(ticker.Limit == TickerShift && !nonMaskableInterruptReset.Value)
            {
                // Limit for NMI
                this.Log(LogLevel.Noisy, "Triggering IRQ");
                IRQ.Set();
                ticker.Limit = 0x0;
                ticker.Value = TickerShift;
                // Send NMI to NVIC
                ((dynamic)nvic).SetPendingIRQ(2);
            }
            else
            {
                // Limit for reset
                this.Log(LogLevel.Warning, "Reseting machine");
                Enabled = false;
                machine.RequestReset();
            }
        }

        private readonly LimitTimer ticker;
        private readonly IDoubleWordPeripheral nvic;

        private IValueRegisterField writeLockFilter;
        private IFlagRegisterField nonMaskableInterruptReset;

        // In hardware it counts down from 0x1fff to negative 0x10. Mock the negative range by starting from +0x10.
        private const ulong TickerShift = 0x10;
        private const ulong TickerDefaultValue = 0x1fff + TickerShift;
        private const ulong WriteEnabled = 0;

        private enum Registers: long
        {
            Value = 0x0,
            Control = 0x4,
        }
    }
}
