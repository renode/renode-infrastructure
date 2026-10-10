//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.DMA
{
    // MPC5645S/PXD20 eDMA subset.
    //
    // The model implements the classic 16-channel, big-endian TCD layout at
    // 0x1000 + channel*0x20, command registers, always-request DMAMUX sources,
    // memory-to-memory minor loops, delayed completion, per-channel interrupts,
    // error flags, DREQ, W1C acknowledgement, and reset cancellation.
    // Unsupported linking, scatter/gather, modulo and peripheral-paced requests
    // fail explicitly instead of completing without memory effects.
    public sealed class MPC5645S_eDMA : IDoubleWordPeripheral, IWordPeripheral,
        IBytePeripheral, IKnownSize, IHasFrequency, INumberedGPIOOutput
    {
        public MPC5645S_eDMA(IMachine machine, ulong transferFrequency = DefaultTransferFrequency)
        {
            if(transferFrequency == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(transferFrequency));
            }

            this.machine = machine;
            systemBus = machine.GetSystemBus(this);
            registers = new byte[SizeValue];
            channels = new ChannelState[ChannelCount];
            connections = new Dictionary<int, IGPIO>();
            muxConfiguration = new byte[ChannelCount];
            channelCompletionCounts = new ulong[ChannelCount];

            for(var channel = 0; channel < ChannelCount; channel++)
            {
                var capturedChannel = channel;
                var timer = new LimitTimer(machine.ClockSource, transferFrequency, this,
                    $"Channel{channel}", limit: MaximumTimerTicks, direction: Direction.Descending,
                    enabled: false, workMode: WorkMode.OneShot, eventEnabled: true, autoUpdate: false);
                timer.LimitReached += () => CompleteTransfer(capturedChannel);
                channels[channel] = new ChannelState { Timer = timer };
                connections[channel] = new GPIO();
            }
            Error = new GPIO();
            Reset();
        }

        public bool IsChannelActive(int channel)
        {
            return IsValidChannel(channel) && channels[channel].Active;
        }

        public bool IsInterruptPending(int channel)
        {
            return IsValidChannel(channel) && (ReadRawDoubleWord(InterruptRequestOffset) & (1u << channel)) != 0;
        }

        public ulong GetChannelCompletionCount(int channel)
        {
            return IsValidChannel(channel) ? channelCompletionCounts[channel] : 0;
        }

        public uint GetMuxConfiguration(int channel)
        {
            return IsValidChannel(channel) ? muxConfiguration[channel] : 0u;
        }

        public void SetMuxConfiguration(int channel, byte value)
        {
            if(!IsValidChannel(channel))
            {
                return;
            }
            lock(sync)
            {
                muxConfiguration[channel] = value;
                EvaluateChannel(channel);
            }
        }

        public void Reset()
        {
            lock(sync)
            {
                Array.Clear(registers, 0, registers.Length);
                Array.Clear(muxConfiguration, 0, muxConfiguration.Length);
                Array.Clear(channelCompletionCounts, 0, channelCompletionCounts.Length);
                foreach(var channel in channels)
                {
                    channel.Timer.Reset();
                    channel.Timer.AutoUpdate = false;
                    channel.Timer.EventEnabled = true;
                    channel.Timer.Limit = 1;
                    channel.Timer.Value = 1;
                    channel.Timer.Enabled = false;
                    channel.Pending = default(PendingTransfer);
                    channel.Active = false;
                }
                foreach(var irq in connections.Values)
                {
                    irq.Unset();
                }
                Error.Unset();
                CompletionCount = 0;
                ErrorCount = 0;
                LastCompletedChannel = uint.MaxValue;
                LastSourceAddress = 0;
                LastDestinationAddress = 0;
                LastTransferBytes = 0;
                LastTransferWriteCount = 0;
                LastErrorReasonCode = 0;
            }
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
            get => channels[0].Timer.Frequency;
            set
            {
                if(value == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                lock(sync)
                {
                    foreach(var channel in channels)
                    {
                        channel.Timer.Frequency = value;
                    }
                }
            }
        }

        public long Size => SizeValue;

        public IReadOnlyDictionary<int, IGPIO> Connections => connections;

        public GPIO Error { get; }

        public ulong CompletionCount { get; private set; }

        public ulong ErrorCount { get; private set; }

        public uint LastCompletedChannel { get; private set; }

        public uint LastSourceAddress { get; private set; }

        public uint LastDestinationAddress { get; private set; }

        public uint LastTransferBytes { get; private set; }

        public ulong LastTransferWriteCount { get; private set; }

        public uint LastErrorReasonCode { get; private set; }

        private static bool IsAlwaysRequest(byte configuration)
        {
            var enabled = (configuration & DmaMuxEnableMask) != 0;
            var triggered = (configuration & DmaMuxTriggerMask) != 0;
            var source = configuration & DmaMuxSourceMask;
            return enabled && !triggered && source >= FirstAlwaysRequestSource;
        }

        private static int SignExtendMinorOffset(uint value)
        {
            return (int)(value << 2) >> 12;
        }

        private static uint AddSigned(uint value, int adjustment)
        {
            return unchecked(value + (uint)adjustment);
        }

        private static bool IsValidChannel(int channel)
        {
            return channel >= 0 && channel < ChannelCount;
        }

        private static bool IsValidRange(long offset, int size)
        {
            return (size == 1 || size == 2 || size == 4)
                && offset >= 0 && offset <= SizeValue - size;
        }

        private static bool Overlaps(long offset, int size, long registerOffset, int registerSize)
        {
            return offset < registerOffset + registerSize && registerOffset < offset + size;
        }

        private static long TcdOffset(int channel)
        {
            return TcdBaseOffset + channel * TcdStride;
        }

        private static void ArmTimer(LimitTimer timer, ulong ticks)
        {
            timer.Enabled = false;
            timer.Limit = ticks;
            timer.Value = ticks;
            timer.Enabled = true;
        }

        private uint ReadSized(long offset, int size)
        {
            if(!IsValidRange(offset, size))
            {
                this.Log(LogLevel.Warning, "Unsupported eDMA read offset/size: 0x{0:X}, {1}", offset, size);
                return 0;
            }
            SynchronizeCurrentCpuTime();
            lock(sync)
            {
                uint result = 0;
                for(var index = 0; index < size; index++)
                {
                    var byteOffset = offset + index;
                    var value = byteOffset >= CommandBaseOffset && byteOffset < CommandEndOffset
                        ? (byte)0 : registers[byteOffset];
                    result = (result << 8) | value;
                }
                return result;
            }
        }

        private void WriteSized(long offset, int size, uint value)
        {
            if(!IsValidRange(offset, size))
            {
                this.Log(LogLevel.Warning, "Unsupported eDMA write offset/size: 0x{0:X}, {1}", offset, size);
                return;
            }
            lock(sync)
            {
                for(var index = 0; index < size; index++)
                {
                    var byteOffset = offset + index;
                    var byteValue = (byte)(value >> ((size - 1 - index) * 8));
                    if(byteOffset >= CommandBaseOffset && byteOffset < CommandEndOffset)
                    {
                        ExecuteCommand(byteOffset, byteValue);
                        continue;
                    }
                    WriteByteInternal(byteOffset, byteValue);
                }
                ApplyWriteSideEffects(offset, size);
            }
        }

        private void WriteByteInternal(long offset, byte value)
        {
            var alignedOffset = offset & ~0x3L;
            if(alignedOffset == InterruptRequestOffset || alignedOffset == ErrorRequestOffset)
            {
                registers[offset] &= (byte)~value;
                UpdateInterrupts();
                return;
            }
            registers[offset] = value;
        }

        private void ApplyWriteSideEffects(long offset, int size)
        {
            if(Overlaps(offset, size, ControlOffset, 4))
            {
                EvaluateAllChannels();
            }
            if(Overlaps(offset, size, EnableRequestOffset, 4))
            {
                EvaluateAllChannels();
            }
            if(Overlaps(offset, size, EnableErrorInterruptOffset, 4))
            {
                UpdateInterrupts();
            }
            for(var channel = 0; channel < ChannelCount; channel++)
            {
                var csrOffset = TcdOffset(channel) + TcdControlStatusOffset;
                if(Overlaps(offset, size, csrOffset, 2) && (ReadRawWord(csrOffset) & ChannelStartMask) != 0)
                {
                    EvaluateChannel(channel);
                }
            }
        }

        private void ExecuteCommand(long offset, byte value)
        {
            if((value & CommandNoOperationMask) != 0)
            {
                return;
            }
            for(var channel = 0; channel < ChannelCount; channel++)
            {
                if((value & CommandAllChannelsMask) == 0 && channel != (value & CommandChannelMask))
                {
                    continue;
                }
                var bit = 1u << channel;
                switch(offset)
                {
                case SetEnableRequestOffset:
                    WriteRawDoubleWord(EnableRequestOffset, ReadRawDoubleWord(EnableRequestOffset) | bit);
                    EvaluateChannel(channel);
                    break;
                case ClearEnableRequestOffset:
                    WriteRawDoubleWord(EnableRequestOffset, ReadRawDoubleWord(EnableRequestOffset) & ~bit);
                    break;
                case SetEnableErrorInterruptOffset:
                    WriteRawDoubleWord(EnableErrorInterruptOffset, ReadRawDoubleWord(EnableErrorInterruptOffset) | bit);
                    UpdateInterrupts();
                    break;
                case ClearEnableErrorInterruptOffset:
                    WriteRawDoubleWord(EnableErrorInterruptOffset, ReadRawDoubleWord(EnableErrorInterruptOffset) & ~bit);
                    UpdateInterrupts();
                    break;
                case ClearInterruptRequestOffset:
                    WriteRawDoubleWord(InterruptRequestOffset, ReadRawDoubleWord(InterruptRequestOffset) & ~bit);
                    UpdateInterrupts();
                    break;
                case ClearErrorRequestOffset:
                    WriteRawDoubleWord(ErrorRequestOffset, ReadRawDoubleWord(ErrorRequestOffset) & ~bit);
                    UpdateInterrupts();
                    break;
                case SetStartOffset:
                    WriteRawWord(TcdOffset(channel) + TcdControlStatusOffset,
                        (ushort)(ReadRawWord(TcdOffset(channel) + TcdControlStatusOffset) | ChannelStartMask));
                    EvaluateChannel(channel);
                    break;
                case ClearDoneOffset:
                    WriteRawWord(TcdOffset(channel) + TcdControlStatusOffset,
                        (ushort)(ReadRawWord(TcdOffset(channel) + TcdControlStatusOffset) & ~ChannelDoneMask));
                    break;
                }
            }
        }

        private void EvaluateAllChannels()
        {
            for(var channel = 0; channel < ChannelCount; channel++)
            {
                EvaluateChannel(channel);
            }
        }

        private void EvaluateChannel(int channel)
        {
            if(channels[channel].Active || (ReadRawDoubleWord(ControlOffset) & HaltMask) != 0)
            {
                return;
            }
            var csrOffset = TcdOffset(channel) + TcdControlStatusOffset;
            var csr = ReadRawWord(csrOffset);
            var softwareRequested = (csr & ChannelStartMask) != 0;
            var hardwareRequested = (ReadRawDoubleWord(EnableRequestOffset) & (1u << channel)) != 0
                && IsAlwaysRequest(muxConfiguration[channel]);
            if(!softwareRequested && !hardwareRequested)
            {
                return;
            }

            if(!TryCaptureTransfer(channel, out var pending, out var errorCode))
            {
                csr = (ushort)(csr & ~(ChannelStartMask | ChannelActiveMask));
                WriteRawWord(csrOffset, csr);
                ReportError(channel, errorCode);
                return;
            }

            channels[channel].Pending = pending;
            channels[channel].Active = true;
            csr = (ushort)((csr & ~(ChannelDoneMask | ChannelStartMask)) | ChannelActiveMask);
            WriteRawWord(csrOffset, csr);
            var ticks = Math.Max(1UL, ((ulong)pending.NBytes + pending.BurstSize - 1) / pending.BurstSize + FixedLatencyTicks);
            ArmTimer(channels[channel].Timer, ticks);
        }

        private bool TryCaptureTransfer(int channel, out PendingTransfer pending, out uint errorCode)
        {
            pending = default(PendingTransfer);
            errorCode = ErrorInvalidGeometry;
            var tcd = TcdOffset(channel);
            var attr = ReadRawWord(tcd + TcdAttributesOffset);
            var sourceSizeCode = (attr >> 8) & 0x7;
            var destinationSizeCode = attr & 0x7;
            var sourceSize = 1u << sourceSizeCode;
            var destinationSize = 1u << destinationSizeCode;
            var csr = ReadRawWord(tcd + TcdControlStatusOffset);
            var citerRaw = ReadRawWord(tcd + TcdCurrentIterationOffset);
            var biterRaw = ReadRawWord(tcd + TcdBeginningIterationOffset);
            var nbytesRaw = ReadRawDoubleWord(tcd + TcdNBytesOffset);
            var nbytes = DecodeMinorByteCount(nbytesRaw);

            if((attr & UnsupportedModuloMask) != 0 || sourceSizeCode > MaximumTransferSizeCode
                || destinationSizeCode > MaximumTransferSizeCode || nbytes == 0 || nbytes > MaximumMinorByteCount
                || (nbytes % sourceSize) != 0 || (nbytes % destinationSize) != 0
                || (citerRaw & IterationLinkMask) != 0 || (biterRaw & IterationLinkMask) != 0
                || (citerRaw & IterationCountMask) == 0 || (biterRaw & IterationCountMask) == 0)
            {
                return false;
            }
            if((csr & (EnableScatterGatherMask | EnableMajorLinkMask)) != 0)
            {
                errorCode = ErrorUnsupportedConfiguration;
                return false;
            }

            pending = new PendingTransfer
            {
                SourceAddress = ReadRawDoubleWord(tcd + TcdSourceAddressOffset),
                DestinationAddress = ReadRawDoubleWord(tcd + TcdDestinationAddressOffset),
                SourceOffset = (short)ReadRawWord(tcd + TcdSourceOffsetOffset),
                DestinationOffset = (short)ReadRawWord(tcd + TcdDestinationOffsetOffset),
                SourceSize = sourceSize,
                DestinationSize = destinationSize,
                BurstSize = Math.Max(sourceSize, destinationSize),
                NBytes = nbytes,
                NBytesRaw = nbytesRaw,
                CurrentIteration = (ushort)(citerRaw & IterationCountMask),
                BeginningIteration = (ushort)(biterRaw & IterationCountMask),
                SourceLastAdjustment = unchecked((int)ReadRawDoubleWord(tcd + TcdSourceLastAdjustmentOffset)),
                DestinationLastAdjustment = unchecked((int)ReadRawDoubleWord(tcd + TcdDestinationLastAdjustmentOffset)),
                ControlStatus = csr,
            };

            if((pending.SourceAddress % sourceSize) != 0 || (pending.DestinationAddress % destinationSize) != 0
                || (unchecked((uint)pending.SourceOffset) % sourceSize) != 0
                || (unchecked((uint)pending.DestinationOffset) % destinationSize) != 0)
            {
                return false;
            }

            if(!ValidateMappedMemoryAccesses(pending))
            {
                errorCode = ErrorUnmappedOrPeripheralAddress;
                return false;
            }
            return true;
        }

        private bool ValidateMappedMemoryAccesses(PendingTransfer transfer)
        {
            var source = transfer.SourceAddress;
            var destination = transfer.DestinationAddress;
            for(uint position = 0; position < transfer.NBytes; position += transfer.BurstSize)
            {
                for(uint index = 0; index < transfer.BurstSize; index += transfer.SourceSize)
                {
                    if(!IsMappedMemoryRange(source, transfer.SourceSize))
                    {
                        return false;
                    }
                    source = AddSigned(source, transfer.SourceOffset);
                }
                for(uint index = 0; index < transfer.BurstSize; index += transfer.DestinationSize)
                {
                    if(!IsMappedMemoryRange(destination, transfer.DestinationSize))
                    {
                        return false;
                    }
                    destination = AddSigned(destination, transfer.DestinationOffset);
                }
            }
            return true;
        }

        private bool IsMappedMemoryRange(uint address, uint size)
        {
            for(uint offset = 0; offset < size; offset++)
            {
                var registration = systemBus.WhatIsAt(unchecked(address + offset), this);
                if(registration == null || !(registration.Peripheral is MappedMemory))
                {
                    return false;
                }
            }
            return true;
        }

        private void CompleteTransfer(int channel)
        {
            lock(sync)
            {
                var state = channels[channel];
                if(!state.Active)
                {
                    return;
                }
                var transfer = state.Pending;
                var source = transfer.SourceAddress;
                var destination = transfer.DestinationAddress;
                ulong writeCount = 0;

                if(TryBulkCopy(transfer))
                {
                    source = unchecked(source + transfer.NBytes);
                    destination = unchecked(destination + transfer.NBytes);
                    writeCount = transfer.NBytes;
                }
                else
                {
                    var buffer = new byte[transfer.BurstSize];
                    for(uint position = 0; position < transfer.NBytes; position += transfer.BurstSize)
                    {
                        for(uint index = 0; index < transfer.BurstSize; index += transfer.SourceSize)
                        {
                            for(uint byteIndex = 0; byteIndex < transfer.SourceSize; byteIndex++)
                            {
                                buffer[index + byteIndex] = systemBus.ReadByte(unchecked(source + byteIndex), this);
                            }
                            source = AddSigned(source, transfer.SourceOffset);
                        }
                        for(uint index = 0; index < transfer.BurstSize; index += transfer.DestinationSize)
                        {
                            for(uint byteIndex = 0; byteIndex < transfer.DestinationSize; byteIndex++)
                            {
                                systemBus.WriteByte(unchecked(destination + byteIndex), buffer[index + byteIndex], this);
                                writeCount++;
                            }
                            destination = AddSigned(destination, transfer.DestinationOffset);
                        }
                    }
                }

                if((ReadRawDoubleWord(ControlOffset) & EnableMinorLoopMappingMask) != 0
                    && (transfer.NBytesRaw & MinorLoopOffsetEnableMask) != 0)
                {
                    var minorOffset = SignExtendMinorOffset(transfer.NBytesRaw);
                    if((transfer.NBytesRaw & SourceMinorLoopOffsetEnableMask) != 0)
                    {
                        source = AddSigned(source, minorOffset);
                    }
                    if((transfer.NBytesRaw & DestinationMinorLoopOffsetEnableMask) != 0)
                    {
                        destination = AddSigned(destination, minorOffset);
                    }
                }

                var remaining = (ushort)(transfer.CurrentIteration - 1);
                var tcd = TcdOffset(channel);
                var csr = (ushort)(transfer.ControlStatus & ~(ChannelActiveMask | ChannelStartMask));
                if((csr & InterruptHalfMask) != 0 && remaining == transfer.BeginningIteration / 2)
                {
                    SetInterruptPending(channel);
                }
                if(remaining == 0)
                {
                    source = AddSigned(source, transfer.SourceLastAdjustment);
                    destination = AddSigned(destination, transfer.DestinationLastAdjustment);
                    remaining = transfer.BeginningIteration;
                    csr |= ChannelDoneMask;
                    if((csr & DisableRequestMask) != 0)
                    {
                        WriteRawDoubleWord(EnableRequestOffset,
                            ReadRawDoubleWord(EnableRequestOffset) & ~(1u << channel));
                    }
                    if((csr & InterruptMajorMask) != 0)
                    {
                        SetInterruptPending(channel);
                    }
                }

                WriteRawDoubleWord(tcd + TcdSourceAddressOffset, source);
                WriteRawDoubleWord(tcd + TcdDestinationAddressOffset, destination);
                WriteRawWord(tcd + TcdCurrentIterationOffset, remaining);
                WriteRawWord(tcd + TcdControlStatusOffset, csr);
                state.Active = false;
                state.Pending = default(PendingTransfer);
                state.Timer.Enabled = false;
                CompletionCount++;
                channelCompletionCounts[channel]++;
                LastCompletedChannel = (uint)channel;
                LastSourceAddress = transfer.SourceAddress;
                LastDestinationAddress = transfer.DestinationAddress;
                LastTransferBytes = transfer.NBytes;
                LastTransferWriteCount = writeCount;
                UpdateInterrupts();
                EvaluateChannel(channel);
            }
        }

        // Copies one minor loop with a single bus read and write when both
        // sides are contiguous (offset == access size), lie inside one
        // MappedMemory each and do not overlap. The result is identical to the
        // byte-wise loop but avoids per-byte bus dispatch and per-byte
        // translation-cache invalidation.
        private bool TryBulkCopy(PendingTransfer transfer)
        {
            var count = transfer.NBytes;
            if(count == 0
                || transfer.SourceOffset != (int)transfer.SourceSize
                || transfer.DestinationOffset != (int)transfer.DestinationSize)
            {
                return false;
            }
            ulong source = transfer.SourceAddress;
            ulong destination = transfer.DestinationAddress;
            if(source + count > 0x100000000UL || destination + count > 0x100000000UL)
            {
                return false;
            }
            if(source < destination + count && destination < source + count)
            {
                return false;
            }
            if(!IsSingleMemory(source, count) || !IsSingleMemory(destination, count))
            {
                return false;
            }
            var data = systemBus.ReadBytes(source, (int)count, onlyMemory: true, context: this);
            systemBus.WriteBytes(data, destination, onlyMemory: true, context: this);
            return true;
        }

        private bool IsSingleMemory(ulong address, uint count)
        {
            var first = systemBus.WhatPeripheralIsAt(address, this) as MappedMemory;
            return first != null && ReferenceEquals(first, systemBus.WhatPeripheralIsAt(address + count - 1, this));
        }

        private void SetInterruptPending(int channel)
        {
            WriteRawDoubleWord(InterruptRequestOffset,
                ReadRawDoubleWord(InterruptRequestOffset) | (1u << channel));
        }

        private void ReportError(int channel, uint reasonCode)
        {
            WriteRawDoubleWord(ErrorRequestOffset,
                ReadRawDoubleWord(ErrorRequestOffset) | (1u << channel));
            ErrorCount++;
            LastErrorReasonCode = reasonCode;
            this.Log(LogLevel.Warning, "eDMA channel {0} rejected transfer, reason code {1}", channel, reasonCode);
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            var pending = ReadRawDoubleWord(InterruptRequestOffset);
            for(var channel = 0; channel < ChannelCount; channel++)
            {
                connections[channel].Set((pending & (1u << channel)) != 0);
            }
            Error.Set((ReadRawDoubleWord(ErrorRequestOffset) & ReadRawDoubleWord(EnableErrorInterruptOffset)) != 0);
        }

        private void SynchronizeCurrentCpuTime()
        {
            if(systemBus.TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
        }

        private uint DecodeMinorByteCount(uint value)
        {
            if((ReadRawDoubleWord(ControlOffset) & EnableMinorLoopMappingMask) == 0)
            {
                return value;
            }
            return (value & MinorLoopOffsetEnableMask) != 0
                ? value & MinorByteCountWithOffsetMask
                : value & MinorByteCountWithoutOffsetMask;
        }

        private ushort ReadRawWord(long offset)
        {
            return (ushort)((registers[offset] << 8) | registers[offset + 1]);
        }

        private uint ReadRawDoubleWord(long offset)
        {
            return ((uint)registers[offset] << 24)
                | ((uint)registers[offset + 1] << 16)
                | ((uint)registers[offset + 2] << 8)
                | registers[offset + 3];
        }

        private void WriteRawWord(long offset, ushort value)
        {
            registers[offset] = (byte)(value >> 8);
            registers[offset + 1] = (byte)value;
        }

        private void WriteRawDoubleWord(long offset, uint value)
        {
            registers[offset] = (byte)(value >> 24);
            registers[offset + 1] = (byte)(value >> 16);
            registers[offset + 2] = (byte)(value >> 8);
            registers[offset + 3] = (byte)value;
        }

        private readonly ulong[] channelCompletionCounts;
        private readonly byte[] muxConfiguration;
        private readonly Dictionary<int, IGPIO> connections;
        private readonly ChannelState[] channels;
        private readonly IBusController systemBus;
        private readonly IMachine machine;

        private readonly object sync = new object();
        private readonly byte[] registers;
        private const ushort IterationLinkMask = 1 << 15;
        private const ushort ChannelDoneMask = 1 << 7;
        private const ushort ChannelActiveMask = 1 << 6;
        private const ushort EnableMajorLinkMask = 1 << 5;
        private const ushort EnableScatterGatherMask = 1 << 4;
        private const ushort DisableRequestMask = 1 << 3;
        private const ushort ChannelStartMask = 1 << 0;
        private const ushort InterruptMajorMask = 1 << 1;
        private const uint EnableMinorLoopMappingMask = 1u << 7;

        private const uint HaltMask = 1u << 5;
        private const long TcdControlStatusOffset = 0x1E;
        private const long TcdBeginningIterationOffset = 0x1C;
        private const ushort InterruptHalfMask = 1 << 2;
        private const ushort IterationCountMask = 0x7FFF;
        private const uint SourceMinorLoopOffsetEnableMask = 0x80000000;

        private const byte CommandNoOperationMask = 1 << 7;
        private const byte CommandAllChannelsMask = 1 << 6;
        private const byte CommandChannelMask = 0x3F;
        private const byte DmaMuxEnableMask = 1 << 7;
        private const byte DmaMuxTriggerMask = 1 << 6;
        private const byte DmaMuxSourceMask = 0x3F;
        private const byte FirstAlwaysRequestSource = 56;

        private const uint MinorLoopOffsetEnableMask = 0xC0000000;
        private const long TcdDestinationLastAdjustmentOffset = 0x18;
        private const uint DestinationMinorLoopOffsetEnableMask = 0x40000000;
        private const uint MinorByteCountWithOffsetMask = 0x3FF;
        private const uint MinorByteCountWithoutOffsetMask = 0x3FFFFFFF;

        private const uint ErrorInvalidGeometry = 1;
        private const ushort UnsupportedModuloMask = 0xF8F8;
        private const long TcdDestinationOffsetOffset = 0x16;
        private const long InterruptRequestOffset = 0x24;
        private const long TcdDestinationAddressOffset = 0x10;

        private const int ChannelCount = 16;
        private const int SizeValue = 0x4000;
        private const ulong DefaultTransferFrequency = 40000000;
        private const ulong FixedLatencyTicks = 16;
        private const ulong MaximumTimerTicks = 1UL << 32;
        private const uint MaximumMinorByteCount = 0x100000;
        private const int MaximumTransferSizeCode = 5;

        private const long ControlOffset = 0x00;
        private const long EnableRequestOffset = 0x0C;
        private const long EnableErrorInterruptOffset = 0x14;
        private const long CommandBaseOffset = 0x18;
        private const long SetEnableRequestOffset = 0x18;
        private const long ClearEnableRequestOffset = 0x19;
        private const long SetEnableErrorInterruptOffset = 0x1A;
        private const long ClearEnableErrorInterruptOffset = 0x1B;
        private const long ClearInterruptRequestOffset = 0x1C;
        private const long ClearErrorRequestOffset = 0x1D;
        private const long SetStartOffset = 0x1E;
        private const long ClearDoneOffset = 0x1F;
        private const long CommandEndOffset = 0x20;
        private const uint ErrorUnmappedOrPeripheralAddress = 2;
        private const long ErrorRequestOffset = 0x2C;
        private const long TcdBaseOffset = 0x1000;
        private const int TcdStride = 0x20;
        private const long TcdSourceAddressOffset = 0x00;
        private const long TcdAttributesOffset = 0x04;
        private const long TcdSourceOffsetOffset = 0x06;
        private const long TcdNBytesOffset = 0x08;
        private const long TcdSourceLastAdjustmentOffset = 0x0C;
        private const long TcdCurrentIterationOffset = 0x14;
        private const uint ErrorUnsupportedConfiguration = 3;

        private sealed class ChannelState
        {
            public LimitTimer Timer;
            public PendingTransfer Pending;
            public bool Active;
        }

        private struct PendingTransfer
        {
            public uint SourceAddress;
            public uint DestinationAddress;
            public int SourceOffset;
            public int DestinationOffset;
            public uint SourceSize;
            public uint DestinationSize;
            public uint BurstSize;
            public uint NBytes;
            public uint NBytesRaw;
            public ushort CurrentIteration;
            public ushort BeginningIteration;
            public int SourceLastAdjustment;
            public int DestinationLastAdjustment;
            public ushort ControlStatus;
        }
    }

    public sealed class MPC5645S_DMAMUX : IDoubleWordPeripheral, IWordPeripheral,
        IBytePeripheral, IKnownSize
    {
        public MPC5645S_DMAMUX(MPC5645S_eDMA dma)
        {
            this.dma = dma ?? throw new ArgumentNullException(nameof(dma));
            channels = new byte[ChannelCount];
            Reset();
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

        public void Reset()
        {
            requestPropagationEnabled = true;
            for(var channel = 0; channel < ChannelCount; channel++)
            {
                channels[channel] = 0;
                dma.SetMuxConfiguration(channel, 0);
            }
        }

        // Host-side diagnostic control for matched disabled/working tests.
        // It is not guest-visible and defaults to normal hardware propagation.
        public bool RequestPropagationEnabled
        {
            get => requestPropagationEnabled;
            set
            {
                requestPropagationEnabled = value;
                for(var channel = 0; channel < ChannelCount; channel++)
                {
                    dma.SetMuxConfiguration(channel, value ? channels[channel] : (byte)0);
                }
            }
        }

        public long Size => SizeValue;

        private static bool IsValidRange(long offset, int size)
        {
            return (size == 1 || size == 2 || size == 4)
                && offset >= 0 && offset <= SizeValue - size;
        }

        private uint ReadSized(long offset, int size)
        {
            if(!IsValidRange(offset, size))
            {
                return 0;
            }
            uint result = 0;
            for(var index = 0; index < size; index++)
            {
                var channel = offset + index;
                result = (result << 8) | (channel < ChannelCount ? channels[channel] : (byte)0);
            }
            return result;
        }

        private void WriteSized(long offset, int size, uint value)
        {
            if(!IsValidRange(offset, size))
            {
                return;
            }
            for(var index = 0; index < size; index++)
            {
                var channel = offset + index;
                if(channel >= ChannelCount)
                {
                    continue;
                }
                var byteValue = (byte)(value >> ((size - 1 - index) * 8));
                channels[channel] = byteValue;
                if(requestPropagationEnabled)
                {
                    dma.SetMuxConfiguration((int)channel, byteValue);
                }
            }
        }

        private bool requestPropagationEnabled;

        private readonly MPC5645S_eDMA dma;
        private readonly byte[] channels;

        private const int ChannelCount = 16;
        private const int SizeValue = 0x4000;
    }
}
