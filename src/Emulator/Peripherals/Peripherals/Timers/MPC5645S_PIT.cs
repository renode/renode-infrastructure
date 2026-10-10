//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    // MPC56xx PIT subset used by the MPC5645S boot path.
    //
    // Counters are computed lazily from the machine clock source: a running
    // channel is described by the absolute timer tick of its next expiry, and
    // CVAL/TIF are derived on access. Only channels with TIE set keep a
    // one-shot clock entry armed at their next expiry. This matters because
    // every enabled clock entry limits how far Renode may advance virtual time
    // in one step: firmware that runs a channel with LDVAL=0 and TIE=0 would
    // otherwise force a 1-tick (25 ns at 40 MHz) execution quantum.
    //
    // Time is provided by Renode's clock source; this is not a cycle-accurate bus model.
    public class MPC5645S_PIT : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize, IHasFrequency
    {
        public MPC5645S_PIT(IMachine machine, ulong frequency = DefaultFrequency)
        {
            this.machine = machine;
            this.frequency = frequency;
            channels = new Channel[ChannelCount];
            irqs = new GPIO[ChannelCount];

            for(var i = 0; i < ChannelCount; i++)
            {
                var channel = new Channel();
                channel.Alarm = new LimitTimer(machine.ClockSource, frequency, this, $"Channel{i}",
                    limit: MaximumAlarmTicks, direction: Direction.Descending, enabled: false,
                    workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: false);
                var channelIndex = i;
                channel.Alarm.LimitReached += () => OnAlarm(channelIndex);
                channels[i] = channel;
                irqs[i] = new GPIO();
            }

            Reset();
        }

        public void Reset()
        {
            lock(sync)
            {
                moduleControl = ModuleControlResetValue;
                for(var i = 0; i < ChannelCount; i++)
                {
                    var channel = channels[i];
                    channel.Alarm.Reset();
                    channel.Alarm.EventEnabled = true;
                    channel.Alarm.Enabled = false;
                    channel.AlarmArmed = false;
                    channel.ArmedExpiry = 0;
                    channel.LoadValue = 0;
                    channel.Control = 0;
                    channel.InterruptFlag = false;
                    channel.HasLoadedValue = false;
                    channel.Running = false;
                    channel.NextExpiry = 0;
                    channel.FrozenRemaining = 1;
                    irqs[i].Unset();
                }
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadRegister(offset & ~0x3L);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)((ReadByte(offset) << 8) | ReadByte(offset + 1));
        }

        public byte ReadByte(long offset)
        {
            var alignedOffset = offset & ~0x3L;
            var shift = (int)((3 - (offset & 0x3L)) * 8);
            return (byte)(ReadRegister(alignedOffset) >> shift);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            WriteSized(offset, 4, value);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteSized(offset, 2, value);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteSized(offset, 1, value);
        }

        public ulong Frequency
        {
            get => frequency;
            set
            {
                lock(sync)
                {
                    // Preserve each channel's remaining ticks across the change.
                    var now = Now();
                    for(var i = 0; i < ChannelCount; i++)
                    {
                        CatchUp(i, now);
                        if(channels[i].Running)
                        {
                            channels[i].FrozenRemaining = channels[i].NextExpiry - now;
                        }
                    }
                    frequency = value;
                    now = Now();
                    for(var i = 0; i < ChannelCount; i++)
                    {
                        channels[i].Alarm.Frequency = value;
                        channels[i].Alarm.Enabled = false;
                        channels[i].AlarmArmed = false;
                        if(channels[i].Running)
                        {
                            channels[i].NextExpiry = now + channels[i].FrozenRemaining;
                        }
                        UpdateAlarm(i, now);
                    }
                }
            }
        }

        public long Size => 0x4000;

        public GPIO IRQ0 => irqs[0];

        public GPIO IRQ1 => irqs[1];

        public GPIO IRQ2 => irqs[2];

        public GPIO IRQ3 => irqs[3];

        private static ulong Period(Channel channel)
        {
            return (ulong)channel.LoadValue + 1UL;
        }

        private static uint Merge(uint previous, uint value, uint mask)
        {
            return (previous & ~mask) | (value & mask);
        }

        private uint ReadRegister(long offset)
        {
            lock(sync)
            {
                if(offset == ModuleControlOffset)
                {
                    return moduleControl;
                }

                if(!TryDecodeChannel(offset, out var channelIndex, out var registerOffset))
                {
                    return 0;
                }

                var channel = channels[channelIndex];
                switch(registerOffset)
                {
                case LoadValueOffset:
                    return channel.LoadValue;
                case CurrentValueOffset:
                {
                    if(!channel.HasLoadedValue)
                    {
                        return 0;
                    }
                    SynchronizeCurrentCpuTime();
                    var now = Now();
                    CatchUp(channelIndex, now);
                    var remaining = channel.Running ? channel.NextExpiry - now : channel.FrozenRemaining;
                    return remaining == 0 ? 0 : (uint)(remaining - 1);
                }
                case ControlOffset:
                    return channel.Control;
                case FlagOffset:
                {
                    if(channel.Running && !channel.InterruptFlag)
                    {
                        SynchronizeCurrentCpuTime();
                        CatchUp(channelIndex, Now());
                    }
                    return channel.InterruptFlag ? 1u : 0u;
                }
                default:
                    return 0;
                }
            }
        }

        private void WriteSized(long offset, int size, uint value)
        {
            var lane = (int)(offset & 0x3L);
            if(size == 4 && lane == 0)
            {
                WriteRegister(offset, value, uint.MaxValue);
                return;
            }
            if(size != 1 && size != 2)
            {
                return;
            }
            if(lane + size > 4)
            {
                for(var i = 0; i < size; i++)
                {
                    var shift = (size - 1 - i) * 8;
                    WriteSized(offset + i, 1, (value >> shift) & 0xFFu);
                }
                return;
            }

            var alignedOffset = offset & ~0x3L;
            var bitShift = (4 - lane - size) * 8;
            var valueMask = size == 1 ? 0xFFu : 0xFFFFu;
            var mask = valueMask << bitShift;
            WriteRegister(alignedOffset, (value & valueMask) << bitShift, mask);
        }

        private void WriteRegister(long offset, uint value, uint mask)
        {
            lock(sync)
            {
                // Every write first applies expiries up to the current
                // instruction: a TFLG clear or LDVAL change must not be
                // ordered before an expiry that already happened.
                SynchronizeCurrentCpuTime();
                var now = Now();

                if(offset == ModuleControlOffset)
                {
                    var previous = moduleControl;
                    for(var i = 0; i < ChannelCount; i++)
                    {
                        CatchUp(i, now);
                    }
                    moduleControl = Merge(previous, value, mask) & ModuleControlWritableMask;
                    if(((previous ^ moduleControl) & ModuleDisableMask) != 0)
                    {
                        UpdateRunStates(now);
                    }
                    return;
                }

                if(!TryDecodeChannel(offset, out var channelIndex, out var registerOffset))
                {
                    return;
                }

                var channel = channels[channelIndex];
                // Expiries before this write used the previous LDVAL/TCTRL.
                CatchUp(channelIndex, now);
                switch(registerOffset)
                {
                case LoadValueOffset:
                    // Takes effect at the next expiry; no restart.
                    channel.LoadValue = Merge(channel.LoadValue, value, mask);
                    break;
                case CurrentValueOffset:
                    // CVAL is read-only.
                    break;
                case ControlOffset:
                    var previousControl = channel.Control;
                    var mergedControl = Merge(previousControl, value, mask);
                    if((mergedControl & ChainModeMask) != 0)
                    {
                        this.Log(LogLevel.Warning, "PIT channel {0}: chain mode is not implemented; CHN is ignored", channelIndex);
                    }
                    channel.Control = mergedControl & ControlWritableMask;
                    var wasEnabled = (previousControl & TimerEnableMask) != 0;
                    var isEnabled = (channel.Control & TimerEnableMask) != 0;
                    if(!wasEnabled && isEnabled)
                    {
                        // Enabling loads LDVAL.
                        channel.FrozenRemaining = Period(channel);
                        channel.HasLoadedValue = true;
                    }
                    UpdateRunState(channelIndex, now);
                    UpdateInterrupt(channelIndex);
                    break;
                case FlagOffset:
                    if((value & mask & InterruptFlagMask) != 0)
                    {
                        channel.InterruptFlag = false;
                        UpdateInterrupt(channelIndex);
                    }
                    break;
                }
                UpdateAlarm(channelIndex, now);
            }
        }

        private void OnAlarm(int channelIndex)
        {
            lock(sync)
            {
                var channel = channels[channelIndex];
                var armedFor = channel.ArmedExpiry;
                channel.AlarmArmed = false;
                if(!channel.Running)
                {
                    return;
                }
                // An alarm armed for the current expiry fires exactly at it;
                // guard against the clock-source value being rounded down by
                // one tick. An early (stale) alarm must not advance time.
                var now = Now();
                if(armedFor >= channel.NextExpiry)
                {
                    now = Math.Max(now, channel.NextExpiry);
                }
                CatchUp(channelIndex, now);
                UpdateAlarm(channelIndex, now);
            }
        }

        // Applies all expiries up to and including `now`.
        private void CatchUp(int channelIndex, ulong now)
        {
            var channel = channels[channelIndex];
            if(!channel.Running || now < channel.NextExpiry)
            {
                return;
            }
            // Expiries after the one at NextExpiry reload the current LDVAL.
            var period = Period(channel);
            var extra = (now - channel.NextExpiry) / period;
            channel.NextExpiry += (extra + 1) * period;
            if(!channel.InterruptFlag)
            {
                channel.InterruptFlag = true;
                UpdateInterrupt(channelIndex);
            }
        }

        private void UpdateRunStates(ulong now)
        {
            for(var i = 0; i < ChannelCount; i++)
            {
                UpdateRunState(i, now);
                UpdateInterrupt(i);
                UpdateAlarm(i, now);
            }
        }

        // Starts or freezes the counter according to TEN and MDIS.
        private void UpdateRunState(int channelIndex, ulong now)
        {
            var channel = channels[channelIndex];
            var shouldRun = (channel.Control & TimerEnableMask) != 0 && !ModuleDisabled;
            if(shouldRun == channel.Running)
            {
                return;
            }
            if(shouldRun)
            {
                channel.NextExpiry = now + Math.Max(1UL, channel.FrozenRemaining);
            }
            else
            {
                channel.FrozenRemaining = channel.NextExpiry - now;
            }
            channel.Running = shouldRun;
        }

        // Keeps a clock entry only where an interrupt must be delivered on time.
        //
        // An armed alarm is left in place when it is no longer needed or fires
        // no later than required: an early or stale alarm only re-evaluates the
        // channel in OnAlarm. This avoids reprogramming the clock source on every
        // TCTRL/LDVAL write (some firmware restarts a channel thousands of times
        // per second).
        private void UpdateAlarm(int channelIndex, ulong now)
        {
            var channel = channels[channelIndex];
            var needed = channel.Running && (channel.Control & InterruptEnableMask) != 0 && !channel.InterruptFlag;
            if(!needed)
            {
                return;
            }
            var target = Math.Max(now + 1, channel.NextExpiry);
            if(channel.AlarmArmed && channel.ArmedExpiry <= target && channel.ArmedExpiry > now)
            {
                return;
            }
            var ticks = target - now;
            var alarm = channel.Alarm;
            alarm.Enabled = false;
            alarm.Limit = ticks;
            alarm.Value = ticks;
            alarm.Enabled = true;
            channel.AlarmArmed = true;
            channel.ArmedExpiry = target;
        }

        private void UpdateInterrupt(int channelIndex)
        {
            var channel = channels[channelIndex];
            irqs[channelIndex].Set(channel.InterruptFlag && (channel.Control & InterruptEnableMask) != 0 && !ModuleDisabled);
        }

        // Current time in timer ticks since the start of emulation.
        private ulong Now()
        {
            var elapsed = (UInt128)machine.ClockSource.CurrentValue.Ticks;
            return (ulong)(elapsed * frequency / TimeInterval.TicksPerSecond);
        }

        private void SynchronizeCurrentCpuTime()
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
        }

        private bool TryDecodeChannel(long offset, out int channelIndex, out long registerOffset)
        {
            channelIndex = -1;
            registerOffset = -1;
            if(offset < ChannelBaseOffset)
            {
                return false;
            }

            channelIndex = (int)((offset - ChannelBaseOffset) / ChannelStride);
            registerOffset = (offset - ChannelBaseOffset) % ChannelStride;
            return channelIndex >= 0 && channelIndex < ChannelCount;
        }

        private bool ModuleDisabled => (moduleControl & ModuleDisableMask) != 0;

        private ulong frequency;
        private uint moduleControl;

        private readonly object sync = new object();
        private readonly IMachine machine;
        private readonly Channel[] channels;
        private readonly GPIO[] irqs;
        private const uint ChainModeMask = 1u << 2;
        private const uint InterruptEnableMask = 1u << 1;
        private const uint TimerEnableMask = 1u << 0;
        private const uint ModuleDisableMask = 1u << 1;
        private const uint ModuleControlWritableMask = 0x7;

        private const uint ModuleControlResetValue = 0x6;
        private const long FlagOffset = 0xC;
        private const long ControlOffset = 0x8;
        private const long ChannelBaseOffset = 0x100;
        private const long LoadValueOffset = 0x0;
        private const long ChannelStride = 0x10;
        private const uint ControlWritableMask = TimerEnableMask | InterruptEnableMask;

        private const long ModuleControlOffset = 0x0;
        // LimitTimer rejects Value above its constructor limit; a period is at most 2^32 ticks.
        private const ulong MaximumAlarmTicks = 1UL << 33;
        private const ulong DefaultFrequency = 40000000;

        private const int ChannelCount = 4;
        private const long CurrentValueOffset = 0x4;
        private const uint InterruptFlagMask = 1u << 0;

        private sealed class Channel
        {
            public LimitTimer Alarm;
            public uint LoadValue;
            public uint Control;
            public bool InterruptFlag;
            public bool HasLoadedValue;
            public bool Running;
            // Absolute timer tick of the next expiry while running.
            public ulong NextExpiry;
            // Ticks left until expiry while stopped (TEN=0 or MDIS=1).
            public ulong FrozenRemaining;
            // One-shot alarm state; the alarm may be early or stale (see UpdateAlarm).
            public bool AlarmArmed;
            public ulong ArmedExpiry;
        }
    }
}
