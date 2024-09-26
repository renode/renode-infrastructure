//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Collections;

namespace Antmicro.Renode.Peripherals.USB
{
    public class USBHost : SimpleContainerBase<IUSBDevice>
    {
        public USBHost(uint defaultDelay = 1000)
        {
            this.defaultDelay = defaultDelay;
            timeSource = EmulationManager.Instance.CurrentEmulation.MasterTimeSource; // We use time as a hack for now
            devices = new TwoWayDictionary<byte, IUSBDevice>();
            addressCounter = 2; // Zero is reserved, 1 is host, so we start from 2
        }

        public void Reset()
        {
            devices.Clear();
            addressCounter = 2;
        }

        public override void Register(IUSBDevice peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            TryInitializeConnectedDevice(peripheral);
        }

        public override void Unregister(IUSBDevice peripheral)
        {
            bool found = devices.TryGetValue(peripheral, out var address);
            if(found)
            {
                devices.Remove(peripheral);
                ChildCollection.Remove(address);
            }
        }

        protected virtual void DeviceEnumerated(IUSBDevice device)
        {
            // Intentionally left empty
        }

        protected bool TryGetDevice(byte address, out IUSBDevice device)
        {
            devices.TryGetValue(address, out device);
            return device != null;
        }

        protected void EnumerateDevice(IUSBDevice device)
        {
            SetAddress(device);
            GetDescriptor(device);
            SetConfiguration(device);
            // Note: We wait for endpoints to fully enable, because if we
            // do it too early, then the data will be lost
            ExecuteWithDelay(() => { DeviceEnumerated(device); });
        }

        private void GetDescriptor(IUSBDevice device)
        {
            var setupPacket = new SetupPacket();
            setupPacket.Recipient = PacketRecipient.Device;
            setupPacket.Type = PacketType.Standard;
            setupPacket.Request = (byte)StandardRequest.GetDescriptor;
            setupPacket.Direction = Core.USB.Direction.DeviceToHost;
            setupPacket.Value = 0x0200;
            setupPacket.Index = 0;
            setupPacket.Count = 9;
            device.USBCore.HandleSetupPacket(setupPacket, _ => {}, null);
        }

        private void SetAddress(IUSBDevice device)
        {
            var setupPacket = new SetupPacket();
            setupPacket.Recipient = PacketRecipient.Device;
            setupPacket.Type = PacketType.Standard;
            setupPacket.Request = (byte)StandardRequest.SetAddress;
            setupPacket.Direction = Core.USB.Direction.HostToDevice;
            setupPacket.Value = addressCounter;
            setupPacket.Index = 0;
            setupPacket.Count = 0;

            // This should respond with empty data packet, but we dont check it for now as it is not necessary
            device.USBCore.HandleSetupPacket(setupPacket, _ => 
            {
                devices.Add(addressCounter, device);
                addressCounter++;
            }, null);
        }

        private void SetConfiguration(IUSBDevice device)
        {
            var setupPacket = new SetupPacket();
            setupPacket.Recipient = PacketRecipient.Device;
            setupPacket.Type = PacketType.Standard;
            setupPacket.Request = (byte)StandardRequest.SetConfiguration;
            setupPacket.Direction = Core.USB.Direction.HostToDevice;
            setupPacket.Value = 1; // Set to first configuration for now
            setupPacket.Index = 0;
            setupPacket.Count = 0;
            device.USBCore.HandleSetupPacket(setupPacket, _ => {}, null);
        }

        private bool TryInitializeConnectedDevice(IUSBDevice peripheral)
        {
            lock(devices)
            {
                // 0 is special address for newly detected device
                // as soon as device get's detected it should get enumerated and get different address
                if(devices.Exists(0))
                {
                    return false;
                }
                // Add to child collection. It's not yet fully connected (not in devices)
                // When the enumeration fails, or device is needed before enumeration (e.g CDC ACM UART)
                // then we can still access the device and it doesn't get lost.
                ChildCollection.Add(addressCounter, peripheral);
                // Initialize device with it's first configuration
                // Note: Delay here is necessary, as if we start to do anything before
                // the USB device gets to pullup, then we'll stall endpoints 
                ExecuteWithDelay(() => { EnumerateDevice(peripheral); });
                return true;
            }
        }

        private void ExecuteWithDelay(Action action)
        {
            var now = timeSource.ElapsedVirtualTime;
            var calculatedDelay = now + TimeInterval.FromMilliseconds(defaultDelay); 
            var calculatedTimestamp = new TimeStamp(calculatedDelay, timeSource.Domain);
            timeSource.ExecuteInSyncedState(_ =>
            {
                action();   
            }, calculatedTimestamp);
        }

        private byte addressCounter;
        private readonly TwoWayDictionary<byte, IUSBDevice> devices;
        private TimeSourceBase timeSource;
        private readonly uint defaultDelay;
    }
}
