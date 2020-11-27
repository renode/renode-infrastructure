//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB.CDC
{
    public class Interface : USBInterface
    {
        public Interface(IUSBDevice device,
                           byte identifier,
                           byte subClassCode,
                           byte protocol,
                           IEnumerable<FunctionalDescriptor> descriptors = null,
                           string description = null) : base(device, identifier, USBClassCode.CommunicationsCDCControl, subClassCode, protocol, description)
        {
            if(descriptors != null)
            {
                var pos = 0;
                foreach(var d in descriptors)
                {
                    RegisterSubdescriptor(d, pos++);
                }
            }
        }

        public override BitStream HandleRequest(SetupPacket packet)
        {
            switch(packet.Type)
            {
                case PacketType.Class:
                    return HandleClassRequest((CdcClassRequest)packet.Request);
                default:
                    device.Log(LogLevel.Warning, "Unsupported type: 0x{0:x}", packet.Type);
                    return BitStream.Empty;
            }
        }

        private BitStream HandleClassRequest(CdcClassRequest request)
        {
            device.Log(LogLevel.Warning, "Handling an unimplemented CDC class request: {0}", request);
            return BitStream.Empty;
        }

        private enum CdcClassRequest
        {
            SetLineEncoding = 0x20,
            GetLineEncoding = 0x21,
            SetControlLineState = 0x22,
            SendBreak = 0x23
        }
    }
}
