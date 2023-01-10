//
// Copyright (c) 2010-2023 Antmicro
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
    public class EXTI : IDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public EXTI(int numberOfOutputLines = 14, int firstDirectLine = DefaultFirstDirectLine)
        {
            this.firstDirectLine = firstDirectLine;
            var innerConnections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfOutputLines; ++i)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);
            Reset();
        }

        public void OnGPIO(int number, bool value)
        {
            if(number >= NumberOfLines)
            {
                this.Log(LogLevel.Error, "GPIO number {0} is out of range [0; {1})", number, NumberOfLines);
                return;
            }
            var lineNumber = (byte)number;

            if((number >= firstDirectLine) && value)
            {
                pending |= (1u << lineNumber);
                Connections[lineNumber].Set();
                return;
            }
            if((number >= firstDirectLine) && !value)
            {
                pending &= ~(1u << lineNumber);
                Connections[lineNumber].Unset();
                return;
            }

            if(BitHelper.IsBitSet(interruptMask, lineNumber) && // irq unmasked
               ((BitHelper.IsBitSet(risingTrigger, lineNumber) && value) // rising edge
               || (BitHelper.IsBitSet(fallingTrigger, lineNumber) && !value))) // falling edge
            {
                pending |= (1u << lineNumber);
                Connections[lineNumber].Set();
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
                    if(x >= NumberOfLines)
                    {
                        this.Log(LogLevel.Warning, "Software interrupt {0} is out of range [0; {1})", x, NumberOfLines);
                        return;
                    }
                    if(BitHelper.IsBitSet(interruptMask, x))
                    {
                        Connections[x].Set();
                    }
                });
                break;
            case Registers.PendingRegister:
                pending &= ~value;
                softwareInterrupt &= ~value;
                BitHelper.ForeachActiveBit(value, (x) =>
                {
                    if(x >= NumberOfLines)
                    {
                        this.Log(LogLevel.Warning, "Cleared interrupt {0} is out of range [0; {1})", x, NumberOfLines);
                        return;
                    }
                    Connections[x].Unset();
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
            foreach(var gpio in Connections)
            {
                gpio.Value.Unset();
            }
        }

        public long Size
        {
            get
            {
                return 0x3FF;
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public long NumberOfLines => Connections.Count;

        private uint interruptMask;
        private uint eventMask;
        private uint risingTrigger;
        private uint fallingTrigger;
        private uint pending;
        private uint softwareInterrupt;

        private readonly int firstDirectLine;
        // We treat lines above 23 as direct by default for backwards compatibility with
        // the old behavior of the EXTI model.
        private const int DefaultFirstDirectLine = 23;

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

