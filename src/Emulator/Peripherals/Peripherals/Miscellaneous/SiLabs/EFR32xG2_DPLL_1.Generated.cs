//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    DPLL, Generated on : 2024-12-17 18:14:44.951341
    DPLL, ID Version : 03b70bbe4726417baff81670ec0ddd1d.1 */

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
    public partial class EFR32xG2_DPLL_1
    {
        public EFR32xG2_DPLL_1(Machine machine) : base(machine)
        {
            EFR32xG2_DPLL_1_constructor();
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
    public partial class EFR32xG2_DPLL_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_DPLL_1(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_DPLL_1_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.En, GenerateEnRegister()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Cfg1, GenerateCfg1Register()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Debugstatus, GenerateDebugstatusRegister()},
                {(long)Registers.Offset, GenerateOffsetRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            DPLL_Reset();
        }
        
        protected enum CFG_MODE
        {
            FLL = 0, // Frequency Lock Mode
            PLL = 1, // Phase Lock Mode
        }
        
        protected enum STATUS_LOCK
        {
            UNLOCKED = 0, // DPLL is unlocked
            LOCKED = 1, // DPLL is locked
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x1)
            
            .WithValueField(0, 32, out ipversion_ipversion_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ipversion_Ipversion_ValueProvider(_);
                        return ipversion_ipversion_field.Value;
                    },
                    
                    readCallback: (_, __) => Ipversion_Ipversion_Read(_, __),
                    name: "Ipversion")
            .WithReadCallback((_, __) => Ipversion_Read(_, __))
            .WithWriteCallback((_, __) => Ipversion_Write(_, __));
        
        // En - Offset : 0x4
        protected DoubleWordRegister  GenerateEnRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out en_en_bit, 
                    valueProviderCallback: (_) => {
                        En_En_ValueProvider(_);
                        return en_en_bit.Value;
                    },
                    
                    writeCallback: (_, __) => En_En_Write(_, __),
                    
                    readCallback: (_, __) => En_En_Read(_, __),
                    name: "En")
            .WithFlag(1, out en_disabling_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        En_Disabling_ValueProvider(_);
                        return en_disabling_bit.Value;
                    },
                    
                    readCallback: (_, __) => En_Disabling_Read(_, __),
                    name: "Disabling")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => En_Read(_, __))
            .WithWriteCallback((_, __) => En_Write(_, __));
        
        // Cfg - Offset : 0x8
        protected DoubleWordRegister  GenerateCfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CFG_MODE>(0, 1, out cfg_mode_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Mode_ValueProvider(_);
                        return cfg_mode_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Mode_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Mode_Read(_, __),
                    name: "Mode")
            .WithFlag(1, out cfg_edgesel_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Edgesel_ValueProvider(_);
                        return cfg_edgesel_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Edgesel_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Edgesel_Read(_, __),
                    name: "Edgesel")
            .WithFlag(2, out cfg_autorecover_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Autorecover_ValueProvider(_);
                        return cfg_autorecover_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Autorecover_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Autorecover_Read(_, __),
                    name: "Autorecover")
            .WithReservedBits(3, 3)
            .WithFlag(6, out cfg_dithen_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Dithen_ValueProvider(_);
                        return cfg_dithen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Dithen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Dithen_Read(_, __),
                    name: "Dithen")
            .WithReservedBits(7, 25)
            .WithReadCallback((_, __) => Cfg_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Write_WithHook(_, __));
        
        // Cfg1 - Offset : 0xC
        protected DoubleWordRegister  GenerateCfg1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 12, out cfg1_m_field, 
                    valueProviderCallback: (_) => {
                        Cfg1_M_ValueProvider(_);
                        return cfg1_m_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg1_M_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg1_M_Read(_, __),
                    name: "M")
            .WithReservedBits(12, 4)
            
            .WithValueField(16, 12, out cfg1_n_field, 
                    valueProviderCallback: (_) => {
                        Cfg1_N_ValueProvider(_);
                        return cfg1_n_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg1_N_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg1_N_Read(_, __),
                    name: "N")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Cfg1_Read(_, __))
            .WithWriteCallback((_, __) => Cfg1_Write(_, __));
        
        // If - Offset : 0x10
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_lock_bit, 
                    valueProviderCallback: (_) => {
                        If_Lock_ValueProvider(_);
                        return if_lock_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Lock_Write(_, __),
                    
                    readCallback: (_, __) => If_Lock_Read(_, __),
                    name: "Lock")
            .WithFlag(1, out if_lockfaillow_bit, 
                    valueProviderCallback: (_) => {
                        If_Lockfaillow_ValueProvider(_);
                        return if_lockfaillow_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Lockfaillow_Write(_, __),
                    
                    readCallback: (_, __) => If_Lockfaillow_Read(_, __),
                    name: "Lockfaillow")
            .WithFlag(2, out if_lockfailhigh_bit, 
                    valueProviderCallback: (_) => {
                        If_Lockfailhigh_ValueProvider(_);
                        return if_lockfailhigh_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Lockfailhigh_Write(_, __),
                    
                    readCallback: (_, __) => If_Lockfailhigh_Read(_, __),
                    name: "Lockfailhigh")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x14
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_lock_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Lock_ValueProvider(_);
                        return ien_lock_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Lock_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Lock_Read(_, __),
                    name: "Lock")
            .WithFlag(1, out ien_lockfaillow_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Lockfaillow_ValueProvider(_);
                        return ien_lockfaillow_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Lockfaillow_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Lockfaillow_Read(_, __),
                    name: "Lockfaillow")
            .WithFlag(2, out ien_lockfailhigh_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Lockfailhigh_ValueProvider(_);
                        return ien_lockfailhigh_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Lockfailhigh_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Lockfailhigh_Read(_, __),
                    name: "Lockfailhigh")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Status - Offset : 0x18
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_rdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Rdy_ValueProvider(_);
                        return status_rdy_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out status_ens_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ens_ValueProvider(_);
                        return status_ens_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ens_Read(_, __),
                    name: "Ens")
            .WithReservedBits(2, 29)
            .WithEnumField<DoubleWordRegister, STATUS_LOCK>(31, 1, out status_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lock_ValueProvider(_);
                        return status_lock_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Lock_Read(_, __),
                    name: "Lock")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Debugstatus - Offset : 0x1C
        protected DoubleWordRegister  GenerateDebugstatusRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out debugstatus_dither_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Debugstatus_Dither_ValueProvider(_);
                        return debugstatus_dither_field.Value;
                    },
                    
                    readCallback: (_, __) => Debugstatus_Dither_Read(_, __),
                    name: "Dither")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Debugstatus_Read(_, __))
            .WithWriteCallback((_, __) => Debugstatus_Write(_, __));
        
        // Offset - Offset : 0x20
        protected DoubleWordRegister  GenerateOffsetRegister() => new DoubleWordRegister(this, 0x5A880)
            .WithReservedBits(0, 4)
            .WithFlag(4, out offset_updateen_bit, 
                    valueProviderCallback: (_) => {
                        Offset_Updateen_ValueProvider(_);
                        return offset_updateen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Offset_Updateen_Write(_, __),
                    
                    readCallback: (_, __) => Offset_Updateen_Read(_, __),
                    name: "Updateen")
            
            .WithValueField(5, 11, out offset_k0_field, 
                    valueProviderCallback: (_) => {
                        Offset_K0_ValueProvider(_);
                        return offset_k0_field.Value;
                    },
                    
                    writeCallback: (_, __) => Offset_K0_Write(_, __),
                    
                    readCallback: (_, __) => Offset_K0_Read(_, __),
                    name: "K0")
            
            .WithValueField(16, 4, out offset_coarsecount_field, 
                    valueProviderCallback: (_) => {
                        Offset_Coarsecount_ValueProvider(_);
                        return offset_coarsecount_field.Value;
                    },
                    
                    writeCallback: (_, __) => Offset_Coarsecount_Write(_, __),
                    
                    readCallback: (_, __) => Offset_Coarsecount_Read(_, __),
                    name: "Coarsecount")
            
            .WithValueField(20, 6, out offset_mincoarse_field, 
                    valueProviderCallback: (_) => {
                        Offset_Mincoarse_ValueProvider(_);
                        return offset_mincoarse_field.Value;
                    },
                    
                    writeCallback: (_, __) => Offset_Mincoarse_Write(_, __),
                    
                    readCallback: (_, __) => Offset_Mincoarse_Read(_, __),
                    name: "Mincoarse")
            .WithReservedBits(26, 6)
            .WithReadCallback((_, __) => Offset_Read(_, __))
            .WithWriteCallback((_, __) => Offset_Write(_, __));
        
        // Lock - Offset : 0x24
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x7102)
            
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        

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

        
        private void WriteWSTATIC()
        {
            if(Enabled)
            {
                this.Log(LogLevel.Error, "Trying to write to a WSTATIC register while peripheral is enabled EN = {0}", Enabled);
            }
        }



        
        // Ipversion - Offset : 0x0
    
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);
        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        // En - Offset : 0x4
    
        protected IFlagRegisterField en_en_bit;
        partial void En_En_Write(bool a, bool b);
        partial void En_En_Read(bool a, bool b);
        partial void En_En_ValueProvider(bool a);
    
        protected IFlagRegisterField en_disabling_bit;
        partial void En_Disabling_Read(bool a, bool b);
        partial void En_Disabling_ValueProvider(bool a);
        partial void En_Write(uint a, uint b);
        partial void En_Read(uint a, uint b);
        
        // Cfg - Offset : 0x8
    
        protected IEnumRegisterField<CFG_MODE> cfg_mode_bit;
        partial void Cfg_Mode_Write(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_Read(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_ValueProvider(CFG_MODE a);
    
        protected IFlagRegisterField cfg_edgesel_bit;
        partial void Cfg_Edgesel_Write(bool a, bool b);
        partial void Cfg_Edgesel_Read(bool a, bool b);
        partial void Cfg_Edgesel_ValueProvider(bool a);
    
        protected IFlagRegisterField cfg_autorecover_bit;
        partial void Cfg_Autorecover_Write(bool a, bool b);
        partial void Cfg_Autorecover_Read(bool a, bool b);
        partial void Cfg_Autorecover_ValueProvider(bool a);
    
        protected IFlagRegisterField cfg_dithen_bit;
        partial void Cfg_Dithen_Write(bool a, bool b);
        partial void Cfg_Dithen_Read(bool a, bool b);
        partial void Cfg_Dithen_ValueProvider(bool a);
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
    
        protected IValueRegisterField cfg1_m_field;
        partial void Cfg1_M_Write(ulong a, ulong b);
        partial void Cfg1_M_Read(ulong a, ulong b);
        partial void Cfg1_M_ValueProvider(ulong a);
    
        protected IValueRegisterField cfg1_n_field;
        partial void Cfg1_N_Write(ulong a, ulong b);
        partial void Cfg1_N_Read(ulong a, ulong b);
        partial void Cfg1_N_ValueProvider(ulong a);
        partial void Cfg1_Write(uint a, uint b);
        partial void Cfg1_Read(uint a, uint b);
        
        // If - Offset : 0x10
    
        protected IFlagRegisterField if_lock_bit;
        partial void If_Lock_Write(bool a, bool b);
        partial void If_Lock_Read(bool a, bool b);
        partial void If_Lock_ValueProvider(bool a);
    
        protected IFlagRegisterField if_lockfaillow_bit;
        partial void If_Lockfaillow_Write(bool a, bool b);
        partial void If_Lockfaillow_Read(bool a, bool b);
        partial void If_Lockfaillow_ValueProvider(bool a);
    
        protected IFlagRegisterField if_lockfailhigh_bit;
        partial void If_Lockfailhigh_Write(bool a, bool b);
        partial void If_Lockfailhigh_Read(bool a, bool b);
        partial void If_Lockfailhigh_ValueProvider(bool a);
        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x14
    
        protected IFlagRegisterField ien_lock_bit;
        partial void Ien_Lock_Write(bool a, bool b);
        partial void Ien_Lock_Read(bool a, bool b);
        partial void Ien_Lock_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_lockfaillow_bit;
        partial void Ien_Lockfaillow_Write(bool a, bool b);
        partial void Ien_Lockfaillow_Read(bool a, bool b);
        partial void Ien_Lockfaillow_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_lockfailhigh_bit;
        partial void Ien_Lockfailhigh_Write(bool a, bool b);
        partial void Ien_Lockfailhigh_Read(bool a, bool b);
        partial void Ien_Lockfailhigh_ValueProvider(bool a);
        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Status - Offset : 0x18
    
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
        
        // Debugstatus - Offset : 0x1C
    
        protected IValueRegisterField debugstatus_dither_field;
        partial void Debugstatus_Dither_Read(ulong a, ulong b);
        partial void Debugstatus_Dither_ValueProvider(ulong a);
        partial void Debugstatus_Write(uint a, uint b);
        partial void Debugstatus_Read(uint a, uint b);
        
        // Offset - Offset : 0x20
    
        protected IFlagRegisterField offset_updateen_bit;
        partial void Offset_Updateen_Write(bool a, bool b);
        partial void Offset_Updateen_Read(bool a, bool b);
        partial void Offset_Updateen_ValueProvider(bool a);
    
        protected IValueRegisterField offset_k0_field;
        partial void Offset_K0_Write(ulong a, ulong b);
        partial void Offset_K0_Read(ulong a, ulong b);
        partial void Offset_K0_ValueProvider(ulong a);
    
        protected IValueRegisterField offset_coarsecount_field;
        partial void Offset_Coarsecount_Write(ulong a, ulong b);
        partial void Offset_Coarsecount_Read(ulong a, ulong b);
        partial void Offset_Coarsecount_ValueProvider(ulong a);
    
        protected IValueRegisterField offset_mincoarse_field;
        partial void Offset_Mincoarse_Write(ulong a, ulong b);
        partial void Offset_Mincoarse_Read(ulong a, ulong b);
        partial void Offset_Mincoarse_ValueProvider(ulong a);
        partial void Offset_Write(uint a, uint b);
        partial void Offset_Read(uint a, uint b);
        
        // Lock - Offset : 0x24
    
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);
        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        partial void DPLL_Reset();

        partial void EFR32xG2_DPLL_1_Constructor();

        public bool Enabled
        {
            get 
            {
                return en_en_bit.Value;
            }
            set 
            {
                en_en_bit.Value = value;
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

        public override uint ReadDoubleWord(long address)
        {
            long temp = address & 0x0FFF;
            switch(address & 0x3000){
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
            switch(address & 0x3000){
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
            En = 0x4,
            Cfg = 0x8,
            Cfg1 = 0xC,
            If = 0x10,
            Ien = 0x14,
            Status = 0x18,
            Debugstatus = 0x1C,
            Offset = 0x20,
            Lock = 0x24,
            
            Ipversion_SET = 0x1000,
            En_SET = 0x1004,
            Cfg_SET = 0x1008,
            Cfg1_SET = 0x100C,
            If_SET = 0x1010,
            Ien_SET = 0x1014,
            Status_SET = 0x1018,
            Debugstatus_SET = 0x101C,
            Offset_SET = 0x1020,
            Lock_SET = 0x1024,
            
            Ipversion_CLR = 0x2000,
            En_CLR = 0x2004,
            Cfg_CLR = 0x2008,
            Cfg1_CLR = 0x200C,
            If_CLR = 0x2010,
            Ien_CLR = 0x2014,
            Status_CLR = 0x2018,
            Debugstatus_CLR = 0x201C,
            Offset_CLR = 0x2020,
            Lock_CLR = 0x2024,
            
            Ipversion_TGL = 0x3000,
            En_TGL = 0x3004,
            Cfg_TGL = 0x3008,
            Cfg1_TGL = 0x300C,
            If_TGL = 0x3010,
            Ien_TGL = 0x3014,
            Status_TGL = 0x3018,
            Debugstatus_TGL = 0x301C,
            Offset_TGL = 0x3020,
            Lock_TGL = 0x3024,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}