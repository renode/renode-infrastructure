//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    DEVINFO, Generated on : 2024-08-05 17:50:38.273238
    DEVINFO, ID Version : 12af773e0bc64b7ab54b37e74a2b4aa6.1 */

/* Here is the template for your defined by hand class. Don't forget to add your eventual constructor with extra parameter.

* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * 
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
    public partial class SiLabs_DEVINFO_1
    {
        public SiLabs_DEVINFO_1(Machine machine) : base(machine)
        {
            SiLabs_DEVINFO_1_constructor();
        }
    }
}
* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_DEVINFO_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_DEVINFO_1(Machine machine) : base(machine)
        {
            Define_Registers();
            SiLabs_DEVINFO_1_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Main0_Revision, GenerateMain0_revisionRegister()},
                {(long)Registers.Main0_Embmsize, GenerateMain0_embmsizeRegister()},
                {(long)Registers.Main0_Stackmsize, GenerateMain0_stackmsizeRegister()},
                {(long)Registers.Main0_Swcapa, GenerateMain0_swcapaRegister()},
                {(long)Registers.Main0_Hfrcocaldefault, GenerateMain0_hfrcocaldefaultRegister()},
                {(long)Registers.Main0_Hfrcocalspeed, GenerateMain0_hfrcocalspeedRegister()},
                {(long)Registers.Main0_Eui64l, GenerateMain0_eui64lRegister()},
                {(long)Registers.Main0_Eui64h, GenerateMain0_eui64hRegister()},
                {(long)Registers.Main0_Part, GenerateMain0_partRegister()},
                {(long)Registers.Spare0_Register_group, GenerateSpare0_register_groupRegister()},
                {(long)Registers.Spare1_Register_group, GenerateSpare1_register_groupRegister()},
                {(long)Registers.Spare2_Register_group, GenerateSpare2_register_groupRegister()},
                {(long)Registers.Spare3_Register_group, GenerateSpare3_register_groupRegister()},
                {(long)Registers.Spare4_Register_group, GenerateSpare4_register_groupRegister()},
                {(long)Registers.Spare5_Register_group, GenerateSpare5_register_groupRegister()},
                {(long)Registers.Spare6_Register_group, GenerateSpare6_register_groupRegister()},
                {(long)Registers.Main1_Hfxocal, GenerateMain1_hfxocalRegister()},
                {(long)Registers.Main1_Moduleinfo, GenerateMain1_moduleinfoRegister()},
                {(long)Registers.Main1_Legacy, GenerateMain1_legacyRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            DEVINFO_Reset();
        }
        
        // Main0_Revision - Offset : 0x100
        protected DoubleWordRegister  GenerateMain0_revisionRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main0_revision_datarevision_field, 
                    valueProviderCallback: (_) => {
                        Main0_Revision_Datarevision_ValueProvider(_);
                        return main0_revision_datarevision_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Revision_Datarevision_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Revision_Datarevision_Read(_, __),
                    name: "Datarevision")
            .WithReadCallback((_, __) => Main0_Revision_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Revision_Write(_, __));
        
        // Main0_Embmsize - Offset : 0x104
        protected DoubleWordRegister  GenerateMain0_embmsizeRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 16, out main0_embmsize_nvm_field, 
                    valueProviderCallback: (_) => {
                        Main0_Embmsize_Nvm_ValueProvider(_);
                        return main0_embmsize_nvm_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Embmsize_Nvm_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Embmsize_Nvm_Read(_, __),
                    name: "Nvm")
            
            .WithValueField(16, 5, out main0_embmsize_rsvd_field, 
                    valueProviderCallback: (_) => {
                        Main0_Embmsize_Rsvd_ValueProvider(_);
                        return main0_embmsize_rsvd_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Embmsize_Rsvd_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Embmsize_Rsvd_Read(_, __),
                    name: "Rsvd")
            
            .WithValueField(21, 11, out main0_embmsize_sram_field, 
                    valueProviderCallback: (_) => {
                        Main0_Embmsize_Sram_ValueProvider(_);
                        return main0_embmsize_sram_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Embmsize_Sram_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Embmsize_Sram_Read(_, __),
                    name: "Sram")
            .WithReadCallback((_, __) => Main0_Embmsize_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Embmsize_Write(_, __));
        
        // Main0_Stackmsize - Offset : 0x108
        protected DoubleWordRegister  GenerateMain0_stackmsizeRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 16, out main0_stackmsize_flash_field, 
                    valueProviderCallback: (_) => {
                        Main0_Stackmsize_Flash_ValueProvider(_);
                        return main0_stackmsize_flash_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Stackmsize_Flash_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Stackmsize_Flash_Read(_, __),
                    name: "Flash")
            
            .WithValueField(16, 16, out main0_stackmsize_psram_field, 
                    valueProviderCallback: (_) => {
                        Main0_Stackmsize_Psram_ValueProvider(_);
                        return main0_stackmsize_psram_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Stackmsize_Psram_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Stackmsize_Psram_Read(_, __),
                    name: "Psram")
            .WithReadCallback((_, __) => Main0_Stackmsize_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Stackmsize_Write(_, __));
        
        // Main0_Swcapa - Offset : 0x10C
        protected DoubleWordRegister  GenerateMain0_swcapaRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main0_swcapa_dataswcapa_field, 
                    valueProviderCallback: (_) => {
                        Main0_Swcapa_Dataswcapa_ValueProvider(_);
                        return main0_swcapa_dataswcapa_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Swcapa_Dataswcapa_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Swcapa_Dataswcapa_Read(_, __),
                    name: "Dataswcapa")
            .WithReadCallback((_, __) => Main0_Swcapa_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Swcapa_Write(_, __));
        
        // Main0_Hfrcocaldefault - Offset : 0x110
        protected DoubleWordRegister  GenerateMain0_hfrcocaldefaultRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main0_hfrcocaldefault_datahfrcocaldefault_field, 
                    valueProviderCallback: (_) => {
                        Main0_Hfrcocaldefault_Datahfrcocaldefault_ValueProvider(_);
                        return main0_hfrcocaldefault_datahfrcocaldefault_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Hfrcocaldefault_Datahfrcocaldefault_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Hfrcocaldefault_Datahfrcocaldefault_Read(_, __),
                    name: "Datahfrcocaldefault")
            .WithReadCallback((_, __) => Main0_Hfrcocaldefault_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Hfrcocaldefault_Write(_, __));
        
        // Main0_Hfrcocalspeed - Offset : 0x114
        protected DoubleWordRegister  GenerateMain0_hfrcocalspeedRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main0_hfrcocalspeed_datahfrcocalspeed_field, 
                    valueProviderCallback: (_) => {
                        Main0_Hfrcocalspeed_Datahfrcocalspeed_ValueProvider(_);
                        return main0_hfrcocalspeed_datahfrcocalspeed_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Hfrcocalspeed_Datahfrcocalspeed_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Hfrcocalspeed_Datahfrcocalspeed_Read(_, __),
                    name: "Datahfrcocalspeed")
            .WithReadCallback((_, __) => Main0_Hfrcocalspeed_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Hfrcocalspeed_Write(_, __));
        
        // Main0_Eui64l - Offset : 0x118
        protected DoubleWordRegister  GenerateMain0_eui64lRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main0_eui64l_dataeui64l_field, 
                    valueProviderCallback: (_) => {
                        Main0_Eui64l_Dataeui64l_ValueProvider(_);
                        return main0_eui64l_dataeui64l_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Eui64l_Dataeui64l_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Eui64l_Dataeui64l_Read(_, __),
                    name: "Dataeui64l")
            .WithReadCallback((_, __) => Main0_Eui64l_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Eui64l_Write(_, __));
        
        // Main0_Eui64h - Offset : 0x11C
        protected DoubleWordRegister  GenerateMain0_eui64hRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main0_eui64h_dataeui64h_field, 
                    valueProviderCallback: (_) => {
                        Main0_Eui64h_Dataeui64h_ValueProvider(_);
                        return main0_eui64h_dataeui64h_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Eui64h_Dataeui64h_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Eui64h_Dataeui64h_Read(_, __),
                    name: "Dataeui64h")
            .WithReadCallback((_, __) => Main0_Eui64h_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Eui64h_Write(_, __));
        
        // Main0_Part - Offset : 0x120
        protected DoubleWordRegister  GenerateMain0_partRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main0_part_datapart_field, 
                    valueProviderCallback: (_) => {
                        Main0_Part_Datapart_ValueProvider(_);
                        return main0_part_datapart_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main0_Part_Datapart_Write(_, __),
                    
                    readCallback: (_, __) => Main0_Part_Datapart_Read(_, __),
                    name: "Datapart")
            .WithReadCallback((_, __) => Main0_Part_Read(_, __))
            .WithWriteCallback((_, __) => Main0_Part_Write(_, __));
        
        // Spare0_Register_group - Offset : 0x124
        protected DoubleWordRegister  GenerateSpare0_register_groupRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out spare_register_group_dataspare_field[0], 
                    valueProviderCallback: (_) => {
                        Spare_Register_group_Dataspare_ValueProvider(0, _);
                        return spare_register_group_dataspare_field[0].Value;
                    },
                    
                    writeCallback: (_, __) => Spare_Register_group_Dataspare_Write(0,_, __),
                    
                    readCallback: (_, __) => Spare_Register_group_Dataspare_Read(0,_, __),
                    name: "Dataspare")
            .WithReadCallback((_, __) => Spare_Register_group_Read(0, _, __))
            .WithWriteCallback((_, __) => Spare_Register_group_Write(0, _, __));
        
        // Spare1_Register_group - Offset : 0x128
        protected DoubleWordRegister  GenerateSpare1_register_groupRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out spare_register_group_dataspare_field[1], 
                    valueProviderCallback: (_) => {
                        Spare_Register_group_Dataspare_ValueProvider(1, _);
                        return spare_register_group_dataspare_field[1].Value;
                    },
                    
                    writeCallback: (_, __) => Spare_Register_group_Dataspare_Write(1,_, __),
                    
                    readCallback: (_, __) => Spare_Register_group_Dataspare_Read(1,_, __),
                    name: "Dataspare")
            .WithReadCallback((_, __) => Spare_Register_group_Read(1, _, __))
            .WithWriteCallback((_, __) => Spare_Register_group_Write(1, _, __));
        
        // Spare2_Register_group - Offset : 0x12C
        protected DoubleWordRegister  GenerateSpare2_register_groupRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out spare_register_group_dataspare_field[2], 
                    valueProviderCallback: (_) => {
                        Spare_Register_group_Dataspare_ValueProvider(2, _);
                        return spare_register_group_dataspare_field[2].Value;
                    },
                    
                    writeCallback: (_, __) => Spare_Register_group_Dataspare_Write(2,_, __),
                    
                    readCallback: (_, __) => Spare_Register_group_Dataspare_Read(2,_, __),
                    name: "Dataspare")
            .WithReadCallback((_, __) => Spare_Register_group_Read(2, _, __))
            .WithWriteCallback((_, __) => Spare_Register_group_Write(2, _, __));
        
        // Spare3_Register_group - Offset : 0x130
        protected DoubleWordRegister  GenerateSpare3_register_groupRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out spare_register_group_dataspare_field[3], 
                    valueProviderCallback: (_) => {
                        Spare_Register_group_Dataspare_ValueProvider(3, _);
                        return spare_register_group_dataspare_field[3].Value;
                    },
                    
                    writeCallback: (_, __) => Spare_Register_group_Dataspare_Write(3,_, __),
                    
                    readCallback: (_, __) => Spare_Register_group_Dataspare_Read(3,_, __),
                    name: "Dataspare")
            .WithReadCallback((_, __) => Spare_Register_group_Read(3, _, __))
            .WithWriteCallback((_, __) => Spare_Register_group_Write(3, _, __));
        
        // Spare4_Register_group - Offset : 0x134
        protected DoubleWordRegister  GenerateSpare4_register_groupRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out spare_register_group_dataspare_field[4], 
                    valueProviderCallback: (_) => {
                        Spare_Register_group_Dataspare_ValueProvider(4, _);
                        return spare_register_group_dataspare_field[4].Value;
                    },
                    
                    writeCallback: (_, __) => Spare_Register_group_Dataspare_Write(4,_, __),
                    
                    readCallback: (_, __) => Spare_Register_group_Dataspare_Read(4,_, __),
                    name: "Dataspare")
            .WithReadCallback((_, __) => Spare_Register_group_Read(4, _, __))
            .WithWriteCallback((_, __) => Spare_Register_group_Write(4, _, __));
        
        // Spare5_Register_group - Offset : 0x138
        protected DoubleWordRegister  GenerateSpare5_register_groupRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out spare_register_group_dataspare_field[5], 
                    valueProviderCallback: (_) => {
                        Spare_Register_group_Dataspare_ValueProvider(5, _);
                        return spare_register_group_dataspare_field[5].Value;
                    },
                    
                    writeCallback: (_, __) => Spare_Register_group_Dataspare_Write(5,_, __),
                    
                    readCallback: (_, __) => Spare_Register_group_Dataspare_Read(5,_, __),
                    name: "Dataspare")
            .WithReadCallback((_, __) => Spare_Register_group_Read(5, _, __))
            .WithWriteCallback((_, __) => Spare_Register_group_Write(5, _, __));
        
        // Spare6_Register_group - Offset : 0x13C
        protected DoubleWordRegister  GenerateSpare6_register_groupRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out spare_register_group_dataspare_field[6], 
                    valueProviderCallback: (_) => {
                        Spare_Register_group_Dataspare_ValueProvider(6, _);
                        return spare_register_group_dataspare_field[6].Value;
                    },
                    
                    writeCallback: (_, __) => Spare_Register_group_Dataspare_Write(6,_, __),
                    
                    readCallback: (_, __) => Spare_Register_group_Dataspare_Read(6,_, __),
                    name: "Dataspare")
            .WithReadCallback((_, __) => Spare_Register_group_Read(6, _, __))
            .WithWriteCallback((_, __) => Spare_Register_group_Write(6, _, __));
        
        // Main1_Hfxocal - Offset : 0x1F4
        protected DoubleWordRegister  GenerateMain1_hfxocalRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main1_hfxocal_datahfxocal_field, 
                    valueProviderCallback: (_) => {
                        Main1_Hfxocal_Datahfxocal_ValueProvider(_);
                        return main1_hfxocal_datahfxocal_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main1_Hfxocal_Datahfxocal_Write(_, __),
                    
                    readCallback: (_, __) => Main1_Hfxocal_Datahfxocal_Read(_, __),
                    name: "Datahfxocal")
            .WithReadCallback((_, __) => Main1_Hfxocal_Read(_, __))
            .WithWriteCallback((_, __) => Main1_Hfxocal_Write(_, __));
        
        // Main1_Moduleinfo - Offset : 0x1F8
        protected DoubleWordRegister  GenerateMain1_moduleinfoRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out main1_moduleinfo_datamoduleinfo_field, 
                    valueProviderCallback: (_) => {
                        Main1_Moduleinfo_Datamoduleinfo_ValueProvider(_);
                        return main1_moduleinfo_datamoduleinfo_field.Value;
                    },
                    
                    writeCallback: (_, __) => Main1_Moduleinfo_Datamoduleinfo_Write(_, __),
                    
                    readCallback: (_, __) => Main1_Moduleinfo_Datamoduleinfo_Read(_, __),
                    name: "Datamoduleinfo")
            .WithReadCallback((_, __) => Main1_Moduleinfo_Read(_, __))
            .WithWriteCallback((_, __) => Main1_Moduleinfo_Write(_, __));
        
        // Main1_Legacy - Offset : 0x1FC
        protected DoubleWordRegister  GenerateMain1_legacyRegister() => new DoubleWordRegister(this, 0x810000)
            
            .WithValueField(0, 32, out main1_legacy_datalegacy_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Main1_Legacy_Datalegacy_ValueProvider(_);
                        return main1_legacy_datalegacy_field.Value;
                    },
                    
                    readCallback: (_, __) => Main1_Legacy_Datalegacy_Read(_, __),
                    name: "Datalegacy")
            .WithReadCallback((_, __) => Main1_Legacy_Read(_, __))
            .WithWriteCallback((_, __) => Main1_Legacy_Write(_, __));
        

        private uint ReadWFIFO()
        {
            this.Log(LogLevel.Warning, "Reading from a WFIFO Field, value returned will always be 0");
            return 0x0;
        }

        private uint ReadLFWSYNC()
        {
            this.Log(LogLevel.Warning, "Reading from a LFWSYNC/HVLFWSYNC Field, value returned will always be 0");
            return 0x0;
        }

        private uint ReadRFIFO()
        {
            this.Log(LogLevel.Warning, "Reading from a RFIFO Field, value returned will always be 0");
            return 0x0;
        }

        



        
        // Main0_Revision - Offset : 0x100
    
        protected IValueRegisterField main0_revision_datarevision_field;
        partial void Main0_Revision_Datarevision_Write(ulong a, ulong b);
        partial void Main0_Revision_Datarevision_Read(ulong a, ulong b);
        partial void Main0_Revision_Datarevision_ValueProvider(ulong a);
        partial void Main0_Revision_Write(uint a, uint b);
        partial void Main0_Revision_Read(uint a, uint b);
        
        // Main0_Embmsize - Offset : 0x104
    
        protected IValueRegisterField main0_embmsize_nvm_field;
        partial void Main0_Embmsize_Nvm_Write(ulong a, ulong b);
        partial void Main0_Embmsize_Nvm_Read(ulong a, ulong b);
        partial void Main0_Embmsize_Nvm_ValueProvider(ulong a);
    
        protected IValueRegisterField main0_embmsize_rsvd_field;
        partial void Main0_Embmsize_Rsvd_Write(ulong a, ulong b);
        partial void Main0_Embmsize_Rsvd_Read(ulong a, ulong b);
        partial void Main0_Embmsize_Rsvd_ValueProvider(ulong a);
    
        protected IValueRegisterField main0_embmsize_sram_field;
        partial void Main0_Embmsize_Sram_Write(ulong a, ulong b);
        partial void Main0_Embmsize_Sram_Read(ulong a, ulong b);
        partial void Main0_Embmsize_Sram_ValueProvider(ulong a);
        partial void Main0_Embmsize_Write(uint a, uint b);
        partial void Main0_Embmsize_Read(uint a, uint b);
        
        // Main0_Stackmsize - Offset : 0x108
    
        protected IValueRegisterField main0_stackmsize_flash_field;
        partial void Main0_Stackmsize_Flash_Write(ulong a, ulong b);
        partial void Main0_Stackmsize_Flash_Read(ulong a, ulong b);
        partial void Main0_Stackmsize_Flash_ValueProvider(ulong a);
    
        protected IValueRegisterField main0_stackmsize_psram_field;
        partial void Main0_Stackmsize_Psram_Write(ulong a, ulong b);
        partial void Main0_Stackmsize_Psram_Read(ulong a, ulong b);
        partial void Main0_Stackmsize_Psram_ValueProvider(ulong a);
        partial void Main0_Stackmsize_Write(uint a, uint b);
        partial void Main0_Stackmsize_Read(uint a, uint b);
        
        // Main0_Swcapa - Offset : 0x10C
    
        protected IValueRegisterField main0_swcapa_dataswcapa_field;
        partial void Main0_Swcapa_Dataswcapa_Write(ulong a, ulong b);
        partial void Main0_Swcapa_Dataswcapa_Read(ulong a, ulong b);
        partial void Main0_Swcapa_Dataswcapa_ValueProvider(ulong a);
        partial void Main0_Swcapa_Write(uint a, uint b);
        partial void Main0_Swcapa_Read(uint a, uint b);
        
        // Main0_Hfrcocaldefault - Offset : 0x110
    
        protected IValueRegisterField main0_hfrcocaldefault_datahfrcocaldefault_field;
        partial void Main0_Hfrcocaldefault_Datahfrcocaldefault_Write(ulong a, ulong b);
        partial void Main0_Hfrcocaldefault_Datahfrcocaldefault_Read(ulong a, ulong b);
        partial void Main0_Hfrcocaldefault_Datahfrcocaldefault_ValueProvider(ulong a);
        partial void Main0_Hfrcocaldefault_Write(uint a, uint b);
        partial void Main0_Hfrcocaldefault_Read(uint a, uint b);
        
        // Main0_Hfrcocalspeed - Offset : 0x114
    
        protected IValueRegisterField main0_hfrcocalspeed_datahfrcocalspeed_field;
        partial void Main0_Hfrcocalspeed_Datahfrcocalspeed_Write(ulong a, ulong b);
        partial void Main0_Hfrcocalspeed_Datahfrcocalspeed_Read(ulong a, ulong b);
        partial void Main0_Hfrcocalspeed_Datahfrcocalspeed_ValueProvider(ulong a);
        partial void Main0_Hfrcocalspeed_Write(uint a, uint b);
        partial void Main0_Hfrcocalspeed_Read(uint a, uint b);
        
        // Main0_Eui64l - Offset : 0x118
    
        protected IValueRegisterField main0_eui64l_dataeui64l_field;
        partial void Main0_Eui64l_Dataeui64l_Write(ulong a, ulong b);
        partial void Main0_Eui64l_Dataeui64l_Read(ulong a, ulong b);
        partial void Main0_Eui64l_Dataeui64l_ValueProvider(ulong a);
        partial void Main0_Eui64l_Write(uint a, uint b);
        partial void Main0_Eui64l_Read(uint a, uint b);
        
        // Main0_Eui64h - Offset : 0x11C
    
        protected IValueRegisterField main0_eui64h_dataeui64h_field;
        partial void Main0_Eui64h_Dataeui64h_Write(ulong a, ulong b);
        partial void Main0_Eui64h_Dataeui64h_Read(ulong a, ulong b);
        partial void Main0_Eui64h_Dataeui64h_ValueProvider(ulong a);
        partial void Main0_Eui64h_Write(uint a, uint b);
        partial void Main0_Eui64h_Read(uint a, uint b);
        
        // Main0_Part - Offset : 0x120
    
        protected IValueRegisterField main0_part_datapart_field;
        partial void Main0_Part_Datapart_Write(ulong a, ulong b);
        partial void Main0_Part_Datapart_Read(ulong a, ulong b);
        partial void Main0_Part_Datapart_ValueProvider(ulong a);
        partial void Main0_Part_Write(uint a, uint b);
        partial void Main0_Part_Read(uint a, uint b);
        
        // Spare0_Register_group - Offset : 0x124
    
    
        protected IValueRegisterField[] spare_register_group_dataspare_field = new IValueRegisterField[7];
        partial void Spare_Register_group_Dataspare_Write(ulong index, ulong a, ulong b);
        partial void Spare_Register_group_Dataspare_Read(ulong index, ulong a, ulong b);
        partial void Spare_Register_group_Dataspare_ValueProvider(ulong index, ulong a);
        partial void Spare_Register_group_Write(ulong index, uint a, uint b);
        partial void Spare_Register_group_Read(ulong index, uint a, uint b);
        
    
    
        
    
    
        
    
    
        
    
    
        
    
    
        
    
    
        
        // Main1_Hfxocal - Offset : 0x1F4
    
        protected IValueRegisterField main1_hfxocal_datahfxocal_field;
        partial void Main1_Hfxocal_Datahfxocal_Write(ulong a, ulong b);
        partial void Main1_Hfxocal_Datahfxocal_Read(ulong a, ulong b);
        partial void Main1_Hfxocal_Datahfxocal_ValueProvider(ulong a);
        partial void Main1_Hfxocal_Write(uint a, uint b);
        partial void Main1_Hfxocal_Read(uint a, uint b);
        
        // Main1_Moduleinfo - Offset : 0x1F8
    
        protected IValueRegisterField main1_moduleinfo_datamoduleinfo_field;
        partial void Main1_Moduleinfo_Datamoduleinfo_Write(ulong a, ulong b);
        partial void Main1_Moduleinfo_Datamoduleinfo_Read(ulong a, ulong b);
        partial void Main1_Moduleinfo_Datamoduleinfo_ValueProvider(ulong a);
        partial void Main1_Moduleinfo_Write(uint a, uint b);
        partial void Main1_Moduleinfo_Read(uint a, uint b);
        
        // Main1_Legacy - Offset : 0x1FC
    
        protected IValueRegisterField main1_legacy_datalegacy_field;
        partial void Main1_Legacy_Datalegacy_Read(ulong a, ulong b);
        partial void Main1_Legacy_Datalegacy_ValueProvider(ulong a);
        partial void Main1_Legacy_Write(uint a, uint b);
        partial void Main1_Legacy_Read(uint a, uint b);
        partial void DEVINFO_Reset();

        partial void SiLabs_DEVINFO_1_Constructor();

        public bool Enabled = true;

        private SiLabs_ICMU _cmu;
        private SiLabs_ICMU cmu
        {
            get
            {
                if (Object.ReferenceEquals(_cmu, null))
                {
                    foreach(var cmu in machine.GetPeripheralsOfType<SiLabs_ICMU>())
                    {
                        _cmu = cmu;
                    }
                }
                return _cmu;
            }
            set
            {
                _cmu = value;
            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            long temp = offset & 0x0FFF;
            switch(offset & 0x3000){
                case 0x0000:
                    return registers.Read(offset);
                default:
                    this.Log(LogLevel.Warning, "Reading from Set/Clr/Tgl is not supported.");
                    return registers.Read(temp);
            }
        }

        public override void WriteDoubleWord(long address, uint value)
        {
            long temp = address & 0x0FFF;
            switch(address & 0x3000){
                case 0x0000:
                    registers.Write(address, value);
                    break;
                case 0x1000:
                    registers.Write(temp, registers.Read(temp) | value);
                    break;
                case 0x2000:
                    registers.Write(temp, registers.Read(temp) & ~value);
                    break;
                case 0x3000:
                    registers.Write(temp, registers.Read(temp) ^ value);
                    break;
                default:
                    this.Log(LogLevel.Error, "writing doubleWord to non existing offset {0:X}, case : {1:X}", address, address & 0x3000);
                    break;
            }           
        }

        protected enum Registers
        {
            Main0_Revision = 0x100,
            Main0_Embmsize = 0x104,
            Main0_Stackmsize = 0x108,
            Main0_Swcapa = 0x10C,
            Main0_Hfrcocaldefault = 0x110,
            Main0_Hfrcocalspeed = 0x114,
            Main0_Eui64l = 0x118,
            Main0_Eui64h = 0x11C,
            Main0_Part = 0x120,
            Spare0_Register_group = 0x124,
            Spare1_Register_group = 0x128,
            Spare2_Register_group = 0x12C,
            Spare3_Register_group = 0x130,
            Spare4_Register_group = 0x134,
            Spare5_Register_group = 0x138,
            Spare6_Register_group = 0x13C,
            Main1_Hfxocal = 0x1F4,
            Main1_Moduleinfo = 0x1F8,
            Main1_Legacy = 0x1FC,
            
            Main0_Revision_SET = 0x1100,
            Main0_Embmsize_SET = 0x1104,
            Main0_Stackmsize_SET = 0x1108,
            Main0_Swcapa_SET = 0x110C,
            Main0_Hfrcocaldefault_SET = 0x1110,
            Main0_Hfrcocalspeed_SET = 0x1114,
            Main0_Eui64l_SET = 0x1118,
            Main0_Eui64h_SET = 0x111C,
            Main0_Part_SET = 0x1120,
            Spare0_Register_group_SET = 0x1124,
            Spare1_Register_group_SET = 0x1128,
            Spare2_Register_group_SET = 0x112C,
            Spare3_Register_group_SET = 0x1130,
            Spare4_Register_group_SET = 0x1134,
            Spare5_Register_group_SET = 0x1138,
            Spare6_Register_group_SET = 0x113C,
            Main1_Hfxocal_SET = 0x11F4,
            Main1_Moduleinfo_SET = 0x11F8,
            Main1_Legacy_SET = 0x11FC,
            
            Main0_Revision_CLR = 0x2100,
            Main0_Embmsize_CLR = 0x2104,
            Main0_Stackmsize_CLR = 0x2108,
            Main0_Swcapa_CLR = 0x210C,
            Main0_Hfrcocaldefault_CLR = 0x2110,
            Main0_Hfrcocalspeed_CLR = 0x2114,
            Main0_Eui64l_CLR = 0x2118,
            Main0_Eui64h_CLR = 0x211C,
            Main0_Part_CLR = 0x2120,
            Spare0_Register_group_CLR = 0x2124,
            Spare1_Register_group_CLR = 0x2128,
            Spare2_Register_group_CLR = 0x212C,
            Spare3_Register_group_CLR = 0x2130,
            Spare4_Register_group_CLR = 0x2134,
            Spare5_Register_group_CLR = 0x2138,
            Spare6_Register_group_CLR = 0x213C,
            Main1_Hfxocal_CLR = 0x21F4,
            Main1_Moduleinfo_CLR = 0x21F8,
            Main1_Legacy_CLR = 0x21FC,
            
            Main0_Revision_TGL = 0x3100,
            Main0_Embmsize_TGL = 0x3104,
            Main0_Stackmsize_TGL = 0x3108,
            Main0_Swcapa_TGL = 0x310C,
            Main0_Hfrcocaldefault_TGL = 0x3110,
            Main0_Hfrcocalspeed_TGL = 0x3114,
            Main0_Eui64l_TGL = 0x3118,
            Main0_Eui64h_TGL = 0x311C,
            Main0_Part_TGL = 0x3120,
            Spare0_Register_group_TGL = 0x3124,
            Spare1_Register_group_TGL = 0x3128,
            Spare2_Register_group_TGL = 0x312C,
            Spare3_Register_group_TGL = 0x3130,
            Spare4_Register_group_TGL = 0x3134,
            Spare5_Register_group_TGL = 0x3138,
            Spare6_Register_group_TGL = 0x313C,
            Main1_Hfxocal_TGL = 0x31F4,
            Main1_Moduleinfo_TGL = 0x31F8,
            Main1_Legacy_TGL = 0x31FC,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}