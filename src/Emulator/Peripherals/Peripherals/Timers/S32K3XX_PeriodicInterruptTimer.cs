//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K3XX_PeriodicInterruptTimer : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_PeriodicInterruptTimer(IMachine machine, long oscillatorFrequency, bool hasRealTimeInterrupt = false, bool hasLifetimeTimer = false, bool supportsTimersChaining = false) : base(machine)
        {
            clockChannels = new SortedList<Registers, ClockChannel>();

            IRQ = new GPIO();

            DefineRegisters(oscillatorFrequency, hasRealTimeInterrupt, hasLifetimeTimer, supportsTimersChaining);
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();

            foreach(var clockChannel in clockChannels.Values)
            {
                clockChannel.Reset();
            }
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }

        private void UpdateInterrupts()
        {
            var interrupt = clockChannels.Values.Any(clockChannel => clockChannel.InterruptEnable && clockChannel.InterruptFlag);
            IRQ.Set(interrupt);
        }

        private void DefineRegisters(long oscillatorFrequency, bool hasRealTimeInterrupt, bool hasLifetimeTimer, bool supportsTimersChaining)
        {
            var moduleControl = Registers.ModuleControl.Define(this)
                .WithReservedBits(3, 29)
                .WithTaggedFlag("ModuleDisableForPIT", 1)
                .WithTaggedFlag("Freeze", 0)
            ;

            if(hasRealTimeInterrupt)
            {
                moduleControl.WithTaggedFlag("ModuleDisableForRTI", 2);
            }
            else
            {
                moduleControl.WithReservedBits(2, 1);
            }

            if(hasLifetimeTimer)
            {
                // NOTE: Technically value of those two registers is only valid when PIT has configured lifetimer,
                // that is Channel 1 has enabled chain mode, and both Channel 0 and Channel 1 start values are set to
                // 0xFFFF_FFFF, but documentation doesn't specify what we should read from lifetimer registers when that's
                // not the case

                Registers.LowerLifetimer.Define(this)
                    .WithValueField(0, 32, out var lowerLifetimerValue, FieldMode.Read, name: "LifetimerValue");
                ;

                Registers.UpperLifetimer.Define(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "LifetimerValue",
                        valueProviderCallback: _ =>
                        {
                            var channel0 = clockChannels[Registers.Control0];
                            var channel1 = clockChannels[Registers.Control1];
                            var lifetimerValue = ulong.MaxValue - ((channel0.Value << 32) | channel1.Value);

                            lowerLifetimerValue.Value = lifetimerValue;
                            return lifetimerValue >> 32;
                        })
                ;
            }

            if(hasRealTimeInterrupt)
            {
                DefineChannelRegisters(oscillatorFrequency, null, Registers.ControlRTI, Registers.FlagRTI, Registers.LoadValueRTI, Registers.CurrentValueRTI);
            }

            DefineChannelRegisters(oscillatorFrequency, null, Registers.Control0, Registers.Flag0, Registers.LoadValue0, Registers.CurrentValue0);
            DefineChannelRegisters(oscillatorFrequency, Registers.Control0, Registers.Control1, Registers.Flag1, Registers.LoadValue1, Registers.CurrentValue1);
            DefineChannelRegisters(oscillatorFrequency, Registers.Control1, Registers.Control2, Registers.Flag2, Registers.LoadValue2, Registers.CurrentValue2);
            DefineChannelRegisters(oscillatorFrequency, Registers.Control2, Registers.Control3, Registers.Flag3, Registers.LoadValue3, Registers.CurrentValue3);

            if(hasRealTimeInterrupt)
            {
                Registers.LoadValueSyncStatusRTI.Define(this)
                    .WithReservedBits(1, 31)
                    .WithTaggedFlag("SyncStatus", 0)
                ;
            }
        }

        private void DefineChannelRegisters(long oscillatorFrequency, Registers? chainedTo, Registers control, Registers flag, Registers load, Registers currentValue)
        {
            var clockChannel = new ClockChannel(machine.ClockSource, this, oscillatorFrequency, Enum.GetName(typeof(Registers), control));
            clockChannel.OnInterrupt += UpdateInterrupts;

            var controlRegister = control.Define(this)
                .WithReservedBits(3, 29)
                .WithFlag(1, name: "InterruptEnable",
                    valueProviderCallback: _ => clockChannel.InterruptEnable,
                    changeCallback: (_, value) =>
                    {
                        clockChannel.InterruptEnable = value;
                        UpdateInterrupts();
                    })
                .WithFlag(0, name: "TimerEnable",
                    valueProviderCallback: _ => clockChannel.Enabled,
                    changeCallback: (_, value) => clockChannel.Enabled = value)
            ;

            if(chainedTo != null)
            {
                controlRegister.WithFlag(2, name: "ChainMode",
                    valueProviderCallback: _ => clockChannel.ChainMode,
                    writeCallback: (_, value) => clockChannel.ChainMode = value)
                ;

                var chainedTimer = clockChannels[chainedTo.Value];
                chainedTimer.OnInterrupt += () =>
                {
                    if(clockChannel.ChainMode)
                    {
                        clockChannel.Step();
                    }
                };
            }
            else
            {
                controlRegister.WithReservedBits(2, 1);
            }

            flag.Define(this)
                .WithReservedBits(1, 31)
                .WithFlag(0, name: "InterruptFlag",
                    valueProviderCallback: _ => clockChannel.InterruptFlag,
                    writeCallback: (_, value) => { if(value) clockChannel.InterruptFlag = false; } )
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            load.Define(this)
                .WithValueField(0, 32, name: "StartValue",
                    valueProviderCallback: _ => clockChannel.StartValue,
                    writeCallback: (_, value) => clockChannel.StartValue = value)
            ;

            currentValue.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "CurrentValue",
                    valueProviderCallback: _ => clockChannel.Value)
            ;

            clockChannels.Add(control, clockChannel);
        }

        private readonly IDictionary<Registers, ClockChannel> clockChannels;

        private class ClockChannel
        {
            public ClockChannel(IClockSource clockSource, IPeripheral parent, long frequency, string name)
            {
                underlyingTimer = new LimitTimer(clockSource, frequency, parent, name);
                underlyingTimer.LimitReached += () =>
                {
                    InterruptFlag = true;
                    OnInterrupt?.Invoke();
                };
            }

            public void Step()
            {
                if(Value == 0)
                {
                    Value = StartValue;
                }
                else if(--Value == 0)
                {
                    InterruptFlag = true;
                    OnInterrupt?.Invoke();
                }
            }

            public void Reset()
            {
                Enabled = false;
                InterruptEnable = false;
                InterruptFlag = false;
                Value = ulong.MaxValue;
                StartValue = Value;
                ChainMode = false;
            }

            public event Action OnInterrupt;

            public bool Enabled
            {
                get => timerEnabled;
                set
                {
                    timerEnabled = value;
                    underlyingTimer.Enabled = value && !ChainMode;
                }
            }

            public bool InterruptEnable
            {
                get => underlyingTimer.EventEnabled;
                set => underlyingTimer.EventEnabled = value;
            }

            public bool InterruptFlag { get; set; }

            public ulong Value
            {
                get => underlyingTimer.Value;
                private set => underlyingTimer.Value = value;
            }

            public ulong StartValue
            {
                get => underlyingTimer.Limit;
                set
                {
                    underlyingTimer.Limit = value;
                    underlyingTimer.Value = value;
                }
            }

            public bool ChainMode
            {
                get => chainMode;
                set
                {
                    chainMode = value;
                    Enabled = timerEnabled;
                }
            }

            private readonly LimitTimer underlyingTimer;

            private bool chainMode;
            private bool timerEnabled;
        }

        private enum Registers
        {
            ModuleControl = 0x0, // MCR
            UpperLifetimer = 0xE0, // LTMR64H
            LowerLifetimer = 0xE4, // LTMR64L
            LoadValueSyncStatusRTI = 0xEC, // RTI_LDVAL_STAT
            LoadValueRTI = 0xF0, // RTI_LDVAL
            CurrentValueRTI = 0xF4, // RTI_CVAL
            ControlRTI = 0xF8, // RTI_TCTRL
            FlagRTI = 0xFC, // RTI_TFLG
            LoadValue0 = 0x100, // LDVAL0
            CurrentValue0 = 0x104, // CVAL0
            Control0 = 0x108, // TCTRL0
            Flag0 = 0x10C, // TFLG0
            LoadValue1 = 0x110, // LDVAL1
            CurrentValue1 = 0x114, // CVAL1
            Control1 = 0x118, // TCTRL1
            Flag1 = 0x11C, // TFLG1
            LoadValue2 = 0x120, // LDVAL2
            CurrentValue2 = 0x124, // CVAL2
            Control2 = 0x128, // TCTRL2
            Flag2 = 0x12C, // TFLG2
            LoadValue3 = 0x130, // LDVAL3
            CurrentValue3 = 0x134, // CVAL3
            Control3 = 0x138, // TCTRL3
            Flag3 = 0x13C, // TFLG3
        }
    }
}
