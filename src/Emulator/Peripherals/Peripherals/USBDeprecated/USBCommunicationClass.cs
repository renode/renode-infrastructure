//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class USBCommunicationClass
    {
        public USBCommunicationClass ()
        {
            //deviceDescriptor.DeviceClass = DeviceClassCode;
            //endpointDescriptor.Type = (DescriptorType) CommunicationClassDescriptorType.Interface;
        }
        protected const byte DeviceClassCode = 0x02;
        protected const byte InterfaceClassCode = 0x02;
        protected const byte DataInterfaceClassCode = 0x0A;


        protected enum SubclassCode : byte
        {
            Reserved = 0x00,
            DirectLineControlModel = 0x01,
            AbstractControlModel = 0x02,
            TelephoneControlModel = 0x03,
            MultiChannelControlModel = 0x04,
            CAPIControlModel = 0x05,
            EthernetNetworkingControlModel = 0x06,
            ATMNetworkingControlModel = 0x07,
            WirellecHandsetControlModel = 0x08,
            DeviceManagement = 0x09,
            MobileDirectLineModel = 0x0A,
            OBEX = 0x0B
        }

        protected enum ProtocolCode : byte
        {
            NoClassSpecific = 0x00,
            ATCommandsV250 = 0x01,
            ATCommandsPCCA101 = 0x02,
            ATCommandsPCCa101AnnexO = 0x03,
            ATCommandsGSM = 0x04,
            ATCommands3GPP = 0x05,
            ATCommandsTIA = 0x06,
            USBEEM = 0x07,
            ExternalProtocol = 0xFE,
            VendorSpecific = 0xFF
        }

        protected enum DataProtocolCode : byte
        {
            NoClassSpecific = 0x00,
            NetworkTransferBlock = 0x01,
            PhysicalInterafaceISDN = 0x30,
            HDLC = 0x31,
            Transparent = 0x32,
            MenagementProtocolQ921 = 0x50,
            DataLinkProtocolQ921 = 0x51,
            TEIMultiplexorQ921 = 0x52,
            DataCompressionProcedures = 0x90,
            EuroISDN = 0x91,
            V24RateAdaptationtoISDN = 0x92,
            CAPICommands = 0x93,
            HostBasedDriver = 0xFD,
            CDCSpecification = 0xFE,
            VendorSpecific = 0xFF

        }
        protected enum CommunicationClassDescriptorType : byte
        {
            Interface = 0x24,
            Endpoint = 0x25
        }

        protected enum CommunicationClassFunctionalDescriptorsSubType : byte
        {
            Headerr = 0x00,
            CallManagement = 0x01,
            AbstractControlManagement = 0x02,
            DirectLineManagement = 0x03,
            TelephoneRinger = 0x04,
            TelephoneCallAndLineReportingCapabilities = 0x05,
            Union = 0x06,
            CountrySelection = 0x07,
            TelephoneOperationalModes = 0x08,
            USBTerminal = 0x09,
            NetworkChannelTerminal = 0x0A,
            ProtocolUnit = 0x0B,
            ExtentsionUnit = 0x0C,
            MultiChannelManagement = 0x0D,
            CAPIControlManagement = 0x0E,
            EthernetNetworking = 0x0F,
            ATMNetworking = 0x10,
            WirelessHandsetControl = 0x11,
            MobileDirectLineModel = 0x12,
            MDLMDetail = 0x13,
            DeviceManagementModel = 0x14,
            OBEX = 0x15,
            CommandSet = 0x16,
            CommandSetDetail = 0x17,
            TelephoneControlModel = 0x18,
            OBEXServiceIdentifier = 0x19,
            NCMFunctionalDescriptor = 0x1A
        }

        protected enum ClassSpecificRequestCodes : byte
        {
            SendEncaplsulatedCommand = 0x00,
            GetEncapsulatedResponse = 0x01,
            SetCommFeature = 0x02,
            GetCommFeature = 0x03,
            ClearCommFeature = 0x04,
            SetAuxLIneState = 0x10,
            SetHookState = 0x11,
            PulseSetup = 0x12,
            SendPulse = 0x13,
            SetPulseTime = 0x14,
            RingAuxJack = 0x15,
            SetLineCoding = 0x20,
            GetLineCoding = 0x21,
            SetControlLineState = 0x22,
            SendBreak = 0x23,
            SetRingerParams = 0x30,
            GetRingerParams = 0x31,
            SetOperationParms = 0x32,
            GetOperationParms = 0x33,
            SetLineParms = 0x34,
            GetLineParms = 0x35,
            DialDigits = 0x36,
            SetUnitParameter = 0x37,
            GetUnitParameter = 0x38,
            GetProfile = 0x3A,
            SetEthernetMulticastFilters = 0x40,
            SetEthernetPowerManagementPatternFilter = 0x41,
            GetEthernetPowerManagementPatternFilter = 0x42,
            SetEthernetPacketFilter = 0x43,
            GetEthernetStatistic = 0x44,
            SetAtmDataFormat = 0x50,
            GetAtmDeviceStatistics = 0x51,
            SetAtmDefaultVc = 0x52,
            GetAtmVcStatistics = 0x53,
            GetNtbParameters = 0x80,
            GetNetAddress = 0x81,
            SetNetAddress = 0x82,
            GetNtbFormat = 0x83,
            SetNtbFormat = 0x84,
            GetNtbInputSize = 0x85,
            SetNtbInputSize = 0x86,
            GetMaxDatagramSize = 0x87,
            SetMaxDatagramSize = 0x88,
            GetCRCMode = 0x89,
            SetCRCMode = 0x8A
        }

        protected enum ClassSpecificNotificationCodes : byte
        {
            NetworkConnection = 0x00,
            ResponseAvaliable = 0x01,
            AuxJackHookState = 0x08,
            RingDetect = 0x09,
            SerialState = 0x20,
            CAllStateChange = 0x28,
            LineStateChange = 0x29,
            ConnectedSpeedChange = 0x2A
        }


        protected class HeaderFunctionalDescriptor : USBDescriptor
        {

            public HeaderFunctionalDescriptor()
            {
                base.Length = 0x04;
                base.Type = (DescriptorType) CommunicationClassDescriptorType.Interface;
            }
            public byte Subtype{get; set;}
            public ushort bcdCDC{get; set;}

            public override byte[] ToArray ()
            {

                var arr = base.ToArray ();
                arr[0x2] = Subtype;
                arr[0x3] = bcdCDC.LoByte();
                arr[0x4] = bcdCDC.HiByte();
                return arr;
            }
        }

        protected class UnionFunctionalDescriptor : USBDescriptor
        {
            public UnionFunctionalDescriptor(byte subordinateInterfacesNumber)
            {
                base.Length = (byte)(subordinateInterfacesNumber + 0x04);
                base.Type = (DescriptorType) CommunicationClassDescriptorType.Interface;
                SubordinateInterface = new byte[subordinateInterfacesNumber];
            }
            public byte Subtype{get; set;}
            public byte ControllInterface{get; set;}
            public byte[] SubordinateInterface{get; set;}

            public override byte[] ToArray ()
            {
                var arr =  base.ToArray ();
                arr[0x2] = Subtype;
                arr[0x3] = ControllInterface;
                Array.Copy(SubordinateInterface, 0,arr, 0x4, SubordinateInterface.Length);
                return arr;

            }
        }

        protected class CountrySelectionFunctionalDescriptor : USBDescriptor
        {

            public CountrySelectionFunctionalDescriptor(byte countryCodesNumber)
            {
                base.Length = (byte)(countryCodesNumber * 2 + 4);
                base.Type = (DescriptorType) CommunicationClassDescriptorType.Interface;
                CountryCode = new byte[countryCodesNumber];
            }

            public byte Subtype{get; set;}
            public byte CountryCodeReleaseDate{get; set;}
            public byte[] CountryCode{get; set;}

            public override byte[] ToArray ()
            {
                var arr = base.ToArray ();
                arr[0x2] = Subtype;
                arr[0x3] = CountryCodeReleaseDate;
                Array.Copy(CountryCode,0,arr,0x4,CountryCode.Length);
                return arr;
            }

        }


        //protected ConfigurationUSBDescriptor configurationDescriptor;
        //protected StandardUSBDescriptor deviceDescriptor;
        //protected InterfaceUSBDescriptor interfaceDescriptor;
        //protected EndpointUSBDescriptor endpointDescriptor;
        //protected StringUSBDescriptor stringDesriptor;


    }
}

