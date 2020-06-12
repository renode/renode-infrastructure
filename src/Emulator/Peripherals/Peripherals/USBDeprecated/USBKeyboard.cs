//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Input;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class USBKeyboard : IUSBPeripheral, IKeyboard
    {

        public event Action <uint> SendInterrupt ;
        public event Action <uint> SendPacket
        {
            add {}
            remove {}
        }

        protected Object thisLock = new Object();
        private const byte NumberOfEndpoints = 2;
        byte[] controlPacket;
        Queue <byte> queue;

        public USBKeyboard()
        {
            queue = new Queue<byte>();
            endpointDescriptor = new EndpointUSBDescriptor[3];
            for(int i=0; i<NumberOfEndpoints; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            fillEndpointsDescriptors(endpointDescriptor);
            interfaceDescriptor[0].EndpointDescriptor = endpointDescriptor;
            configurationDescriptor.InterfaceDescriptor = interfaceDescriptor;
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.Low;
        }

        private InterfaceUSBDescriptor[] interfaceDescriptor = new[]{new InterfaceUSBDescriptor
        {
            AlternateSetting = 0,
            InterfaceNumber = 0x00,
            NumberOfEndpoints = 1,
            InterfaceClass = 0x03,
            InterfaceProtocol = 0x02,
            InterfaceSubClass = 0x01,
            InterfaceIndex = 0x07
        }
        };
        private EndpointUSBDescriptor[] endpointDescriptor;

        public void WriteDataBulk(USBPacket packet)
        {

        }

        private void fillEndpointsDescriptors(EndpointUSBDescriptor[] endpointDesc)
        {
            endpointDesc[0].EndpointNumber = 1;
            endpointDesc[0].InEnpoint = true;
            endpointDesc[0].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Interrupt;
            endpointDesc[0].MaxPacketSize = 0x0004;
            endpointDesc[0].SynchronizationType = EndpointUSBDescriptor.SynchronizationTypeEnum.NoSynchronization;
            endpointDesc[0].UsageType = EndpointUSBDescriptor.UsageTypeEnum.Data;
            endpointDesc[0].Interval = 0x0a;

        }

        public void WriteDataControl(USBPacket packet)
        {
        }

        public void Reset()
        {
        }

        public uint GetAddress()
        {
            return DeviceAddress;
        }

        public     byte GetTransferStatus()
        {
            return 0;
        }

        int pkey;
        int modifiers = 0;

        public void Press(KeyScanCode scanCode)
        {
            lock(thisLock)
            {
                pkey = (int)scanCode & 0x7f;
                if(((int)scanCode) >= 0xe0 && ((int)scanCode) <= 0xe7)
                {
                    modifiers |= 1 << (((int)scanCode) & 0x7);
                    pkey = 0;
                }
                queue.Enqueue((byte)pkey);
            }
            Refresh();
        }

        public void Release(KeyScanCode scanCode)
        {
            lock(thisLock)
            {
                pkey = (int)0;
                if(((int)scanCode) >= 0xe0 && ((int)scanCode) <= 0xe7)
                {
                    modifiers &= ~(1 << (((int)scanCode) & 0x7));
                }
                for(int i = 0; i < 6; i++)
                    queue.Enqueue((byte)pkey);
            }
            Refresh();
        }

        private void Refresh()
        {
            var sendInterrupt = SendInterrupt;
            if(DeviceAddress != 0 && sendInterrupt != null)
            {
                sendInterrupt(DeviceAddress);
            }
        }

        public byte[] WriteInterrupt(USBPacket packet)
        {
            lock(thisLock)
            {
                if(queue.Count == 0)
                    return null;

                byte [] data = new byte[8];
                data[0] = (byte)modifiers;
                data[1] = 0;
                /*
            data [2] = (byte)pkey;
            data [3] = 0;
            data [4] = 0;
            data [5] = 0;
            data [6] = 0;
            data [7] = 0;*/
                for(int i=2; i<8; i++)
                {
                    if(queue.Count != 0)
                        data[i] = queue.Dequeue();
                    else
                        data[i] = 0;
                }
                return data;
            }
        }

        public byte[] GetDataBulk(USBPacket packet)
        {
            return null;
        }

        public byte[] GetDataControl(USBPacket packet)
        {
            return controlPacket;
        }

        private DeviceQualifierUSBDescriptor deviceQualifierDescriptor = new DeviceQualifierUSBDescriptor();
        private ConfigurationUSBDescriptor otherConfigurationDescriptor = new ConfigurationUSBDescriptor();
        private StringUSBDescriptor stringDescriptor = null;

        public byte[] GetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            DescriptorType type;
            type = (DescriptorType)((setupPacket.value & 0xff00) >> 8);
            uint index = (uint)(setupPacket.value & 0xff);
            switch(type)
            {
            case DescriptorType.Device:
                controlPacket = new byte[deviceDescriptor.ToArray().Length];
                deviceDescriptor.ToArray().CopyTo(controlPacket, 0);
                return deviceDescriptor.ToArray();
            case DescriptorType.Configuration:
                controlPacket = new byte[configurationDescriptor.ToArray().Length];
                configurationDescriptor.ToArray().CopyTo(controlPacket, 0);
                controlPacket = keyboardConfigDescriptor;
                return configurationDescriptor.ToArray();
            case DescriptorType.DeviceQualifier:
                controlPacket = new byte[deviceQualifierDescriptor.ToArray().Length];
                deviceQualifierDescriptor.ToArray().CopyTo(controlPacket, 0);
                return deviceQualifierDescriptor.ToArray();
            case DescriptorType.InterfacePower:
                throw new NotImplementedException("Interface Power Descriptor is not yet implemented. Please contact AntMicro for further support.");
            case DescriptorType.OtherSpeedConfiguration:
                controlPacket = new byte[otherConfigurationDescriptor.ToArray().Length];
                otherConfigurationDescriptor.ToArray().CopyTo(controlPacket, 0);
                return otherConfigurationDescriptor.ToArray();
            case DescriptorType.String:
                if(index == 0)
                {
                    stringDescriptor = new StringUSBDescriptor(1);
                    stringDescriptor.LangId[0] = EnglishLangId;
                }
                else
                {
                    stringDescriptor = new StringUSBDescriptor(stringValues[setupPacket.index][index]);
                }
                controlPacket = new byte[stringDescriptor.ToArray().Length];
                stringDescriptor.ToArray().CopyTo(controlPacket, 0);
                return stringDescriptor.ToArray();
            case (DescriptorType)0x22:
                controlPacket = keyboardHIDReportDescritpor;
                break;
            default:
                this.Log(LogLevel.Warning, "Unsupported keyboard request!!!");
                return null;
            }
            return null;
        }

        private Dictionary<ushort, string[]> stringValues = new Dictionary<ushort, string[]>()
        {
            {EnglishLangId, new string[]{
                    "",
                    "1",
                    "HID Keyboard",
                    "AntMicro",
                    "HID Keyboard",
                    "HID Keyboard",
                    "HID Keyboard",
                    "Configuration",
                }}
        };

        public byte[] ProcessClassGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            return controlPacket;
        }

        public void ProcessClassSet(USBPacket packet, USBSetupPacket setupPacket)
        {

        }

        public void SetDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void CleanDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void ToggleDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public bool GetDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void ClearFeature(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new USBRequestException();
        }

        public byte[] GetConfiguration()
        {
            throw new NotImplementedException();
        }


         #region IUSBDevice
        public byte[] GetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public byte[] GetStatus(USBPacket packet, USBSetupPacket setupPacket)
        {
            var arr = new byte[2];
            MessageRecipient recipient = (MessageRecipient)(setupPacket.requestType & 0x3);
            switch(recipient)
            {
            case MessageRecipient.Device:
                arr[0] = (byte)(((configurationDescriptor.RemoteWakeup ? 1 : 0) << 1) | (configurationDescriptor.SelfPowered ? 1 : 0));
                break;
            case MessageRecipient.Endpoint:
                //TODO: endpoint halt status
                goto default;
            default:
                arr[0] = 0;
                break;
            }
            return arr;
        }

        uint DeviceAddress = 0;

        public void SetAddress(uint address)
        {
            DeviceAddress = address;
        }

        public void SetConfiguration(USBPacket packet, USBSetupPacket setupPacket)
        {
        }

        public void SetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void SetFeature(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void SetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void SyncFrame(uint endpointId)
        {
            throw new NotImplementedException();
        }

        public void WriteData(byte[] data)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region descriptors
        private ConfigurationUSBDescriptor configurationDescriptor = new ConfigurationUSBDescriptor()
        {
            ConfigurationIndex = 0,
            SelfPowered = true,
            NumberOfInterfaces = 1,
            RemoteWakeup = true,
            MaxPower = 50, //500mA
            ConfigurationValue = 1
        };

 #endregion

        private StandardUSBDescriptor deviceDescriptor = new StandardUSBDescriptor
        {
            DeviceClass=0x00,
            DeviceSubClass = 0x00,
            USB = 0x0100,
            DeviceProtocol = 0x00,
            MaxPacketSize = 8,
            VendorId = 0x0627,
            ProductId = 0x0001,
            Device = 0x0000,
            ManufacturerIndex = 3,
            ProductIndex = 2,
            SerialNumberIndex = 1,
            NumberOfConfigurations = 1
        };
        byte[] keyboardHIDReportDescritpor = {
            0x05, 0x01,
            0x09, 0x06,
            0xa1, 0x01,
            0x75, 0x01,
            0x95, 0x08,
            0x05, 0x07,
            0x19, 0xe0,
            0x29, 0xe7,
            0x15, 0x00,
            0x25, 0x01,
            0x81, 0x02,
            0x95, 0x01,
            0x75, 0x08,
            0x81, 0x01,
            0x95, 0x05,
            0x75, 0x01,
            0x05, 0x08,
            0x19, 0x01,
            0x29, 0x05,
            0x91, 0x02,
            0x95, 0x01,
            0x75, 0x03,
            0x91, 0x01,
            0x95, 0x06,
            0x75, 0x08,
            0x15, 0x00,
            0x25, 0xff,
            0x05, 0x07,
            0x19, 0x00,
            0x29, 0xff,
            0x81, 0x00,
            0xc0
        };
        byte[] keyboardConfigDescriptor = {
            0x09,
            0x02,
            0x22, 0x00,
            0x01,
            0x01,
            0x06,
            0xa0,
            0x32,
            0x09,
            0x04,
            0x00,
            0x00,
            0x01,
            0x03,
            0x01,
            0x01,
            0x07,
            0x09,
            0x21,
            0x11, 0x01,
            0x00,
            0x01,
            0x22,
            0x3f, 0x00,
            0x07,
            0x05,
            0x81,
            0x03,
            0x08, 0x00,
            0x0a,
        };
        private const ushort EnglishLangId = 0x09;


        #region IUSBDevice implementation
        public byte[] ProcessVendorGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void ProcessVendorSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }





    #endregion
    }
}

