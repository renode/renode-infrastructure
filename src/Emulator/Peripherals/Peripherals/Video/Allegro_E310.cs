//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

using ELFSharp.ELF;

using MiscUtil.Conversion;

namespace Antmicro.Renode.Peripherals.Video
{
    public class Allegro_E310 : BasicDoubleWordPeripheral, IKnownSize
    {
        public Allegro_E310(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Unset();

            cpuToMcuMailbox?.Reset();
            mcuToCpuMailbox?.Reset();

            channels.Clear();
            nextChannelUid = 0;
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            // The mailboxes are actually just a region in the MCU SRAM, where the bootloader is also loaded while the MCU is in the
            // reset/sleep state. Accordingly, ignore writes to the SRAM in this state to avoid confusion while transferring the BL.
            if(mcuSleeping.Value && offset < McuSramSize)
            {
                return;
            }
            base.WriteDoubleWord(offset, value);
        }

        public long Size => 0x10000;

        public GPIO IRQ { get; } = new GPIO();

        public string FrameDumpDirectory { get; set; }

        public IEnumerable<IVideo> Channels => channels.Values;

        public IVideo this[uint uid] => channels.TryGetValue(uid, out var channel) ? channel : throw new RecoverableException($"No channel with UID {uid}");

        protected void ExecuteWithDelay(Action action, ulong milliseconds = 50)
        {
            machine.ScheduleAction(TimeInterval.FromMilliseconds(milliseconds), _ => action());
        }

        private static ChannelBuffers GetBuffersNeeded(Channel channel)
        {
            // Sizes are a linear interpolation based on 3840x2160 8bpp values from real HW
            var referencePixels = (double)3840 * 2160;
            var channelPixels = channel.EncodedWidth * channel.EncodedHeight;
            var intermediateSize = (int)(0x2917340 * (channelPixels / referencePixels));
            var reconstructedSize = (int)(0xcf1300 * (channelPixels / referencePixels));
            if(channel.SourceBitDepth > 8)
            {
                intermediateSize *= 2;
                reconstructedSize *= 2;
            }
            return new ChannelBuffers
            {
                IntermediateBufferCount = 2,
                IntermediateBufferSize = intermediateSize,
                ReconstructedBufferCount = 3,
                ReconstructedBufferSize = reconstructedSize,
                Reserved = 0x3066,
            };
        }

        private void DefineRegisters()
        {
            Registers.McuInterruptHandler.Define(this)
                .WithValueField(0, 32, name: "instruction", writeCallback: (_, val) =>
                    {
                        if(val == McuSleepInstruction)
                        {
                            this.DebugLog("Entering sleep state by sleep instruction");
                            mcuSleeping.Value = true;
                        }
                    })
            ;

            cpuToMcuMailbox = new Mailbox(this, nameof(cpuToMcuMailbox), (long)Registers.CommandMailbox, HandleCpuToMcuMessage);

            mcuToCpuMailbox = new Mailbox(this, nameof(mcuToCpuMailbox), (long)Registers.StatusMailbox);

            Registers.McuReset.Define(this)
                .WithFlag(0, name: "reset", writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            Reset();
                        }
                    })
                .WithReservedBits(1, 31)
            ;

            Registers.McuResetMode.Define(this)
                .WithTaggedFlag("reset_mode", 0)
                .WithReservedBits(1, 31)
            ;

            Registers.McuStatus.Define(this, 1)
                .WithFlag(0, out mcuSleeping, name: "sleeping")
                .WithReservedBits(1, 31)
            ;

