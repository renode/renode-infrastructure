//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.IRQControllers.PLIC
{
    public interface IPlatformLevelInterruptController : IEmulationElement
    {
        /// <summary>
        /// Setting this property to a value different than -1 causes all interrupts to be reported to a context with a given id.
        ///
        /// This is mostly for debugging purposes.
        /// It allows to designate a single core (in a multi-core setup) to handle external interrupts making it easier to debug trap handlers.
        /// </summary>
        int ForcedContext { get; }

        IReadOnlyDictionary<int, IGPIO> Connections { get; }
    }
}
