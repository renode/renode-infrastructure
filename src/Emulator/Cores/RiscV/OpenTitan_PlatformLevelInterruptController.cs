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

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class OpenTitan_PlatformLevelInterruptController : PlatformLevelInterruptControllerBase, IKnownSize
    {
        public OpenTitan_PlatformLevelInterruptController()
            : base(numberOfSources: NumberOfSources, numberOfTargets: 1, prioritiesEnabled: true, countSourcesFrom0: true, supportedLevels: null)
        {
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

            for(var i = 0; i <= NumberOfSources; i++)
            {
                var j = i;
                registersMap[(long)Registers.Source0Priority +  (4 * i)] = new DoubleWordRegister(this)
                    .WithValueField(0, 3,
                                    valueProviderCallback: (_) => irqSources[j].Priority,
                                    writeCallback: (_, value) =>
                                    {
                                        irqSources[j].Priority = value;
                                        RefreshInterrupts();
                                    });
            }

            AddTargetEnablesRegister(registersMap, (long)Registers.Target0MachineEnables, 0, (PrivilegeLevel)0, NumberOfSources);
            AddTargetClaimCompleteRegister(registersMap, (long)Registers.Target0ClaimComplete, 0, (PrivilegeLevel)0);

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public long Size => 0x1000;

        private const int NumberOfSources = 3;

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
