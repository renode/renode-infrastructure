//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    SOCPLL, Generated on : 2023-11-20 18:17:53.431359
    SOCPLL, ID Version : 71a22d9303ae4db1833b3da2d23af873.1 */

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
    public partial class SiLabs_SOCPLL_1
    {
        public SiLabs_SOCPLL_1(Machine machine) : base(machine)
        {
            SiLabs_SOCPLL_1_constructor();
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
    public partial class SiLabs_SOCPLL_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_SOCPLL_1(Machine machine) : base(machine)
        {
            Define_Registers();
            SiLabs_SOCPLL_1_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion_Socpll, GenerateIpversion_socpllRegister()},
                {(long)Registers.Ctrl_Socpll, GenerateCtrl_socpllRegister()},
                {(long)Registers.Cfg_Socpll, GenerateCfg_socpllRegister()},
                {(long)Registers.Test_Socpll, GenerateTest_socpllRegister()},
                {(long)Registers.Trim_Socpll, GenerateTrim_socpllRegister()},
                {(long)Registers.Dcocfg_Socpll, GenerateDcocfg_socpllRegister()},
                {(long)Registers.Status_Socpll, GenerateStatus_socpllRegister()},
                {(long)Registers.If_Socpll, GenerateIf_socpllRegister()},
                {(long)Registers.Ien_Socpll, GenerateIen_socpllRegister()},
                {(long)Registers.Lock_Socpll, GenerateLock_socpllRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            SOCPLL_Reset();
        }
        
        protected enum CTRL_SOCPLL_REFCLKSEL
        {
            REF_HFXO = 0, // Select hfxo clock as reference clock for gp_socpll
            REF_HFRCO = 1, // Select hfrco clock as reference clock for gp_socpll
            REF_EXTCLK = 2, // Select external clock as reference clock for gp_socpll
            DEFAULT_HFXO = 3, // RESERVED
        }
        
        protected enum CTRL_SOCPLL_PROGRDY
        {
            RDY_DELAY_0US = 0, // 0us delay for rdy generation
            RDY_DELAY_4US = 1, // 4us delay for rdy generation
            RDY_DELAY_8US = 2, // 8us delay for rdy generation
            RDY_DELAY_16US = 3, // 16us delay for rdy generation
        }
        
        protected enum CFG_SOCPLL_RDNIBBLE
        {
            TRIM_DCO = 0, // trim_dco (done automatically by filter)
            PER_CAL = 1, // tdc_gain output
            DCO_OUT_5_0 = 2, // DCO<5:0> value
            DCO_OUT_9_6 = 3, // DCO<9:6> value
        }
        
        protected enum TEST_SOCPLL_LDNIB
        {
            NONE = 0, // Default
            TDC_GAIN = 1, // tdc_gain (first 6 bits only)
            DCO = 2, // dco<9:0>
            HOLD = 3, // hold<9:0> initial value
        }
        
        protected enum STATUS_SOCPLL_LOCK
        {
            UNLOCKED = 0, // SOCPLL is unlocked
            LOCKED = 1, // SOCPLL is locked
        }
        
        // Ipversion_Socpll - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversion_socpllRegister() => new DoubleWordRegister(this, 0x1)
            .WithValueField(0, 32, out ipversion_socpll_ipversion_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ipversion_Socpll_Ipversion_ValueProvider(_);
                        return ipversion_socpll_ipversion_field.Value;               
                    },
                    readCallback: (_, __) => Ipversion_Socpll_Ipversion_Read(_, __),
                    name: "Ipversion")
            .WithReadCallback((_, __) => Ipversion_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Ipversion_Socpll_Write(_, __));
        
        // Ctrl_Socpll - Offset : 0x4
        protected DoubleWordRegister  GenerateCtrl_socpllRegister() => new DoubleWordRegister(this, 0x92761604)
            .WithFlag(0, out ctrl_socpll_forceen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Forceen_ValueProvider(_);
                        return ctrl_socpll_forceen_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Socpll_Forceen_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Socpll_Forceen_Read(_, __),
                    name: "Forceen")
            .WithFlag(1, out ctrl_socpll_disondemand_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Disondemand_ValueProvider(_);
                        return ctrl_socpll_disondemand_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Socpll_Disondemand_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Socpll_Disondemand_Read(_, __),
                    name: "Disondemand")
            .WithFlag(2, out ctrl_socpll_enfracn_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Enfracn_ValueProvider(_);
                        return ctrl_socpll_enfracn_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Socpll_Enfracn_Write(_, __),
                    readCallback: (_, __) => Ctrl_Socpll_Enfracn_Read(_, __),
                    name: "Enfracn")
            .WithReservedBits(3, 1)
            .WithEnumField<DoubleWordRegister, CTRL_SOCPLL_REFCLKSEL>(4, 2, out ctrl_socpll_refclksel_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Refclksel_ValueProvider(_);
                        return ctrl_socpll_refclksel_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Socpll_Refclksel_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Socpll_Refclksel_Read(_, __),
                    name: "Refclksel")
            .WithReservedBits(6, 2)
            .WithValueField(8, 5, out ctrl_socpll_divn_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Divn_ValueProvider(_);
                        return ctrl_socpll_divn_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Socpll_Divn_Write(_, __),
                    readCallback: (_, __) => Ctrl_Socpll_Divn_Read(_, __),
                    name: "Divn")
            .WithReservedBits(13, 3)
            .WithValueField(16, 10, out ctrl_socpll_divf_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Divf_ValueProvider(_);
                        return ctrl_socpll_divf_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Socpll_Divf_Write(_, __),
                    readCallback: (_, __) => Ctrl_Socpll_Divf_Read(_, __),
                    name: "Divf")
            .WithReservedBits(26, 1)
            .WithFlag(27, out ctrl_socpll_enflphyclk_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Enflphyclk_ValueProvider(_);
                        return ctrl_socpll_enflphyclk_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Socpll_Enflphyclk_Write(_, __),
                    readCallback: (_, __) => Ctrl_Socpll_Enflphyclk_Read(_, __),
                    name: "Enflphyclk")
            .WithFlag(28, out ctrl_socpll_ensocclk1_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Ensocclk1_ValueProvider(_);
                        return ctrl_socpll_ensocclk1_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Socpll_Ensocclk1_Write(_, __),
                    readCallback: (_, __) => Ctrl_Socpll_Ensocclk1_Read(_, __),
                    name: "Ensocclk1")
            .WithFlag(29, out ctrl_socpll_ensocclk2_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Ensocclk2_ValueProvider(_);
                        return ctrl_socpll_ensocclk2_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Socpll_Ensocclk2_Write(_, __),
                    readCallback: (_, __) => Ctrl_Socpll_Ensocclk2_Read(_, __),
                    name: "Ensocclk2")
            .WithEnumField<DoubleWordRegister, CTRL_SOCPLL_PROGRDY>(30, 2, out ctrl_socpll_progrdy_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Socpll_Progrdy_ValueProvider(_);
                        return ctrl_socpll_progrdy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Socpll_Progrdy_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Socpll_Progrdy_Read(_, __),
                    name: "Progrdy")
            .WithReadCallback((_, __) => Ctrl_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Socpll_Write(_, __));
        
        // Cfg_Socpll - Offset : 0x8
        protected DoubleWordRegister  GenerateCfg_socpllRegister() => new DoubleWordRegister(this, 0x20)
            .WithValueField(0, 10, out cfg_socpll_innibble_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Socpll_Innibble_ValueProvider(_);
                        return cfg_socpll_innibble_field.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Socpll_Innibble_Write(_, __),
                    readCallback: (_, __) => Cfg_Socpll_Innibble_Read(_, __),
                    name: "Innibble")
            .WithReservedBits(10, 2)
            .WithEnumField<DoubleWordRegister, CFG_SOCPLL_RDNIBBLE>(12, 2, out cfg_socpll_rdnibble_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Socpll_Rdnibble_ValueProvider(_);
                        return cfg_socpll_rdnibble_field.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Socpll_Rdnibble_Write(_, __),
                    readCallback: (_, __) => Cfg_Socpll_Rdnibble_Read(_, __),
                    name: "Rdnibble")
            .WithReservedBits(14, 2)
            .WithFlag(16, out cfg_socpll_selfracn1ord_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Socpll_Selfracn1ord_ValueProvider(_);
                        return cfg_socpll_selfracn1ord_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Socpll_Selfracn1ord_Write(_, __),
                    readCallback: (_, __) => Cfg_Socpll_Selfracn1ord_Read(_, __),
                    name: "Selfracn1ord")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Cfg_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Socpll_Write(_, __));
        
        // Test_Socpll - Offset : 0xC
        protected DoubleWordRegister  GenerateTest_socpllRegister() => new DoubleWordRegister(this, 0x420F)
            .WithFlag(0, out test_socpll_encal_bit, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Encal_ValueProvider(_);
                        return test_socpll_encal_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Encal_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Encal_Read(_, __),
                    name: "Encal")
            .WithFlag(1, out test_socpll_enphase_bit, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Enphase_ValueProvider(_);
                        return test_socpll_enphase_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Enphase_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Enphase_Read(_, __),
                    name: "Enphase")
            .WithFlag(2, out test_socpll_enfreqlock_bit, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Enfreqlock_ValueProvider(_);
                        return test_socpll_enfreqlock_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Enfreqlock_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Enfreqlock_Read(_, __),
                    name: "Enfreqlock")
            .WithFlag(3, out test_socpll_enregfil_bit, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Enregfil_ValueProvider(_);
                        return test_socpll_enregfil_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Enregfil_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Enregfil_Read(_, __),
                    name: "Enregfil")
            .WithFlag(4, out test_socpll_enopenloop_bit, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Enopenloop_ValueProvider(_);
                        return test_socpll_enopenloop_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Test_Socpll_Enopenloop_Write(_, __);
                    },
                    readCallback: (_, __) => Test_Socpll_Enopenloop_Read(_, __),
                    name: "Enopenloop")
            .WithFlag(5, out test_socpll_xobypass_bit, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Xobypass_ValueProvider(_);
                        return test_socpll_xobypass_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Xobypass_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Xobypass_Read(_, __),
                    name: "Xobypass")
            .WithReservedBits(6, 2)
            .WithValueField(8, 2, out test_socpll_selkp_field, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Selkp_ValueProvider(_);
                        return test_socpll_selkp_field.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Selkp_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Selkp_Read(_, __),
                    name: "Selkp")
            .WithReservedBits(10, 2)
            .WithValueField(12, 3, out test_socpll_selki_field, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Selki_ValueProvider(_);
                        return test_socpll_selki_field.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Selki_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Selki_Read(_, __),
                    name: "Selki")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, TEST_SOCPLL_LDNIB>(16, 2, out test_socpll_ldnib_field, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Ldnib_ValueProvider(_);
                        return test_socpll_ldnib_field.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Ldnib_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Ldnib_Read(_, __),
                    name: "Ldnib")
            .WithReservedBits(18, 2)
            .WithValueField(20, 2, out test_socpll_socpllspare_field, 
                    valueProviderCallback: (_) => {
                        Test_Socpll_Socpllspare_ValueProvider(_);
                        return test_socpll_socpllspare_field.Value;               
                    },
                    writeCallback: (_, __) => Test_Socpll_Socpllspare_Write(_, __),
                    readCallback: (_, __) => Test_Socpll_Socpllspare_Read(_, __),
                    name: "Socpllspare")
            .WithReservedBits(22, 10)
            .WithReadCallback((_, __) => Test_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Test_Socpll_Write(_, __));
        
        // Trim_Socpll - Offset : 0x10
        protected DoubleWordRegister  GenerateTrim_socpllRegister() => new DoubleWordRegister(this, 0x484)
            .WithValueField(0, 3, out trim_socpll_vregvtrim_field, 
                    valueProviderCallback: (_) => {
                        Trim_Socpll_Vregvtrim_ValueProvider(_);
                        return trim_socpll_vregvtrim_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Socpll_Vregvtrim_Write(_, __),
                    readCallback: (_, __) => Trim_Socpll_Vregvtrim_Read(_, __),
                    name: "Vregvtrim")
            .WithReservedBits(3, 1)
            .WithValueField(4, 4, out trim_socpll_vregitrim_field, 
                    valueProviderCallback: (_) => {
                        Trim_Socpll_Vregitrim_ValueProvider(_);
                        return trim_socpll_vregitrim_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Socpll_Vregitrim_Write(_, __),
                    readCallback: (_, __) => Trim_Socpll_Vregitrim_Read(_, __),
                    name: "Vregitrim")
            .WithValueField(8, 3, out trim_socpll_vregtc_field, 
                    valueProviderCallback: (_) => {
                        Trim_Socpll_Vregtc_ValueProvider(_);
                        return trim_socpll_vregtc_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Socpll_Vregtc_Write(_, __),
                    readCallback: (_, __) => Trim_Socpll_Vregtc_Read(_, __),
                    name: "Vregtc")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Trim_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Trim_Socpll_Write(_, __));
        
        // Dcocfg_Socpll - Offset : 0x14
        protected DoubleWordRegister  GenerateDcocfg_socpllRegister() => new DoubleWordRegister(this, 0xA211)
            .WithFlag(0, out dcocfg_socpll_outdivinsel_bit, 
                    valueProviderCallback: (_) => {
                        Dcocfg_Socpll_Outdivinsel_ValueProvider(_);
                        return dcocfg_socpll_outdivinsel_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcocfg_Socpll_Outdivinsel_Write(_, __),
                    readCallback: (_, __) => Dcocfg_Socpll_Outdivinsel_Read(_, __),
                    name: "Outdivinsel")
            .WithReservedBits(1, 3)
            .WithValueField(4, 3, out dcocfg_socpll_socclk1outdiv_field, 
                    valueProviderCallback: (_) => {
                        Dcocfg_Socpll_Socclk1outdiv_ValueProvider(_);
                        return dcocfg_socpll_socclk1outdiv_field.Value;               
                    },
                    writeCallback: (_, __) => Dcocfg_Socpll_Socclk1outdiv_Write(_, __),
                    readCallback: (_, __) => Dcocfg_Socpll_Socclk1outdiv_Read(_, __),
                    name: "Socclk1outdiv")
            .WithReservedBits(7, 1)
            .WithValueField(8, 3, out dcocfg_socpll_socclk2outdiv_field, 
                    valueProviderCallback: (_) => {
                        Dcocfg_Socpll_Socclk2outdiv_ValueProvider(_);
                        return dcocfg_socpll_socclk2outdiv_field.Value;               
                    },
                    writeCallback: (_, __) => Dcocfg_Socpll_Socclk2outdiv_Write(_, __),
                    readCallback: (_, __) => Dcocfg_Socpll_Socclk2outdiv_Read(_, __),
                    name: "Socclk2outdiv")
            .WithReservedBits(11, 1)
            .WithValueField(12, 4, out dcocfg_socpll_flphyoutdiv_field, 
                    valueProviderCallback: (_) => {
                        Dcocfg_Socpll_Flphyoutdiv_ValueProvider(_);
                        return dcocfg_socpll_flphyoutdiv_field.Value;               
                    },
                    writeCallback: (_, __) => Dcocfg_Socpll_Flphyoutdiv_Write(_, __),
                    readCallback: (_, __) => Dcocfg_Socpll_Flphyoutdiv_Read(_, __),
                    name: "Flphyoutdiv")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Dcocfg_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Dcocfg_Socpll_Write(_, __));
        
        // Status_Socpll - Offset : 0x18
        protected DoubleWordRegister  GenerateStatus_socpllRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_socpll_rdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Socpll_Rdy_ValueProvider(_);
                        return status_socpll_rdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Socpll_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out status_socpll_plllock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Socpll_Plllock_ValueProvider(_);
                        return status_socpll_plllock_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Socpll_Plllock_Read(_, __),
                    name: "Plllock")
            .WithValueField(2, 6, out status_socpll_outnibble_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Socpll_Outnibble_ValueProvider(_);
                        return status_socpll_outnibble_field.Value;               
                    },
                    readCallback: (_, __) => Status_Socpll_Outnibble_Read(_, __),
                    name: "Outnibble")
            .WithFlag(8, out status_socpll_ens_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Socpll_Ens_ValueProvider(_);
                        return status_socpll_ens_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Socpll_Ens_Read(_, __),
                    name: "Ens")
            .WithReservedBits(9, 22)
            .WithEnumField<DoubleWordRegister, STATUS_SOCPLL_LOCK>(31, 1, out status_socpll_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Socpll_Lock_ValueProvider(_);
                        return status_socpll_lock_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Socpll_Lock_Read(_, __),
                    name: "Lock")
            .WithReadCallback((_, __) => Status_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Status_Socpll_Write(_, __));
        
        // If_Socpll - Offset : 0x1C
        protected DoubleWordRegister  GenerateIf_socpllRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_socpll_rdyif_bit, 
                    valueProviderCallback: (_) => {
                        If_Socpll_Rdyif_ValueProvider(_);
                        return if_socpll_rdyif_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Socpll_Rdyif_Write(_, __),
                    readCallback: (_, __) => If_Socpll_Rdyif_Read(_, __),
                    name: "Rdyif")
            .WithFlag(1, out if_socpll_lossoflockif_bit, 
                    valueProviderCallback: (_) => {
                        If_Socpll_Lossoflockif_ValueProvider(_);
                        return if_socpll_lossoflockif_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Socpll_Lossoflockif_Write(_, __),
                    readCallback: (_, __) => If_Socpll_Lossoflockif_Read(_, __),
                    name: "Lossoflockif")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => If_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => If_Socpll_Write(_, __));
        
        // Ien_Socpll - Offset : 0x20
        protected DoubleWordRegister  GenerateIen_socpllRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_socpll_rdyien_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Socpll_Rdyien_ValueProvider(_);
                        return ien_socpll_rdyien_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Socpll_Rdyien_Write(_, __),
                    readCallback: (_, __) => Ien_Socpll_Rdyien_Read(_, __),
                    name: "Rdyien")
            .WithFlag(1, out ien_socpll_lossoflockien_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Socpll_Lossoflockien_ValueProvider(_);
                        return ien_socpll_lossoflockien_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Socpll_Lossoflockien_Write(_, __),
                    readCallback: (_, __) => Ien_Socpll_Lossoflockien_Read(_, __),
                    name: "Lossoflockien")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Ien_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Socpll_Write(_, __));
        
        // Lock_Socpll - Offset : 0x24
        protected DoubleWordRegister  GenerateLock_socpllRegister() => new DoubleWordRegister(this, 0x81A6)
            .WithValueField(0, 16, out lock_socpll_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Socpll_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Socpll_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Socpll_Write(_, __));
        

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

        

        private void WriteWSYNC()
        {
            if(!Enabled)
            {
                // throw new InvalidOperationException("Bus fault trying to write to a WSYNC register while peripheral is disabled");
                this.Log(LogLevel.Error, "Trying to write to a WSYNC register while peripheral is disabled EN = {0}", Enabled);
            }
        }


        // Ipversion_Socpll - Offset : 0x0
        protected IValueRegisterField ipversion_socpll_ipversion_field;
        partial void Ipversion_Socpll_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Socpll_Ipversion_ValueProvider(ulong a);

        partial void Ipversion_Socpll_Write(uint a, uint b);
        partial void Ipversion_Socpll_Read(uint a, uint b);
        
        // Ctrl_Socpll - Offset : 0x4
        protected IFlagRegisterField ctrl_socpll_forceen_bit;
        partial void Ctrl_Socpll_Forceen_Write(bool a, bool b);
        partial void Ctrl_Socpll_Forceen_Read(bool a, bool b);
        partial void Ctrl_Socpll_Forceen_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_socpll_disondemand_bit;
        partial void Ctrl_Socpll_Disondemand_Write(bool a, bool b);
        partial void Ctrl_Socpll_Disondemand_Read(bool a, bool b);
        partial void Ctrl_Socpll_Disondemand_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_socpll_enfracn_bit;
        partial void Ctrl_Socpll_Enfracn_Write(bool a, bool b);
        partial void Ctrl_Socpll_Enfracn_Read(bool a, bool b);
        partial void Ctrl_Socpll_Enfracn_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_SOCPLL_REFCLKSEL> ctrl_socpll_refclksel_field;
        partial void Ctrl_Socpll_Refclksel_Write(CTRL_SOCPLL_REFCLKSEL a, CTRL_SOCPLL_REFCLKSEL b);
        partial void Ctrl_Socpll_Refclksel_Read(CTRL_SOCPLL_REFCLKSEL a, CTRL_SOCPLL_REFCLKSEL b);
        partial void Ctrl_Socpll_Refclksel_ValueProvider(CTRL_SOCPLL_REFCLKSEL a);
        protected IValueRegisterField ctrl_socpll_divn_field;
        partial void Ctrl_Socpll_Divn_Write(ulong a, ulong b);
        partial void Ctrl_Socpll_Divn_Read(ulong a, ulong b);
        partial void Ctrl_Socpll_Divn_ValueProvider(ulong a);
        protected IValueRegisterField ctrl_socpll_divf_field;
        partial void Ctrl_Socpll_Divf_Write(ulong a, ulong b);
        partial void Ctrl_Socpll_Divf_Read(ulong a, ulong b);
        partial void Ctrl_Socpll_Divf_ValueProvider(ulong a);
        protected IFlagRegisterField ctrl_socpll_enflphyclk_bit;
        partial void Ctrl_Socpll_Enflphyclk_Write(bool a, bool b);
        partial void Ctrl_Socpll_Enflphyclk_Read(bool a, bool b);
        partial void Ctrl_Socpll_Enflphyclk_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_socpll_ensocclk1_bit;
        partial void Ctrl_Socpll_Ensocclk1_Write(bool a, bool b);
        partial void Ctrl_Socpll_Ensocclk1_Read(bool a, bool b);
        partial void Ctrl_Socpll_Ensocclk1_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_socpll_ensocclk2_bit;
        partial void Ctrl_Socpll_Ensocclk2_Write(bool a, bool b);
        partial void Ctrl_Socpll_Ensocclk2_Read(bool a, bool b);
        partial void Ctrl_Socpll_Ensocclk2_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_SOCPLL_PROGRDY> ctrl_socpll_progrdy_field;
        partial void Ctrl_Socpll_Progrdy_Write(CTRL_SOCPLL_PROGRDY a, CTRL_SOCPLL_PROGRDY b);
        partial void Ctrl_Socpll_Progrdy_Read(CTRL_SOCPLL_PROGRDY a, CTRL_SOCPLL_PROGRDY b);
        partial void Ctrl_Socpll_Progrdy_ValueProvider(CTRL_SOCPLL_PROGRDY a);

        partial void Ctrl_Socpll_Write(uint a, uint b);
        partial void Ctrl_Socpll_Read(uint a, uint b);
        
        // Cfg_Socpll - Offset : 0x8
        protected IValueRegisterField cfg_socpll_innibble_field;
        partial void Cfg_Socpll_Innibble_Write(ulong a, ulong b);
        partial void Cfg_Socpll_Innibble_Read(ulong a, ulong b);
        partial void Cfg_Socpll_Innibble_ValueProvider(ulong a);
        protected IEnumRegisterField<CFG_SOCPLL_RDNIBBLE> cfg_socpll_rdnibble_field;
        partial void Cfg_Socpll_Rdnibble_Write(CFG_SOCPLL_RDNIBBLE a, CFG_SOCPLL_RDNIBBLE b);
        partial void Cfg_Socpll_Rdnibble_Read(CFG_SOCPLL_RDNIBBLE a, CFG_SOCPLL_RDNIBBLE b);
        partial void Cfg_Socpll_Rdnibble_ValueProvider(CFG_SOCPLL_RDNIBBLE a);
        protected IFlagRegisterField cfg_socpll_selfracn1ord_bit;
        partial void Cfg_Socpll_Selfracn1ord_Write(bool a, bool b);
        partial void Cfg_Socpll_Selfracn1ord_Read(bool a, bool b);
        partial void Cfg_Socpll_Selfracn1ord_ValueProvider(bool a);

        partial void Cfg_Socpll_Write(uint a, uint b);
        partial void Cfg_Socpll_Read(uint a, uint b);
        
        // Test_Socpll - Offset : 0xC
        protected IFlagRegisterField test_socpll_encal_bit;
        partial void Test_Socpll_Encal_Write(bool a, bool b);
        partial void Test_Socpll_Encal_Read(bool a, bool b);
        partial void Test_Socpll_Encal_ValueProvider(bool a);
        protected IFlagRegisterField test_socpll_enphase_bit;
        partial void Test_Socpll_Enphase_Write(bool a, bool b);
        partial void Test_Socpll_Enphase_Read(bool a, bool b);
        partial void Test_Socpll_Enphase_ValueProvider(bool a);
        protected IFlagRegisterField test_socpll_enfreqlock_bit;
        partial void Test_Socpll_Enfreqlock_Write(bool a, bool b);
        partial void Test_Socpll_Enfreqlock_Read(bool a, bool b);
        partial void Test_Socpll_Enfreqlock_ValueProvider(bool a);
        protected IFlagRegisterField test_socpll_enregfil_bit;
        partial void Test_Socpll_Enregfil_Write(bool a, bool b);
        partial void Test_Socpll_Enregfil_Read(bool a, bool b);
        partial void Test_Socpll_Enregfil_ValueProvider(bool a);
        protected IFlagRegisterField test_socpll_enopenloop_bit;
        partial void Test_Socpll_Enopenloop_Write(bool a, bool b);
        partial void Test_Socpll_Enopenloop_Read(bool a, bool b);
        partial void Test_Socpll_Enopenloop_ValueProvider(bool a);
        protected IFlagRegisterField test_socpll_xobypass_bit;
        partial void Test_Socpll_Xobypass_Write(bool a, bool b);
        partial void Test_Socpll_Xobypass_Read(bool a, bool b);
        partial void Test_Socpll_Xobypass_ValueProvider(bool a);
        protected IValueRegisterField test_socpll_selkp_field;
        partial void Test_Socpll_Selkp_Write(ulong a, ulong b);
        partial void Test_Socpll_Selkp_Read(ulong a, ulong b);
        partial void Test_Socpll_Selkp_ValueProvider(ulong a);
        protected IValueRegisterField test_socpll_selki_field;
        partial void Test_Socpll_Selki_Write(ulong a, ulong b);
        partial void Test_Socpll_Selki_Read(ulong a, ulong b);
        partial void Test_Socpll_Selki_ValueProvider(ulong a);
        protected IEnumRegisterField<TEST_SOCPLL_LDNIB> test_socpll_ldnib_field;
        partial void Test_Socpll_Ldnib_Write(TEST_SOCPLL_LDNIB a, TEST_SOCPLL_LDNIB b);
        partial void Test_Socpll_Ldnib_Read(TEST_SOCPLL_LDNIB a, TEST_SOCPLL_LDNIB b);
        partial void Test_Socpll_Ldnib_ValueProvider(TEST_SOCPLL_LDNIB a);
        protected IValueRegisterField test_socpll_socpllspare_field;
        partial void Test_Socpll_Socpllspare_Write(ulong a, ulong b);
        partial void Test_Socpll_Socpllspare_Read(ulong a, ulong b);
        partial void Test_Socpll_Socpllspare_ValueProvider(ulong a);

        partial void Test_Socpll_Write(uint a, uint b);
        partial void Test_Socpll_Read(uint a, uint b);
        
        // Trim_Socpll - Offset : 0x10
        protected IValueRegisterField trim_socpll_vregvtrim_field;
        partial void Trim_Socpll_Vregvtrim_Write(ulong a, ulong b);
        partial void Trim_Socpll_Vregvtrim_Read(ulong a, ulong b);
        partial void Trim_Socpll_Vregvtrim_ValueProvider(ulong a);
        protected IValueRegisterField trim_socpll_vregitrim_field;
        partial void Trim_Socpll_Vregitrim_Write(ulong a, ulong b);
        partial void Trim_Socpll_Vregitrim_Read(ulong a, ulong b);
        partial void Trim_Socpll_Vregitrim_ValueProvider(ulong a);
        protected IValueRegisterField trim_socpll_vregtc_field;
        partial void Trim_Socpll_Vregtc_Write(ulong a, ulong b);
        partial void Trim_Socpll_Vregtc_Read(ulong a, ulong b);
        partial void Trim_Socpll_Vregtc_ValueProvider(ulong a);

        partial void Trim_Socpll_Write(uint a, uint b);
        partial void Trim_Socpll_Read(uint a, uint b);
        
        // Dcocfg_Socpll - Offset : 0x14
        protected IFlagRegisterField dcocfg_socpll_outdivinsel_bit;
        partial void Dcocfg_Socpll_Outdivinsel_Write(bool a, bool b);
        partial void Dcocfg_Socpll_Outdivinsel_Read(bool a, bool b);
        partial void Dcocfg_Socpll_Outdivinsel_ValueProvider(bool a);
        protected IValueRegisterField dcocfg_socpll_socclk1outdiv_field;
        partial void Dcocfg_Socpll_Socclk1outdiv_Write(ulong a, ulong b);
        partial void Dcocfg_Socpll_Socclk1outdiv_Read(ulong a, ulong b);
        partial void Dcocfg_Socpll_Socclk1outdiv_ValueProvider(ulong a);
        protected IValueRegisterField dcocfg_socpll_socclk2outdiv_field;
        partial void Dcocfg_Socpll_Socclk2outdiv_Write(ulong a, ulong b);
        partial void Dcocfg_Socpll_Socclk2outdiv_Read(ulong a, ulong b);
        partial void Dcocfg_Socpll_Socclk2outdiv_ValueProvider(ulong a);
        protected IValueRegisterField dcocfg_socpll_flphyoutdiv_field;
        partial void Dcocfg_Socpll_Flphyoutdiv_Write(ulong a, ulong b);
        partial void Dcocfg_Socpll_Flphyoutdiv_Read(ulong a, ulong b);
        partial void Dcocfg_Socpll_Flphyoutdiv_ValueProvider(ulong a);

        partial void Dcocfg_Socpll_Write(uint a, uint b);
        partial void Dcocfg_Socpll_Read(uint a, uint b);
        
        // Status_Socpll - Offset : 0x18
        protected IFlagRegisterField status_socpll_rdy_bit;
        partial void Status_Socpll_Rdy_Read(bool a, bool b);
        partial void Status_Socpll_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField status_socpll_plllock_bit;
        partial void Status_Socpll_Plllock_Read(bool a, bool b);
        partial void Status_Socpll_Plllock_ValueProvider(bool a);
        protected IValueRegisterField status_socpll_outnibble_field;
        partial void Status_Socpll_Outnibble_Read(ulong a, ulong b);
        partial void Status_Socpll_Outnibble_ValueProvider(ulong a);
        protected IFlagRegisterField status_socpll_ens_bit;
        partial void Status_Socpll_Ens_Read(bool a, bool b);
        partial void Status_Socpll_Ens_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_SOCPLL_LOCK> status_socpll_lock_bit;
        partial void Status_Socpll_Lock_Read(STATUS_SOCPLL_LOCK a, STATUS_SOCPLL_LOCK b);
        partial void Status_Socpll_Lock_ValueProvider(STATUS_SOCPLL_LOCK a);

        partial void Status_Socpll_Write(uint a, uint b);
        partial void Status_Socpll_Read(uint a, uint b);
        
        // If_Socpll - Offset : 0x1C
        protected IFlagRegisterField if_socpll_rdyif_bit;
        partial void If_Socpll_Rdyif_Write(bool a, bool b);
        partial void If_Socpll_Rdyif_Read(bool a, bool b);
        partial void If_Socpll_Rdyif_ValueProvider(bool a);
        protected IFlagRegisterField if_socpll_lossoflockif_bit;
        partial void If_Socpll_Lossoflockif_Write(bool a, bool b);
        partial void If_Socpll_Lossoflockif_Read(bool a, bool b);
        partial void If_Socpll_Lossoflockif_ValueProvider(bool a);

        partial void If_Socpll_Write(uint a, uint b);
        partial void If_Socpll_Read(uint a, uint b);
        
        // Ien_Socpll - Offset : 0x20
        protected IFlagRegisterField ien_socpll_rdyien_bit;
        partial void Ien_Socpll_Rdyien_Write(bool a, bool b);
        partial void Ien_Socpll_Rdyien_Read(bool a, bool b);
        partial void Ien_Socpll_Rdyien_ValueProvider(bool a);
        protected IFlagRegisterField ien_socpll_lossoflockien_bit;
        partial void Ien_Socpll_Lossoflockien_Write(bool a, bool b);
        partial void Ien_Socpll_Lossoflockien_Read(bool a, bool b);
        partial void Ien_Socpll_Lossoflockien_ValueProvider(bool a);

        partial void Ien_Socpll_Write(uint a, uint b);
        partial void Ien_Socpll_Read(uint a, uint b);
        
        // Lock_Socpll - Offset : 0x24
        protected IValueRegisterField lock_socpll_lockkey_field;
        partial void Lock_Socpll_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Socpll_Lockkey_ValueProvider(ulong a);

        partial void Lock_Socpll_Write(uint a, uint b);
        partial void Lock_Socpll_Read(uint a, uint b);
        
        partial void SOCPLL_Reset();

        partial void SiLabs_SOCPLL_1_Constructor();

        public bool Enabled
        {
            get 
            {
                // Your boolean which you have to define in your partial class file
                return isEnabled;
            }
        }

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
            Ipversion_Socpll = 0x0,
            Ctrl_Socpll = 0x4,
            Cfg_Socpll = 0x8,
            Test_Socpll = 0xC,
            Trim_Socpll = 0x10,
            Dcocfg_Socpll = 0x14,
            Status_Socpll = 0x18,
            If_Socpll = 0x1C,
            Ien_Socpll = 0x20,
            Lock_Socpll = 0x24,
            
            Ipversion_Socpll_SET = 0x1000,
            Ctrl_Socpll_SET = 0x1004,
            Cfg_Socpll_SET = 0x1008,
            Test_Socpll_SET = 0x100C,
            Trim_Socpll_SET = 0x1010,
            Dcocfg_Socpll_SET = 0x1014,
            Status_Socpll_SET = 0x1018,
            If_Socpll_SET = 0x101C,
            Ien_Socpll_SET = 0x1020,
            Lock_Socpll_SET = 0x1024,
            
            Ipversion_Socpll_CLR = 0x2000,
            Ctrl_Socpll_CLR = 0x2004,
            Cfg_Socpll_CLR = 0x2008,
            Test_Socpll_CLR = 0x200C,
            Trim_Socpll_CLR = 0x2010,
            Dcocfg_Socpll_CLR = 0x2014,
            Status_Socpll_CLR = 0x2018,
            If_Socpll_CLR = 0x201C,
            Ien_Socpll_CLR = 0x2020,
            Lock_Socpll_CLR = 0x2024,
            
            Ipversion_Socpll_TGL = 0x3000,
            Ctrl_Socpll_TGL = 0x3004,
            Cfg_Socpll_TGL = 0x3008,
            Test_Socpll_TGL = 0x300C,
            Trim_Socpll_TGL = 0x3010,
            Dcocfg_Socpll_TGL = 0x3014,
            Status_Socpll_TGL = 0x3018,
            If_Socpll_TGL = 0x301C,
            Ien_Socpll_TGL = 0x3020,
            Lock_Socpll_TGL = 0x3024,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}