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

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.QuadWordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class PlatformLevelInterruptController : PlatformLevelInterruptControllerBase, IKnownSize
    {
        public PlatformLevelInterruptController(int numberOfSources, int numberOfContexts, bool prioritiesEnabled = true)
            // we set numberOfSources + 1, because the standard PLIC controller counts sources from 1
            : base(numberOfSources + 1, numberOfContexts, prioritiesEnabled)
        {
            if(numberOfSources + 1 > MaxSources)
            {
                throw new ConstructionException($"Current {this.GetType().Name} implementation does not support more than {MaxSources} sources");
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
                                            irqSources[j].Priority = (uint)value;
                                            RefreshInterrupts();
                                        }
                                    });
            }

            for(var i = 0u; i < numberOfContexts; i++)
            {
                AddContextEnablesRegister(registersMap, (long)Registers.Context0Enables + i * ContextEnablesWidth, i, numberOfSources);
                AddContextClaimCompleteRegister(registersMap, (long)Registers.Context0ClaimComplete + i * ContextClaimWidth, i);
                AddContextPriorityThresholdRegister(registersMap, (long)Registers.Context0PriorityThreshold + i * ContextClaimWidth, i);
            }

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public long Size => 0x4000000;

        protected override bool IsIrqSourceAvailable(int number)
        {
            // the standard PLIC controller does not support source 0
            return number != 0 && base.IsIrqSourceAvailable(number);
        }

        private enum Registers : long
        {
            Source0Priority = 0x0, //this is a fake register, as there is no source 0, but the software writes to it anyway.
            Source1Priority = 0x4,
            Source2Priority = 0x8,
            // ...
            StartOfPendingArray = 0x1000,
            Context0Enables = 0x2000,
            Context1Enables = 0x2080,
            Context2Enables = 0x2100,
            // ...
            Context0PriorityThreshold = 0x200000,
            Context0ClaimComplete = 0x200004,
            // ...
            Context1PriorityThreshold = 0x201000,
            Context1ClaimComplete = 0x201004,
            // ...
            Context2PriorityThreshold = 0x202000,
            Context2ClaimComplete = 0x202004,
            //
            Context3PriorityThreshold = 0x203000,
            Context3ClaimComplete = 0x203004,
            //
            Context4PriorityThreshold = 0x204000,
            Context4ClaimComplete = 0x204004,
            // ...
        }

        private const long ContextEnablesWidth = Registers.Context1Enables - Registers.Context0Enables;
        private const long ContextClaimWidth = Registers.Context1ClaimComplete - Registers.Context0ClaimComplete;
        private const uint MaxSources = (uint)(ContextEnablesWidth / 4) * 32;
    }
}
