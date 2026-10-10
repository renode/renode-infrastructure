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
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Analog
{
    // MPC5645S/PXD20 ADC subset. External analog inputs are board state and
    // therefore are configured separately from the peripheral reset state.
    // Supported: software-triggered normal one-shot/scan conversion, raw 10-bit
    // channels, CDR VALID/OVERW lifecycle, EOC/ECH and channel-pending W1C flags.
    // Unsupported: injected/external-trigger conversion, DMA, watchdog threshold
    // evaluation, left-aligned results, and detailed abort/power-transition delays.
    public class MPC5645S_ADC : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize, IHasFrequency
    {
        public MPC5645S_ADC(IMachine machine, ulong conversionFrequency = DefaultConversionFrequency)
        {
            this.machine = machine;
            conversionTimer = new LimitTimer(machine.ClockSource, conversionFrequency, this, nameof(conversionTimer),
                limit: MaximumConversionTicks, direction: Direction.Descending, enabled: false,
                workMode: WorkMode.Periodic, eventEnabled: true, autoUpdate: false);
            conversionTimer.LimitReached += CompleteNormalConversion;
            channelInputs = new ushort[ChannelCount];
            pulseGeneration = new ulong[LogicalChannelCount];
            pulseCompletedGeneration = new ulong[LogicalChannelCount];
            pulseRestoreValue = new ushort[LogicalChannelCount];
            channelData = new ChannelData[ChannelCount];
            EOC = new GPIO();
            Error = new GPIO();
            Watchdog = new GPIO();
            Reset();
        }

        public void Reset()
        {
            lock(sync)
            {
                mainConfiguration = PowerDownMask;
                mainStatus = PowerDownStatus;
                interruptStatus = 0;
                interruptMask = 0;
                channelPending1 = 0;
                channelPending2 = 0;
                channelInterruptMask1 = 0;
                channelInterruptMask2 = 0;
                normalConversionMask1 = 0;
                normalConversionMask2 = 0;
                conversionTiming1 = 0x00000003;
                conversionTiming2 = 0x00000003;
                conversionTimer.Reset();
                conversionTimer.AutoUpdate = false;
                conversionTimer.EventEnabled = true;
                conversionTimer.Limit = 1;
                conversionTimer.Value = 1;
                conversionTimer.Enabled = false;
                activeNormalMask1 = 0;
                activeNormalMask2 = 0;
                scanActive = false;
                lazyScan = false;
                // Invalidate pending pulse restores; inputs themselves are board state.
                for(var i = 0; i < LogicalChannelCount; i++)
                {
                    pulseGeneration[i]++;
                    pulseCompletedGeneration[i] = pulseGeneration[i];
                }
                Array.Clear(channelData, 0, channelData.Length);
                EOC.Unset();
                Error.Unset();
                Watchdog.Unset();
            }
        }

        public void SetChannelValue(int logicalChannel, uint value)
        {
            if(logicalChannel < 0 || logicalChannel >= LogicalChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalChannel));
            }
            lock(sync)
            {
                // Chains completed before this point used the previous input.
                CatchUpLazyScan();
                channelInputs[logicalChannel] = (ushort)(value & DataMask);
            }
        }

        public uint GetChannelValue(int logicalChannel)
        {
            if(logicalChannel < 0 || logicalChannel >= LogicalChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalChannel));
            }
            lock(sync)
            {
                return channelInputs[logicalChannel];
            }
        }

        // Drives `value` on a channel for `durationMicroseconds` of virtual
        // time, then restores the value the channel had before the first of
        // overlapping pulses. A pulse issued while one is active extends it
        // (keyboard auto-repeat) instead of stacking restores.
        public void PulseChannelValue(int logicalChannel, uint value, ulong durationMicroseconds)
        {
            if(logicalChannel < 0 || logicalChannel >= LogicalChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalChannel));
            }
            ulong generation;
            lock(sync)
            {
                CatchUpLazyScan();
                if(pulseGeneration[logicalChannel] == pulseCompletedGeneration[logicalChannel])
                {
                    pulseRestoreValue[logicalChannel] = channelInputs[logicalChannel];
                }
                channelInputs[logicalChannel] = (ushort)(value & DataMask);
                generation = ++pulseGeneration[logicalChannel];
            }
            machine.ScheduleAction(TimeInterval.FromMicroseconds(durationMicroseconds), _ =>
            {
                lock(sync)
                {
                    if(pulseGeneration[logicalChannel] != generation)
                    {
                        return;
                    }
                    CatchUpLazyScan();
                    channelInputs[logicalChannel] = pulseRestoreValue[logicalChannel];
                    pulseCompletedGeneration[logicalChannel] = generation;
                }
            });
        }

        public uint ReadDoubleWord(long offset)
        {
            return ReadSized(offset, 4);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadSized(offset, 2);
        }

        public byte ReadByte(long offset)
        {
            return (byte)ReadSized(offset, 1);
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
            get => conversionTimer.Frequency;
            set
            {
                lock(sync)
                {
                    CatchUpLazyScan();
                    conversionTimer.Frequency = value;
                    if(lazyScan)
                    {
                        nextChainTick = Now() + scanChainTicks;
                    }
                }
            }
        }

        public long Size => 0x4000;

        public GPIO EOC { get; }

        public GPIO Error { get; }

        public GPIO Watchdog { get; }

        private static uint ChannelBit(int channel)
        {
            return 1u << (channel & 31);
        }

        private static int CountBits(uint value)
        {
            var count = 0;
            while(value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        private static bool TryDecodeDataRegister(long offset, out int channel)
        {
            channel = -1;
            if(offset >= DataRegister32Offset && offset <= DataRegister50Offset && (offset & 3) == 0)
            {
                channel = 32 + (int)((offset - DataRegister32Offset) / 4);
                return true;
            }
            if(offset >= DataRegister64Offset && offset <= DataRegister71Offset && (offset & 3) == 0)
            {
                channel = 64 + (int)((offset - DataRegister64Offset) / 4);
                return true;
            }
            return false;
        }

        private static uint BuildDataRegister(ChannelData data)
        {
            return (data.Valid ? ValidMask : 0u) |
                   (data.Overwritten ? OverwriteMask : 0u) |
                   (data.Value & DataMask);
        }

        private static uint Merge(uint previous, uint value, uint mask)
        {
            return (previous & ~mask) | (value & mask);
        }

        private uint ReadSized(long offset, int size)
        {
            var alignedOffset = offset & ~0x3L;
            if(alignedOffset == MainStatusOffset || TryDecodeDataRegister(alignedOffset, out _))
            {
                SynchronizeCurrentCpuTime();
            }
            var lane = (int)(offset & 0x3L);
            if(lane + size > 4)
            {
                uint result = 0;
                for(var i = 0; i < size; i++)
                {
                    result = (result << 8) | ReadSized(offset + i, 1);
                }
                return result;
            }
            var register = ReadRegister(alignedOffset);
            var shift = (4 - lane - size) * 8;
            var mask = size == 4 ? uint.MaxValue : size == 2 ? 0xFFFFu : 0xFFu;
            return (register >> shift) & mask;
        }

        private uint ReadRegister(long offset)
        {
            lock(sync)
            {
                if(lazyScan)
                {
                    SynchronizeCurrentCpuTime();
                    CatchUpLazyScan();
                }
                switch(offset)
                {
                case MainConfigurationOffset:
                    return mainConfiguration;
                case MainStatusOffset:
                    return mainStatus;
                case InterruptStatusOffset:
                    return interruptStatus;
                case ChannelPending1Offset:
                    return channelPending1;
                case ChannelPending2Offset:
                    return channelPending2;
                case InterruptMaskOffset:
                    return interruptMask;
                case ChannelInterruptMask1Offset:
                    return channelInterruptMask1;
                case ChannelInterruptMask2Offset:
                    return channelInterruptMask2;
                case ConversionTiming1Offset:
                    return conversionTiming1;
                case ConversionTiming2Offset:
                    return conversionTiming2;
                case NormalConversionMask1Offset:
                    return normalConversionMask1;
                case NormalConversionMask2Offset:
                    return normalConversionMask2;
                default:
                    if(TryDecodeDataRegister(offset, out var channel))
                    {
                        var result = BuildDataRegister(channelData[channel]);
                        channelData[channel].Valid = false;
                        channelData[channel].Overwritten = false;
                        return result;
                    }
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
            var shiftInRegister = (4 - lane - size) * 8;
            var valueMask = size == 2 ? 0xFFFFu : 0xFFu;
            var mask = valueMask << shiftInRegister;
            WriteRegister(alignedOffset, (value & valueMask) << shiftInRegister, mask);
        }

        private void WriteRegister(long offset, uint value, uint mask)
        {
            lock(sync)
            {
                if(lazyScan)
                {
                    SynchronizeCurrentCpuTime();
                    CatchUpLazyScan();
                }
                WriteRegisterInner(offset, value, mask);
                UpdateScanMode();
            }
        }

        private void WriteRegisterInner(long offset, uint value, uint mask)
        {
            {
                switch(offset)
                {
                case MainConfigurationOffset:
                    var previousConfiguration = mainConfiguration;
                    mainConfiguration = Merge(mainConfiguration, value, mask) & MainConfigurationWritableMask;
                    if((mainConfiguration & PowerDownMask) != 0)
                    {
                        StopConversion(PowerDownStatus);
                    }
                    else
                    {
                        if((previousConfiguration & PowerDownMask) != 0)
                        {
                            mainStatus = IdleStatus;
                        }
                        if((mainConfiguration & NormalStartMask) != 0)
                        {
                            StartNormalConversion();
                        }
                    }
                    break;
                case InterruptStatusOffset:
                    interruptStatus &= ~(value & mask & InterruptStatusMask);
                    UpdateInterrupts();
                    break;
                case ChannelPending1Offset:
                    channelPending1 &= ~(value & mask & InternalChannelMask);
                    UpdateInterrupts();
                    break;
                case ChannelPending2Offset:
                    channelPending2 &= ~(value & mask & ExternalChannelMask);
                    UpdateInterrupts();
                    break;
                case InterruptMaskOffset:
                    interruptMask = Merge(interruptMask, value, mask) & InterruptStatusMask;
                    UpdateInterrupts();
                    break;
                case ChannelInterruptMask1Offset:
                    channelInterruptMask1 = Merge(channelInterruptMask1, value, mask) & InternalChannelMask;
                    UpdateInterrupts();
                    break;
                case ChannelInterruptMask2Offset:
                    channelInterruptMask2 = Merge(channelInterruptMask2, value, mask) & ExternalChannelMask;
                    UpdateInterrupts();
                    break;
                case ConversionTiming1Offset:
                    conversionTiming1 = Merge(conversionTiming1, value, mask);
                    break;
                case ConversionTiming2Offset:
                    conversionTiming2 = Merge(conversionTiming2, value, mask);
                    break;
                case NormalConversionMask1Offset:
                    normalConversionMask1 = Merge(normalConversionMask1, value, mask) & InternalChannelMask;
                    break;
                case NormalConversionMask2Offset:
                    normalConversionMask2 = Merge(normalConversionMask2, value, mask) & ExternalChannelMask;
                    break;
                default:
                    if(TryDecodeDataRegister(offset, out _))
                    {
                        this.Log(LogLevel.Warning, "Writes to ADC channel data registers are not implemented and are ignored");
                    }
                    break;
                }
            }
        }

        private void UpdateInterrupts()
        {
            var globalPending = (interruptStatus & interruptMask & InterruptStatusMask) != 0;
            var channelPending = (channelPending1 & channelInterruptMask1) != 0 ||
                                 (channelPending2 & channelInterruptMask2) != 0;
            EOC.Set(globalPending || channelPending);
            Error.Unset();
            Watchdog.Unset();
        }

        private void StartNormalConversion()
        {
            activeNormalMask1 = normalConversionMask1;
            activeNormalMask2 = normalConversionMask2;
            scanActive = (mainConfiguration & ScanModeMask) != 0;
            if(!scanActive)
            {
                mainConfiguration &= ~NormalStartMask;
            }

            var selectedChannels = CountBits(activeNormalMask1) + CountBits(activeNormalMask2);
            if(selectedChannels == 0)
            {
                interruptStatus |= EndOfChainMask;
                mainStatus = IdleStatus;
                conversionTimer.Enabled = false;
                UpdateInterrupts();
                return;
            }

            mainStatus = NormalStartStatus | ((uint)FirstSelectedChannel() << ChannelAddressShift);
            conversionTimer.Enabled = false;
            lazyScan = false;
            if(scanActive && !InterruptsWanted())
            {
                StartLazyScan((ulong)selectedChannels, (ulong)selectedChannels);
                return;
            }
            conversionTimer.Limit = (ulong)selectedChannels + 1UL;
            conversionTimer.Value = (ulong)selectedChannels;
            conversionTimer.Enabled = true;
        }

        private void CompleteNormalConversion()
        {
            lock(sync)
            {
                CompleteChain();

                if(scanActive && (mainConfiguration & PowerDownMask) == 0)
                {
                    var selectedChannels = CountBits(activeNormalMask1) + CountBits(activeNormalMask2);
                    conversionTimer.Enabled = false;
                    conversionTimer.Limit = (ulong)Math.Max(1, selectedChannels) + 1UL;
                    conversionTimer.Value = (ulong)Math.Max(1, selectedChannels);
                    conversionTimer.Enabled = true;
                    mainStatus = NormalStartStatus | ((uint)FirstSelectedChannel() << ChannelAddressShift);
                    UpdateScanMode();
                }
                else
                {
                    StopConversion(IdleStatus);
                }
            }
        }

        // Applies one completed normal chain to the data/status registers.
        private void CompleteChain()
        {
            {
                for(var channel = 32; channel <= 63; channel++)
                {
                    if((activeNormalMask1 & ChannelBit(channel)) != 0)
                    {
                        CompleteChannel(channel);
                    }
                }
                for(var channel = 64; channel <= 95; channel++)
                {
                    if((activeNormalMask2 & ChannelBit(channel)) != 0)
                    {
                        CompleteChannel(channel);
                    }
                }
                interruptStatus |= EndOfConversionMask | EndOfChainMask;
                UpdateInterrupts();
            }
        }

        // Continuous scan without enabled ADC interrupts has no time-critical
        // side effect, so its chains are applied lazily on register access
        // instead of through a clock entry firing every chain (~11 us). An
        // armed clock entry would also cap Renode's execution quantum.
        private bool InterruptsWanted()
        {
            return (interruptMask & InterruptStatusMask) != 0 || channelInterruptMask1 != 0 || channelInterruptMask2 != 0;
        }

        private void StartLazyScan(ulong chainTicks, ulong ticksToFirstChain)
        {
            scanChainTicks = Math.Max(1UL, chainTicks);
            nextChainTick = Now() + Math.Max(1UL, Math.Min(ticksToFirstChain, scanChainTicks));
            lazyScan = true;
        }

        private void CatchUpLazyScan()
        {
            if(!lazyScan)
            {
                return;
            }
            var now = Now();
            if(now < nextChainTick)
            {
                return;
            }
            var pending = 1 + (now - nextChainTick) / scanChainTicks;
            nextChainTick += pending * scanChainTicks;
            // Inputs are constant between accesses, so chains beyond the
            // second leave the registers unchanged.
            for(ulong i = 0; i < Math.Min(pending, 2UL); i++)
            {
                CompleteChain();
            }
        }

        // Switches a running scan between lazy and timer-driven delivery when
        // the ADC interrupt masks change.
        private void UpdateScanMode()
        {
            if(!scanActive || (mainConfiguration & PowerDownMask) != 0)
            {
                lazyScan = false;
                return;
            }
            if(lazyScan && InterruptsWanted())
            {
                var now = Now();
                var remaining = Math.Max(1UL, nextChainTick - now);
                lazyScan = false;
                conversionTimer.Enabled = false;
                conversionTimer.Limit = scanChainTicks + 1UL;
                conversionTimer.Value = remaining;
                conversionTimer.Enabled = true;
            }
            else if(!lazyScan && !InterruptsWanted() && conversionTimer.Enabled)
            {
                var selectedChannels = (ulong)Math.Max(1, CountBits(activeNormalMask1) + CountBits(activeNormalMask2));
                var remaining = conversionTimer.Value;
                conversionTimer.Enabled = false;
                StartLazyScan(selectedChannels, remaining);
            }
        }

        private ulong Now()
        {
            var elapsed = (UInt128)machine.ClockSource.CurrentValue.Ticks;
            return (ulong)(elapsed * conversionTimer.Frequency / TimeInterval.TicksPerSecond);
        }

        private void CompleteChannel(int channel)
        {
            var data = channelData[channel];
            if(data.Valid && (mainConfiguration & OverwriteEnableMask) == 0)
            {
                SetChannelPending(channel);
                return;
            }
            if(data.Valid)
            {
                data.Overwritten = true;
            }
            var logicalChannel = channel - 32;
            data.Value = logicalChannel >= 0 && logicalChannel < LogicalChannelCount
                ? channelInputs[logicalChannel]
                : (ushort)0;
            data.Valid = true;
            channelData[channel] = data;
            SetChannelPending(channel);
        }

        private void SetChannelPending(int channel)
        {
            if(channel < 64)
            {
                channelPending1 |= ChannelBit(channel);
            }
            else
            {
                channelPending2 |= ChannelBit(channel);
            }
        }

        private void StopConversion(uint status)
        {
            conversionTimer.Enabled = false;
            lazyScan = false;
            scanActive = false;
            mainStatus = status;
        }

        private int FirstSelectedChannel()
        {
            for(var channel = 32; channel <= 63; channel++)
            {
                if((activeNormalMask1 & ChannelBit(channel)) != 0)
                {
                    return channel;
                }
            }
            for(var channel = 64; channel <= 95; channel++)
            {
                if((activeNormalMask2 & ChannelBit(channel)) != 0)
                {
                    return channel;
                }
            }
            return 0;
        }

        private void SynchronizeCurrentCpuTime()
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
        }

        private uint channelInterruptMask2;
        private uint normalConversionMask1;
        private uint normalConversionMask2;
        private uint conversionTiming2;
        private uint activeNormalMask1;
        private bool scanActive;
        private uint channelInterruptMask1;
        private bool lazyScan;
        private ulong nextChainTick;
        private ulong scanChainTicks;
        private uint activeNormalMask2;
        private uint channelPending2;
        private uint conversionTiming1;
        private uint interruptMask;
        private uint interruptStatus;
        private uint mainStatus;
        private uint mainConfiguration;
        private uint channelPending1;

        private readonly object sync = new object();
        private readonly IMachine machine;
        private readonly LimitTimer conversionTimer;
        private readonly ushort[] channelInputs;
        private readonly ulong[] pulseCompletedGeneration;
        private readonly ushort[] pulseRestoreValue;
        private readonly ChannelData[] channelData;
        private readonly ulong[] pulseGeneration;
        private const long ChannelPending2Offset = 0x01C;
        private const uint MainConfigurationWritableMask = 0xE10001E1;

        private const long MainConfigurationOffset = 0x000;
        private const long MainStatusOffset = 0x004;
        private const long InterruptStatusOffset = 0x010;
        private const long ChannelPending1Offset = 0x018;
        private const long InterruptMaskOffset = 0x020;
        private const long DataRegister32Offset = 0x180;
        private const long ChannelInterruptMask2Offset = 0x02C;
        private const long ConversionTiming1Offset = 0x098;
        private const long ConversionTiming2Offset = 0x09C;
        private const long NormalConversionMask1Offset = 0x0A8;
        private const long NormalConversionMask2Offset = 0x0AC;
        private const long DataRegister50Offset = 0x1C8;
        private const uint ExternalChannelMask = 0x000000FF;
        private const long ChannelInterruptMask1Offset = 0x028;
        private const uint InternalChannelMask = 0x0007FFFF;
        private const int ChannelCount = 96;
        private const int ChannelAddressShift = 9;

        private const ulong DefaultConversionFrequency = 1000000;
        private const ulong MaximumConversionTicks = 128;
        private const long DataRegister64Offset = 0x200;
        private const int LogicalChannelCount = 19;
        private const uint DataMask = 0x3FF;
        private const uint ValidMask = 0x00080000;
        private const uint OverwriteMask = 0x00040000;
        private const uint InterruptStatusMask = 0x0000000F;
        private const uint PowerDownMask = 0x00000001;
        private const uint IdleStatus = 0x00000000;
        private const uint NormalStartMask = 0x01000000;
        private const uint NormalStartStatus = 0x01000000;
        private const uint ScanModeMask = 0x20000000;
        private const uint OverwriteEnableMask = 0x80000000;
        private const uint EndOfConversionMask = 0x00000002;
        private const uint EndOfChainMask = 0x00000001;
        private const uint PowerDownStatus = 0x00000001;
        private const long DataRegister71Offset = 0x21C;

        private struct ChannelData
        {
            public ushort Value;
            public bool Valid;
            public bool Overwritten;
        }
    }
}
