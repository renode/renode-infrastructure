//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2022-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_DCDC_2
    {
        partial void Status_Bypsw_ValueProvider(bool a)
        {
            // Enable BYPASS switch.
            status_bypsw_bit.Value = isEnabled;
        }

        partial void If_Regulation_ValueProvider(bool a)
        {
            // Complete DCDC startup
            if_regulation_bit.Value = isEnabled;
        }

        private readonly bool isEnabled = true;
    }
}