            Registers.McuWakeup.Define(this)
                .WithFlag(0, name: "wakeup", writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.InfoLog("Waking up");
                            mcuSleeping.Value = false;
                            // Driver waits for AL_MCU_MSG_INIT
                            SendMessage(MessageId.Initialise, new McuInitFeedback { ChanUid = 0 });
                        }
                    })
                .WithReservedBits(1, 31)
            ;

            Registers.IcacheAddrOffsetMsb.Define(this)
                .WithValueField(0, 32, out icacheAddrHigh, name: "icache_addr_high")
            ;

            Registers.IcacheAddrOffsetLsb.Define(this)
                .WithValueField(0, 32, out icacheAddrLow, name: "icache_addr_low")
            ;

            Registers.DcacheAddrOffsetMsb.Define(this)
                .WithValueField(0, 32, out dcacheAddrHigh, name: "dcache_addr_high")
            ;

            Registers.DcacheAddrOffsetLsb.Define(this)
                .WithValueField(0, 32, out dcacheAddrLow, name: "dcache_addr_low")
            ;

            Registers.McuInterruptTrigger.Define(this)
                .WithFlag(0, name: "interrupt", writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            this.DebugLog("Driver signalled MCU interrupt");
                        }
                    })
                .WithReservedBits(1, 31)
            ;

            Registers.McuInterruptMask.Define(this)
                .WithFlag(0, out interruptMask, name: "interrupt_mask")
                .WithReservedBits(1, 31)
                .WithChangeCallback((_, __) => UpdateInterrupt())
            ;

            Registers.McuInterruptClear.Define(this)
                .WithFlag(0, FieldMode.WriteOneToClear, name: "interrupt_clr", writeCallback: (_, val) =>
                    {
                        interruptStatus.Value &= !val;
                    })
                .WithReservedBits(1, 31)
                .WithWriteCallback((_, __) => UpdateInterrupt())
            ;

            Registers.McuInterruptStatus.Define(this)
                .WithFlag(0, out interruptStatus, FieldMode.Read, name: "MCU_IRQ_STA")
                .WithReservedBits(1, 31)
            ;

            Registers.AxiAddressOffset.Define(this)
                .WithValueField(0, 32, out axiAddrOffsetIp, name: "axi_addr_offset_ip")
            ;
        }

        private Type GetCommandType(MessageId id, IList<byte> payload)
        {
            switch(id)
            {
            case MessageId.Initialise:
                return typeof(McuInitMsg);
            case MessageId.GetMcuParameter:
                return typeof(McuGetMsg);
            case MessageId.CreateChannel:
                return typeof(CreateChannelMsg);
            case MessageId.DestroyChannel:
            case MessageId.DestroyChannelQuiet:
                return typeof(DestroyChannelMsg);
            case MessageId.PushIntermediateBuffer:
            case MessageId.PushReferenceBuffer:
                this.DebugLog("Got {0} {1} buffers, ignoring", (payload.Count - 4) / 12, id == MessageId.PushIntermediateBuffer ? "intermediate" : "reference");
                return typeof(NoOperationMsg);
            case MessageId.EncodeOneFrame:
                // After encoding is finished the driver sends an 8-byte all-zero message, ignore it (don't treat it as an error)
                return payload.Count == 8 ? typeof(NoOperationMsg) : typeof(EncodeOneFrameMsg);
            case MessageId.PutStreamBuffer:
                return typeof(PutStreamBufferMsg);
            default:
                var senderId = payload.Count >= 4 ? EndianBitConverter.Little.ToUInt32(payload.Take(4).ToArray(), 0) : 0;
                this.WarningLog("Unhandled message ID: {0} ({1}) from sender {2}: {3}",
                    (ushort)id, id, senderId, Misc.ToLazyHexString(payload));
                return typeof(NoOperationMsg);
            }
        }

        private void HandleCpuToMcuMessage()
        {
            var msg = cpuToMcuMailbox.ReadMessage();
            if(msg == null)
            {
                return;
            }

            // The first word in the body is usually the sender ID (user_uid or chan_uid), use it for diagnostic messages
            // even if we don't handle this message type
            var senderId = msg.Body.Length >= 4 ? EndianBitConverter.Little.ToUInt32(msg.Body, 0) : 0;
            var messageId = msg.Header.MessageId;
            this.NoisyLog("Received message with ID {0} ({1}), body size {2}, sender {3}", messageId, (ushort)messageId, msg.Header.BodySize, senderId);
            this.NoisyLog("Message bytes:\n{0}", Misc.ToLazyHexString(msg.Body));
            try
            {
                var cmd = Packet.DecodeSubclass<ICommand>(msg.Body, p => GetCommandType(messageId, p));
                this.NoisyLog("Command: {0}", cmd);
                var feedback = cmd.Execute(this);
                if(feedback != null)
                {
                    SendMessage(messageId, feedback);
                }
            }
            catch(Exception e)
            {
                this.ErrorLog("Error when parsing or executing command {0}: {1}", messageId, e);
            }
        }

        private void SendMessage<T>(MessageId msgId, T body) where T : IFeedback
        {
            var bytes = Packet.Encode((object)body);
            SendMessage(msgId, bytes);
            this.InfoLog("Sent message with ID {0} ({1}), body size {2}: {3}", (ushort)msgId, msgId, bytes.Length, body);
        }

        private void SendMessage(MessageId msgId, byte[] body)
        {
            if(!mcuToCpuMailbox.TryWriteMessage(msgId, body))
            {
                this.ErrorLog("Failed to send message {0}: Mailbox full", msgId);
                return;
            }

            interruptStatus.Value = true;
            UpdateInterrupt();
        }

        private void UpdateInterrupt()
        {
            var irqState = interruptStatus.Value && interruptMask.Value;
            if(irqState != IRQ.IsSet)
            {
                this.InfoLog("IRQ set to {0}", irqState);
            }
            IRQ.Set(irqState);
        }

        private ulong TranslateDMAAddress(ulong addr)
        {
            return addr + DMABase;
        }

        private ulong TranslateICacheAddress(ulong addr)
        {
            // TODO: This works in testing, but is it fully correct?
            if(addr > McuCacheOffset)
            {
                addr -= McuCacheOffset;
            }
            return TranslateDMAAddress(addr);
        }

        private T ReadStruct<T>(ulong baseAddress, uint index = 0)
        {
            var length = Packet.CalculateLength<T>();
            var elementAddress = baseAddress + (ulong)length * index;
            return Packet.Decode<T>(sysbus.ReadBytes(elementAddress, length, context: this));
        }

        private void WriteStruct<T>(ulong baseAddress, T obj)
        {
            var bytes = Packet.Encode(obj);
            sysbus.WriteBytes(bytes, baseAddress, context: this);
        }

        private ulong DMABase => axiAddrOffsetIp.Value << 32;

        private IValueRegisterField icacheAddrHigh;
        private IValueRegisterField icacheAddrLow;
        private IValueRegisterField dcacheAddrHigh;
        private IValueRegisterField dcacheAddrLow;
        private IValueRegisterField axiAddrOffsetIp;
        private IFlagRegisterField mcuSleeping;
        private IFlagRegisterField interruptStatus;
        private IFlagRegisterField interruptMask;

        private Mailbox cpuToMcuMailbox;
        private Mailbox mcuToCpuMailbox;
        private int nextChannelUid;

        private readonly ConcurrentDictionary<uint, Channel> channels = new ConcurrentDictionary<uint, Channel>();

        private static readonly int McuSramSize = 32.KB();
        private const int NumberOfFrameTypes = 3; // I, P, B
        private const int NumberOfCores = 4;
        private const int MaxRowsPerTile = 22;
        private const uint McuSleepInstruction = 0xBA020004; // `mbar 16`
        private const uint MessageHeaderSize = 4;
        private const uint EncoderVersion = 0x2a0000;
        private const uint McuCacheOffset = 0x80000000;

        private class Channel : IVideo
        {
            public Channel(Allegro_E310 owner, uint uid, CreateChannelMsg msg)
            {
                this.owner = owner;
                Uid = uid;
                var param = owner.ReadStruct<EncodeChannelParameters>(owner.TranslateICacheAddress(msg.ParamPointer));
                owner.DebugLog("[ch{0}] Encoding parameters: {1}", Uid, param);
                EncodedWidth = param.EncodedWidth;
                EncodedHeight = param.EncodedHeight;
                SourceBitDepth = param.SourceBitDepth;
            }

            public override string ToString() => $"[ch{Uid}: {EncodedWidth}x{EncodedHeight} {SourceBitDepth}bpc]";

            public void PutStreamBuffer(PutStreamBufferMsg msg)
            {
                owner.DebugLog("[ch{0}] Put stream buffer {1}", Uid, msg);
                if((int)msg.Index != streamBuffers.Count) // TODO: Don't store all stream buffers, allow > 2³¹ of them, allow out of order (?)
                {
                    owner.WarningLog("[ch{0}] Stream buffer {0} arrived out of order", msg.Index);
                }
                streamBuffers.Add(msg);
            }

            public EncodeOneFrameFeedback EncodeFrame(EncodeOneFrameMsg msg)
            {
                var pitch = (int)msg.BufferAddresses.Pitch;
                var luma = owner.sysbus.ReadBytes(owner.TranslateDMAAddress(msg.BufferAddresses.YPointer), pitch * EncodedHeight);
                // TODO: Use selected format (PictureFormat), assumes 4:2:0 NV12
                // TODO: Source cropping
                // TODO: Bit depth other than 8
                // TODO: SourceMode other than Raster
                var chroma = msg.BufferAddresses.C1Pointer != 0 ? owner.sysbus.ReadBytes(owner.TranslateDMAAddress(msg.BufferAddresses.C1Pointer), pitch * EncodedHeight / 2) : null;
                var dumpDirectory = owner.FrameDumpDirectory;
                if(!string.IsNullOrEmpty(dumpDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(dumpDirectory);
                        using(var fp = File.OpenWrite(Path.Combine(dumpDirectory, $"ch{Uid:d4}_frame{FrameIndex:d4}.nv12")))
                        {
                            fp.Write(luma, 0, luma.Length);
                            if(chroma != null)
                            {
                                fp.Write(chroma, 0, chroma.Length);
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        owner.ErrorLog("Failed to dump frame for channel {0}: {1}", Uid, e);
                    }
                }
                var isIntra = FrameIndex == 0;
                var result = new EncodeOneFrameFeedback
                {
                    ChanUid = Uid,
                    StreamBufferIndex = FrameIndex,
                    UserParam = msg.Parameters.UserParam,
                    SrcHandle = msg.Parameters.SrcHandle,
                    DpbOutputDelay = 4,
                    NumColumns = 1,
                    NumRows = 1,
                    Qp = 34,
                    StreamPartOffset = 0xc07d00,
                    NumParts = 1,
                    SliceType = isIntra ? SliceType.I : SliceType.P,
                    IsFirstSlice = true,
                    IsLastSlice = true,
                    PpsQp = 0x1a,
                    Poc = (int)FrameIndex * 2,
                    EncodedWidth = EncodedWidth,
                    EncodedHeight = EncodedHeight,
                    BetaOffset = -1,
                    TcOffset = -1,
                    TemporalMVP = true,
                    PictureSize = isIntra ? 0x4000 : 0x100,
                };
                if(FrameIndex < streamBuffers.Count)
                {
                    var addr = owner.TranslateDMAAddress(streamBuffers[(int)FrameIndex].BusAddress);
                    WriteFakeBitstream(addr, result.PictureSize, isIntra);
                    var part = new StreamPartition
                    {
                        Offset = 0,
                        Size = result.PictureSize,
                    };
                    owner.WriteStruct(addr + result.StreamPartOffset, part);
                }
                else
                {
                    owner.WarningLog("[ch{0}] No stream buffer for frame index {1}, have [0; {2})", Uid, FrameIndex, streamBuffers.Count);
                }
                FrameIndex++;
                FrameRendered?.Invoke(chroma?.Prepend(luma).ToArray() ?? luma);
                return result;
            }

            public void Reset()
            {
            }

            public uint FrameIndex { get; private set; }

            // It is not possible to change the parameters at runtime, so use this as a one-time event
            public event Action<int, int, PixelFormat, Endianess> ConfigurationChanged
            {
                add
                {
                    value?.Invoke(EncodedWidth, EncodedHeight, PixelFormat.NV12, Endianess.LittleEndian);
                }

                remove
                {
                }
            }

            public event Action<byte[]> FrameRendered;

            public readonly uint Uid;
            public readonly ushort EncodedWidth;
            public readonly ushort EncodedHeight;
            public readonly int SourceBitDepth;

            // TODO: Use real encoded bitstream
            private void WriteFakeBitstream(ulong address, int size, bool isIntra)
            {
                var data = new byte[size];
                data[0] = 0x00;
                data[1] = 0x00;
                data[2] = 0x00;
                data[3] = 0x01;
                // I-Frame: nal_ref_idc(3)|nal_unit_type(5=Coded slice of an IDR picture) = 0110 0101
                // P-Frame: nal_ref_idc(2)|nal_unit_type(1=Coded slice of a non-IDR picture) = 0100 0001
                data[4] = isIntra ? (byte)0x65 : (byte)0x41;
                data[5] = 0x80;
                for(var i = 6; i < size; i++)
                {
                    data[i] = 0xFF;
                }
                owner.sysbus.WriteBytes(data, address);
            }

            private readonly Allegro_E310 owner;
            private readonly List<PutStreamBufferMsg> streamBuffers = new List<PutStreamBufferMsg>();
        }

        private class Mailbox
        {
            public Mailbox(Allegro_E310 owner, string name, long offset, Action onTailWrite = null)
            {
                Data = new byte[Size];

                ((Registers)(offset + (long)MailboxRegisters.Head)).Define(owner)
                    .WithValueField(0, 32, out head, name: $"{name}Head")
                ;

                ((Registers)(offset + (long)MailboxRegisters.Tail)).Define(owner)
                    .WithValueField(0, 32, out tail, name: $"{name}Tail",
                        writeCallback: (_, __) => onTailWrite?.Invoke())
                ;

                var dataRegsCount = Size / 4;
                ((Registers)(offset + (long)MailboxRegisters.Data)).DefineMany(owner, dataRegsCount, (r, i) =>
                    r.WithValueField(0, 32,
                        valueProviderCallback: _ => ReadData(i * 4),
                        writeCallback: (_, val) => WriteData(i * 4, (uint)val)
                    ))
                ;
            }

            public void Reset()
            {
                // Registers reset by owner's register collection reset
                Array.Clear(Data, 0, (int)Size);
            }

            public Message ReadMessage()
            {
                if(Empty)
                {
                    return null;
                }

                var h = (uint)head.Value;
                var t = (uint)tail.Value;

                var headerBytes = ReadBytes(h, MessageHeaderSize);
                var header = Packet.Decode<MessageHeader>(headerBytes);
                h = (h + MessageHeaderSize) % Size;

                var body = ReadBytes(h, header.BodySize);
                h = (h + header.BodySize).AlignUpToMultipleOf(4) % Size;

                head.Value = h;

                return new Message(header, body);
            }

            public bool TryWriteMessage(MessageId messageId, byte[] body)
            {
                var h = (uint)head.Value;
                var t = (uint)tail.Value;

                var bodySize = body.Length.AlignUpToMultipleOf(4);
                var usedSize = (t >= h) ? (t - h) : (Size + t - h);
                var freeSize = Size - usedSize;

                if(bodySize + MessageHeaderSize > freeSize)
                {
                    return false;
                }

                var header = new MessageHeader
                {
                    MessageId = messageId,
                    BodySize = (ushort)body.Length,
                };
                var headerBytes = Packet.Encode(header);

                t += WriteBytes(t, headerBytes);
                t += WriteBytes(t, body);

                var paddingBytes = bodySize - body.Length;
                t += WritePadding(t, paddingBytes);

                tail.Value = t % Size;
                return true;
            }

            public bool Empty => tail.Value == head.Value;

            public const uint Size = 0x800 - 0x8;

            private byte[] ReadBytes(uint offset, uint count)
            {
                var buffer = new byte[count];
                for(var i = 0; i < count; i++)
                {
                    buffer[i] = Data[(offset + i) % Size];
                }
                return buffer;
            }

            private uint WriteBytes(uint offset, byte[] buffer)
            {
                for(var i = 0; i < buffer.Length; i++)
                {
                    Data[(offset + i) % Size] = buffer[i];
                }
                return (uint)buffer.Length;
            }

            private uint WritePadding(uint offset, int count)
            {
                for(var i = 0; i < count; i++)
                {
                    Data[(offset + i) % Size] = 0;
                }
                return (uint)count;
            }

            private uint ReadData(int offset)
            {
                if(offset + 4 > Data.Length) return 0;
                return BitConverter.ToUInt32(Data, offset);
            }

            private void WriteData(int offset, uint value)
            {
                if(offset + 4 > Data.Length) return;
                var bytes = BitConverter.GetBytes(value);
                Array.Copy(bytes, 0, Data, offset, 4);
            }

            private readonly byte[] Data;
            private readonly IValueRegisterField head;
            private readonly IValueRegisterField tail;
        }

        private class Message
        {
            public Message(MessageHeader header, byte[] body)
            {
                this.Header = header;
                this.Body = body;
            }

            public readonly MessageHeader Header;
            public readonly byte[] Body;
        }

        // Fields in the following Packet structs will be assigned reflexively.
        // Do not put any non-Packet structs or classes below here.
#pragma warning disable 649

        [LeastSignificantByteFirst, Width(bytes: MessageHeaderSize)]
        private struct MessageHeader
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bits: 0), Width(bits: 16)]
            public ushort BodySize;
            [PacketField, Offset(bits: 16), Width(bits: 16)]
            public MessageId MessageId;
        }

        [LeastSignificantByteFirst]
        private struct McuInitMsg : ICommand
        {
            public override string ToString() => this.ToDebugString();

            public IFeedback Execute(Allegro_E310 owner)
            {
                return new McuInitFeedback { ChanUid = ChanUid };
            }

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;

            [PacketField, Offset(bytes: 4)]
            public uint AddrV;

            [PacketField, Offset(bytes: 8)]
            public uint AddrP;

            [PacketField, Offset(bytes: 12)]
            public uint Size;

            [PacketField, Offset(bytes: 16)]
            public uint L2SizeInBits;

            [PacketField, Offset(bytes: 20)]
            public uint L2ColorBitdepth;

            [PacketField, Offset(bytes: 24)]
            public uint NumCores;

            [PacketField, Offset(bytes: 28)]
            public uint CoreFrequency;

            [PacketField, Offset(bytes: 32)]
            public uint PluginEnabled;
        }

        [LeastSignificantByteFirst]
        private struct McuInitFeedback : IFeedback
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;
        }

        [LeastSignificantByteFirst, Width(bytes: 16)]
        private struct McuGetMsg : ICommand, IFeedback
        {
            public override string ToString() => this.ToDebugString();

            public IFeedback Execute(Allegro_E310 owner)
            {
                var res = new McuGetMsg { Param = Param, UserUid = UserUid };
                if(Param == AlMcuParam.SchedulerVersion)
                {
                    res.ParamValue = EncoderVersion;
                }
                else
                {
                    owner.WarningLog("Received Get request for unhandled parameter {0} ({1}), returning 0", Param, (uint)Param);
                }
                return res;
            }

            [PacketField, Offset(bytes: 0)]
            public uint UserUid;

            [PacketField, Offset(bytes: 4)]
            public AlMcuParam Param;

            [PacketField, Offset(bytes: 8)]
            public uint ParamValue;
        }

        [LeastSignificantByteFirst, Width(bytes: 20)]
        private struct CreateChannelMsg : ICommand
        {
            public override string ToString() => this.ToDebugString();

            public IFeedback Execute(Allegro_E310 owner)
            {
                var newChannelUid = (uint)Interlocked.Increment(ref owner.nextChannelUid);
                owner.DebugLog("Creating channel {0} for user {1}: {2}", newChannelUid, UserUid, this);
                var newChannel = new Channel(owner, newChannelUid, this);
                var res = new CreateChannelFeedback
                {
                    ChanUid = newChannelUid,
                    UserUid = UserUid,
                    BuffersNeeded = GetBuffersNeeded(newChannel),
                    ErrorCode = 0,
                };
                owner.channels.TryAdd(newChannelUid, newChannel); // safe as the ID was not reused
                return res;
            }

            [PacketField, Offset(bytes: 0)]
            public uint UserUid;

            [PacketField, Offset(bytes: 4)]
            public uint ParamPointer;

            [PacketField, Offset(bytes: 8)]
            public uint EP1Pointer;

            [PacketField, Offset(bytes: 12)]
            public uint McuRcPluginAddr;

            [PacketField, Offset(bytes: 16)]
            public uint McuRcPluginSize;
        }

        [LeastSignificantByteFirst]
        private struct ChannelBuffers
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public int IntermediateBufferCount;

            [PacketField, Offset(bytes: 4)]
            public int IntermediateBufferSize;

            [PacketField, Offset(bytes: 8)]
            public int ReconstructedBufferCount;

            [PacketField, Offset(bytes: 12)]
            public int ReconstructedBufferSize;

            [PacketField, Offset(bytes: 16)]
            public int Reserved;
        }

        [LeastSignificantByteFirst]
        private struct CreateChannelFeedback : IFeedback
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;

            [PacketField, Offset(bytes: 4)]
            public uint UserUid;

            [PacketField, Offset(bytes: 8), Width(bytes: 20)]
            public ChannelBuffers BuffersNeeded;

            [PacketField, Offset(bytes: 28)]
            public uint ErrorCode;
        }

        [LeastSignificantByteFirst, Width(bytes: 4)]
        private struct DestroyChannelMsg : ICommand
        {
            public override string ToString() => this.ToDebugString();

            public IFeedback Execute(Allegro_E310 owner)
            {
                owner.DebugLog("Destroying channel {0}", ChanUid);
                if(owner.channels.TryRemove(ChanUid, out _))
                {
                    return new DestroyChannelFeedback { ChanUid = ChanUid };
                }
                return null;
            }

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;
        }

        [LeastSignificantByteFirst]
        private struct DestroyChannelFeedback : IFeedback
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;
        }

        private struct NoOperationMsg : ICommand
        {
            public IFeedback Execute(Allegro_E310 owner)
            {
                return null;
            }
        }

        // Variable length!
        [LeastSignificantByteFirst]
        private struct EncodeOneFrameMsg : ICommand
        {
            public override string ToString() => this.ToDebugString();

            public IFeedback Execute(Allegro_E310 owner)
            {
                if(owner.channels.TryGetValue(ChanUid, out var encChannel))
                {
                    owner.InfoLog("Received ENCODE_ONE_FRM for channel {0}: {1}", ChanUid, this);
                    var that = this;
                    // TODO: Remove delay (handle encode / push stream in any order?)
                    owner.ExecuteWithDelay(() => owner.SendMessage(MessageId.EncodeOneFrame, encChannel.EncodeFrame(that)));
                }
                else
                {
                    owner.WarningLog("Received ENCODE_ONE_FRM for nonexistent channel {0}: {1}", ChanUid, this);
                }
                return null; // Feedback will be sent with a delay
            }

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;

            // padding

            [PacketField, Offset(bytes: 8)]
            public EncodePictureParameters Parameters; // Variable length field

            [PacketField, Align(doubleWords: 1)]
            public EncodePictureBufferAddresses BufferAddresses;
        }

        [LeastSignificantByteFirst, Width(bytes: 284)]
        private struct EncodeOneFrameFeedback : IFeedback
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;

            [PacketField, Offset(bytes: 4)]
            public ulong StreamBufferIndex; // actually frame index

            [PacketField, Offset(bytes: 12)]
            public ulong UserParam;

            [PacketField, Offset(bytes: 20)]
            public ulong SrcHandle;

            [PacketField, Offset(bytes: 28)]
            public bool Skip;

            [PacketField, Offset(bytes: 29)]
            public bool IsRef;

            [PacketField, Offset(bytes: 32)]
            public uint InitialRemovalDelay;

            [PacketField, Offset(bytes: 36)]
            public uint DpbOutputDelay;

            [PacketField, Offset(bytes: 40)]
            public uint Size;

            [PacketField, Offset(bytes: 44)]
            public uint FrmTagSize;

            [PacketField, Offset(bytes: 48)]
            public int Stuffing;

            [PacketField, Offset(bytes: 52)]
            public int Filler;

            [PacketField, Offset(bytes: 56)]
            public ushort NumColumns;

            [PacketField, Offset(bytes: 58)]
            public ushort NumRows;

            [PacketField, Offset(bytes: 60)]
            public short Qp;

            [PacketField, Offset(bytes: 62)]
            public byte NumRefIdxL0;

            [PacketField, Offset(bytes: 63)]
            public byte NumRefIdxL1;

            [PacketField, Offset(bytes: 64)]
            public uint StreamPartOffset;

            [PacketField, Offset(bytes: 68)]
            public int NumParts;

            [PacketField, Offset(bytes: 72)]
            public uint SumCplx;

            [PacketField, Offset(bytes: 76), Width(elements: NumberOfCores)]
            public int[] TileWidth;

            [PacketField, Offset(bytes: 92), Width(elements: MaxRowsPerTile)]
            public int[] TileHeight;

            [PacketField, Offset(bytes: 180)]
            public ErrorCode ErrorCode;

            [PacketField, Offset(bytes: 184)]
            public SliceType SliceType;

            [PacketField, Offset(bytes: 188)]
            public PictureStructure PictureStructure;

            [PacketField, Offset(bytes: 192)]
            public bool IsIDR;

            [PacketField, Offset(bytes: 193)]
            public bool IsFirstSlice;

            [PacketField, Offset(bytes: 194)]
            public bool IsLastSlice;

            [PacketField, Offset(bytes: 196)]
            public short PpsQp;

            [PacketField, Offset(bytes: 200)]
            public int RecoveryCount;

            [PacketField, Offset(bytes: 204)]
            public byte TempId;

            [PacketField, Offset(bytes: 208)]
            public int Poc;

            [PacketField, Offset(bytes: 212)]
            public ushort EncodedWidth;

            [PacketField, Offset(bytes: 214)]
            public ushort EncodedHeight;

            [PacketField, Offset(bytes: 216)]
            public byte CuQpDeltaDepth;

            [PacketField, Offset(bytes: 217)]
            public byte DisLoopFilter;

            [PacketField, Offset(bytes: 218)]
            public sbyte BetaOffset;

            [PacketField, Offset(bytes: 219)]
            public sbyte TcOffset;

            [PacketField, Offset(bytes: 220)]
            public bool TemporalMVP;

            [PacketField, Offset(bytes: 224)]
            public int PictureSize;

            [PacketField, Offset(bytes: 228), Width(elements: 5)]
            public sbyte[] PercentIntra;

            // rate control statistics {
            [PacketField, Offset(bytes: 236)]
            public uint NumLcus;

            [PacketField, Offset(bytes: 240)]
            public uint NumBytes;

            [PacketField, Offset(bytes: 244)]
            public uint NumBins;

            [PacketField, Offset(bytes: 248)]
            public uint NumIntra;

            [PacketField, Offset(bytes: 252)]
            public uint NumSkip;

            [PacketField, Offset(bytes: 256)]
            public uint NumCu8x8;

            [PacketField, Offset(bytes: 260)]
            public uint NumCu16x16;

            [PacketField, Offset(bytes: 264)]
            public uint NumCu32x32;

            [PacketField, Offset(bytes: 268)]
            public int SumQp;

            [PacketField, Offset(bytes: 272)]
            public short MinQp;

            [PacketField, Offset(bytes: 274)]
            public short MaxQp;

            // }

            [PacketField, Offset(bytes: 276)]
            public ushort GdrPos;

            [PacketField, Offset(bytes: 280)]
            public GradualDecodingRefreshMode GradualDecodingRefreshMode;
        }

        [LeastSignificantByteFirst]
        private struct StreamPartition
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public uint Offset;

            [PacketField, Offset(bytes: 4)]
            public int Size;
        }

        [LeastSignificantByteFirst]
        private struct EncodePictureParameters
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public PictureEncodingOption EncodingOptions;

            [PacketField, Offset(bytes: 4)]
            public byte PpsId;

            // padding

            [PacketField, Offset(bytes: 6)]
            public short PpsQp;

            // padding

            // lookahead param {
            [PacketField, Offset(bytes: 8)]
            public int ScPictureSize;

            [PacketField, Offset(bytes: 12)]
            public int SciPictureRatio;

            [PacketField, Offset(bytes: 16)]
            public short Complexity;

            [PacketField, Offset(bytes: 18)]
            public short TargetLevel;
            // }

            // padding

            [PacketField, Offset(bytes: 24)]
            public ulong UserParam;

            [PacketField, Offset(bytes: 32)]
            public ulong SrcHandle;

            [PacketField, Offset(bytes: 40)]
            public sbyte Qp1Offset;

            [PacketField, Offset(bytes: 41)]
            public sbyte Qp2Offset;

            // padding

            [PacketField, Offset(bytes: 48)]
            public EncodingRequestOption RequestOptions;

            // Variable length data based on RequestOptions, all fields optional
            // omitted fields take 0 space shifting the layout of the rest

            [PacketField, Align(doubleWords: 1), PresentIf(nameof(HasSceneChangeDelay))]
            public uint? SceneChangeDelay;

            [PacketField, Align(doubleWords: 1), PresentIf(nameof(HasRcGopParameters))]
            public RateControlGopParameters? RateControlGopParameters;

            [PacketField, Align(doubleWords: 1), PresentIf(nameof(HasSetQp))]
            public short? Qp;

            [PacketField, Align(doubleWords: 1), PresentIf(nameof(HasInputResolution))]
            public Dimension? InputResolution;

            [PacketField, Align(doubleWords: 1), PresentIf(nameof(HasInputResolution))]
            public sbyte? LfBetaOffset;

            [PacketField, Align(doubleWords: 1), PresentIf(nameof(HasInputResolution))]
            public sbyte? LfTcOffset;

            public bool HasSceneChangeDelay => RequestOptions.HasFlag(EncodingRequestOption.SceneChange);

            public bool HasRcGopParameters => RequestOptions.HasFlag(EncodingRequestOption.UpdateRcGopParameters);

            public bool HasSetQp => RequestOptions.HasFlag(EncodingRequestOption.SetQp);

            public bool HasInputResolution => RequestOptions.HasFlag(EncodingRequestOption.SetInputResolution);

            public bool HasLfOffsets => RequestOptions.HasFlag(EncodingRequestOption.SetLfOffsets);
        }

        [LeastSignificantByteFirst, Width(bytes: 80)]
        private struct RateControlParameters
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public RateControlMode Mode;

            [PacketField, Offset(bytes: 4)]
            public uint InitialRemDelay;

            [PacketField, Offset(bytes: 8)]
            public uint CPBSize;

            [PacketField, Offset(bytes: 12)]
            public ushort FrameRate;

            [PacketField, Offset(bytes: 14)]
            public ushort ClkRatio;

            [PacketField, Offset(bytes: 16)]
            public uint TargetBitRate;

            [PacketField, Offset(bytes: 20)]
            public uint MaxBitRate;

            [PacketField, Offset(bytes: 24)]
            public uint MaxConsecSkip;

            [PacketField, Offset(bytes: 28)]
            public short InitialQP;

            [PacketField, Offset(bytes: 30), Width(elements: NumberOfFrameTypes)]
            public short[] MinQP;

            [PacketField, Offset(bytes: 36), Width(elements: NumberOfFrameTypes)]
            public short[] MaxQP;

            [PacketField, Offset(bytes: 42)]
            public short IPDelta;

            [PacketField, Offset(bytes: 44)]
            public short PBDelta;

            [PacketField, Offset(bytes: 46)]
            public bool UseGoldenRef;

            [PacketField, Offset(bytes: 48)]
            public short PGoldenDelta;

            [PacketField, Offset(bytes: 50)]
            public short GoldenRefFrequency;

            [PacketField, Offset(bytes: 52)]
            public RateControlOption Options;

            [PacketField, Offset(bytes: 56)]
            public uint NumPel;

            [PacketField, Offset(bytes: 60)]
            public ushort MinPSNR;

            [PacketField, Offset(bytes: 62)]
            public ushort MaxPSNR;

            [PacketField, Offset(bytes: 64)]
            public ushort MaxPelVal;

            [PacketField, Offset(bytes: 68), Width(elements: NumberOfFrameTypes)]
            public uint[] MaxPictureSize;
        }

        [LeastSignificantByteFirst, Width(bytes: 32)]
        private struct GroupOfPicturesParameters
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public GroupOfPicturesControlMode Mode;

            [PacketField, Offset(bytes: 4)]
            public ushort GopLength;

            [PacketField, Offset(bytes: 6)]
            public byte NumB;

            [PacketField, Offset(bytes: 7)]
            public byte FreqGoldenRef;

            [PacketField, Offset(bytes: 8)]
            public uint FreqIDR;

            [PacketField, Offset(bytes: 12)]
            public bool EnableLT;

            [PacketField, Offset(bytes: 13)]
            public bool WriteAvcHdrSvcExt;

            [PacketField, Offset(bytes: 16)]
            public uint FreqLT;

            [PacketField, Offset(bytes: 20)]
            public GradualDecodingRefreshMode GdrMode;

            [PacketField, Offset(bytes: 24)]
            public uint FreqRP;

            [PacketField, Offset(bytes: 28), Width(elements: 4)]
            public sbyte[] TempDQP;
        }

        [LeastSignificantByteFirst, Width(bytes: 112)]
        private struct RateControlGopParameters
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public RateControlParameters Rc;

            [PacketField, Offset(bytes: 80)]
            public GroupOfPicturesParameters Gop;
        }

        [LeastSignificantByteFirst, Width(bytes: 8)]
        private struct Dimension
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public int Width;

            [PacketField, Offset(bytes: 0)]
            public int Height;
        }

        [LeastSignificantByteFirst, Width(bytes: 32)]
        private struct EncodePictureBufferAddresses
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public uint YPointer;

            [PacketField, Offset(bytes: 4)]
            public uint C1Pointer;

            [PacketField, Offset(bytes: 8)]
            public uint Pitch;

            [PacketField, Offset(bytes: 12)]
            public byte BitDepth;

            // padding

            [PacketField, Offset(bytes: 16)]
            public uint EP2Pointer;

            // padding

            [PacketField, Offset(bytes: 24)]
            public ulong EP2Pointer64;
        }

        [LeastSignificantByteFirst, Width(bytes: 32)]
        private struct PutStreamBufferMsg : ICommand // al5_buffer
        {
            public override string ToString() => this.ToDebugString();

            public IFeedback Execute(Allegro_E310 owner)
            {
                if(owner.channels.TryGetValue(ChanUid, out var bufChannel))
                {
                    owner.InfoLog("Received PUT_STREAM_BUFFER for channel {0}: {1}", ChanUid, this);
                    bufChannel.PutStreamBuffer(this);
                }
                else
                {
                    owner.WarningLog("Received PUT_STREAM_BUFFER for nonexistent channel {0}: {1}", ChanUid, this);
                }
                return null; // No response
            }

            [PacketField, Offset(bytes: 0)]
            public uint ChanUid;

            [PacketField, Offset(bytes: 4)]
            public uint BusAddress;

            [PacketField, Offset(bytes: 8)]
            public uint McuVaddr;

            [PacketField, Offset(bytes: 12)]
            public uint Size;

            [PacketField, Offset(bytes: 16)]
            public uint Offset; // header size + SEI size

            [PacketField, Offset(bytes: 20)]
            public ulong Index;

            [PacketField, Offset(bytes: 28)]
            public uint ExternalMcuMvVaddr;
        }

        [LeastSignificantByteFirst, Width(bytes: 304)]
        private struct EncodeChannelParameters // AL_TEncChanParam
        {
            public override string ToString() => this.ToDebugString();

            [PacketField, Offset(bytes: 0)]
            public int LayerID;

            [PacketField, Offset(bytes: 4)]
            public ushort EncodedWidth;

            [PacketField, Offset(bytes: 6)]
            public ushort EncodedHeight;

            [PacketField, Offset(bytes: 8)]
            public ushort SourceWidth;

            [PacketField, Offset(bytes: 10)]
            public ushort SourceHeight;

            [PacketField, Offset(bytes: 12)]
            public bool EnableSourceCrop;

            [PacketField, Offset(bytes: 14)]
            public ushort SourceCropWidth;

            [PacketField, Offset(bytes: 16)]
            public ushort SourceCropHeight;

            [PacketField, Offset(bytes: 18)]
            public ushort SourceCropPosX;

            [PacketField, Offset(bytes: 20)]
            public ushort SourceCropPosY;

            [PacketField, Offset(bytes: 24)]
            public VideoMode VideoMode;

            [PacketField, Offset(bytes: 28)]
            public PictureFormat PictureFormat;

            [PacketField, Offset(bytes: 32)]
            public bool VideoFullRange;

            [PacketField, Offset(bytes: 36)]
            public SourceMode SourceMode;

            [PacketField, Offset(bytes: 40)]
            public byte SourceBitDepth;

            [PacketField, Offset(bytes: 44)]
            public Profile Profile;

            [PacketField, Offset(bytes: 48)]
            public byte Level;

            [PacketField, Offset(bytes: 49)]
            public byte Tier;

            [PacketField, Offset(bytes: 52)]
            public uint SpsParam;

            [PacketField, Offset(bytes: 56)]
            public uint PpsParam;

            [PacketField, Offset(bytes: 60)]
            public bool ForcePpsIdToZero;

            [PacketField, Offset(bytes: 64)]
            public ChannelEncodingOption EncodingOptions;

            [PacketField, Offset(bytes: 68)]
            public ChannelEncodingTool EncodingTools;

            [PacketField, Offset(bytes: 72)]
            public sbyte BetaOffset;

            [PacketField, Offset(bytes: 73)]
            public sbyte TcOffset;

            [PacketField, Offset(bytes: 74)]
            public sbyte CbSliceQpOffset;

            [PacketField, Offset(bytes: 75)]
            public sbyte CrSliceQpOffset;

            [PacketField, Offset(bytes: 76)]
            public sbyte CbPicQpOffset;

            [PacketField, Offset(bytes: 77)]
            public sbyte CrPicQpOffset;

            [PacketField, Offset(bytes: 78)]
            public byte WeightedPred;

            [PacketField, Offset(bytes: 79)]
            public bool Direct8x8Infer;

            [PacketField, Offset(bytes: 80)]
            public byte CuQPDeltaDepth;

            [PacketField, Offset(bytes: 81)]
            public byte CabacInitIdc;

            [PacketField, Offset(bytes: 82)]
            public byte NumCore;

            [PacketField, Offset(bytes: 84)]
            public ushort SliceSize;

            [PacketField, Offset(bytes: 86)]
            public ushort NumSlices;

            [PacketField, Offset(bytes: 88)]
            public uint L2PrefetchMemOffset;

            [PacketField, Offset(bytes: 92)]
            public uint L2PrefetchMemSize;

            [PacketField, Offset(bytes: 96)]
            public bool EnableL2PReducedRange;

            [PacketField, Offset(bytes: 98)]
            public ushort ClipHrzRange;

            [PacketField, Offset(bytes: 100)]
            public ushort ClipVrtRange;

            [PacketField, Offset(bytes: 102), Width(elements: 2 * 2)]
            public short[] MotionEstimationRange; // TODO: 2-dimensional array

            [PacketField, Offset(bytes: 110)]
            public byte Log2MaxCuSize;

            [PacketField, Offset(bytes: 111)]
            public byte Log2MinCuSize;

            [PacketField, Offset(bytes: 112)]
            public byte Log2MaxTuSize;

            [PacketField, Offset(bytes: 113)]
            public byte Log2MaxTuSkipSize;

            [PacketField, Offset(bytes: 114)]
            public byte Log2MinTuSize;

            [PacketField, Offset(bytes: 115)]
            public byte MaxTransfoDepthIntra;

            [PacketField, Offset(bytes: 116)]
            public byte MaxTransfoDepthInter;

            [PacketField, Offset(bytes: 117)]
            public bool StrongIntraSmooth;

            [PacketField, Offset(bytes: 120)]
            public bool UseCabacEntropy; // otherwise CAVLC

            [PacketField, Offset(bytes: 124)]
            public RateControlParameters RateControlParameters;

            [PacketField, Offset(bytes: 204)]
            public GroupOfPicturesParameters GroupOfPicturesParameters;

            [PacketField, Offset(bytes: 236)]
            public bool NonRealtime;

            [PacketField, Offset(bytes: 237)]
            public bool SubframeLatency;

            [PacketField, Offset(bytes: 240)]
            public LambdaControlMode LambdaControlMode;

            [PacketField, Offset(bytes: 244), Width(elements: 6)]
            public int[] LambdaFactors;

            [PacketField, Offset(bytes: 268)]
            public ushort MVVRange;

            [PacketField, Offset(bytes: 270)]
            public sbyte MaxNumMergeCand;

            [PacketField, Offset(bytes: 272)]
            public uint RcPluginDmaSize;

            [PacketField, Offset(bytes: 280)]
            public ulong RcPluginDmaContext;

            [PacketField, Offset(bytes: 288)]
            public bool EnableOutputCrop;

            [PacketField, Offset(bytes: 290)]
            public ushort OutputCropWidth;

            [PacketField, Offset(bytes: 292)]
            public ushort OutputCropHeight;

            [PacketField, Offset(bytes: 294)]
            public ushort OutputCropPosX;

            [PacketField, Offset(bytes: 296)]
            public ushort OutputCropPosY;

            [PacketField, Offset(bytes: 298)]
            public bool UseUniformSliceType;

            [PacketField, Offset(bytes: 300)]
            public StartCodeByteAlignment StartCodeByteAlignment;
        }

        private enum AlMcuParam : uint
        {
            SchedulerVersion = 0, // enc/dec
            SchedulerCore = 1, // dec
            SchedulerChannelTraceCallback = 2, // dec
        }

        [Flags]
        private enum PictureEncodingOption : uint
        {
            UseQpTable = 0x0001,
            ForceLoad = 0x0002,
            UseL2 = 0x0004,
            DisableIntra = 0x0008,
            DependentSlices = 0x0010,
        }

        [Flags]
        private enum EncodingRequestOption : uint
        {
            None = 0x00000,
            SceneChange = 0x00001, // u32 SceneChangeDelay
            IsLongTerm = 0x00002, // ?
            UseLongTerm = 0x00004, // ?
            RestartGop = 0x00008, // ?
            UpdateRcGopParameters = 0x00010, // 2 huge structs (AL_TRCParam, AL_TGopParam)
            UpdateCostMode = 0x01000, // ?
            SetQp = 0x00100, // i16
            SetInputResolution = 0x00200, // i32 Width, i32 Height
            SetLfOffsets = 0x00400, // i8 LfBetaOffset, i8 LfTcOffset
            SetAutoQp = 0x08000, // ?
            UpdateAutoQpValues = 0x20000, // ?
            SetQpOffset = 0x40000, // ?
            RecoveryPoint = 0x10000, // ?
        }

        private enum RateControlMode : uint
        {
            ConstantQp = 0x00,
            ConstantBitrate = 0x01,
            VariableBitrate = 0x02,
            LowLatency = 0x03,
            CappedVariableBitrate = 0x04,
            Bypass = 0x3F,
            Plugin = 0x40,
        }

        [Flags]
        private enum RateControlOption : uint
        {
            None = 0x00,
            SceneChangeReserved = 0x01, // reserved
            Delayed = 0x02,
            StaticScene = 0x04,
            EnableSkip = 0x08,
            SceneChangePrevention = 0x10,
        }

        [Flags]
        private enum GroupOfPicturesControlMode : uint
        {
            BOnly = 0x01,
            Default = 0x02,
            Pyramidal = 0x04,
            LowDelay = 0x08,
            DefaultB = Default | BOnly,
            PyramidalB = Pyramidal | BOnly,
            LowDelayP = LowDelay | Pyramidal,
            LowDelayB = LowDelay | BOnly,
            Adaptive = 0x10,
            Bypass = 0x20,
        }

        [Flags]
        private enum GradualDecodingRefreshMode : uint
        {
            Off = 0x00,
            Horizontal = On | 0x01,
            On = 0x02,
            Vertical = On | 0x00,
        }

        private enum VideoMode : uint
        {
            Progressive = 0,
            InterlacedTopFieldFirst = 1,
            InterlacedBottomFieldFirst = 2,
        }

        private enum PictureFormat : uint
        {
            Sampling400Bits8 = 0x0088,
            Sampling420Bits8 = 0x0188,
            Sampling422Bits8 = 0x0288,
            Sampling444Bits8 = 0x0388,
            Sampling400Bits10 = 0x00AA,
            Sampling420Bits10 = 0x01AA,
            Sampling422Bits10 = 0x02AA,
            Sampling444Bits10 = 0x03AA,
            Sampling400Bits12 = 0x00CC,
            Sampling420Bits12 = 0x01CC,
            Sampling422Bits12 = 0x02CC,
            Sampling444Bits12 = 0x03CC,
        }

        private enum SourceMode : uint
        {
            Raster = 0x0,
            // gap
            Tile64x4 = 0x4,
            Comp64x4 = 0x5,
            Tile32x4 = 0x6,
            Comp32x4 = 0x7,
        }

        private enum Codec : uint
        {
            Avc = 0,
            Hevc = 1,
            Av1 = 2,
            Vp9 = 3,
            Jpeg = 4,
            Vvc = 5,
            Mpeg2 = 6,
            AvcI = 7,
            Lcevc = 8,
        }

        private enum Profile : uint
        {
            Avc = (Codec.Avc << 24),
            AvcCavlc444Intra = Avc | 44, // not supported
            AvcBaseline = Avc | 66,
            AvcMain = Avc | 77,
            AvcExtended = Avc | 88, // not supported
            AvcHigh = Avc | 100,
            AvcHigh10 = Avc | 110,
            AvcHigh422 = Avc | 122,
            AvcHigh444Pred = Avc | 244, // not supported
            AvcCBaseline = AvcBaseline | 0x0002 << 8,
            AvcProgHigh = AvcHigh | 0x0010 << 8,
            AvcCHigh = AvcHigh | 0x0030 << 8,
            AvcHigh10Intra = AvcHigh10 | 0x0008 << 8,
            AvcHigh422Intra = AvcHigh422 | 0x0008 << 8,
            AvcHigh444Intra = AvcHigh444Pred | 0x0008 << 8, // not supported
            XavcHigh10IntraCbg = AvcHigh10 | 0x1008 << 8,
            XavcHigh10IntraVbr = AvcHigh10 | 0x3008 << 8,
            XavcHigh422IntraCbg = AvcHigh422 | 0x1008 << 8,
            XavcHigh422IntraVbr = AvcHigh422 | 0x3008 << 8,
            XavcLongGopMainMp4 = AvcMain | 0x1000 << 8,
            XavcLongGopHighMp4 = AvcHigh | 0x1000 << 8,
            XavcLongGopHighMxf = AvcHigh | 0x5000 << 8,
            XavcLongGopHigh422Mxf = AvcHigh422 | 0x5000 << 8,

            Hevc = (Codec.Hevc << 24),
            HevcMain = Hevc | 1,
            HevcMain10 = Hevc | 2,
            HevcMainStill = Hevc | 3,
            HevcRext = Hevc | 4,
            HevcMono = HevcRext | 0xFC80 << 8,
            HevcMono10 = HevcRext | 0xDC80 << 8,
            HevcMono12 = HevcRext | 0x9C80 << 8,
            HevcMono16 = HevcRext | 0x1C80 << 8, // not supported
            HevcMain12 = HevcRext | 0x9880 << 8,
            HevcMain422 = HevcRext | 0xF080 << 8,
            HevcMain42210 = HevcRext | 0xD080 << 8,
            HevcMain42212 = HevcRext | 0x9080 << 8,
            HevcMain444 = HevcRext | 0xE080 << 8,
            HevcMain44410 = HevcRext | 0xC080 << 8,
            HevcMain44412 = HevcRext | 0x8080 << 8, // not supported
            HevcMainIntra = HevcRext | 0xFA00 << 8,
            HevcMain10Intra = HevcRext | 0xDA00 << 8,
            HevcMain12Intra = HevcRext | 0x9A00 << 8, // not supported
            HevcMain422Intra = HevcRext | 0xF200 << 8,
            HevcMain42210Intra = HevcRext | 0xD200 << 8,
            HevcMain42212Intra = HevcRext | 0x9200 << 8, // not supported
            HevcMain444Intra = HevcRext | 0xE200 << 8,
            HevcMain44410Intra = HevcRext | 0xC200 << 8,
            HevcMain44412Intra = HevcRext | 0x8200 << 8, // not supported
            HevcMain44416Intra = HevcRext | 0x0200 << 8, // not supported
            HevcMain444Still = HevcRext | 0xE300 << 8,
            HevcMain44416Still = HevcRext | 0x0300 << 8, // not supported
            Unknown = uint.MaxValue,
        }

        [Flags]
        private enum ChannelEncodingOption : uint
        {
            None = 0x00000,
            QpTabRelative = 0x00001,
            FixPredictor = 0x00002,
            CustomLda = 0x00004,
            EnableAutoQp = 0x00008,
            AdaptAutoQp = 0x00010,
            ForceRec = 0x00040,
            ForceMvOut = 0x00080,
            LowlatSync = 0x00100,
            LowlatInt = 0x00200,
            HighFreq = 0x02000,
            SceneChangeDetection = 0x04000,
            ForceMvClip = 0x20000,
            RdoCostMode = 0x40000,
        }

        [Flags]
        private enum ChannelEncodingTool : uint
        {
            Wpp = 0x01,
            Tile = 0x02,
            Lf = 0x04,
            LfXSlice = 0x08,
            LfXTile = 0x10,
            SclLst = 0x20,
            ConstIntraPred = 0x40,
            TransfotmSkip = 0x80,
        }

        private enum LambdaControlMode : uint
        {
            Default = 0x00,
            Custom = 0x01,
            Dynamic = 0x02,
            Auto = 0x03,
            Load = 0x80,
        }

        private enum StartCodeByteAlignment : uint
        {
            Auto = 0,
            Bytes3 = 1,
            Bytes4 = 2,
        }

        private enum ErrorCode : uint
        {
            Success = 0, // The operation completed successfully

            WarnConcealDetect = 1, // The decoder had to conceal some errors in the stream
            WarnUnsupportedNal = 2, // NAL has parameters unsupported by decoder
            WarnLcuOverflow = 3, // Some LCU exceed the maximum allowed bits in the stream
            WarnNumSlicesAdjusted = 4, // Number of slices have been adjusted to be hardware compatible
            WarnSpsNotCompatibleWithChannelSettings = 5, // SPS not compatible with channel settings, discarded
            WarnSeiOverflow = 6, // SEI metadata buffer is too small to contains all SEI messages
            WarnResFoundCb = 7, // The resolutionFound callback returned an error
            WarnSpsBitdepthNotCompatibleWithChannelSettings = 8, // SPS bit depth not compatible with channel settings, discarded
            WarnSpsLevelNotCompatibleWithChannelSettings = 9, // SPS level not compatible with channel settings, discarded
            WarnSpsChromaModeNotCompatibleWithChannelSettings = 10, // SPS chroma mode not compatible with channel settings, discarded
            WarnSpsInterlaceNotCompatibleWithChannelSettings = 11, // SPS sequence mode (progressive/interlaced) not compatible with channel settings, discarded
            WarnSpsResolutionNotCompatibleWithChannelSettings = 12, // SPS resolution not compatible with channel settings, discarded
            WarnSpsMinResolutionNotCompatibleWithChannelSettings = 13, // SPS minimal resolution not compatible with channel settings, discarded
            WarnAsoFmoNotSupported = 14, // Arbitrary Slice Order or Flexible Macroblock Reordering features are not supported, discarded
            WarnInvalidAccessUnitStructure = 15, // Found invalid Access Unit structure while decoding in split-input mode
            WarnHwConcealDetect = 16, // The hardware decoder had to conceal some errors in the stream

            Error = 0x80, // Generic error
            ErrNoMemory = 7 | Error, // Couldn't allocate a resource because no (DMA, MCU specific, virtual) memory was left
            ErrStreamOverflow = 8 | Error, // The generated stream couldn't fit inside the allocated stream buffer
            ErrTooManySlices = 9 | Error, // If SliceSize mode is supported, the constraint couldn't be respected as too many slices were required to respect it
            ErrWatchdogTimeout = 11 | Error, // A timeout occurred while processing the request
            ErrChanCreationNoChannelAvailable = 13 | Error, // The scheduler can't handle more channels
            ErrChanCreationResourceUnavailable = 14 | Error, // The processing power of the available cores is insufficient to handle this channel
            ErrChanCreationLoadDistribution = 15 | Error, // Couldn't spread the load on enough cores, or the load can't be spread so much (each core has a requirement on the minimal amount of resources it can handle)
            ErrRequestMalformed = 16 | Error, // Some parameters in the request have an invalid value
            ErrCmdNotAllowed = 17 | Error, // The command is not allowed in this configuration
            ErrInvalidCmdValue = 18 | Error, // The value associated with the command is invalid (in the current configuration)
            ErrChanCreationMixRealtime = 20 | Error, // Couldn't mix realtime and non-realtime channels at the sema time
            ErrCannotOpenFile = 21 | Error, // Failed to open the file
            ErrRoiDisable = 22 | Error, // ROI disabled
            ErrQploadData = 23 | Error, // There are some issues in the QP file
            ErrQploadNotEnoughData = 24 | Error,
            ErrQploadSegmentConfig = 25 | Error,
            ErrQploadQpValue = 26 | Error,
            ErrQploadSegmentIndex = 27 | Error,
            ErrQploadForceFlags = 28 | Error,
            ErrQploadBlkSize = 29 | Error,
            ErrInvalidOrUnsupportedFileFormat = 30 | Error, // Invalid or unsupported file format
            ErrRequestInvalidMinWidth = 31 | Error, // Frame width is below the supported minimum
            ErrRequestInvalidMaxHeight = 32 | Error, // Frame height exceeds the supported maximum
            ErrChanCreationHwCapacityExceeded = 33 | Error, // HW capacity exceeded
        }

        private enum SliceType : uint
        {
            B = 0,  // B Slice (can contain I, P and B blocks)
            P = 1,  // P Slice (can contain I and P blocks)
            I = 2,  // I Slice (can contain I blocks)
            Golden = 3, // Golden Slice
            Sp = 3, // AVC SP Slice
            Si = 4, // AVC SI Slice
            Conceal = 6, // Conceal Slice (slice was concealed)
            Skip = 7, // Skip Slice
            Repeat = 8, // AOM Repeat Slice (repeats the content of its reference)
            Repeat_Post = 9, // AOM Repeat Slice decided post-encoding
        }

        private enum PictureStructure : uint
        {
            Frame = 0,  // Progressive frame
            TopField = 1,  // Single top field
            BottomField = 2,  // Single bottom field
            TopBottomFields = 3,  // Top field then bottom field
            BottomTopFields = 4,  // Bottom field then top field
            TopBottomTopFields = 5,  // T-B-T (3-field sequence)
            BottomTopBottomFields = 6,  // B-T-B (3-field sequence)
            FrameRepeatedTwice = 7,  // Frame displayed twice (X2)
            FrameRepeatedThrice = 8,  // Frame displayed 3 times (X3)
            TopFieldWithPreviousBottom = 9,  // Top field paired with previous bottom field
            BottomFieldWithPreviousTop = 10, // Bottom field paired with previous top field
            TopFieldWithNextBottom = 11, // Top field paired with next bottom field
            BottomFieldWithNextTop = 12, // Bottom field paired with next top field
        }

        private enum MailboxRegisters : long
        {
            Head = 0x00,
            Tail = 0x04,
            Data = 0x08,
        }

        private enum Registers : long
        {
            McuInterruptHandler = 0x0010, // AL5_MCU_INTERRUPT_HANDLER
            CommandMailbox = 0x7000, // MAILBOX_CMD
            StatusMailbox = 0x7800, // MAILBOX_STATUS
            McuReset = 0x9000, // AL5_MCU_RESET
            McuResetMode = 0x9004, // AL5_MCU_RESET_MODE
            McuStatus = 0x9008, // AL5_MCU_STA
            McuWakeup = 0x900c, // AL5_MCU_WAKEUP
            IcacheAddrOffsetMsb = 0x9010, // AL5_ICACHE_ADDR_OFFSET_MSB
            IcacheAddrOffsetLsb = 0x9014, // AL5_ICACHE_ADDR_OFFSET_LSB
            DcacheAddrOffsetMsb = 0x9018, // AL5_DCACHE_ADDR_OFFSET_MSB
            DcacheAddrOffsetLsb = 0x901c, // AL5_DCACHE_ADDR_OFFSET_LSB
            McuInterruptTrigger = 0x9100, // AL5_MCU_INTERRUPT
            McuInterruptMask = 0x9104, // AL5_MCU_INTERRUPT_MASK
            McuInterruptClear = 0x9108, // AL5_MCU_INTERRUPT_CLR
            McuInterruptStatus = 0x910C, // AL5_MCU_IRQ_STA
            AxiAddressOffset = 0x9208, // AXI_ADDR_OFFSET_IP
        }

        private enum MessageId : ushort
        {
            Initialise = 0, // AL_MCU_MSG_INIT
            Deinitialise = 1, // AL_MCU_MSG_DEINIT
            IpInterrupt = 2, // AL_MCU_MSG_IP_INT
            CpuInterrupt = 3, // AL_MCU_MSG_CPU_INT
            CpuResponse = 4, // AL_MCU_MSG_CPU_RSP
            CreateChannel = 5, // AL_MCU_MSG_CREATE_CHANNEL
            DestroyChannel = 6, // AL_MCU_MSG_DESTROY_CHANNEL
            EncodeOneFrame = 7, // AL_MCU_MSG_ENCODE_ONE_FRM
            DecodeOneFrame = 8, // AL_MCU_MSG_DECODE_ONE_FRM
            SearchStartCode = 9, // AL_MCU_MSG_SEARCH_START_CODE
            DestroyChannelQuiet = 10, // AL_MCU_MSG_QUIET_DESTROY_CHANNEL
            GetResource = 11, // AL_MCU_MSG_GET_RESOURCE
            SetBuffer = 12, // AL_MCU_MSG_SET_BUFFER
            Shutdown = 13, // AL_MCU_MSG_SHUTDOWN
            PushIntermediateBuffer = 14, // AL_MCU_MSG_PUSH_BUFFER_INTERMEDIATE
            PushReferenceBuffer = 15, // AL_MCU_MSG_PUSH_BUFFER_REFERENCE
            GetReconstructedPicture = 16, // AL_MCU_MSG_GET_RECONSTRUCTED_PICTURE
            ReleaseReconstructedPicture = 17, // AL_MCU_MSG_RELEASE_RECONSTRUCTED_PICTURE
            PutStreamBuffer = 18, // AL_MCU_MSG_PUT_STREAM_BUFFER
            Trace = 19, // AL_MCU_MSG_TRACE
            RandomTest = 20, // AL_MCU_MSG_RANDOM_TEST
            SetTimerBuffer = 21, // AL_MCU_MSG_SET_TIMER_BUFFER
            SetIrqTimerBuffer = 22, // AL_MCU_MSG_SET_IRQ_TIMER_BUFFER
            DecodeOneSlice = 23, // AL_MCU_MSG_DECODE_ONE_SLICE
            GetMcuParameter = 24, // AL_MCU_MSG_GET
            SetMcuParameter = 25, // AL_MCU_MSG_SET
        }

        private interface ICommand
        {
            IFeedback Execute(Allegro_E310 owner);
        }

        private interface IFeedback
        {
        }
    }
}
