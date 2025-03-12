//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Silabs
{
    public partial class Fault : IFault
    {
        public Fault(IMachine machine)
        {
            this.machine = machine;
        }

        // See constant definitions in src/Infrastructure/src/Emulator/Cores/tlib/arch/arm/cpu.h
        public uint EXCP_PREFETCH_ABORT = 3;

        public void BusFault(uint exception)
        {
            if (
                machine.SystemBus.TryGetCurrentCPU(out var cpu)
                && cpu is TranslationCPU translationCPU
            )
            {
                translationCPU.RaiseException(exception);
            }
        }

        public void HardFault(uint exception)
        {
            if (
                machine.SystemBus.TryGetCurrentCPU(out var cpu)
                && cpu is TranslationCPU translationCPU
            )
            {
                translationCPU.RaiseException(exception);
            }
        }        

        private readonly IMachine machine;
    }
}
