//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using System;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class EFR32_RTCCCounter
    {
        public EFR32_RTCCCounter(IMachine machine, long frequency, IPeripheral owner, string localName, int counterWidth = 32, int preCounterWidth = 32, int numberOfCaptureCompareChannels = 3)
        {
            var counterLimit = (1UL << counterWidth) - 1;
            var preCounterLimit = (1UL << preCounterWidth) - 1;

            coreTimer = new LimitTimer(machine.ClockSource, frequency, owner, localName, counterLimit, Direction.Ascending, eventEnabled: true, autoUpdate: true);
            coreTimerTick = new LimitTimer(machine.ClockSource, frequency, owner, $"{localName}-tick", 1, Direction.Ascending, eventEnabled: true, autoUpdate: true);
            preTimer = new LimitTimer(machine.ClockSource, frequency, owner, $"{localName}-pre", preCounterLimit, Direction.Ascending, eventEnabled: true, autoUpdate: true);

            channels = new CCChannel[numberOfCaptureCompareChannels];
            for(var i = 0; i < numberOfCaptureCompareChannels; ++i)
            {
                var channelTimer = new LimitTimer(machine.ClockSource, frequency, owner, $"{localName}-cc{i}", counterLimit, Direction.Ascending, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: true);
                channels[i] = new CCChannel(channelTimer, coreTimer, owner, i);
            }
            this.machine = machine;
        }

        public void Reset()
        {
            coreTimer.Reset();
            coreTimerTick.Reset();
            preTimer.Reset();
            foreach(var channel in channels)
            {
                channel.Reset();
            }
        }

        public ulong Counter
        {
            get
            {
                TrySyncTime();
                return coreTimer.Value;
            }
            set
            {
                coreTimer.Value = value;
                foreach(var channel in channels)
                {
                    channel.Value = value;
                }
            }
        }

        public ulong PreCounter
        {
            get
            {
                TrySyncTime();
                return preTimer.Value;
            }
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
                    channel.Refresh();
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
                    channel.Divider = value;
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

        private bool TrySyncTime()
        {
            if(machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
                return true;
            }
            return false;
        }

        private readonly CCChannel[] channels;
        private readonly LimitTimer coreTimer;
        private readonly LimitTimer coreTimerTick;
        private readonly LimitTimer preTimer;
        private readonly IMachine machine;

        public enum CCChannelMode
        {
            Off = 0,
            InputCapture = 1,
            OutputCompare = 2,
        }

        public enum CCChannelComparisonBase
        {
            Counter = 0x0,
            PreCounter = 0x1,
        }

        public interface ICCChannel
        {
            CCChannelMode Mode { get; set; }
            CCChannelComparisonBase ComparisonBase { get; set; }
            ulong CompareValue { get; set; }
            event Action CompareReached;
        }

        private class CCChannel : ICCChannel
        {
            public CCChannel(LimitTimer ownTimer, LimitTimer coreTimer, IPeripheral owner, int id)
            {
                this.channelTimer = ownTimer;
                this.coreTimer = coreTimer;
                this.owner = owner;
                this.id = id;
            }

            public void Reset()
            {
                channelTimer.Reset();
            }

            public void Refresh()
            {
                var isEnabled = mode != CCChannelMode.Off;
                var isTargetInThePast = CompareValue < coreTimer.Value;
                var isAlreadyRunning = channelTimer.Enabled;
                var shouldEnableTimer = isEnabled && !isTargetInThePast;

                if(!isAlreadyRunning && shouldEnableTimer)
                {
                    // Reload the counter value, so the individual timer used by the channel
                    // is synchronized with the core timer
                    channelTimer.Value = coreTimer.Value;
                }
                channelTimer.Enabled = shouldEnableTimer;
            }

            public CCChannelMode Mode
            {
                get => mode;
                set
                {
                    if(value == CCChannelMode.InputCapture)
                    {
                        owner.Log(LogLevel.Warning, "Attempt to set mode for Channel #{0} ignored. Input capture is not supported.", id);
                        return;
                    }
                    mode = value;
                    Refresh();
                }
            }

            public CCChannelComparisonBase ComparisonBase
            {
                get => comparisonBase;
                set
                {
                    if(value == CCChannelComparisonBase.PreCounter)
                    {
                        owner.Log(LogLevel.Warning, "Attempt to set comparison base for Channel #{0} ignored. Pre-counter is not supported.", id);
                        return;
                    }
                    comparisonBase = value;
                    Refresh();
                }
            }

            public ulong CompareValue
            {
                get => channelTimer.Limit;
                set
                {
                    // When changing the limit value, the value of the LimitTimer is reset
                    // Save and restore it, so changing the limit while the timer is running
                    // doesn't break the currently set alarm.
                    var oldCounter = channelTimer.Value;
                    channelTimer.Limit = value;
                    channelTimer.Value = oldCounter;
                    Refresh();
                }
            }

            public int Divider
            {
                set
                {
                    channelTimer.Divider = value;
                }
            }

            public ulong Value
            {
                set
                {
                    channelTimer.Value = value;
                    Refresh();
                }
            }

            public event Action CompareReached
            {
                add => channelTimer.LimitReached += value;
                remove => channelTimer.LimitReached -= value;
            }

            private CCChannelMode mode;
            private CCChannelComparisonBase comparisonBase;
            private readonly LimitTimer channelTimer;
            private readonly LimitTimer coreTimer;
            private readonly IPeripheral owner;
            private readonly int id;
        }
    }
}