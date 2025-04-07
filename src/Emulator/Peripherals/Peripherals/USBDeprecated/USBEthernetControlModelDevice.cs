//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Network;
using Antmicro.Renode.Peripherals.Network;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class USBEthernetControlModelDevice : USBEthernetControlModelDevicesSubclass, IUSBPeripheral, IMACInterface
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

        public USBEthernetControlModelDevice()
        {
        }

        public void ReceiveFrame(EthernetFrame frame)//when data is send to us
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

        public void Reset()
        {
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.Low;
        }

        public uint GetAddress()
        {
            return 0;
        }

        public byte[] ProcessClassGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public byte[] WriteInterrupt(USBPacket packet)
        {
            return null;
        }

        public byte[] GetDataBulk(USBPacket packet)
        {
            return null;
        }

        public void WriteDataBulk(USBPacket packet)
        {
        }

        public void WriteDataControl(USBPacket packet)
        {
        }

        public byte GetTransferStatus()
        {
            return 0;
        }

        public byte[] GetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            return null;
        }

        public byte[] GetDataControl(USBPacket packet)
        {
            return null;
        }

        public void ProcessClassSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SetDataToggle(byte endpointNumber)
        {
            throw new NotImplementedException();
        }

        public void SetAddress(uint address)
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
            throw new System.NotImplementedException();
        }

        public byte[] GetConfiguration()
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetStatus(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SetConfiguration(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SetFeature(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SetInterface(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void SyncFrame(uint endpointId)
        {
            throw new System.NotImplementedException();
        }

        public void WriteData(byte[] _) //data from system
        {
        }

        public byte[] GetData()
        {
            throw new System.NotImplementedException();
        }

        public MACAddress MAC { get; set; }

#pragma warning disable 0067
        public event Action<EthernetFrame> FrameReady;
#pragma warning restore 0067

        protected Dictionary <byte,byte[]> MulticastMacAdresses;

        private void SetEndpointsDescriptors()
        {
            endpointDescriptor = new EndpointUSBDescriptor[EndpointsAmount];
            for(byte i = 0; i < EndpointsAmount; i++)
            {
                endpointDescriptor[i] = new EndpointUSBDescriptor();
            }
            for(byte i = 0; i < EndpointsAmount; i++)
            {
                endpointDescriptor[i].EndpointNumber = i;
                endpointDescriptor[i].MaxPacketSize = 512;
                endpointDescriptor[i].SynchronizationType = EndpointUSBDescriptor.SynchronizationTypeEnum.Asynchronous;
                endpointDescriptor[i].UsageType = EndpointUSBDescriptor.UsageTypeEnum.Data;
            }
            endpointDescriptor[2].MaxPacketSize = 16;

            endpointDescriptor[0].InEnpoint = true;
            endpointDescriptor[1].InEnpoint = false;
            endpointDescriptor[2].InEnpoint = true;

            endpointDescriptor[0].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Bulk;
            endpointDescriptor[1].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Bulk;
            endpointDescriptor[2].TransferType = EndpointUSBDescriptor.TransferTypeEnum.Interrupt;
        }

        private void SetInterfaceDescriptors()
        {
            interfaceDescriptor = new InterfaceUSBDescriptor[InterfacesAmount];
            for(int i = 0; i < InterfacesAmount; i++)
            {
                interfaceDescriptor[i] = new InterfaceUSBDescriptor();
            }
        }

        private void InitializeMulticastList()
        {
            for(byte i = 0; i < DefaultNumberOfMulticastAdreses; i++)
            {
                var mac = new byte[]{0,0,0,0,0,0};
                MulticastMacAdresses.Add(i, mac);
            }
        }

        private void SetEthernetMulticastFilters(uint numberOfFilters, Dictionary<byte, byte[]> multicastAdresses)
        {
            for(byte i = 0; i < numberOfFilters; i++)
            {
                if(MulticastMacAdresses.ContainsKey(i))
                {//if position
                    MulticastMacAdresses[i] = multicastAdresses[i];
                }
                else
                {
                    MulticastMacAdresses.Add(i, multicastAdresses[i]);
                }
            }
        }

        private EndpointUSBDescriptor[] endpointDescriptor;
        private InterfaceUSBDescriptor[] interfaceDescriptor;

        private const byte InterfacesAmount = 0x01;
        private const byte EndpointsAmount = 0x03;
        private const byte Interval = 0x00;
        private const byte SubordinateInterfaceAmount = 0x01;
        private const byte CountryCodesAmount = 0x01;
        private const byte DefaultNumberOfMulticastAdreses = 0x01;
    }
}