//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_SYSCFG_10
    {
        partial void SYSCFG_Reset()
        {
            registers.Reset();
        }

        partial void Seswversion_Swversion_ValueProvider(ulong a)
        {
            seswversion_swversion_field.Value = 0xFFFFFF;
        }
    }
}