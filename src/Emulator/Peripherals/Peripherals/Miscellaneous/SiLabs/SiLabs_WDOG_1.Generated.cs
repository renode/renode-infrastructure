//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2010-2026 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    WDOG, Generated on : 2026-06-10 11:52:04.493727
    WDOG, ID Version : e7dbbe64a00c423492ad4ec2993c323e.1 */

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
    public partial class SiLabs_WDOG_1 : SiLabsPeripheral
    {
        protected override DoubleWordRegisterCollection BuildRegistersCollection()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.En, GenerateEnRegister()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Syncbusy, GenerateSyncbusyRegister()},
            };
            return new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            WDOG_Reset();
        }
        
        protected enum CFG_CLRSRC : ulong
        {
            SW = 0, // A write to the clear bit will clear the WDOG counter
            PRSSRC0 = 1, // A rising edge on the PRS Source 0 will clear the WDOG counter
        }
        
        protected enum CFG_EM1RUN : ulong
        {
            DISABLE = 0, // WDOG timer is frozen in EM1.
            ENABLE = 1, // WDOG timer is running in EM1.
        }
        
        protected enum CFG_EM2RUN : ulong
        {
            DISABLE = 0, // WDOG timer is frozen in EM2.
            ENABLE = 1, // WDOG timer is running in EM2.
        }
        
        protected enum CFG_EM3RUN : ulong
        {
            DISABLE = 0, // WDOG timer is frozen in EM3.
            ENABLE = 1, // WDOG timer is running in EM3.
        }
        
        protected enum CFG_EM4BLOCK : ulong
        {
            DISABLE = 0, // EM4 can be entered by software. See EMU for detailed description.
            ENABLE = 1, // EM4 cannot be entered by software.
        }
        
        protected enum CFG_DEBUGRUN : ulong
        {
            DISABLE = 0, // WDOG timer is frozen in debug mode
            ENABLE = 1, // WDOG timer is running in debug mode
        }
        
        protected enum CFG_WDOGRSTDIS : ulong
        {
            EN = 0, // A timeout will cause a WDOG reset
            DIS = 1, // A timeout will not cause a WDOG reset
        }
        
        protected enum CFG_PERSEL : ulong
        {
            SEL0 = 0, // Timeout period of 9 wdog cycles
            SEL1 = 1, // Timeout period of 17 wdog cycles
            SEL2 = 2, // Timeout period of 33 wdog cycles
            SEL3 = 3, // Timeout period of 65 wdog cycles
            SEL4 = 4, // Timeout period of 129 wdog cycles
            SEL5 = 5, // Timeout period of 257 wdog cycles
            SEL6 = 6, // Timeout period of 513 wdog cycles
            SEL7 = 7, // Timeout period of 1k wdog cycles
            SEL8 = 8, // Timeout period of 2k wdog cycles
            SEL9 = 9, // Timeout period of 4k wdog cycles
            SEL10 = 10, // Timeout period of 8k wdog cycles
            SEL11 = 11, // Timeout period of 16k wdog cycles
            SEL12 = 12, // Timeout period of 32k wdog cycles
            SEL13 = 13, // Timeout period of 64k wdog cycles
            SEL14 = 14, // Timeout period of 128k wdog cycles
            SEL15 = 15, // Timeout period of 256k wdog cycles
        }
        
        protected enum CFG_WARNSEL : ulong
        {
            DIS = 0, // Disable
            SEL1 = 1, // Warning timeout is 25% of the Timeout.
            SEL2 = 2, // Warning timeout is 50% of the Timeout.
            SEL3 = 3, // Warning timeout is 75% of the Timeout.
        }
        
        protected enum CFG_WINSEL : ulong
        {
            DIS = 0, // Disabled.
            SEL1 = 1, // Window timeout is 12.5% of the Timeout.
            SEL2 = 2, // Window timeout is 25% of the Timeout.
            SEL3 = 3, // Window timeout is 37.5% of the Timeout.
            SEL4 = 4, // Window timeout is 50% of the Timeout.
            SEL5 = 5, // Window timeout is 62.5% of the Timeout.
            SEL6 = 6, // Window timeout is 75.5% of the Timeout.
            SEL7 = 7, // Window timeout is 87.5% of the Timeout.
        }
        
        protected enum CMD_CLEAR : ulong
        {
            UNCHANGED = 0, // WDOG timer is unchanged.
            CLEARED = 1, // WDOG timer is cleared to 0.
        }
        
        protected enum STATUS_LOCK : ulong
        {
            UNLOCKED = 0, // All WDOG lockable registers are unlocked.
            LOCKED = 1, // All WDOG lockable registers are locked.
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister GenerateIpversionRegister() => new DoubleWordRegister(this, 0x1)
            
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
        protected DoubleWordRegister GenerateEnRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister GenerateCfgRegister() => new DoubleWordRegister(this, 0xF0000)
            .WithEnumField<DoubleWordRegister, CFG_CLRSRC>(0, 1, out cfg_clrsrc_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Clrsrc_ValueProvider(_);
                        return cfg_clrsrc_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Clrsrc_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Clrsrc_Read(_, __),
                    name: "Clrsrc")
            .WithEnumField<DoubleWordRegister, CFG_EM1RUN>(1, 1, out cfg_em1run_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Em1run_ValueProvider(_);
                        return cfg_em1run_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Em1run_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Em1run_Read(_, __),
                    name: "Em1run")
            .WithEnumField<DoubleWordRegister, CFG_EM2RUN>(2, 1, out cfg_em2run_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Em2run_ValueProvider(_);
                        return cfg_em2run_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Em2run_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Em2run_Read(_, __),
                    name: "Em2run")
            .WithEnumField<DoubleWordRegister, CFG_EM3RUN>(3, 1, out cfg_em3run_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Em3run_ValueProvider(_);
                        return cfg_em3run_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Em3run_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Em3run_Read(_, __),
                    name: "Em3run")
            .WithEnumField<DoubleWordRegister, CFG_EM4BLOCK>(4, 1, out cfg_em4block_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Em4block_ValueProvider(_);
                        return cfg_em4block_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Em4block_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Em4block_Read(_, __),
                    name: "Em4block")
            .WithEnumField<DoubleWordRegister, CFG_DEBUGRUN>(5, 1, out cfg_debugrun_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Debugrun_ValueProvider(_);
                        return cfg_debugrun_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Debugrun_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Debugrun_Read(_, __),
                    name: "Debugrun")
            .WithReservedBits(6, 2)
            .WithEnumField<DoubleWordRegister, CFG_WDOGRSTDIS>(8, 1, out cfg_wdogrstdis_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Wdogrstdis_ValueProvider(_);
                        return cfg_wdogrstdis_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Wdogrstdis_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Wdogrstdis_Read(_, __),
                    name: "Wdogrstdis")
            .WithFlag(9, out cfg_prs0missrsten_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Prs0missrsten_ValueProvider(_);
                        return cfg_prs0missrsten_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Prs0missrsten_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Prs0missrsten_Read(_, __),
                    name: "Prs0missrsten")
            .WithFlag(10, out cfg_prs1missrsten_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Prs1missrsten_ValueProvider(_);
                        return cfg_prs1missrsten_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Prs1missrsten_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Prs1missrsten_Read(_, __),
                    name: "Prs1missrsten")
            .WithReservedBits(11, 5)
            .WithEnumField<DoubleWordRegister, CFG_PERSEL>(16, 4, out cfg_persel_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Persel_ValueProvider(_);
                        return cfg_persel_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Persel_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Persel_Read(_, __),
                    name: "Persel")
            .WithReservedBits(20, 4)
            .WithEnumField<DoubleWordRegister, CFG_WARNSEL>(24, 2, out cfg_warnsel_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Warnsel_ValueProvider(_);
                        return cfg_warnsel_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Warnsel_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Warnsel_Read(_, __),
                    name: "Warnsel")
            .WithReservedBits(26, 2)
            .WithEnumField<DoubleWordRegister, CFG_WINSEL>(28, 3, out cfg_winsel_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Winsel_ValueProvider(_);
                        return cfg_winsel_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Winsel_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Winsel_Read(_, __),
                    name: "Winsel")
            .WithReservedBits(31, 1)
            .WithReadCallback((_, __) => Cfg_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Write_WithHook(_, __));
        
        // Cmd - Offset : 0xC
        protected DoubleWordRegister GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CMD_CLEAR>(0, 1, out cmd_clear_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Cmd_Clear_Write(_, __),
                    name: "Clear")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write_WithHook(_, __));
        
        // Status - Offset : 0x14
        protected DoubleWordRegister GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 31)
            .WithEnumField<DoubleWordRegister, STATUS_LOCK>(31, 1, out status_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lock_ValueProvider(_);
                        return status_lock_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Lock_Read(_, __),
                    name: "Lock")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // If - Offset : 0x18
        protected DoubleWordRegister GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_tout_bit, 
                    valueProviderCallback: (_) => {
                        If_Tout_ValueProvider(_);
                        return if_tout_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Tout_Write(_, __),
                    
                    readCallback: (_, __) => If_Tout_Read(_, __),
                    name: "Tout")
            .WithFlag(1, out if_warn_bit, 
                    valueProviderCallback: (_) => {
                        If_Warn_ValueProvider(_);
                        return if_warn_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Warn_Write(_, __),
                    
                    readCallback: (_, __) => If_Warn_Read(_, __),
                    name: "Warn")
            .WithFlag(2, out if_win_bit, 
                    valueProviderCallback: (_) => {
                        If_Win_ValueProvider(_);
                        return if_win_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Win_Write(_, __),
                    
                    readCallback: (_, __) => If_Win_Read(_, __),
                    name: "Win")
            .WithFlag(3, out if_pem0_bit, 
                    valueProviderCallback: (_) => {
                        If_Pem0_ValueProvider(_);
                        return if_pem0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Pem0_Write(_, __),
                    
                    readCallback: (_, __) => If_Pem0_Read(_, __),
                    name: "Pem0")
            .WithFlag(4, out if_pem1_bit, 
                    valueProviderCallback: (_) => {
                        If_Pem1_ValueProvider(_);
                        return if_pem1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Pem1_Write(_, __),
                    
                    readCallback: (_, __) => If_Pem1_Read(_, __),
                    name: "Pem1")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x1C
        protected DoubleWordRegister GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_tout_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Tout_ValueProvider(_);
                        return ien_tout_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Tout_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Tout_Read(_, __),
                    name: "Tout")
            .WithFlag(1, out ien_warn_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Warn_ValueProvider(_);
                        return ien_warn_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Warn_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Warn_Read(_, __),
                    name: "Warn")
            .WithFlag(2, out ien_win_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Win_ValueProvider(_);
                        return ien_win_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Win_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Win_Read(_, __),
                    name: "Win")
            .WithFlag(3, out ien_pem0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Pem0_ValueProvider(_);
                        return ien_pem0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Pem0_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Pem0_Read(_, __),
                    name: "Pem0")
            .WithFlag(4, out ien_pem1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Pem1_ValueProvider(_);
                        return ien_pem1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Pem1_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Pem1_Read(_, __),
                    name: "Pem1")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Lock - Offset : 0x20
        protected DoubleWordRegister GenerateLockRegister() => new DoubleWordRegister(this, 0xABE8)
            
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Syncbusy - Offset : 0x24
        protected DoubleWordRegister GenerateSyncbusyRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_cmd_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Cmd_ValueProvider(_);
                        return syncbusy_cmd_bit.Value;
                    },
                    
                    readCallback: (_, __) => Syncbusy_Cmd_Read(_, __),
                    name: "Cmd")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Syncbusy_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Write(_, __));
        

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
    
        protected IEnumRegisterField<CFG_CLRSRC> cfg_clrsrc_bit;
        partial void Cfg_Clrsrc_Write(CFG_CLRSRC a, CFG_CLRSRC b);
        partial void Cfg_Clrsrc_Read(CFG_CLRSRC a, CFG_CLRSRC b);
        partial void Cfg_Clrsrc_ValueProvider(CFG_CLRSRC a);
    
        protected IEnumRegisterField<CFG_EM1RUN> cfg_em1run_bit;
        partial void Cfg_Em1run_Write(CFG_EM1RUN a, CFG_EM1RUN b);
        partial void Cfg_Em1run_Read(CFG_EM1RUN a, CFG_EM1RUN b);
        partial void Cfg_Em1run_ValueProvider(CFG_EM1RUN a);
    
        protected IEnumRegisterField<CFG_EM2RUN> cfg_em2run_bit;
        partial void Cfg_Em2run_Write(CFG_EM2RUN a, CFG_EM2RUN b);
        partial void Cfg_Em2run_Read(CFG_EM2RUN a, CFG_EM2RUN b);
        partial void Cfg_Em2run_ValueProvider(CFG_EM2RUN a);
    
        protected IEnumRegisterField<CFG_EM3RUN> cfg_em3run_bit;
        partial void Cfg_Em3run_Write(CFG_EM3RUN a, CFG_EM3RUN b);
        partial void Cfg_Em3run_Read(CFG_EM3RUN a, CFG_EM3RUN b);
        partial void Cfg_Em3run_ValueProvider(CFG_EM3RUN a);
    
        protected IEnumRegisterField<CFG_EM4BLOCK> cfg_em4block_bit;
        partial void Cfg_Em4block_Write(CFG_EM4BLOCK a, CFG_EM4BLOCK b);
        partial void Cfg_Em4block_Read(CFG_EM4BLOCK a, CFG_EM4BLOCK b);
        partial void Cfg_Em4block_ValueProvider(CFG_EM4BLOCK a);
    
        protected IEnumRegisterField<CFG_DEBUGRUN> cfg_debugrun_bit;
        partial void Cfg_Debugrun_Write(CFG_DEBUGRUN a, CFG_DEBUGRUN b);
        partial void Cfg_Debugrun_Read(CFG_DEBUGRUN a, CFG_DEBUGRUN b);
        partial void Cfg_Debugrun_ValueProvider(CFG_DEBUGRUN a);
    
        protected IEnumRegisterField<CFG_WDOGRSTDIS> cfg_wdogrstdis_bit;
        partial void Cfg_Wdogrstdis_Write(CFG_WDOGRSTDIS a, CFG_WDOGRSTDIS b);
        partial void Cfg_Wdogrstdis_Read(CFG_WDOGRSTDIS a, CFG_WDOGRSTDIS b);
        partial void Cfg_Wdogrstdis_ValueProvider(CFG_WDOGRSTDIS a);
    
        protected IFlagRegisterField cfg_prs0missrsten_bit;
        partial void Cfg_Prs0missrsten_Write(bool a, bool b);
        partial void Cfg_Prs0missrsten_Read(bool a, bool b);
        partial void Cfg_Prs0missrsten_ValueProvider(bool a);
    
        protected IFlagRegisterField cfg_prs1missrsten_bit;
        partial void Cfg_Prs1missrsten_Write(bool a, bool b);
        partial void Cfg_Prs1missrsten_Read(bool a, bool b);
        partial void Cfg_Prs1missrsten_ValueProvider(bool a);
    
        protected IEnumRegisterField<CFG_PERSEL> cfg_persel_field;
        partial void Cfg_Persel_Write(CFG_PERSEL a, CFG_PERSEL b);
        partial void Cfg_Persel_Read(CFG_PERSEL a, CFG_PERSEL b);
        partial void Cfg_Persel_ValueProvider(CFG_PERSEL a);
    
        protected IEnumRegisterField<CFG_WARNSEL> cfg_warnsel_field;
        partial void Cfg_Warnsel_Write(CFG_WARNSEL a, CFG_WARNSEL b);
        partial void Cfg_Warnsel_Read(CFG_WARNSEL a, CFG_WARNSEL b);
        partial void Cfg_Warnsel_ValueProvider(CFG_WARNSEL a);
    
        protected IEnumRegisterField<CFG_WINSEL> cfg_winsel_field;
        partial void Cfg_Winsel_Write(CFG_WINSEL a, CFG_WINSEL b);
        partial void Cfg_Winsel_Read(CFG_WINSEL a, CFG_WINSEL b);
        partial void Cfg_Winsel_ValueProvider(CFG_WINSEL a);
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

        // Cmd - Offset : 0xC
    
        protected IEnumRegisterField<CMD_CLEAR> cmd_clear_bit;
        partial void Cmd_Clear_Write(CMD_CLEAR a, CMD_CLEAR b);
        partial void Cmd_Clear_ValueProvider(CMD_CLEAR a);
        protected void Cmd_Write_WithHook(uint a, uint b)
        {
            if (status_lock_bit.Value == STATUS_LOCK.LOCKED)
            {
                this.Log(LogLevel.Error, "Cmd: Write access to a locked register");
            }
            Cmd_Write(a, b);
        }
        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);

        // Status - Offset : 0x14
    
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);
        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);

        // If - Offset : 0x18
    
        protected IFlagRegisterField if_tout_bit;
        partial void If_Tout_Write(bool a, bool b);
        partial void If_Tout_Read(bool a, bool b);
        partial void If_Tout_ValueProvider(bool a);
    
        protected IFlagRegisterField if_warn_bit;
        partial void If_Warn_Write(bool a, bool b);
        partial void If_Warn_Read(bool a, bool b);
        partial void If_Warn_ValueProvider(bool a);
    
        protected IFlagRegisterField if_win_bit;
        partial void If_Win_Write(bool a, bool b);
        partial void If_Win_Read(bool a, bool b);
        partial void If_Win_ValueProvider(bool a);
    
        protected IFlagRegisterField if_pem0_bit;
        partial void If_Pem0_Write(bool a, bool b);
        partial void If_Pem0_Read(bool a, bool b);
        partial void If_Pem0_ValueProvider(bool a);
    
        protected IFlagRegisterField if_pem1_bit;
        partial void If_Pem1_Write(bool a, bool b);
        partial void If_Pem1_Read(bool a, bool b);
        partial void If_Pem1_ValueProvider(bool a);
        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);

        // Ien - Offset : 0x1C
    
        protected IFlagRegisterField ien_tout_bit;
        partial void Ien_Tout_Write(bool a, bool b);
        partial void Ien_Tout_Read(bool a, bool b);
        partial void Ien_Tout_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_warn_bit;
        partial void Ien_Warn_Write(bool a, bool b);
        partial void Ien_Warn_Read(bool a, bool b);
        partial void Ien_Warn_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_win_bit;
        partial void Ien_Win_Write(bool a, bool b);
        partial void Ien_Win_Read(bool a, bool b);
        partial void Ien_Win_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_pem0_bit;
        partial void Ien_Pem0_Write(bool a, bool b);
        partial void Ien_Pem0_Read(bool a, bool b);
        partial void Ien_Pem0_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_pem1_bit;
        partial void Ien_Pem1_Write(bool a, bool b);
        partial void Ien_Pem1_Read(bool a, bool b);
        partial void Ien_Pem1_ValueProvider(bool a);
        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);

        // Lock - Offset : 0x20
    
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);
        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);

        // Syncbusy - Offset : 0x24
    
        protected IFlagRegisterField syncbusy_cmd_bit;
        partial void Syncbusy_Cmd_Read(bool a, bool b);
        partial void Syncbusy_Cmd_ValueProvider(bool a);
        partial void Syncbusy_Write(uint a, uint b);
        partial void Syncbusy_Read(uint a, uint b);

        partial void WDOG_Reset();

        public override bool Enabled
        {
            get 
            {
                return en_en_bit.Value;
            }
        }

        protected override Type RegistersType => typeof(Registers);

        protected enum Registers
        {
            Ipversion = 0x0,
            En = 0x4,
            Cfg = 0x8,
            Cmd = 0xC,
            Status = 0x14,
            If = 0x18,
            Ien = 0x1C,
            Lock = 0x20,
            Syncbusy = 0x24,
            
            Ipversion_SET = 0x1000,
            En_SET = 0x1004,
            Cfg_SET = 0x1008,
            Cmd_SET = 0x100C,
            Status_SET = 0x1014,
            If_SET = 0x1018,
            Ien_SET = 0x101C,
            Lock_SET = 0x1020,
            Syncbusy_SET = 0x1024,
            
            Ipversion_CLR = 0x2000,
            En_CLR = 0x2004,
            Cfg_CLR = 0x2008,
            Cmd_CLR = 0x200C,
            Status_CLR = 0x2014,
            If_CLR = 0x2018,
            Ien_CLR = 0x201C,
            Lock_CLR = 0x2020,
            Syncbusy_CLR = 0x2024,
            
            Ipversion_TGL = 0x3000,
            En_TGL = 0x3004,
            Cfg_TGL = 0x3008,
            Cmd_TGL = 0x300C,
            Status_TGL = 0x3014,
            If_TGL = 0x3018,
            Ien_TGL = 0x301C,
            Lock_TGL = 0x3020,
            Syncbusy_TGL = 0x3024,
        }
    }

}
