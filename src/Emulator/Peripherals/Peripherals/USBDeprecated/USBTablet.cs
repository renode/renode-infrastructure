//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Input;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class USBTablet :IUSBPeripheral, IAbsolutePositionPointerInput
    {
        public USBTablet()
        {
            Reset();
        }

        public event Action<uint> SendInterrupt;
        public event Action <uint> SendPacket
        {
            add {}
            remove {}
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.Low;
        }

        public void ClearFeature(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public byte[] GetConfiguration()
        {
            throw new NotImplementedException();
        }

        public void SetAddress(uint address)
        {
            deviceAddress = address;
        }

        public byte[] GetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public byte[] GetStatus(USBPacket packet, USBSetupPacket setupPacket)
        {
            var arr = new byte[2];
            return arr;
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

        public byte[] ProcessVendorGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void ProcessVendorSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

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

        public bool GetDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void ToggleDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public uint GetAddress()
        {
            return deviceAddress;
        }

        public byte[] WriteInterrupt(USBPacket packet)
        {
            lock(thisLock)
            {
                if(changeState)
                {
                    buffer[5] = 0;
                    changeState = false;
                    return this.buffer;
                }
                else
                    return null;
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

        public byte GetTransferStatus()
        {
            return 0;
        }

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
                controlPacket = tabletConfigDescriptor;
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
                controlPacket = tabletHIDReportDescriptor;
                break;
            default:
                this.Log(LogLevel.Warning, "Unsupported mouse request!!!");
                return null;
            }
            return null;
        }

        public void WriteDataBulk(USBPacket packet)
        {

        }

        public void WriteDataControl(USBPacket packet)
        {

        }

        public void Reset()
        {
            x = y = 0;
            otherConfigurationDescriptor = new ConfigurationUSBDescriptor();
            deviceQualifierDescriptor = new DeviceQualifierUSBDescriptor();
            endpointDescriptor = new EndpointUSBDescriptor[3];
            for(int i = 0; i < NumberOfEndpoints; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            fillEndpointsDescriptors(endpointDescriptor);
            interfaceDescriptor[0].EndpointDescriptor = endpointDescriptor;
            configurationDescriptor.InterfaceDescriptor = interfaceDescriptor;

            mstate = 0;
            changeState = false;
            buffer = new byte[6];
        }

        public void Press(MouseButton button)
        {
            lock(thisLock)
            {
                mstate = (byte)button;
                buffer[0] = mstate;
                buffer[1] = (byte)(x & byte.MaxValue);
                // x small
                buffer[2] = (byte)((x >> 8) & 127);
                // x big
                buffer[3] = (byte)(y & byte.MaxValue);
                // y small
                buffer[4] = (byte)((y >> 8) & 127);
                // y big
                changeState = true;
            }
            Refresh();
        }

        public void Release(MouseButton button)
        {
            lock(thisLock)
            {
                buffer[0] = mstate = 0;
                buffer[1] = (byte)(x & byte.MaxValue);
                // x small
                buffer[2] = (byte)((x >> 8) & 127);
                // x big
                buffer[3] = (byte)(y & byte.MaxValue);
                // y small
                buffer[4] = (byte)((y >> 8) & 127);
                // y big
                changeState = true;
            }
            Refresh();
        }

        public void MoveTo(int x, int y)
        {
            lock(thisLock)
            {
                this.x = x;
                this.y = y;
                buffer[0] = mstate;
                buffer[1] = (byte)(x & byte.MaxValue);
                // x small
                buffer[2] = (byte)((x >> 8) & 127);
                // x big
                buffer[3] = (byte)(y & byte.MaxValue);
                // y small
                buffer[4] = (byte)((y >> 8) & 127);
                // y big
                changeState = true;
            }
            Refresh();
        }

        public int MaxX
        {
            get
            {
                return 32767;
            }
        }

        public int MaxY
        {
            get
            {
                return 32767;
            }
        }

        public int MinX
        {
            get
            {
                return 0;
            }
        }

        public int MinY
        {
            get
            {
                return 0;
            }
        }

        private void fillEndpointsDescriptors(EndpointUSBDescriptor[] endpointDesc)
        {
            endpointDesc[0].EndpointNumber = 1;
            endpointDesc[0].InEnpoint = true;
            endpointDesc[0].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Interrupt;
            endpointDesc[0].MaxPacketSize = 0x0008;
            endpointDesc[0].SynchronizationType = EndpointUSBDescriptor.SynchronizationTypeEnum.NoSynchronization;
            endpointDesc[0].UsageType = EndpointUSBDescriptor.UsageTypeEnum.Data;
            endpointDesc[0].Interval = 0x0a;
        }

        private void Refresh()
        {
            if(deviceAddress != 0)
            {
                SendInterrupt(deviceAddress);
            }
        }

        private const byte NumX = 28;
        private const byte NumY = 16;
        private const ushort EnglishLangId = 0x09;
        private const byte NumberOfEndpoints = 2;

        private uint deviceAddress;
        private Object thisLock = new Object();
        private DeviceQualifierUSBDescriptor deviceQualifierDescriptor;
        private ConfigurationUSBDescriptor otherConfigurationDescriptor;
        private StringUSBDescriptor stringDescriptor = null;
        private EndpointUSBDescriptor[] endpointDescriptor;
        private byte[] controlPacket;
        private Byte[] buffer;
        private int x;
        private int y;
        private byte mstate = 0;
        private bool changeState = false;
        private Dictionary<ushort, string[]> stringValues = new Dictionary<ushort, string[]>() { {EnglishLangId, new string[] {
                    "",
                    "1",
                    "HID Tablet",
                    "AntMicro",
                    "",
                    "",
                    "HID Tablet",
                    "Configuration"
                }
            }
        };
        private InterfaceUSBDescriptor[] interfaceDescriptor = new[] {new InterfaceUSBDescriptor {
                AlternateSetting = 0,
                InterfaceNumber = 0x00,
                NumberOfEndpoints = 1,
                InterfaceClass = 0x03,
                InterfaceProtocol = 0x02,
                InterfaceSubClass = 0x01,
                InterfaceIndex = 0x07
            }
        };
        private ConfigurationUSBDescriptor configurationDescriptor = new ConfigurationUSBDescriptor() {
            ConfigurationIndex = 0,
            SelfPowered = true,
            NumberOfInterfaces = 1,
            RemoteWakeup = true,
            MaxPower = 50, //500mA
            ConfigurationValue = 1
        };

        private StandardUSBDescriptor deviceDescriptor = new StandardUSBDescriptor {
            DeviceClass = 0x00,
            DeviceSubClass = 0x00,
            USB = 0x0100,
            DeviceProtocol = 0x00,
            MaxPacketSize = 8,
            VendorId = 0x80ee,
            ProductId = 0x0021,
            Device = 0x0000,
            ManufacturerIndex = 3,
            ProductIndex = 2,
            SerialNumberIndex = 1,
            NumberOfConfigurations = 1
        };

        private byte[] tabletConfigDescriptor = {
            0x09,
            0x02,
            0x22, 0x00,
            0x01,
            0x01,
            0x05,
            0xa0,
            50,
            0x09,
            0x04,
            0x00,
            0x00,
            0x01,
            0x03,
            0x01,
            0x02,
            0x07,
            0x09,
            0x21,
            0x01, 0x00,
            0x00,
            0x01,
            0x22,
            74, 0,
            0x07,
            0x05,
            0x81,
            0x03,
            0x04, 0x00,
            0x0a
        };
        private byte[] tabletHIDReportDescriptor = {
            0x05, 0x01,
            0x09, 0x01,
            0xa1, 0x01,
            0x09, 0x01,
            0xa1, 0x00,
            0x05, 0x09,
            0x19, 0x01,
            0x29, 0x03,
            0x15, 0x00,
            0x25, 0x01,
            0x95, 0x03,
            0x75, 0x01,
            0x81, 0x02,
            0x95, 0x01,
            0x75, 0x05,
            0x81, 0x01,
            0x05, 0x01,
            0x09, 0x30,
            0x09, 0x31,
            0x15, 0x00,
            0x26, 0xff, 0x7f,
            0x35, 0x00,
            0x46, 0xff, 0x7f,
            0x75, 0x10,
            0x95, 0x02,
            0x81, 0x02,
            0x05, 0x01,
            0x09, 0x38,
            0x15, 0x81,
            0x25, 0x7f,
            0x35, 0x00,
            0x45, 0x00,
            0x75, 0x08,
            0x95, 0x01,
            0x81, 0x06,
            0xc0,
            0xc0,
        };
    }
}

