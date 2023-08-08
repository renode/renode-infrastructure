//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class STM32WBA_SPI : STM32H7_SPI
    {
        public STM32WBA_SPI(IMachine machine) : base(machine)
        {
        }

        protected override bool IsWba { get; } = true;
    }
}
