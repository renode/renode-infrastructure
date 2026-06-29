//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Peripherals.USBDeprecated;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Extensions.Utilities.USB;

public interface IDescriptor
{
    byte Type { get; }

    byte[] AsBytes { get; }

    public static IDescriptor DecodeDescriptor(byte[] buf, int offset = 0)
    {
        if(buf.Length - offset < 2)
        {
            throw new ArgumentException("Descriptor must be at least two bytes long");
        }
        var len = buf[offset];
        if(offset + len > buf.Length)
        {
            throw new ArgumentException("Descriptor must be fully contianed within buffer");
        }
        var type = buf[offset + 1];
        switch((DescriptorType)type)
        {
        case DescriptorType.Device:
            return Packet.Decode<DeviceDescriptor>(buf, offset);
        case DescriptorType.Configuration:
            return Packet.Decode<ConfigurationDescriptor>(buf, offset);
        case DescriptorType.Interface:
            return Packet.Decode<InterfaceDescriptor>(buf, offset);
        case DescriptorType.Endpoint:
            return Packet.Decode<EndpointDescriptor>(buf, offset);
        default:
            return new RawDescriptor(buf[offset..(offset + len)]);
        }
    }

    public static IEnumerable<IDescriptor> EnumerateDescriptors(byte[] buf)
    {
        var offset = 0;
        while(offset < buf.Length)
        {
            var len = buf[offset];
            yield return DecodeDescriptor(buf, offset);
            offset += len;
        }
    }

    // Some USB descriptors (most notably endpoint descriptors) aren't fully self-describing,
    // their parent interface has to be inferred from the last interface descriptor. This
    // function groups descriptors by their parent interface descriptor
    public static IEnumerable<(InterfaceDescriptor, IDescriptor[])> EnumerateInterfaceDescriptors(IEnumerable<IDescriptor> descs)
    {
        InterfaceDescriptor? lastIface = null;
        List<IDescriptor> descBuf = new();
        foreach(var desc in descs)
        {
            if(desc is InterfaceDescriptor iface)
            {
                if(lastIface != null)
                {
                    yield return (lastIface.Value, descBuf.ToArray());
                }
                lastIface = iface;
                descBuf.Clear();
            }
            else
            {
                descBuf.Add(desc);
            }
        }
        if(lastIface != null)
        {
            yield return (lastIface.Value, descBuf.ToArray());
        }
    }
}
