//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CRC
{
    public class STM32F0_CRC : STM32_CRCBase
    {
        public STM32F0_CRC(bool configurablePoly) : base(configurablePoly, IndependentDataWidth.Bits8)
        {
        }
    }
}
