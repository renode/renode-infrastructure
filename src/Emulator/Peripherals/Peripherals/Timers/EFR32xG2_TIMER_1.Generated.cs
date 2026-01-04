//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    TIMER, Generated on : 2024-07-31 13:49:55.938358
    TIMER, ID Version : 60defe53b8f54da2ae4c36177458d623.1 */

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

namespace Antmicro.Renode.Peripherals.Timers
{
    public partial class EFR32xG2_TIMER_1
    {
        public EFR32xG2_TIMER_1(Machine machine) : base(machine)
        {
            EFR32xG2_TIMER_1_constructor();
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
using Antmicro.Renode.Peripherals.Miscellaneous.SiLabs;

namespace Antmicro.Renode.Peripherals.Timers
{
    public partial class EFR32xG2_TIMER_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_TIMER_1(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_TIMER_1_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Top, GenerateTopRegister()},
                {(long)Registers.Topb, GenerateTopbRegister()},
                {(long)Registers.Cnt, GenerateCntRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.En, GenerateEnRegister()},
                {(long)Registers.Cc0_Cfg, GenerateCc0_cfgRegister()},
                {(long)Registers.Cc0_Ctrl, GenerateCc0_ctrlRegister()},
                {(long)Registers.Cc0_Oc, GenerateCc0_ocRegister()},
                {(long)Registers.Cc0_Ocb, GenerateCc0_ocbRegister()},
                {(long)Registers.Cc0_Icf, GenerateCc0_icfRegister()},
                {(long)Registers.Cc0_Icof, GenerateCc0_icofRegister()},
                {(long)Registers.Cc1_Cfg, GenerateCc1_cfgRegister()},
                {(long)Registers.Cc1_Ctrl, GenerateCc1_ctrlRegister()},
                {(long)Registers.Cc1_Oc, GenerateCc1_ocRegister()},
                {(long)Registers.Cc1_Ocb, GenerateCc1_ocbRegister()},
                {(long)Registers.Cc1_Icf, GenerateCc1_icfRegister()},
                {(long)Registers.Cc1_Icof, GenerateCc1_icofRegister()},
                {(long)Registers.Cc2_Cfg, GenerateCc2_cfgRegister()},
                {(long)Registers.Cc2_Ctrl, GenerateCc2_ctrlRegister()},
                {(long)Registers.Cc2_Oc, GenerateCc2_ocRegister()},
                {(long)Registers.Cc2_Ocb, GenerateCc2_ocbRegister()},
                {(long)Registers.Cc2_Icf, GenerateCc2_icfRegister()},
                {(long)Registers.Cc2_Icof, GenerateCc2_icofRegister()},
                {(long)Registers.DtCfg, GenerateDtcfgRegister()},
                {(long)Registers.DtTimecfg, GenerateDttimecfgRegister()},
                {(long)Registers.DtFcfg, GenerateDtfcfgRegister()},
                {(long)Registers.DtCtrl, GenerateDtctrlRegister()},
                {(long)Registers.DtOgen, GenerateDtogenRegister()},
                {(long)Registers.DtFault, GenerateDtfaultRegister()},
                {(long)Registers.DtFaultc, GenerateDtfaultcRegister()},
                {(long)Registers.DtLock, GenerateDtlockRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            TIMER_Reset();
        }
        
        protected enum CFG_MODE
        {
            UP = 0, // Up-count mode
            DOWN = 1, // Down-count mode
            UPDOWN = 2, // Up/down-count mode
            QDEC = 3, // Quadrature decoder mode
        }
        
        protected enum CFG_SYNC
        {
            DISABLE = 0, // Timer operation is unaffected by other timers.
            ENABLE = 1, // Timer may be started, stopped and re-loaded from other timer instances.
        }
        
        protected enum CFG_QDM
        {
            X2 = 0, // X2 mode selected
            X4 = 1, // X4 mode selected
        }
        
        protected enum CFG_DEBUGRUN
        {
            HALT = 0, // Timer is halted in debug mode
            RUN = 1, // Timer is running in debug mode
        }
        
        protected enum CFG_CLKSEL
        {
            PRESCEM01GRPACLK = 0, // Prescaled EM01GRPACLK
            CC1 = 1, // Compare/Capture Channel 1 Input
            TIMEROUF = 2, // Timer is clocked by underflow(down-count) or overflow(up-count) in the lower numbered neighbor Timer
        }
        
        protected enum CFG_RETIMEEN
        {
            DISABLE = 0, // PWM outputs are not re-timed.
            ENABLE = 1, // PWM outputs are re-timed.
        }
        
        protected enum CFG_DISSYNCOUT
        {
            EN = 0, // Timer can start/stop/reload other timers with SYNC bit set
            DIS = 1, // Timer cannot start/stop/reload other timers with SYNC bit set
        }
        
        protected enum CFG_PRESC
        {
            DIV1 = 0, // No prescaling
            DIV2 = 1, // Prescale by 2
            DIV4 = 3, // Prescale by 4
            DIV8 = 7, // Prescale by 8
            DIV16 = 15, // Prescale by 16
            DIV32 = 31, // Prescale by 32
            DIV64 = 63, // Prescale by 64
            DIV128 = 127, // Prescale by 128
            DIV256 = 255, // Prescale by 256
            DIV512 = 511, // Prescale by 512
            DIV1024 = 1023, // Prescale by 1024
        }
        
        protected enum CTRL_RISEA
        {
            NONE = 0, // No action
            START = 1, // Start counter without reload
            STOP = 2, // Stop counter without reload
            RELOADSTART = 3, // Reload and start counter
        }
        
        protected enum CTRL_FALLA
        {
            NONE = 0, // No action
            START = 1, // Start counter without reload
            STOP = 2, // Stop counter without reload
            RELOADSTART = 3, // Reload and start counter
        }
        
        protected enum STATUS_DIR
        {
            UP = 0, // Counting up
            DOWN = 1, // Counting down
        }
        
        protected enum STATUS_TIMERLOCKSTATUS
        {
            UNLOCKED = 0, // TIMER registers are unlocked
            LOCKED = 1, // TIMER registers are locked
        }
        
        protected enum STATUS_DTILOCKSTATUS
        {
            UNLOCKED = 0, // DTI registers are unlocked
            LOCKED = 1, // DTI registers are locked
        }
        
        protected enum STATUS_CCPOL0
        {
            LOWRISE = 0, // CCx polarity low level/rising edge
            HIGHFALL = 1, // CCx polarity high level/falling edge
        }
        
        protected enum STATUS_CCPOL1
        {
            LOWRISE = 0, // CCx polarity low level/rising edge
            HIGHFALL = 1, // CCx polarity high level/falling edge
        }
        
        protected enum STATUS_CCPOL2
        {
            LOWRISE = 0, // CCx polarity low level/rising edge
            HIGHFALL = 1, // CCx polarity high level/falling edge
        }
        
        protected enum CC_CFG_MODE
        {
            OFF = 0, // Compare/Capture channel turned off
            INPUTCAPTURE = 1, // Input Capture
            OUTPUTCOMPARE = 2, // Output Compare
            PWM = 3, // Pulse-Width Modulation
        }
        
        protected enum CC_CFG_INSEL
        {
            PIN = 0, // TIMERnCCx pin is selected
            PRSSYNC = 1, // Synchornous PRS selected
            PRSASYNCLEVEL = 2, // Asynchronous Level PRS selected
            PRSASYNCPULSE = 3, // Asynchronous Pulse PRS selected
        }
        
        protected enum CC_CFG_PRSCONF
        {
            PULSE = 0, // Each CC event will generate a one EM01GRPACLK cycle high pulse
            LEVEL = 1, // The PRS channel will follow CC out
        }
        
        protected enum CC_CFG_FILT
        {
            DISABLE = 0, // Digital Filter Disabled
            ENABLE = 1, // Digital Filter Enabled 
        }
        
        protected enum CC_CTRL_CMOA
        {
            NONE = 0, // No action on compare match
            TOGGLE = 1, // Toggle output on compare match
            CLEAR = 2, // Clear output on compare match
            SET = 3, // Set output on compare match
        }
        
        protected enum CC_CTRL_COFOA
        {
            NONE = 0, // No action on counter overflow
            TOGGLE = 1, // Toggle output on counter overflow
            CLEAR = 2, // Clear output on counter overflow
            SET = 3, // Set output on counter overflow
        }
        
        protected enum CC_CTRL_CUFOA
        {
            NONE = 0, // No action on counter underflow
            TOGGLE = 1, // Toggle output on counter underflow
            CLEAR = 2, // Clear output on counter underflow
            SET = 3, // Set output on counter underflow
        }
        
        protected enum CC_CTRL_ICEDGE
        {
            RISING = 0, // Rising edges detected
            FALLING = 1, // Falling edges detected
            BOTH = 2, // Both edges detected
            NONE = 3, // No edge detection, signal is left as it is
        }
        
        protected enum CC_CTRL_ICEVCTRL
        {
            EVERYEDGE = 0, // PRS output pulse and interrupt flag set on every capture
            EVERYSECONDEDGE = 1, // PRS output pulse and interrupt flag set on every second capture
            RISING = 2, // PRS output pulse and interrupt flag set on rising edge only (if ICEDGE = BOTH)
            FALLING = 3, // PRS output pulse and interrupt flag set on falling edge only (if ICEDGE = BOTH)
        }
        
        protected enum DTCFG_DTDAS
        {
            NORESTART = 0, // No DTI restart on debugger exit
            RESTART = 1, // DTI restart on debugger exit
        }
        
        protected enum DTFCFG_DTFA
        {
            NONE = 0, // No action on fault
            INACTIVE = 1, // Set outputs inactive
            CLEAR = 2, // Clear outputs
            TRISTATE = 3, // Tristate outputs
        }
        
        protected enum DTLOCK_DTILOCKKEY
        {
            UNLOCK = 52864, // Write to unlock TIMER DTI registers
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
        
        // Cfg - Offset : 0x4
        protected DoubleWordRegister  GenerateCfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CFG_MODE>(0, 2, out cfg_mode_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Mode_ValueProvider(_);
                        return cfg_mode_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Mode_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Mode_Read(_, __),
                    name: "Mode")
            .WithReservedBits(2, 1)
            .WithEnumField<DoubleWordRegister, CFG_SYNC>(3, 1, out cfg_sync_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Sync_ValueProvider(_);
                        return cfg_sync_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Sync_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Sync_Read(_, __),
                    name: "Sync")
            .WithFlag(4, out cfg_osmen_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Osmen_ValueProvider(_);
                        return cfg_osmen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Osmen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Osmen_Read(_, __),
                    name: "Osmen")
            .WithEnumField<DoubleWordRegister, CFG_QDM>(5, 1, out cfg_qdm_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Qdm_ValueProvider(_);
                        return cfg_qdm_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Qdm_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Qdm_Read(_, __),
                    name: "Qdm")
            .WithEnumField<DoubleWordRegister, CFG_DEBUGRUN>(6, 1, out cfg_debugrun_bit, 
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
            .WithFlag(7, out cfg_dmaclract_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Dmaclract_ValueProvider(_);
                        return cfg_dmaclract_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Dmaclract_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Dmaclract_Read(_, __),
                    name: "Dmaclract")
            .WithEnumField<DoubleWordRegister, CFG_CLKSEL>(8, 2, out cfg_clksel_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Clksel_ValueProvider(_);
                        return cfg_clksel_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Clksel_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Clksel_Read(_, __),
                    name: "Clksel")
            .WithEnumField<DoubleWordRegister, CFG_RETIMEEN>(10, 1, out cfg_retimeen_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Retimeen_ValueProvider(_);
                        return cfg_retimeen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Retimeen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Retimeen_Read(_, __),
                    name: "Retimeen")
            .WithEnumField<DoubleWordRegister, CFG_DISSYNCOUT>(11, 1, out cfg_dissyncout_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Dissyncout_ValueProvider(_);
                        return cfg_dissyncout_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Dissyncout_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Dissyncout_Read(_, __),
                    name: "Dissyncout")
            .WithFlag(12, out cfg_retimesel_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Retimesel_ValueProvider(_);
                        return cfg_retimesel_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Retimesel_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Retimesel_Read(_, __),
                    name: "Retimesel")
            .WithReservedBits(13, 3)
            .WithFlag(16, out cfg_ati_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Ati_ValueProvider(_);
                        return cfg_ati_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Ati_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Ati_Read(_, __),
                    name: "Ati")
            .WithFlag(17, out cfg_rsscoist_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Rsscoist_ValueProvider(_);
                        return cfg_rsscoist_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Rsscoist_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Rsscoist_Read(_, __),
                    name: "Rsscoist")
            .WithEnumField<DoubleWordRegister, CFG_PRESC>(18, 10, out cfg_presc_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Presc_ValueProvider(_);
                        return cfg_presc_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Presc_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cfg_Presc_Read(_, __),
                    name: "Presc")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Cfg_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Write_WithHook(_, __));
        
        // Ctrl - Offset : 0x8
        protected DoubleWordRegister  GenerateCtrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CTRL_RISEA>(0, 2, out ctrl_risea_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Risea_ValueProvider(_);
                        return ctrl_risea_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Risea_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Ctrl_Risea_Read(_, __),
                    name: "Risea")
            .WithEnumField<DoubleWordRegister, CTRL_FALLA>(2, 2, out ctrl_falla_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Falla_ValueProvider(_);
                        return ctrl_falla_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Falla_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Ctrl_Falla_Read(_, __),
                    name: "Falla")
            .WithFlag(4, out ctrl_x2cnt_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_X2cnt_ValueProvider(_);
                        return ctrl_x2cnt_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_X2cnt_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Ctrl_X2cnt_Read(_, __),
                    name: "X2cnt")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write_WithHook(_, __));
        
        // Cmd - Offset : 0xC
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cmd_start_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cmd_Start_Write(_, __);
                    },
                    name: "Start")
            .WithFlag(1, out cmd_stop_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cmd_Stop_Write(_, __);
                    },
                    name: "Stop")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write_WithHook(_, __));
        
        // Status - Offset : 0x10
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_running_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Running_ValueProvider(_);
                        return status_running_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Running_Read(_, __),
                    name: "Running")
            .WithEnumField<DoubleWordRegister, STATUS_DIR>(1, 1, out status_dir_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Dir_ValueProvider(_);
                        return status_dir_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Dir_Read(_, __),
                    name: "Dir")
            .WithFlag(2, out status_topbv_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Topbv_ValueProvider(_);
                        return status_topbv_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Topbv_Read(_, __),
                    name: "Topbv")
            .WithReservedBits(3, 1)
            .WithEnumField<DoubleWordRegister, STATUS_TIMERLOCKSTATUS>(4, 1, out status_timerlockstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Timerlockstatus_ValueProvider(_);
                        return status_timerlockstatus_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Timerlockstatus_Read(_, __),
                    name: "Timerlockstatus")
            .WithEnumField<DoubleWordRegister, STATUS_DTILOCKSTATUS>(5, 1, out status_dtilockstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Dtilockstatus_ValueProvider(_);
                        return status_dtilockstatus_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Dtilockstatus_Read(_, __),
                    name: "Dtilockstatus")
            .WithFlag(6, out status_syncbusy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Syncbusy_ValueProvider(_);
                        return status_syncbusy_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Syncbusy_Read(_, __),
                    name: "Syncbusy")
            .WithReservedBits(7, 1)
            .WithFlag(8, out status_ocbv0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ocbv0_ValueProvider(_);
                        return status_ocbv0_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ocbv0_Read(_, __),
                    name: "Ocbv0")
            .WithFlag(9, out status_ocbv1_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ocbv1_ValueProvider(_);
                        return status_ocbv1_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ocbv1_Read(_, __),
                    name: "Ocbv1")
            .WithFlag(10, out status_ocbv2_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ocbv2_ValueProvider(_);
                        return status_ocbv2_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ocbv2_Read(_, __),
                    name: "Ocbv2")
            .WithReservedBits(11, 5)
            .WithFlag(16, out status_icfempty0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Icfempty0_ValueProvider(_);
                        return status_icfempty0_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Icfempty0_Read(_, __),
                    name: "Icfempty0")
            .WithFlag(17, out status_icfempty1_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Icfempty1_ValueProvider(_);
                        return status_icfempty1_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Icfempty1_Read(_, __),
                    name: "Icfempty1")
            .WithFlag(18, out status_icfempty2_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Icfempty2_ValueProvider(_);
                        return status_icfempty2_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Icfempty2_Read(_, __),
                    name: "Icfempty2")
            .WithReservedBits(19, 5)
            .WithEnumField<DoubleWordRegister, STATUS_CCPOL0>(24, 1, out status_ccpol0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ccpol0_ValueProvider(_);
                        return status_ccpol0_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ccpol0_Read(_, __),
                    name: "Ccpol0")
            .WithEnumField<DoubleWordRegister, STATUS_CCPOL1>(25, 1, out status_ccpol1_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ccpol1_ValueProvider(_);
                        return status_ccpol1_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ccpol1_Read(_, __),
                    name: "Ccpol1")
            .WithEnumField<DoubleWordRegister, STATUS_CCPOL2>(26, 1, out status_ccpol2_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ccpol2_ValueProvider(_);
                        return status_ccpol2_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Ccpol2_Read(_, __),
                    name: "Ccpol2")
            .WithReservedBits(27, 5)
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // If - Offset : 0x14
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_of_bit, 
                    valueProviderCallback: (_) => {
                        If_Of_ValueProvider(_);
                        return if_of_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Of_Write(_, __),
                    
                    readCallback: (_, __) => If_Of_Read(_, __),
                    name: "Of")
            .WithFlag(1, out if_uf_bit, 
                    valueProviderCallback: (_) => {
                        If_Uf_ValueProvider(_);
                        return if_uf_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Uf_Write(_, __),
                    
                    readCallback: (_, __) => If_Uf_Read(_, __),
                    name: "Uf")
            .WithFlag(2, out if_dirchg_bit, 
                    valueProviderCallback: (_) => {
                        If_Dirchg_ValueProvider(_);
                        return if_dirchg_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Dirchg_Write(_, __),
                    
                    readCallback: (_, __) => If_Dirchg_Read(_, __),
                    name: "Dirchg")
            .WithReservedBits(3, 1)
            .WithFlag(4, out if_cc0_bit, 
                    valueProviderCallback: (_) => {
                        If_Cc0_ValueProvider(_);
                        return if_cc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Cc0_Write(_, __),
                    
                    readCallback: (_, __) => If_Cc0_Read(_, __),
                    name: "Cc0")
            .WithFlag(5, out if_cc1_bit, 
                    valueProviderCallback: (_) => {
                        If_Cc1_ValueProvider(_);
                        return if_cc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Cc1_Write(_, __),
                    
                    readCallback: (_, __) => If_Cc1_Read(_, __),
                    name: "Cc1")
            .WithFlag(6, out if_cc2_bit, 
                    valueProviderCallback: (_) => {
                        If_Cc2_ValueProvider(_);
                        return if_cc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Cc2_Write(_, __),
                    
                    readCallback: (_, __) => If_Cc2_Read(_, __),
                    name: "Cc2")
            .WithReservedBits(7, 9)
            .WithFlag(16, out if_icfwlfull0_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfwlfull0_ValueProvider(_);
                        return if_icfwlfull0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfwlfull0_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfwlfull0_Read(_, __),
                    name: "Icfwlfull0")
            .WithFlag(17, out if_icfwlfull1_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfwlfull1_ValueProvider(_);
                        return if_icfwlfull1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfwlfull1_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfwlfull1_Read(_, __),
                    name: "Icfwlfull1")
            .WithFlag(18, out if_icfwlfull2_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfwlfull2_ValueProvider(_);
                        return if_icfwlfull2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfwlfull2_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfwlfull2_Read(_, __),
                    name: "Icfwlfull2")
            .WithReservedBits(19, 1)
            .WithFlag(20, out if_icfof0_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfof0_ValueProvider(_);
                        return if_icfof0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfof0_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfof0_Read(_, __),
                    name: "Icfof0")
            .WithFlag(21, out if_icfof1_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfof1_ValueProvider(_);
                        return if_icfof1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfof1_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfof1_Read(_, __),
                    name: "Icfof1")
            .WithFlag(22, out if_icfof2_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfof2_ValueProvider(_);
                        return if_icfof2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfof2_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfof2_Read(_, __),
                    name: "Icfof2")
            .WithReservedBits(23, 1)
            .WithFlag(24, out if_icfuf0_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfuf0_ValueProvider(_);
                        return if_icfuf0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfuf0_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfuf0_Read(_, __),
                    name: "Icfuf0")
            .WithFlag(25, out if_icfuf1_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfuf1_ValueProvider(_);
                        return if_icfuf1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfuf1_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfuf1_Read(_, __),
                    name: "Icfuf1")
            .WithFlag(26, out if_icfuf2_bit, 
                    valueProviderCallback: (_) => {
                        If_Icfuf2_ValueProvider(_);
                        return if_icfuf2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Icfuf2_Write(_, __),
                    
                    readCallback: (_, __) => If_Icfuf2_Read(_, __),
                    name: "Icfuf2")
            .WithReservedBits(27, 5)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x18
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_of_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Of_ValueProvider(_);
                        return ien_of_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Of_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Of_Read(_, __),
                    name: "Of")
            .WithFlag(1, out ien_uf_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Uf_ValueProvider(_);
                        return ien_uf_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Uf_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Uf_Read(_, __),
                    name: "Uf")
            .WithFlag(2, out ien_dirchg_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Dirchg_ValueProvider(_);
                        return ien_dirchg_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Dirchg_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Dirchg_Read(_, __),
                    name: "Dirchg")
            .WithReservedBits(3, 1)
            .WithFlag(4, out ien_cc0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Cc0_ValueProvider(_);
                        return ien_cc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Cc0_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Cc0_Read(_, __),
                    name: "Cc0")
            .WithFlag(5, out ien_cc1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Cc1_ValueProvider(_);
                        return ien_cc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Cc1_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Cc1_Read(_, __),
                    name: "Cc1")
            .WithFlag(6, out ien_cc2_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Cc2_ValueProvider(_);
                        return ien_cc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Cc2_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Cc2_Read(_, __),
                    name: "Cc2")
            .WithReservedBits(7, 9)
            .WithFlag(16, out ien_icfwlfull0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfwlfull0_ValueProvider(_);
                        return ien_icfwlfull0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfwlfull0_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfwlfull0_Read(_, __),
                    name: "Icfwlfull0")
            .WithFlag(17, out ien_icfwlfull1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfwlfull1_ValueProvider(_);
                        return ien_icfwlfull1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfwlfull1_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfwlfull1_Read(_, __),
                    name: "Icfwlfull1")
            .WithFlag(18, out ien_icfwlfull2_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfwlfull2_ValueProvider(_);
                        return ien_icfwlfull2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfwlfull2_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfwlfull2_Read(_, __),
                    name: "Icfwlfull2")
            .WithReservedBits(19, 1)
            .WithFlag(20, out ien_icfof0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfof0_ValueProvider(_);
                        return ien_icfof0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfof0_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfof0_Read(_, __),
                    name: "Icfof0")
            .WithFlag(21, out ien_icfof1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfof1_ValueProvider(_);
                        return ien_icfof1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfof1_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfof1_Read(_, __),
                    name: "Icfof1")
            .WithFlag(22, out ien_icfof2_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfof2_ValueProvider(_);
                        return ien_icfof2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfof2_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfof2_Read(_, __),
                    name: "Icfof2")
            .WithReservedBits(23, 1)
            .WithFlag(24, out ien_icfuf0_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfuf0_ValueProvider(_);
                        return ien_icfuf0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfuf0_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfuf0_Read(_, __),
                    name: "Icfuf0")
            .WithFlag(25, out ien_icfuf1_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfuf1_ValueProvider(_);
                        return ien_icfuf1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfuf1_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfuf1_Read(_, __),
                    name: "Icfuf1")
            .WithFlag(26, out ien_icfuf2_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Icfuf2_ValueProvider(_);
                        return ien_icfuf2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Icfuf2_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Icfuf2_Read(_, __),
                    name: "Icfuf2")
            .WithReservedBits(27, 5)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Top - Offset : 0x1C
        protected DoubleWordRegister  GenerateTopRegister() => new DoubleWordRegister(this, 0xFFFF)
            
            .WithValueField(0, 32, out top_top_field, 
                    valueProviderCallback: (_) => {
                        Top_Top_ValueProvider(_);
                        return top_top_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Top_Top_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Top_Top_Read(_, __),
                    name: "Top")
            .WithReadCallback((_, __) => Top_Read(_, __))
            .WithWriteCallback((_, __) => Top_Write(_, __));
        
        // Topb - Offset : 0x20
        protected DoubleWordRegister  GenerateTopbRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out topb_topb_field, 
                    valueProviderCallback: (_) => {
                        Topb_Topb_ValueProvider(_);
                        return topb_topb_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Topb_Topb_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Topb_Topb_Read(_, __),
                    name: "Topb")
            .WithReadCallback((_, __) => Topb_Read(_, __))
            .WithWriteCallback((_, __) => Topb_Write(_, __));
        
        // Cnt - Offset : 0x24
        protected DoubleWordRegister  GenerateCntRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out cnt_cnt_field, 
                    valueProviderCallback: (_) => {
                        Cnt_Cnt_ValueProvider(_);
                        return cnt_cnt_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cnt_Cnt_Write(_, __);
                    },
                    
                    readCallback: (_, __) => Cnt_Cnt_Read(_, __),
                    name: "Cnt")
            .WithReadCallback((_, __) => Cnt_Read(_, __))
            .WithWriteCallback((_, __) => Cnt_Write(_, __));
        
        // Lock - Offset : 0x2C
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // En - Offset : 0x30
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
        
        // Cc0_Cfg - Offset : 0x60
        protected DoubleWordRegister  GenerateCc0_cfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CC_CFG_MODE>(0, 2, out cc_cfg_mode_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Mode_ValueProvider(0, _);
                        return cc_cfg_mode_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Mode_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Mode_Read(0,_, __),
                    name: "Mode")
            .WithReservedBits(2, 2)
            .WithFlag(4, out cc_cfg_coist_bit[0], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Coist_ValueProvider(0, _);
                        return cc_cfg_coist_bit[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Coist_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Coist_Read(0,_, __),
                    name: "Coist")
            .WithReservedBits(5, 12)
            .WithEnumField<DoubleWordRegister, CC_CFG_INSEL>(17, 2, out cc_cfg_insel_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Insel_ValueProvider(0, _);
                        return cc_cfg_insel_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Insel_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Insel_Read(0,_, __),
                    name: "Insel")
            .WithEnumField<DoubleWordRegister, CC_CFG_PRSCONF>(19, 1, out cc_cfg_prsconf_bit[0], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Prsconf_ValueProvider(0, _);
                        return cc_cfg_prsconf_bit[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Prsconf_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Prsconf_Read(0,_, __),
                name: "Prsconf")
            .WithEnumField<DoubleWordRegister, CC_CFG_FILT>(20, 1, out cc_cfg_filt_bit[0], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Filt_ValueProvider(0, _);
                        return cc_cfg_filt_bit[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Filt_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Filt_Read(0,_, __),
                name: "Filt")
            .WithFlag(21, out cc_cfg_icfwl_bit[0], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Icfwl_ValueProvider(0, _);
                        return cc_cfg_icfwl_bit[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Icfwl_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Icfwl_Read(0,_, __),
                    name: "Icfwl")
            .WithReservedBits(22, 10)
            .WithReadCallback((_, __) => Cc_Cfg_Read(0, _, __))
            .WithWriteCallback((_, __) => Cc_Cfg_Write(0, _, __));
        
        // Cc0_Ctrl - Offset : 0x64
        protected DoubleWordRegister  GenerateCc0_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 2)
            .WithFlag(2, out cc_ctrl_outinv_bit[0], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Outinv_ValueProvider(0, _);
                        return cc_ctrl_outinv_bit[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Outinv_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Outinv_Read(0,_, __),
                    name: "Outinv")
            .WithReservedBits(3, 5)
            .WithEnumField<DoubleWordRegister, CC_CTRL_CMOA>(8, 2, out cc_ctrl_cmoa_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cmoa_ValueProvider(0, _);
                        return cc_ctrl_cmoa_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cmoa_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cmoa_Read(0,_, __),
                    name: "Cmoa")
            .WithEnumField<DoubleWordRegister, CC_CTRL_COFOA>(10, 2, out cc_ctrl_cofoa_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cofoa_ValueProvider(0, _);
                        return cc_ctrl_cofoa_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cofoa_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cofoa_Read(0,_, __),
                    name: "Cofoa")
            .WithEnumField<DoubleWordRegister, CC_CTRL_CUFOA>(12, 2, out cc_ctrl_cufoa_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cufoa_ValueProvider(0, _);
                        return cc_ctrl_cufoa_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cufoa_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cufoa_Read(0,_, __),
                    name: "Cufoa")
            .WithReservedBits(14, 10)
            .WithEnumField<DoubleWordRegister, CC_CTRL_ICEDGE>(24, 2, out cc_ctrl_icedge_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Icedge_ValueProvider(0, _);
                        return cc_ctrl_icedge_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Icedge_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Icedge_Read(0,_, __),
                    name: "Icedge")
            .WithEnumField<DoubleWordRegister, CC_CTRL_ICEVCTRL>(26, 2, out cc_ctrl_icevctrl_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Icevctrl_ValueProvider(0, _);
                        return cc_ctrl_icevctrl_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Icevctrl_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Icevctrl_Read(0,_, __),
                    name: "Icevctrl")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Cc_Ctrl_Read(0, _, __))
            .WithWriteCallback((_, __) => Cc_Ctrl_Write(0, _, __));
        
        // Cc0_Oc - Offset : 0x68
        protected DoubleWordRegister  GenerateCc0_ocRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_oc_oc_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Oc_Oc_ValueProvider(0, _);
                        return cc_oc_oc_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cc_Oc_Oc_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Oc_Oc_Read(0,_, __),
                    name: "Oc")
            .WithReadCallback((_, __) => Cc_Oc_Read(0, _, __))
            .WithWriteCallback((_, __) => Cc_Oc_Write(0, _, __));
        
        // Cc0_Ocb - Offset : 0x70
        protected DoubleWordRegister  GenerateCc0_ocbRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_ocb_ocb_field[0], 
                    valueProviderCallback: (_) => {
                        Cc_Ocb_Ocb_ValueProvider(0, _);
                        return cc_ocb_ocb_field[0].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ocb_Ocb_Write(0, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ocb_Ocb_Read(0,_, __),
                    name: "Ocb")
            .WithReadCallback((_, __) => Cc_Ocb_Read(0, _, __))
            .WithWriteCallback((_, __) => Cc_Ocb_Write(0, _, __));
        
        // Cc0_Icf - Offset : 0x74
        protected DoubleWordRegister  GenerateCc0_icfRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_icf_icf_field[0], FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cc_Icf_Icf_ValueProvider(0, _);
                        return ReadRFIFO();
                    },
                    
                    readCallback: (_, __) => Cc_Icf_Icf_Read(0,_, __),
                    name: "Icf")
            .WithReadCallback((_, __) => Cc_Icf_Read(0, _, __))
            .WithWriteCallback((_, __) => Cc_Icf_Write(0, _, __));
        
        // Cc0_Icof - Offset : 0x78
        protected DoubleWordRegister  GenerateCc0_icofRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_icof_icof_field[0], FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cc_Icof_Icof_ValueProvider(0, _);
                        return cc_icof_icof_field[0].Value;
                    },
                    
                    readCallback: (_, __) => Cc_Icof_Icof_Read(0,_, __),
                    name: "Icof")
            .WithReadCallback((_, __) => Cc_Icof_Read(0, _, __))
            .WithWriteCallback((_, __) => Cc_Icof_Write(0, _, __));
        
        // Cc1_Cfg - Offset : 0x80
        protected DoubleWordRegister  GenerateCc1_cfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CC_CFG_MODE>(0, 2, out cc_cfg_mode_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Mode_ValueProvider(1, _);
                        return cc_cfg_mode_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Mode_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Mode_Read(1,_, __),
                    name: "Mode")
            .WithReservedBits(2, 2)
            .WithFlag(4, out cc_cfg_coist_bit[1], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Coist_ValueProvider(1, _);
                        return cc_cfg_coist_bit[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Coist_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Coist_Read(1,_, __),
                    name: "Coist")
            .WithReservedBits(5, 12)
            .WithEnumField<DoubleWordRegister, CC_CFG_INSEL>(17, 2, out cc_cfg_insel_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Insel_ValueProvider(1, _);
                        return cc_cfg_insel_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Insel_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Insel_Read(1,_, __),
                    name: "Insel")
            .WithEnumField<DoubleWordRegister, CC_CFG_PRSCONF>(19, 1, out cc_cfg_prsconf_bit[1], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Prsconf_ValueProvider(1, _);
                        return cc_cfg_prsconf_bit[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Prsconf_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Prsconf_Read(1,_, __),
                name: "Prsconf")
            .WithEnumField<DoubleWordRegister, CC_CFG_FILT>(20, 1, out cc_cfg_filt_bit[1], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Filt_ValueProvider(1, _);
                        return cc_cfg_filt_bit[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Filt_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Filt_Read(1,_, __),
                name: "Filt")
            .WithFlag(21, out cc_cfg_icfwl_bit[1], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Icfwl_ValueProvider(1, _);
                        return cc_cfg_icfwl_bit[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Icfwl_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Icfwl_Read(1,_, __),
                    name: "Icfwl")
            .WithReservedBits(22, 10)
            .WithReadCallback((_, __) => Cc_Cfg_Read(1, _, __))
            .WithWriteCallback((_, __) => Cc_Cfg_Write(1, _, __));
        
        // Cc1_Ctrl - Offset : 0x84
        protected DoubleWordRegister  GenerateCc1_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 2)
            .WithFlag(2, out cc_ctrl_outinv_bit[1], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Outinv_ValueProvider(1, _);
                        return cc_ctrl_outinv_bit[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Outinv_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Outinv_Read(1,_, __),
                    name: "Outinv")
            .WithReservedBits(3, 5)
            .WithEnumField<DoubleWordRegister, CC_CTRL_CMOA>(8, 2, out cc_ctrl_cmoa_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cmoa_ValueProvider(1, _);
                        return cc_ctrl_cmoa_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cmoa_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cmoa_Read(1,_, __),
                    name: "Cmoa")
            .WithEnumField<DoubleWordRegister, CC_CTRL_COFOA>(10, 2, out cc_ctrl_cofoa_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cofoa_ValueProvider(1, _);
                        return cc_ctrl_cofoa_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cofoa_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cofoa_Read(1,_, __),
                    name: "Cofoa")
            .WithEnumField<DoubleWordRegister, CC_CTRL_CUFOA>(12, 2, out cc_ctrl_cufoa_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cufoa_ValueProvider(1, _);
                        return cc_ctrl_cufoa_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cufoa_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cufoa_Read(1,_, __),
                    name: "Cufoa")
            .WithReservedBits(14, 10)
            .WithEnumField<DoubleWordRegister, CC_CTRL_ICEDGE>(24, 2, out cc_ctrl_icedge_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Icedge_ValueProvider(1, _);
                        return cc_ctrl_icedge_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Icedge_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Icedge_Read(1,_, __),
                    name: "Icedge")
            .WithEnumField<DoubleWordRegister, CC_CTRL_ICEVCTRL>(26, 2, out cc_ctrl_icevctrl_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Icevctrl_ValueProvider(1, _);
                        return cc_ctrl_icevctrl_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Icevctrl_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Icevctrl_Read(1,_, __),
                    name: "Icevctrl")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Cc_Ctrl_Read(1, _, __))
            .WithWriteCallback((_, __) => Cc_Ctrl_Write(1, _, __));
        
        // Cc1_Oc - Offset : 0x88
        protected DoubleWordRegister  GenerateCc1_ocRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_oc_oc_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Oc_Oc_ValueProvider(1, _);
                        return cc_oc_oc_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cc_Oc_Oc_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Oc_Oc_Read(1,_, __),
                    name: "Oc")
            .WithReadCallback((_, __) => Cc_Oc_Read(1, _, __))
            .WithWriteCallback((_, __) => Cc_Oc_Write(1, _, __));
        
        // Cc1_Ocb - Offset : 0x90
        protected DoubleWordRegister  GenerateCc1_ocbRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_ocb_ocb_field[1], 
                    valueProviderCallback: (_) => {
                        Cc_Ocb_Ocb_ValueProvider(1, _);
                        return cc_ocb_ocb_field[1].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ocb_Ocb_Write(1, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ocb_Ocb_Read(1,_, __),
                    name: "Ocb")
            .WithReadCallback((_, __) => Cc_Ocb_Read(1, _, __))
            .WithWriteCallback((_, __) => Cc_Ocb_Write(1, _, __));
        
        // Cc1_Icf - Offset : 0x94
        protected DoubleWordRegister  GenerateCc1_icfRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_icf_icf_field[1], FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cc_Icf_Icf_ValueProvider(1, _);
                        return ReadRFIFO();
                    },
                    
                    readCallback: (_, __) => Cc_Icf_Icf_Read(1,_, __),
                    name: "Icf")
            .WithReadCallback((_, __) => Cc_Icf_Read(1, _, __))
            .WithWriteCallback((_, __) => Cc_Icf_Write(1, _, __));
        
        // Cc1_Icof - Offset : 0x98
        protected DoubleWordRegister  GenerateCc1_icofRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_icof_icof_field[1], FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cc_Icof_Icof_ValueProvider(1, _);
                        return cc_icof_icof_field[1].Value;
                    },
                    
                    readCallback: (_, __) => Cc_Icof_Icof_Read(1,_, __),
                    name: "Icof")
            .WithReadCallback((_, __) => Cc_Icof_Read(1, _, __))
            .WithWriteCallback((_, __) => Cc_Icof_Write(1, _, __));
        
        // Cc2_Cfg - Offset : 0xA0
        protected DoubleWordRegister  GenerateCc2_cfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, CC_CFG_MODE>(0, 2, out cc_cfg_mode_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Mode_ValueProvider(2, _);
                        return cc_cfg_mode_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Mode_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Mode_Read(2,_, __),
                    name: "Mode")
            .WithReservedBits(2, 2)
            .WithFlag(4, out cc_cfg_coist_bit[2], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Coist_ValueProvider(2, _);
                        return cc_cfg_coist_bit[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Coist_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Coist_Read(2,_, __),
                    name: "Coist")
            .WithReservedBits(5, 12)
            .WithEnumField<DoubleWordRegister, CC_CFG_INSEL>(17, 2, out cc_cfg_insel_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Insel_ValueProvider(2, _);
                        return cc_cfg_insel_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Insel_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Insel_Read(2,_, __),
                    name: "Insel")
            .WithEnumField<DoubleWordRegister, CC_CFG_PRSCONF>(19, 1, out cc_cfg_prsconf_bit[2], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Prsconf_ValueProvider(2, _);
                        return cc_cfg_prsconf_bit[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Prsconf_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Prsconf_Read(2,_, __),
                name: "Prsconf")
            .WithEnumField<DoubleWordRegister, CC_CFG_FILT>(20, 1, out cc_cfg_filt_bit[2], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Filt_ValueProvider(2, _);
                        return cc_cfg_filt_bit[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Filt_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Filt_Read(2,_, __),
                name: "Filt")
            .WithFlag(21, out cc_cfg_icfwl_bit[2], 
                    valueProviderCallback: (_) => {
                        Cc_Cfg_Icfwl_ValueProvider(2, _);
                        return cc_cfg_icfwl_bit[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cc_Cfg_Icfwl_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Cfg_Icfwl_Read(2,_, __),
                    name: "Icfwl")
            .WithReservedBits(22, 10)
            .WithReadCallback((_, __) => Cc_Cfg_Read(2, _, __))
            .WithWriteCallback((_, __) => Cc_Cfg_Write(2, _, __));
        
        // Cc2_Ctrl - Offset : 0xA4
        protected DoubleWordRegister  GenerateCc2_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 2)
            .WithFlag(2, out cc_ctrl_outinv_bit[2], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Outinv_ValueProvider(2, _);
                        return cc_ctrl_outinv_bit[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Outinv_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Outinv_Read(2,_, __),
                    name: "Outinv")
            .WithReservedBits(3, 5)
            .WithEnumField<DoubleWordRegister, CC_CTRL_CMOA>(8, 2, out cc_ctrl_cmoa_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cmoa_ValueProvider(2, _);
                        return cc_ctrl_cmoa_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cmoa_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cmoa_Read(2,_, __),
                    name: "Cmoa")
            .WithEnumField<DoubleWordRegister, CC_CTRL_COFOA>(10, 2, out cc_ctrl_cofoa_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cofoa_ValueProvider(2, _);
                        return cc_ctrl_cofoa_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cofoa_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cofoa_Read(2,_, __),
                    name: "Cofoa")
            .WithEnumField<DoubleWordRegister, CC_CTRL_CUFOA>(12, 2, out cc_ctrl_cufoa_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Cufoa_ValueProvider(2, _);
                        return cc_ctrl_cufoa_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Cufoa_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Cufoa_Read(2,_, __),
                    name: "Cufoa")
            .WithReservedBits(14, 10)
            .WithEnumField<DoubleWordRegister, CC_CTRL_ICEDGE>(24, 2, out cc_ctrl_icedge_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Icedge_ValueProvider(2, _);
                        return cc_ctrl_icedge_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Icedge_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Icedge_Read(2,_, __),
                    name: "Icedge")
            .WithEnumField<DoubleWordRegister, CC_CTRL_ICEVCTRL>(26, 2, out cc_ctrl_icevctrl_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Ctrl_Icevctrl_ValueProvider(2, _);
                        return cc_ctrl_icevctrl_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ctrl_Icevctrl_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ctrl_Icevctrl_Read(2,_, __),
                    name: "Icevctrl")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Cc_Ctrl_Read(2, _, __))
            .WithWriteCallback((_, __) => Cc_Ctrl_Write(2, _, __));
        
        // Cc2_Oc - Offset : 0xA8
        protected DoubleWordRegister  GenerateCc2_ocRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_oc_oc_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Oc_Oc_ValueProvider(2, _);
                        return cc_oc_oc_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cc_Oc_Oc_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Oc_Oc_Read(2,_, __),
                    name: "Oc")
            .WithReadCallback((_, __) => Cc_Oc_Read(2, _, __))
            .WithWriteCallback((_, __) => Cc_Oc_Write(2, _, __));
        
        // Cc2_Ocb - Offset : 0xB0
        protected DoubleWordRegister  GenerateCc2_ocbRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_ocb_ocb_field[2], 
                    valueProviderCallback: (_) => {
                        Cc_Ocb_Ocb_ValueProvider(2, _);
                        return cc_ocb_ocb_field[2].Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cc_Ocb_Ocb_Write(2, _, __);
                    },
                    
                    readCallback: (_, __) => Cc_Ocb_Ocb_Read(2,_, __),
                    name: "Ocb")
            .WithReadCallback((_, __) => Cc_Ocb_Read(2, _, __))
            .WithWriteCallback((_, __) => Cc_Ocb_Write(2, _, __));
        
        // Cc2_Icf - Offset : 0xB4
        protected DoubleWordRegister  GenerateCc2_icfRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_icf_icf_field[2], FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cc_Icf_Icf_ValueProvider(2, _);
                        return ReadRFIFO();
                    },
                    
                    readCallback: (_, __) => Cc_Icf_Icf_Read(2,_, __),
                    name: "Icf")
            .WithReadCallback((_, __) => Cc_Icf_Read(2, _, __))
            .WithWriteCallback((_, __) => Cc_Icf_Write(2, _, __));
        
        // Cc2_Icof - Offset : 0xB8
        protected DoubleWordRegister  GenerateCc2_icofRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out cc_icof_icof_field[2], FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Cc_Icof_Icof_ValueProvider(2, _);
                        return cc_icof_icof_field[2].Value;
                    },
                    
                    readCallback: (_, __) => Cc_Icof_Icof_Read(2,_, __),
                    name: "Icof")
            .WithReadCallback((_, __) => Cc_Icof_Read(2, _, __))
            .WithWriteCallback((_, __) => Cc_Icof_Write(2, _, __));
        
        // DtCfg - Offset : 0xE0
        protected DoubleWordRegister  GenerateDtcfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dtcfg_dten_bit, 
                    valueProviderCallback: (_) => {
                        DtCfg_Dten_ValueProvider(_);
                        return dtcfg_dten_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtCfg_Dten_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtCfg_Dten_Read(_, __),
                    name: "Dten")
            .WithEnumField<DoubleWordRegister, DTCFG_DTDAS>(1, 1, out dtcfg_dtdas_bit, 
                    valueProviderCallback: (_) => {
                        DtCfg_Dtdas_ValueProvider(_);
                        return dtcfg_dtdas_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtCfg_Dtdas_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtCfg_Dtdas_Read(_, __),
                    name: "Dtdas")
            .WithReservedBits(2, 7)
            .WithFlag(9, out dtcfg_dtar_bit, 
                    valueProviderCallback: (_) => {
                        DtCfg_Dtar_ValueProvider(_);
                        return dtcfg_dtar_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtCfg_Dtar_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtCfg_Dtar_Read(_, __),
                    name: "Dtar")
            .WithFlag(10, out dtcfg_dtfats_bit, 
                    valueProviderCallback: (_) => {
                        DtCfg_Dtfats_ValueProvider(_);
                        return dtcfg_dtfats_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtCfg_Dtfats_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtCfg_Dtfats_Read(_, __),
                    name: "Dtfats")
            .WithFlag(11, out dtcfg_dtprsen_bit, 
                    valueProviderCallback: (_) => {
                        DtCfg_Dtprsen_ValueProvider(_);
                        return dtcfg_dtprsen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtCfg_Dtprsen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtCfg_Dtprsen_Read(_, __),
                    name: "Dtprsen")
            .WithReservedBits(12, 20)
            .WithReadCallback((_, __) => DtCfg_Read(_, __))
            .WithWriteCallback((_, __) => DtCfg_Write(_, __));
        
        // DtTimecfg - Offset : 0xE4
        protected DoubleWordRegister  GenerateDttimecfgRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 10, out dttimecfg_dtpresc_field, 
                    valueProviderCallback: (_) => {
                        DtTimecfg_Dtpresc_ValueProvider(_);
                        return dttimecfg_dtpresc_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtTimecfg_Dtpresc_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtTimecfg_Dtpresc_Read(_, __),
                    name: "Dtpresc")
            
            .WithValueField(10, 6, out dttimecfg_dtriset_field, 
                    valueProviderCallback: (_) => {
                        DtTimecfg_Dtriset_ValueProvider(_);
                        return dttimecfg_dtriset_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtTimecfg_Dtriset_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtTimecfg_Dtriset_Read(_, __),
                    name: "Dtriset")
            
            .WithValueField(16, 6, out dttimecfg_dtfallt_field, 
                    valueProviderCallback: (_) => {
                        DtTimecfg_Dtfallt_ValueProvider(_);
                        return dttimecfg_dtfallt_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtTimecfg_Dtfallt_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtTimecfg_Dtfallt_Read(_, __),
                    name: "Dtfallt")
            .WithReservedBits(22, 10)
            .WithReadCallback((_, __) => DtTimecfg_Read(_, __))
            .WithWriteCallback((_, __) => DtTimecfg_Write(_, __));
        
        // DtFcfg - Offset : 0xE8
        protected DoubleWordRegister  GenerateDtfcfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 16)
            .WithEnumField<DoubleWordRegister, DTFCFG_DTFA>(16, 2, out dtfcfg_dtfa_field, 
                    valueProviderCallback: (_) => {
                        DtFcfg_Dtfa_ValueProvider(_);
                        return dtfcfg_dtfa_field.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtFcfg_Dtfa_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtFcfg_Dtfa_Read(_, __),
                    name: "Dtfa")
            .WithReservedBits(18, 6)
            .WithFlag(24, out dtfcfg_dtprs0fen_bit, 
                    valueProviderCallback: (_) => {
                        DtFcfg_Dtprs0fen_ValueProvider(_);
                        return dtfcfg_dtprs0fen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtFcfg_Dtprs0fen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtFcfg_Dtprs0fen_Read(_, __),
                    name: "Dtprs0fen")
            .WithFlag(25, out dtfcfg_dtprs1fen_bit, 
                    valueProviderCallback: (_) => {
                        DtFcfg_Dtprs1fen_ValueProvider(_);
                        return dtfcfg_dtprs1fen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtFcfg_Dtprs1fen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtFcfg_Dtprs1fen_Read(_, __),
                    name: "Dtprs1fen")
            .WithFlag(26, out dtfcfg_dtdbgfen_bit, 
                    valueProviderCallback: (_) => {
                        DtFcfg_Dtdbgfen_ValueProvider(_);
                        return dtfcfg_dtdbgfen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtFcfg_Dtdbgfen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtFcfg_Dtdbgfen_Read(_, __),
                    name: "Dtdbgfen")
            .WithFlag(27, out dtfcfg_dtlockupfen_bit, 
                    valueProviderCallback: (_) => {
                        DtFcfg_Dtlockupfen_ValueProvider(_);
                        return dtfcfg_dtlockupfen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtFcfg_Dtlockupfen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtFcfg_Dtlockupfen_Read(_, __),
                    name: "Dtlockupfen")
            .WithFlag(28, out dtfcfg_dtem23fen_bit, 
                    valueProviderCallback: (_) => {
                        DtFcfg_Dtem23fen_ValueProvider(_);
                        return dtfcfg_dtem23fen_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        DtFcfg_Dtem23fen_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtFcfg_Dtem23fen_Read(_, __),
                    name: "Dtem23fen")
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => DtFcfg_Read(_, __))
            .WithWriteCallback((_, __) => DtFcfg_Write(_, __));
        
        // DtCtrl - Offset : 0xEC
        protected DoubleWordRegister  GenerateDtctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dtctrl_dtcinv_bit, 
                    valueProviderCallback: (_) => {
                        DtCtrl_Dtcinv_ValueProvider(_);
                        return dtctrl_dtcinv_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtCtrl_Dtcinv_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtCtrl_Dtcinv_Read(_, __),
                    name: "Dtcinv")
            .WithFlag(1, out dtctrl_dtipol_bit, 
                    valueProviderCallback: (_) => {
                        DtCtrl_Dtipol_ValueProvider(_);
                        return dtctrl_dtipol_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtCtrl_Dtipol_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtCtrl_Dtipol_Read(_, __),
                    name: "Dtipol")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => DtCtrl_Read(_, __))
            .WithWriteCallback((_, __) => DtCtrl_Write(_, __));
        
        // DtOgen - Offset : 0xF0
        protected DoubleWordRegister  GenerateDtogenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dtogen_dtogcc0en_bit, 
                    valueProviderCallback: (_) => {
                        DtOgen_Dtogcc0en_ValueProvider(_);
                        return dtogen_dtogcc0en_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtOgen_Dtogcc0en_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtOgen_Dtogcc0en_Read(_, __),
                    name: "Dtogcc0en")
            .WithFlag(1, out dtogen_dtogcc1en_bit, 
                    valueProviderCallback: (_) => {
                        DtOgen_Dtogcc1en_ValueProvider(_);
                        return dtogen_dtogcc1en_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtOgen_Dtogcc1en_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtOgen_Dtogcc1en_Read(_, __),
                    name: "Dtogcc1en")
            .WithFlag(2, out dtogen_dtogcc2en_bit, 
                    valueProviderCallback: (_) => {
                        DtOgen_Dtogcc2en_ValueProvider(_);
                        return dtogen_dtogcc2en_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtOgen_Dtogcc2en_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtOgen_Dtogcc2en_Read(_, __),
                    name: "Dtogcc2en")
            .WithFlag(3, out dtogen_dtogcdti0en_bit, 
                    valueProviderCallback: (_) => {
                        DtOgen_Dtogcdti0en_ValueProvider(_);
                        return dtogen_dtogcdti0en_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtOgen_Dtogcdti0en_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtOgen_Dtogcdti0en_Read(_, __),
                    name: "Dtogcdti0en")
            .WithFlag(4, out dtogen_dtogcdti1en_bit, 
                    valueProviderCallback: (_) => {
                        DtOgen_Dtogcdti1en_ValueProvider(_);
                        return dtogen_dtogcdti1en_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtOgen_Dtogcdti1en_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtOgen_Dtogcdti1en_Read(_, __),
                    name: "Dtogcdti1en")
            .WithFlag(5, out dtogen_dtogcdti2en_bit, 
                    valueProviderCallback: (_) => {
                        DtOgen_Dtogcdti2en_ValueProvider(_);
                        return dtogen_dtogcdti2en_bit.Value;
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtOgen_Dtogcdti2en_Write(_, __);
                    },
                    
                    readCallback: (_, __) => DtOgen_Dtogcdti2en_Read(_, __),
                    name: "Dtogcdti2en")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => DtOgen_Read(_, __))
            .WithWriteCallback((_, __) => DtOgen_Write(_, __));
        
        // DtFault - Offset : 0xF4
        protected DoubleWordRegister  GenerateDtfaultRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dtfault_dtprs0f_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        DtFault_Dtprs0f_ValueProvider(_);
                        return dtfault_dtprs0f_bit.Value;
                    },
                    
                    readCallback: (_, __) => DtFault_Dtprs0f_Read(_, __),
                    name: "Dtprs0f")
            .WithFlag(1, out dtfault_dtprs1f_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        DtFault_Dtprs1f_ValueProvider(_);
                        return dtfault_dtprs1f_bit.Value;
                    },
                    
                    readCallback: (_, __) => DtFault_Dtprs1f_Read(_, __),
                    name: "Dtprs1f")
            .WithFlag(2, out dtfault_dtdbgf_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        DtFault_Dtdbgf_ValueProvider(_);
                        return dtfault_dtdbgf_bit.Value;
                    },
                    
                    readCallback: (_, __) => DtFault_Dtdbgf_Read(_, __),
                    name: "Dtdbgf")
            .WithFlag(3, out dtfault_dtlockupf_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        DtFault_Dtlockupf_ValueProvider(_);
                        return dtfault_dtlockupf_bit.Value;
                    },
                    
                    readCallback: (_, __) => DtFault_Dtlockupf_Read(_, __),
                    name: "Dtlockupf")
            .WithFlag(4, out dtfault_dtem23f_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        DtFault_Dtem23f_ValueProvider(_);
                        return dtfault_dtem23f_bit.Value;
                    },
                    
                    readCallback: (_, __) => DtFault_Dtem23f_Read(_, __),
                    name: "Dtem23f")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => DtFault_Read(_, __))
            .WithWriteCallback((_, __) => DtFault_Write(_, __));
        
        // DtFaultc - Offset : 0xF8
        protected DoubleWordRegister  GenerateDtfaultcRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out dtfaultc_dtprs0fc_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtFaultc_Dtprs0fc_Write(_, __);
                    },
                    name: "Dtprs0fc")
            .WithFlag(1, out dtfaultc_dtprs1fc_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtFaultc_Dtprs1fc_Write(_, __);
                    },
                    name: "Dtprs1fc")
            .WithFlag(2, out dtfaultc_dtdbgfc_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtFaultc_Dtdbgfc_Write(_, __);
                    },
                    name: "Dtdbgfc")
            .WithFlag(3, out dtfaultc_dtlockupfc_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtFaultc_Dtlockupfc_Write(_, __);
                    },
                    name: "Dtlockupfc")
            .WithFlag(4, out dtfaultc_dtem23fc_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        DtFaultc_Dtem23fc_Write(_, __);
                    },
                    name: "Dtem23fc")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => DtFaultc_Read(_, __))
            .WithWriteCallback((_, __) => DtFaultc_Write(_, __));
        
        // DtLock - Offset : 0xFC
        protected DoubleWordRegister  GenerateDtlockRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, DTLOCK_DTILOCKKEY>(0, 16, out dtlock_dtilockkey_field, FieldMode.Write,
                    
                    writeCallback: (_, __) => DtLock_Dtilockkey_Write(_, __),
                    name: "Dtilockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => DtLock_Read(_, __))
            .WithWriteCallback((_, __) => DtLock_Write(_, __));
        

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

        private void WriteWSYNC()
        {
            if(!Enabled)
            {
                this.Log(LogLevel.Error, "Trying to write to a WSYNC register while peripheral is disabled EN = {0}", Enabled);
            }
        }

        private void WriteRWSYNC()
        {
            if(!Enabled)
            {
                this.Log(LogLevel.Error, "Trying to write to a RWSYNC register while peripheral is disabled EN = {0}", Enabled);
            }
        }

        
        // Ipversion - Offset : 0x0
    
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);
        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        // Cfg - Offset : 0x4
    
        protected IEnumRegisterField<CFG_MODE> cfg_mode_field;
        partial void Cfg_Mode_Write(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_Read(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_ValueProvider(CFG_MODE a);
    
        protected IEnumRegisterField<CFG_SYNC> cfg_sync_bit;
        partial void Cfg_Sync_Write(CFG_SYNC a, CFG_SYNC b);
        partial void Cfg_Sync_Read(CFG_SYNC a, CFG_SYNC b);
        partial void Cfg_Sync_ValueProvider(CFG_SYNC a);
    
        protected IFlagRegisterField cfg_osmen_bit;
        partial void Cfg_Osmen_Write(bool a, bool b);
        partial void Cfg_Osmen_Read(bool a, bool b);
        partial void Cfg_Osmen_ValueProvider(bool a);
    
        protected IEnumRegisterField<CFG_QDM> cfg_qdm_bit;
        partial void Cfg_Qdm_Write(CFG_QDM a, CFG_QDM b);
        partial void Cfg_Qdm_Read(CFG_QDM a, CFG_QDM b);
        partial void Cfg_Qdm_ValueProvider(CFG_QDM a);
    
        protected IEnumRegisterField<CFG_DEBUGRUN> cfg_debugrun_bit;
        partial void Cfg_Debugrun_Write(CFG_DEBUGRUN a, CFG_DEBUGRUN b);
        partial void Cfg_Debugrun_Read(CFG_DEBUGRUN a, CFG_DEBUGRUN b);
        partial void Cfg_Debugrun_ValueProvider(CFG_DEBUGRUN a);
    
        protected IFlagRegisterField cfg_dmaclract_bit;
        partial void Cfg_Dmaclract_Write(bool a, bool b);
        partial void Cfg_Dmaclract_Read(bool a, bool b);
        partial void Cfg_Dmaclract_ValueProvider(bool a);
    
        protected IEnumRegisterField<CFG_CLKSEL> cfg_clksel_field;
        partial void Cfg_Clksel_Write(CFG_CLKSEL a, CFG_CLKSEL b);
        partial void Cfg_Clksel_Read(CFG_CLKSEL a, CFG_CLKSEL b);
        partial void Cfg_Clksel_ValueProvider(CFG_CLKSEL a);
    
        protected IEnumRegisterField<CFG_RETIMEEN> cfg_retimeen_bit;
        partial void Cfg_Retimeen_Write(CFG_RETIMEEN a, CFG_RETIMEEN b);
        partial void Cfg_Retimeen_Read(CFG_RETIMEEN a, CFG_RETIMEEN b);
        partial void Cfg_Retimeen_ValueProvider(CFG_RETIMEEN a);
    
        protected IEnumRegisterField<CFG_DISSYNCOUT> cfg_dissyncout_bit;
        partial void Cfg_Dissyncout_Write(CFG_DISSYNCOUT a, CFG_DISSYNCOUT b);
        partial void Cfg_Dissyncout_Read(CFG_DISSYNCOUT a, CFG_DISSYNCOUT b);
        partial void Cfg_Dissyncout_ValueProvider(CFG_DISSYNCOUT a);
    
        protected IFlagRegisterField cfg_retimesel_bit;
        partial void Cfg_Retimesel_Write(bool a, bool b);
        partial void Cfg_Retimesel_Read(bool a, bool b);
        partial void Cfg_Retimesel_ValueProvider(bool a);
    
        protected IFlagRegisterField cfg_ati_bit;
        partial void Cfg_Ati_Write(bool a, bool b);
        partial void Cfg_Ati_Read(bool a, bool b);
        partial void Cfg_Ati_ValueProvider(bool a);
    
        protected IFlagRegisterField cfg_rsscoist_bit;
        partial void Cfg_Rsscoist_Write(bool a, bool b);
        partial void Cfg_Rsscoist_Read(bool a, bool b);
        partial void Cfg_Rsscoist_ValueProvider(bool a);
    
        protected IEnumRegisterField<CFG_PRESC> cfg_presc_field;
        partial void Cfg_Presc_Write(CFG_PRESC a, CFG_PRESC b);
        partial void Cfg_Presc_Read(CFG_PRESC a, CFG_PRESC b);
        partial void Cfg_Presc_ValueProvider(CFG_PRESC a);
        protected void Cfg_Write_WithHook(uint a, uint b)
        {
            if (status_timerlockstatus_bit.Value == STATUS_TIMERLOCKSTATUS.LOCKED)
            {
                this.Log(LogLevel.Error, "Cfg: Write access to a locked register");
            }
            Cfg_Write(a, b);
        }
        partial void Cfg_Write(uint a, uint b);
        partial void Cfg_Read(uint a, uint b);
        
        // Ctrl - Offset : 0x8
    
        protected IEnumRegisterField<CTRL_RISEA> ctrl_risea_field;
        partial void Ctrl_Risea_Write(CTRL_RISEA a, CTRL_RISEA b);
        partial void Ctrl_Risea_Read(CTRL_RISEA a, CTRL_RISEA b);
        partial void Ctrl_Risea_ValueProvider(CTRL_RISEA a);
    
        protected IEnumRegisterField<CTRL_FALLA> ctrl_falla_field;
        partial void Ctrl_Falla_Write(CTRL_FALLA a, CTRL_FALLA b);
        partial void Ctrl_Falla_Read(CTRL_FALLA a, CTRL_FALLA b);
        partial void Ctrl_Falla_ValueProvider(CTRL_FALLA a);
    
        protected IFlagRegisterField ctrl_x2cnt_bit;
        partial void Ctrl_X2cnt_Write(bool a, bool b);
        partial void Ctrl_X2cnt_Read(bool a, bool b);
        partial void Ctrl_X2cnt_ValueProvider(bool a);
        protected void Ctrl_Write_WithHook(uint a, uint b)
        {
            if (status_timerlockstatus_bit.Value == STATUS_TIMERLOCKSTATUS.LOCKED)
            {
                this.Log(LogLevel.Error, "Ctrl: Write access to a locked register");
            }
            Ctrl_Write(a, b);
        }
        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Cmd - Offset : 0xC
    
        protected IFlagRegisterField cmd_start_bit;
        partial void Cmd_Start_Write(bool a, bool b);
        partial void Cmd_Start_ValueProvider(bool a);
    
        protected IFlagRegisterField cmd_stop_bit;
        partial void Cmd_Stop_Write(bool a, bool b);
        partial void Cmd_Stop_ValueProvider(bool a);
        protected void Cmd_Write_WithHook(uint a, uint b)
        {
            if (status_timerlockstatus_bit.Value == STATUS_TIMERLOCKSTATUS.LOCKED)
            {
                this.Log(LogLevel.Error, "Cmd: Write access to a locked register");
            }
            Cmd_Write(a, b);
        }
        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);
        
        // Status - Offset : 0x10
    
        protected IFlagRegisterField status_running_bit;
        partial void Status_Running_Read(bool a, bool b);
        partial void Status_Running_ValueProvider(bool a);
    
        protected IEnumRegisterField<STATUS_DIR> status_dir_bit;
        partial void Status_Dir_Read(STATUS_DIR a, STATUS_DIR b);
        partial void Status_Dir_ValueProvider(STATUS_DIR a);
    
        protected IFlagRegisterField status_topbv_bit;
        partial void Status_Topbv_Read(bool a, bool b);
        partial void Status_Topbv_ValueProvider(bool a);
    
        protected IEnumRegisterField<STATUS_TIMERLOCKSTATUS> status_timerlockstatus_bit;
        partial void Status_Timerlockstatus_Read(STATUS_TIMERLOCKSTATUS a, STATUS_TIMERLOCKSTATUS b);
        partial void Status_Timerlockstatus_ValueProvider(STATUS_TIMERLOCKSTATUS a);
    
        protected IEnumRegisterField<STATUS_DTILOCKSTATUS> status_dtilockstatus_bit;
        partial void Status_Dtilockstatus_Read(STATUS_DTILOCKSTATUS a, STATUS_DTILOCKSTATUS b);
        partial void Status_Dtilockstatus_ValueProvider(STATUS_DTILOCKSTATUS a);
    
        protected IFlagRegisterField status_syncbusy_bit;
        partial void Status_Syncbusy_Read(bool a, bool b);
        partial void Status_Syncbusy_ValueProvider(bool a);
    
        protected IFlagRegisterField status_ocbv0_bit;
        partial void Status_Ocbv0_Read(bool a, bool b);
        partial void Status_Ocbv0_ValueProvider(bool a);
    
        protected IFlagRegisterField status_ocbv1_bit;
        partial void Status_Ocbv1_Read(bool a, bool b);
        partial void Status_Ocbv1_ValueProvider(bool a);
    
        protected IFlagRegisterField status_ocbv2_bit;
        partial void Status_Ocbv2_Read(bool a, bool b);
        partial void Status_Ocbv2_ValueProvider(bool a);
    
        protected IFlagRegisterField status_icfempty0_bit;
        partial void Status_Icfempty0_Read(bool a, bool b);
        partial void Status_Icfempty0_ValueProvider(bool a);
    
        protected IFlagRegisterField status_icfempty1_bit;
        partial void Status_Icfempty1_Read(bool a, bool b);
        partial void Status_Icfempty1_ValueProvider(bool a);
    
        protected IFlagRegisterField status_icfempty2_bit;
        partial void Status_Icfempty2_Read(bool a, bool b);
        partial void Status_Icfempty2_ValueProvider(bool a);
    
        protected IEnumRegisterField<STATUS_CCPOL0> status_ccpol0_bit;
        partial void Status_Ccpol0_Read(STATUS_CCPOL0 a, STATUS_CCPOL0 b);
        partial void Status_Ccpol0_ValueProvider(STATUS_CCPOL0 a);
    
        protected IEnumRegisterField<STATUS_CCPOL1> status_ccpol1_bit;
        partial void Status_Ccpol1_Read(STATUS_CCPOL1 a, STATUS_CCPOL1 b);
        partial void Status_Ccpol1_ValueProvider(STATUS_CCPOL1 a);
    
        protected IEnumRegisterField<STATUS_CCPOL2> status_ccpol2_bit;
        partial void Status_Ccpol2_Read(STATUS_CCPOL2 a, STATUS_CCPOL2 b);
        partial void Status_Ccpol2_ValueProvider(STATUS_CCPOL2 a);
        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // If - Offset : 0x14
    
        protected IFlagRegisterField if_of_bit;
        partial void If_Of_Write(bool a, bool b);
        partial void If_Of_Read(bool a, bool b);
        partial void If_Of_ValueProvider(bool a);
    
        protected IFlagRegisterField if_uf_bit;
        partial void If_Uf_Write(bool a, bool b);
        partial void If_Uf_Read(bool a, bool b);
        partial void If_Uf_ValueProvider(bool a);
    
        protected IFlagRegisterField if_dirchg_bit;
        partial void If_Dirchg_Write(bool a, bool b);
        partial void If_Dirchg_Read(bool a, bool b);
        partial void If_Dirchg_ValueProvider(bool a);
    
        protected IFlagRegisterField if_cc0_bit;
        partial void If_Cc0_Write(bool a, bool b);
        partial void If_Cc0_Read(bool a, bool b);
        partial void If_Cc0_ValueProvider(bool a);
    
        protected IFlagRegisterField if_cc1_bit;
        partial void If_Cc1_Write(bool a, bool b);
        partial void If_Cc1_Read(bool a, bool b);
        partial void If_Cc1_ValueProvider(bool a);
    
        protected IFlagRegisterField if_cc2_bit;
        partial void If_Cc2_Write(bool a, bool b);
        partial void If_Cc2_Read(bool a, bool b);
        partial void If_Cc2_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfwlfull0_bit;
        partial void If_Icfwlfull0_Write(bool a, bool b);
        partial void If_Icfwlfull0_Read(bool a, bool b);
        partial void If_Icfwlfull0_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfwlfull1_bit;
        partial void If_Icfwlfull1_Write(bool a, bool b);
        partial void If_Icfwlfull1_Read(bool a, bool b);
        partial void If_Icfwlfull1_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfwlfull2_bit;
        partial void If_Icfwlfull2_Write(bool a, bool b);
        partial void If_Icfwlfull2_Read(bool a, bool b);
        partial void If_Icfwlfull2_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfof0_bit;
        partial void If_Icfof0_Write(bool a, bool b);
        partial void If_Icfof0_Read(bool a, bool b);
        partial void If_Icfof0_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfof1_bit;
        partial void If_Icfof1_Write(bool a, bool b);
        partial void If_Icfof1_Read(bool a, bool b);
        partial void If_Icfof1_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfof2_bit;
        partial void If_Icfof2_Write(bool a, bool b);
        partial void If_Icfof2_Read(bool a, bool b);
        partial void If_Icfof2_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfuf0_bit;
        partial void If_Icfuf0_Write(bool a, bool b);
        partial void If_Icfuf0_Read(bool a, bool b);
        partial void If_Icfuf0_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfuf1_bit;
        partial void If_Icfuf1_Write(bool a, bool b);
        partial void If_Icfuf1_Read(bool a, bool b);
        partial void If_Icfuf1_ValueProvider(bool a);
    
        protected IFlagRegisterField if_icfuf2_bit;
        partial void If_Icfuf2_Write(bool a, bool b);
        partial void If_Icfuf2_Read(bool a, bool b);
        partial void If_Icfuf2_ValueProvider(bool a);
        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x18
    
        protected IFlagRegisterField ien_of_bit;
        partial void Ien_Of_Write(bool a, bool b);
        partial void Ien_Of_Read(bool a, bool b);
        partial void Ien_Of_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_uf_bit;
        partial void Ien_Uf_Write(bool a, bool b);
        partial void Ien_Uf_Read(bool a, bool b);
        partial void Ien_Uf_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_dirchg_bit;
        partial void Ien_Dirchg_Write(bool a, bool b);
        partial void Ien_Dirchg_Read(bool a, bool b);
        partial void Ien_Dirchg_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_cc0_bit;
        partial void Ien_Cc0_Write(bool a, bool b);
        partial void Ien_Cc0_Read(bool a, bool b);
        partial void Ien_Cc0_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_cc1_bit;
        partial void Ien_Cc1_Write(bool a, bool b);
        partial void Ien_Cc1_Read(bool a, bool b);
        partial void Ien_Cc1_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_cc2_bit;
        partial void Ien_Cc2_Write(bool a, bool b);
        partial void Ien_Cc2_Read(bool a, bool b);
        partial void Ien_Cc2_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfwlfull0_bit;
        partial void Ien_Icfwlfull0_Write(bool a, bool b);
        partial void Ien_Icfwlfull0_Read(bool a, bool b);
        partial void Ien_Icfwlfull0_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfwlfull1_bit;
        partial void Ien_Icfwlfull1_Write(bool a, bool b);
        partial void Ien_Icfwlfull1_Read(bool a, bool b);
        partial void Ien_Icfwlfull1_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfwlfull2_bit;
        partial void Ien_Icfwlfull2_Write(bool a, bool b);
        partial void Ien_Icfwlfull2_Read(bool a, bool b);
        partial void Ien_Icfwlfull2_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfof0_bit;
        partial void Ien_Icfof0_Write(bool a, bool b);
        partial void Ien_Icfof0_Read(bool a, bool b);
        partial void Ien_Icfof0_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfof1_bit;
        partial void Ien_Icfof1_Write(bool a, bool b);
        partial void Ien_Icfof1_Read(bool a, bool b);
        partial void Ien_Icfof1_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfof2_bit;
        partial void Ien_Icfof2_Write(bool a, bool b);
        partial void Ien_Icfof2_Read(bool a, bool b);
        partial void Ien_Icfof2_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfuf0_bit;
        partial void Ien_Icfuf0_Write(bool a, bool b);
        partial void Ien_Icfuf0_Read(bool a, bool b);
        partial void Ien_Icfuf0_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfuf1_bit;
        partial void Ien_Icfuf1_Write(bool a, bool b);
        partial void Ien_Icfuf1_Read(bool a, bool b);
        partial void Ien_Icfuf1_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_icfuf2_bit;
        partial void Ien_Icfuf2_Write(bool a, bool b);
        partial void Ien_Icfuf2_Read(bool a, bool b);
        partial void Ien_Icfuf2_ValueProvider(bool a);
        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Top - Offset : 0x1C
    
        protected IValueRegisterField top_top_field;
        partial void Top_Top_Write(ulong a, ulong b);
        partial void Top_Top_Read(ulong a, ulong b);
        partial void Top_Top_ValueProvider(ulong a);
        partial void Top_Write(uint a, uint b);
        partial void Top_Read(uint a, uint b);
        
        // Topb - Offset : 0x20
    
        protected IValueRegisterField topb_topb_field;
        partial void Topb_Topb_Write(ulong a, ulong b);
        partial void Topb_Topb_Read(ulong a, ulong b);
        partial void Topb_Topb_ValueProvider(ulong a);
        partial void Topb_Write(uint a, uint b);
        partial void Topb_Read(uint a, uint b);
        
        // Cnt - Offset : 0x24
    
        protected IValueRegisterField cnt_cnt_field;
        partial void Cnt_Cnt_Write(ulong a, ulong b);
        partial void Cnt_Cnt_Read(ulong a, ulong b);
        partial void Cnt_Cnt_ValueProvider(ulong a);
        partial void Cnt_Write(uint a, uint b);
        partial void Cnt_Read(uint a, uint b);
        
        // Lock - Offset : 0x2C
    
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);
        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // En - Offset : 0x30
    
        protected IFlagRegisterField en_en_bit;
        partial void En_En_Write(bool a, bool b);
        partial void En_En_Read(bool a, bool b);
        partial void En_En_ValueProvider(bool a);
    
        protected IFlagRegisterField en_disabling_bit;
        partial void En_Disabling_Read(bool a, bool b);
        partial void En_Disabling_ValueProvider(bool a);
        partial void En_Write(uint a, uint b);
        partial void En_Read(uint a, uint b);
        
        // Cc0_Cfg - Offset : 0x60
    
    protected IEnumRegisterField<CC_CFG_MODE>[] cc_cfg_mode_field = new IEnumRegisterField<CC_CFG_MODE>[3];
        partial void Cc_Cfg_Mode_Write(ulong index, CC_CFG_MODE a, CC_CFG_MODE b);
        partial void Cc_Cfg_Mode_Read(ulong index, CC_CFG_MODE a, CC_CFG_MODE b);
        partial void Cc_Cfg_Mode_ValueProvider(ulong index, CC_CFG_MODE a);
    
        protected IFlagRegisterField[] cc_cfg_coist_bit = new IFlagRegisterField[3]; //test3
        partial void Cc_Cfg_Coist_Write(ulong index, bool a, bool b);
        partial void Cc_Cfg_Coist_Read(ulong index, bool a, bool b);
        partial void Cc_Cfg_Coist_ValueProvider(ulong index, bool a);
    
    protected IEnumRegisterField<CC_CFG_INSEL>[] cc_cfg_insel_field = new IEnumRegisterField<CC_CFG_INSEL>[3];
        partial void Cc_Cfg_Insel_Write(ulong index, CC_CFG_INSEL a, CC_CFG_INSEL b);
        partial void Cc_Cfg_Insel_Read(ulong index, CC_CFG_INSEL a, CC_CFG_INSEL b);
        partial void Cc_Cfg_Insel_ValueProvider(ulong index, CC_CFG_INSEL a);
    
    protected IEnumRegisterField<CC_CFG_PRSCONF>[] cc_cfg_prsconf_bit = new IEnumRegisterField<CC_CFG_PRSCONF>[3];
        partial void Cc_Cfg_Prsconf_Write(ulong index, CC_CFG_PRSCONF a, CC_CFG_PRSCONF b);
        partial void Cc_Cfg_Prsconf_Read(ulong index, CC_CFG_PRSCONF a, CC_CFG_PRSCONF b);
        partial void Cc_Cfg_Prsconf_ValueProvider(ulong index, CC_CFG_PRSCONF a);
    
    protected IEnumRegisterField<CC_CFG_FILT>[] cc_cfg_filt_bit = new IEnumRegisterField<CC_CFG_FILT>[3];
        partial void Cc_Cfg_Filt_Write(ulong index, CC_CFG_FILT a, CC_CFG_FILT b);
        partial void Cc_Cfg_Filt_Read(ulong index, CC_CFG_FILT a, CC_CFG_FILT b);
        partial void Cc_Cfg_Filt_ValueProvider(ulong index, CC_CFG_FILT a);
    
        protected IFlagRegisterField[] cc_cfg_icfwl_bit = new IFlagRegisterField[3]; //test3
        partial void Cc_Cfg_Icfwl_Write(ulong index, bool a, bool b);
        partial void Cc_Cfg_Icfwl_Read(ulong index, bool a, bool b);
        partial void Cc_Cfg_Icfwl_ValueProvider(ulong index, bool a);
        partial void Cc_Cfg_Write(ulong index, uint a, uint b);
        partial void Cc_Cfg_Read(ulong index, uint a, uint b);
        
        // Cc0_Ctrl - Offset : 0x64
    
        protected IFlagRegisterField[] cc_ctrl_outinv_bit = new IFlagRegisterField[3]; //test3
        partial void Cc_Ctrl_Outinv_Write(ulong index, bool a, bool b);
        partial void Cc_Ctrl_Outinv_Read(ulong index, bool a, bool b);
        partial void Cc_Ctrl_Outinv_ValueProvider(ulong index, bool a);
    
    protected IEnumRegisterField<CC_CTRL_CMOA>[] cc_ctrl_cmoa_field = new IEnumRegisterField<CC_CTRL_CMOA>[3];
        partial void Cc_Ctrl_Cmoa_Write(ulong index, CC_CTRL_CMOA a, CC_CTRL_CMOA b);
        partial void Cc_Ctrl_Cmoa_Read(ulong index, CC_CTRL_CMOA a, CC_CTRL_CMOA b);
        partial void Cc_Ctrl_Cmoa_ValueProvider(ulong index, CC_CTRL_CMOA a);
    
    protected IEnumRegisterField<CC_CTRL_COFOA>[] cc_ctrl_cofoa_field = new IEnumRegisterField<CC_CTRL_COFOA>[3];
        partial void Cc_Ctrl_Cofoa_Write(ulong index, CC_CTRL_COFOA a, CC_CTRL_COFOA b);
        partial void Cc_Ctrl_Cofoa_Read(ulong index, CC_CTRL_COFOA a, CC_CTRL_COFOA b);
        partial void Cc_Ctrl_Cofoa_ValueProvider(ulong index, CC_CTRL_COFOA a);
    
    protected IEnumRegisterField<CC_CTRL_CUFOA>[] cc_ctrl_cufoa_field = new IEnumRegisterField<CC_CTRL_CUFOA>[3];
        partial void Cc_Ctrl_Cufoa_Write(ulong index, CC_CTRL_CUFOA a, CC_CTRL_CUFOA b);
        partial void Cc_Ctrl_Cufoa_Read(ulong index, CC_CTRL_CUFOA a, CC_CTRL_CUFOA b);
        partial void Cc_Ctrl_Cufoa_ValueProvider(ulong index, CC_CTRL_CUFOA a);
    
    protected IEnumRegisterField<CC_CTRL_ICEDGE>[] cc_ctrl_icedge_field = new IEnumRegisterField<CC_CTRL_ICEDGE>[3];
        partial void Cc_Ctrl_Icedge_Write(ulong index, CC_CTRL_ICEDGE a, CC_CTRL_ICEDGE b);
        partial void Cc_Ctrl_Icedge_Read(ulong index, CC_CTRL_ICEDGE a, CC_CTRL_ICEDGE b);
        partial void Cc_Ctrl_Icedge_ValueProvider(ulong index, CC_CTRL_ICEDGE a);
    
    protected IEnumRegisterField<CC_CTRL_ICEVCTRL>[] cc_ctrl_icevctrl_field = new IEnumRegisterField<CC_CTRL_ICEVCTRL>[3];
        partial void Cc_Ctrl_Icevctrl_Write(ulong index, CC_CTRL_ICEVCTRL a, CC_CTRL_ICEVCTRL b);
        partial void Cc_Ctrl_Icevctrl_Read(ulong index, CC_CTRL_ICEVCTRL a, CC_CTRL_ICEVCTRL b);
        partial void Cc_Ctrl_Icevctrl_ValueProvider(ulong index, CC_CTRL_ICEVCTRL a);
        partial void Cc_Ctrl_Write(ulong index, uint a, uint b);
        partial void Cc_Ctrl_Read(ulong index, uint a, uint b);
        
        // Cc0_Oc - Offset : 0x68
    
    
        protected IValueRegisterField[] cc_oc_oc_field = new IValueRegisterField[3];
        partial void Cc_Oc_Oc_Write(ulong index, ulong a, ulong b);
        partial void Cc_Oc_Oc_Read(ulong index, ulong a, ulong b);
        partial void Cc_Oc_Oc_ValueProvider(ulong index, ulong a);
        partial void Cc_Oc_Write(ulong index, uint a, uint b);
        partial void Cc_Oc_Read(ulong index, uint a, uint b);
        
        // Cc0_Ocb - Offset : 0x70
    
    
        protected IValueRegisterField[] cc_ocb_ocb_field = new IValueRegisterField[3];
        partial void Cc_Ocb_Ocb_Write(ulong index, ulong a, ulong b);
        partial void Cc_Ocb_Ocb_Read(ulong index, ulong a, ulong b);
        partial void Cc_Ocb_Ocb_ValueProvider(ulong index, ulong a);
        partial void Cc_Ocb_Write(ulong index, uint a, uint b);
        partial void Cc_Ocb_Read(ulong index, uint a, uint b);
        
        // Cc0_Icf - Offset : 0x74
    
    
        protected IValueRegisterField[] cc_icf_icf_field = new IValueRegisterField[3];
        partial void Cc_Icf_Icf_Read(ulong index, ulong a, ulong b);
        partial void Cc_Icf_Icf_ValueProvider(ulong index, ulong a);
        partial void Cc_Icf_Write(ulong index, uint a, uint b);
        partial void Cc_Icf_Read(ulong index, uint a, uint b);
        
        // Cc0_Icof - Offset : 0x78
    
    
        protected IValueRegisterField[] cc_icof_icof_field = new IValueRegisterField[3];
        partial void Cc_Icof_Icof_Read(ulong index, ulong a, ulong b);
        partial void Cc_Icof_Icof_ValueProvider(ulong index, ulong a);
        partial void Cc_Icof_Write(ulong index, uint a, uint b);
        partial void Cc_Icof_Read(ulong index, uint a, uint b);
        
    
    
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
    
    
    
        
    
    
        
    
    
        
    
    
        
    
    
        
    
    
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
    
    
    
        
    
    
        
    
    
        
    
    
        
    
    
        
        // DtCfg - Offset : 0xE0
    
        protected IFlagRegisterField dtcfg_dten_bit;
        partial void DtCfg_Dten_Write(bool a, bool b);
        partial void DtCfg_Dten_Read(bool a, bool b);
        partial void DtCfg_Dten_ValueProvider(bool a);
    
        protected IEnumRegisterField<DTCFG_DTDAS> dtcfg_dtdas_bit;
        partial void DtCfg_Dtdas_Write(DTCFG_DTDAS a, DTCFG_DTDAS b);
        partial void DtCfg_Dtdas_Read(DTCFG_DTDAS a, DTCFG_DTDAS b);
        partial void DtCfg_Dtdas_ValueProvider(DTCFG_DTDAS a);
    
        protected IFlagRegisterField dtcfg_dtar_bit;
        partial void DtCfg_Dtar_Write(bool a, bool b);
        partial void DtCfg_Dtar_Read(bool a, bool b);
        partial void DtCfg_Dtar_ValueProvider(bool a);
    
        protected IFlagRegisterField dtcfg_dtfats_bit;
        partial void DtCfg_Dtfats_Write(bool a, bool b);
        partial void DtCfg_Dtfats_Read(bool a, bool b);
        partial void DtCfg_Dtfats_ValueProvider(bool a);
    
        protected IFlagRegisterField dtcfg_dtprsen_bit;
        partial void DtCfg_Dtprsen_Write(bool a, bool b);
        partial void DtCfg_Dtprsen_Read(bool a, bool b);
        partial void DtCfg_Dtprsen_ValueProvider(bool a);
        partial void DtCfg_Write(uint a, uint b);
        partial void DtCfg_Read(uint a, uint b);
        
        // DtTimecfg - Offset : 0xE4
    
        protected IValueRegisterField dttimecfg_dtpresc_field;
        partial void DtTimecfg_Dtpresc_Write(ulong a, ulong b);
        partial void DtTimecfg_Dtpresc_Read(ulong a, ulong b);
        partial void DtTimecfg_Dtpresc_ValueProvider(ulong a);
    
        protected IValueRegisterField dttimecfg_dtriset_field;
        partial void DtTimecfg_Dtriset_Write(ulong a, ulong b);
        partial void DtTimecfg_Dtriset_Read(ulong a, ulong b);
        partial void DtTimecfg_Dtriset_ValueProvider(ulong a);
    
        protected IValueRegisterField dttimecfg_dtfallt_field;
        partial void DtTimecfg_Dtfallt_Write(ulong a, ulong b);
        partial void DtTimecfg_Dtfallt_Read(ulong a, ulong b);
        partial void DtTimecfg_Dtfallt_ValueProvider(ulong a);
        partial void DtTimecfg_Write(uint a, uint b);
        partial void DtTimecfg_Read(uint a, uint b);
        
        // DtFcfg - Offset : 0xE8
    
        protected IEnumRegisterField<DTFCFG_DTFA> dtfcfg_dtfa_field;
        partial void DtFcfg_Dtfa_Write(DTFCFG_DTFA a, DTFCFG_DTFA b);
        partial void DtFcfg_Dtfa_Read(DTFCFG_DTFA a, DTFCFG_DTFA b);
        partial void DtFcfg_Dtfa_ValueProvider(DTFCFG_DTFA a);
    
        protected IFlagRegisterField dtfcfg_dtprs0fen_bit;
        partial void DtFcfg_Dtprs0fen_Write(bool a, bool b);
        partial void DtFcfg_Dtprs0fen_Read(bool a, bool b);
        partial void DtFcfg_Dtprs0fen_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfcfg_dtprs1fen_bit;
        partial void DtFcfg_Dtprs1fen_Write(bool a, bool b);
        partial void DtFcfg_Dtprs1fen_Read(bool a, bool b);
        partial void DtFcfg_Dtprs1fen_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfcfg_dtdbgfen_bit;
        partial void DtFcfg_Dtdbgfen_Write(bool a, bool b);
        partial void DtFcfg_Dtdbgfen_Read(bool a, bool b);
        partial void DtFcfg_Dtdbgfen_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfcfg_dtlockupfen_bit;
        partial void DtFcfg_Dtlockupfen_Write(bool a, bool b);
        partial void DtFcfg_Dtlockupfen_Read(bool a, bool b);
        partial void DtFcfg_Dtlockupfen_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfcfg_dtem23fen_bit;
        partial void DtFcfg_Dtem23fen_Write(bool a, bool b);
        partial void DtFcfg_Dtem23fen_Read(bool a, bool b);
        partial void DtFcfg_Dtem23fen_ValueProvider(bool a);
        partial void DtFcfg_Write(uint a, uint b);
        partial void DtFcfg_Read(uint a, uint b);
        
        // DtCtrl - Offset : 0xEC
    
        protected IFlagRegisterField dtctrl_dtcinv_bit;
        partial void DtCtrl_Dtcinv_Write(bool a, bool b);
        partial void DtCtrl_Dtcinv_Read(bool a, bool b);
        partial void DtCtrl_Dtcinv_ValueProvider(bool a);
    
        protected IFlagRegisterField dtctrl_dtipol_bit;
        partial void DtCtrl_Dtipol_Write(bool a, bool b);
        partial void DtCtrl_Dtipol_Read(bool a, bool b);
        partial void DtCtrl_Dtipol_ValueProvider(bool a);
        partial void DtCtrl_Write(uint a, uint b);
        partial void DtCtrl_Read(uint a, uint b);
        
        // DtOgen - Offset : 0xF0
    
        protected IFlagRegisterField dtogen_dtogcc0en_bit;
        partial void DtOgen_Dtogcc0en_Write(bool a, bool b);
        partial void DtOgen_Dtogcc0en_Read(bool a, bool b);
        partial void DtOgen_Dtogcc0en_ValueProvider(bool a);
    
        protected IFlagRegisterField dtogen_dtogcc1en_bit;
        partial void DtOgen_Dtogcc1en_Write(bool a, bool b);
        partial void DtOgen_Dtogcc1en_Read(bool a, bool b);
        partial void DtOgen_Dtogcc1en_ValueProvider(bool a);
    
        protected IFlagRegisterField dtogen_dtogcc2en_bit;
        partial void DtOgen_Dtogcc2en_Write(bool a, bool b);
        partial void DtOgen_Dtogcc2en_Read(bool a, bool b);
        partial void DtOgen_Dtogcc2en_ValueProvider(bool a);
    
        protected IFlagRegisterField dtogen_dtogcdti0en_bit;
        partial void DtOgen_Dtogcdti0en_Write(bool a, bool b);
        partial void DtOgen_Dtogcdti0en_Read(bool a, bool b);
        partial void DtOgen_Dtogcdti0en_ValueProvider(bool a);
    
        protected IFlagRegisterField dtogen_dtogcdti1en_bit;
        partial void DtOgen_Dtogcdti1en_Write(bool a, bool b);
        partial void DtOgen_Dtogcdti1en_Read(bool a, bool b);
        partial void DtOgen_Dtogcdti1en_ValueProvider(bool a);
    
        protected IFlagRegisterField dtogen_dtogcdti2en_bit;
        partial void DtOgen_Dtogcdti2en_Write(bool a, bool b);
        partial void DtOgen_Dtogcdti2en_Read(bool a, bool b);
        partial void DtOgen_Dtogcdti2en_ValueProvider(bool a);
        partial void DtOgen_Write(uint a, uint b);
        partial void DtOgen_Read(uint a, uint b);
        
        // DtFault - Offset : 0xF4
    
        protected IFlagRegisterField dtfault_dtprs0f_bit;
        partial void DtFault_Dtprs0f_Read(bool a, bool b);
        partial void DtFault_Dtprs0f_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfault_dtprs1f_bit;
        partial void DtFault_Dtprs1f_Read(bool a, bool b);
        partial void DtFault_Dtprs1f_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfault_dtdbgf_bit;
        partial void DtFault_Dtdbgf_Read(bool a, bool b);
        partial void DtFault_Dtdbgf_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfault_dtlockupf_bit;
        partial void DtFault_Dtlockupf_Read(bool a, bool b);
        partial void DtFault_Dtlockupf_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfault_dtem23f_bit;
        partial void DtFault_Dtem23f_Read(bool a, bool b);
        partial void DtFault_Dtem23f_ValueProvider(bool a);
        partial void DtFault_Write(uint a, uint b);
        partial void DtFault_Read(uint a, uint b);
        
        // DtFaultc - Offset : 0xF8
    
        protected IFlagRegisterField dtfaultc_dtprs0fc_bit;
        partial void DtFaultc_Dtprs0fc_Write(bool a, bool b);
        partial void DtFaultc_Dtprs0fc_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfaultc_dtprs1fc_bit;
        partial void DtFaultc_Dtprs1fc_Write(bool a, bool b);
        partial void DtFaultc_Dtprs1fc_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfaultc_dtdbgfc_bit;
        partial void DtFaultc_Dtdbgfc_Write(bool a, bool b);
        partial void DtFaultc_Dtdbgfc_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfaultc_dtlockupfc_bit;
        partial void DtFaultc_Dtlockupfc_Write(bool a, bool b);
        partial void DtFaultc_Dtlockupfc_ValueProvider(bool a);
    
        protected IFlagRegisterField dtfaultc_dtem23fc_bit;
        partial void DtFaultc_Dtem23fc_Write(bool a, bool b);
        partial void DtFaultc_Dtem23fc_ValueProvider(bool a);
        partial void DtFaultc_Write(uint a, uint b);
        partial void DtFaultc_Read(uint a, uint b);
        
        // DtLock - Offset : 0xFC
    
        protected IEnumRegisterField<DTLOCK_DTILOCKKEY> dtlock_dtilockkey_field;
        partial void DtLock_Dtilockkey_Write(DTLOCK_DTILOCKKEY a, DTLOCK_DTILOCKKEY b);
        partial void DtLock_Dtilockkey_ValueProvider(DTLOCK_DTILOCKKEY a);
        partial void DtLock_Write(uint a, uint b);
        partial void DtLock_Read(uint a, uint b);
        partial void TIMER_Reset();

        partial void EFR32xG2_TIMER_1_Constructor();

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
            Cfg = 0x4,
            Ctrl = 0x8,
            Cmd = 0xC,
            Status = 0x10,
            If = 0x14,
            Ien = 0x18,
            Top = 0x1C,
            Topb = 0x20,
            Cnt = 0x24,
            Lock = 0x2C,
            En = 0x30,
            Cc0_Cfg = 0x60,
            Cc0_Ctrl = 0x64,
            Cc0_Oc = 0x68,
            Cc0_Ocb = 0x70,
            Cc0_Icf = 0x74,
            Cc0_Icof = 0x78,
            Cc1_Cfg = 0x80,
            Cc1_Ctrl = 0x84,
            Cc1_Oc = 0x88,
            Cc1_Ocb = 0x90,
            Cc1_Icf = 0x94,
            Cc1_Icof = 0x98,
            Cc2_Cfg = 0xA0,
            Cc2_Ctrl = 0xA4,
            Cc2_Oc = 0xA8,
            Cc2_Ocb = 0xB0,
            Cc2_Icf = 0xB4,
            Cc2_Icof = 0xB8,
            DtCfg = 0xE0,
            DtTimecfg = 0xE4,
            DtFcfg = 0xE8,
            DtCtrl = 0xEC,
            DtOgen = 0xF0,
            DtFault = 0xF4,
            DtFaultc = 0xF8,
            DtLock = 0xFC,
            
            Ipversion_SET = 0x1000,
            Cfg_SET = 0x1004,
            Ctrl_SET = 0x1008,
            Cmd_SET = 0x100C,
            Status_SET = 0x1010,
            If_SET = 0x1014,
            Ien_SET = 0x1018,
            Top_SET = 0x101C,
            Topb_SET = 0x1020,
            Cnt_SET = 0x1024,
            Lock_SET = 0x102C,
            En_SET = 0x1030,
            Cc0_Cfg_SET = 0x1060,
            Cc0_Ctrl_SET = 0x1064,
            Cc0_Oc_SET = 0x1068,
            Cc0_Ocb_SET = 0x1070,
            Cc0_Icf_SET = 0x1074,
            Cc0_Icof_SET = 0x1078,
            Cc1_Cfg_SET = 0x1080,
            Cc1_Ctrl_SET = 0x1084,
            Cc1_Oc_SET = 0x1088,
            Cc1_Ocb_SET = 0x1090,
            Cc1_Icf_SET = 0x1094,
            Cc1_Icof_SET = 0x1098,
            Cc2_Cfg_SET = 0x10A0,
            Cc2_Ctrl_SET = 0x10A4,
            Cc2_Oc_SET = 0x10A8,
            Cc2_Ocb_SET = 0x10B0,
            Cc2_Icf_SET = 0x10B4,
            Cc2_Icof_SET = 0x10B8,
            DtCfg_SET = 0x10E0,
            DtTimecfg_SET = 0x10E4,
            DtFcfg_SET = 0x10E8,
            DtCtrl_SET = 0x10EC,
            DtOgen_SET = 0x10F0,
            DtFault_SET = 0x10F4,
            DtFaultc_SET = 0x10F8,
            DtLock_SET = 0x10FC,
            
            Ipversion_CLR = 0x2000,
            Cfg_CLR = 0x2004,
            Ctrl_CLR = 0x2008,
            Cmd_CLR = 0x200C,
            Status_CLR = 0x2010,
            If_CLR = 0x2014,
            Ien_CLR = 0x2018,
            Top_CLR = 0x201C,
            Topb_CLR = 0x2020,
            Cnt_CLR = 0x2024,
            Lock_CLR = 0x202C,
            En_CLR = 0x2030,
            Cc0_Cfg_CLR = 0x2060,
            Cc0_Ctrl_CLR = 0x2064,
            Cc0_Oc_CLR = 0x2068,
            Cc0_Ocb_CLR = 0x2070,
            Cc0_Icf_CLR = 0x2074,
            Cc0_Icof_CLR = 0x2078,
            Cc1_Cfg_CLR = 0x2080,
            Cc1_Ctrl_CLR = 0x2084,
            Cc1_Oc_CLR = 0x2088,
            Cc1_Ocb_CLR = 0x2090,
            Cc1_Icf_CLR = 0x2094,
            Cc1_Icof_CLR = 0x2098,
            Cc2_Cfg_CLR = 0x20A0,
            Cc2_Ctrl_CLR = 0x20A4,
            Cc2_Oc_CLR = 0x20A8,
            Cc2_Ocb_CLR = 0x20B0,
            Cc2_Icf_CLR = 0x20B4,
            Cc2_Icof_CLR = 0x20B8,
            DtCfg_CLR = 0x20E0,
            DtTimecfg_CLR = 0x20E4,
            DtFcfg_CLR = 0x20E8,
            DtCtrl_CLR = 0x20EC,
            DtOgen_CLR = 0x20F0,
            DtFault_CLR = 0x20F4,
            DtFaultc_CLR = 0x20F8,
            DtLock_CLR = 0x20FC,
            
            Ipversion_TGL = 0x3000,
            Cfg_TGL = 0x3004,
            Ctrl_TGL = 0x3008,
            Cmd_TGL = 0x300C,
            Status_TGL = 0x3010,
            If_TGL = 0x3014,
            Ien_TGL = 0x3018,
            Top_TGL = 0x301C,
            Topb_TGL = 0x3020,
            Cnt_TGL = 0x3024,
            Lock_TGL = 0x302C,
            En_TGL = 0x3030,
            Cc0_Cfg_TGL = 0x3060,
            Cc0_Ctrl_TGL = 0x3064,
            Cc0_Oc_TGL = 0x3068,
            Cc0_Ocb_TGL = 0x3070,
            Cc0_Icf_TGL = 0x3074,
            Cc0_Icof_TGL = 0x3078,
            Cc1_Cfg_TGL = 0x3080,
            Cc1_Ctrl_TGL = 0x3084,
            Cc1_Oc_TGL = 0x3088,
            Cc1_Ocb_TGL = 0x3090,
            Cc1_Icf_TGL = 0x3094,
            Cc1_Icof_TGL = 0x3098,
            Cc2_Cfg_TGL = 0x30A0,
            Cc2_Ctrl_TGL = 0x30A4,
            Cc2_Oc_TGL = 0x30A8,
            Cc2_Ocb_TGL = 0x30B0,
            Cc2_Icf_TGL = 0x30B4,
            Cc2_Icof_TGL = 0x30B8,
            DtCfg_TGL = 0x30E0,
            DtTimecfg_TGL = 0x30E4,
            DtFcfg_TGL = 0x30E8,
            DtCtrl_TGL = 0x30EC,
            DtOgen_TGL = 0x30F0,
            DtFault_TGL = 0x30F4,
            DtFaultc_TGL = 0x30F8,
            DtLock_TGL = 0x30FC,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}