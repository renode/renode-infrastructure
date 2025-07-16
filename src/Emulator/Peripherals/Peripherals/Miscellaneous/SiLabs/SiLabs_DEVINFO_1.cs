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

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public partial class SiLabs_DEVINFO_1
    {
        partial void SiLabs_DEVINFO_1_Constructor()
        {
            UID = ++count;
        }

        private static uint count = 0;
        private ulong UID;
        public static readonly ulong OUI64 = 0xCCCCCC0000000000;
        public ulong EUI64 
        { 
            get
            {
                return (ulong)(OUI64 + UID);
            } 
        }

        partial void Main0_Hfrcocaldefault_Datahfrcocaldefault_ValueProvider(ulong a)
        {
            main0_hfrcocaldefault_datahfrcocaldefault_field.Value = 42;
        }


        partial void Main0_Hfrcocalspeed_Datahfrcocalspeed_ValueProvider(ulong a)
        {
            main0_hfrcocalspeed_datahfrcocalspeed_field.Value = 42;
        }

        partial void Main0_Swcapa_Dataswcapa_ValueProvider(ulong a)
        {
            // 0x00000002: ZIGBEE_LEVEL2
            // 0x40000000: NCPEN
            // 0x80000000: RFMCUEN
            main0_swcapa_dataswcapa_field.Value = (0x00000002UL | 0x40000000UL | 0x80000000UL);
        }

        partial void Main0_Eui64l_Dataeui64l_ValueProvider(ulong a)
        {
            main0_eui64l_dataeui64l_field.Value = (uint)(EUI64 & 0xFFFFFFFF);
        }

        partial void Main0_Eui64h_Dataeui64h_ValueProvider(ulong a)
        {
            main0_eui64h_dataeui64h_field.Value = (uint)(EUI64 >> 32);
        }
    }
}