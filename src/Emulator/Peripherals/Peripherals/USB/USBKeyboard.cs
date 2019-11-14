//
// Copyright (c) 2010 - 2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Input;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Extensions.Utilities.USBIP;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.USB
{
    public static class USBKeyboardExtensions
    {
        //
        // DISCLAIMER:
        //
        // Those are helper methods needed because `host` object (part of which `USBIPServer` is)
        // is not fully supported in monitor/repl.
        //
        public static void AttachUSBKeyboard(this USBIPServer usbController, int? port = null)
        {
            if(usbController.Children.Where(m => m.Peripheral.GetType() == typeof(USBKeyboard)).Count() != 0)
            {
                throw new RecoverableException("There is already a USB keyboard connected to the USB/IP server");
            }

            usbController.Register(new USBKeyboard(), port);
        }

        public static void KeyboardType(this USBIPServer usbController, string text)
        {
            var keyboard = usbController.Children.Where(m => m.Peripheral.GetType() == typeof(USBKeyboard)).Select(m => m.Peripheral).Cast<USBKeyboard>().FirstOrDefault();
            if(keyboard == null)
            {
                throw new RecoverableException("No USB keyboard attached to the host. Did you forget to call 'host AttachUSBKeyboard'?");
            }

            foreach(var character in text)
            {
                foreach(var scanCode in character.ToKeyScanCodes())
                {
                    keyboard.Press(scanCode);
                }

                foreach(var scanCode in character.ToKeyScanCodes())
                {
                    keyboard.Release(scanCode);
                }
            }
        }
    }

    public class USBKeyboard : IUSBDevice, IKeyboard
    {
        public USBKeyboard()
        {
            USBCore = new USBDeviceCore(this)
                .WithConfiguration(configure: c =>
                    c.WithInterface(new Core.USB.HID.Interface(this, 0,
                        subClassCode: (byte)Core.USB.HID.SubclassCode.BootInterfaceSubclass,
                        protocol: (byte)Core.USB.HID.Protocol.Keyboard,
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
            USBCore.Reset();
            modifiers = 0;
            pressedKey = 0;
        }

        public void Press(KeyScanCode scanCode)
        {
            this.Log(LogLevel.Noisy, "Pressing {0}", scanCode);
            if(!UpdateModifiers(scanCode, true))
            {
                pressedKey = (byte)((int)scanCode & 0x7f);
            }
            SendPacket();
        }

        public void Release(KeyScanCode scanCode)
        {
            this.Log(LogLevel.Noisy, "Releasing {0}", scanCode);
            if(!UpdateModifiers(scanCode, false))
            {
                pressedKey = 0;
            }
            SendPacket();
        }

        public USBDeviceCore USBCore { get; }

        private bool UpdateModifiers(KeyScanCode scanCode, bool set)
        {
            if(scanCode >= KeyScanCode.CtrlL && scanCode <= KeyScanCode.WinR)
            {
                BitHelper.SetBit(ref modifiers, (byte)((int)scanCode & 0x7), set);
                return true;
            }
            return false;
        }

        private void SendPacket()
        {
            using(var p = endpoint.PreparePacket())
            {
                p.Add(modifiers);
                p.Add(0); // reserved field
                p.Add(pressedKey);

                // keypresses 2-6 are currently not supported
                p.Add(0);
                p.Add(0);
                p.Add(0);
                p.Add(0);
                p.Add(0);
            }
        }

        private byte modifiers;
        private byte pressedKey;
        private USBEndpoint endpoint;

        private readonly byte[] ReportHidDescriptor = new byte[]
        {
            0x05, 0x01, 0x09, 0x06, 0xa1, 0x01, 0x75, 0x01,
            0x95, 0x08, 0x05, 0x07, 0x19, 0xe0, 0x29, 0xe7,
            0x15, 0x00, 0x25, 0x01, 0x81, 0x02, 0x95, 0x01,
            0x75, 0x08, 0x81, 0x01, 0x95, 0x05, 0x75, 0x01,
            0x05, 0x08, 0x19, 0x01, 0x29, 0x05, 0x91, 0x02,
            0x95, 0x01, 0x75, 0x03, 0x91, 0x01, 0x95, 0x06,
            0x75, 0x08, 0x15, 0x00, 0x25, 0xff, 0x05, 0x07,
            0x19, 0x00, 0x29, 0xff, 0x81, 0x00, 0xc0
        };
    }
}
