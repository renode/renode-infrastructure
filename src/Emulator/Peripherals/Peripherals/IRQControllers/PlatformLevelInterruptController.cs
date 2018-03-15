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
    public class PlatformLevelInterruptController : IDoubleWordPeripheral, IKnownSize, IIRQController
    {
        public PlatformLevelInterruptController(Machine machine, int numberOfSources, int numberOfTargets = 1, bool prioritiesEnabled = true)
        {
            if(numberOfTargets != 1)
            {
                throw new ConstructionException($"Current {this.GetType().Name} implementation does not support more than one target");
            }
            // numberOfSources has to fit between these two registers, one bit per source
            if(Math.Ceiling((numberOfSources + 1) / 32.0) * 4 > Registers.Target1Enables - Registers.Target0Enables)
            {
                throw new ConstructionException($"Current {this.GetType().Name} implementation more than {Registers.Target1Enables - Registers.Target0Enables} sources");
            }

            this.prioritiesEnabled = prioritiesEnabled;

            this.machine = machine;
            IRQ = new GPIO();
            // irqSources are initialized from 1, as the source "0" is not used.
            irqSources = new IrqSource[numberOfSources + 1];
            for(var i = 1; i <= numberOfSources; i++)
            {
                irqSources[i] = new IrqSource();
            }
            activeInterrupts = new Stack<uint>();

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Target0ClaimComplete, new DoubleWordRegister(this).WithValueField(0, 32, valueProviderCallback: _ =>
                {
                    return AcknowledgePendingInterrupt();
                }, writeCallback:(_, value) =>
                {
                    CompleteHandlingInterrupt(value);
                }
                )}
            };

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

            var vectorWidth = (uint)Registers.Target1Enables - (uint)Registers.Target0Enables;
            var maximumSourceDoubleWords = (int)Math.Ceiling((numberOfSources + 1) / 32.0) * 4;

            // When writing to TargetXEnables registers, the user will get an error in log when he tries to write to an
            // unused bit of a register that is at least partially used. A warning will be issued on a usual undefined register access otherwise.
            for(var target = 0u; target < numberOfTargets; target++)
            {
                for(var offset = 0u; offset < maximumSourceDoubleWords; offset += 4)
                {
                    var lTarget = target;
                    var lOffset = offset;
                    registersMap.Add((long)Registers.Target0Enables + (vectorWidth * target) + offset, new DoubleWordRegister(this).WithValueField(0, 32, writeCallback: (_, value) =>
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
                                if(sourceIdBase + bit == 0)
                                {
                                    //Source number 0 is not used.
                                    continue;
                                }
                                if(irqSources.Length <= (sourceIdBase + bit))
                                {
                                    if(bits[bit])
                                    {
                                        this.Log(LogLevel.Error, "Trying to enable non-existing source: {0}", sourceIdBase + bit);
                                    }
                                    continue;
                                }
                                var targets = irqSources[sourceIdBase + bit].EnabledTargets;
                                if(bits[bit])
                                {
                                    this.Log(LogLevel.Noisy, "Enabling target: {0}", sourceIdBase + bit);
                                    targets.Add(lTarget);
                                }
                                else
                                {
                                    this.Log(LogLevel.Noisy, "Disabling target: {0}", sourceIdBase + bit);
                                    targets.Remove(lTarget);
                                }
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
            activeInterrupts.Clear();
            foreach(var irqSource in irqSources)
            {
                irqSource?.Clear();
            }
            IRQ.Set(false);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            if(number < 1 || number >= irqSources.Length)
            {
                throw new ArgumentOutOfRangeException($"Wrong gpio source: {number}. This IRQ controller supports sources from 1 to {irqSources.Length - 1}.");
            }
            lock(irqSources)
            {
                this.Log(LogLevel.Noisy, "Setting #{0} gpio state to {1}", number, value);

                var irq = irqSources[(uint)number];
                irq.State = value;
                irq.IsPending |= value;
                RefreshInterrupts();
            }
        }

        // it looks like every hart has it's own IRQ line; right now we support only one
        public GPIO IRQ { get; private set; }

        public long Size => 0x4000000;

        private void RefreshInterrupts()
        {
            lock(irqSources)
            {
                bool val;
                // Skip(1) because irqSources[0] is unused.
                if(activeInterrupts.Count > 0)
                {
                    var currentPriority = irqSources[activeInterrupts.Peek()].Priority;
                    val = irqSources.Skip(1).Any(x => x.Priority > currentPriority && x.IsPending && x.EnabledTargets.Any());
                }
                else
                {
                    val = irqSources.Skip(1).Any(x => x.Priority != 0 && x.IsPending && x.EnabledTargets.Any());
                }

                IRQ.Set(val);
            }
        }

        private uint AcknowledgePendingInterrupt()
        {
            lock(irqSources)
            {
                //Select required for by-index ordering. Skip(1) to omit first, unused entry.
                var result = irqSources.Select((x, i) => new { Value = x, Key = (uint)i }).Skip(1)
                                       .Where(x => x.Value.IsPending && x.Value.EnabledTargets.Any())
                                       .OrderByDescending(x => x.Value.Priority)
                                       .ThenBy(x => x.Key).FirstOrDefault();
                if(result?.Value == null)
                {
                    this.Log(LogLevel.Error, "There is no pending interrupt to acknowledge at the moment");
                    return 0;
                }
                result.Value.IsPending = false;
                activeInterrupts.Push(result.Key);

                this.Log(LogLevel.Noisy, "Acknowledging pending interrupt #{0}", result.Key);

                IRQ.Set(false);
                return result.Key;
            }
        }

        private void CompleteHandlingInterrupt(uint irqId)
        {
            lock(irqSources)
            {
                this.Log(LogLevel.Noisy, "Completing irq {0}", irqId);

                var topActiveInterrupt = activeInterrupts.Pop();
                if(topActiveInterrupt != irqId)
                {
                    this.Log(LogLevel.Error, "Trying to complete irq {0}, but {1} is the active one", irqId, topActiveInterrupt);
                    return;
                }
                irqSources[irqId].IsPending = irqSources[irqId].State;
                RefreshInterrupts();
            }
        }

        private Stack<uint> activeInterrupts;
        private readonly IrqSource[] irqSources;
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection registers;
        private readonly bool prioritiesEnabled;

        private class IrqSource
        {
            public IrqSource()
            {
                EnabledTargets = new HashSet<uint>();
                Clear();
            }

            public override string ToString()
            {
                return $"IrqSource; priority: {Priority}, state: {State}, is pending: {IsPending}, has enabled targets: {EnabledTargets.Any()}";
            }

            public void Clear()
            {
                //1 is the default, lowest value. 0 means "no interrupt".
                Priority = 1;
                State = false;
                IsPending = false;
                EnabledTargets.Clear();
            }

            public uint Priority { get; set; }
            public bool State { get; set; }
            public bool IsPending { get; set; }
            public HashSet<uint> EnabledTargets { get; private set; }
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
