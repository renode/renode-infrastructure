//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    SYSCFG, Generated on : 2025-10-29 13:56:22.575728
    SYSCFG, ID Version : 8502eff413b04f7b9fdc7a6f39981e53.9 */

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
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_SYSCFG_9 : BasicDoubleWordPeripheral, IKnownSize
    {

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Chiprevhw, GenerateChiprevhwRegister()},
                {(long)Registers.Chiprev, GenerateChiprevRegister()},
                {(long)Registers.Instanceid, GenerateInstanceidRegister()},
                {(long)Registers.Cfgstcalib, GenerateCfgstcalibRegister()},
                {(long)Registers.Cfgsystic, GenerateCfgsysticRegister()},
                {(long)Registers.Fpgarevhw, GenerateFpgarevhwRegister()},
                {(long)Registers.Fpgaipothw, GenerateFpgaipothwRegister()},
                {(long)Registers.Cfgahbintercnct, GenerateCfgahbintercnctRegister()},
                {(long)Registers.Rom_Sesysromrm, GenerateRom_sesysromrmRegister()},
                {(long)Registers.Rom_Sepkeromrm, GenerateRom_sepkeromrmRegister()},
                {(long)Registers.Rom_Sesysctrl, GenerateRom_sesysctrlRegister()},
                {(long)Registers.Rom_Sepkectrl, GenerateRom_sepkectrlRegister()},
                {(long)Registers.Ram_Ctrl, GenerateRam_ctrlRegister()},
                {(long)Registers.Ram_Dmem0retnctrl, GenerateRam_dmem0retnctrlRegister()},
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
                {(long)Registers.Ram_Icacheramretnctrl, GenerateRam_icacheramretnctrlRegister()},
                {(long)Registers.Ram_Dmem0portmapsel, GenerateRam_dmem0portmapselRegister()},
                {(long)Registers.RootData0, GenerateRootdata0Register()},
                {(long)Registers.RootData1, GenerateRootdata1Register()},
                {(long)Registers.RootLockstatus, GenerateRootlockstatusRegister()},
                {(long)Registers.RootSeswversion, GenerateRootseswversionRegister()},
                {(long)Registers.Cfgdrpu_Cfgrpuratd0, GenerateCfgdrpu_cfgrpuratd0Register()},
                {(long)Registers.Cfgdrpu_Cfgrpuratd2, GenerateCfgdrpu_cfgrpuratd2Register()},
                {(long)Registers.Cfgdrpu_Cfgrpuratd4, GenerateCfgdrpu_cfgrpuratd4Register()},
                {(long)Registers.Cfgdrpu_Cfgrpuratd6, GenerateCfgdrpu_cfgrpuratd6Register()},
                {(long)Registers.Cfgdrpu_Cfgrpuratd8, GenerateCfgdrpu_cfgrpuratd8Register()},
                {(long)Registers.Cfgdrpu_Cfgrpuratd12, GenerateCfgdrpu_cfgrpuratd12Register()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            SYSCFG_Reset();
        }
        
        protected enum CFGSTCALIB_NOREF
        {
            REF = 0, // Reference clock is implemented
            NOREF = 1, // Reference clock is not implemented
        }
        
        protected enum FPGAIPOTHW_FPGA
        {
            CHIP = 0, // The implementation is a ASIC view
            FPGA = 1, // The implementation is a FPGA view
        }
        
        protected enum FPGAIPOTHW_OTA
        {
            WIRED = 0, // This build do not support external radio PHY
            OTA = 1, // This build support external radio PHY
        }
        
        protected enum FPGAIPOTHW_SESTUB
        {
            SEPRESENT = 0, // Indicates that the SE is present
            SESTUBBED = 1, // Indicates that the SE is stubbed
        }
        
        protected enum FPGAIPOTHW_F38MHZ
        {
            OTHER = 0, // Indicates that the System Clock is not 38MHz
            F38MHZ = 1, // Indicates that the System Clock is 38MHz
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
        
        protected enum RAM_ICACHERAMRETNCTRL_RAMRETNCTRL
        {
            ALLON = 0, // None of the Host ICACHE RAM blocks powered down
            ALLOFF = 1, // Power down all Host ICACHE RAM blocks
        }
        
        // Ipversion - Offset : 0x4
        protected DoubleWordRegister GenerateIpversionRegister() => new DoubleWordRegister(this, 0x9)
            
            .WithValueField(0, 32, out ipversion_ipversion_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ipversion_Ipversion_ValueProvider(_);
                        return ipversion_ipversion_field.Value;
                    },
                    
                    readCallback: (_, __) => Ipversion_Ipversion_Read(_, __),
                    name: "Ipversion")
            .WithReadCallback((_, __) => Ipversion_Read(_, __))
            .WithWriteCallback((_, __) => Ipversion_Write(_, __));
        
        // If - Offset : 0x8
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
            .WithReservedBits(4, 4)
            .WithFlag(8, out if_fpioc_bit, 
                    valueProviderCallback: (_) => {
                        If_Fpioc_ValueProvider(_);
                        return if_fpioc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Fpioc_Write(_, __),
                    
                    readCallback: (_, __) => If_Fpioc_Read(_, __),
                    name: "Fpioc")
            .WithFlag(9, out if_fpdzc_bit, 
                    valueProviderCallback: (_) => {
                        If_Fpdzc_ValueProvider(_);
                        return if_fpdzc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Fpdzc_Write(_, __),
                    
                    readCallback: (_, __) => If_Fpdzc_Read(_, __),
                    name: "Fpdzc")
            .WithFlag(10, out if_fpufc_bit, 
                    valueProviderCallback: (_) => {
                        If_Fpufc_ValueProvider(_);
                        return if_fpufc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Fpufc_Write(_, __),
                    
                    readCallback: (_, __) => If_Fpufc_Read(_, __),
                    name: "Fpufc")
            .WithFlag(11, out if_fpofc_bit, 
                    valueProviderCallback: (_) => {
                        If_Fpofc_ValueProvider(_);
                        return if_fpofc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Fpofc_Write(_, __),
                    
                    readCallback: (_, __) => If_Fpofc_Read(_, __),
                    name: "Fpofc")
            .WithFlag(12, out if_fpidc_bit, 
                    valueProviderCallback: (_) => {
                        If_Fpidc_ValueProvider(_);
                        return if_fpidc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Fpidc_Write(_, __),
                    
                    readCallback: (_, __) => If_Fpidc_Read(_, __),
                    name: "Fpidc")
            .WithFlag(13, out if_fpixc_bit, 
                    valueProviderCallback: (_) => {
                        If_Fpixc_ValueProvider(_);
                        return if_fpixc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Fpixc_Write(_, __),
                    
                    readCallback: (_, __) => If_Fpixc_Read(_, __),
                    name: "Fpixc")
            .WithReservedBits(14, 2)
            .WithFlag(16, out if_host2srwbuserrif_bit, 
                    valueProviderCallback: (_) => {
                        If_Host2srwbuserrif_ValueProvider(_);
                        return if_host2srwbuserrif_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Host2srwbuserrif_Write(_, __),
                    
                    readCallback: (_, __) => If_Host2srwbuserrif_Read(_, __),
                    name: "Host2srwbuserrif")
            .WithFlag(17, out if_srw2hostbuserrif_bit, 
                    valueProviderCallback: (_) => {
                        If_Srw2hostbuserrif_ValueProvider(_);
                        return if_srw2hostbuserrif_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Srw2hostbuserrif_Write(_, __),
                    
                    readCallback: (_, __) => If_Srw2hostbuserrif_Read(_, __),
                    name: "Srw2hostbuserrif")
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
        
        // Ien - Offset : 0xC
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
            .WithReservedBits(4, 4)
            .WithFlag(8, out ien_fpioc_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Fpioc_ValueProvider(_);
                        return ien_fpioc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Fpioc_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Fpioc_Read(_, __),
                    name: "Fpioc")
            .WithFlag(9, out ien_fpdzc_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Fpdzc_ValueProvider(_);
                        return ien_fpdzc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Fpdzc_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Fpdzc_Read(_, __),
                    name: "Fpdzc")
            .WithFlag(10, out ien_fpufc_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Fpufc_ValueProvider(_);
                        return ien_fpufc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Fpufc_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Fpufc_Read(_, __),
                    name: "Fpufc")
            .WithFlag(11, out ien_fpofc_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Fpofc_ValueProvider(_);
                        return ien_fpofc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Fpofc_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Fpofc_Read(_, __),
                    name: "Fpofc")
            .WithFlag(12, out ien_fpidc_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Fpidc_ValueProvider(_);
                        return ien_fpidc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Fpidc_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Fpidc_Read(_, __),
                    name: "Fpidc")
            .WithFlag(13, out ien_fpixc_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Fpixc_ValueProvider(_);
                        return ien_fpixc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Fpixc_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Fpixc_Read(_, __),
                    name: "Fpixc")
            .WithReservedBits(14, 2)
            .WithFlag(16, out ien_host2srwbuserrien_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Host2srwbuserrien_ValueProvider(_);
                        return ien_host2srwbuserrien_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Host2srwbuserrien_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Host2srwbuserrien_Read(_, __),
                    name: "Host2srwbuserrien")
            .WithFlag(17, out ien_srw2hostbuserrien_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Srw2hostbuserrien_ValueProvider(_);
                        return ien_srw2hostbuserrien_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Srw2hostbuserrien_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Srw2hostbuserrien_Read(_, __),
                    name: "Srw2hostbuserrien")
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
        
        // Chiprevhw - Offset : 0x14
        protected DoubleWordRegister GenerateChiprevhwRegister() => new DoubleWordRegister(this, 0x10013)
            
            .WithValueField(0, 12, out chiprevhw_partnumber_field, 
                    valueProviderCallback: (_) => {
                        Chiprevhw_Partnumber_ValueProvider(_);
                        return chiprevhw_partnumber_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprevhw_Partnumber_Write(_, __),
                    
                    readCallback: (_, __) => Chiprevhw_Partnumber_Read(_, __),
                    name: "Partnumber")
            
            .WithValueField(12, 4, out chiprevhw_minor_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Chiprevhw_Minor_ValueProvider(_);
                        return chiprevhw_minor_field.Value;
                    },
                    
                    readCallback: (_, __) => Chiprevhw_Minor_Read(_, __),
                    name: "Minor")
            
            .WithValueField(16, 4, out chiprevhw_major_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Chiprevhw_Major_ValueProvider(_);
                        return chiprevhw_major_field.Value;
                    },
                    
                    readCallback: (_, __) => Chiprevhw_Major_Read(_, __),
                    name: "Major")
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
        
        // Chiprev - Offset : 0x18
        protected DoubleWordRegister GenerateChiprevRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 12, out chiprev_partnumber_field, 
                    valueProviderCallback: (_) => {
                        Chiprev_Partnumber_ValueProvider(_);
                        return chiprev_partnumber_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprev_Partnumber_Write(_, __),
                    
                    readCallback: (_, __) => Chiprev_Partnumber_Read(_, __),
                    name: "Partnumber")
            
            .WithValueField(12, 4, out chiprev_minor_field, 
                    valueProviderCallback: (_) => {
                        Chiprev_Minor_ValueProvider(_);
                        return chiprev_minor_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprev_Minor_Write(_, __),
                    
                    readCallback: (_, __) => Chiprev_Minor_Read(_, __),
                    name: "Minor")
            
            .WithValueField(16, 4, out chiprev_major_field, 
                    valueProviderCallback: (_) => {
                        Chiprev_Major_ValueProvider(_);
                        return chiprev_major_field.Value;
                    },
                    
                    writeCallback: (_, __) => Chiprev_Major_Write(_, __),
                    
                    readCallback: (_, __) => Chiprev_Major_Read(_, __),
                    name: "Major")
            .WithReservedBits(20, 12)
            .WithReadCallback((_, __) => Chiprev_Read(_, __))
            .WithWriteCallback((_, __) => Chiprev_Write(_, __));
        
        // Instanceid - Offset : 0x1C
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
        
        // Cfgstcalib - Offset : 0x20
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
        
        // Cfgsystic - Offset : 0x24
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
        
        // Fpgarevhw - Offset : 0x2C
        protected DoubleWordRegister GenerateFpgarevhwRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out fpgarevhw_fpgarev_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Fpgarevhw_Fpgarev_ValueProvider(_);
                        return fpgarevhw_fpgarev_field.Value;
                    },
                    
                    readCallback: (_, __) => Fpgarevhw_Fpgarev_Read(_, __),
                    name: "Fpgarev")
            .WithReadCallback((_, __) => Fpgarevhw_Read(_, __))
            .WithWriteCallback((_, __) => Fpgarevhw_Write(_, __));
        
        // Fpgaipothw - Offset : 0x30
        protected DoubleWordRegister GenerateFpgaipothwRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, FPGAIPOTHW_FPGA>(0, 1, out fpgaipothw_fpga_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Fpgaipothw_Fpga_ValueProvider(_);
                        return fpgaipothw_fpga_bit.Value;
                    },
                    
                    readCallback: (_, __) => Fpgaipothw_Fpga_Read(_, __),
                    name: "Fpga")
            .WithEnumField<DoubleWordRegister, FPGAIPOTHW_OTA>(1, 1, out fpgaipothw_ota_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Fpgaipothw_Ota_ValueProvider(_);
                        return fpgaipothw_ota_bit.Value;
                    },
                    
                    readCallback: (_, __) => Fpgaipothw_Ota_Read(_, __),
                    name: "Ota")
            .WithEnumField<DoubleWordRegister, FPGAIPOTHW_SESTUB>(2, 1, out fpgaipothw_sestub_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Fpgaipothw_Sestub_ValueProvider(_);
                        return fpgaipothw_sestub_bit.Value;
                    },
                    
                    readCallback: (_, __) => Fpgaipothw_Sestub_Read(_, __),
                    name: "Sestub")
            .WithEnumField<DoubleWordRegister, FPGAIPOTHW_F38MHZ>(3, 1, out fpgaipothw_f38mhz_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Fpgaipothw_F38mhz_ValueProvider(_);
                        return fpgaipothw_f38mhz_bit.Value;
                    },
                    
                    readCallback: (_, __) => Fpgaipothw_F38mhz_Read(_, __),
                    name: "F38mhz")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Fpgaipothw_Read(_, __))
            .WithWriteCallback((_, __) => Fpgaipothw_Write(_, __));
        
        // Cfgahbintercnct - Offset : 0x34
        protected DoubleWordRegister GenerateCfgahbintercnctRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgahbintercnct_mlahbhidleslvsel_bit, 
                    valueProviderCallback: (_) => {
                        Cfgahbintercnct_Mlahbhidleslvsel_ValueProvider(_);
                        return cfgahbintercnct_mlahbhidleslvsel_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgahbintercnct_Mlahbhidleslvsel_Write(_, __),
                    
                    readCallback: (_, __) => Cfgahbintercnct_Mlahbhidleslvsel_Read(_, __),
                    name: "Mlahbhidleslvsel")
            .WithFlag(1, out cfgahbintercnct_mlahbridleslvsel_bit, 
                    valueProviderCallback: (_) => {
                        Cfgahbintercnct_Mlahbridleslvsel_ValueProvider(_);
                        return cfgahbintercnct_mlahbridleslvsel_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgahbintercnct_Mlahbridleslvsel_Write(_, __),
                    
                    readCallback: (_, __) => Cfgahbintercnct_Mlahbridleslvsel_Read(_, __),
                    name: "Mlahbridleslvsel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Cfgahbintercnct_Read(_, __))
            .WithWriteCallback((_, __) => Cfgahbintercnct_Write(_, __));
        
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
        protected DoubleWordRegister GenerateRam_ctrlRegister() => new DoubleWordRegister(this, 0x23)
            .WithFlag(0, out ram_ctrl_addrfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ctrl_Addrfaulten_ValueProvider(_);
                        return ram_ctrl_addrfaulten_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ctrl_Addrfaulten_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ctrl_Addrfaulten_Read(_, __),
                    name: "Addrfaulten")
            .WithFlag(1, out ram_ctrl_clkdisfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Ctrl_Clkdisfaulten_ValueProvider(_);
                        return ram_ctrl_clkdisfaulten_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Ctrl_Clkdisfaulten_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Ctrl_Clkdisfaulten_Read(_, __),
                    name: "Clkdisfaulten")
            .WithReservedBits(2, 3)
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
            .WithValueField(0, 32, out ram_dmem0retnctrl_ramretnctrl_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0retnctrl_Ramretnctrl_ValueProvider(_);
                        return ram_dmem0retnctrl_ramretnctrl_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0retnctrl_Ramretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0retnctrl_Ramretnctrl_Read(_, __),
                    name: "Ramretnctrl")
            .WithReadCallback((_, __) => Ram_Dmem0retnctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Dmem0retnctrl_Write(_, __));
        
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
        
        // Ram_Icacheramretnctrl - Offset : 0x418
        protected DoubleWordRegister GenerateRam_icacheramretnctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, RAM_ICACHERAMRETNCTRL_RAMRETNCTRL>(0, 1, out ram_icacheramretnctrl_ramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Ram_Icacheramretnctrl_Ramretnctrl_ValueProvider(_);
                        return ram_icacheramretnctrl_ramretnctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Icacheramretnctrl_Ramretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Icacheramretnctrl_Ramretnctrl_Read(_, __),
                    name: "Ramretnctrl")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Ram_Icacheramretnctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Icacheramretnctrl_Write(_, __));
        
        // Ram_Dmem0portmapsel - Offset : 0x41C
        protected DoubleWordRegister GenerateRam_dmem0portmapselRegister() => new DoubleWordRegister(this, 0x7905)
            
            .WithValueField(0, 2, out ram_dmem0portmapsel_ldmaportsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Ldmaportsel_ValueProvider(_);
                        return ram_dmem0portmapsel_ldmaportsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Ldmaportsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Ldmaportsel_Read(_, __),
                    name: "Ldmaportsel")
            
            .WithValueField(2, 2, out ram_dmem0portmapsel_srwaesportsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Srwaesportsel_ValueProvider(_);
                        return ram_dmem0portmapsel_srwaesportsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Srwaesportsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Srwaesportsel_Read(_, __),
                    name: "Srwaesportsel")
            
            .WithValueField(4, 2, out ram_dmem0portmapsel_ahbsrwportsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Ahbsrwportsel_ValueProvider(_);
                        return ram_dmem0portmapsel_ahbsrwportsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Ahbsrwportsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Ahbsrwportsel_Read(_, __),
                    name: "Ahbsrwportsel")
            
            .WithValueField(6, 2, out ram_dmem0portmapsel_srweca0portsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Srweca0portsel_ValueProvider(_);
                        return ram_dmem0portmapsel_srweca0portsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Srweca0portsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Srweca0portsel_Read(_, __),
                    name: "Srweca0portsel")
            
            .WithValueField(8, 2, out ram_dmem0portmapsel_srweca1portsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Srweca1portsel_ValueProvider(_);
                        return ram_dmem0portmapsel_srweca1portsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Srweca1portsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Srweca1portsel_Read(_, __),
                    name: "Srweca1portsel")
            
            .WithValueField(10, 2, out ram_dmem0portmapsel_mvpahbdata0portsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Mvpahbdata0portsel_ValueProvider(_);
                        return ram_dmem0portmapsel_mvpahbdata0portsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Mvpahbdata0portsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Mvpahbdata0portsel_Read(_, __),
                    name: "Mvpahbdata0portsel")
            
            .WithValueField(12, 2, out ram_dmem0portmapsel_mvpahbdata1portsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Mvpahbdata1portsel_ValueProvider(_);
                        return ram_dmem0portmapsel_mvpahbdata1portsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Mvpahbdata1portsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Mvpahbdata1portsel_Read(_, __),
                    name: "Mvpahbdata1portsel")
            
            .WithValueField(14, 2, out ram_dmem0portmapsel_mvpahbdata2portsel_field, 
                    valueProviderCallback: (_) => {
                        Ram_Dmem0portmapsel_Mvpahbdata2portsel_ValueProvider(_);
                        return ram_dmem0portmapsel_mvpahbdata2portsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ram_Dmem0portmapsel_Mvpahbdata2portsel_Write(_, __),
                    
                    readCallback: (_, __) => Ram_Dmem0portmapsel_Mvpahbdata2portsel_Read(_, __),
                    name: "Mvpahbdata2portsel")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Ram_Dmem0portmapsel_Read(_, __))
            .WithWriteCallback((_, __) => Ram_Dmem0portmapsel_Write(_, __));
        
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
        protected DoubleWordRegister GenerateRootlockstatusRegister() => new DoubleWordRegister(this, 0x7F0107)
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
            .WithReservedBits(3, 5)
            .WithFlag(8, out rootlockstatus_rootdbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Rootdbglock_ValueProvider(_);
                        return rootlockstatus_rootdbglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Rootdbglock_Read(_, __),
                    name: "Rootdbglock")
            .WithReservedBits(9, 7)
            .WithFlag(16, out rootlockstatus_userdbgaplock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userdbgaplock_ValueProvider(_);
                        return rootlockstatus_userdbgaplock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userdbgaplock_Read(_, __),
                    name: "Userdbgaplock")
            .WithFlag(17, out rootlockstatus_userdbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userdbglock_ValueProvider(_);
                        return rootlockstatus_userdbglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userdbglock_Read(_, __),
                    name: "Userdbglock")
            .WithFlag(18, out rootlockstatus_usernidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Usernidlock_ValueProvider(_);
                        return rootlockstatus_usernidlock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Usernidlock_Read(_, __),
                    name: "Usernidlock")
            .WithFlag(19, out rootlockstatus_userspidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userspidlock_ValueProvider(_);
                        return rootlockstatus_userspidlock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userspidlock_Read(_, __),
                    name: "Userspidlock")
            .WithFlag(20, out rootlockstatus_userspnidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Userspnidlock_ValueProvider(_);
                        return rootlockstatus_userspnidlock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Userspnidlock_Read(_, __),
                    name: "Userspnidlock")
            .WithFlag(21, out rootlockstatus_radioidbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Radioidbglock_ValueProvider(_);
                        return rootlockstatus_radioidbglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Radioidbglock_Read(_, __),
                    name: "Radioidbglock")
            .WithFlag(22, out rootlockstatus_radionidbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Radionidbglock_ValueProvider(_);
                        return rootlockstatus_radionidbglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Radionidbglock_Read(_, __),
                    name: "Radionidbglock")
            .WithReservedBits(23, 8)
            .WithFlag(31, out rootlockstatus_efuseunlocked_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        RootLockstatus_Efuseunlocked_ValueProvider(_);
                        return rootlockstatus_efuseunlocked_bit.Value;
                    },
                    
                    readCallback: (_, __) => RootLockstatus_Efuseunlocked_Read(_, __),
                    name: "Efuseunlocked")
            .WithReadCallback((_, __) => RootLockstatus_Read(_, __))
            .WithWriteCallback((_, __) => RootLockstatus_Write(_, __));
        
        // RootSeswversion - Offset : 0x60C
        protected DoubleWordRegister GenerateRootseswversionRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out rootseswversion_swversion_field, 
                    valueProviderCallback: (_) => {
                        RootSeswversion_Swversion_ValueProvider(_);
                        return rootseswversion_swversion_field.Value;
                    },
                    
                    writeCallback: (_, __) => RootSeswversion_Swversion_Write(_, __),
                    
                    readCallback: (_, __) => RootSeswversion_Swversion_Read(_, __),
                    name: "Swversion")
            .WithReadCallback((_, __) => RootSeswversion_Read(_, __))
            .WithWriteCallback((_, __) => RootSeswversion_Write(_, __));
        
        // Cfgdrpu_Cfgrpuratd0 - Offset : 0x610
        protected DoubleWordRegister GenerateCfgdrpu_cfgrpuratd0Register() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 2)
            .WithFlag(2, out cfgdrpu_cfgrpuratd0_ratdif_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd0_Ratdif_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd0_ratdif_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdif_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdif_Read(_, __),
                    name: "Ratdif")
            .WithFlag(3, out cfgdrpu_cfgrpuratd0_ratdien_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd0_Ratdien_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd0_ratdien_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdien_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdien_Read(_, __),
                    name: "Ratdien")
            .WithReservedBits(4, 2)
            .WithFlag(6, out cfgdrpu_cfgrpuratd0_ratdchiprev_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd0_Ratdchiprev_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd0_ratdchiprev_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdchiprev_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdchiprev_Read(_, __),
                    name: "Ratdchiprev")
            .WithFlag(7, out cfgdrpu_cfgrpuratd0_ratdinstanceid_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd0_Ratdinstanceid_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd0_ratdinstanceid_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdinstanceid_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdinstanceid_Read(_, __),
                    name: "Ratdinstanceid")
            .WithFlag(8, out cfgdrpu_cfgrpuratd0_ratdcfgsstcalib_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd0_Ratdcfgsstcalib_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd0_ratdcfgsstcalib_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdcfgsstcalib_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdcfgsstcalib_Read(_, __),
                    name: "Ratdcfgsstcalib")
            .WithFlag(9, out cfgdrpu_cfgrpuratd0_ratdcfgssystic_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd0_Ratdcfgssystic_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd0_ratdcfgssystic_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdcfgssystic_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdcfgssystic_Read(_, __),
                    name: "Ratdcfgssystic")
            .WithReservedBits(10, 3)
            .WithFlag(13, out cfgdrpu_cfgrpuratd0_ratdcfgahbintercnct_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd0_Ratdcfgahbintercnct_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd0_ratdcfgahbintercnct_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdcfgahbintercnct_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd0_Ratdcfgahbintercnct_Read(_, __),
                    name: "Ratdcfgahbintercnct")
            .WithReservedBits(14, 18)
            .WithReadCallback((_, __) => Cfgdrpu_Cfgrpuratd0_Read(_, __))
            .WithWriteCallback((_, __) => Cfgdrpu_Cfgrpuratd0_Write(_, __));
        
        // Cfgdrpu_Cfgrpuratd2 - Offset : 0x618
        protected DoubleWordRegister GenerateCfgdrpu_cfgrpuratd2Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgdrpu_cfgrpuratd2_ratdsesysromrm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd2_Ratdsesysromrm_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd2_ratdsesysromrm_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsesysromrm_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsesysromrm_Read(_, __),
                    name: "Ratdsesysromrm")
            .WithFlag(1, out cfgdrpu_cfgrpuratd2_ratdsepkeromrm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd2_Ratdsepkeromrm_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd2_ratdsepkeromrm_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsepkeromrm_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsepkeromrm_Read(_, __),
                    name: "Ratdsepkeromrm")
            .WithFlag(2, out cfgdrpu_cfgrpuratd2_ratdsesysctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd2_Ratdsesysctrl_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd2_ratdsesysctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsesysctrl_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsesysctrl_Read(_, __),
                    name: "Ratdsesysctrl")
            .WithFlag(3, out cfgdrpu_cfgrpuratd2_ratdsepkectrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd2_Ratdsepkectrl_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd2_ratdsepkectrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsepkectrl_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd2_Ratdsepkectrl_Read(_, __),
                    name: "Ratdsepkectrl")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Cfgdrpu_Cfgrpuratd2_Read(_, __))
            .WithWriteCallback((_, __) => Cfgdrpu_Cfgrpuratd2_Write(_, __));
        
        // Cfgdrpu_Cfgrpuratd4 - Offset : 0x620
        protected DoubleWordRegister GenerateCfgdrpu_cfgrpuratd4Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgdrpu_cfgrpuratd4_ratdctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd4_Ratdctrl_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd4_ratdctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd4_Ratdctrl_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd4_Ratdctrl_Read(_, __),
                    name: "Ratdctrl")
            .WithReservedBits(1, 1)
            .WithFlag(2, out cfgdrpu_cfgrpuratd4_ratddmem0retnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd4_Ratddmem0retnctrl_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd4_ratddmem0retnctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd4_Ratddmem0retnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd4_Ratddmem0retnctrl_Read(_, __),
                    name: "Ratddmem0retnctrl")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Cfgdrpu_Cfgrpuratd4_Read(_, __))
            .WithWriteCallback((_, __) => Cfgdrpu_Cfgrpuratd4_Write(_, __));
        
        // Cfgdrpu_Cfgrpuratd6 - Offset : 0x628
        protected DoubleWordRegister GenerateCfgdrpu_cfgrpuratd6Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgdrpu_cfgrpuratd6_ratdramrm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd6_Ratdramrm_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd6_ratdramrm_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramrm_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramrm_Read(_, __),
                    name: "Ratdramrm")
            .WithFlag(1, out cfgdrpu_cfgrpuratd6_ratdramwm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd6_Ratdramwm_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd6_ratdramwm_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramwm_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramwm_Read(_, __),
                    name: "Ratdramwm")
            .WithFlag(2, out cfgdrpu_cfgrpuratd6_ratdramra_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd6_Ratdramra_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd6_ratdramra_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramra_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramra_Read(_, __),
                    name: "Ratdramra")
            .WithFlag(3, out cfgdrpu_cfgrpuratd6_ratdrambiasconf_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd6_Ratdrambiasconf_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd6_ratdrambiasconf_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdrambiasconf_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdrambiasconf_Read(_, __),
                    name: "Ratdrambiasconf")
            .WithFlag(4, out cfgdrpu_cfgrpuratd6_ratdramlvtest_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd6_Ratdramlvtest_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd6_ratdramlvtest_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramlvtest_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd6_Ratdramlvtest_Read(_, __),
                    name: "Ratdramlvtest")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Cfgdrpu_Cfgrpuratd6_Read(_, __))
            .WithWriteCallback((_, __) => Cfgdrpu_Cfgrpuratd6_Write(_, __));
        
        // Cfgdrpu_Cfgrpuratd8 - Offset : 0x630
        protected DoubleWordRegister GenerateCfgdrpu_cfgrpuratd8Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgdrpu_cfgrpuratd8_ratdradioramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd8_Ratdradioramretnctrl_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd8_ratdradioramretnctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdradioramretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdradioramretnctrl_Read(_, __),
                    name: "Ratdradioramretnctrl")
            .WithFlag(1, out cfgdrpu_cfgrpuratd8_ratdradioramfeature_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd8_Ratdradioramfeature_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd8_ratdradioramfeature_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdradioramfeature_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdradioramfeature_Read(_, __),
                    name: "Ratdradioramfeature")
            .WithFlag(2, out cfgdrpu_cfgrpuratd8_ratdradioeccctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd8_Ratdradioeccctrl_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd8_ratdradioeccctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdradioeccctrl_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdradioeccctrl_Read(_, __),
                    name: "Ratdradioeccctrl")
            .WithReservedBits(3, 3)
            .WithFlag(6, out cfgdrpu_cfgrpuratd8_ratdicacheramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd8_Ratdicacheramretnctrl_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd8_ratdicacheramretnctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdicacheramretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratdicacheramretnctrl_Read(_, __),
                    name: "Ratdicacheramretnctrl")
            .WithFlag(7, out cfgdrpu_cfgrpuratd8_ratddmem0portmapsel_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd8_Ratddmem0portmapsel_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd8_ratddmem0portmapsel_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratddmem0portmapsel_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd8_Ratddmem0portmapsel_Read(_, __),
                    name: "Ratddmem0portmapsel")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Cfgdrpu_Cfgrpuratd8_Read(_, __))
            .WithWriteCallback((_, __) => Cfgdrpu_Cfgrpuratd8_Write(_, __));
        
        // Cfgdrpu_Cfgrpuratd12 - Offset : 0x640
        protected DoubleWordRegister GenerateCfgdrpu_cfgrpuratd12Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgdrpu_cfgrpuratd12_ratdrootdata0_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd12_Ratdrootdata0_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd12_ratdrootdata0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd12_Ratdrootdata0_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd12_Ratdrootdata0_Read(_, __),
                    name: "Ratdrootdata0")
            .WithFlag(1, out cfgdrpu_cfgrpuratd12_ratdrootdata1_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd12_Ratdrootdata1_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd12_ratdrootdata1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd12_Ratdrootdata1_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd12_Ratdrootdata1_Read(_, __),
                    name: "Ratdrootdata1")
            .WithReservedBits(2, 1)
            .WithFlag(3, out cfgdrpu_cfgrpuratd12_ratdrootseswversion_bit, 
                    valueProviderCallback: (_) => {
                        Cfgdrpu_Cfgrpuratd12_Ratdrootseswversion_ValueProvider(_);
                        return cfgdrpu_cfgrpuratd12_ratdrootseswversion_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfgdrpu_Cfgrpuratd12_Ratdrootseswversion_Write(_, __),
                    
                    readCallback: (_, __) => Cfgdrpu_Cfgrpuratd12_Ratdrootseswversion_Read(_, __),
                    name: "Ratdrootseswversion")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Cfgdrpu_Cfgrpuratd12_Read(_, __))
            .WithWriteCallback((_, __) => Cfgdrpu_Cfgrpuratd12_Write(_, __));
        

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

        



        
        // Ipversion - Offset : 0x4
    
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);
        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        
        // If - Offset : 0x8
    
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
    
        protected IFlagRegisterField if_fpioc_bit;
        partial void If_Fpioc_Write(bool a, bool b);
        partial void If_Fpioc_Read(bool a, bool b);
        partial void If_Fpioc_ValueProvider(bool a);
    
        protected IFlagRegisterField if_fpdzc_bit;
        partial void If_Fpdzc_Write(bool a, bool b);
        partial void If_Fpdzc_Read(bool a, bool b);
        partial void If_Fpdzc_ValueProvider(bool a);
    
        protected IFlagRegisterField if_fpufc_bit;
        partial void If_Fpufc_Write(bool a, bool b);
        partial void If_Fpufc_Read(bool a, bool b);
        partial void If_Fpufc_ValueProvider(bool a);
    
        protected IFlagRegisterField if_fpofc_bit;
        partial void If_Fpofc_Write(bool a, bool b);
        partial void If_Fpofc_Read(bool a, bool b);
        partial void If_Fpofc_ValueProvider(bool a);
    
        protected IFlagRegisterField if_fpidc_bit;
        partial void If_Fpidc_Write(bool a, bool b);
        partial void If_Fpidc_Read(bool a, bool b);
        partial void If_Fpidc_ValueProvider(bool a);
    
        protected IFlagRegisterField if_fpixc_bit;
        partial void If_Fpixc_Write(bool a, bool b);
        partial void If_Fpixc_Read(bool a, bool b);
        partial void If_Fpixc_ValueProvider(bool a);
    
        protected IFlagRegisterField if_host2srwbuserrif_bit;
        partial void If_Host2srwbuserrif_Write(bool a, bool b);
        partial void If_Host2srwbuserrif_Read(bool a, bool b);
        partial void If_Host2srwbuserrif_ValueProvider(bool a);
    
        protected IFlagRegisterField if_srw2hostbuserrif_bit;
        partial void If_Srw2hostbuserrif_Write(bool a, bool b);
        partial void If_Srw2hostbuserrif_Read(bool a, bool b);
        partial void If_Srw2hostbuserrif_ValueProvider(bool a);
    
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
        
        
        // Ien - Offset : 0xC
    
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
    
        protected IFlagRegisterField ien_fpioc_bit;
        partial void Ien_Fpioc_Write(bool a, bool b);
        partial void Ien_Fpioc_Read(bool a, bool b);
        partial void Ien_Fpioc_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_fpdzc_bit;
        partial void Ien_Fpdzc_Write(bool a, bool b);
        partial void Ien_Fpdzc_Read(bool a, bool b);
        partial void Ien_Fpdzc_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_fpufc_bit;
        partial void Ien_Fpufc_Write(bool a, bool b);
        partial void Ien_Fpufc_Read(bool a, bool b);
        partial void Ien_Fpufc_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_fpofc_bit;
        partial void Ien_Fpofc_Write(bool a, bool b);
        partial void Ien_Fpofc_Read(bool a, bool b);
        partial void Ien_Fpofc_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_fpidc_bit;
        partial void Ien_Fpidc_Write(bool a, bool b);
        partial void Ien_Fpidc_Read(bool a, bool b);
        partial void Ien_Fpidc_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_fpixc_bit;
        partial void Ien_Fpixc_Write(bool a, bool b);
        partial void Ien_Fpixc_Read(bool a, bool b);
        partial void Ien_Fpixc_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_host2srwbuserrien_bit;
        partial void Ien_Host2srwbuserrien_Write(bool a, bool b);
        partial void Ien_Host2srwbuserrien_Read(bool a, bool b);
        partial void Ien_Host2srwbuserrien_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_srw2hostbuserrien_bit;
        partial void Ien_Srw2hostbuserrien_Write(bool a, bool b);
        partial void Ien_Srw2hostbuserrien_Read(bool a, bool b);
        partial void Ien_Srw2hostbuserrien_ValueProvider(bool a);
    
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
        
        
        // Chiprevhw - Offset : 0x14
    
        protected IValueRegisterField chiprevhw_partnumber_field;
        partial void Chiprevhw_Partnumber_Write(ulong a, ulong b);
        partial void Chiprevhw_Partnumber_Read(ulong a, ulong b);
        partial void Chiprevhw_Partnumber_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprevhw_minor_field;
        partial void Chiprevhw_Minor_Read(ulong a, ulong b);
        partial void Chiprevhw_Minor_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprevhw_major_field;
        partial void Chiprevhw_Major_Read(ulong a, ulong b);
        partial void Chiprevhw_Major_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprevhw_varient_field;
        partial void Chiprevhw_Varient_Read(ulong a, ulong b);
        partial void Chiprevhw_Varient_ValueProvider(ulong a);
        partial void Chiprevhw_Write(uint a, uint b);
        partial void Chiprevhw_Read(uint a, uint b);
        
        
        // Chiprev - Offset : 0x18
    
        protected IValueRegisterField chiprev_partnumber_field;
        partial void Chiprev_Partnumber_Write(ulong a, ulong b);
        partial void Chiprev_Partnumber_Read(ulong a, ulong b);
        partial void Chiprev_Partnumber_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprev_minor_field;
        partial void Chiprev_Minor_Write(ulong a, ulong b);
        partial void Chiprev_Minor_Read(ulong a, ulong b);
        partial void Chiprev_Minor_ValueProvider(ulong a);
    
        protected IValueRegisterField chiprev_major_field;
        partial void Chiprev_Major_Write(ulong a, ulong b);
        partial void Chiprev_Major_Read(ulong a, ulong b);
        partial void Chiprev_Major_ValueProvider(ulong a);
        partial void Chiprev_Write(uint a, uint b);
        partial void Chiprev_Read(uint a, uint b);
        
        
        // Instanceid - Offset : 0x1C
    
        protected IValueRegisterField instanceid_instanceid_field;
        partial void Instanceid_Instanceid_Write(ulong a, ulong b);
        partial void Instanceid_Instanceid_Read(ulong a, ulong b);
        partial void Instanceid_Instanceid_ValueProvider(ulong a);
        partial void Instanceid_Write(uint a, uint b);
        partial void Instanceid_Read(uint a, uint b);
        
        
        // Cfgstcalib - Offset : 0x20
    
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
        
        
        // Cfgsystic - Offset : 0x24
    
        protected IFlagRegisterField cfgsystic_systicextclken_bit;
        partial void Cfgsystic_Systicextclken_Write(bool a, bool b);
        partial void Cfgsystic_Systicextclken_Read(bool a, bool b);
        partial void Cfgsystic_Systicextclken_ValueProvider(bool a);
        partial void Cfgsystic_Write(uint a, uint b);
        partial void Cfgsystic_Read(uint a, uint b);
        
        
        // Fpgarevhw - Offset : 0x2C
    
        protected IValueRegisterField fpgarevhw_fpgarev_field;
        partial void Fpgarevhw_Fpgarev_Read(ulong a, ulong b);
        partial void Fpgarevhw_Fpgarev_ValueProvider(ulong a);
        partial void Fpgarevhw_Write(uint a, uint b);
        partial void Fpgarevhw_Read(uint a, uint b);
        
        
        // Fpgaipothw - Offset : 0x30
    
        protected IEnumRegisterField<FPGAIPOTHW_FPGA> fpgaipothw_fpga_bit;
        partial void Fpgaipothw_Fpga_Read(FPGAIPOTHW_FPGA a, FPGAIPOTHW_FPGA b);
        partial void Fpgaipothw_Fpga_ValueProvider(FPGAIPOTHW_FPGA a);
    
        protected IEnumRegisterField<FPGAIPOTHW_OTA> fpgaipothw_ota_bit;
        partial void Fpgaipothw_Ota_Read(FPGAIPOTHW_OTA a, FPGAIPOTHW_OTA b);
        partial void Fpgaipothw_Ota_ValueProvider(FPGAIPOTHW_OTA a);
    
        protected IEnumRegisterField<FPGAIPOTHW_SESTUB> fpgaipothw_sestub_bit;
        partial void Fpgaipothw_Sestub_Read(FPGAIPOTHW_SESTUB a, FPGAIPOTHW_SESTUB b);
        partial void Fpgaipothw_Sestub_ValueProvider(FPGAIPOTHW_SESTUB a);
    
        protected IEnumRegisterField<FPGAIPOTHW_F38MHZ> fpgaipothw_f38mhz_bit;
        partial void Fpgaipothw_F38mhz_Read(FPGAIPOTHW_F38MHZ a, FPGAIPOTHW_F38MHZ b);
        partial void Fpgaipothw_F38mhz_ValueProvider(FPGAIPOTHW_F38MHZ a);
        partial void Fpgaipothw_Write(uint a, uint b);
        partial void Fpgaipothw_Read(uint a, uint b);
        
        
        // Cfgahbintercnct - Offset : 0x34
    
        protected IFlagRegisterField cfgahbintercnct_mlahbhidleslvsel_bit;
        partial void Cfgahbintercnct_Mlahbhidleslvsel_Write(bool a, bool b);
        partial void Cfgahbintercnct_Mlahbhidleslvsel_Read(bool a, bool b);
        partial void Cfgahbintercnct_Mlahbhidleslvsel_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgahbintercnct_mlahbridleslvsel_bit;
        partial void Cfgahbintercnct_Mlahbridleslvsel_Write(bool a, bool b);
        partial void Cfgahbintercnct_Mlahbridleslvsel_Read(bool a, bool b);
        partial void Cfgahbintercnct_Mlahbridleslvsel_ValueProvider(bool a);
        partial void Cfgahbintercnct_Write(uint a, uint b);
        partial void Cfgahbintercnct_Read(uint a, uint b);
        
        
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
    
        protected IFlagRegisterField ram_ctrl_clkdisfaulten_bit;
        partial void Ram_Ctrl_Clkdisfaulten_Write(bool a, bool b);
        partial void Ram_Ctrl_Clkdisfaulten_Read(bool a, bool b);
        partial void Ram_Ctrl_Clkdisfaulten_ValueProvider(bool a);
    
        protected IFlagRegisterField ram_ctrl_rameccerrfaulten_bit;
        partial void Ram_Ctrl_Rameccerrfaulten_Write(bool a, bool b);
        partial void Ram_Ctrl_Rameccerrfaulten_Read(bool a, bool b);
        partial void Ram_Ctrl_Rameccerrfaulten_ValueProvider(bool a);
        partial void Ram_Ctrl_Write(uint a, uint b);
        partial void Ram_Ctrl_Read(uint a, uint b);
        
        
        // Ram_Dmem0retnctrl - Offset : 0x208
    
        protected IValueRegisterField ram_dmem0retnctrl_ramretnctrl_field;
        partial void Ram_Dmem0retnctrl_Ramretnctrl_Write(ulong a, ulong b);
        partial void Ram_Dmem0retnctrl_Ramretnctrl_Read(ulong a, ulong b);
        partial void Ram_Dmem0retnctrl_Ramretnctrl_ValueProvider(ulong a);
        partial void Ram_Dmem0retnctrl_Write(uint a, uint b);
        partial void Ram_Dmem0retnctrl_Read(uint a, uint b);
        
        
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
        
        
        // Ram_Icacheramretnctrl - Offset : 0x418
    
        protected IEnumRegisterField<RAM_ICACHERAMRETNCTRL_RAMRETNCTRL> ram_icacheramretnctrl_ramretnctrl_bit;
        partial void Ram_Icacheramretnctrl_Ramretnctrl_Write(RAM_ICACHERAMRETNCTRL_RAMRETNCTRL a, RAM_ICACHERAMRETNCTRL_RAMRETNCTRL b);
        partial void Ram_Icacheramretnctrl_Ramretnctrl_Read(RAM_ICACHERAMRETNCTRL_RAMRETNCTRL a, RAM_ICACHERAMRETNCTRL_RAMRETNCTRL b);
        partial void Ram_Icacheramretnctrl_Ramretnctrl_ValueProvider(RAM_ICACHERAMRETNCTRL_RAMRETNCTRL a);
        partial void Ram_Icacheramretnctrl_Write(uint a, uint b);
        partial void Ram_Icacheramretnctrl_Read(uint a, uint b);
        
        
        // Ram_Dmem0portmapsel - Offset : 0x41C
    
        protected IValueRegisterField ram_dmem0portmapsel_ldmaportsel_field;
        partial void Ram_Dmem0portmapsel_Ldmaportsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Ldmaportsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Ldmaportsel_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_dmem0portmapsel_srwaesportsel_field;
        partial void Ram_Dmem0portmapsel_Srwaesportsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Srwaesportsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Srwaesportsel_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_dmem0portmapsel_ahbsrwportsel_field;
        partial void Ram_Dmem0portmapsel_Ahbsrwportsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Ahbsrwportsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Ahbsrwportsel_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_dmem0portmapsel_srweca0portsel_field;
        partial void Ram_Dmem0portmapsel_Srweca0portsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Srweca0portsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Srweca0portsel_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_dmem0portmapsel_srweca1portsel_field;
        partial void Ram_Dmem0portmapsel_Srweca1portsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Srweca1portsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Srweca1portsel_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_dmem0portmapsel_mvpahbdata0portsel_field;
        partial void Ram_Dmem0portmapsel_Mvpahbdata0portsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Mvpahbdata0portsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Mvpahbdata0portsel_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_dmem0portmapsel_mvpahbdata1portsel_field;
        partial void Ram_Dmem0portmapsel_Mvpahbdata1portsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Mvpahbdata1portsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Mvpahbdata1portsel_ValueProvider(ulong a);
    
        protected IValueRegisterField ram_dmem0portmapsel_mvpahbdata2portsel_field;
        partial void Ram_Dmem0portmapsel_Mvpahbdata2portsel_Write(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Mvpahbdata2portsel_Read(ulong a, ulong b);
        partial void Ram_Dmem0portmapsel_Mvpahbdata2portsel_ValueProvider(ulong a);
        partial void Ram_Dmem0portmapsel_Write(uint a, uint b);
        partial void Ram_Dmem0portmapsel_Read(uint a, uint b);
        
        
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
    
        protected IFlagRegisterField rootlockstatus_rootdbglock_bit;
        partial void RootLockstatus_Rootdbglock_Read(bool a, bool b);
        partial void RootLockstatus_Rootdbglock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_userdbgaplock_bit;
        partial void RootLockstatus_Userdbgaplock_Read(bool a, bool b);
        partial void RootLockstatus_Userdbgaplock_ValueProvider(bool a);
    
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
    
        protected IFlagRegisterField rootlockstatus_radioidbglock_bit;
        partial void RootLockstatus_Radioidbglock_Read(bool a, bool b);
        partial void RootLockstatus_Radioidbglock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_radionidbglock_bit;
        partial void RootLockstatus_Radionidbglock_Read(bool a, bool b);
        partial void RootLockstatus_Radionidbglock_ValueProvider(bool a);
    
        protected IFlagRegisterField rootlockstatus_efuseunlocked_bit;
        partial void RootLockstatus_Efuseunlocked_Read(bool a, bool b);
        partial void RootLockstatus_Efuseunlocked_ValueProvider(bool a);
        partial void RootLockstatus_Write(uint a, uint b);
        partial void RootLockstatus_Read(uint a, uint b);
        
        
        // RootSeswversion - Offset : 0x60C
    
        protected IValueRegisterField rootseswversion_swversion_field;
        partial void RootSeswversion_Swversion_Write(ulong a, ulong b);
        partial void RootSeswversion_Swversion_Read(ulong a, ulong b);
        partial void RootSeswversion_Swversion_ValueProvider(ulong a);
        partial void RootSeswversion_Write(uint a, uint b);
        partial void RootSeswversion_Read(uint a, uint b);
        
        
        // Cfgdrpu_Cfgrpuratd0 - Offset : 0x610
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd0_ratdif_bit;
        partial void Cfgdrpu_Cfgrpuratd0_Ratdif_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdif_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdif_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd0_ratdien_bit;
        partial void Cfgdrpu_Cfgrpuratd0_Ratdien_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdien_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdien_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd0_ratdchiprev_bit;
        partial void Cfgdrpu_Cfgrpuratd0_Ratdchiprev_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdchiprev_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdchiprev_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd0_ratdinstanceid_bit;
        partial void Cfgdrpu_Cfgrpuratd0_Ratdinstanceid_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdinstanceid_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdinstanceid_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd0_ratdcfgsstcalib_bit;
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgsstcalib_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgsstcalib_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgsstcalib_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd0_ratdcfgssystic_bit;
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgssystic_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgssystic_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgssystic_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd0_ratdcfgahbintercnct_bit;
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgahbintercnct_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgahbintercnct_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd0_Ratdcfgahbintercnct_ValueProvider(bool a);
        partial void Cfgdrpu_Cfgrpuratd0_Write(uint a, uint b);
        partial void Cfgdrpu_Cfgrpuratd0_Read(uint a, uint b);
        
        
        // Cfgdrpu_Cfgrpuratd2 - Offset : 0x618
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd2_ratdsesysromrm_bit;
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsesysromrm_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsesysromrm_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsesysromrm_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd2_ratdsepkeromrm_bit;
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsepkeromrm_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsepkeromrm_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsepkeromrm_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd2_ratdsesysctrl_bit;
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsesysctrl_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsesysctrl_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsesysctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd2_ratdsepkectrl_bit;
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsepkectrl_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsepkectrl_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd2_Ratdsepkectrl_ValueProvider(bool a);
        partial void Cfgdrpu_Cfgrpuratd2_Write(uint a, uint b);
        partial void Cfgdrpu_Cfgrpuratd2_Read(uint a, uint b);
        
        
        // Cfgdrpu_Cfgrpuratd4 - Offset : 0x620
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd4_ratdctrl_bit;
        partial void Cfgdrpu_Cfgrpuratd4_Ratdctrl_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd4_Ratdctrl_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd4_Ratdctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd4_ratddmem0retnctrl_bit;
        partial void Cfgdrpu_Cfgrpuratd4_Ratddmem0retnctrl_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd4_Ratddmem0retnctrl_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd4_Ratddmem0retnctrl_ValueProvider(bool a);
        partial void Cfgdrpu_Cfgrpuratd4_Write(uint a, uint b);
        partial void Cfgdrpu_Cfgrpuratd4_Read(uint a, uint b);
        
        
        // Cfgdrpu_Cfgrpuratd6 - Offset : 0x628
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd6_ratdramrm_bit;
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramrm_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramrm_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramrm_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd6_ratdramwm_bit;
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramwm_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramwm_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramwm_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd6_ratdramra_bit;
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramra_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramra_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramra_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd6_ratdrambiasconf_bit;
        partial void Cfgdrpu_Cfgrpuratd6_Ratdrambiasconf_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdrambiasconf_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdrambiasconf_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd6_ratdramlvtest_bit;
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramlvtest_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramlvtest_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd6_Ratdramlvtest_ValueProvider(bool a);
        partial void Cfgdrpu_Cfgrpuratd6_Write(uint a, uint b);
        partial void Cfgdrpu_Cfgrpuratd6_Read(uint a, uint b);
        
        
        // Cfgdrpu_Cfgrpuratd8 - Offset : 0x630
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd8_ratdradioramretnctrl_bit;
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioramretnctrl_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioramretnctrl_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioramretnctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd8_ratdradioramfeature_bit;
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioramfeature_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioramfeature_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioramfeature_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd8_ratdradioeccctrl_bit;
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioeccctrl_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioeccctrl_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdradioeccctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd8_ratdicacheramretnctrl_bit;
        partial void Cfgdrpu_Cfgrpuratd8_Ratdicacheramretnctrl_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdicacheramretnctrl_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratdicacheramretnctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd8_ratddmem0portmapsel_bit;
        partial void Cfgdrpu_Cfgrpuratd8_Ratddmem0portmapsel_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratddmem0portmapsel_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd8_Ratddmem0portmapsel_ValueProvider(bool a);
        partial void Cfgdrpu_Cfgrpuratd8_Write(uint a, uint b);
        partial void Cfgdrpu_Cfgrpuratd8_Read(uint a, uint b);
        
        
        // Cfgdrpu_Cfgrpuratd12 - Offset : 0x640
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd12_ratdrootdata0_bit;
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootdata0_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootdata0_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootdata0_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd12_ratdrootdata1_bit;
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootdata1_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootdata1_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootdata1_ValueProvider(bool a);
    
        protected IFlagRegisterField cfgdrpu_cfgrpuratd12_ratdrootseswversion_bit;
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootseswversion_Write(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootseswversion_Read(bool a, bool b);
        partial void Cfgdrpu_Cfgrpuratd12_Ratdrootseswversion_ValueProvider(bool a);
        partial void Cfgdrpu_Cfgrpuratd12_Write(uint a, uint b);
        partial void Cfgdrpu_Cfgrpuratd12_Read(uint a, uint b);
        
        partial void SYSCFG_Reset();

        public bool Enabled = true;

        private ISiLabs_CMU _cmu;
        private ISiLabs_CMU cmu
        {
            get
            {
                if (Object.ReferenceEquals(_cmu, null))
                {
                    foreach(var cmu in machine.GetPeripheralsOfType<ISiLabs_CMU>())
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
            Ipversion = 0x4,
            If = 0x8,
            Ien = 0xC,
            Chiprevhw = 0x14,
            Chiprev = 0x18,
            Instanceid = 0x1C,
            Cfgstcalib = 0x20,
            Cfgsystic = 0x24,
            Fpgarevhw = 0x2C,
            Fpgaipothw = 0x30,
            Cfgahbintercnct = 0x34,
            Rom_Sesysromrm = 0x100,
            Rom_Sepkeromrm = 0x104,
            Rom_Sesysctrl = 0x108,
            Rom_Sepkectrl = 0x10C,
            Ram_Ctrl = 0x200,
            Ram_Dmem0retnctrl = 0x208,
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
            Ram_Icacheramretnctrl = 0x418,
            Ram_Dmem0portmapsel = 0x41C,
            RootData0 = 0x600,
            RootData1 = 0x604,
            RootLockstatus = 0x608,
            RootSeswversion = 0x60C,
            Cfgdrpu_Cfgrpuratd0 = 0x610,
            Cfgdrpu_Cfgrpuratd2 = 0x618,
            Cfgdrpu_Cfgrpuratd4 = 0x620,
            Cfgdrpu_Cfgrpuratd6 = 0x628,
            Cfgdrpu_Cfgrpuratd8 = 0x630,
            Cfgdrpu_Cfgrpuratd12 = 0x640,
            
            Ipversion_SET = 0x1004,
            If_SET = 0x1008,
            Ien_SET = 0x100C,
            Chiprevhw_SET = 0x1014,
            Chiprev_SET = 0x1018,
            Instanceid_SET = 0x101C,
            Cfgstcalib_SET = 0x1020,
            Cfgsystic_SET = 0x1024,
            Fpgarevhw_SET = 0x102C,
            Fpgaipothw_SET = 0x1030,
            Cfgahbintercnct_SET = 0x1034,
            Rom_Sesysromrm_SET = 0x1100,
            Rom_Sepkeromrm_SET = 0x1104,
            Rom_Sesysctrl_SET = 0x1108,
            Rom_Sepkectrl_SET = 0x110C,
            Ram_Ctrl_SET = 0x1200,
            Ram_Dmem0retnctrl_SET = 0x1208,
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
            Ram_Icacheramretnctrl_SET = 0x1418,
            Ram_Dmem0portmapsel_SET = 0x141C,
            RootData0_SET = 0x1600,
            RootData1_SET = 0x1604,
            RootLockstatus_SET = 0x1608,
            RootSeswversion_SET = 0x160C,
            Cfgdrpu_Cfgrpuratd0_SET = 0x1610,
            Cfgdrpu_Cfgrpuratd2_SET = 0x1618,
            Cfgdrpu_Cfgrpuratd4_SET = 0x1620,
            Cfgdrpu_Cfgrpuratd6_SET = 0x1628,
            Cfgdrpu_Cfgrpuratd8_SET = 0x1630,
            Cfgdrpu_Cfgrpuratd12_SET = 0x1640,
            
            Ipversion_CLR = 0x2004,
            If_CLR = 0x2008,
            Ien_CLR = 0x200C,
            Chiprevhw_CLR = 0x2014,
            Chiprev_CLR = 0x2018,
            Instanceid_CLR = 0x201C,
            Cfgstcalib_CLR = 0x2020,
            Cfgsystic_CLR = 0x2024,
            Fpgarevhw_CLR = 0x202C,
            Fpgaipothw_CLR = 0x2030,
            Cfgahbintercnct_CLR = 0x2034,
            Rom_Sesysromrm_CLR = 0x2100,
            Rom_Sepkeromrm_CLR = 0x2104,
            Rom_Sesysctrl_CLR = 0x2108,
            Rom_Sepkectrl_CLR = 0x210C,
            Ram_Ctrl_CLR = 0x2200,
            Ram_Dmem0retnctrl_CLR = 0x2208,
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
            Ram_Icacheramretnctrl_CLR = 0x2418,
            Ram_Dmem0portmapsel_CLR = 0x241C,
            RootData0_CLR = 0x2600,
            RootData1_CLR = 0x2604,
            RootLockstatus_CLR = 0x2608,
            RootSeswversion_CLR = 0x260C,
            Cfgdrpu_Cfgrpuratd0_CLR = 0x2610,
            Cfgdrpu_Cfgrpuratd2_CLR = 0x2618,
            Cfgdrpu_Cfgrpuratd4_CLR = 0x2620,
            Cfgdrpu_Cfgrpuratd6_CLR = 0x2628,
            Cfgdrpu_Cfgrpuratd8_CLR = 0x2630,
            Cfgdrpu_Cfgrpuratd12_CLR = 0x2640,
            
            Ipversion_TGL = 0x3004,
            If_TGL = 0x3008,
            Ien_TGL = 0x300C,
            Chiprevhw_TGL = 0x3014,
            Chiprev_TGL = 0x3018,
            Instanceid_TGL = 0x301C,
            Cfgstcalib_TGL = 0x3020,
            Cfgsystic_TGL = 0x3024,
            Fpgarevhw_TGL = 0x302C,
            Fpgaipothw_TGL = 0x3030,
            Cfgahbintercnct_TGL = 0x3034,
            Rom_Sesysromrm_TGL = 0x3100,
            Rom_Sepkeromrm_TGL = 0x3104,
            Rom_Sesysctrl_TGL = 0x3108,
            Rom_Sepkectrl_TGL = 0x310C,
            Ram_Ctrl_TGL = 0x3200,
            Ram_Dmem0retnctrl_TGL = 0x3208,
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
            Ram_Icacheramretnctrl_TGL = 0x3418,
            Ram_Dmem0portmapsel_TGL = 0x341C,
            RootData0_TGL = 0x3600,
            RootData1_TGL = 0x3604,
            RootLockstatus_TGL = 0x3608,
            RootSeswversion_TGL = 0x360C,
            Cfgdrpu_Cfgrpuratd0_TGL = 0x3610,
            Cfgdrpu_Cfgrpuratd2_TGL = 0x3618,
            Cfgdrpu_Cfgrpuratd4_TGL = 0x3620,
            Cfgdrpu_Cfgrpuratd6_TGL = 0x3628,
            Cfgdrpu_Cfgrpuratd8_TGL = 0x3630,
            Cfgdrpu_Cfgrpuratd12_TGL = 0x3640,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}
