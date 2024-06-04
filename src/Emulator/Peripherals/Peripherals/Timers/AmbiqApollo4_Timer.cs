//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class AmbiqApollo4_Timer : BasicDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize
    {
        public AmbiqApollo4_Timer(IMachine machine) : base(machine)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            internalTimers = new InternalTimer[TimersCount];
            for(var i = 0; i < TimersCount; ++i)
            {
                internalTimers[i] = new InternalTimer(this, machine.ClockSource, i);
                internalTimers[i].OnCompare += UpdateInterrupts;
                innerConnections[i] = new GPIO();
            }

            padOutput = new IEnumRegisterField<PadOutput>[OutputConfigRegisters * OutputConfigFieldsPerRegister];
            timerEnabled = new IFlagRegisterField[TimersCount];
            functionSelect = new IEnumRegisterField<FunctionSelect>[TimersCount];
            triggerMode = new IEnumRegisterField<TriggerMode>[TimersCount];
            triggerSource = new IEnumRegisterField<TriggerSource>[TimersCount];

            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < TimersCount; ++i)
            {
                internalTimers[i].Reset();
                Connections[i].Unset();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long Size => 0x800;

        private long GetFrequencyAndDivider(ClockSelect clockSelect, out uint divider)
        {
            divider = 1;
            var frequency = 1L;

            switch(clockSelect)
            {
                case ClockSelect.HFRCDiv16:
                    frequency = HFRCFrequency;
                    divider = 16;
                    break;
                case ClockSelect.HFRCDiv64:
                    frequency = HFRCFrequency;
                    divider = 64;
                    break;
                case ClockSelect.HFRCDiv256:
                    frequency = HFRCFrequency;
                    divider = 256;
                    break;
                case ClockSelect.HFRCDiv1024:
                    frequency = HFRCFrequency;
                    divider = 1024;
                    break;
                case ClockSelect.HFRCDiv4K:
                    frequency = HFRCFrequency;
                    divider = 4096;
                    break;
                case ClockSelect.LFRC:
                    frequency = LFRCFrequency;
                    break;
                case ClockSelect.LFRCDiv2:
                    frequency = LFRCFrequency;
                    divider = 2;
                    break;
                case ClockSelect.LFRCDiv32:
                    frequency = LFRCFrequency;
                    divider = 32;
                    break;
                case ClockSelect.LFRCDiv1K:
                    frequency = LFRCFrequency;
                    divider = 1024;
                    break;
                case ClockSelect.XT:
                    frequency = XTFrequency;
                    break;
                case ClockSelect.XTDiv2:
                    frequency = XTFrequency;
                    divider = 2;
                    break;
                case ClockSelect.XTDiv4:
                    frequency = XTFrequency;
                    divider = 4;
                    break;
                case ClockSelect.XTDiv8:
                    frequency = XTFrequency;
                    divider = 8;
                    break;
                case ClockSelect.XTDiv16:
                    frequency = XTFrequency;
                    divider = 16;
                    break;
                case ClockSelect.XTDiv32:
                    frequency = XTFrequency;
                    divider = 32;
                    break;
                case ClockSelect.XTDiv128:
                    frequency = XTFrequency;
                    divider = 128;
                    break;
                case ClockSelect.RTC_100HZ:
                    frequency = 100;
                    break;
                default:
                    this.Log(LogLevel.Warning, "{0} is not supported; set default frequency of 1Hz", clockSelect);
                    break;
            }
            return frequency;
        }

        private void UpdateTimerActiveStatus()
        {
            for(var i = 0 ; i < TimersCount; ++i)
            {
                internalTimers[i].Enabled = timerEnabled[i].Value && globalTimerEnabled[i].Value;
            }
        }

        private void UpdateInterrupts()
        {
            for(var i = 0; i < TimersCount; ++i)
            {
                var interrupt = false;
                interrupt |= internalTimers[i].Compare0Event && internalTimers[i].Compare0Interrupt;
                interrupt |= internalTimers[i].Compare1Event && internalTimers[i].Compare1Interrupt;

                if(Connections[i].IsSet != interrupt)
                {
                    this.NoisyLog("Changing Interrupt{0} from {1} to {2}", i, Connections[i].IsSet, interrupt);
                }

                Connections[i].Set(interrupt);
            }
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                .WithReservedBits(0, 31)
                .WithFlag(31, FieldMode.WriteOneToClear, name: "RESET",
                    writeCallback: (_, value) => { if(value) Reset(); })
            ;

            Registers.Status.Define(this)
                .WithValueField(0, 16, FieldMode.Read, name: "ACTIVE",
                    valueProviderCallback: _ => (uint)internalTimers.Count(timer => timer.Enabled))
                .WithValueField(16, 5, FieldMode.Read, name: "NTIMERS",
                    valueProviderCallback: _ => TimersCount)
                .WithReservedBits(21, 11)
            ;

            Registers.GlobalEnable.Define(this, 0x7ff)
                .WithFlags(0, 16, out globalTimerEnabled, name: "ENB")
                .WithChangeCallback((_, __) => UpdateTimerActiveStatus())
            ;

            {
                var interruptEnable = Registers.InterruptEnable.Define(this)
                    .WithWriteCallback((_, __) => UpdateInterrupts());
                var interruptStatus = Registers.InterruptStatus.Define(this)
                    .WithWriteCallback((_, __) => UpdateInterrupts());
                var interruptClear = Registers.InterruptClear.Define(this)
                    .WithWriteCallback((_, __) => UpdateInterrupts());
                var interruptSet = Registers.InterruptSet.Define(this)
                    .WithWriteCallback((_, __) => UpdateInterrupts());

                for(var i = 0; i < TimersCount; ++i)
                {
                    var index = i;

                    interruptEnable
                        .WithFlag(2 * index, name: $"TMR{index}0INT",
                            valueProviderCallback: _ => internalTimers[index].Compare0Interrupt,
                            writeCallback: (_, value) => internalTimers[index].Compare0Interrupt = value)
                        .WithFlag(2 * index + 1, name: $"TMR{index}1INT",
                            valueProviderCallback: _ => internalTimers[index].Compare1Interrupt,
                            writeCallback: (_, value) => internalTimers[index].Compare1Interrupt = value)
                    ;

                    interruptStatus
                        .WithFlag(2 * index, name: $"TMR{index}0INTSTAT",
                            valueProviderCallback: _ => internalTimers[index].Compare0Event,
                            writeCallback: (_, value) => internalTimers[index].Compare0Event = value)
                        .WithFlag(2 * index + 1, name: $"TMR{index}1INTSTAT",
                            valueProviderCallback: _ => internalTimers[index].Compare1Event,
                            writeCallback: (_, value) => internalTimers[index].Compare1Event = value)
                    ;

                    interruptClear
                        .WithFlag(2 * index, name: $"TMR{index}0INTCLR",
                            valueProviderCallback: _ => internalTimers[index].Compare0Event,
                            writeCallback: (_, value) => { if(value) internalTimers[index].Compare0Event = false; })
                        .WithFlag(2 * index + 1, name: $"TMR{index}1INTCLR",
                            valueProviderCallback: _ => internalTimers[index].Compare1Event,
                            writeCallback: (_, value) => { if(value) internalTimers[index].Compare1Event = false; })
                    ;

                    interruptSet
                        .WithFlag(2 * index, name: $"TMR{index}0INTSET",
                            valueProviderCallback: _ => internalTimers[index].Compare0Event,
                            writeCallback: (_, value) => { if(value) internalTimers[index].Compare0Event = true; })
                        .WithFlag(2 * index + 1, name: $"TMR{index}1INTSET",
                            valueProviderCallback: _ => internalTimers[index].Compare1Event,
                            writeCallback: (_, value) => { if(value) internalTimers[index].Compare1Event = true; })
                    ;
                }
            }

            Registers.OutputConfig0.DefineMany(this, OutputConfigRegisters, (register, index) =>
            {
                register
                    .WithEnumField(0, 6, out padOutput[index * 4], name: $"OUTCFG{index * 4}")
                    .WithReservedBits(6, 2)
                    .WithEnumField(8, 6, out padOutput[index * 4 + 1], name: $"OUTCFG{index * 4 + 1}")
                    .WithReservedBits(14, 2)
                    .WithEnumField(16, 6, out padOutput[index * 4 + 2], name: $"OUTCFG{index * 4 + 2}")
                    .WithReservedBits(22, 2)
                    .WithEnumField(24, 6, out padOutput[index * 4 + 3], name: $"OUTCFG{index * 4 + 3}")
                    .WithReservedBits(30, 2)
                ;
            });

            Registers.Timer0Control.DefineMany(this, TimersCount, (register, index) =>
            {
                register
                    .WithFlag(0, out timerEnabled[index], name: $"TMR{index}EN",
                        writeCallback: (_, value) =>
                        {
                            UpdateTimerActiveStatus();
                        })
                    .WithFlag(1, FieldMode.WriteOneToClear, name: $"TMR{index}CLR",
                        writeCallback: (_, value) => { if(value) internalTimers[index].Reset(); })
                    .WithTaggedFlag($"TMR{index}POL0", 2)
                    .WithTaggedFlag($"TMR{index}POL1", 3)
                    .WithEnumField(4, 4, out functionSelect[index], name: $"TMR{index}FN",
                        writeCallback: (_, value) =>
                        {
                            switch(value)
                            {
                                case FunctionSelect.Continous:
                                    internalTimers[index].OneShot = false;
                                    break;
                                case FunctionSelect.Upcount:
                                    internalTimers[index].OneShot = true;
                                    break;
                                default:
                                    this.Log(LogLevel.Error, "Timer{0}: {1} function mode is not supported", index, value);
                                    break;
                            }
                        })
                    .WithEnumField<DoubleWordRegister, ClockSelect>(8, 8, name: $"TMR{index}CLK",
                        changeCallback: (_, newValue) =>
                        {
                            var frequency = GetFrequencyAndDivider(newValue, out var divider);
                            internalTimers[index].Frequency = frequency;
                            internalTimers[index].Divider = divider;
                        })
                    .WithEnumField(16, 2, out triggerMode[index], name: $"TMR{index}TMODE")
                    .WithReservedBits(18, 6)
                    .WithTag($"TMR{index}LMT", 24, 8)
                ;
            }, stepInBytes: TimerStructureSize);

            Registers.Timer0.DefineMany(this, TimersCount, (register, index) =>
            {
                register
                    .WithValueField(0, 32, name: $"TIMER{index}",
                        valueProviderCallback: _ =>
                        {
                            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                            {
                                cpu.SyncTime();
                            }
                            return (uint)internalTimers[index].Value;
                        },
                        writeCallback: (_, value) => internalTimers[index].Value = (ulong)value)
                ;
            }, stepInBytes: TimerStructureSize);

            Registers.Timer0Compare0.DefineMany(this, TimersCount, (register, index) =>
            {
                register
                    .WithValueField(0, 32, name: $"TMR{index}CMP0",
                        valueProviderCallback: _ => (uint)internalTimers[index].Compare0,
                        writeCallback: (_, value) => internalTimers[index].Compare0 = (ulong)value)
                ;
            }, stepInBytes: TimerStructureSize);

            Registers.Timer0Compare1.DefineMany(this, TimersCount, (register, index) =>
            {
                register
                    .WithValueField(0, 32, name: $"TMR{index}CMP1",
                        valueProviderCallback: _ => (uint)internalTimers[index].Compare1,
                        writeCallback: (_, value) => internalTimers[index].Compare1 = (ulong)value)
                ;
            }, stepInBytes: TimerStructureSize);

            Registers.Timer0Mode.DefineMany(this, TimersCount, (register, index) =>
            {
                register
                    .WithReservedBits(0, 8)
                    .WithEnumField(8, 8, out triggerSource[index], name: $"TMR{index}TRIGSEL")
                    .WithReservedBits(16, 16)
                ;
            }, stepInBytes: TimerStructureSize);
        }

        private readonly InternalTimer[] internalTimers;

        private IFlagRegisterField[] timerEnabled;
        private IFlagRegisterField[] globalTimerEnabled;

        private IEnumRegisterField<PadOutput>[] padOutput;

        private IEnumRegisterField<FunctionSelect>[] functionSelect;
        private IEnumRegisterField<TriggerMode>[] triggerMode;
        private IEnumRegisterField<TriggerSource>[] triggerSource;

        private const int TimerStructureSize = 0x20;
        private const int TimersCount = 16;
        private const long HFRCFrequency = 96000000;
        private const long LFRCFrequency = 1000;
        private const long XTFrequency = 32768;
        private const int OutputConfigRegisters = 32;
        private const int OutputConfigFieldsPerRegister = 4;

        private class InternalTimer
        {
            public InternalTimer(IPeripheral parent, IClockSource clockSource, int index)
            {
                compare0Timer = new ComparingTimer(clockSource, 1, parent, $"timer{index}cmp0", limit: 0xFFFFFFFF, compare: 0xFFFFFFFF, enabled: false);
                compare1Timer = new ComparingTimer(clockSource, 1, parent, $"timer{index}cmp1", limit: 0xFFFFFFFF, compare: 0xFFFFFFFF, enabled: false);

                compare0Timer.CompareReached += () =>
                {
                    Compare0Event = true;
                    CompareReached();
                };
                compare1Timer.CompareReached += () =>
                {
                    Compare1Event = true;
                    CompareReached();
                };
            }

            public void Reset()
            {
                Enabled = false;
                Compare0Event = false;
                Compare1Event = false;
            }

            public bool Enabled
            {
                get => compare0Timer.Enabled;
                set
                {
                    if(Enabled == value)
                    {
                        return;
                    }

                    Value = 0;
                    compare0Timer.Enabled = value;
                    compare1Timer.Enabled = value;
                }
            }

            public bool OneShot { get; set; }

            public ulong Value
            {
                get => compare0Timer.Value;
                set
                {
                    compare0Timer.Value = value;
                    compare1Timer.Value = value;
                }
            }

            public long Frequency
            {
                get => compare0Timer.Frequency;
                set
                {
                    compare0Timer.Frequency = value;
                    compare1Timer.Frequency = value;
                }
            }

            public uint Divider
            {
                get => compare0Timer.Divider;
                set
                {
                    compare0Timer.Divider = value;
                    compare1Timer.Divider = value;
                }
            }

            public ulong Compare0
            {
                get => compare0Timer.Compare;
                set => compare0Timer.Compare = value;
            }

            public ulong Compare1
            {
                get => compare1Timer.Compare;
                set => compare1Timer.Compare = value;
            }

            public bool Compare0Event { get; set; }
            public bool Compare1Event { get; set; }

            public bool Compare0Interrupt
            {
                get => compare0Timer.EventEnabled;
                set => compare0Timer.EventEnabled = value;
            }

            public bool Compare1Interrupt
            {
                get => compare1Timer.EventEnabled;
                set => compare1Timer.EventEnabled = value;
            }

            public Action OnCompare;

            private void CompareReached()
            {
                OnCompare?.Invoke();

                if(OneShot)
                {
                    Value = 0;
                }
            }

            private readonly ComparingTimer compare0Timer;
            private readonly ComparingTimer compare1Timer;
        }

        private enum TriggerSource
        {
            Timer0Output0 = 0x00,  // Trigger source is TIMER 0 Output 0
            Timer0Output1 = 0x01,  // Trigger source is TIMER 0 Output 1
            Timer1Output0 = 0x02,  // Trigger source is TIMER 1 Output 0
            Timer1Output1 = 0x03,  // Trigger source is TIMER 1 Output 1
            Timer2Output0 = 0x04,  // Trigger source is TIMER 2 Output 0
            Timer2Output1 = 0x05,  // Trigger source is TIMER 2 Output 1
            Timer3Output0 = 0x06,  // Trigger source is TIMER 3 Output 0
            Timer3Output1 = 0x07,  // Trigger source is TIMER 3 Output 1
            Timer4Output0 = 0x08,  // Trigger source is TIMER 4 Output 0
            Timer4Output1 = 0x09,  // Trigger source is TIMER 4 Output 1
            Timer5Output0 = 0x0A,  // Trigger source is TIMER 5 Output 0
            Timer5Output1 = 0x0B,  // Trigger source is TIMER 5 Output 1
            Timer6Output0 = 0x0C,  // Trigger source is TIMER 6 Output 0
            Timer6Output1 = 0x0D,  // Trigger source is TIMER 6 Output 1
            Timer7Output0 = 0x0E,  // Trigger source is TIMER 7 Output 0
            Timer7Output1 = 0x0F,  // Trigger source is TIMER 7 Output 1
            Timer8Output0 = 0x10,  // Trigger source is TIMER 8 Output 0
            Timer8Output1 = 0x11,  // Trigger source is TIMER 8 Output 1
            Timer9Output0 = 0x12,  // Trigger source is TIMER 9 Output 0
            Timer9Output1 = 0x13,  // Trigger source is TIMER 9 Output 1
            Timer10Output0 = 0x14, // Trigger source is TIMER 10 Output 0
            Timer10Output1 = 0x15, // Trigger source is TIMER 10 Output 1
            Timer11Output0 = 0x16, // Trigger source is TIMER 11 Output 0
            Timer11Output1 = 0x17, // Trigger source is TIMER 11 Output 1
            Timer12Output0 = 0x18, // Trigger source is TIMER 12 Output 0
            Timer12Output1 = 0x19, // Trigger source is TIMER 12 Output 1
            Timer13Output0 = 0x1A, // Trigger source is TIMER 13 Output 0
            Timer13Output1 = 0x1B, // Trigger source is TIMER 13 Output 1
            Timer14Output0 = 0x1C, // Trigger source is TIMER 14 Output 0
            Timer14Output1 = 0x1D, // Trigger source is TIMER 14 Output 1
            Timer15Output0 = 0x1E, // Trigger source is TIMER 15 Output 0
            Timer15Output1 = 0x1F, // Trigger source is TIMER 15 Output 1
            STimerCompare0 = 0x30, // Trigger source is STIMER Compare 0
            STimerCompare1 = 0x31, // Trigger source is STIMER Compare 1
            STimerCompare2 = 0x32, // Trigger source is STIMER Compare 2
            STimerCompare3 = 0x33, // Trigger source is STIMER Compare 3
            STimerCompare4 = 0x34, // Trigger source is STIMER Compare 4
            STimerCompare5 = 0x35, // Trigger source is STIMER Compare 5
            STimerCompare6 = 0x36, // Trigger source is STIMER Compare 6
            STimerCompare7 = 0x37, // Trigger source is STIMER Compare 7
            STimerCapture0 = 0x38, // Trigger source is STIMER Capture 0
            STimerCapture1 = 0x39, // Trigger source is STIMER Capture 1
            STimerCapture2 = 0x3A, // Trigger source is STIMER Capture 2
            STimerCapture3 = 0x3B, // Trigger source is STIMER Capture 3
            STimerCapture4 = 0x3C, // Trigger source is STIMER Capture 4
            STimerCapture5 = 0x3D, // Trigger source is STIMER Capture 5
            STimerCapture6 = 0x3E, // Trigger source is STIMER Capture 6
            STimerCapture7 = 0x3F, // Trigger source is STIMER Capture 7
            GPIO0 = 0x80,          // Trigger source is GPIO #0
            GPIO127 = 0xFF,        // Trigger source is GPIO #127
        }

        private enum TriggerMode
        {
            Disable = 0x00,
            RisingEdge = 0x01,
            FallingEdge = 0x02,
            EitherEdge = 0x03,
        }

        private enum ClockSelect
        {
            HFRCDiv16 = 0x01,      // Clock source is HFRC / 16
            HFRCDiv64 = 0x02,      // Clock source is HFRC / 64
            HFRCDiv256 = 0x03,     // Clock source is HFRC / 256
            HFRCDiv1024 = 0x04,    // Clock source is HFRC / 1024
            HFRCDiv4K = 0x05,      // Clock source is HFRC / 4096
            LFRC = 0x06,           // Clock source is LFRC
            LFRCDiv2 = 0x07,       // Clock source is LFRC / 2
            LFRCDiv32 = 0x08,      // Clock source is LFRC / 32
            LFRCDiv1K = 0x09,      // Clock source is LFRC / 1024
            XT = 0x0A,             // Clock source is the XT (uncalibrated).
            XTDiv2 = 0x0B,         // Clock source is XT / 2
            XTDiv4 = 0x0C,         // Clock source is XT / 4
            XTDiv8 = 0x0D,         // Clock source is XT / 8
            XTDiv16 = 0x0E,        // Clock source is XT / 16
            XTDiv32 = 0x0F,        // Clock source is XT / 32
            XTDiv128 = 0x10,       // Clock source is XT / 128
            RTC_100HZ = 0x11,      // Clock source is 100 Hz from the current RTC oscillator.
            BuckC = 0x1C,          // Clock source is Buck VDDC TON pulses.
            BuckF = 0x1D,          // Clock source is Buck VDDF TON pulses.
            BuckS = 0x1E,          // Clock source is Buck VDDS TON pulses.
            BuckCLV = 0x1F,        // Clock source is Buck VDDC_LV TON pulses.
            Timer0Output0 = 0x20,  // Clock source is TIMER 0 Output 0
            Timer0Output1 = 0x21,  // Clock source is TIMER 0 Output 1
            Timer1Output0 = 0x22,  // Clock source is TIMER 1 Output 0
            Timer1Output1 = 0x23,  // Clock source is TIMER 1 Output 1
            Timer2Output0 = 0x24,  // Clock source is TIMER 2 Output 0
            Timer2Output1 = 0x25,  // Clock source is TIMER 2 Output 1
            Timer3Output0 = 0x26,  // Clock source is TIMER 3 Output 0
            Timer3Output1 = 0x27,  // Clock source is TIMER 3 Output 1
            Timer4Output0 = 0x28,  // Clock source is TIMER 4 Output 0
            Timer4Output1 = 0x29,  // Clock source is TIMER 4 Output 1
            Timer5Output0 = 0x2A,  // Clock source is TIMER 5 Output 0
            Timer5Output1 = 0x2B,  // Clock source is TIMER 5 Output 1
            Timer6Output0 = 0x2C,  // Clock source is TIMER 6 Output 0
            Timer6Output1 = 0x2D,  // Clock source is TIMER 6 Output 1
            Timer7Output0 = 0x2E,  // Clock source is TIMER 7 Output 0
            Timer7Output1 = 0x2F,  // Clock source is TIMER 7 Output 1
            Timer8Output0 = 0x30,  // Clock source is TIMER 8 Output 0
            Timer8Output1 = 0x31,  // Clock source is TIMER 8 Output 1
            Timer9Output0 = 0x32,  // Clock source is TIMER 9 Output 0
            Timer9Output1 = 0x33,  // Clock source is TIMER 9 Output 1
            Timer10Output0 = 0x34, // Clock source is TIMER 10 Output 0
            Timer10Output1 = 0x35, // Clock source is TIMER 10 Output 1
            Timer11Output0 = 0x36, // Clock source is TIMER 11 Output 0
            Timer11Output1 = 0x37, // Clock source is TIMER 11 Output 1
            Timer12Output0 = 0x38, // Clock source is TIMER 12 Output 0
            Timer12Output1 = 0x39, // Clock source is TIMER 12 Output 1
            Timer13Output0 = 0x3A, // Clock source is TIMER 13 Output 0
            Timer13Output1 = 0x3B, // Clock source is TIMER 13 Output 1
            Timer14Output0 = 0x3C, // Clock source is TIMER 14 Output 0
            Timer14Output1 = 0x3D, // Clock source is TIMER 14 Output 1
            Timer15Output0 = 0x3E, // Clock source is TIMER 15 Output 0
            Timer15Output1 = 0x3F, // Clock source is TIMER 15 Output 1
            GPIO0 = 0x80,          // GPIO #0 is clock source
            GPIO63 = 0xBF,         // GPIO #63 is clock source
            GPIO95 = 0xDF,         // GPIO #95 is clock source
            GPIO127 = 0xFF,        // GPIO #127 is clock source
        }

        private enum FunctionSelect
        {
            Continous = 0x00,
            Edge = 0x01,
            Upcount = 0x02,
            PWM = 0x04,
            Downcount = 0x06,
            SinglePattern = 0x0C,
            RepeatPattern = 0x0D,
            EventTimer = 0x0E,
        }

        private enum PadOutput
        {
            Timer0Output0 = 0x00,  // Output is Timer 0, output 0
            Timer0Output1 = 0x01,  // Output is Timer 0, output 1
            Timer1Output0 = 0x02,  // Output is Timer 1, output 0
            Timer1Output1 = 0x03,  // Output is Timer 1, output 1
            Timer2Output0 = 0x04,  // Output is Timer 2, output 0
            Timer2Output1 = 0x05,  // Output is Timer 2, output 1
            Timer3Output0 = 0x06,  // Output is Timer 3, output 0
            Timer3Output1 = 0x07,  // Output is Timer 3, output 1
            Timer4Output0 = 0x08,  // Output is Timer 4, output 0
            Timer4Output1 = 0x09,  // Output is Timer 4, output 1
            Timer5Output0 = 0x0A,  // Output is Timer 5, output 0
            Timer5Output1 = 0x0B,  // Output is Timer 5, output 1
            Timer6Output0 = 0x0C,  // Output is Timer 6, output 0
            Timer6Output1 = 0x0D,  // Output is Timer 6, output 1
            Timer7Output0 = 0x0E,  // Output is Timer 7, output 0
            Timer7Output1 = 0x0F,  // Output is Timer 7, output 1
            Timer8Output0 = 0x10,  // Output is Timer 8, output 0
            Timer8Output1 = 0x11,  // Output is Timer 8, output 1
            Timer9Output0 = 0x12,  // Output is Timer 9, output 0
            Timer9Output1 = 0x13,  // Output is Timer 9, output 1
            Timer10Output0 = 0x14, // Output is Timer 10, output 0
            Timer10Output1 = 0x15, // Output is Timer 10, output 1
            Timer11Output0 = 0x16, // Output is Timer 11, output 0
            Timer11Output1 = 0x17, // Output is Timer 11, output 1
            Timer12Output0 = 0x18, // Output is Timer 12, output 0
            Timer12Output1 = 0x19, // Output is Timer 12, output 1
            Timer13Output0 = 0x1A, // Output is Timer 13, output 0
            Timer13Output1 = 0x1B, // Output is Timer 13, output 1
            Timer14Output0 = 0x1C, // Output is Timer 14, output 0
            Timer14Output1 = 0x1D, // Output is Timer 14, output 1
            Timer15Output0 = 0x1E, // Output is Timer 15, output 0
            Timer15Output1 = 0x1F, // Output is Timer 15, output 1
            STimer0 = 0x20,        // Output is STimer 0
            STimer1 = 0x21,        // Output is STimer 1
            STimer2 = 0x22,        // Output is STimer 2
            STimer3 = 0x23,        // Output is STimer 3
            STimer4 = 0x24,        // Output is STimer 4
            STimer5 = 0x25,        // Output is STimer 5
            STimer6 = 0x26,        // Output is STimer 6
            STimer7 = 0x27,        // Output is STimer 7
            Pattern0 = 0x20,       // Output is Pattern Output 0
            Pattern1 = 0x21,       // Output is Pattern Output 1
            Pattern2 = 0x22,       // Output is Pattern Output 2
            Pattern3 = 0x23,       // Output is Pattern Output 3
            Pattern4 = 0x24,       // Output is Pattern Output 4
            Pattern5 = 0x25,       // Output is Pattern Output 5
            Pattern6 = 0x26,       // Output is Pattern Output 6
            Pattern7 = 0x27,       // Output is Pattern Output 7
            Disabled = 0x3F,       // Output is disabled
        }

        private enum Registers : long
        {
            Control = 0x000,         // Counter/Timer Control
            Status = 0x004,          // Counter/Timer Status
            GlobalEnable = 0x010,    // Counter/Timer Global Enable
            InterruptEnable = 0x060, // Counter/Timer Interrupts: Enable
            InterruptStatus = 0x064, // Counter/Timer Interrupts: Status
            InterruptClear = 0x068,  // Counter/Timer Interrupts: Clear
            InterruptSet = 0x06C,    // Counter/Timer Interrupts: Set
            OutputConfig0 = 0x080,   // Counter/Timer Output Config 0
            OutputConfig1 = 0x084,   // Counter/Timer Output Config 0
            OutputConfig2 = 0x088,   // Counter/Timer Output Config 0
            OutputConfig3 = 0x08C,   // Counter/Timer Output Config 0
            OutputConfig4 = 0x090,   // Counter/Timer Output Config 0
            OutputConfig5 = 0x094,   // Counter/Timer Output Config 0
            OutputConfig6 = 0x098,   // Counter/Timer Output Config 0
            OutputConfig7 = 0x09C,   // Counter/Timer Output Config 0
            OutputConfig8 = 0x0A0,   // Counter/Timer Output Config 0
            OutputConfig9 = 0x0A4,   // Counter/Timer Output Config 0
            OutputConfig10 = 0x0A8,  // Counter/Timer Output Config 0
            OutputConfig11 = 0x0AC,  // Counter/Timer Output Config 0
            OutputConfig12 = 0x0B0,  // Counter/Timer Output Config 0
            OutputConfig13 = 0x0B4,  // Counter/Timer Output Config 0
            OutputConfig14 = 0x0B8,  // Counter/Timer Output Config 0
            OutputConfig15 = 0x0BC,  // Counter/Timer Output Config 0
            OutputConfig16 = 0x0C0,  // Counter/Timer Output Config 0
            OutputConfig17 = 0x0C4,  // Counter/Timer Output Config 0
            OutputConfig18 = 0x0C8,  // Counter/Timer Output Config 0
            OutputConfig19 = 0x0CC,  // Counter/Timer Output Config 0
            OutputConfig20 = 0x0D0,  // Counter/Timer Output Config 0
            OutputConfig21 = 0x0D4,  // Counter/Timer Output Config 0
            OutputConfig22 = 0x0D8,  // Counter/Timer Output Config 0
            OutputConfig23 = 0x0DC,  // Counter/Timer Output Config 0
            OutputConfig24 = 0x0E0,  // Counter/Timer Output Config 0
            OutputConfig25 = 0x0E4,  // Counter/Timer Output Config 0
            OutputConfig26 = 0x0E8,  // Counter/Timer Output Config 0
            OutputConfig27 = 0x0EC,  // Counter/Timer Output Config 0
            OutputConfig28 = 0x0F0,  // Counter/Timer Output Config 0
            OutputConfig29 = 0x0F4,  // Counter/Timer Output Config 0
            OutputConfig30 = 0x0F8,  // Counter/Timer Output Config 0
            OutputConfig31 = 0x0FC,  // Counter/Timer Output Config 0
            PatternAddress = 0x104,  // Pattern Address
            Timer0Control = 0x200,   // Counter/Timer Control
            Timer0 = 0x204,          // Counter/Timer
            Timer0Compare0 = 0x208,  // Counter/Timer 0 Primary Comparator
            Timer0Compare1 = 0x20C,  // Counter/Timer 0 Secondary Compare
            Timer0Mode = 0x210,      // Counter/Timer 0 Mode
            Timer1Control = 0x220,   // Counter/Timer Control
            Timer1 = 0x224,          // Counter/Timer
            Timer1Compare0 = 0x228,  // Counter/Timer 1 Primary Comparator
            Timer1Compare1 = 0x22C,  // Counter/Timer 1 Secondary Compare
            Timer1Mode = 0x230,      // Counter/Timer 1 Mode
            Timer2Control = 0x240,   // Counter/Timer Control
            Timer2 = 0x244,          // Counter/Timer
            Timer2Compare0 = 0x248,  // Counter/Timer 2 Primary Comparator
            Timer2Compare1 = 0x24C,  // Counter/Timer 2 Secondary Compare
            Timer2Mode = 0x250,      // Counter/Timer 2 Mode
            Timer3Control = 0x260,   // Counter/Timer Control
            Timer3 = 0x264,          // Counter/Timer
            Timer3Compare0 = 0x268,  // Counter/Timer 3 Primary Comparator
            Timer3Compare1 = 0x26C,  // Counter/Timer 3 Secondary Compare
            Timer3Mode = 0x270,      // Counter/Timer 3 Mode
            Timer4Control = 0x280,   // Counter/Timer Control
            Timer4 = 0x284,          // Counter/Timer
            Timer4Compare0 = 0x288,  // Counter/Timer 4 Primary Comparator
            Timer4Compare1 = 0x28C,  // Counter/Timer 4 Secondary Compare
            Timer4Mode = 0x290,      // Counter/Timer 4 Mode
            Timer5Control = 0x2A0,   // Counter/Timer Control
            Timer5 = 0x2A4,          // Counter/Timer
            Timer5Compare0 = 0x2A8,  // Counter/Timer 5 Primary Comparator
            Timer5Compare1 = 0x2AC,  // Counter/Timer 5 Secondary Compare
            Timer5Mode = 0x2B0,      // Counter/Timer 5 Mode
            Timer6Control = 0x2C0,   // Counter/Timer Control
            Timer6 = 0x2C4,          // Counter/Timer
            Timer6Compare0 = 0x2C8,  // Counter/Timer 6 Primary Comparator
            Timer6Compare1 = 0x2CC,  // Counter/Timer 6 Secondary Compare
            Timer6Mode = 0x2D0,      // Counter/Timer 6 Mode
            Timer7Control = 0x2E0,   // Counter/Timer Control
            Timer7 = 0x2E4,          // Counter/Timer
            Timer7Compare0 = 0x2E8,  // Counter/Timer 7 Primary Comparator
            Timer7Compare1 = 0x2EC,  // Counter/Timer 7 Secondary Compare
            Timer7Mode = 0x2F0,      // Counter/Timer 7 Mode
            Timer8Control = 0x300,   // Counter/Timer Control
            Timer8 = 0x304,          // Counter/Timer
            Timer8Compare0 = 0x308,  // Counter/Timer 8 Primary Comparator
            Timer8Compare1 = 0x30C,  // Counter/Timer 8 Secondary Compare
            Timer8Mode = 0x310,      // Counter/Timer 8 Mode
            Timer9Control = 0x320,   // Counter/Timer Control
            Timer9 = 0x324,          // Counter/Timer
            Timer9Compare0 = 0x328,  // Counter/Timer 9 Primary Comparator
            Timer9Compare1 = 0x32C,  // Counter/Timer 9 Secondary Compare
            Timer9Mode = 0x330,      // Counter/Timer 9 Mode
            Timer10Control = 0x340,  // Counter/Timer Control
            Timer10 = 0x344,         // Counter/Timer
            Timer10Compare0 = 0x348, // Counter/Timer 10 Primary Comparator
            Timer10Compare1 = 0x34C, // Counter/Timer 10 Secondary Compare
            Timer10Mode = 0x350,     // Counter/Timer 10 Mode
            Timer11Control = 0x360,  // Counter/Timer Control
            Timer11 = 0x364,         // Counter/Timer
            Timer11Compare0 = 0x368, // Counter/Timer 11 Primary Comparator
            Timer11Compare1 = 0x36C, // Counter/Timer 11 Secondary Compare
            Timer11Mode = 0x370,     // Counter/Timer 11 Mode
            Timer12Control = 0x380,  // Counter/Timer Control
            Timer12 = 0x384,         // Counter/Timer
            Timer12Compare0 = 0x388, // Counter/Timer 12 Primary Comparator
            Timer12Compare1 = 0x38C, // Counter/Timer 12 Secondary Compare
            Timer12Mode = 0x390,     // Counter/Timer 12 Mode
            Timer13Control = 0x3A0,  // Counter/Timer Control
            Timer13 = 0x3A4,         // Counter/Timer
            Timer13Compare0 = 0x3A8, // Counter/Timer 13 Primary Comparator
            Timer13Compare1 = 0x3AC, // Counter/Timer 13 Secondary Compare
            Timer13Mode = 0x3B0,     // Counter/Timer 13 Mode
            Timer14Control = 0x3C0,  // Counter/Timer Control
            Timer14 = 0x3C4,         // Counter/Timer
            Timer14Compare0 = 0x3C8, // Counter/Timer 14 Primary Comparator
            Timer14Compare1 = 0x3CC, // Counter/Timer 14 Secondary Compare
            Timer14Mode = 0x3D0,     // Counter/Timer 14 Mode
            Timer15Control = 0x3E0,  // Counter/Timer Control
            Timer15 = 0x3E4,         // Counter/Timer
            Timer15Compare0 = 0x3E8, // Counter/Timer 15 Primary Comparator
            Timer15Compare1 = 0x3EC, // Counter/Timer 15 Secondary Compare
            Timer15Mode = 0x3F0,     // Counter/Timer 15 Mode
        }
    }
}
