//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    LFXO, Generated on : 2025-05-08 15:29:38.524482
    LFXO, ID Version : b9968dcd7222434089d09cfd6373a3ec.2 */

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
    public partial class SiLabs_LFXO_2 : BasicDoubleWordPeripheral, IKnownSize
    {

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Cfg1, GenerateCfg1Register()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Cal, GenerateCalRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Syncbusy, GenerateSyncbusyRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Delaycal, GenerateDelaycalRegister()},
                {(long)Registers.Agcctrl, GenerateAgcctrlRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            LFXO_Reset();
        }
        
        protected enum CFG_AGCRATE
        {
            SLOW = 0, // 128k cycles
            MEDIUM = 1, // 32k cycles
            FAST = 2, // 8k cycles
            TEST = 3, // RESERVED
        }
        
        protected enum CFG_MODE
        {
            XTAL = 0, // LFXO in Low Power mode. A 32768Hz crystal should be connected to the LF crystal pads. Voltage must not exceed VDDIO. 
            XTAL_HP = 1, // An external sine source with minimum amplitude 100mv (zero-to-peak) and maximum amplitude 500mV (zero-to-peak) should be connected in series with LFXTAL_I pin. Minimum voltage should be larger than ground and maximum voltage smaller than VDDIO. The sine source does not need to be ac coupled externally as it is ac couples inside LFXO. LFXTAL_O is free to be used as a general purpose GPIO. 
            BUFEXTCLK = 2, // An external 32KHz CMOS clock should be provided on LFXTAL_I. LFXTAL_O is free to be used as a general purpose GPIO.
            DIGEXTCLK = 3, // An external 32KHz CMOS clock should be provided on LFXTAL_I. LFXTAL_O is free to be used as a general purpose GPIO.
        }
        
        protected enum CFG_TIMEOUT
        {
            CYCLES2 = 0, // Timeout period of 2 cycles
            CYCLES256 = 1, // Timeout period of 256 cycles
            CYCLES1K = 2, // Timeout period of 1024 cycles
            CYCLES2K = 3, // Timeout period of 2048 cycles
            CYCLES4K = 4, // Timeout period of 4096 cycles
            CYCLES8K = 5, // Timeout period of 8192 cycles
            CYCLES16K = 6, // Timeout period of 16384 cycles
            CYCLES32K = 7, // Timeout period of 32768 cycles
        }
        
        protected enum CFG_DRVCURR
        {
            AGC = 0, // 
            FORCE_1 = 1, // 
            FORCE_2 = 2, // 
            FORCE_3 = 3, // 
            FORCE_4 = 4, // 
            FORCE_5 = 5, // 
            FORCE_6 = 6, // 
            FORCE_7 = 7, // 
            FORCE_8 = 8, // 
            FORCE_9 = 9, // 
            FORCE_10 = 10, // 
            FORCE_11 = 11, // 
            FORCE_12 = 12, // 
            FORCE_13 = 13, // 
            FORCE_14 = 14, // 
            FORCE_15 = 15, // 
        }
        
        protected enum CFG_REFLEVEL
        {
            MIN = 0, // low agc amplitude
            RESET = 1, // default
            MAX = 3, // high agc amplitude
        }
        
        protected enum CFG1_BIASTRIM
        {
            MIN = 0, // 6.9 MOhms, 7.8 nA
            RESET = 1, // 7.7 MOhms, 6.9 nA
            MAX = 2, // 8.6 MOhms, 6.1 nA
            bias3 = 3, // 9.5 MOhms, 5.5 nA
            bias4 = 4, // 10.3 MOhms, 5.0 nA
            bias5 = 5, // 11.2 MOhms, 4.6 nA
            bias6 = 6, // 12.0 MOhms, 4.3 nA
            bias7 = 7, // 12.9 MOhms, 3.9 nA
        }
        
        protected enum CFG1_HPCURRTRIM
        {
            MIN = 0, // 1.2 uA
            MAX = 1, // 1.3 uA
            trim2 = 2, // 1.5 uA
            trim3 = 3, // 1.8 uA
        }
        
        protected enum STATUS_LOCK
        {
            UNLOCKED = 0, // LFXO lockable registers are not locked
            LOCKED = 1, // LFXO lockable registers are locked
        }
        
        protected enum DELAYCAL_DELAYTRIM
        {
            MIN = 0, // -6 us
            RESET = 1, // -4 us
            MAX = 2, // -2 us
            delaytrim3 = 3, // +0 us
            delaytrim4 = 4, // +2 us
            delaytrim5 = 5, // +4 us
            delaytrim6 = 6, // +6 us
            delaytrim7 = 7, // +8 us
        }
        
        protected enum AGCCTRL_DISAGCCLK
        {
            ENABLED = 0, // ENABLE AGC CLK
            DISABLED = 1, // DISABLE AGC CLK
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister GenerateIpversionRegister() => new DoubleWordRegister(this, 0x2)
            
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
        protected DoubleWordRegister GenerateCtrlRegister() => new DoubleWordRegister(this, 0x2)
            .WithFlag(0, out ctrl_forceen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceen_ValueProvider(_);
                        return ctrl_forceen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Forceen_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Forceen_Read(_, __),
                    name: "Forceen")
            .WithFlag(1, out ctrl_disondemand_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Disondemand_ValueProvider(_);
                        return ctrl_disondemand_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Disondemand_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Disondemand_Read(_, __),
                    name: "Disondemand")
            .WithReservedBits(2, 2)
            .WithFlag(4, out ctrl_faildeten_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Faildeten_ValueProvider(_);
                        return ctrl_faildeten_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Faildeten_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Faildeten_Read(_, __),
                    name: "Faildeten")
            .WithFlag(5, out ctrl_faildetem4wuen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Faildetem4wuen_ValueProvider(_);
                        return ctrl_faildetem4wuen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Faildetem4wuen_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Faildetem4wuen_Read(_, __),
                    name: "Faildetem4wuen")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write_WithHook(_, __));
        
        // Cfg - Offset : 0x8
        protected DoubleWordRegister GenerateCfgRegister() => new DoubleWordRegister(this, 0x10700)
            .WithEnumField<DoubleWordRegister, CFG_AGCRATE>(0, 2, out cfg_agcrate_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Agcrate_ValueProvider(_);
                        return cfg_agcrate_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg_Agcrate_Write(_, __),
                    
                    readCallback: (_, __) => Cfg_Agcrate_Read(_, __),
                    name: "Agcrate")
            .WithReservedBits(2, 2)
            .WithEnumField<DoubleWordRegister, CFG_MODE>(4, 2, out cfg_mode_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Mode_ValueProvider(_);
                        return cfg_mode_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg_Mode_Write(_, __),
                    
                    readCallback: (_, __) => Cfg_Mode_Read(_, __),
                    name: "Mode")
            .WithReservedBits(6, 2)
            .WithEnumField<DoubleWordRegister, CFG_TIMEOUT>(8, 3, out cfg_timeout_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Timeout_ValueProvider(_);
                        return cfg_timeout_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg_Timeout_Write(_, __),
                    
                    readCallback: (_, __) => Cfg_Timeout_Read(_, __),
                    name: "Timeout")
            .WithReservedBits(11, 1)
            .WithEnumField<DoubleWordRegister, CFG_DRVCURR>(12, 4, out cfg_drvcurr_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Drvcurr_ValueProvider(_);
                        return cfg_drvcurr_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg_Drvcurr_Write(_, __),
                    
                    readCallback: (_, __) => Cfg_Drvcurr_Read(_, __),
                    name: "Drvcurr")
            .WithEnumField<DoubleWordRegister, CFG_REFLEVEL>(16, 2, out cfg_reflevel_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Reflevel_ValueProvider(_);
                        return cfg_reflevel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg_Reflevel_Write(_, __),
                    
                    readCallback: (_, __) => Cfg_Reflevel_Read(_, __),
                    name: "Reflevel")
            .WithReservedBits(18, 14)
            .WithReadCallback((_, __) => Cfg_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Write_WithHook(_, __));
        
        // Cfg1 - Offset : 0xC
        protected DoubleWordRegister GenerateCfg1Register() => new DoubleWordRegister(this, 0x305)
            .WithEnumField<DoubleWordRegister, CFG1_BIASTRIM>(0, 3, out cfg1_biastrim_field, 
                    valueProviderCallback: (_) => {
                        Cfg1_Biastrim_ValueProvider(_);
                        return cfg1_biastrim_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg1_Biastrim_Write(_, __),
                    
                    readCallback: (_, __) => Cfg1_Biastrim_Read(_, __),
                    name: "Biastrim")
            .WithReservedBits(3, 1)
            .WithFlag(4, out cfg1_clampdis_bit, 
                    valueProviderCallback: (_) => {
                        Cfg1_Clampdis_ValueProvider(_);
                        return cfg1_clampdis_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg1_Clampdis_Write(_, __),
                    
                    readCallback: (_, __) => Cfg1_Clampdis_Read(_, __),
                    name: "Clampdis")
            .WithReservedBits(5, 3)
            .WithEnumField<DoubleWordRegister, CFG1_HPCURRTRIM>(8, 2, out cfg1_hpcurrtrim_field, 
                    valueProviderCallback: (_) => {
                        Cfg1_Hpcurrtrim_ValueProvider(_);
                        return cfg1_hpcurrtrim_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cfg1_Hpcurrtrim_Write(_, __),
                    
                    readCallback: (_, __) => Cfg1_Hpcurrtrim_Read(_, __),
                    name: "Hpcurrtrim")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Cfg1_Read(_, __))
            .WithWriteCallback((_, __) => Cfg1_Write(_, __));
        
        // Status - Offset : 0x10
        protected DoubleWordRegister GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_rdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Rdy_ValueProvider(_);
                        return status_rdy_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Rdy_Read(_, __),
                    name: "Rdy")
            .WithReservedBits(1, 15)
            .WithFlag(16, out status_ens_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ens_ValueProvider(_);
                        return status_ens_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ens_Read(_, __),
                    name: "Ens")
            .WithReservedBits(17, 14)
            .WithEnumField<DoubleWordRegister, STATUS_LOCK>(31, 1, out status_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lock_ValueProvider(_);
                        return status_lock_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Lock_Read(_, __),
                    name: "Lock")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Cal - Offset : 0x14
        protected DoubleWordRegister GenerateCalRegister() => new DoubleWordRegister(this, 0x100)
            
            .WithValueField(0, 7, out cal_captune_field, 
                    valueProviderCallback: (_) => {
                        Cal_Captune_ValueProvider(_);
                        return cal_captune_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cal_Captune_Write(_, __),
                    
                    readCallback: (_, __) => Cal_Captune_Read(_, __),
                    name: "Captune")
            .WithReservedBits(7, 1)
            
            .WithValueField(8, 2, out cal_gain_field, 
                    valueProviderCallback: (_) => {
                        Cal_Gain_ValueProvider(_);
                        return cal_gain_field.Value;
                    },
                    
                    writeCallback: (_, __) => Cal_Gain_Write(_, __),
                    
                    readCallback: (_, __) => Cal_Gain_Read(_, __),
                    name: "Gain")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Cal_Read(_, __))
            .WithWriteCallback((_, __) => Cal_Write(_, __));
        
        // If - Offset : 0x18
        protected DoubleWordRegister GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_rdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Rdy_ValueProvider(_);
                        return if_rdy_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Rdy_Write(_, __),
                    
                    readCallback: (_, __) => If_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out if_posedge_bit, 
                    valueProviderCallback: (_) => {
                        If_Posedge_ValueProvider(_);
                        return if_posedge_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Posedge_Write(_, __),
                    
                    readCallback: (_, __) => If_Posedge_Read(_, __),
                    name: "Posedge")
            .WithFlag(2, out if_negedge_bit, 
                    valueProviderCallback: (_) => {
                        If_Negedge_ValueProvider(_);
                        return if_negedge_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Negedge_Write(_, __),
                    
                    readCallback: (_, __) => If_Negedge_Read(_, __),
                    name: "Negedge")
            .WithFlag(3, out if_fail_bit, 
                    valueProviderCallback: (_) => {
                        If_Fail_ValueProvider(_);
                        return if_fail_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Fail_Write(_, __),
                    
                    readCallback: (_, __) => If_Fail_Read(_, __),
                    name: "Fail")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x1C
        protected DoubleWordRegister GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_rdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Rdy_ValueProvider(_);
                        return ien_rdy_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Rdy_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out ien_posedge_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Posedge_ValueProvider(_);
                        return ien_posedge_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Posedge_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Posedge_Read(_, __),
                    name: "Posedge")
            .WithFlag(2, out ien_negedge_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Negedge_ValueProvider(_);
                        return ien_negedge_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Negedge_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Negedge_Read(_, __),
                    name: "Negedge")
            .WithFlag(3, out ien_fail_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Fail_ValueProvider(_);
                        return ien_fail_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Fail_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Fail_Read(_, __),
                    name: "Fail")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Syncbusy - Offset : 0x20
        protected DoubleWordRegister GenerateSyncbusyRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_cal_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Cal_ValueProvider(_);
                        return syncbusy_cal_bit.Value;
                    },
                    
                    readCallback: (_, __) => Syncbusy_Cal_Read(_, __),
                    name: "Cal")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Syncbusy_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Write(_, __));
        
        // Lock - Offset : 0x24
        protected DoubleWordRegister GenerateLockRegister() => new DoubleWordRegister(this, 0x1A20)
            
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Delaycal - Offset : 0x28
        protected DoubleWordRegister GenerateDelaycalRegister() => new DoubleWordRegister(this, 0x30)
            .WithFlag(0, out delaycal_encal_bit, 
                    valueProviderCallback: (_) => {
                        Delaycal_Encal_ValueProvider(_);
                        return delaycal_encal_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Delaycal_Encal_Write(_, __),
                    
                    readCallback: (_, __) => Delaycal_Encal_Read(_, __),
                    name: "Encal")
            .WithReservedBits(1, 3)
            .WithEnumField<DoubleWordRegister, DELAYCAL_DELAYTRIM>(4, 3, out delaycal_delaytrim_field, 
                    valueProviderCallback: (_) => {
                        Delaycal_Delaytrim_ValueProvider(_);
                        return delaycal_delaytrim_field.Value;
                    },
                    
                    writeCallback: (_, __) => Delaycal_Delaytrim_Write(_, __),
                    
                    readCallback: (_, __) => Delaycal_Delaytrim_Read(_, __),
                    name: "Delaytrim")
            .WithReservedBits(7, 25)
            .WithReadCallback((_, __) => Delaycal_Read(_, __))
            .WithWriteCallback((_, __) => Delaycal_Write(_, __));
        
        // Agcctrl - Offset : 0x2C
        protected DoubleWordRegister GenerateAgcctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, AGCCTRL_DISAGCCLK>(0, 1, out agcctrl_disagcclk_bit, 
                    valueProviderCallback: (_) => {
                        Agcctrl_Disagcclk_ValueProvider(_);
                        return agcctrl_disagcclk_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Agcctrl_Disagcclk_Write(_, __),
                    
                    readCallback: (_, __) => Agcctrl_Disagcclk_Read(_, __),
                    name: "Disagcclk")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Agcctrl_Read(_, __))
            .WithWriteCallback((_, __) => Agcctrl_Write(_, __));
        

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

        



        
        // Ipversion - Offset : 0x0
    
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);
        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        
        // Ctrl - Offset : 0x4
    
        protected IFlagRegisterField ctrl_forceen_bit;
        partial void Ctrl_Forceen_Write(bool a, bool b);
        partial void Ctrl_Forceen_Read(bool a, bool b);
        partial void Ctrl_Forceen_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_disondemand_bit;
        partial void Ctrl_Disondemand_Write(bool a, bool b);
        partial void Ctrl_Disondemand_Read(bool a, bool b);
        partial void Ctrl_Disondemand_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_faildeten_bit;
        partial void Ctrl_Faildeten_Write(bool a, bool b);
        partial void Ctrl_Faildeten_Read(bool a, bool b);
        partial void Ctrl_Faildeten_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_faildetem4wuen_bit;
        partial void Ctrl_Faildetem4wuen_Write(bool a, bool b);
        partial void Ctrl_Faildetem4wuen_Read(bool a, bool b);
        partial void Ctrl_Faildetem4wuen_ValueProvider(bool a);
        protected void Ctrl_Write_WithHook(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                this.Log(LogLevel.Error, "Ctrl: Write access to a locked register");
            }
            Ctrl_Write(a, b);
        }
        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        
        // Cfg - Offset : 0x8
    
        protected IEnumRegisterField<CFG_AGCRATE> cfg_agcrate_field;
        partial void Cfg_Agcrate_Write(CFG_AGCRATE a, CFG_AGCRATE b);
        partial void Cfg_Agcrate_Read(CFG_AGCRATE a, CFG_AGCRATE b);
        partial void Cfg_Agcrate_ValueProvider(CFG_AGCRATE a);
    
        protected IEnumRegisterField<CFG_MODE> cfg_mode_field;
        partial void Cfg_Mode_Write(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_Read(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_ValueProvider(CFG_MODE a);
    
        protected IEnumRegisterField<CFG_TIMEOUT> cfg_timeout_field;
        partial void Cfg_Timeout_Write(CFG_TIMEOUT a, CFG_TIMEOUT b);
        partial void Cfg_Timeout_Read(CFG_TIMEOUT a, CFG_TIMEOUT b);
        partial void Cfg_Timeout_ValueProvider(CFG_TIMEOUT a);
    
        protected IEnumRegisterField<CFG_DRVCURR> cfg_drvcurr_field;
        partial void Cfg_Drvcurr_Write(CFG_DRVCURR a, CFG_DRVCURR b);
        partial void Cfg_Drvcurr_Read(CFG_DRVCURR a, CFG_DRVCURR b);
        partial void Cfg_Drvcurr_ValueProvider(CFG_DRVCURR a);
    
        protected IEnumRegisterField<CFG_REFLEVEL> cfg_reflevel_field;
        partial void Cfg_Reflevel_Write(CFG_REFLEVEL a, CFG_REFLEVEL b);
        partial void Cfg_Reflevel_Read(CFG_REFLEVEL a, CFG_REFLEVEL b);
        partial void Cfg_Reflevel_ValueProvider(CFG_REFLEVEL a);
        protected void Cfg_Write_WithHook(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                this.Log(LogLevel.Error, "Cfg: Write access to a locked register");
            }
            Cfg_Write(a, b);
        }
        partial void Cfg_Write(uint a, uint b);
        partial void Cfg_Read(uint a, uint b);
        
        
        // Cfg1 - Offset : 0xC
    
        protected IEnumRegisterField<CFG1_BIASTRIM> cfg1_biastrim_field;
        partial void Cfg1_Biastrim_Write(CFG1_BIASTRIM a, CFG1_BIASTRIM b);
        partial void Cfg1_Biastrim_Read(CFG1_BIASTRIM a, CFG1_BIASTRIM b);
        partial void Cfg1_Biastrim_ValueProvider(CFG1_BIASTRIM a);
    
        protected IFlagRegisterField cfg1_clampdis_bit;
        partial void Cfg1_Clampdis_Write(bool a, bool b);
        partial void Cfg1_Clampdis_Read(bool a, bool b);
        partial void Cfg1_Clampdis_ValueProvider(bool a);
    
        protected IEnumRegisterField<CFG1_HPCURRTRIM> cfg1_hpcurrtrim_field;
        partial void Cfg1_Hpcurrtrim_Write(CFG1_HPCURRTRIM a, CFG1_HPCURRTRIM b);
        partial void Cfg1_Hpcurrtrim_Read(CFG1_HPCURRTRIM a, CFG1_HPCURRTRIM b);
        partial void Cfg1_Hpcurrtrim_ValueProvider(CFG1_HPCURRTRIM a);
        partial void Cfg1_Write(uint a, uint b);
        partial void Cfg1_Read(uint a, uint b);
        
        
        // Status - Offset : 0x10
    
        protected IFlagRegisterField status_rdy_bit;
        partial void Status_Rdy_Read(bool a, bool b);
        partial void Status_Rdy_ValueProvider(bool a);
    
        protected IFlagRegisterField status_ens_bit;
        partial void Status_Ens_Read(bool a, bool b);
        partial void Status_Ens_ValueProvider(bool a);
    
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);
        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        
        // Cal - Offset : 0x14
    
        protected IValueRegisterField cal_captune_field;
        partial void Cal_Captune_Write(ulong a, ulong b);
        partial void Cal_Captune_Read(ulong a, ulong b);
        partial void Cal_Captune_ValueProvider(ulong a);
    
        protected IValueRegisterField cal_gain_field;
        partial void Cal_Gain_Write(ulong a, ulong b);
        partial void Cal_Gain_Read(ulong a, ulong b);
        partial void Cal_Gain_ValueProvider(ulong a);
        partial void Cal_Write(uint a, uint b);
        partial void Cal_Read(uint a, uint b);
        
        
        // If - Offset : 0x18
    
        protected IFlagRegisterField if_rdy_bit;
        partial void If_Rdy_Write(bool a, bool b);
        partial void If_Rdy_Read(bool a, bool b);
        partial void If_Rdy_ValueProvider(bool a);
    
        protected IFlagRegisterField if_posedge_bit;
        partial void If_Posedge_Write(bool a, bool b);
        partial void If_Posedge_Read(bool a, bool b);
        partial void If_Posedge_ValueProvider(bool a);
    
        protected IFlagRegisterField if_negedge_bit;
        partial void If_Negedge_Write(bool a, bool b);
        partial void If_Negedge_Read(bool a, bool b);
        partial void If_Negedge_ValueProvider(bool a);
    
        protected IFlagRegisterField if_fail_bit;
        partial void If_Fail_Write(bool a, bool b);
        partial void If_Fail_Read(bool a, bool b);
        partial void If_Fail_ValueProvider(bool a);
        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        
        // Ien - Offset : 0x1C
    
        protected IFlagRegisterField ien_rdy_bit;
        partial void Ien_Rdy_Write(bool a, bool b);
        partial void Ien_Rdy_Read(bool a, bool b);
        partial void Ien_Rdy_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_posedge_bit;
        partial void Ien_Posedge_Write(bool a, bool b);
        partial void Ien_Posedge_Read(bool a, bool b);
        partial void Ien_Posedge_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_negedge_bit;
        partial void Ien_Negedge_Write(bool a, bool b);
        partial void Ien_Negedge_Read(bool a, bool b);
        partial void Ien_Negedge_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_fail_bit;
        partial void Ien_Fail_Write(bool a, bool b);
        partial void Ien_Fail_Read(bool a, bool b);
        partial void Ien_Fail_ValueProvider(bool a);
        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        
        // Syncbusy - Offset : 0x20
    
        protected IFlagRegisterField syncbusy_cal_bit;
        partial void Syncbusy_Cal_Read(bool a, bool b);
        partial void Syncbusy_Cal_ValueProvider(bool a);
        partial void Syncbusy_Write(uint a, uint b);
        partial void Syncbusy_Read(uint a, uint b);
        
        
        // Lock - Offset : 0x24
    
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);
        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        
        // Delaycal - Offset : 0x28
    
        protected IFlagRegisterField delaycal_encal_bit;
        partial void Delaycal_Encal_Write(bool a, bool b);
        partial void Delaycal_Encal_Read(bool a, bool b);
        partial void Delaycal_Encal_ValueProvider(bool a);
    
        protected IEnumRegisterField<DELAYCAL_DELAYTRIM> delaycal_delaytrim_field;
        partial void Delaycal_Delaytrim_Write(DELAYCAL_DELAYTRIM a, DELAYCAL_DELAYTRIM b);
        partial void Delaycal_Delaytrim_Read(DELAYCAL_DELAYTRIM a, DELAYCAL_DELAYTRIM b);
        partial void Delaycal_Delaytrim_ValueProvider(DELAYCAL_DELAYTRIM a);
        partial void Delaycal_Write(uint a, uint b);
        partial void Delaycal_Read(uint a, uint b);
        
        
        // Agcctrl - Offset : 0x2C
    
        protected IEnumRegisterField<AGCCTRL_DISAGCCLK> agcctrl_disagcclk_bit;
        partial void Agcctrl_Disagcclk_Write(AGCCTRL_DISAGCCLK a, AGCCTRL_DISAGCCLK b);
        partial void Agcctrl_Disagcclk_Read(AGCCTRL_DISAGCCLK a, AGCCTRL_DISAGCCLK b);
        partial void Agcctrl_Disagcclk_ValueProvider(AGCCTRL_DISAGCCLK a);
        partial void Agcctrl_Write(uint a, uint b);
        partial void Agcctrl_Read(uint a, uint b);
        
        partial void LFXO_Reset();

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
            Ipversion = 0x0,
            Ctrl = 0x4,
            Cfg = 0x8,
            Cfg1 = 0xC,
            Status = 0x10,
            Cal = 0x14,
            If = 0x18,
            Ien = 0x1C,
            Syncbusy = 0x20,
            Lock = 0x24,
            Delaycal = 0x28,
            Agcctrl = 0x2C,
            
            Ipversion_SET = 0x1000,
            Ctrl_SET = 0x1004,
            Cfg_SET = 0x1008,
            Cfg1_SET = 0x100C,
            Status_SET = 0x1010,
            Cal_SET = 0x1014,
            If_SET = 0x1018,
            Ien_SET = 0x101C,
            Syncbusy_SET = 0x1020,
            Lock_SET = 0x1024,
            Delaycal_SET = 0x1028,
            Agcctrl_SET = 0x102C,
            
            Ipversion_CLR = 0x2000,
            Ctrl_CLR = 0x2004,
            Cfg_CLR = 0x2008,
            Cfg1_CLR = 0x200C,
            Status_CLR = 0x2010,
            Cal_CLR = 0x2014,
            If_CLR = 0x2018,
            Ien_CLR = 0x201C,
            Syncbusy_CLR = 0x2020,
            Lock_CLR = 0x2024,
            Delaycal_CLR = 0x2028,
            Agcctrl_CLR = 0x202C,
            
            Ipversion_TGL = 0x3000,
            Ctrl_TGL = 0x3004,
            Cfg_TGL = 0x3008,
            Cfg1_TGL = 0x300C,
            Status_TGL = 0x3010,
            Cal_TGL = 0x3014,
            If_TGL = 0x3018,
            Ien_TGL = 0x301C,
            Syncbusy_TGL = 0x3020,
            Lock_TGL = 0x3024,
            Delaycal_TGL = 0x3028,
            Agcctrl_TGL = 0x302C,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}