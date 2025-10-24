//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_EMU_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        partial void EMU_Reset()
        {
            registers.Reset();
            // RstCause.Sysreq: upon reset we set the Sysreq bit. 
            // It can be cleared by SW using the Rstcauseclr command.
            rstcause_sysreq_bit.Value = true;
        }

        partial void Cmd_Rstcauseclr_Write(bool a, bool b)
        {
            if(b)
            {
                rstcause_sysreq_bit.Value = false;
            }
        }
    }
}