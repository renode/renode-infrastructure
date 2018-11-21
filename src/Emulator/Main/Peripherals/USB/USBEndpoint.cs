//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    public class USBEndpoint : DescriptorProvider
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

            buffer = new Queue<IEnumerable<byte>>();
            packetCreator = new PacketCreator(HandlePacket);
        }

        public event Action<byte[]> DataWritten
        {
            add
            {
                if(Direction != Direction.HostToDevice)
                {
                    throw new ArgumentException("Reading from this descriptor is not supported");
                }
                dataWritten += value;
            }
            remove
            {
                dataWritten -= value;
            }
        }

        public void Reset()
        {
            lock(buffer)
            {
                buffer.Clear();
            }
        }

        public void WriteData(byte[] packet)
        {
            if(Direction != Direction.HostToDevice)
            {
                device.Log(LogLevel.Warning, "Trying to write to a Read-Only endpoint");
                return;
            }

            dataWritten?.Invoke(packet);
        }

        public PacketCreator PreparePacket()
        {
            if(Direction != Direction.DeviceToHost)
            {
                throw new ArgumentException("Writing to this descriptor is not supported");
            }

            return packetCreator;
        }

        public void SetDataReadCallbackOneShot(Action<USBEndpoint, IEnumerable<byte>> callback)
        {
            lock(buffer)
            {
                if(buffer.Count > 0)
                {
                    callback(this, buffer.Dequeue());
                }
                else
                {
                    dataCallback = callback;
                }
            }
        }

        public byte Identifier { get; }
        public Direction Direction { get; }
        public EndpointTransferType TransferType { get; }
        public short MaximumPacketSize { get; }
        public byte Interval { get; }

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append((byte)(((int)Direction << 7) | Identifier))
                /* TODO: here we ignore isochornous fields */
                .Append((byte)TransferType)
                .Append(MaximumPacketSize)
                .Append(Interval);
        }

        public void HandlePacket(IEnumerable<byte> data)
        {
            lock(buffer)
            {
                buffer.Enqueue(data);
                if(dataCallback != null)
                {
                    dataCallback(this, buffer.Dequeue());
                    dataCallback = null;
                }
            }
        }

        private event Action<byte[]> dataWritten;
        private Action<USBEndpoint, IEnumerable<byte>> dataCallback;

        private readonly Queue<IEnumerable<byte>> buffer;
        private readonly PacketCreator packetCreator;
        private readonly IUSBDevice device;

        public class PacketCreator : IDisposable
        {
            public PacketCreator(Action<IEnumerable<byte>> dataReadyCallback)
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
            private readonly Action<IEnumerable<byte>> dataReadyCallback;
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