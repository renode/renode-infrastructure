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

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class PlatformLevelInterruptController : IDoubleWordPeripheral, IKnownSize, IIRQController, INumberedGPIOOutput
    {
        public PlatformLevelInterruptController(Machine machine, int numberOfSources, int numberOfTargets = 1, bool prioritiesEnabled = true)
        {
            // numberOfSources has to fit between these two registers, one bit per source
            if(Math.Ceiling((numberOfSources + 1) / 32.0) * 4 > Registers.Target1Enables - Registers.Target0Enables)
            {
                throw new ConstructionException($"Current {this.GetType().Name} implementation does not support more than {Registers.Target1Enables - Registers.Target0Enables} sources");
            }

            this.prioritiesEnabled = prioritiesEnabled;

            this.machine = machine;
            var connections = new Dictionary<int, IGPIO>();
            for(var i = 0; i < numberOfTargets; i++)
            {
                connections[i] = new GPIO();
            }
            Connections = connections;

            // irqSources are initialized from 1, as the source "0" is not used.
            irqSources = new IrqSource[numberOfSources + 1];
            for(var i = 1u; i <= numberOfSources; i++)
            {
                irqSources[i] = new IrqSource(i);
            }

            irqTargets = new IrqTarget[numberOfTargets];
            for(var i = 0u; i < numberOfTargets; i++)
            {
                irqTargets[i] = new IrqTarget(i, this);
            }

            var registersMap = new Dictionary<long, DoubleWordRegister>();

            registersMap.Add((long)Registers.Source0Priority, new DoubleWordRegister(this)
                             .WithValueField(0, 3, FieldMode.Read,
                                             writeCallback: (_, value) => { if(value != 0) { this.Log(LogLevel.Warning, $"Trying to set priority {value} to Source 0, which is illegal"); } }));
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
                                            this.Log(LogLevel.Noisy, "Setting priority {0} for source #{1}", value, j);
                                            irqSources[j].Priority = value;
                                            RefreshInterrupts();
                                        }
                                    });

            }

            var targetWidth = (uint)Registers.Target1PriorityThreshold - (uint)Registers.Target0PriorityThreshold;
            var vectorWidth = (uint)Registers.Target1Enables - (uint)Registers.Target0Enables;
            var maximumSourceDoubleWords = (int)Math.Ceiling((numberOfSources + 1) / 32.0) * 4;

            // When writing to TargetXEnables registers, the user will get an error in log when he tries to write to an
            // unused bit of a register that is at least partially used. A warning will be issued on a usual undefined register access otherwise.
            for(var targetId = 0u; targetId < numberOfTargets; targetId++)
            {
                var lTargetId = targetId;
                var lTarget = irqTargets[lTargetId];
                registersMap.Add((long)Registers.Target0ClaimComplete + (targetWidth * targetId), new DoubleWordRegister(this).WithValueField(0, 32, valueProviderCallback: _ =>
                {
                    return lTarget.AcknowledgePendingInterrupt();
                }, writeCallback:(_, value) =>
                {
                    lTarget.CompleteHandlingInterrupt(irqSources[value]);
                }
                ));

                for(var offset = 0u; offset < maximumSourceDoubleWords; offset += 4)
                {
                    var lOffset = offset;
                    registersMap.Add((long)Registers.Target0Enables + (vectorWidth * targetId) + offset, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
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
                                if(sourceNumber == 0)
                                {
                                    // Source number 0 is not used.
                                    continue;
                                }
                                if(irqSources.Length <= sourceNumber)
                                {
                                    if(bits[bit])
                                    {
                                        this.Log(LogLevel.Noisy, "Trying to enable non-existing source: {0}", sourceNumber);
                                    }
                                    continue;
                                }

                                lTarget.EnableSource(irqSources[sourceNumber], bits[bit]);
                            }
                            RefreshInterrupts();
                        }
                    }));
                }
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
                throw new ArgumentOutOfRangeException($"Wrong gpio source: {number}. This irq controller supports sources from 1 to {irqSources.Length - 1}.");
            }
            lock(irqSources)
            {
                this.Log(LogLevel.Noisy, "Setting #{0} irq state to {1}", number, value);

                var irq = irqSources[(uint)number];
                irq.State = value;
                irq.IsPending |= value;
                RefreshInterrupts();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }

        public long Size => 0x4000000;

        private void RefreshInterrupts()
        {
            lock(irqSources)
            {
                foreach(var target in irqTargets)
                {
                    target.RefreshInterrupts();
                }
            }
        }

        private readonly IrqSource[] irqSources;
        private readonly IrqTarget[] irqTargets;
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly bool prioritiesEnabled;

        private class IrqSource
        {
            public IrqSource(uint id)
            {
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
            public uint Priority { get; set; }
            public bool State { get; set; }
            public bool IsPending { get; set; }

            // 1 is the default, lowest value. 0 means "no interrupt".
            private const uint DefaultPriority = 1;
        }

        private class IrqTarget
        {
            public IrqTarget(uint id, PlatformLevelInterruptController irqController)
            {
                Id = id;
                this.irqController = irqController;
                activeInterrupts = new Stack<IrqSource>();
                enabledSources = new HashSet<IrqSource>();
            }

            public uint AcknowledgePendingInterrupt()
            {
                lock(irqController.irqSources)
                {
                    var pendingIrq = enabledSources.Where(x => x.IsPending)
                        .OrderByDescending(x => x.Priority)
                        .ThenBy(x => x.Id).FirstOrDefault();
                    if(pendingIrq == null)
                    {
                        irqController.Log(LogLevel.Noisy, "There is no pending interrupt to acknowledge at the moment for target {0}", Id);
                        return 0;
                    }
                    pendingIrq.IsPending = false;
                    activeInterrupts.Push(pendingIrq);

                    irqController.Log(LogLevel.Noisy, "Acknowledging pending interrupt #{0} at target {1}", pendingIrq.Id, Id);

                    RefreshInterrupts();
                    return pendingIrq.Id;
                }
            }

            public void CompleteHandlingInterrupt(IrqSource irq)
            {
                lock(irqController.irqSources)
                {
                    irqController.Log(LogLevel.Noisy, "Completing irq {0} at target {1}", irq.Id, Id);

                    var topActiveInterrupt = activeInterrupts.Pop();
                    if(topActiveInterrupt != irq)
                    {
                        irqController.Log(LogLevel.Error, "Trying to complete irq {0} at target {1}, but {2} is the active one", irq.Id, Id, topActiveInterrupt.Id);
                        return;
                    }

                    irq.IsPending = irq.State;
                    RefreshInterrupts();
                }
            }

            public void RefreshInterrupts()
            {
                lock(irqController.irqSources)
                {
                    var currentPriority = activeInterrupts.Count > 0 ? activeInterrupts.Peek().Priority : 0;
                    irqController.Connections[(int)Id].Set(enabledSources.Any(x => x.Priority > currentPriority && x.IsPending));
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
                RefreshInterrupts();
            }

            public void Reset()
            {
                activeInterrupts.Clear();
                enabledSources.Clear();
                RefreshInterrupts();
            }

            public uint Id { get; private set; }

            private readonly HashSet<IrqSource> enabledSources;
            private readonly PlatformLevelInterruptController irqController;
            private readonly Stack<IrqSource> activeInterrupts;
        }

        private enum Registers : long
        {
            Source0Priority = 0x0, //this is a fake register, as there is no source 0, but the software writes to it anyway.
            Source1Priority = 0x4,
            Source2Priority = 0x8,
            // ...
            StartOfPendingArray = 0x1000,
            Target0Enables = 0x2000,
            Target1Enables = 0x2080,
            // ...
            Target0PriorityThreshold = 0x200000,
            Target0ClaimComplete = 0x200004,
            // ...
            Target1PriorityThreshold = 0x201000,
            Target1ClaimComplete = 0x201004
        }
    }
}
