//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class Atmel91SystemTimer : IDoubleWordPeripheral, IKnownSize
    {
        public Atmel91SystemTimer(IMachine machine)
        {
            IRQ = new GPIO();

            PeriodIntervalTimer = new LimitTimer(machine.ClockSource, 32768, this, nameof(PeriodIntervalTimer), int.MaxValue); // long.MaxValue couses crashes
            PeriodIntervalTimer.Value = 0x00000000;
            PeriodIntervalTimer.AutoUpdate = true;
            PeriodIntervalTimer.LimitReached += PeriodIntervalTimerAlarmHandler;

            WatchdogTimer = new LimitTimer(machine.ClockSource, 32768, this, nameof(WatchdogTimer), int.MaxValue);
            WatchdogTimer.Value = 0x00020000;
            WatchdogTimer.AutoUpdate = true;
            WatchdogTimer.Divider = 128;
            WatchdogTimer.LimitReached += WatchdogTimerAlarmHandler;

            RealTimeTimer = new AT91_InterruptibleTimer(machine, 32768, this, nameof(RealTimeTimer), (ulong)BitHelper.Bit(20), Direction.Ascending);
            RealTimeTimer.Divider = 0x00008000;
            RealTimeTimer.OnUpdate += () => {
                lock (localLock)
                {
                    if (RealtimeTimerIncrementInterruptMask)
                    {
                        RealTimeTimerIncrement = true;
                    }
                }
            };
        }

        private void PeriodIntervalTimerAlarmHandler()
        {
            lock (localLock)
            {
            //this.Log(LogLevel.Noisy, "Period Interval Timer Alarm");

            if (PeriodIntervalTimerStatusInterruptMask)
            {
                PeriodIntervalTimerStatus = true;
                if (!IRQ.IsSet)
                {
                    this.Log(LogLevel.Noisy, "Setting IRQ due to PeriodIntervalTimerAlarm");
                }
                //IRQ.Set(false);
                IRQ.Set(true);
            }
            }
        }

        private void WatchdogTimerAlarmHandler()
        {
            lock (localLock)
            {
            this.Log(LogLevel.Noisy, "Watchdog Timer Alarm");

            WatchdogOverflow = true;
            }
        }

        public GPIO IRQ { get; private set; }

        #region IPeripheral implementation

        public void Reset()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDoubleWordPeripheral implementation

        public uint ReadDoubleWord(long offset)
        {
            lock (localLock)
            {
            switch ((Register)offset)
            {
            case Register.StatusRegister:
                var val = statusRegister;
                statusRegister = 0;
                IRQ.Unset();
                return val;

            case Register.PeriodIntervalModeRegister:
                return (uint)PeriodIntervalTimer.Limit;

            case Register.CurrentRealtimeRegister:
                return (uint)RealTimeTimer.Value;

            case Register.WatchdogModeRegister:
                return (uint)WatchdogTimer.Limit;

            default:
                this.LogUnhandledRead(offset);
                return 0u;
            }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock (localLock)
            {
            switch ((Register)offset)
            {
            case Register.ControlRegister:
                if (value == 0x1)
                {
                    WatchdogTimer.ResetValue();
                }
                break;

            case Register.PeriodIntervalModeRegister:
                PeriodIntervalTimer.Limit = value;
                PeriodIntervalTimer.Enabled = true;
                break;

            case Register.InterruptDisableRegister:
                this.Log(LogLevel.Noisy, "Disabling interrupt 0x{0:X}", value);
                interruptMaskRegister &= ~value;
                break;
               
            case Register.InterruptEnableRegister:
                this.Log(LogLevel.Noisy, "Enabling interrupt 0x{0:X}", value);
                interruptMaskRegister |= value;
                break;

            case Register.RealTimeModeRegister:
                RealTimeTimer.Divider = (int)value;
                break;

            default:
                this.LogUnhandledWrite(offset, value);
                return;
            }
            }
        }

        #endregion

        #region IKnownSize implementation

        public long Size {
            get {
                return 256;
            }
        }

        #endregion

        private LimitTimer PeriodIntervalTimer; // PIT
        private LimitTimer WatchdogTimer;       // WDT
        private AT91_InterruptibleTimer RealTimeTimer;       // RTT
        
        private uint interruptMaskRegister;             // TODO: uses only 4 bits
        private uint statusRegister;                    // TODO: uses only 4 bits

        private object localLock = new object();

        #region Bits

        private bool PeriodIntervalTimerStatus
        {
            get { return BitHelper.IsBitSet(statusRegister, 0); }
            set { BitHelper.SetBit(ref statusRegister, 0, value); }
        }
        private bool WatchdogOverflow
        {
            get { return BitHelper.IsBitSet(statusRegister, 1); }
            set { BitHelper.SetBit(ref statusRegister, 1, value); }
        }
        private bool RealTimeTimerIncrement
        {
            get { return BitHelper.IsBitSet(statusRegister, 2); }
            set { BitHelper.SetBit(ref statusRegister, 2, value); }
        }
        private bool AlarmStatus
        {
            get { return BitHelper.IsBitSet(statusRegister, 3); }
            set { BitHelper.SetBit(ref statusRegister, 3, value); }
        }

        private bool PeriodIntervalTimerStatusInterruptMask
        {
            get { return BitHelper.IsBitSet(interruptMaskRegister, 0); }
        }
        private bool WatchdogOverflowInterruptMask
        {
            get { return BitHelper.IsBitSet(interruptMaskRegister, 1); }
        }
        private bool RealtimeTimerIncrementInterruptMask
        {
            get { return BitHelper.IsBitSet(interruptMaskRegister, 2); }
        }
        private bool AlarmStatusInterruptMask
        {
            get { return BitHelper.IsBitSet(interruptMaskRegister, 3); }
        }

        #endregion

        private enum Register:uint
        {
            ControlRegister             = 0x0000,   // CR
            PeriodIntervalModeRegister  = 0x0004,   // PIMR
            WatchdogModeRegister        = 0x0008,   // WDMR - TODO: there is RSTEN bit mentioned in documentation, but not mapped to WDMR register
            RealTimeModeRegister        = 0x000C,   // RTMR
            StatusRegister              = 0x0010,   // SR
            InterruptEnableRegister     = 0x0014,   // IER
            InterruptDisableRegister    = 0x0018,   // IDR
            RealTimeAlarmRegister       = 0x0020,   // RTAR
            CurrentRealtimeRegister     = 0x0024,   // CRTR
        }

        private class AT91_InterruptibleTimer
        {
            private LimitTimer timer;
            private ulong? prevValue;
            private object lockobj = new object();

            public event Action OnUpdate;

            public AT91_InterruptibleTimer(IMachine machine, long frequency, IPeripheral owner, string localName, ulong limit = ulong.MaxValue, Direction direction = Direction.Descending, bool enabled = false)
            {
                timer = new LimitTimer(machine.ClockSource, frequency, owner, localName, limit, direction, enabled);
                timer.LimitReached += () => { if (OnUpdate != null) OnUpdate(); };
            }

            public ulong Value
            {
                get
                {
                    lock (lockobj)
                    {
                        if (!prevValue.HasValue)
                        {
                            prevValue = timer.Value;
                            return prevValue.Value;
                        }
                        else
                        {
                            var result = prevValue.Value;
                            prevValue = null;
                            return result;
                        }
                    }
                }
                set
                {
                    lock (lockobj)
                    {
                        prevValue = null;
                        timer.Value = value;
                    }
                }
            }

            public int Divider
            {
                get { return timer.Divider; }
                set { timer.Divider = value; }
            }

            public void Enable()
            {
                timer.Enabled = true;
            }
        }
    }
}

