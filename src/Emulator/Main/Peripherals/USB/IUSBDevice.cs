//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Threading;

using Antmicro.Renode.Extensions.Utilities.USB;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Core.USB;

public interface IUSBPipeSetup : IUSBPipeRead, IUSBPipeWrite
{
    void SetupPacketWrite(SetupPacket packet);

    void SetupRead(SetupPacket packet, Action<byte[]> onRead)
    {
        SetupPacketWrite(packet);
        ReadAtLeast(packet.Count, data =>
        {
            Write(new byte[] { });
            onRead(data);
        });
    }

    byte[] SetupReadBlocking(SetupPacket packet, CancellationToken token = default) => Misc.WaitForCallback<byte[]>(onDone => SetupRead(packet, onDone), token);

    void SetupWrite(SetupPacket packet, byte[] data, Action onDone)
    {
        SetupPacketWrite(packet);
        if(data != null)
        {
            Write(data);
        }
        ReadPacket(_ => onDone());
    }

    void SetupWriteBlocking(SetupPacket packet, byte[] data, CancellationToken token = default) => Misc.WaitForCallback(onDone => SetupWrite(packet, data, onDone), token);

    void SetAddress(byte address, Action onDone)
    {
        var setup = new SetupPacket
        {
            Recipient = PacketRecipient.Device,
            Type = PacketType.Standard,
            Direction = Direction.HostToDevice,
            Request = (byte)StandardRequest.SetAddress,
            Value = address,
            Index = 0,
            Count = 0
        };
        SetupWrite(setup, null, onDone);
    }

    void SetConfiguration(byte configuration, Action onDone)
    {
        var setup = new SetupPacket
        {
            Recipient = PacketRecipient.Device,
            Type = PacketType.Standard,
            Direction = Direction.HostToDevice,
            Request = (byte)StandardRequest.SetConfiguration,
            Value = configuration,
            Index = 0,
            Count = 0
        };
        SetupWrite(setup, null, onDone);
    }

    void ReadDeviceDescriptor(Action<DeviceDescriptor> onRead)
    {
        var setupPacket = new SetupPacket
        {
            Recipient = PacketRecipient.Device,
            Type = PacketType.Standard,
            Direction = Direction.DeviceToHost,
            Request = (byte)StandardRequest.GetDescriptor,
            Value = ((int)DescriptorType.Device << 8),
            Index = 0,
            Count = (ushort)Packet.CalculateLength<DeviceDescriptor>()
        };

        SetupRead(
            setupPacket,
            res => onRead(Packet.Decode<DeviceDescriptor>(res))
        );
    }

    DeviceDescriptor ReadDeviceDescriptorBlocking(CancellationToken token = default) => Misc.WaitForCallback<DeviceDescriptor>(onDone => ReadDeviceDescriptor(onDone), token);

    void ReadConfigurationDescriptor(byte configuration, Action<(ConfigurationDescriptor, IEnumerable<IDescriptor>)> onRead)
    {
        var setupPacket = new SetupPacket
        {
            Recipient = PacketRecipient.Device,
            Type = PacketType.Standard,
            Direction = Direction.DeviceToHost,
            Request = (byte)StandardRequest.GetDescriptor,
            Value = (ushort)(((int)DescriptorType.Configuration << 8) | configuration),
            Index = 0,
            Count = (ushort)Packet.CalculateLength<DeviceDescriptor>()
        };

        SetupRead(
            setupPacket,
            res =>
            {
                var config = Packet.Decode<ConfigurationDescriptor>(res);
                setupPacket.Count = config.TotalLength;
                SetupRead(
                    setupPacket,
                    res => onRead((config, IDescriptor.EnumerateDescriptors(res)))
                );
            }
        );
    }

    (ConfigurationDescriptor, IEnumerable<IDescriptor>) ReadConfigurationDescriptorBlocking(byte configuration, CancellationToken token = default) => Misc.WaitForCallback<(ConfigurationDescriptor, IEnumerable<IDescriptor>)>(onDone => ReadConfigurationDescriptor(configuration, onDone), token);

    void EnableInterface(byte interfaceNumber, byte alternateSetting, Action onDone)
    {
        var setup = new SetupPacket
        {
            Recipient = PacketRecipient.Interface,
            Type = PacketType.Standard,
            Direction = Direction.HostToDevice,
            Request = (byte)StandardRequest.SetInterface,
            Value = alternateSetting,
            Index = interfaceNumber,
            Count = 0
        };
        SetupWrite(setup, null, onDone);
    }

    void CDCControlLineState(byte interfaceNumber, bool rts, bool dtr, Action onDone)
    {
        var setup = new SetupPacket
        {
            Recipient = PacketRecipient.Interface,
            Type = PacketType.Class,
            Direction = Direction.HostToDevice,
            Request = 0x22, // SET_CONTROL_LINE_STATE
            Value = (ushort)((rts ? 2 : 0) | (dtr ? 1 : 0)),
            Index = interfaceNumber,
            Count = 0
        };
        SetupWrite(setup, null, onDone);
    }
}

public interface IUSBPipeWrite
{
    void Write(byte[] data);
}

public interface IUSBPipeRead
{
    event Action NewPacket;

    // NOTE: If multiple reads are requested at once, earlier calls to `Read*` will take precedence over later onces

    void ReadPacket(Action<byte[]> cb)
    {
        var calledBack = false;
        var packetLock = new object();

        Action collectChunk = null;
        collectChunk = () =>
        {
            lock(packetLock)
            {
                if(calledBack)
                {
                    return;
                }
                if(!TryRead(out var data))
                {
                    return;
                }
                calledBack = true;
                NewPacket -= collectChunk;
                cb(data);
            }
        };

        lock(this)
        {
            NewPacket += collectChunk;
            collectChunk();
        }
    }

    byte[] ReadPacketBlocking(CancellationToken token = default) => Misc.WaitForCallback<byte[]>(ReadPacket, token);

    void ReadAtLeast(int minLength, Action<byte[]> cb)
    {
        var calledBack = false;
        var data = new List<byte>();
        var packetLock = new object();

        Action collectChunk = null;
        collectChunk = () =>
        {
            lock(packetLock)
            {
                if(calledBack)
                {
                    return;
                }
                while(data.Count < minLength && TryRead(out var chunk))
                {
                    data.AddRange(chunk);
                }
                if(data.Count < minLength)
                {
                    return;
                }
                calledBack = true;
                NewPacket -= collectChunk;
                cb(data.ToArray());
            }
        };

        lock(this)
        {
            NewPacket += collectChunk;
            collectChunk();
        }
    }

    bool TryRead(out byte[] data);
}

public interface IUSBConnection : IDisposable
{
    IUSBPipeSetup ConnectEndpointSetup(byte endpoint);

    IUSBPipeRead ConnectEndpointRead(byte endpoint);

    IUSBPipeWrite ConnectEndpointWrite(byte endpoint);
}

public interface IUSBDevice : IPeripheral
{
    IUSBConnection ConnectUSB();

    // USB addresses are dynamically allocated, so this can't be specified using registration poins
    byte Address { get; }
}
