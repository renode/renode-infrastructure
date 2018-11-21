//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    [Icon("usb")]
    public interface IUSBPeripheral : IPeripheral
    {
        void ClearFeature(USBPacket packet, USBSetupPacket setupPacket);

        byte[] GetConfiguration();

        void SetAddress(uint address);

        byte[] GetInterface(USBPacket packet, USBSetupPacket setupPacket);

        byte[] GetStatus(USBPacket packet, USBSetupPacket setupPacket);

        void SetConfiguration(USBPacket packet, USBSetupPacket setupPacket);

        void SetDescriptor(USBPacket packet, USBSetupPacket setupPacket);

        void SetFeature(USBPacket packet, USBSetupPacket setupPacket);

        void SetInterface(USBPacket packet, USBSetupPacket setupPacket);

        byte[] ProcessVendorGet(USBPacket packet, USBSetupPacket setupPacket);

        void ProcessVendorSet(USBPacket packet, USBSetupPacket setupPacket);

        byte[] ProcessClassGet(USBPacket packet, USBSetupPacket setupPacket);

        void ProcessClassSet(USBPacket packet, USBSetupPacket setupPacket);

        void SetDataToggle(byte endpointNumber);

        void CleanDataToggle(byte endpointNumber);

        bool GetDataToggle(byte endpointNumber);

        void ToggleDataToggle(byte endpointNumber);

        uint GetAddress();

        USBDeviceSpeed GetSpeed();

        byte[] WriteInterrupt(USBPacket packet);

        byte[] GetDataBulk(USBPacket packet);

        byte[] GetDataControl(USBPacket packet);

        byte GetTransferStatus();

        byte[] GetDescriptor(USBPacket packet, USBSetupPacket setupPacket);

        void WriteDataBulk(USBPacket packet);

        void WriteDataControl(USBPacket packet);

        event Action <uint> SendInterrupt ;

        event Action <uint> SendPacket ;
    }
}
