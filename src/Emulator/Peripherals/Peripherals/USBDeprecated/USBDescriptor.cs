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
    public abstract class USBDescriptor
    {

        public USBDescriptor()
        {

        }

        private byte length;

        public byte Length
        {
            get
            {
                return length;
            }
            set
            {
                length = value;
                array = new byte[value];
            }
        }

        public DescriptorType Type{ get; set; }

        byte[] array;

        public virtual byte[] ToArray()
        {
            array[0x0] = Length;
            array[0x1] = (byte)Type;
            return array;
        }
    }

    public class StandardUSBDescriptor : USBDescriptor
    {
        public StandardUSBDescriptor()
        {
            Type = DescriptorType.Device;
            Length = 0x12;
        }

        public ushort USB{ get; set; }

        public byte DeviceClass{ get; set; }

        public byte DeviceSubClass{ get; set; }

        public byte DeviceProtocol{ get; set; }

        public byte MaxPacketSize{ get; set; }

        public ushort VendorId{ get; set; }

        public ushort ProductId{ get; set; }

        public ushort Device{ get; set; }

        public byte ManufacturerIndex{ get; set; }

        public byte ProductIndex{ get; set; }

        public byte SerialNumberIndex{ get; set; }

        public byte NumberOfConfigurations{ get; set; }

        public override byte[] ToArray()
        {
            var arr = base.ToArray();
            arr[0x2] = USB.LoByte();
            arr[0x3] = USB.HiByte();
            arr[0x4] = DeviceClass;
            arr[0x5] = DeviceSubClass;
            arr[0x6] = DeviceProtocol;
            arr[0x7] = MaxPacketSize;
            arr[0x8] = VendorId.LoByte();
            arr[0x9] = VendorId.HiByte();
            arr[0xA] = ProductId.LoByte();
            arr[0xB] = ProductId.HiByte();
            arr[0xC] = Device.LoByte();
            arr[0xD] = Device.HiByte();
            arr[0xE] = ManufacturerIndex;
            arr[0xF] = ProductIndex;
            arr[0x10] = SerialNumberIndex;
            arr[0x11] = NumberOfConfigurations;
            return arr;
        }
    }

    public class DeviceQualifierUSBDescriptor:USBDescriptor
    {
        public DeviceQualifierUSBDescriptor()
        {
            Type = DescriptorType.DeviceQualifier;
            Length = 0xA;
        }

        public ushort USB{ get; set; }

        public byte DeviceClass{ get; set; }

        public byte DeviceSubClass{ get; set; }

        public byte DeviceProtocol{ get; set; }

        public byte MaxPacketSize{ get; set; }

        public byte NumberOfConfigurations{ get; set; }
        //+1 byte reserved

        public override byte[] ToArray()
        {
            var arr = base.ToArray();
            arr[0x2] = USB.LoByte();
            arr[0x3] = USB.HiByte();
            arr[0x4] = DeviceClass;
            arr[0x5] = DeviceSubClass;
            arr[0x6] = DeviceProtocol;
            arr[0x7] = MaxPacketSize;
            arr[0x8] = NumberOfConfigurations;
            arr[0x9] = 0;
            //Reserved
            return arr;
        }
    }

    public class ConfigurationUSBDescriptor:USBDescriptor
    {
        public ConfigurationUSBDescriptor()
        {
            Type = DescriptorType.Configuration;
            Length = 0x9;
        }

        public ushort TotalLength{ get; set; }

        public byte NumberOfInterfaces{ get; set; }

        public byte ConfigurationValue{ get; set; }

        public byte ConfigurationIndex{ get; set; }

        public bool SelfPowered { get; set; }

        public bool RemoteWakeup{ get; set; }

        public byte Attributes
        {
            get
            {
                return (byte)((1 << 7) | ((SelfPowered ? 1 : 0) << 6) | ((RemoteWakeup ? 1 : 0) << 5));
            }
            set
            {

                SelfPowered = ((value >> 6) & 1) != 0;
                RemoteWakeup = ((value >> 5) & 1) != 0;
            }
        }

        public byte MaxPower{ get; set; }

        public InterfaceUSBDescriptor[] InterfaceDescriptor;

        public override byte[] ToArray()
        {
            TotalLength = Length;
            for(int i=0; i<NumberOfInterfaces; i++)
            {
                TotalLength += InterfaceDescriptor[i].Length;
                for(int j=0; j<InterfaceDescriptor[i].NumberOfEndpoints; j++)
                {
                    TotalLength += InterfaceDescriptor[i].EndpointDescriptor[j].Length;
                }
            }

            var arr = new byte[TotalLength];
            var offset = Length;
            arr[0x0] = Length;
            arr[0x1] = (byte)Type;
            arr[0x2] = TotalLength.LoByte();
            arr[0x3] = TotalLength.HiByte();
            arr[0x4] = NumberOfInterfaces;
            arr[0x5] = ConfigurationValue;
            arr[0x6] = ConfigurationIndex;
            arr[0x7] = Attributes;
            arr[0x8] = MaxPower;

            for(int i=0; i<NumberOfInterfaces; i++)
            {
                InterfaceDescriptor[i].ToArray().CopyTo(arr, offset);
                offset += InterfaceDescriptor[i].Length;
                for(int j=0; j<InterfaceDescriptor[i].NumberOfEndpoints; j++)
                {
                    InterfaceDescriptor[i].EndpointDescriptor[j].ToArray().CopyTo(arr, offset);
                    offset += InterfaceDescriptor[i].EndpointDescriptor[j].Length;
                }
            }
            return arr;
        }
    }

    public class InterfaceUSBDescriptor:USBDescriptor
    {
        public InterfaceUSBDescriptor()
        {
            Type = DescriptorType.Interface;
            Length = 0x9;
        }

        public byte InterfaceNumber{ get; set; }

        public byte AlternateSetting{ get; set; }

        public byte NumberOfEndpoints{ get; set; }

        public byte InterfaceClass{ get; set; }

        public byte InterfaceSubClass{ get; set; }

        public byte InterfaceProtocol{ get; set; }

        public byte InterfaceIndex{ get; set; }

        public EndpointUSBDescriptor[] EndpointDescriptor;

        public override byte[] ToArray()
        {
            var arr = base.ToArray();
            arr[0x2] = InterfaceNumber;
            arr[0x3] = AlternateSetting;
            arr[0x4] = NumberOfEndpoints;
            arr[0x5] = InterfaceClass;
            arr[0x6] = InterfaceSubClass;
            arr[0x7] = InterfaceProtocol;
            arr[0x8] = InterfaceIndex;
            return arr;
        }
    }

    public class EndpointUSBDescriptor:USBDescriptor
    {
        public EndpointUSBDescriptor()
        {
            Type = DescriptorType.Endpoint;
            Length = 7;
        }

        public byte EndpointNumber{ get; set; }

        public bool InEnpoint{ get; set; }

        public byte EndpointAddress
        {
            get
            {
                return (byte)(((InEnpoint ? 1 : 0) << 7) | (EndpointNumber & 7));
                //6..4 reserved
            }
            set
            {
                EndpointNumber = (byte)(value & 7);
                InEnpoint = ((value >> 7) & 1) != 0;
            }
        }

        public TransferTypeEnum TransferType{ get; set; }

        public SynchronizationTypeEnum SynchronizationType{ get; set; }

        public UsageTypeEnum UsageType{ get; set; }

        public byte Attributes
        {
            get
            {
                return (byte)((((byte)UsageType & 3) << 4) | ((byte)SynchronizationType & 3) << 2 | ((byte)TransferType & 3));
            }
            set
            {
                TransferType = (TransferTypeEnum)(value & 3);
                SynchronizationType = (SynchronizationTypeEnum)((value >> 2) & 3);
                UsageType = (UsageTypeEnum)((value >> 4) & 3);
            }
        }

        public ushort MaxPacketSize{ get; set; }

        public byte Interval{ get; set; }

        public override byte[] ToArray()
        {
            var arr = base.ToArray();
            arr[0x2] = EndpointAddress;
            arr[0x3] = Attributes;
            arr[0x4] = MaxPacketSize.LoByte();
            arr[0x5] = MaxPacketSize.HiByte();
            arr[0x6] = Interval;

            return arr;
        }
        public enum TransferTypeEnum : byte
        {
            Control = 0x00,
            Isochronous = 0x01,
            Bulk = 0x2,
            Interrupt = 0x03
        }

        public enum SynchronizationTypeEnum : byte
        {
            NoSynchronization = 0x00,
            Asynchronous = 0x01,
            Adaptive = 0x02,
            Synchronous = 0x03
        }

        public enum UsageTypeEnum : byte
        {
            Data = 0x00,
            Feedback = 0x01,
            ImplicitFeedbackData = 0x02
        }
    }

    public class StringUSBDescriptor : USBDescriptor
    {
        public StringUSBDescriptor(string value)
        {
            Type = DescriptorType.String;
            StringValue = value;
            Length = (byte)(2 + System.Text.ASCIIEncoding.Unicode.GetByteCount(value));
        }

        public StringUSBDescriptor(uint numberOfLangs)
        {
            LangId = new ushort[numberOfLangs];
            Length = (byte)(numberOfLangs * 2 + 2);
            Type = DescriptorType.String;
        }

        public string StringValue { get; set; }

        public ushort[] LangId{ get; set; }

        public override byte[] ToArray()
        {
            var arr = base.ToArray();
            int i = 0x2;
            if(StringValue != null)
            {
                var bytes = System.Text.ASCIIEncoding.Unicode.GetBytes(StringValue);

                foreach(var byt in bytes)
                {
                    arr[i] = byt;
                    ++i;
                }
            }
            else
            {
                foreach(var id in LangId)
                {
                    arr[i] = id.LoByte();
                    ++i;

                    arr[i] = id.HiByte();
                    ++i;
                }
            }

            return arr;
        }
    }
}

