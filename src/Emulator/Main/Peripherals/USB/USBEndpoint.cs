//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB
{
    public class USBEndpoint : DescriptorProvider
    {
        public USBEndpoint(USBDeviceCore core,
                           byte identifier,
                           Direction direction,
                           EndpointTransferType transferType,
                           short maximumPacketSize,
                           byte interval) : base(7, (byte)DescriptorType.Endpoint)
        {
            if(identifier > 16)
            {
                throw new ConstructionException($"Endpoint id: {identifier} is out of range - only values from 0 to 15 are valid");
            }

            this.core = core;

            Identifier = identifier;
            Direction = direction;
            TransferType = transferType;
            MaximumPacketSize = maximumPacketSize;
            Interval = interval;

            buffer = new Queue<IEnumerable<byte>>();
            packetCreator = new PacketCreator(HandleEndpointData);
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
                transactionCallback = false;
                dataCallback = null;
            }
        }

        public void WriteData(byte[] packet)
        {
            if(Direction != Direction.HostToDevice)
            {
                core.Device.Log(LogLevel.Warning, "Trying to write to a Read-Only endpoint");
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

        public void ReadPacketOneShot(Action<USBEndpoint, IEnumerable<byte>> callback)
        {
            ReadRequest?.Invoke(this);

            lock(buffer)
            {
                if(buffer.Count > 0)
                {
                    callback(this, buffer.Dequeue());
                }
                else
                {
                    dataCallback = callback;
                    transactionCallback = false;
                }
            }
        }

        public void ReadTransactionOneShot(Action<USBEndpoint, IEnumerable<byte>> callback)
        {
            ReadRequest?.Invoke(this);

            lock(buffer)
            {
                if(buffer.Count > 0)
                {
                    var result = Enumerable.Empty<byte>();

                    while(buffer.Count > 0)
                    {
                        var current = buffer.Dequeue();
                        result.Concat(current);

                        if(current.Count() < MaximumPacketSize)
                        {
                            // end of tranaction detected
                            break;
                        }
                    }
                    
                    callback(this, result);
                }
                else
                {
                    dataCallback = callback;
                    transactionCallback = true;
                }
            }
        }

        public void HandleSetupPacket(SetupPacket packet, Action<byte[]> resultCallback, byte[] additionalData = null)
        {
            var chsph = CustomSetupPacketHandler;
            if(chsph == null)
            {
                core.Device.Log(LogLevel.Warning, "Received setup packet on endpoint {0}, but there is no handler. The data will be lost", Identifier);
                resultCallback(new byte[0]);
                return;
            }

            chsph(packet, resultCallback, additionalData);
        }

        public byte Identifier { get; }
        public Direction Direction { get; }
        public EndpointTransferType TransferType { get; }
        public short MaximumPacketSize { get; }
        public byte Interval { get; }

        public Action<SetupPacket, Action<byte[]>, byte[]> CustomSetupPacketHandler { get; set; }
        public Action<USBEndpoint> ReadRequest { get; set; }

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append((byte)(((int)Direction << 7) | Identifier))
                /* TODO: here we ignore isochornous fields */
                .Append((byte)TransferType)
                .Append(MaximumPacketSize)
                .Append(Interval);
        }

        public void HandleEndpointData(ICollection<byte> data)
        {
            lock(buffer)
            {
                // split data into chunks of size not exceeding `MaximumPacketSize`
                var offset = 0;
                while(offset < data.Count)
                {
                    var toTake = Math.Min(MaximumPacketSize, data.Count - offset);
                    var chunk = data.Skip(offset).Take(toTake);
                    offset += toTake;
                    buffer.Enqueue(chunk);

                    if(offset == data.Count && toTake == MaximumPacketSize)
                    {
                        // in order to indicate the end of a transaction
                        // the chunk should be shorter than `MaximumPacketSize`;
                        // in case there is no data to send, empty chunk
                        // is generated
                        buffer.Enqueue(new byte[0]);
                    }
                }

                if(dataCallback != null)
                {
                    var result = Enumerable.Empty<byte>();

                    if(buffer.Count != 0)
                    {
                        result = buffer.Dequeue();

                        if(transactionCallback)
                        {
                            while(buffer.Count > 0)
                            {
                                var current = buffer.Dequeue();
                                result.Concat(current);

                                if(current.Count() < MaximumPacketSize)
                                {
                                    // end of tranaction detected
                                    break;
                                }
                            }
                        }
                    }

                    dataCallback(this, result);
                    dataCallback = null;
                    transactionCallback = false;
                }
            }
        }

        private event Action<byte[]> dataWritten;
        private Action<USBEndpoint, IEnumerable<byte>> dataCallback;
        private bool transactionCallback;

        private readonly Queue<IEnumerable<byte>> buffer;
        private readonly PacketCreator packetCreator;
        private readonly USBDeviceCore core;

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
