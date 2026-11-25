//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2026 Gerzain Mata <leftger@gmail.com>
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.USB
{
    public class STM32_USB_OTG_HS : IDoubleWordPeripheral, IKnownSize
    {
        private readonly DoubleWordRegisterCollection registers;
        public GPIO IRQ { get; } = new GPIO();

        private bool softReset;
        private uint interruptStatus;
        private uint interruptMask;

        public STM32_USB_OTG_HS(IMachine machine)
        {
            var map = new Dictionary<long, DoubleWordRegister>
            {
                // GOTGCTL at 0x00
                [0x00] = new DoubleWordRegister(this)
                    .WithFlag(16, FieldMode.Read, name: "BVALOVAL", valueProviderCallback: _ => true), // VBUS valid

                // GAHBCFG at 0x08
                [0x08] = new DoubleWordRegister(this)
                    .WithFlag(0, name: "GINTMSK")
                    .WithFlag(5, name: "DMAEN"),

                // GUSBCFG at 0x0C
                [0x0C] = new DoubleWordRegister(this)
                    .WithValueField(0, 6, name: "TOCAL")
                    .WithFlag(6, name: "PHYSEL")
                    .WithValueField(10, 4, name: "TRDT")
                    .WithFlag(30, name: "FDMOD"), // Force Device Mode

                // GRSTCTL at 0x10
                [0x10] = new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write | FieldMode.Read, name: "CSFTRST",
                        valueProviderCallback: _ => softReset,
                        writeCallback: (_, val) => {
                            // Soft reset: self-clearing
                            softReset = false;
                        })
                    .WithFlag(31, FieldMode.Read, name: "AHBIDL", valueProviderCallback: _ => true),

                // GINTSTS at 0x14
                [0x14] = new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => interruptStatus,
                        writeCallback: (_, val) => {
                            // Write 1 to clear
                            interruptStatus &= ~(uint)val;
                            UpdateInterrupt();
                        }),

                // GINTMSK at 0x18
                [0x18] = new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        valueProviderCallback: _ => interruptMask,
                        changeCallback: (_, val) => {
                            interruptMask = (uint)val;
                            UpdateInterrupt();
                        }),

                // GRXFSIZ at 0x24
                [0x24] = new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "RXFDEP"),

                // GCCFG at 0x38
                [0x38] = new DoubleWordRegister(this)
                    .WithFlag(16, name: "PWRDWN")
                    .WithFlag(21, name: "VBDEN"),

                // CID at 0x40 (Core ID for STM32 OTG HS)
                [0x40] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "PRODUCT_ID", valueProviderCallback: _ => 0x00001200),

                // --- Device Mode Registers (0x800+) ---
                // DCFG at 0x800
                [0x800] = new DoubleWordRegister(this)
                    .WithValueField(0, 2, name: "DSPD")
                    .WithValueField(4, 7, name: "DAD"),

                // DCTL at 0x804
                [0x804] = new DoubleWordRegister(this)
                    .WithFlag(1, name: "SFTDISCON"),

                // DSTS at 0x808 (Device Status)
                [0x808] = new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Read, name: "SUSPSTS", valueProviderCallback: _ => false)
                    .WithValueField(1, 2, FieldMode.Read, name: "ENUMSPD", valueProviderCallback: _ => 3u), // Full Speed (30 MHz)

                // DIEPMSK at 0x810
                [0x810] = new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "INEPMSK"),

                // DOEPMSK at 0x814
                [0x814] = new DoubleWordRegister(this)
                    .WithValueField(0, 16, name: "OUTEPMSK"),

                // DAINT at 0x818
                [0x818] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, name: "DAINT", valueProviderCallback: _ => 0u),

                // DAINTMSK at 0x81C
                [0x81C] = new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DAINTMSK")
            };

            // Device IN Endpoint 0 CTL at 0x900
            map[0x900] = new DoubleWordRegister(this)
                .WithValueField(0, 2, name: "MPSIZ")
                .WithFlag(15, FieldMode.Read, name: "USBACTEP", valueProviderCallback: _ => true)
                .WithFlag(31, name: "EPENA");

            // Device OUT Endpoint 0 CTL at 0xB00
            map[0xB00] = new DoubleWordRegister(this)
                .WithValueField(0, 2, name: "MPSIZ")
                .WithFlag(15, FieldMode.Read, name: "USBACTEP", valueProviderCallback: _ => true)
                .WithFlag(31, name: "EPENA");

            registers = new DoubleWordRegisterCollection(this, map);
        }

        private void UpdateInterrupt()
        {
            IRQ.Set((interruptStatus & interruptMask) != 0);
        }

        public uint ReadDoubleWord(long offset) => registers.Read(offset);
        public void WriteDoubleWord(long offset, uint value) => registers.Write(offset, value);
        public void Reset()
        {
            softReset = false;
            interruptStatus = 0;
            interruptMask = 0;
            IRQ.Set(false);
            registers.Reset();
        }

        public long Size => 0x40000; // 256 KB window
    }
}
