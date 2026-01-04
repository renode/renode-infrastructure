//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    SYSCFG, Generated on : 2023-07-20 14:28:48.943273
    SYSCFG, ID Version : 8502eff413b04f7b9fdc7a6f39981e53.3 */

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
    public partial class EFR32xG2_SYSCFG_3
    {
        public EFR32xG2_SYSCFG_3(Machine machine) : base(machine)
        {
            EFR32xG2_SYSCFG_3_constructor();
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
    public partial class EFR32xG2_SYSCFG_3 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_SYSCFG_3(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_SYSCFG_3_Constructor();
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
                {(long)Registers.Cfgahbintercnct, GenerateCfgahbintercnctRegister()},
                {(long)Registers.Sesysromrm_Rom, GenerateSesysromrm_romRegister()},
                {(long)Registers.Sepkeromrm_Rom, GenerateSepkeromrm_romRegister()},
                {(long)Registers.Sesysctrl_Rom, GenerateSesysctrl_romRegister()},
                {(long)Registers.Sepkectrl_Rom, GenerateSepkectrl_romRegister()},
                {(long)Registers.Ctrl_Ram, GenerateCtrl_ramRegister()},
                {(long)Registers.Dmem0retnctrl_Ram, GenerateDmem0retnctrl_ramRegister()},
                {(long)Registers.Ramrm_Ram, GenerateRamrm_ramRegister()},
                {(long)Registers.Ramwm_Ram, GenerateRamwm_ramRegister()},
                {(long)Registers.Ramra_Ram, GenerateRamra_ramRegister()},
                {(long)Registers.Rambiasconf_Ram, GenerateRambiasconf_ramRegister()},
                {(long)Registers.Ramlvtest_Ram, GenerateRamlvtest_ramRegister()},
                {(long)Registers.Radioramretnctrl_Ram, GenerateRadioramretnctrl_ramRegister()},
                {(long)Registers.Radioramfeature_Ram, GenerateRadioramfeature_ramRegister()},
                {(long)Registers.Radioeccctrl_Ram, GenerateRadioeccctrl_ramRegister()},
                {(long)Registers.Seqrameccaddr_Ram, GenerateSeqrameccaddr_ramRegister()},
                {(long)Registers.Frcrameccaddr_Ram, GenerateFrcrameccaddr_ramRegister()},
                {(long)Registers.Icacheramretnctrl_Ram, GenerateIcacheramretnctrl_ramRegister()},
                {(long)Registers.Dmem0portmapsel_Ram, GenerateDmem0portmapsel_ramRegister()},
                {(long)Registers.Data0, GenerateData0Register()},
                {(long)Registers.Data1, GenerateData1Register()},
                {(long)Registers.Lockstatus, GenerateLockstatusRegister()},
                {(long)Registers.Seswversion, GenerateSeswversionRegister()},
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
        
        protected enum FPGAIPOTHW_FPGA
        {
            CHIP = 0, // The implementation is an ASIC view
            FPGA = 1, // The implementation is a FPGA view
        }
        
        protected enum FPGAIPOTHW_OTA
        {
            WIRED = 0, // This build does not support external radio PHY
            OTA = 1, // This build supports external radio PHY
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
        
        protected enum DMEM0RETNCTRL_RAM_RAMRETNCTRL
        {
            ALLON = 0, // None of the RAM blocks powered down
            BLK15 = 16384, // Power down RAM block 15 (address range 0x2003C000-0x20040000)
            BLK14TO15 = 24576, // Power down RAM blocks 14 and above (address range 0x20038000-0x20040000)
            BLK13TO15 = 28672, // Power down RAM blocks 13 and above (address range 0x20034000-0x20040000)
            BLK12TO15 = 30720, // Power down RAM blocks 12 and above (address range 0x20030000-0x20040000)
            BLK11TO15 = 31744, // Power down RAM blocks 11 and above (address range 0x2002C000-0x20040000)
            BLK10TO15 = 32256, // Power down RAM blocks 10 and above (address range 0x20028000-0x20040000)
            BLK9TO15 = 32512, // Power down RAM blocks 9 and above (address range 0x20024000-0x20040000)
            BLK8TO15 = 32640, // Power down RAM blocks 8 and above (address range 0x20020000-0x20040000)
            BLK7TO15 = 32704, // Power down RAM blocks 7 and above (address range 0x2001C000-0x20040000)
            BLK6TO15 = 32736, // Power down RAM blocks 6 and above (address range 0x20018000-0x20040000)
            BLK5TO15 = 32752, // Power down RAM blocks 5 and above (address range 0x20014000-0x20040000)
            BLK4TO15 = 32760, // Power down RAM blocks 4 and above (address range 0x20010000-0x20040000)
            BLK3TO15 = 32764, // Power down RAM blocks 3 and above (address range 0x2000C000-0x20040000)
            BLK2TO15 = 32766, // Power down RAM blocks 2 and above (address range 0x20008000-0x20040000)
            BLK1TO15 = 32767, // Power down RAM blocks 1 and above (address range 0x20004000-0x20040000)
        }
        
        protected enum RAMBIASCONF_RAM_RAMBIASCTRL
        {
            No = 0, // None
            VSB100 = 1, // Voltage Source Bias 100mV
            VSB200 = 2, // Voltage Source Bias 200mV
            VSB300 = 4, // Voltage Source Bias 300mV
            VSB400 = 8, // Voltage Source Bias 400mV
        }
        
        protected enum RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL
        {
            ALLON = 0, // SEQRAM not powered down
            BLK0 = 1, // Power down SEQRAM block 0
            BLK1 = 2, // Power down SEQRAM block 1
            ALLOFF = 3, // Power down all SEQRAM blocks
        }
        
        protected enum RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL
        {
            ALLON = 0, // FRCRAM not powered down
            ALLOFF = 1, // Power down FRCRAM
        }
        
        protected enum RADIORAMFEATURE_RAM_SEQRAMEN
        {
            NONE = 0, // Disable all sequencer ram blocks
            BLK0 = 1, // Enable sequencer ram block 0
            BLK1 = 2, // Enable sequencer ram block 1
            ALL = 3, // Enable all sequencer ram blocks
        }
        
        protected enum RADIORAMFEATURE_RAM_FRCRAMEN
        {
            NONE = 0, // Disable all FRC ram banks
            ALL = 1, // Enable all FRC ram banks
        }
        
        protected enum ICACHERAMRETNCTRL_RAM_RAMRETNCTRL
        {
            ALLON = 0, // None of the Host ICACHE RAM blocks powered down
            ALLOFF = 1, // Power down all Host ICACHE RAM blocks
        }
        
        // Ipversion - Offset : 0x4
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x3)
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
            .WithFlag(16, out if_host2srwbuserr_bit, 
                    valueProviderCallback: (_) => {
                        If_Host2srwbuserr_ValueProvider(_);
                        return if_host2srwbuserr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Host2srwbuserr_Write(_, __),
                    readCallback: (_, __) => If_Host2srwbuserr_Read(_, __),
                    name: "Host2srwbuserr")
            .WithFlag(17, out if_srw2hostbuserr_bit, 
                    valueProviderCallback: (_) => {
                        If_Srw2hostbuserr_ValueProvider(_);
                        return if_srw2hostbuserr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Srw2hostbuserr_Write(_, __),
                    readCallback: (_, __) => If_Srw2hostbuserr_Read(_, __),
                    name: "Srw2hostbuserr")
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
            .WithFlag(16, out ien_host2srwbuserr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Host2srwbuserr_ValueProvider(_);
                        return ien_host2srwbuserr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Host2srwbuserr_Write(_, __),
                    readCallback: (_, __) => Ien_Host2srwbuserr_Read(_, __),
                    name: "Host2srwbuserr")
            .WithFlag(17, out ien_srw2hostbuserr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Srw2hostbuserr_ValueProvider(_);
                        return ien_srw2hostbuserr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Srw2hostbuserr_Write(_, __),
                    readCallback: (_, __) => Ien_Srw2hostbuserr_Read(_, __),
                    name: "Srw2hostbuserr")
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
        protected DoubleWordRegister  GenerateChiprevhwRegister() => new DoubleWordRegister(this, 0xC01)
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
        
        // Chiprev - Offset : 0x18
        protected DoubleWordRegister  GenerateChiprevRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 6, out chiprev_major_field, 
                    valueProviderCallback: (_) => {
                        Chiprev_Major_ValueProvider(_);
                        return chiprev_major_field.Value;               
                    },
                    writeCallback: (_, __) => Chiprev_Major_Write(_, __),
                    readCallback: (_, __) => Chiprev_Major_Read(_, __),
                    name: "Major")
            .WithValueField(6, 6, out chiprev_family_field, 
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
        protected DoubleWordRegister  GenerateCfgahbintercnctRegister() => new DoubleWordRegister(this, 0x0)
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
        
        // Dmem0retnctrl_Ram - Offset : 0x208
        protected DoubleWordRegister  GenerateDmem0retnctrl_ramRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, DMEM0RETNCTRL_RAM_RAMRETNCTRL>(0, 15, out dmem0retnctrl_ram_ramretnctrl_field, 
                    valueProviderCallback: (_) => {
                        Dmem0retnctrl_Ram_Ramretnctrl_ValueProvider(_);
                        return dmem0retnctrl_ram_ramretnctrl_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0retnctrl_Ram_Ramretnctrl_Write(_, __),
                    readCallback: (_, __) => Dmem0retnctrl_Ram_Ramretnctrl_Read(_, __),
                    name: "Ramretnctrl")
            .WithReservedBits(15, 17)
            .WithReadCallback((_, __) => Dmem0retnctrl_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Dmem0retnctrl_Ram_Write(_, __));
        
        // Ramrm_Ram - Offset : 0x300
        protected DoubleWordRegister  GenerateRamrm_ramRegister() => new DoubleWordRegister(this, 0x70301)
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
        protected DoubleWordRegister  GenerateRamwm_ramRegister() => new DoubleWordRegister(this, 0x10307)
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
        
        // Radioramretnctrl_Ram - Offset : 0x400
        protected DoubleWordRegister  GenerateRadioramretnctrl_ramRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL>(0, 2, out radioramretnctrl_ram_seqramretnctrl_field, 
                    valueProviderCallback: (_) => {
                        Radioramretnctrl_Ram_Seqramretnctrl_ValueProvider(_);
                        return radioramretnctrl_ram_seqramretnctrl_field.Value;               
                    },
                    writeCallback: (_, __) => Radioramretnctrl_Ram_Seqramretnctrl_Write(_, __),
                    readCallback: (_, __) => Radioramretnctrl_Ram_Seqramretnctrl_Read(_, __),
                    name: "Seqramretnctrl")
            .WithReservedBits(2, 6)
            .WithEnumField<DoubleWordRegister, RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL>(8, 1, out radioramretnctrl_ram_frcramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Radioramretnctrl_Ram_Frcramretnctrl_ValueProvider(_);
                        return radioramretnctrl_ram_frcramretnctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioramretnctrl_Ram_Frcramretnctrl_Write(_, __),
                    readCallback: (_, __) => Radioramretnctrl_Ram_Frcramretnctrl_Read(_, __),
                    name: "Frcramretnctrl")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Radioramretnctrl_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Radioramretnctrl_Ram_Write(_, __));
        
        // Radioramfeature_Ram - Offset : 0x404
        protected DoubleWordRegister  GenerateRadioramfeature_ramRegister() => new DoubleWordRegister(this, 0x103)
            .WithEnumField<DoubleWordRegister, RADIORAMFEATURE_RAM_SEQRAMEN>(0, 2, out radioramfeature_ram_seqramen_field, 
                    valueProviderCallback: (_) => {
                        Radioramfeature_Ram_Seqramen_ValueProvider(_);
                        return radioramfeature_ram_seqramen_field.Value;               
                    },
                    writeCallback: (_, __) => Radioramfeature_Ram_Seqramen_Write(_, __),
                    readCallback: (_, __) => Radioramfeature_Ram_Seqramen_Read(_, __),
                    name: "Seqramen")
            .WithReservedBits(2, 6)
            .WithEnumField<DoubleWordRegister, RADIORAMFEATURE_RAM_FRCRAMEN>(8, 1, out radioramfeature_ram_frcramen_bit, 
                    valueProviderCallback: (_) => {
                        Radioramfeature_Ram_Frcramen_ValueProvider(_);
                        return radioramfeature_ram_frcramen_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioramfeature_Ram_Frcramen_Write(_, __),
                    readCallback: (_, __) => Radioramfeature_Ram_Frcramen_Read(_, __),
                    name: "Frcramen")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Radioramfeature_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Radioramfeature_Ram_Write(_, __));
        
        // Radioeccctrl_Ram - Offset : 0x408
        protected DoubleWordRegister  GenerateRadioeccctrl_ramRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out radioeccctrl_ram_seqrameccen_bit, 
                    valueProviderCallback: (_) => {
                        Radioeccctrl_Ram_Seqrameccen_ValueProvider(_);
                        return radioeccctrl_ram_seqrameccen_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioeccctrl_Ram_Seqrameccen_Write(_, __),
                    readCallback: (_, __) => Radioeccctrl_Ram_Seqrameccen_Read(_, __),
                    name: "Seqrameccen")
            .WithFlag(1, out radioeccctrl_ram_seqrameccewen_bit, 
                    valueProviderCallback: (_) => {
                        Radioeccctrl_Ram_Seqrameccewen_ValueProvider(_);
                        return radioeccctrl_ram_seqrameccewen_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioeccctrl_Ram_Seqrameccewen_Write(_, __),
                    readCallback: (_, __) => Radioeccctrl_Ram_Seqrameccewen_Read(_, __),
                    name: "Seqrameccewen")
            .WithReservedBits(2, 6)
            .WithFlag(8, out radioeccctrl_ram_frcrameccen_bit, 
                    valueProviderCallback: (_) => {
                        Radioeccctrl_Ram_Frcrameccen_ValueProvider(_);
                        return radioeccctrl_ram_frcrameccen_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioeccctrl_Ram_Frcrameccen_Write(_, __),
                    readCallback: (_, __) => Radioeccctrl_Ram_Frcrameccen_Read(_, __),
                    name: "Frcrameccen")
            .WithFlag(9, out radioeccctrl_ram_frcrameccewen_bit, 
                    valueProviderCallback: (_) => {
                        Radioeccctrl_Ram_Frcrameccewen_ValueProvider(_);
                        return radioeccctrl_ram_frcrameccewen_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioeccctrl_Ram_Frcrameccewen_Write(_, __),
                    readCallback: (_, __) => Radioeccctrl_Ram_Frcrameccewen_Read(_, __),
                    name: "Frcrameccewen")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Radioeccctrl_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Radioeccctrl_Ram_Write(_, __));
        
        // Seqrameccaddr_Ram - Offset : 0x410
        protected DoubleWordRegister  GenerateSeqrameccaddr_ramRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out seqrameccaddr_ram_seqrameccaddr_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Seqrameccaddr_Ram_Seqrameccaddr_ValueProvider(_);
                        return seqrameccaddr_ram_seqrameccaddr_field.Value;               
                    },
                    readCallback: (_, __) => Seqrameccaddr_Ram_Seqrameccaddr_Read(_, __),
                    name: "Seqrameccaddr")
            .WithReadCallback((_, __) => Seqrameccaddr_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Seqrameccaddr_Ram_Write(_, __));
        
        // Frcrameccaddr_Ram - Offset : 0x414
        protected DoubleWordRegister  GenerateFrcrameccaddr_ramRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out frcrameccaddr_ram_frcrameccaddr_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Frcrameccaddr_Ram_Frcrameccaddr_ValueProvider(_);
                        return frcrameccaddr_ram_frcrameccaddr_field.Value;               
                    },
                    readCallback: (_, __) => Frcrameccaddr_Ram_Frcrameccaddr_Read(_, __),
                    name: "Frcrameccaddr")
            .WithReadCallback((_, __) => Frcrameccaddr_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Frcrameccaddr_Ram_Write(_, __));
        
        // Icacheramretnctrl_Ram - Offset : 0x418
        protected DoubleWordRegister  GenerateIcacheramretnctrl_ramRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, ICACHERAMRETNCTRL_RAM_RAMRETNCTRL>(0, 1, out icacheramretnctrl_ram_ramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Icacheramretnctrl_Ram_Ramretnctrl_ValueProvider(_);
                        return icacheramretnctrl_ram_ramretnctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Icacheramretnctrl_Ram_Ramretnctrl_Write(_, __),
                    readCallback: (_, __) => Icacheramretnctrl_Ram_Ramretnctrl_Read(_, __),
                    name: "Ramretnctrl")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Icacheramretnctrl_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Icacheramretnctrl_Ram_Write(_, __));
        
        // Dmem0portmapsel_Ram - Offset : 0x41C
        protected DoubleWordRegister  GenerateDmem0portmapsel_ramRegister() => new DoubleWordRegister(this, 0x7905)
            .WithValueField(0, 2, out dmem0portmapsel_ram_ldmaportsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Ldmaportsel_ValueProvider(_);
                        return dmem0portmapsel_ram_ldmaportsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Ldmaportsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Ldmaportsel_Read(_, __),
                    name: "Ldmaportsel")
            .WithValueField(2, 2, out dmem0portmapsel_ram_srwaesportsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Srwaesportsel_ValueProvider(_);
                        return dmem0portmapsel_ram_srwaesportsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Srwaesportsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Srwaesportsel_Read(_, __),
                    name: "Srwaesportsel")
            .WithValueField(4, 2, out dmem0portmapsel_ram_ahbsrwportsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Ahbsrwportsel_ValueProvider(_);
                        return dmem0portmapsel_ram_ahbsrwportsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Ahbsrwportsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Ahbsrwportsel_Read(_, __),
                    name: "Ahbsrwportsel")
            .WithValueField(6, 2, out dmem0portmapsel_ram_srweca0portsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Srweca0portsel_ValueProvider(_);
                        return dmem0portmapsel_ram_srweca0portsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Srweca0portsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Srweca0portsel_Read(_, __),
                    name: "Srweca0portsel")
            .WithValueField(8, 2, out dmem0portmapsel_ram_srweca1portsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Srweca1portsel_ValueProvider(_);
                        return dmem0portmapsel_ram_srweca1portsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Srweca1portsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Srweca1portsel_Read(_, __),
                    name: "Srweca1portsel")
            .WithValueField(10, 2, out dmem0portmapsel_ram_mvpahbdata0portsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Mvpahbdata0portsel_ValueProvider(_);
                        return dmem0portmapsel_ram_mvpahbdata0portsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Mvpahbdata0portsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Mvpahbdata0portsel_Read(_, __),
                    name: "Mvpahbdata0portsel")
            .WithValueField(12, 2, out dmem0portmapsel_ram_mvpahbdata1portsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Mvpahbdata1portsel_ValueProvider(_);
                        return dmem0portmapsel_ram_mvpahbdata1portsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Mvpahbdata1portsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Mvpahbdata1portsel_Read(_, __),
                    name: "Mvpahbdata1portsel")
            .WithValueField(14, 2, out dmem0portmapsel_ram_mvpahbdata2portsel_field, 
                    valueProviderCallback: (_) => {
                        Dmem0portmapsel_Ram_Mvpahbdata2portsel_ValueProvider(_);
                        return dmem0portmapsel_ram_mvpahbdata2portsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dmem0portmapsel_Ram_Mvpahbdata2portsel_Write(_, __),
                    readCallback: (_, __) => Dmem0portmapsel_Ram_Mvpahbdata2portsel_Read(_, __),
                    name: "Mvpahbdata2portsel")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Dmem0portmapsel_Ram_Read(_, __))
            .WithWriteCallback((_, __) => Dmem0portmapsel_Ram_Write(_, __));
        
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
            .WithFlag(21, out lockstatus_radioidbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Radioidbglock_ValueProvider(_);
                        return lockstatus_radioidbglock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Radioidbglock_Read(_, __),
                    name: "Radioidbglock")
            .WithFlag(22, out lockstatus_radionidbglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Radionidbglock_ValueProvider(_);
                        return lockstatus_radionidbglock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Radionidbglock_Read(_, __),
                    name: "Radionidbglock")
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
        
        // Cfgrpuratd0_Cfgdrpu - Offset : 0x610
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
            .WithReservedBits(10, 3)
            .WithFlag(13, out cfgrpuratd0_cfgdrpu_ratdcfgahbintercnct_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd0_Cfgdrpu_Ratdcfgahbintercnct_ValueProvider(_);
                        return cfgrpuratd0_cfgdrpu_ratdcfgahbintercnct_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdcfgahbintercnct_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd0_Cfgdrpu_Ratdcfgahbintercnct_Read(_, __),
                    name: "Ratdcfgahbintercnct")
            .WithReservedBits(14, 18)
            .WithReadCallback((_, __) => Cfgrpuratd0_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd0_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd2_Cfgdrpu - Offset : 0x618
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
        
        // Cfgrpuratd4_Cfgdrpu - Offset : 0x620
        protected DoubleWordRegister  GenerateCfgrpuratd4_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgrpuratd4_cfgdrpu_ratdctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd4_Cfgdrpu_Ratdctrl_ValueProvider(_);
                        return cfgrpuratd4_cfgdrpu_ratdctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd4_Cfgdrpu_Ratdctrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd4_Cfgdrpu_Ratdctrl_Read(_, __),
                    name: "Ratdctrl")
            .WithReservedBits(1, 1)
            .WithFlag(2, out cfgrpuratd4_cfgdrpu_ratddmem0retnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd4_Cfgdrpu_Ratddmem0retnctrl_ValueProvider(_);
                        return cfgrpuratd4_cfgdrpu_ratddmem0retnctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd4_Cfgdrpu_Ratddmem0retnctrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd4_Cfgdrpu_Ratddmem0retnctrl_Read(_, __),
                    name: "Ratddmem0retnctrl")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Cfgrpuratd4_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd4_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd6_Cfgdrpu - Offset : 0x628
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
        
        // Cfgrpuratd8_Cfgdrpu - Offset : 0x630
        protected DoubleWordRegister  GenerateCfgrpuratd8_cfgdrpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfgrpuratd8_cfgdrpu_ratdradioramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdradioramretnctrl_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdradioramretnctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdradioramretnctrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdradioramretnctrl_Read(_, __),
                    name: "Ratdradioramretnctrl")
            .WithFlag(1, out cfgrpuratd8_cfgdrpu_ratdradioramfeature_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdradioramfeature_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdradioramfeature_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdradioramfeature_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdradioramfeature_Read(_, __),
                    name: "Ratdradioramfeature")
            .WithFlag(2, out cfgrpuratd8_cfgdrpu_ratdradioeccctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdradioeccctrl_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdradioeccctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdradioeccctrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdradioeccctrl_Read(_, __),
                    name: "Ratdradioeccctrl")
            .WithReservedBits(3, 3)
            .WithFlag(6, out cfgrpuratd8_cfgdrpu_ratdicacheramretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratdicacheramretnctrl_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratdicacheramretnctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdicacheramretnctrl_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratdicacheramretnctrl_Read(_, __),
                    name: "Ratdicacheramretnctrl")
            .WithFlag(7, out cfgrpuratd8_cfgdrpu_ratddmem0portmapsel_bit, 
                    valueProviderCallback: (_) => {
                        Cfgrpuratd8_Cfgdrpu_Ratddmem0portmapsel_ValueProvider(_);
                        return cfgrpuratd8_cfgdrpu_ratddmem0portmapsel_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratddmem0portmapsel_Write(_, __),
                    readCallback: (_, __) => Cfgrpuratd8_Cfgdrpu_Ratddmem0portmapsel_Read(_, __),
                    name: "Ratddmem0portmapsel")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Cfgrpuratd8_Cfgdrpu_Read(_, __))
            .WithWriteCallback((_, __) => Cfgrpuratd8_Cfgdrpu_Write(_, __));
        
        // Cfgrpuratd12_Cfgdrpu - Offset : 0x640
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
            .WithReservedBits(4, 28)
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
        protected IFlagRegisterField if_host2srwbuserr_bit;
        partial void If_Host2srwbuserr_Write(bool a, bool b);
        partial void If_Host2srwbuserr_Read(bool a, bool b);
        partial void If_Host2srwbuserr_ValueProvider(bool a);
        protected IFlagRegisterField if_srw2hostbuserr_bit;
        partial void If_Srw2hostbuserr_Write(bool a, bool b);
        partial void If_Srw2hostbuserr_Read(bool a, bool b);
        partial void If_Srw2hostbuserr_ValueProvider(bool a);
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
        protected IFlagRegisterField ien_host2srwbuserr_bit;
        partial void Ien_Host2srwbuserr_Write(bool a, bool b);
        partial void Ien_Host2srwbuserr_Read(bool a, bool b);
        partial void Ien_Host2srwbuserr_ValueProvider(bool a);
        protected IFlagRegisterField ien_srw2hostbuserr_bit;
        partial void Ien_Srw2hostbuserr_Write(bool a, bool b);
        partial void Ien_Srw2hostbuserr_Read(bool a, bool b);
        partial void Ien_Srw2hostbuserr_ValueProvider(bool a);
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
        
        // Chiprev - Offset : 0x18
        protected IValueRegisterField chiprev_major_field;
        partial void Chiprev_Major_Write(ulong a, ulong b);
        partial void Chiprev_Major_Read(ulong a, ulong b);
        partial void Chiprev_Major_ValueProvider(ulong a);
        protected IValueRegisterField chiprev_family_field;
        partial void Chiprev_Family_Write(ulong a, ulong b);
        partial void Chiprev_Family_Read(ulong a, ulong b);
        partial void Chiprev_Family_ValueProvider(ulong a);
        protected IValueRegisterField chiprev_minor_field;
        partial void Chiprev_Minor_Write(ulong a, ulong b);
        partial void Chiprev_Minor_Read(ulong a, ulong b);
        partial void Chiprev_Minor_ValueProvider(ulong a);

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
        
        // Dmem0retnctrl_Ram - Offset : 0x208
        protected IEnumRegisterField<DMEM0RETNCTRL_RAM_RAMRETNCTRL> dmem0retnctrl_ram_ramretnctrl_field;
        partial void Dmem0retnctrl_Ram_Ramretnctrl_Write(DMEM0RETNCTRL_RAM_RAMRETNCTRL a, DMEM0RETNCTRL_RAM_RAMRETNCTRL b);
        partial void Dmem0retnctrl_Ram_Ramretnctrl_Read(DMEM0RETNCTRL_RAM_RAMRETNCTRL a, DMEM0RETNCTRL_RAM_RAMRETNCTRL b);
        partial void Dmem0retnctrl_Ram_Ramretnctrl_ValueProvider(DMEM0RETNCTRL_RAM_RAMRETNCTRL a);

        partial void Dmem0retnctrl_Ram_Write(uint a, uint b);
        partial void Dmem0retnctrl_Ram_Read(uint a, uint b);
        
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
        
        // Radioramretnctrl_Ram - Offset : 0x400
        protected IEnumRegisterField<RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL> radioramretnctrl_ram_seqramretnctrl_field;
        partial void Radioramretnctrl_Ram_Seqramretnctrl_Write(RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL a, RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL b);
        partial void Radioramretnctrl_Ram_Seqramretnctrl_Read(RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL a, RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL b);
        partial void Radioramretnctrl_Ram_Seqramretnctrl_ValueProvider(RADIORAMRETNCTRL_RAM_SEQRAMRETNCTRL a);
        protected IEnumRegisterField<RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL> radioramretnctrl_ram_frcramretnctrl_bit;
        partial void Radioramretnctrl_Ram_Frcramretnctrl_Write(RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL a, RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL b);
        partial void Radioramretnctrl_Ram_Frcramretnctrl_Read(RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL a, RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL b);
        partial void Radioramretnctrl_Ram_Frcramretnctrl_ValueProvider(RADIORAMRETNCTRL_RAM_FRCRAMRETNCTRL a);

        partial void Radioramretnctrl_Ram_Write(uint a, uint b);
        partial void Radioramretnctrl_Ram_Read(uint a, uint b);
        
        // Radioramfeature_Ram - Offset : 0x404
        protected IEnumRegisterField<RADIORAMFEATURE_RAM_SEQRAMEN> radioramfeature_ram_seqramen_field;
        partial void Radioramfeature_Ram_Seqramen_Write(RADIORAMFEATURE_RAM_SEQRAMEN a, RADIORAMFEATURE_RAM_SEQRAMEN b);
        partial void Radioramfeature_Ram_Seqramen_Read(RADIORAMFEATURE_RAM_SEQRAMEN a, RADIORAMFEATURE_RAM_SEQRAMEN b);
        partial void Radioramfeature_Ram_Seqramen_ValueProvider(RADIORAMFEATURE_RAM_SEQRAMEN a);
        protected IEnumRegisterField<RADIORAMFEATURE_RAM_FRCRAMEN> radioramfeature_ram_frcramen_bit;
        partial void Radioramfeature_Ram_Frcramen_Write(RADIORAMFEATURE_RAM_FRCRAMEN a, RADIORAMFEATURE_RAM_FRCRAMEN b);
        partial void Radioramfeature_Ram_Frcramen_Read(RADIORAMFEATURE_RAM_FRCRAMEN a, RADIORAMFEATURE_RAM_FRCRAMEN b);
        partial void Radioramfeature_Ram_Frcramen_ValueProvider(RADIORAMFEATURE_RAM_FRCRAMEN a);

        partial void Radioramfeature_Ram_Write(uint a, uint b);
        partial void Radioramfeature_Ram_Read(uint a, uint b);
        
        // Radioeccctrl_Ram - Offset : 0x408
        protected IFlagRegisterField radioeccctrl_ram_seqrameccen_bit;
        partial void Radioeccctrl_Ram_Seqrameccen_Write(bool a, bool b);
        partial void Radioeccctrl_Ram_Seqrameccen_Read(bool a, bool b);
        partial void Radioeccctrl_Ram_Seqrameccen_ValueProvider(bool a);
        protected IFlagRegisterField radioeccctrl_ram_seqrameccewen_bit;
        partial void Radioeccctrl_Ram_Seqrameccewen_Write(bool a, bool b);
        partial void Radioeccctrl_Ram_Seqrameccewen_Read(bool a, bool b);
        partial void Radioeccctrl_Ram_Seqrameccewen_ValueProvider(bool a);
        protected IFlagRegisterField radioeccctrl_ram_frcrameccen_bit;
        partial void Radioeccctrl_Ram_Frcrameccen_Write(bool a, bool b);
        partial void Radioeccctrl_Ram_Frcrameccen_Read(bool a, bool b);
        partial void Radioeccctrl_Ram_Frcrameccen_ValueProvider(bool a);
        protected IFlagRegisterField radioeccctrl_ram_frcrameccewen_bit;
        partial void Radioeccctrl_Ram_Frcrameccewen_Write(bool a, bool b);
        partial void Radioeccctrl_Ram_Frcrameccewen_Read(bool a, bool b);
        partial void Radioeccctrl_Ram_Frcrameccewen_ValueProvider(bool a);

        partial void Radioeccctrl_Ram_Write(uint a, uint b);
        partial void Radioeccctrl_Ram_Read(uint a, uint b);
        
        // Seqrameccaddr_Ram - Offset : 0x410
        protected IValueRegisterField seqrameccaddr_ram_seqrameccaddr_field;
        partial void Seqrameccaddr_Ram_Seqrameccaddr_Read(ulong a, ulong b);
        partial void Seqrameccaddr_Ram_Seqrameccaddr_ValueProvider(ulong a);

        partial void Seqrameccaddr_Ram_Write(uint a, uint b);
        partial void Seqrameccaddr_Ram_Read(uint a, uint b);
        
        // Frcrameccaddr_Ram - Offset : 0x414
        protected IValueRegisterField frcrameccaddr_ram_frcrameccaddr_field;
        partial void Frcrameccaddr_Ram_Frcrameccaddr_Read(ulong a, ulong b);
        partial void Frcrameccaddr_Ram_Frcrameccaddr_ValueProvider(ulong a);

        partial void Frcrameccaddr_Ram_Write(uint a, uint b);
        partial void Frcrameccaddr_Ram_Read(uint a, uint b);
        
        // Icacheramretnctrl_Ram - Offset : 0x418
        protected IEnumRegisterField<ICACHERAMRETNCTRL_RAM_RAMRETNCTRL> icacheramretnctrl_ram_ramretnctrl_bit;
        partial void Icacheramretnctrl_Ram_Ramretnctrl_Write(ICACHERAMRETNCTRL_RAM_RAMRETNCTRL a, ICACHERAMRETNCTRL_RAM_RAMRETNCTRL b);
        partial void Icacheramretnctrl_Ram_Ramretnctrl_Read(ICACHERAMRETNCTRL_RAM_RAMRETNCTRL a, ICACHERAMRETNCTRL_RAM_RAMRETNCTRL b);
        partial void Icacheramretnctrl_Ram_Ramretnctrl_ValueProvider(ICACHERAMRETNCTRL_RAM_RAMRETNCTRL a);

        partial void Icacheramretnctrl_Ram_Write(uint a, uint b);
        partial void Icacheramretnctrl_Ram_Read(uint a, uint b);
        
        // Dmem0portmapsel_Ram - Offset : 0x41C
        protected IValueRegisterField dmem0portmapsel_ram_ldmaportsel_field;
        partial void Dmem0portmapsel_Ram_Ldmaportsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Ldmaportsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Ldmaportsel_ValueProvider(ulong a);
        protected IValueRegisterField dmem0portmapsel_ram_srwaesportsel_field;
        partial void Dmem0portmapsel_Ram_Srwaesportsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Srwaesportsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Srwaesportsel_ValueProvider(ulong a);
        protected IValueRegisterField dmem0portmapsel_ram_ahbsrwportsel_field;
        partial void Dmem0portmapsel_Ram_Ahbsrwportsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Ahbsrwportsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Ahbsrwportsel_ValueProvider(ulong a);
        protected IValueRegisterField dmem0portmapsel_ram_srweca0portsel_field;
        partial void Dmem0portmapsel_Ram_Srweca0portsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Srweca0portsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Srweca0portsel_ValueProvider(ulong a);
        protected IValueRegisterField dmem0portmapsel_ram_srweca1portsel_field;
        partial void Dmem0portmapsel_Ram_Srweca1portsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Srweca1portsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Srweca1portsel_ValueProvider(ulong a);
        protected IValueRegisterField dmem0portmapsel_ram_mvpahbdata0portsel_field;
        partial void Dmem0portmapsel_Ram_Mvpahbdata0portsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Mvpahbdata0portsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Mvpahbdata0portsel_ValueProvider(ulong a);
        protected IValueRegisterField dmem0portmapsel_ram_mvpahbdata1portsel_field;
        partial void Dmem0portmapsel_Ram_Mvpahbdata1portsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Mvpahbdata1portsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Mvpahbdata1portsel_ValueProvider(ulong a);
        protected IValueRegisterField dmem0portmapsel_ram_mvpahbdata2portsel_field;
        partial void Dmem0portmapsel_Ram_Mvpahbdata2portsel_Write(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Mvpahbdata2portsel_Read(ulong a, ulong b);
        partial void Dmem0portmapsel_Ram_Mvpahbdata2portsel_ValueProvider(ulong a);

        partial void Dmem0portmapsel_Ram_Write(uint a, uint b);
        partial void Dmem0portmapsel_Ram_Read(uint a, uint b);
        
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
        protected IFlagRegisterField lockstatus_radioidbglock_bit;
        partial void Lockstatus_Radioidbglock_Read(bool a, bool b);
        partial void Lockstatus_Radioidbglock_ValueProvider(bool a);
        protected IFlagRegisterField lockstatus_radionidbglock_bit;
        partial void Lockstatus_Radionidbglock_Read(bool a, bool b);
        partial void Lockstatus_Radionidbglock_ValueProvider(bool a);
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
        
        // Cfgrpuratd0_Cfgdrpu - Offset : 0x610
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
        protected IFlagRegisterField cfgrpuratd0_cfgdrpu_ratdcfgahbintercnct_bit;
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgahbintercnct_Write(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgahbintercnct_Read(bool a, bool b);
        partial void Cfgrpuratd0_Cfgdrpu_Ratdcfgahbintercnct_ValueProvider(bool a);

        partial void Cfgrpuratd0_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd0_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd2_Cfgdrpu - Offset : 0x618
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
        
        // Cfgrpuratd4_Cfgdrpu - Offset : 0x620
        protected IFlagRegisterField cfgrpuratd4_cfgdrpu_ratdctrl_bit;
        partial void Cfgrpuratd4_Cfgdrpu_Ratdctrl_Write(bool a, bool b);
        partial void Cfgrpuratd4_Cfgdrpu_Ratdctrl_Read(bool a, bool b);
        partial void Cfgrpuratd4_Cfgdrpu_Ratdctrl_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd4_cfgdrpu_ratddmem0retnctrl_bit;
        partial void Cfgrpuratd4_Cfgdrpu_Ratddmem0retnctrl_Write(bool a, bool b);
        partial void Cfgrpuratd4_Cfgdrpu_Ratddmem0retnctrl_Read(bool a, bool b);
        partial void Cfgrpuratd4_Cfgdrpu_Ratddmem0retnctrl_ValueProvider(bool a);

        partial void Cfgrpuratd4_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd4_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd6_Cfgdrpu - Offset : 0x628
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
        
        // Cfgrpuratd8_Cfgdrpu - Offset : 0x630
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdradioramretnctrl_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioramretnctrl_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioramretnctrl_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioramretnctrl_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdradioramfeature_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioramfeature_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioramfeature_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioramfeature_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdradioeccctrl_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioeccctrl_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioeccctrl_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdradioeccctrl_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratdicacheramretnctrl_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratdicacheramretnctrl_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdicacheramretnctrl_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratdicacheramretnctrl_ValueProvider(bool a);
        protected IFlagRegisterField cfgrpuratd8_cfgdrpu_ratddmem0portmapsel_bit;
        partial void Cfgrpuratd8_Cfgdrpu_Ratddmem0portmapsel_Write(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratddmem0portmapsel_Read(bool a, bool b);
        partial void Cfgrpuratd8_Cfgdrpu_Ratddmem0portmapsel_ValueProvider(bool a);

        partial void Cfgrpuratd8_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd8_Cfgdrpu_Read(uint a, uint b);
        
        // Cfgrpuratd12_Cfgdrpu - Offset : 0x640
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

        partial void Cfgrpuratd12_Cfgdrpu_Write(uint a, uint b);
        partial void Cfgrpuratd12_Cfgdrpu_Read(uint a, uint b);
        
        partial void SYSCFG_Reset();

        partial void EFR32xG2_SYSCFG_3_Constructor();

        public bool Enabled = true;

        private ICMU_EFR32xG2 _cmu;
        private ICMU_EFR32xG2 cmu
        {
            get
            {
                if (Object.ReferenceEquals(_cmu, null))
                {
                    foreach(var cmu in machine.GetPeripheralsOfType<ICMU_EFR32xG2>())
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
            Cfgahbintercnct = 0x34,
            Sesysromrm_Rom = 0x100,
            Sepkeromrm_Rom = 0x104,
            Sesysctrl_Rom = 0x108,
            Sepkectrl_Rom = 0x10C,
            Ctrl_Ram = 0x200,
            Dmem0retnctrl_Ram = 0x208,
            Ramrm_Ram = 0x300,
            Ramwm_Ram = 0x304,
            Ramra_Ram = 0x308,
            Rambiasconf_Ram = 0x30C,
            Ramlvtest_Ram = 0x310,
            Radioramretnctrl_Ram = 0x400,
            Radioramfeature_Ram = 0x404,
            Radioeccctrl_Ram = 0x408,
            Seqrameccaddr_Ram = 0x410,
            Frcrameccaddr_Ram = 0x414,
            Icacheramretnctrl_Ram = 0x418,
            Dmem0portmapsel_Ram = 0x41C,
            Data0 = 0x600,
            Data1 = 0x604,
            Lockstatus = 0x608,
            Seswversion = 0x60C,
            Cfgrpuratd0_Cfgdrpu = 0x610,
            Cfgrpuratd2_Cfgdrpu = 0x618,
            Cfgrpuratd4_Cfgdrpu = 0x620,
            Cfgrpuratd6_Cfgdrpu = 0x628,
            Cfgrpuratd8_Cfgdrpu = 0x630,
            Cfgrpuratd12_Cfgdrpu = 0x640,
            
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
            Sesysromrm_Rom_SET = 0x1100,
            Sepkeromrm_Rom_SET = 0x1104,
            Sesysctrl_Rom_SET = 0x1108,
            Sepkectrl_Rom_SET = 0x110C,
            Ctrl_Ram_SET = 0x1200,
            Dmem0retnctrl_Ram_SET = 0x1208,
            Ramrm_Ram_SET = 0x1300,
            Ramwm_Ram_SET = 0x1304,
            Ramra_Ram_SET = 0x1308,
            Rambiasconf_Ram_SET = 0x130C,
            Ramlvtest_Ram_SET = 0x1310,
            Radioramretnctrl_Ram_SET = 0x1400,
            Radioramfeature_Ram_SET = 0x1404,
            Radioeccctrl_Ram_SET = 0x1408,
            Seqrameccaddr_Ram_SET = 0x1410,
            Frcrameccaddr_Ram_SET = 0x1414,
            Icacheramretnctrl_Ram_SET = 0x1418,
            Dmem0portmapsel_Ram_SET = 0x141C,
            Data0_SET = 0x1600,
            Data1_SET = 0x1604,
            Lockstatus_SET = 0x1608,
            Seswversion_SET = 0x160C,
            Cfgrpuratd0_Cfgdrpu_SET = 0x1610,
            Cfgrpuratd2_Cfgdrpu_SET = 0x1618,
            Cfgrpuratd4_Cfgdrpu_SET = 0x1620,
            Cfgrpuratd6_Cfgdrpu_SET = 0x1628,
            Cfgrpuratd8_Cfgdrpu_SET = 0x1630,
            Cfgrpuratd12_Cfgdrpu_SET = 0x1640,
            
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
            Sesysromrm_Rom_CLR = 0x2100,
            Sepkeromrm_Rom_CLR = 0x2104,
            Sesysctrl_Rom_CLR = 0x2108,
            Sepkectrl_Rom_CLR = 0x210C,
            Ctrl_Ram_CLR = 0x2200,
            Dmem0retnctrl_Ram_CLR = 0x2208,
            Ramrm_Ram_CLR = 0x2300,
            Ramwm_Ram_CLR = 0x2304,
            Ramra_Ram_CLR = 0x2308,
            Rambiasconf_Ram_CLR = 0x230C,
            Ramlvtest_Ram_CLR = 0x2310,
            Radioramretnctrl_Ram_CLR = 0x2400,
            Radioramfeature_Ram_CLR = 0x2404,
            Radioeccctrl_Ram_CLR = 0x2408,
            Seqrameccaddr_Ram_CLR = 0x2410,
            Frcrameccaddr_Ram_CLR = 0x2414,
            Icacheramretnctrl_Ram_CLR = 0x2418,
            Dmem0portmapsel_Ram_CLR = 0x241C,
            Data0_CLR = 0x2600,
            Data1_CLR = 0x2604,
            Lockstatus_CLR = 0x2608,
            Seswversion_CLR = 0x260C,
            Cfgrpuratd0_Cfgdrpu_CLR = 0x2610,
            Cfgrpuratd2_Cfgdrpu_CLR = 0x2618,
            Cfgrpuratd4_Cfgdrpu_CLR = 0x2620,
            Cfgrpuratd6_Cfgdrpu_CLR = 0x2628,
            Cfgrpuratd8_Cfgdrpu_CLR = 0x2630,
            Cfgrpuratd12_Cfgdrpu_CLR = 0x2640,
            
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
            Sesysromrm_Rom_TGL = 0x3100,
            Sepkeromrm_Rom_TGL = 0x3104,
            Sesysctrl_Rom_TGL = 0x3108,
            Sepkectrl_Rom_TGL = 0x310C,
            Ctrl_Ram_TGL = 0x3200,
            Dmem0retnctrl_Ram_TGL = 0x3208,
            Ramrm_Ram_TGL = 0x3300,
            Ramwm_Ram_TGL = 0x3304,
            Ramra_Ram_TGL = 0x3308,
            Rambiasconf_Ram_TGL = 0x330C,
            Ramlvtest_Ram_TGL = 0x3310,
            Radioramretnctrl_Ram_TGL = 0x3400,
            Radioramfeature_Ram_TGL = 0x3404,
            Radioeccctrl_Ram_TGL = 0x3408,
            Seqrameccaddr_Ram_TGL = 0x3410,
            Frcrameccaddr_Ram_TGL = 0x3414,
            Icacheramretnctrl_Ram_TGL = 0x3418,
            Dmem0portmapsel_Ram_TGL = 0x341C,
            Data0_TGL = 0x3600,
            Data1_TGL = 0x3604,
            Lockstatus_TGL = 0x3608,
            Seswversion_TGL = 0x360C,
            Cfgrpuratd0_Cfgdrpu_TGL = 0x3610,
            Cfgrpuratd2_Cfgdrpu_TGL = 0x3618,
            Cfgrpuratd4_Cfgdrpu_TGL = 0x3620,
            Cfgrpuratd6_Cfgdrpu_TGL = 0x3628,
            Cfgrpuratd8_Cfgdrpu_TGL = 0x3630,
            Cfgrpuratd12_Cfgdrpu_TGL = 0x3640,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}