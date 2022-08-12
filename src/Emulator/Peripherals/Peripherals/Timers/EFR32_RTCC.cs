//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EFR32_RTCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32_RTCC(Machine machine, long frequency) : base(machine)
        {
            IRQ = new GPIO();

            captureCompareInterruptPending = new IFlagRegisterField[NumberOfCaptureCompareChannels];
            captureCompareInterruptEnabled = new IFlagRegisterField[NumberOfCaptureCompareChannels];
            mode = new IEnumRegisterField<CCChannelMode>[NumberOfCaptureCompareChannels];
            inputCaptureEdgeSelect = new IEnumRegisterField<Edge>[NumberOfCaptureCompareChannels];
            comparisonBase = new IEnumRegisterField<CCChannelComparisonBase>[NumberOfCaptureCompareChannels];

            innerTimer = new InnerTimer(machine.ClockSource, frequency, this, "rtcc", CounterWidth, PreCounterWidth);
            innerTimer.LimitReached += delegate
            {
                overflowInterrupt.Value = true;
                UpdateInterrupts();
            };
            innerTimer.CounterTicked += delegate
            {
                counterTick.Value = true;
                UpdateInterrupts();
            };

            for(var idx = 0; idx < NumberOfCaptureCompareChannels; ++idx)
            {
                var i = idx;
                innerTimer.Channels[i].CompareReached += delegate
                {
                    captureCompareInterruptPending[i].Value = true;
                    UpdateInterrupts();
                };
            }

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            innerTimer.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x400;

        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
            Register.Control.Define(this)
                .WithFlag(0,
                    writeCallback: (_, value) => innerTimer.Enabled = value,
                    valueProviderCallback: _ => innerTimer.Enabled,
                    name: "ENABLE")
                .WithReservedBits(1, 1)
                .WithTaggedFlag("DEBUGRUN", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("PRECCV0TOP", 4)
                .WithTaggedFlag("CCV1TOP", 5)
                .WithReservedBits(6, 2)
                .WithValueField(8, 4,
                    writeCallback: (_, value) => innerTimer.Prescaler = (int)Math.Pow(2, value),
                    valueProviderCallback: _ => (uint)Math.Log(innerTimer.Prescaler, 2),
                    name: "CNTPRESC")
                .WithTaggedFlag("CNTTICK", 12)
                .WithReservedBits(13, 2)
                .WithTaggedFlag("OSCFDETEN", 15)
                .WithTag("CNTMODE", 16, 1)
                .WithTaggedFlag("LYEARCORRDIS", 17)
                .WithReservedBits(18, 14)
            ;

            Register.PreCounter.Define(this)
                .WithValueField(0, 15,
                    writeCallback: (_, value) =>
                    {
                        innerTimer.PreCounter = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        TrySyncTime();
                        return (uint)innerTimer.PreCounter;
                    },
                    name: "PRECNT")
                .WithReservedBits(15, 17)
            ;

            Register.CounterValue.Define(this)
                .WithValueField(0, 32,
                    writeCallback: (_, value) =>
                    {
                        innerTimer.Counter = value;
                    },
                    valueProviderCallback: _ =>
                    {
                        TrySyncTime();
                        return (uint)innerTimer.Counter;
                    },
                    name: "CNT")
            ;

            Register.CombinedPreCouterValue.Define(this)
                .WithValueField(0, 15, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        TrySyncTime();
                        return (uint)innerTimer.PreCounter;
                    },
                    name: "PRECNT")
                .WithValueField(15, 17, FieldMode.Read,
                    valueProviderCallback: _ =>
                    {
                        TrySyncTime();
                        return (uint)innerTimer.Counter;
                    },
                    name: "CNTLSB")
            ;

            Register.Time.Define(this)
                .WithTag("SECU", 0, 4)
                .WithTag("SECT", 4, 3)
                .WithReservedBits(7, 1)
                .WithTag("MINU", 8, 4)
                .WithTag("MINT", 12, 3)
                .WithReservedBits(15, 1)
                .WithTag("HOURU", 16, 4)
                .WithTag("HOURT", 20, 2)
                .WithReservedBits(22, 10)
            ;

            Register.Date.Define(this)
                .WithTag("DAYOMU", 0, 4)
                .WithTag("DAYOMT", 4, 2)
                .WithReservedBits(6, 2)
                .WithTag("MONTHU", 8, 4)
                .WithTag("MONTHT", 12, 1)
                .WithReservedBits(13, 3)
                .WithTag("YEARU", 16, 4)
                .WithTag("YEART", 20, 4)
                .WithTag("DAYOW", 24, 3)
                .WithReservedBits(27, 5)
            ;

            Register.InterruptFlags.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => overflowInterrupt.Value, name: "OF")
                .WithFlags(1, 3, FieldMode.Read, valueProviderCallback: (i, _) => captureCompareInterruptPending[i].Value, name: "CC")
                .WithTaggedFlag("OSCFAIL", 4)
                .WithFlag(5, FieldMode.Read, valueProviderCallback: _ => counterTick.Value, name: "CNTTICK")
                .WithTaggedFlag("MINTICK", 6)
                .WithTaggedFlag("HOURTICK", 7)
                .WithTaggedFlag("DAYTICK", 8)
                .WithTaggedFlag("DAYOWOF", 9)
                .WithTaggedFlag("MONTHTICK", 10)
                .WithReservedBits(11, 21)
            ;

            Register.InterruptFlagSet.Define(this)
                .WithFlag(0, FieldMode.Set, writeCallback: (_, value) => overflowInterrupt.Value |= value, name: "OF")
                .WithFlags(1, 3, FieldMode.Set, writeCallback: (i, _, value) => captureCompareInterruptPending[i].Value |= value, name: "CC")
                .WithTaggedFlag("OSCFAIL", 4)
                .WithFlag(5, FieldMode.Set, writeCallback: (_, value) => counterTick.Value |= value, name: "CNTTICK")
                .WithTaggedFlag("MINTICK", 6)
                .WithTaggedFlag("HOURTICK", 7)
                .WithTaggedFlag("DAYTICK", 8)
                .WithTaggedFlag("DAYOWOF", 9)
                .WithTaggedFlag("MONTHTICK", 10)
                .WithReservedBits(11, 21)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;

            Register.InterruptFlagClear.Define(this)
                .WithFlag(0, out overflowInterrupt, FieldMode.ReadToClear | FieldMode.WriteOneToClear, name: "OF")
                .WithFlags(1, 3, out captureCompareInterruptPending, FieldMode.ReadToClear | FieldMode.WriteOneToClear, name: "CC")
                .WithTaggedFlag("OSCFAIL", 4)
                .WithFlag(5, out counterTick, FieldMode.ReadToClear | FieldMode.WriteOneToClear, name: "CNTTICK")
                .WithTaggedFlag("MINTICK", 6)
                .WithTaggedFlag("HOURTICK", 7)
                .WithTaggedFlag("DAYTICK", 8)
                .WithTaggedFlag("DAYOWOF", 9)
                .WithTaggedFlag("MONTHTICK", 10)
                .WithReservedBits(11, 21)
                .WithWriteCallback((_, __) => UpdateInterrupts())
                .WithReadCallback((_, __) => UpdateInterrupts())
            ;

            Register.InterruptEnable.Define(this)
                .WithFlag(0, out overflowInterruptEnabled, name: "OF")
                .WithFlags(1, 3, out captureCompareInterruptEnabled, name: "CC")
                .WithTaggedFlag("OSCFAIL", 4)
                .WithFlag(5, out counterTickEnabled, name: "CNTTICK")
                .WithTaggedFlag("MINTICK", 6)
                .WithTaggedFlag("HOURTICK", 7)
                .WithTaggedFlag("DAYTICK", 8)
                .WithTaggedFlag("DAYOWOF", 9)
                .WithTaggedFlag("MONTHTICK", 10)
                .WithReservedBits(11, 21)
                .WithChangeCallback((_, __) => UpdateInterrupts())
            ;

            Register.Status.Define(this)
                .WithReservedBits(0, 32)
            ;

            Register.Command.Define(this)
                .WithTaggedFlag("CLRSTATUS", 0)
                .WithReservedBits(1, 31)
            ;

            Register.SyncBusy.Define(this)
                .WithReservedBits(0, 5)
                .WithTaggedFlag("CMD", 5)
                .WithReservedBits(6, 26)
            ;

            Register.PowerDown.Define(this)
                .WithTaggedFlag("RAM", 0)
                .WithReservedBits(1, 31)
            ;

            Register.ConfLock.Define(this)
                .WithTag("LOCKKEY", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Register.WakeUpEnable.Define(this)
                .WithTaggedFlag("EM4WU", 0)
                .WithReservedBits(1, 31)
            ;

            Register.ChanelControlC0.DefineMany(this, NumberOfCaptureCompareChannels, (register, idx) =>
                register
                    .WithEnumField(0, 2, out mode[idx],
                        changeCallback: (_, __) => innerTimer.Channels[idx].Enabled = mode[idx].Value != CCChannelMode.Off,
                        name: "MODE")
                    .WithTag("CMOA", 2, 2)
                    .WithEnumField(4, 2, out inputCaptureEdgeSelect[idx], name: "ICEDGE")
                    .WithTag("PRSSEL", 6, 4)
                    .WithReservedBits(10, 1)
                    .WithEnumField(11, 1, out comparisonBase[idx],
                        changeCallback: (_, __) => innerTimer.Channels[idx].CompareToCounter = comparisonBase[idx].Value != CCChannelComparisonBase.PreCounter,
                        name: "COMPBASE")
                    .WithTag("COMPMASK", 12, 5)
                    .WithTaggedFlag("DAYCC", 17)
                    .WithReservedBits(18, 14)
                , stepInBytes: StepBetweenCaptureCompareRegisters)
            ;

            Register.CaptureValueC0.DefineMany(this, NumberOfCaptureCompareChannels, (register, idx) =>
                register
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => innerTimer.Channels[idx].CompareValue = value,
                        name: "CCV")
                , stepInBytes: StepBetweenCaptureCompareRegisters)
            ;

            Register.CaptureTimeC0.DefineMany(this, NumberOfCaptureCompareChannels, (register, idx) =>
                register
                    .WithTag("SECU", 0, 4)
                    .WithTag("SECT", 4, 3)
                    .WithReservedBits(7, 1)
                    .WithTag("MINU", 8, 4)
                    .WithTag("MINT", 12, 3)
                    .WithReservedBits(15, 1)
                    .WithTag("HOURU", 16, 4)
                    .WithTag("HOURT", 20, 2)
                    .WithReservedBits(22, 10)
                , stepInBytes: StepBetweenCaptureCompareRegisters)
            ;

            Register.CaptureDateC0.DefineMany(this, NumberOfCaptureCompareChannels, (register, idx) =>
                register
                    .WithTag("DAYU", 0, 4)
                    .WithTag("DAYT", 4, 2)
                    .WithReservedBits(6, 2)
                    .WithTag("MONTHU", 8, 4)
                    .WithTag("MONTHT", 12, 1)
                    .WithReservedBits(13, 19)
                , stepInBytes: StepBetweenCaptureCompareRegisters)
            ;

            Register.Retention0.DefineMany(this, NumberOfRetentionRegisters, (reg, idx) =>
                reg.WithValueField(0, 32, name: "REG")) //these registers store user-written values, no additional logic
            ;
        }

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private void UpdateInterrupts()
        {
            // Executed in lock as a precaution against the gpio/BaseClockSource deadlock until a proper fix is ready
            machine.ClockSource.ExecuteInLock(delegate
            {
                var value = false;
                value |= overflowInterrupt.Value && overflowInterruptEnabled.Value;
                value |= captureCompareInterruptPending.Zip(captureCompareInterruptEnabled, (a, b) => a.Value && b.Value).Any(a => a);
                value |= counterTick.Value && counterTickEnabled.Value;
                IRQ.Set(value);
            });
        }

        private readonly InnerTimer innerTimer;

        private IFlagRegisterField overflowInterrupt;
        private IFlagRegisterField overflowInterruptEnabled;
        private IFlagRegisterField[] captureCompareInterruptPending;
        private IFlagRegisterField[] captureCompareInterruptEnabled;
        private IFlagRegisterField counterTick;
        private IFlagRegisterField counterTickEnabled;
        private IEnumRegisterField<CCChannelMode>[] mode;
        private IEnumRegisterField<Edge>[] inputCaptureEdgeSelect;
        private IEnumRegisterField<CCChannelComparisonBase>[] comparisonBase;

        private const int NumberOfRetentionRegisters = 32;
        private const int NumberOfCaptureCompareChannels = 3;
        private const int StepBetweenCaptureCompareRegisters = Register.ChanelControlC1 - Register.ChanelControlC0;
        private const int CounterWidth = 32;
        private const int PreCounterWidth = 15;

        private class InnerTimer
        {
            public InnerTimer(IClockSource clockSource, long frequency, IPeripheral owner, string localName, int counterWidth = 32, int preCounterWidth = 32)
            {
                var counterLimit = (1UL << counterWidth) - 1;
                var preCounterLimit = (1UL << preCounterWidth) - 1;

                coreTimer = new LimitTimer(clockSource, frequency, owner, localName, counterLimit, Direction.Ascending, eventEnabled: true, autoUpdate: true);
                coreTimerTick = new LimitTimer(clockSource, frequency, owner, $"{localName}-tick", 1, Direction.Ascending, eventEnabled: true, autoUpdate: true);
                preTimer = new LimitTimer(clockSource, frequency, owner, $"{localName}-pre", preCounterLimit, Direction.Ascending, eventEnabled: true, autoUpdate: true);
                coreTimer.LimitReached += () => Array.ForEach(channels, channel => channel.TimerEnabled = true);

                channels = new CCChannel[NumberOfCaptureCompareChannels];
                for(var i = 0; i < NumberOfCaptureCompareChannels; ++i)
                {
                    channels[i] = new CCChannel(new LimitTimer(clockSource, frequency, owner, $"{localName}-cc{i}", counterLimit, Direction.Ascending, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true));
                }
            }

            public void Reset()
            {
                coreTimer.Reset();
                foreach(var channel in channels)
                {
                    channel.Reset();
                }
            }

            public ulong Counter
            {
                get => coreTimer.Value;
                set
                {
                    coreTimer.Value = value;
                    foreach(var channel in channels)
                    {
                        channel.Value = value;
                        channel.TimerEnabled = value <= channel.CompareValue;
                    }
                }
            }

            public ulong PreCounter
            {
                get => preTimer.Value;
                set => preTimer.Value = value;
            }

            public bool Enabled
            {
                get => coreTimer.Enabled;
                set
                {
                    if(value == coreTimer.Enabled)
                    {
                        return;
                    }
                    coreTimer.Enabled = value;
                    coreTimerTick.Enabled = value;
                    preTimer.Enabled = value;
                    foreach(var channel in channels)
                    {
                        channel.TimerEnabled = value && coreTimer.Value <= channel.CompareValue;
                    }
                }
            }

            public int Prescaler
            {
                get => coreTimer.Divider;
                set
                {
                    if(value == coreTimer.Divider)
                    {
                        return;
                    }
                    coreTimer.Divider = value;
                    coreTimerTick.Divider = value;
                    foreach(var channel in channels)
                    {
                        channel.Prescaler = value;
                    }
                }
            }

            public ICCChannel[] Channels => channels;

            public event Action LimitReached
            {
                add => coreTimer.LimitReached += value;
                remove => coreTimer.LimitReached -= value;
            }

            public event Action CounterTicked
            {
                add => coreTimerTick.LimitReached += value;
                remove => coreTimerTick.LimitReached -= value;
            }

            private readonly CCChannel[] channels;

            private readonly LimitTimer coreTimer;
            private readonly LimitTimer coreTimerTick;
            private readonly LimitTimer preTimer;

            public interface ICCChannel
            {
                bool Enabled { get; set; }

                bool CompareToCounter { get; set; }

                ulong CompareValue { get; set; }

                event Action CompareReached;
            }

            private class CCChannel : ICCChannel
            {
                public CCChannel(LimitTimer timer)
                {
                    this.timer = timer;
                }

                public void Reset()
                {
                    timer.Reset();
                }

                public bool Enabled
                {
                    get => enabled;

                    set
                    {
                        enabled = value;
                        timer.Enabled = enabled && timerEnabled && compareToCounter;
                    }
                }

                public bool TimerEnabled
                {
                    set
                    {
                        timerEnabled = value;
                        timer.Enabled = Enabled && timerEnabled && compareToCounter;
                    }
                }

                public bool CompareToCounter
                {
                    get
                    {
                        return compareToCounter;
                    }

                    set
                    {
                        if(compareToCounter == value)
                        {
                            return;
                        }
                        compareToCounter = value;
                        if(!value)
                        {
                            Logger.Log(LogLevel.Error, "Comparing to PreCounter value is not implemented. Channel disabled.");
                        }
                        timer.Enabled = Enabled && timerEnabled && compareToCounter;
                    }
                }

                public ulong CompareValue
                {
                    get => timer.Limit;
                    set => timer.Limit = value;
                }

                public int Prescaler
                {
                    set => timer.Divider = value;
                }

                public ulong Value
                {
                    set => timer.Value = value;
                }

                public event Action CompareReached
                {
                    add => timer.LimitReached += value;
                    remove => timer.LimitReached -= value;
                }

                private bool enabled;
                private bool timerEnabled;
                private bool compareToCounter;
                private readonly LimitTimer timer;
            }
        }

        private enum Edge
        {
            Rising = 0x0,
            Falling = 0x1,
            Both = 0x2,
            None = 0x3,
        }

        private enum CCChannelMode
        {
            Off = 0,
            InputCapture = 1,
            OutputCompare = 2,
        }

        private enum CCChannelComparisonBase
        {
            Counter = 0x0,
            PreCounter = 0x1,
        }

        private enum Register
        {
            Control = 0x0,
            PreCounter = 0x4,
            CounterValue = 0x8,
            CombinedPreCouterValue = 0xC,
            Time = 0x10,
            Date = 0x14,
            InterruptFlags = 0x18,
            InterruptFlagSet = 0x1C,
            InterruptFlagClear = 0x20,
            InterruptEnable = 0x24,
            Status = 0x28,
            Command = 0x2C,
            SyncBusy = 0x30,
            PowerDown = 0x34,
            ConfLock = 0x38,
            WakeUpEnable = 0x3C,
            ChanelControlC0 = 0x40,
            CaptureValueC0 = 0x44,
            CaptureTimeC0 = 0x48,
            CaptureDateC0 = 0x4C,
            ChanelControlC1 = 0x50,
            CaptureValueC1 = 0x54,
            CaptureTimeC1 = 0x58,
            CaptureDateC1 = 0x5C,
            ChanelControlC2 = 0x60,
            CaptureValueC2 = 0x64,
            CaptureTimeC2 = 0x68,
            CaptureDateC2 = 0x6C,
            Retention0 = 0x104,
            Retention1 = 0x108,
            // ...
            Retention31 = 0x180,
        }
    }
}
