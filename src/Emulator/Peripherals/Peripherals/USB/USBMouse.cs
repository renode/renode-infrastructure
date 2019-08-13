//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Input;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Core.USB.HID;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Extensions.Utilities.USBIP;

namespace Antmicro.Renode.Peripherals.USB
{
    public static class USBMouseExtensions
    {
        //
        // DISCLAIMER:
        //
        // Those are helper methods needed because `host` object (part of which `USBIPServer` is)
        // is not fully supported in monitor/repl.
        //
        public static void AttachUSBMouse(this USBIPServer usbController, int? port = null)
        {
            if(usbController.Children.Where(m => m.Peripheral.GetType() == typeof(USBMouse)).Count() != 0)
            {
                throw new RecoverableException("There is already a USB mouse connected to the USB/IP server");
            }

            usbController.Register(new USBMouse(), port);
        }

        public static void MoveMouse(this USBIPServer usbController, int x, int y)
        {
            var mouse = usbController.Children.Where(m => m.Peripheral.GetType() == typeof(USBMouse)).Select(m => m.Peripheral).Cast<USBMouse>().FirstOrDefault();
            if(mouse == null)
            {
                throw new RecoverableException("No USB mouse attached to the host. Did you forget to call 'host AttachUSBMouse'?");
            }

            mouse.MoveBy(x, y);
        }
    }

    public class USBMouse : IUSBDevice, IRelativePositionPointerInput
    {
        public USBMouse()
        {
            USBCore = new USBDeviceCore(this)
                .WithConfiguration(configure: c =>
                    c.WithInterface(new Core.USB.HID.Interface(this, 0,
                        subClassCode: (byte)Core.USB.HID.SubclassCode.BootInterfaceSubclass,
                        protocol: (byte)Core.USB.HID.Protocol.Mouse,
                        reportDescriptor: new Core.USB.HID.ReportDescriptor(ReportHidDescriptor)),
                        configure: i =>
                            i.WithEndpoint(
                                Direction.DeviceToHost,
                                EndpointTransferType.Interrupt,
                                maximumPacketSize: 0x4,
                                interval: 0xa,
                                createdEndpoint: out endpoint)));
        }

        public void Reset()
        {
            buttonState = 0;
            USBCore.Reset();
        }

        public void MoveBy(int x, int y)
        {
            using(var p = endpoint.PreparePacket())
            {
                p.Add((byte)buttonState);
                p.Add((byte)x.Clamp(-127, 127));
                p.Add((byte)y.Clamp(-127, 127));
                p.Add(0);
            }
        }

        public void Press(MouseButton button = MouseButton.Left)
        {
            buttonState = button;
            SendButtonState();
        }

        public void Release(MouseButton button = MouseButton.Left)
        {
            buttonState = 0;
            SendButtonState();
        }

        private void SendButtonState()
        {
            using(var p = endpoint.PreparePacket())
            {
                p.Add((byte)buttonState);
                p.Add(0);
                p.Add(0);
                p.Add(0);
            }
        }

        public USBDeviceCore USBCore { get; }

        private MouseButton buttonState;

        private USBEndpoint endpoint;

        private readonly byte[] ReportHidDescriptor = new byte[]
        {
            0x05, 0x01, 0x09, 0x02, 0xA1, 0x01, 0x09, 0x01,
            0xA1, 0x00, 0x05, 0x09, 0x19, 0x01, 0x29, 0x03,
            0x15, 0x00, 0x25, 0x01, 0x95, 0x08, 0x75, 0x01,
            0x81, 0x02, 0x05, 0x01, 0x09, 0x30, 0x09, 0x31,
            0x09, 0x38, 0x15, 0x81, 0x25, 0x7F, 0x75, 0x08,
            0x95, 0x03, 0x81, 0x06, 0xC0, 0xC0
        };
    }
}

