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
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class DummyUSBDevice : IUSBPeripheral
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

        public DummyUSBDevice()
        {
        }

        public USBDeviceSpeed GetSpeed()
        {
            return USBDeviceSpeed.Low;
        }

        public void WriteDataBulk(USBPacket packet)
        {

        }

        public void WriteDataControl(USBPacket packet)
        {
        }

        public void Reset()
        {
        }
        public uint GetAddress()
        {
            return 0;
        }
        public     byte GetTransferStatus()
        {
            return 0;
        }

        public byte[] WriteInterrupt(USBPacket packet)
        {
            return null;
        }

        public byte[] GetDataBulk(USBPacket packet)
        {
            return null;
        }

        public byte[] GetDataControl(USBPacket packet)
        {
            return null;
        }

        public byte[] GetDescriptor(USBPacket packet, USBSetupPacket setupPacket)
        {
            return null;
        }

        public byte[] ProcessClassGet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
        }

        public void ProcessClassSet(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new System.NotImplementedException();
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

        public void SetAddress(uint address)
        {

        }

        public void SetConfiguration(USBPacket packet, USBSetupPacket setupPacket)
        {
            throw new NotImplementedException();
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
            //throw new NotImplementedException ();
            this.Log(LogLevel.Info, "Bulk Data write");
        }
        #endregion

        #region descriptors
        private ConfigurationUSBDescriptor configurationDescriptor = new ConfigurationUSBDescriptor()
        {
            ConfigurationIndex = 3,
            SelfPowered = true
        };

 #endregion



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

