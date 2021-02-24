//
// Copyright (c) 2010-2020 Antmicro
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
    public class OpenTitan_PlatformLevelInterruptController : IDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public OpenTitan_PlatformLevelInterruptController(int numberOfSources, int numberOfTargets = 1, bool prioritiesEnabled = true)
        {
            var connections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfTargets; i++)
            {
                connections[i] = new GPIO();
            }
            Connections = connections;

            irqSources = new IrqSource[numberOfSources + 1];
            for(var i = 0u; i <= numberOfSources; i++)
            {
                irqSources[i] = new IrqSource(i, this);
            }

            irqTargets = new IrqTarget[numberOfTargets];
	    this.Log(LogLevel.Noisy, $"Create irq targets {numberOfTargets}");
            for(var i = 0u; i < numberOfTargets; i++)
            {
                irqTargets[i] = new IrqTarget(i, this);
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.InterruptPending0, new DoubleWordRegister(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => {
                        return 0;
                    }));

            registersMap.Add((long)Registers.InterruptPending1, new DoubleWordRegister(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => {
                        return 0;
                    }));

            registersMap.Add((long)Registers.InterruptPending2, new DoubleWordRegister(this)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: (_) => {
                        return 0;
                    }));

            registersMap.Add((long)Registers.InterruptSourceMode0, new DoubleWordRegister(this)
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, valueProviderCallback: (_) => {
                        return 0;
                    }, writeCallback: (_, value) => {
                        this.Log(LogLevel.Noisy, $"Write InterruptSourceMode0 0x{value:X}");
                    }));

            registersMap.Add((long)Registers.InterruptSourceMode1, new DoubleWordRegister(this)
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, valueProviderCallback: (_) => {
                        return 0;
                    }, writeCallback: (_, value) => {
                        this.Log(LogLevel.Noisy, $"Write InterruptSourceMode1 0x{value:X}");
                    }));

            registersMap.Add((long)Registers.InterruptSourceMode2, new DoubleWordRegister(this)
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, valueProviderCallback: (_) => {
                        this.Log(LogLevel.Noisy, $"InterruptSourceMode2");
                        return 0;
                    }, writeCallback: (_, value) => {
                        this.Log(LogLevel.Noisy, $"Write InterruptSourceMode2 0x{value:X}");
                    }));

            registersMap.Add((long)Registers.Target0SoftwareInterrupt, new DoubleWordRegister(this)
                .WithValueField(0, 1, FieldMode.Read | FieldMode.Write, valueProviderCallback: (_) => {
                        return 0;
                    }, writeCallback: (_, value) => {
                        this.Log(LogLevel.Noisy, $"Write Target 0 Software Interrupt 0x{value:X}");
                    }));

	    this.Log(LogLevel.Noisy, $"Create source register {numberOfSources}");

            for(var i = 0; i <= numberOfSources; i++)
            {
                var j = i;
	        this.Log(LogLevel.Noisy, $"Create source priority register {i}");
                registersMap[(long)Registers.Source0Priority +  (4 * i)] = new DoubleWordRegister(this)
                    .WithValueField(0, 3,
                                    valueProviderCallback: (_) => irqSources[j].Priority,
                                    writeCallback: (_, value) =>
                                    {
                                        this.Log(LogLevel.Noisy, $"Set priority 0x{value:X} for Source {j}");
                                        if(prioritiesEnabled)
                                        {
                                            irqSources[j].Priority = value;
                                            RefreshInterrupts();
                                        }
                                    });
            }

            AddTargetEnablesRegister(registersMap, (long)Registers.Target0MachineEnables, 0, numberOfSources);
            AddTargetClaimCompleteRegister(registersMap, (long)Registers.Target0ClaimComplete, 0);
            // Target 0 does not support supervisor mode

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
                this.Log(LogLevel.Warning, "Wrong gpio source: {0}. This irq controller supports sources from 1 to {1}.", number, irqSources.Length - 1);
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

        public long Size => 0x1000;

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
                    this.Log(LogLevel.Noisy, $"Refresh interrupts for target {target}");
                    target.RefreshAllInterrupts();
                }
            }
        }

        private void AddTargetClaimCompleteRegister(Dictionary<long, DoubleWordRegister> registersMap, long offset, uint hartId)
        {
            var lOffset = offset;
            var lHartId = hartId;
            this.Log(LogLevel.Noisy, $"Claim Complete Register {lHartId} {offset}");
            registersMap.Add(offset, new DoubleWordRegister(this).WithValueField(0, 32, valueProviderCallback: _ =>
            {
                this.Log(LogLevel.Noisy, $"Acknowledge complete register {lOffset}");
                return irqTargets[hartId].levels[0].AcknowledgePendingInterrupt();
            },
            writeCallback: (_, value) =>
            {
                this.Log(LogLevel.Noisy, $"Claim complete register Hart:{lHartId} Offset:0x{lOffset:X} Value:0x{value:X}");
                if(value >= irqSources.Length)
                {
                    this.Log(LogLevel.Error, "Trying to complete handling of non-existing interrupt source {0}", value);
                    return;
                }
                irqTargets[hartId].levels[0].CompleteHandlingInterrupt(irqSources[value]);
            }));
            this.Log(LogLevel.Noisy, $"Add Claim Complete Register Hart:{lHartId} 0x{offset:X}");
        }

        private void AddTargetEnablesRegister(Dictionary<long, DoubleWordRegister> registersMap, long address, uint hartId, int numberOfSources)
        {
            var maximumSourceDoubleWords = (int)Math.Ceiling((numberOfSources + 1) / 32.0) * 4;

            for(var offset = 0u; offset < maximumSourceDoubleWords; offset += 4)
            {
                var lOffset = offset;
                var lHartId = hartId;
                registersMap.Add(address + offset, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
                {
                    lock(irqSources)
                    {
                        this.Log(LogLevel.Noisy, $"Add Target Enable register Hard:{lHartId} Offset: 0x{lOffset:X} 0x{value:X}");
                        // Each source is represented by one bit. offset and lOffset indicate the offset in double words from TargetXEnables,
                        // `bit` is the bit number in the given double word,
                        // and `sourceIdBase + bit` indicate the source number.
                        var sourceIdBase = lOffset * 8;
                        var bits = BitHelper.GetBits(value);
                        for(var bit = 0u; bit < bits.Length; bit++)
                        {
                            var sourceNumber = sourceIdBase + bit;
                            if(irqSources.Length <= sourceNumber)
                            {
                                if(bits[bit])
                                {
                                    this.Log(LogLevel.Warning, "Trying to enable non-existing source: {0}", sourceNumber);
                                }
                                continue;
                            }

                            irqTargets[hartId].levels[0].EnableSource(irqSources[sourceNumber], bits[bit]);
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
            public IrqSource(uint id, OpenTitan_PlatformLevelInterruptController irqController)
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

            private readonly OpenTitan_PlatformLevelInterruptController irqController;

            // 1 is the default, lowest value. 0 means "no interrupt".
            private const uint DefaultPriority = 1;
        }

        private class IrqTarget
        {
            public IrqTarget(uint id, OpenTitan_PlatformLevelInterruptController irqController)
            {
                levels = new IrqTargetHandler[]
                {
                    new IrqTargetHandler(irqController, id)
                };  // OpenTitan only supports a single level
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
                public IrqTargetHandler(OpenTitan_PlatformLevelInterruptController irqController, uint targetId)
                {
                    this.irqController = irqController;
                    this.targetId = targetId;

                    enabledSources = new HashSet<IrqSource>();
                    activeInterrupts = new Stack<IrqSource>();
                }

                public override string ToString()
                {
                    return $"[Hart #{targetId} at (connection output #{ConnectionNumber})]";
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

                private int ConnectionNumber => (int)(4 * targetId);

                private readonly uint targetId;
                private readonly OpenTitan_PlatformLevelInterruptController irqController;
                private readonly HashSet<IrqSource> enabledSources;
                private readonly Stack<IrqSource> activeInterrupts;
            }
        }

        private enum Registers : long
        {
            InterruptPending0 = 0x0,
            InterruptPending1 = 0x4,
            InterruptPending2 = 0x8,
            InterruptSourceMode0 = 0xc,
            InterruptSourceMode1 = 0x10,
            InterruptSourceMode2 = 0x14,

            Source0Priority = 0x18,
            Source1Priority = 0x1C,
            Source2Priority = 0x20,

            Target0MachineEnables = 0x200,
            Target0PriorityThreshold = 0x20C,
            Target0ClaimComplete = 0x210,

	        Target0SoftwareInterrupt = 0x214,

        }

    }
}
