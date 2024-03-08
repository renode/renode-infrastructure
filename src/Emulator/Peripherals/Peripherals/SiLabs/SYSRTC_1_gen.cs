/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    SYSRTC, Generated on : 2023-07-20 14:21:56.408744
    SYSRTC, ID Version : d8bb4e810eea4a3f9b6c35ed33d19576.1 */

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

namespace Antmicro.Renode.Peripherals.Silabs
{
    public partial class Sysrtc_1
    {
        public Sysrtc_1(Machine machine) : base(machine)
        {
            Sysrtc_1_constructor();
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


namespace Antmicro.Renode.Peripherals.Silabs
{
    public partial class Sysrtc_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public Sysrtc_1(Machine machine) : base(machine)
        {
            Define_Registers();
            Sysrtc_1_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.En, GenerateEnRegister()},
                {(long)Registers.Swrst, GenerateSwrstRegister()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Cnt, GenerateCntRegister()},
                {(long)Registers.Syncbusy, GenerateSyncbusyRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Faildetctrl, GenerateFaildetctrlRegister()},
                {(long)Registers.Faildetlock, GenerateFaildetlockRegister()},
                {(long)Registers.If_Group0, GenerateIf_group0Register()},
                {(long)Registers.Ien_Group0, GenerateIen_group0Register()},
                {(long)Registers.Ctrl_Group0, GenerateCtrl_group0Register()},
                {(long)Registers.Cmp0value_Group0, GenerateCmp0value_group0Register()},
                {(long)Registers.Cmp1value_Group0, GenerateCmp1value_group0Register()},
                {(long)Registers.Cap0value_Group0, GenerateCap0value_group0Register()},
                {(long)Registers.Syncbusy_Group0, GenerateSyncbusy_group0Register()},
                {(long)Registers.If_Group1, GenerateIf_group1Register()},
                {(long)Registers.Ien_Group1, GenerateIen_group1Register()},
                {(long)Registers.Ctrl_Group1, GenerateCtrl_group1Register()},
                {(long)Registers.Cmp0value_Group1, GenerateCmp0value_group1Register()},
                {(long)Registers.Cmp1value_Group1, GenerateCmp1value_group1Register()},
                {(long)Registers.Cap0value_Group1, GenerateCap0value_group1Register()},
                {(long)Registers.Syncbusy_Group1, GenerateSyncbusy_group1Register()},
                {(long)Registers.If_Group2, GenerateIf_group2Register()},
                {(long)Registers.Ien_Group2, GenerateIen_group2Register()},
                {(long)Registers.Ctrl_Group2, GenerateCtrl_group2Register()},
                {(long)Registers.Cmp0value_Group2, GenerateCmp0value_group2Register()},
                {(long)Registers.Syncbusy_Group2, GenerateSyncbusy_group2Register()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            SYSRTC_Reset();
        }
        
        protected enum CFG_DEBUGRUN
        {
            DISABLE = 0, // SYSRTC is frozen in debug mode
            ENABLE = 1, // SYSRTC is running in debug mode
        }
        
        protected enum STATUS_LOCKSTATUS
        {
            UNLOCKED = 0, // SYSRTC registers are unlocked
            LOCKED = 1, // SYSRTC registers are locked
        }
        
        protected enum STATUS_FAILDETLOCKSTATUS
        {
            UNLOCKED = 0, // FAILDETCTRL register is unlocked
            LOCKED = 1, // FAILDETCTRL register is locked
        }
        
        protected enum CTRL_GROUP0_CMP0CMOA
        {
            CLEAR = 0, // Cleared on the next cycle
            SET = 1, // Set on the next cycle
            PULSE = 2, // Set on the next cycle, cleared on the cycle after
            TOGGLE = 3, // Inverted on the next cycle
            CMPIF = 4, // Export this channel's CMP IF
        }
        
        protected enum CTRL_GROUP0_CMP1CMOA
        {
            CLEAR = 0, // Cleared on the next cycle
            SET = 1, // Set on the next cycle
            PULSE = 2, // Set on the next cycle, cleared on the cycle after
            TOGGLE = 3, // Inverted on the next cycle
            CMPIF = 4, // Export this channel's CMP IF
        }
        
        protected enum CTRL_GROUP0_CAP0EDGE
        {
            RISING = 0, // Rising edges detected
            FALLING = 1, // Falling edges detected
            BOTH = 2, // Both edges detected
        }
        
        protected enum CTRL_GROUP1_CMP0CMOA
        {
            CLEAR = 0, // Cleared on the next cycle
            SET = 1, // Set on the next cycle
            PULSE = 2, // Set on the next cycle, cleared on the cycle after
            TOGGLE = 3, // Inverted on the next cycle
            CMPIF = 4, // Export this channel's CMP IF
        }
        
        protected enum CTRL_GROUP1_CMP1CMOA
        {
            CLEAR = 0, // Cleared on the next cycle
            SET = 1, // Set on the next cycle
            PULSE = 2, // Set on the next cycle, cleared on the cycle after
            TOGGLE = 3, // Inverted on the next cycle
            CMPIF = 4, // Export this channel's CMP IF
        }
        
        protected enum CTRL_GROUP1_CAP0EDGE
        {
            RISING = 0, // Rising edges detected
            FALLING = 1, // Falling edges detected
            BOTH = 2, // Both edges detected
        }
        
        protected enum CTRL_GROUP2_CMP0CMOA
        {
            CLEAR = 0, // Cleared on the next cycle
            SET = 1, // Set on the next cycle
            PULSE = 2, // Set on the next cycle, cleared on the cycle after
            TOGGLE = 3, // Inverted on the next cycle
            CMPIF = 4, // Export this channel's CMP IF
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
        
        // Swrst - Offset : 0x8
        protected DoubleWordRegister  GenerateSwrstRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out swrst_swrst_bit, FieldMode.Write,
                    writeCallback: (_, __) => Swrst_Swrst_Write(_, __),
                    name: "Swrst")
            .WithFlag(1, out swrst_resetting_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Swrst_Resetting_ValueProvider(_);
                        return swrst_resetting_bit.Value;               
                    },
                    readCallback: (_, __) => Swrst_Resetting_Read(_, __),
                    name: "Resetting")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Swrst_Read(_, __))
            .WithWriteCallback((_, __) => Swrst_Write(_, __));
        
        // Cfg - Offset : 0xC
        protected DoubleWordRegister  GenerateCfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CFG_DEBUGRUN>(0, 1, out cfg_debugrun_bit, 
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
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Cfg_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Write(_, __));
        
