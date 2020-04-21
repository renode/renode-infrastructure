//
// Copyright (c) 2010-2020 Antmicro
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
using Antmicro.Renode.Utilities.Packets;

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

            OutEnabled = true;
            InEnabled = true;

            buffer = new Queue<IEnumerable<byte>>();
            packetCreator = new PacketCreator(WriteDataToHost);
        }

        public void Stall()
        {
            lock(buffer)
            {
                if(dataCallback != null)
                {
                    RunAfterRead();
                    dataCallback(USBTransactionStage.Stall());
                    dataCallback = null;
                }
                else
                {
                    isStalled = true;
                }
            }
        }

        public void Reset()
        {
            lock(buffer)
            {
                AfterReadOneShot = null;
                buffer.Clear();
                dataCallback = null;
                isStalled = false;
            }
        }

        public PacketCreator PreparePacket()
        {
            return packetCreator;
        }

        public void WriteDataToHost(ICollection<byte> data)
        {
            // control endpoints allow for both read/write regardless of the direction
            if(TransferType != EndpointTransferType.Control && Direction != Direction.DeviceToHost)
            {
                throw new ArgumentException("Writing to this descriptor is not supported");
            }

            lock(buffer)
            {
                if(data.Count == 0)
                {
                    buffer.Enqueue(new byte[0]);
                }
                else
                {
                    // split data into chunks of size not exceeding `MaximumPacketSize`
                    var offset = 0;
                    while(offset < data.Count)
                    {
                        var toTake = Math.Min(MaximumPacketSize, data.Count - offset);
                        var chunk = data.Skip(offset).Take(toTake);
                        offset += toTake;
                        buffer.Enqueue(chunk);
                    }
                }

                if(dataCallback != null)
                {
                    var result = buffer.Dequeue(); 

                    RunAfterRead();
                    dataCallback(USBTransactionStage.Data(result.ToArray()));
                    dataCallback = null;
                }
            }
        }

        public void HandleTransaction(USBTransactionStage hostStage, Action<USBTransactionStage> deviceStage)
        {
            this.Log(LogLevel.Noisy, "Handling {0}", hostStage);

            switch(hostStage.PacketID)
            {
                case USBPacketId.SetupToken:
                    HandleSetupTransaction(hostStage, deviceStage);
                    break;

                case USBPacketId.InToken:
                    HandleInTransaction(hostStage, deviceStage);
                    break;

                case USBPacketId.OutToken:
                    HandleOutTransaction(hostStage, deviceStage);
                    break;

                default:
                    this.Log(LogLevel.Warning, "Unsupported USB transaction: {0}", hostStage.PacketID);                    
                    break;
            }
        }

        public bool InEnabled 
        { 
            get => inEnabled;

            set
            {
                this.Log(LogLevel.Noisy, "IN {0}", value ? "Enabled" : "Disabled");
                inEnabled = value;
            }
        }

        public bool OutEnabled 
        { 
            get => outEnabled;

            set
            {
                this.Log(LogLevel.Noisy, "OUT {0}", value ? "Enabled" : "Disabled");
                outEnabled = value;
            }
        }

        // TODO: change those into events?
        public Action AfterReadOneShot { get; set; }

        public Action<USBEndpoint> AfterRead { get; set; }

        public USBDeviceCore Core => core;

        public event Action<byte[]> DataFromHostWritten
        {
            add
            {
                // control endpoints allow for both IN/OUT 
                if(TransferType != EndpointTransferType.Control && Direction != Direction.HostToDevice)
                {
                    throw new ArgumentException("Reading from this descriptor is not supported");
                }
                dataFromHostWritten += value;
            }
            remove
            {
                dataFromHostWritten -= value;
            }
        }

        public void Log(LogLevel level, string message, params object[] parameters)
        {
            var type = TransferType == EndpointTransferType.Control 
                ? "Control"
                : (Direction == Direction.HostToDevice ? "OUT" : "IN");

            core.Device.Log(level, $"EP#{Identifier}/{type}: {message}", parameters);
        }

        private void RunAfterRead()
        {
            var aros = AfterReadOneShot;
            AfterReadOneShot = null;
            if(aros != null)
            {
                aros.Invoke();
            }

            AfterRead?.Invoke(this);
        }

        private void HandleSetupTransaction(USBTransactionStage hostStage, Action<USBTransactionStage> deviceStage)
        {
            var handler = SetupPacketHandler;
            if(handler == null)
            {
                this.Log(LogLevel.Warning, "Received SETUP packet, but there is no handler. The data will be lost");
                deviceStage(USBTransactionStage.Stall());
                return;
            }

            lock(buffer)
            {
                // SETUP transactions automatically clear stall
                isStalled = false;
            }

            handler(this, hostStage, deviceStage);
        }

        private void HandleInTransaction(USBTransactionStage hostStage, Action<USBTransactionStage> deviceStage)
        {
            var handler = InPacketHandler;
            if(handler != null)
            {
                handler(this, hostStage, deviceStage);
                RunAfterRead();
                return;
            }

            // control endpoints allow for both IN/OUT packets regardless of the direction
            if(TransferType != EndpointTransferType.Control && Direction != Direction.DeviceToHost)
            {
                this.Log(LogLevel.Warning, "Received IN packet on OUT endpoint");
                deviceStage(USBTransactionStage.Stall());
                return;
            }

            lock(buffer)
            {
                if(isStalled)
                {
                    this.Log(LogLevel.Noisy, "Received IN packet on a stalled endpoint");
                    deviceStage(USBTransactionStage.Stall());
                    return;
                }

                if(!InEnabled)
                {
                    this.Log(LogLevel.Noisy, "Received IN packet on a disabled endpoint");
                    // TODO: implement async response
                    deviceStage(USBTransactionStage.NotAck());
                    return;
                }

                if(buffer.Count > 0)
                {
                    var response = USBTransactionStage.Data(buffer.Dequeue().ToArray());
                    this.Log(LogLevel.Noisy, "Sending response to IN transaction of size {0} bytes", response.Payload.Length);
#if DEBUG_PACKETS
                    core.Device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(response.Data));
#endif
                    RunAfterRead();
                    deviceStage(response);
                }
                else
                {
                    dataCallback = deviceStage;
                }
            }
        }

        private void HandleOutTransaction(USBTransactionStage hostStage, Action<USBTransactionStage> deviceStage)
        {
            // control endpoints allow for both IN/OUT packets regardless of the direction
            if(TransferType != EndpointTransferType.Control && Direction != Direction.HostToDevice)
            {
                this.Log(LogLevel.Warning, "Received OUT packet on IN endpoint");
                deviceStage(USBTransactionStage.Stall());
                return;
            }

            lock(buffer)
            {
                if(isStalled)
                {
                    this.Log(LogLevel.Noisy, "Received OUT packet on a stalled endpoint");
                    deviceStage(USBTransactionStage.Stall());
                    return;
                }

                if(!OutEnabled)
                {
                    this.Log(LogLevel.Noisy, "Received OUT packet on a disabled endpoint");
                    // TODO: implement async response
                    deviceStage(USBTransactionStage.NotAck());
                    return;
                }
            }

            if(hostStage.Payload.Length > MaximumPacketSize)
            {
                this.Log(LogLevel.Warning, "Received OUT packet with more bytes ({0}) than the maximum packet size ({1}). Dropping it", hostStage.Payload.Length, MaximumPacketSize);
                deviceStage(USBTransactionStage.Stall());
                return;
            }

            var handler = OutPacketHandler;
            if(handler == null)
            {
                if(hostStage.Payload.Length == 0)
                {
                    // do not fail on empty packets event if there is no callback registered
                    deviceStage(USBTransactionStage.Ack());
                }
                else
                {
                    this.Log(LogLevel.Warning, "There is no data handler currently registered. Ignoring the written data!");
                    deviceStage(USBTransactionStage.Stall());
                }
                return;
            }

            this.Log(LogLevel.Noisy, "Received {0} bytes of data", hostStage.Payload.Length);
#if DEBUG_PACKETS
            core.Device.Log(LogLevel.Noisy, Misc.PrettyPrintCollectionHex(hostStage.Payload));
#endif

            handler(this, hostStage, deviceStage);
        }

        public byte Identifier { get; }
        public Direction Direction { get; }
        public EndpointTransferType TransferType { get; }
        public short MaximumPacketSize { get; }
        public byte Interval { get; }

        public Action<USBEndpoint, USBTransactionStage, Action<USBTransactionStage>> SetupPacketHandler { get; set; }
        public Action<USBEndpoint, USBTransactionStage, Action<USBTransactionStage>> InPacketHandler { get; set; }
        public Action<USBEndpoint, USBTransactionStage, Action<USBTransactionStage>> OutPacketHandler { get; set; }

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append((byte)(((int)Direction << 7) | Identifier))
                /* TODO: here we ignore isochornous fields */
                .Append((byte)TransferType)
                .Append(MaximumPacketSize)
                .Append(Interval);
        }

        private bool outEnabled;
        private bool inEnabled;
        private bool isStalled;

        private event Action<byte[]> dataFromHostWritten;
        private Action<USBTransactionStage> dataCallback;

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
