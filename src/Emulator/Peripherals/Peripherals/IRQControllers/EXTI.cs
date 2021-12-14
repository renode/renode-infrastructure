//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    /// <summary>
    /// EXTI interrupt controller.
    /// To map  number inputs used in JSON to pins from the reference manual, use the following rule:
    /// 0->PA0, 1->PA1, ..., 15->PA15, 16->PB0, ...
    /// This model will accept any number of input pins, but keep in mind that currently System
    /// Configuration Controller (SYSCFG) is able to handle only 16x16 pins in total.
    /// </summary>
    public class EXTI :  IDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public EXTI(int numberOfOutputLines = 14)
        {
            this.numberOfOutputLines = numberOfOutputLines;
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            // Theoretically there could be up to 256 GPIOs connected to 16 EXTI lines - 16 GPIOs per port.
            // GPIOs are connected in this order: 0 indexed to EXTI0 line, 1 -> EXTI1 etc.
            // If we get `number` higher than 15, it means we will address other 7 EXTI lines
            // which are connected to PVD, RTC etc.
            //
            // EXTI map:
            // `number = 0` -> PA0, PB0, PC0 ... (EXTI0 - Interrupt 0)
            // `number = 1` -> PA1, PB1, PC1 ... (EXTI1 - Interrupt 1)
            // ...
            // `number = 4` -> PA4, PB4, PC4 ... (EXTI4 - Interrupt 4)
            // `number = 5` -> PA5, PB5, PC5 ... (EXTI5 - Interrupt 5)
            // `number = 6` -> PA6, PB6, PC6 ... (EXTI6 - Interrupt 5)
            // ...
            // `number = 14` -> PA14, PB14, PC14 ... (EXTI14 - Interrupt 6)
            // `number = 15` -> PA15, PB15, PC15 ... (EXTI15 - Interrupt 6)
            // `number = 16` -> PVD (EXTI16 - Interrupt 7)
            // `number = 17` -> RTC Alarm event (EXTI17 - Interrupt 8)
            // ...
            // `number = 22` -> RTC Wakeup event (EXTI22 - Interrupt 13)

            if(number > MaxEXTILines)
            {
                this.Log(LogLevel.Error, "Given value: {0} exceeds maximum EXTI lines: {1}", number, MaxEXTILines);
                return;
            }
            var lineNumber = (byte)number;
            var irqNumber = gpioMapping[lineNumber];

            if(number == 23 && value)
            {
                pending |= (1u << lineNumber);
                Connections[irqNumber].Set();
                return;
            }
            if(number == 23 && !value)
            {
                pending &= ~(1u << lineNumber);
                Connections[irqNumber].Unset();
                return;
            }

            if(BitHelper.IsBitSet(interruptMask, lineNumber) && // irq unmasked
               ((BitHelper.IsBitSet(risingTrigger, lineNumber) && value) // rising edge
               || (BitHelper.IsBitSet(fallingTrigger, lineNumber) && !value))) // falling edge
            {
                pending |= (1u << lineNumber);
                Connections[irqNumber].Set();
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.InterruptMask:
                return interruptMask;
            case Registers.EventMask:
                return eventMask;
            case Registers.RisingTriggerSelection:
                return risingTrigger;
            case Registers.FallingTriggerSelection:
                return fallingTrigger;
            case Registers.SoftwareInterruptEvent:
                return softwareInterrupt;
            case Registers.PendingRegister:
                return pending;
            default:
                this.LogUnhandledRead(offset);
                break;
            }
            return 0;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.InterruptMask:
                interruptMask = value;
                break;
            case Registers.EventMask:
                eventMask = value;
                break;
            case Registers.RisingTriggerSelection:
                risingTrigger = value;
                break;
            case Registers.FallingTriggerSelection:
                fallingTrigger = value;
                break;
            case Registers.SoftwareInterruptEvent:
                var allNewAndOld = softwareInterrupt | value;
                var bitsToSet = allNewAndOld ^ softwareInterrupt;
                BitHelper.ForeachActiveBit(bitsToSet, (x) =>
                {
                    if(BitHelper.IsBitSet(interruptMask, x))
                    {
                        Connections[gpioMapping[x]].Set();
                    }
                });
                break;
            case Registers.PendingRegister:
                pending &= ~value;
                softwareInterrupt &= ~value;
                BitHelper.ForeachActiveBit(value, (x) =>
                {
                    Connections[gpioMapping[x]].Unset();
                });
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            interruptMask = 0;
            eventMask = 0;
            risingTrigger = 0;
            fallingTrigger = 0;
            pending = 0;
            softwareInterrupt = 0;


            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfOutputLines; ++i)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
        }

        public long Size
        {
            get
            {
                return 0x3FF;
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        private static readonly int[] gpioMapping = { 0, 1, 2, 3, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 7, 8, 9, 10, 11, 12, 13, 23 };
        private readonly int numberOfOutputLines;

        private uint interruptMask;
        private uint eventMask;
        private uint risingTrigger;
        private uint fallingTrigger;
        private uint pending;
        private uint softwareInterrupt;

        private const int MaxEXTILines = 32;

        private enum Registers
        {
            InterruptMask = 0x0,
            EventMask = 0x4,
            RisingTriggerSelection = 0x8,
            FallingTriggerSelection = 0xC,
            SoftwareInterruptEvent = 0x10,
            PendingRegister = 0x14
        }
    }
}

