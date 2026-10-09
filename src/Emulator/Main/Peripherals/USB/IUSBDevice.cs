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

    /// <summary>
    /// Performs a device-to-host control transfer. <paramref name="onRead"/> is called with
    /// <c>null</c> if the device answered with a STALL handshake (see
    /// <see cref="IUSBPipeRead.Stalled"/>), in which case there is no status stage.
    /// </summary>
    void SetupRead(SetupPacket packet, Action<byte[]> onRead)
    {
        SetupPacketWrite(packet);
        ReadAtLeast(packet.Count, data =>
        {
            if(data != null)
            {
                Write(new byte[] { });
            }
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

    /// <summary>
    /// Performs a host-to-device control transfer, reporting whether it was accepted.
    /// <paramref name="onDone"/> is called with <c>false</c> if the device answered with a STALL
    /// handshake (see <see cref="IUSBPipeRead.Stalled"/>) instead of completing the status stage.
    /// The <see cref="Action"/> overload above cannot tell the two apart, so it never completes
    /// on a stalled transfer.
    /// </summary>
    void SetupWrite(SetupPacket packet, byte[] data, Action<bool> onDone)
    {
        SetupPacketWrite(packet);
        if(data != null)
        {
            Write(data);
        }
        ReadPacket(res => onDone(res != null));
    }

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

    /// <summary>
    /// Reads the device descriptor. <paramref name="onRead"/> is not called at all if the device
    /// stalls the request.
    /// </summary>
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
            res =>
            {
                if(res == null)
                {
                    return;
                }
                onRead(Packet.Decode<DeviceDescriptor>(res));
            }
        );
    }

    DeviceDescriptor ReadDeviceDescriptorBlocking(CancellationToken token = default) => Misc.WaitForCallback<DeviceDescriptor>(onDone => ReadDeviceDescriptor(onDone), token);

    /// <summary>
    /// Reads the configuration descriptor. <paramref name="onRead"/> is not called at all if the
    /// device stalls either of the two requests it takes.
    /// </summary>
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
                if(res == null)
                {
                    return;
                }
                var config = Packet.Decode<ConfigurationDescriptor>(res);
                setupPacket.Count = config.TotalLength;
                SetupRead(
                    setupPacket,
                    res =>
                    {
                        if(res == null)
                        {
                            return;
                        }
                        onRead((config, IDescriptor.EnumerateDescriptors(res)));
                    }
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

    /// <summary>
    /// Raised when the device answered the transfer in progress with a STALL handshake
    /// (USB 2.0 SS 8.4.5) instead of data or a successful status stage.
    /// </summary>
    /// <remarks>
    /// Without a status signal a host that has issued a control transfer and is waiting in
    /// <see cref="ReadAtLeast"/> for a response the device will never send waits forever. The
    /// <c>Read*</c> helpers below observe this event and complete with a <c>null</c> buffer, so
    /// a stalled transfer fails instead of hanging. A pipe implementation that cannot detect a
    /// stall declares this event and simply never raises it.
    /// </remarks>
    event Action Stalled;

    // NOTE: If multiple reads are requested at once, earlier calls to `Read*` will take precedence over later onces

    /// <summary>
    /// Reads a single packet. <paramref name="cb"/> is called with <c>null</c> if the transfer
    /// was stalled - see <see cref="Stalled"/>.
    /// </summary>
    void ReadPacket(Action<byte[]> cb)
    {
        var calledBack = false;
        var packetLock = new object();

        Action collectChunk = null;
        Action onStalled = null;
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
                Stalled -= onStalled;
                cb(data);
            }
        };
        onStalled = () =>
        {
            lock(packetLock)
            {
                if(calledBack)
                {
                    return;
                }
                calledBack = true;
                NewPacket -= collectChunk;
                Stalled -= onStalled;
                cb(null);
            }
        };

        lock(this)
        {
            NewPacket += collectChunk;
            Stalled += onStalled;
            collectChunk();
        }
    }

    byte[] ReadPacketBlocking(CancellationToken token = default) => Misc.WaitForCallback<byte[]>(ReadPacket, token);

    /// <summary>
    /// Reads until at least <paramref name="minLength"/> bytes have been collected.
    /// <paramref name="cb"/> is called with <c>null</c> if the transfer was stalled before that
    /// happened - see <see cref="Stalled"/>.
    /// </summary>
    void ReadAtLeast(int minLength, Action<byte[]> cb)
    {
        var calledBack = false;
        var data = new List<byte>();
        var packetLock = new object();

        Action collectChunk = null;
        Action onStalled = null;
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
                Stalled -= onStalled;
                cb(data.ToArray());
            }
        };
        onStalled = () =>
        {
            lock(packetLock)
            {
                if(calledBack)
                {
                    return;
                }
                calledBack = true;
                NewPacket -= collectChunk;
                Stalled -= onStalled;
                cb(null);
            }
        };

        lock(this)
        {
            NewPacket += collectChunk;
            Stalled += onStalled;
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
