//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    CMU, Generated on : 2023-07-20 14:29:31.708431
    CMU, ID Version : 52bfa38ff76643fb8fa46c63ecf729ea.3 */

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
    public partial class EFR32xG2_CMU_3
    {
        public EFR32xG2_CMU_3(Machine machine) : base(machine)
        {
            EFR32xG2_CMU_3_constructor();
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
    public partial class EFR32xG2_CMU_3 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_CMU_3(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_CMU_3_Constructor();
        }

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
                {(long)Registers.Seclkctrl, GenerateSeclkctrlRegister()},
                {(long)Registers.Traceclkctrl, GenerateTraceclkctrlRegister()},
                {(long)Registers.Exportclkctrl, GenerateExportclkctrlRegister()},
                {(long)Registers.Dpllrefclkctrl, GenerateDpllrefclkctrlRegister()},
                {(long)Registers.Em01grpaclkctrl, GenerateEm01grpaclkctrlRegister()},
                {(long)Registers.Em01grpcclkctrl, GenerateEm01grpcclkctrlRegister()},
                {(long)Registers.Em23grpaclkctrl, GenerateEm23grpaclkctrlRegister()},
                {(long)Registers.Em4grpaclkctrl, GenerateEm4grpaclkctrlRegister()},
                {(long)Registers.Iadcclkctrl, GenerateIadcclkctrlRegister()},
                {(long)Registers.Wdog0clkctrl, GenerateWdog0clkctrlRegister()},
                {(long)Registers.Wdog1clkctrl, GenerateWdog1clkctrlRegister()},
                {(long)Registers.Eusart0clkctrl, GenerateEusart0clkctrlRegister()},
                {(long)Registers.Synthclkctrl, GenerateSynthclkctrlRegister()},
                {(long)Registers.Sysrtc0clkctrl, GenerateSysrtc0clkctrlRegister()},
                {(long)Registers.Lcdclkctrl, GenerateLcdclkctrlRegister()},
                {(long)Registers.Vdac0clkctrl, GenerateVdac0clkctrlRegister()},
                {(long)Registers.Pcnt0clkctrl, GeneratePcnt0clkctrlRegister()},
                {(long)Registers.Radioclkctrl, GenerateRadioclkctrlRegister()},
                {(long)Registers.Dapclkctrl, GenerateDapclkctrlRegister()},
                {(long)Registers.Lesensehfclkctrl, GenerateLesensehfclkctrlRegister()},
                {(long)Registers.Vdac1clkctrl, GenerateVdac1clkctrlRegister()},
                {(long)Registers.Rpuratd0_Drpu, GenerateRpuratd0_drpuRegister()},
                {(long)Registers.Rpuratd1_Drpu, GenerateRpuratd1_drpuRegister()},
                {(long)Registers.Rpuratd2_Drpu, GenerateRpuratd2_drpuRegister()},
                {(long)Registers.Rpuratd3_Drpu, GenerateRpuratd3_drpuRegister()},
                {(long)Registers.Rpuratd4_Drpu, GenerateRpuratd4_drpuRegister()},
                {(long)Registers.Rpuratd5_Drpu, GenerateRpuratd5_drpuRegister()},
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
            HFRCOSE = 1, // HFRCOSE output is clocking CLKOUTHIDDEN
            ULFRCODUTY = 2, // ULFRCO duty output is clocking CLKOUTHIDDEN
            ULFRCOLV = 3, // ULFRCO LV output is clocking CLKOUTHIDDEN
            ULFRCOHV = 4, // ULFRCO HV output is clocking CLKOUTHIDDEN
            LFRCOLV = 5, // LFRCO LV output is clocking CLKOUTHIDDEN
            LFRCOHV = 6, // LFRCO HV output is clocking CLKOUTHIDDEN
            LFXOLV = 7, // LFXO LV output is clocking CLKOUTHIDDEN
            LFXOHV = 8, // LFXO HV output is clocking CLKOUTHIDDEN
            FSRCOLV = 9, // FSRCO LV output is clocking CLKOUTHIDDEN
            FSRCOLVBC = 10, // FSRCO LV bias controller output is clocking CLKOUTHIDDEN
            FSRCOHV = 11, // FSRCO HV output is clocking CLKOUTHIDDEN
            PFMOSC = 12, // PFMOSC output is clocking CLKOUTHIDDEN
            HFRCOEM23 = 13, // HFRCOEM23 output is clocking CLKOUTHIDDEN
            HFRCODPLLQ = 14, // HFRCODPLL qual output (retime input) is clocking CLKOUTHIDDEN
            HFRCODPLL = 15, // HFRCODPLL raw output is clocking CLKOUTHIDDEN
            HFXOQ = 16, // HFXO qual output (retime input) is clocking CLKOUTHIDDEN
            HFXO = 17, // HFXO raw output is clocking CLKOUTHIDDEN
            BIASOSC = 18, // BIASOSC is clocking CLKOUTHIDDEN
            RFIFADC = 19, // RF IFADC clock is clocking CLKOUTHIDDEN
            RFAUXADC = 20, // RF AUXADC clock is clocking CLKOUTHIDDEN
            RFMMD = 21, // RF MMD clock is clocking CLKOUTHIDDEN
            PCLK = 22, // PCLK is clocking CLKOUTHIDDEN
            LSPCLK = 23, // LSPCLK is clocking CLKOUTHIDDEN
            RHCLK = 24, // RHCLK is clocking CLKOUTHIDDEN
            TEMPOSC = 25, // TEMPOSC output is clocking CLKOUTHIDDEN
        }
        
        protected enum CALCTRL_UPSEL
        {
            DISABLED = 0, // Up-counter is not clocked
            PRS = 1, // PRS CMU_CALUP consumer is clocking up-counter
            HFXO = 2, // HFXO is clocking up-counter
            LFXO = 3, // LFXO is clocking up-counter
            HFRCODPLL = 4, // HFRCODPLL is clocking up-counter
            HFRCOEM23 = 5, // HFRCOEM23 is clocking up-counter
            PFMOSC = 6, // PFMOSC is clocking up-counter
            BIASOSC = 7, // BIASOSC is clocking up-counter
            FSRCO = 8, // FSRCO is clocking up-counter
            LFRCO = 9, // LFRCO is clocking up-counter
            ULFRCO = 10, // ULFRCO is clocking up-counter
            HFRCOSE = 11, // HFRCOSE is clocking up-counter
            TEMPOSC = 12, // TEMPOSC is clocking up-counter
        }
        
        protected enum CALCTRL_DOWNSEL
        {
            DISABLED = 0, // Down-counter is not clocked
            HCLK = 1, // HCLK is clocking down-counter
            PRS = 2, // PRS CMU_CALDN consumer is clocking down-counter
            HFXO = 3, // HFXO is clocking down-counter
            LFXO = 4, // LFXO is clocking down-counter
            HFRCODPLL = 5, // HFRCODPLL is clocking down-counter
            HFRCOEM23 = 6, // HFRCOEM23 is clocking down-counter
            PFMOSC = 7, // PFMOSC is clocking down-counter
            BIASOSC = 8, // BIASOSC is clocking down-counter
            FSRCO = 9, // FSRCO is clocking down-counter
            LFRCO = 10, // LFRCO is clocking down-counter
            ULFRCO = 11, // ULFRCO is clocking down-counter
            HFRCOSE = 12, // HFRCOSE is clocking down-counter
            TEMPOSC = 13, // TEMPOSC is clocking down-counter
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
        
        protected enum SECLKCTRL_CLKSEL
        {
            FSRCO = 1, // FSRCO is clocking SECLK
            HFRCOSE = 2, // HFRCOSE is clocking SECLK
        }
        
        protected enum TRACECLKCTRL_CLKSEL
        {
            DISABLE = 0, // TRACE clock is disable
            SYSCLK = 1, // SYSCLK is driving TRACE
            HFRCOEM23 = 2, // HFRCOEM23 is driving TRACE
            HFRCODPLLRT = 3, // HFRCODPLLRT is driving TRACE
        }
        
        protected enum TRACECLKCTRL_PRESC
        {
            DIV1 = 0, // TRACECLK is SYSCLK divided by 1
            DIV2 = 1, // TRACECLK is SYSCLK divided by 2
            DIV3 = 2, // TRACECLK is SYSCLK divided by 3
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
            HFRCOEM23 = 9, // HFRCOEM23 is clocking CLKOUT0
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
            HFRCOEM23 = 9, // HFRCOEM23 is clocking CLKOUT1
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
            HFRCOEM23 = 9, // HFRCOEM23 is clocking CLKOUT2
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
            HFRCOEM23 = 4, // HFRCOEM23 is clocking EM01GRPACLK
            HFRCODPLLRT = 5, // HFRCODPLL (retimed) is clocking EM01GRPACLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
            HFXORT = 6, // HFXO (retimed) is clocking EM01GRPACLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
        }
        
        protected enum EM01GRPCCLKCTRL_CLKSEL
        {
            DISABLED = 0, // EM01GRPCCLK is not clocked
            HFRCODPLL = 1, // HFRCODPLL is clocking EM01GRPCCLK
            HFXO = 2, // HFXO is clocking EM01GRPCCLK
            FSRCO = 3, // FSRCO is clocking EM01GRPCCLK
            HFRCOEM23 = 4, // HFRCOEM23 is clocking EM01GRPCCLK
            HFRCODPLLRT = 5, // HFRCODPLL (retimed) is clocking EM01GRPCCLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
            HFXORT = 6, // HFXO (retimed) is clocking EM01GRPCCLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
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
            HFRCOEM23 = 3, // HFRCOEM23 is clocking IADCCLK
        }
        
        protected enum WDOG0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // WDOG0CLK is not clocked
            LFRCO = 1, // LFRCO is clocking WDOG0CLK
            LFXO = 2, // LFXO is clocking WDOG0CLK
            ULFRCO = 3, // ULFRCO is clocking WDOG0CLK
            HCLKDIV1024 = 4, // HCLKDIV1024 is clocking WDOG0CLK
        }
        
        protected enum WDOG1CLKCTRL_CLKSEL
        {
            DISABLED = 0, // WDOG0CLK is not clocked
            LFRCO = 1, // LFRCO is clocking WDOG0CLK
            LFXO = 2, // LFXO is clocking WDOG0CLK
            ULFRCO = 3, // ULFRCO is clocking WDOG0CLK
            HCLKDIV1024 = 4, // HCLKDIV1024 is clocking WDOG0CLK
        }
        
        protected enum EUSART0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // EUSART0 is not clocked
            EM01GRPCCLK = 1, // EM01GRPCCLK is clocking EUSART0
            HFRCOEM23 = 2, // HFRCOEM23 is clocking EUSART0
            LFRCO = 3, // LFRCO is clocking EUSART0
            LFXO = 4, // LFXO is clocking EUSART0
        }
        
        protected enum SYNTHCLKCTRL_CLKSEL
        {
            DISABLED = 0, // SYNTHCLK is not clocked
            HFXO = 1, // HFXO is clocking SYNTHCLK
            CLKIN0 = 2, // CLKIN0 is clocking SYNTHCLK
        }
        
        protected enum SYSRTC0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // SYSRTC0CLK is not clocked
            LFRCO = 1, // LFRCO is clocking SYSRTC0CLK
            LFXO = 2, // LFXO is clocking SYSRTC0CLK
            ULFRCO = 3, // ULFRCO is clocking SYSRTC0CLK
        }
        
        protected enum LCDCLKCTRL_CLKSEL
        {
            DISABLED = 0, // LCDCLK is not clocked
            LFRCO = 1, // LFRCO is clocking LCDCLK
            LFXO = 2, // LFXO is clocking LCDCLK
            ULFRCO = 3, // ULFRCO is clocking LCDCLK
        }
        
        protected enum VDAC0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // VDAC is not clocked
            EM01GRPACLK = 1, // EM01GRPACLK is clocking VDAC
            EM23GRPACLK = 2, // EM23GRPACLK is clocking VDAC
            FSRCO = 3, // FSRCO is clocking VDAC
            HFRCOEM23 = 4, // HFRCOEM23 is clocking VDAC
        }
        
        protected enum PCNT0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // PCNT0 is not clocked
            EM23GRPACLK = 1, // EM23GRPACLK is clocking PCNT0
            PCNTS0 = 2, // External pin PCNT_S0 is clocking PCNT0
        }
        
        protected enum DAPCLKCTRL_CLKSEL
        {
            DISABLED = 0, // DAP is not clocked
            FSRCO = 1, // FSRCO is clocking DAP
            HFRCODPLL = 2, // HFRCODPLL is clocking DAP
        }
        
        protected enum LESENSEHFCLKCTRL_CLKSEL
        {
            DISABLED = 0, // LESENSEHFCLK is not clocked
            FSRCO = 1, // FSRCO is clocking LESENSEHFCLK
            HFRCOEM23 = 2, // HFRCOEM23 is clocking LESENSEHFCLK
        }
        
        protected enum VDAC1CLKCTRL_CLKSEL
        {
            DISABLED = 0, // VDAC is not clocked
            EM01GRPACLK = 1, // EM01GRPACLK is clocking VDAC
            EM23GRPACLK = 2, // EM23GRPACLK is clocking VDAC
            FSRCO = 3, // FSRCO is clocking VDAC
            HFRCOEM23 = 4, // HFRCOEM23 is clocking VDAC
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
        
        // Ctrl - Offset : 0x4
        protected DoubleWordRegister  GenerateCtrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 8, out ctrl_runningdebugsel_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Runningdebugsel_ValueProvider(_);
                        return ctrl_runningdebugsel_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Runningdebugsel_Write(_, __),
                    readCallback: (_, __) => Ctrl_Runningdebugsel_Read(_, __),
                    name: "Runningdebugsel")
            .WithReservedBits(8, 22)
            .WithFlag(30, out ctrl_forceem1pclken_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceem1pclken_ValueProvider(_);
                        return ctrl_forceem1pclken_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Forceem1pclken_Write(_, __),
                    readCallback: (_, __) => Ctrl_Forceem1pclken_Read(_, __),
                    name: "Forceem1pclken")
            .WithFlag(31, out ctrl_forceclkin0_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceclkin0_ValueProvider(_);
                        return ctrl_forceclkin0_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Forceclkin0_Write(_, __),
                    readCallback: (_, __) => Ctrl_Forceclkin0_Read(_, __),
                    name: "Forceclkin0")
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write(_, __));
        
        // Status - Offset : 0x8
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_calrdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Calrdy_ValueProvider(_);
                        return status_calrdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Calrdy_Read(_, __),
                    name: "Calrdy")
            .WithReservedBits(1, 14)
            .WithFlag(15, out status_requestdebug_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Requestdebug_ValueProvider(_);
                        return status_requestdebug_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Requestdebug_Read(_, __),
                    name: "Requestdebug")
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
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x93F7)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Wdoglock - Offset : 0x14
        protected DoubleWordRegister  GenerateWdoglockRegister() => new DoubleWordRegister(this, 0x5257)
            .WithValueField(0, 16, out wdoglock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Wdoglock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Wdoglock_Read(_, __))
            .WithWriteCallback((_, __) => Wdoglock_Write(_, __));
        
        // If - Offset : 0x20
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateTestRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, TEST_CLKOUTHIDDENSEL>(0, 5, out test_clkouthiddensel_field, 
                    valueProviderCallback: (_) => {
                        Test_Clkouthiddensel_ValueProvider(_);
                        return test_clkouthiddensel_field.Value;               
                    },
                    writeCallback: (_, __) => Test_Clkouthiddensel_Write(_, __),
                    readCallback: (_, __) => Test_Clkouthiddensel_Read(_, __),
                    name: "Clkouthiddensel")
            .WithReservedBits(5, 11)
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
        protected DoubleWordRegister  GenerateTesthvRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateTestclkenRegister() => new DoubleWordRegister(this, 0xBC)
            .WithReservedBits(0, 2)
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
            .WithReservedBits(6, 1)
            .WithFlag(7, out testclken_imemflash_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Imemflash_ValueProvider(_);
                        return testclken_imemflash_bit.Value;               
                    },
                    writeCallback: (_, __) => Testclken_Imemflash_Write(_, __),
                    readCallback: (_, __) => Testclken_Imemflash_Read(_, __),
                    name: "Imemflash")
            .WithReservedBits(8, 22)
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
        protected DoubleWordRegister  GenerateCalcmdRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateCalctrlRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateCalcntRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateClken0Register() => new DoubleWordRegister(this, 0x0)
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
            .WithFlag(8, out clken0_timer4_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Timer4_ValueProvider(_);
                        return clken0_timer4_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Timer4_Write(_, __),
                    readCallback: (_, __) => Clken0_Timer4_Read(_, __),
                    name: "Timer4")
            .WithFlag(9, out clken0_usart0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Usart0_ValueProvider(_);
                        return clken0_usart0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Usart0_Write(_, __),
                    readCallback: (_, __) => Clken0_Usart0_Read(_, __),
                    name: "Usart0")
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
            .WithFlag(19, out clken0_hfrcoem23_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Hfrcoem23_ValueProvider(_);
                        return clken0_hfrcoem23_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Hfrcoem23_Write(_, __),
                    readCallback: (_, __) => Clken0_Hfrcoem23_Read(_, __),
                    name: "Hfrcoem23")
            .WithFlag(20, out clken0_hfxo0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Hfxo0_ValueProvider(_);
                        return clken0_hfxo0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Hfxo0_Write(_, __),
                    readCallback: (_, __) => Clken0_Hfxo0_Read(_, __),
                    name: "Hfxo0")
            .WithFlag(21, out clken0_fsrco_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Fsrco_ValueProvider(_);
                        return clken0_fsrco_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Fsrco_Write(_, __),
                    readCallback: (_, __) => Clken0_Fsrco_Read(_, __),
                    name: "Fsrco")
            .WithFlag(22, out clken0_lfrco_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Lfrco_ValueProvider(_);
                        return clken0_lfrco_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Lfrco_Write(_, __),
                    readCallback: (_, __) => Clken0_Lfrco_Read(_, __),
                    name: "Lfrco")
            .WithFlag(23, out clken0_lfxo_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Lfxo_ValueProvider(_);
                        return clken0_lfxo_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Lfxo_Write(_, __),
                    readCallback: (_, __) => Clken0_Lfxo_Read(_, __),
                    name: "Lfxo")
            .WithFlag(24, out clken0_ulfrco_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Ulfrco_ValueProvider(_);
                        return clken0_ulfrco_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Ulfrco_Write(_, __),
                    readCallback: (_, __) => Clken0_Ulfrco_Read(_, __),
                    name: "Ulfrco")
            .WithReservedBits(25, 1)
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
            .WithFlag(30, out clken0_sysrtc0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Sysrtc0_ValueProvider(_);
                        return clken0_sysrtc0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Sysrtc0_Write(_, __),
                    readCallback: (_, __) => Clken0_Sysrtc0_Read(_, __),
                    name: "Sysrtc0")
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
        protected DoubleWordRegister  GenerateClken1Register() => new DoubleWordRegister(this, 0x0)
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
            .WithFlag(7, out clken1_rfscratchpad_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rfscratchpad_ValueProvider(_);
                        return clken1_rfscratchpad_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Rfscratchpad_Write(_, __),
                    readCallback: (_, __) => Clken1_Rfscratchpad_Read(_, __),
                    name: "Rfscratchpad")
            .WithFlag(8, out clken1_hostmailbox_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Hostmailbox_ValueProvider(_);
                        return clken1_hostmailbox_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Hostmailbox_Write(_, __),
                    readCallback: (_, __) => Clken1_Hostmailbox_Read(_, __),
                    name: "Hostmailbox")
            .WithFlag(9, out clken1_rfmailbox_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rfmailbox_ValueProvider(_);
                        return clken1_rfmailbox_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Rfmailbox_Write(_, __),
                    readCallback: (_, __) => Clken1_Rfmailbox_Read(_, __),
                    name: "Rfmailbox")
            .WithFlag(10, out clken1_semailboxhost_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Semailboxhost_ValueProvider(_);
                        return clken1_semailboxhost_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Semailboxhost_Write(_, __),
                    readCallback: (_, __) => Clken1_Semailboxhost_Read(_, __),
                    name: "Semailboxhost")
            .WithFlag(11, out clken1_bufc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Bufc_ValueProvider(_);
                        return clken1_bufc_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Bufc_Write(_, __),
                    readCallback: (_, __) => Clken1_Bufc_Read(_, __),
                    name: "Bufc")
            .WithReservedBits(12, 1)
            .WithFlag(13, out clken1_keyscan_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Keyscan_ValueProvider(_);
                        return clken1_keyscan_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Keyscan_Write(_, __),
                    readCallback: (_, __) => Clken1_Keyscan_Read(_, __),
                    name: "Keyscan")
            .WithFlag(14, out clken1_smu_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Smu_ValueProvider(_);
                        return clken1_smu_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Smu_Write(_, __),
                    readCallback: (_, __) => Clken1_Smu_Read(_, __),
                    name: "Smu")
            .WithFlag(15, out clken1_icache0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Icache0_ValueProvider(_);
                        return clken1_icache0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Icache0_Write(_, __),
                    readCallback: (_, __) => Clken1_Icache0_Read(_, __),
                    name: "Icache0")
            .WithFlag(16, out clken1_msc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Msc_ValueProvider(_);
                        return clken1_msc_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Msc_Write(_, __),
                    readCallback: (_, __) => Clken1_Msc_Read(_, __),
                    name: "Msc")
            .WithFlag(17, out clken1_wdog1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Wdog1_ValueProvider(_);
                        return clken1_wdog1_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Wdog1_Write(_, __),
                    readCallback: (_, __) => Clken1_Wdog1_Read(_, __),
                    name: "Wdog1")
            .WithFlag(18, out clken1_acmp0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Acmp0_ValueProvider(_);
                        return clken1_acmp0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Acmp0_Write(_, __),
                    readCallback: (_, __) => Clken1_Acmp0_Read(_, __),
                    name: "Acmp0")
            .WithFlag(19, out clken1_acmp1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Acmp1_ValueProvider(_);
                        return clken1_acmp1_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Acmp1_Write(_, __),
                    readCallback: (_, __) => Clken1_Acmp1_Read(_, __),
                    name: "Acmp1")
            .WithFlag(20, out clken1_vdac0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Vdac0_ValueProvider(_);
                        return clken1_vdac0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Vdac0_Write(_, __),
                    readCallback: (_, __) => Clken1_Vdac0_Read(_, __),
                    name: "Vdac0")
            .WithFlag(21, out clken1_pcnt0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Pcnt0_ValueProvider(_);
                        return clken1_pcnt0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Pcnt0_Write(_, __),
                    readCallback: (_, __) => Clken1_Pcnt0_Read(_, __),
                    name: "Pcnt0")
            .WithFlag(22, out clken1_eusart0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Eusart0_ValueProvider(_);
                        return clken1_eusart0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Eusart0_Write(_, __),
                    readCallback: (_, __) => Clken1_Eusart0_Read(_, __),
                    name: "Eusart0")
            .WithFlag(23, out clken1_eusart1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Eusart1_ValueProvider(_);
                        return clken1_eusart1_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Eusart1_Write(_, __),
                    readCallback: (_, __) => Clken1_Eusart1_Read(_, __),
                    name: "Eusart1")
            .WithReservedBits(24, 1)
            .WithFlag(25, out clken1_rfeca0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rfeca0_ValueProvider(_);
                        return clken1_rfeca0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Rfeca0_Write(_, __),
                    readCallback: (_, __) => Clken1_Rfeca0_Read(_, __),
                    name: "Rfeca0")
            .WithFlag(26, out clken1_rfeca1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rfeca1_ValueProvider(_);
                        return clken1_rfeca1_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Rfeca1_Write(_, __),
                    readCallback: (_, __) => Clken1_Rfeca1_Read(_, __),
                    name: "Rfeca1")
            .WithFlag(27, out clken1_dmem_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Dmem_ValueProvider(_);
                        return clken1_dmem_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Dmem_Write(_, __),
                    readCallback: (_, __) => Clken1_Dmem_Read(_, __),
                    name: "Dmem")
            .WithFlag(28, out clken1_ecaifadc_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Ecaifadc_ValueProvider(_);
                        return clken1_ecaifadc_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Ecaifadc_Write(_, __),
                    readCallback: (_, __) => Clken1_Ecaifadc_Read(_, __),
                    name: "Ecaifadc")
            .WithFlag(29, out clken1_vdac1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Vdac1_ValueProvider(_);
                        return clken1_vdac1_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Vdac1_Write(_, __),
                    readCallback: (_, __) => Clken1_Vdac1_Read(_, __),
                    name: "Vdac1")
            .WithFlag(30, out clken1_mvp_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Mvp_ValueProvider(_);
                        return clken1_mvp_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Mvp_Write(_, __),
                    readCallback: (_, __) => Clken1_Mvp_Read(_, __),
                    name: "Mvp")
            .WithReservedBits(31, 1)
            .WithReadCallback((_, __) => Clken1_Read(_, __))
            .WithWriteCallback((_, __) => Clken1_Write(_, __));
        
        // Sysclkctrl - Offset : 0x70
        protected DoubleWordRegister  GenerateSysclkctrlRegister() => new DoubleWordRegister(this, 0x1)
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
        
        // Seclkctrl - Offset : 0x74
        protected DoubleWordRegister  GenerateSeclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, SECLKCTRL_CLKSEL>(0, 2, out seclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Seclkctrl_Clksel_ValueProvider(_);
                        return seclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Seclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Seclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 29)
            .WithFlag(31, out seclkctrl_discon_bit, 
                    valueProviderCallback: (_) => {
                        Seclkctrl_Discon_ValueProvider(_);
                        return seclkctrl_discon_bit.Value;               
                    },
                    writeCallback: (_, __) => Seclkctrl_Discon_Write(_, __),
                    readCallback: (_, __) => Seclkctrl_Discon_Read(_, __),
                    name: "Discon")
            .WithReadCallback((_, __) => Seclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Seclkctrl_Write(_, __));
        
        // Traceclkctrl - Offset : 0x80
        protected DoubleWordRegister  GenerateTraceclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, TRACECLKCTRL_CLKSEL>(0, 2, out traceclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Traceclkctrl_Clksel_ValueProvider(_);
                        return traceclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Traceclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Traceclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 2)
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
        protected DoubleWordRegister  GenerateExportclkctrlRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateDpllrefclkctrlRegister() => new DoubleWordRegister(this, 0x0)
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
        protected DoubleWordRegister  GenerateEm01grpaclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EM01GRPACLKCTRL_CLKSEL>(0, 3, out em01grpaclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Em01grpaclkctrl_Clksel_ValueProvider(_);
                        return em01grpaclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Em01grpaclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Em01grpaclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Em01grpaclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em01grpaclkctrl_Write(_, __));
        
        // Em01grpcclkctrl - Offset : 0x128
        protected DoubleWordRegister  GenerateEm01grpcclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EM01GRPCCLKCTRL_CLKSEL>(0, 3, out em01grpcclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Em01grpcclkctrl_Clksel_ValueProvider(_);
                        return em01grpcclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Em01grpcclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Em01grpcclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Em01grpcclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em01grpcclkctrl_Write(_, __));
        
        // Em23grpaclkctrl - Offset : 0x140
        protected DoubleWordRegister  GenerateEm23grpaclkctrlRegister() => new DoubleWordRegister(this, 0x1)
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
        protected DoubleWordRegister  GenerateEm4grpaclkctrlRegister() => new DoubleWordRegister(this, 0x1)
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
        protected DoubleWordRegister  GenerateIadcclkctrlRegister() => new DoubleWordRegister(this, 0x1)
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
        protected DoubleWordRegister  GenerateWdog0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
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
        
        // Wdog1clkctrl - Offset : 0x208
        protected DoubleWordRegister  GenerateWdog1clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, WDOG1CLKCTRL_CLKSEL>(0, 3, out wdog1clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Wdog1clkctrl_Clksel_ValueProvider(_);
                        return wdog1clkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Wdog1clkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Wdog1clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Wdog1clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Wdog1clkctrl_Write(_, __));
        
        // Eusart0clkctrl - Offset : 0x220
        protected DoubleWordRegister  GenerateEusart0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EUSART0CLKCTRL_CLKSEL>(0, 3, out eusart0clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Eusart0clkctrl_Clksel_ValueProvider(_);
                        return eusart0clkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Eusart0clkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Eusart0clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Eusart0clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Eusart0clkctrl_Write(_, __));
        
        // Synthclkctrl - Offset : 0x230
        protected DoubleWordRegister  GenerateSynthclkctrlRegister() => new DoubleWordRegister(this, 0x1)
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
        
        // Sysrtc0clkctrl - Offset : 0x240
        protected DoubleWordRegister  GenerateSysrtc0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, SYSRTC0CLKCTRL_CLKSEL>(0, 2, out sysrtc0clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Sysrtc0clkctrl_Clksel_ValueProvider(_);
                        return sysrtc0clkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Sysrtc0clkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Sysrtc0clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Sysrtc0clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Sysrtc0clkctrl_Write(_, __));
        
        // Lcdclkctrl - Offset : 0x250
        protected DoubleWordRegister  GenerateLcdclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, LCDCLKCTRL_CLKSEL>(0, 2, out lcdclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Lcdclkctrl_Clksel_ValueProvider(_);
                        return lcdclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Lcdclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Lcdclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Lcdclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Lcdclkctrl_Write(_, __));
        
        // Vdac0clkctrl - Offset : 0x260
        protected DoubleWordRegister  GenerateVdac0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, VDAC0CLKCTRL_CLKSEL>(0, 3, out vdac0clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Vdac0clkctrl_Clksel_ValueProvider(_);
                        return vdac0clkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Vdac0clkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Vdac0clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Vdac0clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Vdac0clkctrl_Write(_, __));
        
        // Pcnt0clkctrl - Offset : 0x270
        protected DoubleWordRegister  GeneratePcnt0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, PCNT0CLKCTRL_CLKSEL>(0, 2, out pcnt0clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Pcnt0clkctrl_Clksel_ValueProvider(_);
                        return pcnt0clkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Pcnt0clkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Pcnt0clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Pcnt0clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pcnt0clkctrl_Write(_, __));
        
        // Radioclkctrl - Offset : 0x280
        protected DoubleWordRegister  GenerateRadioclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out radioclkctrl_en_bit, 
                    valueProviderCallback: (_) => {
                        Radioclkctrl_En_ValueProvider(_);
                        return radioclkctrl_en_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioclkctrl_En_Write(_, __),
                    readCallback: (_, __) => Radioclkctrl_En_Read(_, __),
                    name: "En")
            .WithFlag(1, out radioclkctrl_forceclkenradio_bit, 
                    valueProviderCallback: (_) => {
                        Radioclkctrl_Forceclkenradio_ValueProvider(_);
                        return radioclkctrl_forceclkenradio_bit.Value;               
                    },
                    writeCallback: (_, __) => Radioclkctrl_Forceclkenradio_Write(_, __),
                    readCallback: (_, __) => Radioclkctrl_Forceclkenradio_Read(_, __),
                    name: "Forceclkenradio")
            .WithReservedBits(2, 29)
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
        
        // Dapclkctrl - Offset : 0x284
        protected DoubleWordRegister  GenerateDapclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, DAPCLKCTRL_CLKSEL>(0, 2, out dapclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Dapclkctrl_Clksel_ValueProvider(_);
                        return dapclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Dapclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Dapclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Dapclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Dapclkctrl_Write(_, __));
        
        // Lesensehfclkctrl - Offset : 0x290
        protected DoubleWordRegister  GenerateLesensehfclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, LESENSEHFCLKCTRL_CLKSEL>(0, 2, out lesensehfclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Lesensehfclkctrl_Clksel_ValueProvider(_);
                        return lesensehfclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Lesensehfclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Lesensehfclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Lesensehfclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Lesensehfclkctrl_Write(_, __));
        
        // Vdac1clkctrl - Offset : 0x294
        protected DoubleWordRegister  GenerateVdac1clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, VDAC1CLKCTRL_CLKSEL>(0, 3, out vdac1clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Vdac1clkctrl_Clksel_ValueProvider(_);
                        return vdac1clkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Vdac1clkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Vdac1clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Vdac1clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Vdac1clkctrl_Write(_, __));
        
        // Rpuratd0_Drpu - Offset : 0x298
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
            .WithReservedBits(2, 2)
            .WithFlag(4, out rpuratd0_drpu_ratdlock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdlock_ValueProvider(_);
                        return rpuratd0_drpu_ratdlock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdlock_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdlock_Read(_, __),
                    name: "Ratdlock")
            .WithFlag(5, out rpuratd0_drpu_ratdwdoglock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdwdoglock_ValueProvider(_);
                        return rpuratd0_drpu_ratdwdoglock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdwdoglock_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdwdoglock_Read(_, __),
                    name: "Ratdwdoglock")
            .WithReservedBits(6, 2)
            .WithFlag(8, out rpuratd0_drpu_ratdif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdif_ValueProvider(_);
                        return rpuratd0_drpu_ratdif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdif_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdif_Read(_, __),
                    name: "Ratdif")
            .WithFlag(9, out rpuratd0_drpu_ratdien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdien_ValueProvider(_);
                        return rpuratd0_drpu_ratdien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdien_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdien_Read(_, __),
                    name: "Ratdien")
            .WithReservedBits(10, 2)
            .WithFlag(12, out rpuratd0_drpu_ratdtest_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtest_ValueProvider(_);
                        return rpuratd0_drpu_ratdtest_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtest_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtest_Read(_, __),
                    name: "Ratdtest")
            .WithFlag(13, out rpuratd0_drpu_ratdtesthv_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtesthv_ValueProvider(_);
                        return rpuratd0_drpu_ratdtesthv_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtesthv_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtesthv_Read(_, __),
                    name: "Ratdtesthv")
            .WithReservedBits(14, 2)
            .WithFlag(16, out rpuratd0_drpu_ratdtestclken_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtestclken_ValueProvider(_);
                        return rpuratd0_drpu_ratdtestclken_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtestclken_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtestclken_Read(_, __),
                    name: "Ratdtestclken")
            .WithReservedBits(17, 3)
            .WithFlag(20, out rpuratd0_drpu_ratdcalcmd_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdcalcmd_ValueProvider(_);
                        return rpuratd0_drpu_ratdcalcmd_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdcalcmd_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdcalcmd_Read(_, __),
                    name: "Ratdcalcmd")
            .WithFlag(21, out rpuratd0_drpu_ratdcalctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdcalctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdcalctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdcalctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdcalctrl_Read(_, __),
                    name: "Ratdcalctrl")
            .WithReservedBits(22, 3)
            .WithFlag(25, out rpuratd0_drpu_ratdclken0_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdclken0_ValueProvider(_);
                        return rpuratd0_drpu_ratdclken0_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdclken0_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdclken0_Read(_, __),
                    name: "Ratdclken0")
            .WithFlag(26, out rpuratd0_drpu_ratdclken1_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdclken1_ValueProvider(_);
                        return rpuratd0_drpu_ratdclken1_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdclken1_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdclken1_Read(_, __),
                    name: "Ratdclken1")
            .WithReservedBits(27, 1)
            .WithFlag(28, out rpuratd0_drpu_ratdsysclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdsysclkctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdsysclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdsysclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdsysclkctrl_Read(_, __),
                    name: "Ratdsysclkctrl")
            .WithFlag(29, out rpuratd0_drpu_ratdseclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdseclkctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdseclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdseclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdseclkctrl_Read(_, __),
                    name: "Ratdseclkctrl")
            .WithReservedBits(30, 2)
            .WithReadCallback((_, __) => Rpuratd0_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd0_Drpu_Write(_, __));
        
        // Rpuratd1_Drpu - Offset : 0x29C
        protected DoubleWordRegister  GenerateRpuratd1_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd1_drpu_ratdtraceclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdtraceclkctrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdtraceclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdtraceclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdtraceclkctrl_Read(_, __),
                    name: "Ratdtraceclkctrl")
            .WithReservedBits(1, 3)
            .WithFlag(4, out rpuratd1_drpu_ratdexportclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdexportclkctrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdexportclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdexportclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdexportclkctrl_Read(_, __),
                    name: "Ratdexportclkctrl")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Rpuratd1_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd1_Drpu_Write(_, __));
        
        // Rpuratd2_Drpu - Offset : 0x2A0
        protected DoubleWordRegister  GenerateRpuratd2_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd2_drpu_ratddpllrefclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratddpllrefclkctrl_ValueProvider(_);
                        return rpuratd2_drpu_ratddpllrefclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratddpllrefclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratddpllrefclkctrl_Read(_, __),
                    name: "Ratddpllrefclkctrl")
            .WithReservedBits(1, 7)
            .WithFlag(8, out rpuratd2_drpu_ratdem01grpaclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdem01grpaclkctrl_ValueProvider(_);
                        return rpuratd2_drpu_ratdem01grpaclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdem01grpaclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdem01grpaclkctrl_Read(_, __),
                    name: "Ratdem01grpaclkctrl")
            .WithReservedBits(9, 1)
            .WithFlag(10, out rpuratd2_drpu_ratdem01grpcclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdem01grpcclkctrl_ValueProvider(_);
                        return rpuratd2_drpu_ratdem01grpcclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdem01grpcclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdem01grpcclkctrl_Read(_, __),
                    name: "Ratdem01grpcclkctrl")
            .WithReservedBits(11, 5)
            .WithFlag(16, out rpuratd2_drpu_ratdem23grpaclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdem23grpaclkctrl_ValueProvider(_);
                        return rpuratd2_drpu_ratdem23grpaclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdem23grpaclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdem23grpaclkctrl_Read(_, __),
                    name: "Ratdem23grpaclkctrl")
            .WithReservedBits(17, 7)
            .WithFlag(24, out rpuratd2_drpu_ratdem4grpaclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdem4grpaclkctrl_ValueProvider(_);
                        return rpuratd2_drpu_ratdem4grpaclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdem4grpaclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdem4grpaclkctrl_Read(_, __),
                    name: "Ratdem4grpaclkctrl")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => Rpuratd2_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd2_Drpu_Write(_, __));
        
        // Rpuratd3_Drpu - Offset : 0x2A4
        protected DoubleWordRegister  GenerateRpuratd3_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd3_drpu_ratdiadcclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdiadcclkctrl_ValueProvider(_);
                        return rpuratd3_drpu_ratdiadcclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdiadcclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdiadcclkctrl_Read(_, __),
                    name: "Ratdiadcclkctrl")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Rpuratd3_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd3_Drpu_Write(_, __));
        
        // Rpuratd4_Drpu - Offset : 0x2A8
        protected DoubleWordRegister  GenerateRpuratd4_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd4_drpu_ratdwdog0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdwdog0clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdwdog0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdwdog0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdwdog0clkctrl_Read(_, __),
                    name: "Ratdwdog0clkctrl")
            .WithReservedBits(1, 1)
            .WithFlag(2, out rpuratd4_drpu_ratdwdog1clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdwdog1clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdwdog1clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdwdog1clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdwdog1clkctrl_Read(_, __),
                    name: "Ratdwdog1clkctrl")
            .WithReservedBits(3, 5)
            .WithFlag(8, out rpuratd4_drpu_ratdeusart0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdeusart0clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdeusart0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdeusart0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdeusart0clkctrl_Read(_, __),
                    name: "Ratdeusart0clkctrl")
            .WithReservedBits(9, 3)
            .WithFlag(12, out rpuratd4_drpu_ratdsynthclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdsynthclkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdsynthclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdsynthclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdsynthclkctrl_Read(_, __),
                    name: "Ratdsynthclkctrl")
            .WithReservedBits(13, 3)
            .WithFlag(16, out rpuratd4_drpu_ratdsysrtc0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdsysrtc0clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdsysrtc0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdsysrtc0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdsysrtc0clkctrl_Read(_, __),
                    name: "Ratdsysrtc0clkctrl")
            .WithReservedBits(17, 3)
            .WithFlag(20, out rpuratd4_drpu_ratdlcdclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdlcdclkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdlcdclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdlcdclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdlcdclkctrl_Read(_, __),
                    name: "Ratdlcdclkctrl")
            .WithReservedBits(21, 3)
            .WithFlag(24, out rpuratd4_drpu_ratdvdac0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdvdac0clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdvdac0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdvdac0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdvdac0clkctrl_Read(_, __),
                    name: "Ratdvdac0clkctrl")
            .WithReservedBits(25, 3)
            .WithFlag(28, out rpuratd4_drpu_ratdpcnt0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdpcnt0clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdpcnt0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdpcnt0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdpcnt0clkctrl_Read(_, __),
                    name: "Ratdpcnt0clkctrl")
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => Rpuratd4_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd4_Drpu_Write(_, __));
        
        // Rpuratd5_Drpu - Offset : 0x2AC
        protected DoubleWordRegister  GenerateRpuratd5_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd5_drpu_ratdradioclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdradioclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdradioclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdradioclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdradioclkctrl_Read(_, __),
                    name: "Ratdradioclkctrl")
            .WithFlag(1, out rpuratd5_drpu_ratddapclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratddapclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratddapclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratddapclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratddapclkctrl_Read(_, __),
                    name: "Ratddapclkctrl")
            .WithReservedBits(2, 2)
            .WithFlag(4, out rpuratd5_drpu_ratdlesensehfclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdlesensehfclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdlesensehfclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdlesensehfclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdlesensehfclkctrl_Read(_, __),
                    name: "Ratdlesensehfclkctrl")
            .WithFlag(5, out rpuratd5_drpu_ratdvdac1clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdvdac1clkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdvdac1clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdvdac1clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdvdac1clkctrl_Read(_, __),
                    name: "Ratdvdac1clkctrl")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Rpuratd5_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd5_Drpu_Write(_, __));
        

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
        protected IFlagRegisterField ctrl_forceem1pclken_bit;
        partial void Ctrl_Forceem1pclken_Write(bool a, bool b);
        partial void Ctrl_Forceem1pclken_Read(bool a, bool b);
        partial void Ctrl_Forceem1pclken_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_forceclkin0_bit;
        partial void Ctrl_Forceclkin0_Write(bool a, bool b);
        partial void Ctrl_Forceclkin0_Read(bool a, bool b);
        partial void Ctrl_Forceclkin0_ValueProvider(bool a);

        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Status - Offset : 0x8
        protected IFlagRegisterField status_calrdy_bit;
        partial void Status_Calrdy_Read(bool a, bool b);
        partial void Status_Calrdy_ValueProvider(bool a);
        protected IFlagRegisterField status_requestdebug_bit;
        partial void Status_Requestdebug_Read(bool a, bool b);
        partial void Status_Requestdebug_ValueProvider(bool a);
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
        protected IFlagRegisterField testclken_imemflash_bit;
        partial void Testclken_Imemflash_Write(bool a, bool b);
        partial void Testclken_Imemflash_Read(bool a, bool b);
        partial void Testclken_Imemflash_ValueProvider(bool a);
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
        protected IFlagRegisterField clken0_timer4_bit;
        partial void Clken0_Timer4_Write(bool a, bool b);
        partial void Clken0_Timer4_Read(bool a, bool b);
        partial void Clken0_Timer4_ValueProvider(bool a);
        protected IFlagRegisterField clken0_usart0_bit;
        partial void Clken0_Usart0_Write(bool a, bool b);
        partial void Clken0_Usart0_Read(bool a, bool b);
        partial void Clken0_Usart0_ValueProvider(bool a);
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
        protected IFlagRegisterField clken0_hfrcoem23_bit;
        partial void Clken0_Hfrcoem23_Write(bool a, bool b);
        partial void Clken0_Hfrcoem23_Read(bool a, bool b);
        partial void Clken0_Hfrcoem23_ValueProvider(bool a);
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
        protected IFlagRegisterField clken0_sysrtc0_bit;
        partial void Clken0_Sysrtc0_Write(bool a, bool b);
        partial void Clken0_Sysrtc0_Read(bool a, bool b);
        partial void Clken0_Sysrtc0_ValueProvider(bool a);
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
        protected IFlagRegisterField clken1_rfscratchpad_bit;
        partial void Clken1_Rfscratchpad_Write(bool a, bool b);
        partial void Clken1_Rfscratchpad_Read(bool a, bool b);
        partial void Clken1_Rfscratchpad_ValueProvider(bool a);
        protected IFlagRegisterField clken1_hostmailbox_bit;
        partial void Clken1_Hostmailbox_Write(bool a, bool b);
        partial void Clken1_Hostmailbox_Read(bool a, bool b);
        partial void Clken1_Hostmailbox_ValueProvider(bool a);
        protected IFlagRegisterField clken1_rfmailbox_bit;
        partial void Clken1_Rfmailbox_Write(bool a, bool b);
        partial void Clken1_Rfmailbox_Read(bool a, bool b);
        partial void Clken1_Rfmailbox_ValueProvider(bool a);
        protected IFlagRegisterField clken1_semailboxhost_bit;
        partial void Clken1_Semailboxhost_Write(bool a, bool b);
        partial void Clken1_Semailboxhost_Read(bool a, bool b);
        partial void Clken1_Semailboxhost_ValueProvider(bool a);
        protected IFlagRegisterField clken1_bufc_bit;
        partial void Clken1_Bufc_Write(bool a, bool b);
        partial void Clken1_Bufc_Read(bool a, bool b);
        partial void Clken1_Bufc_ValueProvider(bool a);
        protected IFlagRegisterField clken1_keyscan_bit;
        partial void Clken1_Keyscan_Write(bool a, bool b);
        partial void Clken1_Keyscan_Read(bool a, bool b);
        partial void Clken1_Keyscan_ValueProvider(bool a);
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
        protected IFlagRegisterField clken1_wdog1_bit;
        partial void Clken1_Wdog1_Write(bool a, bool b);
        partial void Clken1_Wdog1_Read(bool a, bool b);
        partial void Clken1_Wdog1_ValueProvider(bool a);
        protected IFlagRegisterField clken1_acmp0_bit;
        partial void Clken1_Acmp0_Write(bool a, bool b);
        partial void Clken1_Acmp0_Read(bool a, bool b);
        partial void Clken1_Acmp0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_acmp1_bit;
        partial void Clken1_Acmp1_Write(bool a, bool b);
        partial void Clken1_Acmp1_Read(bool a, bool b);
        partial void Clken1_Acmp1_ValueProvider(bool a);
        protected IFlagRegisterField clken1_vdac0_bit;
        partial void Clken1_Vdac0_Write(bool a, bool b);
        partial void Clken1_Vdac0_Read(bool a, bool b);
        partial void Clken1_Vdac0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_pcnt0_bit;
        partial void Clken1_Pcnt0_Write(bool a, bool b);
        partial void Clken1_Pcnt0_Read(bool a, bool b);
        partial void Clken1_Pcnt0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_eusart0_bit;
        partial void Clken1_Eusart0_Write(bool a, bool b);
        partial void Clken1_Eusart0_Read(bool a, bool b);
        partial void Clken1_Eusart0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_eusart1_bit;
        partial void Clken1_Eusart1_Write(bool a, bool b);
        partial void Clken1_Eusart1_Read(bool a, bool b);
        partial void Clken1_Eusart1_ValueProvider(bool a);
        protected IFlagRegisterField clken1_rfeca0_bit;
        partial void Clken1_Rfeca0_Write(bool a, bool b);
        partial void Clken1_Rfeca0_Read(bool a, bool b);
        partial void Clken1_Rfeca0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_rfeca1_bit;
        partial void Clken1_Rfeca1_Write(bool a, bool b);
        partial void Clken1_Rfeca1_Read(bool a, bool b);
        partial void Clken1_Rfeca1_ValueProvider(bool a);
        protected IFlagRegisterField clken1_dmem_bit;
        partial void Clken1_Dmem_Write(bool a, bool b);
        partial void Clken1_Dmem_Read(bool a, bool b);
        partial void Clken1_Dmem_ValueProvider(bool a);
        protected IFlagRegisterField clken1_ecaifadc_bit;
        partial void Clken1_Ecaifadc_Write(bool a, bool b);
        partial void Clken1_Ecaifadc_Read(bool a, bool b);
        partial void Clken1_Ecaifadc_ValueProvider(bool a);
        protected IFlagRegisterField clken1_vdac1_bit;
        partial void Clken1_Vdac1_Write(bool a, bool b);
        partial void Clken1_Vdac1_Read(bool a, bool b);
        partial void Clken1_Vdac1_ValueProvider(bool a);
        protected IFlagRegisterField clken1_mvp_bit;
        partial void Clken1_Mvp_Write(bool a, bool b);
        partial void Clken1_Mvp_Read(bool a, bool b);
        partial void Clken1_Mvp_ValueProvider(bool a);

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
        
        // Seclkctrl - Offset : 0x74
        protected IEnumRegisterField<SECLKCTRL_CLKSEL> seclkctrl_clksel_field;
        partial void Seclkctrl_Clksel_Write(SECLKCTRL_CLKSEL a, SECLKCTRL_CLKSEL b);
        partial void Seclkctrl_Clksel_Read(SECLKCTRL_CLKSEL a, SECLKCTRL_CLKSEL b);
        partial void Seclkctrl_Clksel_ValueProvider(SECLKCTRL_CLKSEL a);
        protected IFlagRegisterField seclkctrl_discon_bit;
        partial void Seclkctrl_Discon_Write(bool a, bool b);
        partial void Seclkctrl_Discon_Read(bool a, bool b);
        partial void Seclkctrl_Discon_ValueProvider(bool a);

        partial void Seclkctrl_Write(uint a, uint b);
        partial void Seclkctrl_Read(uint a, uint b);
        
        // Traceclkctrl - Offset : 0x80
        protected IEnumRegisterField<TRACECLKCTRL_CLKSEL> traceclkctrl_clksel_field;
        partial void Traceclkctrl_Clksel_Write(TRACECLKCTRL_CLKSEL a, TRACECLKCTRL_CLKSEL b);
        partial void Traceclkctrl_Clksel_Read(TRACECLKCTRL_CLKSEL a, TRACECLKCTRL_CLKSEL b);
        partial void Traceclkctrl_Clksel_ValueProvider(TRACECLKCTRL_CLKSEL a);
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
        
        // Em01grpcclkctrl - Offset : 0x128
        protected IEnumRegisterField<EM01GRPCCLKCTRL_CLKSEL> em01grpcclkctrl_clksel_field;
        partial void Em01grpcclkctrl_Clksel_Write(EM01GRPCCLKCTRL_CLKSEL a, EM01GRPCCLKCTRL_CLKSEL b);
        partial void Em01grpcclkctrl_Clksel_Read(EM01GRPCCLKCTRL_CLKSEL a, EM01GRPCCLKCTRL_CLKSEL b);
        partial void Em01grpcclkctrl_Clksel_ValueProvider(EM01GRPCCLKCTRL_CLKSEL a);

        partial void Em01grpcclkctrl_Write(uint a, uint b);
        partial void Em01grpcclkctrl_Read(uint a, uint b);
        
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
        
        // Wdog1clkctrl - Offset : 0x208
        protected IEnumRegisterField<WDOG1CLKCTRL_CLKSEL> wdog1clkctrl_clksel_field;
        partial void Wdog1clkctrl_Clksel_Write(WDOG1CLKCTRL_CLKSEL a, WDOG1CLKCTRL_CLKSEL b);
        partial void Wdog1clkctrl_Clksel_Read(WDOG1CLKCTRL_CLKSEL a, WDOG1CLKCTRL_CLKSEL b);
        partial void Wdog1clkctrl_Clksel_ValueProvider(WDOG1CLKCTRL_CLKSEL a);

        partial void Wdog1clkctrl_Write(uint a, uint b);
        partial void Wdog1clkctrl_Read(uint a, uint b);
        
        // Eusart0clkctrl - Offset : 0x220
        protected IEnumRegisterField<EUSART0CLKCTRL_CLKSEL> eusart0clkctrl_clksel_field;
        partial void Eusart0clkctrl_Clksel_Write(EUSART0CLKCTRL_CLKSEL a, EUSART0CLKCTRL_CLKSEL b);
        partial void Eusart0clkctrl_Clksel_Read(EUSART0CLKCTRL_CLKSEL a, EUSART0CLKCTRL_CLKSEL b);
        partial void Eusart0clkctrl_Clksel_ValueProvider(EUSART0CLKCTRL_CLKSEL a);

        partial void Eusart0clkctrl_Write(uint a, uint b);
        partial void Eusart0clkctrl_Read(uint a, uint b);
        
        // Synthclkctrl - Offset : 0x230
        protected IEnumRegisterField<SYNTHCLKCTRL_CLKSEL> synthclkctrl_clksel_field;
        partial void Synthclkctrl_Clksel_Write(SYNTHCLKCTRL_CLKSEL a, SYNTHCLKCTRL_CLKSEL b);
        partial void Synthclkctrl_Clksel_Read(SYNTHCLKCTRL_CLKSEL a, SYNTHCLKCTRL_CLKSEL b);
        partial void Synthclkctrl_Clksel_ValueProvider(SYNTHCLKCTRL_CLKSEL a);

        partial void Synthclkctrl_Write(uint a, uint b);
        partial void Synthclkctrl_Read(uint a, uint b);
        
        // Sysrtc0clkctrl - Offset : 0x240
        protected IEnumRegisterField<SYSRTC0CLKCTRL_CLKSEL> sysrtc0clkctrl_clksel_field;
        partial void Sysrtc0clkctrl_Clksel_Write(SYSRTC0CLKCTRL_CLKSEL a, SYSRTC0CLKCTRL_CLKSEL b);
        partial void Sysrtc0clkctrl_Clksel_Read(SYSRTC0CLKCTRL_CLKSEL a, SYSRTC0CLKCTRL_CLKSEL b);
        partial void Sysrtc0clkctrl_Clksel_ValueProvider(SYSRTC0CLKCTRL_CLKSEL a);

        partial void Sysrtc0clkctrl_Write(uint a, uint b);
        partial void Sysrtc0clkctrl_Read(uint a, uint b);
        
        // Lcdclkctrl - Offset : 0x250
        protected IEnumRegisterField<LCDCLKCTRL_CLKSEL> lcdclkctrl_clksel_field;
        partial void Lcdclkctrl_Clksel_Write(LCDCLKCTRL_CLKSEL a, LCDCLKCTRL_CLKSEL b);
        partial void Lcdclkctrl_Clksel_Read(LCDCLKCTRL_CLKSEL a, LCDCLKCTRL_CLKSEL b);
        partial void Lcdclkctrl_Clksel_ValueProvider(LCDCLKCTRL_CLKSEL a);

        partial void Lcdclkctrl_Write(uint a, uint b);
        partial void Lcdclkctrl_Read(uint a, uint b);
        
        // Vdac0clkctrl - Offset : 0x260
        protected IEnumRegisterField<VDAC0CLKCTRL_CLKSEL> vdac0clkctrl_clksel_field;
        partial void Vdac0clkctrl_Clksel_Write(VDAC0CLKCTRL_CLKSEL a, VDAC0CLKCTRL_CLKSEL b);
        partial void Vdac0clkctrl_Clksel_Read(VDAC0CLKCTRL_CLKSEL a, VDAC0CLKCTRL_CLKSEL b);
        partial void Vdac0clkctrl_Clksel_ValueProvider(VDAC0CLKCTRL_CLKSEL a);

        partial void Vdac0clkctrl_Write(uint a, uint b);
        partial void Vdac0clkctrl_Read(uint a, uint b);
        
        // Pcnt0clkctrl - Offset : 0x270
        protected IEnumRegisterField<PCNT0CLKCTRL_CLKSEL> pcnt0clkctrl_clksel_field;
        partial void Pcnt0clkctrl_Clksel_Write(PCNT0CLKCTRL_CLKSEL a, PCNT0CLKCTRL_CLKSEL b);
        partial void Pcnt0clkctrl_Clksel_Read(PCNT0CLKCTRL_CLKSEL a, PCNT0CLKCTRL_CLKSEL b);
        partial void Pcnt0clkctrl_Clksel_ValueProvider(PCNT0CLKCTRL_CLKSEL a);

        partial void Pcnt0clkctrl_Write(uint a, uint b);
        partial void Pcnt0clkctrl_Read(uint a, uint b);
        
        // Radioclkctrl - Offset : 0x280
        protected IFlagRegisterField radioclkctrl_en_bit;
        partial void Radioclkctrl_En_Write(bool a, bool b);
        partial void Radioclkctrl_En_Read(bool a, bool b);
        partial void Radioclkctrl_En_ValueProvider(bool a);
        protected IFlagRegisterField radioclkctrl_forceclkenradio_bit;
        partial void Radioclkctrl_Forceclkenradio_Write(bool a, bool b);
        partial void Radioclkctrl_Forceclkenradio_Read(bool a, bool b);
        partial void Radioclkctrl_Forceclkenradio_ValueProvider(bool a);
        protected IFlagRegisterField radioclkctrl_dbgclk_bit;
        partial void Radioclkctrl_Dbgclk_Write(bool a, bool b);
        partial void Radioclkctrl_Dbgclk_Read(bool a, bool b);
        partial void Radioclkctrl_Dbgclk_ValueProvider(bool a);

        partial void Radioclkctrl_Write(uint a, uint b);
        partial void Radioclkctrl_Read(uint a, uint b);
        
        // Dapclkctrl - Offset : 0x284
        protected IEnumRegisterField<DAPCLKCTRL_CLKSEL> dapclkctrl_clksel_field;
        partial void Dapclkctrl_Clksel_Write(DAPCLKCTRL_CLKSEL a, DAPCLKCTRL_CLKSEL b);
        partial void Dapclkctrl_Clksel_Read(DAPCLKCTRL_CLKSEL a, DAPCLKCTRL_CLKSEL b);
        partial void Dapclkctrl_Clksel_ValueProvider(DAPCLKCTRL_CLKSEL a);

        partial void Dapclkctrl_Write(uint a, uint b);
        partial void Dapclkctrl_Read(uint a, uint b);
        
        // Lesensehfclkctrl - Offset : 0x290
        protected IEnumRegisterField<LESENSEHFCLKCTRL_CLKSEL> lesensehfclkctrl_clksel_field;
        partial void Lesensehfclkctrl_Clksel_Write(LESENSEHFCLKCTRL_CLKSEL a, LESENSEHFCLKCTRL_CLKSEL b);
        partial void Lesensehfclkctrl_Clksel_Read(LESENSEHFCLKCTRL_CLKSEL a, LESENSEHFCLKCTRL_CLKSEL b);
        partial void Lesensehfclkctrl_Clksel_ValueProvider(LESENSEHFCLKCTRL_CLKSEL a);

        partial void Lesensehfclkctrl_Write(uint a, uint b);
        partial void Lesensehfclkctrl_Read(uint a, uint b);
        
        // Vdac1clkctrl - Offset : 0x294
        protected IEnumRegisterField<VDAC1CLKCTRL_CLKSEL> vdac1clkctrl_clksel_field;
        partial void Vdac1clkctrl_Clksel_Write(VDAC1CLKCTRL_CLKSEL a, VDAC1CLKCTRL_CLKSEL b);
        partial void Vdac1clkctrl_Clksel_Read(VDAC1CLKCTRL_CLKSEL a, VDAC1CLKCTRL_CLKSEL b);
        partial void Vdac1clkctrl_Clksel_ValueProvider(VDAC1CLKCTRL_CLKSEL a);

        partial void Vdac1clkctrl_Write(uint a, uint b);
        partial void Vdac1clkctrl_Read(uint a, uint b);
        
        // Rpuratd0_Drpu - Offset : 0x298
        protected IFlagRegisterField rpuratd0_drpu_ratdctrl_bit;
        partial void Rpuratd0_Drpu_Ratdctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdlock_bit;
        partial void Rpuratd0_Drpu_Ratdlock_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdlock_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdlock_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdwdoglock_bit;
        partial void Rpuratd0_Drpu_Ratdwdoglock_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdwdoglock_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdwdoglock_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdif_bit;
        partial void Rpuratd0_Drpu_Ratdif_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdif_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdien_bit;
        partial void Rpuratd0_Drpu_Ratdien_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdien_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtest_bit;
        partial void Rpuratd0_Drpu_Ratdtest_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtest_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtest_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtesthv_bit;
        partial void Rpuratd0_Drpu_Ratdtesthv_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtesthv_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtesthv_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtestclken_bit;
        partial void Rpuratd0_Drpu_Ratdtestclken_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtestclken_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtestclken_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdcalcmd_bit;
        partial void Rpuratd0_Drpu_Ratdcalcmd_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcalcmd_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcalcmd_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdcalctrl_bit;
        partial void Rpuratd0_Drpu_Ratdcalctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcalctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcalctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdclken0_bit;
        partial void Rpuratd0_Drpu_Ratdclken0_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdclken0_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdclken0_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdclken1_bit;
        partial void Rpuratd0_Drpu_Ratdclken1_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdclken1_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdclken1_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdsysclkctrl_bit;
        partial void Rpuratd0_Drpu_Ratdsysclkctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdsysclkctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdsysclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdseclkctrl_bit;
        partial void Rpuratd0_Drpu_Ratdseclkctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdseclkctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdseclkctrl_ValueProvider(bool a);

        partial void Rpuratd0_Drpu_Write(uint a, uint b);
        partial void Rpuratd0_Drpu_Read(uint a, uint b);
        
        // Rpuratd1_Drpu - Offset : 0x29C
        protected IFlagRegisterField rpuratd1_drpu_ratdtraceclkctrl_bit;
        partial void Rpuratd1_Drpu_Ratdtraceclkctrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtraceclkctrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtraceclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdexportclkctrl_bit;
        partial void Rpuratd1_Drpu_Ratdexportclkctrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdexportclkctrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdexportclkctrl_ValueProvider(bool a);

        partial void Rpuratd1_Drpu_Write(uint a, uint b);
        partial void Rpuratd1_Drpu_Read(uint a, uint b);
        
        // Rpuratd2_Drpu - Offset : 0x2A0
        protected IFlagRegisterField rpuratd2_drpu_ratddpllrefclkctrl_bit;
        partial void Rpuratd2_Drpu_Ratddpllrefclkctrl_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratddpllrefclkctrl_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratddpllrefclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdem01grpaclkctrl_bit;
        partial void Rpuratd2_Drpu_Ratdem01grpaclkctrl_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem01grpaclkctrl_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem01grpaclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdem01grpcclkctrl_bit;
        partial void Rpuratd2_Drpu_Ratdem01grpcclkctrl_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem01grpcclkctrl_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem01grpcclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdem23grpaclkctrl_bit;
        partial void Rpuratd2_Drpu_Ratdem23grpaclkctrl_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem23grpaclkctrl_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem23grpaclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdem4grpaclkctrl_bit;
        partial void Rpuratd2_Drpu_Ratdem4grpaclkctrl_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem4grpaclkctrl_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdem4grpaclkctrl_ValueProvider(bool a);

        partial void Rpuratd2_Drpu_Write(uint a, uint b);
        partial void Rpuratd2_Drpu_Read(uint a, uint b);
        
        // Rpuratd3_Drpu - Offset : 0x2A4
        protected IFlagRegisterField rpuratd3_drpu_ratdiadcclkctrl_bit;
        partial void Rpuratd3_Drpu_Ratdiadcclkctrl_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdiadcclkctrl_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdiadcclkctrl_ValueProvider(bool a);

        partial void Rpuratd3_Drpu_Write(uint a, uint b);
        partial void Rpuratd3_Drpu_Read(uint a, uint b);
        
        // Rpuratd4_Drpu - Offset : 0x2A8
        protected IFlagRegisterField rpuratd4_drpu_ratdwdog0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdwdog0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdwdog0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdwdog0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdwdog1clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdwdog1clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdwdog1clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdwdog1clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdeusart0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdeusart0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdeusart0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdeusart0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdsynthclkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdsynthclkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdsynthclkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdsynthclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdsysrtc0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdsysrtc0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdsysrtc0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdsysrtc0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdlcdclkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdlcdclkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdlcdclkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdlcdclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdvdac0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdvdac0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdvdac0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdvdac0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdpcnt0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdpcnt0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdpcnt0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdpcnt0clkctrl_ValueProvider(bool a);

        partial void Rpuratd4_Drpu_Write(uint a, uint b);
        partial void Rpuratd4_Drpu_Read(uint a, uint b);
        
        // Rpuratd5_Drpu - Offset : 0x2AC
        protected IFlagRegisterField rpuratd5_drpu_ratdradioclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdradioclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdradioclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdradioclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratddapclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratddapclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratddapclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratddapclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdlesensehfclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdlesensehfclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdlesensehfclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdlesensehfclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdvdac1clkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdvdac1clkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdvdac1clkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdvdac1clkctrl_ValueProvider(bool a);

        partial void Rpuratd5_Drpu_Write(uint a, uint b);
        partial void Rpuratd5_Drpu_Read(uint a, uint b);
        
        partial void CMU_Reset();

        partial void EFR32xG2_CMU_3_Constructor();

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
            Seclkctrl = 0x74,
            Traceclkctrl = 0x80,
            Exportclkctrl = 0x90,
            Dpllrefclkctrl = 0x100,
            Em01grpaclkctrl = 0x120,
            Em01grpcclkctrl = 0x128,
            Em23grpaclkctrl = 0x140,
            Em4grpaclkctrl = 0x160,
            Iadcclkctrl = 0x180,
            Wdog0clkctrl = 0x200,
            Wdog1clkctrl = 0x208,
            Eusart0clkctrl = 0x220,
            Synthclkctrl = 0x230,
            Sysrtc0clkctrl = 0x240,
            Lcdclkctrl = 0x250,
            Vdac0clkctrl = 0x260,
            Pcnt0clkctrl = 0x270,
            Radioclkctrl = 0x280,
            Dapclkctrl = 0x284,
            Lesensehfclkctrl = 0x290,
            Vdac1clkctrl = 0x294,
            Rpuratd0_Drpu = 0x298,
            Rpuratd1_Drpu = 0x29C,
            Rpuratd2_Drpu = 0x2A0,
            Rpuratd3_Drpu = 0x2A4,
            Rpuratd4_Drpu = 0x2A8,
            Rpuratd5_Drpu = 0x2AC,
            
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
            Seclkctrl_SET = 0x1074,
            Traceclkctrl_SET = 0x1080,
            Exportclkctrl_SET = 0x1090,
            Dpllrefclkctrl_SET = 0x1100,
            Em01grpaclkctrl_SET = 0x1120,
            Em01grpcclkctrl_SET = 0x1128,
            Em23grpaclkctrl_SET = 0x1140,
            Em4grpaclkctrl_SET = 0x1160,
            Iadcclkctrl_SET = 0x1180,
            Wdog0clkctrl_SET = 0x1200,
            Wdog1clkctrl_SET = 0x1208,
            Eusart0clkctrl_SET = 0x1220,
            Synthclkctrl_SET = 0x1230,
            Sysrtc0clkctrl_SET = 0x1240,
            Lcdclkctrl_SET = 0x1250,
            Vdac0clkctrl_SET = 0x1260,
            Pcnt0clkctrl_SET = 0x1270,
            Radioclkctrl_SET = 0x1280,
            Dapclkctrl_SET = 0x1284,
            Lesensehfclkctrl_SET = 0x1290,
            Vdac1clkctrl_SET = 0x1294,
            Rpuratd0_Drpu_SET = 0x1298,
            Rpuratd1_Drpu_SET = 0x129C,
            Rpuratd2_Drpu_SET = 0x12A0,
            Rpuratd3_Drpu_SET = 0x12A4,
            Rpuratd4_Drpu_SET = 0x12A8,
            Rpuratd5_Drpu_SET = 0x12AC,
            
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
            Seclkctrl_CLR = 0x2074,
            Traceclkctrl_CLR = 0x2080,
            Exportclkctrl_CLR = 0x2090,
            Dpllrefclkctrl_CLR = 0x2100,
            Em01grpaclkctrl_CLR = 0x2120,
            Em01grpcclkctrl_CLR = 0x2128,
            Em23grpaclkctrl_CLR = 0x2140,
            Em4grpaclkctrl_CLR = 0x2160,
            Iadcclkctrl_CLR = 0x2180,
            Wdog0clkctrl_CLR = 0x2200,
            Wdog1clkctrl_CLR = 0x2208,
            Eusart0clkctrl_CLR = 0x2220,
            Synthclkctrl_CLR = 0x2230,
            Sysrtc0clkctrl_CLR = 0x2240,
            Lcdclkctrl_CLR = 0x2250,
            Vdac0clkctrl_CLR = 0x2260,
            Pcnt0clkctrl_CLR = 0x2270,
            Radioclkctrl_CLR = 0x2280,
            Dapclkctrl_CLR = 0x2284,
            Lesensehfclkctrl_CLR = 0x2290,
            Vdac1clkctrl_CLR = 0x2294,
            Rpuratd0_Drpu_CLR = 0x2298,
            Rpuratd1_Drpu_CLR = 0x229C,
            Rpuratd2_Drpu_CLR = 0x22A0,
            Rpuratd3_Drpu_CLR = 0x22A4,
            Rpuratd4_Drpu_CLR = 0x22A8,
            Rpuratd5_Drpu_CLR = 0x22AC,
            
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
            Seclkctrl_TGL = 0x3074,
            Traceclkctrl_TGL = 0x3080,
            Exportclkctrl_TGL = 0x3090,
            Dpllrefclkctrl_TGL = 0x3100,
            Em01grpaclkctrl_TGL = 0x3120,
            Em01grpcclkctrl_TGL = 0x3128,
            Em23grpaclkctrl_TGL = 0x3140,
            Em4grpaclkctrl_TGL = 0x3160,
            Iadcclkctrl_TGL = 0x3180,
            Wdog0clkctrl_TGL = 0x3200,
            Wdog1clkctrl_TGL = 0x3208,
            Eusart0clkctrl_TGL = 0x3220,
            Synthclkctrl_TGL = 0x3230,
            Sysrtc0clkctrl_TGL = 0x3240,
            Lcdclkctrl_TGL = 0x3250,
            Vdac0clkctrl_TGL = 0x3260,
            Pcnt0clkctrl_TGL = 0x3270,
            Radioclkctrl_TGL = 0x3280,
            Dapclkctrl_TGL = 0x3284,
            Lesensehfclkctrl_TGL = 0x3290,
            Vdac1clkctrl_TGL = 0x3294,
            Rpuratd0_Drpu_TGL = 0x3298,
            Rpuratd1_Drpu_TGL = 0x329C,
            Rpuratd2_Drpu_TGL = 0x32A0,
            Rpuratd3_Drpu_TGL = 0x32A4,
            Rpuratd4_Drpu_TGL = 0x32A8,
            Rpuratd5_Drpu_TGL = 0x32AC,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}