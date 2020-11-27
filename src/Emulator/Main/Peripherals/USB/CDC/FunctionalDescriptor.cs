//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB.CDC
{
    public class FunctionalDescriptor: IProvidesDescriptor
    {
        public FunctionalDescriptor(CdcFunctionalDescriptorType type, CdcFunctionalDescriptorSubtype subtype, params byte[] specificData)
        {
            this.type = type;
            this.subtype = subtype;
            this.specificData = specificData;
        }

        public BitStream GetDescriptor(bool recursive, BitStream buffer = null)
        {
            if(buffer == null)
            {
                buffer = new BitStream();
            }

            buffer.Append((byte)DescriptorLength);
            buffer.Append((byte)type);
            buffer.Append((byte)subtype);
            buffer.Append(specificData);

            return buffer;
        }

        public int RecursiveDescriptorLength => DescriptorLength;
        public int DescriptorLength => specificData.Length + 3;

        private readonly CdcFunctionalDescriptorType type;
        private readonly CdcFunctionalDescriptorSubtype subtype;
        private readonly byte[] specificData;
    }

    public enum CdcFunctionalDescriptorType : byte
    {
        Interface = 0x24,
        Endpoint = 0x25
    }

    public enum CdcFunctionalDescriptorSubtype : byte
    {
        Header = 0x00,
        CallManagement = 0x01,
        AbstractControlManagement = 0x02,
        DirectLineManagement = 0x03,
        TelephoneRinger = 0x04,
        TelephoneCallLineState = 0x05,
        Union = 0x06,
        CountrySelection = 0x07,
        TelephoneOperationalModes = 0x08,
        USBTerminal = 0x09,
        NetworkChannel = 0x0A,
        ProtocolUnit = 0x0B,
        ExtensionUnit = 0x0C,
        MultiChannelManagement = 0x0D,
        CAPIControlManagement = 0x0E,
        EthernetNetworking = 0x0F,
        ATMNetworking = 0x10,

        // 0x11 - 0xFF RESERVED
    }
}
