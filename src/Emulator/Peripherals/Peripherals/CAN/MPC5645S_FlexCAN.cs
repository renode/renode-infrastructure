//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Alexey Zagorodnikov <xglooom@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CAN
{
    // MPC5645S FlexCAN subset used by the tested MPC5645S firmware. This keeps the classic
    // 64 x 16-byte mailbox layout and six grouped mailbox interrupt outputs.
    // CAN electrical arbitration and error confinement are outside this model.
    public class MPC5645S_FlexCAN : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral,
        IKnownSize, IHasFrequency, ICAN, INumberedGPIOOutput
    {
        public MPC5645S_FlexCAN(IMachine machine, ulong baudRate = DefaultBaudRate,
            ulong timerFrequency = DefaultTimerFrequency)
        {
            if(baudRate == 0)
            {
                throw new ConstructionException("baudRate must be non-zero");
            }
            if(timerFrequency == 0)
            {
                throw new ConstructionException("timerFrequency must be non-zero");
            }

            this.machine = machine;
            this.baudRate = baudRate;
            messageBufferWords = new uint[MailboxCount * MailboxWordCount];
            individualMasks = new uint[MailboxCount];
            mailboxLocked = new bool[MailboxCount];
            transmitPending = new bool[MailboxCount];
            mailboxReceiveCounts = new ulong[MailboxCount];
            receivedIds = new List<uint>();
            replayFrames = new List<ReplayFrame>();
            replaySourceChannels = new List<string>();

            var outputs = new Dictionary<int, IGPIO>();
            interruptOutputs = new GPIO[InterruptOutputCount];
            for(var i = 0; i < InterruptOutputCount; i++)
            {
                interruptOutputs[i] = new GPIO();
                outputs[i] = interruptOutputs[i];
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(outputs);

            freeRunningTimer = new LimitTimer(machine.ClockSource, timerFrequency, this,
                nameof(freeRunningTimer), limit: ushort.MaxValue + 1UL,
                direction: Direction.Ascending, enabled: false,
                workMode: WorkMode.Periodic, eventEnabled: false, autoUpdate: true);
            Reset();
        }

        public void Reset()
        {
            lock(sync)
            {
                ResetController();
                replayActive = false;
                replayGeneration++;
                replayIndex = 0;
                replayAttemptedFrameCount = 0;
                replayCompletedLoops = 0;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            lock(sync)
            {
                if(!IsValidAccess(offset, 4))
                {
                    return 0;
                }
                var aligned = offset & ~0x3L;
                if(TryDecodeMailbox(aligned, out var mailbox, out var word))
                {
                    if(word == 0)
                    {
                        var code = (messageBufferWords[mailbox * MailboxWordCount] >> CodeShift) & CodeMask;
                        if(code == ReceiveFullCode || code == ReceiveOverrunCode)
                        {
                            mailboxLocked[mailbox] = true;
                        }
                    }
                    return messageBufferWords[mailbox * MailboxWordCount + word];
                }
                if(TryDecodeIndividualMask(aligned, out var maskIndex))
                {
                    return individualMasks[maskIndex];
                }
                return ReadRegister(aligned);
            }
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)((ReadByte(offset) << 8) | ReadByte(offset + 1));
        }

        public byte ReadByte(long offset)
        {
            var aligned = offset & ~0x3L;
            var shift = (int)((3 - (offset & 0x3L)) * 8);
            return (byte)(ReadDoubleWord(aligned) >> shift);
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

        public void OnFrameReceived(CANMessageFrame message)
        {
            lock(sync)
            {
                DeliverFrame(message);
            }
        }

        public void InjectFrame(uint id, string hexadecimalData, bool extendedFormat = false,
            bool remoteFrame = false)
        {
            byte[] data;
            try
            {
                data = Misc.HexStringToByteArray(hexadecimalData);
            }
            catch(Exception exception)
            {
                throw new RecoverableException($"Invalid CAN data: {exception.Message}");
            }
            if(data.Length > MaximumClassicalPayload)
            {
                throw new RecoverableException("Classical CAN payload cannot exceed 8 bytes");
            }
            OnFrameReceived(new CANMessageFrame(id, data, extendedFormat, remoteFrame));
        }

        public void LoadReplay(string path, bool repeat = false)
        {
            lock(sync)
            {
                if(replayActive)
                {
                    throw new RecoverableException("Cannot replace a replay while it is active");
                }
                if(!File.Exists(path))
                {
                    throw new RecoverableException($"Replay file does not exist: {path}");
                }

                var parsed = new List<ReplayFrame>();
                var channels = new List<string>();
                decimal? firstTimestamp = null;
                decimal? previousTimestamp = null;
                var lineNumber = 0;
                foreach(var line in File.ReadLines(path))
                {
                    lineNumber++;
                    var match = CandumpPattern.Match(line.Trim());
                    if(!match.Success)
                    {
                        throw new RecoverableException($"Malformed candump record at line {lineNumber}");
                    }
                    if(!decimal.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var timestamp))
                    {
                        throw new RecoverableException($"Invalid timestamp at line {lineNumber}");
                    }
                    if(previousTimestamp.HasValue && timestamp < previousTimestamp.Value)
                    {
                        throw new RecoverableException($"Timestamp regression at line {lineNumber}");
                    }

                    var channel = match.Groups[2].Value;
                    if(!channels.Contains(channel))
                    {
                        channels.Add(channel);
                    }
                    if(!uint.TryParse(match.Groups[3].Value, NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture, out var id))
                    {
                        throw new RecoverableException($"Invalid CAN identifier at line {lineNumber}");
                    }
                    var dataText = match.Groups[4].Value;
                    if((dataText.Length & 1) != 0)
                    {
                        throw new RecoverableException($"Odd-length CAN payload at line {lineNumber}");
                    }
                    var data = Misc.HexStringToByteArray(dataText);
                    if(data.Length > MaximumClassicalPayload)
                    {
                        throw new RecoverableException($"CAN FD record is unsupported at line {lineNumber}");
                    }
                    var extended = id > StandardIdentifierMask;
                    if(id > (extended ? ExtendedIdentifierMask : StandardIdentifierMask))
                    {
                        throw new RecoverableException($"Out-of-range CAN identifier at line {lineNumber}");
                    }

                    if(!firstTimestamp.HasValue)
                    {
                        firstTimestamp = timestamp;
                    }
                    var offsetMicroseconds = DecimalSecondsToMicroseconds(timestamp - firstTimestamp.Value);
                    parsed.Add(new ReplayFrame(offsetMicroseconds,
                        new CANMessageFrame(id, data, extended)));
                    previousTimestamp = timestamp;
                }
                if(parsed.Count == 0)
                {
                    throw new RecoverableException("Replay contains no CAN frames");
                }

                replayFrames = parsed;
                replaySourceChannels = channels;
                replayPath = Path.GetFullPath(path);
                replayRepeat = repeat;
                replayFirstTimestamp = firstTimestamp.Value;
                replayDurationMicroseconds = parsed[parsed.Count - 1].OffsetMicroseconds;
                replayLoopGapMicroseconds = FindFirstPositiveDelta(parsed);
                replayIndex = 0;
                replayAttemptedFrameCount = 0;
                replayCompletedLoops = 0;
            }
        }

        public void StartReplay()
        {
            lock(sync)
            {
                if(replayActive)
                {
                    throw new RecoverableException("Replay is already active");
                }
                if(replayFrames.Count == 0)
                {
                    throw new RecoverableException("No replay has been loaded");
                }
                replayActive = true;
                replayIndex = 0;
                replayAttemptedFrameCount = 0;
                replayCompletedLoops = 0;
                replayGeneration++;
                ScheduleReplayFrame(replayGeneration, replayFrames[0].OffsetMicroseconds);
            }
        }

        public void StopReplay()
        {
            lock(sync)
            {
                replayActive = false;
                replayGeneration++;
            }
        }

        public bool GetInterruptState(int route)
        {
            if(route < 0 || route >= interruptOutputs.Length)
            {
                throw new RecoverableException($"Invalid interrupt route {route}");
            }
            return interruptOutputs[route].IsSet;
        }

        public ulong GetMailboxReceiveCount(int mailbox)
        {
            if(mailbox < 0 || mailbox >= MailboxCount)
            {
                throw new RecoverableException($"Invalid mailbox {mailbox}");
            }
            return mailboxReceiveCounts[mailbox];
        }

        public uint GetReceivedIdAt(int index)
        {
            if(index < 0 || index >= receivedIds.Count)
            {
                throw new RecoverableException($"Invalid received frame index {index}");
            }
            return receivedIds[index];
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public bool ReplayRepeat => replayRepeat;

        public ulong ReplayDurationMicroseconds => replayDurationMicroseconds;

        public string ReplayTimeOrigin => replayFrames.Count == 0 ? string.Empty : replayFirstTimestamp.ToString(CultureInfo.InvariantCulture);

        public string ReplayPath => replayPath ?? string.Empty;

        public string ReplaySourceChannels => string.Join(",", replaySourceChannels);

        public string ReplayPrimarySourceChannel => replaySourceChannels.Count == 0 ? string.Empty : replaySourceChannels[0];

        public int ReplaySourceChannelCount => replaySourceChannels.Count;

        public bool ReplayInProgress => replayActive;

        public ulong ReplayCompletedLoops => replayCompletedLoops;

        public ulong ReplayAttemptedFrameCount => replayAttemptedFrameCount;

        public uint LastTransmittedId => lastTransmittedId;

        public uint LastReceivedId => lastReceivedId;

        public int LastReceivedMailbox => lastReceivedMailbox;

        public ulong TransmittedFrameCount => transmittedFrameCount;

        public ulong DroppedFrameCount => droppedFrameCount;

        public ulong ReceivedFrameCount => receivedFrameCount;

        public ulong BaudRate
        {
            get => baudRate;
            set
            {
                if(value == 0)
                {
                    throw new RecoverableException("BaudRate must be non-zero");
                }
                baudRate = value;
            }
        }

        public ulong Frequency
        {
            get => freeRunningTimer.Frequency;
            set => freeRunningTimer.Frequency = value;
        }

        public long Size => PeripheralSize;

        public ulong ReplayLoadedFrameCount => (ulong)replayFrames.Count;

        public event Action<CANMessageFrame> FrameSent;

        private static ulong FindFirstPositiveDelta(List<ReplayFrame> frames)
        {
            for(var i = 1; i < frames.Count; i++)
            {
                var delta = frames[i].OffsetMicroseconds - frames[i - 1].OffsetMicroseconds;
                if(delta > 0)
                {
                    return delta;
                }
            }
            return 1;
        }

        private static ulong DecimalSecondsToMicroseconds(decimal seconds)
        {
            if(seconds < 0)
            {
                throw new RecoverableException("Negative replay timestamp offset");
            }
            return decimal.ToUInt64(decimal.Round(seconds * 1000000m, 0, MidpointRounding.AwayFromZero));
        }

        private static uint PackDataWord(byte[] data, int start)
        {
            uint value = 0;
            for(var i = 0; i < 4; i++)
            {
                value <<= 8;
                if(start + i < data.Length)
                {
                    value |= data[start + i];
                }
            }
            return value;
        }

        private static uint Merge(uint previous, uint value, uint mask)
        {
            return (previous & ~mask) | (value & mask);
        }

        private void ResetController()
        {
            mcr = ModuleConfigurationResetValue;
            control1 = 0;
            rxGlobalMask = 0;
            rx14Mask = 0;
            rx15Mask = 0;
            errorAndStatus = 0;
            interruptMask1 = 0;
            interruptMask2 = 0;
            interruptFlag1 = 0;
            interruptFlag2 = 0;
            Array.Clear(messageBufferWords, 0, messageBufferWords.Length);
            Array.Clear(individualMasks, 0, individualMasks.Length);
            Array.Clear(mailboxLocked, 0, mailboxLocked.Length);
            Array.Clear(transmitPending, 0, transmitPending.Length);
            Array.Clear(mailboxReceiveCounts, 0, mailboxReceiveCounts.Length);
            receivedIds.Clear();
            receivedFrameCount = 0;
            droppedFrameCount = 0;
            transmittedFrameCount = 0;
            lastReceivedMailbox = -1;
            lastReceivedId = 0;
            lastTransmittedId = 0;
            freeRunningTimer.Reset();
            freeRunningTimer.Limit = ushort.MaxValue + 1UL;
            freeRunningTimer.Value = 0;
            freeRunningTimer.Enabled = false;
            UpdateStateAndInterrupts();
        }

        private void SoftReset(uint requestedMcr)
        {
            // MPC56xx software reset is a command bit, not a request to restore
            // MCR.MDIS. Firmware clears MDIS, writes SOFTRST, then expects the
            // command to self-clear while the controller remains enabled.
            mcr = requestedMcr & ~(ModuleConfigurationReadOnlyMask | SoftResetMask);
            control1 = 0;
            rxGlobalMask = 0;
            rx14Mask = 0;
            rx15Mask = 0;
            errorAndStatus = 0;
            interruptMask1 = 0;
            interruptMask2 = 0;
            interruptFlag1 = 0;
            interruptFlag2 = 0;
            Array.Clear(mailboxLocked, 0, mailboxLocked.Length);
            Array.Clear(transmitPending, 0, transmitPending.Length);
            freeRunningTimer.Reset();
            freeRunningTimer.Limit = ushort.MaxValue + 1UL;
            freeRunningTimer.Value = 0;
            UpdateStateAndInterrupts();
        }

        private uint ReadRegister(long offset)
        {
            switch(offset)
            {
            case ModuleConfigurationOffset:
                return mcr;
            case Control1Offset:
                return control1;
            case TimerOffset:
                SynchronizeCurrentCpuTime();
                ReleaseMailboxLocks();
                return (uint)freeRunningTimer.Value & 0xFFFFu;
            case RxGlobalMaskOffset:
                return rxGlobalMask;
            case Rx14MaskOffset:
                return rx14Mask;
            case Rx15MaskOffset:
                return rx15Mask;
            case ErrorAndStatusOffset:
                return errorAndStatus | (Ready ? (IdleMask | SynchronizationMask) : 0u);
            case InterruptMask2Offset:
                return interruptMask2;
            case InterruptMask1Offset:
                return interruptMask1;
            case InterruptFlag2Offset:
                return interruptFlag2;
            case InterruptFlag1Offset:
                return interruptFlag1;
            default:
                return 0;
            }
        }

        private void WriteSized(long offset, int size, uint value)
        {
            lock(sync)
            {
                if(!IsValidAccess(offset, size))
                {
                    return;
                }
                var lane = (int)(offset & 0x3L);
                if(size == 4 && lane == 0)
                {
                    WriteAligned(offset, value, uint.MaxValue);
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
                var aligned = offset & ~0x3L;
                var bitShift = (4 - lane - size) * 8;
                var valueMask = size == 1 ? 0xFFu : 0xFFFFu;
                WriteAligned(aligned, (value & valueMask) << bitShift, valueMask << bitShift);
            }
        }

        private void WriteAligned(long offset, uint value, uint mask)
        {
            if(TryDecodeMailbox(offset, out var mailbox, out var word))
            {
                var index = mailbox * MailboxWordCount + word;
                messageBufferWords[index] = Merge(messageBufferWords[index], value, mask);
                if(word == 0)
                {
                    TryStartTransmit(mailbox);
                }
                return;
            }
            if(TryDecodeIndividualMask(offset, out var maskIndex))
            {
                individualMasks[maskIndex] = Merge(individualMasks[maskIndex], value, mask);
                return;
            }

            switch(offset)
            {
            case ModuleConfigurationOffset:
                WriteModuleConfiguration(value, mask);
                break;
            case Control1Offset:
                control1 = Merge(control1, value, mask);
                break;
            case TimerOffset:
                // TIMER is read-only; reading it releases receive mailbox locks.
                break;
            case RxGlobalMaskOffset:
                rxGlobalMask = Merge(rxGlobalMask, value, mask);
                break;
            case Rx14MaskOffset:
                rx14Mask = Merge(rx14Mask, value, mask);
                break;
            case Rx15MaskOffset:
                rx15Mask = Merge(rx15Mask, value, mask);
                break;
            case ErrorAndStatusOffset:
                errorAndStatus &= ~(value & mask & ErrorStatusWritableOneToClearMask);
                break;
            case InterruptMask2Offset:
                interruptMask2 = Merge(interruptMask2, value, mask);
                UpdateInterrupts();
                break;
            case InterruptMask1Offset:
                interruptMask1 = Merge(interruptMask1, value, mask);
                UpdateInterrupts();
                break;
            case InterruptFlag2Offset:
                interruptFlag2 &= ~(value & mask);
                UpdateInterrupts();
                break;
            case InterruptFlag1Offset:
                interruptFlag1 &= ~(value & mask);
                UpdateInterrupts();
                break;
            }
        }

        private void WriteModuleConfiguration(uint value, uint mask)
        {
            var merged = Merge(mcr, value, mask);
            if((merged & SoftResetMask) != 0)
            {
                SoftReset(merged);
                return;
            }

            var writable = merged & ~ModuleConfigurationReadOnlyMask;
            mcr = writable;
            UpdateStateAndInterrupts();
            if(Ready)
            {
                TryStartPendingTransmits();
            }
        }

        private void UpdateStateAndInterrupts()
        {
            mcr &= ~(LowPowerAcknowledgeMask | FreezeAcknowledgeMask | NotReadyMask | SoftResetMask);
            if((mcr & ModuleDisableMask) != 0)
            {
                mcr |= LowPowerAcknowledgeMask | NotReadyMask;
            }
            if((mcr & FreezeEnableMask) != 0 && (mcr & HaltMask) != 0)
            {
                mcr |= FreezeAcknowledgeMask | NotReadyMask;
            }
            freeRunningTimer.Enabled = Ready;
            UpdateInterrupts();
        }

        private bool DeliverFrame(CANMessageFrame frame)
        {
            if(frame.FDFormat || frame.Data.Length > MaximumClassicalPayload || !Ready)
            {
                droppedFrameCount++;
                return false;
            }

            var maximumMailbox = Math.Min((int)(mcr & MaximumMailboxMask), MailboxCount - 1);
            for(var mailbox = 0; mailbox <= maximumMailbox; mailbox++)
            {
                if(mailboxLocked[mailbox])
                {
                    continue;
                }
                var cs = GetMailboxWord(mailbox, 0);
                var code = (cs >> CodeShift) & CodeMask;
                if(code != ReceiveEmptyCode && code != ReceiveFullCode && code != ReceiveOverrunCode)
                {
                    continue;
                }
                var configuredExtended = (cs & IdentifierExtendedMask) != 0;
                if(configuredExtended != frame.ExtendedFormat)
                {
                    continue;
                }
                var frameIdentifier = frame.ExtendedFormat ? frame.Id : frame.Id << StandardIdentifierShift;
                var configuredIdentifier = GetMailboxWord(mailbox, 1);
                var receiveMask = GetReceiveMask(mailbox) & ExtendedIdentifierMask;
                if(((frameIdentifier ^ configuredIdentifier) & receiveMask) != 0)
                {
                    continue;
                }

                var nextCode = code == ReceiveEmptyCode ? ReceiveFullCode : ReceiveOverrunCode;
                var nextCs = cs & ~(CodeFieldMask | DataLengthFieldMask | RemoteTransmissionRequestMask | TimestampMask);
                nextCs |= nextCode << CodeShift;
                nextCs |= (uint)frame.Data.Length << DataLengthShift;
                if(frame.RemoteFrame)
                {
                    nextCs |= RemoteTransmissionRequestMask;
                }
                SynchronizeCurrentCpuTime();
                nextCs |= (uint)freeRunningTimer.Value & TimestampMask;

                SetMailboxWord(mailbox, 1, frameIdentifier);
                SetMailboxWord(mailbox, 2, PackDataWord(frame.Data, 0));
                SetMailboxWord(mailbox, 3, PackDataWord(frame.Data, 4));
                SetMailboxWord(mailbox, 0, nextCs);
                SetMailboxInterrupt(mailbox);
                receivedFrameCount++;
                mailboxReceiveCounts[mailbox]++;
                lastReceivedMailbox = mailbox;
                lastReceivedId = frame.Id;
                receivedIds.Add(frame.Id);
                UpdateInterrupts();
                return true;
            }

            droppedFrameCount++;
            return false;
        }

        private void TryStartTransmit(int mailbox)
        {
            var code = (GetMailboxWord(mailbox, 0) >> CodeShift) & CodeMask;
            if(code != TransmitDataCode)
            {
                return;
            }
            if(!Ready)
            {
                transmitPending[mailbox] = true;
                return;
            }
            if(transmitPending[mailbox])
            {
                return;
            }
            transmitPending[mailbox] = true;
            var dlc = (int)((GetMailboxWord(mailbox, 0) >> DataLengthShift) & DataLengthCodeMask);
            dlc = Math.Min(dlc, MaximumClassicalPayload);
            var nominalBits = ClassicalFrameOverheadBits + (ulong)dlc * 8UL;
            var delayMicroseconds = Math.Max(1UL, (nominalBits * 1000000UL + baudRate - 1) / baudRate);
            machine.ScheduleAction(TimeInterval.FromMicroseconds(delayMicroseconds), _ => CompleteTransmit(mailbox));
        }

        private void TryStartPendingTransmits()
        {
            for(var mailbox = 0; mailbox < MailboxCount; mailbox++)
            {
                if(transmitPending[mailbox])
                {
                    transmitPending[mailbox] = false;
                }
                TryStartTransmit(mailbox);
            }
        }

        private void CompleteTransmit(int mailbox)
        {
            lock(sync)
            {
                if(!transmitPending[mailbox])
                {
                    return;
                }
                transmitPending[mailbox] = false;
                var cs = GetMailboxWord(mailbox, 0);
                if(((cs >> CodeShift) & CodeMask) != TransmitDataCode)
                {
                    return;
                }
                var extended = (cs & IdentifierExtendedMask) != 0;
                var remote = (cs & RemoteTransmissionRequestMask) != 0;
                var dlc = Math.Min((int)((cs >> DataLengthShift) & DataLengthCodeMask), MaximumClassicalPayload);
                var idWord = GetMailboxWord(mailbox, 1);
                var id = extended ? idWord & ExtendedIdentifierMask : (idWord >> StandardIdentifierShift) & StandardIdentifierMask;
                var data = UnpackData(mailbox, dlc);
                var frame = new CANMessageFrame(id, data, extended, remote);

                SetMailboxWord(mailbox, 0, (cs & ~CodeFieldMask) | (TransmitInactiveCode << CodeShift));
                SetMailboxInterrupt(mailbox);
                transmittedFrameCount++;
                lastTransmittedId = id;
                UpdateInterrupts();
                FrameSent?.Invoke(frame);
            }
        }

        private void ScheduleReplayFrame(ulong generation, ulong delayMicroseconds)
        {
            machine.ScheduleAction(TimeInterval.FromMicroseconds(delayMicroseconds), _ => ReplayStep(generation));
        }

        private void ReplayStep(ulong generation)
        {
            lock(sync)
            {
                if(!replayActive || generation != replayGeneration)
                {
                    return;
                }
                var frame = replayFrames[replayIndex];
                replayAttemptedFrameCount++;
                DeliverFrame(frame.Message);

                var currentOffset = frame.OffsetMicroseconds;
                replayIndex++;
                if(replayIndex < replayFrames.Count)
                {
                    var delay = replayFrames[replayIndex].OffsetMicroseconds - currentOffset;
                    ScheduleReplayFrame(generation, delay);
                    return;
                }

                replayCompletedLoops++;
                if(!replayRepeat)
                {
                    replayActive = false;
                    replayIndex = 0;
                    return;
                }
                replayIndex = 0;
                ScheduleReplayFrame(generation, replayLoopGapMicroseconds);
            }
        }

        private void UpdateInterrupts()
        {
            var pending = ((ulong)(interruptFlag2 & interruptMask2) << 32)
                | (interruptFlag1 & interruptMask1);
            for(var route = 0; route < InterruptOutputCount; route++)
            {
                interruptOutputs[route].Set((pending & InterruptRouteMasks[route]) != 0);
            }
        }

        private void ReleaseMailboxLocks()
        {
            Array.Clear(mailboxLocked, 0, mailboxLocked.Length);
        }

        private void SetMailboxInterrupt(int mailbox)
        {
            if(mailbox < 32)
            {
                interruptFlag1 |= 1u << mailbox;
            }
            else
            {
                interruptFlag2 |= 1u << (mailbox - 32);
            }
        }

        private uint GetReceiveMask(int mailbox)
        {
            if((mcr & IndividualMaskingMask) != 0)
            {
                return individualMasks[mailbox];
            }
            if(mailbox == 14)
            {
                return rx14Mask;
            }
            if(mailbox == 15)
            {
                return rx15Mask;
            }
            return rxGlobalMask;
        }

        private byte[] UnpackData(int mailbox, int length)
        {
            var result = new byte[length];
            for(var i = 0; i < length; i++)
            {
                var word = GetMailboxWord(mailbox, 2 + i / 4);
                result[i] = (byte)(word >> ((3 - (i % 4)) * 8));
            }
            return result;
        }

        private uint GetMailboxWord(int mailbox, int word)
        {
            return messageBufferWords[mailbox * MailboxWordCount + word];
        }

        private void SetMailboxWord(int mailbox, int word, uint value)
        {
            messageBufferWords[mailbox * MailboxWordCount + word] = value;
        }

        private bool TryDecodeMailbox(long offset, out int mailbox, out int word)
        {
            mailbox = -1;
            word = -1;
            if(offset < MessageBufferBaseOffset || offset >= MessageBufferBaseOffset + MailboxCount * MailboxStride)
            {
                return false;
            }
            var relative = offset - MessageBufferBaseOffset;
            mailbox = (int)(relative / MailboxStride);
            word = (int)((relative % MailboxStride) / 4);
            return true;
        }

        private bool TryDecodeIndividualMask(long offset, out int index)
        {
            index = -1;
            if(offset < IndividualMaskBaseOffset || offset >= IndividualMaskBaseOffset + MailboxCount * 4)
            {
                return false;
            }
            index = (int)((offset - IndividualMaskBaseOffset) / 4);
            return true;
        }

        private bool IsValidAccess(long offset, int size)
        {
            return (size == 1 || size == 2 || size == 4)
                && offset >= 0 && offset + size <= PeripheralSize;
        }

        private void SynchronizeCurrentCpuTime()
        {
            if(machine.GetSystemBus(this).TryGetCurrentCPU(out var cpu))
            {
                cpu.SyncTime();
            }
        }

        private bool Ready => (mcr & ModuleDisableMask) == 0
            && !((mcr & FreezeEnableMask) != 0 && (mcr & HaltMask) != 0);

        private uint interruptFlag1;
        private uint interruptFlag2;
        private ulong baudRate;
        private ulong receivedFrameCount;
        private ulong droppedFrameCount;
        private ulong transmittedFrameCount;
        private int lastReceivedMailbox;
        private uint lastReceivedId;
        private uint lastTransmittedId;

        private bool replayActive;
        private int replayIndex;
        private ulong replayGeneration;
        private ulong replayCompletedLoops;
        private ulong replayDurationMicroseconds;
        private ulong replayLoopGapMicroseconds;
        private decimal replayFirstTimestamp;
        private string replayPath;
        private bool replayRepeat;
        private uint interruptMask2;
        private ulong replayAttemptedFrameCount;
        private uint errorAndStatus;
        private uint rx15Mask;
        private uint rx14Mask;
        private uint rxGlobalMask;
        private uint control1;

        private uint mcr;
        private List<string> replaySourceChannels;
        private List<ReplayFrame> replayFrames;
        private uint interruptMask1;

        private readonly object sync = new object();
        private readonly IMachine machine;
        private readonly LimitTimer freeRunningTimer;
        private readonly uint[] messageBufferWords;
        private readonly uint[] individualMasks;
        private readonly bool[] mailboxLocked;
        private readonly bool[] transmitPending;
        private readonly ulong[] mailboxReceiveCounts;
        private readonly GPIO[] interruptOutputs;
        private readonly List<uint> receivedIds;
        private const uint DataLengthCodeMask = 0xF;
        private const uint LowPowerAcknowledgeMask = 0x00100000;
        private const uint IndividualMaskingMask = 0x00010000;
        private const uint MaximumMailboxMask = 0x0000003F;
        private const uint ModuleConfigurationReadOnlyMask = NotReadyMask | SoftResetMask
            | FreezeAcknowledgeMask | LowPowerAcknowledgeMask;

        private const int CodeShift = 24;
        private const uint CodeMask = 0xF;
        private const uint CodeFieldMask = CodeMask << CodeShift;
        private const int DataLengthShift = 16;
        private const uint FreezeAcknowledgeMask = 0x01000000;
        private const uint DataLengthFieldMask = DataLengthCodeMask << DataLengthShift;
        private const int StandardIdentifierShift = 18;
        private const uint IdentifierExtendedMask = 0x00200000;
        private const uint TimestampMask = 0x0000FFFF;
        private const uint ReceiveEmptyCode = 0x4;
        private const uint ReceiveFullCode = 0x2;
        private const uint ReceiveOverrunCode = 0x6;
        private const uint TransmitInactiveCode = 0x8;
        private const uint TransmitDataCode = 0xC;
        private const uint SoftResetMask = 0x02000000;
        private const uint StandardIdentifierMask = 0x7FF;
        private const uint ExtendedIdentifierMask = 0x1FFFFFFF;

        private const uint IdleMask = 1u << 7;
        private const uint RemoteTransmissionRequestMask = 0x00100000;
        private const uint NotReadyMask = 0x08000000;
        private const long InterruptMask2Offset = 0x24;
        private const uint FreezeEnableMask = 0x40000000;
        private const uint SynchronizationMask = 1u << 18;

        private const long PeripheralSize = 0x4000;
        private const int MailboxCount = 64;
        private const int MailboxWordCount = 4;
        private const int MailboxStride = 0x10;
        private const int InterruptOutputCount = 6;
        private const int MaximumClassicalPayload = 8;
        private const ulong DefaultBaudRate = 500000;
        private const ulong DefaultTimerFrequency = 1000000;
        private const ulong ClassicalFrameOverheadBits = 47;

        private const long ModuleConfigurationOffset = 0x00;
        private const uint HaltMask = 0x10000000;
        private const long Control1Offset = 0x04;
        private const long RxGlobalMaskOffset = 0x10;
        private const long Rx14MaskOffset = 0x14;
        private const long Rx15MaskOffset = 0x18;
        private const long ErrorAndStatusOffset = 0x20;
        private const long InterruptMask1Offset = 0x28;
        private const long InterruptFlag2Offset = 0x2C;
        private const long InterruptFlag1Offset = 0x30;
        private const long MessageBufferBaseOffset = 0x80;
        private const long IndividualMaskBaseOffset = 0x880;

        private const uint ModuleConfigurationResetValue = 0xD890000F;
        private const uint ModuleDisableMask = 0x80000000;
        private const long TimerOffset = 0x08;
        private const uint ErrorStatusWritableOneToClearMask = 0xFFFFFFFF;

        private sealed class ReplayFrame
        {
            public ReplayFrame(ulong offsetMicroseconds, CANMessageFrame message)
            {
                OffsetMicroseconds = offsetMicroseconds;
                Message = message;
            }

            public ulong OffsetMicroseconds { get; }

            public CANMessageFrame Message { get; }
        }

        private static readonly ulong[] InterruptRouteMasks =
        {
            0x000000000000000FUL,
            0x00000000000000F0UL,
            0x0000000000000F00UL,
            0x000000000000F000UL,
            0x00000000FFFF0000UL,
            0xFFFFFFFF00000000UL,
        };

        private static readonly Regex CandumpPattern = new Regex(
            @"^\(([0-9]+(?:\.[0-9]+)?)\)\s+(\S+)\s+([0-9A-Fa-f]+)#([0-9A-Fa-f]*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }
}
