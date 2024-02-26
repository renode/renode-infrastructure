//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K3XX_RealTimeClock : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K3XX_RealTimeClock(IMachine machine, long externalFastCrystalOscillatorFrequency = 2097152, long externalSlowCrystalOscillatorFrequency = 32768) : base(machine)
        {
            this.externalFastCrystalOscillatorFrequency = externalFastCrystalOscillatorFrequency;
            this.externalSlowCrystalOscillatorFrequency = externalSlowCrystalOscillatorFrequency;

            IRQ = new GPIO();

            DefineRegisters();

            internalClock = new InternalClock(machine.ClockSource, this, GetClockFrequency());
            internalClock.OnOverflowInterrupt += () =>
            {
                rolloverInterruptPending.Value = true;
                UpdateInterrupts();
            };
            internalClock.OnAPIInterrupt += () =>
            {
                apiInterruptPending.Value = true;
                UpdateInterrupts();
            };
            internalClock.OnRTCInterrupt += () =>
            {
                rtcInterruptPending.Value = true;
                UpdateInterrupts();
            };
        }

        private void UpdateInterrupts()
        {
            var interrupt = false;

            interrupt |= rtcInterruptEnabled.Value && rtcInterruptPending.Value;
            interrupt |= apiInterruptEnabled.Value && apiInterruptPending.Value;
            interrupt |= rolloverInterruptEnabled.Value && rolloverInterruptPending.Value;

            IRQ.Set(interrupt);
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();

            clockDividerFlags = ClockDivider.None;
        }

        public long Size => 0x4000;
        public GPIO IRQ { get; }

        private void SetClockDivider(bool state, ClockDivider divideBy)
        {
            if(internalClock.Enabled)
            {
                this.Log(LogLevel.Warning, "Trying to change clock divider when counter is enabled. Operation ignored");
                return;
            }

            if(state)
            {
                clockDividerFlags |= divideBy;
            }
            else
            {
                clockDividerFlags &= ~divideBy;
            }
        }

        private long GetClockFrequency()
        {
            long clockFrequency;
            switch(clockSource.Value)
            {
                case ClockSource.SXOSC:
                    clockFrequency = this.externalSlowCrystalOscillatorFrequency;
                    break;
                case ClockSource.SIRC:
                    clockFrequency = 32000; // 32kHz
                    break;
                case ClockSource.FIRC:
                    clockFrequency = 48000000; // 48MHz
                    break;
                case ClockSource.FXOSC:
                    clockFrequency = this.externalFastCrystalOscillatorFrequency;
                    break;
                default:
                    throw new Exception("unreachable code");
            }

            if(clockDividerFlags.HasFlag(ClockDivider.DivideBy32))
            {
                clockFrequency >>= 5;
            }

            if(clockDividerFlags.HasFlag(ClockDivider.DivideBy512))
            {
                clockFrequency >>= 9;
            }

            return clockFrequency;
        }

        private void DefineRegisters()
        {
            Registers.RTCSupervisorControl.Define(this, 0x80000000)
                .WithReservedBits(0, 31)
                .WithTaggedFlag("RTCSupervisorBit", 31)
            ;

            Registers.RTCControl.Define(this)
                .WithTaggedFlag("TriggerEnableForAnalogComparator", 0)
                .WithReservedBits(1, 9)
                .WithFlag(10, name: "DivideBy32enable",
                    valueProviderCallback: _ => clockDividerFlags.HasFlag(ClockDivider.DivideBy32),
                    changeCallback: (_, value) => SetClockDivider(value, ClockDivider.DivideBy32))
                .WithFlag(11, name: "DivideBy512enable",
                    valueProviderCallback: _ => clockDividerFlags.HasFlag(ClockDivider.DivideBy512),
                    changeCallback: (_, value) => SetClockDivider(value, ClockDivider.DivideBy512))
                .WithEnumField(12, 2, out clockSource, name: "ClockSelect")
                // NOTE: This flag enables writes to APIInterruptFlag field in RTCStatus
                .WithFlag(14, name: "APIInterruptEnable",
                    valueProviderCallback: _ => internalClock.APIInterruptEnabled,
                    changeCallback: (_, value) => internalClock.APIInterruptEnabled = value)
                // NOTE: This flag 'connects' APIInterruptFlag to IRQ output
                .WithFlag(15, out apiInterruptEnabled, name: "AutonomousPeriodicInterruptEnable")
                .WithReservedBits(16, 12)
                .WithFlag(28, out rolloverInterruptEnabled, name: "CounterRollOverInterruptEnable")
                .WithTaggedFlag("FreezeEnableBit", 29)
                .WithFlag(30, out rtcInterruptEnabled, name: "RTCInterruptEnable")
                .WithFlag(31, name: "CounterEnable",
                    valueProviderCallback: _ => internalClock.Enabled,
                    changeCallback: (_, value) => internalClock.Enabled = value)
                .WithChangeCallback((_, __) =>
                {
                    UpdateInterrupts();
                    internalClock.Frequency = GetClockFrequency();
                })
            ;

            Registers.RTCStatus.Define(this)
                .WithReservedBits(0, 9)
                .WithFlag(10, out rolloverInterruptPending, FieldMode.WriteOneToClear | FieldMode.Read, name: "CounterRollOverInterruptFlag")
                .WithReservedBits(11, 2)
                .WithFlag(13, out apiInterruptPending, FieldMode.WriteOneToClear | FieldMode.Read, name: "APIInterruptFlag")
                .WithReservedBits(14, 2)
                .WithReservedBits(16, 1)
                .WithTaggedFlag("InvalidAPIVALWrite", 17)
                .WithTaggedFlag("InvalidRTCWrite", 18)
                .WithReservedBits(19, 10)
                .WithFlag(29, out rtcInterruptPending, FieldMode.WriteOneToClear | FieldMode.Read, name: "RTCInterruptFlag")
                .WithReservedBits(30, 2)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Registers.RTCCounter.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "RTCCounterValue",
                    valueProviderCallback: _ => internalClock.Value)
            ;

            Registers.APICompareValue.Define(this)
                .WithValueField(0, 32, name: "APICompareValue",
                    valueProviderCallback: _ => internalClock.APICompareValue,
                    writeCallback: (_, value) => internalClock.APICompareValue = value)
            ;

            Registers.RTCCompareValue.Define(this)
                .WithValueField(0, 32, name: "RTCCompareValue",
                    valueProviderCallback: _ => internalClock.RTCCompareValue,
                    writeCallback: (_, value) => internalClock.RTCCompareValue = value)
            ;
        }

        private readonly InternalClock internalClock;
        private readonly long externalSlowCrystalOscillatorFrequency;
        private readonly long externalFastCrystalOscillatorFrequency;

        private ClockDivider clockDividerFlags;

        private IEnumRegisterField<ClockSource> clockSource;

        private IFlagRegisterField rtcInterruptEnabled;
        private IFlagRegisterField apiInterruptEnabled;
        private IFlagRegisterField rolloverInterruptEnabled;

        private IFlagRegisterField rtcInterruptPending;
        private IFlagRegisterField apiInterruptPending;
        private IFlagRegisterField rolloverInterruptPending;

        private class InternalClock
        {
            public InternalClock(IClockSource clockSource, IPeripheral parent, long frequency)
            {
                mainClock = new LimitTimer(clockSource, frequency, parent, "main_clk", limit: uint.MaxValue, direction: Direction.Ascending, eventEnabled: true);
                mainClock.LimitReached += HandleOverflow;

                apiInterruptClock = new LimitTimer(clockSource, frequency, parent, "api_int_clk", limit: uint.MaxValue, direction: Direction.Ascending, eventEnabled: false);
                apiInterruptClock.LimitReached += OnAPIInterrupt;

                rtcInterruptClock = new LimitTimer(clockSource, frequency, parent, "rtc_int_clk", limit: uint.MaxValue, direction: Direction.Ascending, eventEnabled: false);
                rtcInterruptClock.LimitReached += OnRTCInterrupt;
            }

            private void HandleOverflow()
            {
                if(APIInterruptEnabled)
                {
                    apiInterruptClock.Value = 0;
                    apiInterruptClock.Enabled = true;
                }

                if(RTCInterruptEnabled)
                {
                    rtcInterruptClock.Value = 0;
                    rtcInterruptClock.Enabled = true;
                }

                OnOverflowInterrupt?.Invoke();
            }

            public ulong Value => mainClock.Value;

            public long Frequency
            {
                get => mainClock.Frequency;
                set
                {
                    if(value == mainClock.Frequency)
                    {
                        return;
                    }

                    mainClock.Frequency = value;
                    apiInterruptClock.Frequency = value;
                    rtcInterruptClock.Frequency = value;
                }
            }

            public bool AutonomusPeriodicInterruptEnable
            {
                get => apiInterruptClock.Enabled;
            }

            public ulong APICompareValue
            {
                get => apiInterruptClock.Limit;
                set => apiInterruptClock.Limit = value;
            }

            public ulong RTCCompareValue
            {
                get => rtcInterruptClock.Limit;
                set => rtcInterruptClock.Limit = value;
            }

            public bool Enabled
            {
                get => mainClock.Enabled;
                set
                {
                    mainClock.Enabled = value;
                    if(!value)
                    {
                        apiInterruptClock.Enabled = false;
                        rtcInterruptClock.Enabled = false;
                    }
                }
            }

            public bool APIInterruptEnabled
            {
                get => apiInterruptClock.EventEnabled;
                set
                {
                    apiInterruptClock.EventEnabled = value;
                    if(value && APICompareValue > Value)
                    {
                        apiInterruptClock.Value = Value;
                        apiInterruptClock.Enabled = true;
                    }
                }
            }

            public bool RTCInterruptEnabled
            {
                get => rtcInterruptClock.EventEnabled;
                set
                {
                    rtcInterruptClock.EventEnabled = value;
                    if(value && RTCCompareValue > Value)
                    {
                        rtcInterruptClock.Value = Value;
                        rtcInterruptClock.Enabled = true;
                    }
                }
            }

            public event Action OnOverflowInterrupt;
            public event Action OnRTCInterrupt;
            public event Action OnAPIInterrupt;

            private readonly LimitTimer mainClock;
            private readonly LimitTimer apiInterruptClock;
            private readonly LimitTimer rtcInterruptClock;
        }

        [Flags]
        private enum ClockDivider
        {
            None = (0 << 0),
            DivideBy32 = (1 << 0),
            DivideBy512 = (1 << 1),
        }

        private enum ClockSource
        {
            SXOSC, // NOTE: Not available on: S32K311; instead SIRC will be used
            SIRC,
            FIRC,
            FXOSC,
        }

        private enum Registers
        {
            RTCSupervisorControl = 0x0, // RTCSUPV
            RTCControl = 0x4, // RTCC
            RTCStatus = 0x8, // RTCS
            RTCCounter = 0xC, // RTCCNT
            APICompareValue = 0x10, // APIVAL
            RTCCompareValue = 0x14 // RTCVAL
        }
    }
}
