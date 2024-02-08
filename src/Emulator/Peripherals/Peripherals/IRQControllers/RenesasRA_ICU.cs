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
        public RenesasRA_ICU(IMachine machine, IGPIOReceiver nvic,
            uint numberOfExternalInterrupts = DefaultNumberOfExternalInterrupts,
            uint highestEventNumber = DefaultHighestEventNumber,
            uint numberOfNVICOutputs = DefaultNumberOfNVICOutputs) : base(machine)
        {
            // Type comparison like this is required due to NVIC model being in another project
            if(nvic.GetType().FullName != "Antmicro.Renode.Peripherals.IRQControllers.NVIC")
            {
                throw new ConstructionException($"{nvic.GetType()} is invalid type for NVIC");
            }

            var numberOfEvents = highestEventNumber + 1;
            if(numberOfEvents < numberOfExternalInterrupts)
            {
                throw new ConstructionException($"The number of events ({numberOfEvents}) is lower than number of external interrupts ({numberOfExternalInterrupts})");
            }

            this.nvic = nvic;

            interruptsForEvent = Enumerable.Range(0, (int)numberOfEvents).Select(_ => new HashSet<int>()).ToArray();
            latestEventState = new bool[numberOfEvents];
            externalInterruptTrigger = new IEnumRegisterField<InterruptTrigger>[numberOfExternalInterrupts];
            interruptEventLink = new IValueRegisterField[numberOfNVICOutputs];
            interruptPending = new IFlagRegisterField[numberOfNVICOutputs];

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var irqs in interruptsForEvent)
            {
                irqs.Clear();
            }
            Array.Clear(latestEventState, 0, latestEventState.Length);
        }

        public void OnGPIO(int eventIndex, bool state)
        {
            if(eventIndex >= latestEventState.Length)
            {
                this.Log(LogLevel.Warning, "Trying to update a state of event of index 0x{0:x}, which is larger than declared number of events", eventIndex);
                return;
            }

            UpdateEventAndInterrupts(eventIndex, state);
        }

        public long Size => 0x1000;

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private int GetEventForInterruptIndex(int irqIndex)
        {
            var eventIndex = Array.FindIndex(interruptsForEvent, irqs => irqs.Contains(irqIndex));
            if(eventIndex == -1)
            {
                return NoEventIndex;
            }
            return eventIndex;
        }

        private int GetEventForEventLink(int irqIndex, ulong eventLink)
        {
            return (int)eventLink;
        }

        private bool IsEventTriggered(int eventIndex, bool previousState, bool state)
        {
            if(eventIndex == NoEventIndex)
            {
                // There is no event with index 0.
                return false;
            }

            if(eventIndex > externalInterruptTrigger.Length)
            {
                // Handle an event from a peripheral
                return state;
            }

            // Handle an IRQn (an external interrupt)
            // As number is between 1 and NumberOfExternalInterrupts,
            // externalIrqNumber will be between 0 and NumberOfExternalInterrupts - 1.
            var externalIrqNumber = eventIndex - 1;
            var trigger = externalInterruptTrigger[externalIrqNumber].Value;
            switch(trigger)
            {
                case InterruptTrigger.RisingEdge:
                    return !previousState && state;
                case InterruptTrigger.FallingEdge:
                    return previousState && !state;
                case InterruptTrigger.BothEdges:
                    return previousState != state;
                case InterruptTrigger.ActiveLow:
                    return !state;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown value of interrupt trigger {trigger}");
            }
        }

        private void UpdateEventAndInterrupts(int eventIndex, bool? newEventState = null, ICollection<int> interruptIndexes = null)
        {
            var previousState = latestEventState[eventIndex];
            // If newEventState isn't passed just keep existing state.
            var newState = newEventState ?? previousState;
            latestEventState[eventIndex] = newState;

            // Update the passed list of interrupts or all linked to the event.
            var irqs = interruptIndexes ?? interruptsForEvent[eventIndex];
            var isTriggered = IsEventTriggered(eventIndex, previousState, newState);

            if(irqs.Count() == 0 && isTriggered)
            {
                // If event mapping is not registered by software and there is no list of interrupts to update, ignore incoming IRQ.
                this.Log(LogLevel.Warning, "Unhandled event request: 0x{0:X}. There is no configured link to the NVIC.", eventIndex);
                return;
            }

            foreach(var irqIndex in irqs)
            {
                // Latch signal and pass to the NVIC.
                interruptPending[irqIndex].Value |= isTriggered;
                nvic.OnGPIO(irqIndex, interruptPending[irqIndex].Value);
            }
        }

        private void DefineRegisters()
        {
            Registers.IRQControl0.DefineMany(this, (uint)externalInterruptTrigger.Length, (register, registerIndex) =>
            {
                register
                    .WithEnumField(0, 2, out externalInterruptTrigger[registerIndex], name: $"IRQMD{registerIndex}")
                    .WithReservedBits(2, 2)
                    .WithTag($"FCLKSEL{registerIndex}", 4, 2)
                    .WithReservedBits(6, 1)
                    .WithTaggedFlag($"FLTEN{registerIndex}", 7)
                    .WithReservedBits(8, 24)
                    .WithChangeCallback((_, __) => UpdateEventAndInterrupts(registerIndex + 1))
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

            Registers.DMACEventLinkSetting0.DefineMany(this, DefaultNumberOfDMACEvents, (register, registerIndex) =>
            {
                register
                    .WithTag($"DELS{registerIndex}", 0, 9)
                    .WithReservedBits(9, 7)
                    .WithTaggedFlag($"IR{registerIndex}", 16)
                    .WithReservedBits(17, 15)
                ;
            });

            var eventLinkRegisterLength = 9;
            Registers.ICUEventLinkSetting0.DefineMany(this, (uint)interruptEventLink.Length, (register, registerIndex) =>
            {
                register
                    .WithValueField(0, eventLinkRegisterLength, out interruptEventLink[registerIndex], name: $"IELS{registerIndex}",
                        changeCallback: (prevVal, val) =>
                        {
                            interruptsForEvent[GetEventForEventLink(registerIndex, prevVal)].Remove(registerIndex);
                            interruptsForEvent[GetEventForEventLink(registerIndex, val)].Add(registerIndex);
                        }
                    )
                    .WithReservedBits(eventLinkRegisterLength, 16 - eventLinkRegisterLength)
                    .WithFlag(16, out interruptPending[registerIndex], FieldMode.Read | FieldMode.WriteZeroToClear, name: $"IR{registerIndex}")
                    .WithReservedBits(17, 7)
                    .WithTaggedFlag($"DTCE{registerIndex}", 24)
                    .WithReservedBits(25, 7)
                    // If there is no event for the interrupt, the event with index 0 is returned, which is never triggered.
                    .WithChangeCallback((_, __) => UpdateEventAndInterrupts(GetEventForInterruptIndex(registerIndex), null, new int[] { registerIndex }))
                ;
            });
        }

        private readonly IEnumRegisterField<InterruptTrigger>[] externalInterruptTrigger;
        private readonly IValueRegisterField[] interruptEventLink;
        private readonly IFlagRegisterField[] interruptPending;

        private readonly ISet<int>[] interruptsForEvent;
        private readonly bool[] latestEventState;
        private readonly IGPIOReceiver nvic;

        private const int NoEventIndex = 0;
        private const uint DefaultNumberOfExternalInterrupts = 16;
        private const uint DefaultHighestEventNumber = 0x1DA;
        private const uint DefaultNumberOfDMACEvents = 8;
        private const uint DefaultNumberOfNVICOutputs = 96;

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
