//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public partial class SiLabs_DEVINFO_1
    {
        public SiLabs_DEVINFO_1(Machine machine) : base(machine)
        {
        }

        partial void DEVINFO_Reset()
        {
        }

        public ulong EUI64
        {
            get
            {
                return OUI64 + (ulong)machine.UniqueObjectId;
            }
        }

        public static readonly ulong OUI64 = 0xCCCCCC0000000000;

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

        private readonly bool isEnabled = true;
    }
}
