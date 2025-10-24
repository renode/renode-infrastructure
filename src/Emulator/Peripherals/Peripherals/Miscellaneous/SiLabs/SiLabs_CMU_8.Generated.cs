//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    CMU, Generated on : 2023-10-25 13:25:59.997409
    CMU, ID Version : 52bfa38ff76643fb8fa46c63ecf729ea.8 */

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
    public partial class SiLabs_CMU_8
    {
        public SiLabs_CMU_8(Machine machine) : base(machine)
        {
            SiLabs_CMU_8_constructor();
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
    public partial class SiLabs_CMU_8 : BasicDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_CMU_8(Machine machine) : base(machine)
        {
            Define_Registers();
            SiLabs_CMU_8_Constructor();
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
                {(long)Registers.Caltop, GenerateCaltopRegister()},
                {(long)Registers.Clken0, GenerateClken0Register()},
                {(long)Registers.Clken1, GenerateClken1Register()},
                {(long)Registers.Sysclkctrl, GenerateSysclkctrlRegister()},
                {(long)Registers.Traceclkctrl, GenerateTraceclkctrlRegister()},
                {(long)Registers.Exportclkctrl, GenerateExportclkctrlRegister()},
                {(long)Registers.Dpllrefclkctrl, GenerateDpllrefclkctrlRegister()},
                {(long)Registers.Em01grpaclkctrl, GenerateEm01grpaclkctrlRegister()},
                {(long)Registers.Em01grpcclkctrl, GenerateEm01grpcclkctrlRegister()},
                {(long)Registers.Em23grpaclkctrl, GenerateEm23grpaclkctrlRegister()},
                {(long)Registers.Em4grpaclkctrl, GenerateEm4grpaclkctrlRegister()},
                {(long)Registers.Adcclkctrl, GenerateAdcclkctrlRegister()},
                {(long)Registers.Wdog0clkctrl, GenerateWdog0clkctrlRegister()},
                {(long)Registers.Eusart0clkctrl, GenerateEusart0clkctrlRegister()},
                {(long)Registers.Sysrtc0clkctrl, GenerateSysrtc0clkctrlRegister()},
                {(long)Registers.Lcdclkctrl, GenerateLcdclkctrlRegister()},
                {(long)Registers.Pcnt0clkctrl, GeneratePcnt0clkctrlRegister()},
                {(long)Registers.Radioclkctrl, GenerateRadioclkctrlRegister()},
                {(long)Registers.Dapclkctrl, GenerateDapclkctrlRegister()},
                {(long)Registers.Qspisysclkctrl, GenerateQspisysclkctrlRegister()},
                {(long)Registers.Syxo0lfclkctrl, GenerateSyxo0lfclkctrlRegister()},
                {(long)Registers.Flpllrefclkctrl, GenerateFlpllrefclkctrlRegister()},
                {(long)Registers.Em01grpdclkctrl, GenerateEm01grpdclkctrlRegister()},
                {(long)Registers.I2c0clkctrl, GenerateI2c0clkctrlRegister()},
                {(long)Registers.Pixelrzclkctrl, GeneratePixelrzclkctrlRegister()},
                {(long)Registers.Clkenhv, GenerateClkenhvRegister()},
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
            PFMOSC = 12, // PFMOSC output is clocking CLKOUTHIDDEN. No PFMOSC in Rainier. tied zero
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
            TEMPOSC = 24, // TEMPOSC output is clocking CLKOUTHIDDEN
            CLKLPWEXP = 25, // CLKLPWEXP output is clocking CLKOUTHIDDEN. see the SRW_CMU.EXPORTCTRL register to select the actual export clock from LPW subsystem
            HFRCOLPW = 26, // HFRCOLPW is clocking CLKOUTHIDDEN
            CLKEXPQSPI = 27, // CLKEXPQSPI is clocking CLKOUTHIDDEN
        }
        
        protected enum CALCTRL_UPSEL
        {
            DISABLED = 0, // Up-counter is not clocked
            HCLK = 1, // HCLK is clocking up-counter
            PRS = 2, // PRS is clocking down-counter
            HFXO = 3, // HFXO is clocking up-counter
            LFXO = 4, // LFXO is clocking up-counter
            HFRCODPLL = 5, // HFRCODPLL is clocking up-counter
            HFRCOEM23 = 6, // HFRCOEM23 is clocking up-counter
            FSRCO = 7, // FSRCO is clocking up-counter
            LFRCO = 8, // LFRCO is clocking up-counter
            ULFRCO = 9, // ULFRCO is clocking up-counter
            HFRCOLPW = 10, // HFRCOLPW is clocking up-counter
            SOCPLL = 11, // SOCPLL is clocking up-counter
            PFMOSC = 12, // PFMOSC is clocking up-counter. No PFMOSC for Rainier. Tie zero
            BIASOSC = 13, // BIASOSC is clocking up-counter
            HFRCOSE = 14, // HFRCOSE is clocking up-counter
            TEMPOSC = 15, // TEMPOSC is clocking up-counter
            CLKOUTHIDDEN = 16, // CLKOUTHIDDEN is clocking up-counter
            CLKTST_LEDDRV = 17, // CLkTST_LEDDRV is clocking up-counter
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
            FSRCO = 7, // FSRCO is clocking down-counter
            LFRCO = 8, // LFRCO is clocking down-counter
            ULFRCO = 9, // ULFRCO is clocking down-counter
            HFRCOLPW = 10, // HFRCOLPW is clocking down-counter
            SOCPLL = 11, // SOCPLL is clocking down-counter
            PFMOSC = 12, // PFMOSC is clocking down-counter. No PFMOSC in Rainier. Tie Zero
            BIASOSC = 13, // BIASOSC is clocking down-counter
            HFRCOSE = 14, // HFRCOSE is clocking down-counter
            TEMPOSC = 15, // TEMPOSC is clocking down-counter
            CLKOUTHIDDEN = 16, // CLKOUTHIDDEN is clocking down-counter
            CLKTST_LEDDRV = 17, // CLkTST_LEDDRV is clocking down-counter
        }
        
        protected enum SYSCLKCTRL_CLKSEL
        {
            FSRCO = 1, // FSRCO is clocking SYSCLK
            HFRCODPLL = 2, // HFRCODPLL is clocking SYSCLK
            HFXO = 3, // HFXO is clocking SYSCLK
            CLKIN0 = 4, // CLKIN0 is clocking SYSCLK
            SOCPLL = 5, // SOCPLL is clocking SYSCLK
        }
        
        protected enum SYSCLKCTRL_LSPCLKPRESC
        {
            DIV2 = 0, // LSPCLK is PCLK divided by 2
            DIV1 = 1, // LSPCLK is PCLK divided by 1
            DIV4 = 3, // LSPCLK is PCLK divided by 4
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
        
        protected enum TRACECLKCTRL_CLKSEL
        {
            DISABLE = 0, // TRACE clock is disable
            SYSCLK = 1, // SYSCLK is driving TRACE
            HFRCOEM23 = 2, // HFRCOEM23 is driving TRACE
            HFRCODPLLRT = 3, // HFRCODPLLRT is driving TRACE
        }
        
        protected enum TRACECLKCTRL_PRESC
        {
            DIV1 = 0, // TRACECLK is selected clock source divided by 1
            DIV2 = 1, // TRACECLK is selected clock source divided by 2
            DIV3 = 2, // TRACECLK is SYSCLK divided by 3
            DIV4 = 3, // TRACECLK is selected clock source divided by 4
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
            SOCPLL = 10, // SOCPLL is clocking CLKOUT0
            CLKEXPQSPI = 11, // CLKEXPQSPI is clocking CLKOUT0
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
            SOCPLL = 10, // SOCPLL is clocking CLKOUT1
            CLKEXPQSPI = 11, // CLKEXPQSPI is clocking CLKOUT1
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
            SOCPLL = 10, // SOCPLL is clocking CLKOUT2
            CLKEXPQSPI = 11, // CLKEXPQSPI is clocking CLKOUT2
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
        
        protected enum ADCCLKCTRL_CLKSEL
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
        
        protected enum EUSART0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // EUSART0 is not clocked
            EM01GRPCCLK = 1, // EM01GRPCCLK is clocking EUSART0
            HFRCOEM23 = 2, // HFRCOEM23 is clocking EUSART0
            LFRCO = 3, // LFRCO is clocking EUSART0
            LFXO = 4, // LFXO is clocking EUSART0
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
        
        protected enum QSPISYSCLKCTRL_CLKSEL
        {
            DISABLED = 0, // QSPI is not clocked
            HFRCODPLL = 1, // HFRCO0DPLL retiming is clocking QSPI
            HFXO = 2, // HFXO retiming is clocking QSPI
            SOCPLL = 3, // SOCPLL is clocking QSPI
        }
        
        protected enum QSPISYSCLKCTRL_PRESC
        {
            DIV1 = 0, // QSPI system clock divided by 1
            DIV2 = 1, // QSPI system clock divided by 2
            DIV3 = 2, // QSPI system clock divided by 3
            DIV4 = 3, // QSPI system clock divided by 4
        }
        
        protected enum SYXO0LFCLKCTRL_CLKSEL
        {
            DISABLED = 0, // SYXO0 LF clock is not generated
            LFXO = 1, // LFXO is clocking for startup time measurement
            LFRCO = 2, // LFRCO is clocking for startup time measurement
        }
        
        protected enum FLPLLREFCLKCTRL_CLKSEL
        {
            DISABLE = 0, // FLPLLREF clock is disable
            HFRCODPLLRT = 1, // HFRCODPLL retiming clock is driving FLPLLREF
            CLKIN0 = 2, // CLKIN0 is driving FLPLLREF
        }
        
        protected enum FLPLLREFCLKCTRL_PRESC
        {
            DIV1 = 0, // FLPHY clock divided by 1
            DIV2 = 1, // FLPHY clock divided by 2
            DIV3 = 2, // FLPHY clock divided by 3
            DIV4 = 3, // FLPHY clock divided by 4
        }
        
        protected enum EM01GRPDCLKCTRL_CLKSEL
        {
            DISABLED = 0, // EM01GRPDCLK is not clocked
            HFRCODPLL = 1, // HFRCODPLL is clocking EM01GRPDCLK
            HFXO = 2, // HFXO is clocking EM01GRPDCLK
            FSRCO = 3, // FSRCO is clocking EM01GRPDCLK
            HFRCOEM23 = 4, // HFRCOEM23 is clocking EM01GRPDCLK
            HFRCODPLLRT = 5, // HFRCODPLL (retimed) is clocking EM01GRPDCLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
            HFXORT = 6, // HFXO (retimed) is clocking EM01GRPDCLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
        }
        
        protected enum I2C0CLKCTRL_CLKSEL
        {
            DISABLED = 0, // I2C0 is not clocked
            EM01GRPDCLK = 1, // EM01GRPCCLK is clocking I2C0
            HFRCOEM23 = 2, // HFRCOEM23 is clocking I2C0
            LFRCO = 3, // LFRCO is clocking I2C0
            LFXO = 4, // LFXO is clocking I2C0
        }
        
        protected enum PIXELRZCLKCTRL_PRESC
        {
            DIV1 = 0, // PIXELRZ clock divided by 1
            DIV2 = 1, // PIXELRZ clock divided by 2
            DIV3 = 2, // PIXELRZ clock divided by 3
            DIV4 = 3, // PIXELRZ clock divided by 4
            DIV10 = 9, // PIXELRZ clock divided by 10
            DIV16 = 15, // PIXELRZ clock divided by 16
        }
        
        protected enum PIXELRZCLKCTRL_CLKSEL
        {
            DISABLED = 0, // EM01GRPDCLK is not clocked
            HFRCODPLL = 1, // HFRCODPLL is clocking EM01GRPDCLK
            HFXO = 2, // HFXO is clocking EM01GRPDCLK
            FSRCO = 3, // FSRCO is clocking EM01GRPDCLK
            HFRCOEM23 = 4, // HFRCOEM23 is clocking EM01GRPDCLK
            HFRCODPLLRT = 5, // HFRCODPLL (retimed) is clocking EM01GRPDCLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
            HFXORT = 6, // HFXO (retimed) is clocking EM01GRPDCLK.  Check with datasheet for frequency limitation when using retiming with voltage scaling.
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x8)
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
        protected DoubleWordRegister  GenerateTestclkenRegister() => new DoubleWordRegister(this, 0x3C)
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
            .WithReservedBits(6, 24)
            .WithFlag(30, out testclken_chiptestctrl_bit, 
                    valueProviderCallback: (_) => {
                        Testclken_Chiptestctrl_ValueProvider(_);
                        return testclken_chiptestctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Testclken_Chiptestctrl_Write(_, __),
                    readCallback: (_, __) => Testclken_Chiptestctrl_Read(_, __),
                    name: "Chiptestctrl")
            .WithReservedBits(31, 1)
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
            .WithFlag(0, out calctrl_wrapup_bit, 
                    valueProviderCallback: (_) => {
                        Calctrl_Wrapup_ValueProvider(_);
                        return calctrl_wrapup_bit.Value;               
                    },
                    writeCallback: (_, __) => Calctrl_Wrapup_Write(_, __),
                    readCallback: (_, __) => Calctrl_Wrapup_Read(_, __),
                    name: "Wrapup")
            .WithReservedBits(1, 3)
            .WithFlag(4, out calctrl_cont_bit, 
                    valueProviderCallback: (_) => {
                        Calctrl_Cont_ValueProvider(_);
                        return calctrl_cont_bit.Value;               
                    },
                    writeCallback: (_, __) => Calctrl_Cont_Write(_, __),
                    readCallback: (_, __) => Calctrl_Cont_Read(_, __),
                    name: "Cont")
            .WithReservedBits(5, 3)
            .WithEnumField<DoubleWordRegister, CALCTRL_UPSEL>(8, 5, out calctrl_upsel_field, 
                    valueProviderCallback: (_) => {
                        Calctrl_Upsel_ValueProvider(_);
                        return calctrl_upsel_field.Value;               
                    },
                    writeCallback: (_, __) => Calctrl_Upsel_Write(_, __),
                    readCallback: (_, __) => Calctrl_Upsel_Read(_, __),
                    name: "Upsel")
            .WithReservedBits(13, 3)
            .WithEnumField<DoubleWordRegister, CALCTRL_DOWNSEL>(16, 5, out calctrl_downsel_field, 
                    valueProviderCallback: (_) => {
                        Calctrl_Downsel_ValueProvider(_);
                        return calctrl_downsel_field.Value;               
                    },
                    writeCallback: (_, __) => Calctrl_Downsel_Write(_, __),
                    readCallback: (_, __) => Calctrl_Downsel_Read(_, __),
                    name: "Downsel")
            .WithReservedBits(21, 11)
            .WithReadCallback((_, __) => Calctrl_Read(_, __))
            .WithWriteCallback((_, __) => Calctrl_Write(_, __));
        
        // Calcnt - Offset : 0x58
        protected DoubleWordRegister  GenerateCalcntRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 24, out calcnt_calcnt_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Calcnt_Calcnt_ValueProvider(_);
                        return calcnt_calcnt_field.Value;               
                    },
                    readCallback: (_, __) => Calcnt_Calcnt_Read(_, __),
                    name: "Calcnt")
            .WithReservedBits(24, 8)
            .WithReadCallback((_, __) => Calcnt_Read(_, __))
            .WithWriteCallback((_, __) => Calcnt_Write(_, __));
        
        // Caltop - Offset : 0x5C
        protected DoubleWordRegister  GenerateCaltopRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 24, out caltop_caltop_field, 
                    valueProviderCallback: (_) => {
                        Caltop_Caltop_ValueProvider(_);
                        return caltop_caltop_field.Value;               
                    },
                    writeCallback: (_, __) => Caltop_Caltop_Write(_, __),
                    readCallback: (_, __) => Caltop_Caltop_Read(_, __),
                    name: "Caltop")
            .WithReservedBits(24, 8)
            .WithReadCallback((_, __) => Caltop_Read(_, __))
            .WithWriteCallback((_, __) => Caltop_Write(_, __));
        
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
            .WithFlag(3, out clken0_gpcrc0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Gpcrc0_ValueProvider(_);
                        return clken0_gpcrc0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Gpcrc0_Write(_, __),
                    readCallback: (_, __) => Clken0_Gpcrc0_Read(_, __),
                    name: "Gpcrc0")
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
            .WithReservedBits(8, 2)
            .WithFlag(10, out clken0_adc0_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_Adc0_ValueProvider(_);
                        return clken0_adc0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_Adc0_Write(_, __),
                    readCallback: (_, __) => Clken0_Adc0_Read(_, __),
                    name: "Adc0")
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
            .WithFlag(25, out clken0_i2c2_bit, 
                    valueProviderCallback: (_) => {
                        Clken0_I2c2_ValueProvider(_);
                        return clken0_i2c2_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken0_I2c2_Write(_, __),
                    readCallback: (_, __) => Clken0_I2c2_Read(_, __),
                    name: "I2c2")
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
            .WithReservedBits(30, 2)
            .WithReadCallback((_, __) => Clken0_Read(_, __))
            .WithWriteCallback((_, __) => Clken0_Write(_, __));
        
        // Clken1 - Offset : 0x68
        protected DoubleWordRegister  GenerateClken1Register() => new DoubleWordRegister(this, 0x10000000)
            .WithFlag(0, out clken1_rpa_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Rpa_ValueProvider(_);
                        return clken1_rpa_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Rpa_Write(_, __),
                    readCallback: (_, __) => Clken1_Rpa_Read(_, __),
                    name: "Rpa")
            .WithFlag(1, out clken1_ksu_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Ksu_ValueProvider(_);
                        return clken1_ksu_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Ksu_Write(_, __),
                    readCallback: (_, __) => Clken1_Ksu_Read(_, __),
                    name: "Ksu")
            .WithFlag(2, out clken1_etampdet_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Etampdet_ValueProvider(_);
                        return clken1_etampdet_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Etampdet_Write(_, __),
                    readCallback: (_, __) => Clken1_Etampdet_Read(_, __),
                    name: "Etampdet")
            .WithFlag(3, out clken1_socpll0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Socpll0_ValueProvider(_);
                        return clken1_socpll0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Socpll0_Write(_, __),
                    readCallback: (_, __) => Clken1_Socpll0_Read(_, __),
                    name: "Socpll0")
            .WithFlag(4, out clken1_semaphore0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Semaphore0_ValueProvider(_);
                        return clken1_semaphore0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Semaphore0_Write(_, __),
                    readCallback: (_, __) => Clken1_Semaphore0_Read(_, __),
                    name: "Semaphore0")
            .WithFlag(5, out clken1_semaphore1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Semaphore1_ValueProvider(_);
                        return clken1_semaphore1_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Semaphore1_Write(_, __),
                    readCallback: (_, __) => Clken1_Semaphore1_Read(_, __),
                    name: "Semaphore1")
            .WithFlag(6, out clken1_leddrv0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Leddrv0_ValueProvider(_);
                        return clken1_leddrv0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Leddrv0_Write(_, __),
                    readCallback: (_, __) => Clken1_Leddrv0_Read(_, __),
                    name: "Leddrv0")
            .WithReservedBits(7, 3)
            .WithFlag(10, out clken1_semailboxhost_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Semailboxhost_ValueProvider(_);
                        return clken1_semailboxhost_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Semailboxhost_Write(_, __),
                    readCallback: (_, __) => Clken1_Semailboxhost_Read(_, __),
                    name: "Semailboxhost")
            .WithReservedBits(11, 1)
            .WithFlag(12, out clken1_l2icache0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_L2icache0_ValueProvider(_);
                        return clken1_l2icache0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_L2icache0_Write(_, __),
                    readCallback: (_, __) => Clken1_L2icache0_Read(_, __),
                    name: "L2icache0")
            .WithReservedBits(13, 1)
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
            .WithReservedBits(16, 1)
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
            .WithReservedBits(20, 1)
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
            .WithFlag(24, out clken1_eusart2_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Eusart2_ValueProvider(_);
                        return clken1_eusart2_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Eusart2_Write(_, __),
                    readCallback: (_, __) => Clken1_Eusart2_Read(_, __),
                    name: "Eusart2")
            .WithReservedBits(25, 2)
            .WithFlag(27, out clken1_dmem_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Dmem_ValueProvider(_);
                        return clken1_dmem_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Dmem_Write(_, __),
                    readCallback: (_, __) => Clken1_Dmem_Read(_, __),
                    name: "Dmem")
            .WithFlag(28, out clken1_devinfo_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Devinfo_ValueProvider(_);
                        return clken1_devinfo_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Devinfo_Write(_, __),
                    readCallback: (_, __) => Clken1_Devinfo_Read(_, __),
                    name: "Devinfo")
            .WithFlag(29, out clken1_pixelrz0_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Pixelrz0_ValueProvider(_);
                        return clken1_pixelrz0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Pixelrz0_Write(_, __),
                    readCallback: (_, __) => Clken1_Pixelrz0_Read(_, __),
                    name: "Pixelrz0")
            .WithFlag(30, out clken1_pixelrz1_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Pixelrz1_ValueProvider(_);
                        return clken1_pixelrz1_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Pixelrz1_Write(_, __),
                    readCallback: (_, __) => Clken1_Pixelrz1_Read(_, __),
                    name: "Pixelrz1")
            .WithFlag(31, out clken1_symcrypto_bit, 
                    valueProviderCallback: (_) => {
                        Clken1_Symcrypto_ValueProvider(_);
                        return clken1_symcrypto_bit.Value;               
                    },
                    writeCallback: (_, __) => Clken1_Symcrypto_Write(_, __),
                    readCallback: (_, __) => Clken1_Symcrypto_Read(_, __),
                    name: "Symcrypto")
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
            .WithEnumField<DoubleWordRegister, SYSCLKCTRL_LSPCLKPRESC>(8, 2, out sysclkctrl_lspclkpresc_field, 
                    valueProviderCallback: (_) => {
                        Sysclkctrl_Lspclkpresc_ValueProvider(_);
                        return sysclkctrl_lspclkpresc_field.Value;               
                    },
                    writeCallback: (_, __) => Sysclkctrl_Lspclkpresc_Write(_, __),
                    readCallback: (_, __) => Sysclkctrl_Lspclkpresc_Read(_, __),
                    name: "Lspclkpresc")
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
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Sysclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Sysclkctrl_Write(_, __));
        
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
            .WithEnumField<DoubleWordRegister, EXPORTCLKCTRL_CLKOUTSEL1>(4, 4, out exportclkctrl_clkoutsel1_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Clkoutsel1_ValueProvider(_);
                        return exportclkctrl_clkoutsel1_field.Value;               
                    },
                    writeCallback: (_, __) => Exportclkctrl_Clkoutsel1_Write(_, __),
                    readCallback: (_, __) => Exportclkctrl_Clkoutsel1_Read(_, __),
                    name: "Clkoutsel1")
            .WithEnumField<DoubleWordRegister, EXPORTCLKCTRL_CLKOUTSEL2>(8, 4, out exportclkctrl_clkoutsel2_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Clkoutsel2_ValueProvider(_);
                        return exportclkctrl_clkoutsel2_field.Value;               
                    },
                    writeCallback: (_, __) => Exportclkctrl_Clkoutsel2_Write(_, __),
                    readCallback: (_, __) => Exportclkctrl_Clkoutsel2_Read(_, __),
                    name: "Clkoutsel2")
            .WithReservedBits(12, 4)
            .WithValueField(16, 5, out exportclkctrl_presc_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Presc_ValueProvider(_);
                        return exportclkctrl_presc_field.Value;               
                    },
                    writeCallback: (_, __) => Exportclkctrl_Presc_Write(_, __),
                    readCallback: (_, __) => Exportclkctrl_Presc_Read(_, __),
                    name: "Presc")
            .WithReservedBits(21, 3)
            .WithValueField(24, 5, out exportclkctrl_clkoutpresc_field, 
                    valueProviderCallback: (_) => {
                        Exportclkctrl_Clkoutpresc_ValueProvider(_);
                        return exportclkctrl_clkoutpresc_field.Value;               
                    },
                    writeCallback: (_, __) => Exportclkctrl_Clkoutpresc_Write(_, __),
                    readCallback: (_, __) => Exportclkctrl_Clkoutpresc_Read(_, __),
                    name: "Clkoutpresc")
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
        
        // Adcclkctrl - Offset : 0x180
        protected DoubleWordRegister  GenerateAdcclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, ADCCLKCTRL_CLKSEL>(0, 2, out adcclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Adcclkctrl_Clksel_ValueProvider(_);
                        return adcclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Adcclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Adcclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Adcclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Adcclkctrl_Write(_, __));
        
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
        
        // Qspisysclkctrl - Offset : 0x298
        protected DoubleWordRegister  GenerateQspisysclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, QSPISYSCLKCTRL_CLKSEL>(0, 2, out qspisysclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Qspisysclkctrl_Clksel_ValueProvider(_);
                        return qspisysclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Qspisysclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Qspisysclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 6)
            .WithEnumField<DoubleWordRegister, QSPISYSCLKCTRL_PRESC>(8, 2, out qspisysclkctrl_presc_field, 
                    valueProviderCallback: (_) => {
                        Qspisysclkctrl_Presc_ValueProvider(_);
                        return qspisysclkctrl_presc_field.Value;               
                    },
                    writeCallback: (_, __) => Qspisysclkctrl_Presc_Write(_, __),
                    readCallback: (_, __) => Qspisysclkctrl_Presc_Read(_, __),
                    name: "Presc")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Qspisysclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Qspisysclkctrl_Write(_, __));
        
        // Syxo0lfclkctrl - Offset : 0x29C
        protected DoubleWordRegister  GenerateSyxo0lfclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, SYXO0LFCLKCTRL_CLKSEL>(0, 2, out syxo0lfclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Syxo0lfclkctrl_Clksel_ValueProvider(_);
                        return syxo0lfclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Syxo0lfclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Syxo0lfclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Syxo0lfclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Syxo0lfclkctrl_Write(_, __));
        
        // Flpllrefclkctrl - Offset : 0x2A4
        protected DoubleWordRegister  GenerateFlpllrefclkctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, FLPLLREFCLKCTRL_CLKSEL>(0, 2, out flpllrefclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Flpllrefclkctrl_Clksel_ValueProvider(_);
                        return flpllrefclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Flpllrefclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Flpllrefclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(2, 2)
            .WithEnumField<DoubleWordRegister, FLPLLREFCLKCTRL_PRESC>(4, 4, out flpllrefclkctrl_presc_field, 
                    valueProviderCallback: (_) => {
                        Flpllrefclkctrl_Presc_ValueProvider(_);
                        return flpllrefclkctrl_presc_field.Value;               
                    },
                    writeCallback: (_, __) => Flpllrefclkctrl_Presc_Write(_, __),
                    readCallback: (_, __) => Flpllrefclkctrl_Presc_Read(_, __),
                    name: "Presc")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Flpllrefclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Flpllrefclkctrl_Write(_, __));
        
        // Em01grpdclkctrl - Offset : 0x2A8
        protected DoubleWordRegister  GenerateEm01grpdclkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, EM01GRPDCLKCTRL_CLKSEL>(0, 3, out em01grpdclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Em01grpdclkctrl_Clksel_ValueProvider(_);
                        return em01grpdclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Em01grpdclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Em01grpdclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Em01grpdclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em01grpdclkctrl_Write(_, __));
        
        // I2c0clkctrl - Offset : 0x2AC
        protected DoubleWordRegister  GenerateI2c0clkctrlRegister() => new DoubleWordRegister(this, 0x1)
            .WithEnumField<DoubleWordRegister, I2C0CLKCTRL_CLKSEL>(0, 3, out i2c0clkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        I2c0clkctrl_Clksel_ValueProvider(_);
                        return i2c0clkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => I2c0clkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => I2c0clkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => I2c0clkctrl_Read(_, __))
            .WithWriteCallback((_, __) => I2c0clkctrl_Write(_, __));
        
        // Pixelrzclkctrl - Offset : 0x2B0
        protected DoubleWordRegister  GeneratePixelrzclkctrlRegister() => new DoubleWordRegister(this, 0x109)
            .WithEnumField<DoubleWordRegister, PIXELRZCLKCTRL_PRESC>(0, 6, out pixelrzclkctrl_presc_field, 
                    valueProviderCallback: (_) => {
                        Pixelrzclkctrl_Presc_ValueProvider(_);
                        return pixelrzclkctrl_presc_field.Value;               
                    },
                    writeCallback: (_, __) => Pixelrzclkctrl_Presc_Write(_, __),
                    readCallback: (_, __) => Pixelrzclkctrl_Presc_Read(_, __),
                    name: "Presc")
            .WithReservedBits(6, 2)
            .WithEnumField<DoubleWordRegister, PIXELRZCLKCTRL_CLKSEL>(8, 3, out pixelrzclkctrl_clksel_field, 
                    valueProviderCallback: (_) => {
                        Pixelrzclkctrl_Clksel_ValueProvider(_);
                        return pixelrzclkctrl_clksel_field.Value;               
                    },
                    writeCallback: (_, __) => Pixelrzclkctrl_Clksel_Write(_, __),
                    readCallback: (_, __) => Pixelrzclkctrl_Clksel_Read(_, __),
                    name: "Clksel")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Pixelrzclkctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pixelrzclkctrl_Write(_, __));
        
        // Clkenhv - Offset : 0x2B4
        protected DoubleWordRegister  GenerateClkenhvRegister() => new DoubleWordRegister(this, 0x10)
            .WithFlag(0, out clkenhv_sysrtc0_bit, 
                    valueProviderCallback: (_) => {
                        Clkenhv_Sysrtc0_ValueProvider(_);
                        return clkenhv_sysrtc0_bit.Value;               
                    },
                    writeCallback: (_, __) => Clkenhv_Sysrtc0_Write(_, __),
                    readCallback: (_, __) => Clkenhv_Sysrtc0_Read(_, __),
                    name: "Sysrtc0")
            .WithReservedBits(1, 1)
            .WithFlag(2, out clkenhv_scratchpad_bit, 
                    valueProviderCallback: (_) => {
                        Clkenhv_Scratchpad_ValueProvider(_);
                        return clkenhv_scratchpad_bit.Value;               
                    },
                    writeCallback: (_, __) => Clkenhv_Scratchpad_Write(_, __),
                    readCallback: (_, __) => Clkenhv_Scratchpad_Read(_, __),
                    name: "Scratchpad")
            .WithReservedBits(3, 1)
            .WithFlag(4, out clkenhv_slaxisysmb_bit, 
                    valueProviderCallback: (_) => {
                        Clkenhv_Slaxisysmb_ValueProvider(_);
                        return clkenhv_slaxisysmb_bit.Value;               
                    },
                    writeCallback: (_, __) => Clkenhv_Slaxisysmb_Write(_, __),
                    readCallback: (_, __) => Clkenhv_Slaxisysmb_Read(_, __),
                    name: "Slaxisysmb")
            .WithFlag(5, out clkenhv_hostportal_bit, 
                    valueProviderCallback: (_) => {
                        Clkenhv_Hostportal_ValueProvider(_);
                        return clkenhv_hostportal_bit.Value;               
                    },
                    writeCallback: (_, __) => Clkenhv_Hostportal_Write(_, __),
                    readCallback: (_, __) => Clkenhv_Hostportal_Read(_, __),
                    name: "Hostportal")
            .WithFlag(6, out clkenhv_lpw0portal_bit, 
                    valueProviderCallback: (_) => {
                        Clkenhv_Lpw0portal_ValueProvider(_);
                        return clkenhv_lpw0portal_bit.Value;               
                    },
                    writeCallback: (_, __) => Clkenhv_Lpw0portal_Write(_, __),
                    readCallback: (_, __) => Clkenhv_Lpw0portal_Read(_, __),
                    name: "Lpw0portal")
            .WithFlag(7, out clkenhv_seportal_bit, 
                    valueProviderCallback: (_) => {
                        Clkenhv_Seportal_ValueProvider(_);
                        return clkenhv_seportal_bit.Value;               
                    },
                    writeCallback: (_, __) => Clkenhv_Seportal_Write(_, __),
                    readCallback: (_, __) => Clkenhv_Seportal_Read(_, __),
                    name: "Seportal")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Clkenhv_Read(_, __))
            .WithWriteCallback((_, __) => Clkenhv_Write(_, __));
        
        // Rpuratd0_Drpu - Offset : 0x2B8
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
            .WithReservedBits(22, 1)
            .WithFlag(23, out rpuratd0_drpu_ratdcaltop_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdcaltop_ValueProvider(_);
                        return rpuratd0_drpu_ratdcaltop_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdcaltop_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdcaltop_Read(_, __),
                    name: "Ratdcaltop")
            .WithReservedBits(24, 1)
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
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => Rpuratd0_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd0_Drpu_Write(_, __));
        
        // Rpuratd1_Drpu - Offset : 0x2BC
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
        
        // Rpuratd2_Drpu - Offset : 0x2C0
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
        
        // Rpuratd3_Drpu - Offset : 0x2C4
        protected DoubleWordRegister  GenerateRpuratd3_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd3_drpu_ratdadcclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdadcclkctrl_ValueProvider(_);
                        return rpuratd3_drpu_ratdadcclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdadcclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdadcclkctrl_Read(_, __),
                    name: "Ratdadcclkctrl")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Rpuratd3_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd3_Drpu_Write(_, __));
        
        // Rpuratd4_Drpu - Offset : 0x2C8
        protected DoubleWordRegister  GenerateRpuratd4_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd4_drpu_ratdwdog0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdwdog0clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdwdog0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdwdog0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdwdog0clkctrl_Read(_, __),
                    name: "Ratdwdog0clkctrl")
            .WithReservedBits(1, 7)
            .WithFlag(8, out rpuratd4_drpu_ratdeusart0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd4_Drpu_Ratdeusart0clkctrl_ValueProvider(_);
                        return rpuratd4_drpu_ratdeusart0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd4_Drpu_Ratdeusart0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd4_Drpu_Ratdeusart0clkctrl_Read(_, __),
                    name: "Ratdeusart0clkctrl")
            .WithReservedBits(9, 7)
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
            .WithReservedBits(21, 7)
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
        
        // Rpuratd5_Drpu - Offset : 0x2CC
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
            .WithReservedBits(2, 4)
            .WithFlag(6, out rpuratd5_drpu_ratdqspisysclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdqspisysclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdqspisysclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdqspisysclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdqspisysclkctrl_Read(_, __),
                    name: "Ratdqspisysclkctrl")
            .WithFlag(7, out rpuratd5_drpu_ratdsyxo0lfclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdsyxo0lfclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdsyxo0lfclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdsyxo0lfclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdsyxo0lfclkctrl_Read(_, __),
                    name: "Ratdsyxo0lfclkctrl")
            .WithReservedBits(8, 1)
            .WithFlag(9, out rpuratd5_drpu_ratdflpllrefclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdflpllrefclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdflpllrefclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdflpllrefclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdflpllrefclkctrl_Read(_, __),
                    name: "Ratdflpllrefclkctrl")
            .WithFlag(10, out rpuratd5_drpu_ratdem01grpdclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdem01grpdclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdem01grpdclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdem01grpdclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdem01grpdclkctrl_Read(_, __),
                    name: "Ratdem01grpdclkctrl")
            .WithFlag(11, out rpuratd5_drpu_ratdi2c0clkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdi2c0clkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdi2c0clkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdi2c0clkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdi2c0clkctrl_Read(_, __),
                    name: "Ratdi2c0clkctrl")
            .WithFlag(12, out rpuratd5_drpu_ratdpixelrzclkctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdpixelrzclkctrl_ValueProvider(_);
                        return rpuratd5_drpu_ratdpixelrzclkctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdpixelrzclkctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdpixelrzclkctrl_Read(_, __),
                    name: "Ratdpixelrzclkctrl")
            .WithFlag(13, out rpuratd5_drpu_ratdclkenhv_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd5_Drpu_Ratdclkenhv_ValueProvider(_);
                        return rpuratd5_drpu_ratdclkenhv_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd5_Drpu_Ratdclkenhv_Write(_, __),
                    readCallback: (_, __) => Rpuratd5_Drpu_Ratdclkenhv_Read(_, __),
                    name: "Ratdclkenhv")
            .WithReservedBits(14, 18)
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
        protected IFlagRegisterField testclken_chiptestctrl_bit;
        partial void Testclken_Chiptestctrl_Write(bool a, bool b);
        partial void Testclken_Chiptestctrl_Read(bool a, bool b);
        partial void Testclken_Chiptestctrl_ValueProvider(bool a);

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
        protected IFlagRegisterField calctrl_wrapup_bit;
        partial void Calctrl_Wrapup_Write(bool a, bool b);
        partial void Calctrl_Wrapup_Read(bool a, bool b);
        partial void Calctrl_Wrapup_ValueProvider(bool a);
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
        
        // Caltop - Offset : 0x5C
        protected IValueRegisterField caltop_caltop_field;
        partial void Caltop_Caltop_Write(ulong a, ulong b);
        partial void Caltop_Caltop_Read(ulong a, ulong b);
        partial void Caltop_Caltop_ValueProvider(ulong a);

        partial void Caltop_Write(uint a, uint b);
        partial void Caltop_Read(uint a, uint b);
        
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
        protected IFlagRegisterField clken0_gpcrc0_bit;
        partial void Clken0_Gpcrc0_Write(bool a, bool b);
        partial void Clken0_Gpcrc0_Read(bool a, bool b);
        partial void Clken0_Gpcrc0_ValueProvider(bool a);
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
        protected IFlagRegisterField clken0_adc0_bit;
        partial void Clken0_Adc0_Write(bool a, bool b);
        partial void Clken0_Adc0_Read(bool a, bool b);
        partial void Clken0_Adc0_ValueProvider(bool a);
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
        protected IFlagRegisterField clken0_i2c2_bit;
        partial void Clken0_I2c2_Write(bool a, bool b);
        partial void Clken0_I2c2_Read(bool a, bool b);
        partial void Clken0_I2c2_ValueProvider(bool a);
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

        partial void Clken0_Write(uint a, uint b);
        partial void Clken0_Read(uint a, uint b);
        
        // Clken1 - Offset : 0x68
        protected IFlagRegisterField clken1_rpa_bit;
        partial void Clken1_Rpa_Write(bool a, bool b);
        partial void Clken1_Rpa_Read(bool a, bool b);
        partial void Clken1_Rpa_ValueProvider(bool a);
        protected IFlagRegisterField clken1_ksu_bit;
        partial void Clken1_Ksu_Write(bool a, bool b);
        partial void Clken1_Ksu_Read(bool a, bool b);
        partial void Clken1_Ksu_ValueProvider(bool a);
        protected IFlagRegisterField clken1_etampdet_bit;
        partial void Clken1_Etampdet_Write(bool a, bool b);
        partial void Clken1_Etampdet_Read(bool a, bool b);
        partial void Clken1_Etampdet_ValueProvider(bool a);
        protected IFlagRegisterField clken1_socpll0_bit;
        partial void Clken1_Socpll0_Write(bool a, bool b);
        partial void Clken1_Socpll0_Read(bool a, bool b);
        partial void Clken1_Socpll0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_semaphore0_bit;
        partial void Clken1_Semaphore0_Write(bool a, bool b);
        partial void Clken1_Semaphore0_Read(bool a, bool b);
        partial void Clken1_Semaphore0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_semaphore1_bit;
        partial void Clken1_Semaphore1_Write(bool a, bool b);
        partial void Clken1_Semaphore1_Read(bool a, bool b);
        partial void Clken1_Semaphore1_ValueProvider(bool a);
        protected IFlagRegisterField clken1_leddrv0_bit;
        partial void Clken1_Leddrv0_Write(bool a, bool b);
        partial void Clken1_Leddrv0_Read(bool a, bool b);
        partial void Clken1_Leddrv0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_semailboxhost_bit;
        partial void Clken1_Semailboxhost_Write(bool a, bool b);
        partial void Clken1_Semailboxhost_Read(bool a, bool b);
        partial void Clken1_Semailboxhost_ValueProvider(bool a);
        protected IFlagRegisterField clken1_l2icache0_bit;
        partial void Clken1_L2icache0_Write(bool a, bool b);
        partial void Clken1_L2icache0_Read(bool a, bool b);
        partial void Clken1_L2icache0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_smu_bit;
        partial void Clken1_Smu_Write(bool a, bool b);
        partial void Clken1_Smu_Read(bool a, bool b);
        partial void Clken1_Smu_ValueProvider(bool a);
        protected IFlagRegisterField clken1_icache0_bit;
        partial void Clken1_Icache0_Write(bool a, bool b);
        partial void Clken1_Icache0_Read(bool a, bool b);
        partial void Clken1_Icache0_ValueProvider(bool a);
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
        protected IFlagRegisterField clken1_eusart2_bit;
        partial void Clken1_Eusart2_Write(bool a, bool b);
        partial void Clken1_Eusart2_Read(bool a, bool b);
        partial void Clken1_Eusart2_ValueProvider(bool a);
        protected IFlagRegisterField clken1_dmem_bit;
        partial void Clken1_Dmem_Write(bool a, bool b);
        partial void Clken1_Dmem_Read(bool a, bool b);
        partial void Clken1_Dmem_ValueProvider(bool a);
        protected IFlagRegisterField clken1_devinfo_bit;
        partial void Clken1_Devinfo_Write(bool a, bool b);
        partial void Clken1_Devinfo_Read(bool a, bool b);
        partial void Clken1_Devinfo_ValueProvider(bool a);
        protected IFlagRegisterField clken1_pixelrz0_bit;
        partial void Clken1_Pixelrz0_Write(bool a, bool b);
        partial void Clken1_Pixelrz0_Read(bool a, bool b);
        partial void Clken1_Pixelrz0_ValueProvider(bool a);
        protected IFlagRegisterField clken1_pixelrz1_bit;
        partial void Clken1_Pixelrz1_Write(bool a, bool b);
        partial void Clken1_Pixelrz1_Read(bool a, bool b);
        partial void Clken1_Pixelrz1_ValueProvider(bool a);
        protected IFlagRegisterField clken1_symcrypto_bit;
        partial void Clken1_Symcrypto_Write(bool a, bool b);
        partial void Clken1_Symcrypto_Read(bool a, bool b);
        partial void Clken1_Symcrypto_ValueProvider(bool a);

        partial void Clken1_Write(uint a, uint b);
        partial void Clken1_Read(uint a, uint b);
        
        // Sysclkctrl - Offset : 0x70
        protected IEnumRegisterField<SYSCLKCTRL_CLKSEL> sysclkctrl_clksel_field;
        partial void Sysclkctrl_Clksel_Write(SYSCLKCTRL_CLKSEL a, SYSCLKCTRL_CLKSEL b);
        partial void Sysclkctrl_Clksel_Read(SYSCLKCTRL_CLKSEL a, SYSCLKCTRL_CLKSEL b);
        partial void Sysclkctrl_Clksel_ValueProvider(SYSCLKCTRL_CLKSEL a);
        protected IEnumRegisterField<SYSCLKCTRL_LSPCLKPRESC> sysclkctrl_lspclkpresc_field;
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

        partial void Sysclkctrl_Write(uint a, uint b);
        partial void Sysclkctrl_Read(uint a, uint b);
        
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
        protected IValueRegisterField exportclkctrl_clkoutpresc_field;
        partial void Exportclkctrl_Clkoutpresc_Write(ulong a, ulong b);
        partial void Exportclkctrl_Clkoutpresc_Read(ulong a, ulong b);
        partial void Exportclkctrl_Clkoutpresc_ValueProvider(ulong a);

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
        
        // Adcclkctrl - Offset : 0x180
        protected IEnumRegisterField<ADCCLKCTRL_CLKSEL> adcclkctrl_clksel_field;
        partial void Adcclkctrl_Clksel_Write(ADCCLKCTRL_CLKSEL a, ADCCLKCTRL_CLKSEL b);
        partial void Adcclkctrl_Clksel_Read(ADCCLKCTRL_CLKSEL a, ADCCLKCTRL_CLKSEL b);
        partial void Adcclkctrl_Clksel_ValueProvider(ADCCLKCTRL_CLKSEL a);

        partial void Adcclkctrl_Write(uint a, uint b);
        partial void Adcclkctrl_Read(uint a, uint b);
        
        // Wdog0clkctrl - Offset : 0x200
        protected IEnumRegisterField<WDOG0CLKCTRL_CLKSEL> wdog0clkctrl_clksel_field;
        partial void Wdog0clkctrl_Clksel_Write(WDOG0CLKCTRL_CLKSEL a, WDOG0CLKCTRL_CLKSEL b);
        partial void Wdog0clkctrl_Clksel_Read(WDOG0CLKCTRL_CLKSEL a, WDOG0CLKCTRL_CLKSEL b);
        partial void Wdog0clkctrl_Clksel_ValueProvider(WDOG0CLKCTRL_CLKSEL a);

        partial void Wdog0clkctrl_Write(uint a, uint b);
        partial void Wdog0clkctrl_Read(uint a, uint b);
        
        // Eusart0clkctrl - Offset : 0x220
        protected IEnumRegisterField<EUSART0CLKCTRL_CLKSEL> eusart0clkctrl_clksel_field;
        partial void Eusart0clkctrl_Clksel_Write(EUSART0CLKCTRL_CLKSEL a, EUSART0CLKCTRL_CLKSEL b);
        partial void Eusart0clkctrl_Clksel_Read(EUSART0CLKCTRL_CLKSEL a, EUSART0CLKCTRL_CLKSEL b);
        partial void Eusart0clkctrl_Clksel_ValueProvider(EUSART0CLKCTRL_CLKSEL a);

        partial void Eusart0clkctrl_Write(uint a, uint b);
        partial void Eusart0clkctrl_Read(uint a, uint b);
        
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
        
        // Qspisysclkctrl - Offset : 0x298
        protected IEnumRegisterField<QSPISYSCLKCTRL_CLKSEL> qspisysclkctrl_clksel_field;
        partial void Qspisysclkctrl_Clksel_Write(QSPISYSCLKCTRL_CLKSEL a, QSPISYSCLKCTRL_CLKSEL b);
        partial void Qspisysclkctrl_Clksel_Read(QSPISYSCLKCTRL_CLKSEL a, QSPISYSCLKCTRL_CLKSEL b);
        partial void Qspisysclkctrl_Clksel_ValueProvider(QSPISYSCLKCTRL_CLKSEL a);
        protected IEnumRegisterField<QSPISYSCLKCTRL_PRESC> qspisysclkctrl_presc_field;
        partial void Qspisysclkctrl_Presc_Write(QSPISYSCLKCTRL_PRESC a, QSPISYSCLKCTRL_PRESC b);
        partial void Qspisysclkctrl_Presc_Read(QSPISYSCLKCTRL_PRESC a, QSPISYSCLKCTRL_PRESC b);
        partial void Qspisysclkctrl_Presc_ValueProvider(QSPISYSCLKCTRL_PRESC a);

        partial void Qspisysclkctrl_Write(uint a, uint b);
        partial void Qspisysclkctrl_Read(uint a, uint b);
        
        // Syxo0lfclkctrl - Offset : 0x29C
        protected IEnumRegisterField<SYXO0LFCLKCTRL_CLKSEL> syxo0lfclkctrl_clksel_field;
        partial void Syxo0lfclkctrl_Clksel_Write(SYXO0LFCLKCTRL_CLKSEL a, SYXO0LFCLKCTRL_CLKSEL b);
        partial void Syxo0lfclkctrl_Clksel_Read(SYXO0LFCLKCTRL_CLKSEL a, SYXO0LFCLKCTRL_CLKSEL b);
        partial void Syxo0lfclkctrl_Clksel_ValueProvider(SYXO0LFCLKCTRL_CLKSEL a);

        partial void Syxo0lfclkctrl_Write(uint a, uint b);
        partial void Syxo0lfclkctrl_Read(uint a, uint b);
        
        // Flpllrefclkctrl - Offset : 0x2A4
        protected IEnumRegisterField<FLPLLREFCLKCTRL_CLKSEL> flpllrefclkctrl_clksel_field;
        partial void Flpllrefclkctrl_Clksel_Write(FLPLLREFCLKCTRL_CLKSEL a, FLPLLREFCLKCTRL_CLKSEL b);
        partial void Flpllrefclkctrl_Clksel_Read(FLPLLREFCLKCTRL_CLKSEL a, FLPLLREFCLKCTRL_CLKSEL b);
        partial void Flpllrefclkctrl_Clksel_ValueProvider(FLPLLREFCLKCTRL_CLKSEL a);
        protected IEnumRegisterField<FLPLLREFCLKCTRL_PRESC> flpllrefclkctrl_presc_field;
        partial void Flpllrefclkctrl_Presc_Write(FLPLLREFCLKCTRL_PRESC a, FLPLLREFCLKCTRL_PRESC b);
        partial void Flpllrefclkctrl_Presc_Read(FLPLLREFCLKCTRL_PRESC a, FLPLLREFCLKCTRL_PRESC b);
        partial void Flpllrefclkctrl_Presc_ValueProvider(FLPLLREFCLKCTRL_PRESC a);

        partial void Flpllrefclkctrl_Write(uint a, uint b);
        partial void Flpllrefclkctrl_Read(uint a, uint b);
        
        // Em01grpdclkctrl - Offset : 0x2A8
        protected IEnumRegisterField<EM01GRPDCLKCTRL_CLKSEL> em01grpdclkctrl_clksel_field;
        partial void Em01grpdclkctrl_Clksel_Write(EM01GRPDCLKCTRL_CLKSEL a, EM01GRPDCLKCTRL_CLKSEL b);
        partial void Em01grpdclkctrl_Clksel_Read(EM01GRPDCLKCTRL_CLKSEL a, EM01GRPDCLKCTRL_CLKSEL b);
        partial void Em01grpdclkctrl_Clksel_ValueProvider(EM01GRPDCLKCTRL_CLKSEL a);

        partial void Em01grpdclkctrl_Write(uint a, uint b);
        partial void Em01grpdclkctrl_Read(uint a, uint b);
        
        // I2c0clkctrl - Offset : 0x2AC
        protected IEnumRegisterField<I2C0CLKCTRL_CLKSEL> i2c0clkctrl_clksel_field;
        partial void I2c0clkctrl_Clksel_Write(I2C0CLKCTRL_CLKSEL a, I2C0CLKCTRL_CLKSEL b);
        partial void I2c0clkctrl_Clksel_Read(I2C0CLKCTRL_CLKSEL a, I2C0CLKCTRL_CLKSEL b);
        partial void I2c0clkctrl_Clksel_ValueProvider(I2C0CLKCTRL_CLKSEL a);

        partial void I2c0clkctrl_Write(uint a, uint b);
        partial void I2c0clkctrl_Read(uint a, uint b);
        
        // Pixelrzclkctrl - Offset : 0x2B0
        protected IEnumRegisterField<PIXELRZCLKCTRL_PRESC> pixelrzclkctrl_presc_field;
        partial void Pixelrzclkctrl_Presc_Write(PIXELRZCLKCTRL_PRESC a, PIXELRZCLKCTRL_PRESC b);
        partial void Pixelrzclkctrl_Presc_Read(PIXELRZCLKCTRL_PRESC a, PIXELRZCLKCTRL_PRESC b);
        partial void Pixelrzclkctrl_Presc_ValueProvider(PIXELRZCLKCTRL_PRESC a);
        protected IEnumRegisterField<PIXELRZCLKCTRL_CLKSEL> pixelrzclkctrl_clksel_field;
        partial void Pixelrzclkctrl_Clksel_Write(PIXELRZCLKCTRL_CLKSEL a, PIXELRZCLKCTRL_CLKSEL b);
        partial void Pixelrzclkctrl_Clksel_Read(PIXELRZCLKCTRL_CLKSEL a, PIXELRZCLKCTRL_CLKSEL b);
        partial void Pixelrzclkctrl_Clksel_ValueProvider(PIXELRZCLKCTRL_CLKSEL a);

        partial void Pixelrzclkctrl_Write(uint a, uint b);
        partial void Pixelrzclkctrl_Read(uint a, uint b);
        
        // Clkenhv - Offset : 0x2B4
        protected IFlagRegisterField clkenhv_sysrtc0_bit;
        partial void Clkenhv_Sysrtc0_Write(bool a, bool b);
        partial void Clkenhv_Sysrtc0_Read(bool a, bool b);
        partial void Clkenhv_Sysrtc0_ValueProvider(bool a);
        protected IFlagRegisterField clkenhv_scratchpad_bit;
        partial void Clkenhv_Scratchpad_Write(bool a, bool b);
        partial void Clkenhv_Scratchpad_Read(bool a, bool b);
        partial void Clkenhv_Scratchpad_ValueProvider(bool a);
        protected IFlagRegisterField clkenhv_slaxisysmb_bit;
        partial void Clkenhv_Slaxisysmb_Write(bool a, bool b);
        partial void Clkenhv_Slaxisysmb_Read(bool a, bool b);
        partial void Clkenhv_Slaxisysmb_ValueProvider(bool a);
        protected IFlagRegisterField clkenhv_hostportal_bit;
        partial void Clkenhv_Hostportal_Write(bool a, bool b);
        partial void Clkenhv_Hostportal_Read(bool a, bool b);
        partial void Clkenhv_Hostportal_ValueProvider(bool a);
        protected IFlagRegisterField clkenhv_lpw0portal_bit;
        partial void Clkenhv_Lpw0portal_Write(bool a, bool b);
        partial void Clkenhv_Lpw0portal_Read(bool a, bool b);
        partial void Clkenhv_Lpw0portal_ValueProvider(bool a);
        protected IFlagRegisterField clkenhv_seportal_bit;
        partial void Clkenhv_Seportal_Write(bool a, bool b);
        partial void Clkenhv_Seportal_Read(bool a, bool b);
        partial void Clkenhv_Seportal_ValueProvider(bool a);

        partial void Clkenhv_Write(uint a, uint b);
        partial void Clkenhv_Read(uint a, uint b);
        
        // Rpuratd0_Drpu - Offset : 0x2B8
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
        protected IFlagRegisterField rpuratd0_drpu_ratdcaltop_bit;
        partial void Rpuratd0_Drpu_Ratdcaltop_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcaltop_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcaltop_ValueProvider(bool a);
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

        partial void Rpuratd0_Drpu_Write(uint a, uint b);
        partial void Rpuratd0_Drpu_Read(uint a, uint b);
        
        // Rpuratd1_Drpu - Offset : 0x2BC
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
        
        // Rpuratd2_Drpu - Offset : 0x2C0
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
        
        // Rpuratd3_Drpu - Offset : 0x2C4
        protected IFlagRegisterField rpuratd3_drpu_ratdadcclkctrl_bit;
        partial void Rpuratd3_Drpu_Ratdadcclkctrl_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdadcclkctrl_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdadcclkctrl_ValueProvider(bool a);

        partial void Rpuratd3_Drpu_Write(uint a, uint b);
        partial void Rpuratd3_Drpu_Read(uint a, uint b);
        
        // Rpuratd4_Drpu - Offset : 0x2C8
        protected IFlagRegisterField rpuratd4_drpu_ratdwdog0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdwdog0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdwdog0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdwdog0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdeusart0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdeusart0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdeusart0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdeusart0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdsysrtc0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdsysrtc0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdsysrtc0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdsysrtc0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdlcdclkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdlcdclkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdlcdclkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdlcdclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd4_drpu_ratdpcnt0clkctrl_bit;
        partial void Rpuratd4_Drpu_Ratdpcnt0clkctrl_Write(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdpcnt0clkctrl_Read(bool a, bool b);
        partial void Rpuratd4_Drpu_Ratdpcnt0clkctrl_ValueProvider(bool a);

        partial void Rpuratd4_Drpu_Write(uint a, uint b);
        partial void Rpuratd4_Drpu_Read(uint a, uint b);
        
        // Rpuratd5_Drpu - Offset : 0x2CC
        protected IFlagRegisterField rpuratd5_drpu_ratdradioclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdradioclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdradioclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdradioclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratddapclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratddapclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratddapclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratddapclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdqspisysclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdqspisysclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdqspisysclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdqspisysclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdsyxo0lfclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdsyxo0lfclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdsyxo0lfclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdsyxo0lfclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdflpllrefclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdflpllrefclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdflpllrefclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdflpllrefclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdem01grpdclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdem01grpdclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdem01grpdclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdem01grpdclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdi2c0clkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdi2c0clkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdi2c0clkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdi2c0clkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdpixelrzclkctrl_bit;
        partial void Rpuratd5_Drpu_Ratdpixelrzclkctrl_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdpixelrzclkctrl_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdpixelrzclkctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd5_drpu_ratdclkenhv_bit;
        partial void Rpuratd5_Drpu_Ratdclkenhv_Write(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdclkenhv_Read(bool a, bool b);
        partial void Rpuratd5_Drpu_Ratdclkenhv_ValueProvider(bool a);

        partial void Rpuratd5_Drpu_Write(uint a, uint b);
        partial void Rpuratd5_Drpu_Read(uint a, uint b);
        
        partial void CMU_Reset();

        partial void SiLabs_CMU_8_Constructor();

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
            Caltop = 0x5C,
            Clken0 = 0x64,
            Clken1 = 0x68,
            Sysclkctrl = 0x70,
            Traceclkctrl = 0x80,
            Exportclkctrl = 0x90,
            Dpllrefclkctrl = 0x100,
            Em01grpaclkctrl = 0x120,
            Em01grpcclkctrl = 0x128,
            Em23grpaclkctrl = 0x140,
            Em4grpaclkctrl = 0x160,
            Adcclkctrl = 0x180,
            Wdog0clkctrl = 0x200,
            Eusart0clkctrl = 0x220,
            Sysrtc0clkctrl = 0x240,
            Lcdclkctrl = 0x250,
            Pcnt0clkctrl = 0x270,
            Radioclkctrl = 0x280,
            Dapclkctrl = 0x284,
            Qspisysclkctrl = 0x298,
            Syxo0lfclkctrl = 0x29C,
            Flpllrefclkctrl = 0x2A4,
            Em01grpdclkctrl = 0x2A8,
            I2c0clkctrl = 0x2AC,
            Pixelrzclkctrl = 0x2B0,
            Clkenhv = 0x2B4,
            Rpuratd0_Drpu = 0x2B8,
            Rpuratd1_Drpu = 0x2BC,
            Rpuratd2_Drpu = 0x2C0,
            Rpuratd3_Drpu = 0x2C4,
            Rpuratd4_Drpu = 0x2C8,
            Rpuratd5_Drpu = 0x2CC,
            
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
            Caltop_SET = 0x105C,
            Clken0_SET = 0x1064,
            Clken1_SET = 0x1068,
            Sysclkctrl_SET = 0x1070,
            Traceclkctrl_SET = 0x1080,
            Exportclkctrl_SET = 0x1090,
            Dpllrefclkctrl_SET = 0x1100,
            Em01grpaclkctrl_SET = 0x1120,
            Em01grpcclkctrl_SET = 0x1128,
            Em23grpaclkctrl_SET = 0x1140,
            Em4grpaclkctrl_SET = 0x1160,
            Adcclkctrl_SET = 0x1180,
            Wdog0clkctrl_SET = 0x1200,
            Eusart0clkctrl_SET = 0x1220,
            Sysrtc0clkctrl_SET = 0x1240,
            Lcdclkctrl_SET = 0x1250,
            Pcnt0clkctrl_SET = 0x1270,
            Radioclkctrl_SET = 0x1280,
            Dapclkctrl_SET = 0x1284,
            Qspisysclkctrl_SET = 0x1298,
            Syxo0lfclkctrl_SET = 0x129C,
            Flpllrefclkctrl_SET = 0x12A4,
            Em01grpdclkctrl_SET = 0x12A8,
            I2c0clkctrl_SET = 0x12AC,
            Pixelrzclkctrl_SET = 0x12B0,
            Clkenhv_SET = 0x12B4,
            Rpuratd0_Drpu_SET = 0x12B8,
            Rpuratd1_Drpu_SET = 0x12BC,
            Rpuratd2_Drpu_SET = 0x12C0,
            Rpuratd3_Drpu_SET = 0x12C4,
            Rpuratd4_Drpu_SET = 0x12C8,
            Rpuratd5_Drpu_SET = 0x12CC,
            
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
            Caltop_CLR = 0x205C,
            Clken0_CLR = 0x2064,
            Clken1_CLR = 0x2068,
            Sysclkctrl_CLR = 0x2070,
            Traceclkctrl_CLR = 0x2080,
            Exportclkctrl_CLR = 0x2090,
            Dpllrefclkctrl_CLR = 0x2100,
            Em01grpaclkctrl_CLR = 0x2120,
            Em01grpcclkctrl_CLR = 0x2128,
            Em23grpaclkctrl_CLR = 0x2140,
            Em4grpaclkctrl_CLR = 0x2160,
            Adcclkctrl_CLR = 0x2180,
            Wdog0clkctrl_CLR = 0x2200,
            Eusart0clkctrl_CLR = 0x2220,
            Sysrtc0clkctrl_CLR = 0x2240,
            Lcdclkctrl_CLR = 0x2250,
            Pcnt0clkctrl_CLR = 0x2270,
            Radioclkctrl_CLR = 0x2280,
            Dapclkctrl_CLR = 0x2284,
            Qspisysclkctrl_CLR = 0x2298,
            Syxo0lfclkctrl_CLR = 0x229C,
            Flpllrefclkctrl_CLR = 0x22A4,
            Em01grpdclkctrl_CLR = 0x22A8,
            I2c0clkctrl_CLR = 0x22AC,
            Pixelrzclkctrl_CLR = 0x22B0,
            Clkenhv_CLR = 0x22B4,
            Rpuratd0_Drpu_CLR = 0x22B8,
            Rpuratd1_Drpu_CLR = 0x22BC,
            Rpuratd2_Drpu_CLR = 0x22C0,
            Rpuratd3_Drpu_CLR = 0x22C4,
            Rpuratd4_Drpu_CLR = 0x22C8,
            Rpuratd5_Drpu_CLR = 0x22CC,
            
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
            Caltop_TGL = 0x305C,
            Clken0_TGL = 0x3064,
            Clken1_TGL = 0x3068,
            Sysclkctrl_TGL = 0x3070,
            Traceclkctrl_TGL = 0x3080,
            Exportclkctrl_TGL = 0x3090,
            Dpllrefclkctrl_TGL = 0x3100,
            Em01grpaclkctrl_TGL = 0x3120,
            Em01grpcclkctrl_TGL = 0x3128,
            Em23grpaclkctrl_TGL = 0x3140,
            Em4grpaclkctrl_TGL = 0x3160,
            Adcclkctrl_TGL = 0x3180,
            Wdog0clkctrl_TGL = 0x3200,
            Eusart0clkctrl_TGL = 0x3220,
            Sysrtc0clkctrl_TGL = 0x3240,
            Lcdclkctrl_TGL = 0x3250,
            Pcnt0clkctrl_TGL = 0x3270,
            Radioclkctrl_TGL = 0x3280,
            Dapclkctrl_TGL = 0x3284,
            Qspisysclkctrl_TGL = 0x3298,
            Syxo0lfclkctrl_TGL = 0x329C,
            Flpllrefclkctrl_TGL = 0x32A4,
            Em01grpdclkctrl_TGL = 0x32A8,
            I2c0clkctrl_TGL = 0x32AC,
            Pixelrzclkctrl_TGL = 0x32B0,
            Clkenhv_TGL = 0x32B4,
            Rpuratd0_Drpu_TGL = 0x32B8,
            Rpuratd1_Drpu_TGL = 0x32BC,
            Rpuratd2_Drpu_TGL = 0x32C0,
            Rpuratd3_Drpu_TGL = 0x32C4,
            Rpuratd4_Drpu_TGL = 0x32C8,
            Rpuratd5_Drpu_TGL = 0x32CC,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}