//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    // NOTE: Do not use this class for USB controller peripherals a CPU will talk to. Implement `IUSBPipe*` yourself. This class is only for USB devices that exist entirely within C# - `USBPendrive` and the like
    public class USBEndpoint : DescriptorProvider, IUSBPipeWrite, IUSBPipeRead
    {
        public USBEndpoint(IUSBDevice device,
                           byte identifier,
                           Direction direction,
                           EndpointTransferType transferType,
                           short maximumPacketSize,
                           byte interval) : base(7, (byte)DescriptorType.Endpoint)
        {
            this.device = device;

            Identifier = identifier;
            Direction = direction;
            TransferType = transferType;
            MaximumPacketSize = maximumPacketSize;
            Interval = interval;

            packetCreator = new PacketCreator(DeviceWrite);
        }

        public event Action<byte[]> DeviceGotWriteFromHost
        {
            add
            {
                if(Direction != Direction.HostToDevice)
                {
                    throw new ArgumentException("Reading from this descriptor is not supported");
                }
                DataWrittenInner += value;
            }

            remove
            {
                DataWrittenInner -= value;
            }
        }

        public void DeviceWrite(ICollection<byte> data)
        {
            device.Log(LogLevel.Noisy, "Handling data packet of size: {0}", data.Count);
#if DEBUG_PACKETS
            device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(data));
#endif
            // Lock on write (despite `readBuffer` being a concurrent queue) so that
            // responses split into multiple packets don't get mixed together

            lock(readBuffer)
            {
                // split packet into chunks of size not exceeding `MaximumPacketSize`
                var offset = 0;
                while(offset < data.Count)
                {
                    var toTake = Math.Min(MaximumPacketSize, data.Count - offset);
                    var chunk = data.Skip(offset).Take(toTake);
                    offset += toTake;
                    readBuffer.Enqueue(chunk.ToArray());
                    NewPacket?.Invoke();
#if DEBUG_PACKETS
                    device.Log(LogLevel.Noisy, "Enqueuing chunk of {0} bytes: {1}", chunk.Count(), Misc.PrettyPrintCollectionHex(chunk));
#endif

                    if(offset == data.Count && toTake == MaximumPacketSize)
                    {
                        // in order to indicate the end of a packet
                        // the chunk should be shorter than `MaximumPacketSize`;
                        // in case there is no data to send, empty chunk
                        // is generated
                        readBuffer.Enqueue(new byte[] { });
                        NewPacket?.Invoke();
                        device.Log(LogLevel.Noisy, "Enqueuing end of packet marker");
                    }
                }
            }
        }

        public PacketCreator DevicePreparePacket()
        {
            if(Direction != Direction.DeviceToHost)
            {
                throw new ArgumentException("Writing to this descriptor is not supported");
            }

            return packetCreator;
        }

        public bool TryRead(out byte[] data) => readBuffer.TryDequeue(out data);

        public override string ToString()
        {
            return $"[EP: id={Identifier}, dir={Direction}, type={TransferType}, mps={MaximumPacketSize}, int={Interval}]";
        }

        public byte Identifier { get; }

        public Direction Direction { get; }

        public EndpointTransferType TransferType { get; }

        public short MaximumPacketSize { get; }

        public byte Interval { get; }

        public bool DeviceNonBlocking { get; set; }

        public event Action NewPacket;

        void IUSBPipeWrite.Write(byte[] packet)
        {
            if(Direction != Direction.HostToDevice)
            {
                device.Log(LogLevel.Warning, "Trying to write to a Read-Only endpoint");
                return;
            }

            device.Log(LogLevel.Noisy, "Writing {0} bytes of data", packet.Length);
#if DEBUG_PACKETS
            device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(packet));
#endif

            var dw = DataWrittenInner;
            if(dw == null)
            {
                device.Log(LogLevel.Warning, "There is no data handler currently registered. Ignoring the written data!");
                return;
            }

            dw(packet);
        }

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append((byte)(((int)Direction << 7) | Identifier))
                /* TODO: here we ignore isochornous fields */
                .Append((byte)TransferType)
                .Append(MaximumPacketSize)
                .Append(Interval);
        }

        private event Action<byte[]> DataWrittenInner;

        private readonly PacketCreator packetCreator;
        private readonly IUSBDevice device;

        private readonly ConcurrentQueue<byte[]> readBuffer = new();

        public class PacketCreator : IDisposable
        {
            public PacketCreator(Action<ICollection<byte>> dataReadyCallback)
            {
                this.dataReadyCallback = dataReadyCallback;
                localBuffer = new List<byte>();
            }

            public void Add(byte b)
            {
                localBuffer.Add(b);
            }

            public void Add(uint u)
            {
                foreach(var b in BitConverter.GetBytes(u).Reverse())
                {
                    localBuffer.Add(b);
                }
            }

            public void Dispose()
            {
                dataReadyCallback(localBuffer);
                localBuffer = new List<byte>();
            }

            private List<byte> localBuffer;
            private readonly Action<ICollection<byte>> dataReadyCallback;
        }

        public enum EndpointSynchronizationType
        {
            NoSynchronization = 0,
            Asynchronous = 1,
            Adaptive = 2,
            Synchronous = 3
        }

        public enum EndpointIsoModeType
        {
            DataEndpoint = 0,
            FeedbackEndpoint = 1,
            ExplicitFeedbackEndpoint = 2
        }
    }
}