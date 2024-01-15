//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class RenesasRA_ICU : BasicDoubleWordPeripheral, IIRQController, IKnownSize
    {
        public RenesasRA_ICU(IMachine machine, IGPIOReceiver nvic) : base(machine)
        {
            // Type comparison like this is required due to NVIC model being in another project
            if(nvic.GetType().FullName != "Antmicro.Renode.Peripherals.IRQControllers.NVIC")
            {
                throw new ConstructionException($"{nvic.GetType()} is invalid type for NVIC");
            }

            this.nvic = nvic;

            MapEventToIRQ = new int?[NumberOfEvents];
            interruptPending = new IFlagRegisterField[NumberOfEvents];

            interruptTrigger = new IEnumRegisterField<InterruptTrigger>[NumberOfExternalInterrupts];
            previousPinState = new bool[NumberOfExternalInterrupts];

            DefineRegisters();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number <= 0 || number >= MapEventToIRQ.Length)
            {
                return;
            }

            if(MapEventToIRQ[number] == null)
            {
                // If event mapping is not registered by software, ignore incoming IRQ
                this.Log(LogLevel.Debug, "Unhandled IRQ request from 0x{0:X}", number);
                return;
            }

            var irqIndex = MapEventToIRQ[number].Value;
            if(number > NumberOfExternalInterrupts)
            {
                // Handle peripheral interrupt
                interruptPending[irqIndex].Value |= value;
                nvic.OnGPIO(irqIndex, value);
                return;
            }

            // Handle IRQn interrupt
            // As number is between 1 and NumberOfExternalInterrupts,
            // externalIrqNumber will be between 0 and NumberOfExternalInterrupts - 1
            var externalIrqNumber = number - 1;
            switch(interruptTrigger[externalIrqNumber].Value)
            {
                case InterruptTrigger.RisingEdge:
                    if(!(!previousPinState[externalIrqNumber] && value))
                    {
                        return;
                    }
                    break;

                case InterruptTrigger.FallingEdge:
                    if(!(previousPinState[externalIrqNumber] && !value))
                    {
                        return;
                    }
                    break;

                case InterruptTrigger.BothEdges:
                    if(!(previousPinState[externalIrqNumber] ^ value))
                    {
                        return;
                    }
                    break;

                case InterruptTrigger.ActiveLow:
                    if(value)
                    {
                        return;
                    }
                    break;
            }

            interruptPending[irqIndex].Value = true;
            nvic.OnGPIO(irqIndex, true);
            previousPinState[externalIrqNumber] = value;
        }

        public long Size => 0x1000;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public int?[] MapEventToIRQ { get; }

        private void UpdateInterrupts()
        {
            for(var i = 0; i < NumberOfExternalInterrupts; ++i)
            {
                if(interruptTrigger[i].Value == InterruptTrigger.ActiveLow && !previousPinState[i])
                {
                    var index = Array.FindIndex(MapEventToIRQ, val => val == i);
                    if(index != -1)
                    {
                        OnGPIO(index, false);
                        break;
                    }
                }
            }
        }

        private void DefineRegisters()
        {
            Registers.IRQControl0.DefineMany(this, NumberOfExternalInterrupts, (register, registerIndex) =>
            {
                register
                    .WithEnumField(0, 2, out interruptTrigger[registerIndex], name: $"IRQMD{registerIndex}")
                    .WithReservedBits(2, 2)
                    .WithTag($"FCLKSEL{registerIndex}", 4, 2)
                    .WithReservedBits(6, 1)
                    .WithTaggedFlag($"FLTEN{registerIndex}", 7)
                    .WithReservedBits(8, 24)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                ;
            });

            Registers.NMIPinInterruptControl.Define(this)
                .WithTaggedFlag("NMIMD", 0)
                .WithReservedBits(1, 3)
                .WithTag("NFCLKSEL", 4, 2)
                .WithReservedBits(6, 1)
                .WithTaggedFlag("NFLTEN", 7)
                .WithReservedBits(8, 24)
            ;

            Registers.NonMaskableInterruptEnable.Define(this)
                .WithTaggedFlag("IWDTEN", 0)
                .WithTaggedFlag("WDTEN", 1)
                .WithTaggedFlag("LVD1EN", 2)
                .WithTaggedFlag("LVD2EN", 3)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("OSTEN", 6)
                .WithTaggedFlag("NMIEN", 7)
                .WithTaggedFlag("RPEEN", 8)
                .WithTaggedFlag("RECCEN", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("BUSMEN", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("TZFEN", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("CPEEN", 15)
                .WithReservedBits(16, 16)
            ;

            Registers.NonMaskableInterruptStatusClear.Define(this)
                .WithTaggedFlag("IWDTCLR", 0)
                .WithTaggedFlag("WDTCLR", 1)
                .WithTaggedFlag("LVD1CLR", 2)
                .WithTaggedFlag("LVD2CLR", 3)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("OSTCLR", 6)
                .WithTaggedFlag("NMICLR", 7)
                .WithTaggedFlag("RPECLR", 8)
                .WithTaggedFlag("RECCCLR", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("BUSMCLR", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("TZFCLR", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("CPECLR", 15)
                .WithReservedBits(16, 16)
            ;

            Registers.NonMaskableInterruptStatus.Define(this)
                .WithTaggedFlag("IWDTST", 0)
                .WithTaggedFlag("WDTST", 1)
                .WithTaggedFlag("LVD1ST", 2)
                .WithTaggedFlag("LVD2ST", 3)
                .WithReservedBits(4, 2)
                .WithTaggedFlag("OSTST", 6)
                .WithTaggedFlag("NMIST", 7)
                .WithTaggedFlag("RPEST", 8)
                .WithTaggedFlag("RECCST", 9)
                .WithReservedBits(10, 1)
                .WithTaggedFlag("BUSMST", 11)
                .WithReservedBits(12, 1)
                .WithTaggedFlag("TZFST", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("CPEST", 15)
                .WithReservedBits(16, 16)
            ;

            Registers.WakeUpInterruptEnable0.Define(this)
                .WithTag("IRQWUPEN", 0, 16)
                .WithTaggedFlag("IWDTWUPEN", 16)
                .WithReservedBits(17, 1)
                .WithTaggedFlag("LVD1WUPEN", 18)
                .WithTaggedFlag("LVD2WUPEN", 19)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("RTCALMWUPEN", 24)
                .WithTaggedFlag("RTCPRDWUPEN", 25)
                .WithTaggedFlag("USBHSWUPEN", 26)
                .WithTaggedFlag("USBFS0WUPEN", 27)
                .WithTaggedFlag("AGT1UDWUPEN", 28)
                .WithTaggedFlag("AGT1CAWUPEN", 29)
                .WithTaggedFlag("AGT1CBWUPEN", 30)
                .WithTaggedFlag("IIC0WUPEN", 31)
            ;

            Registers.WakeUpinterruptenableregister1.Define(this)
                .WithTaggedFlag("AGT3UDWUPEN", 0)
                .WithTaggedFlag("AGT3CAWUPEN", 1)
                .WithTaggedFlag("AGT3CBWUPEN", 2)
                .WithReservedBits(3, 29)
            ;

            Registers.SYSEventLinkSetting.Define(this)
                .WithTag("SELSR0", 0, 16)
                .WithReservedBits(16, 16)
            ;

            Registers.DMACEventLinkSetting0.DefineMany(this, NumberOfDMACEvents, (register, registerIndex) =>
            {
                register
                    .WithTag($"DELS{registerIndex}", 0, 9)
                    .WithReservedBits(9, 7)
                    .WithTaggedFlag($"IR{registerIndex}", 16)
                    .WithReservedBits(17, 15)
                ;
            });

            Registers.ICUEventLinkSetting0.DefineMany(this, NumberOfNvicOutputs, (register, registerIndex) =>
            {
                register
                    .WithValueField(0, 9, name: $"IELS{registerIndex}",
                        valueProviderCallback: _ =>
                            (ulong)MapEventToIRQ
                                .Select((irqIndex, eventIndex) => irqIndex != null && irqIndex.Value == registerIndex ? eventIndex : 0)
                                .FirstOrDefault(eventIndex => eventIndex > 0),
                        writeCallback: (previousValue, value) =>
                        {
                            if(previousValue > 0)
                            {
                                MapEventToIRQ[previousValue] = null;
                            }
                            if(value > 0)
                            {
                                MapEventToIRQ[value] = registerIndex;
                            }
                        })
                    .WithReservedBits(9, 7)
                    .WithFlag(16, out interruptPending[registerIndex], FieldMode.WriteZeroToClear, name: $"IR{registerIndex}")
                    .WithReservedBits(17, 7)
                    .WithTaggedFlag($"DTCE{registerIndex}", 24)
                    .WithReservedBits(25, 7)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                ;
            });
        }

        private const int NumberOfExternalInterrupts = 16;
        private const int NumberOfEvents = 512;
        private const int NumberOfDMACEvents = 8;
        private const int NumberOfNvicOutputs = 96;

        private readonly IFlagRegisterField[] interruptPending;
        private readonly IEnumRegisterField<InterruptTrigger>[] interruptTrigger;
        private readonly bool[] previousPinState;

        private readonly IGPIOReceiver nvic;

        private enum InterruptTrigger
        {
            FallingEdge,
            RisingEdge,
            BothEdges,
            ActiveLow,
        }

        private enum Registers
        {
            IRQControl0 = 0x0,
            NMIPinInterruptControl = 0x100,
            NonMaskableInterruptEnable = 0x120,
            NonMaskableInterruptStatusClear = 0x130,
            NonMaskableInterruptStatus = 0x140,
            WakeUpInterruptEnable0 = 0x1a0,
            WakeUpinterruptenableregister1 = 0x1a4,
            SYSEventLinkSetting = 0x200,
            DMACEventLinkSetting0 = 0x280,
            ICUEventLinkSetting0 = 0x300,
        }
    }
}
