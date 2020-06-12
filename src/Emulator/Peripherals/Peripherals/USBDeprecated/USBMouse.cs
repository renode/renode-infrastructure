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
    public class USBMouse : IUSBPeripheral, IRelativePositionPointerInput
    {
        public event Action <uint> SendInterrupt;
        public event Action <uint> SendPacket
        {
            add {}
            remove {}
        }

        protected Object thisLock = new Object();
        private const byte NumberOfEndpoints = 2;
        byte[] controlPacket;
        Queue <sbyte> queue;

        public USBMouse()
        {
            endpointDescriptor = new EndpointUSBDescriptor[3];
            for(int i=0; i<NumberOfEndpoints; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            fillEndpointsDescriptors(endpointDescriptor);
            interfaceDescriptor[0].EndpointDescriptor = endpointDescriptor;
            configurationDescriptor.InterfaceDescriptor = interfaceDescriptor;
            x = 0;
            y = 0;
            mstate = 0;
            queue = new Queue<sbyte>();
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.Low;
        }

        private InterfaceUSBDescriptor[] interfaceDescriptor = new[] {new InterfaceUSBDescriptor
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

        public byte GetTransferStatus()
        {
            return 0;
        }

        public byte[] WriteInterrupt(USBPacket packet)
        {
            lock(thisLock)
            {
                if(queue.Count < 3)
                    return null;

                byte[] data = new byte[4];
                int[] datas = new int[4];
                datas[0] = queue.Dequeue();
                datas[1] = queue.Dequeue();
                datas[2] = queue.Dequeue();
                datas[3] = 0;

                while(queue.Count >2 && queue.Peek()==datas[0])
                {
                    if(datas[1]==127 || datas[1]==-127 || datas[2]==127 || datas[2]==-127)
                        break;
                    queue.Dequeue();
                    int x, y;
                    x = (sbyte)queue.Dequeue();
                    y = (sbyte)queue.Dequeue();
                    if(datas[1] + x > 127)
                    {
                        x = 127;
                    }
                    else
                        if(datas[1] + x < -127)
                    {
                        x = -127;
                    }
                    else
                    {
                        datas[1] += x;
                    }
                    if(datas[2] + y > 127)
                    {
                        y = 127;
                    }
                    else
                        if(datas[2] + y < -127)
                    {
                        y = -127;
                    }
                    else
                    {
                        datas[2] += y;
                    }

                }
                for(int i=0; i<4; i++)
                    data[i] = (byte)datas[i];
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
                controlPacket = mouseConfigDescriptor;
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
                controlPacket = mouseHIDReportDescriptor;
                break;
            default:
                this.Log(LogLevel.Warning, "Unsupported mouse request!!!");
                return null;
            }
            return null;
        }

        private Dictionary<ushort, string[]> stringValues = new Dictionary<ushort, string[]>() {
            {EnglishLangId, new string[]{
                    "",
                    "1",
                    "HID Mouse",
                    "AntMicro",
                    "",
                    "",
                    "HID Mouse",
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

            return arr;
        }

        uint DeviceAddress = 0;

        public void SetAddress(uint address)
        {
            DeviceAddress = address;
        }

        public void MoveBy(int newx, int newy)
        {
            lock(thisLock)
            {
                x = newx;
                y = newy;
                if(x > 127)
                {
                    x = 127;
                }
                else
                    if(x < -127)
                    {
                        x = -127;
                    }
                if(y > 127)
                {
                    y = 127;
                }
                else
                    if(y < -127)
                    {
                        y = -127;
                    }
                queue.Enqueue((sbyte)mstate);
                queue.Enqueue((sbyte)x);
                queue.Enqueue((sbyte)y);
            }
            Refresh();
        }

        int mstate = 0;

        public void Press(MouseButton button)
        {
            lock(thisLock)
            {
                if(button == MouseButton.Left)
                    mstate = 0x01;
                if(button == MouseButton.Right)
                    mstate = 0x02;
                if(button == MouseButton.Middle)
                    mstate = 0x04;
                x = 0;
                y = 0;
                queue.Enqueue((sbyte)mstate);
                queue.Enqueue((sbyte)x);
                queue.Enqueue((sbyte)y);
            }
            Refresh();
        }

        public void Release(MouseButton button)
        {
            lock(thisLock)
            {
                mstate = 0;
                x = 0;
                y = 0;
                queue.Enqueue((sbyte)mstate);
                queue.Enqueue((sbyte)x);
                queue.Enqueue((sbyte)y);
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

        private int x;
        private int y;

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
        private ConfigurationUSBDescriptor configurationDescriptor = new ConfigurationUSBDescriptor() {
            ConfigurationIndex = 0,
            SelfPowered = true,
            NumberOfInterfaces = 1,
            RemoteWakeup = true,
            MaxPower = 50, //500mA
            ConfigurationValue = 1
        };
        #endregion
        private StandardUSBDescriptor deviceDescriptor = new StandardUSBDescriptor {
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
        byte[] mouseConfigDescriptor = {
            0x09,
            0x02,
            0x22, 0x00,
            0x01,
            0x01,
            0x04,
            0xe0,
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
            52, 0,
            0x07,
            0x05,
            0x81,
            0x03,
            0x04, 0x00,
            0x0a
        };
        byte[] mouseHIDReportDescriptor = {
            0x05, 0x01,
            0x09, 0x02,
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
            0x09, 0x38,
            0x15, 0x81,
            0x25, 0x7f,
            0x75, 0x08,
            0x95, 0x03,
            0x81, 0x06,
            0xc0,
            0xc0
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

