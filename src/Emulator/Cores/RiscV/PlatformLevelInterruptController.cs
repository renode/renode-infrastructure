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

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class PlatformLevelInterruptController : IDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
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

            // irqSources are initialized from 1, as the source "0" is not used.
            irqSources = new IrqSource[numberOfSources + 1];
            for(var i = 1u; i <= numberOfSources; i++)
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
            foreach(var irqSource in irqSources.Skip(1))
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
            if(number < 1 || number >= irqSources.Length)
            {
                this.Log(LogLevel.Error, "Wrong gpio source: {0}. This irq controller supports sources from 1 to {1}.", number, irqSources.Length - 1);
            }
            lock(irqSources)
            {
                this.Log(LogLevel.Noisy, "Setting GPIO number #{0} to value {1}", number, value);
                var irq = irqSources[(uint)number];
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
                return irqTargets[hartId].levels[(int)level].AcknowledgePendingInterrupt();
            },
            writeCallback: (_, value) =>
            {
                if(value == 0 || value >= irqSources.Length)
                {
                    this.Log(LogLevel.Error, "Trying to complete handling of non-existing interrupt source {0}", value);
                    return;
                }
                irqTargets[hartId].levels[(int)level].CompleteHandlingInterrupt(irqSources[value]);
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
                            if(sourceNumber == 0 || irqSources.Length <= sourceNumber)
                            {
                                if(bits[bit])
                                {
                                    this.Log(LogLevel.Noisy, "Trying to enable non-existing source: {0}", sourceNumber);
                                }
                                continue;
                            }

                            irqTargets[hartId].levels[(int)level].EnableSource(irqSources[sourceNumber], bits[bit]);
                        }
                        RefreshInterrupts();
                    }
                }));
            }
        }

        private readonly IrqSource[] irqSources;
        private readonly IrqTarget[] irqTargets;
        private readonly DoubleWordRegisterCollection registers;

        private class IrqSource
        {
            public IrqSource(uint id, PlatformLevelInterruptController irqController)
            {
                this.irqController = irqController;

                Id = id;
                Reset();
            }

            public override string ToString()
            {
                return $"IrqSource id: {Id}, priority: {Priority}, state: {State}, pending: {IsPending}";
            }

            public void Reset()
            {
                Priority = DefaultPriority;
                State = false;
                IsPending = false;
            }

            public uint Id { get; private set; }

            public uint Priority
            {
                 get { return priority; }
                 set
                 {
                     if(value == priority)
                     {
                         return;
                     }

                     irqController.Log(LogLevel.Noisy, "Setting priority {0} for source #{1}", value, Id);
                     priority = value;
                 }
            }

            public bool State
            {
                get { return state; }
                set
                {
                    if(value == state)
                    {
                        return;
                    }

                    state = value;
                    irqController.Log(LogLevel.Noisy, "Setting state to {0} for source #{1}", value, Id);
                }
            }
            public bool IsPending
            {
                get { return isPending; }
                set
                {
                    if(value == isPending)
                    {
                        return;
                    }

                    isPending = value;
                    irqController.Log(LogLevel.Noisy, "Setting pending status to {0} for source #{1}", value, Id);
                }
            }

            private uint priority;
            private bool state;
            private bool isPending;

            private readonly PlatformLevelInterruptController irqController;

            // 1 is the default, lowest value. 0 means "no interrupt".
            private const uint DefaultPriority = 1;
        }

        private class IrqTarget
        {
            public IrqTarget(uint id, PlatformLevelInterruptController irqController)
            {
                levels = new IrqTargetHandler[]
                {
                    new IrqTargetHandler(irqController, id, PrivilegeLevel.User),
                    new IrqTargetHandler(irqController, id, PrivilegeLevel.Supervisor),
                    new IrqTargetHandler(irqController, id, PrivilegeLevel.Hypervisor),
                    new IrqTargetHandler(irqController, id, PrivilegeLevel.Machine)
                };
            }

            public void RefreshAllInterrupts()
            {
                foreach(var lr in levels)
                {
                    lr.RefreshInterrupt();
                }
            }

            public void Reset()
            {
                foreach(var lr in levels)
                {
                    lr.Reset();
                }
            }

            public readonly IrqTargetHandler[] levels;

            public class IrqTargetHandler
            {
                public IrqTargetHandler(PlatformLevelInterruptController irqController, uint targetId, PrivilegeLevel privilegeLevel)
                {
                    this.irqController = irqController;
                    this.targetId = targetId;
                    this.privilegeLevel = privilegeLevel;

                    enabledSources = new HashSet<IrqSource>();
                    activeInterrupts = new Stack<IrqSource>();
                }

                public override string ToString()
                {
                    return $"[Hart #{targetId} at {privilegeLevel} level (connection output #{ConnectionNumber})]";
                }

                public void Reset()
                {
                    activeInterrupts.Clear();
                    enabledSources.Clear();

                    RefreshInterrupt();
                }

                public void RefreshInterrupt()
                {
                    lock(irqController.irqSources)
                    {
                        var forcedTarget = irqController.ForcedTarget;
                        if(forcedTarget != -1 && this.targetId != forcedTarget)
                        {
                            irqController.Connections[ConnectionNumber].Set(false);
                            return;
                        }

                        var currentPriority = activeInterrupts.Count > 0 ? activeInterrupts.Peek().Priority : 0;
                        var isPending = enabledSources.Any(x => x.Priority > currentPriority && x.IsPending);
                        irqController.Connections[ConnectionNumber].Set(isPending);
                    }
                }

                public void CompleteHandlingInterrupt(IrqSource irq)
                {
                    lock(irqController.irqSources)
                    {
                        irqController.Log(LogLevel.Noisy, "Completing irq {0} at {1}", irq.Id, this);

                        if(activeInterrupts.Count == 0)
                        {
                            irqController.Log(LogLevel.Error, "Trying to complete irq {0} @ {1}, there are no active interrupts left", irq.Id, this);
                            return;
                        }
                        var topActiveInterrupt = activeInterrupts.Pop();
                        if(topActiveInterrupt != irq)
                        {
                            irqController.Log(LogLevel.Error, "Trying to complete irq {0} @ {1}, but {2} is the active one", irq.Id, this, topActiveInterrupt.Id);
                            return;
                        }

                        irq.IsPending = irq.State;
                        RefreshInterrupt();
                    }
                }

                public void EnableSource(IrqSource s, bool enabled)
                {
                    if(enabled)
                    {
                        enabledSources.Add(s);
                    }
                    else
                    {
                        enabledSources.Remove(s);
                    }
                    irqController.Log(LogLevel.Noisy, "{0} source #{1} @ {2}", enabled ? "Enabling" : "Disabling", s.Id, this);
                    RefreshInterrupt();
                }

                public uint AcknowledgePendingInterrupt()
                {
                    lock(irqController.irqSources)
                    {
                        IrqSource pendingIrq;

                        var forcedTarget = irqController.ForcedTarget;
                        if(forcedTarget != -1 && this.targetId != forcedTarget)
                        {
                            pendingIrq = null;
                        }
                        else
                        {
                            pendingIrq = enabledSources.Where(x => x.IsPending)
                                .OrderByDescending(x => x.Priority)
                                .ThenBy(x => x.Id).FirstOrDefault();
                        }

                        if(pendingIrq == null)
                        {
                            irqController.Log(LogLevel.Noisy, "There is no pending interrupt to acknowledge at the moment for {0}. Currently enabled sources: {1}", this, string.Join(", ", enabledSources.Select(x => x.ToString())));
                            return 0;
                        }
                        pendingIrq.IsPending = false;
                        activeInterrupts.Push(pendingIrq);

                        irqController.Log(LogLevel.Noisy, "Acknowledging pending interrupt #{0} @ {1}", pendingIrq.Id, this);

                        RefreshInterrupt();
                        return pendingIrq.Id;
                    }
                }

                private int ConnectionNumber => (int)(4 * targetId + (int)privilegeLevel);

                private readonly uint targetId;
                private readonly PrivilegeLevel privilegeLevel;
                private readonly PlatformLevelInterruptController irqController;
                private readonly HashSet<IrqSource> enabledSources;
                private readonly Stack<IrqSource> activeInterrupts;
            }
        }

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
