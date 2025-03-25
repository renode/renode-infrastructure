//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    PRS, Generated on : 2024-07-31 13:43:44.537334
    PRS, ID Version : 79a781f6f5f142b494139694536aeeb2.3 */

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
    public partial class EFR32xG2_PRS_3
    {
        public EFR32xG2_PRS_3(Machine machine) : base(machine)
        {
            EFR32xG2_PRS_3_constructor();
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
    public partial class EFR32xG2_PRS_3 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_PRS_3(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_PRS_3_Constructor();
        }

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
                {(long)Registers.Async_Ch12_Ctrl, GenerateAsync_ch12_ctrlRegister()},
                {(long)Registers.Async_Ch13_Ctrl, GenerateAsync_ch13_ctrlRegister()},
                {(long)Registers.Async_Ch14_Ctrl, GenerateAsync_ch14_ctrlRegister()},
                {(long)Registers.Async_Ch15_Ctrl, GenerateAsync_ch15_ctrlRegister()},
                {(long)Registers.Sync_Ch0_Ctrl, GenerateSync_ch0_ctrlRegister()},
                {(long)Registers.Sync_Ch1_Ctrl, GenerateSync_ch1_ctrlRegister()},
                {(long)Registers.Sync_Ch2_Ctrl, GenerateSync_ch2_ctrlRegister()},
                {(long)Registers.Sync_Ch3_Ctrl, GenerateSync_ch3_ctrlRegister()},
                {(long)Registers.Consumer_Cmu_caldn, GenerateConsumer_cmu_caldnRegister()},
                {(long)Registers.Consumer_Cmu_calup, GenerateConsumer_cmu_calupRegister()},
                {(long)Registers.Consumer_Eusart0_clk, GenerateConsumer_eusart0_clkRegister()},
                {(long)Registers.Consumer_Eusart0_rx, GenerateConsumer_eusart0_rxRegister()},
                {(long)Registers.Consumer_Eusart0_trigger, GenerateConsumer_eusart0_triggerRegister()},
                {(long)Registers.Consumer_Eusart1_clk, GenerateConsumer_eusart1_clkRegister()},
                {(long)Registers.Consumer_Eusart1_rx, GenerateConsumer_eusart1_rxRegister()},
                {(long)Registers.Consumer_Eusart1_trigger, GenerateConsumer_eusart1_triggerRegister()},
                {(long)Registers.Consumer_Frc_rxraw, GenerateConsumer_frc_rxrawRegister()},
                {(long)Registers.Consumer_Iadc0_scantrigger, GenerateConsumer_iadc0_scantriggerRegister()},
                {(long)Registers.Consumer_Iadc0_singletrigger, GenerateConsumer_iadc0_singletriggerRegister()},
                {(long)Registers.Consumer_Ldmaxbar_dmareq0, GenerateConsumer_ldmaxbar_dmareq0Register()},
                {(long)Registers.Consumer_Ldmaxbar_dmareq1, GenerateConsumer_ldmaxbar_dmareq1Register()},
                {(long)Registers.Consumer_Letimer0_clear, GenerateConsumer_letimer0_clearRegister()},
                {(long)Registers.Consumer_Letimer0_start, GenerateConsumer_letimer0_startRegister()},
                {(long)Registers.Consumer_Letimer0_stop, GenerateConsumer_letimer0_stopRegister()},
                {(long)Registers.Consumer_Modem_din, GenerateConsumer_modem_dinRegister()},
                {(long)Registers.Consumer_Modem_paen, GenerateConsumer_modem_paenRegister()},
                {(long)Registers.Consumer_Pcnt0_s0in, GenerateConsumer_pcnt0_s0inRegister()},
                {(long)Registers.Consumer_Pcnt0_s1in, GenerateConsumer_pcnt0_s1inRegister()},
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
                {(long)Registers.Consumer_Rac_txen, GenerateConsumer_rac_txenRegister()},
                {(long)Registers.Consumer_Setamper_tampersrc25, GenerateConsumer_setamper_tampersrc25Register()},
                {(long)Registers.Consumer_Setamper_tampersrc26, GenerateConsumer_setamper_tampersrc26Register()},
                {(long)Registers.Consumer_Setamper_tampersrc27, GenerateConsumer_setamper_tampersrc27Register()},
                {(long)Registers.Consumer_Setamper_tampersrc28, GenerateConsumer_setamper_tampersrc28Register()},
                {(long)Registers.Consumer_Setamper_tampersrc29, GenerateConsumer_setamper_tampersrc29Register()},
                {(long)Registers.Consumer_Setamper_tampersrc30, GenerateConsumer_setamper_tampersrc30Register()},
                {(long)Registers.Consumer_Setamper_tampersrc31, GenerateConsumer_setamper_tampersrc31Register()},
                {(long)Registers.Consumer_Sysrtc0_in0, GenerateConsumer_sysrtc0_in0Register()},
                {(long)Registers.Consumer_Sysrtc0_in1, GenerateConsumer_sysrtc0_in1Register()},
                {(long)Registers.Consumer_Hfxo0_oscreq, GenerateConsumer_hfxo0_oscreqRegister()},
                {(long)Registers.Consumer_Hfxo0_timeout, GenerateConsumer_hfxo0_timeoutRegister()},
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
                {(long)Registers.Consumer_Vdac0_asynctrigch0, GenerateConsumer_vdac0_asynctrigch0Register()},
                {(long)Registers.Consumer_Vdac0_asynctrigch1, GenerateConsumer_vdac0_asynctrigch1Register()},
                {(long)Registers.Consumer_Vdac0_synctrigch0, GenerateConsumer_vdac0_synctrigch0Register()},
                {(long)Registers.Consumer_Vdac0_synctrigch1, GenerateConsumer_vdac0_synctrigch1Register()},
                {(long)Registers.Consumer_Vdac1_asynctrigch0, GenerateConsumer_vdac1_asynctrigch0Register()},
                {(long)Registers.Consumer_Vdac1_asynctrigch1, GenerateConsumer_vdac1_asynctrigch1Register()},
                {(long)Registers.Consumer_Vdac1_synctrigch0, GenerateConsumer_vdac1_synctrigch0Register()},
                {(long)Registers.Consumer_Vdac1_synctrigch1, GenerateConsumer_vdac1_synctrigch1Register()},
                {(long)Registers.Consumer_Wdog0_src0, GenerateConsumer_wdog0_src0Register()},
                {(long)Registers.Consumer_Wdog0_src1, GenerateConsumer_wdog0_src1Register()},
                {(long)Registers.Consumer_Wdog1_src0, GenerateConsumer_wdog1_src0Register()},
                {(long)Registers.Consumer_Wdog1_src1, GenerateConsumer_wdog1_src1Register()},
                {(long)Registers.Drpu_Rpuratd0, GenerateDrpu_rpuratd0Register()},
                {(long)Registers.Drpu_Rpuratd1, GenerateDrpu_rpuratd1Register()},
                {(long)Registers.Drpu_Rpuratd2, GenerateDrpu_rpuratd2Register()},
                {(long)Registers.Drpu_Rpuratd3, GenerateDrpu_rpuratd3Register()},
                {(long)Registers.Drpu_Rpuratd4, GenerateDrpu_rpuratd4Register()},
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
        
        protected enum SYNC_CH_CTRL_SIGSEL
        {
            NONE = 0, // 
        }
        
        // Ipversion - Offset : 0x0
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
        
        // Enable - Offset : 0x4
        protected DoubleWordRegister  GenerateEnableRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateAsync_swpulseRegister() => new DoubleWordRegister(this, 0x0)
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
            .WithFlag(12, out async_swpulse_ch12pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch12pulse_Write(_, __),
                    name: "Ch12pulse")
            .WithFlag(13, out async_swpulse_ch13pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch13pulse_Write(_, __),
                    name: "Ch13pulse")
            .WithFlag(14, out async_swpulse_ch14pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch14pulse_Write(_, __),
                    name: "Ch14pulse")
            .WithFlag(15, out async_swpulse_ch15pulse_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Async_swpulse_Ch15pulse_Write(_, __),
                    name: "Ch15pulse")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Async_swpulse_Read(_, __))
            .WithWriteCallback((_, __) => Async_swpulse_Write(_, __));
        
        // Async_swlevel - Offset : 0xC
        protected DoubleWordRegister  GenerateAsync_swlevelRegister() => new DoubleWordRegister(this, 0x0)
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
            .WithFlag(12, out async_swlevel_ch12level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch12level_ValueProvider(_);
                        return async_swlevel_ch12level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch12level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch12level_Read(_, __),
                    name: "Ch12level")
            .WithFlag(13, out async_swlevel_ch13level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch13level_ValueProvider(_);
                        return async_swlevel_ch13level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch13level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch13level_Read(_, __),
                    name: "Ch13level")
            .WithFlag(14, out async_swlevel_ch14level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch14level_ValueProvider(_);
                        return async_swlevel_ch14level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch14level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch14level_Read(_, __),
                    name: "Ch14level")
            .WithFlag(15, out async_swlevel_ch15level_bit, 
                    valueProviderCallback: (_) => {
                        Async_swlevel_Ch15level_ValueProvider(_);
                        return async_swlevel_ch15level_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Async_swlevel_Ch15level_Write(_, __),
                    
                    readCallback: (_, __) => Async_swlevel_Ch15level_Read(_, __),
                    name: "Ch15level")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Async_swlevel_Read(_, __))
            .WithWriteCallback((_, __) => Async_swlevel_Write(_, __));
        
        // Async_peek - Offset : 0x10
        protected DoubleWordRegister  GenerateAsync_peekRegister() => new DoubleWordRegister(this, 0x0)
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
            .WithFlag(12, out async_peek_ch12val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch12val_ValueProvider(_);
                        return async_peek_ch12val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch12val_Read(_, __),
                    name: "Ch12val")
            .WithFlag(13, out async_peek_ch13val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch13val_ValueProvider(_);
                        return async_peek_ch13val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch13val_Read(_, __),
                    name: "Ch13val")
            .WithFlag(14, out async_peek_ch14val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch14val_ValueProvider(_);
                        return async_peek_ch14val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch14val_Read(_, __),
                    name: "Ch14val")
            .WithFlag(15, out async_peek_ch15val_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Async_peek_Ch15val_ValueProvider(_);
                        return async_peek_ch15val_bit.Value;
                    },
                    
                    readCallback: (_, __) => Async_peek_Ch15val_Read(_, __),
                    name: "Ch15val")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Async_peek_Read(_, __))
            .WithWriteCallback((_, __) => Async_peek_Write(_, __));
        
        // Sync_peek - Offset : 0x14
        protected DoubleWordRegister  GenerateSync_peekRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateAsync_ch0_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch1_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch2_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch3_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch4_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch5_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch6_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch7_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch8_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch9_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch10_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        protected DoubleWordRegister  GenerateAsync_ch11_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
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
        
        // Async_Ch12_Ctrl - Offset : 0x48
        protected DoubleWordRegister  GenerateAsync_ch12_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[12], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(12, _);
                        return async_ch_ctrl_sigsel_field[12].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(12,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(12,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[12], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(12, _);
                        return async_ch_ctrl_sourcesel_field[12].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(12,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(12,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[12], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(12, _);
                        return async_ch_ctrl_fnsel_field[12].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(12,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(12,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[12], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(12, _);
                        return async_ch_ctrl_auxsel_field[12].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(12,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(12,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(12, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(12, _, __));
        
        // Async_Ch13_Ctrl - Offset : 0x4C
        protected DoubleWordRegister  GenerateAsync_ch13_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[13], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(13, _);
                        return async_ch_ctrl_sigsel_field[13].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(13,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(13,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[13], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(13, _);
                        return async_ch_ctrl_sourcesel_field[13].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(13,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(13,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[13], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(13, _);
                        return async_ch_ctrl_fnsel_field[13].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(13,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(13,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[13], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(13, _);
                        return async_ch_ctrl_auxsel_field[13].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(13,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(13,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(13, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(13, _, __));
        
        // Async_Ch14_Ctrl - Offset : 0x50
        protected DoubleWordRegister  GenerateAsync_ch14_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[14], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(14, _);
                        return async_ch_ctrl_sigsel_field[14].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(14,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(14,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[14], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(14, _);
                        return async_ch_ctrl_sourcesel_field[14].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(14,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(14,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[14], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(14, _);
                        return async_ch_ctrl_fnsel_field[14].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(14,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(14,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[14], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(14, _);
                        return async_ch_ctrl_auxsel_field[14].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(14,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(14,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(14, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(14, _, __));
        
        // Async_Ch15_Ctrl - Offset : 0x54
        protected DoubleWordRegister  GenerateAsync_ch15_ctrlRegister() => new DoubleWordRegister(this, 0xC0000)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_SIGSEL>(0, 3, out async_ch_ctrl_sigsel_field[15], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sigsel_ValueProvider(15, _);
                        return async_ch_ctrl_sigsel_field[15].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Write(15,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sigsel_Read(15,_, __),
                    name: "Sigsel")
            .WithReservedBits(3, 5)
            .WithValueField(8, 7, out async_ch_ctrl_sourcesel_field[15], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Sourcesel_ValueProvider(15, _);
                        return async_ch_ctrl_sourcesel_field[15].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Write(15,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Sourcesel_Read(15,_, __),
                    name: "Sourcesel")
            .WithReservedBits(15, 1)
            .WithEnumField<DoubleWordRegister, ASYNC_CH_CTRL_FNSEL>(16, 4, out async_ch_ctrl_fnsel_field[15], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Fnsel_ValueProvider(15, _);
                        return async_ch_ctrl_fnsel_field[15].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Write(15,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Fnsel_Read(15,_, __),
                    name: "Fnsel")
            .WithReservedBits(20, 4)
            .WithValueField(24, 4, out async_ch_ctrl_auxsel_field[15], 
                    valueProviderCallback: (_) => {
                        Async_Ch_Ctrl_Auxsel_ValueProvider(15, _);
                        return async_ch_ctrl_auxsel_field[15].Value;
                    },
                    
                    writeCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Write(15,_, __),
                    
                    readCallback: (_, __) => Async_Ch_Ctrl_Auxsel_Read(15,_, __),
                    name: "Auxsel")
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Async_Ch_Ctrl_Read(15, _, __))
            .WithWriteCallback((_, __) => Async_Ch_Ctrl_Write(15, _, __));
        
        // Sync_Ch0_Ctrl - Offset : 0x58
        protected DoubleWordRegister  GenerateSync_ch0_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, SYNC_CH_CTRL_SIGSEL>(0, 3, out sync_ch_ctrl_sigsel_field[0], 
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
        
        // Sync_Ch1_Ctrl - Offset : 0x5C
        protected DoubleWordRegister  GenerateSync_ch1_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, SYNC_CH_CTRL_SIGSEL>(0, 3, out sync_ch_ctrl_sigsel_field[1], 
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
        
        // Sync_Ch2_Ctrl - Offset : 0x60
        protected DoubleWordRegister  GenerateSync_ch2_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, SYNC_CH_CTRL_SIGSEL>(0, 3, out sync_ch_ctrl_sigsel_field[2], 
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
        
        // Sync_Ch3_Ctrl - Offset : 0x64
        protected DoubleWordRegister  GenerateSync_ch3_ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, SYNC_CH_CTRL_SIGSEL>(0, 3, out sync_ch_ctrl_sigsel_field[3], 
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
        
        // Consumer_Cmu_caldn - Offset : 0x68
        protected DoubleWordRegister  GenerateConsumer_cmu_caldnRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Cmu_calup - Offset : 0x6C
        protected DoubleWordRegister  GenerateConsumer_cmu_calupRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Eusart0_clk - Offset : 0x70
        protected DoubleWordRegister  GenerateConsumer_eusart0_clkRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_eusart0_clk_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Eusart0_clk_Prssel_ValueProvider(_);
                        return consumer_eusart0_clk_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Eusart0_clk_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Eusart0_clk_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Eusart0_clk_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Eusart0_clk_Write(_, __));
        
        // Consumer_Eusart0_rx - Offset : 0x74
        protected DoubleWordRegister  GenerateConsumer_eusart0_rxRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_eusart0_rx_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Eusart0_rx_Prssel_ValueProvider(_);
                        return consumer_eusart0_rx_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Eusart0_rx_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Eusart0_rx_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Eusart0_rx_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Eusart0_rx_Write(_, __));
        
        // Consumer_Eusart0_trigger - Offset : 0x78
        protected DoubleWordRegister  GenerateConsumer_eusart0_triggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_eusart0_trigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Eusart0_trigger_Prssel_ValueProvider(_);
                        return consumer_eusart0_trigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Eusart0_trigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Eusart0_trigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Eusart0_trigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Eusart0_trigger_Write(_, __));
        
        // Consumer_Eusart1_clk - Offset : 0x7C
        protected DoubleWordRegister  GenerateConsumer_eusart1_clkRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_eusart1_clk_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Eusart1_clk_Prssel_ValueProvider(_);
                        return consumer_eusart1_clk_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Eusart1_clk_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Eusart1_clk_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Eusart1_clk_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Eusart1_clk_Write(_, __));
        
        // Consumer_Eusart1_rx - Offset : 0x80
        protected DoubleWordRegister  GenerateConsumer_eusart1_rxRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_eusart1_rx_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Eusart1_rx_Prssel_ValueProvider(_);
                        return consumer_eusart1_rx_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Eusart1_rx_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Eusart1_rx_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Eusart1_rx_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Eusart1_rx_Write(_, __));
        
        // Consumer_Eusart1_trigger - Offset : 0x84
        protected DoubleWordRegister  GenerateConsumer_eusart1_triggerRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_eusart1_trigger_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Eusart1_trigger_Prssel_ValueProvider(_);
                        return consumer_eusart1_trigger_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Eusart1_trigger_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Eusart1_trigger_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Eusart1_trigger_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Eusart1_trigger_Write(_, __));
        
        // Consumer_Frc_rxraw - Offset : 0x88
        protected DoubleWordRegister  GenerateConsumer_frc_rxrawRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Iadc0_scantrigger - Offset : 0x8C
        protected DoubleWordRegister  GenerateConsumer_iadc0_scantriggerRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Iadc0_singletrigger - Offset : 0x90
        protected DoubleWordRegister  GenerateConsumer_iadc0_singletriggerRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Ldmaxbar_dmareq0 - Offset : 0x94
        protected DoubleWordRegister  GenerateConsumer_ldmaxbar_dmareq0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Ldmaxbar_dmareq1 - Offset : 0x98
        protected DoubleWordRegister  GenerateConsumer_ldmaxbar_dmareq1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Letimer0_clear - Offset : 0x9C
        protected DoubleWordRegister  GenerateConsumer_letimer0_clearRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_letimer0_clear_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Letimer0_clear_Prssel_ValueProvider(_);
                        return consumer_letimer0_clear_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Letimer0_clear_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Letimer0_clear_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Letimer0_clear_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Letimer0_clear_Write(_, __));
        
        // Consumer_Letimer0_start - Offset : 0xA0
        protected DoubleWordRegister  GenerateConsumer_letimer0_startRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_letimer0_start_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Letimer0_start_Prssel_ValueProvider(_);
                        return consumer_letimer0_start_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Letimer0_start_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Letimer0_start_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Letimer0_start_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Letimer0_start_Write(_, __));
        
        // Consumer_Letimer0_stop - Offset : 0xA4
        protected DoubleWordRegister  GenerateConsumer_letimer0_stopRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_letimer0_stop_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Letimer0_stop_Prssel_ValueProvider(_);
                        return consumer_letimer0_stop_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Letimer0_stop_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Letimer0_stop_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Letimer0_stop_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Letimer0_stop_Write(_, __));
        
        // Consumer_Modem_din - Offset : 0xA8
        protected DoubleWordRegister  GenerateConsumer_modem_dinRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Modem_paen - Offset : 0xAC
        protected DoubleWordRegister  GenerateConsumer_modem_paenRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_modem_paen_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Modem_paen_Prssel_ValueProvider(_);
                        return consumer_modem_paen_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Modem_paen_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Modem_paen_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Modem_paen_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Modem_paen_Write(_, __));
        
        // Consumer_Pcnt0_s0in - Offset : 0xB0
        protected DoubleWordRegister  GenerateConsumer_pcnt0_s0inRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_pcnt0_s0in_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Pcnt0_s0in_Prssel_ValueProvider(_);
                        return consumer_pcnt0_s0in_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Pcnt0_s0in_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Pcnt0_s0in_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Pcnt0_s0in_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Pcnt0_s0in_Write(_, __));
        
        // Consumer_Pcnt0_s1in - Offset : 0xB4
        protected DoubleWordRegister  GenerateConsumer_pcnt0_s1inRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_pcnt0_s1in_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Pcnt0_s1in_Prssel_ValueProvider(_);
                        return consumer_pcnt0_s1in_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Pcnt0_s1in_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Pcnt0_s1in_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Pcnt0_s1in_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Pcnt0_s1in_Write(_, __));
        
        // Consumer_Protimer_cc0 - Offset : 0xB8
        protected DoubleWordRegister  GenerateConsumer_protimer_cc0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_cc1 - Offset : 0xBC
        protected DoubleWordRegister  GenerateConsumer_protimer_cc1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_cc2 - Offset : 0xC0
        protected DoubleWordRegister  GenerateConsumer_protimer_cc2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_cc3 - Offset : 0xC4
        protected DoubleWordRegister  GenerateConsumer_protimer_cc3Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_cc4 - Offset : 0xC8
        protected DoubleWordRegister  GenerateConsumer_protimer_cc4Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_lbtpause - Offset : 0xCC
        protected DoubleWordRegister  GenerateConsumer_protimer_lbtpauseRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_lbtstart - Offset : 0xD0
        protected DoubleWordRegister  GenerateConsumer_protimer_lbtstartRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_lbtstop - Offset : 0xD4
        protected DoubleWordRegister  GenerateConsumer_protimer_lbtstopRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_rtcctrigger - Offset : 0xD8
        protected DoubleWordRegister  GenerateConsumer_protimer_rtcctriggerRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_start - Offset : 0xDC
        protected DoubleWordRegister  GenerateConsumer_protimer_startRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Protimer_stop - Offset : 0xE0
        protected DoubleWordRegister  GenerateConsumer_protimer_stopRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_clr - Offset : 0xE4
        protected DoubleWordRegister  GenerateConsumer_rac_clrRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_ctiin0 - Offset : 0xE8
        protected DoubleWordRegister  GenerateConsumer_rac_ctiin0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_ctiin1 - Offset : 0xEC
        protected DoubleWordRegister  GenerateConsumer_rac_ctiin1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_ctiin2 - Offset : 0xF0
        protected DoubleWordRegister  GenerateConsumer_rac_ctiin2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_ctiin3 - Offset : 0xF4
        protected DoubleWordRegister  GenerateConsumer_rac_ctiin3Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_forcetx - Offset : 0xF8
        protected DoubleWordRegister  GenerateConsumer_rac_forcetxRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_rxdis - Offset : 0xFC
        protected DoubleWordRegister  GenerateConsumer_rac_rxdisRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_rxen - Offset : 0x100
        protected DoubleWordRegister  GenerateConsumer_rac_rxenRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Rac_txen - Offset : 0x104
        protected DoubleWordRegister  GenerateConsumer_rac_txenRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Setamper_tampersrc25 - Offset : 0x108
        protected DoubleWordRegister  GenerateConsumer_setamper_tampersrc25Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_setamper_tampersrc25_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Setamper_tampersrc25_Prssel_ValueProvider(_);
                        return consumer_setamper_tampersrc25_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Setamper_tampersrc25_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Setamper_tampersrc25_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Setamper_tampersrc25_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Setamper_tampersrc25_Write(_, __));
        
        // Consumer_Setamper_tampersrc26 - Offset : 0x10C
        protected DoubleWordRegister  GenerateConsumer_setamper_tampersrc26Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_setamper_tampersrc26_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Setamper_tampersrc26_Prssel_ValueProvider(_);
                        return consumer_setamper_tampersrc26_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Setamper_tampersrc26_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Setamper_tampersrc26_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Setamper_tampersrc26_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Setamper_tampersrc26_Write(_, __));
        
        // Consumer_Setamper_tampersrc27 - Offset : 0x110
        protected DoubleWordRegister  GenerateConsumer_setamper_tampersrc27Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_setamper_tampersrc27_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Setamper_tampersrc27_Prssel_ValueProvider(_);
                        return consumer_setamper_tampersrc27_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Setamper_tampersrc27_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Setamper_tampersrc27_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Setamper_tampersrc27_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Setamper_tampersrc27_Write(_, __));
        
        // Consumer_Setamper_tampersrc28 - Offset : 0x114
        protected DoubleWordRegister  GenerateConsumer_setamper_tampersrc28Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_setamper_tampersrc28_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Setamper_tampersrc28_Prssel_ValueProvider(_);
                        return consumer_setamper_tampersrc28_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Setamper_tampersrc28_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Setamper_tampersrc28_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Setamper_tampersrc28_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Setamper_tampersrc28_Write(_, __));
        
        // Consumer_Setamper_tampersrc29 - Offset : 0x118
        protected DoubleWordRegister  GenerateConsumer_setamper_tampersrc29Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_setamper_tampersrc29_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Setamper_tampersrc29_Prssel_ValueProvider(_);
                        return consumer_setamper_tampersrc29_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Setamper_tampersrc29_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Setamper_tampersrc29_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Setamper_tampersrc29_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Setamper_tampersrc29_Write(_, __));
        
        // Consumer_Setamper_tampersrc30 - Offset : 0x11C
        protected DoubleWordRegister  GenerateConsumer_setamper_tampersrc30Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_setamper_tampersrc30_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Setamper_tampersrc30_Prssel_ValueProvider(_);
                        return consumer_setamper_tampersrc30_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Setamper_tampersrc30_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Setamper_tampersrc30_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Setamper_tampersrc30_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Setamper_tampersrc30_Write(_, __));
        
        // Consumer_Setamper_tampersrc31 - Offset : 0x120
        protected DoubleWordRegister  GenerateConsumer_setamper_tampersrc31Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_setamper_tampersrc31_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Setamper_tampersrc31_Prssel_ValueProvider(_);
                        return consumer_setamper_tampersrc31_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Setamper_tampersrc31_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Setamper_tampersrc31_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Setamper_tampersrc31_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Setamper_tampersrc31_Write(_, __));
        
        // Consumer_Sysrtc0_in0 - Offset : 0x124
        protected DoubleWordRegister  GenerateConsumer_sysrtc0_in0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_sysrtc0_in0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Sysrtc0_in0_Prssel_ValueProvider(_);
                        return consumer_sysrtc0_in0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Sysrtc0_in0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Sysrtc0_in0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Sysrtc0_in0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Sysrtc0_in0_Write(_, __));
        
        // Consumer_Sysrtc0_in1 - Offset : 0x128
        protected DoubleWordRegister  GenerateConsumer_sysrtc0_in1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_sysrtc0_in1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Sysrtc0_in1_Prssel_ValueProvider(_);
                        return consumer_sysrtc0_in1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Sysrtc0_in1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Sysrtc0_in1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Sysrtc0_in1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Sysrtc0_in1_Write(_, __));
        
        // Consumer_Hfxo0_oscreq - Offset : 0x12C
        protected DoubleWordRegister  GenerateConsumer_hfxo0_oscreqRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_hfxo0_oscreq_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Hfxo0_oscreq_Prssel_ValueProvider(_);
                        return consumer_hfxo0_oscreq_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Hfxo0_oscreq_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Hfxo0_oscreq_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Hfxo0_oscreq_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Hfxo0_oscreq_Write(_, __));
        
        // Consumer_Hfxo0_timeout - Offset : 0x130
        protected DoubleWordRegister  GenerateConsumer_hfxo0_timeoutRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_hfxo0_timeout_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Hfxo0_timeout_Prssel_ValueProvider(_);
                        return consumer_hfxo0_timeout_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Hfxo0_timeout_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Hfxo0_timeout_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Hfxo0_timeout_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Hfxo0_timeout_Write(_, __));
        
        // Consumer_Core_ctiin0 - Offset : 0x134
        protected DoubleWordRegister  GenerateConsumer_core_ctiin0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Core_ctiin1 - Offset : 0x138
        protected DoubleWordRegister  GenerateConsumer_core_ctiin1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Core_ctiin2 - Offset : 0x13C
        protected DoubleWordRegister  GenerateConsumer_core_ctiin2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Core_ctiin3 - Offset : 0x140
        protected DoubleWordRegister  GenerateConsumer_core_ctiin3Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Core_m33rxev - Offset : 0x144
        protected DoubleWordRegister  GenerateConsumer_core_m33rxevRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer0_cc0 - Offset : 0x148
        protected DoubleWordRegister  GenerateConsumer_timer0_cc0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer0_cc1 - Offset : 0x14C
        protected DoubleWordRegister  GenerateConsumer_timer0_cc1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer0_cc2 - Offset : 0x150
        protected DoubleWordRegister  GenerateConsumer_timer0_cc2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer0_dti - Offset : 0x154
        protected DoubleWordRegister  GenerateConsumer_timer0_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer0_dtifs1 - Offset : 0x158
        protected DoubleWordRegister  GenerateConsumer_timer0_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer0_dtifs2 - Offset : 0x15C
        protected DoubleWordRegister  GenerateConsumer_timer0_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer1_cc0 - Offset : 0x160
        protected DoubleWordRegister  GenerateConsumer_timer1_cc0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer1_cc1 - Offset : 0x164
        protected DoubleWordRegister  GenerateConsumer_timer1_cc1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer1_cc2 - Offset : 0x168
        protected DoubleWordRegister  GenerateConsumer_timer1_cc2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer1_dti - Offset : 0x16C
        protected DoubleWordRegister  GenerateConsumer_timer1_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer1_dtifs1 - Offset : 0x170
        protected DoubleWordRegister  GenerateConsumer_timer1_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer1_dtifs2 - Offset : 0x174
        protected DoubleWordRegister  GenerateConsumer_timer1_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer2_cc0 - Offset : 0x178
        protected DoubleWordRegister  GenerateConsumer_timer2_cc0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer2_cc1 - Offset : 0x17C
        protected DoubleWordRegister  GenerateConsumer_timer2_cc1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer2_cc2 - Offset : 0x180
        protected DoubleWordRegister  GenerateConsumer_timer2_cc2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer2_dti - Offset : 0x184
        protected DoubleWordRegister  GenerateConsumer_timer2_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer2_dtifs1 - Offset : 0x188
        protected DoubleWordRegister  GenerateConsumer_timer2_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer2_dtifs2 - Offset : 0x18C
        protected DoubleWordRegister  GenerateConsumer_timer2_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer3_cc0 - Offset : 0x190
        protected DoubleWordRegister  GenerateConsumer_timer3_cc0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer3_cc1 - Offset : 0x194
        protected DoubleWordRegister  GenerateConsumer_timer3_cc1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer3_cc2 - Offset : 0x198
        protected DoubleWordRegister  GenerateConsumer_timer3_cc2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer3_dti - Offset : 0x19C
        protected DoubleWordRegister  GenerateConsumer_timer3_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer3_dtifs1 - Offset : 0x1A0
        protected DoubleWordRegister  GenerateConsumer_timer3_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer3_dtifs2 - Offset : 0x1A4
        protected DoubleWordRegister  GenerateConsumer_timer3_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer4_cc0 - Offset : 0x1A8
        protected DoubleWordRegister  GenerateConsumer_timer4_cc0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer4_cc1 - Offset : 0x1AC
        protected DoubleWordRegister  GenerateConsumer_timer4_cc1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer4_cc2 - Offset : 0x1B0
        protected DoubleWordRegister  GenerateConsumer_timer4_cc2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer4_dti - Offset : 0x1B4
        protected DoubleWordRegister  GenerateConsumer_timer4_dtiRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer4_dtifs1 - Offset : 0x1B8
        protected DoubleWordRegister  GenerateConsumer_timer4_dtifs1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Timer4_dtifs2 - Offset : 0x1BC
        protected DoubleWordRegister  GenerateConsumer_timer4_dtifs2Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Usart0_clk - Offset : 0x1C0
        protected DoubleWordRegister  GenerateConsumer_usart0_clkRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Usart0_ir - Offset : 0x1C4
        protected DoubleWordRegister  GenerateConsumer_usart0_irRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Usart0_rx - Offset : 0x1C8
        protected DoubleWordRegister  GenerateConsumer_usart0_rxRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Usart0_trigger - Offset : 0x1CC
        protected DoubleWordRegister  GenerateConsumer_usart0_triggerRegister() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Vdac0_asynctrigch0 - Offset : 0x1DC
        protected DoubleWordRegister  GenerateConsumer_vdac0_asynctrigch0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_vdac0_asynctrigch0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac0_asynctrigch0_Prssel_ValueProvider(_);
                        return consumer_vdac0_asynctrigch0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac0_asynctrigch0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac0_asynctrigch0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Vdac0_asynctrigch0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac0_asynctrigch0_Write(_, __));
        
        // Consumer_Vdac0_asynctrigch1 - Offset : 0x1E0
        protected DoubleWordRegister  GenerateConsumer_vdac0_asynctrigch1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_vdac0_asynctrigch1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac0_asynctrigch1_Prssel_ValueProvider(_);
                        return consumer_vdac0_asynctrigch1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac0_asynctrigch1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac0_asynctrigch1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Vdac0_asynctrigch1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac0_asynctrigch1_Write(_, __));
        
        // Consumer_Vdac0_synctrigch0 - Offset : 0x1E4
        protected DoubleWordRegister  GenerateConsumer_vdac0_synctrigch0Register() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 8)
            
            .WithValueField(8, 2, out consumer_vdac0_synctrigch0_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac0_synctrigch0_Sprssel_ValueProvider(_);
                        return consumer_vdac0_synctrigch0_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac0_synctrigch0_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac0_synctrigch0_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Vdac0_synctrigch0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac0_synctrigch0_Write(_, __));
        
        // Consumer_Vdac0_synctrigch1 - Offset : 0x1E8
        protected DoubleWordRegister  GenerateConsumer_vdac0_synctrigch1Register() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 8)
            
            .WithValueField(8, 2, out consumer_vdac0_synctrigch1_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac0_synctrigch1_Sprssel_ValueProvider(_);
                        return consumer_vdac0_synctrigch1_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac0_synctrigch1_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac0_synctrigch1_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Vdac0_synctrigch1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac0_synctrigch1_Write(_, __));
        
        // Consumer_Vdac1_asynctrigch0 - Offset : 0x1EC
        protected DoubleWordRegister  GenerateConsumer_vdac1_asynctrigch0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_vdac1_asynctrigch0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac1_asynctrigch0_Prssel_ValueProvider(_);
                        return consumer_vdac1_asynctrigch0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac1_asynctrigch0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac1_asynctrigch0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Vdac1_asynctrigch0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac1_asynctrigch0_Write(_, __));
        
        // Consumer_Vdac1_asynctrigch1 - Offset : 0x1F0
        protected DoubleWordRegister  GenerateConsumer_vdac1_asynctrigch1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_vdac1_asynctrigch1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac1_asynctrigch1_Prssel_ValueProvider(_);
                        return consumer_vdac1_asynctrigch1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac1_asynctrigch1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac1_asynctrigch1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Vdac1_asynctrigch1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac1_asynctrigch1_Write(_, __));
        
        // Consumer_Vdac1_synctrigch0 - Offset : 0x1F4
        protected DoubleWordRegister  GenerateConsumer_vdac1_synctrigch0Register() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 8)
            
            .WithValueField(8, 2, out consumer_vdac1_synctrigch0_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac1_synctrigch0_Sprssel_ValueProvider(_);
                        return consumer_vdac1_synctrigch0_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac1_synctrigch0_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac1_synctrigch0_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Vdac1_synctrigch0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac1_synctrigch0_Write(_, __));
        
        // Consumer_Vdac1_synctrigch1 - Offset : 0x1F8
        protected DoubleWordRegister  GenerateConsumer_vdac1_synctrigch1Register() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 8)
            
            .WithValueField(8, 2, out consumer_vdac1_synctrigch1_sprssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Vdac1_synctrigch1_Sprssel_ValueProvider(_);
                        return consumer_vdac1_synctrigch1_sprssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Vdac1_synctrigch1_Sprssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Vdac1_synctrigch1_Sprssel_Read(_, __),
                    name: "Sprssel")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Consumer_Vdac1_synctrigch1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Vdac1_synctrigch1_Write(_, __));
        
        // Consumer_Wdog0_src0 - Offset : 0x1FC
        protected DoubleWordRegister  GenerateConsumer_wdog0_src0Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Wdog0_src1 - Offset : 0x200
        protected DoubleWordRegister  GenerateConsumer_wdog0_src1Register() => new DoubleWordRegister(this, 0x0)
            
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
        
        // Consumer_Wdog1_src0 - Offset : 0x204
        protected DoubleWordRegister  GenerateConsumer_wdog1_src0Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_wdog1_src0_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Wdog1_src0_Prssel_ValueProvider(_);
                        return consumer_wdog1_src0_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Wdog1_src0_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Wdog1_src0_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Wdog1_src0_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Wdog1_src0_Write(_, __));
        
        // Consumer_Wdog1_src1 - Offset : 0x208
        protected DoubleWordRegister  GenerateConsumer_wdog1_src1Register() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 4, out consumer_wdog1_src1_prssel_field, 
                    valueProviderCallback: (_) => {
                        Consumer_Wdog1_src1_Prssel_ValueProvider(_);
                        return consumer_wdog1_src1_prssel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Consumer_Wdog1_src1_Prssel_Write(_, __),
                    
                    readCallback: (_, __) => Consumer_Wdog1_src1_Prssel_Read(_, __),
                    name: "Prssel")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Consumer_Wdog1_src1_Read(_, __))
            .WithWriteCallback((_, __) => Consumer_Wdog1_src1_Write(_, __));
        
        // Drpu_Rpuratd0 - Offset : 0x20C
        protected DoubleWordRegister  GenerateDrpu_rpuratd0Register() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out drpu_rpuratd0_ratdenable_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdenable_ValueProvider(_);
                        return drpu_rpuratd0_ratdenable_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdenable_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdenable_Read(_, __),
                    name: "Ratdenable")
            .WithFlag(2, out drpu_rpuratd0_ratdasyncswpulse_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasyncswpulse_ValueProvider(_);
                        return drpu_rpuratd0_ratdasyncswpulse_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasyncswpulse_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasyncswpulse_Read(_, __),
                    name: "Ratdasyncswpulse")
            .WithFlag(3, out drpu_rpuratd0_ratdasyncswlevel_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasyncswlevel_ValueProvider(_);
                        return drpu_rpuratd0_ratdasyncswlevel_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasyncswlevel_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasyncswlevel_Read(_, __),
                    name: "Ratdasyncswlevel")
            .WithReservedBits(4, 2)
            .WithFlag(6, out drpu_rpuratd0_ratdasynch0ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch0ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch0ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch0ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch0ctrl_Read(_, __),
                    name: "Ratdasynch0ctrl")
            .WithFlag(7, out drpu_rpuratd0_ratdasynch1ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch1ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch1ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch1ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch1ctrl_Read(_, __),
                    name: "Ratdasynch1ctrl")
            .WithFlag(8, out drpu_rpuratd0_ratdasynch2ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch2ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch2ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch2ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch2ctrl_Read(_, __),
                    name: "Ratdasynch2ctrl")
            .WithFlag(9, out drpu_rpuratd0_ratdasynch3ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch3ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch3ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch3ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch3ctrl_Read(_, __),
                    name: "Ratdasynch3ctrl")
            .WithFlag(10, out drpu_rpuratd0_ratdasynch4ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch4ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch4ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch4ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch4ctrl_Read(_, __),
                    name: "Ratdasynch4ctrl")
            .WithFlag(11, out drpu_rpuratd0_ratdasynch5ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch5ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch5ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch5ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch5ctrl_Read(_, __),
                    name: "Ratdasynch5ctrl")
            .WithFlag(12, out drpu_rpuratd0_ratdasynch6ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch6ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch6ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch6ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch6ctrl_Read(_, __),
                    name: "Ratdasynch6ctrl")
            .WithFlag(13, out drpu_rpuratd0_ratdasynch7ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch7ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch7ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch7ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch7ctrl_Read(_, __),
                    name: "Ratdasynch7ctrl")
            .WithFlag(14, out drpu_rpuratd0_ratdasynch8ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch8ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch8ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch8ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch8ctrl_Read(_, __),
                    name: "Ratdasynch8ctrl")
            .WithFlag(15, out drpu_rpuratd0_ratdasynch9ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch9ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch9ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch9ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch9ctrl_Read(_, __),
                    name: "Ratdasynch9ctrl")
            .WithFlag(16, out drpu_rpuratd0_ratdasynch10ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch10ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch10ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch10ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch10ctrl_Read(_, __),
                    name: "Ratdasynch10ctrl")
            .WithFlag(17, out drpu_rpuratd0_ratdasynch11ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch11ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch11ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch11ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch11ctrl_Read(_, __),
                    name: "Ratdasynch11ctrl")
            .WithFlag(18, out drpu_rpuratd0_ratdasynch12ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch12ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch12ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch12ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch12ctrl_Read(_, __),
                    name: "Ratdasynch12ctrl")
            .WithFlag(19, out drpu_rpuratd0_ratdasynch13ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch13ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch13ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch13ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch13ctrl_Read(_, __),
                    name: "Ratdasynch13ctrl")
            .WithFlag(20, out drpu_rpuratd0_ratdasynch14ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch14ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch14ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch14ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch14ctrl_Read(_, __),
                    name: "Ratdasynch14ctrl")
            .WithFlag(21, out drpu_rpuratd0_ratdasynch15ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdasynch15ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdasynch15ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch15ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdasynch15ctrl_Read(_, __),
                    name: "Ratdasynch15ctrl")
            .WithFlag(22, out drpu_rpuratd0_ratdsynch0ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdsynch0ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdsynch0ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch0ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch0ctrl_Read(_, __),
                    name: "Ratdsynch0ctrl")
            .WithFlag(23, out drpu_rpuratd0_ratdsynch1ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdsynch1ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdsynch1ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch1ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch1ctrl_Read(_, __),
                    name: "Ratdsynch1ctrl")
            .WithFlag(24, out drpu_rpuratd0_ratdsynch2ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdsynch2ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdsynch2ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch2ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch2ctrl_Read(_, __),
                    name: "Ratdsynch2ctrl")
            .WithFlag(25, out drpu_rpuratd0_ratdsynch3ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdsynch3ctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdsynch3ctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch3ctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdsynch3ctrl_Read(_, __),
                    name: "Ratdsynch3ctrl")
            .WithFlag(26, out drpu_rpuratd0_ratdcmucaldn_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdcmucaldn_ValueProvider(_);
                        return drpu_rpuratd0_ratdcmucaldn_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdcmucaldn_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdcmucaldn_Read(_, __),
                    name: "Ratdcmucaldn")
            .WithFlag(27, out drpu_rpuratd0_ratdcmucalup_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdcmucalup_ValueProvider(_);
                        return drpu_rpuratd0_ratdcmucalup_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdcmucalup_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdcmucalup_Read(_, __),
                    name: "Ratdcmucalup")
            .WithFlag(28, out drpu_rpuratd0_ratdeusart0clk_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdeusart0clk_ValueProvider(_);
                        return drpu_rpuratd0_ratdeusart0clk_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart0clk_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart0clk_Read(_, __),
                    name: "Ratdeusart0clk")
            .WithFlag(29, out drpu_rpuratd0_ratdeusart0rx_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdeusart0rx_ValueProvider(_);
                        return drpu_rpuratd0_ratdeusart0rx_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart0rx_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart0rx_Read(_, __),
                    name: "Ratdeusart0rx")
            .WithFlag(30, out drpu_rpuratd0_ratdeusart0trigger_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdeusart0trigger_ValueProvider(_);
                        return drpu_rpuratd0_ratdeusart0trigger_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart0trigger_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart0trigger_Read(_, __),
                    name: "Ratdeusart0trigger")
            .WithFlag(31, out drpu_rpuratd0_ratdeusart1clk_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdeusart1clk_ValueProvider(_);
                        return drpu_rpuratd0_ratdeusart1clk_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart1clk_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdeusart1clk_Read(_, __),
                    name: "Ratdeusart1clk")
            .WithReadCallback((_, __) => Drpu_Rpuratd0_Read(_, __))
            .WithWriteCallback((_, __) => Drpu_Rpuratd0_Write(_, __));
        
        // Drpu_Rpuratd1 - Offset : 0x210
        protected DoubleWordRegister  GenerateDrpu_rpuratd1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out drpu_rpuratd1_ratdeusart1rx_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdeusart1rx_ValueProvider(_);
                        return drpu_rpuratd1_ratdeusart1rx_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdeusart1rx_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdeusart1rx_Read(_, __),
                    name: "Ratdeusart1rx")
            .WithFlag(1, out drpu_rpuratd1_ratdeusart1trigger_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdeusart1trigger_ValueProvider(_);
                        return drpu_rpuratd1_ratdeusart1trigger_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdeusart1trigger_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdeusart1trigger_Read(_, __),
                    name: "Ratdeusart1trigger")
            .WithFlag(2, out drpu_rpuratd1_ratdfrcrxraw_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdfrcrxraw_ValueProvider(_);
                        return drpu_rpuratd1_ratdfrcrxraw_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdfrcrxraw_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdfrcrxraw_Read(_, __),
                    name: "Ratdfrcrxraw")
            .WithFlag(3, out drpu_rpuratd1_ratdiadc0scantrigger_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdiadc0scantrigger_ValueProvider(_);
                        return drpu_rpuratd1_ratdiadc0scantrigger_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdiadc0scantrigger_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdiadc0scantrigger_Read(_, __),
                    name: "Ratdiadc0scantrigger")
            .WithFlag(4, out drpu_rpuratd1_ratdiadc0singletrigger_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdiadc0singletrigger_ValueProvider(_);
                        return drpu_rpuratd1_ratdiadc0singletrigger_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdiadc0singletrigger_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdiadc0singletrigger_Read(_, __),
                    name: "Ratdiadc0singletrigger")
            .WithFlag(5, out drpu_rpuratd1_ratdldmaxbardmareq0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdldmaxbardmareq0_ValueProvider(_);
                        return drpu_rpuratd1_ratdldmaxbardmareq0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdldmaxbardmareq0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdldmaxbardmareq0_Read(_, __),
                    name: "Ratdldmaxbardmareq0")
            .WithFlag(6, out drpu_rpuratd1_ratdldmaxbardmareq1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdldmaxbardmareq1_ValueProvider(_);
                        return drpu_rpuratd1_ratdldmaxbardmareq1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdldmaxbardmareq1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdldmaxbardmareq1_Read(_, __),
                    name: "Ratdldmaxbardmareq1")
            .WithFlag(7, out drpu_rpuratd1_ratdletimerclear_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdletimerclear_ValueProvider(_);
                        return drpu_rpuratd1_ratdletimerclear_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdletimerclear_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdletimerclear_Read(_, __),
                    name: "Ratdletimerclear")
            .WithFlag(8, out drpu_rpuratd1_ratdletimerstart_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdletimerstart_ValueProvider(_);
                        return drpu_rpuratd1_ratdletimerstart_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdletimerstart_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdletimerstart_Read(_, __),
                    name: "Ratdletimerstart")
            .WithFlag(9, out drpu_rpuratd1_ratdletimerstop_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdletimerstop_ValueProvider(_);
                        return drpu_rpuratd1_ratdletimerstop_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdletimerstop_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdletimerstop_Read(_, __),
                    name: "Ratdletimerstop")
            .WithFlag(10, out drpu_rpuratd1_ratdmodemdin_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdmodemdin_ValueProvider(_);
                        return drpu_rpuratd1_ratdmodemdin_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdmodemdin_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdmodemdin_Read(_, __),
                    name: "Ratdmodemdin")
            .WithFlag(11, out drpu_rpuratd1_ratdmodempaen_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdmodempaen_ValueProvider(_);
                        return drpu_rpuratd1_ratdmodempaen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdmodempaen_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdmodempaen_Read(_, __),
                    name: "Ratdmodempaen")
            .WithFlag(12, out drpu_rpuratd1_ratdpcnt0s0in_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdpcnt0s0in_ValueProvider(_);
                        return drpu_rpuratd1_ratdpcnt0s0in_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdpcnt0s0in_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdpcnt0s0in_Read(_, __),
                    name: "Ratdpcnt0s0in")
            .WithFlag(13, out drpu_rpuratd1_ratdpcnt0s1in_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdpcnt0s1in_ValueProvider(_);
                        return drpu_rpuratd1_ratdpcnt0s1in_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdpcnt0s1in_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdpcnt0s1in_Read(_, __),
                    name: "Ratdpcnt0s1in")
            .WithFlag(14, out drpu_rpuratd1_ratdprotimercc0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimercc0_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimercc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc0_Read(_, __),
                    name: "Ratdprotimercc0")
            .WithFlag(15, out drpu_rpuratd1_ratdprotimercc1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimercc1_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimercc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc1_Read(_, __),
                    name: "Ratdprotimercc1")
            .WithFlag(16, out drpu_rpuratd1_ratdprotimercc2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimercc2_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimercc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc2_Read(_, __),
                    name: "Ratdprotimercc2")
            .WithFlag(17, out drpu_rpuratd1_ratdprotimercc3_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimercc3_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimercc3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc3_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc3_Read(_, __),
                    name: "Ratdprotimercc3")
            .WithFlag(18, out drpu_rpuratd1_ratdprotimercc4_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimercc4_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimercc4_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc4_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimercc4_Read(_, __),
                    name: "Ratdprotimercc4")
            .WithFlag(19, out drpu_rpuratd1_ratdprotimerlbtpause_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimerlbtpause_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimerlbtpause_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerlbtpause_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerlbtpause_Read(_, __),
                    name: "Ratdprotimerlbtpause")
            .WithFlag(20, out drpu_rpuratd1_ratdprotimerlbtstart_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimerlbtstart_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimerlbtstart_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerlbtstart_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerlbtstart_Read(_, __),
                    name: "Ratdprotimerlbtstart")
            .WithFlag(21, out drpu_rpuratd1_ratdprotimerlbtstop_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimerlbtstop_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimerlbtstop_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerlbtstop_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerlbtstop_Read(_, __),
                    name: "Ratdprotimerlbtstop")
            .WithFlag(22, out drpu_rpuratd1_ratdprotimerrtcctrigger_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimerrtcctrigger_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimerrtcctrigger_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerrtcctrigger_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerrtcctrigger_Read(_, __),
                    name: "Ratdprotimerrtcctrigger")
            .WithFlag(23, out drpu_rpuratd1_ratdprotimerstart_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimerstart_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimerstart_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerstart_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerstart_Read(_, __),
                    name: "Ratdprotimerstart")
            .WithFlag(24, out drpu_rpuratd1_ratdprotimerstop_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdprotimerstop_ValueProvider(_);
                        return drpu_rpuratd1_ratdprotimerstop_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerstop_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdprotimerstop_Read(_, __),
                    name: "Ratdprotimerstop")
            .WithFlag(25, out drpu_rpuratd1_ratdracclr_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdracclr_ValueProvider(_);
                        return drpu_rpuratd1_ratdracclr_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdracclr_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdracclr_Read(_, __),
                    name: "Ratdracclr")
            .WithFlag(26, out drpu_rpuratd1_ratdracctiin0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdracctiin0_ValueProvider(_);
                        return drpu_rpuratd1_ratdracctiin0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin0_Read(_, __),
                    name: "Ratdracctiin0")
            .WithFlag(27, out drpu_rpuratd1_ratdracctiin1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdracctiin1_ValueProvider(_);
                        return drpu_rpuratd1_ratdracctiin1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin1_Read(_, __),
                    name: "Ratdracctiin1")
            .WithFlag(28, out drpu_rpuratd1_ratdracctiin2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdracctiin2_ValueProvider(_);
                        return drpu_rpuratd1_ratdracctiin2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin2_Read(_, __),
                    name: "Ratdracctiin2")
            .WithFlag(29, out drpu_rpuratd1_ratdracctiin3_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdracctiin3_ValueProvider(_);
                        return drpu_rpuratd1_ratdracctiin3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin3_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdracctiin3_Read(_, __),
                    name: "Ratdracctiin3")
            .WithFlag(30, out drpu_rpuratd1_ratdracforcetx_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdracforcetx_ValueProvider(_);
                        return drpu_rpuratd1_ratdracforcetx_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdracforcetx_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdracforcetx_Read(_, __),
                    name: "Ratdracforcetx")
            .WithFlag(31, out drpu_rpuratd1_ratdracrxdis_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd1_Ratdracrxdis_ValueProvider(_);
                        return drpu_rpuratd1_ratdracrxdis_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd1_Ratdracrxdis_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd1_Ratdracrxdis_Read(_, __),
                    name: "Ratdracrxdis")
            .WithReadCallback((_, __) => Drpu_Rpuratd1_Read(_, __))
            .WithWriteCallback((_, __) => Drpu_Rpuratd1_Write(_, __));
        
        // Drpu_Rpuratd2 - Offset : 0x214
        protected DoubleWordRegister  GenerateDrpu_rpuratd2Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out drpu_rpuratd2_ratdracrxen_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdracrxen_ValueProvider(_);
                        return drpu_rpuratd2_ratdracrxen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdracrxen_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdracrxen_Read(_, __),
                    name: "Ratdracrxen")
            .WithFlag(1, out drpu_rpuratd2_ratdractxen_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdractxen_ValueProvider(_);
                        return drpu_rpuratd2_ratdractxen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdractxen_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdractxen_Read(_, __),
                    name: "Ratdractxen")
            .WithFlag(2, out drpu_rpuratd2_ratdsetampertampersrc25_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsetampertampersrc25_ValueProvider(_);
                        return drpu_rpuratd2_ratdsetampertampersrc25_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc25_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc25_Read(_, __),
                    name: "Ratdsetampertampersrc25")
            .WithFlag(3, out drpu_rpuratd2_ratdsetampertampersrc26_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsetampertampersrc26_ValueProvider(_);
                        return drpu_rpuratd2_ratdsetampertampersrc26_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc26_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc26_Read(_, __),
                    name: "Ratdsetampertampersrc26")
            .WithFlag(4, out drpu_rpuratd2_ratdsetampertampersrc27_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsetampertampersrc27_ValueProvider(_);
                        return drpu_rpuratd2_ratdsetampertampersrc27_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc27_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc27_Read(_, __),
                    name: "Ratdsetampertampersrc27")
            .WithFlag(5, out drpu_rpuratd2_ratdsetampertampersrc28_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsetampertampersrc28_ValueProvider(_);
                        return drpu_rpuratd2_ratdsetampertampersrc28_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc28_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc28_Read(_, __),
                    name: "Ratdsetampertampersrc28")
            .WithFlag(6, out drpu_rpuratd2_ratdsetampertampersrc29_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsetampertampersrc29_ValueProvider(_);
                        return drpu_rpuratd2_ratdsetampertampersrc29_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc29_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc29_Read(_, __),
                    name: "Ratdsetampertampersrc29")
            .WithFlag(7, out drpu_rpuratd2_ratdsetampertampersrc30_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsetampertampersrc30_ValueProvider(_);
                        return drpu_rpuratd2_ratdsetampertampersrc30_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc30_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc30_Read(_, __),
                    name: "Ratdsetampertampersrc30")
            .WithFlag(8, out drpu_rpuratd2_ratdsetampertampersrc31_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsetampertampersrc31_ValueProvider(_);
                        return drpu_rpuratd2_ratdsetampertampersrc31_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc31_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsetampertampersrc31_Read(_, __),
                    name: "Ratdsetampertampersrc31")
            .WithFlag(9, out drpu_rpuratd2_ratdsysrtc0in0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsysrtc0in0_ValueProvider(_);
                        return drpu_rpuratd2_ratdsysrtc0in0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsysrtc0in0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsysrtc0in0_Read(_, __),
                    name: "Ratdsysrtc0in0")
            .WithFlag(10, out drpu_rpuratd2_ratdsysrtc0in1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsysrtc0in1_ValueProvider(_);
                        return drpu_rpuratd2_ratdsysrtc0in1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsysrtc0in1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsysrtc0in1_Read(_, __),
                    name: "Ratdsysrtc0in1")
            .WithFlag(11, out drpu_rpuratd2_ratdsyxo0oscreq_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsyxo0oscreq_ValueProvider(_);
                        return drpu_rpuratd2_ratdsyxo0oscreq_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsyxo0oscreq_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsyxo0oscreq_Read(_, __),
                    name: "Ratdsyxo0oscreq")
            .WithFlag(12, out drpu_rpuratd2_ratdsyxo0timeout_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdsyxo0timeout_ValueProvider(_);
                        return drpu_rpuratd2_ratdsyxo0timeout_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdsyxo0timeout_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdsyxo0timeout_Read(_, __),
                    name: "Ratdsyxo0timeout")
            .WithFlag(13, out drpu_rpuratd2_ratdcorectiin0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdcorectiin0_ValueProvider(_);
                        return drpu_rpuratd2_ratdcorectiin0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin0_Read(_, __),
                    name: "Ratdcorectiin0")
            .WithFlag(14, out drpu_rpuratd2_ratdcorectiin1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdcorectiin1_ValueProvider(_);
                        return drpu_rpuratd2_ratdcorectiin1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin1_Read(_, __),
                    name: "Ratdcorectiin1")
            .WithFlag(15, out drpu_rpuratd2_ratdcorectiin2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdcorectiin2_ValueProvider(_);
                        return drpu_rpuratd2_ratdcorectiin2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin2_Read(_, __),
                    name: "Ratdcorectiin2")
            .WithFlag(16, out drpu_rpuratd2_ratdcorectiin3_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdcorectiin3_ValueProvider(_);
                        return drpu_rpuratd2_ratdcorectiin3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin3_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdcorectiin3_Read(_, __),
                    name: "Ratdcorectiin3")
            .WithFlag(17, out drpu_rpuratd2_ratdcorem33rxev_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdcorem33rxev_ValueProvider(_);
                        return drpu_rpuratd2_ratdcorem33rxev_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdcorem33rxev_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdcorem33rxev_Read(_, __),
                    name: "Ratdcorem33rxev")
            .WithFlag(18, out drpu_rpuratd2_ratdtimer0cc0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer0cc0_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer0cc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0cc0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0cc0_Read(_, __),
                    name: "Ratdtimer0cc0")
            .WithFlag(19, out drpu_rpuratd2_ratdtimer0cc1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer0cc1_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer0cc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0cc1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0cc1_Read(_, __),
                    name: "Ratdtimer0cc1")
            .WithFlag(20, out drpu_rpuratd2_ratdtimer0cc2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer0cc2_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer0cc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0cc2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0cc2_Read(_, __),
                    name: "Ratdtimer0cc2")
            .WithFlag(21, out drpu_rpuratd2_ratdtimer0dti_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer0dti_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer0dti_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0dti_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0dti_Read(_, __),
                    name: "Ratdtimer0dti")
            .WithFlag(22, out drpu_rpuratd2_ratdtimer0dtifs1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer0dtifs1_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer0dtifs1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0dtifs1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0dtifs1_Read(_, __),
                    name: "Ratdtimer0dtifs1")
            .WithFlag(23, out drpu_rpuratd2_ratdtimer0dtifs2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer0dtifs2_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer0dtifs2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0dtifs2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer0dtifs2_Read(_, __),
                    name: "Ratdtimer0dtifs2")
            .WithFlag(24, out drpu_rpuratd2_ratdtimer1cc0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer1cc0_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer1cc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1cc0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1cc0_Read(_, __),
                    name: "Ratdtimer1cc0")
            .WithFlag(25, out drpu_rpuratd2_ratdtimer1cc1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer1cc1_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer1cc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1cc1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1cc1_Read(_, __),
                    name: "Ratdtimer1cc1")
            .WithFlag(26, out drpu_rpuratd2_ratdtimer1cc2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer1cc2_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer1cc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1cc2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1cc2_Read(_, __),
                    name: "Ratdtimer1cc2")
            .WithFlag(27, out drpu_rpuratd2_ratdtimer1dti_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer1dti_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer1dti_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1dti_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1dti_Read(_, __),
                    name: "Ratdtimer1dti")
            .WithFlag(28, out drpu_rpuratd2_ratdtimer1dtifs1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer1dtifs1_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer1dtifs1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1dtifs1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1dtifs1_Read(_, __),
                    name: "Ratdtimer1dtifs1")
            .WithFlag(29, out drpu_rpuratd2_ratdtimer1dtifs2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer1dtifs2_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer1dtifs2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1dtifs2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer1dtifs2_Read(_, __),
                    name: "Ratdtimer1dtifs2")
            .WithFlag(30, out drpu_rpuratd2_ratdtimer2cc0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer2cc0_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer2cc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer2cc0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer2cc0_Read(_, __),
                    name: "Ratdtimer2cc0")
            .WithFlag(31, out drpu_rpuratd2_ratdtimer2cc1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd2_Ratdtimer2cc1_ValueProvider(_);
                        return drpu_rpuratd2_ratdtimer2cc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer2cc1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd2_Ratdtimer2cc1_Read(_, __),
                    name: "Ratdtimer2cc1")
            .WithReadCallback((_, __) => Drpu_Rpuratd2_Read(_, __))
            .WithWriteCallback((_, __) => Drpu_Rpuratd2_Write(_, __));
        
        // Drpu_Rpuratd3 - Offset : 0x218
        protected DoubleWordRegister  GenerateDrpu_rpuratd3Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out drpu_rpuratd3_ratdtimer2cc2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer2cc2_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer2cc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2cc2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2cc2_Read(_, __),
                    name: "Ratdtimer2cc2")
            .WithFlag(1, out drpu_rpuratd3_ratdtimer2dti_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer2dti_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer2dti_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2dti_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2dti_Read(_, __),
                    name: "Ratdtimer2dti")
            .WithFlag(2, out drpu_rpuratd3_ratdtimer2dtifs1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer2dtifs1_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer2dtifs1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2dtifs1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2dtifs1_Read(_, __),
                    name: "Ratdtimer2dtifs1")
            .WithFlag(3, out drpu_rpuratd3_ratdtimer2dtifs2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer2dtifs2_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer2dtifs2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2dtifs2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer2dtifs2_Read(_, __),
                    name: "Ratdtimer2dtifs2")
            .WithFlag(4, out drpu_rpuratd3_ratdtimer3cc0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer3cc0_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer3cc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3cc0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3cc0_Read(_, __),
                    name: "Ratdtimer3cc0")
            .WithFlag(5, out drpu_rpuratd3_ratdtimer3cc1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer3cc1_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer3cc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3cc1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3cc1_Read(_, __),
                    name: "Ratdtimer3cc1")
            .WithFlag(6, out drpu_rpuratd3_ratdtimer3cc2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer3cc2_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer3cc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3cc2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3cc2_Read(_, __),
                    name: "Ratdtimer3cc2")
            .WithFlag(7, out drpu_rpuratd3_ratdtimer3dti_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer3dti_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer3dti_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3dti_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3dti_Read(_, __),
                    name: "Ratdtimer3dti")
            .WithFlag(8, out drpu_rpuratd3_ratdtimer3dtifs1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer3dtifs1_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer3dtifs1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3dtifs1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3dtifs1_Read(_, __),
                    name: "Ratdtimer3dtifs1")
            .WithFlag(9, out drpu_rpuratd3_ratdtimer3dtifs2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer3dtifs2_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer3dtifs2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3dtifs2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer3dtifs2_Read(_, __),
                    name: "Ratdtimer3dtifs2")
            .WithFlag(10, out drpu_rpuratd3_ratdtimer4cc0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer4cc0_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer4cc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4cc0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4cc0_Read(_, __),
                    name: "Ratdtimer4cc0")
            .WithFlag(11, out drpu_rpuratd3_ratdtimer4cc1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer4cc1_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer4cc1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4cc1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4cc1_Read(_, __),
                    name: "Ratdtimer4cc1")
            .WithFlag(12, out drpu_rpuratd3_ratdtimer4cc2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer4cc2_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer4cc2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4cc2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4cc2_Read(_, __),
                    name: "Ratdtimer4cc2")
            .WithFlag(13, out drpu_rpuratd3_ratdtimer4dti_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer4dti_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer4dti_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4dti_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4dti_Read(_, __),
                    name: "Ratdtimer4dti")
            .WithFlag(14, out drpu_rpuratd3_ratdtimer4dtifs1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer4dtifs1_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer4dtifs1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4dtifs1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4dtifs1_Read(_, __),
                    name: "Ratdtimer4dtifs1")
            .WithFlag(15, out drpu_rpuratd3_ratdtimer4dtifs2_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdtimer4dtifs2_ValueProvider(_);
                        return drpu_rpuratd3_ratdtimer4dtifs2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4dtifs2_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdtimer4dtifs2_Read(_, __),
                    name: "Ratdtimer4dtifs2")
            .WithFlag(16, out drpu_rpuratd3_ratdusart0clk_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdusart0clk_ValueProvider(_);
                        return drpu_rpuratd3_ratdusart0clk_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0clk_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0clk_Read(_, __),
                    name: "Ratdusart0clk")
            .WithFlag(17, out drpu_rpuratd3_ratdusart0ir_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdusart0ir_ValueProvider(_);
                        return drpu_rpuratd3_ratdusart0ir_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0ir_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0ir_Read(_, __),
                    name: "Ratdusart0ir")
            .WithFlag(18, out drpu_rpuratd3_ratdusart0rx_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdusart0rx_ValueProvider(_);
                        return drpu_rpuratd3_ratdusart0rx_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0rx_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0rx_Read(_, __),
                    name: "Ratdusart0rx")
            .WithFlag(19, out drpu_rpuratd3_ratdusart0trigger_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdusart0trigger_ValueProvider(_);
                        return drpu_rpuratd3_ratdusart0trigger_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0trigger_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdusart0trigger_Read(_, __),
                    name: "Ratdusart0trigger")
            .WithReservedBits(20, 3)
            .WithFlag(23, out drpu_rpuratd3_ratdvdac0asynctrigch0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac0asynctrigch0_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac0asynctrigch0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0asynctrigch0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0asynctrigch0_Read(_, __),
                    name: "Ratdvdac0asynctrigch0")
            .WithFlag(24, out drpu_rpuratd3_ratdvdac0asynctrigch1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac0asynctrigch1_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac0asynctrigch1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0asynctrigch1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0asynctrigch1_Read(_, __),
                    name: "Ratdvdac0asynctrigch1")
            .WithFlag(25, out drpu_rpuratd3_ratdvdac0synctrigch0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac0synctrigch0_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac0synctrigch0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0synctrigch0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0synctrigch0_Read(_, __),
                    name: "Ratdvdac0synctrigch0")
            .WithFlag(26, out drpu_rpuratd3_ratdvdac0synctrigch1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac0synctrigch1_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac0synctrigch1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0synctrigch1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac0synctrigch1_Read(_, __),
                    name: "Ratdvdac0synctrigch1")
            .WithFlag(27, out drpu_rpuratd3_ratdvdac1asynctrigch0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac1asynctrigch0_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac1asynctrigch0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1asynctrigch0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1asynctrigch0_Read(_, __),
                    name: "Ratdvdac1asynctrigch0")
            .WithFlag(28, out drpu_rpuratd3_ratdvdac1asynctrigch1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac1asynctrigch1_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac1asynctrigch1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1asynctrigch1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1asynctrigch1_Read(_, __),
                    name: "Ratdvdac1asynctrigch1")
            .WithFlag(29, out drpu_rpuratd3_ratdvdac1synctrigch0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac1synctrigch0_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac1synctrigch0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1synctrigch0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1synctrigch0_Read(_, __),
                    name: "Ratdvdac1synctrigch0")
            .WithFlag(30, out drpu_rpuratd3_ratdvdac1synctrigch1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdvdac1synctrigch1_ValueProvider(_);
                        return drpu_rpuratd3_ratdvdac1synctrigch1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1synctrigch1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdvdac1synctrigch1_Read(_, __),
                    name: "Ratdvdac1synctrigch1")
            .WithFlag(31, out drpu_rpuratd3_ratdwdog0src0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd3_Ratdwdog0src0_ValueProvider(_);
                        return drpu_rpuratd3_ratdwdog0src0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd3_Ratdwdog0src0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd3_Ratdwdog0src0_Read(_, __),
                    name: "Ratdwdog0src0")
            .WithReadCallback((_, __) => Drpu_Rpuratd3_Read(_, __))
            .WithWriteCallback((_, __) => Drpu_Rpuratd3_Write(_, __));
        
        // Drpu_Rpuratd4 - Offset : 0x21C
        protected DoubleWordRegister  GenerateDrpu_rpuratd4Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out drpu_rpuratd4_ratdwdog0src1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd4_Ratdwdog0src1_ValueProvider(_);
                        return drpu_rpuratd4_ratdwdog0src1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd4_Ratdwdog0src1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd4_Ratdwdog0src1_Read(_, __),
                    name: "Ratdwdog0src1")
            .WithFlag(1, out drpu_rpuratd4_ratdwdog1src0_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd4_Ratdwdog1src0_ValueProvider(_);
                        return drpu_rpuratd4_ratdwdog1src0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd4_Ratdwdog1src0_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd4_Ratdwdog1src0_Read(_, __),
                    name: "Ratdwdog1src0")
            .WithFlag(2, out drpu_rpuratd4_ratdwdog1src1_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd4_Ratdwdog1src1_ValueProvider(_);
                        return drpu_rpuratd4_ratdwdog1src1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd4_Ratdwdog1src1_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd4_Ratdwdog1src1_Read(_, __),
                    name: "Ratdwdog1src1")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Drpu_Rpuratd4_Read(_, __))
            .WithWriteCallback((_, __) => Drpu_Rpuratd4_Write(_, __));
        

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
    
        protected IFlagRegisterField async_swpulse_ch12pulse_bit;
        partial void Async_swpulse_Ch12pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch12pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch13pulse_bit;
        partial void Async_swpulse_Ch13pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch13pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch14pulse_bit;
        partial void Async_swpulse_Ch14pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch14pulse_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swpulse_ch15pulse_bit;
        partial void Async_swpulse_Ch15pulse_Write(bool a, bool b);
        partial void Async_swpulse_Ch15pulse_ValueProvider(bool a);
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
    
        protected IFlagRegisterField async_swlevel_ch12level_bit;
        partial void Async_swlevel_Ch12level_Write(bool a, bool b);
        partial void Async_swlevel_Ch12level_Read(bool a, bool b);
        partial void Async_swlevel_Ch12level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch13level_bit;
        partial void Async_swlevel_Ch13level_Write(bool a, bool b);
        partial void Async_swlevel_Ch13level_Read(bool a, bool b);
        partial void Async_swlevel_Ch13level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch14level_bit;
        partial void Async_swlevel_Ch14level_Write(bool a, bool b);
        partial void Async_swlevel_Ch14level_Read(bool a, bool b);
        partial void Async_swlevel_Ch14level_ValueProvider(bool a);
    
        protected IFlagRegisterField async_swlevel_ch15level_bit;
        partial void Async_swlevel_Ch15level_Write(bool a, bool b);
        partial void Async_swlevel_Ch15level_Read(bool a, bool b);
        partial void Async_swlevel_Ch15level_ValueProvider(bool a);
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
    
        protected IFlagRegisterField async_peek_ch12val_bit;
        partial void Async_peek_Ch12val_Read(bool a, bool b);
        partial void Async_peek_Ch12val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch13val_bit;
        partial void Async_peek_Ch13val_Read(bool a, bool b);
        partial void Async_peek_Ch13val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch14val_bit;
        partial void Async_peek_Ch14val_Read(bool a, bool b);
        partial void Async_peek_Ch14val_ValueProvider(bool a);
    
        protected IFlagRegisterField async_peek_ch15val_bit;
        partial void Async_peek_Ch15val_Read(bool a, bool b);
        partial void Async_peek_Ch15val_ValueProvider(bool a);
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
    
    protected IEnumRegisterField<ASYNC_CH_CTRL_SIGSEL>[] async_ch_ctrl_sigsel_field = new IEnumRegisterField<ASYNC_CH_CTRL_SIGSEL>[16];
        partial void Async_Ch_Ctrl_Sigsel_Write(ulong index, ASYNC_CH_CTRL_SIGSEL a, ASYNC_CH_CTRL_SIGSEL b);
        partial void Async_Ch_Ctrl_Sigsel_Read(ulong index, ASYNC_CH_CTRL_SIGSEL a, ASYNC_CH_CTRL_SIGSEL b);
        partial void Async_Ch_Ctrl_Sigsel_ValueProvider(ulong index, ASYNC_CH_CTRL_SIGSEL a);
    
    
        protected IValueRegisterField[] async_ch_ctrl_sourcesel_field = new IValueRegisterField[16];
        partial void Async_Ch_Ctrl_Sourcesel_Write(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Sourcesel_Read(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Sourcesel_ValueProvider(ulong index, ulong a);
    
    protected IEnumRegisterField<ASYNC_CH_CTRL_FNSEL>[] async_ch_ctrl_fnsel_field = new IEnumRegisterField<ASYNC_CH_CTRL_FNSEL>[16];
        partial void Async_Ch_Ctrl_Fnsel_Write(ulong index, ASYNC_CH_CTRL_FNSEL a, ASYNC_CH_CTRL_FNSEL b);
        partial void Async_Ch_Ctrl_Fnsel_Read(ulong index, ASYNC_CH_CTRL_FNSEL a, ASYNC_CH_CTRL_FNSEL b);
        partial void Async_Ch_Ctrl_Fnsel_ValueProvider(ulong index, ASYNC_CH_CTRL_FNSEL a);
    
    
        protected IValueRegisterField[] async_ch_ctrl_auxsel_field = new IValueRegisterField[16];
        partial void Async_Ch_Ctrl_Auxsel_Write(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Auxsel_Read(ulong index, ulong a, ulong b);
        partial void Async_Ch_Ctrl_Auxsel_ValueProvider(ulong index, ulong a);
        partial void Async_Ch_Ctrl_Write(ulong index, uint a, uint b);
        partial void Async_Ch_Ctrl_Read(ulong index, uint a, uint b);
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
    
    
    
    
    
    
    
    
        
        // Sync_Ch0_Ctrl - Offset : 0x58
    
    protected IEnumRegisterField<SYNC_CH_CTRL_SIGSEL>[] sync_ch_ctrl_sigsel_field = new IEnumRegisterField<SYNC_CH_CTRL_SIGSEL>[4];
        partial void Sync_Ch_Ctrl_Sigsel_Write(ulong index, SYNC_CH_CTRL_SIGSEL a, SYNC_CH_CTRL_SIGSEL b);
        partial void Sync_Ch_Ctrl_Sigsel_Read(ulong index, SYNC_CH_CTRL_SIGSEL a, SYNC_CH_CTRL_SIGSEL b);
        partial void Sync_Ch_Ctrl_Sigsel_ValueProvider(ulong index, SYNC_CH_CTRL_SIGSEL a);
    
    
        protected IValueRegisterField[] sync_ch_ctrl_sourcesel_field = new IValueRegisterField[4];
        partial void Sync_Ch_Ctrl_Sourcesel_Write(ulong index, ulong a, ulong b);
        partial void Sync_Ch_Ctrl_Sourcesel_Read(ulong index, ulong a, ulong b);
        partial void Sync_Ch_Ctrl_Sourcesel_ValueProvider(ulong index, ulong a);
        partial void Sync_Ch_Ctrl_Write(ulong index, uint a, uint b);
        partial void Sync_Ch_Ctrl_Read(ulong index, uint a, uint b);
        
    
    
    
    
        
    
    
    
    
        
    
    
    
    
        
        // Consumer_Cmu_caldn - Offset : 0x68
    
        protected IValueRegisterField consumer_cmu_caldn_prssel_field;
        partial void Consumer_Cmu_caldn_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Cmu_caldn_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Cmu_caldn_Prssel_ValueProvider(ulong a);
        partial void Consumer_Cmu_caldn_Write(uint a, uint b);
        partial void Consumer_Cmu_caldn_Read(uint a, uint b);
        
        // Consumer_Cmu_calup - Offset : 0x6C
    
        protected IValueRegisterField consumer_cmu_calup_prssel_field;
        partial void Consumer_Cmu_calup_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Cmu_calup_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Cmu_calup_Prssel_ValueProvider(ulong a);
        partial void Consumer_Cmu_calup_Write(uint a, uint b);
        partial void Consumer_Cmu_calup_Read(uint a, uint b);
        
        // Consumer_Eusart0_clk - Offset : 0x70
    
        protected IValueRegisterField consumer_eusart0_clk_prssel_field;
        partial void Consumer_Eusart0_clk_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Eusart0_clk_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Eusart0_clk_Prssel_ValueProvider(ulong a);
        partial void Consumer_Eusart0_clk_Write(uint a, uint b);
        partial void Consumer_Eusart0_clk_Read(uint a, uint b);
        
        // Consumer_Eusart0_rx - Offset : 0x74
    
        protected IValueRegisterField consumer_eusart0_rx_prssel_field;
        partial void Consumer_Eusart0_rx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Eusart0_rx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Eusart0_rx_Prssel_ValueProvider(ulong a);
        partial void Consumer_Eusart0_rx_Write(uint a, uint b);
        partial void Consumer_Eusart0_rx_Read(uint a, uint b);
        
        // Consumer_Eusart0_trigger - Offset : 0x78
    
        protected IValueRegisterField consumer_eusart0_trigger_prssel_field;
        partial void Consumer_Eusart0_trigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Eusart0_trigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Eusart0_trigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_Eusart0_trigger_Write(uint a, uint b);
        partial void Consumer_Eusart0_trigger_Read(uint a, uint b);
        
        // Consumer_Eusart1_clk - Offset : 0x7C
    
        protected IValueRegisterField consumer_eusart1_clk_prssel_field;
        partial void Consumer_Eusart1_clk_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Eusart1_clk_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Eusart1_clk_Prssel_ValueProvider(ulong a);
        partial void Consumer_Eusart1_clk_Write(uint a, uint b);
        partial void Consumer_Eusart1_clk_Read(uint a, uint b);
        
        // Consumer_Eusart1_rx - Offset : 0x80
    
        protected IValueRegisterField consumer_eusart1_rx_prssel_field;
        partial void Consumer_Eusart1_rx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Eusart1_rx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Eusart1_rx_Prssel_ValueProvider(ulong a);
        partial void Consumer_Eusart1_rx_Write(uint a, uint b);
        partial void Consumer_Eusart1_rx_Read(uint a, uint b);
        
        // Consumer_Eusart1_trigger - Offset : 0x84
    
        protected IValueRegisterField consumer_eusart1_trigger_prssel_field;
        partial void Consumer_Eusart1_trigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Eusart1_trigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Eusart1_trigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_Eusart1_trigger_Write(uint a, uint b);
        partial void Consumer_Eusart1_trigger_Read(uint a, uint b);
        
        // Consumer_Frc_rxraw - Offset : 0x88
    
        protected IValueRegisterField consumer_frc_rxraw_prssel_field;
        partial void Consumer_Frc_rxraw_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Frc_rxraw_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Frc_rxraw_Prssel_ValueProvider(ulong a);
        partial void Consumer_Frc_rxraw_Write(uint a, uint b);
        partial void Consumer_Frc_rxraw_Read(uint a, uint b);
        
        // Consumer_Iadc0_scantrigger - Offset : 0x8C
    
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
        
        // Consumer_Iadc0_singletrigger - Offset : 0x90
    
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
        
        // Consumer_Ldmaxbar_dmareq0 - Offset : 0x94
    
        protected IValueRegisterField consumer_ldmaxbar_dmareq0_prssel_field;
        partial void Consumer_Ldmaxbar_dmareq0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Ldmaxbar_dmareq0_Write(uint a, uint b);
        partial void Consumer_Ldmaxbar_dmareq0_Read(uint a, uint b);
        
        // Consumer_Ldmaxbar_dmareq1 - Offset : 0x98
    
        protected IValueRegisterField consumer_ldmaxbar_dmareq1_prssel_field;
        partial void Consumer_Ldmaxbar_dmareq1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Ldmaxbar_dmareq1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Ldmaxbar_dmareq1_Write(uint a, uint b);
        partial void Consumer_Ldmaxbar_dmareq1_Read(uint a, uint b);
        
        // Consumer_Letimer0_clear - Offset : 0x9C
    
        protected IValueRegisterField consumer_letimer0_clear_prssel_field;
        partial void Consumer_Letimer0_clear_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Letimer0_clear_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Letimer0_clear_Prssel_ValueProvider(ulong a);
        partial void Consumer_Letimer0_clear_Write(uint a, uint b);
        partial void Consumer_Letimer0_clear_Read(uint a, uint b);
        
        // Consumer_Letimer0_start - Offset : 0xA0
    
        protected IValueRegisterField consumer_letimer0_start_prssel_field;
        partial void Consumer_Letimer0_start_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Letimer0_start_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Letimer0_start_Prssel_ValueProvider(ulong a);
        partial void Consumer_Letimer0_start_Write(uint a, uint b);
        partial void Consumer_Letimer0_start_Read(uint a, uint b);
        
        // Consumer_Letimer0_stop - Offset : 0xA4
    
        protected IValueRegisterField consumer_letimer0_stop_prssel_field;
        partial void Consumer_Letimer0_stop_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Letimer0_stop_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Letimer0_stop_Prssel_ValueProvider(ulong a);
        partial void Consumer_Letimer0_stop_Write(uint a, uint b);
        partial void Consumer_Letimer0_stop_Read(uint a, uint b);
        
        // Consumer_Modem_din - Offset : 0xA8
    
        protected IValueRegisterField consumer_modem_din_prssel_field;
        partial void Consumer_Modem_din_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Modem_din_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Modem_din_Prssel_ValueProvider(ulong a);
        partial void Consumer_Modem_din_Write(uint a, uint b);
        partial void Consumer_Modem_din_Read(uint a, uint b);
        
        // Consumer_Modem_paen - Offset : 0xAC
    
        protected IValueRegisterField consumer_modem_paen_prssel_field;
        partial void Consumer_Modem_paen_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Modem_paen_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Modem_paen_Prssel_ValueProvider(ulong a);
        partial void Consumer_Modem_paen_Write(uint a, uint b);
        partial void Consumer_Modem_paen_Read(uint a, uint b);
        
        // Consumer_Pcnt0_s0in - Offset : 0xB0
    
        protected IValueRegisterField consumer_pcnt0_s0in_prssel_field;
        partial void Consumer_Pcnt0_s0in_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Pcnt0_s0in_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Pcnt0_s0in_Prssel_ValueProvider(ulong a);
        partial void Consumer_Pcnt0_s0in_Write(uint a, uint b);
        partial void Consumer_Pcnt0_s0in_Read(uint a, uint b);
        
        // Consumer_Pcnt0_s1in - Offset : 0xB4
    
        protected IValueRegisterField consumer_pcnt0_s1in_prssel_field;
        partial void Consumer_Pcnt0_s1in_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Pcnt0_s1in_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Pcnt0_s1in_Prssel_ValueProvider(ulong a);
        partial void Consumer_Pcnt0_s1in_Write(uint a, uint b);
        partial void Consumer_Pcnt0_s1in_Read(uint a, uint b);
        
        // Consumer_Protimer_cc0 - Offset : 0xB8
    
        protected IValueRegisterField consumer_protimer_cc0_prssel_field;
        partial void Consumer_Protimer_cc0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc0_Write(uint a, uint b);
        partial void Consumer_Protimer_cc0_Read(uint a, uint b);
        
        // Consumer_Protimer_cc1 - Offset : 0xBC
    
        protected IValueRegisterField consumer_protimer_cc1_prssel_field;
        partial void Consumer_Protimer_cc1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc1_Write(uint a, uint b);
        partial void Consumer_Protimer_cc1_Read(uint a, uint b);
        
        // Consumer_Protimer_cc2 - Offset : 0xC0
    
        protected IValueRegisterField consumer_protimer_cc2_prssel_field;
        partial void Consumer_Protimer_cc2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc2_Write(uint a, uint b);
        partial void Consumer_Protimer_cc2_Read(uint a, uint b);
        
        // Consumer_Protimer_cc3 - Offset : 0xC4
    
        protected IValueRegisterField consumer_protimer_cc3_prssel_field;
        partial void Consumer_Protimer_cc3_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc3_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc3_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc3_Write(uint a, uint b);
        partial void Consumer_Protimer_cc3_Read(uint a, uint b);
        
        // Consumer_Protimer_cc4 - Offset : 0xC8
    
        protected IValueRegisterField consumer_protimer_cc4_prssel_field;
        partial void Consumer_Protimer_cc4_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_cc4_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_cc4_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_cc4_Write(uint a, uint b);
        partial void Consumer_Protimer_cc4_Read(uint a, uint b);
        
        // Consumer_Protimer_lbtpause - Offset : 0xCC
    
        protected IValueRegisterField consumer_protimer_lbtpause_prssel_field;
        partial void Consumer_Protimer_lbtpause_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_lbtpause_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_lbtpause_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_lbtpause_Write(uint a, uint b);
        partial void Consumer_Protimer_lbtpause_Read(uint a, uint b);
        
        // Consumer_Protimer_lbtstart - Offset : 0xD0
    
        protected IValueRegisterField consumer_protimer_lbtstart_prssel_field;
        partial void Consumer_Protimer_lbtstart_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstart_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstart_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_lbtstart_Write(uint a, uint b);
        partial void Consumer_Protimer_lbtstart_Read(uint a, uint b);
        
        // Consumer_Protimer_lbtstop - Offset : 0xD4
    
        protected IValueRegisterField consumer_protimer_lbtstop_prssel_field;
        partial void Consumer_Protimer_lbtstop_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstop_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_lbtstop_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_lbtstop_Write(uint a, uint b);
        partial void Consumer_Protimer_lbtstop_Read(uint a, uint b);
        
        // Consumer_Protimer_rtcctrigger - Offset : 0xD8
    
        protected IValueRegisterField consumer_protimer_rtcctrigger_prssel_field;
        partial void Consumer_Protimer_rtcctrigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_rtcctrigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_rtcctrigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_rtcctrigger_Write(uint a, uint b);
        partial void Consumer_Protimer_rtcctrigger_Read(uint a, uint b);
        
        // Consumer_Protimer_start - Offset : 0xDC
    
        protected IValueRegisterField consumer_protimer_start_prssel_field;
        partial void Consumer_Protimer_start_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_start_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_start_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_start_Write(uint a, uint b);
        partial void Consumer_Protimer_start_Read(uint a, uint b);
        
        // Consumer_Protimer_stop - Offset : 0xE0
    
        protected IValueRegisterField consumer_protimer_stop_prssel_field;
        partial void Consumer_Protimer_stop_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Protimer_stop_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Protimer_stop_Prssel_ValueProvider(ulong a);
        partial void Consumer_Protimer_stop_Write(uint a, uint b);
        partial void Consumer_Protimer_stop_Read(uint a, uint b);
        
        // Consumer_Rac_clr - Offset : 0xE4
    
        protected IValueRegisterField consumer_rac_clr_prssel_field;
        partial void Consumer_Rac_clr_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_clr_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_clr_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_clr_Write(uint a, uint b);
        partial void Consumer_Rac_clr_Read(uint a, uint b);
        
        // Consumer_Rac_ctiin0 - Offset : 0xE8
    
        protected IValueRegisterField consumer_rac_ctiin0_prssel_field;
        partial void Consumer_Rac_ctiin0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin0_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin0_Read(uint a, uint b);
        
        // Consumer_Rac_ctiin1 - Offset : 0xEC
    
        protected IValueRegisterField consumer_rac_ctiin1_prssel_field;
        partial void Consumer_Rac_ctiin1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin1_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin1_Read(uint a, uint b);
        
        // Consumer_Rac_ctiin2 - Offset : 0xF0
    
        protected IValueRegisterField consumer_rac_ctiin2_prssel_field;
        partial void Consumer_Rac_ctiin2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin2_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin2_Read(uint a, uint b);
        
        // Consumer_Rac_ctiin3 - Offset : 0xF4
    
        protected IValueRegisterField consumer_rac_ctiin3_prssel_field;
        partial void Consumer_Rac_ctiin3_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_ctiin3_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_ctiin3_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_ctiin3_Write(uint a, uint b);
        partial void Consumer_Rac_ctiin3_Read(uint a, uint b);
        
        // Consumer_Rac_forcetx - Offset : 0xF8
    
        protected IValueRegisterField consumer_rac_forcetx_prssel_field;
        partial void Consumer_Rac_forcetx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_forcetx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_forcetx_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_forcetx_Write(uint a, uint b);
        partial void Consumer_Rac_forcetx_Read(uint a, uint b);
        
        // Consumer_Rac_rxdis - Offset : 0xFC
    
        protected IValueRegisterField consumer_rac_rxdis_prssel_field;
        partial void Consumer_Rac_rxdis_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_rxdis_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_rxdis_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_rxdis_Write(uint a, uint b);
        partial void Consumer_Rac_rxdis_Read(uint a, uint b);
        
        // Consumer_Rac_rxen - Offset : 0x100
    
        protected IValueRegisterField consumer_rac_rxen_prssel_field;
        partial void Consumer_Rac_rxen_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_rxen_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_rxen_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_rxen_Write(uint a, uint b);
        partial void Consumer_Rac_rxen_Read(uint a, uint b);
        
        // Consumer_Rac_txen - Offset : 0x104
    
        protected IValueRegisterField consumer_rac_txen_prssel_field;
        partial void Consumer_Rac_txen_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Rac_txen_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Rac_txen_Prssel_ValueProvider(ulong a);
        partial void Consumer_Rac_txen_Write(uint a, uint b);
        partial void Consumer_Rac_txen_Read(uint a, uint b);
        
        // Consumer_Setamper_tampersrc25 - Offset : 0x108
    
        protected IValueRegisterField consumer_setamper_tampersrc25_prssel_field;
        partial void Consumer_Setamper_tampersrc25_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc25_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc25_Prssel_ValueProvider(ulong a);
        partial void Consumer_Setamper_tampersrc25_Write(uint a, uint b);
        partial void Consumer_Setamper_tampersrc25_Read(uint a, uint b);
        
        // Consumer_Setamper_tampersrc26 - Offset : 0x10C
    
        protected IValueRegisterField consumer_setamper_tampersrc26_prssel_field;
        partial void Consumer_Setamper_tampersrc26_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc26_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc26_Prssel_ValueProvider(ulong a);
        partial void Consumer_Setamper_tampersrc26_Write(uint a, uint b);
        partial void Consumer_Setamper_tampersrc26_Read(uint a, uint b);
        
        // Consumer_Setamper_tampersrc27 - Offset : 0x110
    
        protected IValueRegisterField consumer_setamper_tampersrc27_prssel_field;
        partial void Consumer_Setamper_tampersrc27_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc27_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc27_Prssel_ValueProvider(ulong a);
        partial void Consumer_Setamper_tampersrc27_Write(uint a, uint b);
        partial void Consumer_Setamper_tampersrc27_Read(uint a, uint b);
        
        // Consumer_Setamper_tampersrc28 - Offset : 0x114
    
        protected IValueRegisterField consumer_setamper_tampersrc28_prssel_field;
        partial void Consumer_Setamper_tampersrc28_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc28_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc28_Prssel_ValueProvider(ulong a);
        partial void Consumer_Setamper_tampersrc28_Write(uint a, uint b);
        partial void Consumer_Setamper_tampersrc28_Read(uint a, uint b);
        
        // Consumer_Setamper_tampersrc29 - Offset : 0x118
    
        protected IValueRegisterField consumer_setamper_tampersrc29_prssel_field;
        partial void Consumer_Setamper_tampersrc29_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc29_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc29_Prssel_ValueProvider(ulong a);
        partial void Consumer_Setamper_tampersrc29_Write(uint a, uint b);
        partial void Consumer_Setamper_tampersrc29_Read(uint a, uint b);
        
        // Consumer_Setamper_tampersrc30 - Offset : 0x11C
    
        protected IValueRegisterField consumer_setamper_tampersrc30_prssel_field;
        partial void Consumer_Setamper_tampersrc30_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc30_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc30_Prssel_ValueProvider(ulong a);
        partial void Consumer_Setamper_tampersrc30_Write(uint a, uint b);
        partial void Consumer_Setamper_tampersrc30_Read(uint a, uint b);
        
        // Consumer_Setamper_tampersrc31 - Offset : 0x120
    
        protected IValueRegisterField consumer_setamper_tampersrc31_prssel_field;
        partial void Consumer_Setamper_tampersrc31_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc31_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Setamper_tampersrc31_Prssel_ValueProvider(ulong a);
        partial void Consumer_Setamper_tampersrc31_Write(uint a, uint b);
        partial void Consumer_Setamper_tampersrc31_Read(uint a, uint b);
        
        // Consumer_Sysrtc0_in0 - Offset : 0x124
    
        protected IValueRegisterField consumer_sysrtc0_in0_prssel_field;
        partial void Consumer_Sysrtc0_in0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Sysrtc0_in0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Sysrtc0_in0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Sysrtc0_in0_Write(uint a, uint b);
        partial void Consumer_Sysrtc0_in0_Read(uint a, uint b);
        
        // Consumer_Sysrtc0_in1 - Offset : 0x128
    
        protected IValueRegisterField consumer_sysrtc0_in1_prssel_field;
        partial void Consumer_Sysrtc0_in1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Sysrtc0_in1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Sysrtc0_in1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Sysrtc0_in1_Write(uint a, uint b);
        partial void Consumer_Sysrtc0_in1_Read(uint a, uint b);
        
        // Consumer_Hfxo0_oscreq - Offset : 0x12C
    
        protected IValueRegisterField consumer_hfxo0_oscreq_prssel_field;
        partial void Consumer_Hfxo0_oscreq_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Hfxo0_oscreq_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Hfxo0_oscreq_Prssel_ValueProvider(ulong a);
        partial void Consumer_Hfxo0_oscreq_Write(uint a, uint b);
        partial void Consumer_Hfxo0_oscreq_Read(uint a, uint b);
        
        // Consumer_Hfxo0_timeout - Offset : 0x130
    
        protected IValueRegisterField consumer_hfxo0_timeout_prssel_field;
        partial void Consumer_Hfxo0_timeout_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Hfxo0_timeout_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Hfxo0_timeout_Prssel_ValueProvider(ulong a);
        partial void Consumer_Hfxo0_timeout_Write(uint a, uint b);
        partial void Consumer_Hfxo0_timeout_Read(uint a, uint b);
        
        // Consumer_Core_ctiin0 - Offset : 0x134
    
        protected IValueRegisterField consumer_core_ctiin0_prssel_field;
        partial void Consumer_Core_ctiin0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin0_Write(uint a, uint b);
        partial void Consumer_Core_ctiin0_Read(uint a, uint b);
        
        // Consumer_Core_ctiin1 - Offset : 0x138
    
        protected IValueRegisterField consumer_core_ctiin1_prssel_field;
        partial void Consumer_Core_ctiin1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin1_Write(uint a, uint b);
        partial void Consumer_Core_ctiin1_Read(uint a, uint b);
        
        // Consumer_Core_ctiin2 - Offset : 0x13C
    
        protected IValueRegisterField consumer_core_ctiin2_prssel_field;
        partial void Consumer_Core_ctiin2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin2_Write(uint a, uint b);
        partial void Consumer_Core_ctiin2_Read(uint a, uint b);
        
        // Consumer_Core_ctiin3 - Offset : 0x140
    
        protected IValueRegisterField consumer_core_ctiin3_prssel_field;
        partial void Consumer_Core_ctiin3_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_ctiin3_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_ctiin3_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_ctiin3_Write(uint a, uint b);
        partial void Consumer_Core_ctiin3_Read(uint a, uint b);
        
        // Consumer_Core_m33rxev - Offset : 0x144
    
        protected IValueRegisterField consumer_core_m33rxev_prssel_field;
        partial void Consumer_Core_m33rxev_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Core_m33rxev_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Core_m33rxev_Prssel_ValueProvider(ulong a);
        partial void Consumer_Core_m33rxev_Write(uint a, uint b);
        partial void Consumer_Core_m33rxev_Read(uint a, uint b);
        
        // Consumer_Timer0_cc0 - Offset : 0x148
    
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
        
        // Consumer_Timer0_cc1 - Offset : 0x14C
    
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
        
        // Consumer_Timer0_cc2 - Offset : 0x150
    
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
        
        // Consumer_Timer0_dti - Offset : 0x154
    
        protected IValueRegisterField consumer_timer0_dti_prssel_field;
        partial void Consumer_Timer0_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_dti_Write(uint a, uint b);
        partial void Consumer_Timer0_dti_Read(uint a, uint b);
        
        // Consumer_Timer0_dtifs1 - Offset : 0x158
    
        protected IValueRegisterField consumer_timer0_dtifs1_prssel_field;
        partial void Consumer_Timer0_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer0_dtifs1_Read(uint a, uint b);
        
        // Consumer_Timer0_dtifs2 - Offset : 0x15C
    
        protected IValueRegisterField consumer_timer0_dtifs2_prssel_field;
        partial void Consumer_Timer0_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer0_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer0_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer0_dtifs2_Read(uint a, uint b);
        
        // Consumer_Timer1_cc0 - Offset : 0x160
    
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
        
        // Consumer_Timer1_cc1 - Offset : 0x164
    
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
        
        // Consumer_Timer1_cc2 - Offset : 0x168
    
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
        
        // Consumer_Timer1_dti - Offset : 0x16C
    
        protected IValueRegisterField consumer_timer1_dti_prssel_field;
        partial void Consumer_Timer1_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_dti_Write(uint a, uint b);
        partial void Consumer_Timer1_dti_Read(uint a, uint b);
        
        // Consumer_Timer1_dtifs1 - Offset : 0x170
    
        protected IValueRegisterField consumer_timer1_dtifs1_prssel_field;
        partial void Consumer_Timer1_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer1_dtifs1_Read(uint a, uint b);
        
        // Consumer_Timer1_dtifs2 - Offset : 0x174
    
        protected IValueRegisterField consumer_timer1_dtifs2_prssel_field;
        partial void Consumer_Timer1_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer1_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer1_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer1_dtifs2_Read(uint a, uint b);
        
        // Consumer_Timer2_cc0 - Offset : 0x178
    
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
        
        // Consumer_Timer2_cc1 - Offset : 0x17C
    
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
        
        // Consumer_Timer2_cc2 - Offset : 0x180
    
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
        
        // Consumer_Timer2_dti - Offset : 0x184
    
        protected IValueRegisterField consumer_timer2_dti_prssel_field;
        partial void Consumer_Timer2_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_dti_Write(uint a, uint b);
        partial void Consumer_Timer2_dti_Read(uint a, uint b);
        
        // Consumer_Timer2_dtifs1 - Offset : 0x188
    
        protected IValueRegisterField consumer_timer2_dtifs1_prssel_field;
        partial void Consumer_Timer2_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer2_dtifs1_Read(uint a, uint b);
        
        // Consumer_Timer2_dtifs2 - Offset : 0x18C
    
        protected IValueRegisterField consumer_timer2_dtifs2_prssel_field;
        partial void Consumer_Timer2_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer2_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer2_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer2_dtifs2_Read(uint a, uint b);
        
        // Consumer_Timer3_cc0 - Offset : 0x190
    
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
        
        // Consumer_Timer3_cc1 - Offset : 0x194
    
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
        
        // Consumer_Timer3_cc2 - Offset : 0x198
    
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
        
        // Consumer_Timer3_dti - Offset : 0x19C
    
        protected IValueRegisterField consumer_timer3_dti_prssel_field;
        partial void Consumer_Timer3_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_dti_Write(uint a, uint b);
        partial void Consumer_Timer3_dti_Read(uint a, uint b);
        
        // Consumer_Timer3_dtifs1 - Offset : 0x1A0
    
        protected IValueRegisterField consumer_timer3_dtifs1_prssel_field;
        partial void Consumer_Timer3_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer3_dtifs1_Read(uint a, uint b);
        
        // Consumer_Timer3_dtifs2 - Offset : 0x1A4
    
        protected IValueRegisterField consumer_timer3_dtifs2_prssel_field;
        partial void Consumer_Timer3_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer3_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer3_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer3_dtifs2_Read(uint a, uint b);
        
        // Consumer_Timer4_cc0 - Offset : 0x1A8
    
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
        
        // Consumer_Timer4_cc1 - Offset : 0x1AC
    
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
        
        // Consumer_Timer4_cc2 - Offset : 0x1B0
    
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
        
        // Consumer_Timer4_dti - Offset : 0x1B4
    
        protected IValueRegisterField consumer_timer4_dti_prssel_field;
        partial void Consumer_Timer4_dti_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_dti_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_dti_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_dti_Write(uint a, uint b);
        partial void Consumer_Timer4_dti_Read(uint a, uint b);
        
        // Consumer_Timer4_dtifs1 - Offset : 0x1B8
    
        protected IValueRegisterField consumer_timer4_dtifs1_prssel_field;
        partial void Consumer_Timer4_dtifs1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_dtifs1_Write(uint a, uint b);
        partial void Consumer_Timer4_dtifs1_Read(uint a, uint b);
        
        // Consumer_Timer4_dtifs2 - Offset : 0x1BC
    
        protected IValueRegisterField consumer_timer4_dtifs2_prssel_field;
        partial void Consumer_Timer4_dtifs2_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs2_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Timer4_dtifs2_Prssel_ValueProvider(ulong a);
        partial void Consumer_Timer4_dtifs2_Write(uint a, uint b);
        partial void Consumer_Timer4_dtifs2_Read(uint a, uint b);
        
        // Consumer_Usart0_clk - Offset : 0x1C0
    
        protected IValueRegisterField consumer_usart0_clk_prssel_field;
        partial void Consumer_Usart0_clk_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_clk_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_clk_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_clk_Write(uint a, uint b);
        partial void Consumer_Usart0_clk_Read(uint a, uint b);
        
        // Consumer_Usart0_ir - Offset : 0x1C4
    
        protected IValueRegisterField consumer_usart0_ir_prssel_field;
        partial void Consumer_Usart0_ir_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_ir_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_ir_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_ir_Write(uint a, uint b);
        partial void Consumer_Usart0_ir_Read(uint a, uint b);
        
        // Consumer_Usart0_rx - Offset : 0x1C8
    
        protected IValueRegisterField consumer_usart0_rx_prssel_field;
        partial void Consumer_Usart0_rx_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_rx_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_rx_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_rx_Write(uint a, uint b);
        partial void Consumer_Usart0_rx_Read(uint a, uint b);
        
        // Consumer_Usart0_trigger - Offset : 0x1CC
    
        protected IValueRegisterField consumer_usart0_trigger_prssel_field;
        partial void Consumer_Usart0_trigger_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Usart0_trigger_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Usart0_trigger_Prssel_ValueProvider(ulong a);
        partial void Consumer_Usart0_trigger_Write(uint a, uint b);
        partial void Consumer_Usart0_trigger_Read(uint a, uint b);
        
        // Consumer_Vdac0_asynctrigch0 - Offset : 0x1DC
    
        protected IValueRegisterField consumer_vdac0_asynctrigch0_prssel_field;
        partial void Consumer_Vdac0_asynctrigch0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac0_asynctrigch0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac0_asynctrigch0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Vdac0_asynctrigch0_Write(uint a, uint b);
        partial void Consumer_Vdac0_asynctrigch0_Read(uint a, uint b);
        
        // Consumer_Vdac0_asynctrigch1 - Offset : 0x1E0
    
        protected IValueRegisterField consumer_vdac0_asynctrigch1_prssel_field;
        partial void Consumer_Vdac0_asynctrigch1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac0_asynctrigch1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac0_asynctrigch1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Vdac0_asynctrigch1_Write(uint a, uint b);
        partial void Consumer_Vdac0_asynctrigch1_Read(uint a, uint b);
        
        // Consumer_Vdac0_synctrigch0 - Offset : 0x1E4
    
        protected IValueRegisterField consumer_vdac0_synctrigch0_sprssel_field;
        partial void Consumer_Vdac0_synctrigch0_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac0_synctrigch0_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac0_synctrigch0_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Vdac0_synctrigch0_Write(uint a, uint b);
        partial void Consumer_Vdac0_synctrigch0_Read(uint a, uint b);
        
        // Consumer_Vdac0_synctrigch1 - Offset : 0x1E8
    
        protected IValueRegisterField consumer_vdac0_synctrigch1_sprssel_field;
        partial void Consumer_Vdac0_synctrigch1_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac0_synctrigch1_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac0_synctrigch1_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Vdac0_synctrigch1_Write(uint a, uint b);
        partial void Consumer_Vdac0_synctrigch1_Read(uint a, uint b);
        
        // Consumer_Vdac1_asynctrigch0 - Offset : 0x1EC
    
        protected IValueRegisterField consumer_vdac1_asynctrigch0_prssel_field;
        partial void Consumer_Vdac1_asynctrigch0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac1_asynctrigch0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac1_asynctrigch0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Vdac1_asynctrigch0_Write(uint a, uint b);
        partial void Consumer_Vdac1_asynctrigch0_Read(uint a, uint b);
        
        // Consumer_Vdac1_asynctrigch1 - Offset : 0x1F0
    
        protected IValueRegisterField consumer_vdac1_asynctrigch1_prssel_field;
        partial void Consumer_Vdac1_asynctrigch1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac1_asynctrigch1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac1_asynctrigch1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Vdac1_asynctrigch1_Write(uint a, uint b);
        partial void Consumer_Vdac1_asynctrigch1_Read(uint a, uint b);
        
        // Consumer_Vdac1_synctrigch0 - Offset : 0x1F4
    
        protected IValueRegisterField consumer_vdac1_synctrigch0_sprssel_field;
        partial void Consumer_Vdac1_synctrigch0_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac1_synctrigch0_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac1_synctrigch0_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Vdac1_synctrigch0_Write(uint a, uint b);
        partial void Consumer_Vdac1_synctrigch0_Read(uint a, uint b);
        
        // Consumer_Vdac1_synctrigch1 - Offset : 0x1F8
    
        protected IValueRegisterField consumer_vdac1_synctrigch1_sprssel_field;
        partial void Consumer_Vdac1_synctrigch1_Sprssel_Write(ulong a, ulong b);
        partial void Consumer_Vdac1_synctrigch1_Sprssel_Read(ulong a, ulong b);
        partial void Consumer_Vdac1_synctrigch1_Sprssel_ValueProvider(ulong a);
        partial void Consumer_Vdac1_synctrigch1_Write(uint a, uint b);
        partial void Consumer_Vdac1_synctrigch1_Read(uint a, uint b);
        
        // Consumer_Wdog0_src0 - Offset : 0x1FC
    
        protected IValueRegisterField consumer_wdog0_src0_prssel_field;
        partial void Consumer_Wdog0_src0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Wdog0_src0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Wdog0_src0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Wdog0_src0_Write(uint a, uint b);
        partial void Consumer_Wdog0_src0_Read(uint a, uint b);
        
        // Consumer_Wdog0_src1 - Offset : 0x200
    
        protected IValueRegisterField consumer_wdog0_src1_prssel_field;
        partial void Consumer_Wdog0_src1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Wdog0_src1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Wdog0_src1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Wdog0_src1_Write(uint a, uint b);
        partial void Consumer_Wdog0_src1_Read(uint a, uint b);
        
        // Consumer_Wdog1_src0 - Offset : 0x204
    
        protected IValueRegisterField consumer_wdog1_src0_prssel_field;
        partial void Consumer_Wdog1_src0_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Wdog1_src0_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Wdog1_src0_Prssel_ValueProvider(ulong a);
        partial void Consumer_Wdog1_src0_Write(uint a, uint b);
        partial void Consumer_Wdog1_src0_Read(uint a, uint b);
        
        // Consumer_Wdog1_src1 - Offset : 0x208
    
        protected IValueRegisterField consumer_wdog1_src1_prssel_field;
        partial void Consumer_Wdog1_src1_Prssel_Write(ulong a, ulong b);
        partial void Consumer_Wdog1_src1_Prssel_Read(ulong a, ulong b);
        partial void Consumer_Wdog1_src1_Prssel_ValueProvider(ulong a);
        partial void Consumer_Wdog1_src1_Write(uint a, uint b);
        partial void Consumer_Wdog1_src1_Read(uint a, uint b);
        
        // Drpu_Rpuratd0 - Offset : 0x20C
    
        protected IFlagRegisterField drpu_rpuratd0_ratdenable_bit;
        partial void Drpu_Rpuratd0_Ratdenable_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdenable_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdenable_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasyncswpulse_bit;
        partial void Drpu_Rpuratd0_Ratdasyncswpulse_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasyncswpulse_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasyncswpulse_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasyncswlevel_bit;
        partial void Drpu_Rpuratd0_Ratdasyncswlevel_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasyncswlevel_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasyncswlevel_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch0ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch0ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch0ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch0ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch1ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch1ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch1ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch1ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch2ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch2ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch2ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch2ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch3ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch3ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch3ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch3ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch4ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch4ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch4ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch4ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch5ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch5ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch5ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch5ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch6ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch6ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch6ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch6ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch7ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch7ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch7ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch7ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch8ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch8ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch8ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch8ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch9ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch9ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch9ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch9ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch10ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch10ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch10ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch10ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch11ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch11ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch11ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch11ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch12ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch12ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch12ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch12ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch13ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch13ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch13ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch13ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch14ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch14ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch14ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch14ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdasynch15ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdasynch15ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch15ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdasynch15ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdsynch0ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdsynch0ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch0ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch0ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdsynch1ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdsynch1ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch1ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch1ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdsynch2ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdsynch2ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch2ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch2ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdsynch3ctrl_bit;
        partial void Drpu_Rpuratd0_Ratdsynch3ctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch3ctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdsynch3ctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdcmucaldn_bit;
        partial void Drpu_Rpuratd0_Ratdcmucaldn_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdcmucaldn_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdcmucaldn_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdcmucalup_bit;
        partial void Drpu_Rpuratd0_Ratdcmucalup_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdcmucalup_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdcmucalup_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdeusart0clk_bit;
        partial void Drpu_Rpuratd0_Ratdeusart0clk_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart0clk_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart0clk_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdeusart0rx_bit;
        partial void Drpu_Rpuratd0_Ratdeusart0rx_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart0rx_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart0rx_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdeusart0trigger_bit;
        partial void Drpu_Rpuratd0_Ratdeusart0trigger_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart0trigger_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart0trigger_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdeusart1clk_bit;
        partial void Drpu_Rpuratd0_Ratdeusart1clk_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart1clk_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdeusart1clk_ValueProvider(bool a);
        partial void Drpu_Rpuratd0_Write(uint a, uint b);
        partial void Drpu_Rpuratd0_Read(uint a, uint b);
        
        // Drpu_Rpuratd1 - Offset : 0x210
    
        protected IFlagRegisterField drpu_rpuratd1_ratdeusart1rx_bit;
        partial void Drpu_Rpuratd1_Ratdeusart1rx_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdeusart1rx_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdeusart1rx_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdeusart1trigger_bit;
        partial void Drpu_Rpuratd1_Ratdeusart1trigger_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdeusart1trigger_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdeusart1trigger_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdfrcrxraw_bit;
        partial void Drpu_Rpuratd1_Ratdfrcrxraw_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdfrcrxraw_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdfrcrxraw_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdiadc0scantrigger_bit;
        partial void Drpu_Rpuratd1_Ratdiadc0scantrigger_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdiadc0scantrigger_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdiadc0scantrigger_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdiadc0singletrigger_bit;
        partial void Drpu_Rpuratd1_Ratdiadc0singletrigger_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdiadc0singletrigger_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdiadc0singletrigger_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdldmaxbardmareq0_bit;
        partial void Drpu_Rpuratd1_Ratdldmaxbardmareq0_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdldmaxbardmareq0_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdldmaxbardmareq0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdldmaxbardmareq1_bit;
        partial void Drpu_Rpuratd1_Ratdldmaxbardmareq1_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdldmaxbardmareq1_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdldmaxbardmareq1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdletimerclear_bit;
        partial void Drpu_Rpuratd1_Ratdletimerclear_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdletimerclear_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdletimerclear_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdletimerstart_bit;
        partial void Drpu_Rpuratd1_Ratdletimerstart_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdletimerstart_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdletimerstart_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdletimerstop_bit;
        partial void Drpu_Rpuratd1_Ratdletimerstop_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdletimerstop_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdletimerstop_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdmodemdin_bit;
        partial void Drpu_Rpuratd1_Ratdmodemdin_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdmodemdin_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdmodemdin_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdmodempaen_bit;
        partial void Drpu_Rpuratd1_Ratdmodempaen_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdmodempaen_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdmodempaen_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdpcnt0s0in_bit;
        partial void Drpu_Rpuratd1_Ratdpcnt0s0in_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdpcnt0s0in_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdpcnt0s0in_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdpcnt0s1in_bit;
        partial void Drpu_Rpuratd1_Ratdpcnt0s1in_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdpcnt0s1in_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdpcnt0s1in_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimercc0_bit;
        partial void Drpu_Rpuratd1_Ratdprotimercc0_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc0_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimercc1_bit;
        partial void Drpu_Rpuratd1_Ratdprotimercc1_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc1_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimercc2_bit;
        partial void Drpu_Rpuratd1_Ratdprotimercc2_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc2_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimercc3_bit;
        partial void Drpu_Rpuratd1_Ratdprotimercc3_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc3_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc3_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimercc4_bit;
        partial void Drpu_Rpuratd1_Ratdprotimercc4_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc4_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimercc4_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimerlbtpause_bit;
        partial void Drpu_Rpuratd1_Ratdprotimerlbtpause_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerlbtpause_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerlbtpause_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimerlbtstart_bit;
        partial void Drpu_Rpuratd1_Ratdprotimerlbtstart_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerlbtstart_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerlbtstart_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimerlbtstop_bit;
        partial void Drpu_Rpuratd1_Ratdprotimerlbtstop_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerlbtstop_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerlbtstop_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimerrtcctrigger_bit;
        partial void Drpu_Rpuratd1_Ratdprotimerrtcctrigger_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerrtcctrigger_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerrtcctrigger_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimerstart_bit;
        partial void Drpu_Rpuratd1_Ratdprotimerstart_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerstart_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerstart_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdprotimerstop_bit;
        partial void Drpu_Rpuratd1_Ratdprotimerstop_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerstop_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdprotimerstop_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdracclr_bit;
        partial void Drpu_Rpuratd1_Ratdracclr_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracclr_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracclr_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdracctiin0_bit;
        partial void Drpu_Rpuratd1_Ratdracctiin0_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin0_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdracctiin1_bit;
        partial void Drpu_Rpuratd1_Ratdracctiin1_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin1_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdracctiin2_bit;
        partial void Drpu_Rpuratd1_Ratdracctiin2_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin2_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdracctiin3_bit;
        partial void Drpu_Rpuratd1_Ratdracctiin3_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin3_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracctiin3_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdracforcetx_bit;
        partial void Drpu_Rpuratd1_Ratdracforcetx_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracforcetx_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracforcetx_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd1_ratdracrxdis_bit;
        partial void Drpu_Rpuratd1_Ratdracrxdis_Write(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracrxdis_Read(bool a, bool b);
        partial void Drpu_Rpuratd1_Ratdracrxdis_ValueProvider(bool a);
        partial void Drpu_Rpuratd1_Write(uint a, uint b);
        partial void Drpu_Rpuratd1_Read(uint a, uint b);
        
        // Drpu_Rpuratd2 - Offset : 0x214
    
        protected IFlagRegisterField drpu_rpuratd2_ratdracrxen_bit;
        partial void Drpu_Rpuratd2_Ratdracrxen_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdracrxen_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdracrxen_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdractxen_bit;
        partial void Drpu_Rpuratd2_Ratdractxen_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdractxen_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdractxen_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsetampertampersrc25_bit;
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc25_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc25_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc25_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsetampertampersrc26_bit;
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc26_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc26_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc26_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsetampertampersrc27_bit;
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc27_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc27_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc27_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsetampertampersrc28_bit;
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc28_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc28_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc28_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsetampertampersrc29_bit;
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc29_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc29_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc29_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsetampertampersrc30_bit;
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc30_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc30_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc30_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsetampertampersrc31_bit;
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc31_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc31_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsetampertampersrc31_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsysrtc0in0_bit;
        partial void Drpu_Rpuratd2_Ratdsysrtc0in0_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsysrtc0in0_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsysrtc0in0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsysrtc0in1_bit;
        partial void Drpu_Rpuratd2_Ratdsysrtc0in1_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsysrtc0in1_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsysrtc0in1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsyxo0oscreq_bit;
        partial void Drpu_Rpuratd2_Ratdsyxo0oscreq_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsyxo0oscreq_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsyxo0oscreq_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdsyxo0timeout_bit;
        partial void Drpu_Rpuratd2_Ratdsyxo0timeout_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsyxo0timeout_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdsyxo0timeout_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdcorectiin0_bit;
        partial void Drpu_Rpuratd2_Ratdcorectiin0_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin0_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdcorectiin1_bit;
        partial void Drpu_Rpuratd2_Ratdcorectiin1_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin1_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdcorectiin2_bit;
        partial void Drpu_Rpuratd2_Ratdcorectiin2_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin2_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdcorectiin3_bit;
        partial void Drpu_Rpuratd2_Ratdcorectiin3_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin3_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorectiin3_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdcorem33rxev_bit;
        partial void Drpu_Rpuratd2_Ratdcorem33rxev_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorem33rxev_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdcorem33rxev_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer0cc0_bit;
        partial void Drpu_Rpuratd2_Ratdtimer0cc0_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0cc0_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0cc0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer0cc1_bit;
        partial void Drpu_Rpuratd2_Ratdtimer0cc1_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0cc1_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0cc1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer0cc2_bit;
        partial void Drpu_Rpuratd2_Ratdtimer0cc2_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0cc2_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0cc2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer0dti_bit;
        partial void Drpu_Rpuratd2_Ratdtimer0dti_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0dti_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0dti_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer0dtifs1_bit;
        partial void Drpu_Rpuratd2_Ratdtimer0dtifs1_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0dtifs1_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0dtifs1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer0dtifs2_bit;
        partial void Drpu_Rpuratd2_Ratdtimer0dtifs2_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0dtifs2_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer0dtifs2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer1cc0_bit;
        partial void Drpu_Rpuratd2_Ratdtimer1cc0_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1cc0_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1cc0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer1cc1_bit;
        partial void Drpu_Rpuratd2_Ratdtimer1cc1_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1cc1_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1cc1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer1cc2_bit;
        partial void Drpu_Rpuratd2_Ratdtimer1cc2_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1cc2_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1cc2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer1dti_bit;
        partial void Drpu_Rpuratd2_Ratdtimer1dti_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1dti_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1dti_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer1dtifs1_bit;
        partial void Drpu_Rpuratd2_Ratdtimer1dtifs1_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1dtifs1_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1dtifs1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer1dtifs2_bit;
        partial void Drpu_Rpuratd2_Ratdtimer1dtifs2_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1dtifs2_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer1dtifs2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer2cc0_bit;
        partial void Drpu_Rpuratd2_Ratdtimer2cc0_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer2cc0_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer2cc0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd2_ratdtimer2cc1_bit;
        partial void Drpu_Rpuratd2_Ratdtimer2cc1_Write(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer2cc1_Read(bool a, bool b);
        partial void Drpu_Rpuratd2_Ratdtimer2cc1_ValueProvider(bool a);
        partial void Drpu_Rpuratd2_Write(uint a, uint b);
        partial void Drpu_Rpuratd2_Read(uint a, uint b);
        
        // Drpu_Rpuratd3 - Offset : 0x218
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer2cc2_bit;
        partial void Drpu_Rpuratd3_Ratdtimer2cc2_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2cc2_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2cc2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer2dti_bit;
        partial void Drpu_Rpuratd3_Ratdtimer2dti_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2dti_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2dti_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer2dtifs1_bit;
        partial void Drpu_Rpuratd3_Ratdtimer2dtifs1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2dtifs1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2dtifs1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer2dtifs2_bit;
        partial void Drpu_Rpuratd3_Ratdtimer2dtifs2_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2dtifs2_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer2dtifs2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer3cc0_bit;
        partial void Drpu_Rpuratd3_Ratdtimer3cc0_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3cc0_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3cc0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer3cc1_bit;
        partial void Drpu_Rpuratd3_Ratdtimer3cc1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3cc1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3cc1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer3cc2_bit;
        partial void Drpu_Rpuratd3_Ratdtimer3cc2_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3cc2_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3cc2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer3dti_bit;
        partial void Drpu_Rpuratd3_Ratdtimer3dti_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3dti_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3dti_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer3dtifs1_bit;
        partial void Drpu_Rpuratd3_Ratdtimer3dtifs1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3dtifs1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3dtifs1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer3dtifs2_bit;
        partial void Drpu_Rpuratd3_Ratdtimer3dtifs2_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3dtifs2_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer3dtifs2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer4cc0_bit;
        partial void Drpu_Rpuratd3_Ratdtimer4cc0_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4cc0_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4cc0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer4cc1_bit;
        partial void Drpu_Rpuratd3_Ratdtimer4cc1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4cc1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4cc1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer4cc2_bit;
        partial void Drpu_Rpuratd3_Ratdtimer4cc2_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4cc2_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4cc2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer4dti_bit;
        partial void Drpu_Rpuratd3_Ratdtimer4dti_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4dti_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4dti_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer4dtifs1_bit;
        partial void Drpu_Rpuratd3_Ratdtimer4dtifs1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4dtifs1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4dtifs1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdtimer4dtifs2_bit;
        partial void Drpu_Rpuratd3_Ratdtimer4dtifs2_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4dtifs2_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdtimer4dtifs2_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdusart0clk_bit;
        partial void Drpu_Rpuratd3_Ratdusart0clk_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0clk_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0clk_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdusart0ir_bit;
        partial void Drpu_Rpuratd3_Ratdusart0ir_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0ir_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0ir_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdusart0rx_bit;
        partial void Drpu_Rpuratd3_Ratdusart0rx_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0rx_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0rx_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdusart0trigger_bit;
        partial void Drpu_Rpuratd3_Ratdusart0trigger_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0trigger_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdusart0trigger_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac0asynctrigch0_bit;
        partial void Drpu_Rpuratd3_Ratdvdac0asynctrigch0_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0asynctrigch0_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0asynctrigch0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac0asynctrigch1_bit;
        partial void Drpu_Rpuratd3_Ratdvdac0asynctrigch1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0asynctrigch1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0asynctrigch1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac0synctrigch0_bit;
        partial void Drpu_Rpuratd3_Ratdvdac0synctrigch0_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0synctrigch0_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0synctrigch0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac0synctrigch1_bit;
        partial void Drpu_Rpuratd3_Ratdvdac0synctrigch1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0synctrigch1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac0synctrigch1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac1asynctrigch0_bit;
        partial void Drpu_Rpuratd3_Ratdvdac1asynctrigch0_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1asynctrigch0_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1asynctrigch0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac1asynctrigch1_bit;
        partial void Drpu_Rpuratd3_Ratdvdac1asynctrigch1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1asynctrigch1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1asynctrigch1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac1synctrigch0_bit;
        partial void Drpu_Rpuratd3_Ratdvdac1synctrigch0_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1synctrigch0_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1synctrigch0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdvdac1synctrigch1_bit;
        partial void Drpu_Rpuratd3_Ratdvdac1synctrigch1_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1synctrigch1_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdvdac1synctrigch1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd3_ratdwdog0src0_bit;
        partial void Drpu_Rpuratd3_Ratdwdog0src0_Write(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdwdog0src0_Read(bool a, bool b);
        partial void Drpu_Rpuratd3_Ratdwdog0src0_ValueProvider(bool a);
        partial void Drpu_Rpuratd3_Write(uint a, uint b);
        partial void Drpu_Rpuratd3_Read(uint a, uint b);
        
        // Drpu_Rpuratd4 - Offset : 0x21C
    
        protected IFlagRegisterField drpu_rpuratd4_ratdwdog0src1_bit;
        partial void Drpu_Rpuratd4_Ratdwdog0src1_Write(bool a, bool b);
        partial void Drpu_Rpuratd4_Ratdwdog0src1_Read(bool a, bool b);
        partial void Drpu_Rpuratd4_Ratdwdog0src1_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd4_ratdwdog1src0_bit;
        partial void Drpu_Rpuratd4_Ratdwdog1src0_Write(bool a, bool b);
        partial void Drpu_Rpuratd4_Ratdwdog1src0_Read(bool a, bool b);
        partial void Drpu_Rpuratd4_Ratdwdog1src0_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd4_ratdwdog1src1_bit;
        partial void Drpu_Rpuratd4_Ratdwdog1src1_Write(bool a, bool b);
        partial void Drpu_Rpuratd4_Ratdwdog1src1_Read(bool a, bool b);
        partial void Drpu_Rpuratd4_Ratdwdog1src1_ValueProvider(bool a);
        partial void Drpu_Rpuratd4_Write(uint a, uint b);
        partial void Drpu_Rpuratd4_Read(uint a, uint b);
        partial void PRS_Reset();

        partial void EFR32xG2_PRS_3_Constructor();

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
            Async_Ch12_Ctrl = 0x48,
            Async_Ch13_Ctrl = 0x4C,
            Async_Ch14_Ctrl = 0x50,
            Async_Ch15_Ctrl = 0x54,
            Sync_Ch0_Ctrl = 0x58,
            Sync_Ch1_Ctrl = 0x5C,
            Sync_Ch2_Ctrl = 0x60,
            Sync_Ch3_Ctrl = 0x64,
            Consumer_Cmu_caldn = 0x68,
            Consumer_Cmu_calup = 0x6C,
            Consumer_Eusart0_clk = 0x70,
            Consumer_Eusart0_rx = 0x74,
            Consumer_Eusart0_trigger = 0x78,
            Consumer_Eusart1_clk = 0x7C,
            Consumer_Eusart1_rx = 0x80,
            Consumer_Eusart1_trigger = 0x84,
            Consumer_Frc_rxraw = 0x88,
            Consumer_Iadc0_scantrigger = 0x8C,
            Consumer_Iadc0_singletrigger = 0x90,
            Consumer_Ldmaxbar_dmareq0 = 0x94,
            Consumer_Ldmaxbar_dmareq1 = 0x98,
            Consumer_Letimer0_clear = 0x9C,
            Consumer_Letimer0_start = 0xA0,
            Consumer_Letimer0_stop = 0xA4,
            Consumer_Modem_din = 0xA8,
            Consumer_Modem_paen = 0xAC,
            Consumer_Pcnt0_s0in = 0xB0,
            Consumer_Pcnt0_s1in = 0xB4,
            Consumer_Protimer_cc0 = 0xB8,
            Consumer_Protimer_cc1 = 0xBC,
            Consumer_Protimer_cc2 = 0xC0,
            Consumer_Protimer_cc3 = 0xC4,
            Consumer_Protimer_cc4 = 0xC8,
            Consumer_Protimer_lbtpause = 0xCC,
            Consumer_Protimer_lbtstart = 0xD0,
            Consumer_Protimer_lbtstop = 0xD4,
            Consumer_Protimer_rtcctrigger = 0xD8,
            Consumer_Protimer_start = 0xDC,
            Consumer_Protimer_stop = 0xE0,
            Consumer_Rac_clr = 0xE4,
            Consumer_Rac_ctiin0 = 0xE8,
            Consumer_Rac_ctiin1 = 0xEC,
            Consumer_Rac_ctiin2 = 0xF0,
            Consumer_Rac_ctiin3 = 0xF4,
            Consumer_Rac_forcetx = 0xF8,
            Consumer_Rac_rxdis = 0xFC,
            Consumer_Rac_rxen = 0x100,
            Consumer_Rac_txen = 0x104,
            Consumer_Setamper_tampersrc25 = 0x108,
            Consumer_Setamper_tampersrc26 = 0x10C,
            Consumer_Setamper_tampersrc27 = 0x110,
            Consumer_Setamper_tampersrc28 = 0x114,
            Consumer_Setamper_tampersrc29 = 0x118,
            Consumer_Setamper_tampersrc30 = 0x11C,
            Consumer_Setamper_tampersrc31 = 0x120,
            Consumer_Sysrtc0_in0 = 0x124,
            Consumer_Sysrtc0_in1 = 0x128,
            Consumer_Hfxo0_oscreq = 0x12C,
            Consumer_Hfxo0_timeout = 0x130,
            Consumer_Core_ctiin0 = 0x134,
            Consumer_Core_ctiin1 = 0x138,
            Consumer_Core_ctiin2 = 0x13C,
            Consumer_Core_ctiin3 = 0x140,
            Consumer_Core_m33rxev = 0x144,
            Consumer_Timer0_cc0 = 0x148,
            Consumer_Timer0_cc1 = 0x14C,
            Consumer_Timer0_cc2 = 0x150,
            Consumer_Timer0_dti = 0x154,
            Consumer_Timer0_dtifs1 = 0x158,
            Consumer_Timer0_dtifs2 = 0x15C,
            Consumer_Timer1_cc0 = 0x160,
            Consumer_Timer1_cc1 = 0x164,
            Consumer_Timer1_cc2 = 0x168,
            Consumer_Timer1_dti = 0x16C,
            Consumer_Timer1_dtifs1 = 0x170,
            Consumer_Timer1_dtifs2 = 0x174,
            Consumer_Timer2_cc0 = 0x178,
            Consumer_Timer2_cc1 = 0x17C,
            Consumer_Timer2_cc2 = 0x180,
            Consumer_Timer2_dti = 0x184,
            Consumer_Timer2_dtifs1 = 0x188,
            Consumer_Timer2_dtifs2 = 0x18C,
            Consumer_Timer3_cc0 = 0x190,
            Consumer_Timer3_cc1 = 0x194,
            Consumer_Timer3_cc2 = 0x198,
            Consumer_Timer3_dti = 0x19C,
            Consumer_Timer3_dtifs1 = 0x1A0,
            Consumer_Timer3_dtifs2 = 0x1A4,
            Consumer_Timer4_cc0 = 0x1A8,
            Consumer_Timer4_cc1 = 0x1AC,
            Consumer_Timer4_cc2 = 0x1B0,
            Consumer_Timer4_dti = 0x1B4,
            Consumer_Timer4_dtifs1 = 0x1B8,
            Consumer_Timer4_dtifs2 = 0x1BC,
            Consumer_Usart0_clk = 0x1C0,
            Consumer_Usart0_ir = 0x1C4,
            Consumer_Usart0_rx = 0x1C8,
            Consumer_Usart0_trigger = 0x1CC,
            Consumer_Vdac0_asynctrigch0 = 0x1DC,
            Consumer_Vdac0_asynctrigch1 = 0x1E0,
            Consumer_Vdac0_synctrigch0 = 0x1E4,
            Consumer_Vdac0_synctrigch1 = 0x1E8,
            Consumer_Vdac1_asynctrigch0 = 0x1EC,
            Consumer_Vdac1_asynctrigch1 = 0x1F0,
            Consumer_Vdac1_synctrigch0 = 0x1F4,
            Consumer_Vdac1_synctrigch1 = 0x1F8,
            Consumer_Wdog0_src0 = 0x1FC,
            Consumer_Wdog0_src1 = 0x200,
            Consumer_Wdog1_src0 = 0x204,
            Consumer_Wdog1_src1 = 0x208,
            Drpu_Rpuratd0 = 0x20C,
            Drpu_Rpuratd1 = 0x210,
            Drpu_Rpuratd2 = 0x214,
            Drpu_Rpuratd3 = 0x218,
            Drpu_Rpuratd4 = 0x21C,
            
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
            Async_Ch12_Ctrl_SET = 0x1048,
            Async_Ch13_Ctrl_SET = 0x104C,
            Async_Ch14_Ctrl_SET = 0x1050,
            Async_Ch15_Ctrl_SET = 0x1054,
            Sync_Ch0_Ctrl_SET = 0x1058,
            Sync_Ch1_Ctrl_SET = 0x105C,
            Sync_Ch2_Ctrl_SET = 0x1060,
            Sync_Ch3_Ctrl_SET = 0x1064,
            Consumer_Cmu_caldn_SET = 0x1068,
            Consumer_Cmu_calup_SET = 0x106C,
            Consumer_Eusart0_clk_SET = 0x1070,
            Consumer_Eusart0_rx_SET = 0x1074,
            Consumer_Eusart0_trigger_SET = 0x1078,
            Consumer_Eusart1_clk_SET = 0x107C,
            Consumer_Eusart1_rx_SET = 0x1080,
            Consumer_Eusart1_trigger_SET = 0x1084,
            Consumer_Frc_rxraw_SET = 0x1088,
            Consumer_Iadc0_scantrigger_SET = 0x108C,
            Consumer_Iadc0_singletrigger_SET = 0x1090,
            Consumer_Ldmaxbar_dmareq0_SET = 0x1094,
            Consumer_Ldmaxbar_dmareq1_SET = 0x1098,
            Consumer_Letimer0_clear_SET = 0x109C,
            Consumer_Letimer0_start_SET = 0x10A0,
            Consumer_Letimer0_stop_SET = 0x10A4,
            Consumer_Modem_din_SET = 0x10A8,
            Consumer_Modem_paen_SET = 0x10AC,
            Consumer_Pcnt0_s0in_SET = 0x10B0,
            Consumer_Pcnt0_s1in_SET = 0x10B4,
            Consumer_Protimer_cc0_SET = 0x10B8,
            Consumer_Protimer_cc1_SET = 0x10BC,
            Consumer_Protimer_cc2_SET = 0x10C0,
            Consumer_Protimer_cc3_SET = 0x10C4,
            Consumer_Protimer_cc4_SET = 0x10C8,
            Consumer_Protimer_lbtpause_SET = 0x10CC,
            Consumer_Protimer_lbtstart_SET = 0x10D0,
            Consumer_Protimer_lbtstop_SET = 0x10D4,
            Consumer_Protimer_rtcctrigger_SET = 0x10D8,
            Consumer_Protimer_start_SET = 0x10DC,
            Consumer_Protimer_stop_SET = 0x10E0,
            Consumer_Rac_clr_SET = 0x10E4,
            Consumer_Rac_ctiin0_SET = 0x10E8,
            Consumer_Rac_ctiin1_SET = 0x10EC,
            Consumer_Rac_ctiin2_SET = 0x10F0,
            Consumer_Rac_ctiin3_SET = 0x10F4,
            Consumer_Rac_forcetx_SET = 0x10F8,
            Consumer_Rac_rxdis_SET = 0x10FC,
            Consumer_Rac_rxen_SET = 0x1100,
            Consumer_Rac_txen_SET = 0x1104,
            Consumer_Setamper_tampersrc25_SET = 0x1108,
            Consumer_Setamper_tampersrc26_SET = 0x110C,
            Consumer_Setamper_tampersrc27_SET = 0x1110,
            Consumer_Setamper_tampersrc28_SET = 0x1114,
            Consumer_Setamper_tampersrc29_SET = 0x1118,
            Consumer_Setamper_tampersrc30_SET = 0x111C,
            Consumer_Setamper_tampersrc31_SET = 0x1120,
            Consumer_Sysrtc0_in0_SET = 0x1124,
            Consumer_Sysrtc0_in1_SET = 0x1128,
            Consumer_Hfxo0_oscreq_SET = 0x112C,
            Consumer_Hfxo0_timeout_SET = 0x1130,
            Consumer_Core_ctiin0_SET = 0x1134,
            Consumer_Core_ctiin1_SET = 0x1138,
            Consumer_Core_ctiin2_SET = 0x113C,
            Consumer_Core_ctiin3_SET = 0x1140,
            Consumer_Core_m33rxev_SET = 0x1144,
            Consumer_Timer0_cc0_SET = 0x1148,
            Consumer_Timer0_cc1_SET = 0x114C,
            Consumer_Timer0_cc2_SET = 0x1150,
            Consumer_Timer0_dti_SET = 0x1154,
            Consumer_Timer0_dtifs1_SET = 0x1158,
            Consumer_Timer0_dtifs2_SET = 0x115C,
            Consumer_Timer1_cc0_SET = 0x1160,
            Consumer_Timer1_cc1_SET = 0x1164,
            Consumer_Timer1_cc2_SET = 0x1168,
            Consumer_Timer1_dti_SET = 0x116C,
            Consumer_Timer1_dtifs1_SET = 0x1170,
            Consumer_Timer1_dtifs2_SET = 0x1174,
            Consumer_Timer2_cc0_SET = 0x1178,
            Consumer_Timer2_cc1_SET = 0x117C,
            Consumer_Timer2_cc2_SET = 0x1180,
            Consumer_Timer2_dti_SET = 0x1184,
            Consumer_Timer2_dtifs1_SET = 0x1188,
            Consumer_Timer2_dtifs2_SET = 0x118C,
            Consumer_Timer3_cc0_SET = 0x1190,
            Consumer_Timer3_cc1_SET = 0x1194,
            Consumer_Timer3_cc2_SET = 0x1198,
            Consumer_Timer3_dti_SET = 0x119C,
            Consumer_Timer3_dtifs1_SET = 0x11A0,
            Consumer_Timer3_dtifs2_SET = 0x11A4,
            Consumer_Timer4_cc0_SET = 0x11A8,
            Consumer_Timer4_cc1_SET = 0x11AC,
            Consumer_Timer4_cc2_SET = 0x11B0,
            Consumer_Timer4_dti_SET = 0x11B4,
            Consumer_Timer4_dtifs1_SET = 0x11B8,
            Consumer_Timer4_dtifs2_SET = 0x11BC,
            Consumer_Usart0_clk_SET = 0x11C0,
            Consumer_Usart0_ir_SET = 0x11C4,
            Consumer_Usart0_rx_SET = 0x11C8,
            Consumer_Usart0_trigger_SET = 0x11CC,
            Consumer_Vdac0_asynctrigch0_SET = 0x11DC,
            Consumer_Vdac0_asynctrigch1_SET = 0x11E0,
            Consumer_Vdac0_synctrigch0_SET = 0x11E4,
            Consumer_Vdac0_synctrigch1_SET = 0x11E8,
            Consumer_Vdac1_asynctrigch0_SET = 0x11EC,
            Consumer_Vdac1_asynctrigch1_SET = 0x11F0,
            Consumer_Vdac1_synctrigch0_SET = 0x11F4,
            Consumer_Vdac1_synctrigch1_SET = 0x11F8,
            Consumer_Wdog0_src0_SET = 0x11FC,
            Consumer_Wdog0_src1_SET = 0x1200,
            Consumer_Wdog1_src0_SET = 0x1204,
            Consumer_Wdog1_src1_SET = 0x1208,
            Drpu_Rpuratd0_SET = 0x120C,
            Drpu_Rpuratd1_SET = 0x1210,
            Drpu_Rpuratd2_SET = 0x1214,
            Drpu_Rpuratd3_SET = 0x1218,
            Drpu_Rpuratd4_SET = 0x121C,
            
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
            Async_Ch12_Ctrl_CLR = 0x2048,
            Async_Ch13_Ctrl_CLR = 0x204C,
            Async_Ch14_Ctrl_CLR = 0x2050,
            Async_Ch15_Ctrl_CLR = 0x2054,
            Sync_Ch0_Ctrl_CLR = 0x2058,
            Sync_Ch1_Ctrl_CLR = 0x205C,
            Sync_Ch2_Ctrl_CLR = 0x2060,
            Sync_Ch3_Ctrl_CLR = 0x2064,
            Consumer_Cmu_caldn_CLR = 0x2068,
            Consumer_Cmu_calup_CLR = 0x206C,
            Consumer_Eusart0_clk_CLR = 0x2070,
            Consumer_Eusart0_rx_CLR = 0x2074,
            Consumer_Eusart0_trigger_CLR = 0x2078,
            Consumer_Eusart1_clk_CLR = 0x207C,
            Consumer_Eusart1_rx_CLR = 0x2080,
            Consumer_Eusart1_trigger_CLR = 0x2084,
            Consumer_Frc_rxraw_CLR = 0x2088,
            Consumer_Iadc0_scantrigger_CLR = 0x208C,
            Consumer_Iadc0_singletrigger_CLR = 0x2090,
            Consumer_Ldmaxbar_dmareq0_CLR = 0x2094,
            Consumer_Ldmaxbar_dmareq1_CLR = 0x2098,
            Consumer_Letimer0_clear_CLR = 0x209C,
            Consumer_Letimer0_start_CLR = 0x20A0,
            Consumer_Letimer0_stop_CLR = 0x20A4,
            Consumer_Modem_din_CLR = 0x20A8,
            Consumer_Modem_paen_CLR = 0x20AC,
            Consumer_Pcnt0_s0in_CLR = 0x20B0,
            Consumer_Pcnt0_s1in_CLR = 0x20B4,
            Consumer_Protimer_cc0_CLR = 0x20B8,
            Consumer_Protimer_cc1_CLR = 0x20BC,
            Consumer_Protimer_cc2_CLR = 0x20C0,
            Consumer_Protimer_cc3_CLR = 0x20C4,
            Consumer_Protimer_cc4_CLR = 0x20C8,
            Consumer_Protimer_lbtpause_CLR = 0x20CC,
            Consumer_Protimer_lbtstart_CLR = 0x20D0,
            Consumer_Protimer_lbtstop_CLR = 0x20D4,
            Consumer_Protimer_rtcctrigger_CLR = 0x20D8,
            Consumer_Protimer_start_CLR = 0x20DC,
            Consumer_Protimer_stop_CLR = 0x20E0,
            Consumer_Rac_clr_CLR = 0x20E4,
            Consumer_Rac_ctiin0_CLR = 0x20E8,
            Consumer_Rac_ctiin1_CLR = 0x20EC,
            Consumer_Rac_ctiin2_CLR = 0x20F0,
            Consumer_Rac_ctiin3_CLR = 0x20F4,
            Consumer_Rac_forcetx_CLR = 0x20F8,
            Consumer_Rac_rxdis_CLR = 0x20FC,
            Consumer_Rac_rxen_CLR = 0x2100,
            Consumer_Rac_txen_CLR = 0x2104,
            Consumer_Setamper_tampersrc25_CLR = 0x2108,
            Consumer_Setamper_tampersrc26_CLR = 0x210C,
            Consumer_Setamper_tampersrc27_CLR = 0x2110,
            Consumer_Setamper_tampersrc28_CLR = 0x2114,
            Consumer_Setamper_tampersrc29_CLR = 0x2118,
            Consumer_Setamper_tampersrc30_CLR = 0x211C,
            Consumer_Setamper_tampersrc31_CLR = 0x2120,
            Consumer_Sysrtc0_in0_CLR = 0x2124,
            Consumer_Sysrtc0_in1_CLR = 0x2128,
            Consumer_Hfxo0_oscreq_CLR = 0x212C,
            Consumer_Hfxo0_timeout_CLR = 0x2130,
            Consumer_Core_ctiin0_CLR = 0x2134,
            Consumer_Core_ctiin1_CLR = 0x2138,
            Consumer_Core_ctiin2_CLR = 0x213C,
            Consumer_Core_ctiin3_CLR = 0x2140,
            Consumer_Core_m33rxev_CLR = 0x2144,
            Consumer_Timer0_cc0_CLR = 0x2148,
            Consumer_Timer0_cc1_CLR = 0x214C,
            Consumer_Timer0_cc2_CLR = 0x2150,
            Consumer_Timer0_dti_CLR = 0x2154,
            Consumer_Timer0_dtifs1_CLR = 0x2158,
            Consumer_Timer0_dtifs2_CLR = 0x215C,
            Consumer_Timer1_cc0_CLR = 0x2160,
            Consumer_Timer1_cc1_CLR = 0x2164,
            Consumer_Timer1_cc2_CLR = 0x2168,
            Consumer_Timer1_dti_CLR = 0x216C,
            Consumer_Timer1_dtifs1_CLR = 0x2170,
            Consumer_Timer1_dtifs2_CLR = 0x2174,
            Consumer_Timer2_cc0_CLR = 0x2178,
            Consumer_Timer2_cc1_CLR = 0x217C,
            Consumer_Timer2_cc2_CLR = 0x2180,
            Consumer_Timer2_dti_CLR = 0x2184,
            Consumer_Timer2_dtifs1_CLR = 0x2188,
            Consumer_Timer2_dtifs2_CLR = 0x218C,
            Consumer_Timer3_cc0_CLR = 0x2190,
            Consumer_Timer3_cc1_CLR = 0x2194,
            Consumer_Timer3_cc2_CLR = 0x2198,
            Consumer_Timer3_dti_CLR = 0x219C,
            Consumer_Timer3_dtifs1_CLR = 0x21A0,
            Consumer_Timer3_dtifs2_CLR = 0x21A4,
            Consumer_Timer4_cc0_CLR = 0x21A8,
            Consumer_Timer4_cc1_CLR = 0x21AC,
            Consumer_Timer4_cc2_CLR = 0x21B0,
            Consumer_Timer4_dti_CLR = 0x21B4,
            Consumer_Timer4_dtifs1_CLR = 0x21B8,
            Consumer_Timer4_dtifs2_CLR = 0x21BC,
            Consumer_Usart0_clk_CLR = 0x21C0,
            Consumer_Usart0_ir_CLR = 0x21C4,
            Consumer_Usart0_rx_CLR = 0x21C8,
            Consumer_Usart0_trigger_CLR = 0x21CC,
            Consumer_Vdac0_asynctrigch0_CLR = 0x21DC,
            Consumer_Vdac0_asynctrigch1_CLR = 0x21E0,
            Consumer_Vdac0_synctrigch0_CLR = 0x21E4,
            Consumer_Vdac0_synctrigch1_CLR = 0x21E8,
            Consumer_Vdac1_asynctrigch0_CLR = 0x21EC,
            Consumer_Vdac1_asynctrigch1_CLR = 0x21F0,
            Consumer_Vdac1_synctrigch0_CLR = 0x21F4,
            Consumer_Vdac1_synctrigch1_CLR = 0x21F8,
            Consumer_Wdog0_src0_CLR = 0x21FC,
            Consumer_Wdog0_src1_CLR = 0x2200,
            Consumer_Wdog1_src0_CLR = 0x2204,
            Consumer_Wdog1_src1_CLR = 0x2208,
            Drpu_Rpuratd0_CLR = 0x220C,
            Drpu_Rpuratd1_CLR = 0x2210,
            Drpu_Rpuratd2_CLR = 0x2214,
            Drpu_Rpuratd3_CLR = 0x2218,
            Drpu_Rpuratd4_CLR = 0x221C,
            
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
            Async_Ch12_Ctrl_TGL = 0x3048,
            Async_Ch13_Ctrl_TGL = 0x304C,
            Async_Ch14_Ctrl_TGL = 0x3050,
            Async_Ch15_Ctrl_TGL = 0x3054,
            Sync_Ch0_Ctrl_TGL = 0x3058,
            Sync_Ch1_Ctrl_TGL = 0x305C,
            Sync_Ch2_Ctrl_TGL = 0x3060,
            Sync_Ch3_Ctrl_TGL = 0x3064,
            Consumer_Cmu_caldn_TGL = 0x3068,
            Consumer_Cmu_calup_TGL = 0x306C,
            Consumer_Eusart0_clk_TGL = 0x3070,
            Consumer_Eusart0_rx_TGL = 0x3074,
            Consumer_Eusart0_trigger_TGL = 0x3078,
            Consumer_Eusart1_clk_TGL = 0x307C,
            Consumer_Eusart1_rx_TGL = 0x3080,
            Consumer_Eusart1_trigger_TGL = 0x3084,
            Consumer_Frc_rxraw_TGL = 0x3088,
            Consumer_Iadc0_scantrigger_TGL = 0x308C,
            Consumer_Iadc0_singletrigger_TGL = 0x3090,
            Consumer_Ldmaxbar_dmareq0_TGL = 0x3094,
            Consumer_Ldmaxbar_dmareq1_TGL = 0x3098,
            Consumer_Letimer0_clear_TGL = 0x309C,
            Consumer_Letimer0_start_TGL = 0x30A0,
            Consumer_Letimer0_stop_TGL = 0x30A4,
            Consumer_Modem_din_TGL = 0x30A8,
            Consumer_Modem_paen_TGL = 0x30AC,
            Consumer_Pcnt0_s0in_TGL = 0x30B0,
            Consumer_Pcnt0_s1in_TGL = 0x30B4,
            Consumer_Protimer_cc0_TGL = 0x30B8,
            Consumer_Protimer_cc1_TGL = 0x30BC,
            Consumer_Protimer_cc2_TGL = 0x30C0,
            Consumer_Protimer_cc3_TGL = 0x30C4,
            Consumer_Protimer_cc4_TGL = 0x30C8,
            Consumer_Protimer_lbtpause_TGL = 0x30CC,
            Consumer_Protimer_lbtstart_TGL = 0x30D0,
            Consumer_Protimer_lbtstop_TGL = 0x30D4,
            Consumer_Protimer_rtcctrigger_TGL = 0x30D8,
            Consumer_Protimer_start_TGL = 0x30DC,
            Consumer_Protimer_stop_TGL = 0x30E0,
            Consumer_Rac_clr_TGL = 0x30E4,
            Consumer_Rac_ctiin0_TGL = 0x30E8,
            Consumer_Rac_ctiin1_TGL = 0x30EC,
            Consumer_Rac_ctiin2_TGL = 0x30F0,
            Consumer_Rac_ctiin3_TGL = 0x30F4,
            Consumer_Rac_forcetx_TGL = 0x30F8,
            Consumer_Rac_rxdis_TGL = 0x30FC,
            Consumer_Rac_rxen_TGL = 0x3100,
            Consumer_Rac_txen_TGL = 0x3104,
            Consumer_Setamper_tampersrc25_TGL = 0x3108,
            Consumer_Setamper_tampersrc26_TGL = 0x310C,
            Consumer_Setamper_tampersrc27_TGL = 0x3110,
            Consumer_Setamper_tampersrc28_TGL = 0x3114,
            Consumer_Setamper_tampersrc29_TGL = 0x3118,
            Consumer_Setamper_tampersrc30_TGL = 0x311C,
            Consumer_Setamper_tampersrc31_TGL = 0x3120,
            Consumer_Sysrtc0_in0_TGL = 0x3124,
            Consumer_Sysrtc0_in1_TGL = 0x3128,
            Consumer_Hfxo0_oscreq_TGL = 0x312C,
            Consumer_Hfxo0_timeout_TGL = 0x3130,
            Consumer_Core_ctiin0_TGL = 0x3134,
            Consumer_Core_ctiin1_TGL = 0x3138,
            Consumer_Core_ctiin2_TGL = 0x313C,
            Consumer_Core_ctiin3_TGL = 0x3140,
            Consumer_Core_m33rxev_TGL = 0x3144,
            Consumer_Timer0_cc0_TGL = 0x3148,
            Consumer_Timer0_cc1_TGL = 0x314C,
            Consumer_Timer0_cc2_TGL = 0x3150,
            Consumer_Timer0_dti_TGL = 0x3154,
            Consumer_Timer0_dtifs1_TGL = 0x3158,
            Consumer_Timer0_dtifs2_TGL = 0x315C,
            Consumer_Timer1_cc0_TGL = 0x3160,
            Consumer_Timer1_cc1_TGL = 0x3164,
            Consumer_Timer1_cc2_TGL = 0x3168,
            Consumer_Timer1_dti_TGL = 0x316C,
            Consumer_Timer1_dtifs1_TGL = 0x3170,
            Consumer_Timer1_dtifs2_TGL = 0x3174,
            Consumer_Timer2_cc0_TGL = 0x3178,
            Consumer_Timer2_cc1_TGL = 0x317C,
            Consumer_Timer2_cc2_TGL = 0x3180,
            Consumer_Timer2_dti_TGL = 0x3184,
            Consumer_Timer2_dtifs1_TGL = 0x3188,
            Consumer_Timer2_dtifs2_TGL = 0x318C,
            Consumer_Timer3_cc0_TGL = 0x3190,
            Consumer_Timer3_cc1_TGL = 0x3194,
            Consumer_Timer3_cc2_TGL = 0x3198,
            Consumer_Timer3_dti_TGL = 0x319C,
            Consumer_Timer3_dtifs1_TGL = 0x31A0,
            Consumer_Timer3_dtifs2_TGL = 0x31A4,
            Consumer_Timer4_cc0_TGL = 0x31A8,
            Consumer_Timer4_cc1_TGL = 0x31AC,
            Consumer_Timer4_cc2_TGL = 0x31B0,
            Consumer_Timer4_dti_TGL = 0x31B4,
            Consumer_Timer4_dtifs1_TGL = 0x31B8,
            Consumer_Timer4_dtifs2_TGL = 0x31BC,
            Consumer_Usart0_clk_TGL = 0x31C0,
            Consumer_Usart0_ir_TGL = 0x31C4,
            Consumer_Usart0_rx_TGL = 0x31C8,
            Consumer_Usart0_trigger_TGL = 0x31CC,
            Consumer_Vdac0_asynctrigch0_TGL = 0x31DC,
            Consumer_Vdac0_asynctrigch1_TGL = 0x31E0,
            Consumer_Vdac0_synctrigch0_TGL = 0x31E4,
            Consumer_Vdac0_synctrigch1_TGL = 0x31E8,
            Consumer_Vdac1_asynctrigch0_TGL = 0x31EC,
            Consumer_Vdac1_asynctrigch1_TGL = 0x31F0,
            Consumer_Vdac1_synctrigch0_TGL = 0x31F4,
            Consumer_Vdac1_synctrigch1_TGL = 0x31F8,
            Consumer_Wdog0_src0_TGL = 0x31FC,
            Consumer_Wdog0_src1_TGL = 0x3200,
            Consumer_Wdog1_src0_TGL = 0x3204,
            Consumer_Wdog1_src1_TGL = 0x3208,
            Drpu_Rpuratd0_TGL = 0x320C,
            Drpu_Rpuratd1_TGL = 0x3210,
            Drpu_Rpuratd2_TGL = 0x3214,
            Drpu_Rpuratd3_TGL = 0x3218,
            Drpu_Rpuratd4_TGL = 0x321C,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}