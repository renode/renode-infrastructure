//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class MAX32650_RTC : BasicDoubleWordPeripheral, IKnownSize
    {
        public MAX32650_RTC(IMachine machine, bool subSecondsMSBOverwrite = false, string baseDateTime = null, bool secondsTickOnOneSubSecond = false) : base(machine)
        {
            BaseDateTime = Misc.UnixEpoch;
            if(baseDateTime != null)
            {
                if(!DateTime.TryParse(baseDateTime, out var parsedBaseDateTime))
                {
                    throw new Exceptions.ConstructionException($"Invalid 'baseDateTime': {baseDateTime}");
                }
                BaseDateTime = parsedBaseDateTime;
            }
            machine.RealTimeClockModeChanged += _ => SetDateTimeFromMachine();

            DefineRegisters();

            internalTimer = new LimitTimer(machine.ClockSource, SubSecondCounterResolution, this, "timer", limit: SubSecondCounterResolution, direction: Direction.Ascending, enabled: false, eventEnabled: true);
            internalTimer.LimitReached += SecondTick;
            subSecondAlarmTimer = new LimitTimer(machine.ClockSource, SubSecondCounterResolution, this, "ss_alarm", limit: SubSecondAlarmMaxValue, direction: Direction.Ascending, enabled: false, eventEnabled: true);
            subSecondAlarmTimer.LimitReached += SubSecondAlarm;

            this.subSecondsMSBOverwrite = subSecondsMSBOverwrite;
            this.secondsTickOnOneSubSecond = secondsTickOnOneSubSecond;

            IRQ = new GPIO();

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();

            internalTimer.Reset();
            subSecondAlarmTimer.Reset();

            secondsCounter = 0;
            secondsCounterCache = 0;
            subSecondsCounterCache = 0;
            secondsCounterReadFlag = false;
            subSecondsCounterReadFlag = false;


            SetDateTimeFromMachine(hushLog: true);
        }

        public string PrintPreciseCurrentDateTime()
        {
            return CurrentDateTime.ToString("o");
        }

        public void SetDateTime(int? year = null, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null, double? millisecond = null)
        {
            SetDateTime(CurrentDateTime.With(year, month, day, hour, minute, second, millisecond));
        }

        public DateTime BaseDateTime { get; }

        public DateTime CurrentDateTime => BaseDateTime + TimePassedSinceBaseDateTime;

        public GPIO IRQ { get; }

        public long Size => 0x400;

        public byte SubSecondsSignificantBits => (byte)(subSecondsCounterCache >> 8);

        public TimeSpan TimePassedSinceBaseDateTime => TimeSpan.FromSeconds(CurrentSecond + ((double)CurrentSubSecond / SubSecondCounterResolution));

        private static uint CalculateSubSeconds(double seconds)
        {
            var subSecondFraction = seconds % 1;
            return (uint)(subSecondFraction * SubSecondCounterResolution);
        }

        private void UpdateCounterCacheIfInvalid()
        {
            // MAX32650_RTC datasheet says that RTC_SEC.sec and RTC_SSEC.ssec registers should
            // be stable for 120us after RTC_CTRL.ready is set.
            // As we are always setting RTC_CTRL.ready, in order to simulate
            // this behavior, we are caching both registers when one of them is read
            // and returning cached value when another is read.
            // It might provide invalid result when there is intentionally large
            // delay between registers read.
            if(secondsCounterReadFlag && subSecondsCounterReadFlag)
            {
                // return cached value
                secondsCounterReadFlag = false;
                subSecondsCounterReadFlag = false;
            }
            else
            {
                secondsCounterCache = (uint)CurrentSecond;
                subSecondsCounterCache = (uint)CurrentSubSecond;
            }
        }

        private void DefineRegisters()
        {
            Registers.Seconds.Define(this)
                .WithValueField(0, 32, name: "RTC_SEC.sec",
                    valueProviderCallback: _ =>
                    {
                        secondsCounterReadFlag = true;
                        UpdateCounterCacheIfInvalid();
                        return secondsCounterCache;
                    },
                    writeCallback: (_, value) =>
                    {
                        lock(countersLock)
                        {
                            secondsCounter = (uint)value;
                            UpdateCounterCacheIfInvalid();
                        }
                    });
            Registers.SubSeconds.Define(this)
                .WithValueField(0, 8, name: "RTC_SSEC.ssec",
                    valueProviderCallback: _ =>
                    {
                        subSecondsCounterReadFlag = true;
                        UpdateCounterCacheIfInvalid();
                        return (byte)subSecondsCounterCache;
                    },
                    writeCallback: (_, value) =>
                    {
                        lock(countersLock)
                        {
                            internalTimer.Value = (ulong)((subSecondsMSBOverwrite ? 0xF00 : (CurrentSubSecond & 0xF00)) | (uint)value);
                            UpdateCounterCacheIfInvalid();
                        }
                    })
                .WithReservedBits(8, 24);
            Registers.TimeOfDayAlarm.Define(this)
                .WithValueField(0, 20, out timeOfDayAlarm, name: "RTC_TODA.tod_alarm")
                .WithReservedBits(20, 12);
            Registers.SubSecondAlarm.Define(this)
                .WithValueField(0, 32, out subSecondAlarm, name: "RTC_SSECA.ssec_alarm",
                    writeCallback: (_, value) => subSecondAlarmTimer.Value = value);
            Registers.Control.Define(this)
                .WithFlag(0, name: "RTC_CTRL.enable",
                    valueProviderCallback: _ => internalTimer.Enabled,
                    changeCallback: (_, value) =>
                    {
                        if(!canBeToggled.Value)
                        {
                            this.Log(LogLevel.Warning, "Tried to write RTC_CTRL.enable with RTC_CTRL.write_en disabled");
                            return;
                        }
                        internalTimer.Enabled = value;
                    })
                .WithFlag(1, out timeOfDayAlarmEnabled, name: "RTC_CTRL.tod_alarm_en")
                .WithFlag(2, name: "RTC_CTRL.ssec_alarm_en",
                    valueProviderCallback: _ => subSecondAlarmTimer.Enabled,
                    writeCallback: (_, value) => subSecondAlarmTimer.Enabled = value)
                .WithFlag(3, name: "RTC_CTRL.busy", valueProviderCallback: _ => false)
                // It seems that on real HW, semantic of the READY bit is inverted, that is
                // when RTC_CTRL.ready is set to false, then software is able to read
                // correct data from RTC_SEC and RTC_SSEC registers.
                .WithFlag(4, name: "RTC_CTRL.ready",
                    valueProviderCallback: _ => false,
                    writeCallback: (_, value) =>
                    {
                        // SW sets the bit to false to force registers values synchronization
                        if(value == false)
                        {
                            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                            {
                                cpu.SyncTime();
                            }
                        }
                    })
                .WithFlag(5, out readyInterruptEnabled, name: "RTC_CTRL.ready_int_en")
                .WithFlag(6, out timeOfDayAlarmFlag, name: "RTC_CTRL.tod_alarm_fl")
                .WithFlag(7, out subSecondAlarmFlag, name: "RTC_CTRL.ssec_alarm_fl")
                .WithTaggedFlag("RTC_CTRL.sqwout_en", 8)
                .WithTag("RTC_CTRL.freq_sel", 9, 2)
                .WithReservedBits(11, 3)
                .WithTaggedFlag("RTC_CTRL.acre", 14)
                .WithFlag(15, out canBeToggled, name: "RTC_CTRL.write_en")
                .WithReservedBits(16, 16);
            Registers.OscillatorControl.Define(this)
                .WithReservedBits(0, 4)
                .WithTaggedFlag("RTC_OSCCTRL.bypass", 4)
                .WithTaggedFlag("RTC_OSCCTRL.32kout", 5)
                .WithReservedBits(6, 26);
        }

        private void SetDateTime(DateTime dateTime, bool hushLog = false)
        {
            if(dateTime < BaseDateTime)
            {
                this.Log(LogLevel.Warning, "Tried to set DateTime older than the RTC's BaseDateTime ({0}): {1:o}", BaseDateTime, dateTime);
                return;
            }
            var sinceBaseDateTime = dateTime - BaseDateTime;

            lock(countersLock)
            {
                secondsCounter = (uint)sinceBaseDateTime.TotalSeconds;
                internalTimer.Value = CalculateSubSeconds(sinceBaseDateTime.TotalSeconds);

                if(!hushLog)
                {
                    this.Log(LogLevel.Info, "New date time set: {0:o}", CurrentDateTime);
                }
            }
        }

        private void SetDateTimeFromMachine(bool hushLog = false)
        {
            SetDateTime(machine.RealTimeClockDateTime, hushLog);
        }

        private void SecondTick()
        {
            lock(countersLock)
            {
                secondsCounter += 1;
                if(timeOfDayAlarmEnabled.Value && (secondsCounter & TimeOfDayAlarmMask) == timeOfDayAlarm.Value)
                {
                    timeOfDayAlarmFlag.Value = true;
                }

                UpdateInterrupts();
            }
        }

        private void SubSecondAlarm()
        {
            subSecondAlarmTimer.Value = subSecondAlarm.Value;
            subSecondAlarmFlag.Value = true;
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var interruptPending = readyInterruptEnabled.Value;
            interruptPending |= timeOfDayAlarmEnabled.Value && timeOfDayAlarmFlag.Value;
            interruptPending |= subSecondAlarmTimer.Enabled && subSecondAlarmFlag.Value;
            IRQ.Set(interruptPending);
        }

        private ulong CurrentSecond
        {
            get
            {
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                // On some revisions of the HW the seconds counter won't tick on subseconds
                // counter overflow, but rather after first tick on value=1. This enables
                // this behaviour when secondsTickOnOneSubSecond is set.
                if(secondsTickOnOneSubSecond && internalTimer.Value == 0)
                {
                    return secondsCounter > 0 ? secondsCounter - 1 : secondsCounter;
                }
                return secondsCounter;
            }
        }

        private ulong CurrentSubSecond
        {
            get
            {
                if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
                {
                    cpu.SyncTime();
                }
                return internalTimer.Value;
            }
        }

        private uint secondsCounter;
        private uint secondsCounterCache;
        private uint subSecondsCounterCache;
        private bool secondsCounterReadFlag;
        private bool subSecondsCounterReadFlag;

        private IFlagRegisterField canBeToggled;
        private IFlagRegisterField readyInterruptEnabled;
        private IFlagRegisterField subSecondAlarmFlag;
        private IFlagRegisterField timeOfDayAlarmEnabled;
        private IFlagRegisterField timeOfDayAlarmFlag;

        private IValueRegisterField subSecondAlarm;
        private IValueRegisterField timeOfDayAlarm;

        private readonly LimitTimer internalTimer;
        private readonly LimitTimer subSecondAlarmTimer;
        private readonly object countersLock = new object();
        private readonly bool subSecondsMSBOverwrite;

        private const long SubSecondAlarmMaxValue = 0xFFFFFFFF;
        private const uint SubSecondCounterResolution = 4096;
        private const ulong TimeOfDayAlarmMask = 0xFFFFF;
        // Some revisions of this peripheral have HW bug that makes
        // RTC to increase seconds counter when subseconds are 1 instead on 0.
        // This flag allows to enable this behavior.
        private bool secondsTickOnOneSubSecond;

        private enum Registers
        {
            Seconds = 0x00,
            SubSeconds = 0x04,
            TimeOfDayAlarm = 0x08,
            SubSecondAlarm = 0x0C,
            Control = 0x10,
            OscillatorControl = 0x18,
        }
    }
}
