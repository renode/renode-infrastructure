//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.GPIOPort;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class NRF_USBREG : BasicDoubleWordPeripheral, IKnownSize
    {
        public NRF_USBREG(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            IRQ.Set(false);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        private void SetInterrupt(bool irq)
        {
            this.Log(LogLevel.Noisy, "Setting IRQ: {0}", irq);
            IRQ.Set(irq);
        }

        private void DefineRegisters()
        {
            // This is hacky way of setting interrupts, but it's only needed
            // for usb pullup for nRF5340, which works with this
            Registers.EventsUsbDetected.Define(this)
                .WithFlag(0, 
                    valueProviderCallback: _ => true,
                    writeCallback: (_, __) => SetInterrupt(false),
                    name: "USBDETECTED")
                .WithReservedBits(1, 31);

            Registers.EventsUsbPowerReady.Define(this)
                .WithFlag(0, 
                    valueProviderCallback: _ => true, 
                    writeCallback: (_, __) => SetInterrupt(false),
                    name: "USBPWRRDY")
                .WithReservedBits(1, 31);

            Registers.InterruptSet.Define(this)
                .WithTaggedFlag(name: "USBDETECTED", 0)
                .WithTaggedFlag(name: "USBREMOVED", 1)
                .WithFlag(2,
                    valueProviderCallback: _ => true,
                    writeCallback: (_, __) => SetInterrupt(true), 
                    name: "USBPWRRDY")
                .WithReservedBits(3, 29);

            Registers.UsbRegisterStatus.Define(this)
                .WithFlag(0, 
                    FieldMode.Read,
                    valueProviderCallback: _ => true,
                    name: "VBUSDETECT")
                .WithFlag(1, 
                    FieldMode.Read,
                    valueProviderCallback: _ => true,
                    name: "OUTPUTRDY")
                .WithReservedBits(2, 30);
        }

        private enum Registers
        {
            EventsUsbDetected = 0x100,
            EventsUsbPowerReady = 0x108,
            InterruptSet = 0x304,
            UsbRegisterStatus = 0x400,
        }
    }
}