        // Cmd - Offset : 0x10
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cmd_start_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Start_Write(_, __),
                    name: "Start")
            .WithFlag(1, out cmd_stop_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Stop_Write(_, __),
                    name: "Stop")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write(_, __));
        
        // Status - Offset : 0x14
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_running_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Running_ValueProvider(_);
                        return status_running_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Running_Read(_, __),
                    name: "Running")
            .WithEnumField<DoubleWordRegister, STATUS_LOCKSTATUS>(1, 1, out status_lockstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lockstatus_ValueProvider(_);
                        return status_lockstatus_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Lockstatus_Read(_, __),
                    name: "Lockstatus")
            .WithEnumField<DoubleWordRegister, STATUS_FAILDETLOCKSTATUS>(2, 1, out status_faildetlockstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Faildetlockstatus_ValueProvider(_);
                        return status_faildetlockstatus_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Faildetlockstatus_Read(_, __),
                    name: "Faildetlockstatus")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Cnt - Offset : 0x18
        protected DoubleWordRegister  GenerateCntRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cnt_cnt_field, 
                    valueProviderCallback: (_) => {
                        Cnt_Cnt_ValueProvider(_);
                        return cnt_cnt_field.Value;               
                    },
                    writeCallback: (_, __) => Cnt_Cnt_Write(_, __),
                    readCallback: (_, __) => Cnt_Cnt_Read(_, __),
                    name: "Cnt")
            .WithReadCallback((_, __) => Cnt_Read(_, __))
            .WithWriteCallback((_, __) => Cnt_Write(_, __));
        
        // Syncbusy - Offset : 0x1C
        protected DoubleWordRegister  GenerateSyncbusyRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_start_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Start_ValueProvider(_);
                        return syncbusy_start_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Start_Read(_, __),
                    name: "Start")
            .WithFlag(1, out syncbusy_stop_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Stop_ValueProvider(_);
                        return syncbusy_stop_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Stop_Read(_, __),
                    name: "Stop")
            .WithFlag(2, out syncbusy_cnt_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Cnt_ValueProvider(_);
                        return syncbusy_cnt_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Cnt_Read(_, __),
                    name: "Cnt")
            .WithFlag(3, out syncbusy_faildetctrl_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Faildetctrl_ValueProvider(_);
                        return syncbusy_faildetctrl_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Faildetctrl_Read(_, __),
                    name: "Faildetctrl")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Syncbusy_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Write(_, __));
        
        // Lock - Offset : 0x20
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Faildetctrl - Offset : 0x30
        protected DoubleWordRegister  GenerateFaildetctrlRegister() => new DoubleWordRegister(this, 0x1030)
            .WithValueField(0, 6, out faildetctrl_failcnthi_field, 
                    valueProviderCallback: (_) => {
                        Faildetctrl_Failcnthi_ValueProvider(_);
                        return faildetctrl_failcnthi_field.Value;               
                    },
                    writeCallback: (_, __) => Faildetctrl_Failcnthi_Write(_, __),
                    readCallback: (_, __) => Faildetctrl_Failcnthi_Read(_, __),
                    name: "Failcnthi")
            .WithReservedBits(6, 2)
            .WithValueField(8, 5, out faildetctrl_failcntlo_field, 
                    valueProviderCallback: (_) => {
                        Faildetctrl_Failcntlo_ValueProvider(_);
                        return faildetctrl_failcntlo_field.Value;               
                    },
                    writeCallback: (_, __) => Faildetctrl_Failcntlo_Write(_, __),
                    readCallback: (_, __) => Faildetctrl_Failcntlo_Read(_, __),
                    name: "Failcntlo")
            .WithReservedBits(13, 3)
            .WithFlag(16, out faildetctrl_faildeten_bit, 
                    valueProviderCallback: (_) => {
                        Faildetctrl_Faildeten_ValueProvider(_);
                        return faildetctrl_faildeten_bit.Value;               
                    },
                    writeCallback: (_, __) => Faildetctrl_Faildeten_Write(_, __),
                    readCallback: (_, __) => Faildetctrl_Faildeten_Read(_, __),
                    name: "Faildeten")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Faildetctrl_Read(_, __))
            .WithWriteCallback((_, __) => Faildetctrl_Write(_, __));
        
        // Faildetlock - Offset : 0x34
        protected DoubleWordRegister  GenerateFaildetlockRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out faildetlock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Faildetlock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Faildetlock_Read(_, __))
            .WithWriteCallback((_, __) => Faildetlock_Write(_, __));
        
        // If_Group0 - Offset : 0x40
        protected DoubleWordRegister  GenerateIf_group0Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_group0_ovf_bit, 
                    valueProviderCallback: (_) => {
                        If_Group0_Ovf_ValueProvider(_);
                        return if_group0_ovf_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group0_Ovf_Write(_, __),
                    readCallback: (_, __) => If_Group0_Ovf_Read(_, __),
                    name: "Ovf")
            .WithFlag(1, out if_group0_cmp0_bit, 
                    valueProviderCallback: (_) => {
                        If_Group0_Cmp0_ValueProvider(_);
                        return if_group0_cmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group0_Cmp0_Write(_, __),
                    readCallback: (_, __) => If_Group0_Cmp0_Read(_, __),
                    name: "Cmp0")
            .WithFlag(2, out if_group0_cmp1_bit, 
                    valueProviderCallback: (_) => {
                        If_Group0_Cmp1_ValueProvider(_);
                        return if_group0_cmp1_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group0_Cmp1_Write(_, __),
                    readCallback: (_, __) => If_Group0_Cmp1_Read(_, __),
                    name: "Cmp1")
            .WithFlag(3, out if_group0_cap0_bit, 
                    valueProviderCallback: (_) => {
                        If_Group0_Cap0_ValueProvider(_);
                        return if_group0_cap0_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group0_Cap0_Write(_, __),
                    readCallback: (_, __) => If_Group0_Cap0_Read(_, __),
                    name: "Cap0")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => If_Group0_Read(_, __))
            .WithWriteCallback((_, __) => If_Group0_Write(_, __));
        
        // Ien_Group0 - Offset : 0x44
        protected DoubleWordRegister  GenerateIen_group0Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_group0_ovf_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group0_Ovf_ValueProvider(_);
                        return ien_group0_ovf_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group0_Ovf_Write(_, __),
                    readCallback: (_, __) => Ien_Group0_Ovf_Read(_, __),
                    name: "Ovf")
            .WithFlag(1, out ien_group0_cmp0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group0_Cmp0_ValueProvider(_);
                        return ien_group0_cmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group0_Cmp0_Write(_, __),
                    readCallback: (_, __) => Ien_Group0_Cmp0_Read(_, __),
                    name: "Cmp0")
            .WithFlag(2, out ien_group0_cmp1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group0_Cmp1_ValueProvider(_);
                        return ien_group0_cmp1_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group0_Cmp1_Write(_, __),
                    readCallback: (_, __) => Ien_Group0_Cmp1_Read(_, __),
                    name: "Cmp1")
            .WithFlag(3, out ien_group0_cap0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group0_Cap0_ValueProvider(_);
                        return ien_group0_cap0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group0_Cap0_Write(_, __),
                    readCallback: (_, __) => Ien_Group0_Cap0_Read(_, __),
                    name: "Cap0")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Ien_Group0_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Group0_Write(_, __));
        
        // Ctrl_Group0 - Offset : 0x48
        protected DoubleWordRegister  GenerateCtrl_group0Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ctrl_group0_cmp0en_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group0_Cmp0en_ValueProvider(_);
                        return ctrl_group0_cmp0en_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group0_Cmp0en_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group0_Cmp0en_Read(_, __),
                    name: "Cmp0en")
            .WithFlag(1, out ctrl_group0_cmp1en_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group0_Cmp1en_ValueProvider(_);
                        return ctrl_group0_cmp1en_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group0_Cmp1en_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group0_Cmp1en_Read(_, __),
                    name: "Cmp1en")
            .WithFlag(2, out ctrl_group0_cap0en_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group0_Cap0en_ValueProvider(_);
                        return ctrl_group0_cap0en_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group0_Cap0en_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group0_Cap0en_Read(_, __),
                    name: "Cap0en")
            .WithEnumField<DoubleWordRegister, CTRL_GROUP0_CMP0CMOA>(3, 3, out ctrl_group0_cmp0cmoa_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group0_Cmp0cmoa_ValueProvider(_);
                        return ctrl_group0_cmp0cmoa_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group0_Cmp0cmoa_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group0_Cmp0cmoa_Read(_, __),
                    name: "Cmp0cmoa")
            .WithEnumField<DoubleWordRegister, CTRL_GROUP0_CMP1CMOA>(6, 3, out ctrl_group0_cmp1cmoa_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group0_Cmp1cmoa_ValueProvider(_);
                        return ctrl_group0_cmp1cmoa_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group0_Cmp1cmoa_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group0_Cmp1cmoa_Read(_, __),
                    name: "Cmp1cmoa")
            .WithEnumField<DoubleWordRegister, CTRL_GROUP0_CAP0EDGE>(9, 2, out ctrl_group0_cap0edge_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group0_Cap0edge_ValueProvider(_);
                        return ctrl_group0_cap0edge_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group0_Cap0edge_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group0_Cap0edge_Read(_, __),
                    name: "Cap0edge")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Ctrl_Group0_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Group0_Write(_, __));
        
        // Cmp0value_Group0 - Offset : 0x4C
        protected DoubleWordRegister  GenerateCmp0value_group0Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cmp0value_group0_cmp0value_field, 
                    valueProviderCallback: (_) => {
                        Cmp0value_Group0_Cmp0value_ValueProvider(_);
                        return cmp0value_group0_cmp0value_field.Value;               
                    },
                    writeCallback: (_, __) => Cmp0value_Group0_Cmp0value_Write(_, __),
                    readCallback: (_, __) => Cmp0value_Group0_Cmp0value_Read(_, __),
                    name: "Cmp0value")
            .WithReadCallback((_, __) => Cmp0value_Group0_Read(_, __))
            .WithWriteCallback((_, __) => Cmp0value_Group0_Write(_, __));
        
        // Cmp1value_Group0 - Offset : 0x50
        protected DoubleWordRegister  GenerateCmp1value_group0Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cmp1value_group0_cmp1value_field, 
                    valueProviderCallback: (_) => {
                        Cmp1value_Group0_Cmp1value_ValueProvider(_);
                        return cmp1value_group0_cmp1value_field.Value;               
                    },
                    writeCallback: (_, __) => Cmp1value_Group0_Cmp1value_Write(_, __),
                    readCallback: (_, __) => Cmp1value_Group0_Cmp1value_Read(_, __),
                    name: "Cmp1value")
            .WithReadCallback((_, __) => Cmp1value_Group0_Read(_, __))
            .WithWriteCallback((_, __) => Cmp1value_Group0_Write(_, __));
        
        // Cap0value_Group0 - Offset : 0x54
        protected DoubleWordRegister  GenerateCap0value_group0Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cap0value_group0_cap0value_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cap0value_Group0_Cap0value_ValueProvider(_);
                        return cap0value_group0_cap0value_field.Value;               
                    },
                    readCallback: (_, __) => Cap0value_Group0_Cap0value_Read(_, __),
                    name: "Cap0value")
            .WithReadCallback((_, __) => Cap0value_Group0_Read(_, __))
            .WithWriteCallback((_, __) => Cap0value_Group0_Write(_, __));
        
        // Syncbusy_Group0 - Offset : 0x58
        protected DoubleWordRegister  GenerateSyncbusy_group0Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_group0_ctrl_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group0_Ctrl_ValueProvider(_);
                        return syncbusy_group0_ctrl_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group0_Ctrl_Read(_, __),
                    name: "Ctrl")
            .WithFlag(1, out syncbusy_group0_cmp0value_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group0_Cmp0value_ValueProvider(_);
                        return syncbusy_group0_cmp0value_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group0_Cmp0value_Read(_, __),
                    name: "Cmp0value")
            .WithFlag(2, out syncbusy_group0_cmp1value_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group0_Cmp1value_ValueProvider(_);
                        return syncbusy_group0_cmp1value_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group0_Cmp1value_Read(_, __),
                    name: "Cmp1value")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Syncbusy_Group0_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Group0_Write(_, __));
        
        // If_Group1 - Offset : 0x60
        protected DoubleWordRegister  GenerateIf_group1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_group1_ovf_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Ovf_ValueProvider(_);
                        return if_group1_ovf_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Ovf_Write(_, __),
                    readCallback: (_, __) => If_Group1_Ovf_Read(_, __),
                    name: "Ovf")
            .WithFlag(1, out if_group1_cmp0_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Cmp0_ValueProvider(_);
                        return if_group1_cmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Cmp0_Write(_, __),
                    readCallback: (_, __) => If_Group1_Cmp0_Read(_, __),
                    name: "Cmp0")
            .WithFlag(2, out if_group1_cmp1_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Cmp1_ValueProvider(_);
                        return if_group1_cmp1_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Cmp1_Write(_, __),
                    readCallback: (_, __) => If_Group1_Cmp1_Read(_, __),
                    name: "Cmp1")
            .WithFlag(3, out if_group1_cap0_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Cap0_ValueProvider(_);
                        return if_group1_cap0_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Cap0_Write(_, __),
                    readCallback: (_, __) => If_Group1_Cap0_Read(_, __),
                    name: "Cap0")
            .WithFlag(4, out if_group1_altovf_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Altovf_ValueProvider(_);
                        return if_group1_altovf_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Altovf_Write(_, __),
                    readCallback: (_, __) => If_Group1_Altovf_Read(_, __),
                    name: "Altovf")
            .WithFlag(5, out if_group1_altcmp0_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Altcmp0_ValueProvider(_);
                        return if_group1_altcmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Altcmp0_Write(_, __),
                    readCallback: (_, __) => If_Group1_Altcmp0_Read(_, __),
                    name: "Altcmp0")
            .WithFlag(6, out if_group1_altcmp1_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Altcmp1_ValueProvider(_);
                        return if_group1_altcmp1_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Altcmp1_Write(_, __),
                    readCallback: (_, __) => If_Group1_Altcmp1_Read(_, __),
                    name: "Altcmp1")
            .WithFlag(7, out if_group1_altcap0_bit, 
                    valueProviderCallback: (_) => {
                        If_Group1_Altcap0_ValueProvider(_);
                        return if_group1_altcap0_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group1_Altcap0_Write(_, __),
                    readCallback: (_, __) => If_Group1_Altcap0_Read(_, __),
                    name: "Altcap0")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => If_Group1_Read(_, __))
            .WithWriteCallback((_, __) => If_Group1_Write(_, __));
        
        // Ien_Group1 - Offset : 0x64
        protected DoubleWordRegister  GenerateIen_group1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_group1_ovf_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Ovf_ValueProvider(_);
                        return ien_group1_ovf_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Ovf_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Ovf_Read(_, __),
                    name: "Ovf")
            .WithFlag(1, out ien_group1_cmp0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Cmp0_ValueProvider(_);
                        return ien_group1_cmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Cmp0_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Cmp0_Read(_, __),
                    name: "Cmp0")
            .WithFlag(2, out ien_group1_cmp1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Cmp1_ValueProvider(_);
                        return ien_group1_cmp1_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Cmp1_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Cmp1_Read(_, __),
                    name: "Cmp1")
            .WithFlag(3, out ien_group1_cap0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Cap0_ValueProvider(_);
                        return ien_group1_cap0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Cap0_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Cap0_Read(_, __),
                    name: "Cap0")
            .WithFlag(4, out ien_group1_altovf_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Altovf_ValueProvider(_);
                        return ien_group1_altovf_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Altovf_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Altovf_Read(_, __),
                    name: "Altovf")
            .WithFlag(5, out ien_group1_altcmp0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Altcmp0_ValueProvider(_);
                        return ien_group1_altcmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Altcmp0_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Altcmp0_Read(_, __),
                    name: "Altcmp0")
            .WithFlag(6, out ien_group1_altcmp1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Altcmp1_ValueProvider(_);
                        return ien_group1_altcmp1_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Altcmp1_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Altcmp1_Read(_, __),
                    name: "Altcmp1")
            .WithFlag(7, out ien_group1_altcap0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group1_Altcap0_ValueProvider(_);
                        return ien_group1_altcap0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group1_Altcap0_Write(_, __),
                    readCallback: (_, __) => Ien_Group1_Altcap0_Read(_, __),
                    name: "Altcap0")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Ien_Group1_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Group1_Write(_, __));
        
        // Ctrl_Group1 - Offset : 0x68
        protected DoubleWordRegister  GenerateCtrl_group1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ctrl_group1_cmp0en_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group1_Cmp0en_ValueProvider(_);
                        return ctrl_group1_cmp0en_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group1_Cmp0en_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group1_Cmp0en_Read(_, __),
                    name: "Cmp0en")
            .WithFlag(1, out ctrl_group1_cmp1en_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group1_Cmp1en_ValueProvider(_);
                        return ctrl_group1_cmp1en_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group1_Cmp1en_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group1_Cmp1en_Read(_, __),
                    name: "Cmp1en")
            .WithFlag(2, out ctrl_group1_cap0en_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group1_Cap0en_ValueProvider(_);
                        return ctrl_group1_cap0en_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group1_Cap0en_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group1_Cap0en_Read(_, __),
                    name: "Cap0en")
            .WithEnumField<DoubleWordRegister, CTRL_GROUP1_CMP0CMOA>(3, 3, out ctrl_group1_cmp0cmoa_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group1_Cmp0cmoa_ValueProvider(_);
                        return ctrl_group1_cmp0cmoa_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group1_Cmp0cmoa_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group1_Cmp0cmoa_Read(_, __),
                    name: "Cmp0cmoa")
            .WithEnumField<DoubleWordRegister, CTRL_GROUP1_CMP1CMOA>(6, 3, out ctrl_group1_cmp1cmoa_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group1_Cmp1cmoa_ValueProvider(_);
                        return ctrl_group1_cmp1cmoa_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group1_Cmp1cmoa_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group1_Cmp1cmoa_Read(_, __),
                    name: "Cmp1cmoa")
            .WithEnumField<DoubleWordRegister, CTRL_GROUP1_CAP0EDGE>(9, 2, out ctrl_group1_cap0edge_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group1_Cap0edge_ValueProvider(_);
                        return ctrl_group1_cap0edge_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group1_Cap0edge_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group1_Cap0edge_Read(_, __),
                    name: "Cap0edge")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Ctrl_Group1_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Group1_Write(_, __));
        
        // Cmp0value_Group1 - Offset : 0x6C
        protected DoubleWordRegister  GenerateCmp0value_group1Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cmp0value_group1_cmp0value_field, 
                    valueProviderCallback: (_) => {
                        Cmp0value_Group1_Cmp0value_ValueProvider(_);
                        return cmp0value_group1_cmp0value_field.Value;               
                    },
                    writeCallback: (_, __) => Cmp0value_Group1_Cmp0value_Write(_, __),
                    readCallback: (_, __) => Cmp0value_Group1_Cmp0value_Read(_, __),
                    name: "Cmp0value")
            .WithReadCallback((_, __) => Cmp0value_Group1_Read(_, __))
            .WithWriteCallback((_, __) => Cmp0value_Group1_Write(_, __));
        
        // Cmp1value_Group1 - Offset : 0x70
        protected DoubleWordRegister  GenerateCmp1value_group1Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cmp1value_group1_cmp1value_field, 
                    valueProviderCallback: (_) => {
                        Cmp1value_Group1_Cmp1value_ValueProvider(_);
                        return cmp1value_group1_cmp1value_field.Value;               
                    },
                    writeCallback: (_, __) => Cmp1value_Group1_Cmp1value_Write(_, __),
                    readCallback: (_, __) => Cmp1value_Group1_Cmp1value_Read(_, __),
                    name: "Cmp1value")
            .WithReadCallback((_, __) => Cmp1value_Group1_Read(_, __))
            .WithWriteCallback((_, __) => Cmp1value_Group1_Write(_, __));
        
        // Cap0value_Group1 - Offset : 0x74
        protected DoubleWordRegister  GenerateCap0value_group1Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cap0value_group1_cap0value_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cap0value_Group1_Cap0value_ValueProvider(_);
                        return cap0value_group1_cap0value_field.Value;               
                    },
                    readCallback: (_, __) => Cap0value_Group1_Cap0value_Read(_, __),
                    name: "Cap0value")
            .WithReadCallback((_, __) => Cap0value_Group1_Read(_, __))
            .WithWriteCallback((_, __) => Cap0value_Group1_Write(_, __));
        
        // Syncbusy_Group1 - Offset : 0x78
        protected DoubleWordRegister  GenerateSyncbusy_group1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_group1_ctrl_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group1_Ctrl_ValueProvider(_);
                        return syncbusy_group1_ctrl_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group1_Ctrl_Read(_, __),
                    name: "Ctrl")
            .WithFlag(1, out syncbusy_group1_cmp0value_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group1_Cmp0value_ValueProvider(_);
                        return syncbusy_group1_cmp0value_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group1_Cmp0value_Read(_, __),
                    name: "Cmp0value")
            .WithFlag(2, out syncbusy_group1_cmp1value_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group1_Cmp1value_ValueProvider(_);
                        return syncbusy_group1_cmp1value_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group1_Cmp1value_Read(_, __),
                    name: "Cmp1value")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Syncbusy_Group1_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Group1_Write(_, __));
        
        // If_Group2 - Offset : 0x80
        protected DoubleWordRegister  GenerateIf_group2Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_group2_ovf_bit, 
                    valueProviderCallback: (_) => {
                        If_Group2_Ovf_ValueProvider(_);
                        return if_group2_ovf_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group2_Ovf_Write(_, __),
                    readCallback: (_, __) => If_Group2_Ovf_Read(_, __),
                    name: "Ovf")
            .WithFlag(1, out if_group2_cmp0_bit, 
                    valueProviderCallback: (_) => {
                        If_Group2_Cmp0_ValueProvider(_);
                        return if_group2_cmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group2_Cmp0_Write(_, __),
                    readCallback: (_, __) => If_Group2_Cmp0_Read(_, __),
                    name: "Cmp0")
            .WithReservedBits(2, 6)
            .WithFlag(8, out if_group2_faildet_bit, 
                    valueProviderCallback: (_) => {
                        If_Group2_Faildet_ValueProvider(_);
                        return if_group2_faildet_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group2_Faildet_Write(_, __),
                    readCallback: (_, __) => If_Group2_Faildet_Read(_, __),
                    name: "Faildet")
            .WithFlag(9, out if_group2_tamper_bit, 
                    valueProviderCallback: (_) => {
                        If_Group2_Tamper_ValueProvider(_);
                        return if_group2_tamper_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Group2_Tamper_Write(_, __),
                    readCallback: (_, __) => If_Group2_Tamper_Read(_, __),
                    name: "Tamper")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => If_Group2_Read(_, __))
            .WithWriteCallback((_, __) => If_Group2_Write(_, __));
        
        // Ien_Group2 - Offset : 0x84
        protected DoubleWordRegister  GenerateIen_group2Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_group2_ovf_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group2_Ovf_ValueProvider(_);
                        return ien_group2_ovf_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group2_Ovf_Write(_, __),
                    readCallback: (_, __) => Ien_Group2_Ovf_Read(_, __),
                    name: "Ovf")
            .WithFlag(1, out ien_group2_cmp0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group2_Cmp0_ValueProvider(_);
                        return ien_group2_cmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group2_Cmp0_Write(_, __),
                    readCallback: (_, __) => Ien_Group2_Cmp0_Read(_, __),
                    name: "Cmp0")
            .WithReservedBits(2, 6)
            .WithFlag(8, out ien_group2_faildet_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group2_Faildet_ValueProvider(_);
                        return ien_group2_faildet_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group2_Faildet_Write(_, __),
                    readCallback: (_, __) => Ien_Group2_Faildet_Read(_, __),
                    name: "Faildet")
            .WithFlag(9, out ien_group2_tamper_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Group2_Tamper_ValueProvider(_);
                        return ien_group2_tamper_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Group2_Tamper_Write(_, __),
                    readCallback: (_, __) => Ien_Group2_Tamper_Read(_, __),
                    name: "Tamper")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Ien_Group2_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Group2_Write(_, __));
        
        // Ctrl_Group2 - Offset : 0x88
        protected DoubleWordRegister  GenerateCtrl_group2Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ctrl_group2_cmp0en_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group2_Cmp0en_ValueProvider(_);
                        return ctrl_group2_cmp0en_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group2_Cmp0en_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group2_Cmp0en_Read(_, __),
                    name: "Cmp0en")
            .WithReservedBits(1, 2)
            .WithEnumField<DoubleWordRegister, CTRL_GROUP2_CMP0CMOA>(3, 3, out ctrl_group2_cmp0cmoa_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Group2_Cmp0cmoa_ValueProvider(_);
                        return ctrl_group2_cmp0cmoa_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Group2_Cmp0cmoa_Write(_, __),
                    readCallback: (_, __) => Ctrl_Group2_Cmp0cmoa_Read(_, __),
                    name: "Cmp0cmoa")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Ctrl_Group2_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Group2_Write(_, __));
        
        // Cmp0value_Group2 - Offset : 0x8C
        protected DoubleWordRegister  GenerateCmp0value_group2Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cmp0value_group2_cmp0value_field, 
                    valueProviderCallback: (_) => {
                        Cmp0value_Group2_Cmp0value_ValueProvider(_);
                        return cmp0value_group2_cmp0value_field.Value;               
                    },
                    writeCallback: (_, __) => Cmp0value_Group2_Cmp0value_Write(_, __),
                    readCallback: (_, __) => Cmp0value_Group2_Cmp0value_Read(_, __),
                    name: "Cmp0value")
            .WithReadCallback((_, __) => Cmp0value_Group2_Read(_, __))
            .WithWriteCallback((_, __) => Cmp0value_Group2_Write(_, __));
        
        // Syncbusy_Group2 - Offset : 0x98
        protected DoubleWordRegister  GenerateSyncbusy_group2Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_group2_ctrl_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group2_Ctrl_ValueProvider(_);
                        return syncbusy_group2_ctrl_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group2_Ctrl_Read(_, __),
                    name: "Ctrl")
            .WithFlag(1, out syncbusy_group2_cmp0value_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Group2_Cmp0value_ValueProvider(_);
                        return syncbusy_group2_cmp0value_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Group2_Cmp0value_Read(_, __),
                    name: "Cmp0value")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Syncbusy_Group2_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Group2_Write(_, __));
        

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
                // throw new InvalidOperationException("Bus fault trying to write to a WSTATIC register while peripheral is enabled");
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
        
        // Swrst - Offset : 0x8
        protected IFlagRegisterField swrst_swrst_bit;
        partial void Swrst_Swrst_Write(bool a, bool b);
        partial void Swrst_Swrst_ValueProvider(bool a);
        protected IFlagRegisterField swrst_resetting_bit;
        partial void Swrst_Resetting_Read(bool a, bool b);
        partial void Swrst_Resetting_ValueProvider(bool a);

        partial void Swrst_Write(uint a, uint b);
        partial void Swrst_Read(uint a, uint b);
        
        // Cfg - Offset : 0xC
        protected IEnumRegisterField<CFG_DEBUGRUN> cfg_debugrun_bit;
        partial void Cfg_Debugrun_Write(CFG_DEBUGRUN a, CFG_DEBUGRUN b);
        partial void Cfg_Debugrun_Read(CFG_DEBUGRUN a, CFG_DEBUGRUN b);
        partial void Cfg_Debugrun_ValueProvider(CFG_DEBUGRUN a);

        partial void Cfg_Write(uint a, uint b);
        partial void Cfg_Read(uint a, uint b);
        
        // Cmd - Offset : 0x10
        protected IFlagRegisterField cmd_start_bit;
        partial void Cmd_Start_Write(bool a, bool b);
        partial void Cmd_Start_ValueProvider(bool a);
        protected IFlagRegisterField cmd_stop_bit;
        partial void Cmd_Stop_Write(bool a, bool b);
        partial void Cmd_Stop_ValueProvider(bool a);

        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);
        
        // Status - Offset : 0x14
        protected IFlagRegisterField status_running_bit;
        partial void Status_Running_Read(bool a, bool b);
        partial void Status_Running_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_LOCKSTATUS> status_lockstatus_bit;
        partial void Status_Lockstatus_Read(STATUS_LOCKSTATUS a, STATUS_LOCKSTATUS b);
        partial void Status_Lockstatus_ValueProvider(STATUS_LOCKSTATUS a);
        protected IEnumRegisterField<STATUS_FAILDETLOCKSTATUS> status_faildetlockstatus_bit;
        partial void Status_Faildetlockstatus_Read(STATUS_FAILDETLOCKSTATUS a, STATUS_FAILDETLOCKSTATUS b);
        partial void Status_Faildetlockstatus_ValueProvider(STATUS_FAILDETLOCKSTATUS a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // Cnt - Offset : 0x18
        protected IValueRegisterField cnt_cnt_field;
        partial void Cnt_Cnt_Write(ulong a, ulong b);
        partial void Cnt_Cnt_Read(ulong a, ulong b);
        partial void Cnt_Cnt_ValueProvider(ulong a);

        partial void Cnt_Write(uint a, uint b);
        partial void Cnt_Read(uint a, uint b);
        
        // Syncbusy - Offset : 0x1C
        protected IFlagRegisterField syncbusy_start_bit;
        partial void Syncbusy_Start_Read(bool a, bool b);
        partial void Syncbusy_Start_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_stop_bit;
        partial void Syncbusy_Stop_Read(bool a, bool b);
        partial void Syncbusy_Stop_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_cnt_bit;
        partial void Syncbusy_Cnt_Read(bool a, bool b);
        partial void Syncbusy_Cnt_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_faildetctrl_bit;
        partial void Syncbusy_Faildetctrl_Read(bool a, bool b);
        partial void Syncbusy_Faildetctrl_ValueProvider(bool a);

        partial void Syncbusy_Write(uint a, uint b);
        partial void Syncbusy_Read(uint a, uint b);
        
        // Lock - Offset : 0x20
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // Faildetctrl - Offset : 0x30
        protected IValueRegisterField faildetctrl_failcnthi_field;
        partial void Faildetctrl_Failcnthi_Write(ulong a, ulong b);
        partial void Faildetctrl_Failcnthi_Read(ulong a, ulong b);
        partial void Faildetctrl_Failcnthi_ValueProvider(ulong a);
        protected IValueRegisterField faildetctrl_failcntlo_field;
        partial void Faildetctrl_Failcntlo_Write(ulong a, ulong b);
        partial void Faildetctrl_Failcntlo_Read(ulong a, ulong b);
        partial void Faildetctrl_Failcntlo_ValueProvider(ulong a);
        protected IFlagRegisterField faildetctrl_faildeten_bit;
        partial void Faildetctrl_Faildeten_Write(bool a, bool b);
        partial void Faildetctrl_Faildeten_Read(bool a, bool b);
        partial void Faildetctrl_Faildeten_ValueProvider(bool a);

        partial void Faildetctrl_Write(uint a, uint b);
        partial void Faildetctrl_Read(uint a, uint b);
        
        // Faildetlock - Offset : 0x34
        protected IValueRegisterField faildetlock_lockkey_field;
        partial void Faildetlock_Lockkey_Write(ulong a, ulong b);
        partial void Faildetlock_Lockkey_ValueProvider(ulong a);

        partial void Faildetlock_Write(uint a, uint b);
        partial void Faildetlock_Read(uint a, uint b);
        
        // If_Group0 - Offset : 0x40
        protected IFlagRegisterField if_group0_ovf_bit;
        partial void If_Group0_Ovf_Write(bool a, bool b);
        partial void If_Group0_Ovf_Read(bool a, bool b);
        partial void If_Group0_Ovf_ValueProvider(bool a);
        protected IFlagRegisterField if_group0_cmp0_bit;
        partial void If_Group0_Cmp0_Write(bool a, bool b);
        partial void If_Group0_Cmp0_Read(bool a, bool b);
        partial void If_Group0_Cmp0_ValueProvider(bool a);
        protected IFlagRegisterField if_group0_cmp1_bit;
        partial void If_Group0_Cmp1_Write(bool a, bool b);
        partial void If_Group0_Cmp1_Read(bool a, bool b);
        partial void If_Group0_Cmp1_ValueProvider(bool a);
        protected IFlagRegisterField if_group0_cap0_bit;
        partial void If_Group0_Cap0_Write(bool a, bool b);
        partial void If_Group0_Cap0_Read(bool a, bool b);
        partial void If_Group0_Cap0_ValueProvider(bool a);

        partial void If_Group0_Write(uint a, uint b);
        partial void If_Group0_Read(uint a, uint b);
        
        // Ien_Group0 - Offset : 0x44
        protected IFlagRegisterField ien_group0_ovf_bit;
        partial void Ien_Group0_Ovf_Write(bool a, bool b);
        partial void Ien_Group0_Ovf_Read(bool a, bool b);
        partial void Ien_Group0_Ovf_ValueProvider(bool a);
        protected IFlagRegisterField ien_group0_cmp0_bit;
        partial void Ien_Group0_Cmp0_Write(bool a, bool b);
        partial void Ien_Group0_Cmp0_Read(bool a, bool b);
        partial void Ien_Group0_Cmp0_ValueProvider(bool a);
        protected IFlagRegisterField ien_group0_cmp1_bit;
        partial void Ien_Group0_Cmp1_Write(bool a, bool b);
        partial void Ien_Group0_Cmp1_Read(bool a, bool b);
        partial void Ien_Group0_Cmp1_ValueProvider(bool a);
        protected IFlagRegisterField ien_group0_cap0_bit;
        partial void Ien_Group0_Cap0_Write(bool a, bool b);
        partial void Ien_Group0_Cap0_Read(bool a, bool b);
        partial void Ien_Group0_Cap0_ValueProvider(bool a);

        partial void Ien_Group0_Write(uint a, uint b);
        partial void Ien_Group0_Read(uint a, uint b);
        
        // Ctrl_Group0 - Offset : 0x48
        protected IFlagRegisterField ctrl_group0_cmp0en_bit;
        partial void Ctrl_Group0_Cmp0en_Write(bool a, bool b);
        partial void Ctrl_Group0_Cmp0en_Read(bool a, bool b);
        partial void Ctrl_Group0_Cmp0en_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_group0_cmp1en_bit;
        partial void Ctrl_Group0_Cmp1en_Write(bool a, bool b);
        partial void Ctrl_Group0_Cmp1en_Read(bool a, bool b);
        partial void Ctrl_Group0_Cmp1en_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_group0_cap0en_bit;
        partial void Ctrl_Group0_Cap0en_Write(bool a, bool b);
        partial void Ctrl_Group0_Cap0en_Read(bool a, bool b);
        partial void Ctrl_Group0_Cap0en_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_GROUP0_CMP0CMOA> ctrl_group0_cmp0cmoa_field;
        partial void Ctrl_Group0_Cmp0cmoa_Write(CTRL_GROUP0_CMP0CMOA a, CTRL_GROUP0_CMP0CMOA b);
        partial void Ctrl_Group0_Cmp0cmoa_Read(CTRL_GROUP0_CMP0CMOA a, CTRL_GROUP0_CMP0CMOA b);
        partial void Ctrl_Group0_Cmp0cmoa_ValueProvider(CTRL_GROUP0_CMP0CMOA a);
        protected IEnumRegisterField<CTRL_GROUP0_CMP1CMOA> ctrl_group0_cmp1cmoa_field;
        partial void Ctrl_Group0_Cmp1cmoa_Write(CTRL_GROUP0_CMP1CMOA a, CTRL_GROUP0_CMP1CMOA b);
        partial void Ctrl_Group0_Cmp1cmoa_Read(CTRL_GROUP0_CMP1CMOA a, CTRL_GROUP0_CMP1CMOA b);
        partial void Ctrl_Group0_Cmp1cmoa_ValueProvider(CTRL_GROUP0_CMP1CMOA a);
        protected IEnumRegisterField<CTRL_GROUP0_CAP0EDGE> ctrl_group0_cap0edge_field;
        partial void Ctrl_Group0_Cap0edge_Write(CTRL_GROUP0_CAP0EDGE a, CTRL_GROUP0_CAP0EDGE b);
        partial void Ctrl_Group0_Cap0edge_Read(CTRL_GROUP0_CAP0EDGE a, CTRL_GROUP0_CAP0EDGE b);
        partial void Ctrl_Group0_Cap0edge_ValueProvider(CTRL_GROUP0_CAP0EDGE a);

        partial void Ctrl_Group0_Write(uint a, uint b);
        partial void Ctrl_Group0_Read(uint a, uint b);
        
        // Cmp0value_Group0 - Offset : 0x4C
        protected IValueRegisterField cmp0value_group0_cmp0value_field;
        partial void Cmp0value_Group0_Cmp0value_Write(ulong a, ulong b);
        partial void Cmp0value_Group0_Cmp0value_Read(ulong a, ulong b);
        partial void Cmp0value_Group0_Cmp0value_ValueProvider(ulong a);

        partial void Cmp0value_Group0_Write(uint a, uint b);
        partial void Cmp0value_Group0_Read(uint a, uint b);
        
        // Cmp1value_Group0 - Offset : 0x50
        protected IValueRegisterField cmp1value_group0_cmp1value_field;
        partial void Cmp1value_Group0_Cmp1value_Write(ulong a, ulong b);
        partial void Cmp1value_Group0_Cmp1value_Read(ulong a, ulong b);
        partial void Cmp1value_Group0_Cmp1value_ValueProvider(ulong a);

        partial void Cmp1value_Group0_Write(uint a, uint b);
        partial void Cmp1value_Group0_Read(uint a, uint b);
        
        // Cap0value_Group0 - Offset : 0x54
        protected IValueRegisterField cap0value_group0_cap0value_field;
        partial void Cap0value_Group0_Cap0value_Read(ulong a, ulong b);
        partial void Cap0value_Group0_Cap0value_ValueProvider(ulong a);

        partial void Cap0value_Group0_Write(uint a, uint b);
        partial void Cap0value_Group0_Read(uint a, uint b);
        
        // Syncbusy_Group0 - Offset : 0x58
        protected IFlagRegisterField syncbusy_group0_ctrl_bit;
        partial void Syncbusy_Group0_Ctrl_Read(bool a, bool b);
        partial void Syncbusy_Group0_Ctrl_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_group0_cmp0value_bit;
        partial void Syncbusy_Group0_Cmp0value_Read(bool a, bool b);
        partial void Syncbusy_Group0_Cmp0value_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_group0_cmp1value_bit;
        partial void Syncbusy_Group0_Cmp1value_Read(bool a, bool b);
        partial void Syncbusy_Group0_Cmp1value_ValueProvider(bool a);

        partial void Syncbusy_Group0_Write(uint a, uint b);
        partial void Syncbusy_Group0_Read(uint a, uint b);
        
        // If_Group1 - Offset : 0x60
        protected IFlagRegisterField if_group1_ovf_bit;
        partial void If_Group1_Ovf_Write(bool a, bool b);
        partial void If_Group1_Ovf_Read(bool a, bool b);
        partial void If_Group1_Ovf_ValueProvider(bool a);
        protected IFlagRegisterField if_group1_cmp0_bit;
        partial void If_Group1_Cmp0_Write(bool a, bool b);
        partial void If_Group1_Cmp0_Read(bool a, bool b);
        partial void If_Group1_Cmp0_ValueProvider(bool a);
        protected IFlagRegisterField if_group1_cmp1_bit;
        partial void If_Group1_Cmp1_Write(bool a, bool b);
        partial void If_Group1_Cmp1_Read(bool a, bool b);
        partial void If_Group1_Cmp1_ValueProvider(bool a);
        protected IFlagRegisterField if_group1_cap0_bit;
        partial void If_Group1_Cap0_Write(bool a, bool b);
        partial void If_Group1_Cap0_Read(bool a, bool b);
        partial void If_Group1_Cap0_ValueProvider(bool a);
        protected IFlagRegisterField if_group1_altovf_bit;
        partial void If_Group1_Altovf_Write(bool a, bool b);
        partial void If_Group1_Altovf_Read(bool a, bool b);
        partial void If_Group1_Altovf_ValueProvider(bool a);
        protected IFlagRegisterField if_group1_altcmp0_bit;
        partial void If_Group1_Altcmp0_Write(bool a, bool b);
        partial void If_Group1_Altcmp0_Read(bool a, bool b);
        partial void If_Group1_Altcmp0_ValueProvider(bool a);
        protected IFlagRegisterField if_group1_altcmp1_bit;
        partial void If_Group1_Altcmp1_Write(bool a, bool b);
        partial void If_Group1_Altcmp1_Read(bool a, bool b);
        partial void If_Group1_Altcmp1_ValueProvider(bool a);
        protected IFlagRegisterField if_group1_altcap0_bit;
        partial void If_Group1_Altcap0_Write(bool a, bool b);
        partial void If_Group1_Altcap0_Read(bool a, bool b);
        partial void If_Group1_Altcap0_ValueProvider(bool a);

        partial void If_Group1_Write(uint a, uint b);
        partial void If_Group1_Read(uint a, uint b);
        
        // Ien_Group1 - Offset : 0x64
        protected IFlagRegisterField ien_group1_ovf_bit;
        partial void Ien_Group1_Ovf_Write(bool a, bool b);
        partial void Ien_Group1_Ovf_Read(bool a, bool b);
        partial void Ien_Group1_Ovf_ValueProvider(bool a);
        protected IFlagRegisterField ien_group1_cmp0_bit;
        partial void Ien_Group1_Cmp0_Write(bool a, bool b);
        partial void Ien_Group1_Cmp0_Read(bool a, bool b);
        partial void Ien_Group1_Cmp0_ValueProvider(bool a);
        protected IFlagRegisterField ien_group1_cmp1_bit;
        partial void Ien_Group1_Cmp1_Write(bool a, bool b);
        partial void Ien_Group1_Cmp1_Read(bool a, bool b);
        partial void Ien_Group1_Cmp1_ValueProvider(bool a);
        protected IFlagRegisterField ien_group1_cap0_bit;
        partial void Ien_Group1_Cap0_Write(bool a, bool b);
        partial void Ien_Group1_Cap0_Read(bool a, bool b);
        partial void Ien_Group1_Cap0_ValueProvider(bool a);
        protected IFlagRegisterField ien_group1_altovf_bit;
        partial void Ien_Group1_Altovf_Write(bool a, bool b);
        partial void Ien_Group1_Altovf_Read(bool a, bool b);
        partial void Ien_Group1_Altovf_ValueProvider(bool a);
        protected IFlagRegisterField ien_group1_altcmp0_bit;
        partial void Ien_Group1_Altcmp0_Write(bool a, bool b);
        partial void Ien_Group1_Altcmp0_Read(bool a, bool b);
        partial void Ien_Group1_Altcmp0_ValueProvider(bool a);
        protected IFlagRegisterField ien_group1_altcmp1_bit;
        partial void Ien_Group1_Altcmp1_Write(bool a, bool b);
        partial void Ien_Group1_Altcmp1_Read(bool a, bool b);
        partial void Ien_Group1_Altcmp1_ValueProvider(bool a);
        protected IFlagRegisterField ien_group1_altcap0_bit;
        partial void Ien_Group1_Altcap0_Write(bool a, bool b);
        partial void Ien_Group1_Altcap0_Read(bool a, bool b);
        partial void Ien_Group1_Altcap0_ValueProvider(bool a);

        partial void Ien_Group1_Write(uint a, uint b);
        partial void Ien_Group1_Read(uint a, uint b);
        
        // Ctrl_Group1 - Offset : 0x68
        protected IFlagRegisterField ctrl_group1_cmp0en_bit;
        partial void Ctrl_Group1_Cmp0en_Write(bool a, bool b);
        partial void Ctrl_Group1_Cmp0en_Read(bool a, bool b);
        partial void Ctrl_Group1_Cmp0en_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_group1_cmp1en_bit;
        partial void Ctrl_Group1_Cmp1en_Write(bool a, bool b);
        partial void Ctrl_Group1_Cmp1en_Read(bool a, bool b);
        partial void Ctrl_Group1_Cmp1en_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_group1_cap0en_bit;
        partial void Ctrl_Group1_Cap0en_Write(bool a, bool b);
        partial void Ctrl_Group1_Cap0en_Read(bool a, bool b);
        partial void Ctrl_Group1_Cap0en_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_GROUP1_CMP0CMOA> ctrl_group1_cmp0cmoa_field;
        partial void Ctrl_Group1_Cmp0cmoa_Write(CTRL_GROUP1_CMP0CMOA a, CTRL_GROUP1_CMP0CMOA b);
        partial void Ctrl_Group1_Cmp0cmoa_Read(CTRL_GROUP1_CMP0CMOA a, CTRL_GROUP1_CMP0CMOA b);
        partial void Ctrl_Group1_Cmp0cmoa_ValueProvider(CTRL_GROUP1_CMP0CMOA a);
        protected IEnumRegisterField<CTRL_GROUP1_CMP1CMOA> ctrl_group1_cmp1cmoa_field;
        partial void Ctrl_Group1_Cmp1cmoa_Write(CTRL_GROUP1_CMP1CMOA a, CTRL_GROUP1_CMP1CMOA b);
        partial void Ctrl_Group1_Cmp1cmoa_Read(CTRL_GROUP1_CMP1CMOA a, CTRL_GROUP1_CMP1CMOA b);
        partial void Ctrl_Group1_Cmp1cmoa_ValueProvider(CTRL_GROUP1_CMP1CMOA a);
        protected IEnumRegisterField<CTRL_GROUP1_CAP0EDGE> ctrl_group1_cap0edge_field;
        partial void Ctrl_Group1_Cap0edge_Write(CTRL_GROUP1_CAP0EDGE a, CTRL_GROUP1_CAP0EDGE b);
        partial void Ctrl_Group1_Cap0edge_Read(CTRL_GROUP1_CAP0EDGE a, CTRL_GROUP1_CAP0EDGE b);
        partial void Ctrl_Group1_Cap0edge_ValueProvider(CTRL_GROUP1_CAP0EDGE a);

        partial void Ctrl_Group1_Write(uint a, uint b);
        partial void Ctrl_Group1_Read(uint a, uint b);
        
        // Cmp0value_Group1 - Offset : 0x6C
        protected IValueRegisterField cmp0value_group1_cmp0value_field;
        partial void Cmp0value_Group1_Cmp0value_Write(ulong a, ulong b);
        partial void Cmp0value_Group1_Cmp0value_Read(ulong a, ulong b);
        partial void Cmp0value_Group1_Cmp0value_ValueProvider(ulong a);

        partial void Cmp0value_Group1_Write(uint a, uint b);
        partial void Cmp0value_Group1_Read(uint a, uint b);
        
        // Cmp1value_Group1 - Offset : 0x70
        protected IValueRegisterField cmp1value_group1_cmp1value_field;
        partial void Cmp1value_Group1_Cmp1value_Write(ulong a, ulong b);
        partial void Cmp1value_Group1_Cmp1value_Read(ulong a, ulong b);
        partial void Cmp1value_Group1_Cmp1value_ValueProvider(ulong a);

        partial void Cmp1value_Group1_Write(uint a, uint b);
        partial void Cmp1value_Group1_Read(uint a, uint b);
        
        // Cap0value_Group1 - Offset : 0x74
        protected IValueRegisterField cap0value_group1_cap0value_field;
        partial void Cap0value_Group1_Cap0value_Read(ulong a, ulong b);
        partial void Cap0value_Group1_Cap0value_ValueProvider(ulong a);

        partial void Cap0value_Group1_Write(uint a, uint b);
        partial void Cap0value_Group1_Read(uint a, uint b);
        
        // Syncbusy_Group1 - Offset : 0x78
        protected IFlagRegisterField syncbusy_group1_ctrl_bit;
        partial void Syncbusy_Group1_Ctrl_Read(bool a, bool b);
        partial void Syncbusy_Group1_Ctrl_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_group1_cmp0value_bit;
        partial void Syncbusy_Group1_Cmp0value_Read(bool a, bool b);
        partial void Syncbusy_Group1_Cmp0value_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_group1_cmp1value_bit;
        partial void Syncbusy_Group1_Cmp1value_Read(bool a, bool b);
        partial void Syncbusy_Group1_Cmp1value_ValueProvider(bool a);

        partial void Syncbusy_Group1_Write(uint a, uint b);
        partial void Syncbusy_Group1_Read(uint a, uint b);
        
        // If_Group2 - Offset : 0x80
        protected IFlagRegisterField if_group2_ovf_bit;
        partial void If_Group2_Ovf_Write(bool a, bool b);
        partial void If_Group2_Ovf_Read(bool a, bool b);
        partial void If_Group2_Ovf_ValueProvider(bool a);
        protected IFlagRegisterField if_group2_cmp0_bit;
        partial void If_Group2_Cmp0_Write(bool a, bool b);
        partial void If_Group2_Cmp0_Read(bool a, bool b);
        partial void If_Group2_Cmp0_ValueProvider(bool a);
        protected IFlagRegisterField if_group2_faildet_bit;
        partial void If_Group2_Faildet_Write(bool a, bool b);
        partial void If_Group2_Faildet_Read(bool a, bool b);
        partial void If_Group2_Faildet_ValueProvider(bool a);
        protected IFlagRegisterField if_group2_tamper_bit;
        partial void If_Group2_Tamper_Write(bool a, bool b);
        partial void If_Group2_Tamper_Read(bool a, bool b);
        partial void If_Group2_Tamper_ValueProvider(bool a);

        partial void If_Group2_Write(uint a, uint b);
        partial void If_Group2_Read(uint a, uint b);
        
        // Ien_Group2 - Offset : 0x84
        protected IFlagRegisterField ien_group2_ovf_bit;
        partial void Ien_Group2_Ovf_Write(bool a, bool b);
        partial void Ien_Group2_Ovf_Read(bool a, bool b);
        partial void Ien_Group2_Ovf_ValueProvider(bool a);
        protected IFlagRegisterField ien_group2_cmp0_bit;
        partial void Ien_Group2_Cmp0_Write(bool a, bool b);
        partial void Ien_Group2_Cmp0_Read(bool a, bool b);
        partial void Ien_Group2_Cmp0_ValueProvider(bool a);
        protected IFlagRegisterField ien_group2_faildet_bit;
        partial void Ien_Group2_Faildet_Write(bool a, bool b);
        partial void Ien_Group2_Faildet_Read(bool a, bool b);
        partial void Ien_Group2_Faildet_ValueProvider(bool a);
        protected IFlagRegisterField ien_group2_tamper_bit;
        partial void Ien_Group2_Tamper_Write(bool a, bool b);
        partial void Ien_Group2_Tamper_Read(bool a, bool b);
        partial void Ien_Group2_Tamper_ValueProvider(bool a);

        partial void Ien_Group2_Write(uint a, uint b);
        partial void Ien_Group2_Read(uint a, uint b);
        
        // Ctrl_Group2 - Offset : 0x88
        protected IFlagRegisterField ctrl_group2_cmp0en_bit;
        partial void Ctrl_Group2_Cmp0en_Write(bool a, bool b);
        partial void Ctrl_Group2_Cmp0en_Read(bool a, bool b);
        partial void Ctrl_Group2_Cmp0en_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_GROUP2_CMP0CMOA> ctrl_group2_cmp0cmoa_field;
        partial void Ctrl_Group2_Cmp0cmoa_Write(CTRL_GROUP2_CMP0CMOA a, CTRL_GROUP2_CMP0CMOA b);
        partial void Ctrl_Group2_Cmp0cmoa_Read(CTRL_GROUP2_CMP0CMOA a, CTRL_GROUP2_CMP0CMOA b);
        partial void Ctrl_Group2_Cmp0cmoa_ValueProvider(CTRL_GROUP2_CMP0CMOA a);

        partial void Ctrl_Group2_Write(uint a, uint b);
        partial void Ctrl_Group2_Read(uint a, uint b);
        
        // Cmp0value_Group2 - Offset : 0x8C
        protected IValueRegisterField cmp0value_group2_cmp0value_field;
        partial void Cmp0value_Group2_Cmp0value_Write(ulong a, ulong b);
        partial void Cmp0value_Group2_Cmp0value_Read(ulong a, ulong b);
        partial void Cmp0value_Group2_Cmp0value_ValueProvider(ulong a);

        partial void Cmp0value_Group2_Write(uint a, uint b);
        partial void Cmp0value_Group2_Read(uint a, uint b);
        
        // Syncbusy_Group2 - Offset : 0x98
        protected IFlagRegisterField syncbusy_group2_ctrl_bit;
        partial void Syncbusy_Group2_Ctrl_Read(bool a, bool b);
        partial void Syncbusy_Group2_Ctrl_ValueProvider(bool a);
        protected IFlagRegisterField syncbusy_group2_cmp0value_bit;
        partial void Syncbusy_Group2_Cmp0value_Read(bool a, bool b);
        partial void Syncbusy_Group2_Cmp0value_ValueProvider(bool a);

        partial void Syncbusy_Group2_Write(uint a, uint b);
        partial void Syncbusy_Group2_Read(uint a, uint b);
        
        partial void SYSRTC_Reset();

        partial void Sysrtc_1_Constructor();

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

        private ICmu _cmu;
        private ICmu cmu
        {
            get
            {
                if (Object.ReferenceEquals(_cmu, null))
                {
                    foreach(var cmu in machine.GetPeripheralsOfType<ICmu>())
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
            switch(offset & 0xF000){
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
            switch(address & 0xF000){
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
                    this.Log(LogLevel.Error, "writing doubleWord to non existing offset {0:X}, case : {1:X}", address, address & 0xF000);
                    break;
            }           
        }

        protected enum Registers
        {
            Ipversion = 0x0,
            En = 0x4,
            Swrst = 0x8,
            Cfg = 0xC,
            Cmd = 0x10,
            Status = 0x14,
            Cnt = 0x18,
            Syncbusy = 0x1C,
            Lock = 0x20,
            Faildetctrl = 0x30,
            Faildetlock = 0x34,
            If_Group0 = 0x40,
            Ien_Group0 = 0x44,
            Ctrl_Group0 = 0x48,
            Cmp0value_Group0 = 0x4C,
            Cmp1value_Group0 = 0x50,
            Cap0value_Group0 = 0x54,
            Syncbusy_Group0 = 0x58,
            If_Group1 = 0x60,
            Ien_Group1 = 0x64,
            Ctrl_Group1 = 0x68,
            Cmp0value_Group1 = 0x6C,
            Cmp1value_Group1 = 0x70,
            Cap0value_Group1 = 0x74,
            Syncbusy_Group1 = 0x78,
            If_Group2 = 0x80,
            Ien_Group2 = 0x84,
            Ctrl_Group2 = 0x88,
            Cmp0value_Group2 = 0x8C,
            Syncbusy_Group2 = 0x98,
            
            Ipversion_SET = 0x1000,
            En_SET = 0x1004,
            Swrst_SET = 0x1008,
            Cfg_SET = 0x100C,
            Cmd_SET = 0x1010,
            Status_SET = 0x1014,
            Cnt_SET = 0x1018,
            Syncbusy_SET = 0x101C,
            Lock_SET = 0x1020,
            Faildetctrl_SET = 0x1030,
            Faildetlock_SET = 0x1034,
            If_Group0_SET = 0x1040,
            Ien_Group0_SET = 0x1044,
            Ctrl_Group0_SET = 0x1048,
            Cmp0value_Group0_SET = 0x104C,
            Cmp1value_Group0_SET = 0x1050,
            Cap0value_Group0_SET = 0x1054,
            Syncbusy_Group0_SET = 0x1058,
            If_Group1_SET = 0x1060,
            Ien_Group1_SET = 0x1064,
            Ctrl_Group1_SET = 0x1068,
            Cmp0value_Group1_SET = 0x106C,
            Cmp1value_Group1_SET = 0x1070,
            Cap0value_Group1_SET = 0x1074,
            Syncbusy_Group1_SET = 0x1078,
            If_Group2_SET = 0x1080,
            Ien_Group2_SET = 0x1084,
            Ctrl_Group2_SET = 0x1088,
            Cmp0value_Group2_SET = 0x108C,
            Syncbusy_Group2_SET = 0x1098,
            
            Ipversion_CLR = 0x2000,
            En_CLR = 0x2004,
            Swrst_CLR = 0x2008,
            Cfg_CLR = 0x200C,
            Cmd_CLR = 0x2010,
            Status_CLR = 0x2014,
            Cnt_CLR = 0x2018,
            Syncbusy_CLR = 0x201C,
            Lock_CLR = 0x2020,
            Faildetctrl_CLR = 0x2030,
            Faildetlock_CLR = 0x2034,
            If_Group0_CLR = 0x2040,
            Ien_Group0_CLR = 0x2044,
            Ctrl_Group0_CLR = 0x2048,
            Cmp0value_Group0_CLR = 0x204C,
            Cmp1value_Group0_CLR = 0x2050,
            Cap0value_Group0_CLR = 0x2054,
            Syncbusy_Group0_CLR = 0x2058,
            If_Group1_CLR = 0x2060,
            Ien_Group1_CLR = 0x2064,
            Ctrl_Group1_CLR = 0x2068,
            Cmp0value_Group1_CLR = 0x206C,
            Cmp1value_Group1_CLR = 0x2070,
            Cap0value_Group1_CLR = 0x2074,
            Syncbusy_Group1_CLR = 0x2078,
            If_Group2_CLR = 0x2080,
            Ien_Group2_CLR = 0x2084,
            Ctrl_Group2_CLR = 0x2088,
            Cmp0value_Group2_CLR = 0x208C,
            Syncbusy_Group2_CLR = 0x2098,
            
            Ipversion_TGL = 0x3000,
            En_TGL = 0x3004,
            Swrst_TGL = 0x3008,
            Cfg_TGL = 0x300C,
            Cmd_TGL = 0x3010,
            Status_TGL = 0x3014,
            Cnt_TGL = 0x3018,
            Syncbusy_TGL = 0x301C,
            Lock_TGL = 0x3020,
            Faildetctrl_TGL = 0x3030,
            Faildetlock_TGL = 0x3034,
            If_Group0_TGL = 0x3040,
            Ien_Group0_TGL = 0x3044,
            Ctrl_Group0_TGL = 0x3048,
            Cmp0value_Group0_TGL = 0x304C,
            Cmp1value_Group0_TGL = 0x3050,
            Cap0value_Group0_TGL = 0x3054,
            Syncbusy_Group0_TGL = 0x3058,
            If_Group1_TGL = 0x3060,
            Ien_Group1_TGL = 0x3064,
            Ctrl_Group1_TGL = 0x3068,
            Cmp0value_Group1_TGL = 0x306C,
            Cmp1value_Group1_TGL = 0x3070,
            Cap0value_Group1_TGL = 0x3074,
            Syncbusy_Group1_TGL = 0x3078,
            If_Group2_TGL = 0x3080,
            Ien_Group2_TGL = 0x3084,
            Ctrl_Group2_TGL = 0x3088,
            Cmp0value_Group2_TGL = 0x308C,
            Syncbusy_Group2_TGL = 0x3098,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}
