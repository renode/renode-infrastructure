//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Network;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Peripherals;
using System.Linq;
using System.Threading;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class UsbHub :  IUSBHub, IUSBPeripheral
    {
        public event Action <uint> SendInterrupt
        {
            add {}
            remove {}
        }

        public event Action <uint> SendPacket
        {
            add {}
            remove {}
        }

        private readonly IMachine machine;

        public event Action <uint> Connected ;
        public event Action <uint,uint> Disconnected ;
        public event Action <IUSBHub> RegisterHub ;
        public event Action <IUSBPeripheral> ActiveDevice ;

        byte[] controlPacket;

        public byte NumberOfPorts { get; set; }

        public void WriteDataControl(USBPacket packet)
        {
            //throw new System.NotImplementedException();
        }

        public byte GetTransferStatus()
        {
            return 0;
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.High;
        }

        public void WriteDataBulk(USBPacket packet)
        {
            // throw new System.NotImplementedException();
        }

        public byte[] GetDataControl(USBPacket packet)
        {
            return controlPacket;
        }

        public byte[] GetDataBulk(USBPacket packet)
        {
            throw new System.NotImplementedException();
            // return
        }

        uint DeviceAddress;

        public uint GetAddress()
        {
            return DeviceAddress;
        }

        public UsbHub(IMachine machine)
        {
            this.machine = machine;
            registeredDevices = new Dictionary<byte, IUSBPeripheral>();
            endpointDescriptor = new EndpointUSBDescriptor[NumberOfEndpoints];
            NumberOfPorts = 3;
            ports = new uint [NumberOfPorts];
            hubDescriptor.NbrPorts = NumberOfPorts;
            for(int i=0; i<NumberOfEndpoints; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            fillEndpointsDescriptors(endpointDescriptor);
            interfaceDescriptor[0].EndpointDescriptor = endpointDescriptor;
            configurationDescriptor.InterfaceDescriptor = interfaceDescriptor;

            for(int i=0; i<NumberOfPorts; i++)
            {
                ports[i] = (uint)PortStatus.PortPower;
            }
        }

        public UsbHub(IMachine machine, byte nrPorts)
        {
            this.machine = machine;
            registeredDevices = new Dictionary<byte, IUSBPeripheral>();
            endpointDescriptor = new EndpointUSBDescriptor[3];
            NumberOfPorts = nrPorts;
            ports = new uint [NumberOfPorts];
            hubDescriptor.NbrPorts = NumberOfPorts;
            for(int i=0; i<NumberOfEndpoints; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            fillEndpointsDescriptors(endpointDescriptor);
            interfaceDescriptor[0].EndpointDescriptor = endpointDescriptor;
            configurationDescriptor.InterfaceDescriptor = interfaceDescriptor;

            for(int i=0; i<NumberOfPorts; i++)
            {
                ports[i] = (uint)PortStatus.PortPower;
            }
        }

        public IUSBHub Parent
        {
            get;
            set;
        }


        public void Register(IUSBPeripheral peripheral, USBRegistrationPoint registrationPoint)
        {
            AttachDevice(peripheral, registrationPoint.Address.Value);
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
            registrationPoints.Add(peripheral, registrationPoint);
        }

        public void Register(IUSBHub peripheral, USBRegistrationPoint registrationPoint)
        {
            peripheral.Connected += Connected;
            peripheral.Disconnected += Disconnected;
            peripheral.RegisterHub += RegisterHub;
            peripheral.ActiveDevice += ActiveDevice;
            AttachDevice(peripheral, registrationPoint.Address.Value);
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public void Unregister(IUSBHub peripheral)
        {
            var port = registeredDevices.FirstOrDefault(x => x.Value == peripheral).Key;
            DetachDevice(port);
            machine.UnregisterAsAChildOf(this, peripheral);
            registrationPoints.Remove(peripheral);
            registeredDevices.Remove(port);
        }

        public void Unregister(IUSBPeripheral peripheral)
        {
            DetachDevice(registrationPoints[peripheral].Address.Value);
            machine.UnregisterAsAChildOf(this, registrationPoints[peripheral]);
            registrationPoints.Remove(peripheral);
        }

        public IEnumerable<USBRegistrationPoint> GetRegistrationPoints(IUSBPeripheral peripheral)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<IRegistered<IUSBPeripheral, USBRegistrationPoint>> Children
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        public IUSBPeripheral GetDevice(byte port)
        {
            if(registeredDevices.ContainsKey(port))
            {
                return registeredDevices[port];
            }
            else
            {
                return  null;
            }
        }

        public void AttachDevice(IUSBPeripheral device, byte port)
        {
            if (device.GetSpeed()==USBDeviceSpeed.High)
                ports[port - 1] |= (uint)PortStatus.CPortConnection | (uint)PortStatus.PortConnection | (uint)PortStatus.PortHighSpeed;
            else
                if (device.GetSpeed()==USBDeviceSpeed.Low)
                    ports[port - 1] |= (uint)PortStatus.CPortConnection | (uint)PortStatus.PortConnection | (uint)PortStatus.PortLowSpeed;
            else
                ports[port - 1] |= (uint)PortStatus.CPortConnection | (uint)PortStatus.PortConnection;

            registeredDevices.Add(port, device);

            changed = true;
            /* Connect device to controller and send interrupt*/
            var connected = Connected;
            if(connected != null)
            {
                connected(DeviceAddress);
            }
        }

        public void DetachDevice(byte port)
        {
            changed = true;
            ports[port - 1] &= (~(uint)PortStatus.PortConnection);
            ports[port - 1] |= ((uint)PortStatus.CPortConnection);
            if((ports[port - 1] & (uint)PortStatus.PortEnable) != 0)
            {
                ports[port - 1] &= ~((uint)PortStatus.PortEnable);
                ports[port - 1] |= ((uint)PortStatus.CPortEnable);
            }
            /* Disconnect device from controller and send interrupt*/
            Disconnected(DeviceAddress, registeredDevices[port].GetAddress());
            /* Unregister device from controller */
            registeredDevices.Remove(port);
        }

        public void Reset()
        {
            for(int i=0; i<NumberOfPorts; i++)
            {
                if ((ports[i] & (uint)PortStatus.PortEnable)!=0)
                    ports[i] = (uint)PortStatus.CPortConnection | (uint)PortStatus.PortConnection | 1 << 10;
            }
        }

        private readonly Dictionary<IUSBPeripheral, USBRegistrationPoint> registrationPoints = new Dictionary<IUSBPeripheral, USBRegistrationPoint>();
        private Dictionary <byte,IUSBPeripheral> registeredDevices;

        #region IUSBDevice implementation
        public byte[] ProcessClassGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            byte[] returnValue;
            //MessageRecipient recipient = (MessageRecipient)(setupPacket.requestType & 0x3);
            ushort index = setupPacket.index;
            byte request = setupPacket.request;
            //ushort value = setupPacket.value;
            returnValue = new byte[4];
            switch((HUBRequestCode)request)
            {
            case HUBRequestCode.GetStatus:
                if(index == 0)
                {
                    returnValue[0] = 0;
                    returnValue[1] = 0;
                    returnValue[2] = 0;
                    returnValue[3] = 0;
                    controlPacket = returnValue;
                    return controlPacket;
                }
                else
                {
                    if(index - 1 < NumberOfPorts)
                    {
                        returnValue[0] = (byte)ports[index - 1];
                        returnValue[1] = (byte)(ports[index - 1] >> 8);
                        returnValue[2] = (byte)(ports[index - 1] >> 16);
                        returnValue[3] = (byte)(ports[index - 1] >> 24);
                    }
                    else
                    {
                        return null;
                    }
                    controlPacket = returnValue;
                    return controlPacket;
                }
            case HUBRequestCode.GetHubDescriptor:
                controlPacket = hubDescriptor.ToArray();
                return controlPacket;
            default:
                controlPacket = new byte[0];
                this.Log(LogLevel.Warning, "Unsupported HUB ProcessClassGet request!!!");
                return controlPacket;
            }
        }

        public void ProcessClassSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            //MessageRecipient recipient = (MessageRecipient)(setupPacket.requestType & 0x3);
            ushort index = setupPacket.index;
            byte request = setupPacket.request;
            ushort value = setupPacket.value;
            switch((HUBRequestCode)request)
            {
            case HUBRequestCode.ClearHubFeature:
                if(index > 0)
                {
                    switch((PortFeature)value)
                    {
                    case PortFeature.CPortSuspend:
                        ports[index - 1] = (uint)(ports[index - 1] & (~((uint)PortStatus.CPortSuspend)));
                        break;
                    case PortFeature.CPortOverCurrent:
                        ports[index - 1] = (uint)(ports[index - 1] & (~((uint)PortStatus.CPortOverCurrent)));
                        break;
                    case PortFeature.CPortEnable:
                        ports[index - 1] = (uint)(ports[index - 1] & (~((uint)PortStatus.CPortEnable)));
                        break;
                    case PortFeature.PortEnable:
                        ports[index - 1] = (uint)(ports[index - 1] & (uint)PortStatus.PortEnable);
                        break;
                    case PortFeature.PortSuspend:
                        ports[index - 1] = (uint)(ports[index - 1] & (uint)PortStatus.PortSuspend);
                        break;
                    case PortFeature.CPortConnection:
                        ports[index - 1] = (uint)(ports[index - 1] & (~((uint)PortStatus.CPortConnection)));
                        break;
                    case PortFeature.CPortReset:
                        ActiveDevice(this.GetDevice((byte)(index)));
                        ports[index - 1] = (uint)(ports[index - 1] & (~((uint)PortStatus.CPortReset)));
                        break;
                    default:
                        this.Log(LogLevel.Warning, "Unsupported ClearHubFeature request!!!");
                        break;
                    }
                }
                break;
            case HUBRequestCode.SetHubFeature:
                if(index > 0)
                {
                    if((PortFeature)value == PortFeature.PortReset)
                    {
                        IUSBPeripheral device = GetDevice((byte)(index));
                        ports[index - 1] |= (uint)PortStatus.CPortReset;
                        ports[index - 1] |= (uint)PortStatus.PortEnable;
                        if(device != null)
                        {
                            device.SetAddress(0);
                            device.Reset();
                        }
                    }
                    else if((PortFeature)value == PortFeature.PortSuspend)
                    {
                        ports[index - 1] |= (uint)PortStatus.PortSuspend;
                    }
                }

                break;
            default:
                this.Log(LogLevel.Warning, "Unsupported HUB ProcessClassSet request!!!");
                break;
            }
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
            throw new NotImplementedException();
        }

        public byte[] GetConfiguration()
        {
            throw new NotImplementedException();
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
            default:
                this.Log(LogLevel.Warning, "Unsupported HUB GetDescriptor request!!!");
                return null;
            }
        }

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
                goto default;
            default:
                arr[0] = 0;
                break;
            }
            controlPacket = new byte[arr.Length];
            arr.CopyTo(controlPacket, 0);
            return arr;
        }

        public void SetAddress(uint address)
        {
            DeviceAddress = address;
        }

        public void SetConfiguration(USBPacket packet, USBSetupPacket setupPacket)
        {
            //throw new NotImplementedException();
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
            return;
        }

        public int ii = 0;
        bool changed = false;

        public byte[] WriteInterrupt(USBPacket packet)
        {

            byte  [] buf = new byte[8];
            buf[0] = 0x00;
            buf[1] = 0x00;
            buf[2] = 0x00;
            buf[3] = 0x00;
            buf[4] = 0x00;
            buf[5] = 0x00;
            buf[6] = 0x00;
            buf[7] = 0x00;

            int status = 0;
            for(int i = 0; i < NumberOfPorts; i++)
            {
                if(((ports[i] >> 16) & 0xffff) != 0)
                {
                    status |= (1 << (i + 1));
                }
            }
            if(status != 0)
            {
                for(int i = 0; i < 1; i++)
                {
                    buf[i] = (byte)(status >> (8 * i));
                }

                if(changed == true)
                {
                    changed = false;
                    return buf;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public byte[] ProcessVendorGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }

        public void ProcessVendorSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
        }
        #endregion


        #region Device constans
        private const byte maxLun = 0;
        private const byte NumberOfEndpoints = 1;
        private const ushort EnglishLangId = 0x09;
        #endregion



        #region USB descriptors

        private ConfigurationUSBDescriptor configurationDescriptor = new ConfigurationUSBDescriptor()
        {
            ConfigurationIndex = 0,
            SelfPowered = true,
            NumberOfInterfaces = 1,
            RemoteWakeup = true,
            MaxPower = 10, //500mA
            ConfigurationValue = 1
        };
        private ConfigurationUSBDescriptor otherConfigurationDescriptor = new ConfigurationUSBDescriptor();
        private StringUSBDescriptor stringDescriptor = null;
        private StandardUSBDescriptor deviceDescriptor = new StandardUSBDescriptor
        {
            DeviceClass=0x09,//specified in interface descritor
            DeviceSubClass = 0x00,//specified in interface descritor
            USB = 0x0200,
            DeviceProtocol = 0x01,//specified in interface descritor
            MaxPacketSize = 64,
            VendorId = 0x05e3,
            ProductId = 0x0608,
            Device = 0x0901,
            ManufacturerIndex = 0,
            ProductIndex = 0,
            SerialNumberIndex = 0,
            NumberOfConfigurations = 1
        };
        private DeviceQualifierUSBDescriptor deviceQualifierDescriptor = new DeviceQualifierUSBDescriptor();
        private EndpointUSBDescriptor[] endpointDescriptor;
        private InterfaceUSBDescriptor[] interfaceDescriptor = new[]{new InterfaceUSBDescriptor
        {
            AlternateSetting = 0,
            InterfaceNumber = 0,
            NumberOfEndpoints = NumberOfEndpoints,
            InterfaceClass = 0x09, //vendor specific
            InterfaceProtocol = 0x01,
            InterfaceSubClass = 0x00,
            InterfaceIndex = 0
        }
        };
        private Dictionary<ushort, string[]> stringValues = new Dictionary<ushort, string[]>()
        {
            {EnglishLangId, new string[]{
                    "",
                    "HUB USB",
                    "0xALLMAN",
                    "Configuration",
                    "AntMicro"
                }}
        };
        private HubUSBDescriptor hubDescriptor = new HubUSBDescriptor
        {
                NbrPorts = (byte)3,
                HubCharacteristics=0x0089,
                PwrOn2PwrGood =0x10,
                HubContrCurrent = 0x00
        };

        private void fillEndpointsDescriptors(EndpointUSBDescriptor[] endpointDesc)
        {
            endpointDesc[0].EndpointNumber = 1;
            endpointDesc[0].InEnpoint = true;
            endpointDesc[0].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Interrupt;
            endpointDesc[0].MaxPacketSize = 0x1;
            endpointDesc[0].SynchronizationType = EndpointUSBDescriptor.SynchronizationTypeEnum.NoSynchronization;
            endpointDesc[0].UsageType = EndpointUSBDescriptor.UsageTypeEnum.Data;
            endpointDesc[0].Interval = 12;

        }
        public class HubUSBDescriptor:USBDescriptor
        {
            public HubUSBDescriptor()
            {
                Type = (DescriptorType)0x29;
                Length = 0x9;
            }

            public byte NbrPorts { get; set; }

            public ushort HubCharacteristics { get; set; }

            public byte PwrOn2PwrGood { get; set; }

            public byte HubContrCurrent { get; set; }

            public override byte[] ToArray()
            {
                var arr = base.ToArray();
                arr[0x2] = NbrPorts;
                arr[0x3] = HubCharacteristics.LoByte();
                arr[0x4] = HubCharacteristics.HiByte();
                arr[0x5] = PwrOn2PwrGood;
                arr[0x6] = HubContrCurrent;
                arr[0x7] = 0x00;
                arr[0x8] = 0xff;
                return arr;
            }
        }

        private enum HUBRequestCode
        {
            GetStatus = 0x00,
            ClearHubFeature=0x01,
            SetHubFeature = 0x03,
            GetHubDescriptor = 0x06
        }

        private enum PortFeature
        {
            PortConnection=0,
            PortEnable=1,
            PortSuspend=2,
            PortOverCurrent=3,
            PortReset=4,
            PortPower=8,
            PortLowSpeed1=9,
            CPortConnection=16,
            CPortEnable=17,
            CPortSuspend=18,
            CPortOverCurrent=19,
            CPortReset=20
        }

        private enum PortStatus
        {
            PortConnection=0x0001,
            PortEnable=0x0002,
            PortSuspend=0x0004,
            PortOverCurrent=0x0008,
            PortReset=0x0010,
            PortPower=0x0100,
            PortLowSpeed=0x0200,
            PortHighSpeed=0x0400,
            CPortConnection=0x10000,
            CPortEnable=0x20000,
            CPortSuspend=0x40000,
            CPortOverCurrent=0x80000,
            CPortReset=0x100000
        }
        public uint[] ports;
     #endregion

    }
}
