//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    DCDC, Generated on : 2023-07-20 14:23:40.136125
    DCDC, ID Version : a4ab938834d94b4fb8292e6008cc8f8c.2 */

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
    public partial class EFR32xG2_DCDC_2
    {
        public EFR32xG2_DCDC_2(Machine machine) : base(machine)
        {
            EFR32xG2_DCDC_2_constructor();
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
    public partial class EFR32xG2_DCDC_2 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_DCDC_2(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_DCDC_2_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Em01ctrl0, GenerateEm01ctrl0Register()},
                {(long)Registers.Izctrl, GenerateIzctrlRegister()},
                {(long)Registers.Em23ctrl0, GenerateEm23ctrl0Register()},
                {(long)Registers.Em01ctrl1, GenerateEm01ctrl1Register()},
                {(long)Registers.Em23ctrl1, GenerateEm23ctrl1Register()},
                {(long)Registers.Ppcfg, GeneratePpcfgRegister()},
                {(long)Registers.Pfmxctrl, GeneratePfmxctrlRegister()},
                {(long)Registers.Transcfg, GenerateTranscfgRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Syncbusy, GenerateSyncbusyRegister()},
                {(long)Registers.Lock_Lock, GenerateLock_lockRegister()},
                {(long)Registers.Lockstatus_Lock, GenerateLockstatus_lockRegister()},
                {(long)Registers.Trim0_Feature, GenerateTrim0_featureRegister()},
                {(long)Registers.Trim1_Feature, GenerateTrim1_featureRegister()},
                {(long)Registers.Trim2_Feature, GenerateTrim2_featureRegister()},
                {(long)Registers.Cfg_Feature, GenerateCfg_featureRegister()},
                {(long)Registers.Dcdcforce_Test, GenerateDcdcforce_testRegister()},
                {(long)Registers.Dbustest_Test, GenerateDbustest_testRegister()},
                {(long)Registers.Dcdcvcmptest_Test, GenerateDcdcvcmptest_testRegister()},
                {(long)Registers.Izctest_Test, GenerateIzctest_testRegister()},
                {(long)Registers.Dcdctest_Test, GenerateDcdctest_testRegister()},
                {(long)Registers.Teststatus0_Test, GenerateTeststatus0_testRegister()},
                {(long)Registers.Teststatus1_Test, GenerateTeststatus1_testRegister()},
                {(long)Registers.Teststatus2_Test, GenerateTeststatus2_testRegister()},
                {(long)Registers.Rpuratd0_Drpu, GenerateRpuratd0_drpuRegister()},
                {(long)Registers.Rpuratd1_Drpu, GenerateRpuratd1_drpuRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            DCDC_Reset();
        }
        
        protected enum CTRL_MODE
        {
            BYPASS = 0, // DCDC is OFF, bypass switch is enabled
            DCDCREGULATION = 1, // Request DCDC regulation, bypass switch disabled
        }
        
        protected enum CTRL_PFMXEXTREQ
        {
            EXTLOWLOAD = 0, // External chip is not requesting high load current for TX
            EXTHIGHLOAD = 1, // External chip is requesting high load current for TX
        }
        
        protected enum EM01CTRL0_IPKVAL
        {
            Load28mA = 1, // Ipeak = 70mA, Iload = 28mA
            Load32mA = 2, // Ipeak = 80mA, Iload = 32mA
            Load36mA = 3, // Ipeak = 90mA, Iload = 36mA
            Load40mA = 4, // Ipeak = 100mA, Iload = 40mA
            Load44mA = 5, // Ipeak = 110mA, Iload = 44mA
            Load48mA = 6, // Ipeak = 120mA, Iload = 48mA
            Load52mA = 7, // Ipeak = 130mA, Iload = 52mA
            Load56mA = 8, // Ipeak = 140mA, Iload = 56mA
            Load60mA = 9, // Ipeak = 150mA, Iload = 60mA
            Load64mA = 10, // Ipeak = 160mA, Iload = 64mA
            Load68mA = 11, // Ipeak = 170mA, Iload = 68mA
            Load72mA = 12, // Ipeak = 180mA, Iload = 72mA
            Load76mA = 13, // Ipeak = 190mA, Iload = 76mA
            Load80mA = 14, // Ipeak = 200mA, Iload = 80mA
        }
        
        protected enum EM01CTRL0_DRVSPEED
        {
            BEST_EMI = 0, // Lowest Efficiency, Lowest EMI.. Small decrease in efficiency from default setting
            DEFAULT_SETTING = 1, // Default Efficiency, Acceptable EMI level
            INTERMEDIATE = 2, // Small increase in efficiency from the default setting
            BEST_EFFICIENCY = 3, // Highest Efficiency, Highest EMI.. Small increase in efficiency from INTERMEDIATE setting
        }
        
        protected enum IZCTRL_IZSTALL
        {
            RUN = 0, // Counter is not stalled
            STALL = 1, // Counter is stalled
        }
        
        protected enum IZCTRL_IZFIXEDEN
        {
            NORMAL = 0, // izdet DAC digital input mux selects counter output
            OVERRIDE = 1, // izdet DAC digital input mux selects izval[4:0]
        }
        
        protected enum EM23CTRL0_IPKVAL
        {
            Load28mA = 1, // Ipeak = 70mA, Iload = 28mA
            Load32mA = 2, // Ipeak = 80mA, Iload = 32mA
            Load36mA = 3, // Ipeak = 90mA, Iload = 36mA
            Load40mA = 4, // Ipeak = 100mA, Iload = 40mA
            Load44mA = 5, // Ipeak = 110mA, Iload = 44mA
            Load48mA = 6, // Ipeak = 120mA, Iload = 48mA
            Load52mA = 7, // Ipeak = 130mA, Iload = 52mA
            Load56mA = 8, // Ipeak = 140mA, Iload = 56mA
            Load60mA = 9, // Ipeak = 150mA, Iload = 60mA
            Load64mA = 10, // Ipeak = 160mA, Iload = 64mA
            Load68mA = 11, // Ipeak = 170mA, Iload = 68mA
            Load72mA = 12, // Ipeak = 180mA, Iload = 72mA
            Load76mA = 13, // Ipeak = 190mA, Iload = 76mA
            Load80mA = 14, // Ipeak = 200mA, Iload = 80mA
        }
        
        protected enum EM23CTRL0_DRVSPEED
        {
            BEST_EMI = 0, // Lowest Efficiency, Lowest EMI.. Small decrease in efficiency from default setting
            DEFAULT_SETTING = 1, // Default Efficiency, Acceptable EMI level
            INTERMEDIATE = 2, // Small increase in efficiency from the default setting
            BEST_EFFICIENCY = 3, // Highest Efficiency, Highest EMI.. Small increase in efficiency from INTERMEDIATE setting
        }
        
        protected enum EM01CTRL1_VCMPIBIAS
        {
            IDLE90nA = 0, // I_idle = 90nA
            IDLE150nA = 1, // I_idle = 150nA
            IDLE275nA = 2, // I_idle = 275nA
            IDLE515nA = 3, // I_idle = 515nA
        }
        
        protected enum EM23CTRL1_VCMPIBIAS
        {
            IDLE90nA = 0, // I_idle = 90nA
            IDLE150nA = 1, // I_idle = 150nA
            IDLE275nA = 2, // I_idle = 275nA
            IDLE515nA = 3, // I_idle = 515nA
        }
        
        protected enum EM23CTRL1_TEMPBIASCTRL
        {
            NO_ACTION = 0, // Do nothing
            BIASLSB1_R3 = 1, // Set bias lsb to 1 if refreshrate (biasctrl) is R3REFRESHRATE
            BIASLSB1_R3_R2 = 2, // Set bias lsb to 1 if refreshrate (biasctrl) is R3REFRESHRATE or R2REFRESHRATE
            BIASLSB1_R2_0x2_R3 = 3, // In R2, BIAS[0] is set, in R3 BIAS[1:0]=0x3 (even though name suggests 0x2)
        }
        
        protected enum PPCFG_DRVSPEED
        {
            BEST_EMI = 0, // Lowest Efficiency, Lowest EMI.. Small decrease in efficiency from default setting
            DEFAULT_SETTING = 1, // Default Efficiency, Acceptable EMI level
            INTERMEDIATE = 2, // Small increase in efficiency from the default setting
            BEST_EFFICIENCY = 3, // Highest Efficiency, Highest EMI.. Small increase in efficiency from INTERMEDIATE setting
        }
        
        protected enum TRANSCFG_IPKVAL
        {
            Load28mA = 1, // Ipeak = 70mA, IL = 28mA
            Load32mA = 2, // Ipeak = 80mA, IL = 32mA
            Load36mA = 3, // Ipeak = 90mA, IL = 36mA
            Load40mA = 4, // Ipeak = 100mA, IL = 40mA
            Load44mA = 5, // Ipeak = 110mA, IL = 44mA
            Load48mA = 6, // Ipeak = 120mA, IL = 48mA
            Load52mA = 7, // Ipeak = 130mA, IL = 52mA
            Load56mA = 8, // Ipeak = 140mA, IL = 56mA
            Load60mA = 9, // Ipeak = 150mA, IL = 60mA
            Load64mA = 10, // Ipeak = 160mA, IL = 64mA
            Load68mA = 11, // Ipeak = 170mA, IL = 68mA
            Load72mA = 12, // Ipeak = 180mA, IL = 72mA
            Load76mA = 13, // Ipeak = 190mA, IL = 76mA
            Load80mA = 14, // Ipeak = 200mA, IL = 80mA
        }
        
        protected enum LOCKSTATUS_LOCK_LOCK
        {
            UNLOCKED = 0, // Unlocked State
            LOCKED = 1, // LOCKED STATE
        }
        
        protected enum CFG_FEATURE_EARLYRESETEN
        {
            DISABLE = 0, // Disabled
            ENABLE = 1, // Enable generation of Early Reset Pulses
        }
        
        protected enum CFG_FEATURE_VCMPVPROG
        {
            TARGET_1P775 = 0, // Target voltage at 1.775V
            TARGET_1P8 = 1, // Target voltage at 1.8V
            TARGET_1P825 = 2, // Target voltage at 1.825V
            TARGET_1P85 = 3, // Target voltage at 1.85V
        }
        
        protected enum CFG_FEATURE_SWDRVDLY
        {
            ONECYCLE = 0, // Default of 1 Cycle
            TWOCYCLE = 1, // Add 1 extra cycle of delay
        }
        
        protected enum DBUSTEST_TEST_TESTSEL
        {
            GND_VCMPOUT = 0, // dbus_core=gnd; dbus_vcmp=vcmp_out
            IZDET_CORESEL = 1, // dbus_core=iz_det; dbus_vcmp=core_sel
            IZDNOUT_COREEN1 = 2, // dbus_core=iz_dn_out; dbus_vcmp=core_en[1]
            IZCOUNTERINEN_COREEN0 = 3, // dbus_core=iz_counterin_en; dbus_vcmp=core_en[0]; set iz_counterin_en
            IPKRCOCALOUT_CORE0IB1 = 4, // dbus_core=ipk_rcocal_out; dbus_vcmp=core0_ib_sel[1]; Set rcocal_en internal signal
            TMAXSTATUS_CORE0IB0 = 5, // dbus_core=tmax_status; dbus_vcmp=core0_ib_sel[0]
            IPKDET_CORE1IB1 = 6, // dbus_core=ipk_det; dbus_vcmp=core1_ib_sel[1]
            TMAX_CORE1IB0 = 7, // dbus_core=tmax; dbus_vcmp=core1_ib_sel[0]
            VCMPOUT_VCMPRESET = 8, // dbus_core=vcmp_out; dbus_vcmp=vcmp_reset
            VCMPRESET_VREFEN = 9, // dbus_core=vcmp_reset; dbus_vcmp=vref_en
            PCHISOFF_RESET1 = 10, // dbus_core=pch_isoff; dbus_vcmp=reset[1]
            NCHISOFF_RESET0 = 11, // dbus_core=nch_isoff; dbus_vcmp=reset[0]
            IPKAZOUT_AZ1 = 12, // dbus_core=ipk_azout; dbus_vcmp=az[1]
            IPKDETEN_AZ0 = 13, // dbus_core=ipkdet_en; dbus_vcmp=az[0]
            DRVPG_GND = 14, // dbus_core=drv_pg; dbus_vcmp=gnd
            DRVNG_GND = 15, // dbus_core=drv_ng; dbus_vcmp=gnd
        }
        
        protected enum DCDCVCMPTEST_TEST_VCMPCMPFORCE
        {
            NORMAL = 0, // Normal Operation
            BOTH = 1, // Enable current for both the comparators
            CMP0 = 2, // Enable current for comparators and select comparator 0
            CMP1 = 3, // Enable current for comparators and select comparator 1
        }
        
        protected enum IZCTEST_TEST_IZCOUNTERIN
        {
            UP = 0, // Counter counts up
            DOWN = 1, // Counter counts down
        }
        
        protected enum IZCTEST_TEST_IZNFETREPPD
        {
            OFF = 0, // Step size of zero current DAC is independent of the resistance of the nfet power switch
            ON = 1, // Larger step size, changes with resistance of the nfet power switch (~10mA step)
        }
        
        protected enum TESTSTATUS0_TEST_UPDIPKDAC
        {
            DONE = 0, // IPKVAL -> IPKDAC sync done
            BUSY = 1, // IPKVAL -> IPKDAC sync busy
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x2)
            .WithValueField(0, 32, out ipversion_ipversion_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ipversion_Ipversion_ValueProvider(_);
                        return ipversion_ipversion_field.Value;               
                    },
                    readCallback: (_, __) => Ipversion_Ipversion_Read(_, __),
                    name: "Ipversion")
            .WithReadCallback((_, __) => Ipversion_Read(_, __))
            .WithWriteCallback((_, __) => Ipversion_Write(_, __));
        
        // Ctrl - Offset : 0x4
        protected DoubleWordRegister  GenerateCtrlRegister() => new DoubleWordRegister(this, 0x100)
            .WithEnumField<DoubleWordRegister, CTRL_MODE>(0, 1, out ctrl_mode_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Mode_ValueProvider(_);
                        return ctrl_mode_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Mode_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Mode_Read(_, __),
                    name: "Mode")
            .WithReservedBits(1, 3)
            .WithValueField(4, 5, out ctrl_ipktmaxctrl_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Ipktmaxctrl_ValueProvider(_);
                        return ctrl_ipktmaxctrl_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Ipktmaxctrl_Write(_, __),
                    readCallback: (_, __) => Ctrl_Ipktmaxctrl_Read(_, __),
                    name: "Ipktmaxctrl")
            .WithReservedBits(9, 22)
            .WithEnumField<DoubleWordRegister, CTRL_PFMXEXTREQ>(31, 1, out ctrl_pfmxextreq_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Pfmxextreq_ValueProvider(_);
                        return ctrl_pfmxextreq_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Pfmxextreq_Write(_, __),
                    readCallback: (_, __) => Ctrl_Pfmxextreq_Read(_, __),
                    name: "Pfmxextreq")
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write(_, __));
        
        // Em01ctrl0 - Offset : 0x8
        protected DoubleWordRegister  GenerateEm01ctrl0Register() => new DoubleWordRegister(this, 0x109)
            .WithEnumField<DoubleWordRegister, EM01CTRL0_IPKVAL>(0, 4, out em01ctrl0_ipkval_field, 
                    valueProviderCallback: (_) => {
                        Em01ctrl0_Ipkval_ValueProvider(_);
                        return em01ctrl0_ipkval_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Em01ctrl0_Ipkval_Write(_, __);
                    },
                    readCallback: (_, __) => Em01ctrl0_Ipkval_Read(_, __),
                    name: "Ipkval")
            .WithReservedBits(4, 4)
            .WithEnumField<DoubleWordRegister, EM01CTRL0_DRVSPEED>(8, 2, out em01ctrl0_drvspeed_field, 
                    valueProviderCallback: (_) => {
                        Em01ctrl0_Drvspeed_ValueProvider(_);
                        return em01ctrl0_drvspeed_field.Value;               
                    },
                    writeCallback: (_, __) => Em01ctrl0_Drvspeed_Write(_, __),
                    readCallback: (_, __) => Em01ctrl0_Drvspeed_Read(_, __),
                    name: "Drvspeed")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Em01ctrl0_Read(_, __))
            .WithWriteCallback((_, __) => Em01ctrl0_Write(_, __));
        
        // Izctrl - Offset : 0xC
        protected DoubleWordRegister  GenerateIzctrlRegister() => new DoubleWordRegister(this, 0x80)
            .WithEnumField<DoubleWordRegister, IZCTRL_IZSTALL>(0, 1, out izctrl_izstall_bit, 
                    valueProviderCallback: (_) => {
                        Izctrl_Izstall_ValueProvider(_);
                        return izctrl_izstall_bit.Value;               
                    },
                    writeCallback: (_, __) => Izctrl_Izstall_Write(_, __),
                    readCallback: (_, __) => Izctrl_Izstall_Read(_, __),
                    name: "Izstall")
            .WithReservedBits(1, 2)
            .WithEnumField<DoubleWordRegister, IZCTRL_IZFIXEDEN>(3, 1, out izctrl_izfixeden_bit, 
                    valueProviderCallback: (_) => {
                        Izctrl_Izfixeden_ValueProvider(_);
                        return izctrl_izfixeden_bit.Value;               
                    },
                    writeCallback: (_, __) => Izctrl_Izfixeden_Write(_, __),
                    readCallback: (_, __) => Izctrl_Izfixeden_Read(_, __),
                    name: "Izfixeden")
            .WithValueField(4, 5, out izctrl_izval_field, 
                    valueProviderCallback: (_) => {
                        Izctrl_Izval_ValueProvider(_);
                        return izctrl_izval_field.Value;               
                    },
                    writeCallback: (_, __) => Izctrl_Izval_Write(_, __),
                    readCallback: (_, __) => Izctrl_Izval_Read(_, __),
                    name: "Izval")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Izctrl_Read(_, __))
            .WithWriteCallback((_, __) => Izctrl_Write(_, __));
        
        // Em23ctrl0 - Offset : 0x10
        protected DoubleWordRegister  GenerateEm23ctrl0Register() => new DoubleWordRegister(this, 0x103)
            .WithEnumField<DoubleWordRegister, EM23CTRL0_IPKVAL>(0, 4, out em23ctrl0_ipkval_field, 
                    valueProviderCallback: (_) => {
                        Em23ctrl0_Ipkval_ValueProvider(_);
                        return em23ctrl0_ipkval_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Em23ctrl0_Ipkval_Write(_, __);
                    },
                    readCallback: (_, __) => Em23ctrl0_Ipkval_Read(_, __),
                    name: "Ipkval")
            .WithReservedBits(4, 4)
            .WithEnumField<DoubleWordRegister, EM23CTRL0_DRVSPEED>(8, 2, out em23ctrl0_drvspeed_field, 
                    valueProviderCallback: (_) => {
                        Em23ctrl0_Drvspeed_ValueProvider(_);
                        return em23ctrl0_drvspeed_field.Value;               
                    },
                    writeCallback: (_, __) => Em23ctrl0_Drvspeed_Write(_, __),
                    readCallback: (_, __) => Em23ctrl0_Drvspeed_Read(_, __),
                    name: "Drvspeed")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Em23ctrl0_Read(_, __))
            .WithWriteCallback((_, __) => Em23ctrl0_Write(_, __));
        
        // Em01ctrl1 - Offset : 0x14
        protected DoubleWordRegister  GenerateEm01ctrl1Register() => new DoubleWordRegister(this, 0x3)
            .WithEnumField<DoubleWordRegister, EM01CTRL1_VCMPIBIAS>(0, 2, out em01ctrl1_vcmpibias_field, 
                    valueProviderCallback: (_) => {
                        Em01ctrl1_Vcmpibias_ValueProvider(_);
                        return em01ctrl1_vcmpibias_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Em01ctrl1_Vcmpibias_Write(_, __);
                    },
                    readCallback: (_, __) => Em01ctrl1_Vcmpibias_Read(_, __),
                    name: "Vcmpibias")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Em01ctrl1_Read(_, __))
            .WithWriteCallback((_, __) => Em01ctrl1_Write(_, __));
        
        // Em23ctrl1 - Offset : 0x18
        protected DoubleWordRegister  GenerateEm23ctrl1Register() => new DoubleWordRegister(this, 0x20)
            .WithEnumField<DoubleWordRegister, EM23CTRL1_VCMPIBIAS>(0, 2, out em23ctrl1_vcmpibias_field, 
                    valueProviderCallback: (_) => {
                        Em23ctrl1_Vcmpibias_ValueProvider(_);
                        return em23ctrl1_vcmpibias_field.Value;               
                    },
                    writeCallback: (_, __) => Em23ctrl1_Vcmpibias_Write(_, __),
                    readCallback: (_, __) => Em23ctrl1_Vcmpibias_Read(_, __),
                    name: "Vcmpibias")
            .WithReservedBits(2, 2)
            .WithEnumField<DoubleWordRegister, EM23CTRL1_TEMPBIASCTRL>(4, 2, out em23ctrl1_tempbiasctrl_field, 
                    valueProviderCallback: (_) => {
                        Em23ctrl1_Tempbiasctrl_ValueProvider(_);
                        return em23ctrl1_tempbiasctrl_field.Value;               
                    },
                    writeCallback: (_, __) => Em23ctrl1_Tempbiasctrl_Write(_, __),
                    readCallback: (_, __) => Em23ctrl1_Tempbiasctrl_Read(_, __),
                    name: "Tempbiasctrl")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Em23ctrl1_Read(_, __))
            .WithWriteCallback((_, __) => Em23ctrl1_Write(_, __));
        
        // Ppcfg - Offset : 0x1C
        protected DoubleWordRegister  GeneratePpcfgRegister() => new DoubleWordRegister(this, 0x11009)
            .WithValueField(0, 4, out ppcfg_ipkval_field, 
                    valueProviderCallback: (_) => {
                        Ppcfg_Ipkval_ValueProvider(_);
                        return ppcfg_ipkval_field.Value;               
                    },
                    writeCallback: (_, __) => Ppcfg_Ipkval_Write(_, __),
                    readCallback: (_, __) => Ppcfg_Ipkval_Read(_, __),
                    name: "Ipkval")
            .WithReservedBits(4, 4)
            .WithValueField(8, 5, out ppcfg_ipktmaxctrl_field, 
                    valueProviderCallback: (_) => {
                        Ppcfg_Ipktmaxctrl_ValueProvider(_);
                        return ppcfg_ipktmaxctrl_field.Value;               
                    },
                    writeCallback: (_, __) => Ppcfg_Ipktmaxctrl_Write(_, __),
                    readCallback: (_, __) => Ppcfg_Ipktmaxctrl_Read(_, __),
                    name: "Ipktmaxctrl")
            .WithReservedBits(13, 3)
            .WithEnumField<DoubleWordRegister, PPCFG_DRVSPEED>(16, 2, out ppcfg_drvspeed_field, 
                    valueProviderCallback: (_) => {
                        Ppcfg_Drvspeed_ValueProvider(_);
                        return ppcfg_drvspeed_field.Value;               
                    },
                    writeCallback: (_, __) => Ppcfg_Drvspeed_Write(_, __),
                    readCallback: (_, __) => Ppcfg_Drvspeed_Read(_, __),
                    name: "Drvspeed")
            .WithReservedBits(18, 14)
            .WithReadCallback((_, __) => Ppcfg_Read(_, __))
            .WithWriteCallback((_, __) => Ppcfg_Write(_, __));
        
        // Pfmxctrl - Offset : 0x20
        protected DoubleWordRegister  GeneratePfmxctrlRegister() => new DoubleWordRegister(this, 0xB0C)
            .WithValueField(0, 4, out pfmxctrl_ipkval_field, 
                    valueProviderCallback: (_) => {
                        Pfmxctrl_Ipkval_ValueProvider(_);
                        return pfmxctrl_ipkval_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pfmxctrl_Ipkval_Write(_, __);
                    },
                    readCallback: (_, __) => Pfmxctrl_Ipkval_Read(_, __),
                    name: "Ipkval")
            .WithReservedBits(4, 4)
            .WithValueField(8, 5, out pfmxctrl_ipktmaxctrl_field, 
                    valueProviderCallback: (_) => {
                        Pfmxctrl_Ipktmaxctrl_ValueProvider(_);
                        return pfmxctrl_ipktmaxctrl_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pfmxctrl_Ipktmaxctrl_Write(_, __);
                    },
                    readCallback: (_, __) => Pfmxctrl_Ipktmaxctrl_Read(_, __),
                    name: "Ipktmaxctrl")
            .WithReservedBits(13, 19)
            .WithReadCallback((_, __) => Pfmxctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pfmxctrl_Write(_, __));
        
        // Transcfg - Offset : 0x24
        protected DoubleWordRegister  GenerateTranscfgRegister() => new DoubleWordRegister(this, 0x80000009)
            .WithEnumField<DoubleWordRegister, TRANSCFG_IPKVAL>(0, 4, out transcfg_ipkval_field, 
                    valueProviderCallback: (_) => {
                        Transcfg_Ipkval_ValueProvider(_);
                        return transcfg_ipkval_field.Value;               
                    },
                    writeCallback: (_, __) => Transcfg_Ipkval_Write(_, __),
                    readCallback: (_, __) => Transcfg_Ipkval_Read(_, __),
                    name: "Ipkval")
            .WithReservedBits(4, 27)
            .WithFlag(31, out transcfg_ipkvaltransen_bit, 
                    valueProviderCallback: (_) => {
                        Transcfg_Ipkvaltransen_ValueProvider(_);
                        return transcfg_ipkvaltransen_bit.Value;               
                    },
                    writeCallback: (_, __) => Transcfg_Ipkvaltransen_Write(_, __),
                    readCallback: (_, __) => Transcfg_Ipkvaltransen_Read(_, __),
                    name: "Ipkvaltransen")
            .WithReadCallback((_, __) => Transcfg_Read(_, __))
            .WithWriteCallback((_, __) => Transcfg_Write(_, __));
        
        // If - Offset : 0x28
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_bypsw_bit, 
                    valueProviderCallback: (_) => {
                        If_Bypsw_ValueProvider(_);
                        return if_bypsw_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Bypsw_Write(_, __),
                    readCallback: (_, __) => If_Bypsw_Read(_, __),
                    name: "Bypsw")
            .WithFlag(1, out if_warm_bit, 
                    valueProviderCallback: (_) => {
                        If_Warm_ValueProvider(_);
                        return if_warm_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Warm_Write(_, __),
                    readCallback: (_, __) => If_Warm_Read(_, __),
                    name: "Warm")
            .WithFlag(2, out if_running_bit, 
                    valueProviderCallback: (_) => {
                        If_Running_ValueProvider(_);
                        return if_running_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Running_Write(_, __),
                    readCallback: (_, __) => If_Running_Read(_, __),
                    name: "Running")
            .WithFlag(3, out if_vreginlow_bit, 
                    valueProviderCallback: (_) => {
                        If_Vreginlow_ValueProvider(_);
                        return if_vreginlow_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Vreginlow_Write(_, __),
                    readCallback: (_, __) => If_Vreginlow_Read(_, __),
                    name: "Vreginlow")
            .WithFlag(4, out if_vreginhigh_bit, 
                    valueProviderCallback: (_) => {
                        If_Vreginhigh_ValueProvider(_);
                        return if_vreginhigh_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Vreginhigh_Write(_, __),
                    readCallback: (_, __) => If_Vreginhigh_Read(_, __),
                    name: "Vreginhigh")
            .WithFlag(5, out if_regulation_bit, 
                    valueProviderCallback: (_) => {
                        If_Regulation_ValueProvider(_);
                        return if_regulation_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Regulation_Write(_, __),
                    readCallback: (_, __) => If_Regulation_Read(_, __),
                    name: "Regulation")
            .WithFlag(6, out if_tmax_bit, 
                    valueProviderCallback: (_) => {
                        If_Tmax_ValueProvider(_);
                        return if_tmax_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Tmax_Write(_, __),
                    readCallback: (_, __) => If_Tmax_Read(_, __),
                    name: "Tmax")
            .WithFlag(7, out if_em4err_bit, 
                    valueProviderCallback: (_) => {
                        If_Em4err_ValueProvider(_);
                        return if_em4err_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Em4err_Write(_, __),
                    readCallback: (_, __) => If_Em4err_Read(_, __),
                    name: "Em4err")
            .WithFlag(8, out if_ppmode_bit, 
                    valueProviderCallback: (_) => {
                        If_Ppmode_ValueProvider(_);
                        return if_ppmode_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Ppmode_Write(_, __),
                    readCallback: (_, __) => If_Ppmode_Read(_, __),
                    name: "Ppmode")
            .WithFlag(9, out if_pfmxmode_bit, 
                    valueProviderCallback: (_) => {
                        If_Pfmxmode_ValueProvider(_);
                        return if_pfmxmode_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Pfmxmode_Write(_, __),
                    readCallback: (_, __) => If_Pfmxmode_Read(_, __),
                    name: "Pfmxmode")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x2C
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_bypsw_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Bypsw_ValueProvider(_);
                        return ien_bypsw_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Bypsw_Write(_, __),
                    readCallback: (_, __) => Ien_Bypsw_Read(_, __),
                    name: "Bypsw")
            .WithFlag(1, out ien_warm_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Warm_ValueProvider(_);
                        return ien_warm_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Warm_Write(_, __),
                    readCallback: (_, __) => Ien_Warm_Read(_, __),
                    name: "Warm")
            .WithFlag(2, out ien_running_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Running_ValueProvider(_);
                        return ien_running_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Running_Write(_, __),
                    readCallback: (_, __) => Ien_Running_Read(_, __),
                    name: "Running")
            .WithFlag(3, out ien_vreginlow_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Vreginlow_ValueProvider(_);
                        return ien_vreginlow_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Vreginlow_Write(_, __),
                    readCallback: (_, __) => Ien_Vreginlow_Read(_, __),
                    name: "Vreginlow")
            .WithFlag(4, out ien_vreginhigh_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Vreginhigh_ValueProvider(_);
                        return ien_vreginhigh_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Vreginhigh_Write(_, __),
                    readCallback: (_, __) => Ien_Vreginhigh_Read(_, __),
                    name: "Vreginhigh")
            .WithFlag(5, out ien_regulation_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Regulation_ValueProvider(_);
                        return ien_regulation_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Regulation_Write(_, __),
                    readCallback: (_, __) => Ien_Regulation_Read(_, __),
                    name: "Regulation")
            .WithFlag(6, out ien_tmax_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Tmax_ValueProvider(_);
                        return ien_tmax_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Tmax_Write(_, __),
                    readCallback: (_, __) => Ien_Tmax_Read(_, __),
                    name: "Tmax")
            .WithFlag(7, out ien_em4err_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Em4err_ValueProvider(_);
                        return ien_em4err_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Em4err_Write(_, __),
                    readCallback: (_, __) => Ien_Em4err_Read(_, __),
                    name: "Em4err")
            .WithFlag(8, out ien_ppmode_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Ppmode_ValueProvider(_);
                        return ien_ppmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Ppmode_Write(_, __),
                    readCallback: (_, __) => Ien_Ppmode_Read(_, __),
                    name: "Ppmode")
            .WithFlag(9, out ien_pfmxmode_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Pfmxmode_ValueProvider(_);
                        return ien_pfmxmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Pfmxmode_Write(_, __),
                    readCallback: (_, __) => Ien_Pfmxmode_Read(_, __),
                    name: "Pfmxmode")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Status - Offset : 0x30
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_bypsw_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Bypsw_ValueProvider(_);
                        return status_bypsw_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Bypsw_Read(_, __),
                    name: "Bypsw")
            .WithFlag(1, out status_warm_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Warm_ValueProvider(_);
                        return status_warm_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Warm_Read(_, __),
                    name: "Warm")
            .WithFlag(2, out status_running_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Running_ValueProvider(_);
                        return status_running_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Running_Read(_, __),
                    name: "Running")
            .WithFlag(3, out status_vregin_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Vregin_ValueProvider(_);
                        return status_vregin_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Vregin_Read(_, __),
                    name: "Vregin")
            .WithFlag(4, out status_bypcmpout_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Bypcmpout_ValueProvider(_);
                        return status_bypcmpout_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Bypcmpout_Read(_, __),
                    name: "Bypcmpout")
            .WithReservedBits(5, 3)
            .WithFlag(8, out status_ppmode_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ppmode_ValueProvider(_);
                        return status_ppmode_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Ppmode_Read(_, __),
                    name: "Ppmode")
            .WithFlag(9, out status_pfmxmode_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Pfmxmode_ValueProvider(_);
                        return status_pfmxmode_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Pfmxmode_Read(_, __),
                    name: "Pfmxmode")
            .WithFlag(10, out status_hipwr_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Hipwr_ValueProvider(_);
                        return status_hipwr_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Hipwr_Read(_, __),
                    name: "Hipwr")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Syncbusy - Offset : 0x34
        protected DoubleWordRegister  GenerateSyncbusyRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_ctrl_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Ctrl_ValueProvider(_);
                        return syncbusy_ctrl_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Ctrl_Read(_, __),
                    name: "Ctrl")
            .WithFlag(1, out syncbusy_em01ctrl0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Em01ctrl0_ValueProvider(_);
                        return syncbusy_em01ctrl0_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Em01ctrl0_Read(_, __),
                    name: "Em01ctrl0")
            .WithFlag(2, out syncbusy_em01ctrl1_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Em01ctrl1_ValueProvider(_);
                        return syncbusy_em01ctrl1_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Em01ctrl1_Read(_, __),
                    name: "Em01ctrl1")
            .WithFlag(3, out syncbusy_em23ctrl0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Em23ctrl0_ValueProvider(_);
                        return syncbusy_em23ctrl0_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Em23ctrl0_Read(_, __),
                    name: "Em23ctrl0")
            .WithFlag(4, out syncbusy_trim0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Trim0_ValueProvider(_);
                        return syncbusy_trim0_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Trim0_Read(_, __),
                    name: "Trim0")
            .WithFlag(5, out syncbusy_trim2_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Trim2_ValueProvider(_);
                        return syncbusy_trim2_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Trim2_Read(_, __),
                    name: "Trim2")
            .WithFlag(6, out syncbusy_dcdcforce_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Dcdcforce_ValueProvider(_);
                        return syncbusy_dcdcforce_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Dcdcforce_Read(_, __),
                    name: "Dcdcforce")
            .WithFlag(7, out syncbusy_pfmxctrl_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Pfmxctrl_ValueProvider(_);
                        return syncbusy_pfmxctrl_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Pfmxctrl_Read(_, __),
                    name: "Pfmxctrl")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Syncbusy_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Write(_, __));
        
        // Lock_Lock - Offset : 0x40
        protected DoubleWordRegister  GenerateLock_lockRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out lock_lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Lock_Write(_, __));
        
        // Lockstatus_Lock - Offset : 0x44
        protected DoubleWordRegister  GenerateLockstatus_lockRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, LOCKSTATUS_LOCK_LOCK>(0, 1, out lockstatus_lock_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Lockstatus_Lock_Lock_ValueProvider(_);
                        return lockstatus_lock_lock_bit.Value;               
                    },
                    readCallback: (_, __) => Lockstatus_Lock_Lock_Read(_, __),
                    name: "Lock")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Lockstatus_Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lockstatus_Lock_Write(_, __));
        
        // Trim0_Feature - Offset : 0x50
        protected DoubleWordRegister  GenerateTrim0_featureRegister() => new DoubleWordRegister(this, 0xF)
            .WithValueField(0, 5, out trim0_feature_vcmptrim_field, 
                    valueProviderCallback: (_) => {
                        Trim0_Feature_Vcmptrim_ValueProvider(_);
                        return trim0_feature_vcmptrim_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim0_Feature_Vcmptrim_Write(_, __);
                    },
                    readCallback: (_, __) => Trim0_Feature_Vcmptrim_Read(_, __),
                    name: "Vcmptrim")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Trim0_Feature_Read(_, __))
            .WithWriteCallback((_, __) => Trim0_Feature_Write(_, __));
        
        // Trim1_Feature - Offset : 0x54
        protected DoubleWordRegister  GenerateTrim1_featureRegister() => new DoubleWordRegister(this, 0x7)
            .WithValueField(0, 4, out trim1_feature_rcotrim_field, 
                    valueProviderCallback: (_) => {
                        Trim1_Feature_Rcotrim_ValueProvider(_);
                        return trim1_feature_rcotrim_field.Value;               
                    },
                    writeCallback: (_, __) => Trim1_Feature_Rcotrim_Write(_, __),
                    readCallback: (_, __) => Trim1_Feature_Rcotrim_Read(_, __),
                    name: "Rcotrim")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Trim1_Feature_Read(_, __))
            .WithWriteCallback((_, __) => Trim1_Feature_Write(_, __));
        
        // Trim2_Feature - Offset : 0x58
        protected DoubleWordRegister  GenerateTrim2_featureRegister() => new DoubleWordRegister(this, 0x4)
            .WithValueField(0, 4, out trim2_feature_ipktrim_field, 
                    valueProviderCallback: (_) => {
                        Trim2_Feature_Ipktrim_ValueProvider(_);
                        return trim2_feature_ipktrim_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim2_Feature_Ipktrim_Write(_, __);
                    },
                    readCallback: (_, __) => Trim2_Feature_Ipktrim_Read(_, __),
                    name: "Ipktrim")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Trim2_Feature_Read(_, __))
            .WithWriteCallback((_, __) => Trim2_Feature_Write(_, __));
        
        // Cfg_Feature - Offset : 0x5C
        protected DoubleWordRegister  GenerateCfg_featureRegister() => new DoubleWordRegister(this, 0x80000010)
            .WithEnumField<DoubleWordRegister, CFG_FEATURE_EARLYRESETEN>(0, 1, out cfg_feature_earlyreseten_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Earlyreseten_ValueProvider(_);
                        return cfg_feature_earlyreseten_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Feature_Earlyreseten_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Feature_Earlyreseten_Read(_, __),
                    name: "Earlyreseten")
            .WithReservedBits(1, 3)
            .WithEnumField<DoubleWordRegister, CFG_FEATURE_VCMPVPROG>(4, 2, out cfg_feature_vcmpvprog_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Vcmpvprog_ValueProvider(_);
                        return cfg_feature_vcmpvprog_field.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Vcmpvprog_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Vcmpvprog_Read(_, __),
                    name: "Vcmpvprog")
            .WithReservedBits(6, 2)
            .WithEnumField<DoubleWordRegister, CFG_FEATURE_SWDRVDLY>(8, 1, out cfg_feature_swdrvdly_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Swdrvdly_ValueProvider(_);
                        return cfg_feature_swdrvdly_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Feature_Swdrvdly_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Feature_Swdrvdly_Read(_, __),
                    name: "Swdrvdly")
            .WithReservedBits(9, 7)
            .WithFlag(16, out cfg_feature_disnxmtoff_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Disnxmtoff_ValueProvider(_);
                        return cfg_feature_disnxmtoff_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Disnxmtoff_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Disnxmtoff_Read(_, __),
                    name: "Disnxmtoff")
            .WithFlag(17, out cfg_feature_ensccilim_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Ensccilim_ValueProvider(_);
                        return cfg_feature_ensccilim_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Ensccilim_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Ensccilim_Read(_, __),
                    name: "Ensccilim")
            .WithFlag(18, out cfg_feature_disricompduty_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Disricompduty_ValueProvider(_);
                        return cfg_feature_disricompduty_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Disricompduty_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Disricompduty_Read(_, __),
                    name: "Disricompduty")
            .WithFlag(19, out cfg_feature_redvswrc_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Redvswrc_ValueProvider(_);
                        return cfg_feature_redvswrc_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Redvswrc_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Redvswrc_Read(_, __),
                    name: "Redvswrc")
            .WithFlag(20, out cfg_feature_disricomp_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Disricomp_ValueProvider(_);
                        return cfg_feature_disricomp_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Disricomp_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Disricomp_Read(_, __),
                    name: "Disricomp")
            .WithFlag(21, out cfg_feature_disduty_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Disduty_ValueProvider(_);
                        return cfg_feature_disduty_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Disduty_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Disduty_Read(_, __),
                    name: "Disduty")
            .WithFlag(22, out cfg_feature_pfmxforceizstall_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Pfmxforceizstall_ValueProvider(_);
                        return cfg_feature_pfmxforceizstall_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Pfmxforceizstall_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Pfmxforceizstall_Read(_, __),
                    name: "Pfmxforceizstall")
            .WithFlag(23, out cfg_feature_enmtoffvcmprst_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Enmtoffvcmprst_ValueProvider(_);
                        return cfg_feature_enmtoffvcmprst_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Enmtoffvcmprst_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Enmtoffvcmprst_Read(_, __),
                    name: "Enmtoffvcmprst")
            .WithReservedBits(24, 7)
            .WithFlag(31, out cfg_feature_ppdlydblren_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Feature_Ppdlydblren_ValueProvider(_);
                        return cfg_feature_ppdlydblren_bit.Value;               
                    },
                    writeCallback: (_, __) => Cfg_Feature_Ppdlydblren_Write(_, __),
                    readCallback: (_, __) => Cfg_Feature_Ppdlydblren_Read(_, __),
                    name: "Ppdlydblren")
            .WithReadCallback((_, __) => Cfg_Feature_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Feature_Write(_, __));
        
        // Dcdcforce_Test - Offset : 0x70
        protected DoubleWordRegister  GenerateDcdcforce_testRegister() => new DoubleWordRegister(this, 0x8)
            .WithFlag(0, out dcdcforce_test_biasen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Biasen_ValueProvider(_);
                        return dcdcforce_test_biasen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcforce_Test_Biasen_Write(_, __),
                    readCallback: (_, __) => Dcdcforce_Test_Biasen_Read(_, __),
                    name: "Biasen")
            .WithFlag(1, out dcdcforce_test_dcdcvcmpen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Dcdcvcmpen_ValueProvider(_);
                        return dcdcforce_test_dcdcvcmpen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcforce_Test_Dcdcvcmpen_Write(_, __),
                    readCallback: (_, __) => Dcdcforce_Test_Dcdcvcmpen_Read(_, __),
                    name: "Dcdcvcmpen")
            .WithFlag(2, out dcdcforce_test_buckmodeen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Buckmodeen_ValueProvider(_);
                        return dcdcforce_test_buckmodeen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcforce_Test_Buckmodeen_Write(_, __),
                    readCallback: (_, __) => Dcdcforce_Test_Buckmodeen_Read(_, __),
                    name: "Buckmodeen")
            .WithFlag(3, out dcdcforce_test_bypswen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Bypswen_ValueProvider(_);
                        return dcdcforce_test_bypswen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcforce_Test_Bypswen_Write(_, __),
                    readCallback: (_, __) => Dcdcforce_Test_Bypswen_Read(_, __),
                    name: "Bypswen")
            .WithFlag(4, out dcdcforce_test_vcmpiboost_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Vcmpiboost_ValueProvider(_);
                        return dcdcforce_test_vcmpiboost_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcforce_Test_Vcmpiboost_Write(_, __),
                    readCallback: (_, __) => Dcdcforce_Test_Vcmpiboost_Read(_, __),
                    name: "Vcmpiboost")
            .WithFlag(5, out dcdcforce_test_pfmxmodeen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Pfmxmodeen_ValueProvider(_);
                        return dcdcforce_test_pfmxmodeen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcforce_Test_Pfmxmodeen_Write(_, __),
                    readCallback: (_, __) => Dcdcforce_Test_Pfmxmodeen_Read(_, __),
                    name: "Pfmxmodeen")
            .WithFlag(6, out dcdcforce_test_ppmodeen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Ppmodeen_ValueProvider(_);
                        return dcdcforce_test_ppmodeen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcforce_Test_Ppmodeen_Write(_, __),
                    readCallback: (_, __) => Dcdcforce_Test_Ppmodeen_Read(_, __),
                    name: "Ppmodeen")
            .WithReservedBits(7, 1)
            .WithFlag(8, out dcdcforce_test_ipkdacem2_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Ipkdacem2_ValueProvider(_);
                        return dcdcforce_test_ipkdacem2_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Dcdcforce_Test_Ipkdacem2_Write(_, __);
                    },
                    readCallback: (_, __) => Dcdcforce_Test_Ipkdacem2_Read(_, __),
                    name: "Ipkdacem2")
            .WithReservedBits(9, 22)
            .WithFlag(31, out dcdcforce_test_forceen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcforce_Test_Forceen_ValueProvider(_);
                        return dcdcforce_test_forceen_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Dcdcforce_Test_Forceen_Write(_, __);
                    },
                    readCallback: (_, __) => Dcdcforce_Test_Forceen_Read(_, __),
                    name: "Forceen")
            .WithReadCallback((_, __) => Dcdcforce_Test_Read(_, __))
            .WithWriteCallback((_, __) => Dcdcforce_Test_Write(_, __));
        
        // Dbustest_Test - Offset : 0x74
        protected DoubleWordRegister  GenerateDbustest_testRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dbustest_test_testen_bit, 
                    valueProviderCallback: (_) => {
                        Dbustest_Test_Testen_ValueProvider(_);
                        return dbustest_test_testen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dbustest_Test_Testen_Write(_, __),
                    readCallback: (_, __) => Dbustest_Test_Testen_Read(_, __),
                    name: "Testen")
            .WithEnumField<DoubleWordRegister, DBUSTEST_TEST_TESTSEL>(1, 4, out dbustest_test_testsel_field, 
                    valueProviderCallback: (_) => {
                        Dbustest_Test_Testsel_ValueProvider(_);
                        return dbustest_test_testsel_field.Value;               
                    },
                    writeCallback: (_, __) => Dbustest_Test_Testsel_Write(_, __),
                    readCallback: (_, __) => Dbustest_Test_Testsel_Read(_, __),
                    name: "Testsel")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Dbustest_Test_Read(_, __))
            .WithWriteCallback((_, __) => Dbustest_Test_Write(_, __));
        
        // Dcdcvcmptest_Test - Offset : 0x78
        protected DoubleWordRegister  GenerateDcdcvcmptest_testRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dcdcvcmptest_test_vcmpbufforce_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcvcmptest_Test_Vcmpbufforce_ValueProvider(_);
                        return dcdcvcmptest_test_vcmpbufforce_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcvcmptest_Test_Vcmpbufforce_Write(_, __),
                    readCallback: (_, __) => Dcdcvcmptest_Test_Vcmpbufforce_Read(_, __),
                    name: "Vcmpbufforce")
            .WithEnumField<DoubleWordRegister, DCDCVCMPTEST_TEST_VCMPCMPFORCE>(1, 2, out dcdcvcmptest_test_vcmpcmpforce_field, 
                    valueProviderCallback: (_) => {
                        Dcdcvcmptest_Test_Vcmpcmpforce_ValueProvider(_);
                        return dcdcvcmptest_test_vcmpcmpforce_field.Value;               
                    },
                    writeCallback: (_, __) => Dcdcvcmptest_Test_Vcmpcmpforce_Write(_, __),
                    readCallback: (_, __) => Dcdcvcmptest_Test_Vcmpcmpforce_Read(_, __),
                    name: "Vcmpcmpforce")
            .WithReservedBits(3, 28)
            .WithFlag(31, out dcdcvcmptest_test_vcmpcalen_bit, 
                    valueProviderCallback: (_) => {
                        Dcdcvcmptest_Test_Vcmpcalen_ValueProvider(_);
                        return dcdcvcmptest_test_vcmpcalen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdcvcmptest_Test_Vcmpcalen_Write(_, __),
                    readCallback: (_, __) => Dcdcvcmptest_Test_Vcmpcalen_Read(_, __),
                    name: "Vcmpcalen")
            .WithReadCallback((_, __) => Dcdcvcmptest_Test_Read(_, __))
            .WithWriteCallback((_, __) => Dcdcvcmptest_Test_Write(_, __));
        
        // Izctest_Test - Offset : 0x7C
        protected DoubleWordRegister  GenerateIzctest_testRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, IZCTEST_TEST_IZCOUNTERIN>(0, 1, out izctest_test_izcounterin_bit, 
                    valueProviderCallback: (_) => {
                        Izctest_Test_Izcounterin_ValueProvider(_);
                        return izctest_test_izcounterin_bit.Value;               
                    },
                    writeCallback: (_, __) => Izctest_Test_Izcounterin_Write(_, __),
                    readCallback: (_, __) => Izctest_Test_Izcounterin_Read(_, __),
                    name: "Izcounterin")
            .WithEnumField<DoubleWordRegister, IZCTEST_TEST_IZNFETREPPD>(1, 1, out izctest_test_iznfetreppd_bit, 
                    valueProviderCallback: (_) => {
                        Izctest_Test_Iznfetreppd_ValueProvider(_);
                        return izctest_test_iznfetreppd_bit.Value;               
                    },
                    writeCallback: (_, __) => Izctest_Test_Iznfetreppd_Write(_, __),
                    readCallback: (_, __) => Izctest_Test_Iznfetreppd_Read(_, __),
                    name: "Iznfetreppd")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Izctest_Test_Read(_, __))
            .WithWriteCallback((_, __) => Izctest_Test_Write(_, __));
        
        // Dcdctest_Test - Offset : 0x80
        protected DoubleWordRegister  GenerateDcdctest_testRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dcdctest_test_swnfeton_bit, 
                    valueProviderCallback: (_) => {
                        Dcdctest_Test_Swnfeton_ValueProvider(_);
                        return dcdctest_test_swnfeton_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdctest_Test_Swnfeton_Write(_, __),
                    readCallback: (_, __) => Dcdctest_Test_Swnfeton_Read(_, __),
                    name: "Swnfeton")
            .WithFlag(1, out dcdctest_test_conten_bit, 
                    valueProviderCallback: (_) => {
                        Dcdctest_Test_Conten_ValueProvider(_);
                        return dcdctest_test_conten_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdctest_Test_Conten_Write(_, __),
                    readCallback: (_, __) => Dcdctest_Test_Conten_Read(_, __),
                    name: "Conten")
            .WithFlag(2, out dcdctest_test_tocmode_bit, 
                    valueProviderCallback: (_) => {
                        Dcdctest_Test_Tocmode_ValueProvider(_);
                        return dcdctest_test_tocmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdctest_Test_Tocmode_Write(_, __),
                    readCallback: (_, __) => Dcdctest_Test_Tocmode_Read(_, __),
                    name: "Tocmode")
            .WithFlag(3, out dcdctest_test_toctrig_bit, 
                    valueProviderCallback: (_) => {
                        Dcdctest_Test_Toctrig_ValueProvider(_);
                        return dcdctest_test_toctrig_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdctest_Test_Toctrig_Write(_, __),
                    readCallback: (_, __) => Dcdctest_Test_Toctrig_Read(_, __),
                    name: "Toctrig")
            .WithFlag(4, out dcdctest_test_tocfreerun_bit, 
                    valueProviderCallback: (_) => {
                        Dcdctest_Test_Tocfreerun_ValueProvider(_);
                        return dcdctest_test_tocfreerun_bit.Value;               
                    },
                    writeCallback: (_, __) => Dcdctest_Test_Tocfreerun_Write(_, __),
                    readCallback: (_, __) => Dcdctest_Test_Tocfreerun_Read(_, __),
                    name: "Tocfreerun")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Dcdctest_Test_Read(_, __))
            .WithWriteCallback((_, __) => Dcdctest_Test_Write(_, __));
        
        // Teststatus0_Test - Offset : 0x84
        protected DoubleWordRegister  GenerateTeststatus0_testRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out teststatus0_test_biasen_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Teststatus0_Test_Biasen_ValueProvider(_);
                        return teststatus0_test_biasen_bit.Value;               
                    },
                    readCallback: (_, __) => Teststatus0_Test_Biasen_Read(_, __),
                    name: "Biasen")
            .WithFlag(1, out teststatus0_test_dcdcvcmpen_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Teststatus0_Test_Dcdcvcmpen_ValueProvider(_);
                        return teststatus0_test_dcdcvcmpen_bit.Value;               
                    },
                    readCallback: (_, __) => Teststatus0_Test_Dcdcvcmpen_Read(_, __),
                    name: "Dcdcvcmpen")
            .WithFlag(2, out teststatus0_test_buckmodeen_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Teststatus0_Test_Buckmodeen_ValueProvider(_);
                        return teststatus0_test_buckmodeen_bit.Value;               
                    },
                    readCallback: (_, __) => Teststatus0_Test_Buckmodeen_Read(_, __),
                    name: "Buckmodeen")
            .WithEnumField<DoubleWordRegister, TESTSTATUS0_TEST_UPDIPKDAC>(3, 1, out teststatus0_test_updipkdac_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Teststatus0_Test_Updipkdac_ValueProvider(_);
                        return teststatus0_test_updipkdac_bit.Value;               
                    },
                    readCallback: (_, __) => Teststatus0_Test_Updipkdac_Read(_, __),
                    name: "Updipkdac")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Teststatus0_Test_Read(_, __))
            .WithWriteCallback((_, __) => Teststatus0_Test_Write(_, __));
        
        // Teststatus1_Test - Offset : 0x88
        protected DoubleWordRegister  GenerateTeststatus1_testRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 5, out teststatus1_test_izvalread_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Teststatus1_Test_Izvalread_ValueProvider(_);
                        return teststatus1_test_izvalread_field.Value;               
                    },
                    readCallback: (_, __) => Teststatus1_Test_Izvalread_Read(_, __),
                    name: "Izvalread")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Teststatus1_Test_Read(_, __))
            .WithWriteCallback((_, __) => Teststatus1_Test_Write(_, __));
        
        // Teststatus2_Test - Offset : 0x8C
        protected DoubleWordRegister  GenerateTeststatus2_testRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 6, out teststatus2_test_ipkdac_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Teststatus2_Test_Ipkdac_ValueProvider(_);
                        return teststatus2_test_ipkdac_field.Value;               
                    },
                    readCallback: (_, __) => Teststatus2_Test_Ipkdac_Read(_, __),
                    name: "Ipkdac")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Teststatus2_Test_Read(_, __))
            .WithWriteCallback((_, __) => Teststatus2_Test_Write(_, __));
        
        // Rpuratd0_Drpu - Offset : 0x90
        protected DoubleWordRegister  GenerateRpuratd0_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out rpuratd0_drpu_ratdctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdctrl_Read(_, __),
                    name: "Ratdctrl")
            .WithFlag(2, out rpuratd0_drpu_ratdem01ctrl0_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdem01ctrl0_ValueProvider(_);
                        return rpuratd0_drpu_ratdem01ctrl0_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdem01ctrl0_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdem01ctrl0_Read(_, __),
                    name: "Ratdem01ctrl0")
            .WithFlag(3, out rpuratd0_drpu_ratdizctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdizctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdizctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdizctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdizctrl_Read(_, __),
                    name: "Ratdizctrl")
            .WithFlag(4, out rpuratd0_drpu_ratdem23ctrl0_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdem23ctrl0_ValueProvider(_);
                        return rpuratd0_drpu_ratdem23ctrl0_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdem23ctrl0_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdem23ctrl0_Read(_, __),
                    name: "Ratdem23ctrl0")
            .WithFlag(5, out rpuratd0_drpu_ratdem01ctrl1_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdem01ctrl1_ValueProvider(_);
                        return rpuratd0_drpu_ratdem01ctrl1_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdem01ctrl1_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdem01ctrl1_Read(_, __),
                    name: "Ratdem01ctrl1")
            .WithFlag(6, out rpuratd0_drpu_ratdem23ctrl1_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdem23ctrl1_ValueProvider(_);
                        return rpuratd0_drpu_ratdem23ctrl1_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdem23ctrl1_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdem23ctrl1_Read(_, __),
                    name: "Ratdem23ctrl1")
            .WithFlag(7, out rpuratd0_drpu_ratdppcfg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdppcfg_ValueProvider(_);
                        return rpuratd0_drpu_ratdppcfg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdppcfg_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdppcfg_Read(_, __),
                    name: "Ratdppcfg")
            .WithFlag(8, out rpuratd0_drpu_ratdpfmxctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdpfmxctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdpfmxctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdpfmxctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdpfmxctrl_Read(_, __),
                    name: "Ratdpfmxctrl")
            .WithFlag(9, out rpuratd0_drpu_ratdtranscfg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtranscfg_ValueProvider(_);
                        return rpuratd0_drpu_ratdtranscfg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtranscfg_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtranscfg_Read(_, __),
                    name: "Ratdtranscfg")
            .WithFlag(10, out rpuratd0_drpu_ratdif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdif_ValueProvider(_);
                        return rpuratd0_drpu_ratdif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdif_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdif_Read(_, __),
                    name: "Ratdif")
            .WithFlag(11, out rpuratd0_drpu_ratdien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdien_ValueProvider(_);
                        return rpuratd0_drpu_ratdien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdien_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdien_Read(_, __),
                    name: "Ratdien")
            .WithReservedBits(12, 4)
            .WithFlag(16, out rpuratd0_drpu_ratdlock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdlock_ValueProvider(_);
                        return rpuratd0_drpu_ratdlock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdlock_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdlock_Read(_, __),
                    name: "Ratdlock")
            .WithReservedBits(17, 3)
            .WithFlag(20, out rpuratd0_drpu_ratdtrim0_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtrim0_ValueProvider(_);
                        return rpuratd0_drpu_ratdtrim0_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtrim0_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtrim0_Read(_, __),
                    name: "Ratdtrim0")
            .WithFlag(21, out rpuratd0_drpu_ratdtrim1_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtrim1_ValueProvider(_);
                        return rpuratd0_drpu_ratdtrim1_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtrim1_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtrim1_Read(_, __),
                    name: "Ratdtrim1")
            .WithFlag(22, out rpuratd0_drpu_ratdtrim2_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtrim2_ValueProvider(_);
                        return rpuratd0_drpu_ratdtrim2_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtrim2_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtrim2_Read(_, __),
                    name: "Ratdtrim2")
            .WithFlag(23, out rpuratd0_drpu_ratdcfg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdcfg_ValueProvider(_);
                        return rpuratd0_drpu_ratdcfg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdcfg_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdcfg_Read(_, __),
                    name: "Ratdcfg")
            .WithReservedBits(24, 4)
            .WithFlag(28, out rpuratd0_drpu_ratddcdcforce_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratddcdcforce_ValueProvider(_);
                        return rpuratd0_drpu_ratddcdcforce_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratddcdcforce_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratddcdcforce_Read(_, __),
                    name: "Ratddcdcforce")
            .WithFlag(29, out rpuratd0_drpu_ratddbustest_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratddbustest_ValueProvider(_);
                        return rpuratd0_drpu_ratddbustest_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratddbustest_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratddbustest_Read(_, __),
                    name: "Ratddbustest")
            .WithFlag(30, out rpuratd0_drpu_ratddcdcvcmptest_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratddcdcvcmptest_ValueProvider(_);
                        return rpuratd0_drpu_ratddcdcvcmptest_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratddcdcvcmptest_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratddcdcvcmptest_Read(_, __),
                    name: "Ratddcdcvcmptest")
            .WithFlag(31, out rpuratd0_drpu_ratdizctest_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdizctest_ValueProvider(_);
                        return rpuratd0_drpu_ratdizctest_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdizctest_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdizctest_Read(_, __),
                    name: "Ratdizctest")
            .WithReadCallback((_, __) => Rpuratd0_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd0_Drpu_Write(_, __));
        
        // Rpuratd1_Drpu - Offset : 0x94
        protected DoubleWordRegister  GenerateRpuratd1_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd1_drpu_ratddcdctest_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratddcdctest_ValueProvider(_);
                        return rpuratd1_drpu_ratddcdctest_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratddcdctest_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratddcdctest_Read(_, __),
                    name: "Ratddcdctest")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Rpuratd1_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd1_Drpu_Write(_, __));
        

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


        // Ipversion - Offset : 0x0
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);

        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        // Ctrl - Offset : 0x4
        protected IEnumRegisterField<CTRL_MODE> ctrl_mode_bit;
        partial void Ctrl_Mode_Write(CTRL_MODE a, CTRL_MODE b);
        partial void Ctrl_Mode_Read(CTRL_MODE a, CTRL_MODE b);
        partial void Ctrl_Mode_ValueProvider(CTRL_MODE a);
        protected IValueRegisterField ctrl_ipktmaxctrl_field;
        partial void Ctrl_Ipktmaxctrl_Write(ulong a, ulong b);
        partial void Ctrl_Ipktmaxctrl_Read(ulong a, ulong b);
        partial void Ctrl_Ipktmaxctrl_ValueProvider(ulong a);
        protected IEnumRegisterField<CTRL_PFMXEXTREQ> ctrl_pfmxextreq_bit;
        partial void Ctrl_Pfmxextreq_Write(CTRL_PFMXEXTREQ a, CTRL_PFMXEXTREQ b);
        partial void Ctrl_Pfmxextreq_Read(CTRL_PFMXEXTREQ a, CTRL_PFMXEXTREQ b);
        partial void Ctrl_Pfmxextreq_ValueProvider(CTRL_PFMXEXTREQ a);

        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Em01ctrl0 - Offset : 0x8
        protected IEnumRegisterField<EM01CTRL0_IPKVAL> em01ctrl0_ipkval_field;
        partial void Em01ctrl0_Ipkval_Write(EM01CTRL0_IPKVAL a, EM01CTRL0_IPKVAL b);
        partial void Em01ctrl0_Ipkval_Read(EM01CTRL0_IPKVAL a, EM01CTRL0_IPKVAL b);
        partial void Em01ctrl0_Ipkval_ValueProvider(EM01CTRL0_IPKVAL a);
        protected IEnumRegisterField<EM01CTRL0_DRVSPEED> em01ctrl0_drvspeed_field;
        partial void Em01ctrl0_Drvspeed_Write(EM01CTRL0_DRVSPEED a, EM01CTRL0_DRVSPEED b);
        partial void Em01ctrl0_Drvspeed_Read(EM01CTRL0_DRVSPEED a, EM01CTRL0_DRVSPEED b);
        partial void Em01ctrl0_Drvspeed_ValueProvider(EM01CTRL0_DRVSPEED a);

        partial void Em01ctrl0_Write(uint a, uint b);
        partial void Em01ctrl0_Read(uint a, uint b);
        
        // Izctrl - Offset : 0xC
        protected IEnumRegisterField<IZCTRL_IZSTALL> izctrl_izstall_bit;
        partial void Izctrl_Izstall_Write(IZCTRL_IZSTALL a, IZCTRL_IZSTALL b);
        partial void Izctrl_Izstall_Read(IZCTRL_IZSTALL a, IZCTRL_IZSTALL b);
        partial void Izctrl_Izstall_ValueProvider(IZCTRL_IZSTALL a);
        protected IEnumRegisterField<IZCTRL_IZFIXEDEN> izctrl_izfixeden_bit;
        partial void Izctrl_Izfixeden_Write(IZCTRL_IZFIXEDEN a, IZCTRL_IZFIXEDEN b);
        partial void Izctrl_Izfixeden_Read(IZCTRL_IZFIXEDEN a, IZCTRL_IZFIXEDEN b);
        partial void Izctrl_Izfixeden_ValueProvider(IZCTRL_IZFIXEDEN a);
        protected IValueRegisterField izctrl_izval_field;
        partial void Izctrl_Izval_Write(ulong a, ulong b);
        partial void Izctrl_Izval_Read(ulong a, ulong b);
        partial void Izctrl_Izval_ValueProvider(ulong a);

        partial void Izctrl_Write(uint a, uint b);
        partial void Izctrl_Read(uint a, uint b);
        
        // Em23ctrl0 - Offset : 0x10
        protected IEnumRegisterField<EM23CTRL0_IPKVAL> em23ctrl0_ipkval_field;
        partial void Em23ctrl0_Ipkval_Write(EM23CTRL0_IPKVAL a, EM23CTRL0_IPKVAL b);
        partial void Em23ctrl0_Ipkval_Read(EM23CTRL0_IPKVAL a, EM23CTRL0_IPKVAL b);
        partial void Em23ctrl0_Ipkval_ValueProvider(EM23CTRL0_IPKVAL a);
        protected IEnumRegisterField<EM23CTRL0_DRVSPEED> em23ctrl0_drvspeed_field;
        partial void Em23ctrl0_Drvspeed_Write(EM23CTRL0_DRVSPEED a, EM23CTRL0_DRVSPEED b);
        partial void Em23ctrl0_Drvspeed_Read(EM23CTRL0_DRVSPEED a, EM23CTRL0_DRVSPEED b);
        partial void Em23ctrl0_Drvspeed_ValueProvider(EM23CTRL0_DRVSPEED a);

        partial void Em23ctrl0_Write(uint a, uint b);
        partial void Em23ctrl0_Read(uint a, uint b);
        
        // Em01ctrl1 - Offset : 0x14
        protected IEnumRegisterField<EM01CTRL1_VCMPIBIAS> em01ctrl1_vcmpibias_field;
        partial void Em01ctrl1_Vcmpibias_Write(EM01CTRL1_VCMPIBIAS a, EM01CTRL1_VCMPIBIAS b);
        partial void Em01ctrl1_Vcmpibias_Read(EM01CTRL1_VCMPIBIAS a, EM01CTRL1_VCMPIBIAS b);
        partial void Em01ctrl1_Vcmpibias_ValueProvider(EM01CTRL1_VCMPIBIAS a);

        partial void Em01ctrl1_Write(uint a, uint b);
        partial void Em01ctrl1_Read(uint a, uint b);
        
        // Em23ctrl1 - Offset : 0x18
        protected IEnumRegisterField<EM23CTRL1_VCMPIBIAS> em23ctrl1_vcmpibias_field;
        partial void Em23ctrl1_Vcmpibias_Write(EM23CTRL1_VCMPIBIAS a, EM23CTRL1_VCMPIBIAS b);
        partial void Em23ctrl1_Vcmpibias_Read(EM23CTRL1_VCMPIBIAS a, EM23CTRL1_VCMPIBIAS b);
        partial void Em23ctrl1_Vcmpibias_ValueProvider(EM23CTRL1_VCMPIBIAS a);
        protected IEnumRegisterField<EM23CTRL1_TEMPBIASCTRL> em23ctrl1_tempbiasctrl_field;
        partial void Em23ctrl1_Tempbiasctrl_Write(EM23CTRL1_TEMPBIASCTRL a, EM23CTRL1_TEMPBIASCTRL b);
        partial void Em23ctrl1_Tempbiasctrl_Read(EM23CTRL1_TEMPBIASCTRL a, EM23CTRL1_TEMPBIASCTRL b);
        partial void Em23ctrl1_Tempbiasctrl_ValueProvider(EM23CTRL1_TEMPBIASCTRL a);

        partial void Em23ctrl1_Write(uint a, uint b);
        partial void Em23ctrl1_Read(uint a, uint b);
        
        // Ppcfg - Offset : 0x1C
        protected IValueRegisterField ppcfg_ipkval_field;
        partial void Ppcfg_Ipkval_Write(ulong a, ulong b);
        partial void Ppcfg_Ipkval_Read(ulong a, ulong b);
        partial void Ppcfg_Ipkval_ValueProvider(ulong a);
        protected IValueRegisterField ppcfg_ipktmaxctrl_field;
        partial void Ppcfg_Ipktmaxctrl_Write(ulong a, ulong b);
        partial void Ppcfg_Ipktmaxctrl_Read(ulong a, ulong b);
        partial void Ppcfg_Ipktmaxctrl_ValueProvider(ulong a);
        protected IEnumRegisterField<PPCFG_DRVSPEED> ppcfg_drvspeed_field;
        partial void Ppcfg_Drvspeed_Write(PPCFG_DRVSPEED a, PPCFG_DRVSPEED b);
        partial void Ppcfg_Drvspeed_Read(PPCFG_DRVSPEED a, PPCFG_DRVSPEED b);
        partial void Ppcfg_Drvspeed_ValueProvider(PPCFG_DRVSPEED a);

        partial void Ppcfg_Write(uint a, uint b);
        partial void Ppcfg_Read(uint a, uint b);
        
        // Pfmxctrl - Offset : 0x20
        protected IValueRegisterField pfmxctrl_ipkval_field;
        partial void Pfmxctrl_Ipkval_Write(ulong a, ulong b);
        partial void Pfmxctrl_Ipkval_Read(ulong a, ulong b);
        partial void Pfmxctrl_Ipkval_ValueProvider(ulong a);
        protected IValueRegisterField pfmxctrl_ipktmaxctrl_field;
        partial void Pfmxctrl_Ipktmaxctrl_Write(ulong a, ulong b);
        partial void Pfmxctrl_Ipktmaxctrl_Read(ulong a, ulong b);
        partial void Pfmxctrl_Ipktmaxctrl_ValueProvider(ulong a);

        partial void Pfmxctrl_Write(uint a, uint b);
        partial void Pfmxctrl_Read(uint a, uint b);
        
        // Transcfg - Offset : 0x24
        protected IEnumRegisterField<TRANSCFG_IPKVAL> transcfg_ipkval_field;
        partial void Transcfg_Ipkval_Write(TRANSCFG_IPKVAL a, TRANSCFG_IPKVAL b);
        partial void Transcfg_Ipkval_Read(TRANSCFG_IPKVAL a, TRANSCFG_IPKVAL b);
        partial void Transcfg_Ipkval_ValueProvider(TRANSCFG_IPKVAL a);
        protected IFlagRegisterField transcfg_ipkvaltransen_bit;
        partial void Transcfg_Ipkvaltransen_Write(bool a, bool b);
        partial void Transcfg_Ipkvaltransen_Read(bool a, bool b);
        partial void Transcfg_Ipkvaltransen_ValueProvider(bool a);

        partial void Transcfg_Write(uint a, uint b);
        partial void Transcfg_Read(uint a, uint b);
        
        // If - Offset : 0x28
        protected IFlagRegisterField if_bypsw_bit;
        partial void If_Bypsw_Write(bool a, bool b);
        partial void If_Bypsw_Read(bool a, bool b);
        partial void If_Bypsw_ValueProvider(bool a);
        protected IFlagRegisterField if_warm_bit;
        partial void If_Warm_Write(bool a, bool b);
        partial void If_Warm_Read(bool a, bool b);
        partial void If_Warm_ValueProvider(bool a);
        protected IFlagRegisterField if_running_bit;
        partial void If_Running_Write(bool a, bool b);
        partial void If_Running_Read(bool a, bool b);
        partial void If_Running_ValueProvider(bool a);
        protected IFlagRegisterField if_vreginlow_bit;
        partial void If_Vreginlow_Write(bool a, bool b);
        partial void If_Vreginlow_Read(bool a, bool b);
        partial void If_Vreginlow_ValueProvider(bool a);
        protected IFlagRegisterField if_vreginhigh_bit;
        partial void If_Vreginhigh_Write(bool a, bool b);
        partial void If_Vreginhigh_Read(bool a, bool b);
        partial void If_Vreginhigh_ValueProvider(bool a);
        protected IFlagRegisterField if_regulation_bit;
        partial void If_Regulation_Write(bool a, bool b);
        partial void If_Regulation_Read(bool a, bool b);
        partial void If_Regulation_ValueProvider(bool a);
        protected IFlagRegisterField if_tmax_bit;
        partial void If_Tmax_Write(bool a, bool b);
        partial void If_Tmax_Read(bool a, bool b);
        partial void If_Tmax_ValueProvider(bool a);
        protected IFlagRegisterField if_em4err_bit;
        partial void If_Em4err_Write(bool a, bool b);
        partial void If_Em4err_Read(bool a, bool b);
        partial void If_Em4err_ValueProvider(bool a);
        protected IFlagRegisterField if_ppmode_bit;
        partial void If_Ppmode_Write(bool a, bool b);
        partial void If_Ppmode_Read(bool a, bool b);
        partial void If_Ppmode_ValueProvider(bool a);
        protected IFlagRegisterField if_pfmxmode_bit;
        partial void If_Pfmxmode_Write(bool a, bool b);
        partial void If_Pfmxmode_Read(bool a, bool b);
        partial void If_Pfmxmode_ValueProvider(bool a);

        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x2C
        protected IFlagRegisterField ien_bypsw_bit;
        partial void Ien_Bypsw_Write(bool a, bool b);
        partial void Ien_Bypsw_Read(bool a, bool b);
        partial void Ien_Bypsw_ValueProvider(bool a);
        protected IFlagRegisterField ien_warm_bit;
        partial void Ien_Warm_Write(bool a, bool b);
        partial void Ien_Warm_Read(bool a, bool b);
        partial void Ien_Warm_ValueProvider(bool a);
        protected IFlagRegisterField ien_running_bit;
        partial void Ien_Running_Write(bool a, bool b);
        partial void Ien_Running_Read(bool a, bool b);
        partial void Ien_Running_ValueProvider(bool a);
        protected IFlagRegisterField ien_vreginlow_bit;
        partial void Ien_Vreginlow_Write(bool a, bool b);
        partial void Ien_Vreginlow_Read(bool a, bool b);
        partial void Ien_Vreginlow_ValueProvider(bool a);
        protected IFlagRegisterField ien_vreginhigh_bit;
        partial void Ien_Vreginhigh_Write(bool a, bool b);
        partial void Ien_Vreginhigh_Read(bool a, bool b);
        partial void Ien_Vreginhigh_ValueProvider(bool a);
        protected IFlagRegisterField ien_regulation_bit;
        partial void Ien_Regulation_Write(bool a, bool b);
        partial void Ien_Regulation_Read(bool a, bool b);
        partial void Ien_Regulation_ValueProvider(bool a);
        protected IFlagRegisterField ien_tmax_bit;
        partial void Ien_Tmax_Write(bool a, bool b);
        partial void Ien_Tmax_Read(bool a, bool b);
        partial void Ien_Tmax_ValueProvider(bool a);
        protected IFlagRegisterField ien_em4err_bit;
        partial void Ien_Em4err_Write(bool a, bool b);
        partial void Ien_Em4err_Read(bool a, bool b);
        partial void Ien_Em4err_ValueProvider(bool a);
        protected IFlagRegisterField ien_ppmode_bit;
        partial void Ien_Ppmode_Write(bool a, bool b);
        partial void Ien_Ppmode_Read(bool a, bool b);
        partial void Ien_Ppmode_ValueProvider(bool a);
        protected IFlagRegisterField ien_pfmxmode_bit;
        partial void Ien_Pfmxmode_Write(bool a, bool b);
        partial void Ien_Pfmxmode_Read(bool a, bool b);
        partial void Ien_Pfmxmode_ValueProvider(bool a);

        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Status - Offset : 0x30
        protected IFlagRegisterField status_bypsw_bit;
        partial void Status_Bypsw_Read(bool a, bool b);
        partial void Status_Bypsw_ValueProvider(bool a);
        protected IFlagRegisterField status_warm_bit;
        partial void Status_Warm_Read(bool a, bool b);
        partial void Status_Warm_ValueProvider(bool a);
        protected IFlagRegisterField status_running_bit;
        partial void Status_Running_Read(bool a, bool b);
        partial void Status_Running_ValueProvider(bool a);
        protected IFlagRegisterField status_vregin_bit;
        partial void Status_Vregin_Read(bool a, bool b);
        partial void Status_Vregin_ValueProvider(bool a);
        protected IFlagRegisterField status_bypcmpout_bit;
        partial void Status_Bypcmpout_Read(bool a, bool b);
        partial void Status_Bypcmpout_ValueProvider(bool a);
        protected IFlagRegisterField status_ppmode_bit;
        partial void Status_Ppmode_Read(bool a, bool b);
        partial void Status_Ppmode_ValueProvider(bool a);
        protected IFlagRegisterField status_pfmxmode_bit;
        partial void Status_Pfmxmode_Read(bool a, bool b);
        partial void Status_Pfmxmode_ValueProvider(bool a);
        protected IFlagRegisterField status_hipwr_bit;
        partial void Status_Hipwr_Read(bool a, bool b);
        partial void Status_Hipwr_ValueProvider(bool a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // Syncbusy - Offset : 0x34
        protected IFlagRegisterField syncbusy_ctrl_bit;
        partial void Syncbusy_Ctrl_Read(bool a, bool b);
        partial void Syncbusy_Ctrl_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_em01ctrl0_bit;
        partial void Syncbusy_Em01ctrl0_Read(bool a, bool b);
        partial void Syncbusy_Em01ctrl0_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_em01ctrl1_bit;
        partial void Syncbusy_Em01ctrl1_Read(bool a, bool b);
        partial void Syncbusy_Em01ctrl1_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_em23ctrl0_bit;
        partial void Syncbusy_Em23ctrl0_Read(bool a, bool b);
        partial void Syncbusy_Em23ctrl0_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_trim0_bit;
        partial void Syncbusy_Trim0_Read(bool a, bool b);
        partial void Syncbusy_Trim0_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_trim2_bit;
        partial void Syncbusy_Trim2_Read(bool a, bool b);
        partial void Syncbusy_Trim2_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_dcdcforce_bit;
        partial void Syncbusy_Dcdcforce_Read(bool a, bool b);
        partial void Syncbusy_Dcdcforce_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_pfmxctrl_bit;
        partial void Syncbusy_Pfmxctrl_Read(bool a, bool b);
        partial void Syncbusy_Pfmxctrl_ValueProvider(bool a);

        partial void Syncbusy_Write(uint a, uint b);
        partial void Syncbusy_Read(uint a, uint b);
        
        // Lock_Lock - Offset : 0x40
        protected IValueRegisterField lock_lock_lockkey_field;
        partial void Lock_Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Lock_Write(uint a, uint b);
        partial void Lock_Lock_Read(uint a, uint b);
        
        // Lockstatus_Lock - Offset : 0x44
        protected IEnumRegisterField<LOCKSTATUS_LOCK_LOCK> lockstatus_lock_lock_bit;
        partial void Lockstatus_Lock_Lock_Read(LOCKSTATUS_LOCK_LOCK a, LOCKSTATUS_LOCK_LOCK b);
        partial void Lockstatus_Lock_Lock_ValueProvider(LOCKSTATUS_LOCK_LOCK a);

        partial void Lockstatus_Lock_Write(uint a, uint b);
        partial void Lockstatus_Lock_Read(uint a, uint b);
        
        // Trim0_Feature - Offset : 0x50
        protected IValueRegisterField trim0_feature_vcmptrim_field;
        partial void Trim0_Feature_Vcmptrim_Write(ulong a, ulong b);
        partial void Trim0_Feature_Vcmptrim_Read(ulong a, ulong b);
        partial void Trim0_Feature_Vcmptrim_ValueProvider(ulong a);

        partial void Trim0_Feature_Write(uint a, uint b);
        partial void Trim0_Feature_Read(uint a, uint b);
        
        // Trim1_Feature - Offset : 0x54
        protected IValueRegisterField trim1_feature_rcotrim_field;
        partial void Trim1_Feature_Rcotrim_Write(ulong a, ulong b);
        partial void Trim1_Feature_Rcotrim_Read(ulong a, ulong b);
        partial void Trim1_Feature_Rcotrim_ValueProvider(ulong a);

        partial void Trim1_Feature_Write(uint a, uint b);
        partial void Trim1_Feature_Read(uint a, uint b);
        
        // Trim2_Feature - Offset : 0x58
        protected IValueRegisterField trim2_feature_ipktrim_field;
        partial void Trim2_Feature_Ipktrim_Write(ulong a, ulong b);
        partial void Trim2_Feature_Ipktrim_Read(ulong a, ulong b);
        partial void Trim2_Feature_Ipktrim_ValueProvider(ulong a);

        partial void Trim2_Feature_Write(uint a, uint b);
        partial void Trim2_Feature_Read(uint a, uint b);
        
        // Cfg_Feature - Offset : 0x5C
        protected IEnumRegisterField<CFG_FEATURE_EARLYRESETEN> cfg_feature_earlyreseten_bit;
        partial void Cfg_Feature_Earlyreseten_Write(CFG_FEATURE_EARLYRESETEN a, CFG_FEATURE_EARLYRESETEN b);
        partial void Cfg_Feature_Earlyreseten_Read(CFG_FEATURE_EARLYRESETEN a, CFG_FEATURE_EARLYRESETEN b);
        partial void Cfg_Feature_Earlyreseten_ValueProvider(CFG_FEATURE_EARLYRESETEN a);
        protected IEnumRegisterField<CFG_FEATURE_VCMPVPROG> cfg_feature_vcmpvprog_field;
        partial void Cfg_Feature_Vcmpvprog_Write(CFG_FEATURE_VCMPVPROG a, CFG_FEATURE_VCMPVPROG b);
        partial void Cfg_Feature_Vcmpvprog_Read(CFG_FEATURE_VCMPVPROG a, CFG_FEATURE_VCMPVPROG b);
        partial void Cfg_Feature_Vcmpvprog_ValueProvider(CFG_FEATURE_VCMPVPROG a);
        protected IEnumRegisterField<CFG_FEATURE_SWDRVDLY> cfg_feature_swdrvdly_bit;
        partial void Cfg_Feature_Swdrvdly_Write(CFG_FEATURE_SWDRVDLY a, CFG_FEATURE_SWDRVDLY b);
        partial void Cfg_Feature_Swdrvdly_Read(CFG_FEATURE_SWDRVDLY a, CFG_FEATURE_SWDRVDLY b);
        partial void Cfg_Feature_Swdrvdly_ValueProvider(CFG_FEATURE_SWDRVDLY a);
        protected IFlagRegisterField cfg_feature_disnxmtoff_bit;
        partial void Cfg_Feature_Disnxmtoff_Write(bool a, bool b);
        partial void Cfg_Feature_Disnxmtoff_Read(bool a, bool b);
        partial void Cfg_Feature_Disnxmtoff_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_ensccilim_bit;
        partial void Cfg_Feature_Ensccilim_Write(bool a, bool b);
        partial void Cfg_Feature_Ensccilim_Read(bool a, bool b);
        partial void Cfg_Feature_Ensccilim_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_disricompduty_bit;
        partial void Cfg_Feature_Disricompduty_Write(bool a, bool b);
        partial void Cfg_Feature_Disricompduty_Read(bool a, bool b);
        partial void Cfg_Feature_Disricompduty_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_redvswrc_bit;
        partial void Cfg_Feature_Redvswrc_Write(bool a, bool b);
        partial void Cfg_Feature_Redvswrc_Read(bool a, bool b);
        partial void Cfg_Feature_Redvswrc_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_disricomp_bit;
        partial void Cfg_Feature_Disricomp_Write(bool a, bool b);
        partial void Cfg_Feature_Disricomp_Read(bool a, bool b);
        partial void Cfg_Feature_Disricomp_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_disduty_bit;
        partial void Cfg_Feature_Disduty_Write(bool a, bool b);
        partial void Cfg_Feature_Disduty_Read(bool a, bool b);
        partial void Cfg_Feature_Disduty_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_pfmxforceizstall_bit;
        partial void Cfg_Feature_Pfmxforceizstall_Write(bool a, bool b);
        partial void Cfg_Feature_Pfmxforceizstall_Read(bool a, bool b);
        partial void Cfg_Feature_Pfmxforceizstall_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_enmtoffvcmprst_bit;
        partial void Cfg_Feature_Enmtoffvcmprst_Write(bool a, bool b);
        partial void Cfg_Feature_Enmtoffvcmprst_Read(bool a, bool b);
        partial void Cfg_Feature_Enmtoffvcmprst_ValueProvider(bool a);
        protected IFlagRegisterField cfg_feature_ppdlydblren_bit;
        partial void Cfg_Feature_Ppdlydblren_Write(bool a, bool b);
        partial void Cfg_Feature_Ppdlydblren_Read(bool a, bool b);
        partial void Cfg_Feature_Ppdlydblren_ValueProvider(bool a);

        partial void Cfg_Feature_Write(uint a, uint b);
        partial void Cfg_Feature_Read(uint a, uint b);
        
        // Dcdcforce_Test - Offset : 0x70
        protected IFlagRegisterField dcdcforce_test_biasen_bit;
        partial void Dcdcforce_Test_Biasen_Write(bool a, bool b);
        partial void Dcdcforce_Test_Biasen_Read(bool a, bool b);
        partial void Dcdcforce_Test_Biasen_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_dcdcvcmpen_bit;
        partial void Dcdcforce_Test_Dcdcvcmpen_Write(bool a, bool b);
        partial void Dcdcforce_Test_Dcdcvcmpen_Read(bool a, bool b);
        partial void Dcdcforce_Test_Dcdcvcmpen_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_buckmodeen_bit;
        partial void Dcdcforce_Test_Buckmodeen_Write(bool a, bool b);
        partial void Dcdcforce_Test_Buckmodeen_Read(bool a, bool b);
        partial void Dcdcforce_Test_Buckmodeen_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_bypswen_bit;
        partial void Dcdcforce_Test_Bypswen_Write(bool a, bool b);
        partial void Dcdcforce_Test_Bypswen_Read(bool a, bool b);
        partial void Dcdcforce_Test_Bypswen_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_vcmpiboost_bit;
        partial void Dcdcforce_Test_Vcmpiboost_Write(bool a, bool b);
        partial void Dcdcforce_Test_Vcmpiboost_Read(bool a, bool b);
        partial void Dcdcforce_Test_Vcmpiboost_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_pfmxmodeen_bit;
        partial void Dcdcforce_Test_Pfmxmodeen_Write(bool a, bool b);
        partial void Dcdcforce_Test_Pfmxmodeen_Read(bool a, bool b);
        partial void Dcdcforce_Test_Pfmxmodeen_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_ppmodeen_bit;
        partial void Dcdcforce_Test_Ppmodeen_Write(bool a, bool b);
        partial void Dcdcforce_Test_Ppmodeen_Read(bool a, bool b);
        partial void Dcdcforce_Test_Ppmodeen_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_ipkdacem2_bit;
        partial void Dcdcforce_Test_Ipkdacem2_Write(bool a, bool b);
        partial void Dcdcforce_Test_Ipkdacem2_Read(bool a, bool b);
        partial void Dcdcforce_Test_Ipkdacem2_ValueProvider(bool a);
        protected IFlagRegisterField dcdcforce_test_forceen_bit;
        partial void Dcdcforce_Test_Forceen_Write(bool a, bool b);
        partial void Dcdcforce_Test_Forceen_Read(bool a, bool b);
        partial void Dcdcforce_Test_Forceen_ValueProvider(bool a);

        partial void Dcdcforce_Test_Write(uint a, uint b);
        partial void Dcdcforce_Test_Read(uint a, uint b);
        
        // Dbustest_Test - Offset : 0x74
        protected IFlagRegisterField dbustest_test_testen_bit;
        partial void Dbustest_Test_Testen_Write(bool a, bool b);
        partial void Dbustest_Test_Testen_Read(bool a, bool b);
        partial void Dbustest_Test_Testen_ValueProvider(bool a);
        protected IEnumRegisterField<DBUSTEST_TEST_TESTSEL> dbustest_test_testsel_field;
        partial void Dbustest_Test_Testsel_Write(DBUSTEST_TEST_TESTSEL a, DBUSTEST_TEST_TESTSEL b);
        partial void Dbustest_Test_Testsel_Read(DBUSTEST_TEST_TESTSEL a, DBUSTEST_TEST_TESTSEL b);
        partial void Dbustest_Test_Testsel_ValueProvider(DBUSTEST_TEST_TESTSEL a);

        partial void Dbustest_Test_Write(uint a, uint b);
        partial void Dbustest_Test_Read(uint a, uint b);
        
        // Dcdcvcmptest_Test - Offset : 0x78
        protected IFlagRegisterField dcdcvcmptest_test_vcmpbufforce_bit;
        partial void Dcdcvcmptest_Test_Vcmpbufforce_Write(bool a, bool b);
        partial void Dcdcvcmptest_Test_Vcmpbufforce_Read(bool a, bool b);
        partial void Dcdcvcmptest_Test_Vcmpbufforce_ValueProvider(bool a);
        protected IEnumRegisterField<DCDCVCMPTEST_TEST_VCMPCMPFORCE> dcdcvcmptest_test_vcmpcmpforce_field;
        partial void Dcdcvcmptest_Test_Vcmpcmpforce_Write(DCDCVCMPTEST_TEST_VCMPCMPFORCE a, DCDCVCMPTEST_TEST_VCMPCMPFORCE b);
        partial void Dcdcvcmptest_Test_Vcmpcmpforce_Read(DCDCVCMPTEST_TEST_VCMPCMPFORCE a, DCDCVCMPTEST_TEST_VCMPCMPFORCE b);
        partial void Dcdcvcmptest_Test_Vcmpcmpforce_ValueProvider(DCDCVCMPTEST_TEST_VCMPCMPFORCE a);
        protected IFlagRegisterField dcdcvcmptest_test_vcmpcalen_bit;
        partial void Dcdcvcmptest_Test_Vcmpcalen_Write(bool a, bool b);
        partial void Dcdcvcmptest_Test_Vcmpcalen_Read(bool a, bool b);
        partial void Dcdcvcmptest_Test_Vcmpcalen_ValueProvider(bool a);

        partial void Dcdcvcmptest_Test_Write(uint a, uint b);
        partial void Dcdcvcmptest_Test_Read(uint a, uint b);
        
        // Izctest_Test - Offset : 0x7C
        protected IEnumRegisterField<IZCTEST_TEST_IZCOUNTERIN> izctest_test_izcounterin_bit;
        partial void Izctest_Test_Izcounterin_Write(IZCTEST_TEST_IZCOUNTERIN a, IZCTEST_TEST_IZCOUNTERIN b);
        partial void Izctest_Test_Izcounterin_Read(IZCTEST_TEST_IZCOUNTERIN a, IZCTEST_TEST_IZCOUNTERIN b);
        partial void Izctest_Test_Izcounterin_ValueProvider(IZCTEST_TEST_IZCOUNTERIN a);
        protected IEnumRegisterField<IZCTEST_TEST_IZNFETREPPD> izctest_test_iznfetreppd_bit;
        partial void Izctest_Test_Iznfetreppd_Write(IZCTEST_TEST_IZNFETREPPD a, IZCTEST_TEST_IZNFETREPPD b);
        partial void Izctest_Test_Iznfetreppd_Read(IZCTEST_TEST_IZNFETREPPD a, IZCTEST_TEST_IZNFETREPPD b);
        partial void Izctest_Test_Iznfetreppd_ValueProvider(IZCTEST_TEST_IZNFETREPPD a);

        partial void Izctest_Test_Write(uint a, uint b);
        partial void Izctest_Test_Read(uint a, uint b);
        
        // Dcdctest_Test - Offset : 0x80
        protected IFlagRegisterField dcdctest_test_swnfeton_bit;
        partial void Dcdctest_Test_Swnfeton_Write(bool a, bool b);
        partial void Dcdctest_Test_Swnfeton_Read(bool a, bool b);
        partial void Dcdctest_Test_Swnfeton_ValueProvider(bool a);
        protected IFlagRegisterField dcdctest_test_conten_bit;
        partial void Dcdctest_Test_Conten_Write(bool a, bool b);
        partial void Dcdctest_Test_Conten_Read(bool a, bool b);
        partial void Dcdctest_Test_Conten_ValueProvider(bool a);
        protected IFlagRegisterField dcdctest_test_tocmode_bit;
        partial void Dcdctest_Test_Tocmode_Write(bool a, bool b);
        partial void Dcdctest_Test_Tocmode_Read(bool a, bool b);
        partial void Dcdctest_Test_Tocmode_ValueProvider(bool a);
        protected IFlagRegisterField dcdctest_test_toctrig_bit;
        partial void Dcdctest_Test_Toctrig_Write(bool a, bool b);
        partial void Dcdctest_Test_Toctrig_Read(bool a, bool b);
        partial void Dcdctest_Test_Toctrig_ValueProvider(bool a);
        protected IFlagRegisterField dcdctest_test_tocfreerun_bit;
        partial void Dcdctest_Test_Tocfreerun_Write(bool a, bool b);
        partial void Dcdctest_Test_Tocfreerun_Read(bool a, bool b);
        partial void Dcdctest_Test_Tocfreerun_ValueProvider(bool a);

        partial void Dcdctest_Test_Write(uint a, uint b);
        partial void Dcdctest_Test_Read(uint a, uint b);
        
        // Teststatus0_Test - Offset : 0x84
        protected IFlagRegisterField teststatus0_test_biasen_bit;
        partial void Teststatus0_Test_Biasen_Read(bool a, bool b);
        partial void Teststatus0_Test_Biasen_ValueProvider(bool a);
        protected IFlagRegisterField teststatus0_test_dcdcvcmpen_bit;
        partial void Teststatus0_Test_Dcdcvcmpen_Read(bool a, bool b);
        partial void Teststatus0_Test_Dcdcvcmpen_ValueProvider(bool a);
        protected IFlagRegisterField teststatus0_test_buckmodeen_bit;
        partial void Teststatus0_Test_Buckmodeen_Read(bool a, bool b);
        partial void Teststatus0_Test_Buckmodeen_ValueProvider(bool a);
        protected IEnumRegisterField<TESTSTATUS0_TEST_UPDIPKDAC> teststatus0_test_updipkdac_bit;
        partial void Teststatus0_Test_Updipkdac_Read(TESTSTATUS0_TEST_UPDIPKDAC a, TESTSTATUS0_TEST_UPDIPKDAC b);
        partial void Teststatus0_Test_Updipkdac_ValueProvider(TESTSTATUS0_TEST_UPDIPKDAC a);

        partial void Teststatus0_Test_Write(uint a, uint b);
        partial void Teststatus0_Test_Read(uint a, uint b);
        
        // Teststatus1_Test - Offset : 0x88
        protected IValueRegisterField teststatus1_test_izvalread_field;
        partial void Teststatus1_Test_Izvalread_Read(ulong a, ulong b);
        partial void Teststatus1_Test_Izvalread_ValueProvider(ulong a);

        partial void Teststatus1_Test_Write(uint a, uint b);
        partial void Teststatus1_Test_Read(uint a, uint b);
        
        // Teststatus2_Test - Offset : 0x8C
        protected IValueRegisterField teststatus2_test_ipkdac_field;
        partial void Teststatus2_Test_Ipkdac_Read(ulong a, ulong b);
        partial void Teststatus2_Test_Ipkdac_ValueProvider(ulong a);

        partial void Teststatus2_Test_Write(uint a, uint b);
        partial void Teststatus2_Test_Read(uint a, uint b);
        
        // Rpuratd0_Drpu - Offset : 0x90
        protected IFlagRegisterField rpuratd0_drpu_ratdctrl_bit;
        partial void Rpuratd0_Drpu_Ratdctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdem01ctrl0_bit;
        partial void Rpuratd0_Drpu_Ratdem01ctrl0_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem01ctrl0_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem01ctrl0_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdizctrl_bit;
        partial void Rpuratd0_Drpu_Ratdizctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdizctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdizctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdem23ctrl0_bit;
        partial void Rpuratd0_Drpu_Ratdem23ctrl0_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem23ctrl0_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem23ctrl0_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdem01ctrl1_bit;
        partial void Rpuratd0_Drpu_Ratdem01ctrl1_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem01ctrl1_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem01ctrl1_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdem23ctrl1_bit;
        partial void Rpuratd0_Drpu_Ratdem23ctrl1_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem23ctrl1_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem23ctrl1_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdppcfg_bit;
        partial void Rpuratd0_Drpu_Ratdppcfg_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdppcfg_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdppcfg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdpfmxctrl_bit;
        partial void Rpuratd0_Drpu_Ratdpfmxctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpfmxctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpfmxctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtranscfg_bit;
        partial void Rpuratd0_Drpu_Ratdtranscfg_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtranscfg_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtranscfg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdif_bit;
        partial void Rpuratd0_Drpu_Ratdif_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdif_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdien_bit;
        partial void Rpuratd0_Drpu_Ratdien_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdien_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdlock_bit;
        partial void Rpuratd0_Drpu_Ratdlock_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdlock_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdlock_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtrim0_bit;
        partial void Rpuratd0_Drpu_Ratdtrim0_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtrim0_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtrim0_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtrim1_bit;
        partial void Rpuratd0_Drpu_Ratdtrim1_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtrim1_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtrim1_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtrim2_bit;
        partial void Rpuratd0_Drpu_Ratdtrim2_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtrim2_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtrim2_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdcfg_bit;
        partial void Rpuratd0_Drpu_Ratdcfg_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcfg_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcfg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratddcdcforce_bit;
        partial void Rpuratd0_Drpu_Ratddcdcforce_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddcdcforce_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddcdcforce_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratddbustest_bit;
        partial void Rpuratd0_Drpu_Ratddbustest_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddbustest_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddbustest_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratddcdcvcmptest_bit;
        partial void Rpuratd0_Drpu_Ratddcdcvcmptest_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddcdcvcmptest_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddcdcvcmptest_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdizctest_bit;
        partial void Rpuratd0_Drpu_Ratdizctest_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdizctest_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdizctest_ValueProvider(bool a);

        partial void Rpuratd0_Drpu_Write(uint a, uint b);
        partial void Rpuratd0_Drpu_Read(uint a, uint b);
        
        // Rpuratd1_Drpu - Offset : 0x94
        protected IFlagRegisterField rpuratd1_drpu_ratddcdctest_bit;
        partial void Rpuratd1_Drpu_Ratddcdctest_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddcdctest_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddcdctest_ValueProvider(bool a);

        partial void Rpuratd1_Drpu_Write(uint a, uint b);
        partial void Rpuratd1_Drpu_Read(uint a, uint b);
        
        partial void DCDC_Reset();

        partial void EFR32xG2_DCDC_2_Constructor();

        public bool Enabled
        {
            get 
            {
                // Your boolean which you have to define in your partial class file
                return isEnabled;
            }
        }

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
            Ipversion = 0x0,
            Ctrl = 0x4,
            Em01ctrl0 = 0x8,
            Izctrl = 0xC,
            Em23ctrl0 = 0x10,
            Em01ctrl1 = 0x14,
            Em23ctrl1 = 0x18,
            Ppcfg = 0x1C,
            Pfmxctrl = 0x20,
            Transcfg = 0x24,
            If = 0x28,
            Ien = 0x2C,
            Status = 0x30,
            Syncbusy = 0x34,
            Lock_Lock = 0x40,
            Lockstatus_Lock = 0x44,
            Trim0_Feature = 0x50,
            Trim1_Feature = 0x54,
            Trim2_Feature = 0x58,
            Cfg_Feature = 0x5C,
            Dcdcforce_Test = 0x70,
            Dbustest_Test = 0x74,
            Dcdcvcmptest_Test = 0x78,
            Izctest_Test = 0x7C,
            Dcdctest_Test = 0x80,
            Teststatus0_Test = 0x84,
            Teststatus1_Test = 0x88,
            Teststatus2_Test = 0x8C,
            Rpuratd0_Drpu = 0x90,
            Rpuratd1_Drpu = 0x94,
            
            Ipversion_SET = 0x1000,
            Ctrl_SET = 0x1004,
            Em01ctrl0_SET = 0x1008,
            Izctrl_SET = 0x100C,
            Em23ctrl0_SET = 0x1010,
            Em01ctrl1_SET = 0x1014,
            Em23ctrl1_SET = 0x1018,
            Ppcfg_SET = 0x101C,
            Pfmxctrl_SET = 0x1020,
            Transcfg_SET = 0x1024,
            If_SET = 0x1028,
            Ien_SET = 0x102C,
            Status_SET = 0x1030,
            Syncbusy_SET = 0x1034,
            Lock_Lock_SET = 0x1040,
            Lockstatus_Lock_SET = 0x1044,
            Trim0_Feature_SET = 0x1050,
            Trim1_Feature_SET = 0x1054,
            Trim2_Feature_SET = 0x1058,
            Cfg_Feature_SET = 0x105C,
            Dcdcforce_Test_SET = 0x1070,
            Dbustest_Test_SET = 0x1074,
            Dcdcvcmptest_Test_SET = 0x1078,
            Izctest_Test_SET = 0x107C,
            Dcdctest_Test_SET = 0x1080,
            Teststatus0_Test_SET = 0x1084,
            Teststatus1_Test_SET = 0x1088,
            Teststatus2_Test_SET = 0x108C,
            Rpuratd0_Drpu_SET = 0x1090,
            Rpuratd1_Drpu_SET = 0x1094,
            
            Ipversion_CLR = 0x2000,
            Ctrl_CLR = 0x2004,
            Em01ctrl0_CLR = 0x2008,
            Izctrl_CLR = 0x200C,
            Em23ctrl0_CLR = 0x2010,
            Em01ctrl1_CLR = 0x2014,
            Em23ctrl1_CLR = 0x2018,
            Ppcfg_CLR = 0x201C,
            Pfmxctrl_CLR = 0x2020,
            Transcfg_CLR = 0x2024,
            If_CLR = 0x2028,
            Ien_CLR = 0x202C,
            Status_CLR = 0x2030,
            Syncbusy_CLR = 0x2034,
            Lock_Lock_CLR = 0x2040,
            Lockstatus_Lock_CLR = 0x2044,
            Trim0_Feature_CLR = 0x2050,
            Trim1_Feature_CLR = 0x2054,
            Trim2_Feature_CLR = 0x2058,
            Cfg_Feature_CLR = 0x205C,
            Dcdcforce_Test_CLR = 0x2070,
            Dbustest_Test_CLR = 0x2074,
            Dcdcvcmptest_Test_CLR = 0x2078,
            Izctest_Test_CLR = 0x207C,
            Dcdctest_Test_CLR = 0x2080,
            Teststatus0_Test_CLR = 0x2084,
            Teststatus1_Test_CLR = 0x2088,
            Teststatus2_Test_CLR = 0x208C,
            Rpuratd0_Drpu_CLR = 0x2090,
            Rpuratd1_Drpu_CLR = 0x2094,
            
            Ipversion_TGL = 0x3000,
            Ctrl_TGL = 0x3004,
            Em01ctrl0_TGL = 0x3008,
            Izctrl_TGL = 0x300C,
            Em23ctrl0_TGL = 0x3010,
            Em01ctrl1_TGL = 0x3014,
            Em23ctrl1_TGL = 0x3018,
            Ppcfg_TGL = 0x301C,
            Pfmxctrl_TGL = 0x3020,
            Transcfg_TGL = 0x3024,
            If_TGL = 0x3028,
            Ien_TGL = 0x302C,
            Status_TGL = 0x3030,
            Syncbusy_TGL = 0x3034,
            Lock_Lock_TGL = 0x3040,
            Lockstatus_Lock_TGL = 0x3044,
            Trim0_Feature_TGL = 0x3050,
            Trim1_Feature_TGL = 0x3054,
            Trim2_Feature_TGL = 0x3058,
            Cfg_Feature_TGL = 0x305C,
            Dcdcforce_Test_TGL = 0x3070,
            Dbustest_Test_TGL = 0x3074,
            Dcdcvcmptest_Test_TGL = 0x3078,
            Izctest_Test_TGL = 0x307C,
            Dcdctest_Test_TGL = 0x3080,
            Teststatus0_Test_TGL = 0x3084,
            Teststatus1_Test_TGL = 0x3088,
            Teststatus2_Test_TGL = 0x308C,
            Rpuratd0_Drpu_TGL = 0x3090,
            Rpuratd1_Drpu_TGL = 0x3094,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}