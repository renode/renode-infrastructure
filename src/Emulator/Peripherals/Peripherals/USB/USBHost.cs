//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
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

        protected virtual void DeviceEnumerated(IUSBConnection device)
        {
            // Intentionally left empty
        }

        protected bool TryGetDevice(byte address, out IUSBDevice device)
        {
            devices.TryGetValue(address, out device);
            return device != null;
        }

        // A device that can't or won't service a request answers it with a STALL handshake
        // (USB 2.0 8.5.3.4 / 9.2.7). Both steps below are issued with the stall-aware overload
        // of `SetupWrite`, so such an answer stops the enumeration with a warning naming the
        // step instead of leaving it silently stuck waiting for a status stage.
        protected void EnumerateDevice(IUSBDevice device)
        {
            var conn = device.ConnectUSB();
            addressCounter += 1;
            // Note: We wait for endpoints to fully enable, because if we
            // do it too early, then the data will be lost
            ExecuteWithDelay(() =>
            {
                var ep0 = conn.ConnectEndpointSetup(0);
                SetAddress(ep0, addressCounter, addressAccepted =>
                {
                    if(!addressAccepted)
                    {
                        EnumerationFailed(device, "SET_ADDRESS");
                        return;
                    }
                    // USB configurations are 1-based (0 means unconfigured),
                    // so choose the first configuration, which all devices should have
                    SetConfiguration(ep0, 1, configurationAccepted =>
                    {
                        if(!configurationAccepted)
                        {
                            EnumerationFailed(device, "SET_CONFIGURATION");
                            return;
                        }
                        DeviceEnumerated(conn);
                    });
                });
            });
        }

        private void EnumerationFailed(IUSBDevice device, string step)
        {
            this.Log(LogLevel.Warning, "USB enumeration of {0} failed: the device stalled {1}", device, step);
        }

        private static void SetAddress(IUSBPipeSetup ep0, byte address, Action<bool> callback)
        {
            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Standard,
                Direction = Core.USB.Direction.HostToDevice,
                Request = (byte)StandardRequest.SetAddress,
                Value = address,
                Index = 0,
                Count = 0
            };
            ep0.SetupWrite(setupPacket, null, callback);
        }

        private static void SetConfiguration(IUSBPipeSetup ep0, byte configuration, Action<bool> callback)
        {
            var setupPacket = new SetupPacket
            {
                Recipient = PacketRecipient.Device,
                Type = PacketType.Standard,
                Direction = Core.USB.Direction.HostToDevice,
                Request = (byte)StandardRequest.SetConfiguration,
                Value = configuration,
                Index = 0,
                Count = 0
            };
            ep0.SetupWrite(setupPacket, null, callback);
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
        private readonly TimeSourceBase timeSource;
        private readonly TwoWayDictionary<byte, IUSBDevice> devices;
        private readonly uint defaultDelay;
    }
}
