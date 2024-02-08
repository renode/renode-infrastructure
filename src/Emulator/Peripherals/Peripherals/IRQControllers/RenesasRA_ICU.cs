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
        public RenesasRA_ICU(IMachine machine, IGPIOReceiver nvic, EventToInterruptLinkType eventToInterruptLink = EventToInterruptLinkType.RA8,
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
            eventLinkType = eventToInterruptLink;

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
            // The IELS register in platforms other than RA2 directly indicate an event.
            if(eventLinkType != EventToInterruptLinkType.RA2)
            {
                return (int)eventLink;
            }
            // For RA2 also a group of interrupt takes account.
            var irqGroup = irqIndex % 8;
            return eventLinkRA2[eventLink, irqGroup];
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

            var eventLinkRegisterLength = eventLinkType == EventToInterruptLinkType.RA2 ? 5 : 9;
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

        private readonly EventToInterruptLinkType eventLinkType;
        private readonly ISet<int>[] interruptsForEvent;
        private readonly bool[] latestEventState;
        private readonly IGPIOReceiver nvic;

        private const int NoEventIndex = 0;
        private const uint DefaultNumberOfExternalInterrupts = 16;
        private const uint DefaultHighestEventNumber = 0x1DA;
        private const uint DefaultNumberOfDMACEvents = 8;
        private const uint DefaultNumberOfNVICOutputs = 96;

        private readonly int[,] eventLinkRA2 =
            {
                //  GROUP0          GROUP1          GROUP2          GROUP3          GROUP4          GROUP5          GROUP6          GROUP7          
                {
                //  IELS = 0x00
                //  NO_EVENT,       NO_EVENT,       NO_EVENT,       NO_EVENT,       NO_EVENT,       NO_EVENT,       NO_EVENT,       NO_EVENT
                    0x00,           0x00,           0x00,           0x00,           0x00,           0x00,           0x00,           0x00
                },
                {
                //  IELS = 0x01
                //  PORT_IRQ0,      PORT_IRQ1,      PORT_IRQ2,      PORT_IRQ3,      PORT_IRQ0,      PORT_IRQ1,      PORT_IRQ2,      PORT_IRQ3
                    0x01,           0x02,           0x03,           0x04,           0x01,           0x02,           0x03,           0x04
                },
                {
                //  IELS = 0x02
                //  DTC_COMPLETE,   LVD_LVD2,       FCU_FRDYI,      SYSTEM_SNZREQ,  DTC_COMPLETE,   LVD_LVD2,       FCU_FRDYI,      SYSTEM_SNZREQ
                    0x09,           0x0E,           0x0C,           0x10,           0x09,           0x0E,           0x0C,           0x10
                },
                {
                //  IELS = 0x03
                //  ICU_SNZCANCEL,  AGT1_AGTCMAI,   AGT1_AGTCMBI,   IWDT_NMIUNDF,   ICU_SNZCANCEL,  AGT1_AGTCMAI,   AGT1_AGTCMBI,   IWDT_NMIUNDF
                    0x0B,           0x15,           0x16,           0x17,           0x0B,           0x15,           0x16,           0x17
                },
                {
                //  IELS = 0x04
                //  LVD_LVD1,       RTC_ALM,        RTC_PRD,        RTC_CUP,        LVD_LVD1,       RTC_ALM,        RTC_PRD,        RTC_CUP
                    0x0D,           0x19,           0x1A,           0x1B,           0x0D,           0x19,           0x1A,           0x1B
                },
                {
                //  IELS = 0x05
                //  AGT1_AGTI,      ADC120_GBADI,   ADC120_CMPAI,   ADC120_CMPBI,   AGT1_AGTI,      ADC120_GBADI,   ADC120_CMPAI,   ADC120_CMPBI
                    0x14,           0x1D,           0x1E,           0x1F,           0x14,           0x1D,           0x1E,           0x1F
                },
                {
                //  IELS = 0x06
                //  WDT_NMIUNDF,    ADC120_WCMPUM,  IIC0_TEI,       IIC0_EEI,       WDT_NMIUNDF,    ADC120_WCMPUM,  IIC0_TEI,       IIC0_EEI
                    0x18,           0x21,           0x29,           0x2A,           0x18,           0x21,           0x29,           0x2A
                },
                {
                //  IELS = 0x07
                //  ADC120_ADI,     ACMP_LP1,       CTSU_CTSURD,    CTSU_CTSUFN,    ADC120_ADI,     ACMP_LP1,       CTSU_CTSURD,    CTSU_CTSUFN
                    0x1C,           0x24,           0x31,           0x32,           0x1C,           0x24,           0x31,           0x32
                },
                {
                //  IELS = 0x08
                //  ADC120_WCMPM,   IIC0_TXI,       CAC_MENDI,      CAC_OVFI,       ADC120_WCMPM,   IIC0_TXI,       CAC_MENDI,      CAC_OVFI
                    0x20,           0x28,           0x36,           0x37,           0x20,           0x28,           0x36,           0x37
                },
                {
                //  IELS = 0x09
                //  ACMP_LP0,       CTSU_CTSUWR,    CAN0_TXF,       CAN0_RXM,       ACMP_LP0,       CTSU_CTSUWR,    CAN0_TXF,       CAN0_RXM
                    0x23,           0x30,           0x3A,           0x3B,           0x23,           0x30,           0x3A,           0x3B
                },
                {
                //  IELS = 0x0A
                //  IIC0_RXI,       DOC_DOPCI,      ELC_SWEVT0,     ELC_SWEVT1,     IIC0_RXI,       DOC_DOPCI,      ELC_SWEVT0,     ELC_SWEVT1
                    0x27,           0x34,           0x3F,           0x40,           0x27,           0x34,           0x3F,           0x40
                },
                {
                //  IELS = 0x0B
                //  IIC0_WUI,       CAC_FERRI,      POEG_GROUP0,    POEG_GROUP1,    IIC0_WUI,       CAC_FERRI,      POEG_GROUP0,    POEG_GROUP1
                    0x2B,           0x35,           0x41,           0x42,           0x2B,           0x35,           0x41,           0x42
                },
                {
                //  IELS = 0x0C
                //  CAN0_ERS,       CAN0_RXF,       GPT0_CMPC,      GPT0_CMPD,      CAN0_ERS,       CAN0_RXF,       GPT0_CMPC,      GPT0_CMPD
                    0x38,           0x39,           0x48,           0x49,           0x38,           0x39,           0x48,           0x49
                },
                {
                //  IELS = 0x0D
                //  CAN0_TXM,       GPT0_CCMPB,     GPT2_CMPC,      GPT2_CMPD,      CAN0_TXM,       GPT0_CCMPB,     GPT2_CMPC,      GPT2_CMPD
                    0x3C,           0x47,           0x54,           0x55,           0x3C,           0x47,           0x54,           0x55
                },
                {
                //  IELS = 0x0E
                //  GPT0_CCMPA,     GPT0_UDF,       GPT2_OVF,       GPT2_UDF,       GPT0_CCMPA,     GPT0_UDF,       GPT2_OVF,       GPT2_UDF
                    0x46,           0x4B,           0x56,           0x57,           0x46,           0x4B,           0x56,           0x57
                },
                {
                //  IELS = 0x0F
                //  GPT0_OVF,       GPT2_CCMPB,     SCI0_TEI,       SCI0_ERI,       GPT0_OVF,       GPT2_CCMPB,     SCI0_TEI,       SCI0_ERI
                    0x4A,           0x53,           0x73,           0x74,           0x4A,           0x53,           0x73,           0x74
                },
                {
                //  IELS = 0x10
                //  GPT2_CCMPA,     SCI0_TXI,       SPI0_SPII,      SPI0_SPEI,      GPT2_CCMPA,     SCI0_TXI,       SPI0_SPII,      SPI0_SPEI
                    0x52,           0x72,           0x83,           0x84,           0x52,           0x72,           0x83,           0x84
                },
                {
                //  IELS = 0x11
                //  GPT_UVWEDGE,    SPI0_SPTI,      SPI0_SPTEND,    AGT0_AGTI,      GPT_UVWEDGE,    SPI0_SPTI,      SPI0_SPTEND,    PORT_IRQ7
                    0x70,           0x82,           0x85,           0x11,           0x70,           0x82,           0x85,           0x08
                },
                {
                //  IELS = 0x12
                //  SCI0_RXI,       AES_RDREQ,      TRNG_RDREQ,     GPT1_CMPD,      SCI0_RXI,       AES_RDREQ,      TRNG_RDREQ,     GPT3_CMPD
                    0x71,           0x8C,           0x8D,           0x4F,           0x71,           0x8C,           0x8D,           0x5B
                },
                {
                //  IELS = 0x13
                //  SCI0_AM,        AGT0_AGTCMBI,   IOPORT_GROUP2,  GPT4_CMPD,      SCI0_AM,        PORT_IRQ5,      PORT_IRQ6,      GPT4_UDF
                    0x75,           0x13,           0x3E,           0x61,           0x75,           0x06,           0x07,           0x63
                },
                {
                //  IELS = 0x14
                //  SPI0_SPRI,      IIC1_TXI,       GPT1_CMPC,      GPT5_UDF,       SPI0_SPRI,      IIC1_EEI,       MOSC_STOP,      GPT5_CMPD
                    0x81,           0x2D,           0x4E,           0x69,           0x81,           0x2F,           0x0F,           0x67
                },
                {
                //  IELS = 0x15
                //  AES_WRREQ,      IOPORT_GROUP1,  GPT4_CMPC,      GPT6_CMPD,      AES_WRREQ,      GPT1_UDF,       GPT3_CMPC,      GPT6_UDF
                    0x8B,           0x3D,           0x60,           0x6D,           0x8B,           0x51,           0x5A,           0x6F
                },
                {
                //  IELS = 0x16
                //  AGT0_AGTCMAI,   GPT1_CCMPB,     GPT5_OVF,       GPT7_UDF,       PORT_IRQ4,      GPT3_CCMPB,     GPT4_OVF,       GPT7_CMPD
                    0x12,           0x4D,           0x68,           0x9D,           0x05,           0x59,           0x62,           0x9B
                },
                {
                //  IELS = 0x17
                //  IIC1_RXI,       GPT3_UDF,       GPT6_CMPC,      GPT8_CMPD,      IIC1_TEI,       GPT5_CCMPB,     GPT5_CMPC,      GPT8_UDF
                    0x2C,           0x5D,           0x6C,           0xA1,           0x2E,           0x65,           0x66,           0xA3
                },
                {
                //  IELS = 0x18
                //  KEY_INTKR,      GPT4_CCMPB,     GPT7_OVF,       GPT9_UDF,       GPT1_OVF,       GPT7_CCMPB,     GPT6_OVF,       GPT9_CMPD
                    0x33,           0x5F,           0x9C,           0xA9,           0x50,           0x99,           0x6E,           0xA7
                },
                {
                //  IELS = 0x19
                //  GPT1_CCMPA,     GPT6_CCMPB,     GPT8_CMPC,      SCI1_ERI,       GPT3_CCMPA,     GPT9_CCMPB,     GPT7_CMPC,      SCI2_ERI
                    0x4C,           0x6B,           0xA0,           0x7A,           0x58,           0xA5,           0x9A,           0x91
                },
                {
                //  IELS = 0x1A
                //  GPT3_OVF,       GPT8_CCMPB,     GPT9_OVF,       SCI3_ERI,       GPT5_CCMPA,     SCI1_AM,        GPT8_OVF,       SCI9_ERI
                    0x5C,           0x9F,           0xA8,           0x96,           0x64,           0x7B,           0xA2,           0x7F
                },
                {
                //  IELS = 0x1B
                //  GPT4_CCMPA,     SCI1_TXI,       SCI1_TEI,       SCI9_AM,        GPT7_CCMPA,     SCI2_TXI,       GPT9_CMPC,      SPI1_SPEI
                    0x5E,           0x78,           0x79,           0x80,           0x98,           0x8F,           0xA6,           0x89
                },
                {
                //  IELS = 0x1C
                //  GPT6_CCMPA,     SCI2_AM,        SCI3_TEI,       NO_EVENT,       GPT9_CCMPA,     SCI9_TXI,       SCI2_TEI,       NO_EVENT
                    0x6A,           0x92,           0x95,           0x00,           0xA4,           0x7D,           0x90,           0x00
                },
                {
                //  IELS = 0x1D
                //  GPT8_CCMPA,     SCI3_TXI,       SPI1_SPII,      NO_EVENT,       SCI2_RXI,       SPI1_SPTI,      SCI3_AM,        NO_EVENT
                    0x9E,           0x94,           0x88,           0x00,           0x8E,           0x87,           0x97,           0x00
                },
                {
                //  IELS = 0x1E
                //  SCI1_RXI,       NO_EVENT,       NO_EVENT,       NO_EVENT,       SCI9_RXI,       NO_EVENT,       SCI9_TEI,       NO_EVENT
                    0x77,           0x00,           0x00,           0x00,           0x7C,           0x00,           0x7E,           0x00
                },
                {
                //  IELS = 0x1F
                //  SCI3_RXI,       NO_EVENT,       NO_EVENT,       NO_EVENT,       SPI1_SPRI,      NO_EVENT,       SPI1_SPTEND,    NO_EVENT
                    0x93,           0x00,           0x00,           0x00,           0x86,           0x00,           0x8A,           0x00
                }
            };

        public enum EventToInterruptLinkType
        {
            RA8,
            RA6 = RA8,
            RA4 = RA8,
            RA2
        }

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
