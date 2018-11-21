//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB.MSC
{
    public class Interface : USBInterface
    {
        public Interface(IUSBDevice device,
                               byte identifier,
                               byte subClassCode,
                               byte protocol,
                               string description = null) : base(device, identifier, USBClassCode.MassStorage, subClassCode, protocol, description)
        {
        }

        public override BitStream HandleRequest(SetupPacket packet)
        {
            switch(packet.Type)
            {
                case PacketType.Class:
                    return HandleClassRequest(packet);
                default:
                    device.Log(LogLevel.Warning, "Unsupported packet type: 0x{0:X}", packet.Type);
                    return BitStream.Empty;
            }
        }

        private BitStream HandleClassRequest(SetupPacket packet)
        {
            switch((ClassRequests)packet.Request)
            {
                case ClassRequests.GetMaxLUN:
                    // If no LUN is associated with the device, the value returned shall be 0.
                    return new BitStream().Append((byte)0);
                default:
                    device.Log(LogLevel.Warning, "Unsupported class request: 0x{0:X}", packet.Request);
                    return BitStream.Empty;
            }
        }

        private enum ClassRequests
        {
            GetMaxLUN = 0xFE,
        }
    }
}