//
// Copyright (c) 2010-2021 Antmicro
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
        public PlatformLevelInterruptControllerBase(int numberOfSources, int numberOfTargets, bool prioritiesEnabled, bool countSourcesFrom0, PrivilegeLevel[] supportedLevels)
        {
            this.countSourcesFrom0 = countSourcesFrom0;

            var connections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < (supportedLevels != null ? supportedLevels.Length : 1) * numberOfTargets; i++)
            {
                connections[i] = new GPIO();
            }
            Connections = connections;

            // depending on the `countSourcesFrom0` value
            // irqSources might be identified starting from 1, as the source "0" is not used;
            // regardless, we always initiate the array starting from 0 and the 
            // identifier transformation is done in the `GetIrqSource` method
            irqSources = new IrqSource[numberOfSources];
            for(var i = 0u; i < numberOfSources; i++)
            {
                irqSources[i] = new IrqSource(countSourcesFrom0 ? i : i + 1, this);
            }

            irqTargets = new IrqTarget[numberOfTargets];
            for(var i = 0u; i < numberOfTargets; i++)
            {
                irqTargets[i] = (supportedLevels == null)
                    ? new IrqTarget(i, this)
                    : new IrqTarget(i, this, supportedLevels);
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
            foreach(var irqTarget in irqTargets)
            {
                irqTarget.Reset();
            }
            RefreshInterrupts();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            if(!IsIrqSourceAvailable((uint)number))
            {
                this.Log(LogLevel.Error, "Wrong gpio source: {0}. This irq controller supports sources from {1} to {2}.", number, countSourcesFrom0 ? 0 : 1, countSourcesFrom0 ? irqSources.Length - 1 : irqSources.Length);
                return;
            }
            lock(irqSources)
            {
                this.Log(LogLevel.Noisy, "Setting GPIO number #{0} to value {1}", number, value);
                var irq = GetIrqSource((uint)number);
                irq.State = value;
                irq.IsPending |= value;
                RefreshInterrupts();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        /// <summary>
        /// Setting this property to a value different than -1 causes all interrupts to be reported to a target with a given id.
        ///
        /// This is mostly for debugging purposes.
        /// It allows to designate a single core (in a multi-core setup) to handle external interrupts making it easier to debug trap handlers.
        /// </summary>
        public int ForcedTarget { get; set; } = -1;

        protected void RefreshInterrupts()
        {
            lock(irqSources)
            {
                foreach(var target in irqTargets)
                {
                    target.RefreshAllInterrupts();
                }
            }
        }

        protected void AddTargetClaimCompleteRegister(Dictionary<long, DoubleWordRegister> registersMap, long offset, uint hartId, PrivilegeLevel level)
        {
            registersMap.Add(offset, new DoubleWordRegister(this).WithValueField(0, 32, valueProviderCallback: _ =>
            {
                lock(irqSources)
                {
                    return irqTargets[hartId].Handlers[(int)level].AcknowledgePendingInterrupt();
                }
            },
            writeCallback: (_, value) =>
            {
                if(!IsIrqSourceAvailable(value))
                {
                    this.Log(LogLevel.Error, "Trying to complete handling of non-existing interrupt source {0}", value);
                    return;
                }
                lock(irqSources)
                {
                    irqTargets[hartId].Handlers[(int)level].CompleteHandlingInterrupt(GetIrqSource(value));
                }
            }));
        }

        protected void AddTargetEnablesRegister(Dictionary<long, DoubleWordRegister> registersMap, long address, uint hartId, PrivilegeLevel level, int numberOfSources)
        {
            var maximumSourceDoubleWords = (int)Math.Ceiling((numberOfSources + 1) / 32.0) * 4;

            for(var offset = 0u; offset < maximumSourceDoubleWords; offset += 4)
            {
                var lOffset = offset;
                registersMap.Add(address + offset, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                {
                    lock(irqSources)
                    {
                        // Each source is represented by one bit. offset and lOffset indicate the offset in double words from TargetXEnables,
                        // `bit` is the bit number in the given double word,
                        // and `sourceIdBase + bit` indicate the source number.
                        var sourceIdBase = lOffset * 8;
                        var bits = BitHelper.GetBits(value);
                        for(var bit = 0u; bit < bits.Length; bit++)
                        {
                            var sourceNumber = sourceIdBase + bit;
                            if(!IsIrqSourceAvailable(sourceNumber))
                            {
                                if(bits[bit])
                                {
                                    this.Log(LogLevel.Warning, "Trying to enable non-existing source: {0}", sourceNumber);
                                }
                                continue;
                            }

                            irqTargets[hartId].Handlers[(int)level].EnableSource(GetIrqSource(sourceNumber), bits[bit]);
                        }
                        RefreshInterrupts();
                    }
                }));
            }
        }

        protected bool IsIrqSourceAvailable(uint number)
        {
            return (countSourcesFrom0)
                ? number < irqSources.Length
                : number > 0 && number <= irqSources.Length;
        }

        internal IrqSource GetIrqSource(uint number)
        {
            // we need to decrement the `number` in case
            // interrupt sources are counted externally from 1
            // as we keep them internally always 0-based
            return irqSources[countSourcesFrom0 ? number : number - 1];
        }

        protected DoubleWordRegisterCollection registers;

        protected readonly IrqSource[] irqSources;
        protected readonly IrqTarget[] irqTargets;
        private readonly bool countSourcesFrom0;
    }
}
