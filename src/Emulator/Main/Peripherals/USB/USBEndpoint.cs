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

            device.Log(LogLevel.Noisy, "Writing {0} bytes of data", packet.Length);
#if DEBUG_PACKETS
            device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(packet));
#endif

            var dw = dataWritten;
            if(dw == null)
            {
                device.Log(LogLevel.Warning, "There is no data handler currently registered. Ignoring the written data!");
                return;
            }

            dw(packet);
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
                device.Log(LogLevel.Noisy, "Data read callback set");
                if(buffer.Count > 0)
                {
                    device.Log(LogLevel.Noisy, "Data read callback fired");
#if DEBUG_PACKETS
                    device.Log(LogLevel.Noisy, "Sending back {0} bytes: {1}", buffer.Peek().Count(), Misc.PrettyPrintCollectionHex(buffer.Peek()));
#endif
                    callback(this, buffer.Dequeue());
                }
                else
                {
                    if(NonBlocking)
                    {
                        device.Log(LogLevel.Noisy, "No data to read in non-blocking mode - returning an empty buffer");
                        callback(this, new byte[0]);
                    }
                    else
                    {
                        dataCallback = callback;
                    }
                }
            }
        }

        public byte[] Read(uint limit, System.Threading.CancellationToken cancellationToken)
        {
            var result = Enumerable.Empty<byte>();

            var endOfPacketDetected = false;
            while(!endOfPacketDetected
                    && (!cancellationToken.IsCancellationRequested)
                    && (limit == 0 || result.Count() < limit))
            {
                var mre = new System.Threading.ManualResetEvent(false);
                SetDataReadCallbackOneShot((e, bytes) =>
                {
                    var arr = bytes.ToArray();
                    result = result.Concat(arr);
                    if(arr.Length < MaximumPacketSize)
                    {
                        endOfPacketDetected = true;
                    }
                    mre.Set();
                });

                System.Threading.WaitHandle.WaitAny(new System.Threading.WaitHandle[] { cancellationToken.WaitHandle, mre });
            }

            if(result.Count() > limit)
            {
                Logger.Log(LogLevel.Warning, "Read more data from the USB endpoint ({0}) than limit ({1}). Some bytes will be dropped, expect problems!", result.Count(), limit);
                result = result.Take((int)limit);
            }

            return result.ToArray();
        }

        public override string ToString()
        {
            return $"[EP: id={Identifier}, dir={Direction}, type={TransferType}, mps={MaximumPacketSize}, int={Interval}]";
        }

        public byte Identifier { get; }
        public Direction Direction { get; }
        public EndpointTransferType TransferType { get; }
        public short MaximumPacketSize { get; }
        public byte Interval { get; }

        public bool NonBlocking { get; set; }

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append((byte)(((int)Direction << 7) | Identifier))
                /* TODO: here we ignore isochornous fields */
                .Append((byte)TransferType)
                .Append(MaximumPacketSize)
                .Append(Interval);
        }

        public void HandlePacket(ICollection<byte> data)
        {
            lock(buffer)
            {
                device.Log(LogLevel.Noisy, "Handling data packet of size: {0}", data.Count);
#if DEBUG_PACKETS
                device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(data));
#endif

                // split packet into chunks of size not exceeding `MaximumPacketSize`
                var offset = 0;
                while(offset < data.Count)
                {
                    var toTake = Math.Min(MaximumPacketSize, data.Count - offset);
                    var chunk = data.Skip(offset).Take(toTake);
                    offset += toTake;
                    buffer.Enqueue(chunk);
#if DEBUG_PACKETS
                    device.Log(LogLevel.Noisy, "Enqueuing chunk of {0} bytes: {1}", chunk.Count(), Misc.PrettyPrintCollectionHex(chunk));
#endif

                    if(offset == data.Count && toTake == MaximumPacketSize)
                    {
                        // in order to indicate the end of a packet
                        // the chunk should be shorter than `MaximumPacketSize`;
                        // in case there is no data to send, empty chunk
                        // is generated
                        buffer.Enqueue(new byte[0]);
                        device.Log(LogLevel.Noisy, "Enqueuing end of packet marker");
                    }
                }

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
