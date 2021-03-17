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
    public class PlatformLevelInterruptController : PlatformLevelInterruptControllerBase, IKnownSize
    {
        public PlatformLevelInterruptController(int numberOfSources, int numberOfTargets = 1, bool prioritiesEnabled = true, bool countSourcesFrom0 = false)
            : base(numberOfSources, numberOfTargets, prioritiesEnabled, countSourcesFrom0: false, supportedLevels: new [] { PrivilegeLevel.User, PrivilegeLevel.Supervisor, PrivilegeLevel.Hypervisor, PrivilegeLevel.Machine })
        {
            // numberOfSources has to fit between these two registers, one bit per source
            if(Math.Ceiling((numberOfSources + 1) / 32.0) * 4 > Targets01EnablesWidth)
            {
                throw new ConstructionException($"Current {this.GetType().Name} implementation does not support more than {Targets01EnablesWidth} sources");
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
                                    valueProviderCallback: (_) => GetIrqSource((uint)j).Priority,
                                    writeCallback: (_, value) =>
                                    {
                                        if(prioritiesEnabled)
                                        {
                                            GetIrqSource((uint)j).Priority = value;
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

        public long Size => 0x4000000;

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
