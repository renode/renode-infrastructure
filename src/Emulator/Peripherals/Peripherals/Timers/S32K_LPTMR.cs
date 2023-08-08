//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K_LPTMR : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K_LPTMR(IMachine machine, long frequency) : base(machine)
        {
            innerTimer = new ComparingTimer(machine.ClockSource, frequency, this, "lptmr", limit: 0xFFFF, direction: Direction.Ascending,
                enabled: false, eventEnabled: true, workMode: WorkMode.Periodic, compare: 0xFFFF, divider: 2);

            innerTimer.CompareReached += CompareReached;

            IRQ = new GPIO();

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            innerTimer.Reset();
            prescaleValue = 0;
            prescalerBypass = false;
            compare = 0;
            latchedTimerValue = 0;
            UpdateInterrupt();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void UpdateInterrupt()
        {
            var value = true;
            value &= compareFlag.Value;
            value &= interruptEnable.Value;
            IRQ.Set(value);
        }

        private void CompareReached()
        {
            compareFlag.Value = true;
            if(!freeRunningCounter.Value)
            {
                innerTimer.Value = 0;
            }
            UpdateInterrupt();
        }

        private void UpdateDivider()
        {
            if(!prescalerBypass)
            {
                innerTimer.Divider = (uint)System.Math.Pow(2, prescaleValue + 1);
            }
        }

        private void DefineRegisters()
        {
            Registers.ControlStatus.Define(this)
                .WithFlag(0, out enabled, name: "TEN", changeCallback: (_, value) =>
                    {
                        innerTimer.Enabled = value;
                        if(!value)
                        {
                            innerTimer.Value = 0;
                            compareFlag.Value = false;
                        }
                    })
                .WithTaggedFlag("TMS", 1)
                .WithFlag(2, out freeRunningCounter, name: "TFC")
                .WithTaggedFlag("TPP", 3)
                .WithTag("TPS", 4, 2)
                .WithFlag(6, out interruptEnable, name: "TIE")
                .WithFlag(7, out compareFlag, FieldMode.Read | FieldMode.WriteOneToClear, name: "TCF")
                .WithTaggedFlag("TDRE", 8)
                .WithReservedBits(9, 23)
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.Prescale.Define(this)
                .WithTag("PCS", 0, 2)
                .WithFlag(2, name: "PBYP", changeCallback: (_, value) =>
                    {
                        if(enabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to update the prescaler bypass value with LPTIMR enabled - ignoring...");
                            return;
                        }
                        prescalerBypass = value;
                    })
                .WithValueField(3, 4, name: "PRESCALE", changeCallback: (_, value) =>
                    {
                        if(enabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to update the prescale value with LPTIMR enabled - ignoring...");
                            return;
                        }
                        prescaleValue = (uint)value;
                    }, valueProviderCallback: _ => prescaleValue) // we keep the prescaleValue not to have to calculate logarithms
                .WithReservedBits(7, 25)
                .WithWriteCallback((_, __) => UpdateDivider())
            ;

            Registers.Compare.Define(this)
                .WithValueField(0, 16, name: "CMR", writeCallback: (_, value) =>
                    {
                        if(enabled.Value && !compareFlag.Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to update the compare value while the timer is enabled and TCF is not set, ignoring...");
                            return;
                        }
                        if(value == 0)
                        {
                            //set tcf until disabled. May be unhandled, because setting compare to 0 can be difficult
                            this.Log(LogLevel.Warning, "Trying to set CMR to 0, this is currently not handled");
                        }
                        compare = (uint)value;

                    })
                .WithReservedBits(16, 16)
            ;

            // value written is not relevant - used only to latch the current value
            Registers.Counter.Define(this)
                .WithValueField(0, 16, writeCallback: (_, __) => latchedTimerValue = (uint)innerTimer.Value, valueProviderCallback: _ => latchedTimerValue, name: "CNR")
                .WithReservedBits(16, 16)
            ;
        }

        private IFlagRegisterField enabled;
        private IFlagRegisterField compareFlag;
        private IFlagRegisterField interruptEnable;
        private IFlagRegisterField freeRunningCounter;

        private uint compare;
        private uint latchedTimerValue;
        private bool prescalerBypass;
        private uint prescaleValue;

        private readonly ComparingTimer innerTimer;

        private enum Registers
        {
            ControlStatus = 0x0,
            Prescale = 0x4,
            Compare = 0x8,
            Counter = 0xC,
        }
    }
}
