//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public class EXTI : STM32F4_EXTI
    {
        public EXTI(IMachine machine, int numberOfOutputLines = 14, int firstDirectLine = STM32F4_EXTI.DefaultFirstDirectLine)
            : base(machine, numberOfOutputLines, firstDirectLine)
        {
            this.Log(LogLevel.Warning, "Peripheral '{0}' is deprecated. '{1}' should be used instead", nameof(EXTI), nameof(STM32F4_EXTI));
        }
    }
}
