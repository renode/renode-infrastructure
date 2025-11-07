//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    PRS, Generated on : 2025-07-21 18:36:31.957909
    PRS, ID Version : 79a781f6f5f142b494139694536aeeb2.1 */

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
using Antmicro.Renode.Peripherals.Wireless;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public partial class SiLabs_PRS_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Enable, GenerateEnableRegister()},
                {(long)Registers.Async_swpulse, GenerateAsync_swpulseRegister()},
                {(long)Registers.Async_swlevel, GenerateAsync_swlevelRegister()},
                {(long)Registers.Async_peek, GenerateAsync_peekRegister()},
                {(long)Registers.Sync_peek, GenerateSync_peekRegister()},
                {(long)Registers.Async_Ch0_Ctrl, GenerateAsync_ch0_ctrlRegister()},
                {(long)Registers.Async_Ch1_Ctrl, GenerateAsync_ch1_ctrlRegister()},
                {(long)Registers.Async_Ch2_Ctrl, GenerateAsync_ch2_ctrlRegister()},
                {(long)Registers.Async_Ch3_Ctrl, GenerateAsync_ch3_ctrlRegister()},
                {(long)Registers.Async_Ch4_Ctrl, GenerateAsync_ch4_ctrlRegister()},
                {(long)Registers.Async_Ch5_Ctrl, GenerateAsync_ch5_ctrlRegister()},
                {(long)Registers.Async_Ch6_Ctrl, GenerateAsync_ch6_ctrlRegister()},
                {(long)Registers.Async_Ch7_Ctrl, GenerateAsync_ch7_ctrlRegister()},
                {(long)Registers.Async_Ch8_Ctrl, GenerateAsync_ch8_ctrlRegister()},
                {(long)Registers.Async_Ch9_Ctrl, GenerateAsync_ch9_ctrlRegister()},
                {(long)Registers.Async_Ch10_Ctrl, GenerateAsync_ch10_ctrlRegister()},
                {(long)Registers.Async_Ch11_Ctrl, GenerateAsync_ch11_ctrlRegister()},
                {(long)Registers.Sync_Ch0_Ctrl, GenerateSync_ch0_ctrlRegister()},
                {(long)Registers.Sync_Ch1_Ctrl, GenerateSync_ch1_ctrlRegister()},
                {(long)Registers.Sync_Ch2_Ctrl, GenerateSync_ch2_ctrlRegister()},
                {(long)Registers.Sync_Ch3_Ctrl, GenerateSync_ch3_ctrlRegister()},
                {(long)Registers.Consumer_Cmu_caldn, GenerateConsumer_cmu_caldnRegister()},
                {(long)Registers.Consumer_Cmu_calup, GenerateConsumer_cmu_calupRegister()},
                {(long)Registers.Consumer_Frc_rxraw, GenerateConsumer_frc_rxrawRegister()},
                {(long)Registers.Consumer_Iadc0_scantrigger, GenerateConsumer_iadc0_scantriggerRegister()},
                {(long)Registers.Consumer_Iadc0_singletrigger, GenerateConsumer_iadc0_singletriggerRegister()},
                {(long)Registers.Consumer_Ldmaxbar_dmareq0, GenerateConsumer_ldmaxbar_dmareq0Register()},
                {(long)Registers.Consumer_Ldmaxbar_dmareq1, GenerateConsumer_ldmaxbar_dmareq1Register()},
                {(long)Registers.Consumer_letimer0_Clear, GenerateConsumer_letimer0_clearRegister()},
                {(long)Registers.Consumer_letimer0_Start, GenerateConsumer_letimer0_startRegister()},
                {(long)Registers.Consumer_letimer0_Stop, GenerateConsumer_letimer0_stopRegister()},
                {(long)Registers.Consumer_euart0_Rx, GenerateConsumer_euart0_rxRegister()},
                {(long)Registers.Consumer_euart0_Trigger, GenerateConsumer_euart0_triggerRegister()},
                {(long)Registers.Consumer_Modem_din, GenerateConsumer_modem_dinRegister()},
                {(long)Registers.Consumer_Prortc_cc0, GenerateConsumer_prortc_cc0Register()},
                {(long)Registers.Consumer_Prortc_cc1, GenerateConsumer_prortc_cc1Register()},
                {(long)Registers.Consumer_Protimer_cc0, GenerateConsumer_protimer_cc0Register()},
                {(long)Registers.Consumer_Protimer_cc1, GenerateConsumer_protimer_cc1Register()},
                {(long)Registers.Consumer_Protimer_cc2, GenerateConsumer_protimer_cc2Register()},
                {(long)Registers.Consumer_Protimer_cc3, GenerateConsumer_protimer_cc3Register()},
                {(long)Registers.Consumer_Protimer_cc4, GenerateConsumer_protimer_cc4Register()},
                {(long)Registers.Consumer_Protimer_lbtpause, GenerateConsumer_protimer_lbtpauseRegister()},
                {(long)Registers.Consumer_Protimer_lbtstart, GenerateConsumer_protimer_lbtstartRegister()},
                {(long)Registers.Consumer_Protimer_lbtstop, GenerateConsumer_protimer_lbtstopRegister()},
                {(long)Registers.Consumer_Protimer_rtcctrigger, GenerateConsumer_protimer_rtcctriggerRegister()},
                {(long)Registers.Consumer_Protimer_start, GenerateConsumer_protimer_startRegister()},
                {(long)Registers.Consumer_Protimer_stop, GenerateConsumer_protimer_stopRegister()},
                {(long)Registers.Consumer_Rac_clr, GenerateConsumer_rac_clrRegister()},
                {(long)Registers.Consumer_Rac_ctiin0, GenerateConsumer_rac_ctiin0Register()},
                {(long)Registers.Consumer_Rac_ctiin1, GenerateConsumer_rac_ctiin1Register()},
                {(long)Registers.Consumer_Rac_ctiin2, GenerateConsumer_rac_ctiin2Register()},
                {(long)Registers.Consumer_Rac_ctiin3, GenerateConsumer_rac_ctiin3Register()},
                {(long)Registers.Consumer_Rac_forcetx, GenerateConsumer_rac_forcetxRegister()},
                {(long)Registers.Consumer_Rac_rxdis, GenerateConsumer_rac_rxdisRegister()},
                {(long)Registers.Consumer_Rac_rxen, GenerateConsumer_rac_rxenRegister()},
                {(long)Registers.Consumer_Rac_seq, GenerateConsumer_rac_seqRegister()},
                {(long)Registers.Consumer_Rac_txen, GenerateConsumer_rac_txenRegister()},
                {(long)Registers.Consumer_Rtcc_cc0, GenerateConsumer_rtcc_cc0Register()},
                {(long)Registers.Consumer_Rtcc_cc1, GenerateConsumer_rtcc_cc1Register()},
                {(long)Registers.Consumer_Rtcc_cc2, GenerateConsumer_rtcc_cc2Register()},
                {(long)Registers.Consumer_Core_ctiin0, GenerateConsumer_core_ctiin0Register()},
                {(long)Registers.Consumer_Core_ctiin1, GenerateConsumer_core_ctiin1Register()},
                {(long)Registers.Consumer_Core_ctiin2, GenerateConsumer_core_ctiin2Register()},
                {(long)Registers.Consumer_Core_ctiin3, GenerateConsumer_core_ctiin3Register()},
                {(long)Registers.Consumer_Core_m33rxev, GenerateConsumer_core_m33rxevRegister()},
                {(long)Registers.Consumer_Timer0_cc0, GenerateConsumer_timer0_cc0Register()},
                {(long)Registers.Consumer_Timer0_cc1, GenerateConsumer_timer0_cc1Register()},
                {(long)Registers.Consumer_Timer0_cc2, GenerateConsumer_timer0_cc2Register()},
                {(long)Registers.Consumer_Timer0_dti, GenerateConsumer_timer0_dtiRegister()},
                {(long)Registers.Consumer_Timer0_dtifs1, GenerateConsumer_timer0_dtifs1Register()},
                {(long)Registers.Consumer_Timer0_dtifs2, GenerateConsumer_timer0_dtifs2Register()},
                {(long)Registers.Consumer_Timer1_cc0, GenerateConsumer_timer1_cc0Register()},
                {(long)Registers.Consumer_Timer1_cc1, GenerateConsumer_timer1_cc1Register()},
                {(long)Registers.Consumer_Timer1_cc2, GenerateConsumer_timer1_cc2Register()},
                {(long)Registers.Consumer_Timer1_dti, GenerateConsumer_timer1_dtiRegister()},
                {(long)Registers.Consumer_Timer1_dtifs1, GenerateConsumer_timer1_dtifs1Register()},
                {(long)Registers.Consumer_Timer1_dtifs2, GenerateConsumer_timer1_dtifs2Register()},
                {(long)Registers.Consumer_Timer2_cc0, GenerateConsumer_timer2_cc0Register()},
                {(long)Registers.Consumer_Timer2_cc1, GenerateConsumer_timer2_cc1Register()},
                {(long)Registers.Consumer_Timer2_cc2, GenerateConsumer_timer2_cc2Register()},
                {(long)Registers.Consumer_Timer2_dti, GenerateConsumer_timer2_dtiRegister()},
                {(long)Registers.Consumer_Timer2_dtifs1, GenerateConsumer_timer2_dtifs1Register()},
                {(long)Registers.Consumer_Timer2_dtifs2, GenerateConsumer_timer2_dtifs2Register()},
                {(long)Registers.Consumer_Timer3_cc0, GenerateConsumer_timer3_cc0Register()},
                {(long)Registers.Consumer_Timer3_cc1, GenerateConsumer_timer3_cc1Register()},
                {(long)Registers.Consumer_Timer3_cc2, GenerateConsumer_timer3_cc2Register()},
                {(long)Registers.Consumer_Timer3_dti, GenerateConsumer_timer3_dtiRegister()},
                {(long)Registers.Consumer_Timer3_dtifs1, GenerateConsumer_timer3_dtifs1Register()},
                {(long)Registers.Consumer_Timer3_dtifs2, GenerateConsumer_timer3_dtifs2Register()},
                {(long)Registers.Consumer_Timer4_cc0, GenerateConsumer_timer4_cc0Register()},
                {(long)Registers.Consumer_Timer4_cc1, GenerateConsumer_timer4_cc1Register()},
                {(long)Registers.Consumer_Timer4_cc2, GenerateConsumer_timer4_cc2Register()},
                {(long)Registers.Consumer_Timer4_dti, GenerateConsumer_timer4_dtiRegister()},
                {(long)Registers.Consumer_Timer4_dtifs1, GenerateConsumer_timer4_dtifs1Register()},
                {(long)Registers.Consumer_Timer4_dtifs2, GenerateConsumer_timer4_dtifs2Register()},
                {(long)Registers.Consumer_Usart0_clk, GenerateConsumer_usart0_clkRegister()},
                {(long)Registers.Consumer_Usart0_ir, GenerateConsumer_usart0_irRegister()},
                {(long)Registers.Consumer_Usart0_rx, GenerateConsumer_usart0_rxRegister()},
                {(long)Registers.Consumer_Usart0_trigger, GenerateConsumer_usart0_triggerRegister()},
                {(long)Registers.Consumer_Usart1_clk, GenerateConsumer_usart1_clkRegister()},
                {(long)Registers.Consumer_Usart1_ir, GenerateConsumer_usart1_irRegister()},
                {(long)Registers.Consumer_Usart1_rx, GenerateConsumer_usart1_rxRegister()},
                {(long)Registers.Consumer_Usart1_trigger, GenerateConsumer_usart1_triggerRegister()},
                {(long)Registers.Consumer_Wdog0_src0, GenerateConsumer_wdog0_src0Register()},
                {(long)Registers.Consumer_Wdog0_src1, GenerateConsumer_wdog0_src1Register()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            PRS_Reset();
        }
        
        protected enum ASYNC_CH_CTRL_SIGSEL
        {
            NONE = 0, // 
        }
        
        protected enum ASYNC_CH_CTRL_FNSEL
        {
            LOGICAL_ZERO = 0, // Logical 0
            A_NOR_B = 1, // A NOR B
            NOT_A_AND_B = 2, // (!A) AND B
            NOT_A = 3, // !A
            A_AND_NOT_B = 4, // A AND (!B)
            NOT_B = 5, // !B
            A_XOR_B = 6, // A XOR B
            A_NAND_B = 7, // A NAND B
            A_AND_B = 8, // A AND B
            A_XNOR_B = 9, // A XNOR B
            B = 10, // B
            NOT_A_OR_B = 11, // (!A) OR B
            A = 12, // A
            A_OR_NOT_B = 13, // A OR (!B)
            A_OR_B = 14, // A OR B
            LOGICAL_ONE = 15, // Logical 1
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
        
        // Enable - Offset : 0x4
        protected DoubleWordRegister GenerateEnableRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out enable_em23dis_bit, 
                    valueProviderCallback: (_) => {
                        Enable_Em23dis_ValueProvider(_);
                        return enable_em23dis_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Enable_Em23dis_Write(_, __),
                    
                    readCallback: (_, __) => Enable_Em23dis_Read(_, __),
                    name: "Em23dis")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Enable_Read(_, __))
            .WithWriteCallback((_, __) => Enable_Write(_, __));
        
        // Async_swpulse - Offset : 0x8
        protected DoubleWordRegister GenerateAsync_swpulseRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out async_swpulse_ch0pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch0pulse_Write(_, __),
                    name: "Ch0pulse")
            .WithFlag(1, out async_swpulse_ch1pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch1pulse_Write(_, __),
                    name: "Ch1pulse")
            .WithFlag(2, out async_swpulse_ch2pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch2pulse_Write(_, __),
                    name: "Ch2pulse")
            .WithFlag(3, out async_swpulse_ch3pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch3pulse_Write(_, __),
                    name: "Ch3pulse")
            .WithFlag(4, out async_swpulse_ch4pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch4pulse_Write(_, __),
                    name: "Ch4pulse")
            .WithFlag(5, out async_swpulse_ch5pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch5pulse_Write(_, __),
                    name: "Ch5pulse")
            .WithFlag(6, out async_swpulse_ch6pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch6pulse_Write(_, __),
                    name: "Ch6pulse")
            .WithFlag(7, out async_swpulse_ch7pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch7pulse_Write(_, __),
                    name: "Ch7pulse")
            .WithFlag(8, out async_swpulse_ch8pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch8pulse_Write(_, __),
                    name: "Ch8pulse")
            .WithFlag(9, out async_swpulse_ch9pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch9pulse_Write(_, __),
                    name: "Ch9pulse")
            .WithFlag(10, out async_swpulse_ch10pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch10pulse_Write(_, __),
                    name: "Ch10pulse")
            .WithFlag(11, out async_swpulse_ch11pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch11pulse_Write(_, __),
                    name: "Ch11pulse")
            .WithReservedBits(12, 20)
            .WithReadCallback((_, __) => Async_swpulse_Read(_, __))
            .WithWriteCallback((_, __) => Async_swpulse_Write(_, __));
        
        // Async_swlevel - Offset : 0xC
        protected DoubleWordRegister GenerateAsync_swlevelRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out async_swlevel_ch0level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch0level_ValueProvider(_);
                        return async_swlevel_ch0level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch0level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch0level_Read(_, __),
                    name: "Ch0level")
            .WithFlag(1, out async_swlevel_ch1level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch1level_ValueProvider(_);
                        return async_swlevel_ch1level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch1level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch1level_Read(_, __),
                    name: "Ch1level")
            .WithFlag(2, out async_swlevel_ch2level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch2level_ValueProvider(_);
                        return async_swlevel_ch2level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch2level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch2level_Read(_, __),
                    name: "Ch2level")
            .WithFlag(3, out async_swlevel_ch3level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch3level_ValueProvider(_);
                        return async_swlevel_ch3level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch3level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch3level_Read(_, __),
                    name: "Ch3level")
            .WithFlag(4, out async_swlevel_ch4level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch4level_ValueProvider(_);
                        return async_swlevel_ch4level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch4level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch4level_Read(_, __),
                    name: "Ch4level")
            .WithFlag(5, out async_swlevel_ch5level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch5level_ValueProvider(_);
                        return async_swlevel_ch5level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch5level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch5level_Read(_, __),
                    name: "Ch5level")
            .WithFlag(6, out async_swlevel_ch6level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch6level_ValueProvider(_);
                        return async_swlevel_ch6level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch6level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch6level_Read(_, __),
                    name: "Ch6level")
            .WithFlag(7, out async_swlevel_ch7level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch7level_ValueProvider(_);
                        return async_swlevel_ch7level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch7level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch7level_Read(_, __),
                    name: "Ch7level")
            .WithFlag(8, out async_swlevel_ch8level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch8level_ValueProvider(_);
                        return async_swlevel_ch8level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch8level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch8level_Read(_, __),
                    name: "Ch8level")
            .WithFlag(9, out async_swlevel_ch9level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch9level_ValueProvider(_);
                        return async_swlevel_ch9level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch9level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch9level_Read(_, __),
                    name: "Ch9level")
            .WithFlag(10, out async_swlevel_ch10level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch10level_ValueProvider(_);
                        return async_swlevel_ch10level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch10level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch10level_Read(_, __),
                    name: "Ch10level")
            .WithFlag(11, out async_swlevel_ch11level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch11level_ValueProvider(_);
                        return async_swlevel_ch11level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch11level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch11level_Read(_, __),
                    name: "Ch11level")
            .WithReservedBits(12, 20)
            .WithReadCallback((_, __) => Async_swlevel_Read(_, __))
            .WithWriteCallback((_, __) => Async_swlevel_Write(_, __));
        
        // Async_peek - Offset : 0x10
        protected DoubleWordRegister GenerateAsync_peekRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out async_peek_ch0val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch0val_ValueProvider(_);
                        return async_peek_ch0val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch0val_Read(_, __),
                    name: "Ch0val")
            .WithFlag(1, out async_peek_ch1val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch1val_ValueProvider(_);
                        return async_peek_ch1val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch1val_Read(_, __),
                    name: "Ch1val")
            .WithFlag(2, out async_peek_ch2val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch2val_ValueProvider(_);
                        return async_peek_ch2val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch2val_Read(_, __),
                    name: "Ch2val")
            .WithFlag(3, out async_peek_ch3val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch3val_ValueProvider(_);
                        return async_peek_ch3val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch3val_Read(_, __),
                    name: "Ch3val")
            .WithFlag(4, out async_peek_ch4val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch4val_ValueProvider(_);
                        return async_peek_ch4val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch4val_Read(_, __),
                    name: "Ch4val")
            .WithFlag(5, out async_peek_ch5val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch5val_ValueProvider(_);
                        return async_peek_ch5val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch5val_Read(_, __),
                    name: "Ch5val")
            .WithFlag(6, out async_peek_ch6val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch6val_ValueProvider(_);
                        return async_peek_ch6val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch6val_Read(_, __),
                    name: "Ch6val")
            .WithFlag(7, out async_peek_ch7val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch7val_ValueProvider(_);
                        return async_peek_ch7val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch7val_Read(_, __),
                    name: "Ch7val")
            .WithFlag(8, out async_peek_ch8val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch8val_ValueProvider(_);
                        return async_peek_ch8val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch8val_Read(_, __),
                    name: "Ch8val")
            .WithFlag(9, out async_peek_ch9val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch9val_ValueProvider(_);
                        return async_peek_ch9val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch9val_Read(_, __),
                    name: "Ch9val")
            .WithFlag(10, out async_peek_ch10val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch10val_ValueProvider(_);
                        return async_peek_ch10val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch10val_Read(_, __),
                    name: "Ch10val")
            .WithFlag(11, out async_peek_ch11val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch11val_ValueProvider(_);
                        return async_peek_ch11val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch11val_Read(_, __),
                    name: "Ch11val")
            .WithReservedBits(12, 20)
            .WithReadCallback((_, __) => Async_peek_Read(_, __))
            .WithWriteCallback((_, __) => Async_peek_Write(_, __));
        
        // Sync_peek - Offset : 0x14
        protected DoubleWordRegister GenerateSync_peekRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out sync_peek_ch0val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sync_peek_Ch0val_ValueProvider(_);
                        return sync_peek_ch0val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Sync_peek_Ch0val_Read(_, __),
                    name: "Ch0val")
            .WithFlag(1, out sync_peek_ch1val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sync_peek_Ch1val_ValueProvider(_);
                        return sync_peek_ch1val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Sync_peek_Ch1val_Read(_, __),
                    name: "Ch1val")
            .WithFlag(2, out sync_peek_ch2val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sync_peek_Ch2val_ValueProvider(_);
                        return sync_peek_ch2val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Sync_peek_Ch2val_Read(_, __),
                    name: "Ch2val")
            .WithFlag(3, out sync_peek_ch3val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sync_peek_Ch3val_ValueProvider(_);
                        return sync_peek_ch3val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Sync_peek_Ch3val_Read(_, __),
                    name: "Ch3val")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Sync_peek_Read(_, __))
            .WithWriteCallback((_, __) => Sync_peek_Write(_, __));
        
        // Async_Ch0_Ctrl - Offset : 0x18
        protected DoubleWordRegister GenerateAsync_ch0_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[0], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(0, _);
                        return async_ch_ctrl_sigsel_field[0].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(0,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(0,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[0], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(0, _);
                        return async_ch_ctrl_sourcesel_field[0].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(0,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(0,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[0], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(0, _);
                        return async_ch_ctrl_fnsel_field[0].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(0,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(0,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[0], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(0, _);
                        return async_ch_ctrl_auxsel_field[0].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(0,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(0,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(0, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(0, _, __));
        
        // Async_Ch1_Ctrl - Offset : 0x1C
        protected DoubleWordRegister GenerateAsync_ch1_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[1], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(1, _);
                        return async_ch_ctrl_sigsel_field[1].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(1,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(1,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[1], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(1, _);
                        return async_ch_ctrl_sourcesel_field[1].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(1,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(1,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[1], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(1, _);
                        return async_ch_ctrl_fnsel_field[1].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(1,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(1,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[1], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(1, _);
                        return async_ch_ctrl_auxsel_field[1].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(1,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(1,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(1, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(1, _, __));
        
        // Async_Ch2_Ctrl - Offset : 0x20
        protected DoubleWordRegister GenerateAsync_ch2_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[2], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(2, _);
                        return async_ch_ctrl_sigsel_field[2].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(2,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(2,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[2], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(2, _);
                        return async_ch_ctrl_sourcesel_field[2].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(2,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(2,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[2], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(2, _);
                        return async_ch_ctrl_fnsel_field[2].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(2,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(2,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[2], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(2, _);
                        return async_ch_ctrl_auxsel_field[2].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(2,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(2,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(2, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(2, _, __));
        
        // Async_Ch3_Ctrl - Offset : 0x24
        protected DoubleWordRegister GenerateAsync_ch3_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[3], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(3, _);
                        return async_ch_ctrl_sigsel_field[3].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(3,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(3,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[3], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(3, _);
                        return async_ch_ctrl_sourcesel_field[3].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(3,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(3,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[3], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(3, _);
                        return async_ch_ctrl_fnsel_field[3].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(3,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(3,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[3], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(3, _);
                        return async_ch_ctrl_auxsel_field[3].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(3,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(3,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(3, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(3, _, __));
        
        // Async_Ch4_Ctrl - Offset : 0x28
        protected DoubleWordRegister GenerateAsync_ch4_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[4], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(4, _);
                        return async_ch_ctrl_sigsel_field[4].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(4,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(4,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[4], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(4, _);
                        return async_ch_ctrl_sourcesel_field[4].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(4,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(4,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[4], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(4, _);
                        return async_ch_ctrl_fnsel_field[4].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(4,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(4,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[4], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(4, _);
                        return async_ch_ctrl_auxsel_field[4].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(4,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(4,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(4, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(4, _, __));
        
        // Async_Ch5_Ctrl - Offset : 0x2C
        protected DoubleWordRegister GenerateAsync_ch5_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[5], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(5, _);
                        return async_ch_ctrl_sigsel_field[5].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(5,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(5,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[5], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(5, _);
                        return async_ch_ctrl_sourcesel_field[5].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(5,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(5,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[5], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(5, _);
                        return async_ch_ctrl_fnsel_field[5].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(5,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(5,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[5], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(5, _);
                        return async_ch_ctrl_auxsel_field[5].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(5,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(5,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(5, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(5, _, __));
        
        // Async_Ch6_Ctrl - Offset : 0x30
        protected DoubleWordRegister GenerateAsync_ch6_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[6], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(6, _);
                        return async_ch_ctrl_sigsel_field[6].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(6,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(6,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[6], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(6, _);
                        return async_ch_ctrl_sourcesel_field[6].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(6,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(6,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[6], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(6, _);
                        return async_ch_ctrl_fnsel_field[6].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(6,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(6,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[6], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(6, _);
                        return async_ch_ctrl_auxsel_field[6].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(6,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(6,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(6, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(6, _, __));
        
        // Async_Ch7_Ctrl - Offset : 0x34
        protected DoubleWordRegister GenerateAsync_ch7_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[7], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(7, _);
                        return async_ch_ctrl_sigsel_field[7].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(7,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(7,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[7], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(7, _);
                        return async_ch_ctrl_sourcesel_field[7].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(7,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(7,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[7], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(7, _);
                        return async_ch_ctrl_fnsel_field[7].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(7,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(7,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[7], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(7, _);
                        return async_ch_ctrl_auxsel_field[7].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(7,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(7,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(7, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(7, _, __));
        
        // Async_Ch8_Ctrl - Offset : 0x38
        protected DoubleWordRegister GenerateAsync_ch8_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[8], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(8, _);
                        return async_ch_ctrl_sigsel_field[8].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(8,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(8,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[8], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(8, _);
                        return async_ch_ctrl_sourcesel_field[8].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(8,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(8,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[8], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(8, _);
                        return async_ch_ctrl_fnsel_field[8].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(8,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(8,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[8], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(8, _);
                        return async_ch_ctrl_auxsel_field[8].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(8,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(8,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(8, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(8, _, __));
        
        // Async_Ch9_Ctrl - Offset : 0x3C
        protected DoubleWordRegister GenerateAsync_ch9_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[9], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(9, _);
                        return async_ch_ctrl_sigsel_field[9].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(9,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(9,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[9], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(9, _);
                        return async_ch_ctrl_sourcesel_field[9].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(9,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(9,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[9], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(9, _);
                        return async_ch_ctrl_fnsel_field[9].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(9,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(9,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[9], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(9, _);
                        return async_ch_ctrl_auxsel_field[9].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(9,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(9,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(9, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(9, _, __));
        
        // Async_Ch10_Ctrl - Offset : 0x40
        protected DoubleWordRegister GenerateAsync_ch10_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[10], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(10, _);
                        return async_ch_ctrl_sigsel_field[10].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(10,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(10,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[10], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(10, _);
                        return async_ch_ctrl_sourcesel_field[10].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(10,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(10,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[10], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(10, _);
                        return async_ch_ctrl_fnsel_field[10].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(10,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(10,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[10], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(10, _);
                        return async_ch_ctrl_auxsel_field[10].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(10,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(10,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(10, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(10, _, __));
        
        // Async_Ch11_Ctrl - Offset : 0x44
        protected DoubleWordRegister GenerateAsync_ch11_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[11], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(11, _);
                        return async_ch_ctrl_sigsel_field[11].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(11,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(11,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[11], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(11, _);
                        return async_ch_ctrl_sourcesel_field[11].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(11,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(11,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[11], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(11, _);
                        return async_ch_ctrl_fnsel_field[11].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(11,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(11,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[11], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(11, _);
                        return async_ch_ctrl_auxsel_field[11].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(11,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(11,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(11, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(11, _, __));
        
        // Sync_Ch0_Ctrl - Offset : 0x48
        protected DoubleWordRegister GenerateSync_ch0_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 3, out sync_ch_ctrl_sigsel_field[0], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sigsel_ValueProvider(0, _);
                        return sync_ch_ctrl_sigsel_field[0].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Write(0,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Read(0,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out sync_ch_ctrl_sourcesel_field[0], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sourcesel_ValueProvider(0, _);
                        return sync_ch_ctrl_sourcesel_field[0].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Write(0,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Read(0,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 17)
            .WithReadCallback((_, __) => Sync_Ch_Ctrl_Read(0, _, __))
            .WithWriteCallback((_, __) => Sync_Ch_Ctrl_Write(0, _, __));
        
        // Sync_Ch1_Ctrl - Offset : 0x4C
        protected DoubleWordRegister GenerateSync_ch1_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 3, out sync_ch_ctrl_sigsel_field[1], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sigsel_ValueProvider(1, _);
                        return sync_ch_ctrl_sigsel_field[1].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Write(1,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Read(1,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out sync_ch_ctrl_sourcesel_field[1], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sourcesel_ValueProvider(1, _);
                        return sync_ch_ctrl_sourcesel_field[1].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Write(1,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Read(1,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 17)
            .WithReadCallback((_, __) => Sync_Ch_Ctrl_Read(1, _, __))
            .WithWriteCallback((_, __) => Sync_Ch_Ctrl_Write(1, _, __));
        
        // Sync_Ch2_Ctrl - Offset : 0x50
        protected DoubleWordRegister GenerateSync_ch2_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 3, out sync_ch_ctrl_sigsel_field[2], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sigsel_ValueProvider(2, _);
                        return sync_ch_ctrl_sigsel_field[2].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Write(2,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Read(2,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out sync_ch_ctrl_sourcesel_field[2], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sourcesel_ValueProvider(2, _);
                        return sync_ch_ctrl_sourcesel_field[2].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Write(2,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Read(2,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 17)
            .WithReadCallback((_, __) => Sync_Ch_Ctrl_Read(2, _, __))
            .WithWriteCallback((_, __) => Sync_Ch_Ctrl_Write(2, _, __));
        
        // Sync_Ch3_Ctrl - Offset : 0x54
        protected DoubleWordRegister GenerateSync_ch3_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 3, out sync_ch_ctrl_sigsel_field[3], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sigsel_ValueProvider(3, _);
                        return sync_ch_ctrl_sigsel_field[3].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Write(3,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sigsel_Read(3,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out sync_ch_ctrl_sourcesel_field[3], 
                    valueProviderCallback: (_) => {
                        Sync_Ch_Ctrl_Sourcesel_ValueProvider(3, _);
                        return sync_ch_ctrl_sourcesel_field[3].Value;
                    },
                    
                    writeCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Write(3,_, __),
                    
                    readCallback: (_, __) => Sync_Ch_Ctrl_Sourcesel_Read(3,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 17)
            .WithReadCallback((_, __) => Sync_Ch_Ctrl_Read(3, _, __))
            .WithWriteCallback((_, __) => Sync_Ch_Ctrl_Write(3, _, __));
        
        // Consumer_Cmu_caldn - Offset : 0x58
        protected DoubleWordRegister GenerateConsumer_cmu_caldnRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_cmu_caldn_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Cmu_caldn_Prssel_ValueProvider(_);
                        return consumer_cmu_caldn_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Cmu_caldn_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Cmu_caldn_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Cmu_caldn_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Cmu_caldn_Write(_, __));
        
        // Consumer_Cmu_calup - Offset : 0x5C
        protected DoubleWordRegister GenerateConsumer_cmu_calupRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_cmu_calup_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Cmu_calup_Prssel_ValueProvider(_);
                        return consumer_cmu_calup_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Cmu_calup_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Cmu_calup_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Cmu_calup_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Cmu_calup_Write(_, __));
        
        // Consumer_Frc_rxraw - Offset : 0x60
        protected DoubleWordRegister GenerateConsumer_frc_rxrawRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_frc_rxraw_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Frc_rxraw_Prssel_ValueProvider(_);
                        return consumer_frc_rxraw_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Frc_rxraw_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Frc_rxraw_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Frc_rxraw_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Frc_rxraw_Write(_, __));
        
        // Consumer_Iadc0_scantrigger - Offset : 0x64
        protected DoubleWordRegister GenerateConsumer_iadc0_scantriggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_iadc0_scantrigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Iadc0_scantrigger_Prssel_ValueProvider(_);
                        return consumer_iadc0_scantrigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Iadc0_scantrigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Iadc0_scantrigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_iadc0_scantrigger_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Iadc0_scantrigger_Sprssel_ValueProvider(_);
                        return consumer_iadc0_scantrigger_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Iadc0_scantrigger_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Iadc0_scantrigger_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Iadc0_scantrigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Iadc0_scantrigger_Write(_, __));
        
        // Consumer_Iadc0_singletrigger - Offset : 0x68
        protected DoubleWordRegister GenerateConsumer_iadc0_singletriggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_iadc0_singletrigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Iadc0_singletrigger_Prssel_ValueProvider(_);
                        return consumer_iadc0_singletrigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Iadc0_singletrigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Iadc0_singletrigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_iadc0_singletrigger_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Iadc0_singletrigger_Sprssel_ValueProvider(_);
                        return consumer_iadc0_singletrigger_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Iadc0_singletrigger_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Iadc0_singletrigger_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Iadc0_singletrigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Iadc0_singletrigger_Write(_, __));
        
        // Consumer_Ldmaxbar_dmareq0 - Offset : 0x6C
        protected DoubleWordRegister GenerateConsumer_ldmaxbar_dmareq0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_ldmaxbar_dmareq0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Ldmaxbar_dmareq0_Prssel_ValueProvider(_);
                        return consumer_ldmaxbar_dmareq0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Ldmaxbar_dmareq0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Ldmaxbar_dmareq0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Ldmaxbar_dmareq0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Ldmaxbar_dmareq0_Write(_, __));
        
        // Consumer_Ldmaxbar_dmareq1 - Offset : 0x70
        protected DoubleWordRegister GenerateConsumer_ldmaxbar_dmareq1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_ldmaxbar_dmareq1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Ldmaxbar_dmareq1_Prssel_ValueProvider(_);
                        return consumer_ldmaxbar_dmareq1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Ldmaxbar_dmareq1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Ldmaxbar_dmareq1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Ldmaxbar_dmareq1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Ldmaxbar_dmareq1_Write(_, __));
        
        // Consumer_letimer0_Clear - Offset : 0x74
        protected DoubleWordRegister GenerateConsumer_letimer0_clearRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_letimer0_clear_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_letimer0_Clear_Prssel_ValueProvider(_);
                        return consumer_letimer0_clear_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_letimer0_Clear_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_letimer0_Clear_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_letimer0_Clear_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_letimer0_Clear_Write(_, __));
        
        // Consumer_letimer0_Start - Offset : 0x78
        protected DoubleWordRegister GenerateConsumer_letimer0_startRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_letimer0_start_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_letimer0_Start_Prssel_ValueProvider(_);
                        return consumer_letimer0_start_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_letimer0_Start_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_letimer0_Start_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_letimer0_Start_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_letimer0_Start_Write(_, __));
        
        // Consumer_letimer0_Stop - Offset : 0x7C
        protected DoubleWordRegister GenerateConsumer_letimer0_stopRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_letimer0_stop_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_letimer0_Stop_Prssel_ValueProvider(_);
                        return consumer_letimer0_stop_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_letimer0_Stop_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_letimer0_Stop_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_letimer0_Stop_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_letimer0_Stop_Write(_, __));
        
        // Consumer_euart0_Rx - Offset : 0x80
        protected DoubleWordRegister GenerateConsumer_euart0_rxRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_euart0_rx_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_euart0_Rx_Prssel_ValueProvider(_);
                        return consumer_euart0_rx_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_euart0_Rx_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_euart0_Rx_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_euart0_Rx_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_euart0_Rx_Write(_, __));
        
        // Consumer_euart0_Trigger - Offset : 0x84
        protected DoubleWordRegister GenerateConsumer_euart0_triggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_euart0_trigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_euart0_Trigger_Prssel_ValueProvider(_);
                        return consumer_euart0_trigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_euart0_Trigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_euart0_Trigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_euart0_Trigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_euart0_Trigger_Write(_, __));
        
        // Consumer_Modem_din - Offset : 0x88
        protected DoubleWordRegister GenerateConsumer_modem_dinRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_modem_din_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Modem_din_Prssel_ValueProvider(_);
                        return consumer_modem_din_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Modem_din_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Modem_din_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Modem_din_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Modem_din_Write(_, __));
        
        // Consumer_Prortc_cc0 - Offset : 0x8C
        protected DoubleWordRegister GenerateConsumer_prortc_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_prortc_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Prortc_cc0_Prssel_ValueProvider(_);
                        return consumer_prortc_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Prortc_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Prortc_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Prortc_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Prortc_cc0_Write(_, __));
        
        // Consumer_Prortc_cc1 - Offset : 0x90
        protected DoubleWordRegister GenerateConsumer_prortc_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_prortc_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Prortc_cc1_Prssel_ValueProvider(_);
                        return consumer_prortc_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Prortc_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Prortc_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Prortc_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Prortc_cc1_Write(_, __));
        
        // Consumer_Protimer_cc0 - Offset : 0x94
        protected DoubleWordRegister GenerateConsumer_protimer_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_cc0_Prssel_ValueProvider(_);
                        return consumer_protimer_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_cc0_Write(_, __));
        
        // Consumer_Protimer_cc1 - Offset : 0x98
        protected DoubleWordRegister GenerateConsumer_protimer_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_cc1_Prssel_ValueProvider(_);
                        return consumer_protimer_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_cc1_Write(_, __));
        
        // Consumer_Protimer_cc2 - Offset : 0x9C
        protected DoubleWordRegister GenerateConsumer_protimer_cc2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_cc2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_cc2_Prssel_ValueProvider(_);
                        return consumer_protimer_cc2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_cc2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_cc2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_cc2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_cc2_Write(_, __));
        
        // Consumer_Protimer_cc3 - Offset : 0xA0
        protected DoubleWordRegister GenerateConsumer_protimer_cc3Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_cc3_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_cc3_Prssel_ValueProvider(_);
                        return consumer_protimer_cc3_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_cc3_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_cc3_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_cc3_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_cc3_Write(_, __));
        
        // Consumer_Protimer_cc4 - Offset : 0xA4
        protected DoubleWordRegister GenerateConsumer_protimer_cc4Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_cc4_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_cc4_Prssel_ValueProvider(_);
                        return consumer_protimer_cc4_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_cc4_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_cc4_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_cc4_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_cc4_Write(_, __));
        
        // Consumer_Protimer_lbtpause - Offset : 0xA8
        protected DoubleWordRegister GenerateConsumer_protimer_lbtpauseRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_lbtpause_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_lbtpause_Prssel_ValueProvider(_);
                        return consumer_protimer_lbtpause_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_lbtpause_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_lbtpause_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_lbtpause_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_lbtpause_Write(_, __));
        
        // Consumer_Protimer_lbtstart - Offset : 0xAC
        protected DoubleWordRegister GenerateConsumer_protimer_lbtstartRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_lbtstart_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_lbtstart_Prssel_ValueProvider(_);
                        return consumer_protimer_lbtstart_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_lbtstart_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_lbtstart_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_lbtstart_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_lbtstart_Write(_, __));
        
        // Consumer_Protimer_lbtstop - Offset : 0xB0
        protected DoubleWordRegister GenerateConsumer_protimer_lbtstopRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_lbtstop_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_lbtstop_Prssel_ValueProvider(_);
                        return consumer_protimer_lbtstop_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_lbtstop_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_lbtstop_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_lbtstop_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_lbtstop_Write(_, __));
        
        // Consumer_Protimer_rtcctrigger - Offset : 0xB4
        protected DoubleWordRegister GenerateConsumer_protimer_rtcctriggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_rtcctrigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_rtcctrigger_Prssel_ValueProvider(_);
                        return consumer_protimer_rtcctrigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_rtcctrigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_rtcctrigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_rtcctrigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_rtcctrigger_Write(_, __));
        
        // Consumer_Protimer_start - Offset : 0xB8
        protected DoubleWordRegister GenerateConsumer_protimer_startRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_start_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_start_Prssel_ValueProvider(_);
                        return consumer_protimer_start_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_start_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_start_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_start_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_start_Write(_, __));
        
        // Consumer_Protimer_stop - Offset : 0xBC
        protected DoubleWordRegister GenerateConsumer_protimer_stopRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_protimer_stop_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Protimer_stop_Prssel_ValueProvider(_);
                        return consumer_protimer_stop_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Protimer_stop_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Protimer_stop_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Protimer_stop_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Protimer_stop_Write(_, __));
        
        // Consumer_Rac_clr - Offset : 0xC0
        protected DoubleWordRegister GenerateConsumer_rac_clrRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_clr_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_clr_Prssel_ValueProvider(_);
                        return consumer_rac_clr_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_clr_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_clr_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_clr_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_clr_Write(_, __));
        
        // Consumer_Rac_ctiin0 - Offset : 0xC4
        protected DoubleWordRegister GenerateConsumer_rac_ctiin0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_ctiin0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_ctiin0_Prssel_ValueProvider(_);
                        return consumer_rac_ctiin0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_ctiin0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_ctiin0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_ctiin0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_ctiin0_Write(_, __));
        
        // Consumer_Rac_ctiin1 - Offset : 0xC8
        protected DoubleWordRegister GenerateConsumer_rac_ctiin1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_ctiin1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_ctiin1_Prssel_ValueProvider(_);
                        return consumer_rac_ctiin1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_ctiin1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_ctiin1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_ctiin1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_ctiin1_Write(_, __));
        
        // Consumer_Rac_ctiin2 - Offset : 0xCC
        protected DoubleWordRegister GenerateConsumer_rac_ctiin2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_ctiin2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_ctiin2_Prssel_ValueProvider(_);
                        return consumer_rac_ctiin2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_ctiin2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_ctiin2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_ctiin2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_ctiin2_Write(_, __));
        
        // Consumer_Rac_ctiin3 - Offset : 0xD0
        protected DoubleWordRegister GenerateConsumer_rac_ctiin3Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_ctiin3_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_ctiin3_Prssel_ValueProvider(_);
                        return consumer_rac_ctiin3_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_ctiin3_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_ctiin3_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_ctiin3_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_ctiin3_Write(_, __));
        
        // Consumer_Rac_forcetx - Offset : 0xD4
        protected DoubleWordRegister GenerateConsumer_rac_forcetxRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_forcetx_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_forcetx_Prssel_ValueProvider(_);
                        return consumer_rac_forcetx_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_forcetx_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_forcetx_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_forcetx_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_forcetx_Write(_, __));
        
        // Consumer_Rac_rxdis - Offset : 0xD8
        protected DoubleWordRegister GenerateConsumer_rac_rxdisRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_rxdis_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_rxdis_Prssel_ValueProvider(_);
                        return consumer_rac_rxdis_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_rxdis_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_rxdis_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_rxdis_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_rxdis_Write(_, __));
        
        // Consumer_Rac_rxen - Offset : 0xDC
        protected DoubleWordRegister GenerateConsumer_rac_rxenRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_rxen_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_rxen_Prssel_ValueProvider(_);
                        return consumer_rac_rxen_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_rxen_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_rxen_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_rxen_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_rxen_Write(_, __));
        
        // Consumer_Rac_seq - Offset : 0xE0
        protected DoubleWordRegister GenerateConsumer_rac_seqRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_seq_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_seq_Prssel_ValueProvider(_);
                        return consumer_rac_seq_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_seq_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_seq_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_seq_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_seq_Write(_, __));
        
        // Consumer_Rac_txen - Offset : 0xE4
        protected DoubleWordRegister GenerateConsumer_rac_txenRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rac_txen_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rac_txen_Prssel_ValueProvider(_);
                        return consumer_rac_txen_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rac_txen_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rac_txen_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rac_txen_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rac_txen_Write(_, __));
        
        // Consumer_Rtcc_cc0 - Offset : 0xE8
        protected DoubleWordRegister GenerateConsumer_rtcc_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rtcc_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rtcc_cc0_Prssel_ValueProvider(_);
                        return consumer_rtcc_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rtcc_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rtcc_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rtcc_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rtcc_cc0_Write(_, __));
        
        // Consumer_Rtcc_cc1 - Offset : 0xEC
        protected DoubleWordRegister GenerateConsumer_rtcc_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rtcc_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rtcc_cc1_Prssel_ValueProvider(_);
                        return consumer_rtcc_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rtcc_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rtcc_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rtcc_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rtcc_cc1_Write(_, __));
        
        // Consumer_Rtcc_cc2 - Offset : 0xF0
        protected DoubleWordRegister GenerateConsumer_rtcc_cc2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_rtcc_cc2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Rtcc_cc2_Prssel_ValueProvider(_);
                        return consumer_rtcc_cc2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Rtcc_cc2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Rtcc_cc2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Rtcc_cc2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Rtcc_cc2_Write(_, __));
        
        // Consumer_Core_ctiin0 - Offset : 0xF8
        protected DoubleWordRegister GenerateConsumer_core_ctiin0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_core_ctiin0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Core_ctiin0_Prssel_ValueProvider(_);
                        return consumer_core_ctiin0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Core_ctiin0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Core_ctiin0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Core_ctiin0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Core_ctiin0_Write(_, __));
        
        // Consumer_Core_ctiin1 - Offset : 0xFC
        protected DoubleWordRegister GenerateConsumer_core_ctiin1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_core_ctiin1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Core_ctiin1_Prssel_ValueProvider(_);
                        return consumer_core_ctiin1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Core_ctiin1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Core_ctiin1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Core_ctiin1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Core_ctiin1_Write(_, __));
        
        // Consumer_Core_ctiin2 - Offset : 0x100
        protected DoubleWordRegister GenerateConsumer_core_ctiin2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_core_ctiin2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Core_ctiin2_Prssel_ValueProvider(_);
                        return consumer_core_ctiin2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Core_ctiin2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Core_ctiin2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Core_ctiin2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Core_ctiin2_Write(_, __));
        
        // Consumer_Core_ctiin3 - Offset : 0x104
        protected DoubleWordRegister GenerateConsumer_core_ctiin3Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_core_ctiin3_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Core_ctiin3_Prssel_ValueProvider(_);
                        return consumer_core_ctiin3_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Core_ctiin3_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Core_ctiin3_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Core_ctiin3_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Core_ctiin3_Write(_, __));
        
        // Consumer_Core_m33rxev - Offset : 0x108
        protected DoubleWordRegister GenerateConsumer_core_m33rxevRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_core_m33rxev_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Core_m33rxev_Prssel_ValueProvider(_);
                        return consumer_core_m33rxev_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Core_m33rxev_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Core_m33rxev_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Core_m33rxev_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Core_m33rxev_Write(_, __));
        
        // Consumer_Timer0_cc0 - Offset : 0x10C
        protected DoubleWordRegister GenerateConsumer_timer0_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer0_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_cc0_Prssel_ValueProvider(_);
                        return consumer_timer0_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer0_cc0_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_cc0_Sprssel_ValueProvider(_);
                        return consumer_timer0_cc0_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_cc0_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_cc0_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer0_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer0_cc0_Write(_, __));
        
        // Consumer_Timer0_cc1 - Offset : 0x110
        protected DoubleWordRegister GenerateConsumer_timer0_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer0_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_cc1_Prssel_ValueProvider(_);
                        return consumer_timer0_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer0_cc1_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_cc1_Sprssel_ValueProvider(_);
                        return consumer_timer0_cc1_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_cc1_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_cc1_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer0_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer0_cc1_Write(_, __));
        
        // Consumer_Timer0_cc2 - Offset : 0x114
        protected DoubleWordRegister GenerateConsumer_timer0_cc2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer0_cc2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_cc2_Prssel_ValueProvider(_);
                        return consumer_timer0_cc2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_cc2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_cc2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer0_cc2_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_cc2_Sprssel_ValueProvider(_);
                        return consumer_timer0_cc2_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_cc2_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_cc2_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer0_cc2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer0_cc2_Write(_, __));
        
        // Consumer_Timer0_dti - Offset : 0x118
        protected DoubleWordRegister GenerateConsumer_timer0_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer0_dti_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_dti_Prssel_ValueProvider(_);
                        return consumer_timer0_dti_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_dti_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_dti_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer0_dti_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer0_dti_Write(_, __));
        
        // Consumer_Timer0_dtifs1 - Offset : 0x11C
        protected DoubleWordRegister GenerateConsumer_timer0_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer0_dtifs1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_dtifs1_Prssel_ValueProvider(_);
                        return consumer_timer0_dtifs1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_dtifs1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_dtifs1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer0_dtifs1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer0_dtifs1_Write(_, __));
        
        // Consumer_Timer0_dtifs2 - Offset : 0x120
        protected DoubleWordRegister GenerateConsumer_timer0_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer0_dtifs2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer0_dtifs2_Prssel_ValueProvider(_);
                        return consumer_timer0_dtifs2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer0_dtifs2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer0_dtifs2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer0_dtifs2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer0_dtifs2_Write(_, __));
        
        // Consumer_Timer1_cc0 - Offset : 0x124
        protected DoubleWordRegister GenerateConsumer_timer1_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer1_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_cc0_Prssel_ValueProvider(_);
                        return consumer_timer1_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer1_cc0_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_cc0_Sprssel_ValueProvider(_);
                        return consumer_timer1_cc0_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_cc0_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_cc0_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer1_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer1_cc0_Write(_, __));
        
        // Consumer_Timer1_cc1 - Offset : 0x128
        protected DoubleWordRegister GenerateConsumer_timer1_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer1_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_cc1_Prssel_ValueProvider(_);
                        return consumer_timer1_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer1_cc1_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_cc1_Sprssel_ValueProvider(_);
                        return consumer_timer1_cc1_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_cc1_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_cc1_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer1_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer1_cc1_Write(_, __));
        
        // Consumer_Timer1_cc2 - Offset : 0x12C
        protected DoubleWordRegister GenerateConsumer_timer1_cc2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer1_cc2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_cc2_Prssel_ValueProvider(_);
                        return consumer_timer1_cc2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_cc2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_cc2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer1_cc2_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_cc2_Sprssel_ValueProvider(_);
                        return consumer_timer1_cc2_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_cc2_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_cc2_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer1_cc2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer1_cc2_Write(_, __));
        
        // Consumer_Timer1_dti - Offset : 0x130
        protected DoubleWordRegister GenerateConsumer_timer1_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer1_dti_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_dti_Prssel_ValueProvider(_);
                        return consumer_timer1_dti_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_dti_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_dti_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer1_dti_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer1_dti_Write(_, __));
        
        // Consumer_Timer1_dtifs1 - Offset : 0x134
        protected DoubleWordRegister GenerateConsumer_timer1_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer1_dtifs1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_dtifs1_Prssel_ValueProvider(_);
                        return consumer_timer1_dtifs1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_dtifs1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_dtifs1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer1_dtifs1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer1_dtifs1_Write(_, __));
        
        // Consumer_Timer1_dtifs2 - Offset : 0x138
        protected DoubleWordRegister GenerateConsumer_timer1_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer1_dtifs2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer1_dtifs2_Prssel_ValueProvider(_);
                        return consumer_timer1_dtifs2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer1_dtifs2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer1_dtifs2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer1_dtifs2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer1_dtifs2_Write(_, __));
        
        // Consumer_Timer2_cc0 - Offset : 0x13C
        protected DoubleWordRegister GenerateConsumer_timer2_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer2_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_cc0_Prssel_ValueProvider(_);
                        return consumer_timer2_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer2_cc0_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_cc0_Sprssel_ValueProvider(_);
                        return consumer_timer2_cc0_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_cc0_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_cc0_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer2_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer2_cc0_Write(_, __));
        
        // Consumer_Timer2_cc1 - Offset : 0x140
        protected DoubleWordRegister GenerateConsumer_timer2_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer2_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_cc1_Prssel_ValueProvider(_);
                        return consumer_timer2_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer2_cc1_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_cc1_Sprssel_ValueProvider(_);
                        return consumer_timer2_cc1_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_cc1_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_cc1_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer2_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer2_cc1_Write(_, __));
        
        // Consumer_Timer2_cc2 - Offset : 0x144
        protected DoubleWordRegister GenerateConsumer_timer2_cc2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer2_cc2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_cc2_Prssel_ValueProvider(_);
                        return consumer_timer2_cc2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_cc2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_cc2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer2_cc2_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_cc2_Sprssel_ValueProvider(_);
                        return consumer_timer2_cc2_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_cc2_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_cc2_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer2_cc2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer2_cc2_Write(_, __));
        
        // Consumer_Timer2_dti - Offset : 0x148
        protected DoubleWordRegister GenerateConsumer_timer2_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer2_dti_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_dti_Prssel_ValueProvider(_);
                        return consumer_timer2_dti_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_dti_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_dti_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer2_dti_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer2_dti_Write(_, __));
        
        // Consumer_Timer2_dtifs1 - Offset : 0x14C
        protected DoubleWordRegister GenerateConsumer_timer2_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer2_dtifs1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_dtifs1_Prssel_ValueProvider(_);
                        return consumer_timer2_dtifs1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_dtifs1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_dtifs1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer2_dtifs1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer2_dtifs1_Write(_, __));
        
        // Consumer_Timer2_dtifs2 - Offset : 0x150
        protected DoubleWordRegister GenerateConsumer_timer2_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer2_dtifs2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer2_dtifs2_Prssel_ValueProvider(_);
                        return consumer_timer2_dtifs2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer2_dtifs2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer2_dtifs2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer2_dtifs2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer2_dtifs2_Write(_, __));
        
        // Consumer_Timer3_cc0 - Offset : 0x154
        protected DoubleWordRegister GenerateConsumer_timer3_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer3_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_cc0_Prssel_ValueProvider(_);
                        return consumer_timer3_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer3_cc0_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_cc0_Sprssel_ValueProvider(_);
                        return consumer_timer3_cc0_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_cc0_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_cc0_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer3_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer3_cc0_Write(_, __));
        
        // Consumer_Timer3_cc1 - Offset : 0x158
        protected DoubleWordRegister GenerateConsumer_timer3_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer3_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_cc1_Prssel_ValueProvider(_);
                        return consumer_timer3_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer3_cc1_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_cc1_Sprssel_ValueProvider(_);
                        return consumer_timer3_cc1_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_cc1_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_cc1_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer3_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer3_cc1_Write(_, __));
        
        // Consumer_Timer3_cc2 - Offset : 0x15C
        protected DoubleWordRegister GenerateConsumer_timer3_cc2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer3_cc2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_cc2_Prssel_ValueProvider(_);
                        return consumer_timer3_cc2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_cc2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_cc2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer3_cc2_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_cc2_Sprssel_ValueProvider(_);
                        return consumer_timer3_cc2_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_cc2_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_cc2_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer3_cc2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer3_cc2_Write(_, __));
        
        // Consumer_Timer3_dti - Offset : 0x160
        protected DoubleWordRegister GenerateConsumer_timer3_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer3_dti_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_dti_Prssel_ValueProvider(_);
                        return consumer_timer3_dti_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_dti_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_dti_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer3_dti_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer3_dti_Write(_, __));
        
        // Consumer_Timer3_dtifs1 - Offset : 0x164
        protected DoubleWordRegister GenerateConsumer_timer3_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer3_dtifs1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_dtifs1_Prssel_ValueProvider(_);
                        return consumer_timer3_dtifs1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_dtifs1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_dtifs1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer3_dtifs1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer3_dtifs1_Write(_, __));
        
        // Consumer_Timer3_dtifs2 - Offset : 0x168
        protected DoubleWordRegister GenerateConsumer_timer3_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer3_dtifs2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer3_dtifs2_Prssel_ValueProvider(_);
                        return consumer_timer3_dtifs2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer3_dtifs2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer3_dtifs2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer3_dtifs2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer3_dtifs2_Write(_, __));
        
        // Consumer_Timer4_cc0 - Offset : 0x16C
        protected DoubleWordRegister GenerateConsumer_timer4_cc0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer4_cc0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_cc0_Prssel_ValueProvider(_);
                        return consumer_timer4_cc0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_cc0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_cc0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer4_cc0_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_cc0_Sprssel_ValueProvider(_);
                        return consumer_timer4_cc0_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_cc0_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_cc0_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer4_cc0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer4_cc0_Write(_, __));
        
        // Consumer_Timer4_cc1 - Offset : 0x170
        protected DoubleWordRegister GenerateConsumer_timer4_cc1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer4_cc1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_cc1_Prssel_ValueProvider(_);
                        return consumer_timer4_cc1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_cc1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_cc1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer4_cc1_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_cc1_Sprssel_ValueProvider(_);
                        return consumer_timer4_cc1_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_cc1_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_cc1_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer4_cc1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer4_cc1_Write(_, __));
        
        // Consumer_Timer4_cc2 - Offset : 0x174
        protected DoubleWordRegister GenerateConsumer_timer4_cc2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer4_cc2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_cc2_Prssel_ValueProvider(_);
                        return consumer_timer4_cc2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_cc2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_cc2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 4)
            
            .WithValueField(8, 2, out consumer_timer4_cc2_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_cc2_Sprssel_ValueProvider(_);
                        return consumer_timer4_cc2_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_cc2_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_cc2_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Timer4_cc2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer4_cc2_Write(_, __));
        
        // Consumer_Timer4_dti - Offset : 0x178
        protected DoubleWordRegister GenerateConsumer_timer4_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer4_dti_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_dti_Prssel_ValueProvider(_);
                        return consumer_timer4_dti_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_dti_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_dti_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer4_dti_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer4_dti_Write(_, __));
        
        // Consumer_Timer4_dtifs1 - Offset : 0x17C
        protected DoubleWordRegister GenerateConsumer_timer4_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer4_dtifs1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_dtifs1_Prssel_ValueProvider(_);
                        return consumer_timer4_dtifs1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_dtifs1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_dtifs1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer4_dtifs1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer4_dtifs1_Write(_, __));
        
        // Consumer_Timer4_dtifs2 - Offset : 0x180
        protected DoubleWordRegister GenerateConsumer_timer4_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_timer4_dtifs2_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Timer4_dtifs2_Prssel_ValueProvider(_);
                        return consumer_timer4_dtifs2_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Timer4_dtifs2_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Timer4_dtifs2_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Timer4_dtifs2_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Timer4_dtifs2_Write(_, __));
        
        // Consumer_Usart0_clk - Offset : 0x184
        protected DoubleWordRegister GenerateConsumer_usart0_clkRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart0_clk_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart0_clk_Prssel_ValueProvider(_);
                        return consumer_usart0_clk_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart0_clk_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart0_clk_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart0_clk_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart0_clk_Write(_, __));
        
        // Consumer_Usart0_ir - Offset : 0x188
        protected DoubleWordRegister GenerateConsumer_usart0_irRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart0_ir_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart0_ir_Prssel_ValueProvider(_);
                        return consumer_usart0_ir_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart0_ir_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart0_ir_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart0_ir_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart0_ir_Write(_, __));
        
        // Consumer_Usart0_rx - Offset : 0x18C
        protected DoubleWordRegister GenerateConsumer_usart0_rxRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart0_rx_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart0_rx_Prssel_ValueProvider(_);
                        return consumer_usart0_rx_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart0_rx_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart0_rx_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart0_rx_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart0_rx_Write(_, __));
        
        // Consumer_Usart0_trigger - Offset : 0x190
        protected DoubleWordRegister GenerateConsumer_usart0_triggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart0_trigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart0_trigger_Prssel_ValueProvider(_);
                        return consumer_usart0_trigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart0_trigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart0_trigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart0_trigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart0_trigger_Write(_, __));
        
        // Consumer_Usart1_clk - Offset : 0x194
        protected DoubleWordRegister GenerateConsumer_usart1_clkRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart1_clk_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart1_clk_Prssel_ValueProvider(_);
                        return consumer_usart1_clk_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart1_clk_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart1_clk_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart1_clk_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart1_clk_Write(_, __));
        
        // Consumer_Usart1_ir - Offset : 0x198
        protected DoubleWordRegister GenerateConsumer_usart1_irRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart1_ir_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart1_ir_Prssel_ValueProvider(_);
                        return consumer_usart1_ir_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart1_ir_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart1_ir_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart1_ir_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart1_ir_Write(_, __));
        
        // Consumer_Usart1_rx - Offset : 0x19C
        protected DoubleWordRegister GenerateConsumer_usart1_rxRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart1_rx_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart1_rx_Prssel_ValueProvider(_);
                        return consumer_usart1_rx_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart1_rx_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart1_rx_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart1_rx_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart1_rx_Write(_, __));
        
        // Consumer_Usart1_trigger - Offset : 0x1A0
        protected DoubleWordRegister GenerateConsumer_usart1_triggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_usart1_trigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Usart1_trigger_Prssel_ValueProvider(_);
                        return consumer_usart1_trigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Usart1_trigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Usart1_trigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Usart1_trigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Usart1_trigger_Write(_, __));
        
        // Consumer_Wdog0_src0 - Offset : 0x1A4
        protected DoubleWordRegister GenerateConsumer_wdog0_src0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_wdog0_src0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Wdog0_src0_Prssel_ValueProvider(_);
                        return consumer_wdog0_src0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Wdog0_src0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Wdog0_src0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Wdog0_src0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Wdog0_src0_Write(_, __));
        
        // Consumer_Wdog0_src1 - Offset : 0x1A8
        protected DoubleWordRegister GenerateConsumer_wdog0_src1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_wdog0_src1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Wdog0_src1_Prssel_ValueProvider(_);
                        return consumer_wdog0_src1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Wdog0_src1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Wdog0_src1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Wdog0_src1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Wdog0_src1_Write(_, __));
        

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
        
        
        // Enable - Offset : 0x4
    
        protected IFlagRegisterField enable_em23dis_bit;
        partial void Enable_Em23dis_Write(bool a, bool b);
        partial void Enable_Em23dis_Read(bool a, bool b);
        partial void Enable_Em23dis_ValueProvider(bool a);
        partial void Enable_Write(uint a, uint b);
        partial void Enable_Read(uint a, uint b);
        
        
        // Async_swpulse - Offset : 0x8
    
        protected IFlagRegisterField async_swpulse_ch0pulse_bit;
        partial void Async_swpulse_Ch0pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch0pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch1pulse_bit;
        partial void Async_swpulse_Ch1pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch1pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch2pulse_bit;
        partial void Async_swpulse_Ch2pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch2pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch3pulse_bit;
        partial void Async_swpulse_Ch3pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch3pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch4pulse_bit;
        partial void Async_swpulse_Ch4pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch4pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch5pulse_bit;
        partial void Async_swpulse_Ch5pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch5pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch6pulse_bit;
        partial void Async_swpulse_Ch6pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch6pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch7pulse_bit;
        partial void Async_swpulse_Ch7pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch7pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch8pulse_bit;
        partial void Async_swpulse_Ch8pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch8pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch9pulse_bit;
        partial void Async_swpulse_Ch9pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch9pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch10pulse_bit;
        partial void Async_swpulse_Ch10pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch10pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch11pulse_bit;
        partial void Async_swpulse_Ch11pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch11pulse_ValueProvider(bool a);
        partial void Async_swpulse_Write(uint a, uint b);
        partial void Async_swpulse_Read(uint a, uint b);
        
        
        // Async_swlevel - Offset : 0xC
    
        protected IFlagRegisterField async_swlevel_ch0level_bit;
        partial void Async_swlevel_Ch0level_Write(bool a, bool b);
        partial void Async_swlevel_Ch0level_Read(bool a, bool b);
        partial void Async_swlevel_Ch0level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch1level_bit;
        partial void Async_swlevel_Ch1level_Write(bool a, bool b);
        partial void Async_swlevel_Ch1level_Read(bool a, bool b);
        partial void Async_swlevel_Ch1level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch2level_bit;
        partial void Async_swlevel_Ch2level_Write(bool a, bool b);
        partial void Async_swlevel_Ch2level_Read(bool a, bool b);
        partial void Async_swlevel_Ch2level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch3level_bit;
        partial void Async_swlevel_Ch3level_Write(bool a, bool b);
        partial void Async_swlevel_Ch3level_Read(bool a, bool b);
        partial void Async_swlevel_Ch3level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch4level_bit;
        partial void Async_swlevel_Ch4level_Write(bool a, bool b);
        partial void Async_swlevel_Ch4level_Read(bool a, bool b);
        partial void Async_swlevel_Ch4level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch5level_bit;
        partial void Async_swlevel_Ch5level_Write(bool a, bool b);
        partial void Async_swlevel_Ch5level_Read(bool a, bool b);
        partial void Async_swlevel_Ch5level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch6level_bit;
        partial void Async_swlevel_Ch6level_Write(bool a, bool b);
        partial void Async_swlevel_Ch6level_Read(bool a, bool b);
        partial void Async_swlevel_Ch6level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch7level_bit;
        partial void Async_swlevel_Ch7level_Write(bool a, bool b);
        partial void Async_swlevel_Ch7level_Read(bool a, bool b);
        partial void Async_swlevel_Ch7level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch8level_bit;
        partial void Async_swlevel_Ch8level_Write(bool a, bool b);
        partial void Async_swlevel_Ch8level_Read(bool a, bool b);
        partial void Async_swlevel_Ch8level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch9level_bit;
        partial void Async_swlevel_Ch9level_Write(bool a, bool b);
        partial void Async_swlevel_Ch9level_Read(bool a, bool b);
        partial void Async_swlevel_Ch9level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch10level_bit;
        partial void Async_swlevel_Ch10level_Write(bool a, bool b);
        partial void Async_swlevel_Ch10level_Read(bool a, bool b);
        partial void Async_swlevel_Ch10level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch11level_bit;
        partial void Async_swlevel_Ch11level_Write(bool a, bool b);
        partial void Async_swlevel_Ch11level_Read(bool a, bool b);
        partial void Async_swlevel_Ch11level_ValueProvider(bool a);
        partial void Async_swlevel_Write(uint a, uint b);
        partial void Async_swlevel_Read(uint a, uint b);
        
        
        // Async_peek - Offset : 0x10
    
        protected IFlagRegisterField async_peek_ch0val_bit;
        partial void Async_peek_Ch0val_Read(bool a, bool b);
        partial void Async_peek_Ch0val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch1val_bit;
        partial void Async_peek_Ch1val_Read(bool a, bool b);
        partial void Async_peek_Ch1val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch2val_bit;
        partial void Async_peek_Ch2val_Read(bool a, bool b);
        partial void Async_peek_Ch2val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch3val_bit;
        partial void Async_peek_Ch3val_Read(bool a, bool b);
        partial void Async_peek_Ch3val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch4val_bit;
        partial void Async_peek_Ch4val_Read(bool a, bool b);
        partial void Async_peek_Ch4val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch5val_bit;
        partial void Async_peek_Ch5val_Read(bool a, bool b);
        partial void Async_peek_Ch5val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch6val_bit;
        partial void Async_peek_Ch6val_Read(bool a, bool b);
        partial void Async_peek_Ch6val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch7val_bit;
        partial void Async_peek_Ch7val_Read(bool a, bool b);
        partial void Async_peek_Ch7val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch8val_bit;
        partial void Async_peek_Ch8val_Read(bool a, bool b);
        partial void Async_peek_Ch8val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch9val_bit;
        partial void Async_peek_Ch9val_Read(bool a, bool b);
        partial void Async_peek_Ch9val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch10val_bit;
        partial void Async_peek_Ch10val_Read(bool a, bool b);
        partial void Async_peek_Ch10val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch11val_bit;
        partial void Async_peek_Ch11val_Read(bool a, bool b);
        partial void Async_peek_Ch11val_ValueProvider(bool a);
        partial void Async_peek_Write(uint a, uint b);
        partial void Async_peek_Read(uint a, uint b);
        
        
        // Sync_peek - Offset : 0x14
    
        protected IFlagRegisterField sync_peek_ch0val_bit;
        partial void Sync_peek_Ch0val_Read(bool a, bool b);
        partial void Sync_peek_Ch0val_ValueProvider(bool a);
    
        protected IFlagRegisterField sync_peek_ch1val_bit;
        partial void Sync_peek_Ch1val_Read(bool a, bool b);
        partial void Sync_peek_Ch1val_ValueProvider(bool a);
    
        protected IFlagRegisterField sync_peek_ch2val_bit;
        partial void Sync_peek_Ch2val_Read(bool a, bool b);
        partial void Sync_peek_Ch2val_ValueProvider(bool a);
    
        protected IFlagRegisterField sync_peek_ch3val_bit;
        partial void Sync_peek_Ch3val_Read(bool a, bool b);
        partial void Sync_peek_Ch3val_ValueProvider(bool a);
        partial void Sync_peek_Write(uint a, uint b);
        partial void Sync_peek_Read(uint a, uint b);
        
        
        // Async_Ch0_Ctrl - Offset : 0x18
    
    protected IEnumRegisterField<ASYNC_CH_CTRL_SIGSEL>[] async_ch_ctrl_sigsel_field = new IEnumRegisterField<ASYNC_CH_CTRL_SIGSEL>[12];
        partial void Async_Ch_Ctrl_Sigsel_Write(ulong index, ASYNC_CH_CTRL_SIGSEL a, ASYNC_CH_CTRL_SIGSEL b);
        partial void Async_Ch_Ctrl_Sigsel_Read(ulong index, ASYNC_CH_CTRL_SIGSEL a, ASYNC_CH_CTRL_SIGSEL b);
        partial void Async_Ch_Ctrl_Sigsel_ValueProvider(ulong index, ASYNC_CH_CTRL_SIGSEL a);
    
    
        protected IValueRegisterField[] async_ch_ctrl_sourcesel_field = new IValueRegisterField[12];
        partial void Async_Ch_Ctrl_Sourcesel_Write(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Sourcesel_Read(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Sourcesel_ValueProvider(ulong index, ulong a);
    
    protected IEnumRegisterField<ASYNC_CH_CTRL_FNSEL>[] async_ch_ctrl_fnsel_field = new IEnumRegisterField<ASYNC_CH_CTRL_FNSEL>[12];
        partial void Async_Ch_Ctrl_Fnsel_Write(ulong index, ASYNC_CH_CTRL_FNSEL a, ASYNC_CH_CTRL_FNSEL b);
        partial void Async_Ch_Ctrl_Fnsel_Read(ulong index, ASYNC_CH_CTRL_FNSEL a, ASYNC_CH_CTRL_FNSEL b);
        partial void Async_Ch_Ctrl_Fnsel_ValueProvider(ulong index, ASYNC_CH_CTRL_FNSEL a);
    
    
        protected IValueRegisterField[] async_ch_ctrl_auxsel_field = new IValueRegisterField[12];
        partial void Async_Ch_Ctrl_Auxsel_Write(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Auxsel_Read(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Auxsel_ValueProvider(ulong index, ulong a);
        partial void Async_Ch_Ctrl_Write(ulong index, uint a, uint b);
        partial void Async_Ch_Ctrl_Read(ulong index, uint a, uint b);
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
    
    
    
    
    
    
    
    
        
        
        // Sync_Ch0_Ctrl - Offset : 0x48
    
    
        protected IValueRegisterField[] sync_ch_ctrl_sigsel_field = new IValueRegisterField[4];
        partial void Sync_Ch_Ctrl_Sigsel_Write(ulong index, ulong a, ulong b);
        partial void Sync_Ch_Ctrl_Sigsel_Read(ulong index, ulong a, ulong b);
        partial void Sync_Ch_Ctrl_Sigsel_ValueProvider(ulong index, ulong a);
    
    
        protected IValueRegisterField[] sync_ch_ctrl_sourcesel_field = new IValueRegisterField[4];
        partial void Sync_Ch_Ctrl_Sourcesel_Write(ulong index, ulong a, ulong b);
        partial void Sync_Ch_Ctrl_Sourcesel_Read(ulong index, ulong a, ulong b);
        partial void Sync_Ch_Ctrl_Sourcesel_ValueProvider(ulong index, ulong a);
        partial void Sync_Ch_Ctrl_Write(ulong index, uint a, uint b);
        partial void Sync_Ch_Ctrl_Read(ulong index, uint a, uint b);
        
        
    
    
    
    
        
        
    
    
    
    
        
        
    
    
    
    
        
        
        // Consumer_Cmu_caldn - Offset : 0x58
    
        protected IValueRegisterField consumer_cmu_caldn_prssel_field;
        partial void Consumer_Cmu_caldn_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Cmu_caldn_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Cmu_caldn_Prssel_ValueProvider(ulong a);
        partial void Consumer_Cmu_caldn_Write(uint a, uint b);
        partial void Consumer_Cmu_caldn_Read(uint a, uint b);
        
        
        // Consumer_Cmu_calup - Offset : 0x5C
    
        protected IValueRegisterField consumer_cmu_calup_prssel_field;
        partial void Consumer_Cmu_calup_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Cmu_calup_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Cmu_calup_Prssel_ValueProvider(ulong a);
        partial void Consumer_Cmu_calup_Write(uint a, uint b);
        partial void Consumer_Cmu_calup_Read(uint a, uint b);
        
        
        // Consumer_Frc_rxraw - Offset : 0x60
    
        protected IValueRegisterField consumer_frc_rxraw_prssel_field;
        partial void Consumer_Frc_rxraw_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Frc_rxraw_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Frc_rxraw_Prssel_ValueProvider(ulong a);
        partial void Consumer_Frc_rxraw_Write(uint a, uint b);
        partial void Consumer_Frc_rxraw_Read(uint a, uint b);
        
        
        // Consumer_Iadc0_scantrigger - Offset : 0x64
    
        protected IValueRegisterField consumer_iadc0_scantrigger_prssel_field;
        partial void Consumer_Iadc0_scantrigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Iadc0_scantrigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Iadc0_scantrigger_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_iadc0_scantrigger_sprssel_field;
        partial void Consumer_Iadc0_scantrigger_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Iadc0_scantrigger_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Iadc0_scantrigger_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Iadc0_scantrigger_Write(uint a, uint b);
        partial void Consumer_Iadc0_scantrigger_Read(uint a, uint b);
        
        
        // Consumer_Iadc0_singletrigger - Offset : 0x68
    
        protected IValueRegisterField consumer_iadc0_singletrigger_prssel_field;
        partial void Consumer_Iadc0_singletrigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Iadc0_singletrigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Iadc0_singletrigger_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_iadc0_singletrigger_sprssel_field;
        partial void Consumer_Iadc0_singletrigger_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Iadc0_singletrigger_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Iadc0_singletrigger_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Iadc0_singletrigger_Write(uint a, uint b);
        partial void Consumer_Iadc0_singletrigger_Read(uint a, uint b);
        
        
        // Consumer_Ldmaxbar_dmareq0 - Offset : 0x6C
    
        protected IValueRegisterField consumer_ldmaxbar_dmareq0_prssel_field;
        partial void Consumer_Ldmaxbar_dmareq0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Ldmaxbar_dmareq0_Write(uint a, uint b);
        partial void Consumer_Ldmaxbar_dmareq0_Read(uint a, uint b);
        
        
        // Consumer_Ldmaxbar_dmareq1 - Offset : 0x70
    
        protected IValueRegisterField consumer_ldmaxbar_dmareq1_prssel_field;
        partial void Consumer_Ldmaxbar_dmareq1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Ldmaxbar_dmareq1_Write(uint a, uint b);
        partial void Consumer_Ldmaxbar_dmareq1_Read(uint a, uint b);
        
        
        // Consumer_letimer0_Clear - Offset : 0x74
    
        protected IValueRegisterField consumer_letimer0_clear_prssel_field;
        partial void Consumer_letimer0_Clear_Prssel_Write(ulong a, ulong b);
        partial void Consumer_letimer0_Clear_Prssel_Read(ulong a, ulong b);
        partial void Consumer_letimer0_Clear_Prssel_ValueProvider(ulong a);
        partial void Consumer_letimer0_Clear_Write(uint a, uint b);
        partial void Consumer_letimer0_Clear_Read(uint a, uint b);
        
        
        // Consumer_letimer0_Start - Offset : 0x78
    
        protected IValueRegisterField consumer_letimer0_start_prssel_field;
        partial void Consumer_letimer0_Start_Prssel_Write(ulong a, ulong b);
        partial void Consumer_letimer0_Start_Prssel_Read(ulong a, ulong b);
        partial void Consumer_letimer0_Start_Prssel_ValueProvider(ulong a);
        partial void Consumer_letimer0_Start_Write(uint a, uint b);
        partial void Consumer_letimer0_Start_Read(uint a, uint b);
        
        
        // Consumer_letimer0_Stop - Offset : 0x7C
    
        protected IValueRegisterField consumer_letimer0_stop_prssel_field;
        partial void Consumer_letimer0_Stop_Prssel_Write(ulong a, ulong b);
        partial void Consumer_letimer0_Stop_Prssel_Read(ulong a, ulong b);
        partial void Consumer_letimer0_Stop_Prssel_ValueProvider(ulong a);
        partial void Consumer_letimer0_Stop_Write(uint a, uint b);
        partial void Consumer_letimer0_Stop_Read(uint a, uint b);
        
        
        // Consumer_euart0_Rx - Offset : 0x80
    
        protected IValueRegisterField consumer_euart0_rx_prssel_field;
        partial void Consumer_euart0_Rx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_euart0_Rx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_euart0_Rx_Prssel_ValueProvider(ulong a);
        partial void Consumer_euart0_Rx_Write(uint a, uint b);
        partial void Consumer_euart0_Rx_Read(uint a, uint b);
        
        
        // Consumer_euart0_Trigger - Offset : 0x84
    
        protected IValueRegisterField consumer_euart0_trigger_prssel_field;
        partial void Consumer_euart0_Trigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_euart0_Trigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_euart0_Trigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_euart0_Trigger_Write(uint a, uint b);
        partial void Consumer_euart0_Trigger_Read(uint a, uint b);
        
        
        // Consumer_Modem_din - Offset : 0x88
    
        protected IValueRegisterField consumer_modem_din_prssel_field;
        partial void Consumer_Modem_din_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Modem_din_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Modem_din_Prssel_ValueProvider(ulong a);
        partial void Consumer_Modem_din_Write(uint a, uint b);
        partial void Consumer_Modem_din_Read(uint a, uint b);
        
        
        // Consumer_Prortc_cc0 - Offset : 0x8C
    
        protected IValueRegisterField consumer_prortc_cc0_prssel_field;
        partial void Consumer_Prortc_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Prortc_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Prortc_cc0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Prortc_cc0_Write(uint a, uint b);
        partial void Consumer_Prortc_cc0_Read(uint a, uint b);
        
        
        // Consumer_Prortc_cc1 - Offset : 0x90
    
        protected IValueRegisterField consumer_prortc_cc1_prssel_field;
        partial void Consumer_Prortc_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Prortc_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Prortc_cc1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Prortc_cc1_Write(uint a, uint b);
        partial void Consumer_Prortc_cc1_Read(uint a, uint b);
        
        
        // Consumer_Protimer_cc0 - Offset : 0x94
    
        protected IValueRegisterField consumer_protimer_cc0_prssel_field;
        partial void Consumer_Protimer_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc0_Write(uint a, uint b);
        partial void Consumer_Protimer_cc0_Read(uint a, uint b);
        
        
        // Consumer_Protimer_cc1 - Offset : 0x98
    
        protected IValueRegisterField consumer_protimer_cc1_prssel_field;
        partial void Consumer_Protimer_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc1_Write(uint a, uint b);
        partial void Consumer_Protimer_cc1_Read(uint a, uint b);
        
        
        // Consumer_Protimer_cc2 - Offset : 0x9C
    
        protected IValueRegisterField consumer_protimer_cc2_prssel_field;
        partial void Consumer_Protimer_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc2_Write(uint a, uint b);
        partial void Consumer_Protimer_cc2_Read(uint a, uint b);
        
        
        // Consumer_Protimer_cc3 - Offset : 0xA0
    
        protected IValueRegisterField consumer_protimer_cc3_prssel_field;
        partial void Consumer_Protimer_cc3_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc3_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc3_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc3_Write(uint a, uint b);
        partial void Consumer_Protimer_cc3_Read(uint a, uint b);
        
        
        // Consumer_Protimer_cc4 - Offset : 0xA4
    
        protected IValueRegisterField consumer_protimer_cc4_prssel_field;
        partial void Consumer_Protimer_cc4_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc4_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc4_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc4_Write(uint a, uint b);
        partial void Consumer_Protimer_cc4_Read(uint a, uint b);
        
        
        // Consumer_Protimer_lbtpause - Offset : 0xA8
    
        protected IValueRegisterField consumer_protimer_lbtpause_prssel_field;
        partial void Consumer_Protimer_lbtpause_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_lbtpause_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_lbtpause_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_lbtpause_Write(uint a, uint b);
        partial void Consumer_Protimer_lbtpause_Read(uint a, uint b);
        
        
        // Consumer_Protimer_lbtstart - Offset : 0xAC
    
        protected IValueRegisterField consumer_protimer_lbtstart_prssel_field;
        partial void Consumer_Protimer_lbtstart_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstart_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstart_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_lbtstart_Write(uint a, uint b);
        partial void Consumer_Protimer_lbtstart_Read(uint a, uint b);
        
        
        // Consumer_Protimer_lbtstop - Offset : 0xB0
    
        protected IValueRegisterField consumer_protimer_lbtstop_prssel_field;
        partial void Consumer_Protimer_lbtstop_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstop_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstop_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_lbtstop_Write(uint a, uint b);
        partial void Consumer_Protimer_lbtstop_Read(uint a, uint b);
        
        
        // Consumer_Protimer_rtcctrigger - Offset : 0xB4
    
        protected IValueRegisterField consumer_protimer_rtcctrigger_prssel_field;
        partial void Consumer_Protimer_rtcctrigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_rtcctrigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_rtcctrigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_rtcctrigger_Write(uint a, uint b);
        partial void Consumer_Protimer_rtcctrigger_Read(uint a, uint b);
        
        
        // Consumer_Protimer_start - Offset : 0xB8
    
        protected IValueRegisterField consumer_protimer_start_prssel_field;
        partial void Consumer_Protimer_start_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_start_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_start_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_start_Write(uint a, uint b);
        partial void Consumer_Protimer_start_Read(uint a, uint b);
        
        
        // Consumer_Protimer_stop - Offset : 0xBC
    
        protected IValueRegisterField consumer_protimer_stop_prssel_field;
        partial void Consumer_Protimer_stop_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_stop_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_stop_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_stop_Write(uint a, uint b);
        partial void Consumer_Protimer_stop_Read(uint a, uint b);
        
        
        // Consumer_Rac_clr - Offset : 0xC0
    
        protected IValueRegisterField consumer_rac_clr_prssel_field;
        partial void Consumer_Rac_clr_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_clr_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_clr_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_clr_Write(uint a, uint b);
        partial void Consumer_Rac_clr_Read(uint a, uint b);
        
        
        // Consumer_Rac_ctiin0 - Offset : 0xC4
    
        protected IValueRegisterField consumer_rac_ctiin0_prssel_field;
        partial void Consumer_Rac_ctiin0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin0_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin0_Read(uint a, uint b);
        
        
        // Consumer_Rac_ctiin1 - Offset : 0xC8
    
        protected IValueRegisterField consumer_rac_ctiin1_prssel_field;
        partial void Consumer_Rac_ctiin1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin1_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin1_Read(uint a, uint b);
        
        
        // Consumer_Rac_ctiin2 - Offset : 0xCC
    
        protected IValueRegisterField consumer_rac_ctiin2_prssel_field;
        partial void Consumer_Rac_ctiin2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin2_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin2_Read(uint a, uint b);
        
        
        // Consumer_Rac_ctiin3 - Offset : 0xD0
    
        protected IValueRegisterField consumer_rac_ctiin3_prssel_field;
        partial void Consumer_Rac_ctiin3_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin3_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin3_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin3_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin3_Read(uint a, uint b);
        
        
        // Consumer_Rac_forcetx - Offset : 0xD4
    
        protected IValueRegisterField consumer_rac_forcetx_prssel_field;
        partial void Consumer_Rac_forcetx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_forcetx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_forcetx_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_forcetx_Write(uint a, uint b);
        partial void Consumer_Rac_forcetx_Read(uint a, uint b);
        
        
        // Consumer_Rac_rxdis - Offset : 0xD8
    
        protected IValueRegisterField consumer_rac_rxdis_prssel_field;
        partial void Consumer_Rac_rxdis_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_rxdis_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_rxdis_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_rxdis_Write(uint a, uint b);
        partial void Consumer_Rac_rxdis_Read(uint a, uint b);
        
        
        // Consumer_Rac_rxen - Offset : 0xDC
    
        protected IValueRegisterField consumer_rac_rxen_prssel_field;
        partial void Consumer_Rac_rxen_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_rxen_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_rxen_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_rxen_Write(uint a, uint b);
        partial void Consumer_Rac_rxen_Read(uint a, uint b);
        
        
        // Consumer_Rac_seq - Offset : 0xE0
    
        protected IValueRegisterField consumer_rac_seq_prssel_field;
        partial void Consumer_Rac_seq_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_seq_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_seq_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_seq_Write(uint a, uint b);
        partial void Consumer_Rac_seq_Read(uint a, uint b);
        
        
        // Consumer_Rac_txen - Offset : 0xE4
    
        protected IValueRegisterField consumer_rac_txen_prssel_field;
        partial void Consumer_Rac_txen_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_txen_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_txen_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_txen_Write(uint a, uint b);
        partial void Consumer_Rac_txen_Read(uint a, uint b);
        
        
        // Consumer_Rtcc_cc0 - Offset : 0xE8
    
        protected IValueRegisterField consumer_rtcc_cc0_prssel_field;
        partial void Consumer_Rtcc_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rtcc_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rtcc_cc0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rtcc_cc0_Write(uint a, uint b);
        partial void Consumer_Rtcc_cc0_Read(uint a, uint b);
        
        
        // Consumer_Rtcc_cc1 - Offset : 0xEC
    
        protected IValueRegisterField consumer_rtcc_cc1_prssel_field;
        partial void Consumer_Rtcc_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rtcc_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rtcc_cc1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rtcc_cc1_Write(uint a, uint b);
        partial void Consumer_Rtcc_cc1_Read(uint a, uint b);
        
        
        // Consumer_Rtcc_cc2 - Offset : 0xF0
    
        protected IValueRegisterField consumer_rtcc_cc2_prssel_field;
        partial void Consumer_Rtcc_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rtcc_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rtcc_cc2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rtcc_cc2_Write(uint a, uint b);
        partial void Consumer_Rtcc_cc2_Read(uint a, uint b);
        
        
        // Consumer_Core_ctiin0 - Offset : 0xF8
    
        protected IValueRegisterField consumer_core_ctiin0_prssel_field;
        partial void Consumer_Core_ctiin0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin0_Write(uint a, uint b);
        partial void Consumer_Core_ctiin0_Read(uint a, uint b);
        
        
        // Consumer_Core_ctiin1 - Offset : 0xFC
    
        protected IValueRegisterField consumer_core_ctiin1_prssel_field;
        partial void Consumer_Core_ctiin1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin1_Write(uint a, uint b);
        partial void Consumer_Core_ctiin1_Read(uint a, uint b);
        
        
        // Consumer_Core_ctiin2 - Offset : 0x100
    
        protected IValueRegisterField consumer_core_ctiin2_prssel_field;
        partial void Consumer_Core_ctiin2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin2_Write(uint a, uint b);
        partial void Consumer_Core_ctiin2_Read(uint a, uint b);
        
        
        // Consumer_Core_ctiin3 - Offset : 0x104
    
        protected IValueRegisterField consumer_core_ctiin3_prssel_field;
        partial void Consumer_Core_ctiin3_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin3_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin3_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin3_Write(uint a, uint b);
        partial void Consumer_Core_ctiin3_Read(uint a, uint b);
        
        
        // Consumer_Core_m33rxev - Offset : 0x108
    
        protected IValueRegisterField consumer_core_m33rxev_prssel_field;
        partial void Consumer_Core_m33rxev_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_m33rxev_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_m33rxev_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_m33rxev_Write(uint a, uint b);
        partial void Consumer_Core_m33rxev_Read(uint a, uint b);
        
        
        // Consumer_Timer0_cc0 - Offset : 0x10C
    
        protected IValueRegisterField consumer_timer0_cc0_prssel_field;
        partial void Consumer_Timer0_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_cc0_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer0_cc0_sprssel_field;
        partial void Consumer_Timer0_cc0_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_cc0_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_cc0_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_cc0_Write(uint a, uint b);
        partial void Consumer_Timer0_cc0_Read(uint a, uint b);
        
        
        // Consumer_Timer0_cc1 - Offset : 0x110
    
        protected IValueRegisterField consumer_timer0_cc1_prssel_field;
        partial void Consumer_Timer0_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_cc1_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer0_cc1_sprssel_field;
        partial void Consumer_Timer0_cc1_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_cc1_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_cc1_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_cc1_Write(uint a, uint b);
        partial void Consumer_Timer0_cc1_Read(uint a, uint b);
        
        
        // Consumer_Timer0_cc2 - Offset : 0x114
    
        protected IValueRegisterField consumer_timer0_cc2_prssel_field;
        partial void Consumer_Timer0_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_cc2_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer0_cc2_sprssel_field;
        partial void Consumer_Timer0_cc2_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_cc2_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_cc2_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_cc2_Write(uint a, uint b);
        partial void Consumer_Timer0_cc2_Read(uint a, uint b);
        
        
        // Consumer_Timer0_dti - Offset : 0x118
    
        protected IValueRegisterField consumer_timer0_dti_prssel_field;
        partial void Consumer_Timer0_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_dti_Write(uint a, uint b);
        partial void Consumer_Timer0_dti_Read(uint a, uint b);
        
        
        // Consumer_Timer0_dtifs1 - Offset : 0x11C
    
        protected IValueRegisterField consumer_timer0_dtifs1_prssel_field;
        partial void Consumer_Timer0_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer0_dtifs1_Read(uint a, uint b);
        
        
        // Consumer_Timer0_dtifs2 - Offset : 0x120
    
        protected IValueRegisterField consumer_timer0_dtifs2_prssel_field;
        partial void Consumer_Timer0_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer0_dtifs2_Read(uint a, uint b);
        
        
        // Consumer_Timer1_cc0 - Offset : 0x124
    
        protected IValueRegisterField consumer_timer1_cc0_prssel_field;
        partial void Consumer_Timer1_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_cc0_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer1_cc0_sprssel_field;
        partial void Consumer_Timer1_cc0_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_cc0_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_cc0_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_cc0_Write(uint a, uint b);
        partial void Consumer_Timer1_cc0_Read(uint a, uint b);
        
        
        // Consumer_Timer1_cc1 - Offset : 0x128
    
        protected IValueRegisterField consumer_timer1_cc1_prssel_field;
        partial void Consumer_Timer1_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_cc1_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer1_cc1_sprssel_field;
        partial void Consumer_Timer1_cc1_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_cc1_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_cc1_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_cc1_Write(uint a, uint b);
        partial void Consumer_Timer1_cc1_Read(uint a, uint b);
        
        
        // Consumer_Timer1_cc2 - Offset : 0x12C
    
        protected IValueRegisterField consumer_timer1_cc2_prssel_field;
        partial void Consumer_Timer1_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_cc2_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer1_cc2_sprssel_field;
        partial void Consumer_Timer1_cc2_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_cc2_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_cc2_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_cc2_Write(uint a, uint b);
        partial void Consumer_Timer1_cc2_Read(uint a, uint b);
        
        
        // Consumer_Timer1_dti - Offset : 0x130
    
        protected IValueRegisterField consumer_timer1_dti_prssel_field;
        partial void Consumer_Timer1_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_dti_Write(uint a, uint b);
        partial void Consumer_Timer1_dti_Read(uint a, uint b);
        
        
        // Consumer_Timer1_dtifs1 - Offset : 0x134
    
        protected IValueRegisterField consumer_timer1_dtifs1_prssel_field;
        partial void Consumer_Timer1_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer1_dtifs1_Read(uint a, uint b);
        
        
        // Consumer_Timer1_dtifs2 - Offset : 0x138
    
        protected IValueRegisterField consumer_timer1_dtifs2_prssel_field;
        partial void Consumer_Timer1_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer1_dtifs2_Read(uint a, uint b);
        
        
        // Consumer_Timer2_cc0 - Offset : 0x13C
    
        protected IValueRegisterField consumer_timer2_cc0_prssel_field;
        partial void Consumer_Timer2_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_cc0_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer2_cc0_sprssel_field;
        partial void Consumer_Timer2_cc0_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_cc0_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_cc0_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_cc0_Write(uint a, uint b);
        partial void Consumer_Timer2_cc0_Read(uint a, uint b);
        
        
        // Consumer_Timer2_cc1 - Offset : 0x140
    
        protected IValueRegisterField consumer_timer2_cc1_prssel_field;
        partial void Consumer_Timer2_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_cc1_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer2_cc1_sprssel_field;
        partial void Consumer_Timer2_cc1_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_cc1_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_cc1_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_cc1_Write(uint a, uint b);
        partial void Consumer_Timer2_cc1_Read(uint a, uint b);
        
        
        // Consumer_Timer2_cc2 - Offset : 0x144
    
        protected IValueRegisterField consumer_timer2_cc2_prssel_field;
        partial void Consumer_Timer2_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_cc2_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer2_cc2_sprssel_field;
        partial void Consumer_Timer2_cc2_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_cc2_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_cc2_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_cc2_Write(uint a, uint b);
        partial void Consumer_Timer2_cc2_Read(uint a, uint b);
        
        
        // Consumer_Timer2_dti - Offset : 0x148
    
        protected IValueRegisterField consumer_timer2_dti_prssel_field;
        partial void Consumer_Timer2_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_dti_Write(uint a, uint b);
        partial void Consumer_Timer2_dti_Read(uint a, uint b);
        
        
        // Consumer_Timer2_dtifs1 - Offset : 0x14C
    
        protected IValueRegisterField consumer_timer2_dtifs1_prssel_field;
        partial void Consumer_Timer2_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer2_dtifs1_Read(uint a, uint b);
        
        
        // Consumer_Timer2_dtifs2 - Offset : 0x150
    
        protected IValueRegisterField consumer_timer2_dtifs2_prssel_field;
        partial void Consumer_Timer2_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer2_dtifs2_Read(uint a, uint b);
        
        
        // Consumer_Timer3_cc0 - Offset : 0x154
    
        protected IValueRegisterField consumer_timer3_cc0_prssel_field;
        partial void Consumer_Timer3_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_cc0_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer3_cc0_sprssel_field;
        partial void Consumer_Timer3_cc0_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_cc0_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_cc0_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_cc0_Write(uint a, uint b);
        partial void Consumer_Timer3_cc0_Read(uint a, uint b);
        
        
        // Consumer_Timer3_cc1 - Offset : 0x158
    
        protected IValueRegisterField consumer_timer3_cc1_prssel_field;
        partial void Consumer_Timer3_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_cc1_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer3_cc1_sprssel_field;
        partial void Consumer_Timer3_cc1_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_cc1_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_cc1_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_cc1_Write(uint a, uint b);
        partial void Consumer_Timer3_cc1_Read(uint a, uint b);
        
        
        // Consumer_Timer3_cc2 - Offset : 0x15C
    
        protected IValueRegisterField consumer_timer3_cc2_prssel_field;
        partial void Consumer_Timer3_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_cc2_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer3_cc2_sprssel_field;
        partial void Consumer_Timer3_cc2_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_cc2_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_cc2_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_cc2_Write(uint a, uint b);
        partial void Consumer_Timer3_cc2_Read(uint a, uint b);
        
        
        // Consumer_Timer3_dti - Offset : 0x160
    
        protected IValueRegisterField consumer_timer3_dti_prssel_field;
        partial void Consumer_Timer3_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_dti_Write(uint a, uint b);
        partial void Consumer_Timer3_dti_Read(uint a, uint b);
        
        
        // Consumer_Timer3_dtifs1 - Offset : 0x164
    
        protected IValueRegisterField consumer_timer3_dtifs1_prssel_field;
        partial void Consumer_Timer3_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer3_dtifs1_Read(uint a, uint b);
        
        
        // Consumer_Timer3_dtifs2 - Offset : 0x168
    
        protected IValueRegisterField consumer_timer3_dtifs2_prssel_field;
        partial void Consumer_Timer3_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer3_dtifs2_Read(uint a, uint b);
        
        
        // Consumer_Timer4_cc0 - Offset : 0x16C
    
        protected IValueRegisterField consumer_timer4_cc0_prssel_field;
        partial void Consumer_Timer4_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_cc0_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer4_cc0_sprssel_field;
        partial void Consumer_Timer4_cc0_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_cc0_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_cc0_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_cc0_Write(uint a, uint b);
        partial void Consumer_Timer4_cc0_Read(uint a, uint b);
        
        
        // Consumer_Timer4_cc1 - Offset : 0x170
    
        protected IValueRegisterField consumer_timer4_cc1_prssel_field;
        partial void Consumer_Timer4_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_cc1_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer4_cc1_sprssel_field;
        partial void Consumer_Timer4_cc1_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_cc1_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_cc1_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_cc1_Write(uint a, uint b);
        partial void Consumer_Timer4_cc1_Read(uint a, uint b);
        
        
        // Consumer_Timer4_cc2 - Offset : 0x174
    
        protected IValueRegisterField consumer_timer4_cc2_prssel_field;
        partial void Consumer_Timer4_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_cc2_Prssel_ValueProvider(ulong a);
    
        protected IValueRegisterField consumer_timer4_cc2_sprssel_field;
        partial void Consumer_Timer4_cc2_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_cc2_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_cc2_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_cc2_Write(uint a, uint b);
        partial void Consumer_Timer4_cc2_Read(uint a, uint b);
        
        
        // Consumer_Timer4_dti - Offset : 0x178
    
        protected IValueRegisterField consumer_timer4_dti_prssel_field;
        partial void Consumer_Timer4_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_dti_Write(uint a, uint b);
        partial void Consumer_Timer4_dti_Read(uint a, uint b);
        
        
        // Consumer_Timer4_dtifs1 - Offset : 0x17C
    
        protected IValueRegisterField consumer_timer4_dtifs1_prssel_field;
        partial void Consumer_Timer4_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer4_dtifs1_Read(uint a, uint b);
        
        
        // Consumer_Timer4_dtifs2 - Offset : 0x180
    
        protected IValueRegisterField consumer_timer4_dtifs2_prssel_field;
        partial void Consumer_Timer4_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer4_dtifs2_Read(uint a, uint b);
        
        
        // Consumer_Usart0_clk - Offset : 0x184
    
        protected IValueRegisterField consumer_usart0_clk_prssel_field;
        partial void Consumer_Usart0_clk_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_clk_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_clk_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_clk_Write(uint a, uint b);
        partial void Consumer_Usart0_clk_Read(uint a, uint b);
        
        
        // Consumer_Usart0_ir - Offset : 0x188
    
        protected IValueRegisterField consumer_usart0_ir_prssel_field;
        partial void Consumer_Usart0_ir_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_ir_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_ir_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_ir_Write(uint a, uint b);
        partial void Consumer_Usart0_ir_Read(uint a, uint b);
        
        
        // Consumer_Usart0_rx - Offset : 0x18C
    
        protected IValueRegisterField consumer_usart0_rx_prssel_field;
        partial void Consumer_Usart0_rx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_rx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_rx_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_rx_Write(uint a, uint b);
        partial void Consumer_Usart0_rx_Read(uint a, uint b);
        
        
        // Consumer_Usart0_trigger - Offset : 0x190
    
        protected IValueRegisterField consumer_usart0_trigger_prssel_field;
        partial void Consumer_Usart0_trigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_trigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_trigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_trigger_Write(uint a, uint b);
        partial void Consumer_Usart0_trigger_Read(uint a, uint b);
        
        
        // Consumer_Usart1_clk - Offset : 0x194
    
        protected IValueRegisterField consumer_usart1_clk_prssel_field;
        partial void Consumer_Usart1_clk_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart1_clk_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart1_clk_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart1_clk_Write(uint a, uint b);
        partial void Consumer_Usart1_clk_Read(uint a, uint b);
        
        
        // Consumer_Usart1_ir - Offset : 0x198
    
        protected IValueRegisterField consumer_usart1_ir_prssel_field;
        partial void Consumer_Usart1_ir_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart1_ir_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart1_ir_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart1_ir_Write(uint a, uint b);
        partial void Consumer_Usart1_ir_Read(uint a, uint b);
        
        
        // Consumer_Usart1_rx - Offset : 0x19C
    
        protected IValueRegisterField consumer_usart1_rx_prssel_field;
        partial void Consumer_Usart1_rx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart1_rx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart1_rx_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart1_rx_Write(uint a, uint b);
        partial void Consumer_Usart1_rx_Read(uint a, uint b);
        
        
        // Consumer_Usart1_trigger - Offset : 0x1A0
    
        protected IValueRegisterField consumer_usart1_trigger_prssel_field;
        partial void Consumer_Usart1_trigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart1_trigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart1_trigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart1_trigger_Write(uint a, uint b);
        partial void Consumer_Usart1_trigger_Read(uint a, uint b);
        
        
        // Consumer_Wdog0_src0 - Offset : 0x1A4
    
        protected IValueRegisterField consumer_wdog0_src0_prssel_field;
        partial void Consumer_Wdog0_src0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Wdog0_src0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Wdog0_src0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Wdog0_src0_Write(uint a, uint b);
        partial void Consumer_Wdog0_src0_Read(uint a, uint b);
        
        
        // Consumer_Wdog0_src1 - Offset : 0x1A8
    
        protected IValueRegisterField consumer_wdog0_src1_prssel_field;
        partial void Consumer_Wdog0_src1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Wdog0_src1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Wdog0_src1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Wdog0_src1_Write(uint a, uint b);
        partial void Consumer_Wdog0_src1_Read(uint a, uint b);
        
        partial void PRS_Reset();

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
            Enable = 0x4,
            Async_swpulse = 0x8,
            Async_swlevel = 0xC,
            Async_peek = 0x10,
            Sync_peek = 0x14,
            Async_Ch0_Ctrl = 0x18,
            Async_Ch1_Ctrl = 0x1C,
            Async_Ch2_Ctrl = 0x20,
            Async_Ch3_Ctrl = 0x24,
            Async_Ch4_Ctrl = 0x28,
            Async_Ch5_Ctrl = 0x2C,
            Async_Ch6_Ctrl = 0x30,
            Async_Ch7_Ctrl = 0x34,
            Async_Ch8_Ctrl = 0x38,
            Async_Ch9_Ctrl = 0x3C,
            Async_Ch10_Ctrl = 0x40,
            Async_Ch11_Ctrl = 0x44,
            Sync_Ch0_Ctrl = 0x48,
            Sync_Ch1_Ctrl = 0x4C,
            Sync_Ch2_Ctrl = 0x50,
            Sync_Ch3_Ctrl = 0x54,
            Consumer_Cmu_caldn = 0x58,
            Consumer_Cmu_calup = 0x5C,
            Consumer_Frc_rxraw = 0x60,
            Consumer_Iadc0_scantrigger = 0x64,
            Consumer_Iadc0_singletrigger = 0x68,
            Consumer_Ldmaxbar_dmareq0 = 0x6C,
            Consumer_Ldmaxbar_dmareq1 = 0x70,
            Consumer_letimer0_Clear = 0x74,
            Consumer_letimer0_Start = 0x78,
            Consumer_letimer0_Stop = 0x7C,
            Consumer_euart0_Rx = 0x80,
            Consumer_euart0_Trigger = 0x84,
            Consumer_Modem_din = 0x88,
            Consumer_Prortc_cc0 = 0x8C,
            Consumer_Prortc_cc1 = 0x90,
            Consumer_Protimer_cc0 = 0x94,
            Consumer_Protimer_cc1 = 0x98,
            Consumer_Protimer_cc2 = 0x9C,
            Consumer_Protimer_cc3 = 0xA0,
            Consumer_Protimer_cc4 = 0xA4,
            Consumer_Protimer_lbtpause = 0xA8,
            Consumer_Protimer_lbtstart = 0xAC,
            Consumer_Protimer_lbtstop = 0xB0,
            Consumer_Protimer_rtcctrigger = 0xB4,
            Consumer_Protimer_start = 0xB8,
            Consumer_Protimer_stop = 0xBC,
            Consumer_Rac_clr = 0xC0,
            Consumer_Rac_ctiin0 = 0xC4,
            Consumer_Rac_ctiin1 = 0xC8,
            Consumer_Rac_ctiin2 = 0xCC,
            Consumer_Rac_ctiin3 = 0xD0,
            Consumer_Rac_forcetx = 0xD4,
            Consumer_Rac_rxdis = 0xD8,
            Consumer_Rac_rxen = 0xDC,
            Consumer_Rac_seq = 0xE0,
            Consumer_Rac_txen = 0xE4,
            Consumer_Rtcc_cc0 = 0xE8,
            Consumer_Rtcc_cc1 = 0xEC,
            Consumer_Rtcc_cc2 = 0xF0,
            Consumer_Core_ctiin0 = 0xF8,
            Consumer_Core_ctiin1 = 0xFC,
            Consumer_Core_ctiin2 = 0x100,
            Consumer_Core_ctiin3 = 0x104,
            Consumer_Core_m33rxev = 0x108,
            Consumer_Timer0_cc0 = 0x10C,
            Consumer_Timer0_cc1 = 0x110,
            Consumer_Timer0_cc2 = 0x114,
            Consumer_Timer0_dti = 0x118,
            Consumer_Timer0_dtifs1 = 0x11C,
            Consumer_Timer0_dtifs2 = 0x120,
            Consumer_Timer1_cc0 = 0x124,
            Consumer_Timer1_cc1 = 0x128,
            Consumer_Timer1_cc2 = 0x12C,
            Consumer_Timer1_dti = 0x130,
            Consumer_Timer1_dtifs1 = 0x134,
            Consumer_Timer1_dtifs2 = 0x138,
            Consumer_Timer2_cc0 = 0x13C,
            Consumer_Timer2_cc1 = 0x140,
            Consumer_Timer2_cc2 = 0x144,
            Consumer_Timer2_dti = 0x148,
            Consumer_Timer2_dtifs1 = 0x14C,
            Consumer_Timer2_dtifs2 = 0x150,
            Consumer_Timer3_cc0 = 0x154,
            Consumer_Timer3_cc1 = 0x158,
            Consumer_Timer3_cc2 = 0x15C,
            Consumer_Timer3_dti = 0x160,
            Consumer_Timer3_dtifs1 = 0x164,
            Consumer_Timer3_dtifs2 = 0x168,
            Consumer_Timer4_cc0 = 0x16C,
            Consumer_Timer4_cc1 = 0x170,
            Consumer_Timer4_cc2 = 0x174,
            Consumer_Timer4_dti = 0x178,
            Consumer_Timer4_dtifs1 = 0x17C,
            Consumer_Timer4_dtifs2 = 0x180,
            Consumer_Usart0_clk = 0x184,
            Consumer_Usart0_ir = 0x188,
            Consumer_Usart0_rx = 0x18C,
            Consumer_Usart0_trigger = 0x190,
            Consumer_Usart1_clk = 0x194,
            Consumer_Usart1_ir = 0x198,
            Consumer_Usart1_rx = 0x19C,
            Consumer_Usart1_trigger = 0x1A0,
            Consumer_Wdog0_src0 = 0x1A4,
            Consumer_Wdog0_src1 = 0x1A8,
            
            Ipversion_SET = 0x1000,
            Enable_SET = 0x1004,
            Async_swpulse_SET = 0x1008,
            Async_swlevel_SET = 0x100C,
            Async_peek_SET = 0x1010,
            Sync_peek_SET = 0x1014,
            Async_Ch0_Ctrl_SET = 0x1018,
            Async_Ch1_Ctrl_SET = 0x101C,
            Async_Ch2_Ctrl_SET = 0x1020,
            Async_Ch3_Ctrl_SET = 0x1024,
            Async_Ch4_Ctrl_SET = 0x1028,
            Async_Ch5_Ctrl_SET = 0x102C,
            Async_Ch6_Ctrl_SET = 0x1030,
            Async_Ch7_Ctrl_SET = 0x1034,
            Async_Ch8_Ctrl_SET = 0x1038,
            Async_Ch9_Ctrl_SET = 0x103C,
            Async_Ch10_Ctrl_SET = 0x1040,
            Async_Ch11_Ctrl_SET = 0x1044,
            Sync_Ch0_Ctrl_SET = 0x1048,
            Sync_Ch1_Ctrl_SET = 0x104C,
            Sync_Ch2_Ctrl_SET = 0x1050,
            Sync_Ch3_Ctrl_SET = 0x1054,
            Consumer_Cmu_caldn_SET = 0x1058,
            Consumer_Cmu_calup_SET = 0x105C,
            Consumer_Frc_rxraw_SET = 0x1060,
            Consumer_Iadc0_scantrigger_SET = 0x1064,
            Consumer_Iadc0_singletrigger_SET = 0x1068,
            Consumer_Ldmaxbar_dmareq0_SET = 0x106C,
            Consumer_Ldmaxbar_dmareq1_SET = 0x1070,
            Consumer_letimer0_Clear_SET = 0x1074,
            Consumer_letimer0_Start_SET = 0x1078,
            Consumer_letimer0_Stop_SET = 0x107C,
            Consumer_euart0_Rx_SET = 0x1080,
            Consumer_euart0_Trigger_SET = 0x1084,
            Consumer_Modem_din_SET = 0x1088,
            Consumer_Prortc_cc0_SET = 0x108C,
            Consumer_Prortc_cc1_SET = 0x1090,
            Consumer_Protimer_cc0_SET = 0x1094,
            Consumer_Protimer_cc1_SET = 0x1098,
            Consumer_Protimer_cc2_SET = 0x109C,
            Consumer_Protimer_cc3_SET = 0x10A0,
            Consumer_Protimer_cc4_SET = 0x10A4,
            Consumer_Protimer_lbtpause_SET = 0x10A8,
            Consumer_Protimer_lbtstart_SET = 0x10AC,
            Consumer_Protimer_lbtstop_SET = 0x10B0,
            Consumer_Protimer_rtcctrigger_SET = 0x10B4,
            Consumer_Protimer_start_SET = 0x10B8,
            Consumer_Protimer_stop_SET = 0x10BC,
            Consumer_Rac_clr_SET = 0x10C0,
            Consumer_Rac_ctiin0_SET = 0x10C4,
            Consumer_Rac_ctiin1_SET = 0x10C8,
            Consumer_Rac_ctiin2_SET = 0x10CC,
            Consumer_Rac_ctiin3_SET = 0x10D0,
            Consumer_Rac_forcetx_SET = 0x10D4,
            Consumer_Rac_rxdis_SET = 0x10D8,
            Consumer_Rac_rxen_SET = 0x10DC,
            Consumer_Rac_seq_SET = 0x10E0,
            Consumer_Rac_txen_SET = 0x10E4,
            Consumer_Rtcc_cc0_SET = 0x10E8,
            Consumer_Rtcc_cc1_SET = 0x10EC,
            Consumer_Rtcc_cc2_SET = 0x10F0,
            Consumer_Core_ctiin0_SET = 0x10F8,
            Consumer_Core_ctiin1_SET = 0x10FC,
            Consumer_Core_ctiin2_SET = 0x1100,
            Consumer_Core_ctiin3_SET = 0x1104,
            Consumer_Core_m33rxev_SET = 0x1108,
            Consumer_Timer0_cc0_SET = 0x110C,
            Consumer_Timer0_cc1_SET = 0x1110,
            Consumer_Timer0_cc2_SET = 0x1114,
            Consumer_Timer0_dti_SET = 0x1118,
            Consumer_Timer0_dtifs1_SET = 0x111C,
            Consumer_Timer0_dtifs2_SET = 0x1120,
            Consumer_Timer1_cc0_SET = 0x1124,
            Consumer_Timer1_cc1_SET = 0x1128,
            Consumer_Timer1_cc2_SET = 0x112C,
            Consumer_Timer1_dti_SET = 0x1130,
            Consumer_Timer1_dtifs1_SET = 0x1134,
            Consumer_Timer1_dtifs2_SET = 0x1138,
            Consumer_Timer2_cc0_SET = 0x113C,
            Consumer_Timer2_cc1_SET = 0x1140,
            Consumer_Timer2_cc2_SET = 0x1144,
            Consumer_Timer2_dti_SET = 0x1148,
            Consumer_Timer2_dtifs1_SET = 0x114C,
            Consumer_Timer2_dtifs2_SET = 0x1150,
            Consumer_Timer3_cc0_SET = 0x1154,
            Consumer_Timer3_cc1_SET = 0x1158,
            Consumer_Timer3_cc2_SET = 0x115C,
            Consumer_Timer3_dti_SET = 0x1160,
            Consumer_Timer3_dtifs1_SET = 0x1164,
            Consumer_Timer3_dtifs2_SET = 0x1168,
            Consumer_Timer4_cc0_SET = 0x116C,
            Consumer_Timer4_cc1_SET = 0x1170,
            Consumer_Timer4_cc2_SET = 0x1174,
            Consumer_Timer4_dti_SET = 0x1178,
            Consumer_Timer4_dtifs1_SET = 0x117C,
            Consumer_Timer4_dtifs2_SET = 0x1180,
            Consumer_Usart0_clk_SET = 0x1184,
            Consumer_Usart0_ir_SET = 0x1188,
            Consumer_Usart0_rx_SET = 0x118C,
            Consumer_Usart0_trigger_SET = 0x1190,
            Consumer_Usart1_clk_SET = 0x1194,
            Consumer_Usart1_ir_SET = 0x1198,
            Consumer_Usart1_rx_SET = 0x119C,
            Consumer_Usart1_trigger_SET = 0x11A0,
            Consumer_Wdog0_src0_SET = 0x11A4,
            Consumer_Wdog0_src1_SET = 0x11A8,
            
            Ipversion_CLR = 0x2000,
            Enable_CLR = 0x2004,
            Async_swpulse_CLR = 0x2008,
            Async_swlevel_CLR = 0x200C,
            Async_peek_CLR = 0x2010,
            Sync_peek_CLR = 0x2014,
            Async_Ch0_Ctrl_CLR = 0x2018,
            Async_Ch1_Ctrl_CLR = 0x201C,
            Async_Ch2_Ctrl_CLR = 0x2020,
            Async_Ch3_Ctrl_CLR = 0x2024,
            Async_Ch4_Ctrl_CLR = 0x2028,
            Async_Ch5_Ctrl_CLR = 0x202C,
            Async_Ch6_Ctrl_CLR = 0x2030,
            Async_Ch7_Ctrl_CLR = 0x2034,
            Async_Ch8_Ctrl_CLR = 0x2038,
            Async_Ch9_Ctrl_CLR = 0x203C,
            Async_Ch10_Ctrl_CLR = 0x2040,
            Async_Ch11_Ctrl_CLR = 0x2044,
            Sync_Ch0_Ctrl_CLR = 0x2048,
            Sync_Ch1_Ctrl_CLR = 0x204C,
            Sync_Ch2_Ctrl_CLR = 0x2050,
            Sync_Ch3_Ctrl_CLR = 0x2054,
            Consumer_Cmu_caldn_CLR = 0x2058,
            Consumer_Cmu_calup_CLR = 0x205C,
            Consumer_Frc_rxraw_CLR = 0x2060,
            Consumer_Iadc0_scantrigger_CLR = 0x2064,
            Consumer_Iadc0_singletrigger_CLR = 0x2068,
            Consumer_Ldmaxbar_dmareq0_CLR = 0x206C,
            Consumer_Ldmaxbar_dmareq1_CLR = 0x2070,
            Consumer_letimer0_Clear_CLR = 0x2074,
            Consumer_letimer0_Start_CLR = 0x2078,
            Consumer_letimer0_Stop_CLR = 0x207C,
            Consumer_euart0_Rx_CLR = 0x2080,
            Consumer_euart0_Trigger_CLR = 0x2084,
            Consumer_Modem_din_CLR = 0x2088,
            Consumer_Prortc_cc0_CLR = 0x208C,
            Consumer_Prortc_cc1_CLR = 0x2090,
            Consumer_Protimer_cc0_CLR = 0x2094,
            Consumer_Protimer_cc1_CLR = 0x2098,
            Consumer_Protimer_cc2_CLR = 0x209C,
            Consumer_Protimer_cc3_CLR = 0x20A0,
            Consumer_Protimer_cc4_CLR = 0x20A4,
            Consumer_Protimer_lbtpause_CLR = 0x20A8,
            Consumer_Protimer_lbtstart_CLR = 0x20AC,
            Consumer_Protimer_lbtstop_CLR = 0x20B0,
            Consumer_Protimer_rtcctrigger_CLR = 0x20B4,
            Consumer_Protimer_start_CLR = 0x20B8,
            Consumer_Protimer_stop_CLR = 0x20BC,
            Consumer_Rac_clr_CLR = 0x20C0,
            Consumer_Rac_ctiin0_CLR = 0x20C4,
            Consumer_Rac_ctiin1_CLR = 0x20C8,
            Consumer_Rac_ctiin2_CLR = 0x20CC,
            Consumer_Rac_ctiin3_CLR = 0x20D0,
            Consumer_Rac_forcetx_CLR = 0x20D4,
            Consumer_Rac_rxdis_CLR = 0x20D8,
            Consumer_Rac_rxen_CLR = 0x20DC,
            Consumer_Rac_seq_CLR = 0x20E0,
            Consumer_Rac_txen_CLR = 0x20E4,
            Consumer_Rtcc_cc0_CLR = 0x20E8,
            Consumer_Rtcc_cc1_CLR = 0x20EC,
            Consumer_Rtcc_cc2_CLR = 0x20F0,
            Consumer_Core_ctiin0_CLR = 0x20F8,
            Consumer_Core_ctiin1_CLR = 0x20FC,
            Consumer_Core_ctiin2_CLR = 0x2100,
            Consumer_Core_ctiin3_CLR = 0x2104,
            Consumer_Core_m33rxev_CLR = 0x2108,
            Consumer_Timer0_cc0_CLR = 0x210C,
            Consumer_Timer0_cc1_CLR = 0x2110,
            Consumer_Timer0_cc2_CLR = 0x2114,
            Consumer_Timer0_dti_CLR = 0x2118,
            Consumer_Timer0_dtifs1_CLR = 0x211C,
            Consumer_Timer0_dtifs2_CLR = 0x2120,
            Consumer_Timer1_cc0_CLR = 0x2124,
            Consumer_Timer1_cc1_CLR = 0x2128,
            Consumer_Timer1_cc2_CLR = 0x212C,
            Consumer_Timer1_dti_CLR = 0x2130,
            Consumer_Timer1_dtifs1_CLR = 0x2134,
            Consumer_Timer1_dtifs2_CLR = 0x2138,
            Consumer_Timer2_cc0_CLR = 0x213C,
            Consumer_Timer2_cc1_CLR = 0x2140,
            Consumer_Timer2_cc2_CLR = 0x2144,
            Consumer_Timer2_dti_CLR = 0x2148,
            Consumer_Timer2_dtifs1_CLR = 0x214C,
            Consumer_Timer2_dtifs2_CLR = 0x2150,
            Consumer_Timer3_cc0_CLR = 0x2154,
            Consumer_Timer3_cc1_CLR = 0x2158,
            Consumer_Timer3_cc2_CLR = 0x215C,
            Consumer_Timer3_dti_CLR = 0x2160,
            Consumer_Timer3_dtifs1_CLR = 0x2164,
            Consumer_Timer3_dtifs2_CLR = 0x2168,
            Consumer_Timer4_cc0_CLR = 0x216C,
            Consumer_Timer4_cc1_CLR = 0x2170,
            Consumer_Timer4_cc2_CLR = 0x2174,
            Consumer_Timer4_dti_CLR = 0x2178,
            Consumer_Timer4_dtifs1_CLR = 0x217C,
            Consumer_Timer4_dtifs2_CLR = 0x2180,
            Consumer_Usart0_clk_CLR = 0x2184,
            Consumer_Usart0_ir_CLR = 0x2188,
            Consumer_Usart0_rx_CLR = 0x218C,
            Consumer_Usart0_trigger_CLR = 0x2190,
            Consumer_Usart1_clk_CLR = 0x2194,
            Consumer_Usart1_ir_CLR = 0x2198,
            Consumer_Usart1_rx_CLR = 0x219C,
            Consumer_Usart1_trigger_CLR = 0x21A0,
            Consumer_Wdog0_src0_CLR = 0x21A4,
            Consumer_Wdog0_src1_CLR = 0x21A8,
            
            Ipversion_TGL = 0x3000,
            Enable_TGL = 0x3004,
            Async_swpulse_TGL = 0x3008,
            Async_swlevel_TGL = 0x300C,
            Async_peek_TGL = 0x3010,
            Sync_peek_TGL = 0x3014,
            Async_Ch0_Ctrl_TGL = 0x3018,
            Async_Ch1_Ctrl_TGL = 0x301C,
            Async_Ch2_Ctrl_TGL = 0x3020,
            Async_Ch3_Ctrl_TGL = 0x3024,
            Async_Ch4_Ctrl_TGL = 0x3028,
            Async_Ch5_Ctrl_TGL = 0x302C,
            Async_Ch6_Ctrl_TGL = 0x3030,
            Async_Ch7_Ctrl_TGL = 0x3034,
            Async_Ch8_Ctrl_TGL = 0x3038,
            Async_Ch9_Ctrl_TGL = 0x303C,
            Async_Ch10_Ctrl_TGL = 0x3040,
            Async_Ch11_Ctrl_TGL = 0x3044,
            Sync_Ch0_Ctrl_TGL = 0x3048,
            Sync_Ch1_Ctrl_TGL = 0x304C,
            Sync_Ch2_Ctrl_TGL = 0x3050,
            Sync_Ch3_Ctrl_TGL = 0x3054,
            Consumer_Cmu_caldn_TGL = 0x3058,
            Consumer_Cmu_calup_TGL = 0x305C,
            Consumer_Frc_rxraw_TGL = 0x3060,
            Consumer_Iadc0_scantrigger_TGL = 0x3064,
            Consumer_Iadc0_singletrigger_TGL = 0x3068,
            Consumer_Ldmaxbar_dmareq0_TGL = 0x306C,
            Consumer_Ldmaxbar_dmareq1_TGL = 0x3070,
            Consumer_letimer0_Clear_TGL = 0x3074,
            Consumer_letimer0_Start_TGL = 0x3078,
            Consumer_letimer0_Stop_TGL = 0x307C,
            Consumer_euart0_Rx_TGL = 0x3080,
            Consumer_euart0_Trigger_TGL = 0x3084,
            Consumer_Modem_din_TGL = 0x3088,
            Consumer_Prortc_cc0_TGL = 0x308C,
            Consumer_Prortc_cc1_TGL = 0x3090,
            Consumer_Protimer_cc0_TGL = 0x3094,
            Consumer_Protimer_cc1_TGL = 0x3098,
            Consumer_Protimer_cc2_TGL = 0x309C,
            Consumer_Protimer_cc3_TGL = 0x30A0,
            Consumer_Protimer_cc4_TGL = 0x30A4,
            Consumer_Protimer_lbtpause_TGL = 0x30A8,
            Consumer_Protimer_lbtstart_TGL = 0x30AC,
            Consumer_Protimer_lbtstop_TGL = 0x30B0,
            Consumer_Protimer_rtcctrigger_TGL = 0x30B4,
            Consumer_Protimer_start_TGL = 0x30B8,
            Consumer_Protimer_stop_TGL = 0x30BC,
            Consumer_Rac_clr_TGL = 0x30C0,
            Consumer_Rac_ctiin0_TGL = 0x30C4,
            Consumer_Rac_ctiin1_TGL = 0x30C8,
            Consumer_Rac_ctiin2_TGL = 0x30CC,
            Consumer_Rac_ctiin3_TGL = 0x30D0,
            Consumer_Rac_forcetx_TGL = 0x30D4,
            Consumer_Rac_rxdis_TGL = 0x30D8,
            Consumer_Rac_rxen_TGL = 0x30DC,
            Consumer_Rac_seq_TGL = 0x30E0,
            Consumer_Rac_txen_TGL = 0x30E4,
            Consumer_Rtcc_cc0_TGL = 0x30E8,
            Consumer_Rtcc_cc1_TGL = 0x30EC,
            Consumer_Rtcc_cc2_TGL = 0x30F0,
            Consumer_Core_ctiin0_TGL = 0x30F8,
            Consumer_Core_ctiin1_TGL = 0x30FC,
            Consumer_Core_ctiin2_TGL = 0x3100,
            Consumer_Core_ctiin3_TGL = 0x3104,
            Consumer_Core_m33rxev_TGL = 0x3108,
            Consumer_Timer0_cc0_TGL = 0x310C,
            Consumer_Timer0_cc1_TGL = 0x3110,
            Consumer_Timer0_cc2_TGL = 0x3114,
            Consumer_Timer0_dti_TGL = 0x3118,
            Consumer_Timer0_dtifs1_TGL = 0x311C,
            Consumer_Timer0_dtifs2_TGL = 0x3120,
            Consumer_Timer1_cc0_TGL = 0x3124,
            Consumer_Timer1_cc1_TGL = 0x3128,
            Consumer_Timer1_cc2_TGL = 0x312C,
            Consumer_Timer1_dti_TGL = 0x3130,
            Consumer_Timer1_dtifs1_TGL = 0x3134,
            Consumer_Timer1_dtifs2_TGL = 0x3138,
            Consumer_Timer2_cc0_TGL = 0x313C,
            Consumer_Timer2_cc1_TGL = 0x3140,
            Consumer_Timer2_cc2_TGL = 0x3144,
            Consumer_Timer2_dti_TGL = 0x3148,
            Consumer_Timer2_dtifs1_TGL = 0x314C,
            Consumer_Timer2_dtifs2_TGL = 0x3150,
            Consumer_Timer3_cc0_TGL = 0x3154,
            Consumer_Timer3_cc1_TGL = 0x3158,
            Consumer_Timer3_cc2_TGL = 0x315C,
            Consumer_Timer3_dti_TGL = 0x3160,
            Consumer_Timer3_dtifs1_TGL = 0x3164,
            Consumer_Timer3_dtifs2_TGL = 0x3168,
            Consumer_Timer4_cc0_TGL = 0x316C,
            Consumer_Timer4_cc1_TGL = 0x3170,
            Consumer_Timer4_cc2_TGL = 0x3174,
            Consumer_Timer4_dti_TGL = 0x3178,
            Consumer_Timer4_dtifs1_TGL = 0x317C,
            Consumer_Timer4_dtifs2_TGL = 0x3180,
            Consumer_Usart0_clk_TGL = 0x3184,
            Consumer_Usart0_ir_TGL = 0x3188,
            Consumer_Usart0_rx_TGL = 0x318C,
            Consumer_Usart0_trigger_TGL = 0x3190,
            Consumer_Usart1_clk_TGL = 0x3194,
            Consumer_Usart1_ir_TGL = 0x3198,
            Consumer_Usart1_rx_TGL = 0x319C,
            Consumer_Usart1_trigger_TGL = 0x31A0,
            Consumer_Wdog0_src0_TGL = 0x31A4,
            Consumer_Wdog0_src1_TGL = 0x31A8,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}