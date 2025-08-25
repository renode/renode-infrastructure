//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    SYSCFG, Generated on : 2024-01-19 14:24:38.014281
    SYSCFG, ID Version : 8502eff413b04f7b9fdc7a6f39981e53.10 */

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
    public partial class SiLabs_SYSCFG_10
    {
        public SiLabs_SYSCFG_10(Machine machine) : base(machine)
        {
            SiLabs_SYSCFG_10_constructor();
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
    public partial class SiLabs_SYSCFG_10 : BasicDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_SYSCFG_10(Machine machine) : base(machine)
        {
            Define_Registers();
            SiLabs_SYSCFG_10_Constructor();
        }

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
                {(long)Registers.Romrevhw, GenerateRomrevhwRegister()},
                {(long)Registers.Sesysromrm_Rom, GenerateSesysromrm_romRegister()},
                {(long)Registers.Sepkeromrm_Rom, GenerateSepkeromrm_romRegister()},
                {(long)Registers.Sesysctrl_Rom, GenerateSesysctrl_romRegister()},
                {(long)Registers.Sepkectrl_Rom, GenerateSepkectrl_romRegister()},
                {(long)Registers.Ctrl_Ram, GenerateCtrl_ramRegister()},
                {(long)Registers.Ramrm_Ram, GenerateRamrm_ramRegister()},
                {(long)Registers.Ramwm_Ram, GenerateRamwm_ramRegister()},
                {(long)Registers.Ramra_Ram, GenerateRamra_ramRegister()},
                {(long)Registers.Rambiasconf_Ram, GenerateRambiasconf_ramRegister()},
                {(long)Registers.Ramlvtest_Ram, GenerateRamlvtest_ramRegister()},
                {(long)Registers.Ram1prm_Ram, GenerateRam1prm_ramRegister()},
                {(long)Registers.Reg1prm_Ram, GenerateReg1prm_ramRegister()},
                {(long)Registers.Ram1pwm_Ram, GenerateRam1pwm_ramRegister()},
                {(long)Registers.Reg1pwm_Ram, GenerateReg1pwm_ramRegister()},
                {(long)Registers.Data0, GenerateData0Register()},
                {(long)Registers.Data1, GenerateData1Register()},
                {(long)Registers.Lockstatus, GenerateLockstatusRegister()},
                {(long)Registers.Seswversion, GenerateSeswversionRegister()},
                {(long)Registers.Serootinfo, GenerateSerootinfoRegister()},
                {(long)Registers.Serootinfostatus, GenerateSerootinfostatusRegister()},
                {(long)Registers.Cfgrpuratd0_Cfgdrpu, GenerateCfgrpuratd0_cfgdrpuRegister()},
                {(long)Registers.Cfgrpuratd2_Cfgdrpu, GenerateCfgrpuratd2_cfgdrpuRegister()},
                {(long)Registers.Cfgrpuratd4_Cfgdrpu, GenerateCfgrpuratd4_cfgdrpuRegister()},
                {(long)Registers.Cfgrpuratd6_Cfgdrpu, GenerateCfgrpuratd6_cfgdrpuRegister()},
                {(long)Registers.Cfgrpuratd8_Cfgdrpu, GenerateCfgrpuratd8_cfgdrpuRegister()},
                {(long)Registers.Cfgrpuratd12_Cfgdrpu, GenerateCfgrpuratd12_cfgdrpuRegister()},
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
        
        protected enum RAMBIASCONF_RAM_RAMBIASCTRL
        {
            No = 0, // None
            VSB100 = 1, // Voltage Source Bias 100mV
            VSB200 = 2, // Voltage Source Bias 200mV
            VSB300 = 4, // Voltage Source Bias 300mV
            VSB400 = 8, // Voltage Source Bias 400mV
        }
        
        // Ipversion - Offset : 0x4
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0xA)
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
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
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
            .WithFlag(16, out if_tealsysif_bit, 
                    valueProviderCallback: (_) => {
                        If_Tealsysif_ValueProvider(_);
                        return if_tealsysif_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Tealsysif_Write(_, __),
                    readCallback: (_, __) => If_Tealsysif_Read(_, __),
                    name: "Tealsysif")
            .WithFlag(17, out if_l1cache0if_bit, 
                    valueProviderCallback: (_) => {
                        If_L1cache0if_ValueProvider(_);
                        return if_l1cache0if_bit.Value;               
                    },
                    writeCallback: (_, __) => If_L1cache0if_Write(_, __),
                    readCallback: (_, __) => If_L1cache0if_Read(_, __),
                    name: "L1cache0if")
            .WithReservedBits(18, 14)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0xC
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
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
            .WithFlag(16, out ien_tealsysien_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Tealsysien_ValueProvider(_);
                        return ien_tealsysien_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Tealsysien_Write(_, __),
                    readCallback: (_, __) => Ien_Tealsysien_Read(_, __),
                    name: "Tealsysien")
            .WithFlag(17, out ien_l1cache0ien_bit, 
                    valueProviderCallback: (_) => {
                        Ien_L1cache0ien_ValueProvider(_);
                        return ien_l1cache0ien_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_L1cache0ien_Write(_, __),
                    readCallback: (_, __) => Ien_L1cache0ien_Read(_, __),
                    name: "L1cache0ien")
            .WithReservedBits(18, 14)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Chiprevhw - Offset : 0x14
        protected DoubleWordRegister  GenerateChiprevhwRegister() => new DoubleWordRegister(this, 0x10011)
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
            .WithValueField(24, 8, out chiprevhw_variant_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Chiprevhw_Variant_ValueProvider(_);
                        return chiprevhw_variant_field.Value;               
                    },
                    readCallback: (_, __) => Chiprevhw_Variant_Read(_, __),
                    name: "Variant")
            .WithReadCallback((_, __) => Chiprevhw_Read(_, __))
            .WithWriteCallback((_, __) => Chiprevhw_Write(_, __));
        
        // Chiprev - Offset : 0x18
        protected DoubleWordRegister  GenerateChiprevRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateInstanceidRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateCfgstcalibRegister() => new DoubleWordRegister(this, 0x1004A37)
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
        protected DoubleWordRegister  GenerateCfgsysticRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateFpgarevhwRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateFpgaipothwRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out fpgaipothw_fpgaipo_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Fpgaipothw_Fpgaipo_ValueProvider(_);
                        return fpgaipothw_fpgaipo_field.Value;               
                    },
                    readCallback: (_, __) => Fpgaipothw_Fpgaipo_Read(_, __),
                    name: "Fpgaipo")
            .WithReadCallback((_, __) => Fpgaipothw_Read(_, __))
            .WithWriteCallback((_, __) => Fpgaipothw_Write(_, __));
        
        // Romrevhw - Offset : 0x34
        protected DoubleWordRegister  GenerateRomrevhwRegister() => new DoubleWordRegister(this, 0x30000001)
            .WithValueField(0, 8, out romrevhw_seromrev_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Romrevhw_Seromrev_ValueProvider(_);
                        return romrevhw_seromrev_field.Value;               
                    },
                    readCallback: (_, __) => Romrevhw_Seromrev_Read(_, __),
                    name: "Seromrev")
            .WithReservedBits(8, 16)
            .WithValueField(24, 8, out romrevhw_secompatid_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Romrevhw_Secompatid_ValueProvider(_);
                        return romrevhw_secompatid_field.Value;               
                    },
                    readCallback: (_, __) => Romrevhw_Secompatid_Read(_, __),
                    name: "Secompatid")
            .WithReadCallback((_, __) => Romrevhw_Read(_, __))
            .WithWriteCallback((_, __) => Romrevhw_Write(_, __));
        
        // Sesysromrm_Rom - Offset : 0x100
        protected DoubleWordRegister  GenerateSesysromrm_romRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 4, out sesysromrm_rom_sesysromrm_field, 
                    valueProviderCallback: (_) => {
                        Sesysromrm_Rom_Sesysromrm_ValueProvider(_);
                        return sesysromrm_rom_sesysromrm_field.Value;               
                    },
                    writeCallback: (_, __) => Sesysromrm_Rom_Sesysromrm_Write(_, __),
                    readCallback: (_, __) => Sesysromrm_Rom_Sesysromrm_Read(_, __),
                    name: "Sesysromrm")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Sesysromrm_Rom_Read(_, __))
            .WithWriteCallback((_, __) => Sesysromrm_Rom_Write(_, __));
        
        // Sepkeromrm_Rom - Offset : 0x104
        protected DoubleWordRegister  GenerateSepkeromrm_romRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 4, out sepkeromrm_rom_sepkeromrm_field, 
                    valueProviderCallback: (_) => {
                        Sepkeromrm_Rom_Sepkeromrm_ValueProvider(_);
                        return sepkeromrm_rom_sepkeromrm_field.Value;               
                    },
                    writeCallback: (_, __) => Sepkeromrm_Rom_Sepkeromrm_Write(_, __),
                    readCallback: (_, __) => Sepkeromrm_Rom_Sepkeromrm_Read(_, __),
                    name: "Sepkeromrm")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Sepkeromrm_Rom_Read(_, __))
            .WithWriteCallback((_, __) => Sepkeromrm_Rom_Write(_, __));
        
        // Sesysctrl_Rom - Offset : 0x108
        protected DoubleWordRegister  GenerateSesysctrl_romRegister() => new DoubleWordRegister(this, 0x100)
            .WithFlag(0, out sesysctrl_rom_sesysromtest1_bit, 
                    valueProviderCallback: (_) => {
                        Sesysctrl_Rom_Sesysromtest1_ValueProvider(_);
                        return sesysctrl_rom_sesysromtest1_bit.Value;               
                    },
                    writeCallback: (_, __) => Sesysctrl_Rom_Sesysromtest1_Write(_, __),
                    readCallback: (_, __) => Sesysctrl_Rom_Sesysromtest1_Read(_, __),
                    name: "Sesysromtest1")
            .WithReservedBits(1, 7)
            .WithFlag(8, out sesysctrl_rom_sesysromrme_bit, 
                    valueProviderCallback: (_) => {
                        Sesysctrl_Rom_Sesysromrme_ValueProvider(_);
                        return sesysctrl_rom_sesysromrme_bit.Value;               
                    },
                    writeCallback: (_, __) => Sesysctrl_Rom_Sesysromrme_Write(_, __),
                    readCallback: (_, __) => Sesysctrl_Rom_Sesysromrme_Read(_, __),
                    name: "Sesysromrme")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Sesysctrl_Rom_Read(_, __))
            .WithWriteCallback((_, __) => Sesysctrl_Rom_Write(_, __));
        
        // Sepkectrl_Rom - Offset : 0x10C
        protected DoubleWordRegister  GenerateSepkectrl_romRegister() => new DoubleWordRegister(this, 0x100)
            .WithFlag(0, out sepkectrl_rom_sepkeromtest1_bit, 
                    valueProviderCallback: (_) => {
                        Sepkectrl_Rom_Sepkeromtest1_ValueProvider(_);
                        return sepkectrl_rom_sepkeromtest1_bit.Value;               
                    },
                    writeCallback: (_, __) => Sepkectrl_Rom_Sepkeromtest1_Write(_, __),
                    readCallback: (_, __) => Sepkectrl_Rom_Sepkeromtest1_Read(_, __),
                    name: "Sepkeromtest1")
            .WithReservedBits(1, 7)
            .WithFlag(8, out sepkectrl_rom_sepkeromrme_bit, 
                    valueProviderCallback: (_) => {
                        Sepkectrl_Rom_Sepkeromrme_ValueProvider(_);
                        return sepkectrl_rom_sepkeromrme_bit.Value;               
                    },
                    writeCallback: (_, __) => Sepkectrl_Rom_Sepkeromrme_Write(_, __),
                    readCallback: (_, __) => Sepkectrl_Rom_Sepkeromrme_Read(_, __),
                    name: "Sepkeromrme")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Sepkectrl_Rom_Read(_, __))
            .WithWriteCallback((_, __) => Sepkectrl_Rom_Write(_, __));
        
        // Ctrl_Ram - Offset : 0x200
        protected DoubleWordRegister  GenerateCtrl_ramRegister() => new DoubleWordRegister(this, 0x23)
            .WithFlag(0, out ctrl_ram_addrfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Ram_Addrfaulten_ValueProvider(_);
                        return ctrl_ram_addrfaulten_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Ram_Addrfaulten_Write(_, __),
                    readCallback: (_, __) => Ctrl_Ram_Addrfaulten_Read(_, __),
                    name: "Addrfaulten")
            .WithFlag(1, out ctrl_ram_clkdisfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Ram_Clkdisfaulten_ValueProvider(_);
                        return ctrl_ram_clkdisfaulten_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Ram_Clkdisfaulten_Write(_, __),
                    readCallback: (_, __) => Ctrl_Ram_Clkdisfaulten_Read(_, __),
                    name: "Clkdisfaulten")
            .WithReservedBits(2, 3)
            .WithFlag(5, out ctrl_ram_rameccerrfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Ram_Rameccerrfaulten_ValueProvider(_);
                        return ctrl_ram_rameccerrfaulten_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Ram_Rameccerrfaulten_Write(_, __),
                    readCallback: (_, __) => Ctrl_Ram_Rameccerrfaulten_Read(_, __),
                    name: "Rameccerrfaulten")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Ctrl_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Ram_Write(_, __));
        
        // Ramrm_Ram - Offset : 0x300
        protected DoubleWordRegister  GenerateRamrm_ramRegister() => new DoubleWordRegister(this, 0x30301)
            .WithValueField(0, 3, out ramrm_ram_ramrm0_field, 
                    valueProviderCallback: (_) => {
                        Ramrm_Ram_Ramrm0_ValueProvider(_);
                        return ramrm_ram_ramrm0_field.Value;               
                    },
                    writeCallback: (_, __) => Ramrm_Ram_Ramrm0_Write(_, __),
                    readCallback: (_, __) => Ramrm_Ram_Ramrm0_Read(_, __),
                    name: "Ramrm0")
            .WithReservedBits(3, 5)
            .WithValueField(8, 3, out ramrm_ram_ramrm1_field, 
                    valueProviderCallback: (_) => {
                        Ramrm_Ram_Ramrm1_ValueProvider(_);
                        return ramrm_ram_ramrm1_field.Value;               
                    },
                    writeCallback: (_, __) => Ramrm_Ram_Ramrm1_Write(_, __),
                    readCallback: (_, __) => Ramrm_Ram_Ramrm1_Read(_, __),
                    name: "Ramrm1")
            .WithReservedBits(11, 5)
            .WithValueField(16, 3, out ramrm_ram_ramrm2_field, 
                    valueProviderCallback: (_) => {
                        Ramrm_Ram_Ramrm2_ValueProvider(_);
                        return ramrm_ram_ramrm2_field.Value;               
                    },
                    writeCallback: (_, __) => Ramrm_Ram_Ramrm2_Write(_, __),
                    readCallback: (_, __) => Ramrm_Ram_Ramrm2_Read(_, __),
                    name: "Ramrm2")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Ramrm_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Ramrm_Ram_Write(_, __));
        
        // Ramwm_Ram - Offset : 0x304
        protected DoubleWordRegister  GenerateRamwm_ramRegister() => new DoubleWordRegister(this, 0x70307)
            .WithValueField(0, 3, out ramwm_ram_ramwm0_field, 
                    valueProviderCallback: (_) => {
                        Ramwm_Ram_Ramwm0_ValueProvider(_);
                        return ramwm_ram_ramwm0_field.Value;               
                    },
                    writeCallback: (_, __) => Ramwm_Ram_Ramwm0_Write(_, __),
                    readCallback: (_, __) => Ramwm_Ram_Ramwm0_Read(_, __),
                    name: "Ramwm0")
            .WithReservedBits(3, 5)
            .WithValueField(8, 3, out ramwm_ram_ramwm1_field, 
                    valueProviderCallback: (_) => {
                        Ramwm_Ram_Ramwm1_ValueProvider(_);
                        return ramwm_ram_ramwm1_field.Value;               
                    },
                    writeCallback: (_, __) => Ramwm_Ram_Ramwm1_Write(_, __),
                    readCallback: (_, __) => Ramwm_Ram_Ramwm1_Read(_, __),
                    name: "Ramwm1")
            .WithReservedBits(11, 5)
            .WithValueField(16, 3, out ramwm_ram_ramwm2_field, 
                    valueProviderCallback: (_) => {
                        Ramwm_Ram_Ramwm2_ValueProvider(_);
                        return ramwm_ram_ramwm2_field.Value;               
                    },
                    writeCallback: (_, __) => Ramwm_Ram_Ramwm2_Write(_, __),
                    readCallback: (_, __) => Ramwm_Ram_Ramwm2_Read(_, __),
                    name: "Ramwm2")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Ramwm_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Ramwm_Ram_Write(_, __));
        
        // Ramra_Ram - Offset : 0x308
        protected DoubleWordRegister  GenerateRamra_ramRegister() => new DoubleWordRegister(this, 0x1)
            .WithFlag(0, out ramra_ram_ramra0_bit, 
                    valueProviderCallback: (_) => {
                        Ramra_Ram_Ramra0_ValueProvider(_);
                        return ramra_ram_ramra0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ramra_Ram_Ramra0_Write(_, __),
                    readCallback: (_, __) => Ramra_Ram_Ramra0_Read(_, __),
                    name: "Ramra0")
            .WithReservedBits(1, 7)
            .WithFlag(8, out ramra_ram_ramra1_bit, 
                    valueProviderCallback: (_) => {
                        Ramra_Ram_Ramra1_ValueProvider(_);
                        return ramra_ram_ramra1_bit.Value;               
                    },
                    writeCallback: (_, __) => Ramra_Ram_Ramra1_Write(_, __),
                    readCallback: (_, __) => Ramra_Ram_Ramra1_Read(_, __),
                    name: "Ramra1")
            .WithReservedBits(9, 7)
            .WithFlag(16, out ramra_ram_ramra2_bit, 
                    valueProviderCallback: (_) => {
                        Ramra_Ram_Ramra2_ValueProvider(_);
                        return ramra_ram_ramra2_bit.Value;               
                    },
                    writeCallback: (_, __) => Ramra_Ram_Ramra2_Write(_, __),
                    readCallback: (_, __) => Ramra_Ram_Ramra2_Read(_, __),
                    name: "Ramra2")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Ramra_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Ramra_Ram_Write(_, __));
        
        // Rambiasconf_Ram - Offset : 0x30C
        protected DoubleWordRegister  GenerateRambiasconf_ramRegister() => new DoubleWordRegister(this, 0x2)
            .WithEnumField<DoubleWordRegister, RAMBIASCONF_RAM_RAMBIASCTRL>(0, 4, out rambiasconf_ram_rambiasctrl_field, 
                    valueProviderCallback: (_) => {
                        Rambiasconf_Ram_Rambiasctrl_ValueProvider(_);
                        return rambiasconf_ram_rambiasctrl_field.Value;               
                    },
                    writeCallback: (_, __) => Rambiasconf_Ram_Rambiasctrl_Write(_, __),
                    readCallback: (_, __) => Rambiasconf_Ram_Rambiasctrl_Read(_, __),
                    name: "Rambiasctrl")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Rambiasconf_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Rambiasconf_Ram_Write(_, __));
        
        // Ramlvtest_Ram - Offset : 0x310
        protected DoubleWordRegister  GenerateRamlvtest_ramRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ramlvtest_ram_ramlvtest_bit, 
                    valueProviderCallback: (_) => {
                        Ramlvtest_Ram_Ramlvtest_ValueProvider(_);
                        return ramlvtest_ram_ramlvtest_bit.Value;               
                    },
                    writeCallback: (_, __) => Ramlvtest_Ram_Ramlvtest_Write(_, __),
                    readCallback: (_, __) => Ramlvtest_Ram_Ramlvtest_Read(_, __),
                    name: "Ramlvtest")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Ramlvtest_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Ramlvtest_Ram_Write(_, __));
        
        // Ram1prm_Ram - Offset : 0x400
        protected DoubleWordRegister  GenerateRam1prm_ramRegister() => new DoubleWordRegister(this, 0x1A89A)
            .WithValueField(0, 3, out ram1prm_ram_s4096x39_field, 
                    valueProviderCallback: (_) => {
                        Ram1prm_Ram_S4096x39_ValueProvider(_);
                        return ram1prm_ram_s4096x39_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1prm_Ram_S4096x39_Write(_, __),
                    readCallback: (_, __) => Ram1prm_Ram_S4096x39_Read(_, __),
                    name: "S4096x39")
            .WithValueField(3, 3, out ram1prm_ram_s1024x34_field, 
                    valueProviderCallback: (_) => {
                        Ram1prm_Ram_S1024x34_ValueProvider(_);
                        return ram1prm_ram_s1024x34_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1prm_Ram_S1024x34_Write(_, __),
                    readCallback: (_, __) => Ram1prm_Ram_S1024x34_Read(_, __),
                    name: "S1024x34")
            .WithValueField(6, 3, out ram1prm_ram_s2048x34_field, 
                    valueProviderCallback: (_) => {
                        Ram1prm_Ram_S2048x34_ValueProvider(_);
                        return ram1prm_ram_s2048x34_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1prm_Ram_S2048x34_Write(_, __),
                    readCallback: (_, __) => Ram1prm_Ram_S2048x34_Read(_, __),
                    name: "S2048x34")
            .WithValueField(9, 3, out ram1prm_ram_s1024x14_field, 
                    valueProviderCallback: (_) => {
                        Ram1prm_Ram_S1024x14_ValueProvider(_);
                        return ram1prm_ram_s1024x14_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1prm_Ram_S1024x14_Write(_, __),
                    readCallback: (_, __) => Ram1prm_Ram_S1024x14_Read(_, __),
                    name: "S1024x14")
            .WithValueField(12, 3, out ram1prm_ram_s2048x39_field, 
                    valueProviderCallback: (_) => {
                        Ram1prm_Ram_S2048x39_ValueProvider(_);
                        return ram1prm_ram_s2048x39_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1prm_Ram_S2048x39_Write(_, __),
                    readCallback: (_, __) => Ram1prm_Ram_S2048x39_Read(_, __),
                    name: "S2048x39")
            .WithValueField(15, 3, out ram1prm_ram_s1024x39_field, 
                    valueProviderCallback: (_) => {
                        Ram1prm_Ram_S1024x39_ValueProvider(_);
                        return ram1prm_ram_s1024x39_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1prm_Ram_S1024x39_Write(_, __),
                    readCallback: (_, __) => Ram1prm_Ram_S1024x39_Read(_, __),
                    name: "S1024x39")
            .WithReservedBits(18, 14)
            .WithReadCallback((_, __) => Ram1prm_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Ram1prm_Ram_Write(_, __));
        
        // Reg1prm_Ram - Offset : 0x404
        protected DoubleWordRegister  GenerateReg1prm_ramRegister() => new DoubleWordRegister(this, 0x3C)
            .WithValueField(0, 3, out reg1prm_ram_s512x50_field, 
                    valueProviderCallback: (_) => {
                        Reg1prm_Ram_S512x50_ValueProvider(_);
                        return reg1prm_ram_s512x50_field.Value;               
                    },
                    writeCallback: (_, __) => Reg1prm_Ram_S512x50_Write(_, __),
                    readCallback: (_, __) => Reg1prm_Ram_S512x50_Read(_, __),
                    name: "S512x50")
            .WithValueField(3, 3, out reg1prm_ram_s40x138_field, 
                    valueProviderCallback: (_) => {
                        Reg1prm_Ram_S40x138_ValueProvider(_);
                        return reg1prm_ram_s40x138_field.Value;               
                    },
                    writeCallback: (_, __) => Reg1prm_Ram_S40x138_Write(_, __),
                    readCallback: (_, __) => Reg1prm_Ram_S40x138_Read(_, __),
                    name: "S40x138")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Reg1prm_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Reg1prm_Ram_Write(_, __));
        
        // Ram1pwm_Ram - Offset : 0x408
        protected DoubleWordRegister  GenerateRam1pwm_ramRegister() => new DoubleWordRegister(this, 0x2DB6F)
            .WithValueField(0, 3, out ram1pwm_ram_s4096x39_field, 
                    valueProviderCallback: (_) => {
                        Ram1pwm_Ram_S4096x39_ValueProvider(_);
                        return ram1pwm_ram_s4096x39_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1pwm_Ram_S4096x39_Write(_, __),
                    readCallback: (_, __) => Ram1pwm_Ram_S4096x39_Read(_, __),
                    name: "S4096x39")
            .WithValueField(3, 3, out ram1pwm_ram_s1024x34_field, 
                    valueProviderCallback: (_) => {
                        Ram1pwm_Ram_S1024x34_ValueProvider(_);
                        return ram1pwm_ram_s1024x34_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1pwm_Ram_S1024x34_Write(_, __),
                    readCallback: (_, __) => Ram1pwm_Ram_S1024x34_Read(_, __),
                    name: "S1024x34")
            .WithValueField(6, 3, out ram1pwm_ram_s2048x34_field, 
                    valueProviderCallback: (_) => {
                        Ram1pwm_Ram_S2048x34_ValueProvider(_);
                        return ram1pwm_ram_s2048x34_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1pwm_Ram_S2048x34_Write(_, __),
                    readCallback: (_, __) => Ram1pwm_Ram_S2048x34_Read(_, __),
                    name: "S2048x34")
            .WithValueField(9, 3, out ram1pwm_ram_s1024x14_field, 
                    valueProviderCallback: (_) => {
                        Ram1pwm_Ram_S1024x14_ValueProvider(_);
                        return ram1pwm_ram_s1024x14_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1pwm_Ram_S1024x14_Write(_, __),
                    readCallback: (_, __) => Ram1pwm_Ram_S1024x14_Read(_, __),
                    name: "S1024x14")
            .WithValueField(12, 3, out ram1pwm_ram_s2048x39_field, 
                    valueProviderCallback: (_) => {
                        Ram1pwm_Ram_S2048x39_ValueProvider(_);
                        return ram1pwm_ram_s2048x39_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1pwm_Ram_S2048x39_Write(_, __),
                    readCallback: (_, __) => Ram1pwm_Ram_S2048x39_Read(_, __),
                    name: "S2048x39")
            .WithValueField(15, 3, out ram1pwm_ram_s1024x39_field, 
                    valueProviderCallback: (_) => {
                        Ram1pwm_Ram_S1024x39_ValueProvider(_);
                        return ram1pwm_ram_s1024x39_field.Value;               
                    },
                    writeCallback: (_, __) => Ram1pwm_Ram_S1024x39_Write(_, __),
                    readCallback: (_, __) => Ram1pwm_Ram_S1024x39_Read(_, __),
                    name: "S1024x39")
            .WithReservedBits(18, 14)
            .WithReadCallback((_, __) => Ram1pwm_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Ram1pwm_Ram_Write(_, __));
        
        // Reg1pwm_Ram - Offset : 0x40C
        protected DoubleWordRegister  GenerateReg1pwm_ramRegister() => new DoubleWordRegister(this, 0x3F)
            .WithValueField(0, 3, out reg1pwm_ram_s512x50_field, 
                    valueProviderCallback: (_) => {
                        Reg1pwm_Ram_S512x50_ValueProvider(_);
                        return reg1pwm_ram_s512x50_field.Value;               
                    },
                    writeCallback: (_, __) => Reg1pwm_Ram_S512x50_Write(_, __),
                    readCallback: (_, __) => Reg1pwm_Ram_S512x50_Read(_, __),
                    name: "S512x50")
            .WithValueField(3, 3, out reg1pwm_ram_s40x138_field, 
                    valueProviderCallback: (_) => {
                        Reg1pwm_Ram_S40x138_ValueProvider(_);
                        return reg1pwm_ram_s40x138_field.Value;               
                    },
                    writeCallback: (_, __) => Reg1pwm_Ram_S40x138_Write(_, __),
                    readCallback: (_, __) => Reg1pwm_Ram_S40x138_Read(_, __),
                    name: "S40x138")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Reg1pwm_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Reg1pwm_Ram_Write(_, __));
        
        // Data0 - Offset : 0x600
        protected DoubleWordRegister  GenerateData0Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out data0_data_field, 
                    valueProviderCallback: (_) => {
                        Data0_Data_ValueProvider(_);
                        return data0_data_field.Value;               
                    },
                    writeCallback: (_, __) => Data0_Data_Write(_, __),
                    readCallback: (_, __) => Data0_Data_Read(_, __),
                    name: "Data")
            .WithReadCallback((_, __) => Data0_Read(_, __))
            .WithWriteCallback((_, __) => Data0_Write(_, __));
        
        // Data1 - Offset : 0x604
        protected DoubleWordRegister  GenerateData1Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out data1_data_field, 
                    valueProviderCallback: (_) => {
                        Data1_Data_ValueProvider(_);
                        return data1_data_field.Value;               
                    },
                    writeCallback: (_, __) => Data1_Data_Write(_, __),
                    readCallback: (_, __) => Data1_Data_Read(_, __),
                    name: "Data")
            .WithReadCallback((_, __) => Data1_Read(_, __))
            .WithWriteCallback((_, __) => Data1_Write(_, __));
        
        // Lockstatus - Offset : 0x608
        protected DoubleWordRegister  GenerateLockstatusRegister() => new DoubleWordRegister(this, 0x7F0107)
            .WithFlag(0, out lockstatus_buslock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Buslock_ValueProvider(_);
                        return lockstatus_buslock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Buslock_Read(_, __),
                    name: "Buslock")
            .WithFlag(1, out lockstatus_reglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Reglock_ValueProvider(_);
                        return lockstatus_reglock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Reglock_Read(_, __),
                    name: "Reglock")
            .WithFlag(2, out lockstatus_mfrlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Mfrlock_ValueProvider(_);
                        return lockstatus_mfrlock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Mfrlock_Read(_, __),
                    name: "Mfrlock")
            .WithReservedBits(3, 5)
            .WithFlag(8, out lockstatus_rootdbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Rootdbglock_ValueProvider(_);
                        return lockstatus_rootdbglock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Rootdbglock_Read(_, __),
                    name: "Rootdbglock")
            .WithReservedBits(9, 7)
            .WithFlag(16, out lockstatus_userdbgaplock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Userdbgaplock_ValueProvider(_);
                        return lockstatus_userdbgaplock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Userdbgaplock_Read(_, __),
                    name: "Userdbgaplock")
            .WithFlag(17, out lockstatus_userdbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Userdbglock_ValueProvider(_);
                        return lockstatus_userdbglock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Userdbglock_Read(_, __),
                    name: "Userdbglock")
            .WithFlag(18, out lockstatus_usernidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Usernidlock_ValueProvider(_);
                        return lockstatus_usernidlock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Usernidlock_Read(_, __),
                    name: "Usernidlock")
            .WithFlag(19, out lockstatus_userspidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Userspidlock_ValueProvider(_);
                        return lockstatus_userspidlock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Userspidlock_Read(_, __),
                    name: "Userspidlock")
            .WithFlag(20, out lockstatus_userspnidlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Userspnidlock_ValueProvider(_);
                        return lockstatus_userspnidlock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Userspnidlock_Read(_, __),
                    name: "Userspnidlock")
            .WithFlag(21, out lockstatus_lpwcpu0dbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Lpwcpu0dbglock_ValueProvider(_);
                        return lockstatus_lpwcpu0dbglock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Lpwcpu0dbglock_Read(_, __),
                    name: "Lpwcpu0dbglock")
            .WithFlag(22, out lockstatus_lpwcpu1dbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Lpwcpu1dbglock_ValueProvider(_);
                        return lockstatus_lpwcpu1dbglock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Lpwcpu1dbglock_Read(_, __),
                    name: "Lpwcpu1dbglock")
            .WithReservedBits(23, 8)
            .WithFlag(31, out lockstatus_efuseunlocked_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Efuseunlocked_ValueProvider(_);
                        return lockstatus_efuseunlocked_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Efuseunlocked_Read(_, __),
                    name: "Efuseunlocked")
            .WithReadCallback((_, __) => Lockstatus_Read(_, __))
            .WithWriteCallback((_, __) => Lockstatus_Write(_, __));
        
        // Seswversion - Offset : 0x60C
        protected DoubleWordRegister  GenerateSeswversionRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out seswversion_swversion_field, 
                    valueProviderCallback: (_) => {
                        Seswversion_Swversion_ValueProvider(_);
                        return seswversion_swversion_field.Value;               
                    },
                    writeCallback: (_, __) => Seswversion_Swversion_Write(_, __),
                    readCallback: (_, __) => Seswversion_Swversion_Read(_, __),
                    name: "Swversion")
            .WithReadCallback((_, __) => Seswversion_Read(_, __))
            .WithWriteCallback((_, __) => Seswversion_Write(_, __));
        
        // Serootinfo - Offset : 0x610
        protected DoubleWordRegister  GenerateSerootinfoRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out serootinfo_rootsecompromised_bit, 
                    valueProviderCallback: (_) => {
                        Serootinfo_Rootsecompromised_ValueProvider(_);
                        return serootinfo_rootsecompromised_bit.Value;               
                    },
                    writeCallback: (_, __) => Serootinfo_Rootsecompromised_Write(_, __),
                    readCallback: (_, __) => Serootinfo_Rootsecompromised_Read(_, __),
                    name: "Rootsecompromised")
            .WithReservedBits(1, 7)
            .WithFlag(8, out serootinfo_rootselifecycle0_bit, 
                    valueProviderCallback: (_) => {
                        Serootinfo_Rootselifecycle0_ValueProvider(_);
                        return serootinfo_rootselifecycle0_bit.Value;               
                    },
                    writeCallback: (_, __) => Serootinfo_Rootselifecycle0_Write(_, __),
                    readCallback: (_, __) => Serootinfo_Rootselifecycle0_Read(_, __),
                    name: "Rootselifecycle0")
            .WithFlag(9, out serootinfo_rootselifecycle1_bit, 
                    valueProviderCallback: (_) => {
                        Serootinfo_Rootselifecycle1_ValueProvider(_);
                        return serootinfo_rootselifecycle1_bit.Value;               
                    },
                    writeCallback: (_, __) => Serootinfo_Rootselifecycle1_Write(_, __),
                    readCallback: (_, __) => Serootinfo_Rootselifecycle1_Read(_, __),
                    name: "Rootselifecycle1")
            .WithFlag(10, out serootinfo_rootselifecycle2_bit, 
                    valueProviderCallback: (_) => {
                        Serootinfo_Rootselifecycle2_ValueProvider(_);
                        return serootinfo_rootselifecycle2_bit.Value;               
                    },
                    writeCallback: (_, __) => Serootinfo_Rootselifecycle2_Write(_, __),
                    readCallback: (_, __) => Serootinfo_Rootselifecycle2_Read(_, __),
                    name: "Rootselifecycle2")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Serootinfo_Read(_, __))
            .WithWriteCallback((_, __) => Serootinfo_Write(_, __));
        
        // Serootinfostatus - Offset : 0x614
        protected DoubleWordRegister  GenerateSerootinfostatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out serootinfostatus_secompromised_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Serootinfostatus_Secompromised_ValueProvider(_);
                        return serootinfostatus_secompromised_bit.Value;               
                    },
                    readCallback: (_, __) => Serootinfostatus_Secompromised_Read(_, __),
                    name: "Secompromised")
            .WithReservedBits(1, 7)
            .WithFlag(8, out serootinfostatus_selifecycle0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Serootinfostatus_Selifecycle0_ValueProvider(_);
                        return serootinfostatus_selifecycle0_bit.Value;               
                    },
                    readCallback: (_, __) => Serootinfostatus_Selifecycle0_Read(_, __),
                    name: "Selifecycle0")
            .WithFlag(9, out serootinfostatus_selifecycle1_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Serootinfostatus_Selifecycle1_ValueProvider(_);
                        return serootinfostatus_selifecycle1_bit.Value;               
                    },
                    readCallback: (_, __) => Serootinfostatus_Selifecycle1_Read(_, __),
                    name: "Selifecycle1")
            .WithFlag(10, out serootinfostatus_selifecycle2_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Serootinfostatus_Selifecycle2_ValueProvider(_);
                        return serootinfostatus_selifecycle2_bit.Value;               
                    },
                    readCallback: (_, __) => Serootinfostatus_Selifecycle2_Read(_, __),
                    name: "Selifecycle2")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Serootinfostatus_Read(_, __))
            .WithWriteCallback((_, __) => Serootinfostatus_Write(_, __));
        
        // Cfgrpuratd0_Cfgdrpu - Offset : 0x618
        protected DoubleWordRegister  GenerateCfgrpuratd0_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 2)
            .WithFlag(2, out cfgrpuratd0_cfgdrpu_ratdif_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd0_Cfgdrpu_Ratdif_ValueProvider(_);
                        return cfgrpuratd0_cfgdrpu_ratdif_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdif_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdif_Read(_, __),
                    name: "Ratdif")
            .WithFlag(3, out cfgrpuratd0_cfgdrpu_ratdien_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd0_Cfgdrpu_Ratdien_ValueProvider(_);
                        return cfgrpuratd0_cfgdrpu_ratdien_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdien_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdien_Read(_, __),
                    name: "Ratdien")
            .WithReservedBits(4, 2)
            .WithFlag(6, out cfgrpuratd0_cfgdrpu_ratdchiprev_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd0_Cfgdrpu_Ratdchiprev_ValueProvider(_);
                        return cfgrpuratd0_cfgdrpu_ratdchiprev_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdchiprev_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdchiprev_Read(_, __),
                    name: "Ratdchiprev")
            .WithFlag(7, out cfgrpuratd0_cfgdrpu_ratdinstanceid_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd0_Cfgdrpu_Ratdinstanceid_ValueProvider(_);
                        return cfgrpuratd0_cfgdrpu_ratdinstanceid_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdinstanceid_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdinstanceid_Read(_, __),
                    name: "Ratdinstanceid")
            .WithFlag(8, out cfgrpuratd0_cfgdrpu_ratdcfgsstcalib_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd0_Cfgdrpu_Ratdcfgsstcalib_ValueProvider(_);
                        return cfgrpuratd0_cfgdrpu_ratdcfgsstcalib_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdcfgsstcalib_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdcfgsstcalib_Read(_, __),
                    name: "Ratdcfgsstcalib")
            .WithFlag(9, out cfgrpuratd0_cfgdrpu_ratdcfgssystic_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd0_Cfgdrpu_Ratdcfgssystic_ValueProvider(_);
                        return cfgrpuratd0_cfgdrpu_ratdcfgssystic_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdcfgssystic_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdcfgssystic_Read(_, __),
                    name: "Ratdcfgssystic")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Cfgrpuratd0_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd0_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd2_Cfgdrpu - Offset : 0x620
        protected DoubleWordRegister  GenerateCfgrpuratd2_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgrpuratd2_cfgdrpu_ratdsesysromrm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd2_Cfgdrpu_Ratdsesysromrm_ValueProvider(_);
                        return cfgrpuratd2_cfgdrpu_ratdsesysromrm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsesysromrm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsesysromrm_Read(_, __),
                    name: "Ratdsesysromrm")
            .WithFlag(1, out cfgrpuratd2_cfgdrpu_ratdsepkeromrm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd2_Cfgdrpu_Ratdsepkeromrm_ValueProvider(_);
                        return cfgrpuratd2_cfgdrpu_ratdsepkeromrm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsepkeromrm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsepkeromrm_Read(_, __),
                    name: "Ratdsepkeromrm")
            .WithFlag(2, out cfgrpuratd2_cfgdrpu_ratdsesysctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd2_Cfgdrpu_Ratdsesysctrl_ValueProvider(_);
                        return cfgrpuratd2_cfgdrpu_ratdsesysctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsesysctrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsesysctrl_Read(_, __),
                    name: "Ratdsesysctrl")
            .WithFlag(3, out cfgrpuratd2_cfgdrpu_ratdsepkectrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd2_Cfgdrpu_Ratdsepkectrl_ValueProvider(_);
                        return cfgrpuratd2_cfgdrpu_ratdsepkectrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsepkectrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd2_Cfgdrpu_Ratdsepkectrl_Read(_, __),
                    name: "Ratdsepkectrl")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Cfgrpuratd2_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd2_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd4_Cfgdrpu - Offset : 0x628
        protected DoubleWordRegister  GenerateCfgrpuratd4_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgrpuratd4_cfgdrpu_ratdctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd4_Cfgdrpu_Ratdctrl_ValueProvider(_);
                        return cfgrpuratd4_cfgdrpu_ratdctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd4_Cfgdrpu_Ratdctrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd4_Cfgdrpu_Ratdctrl_Read(_, __),
                    name: "Ratdctrl")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Cfgrpuratd4_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd4_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd6_Cfgdrpu - Offset : 0x630
        protected DoubleWordRegister  GenerateCfgrpuratd6_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgrpuratd6_cfgdrpu_ratdramrm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd6_Cfgdrpu_Ratdramrm_ValueProvider(_);
                        return cfgrpuratd6_cfgdrpu_ratdramrm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramrm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramrm_Read(_, __),
                    name: "Ratdramrm")
            .WithFlag(1, out cfgrpuratd6_cfgdrpu_ratdramwm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd6_Cfgdrpu_Ratdramwm_ValueProvider(_);
                        return cfgrpuratd6_cfgdrpu_ratdramwm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramwm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramwm_Read(_, __),
                    name: "Ratdramwm")
            .WithFlag(2, out cfgrpuratd6_cfgdrpu_ratdramra_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd6_Cfgdrpu_Ratdramra_ValueProvider(_);
                        return cfgrpuratd6_cfgdrpu_ratdramra_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramra_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramra_Read(_, __),
                    name: "Ratdramra")
            .WithFlag(3, out cfgrpuratd6_cfgdrpu_ratdrambiasconf_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd6_Cfgdrpu_Ratdrambiasconf_ValueProvider(_);
                        return cfgrpuratd6_cfgdrpu_ratdrambiasconf_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdrambiasconf_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdrambiasconf_Read(_, __),
                    name: "Ratdrambiasconf")
            .WithFlag(4, out cfgrpuratd6_cfgdrpu_ratdramlvtest_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd6_Cfgdrpu_Ratdramlvtest_ValueProvider(_);
                        return cfgrpuratd6_cfgdrpu_ratdramlvtest_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramlvtest_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd6_Cfgdrpu_Ratdramlvtest_Read(_, __),
                    name: "Ratdramlvtest")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Cfgrpuratd6_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd6_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd8_Cfgdrpu - Offset : 0x638
        protected DoubleWordRegister  GenerateCfgrpuratd8_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgrpuratd8_cfgdrpu_ratdram1prm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdram1prm_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdram1prm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdram1prm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdram1prm_Read(_, __),
                    name: "Ratdram1prm")
            .WithFlag(1, out cfgrpuratd8_cfgdrpu_ratdreg1prm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdreg1prm_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdreg1prm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdreg1prm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdreg1prm_Read(_, __),
                    name: "Ratdreg1prm")
            .WithFlag(2, out cfgrpuratd8_cfgdrpu_ratdram1pwm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdram1pwm_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdram1pwm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdram1pwm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdram1pwm_Read(_, __),
                    name: "Ratdram1pwm")
            .WithFlag(3, out cfgrpuratd8_cfgdrpu_ratdreg1pwm_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdreg1pwm_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdreg1pwm_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdreg1pwm_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdreg1pwm_Read(_, __),
                    name: "Ratdreg1pwm")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Cfgrpuratd8_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd8_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd12_Cfgdrpu - Offset : 0x648
        protected DoubleWordRegister  GenerateCfgrpuratd12_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgrpuratd12_cfgdrpu_ratdrootdata0_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd12_Cfgdrpu_Ratdrootdata0_ValueProvider(_);
                        return cfgrpuratd12_cfgdrpu_ratdrootdata0_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootdata0_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootdata0_Read(_, __),
                    name: "Ratdrootdata0")
            .WithFlag(1, out cfgrpuratd12_cfgdrpu_ratdrootdata1_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd12_Cfgdrpu_Ratdrootdata1_ValueProvider(_);
                        return cfgrpuratd12_cfgdrpu_ratdrootdata1_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootdata1_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootdata1_Read(_, __),
                    name: "Ratdrootdata1")
            .WithReservedBits(2, 1)
            .WithFlag(3, out cfgrpuratd12_cfgdrpu_ratdrootseswversion_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd12_Cfgdrpu_Ratdrootseswversion_ValueProvider(_);
                        return cfgrpuratd12_cfgdrpu_ratdrootseswversion_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootseswversion_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootseswversion_Read(_, __),
                    name: "Ratdrootseswversion")
            .WithFlag(4, out cfgrpuratd12_cfgdrpu_ratdrootserootinfo_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd12_Cfgdrpu_Ratdrootserootinfo_ValueProvider(_);
                        return cfgrpuratd12_cfgdrpu_ratdrootserootinfo_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootserootinfo_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd12_Cfgdrpu_Ratdrootserootinfo_Read(_, __),
                    name: "Ratdrootserootinfo")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Cfgrpuratd12_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd12_Cfgdrpu_Write(_, __));
        

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
        protected IFlagRegisterField if_tealsysif_bit;
        partial void If_Tealsysif_Write(bool a, bool b);
        partial void If_Tealsysif_Read(bool a, bool b);
        partial void If_Tealsysif_ValueProvider(bool a);
        protected IFlagRegisterField if_l1cache0if_bit;
        partial void If_L1cache0if_Write(bool a, bool b);
        partial void If_L1cache0if_Read(bool a, bool b);
        partial void If_L1cache0if_ValueProvider(bool a);

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
        protected IFlagRegisterField ien_tealsysien_bit;
        partial void Ien_Tealsysien_Write(bool a, bool b);
        partial void Ien_Tealsysien_Read(bool a, bool b);
        partial void Ien_Tealsysien_ValueProvider(bool a);
        protected IFlagRegisterField ien_l1cache0ien_bit;
        partial void Ien_L1cache0ien_Write(bool a, bool b);
        partial void Ien_L1cache0ien_Read(bool a, bool b);
        partial void Ien_L1cache0ien_ValueProvider(bool a);

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
        protected IValueRegisterField chiprevhw_variant_field;
        partial void Chiprevhw_Variant_Read(ulong a, ulong b);
        partial void Chiprevhw_Variant_ValueProvider(ulong a);

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
        protected IValueRegisterField fpgaipothw_fpgaipo_field;
        partial void Fpgaipothw_Fpgaipo_Read(ulong a, ulong b);
        partial void Fpgaipothw_Fpgaipo_ValueProvider(ulong a);

        partial void Fpgaipothw_Write(uint a, uint b);
        partial void Fpgaipothw_Read(uint a, uint b);
        
        // Romrevhw - Offset : 0x34
        protected IValueRegisterField romrevhw_seromrev_field;
        partial void Romrevhw_Seromrev_Read(ulong a, ulong b);
        partial void Romrevhw_Seromrev_ValueProvider(ulong a);
        protected IValueRegisterField romrevhw_secompatid_field;
        partial void Romrevhw_Secompatid_Read(ulong a, ulong b);
        partial void Romrevhw_Secompatid_ValueProvider(ulong a);

        partial void Romrevhw_Write(uint a, uint b);
        partial void Romrevhw_Read(uint a, uint b);
        
        // Sesysromrm_Rom - Offset : 0x100
        protected IValueRegisterField sesysromrm_rom_sesysromrm_field;
        partial void Sesysromrm_Rom_Sesysromrm_Write(ulong a, ulong b);
        partial void Sesysromrm_Rom_Sesysromrm_Read(ulong a, ulong b);
        partial void Sesysromrm_Rom_Sesysromrm_ValueProvider(ulong a);

        partial void Sesysromrm_Rom_Write(uint a, uint b);
        partial void Sesysromrm_Rom_Read(uint a, uint b);
        
        // Sepkeromrm_Rom - Offset : 0x104
        protected IValueRegisterField sepkeromrm_rom_sepkeromrm_field;
        partial void Sepkeromrm_Rom_Sepkeromrm_Write(ulong a, ulong b);
        partial void Sepkeromrm_Rom_Sepkeromrm_Read(ulong a, ulong b);
        partial void Sepkeromrm_Rom_Sepkeromrm_ValueProvider(ulong a);

        partial void Sepkeromrm_Rom_Write(uint a, uint b);
        partial void Sepkeromrm_Rom_Read(uint a, uint b);
        
        // Sesysctrl_Rom - Offset : 0x108
        protected IFlagRegisterField sesysctrl_rom_sesysromtest1_bit;
        partial void Sesysctrl_Rom_Sesysromtest1_Write(bool a, bool b);
        partial void Sesysctrl_Rom_Sesysromtest1_Read(bool a, bool b);
        partial void Sesysctrl_Rom_Sesysromtest1_ValueProvider(bool a);
        protected IFlagRegisterField sesysctrl_rom_sesysromrme_bit;
        partial void Sesysctrl_Rom_Sesysromrme_Write(bool a, bool b);
        partial void Sesysctrl_Rom_Sesysromrme_Read(bool a, bool b);
        partial void Sesysctrl_Rom_Sesysromrme_ValueProvider(bool a);

        partial void Sesysctrl_Rom_Write(uint a, uint b);
        partial void Sesysctrl_Rom_Read(uint a, uint b);
        
        // Sepkectrl_Rom - Offset : 0x10C
        protected IFlagRegisterField sepkectrl_rom_sepkeromtest1_bit;
        partial void Sepkectrl_Rom_Sepkeromtest1_Write(bool a, bool b);
        partial void Sepkectrl_Rom_Sepkeromtest1_Read(bool a, bool b);
        partial void Sepkectrl_Rom_Sepkeromtest1_ValueProvider(bool a);
        protected IFlagRegisterField sepkectrl_rom_sepkeromrme_bit;
        partial void Sepkectrl_Rom_Sepkeromrme_Write(bool a, bool b);
        partial void Sepkectrl_Rom_Sepkeromrme_Read(bool a, bool b);
        partial void Sepkectrl_Rom_Sepkeromrme_ValueProvider(bool a);

        partial void Sepkectrl_Rom_Write(uint a, uint b);
        partial void Sepkectrl_Rom_Read(uint a, uint b);
        
        // Ctrl_Ram - Offset : 0x200
        protected IFlagRegisterField ctrl_ram_addrfaulten_bit;
        partial void Ctrl_Ram_Addrfaulten_Write(bool a, bool b);
        partial void Ctrl_Ram_Addrfaulten_Read(bool a, bool b);
        partial void Ctrl_Ram_Addrfaulten_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_ram_clkdisfaulten_bit;
        partial void Ctrl_Ram_Clkdisfaulten_Write(bool a, bool b);
        partial void Ctrl_Ram_Clkdisfaulten_Read(bool a, bool b);
        partial void Ctrl_Ram_Clkdisfaulten_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_ram_rameccerrfaulten_bit;
        partial void Ctrl_Ram_Rameccerrfaulten_Write(bool a, bool b);
        partial void Ctrl_Ram_Rameccerrfaulten_Read(bool a, bool b);
        partial void Ctrl_Ram_Rameccerrfaulten_ValueProvider(bool a);

        partial void Ctrl_Ram_Write(uint a, uint b);
        partial void Ctrl_Ram_Read(uint a, uint b);
        
        // Ramrm_Ram - Offset : 0x300
        protected IValueRegisterField ramrm_ram_ramrm0_field;
        partial void Ramrm_Ram_Ramrm0_Write(ulong a, ulong b);
        partial void Ramrm_Ram_Ramrm0_Read(ulong a, ulong b);
        partial void Ramrm_Ram_Ramrm0_ValueProvider(ulong a);
        protected IValueRegisterField ramrm_ram_ramrm1_field;
        partial void Ramrm_Ram_Ramrm1_Write(ulong a, ulong b);
        partial void Ramrm_Ram_Ramrm1_Read(ulong a, ulong b);
        partial void Ramrm_Ram_Ramrm1_ValueProvider(ulong a);
        protected IValueRegisterField ramrm_ram_ramrm2_field;
        partial void Ramrm_Ram_Ramrm2_Write(ulong a, ulong b);
        partial void Ramrm_Ram_Ramrm2_Read(ulong a, ulong b);
        partial void Ramrm_Ram_Ramrm2_ValueProvider(ulong a);

        partial void Ramrm_Ram_Write(uint a, uint b);
        partial void Ramrm_Ram_Read(uint a, uint b);
        
        // Ramwm_Ram - Offset : 0x304
        protected IValueRegisterField ramwm_ram_ramwm0_field;
        partial void Ramwm_Ram_Ramwm0_Write(ulong a, ulong b);
        partial void Ramwm_Ram_Ramwm0_Read(ulong a, ulong b);
        partial void Ramwm_Ram_Ramwm0_ValueProvider(ulong a);
        protected IValueRegisterField ramwm_ram_ramwm1_field;
        partial void Ramwm_Ram_Ramwm1_Write(ulong a, ulong b);
        partial void Ramwm_Ram_Ramwm1_Read(ulong a, ulong b);
        partial void Ramwm_Ram_Ramwm1_ValueProvider(ulong a);
        protected IValueRegisterField ramwm_ram_ramwm2_field;
        partial void Ramwm_Ram_Ramwm2_Write(ulong a, ulong b);
        partial void Ramwm_Ram_Ramwm2_Read(ulong a, ulong b);
        partial void Ramwm_Ram_Ramwm2_ValueProvider(ulong a);

        partial void Ramwm_Ram_Write(uint a, uint b);
        partial void Ramwm_Ram_Read(uint a, uint b);
        
        // Ramra_Ram - Offset : 0x308
        protected IFlagRegisterField ramra_ram_ramra0_bit;
        partial void Ramra_Ram_Ramra0_Write(bool a, bool b);
        partial void Ramra_Ram_Ramra0_Read(bool a, bool b);
        partial void Ramra_Ram_Ramra0_ValueProvider(bool a);
        protected IFlagRegisterField ramra_ram_ramra1_bit;
        partial void Ramra_Ram_Ramra1_Write(bool a, bool b);
        partial void Ramra_Ram_Ramra1_Read(bool a, bool b);
        partial void Ramra_Ram_Ramra1_ValueProvider(bool a);
        protected IFlagRegisterField ramra_ram_ramra2_bit;
        partial void Ramra_Ram_Ramra2_Write(bool a, bool b);
        partial void Ramra_Ram_Ramra2_Read(bool a, bool b);
        partial void Ramra_Ram_Ramra2_ValueProvider(bool a);

        partial void Ramra_Ram_Write(uint a, uint b);
        partial void Ramra_Ram_Read(uint a, uint b);
        
        // Rambiasconf_Ram - Offset : 0x30C
        protected IEnumRegisterField<RAMBIASCONF_RAM_RAMBIASCTRL> rambiasconf_ram_rambiasctrl_field;
        partial void Rambiasconf_Ram_Rambiasctrl_Write(RAMBIASCONF_RAM_RAMBIASCTRL a, RAMBIASCONF_RAM_RAMBIASCTRL b);
        partial void Rambiasconf_Ram_Rambiasctrl_Read(RAMBIASCONF_RAM_RAMBIASCTRL a, RAMBIASCONF_RAM_RAMBIASCTRL b);
        partial void Rambiasconf_Ram_Rambiasctrl_ValueProvider(RAMBIASCONF_RAM_RAMBIASCTRL a);

        partial void Rambiasconf_Ram_Write(uint a, uint b);
        partial void Rambiasconf_Ram_Read(uint a, uint b);
        
        // Ramlvtest_Ram - Offset : 0x310
        protected IFlagRegisterField ramlvtest_ram_ramlvtest_bit;
        partial void Ramlvtest_Ram_Ramlvtest_Write(bool a, bool b);
        partial void Ramlvtest_Ram_Ramlvtest_Read(bool a, bool b);
        partial void Ramlvtest_Ram_Ramlvtest_ValueProvider(bool a);

        partial void Ramlvtest_Ram_Write(uint a, uint b);
        partial void Ramlvtest_Ram_Read(uint a, uint b);
        
        // Ram1prm_Ram - Offset : 0x400
        protected IValueRegisterField ram1prm_ram_s4096x39_field;
        partial void Ram1prm_Ram_S4096x39_Write(ulong a, ulong b);
        partial void Ram1prm_Ram_S4096x39_Read(ulong a, ulong b);
        partial void Ram1prm_Ram_S4096x39_ValueProvider(ulong a);
        protected IValueRegisterField ram1prm_ram_s1024x34_field;
        partial void Ram1prm_Ram_S1024x34_Write(ulong a, ulong b);
        partial void Ram1prm_Ram_S1024x34_Read(ulong a, ulong b);
        partial void Ram1prm_Ram_S1024x34_ValueProvider(ulong a);
        protected IValueRegisterField ram1prm_ram_s2048x34_field;
        partial void Ram1prm_Ram_S2048x34_Write(ulong a, ulong b);
        partial void Ram1prm_Ram_S2048x34_Read(ulong a, ulong b);
        partial void Ram1prm_Ram_S2048x34_ValueProvider(ulong a);
        protected IValueRegisterField ram1prm_ram_s1024x14_field;
        partial void Ram1prm_Ram_S1024x14_Write(ulong a, ulong b);
        partial void Ram1prm_Ram_S1024x14_Read(ulong a, ulong b);
        partial void Ram1prm_Ram_S1024x14_ValueProvider(ulong a);
        protected IValueRegisterField ram1prm_ram_s2048x39_field;
        partial void Ram1prm_Ram_S2048x39_Write(ulong a, ulong b);
        partial void Ram1prm_Ram_S2048x39_Read(ulong a, ulong b);
        partial void Ram1prm_Ram_S2048x39_ValueProvider(ulong a);
        protected IValueRegisterField ram1prm_ram_s1024x39_field;
        partial void Ram1prm_Ram_S1024x39_Write(ulong a, ulong b);
        partial void Ram1prm_Ram_S1024x39_Read(ulong a, ulong b);
        partial void Ram1prm_Ram_S1024x39_ValueProvider(ulong a);

        partial void Ram1prm_Ram_Write(uint a, uint b);
        partial void Ram1prm_Ram_Read(uint a, uint b);
        
        // Reg1prm_Ram - Offset : 0x404
        protected IValueRegisterField reg1prm_ram_s512x50_field;
        partial void Reg1prm_Ram_S512x50_Write(ulong a, ulong b);
        partial void Reg1prm_Ram_S512x50_Read(ulong a, ulong b);
        partial void Reg1prm_Ram_S512x50_ValueProvider(ulong a);
        protected IValueRegisterField reg1prm_ram_s40x138_field;
        partial void Reg1prm_Ram_S40x138_Write(ulong a, ulong b);
        partial void Reg1prm_Ram_S40x138_Read(ulong a, ulong b);
        partial void Reg1prm_Ram_S40x138_ValueProvider(ulong a);

        partial void Reg1prm_Ram_Write(uint a, uint b);
        partial void Reg1prm_Ram_Read(uint a, uint b);
        
        // Ram1pwm_Ram - Offset : 0x408
        protected IValueRegisterField ram1pwm_ram_s4096x39_field;
        partial void Ram1pwm_Ram_S4096x39_Write(ulong a, ulong b);
        partial void Ram1pwm_Ram_S4096x39_Read(ulong a, ulong b);
        partial void Ram1pwm_Ram_S4096x39_ValueProvider(ulong a);
        protected IValueRegisterField ram1pwm_ram_s1024x34_field;
        partial void Ram1pwm_Ram_S1024x34_Write(ulong a, ulong b);
        partial void Ram1pwm_Ram_S1024x34_Read(ulong a, ulong b);
        partial void Ram1pwm_Ram_S1024x34_ValueProvider(ulong a);
        protected IValueRegisterField ram1pwm_ram_s2048x34_field;
        partial void Ram1pwm_Ram_S2048x34_Write(ulong a, ulong b);
        partial void Ram1pwm_Ram_S2048x34_Read(ulong a, ulong b);
        partial void Ram1pwm_Ram_S2048x34_ValueProvider(ulong a);
        protected IValueRegisterField ram1pwm_ram_s1024x14_field;
        partial void Ram1pwm_Ram_S1024x14_Write(ulong a, ulong b);
        partial void Ram1pwm_Ram_S1024x14_Read(ulong a, ulong b);
        partial void Ram1pwm_Ram_S1024x14_ValueProvider(ulong a);
        protected IValueRegisterField ram1pwm_ram_s2048x39_field;
        partial void Ram1pwm_Ram_S2048x39_Write(ulong a, ulong b);
        partial void Ram1pwm_Ram_S2048x39_Read(ulong a, ulong b);
        partial void Ram1pwm_Ram_S2048x39_ValueProvider(ulong a);
        protected IValueRegisterField ram1pwm_ram_s1024x39_field;
        partial void Ram1pwm_Ram_S1024x39_Write(ulong a, ulong b);
        partial void Ram1pwm_Ram_S1024x39_Read(ulong a, ulong b);
        partial void Ram1pwm_Ram_S1024x39_ValueProvider(ulong a);

        partial void Ram1pwm_Ram_Write(uint a, uint b);
        partial void Ram1pwm_Ram_Read(uint a, uint b);
        
        // Reg1pwm_Ram - Offset : 0x40C
        protected IValueRegisterField reg1pwm_ram_s512x50_field;
        partial void Reg1pwm_Ram_S512x50_Write(ulong a, ulong b);
        partial void Reg1pwm_Ram_S512x50_Read(ulong a, ulong b);
        partial void Reg1pwm_Ram_S512x50_ValueProvider(ulong a);
        protected IValueRegisterField reg1pwm_ram_s40x138_field;
        partial void Reg1pwm_Ram_S40x138_Write(ulong a, ulong b);
        partial void Reg1pwm_Ram_S40x138_Read(ulong a, ulong b);
        partial void Reg1pwm_Ram_S40x138_ValueProvider(ulong a);

        partial void Reg1pwm_Ram_Write(uint a, uint b);
        partial void Reg1pwm_Ram_Read(uint a, uint b);
        
        // Data0 - Offset : 0x600
        protected IValueRegisterField data0_data_field;
        partial void Data0_Data_Write(ulong a, ulong b);
        partial void Data0_Data_Read(ulong a, ulong b);
        partial void Data0_Data_ValueProvider(ulong a);

        partial void Data0_Write(uint a, uint b);
        partial void Data0_Read(uint a, uint b);
        
        // Data1 - Offset : 0x604
        protected IValueRegisterField data1_data_field;
        partial void Data1_Data_Write(ulong a, ulong b);
        partial void Data1_Data_Read(ulong a, ulong b);
        partial void Data1_Data_ValueProvider(ulong a);

        partial void Data1_Write(uint a, uint b);
        partial void Data1_Read(uint a, uint b);
        
        // Lockstatus - Offset : 0x608
        protected IFlagRegisterField lockstatus_buslock_bit;
        partial void Lockstatus_Buslock_Read(bool a, bool b);
        partial void Lockstatus_Buslock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_reglock_bit;
        partial void Lockstatus_Reglock_Read(bool a, bool b);
        partial void Lockstatus_Reglock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_mfrlock_bit;
        partial void Lockstatus_Mfrlock_Read(bool a, bool b);
        partial void Lockstatus_Mfrlock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_rootdbglock_bit;
        partial void Lockstatus_Rootdbglock_Read(bool a, bool b);
        partial void Lockstatus_Rootdbglock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_userdbgaplock_bit;
        partial void Lockstatus_Userdbgaplock_Read(bool a, bool b);
        partial void Lockstatus_Userdbgaplock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_userdbglock_bit;
        partial void Lockstatus_Userdbglock_Read(bool a, bool b);
        partial void Lockstatus_Userdbglock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_usernidlock_bit;
        partial void Lockstatus_Usernidlock_Read(bool a, bool b);
        partial void Lockstatus_Usernidlock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_userspidlock_bit;
        partial void Lockstatus_Userspidlock_Read(bool a, bool b);
        partial void Lockstatus_Userspidlock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_userspnidlock_bit;
        partial void Lockstatus_Userspnidlock_Read(bool a, bool b);
        partial void Lockstatus_Userspnidlock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_lpwcpu0dbglock_bit;
        partial void Lockstatus_Lpwcpu0dbglock_Read(bool a, bool b);
        partial void Lockstatus_Lpwcpu0dbglock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_lpwcpu1dbglock_bit;
        partial void Lockstatus_Lpwcpu1dbglock_Read(bool a, bool b);
        partial void Lockstatus_Lpwcpu1dbglock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_efuseunlocked_bit;
        partial void Lockstatus_Efuseunlocked_Read(bool a, bool b);
        partial void Lockstatus_Efuseunlocked_ValueProvider(bool a);

        partial void Lockstatus_Write(uint a, uint b);
        partial void Lockstatus_Read(uint a, uint b);
        
        // Seswversion - Offset : 0x60C
        protected IValueRegisterField seswversion_swversion_field;
        partial void Seswversion_Swversion_Write(ulong a, ulong b);
        partial void Seswversion_Swversion_Read(ulong a, ulong b);
        partial void Seswversion_Swversion_ValueProvider(ulong a);

        partial void Seswversion_Write(uint a, uint b);
        partial void Seswversion_Read(uint a, uint b);
        
        // Serootinfo - Offset : 0x610
        protected IFlagRegisterField serootinfo_rootsecompromised_bit;
        partial void Serootinfo_Rootsecompromised_Write(bool a, bool b);
        partial void Serootinfo_Rootsecompromised_Read(bool a, bool b);
        partial void Serootinfo_Rootsecompromised_ValueProvider(bool a);
        protected IFlagRegisterField serootinfo_rootselifecycle0_bit;
        partial void Serootinfo_Rootselifecycle0_Write(bool a, bool b);
        partial void Serootinfo_Rootselifecycle0_Read(bool a, bool b);
        partial void Serootinfo_Rootselifecycle0_ValueProvider(bool a);
        protected IFlagRegisterField serootinfo_rootselifecycle1_bit;
        partial void Serootinfo_Rootselifecycle1_Write(bool a, bool b);
        partial void Serootinfo_Rootselifecycle1_Read(bool a, bool b);
        partial void Serootinfo_Rootselifecycle1_ValueProvider(bool a);
        protected IFlagRegisterField serootinfo_rootselifecycle2_bit;
        partial void Serootinfo_Rootselifecycle2_Write(bool a, bool b);
        partial void Serootinfo_Rootselifecycle2_Read(bool a, bool b);
        partial void Serootinfo_Rootselifecycle2_ValueProvider(bool a);

        partial void Serootinfo_Write(uint a, uint b);
        partial void Serootinfo_Read(uint a, uint b);
        
        // Serootinfostatus - Offset : 0x614
        protected IFlagRegisterField serootinfostatus_secompromised_bit;
        partial void Serootinfostatus_Secompromised_Read(bool a, bool b);
        partial void Serootinfostatus_Secompromised_ValueProvider(bool a);
        protected IFlagRegisterField serootinfostatus_selifecycle0_bit;
        partial void Serootinfostatus_Selifecycle0_Read(bool a, bool b);
        partial void Serootinfostatus_Selifecycle0_ValueProvider(bool a);
        protected IFlagRegisterField serootinfostatus_selifecycle1_bit;
        partial void Serootinfostatus_Selifecycle1_Read(bool a, bool b);
        partial void Serootinfostatus_Selifecycle1_ValueProvider(bool a);
        protected IFlagRegisterField serootinfostatus_selifecycle2_bit;
        partial void Serootinfostatus_Selifecycle2_Read(bool a, bool b);
        partial void Serootinfostatus_Selifecycle2_ValueProvider(bool a);

        partial void Serootinfostatus_Write(uint a, uint b);
        partial void Serootinfostatus_Read(uint a, uint b);
        
        // Cfgrpuratd0_Cfgdrpu - Offset : 0x618
        protected IFlagRegisterField cfgrpuratd0_cfgdrpu_ratdif_bit;
        partial void Cfgrpuratd0_Cfgdrpu_Ratdif_Write(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdif_Read(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdif_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd0_cfgdrpu_ratdien_bit;
        partial void Cfgrpuratd0_Cfgdrpu_Ratdien_Write(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdien_Read(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdien_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd0_cfgdrpu_ratdchiprev_bit;
        partial void Cfgrpuratd0_Cfgdrpu_Ratdchiprev_Write(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdchiprev_Read(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdchiprev_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd0_cfgdrpu_ratdinstanceid_bit;
        partial void Cfgrpuratd0_Cfgdrpu_Ratdinstanceid_Write(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdinstanceid_Read(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdinstanceid_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd0_cfgdrpu_ratdcfgsstcalib_bit;
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgsstcalib_Write(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgsstcalib_Read(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgsstcalib_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd0_cfgdrpu_ratdcfgssystic_bit;
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgssystic_Write(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgssystic_Read(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgssystic_ValueProvider(bool a);

        partial void Cfgrpuratd0_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd0_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd2_Cfgdrpu - Offset : 0x620
        protected IFlagRegisterField cfgrpuratd2_cfgdrpu_ratdsesysromrm_bit;
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsesysromrm_Write(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsesysromrm_Read(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsesysromrm_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd2_cfgdrpu_ratdsepkeromrm_bit;
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsepkeromrm_Write(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsepkeromrm_Read(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsepkeromrm_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd2_cfgdrpu_ratdsesysctrl_bit;
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsesysctrl_Write(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsesysctrl_Read(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsesysctrl_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd2_cfgdrpu_ratdsepkectrl_bit;
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsepkectrl_Write(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsepkectrl_Read(bool a, bool b);
        partial void Cfgrpuratd2_Cfgdrpu_Ratdsepkectrl_ValueProvider(bool a);

        partial void Cfgrpuratd2_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd2_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd4_Cfgdrpu - Offset : 0x628
        protected IFlagRegisterField cfgrpuratd4_cfgdrpu_ratdctrl_bit;
        partial void Cfgrpuratd4_Cfgdrpu_Ratdctrl_Write(bool a, bool b);
        partial void Cfgrpuratd4_Cfgdrpu_Ratdctrl_Read(bool a, bool b);
        partial void Cfgrpuratd4_Cfgdrpu_Ratdctrl_ValueProvider(bool a);

        partial void Cfgrpuratd4_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd4_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd6_Cfgdrpu - Offset : 0x630
        protected IFlagRegisterField cfgrpuratd6_cfgdrpu_ratdramrm_bit;
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramrm_Write(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramrm_Read(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramrm_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd6_cfgdrpu_ratdramwm_bit;
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramwm_Write(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramwm_Read(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramwm_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd6_cfgdrpu_ratdramra_bit;
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramra_Write(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramra_Read(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramra_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd6_cfgdrpu_ratdrambiasconf_bit;
        partial void Cfgrpuratd6_Cfgdrpu_Ratdrambiasconf_Write(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdrambiasconf_Read(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdrambiasconf_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd6_cfgdrpu_ratdramlvtest_bit;
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramlvtest_Write(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramlvtest_Read(bool a, bool b);
        partial void Cfgrpuratd6_Cfgdrpu_Ratdramlvtest_ValueProvider(bool a);

        partial void Cfgrpuratd6_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd6_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd8_Cfgdrpu - Offset : 0x638
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdram1prm_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdram1prm_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdram1prm_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdram1prm_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdreg1prm_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdreg1prm_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdreg1prm_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdreg1prm_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdram1pwm_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdram1pwm_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdram1pwm_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdram1pwm_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdreg1pwm_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdreg1pwm_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdreg1pwm_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdreg1pwm_ValueProvider(bool a);

        partial void Cfgrpuratd8_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd8_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd12_Cfgdrpu - Offset : 0x648
        protected IFlagRegisterField cfgrpuratd12_cfgdrpu_ratdrootdata0_bit;
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootdata0_Write(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootdata0_Read(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootdata0_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd12_cfgdrpu_ratdrootdata1_bit;
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootdata1_Write(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootdata1_Read(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootdata1_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd12_cfgdrpu_ratdrootseswversion_bit;
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootseswversion_Write(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootseswversion_Read(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootseswversion_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd12_cfgdrpu_ratdrootserootinfo_bit;
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootserootinfo_Write(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootserootinfo_Read(bool a, bool b);
        partial void Cfgrpuratd12_Cfgdrpu_Ratdrootserootinfo_ValueProvider(bool a);

        partial void Cfgrpuratd12_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd12_Cfgdrpu_Read(uint a, uint b);
        
        partial void SYSCFG_Reset();

        partial void SiLabs_SYSCFG_10_Constructor();

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
            Romrevhw = 0x34,
            Sesysromrm_Rom = 0x100,
            Sepkeromrm_Rom = 0x104,
            Sesysctrl_Rom = 0x108,
            Sepkectrl_Rom = 0x10C,
            Ctrl_Ram = 0x200,
            Ramrm_Ram = 0x300,
            Ramwm_Ram = 0x304,
            Ramra_Ram = 0x308,
            Rambiasconf_Ram = 0x30C,
            Ramlvtest_Ram = 0x310,
            Ram1prm_Ram = 0x400,
            Reg1prm_Ram = 0x404,
            Ram1pwm_Ram = 0x408,
            Reg1pwm_Ram = 0x40C,
            Data0 = 0x600,
            Data1 = 0x604,
            Lockstatus = 0x608,
            Seswversion = 0x60C,
            Serootinfo = 0x610,
            Serootinfostatus = 0x614,
            Cfgrpuratd0_Cfgdrpu = 0x618,
            Cfgrpuratd2_Cfgdrpu = 0x620,
            Cfgrpuratd4_Cfgdrpu = 0x628,
            Cfgrpuratd6_Cfgdrpu = 0x630,
            Cfgrpuratd8_Cfgdrpu = 0x638,
            Cfgrpuratd12_Cfgdrpu = 0x648,
            
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
            Romrevhw_SET = 0x1034,
            Sesysromrm_Rom_SET = 0x1100,
            Sepkeromrm_Rom_SET = 0x1104,
            Sesysctrl_Rom_SET = 0x1108,
            Sepkectrl_Rom_SET = 0x110C,
            Ctrl_Ram_SET = 0x1200,
            Ramrm_Ram_SET = 0x1300,
            Ramwm_Ram_SET = 0x1304,
            Ramra_Ram_SET = 0x1308,
            Rambiasconf_Ram_SET = 0x130C,
            Ramlvtest_Ram_SET = 0x1310,
            Ram1prm_Ram_SET = 0x1400,
            Reg1prm_Ram_SET = 0x1404,
            Ram1pwm_Ram_SET = 0x1408,
            Reg1pwm_Ram_SET = 0x140C,
            Data0_SET = 0x1600,
            Data1_SET = 0x1604,
            Lockstatus_SET = 0x1608,
            Seswversion_SET = 0x160C,
            Serootinfo_SET = 0x1610,
            Serootinfostatus_SET = 0x1614,
            Cfgrpuratd0_Cfgdrpu_SET = 0x1618,
            Cfgrpuratd2_Cfgdrpu_SET = 0x1620,
            Cfgrpuratd4_Cfgdrpu_SET = 0x1628,
            Cfgrpuratd6_Cfgdrpu_SET = 0x1630,
            Cfgrpuratd8_Cfgdrpu_SET = 0x1638,
            Cfgrpuratd12_Cfgdrpu_SET = 0x1648,
            
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
            Romrevhw_CLR = 0x2034,
            Sesysromrm_Rom_CLR = 0x2100,
            Sepkeromrm_Rom_CLR = 0x2104,
            Sesysctrl_Rom_CLR = 0x2108,
            Sepkectrl_Rom_CLR = 0x210C,
            Ctrl_Ram_CLR = 0x2200,
            Ramrm_Ram_CLR = 0x2300,
            Ramwm_Ram_CLR = 0x2304,
            Ramra_Ram_CLR = 0x2308,
            Rambiasconf_Ram_CLR = 0x230C,
            Ramlvtest_Ram_CLR = 0x2310,
            Ram1prm_Ram_CLR = 0x2400,
            Reg1prm_Ram_CLR = 0x2404,
            Ram1pwm_Ram_CLR = 0x2408,
            Reg1pwm_Ram_CLR = 0x240C,
            Data0_CLR = 0x2600,
            Data1_CLR = 0x2604,
            Lockstatus_CLR = 0x2608,
            Seswversion_CLR = 0x260C,
            Serootinfo_CLR = 0x2610,
            Serootinfostatus_CLR = 0x2614,
            Cfgrpuratd0_Cfgdrpu_CLR = 0x2618,
            Cfgrpuratd2_Cfgdrpu_CLR = 0x2620,
            Cfgrpuratd4_Cfgdrpu_CLR = 0x2628,
            Cfgrpuratd6_Cfgdrpu_CLR = 0x2630,
            Cfgrpuratd8_Cfgdrpu_CLR = 0x2638,
            Cfgrpuratd12_Cfgdrpu_CLR = 0x2648,
            
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
            Romrevhw_TGL = 0x3034,
            Sesysromrm_Rom_TGL = 0x3100,
            Sepkeromrm_Rom_TGL = 0x3104,
            Sesysctrl_Rom_TGL = 0x3108,
            Sepkectrl_Rom_TGL = 0x310C,
            Ctrl_Ram_TGL = 0x3200,
            Ramrm_Ram_TGL = 0x3300,
            Ramwm_Ram_TGL = 0x3304,
            Ramra_Ram_TGL = 0x3308,
            Rambiasconf_Ram_TGL = 0x330C,
            Ramlvtest_Ram_TGL = 0x3310,
            Ram1prm_Ram_TGL = 0x3400,
            Reg1prm_Ram_TGL = 0x3404,
            Ram1pwm_Ram_TGL = 0x3408,
            Reg1pwm_Ram_TGL = 0x340C,
            Data0_TGL = 0x3600,
            Data1_TGL = 0x3604,
            Lockstatus_TGL = 0x3608,
            Seswversion_TGL = 0x360C,
            Serootinfo_TGL = 0x3610,
            Serootinfostatus_TGL = 0x3614,
            Cfgrpuratd0_Cfgdrpu_TGL = 0x3618,
            Cfgrpuratd2_Cfgdrpu_TGL = 0x3620,
            Cfgrpuratd4_Cfgdrpu_TGL = 0x3628,
            Cfgrpuratd6_Cfgdrpu_TGL = 0x3630,
            Cfgrpuratd8_Cfgdrpu_TGL = 0x3638,
            Cfgrpuratd12_Cfgdrpu_TGL = 0x3648,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}