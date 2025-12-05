//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public class SiLabs_RvCfg_2 : SiLabsPeripheral, SiLabs_IRvConfig
    {
        public SiLabs_RvCfg_2(Machine machine) : base(machine)
        {
        }

        public ulong BootAddress
        {
            get
            {
                return bootAddress.Value;
            }
        }

        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.BootAddressConfig, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out bootAddress, name: "BOOTADDRCFG")
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        protected override Type RegistersType => typeof(Registers);

        private IValueRegisterField bootAddress;

        private enum Registers
        {
            IpVersion                                 = 0x0000,
            SoftwareReset                             = 0x0004,
            IsoControl                                = 0x0008,
            IsoStatus                                 = 0x000C,
            FetchEnable                               = 0x0020,
            BootAddressConfig                         = 0x0024,
            MtVecAddressConfig                        = 0x0028,
            Status                                    = 0x002C,
            PcSample                                  = 0x0030,
            // Set registers
            IpVersion_Set                             = 0x1000,
            SoftwareReset_Set                         = 0x1004,
            IsoControl_Set                            = 0x1008,
            IsoStatus_Set                             = 0x100C,
            FetchEnable_Set                           = 0x1020,
            BootAddressConfig_Set                     = 0x1024,
            MtVecAddressConfig_Set                    = 0x1028,
            Status_Set                                = 0x102C,
            PcSample_Set                              = 0x1030,
            // Clear registers
            IpVersion_Clr                             = 0x2000,
            SoftwareReset_Clr                         = 0x2004,
            IsoControl_Clr                            = 0x2008,
            IsoStatus_Clr                             = 0x200C,
            FetchEnable_Clr                           = 0x2020,
            BootAddressConfig_Clr                     = 0x2024,
            MtVecAddressConfig_Clr                    = 0x2028,
            Status_Clr                                = 0x202C,
            PcSample_Clr                              = 0x2030,
            // Toggle registers
            IpVersion_Tgl                             = 0x3000,
            SoftwareReset_Tgl                         = 0x3004,
            IsoControl_Tgl                            = 0x3008,
            IsoStatus_Tgl                             = 0x300C,
            FetchEnable_Tgl                           = 0x3020,
            BootAddressConfig_Tgl                     = 0x3024,
            MtVecAddressConfig_Tgl                    = 0x3028,
            Status_Tgl                                = 0x302C,
            PcSample_Tgl                              = 0x3030,
        }
    }
}