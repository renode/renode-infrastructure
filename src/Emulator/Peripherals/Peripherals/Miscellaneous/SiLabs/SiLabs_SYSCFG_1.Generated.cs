//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    SYSCFG, Generated on : 2025-07-24 21:39:48.825768
    SYSCFG, ID Version : 8502eff413b04f7b9fdc7a6f39981e53.1 */

// Note: The constructor has been removed from the auto-generated code.
// Please implement your own constructor in a separate partial class file (.impl).

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
    public partial class SiLabs_SYSCFG_1 : BasicDoubleWordPeripheral, IKnownSize
    {

        public SiLabs_SYSCFG_1(Machine machine) : base(machine)
        {
            Define_Registers();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Chiprevhw, GenerateChiprevhwRegister()},
                {(long)Registers.Chiprev, GenerateChiprevRegister()},
                {(long)Registers.Instanceid, GenerateInstanceidRegister()},
                {(long)Registers.Cfgstcalib, GenerateCfgstcalibRegister()},
                {(long)Registers.Cfgsystic, GenerateCfgsysticRegister()},
                {(long)Registers.Rom_Sesysromrm, GenerateRom_sesysromrmRegister()},
                {(long)Registers.Rom_Sepkeromrm, GenerateRom_sepkeromrmRegister()},
                {(long)Registers.Rom_Sesysctrl, GenerateRom_sesysctrlRegister()},
                {(long)Registers.Rom_Sepkectrl, GenerateRom_sepkectrlRegister()},
                {(long)Registers.Ram_Ctrl, GenerateRam_ctrlRegister()},
                {(long)Registers.Ram_Dmem0retnctrl, GenerateRam_dmem0retnctrlRegister()},
                {(long)Registers.Ram_Dmem0feature, GenerateRam_dmem0featureRegister()},
                {(long)Registers.Ram_Dmem0eccaddr, GenerateRam_dmem0eccaddrRegister()},
                {(long)Registers.Ram_Dmem0eccctrl, GenerateRam_dmem0eccctrlRegister()},
                {(long)Registers.Ram_Ramrm, GenerateRam_ramrmRegister()},
                {(long)Registers.Ram_Ramwm, GenerateRam_ramwmRegister()},
                {(long)Registers.Ram_Ramra, GenerateRam_ramraRegister()},
                {(long)Registers.Ram_Rambiasconf, GenerateRam_rambiasconfRegister()},
                {(long)Registers.Ram_Ramlvtest, GenerateRam_ramlvtestRegister()},
                {(long)Registers.Ram_Radioramretnctrl, GenerateRam_radioramretnctrlRegister()},
                {(long)Registers.Ram_Radioramfeature, GenerateRam_radioramfeatureRegister()},
                {(long)Registers.Ram_Radioeccctrl, GenerateRam_radioeccctrlRegister()},
                {(long)Registers.Ram_Seqrameccaddr, GenerateRam_seqrameccaddrRegister()},
                {(long)Registers.Ram_Frcrameccaddr, GenerateRam_frcrameccaddrRegister()},
                {(long)Registers.RootData0, GenerateRootdata0Register()},
                {(long)Registers.RootData1, GenerateRootdata1Register()},
                {(long)Registers.RootLockstatus, GenerateRootlockstatusRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            registers.Reset();
            SYSCFG_Reset();
        }
        
        protected enum CHIPREV_FAMILY
        {
            PG22 = 24, // Product is in PG22 family
            MG22 = 52, // Product is in MG22 family
            BG22 = 53, // Product is in BG22 family
            FG22 = 55, // Product is in FG22 family
        }
        
        protected enum CFGSTCALIB_NOREF
        {
            REF = 0, // Reference clock is implemented
            NOREF = 1, // Reference clock is not implemented
        }
        
        protected enum RAM_DMEM0RETNCTRL_RAMRETNCTRL
        {
            ALLON = 0, // None of the RAM blocks powered down
            BLK0 = 1, // Power down RAM block 0
            BLK1 = 2, // Power down RAM block 1
        }
        
        protected enum RAM_DMEM0FEATURE_RAMSIZE
        {
            BLK0 = 1, // Enable RAM block0 
            BLK1 = 2, // Enable RAM block1 
            BLK0TO1 = 3, // Enable all RAM blocks (block 0 and 1)
        }
        
        protected enum RAM_RAMBIASCONF_RAMBIASCTRL
        {
            No = 0, // None 
            VSB100 = 1, // Voltage Source Bias 100mV
            VSB200 = 2, // Voltage Source Bias 200mV
            VSB300 = 4, // Voltage Source Bias 300mV
            VSB400 = 8, // Voltage Source Bias 400mV
        }
        
        protected enum RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL
        {
            ALLON = 0, // SEQRAM not powered down
            BLK0 = 1, // Power down SEQRAM block 0
            BLK1 = 2, // Power down SEQRAM block 1
            ALLOFF = 3, // Power down all SEQRAM blocks
        }
        
        protected enum RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL
        {
            ALLON = 0, // FRCRAM not powered down
            ALLOFF = 1, // Power down FRCRAM
        }
        
        protected enum RAM_RADIORAMFEATURE_SEQRAMEN
        {
            NONE = 0, // Disable all sequencer ram blocks
            BLK0 = 1, // Enable sequencer ram block 0
            BLK1 = 2, // Enable sequencer ram block 1
            ALL = 3, // Enable all sequencer ram blocks
        }
        
        protected enum RAM_RADIORAMFEATURE_FRCRAMEN
        {
            NONE = 0, // Disable all FRC ram banks
            ALL = 1, // Enable all FRC ram banks
        }
        
        // If - Offset : 0x0
        protected DoubleWordRegister GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_sw0_bit, 
                    valueProviderCallback: (_) => {
                        If_Sw0_ValueProvider(_);
                        return if_sw0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Sw0_Write(_, __),
                    
                    readCallback: (_, __) => If_Sw0_Read(_, __),
                    name: "Sw0")
            .WithFlag(1, out if_sw1_bit, 
                    valueProviderCallback: (_) => {
                        If_Sw1_ValueProvider(_);
                        return if_sw1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Sw1_Write(_, __),
                    
                    readCallback: (_, __) => If_Sw1_Read(_, __),
                    name: "Sw1")
            .WithFlag(2, out if_sw2_bit, 
                    valueProviderCallback: (_) => {
                        If_Sw2_ValueProvider(_);
                        return if_sw2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Sw2_Write(_, __),
                    
                    readCallback: (_, __) => If_Sw2_Read(_, __),
                    name: "Sw2")
            .WithFlag(3, out if_sw3_bit, 
                    valueProviderCallback: (_) => {
                        If_Sw3_ValueProvider(_);
                        return if_sw3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Sw3_Write(_, __),
                    
                    readCallback: (_, __) => If_Sw3_Read(_, __),
                    name: "Sw3")
            .WithReservedBits(4, 12)
            .WithFlag(16, out if_ramerr1b_bit, 
                    valueProviderCallback: (_) => {
                        If_Ramerr1b_ValueProvider(_);
                        return if_ramerr1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Ramerr1b_Write(_, __),
                    
                    readCallback: (_, __) => If_Ramerr1b_Read(_, __),
                    name: "Ramerr1b")
            .WithFlag(17, out if_ramerr2b_bit, 
                    valueProviderCallback: (_) => {
                        If_Ramerr2b_ValueProvider(_);
                        return if_ramerr2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Ramerr2b_Write(_, __),
                    
                    readCallback: (_, __) => If_Ramerr2b_Read(_, __),
                    name: "Ramerr2b")
            .WithReservedBits(18, 6)
            .WithFlag(24, out if_seqramerr1b_bit, 
                    valueProviderCallback: (_) => {
                        If_Seqramerr1b_ValueProvider(_);
                        return if_seqramerr1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Seqramerr1b_Write(_, __),
                    
                    readCallback: (_, __) => If_Seqramerr1b_Read(_, __),
                    name: "Seqramerr1b")
            .WithFlag(25, out if_seqramerr2b_bit, 
                    valueProviderCallback: (_) => {
                        If_Seqramerr2b_ValueProvider(_);
                        return if_seqramerr2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Seqramerr2b_Write(_, __),
                    
                    readCallback: (_, __) => If_Seqramerr2b_Read(_, __),
                    name: "Seqramerr2b")
            .WithReservedBits(26, 2)
            .WithFlag(28, out if_frcramerr1b_bit, 
                    valueProviderCallback: (_) => {
                        If_Frcramerr1b_ValueProvider(_);
                        return if_frcramerr1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Frcramerr1b_Write(_, __),
                    
                    readCallback: (_, __) => If_Frcramerr1b_Read(_, __),
                    name: "Frcramerr1b")
            .WithFlag(29, out if_frcramerr2b_bit, 
                    valueProviderCallback: (_) => {
                        If_Frcramerr2b_ValueProvider(_);
                        return if_frcramerr2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Frcramerr2b_Write(_, __),
                    
                    readCallback: (_, __) => If_Frcramerr2b_Read(_, __),
                    name: "Frcramerr2b")
            .WithReservedBits(30, 2)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x4
        protected DoubleWordRegister GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_sw0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Sw0_ValueProvider(_);
                        return ien_sw0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Sw0_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Sw0_Read(_, __),
                    name: "Sw0")
            .WithFlag(1, out ien_sw1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Sw1_ValueProvider(_);
                        return ien_sw1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Sw1_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Sw1_Read(_, __),
                    name: "Sw1")
            .WithFlag(2, out ien_sw2_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Sw2_ValueProvider(_);
                        return ien_sw2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Sw2_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Sw2_Read(_, __),
                    name: "Sw2")
            .WithFlag(3, out ien_sw3_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Sw3_ValueProvider(_);
                        return ien_sw3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Sw3_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Sw3_Read(_, __),
                    name: "Sw3")
            .WithReservedBits(4, 12)
            .WithFlag(16, out ien_ramerr1b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Ramerr1b_ValueProvider(_);
                        return ien_ramerr1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Ramerr1b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Ramerr1b_Read(_, __),
                    name: "Ramerr1b")
            .WithFlag(17, out ien_ramerr2b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Ramerr2b_ValueProvider(_);
                        return ien_ramerr2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Ramerr2b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Ramerr2b_Read(_, __),
                    name: "Ramerr2b")
            .WithReservedBits(18, 6)
            .WithFlag(24, out ien_seqramerr1b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Seqramerr1b_ValueProvider(_);
                        return ien_seqramerr1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Seqramerr1b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Seqramerr1b_Read(_, __),
                    name: "Seqramerr1b")
            .WithFlag(25, out ien_seqramerr2b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Seqramerr2b_ValueProvider(_);
                        return ien_seqramerr2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Seqramerr2b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Seqramerr2b_Read(_, __),
                    name: "Seqramerr2b")
            .WithReservedBits(26, 2)
            .WithFlag(28, out ien_frcramerr1b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Frcramerr1b_ValueProvider(_);
                        return ien_frcramerr1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Frcramerr1b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Frcramerr1b_Read(_, __),
                    name: "Frcramerr1b")
            .WithFlag(29, out ien_frcramerr2b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Frcramerr2b_ValueProvider(_);
                        return ien_frcramerr2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Frcramerr2b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Frcramerr2b_Read(_, __),
                    name: "Frcramerr2b")
            .WithReservedBits(30, 2)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Chiprevhw - Offset : 0x10
        protected DoubleWordRegister GenerateChiprevhwRegister() => new DoubleWordRegister(this, 0xD01)
            
            .WithValueField(0, 6, out chiprevhw_major_field, 
                    valueProviderCallback: (_) => {
                        Chiprevhw_Major_ValueProvider(_);
                        return chiprevhw_major_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprevhw_Major_Write(_, __),
                    
                    readCallback: (_, __) => Chiprevhw_Major_Read(_, __),
                    name: "Major")
            
            .WithValueField(6, 6, out chiprevhw_family_field, 
                    valueProviderCallback: (_) => {
                        Chiprevhw_Family_ValueProvider(_);
                        return chiprevhw_family_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprevhw_Family_Write(_, __),
                    
                    readCallback: (_, __) => Chiprevhw_Family_Read(_, __),
                    name: "Family")
            
            .WithValueField(12, 8, out chiprevhw_minor_field, 
                    valueProviderCallback: (_) => {
                        Chiprevhw_Minor_ValueProvider(_);
                        return chiprevhw_minor_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprevhw_Minor_Write(_, __),
                    
                    readCallback: (_, __) => Chiprevhw_Minor_Read(_, __),
                    name: "Minor")
            .WithReservedBits(20, 4)
            
            .WithValueField(24, 8, out chiprevhw_varient_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Chiprevhw_Varient_ValueProvider(_);
                        return chiprevhw_varient_field.Value;
                    },
                    
                    readCallback: (_, __) => Chiprevhw_Varient_Read(_, __),
                    name: "Varient")
            .WithReadCallback((_, __) => Chiprevhw_Read(_, __))
            .WithWriteCallback((_, __) => Chiprevhw_Write(_, __));
        
        // Chiprev - Offset : 0x14
        protected DoubleWordRegister GenerateChiprevRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 6, out chiprev_major_field, 
                    valueProviderCallback: (_) => {
                        Chiprev_Major_ValueProvider(_);
                        return chiprev_major_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprev_Major_Write(_, __),
                    
                    readCallback: (_, __) => Chiprev_Major_Read(_, __),
                    name: "Major")
            .WithEnumField<DoubleWordRegister, CHIPREV_FAMILY>(6, 6, out chiprev_family_field, 
                    valueProviderCallback: (_) => {
                        Chiprev_Family_ValueProvider(_);
                        return chiprev_family_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprev_Family_Write(_, __),
                    
                    readCallback: (_, __) => Chiprev_Family_Read(_, __),
                    name: "Family")
            
            .WithValueField(12, 8, out chiprev_minor_field, 
                    valueProviderCallback: (_) => {
                        Chiprev_Minor_ValueProvider(_);
                        return chiprev_minor_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprev_Minor_Write(_, __),
                    
                    readCallback: (_, __) => Chiprev_Minor_Read(_, __),
                    name: "Minor")
            .WithReservedBits(20, 12)
            .WithReadCallback((_, __) => Chiprev_Read(_, __))
            .WithWriteCallback((_, __) => Chiprev_Write(_, __));
        
        // Instanceid - Offset : 0x18
        protected DoubleWordRegister GenerateInstanceidRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out instanceid_instanceid_field, 
                    valueProviderCallback: (_) => {
                        Instanceid_Instanceid_ValueProvider(_);
                        return instanceid_instanceid_field.Value;
                    },
                    
                    writeCallback: (_, __) => Instanceid_Instanceid_Write(_, __),
                    
                    readCallback: (_, __) => Instanceid_Instanceid_Read(_, __),
                    name: "Instanceid")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Instanceid_Read(_, __))
            .WithWriteCallback((_, __) => Instanceid_Write(_, __));
        
        // Cfgstcalib - Offset : 0x1C
        protected DoubleWordRegister GenerateCfgstcalibRegister() => new DoubleWordRegister(this, 0x1004A37)
            
            .WithValueField(0, 24, out cfgstcalib_tenms_field, 
                    valueProviderCallback: (_) => {
                        Cfgstcalib_Tenms_ValueProvider(_);
                        return cfgstcalib_tenms_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgstcalib_Tenms_Write(_, __),
                    
                    readCallback: (_, __) => Cfgstcalib_Tenms_Read(_, __),
                    name: "Tenms")
            .WithFlag(24, out cfgstcalib_skew_bit, 
                    valueProviderCallback: (_) => {
                        Cfgstcalib_Skew_ValueProvider(_);
                        return cfgstcalib_skew_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgstcalib_Skew_Write(_, __),
                    
                    readCallback: (_, __) => Cfgstcalib_Skew_Read(_, __),
                    name: "Skew")
            .WithEnumField<DoubleWordRegister, CFGSTCALIB_NOREF>(25, 1, out cfgstcalib_noref_bit, 
                    valueProviderCallback: (_) => {
                        Cfgstcalib_Noref_ValueProvider(_);
                        return cfgstcalib_noref_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgstcalib_Noref_Write(_, __),
                    
                    readCallback: (_, __) => Cfgstcalib_Noref_Read(_, __),
                    name: "Noref")
            .WithReservedBits(26, 6)
            .WithReadCallback((_, __) => Cfgstcalib_Read(_, __))
            .WithWriteCallback((_, __) => Cfgstcalib_Write(_, __));
        
        // Cfgsystic - Offset : 0x20
        protected DoubleWordRegister GenerateCfgsysticRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgsystic_systicextclken_bit, 
                    valueProviderCallback: (_) => {
                        Cfgsystic_Systicextclken_ValueProvider(_);
                        return cfgsystic_systicextclken_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgsystic_Systicextclken_Write(_, __),
                    
                    readCallback: (_, __) => Cfgsystic_Systicextclken_Read(_, __),
                    name: "Systicextclken")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Cfgsystic_Read(_, __))
            .WithWriteCallback((_, __) => Cfgsystic_Write(_, __));
        
        // Rom_Sesysromrm - Offset : 0x100
        protected DoubleWordRegister GenerateRom_sesysromrmRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out rom_sesysromrm_sesysromrm_field, 
                    valueProviderCallback: (_) => {
                        Rom_Sesysromrm_Sesysromrm_ValueProvider(_);
                        return rom_sesysromrm_sesysromrm_field.Value;
                    },
                    
                    writeCallback: (_, __) => Rom_Sesysromrm_Sesysromrm_Write(_, __),
                    
                    readCallback: (_, __) => Rom_Sesysromrm_Sesysromrm_Read(_, __),
                    name: "Sesysromrm")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Rom_Sesysromrm_Read(_, __))
            .WithWriteCallback((_, __) => Rom_Sesysromrm_Write(_, __));
        
        // Rom_Sepkeromrm - Offset : 0x104
        protected DoubleWordRegister GenerateRom_sepkeromrmRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out rom_sepkeromrm_sepkeromrm_field, 
                    valueProviderCallback: (_) => {
                        Rom_Sepkeromrm_Sepkeromrm_ValueProvider(_);
                        return rom_sepkeromrm_sepkeromrm_field.Value;
                    },
                    
                    writeCallback: (_, __) => Rom_Sepkeromrm_Sepkeromrm_Write(_, __),
                    
                    readCallback: (_, __) => Rom_Sepkeromrm_Sepkeromrm_Read(_, __),
                    name: "Sepkeromrm")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Rom_Sepkeromrm_Read(_, __))
            .WithWriteCallback((_, __) => Rom_Sepkeromrm_Write(_, __));
        
        // Rom_Sesysctrl - Offset : 0x108
        protected DoubleWordRegister GenerateRom_sesysctrlRegister() => new DoubleWordRegister(this, 0x100)
            .WithFlag(0, out rom_sesysctrl_sesysromtest1_bit, 
                    valueProviderCallback: (_) => {
                        Rom_Sesysctrl_Sesysromtest1_ValueProvider(_);
                        return rom_sesysctrl_sesysromtest1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Rom_Sesysctrl_Sesysromtest1_Write(_, __),
                    
                    readCallback: (_, __) => Rom_Sesysctrl_Sesysromtest1_Read(_, __),
                    name: "Sesysromtest1")
            .WithReservedBits(1, 7)
            .WithFlag(8, out rom_sesysctrl_sesysromrme_bit, 
                    valueProviderCallback: (_) => {
                        Rom_Sesysctrl_Sesysromrme_ValueProvider(_);
                        return rom_sesysctrl_sesysromrme_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Rom_Sesysctrl_Sesysromrme_Write(_, __),
                    
                    readCallback: (_, __) => Rom_Sesysctrl_Sesysromrme_Read(_, __),
                    name: "Sesysromrme")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Rom_Sesysctrl_Read(_, __))
            .WithWriteCallback((_, __) => Rom_Sesysctrl_Write(_, __));
        
        // Rom_Sepkectrl - Offset : 0x10C
        protected DoubleWordRegister GenerateRom_sepkectrlRegister() => new DoubleWordRegister(this, 0x100)
            .WithFlag(0, out rom_sepkectrl_sepkeromtest1_bit, 
                    valueProviderCallback: (_) => {
                        Rom_Sepkectrl_Sepkeromtest1_ValueProvider(_);
                        return rom_sepkectrl_sepkeromtest1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Rom_Sepkectrl_Sepkeromtest1_Write(_, __),
                    
                    readCallback: (_, __) => Rom_Sepkectrl_Sepkeromtest1_Read(_, __),
                    name: "Sepkeromtest1")
            .WithReservedBits(1, 7)
            .WithFlag(8, out rom_sepkectrl_sepkeromrme_bit, 
                    valueProviderCallback: (_) => {
                        Rom_Sepkectrl_Sepkeromrme_ValueProvider(_);
                        return rom_sepkectrl_sepkeromrme_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Rom_Sepkectrl_Sepkeromrme_Write(_, __),
                    
                    readCallback: (_, __) => Rom_Sepkectrl_Sepkeromrme_Read(_, __),
                    name: "Sepkeromrme")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Rom_Sepkectrl_Read(_, __))
            .WithWriteCallback((_, __) => Rom_Sepkectrl_Write(_, __));
        
        // Ram_Ctrl - Offset : 0x200
        protected DoubleWordRegister GenerateRam_ctrlRegister() => new DoubleWordRegister(this, 0x21)
            .WithFlag(0, out ram_ctrl_addrfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ctrl_Addrfaulten_ValueProvider(_);
                        return ram_ctrl_addrfaulten_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ctrl_Addrfaulten_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ctrl_Addrfaulten_Read(_, __),
                    name: "Addrfaulten")
            .WithReservedBits(1, 4)
            .WithFlag(5, out ram_ctrl_rameccerrfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ctrl_Rameccerrfaulten_ValueProvider(_);
                        return ram_ctrl_rameccerrfaulten_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ctrl_Rameccerrfaulten_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ctrl_Rameccerrfaulten_Read(_, __),
                    name: "Rameccerrfaulten")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Ram_Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Ctrl_Write(_, __));
        
        // Ram_Dmem0retnctrl - Offset : 0x208
        protected DoubleWordRegister GenerateRam_dmem0retnctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, RAM_DMEM0RETNCTRL_RAMRETNCTRL>(0, 2, out ram_dmem0retnctrl_ramretnctrl_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0retnctrl_Ramretnctrl_ValueProvider(_);
                        return ram_dmem0retnctrl_ramretnctrl_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0retnctrl_Ramretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0retnctrl_Ramretnctrl_Read(_, __),
                    name: "Ramretnctrl")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Ram_Dmem0retnctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Dmem0retnctrl_Write(_, __));
        
        // Ram_Dmem0feature - Offset : 0x20C
        protected DoubleWordRegister GenerateRam_dmem0featureRegister() => new DoubleWordRegister(this, 0x3)
            .WithEnumField<DoubleWordRegister, RAM_DMEM0FEATURE_RAMSIZE>(0, 2, out ram_dmem0feature_ramsize_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0feature_Ramsize_ValueProvider(_);
                        return ram_dmem0feature_ramsize_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0feature_Ramsize_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0feature_Ramsize_Read(_, __),
                    name: "Ramsize")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Ram_Dmem0feature_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Dmem0feature_Write(_, __));
        
        // Ram_Dmem0eccaddr - Offset : 0x210
        protected DoubleWordRegister GenerateRam_dmem0eccaddrRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out ram_dmem0eccaddr_dmem0eccaddr_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ram_Dmem0eccaddr_Dmem0eccaddr_ValueProvider(_);
                        return ram_dmem0eccaddr_dmem0eccaddr_field.Value;
                    },
                    
                    readCallback: (_, __) => Ram_Dmem0eccaddr_Dmem0eccaddr_Read(_, __),
                    name: "Dmem0eccaddr")
            .WithReadCallback((_, __) => Ram_Dmem0eccaddr_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Dmem0eccaddr_Write(_, __));
        
        // Ram_Dmem0eccctrl - Offset : 0x214
        protected DoubleWordRegister GenerateRam_dmem0eccctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ram_dmem0eccctrl_rameccen_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0eccctrl_Rameccen_ValueProvider(_);
                        return ram_dmem0eccctrl_rameccen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0eccctrl_Rameccen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0eccctrl_Rameccen_Read(_, __),
                    name: "Rameccen")
            .WithFlag(1, out ram_dmem0eccctrl_rameccewen_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0eccctrl_Rameccewen_ValueProvider(_);
                        return ram_dmem0eccctrl_rameccewen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0eccctrl_Rameccewen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0eccctrl_Rameccewen_Read(_, __),
                    name: "Rameccewen")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Ram_Dmem0eccctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Dmem0eccctrl_Write(_, __));
        
        // Ram_Ramrm - Offset : 0x300
        protected DoubleWordRegister GenerateRam_ramrmRegister() => new DoubleWordRegister(this, 0x70301)
            
            .WithValueField(0, 3, out ram_ramrm_ramrm0_field, 
                    valueProviderCallback: (_) => {
                        Ram_Ramrm_Ramrm0_ValueProvider(_);
                        return ram_ramrm_ramrm0_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramrm_Ramrm0_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramrm_Ramrm0_Read(_, __),
                    name: "Ramrm0")
            .WithReservedBits(3, 5)
            
            .WithValueField(8, 3, out ram_ramrm_ramrm1_field, 
                    valueProviderCallback: (_) => {
                        Ram_Ramrm_Ramrm1_ValueProvider(_);
                        return ram_ramrm_ramrm1_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramrm_Ramrm1_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramrm_Ramrm1_Read(_, __),
                    name: "Ramrm1")
            .WithReservedBits(11, 5)
            
            .WithValueField(16, 3, out ram_ramrm_ramrm2_field, 
                    valueProviderCallback: (_) => {
                        Ram_Ramrm_Ramrm2_ValueProvider(_);
                        return ram_ramrm_ramrm2_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramrm_Ramrm2_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramrm_Ramrm2_Read(_, __),
                    name: "Ramrm2")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Ram_Ramrm_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Ramrm_Write(_, __));
        
        // Ram_Ramwm - Offset : 0x304
        protected DoubleWordRegister GenerateRam_ramwmRegister() => new DoubleWordRegister(this, 0x10307)
            
            .WithValueField(0, 3, out ram_ramwm_ramwm0_field, 
                    valueProviderCallback: (_) => {
                        Ram_Ramwm_Ramwm0_ValueProvider(_);
                        return ram_ramwm_ramwm0_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramwm_Ramwm0_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramwm_Ramwm0_Read(_, __),
                    name: "Ramwm0")
            .WithReservedBits(3, 5)
            
            .WithValueField(8, 3, out ram_ramwm_ramwm1_field, 
                    valueProviderCallback: (_) => {
                        Ram_Ramwm_Ramwm1_ValueProvider(_);
                        return ram_ramwm_ramwm1_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramwm_Ramwm1_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramwm_Ramwm1_Read(_, __),
                    name: "Ramwm1")
            .WithReservedBits(11, 5)
            
            .WithValueField(16, 3, out ram_ramwm_ramwm2_field, 
                    valueProviderCallback: (_) => {
                        Ram_Ramwm_Ramwm2_ValueProvider(_);
                        return ram_ramwm_ramwm2_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramwm_Ramwm2_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramwm_Ramwm2_Read(_, __),
                    name: "Ramwm2")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Ram_Ramwm_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Ramwm_Write(_, __));
        
        // Ram_Ramra - Offset : 0x308
        protected DoubleWordRegister GenerateRam_ramraRegister() => new DoubleWordRegister(this, 0x1)
            .WithFlag(0, out ram_ramra_ramra0_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ramra_Ramra0_ValueProvider(_);
                        return ram_ramra_ramra0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramra_Ramra0_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramra_Ramra0_Read(_, __),
                    name: "Ramra0")
            .WithReservedBits(1, 7)
            .WithFlag(8, out ram_ramra_ramra1_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ramra_Ramra1_ValueProvider(_);
                        return ram_ramra_ramra1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramra_Ramra1_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramra_Ramra1_Read(_, __),
                    name: "Ramra1")
            .WithReservedBits(9, 7)
            .WithFlag(16, out ram_ramra_ramra2_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ramra_Ramra2_ValueProvider(_);
                        return ram_ramra_ramra2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramra_Ramra2_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramra_Ramra2_Read(_, __),
                    name: "Ramra2")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Ram_Ramra_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Ramra_Write(_, __));
        
        // Ram_Rambiasconf - Offset : 0x30C
        protected DoubleWordRegister GenerateRam_rambiasconfRegister() => new DoubleWordRegister(this, 0x2)
            .WithEnumField<DoubleWordRegister, RAM_RAMBIASCONF_RAMBIASCTRL>(0, 4, out ram_rambiasconf_rambiasctrl_field, 
                    valueProviderCallback: (_) => {
                        Ram_Rambiasconf_Rambiasctrl_ValueProvider(_);
                        return ram_rambiasconf_rambiasctrl_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Rambiasconf_Rambiasctrl_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Rambiasconf_Rambiasctrl_Read(_, __),
                    name: "Rambiasctrl")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Ram_Rambiasconf_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Rambiasconf_Write(_, __));
        
        // Ram_Ramlvtest - Offset : 0x310
        protected DoubleWordRegister GenerateRam_ramlvtestRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ram_ramlvtest_ramlvtest_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ramlvtest_Ramlvtest_ValueProvider(_);
                        return ram_ramlvtest_ramlvtest_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ramlvtest_Ramlvtest_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ramlvtest_Ramlvtest_Read(_, __),
                    name: "Ramlvtest")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Ram_Ramlvtest_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Ramlvtest_Write(_, __));
        
        // Ram_Radioramretnctrl - Offset : 0x400
        protected DoubleWordRegister GenerateRam_radioramretnctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL>(0, 2, out ram_radioramretnctrl_seqramretnctrl_field, 
                    valueProviderCallback: (_) => {
                        Ram_Radioramretnctrl_Seqramretnctrl_ValueProvider(_);
                        return ram_radioramretnctrl_seqramretnctrl_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioramretnctrl_Seqramretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioramretnctrl_Seqramretnctrl_Read(_, __),
                    name: "Seqramretnctrl")
            .WithReservedBits(2, 6)
            .WithEnumField<DoubleWordRegister, RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL>(8, 1, out ram_radioramretnctrl_frcramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Radioramretnctrl_Frcramretnctrl_ValueProvider(_);
                        return ram_radioramretnctrl_frcramretnctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioramretnctrl_Frcramretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioramretnctrl_Frcramretnctrl_Read(_, __),
                    name: "Frcramretnctrl")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Ram_Radioramretnctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Radioramretnctrl_Write(_, __));
        
        // Ram_Radioramfeature - Offset : 0x404
        protected DoubleWordRegister GenerateRam_radioramfeatureRegister() => new DoubleWordRegister(this, 0x103)
            .WithEnumField<DoubleWordRegister, RAM_RADIORAMFEATURE_SEQRAMEN>(0, 2, out ram_radioramfeature_seqramen_field, 
                    valueProviderCallback: (_) => {
                        Ram_Radioramfeature_Seqramen_ValueProvider(_);
                        return ram_radioramfeature_seqramen_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioramfeature_Seqramen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioramfeature_Seqramen_Read(_, __),
                    name: "Seqramen")
            .WithReservedBits(2, 6)
            .WithEnumField<DoubleWordRegister, RAM_RADIORAMFEATURE_FRCRAMEN>(8, 1, out ram_radioramfeature_frcramen_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Radioramfeature_Frcramen_ValueProvider(_);
                        return ram_radioramfeature_frcramen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioramfeature_Frcramen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioramfeature_Frcramen_Read(_, __),
                    name: "Frcramen")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Ram_Radioramfeature_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Radioramfeature_Write(_, __));
        
        // Ram_Radioeccctrl - Offset : 0x408
        protected DoubleWordRegister GenerateRam_radioeccctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ram_radioeccctrl_seqrameccen_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Radioeccctrl_Seqrameccen_ValueProvider(_);
                        return ram_radioeccctrl_seqrameccen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioeccctrl_Seqrameccen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioeccctrl_Seqrameccen_Read(_, __),
                    name: "Seqrameccen")
            .WithFlag(1, out ram_radioeccctrl_seqrameccewen_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Radioeccctrl_Seqrameccewen_ValueProvider(_);
                        return ram_radioeccctrl_seqrameccewen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioeccctrl_Seqrameccewen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioeccctrl_Seqrameccewen_Read(_, __),
                    name: "Seqrameccewen")
            .WithReservedBits(2, 6)
            .WithFlag(8, out ram_radioeccctrl_frcrameccen_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Radioeccctrl_Frcrameccen_ValueProvider(_);
                        return ram_radioeccctrl_frcrameccen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioeccctrl_Frcrameccen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioeccctrl_Frcrameccen_Read(_, __),
                    name: "Frcrameccen")
            .WithFlag(9, out ram_radioeccctrl_frcrameccewen_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Radioeccctrl_Frcrameccewen_ValueProvider(_);
                        return ram_radioeccctrl_frcrameccewen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Radioeccctrl_Frcrameccewen_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Radioeccctrl_Frcrameccewen_Read(_, __),
                    name: "Frcrameccewen")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Ram_Radioeccctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Radioeccctrl_Write(_, __));
        
        // Ram_Seqrameccaddr - Offset : 0x410
        protected DoubleWordRegister GenerateRam_seqrameccaddrRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out ram_seqrameccaddr_seqrameccaddr_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ram_Seqrameccaddr_Seqrameccaddr_ValueProvider(_);
                        return ram_seqrameccaddr_seqrameccaddr_field.Value;
                    },
                    
                    readCallback: (_, __) => Ram_Seqrameccaddr_Seqrameccaddr_Read(_, __),
                    name: "Seqrameccaddr")
            .WithReadCallback((_, __) => Ram_Seqrameccaddr_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Seqrameccaddr_Write(_, __));
        
        // Ram_Frcrameccaddr - Offset : 0x414
        protected DoubleWordRegister GenerateRam_frcrameccaddrRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out ram_frcrameccaddr_frcrameccaddr_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ram_Frcrameccaddr_Frcrameccaddr_ValueProvider(_);
                        return ram_frcrameccaddr_frcrameccaddr_field.Value;
                    },
                    
                    readCallback: (_, __) => Ram_Frcrameccaddr_Frcrameccaddr_Read(_, __),
                    name: "Frcrameccaddr")
            .WithReadCallback((_, __) => Ram_Frcrameccaddr_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Frcrameccaddr_Write(_, __));
        
        // RootData0 - Offset : 0x600
        protected DoubleWordRegister GenerateRootdata0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out rootdata0_data_field, 
                    valueProviderCallback: (_) => {
                        RootData0_Data_ValueProvider(_);
                        return rootdata0_data_field.Value;
                    },
                    
                    writeCallback: (_, __) => RootData0_Data_Write(_, __),
                    
                    readCallback: (_, __) => RootData0_Data_Read(_, __),
                    name: "Data")
            .WithReadCallback((_, __) => RootData0_Read(_, __))
            .WithWriteCallback((_, __) => RootData0_Write(_, __));
        
        // RootData1 - Offset : 0x604
        protected DoubleWordRegister GenerateRootdata1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out rootdata1_data_field, 
                    valueProviderCallback: (_) => {
                        RootData1_Data_ValueProvider(_);
                        return rootdata1_data_field.Value;
                    },
                    
                    writeCallback: (_, __) => RootData1_Data_Write(_, __),
                    
                    readCallback: (_, __) => RootData1_Data_Read(_, __),
                    name: "Data")
            .WithReadCallback((_, __) => RootData1_Read(_, __))
            .WithWriteCallback((_, __) => RootData1_Write(_, __));
        
        // RootLockstatus - Offset : 0x608
        protected DoubleWordRegister GenerateRootlockstatusRegister() => new DoubleWordRegister(this, 0x11F0107)
            .WithFlag(0, out rootlockstatus_buslock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Buslock_ValueProvider(_);
                        return rootlockstatus_buslock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Buslock_Read(_, __),
                    name: "Buslock")
            .WithFlag(1, out rootlockstatus_reglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Reglock_ValueProvider(_);
                        return rootlockstatus_reglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Reglock_Read(_, __),
                    name: "Reglock")
            .WithFlag(2, out rootlockstatus_mfrlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Mfrlock_ValueProvider(_);
                        return rootlockstatus_mfrlock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Mfrlock_Read(_, __),
                    name: "Mfrlock")
            .WithReservedBits(3, 1)
            .WithFlag(4, out rootlockstatus_rootmodelock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Rootmodelock_ValueProvider(_);
                        return rootlockstatus_rootmodelock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Rootmodelock_Read(_, __),
                    name: "Rootmodelock")
            .WithReservedBits(5, 3)
            .WithFlag(8, out rootlockstatus_rootdbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Rootdbglock_ValueProvider(_);
                        return rootlockstatus_rootdbglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Rootdbglock_Read(_, __),
                    name: "Rootdbglock")
            .WithReservedBits(9, 7)
            .WithFlag(16, out rootlockstatus_userdbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userdbglock_ValueProvider(_);
                        return rootlockstatus_userdbglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userdbglock_Read(_, __),
                    name: "Userdbglock")
            .WithFlag(17, out rootlockstatus_usernidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Usernidlock_ValueProvider(_);
                        return rootlockstatus_usernidlock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Usernidlock_Read(_, __),
                    name: "Usernidlock")
            .WithFlag(18, out rootlockstatus_userspidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userspidlock_ValueProvider(_);
                        return rootlockstatus_userspidlock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userspidlock_Read(_, __),
                    name: "Userspidlock")
            .WithFlag(19, out rootlockstatus_userspnidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userspnidlock_ValueProvider(_);
                        return rootlockstatus_userspnidlock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userspnidlock_Read(_, __),
                    name: "Userspnidlock")
            .WithFlag(20, out rootlockstatus_userdbgaplock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userdbgaplock_ValueProvider(_);
                        return rootlockstatus_userdbgaplock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userdbgaplock_Read(_, __),
                    name: "Userdbgaplock")
            .WithReservedBits(21, 3)
            .WithFlag(24, out rootlockstatus_radiodbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Radiodbglock_ValueProvider(_);
                        return rootlockstatus_radiodbglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Radiodbglock_Read(_, __),
                    name: "Radiodbglock")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => RootLockstatus_Read(_, __))
            .WithWriteCallback((_, __) => RootLockstatus_Write(_, __));
        

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

        



        
        // If - Offset : 0x0
    
        protected IFlagRegisterField if_sw0_bit;
        partial void If_Sw0_Write(bool a, bool b);
        partial void If_Sw0_Read(bool a, bool b);
        partial void If_Sw0_ValueProvider(bool a);
    
        protected IFlagRegisterField if_sw1_bit;
        partial void If_Sw1_Write(bool a, bool b);
        partial void If_Sw1_Read(bool a, bool b);
        partial void If_Sw1_ValueProvider(bool a);
    
        protected IFlagRegisterField if_sw2_bit;
        partial void If_Sw2_Write(bool a, bool b);
        partial void If_Sw2_Read(bool a, bool b);
        partial void If_Sw2_ValueProvider(bool a);
    
        protected IFlagRegisterField if_sw3_bit;
        partial void If_Sw3_Write(bool a, bool b);
        partial void If_Sw3_Read(bool a, bool b);
        partial void If_Sw3_ValueProvider(bool a);
    
        protected IFlagRegisterField if_ramerr1b_bit;
        partial void If_Ramerr1b_Write(bool a, bool b);
        partial void If_Ramerr1b_Read(bool a, bool b);
        partial void If_Ramerr1b_ValueProvider(bool a);
    
        protected IFlagRegisterField if_ramerr2b_bit;
        partial void If_Ramerr2b_Write(bool a, bool b);
        partial void If_Ramerr2b_Read(bool a, bool b);
        partial void If_Ramerr2b_ValueProvider(bool a);
    
        protected IFlagRegisterField if_seqramerr1b_bit;
        partial void If_Seqramerr1b_Write(bool a, bool b);
        partial void If_Seqramerr1b_Read(bool a, bool b);
        partial void If_Seqramerr1b_ValueProvider(bool a);
    
        protected IFlagRegisterField if_seqramerr2b_bit;
        partial void If_Seqramerr2b_Write(bool a, bool b);
        partial void If_Seqramerr2b_Read(bool a, bool b);
        partial void If_Seqramerr2b_ValueProvider(bool a);
    
        protected IFlagRegisterField if_frcramerr1b_bit;
        partial void If_Frcramerr1b_Write(bool a, bool b);
        partial void If_Frcramerr1b_Read(bool a, bool b);
        partial void If_Frcramerr1b_ValueProvider(bool a);
    
        protected IFlagRegisterField if_frcramerr2b_bit;
        partial void If_Frcramerr2b_Write(bool a, bool b);
        partial void If_Frcramerr2b_Read(bool a, bool b);
        partial void If_Frcramerr2b_ValueProvider(bool a);
        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        
        // Ien - Offset : 0x4
    
        protected IFlagRegisterField ien_sw0_bit;
        partial void Ien_Sw0_Write(bool a, bool b);
        partial void Ien_Sw0_Read(bool a, bool b);
        partial void Ien_Sw0_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_sw1_bit;
        partial void Ien_Sw1_Write(bool a, bool b);
        partial void Ien_Sw1_Read(bool a, bool b);
        partial void Ien_Sw1_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_sw2_bit;
        partial void Ien_Sw2_Write(bool a, bool b);
        partial void Ien_Sw2_Read(bool a, bool b);
        partial void Ien_Sw2_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_sw3_bit;
        partial void Ien_Sw3_Write(bool a, bool b);
        partial void Ien_Sw3_Read(bool a, bool b);
        partial void Ien_Sw3_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_ramerr1b_bit;
        partial void Ien_Ramerr1b_Write(bool a, bool b);
        partial void Ien_Ramerr1b_Read(bool a, bool b);
        partial void Ien_Ramerr1b_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_ramerr2b_bit;
        partial void Ien_Ramerr2b_Write(bool a, bool b);
        partial void Ien_Ramerr2b_Read(bool a, bool b);
        partial void Ien_Ramerr2b_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_seqramerr1b_bit;
        partial void Ien_Seqramerr1b_Write(bool a, bool b);
        partial void Ien_Seqramerr1b_Read(bool a, bool b);
        partial void Ien_Seqramerr1b_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_seqramerr2b_bit;
        partial void Ien_Seqramerr2b_Write(bool a, bool b);
        partial void Ien_Seqramerr2b_Read(bool a, bool b);
        partial void Ien_Seqramerr2b_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_frcramerr1b_bit;
        partial void Ien_Frcramerr1b_Write(bool a, bool b);
        partial void Ien_Frcramerr1b_Read(bool a, bool b);
        partial void Ien_Frcramerr1b_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_frcramerr2b_bit;
        partial void Ien_Frcramerr2b_Write(bool a, bool b);
        partial void Ien_Frcramerr2b_Read(bool a, bool b);
        partial void Ien_Frcramerr2b_ValueProvider(bool a);
        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        
        // Chiprevhw - Offset : 0x10
    
        protected IValueRegisterField chiprevhw_major_field;
        partial void Chiprevhw_Major_Write(ulong a, ulong b);
        partial void Chiprevhw_Major_Read(ulong a, ulong b);
        partial void Chiprevhw_Major_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprevhw_family_field;
        partial void Chiprevhw_Family_Write(ulong a, ulong b);
        partial void Chiprevhw_Family_Read(ulong a, ulong b);
        partial void Chiprevhw_Family_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprevhw_minor_field;
        partial void Chiprevhw_Minor_Write(ulong a, ulong b);
        partial void Chiprevhw_Minor_Read(ulong a, ulong b);
        partial void Chiprevhw_Minor_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprevhw_varient_field;
        partial void Chiprevhw_Varient_Read(ulong a, ulong b);
        partial void Chiprevhw_Varient_ValueProvider(ulong a);
        partial void Chiprevhw_Write(uint a, uint b);
        partial void Chiprevhw_Read(uint a, uint b);
        
        
        // Chiprev - Offset : 0x14
    
        protected IValueRegisterField chiprev_major_field;
        partial void Chiprev_Major_Write(ulong a, ulong b);
        partial void Chiprev_Major_Read(ulong a, ulong b);
        partial void Chiprev_Major_ValueProvider(ulong a);
    
        protected IEnumRegisterField<CHIPREV_FAMILY> chiprev_family_field;
        partial void Chiprev_Family_Write(CHIPREV_FAMILY a, CHIPREV_FAMILY b);
        partial void Chiprev_Family_Read(CHIPREV_FAMILY a, CHIPREV_FAMILY b);
        partial void Chiprev_Family_ValueProvider(CHIPREV_FAMILY a);
    
        protected IValueRegisterField chiprev_minor_field;
        partial void Chiprev_Minor_Write(ulong a, ulong b);
        partial void Chiprev_Minor_Read(ulong a, ulong b);
        partial void Chiprev_Minor_ValueProvider(ulong a);
        partial void Chiprev_Write(uint a, uint b);
        partial void Chiprev_Read(uint a, uint b);
        
        
        // Instanceid - Offset : 0x18
    
        protected IValueRegisterField instanceid_instanceid_field;
        partial void Instanceid_Instanceid_Write(ulong a, ulong b);
        partial void Instanceid_Instanceid_Read(ulong a, ulong b);
        partial void Instanceid_Instanceid_ValueProvider(ulong a);
        partial void Instanceid_Write(uint a, uint b);
        partial void Instanceid_Read(uint a, uint b);
        
        
        // Cfgstcalib - Offset : 0x1C
    
        protected IValueRegisterField cfgstcalib_tenms_field;
        partial void Cfgstcalib_Tenms_Write(ulong a, ulong b);
        partial void Cfgstcalib_Tenms_Read(ulong a, ulong b);
        partial void Cfgstcalib_Tenms_ValueProvider(ulong a);
    
        protected IFlagRegisterField cfgstcalib_skew_bit;
        partial void Cfgstcalib_Skew_Write(bool a, bool b);
        partial void Cfgstcalib_Skew_Read(bool a, bool b);
        partial void Cfgstcalib_Skew_ValueProvider(bool a);
    
        protected IEnumRegisterField<CFGSTCALIB_NOREF> cfgstcalib_noref_bit;
        partial void Cfgstcalib_Noref_Write(CFGSTCALIB_NOREF a, CFGSTCALIB_NOREF b);
        partial void Cfgstcalib_Noref_Read(CFGSTCALIB_NOREF a, CFGSTCALIB_NOREF b);
        partial void Cfgstcalib_Noref_ValueProvider(CFGSTCALIB_NOREF a);
        partial void Cfgstcalib_Write(uint a, uint b);
        partial void Cfgstcalib_Read(uint a, uint b);
        
        
        // Cfgsystic - Offset : 0x20
    
        protected IFlagRegisterField cfgsystic_systicextclken_bit;
        partial void Cfgsystic_Systicextclken_Write(bool a, bool b);
        partial void Cfgsystic_Systicextclken_Read(bool a, bool b);
        partial void Cfgsystic_Systicextclken_ValueProvider(bool a);
        partial void Cfgsystic_Write(uint a, uint b);
        partial void Cfgsystic_Read(uint a, uint b);
        
        
        // Rom_Sesysromrm - Offset : 0x100
    
        protected IValueRegisterField rom_sesysromrm_sesysromrm_field;
        partial void Rom_Sesysromrm_Sesysromrm_Write(ulong a, ulong b);
        partial void Rom_Sesysromrm_Sesysromrm_Read(ulong a, ulong b);
        partial void Rom_Sesysromrm_Sesysromrm_ValueProvider(ulong a);
        partial void Rom_Sesysromrm_Write(uint a, uint b);
        partial void Rom_Sesysromrm_Read(uint a, uint b);
        
        
        // Rom_Sepkeromrm - Offset : 0x104
    
        protected IValueRegisterField rom_sepkeromrm_sepkeromrm_field;
        partial void Rom_Sepkeromrm_Sepkeromrm_Write(ulong a, ulong b);
        partial void Rom_Sepkeromrm_Sepkeromrm_Read(ulong a, ulong b);
        partial void Rom_Sepkeromrm_Sepkeromrm_ValueProvider(ulong a);
        partial void Rom_Sepkeromrm_Write(uint a, uint b);
        partial void Rom_Sepkeromrm_Read(uint a, uint b);
        
        
        // Rom_Sesysctrl - Offset : 0x108
    
        protected IFlagRegisterField rom_sesysctrl_sesysromtest1_bit;
        partial void Rom_Sesysctrl_Sesysromtest1_Write(bool a, bool b);
        partial void Rom_Sesysctrl_Sesysromtest1_Read(bool a, bool b);
        partial void Rom_Sesysctrl_Sesysromtest1_ValueProvider(bool a);
    
        protected IFlagRegisterField rom_sesysctrl_sesysromrme_bit;
        partial void Rom_Sesysctrl_Sesysromrme_Write(bool a, bool b);
        partial void Rom_Sesysctrl_Sesysromrme_Read(bool a, bool b);
        partial void Rom_Sesysctrl_Sesysromrme_ValueProvider(bool a);
        partial void Rom_Sesysctrl_Write(uint a, uint b);
        partial void Rom_Sesysctrl_Read(uint a, uint b);
        
        
        // Rom_Sepkectrl - Offset : 0x10C
    
        protected IFlagRegisterField rom_sepkectrl_sepkeromtest1_bit;
        partial void Rom_Sepkectrl_Sepkeromtest1_Write(bool a, bool b);
        partial void Rom_Sepkectrl_Sepkeromtest1_Read(bool a, bool b);
        partial void Rom_Sepkectrl_Sepkeromtest1_ValueProvider(bool a);
    
        protected IFlagRegisterField rom_sepkectrl_sepkeromrme_bit;
        partial void Rom_Sepkectrl_Sepkeromrme_Write(bool a, bool b);
        partial void Rom_Sepkectrl_Sepkeromrme_Read(bool a, bool b);
        partial void Rom_Sepkectrl_Sepkeromrme_ValueProvider(bool a);
        partial void Rom_Sepkectrl_Write(uint a, uint b);
        partial void Rom_Sepkectrl_Read(uint a, uint b);
        
        
        // Ram_Ctrl - Offset : 0x200
    
        protected IFlagRegisterField ram_ctrl_addrfaulten_bit;
        partial void Ram_Ctrl_Addrfaulten_Write(bool a, bool b);
        partial void Ram_Ctrl_Addrfaulten_Read(bool a, bool b);
        partial void Ram_Ctrl_Addrfaulten_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_ctrl_rameccerrfaulten_bit;
        partial void Ram_Ctrl_Rameccerrfaulten_Write(bool a, bool b);
        partial void Ram_Ctrl_Rameccerrfaulten_Read(bool a, bool b);
        partial void Ram_Ctrl_Rameccerrfaulten_ValueProvider(bool a);
        partial void Ram_Ctrl_Write(uint a, uint b);
        partial void Ram_Ctrl_Read(uint a, uint b);
        
        
        // Ram_Dmem0retnctrl - Offset : 0x208
    
        protected IEnumRegisterField<RAM_DMEM0RETNCTRL_RAMRETNCTRL> ram_dmem0retnctrl_ramretnctrl_field;
        partial void Ram_Dmem0retnctrl_Ramretnctrl_Write(RAM_DMEM0RETNCTRL_RAMRETNCTRL a, RAM_DMEM0RETNCTRL_RAMRETNCTRL b);
        partial void Ram_Dmem0retnctrl_Ramretnctrl_Read(RAM_DMEM0RETNCTRL_RAMRETNCTRL a, RAM_DMEM0RETNCTRL_RAMRETNCTRL b);
        partial void Ram_Dmem0retnctrl_Ramretnctrl_ValueProvider(RAM_DMEM0RETNCTRL_RAMRETNCTRL a);
        partial void Ram_Dmem0retnctrl_Write(uint a, uint b);
        partial void Ram_Dmem0retnctrl_Read(uint a, uint b);
        
        
        // Ram_Dmem0feature - Offset : 0x20C
    
        protected IEnumRegisterField<RAM_DMEM0FEATURE_RAMSIZE> ram_dmem0feature_ramsize_field;
        partial void Ram_Dmem0feature_Ramsize_Write(RAM_DMEM0FEATURE_RAMSIZE a, RAM_DMEM0FEATURE_RAMSIZE b);
        partial void Ram_Dmem0feature_Ramsize_Read(RAM_DMEM0FEATURE_RAMSIZE a, RAM_DMEM0FEATURE_RAMSIZE b);
        partial void Ram_Dmem0feature_Ramsize_ValueProvider(RAM_DMEM0FEATURE_RAMSIZE a);
        partial void Ram_Dmem0feature_Write(uint a, uint b);
        partial void Ram_Dmem0feature_Read(uint a, uint b);
        
        
        // Ram_Dmem0eccaddr - Offset : 0x210
    
        protected IValueRegisterField ram_dmem0eccaddr_dmem0eccaddr_field;
        partial void Ram_Dmem0eccaddr_Dmem0eccaddr_Read(ulong a, ulong b);
        partial void Ram_Dmem0eccaddr_Dmem0eccaddr_ValueProvider(ulong a);
        partial void Ram_Dmem0eccaddr_Write(uint a, uint b);
        partial void Ram_Dmem0eccaddr_Read(uint a, uint b);
        
        
        // Ram_Dmem0eccctrl - Offset : 0x214
    
        protected IFlagRegisterField ram_dmem0eccctrl_rameccen_bit;
        partial void Ram_Dmem0eccctrl_Rameccen_Write(bool a, bool b);
        partial void Ram_Dmem0eccctrl_Rameccen_Read(bool a, bool b);
        partial void Ram_Dmem0eccctrl_Rameccen_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_dmem0eccctrl_rameccewen_bit;
        partial void Ram_Dmem0eccctrl_Rameccewen_Write(bool a, bool b);
        partial void Ram_Dmem0eccctrl_Rameccewen_Read(bool a, bool b);
        partial void Ram_Dmem0eccctrl_Rameccewen_ValueProvider(bool a);
        partial void Ram_Dmem0eccctrl_Write(uint a, uint b);
        partial void Ram_Dmem0eccctrl_Read(uint a, uint b);
        
        
        // Ram_Ramrm - Offset : 0x300
    
        protected IValueRegisterField ram_ramrm_ramrm0_field;
        partial void Ram_Ramrm_Ramrm0_Write(ulong a, ulong b);
        partial void Ram_Ramrm_Ramrm0_Read(ulong a, ulong b);
        partial void Ram_Ramrm_Ramrm0_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_ramrm_ramrm1_field;
        partial void Ram_Ramrm_Ramrm1_Write(ulong a, ulong b);
        partial void Ram_Ramrm_Ramrm1_Read(ulong a, ulong b);
        partial void Ram_Ramrm_Ramrm1_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_ramrm_ramrm2_field;
        partial void Ram_Ramrm_Ramrm2_Write(ulong a, ulong b);
        partial void Ram_Ramrm_Ramrm2_Read(ulong a, ulong b);
        partial void Ram_Ramrm_Ramrm2_ValueProvider(ulong a);
        partial void Ram_Ramrm_Write(uint a, uint b);
        partial void Ram_Ramrm_Read(uint a, uint b);
        
        
        // Ram_Ramwm - Offset : 0x304
    
        protected IValueRegisterField ram_ramwm_ramwm0_field;
        partial void Ram_Ramwm_Ramwm0_Write(ulong a, ulong b);
        partial void Ram_Ramwm_Ramwm0_Read(ulong a, ulong b);
        partial void Ram_Ramwm_Ramwm0_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_ramwm_ramwm1_field;
        partial void Ram_Ramwm_Ramwm1_Write(ulong a, ulong b);
        partial void Ram_Ramwm_Ramwm1_Read(ulong a, ulong b);
        partial void Ram_Ramwm_Ramwm1_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_ramwm_ramwm2_field;
        partial void Ram_Ramwm_Ramwm2_Write(ulong a, ulong b);
        partial void Ram_Ramwm_Ramwm2_Read(ulong a, ulong b);
        partial void Ram_Ramwm_Ramwm2_ValueProvider(ulong a);
        partial void Ram_Ramwm_Write(uint a, uint b);
        partial void Ram_Ramwm_Read(uint a, uint b);
        
        
        // Ram_Ramra - Offset : 0x308
    
        protected IFlagRegisterField ram_ramra_ramra0_bit;
        partial void Ram_Ramra_Ramra0_Write(bool a, bool b);
        partial void Ram_Ramra_Ramra0_Read(bool a, bool b);
        partial void Ram_Ramra_Ramra0_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_ramra_ramra1_bit;
        partial void Ram_Ramra_Ramra1_Write(bool a, bool b);
        partial void Ram_Ramra_Ramra1_Read(bool a, bool b);
        partial void Ram_Ramra_Ramra1_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_ramra_ramra2_bit;
        partial void Ram_Ramra_Ramra2_Write(bool a, bool b);
        partial void Ram_Ramra_Ramra2_Read(bool a, bool b);
        partial void Ram_Ramra_Ramra2_ValueProvider(bool a);
        partial void Ram_Ramra_Write(uint a, uint b);
        partial void Ram_Ramra_Read(uint a, uint b);
        
        
        // Ram_Rambiasconf - Offset : 0x30C
    
        protected IEnumRegisterField<RAM_RAMBIASCONF_RAMBIASCTRL> ram_rambiasconf_rambiasctrl_field;
        partial void Ram_Rambiasconf_Rambiasctrl_Write(RAM_RAMBIASCONF_RAMBIASCTRL a, RAM_RAMBIASCONF_RAMBIASCTRL b);
        partial void Ram_Rambiasconf_Rambiasctrl_Read(RAM_RAMBIASCONF_RAMBIASCTRL a, RAM_RAMBIASCONF_RAMBIASCTRL b);
        partial void Ram_Rambiasconf_Rambiasctrl_ValueProvider(RAM_RAMBIASCONF_RAMBIASCTRL a);
        partial void Ram_Rambiasconf_Write(uint a, uint b);
        partial void Ram_Rambiasconf_Read(uint a, uint b);
        
        
        // Ram_Ramlvtest - Offset : 0x310
    
        protected IFlagRegisterField ram_ramlvtest_ramlvtest_bit;
        partial void Ram_Ramlvtest_Ramlvtest_Write(bool a, bool b);
        partial void Ram_Ramlvtest_Ramlvtest_Read(bool a, bool b);
        partial void Ram_Ramlvtest_Ramlvtest_ValueProvider(bool a);
        partial void Ram_Ramlvtest_Write(uint a, uint b);
        partial void Ram_Ramlvtest_Read(uint a, uint b);
        
        
        // Ram_Radioramretnctrl - Offset : 0x400
    
        protected IEnumRegisterField<RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL> ram_radioramretnctrl_seqramretnctrl_field;
        partial void Ram_Radioramretnctrl_Seqramretnctrl_Write(RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL a, RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL b);
        partial void Ram_Radioramretnctrl_Seqramretnctrl_Read(RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL a, RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL b);
        partial void Ram_Radioramretnctrl_Seqramretnctrl_ValueProvider(RAM_RADIORAMRETNCTRL_SEQRAMRETNCTRL a);
    
        protected IEnumRegisterField<RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL> ram_radioramretnctrl_frcramretnctrl_bit;
        partial void Ram_Radioramretnctrl_Frcramretnctrl_Write(RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL a, RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL b);
        partial void Ram_Radioramretnctrl_Frcramretnctrl_Read(RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL a, RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL b);
        partial void Ram_Radioramretnctrl_Frcramretnctrl_ValueProvider(RAM_RADIORAMRETNCTRL_FRCRAMRETNCTRL a);
        partial void Ram_Radioramretnctrl_Write(uint a, uint b);
        partial void Ram_Radioramretnctrl_Read(uint a, uint b);
        
        
        // Ram_Radioramfeature - Offset : 0x404
    
        protected IEnumRegisterField<RAM_RADIORAMFEATURE_SEQRAMEN> ram_radioramfeature_seqramen_field;
        partial void Ram_Radioramfeature_Seqramen_Write(RAM_RADIORAMFEATURE_SEQRAMEN a, RAM_RADIORAMFEATURE_SEQRAMEN b);
        partial void Ram_Radioramfeature_Seqramen_Read(RAM_RADIORAMFEATURE_SEQRAMEN a, RAM_RADIORAMFEATURE_SEQRAMEN b);
        partial void Ram_Radioramfeature_Seqramen_ValueProvider(RAM_RADIORAMFEATURE_SEQRAMEN a);
    
        protected IEnumRegisterField<RAM_RADIORAMFEATURE_FRCRAMEN> ram_radioramfeature_frcramen_bit;
        partial void Ram_Radioramfeature_Frcramen_Write(RAM_RADIORAMFEATURE_FRCRAMEN a, RAM_RADIORAMFEATURE_FRCRAMEN b);
        partial void Ram_Radioramfeature_Frcramen_Read(RAM_RADIORAMFEATURE_FRCRAMEN a, RAM_RADIORAMFEATURE_FRCRAMEN b);
        partial void Ram_Radioramfeature_Frcramen_ValueProvider(RAM_RADIORAMFEATURE_FRCRAMEN a);
        partial void Ram_Radioramfeature_Write(uint a, uint b);
        partial void Ram_Radioramfeature_Read(uint a, uint b);
        
        
        // Ram_Radioeccctrl - Offset : 0x408
    
        protected IFlagRegisterField ram_radioeccctrl_seqrameccen_bit;
        partial void Ram_Radioeccctrl_Seqrameccen_Write(bool a, bool b);
        partial void Ram_Radioeccctrl_Seqrameccen_Read(bool a, bool b);
        partial void Ram_Radioeccctrl_Seqrameccen_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_radioeccctrl_seqrameccewen_bit;
        partial void Ram_Radioeccctrl_Seqrameccewen_Write(bool a, bool b);
        partial void Ram_Radioeccctrl_Seqrameccewen_Read(bool a, bool b);
        partial void Ram_Radioeccctrl_Seqrameccewen_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_radioeccctrl_frcrameccen_bit;
        partial void Ram_Radioeccctrl_Frcrameccen_Write(bool a, bool b);
        partial void Ram_Radioeccctrl_Frcrameccen_Read(bool a, bool b);
        partial void Ram_Radioeccctrl_Frcrameccen_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_radioeccctrl_frcrameccewen_bit;
        partial void Ram_Radioeccctrl_Frcrameccewen_Write(bool a, bool b);
        partial void Ram_Radioeccctrl_Frcrameccewen_Read(bool a, bool b);
        partial void Ram_Radioeccctrl_Frcrameccewen_ValueProvider(bool a);
        partial void Ram_Radioeccctrl_Write(uint a, uint b);
        partial void Ram_Radioeccctrl_Read(uint a, uint b);
        
        
        // Ram_Seqrameccaddr - Offset : 0x410
    
        protected IValueRegisterField ram_seqrameccaddr_seqrameccaddr_field;
        partial void Ram_Seqrameccaddr_Seqrameccaddr_Read(ulong a, ulong b);
        partial void Ram_Seqrameccaddr_Seqrameccaddr_ValueProvider(ulong a);
        partial void Ram_Seqrameccaddr_Write(uint a, uint b);
        partial void Ram_Seqrameccaddr_Read(uint a, uint b);
        
        
        // Ram_Frcrameccaddr - Offset : 0x414
    
        protected IValueRegisterField ram_frcrameccaddr_frcrameccaddr_field;
        partial void Ram_Frcrameccaddr_Frcrameccaddr_Read(ulong a, ulong b);
        partial void Ram_Frcrameccaddr_Frcrameccaddr_ValueProvider(ulong a);
        partial void Ram_Frcrameccaddr_Write(uint a, uint b);
        partial void Ram_Frcrameccaddr_Read(uint a, uint b);
        
        
        // RootData0 - Offset : 0x600
    
        protected IValueRegisterField rootdata0_data_field;
        partial void RootData0_Data_Write(ulong a, ulong b);
        partial void RootData0_Data_Read(ulong a, ulong b);
        partial void RootData0_Data_ValueProvider(ulong a);
        partial void RootData0_Write(uint a, uint b);
        partial void RootData0_Read(uint a, uint b);
        
        
        // RootData1 - Offset : 0x604
    
        protected IValueRegisterField rootdata1_data_field;
        partial void RootData1_Data_Write(ulong a, ulong b);
        partial void RootData1_Data_Read(ulong a, ulong b);
        partial void RootData1_Data_ValueProvider(ulong a);
        partial void RootData1_Write(uint a, uint b);
        partial void RootData1_Read(uint a, uint b);
        
        
        // RootLockstatus - Offset : 0x608
    
        protected IFlagRegisterField rootlockstatus_buslock_bit;
        partial void RootLockstatus_Buslock_Read(bool a, bool b);
        partial void RootLockstatus_Buslock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_reglock_bit;
        partial void RootLockstatus_Reglock_Read(bool a, bool b);
        partial void RootLockstatus_Reglock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_mfrlock_bit;
        partial void RootLockstatus_Mfrlock_Read(bool a, bool b);
        partial void RootLockstatus_Mfrlock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_rootmodelock_bit;
        partial void RootLockstatus_Rootmodelock_Read(bool a, bool b);
        partial void RootLockstatus_Rootmodelock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_rootdbglock_bit;
        partial void RootLockstatus_Rootdbglock_Read(bool a, bool b);
        partial void RootLockstatus_Rootdbglock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_userdbglock_bit;
        partial void RootLockstatus_Userdbglock_Read(bool a, bool b);
        partial void RootLockstatus_Userdbglock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_usernidlock_bit;
        partial void RootLockstatus_Usernidlock_Read(bool a, bool b);
        partial void RootLockstatus_Usernidlock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_userspidlock_bit;
        partial void RootLockstatus_Userspidlock_Read(bool a, bool b);
        partial void RootLockstatus_Userspidlock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_userspnidlock_bit;
        partial void RootLockstatus_Userspnidlock_Read(bool a, bool b);
        partial void RootLockstatus_Userspnidlock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_userdbgaplock_bit;
        partial void RootLockstatus_Userdbgaplock_Read(bool a, bool b);
        partial void RootLockstatus_Userdbgaplock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_radiodbglock_bit;
        partial void RootLockstatus_Radiodbglock_Read(bool a, bool b);
        partial void RootLockstatus_Radiodbglock_ValueProvider(bool a);
        partial void RootLockstatus_Write(uint a, uint b);
        partial void RootLockstatus_Read(uint a, uint b);
        
        partial void SYSCFG_Reset();

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

        public override uint ReadDoubleWord(long address)
        {
            long temp = address & 0x0FFF;
            switch(address & 0x3000)
            {
                case 0x0000:
                    return registers.Read(temp);
                default:
                    this.Log(LogLevel.Warning, "Reading from Set/Clr/Tgl is not supported.");
                    return registers.Read(temp);
            }
        }

        public override void WriteDoubleWord(long address, uint value)
        {
            long temp = address & 0x0FFF;
            switch(address & 0x3000)
            {
                case 0x0000:
                    registers.Write(temp, value);
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
            If = 0x0,
            Ien = 0x4,
            Chiprevhw = 0x10,
            Chiprev = 0x14,
            Instanceid = 0x18,
            Cfgstcalib = 0x1C,
            Cfgsystic = 0x20,
            Rom_Sesysromrm = 0x100,
            Rom_Sepkeromrm = 0x104,
            Rom_Sesysctrl = 0x108,
            Rom_Sepkectrl = 0x10C,
            Ram_Ctrl = 0x200,
            Ram_Dmem0retnctrl = 0x208,
            Ram_Dmem0feature = 0x20C,
            Ram_Dmem0eccaddr = 0x210,
            Ram_Dmem0eccctrl = 0x214,
            Ram_Ramrm = 0x300,
            Ram_Ramwm = 0x304,
            Ram_Ramra = 0x308,
            Ram_Rambiasconf = 0x30C,
            Ram_Ramlvtest = 0x310,
            Ram_Radioramretnctrl = 0x400,
            Ram_Radioramfeature = 0x404,
            Ram_Radioeccctrl = 0x408,
            Ram_Seqrameccaddr = 0x410,
            Ram_Frcrameccaddr = 0x414,
            RootData0 = 0x600,
            RootData1 = 0x604,
            RootLockstatus = 0x608,
            
            If_SET = 0x1000,
            Ien_SET = 0x1004,
            Chiprevhw_SET = 0x1010,
            Chiprev_SET = 0x1014,
            Instanceid_SET = 0x1018,
            Cfgstcalib_SET = 0x101C,
            Cfgsystic_SET = 0x1020,
            Rom_Sesysromrm_SET = 0x1100,
            Rom_Sepkeromrm_SET = 0x1104,
            Rom_Sesysctrl_SET = 0x1108,
            Rom_Sepkectrl_SET = 0x110C,
            Ram_Ctrl_SET = 0x1200,
            Ram_Dmem0retnctrl_SET = 0x1208,
            Ram_Dmem0feature_SET = 0x120C,
            Ram_Dmem0eccaddr_SET = 0x1210,
            Ram_Dmem0eccctrl_SET = 0x1214,
            Ram_Ramrm_SET = 0x1300,
            Ram_Ramwm_SET = 0x1304,
            Ram_Ramra_SET = 0x1308,
            Ram_Rambiasconf_SET = 0x130C,
            Ram_Ramlvtest_SET = 0x1310,
            Ram_Radioramretnctrl_SET = 0x1400,
            Ram_Radioramfeature_SET = 0x1404,
            Ram_Radioeccctrl_SET = 0x1408,
            Ram_Seqrameccaddr_SET = 0x1410,
            Ram_Frcrameccaddr_SET = 0x1414,
            RootData0_SET = 0x1600,
            RootData1_SET = 0x1604,
            RootLockstatus_SET = 0x1608,
            
            If_CLR = 0x2000,
            Ien_CLR = 0x2004,
            Chiprevhw_CLR = 0x2010,
            Chiprev_CLR = 0x2014,
            Instanceid_CLR = 0x2018,
            Cfgstcalib_CLR = 0x201C,
            Cfgsystic_CLR = 0x2020,
            Rom_Sesysromrm_CLR = 0x2100,
            Rom_Sepkeromrm_CLR = 0x2104,
            Rom_Sesysctrl_CLR = 0x2108,
            Rom_Sepkectrl_CLR = 0x210C,
            Ram_Ctrl_CLR = 0x2200,
            Ram_Dmem0retnctrl_CLR = 0x2208,
            Ram_Dmem0feature_CLR = 0x220C,
            Ram_Dmem0eccaddr_CLR = 0x2210,
            Ram_Dmem0eccctrl_CLR = 0x2214,
            Ram_Ramrm_CLR = 0x2300,
            Ram_Ramwm_CLR = 0x2304,
            Ram_Ramra_CLR = 0x2308,
            Ram_Rambiasconf_CLR = 0x230C,
            Ram_Ramlvtest_CLR = 0x2310,
            Ram_Radioramretnctrl_CLR = 0x2400,
            Ram_Radioramfeature_CLR = 0x2404,
            Ram_Radioeccctrl_CLR = 0x2408,
            Ram_Seqrameccaddr_CLR = 0x2410,
            Ram_Frcrameccaddr_CLR = 0x2414,
            RootData0_CLR = 0x2600,
            RootData1_CLR = 0x2604,
            RootLockstatus_CLR = 0x2608,
            
            If_TGL = 0x3000,
            Ien_TGL = 0x3004,
            Chiprevhw_TGL = 0x3010,
            Chiprev_TGL = 0x3014,
            Instanceid_TGL = 0x3018,
            Cfgstcalib_TGL = 0x301C,
            Cfgsystic_TGL = 0x3020,
            Rom_Sesysromrm_TGL = 0x3100,
            Rom_Sepkeromrm_TGL = 0x3104,
            Rom_Sesysctrl_TGL = 0x3108,
            Rom_Sepkectrl_TGL = 0x310C,
            Ram_Ctrl_TGL = 0x3200,
            Ram_Dmem0retnctrl_TGL = 0x3208,
            Ram_Dmem0feature_TGL = 0x320C,
            Ram_Dmem0eccaddr_TGL = 0x3210,
            Ram_Dmem0eccctrl_TGL = 0x3214,
            Ram_Ramrm_TGL = 0x3300,
            Ram_Ramwm_TGL = 0x3304,
            Ram_Ramra_TGL = 0x3308,
            Ram_Rambiasconf_TGL = 0x330C,
            Ram_Ramlvtest_TGL = 0x3310,
            Ram_Radioramretnctrl_TGL = 0x3400,
            Ram_Radioramfeature_TGL = 0x3404,
            Ram_Radioeccctrl_TGL = 0x3408,
            Ram_Seqrameccaddr_TGL = 0x3410,
            Ram_Frcrameccaddr_TGL = 0x3414,
            RootData0_TGL = 0x3600,
            RootData1_TGL = 0x3604,
            RootLockstatus_TGL = 0x3608,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}