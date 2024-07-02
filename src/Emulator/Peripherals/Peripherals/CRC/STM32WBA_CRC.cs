//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.CRC
{
    public class STM32WBA_CRC : STM32_CRCBase
    {
        public STM32WBA_CRC() : base(true, IndependentDataWidth.Bits32)
        {
        }
    }
}
