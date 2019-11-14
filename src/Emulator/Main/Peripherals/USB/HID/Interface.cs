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

namespace Antmicro.Renode.Core.USB.HID
{
    public class Interface : USBInterface
    {
        public Interface(IUSBDevice device,
                               byte identifier,
                               byte subClassCode = (byte)SubclassCode.BootInterfaceSubclass,
                               byte protocol = (byte)HID.Protocol.None,
                               string description = null,
                               ReportDescriptor reportDescriptor = null) : base(device, identifier, USBClassCode.HumanInterfaceDevice, subClassCode, protocol, description)
        {
            HID_ReportDescriptor = reportDescriptor ?? new ReportDescriptor();
            HID_Descriptor = new HID.Descriptor(HID_ReportDescriptor);

            RegisterSubdescriptor(HID_Descriptor, 0);
        }

        public HID.Descriptor HID_Descriptor { get; }
        public ReportDescriptor HID_ReportDescriptor { get; }

        public override BitStream HandleRequest(SetupPacket packet)
        {
            switch(packet.Type)
            {
                case PacketType.Standard:
                    return HandleStandardRequest(packet.Direction, (StandardRequest)packet.Request, packet.Value);
                case PacketType.Class:
                    return HandleClassRequest(packet.Direction, (HidClassRequest)packet.Request, packet.Value);
                default:
                    device.Log(LogLevel.Warning, "Unsupported type: 0x{0:X}", packet.Type);
                    return BitStream.Empty;
            }
        }

        private BitStream HandleClassRequest(Direction direction, HidClassRequest request, ushort value)
        {
            switch(request)
            {
                case HidClassRequest.SetIdle:
                    // we simply ignore this as we don't implement any repeated interrupts at all
                    return BitStream.Empty;
                default:
                    device.Log(LogLevel.Warning, "Unsupported class request: 0x{0:X}", request);
                    return BitStream.Empty;
            }
        }

        private BitStream HandleStandardRequest(Direction direction, StandardRequest request, ushort value)
        {
            switch(request)
            {
                case StandardRequest.GetDescriptor:
                    if(direction != Direction.DeviceToHost)
                    {
                        device.Log(LogLevel.Warning, "Unexpected standard request direction");
                        return BitStream.Empty;
                    }
                    return HandleGetDescriptor(value);
                default:
                    device.Log(LogLevel.Warning, "Unsupported standard request: 0x{0:X}", request);
                    return BitStream.Empty;
            }
        }

        private BitStream HandleGetDescriptor(ushort value)
        {
            var descriptorType = (DescriptorType)(value >> 8);
            var descriptorIndex = (byte)value;

            switch(descriptorType)
            {
                case DescriptorType.HID:
                    return HID_Descriptor.GetDescriptor(false);
                case DescriptorType.Report:
                    return HID_ReportDescriptor.GetDescriptor(false);
                default:
                    device.Log(LogLevel.Warning, "Unsupported descriptor type: 0x{0:X}", descriptorType);
                    return BitStream.Empty;
            }
        }

        private enum HidClassRequest
        {
            GetReport = 0x1,
            GetIdle = 0x2,
            GetProtocol = 0x3,
            // 0x4 - 0x8: reserved
            SetReport = 0x9,
            SetIdle = 0xa,
            SetProtocol = 0xb
        }
    }
}