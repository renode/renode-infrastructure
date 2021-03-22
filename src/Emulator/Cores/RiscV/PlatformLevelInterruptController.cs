//
// Copyright (c) 2010-2018 Antmicro
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

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class PlatformLevelInterruptController : IDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput, IPlatformLevelInterruptController
    {
        public PlatformLevelInterruptController(int numberOfSources, int numberOfTargets = 1, bool prioritiesEnabled = true)
        {
            // numberOfSources has to fit between these two registers, one bit per source
            if(Math.Ceiling((numberOfSources + 1) / 32.0) * 4 > Targets01EnablesWidth)
            {
                throw new ConstructionException($"Current {this.GetType().Name} implementation does not support more than {Targets01EnablesWidth} sources");
            }

            var connections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < 4 * numberOfTargets; i++)
            {
                connections[i] = new GPIO();
            }
            Connections = connections;

            // the standard PLIC controller counts sources from 1
            irqSources = new IrqSource[numberOfSources + 1];
            for(var i = 0u; i < irqSources.Length; i++)
            {
                irqSources[i] = new IrqSource(i, this);
            }

            irqTargets = new IrqTarget[numberOfTargets];
            for(var i = 0u; i < numberOfTargets; i++)
            {
                irqTargets[i] = new IrqTarget(i, this);
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.Source0Priority, new DoubleWordRegister(this)
                .WithValueField(0, 3, FieldMode.Read, writeCallback: (_, value) =>
                {
                    if(value != 0)
                    {
                        this.Log(LogLevel.Warning, $"Trying to set priority {value} for Source 0, which is illegal");
                    }
                }));
            for(var i = 1; i <= numberOfSources; i++)
            {
                var j = i;
                registersMap[(long)Registers.Source1Priority * i] = new DoubleWordRegister(this)
                    .WithValueField(0, 3,
                                    valueProviderCallback: (_) => irqSources[j].Priority,
                                    writeCallback: (_, value) =>
                                    {
                                        if(prioritiesEnabled)
                                        {
                                            irqSources[j].Priority = value;
                                            RefreshInterrupts();
                                        }
                                    });
            }

            AddTargetEnablesRegister(registersMap, (long)Registers.Target0MachineEnables, 0, PrivilegeLevel.Machine, numberOfSources);
            AddTargetClaimCompleteRegister(registersMap, (long)Registers.Target0ClaimComplete, 0, PrivilegeLevel.Machine);
            // Target 0 does not support supervisor mode

            for(var i = 0u; i < numberOfTargets; i++)
            {
                AddTargetEnablesRegister(registersMap, (long)Registers.Target1MachineEnables + i * Targets12EnablesWidth, i + 1, PrivilegeLevel.Machine, numberOfSources);
                AddTargetEnablesRegister(registersMap, (long)Registers.Target1SupervisorEnables + i * Targets12EnablesWidth, i + 1, PrivilegeLevel.Supervisor, numberOfSources);

                AddTargetClaimCompleteRegister(registersMap, (long)Registers.Target1MachineClaimComplete + i * Targets12ClaimCompleteWidth, i + 1, PrivilegeLevel.Machine);
                AddTargetClaimCompleteRegister(registersMap, (long)Registers.Target1SupervisorClaimComplete + i * Targets12ClaimCompleteWidth, i + 1, PrivilegeLevel.Supervisor);
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
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
            if(!IsIrqSourceAvailable(number))
            {
                this.Log(LogLevel.Error, "Wrong gpio source: {0}", number);
            }
            lock(irqSources)
            {
                this.Log(LogLevel.Noisy, "Setting GPIO number #{0} to value {1}", number, value);
                var irq = irqSources[number];
                irq.State = value;
                irq.IsPending |= value;
                RefreshInterrupts();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        public long Size => 0x4000000;

        /// <summary>
        /// Setting this property to a value different than -1 causes all interrupts to be reported to a target with a given id.
        ///
        /// This is mostly for debugging purposes.
        /// It allows to designate a single core (in a multi-core setup) to handle external interrupts making it easier to debug trap handlers.
        /// </summary>
        public int ForcedTarget { get; set; } = -1;

        private void RefreshInterrupts()
        {
            lock(irqSources)
            {
                foreach(var target in irqTargets)
                {
                    target.RefreshAllInterrupts();
                }
            }
        }

        private void AddTargetClaimCompleteRegister(Dictionary<long, DoubleWordRegister> registersMap, long offset, uint hartId, PrivilegeLevel level)
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
                if(!IsIrqSourceAvailable((int)value))
                {
                    this.Log(LogLevel.Error, "Trying to complete handling of non-existing interrupt source {0}", value);
                    return;
                }
                lock(irqSources)
                {
                    irqTargets[hartId].Handlers[(int)level].CompleteHandlingInterrupt(irqSources[value]);
                }
            }));
        }

        private void AddTargetEnablesRegister(Dictionary<long, DoubleWordRegister> registersMap, long address, uint hartId, PrivilegeLevel level, int numberOfSources)
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
                            if(!IsIrqSourceAvailable((int)sourceNumber))
                            {
                                if(bits[bit])
                                {
                                    this.Log(LogLevel.Warning, "Trying to enable non-existing source: {0}", sourceNumber);
                                }
                                continue;
                            }

                            irqTargets[hartId].Handlers[(int)level].EnableSource(irqSources[sourceNumber], bits[bit]);
                        }
                        RefreshInterrupts();
                    }
                }));
            }
        }

        private bool IsIrqSourceAvailable(uint number)
        {
            // standard PLIC controller does not support source 0
            return number > 0 && number < irqSources.Length;
        }

        private readonly IrqSource[] irqSources;
        private readonly IrqTarget[] irqTargets;
        private readonly DoubleWordRegisterCollection registers;

        private enum Registers : long
        {
            Source0Priority = 0x0, //this is a fake register, as there is no source 0, but the software writes to it anyway.
            Source1Priority = 0x4,
            Source2Priority = 0x8,
            // ...
            StartOfPendingArray = 0x1000,
            // WARNING: offset between Enables of Targets 0 and 1 is different than between Targets 2 and 1, 3 and 2, etc.!
            Target0MachineEnables = 0x2000,
            Target1MachineEnables = 0x2080,
            Target1SupervisorEnables = 0x2100,
            Target2MachineEnables = 0x2180,
            Target2SupervisorEnables = 0x2200,
            // ...
            Target0PriorityThreshold = 0x200000,
            Target0ClaimComplete = 0x200004,
            // ...
            Target1PriorityThreshold = 0x201000,
            Target1MachineClaimComplete = 0x201004,
            Target1SupervisorClaimComplete = 0x202004,
            Target2MachineClaimComplete = 0x203004,
            Target2SupervisorClaimComplete = 0x204004,
            // ...
        }

        private const long Targets01EnablesWidth = Registers.Target1MachineEnables - Registers.Target0MachineEnables;
        private const long Targets12EnablesWidth = Registers.Target2MachineEnables - Registers.Target1MachineEnables;
        private const long Targets12ClaimCompleteWidth = Registers.Target2MachineClaimComplete - Registers.Target1MachineClaimComplete;
    }
}
