//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    CMU, Generated on : 2025-07-23 19:39:11.009240
    CMU, ID Version : 52bfa38ff76643fb8fa46c63ecf729ea.1 */

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
    public partial class SiLabs_CMU_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Wdoglock, GenerateWdoglockRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Test, GenerateTestRegister()},
                {(long)Registers.Testhv, GenerateTesthvRegister()},
                {(long)Registers.Testclken, GenerateTestclkenRegister()},
                {(long)Registers.Calcmd, GenerateCalcmdRegister()},
                {(long)Registers.Calctrl, GenerateCalctrlRegister()},
                {(long)Registers.Calcnt, GenerateCalcntRegister()},
                {(long)Registers.Clken0, GenerateClken0Register()},
                {(long)Registers.Clken1, GenerateClken1Register()},
                {(long)Registers.Sysclkctrl, GenerateSysclkctrlRegister()},
                {(long)Registers.Traceclkctrl, GenerateTraceclkctrlRegister()},
                {(long)Registers.Exportclkctrl, GenerateExportclkctrlRegister()},
                {(long)Registers.Dpllrefclkctrl, GenerateDpllrefclkctrlRegister()},
                {(long)Registers.Em01grpaclkctrl, GenerateEm01grpaclkctrlRegister()},
                {(long)Registers.Em01grpbclkctrl, GenerateEm01grpbclkctrlRegister()},
                {(long)Registers.Em23grpaclkctrl, GenerateEm23grpaclkctrlRegister()},
                {(long)Registers.Em4grpaclkctrl, GenerateEm4grpaclkctrlRegister()},
                {(long)Registers.Iadcclkctrl, GenerateIadcclkctrlRegister()},
                {(long)Registers.Wdog0clkctrl, GenerateWdog0clkctrlRegister()},
                {(long)Registers.Euart0clkctrl, GenerateEuart0clkctrlRegister()},
                {(long)Registers.Synthclkctrl, GenerateSynthclkctrlRegister()},
                {(long)Registers.Rtccclkctrl, GenerateRtccclkctrlRegister()},
                {(long)Registers.Prortcclkctrl, GeneratePrortcclkctrlRegister()},
                {(long)Registers.Cryptoaccclkctrl, GenerateCryptoaccclkctrlRegister()},
                {(long)Registers.Radioclkctrl, GenerateRadioclkctrlRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            CMU_Reset();
        }
        
        protected enum STATUS_WDOGLOCK
        {
            UNLOCKED = 0, // WDOG configuration lock is unlocked
            LOCKED = 1, // WDOG configuration lock is locked
        }
        
        protected enum STATUS_LOCK
        {
            UNLOCKED = 0, // Configuration lock is unlocked
            LOCKED = 1, // Configuration lock is locked
        }
        
        protected enum TEST_CLKOUTHIDDENSEL
        {
            DISABLED = 0, // CLKOUTHIDDEN is not clocked
            ULFRCO4K = 1, // ULFRCO 4KHz output is clocking CLKOUTHIDDEN
            ULFRCODUTY = 2, // ULFRCO duty output is clocking CLKOUTHIDDEN
            ULFRCOLV = 3, // ULFRCO LV output is clocking CLKOUTHIDDEN
            ULFRCOHV = 4, // ULFRCO HV output is clocking CLKOUTHIDDEN
            LFRCOLV = 5, // LFRCO LV output is clocking CLKOUTHIDDEN
            LFRCOHV = 6, // LFRCO HV output is clocking CLKOUTHIDDEN
            LFXOLV = 7, // LFXO LV output is clocking CLKOUTHIDDEN
            LFXOHV = 8, // LFXO HV output is clocking CLKOUTHIDDEN
            FSRCOLV = 9, // FSRCO LV output is clocking CLKOUTHIDDEN
            FSRCOLVEMU = 10, // FSRCO LV EMU output is clocking CLKOUTHIDDEN
            FSRCOHV = 11, // FSRCO HV output is clocking CLKOUTHIDDEN
            PFMOSC = 12, // PFMOSC output is clocking CLKOUTHIDDEN
            TEMPOSC = 13, // TEMPOSC output is clocking CLKOUTHIDDEN
            HFRCODPLLPD1RT = 14, // HFRCODPLL PD1 retimed output is clocking CLKOUTHIDDEN
            HFRCODPLLPD1NR = 15, // HFRCODPLL PD1 non-retimed output is clocking CLKOUTHIDDEN
            HFXORT = 16, // HFXO retimed output is clocking CLKOUTHIDDEN
            HFXONR = 17, // HFXO non-retimed output is clocking CLKOUTHIDDEN
            BIASOSC = 18, // BIASOSC is clocking CLKOUTHIDDEN
            RFIFADC = 19, // RF IFADC clock is clocking CLKOUTHIDDEN
            RFAUXADC = 20, // RF AUXADC clock is clocking CLKOUTHIDDEN
            RFMMD = 21, // RF MMD clock is clocking CLKOUTHIDDEN
        }
        
        protected enum CALCTRL_UPSEL
        {
            DISABLED = 0, // Up-counter is not clocked
            PRS = 1, // PRS CMU_CALUP consumer is clocking up-counter
            HFXO = 2, // HFXO is clocking up-counter
            LFXO = 3, // LFXO is clocking up-counter
            HFRCODPLL = 4, // HFRCODPLL is clocking up-counter
            TEMPOSC = 5, // TEMPOSC is clocking up-counter
            PFMOSC = 6, // PFMOSC is clocking up-counter
            BIASOSC = 7, // BIASOSC is clocking up-counter
            FSRCO = 8, // FSRCO is clocking up-counter
            LFRCO = 9, // LFRCO is clocking up-counter
            ULFRCO = 10, // ULFRCO is clocking up-counter
        }
        
        protected enum CALCTRL_DOWNSEL
        {
            DISABLED = 0, // Down-counter is not clocked
            HCLK = 1, // HCLK is clocking down-counter
            PRS = 2, // PRS CMU_CALDN consumer is clocking down-counter
            HFXO = 3, // HFXO is clocking down-counter
            LFXO = 4, // LFXO is clocking down-counter
            HFRCODPLL = 5, // HFRCODPLL is clocking down-counter
            TEMPOSC = 6, // TEMPOSC is clocking down-counter
            PFMOSC = 7, // PFMOSC is clocking down-counter
            BIASOSC = 8, // BIASOSC is clocking down-counter
            FSRCO = 9, // FSRCO is clocking down-counter
            LFRCO = 10, // LFRCO is clocking down-counter
            ULFRCO = 11, // ULFRCO is clocking down-counter
        }
        
        protected enum SYSCLKCTRL_CLKSEL
        {
            FSRCO = 1, // FSRCO is clocking SYSCLK
            HFRCODPLL = 2, // HFRCODPLL is clocking SYSCLK
            HFXO = 3, // HFXO is clocking SYSCLK
            CLKIN0 = 4, // CLKIN0 is clocking SYSCLK
        }
        
        protected enum SYSCLKCTRL_LSPCLKPRESC
        {
            DIV2 = 0, // LSPCLK is PCLK divided by 2
            DIV1 = 1, // LSPCLK is PCLK divided by 1
        }
        
        protected enum SYSCLKCTRL_PCLKPRESC
        {
            DIV1 = 0, // PCLK is HCLK divided by 1
            DIV2 = 1, // PCLK is HCLK divided by 2
        }
        
        protected enum SYSCLKCTRL_HCLKPRESC
        {
            DIV1 = 0, // HCLK is SYSCLK divided by 1
            DIV2 = 1, // HCLK is SYSCLK divided by 2
            DIV4 = 3, // HCLK is SYSCLK divided by 4
            DIV8 = 7, // HCLK is SYSCLK divided by 8
            DIV16 = 15, // HCLK is SYSCLK divided by 16
        }
        
        protected enum SYSCLKCTRL_RHCLKPRESC
        {
            DIV1 = 0, // Radio HCLK is SYSCLK divided by 1
            DIV2 = 1, // Radio HCLK is SYSCLK divided by 2
        }
        
        protected enum TRACECLKCTRL_PRESC
        {
            DIV1 = 0, // TRACECLK is SYSCLK divided by 1
            DIV2 = 1, // TRACECLK is SYSCLK divided by 2
            DIV4 = 3, // TRACECLK is SYSCLK divided by 4
        }
        
        protected enum EXPORTCLKCTRL_CLKOUTSEL0
        {
            DISABLED = 0, // CLKOUT0 is not clocked
            HCLK = 1, // HCLK is clocking CLKOUT0
            HFEXPCLK = 2, // EXPORTCLK is clocking CLKOUT0
            ULFRCO = 3, // ULFRCO is clocking CLKOUT0
            LFRCO = 4, // LFRCO is clocking CLKOUT0
            LFXO = 5, // LFXO is clocking CLKOUT0
            HFRCODPLL = 6, // HFRCODPLL is clocking CLKOUT0
            HFXO = 7, // HFXO is clocking CLKOUT0
            FSRCO = 8, // FSRCO is clocking CLKOUT0
        }
        
        protected enum EXPORTCLKCTRL_CLKOUTSEL1
        {
            DISABLED = 0, // CLKOUT1 is not clocked
            HCLK = 1, // HCLK is clocking CLKOUT1
            HFEXPCLK = 2, // EXPORTCLK is clocking CLKOUT1
            ULFRCO = 3, // ULFRCO is clocking CLKOUT1
            LFRCO = 4, // LFRCO is clocking CLKOUT1
            LFXO = 5, // LFXO is clocking CLKOUT1
            HFRCODPLL = 6, // HFRCODPLL is clocking CLKOUT1
            HFXO = 7, // HFXO is clocking CLKOUT1
            FSRCO = 8, // FSRCO is clocking CLKOUT1
        }
        
        protected enum EXPORTCLKCTRL_CLKOUTSEL2
        {
            DISABLED = 0, // CLKOUT2 is not clocked
            HCLK = 1, // HCLK is clocking CLKOUT2
            HFEXPCLK = 2, // EXPORTCLK is clocking CLKOUT2
            ULFRCO = 3, // ULFRCO is clocking CLKOUT2
            LFRCO = 4, // LFRCO is clocking CLKOUT2
            LFXO = 5, // LFXO is clocking CLKOUT2
            HFRCODPLL = 6, // HFRCODPLL is clocking CLKOUT2
            HFXO = 7, // HFXO is clocking CLKOUT2
            FSRCO = 8, // FSRCO is clocking CLKOUT2
        }
        
        protected enum DPLLREFCLKCTRL_CLKSEL
        {
            DISABLED = 0, // DPLLREFCLK is not clocked
            HFXO = 1, // HFXO is clocking DPLLREFCLK
            LFXO = 2, // LFXO is clocking DPLLREFCLK
            CLKIN0 = 3, // CLKIN0 is clocking DPLLREFCLK
        }
        
        protected enum EM01GRPACLKCTRL_CLKSEL
        {
            DISABLED = 0, // EM01GRPACLK is not clocked
            HFRCODPLL = 1, // HFRCODPLL is clocking EM01GRPACLK
            HFXO = 2, // HFXO is clocking EM01GRPACLK
            FSRCO = 3, // FSRCO is clocking EM01GRPACLK
        }
        
        protected enum EM01GRPBCLKCTRL_CLKSEL
        {
            DISABLED = 0, // EM01GRPBCLK is not clocked
            HFRCODPLL = 1, // HFRCODPLL is clocking EM01GRPBCLK
            HFXO = 2, // HFXO is clocking EM01GRPBCLK
            FSRCO = 3, // FSRCO is clocking EM01GRPBCLK
            CLKIN0 = 4, // CLKIN0 is clocking EM01GRPBCLK
            HFRCODPLLRT = 5, // HFRCODPLL (re-timed) is clocking EM01GRPBCLK
            HFXORT = 6, // HFXO (re-timed) is clocking EM01GRPBCLK
        }
        
        protected enum EM23GRPACLKCTRL_CLKSEL
        {
            DISABLED = 0, // EM23GRPACLK is not clocked
            LFRCO = 1, // LFRCO is clocking EM23GRPACLK
            LFXO = 2, // LFXO is clocking EM23GRPACLK
            ULFRCO = 3, // ULFRCO is clocking EM23GRPACLK
        }
        
        protected enum EM4GRPACLKCTRL_CLKSEL
        {
            DISABLED = 0, // EM4GRPACLK is not clocked
            LFRCO = 1, // LFRCO is clocking EM4GRPACLK
            LFXO = 2, // LFXO is clocking EM4GRPACLK
            ULFRCO = 3, // ULFRCO is clocking EM4GRPACLK
        }
        
        protected enum IADCCLKCTRL_CLKSEL
        {
            DISABLED = 0, // IADCCLK is not clocked
            EM01GRPACLK = 1, // EM01GRPACLK is clocking IADCCLK
            FSRCO = 2, // FSRCO is clocking IADCCLK
        }
        
        protected enum WDOG0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // WDOG0CLK is not clocked
            LFRCO = 1, // LFRCO is clocking WDOG0CLK
            LFXO = 2, // LFXO is clocking WDOG0CLK
            ULFRCO = 3, // ULFRCO is clocking WDOG0CLK
            HCLKDIV1024 = 4, // HCLKDIV1024 is clocking WDOG0CLK
        }
        
        protected enum EUART0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // UART is not clocked
            EM01GRPACLK = 1, // EM01GRPACLK is clocking UART
            EM23GRPACLK = 2, // EM23GRPACLK is clocking UART
        }
        
        protected enum SYNTHCLKCTRL_CLKSEL
        {
            DISABLED = 0, // SYNTHCLK is not clocked
            HFXO = 1, // HFXO is clocking SYNTHCLK
            CLKIN0 = 2, // CLKIN0 is clocking SYNTHCLK
        }
        
        protected enum RTCCCLKCTRL_CLKSEL
        {
            DISABLED = 0, // RTCCCLK is not clocked
            LFRCO = 1, // LFRCO is clocking RTCCCLK
            LFXO = 2, // LFXO is clocking RTCCCLK
            ULFRCO = 3, // ULFRCO is clocking RTCCCLK
        }
        
        protected enum PRORTCCLKCTRL_CLKSEL
        {
            DISABLED = 0, // PRORTCCLK is not clocked
            LFRCO = 1, // LFRCO is clocking PRORTCCLK
            LFXO = 2, // LFXO is clocking PRORTCCLK
            ULFRCO = 3, // ULFRCO is clocking PRORTCCLK
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
        
        // Ctrl - Offset : 0x4
        protected DoubleWordRegister GenerateCtrlRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 8, out ctrl_runningdebugsel_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Runningdebugsel_ValueProvider(_);
                        return ctrl_runningdebugsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Runningdebugsel_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Runningdebugsel_Read(_, __),
                    name: "Runningdebugsel")
            .WithReservedBits(8, 23)
            .WithFlag(31, out ctrl_forceclkin0_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceclkin0_ValueProvider(_);
                        return ctrl_forceclkin0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Forceclkin0_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Forceclkin0_Read(_, __),
                    name: "Forceclkin0")
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write_WithHook(_, __));
        
        // Status - Offset : 0x8
        protected DoubleWordRegister GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_calrdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Calrdy_ValueProvider(_);
                        return status_calrdy_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Calrdy_Read(_, __),
                    name: "Calrdy")
            .WithReservedBits(1, 15)
            .WithFlag(16, out status_runningdebug_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Runningdebug_ValueProvider(_);
                        return status_runningdebug_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Runningdebug_Read(_, __),
                    name: "Runningdebug")
            .WithFlag(17, out status_isforcedclkin0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Isforcedclkin0_ValueProvider(_);
                        return status_isforcedclkin0_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Isforcedclkin0_Read(_, __),
                    name: "Isforcedclkin0")
            .WithReservedBits(18, 12)
            .WithEnumField<DoubleWordRegister, STATUS_WDOGLOCK>(30, 1, out status_wdoglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Wdoglock_ValueProvider(_);
                        return status_wdoglock_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Wdoglock_Read(_, __),
                    name: "Wdoglock")
            .WithEnumField<DoubleWordRegister, STATUS_LOCK>(31, 1, out status_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lock_ValueProvider(_);
                        return status_lock_bit.Value;
                    },
                    
                    readCallback: (_, __) => Status_Lock_Read(_, __),
                    name: "Lock")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Lock - Offset : 0x10
        protected DoubleWordRegister GenerateLockRegister() => new DoubleWordRegister(this, 0x93F7)
            
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Wdoglock - Offset : 0x14
        protected DoubleWordRegister GenerateWdoglockRegister() => new DoubleWordRegister(this, 0x5257)
            
            .WithValueField(0, 16, out wdoglock_lockkey_field, FieldMode.Write,
                    
                    writeCallback: (_, __) => Wdoglock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Wdoglock_Read(_, __))
            .WithWriteCallback((_, __) => Wdoglock_Write(_, __));
        
        // If - Offset : 0x20
        protected DoubleWordRegister GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_calrdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Calrdy_ValueProvider(_);
                        return if_calrdy_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Calrdy_Write(_, __),
                    
                    readCallback: (_, __) => If_Calrdy_Read(_, __),
                    name: "Calrdy")
            .WithFlag(1, out if_calof_bit, 
                    valueProviderCallback: (_) => {
                        If_Calof_ValueProvider(_);
                        return if_calof_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Calof_Write(_, __),
                    
                    readCallback: (_, __) => If_Calof_Read(_, __),
                    name: "Calof")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x24
        protected DoubleWordRegister GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_calrdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Calrdy_ValueProvider(_);
                        return ien_calrdy_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Calrdy_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Calrdy_Read(_, __),
                    name: "Calrdy")
            .WithFlag(1, out ien_calof_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Calof_ValueProvider(_);
                        return ien_calof_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Calof_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Calof_Read(_, __),
                    name: "Calof")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Test - Offset : 0x30
        protected DoubleWordRegister GenerateTestRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, TEST_CLKOUTHIDDENSEL>(0, 5, out test_clkouthiddensel_field, 
                    valueProviderCallback: (_) => {
                        Test_Clkouthiddensel_ValueProvider(_);
                        return test_clkouthiddensel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Test_Clkouthiddensel_Write(_, __),
                    
                    readCallback: (_, __) => Test_Clkouthiddensel_Read(_, __),
                    name: "Clkouthiddensel")
            .WithReservedBits(5, 3)
            .WithFlag(8, out test_entimeout_bit, 
                    valueProviderCallback: (_) => {
                        Test_Entimeout_ValueProvider(_);
                        return test_entimeout_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Test_Entimeout_Write(_, __),
                    
                    readCallback: (_, __) => Test_Entimeout_Read(_, __),
                    name: "Entimeout")
            .WithReservedBits(9, 7)
            
            .WithValueField(16, 8, out test_reqdebugsel0_field, 
                    valueProviderCallback: (_) => {
                        Test_Reqdebugsel0_ValueProvider(_);
                        return test_reqdebugsel0_field.Value;
                    },
                    
                    writeCallback: (_, __) => Test_Reqdebugsel0_Write(_, __),
                    
                    readCallback: (_, __) => Test_Reqdebugsel0_Read(_, __),
                    name: "Reqdebugsel0")
            
            .WithValueField(24, 8, out test_reqdebugsel1_field, 
                    valueProviderCallback: (_) => {
                        Test_Reqdebugsel1_ValueProvider(_);
                        return test_reqdebugsel1_field.Value;
                    },
                    
                    writeCallback: (_, __) => Test_Reqdebugsel1_Write(_, __),
                    
                    readCallback: (_, __) => Test_Reqdebugsel1_Read(_, __),
                    name: "Reqdebugsel1")
            .WithReadCallback((_, __) => Test_Read(_, __))
            .WithWriteCallback((_, __) => Test_Write(_, __));
        
        // Testhv - Offset : 0x34
        protected DoubleWordRegister GenerateTesthvRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out testhv_plsdebugen_bit, 
                    valueProviderCallback: (_) => {
                        Testhv_Plsdebugen_ValueProvider(_);
                        return testhv_plsdebugen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testhv_Plsdebugen_Write(_, __),
                    
                    readCallback: (_, __) => Testhv_Plsdebugen_Read(_, __),
                    name: "Plsdebugen")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Testhv_Read(_, __))
            .WithWriteCallback((_, __) => Testhv_Write(_, __));
        
        // Testclken - Offset : 0x40
        protected DoubleWordRegister GenerateTestclkenRegister() => new DoubleWordRegister(this, 0x1FF)
            .WithFlag(0, out testclken_rootcfg_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Rootcfg_ValueProvider(_);
                        return testclken_rootcfg_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Rootcfg_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Rootcfg_Read(_, __),
                    name: "Rootcfg")
            .WithFlag(1, out testclken_dci_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Dci_ValueProvider(_);
                        return testclken_dci_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Dci_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Dci_Read(_, __),
                    name: "Dci")
            .WithFlag(2, out testclken_busmatrix_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Busmatrix_ValueProvider(_);
                        return testclken_busmatrix_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Busmatrix_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Busmatrix_Read(_, __),
                    name: "Busmatrix")
            .WithFlag(3, out testclken_hostcpu_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Hostcpu_ValueProvider(_);
                        return testclken_hostcpu_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Hostcpu_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Hostcpu_Read(_, __),
                    name: "Hostcpu")
            .WithFlag(4, out testclken_emu_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Emu_ValueProvider(_);
                        return testclken_emu_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Emu_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Emu_Read(_, __),
                    name: "Emu")
            .WithFlag(5, out testclken_cmu_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Cmu_ValueProvider(_);
                        return testclken_cmu_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Cmu_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Cmu_Read(_, __),
                    name: "Cmu")
            .WithFlag(6, out testclken_sysrom_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Sysrom_ValueProvider(_);
                        return testclken_sysrom_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Sysrom_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Sysrom_Read(_, __),
                    name: "Sysrom")
            .WithFlag(7, out testclken_imemflash_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Imemflash_ValueProvider(_);
                        return testclken_imemflash_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Imemflash_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Imemflash_Read(_, __),
                    name: "Imemflash")
            .WithFlag(8, out testclken_ram0_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Ram0_ValueProvider(_);
                        return testclken_ram0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Ram0_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Ram0_Read(_, __),
                    name: "Ram0")
            .WithReservedBits(9, 21)
            .WithFlag(30, out testclken_chiptestctrl_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Chiptestctrl_ValueProvider(_);
                        return testclken_chiptestctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Chiptestctrl_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Chiptestctrl_Read(_, __),
                    name: "Chiptestctrl")
            .WithFlag(31, out testclken_scratchpad_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Scratchpad_ValueProvider(_);
                        return testclken_scratchpad_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Testclken_Scratchpad_Write(_, __),
                    
                    readCallback: (_, __) => Testclken_Scratchpad_Read(_, __),
                    name: "Scratchpad")
            .WithReadCallback((_, __) => Testclken_Read(_, __))
            .WithWriteCallback((_, __) => Testclken_Write(_, __));
        
        // Calcmd - Offset : 0x50
        protected DoubleWordRegister GenerateCalcmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out calcmd_calstart_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Calcmd_Calstart_Write(_, __),
                    name: "Calstart")
            .WithFlag(1, out calcmd_calstop_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Calcmd_Calstop_Write(_, __),
                    name: "Calstop")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Calcmd_Read(_, __))
            .WithWriteCallback((_, __) => Calcmd_Write(_, __));
        
        // Calctrl - Offset : 0x54
        protected DoubleWordRegister GenerateCalctrlRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 20, out calctrl_caltop_field, 
                    valueProviderCallback: (_) => {
                        Calctrl_Caltop_ValueProvider(_);
                        return calctrl_caltop_field.Value;
                    },
                    
                    writeCallback: (_, __) => Calctrl_Caltop_Write(_, __),
                    
                    readCallback: (_, __) => Calctrl_Caltop_Read(_, __),
                    name: "Caltop")
            .WithReservedBits(20, 3)
            .WithFlag(23, out calctrl_cont_bit, 
                    valueProviderCallback: (_) => {
                        Calctrl_Cont_ValueProvider(_);
                        return calctrl_cont_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Calctrl_Cont_Write(_, __),
                    
                    readCallback: (_, __) => Calctrl_Cont_Read(_, __),
                    name: "Cont")
            .WithEnumField<DoubleWordRegister, CALCTRL_UPSEL>(24, 4, out calctrl_upsel_field, 
                    valueProviderCallback: (_) => {
                        Calctrl_Upsel_ValueProvider(_);
                        return calctrl_upsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Calctrl_Upsel_Write(_, __),
                    
                    readCallback: (_, __) => Calctrl_Upsel_Read(_, __),
                    name: "Upsel")
            .WithEnumField<DoubleWordRegister, CALCTRL_DOWNSEL>(28, 4, out calctrl_downsel_field, 
                    valueProviderCallback: (_) => {
                        Calctrl_Downsel_ValueProvider(_);
                        return calctrl_downsel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Calctrl_Downsel_Write(_, __),
                    
                    readCallback: (_, __) => Calctrl_Downsel_Read(_, __),
                    name: "Downsel")
            .WithReadCallback((_, __) => Calctrl_Read(_, __))
            .WithWriteCallback((_, __) => Calctrl_Write(_, __));
        
        // Calcnt - Offset : 0x58
        protected DoubleWordRegister GenerateCalcntRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 20, out calcnt_calcnt_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Calcnt_Calcnt_ValueProvider(_);
                        return calcnt_calcnt_field.Value;
                    },
                    
                    readCallback: (_, __) => Calcnt_Calcnt_Read(_, __),
                    name: "Calcnt")
            .WithReservedBits(20, 12)
            .WithReadCallback((_, __) => Calcnt_Read(_, __))
            .WithWriteCallback((_, __) => Calcnt_Write(_, __));
        
        // Clken0 - Offset : 0x64
        protected DoubleWordRegister GenerateClken0Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out clken0_ldma_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Ldma_ValueProvider(_);
                        return clken0_ldma_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Ldma_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Ldma_Read(_, __),
                    name: "Ldma")
            .WithFlag(1, out clken0_ldmaxbar_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Ldmaxbar_ValueProvider(_);
                        return clken0_ldmaxbar_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Ldmaxbar_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Ldmaxbar_Read(_, __),
                    name: "Ldmaxbar")
            .WithFlag(2, out clken0_radioaes_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Radioaes_ValueProvider(_);
                        return clken0_radioaes_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Radioaes_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Radioaes_Read(_, __),
                    name: "Radioaes")
            .WithFlag(3, out clken0_gpcrc_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Gpcrc_ValueProvider(_);
                        return clken0_gpcrc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Gpcrc_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Gpcrc_Read(_, __),
                    name: "Gpcrc")
            .WithFlag(4, out clken0_timer0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Timer0_ValueProvider(_);
                        return clken0_timer0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Timer0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Timer0_Read(_, __),
                    name: "Timer0")
            .WithFlag(5, out clken0_timer1_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Timer1_ValueProvider(_);
                        return clken0_timer1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Timer1_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Timer1_Read(_, __),
                    name: "Timer1")
            .WithFlag(6, out clken0_timer2_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Timer2_ValueProvider(_);
                        return clken0_timer2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Timer2_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Timer2_Read(_, __),
                    name: "Timer2")
            .WithFlag(7, out clken0_timer3_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Timer3_ValueProvider(_);
                        return clken0_timer3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Timer3_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Timer3_Read(_, __),
                    name: "Timer3")
            .WithFlag(8, out clken0_usart0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Usart0_ValueProvider(_);
                        return clken0_usart0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Usart0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Usart0_Read(_, __),
                    name: "Usart0")
            .WithFlag(9, out clken0_usart1_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Usart1_ValueProvider(_);
                        return clken0_usart1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Usart1_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Usart1_Read(_, __),
                    name: "Usart1")
            .WithFlag(10, out clken0_iadc0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Iadc0_ValueProvider(_);
                        return clken0_iadc0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Iadc0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Iadc0_Read(_, __),
                    name: "Iadc0")
            .WithFlag(11, out clken0_amuxcp0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Amuxcp0_ValueProvider(_);
                        return clken0_amuxcp0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Amuxcp0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Amuxcp0_Read(_, __),
                    name: "Amuxcp0")
            .WithFlag(12, out clken0_letimer0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Letimer0_ValueProvider(_);
                        return clken0_letimer0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Letimer0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Letimer0_Read(_, __),
                    name: "Letimer0")
            .WithFlag(13, out clken0_wdog0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Wdog0_ValueProvider(_);
                        return clken0_wdog0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Wdog0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Wdog0_Read(_, __),
                    name: "Wdog0")
            .WithFlag(14, out clken0_i2c0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_I2c0_ValueProvider(_);
                        return clken0_i2c0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_I2c0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_I2c0_Read(_, __),
                    name: "I2c0")
            .WithFlag(15, out clken0_i2c1_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_I2c1_ValueProvider(_);
                        return clken0_i2c1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_I2c1_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_I2c1_Read(_, __),
                    name: "I2c1")
            .WithFlag(16, out clken0_syscfg_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Syscfg_ValueProvider(_);
                        return clken0_syscfg_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Syscfg_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Syscfg_Read(_, __),
                    name: "Syscfg")
            .WithFlag(17, out clken0_dpll0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Dpll0_ValueProvider(_);
                        return clken0_dpll0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Dpll0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Dpll0_Read(_, __),
                    name: "Dpll0")
            .WithFlag(18, out clken0_hfrco0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Hfrco0_ValueProvider(_);
                        return clken0_hfrco0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Hfrco0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Hfrco0_Read(_, __),
                    name: "Hfrco0")
            .WithFlag(19, out clken0_hfxo0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Hfxo0_ValueProvider(_);
                        return clken0_hfxo0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Hfxo0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Hfxo0_Read(_, __),
                    name: "Hfxo0")
            .WithFlag(20, out clken0_fsrco_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Fsrco_ValueProvider(_);
                        return clken0_fsrco_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Fsrco_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Fsrco_Read(_, __),
                    name: "Fsrco")
            .WithFlag(21, out clken0_lfrco_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Lfrco_ValueProvider(_);
                        return clken0_lfrco_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Lfrco_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Lfrco_Read(_, __),
                    name: "Lfrco")
            .WithFlag(22, out clken0_lfxo_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Lfxo_ValueProvider(_);
                        return clken0_lfxo_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Lfxo_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Lfxo_Read(_, __),
                    name: "Lfxo")
            .WithFlag(23, out clken0_ulfrco_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Ulfrco_ValueProvider(_);
                        return clken0_ulfrco_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Ulfrco_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Ulfrco_Read(_, __),
                    name: "Ulfrco")
            .WithFlag(24, out clken0_euart0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Euart0_ValueProvider(_);
                        return clken0_euart0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Euart0_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Euart0_Read(_, __),
                    name: "Euart0")
            .WithFlag(25, out clken0_pdm_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Pdm_ValueProvider(_);
                        return clken0_pdm_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Pdm_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Pdm_Read(_, __),
                    name: "Pdm")
            .WithFlag(26, out clken0_gpio_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Gpio_ValueProvider(_);
                        return clken0_gpio_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Gpio_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Gpio_Read(_, __),
                    name: "Gpio")
            .WithFlag(27, out clken0_prs_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Prs_ValueProvider(_);
                        return clken0_prs_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Prs_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Prs_Read(_, __),
                    name: "Prs")
            .WithFlag(28, out clken0_buram_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Buram_ValueProvider(_);
                        return clken0_buram_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Buram_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Buram_Read(_, __),
                    name: "Buram")
            .WithFlag(29, out clken0_burtc_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Burtc_ValueProvider(_);
                        return clken0_burtc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Burtc_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Burtc_Read(_, __),
                    name: "Burtc")
            .WithFlag(30, out clken0_rtcc_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Rtcc_ValueProvider(_);
                        return clken0_rtcc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Rtcc_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Rtcc_Read(_, __),
                    name: "Rtcc")
            .WithFlag(31, out clken0_dcdc_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Dcdc_ValueProvider(_);
                        return clken0_dcdc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken0_Dcdc_Write(_, __),
                    
                    readCallback: (_, __) => Clken0_Dcdc_Read(_, __),
                    name: "Dcdc")
            .WithReadCallback((_, __) => Clken0_Read(_, __))
            .WithWriteCallback((_, __) => Clken0_Write(_, __));
        
        // Clken1 - Offset : 0x68
        protected DoubleWordRegister GenerateClken1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out clken1_agc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Agc_ValueProvider(_);
                        return clken1_agc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Agc_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Agc_Read(_, __),
                    name: "Agc")
            .WithFlag(1, out clken1_modem_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Modem_ValueProvider(_);
                        return clken1_modem_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Modem_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Modem_Read(_, __),
                    name: "Modem")
            .WithFlag(2, out clken1_rfcrc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rfcrc_ValueProvider(_);
                        return clken1_rfcrc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Rfcrc_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Rfcrc_Read(_, __),
                    name: "Rfcrc")
            .WithFlag(3, out clken1_frc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Frc_ValueProvider(_);
                        return clken1_frc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Frc_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Frc_Read(_, __),
                    name: "Frc")
            .WithFlag(4, out clken1_protimer_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Protimer_ValueProvider(_);
                        return clken1_protimer_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Protimer_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Protimer_Read(_, __),
                    name: "Protimer")
            .WithFlag(5, out clken1_rac_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rac_ValueProvider(_);
                        return clken1_rac_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Rac_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Rac_Read(_, __),
                    name: "Rac")
            .WithFlag(6, out clken1_synth_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Synth_ValueProvider(_);
                        return clken1_synth_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Synth_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Synth_Read(_, __),
                    name: "Synth")
            .WithFlag(7, out clken1_rdscratchpad_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rdscratchpad_ValueProvider(_);
                        return clken1_rdscratchpad_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Rdscratchpad_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Rdscratchpad_Read(_, __),
                    name: "Rdscratchpad")
            .WithFlag(8, out clken1_rdmailbox0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rdmailbox0_ValueProvider(_);
                        return clken1_rdmailbox0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Rdmailbox0_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Rdmailbox0_Read(_, __),
                    name: "Rdmailbox0")
            .WithFlag(9, out clken1_rdmailbox1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rdmailbox1_ValueProvider(_);
                        return clken1_rdmailbox1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Rdmailbox1_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Rdmailbox1_Read(_, __),
                    name: "Rdmailbox1")
            .WithFlag(10, out clken1_prortc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Prortc_ValueProvider(_);
                        return clken1_prortc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Prortc_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Prortc_Read(_, __),
                    name: "Prortc")
            .WithFlag(11, out clken1_bufc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Bufc_ValueProvider(_);
                        return clken1_bufc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Bufc_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Bufc_Read(_, __),
                    name: "Bufc")
            .WithFlag(12, out clken1_ifadcdebug_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Ifadcdebug_ValueProvider(_);
                        return clken1_ifadcdebug_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Ifadcdebug_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Ifadcdebug_Read(_, __),
                    name: "Ifadcdebug")
            .WithFlag(13, out clken1_cryptoacc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Cryptoacc_ValueProvider(_);
                        return clken1_cryptoacc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Cryptoacc_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Cryptoacc_Read(_, __),
                    name: "Cryptoacc")
            .WithFlag(14, out clken1_rfsense_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rfsense_ValueProvider(_);
                        return clken1_rfsense_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Rfsense_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Rfsense_Read(_, __),
                    name: "Rfsense")
            .WithFlag(15, out clken1_smu_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Smu_ValueProvider(_);
                        return clken1_smu_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Smu_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Smu_Read(_, __),
                    name: "Smu")
            .WithFlag(16, out clken1_icache0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Icache0_ValueProvider(_);
                        return clken1_icache0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Icache0_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Icache0_Read(_, __),
                    name: "Icache0")
            .WithFlag(17, out clken1_msc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Msc_ValueProvider(_);
                        return clken1_msc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Msc_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Msc_Read(_, __),
                    name: "Msc")
            .WithFlag(18, out clken1_timer4_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Timer4_ValueProvider(_);
                        return clken1_timer4_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Clken1_Timer4_Write(_, __),
                    
                    readCallback: (_, __) => Clken1_Timer4_Read(_, __),
                    name: "Timer4")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Clken1_Read(_, __))
            .WithWriteCallback((_, __) => Clken1_Write(_, __));
        
        // Sysclkctrl - Offset : 0x70
        protected DoubleWordRegister GenerateSysclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, SYSCLKCTRL_CLKSEL>(0, 3, out sysclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Sysclkctrl_Clksel_ValueProvider(_);
                        return sysclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Sysclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Sysclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 5)
            .WithEnumField<DoubleWordRegister, SYSCLKCTRL_LSPCLKPRESC>(8, 1, out sysclkctrl_lspclkpresc_bit, 
                    valueProviderCallback: (_) => {
                        Sysclkctrl_Lspclkpresc_ValueProvider(_);
                        return sysclkctrl_lspclkpresc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Sysclkctrl_Lspclkpresc_Write(_, __),
                    
                    readCallback: (_, __) => Sysclkctrl_Lspclkpresc_Read(_, __),
                    name: "Lspclkpresc")
            .WithReservedBits(9, 1)
            .WithEnumField<DoubleWordRegister, SYSCLKCTRL_PCLKPRESC>(10, 1, out sysclkctrl_pclkpresc_bit, 
                    valueProviderCallback: (_) => {
                        Sysclkctrl_Pclkpresc_ValueProvider(_);
                        return sysclkctrl_pclkpresc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Sysclkctrl_Pclkpresc_Write(_, __),
                    
                    readCallback: (_, __) => Sysclkctrl_Pclkpresc_Read(_, __),
                    name: "Pclkpresc")
            .WithReservedBits(11, 1)
            .WithEnumField<DoubleWordRegister, SYSCLKCTRL_HCLKPRESC>(12, 4, out sysclkctrl_hclkpresc_field, 
                    valueProviderCallback: (_) => {
                        Sysclkctrl_Hclkpresc_ValueProvider(_);
                        return sysclkctrl_hclkpresc_field.Value;
                    },
                    
                    writeCallback: (_, __) => Sysclkctrl_Hclkpresc_Write(_, __),
                    
                    readCallback: (_, __) => Sysclkctrl_Hclkpresc_Read(_, __),
                    name: "Hclkpresc")
            .WithEnumField<DoubleWordRegister, SYSCLKCTRL_RHCLKPRESC>(16, 1, out sysclkctrl_rhclkpresc_bit, 
                    valueProviderCallback: (_) => {
                        Sysclkctrl_Rhclkpresc_ValueProvider(_);
                        return sysclkctrl_rhclkpresc_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Sysclkctrl_Rhclkpresc_Write(_, __),
                    
                    readCallback: (_, __) => Sysclkctrl_Rhclkpresc_Read(_, __),
                    name: "Rhclkpresc")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Sysclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Sysclkctrl_Write(_, __));
        
        // Traceclkctrl - Offset : 0x80
        protected DoubleWordRegister GenerateTraceclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 4)
            .WithEnumField<DoubleWordRegister, TRACECLKCTRL_PRESC>(4, 2, out traceclkctrl_presc_field, 
                    valueProviderCallback: (_) => {
                        Traceclkctrl_Presc_ValueProvider(_);
                        return traceclkctrl_presc_field.Value;
                    },
                    
                    writeCallback: (_, __) => Traceclkctrl_Presc_Write(_, __),
                    
                    readCallback: (_, __) => Traceclkctrl_Presc_Read(_, __),
                    name: "Presc")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Traceclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Traceclkctrl_Write(_, __));
        
        // Exportclkctrl - Offset : 0x90
        protected DoubleWordRegister GenerateExportclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, EXPORTCLKCTRL_CLKOUTSEL0>(0, 4, out exportclkctrl_clkoutsel0_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Clkoutsel0_ValueProvider(_);
                        return exportclkctrl_clkoutsel0_field.Value;
                    },
                    
                    writeCallback: (_, __) => Exportclkctrl_Clkoutsel0_Write(_, __),
                    
                    readCallback: (_, __) => Exportclkctrl_Clkoutsel0_Read(_, __),
                    name: "Clkoutsel0")
            .WithReservedBits(4, 4)
            .WithEnumField<DoubleWordRegister, EXPORTCLKCTRL_CLKOUTSEL1>(8, 4, out exportclkctrl_clkoutsel1_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Clkoutsel1_ValueProvider(_);
                        return exportclkctrl_clkoutsel1_field.Value;
                    },
                    
                    writeCallback: (_, __) => Exportclkctrl_Clkoutsel1_Write(_, __),
                    
                    readCallback: (_, __) => Exportclkctrl_Clkoutsel1_Read(_, __),
                    name: "Clkoutsel1")
            .WithReservedBits(12, 4)
            .WithEnumField<DoubleWordRegister, EXPORTCLKCTRL_CLKOUTSEL2>(16, 4, out exportclkctrl_clkoutsel2_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Clkoutsel2_ValueProvider(_);
                        return exportclkctrl_clkoutsel2_field.Value;
                    },
                    
                    writeCallback: (_, __) => Exportclkctrl_Clkoutsel2_Write(_, __),
                    
                    readCallback: (_, __) => Exportclkctrl_Clkoutsel2_Read(_, __),
                    name: "Clkoutsel2")
            .WithReservedBits(20, 4)
            
            .WithValueField(24, 5, out exportclkctrl_presc_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Presc_ValueProvider(_);
                        return exportclkctrl_presc_field.Value;
                    },
                    
                    writeCallback: (_, __) => Exportclkctrl_Presc_Write(_, __),
                    
                    readCallback: (_, __) => Exportclkctrl_Presc_Read(_, __),
                    name: "Presc")
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => Exportclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Exportclkctrl_Write(_, __));
        
        // Dpllrefclkctrl - Offset : 0x100
        protected DoubleWordRegister GenerateDpllrefclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, DPLLREFCLKCTRL_CLKSEL>(0, 2, out dpllrefclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Dpllrefclkctrl_Clksel_ValueProvider(_);
                        return dpllrefclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Dpllrefclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Dpllrefclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Dpllrefclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Dpllrefclkctrl_Write(_, __));
        
        // Em01grpaclkctrl - Offset : 0x120
        protected DoubleWordRegister GenerateEm01grpaclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EM01GRPACLKCTRL_CLKSEL>(0, 2, out em01grpaclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Em01grpaclkctrl_Clksel_ValueProvider(_);
                        return em01grpaclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Em01grpaclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Em01grpaclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Em01grpaclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em01grpaclkctrl_Write(_, __));
        
        // Em01grpbclkctrl - Offset : 0x124
        protected DoubleWordRegister GenerateEm01grpbclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EM01GRPBCLKCTRL_CLKSEL>(0, 3, out em01grpbclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Em01grpbclkctrl_Clksel_ValueProvider(_);
                        return em01grpbclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Em01grpbclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Em01grpbclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Em01grpbclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em01grpbclkctrl_Write(_, __));
        
        // Em23grpaclkctrl - Offset : 0x140
        protected DoubleWordRegister GenerateEm23grpaclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EM23GRPACLKCTRL_CLKSEL>(0, 2, out em23grpaclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Em23grpaclkctrl_Clksel_ValueProvider(_);
                        return em23grpaclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Em23grpaclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Em23grpaclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Em23grpaclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em23grpaclkctrl_Write(_, __));
        
        // Em4grpaclkctrl - Offset : 0x160
        protected DoubleWordRegister GenerateEm4grpaclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EM4GRPACLKCTRL_CLKSEL>(0, 2, out em4grpaclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Em4grpaclkctrl_Clksel_ValueProvider(_);
                        return em4grpaclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Em4grpaclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Em4grpaclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Em4grpaclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em4grpaclkctrl_Write(_, __));
        
        // Iadcclkctrl - Offset : 0x180
        protected DoubleWordRegister GenerateIadcclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, IADCCLKCTRL_CLKSEL>(0, 2, out iadcclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Iadcclkctrl_Clksel_ValueProvider(_);
                        return iadcclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Iadcclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Iadcclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Iadcclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Iadcclkctrl_Write(_, __));
        
        // Wdog0clkctrl - Offset : 0x200
        protected DoubleWordRegister GenerateWdog0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, WDOG0CLKCTRL_CLKSEL>(0, 3, out wdog0clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Wdog0clkctrl_Clksel_ValueProvider(_);
                        return wdog0clkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Wdog0clkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Wdog0clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Wdog0clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Wdog0clkctrl_Write(_, __));
        
        // Euart0clkctrl - Offset : 0x220
        protected DoubleWordRegister GenerateEuart0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EUART0CLKCTRL_CLKSEL>(0, 2, out euart0clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Euart0clkctrl_Clksel_ValueProvider(_);
                        return euart0clkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Euart0clkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Euart0clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Euart0clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Euart0clkctrl_Write(_, __));
        
        // Synthclkctrl - Offset : 0x230
        protected DoubleWordRegister GenerateSynthclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, SYNTHCLKCTRL_CLKSEL>(0, 2, out synthclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Synthclkctrl_Clksel_ValueProvider(_);
                        return synthclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Synthclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Synthclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Synthclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Synthclkctrl_Write(_, __));
        
        // Rtccclkctrl - Offset : 0x240
        protected DoubleWordRegister GenerateRtccclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, RTCCCLKCTRL_CLKSEL>(0, 2, out rtccclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Rtccclkctrl_Clksel_ValueProvider(_);
                        return rtccclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Rtccclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Rtccclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Rtccclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Rtccclkctrl_Write(_, __));
        
        // Prortcclkctrl - Offset : 0x248
        protected DoubleWordRegister GeneratePrortcclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, PRORTCCLKCTRL_CLKSEL>(0, 2, out prortcclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Prortcclkctrl_Clksel_ValueProvider(_);
                        return prortcclkctrl_clksel_field.Value;
                    },
                    
                    writeCallback: (_, __) => Prortcclkctrl_Clksel_Write(_, __),
                    
                    readCallback: (_, __) => Prortcclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Prortcclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Prortcclkctrl_Write(_, __));
        
        // Cryptoaccclkctrl - Offset : 0x260
        protected DoubleWordRegister GenerateCryptoaccclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cryptoaccclkctrl_pken_bit, 
                    valueProviderCallback: (_) => {
                        Cryptoaccclkctrl_Pken_ValueProvider(_);
                        return cryptoaccclkctrl_pken_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cryptoaccclkctrl_Pken_Write(_, __),
                    
                    readCallback: (_, __) => Cryptoaccclkctrl_Pken_Read(_, __),
                    name: "Pken")
            .WithFlag(1, out cryptoaccclkctrl_aesen_bit, 
                    valueProviderCallback: (_) => {
                        Cryptoaccclkctrl_Aesen_ValueProvider(_);
                        return cryptoaccclkctrl_aesen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Cryptoaccclkctrl_Aesen_Write(_, __),
                    
                    readCallback: (_, __) => Cryptoaccclkctrl_Aesen_Read(_, __),
                    name: "Aesen")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Cryptoaccclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Cryptoaccclkctrl_Write(_, __));
        
        // Radioclkctrl - Offset : 0x280
        protected DoubleWordRegister GenerateRadioclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out radioclkctrl_en_bit, 
                    valueProviderCallback: (_) => {
                        Radioclkctrl_En_ValueProvider(_);
                        return radioclkctrl_en_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Radioclkctrl_En_Write(_, __),
                    
                    readCallback: (_, __) => Radioclkctrl_En_Read(_, __),
                    name: "En")
            .WithReservedBits(1, 30)
            .WithFlag(31, out radioclkctrl_dbgclk_bit, 
                    valueProviderCallback: (_) => {
                        Radioclkctrl_Dbgclk_ValueProvider(_);
                        return radioclkctrl_dbgclk_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Radioclkctrl_Dbgclk_Write(_, __),
                    
                    readCallback: (_, __) => Radioclkctrl_Dbgclk_Read(_, __),
                    name: "Dbgclk")
            .WithReadCallback((_, __) => Radioclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Radioclkctrl_Write(_, __));
        

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
    
        protected IValueRegisterField ctrl_runningdebugsel_field;
        partial void Ctrl_Runningdebugsel_Write(ulong a, ulong b);
        partial void Ctrl_Runningdebugsel_Read(ulong a, ulong b);
        partial void Ctrl_Runningdebugsel_ValueProvider(ulong a);
    
        protected IFlagRegisterField ctrl_forceclkin0_bit;
        partial void Ctrl_Forceclkin0_Write(bool a, bool b);
        partial void Ctrl_Forceclkin0_Read(bool a, bool b);
        partial void Ctrl_Forceclkin0_ValueProvider(bool a);
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
        
        
        // Status - Offset : 0x8
    
        protected IFlagRegisterField status_calrdy_bit;
        partial void Status_Calrdy_Read(bool a, bool b);
        partial void Status_Calrdy_ValueProvider(bool a);
    
        protected IFlagRegisterField status_runningdebug_bit;
        partial void Status_Runningdebug_Read(bool a, bool b);
        partial void Status_Runningdebug_ValueProvider(bool a);
    
        protected IFlagRegisterField status_isforcedclkin0_bit;
        partial void Status_Isforcedclkin0_Read(bool a, bool b);
        partial void Status_Isforcedclkin0_ValueProvider(bool a);
    
        protected IEnumRegisterField<STATUS_WDOGLOCK> status_wdoglock_bit;
        partial void Status_Wdoglock_Read(STATUS_WDOGLOCK a, STATUS_WDOGLOCK b);
        partial void Status_Wdoglock_ValueProvider(STATUS_WDOGLOCK a);
    
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);
        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        
        // Lock - Offset : 0x10
    
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);
        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        
        // Wdoglock - Offset : 0x14
    
        protected IValueRegisterField wdoglock_lockkey_field;
        partial void Wdoglock_Lockkey_Write(ulong a, ulong b);
        partial void Wdoglock_Lockkey_ValueProvider(ulong a);
        partial void Wdoglock_Write(uint a, uint b);
        partial void Wdoglock_Read(uint a, uint b);
        
        
        // If - Offset : 0x20
    
        protected IFlagRegisterField if_calrdy_bit;
        partial void If_Calrdy_Write(bool a, bool b);
        partial void If_Calrdy_Read(bool a, bool b);
        partial void If_Calrdy_ValueProvider(bool a);
    
        protected IFlagRegisterField if_calof_bit;
        partial void If_Calof_Write(bool a, bool b);
        partial void If_Calof_Read(bool a, bool b);
        partial void If_Calof_ValueProvider(bool a);
        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        
        // Ien - Offset : 0x24
    
        protected IFlagRegisterField ien_calrdy_bit;
        partial void Ien_Calrdy_Write(bool a, bool b);
        partial void Ien_Calrdy_Read(bool a, bool b);
        partial void Ien_Calrdy_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_calof_bit;
        partial void Ien_Calof_Write(bool a, bool b);
        partial void Ien_Calof_Read(bool a, bool b);
        partial void Ien_Calof_ValueProvider(bool a);
        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        
        // Test - Offset : 0x30
    
        protected IEnumRegisterField<TEST_CLKOUTHIDDENSEL> test_clkouthiddensel_field;
        partial void Test_Clkouthiddensel_Write(TEST_CLKOUTHIDDENSEL a, TEST_CLKOUTHIDDENSEL b);
        partial void Test_Clkouthiddensel_Read(TEST_CLKOUTHIDDENSEL a, TEST_CLKOUTHIDDENSEL b);
        partial void Test_Clkouthiddensel_ValueProvider(TEST_CLKOUTHIDDENSEL a);
    
        protected IFlagRegisterField test_entimeout_bit;
        partial void Test_Entimeout_Write(bool a, bool b);
        partial void Test_Entimeout_Read(bool a, bool b);
        partial void Test_Entimeout_ValueProvider(bool a);
    
        protected IValueRegisterField test_reqdebugsel0_field;
        partial void Test_Reqdebugsel0_Write(ulong a, ulong b);
        partial void Test_Reqdebugsel0_Read(ulong a, ulong b);
        partial void Test_Reqdebugsel0_ValueProvider(ulong a);
    
        protected IValueRegisterField test_reqdebugsel1_field;
        partial void Test_Reqdebugsel1_Write(ulong a, ulong b);
        partial void Test_Reqdebugsel1_Read(ulong a, ulong b);
        partial void Test_Reqdebugsel1_ValueProvider(ulong a);
        partial void Test_Write(uint a, uint b);
        partial void Test_Read(uint a, uint b);
        
        
        // Testhv - Offset : 0x34
    
        protected IFlagRegisterField testhv_plsdebugen_bit;
        partial void Testhv_Plsdebugen_Write(bool a, bool b);
        partial void Testhv_Plsdebugen_Read(bool a, bool b);
        partial void Testhv_Plsdebugen_ValueProvider(bool a);
        partial void Testhv_Write(uint a, uint b);
        partial void Testhv_Read(uint a, uint b);
        
        
        // Testclken - Offset : 0x40
    
        protected IFlagRegisterField testclken_rootcfg_bit;
        partial void Testclken_Rootcfg_Write(bool a, bool b);
        partial void Testclken_Rootcfg_Read(bool a, bool b);
        partial void Testclken_Rootcfg_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_dci_bit;
        partial void Testclken_Dci_Write(bool a, bool b);
        partial void Testclken_Dci_Read(bool a, bool b);
        partial void Testclken_Dci_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_busmatrix_bit;
        partial void Testclken_Busmatrix_Write(bool a, bool b);
        partial void Testclken_Busmatrix_Read(bool a, bool b);
        partial void Testclken_Busmatrix_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_hostcpu_bit;
        partial void Testclken_Hostcpu_Write(bool a, bool b);
        partial void Testclken_Hostcpu_Read(bool a, bool b);
        partial void Testclken_Hostcpu_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_emu_bit;
        partial void Testclken_Emu_Write(bool a, bool b);
        partial void Testclken_Emu_Read(bool a, bool b);
        partial void Testclken_Emu_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_cmu_bit;
        partial void Testclken_Cmu_Write(bool a, bool b);
        partial void Testclken_Cmu_Read(bool a, bool b);
        partial void Testclken_Cmu_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_sysrom_bit;
        partial void Testclken_Sysrom_Write(bool a, bool b);
        partial void Testclken_Sysrom_Read(bool a, bool b);
        partial void Testclken_Sysrom_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_imemflash_bit;
        partial void Testclken_Imemflash_Write(bool a, bool b);
        partial void Testclken_Imemflash_Read(bool a, bool b);
        partial void Testclken_Imemflash_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_ram0_bit;
        partial void Testclken_Ram0_Write(bool a, bool b);
        partial void Testclken_Ram0_Read(bool a, bool b);
        partial void Testclken_Ram0_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_chiptestctrl_bit;
        partial void Testclken_Chiptestctrl_Write(bool a, bool b);
        partial void Testclken_Chiptestctrl_Read(bool a, bool b);
        partial void Testclken_Chiptestctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField testclken_scratchpad_bit;
        partial void Testclken_Scratchpad_Write(bool a, bool b);
        partial void Testclken_Scratchpad_Read(bool a, bool b);
        partial void Testclken_Scratchpad_ValueProvider(bool a);
        partial void Testclken_Write(uint a, uint b);
        partial void Testclken_Read(uint a, uint b);
        
        
        // Calcmd - Offset : 0x50
    
        protected IFlagRegisterField calcmd_calstart_bit;
        partial void Calcmd_Calstart_Write(bool a, bool b);
        partial void Calcmd_Calstart_ValueProvider(bool a);
    
        protected IFlagRegisterField calcmd_calstop_bit;
        partial void Calcmd_Calstop_Write(bool a, bool b);
        partial void Calcmd_Calstop_ValueProvider(bool a);
        partial void Calcmd_Write(uint a, uint b);
        partial void Calcmd_Read(uint a, uint b);
        
        
        // Calctrl - Offset : 0x54
    
        protected IValueRegisterField calctrl_caltop_field;
        partial void Calctrl_Caltop_Write(ulong a, ulong b);
        partial void Calctrl_Caltop_Read(ulong a, ulong b);
        partial void Calctrl_Caltop_ValueProvider(ulong a);
    
        protected IFlagRegisterField calctrl_cont_bit;
        partial void Calctrl_Cont_Write(bool a, bool b);
        partial void Calctrl_Cont_Read(bool a, bool b);
        partial void Calctrl_Cont_ValueProvider(bool a);
    
        protected IEnumRegisterField<CALCTRL_UPSEL> calctrl_upsel_field;
        partial void Calctrl_Upsel_Write(CALCTRL_UPSEL a, CALCTRL_UPSEL b);
        partial void Calctrl_Upsel_Read(CALCTRL_UPSEL a, CALCTRL_UPSEL b);
        partial void Calctrl_Upsel_ValueProvider(CALCTRL_UPSEL a);
    
        protected IEnumRegisterField<CALCTRL_DOWNSEL> calctrl_downsel_field;
        partial void Calctrl_Downsel_Write(CALCTRL_DOWNSEL a, CALCTRL_DOWNSEL b);
        partial void Calctrl_Downsel_Read(CALCTRL_DOWNSEL a, CALCTRL_DOWNSEL b);
        partial void Calctrl_Downsel_ValueProvider(CALCTRL_DOWNSEL a);
        partial void Calctrl_Write(uint a, uint b);
        partial void Calctrl_Read(uint a, uint b);
        
        
        // Calcnt - Offset : 0x58
    
        protected IValueRegisterField calcnt_calcnt_field;
        partial void Calcnt_Calcnt_Read(ulong a, ulong b);
        partial void Calcnt_Calcnt_ValueProvider(ulong a);
        partial void Calcnt_Write(uint a, uint b);
        partial void Calcnt_Read(uint a, uint b);
        
        
        // Clken0 - Offset : 0x64
    
        protected IFlagRegisterField clken0_ldma_bit;
        partial void Clken0_Ldma_Write(bool a, bool b);
        partial void Clken0_Ldma_Read(bool a, bool b);
        partial void Clken0_Ldma_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_ldmaxbar_bit;
        partial void Clken0_Ldmaxbar_Write(bool a, bool b);
        partial void Clken0_Ldmaxbar_Read(bool a, bool b);
        partial void Clken0_Ldmaxbar_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_radioaes_bit;
        partial void Clken0_Radioaes_Write(bool a, bool b);
        partial void Clken0_Radioaes_Read(bool a, bool b);
        partial void Clken0_Radioaes_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_gpcrc_bit;
        partial void Clken0_Gpcrc_Write(bool a, bool b);
        partial void Clken0_Gpcrc_Read(bool a, bool b);
        partial void Clken0_Gpcrc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_timer0_bit;
        partial void Clken0_Timer0_Write(bool a, bool b);
        partial void Clken0_Timer0_Read(bool a, bool b);
        partial void Clken0_Timer0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_timer1_bit;
        partial void Clken0_Timer1_Write(bool a, bool b);
        partial void Clken0_Timer1_Read(bool a, bool b);
        partial void Clken0_Timer1_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_timer2_bit;
        partial void Clken0_Timer2_Write(bool a, bool b);
        partial void Clken0_Timer2_Read(bool a, bool b);
        partial void Clken0_Timer2_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_timer3_bit;
        partial void Clken0_Timer3_Write(bool a, bool b);
        partial void Clken0_Timer3_Read(bool a, bool b);
        partial void Clken0_Timer3_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_usart0_bit;
        partial void Clken0_Usart0_Write(bool a, bool b);
        partial void Clken0_Usart0_Read(bool a, bool b);
        partial void Clken0_Usart0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_usart1_bit;
        partial void Clken0_Usart1_Write(bool a, bool b);
        partial void Clken0_Usart1_Read(bool a, bool b);
        partial void Clken0_Usart1_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_iadc0_bit;
        partial void Clken0_Iadc0_Write(bool a, bool b);
        partial void Clken0_Iadc0_Read(bool a, bool b);
        partial void Clken0_Iadc0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_amuxcp0_bit;
        partial void Clken0_Amuxcp0_Write(bool a, bool b);
        partial void Clken0_Amuxcp0_Read(bool a, bool b);
        partial void Clken0_Amuxcp0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_letimer0_bit;
        partial void Clken0_Letimer0_Write(bool a, bool b);
        partial void Clken0_Letimer0_Read(bool a, bool b);
        partial void Clken0_Letimer0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_wdog0_bit;
        partial void Clken0_Wdog0_Write(bool a, bool b);
        partial void Clken0_Wdog0_Read(bool a, bool b);
        partial void Clken0_Wdog0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_i2c0_bit;
        partial void Clken0_I2c0_Write(bool a, bool b);
        partial void Clken0_I2c0_Read(bool a, bool b);
        partial void Clken0_I2c0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_i2c1_bit;
        partial void Clken0_I2c1_Write(bool a, bool b);
        partial void Clken0_I2c1_Read(bool a, bool b);
        partial void Clken0_I2c1_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_syscfg_bit;
        partial void Clken0_Syscfg_Write(bool a, bool b);
        partial void Clken0_Syscfg_Read(bool a, bool b);
        partial void Clken0_Syscfg_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_dpll0_bit;
        partial void Clken0_Dpll0_Write(bool a, bool b);
        partial void Clken0_Dpll0_Read(bool a, bool b);
        partial void Clken0_Dpll0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_hfrco0_bit;
        partial void Clken0_Hfrco0_Write(bool a, bool b);
        partial void Clken0_Hfrco0_Read(bool a, bool b);
        partial void Clken0_Hfrco0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_hfxo0_bit;
        partial void Clken0_Hfxo0_Write(bool a, bool b);
        partial void Clken0_Hfxo0_Read(bool a, bool b);
        partial void Clken0_Hfxo0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_fsrco_bit;
        partial void Clken0_Fsrco_Write(bool a, bool b);
        partial void Clken0_Fsrco_Read(bool a, bool b);
        partial void Clken0_Fsrco_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_lfrco_bit;
        partial void Clken0_Lfrco_Write(bool a, bool b);
        partial void Clken0_Lfrco_Read(bool a, bool b);
        partial void Clken0_Lfrco_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_lfxo_bit;
        partial void Clken0_Lfxo_Write(bool a, bool b);
        partial void Clken0_Lfxo_Read(bool a, bool b);
        partial void Clken0_Lfxo_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_ulfrco_bit;
        partial void Clken0_Ulfrco_Write(bool a, bool b);
        partial void Clken0_Ulfrco_Read(bool a, bool b);
        partial void Clken0_Ulfrco_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_euart0_bit;
        partial void Clken0_Euart0_Write(bool a, bool b);
        partial void Clken0_Euart0_Read(bool a, bool b);
        partial void Clken0_Euart0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_pdm_bit;
        partial void Clken0_Pdm_Write(bool a, bool b);
        partial void Clken0_Pdm_Read(bool a, bool b);
        partial void Clken0_Pdm_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_gpio_bit;
        partial void Clken0_Gpio_Write(bool a, bool b);
        partial void Clken0_Gpio_Read(bool a, bool b);
        partial void Clken0_Gpio_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_prs_bit;
        partial void Clken0_Prs_Write(bool a, bool b);
        partial void Clken0_Prs_Read(bool a, bool b);
        partial void Clken0_Prs_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_buram_bit;
        partial void Clken0_Buram_Write(bool a, bool b);
        partial void Clken0_Buram_Read(bool a, bool b);
        partial void Clken0_Buram_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_burtc_bit;
        partial void Clken0_Burtc_Write(bool a, bool b);
        partial void Clken0_Burtc_Read(bool a, bool b);
        partial void Clken0_Burtc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_rtcc_bit;
        partial void Clken0_Rtcc_Write(bool a, bool b);
        partial void Clken0_Rtcc_Read(bool a, bool b);
        partial void Clken0_Rtcc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken0_dcdc_bit;
        partial void Clken0_Dcdc_Write(bool a, bool b);
        partial void Clken0_Dcdc_Read(bool a, bool b);
        partial void Clken0_Dcdc_ValueProvider(bool a);
        partial void Clken0_Write(uint a, uint b);
        partial void Clken0_Read(uint a, uint b);
        
        
        // Clken1 - Offset : 0x68
    
        protected IFlagRegisterField clken1_agc_bit;
        partial void Clken1_Agc_Write(bool a, bool b);
        partial void Clken1_Agc_Read(bool a, bool b);
        partial void Clken1_Agc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_modem_bit;
        partial void Clken1_Modem_Write(bool a, bool b);
        partial void Clken1_Modem_Read(bool a, bool b);
        partial void Clken1_Modem_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_rfcrc_bit;
        partial void Clken1_Rfcrc_Write(bool a, bool b);
        partial void Clken1_Rfcrc_Read(bool a, bool b);
        partial void Clken1_Rfcrc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_frc_bit;
        partial void Clken1_Frc_Write(bool a, bool b);
        partial void Clken1_Frc_Read(bool a, bool b);
        partial void Clken1_Frc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_protimer_bit;
        partial void Clken1_Protimer_Write(bool a, bool b);
        partial void Clken1_Protimer_Read(bool a, bool b);
        partial void Clken1_Protimer_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_rac_bit;
        partial void Clken1_Rac_Write(bool a, bool b);
        partial void Clken1_Rac_Read(bool a, bool b);
        partial void Clken1_Rac_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_synth_bit;
        partial void Clken1_Synth_Write(bool a, bool b);
        partial void Clken1_Synth_Read(bool a, bool b);
        partial void Clken1_Synth_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_rdscratchpad_bit;
        partial void Clken1_Rdscratchpad_Write(bool a, bool b);
        partial void Clken1_Rdscratchpad_Read(bool a, bool b);
        partial void Clken1_Rdscratchpad_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_rdmailbox0_bit;
        partial void Clken1_Rdmailbox0_Write(bool a, bool b);
        partial void Clken1_Rdmailbox0_Read(bool a, bool b);
        partial void Clken1_Rdmailbox0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_rdmailbox1_bit;
        partial void Clken1_Rdmailbox1_Write(bool a, bool b);
        partial void Clken1_Rdmailbox1_Read(bool a, bool b);
        partial void Clken1_Rdmailbox1_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_prortc_bit;
        partial void Clken1_Prortc_Write(bool a, bool b);
        partial void Clken1_Prortc_Read(bool a, bool b);
        partial void Clken1_Prortc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_bufc_bit;
        partial void Clken1_Bufc_Write(bool a, bool b);
        partial void Clken1_Bufc_Read(bool a, bool b);
        partial void Clken1_Bufc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_ifadcdebug_bit;
        partial void Clken1_Ifadcdebug_Write(bool a, bool b);
        partial void Clken1_Ifadcdebug_Read(bool a, bool b);
        partial void Clken1_Ifadcdebug_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_cryptoacc_bit;
        partial void Clken1_Cryptoacc_Write(bool a, bool b);
        partial void Clken1_Cryptoacc_Read(bool a, bool b);
        partial void Clken1_Cryptoacc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_rfsense_bit;
        partial void Clken1_Rfsense_Write(bool a, bool b);
        partial void Clken1_Rfsense_Read(bool a, bool b);
        partial void Clken1_Rfsense_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_smu_bit;
        partial void Clken1_Smu_Write(bool a, bool b);
        partial void Clken1_Smu_Read(bool a, bool b);
        partial void Clken1_Smu_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_icache0_bit;
        partial void Clken1_Icache0_Write(bool a, bool b);
        partial void Clken1_Icache0_Read(bool a, bool b);
        partial void Clken1_Icache0_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_msc_bit;
        partial void Clken1_Msc_Write(bool a, bool b);
        partial void Clken1_Msc_Read(bool a, bool b);
        partial void Clken1_Msc_ValueProvider(bool a);
    
        protected IFlagRegisterField clken1_timer4_bit;
        partial void Clken1_Timer4_Write(bool a, bool b);
        partial void Clken1_Timer4_Read(bool a, bool b);
        partial void Clken1_Timer4_ValueProvider(bool a);
        partial void Clken1_Write(uint a, uint b);
        partial void Clken1_Read(uint a, uint b);
        
        
        // Sysclkctrl - Offset : 0x70
    
        protected IEnumRegisterField<SYSCLKCTRL_CLKSEL> sysclkctrl_clksel_field;
        partial void Sysclkctrl_Clksel_Write(SYSCLKCTRL_CLKSEL a, SYSCLKCTRL_CLKSEL b);
        partial void Sysclkctrl_Clksel_Read(SYSCLKCTRL_CLKSEL a, SYSCLKCTRL_CLKSEL b);
        partial void Sysclkctrl_Clksel_ValueProvider(SYSCLKCTRL_CLKSEL a);
    
        protected IEnumRegisterField<SYSCLKCTRL_LSPCLKPRESC> sysclkctrl_lspclkpresc_bit;
        partial void Sysclkctrl_Lspclkpresc_Write(SYSCLKCTRL_LSPCLKPRESC a, SYSCLKCTRL_LSPCLKPRESC b);
        partial void Sysclkctrl_Lspclkpresc_Read(SYSCLKCTRL_LSPCLKPRESC a, SYSCLKCTRL_LSPCLKPRESC b);
        partial void Sysclkctrl_Lspclkpresc_ValueProvider(SYSCLKCTRL_LSPCLKPRESC a);
    
        protected IEnumRegisterField<SYSCLKCTRL_PCLKPRESC> sysclkctrl_pclkpresc_bit;
        partial void Sysclkctrl_Pclkpresc_Write(SYSCLKCTRL_PCLKPRESC a, SYSCLKCTRL_PCLKPRESC b);
        partial void Sysclkctrl_Pclkpresc_Read(SYSCLKCTRL_PCLKPRESC a, SYSCLKCTRL_PCLKPRESC b);
        partial void Sysclkctrl_Pclkpresc_ValueProvider(SYSCLKCTRL_PCLKPRESC a);
    
        protected IEnumRegisterField<SYSCLKCTRL_HCLKPRESC> sysclkctrl_hclkpresc_field;
        partial void Sysclkctrl_Hclkpresc_Write(SYSCLKCTRL_HCLKPRESC a, SYSCLKCTRL_HCLKPRESC b);
        partial void Sysclkctrl_Hclkpresc_Read(SYSCLKCTRL_HCLKPRESC a, SYSCLKCTRL_HCLKPRESC b);
        partial void Sysclkctrl_Hclkpresc_ValueProvider(SYSCLKCTRL_HCLKPRESC a);
    
        protected IEnumRegisterField<SYSCLKCTRL_RHCLKPRESC> sysclkctrl_rhclkpresc_bit;
        partial void Sysclkctrl_Rhclkpresc_Write(SYSCLKCTRL_RHCLKPRESC a, SYSCLKCTRL_RHCLKPRESC b);
        partial void Sysclkctrl_Rhclkpresc_Read(SYSCLKCTRL_RHCLKPRESC a, SYSCLKCTRL_RHCLKPRESC b);
        partial void Sysclkctrl_Rhclkpresc_ValueProvider(SYSCLKCTRL_RHCLKPRESC a);
        partial void Sysclkctrl_Write(uint a, uint b);
        partial void Sysclkctrl_Read(uint a, uint b);
        
        
        // Traceclkctrl - Offset : 0x80
    
        protected IEnumRegisterField<TRACECLKCTRL_PRESC> traceclkctrl_presc_field;
        partial void Traceclkctrl_Presc_Write(TRACECLKCTRL_PRESC a, TRACECLKCTRL_PRESC b);
        partial void Traceclkctrl_Presc_Read(TRACECLKCTRL_PRESC a, TRACECLKCTRL_PRESC b);
        partial void Traceclkctrl_Presc_ValueProvider(TRACECLKCTRL_PRESC a);
        partial void Traceclkctrl_Write(uint a, uint b);
        partial void Traceclkctrl_Read(uint a, uint b);
        
        
        // Exportclkctrl - Offset : 0x90
    
        protected IEnumRegisterField<EXPORTCLKCTRL_CLKOUTSEL0> exportclkctrl_clkoutsel0_field;
        partial void Exportclkctrl_Clkoutsel0_Write(EXPORTCLKCTRL_CLKOUTSEL0 a, EXPORTCLKCTRL_CLKOUTSEL0 b);
        partial void Exportclkctrl_Clkoutsel0_Read(EXPORTCLKCTRL_CLKOUTSEL0 a, EXPORTCLKCTRL_CLKOUTSEL0 b);
        partial void Exportclkctrl_Clkoutsel0_ValueProvider(EXPORTCLKCTRL_CLKOUTSEL0 a);
    
        protected IEnumRegisterField<EXPORTCLKCTRL_CLKOUTSEL1> exportclkctrl_clkoutsel1_field;
        partial void Exportclkctrl_Clkoutsel1_Write(EXPORTCLKCTRL_CLKOUTSEL1 a, EXPORTCLKCTRL_CLKOUTSEL1 b);
        partial void Exportclkctrl_Clkoutsel1_Read(EXPORTCLKCTRL_CLKOUTSEL1 a, EXPORTCLKCTRL_CLKOUTSEL1 b);
        partial void Exportclkctrl_Clkoutsel1_ValueProvider(EXPORTCLKCTRL_CLKOUTSEL1 a);
    
        protected IEnumRegisterField<EXPORTCLKCTRL_CLKOUTSEL2> exportclkctrl_clkoutsel2_field;
        partial void Exportclkctrl_Clkoutsel2_Write(EXPORTCLKCTRL_CLKOUTSEL2 a, EXPORTCLKCTRL_CLKOUTSEL2 b);
        partial void Exportclkctrl_Clkoutsel2_Read(EXPORTCLKCTRL_CLKOUTSEL2 a, EXPORTCLKCTRL_CLKOUTSEL2 b);
        partial void Exportclkctrl_Clkoutsel2_ValueProvider(EXPORTCLKCTRL_CLKOUTSEL2 a);
    
        protected IValueRegisterField exportclkctrl_presc_field;
        partial void Exportclkctrl_Presc_Write(ulong a, ulong b);
        partial void Exportclkctrl_Presc_Read(ulong a, ulong b);
        partial void Exportclkctrl_Presc_ValueProvider(ulong a);
        partial void Exportclkctrl_Write(uint a, uint b);
        partial void Exportclkctrl_Read(uint a, uint b);
        
        
        // Dpllrefclkctrl - Offset : 0x100
    
        protected IEnumRegisterField<DPLLREFCLKCTRL_CLKSEL> dpllrefclkctrl_clksel_field;
        partial void Dpllrefclkctrl_Clksel_Write(DPLLREFCLKCTRL_CLKSEL a, DPLLREFCLKCTRL_CLKSEL b);
        partial void Dpllrefclkctrl_Clksel_Read(DPLLREFCLKCTRL_CLKSEL a, DPLLREFCLKCTRL_CLKSEL b);
        partial void Dpllrefclkctrl_Clksel_ValueProvider(DPLLREFCLKCTRL_CLKSEL a);
        partial void Dpllrefclkctrl_Write(uint a, uint b);
        partial void Dpllrefclkctrl_Read(uint a, uint b);
        
        
        // Em01grpaclkctrl - Offset : 0x120
    
        protected IEnumRegisterField<EM01GRPACLKCTRL_CLKSEL> em01grpaclkctrl_clksel_field;
        partial void Em01grpaclkctrl_Clksel_Write(EM01GRPACLKCTRL_CLKSEL a, EM01GRPACLKCTRL_CLKSEL b);
        partial void Em01grpaclkctrl_Clksel_Read(EM01GRPACLKCTRL_CLKSEL a, EM01GRPACLKCTRL_CLKSEL b);
        partial void Em01grpaclkctrl_Clksel_ValueProvider(EM01GRPACLKCTRL_CLKSEL a);
        partial void Em01grpaclkctrl_Write(uint a, uint b);
        partial void Em01grpaclkctrl_Read(uint a, uint b);
        
        
        // Em01grpbclkctrl - Offset : 0x124
    
        protected IEnumRegisterField<EM01GRPBCLKCTRL_CLKSEL> em01grpbclkctrl_clksel_field;
        partial void Em01grpbclkctrl_Clksel_Write(EM01GRPBCLKCTRL_CLKSEL a, EM01GRPBCLKCTRL_CLKSEL b);
        partial void Em01grpbclkctrl_Clksel_Read(EM01GRPBCLKCTRL_CLKSEL a, EM01GRPBCLKCTRL_CLKSEL b);
        partial void Em01grpbclkctrl_Clksel_ValueProvider(EM01GRPBCLKCTRL_CLKSEL a);
        partial void Em01grpbclkctrl_Write(uint a, uint b);
        partial void Em01grpbclkctrl_Read(uint a, uint b);
        
        
        // Em23grpaclkctrl - Offset : 0x140
    
        protected IEnumRegisterField<EM23GRPACLKCTRL_CLKSEL> em23grpaclkctrl_clksel_field;
        partial void Em23grpaclkctrl_Clksel_Write(EM23GRPACLKCTRL_CLKSEL a, EM23GRPACLKCTRL_CLKSEL b);
        partial void Em23grpaclkctrl_Clksel_Read(EM23GRPACLKCTRL_CLKSEL a, EM23GRPACLKCTRL_CLKSEL b);
        partial void Em23grpaclkctrl_Clksel_ValueProvider(EM23GRPACLKCTRL_CLKSEL a);
        partial void Em23grpaclkctrl_Write(uint a, uint b);
        partial void Em23grpaclkctrl_Read(uint a, uint b);
        
        
        // Em4grpaclkctrl - Offset : 0x160
    
        protected IEnumRegisterField<EM4GRPACLKCTRL_CLKSEL> em4grpaclkctrl_clksel_field;
        partial void Em4grpaclkctrl_Clksel_Write(EM4GRPACLKCTRL_CLKSEL a, EM4GRPACLKCTRL_CLKSEL b);
        partial void Em4grpaclkctrl_Clksel_Read(EM4GRPACLKCTRL_CLKSEL a, EM4GRPACLKCTRL_CLKSEL b);
        partial void Em4grpaclkctrl_Clksel_ValueProvider(EM4GRPACLKCTRL_CLKSEL a);
        partial void Em4grpaclkctrl_Write(uint a, uint b);
        partial void Em4grpaclkctrl_Read(uint a, uint b);
        
        
        // Iadcclkctrl - Offset : 0x180
    
        protected IEnumRegisterField<IADCCLKCTRL_CLKSEL> iadcclkctrl_clksel_field;
        partial void Iadcclkctrl_Clksel_Write(IADCCLKCTRL_CLKSEL a, IADCCLKCTRL_CLKSEL b);
        partial void Iadcclkctrl_Clksel_Read(IADCCLKCTRL_CLKSEL a, IADCCLKCTRL_CLKSEL b);
        partial void Iadcclkctrl_Clksel_ValueProvider(IADCCLKCTRL_CLKSEL a);
        partial void Iadcclkctrl_Write(uint a, uint b);
        partial void Iadcclkctrl_Read(uint a, uint b);
        
        
        // Wdog0clkctrl - Offset : 0x200
    
        protected IEnumRegisterField<WDOG0CLKCTRL_CLKSEL> wdog0clkctrl_clksel_field;
        partial void Wdog0clkctrl_Clksel_Write(WDOG0CLKCTRL_CLKSEL a, WDOG0CLKCTRL_CLKSEL b);
        partial void Wdog0clkctrl_Clksel_Read(WDOG0CLKCTRL_CLKSEL a, WDOG0CLKCTRL_CLKSEL b);
        partial void Wdog0clkctrl_Clksel_ValueProvider(WDOG0CLKCTRL_CLKSEL a);
        partial void Wdog0clkctrl_Write(uint a, uint b);
        partial void Wdog0clkctrl_Read(uint a, uint b);
        
        
        // Euart0clkctrl - Offset : 0x220
    
        protected IEnumRegisterField<EUART0CLKCTRL_CLKSEL> euart0clkctrl_clksel_field;
        partial void Euart0clkctrl_Clksel_Write(EUART0CLKCTRL_CLKSEL a, EUART0CLKCTRL_CLKSEL b);
        partial void Euart0clkctrl_Clksel_Read(EUART0CLKCTRL_CLKSEL a, EUART0CLKCTRL_CLKSEL b);
        partial void Euart0clkctrl_Clksel_ValueProvider(EUART0CLKCTRL_CLKSEL a);
        partial void Euart0clkctrl_Write(uint a, uint b);
        partial void Euart0clkctrl_Read(uint a, uint b);
        
        
        // Synthclkctrl - Offset : 0x230
    
        protected IEnumRegisterField<SYNTHCLKCTRL_CLKSEL> synthclkctrl_clksel_field;
        partial void Synthclkctrl_Clksel_Write(SYNTHCLKCTRL_CLKSEL a, SYNTHCLKCTRL_CLKSEL b);
        partial void Synthclkctrl_Clksel_Read(SYNTHCLKCTRL_CLKSEL a, SYNTHCLKCTRL_CLKSEL b);
        partial void Synthclkctrl_Clksel_ValueProvider(SYNTHCLKCTRL_CLKSEL a);
        partial void Synthclkctrl_Write(uint a, uint b);
        partial void Synthclkctrl_Read(uint a, uint b);
        
        
        // Rtccclkctrl - Offset : 0x240
    
        protected IEnumRegisterField<RTCCCLKCTRL_CLKSEL> rtccclkctrl_clksel_field;
        partial void Rtccclkctrl_Clksel_Write(RTCCCLKCTRL_CLKSEL a, RTCCCLKCTRL_CLKSEL b);
        partial void Rtccclkctrl_Clksel_Read(RTCCCLKCTRL_CLKSEL a, RTCCCLKCTRL_CLKSEL b);
        partial void Rtccclkctrl_Clksel_ValueProvider(RTCCCLKCTRL_CLKSEL a);
        partial void Rtccclkctrl_Write(uint a, uint b);
        partial void Rtccclkctrl_Read(uint a, uint b);
        
        
        // Prortcclkctrl - Offset : 0x248
    
        protected IEnumRegisterField<PRORTCCLKCTRL_CLKSEL> prortcclkctrl_clksel_field;
        partial void Prortcclkctrl_Clksel_Write(PRORTCCLKCTRL_CLKSEL a, PRORTCCLKCTRL_CLKSEL b);
        partial void Prortcclkctrl_Clksel_Read(PRORTCCLKCTRL_CLKSEL a, PRORTCCLKCTRL_CLKSEL b);
        partial void Prortcclkctrl_Clksel_ValueProvider(PRORTCCLKCTRL_CLKSEL a);
        partial void Prortcclkctrl_Write(uint a, uint b);
        partial void Prortcclkctrl_Read(uint a, uint b);
        
        
        // Cryptoaccclkctrl - Offset : 0x260
    
        protected IFlagRegisterField cryptoaccclkctrl_pken_bit;
        partial void Cryptoaccclkctrl_Pken_Write(bool a, bool b);
        partial void Cryptoaccclkctrl_Pken_Read(bool a, bool b);
        partial void Cryptoaccclkctrl_Pken_ValueProvider(bool a);
    
        protected IFlagRegisterField cryptoaccclkctrl_aesen_bit;
        partial void Cryptoaccclkctrl_Aesen_Write(bool a, bool b);
        partial void Cryptoaccclkctrl_Aesen_Read(bool a, bool b);
        partial void Cryptoaccclkctrl_Aesen_ValueProvider(bool a);
        partial void Cryptoaccclkctrl_Write(uint a, uint b);
        partial void Cryptoaccclkctrl_Read(uint a, uint b);
        
        
        // Radioclkctrl - Offset : 0x280
    
        protected IFlagRegisterField radioclkctrl_en_bit;
        partial void Radioclkctrl_En_Write(bool a, bool b);
        partial void Radioclkctrl_En_Read(bool a, bool b);
        partial void Radioclkctrl_En_ValueProvider(bool a);
    
        protected IFlagRegisterField radioclkctrl_dbgclk_bit;
        partial void Radioclkctrl_Dbgclk_Write(bool a, bool b);
        partial void Radioclkctrl_Dbgclk_Read(bool a, bool b);
        partial void Radioclkctrl_Dbgclk_ValueProvider(bool a);
        partial void Radioclkctrl_Write(uint a, uint b);
        partial void Radioclkctrl_Read(uint a, uint b);
        
        partial void CMU_Reset();

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
            Status = 0x8,
            Lock = 0x10,
            Wdoglock = 0x14,
            If = 0x20,
            Ien = 0x24,
            Test = 0x30,
            Testhv = 0x34,
            Testclken = 0x40,
            Calcmd = 0x50,
            Calctrl = 0x54,
            Calcnt = 0x58,
            Clken0 = 0x64,
            Clken1 = 0x68,
            Sysclkctrl = 0x70,
            Traceclkctrl = 0x80,
            Exportclkctrl = 0x90,
            Dpllrefclkctrl = 0x100,
            Em01grpaclkctrl = 0x120,
            Em01grpbclkctrl = 0x124,
            Em23grpaclkctrl = 0x140,
            Em4grpaclkctrl = 0x160,
            Iadcclkctrl = 0x180,
            Wdog0clkctrl = 0x200,
            Euart0clkctrl = 0x220,
            Synthclkctrl = 0x230,
            Rtccclkctrl = 0x240,
            Prortcclkctrl = 0x248,
            Cryptoaccclkctrl = 0x260,
            Radioclkctrl = 0x280,
            
            Ipversion_SET = 0x1000,
            Ctrl_SET = 0x1004,
            Status_SET = 0x1008,
            Lock_SET = 0x1010,
            Wdoglock_SET = 0x1014,
            If_SET = 0x1020,
            Ien_SET = 0x1024,
            Test_SET = 0x1030,
            Testhv_SET = 0x1034,
            Testclken_SET = 0x1040,
            Calcmd_SET = 0x1050,
            Calctrl_SET = 0x1054,
            Calcnt_SET = 0x1058,
            Clken0_SET = 0x1064,
            Clken1_SET = 0x1068,
            Sysclkctrl_SET = 0x1070,
            Traceclkctrl_SET = 0x1080,
            Exportclkctrl_SET = 0x1090,
            Dpllrefclkctrl_SET = 0x1100,
            Em01grpaclkctrl_SET = 0x1120,
            Em01grpbclkctrl_SET = 0x1124,
            Em23grpaclkctrl_SET = 0x1140,
            Em4grpaclkctrl_SET = 0x1160,
            Iadcclkctrl_SET = 0x1180,
            Wdog0clkctrl_SET = 0x1200,
            Euart0clkctrl_SET = 0x1220,
            Synthclkctrl_SET = 0x1230,
            Rtccclkctrl_SET = 0x1240,
            Prortcclkctrl_SET = 0x1248,
            Cryptoaccclkctrl_SET = 0x1260,
            Radioclkctrl_SET = 0x1280,
            
            Ipversion_CLR = 0x2000,
            Ctrl_CLR = 0x2004,
            Status_CLR = 0x2008,
            Lock_CLR = 0x2010,
            Wdoglock_CLR = 0x2014,
            If_CLR = 0x2020,
            Ien_CLR = 0x2024,
            Test_CLR = 0x2030,
            Testhv_CLR = 0x2034,
            Testclken_CLR = 0x2040,
            Calcmd_CLR = 0x2050,
            Calctrl_CLR = 0x2054,
            Calcnt_CLR = 0x2058,
            Clken0_CLR = 0x2064,
            Clken1_CLR = 0x2068,
            Sysclkctrl_CLR = 0x2070,
            Traceclkctrl_CLR = 0x2080,
            Exportclkctrl_CLR = 0x2090,
            Dpllrefclkctrl_CLR = 0x2100,
            Em01grpaclkctrl_CLR = 0x2120,
            Em01grpbclkctrl_CLR = 0x2124,
            Em23grpaclkctrl_CLR = 0x2140,
            Em4grpaclkctrl_CLR = 0x2160,
            Iadcclkctrl_CLR = 0x2180,
            Wdog0clkctrl_CLR = 0x2200,
            Euart0clkctrl_CLR = 0x2220,
            Synthclkctrl_CLR = 0x2230,
            Rtccclkctrl_CLR = 0x2240,
            Prortcclkctrl_CLR = 0x2248,
            Cryptoaccclkctrl_CLR = 0x2260,
            Radioclkctrl_CLR = 0x2280,
            
            Ipversion_TGL = 0x3000,
            Ctrl_TGL = 0x3004,
            Status_TGL = 0x3008,
            Lock_TGL = 0x3010,
            Wdoglock_TGL = 0x3014,
            If_TGL = 0x3020,
            Ien_TGL = 0x3024,
            Test_TGL = 0x3030,
            Testhv_TGL = 0x3034,
            Testclken_TGL = 0x3040,
            Calcmd_TGL = 0x3050,
            Calctrl_TGL = 0x3054,
            Calcnt_TGL = 0x3058,
            Clken0_TGL = 0x3064,
            Clken1_TGL = 0x3068,
            Sysclkctrl_TGL = 0x3070,
            Traceclkctrl_TGL = 0x3080,
            Exportclkctrl_TGL = 0x3090,
            Dpllrefclkctrl_TGL = 0x3100,
            Em01grpaclkctrl_TGL = 0x3120,
            Em01grpbclkctrl_TGL = 0x3124,
            Em23grpaclkctrl_TGL = 0x3140,
            Em4grpaclkctrl_TGL = 0x3160,
            Iadcclkctrl_TGL = 0x3180,
            Wdog0clkctrl_TGL = 0x3200,
            Euart0clkctrl_TGL = 0x3220,
            Synthclkctrl_TGL = 0x3230,
            Rtccclkctrl_TGL = 0x3240,
            Prortcclkctrl_TGL = 0x3248,
            Cryptoaccclkctrl_TGL = 0x3260,
            Radioclkctrl_TGL = 0x3280,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}