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
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.IRQControllers.PLIC;

namespace Antmicro.Renode.Peripherals.IRQControllers.PLIC
{
    public abstract class PlatformLevelInterruptControllerBase : IPlatformLevelInterruptController, IDoubleWordPeripheral, IIRQController, INumberedGPIOOutput
    {
        public PlatformLevelInterruptControllerBase(int numberOfSources, int numberOfContexts, bool prioritiesEnabled, uint extraInterrupts = 0)
        {
            var connections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfContexts + extraInterrupts; i++)
            {
                connections[i] = new GPIO();
            }
            Connections = connections;

            this.prioritiesEnabled = prioritiesEnabled;

            irqSources = new IrqSource[numberOfSources];
            for(var i = 0u; i < numberOfSources; i++)
            {
                irqSources[i] = new IrqSource(i, this);
            }

            irqContexts = new IrqContext[numberOfContexts];
            for(var i = 0u; i < irqContexts.Length; i++)
            {
                irqContexts[i] = new IrqContext(i, this);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            this.Log(LogLevel.Noisy, "Resetting peripheral state");

            registers.Reset();
            foreach(var irqSource in irqSources)
            {
                irqSource.Reset();
            }
            foreach(var irqContext in irqContexts)
            {
                irqContext.Reset();
            }
            RefreshInterrupts();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            if(!IsIrqSourceAvailable(number))
            {
                this.Log(LogLevel.Error, "Wrong gpio source: {0}", number);
                return;
            }
            lock(irqSources)
            {
                this.Log(LogLevel.Noisy, "Setting GPIO number #{0} to value {1}", number, value);
                var irq = irqSources[number];
                irq.State = value;

                if(value)
                {
                    foreach(var irqContext in irqContexts)
                    {
                        irqContext.MarkSourceAsPending(irq);
                    }
                }

                RefreshInterrupts();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        /// <summary>
        /// Setting this property to a value different than -1 causes all interrupts to be reported to a context with a given id.
        ///
        /// This is mostly for debugging purposes.
        /// It allows to designate a single core (in a multi-core setup) to handle external interrupts making it easier to debug trap handlers.
        /// </summary>
        public int ForcedContext { get; set; } = -1;

        protected void RefreshInterrupts()
        {
            lock(irqSources)
            {
                foreach(var context in irqContexts)
                {
                    context.RefreshInterrupt();
                }
            }
        }

        protected void AddContextClaimCompleteRegister(Dictionary<long, DoubleWordRegister> registersMap, long offset, uint contextId)
        {
            registersMap.Add(offset, new DoubleWordRegister(this).WithValueField(0, 32, valueProviderCallback: _ =>
            {
                lock(irqSources)
                {
                    return irqContexts[contextId].AcknowledgePendingInterrupt();
                }
            },
            writeCallback: (_, value) =>
            {
                if(!IsIrqSourceAvailable((int)value))
                {
                    this.Log(LogLevel.Error, "Trying to complete handling of non-existing interrupt source {0}", value);
                    return;
                }
                lock(irqSources)
                {
                    irqContexts[contextId].CompleteHandlingInterrupt(irqSources[value]);
                }
            }));
        }

        protected void AddContextEnablesRegister(Dictionary<long, DoubleWordRegister> registersMap, long address, uint contextId, int numberOfSources)
        {
            var maximumSourceDoubleWords = (int)Math.Ceiling((numberOfSources + 1) / 32.0) * 4;

            for(var offset = 0u; offset < maximumSourceDoubleWords; offset += 4)
            {
                var lOffset = offset;
                registersMap.Add(address + offset, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                {
                    lock(irqSources)
                    {
                        // Each source is represented by one bit. offset and lOffset indicate the offset in double words from ContextXEnables,
                        // `bit` is the bit number in the given double word,
                        // and `sourceIdBase + bit` indicate the source number.
                        var sourceIdBase = lOffset * 8;
                        var bits = BitHelper.GetBits((uint)value);
                        for(var bit = 0u; bit < bits.Length; bit++)
                        {
                            var sourceNumber = sourceIdBase + bit;
                            if(!IsIrqSourceAvailable((int)sourceNumber))
                            {
                                if(bits[bit])
                                {
                                    this.Log(LogLevel.Warning, "Trying to enable non-existing source: {0}", sourceNumber);
                                }
                                continue;
                            }

                            irqContexts[contextId].EnableSource(irqSources[sourceNumber], bits[bit]);
                        }
                        RefreshInterrupts();
                    }
                }));
            }
        }

        protected void AddContextPriorityThresholdRegister(Dictionary<long, DoubleWordRegister> registersMap, long offset, uint hartId)
        {
            this.Log(LogLevel.Noisy, "Adding Context {0} threshold priority register address 0x{1:X}", hartId, offset);
            registersMap.Add(offset, new DoubleWordRegister(this)
                .WithValueField(0, 32,
                    valueProviderCallback: _ =>
                    {
                        // TODO: implement
                        return 0;
                    },
                    writeCallback: (_, value) =>
                    {
                        // TODO: implement
                        // Only log a warning if the software attempts to set a nonzero threshold as the model behaves as if it is 0
                        this.Log(prioritiesEnabled && value != 0 ? LogLevel.Warning : LogLevel.Noisy, "Setting priority threshold for Context {0} not supported", hartId);
                    }));
        }

        protected virtual bool IsIrqSourceAvailable(int number)
        {
            return number >= 0 && number < irqSources.Length;
        }

        protected DoubleWordRegisterCollection registers;

        protected readonly bool prioritiesEnabled;

        protected readonly IrqSource[] irqSources;
        protected readonly IrqContext[] irqContexts;
    }
}
