//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    HFXO, Generated on : 2023-10-25 13:34:08.616649
    HFXO, ID Version : dc00b03cfe6f4853848d5d69c970d617.5 */

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
    public partial class SiLabs_HFXO_5
    {
        public SiLabs_HFXO_5(Machine machine) : base(machine)
        {
            SiLabs_HFXO_5_constructor();
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
    public partial class SiLabs_HFXO_5 : BasicDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_HFXO_5(Machine machine) : base(machine)
        {
            Define_Registers();
            SiLabs_HFXO_5_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Trim, GenerateTrimRegister()},
                {(long)Registers.Xouttrim, GenerateXouttrimRegister()},
                {(long)Registers.Xtalcfg, GenerateXtalcfgRegister()},
                {(long)Registers.Xtalctrl, GenerateXtalctrlRegister()},
                {(long)Registers.Xtalctrl1, GenerateXtalctrl1Register()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Sleepyxtalcfg0, GenerateSleepyxtalcfg0Register()},
                {(long)Registers.Sleepyxtalcfg1, GenerateSleepyxtalcfg1Register()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Pkdetctrl1, GeneratePkdetctrl1Register()},
                {(long)Registers.Lowpwrctrl, GenerateLowpwrctrlRegister()},
                {(long)Registers.Pkdetctrl, GeneratePkdetctrlRegister()},
                {(long)Registers.Extclkpkdetctrl, GenerateExtclkpkdetctrlRegister()},
                {(long)Registers.Internalctrl, GenerateInternalctrlRegister()},
                {(long)Registers.Internalxoutctrl, GenerateInternalxoutctrlRegister()},
                {(long)Registers.Bufouttrim, GenerateBufouttrimRegister()},
                {(long)Registers.Bufoutctrl, GenerateBufoutctrlRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Avgstartuptime, GenerateAvgstartuptimeRegister()},
                {(long)Registers.Dbgctrl, GenerateDbgctrlRegister()},
                {(long)Registers.Dbgstatus, GenerateDbgstatusRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Rfcfg_Xoinjhwseq, GenerateRfcfg_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp0_Xoinjhwseq, GenerateSyvcocaptemp0_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp1_Xoinjhwseq, GenerateSyvcocaptemp1_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp2_Xoinjhwseq, GenerateSyvcocaptemp2_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp3_Xoinjhwseq, GenerateSyvcocaptemp3_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp4_Xoinjhwseq, GenerateSyvcocaptemp4_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp5_Xoinjhwseq, GenerateSyvcocaptemp5_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp6_Xoinjhwseq, GenerateSyvcocaptemp6_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp7_Xoinjhwseq, GenerateSyvcocaptemp7_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp8_Xoinjhwseq, GenerateSyvcocaptemp8_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp9_Xoinjhwseq, GenerateSyvcocaptemp9_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp10_Xoinjhwseq, GenerateSyvcocaptemp10_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp11_Xoinjhwseq, GenerateSyvcocaptemp11_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp12_Xoinjhwseq, GenerateSyvcocaptemp12_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp13_Xoinjhwseq, GenerateSyvcocaptemp13_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp14_Xoinjhwseq, GenerateSyvcocaptemp14_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp15_Xoinjhwseq, GenerateSyvcocaptemp15_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp16_Xoinjhwseq, GenerateSyvcocaptemp16_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp17_Xoinjhwseq, GenerateSyvcocaptemp17_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp18_Xoinjhwseq, GenerateSyvcocaptemp18_xoinjhwseqRegister()},
                {(long)Registers.Syvcocaptemp19_Xoinjhwseq, GenerateSyvcocaptemp19_xoinjhwseqRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            HFXO_Reset();
        }
        
        protected enum XTALCFG_TIMEOUTSTEADY
        {
            T4US = 0, // The steady state timeout is set to 4 us minimum.  The maximum can be +40%.
            T16US = 1, // The steady state timeout is set to 16 us minimum.  The maximum can be +40%.
            T41US = 2, // The steady state timeout is set to 41 us minimum.  The maximum can be +40%.
            T83US = 3, // The steady state timeout is set to 83 us minimum.  The maximum can be +40%.
            T125US = 4, // The steady state timeout is set to 125 us minimum.  The maximum can be +40%.
            T166US = 5, // The steady state timeout is set to 166 us minimum.  The maximum can be +40%.
            T208US = 6, // The steady state timeout is set to 208 us minimum.  The maximum can be +40%.
            T250US = 7, // The steady state timeout is set to 250 us minimum.  The maximum can be +40%.
            T333US = 8, // The steady state timeout is set to 333 us minimum.  The maximum can be +40%.
            T416US = 9, // The steady state timeout is set to 416 us minimum.  The maximum can be +40%.
            T500US = 10, // The steady state timeout is set to 500 us minimum.  The maximum can be +40%.
            T666US = 11, // The steady state timeout is set to 666 us minimum.  The maximum can be +40%.
            T833US = 12, // The steady state timeout is set to 833 us minimum.  The maximum can be +40%.
            T1666US = 13, // The steady state timeout is set to 1666 us minimum.  The maximum can be +40%.
            T2500US = 14, // The steady state timeout is set to 2500 us minimum.  The maximum can be +40%.
            T4166US = 15, // The steady state timeout is set to 4166 us minimum.  The maximum can be +40%.
        }
        
        protected enum XTALCFG_TIMEOUTCBLSB
        {
            T8US = 0, // The core bias LSB change timeout is set to 8 us minimum.  The maximum can be +40%.
            T20US = 1, // The core bias LSB change timeout is set to 20 us minimum.  The maximum can be +40%.
            T41US = 2, // The core bias LSB change timeout is set to 41 us minimum.  The maximum can be +40%.
            T62US = 3, // The core bias LSB change timeout is set to 62 us minimum.  The maximum can be +40%.
            T83US = 4, // The core bias LSB change timeout is set to 83 us minimum.  The maximum can be +40%.
            T104US = 5, // The core bias LSB change timeout is set to 104 us minimum.  The maximum can be +40%.
            T125US = 6, // The core bias LSB change timeout is set to 125 us minimum.  The maximum can be +40%.
            T166US = 7, // The core bias LSB change timeout is set to 166 us minimum.  The maximum can be +40%.
            T208US = 8, // The core bias LSB change timeout is set to 208 us minimum.  The maximum can be +40%.
            T250US = 9, // The core bias LSB change timeout is set to 250 us minimum.  The maximum can be +40%.
            T333US = 10, // The core bias LSB change timeout is set to 333 us minimum.  The maximum can be +40%.
            T416US = 11, // The core bias LSB change timeout is set to 416 us minimum.  The maximum can be +40%.
            T833US = 12, // The core bias LSB change timeout is set to 833 us minimum.  The maximum can be +40%.
            T1250US = 13, // The core bias LSB change timeout is set to 1250 us minimum.  The maximum can be +40%.
            T2083US = 14, // The core bias LSB change timeout is set to 2083 us minimum.  The maximum can be +40%.
            T3750US = 15, // The core bias LSB change timeout is set to 3750 us minimum.  The maximum can be +40%.
        }
        
        protected enum XTALCFG_TINJ
        {
            T10CYCLES = 0, // Clock injection for 10 xtal cycles (0.5us)
            T20CYCLES = 1, // Clock injection for 20 xtal cycles (1us)
            T30CYCLES = 2, // Clock injection for 30 xtal cycles (1.5us)
            T40CYCLES = 3, // Clock injection for 40 xtal cycles (2us)
            T50CYCLES = 4, // Clock injection for 50 xtal cycles (2.5us)
            T60CYCLES = 5, // Clock injection for 60 xtal cycles (3us)
            T70CYCLES = 6, // Clock injection for 70 xtal cycles (3.5us)
            T80CYCLES = 7, // Clock injection for 80 xtal cycles (4us)
            T90CYCLES = 8, // Clock injection for 90 xtal cycles (4.5us)
            T100CYCLES = 9, // Clock injection for 100 xtal cycles (5us)
            T110CYCLES = 10, // Clock injection for 110 xtal cycles (5.5us)
            T120CYCLES = 11, // Clock injection for 120 xtal cycles (6us)
            T130CYCLES = 12, // Clock injection for 130 xtal cycles (6.5us)
            T140CYCLES = 13, // Clock injection for 140 xtal cycles (7us)
            T180CYCLES = 14, // Clock injection for 180 xtal cycles (9us)
            T210CYCLES = 15, // Clock injection for 210 xtal cycles (10.5us)
        }
        
        protected enum XTALCTRL_CTUNEFIXANA
        {
            NONE = 0, // Remove fixed capacitance on XO node
            SEL0 = 1, // Adds fixed capacitance of 1.31pF on XO node
            SEL1 = 2, // Adds fixed capacitance of 2.62pF on XO node
            SEL2 = 3, // Adds fixed capacitance of 3.93pF on XO node
        }
        
        protected enum CFG_MODE
        {
            XTAL = 0, // crystal oscillator
            EXTCLK = 1, // external sinusoidal clock can be supplied on XI pin.
            EXTCLKPKDET = 2, // external sinusoidal clock can be supplied on XI pin (peak detector used).
        }
        
        protected enum CFG_NUMSTUPMEAS
        {
            MEASURE1 = 0, // Measure and average over 1 startup time samples
            MEASURE8 = 1, // Measure and average over 8 startup time samples
            MEASURE16 = 2, // Measure and average over 16 startup time samples
            NA = 3, // Measure and average over 16 startup time samples
        }
        
        protected enum CTRL_FORCEXI2GNDANA
        {
            DISABLE = 0, // Disabled (not pulled)
            ENABLE = 1, // Enabled (pulled)
        }
        
        protected enum CTRL_FORCEXO2GNDANA
        {
            DISABLE = 0, // Disabled (not pulled)
            ENABLE = 1, // Enabled (pulled)
        }
        
        protected enum CTRL_PRSSTATUSSEL0
        {
            DISABLED = 0, // PRS mux outputs 0
            ENS = 1, // PRS mux outputs enabled status
            COREBIASOPTRDY = 2, // PRS mux outputs core bias optimization ready status
            RDY = 3, // PRS mux outputs ready status
            PRSRDY = 4, // PRS mux outputs PRS ready status
            SYSRTCRDY = 5, // PRS mux outputs SYSRTC ready status
            BUFOUTRDY = 6, // PRS mux outputs BUFOUT ready status
            CLKINTRA0RDY = 7, // PRS mux outputs intra-clock 0 ready status
            CLKINTRA1RDY = 8, // PRS mux outputs intra-clock 1 ready status
            HWREQ = 9, // PRS mux outputs oscillator requested by digital clock status
            PRSHWREQ = 10, // PRS mux outputs oscillator requested by PRS request status
            SYSRTCHWREQ = 11, // PRS mux outputs oscillator requested by SYSRTC request status
            BUFOUTHWREQ = 12, // PRS mux outputs oscillator requested by BUFOUT request status
            CLKINTRA0HWREQ = 13, // PRS mux outputs oscillator requested by intra-clock 0 request status
            CLKINTRA1HWREQ = 14, // PRS mux outputs oscillator requested by intra-clock 1 request status
        }
        
        protected enum CTRL_PRSSTATUSSEL1
        {
            DISABLED = 0, // PRS mux outputs 0
            ENS = 1, // PRS mux outputs enabled status
            COREBIASOPTRDY = 2, // PRS mux outputs core bias optimization ready status
            RDY = 3, // PRS mux outputs ready status
            PRSRDY = 4, // PRS mux outputs PRS ready status
            SYSRTCRDY = 5, // PRS mux outputs SYSRTC ready status
            BUFOUTRDY = 6, // PRS mux outputs BUFOUT ready status
            CLKINTRA0RDY = 7, // PRS mux outputs intra-clock 0 ready status
            CLKINTRA1RDY = 8, // PRS mux outputs intra-clock 1 ready status
            HWREQ = 9, // PRS mux outputs oscillator requested by digital clock status
            PRSHWREQ = 10, // PRS mux outputs oscillator requested by PRS request status
            SYSRTCHWREQ = 11, // PRS mux outputs oscillator requested by SYSRTC request status
            BUFOUTHWREQ = 12, // PRS mux outputs oscillator requested by BUFOUT request status
            CLKINTRA0HWREQ = 13, // PRS mux outputs oscillator requested by intra-clock 0 request status
            CLKINTRA1HWREQ = 14, // PRS mux outputs oscillator requested by intra-clock 1 request status
        }
        
        protected enum LOWPWRCTRL_SQBUFBIASRESANA
        {
            R13K = 0, // 
            R20K = 1, // 
            R40K = 2, // 
            R80K = 3, // 
        }
        
        protected enum LOWPWRCTRL_SQBUFBIASANA
        {
            I20UA = 0, // 
            I40UA = 1, // 
            I60UA = 2, // 
            I80UA = 3, // 
            I100UA = 4, // 
            I120UA = 5, // 
            I140UA = 6, // 
            I160UA = 7, // 
        }
        
        protected enum LOWPWRCTRL_TIMEOUTWARM
        {
            T0US = 0, // The keep-warm startup timeout is set to 0 us minimum.  The maximum can be +40%.
            T8US = 1, // The keep-warm startup timeout is set to 8 us minimum.  The maximum can be +40%.
            T17US = 2, // The keep-warm startup timeout is set to 17 us minimum.  The maximum can be +40%.
            T25US = 3, // The keep-warm startup timeout is set to 25 us minimum.  The maximum can be +40%.
            T42US = 4, // The keep-warm startup timeout is set to 42 us minimum.  The maximum can be +40%.
            T58US = 5, // The keep-warm startup timeout is set to 58 us minimum.  The maximum can be +40%.
            T83US = 6, // The keep-warm startup timeout is set to 83 us minimum.  The maximum can be +40%.
            T125US = 7, // The keep-warm startup timeout is set to 125 us minimum.  The maximum can be +40%.
        }
        
        protected enum LOWPWRCTRL_SHUNTBIASANALP
        {
            I20UA = 0, // 
            I30UA = 1, // 
            I40UA = 2, // 
            I50UA = 3, // 
            I60UA = 4, // 
            I70UA = 5, // 
            I80UA = 6, // 
            I90UA = 7, // 
            I100UA = 8, // 
            I110UA = 9, // 
            I120UA = 10, // 
            I130UA = 11, // 
            I140UA = 12, // 
            I150UA = 13, // 
            I160UA = 14, // 
            I170UA = 15, // 
        }
        
        protected enum LOWPWRCTRL_SHUNTBIASANAHP
        {
            I20UA = 0, // 
            I30UA = 1, // 
            I40UA = 2, // 
            I50UA = 3, // 
            I60UA = 4, // 
            I70UA = 5, // 
            I80UA = 6, // 
            I90UA = 7, // 
            I100UA = 8, // 
            I110UA = 9, // 
            I120UA = 10, // 
            I130UA = 11, // 
            I140UA = 12, // 
            I150UA = 13, // 
            I160UA = 14, // 
            I170UA = 15, // 
        }
        
        protected enum PKDETCTRL_PKDETNODEANA
        {
            XI = 0, // Apply peak detection to XI node
            XO = 1, // Apply peak detection to XO node
        }
        
        protected enum PKDETCTRL_PKDETNODESTARTUPI
        {
            XI = 0, // Apply peak detection to XI node
            XO = 1, // Apply peak detection to XO node
        }
        
        protected enum PKDETCTRL_PKDETNODESTARTUP
        {
            XI = 0, // Apply peak detection to XI node
            XO = 1, // Apply peak detection to XO node
        }
        
        protected enum PKDETCTRL_PKDETTHANA
        {
            TH133mV = 0, // 133.3mV
            TH165mV = 1, // 165.5mV
            TH197mV = 2, // 197.4mV
            TH229mV = 3, // 229.1mV
            TH260mV = 4, // 260.9mV
            TH292mV = 5, // 292.5mV
            TH1324V = 6, // 324.5mV
            TH357mV = 7, // 357.1mV
            TH390mV = 8, // 390.2mV
            TH422mV = 9, // 422.4mV
            TH454mV = 10, // 454.6mV
            TH488mV = 11, // 488.6mV
            TH524mV = 12, // 524.8mV
            TH562mV = 13, // 562.6mV
            TH602mV = 14, // 602.5mV
            TH643mV = 15, // 643.2mV
        }
        
        protected enum PKDETCTRL_PKDETTHSTARTUPI
        {
            TH133mV = 0, // 133.3mV
            TH165mV = 1, // 165.5mV
            TH197mV = 2, // 197.4mV
            TH229mV = 3, // 229.1mV
            TH260mV = 4, // 260.9mV
            TH292mV = 5, // 292.5mV
            TH1324V = 6, // 324.5mV
            TH357mV = 7, // 357.1mV
            TH390mV = 8, // 390.2mV
            TH422mV = 9, // 422.4mV
            TH454mV = 10, // 454.6mV
            TH488mV = 11, // 488.6mV
            TH524mV = 12, // 524.8mV
            TH562mV = 13, // 562.6mV
            TH602mV = 14, // 602.5mV
            TH643mV = 15, // 643.2mV
        }
        
        protected enum PKDETCTRL_PKDETTHSTARTUP
        {
            TH133mV = 0, // 133.3mV
            TH165mV = 1, // 165.5mV
            TH197mV = 2, // 197.4mV
            TH229mV = 3, // 229.1mV
            TH260mV = 4, // 260.9mV
            TH292mV = 5, // 292.5mV
            TH1324V = 6, // 324.5mV
            TH357mV = 7, // 357.1mV
            TH390mV = 8, // 390.2mV
            TH422mV = 9, // 422.4mV
            TH454mV = 10, // 454.6mV
            TH488mV = 11, // 488.6mV
            TH524mV = 12, // 524.8mV
            TH562mV = 13, // 562.6mV
            TH602mV = 14, // 602.5mV
            TH643mV = 15, // 643.2mV
        }
        
        protected enum PKDETCTRL_PKDETTHHIGH
        {
            TH133mV = 0, // 133.3mV
            TH165mV = 1, // 165.5mV
            TH197mV = 2, // 197.4mV
            TH229mV = 3, // 229.1mV
            TH260mV = 4, // 260.9mV
            TH292mV = 5, // 292.5mV
            TH1324V = 6, // 324.5mV
            TH357mV = 7, // 357.1mV
            TH390mV = 8, // 390.2mV
            TH422mV = 9, // 422.4mV
            TH454mV = 10, // 454.6mV
            TH488mV = 11, // 488.6mV
            TH524mV = 12, // 524.8mV
            TH562mV = 13, // 562.6mV
            TH602mV = 14, // 602.5mV
            TH643mV = 15, // 643.2mV
        }
        
        protected enum PKDETCTRL_TIMEOUTPKDET
        {
            T6US = 0, // The peak detector timeout is set to 6 us minimum.  The maximum can be +40%.
            T8US = 1, // The peak detector timeout is set to 8 us minimum.  The maximum can be +40%.
            T12US = 2, // The peak detector timeout is set to 12 us minimum.  The maximum can be +40%.
            T20US = 3, // The peak detector timeout is set to 20 us minimum.  The maximum can be +40%.
        }
        
        protected enum PKDETCTRL_REGLVLANA
        {
            REGTRIMVREG0 = 0, // Select REGTRIMVREG0 during steady state
            REGTRIMVREG1 = 1, // Select REGTRIMVREG1 during steady state
        }
        
        protected enum PKDETCTRL_REGLVLSTARTUP
        {
            REGTRIMVREG0 = 0, // Select REGTRIMVREG0 during startup phase
            REGTRIMVREG1 = 1, // Select REGTRIMVREG1 during startup phase
        }
        
        protected enum INTERNALCTRL_SQBUFFILTANA
        {
            BYPASS = 0, // 
            FILT1 = 1, // 
            FILT2 = 2, // 
            FILT3 = 3, // 
        }
        
        protected enum INTERNALCTRL_VTRCOREDISSTARTUPANA
        {
            OFF = 0, // 
            DISABLE = 1, // 
        }
        
        protected enum BUFOUTCTRL_TIMEOUTCTUNE
        {
            T2US = 0, // The tuning cap change timeout is set to 2 us minimum.  The maximum can be +40%.
            T5US = 1, // The tuning cap change timeout is set to 5 us minimum.  The maximum can be +40%.
            T10US = 2, // The tuning cap change timeout is set to 10 us minimum.  The maximum can be +40%.
            T16US = 3, // The tuning cap change timeout is set to 16 us minimum.  The maximum can be +40%.
            T21US = 4, // The tuning cap change timeout is set to 21 us minimum.  The maximum can be +40%.
            T26US = 5, // The tuning cap change timeout is set to 26 us minimum.  The maximum can be +40%.
            T31US = 6, // The tuning cap change timeout is set to 31 us minimum.  The maximum can be +40%.
            T42US = 7, // The tuning cap change timeout is set to 42 us minimum.  The maximum can be +40%.
            T52US = 8, // The tuning cap change timeout is set to 52 us minimum.  The maximum can be +40%.
            T63US = 9, // The tuning cap change timeout is set to 63 us minimum.  The maximum can be +40%.
            T83US = 10, // The tuning cap change timeout is set to 83 us minimum.  The maximum can be +40%.
            T104US = 11, // The tuning cap change timeout is set to 104 us minimum.  The maximum can be +40%.
            T208US = 12, // The tuning cap change timeout is set to 208 us minimum.  The maximum can be +40%.
            T313US = 13, // The tuning cap change timeout is set to 313 us minimum.  The maximum can be +40%.
            T521US = 14, // The tuning cap change timeout is set to 521 us minimum.  The maximum can be +40%.
            T938US = 15, // The tuning cap change timeout is set to 938 us minimum.  The maximum can be +40%.
        }
        
        protected enum BUFOUTCTRL_TIMEOUTSTARTUP
        {
            T42US = 0, // The oscillator startup timeout is set to 42 us minimum.  The maximum can be +40%.
            T83US = 1, // The oscillator startup timeout is set to 83 us minimum.  The maximum can be +40%.
            T108US = 2, // The oscillator startup timeout is set to 108 us minimum.  The maximum can be +40%.
            T133US = 3, // The oscillator startup timeout is set to 133 us minimum.  The maximum can be +40%.
            T158US = 4, // The oscillator startup timeout is set to 158 us minimum.  The maximum can be +40%.
            T183US = 5, // The oscillator startup timeout is set to 183 us minimum.  The maximum can be +40%.
            T208US = 6, // The oscillator startup timeout is set to 208 us minimum.  The maximum can be +40%.
            T233US = 7, // The oscillator startup timeout is set to 233 us minimum.  The maximum can be +40%.
            T258US = 8, // The oscillator startup timeout is set to 258 us minimum.  The maximum can be +40%.
            T283US = 9, // The oscillator startup timeout is set to 283 us minimum.  The maximum can be +40%.
            T333US = 10, // The oscillator startup timeout is set to 333 us minimum.  The maximum can be +40%.
            T375US = 11, // The oscillator startup timeout is set to 375 us minimum.  The maximum can be +40%.
            T417US = 12, // The oscillator startup timeout is set to 417 us minimum.  The maximum can be +40%.
            T458US = 13, // The oscillator startup timeout is set to 458 us minimum.  The maximum can be +40%.
            T500US = 14, // The oscillator startup timeout is set to 500 us minimum.  The maximum can be +40%.
            T667US = 15, // The oscillator startup timeout is set to 667 us minimum.  The maximum can be +40%.
        }
        
        protected enum STATUS_LOCK
        {
            UNLOCKED = 0, // Configuration lock is unlocked
            LOCKED = 1, // Configuration lock is locked
        }
        
        protected enum DBGCTRL_PRSDBGSEL0
        {
            DISABLED = 0, // PRS mux outputs 0
            ENCORE = 1, // PRS mux outputs en_core
            ENSQBUF = 2, // PRS mux outputs en_sqbuf
            ENHIGHGMMODE = 3, // PRS mux outputs en_high_gm_mode
            ENINJ = 4, // PRS mux outputs en_inj
            INJCLK = 5, // PRS mux outputs injection clock
            PKDETSTATUS = 6, // PRS mux outputs pkdet_status
            XOUTPKDETSTATUS = 7, // PRS mux outputs XOUT pkdet_status
        }
        
        protected enum DBGCTRL_PRSDBGSEL1
        {
            DISABLED = 0, // PRS mux outputs 0
            ENCORE = 1, // PRS mux outputs en_core
            ENSQBUF = 2, // PRS mux outputs en_sqbuf
            ENHIGHGMMODE = 3, // PRS mux outputs en_high_gm_mode
            ENINJ = 4, // PRS mux outputs en_inj
            INJCLK = 5, // PRS mux outputs injection clock
            PKDETSTATUS = 6, // PRS mux outputs pkdet_status
            XOUTPKDETSTATUS = 7, // PRS mux outputs XOUT pkdet_status
        }
        
        protected enum DBGSTATUS_PKDETSTATUS
        {
            BELOW = 0, // Oscillator amplitude is below peak detection threshold.
            ABOVE = 1, // Oscillator amplitude is above peak detection threshold.
        }
        
        protected enum DBGSTATUS_XOUTPKDETSTATUS
        {
            BELOW = 0, // BUFOUT amplitude is below peak detection threshold.
            ABOVE = 1, // BUFOUT amplitude is above peak detection threshold.
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x5)
            .WithValueField(0, 32, out ipversion_ipversion_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ipversion_Ipversion_ValueProvider(_);
                        return ipversion_ipversion_field.Value;               
                    },
                    readCallback: (_, __) => Ipversion_Ipversion_Read(_, __),
                    name: "Ipversion")
            .WithReadCallback((_, __) => Ipversion_Read(_, __))
            .WithWriteCallback((_, __) => Ipversion_Write(_, __));
        
        // Trim - Offset : 0x4
        protected DoubleWordRegister  GenerateTrimRegister() => new DoubleWordRegister(this, 0x670F0711)
            .WithValueField(0, 2, out trim_regtrimcgmana_field, 
                    valueProviderCallback: (_) => {
                        Trim_Regtrimcgmana_ValueProvider(_);
                        return trim_regtrimcgmana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Regtrimcgmana_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Regtrimcgmana_Read(_, __),
                    name: "Regtrimcgmana")
            .WithReservedBits(2, 2)
            .WithValueField(4, 2, out trim_vtrcoretcana_field, 
                    valueProviderCallback: (_) => {
                        Trim_Vtrcoretcana_ValueProvider(_);
                        return trim_vtrcoretcana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Vtrcoretcana_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Vtrcoretcana_Read(_, __),
                    name: "Vtrcoretcana")
            .WithReservedBits(6, 2)
            .WithValueField(8, 5, out trim_regtrimvreg0_field, 
                    valueProviderCallback: (_) => {
                        Trim_Regtrimvreg0_ValueProvider(_);
                        return trim_regtrimvreg0_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Regtrimvreg0_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Regtrimvreg0_Read(_, __),
                    name: "Regtrimvreg0")
            .WithReservedBits(13, 3)
            .WithValueField(16, 5, out trim_regtrimvreg1_field, 
                    valueProviderCallback: (_) => {
                        Trim_Regtrimvreg1_ValueProvider(_);
                        return trim_regtrimvreg1_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Regtrimvreg1_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Regtrimvreg1_Read(_, __),
                    name: "Regtrimvreg1")
            .WithReservedBits(21, 3)
            .WithValueField(24, 4, out trim_vtrcoretrimana_field, 
                    valueProviderCallback: (_) => {
                        Trim_Vtrcoretrimana_ValueProvider(_);
                        return trim_vtrcoretrimana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Vtrcoretrimana_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Vtrcoretrimana_Read(_, __),
                    name: "Vtrcoretrimana")
            .WithValueField(28, 4, out trim_shuntlvlana_field, 
                    valueProviderCallback: (_) => {
                        Trim_Shuntlvlana_ValueProvider(_);
                        return trim_shuntlvlana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Shuntlvlana_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Shuntlvlana_Read(_, __),
                    name: "Shuntlvlana")
            .WithReadCallback((_, __) => Trim_Read(_, __))
            .WithWriteCallback((_, __) => Trim_Write(_, __));
        
        // Xouttrim - Offset : 0xC
        protected DoubleWordRegister  GenerateXouttrimRegister() => new DoubleWordRegister(this, 0x54534)
            .WithValueField(0, 3, out xouttrim_vregbiastrimibndioana_field, 
                    valueProviderCallback: (_) => {
                        Xouttrim_Vregbiastrimibndioana_ValueProvider(_);
                        return xouttrim_vregbiastrimibndioana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xouttrim_Vregbiastrimibndioana_Write(_, __);
                    },
                    readCallback: (_, __) => Xouttrim_Vregbiastrimibndioana_Read(_, __),
                    name: "Vregbiastrimibndioana")
            .WithReservedBits(3, 1)
            .WithValueField(4, 3, out xouttrim_vregbiastrimibcoreana_field, 
                    valueProviderCallback: (_) => {
                        Xouttrim_Vregbiastrimibcoreana_ValueProvider(_);
                        return xouttrim_vregbiastrimibcoreana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xouttrim_Vregbiastrimibcoreana_Write(_, __);
                    },
                    readCallback: (_, __) => Xouttrim_Vregbiastrimibcoreana_Read(_, __),
                    name: "Vregbiastrimibcoreana")
            .WithReservedBits(7, 1)
            .WithValueField(8, 2, out xouttrim_xoutcasbiasana_field, 
                    valueProviderCallback: (_) => {
                        Xouttrim_Xoutcasbiasana_ValueProvider(_);
                        return xouttrim_xoutcasbiasana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xouttrim_Xoutcasbiasana_Write(_, __);
                    },
                    readCallback: (_, __) => Xouttrim_Xoutcasbiasana_Read(_, __),
                    name: "Xoutcasbiasana")
            .WithValueField(10, 2, out xouttrim_xoutpdiocasana_field, 
                    valueProviderCallback: (_) => {
                        Xouttrim_Xoutpdiocasana_ValueProvider(_);
                        return xouttrim_xoutpdiocasana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xouttrim_Xoutpdiocasana_Write(_, __);
                    },
                    readCallback: (_, __) => Xouttrim_Xoutpdiocasana_Read(_, __),
                    name: "Xoutpdiocasana")
            .WithValueField(12, 4, out xouttrim_xoutcmfiltresana_field, 
                    valueProviderCallback: (_) => {
                        Xouttrim_Xoutcmfiltresana_ValueProvider(_);
                        return xouttrim_xoutcmfiltresana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xouttrim_Xoutcmfiltresana_Write(_, __);
                    },
                    readCallback: (_, __) => Xouttrim_Xoutcmfiltresana_Read(_, __),
                    name: "Xoutcmfiltresana")
            .WithValueField(16, 3, out xouttrim_vtrtcana_field, 
                    valueProviderCallback: (_) => {
                        Xouttrim_Vtrtcana_ValueProvider(_);
                        return xouttrim_vtrtcana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xouttrim_Vtrtcana_Write(_, __);
                    },
                    readCallback: (_, __) => Xouttrim_Vtrtcana_Read(_, __),
                    name: "Vtrtcana")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Xouttrim_Read(_, __))
            .WithWriteCallback((_, __) => Xouttrim_Write(_, __));
        
        // Xtalcfg - Offset : 0x10
        protected DoubleWordRegister  GenerateXtalcfgRegister() => new DoubleWordRegister(this, 0x2BC00208)
            .WithValueField(0, 4, out xtalcfg_corebiasstartupi_field, 
                    valueProviderCallback: (_) => {
                        Xtalcfg_Corebiasstartupi_ValueProvider(_);
                        return xtalcfg_corebiasstartupi_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalcfg_Corebiasstartupi_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalcfg_Corebiasstartupi_Read(_, __),
                    name: "Corebiasstartupi")
            .WithReservedBits(4, 2)
            .WithValueField(6, 4, out xtalcfg_corebiasstartup_field, 
                    valueProviderCallback: (_) => {
                        Xtalcfg_Corebiasstartup_ValueProvider(_);
                        return xtalcfg_corebiasstartup_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalcfg_Corebiasstartup_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalcfg_Corebiasstartup_Read(_, __),
                    name: "Corebiasstartup")
            .WithReservedBits(10, 2)
            .WithValueField(12, 3, out xtalcfg_ctunexistartup_field, 
                    valueProviderCallback: (_) => {
                        Xtalcfg_Ctunexistartup_ValueProvider(_);
                        return xtalcfg_ctunexistartup_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalcfg_Ctunexistartup_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalcfg_Ctunexistartup_Read(_, __),
                    name: "Ctunexistartup")
            .WithReservedBits(15, 1)
            .WithValueField(16, 3, out xtalcfg_ctunexostartup_field, 
                    valueProviderCallback: (_) => {
                        Xtalcfg_Ctunexostartup_ValueProvider(_);
                        return xtalcfg_ctunexostartup_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalcfg_Ctunexostartup_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalcfg_Ctunexostartup_Read(_, __),
                    name: "Ctunexostartup")
            .WithReservedBits(19, 1)
            .WithEnumField<DoubleWordRegister, XTALCFG_TIMEOUTSTEADY>(20, 4, out xtalcfg_timeoutsteady_field, 
                    valueProviderCallback: (_) => {
                        Xtalcfg_Timeoutsteady_ValueProvider(_);
                        return xtalcfg_timeoutsteady_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalcfg_Timeoutsteady_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalcfg_Timeoutsteady_Read(_, __),
                    name: "Timeoutsteady")
            .WithEnumField<DoubleWordRegister, XTALCFG_TIMEOUTCBLSB>(24, 4, out xtalcfg_timeoutcblsb_field, 
                    valueProviderCallback: (_) => {
                        Xtalcfg_Timeoutcblsb_ValueProvider(_);
                        return xtalcfg_timeoutcblsb_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalcfg_Timeoutcblsb_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalcfg_Timeoutcblsb_Read(_, __),
                    name: "Timeoutcblsb")
            .WithEnumField<DoubleWordRegister, XTALCFG_TINJ>(28, 4, out xtalcfg_tinj_field, 
                    valueProviderCallback: (_) => {
                        Xtalcfg_Tinj_ValueProvider(_);
                        return xtalcfg_tinj_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalcfg_Tinj_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalcfg_Tinj_Read(_, __),
                    name: "Tinj")
            .WithReadCallback((_, __) => Xtalcfg_Read(_, __))
            .WithWriteCallback((_, __) => Xtalcfg_Write(_, __));
        
        // Xtalctrl - Offset : 0x18
        protected DoubleWordRegister  GenerateXtalctrlRegister() => new DoubleWordRegister(this, 0x2404078)
            .WithValueField(0, 9, out xtalctrl_corebiasana_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl_Corebiasana_ValueProvider(_);
                        return xtalctrl_corebiasana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Xtalctrl_Corebiasana_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl_Corebiasana_Read(_, __),
                    name: "Corebiasana")
            .WithValueField(9, 8, out xtalctrl_ctunexiana_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl_Ctunexiana_ValueProvider(_);
                        return xtalctrl_ctunexiana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalctrl_Ctunexiana_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl_Ctunexiana_Read(_, __),
                    name: "Ctunexiana")
            .WithValueField(17, 8, out xtalctrl_ctunexoana_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl_Ctunexoana_ValueProvider(_);
                        return xtalctrl_ctunexoana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalctrl_Ctunexoana_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl_Ctunexoana_Read(_, __),
                    name: "Ctunexoana")
            .WithEnumField<DoubleWordRegister, XTALCTRL_CTUNEFIXANA>(25, 2, out xtalctrl_ctunefixana_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl_Ctunefixana_ValueProvider(_);
                        return xtalctrl_ctunefixana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalctrl_Ctunefixana_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl_Ctunefixana_Read(_, __),
                    name: "Ctunefixana")
            .WithReservedBits(27, 4)
            .WithFlag(31, out xtalctrl_skipcorebiasopt_bit, 
                    valueProviderCallback: (_) => {
                        Xtalctrl_Skipcorebiasopt_ValueProvider(_);
                        return xtalctrl_skipcorebiasopt_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Xtalctrl_Skipcorebiasopt_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl_Skipcorebiasopt_Read(_, __),
                    name: "Skipcorebiasopt")
            .WithReadCallback((_, __) => Xtalctrl_Read(_, __))
            .WithWriteCallback((_, __) => Xtalctrl_Write(_, __));
        
        // Xtalctrl1 - Offset : 0x1C
        protected DoubleWordRegister  GenerateXtalctrl1Register() => new DoubleWordRegister(this, 0x70200)
            .WithReservedBits(0, 8)
            .WithValueField(8, 4, out xtalctrl1_ctunexibufoutdelta_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl1_Ctunexibufoutdelta_ValueProvider(_);
                        return xtalctrl1_ctunexibufoutdelta_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalctrl1_Ctunexibufoutdelta_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl1_Ctunexibufoutdelta_Read(_, __),
                    name: "Ctunexibufoutdelta")
            .WithReservedBits(12, 4)
            .WithValueField(16, 5, out xtalctrl1_corebiasbufoutdelta_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl1_Corebiasbufoutdelta_ValueProvider(_);
                        return xtalctrl1_corebiasbufoutdelta_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalctrl1_Corebiasbufoutdelta_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl1_Corebiasbufoutdelta_Read(_, __),
                    name: "Corebiasbufoutdelta")
            .WithReservedBits(21, 11)
            .WithReadCallback((_, __) => Xtalctrl1_Read(_, __))
            .WithWriteCallback((_, __) => Xtalctrl1_Write(_, __));
        
        // Cfg - Offset : 0x20
        protected DoubleWordRegister  GenerateCfgRegister() => new DoubleWordRegister(this, 0x18000000)
            .WithEnumField<DoubleWordRegister, CFG_MODE>(0, 2, out cfg_mode_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Mode_ValueProvider(_);
                        return cfg_mode_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Mode_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Mode_Read(_, __),
                    name: "Mode")
            .WithFlag(2, out cfg_enxidcbiasana_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Enxidcbiasana_ValueProvider(_);
                        return cfg_enxidcbiasana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Enxidcbiasana_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Enxidcbiasana_Read(_, __),
                    name: "Enxidcbiasana")
            .WithReservedBits(3, 5)
            .WithFlag(8, out cfg_sleepyxtalsupen_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Sleepyxtalsupen_ValueProvider(_);
                        return cfg_sleepyxtalsupen_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Sleepyxtalsupen_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Sleepyxtalsupen_Read(_, __),
                    name: "Sleepyxtalsupen")
            .WithFlag(9, out cfg_stupmeasen_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Stupmeasen_ValueProvider(_);
                        return cfg_stupmeasen_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Stupmeasen_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Stupmeasen_Read(_, __),
                    name: "Stupmeasen")
            .WithEnumField<DoubleWordRegister, CFG_NUMSTUPMEAS>(10, 2, out cfg_numstupmeas_field, 
                    valueProviderCallback: (_) => {
                        Cfg_Numstupmeas_ValueProvider(_);
                        return cfg_numstupmeas_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Numstupmeas_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Numstupmeas_Read(_, __),
                    name: "Numstupmeas")
            .WithReservedBits(12, 15)
            .WithFlag(27, out cfg_forcelftimeoutsysrtc_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Forcelftimeoutsysrtc_ValueProvider(_);
                        return cfg_forcelftimeoutsysrtc_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Forcelftimeoutsysrtc_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Forcelftimeoutsysrtc_Read(_, __),
                    name: "Forcelftimeoutsysrtc")
            .WithFlag(28, out cfg_forcelftimeoutprs_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Forcelftimeoutprs_ValueProvider(_);
                        return cfg_forcelftimeoutprs_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Forcelftimeoutprs_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Forcelftimeoutprs_Read(_, __),
                    name: "Forcelftimeoutprs")
            .WithFlag(29, out cfg_forcehftimeout_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Forcehftimeout_ValueProvider(_);
                        return cfg_forcehftimeout_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Forcehftimeout_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Forcehftimeout_Read(_, __),
                    name: "Forcehftimeout")
            .WithFlag(30, out cfg_sqbufenstartupi_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Sqbufenstartupi_ValueProvider(_);
                        return cfg_sqbufenstartupi_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Sqbufenstartupi_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Sqbufenstartupi_Read(_, __),
                    name: "Sqbufenstartupi")
            .WithFlag(31, out cfg_disfsm_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Disfsm_ValueProvider(_);
                        return cfg_disfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Disfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Disfsm_Read(_, __),
                    name: "Disfsm")
            .WithReadCallback((_, __) => Cfg_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Write(_, __));
        
        // Sleepyxtalcfg0 - Offset : 0x28
        protected DoubleWordRegister  GenerateSleepyxtalcfg0Register() => new DoubleWordRegister(this, 0x20200518)
            .WithValueField(0, 4, out sleepyxtalcfg0_pkdetthsupsleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg0_Pkdetthsupsleepy_ValueProvider(_);
                        return sleepyxtalcfg0_pkdetthsupsleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg0_Pkdetthsupsleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg0_Pkdetthsupsleepy_Read(_, __),
                    name: "Pkdetthsupsleepy")
            .WithValueField(4, 4, out sleepyxtalcfg0_pkdetthsupisleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg0_Pkdetthsupisleepy_ValueProvider(_);
                        return sleepyxtalcfg0_pkdetthsupisleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg0_Pkdetthsupisleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg0_Pkdetthsupisleepy_Read(_, __),
                    name: "Pkdetthsupisleepy")
            .WithValueField(8, 4, out sleepyxtalcfg0_pkdetthanasleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg0_Pkdetthanasleepy_ValueProvider(_);
                        return sleepyxtalcfg0_pkdetthanasleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg0_Pkdetthanasleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg0_Pkdetthanasleepy_Read(_, __),
                    name: "Pkdetthanasleepy")
            .WithReservedBits(12, 4)
            .WithValueField(16, 3, out sleepyxtalcfg0_ctunexisupsleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg0_Ctunexisupsleepy_ValueProvider(_);
                        return sleepyxtalcfg0_ctunexisupsleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg0_Ctunexisupsleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg0_Ctunexisupsleepy_Read(_, __),
                    name: "Ctunexisupsleepy")
            .WithReservedBits(19, 1)
            .WithValueField(20, 3, out sleepyxtalcfg0_ctunexianasleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg0_Ctunexianasleepy_ValueProvider(_);
                        return sleepyxtalcfg0_ctunexianasleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg0_Ctunexianasleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg0_Ctunexianasleepy_Read(_, __),
                    name: "Ctunexianasleepy")
            .WithReservedBits(23, 1)
            .WithValueField(24, 3, out sleepyxtalcfg0_ctunexosupsleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg0_Ctunexosupsleepy_ValueProvider(_);
                        return sleepyxtalcfg0_ctunexosupsleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg0_Ctunexosupsleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg0_Ctunexosupsleepy_Read(_, __),
                    name: "Ctunexosupsleepy")
            .WithReservedBits(27, 1)
            .WithValueField(28, 3, out sleepyxtalcfg0_ctunexoanasleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg0_Ctunexoanasleepy_ValueProvider(_);
                        return sleepyxtalcfg0_ctunexoanasleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg0_Ctunexoanasleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg0_Ctunexoanasleepy_Read(_, __),
                    name: "Ctunexoanasleepy")
            .WithReservedBits(31, 1)
            .WithReadCallback((_, __) => Sleepyxtalcfg0_Read(_, __))
            .WithWriteCallback((_, __) => Sleepyxtalcfg0_Write(_, __));
        
        // Sleepyxtalcfg1 - Offset : 0x2C
        protected DoubleWordRegister  GenerateSleepyxtalcfg1Register() => new DoubleWordRegister(this, 0x7FFFFFF)
            .WithValueField(0, 9, out sleepyxtalcfg1_corebiassupsleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg1_Corebiassupsleepy_ValueProvider(_);
                        return sleepyxtalcfg1_corebiassupsleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg1_Corebiassupsleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg1_Corebiassupsleepy_Read(_, __),
                    name: "Corebiassupsleepy")
            .WithValueField(9, 9, out sleepyxtalcfg1_corebiassupisleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg1_Corebiassupisleepy_ValueProvider(_);
                        return sleepyxtalcfg1_corebiassupisleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg1_Corebiassupisleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg1_Corebiassupisleepy_Read(_, __),
                    name: "Corebiassupisleepy")
            .WithValueField(18, 9, out sleepyxtalcfg1_corebiasanasleepy_field, 
                    valueProviderCallback: (_) => {
                        Sleepyxtalcfg1_Corebiasanasleepy_ValueProvider(_);
                        return sleepyxtalcfg1_corebiasanasleepy_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Sleepyxtalcfg1_Corebiasanasleepy_Write(_, __);
                    },
                    readCallback: (_, __) => Sleepyxtalcfg1_Corebiasanasleepy_Read(_, __),
                    name: "Corebiasanasleepy")
            .WithReservedBits(27, 5)
            .WithReadCallback((_, __) => Sleepyxtalcfg1_Read(_, __))
            .WithWriteCallback((_, __) => Sleepyxtalcfg1_Write(_, __));
        
        // Ctrl - Offset : 0x30
        protected DoubleWordRegister  GenerateCtrlRegister() => new DoubleWordRegister(this, 0xF000040)
            .WithFlag(0, out ctrl_bufoutfreeze_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Bufoutfreeze_ValueProvider(_);
                        return ctrl_bufoutfreeze_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Bufoutfreeze_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Bufoutfreeze_Read(_, __),
                    name: "Bufoutfreeze")
            .WithReservedBits(1, 1)
            .WithFlag(2, out ctrl_keepwarm_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Keepwarm_ValueProvider(_);
                        return ctrl_keepwarm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Keepwarm_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Keepwarm_Read(_, __),
                    name: "Keepwarm")
            .WithFlag(3, out ctrl_em23ondemand_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Em23ondemand_ValueProvider(_);
                        return ctrl_em23ondemand_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Em23ondemand_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Em23ondemand_Read(_, __),
                    name: "Em23ondemand")
            .WithEnumField<DoubleWordRegister, CTRL_FORCEXI2GNDANA>(4, 1, out ctrl_forcexi2gndana_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forcexi2gndana_ValueProvider(_);
                        return ctrl_forcexi2gndana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Forcexi2gndana_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forcexi2gndana_Read(_, __),
                    name: "Forcexi2gndana")
            .WithEnumField<DoubleWordRegister, CTRL_FORCEXO2GNDANA>(5, 1, out ctrl_forcexo2gndana_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forcexo2gndana_ValueProvider(_);
                        return ctrl_forcexo2gndana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Forcexo2gndana_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forcexo2gndana_Read(_, __),
                    name: "Forcexo2gndana")
            .WithFlag(6, out ctrl_forcectunemax_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forcectunemax_ValueProvider(_);
                        return ctrl_forcectunemax_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Ctrl_Forcectunemax_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forcectunemax_Read(_, __),
                    name: "Forcectunemax")
            .WithReservedBits(7, 1)
            .WithEnumField<DoubleWordRegister, CTRL_PRSSTATUSSEL0>(8, 4, out ctrl_prsstatussel0_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Prsstatussel0_ValueProvider(_);
                        return ctrl_prsstatussel0_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Prsstatussel0_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Prsstatussel0_Read(_, __),
                    name: "Prsstatussel0")
            .WithEnumField<DoubleWordRegister, CTRL_PRSSTATUSSEL1>(12, 4, out ctrl_prsstatussel1_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Prsstatussel1_ValueProvider(_);
                        return ctrl_prsstatussel1_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Prsstatussel1_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Prsstatussel1_Read(_, __),
                    name: "Prsstatussel1")
            .WithFlag(16, out ctrl_forceen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceen_ValueProvider(_);
                        return ctrl_forceen_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Forceen_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forceen_Read(_, __),
                    name: "Forceen")
            .WithFlag(17, out ctrl_forceenprs_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceenprs_ValueProvider(_);
                        return ctrl_forceenprs_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Forceenprs_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forceenprs_Read(_, __),
                    name: "Forceenprs")
            .WithFlag(18, out ctrl_forceenbufout_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceenbufout_ValueProvider(_);
                        return ctrl_forceenbufout_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Forceenbufout_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forceenbufout_Read(_, __),
                    name: "Forceenbufout")
            .WithFlag(19, out ctrl_forceensysrtc_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceensysrtc_ValueProvider(_);
                        return ctrl_forceensysrtc_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Forceensysrtc_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forceensysrtc_Read(_, __),
                    name: "Forceensysrtc")
            .WithReservedBits(20, 4)
            .WithFlag(24, out ctrl_disondemand_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Disondemand_ValueProvider(_);
                        return ctrl_disondemand_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Disondemand_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Disondemand_Read(_, __),
                    name: "Disondemand")
            .WithFlag(25, out ctrl_disondemandprs_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Disondemandprs_ValueProvider(_);
                        return ctrl_disondemandprs_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Disondemandprs_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Disondemandprs_Read(_, __),
                    name: "Disondemandprs")
            .WithFlag(26, out ctrl_disondemandbufout_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Disondemandbufout_ValueProvider(_);
                        return ctrl_disondemandbufout_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Disondemandbufout_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Disondemandbufout_Read(_, __),
                    name: "Disondemandbufout")
            .WithFlag(27, out ctrl_disondemandsysrtc_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Disondemandsysrtc_ValueProvider(_);
                        return ctrl_disondemandsysrtc_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Disondemandsysrtc_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Disondemandsysrtc_Read(_, __),
                    name: "Disondemandsysrtc")
            .WithReservedBits(28, 3)
            .WithFlag(31, out ctrl_forcerawclk_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forcerawclk_ValueProvider(_);
                        return ctrl_forcerawclk_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Ctrl_Forcerawclk_Write(_, __);
                    },
                    readCallback: (_, __) => Ctrl_Forcerawclk_Read(_, __),
                    name: "Forcerawclk")
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write(_, __));
        
        // Pkdetctrl1 - Offset : 0x34
        protected DoubleWordRegister  GeneratePkdetctrl1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out pkdetctrl1_notimeouterr_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl1_Notimeouterr_ValueProvider(_);
                        return pkdetctrl1_notimeouterr_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl1_Notimeouterr_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl1_Notimeouterr_Read(_, __),
                    name: "Notimeouterr")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Pkdetctrl1_Read(_, __))
            .WithWriteCallback((_, __) => Pkdetctrl1_Write(_, __));
        
        // Lowpwrctrl - Offset : 0x38
        protected DoubleWordRegister  GenerateLowpwrctrlRegister() => new DoubleWordRegister(this, 0xF8000320)
            .WithFlag(0, out lowpwrctrl_regtrimbwana_bit, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Regtrimbwana_ValueProvider(_);
                        return lowpwrctrl_regtrimbwana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Regtrimbwana_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Regtrimbwana_Read(_, __),
                    name: "Regtrimbwana")
            .WithReservedBits(1, 3)
            .WithEnumField<DoubleWordRegister, LOWPWRCTRL_SQBUFBIASRESANA>(4, 2, out lowpwrctrl_sqbufbiasresana_field, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Sqbufbiasresana_ValueProvider(_);
                        return lowpwrctrl_sqbufbiasresana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Sqbufbiasresana_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Sqbufbiasresana_Read(_, __),
                    name: "Sqbufbiasresana")
            .WithReservedBits(6, 2)
            .WithEnumField<DoubleWordRegister, LOWPWRCTRL_SQBUFBIASANA>(8, 3, out lowpwrctrl_sqbufbiasana_field, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Sqbufbiasana_ValueProvider(_);
                        return lowpwrctrl_sqbufbiasana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Sqbufbiasana_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Sqbufbiasana_Read(_, __),
                    name: "Sqbufbiasana")
            .WithReservedBits(11, 5)
            .WithEnumField<DoubleWordRegister, LOWPWRCTRL_TIMEOUTWARM>(16, 3, out lowpwrctrl_timeoutwarm_field, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Timeoutwarm_ValueProvider(_);
                        return lowpwrctrl_timeoutwarm_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Timeoutwarm_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Timeoutwarm_Read(_, __),
                    name: "Timeoutwarm")
            .WithReservedBits(19, 4)
            .WithFlag(23, out lowpwrctrl_shuntbiasanahpen_bit, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Shuntbiasanahpen_ValueProvider(_);
                        return lowpwrctrl_shuntbiasanahpen_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Shuntbiasanahpen_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Shuntbiasanahpen_Read(_, __),
                    name: "Shuntbiasanahpen")
            .WithEnumField<DoubleWordRegister, LOWPWRCTRL_SHUNTBIASANALP>(24, 4, out lowpwrctrl_shuntbiasanalp_field, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Shuntbiasanalp_ValueProvider(_);
                        return lowpwrctrl_shuntbiasanalp_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Shuntbiasanalp_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Shuntbiasanalp_Read(_, __),
                    name: "Shuntbiasanalp")
            .WithEnumField<DoubleWordRegister, LOWPWRCTRL_SHUNTBIASANAHP>(28, 4, out lowpwrctrl_shuntbiasanahp_field, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Shuntbiasanahp_ValueProvider(_);
                        return lowpwrctrl_shuntbiasanahp_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Shuntbiasanahp_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Shuntbiasanahp_Read(_, __),
                    name: "Shuntbiasanahp")
            .WithReadCallback((_, __) => Lowpwrctrl_Read(_, __))
            .WithWriteCallback((_, __) => Lowpwrctrl_Write(_, __));
        
        // Pkdetctrl - Offset : 0x3C
        protected DoubleWordRegister  GeneratePkdetctrlRegister() => new DoubleWordRegister(this, 0x81F78558)
            .WithFlag(0, out pkdetctrl_enpkdetana_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Enpkdetana_ValueProvider(_);
                        return pkdetctrl_enpkdetana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Enpkdetana_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Enpkdetana_Read(_, __),
                    name: "Enpkdetana")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_PKDETNODEANA>(1, 1, out pkdetctrl_pkdetnodeana_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetnodeana_ValueProvider(_);
                        return pkdetctrl_pkdetnodeana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Pkdetnodeana_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetnodeana_Read(_, __),
                    name: "Pkdetnodeana")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_PKDETNODESTARTUPI>(2, 1, out pkdetctrl_pkdetnodestartupi_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetnodestartupi_ValueProvider(_);
                        return pkdetctrl_pkdetnodestartupi_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Pkdetnodestartupi_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetnodestartupi_Read(_, __),
                    name: "Pkdetnodestartupi")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_PKDETNODESTARTUP>(3, 1, out pkdetctrl_pkdetnodestartup_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetnodestartup_ValueProvider(_);
                        return pkdetctrl_pkdetnodestartup_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Pkdetctrl_Pkdetnodestartup_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetnodestartup_Read(_, __),
                    name: "Pkdetnodestartup")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_PKDETTHANA>(4, 4, out pkdetctrl_pkdetthana_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetthana_ValueProvider(_);
                        return pkdetctrl_pkdetthana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Pkdetthana_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetthana_Read(_, __),
                    name: "Pkdetthana")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_PKDETTHSTARTUPI>(8, 4, out pkdetctrl_pkdetthstartupi_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetthstartupi_ValueProvider(_);
                        return pkdetctrl_pkdetthstartupi_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Pkdetthstartupi_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetthstartupi_Read(_, __),
                    name: "Pkdetthstartupi")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_PKDETTHSTARTUP>(12, 4, out pkdetctrl_pkdetthstartup_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetthstartup_ValueProvider(_);
                        return pkdetctrl_pkdetthstartup_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Pkdetthstartup_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetthstartup_Read(_, __),
                    name: "Pkdetthstartup")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_PKDETTHHIGH>(16, 4, out pkdetctrl_pkdetthhigh_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetthhigh_ValueProvider(_);
                        return pkdetctrl_pkdetthhigh_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Pkdetthhigh_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetthhigh_Read(_, __),
                    name: "Pkdetthhigh")
            .WithValueField(20, 3, out pkdetctrl_pkdetstep_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Pkdetstep_ValueProvider(_);
                        return pkdetctrl_pkdetstep_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Pkdetctrl_Pkdetstep_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Pkdetstep_Read(_, __),
                    name: "Pkdetstep")
            .WithFlag(23, out pkdetctrl_enpkdetfsm_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Enpkdetfsm_ValueProvider(_);
                        return pkdetctrl_enpkdetfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Pkdetctrl_Enpkdetfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Enpkdetfsm_Read(_, __),
                    name: "Enpkdetfsm")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_TIMEOUTPKDET>(24, 2, out pkdetctrl_timeoutpkdet_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Timeoutpkdet_ValueProvider(_);
                        return pkdetctrl_timeoutpkdet_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Timeoutpkdet_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Timeoutpkdet_Read(_, __),
                    name: "Timeoutpkdet")
            .WithReservedBits(26, 4)
            .WithEnumField<DoubleWordRegister, PKDETCTRL_REGLVLANA>(30, 1, out pkdetctrl_reglvlana_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Reglvlana_ValueProvider(_);
                        return pkdetctrl_reglvlana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Reglvlana_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Reglvlana_Read(_, __),
                    name: "Reglvlana")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_REGLVLSTARTUP>(31, 1, out pkdetctrl_reglvlstartup_bit, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Reglvlstartup_ValueProvider(_);
                        return pkdetctrl_reglvlstartup_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Reglvlstartup_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Reglvlstartup_Read(_, __),
                    name: "Reglvlstartup")
            .WithReadCallback((_, __) => Pkdetctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pkdetctrl_Write(_, __));
        
        // Extclkpkdetctrl - Offset : 0x40
        protected DoubleWordRegister  GenerateExtclkpkdetctrlRegister() => new DoubleWordRegister(this, 0x55)
            .WithValueField(0, 4, out extclkpkdetctrl_pkdetthextclk0_field, 
                    valueProviderCallback: (_) => {
                        Extclkpkdetctrl_Pkdetthextclk0_ValueProvider(_);
                        return extclkpkdetctrl_pkdetthextclk0_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Extclkpkdetctrl_Pkdetthextclk0_Write(_, __);
                    },
                    readCallback: (_, __) => Extclkpkdetctrl_Pkdetthextclk0_Read(_, __),
                    name: "Pkdetthextclk0")
            .WithValueField(4, 4, out extclkpkdetctrl_pkdetthextclk1_field, 
                    valueProviderCallback: (_) => {
                        Extclkpkdetctrl_Pkdetthextclk1_ValueProvider(_);
                        return extclkpkdetctrl_pkdetthextclk1_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Extclkpkdetctrl_Pkdetthextclk1_Write(_, __);
                    },
                    readCallback: (_, __) => Extclkpkdetctrl_Pkdetthextclk1_Read(_, __),
                    name: "Pkdetthextclk1")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Extclkpkdetctrl_Read(_, __))
            .WithWriteCallback((_, __) => Extclkpkdetctrl_Write(_, __));
        
        // Internalctrl - Offset : 0x44
        protected DoubleWordRegister  GenerateInternalctrlRegister() => new DoubleWordRegister(this, 0x120AF)
            .WithFlag(0, out internalctrl_enregana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enregana_ValueProvider(_);
                        return internalctrl_enregana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enregana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enregana_Read(_, __),
                    name: "Enregana")
            .WithFlag(1, out internalctrl_ensqbufana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Ensqbufana_ValueProvider(_);
                        return internalctrl_ensqbufana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Ensqbufana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Ensqbufana_Read(_, __),
                    name: "Ensqbufana")
            .WithFlag(2, out internalctrl_encoreana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Encoreana_ValueProvider(_);
                        return internalctrl_encoreana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Internalctrl_Encoreana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Encoreana_Read(_, __),
                    name: "Encoreana")
            .WithFlag(3, out internalctrl_enshuntregana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enshuntregana_ValueProvider(_);
                        return internalctrl_enshuntregana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enshuntregana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enshuntregana_Read(_, __),
                    name: "Enshuntregana")
            .WithFlag(4, out internalctrl_shortxi2xoana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Shortxi2xoana_ValueProvider(_);
                        return internalctrl_shortxi2xoana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Shortxi2xoana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Shortxi2xoana_Read(_, __),
                    name: "Shortxi2xoana")
            .WithFlag(5, out internalctrl_shortxi2xofsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Shortxi2xofsm_ValueProvider(_);
                        return internalctrl_shortxi2xofsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Internalctrl_Shortxi2xofsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Shortxi2xofsm_Read(_, __),
                    name: "Shortxi2xofsm")
            .WithFlag(6, out internalctrl_enhighgmmodeana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enhighgmmodeana_ValueProvider(_);
                        return internalctrl_enhighgmmodeana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enhighgmmodeana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enhighgmmodeana_Read(_, __),
                    name: "Enhighgmmodeana")
            .WithFlag(7, out internalctrl_enhighgmmodefsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enhighgmmodefsm_ValueProvider(_);
                        return internalctrl_enhighgmmodefsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Internalctrl_Enhighgmmodefsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enhighgmmodefsm_Read(_, __),
                    name: "Enhighgmmodefsm")
            .WithEnumField<DoubleWordRegister, INTERNALCTRL_SQBUFFILTANA>(8, 2, out internalctrl_sqbuffiltana_field, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Sqbuffiltana_ValueProvider(_);
                        return internalctrl_sqbuffiltana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Sqbuffiltana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Sqbuffiltana_Read(_, __),
                    name: "Sqbuffiltana")
            .WithFlag(10, out internalctrl_enclkdifana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enclkdifana_ValueProvider(_);
                        return internalctrl_enclkdifana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enclkdifana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enclkdifana_Read(_, __),
                    name: "Enclkdifana")
            .WithFlag(11, out internalctrl_eninjana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Eninjana_ValueProvider(_);
                        return internalctrl_eninjana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Eninjana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Eninjana_Read(_, __),
                    name: "Eninjana")
            .WithFlag(12, out internalctrl_eninjfsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Eninjfsm_ValueProvider(_);
                        return internalctrl_eninjfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Eninjfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Eninjfsm_Read(_, __),
                    name: "Eninjfsm")
            .WithFlag(13, out internalctrl_enclkdigana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enclkdigana_ValueProvider(_);
                        return internalctrl_enclkdigana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enclkdigana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enclkdigana_Read(_, __),
                    name: "Enclkdigana")
            .WithFlag(14, out internalctrl_enclkauxadcana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enclkauxadcana_ValueProvider(_);
                        return internalctrl_enclkauxadcana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enclkauxadcana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enclkauxadcana_Read(_, __),
                    name: "Enclkauxadcana")
            .WithFlag(15, out internalctrl_enclkclkmultana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enclkclkmultana_ValueProvider(_);
                        return internalctrl_enclkclkmultana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enclkclkmultana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enclkclkmultana_Read(_, __),
                    name: "Enclkclkmultana")
            .WithFlag(16, out internalctrl_enclksyana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enclksyana_ValueProvider(_);
                        return internalctrl_enclksyana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enclksyana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enclksyana_Read(_, __),
                    name: "Enclksyana")
            .WithFlag(17, out internalctrl_enclktxana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enclktxana_ValueProvider(_);
                        return internalctrl_enclktxana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enclktxana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enclktxana_Read(_, __),
                    name: "Enclktxana")
            .WithFlag(18, out internalctrl_invclkdigana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Invclkdigana_ValueProvider(_);
                        return internalctrl_invclkdigana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Invclkdigana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Invclkdigana_Read(_, __),
                    name: "Invclkdigana")
            .WithFlag(19, out internalctrl_invclkauxadcana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Invclkauxadcana_ValueProvider(_);
                        return internalctrl_invclkauxadcana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Invclkauxadcana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Invclkauxadcana_Read(_, __),
                    name: "Invclkauxadcana")
            .WithFlag(20, out internalctrl_invclkclkmultana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Invclkclkmultana_ValueProvider(_);
                        return internalctrl_invclkclkmultana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Invclkclkmultana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Invclkclkmultana_Read(_, __),
                    name: "Invclkclkmultana")
            .WithFlag(21, out internalctrl_invclksyana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Invclksyana_ValueProvider(_);
                        return internalctrl_invclksyana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Invclksyana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Invclksyana_Read(_, __),
                    name: "Invclksyana")
            .WithFlag(22, out internalctrl_invclktxana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Invclktxana_ValueProvider(_);
                        return internalctrl_invclktxana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Invclktxana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Invclktxana_Read(_, __),
                    name: "Invclktxana")
            .WithReservedBits(23, 1)
            .WithFlag(24, out internalctrl_eninjtempcompfsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Eninjtempcompfsm_ValueProvider(_);
                        return internalctrl_eninjtempcompfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Eninjtempcompfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Eninjtempcompfsm_Read(_, __),
                    name: "Eninjtempcompfsm")
            .WithFlag(25, out internalctrl_eninjsyseqfsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Eninjsyseqfsm_ValueProvider(_);
                        return internalctrl_eninjsyseqfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Eninjsyseqfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Eninjsyseqfsm_Read(_, __),
                    name: "Eninjsyseqfsm")
            .WithReservedBits(26, 2)
            .WithFlag(28, out internalctrl_ensleepyxtalerrstate_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Ensleepyxtalerrstate_ValueProvider(_);
                        return internalctrl_ensleepyxtalerrstate_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Ensleepyxtalerrstate_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Ensleepyxtalerrstate_Read(_, __),
                    name: "Ensleepyxtalerrstate")
            .WithReservedBits(29, 2)
            .WithEnumField<DoubleWordRegister, INTERNALCTRL_VTRCOREDISSTARTUPANA>(31, 1, out internalctrl_vtrcoredisstartupana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Vtrcoredisstartupana_ValueProvider(_);
                        return internalctrl_vtrcoredisstartupana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Vtrcoredisstartupana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Vtrcoredisstartupana_Read(_, __),
                    name: "Vtrcoredisstartupana")
            .WithReadCallback((_, __) => Internalctrl_Read(_, __))
            .WithWriteCallback((_, __) => Internalctrl_Write(_, __));
        
        // Internalxoutctrl - Offset : 0x48
        protected DoubleWordRegister  GenerateInternalxoutctrlRegister() => new DoubleWordRegister(this, 0x153)
            .WithFlag(0, out internalxoutctrl_envregbiasana_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Envregbiasana_ValueProvider(_);
                        return internalxoutctrl_envregbiasana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Envregbiasana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Envregbiasana_Read(_, __),
                    name: "Envregbiasana")
            .WithFlag(1, out internalxoutctrl_envregana_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Envregana_ValueProvider(_);
                        return internalxoutctrl_envregana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Envregana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Envregana_Read(_, __),
                    name: "Envregana")
            .WithFlag(2, out internalxoutctrl_vtrforcestartupana_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Vtrforcestartupana_ValueProvider(_);
                        return internalxoutctrl_vtrforcestartupana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Vtrforcestartupana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Vtrforcestartupana_Read(_, __),
                    name: "Vtrforcestartupana")
            .WithFlag(3, out internalxoutctrl_vtrforcestartupfsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Vtrforcestartupfsm_ValueProvider(_);
                        return internalxoutctrl_vtrforcestartupfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Vtrforcestartupfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Vtrforcestartupfsm_Read(_, __),
                    name: "Vtrforcestartupfsm")
            .WithFlag(4, out internalxoutctrl_enxoutana_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Enxoutana_ValueProvider(_);
                        return internalxoutctrl_enxoutana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Enxoutana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Enxoutana_Read(_, __),
                    name: "Enxoutana")
            .WithFlag(5, out internalxoutctrl_enpeakdetana_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Enpeakdetana_ValueProvider(_);
                        return internalxoutctrl_enpeakdetana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Enpeakdetana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Enpeakdetana_Read(_, __),
                    name: "Enpeakdetana")
            .WithFlag(6, out internalxoutctrl_enpeakdetfsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Enpeakdetfsm_ValueProvider(_);
                        return internalxoutctrl_enpeakdetfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Enpeakdetfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Enpeakdetfsm_Read(_, __),
                    name: "Enpeakdetfsm")
            .WithFlag(7, out internalxoutctrl_enib2p5uapana_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Enib2p5uapana_ValueProvider(_);
                        return internalxoutctrl_enib2p5uapana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Enib2p5uapana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Enib2p5uapana_Read(_, __),
                    name: "Enib2p5uapana")
            .WithFlag(8, out internalxoutctrl_enib2p5uapfsm_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Enib2p5uapfsm_ValueProvider(_);
                        return internalxoutctrl_enib2p5uapfsm_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Enib2p5uapfsm_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Enib2p5uapfsm_Read(_, __),
                    name: "Enib2p5uapfsm")
            .WithFlag(9, out internalxoutctrl_envregloadana_bit, 
                    valueProviderCallback: (_) => {
                        Internalxoutctrl_Envregloadana_ValueProvider(_);
                        return internalxoutctrl_envregloadana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalxoutctrl_Envregloadana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalxoutctrl_Envregloadana_Read(_, __),
                    name: "Envregloadana")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Internalxoutctrl_Read(_, __))
            .WithWriteCallback((_, __) => Internalxoutctrl_Write(_, __));
        
        // Bufouttrim - Offset : 0x4C
        protected DoubleWordRegister  GenerateBufouttrimRegister() => new DoubleWordRegister(this, 0x8)
            .WithValueField(0, 4, out bufouttrim_vtrtrimana_field, 
                    valueProviderCallback: (_) => {
                        Bufouttrim_Vtrtrimana_ValueProvider(_);
                        return bufouttrim_vtrtrimana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufouttrim_Vtrtrimana_Write(_, __);
                    },
                    readCallback: (_, __) => Bufouttrim_Vtrtrimana_Read(_, __),
                    name: "Vtrtrimana")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Bufouttrim_Read(_, __))
            .WithWriteCallback((_, __) => Bufouttrim_Write(_, __));
        
        // Bufoutctrl - Offset : 0x50
        protected DoubleWordRegister  GenerateBufoutctrlRegister() => new DoubleWordRegister(this, 0x643C15)
            .WithValueField(0, 4, out bufoutctrl_xoutbiasana_field, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Xoutbiasana_ValueProvider(_);
                        return bufoutctrl_xoutbiasana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Xoutbiasana_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Xoutbiasana_Read(_, __),
                    name: "Xoutbiasana")
            .WithValueField(4, 4, out bufoutctrl_xoutcfana_field, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Xoutcfana_ValueProvider(_);
                        return bufoutctrl_xoutcfana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Xoutcfana_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Xoutcfana_Read(_, __),
                    name: "Xoutcfana")
            .WithValueField(8, 4, out bufoutctrl_xoutgmana_field, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Xoutgmana_ValueProvider(_);
                        return bufoutctrl_xoutgmana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Xoutgmana_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Xoutgmana_Read(_, __),
                    name: "Xoutgmana")
            .WithValueField(12, 4, out bufoutctrl_peakdetthresana_field, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Peakdetthresana_ValueProvider(_);
                        return bufoutctrl_peakdetthresana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Peakdetthresana_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Peakdetthresana_Read(_, __),
                    name: "Peakdetthresana")
            .WithEnumField<DoubleWordRegister, BUFOUTCTRL_TIMEOUTCTUNE>(16, 4, out bufoutctrl_timeoutctune_field, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Timeoutctune_ValueProvider(_);
                        return bufoutctrl_timeoutctune_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Timeoutctune_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Timeoutctune_Read(_, __),
                    name: "Timeoutctune")
            .WithEnumField<DoubleWordRegister, BUFOUTCTRL_TIMEOUTSTARTUP>(20, 4, out bufoutctrl_timeoutstartup_field, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Timeoutstartup_ValueProvider(_);
                        return bufoutctrl_timeoutstartup_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Timeoutstartup_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Timeoutstartup_Read(_, __),
                    name: "Timeoutstartup")
            .WithReservedBits(24, 6)
            .WithFlag(30, out bufoutctrl_allowcorebiasopt_bit, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Allowcorebiasopt_ValueProvider(_);
                        return bufoutctrl_allowcorebiasopt_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Allowcorebiasopt_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Allowcorebiasopt_Read(_, __),
                    name: "Allowcorebiasopt")
            .WithFlag(31, out bufoutctrl_minimumstartupdelay_bit, 
                    valueProviderCallback: (_) => {
                        Bufoutctrl_Minimumstartupdelay_ValueProvider(_);
                        return bufoutctrl_minimumstartupdelay_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Bufoutctrl_Minimumstartupdelay_Write(_, __);
                    },
                    readCallback: (_, __) => Bufoutctrl_Minimumstartupdelay_Read(_, __),
                    name: "Minimumstartupdelay")
            .WithReadCallback((_, __) => Bufoutctrl_Read(_, __))
            .WithWriteCallback((_, __) => Bufoutctrl_Write(_, __));
        
        // Cmd - Offset : 0x54
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cmd_corebiasopt_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cmd_Corebiasopt_Write(_, __);
                    },
                    name: "Corebiasopt")
            .WithFlag(1, out cmd_startmeas_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cmd_Startmeas_Write(_, __);
                    },
                    name: "Startmeas")
            .WithFlag(2, out cmd_stopmeas_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cmd_Stopmeas_Write(_, __);
                    },
                    name: "Stopmeas")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write(_, __));
        
        // Status - Offset : 0x58
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_rdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Rdy_ValueProvider(_);
                        return status_rdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out status_corebiasoptrdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Corebiasoptrdy_ValueProvider(_);
                        return status_corebiasoptrdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Corebiasoptrdy_Read(_, __),
                    name: "Corebiasoptrdy")
            .WithFlag(2, out status_prsrdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Prsrdy_ValueProvider(_);
                        return status_prsrdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Prsrdy_Read(_, __),
                    name: "Prsrdy")
            .WithFlag(3, out status_bufoutrdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Bufoutrdy_ValueProvider(_);
                        return status_bufoutrdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Bufoutrdy_Read(_, __),
                    name: "Bufoutrdy")
            .WithFlag(4, out status_sysrtcrdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Sysrtcrdy_ValueProvider(_);
                        return status_sysrtcrdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Sysrtcrdy_Read(_, __),
                    name: "Sysrtcrdy")
            .WithReservedBits(5, 3)
            .WithFlag(8, out status_sleepyxtal_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Sleepyxtal_ValueProvider(_);
                        return status_sleepyxtal_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Sleepyxtal_Read(_, __),
                    name: "Sleepyxtal")
            .WithFlag(9, out status_sleepyxtalerr_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Sleepyxtalerr_ValueProvider(_);
                        return status_sleepyxtalerr_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Sleepyxtalerr_Read(_, __),
                    name: "Sleepyxtalerr")
            .WithReservedBits(10, 5)
            .WithFlag(15, out status_bufoutfrozen_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Bufoutfrozen_ValueProvider(_);
                        return status_bufoutfrozen_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Bufoutfrozen_Read(_, __),
                    name: "Bufoutfrozen")
            .WithFlag(16, out status_ens_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ens_ValueProvider(_);
                        return status_ens_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Ens_Read(_, __),
                    name: "Ens")
            .WithFlag(17, out status_hwreq_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Hwreq_ValueProvider(_);
                        return status_hwreq_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Hwreq_Read(_, __),
                    name: "Hwreq")
            .WithFlag(18, out status_isforced_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Isforced_ValueProvider(_);
                        return status_isforced_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Isforced_Read(_, __),
                    name: "Isforced")
            .WithFlag(19, out status_iswarm_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Iswarm_ValueProvider(_);
                        return status_iswarm_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Iswarm_Read(_, __),
                    name: "Iswarm")
            .WithFlag(20, out status_prshwreq_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Prshwreq_ValueProvider(_);
                        return status_prshwreq_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Prshwreq_Read(_, __),
                    name: "Prshwreq")
            .WithFlag(21, out status_bufouthwreq_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Bufouthwreq_ValueProvider(_);
                        return status_bufouthwreq_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Bufouthwreq_Read(_, __),
                    name: "Bufouthwreq")
            .WithFlag(22, out status_sysrtchwreq_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Sysrtchwreq_ValueProvider(_);
                        return status_sysrtchwreq_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Sysrtchwreq_Read(_, __),
                    name: "Sysrtchwreq")
            .WithReservedBits(23, 5)
            .WithFlag(28, out status_stupmeasbsy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Stupmeasbsy_ValueProvider(_);
                        return status_stupmeasbsy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Stupmeasbsy_Read(_, __),
                    name: "Stupmeasbsy")
            .WithFlag(29, out status_injbsy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Injbsy_ValueProvider(_);
                        return status_injbsy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Injbsy_Read(_, __),
                    name: "Injbsy")
            .WithFlag(30, out status_syncbusy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Syncbusy_ValueProvider(_);
                        return status_syncbusy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Syncbusy_Read(_, __),
                    name: "Syncbusy")
            .WithEnumField<DoubleWordRegister, STATUS_LOCK>(31, 1, out status_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lock_ValueProvider(_);
                        return status_lock_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Lock_Read(_, __),
                    name: "Lock")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Avgstartuptime - Offset : 0x5C
        protected DoubleWordRegister  GenerateAvgstartuptimeRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 9, out avgstartuptime_avgstup_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Avgstartuptime_Avgstup_ValueProvider(_);
                        return avgstartuptime_avgstup_field.Value;               
                    },
                    readCallback: (_, __) => Avgstartuptime_Avgstup_Read(_, __),
                    name: "Avgstup")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Avgstartuptime_Read(_, __))
            .WithWriteCallback((_, __) => Avgstartuptime_Write(_, __));
        
        // Dbgctrl - Offset : 0x60
        protected DoubleWordRegister  GenerateDbgctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, DBGCTRL_PRSDBGSEL0>(0, 3, out dbgctrl_prsdbgsel0_field, 
                    valueProviderCallback: (_) => {
                        Dbgctrl_Prsdbgsel0_ValueProvider(_);
                        return dbgctrl_prsdbgsel0_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Dbgctrl_Prsdbgsel0_Write(_, __);
                    },
                    readCallback: (_, __) => Dbgctrl_Prsdbgsel0_Read(_, __),
                    name: "Prsdbgsel0")
            .WithReservedBits(3, 5)
            .WithEnumField<DoubleWordRegister, DBGCTRL_PRSDBGSEL1>(8, 3, out dbgctrl_prsdbgsel1_field, 
                    valueProviderCallback: (_) => {
                        Dbgctrl_Prsdbgsel1_ValueProvider(_);
                        return dbgctrl_prsdbgsel1_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Dbgctrl_Prsdbgsel1_Write(_, __);
                    },
                    readCallback: (_, __) => Dbgctrl_Prsdbgsel1_Read(_, __),
                    name: "Prsdbgsel1")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Dbgctrl_Read(_, __))
            .WithWriteCallback((_, __) => Dbgctrl_Write(_, __));
        
        // Dbgstatus - Offset : 0x64
        protected DoubleWordRegister  GenerateDbgstatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, DBGSTATUS_PKDETSTATUS>(0, 1, out dbgstatus_pkdetstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Dbgstatus_Pkdetstatus_ValueProvider(_);
                        return dbgstatus_pkdetstatus_bit.Value;               
                    },
                    readCallback: (_, __) => Dbgstatus_Pkdetstatus_Read(_, __),
                    name: "Pkdetstatus")
            .WithFlag(1, out dbgstatus_startupdone_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Dbgstatus_Startupdone_ValueProvider(_);
                        return dbgstatus_startupdone_bit.Value;               
                    },
                    readCallback: (_, __) => Dbgstatus_Startupdone_Read(_, __),
                    name: "Startupdone")
            .WithEnumField<DoubleWordRegister, DBGSTATUS_XOUTPKDETSTATUS>(2, 1, out dbgstatus_xoutpkdetstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Dbgstatus_Xoutpkdetstatus_ValueProvider(_);
                        return dbgstatus_xoutpkdetstatus_bit.Value;               
                    },
                    readCallback: (_, __) => Dbgstatus_Xoutpkdetstatus_Read(_, __),
                    name: "Xoutpkdetstatus")
            .WithReservedBits(3, 1)
            .WithValueField(4, 13, out dbgstatus_accstupmeas_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Dbgstatus_Accstupmeas_ValueProvider(_);
                        return dbgstatus_accstupmeas_field.Value;               
                    },
                    readCallback: (_, __) => Dbgstatus_Accstupmeas_Read(_, __),
                    name: "Accstupmeas")
            .WithReservedBits(17, 3)
            .WithValueField(20, 9, out dbgstatus_stupmeas_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Dbgstatus_Stupmeas_ValueProvider(_);
                        return dbgstatus_stupmeas_field.Value;               
                    },
                    readCallback: (_, __) => Dbgstatus_Stupmeas_Read(_, __),
                    name: "Stupmeas")
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => Dbgstatus_Read(_, __))
            .WithWriteCallback((_, __) => Dbgstatus_Write(_, __));
        
        // If - Offset : 0x70
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_rdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Rdy_ValueProvider(_);
                        return if_rdy_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Rdy_Write(_, __),
                    readCallback: (_, __) => If_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out if_corebiasoptrdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Corebiasoptrdy_ValueProvider(_);
                        return if_corebiasoptrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Corebiasoptrdy_Write(_, __),
                    readCallback: (_, __) => If_Corebiasoptrdy_Read(_, __),
                    name: "Corebiasoptrdy")
            .WithFlag(2, out if_prsrdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Prsrdy_ValueProvider(_);
                        return if_prsrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Prsrdy_Write(_, __),
                    readCallback: (_, __) => If_Prsrdy_Read(_, __),
                    name: "Prsrdy")
            .WithFlag(3, out if_bufoutrdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Bufoutrdy_ValueProvider(_);
                        return if_bufoutrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Bufoutrdy_Write(_, __),
                    readCallback: (_, __) => If_Bufoutrdy_Read(_, __),
                    name: "Bufoutrdy")
            .WithFlag(4, out if_sysrtcrdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Sysrtcrdy_ValueProvider(_);
                        return if_sysrtcrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Sysrtcrdy_Write(_, __),
                    readCallback: (_, __) => If_Sysrtcrdy_Read(_, __),
                    name: "Sysrtcrdy")
            .WithReservedBits(5, 3)
            .WithFlag(8, out if_stupmeasdone_bit, 
                    valueProviderCallback: (_) => {
                        If_Stupmeasdone_ValueProvider(_);
                        return if_stupmeasdone_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Stupmeasdone_Write(_, __),
                    readCallback: (_, __) => If_Stupmeasdone_Read(_, __),
                    name: "Stupmeasdone")
            .WithFlag(9, out if_sleepyxtal_bit, 
                    valueProviderCallback: (_) => {
                        If_Sleepyxtal_ValueProvider(_);
                        return if_sleepyxtal_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Sleepyxtal_Write(_, __),
                    readCallback: (_, __) => If_Sleepyxtal_Read(_, __),
                    name: "Sleepyxtal")
            .WithReservedBits(10, 2)
            .WithFlag(12, out if_injskip_bit, 
                    valueProviderCallback: (_) => {
                        If_Injskip_ValueProvider(_);
                        return if_injskip_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Injskip_Write(_, __),
                    readCallback: (_, __) => If_Injskip_Read(_, __),
                    name: "Injskip")
            .WithReservedBits(13, 2)
            .WithFlag(15, out if_bufoutfrozen_bit, 
                    valueProviderCallback: (_) => {
                        If_Bufoutfrozen_ValueProvider(_);
                        return if_bufoutfrozen_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Bufoutfrozen_Write(_, __),
                    readCallback: (_, __) => If_Bufoutfrozen_Read(_, __),
                    name: "Bufoutfrozen")
            .WithReservedBits(16, 4)
            .WithFlag(20, out if_prserr_bit, 
                    valueProviderCallback: (_) => {
                        If_Prserr_ValueProvider(_);
                        return if_prserr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Prserr_Write(_, __),
                    readCallback: (_, __) => If_Prserr_Read(_, __),
                    name: "Prserr")
            .WithFlag(21, out if_bufouterr_bit, 
                    valueProviderCallback: (_) => {
                        If_Bufouterr_ValueProvider(_);
                        return if_bufouterr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Bufouterr_Write(_, __),
                    readCallback: (_, __) => If_Bufouterr_Read(_, __),
                    name: "Bufouterr")
            .WithFlag(22, out if_sysrtcerr_bit, 
                    valueProviderCallback: (_) => {
                        If_Sysrtcerr_ValueProvider(_);
                        return if_sysrtcerr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Sysrtcerr_Write(_, __),
                    readCallback: (_, __) => If_Sysrtcerr_Read(_, __),
                    name: "Sysrtcerr")
            .WithReservedBits(23, 2)
            .WithFlag(25, out if_injerr_bit, 
                    valueProviderCallback: (_) => {
                        If_Injerr_ValueProvider(_);
                        return if_injerr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Injerr_Write(_, __),
                    readCallback: (_, __) => If_Injerr_Read(_, __),
                    name: "Injerr")
            .WithFlag(26, out if_dnssleepyxtalerr_bit, 
                    valueProviderCallback: (_) => {
                        If_Dnssleepyxtalerr_ValueProvider(_);
                        return if_dnssleepyxtalerr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Dnssleepyxtalerr_Write(_, __),
                    readCallback: (_, __) => If_Dnssleepyxtalerr_Read(_, __),
                    name: "Dnssleepyxtalerr")
            .WithFlag(27, out if_bufoutfreezeerr_bit, 
                    valueProviderCallback: (_) => {
                        If_Bufoutfreezeerr_ValueProvider(_);
                        return if_bufoutfreezeerr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Bufoutfreezeerr_Write(_, __),
                    readCallback: (_, __) => If_Bufoutfreezeerr_Read(_, __),
                    name: "Bufoutfreezeerr")
            .WithFlag(28, out if_bufoutdnserr_bit, 
                    valueProviderCallback: (_) => {
                        If_Bufoutdnserr_ValueProvider(_);
                        return if_bufoutdnserr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Bufoutdnserr_Write(_, __),
                    readCallback: (_, __) => If_Bufoutdnserr_Read(_, __),
                    name: "Bufoutdnserr")
            .WithFlag(29, out if_dnserr_bit, 
                    valueProviderCallback: (_) => {
                        If_Dnserr_ValueProvider(_);
                        return if_dnserr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Dnserr_Write(_, __),
                    readCallback: (_, __) => If_Dnserr_Read(_, __),
                    name: "Dnserr")
            .WithFlag(30, out if_lftimeouterr_bit, 
                    valueProviderCallback: (_) => {
                        If_Lftimeouterr_ValueProvider(_);
                        return if_lftimeouterr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Lftimeouterr_Write(_, __),
                    readCallback: (_, __) => If_Lftimeouterr_Read(_, __),
                    name: "Lftimeouterr")
            .WithFlag(31, out if_corebiasopterr_bit, 
                    valueProviderCallback: (_) => {
                        If_Corebiasopterr_ValueProvider(_);
                        return if_corebiasopterr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Corebiasopterr_Write(_, __),
                    readCallback: (_, __) => If_Corebiasopterr_Read(_, __),
                    name: "Corebiasopterr")
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x74
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_rdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Rdy_ValueProvider(_);
                        return ien_rdy_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Rdy_Write(_, __),
                    readCallback: (_, __) => Ien_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out ien_corebiasoptrdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Corebiasoptrdy_ValueProvider(_);
                        return ien_corebiasoptrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Corebiasoptrdy_Write(_, __),
                    readCallback: (_, __) => Ien_Corebiasoptrdy_Read(_, __),
                    name: "Corebiasoptrdy")
            .WithFlag(2, out ien_prsrdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Prsrdy_ValueProvider(_);
                        return ien_prsrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Prsrdy_Write(_, __),
                    readCallback: (_, __) => Ien_Prsrdy_Read(_, __),
                    name: "Prsrdy")
            .WithFlag(3, out ien_bufoutrdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Bufoutrdy_ValueProvider(_);
                        return ien_bufoutrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Bufoutrdy_Write(_, __),
                    readCallback: (_, __) => Ien_Bufoutrdy_Read(_, __),
                    name: "Bufoutrdy")
            .WithFlag(4, out ien_sysrtcrdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Sysrtcrdy_ValueProvider(_);
                        return ien_sysrtcrdy_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Sysrtcrdy_Write(_, __),
                    readCallback: (_, __) => Ien_Sysrtcrdy_Read(_, __),
                    name: "Sysrtcrdy")
            .WithReservedBits(5, 3)
            .WithFlag(8, out ien_stupmeasdone_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Stupmeasdone_ValueProvider(_);
                        return ien_stupmeasdone_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Stupmeasdone_Write(_, __),
                    readCallback: (_, __) => Ien_Stupmeasdone_Read(_, __),
                    name: "Stupmeasdone")
            .WithFlag(9, out ien_sleepyxtal_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Sleepyxtal_ValueProvider(_);
                        return ien_sleepyxtal_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Sleepyxtal_Write(_, __),
                    readCallback: (_, __) => Ien_Sleepyxtal_Read(_, __),
                    name: "Sleepyxtal")
            .WithReservedBits(10, 2)
            .WithFlag(12, out ien_injskip_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Injskip_ValueProvider(_);
                        return ien_injskip_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Injskip_Write(_, __),
                    readCallback: (_, __) => Ien_Injskip_Read(_, __),
                    name: "Injskip")
            .WithReservedBits(13, 2)
            .WithFlag(15, out ien_bufoutfrozen_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Bufoutfrozen_ValueProvider(_);
                        return ien_bufoutfrozen_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Bufoutfrozen_Write(_, __),
                    readCallback: (_, __) => Ien_Bufoutfrozen_Read(_, __),
                    name: "Bufoutfrozen")
            .WithReservedBits(16, 4)
            .WithFlag(20, out ien_prserr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Prserr_ValueProvider(_);
                        return ien_prserr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Prserr_Write(_, __),
                    readCallback: (_, __) => Ien_Prserr_Read(_, __),
                    name: "Prserr")
            .WithFlag(21, out ien_bufouterr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Bufouterr_ValueProvider(_);
                        return ien_bufouterr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Bufouterr_Write(_, __),
                    readCallback: (_, __) => Ien_Bufouterr_Read(_, __),
                    name: "Bufouterr")
            .WithFlag(22, out ien_sysrtcerr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Sysrtcerr_ValueProvider(_);
                        return ien_sysrtcerr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Sysrtcerr_Write(_, __),
                    readCallback: (_, __) => Ien_Sysrtcerr_Read(_, __),
                    name: "Sysrtcerr")
            .WithReservedBits(23, 2)
            .WithFlag(25, out ien_injerr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Injerr_ValueProvider(_);
                        return ien_injerr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Injerr_Write(_, __),
                    readCallback: (_, __) => Ien_Injerr_Read(_, __),
                    name: "Injerr")
            .WithFlag(26, out ien_dnssleepyxtalerr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Dnssleepyxtalerr_ValueProvider(_);
                        return ien_dnssleepyxtalerr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Dnssleepyxtalerr_Write(_, __),
                    readCallback: (_, __) => Ien_Dnssleepyxtalerr_Read(_, __),
                    name: "Dnssleepyxtalerr")
            .WithFlag(27, out ien_bufoutfreezeerr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Bufoutfreezeerr_ValueProvider(_);
                        return ien_bufoutfreezeerr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Bufoutfreezeerr_Write(_, __),
                    readCallback: (_, __) => Ien_Bufoutfreezeerr_Read(_, __),
                    name: "Bufoutfreezeerr")
            .WithFlag(28, out ien_bufoutdnserr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Bufoutdnserr_ValueProvider(_);
                        return ien_bufoutdnserr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Bufoutdnserr_Write(_, __),
                    readCallback: (_, __) => Ien_Bufoutdnserr_Read(_, __),
                    name: "Bufoutdnserr")
            .WithFlag(29, out ien_dnserr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Dnserr_ValueProvider(_);
                        return ien_dnserr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Dnserr_Write(_, __),
                    readCallback: (_, __) => Ien_Dnserr_Read(_, __),
                    name: "Dnserr")
            .WithFlag(30, out ien_lftimeouterr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Lftimeouterr_ValueProvider(_);
                        return ien_lftimeouterr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Lftimeouterr_Write(_, __),
                    readCallback: (_, __) => Ien_Lftimeouterr_Read(_, __),
                    name: "Lftimeouterr")
            .WithFlag(31, out ien_corebiasopterr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Corebiasopterr_ValueProvider(_);
                        return ien_corebiasopterr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Corebiasopterr_Write(_, __),
                    readCallback: (_, __) => Ien_Corebiasopterr_Read(_, __),
                    name: "Corebiasopterr")
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Lock - Offset : 0x80
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x580E)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Rfcfg_Xoinjhwseq - Offset : 0xA0
        protected DoubleWordRegister  GenerateRfcfg_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x2000000)
            .WithValueField(0, 11, out rfcfg_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Rfcfg_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return rfcfg_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Rfcfg_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Rfcfg_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 13)
            .WithValueField(24, 3, out rfcfg_xoinjhwseq_vtrtrimvreg_field, 
                    valueProviderCallback: (_) => {
                        Rfcfg_Xoinjhwseq_Vtrtrimvreg_ValueProvider(_);
                        return rfcfg_xoinjhwseq_vtrtrimvreg_field.Value;               
                    },
                    writeCallback: (_, __) => Rfcfg_Xoinjhwseq_Vtrtrimvreg_Write(_, __),
                    readCallback: (_, __) => Rfcfg_Xoinjhwseq_Vtrtrimvreg_Read(_, __),
                    name: "Vtrtrimvreg")
            .WithReservedBits(27, 3)
            .WithFlag(30, out rfcfg_xoinjhwseq_syvcocapdcap_bit, 
                    valueProviderCallback: (_) => {
                        Rfcfg_Xoinjhwseq_Syvcocapdcap_ValueProvider(_);
                        return rfcfg_xoinjhwseq_syvcocapdcap_bit.Value;               
                    },
                    writeCallback: (_, __) => Rfcfg_Xoinjhwseq_Syvcocapdcap_Write(_, __),
                    readCallback: (_, __) => Rfcfg_Xoinjhwseq_Syvcocapdcap_Read(_, __),
                    name: "Syvcocapdcap")
            .WithFlag(31, out rfcfg_xoinjhwseq_syvcocaphcap_bit, 
                    valueProviderCallback: (_) => {
                        Rfcfg_Xoinjhwseq_Syvcocaphcap_ValueProvider(_);
                        return rfcfg_xoinjhwseq_syvcocaphcap_bit.Value;               
                    },
                    writeCallback: (_, __) => Rfcfg_Xoinjhwseq_Syvcocaphcap_Write(_, __),
                    readCallback: (_, __) => Rfcfg_Xoinjhwseq_Syvcocaphcap_Read(_, __),
                    name: "Syvcocaphcap")
            .WithReadCallback((_, __) => Rfcfg_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Rfcfg_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp0_Xoinjhwseq - Offset : 0xA4
        protected DoubleWordRegister  GenerateSyvcocaptemp0_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp0_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp0_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp0_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp0_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp0_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp0_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp0_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp1_Xoinjhwseq - Offset : 0xA8
        protected DoubleWordRegister  GenerateSyvcocaptemp1_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp1_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp1_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp1_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp1_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp1_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp1_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp1_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp2_Xoinjhwseq - Offset : 0xAC
        protected DoubleWordRegister  GenerateSyvcocaptemp2_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp2_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp2_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp2_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp2_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp2_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp2_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp2_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp3_Xoinjhwseq - Offset : 0xB0
        protected DoubleWordRegister  GenerateSyvcocaptemp3_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp3_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp3_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp3_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp3_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp3_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp3_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp3_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp4_Xoinjhwseq - Offset : 0xB4
        protected DoubleWordRegister  GenerateSyvcocaptemp4_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp4_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp4_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp4_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp4_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp4_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp4_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp4_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp5_Xoinjhwseq - Offset : 0xB8
        protected DoubleWordRegister  GenerateSyvcocaptemp5_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp5_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp5_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp5_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp5_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp5_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp5_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp5_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp6_Xoinjhwseq - Offset : 0xBC
        protected DoubleWordRegister  GenerateSyvcocaptemp6_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp6_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp6_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp6_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp6_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp6_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp6_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp6_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp7_Xoinjhwseq - Offset : 0xC0
        protected DoubleWordRegister  GenerateSyvcocaptemp7_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp7_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp7_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp7_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp7_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp7_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp7_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp7_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp8_Xoinjhwseq - Offset : 0xC4
        protected DoubleWordRegister  GenerateSyvcocaptemp8_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp8_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp8_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp8_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp8_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp8_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp8_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp8_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp9_Xoinjhwseq - Offset : 0xC8
        protected DoubleWordRegister  GenerateSyvcocaptemp9_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp9_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp9_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp9_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp9_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp9_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp9_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp9_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp10_Xoinjhwseq - Offset : 0xCC
        protected DoubleWordRegister  GenerateSyvcocaptemp10_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp10_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp10_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp10_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp10_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp10_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp10_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp10_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp11_Xoinjhwseq - Offset : 0xD0
        protected DoubleWordRegister  GenerateSyvcocaptemp11_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp11_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp11_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp11_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp11_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp11_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp11_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp11_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp12_Xoinjhwseq - Offset : 0xD4
        protected DoubleWordRegister  GenerateSyvcocaptemp12_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp12_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp12_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp12_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp12_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp12_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp12_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp12_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp13_Xoinjhwseq - Offset : 0xD8
        protected DoubleWordRegister  GenerateSyvcocaptemp13_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp13_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp13_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp13_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp13_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp13_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp13_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp13_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp14_Xoinjhwseq - Offset : 0xDC
        protected DoubleWordRegister  GenerateSyvcocaptemp14_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp14_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp14_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp14_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp14_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp14_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp14_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp14_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp15_Xoinjhwseq - Offset : 0xE0
        protected DoubleWordRegister  GenerateSyvcocaptemp15_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp15_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp15_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp15_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp15_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp15_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp15_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp15_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp16_Xoinjhwseq - Offset : 0xE4
        protected DoubleWordRegister  GenerateSyvcocaptemp16_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp16_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp16_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp16_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp16_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp16_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp16_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp16_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp17_Xoinjhwseq - Offset : 0xE8
        protected DoubleWordRegister  GenerateSyvcocaptemp17_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp17_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp17_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp17_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp17_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp17_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp17_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp17_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp18_Xoinjhwseq - Offset : 0xEC
        protected DoubleWordRegister  GenerateSyvcocaptemp18_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp18_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp18_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp18_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp18_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp18_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp18_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp18_Xoinjhwseq_Write(_, __));
        
        // Syvcocaptemp19_Xoinjhwseq - Offset : 0xF0
        protected DoubleWordRegister  GenerateSyvcocaptemp19_xoinjhwseqRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 11, out syvcocaptemp19_xoinjhwseq_syvcocap_field, 
                    valueProviderCallback: (_) => {
                        Syvcocaptemp19_Xoinjhwseq_Syvcocap_ValueProvider(_);
                        return syvcocaptemp19_xoinjhwseq_syvcocap_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Syvcocaptemp19_Xoinjhwseq_Syvcocap_Write(_, __);
                    },
                    readCallback: (_, __) => Syvcocaptemp19_Xoinjhwseq_Syvcocap_Read(_, __),
                    name: "Syvcocap")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Syvcocaptemp19_Xoinjhwseq_Read(_, __))
            .WithWriteCallback((_, __) => Syvcocaptemp19_Xoinjhwseq_Write(_, __));
        

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

        

        private void WriteWSYNC()
        {
            if(!Enabled)
            {
                // throw new InvalidOperationException("Bus fault trying to write to a WSYNC register while peripheral is disabled");
                this.Log(LogLevel.Error, "Trying to write to a WSYNC register while peripheral is disabled EN = {0}", Enabled);
            }
        }

        private void WriteRWSYNC()
        {
            if(!Enabled)
            {
                // throw new InvalidOperationException("Bus fault trying to write to a RWSYNC register while peripheral is disabled");
                this.Log(LogLevel.Error, "Trying to write to a RWSYNC register while peripheral is disabled EN = {0}", Enabled);
            }
        }

        // Ipversion - Offset : 0x0
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);

        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        // Trim - Offset : 0x4
        protected IValueRegisterField trim_regtrimcgmana_field;
        partial void Trim_Regtrimcgmana_Write(ulong a, ulong b);
        partial void Trim_Regtrimcgmana_Read(ulong a, ulong b);
        partial void Trim_Regtrimcgmana_ValueProvider(ulong a);
        protected IValueRegisterField trim_vtrcoretcana_field;
        partial void Trim_Vtrcoretcana_Write(ulong a, ulong b);
        partial void Trim_Vtrcoretcana_Read(ulong a, ulong b);
        partial void Trim_Vtrcoretcana_ValueProvider(ulong a);
        protected IValueRegisterField trim_regtrimvreg0_field;
        partial void Trim_Regtrimvreg0_Write(ulong a, ulong b);
        partial void Trim_Regtrimvreg0_Read(ulong a, ulong b);
        partial void Trim_Regtrimvreg0_ValueProvider(ulong a);
        protected IValueRegisterField trim_regtrimvreg1_field;
        partial void Trim_Regtrimvreg1_Write(ulong a, ulong b);
        partial void Trim_Regtrimvreg1_Read(ulong a, ulong b);
        partial void Trim_Regtrimvreg1_ValueProvider(ulong a);
        protected IValueRegisterField trim_vtrcoretrimana_field;
        partial void Trim_Vtrcoretrimana_Write(ulong a, ulong b);
        partial void Trim_Vtrcoretrimana_Read(ulong a, ulong b);
        partial void Trim_Vtrcoretrimana_ValueProvider(ulong a);
        protected IValueRegisterField trim_shuntlvlana_field;
        partial void Trim_Shuntlvlana_Write(ulong a, ulong b);
        partial void Trim_Shuntlvlana_Read(ulong a, ulong b);
        partial void Trim_Shuntlvlana_ValueProvider(ulong a);

        partial void Trim_Write(uint a, uint b);
        partial void Trim_Read(uint a, uint b);
        
        // Xouttrim - Offset : 0xC
        protected IValueRegisterField xouttrim_vregbiastrimibndioana_field;
        partial void Xouttrim_Vregbiastrimibndioana_Write(ulong a, ulong b);
        partial void Xouttrim_Vregbiastrimibndioana_Read(ulong a, ulong b);
        partial void Xouttrim_Vregbiastrimibndioana_ValueProvider(ulong a);
        protected IValueRegisterField xouttrim_vregbiastrimibcoreana_field;
        partial void Xouttrim_Vregbiastrimibcoreana_Write(ulong a, ulong b);
        partial void Xouttrim_Vregbiastrimibcoreana_Read(ulong a, ulong b);
        partial void Xouttrim_Vregbiastrimibcoreana_ValueProvider(ulong a);
        protected IValueRegisterField xouttrim_xoutcasbiasana_field;
        partial void Xouttrim_Xoutcasbiasana_Write(ulong a, ulong b);
        partial void Xouttrim_Xoutcasbiasana_Read(ulong a, ulong b);
        partial void Xouttrim_Xoutcasbiasana_ValueProvider(ulong a);
        protected IValueRegisterField xouttrim_xoutpdiocasana_field;
        partial void Xouttrim_Xoutpdiocasana_Write(ulong a, ulong b);
        partial void Xouttrim_Xoutpdiocasana_Read(ulong a, ulong b);
        partial void Xouttrim_Xoutpdiocasana_ValueProvider(ulong a);
        protected IValueRegisterField xouttrim_xoutcmfiltresana_field;
        partial void Xouttrim_Xoutcmfiltresana_Write(ulong a, ulong b);
        partial void Xouttrim_Xoutcmfiltresana_Read(ulong a, ulong b);
        partial void Xouttrim_Xoutcmfiltresana_ValueProvider(ulong a);
        protected IValueRegisterField xouttrim_vtrtcana_field;
        partial void Xouttrim_Vtrtcana_Write(ulong a, ulong b);
        partial void Xouttrim_Vtrtcana_Read(ulong a, ulong b);
        partial void Xouttrim_Vtrtcana_ValueProvider(ulong a);

        partial void Xouttrim_Write(uint a, uint b);
        partial void Xouttrim_Read(uint a, uint b);
        
        // Xtalcfg - Offset : 0x10
        protected IValueRegisterField xtalcfg_corebiasstartupi_field;
        partial void Xtalcfg_Corebiasstartupi_Write(ulong a, ulong b);
        partial void Xtalcfg_Corebiasstartupi_Read(ulong a, ulong b);
        partial void Xtalcfg_Corebiasstartupi_ValueProvider(ulong a);
        protected IValueRegisterField xtalcfg_corebiasstartup_field;
        partial void Xtalcfg_Corebiasstartup_Write(ulong a, ulong b);
        partial void Xtalcfg_Corebiasstartup_Read(ulong a, ulong b);
        partial void Xtalcfg_Corebiasstartup_ValueProvider(ulong a);
        protected IValueRegisterField xtalcfg_ctunexistartup_field;
        partial void Xtalcfg_Ctunexistartup_Write(ulong a, ulong b);
        partial void Xtalcfg_Ctunexistartup_Read(ulong a, ulong b);
        partial void Xtalcfg_Ctunexistartup_ValueProvider(ulong a);
        protected IValueRegisterField xtalcfg_ctunexostartup_field;
        partial void Xtalcfg_Ctunexostartup_Write(ulong a, ulong b);
        partial void Xtalcfg_Ctunexostartup_Read(ulong a, ulong b);
        partial void Xtalcfg_Ctunexostartup_ValueProvider(ulong a);
        protected IEnumRegisterField<XTALCFG_TIMEOUTSTEADY> xtalcfg_timeoutsteady_field;
        partial void Xtalcfg_Timeoutsteady_Write(XTALCFG_TIMEOUTSTEADY a, XTALCFG_TIMEOUTSTEADY b);
        partial void Xtalcfg_Timeoutsteady_Read(XTALCFG_TIMEOUTSTEADY a, XTALCFG_TIMEOUTSTEADY b);
        partial void Xtalcfg_Timeoutsteady_ValueProvider(XTALCFG_TIMEOUTSTEADY a);
        protected IEnumRegisterField<XTALCFG_TIMEOUTCBLSB> xtalcfg_timeoutcblsb_field;
        partial void Xtalcfg_Timeoutcblsb_Write(XTALCFG_TIMEOUTCBLSB a, XTALCFG_TIMEOUTCBLSB b);
        partial void Xtalcfg_Timeoutcblsb_Read(XTALCFG_TIMEOUTCBLSB a, XTALCFG_TIMEOUTCBLSB b);
        partial void Xtalcfg_Timeoutcblsb_ValueProvider(XTALCFG_TIMEOUTCBLSB a);
        protected IEnumRegisterField<XTALCFG_TINJ> xtalcfg_tinj_field;
        partial void Xtalcfg_Tinj_Write(XTALCFG_TINJ a, XTALCFG_TINJ b);
        partial void Xtalcfg_Tinj_Read(XTALCFG_TINJ a, XTALCFG_TINJ b);
        partial void Xtalcfg_Tinj_ValueProvider(XTALCFG_TINJ a);

        partial void Xtalcfg_Write(uint a, uint b);
        partial void Xtalcfg_Read(uint a, uint b);
        
        // Xtalctrl - Offset : 0x18
        protected IValueRegisterField xtalctrl_corebiasana_field;
        partial void Xtalctrl_Corebiasana_Write(ulong a, ulong b);
        partial void Xtalctrl_Corebiasana_Read(ulong a, ulong b);
        partial void Xtalctrl_Corebiasana_ValueProvider(ulong a);
        protected IValueRegisterField xtalctrl_ctunexiana_field;
        partial void Xtalctrl_Ctunexiana_Write(ulong a, ulong b);
        partial void Xtalctrl_Ctunexiana_Read(ulong a, ulong b);
        partial void Xtalctrl_Ctunexiana_ValueProvider(ulong a);
        protected IValueRegisterField xtalctrl_ctunexoana_field;
        partial void Xtalctrl_Ctunexoana_Write(ulong a, ulong b);
        partial void Xtalctrl_Ctunexoana_Read(ulong a, ulong b);
        partial void Xtalctrl_Ctunexoana_ValueProvider(ulong a);
        protected IEnumRegisterField<XTALCTRL_CTUNEFIXANA> xtalctrl_ctunefixana_field;
        partial void Xtalctrl_Ctunefixana_Write(XTALCTRL_CTUNEFIXANA a, XTALCTRL_CTUNEFIXANA b);
        partial void Xtalctrl_Ctunefixana_Read(XTALCTRL_CTUNEFIXANA a, XTALCTRL_CTUNEFIXANA b);
        partial void Xtalctrl_Ctunefixana_ValueProvider(XTALCTRL_CTUNEFIXANA a);
        protected IFlagRegisterField xtalctrl_skipcorebiasopt_bit;
        partial void Xtalctrl_Skipcorebiasopt_Write(bool a, bool b);
        partial void Xtalctrl_Skipcorebiasopt_Read(bool a, bool b);
        partial void Xtalctrl_Skipcorebiasopt_ValueProvider(bool a);

        partial void Xtalctrl_Write(uint a, uint b);
        partial void Xtalctrl_Read(uint a, uint b);
        
        // Xtalctrl1 - Offset : 0x1C
        protected IValueRegisterField xtalctrl1_ctunexibufoutdelta_field;
        partial void Xtalctrl1_Ctunexibufoutdelta_Write(ulong a, ulong b);
        partial void Xtalctrl1_Ctunexibufoutdelta_Read(ulong a, ulong b);
        partial void Xtalctrl1_Ctunexibufoutdelta_ValueProvider(ulong a);
        protected IValueRegisterField xtalctrl1_corebiasbufoutdelta_field;
        partial void Xtalctrl1_Corebiasbufoutdelta_Write(ulong a, ulong b);
        partial void Xtalctrl1_Corebiasbufoutdelta_Read(ulong a, ulong b);
        partial void Xtalctrl1_Corebiasbufoutdelta_ValueProvider(ulong a);

        partial void Xtalctrl1_Write(uint a, uint b);
        partial void Xtalctrl1_Read(uint a, uint b);
        
        // Cfg - Offset : 0x20
        protected IEnumRegisterField<CFG_MODE> cfg_mode_field;
        partial void Cfg_Mode_Write(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_Read(CFG_MODE a, CFG_MODE b);
        partial void Cfg_Mode_ValueProvider(CFG_MODE a);
        protected IFlagRegisterField cfg_enxidcbiasana_bit;
        partial void Cfg_Enxidcbiasana_Write(bool a, bool b);
        partial void Cfg_Enxidcbiasana_Read(bool a, bool b);
        partial void Cfg_Enxidcbiasana_ValueProvider(bool a);
        protected IFlagRegisterField cfg_sleepyxtalsupen_bit;
        partial void Cfg_Sleepyxtalsupen_Write(bool a, bool b);
        partial void Cfg_Sleepyxtalsupen_Read(bool a, bool b);
        partial void Cfg_Sleepyxtalsupen_ValueProvider(bool a);
        protected IFlagRegisterField cfg_stupmeasen_bit;
        partial void Cfg_Stupmeasen_Write(bool a, bool b);
        partial void Cfg_Stupmeasen_Read(bool a, bool b);
        partial void Cfg_Stupmeasen_ValueProvider(bool a);
        protected IEnumRegisterField<CFG_NUMSTUPMEAS> cfg_numstupmeas_field;
        partial void Cfg_Numstupmeas_Write(CFG_NUMSTUPMEAS a, CFG_NUMSTUPMEAS b);
        partial void Cfg_Numstupmeas_Read(CFG_NUMSTUPMEAS a, CFG_NUMSTUPMEAS b);
        partial void Cfg_Numstupmeas_ValueProvider(CFG_NUMSTUPMEAS a);
        protected IFlagRegisterField cfg_forcelftimeoutsysrtc_bit;
        partial void Cfg_Forcelftimeoutsysrtc_Write(bool a, bool b);
        partial void Cfg_Forcelftimeoutsysrtc_Read(bool a, bool b);
        partial void Cfg_Forcelftimeoutsysrtc_ValueProvider(bool a);
        protected IFlagRegisterField cfg_forcelftimeoutprs_bit;
        partial void Cfg_Forcelftimeoutprs_Write(bool a, bool b);
        partial void Cfg_Forcelftimeoutprs_Read(bool a, bool b);
        partial void Cfg_Forcelftimeoutprs_ValueProvider(bool a);
        protected IFlagRegisterField cfg_forcehftimeout_bit;
        partial void Cfg_Forcehftimeout_Write(bool a, bool b);
        partial void Cfg_Forcehftimeout_Read(bool a, bool b);
        partial void Cfg_Forcehftimeout_ValueProvider(bool a);
        protected IFlagRegisterField cfg_sqbufenstartupi_bit;
        partial void Cfg_Sqbufenstartupi_Write(bool a, bool b);
        partial void Cfg_Sqbufenstartupi_Read(bool a, bool b);
        partial void Cfg_Sqbufenstartupi_ValueProvider(bool a);
        protected IFlagRegisterField cfg_disfsm_bit;
        partial void Cfg_Disfsm_Write(bool a, bool b);
        partial void Cfg_Disfsm_Read(bool a, bool b);
        partial void Cfg_Disfsm_ValueProvider(bool a);

        partial void Cfg_Write(uint a, uint b);
        partial void Cfg_Read(uint a, uint b);
        
        // Sleepyxtalcfg0 - Offset : 0x28
        protected IValueRegisterField sleepyxtalcfg0_pkdetthsupsleepy_field;
        partial void Sleepyxtalcfg0_Pkdetthsupsleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Pkdetthsupsleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Pkdetthsupsleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg0_pkdetthsupisleepy_field;
        partial void Sleepyxtalcfg0_Pkdetthsupisleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Pkdetthsupisleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Pkdetthsupisleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg0_pkdetthanasleepy_field;
        partial void Sleepyxtalcfg0_Pkdetthanasleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Pkdetthanasleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Pkdetthanasleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg0_ctunexisupsleepy_field;
        partial void Sleepyxtalcfg0_Ctunexisupsleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexisupsleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexisupsleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg0_ctunexianasleepy_field;
        partial void Sleepyxtalcfg0_Ctunexianasleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexianasleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexianasleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg0_ctunexosupsleepy_field;
        partial void Sleepyxtalcfg0_Ctunexosupsleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexosupsleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexosupsleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg0_ctunexoanasleepy_field;
        partial void Sleepyxtalcfg0_Ctunexoanasleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexoanasleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg0_Ctunexoanasleepy_ValueProvider(ulong a);

        partial void Sleepyxtalcfg0_Write(uint a, uint b);
        partial void Sleepyxtalcfg0_Read(uint a, uint b);
        
        // Sleepyxtalcfg1 - Offset : 0x2C
        protected IValueRegisterField sleepyxtalcfg1_corebiassupsleepy_field;
        partial void Sleepyxtalcfg1_Corebiassupsleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg1_Corebiassupsleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg1_Corebiassupsleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg1_corebiassupisleepy_field;
        partial void Sleepyxtalcfg1_Corebiassupisleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg1_Corebiassupisleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg1_Corebiassupisleepy_ValueProvider(ulong a);
        protected IValueRegisterField sleepyxtalcfg1_corebiasanasleepy_field;
        partial void Sleepyxtalcfg1_Corebiasanasleepy_Write(ulong a, ulong b);
        partial void Sleepyxtalcfg1_Corebiasanasleepy_Read(ulong a, ulong b);
        partial void Sleepyxtalcfg1_Corebiasanasleepy_ValueProvider(ulong a);

        partial void Sleepyxtalcfg1_Write(uint a, uint b);
        partial void Sleepyxtalcfg1_Read(uint a, uint b);
        
        // Ctrl - Offset : 0x30
        protected IFlagRegisterField ctrl_bufoutfreeze_bit;
        partial void Ctrl_Bufoutfreeze_Write(bool a, bool b);
        partial void Ctrl_Bufoutfreeze_Read(bool a, bool b);
        partial void Ctrl_Bufoutfreeze_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_keepwarm_bit;
        partial void Ctrl_Keepwarm_Write(bool a, bool b);
        partial void Ctrl_Keepwarm_Read(bool a, bool b);
        partial void Ctrl_Keepwarm_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_em23ondemand_bit;
        partial void Ctrl_Em23ondemand_Write(bool a, bool b);
        partial void Ctrl_Em23ondemand_Read(bool a, bool b);
        partial void Ctrl_Em23ondemand_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_FORCEXI2GNDANA> ctrl_forcexi2gndana_bit;
        partial void Ctrl_Forcexi2gndana_Write(CTRL_FORCEXI2GNDANA a, CTRL_FORCEXI2GNDANA b);
        partial void Ctrl_Forcexi2gndana_Read(CTRL_FORCEXI2GNDANA a, CTRL_FORCEXI2GNDANA b);
        partial void Ctrl_Forcexi2gndana_ValueProvider(CTRL_FORCEXI2GNDANA a);
        protected IEnumRegisterField<CTRL_FORCEXO2GNDANA> ctrl_forcexo2gndana_bit;
        partial void Ctrl_Forcexo2gndana_Write(CTRL_FORCEXO2GNDANA a, CTRL_FORCEXO2GNDANA b);
        partial void Ctrl_Forcexo2gndana_Read(CTRL_FORCEXO2GNDANA a, CTRL_FORCEXO2GNDANA b);
        partial void Ctrl_Forcexo2gndana_ValueProvider(CTRL_FORCEXO2GNDANA a);
        protected IFlagRegisterField ctrl_forcectunemax_bit;
        partial void Ctrl_Forcectunemax_Write(bool a, bool b);
        partial void Ctrl_Forcectunemax_Read(bool a, bool b);
        partial void Ctrl_Forcectunemax_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_PRSSTATUSSEL0> ctrl_prsstatussel0_field;
        partial void Ctrl_Prsstatussel0_Write(CTRL_PRSSTATUSSEL0 a, CTRL_PRSSTATUSSEL0 b);
        partial void Ctrl_Prsstatussel0_Read(CTRL_PRSSTATUSSEL0 a, CTRL_PRSSTATUSSEL0 b);
        partial void Ctrl_Prsstatussel0_ValueProvider(CTRL_PRSSTATUSSEL0 a);
        protected IEnumRegisterField<CTRL_PRSSTATUSSEL1> ctrl_prsstatussel1_field;
        partial void Ctrl_Prsstatussel1_Write(CTRL_PRSSTATUSSEL1 a, CTRL_PRSSTATUSSEL1 b);
        partial void Ctrl_Prsstatussel1_Read(CTRL_PRSSTATUSSEL1 a, CTRL_PRSSTATUSSEL1 b);
        partial void Ctrl_Prsstatussel1_ValueProvider(CTRL_PRSSTATUSSEL1 a);
        protected IFlagRegisterField ctrl_forceen_bit;
        partial void Ctrl_Forceen_Write(bool a, bool b);
        partial void Ctrl_Forceen_Read(bool a, bool b);
        partial void Ctrl_Forceen_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_forceenprs_bit;
        partial void Ctrl_Forceenprs_Write(bool a, bool b);
        partial void Ctrl_Forceenprs_Read(bool a, bool b);
        partial void Ctrl_Forceenprs_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_forceenbufout_bit;
        partial void Ctrl_Forceenbufout_Write(bool a, bool b);
        partial void Ctrl_Forceenbufout_Read(bool a, bool b);
        partial void Ctrl_Forceenbufout_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_forceensysrtc_bit;
        partial void Ctrl_Forceensysrtc_Write(bool a, bool b);
        partial void Ctrl_Forceensysrtc_Read(bool a, bool b);
        partial void Ctrl_Forceensysrtc_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_disondemand_bit;
        partial void Ctrl_Disondemand_Write(bool a, bool b);
        partial void Ctrl_Disondemand_Read(bool a, bool b);
        partial void Ctrl_Disondemand_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_disondemandprs_bit;
        partial void Ctrl_Disondemandprs_Write(bool a, bool b);
        partial void Ctrl_Disondemandprs_Read(bool a, bool b);
        partial void Ctrl_Disondemandprs_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_disondemandbufout_bit;
        partial void Ctrl_Disondemandbufout_Write(bool a, bool b);
        partial void Ctrl_Disondemandbufout_Read(bool a, bool b);
        partial void Ctrl_Disondemandbufout_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_disondemandsysrtc_bit;
        partial void Ctrl_Disondemandsysrtc_Write(bool a, bool b);
        partial void Ctrl_Disondemandsysrtc_Read(bool a, bool b);
        partial void Ctrl_Disondemandsysrtc_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_forcerawclk_bit;
        partial void Ctrl_Forcerawclk_Write(bool a, bool b);
        partial void Ctrl_Forcerawclk_Read(bool a, bool b);
        partial void Ctrl_Forcerawclk_ValueProvider(bool a);

        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Pkdetctrl1 - Offset : 0x34
        protected IFlagRegisterField pkdetctrl1_notimeouterr_bit;
        partial void Pkdetctrl1_Notimeouterr_Write(bool a, bool b);
        partial void Pkdetctrl1_Notimeouterr_Read(bool a, bool b);
        partial void Pkdetctrl1_Notimeouterr_ValueProvider(bool a);

        partial void Pkdetctrl1_Write(uint a, uint b);
        partial void Pkdetctrl1_Read(uint a, uint b);
        
        // Lowpwrctrl - Offset : 0x38
        protected IFlagRegisterField lowpwrctrl_regtrimbwana_bit;
        partial void Lowpwrctrl_Regtrimbwana_Write(bool a, bool b);
        partial void Lowpwrctrl_Regtrimbwana_Read(bool a, bool b);
        partial void Lowpwrctrl_Regtrimbwana_ValueProvider(bool a);
        protected IEnumRegisterField<LOWPWRCTRL_SQBUFBIASRESANA> lowpwrctrl_sqbufbiasresana_field;
        partial void Lowpwrctrl_Sqbufbiasresana_Write(LOWPWRCTRL_SQBUFBIASRESANA a, LOWPWRCTRL_SQBUFBIASRESANA b);
        partial void Lowpwrctrl_Sqbufbiasresana_Read(LOWPWRCTRL_SQBUFBIASRESANA a, LOWPWRCTRL_SQBUFBIASRESANA b);
        partial void Lowpwrctrl_Sqbufbiasresana_ValueProvider(LOWPWRCTRL_SQBUFBIASRESANA a);
        protected IEnumRegisterField<LOWPWRCTRL_SQBUFBIASANA> lowpwrctrl_sqbufbiasana_field;
        partial void Lowpwrctrl_Sqbufbiasana_Write(LOWPWRCTRL_SQBUFBIASANA a, LOWPWRCTRL_SQBUFBIASANA b);
        partial void Lowpwrctrl_Sqbufbiasana_Read(LOWPWRCTRL_SQBUFBIASANA a, LOWPWRCTRL_SQBUFBIASANA b);
        partial void Lowpwrctrl_Sqbufbiasana_ValueProvider(LOWPWRCTRL_SQBUFBIASANA a);
        protected IEnumRegisterField<LOWPWRCTRL_TIMEOUTWARM> lowpwrctrl_timeoutwarm_field;
        partial void Lowpwrctrl_Timeoutwarm_Write(LOWPWRCTRL_TIMEOUTWARM a, LOWPWRCTRL_TIMEOUTWARM b);
        partial void Lowpwrctrl_Timeoutwarm_Read(LOWPWRCTRL_TIMEOUTWARM a, LOWPWRCTRL_TIMEOUTWARM b);
        partial void Lowpwrctrl_Timeoutwarm_ValueProvider(LOWPWRCTRL_TIMEOUTWARM a);
        protected IFlagRegisterField lowpwrctrl_shuntbiasanahpen_bit;
        partial void Lowpwrctrl_Shuntbiasanahpen_Write(bool a, bool b);
        partial void Lowpwrctrl_Shuntbiasanahpen_Read(bool a, bool b);
        partial void Lowpwrctrl_Shuntbiasanahpen_ValueProvider(bool a);
        protected IEnumRegisterField<LOWPWRCTRL_SHUNTBIASANALP> lowpwrctrl_shuntbiasanalp_field;
        partial void Lowpwrctrl_Shuntbiasanalp_Write(LOWPWRCTRL_SHUNTBIASANALP a, LOWPWRCTRL_SHUNTBIASANALP b);
        partial void Lowpwrctrl_Shuntbiasanalp_Read(LOWPWRCTRL_SHUNTBIASANALP a, LOWPWRCTRL_SHUNTBIASANALP b);
        partial void Lowpwrctrl_Shuntbiasanalp_ValueProvider(LOWPWRCTRL_SHUNTBIASANALP a);
        protected IEnumRegisterField<LOWPWRCTRL_SHUNTBIASANAHP> lowpwrctrl_shuntbiasanahp_field;
        partial void Lowpwrctrl_Shuntbiasanahp_Write(LOWPWRCTRL_SHUNTBIASANAHP a, LOWPWRCTRL_SHUNTBIASANAHP b);
        partial void Lowpwrctrl_Shuntbiasanahp_Read(LOWPWRCTRL_SHUNTBIASANAHP a, LOWPWRCTRL_SHUNTBIASANAHP b);
        partial void Lowpwrctrl_Shuntbiasanahp_ValueProvider(LOWPWRCTRL_SHUNTBIASANAHP a);

        partial void Lowpwrctrl_Write(uint a, uint b);
        partial void Lowpwrctrl_Read(uint a, uint b);
        
        // Pkdetctrl - Offset : 0x3C
        protected IFlagRegisterField pkdetctrl_enpkdetana_bit;
        partial void Pkdetctrl_Enpkdetana_Write(bool a, bool b);
        partial void Pkdetctrl_Enpkdetana_Read(bool a, bool b);
        partial void Pkdetctrl_Enpkdetana_ValueProvider(bool a);
        protected IEnumRegisterField<PKDETCTRL_PKDETNODEANA> pkdetctrl_pkdetnodeana_bit;
        partial void Pkdetctrl_Pkdetnodeana_Write(PKDETCTRL_PKDETNODEANA a, PKDETCTRL_PKDETNODEANA b);
        partial void Pkdetctrl_Pkdetnodeana_Read(PKDETCTRL_PKDETNODEANA a, PKDETCTRL_PKDETNODEANA b);
        partial void Pkdetctrl_Pkdetnodeana_ValueProvider(PKDETCTRL_PKDETNODEANA a);
        protected IEnumRegisterField<PKDETCTRL_PKDETNODESTARTUPI> pkdetctrl_pkdetnodestartupi_bit;
        partial void Pkdetctrl_Pkdetnodestartupi_Write(PKDETCTRL_PKDETNODESTARTUPI a, PKDETCTRL_PKDETNODESTARTUPI b);
        partial void Pkdetctrl_Pkdetnodestartupi_Read(PKDETCTRL_PKDETNODESTARTUPI a, PKDETCTRL_PKDETNODESTARTUPI b);
        partial void Pkdetctrl_Pkdetnodestartupi_ValueProvider(PKDETCTRL_PKDETNODESTARTUPI a);
        protected IEnumRegisterField<PKDETCTRL_PKDETNODESTARTUP> pkdetctrl_pkdetnodestartup_bit;
        partial void Pkdetctrl_Pkdetnodestartup_Write(PKDETCTRL_PKDETNODESTARTUP a, PKDETCTRL_PKDETNODESTARTUP b);
        partial void Pkdetctrl_Pkdetnodestartup_Read(PKDETCTRL_PKDETNODESTARTUP a, PKDETCTRL_PKDETNODESTARTUP b);
        partial void Pkdetctrl_Pkdetnodestartup_ValueProvider(PKDETCTRL_PKDETNODESTARTUP a);
        protected IEnumRegisterField<PKDETCTRL_PKDETTHANA> pkdetctrl_pkdetthana_field;
        partial void Pkdetctrl_Pkdetthana_Write(PKDETCTRL_PKDETTHANA a, PKDETCTRL_PKDETTHANA b);
        partial void Pkdetctrl_Pkdetthana_Read(PKDETCTRL_PKDETTHANA a, PKDETCTRL_PKDETTHANA b);
        partial void Pkdetctrl_Pkdetthana_ValueProvider(PKDETCTRL_PKDETTHANA a);
        protected IEnumRegisterField<PKDETCTRL_PKDETTHSTARTUPI> pkdetctrl_pkdetthstartupi_field;
        partial void Pkdetctrl_Pkdetthstartupi_Write(PKDETCTRL_PKDETTHSTARTUPI a, PKDETCTRL_PKDETTHSTARTUPI b);
        partial void Pkdetctrl_Pkdetthstartupi_Read(PKDETCTRL_PKDETTHSTARTUPI a, PKDETCTRL_PKDETTHSTARTUPI b);
        partial void Pkdetctrl_Pkdetthstartupi_ValueProvider(PKDETCTRL_PKDETTHSTARTUPI a);
        protected IEnumRegisterField<PKDETCTRL_PKDETTHSTARTUP> pkdetctrl_pkdetthstartup_field;
        partial void Pkdetctrl_Pkdetthstartup_Write(PKDETCTRL_PKDETTHSTARTUP a, PKDETCTRL_PKDETTHSTARTUP b);
        partial void Pkdetctrl_Pkdetthstartup_Read(PKDETCTRL_PKDETTHSTARTUP a, PKDETCTRL_PKDETTHSTARTUP b);
        partial void Pkdetctrl_Pkdetthstartup_ValueProvider(PKDETCTRL_PKDETTHSTARTUP a);
        protected IEnumRegisterField<PKDETCTRL_PKDETTHHIGH> pkdetctrl_pkdetthhigh_field;
        partial void Pkdetctrl_Pkdetthhigh_Write(PKDETCTRL_PKDETTHHIGH a, PKDETCTRL_PKDETTHHIGH b);
        partial void Pkdetctrl_Pkdetthhigh_Read(PKDETCTRL_PKDETTHHIGH a, PKDETCTRL_PKDETTHHIGH b);
        partial void Pkdetctrl_Pkdetthhigh_ValueProvider(PKDETCTRL_PKDETTHHIGH a);
        protected IValueRegisterField pkdetctrl_pkdetstep_field;
        partial void Pkdetctrl_Pkdetstep_Write(ulong a, ulong b);
        partial void Pkdetctrl_Pkdetstep_Read(ulong a, ulong b);
        partial void Pkdetctrl_Pkdetstep_ValueProvider(ulong a);
        protected IFlagRegisterField pkdetctrl_enpkdetfsm_bit;
        partial void Pkdetctrl_Enpkdetfsm_Write(bool a, bool b);
        partial void Pkdetctrl_Enpkdetfsm_Read(bool a, bool b);
        partial void Pkdetctrl_Enpkdetfsm_ValueProvider(bool a);
        protected IEnumRegisterField<PKDETCTRL_TIMEOUTPKDET> pkdetctrl_timeoutpkdet_field;
        partial void Pkdetctrl_Timeoutpkdet_Write(PKDETCTRL_TIMEOUTPKDET a, PKDETCTRL_TIMEOUTPKDET b);
        partial void Pkdetctrl_Timeoutpkdet_Read(PKDETCTRL_TIMEOUTPKDET a, PKDETCTRL_TIMEOUTPKDET b);
        partial void Pkdetctrl_Timeoutpkdet_ValueProvider(PKDETCTRL_TIMEOUTPKDET a);
        protected IEnumRegisterField<PKDETCTRL_REGLVLANA> pkdetctrl_reglvlana_bit;
        partial void Pkdetctrl_Reglvlana_Write(PKDETCTRL_REGLVLANA a, PKDETCTRL_REGLVLANA b);
        partial void Pkdetctrl_Reglvlana_Read(PKDETCTRL_REGLVLANA a, PKDETCTRL_REGLVLANA b);
        partial void Pkdetctrl_Reglvlana_ValueProvider(PKDETCTRL_REGLVLANA a);
        protected IEnumRegisterField<PKDETCTRL_REGLVLSTARTUP> pkdetctrl_reglvlstartup_bit;
        partial void Pkdetctrl_Reglvlstartup_Write(PKDETCTRL_REGLVLSTARTUP a, PKDETCTRL_REGLVLSTARTUP b);
        partial void Pkdetctrl_Reglvlstartup_Read(PKDETCTRL_REGLVLSTARTUP a, PKDETCTRL_REGLVLSTARTUP b);
        partial void Pkdetctrl_Reglvlstartup_ValueProvider(PKDETCTRL_REGLVLSTARTUP a);

        partial void Pkdetctrl_Write(uint a, uint b);
        partial void Pkdetctrl_Read(uint a, uint b);
        
        // Extclkpkdetctrl - Offset : 0x40
        protected IValueRegisterField extclkpkdetctrl_pkdetthextclk0_field;
        partial void Extclkpkdetctrl_Pkdetthextclk0_Write(ulong a, ulong b);
        partial void Extclkpkdetctrl_Pkdetthextclk0_Read(ulong a, ulong b);
        partial void Extclkpkdetctrl_Pkdetthextclk0_ValueProvider(ulong a);
        protected IValueRegisterField extclkpkdetctrl_pkdetthextclk1_field;
        partial void Extclkpkdetctrl_Pkdetthextclk1_Write(ulong a, ulong b);
        partial void Extclkpkdetctrl_Pkdetthextclk1_Read(ulong a, ulong b);
        partial void Extclkpkdetctrl_Pkdetthextclk1_ValueProvider(ulong a);

        partial void Extclkpkdetctrl_Write(uint a, uint b);
        partial void Extclkpkdetctrl_Read(uint a, uint b);
        
        // Internalctrl - Offset : 0x44
        protected IFlagRegisterField internalctrl_enregana_bit;
        partial void Internalctrl_Enregana_Write(bool a, bool b);
        partial void Internalctrl_Enregana_Read(bool a, bool b);
        partial void Internalctrl_Enregana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_ensqbufana_bit;
        partial void Internalctrl_Ensqbufana_Write(bool a, bool b);
        partial void Internalctrl_Ensqbufana_Read(bool a, bool b);
        partial void Internalctrl_Ensqbufana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_encoreana_bit;
        partial void Internalctrl_Encoreana_Write(bool a, bool b);
        partial void Internalctrl_Encoreana_Read(bool a, bool b);
        partial void Internalctrl_Encoreana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enshuntregana_bit;
        partial void Internalctrl_Enshuntregana_Write(bool a, bool b);
        partial void Internalctrl_Enshuntregana_Read(bool a, bool b);
        partial void Internalctrl_Enshuntregana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_shortxi2xoana_bit;
        partial void Internalctrl_Shortxi2xoana_Write(bool a, bool b);
        partial void Internalctrl_Shortxi2xoana_Read(bool a, bool b);
        partial void Internalctrl_Shortxi2xoana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_shortxi2xofsm_bit;
        partial void Internalctrl_Shortxi2xofsm_Write(bool a, bool b);
        partial void Internalctrl_Shortxi2xofsm_Read(bool a, bool b);
        partial void Internalctrl_Shortxi2xofsm_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enhighgmmodeana_bit;
        partial void Internalctrl_Enhighgmmodeana_Write(bool a, bool b);
        partial void Internalctrl_Enhighgmmodeana_Read(bool a, bool b);
        partial void Internalctrl_Enhighgmmodeana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enhighgmmodefsm_bit;
        partial void Internalctrl_Enhighgmmodefsm_Write(bool a, bool b);
        partial void Internalctrl_Enhighgmmodefsm_Read(bool a, bool b);
        partial void Internalctrl_Enhighgmmodefsm_ValueProvider(bool a);
        protected IEnumRegisterField<INTERNALCTRL_SQBUFFILTANA> internalctrl_sqbuffiltana_field;
        partial void Internalctrl_Sqbuffiltana_Write(INTERNALCTRL_SQBUFFILTANA a, INTERNALCTRL_SQBUFFILTANA b);
        partial void Internalctrl_Sqbuffiltana_Read(INTERNALCTRL_SQBUFFILTANA a, INTERNALCTRL_SQBUFFILTANA b);
        partial void Internalctrl_Sqbuffiltana_ValueProvider(INTERNALCTRL_SQBUFFILTANA a);
        protected IFlagRegisterField internalctrl_enclkdifana_bit;
        partial void Internalctrl_Enclkdifana_Write(bool a, bool b);
        partial void Internalctrl_Enclkdifana_Read(bool a, bool b);
        partial void Internalctrl_Enclkdifana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_eninjana_bit;
        partial void Internalctrl_Eninjana_Write(bool a, bool b);
        partial void Internalctrl_Eninjana_Read(bool a, bool b);
        partial void Internalctrl_Eninjana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_eninjfsm_bit;
        partial void Internalctrl_Eninjfsm_Write(bool a, bool b);
        partial void Internalctrl_Eninjfsm_Read(bool a, bool b);
        partial void Internalctrl_Eninjfsm_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enclkdigana_bit;
        partial void Internalctrl_Enclkdigana_Write(bool a, bool b);
        partial void Internalctrl_Enclkdigana_Read(bool a, bool b);
        partial void Internalctrl_Enclkdigana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enclkauxadcana_bit;
        partial void Internalctrl_Enclkauxadcana_Write(bool a, bool b);
        partial void Internalctrl_Enclkauxadcana_Read(bool a, bool b);
        partial void Internalctrl_Enclkauxadcana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enclkclkmultana_bit;
        partial void Internalctrl_Enclkclkmultana_Write(bool a, bool b);
        partial void Internalctrl_Enclkclkmultana_Read(bool a, bool b);
        partial void Internalctrl_Enclkclkmultana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enclksyana_bit;
        partial void Internalctrl_Enclksyana_Write(bool a, bool b);
        partial void Internalctrl_Enclksyana_Read(bool a, bool b);
        partial void Internalctrl_Enclksyana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enclktxana_bit;
        partial void Internalctrl_Enclktxana_Write(bool a, bool b);
        partial void Internalctrl_Enclktxana_Read(bool a, bool b);
        partial void Internalctrl_Enclktxana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_invclkdigana_bit;
        partial void Internalctrl_Invclkdigana_Write(bool a, bool b);
        partial void Internalctrl_Invclkdigana_Read(bool a, bool b);
        partial void Internalctrl_Invclkdigana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_invclkauxadcana_bit;
        partial void Internalctrl_Invclkauxadcana_Write(bool a, bool b);
        partial void Internalctrl_Invclkauxadcana_Read(bool a, bool b);
        partial void Internalctrl_Invclkauxadcana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_invclkclkmultana_bit;
        partial void Internalctrl_Invclkclkmultana_Write(bool a, bool b);
        partial void Internalctrl_Invclkclkmultana_Read(bool a, bool b);
        partial void Internalctrl_Invclkclkmultana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_invclksyana_bit;
        partial void Internalctrl_Invclksyana_Write(bool a, bool b);
        partial void Internalctrl_Invclksyana_Read(bool a, bool b);
        partial void Internalctrl_Invclksyana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_invclktxana_bit;
        partial void Internalctrl_Invclktxana_Write(bool a, bool b);
        partial void Internalctrl_Invclktxana_Read(bool a, bool b);
        partial void Internalctrl_Invclktxana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_eninjtempcompfsm_bit;
        partial void Internalctrl_Eninjtempcompfsm_Write(bool a, bool b);
        partial void Internalctrl_Eninjtempcompfsm_Read(bool a, bool b);
        partial void Internalctrl_Eninjtempcompfsm_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_eninjsyseqfsm_bit;
        partial void Internalctrl_Eninjsyseqfsm_Write(bool a, bool b);
        partial void Internalctrl_Eninjsyseqfsm_Read(bool a, bool b);
        partial void Internalctrl_Eninjsyseqfsm_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_ensleepyxtalerrstate_bit;
        partial void Internalctrl_Ensleepyxtalerrstate_Write(bool a, bool b);
        partial void Internalctrl_Ensleepyxtalerrstate_Read(bool a, bool b);
        partial void Internalctrl_Ensleepyxtalerrstate_ValueProvider(bool a);
        protected IEnumRegisterField<INTERNALCTRL_VTRCOREDISSTARTUPANA> internalctrl_vtrcoredisstartupana_bit;
        partial void Internalctrl_Vtrcoredisstartupana_Write(INTERNALCTRL_VTRCOREDISSTARTUPANA a, INTERNALCTRL_VTRCOREDISSTARTUPANA b);
        partial void Internalctrl_Vtrcoredisstartupana_Read(INTERNALCTRL_VTRCOREDISSTARTUPANA a, INTERNALCTRL_VTRCOREDISSTARTUPANA b);
        partial void Internalctrl_Vtrcoredisstartupana_ValueProvider(INTERNALCTRL_VTRCOREDISSTARTUPANA a);

        partial void Internalctrl_Write(uint a, uint b);
        partial void Internalctrl_Read(uint a, uint b);
        
        // Internalxoutctrl - Offset : 0x48
        protected IFlagRegisterField internalxoutctrl_envregbiasana_bit;
        partial void Internalxoutctrl_Envregbiasana_Write(bool a, bool b);
        partial void Internalxoutctrl_Envregbiasana_Read(bool a, bool b);
        partial void Internalxoutctrl_Envregbiasana_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_envregana_bit;
        partial void Internalxoutctrl_Envregana_Write(bool a, bool b);
        partial void Internalxoutctrl_Envregana_Read(bool a, bool b);
        partial void Internalxoutctrl_Envregana_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_vtrforcestartupana_bit;
        partial void Internalxoutctrl_Vtrforcestartupana_Write(bool a, bool b);
        partial void Internalxoutctrl_Vtrforcestartupana_Read(bool a, bool b);
        partial void Internalxoutctrl_Vtrforcestartupana_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_vtrforcestartupfsm_bit;
        partial void Internalxoutctrl_Vtrforcestartupfsm_Write(bool a, bool b);
        partial void Internalxoutctrl_Vtrforcestartupfsm_Read(bool a, bool b);
        partial void Internalxoutctrl_Vtrforcestartupfsm_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_enxoutana_bit;
        partial void Internalxoutctrl_Enxoutana_Write(bool a, bool b);
        partial void Internalxoutctrl_Enxoutana_Read(bool a, bool b);
        partial void Internalxoutctrl_Enxoutana_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_enpeakdetana_bit;
        partial void Internalxoutctrl_Enpeakdetana_Write(bool a, bool b);
        partial void Internalxoutctrl_Enpeakdetana_Read(bool a, bool b);
        partial void Internalxoutctrl_Enpeakdetana_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_enpeakdetfsm_bit;
        partial void Internalxoutctrl_Enpeakdetfsm_Write(bool a, bool b);
        partial void Internalxoutctrl_Enpeakdetfsm_Read(bool a, bool b);
        partial void Internalxoutctrl_Enpeakdetfsm_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_enib2p5uapana_bit;
        partial void Internalxoutctrl_Enib2p5uapana_Write(bool a, bool b);
        partial void Internalxoutctrl_Enib2p5uapana_Read(bool a, bool b);
        partial void Internalxoutctrl_Enib2p5uapana_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_enib2p5uapfsm_bit;
        partial void Internalxoutctrl_Enib2p5uapfsm_Write(bool a, bool b);
        partial void Internalxoutctrl_Enib2p5uapfsm_Read(bool a, bool b);
        partial void Internalxoutctrl_Enib2p5uapfsm_ValueProvider(bool a);
        protected IFlagRegisterField internalxoutctrl_envregloadana_bit;
        partial void Internalxoutctrl_Envregloadana_Write(bool a, bool b);
        partial void Internalxoutctrl_Envregloadana_Read(bool a, bool b);
        partial void Internalxoutctrl_Envregloadana_ValueProvider(bool a);

        partial void Internalxoutctrl_Write(uint a, uint b);
        partial void Internalxoutctrl_Read(uint a, uint b);
        
        // Bufouttrim - Offset : 0x4C
        protected IValueRegisterField bufouttrim_vtrtrimana_field;
        partial void Bufouttrim_Vtrtrimana_Write(ulong a, ulong b);
        partial void Bufouttrim_Vtrtrimana_Read(ulong a, ulong b);
        partial void Bufouttrim_Vtrtrimana_ValueProvider(ulong a);

        partial void Bufouttrim_Write(uint a, uint b);
        partial void Bufouttrim_Read(uint a, uint b);
        
        // Bufoutctrl - Offset : 0x50
        protected IValueRegisterField bufoutctrl_xoutbiasana_field;
        partial void Bufoutctrl_Xoutbiasana_Write(ulong a, ulong b);
        partial void Bufoutctrl_Xoutbiasana_Read(ulong a, ulong b);
        partial void Bufoutctrl_Xoutbiasana_ValueProvider(ulong a);
        protected IValueRegisterField bufoutctrl_xoutcfana_field;
        partial void Bufoutctrl_Xoutcfana_Write(ulong a, ulong b);
        partial void Bufoutctrl_Xoutcfana_Read(ulong a, ulong b);
        partial void Bufoutctrl_Xoutcfana_ValueProvider(ulong a);
        protected IValueRegisterField bufoutctrl_xoutgmana_field;
        partial void Bufoutctrl_Xoutgmana_Write(ulong a, ulong b);
        partial void Bufoutctrl_Xoutgmana_Read(ulong a, ulong b);
        partial void Bufoutctrl_Xoutgmana_ValueProvider(ulong a);
        protected IValueRegisterField bufoutctrl_peakdetthresana_field;
        partial void Bufoutctrl_Peakdetthresana_Write(ulong a, ulong b);
        partial void Bufoutctrl_Peakdetthresana_Read(ulong a, ulong b);
        partial void Bufoutctrl_Peakdetthresana_ValueProvider(ulong a);
        protected IEnumRegisterField<BUFOUTCTRL_TIMEOUTCTUNE> bufoutctrl_timeoutctune_field;
        partial void Bufoutctrl_Timeoutctune_Write(BUFOUTCTRL_TIMEOUTCTUNE a, BUFOUTCTRL_TIMEOUTCTUNE b);
        partial void Bufoutctrl_Timeoutctune_Read(BUFOUTCTRL_TIMEOUTCTUNE a, BUFOUTCTRL_TIMEOUTCTUNE b);
        partial void Bufoutctrl_Timeoutctune_ValueProvider(BUFOUTCTRL_TIMEOUTCTUNE a);
        protected IEnumRegisterField<BUFOUTCTRL_TIMEOUTSTARTUP> bufoutctrl_timeoutstartup_field;
        partial void Bufoutctrl_Timeoutstartup_Write(BUFOUTCTRL_TIMEOUTSTARTUP a, BUFOUTCTRL_TIMEOUTSTARTUP b);
        partial void Bufoutctrl_Timeoutstartup_Read(BUFOUTCTRL_TIMEOUTSTARTUP a, BUFOUTCTRL_TIMEOUTSTARTUP b);
        partial void Bufoutctrl_Timeoutstartup_ValueProvider(BUFOUTCTRL_TIMEOUTSTARTUP a);
        protected IFlagRegisterField bufoutctrl_allowcorebiasopt_bit;
        partial void Bufoutctrl_Allowcorebiasopt_Write(bool a, bool b);
        partial void Bufoutctrl_Allowcorebiasopt_Read(bool a, bool b);
        partial void Bufoutctrl_Allowcorebiasopt_ValueProvider(bool a);
        protected IFlagRegisterField bufoutctrl_minimumstartupdelay_bit;
        partial void Bufoutctrl_Minimumstartupdelay_Write(bool a, bool b);
        partial void Bufoutctrl_Minimumstartupdelay_Read(bool a, bool b);
        partial void Bufoutctrl_Minimumstartupdelay_ValueProvider(bool a);

        partial void Bufoutctrl_Write(uint a, uint b);
        partial void Bufoutctrl_Read(uint a, uint b);
        
        // Cmd - Offset : 0x54
        protected IFlagRegisterField cmd_corebiasopt_bit;
        partial void Cmd_Corebiasopt_Write(bool a, bool b);
        partial void Cmd_Corebiasopt_ValueProvider(bool a);
        protected IFlagRegisterField cmd_startmeas_bit;
        partial void Cmd_Startmeas_Write(bool a, bool b);
        partial void Cmd_Startmeas_ValueProvider(bool a);
        protected IFlagRegisterField cmd_stopmeas_bit;
        partial void Cmd_Stopmeas_Write(bool a, bool b);
        partial void Cmd_Stopmeas_ValueProvider(bool a);

        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);
        
        // Status - Offset : 0x58
        protected IFlagRegisterField status_rdy_bit;
        partial void Status_Rdy_Read(bool a, bool b);
        partial void Status_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField status_corebiasoptrdy_bit;
        partial void Status_Corebiasoptrdy_Read(bool a, bool b);
        partial void Status_Corebiasoptrdy_ValueProvider(bool a);
        protected IFlagRegisterField status_prsrdy_bit;
        partial void Status_Prsrdy_Read(bool a, bool b);
        partial void Status_Prsrdy_ValueProvider(bool a);
        protected IFlagRegisterField status_bufoutrdy_bit;
        partial void Status_Bufoutrdy_Read(bool a, bool b);
        partial void Status_Bufoutrdy_ValueProvider(bool a);
        protected IFlagRegisterField status_sysrtcrdy_bit;
        partial void Status_Sysrtcrdy_Read(bool a, bool b);
        partial void Status_Sysrtcrdy_ValueProvider(bool a);
        protected IFlagRegisterField status_sleepyxtal_bit;
        partial void Status_Sleepyxtal_Read(bool a, bool b);
        partial void Status_Sleepyxtal_ValueProvider(bool a);
        protected IFlagRegisterField status_sleepyxtalerr_bit;
        partial void Status_Sleepyxtalerr_Read(bool a, bool b);
        partial void Status_Sleepyxtalerr_ValueProvider(bool a);
        protected IFlagRegisterField status_bufoutfrozen_bit;
        partial void Status_Bufoutfrozen_Read(bool a, bool b);
        partial void Status_Bufoutfrozen_ValueProvider(bool a);
        protected IFlagRegisterField status_ens_bit;
        partial void Status_Ens_Read(bool a, bool b);
        partial void Status_Ens_ValueProvider(bool a);
        protected IFlagRegisterField status_hwreq_bit;
        partial void Status_Hwreq_Read(bool a, bool b);
        partial void Status_Hwreq_ValueProvider(bool a);
        protected IFlagRegisterField status_isforced_bit;
        partial void Status_Isforced_Read(bool a, bool b);
        partial void Status_Isforced_ValueProvider(bool a);
        protected IFlagRegisterField status_iswarm_bit;
        partial void Status_Iswarm_Read(bool a, bool b);
        partial void Status_Iswarm_ValueProvider(bool a);
        protected IFlagRegisterField status_prshwreq_bit;
        partial void Status_Prshwreq_Read(bool a, bool b);
        partial void Status_Prshwreq_ValueProvider(bool a);
        protected IFlagRegisterField status_bufouthwreq_bit;
        partial void Status_Bufouthwreq_Read(bool a, bool b);
        partial void Status_Bufouthwreq_ValueProvider(bool a);
        protected IFlagRegisterField status_sysrtchwreq_bit;
        partial void Status_Sysrtchwreq_Read(bool a, bool b);
        partial void Status_Sysrtchwreq_ValueProvider(bool a);
        protected IFlagRegisterField status_stupmeasbsy_bit;
        partial void Status_Stupmeasbsy_Read(bool a, bool b);
        partial void Status_Stupmeasbsy_ValueProvider(bool a);
        protected IFlagRegisterField status_injbsy_bit;
        partial void Status_Injbsy_Read(bool a, bool b);
        partial void Status_Injbsy_ValueProvider(bool a);
        protected IFlagRegisterField status_syncbusy_bit;
        partial void Status_Syncbusy_Read(bool a, bool b);
        partial void Status_Syncbusy_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // Avgstartuptime - Offset : 0x5C
        protected IValueRegisterField avgstartuptime_avgstup_field;
        partial void Avgstartuptime_Avgstup_Read(ulong a, ulong b);
        partial void Avgstartuptime_Avgstup_ValueProvider(ulong a);

        partial void Avgstartuptime_Write(uint a, uint b);
        partial void Avgstartuptime_Read(uint a, uint b);
        
        // Dbgctrl - Offset : 0x60
        protected IEnumRegisterField<DBGCTRL_PRSDBGSEL0> dbgctrl_prsdbgsel0_field;
        partial void Dbgctrl_Prsdbgsel0_Write(DBGCTRL_PRSDBGSEL0 a, DBGCTRL_PRSDBGSEL0 b);
        partial void Dbgctrl_Prsdbgsel0_Read(DBGCTRL_PRSDBGSEL0 a, DBGCTRL_PRSDBGSEL0 b);
        partial void Dbgctrl_Prsdbgsel0_ValueProvider(DBGCTRL_PRSDBGSEL0 a);
        protected IEnumRegisterField<DBGCTRL_PRSDBGSEL1> dbgctrl_prsdbgsel1_field;
        partial void Dbgctrl_Prsdbgsel1_Write(DBGCTRL_PRSDBGSEL1 a, DBGCTRL_PRSDBGSEL1 b);
        partial void Dbgctrl_Prsdbgsel1_Read(DBGCTRL_PRSDBGSEL1 a, DBGCTRL_PRSDBGSEL1 b);
        partial void Dbgctrl_Prsdbgsel1_ValueProvider(DBGCTRL_PRSDBGSEL1 a);

        partial void Dbgctrl_Write(uint a, uint b);
        partial void Dbgctrl_Read(uint a, uint b);
        
        // Dbgstatus - Offset : 0x64
        protected IEnumRegisterField<DBGSTATUS_PKDETSTATUS> dbgstatus_pkdetstatus_bit;
        partial void Dbgstatus_Pkdetstatus_Read(DBGSTATUS_PKDETSTATUS a, DBGSTATUS_PKDETSTATUS b);
        partial void Dbgstatus_Pkdetstatus_ValueProvider(DBGSTATUS_PKDETSTATUS a);
        protected IFlagRegisterField dbgstatus_startupdone_bit;
        partial void Dbgstatus_Startupdone_Read(bool a, bool b);
        partial void Dbgstatus_Startupdone_ValueProvider(bool a);
        protected IEnumRegisterField<DBGSTATUS_XOUTPKDETSTATUS> dbgstatus_xoutpkdetstatus_bit;
        partial void Dbgstatus_Xoutpkdetstatus_Read(DBGSTATUS_XOUTPKDETSTATUS a, DBGSTATUS_XOUTPKDETSTATUS b);
        partial void Dbgstatus_Xoutpkdetstatus_ValueProvider(DBGSTATUS_XOUTPKDETSTATUS a);
        protected IValueRegisterField dbgstatus_accstupmeas_field;
        partial void Dbgstatus_Accstupmeas_Read(ulong a, ulong b);
        partial void Dbgstatus_Accstupmeas_ValueProvider(ulong a);
        protected IValueRegisterField dbgstatus_stupmeas_field;
        partial void Dbgstatus_Stupmeas_Read(ulong a, ulong b);
        partial void Dbgstatus_Stupmeas_ValueProvider(ulong a);

        partial void Dbgstatus_Write(uint a, uint b);
        partial void Dbgstatus_Read(uint a, uint b);
        
        // If - Offset : 0x70
        protected IFlagRegisterField if_rdy_bit;
        partial void If_Rdy_Write(bool a, bool b);
        partial void If_Rdy_Read(bool a, bool b);
        partial void If_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField if_corebiasoptrdy_bit;
        partial void If_Corebiasoptrdy_Write(bool a, bool b);
        partial void If_Corebiasoptrdy_Read(bool a, bool b);
        partial void If_Corebiasoptrdy_ValueProvider(bool a);
        protected IFlagRegisterField if_prsrdy_bit;
        partial void If_Prsrdy_Write(bool a, bool b);
        partial void If_Prsrdy_Read(bool a, bool b);
        partial void If_Prsrdy_ValueProvider(bool a);
        protected IFlagRegisterField if_bufoutrdy_bit;
        partial void If_Bufoutrdy_Write(bool a, bool b);
        partial void If_Bufoutrdy_Read(bool a, bool b);
        partial void If_Bufoutrdy_ValueProvider(bool a);
        protected IFlagRegisterField if_sysrtcrdy_bit;
        partial void If_Sysrtcrdy_Write(bool a, bool b);
        partial void If_Sysrtcrdy_Read(bool a, bool b);
        partial void If_Sysrtcrdy_ValueProvider(bool a);
        protected IFlagRegisterField if_stupmeasdone_bit;
        partial void If_Stupmeasdone_Write(bool a, bool b);
        partial void If_Stupmeasdone_Read(bool a, bool b);
        partial void If_Stupmeasdone_ValueProvider(bool a);
        protected IFlagRegisterField if_sleepyxtal_bit;
        partial void If_Sleepyxtal_Write(bool a, bool b);
        partial void If_Sleepyxtal_Read(bool a, bool b);
        partial void If_Sleepyxtal_ValueProvider(bool a);
        protected IFlagRegisterField if_injskip_bit;
        partial void If_Injskip_Write(bool a, bool b);
        partial void If_Injskip_Read(bool a, bool b);
        partial void If_Injskip_ValueProvider(bool a);
        protected IFlagRegisterField if_bufoutfrozen_bit;
        partial void If_Bufoutfrozen_Write(bool a, bool b);
        partial void If_Bufoutfrozen_Read(bool a, bool b);
        partial void If_Bufoutfrozen_ValueProvider(bool a);
        protected IFlagRegisterField if_prserr_bit;
        partial void If_Prserr_Write(bool a, bool b);
        partial void If_Prserr_Read(bool a, bool b);
        partial void If_Prserr_ValueProvider(bool a);
        protected IFlagRegisterField if_bufouterr_bit;
        partial void If_Bufouterr_Write(bool a, bool b);
        partial void If_Bufouterr_Read(bool a, bool b);
        partial void If_Bufouterr_ValueProvider(bool a);
        protected IFlagRegisterField if_sysrtcerr_bit;
        partial void If_Sysrtcerr_Write(bool a, bool b);
        partial void If_Sysrtcerr_Read(bool a, bool b);
        partial void If_Sysrtcerr_ValueProvider(bool a);
        protected IFlagRegisterField if_injerr_bit;
        partial void If_Injerr_Write(bool a, bool b);
        partial void If_Injerr_Read(bool a, bool b);
        partial void If_Injerr_ValueProvider(bool a);
        protected IFlagRegisterField if_dnssleepyxtalerr_bit;
        partial void If_Dnssleepyxtalerr_Write(bool a, bool b);
        partial void If_Dnssleepyxtalerr_Read(bool a, bool b);
        partial void If_Dnssleepyxtalerr_ValueProvider(bool a);
        protected IFlagRegisterField if_bufoutfreezeerr_bit;
        partial void If_Bufoutfreezeerr_Write(bool a, bool b);
        partial void If_Bufoutfreezeerr_Read(bool a, bool b);
        partial void If_Bufoutfreezeerr_ValueProvider(bool a);
        protected IFlagRegisterField if_bufoutdnserr_bit;
        partial void If_Bufoutdnserr_Write(bool a, bool b);
        partial void If_Bufoutdnserr_Read(bool a, bool b);
        partial void If_Bufoutdnserr_ValueProvider(bool a);
        protected IFlagRegisterField if_dnserr_bit;
        partial void If_Dnserr_Write(bool a, bool b);
        partial void If_Dnserr_Read(bool a, bool b);
        partial void If_Dnserr_ValueProvider(bool a);
        protected IFlagRegisterField if_lftimeouterr_bit;
        partial void If_Lftimeouterr_Write(bool a, bool b);
        partial void If_Lftimeouterr_Read(bool a, bool b);
        partial void If_Lftimeouterr_ValueProvider(bool a);
        protected IFlagRegisterField if_corebiasopterr_bit;
        partial void If_Corebiasopterr_Write(bool a, bool b);
        partial void If_Corebiasopterr_Read(bool a, bool b);
        partial void If_Corebiasopterr_ValueProvider(bool a);

        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x74
        protected IFlagRegisterField ien_rdy_bit;
        partial void Ien_Rdy_Write(bool a, bool b);
        partial void Ien_Rdy_Read(bool a, bool b);
        partial void Ien_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField ien_corebiasoptrdy_bit;
        partial void Ien_Corebiasoptrdy_Write(bool a, bool b);
        partial void Ien_Corebiasoptrdy_Read(bool a, bool b);
        partial void Ien_Corebiasoptrdy_ValueProvider(bool a);
        protected IFlagRegisterField ien_prsrdy_bit;
        partial void Ien_Prsrdy_Write(bool a, bool b);
        partial void Ien_Prsrdy_Read(bool a, bool b);
        partial void Ien_Prsrdy_ValueProvider(bool a);
        protected IFlagRegisterField ien_bufoutrdy_bit;
        partial void Ien_Bufoutrdy_Write(bool a, bool b);
        partial void Ien_Bufoutrdy_Read(bool a, bool b);
        partial void Ien_Bufoutrdy_ValueProvider(bool a);
        protected IFlagRegisterField ien_sysrtcrdy_bit;
        partial void Ien_Sysrtcrdy_Write(bool a, bool b);
        partial void Ien_Sysrtcrdy_Read(bool a, bool b);
        partial void Ien_Sysrtcrdy_ValueProvider(bool a);
        protected IFlagRegisterField ien_stupmeasdone_bit;
        partial void Ien_Stupmeasdone_Write(bool a, bool b);
        partial void Ien_Stupmeasdone_Read(bool a, bool b);
        partial void Ien_Stupmeasdone_ValueProvider(bool a);
        protected IFlagRegisterField ien_sleepyxtal_bit;
        partial void Ien_Sleepyxtal_Write(bool a, bool b);
        partial void Ien_Sleepyxtal_Read(bool a, bool b);
        partial void Ien_Sleepyxtal_ValueProvider(bool a);
        protected IFlagRegisterField ien_injskip_bit;
        partial void Ien_Injskip_Write(bool a, bool b);
        partial void Ien_Injskip_Read(bool a, bool b);
        partial void Ien_Injskip_ValueProvider(bool a);
        protected IFlagRegisterField ien_bufoutfrozen_bit;
        partial void Ien_Bufoutfrozen_Write(bool a, bool b);
        partial void Ien_Bufoutfrozen_Read(bool a, bool b);
        partial void Ien_Bufoutfrozen_ValueProvider(bool a);
        protected IFlagRegisterField ien_prserr_bit;
        partial void Ien_Prserr_Write(bool a, bool b);
        partial void Ien_Prserr_Read(bool a, bool b);
        partial void Ien_Prserr_ValueProvider(bool a);
        protected IFlagRegisterField ien_bufouterr_bit;
        partial void Ien_Bufouterr_Write(bool a, bool b);
        partial void Ien_Bufouterr_Read(bool a, bool b);
        partial void Ien_Bufouterr_ValueProvider(bool a);
        protected IFlagRegisterField ien_sysrtcerr_bit;
        partial void Ien_Sysrtcerr_Write(bool a, bool b);
        partial void Ien_Sysrtcerr_Read(bool a, bool b);
        partial void Ien_Sysrtcerr_ValueProvider(bool a);
        protected IFlagRegisterField ien_injerr_bit;
        partial void Ien_Injerr_Write(bool a, bool b);
        partial void Ien_Injerr_Read(bool a, bool b);
        partial void Ien_Injerr_ValueProvider(bool a);
        protected IFlagRegisterField ien_dnssleepyxtalerr_bit;
        partial void Ien_Dnssleepyxtalerr_Write(bool a, bool b);
        partial void Ien_Dnssleepyxtalerr_Read(bool a, bool b);
        partial void Ien_Dnssleepyxtalerr_ValueProvider(bool a);
        protected IFlagRegisterField ien_bufoutfreezeerr_bit;
        partial void Ien_Bufoutfreezeerr_Write(bool a, bool b);
        partial void Ien_Bufoutfreezeerr_Read(bool a, bool b);
        partial void Ien_Bufoutfreezeerr_ValueProvider(bool a);
        protected IFlagRegisterField ien_bufoutdnserr_bit;
        partial void Ien_Bufoutdnserr_Write(bool a, bool b);
        partial void Ien_Bufoutdnserr_Read(bool a, bool b);
        partial void Ien_Bufoutdnserr_ValueProvider(bool a);
        protected IFlagRegisterField ien_dnserr_bit;
        partial void Ien_Dnserr_Write(bool a, bool b);
        partial void Ien_Dnserr_Read(bool a, bool b);
        partial void Ien_Dnserr_ValueProvider(bool a);
        protected IFlagRegisterField ien_lftimeouterr_bit;
        partial void Ien_Lftimeouterr_Write(bool a, bool b);
        partial void Ien_Lftimeouterr_Read(bool a, bool b);
        partial void Ien_Lftimeouterr_ValueProvider(bool a);
        protected IFlagRegisterField ien_corebiasopterr_bit;
        partial void Ien_Corebiasopterr_Write(bool a, bool b);
        partial void Ien_Corebiasopterr_Read(bool a, bool b);
        partial void Ien_Corebiasopterr_ValueProvider(bool a);

        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Lock - Offset : 0x80
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // Rfcfg_Xoinjhwseq - Offset : 0xA0
        protected IValueRegisterField rfcfg_xoinjhwseq_syvcocap_field;
        partial void Rfcfg_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Rfcfg_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Rfcfg_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);
        protected IValueRegisterField rfcfg_xoinjhwseq_vtrtrimvreg_field;
        partial void Rfcfg_Xoinjhwseq_Vtrtrimvreg_Write(ulong a, ulong b);
        partial void Rfcfg_Xoinjhwseq_Vtrtrimvreg_Read(ulong a, ulong b);
        partial void Rfcfg_Xoinjhwseq_Vtrtrimvreg_ValueProvider(ulong a);
        protected IFlagRegisterField rfcfg_xoinjhwseq_syvcocapdcap_bit;
        partial void Rfcfg_Xoinjhwseq_Syvcocapdcap_Write(bool a, bool b);
        partial void Rfcfg_Xoinjhwseq_Syvcocapdcap_Read(bool a, bool b);
        partial void Rfcfg_Xoinjhwseq_Syvcocapdcap_ValueProvider(bool a);
        protected IFlagRegisterField rfcfg_xoinjhwseq_syvcocaphcap_bit;
        partial void Rfcfg_Xoinjhwseq_Syvcocaphcap_Write(bool a, bool b);
        partial void Rfcfg_Xoinjhwseq_Syvcocaphcap_Read(bool a, bool b);
        partial void Rfcfg_Xoinjhwseq_Syvcocaphcap_ValueProvider(bool a);

        partial void Rfcfg_Xoinjhwseq_Write(uint a, uint b);
        partial void Rfcfg_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp0_Xoinjhwseq - Offset : 0xA4
        protected IValueRegisterField syvcocaptemp0_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp0_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp0_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp0_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp0_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp0_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp1_Xoinjhwseq - Offset : 0xA8
        protected IValueRegisterField syvcocaptemp1_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp1_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp1_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp1_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp1_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp1_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp2_Xoinjhwseq - Offset : 0xAC
        protected IValueRegisterField syvcocaptemp2_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp2_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp2_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp2_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp2_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp2_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp3_Xoinjhwseq - Offset : 0xB0
        protected IValueRegisterField syvcocaptemp3_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp3_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp3_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp3_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp3_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp3_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp4_Xoinjhwseq - Offset : 0xB4
        protected IValueRegisterField syvcocaptemp4_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp4_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp4_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp4_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp4_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp4_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp5_Xoinjhwseq - Offset : 0xB8
        protected IValueRegisterField syvcocaptemp5_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp5_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp5_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp5_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp5_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp5_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp6_Xoinjhwseq - Offset : 0xBC
        protected IValueRegisterField syvcocaptemp6_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp6_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp6_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp6_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp6_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp6_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp7_Xoinjhwseq - Offset : 0xC0
        protected IValueRegisterField syvcocaptemp7_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp7_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp7_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp7_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp7_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp7_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp8_Xoinjhwseq - Offset : 0xC4
        protected IValueRegisterField syvcocaptemp8_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp8_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp8_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp8_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp8_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp8_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp9_Xoinjhwseq - Offset : 0xC8
        protected IValueRegisterField syvcocaptemp9_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp9_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp9_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp9_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp9_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp9_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp10_Xoinjhwseq - Offset : 0xCC
        protected IValueRegisterField syvcocaptemp10_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp10_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp10_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp10_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp10_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp10_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp11_Xoinjhwseq - Offset : 0xD0
        protected IValueRegisterField syvcocaptemp11_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp11_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp11_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp11_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp11_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp11_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp12_Xoinjhwseq - Offset : 0xD4
        protected IValueRegisterField syvcocaptemp12_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp12_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp12_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp12_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp12_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp12_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp13_Xoinjhwseq - Offset : 0xD8
        protected IValueRegisterField syvcocaptemp13_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp13_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp13_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp13_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp13_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp13_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp14_Xoinjhwseq - Offset : 0xDC
        protected IValueRegisterField syvcocaptemp14_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp14_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp14_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp14_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp14_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp14_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp15_Xoinjhwseq - Offset : 0xE0
        protected IValueRegisterField syvcocaptemp15_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp15_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp15_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp15_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp15_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp15_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp16_Xoinjhwseq - Offset : 0xE4
        protected IValueRegisterField syvcocaptemp16_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp16_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp16_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp16_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp16_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp16_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp17_Xoinjhwseq - Offset : 0xE8
        protected IValueRegisterField syvcocaptemp17_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp17_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp17_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp17_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp17_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp17_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp18_Xoinjhwseq - Offset : 0xEC
        protected IValueRegisterField syvcocaptemp18_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp18_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp18_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp18_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp18_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp18_Xoinjhwseq_Read(uint a, uint b);
        
        // Syvcocaptemp19_Xoinjhwseq - Offset : 0xF0
        protected IValueRegisterField syvcocaptemp19_xoinjhwseq_syvcocap_field;
        partial void Syvcocaptemp19_Xoinjhwseq_Syvcocap_Write(ulong a, ulong b);
        partial void Syvcocaptemp19_Xoinjhwseq_Syvcocap_Read(ulong a, ulong b);
        partial void Syvcocaptemp19_Xoinjhwseq_Syvcocap_ValueProvider(ulong a);

        partial void Syvcocaptemp19_Xoinjhwseq_Write(uint a, uint b);
        partial void Syvcocaptemp19_Xoinjhwseq_Read(uint a, uint b);
        
        partial void HFXO_Reset();

        partial void SiLabs_HFXO_5_Constructor();

        public bool Enabled
        {
            get 
            {
                // Your boolean which you have to define in your partial class file
                return isEnabled;
            }
        }

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
            Trim = 0x4,
            Xouttrim = 0xC,
            Xtalcfg = 0x10,
            Xtalctrl = 0x18,
            Xtalctrl1 = 0x1C,
            Cfg = 0x20,
            Sleepyxtalcfg0 = 0x28,
            Sleepyxtalcfg1 = 0x2C,
            Ctrl = 0x30,
            Pkdetctrl1 = 0x34,
            Lowpwrctrl = 0x38,
            Pkdetctrl = 0x3C,
            Extclkpkdetctrl = 0x40,
            Internalctrl = 0x44,
            Internalxoutctrl = 0x48,
            Bufouttrim = 0x4C,
            Bufoutctrl = 0x50,
            Cmd = 0x54,
            Status = 0x58,
            Avgstartuptime = 0x5C,
            Dbgctrl = 0x60,
            Dbgstatus = 0x64,
            If = 0x70,
            Ien = 0x74,
            Lock = 0x80,
            Rfcfg_Xoinjhwseq = 0xA0,
            Syvcocaptemp0_Xoinjhwseq = 0xA4,
            Syvcocaptemp1_Xoinjhwseq = 0xA8,
            Syvcocaptemp2_Xoinjhwseq = 0xAC,
            Syvcocaptemp3_Xoinjhwseq = 0xB0,
            Syvcocaptemp4_Xoinjhwseq = 0xB4,
            Syvcocaptemp5_Xoinjhwseq = 0xB8,
            Syvcocaptemp6_Xoinjhwseq = 0xBC,
            Syvcocaptemp7_Xoinjhwseq = 0xC0,
            Syvcocaptemp8_Xoinjhwseq = 0xC4,
            Syvcocaptemp9_Xoinjhwseq = 0xC8,
            Syvcocaptemp10_Xoinjhwseq = 0xCC,
            Syvcocaptemp11_Xoinjhwseq = 0xD0,
            Syvcocaptemp12_Xoinjhwseq = 0xD4,
            Syvcocaptemp13_Xoinjhwseq = 0xD8,
            Syvcocaptemp14_Xoinjhwseq = 0xDC,
            Syvcocaptemp15_Xoinjhwseq = 0xE0,
            Syvcocaptemp16_Xoinjhwseq = 0xE4,
            Syvcocaptemp17_Xoinjhwseq = 0xE8,
            Syvcocaptemp18_Xoinjhwseq = 0xEC,
            Syvcocaptemp19_Xoinjhwseq = 0xF0,
            
            Ipversion_SET = 0x1000,
            Trim_SET = 0x1004,
            Xouttrim_SET = 0x100C,
            Xtalcfg_SET = 0x1010,
            Xtalctrl_SET = 0x1018,
            Xtalctrl1_SET = 0x101C,
            Cfg_SET = 0x1020,
            Sleepyxtalcfg0_SET = 0x1028,
            Sleepyxtalcfg1_SET = 0x102C,
            Ctrl_SET = 0x1030,
            Pkdetctrl1_SET = 0x1034,
            Lowpwrctrl_SET = 0x1038,
            Pkdetctrl_SET = 0x103C,
            Extclkpkdetctrl_SET = 0x1040,
            Internalctrl_SET = 0x1044,
            Internalxoutctrl_SET = 0x1048,
            Bufouttrim_SET = 0x104C,
            Bufoutctrl_SET = 0x1050,
            Cmd_SET = 0x1054,
            Status_SET = 0x1058,
            Avgstartuptime_SET = 0x105C,
            Dbgctrl_SET = 0x1060,
            Dbgstatus_SET = 0x1064,
            If_SET = 0x1070,
            Ien_SET = 0x1074,
            Lock_SET = 0x1080,
            Rfcfg_Xoinjhwseq_SET = 0x10A0,
            Syvcocaptemp0_Xoinjhwseq_SET = 0x10A4,
            Syvcocaptemp1_Xoinjhwseq_SET = 0x10A8,
            Syvcocaptemp2_Xoinjhwseq_SET = 0x10AC,
            Syvcocaptemp3_Xoinjhwseq_SET = 0x10B0,
            Syvcocaptemp4_Xoinjhwseq_SET = 0x10B4,
            Syvcocaptemp5_Xoinjhwseq_SET = 0x10B8,
            Syvcocaptemp6_Xoinjhwseq_SET = 0x10BC,
            Syvcocaptemp7_Xoinjhwseq_SET = 0x10C0,
            Syvcocaptemp8_Xoinjhwseq_SET = 0x10C4,
            Syvcocaptemp9_Xoinjhwseq_SET = 0x10C8,
            Syvcocaptemp10_Xoinjhwseq_SET = 0x10CC,
            Syvcocaptemp11_Xoinjhwseq_SET = 0x10D0,
            Syvcocaptemp12_Xoinjhwseq_SET = 0x10D4,
            Syvcocaptemp13_Xoinjhwseq_SET = 0x10D8,
            Syvcocaptemp14_Xoinjhwseq_SET = 0x10DC,
            Syvcocaptemp15_Xoinjhwseq_SET = 0x10E0,
            Syvcocaptemp16_Xoinjhwseq_SET = 0x10E4,
            Syvcocaptemp17_Xoinjhwseq_SET = 0x10E8,
            Syvcocaptemp18_Xoinjhwseq_SET = 0x10EC,
            Syvcocaptemp19_Xoinjhwseq_SET = 0x10F0,
            
            Ipversion_CLR = 0x2000,
            Trim_CLR = 0x2004,
            Xouttrim_CLR = 0x200C,
            Xtalcfg_CLR = 0x2010,
            Xtalctrl_CLR = 0x2018,
            Xtalctrl1_CLR = 0x201C,
            Cfg_CLR = 0x2020,
            Sleepyxtalcfg0_CLR = 0x2028,
            Sleepyxtalcfg1_CLR = 0x202C,
            Ctrl_CLR = 0x2030,
            Pkdetctrl1_CLR = 0x2034,
            Lowpwrctrl_CLR = 0x2038,
            Pkdetctrl_CLR = 0x203C,
            Extclkpkdetctrl_CLR = 0x2040,
            Internalctrl_CLR = 0x2044,
            Internalxoutctrl_CLR = 0x2048,
            Bufouttrim_CLR = 0x204C,
            Bufoutctrl_CLR = 0x2050,
            Cmd_CLR = 0x2054,
            Status_CLR = 0x2058,
            Avgstartuptime_CLR = 0x205C,
            Dbgctrl_CLR = 0x2060,
            Dbgstatus_CLR = 0x2064,
            If_CLR = 0x2070,
            Ien_CLR = 0x2074,
            Lock_CLR = 0x2080,
            Rfcfg_Xoinjhwseq_CLR = 0x20A0,
            Syvcocaptemp0_Xoinjhwseq_CLR = 0x20A4,
            Syvcocaptemp1_Xoinjhwseq_CLR = 0x20A8,
            Syvcocaptemp2_Xoinjhwseq_CLR = 0x20AC,
            Syvcocaptemp3_Xoinjhwseq_CLR = 0x20B0,
            Syvcocaptemp4_Xoinjhwseq_CLR = 0x20B4,
            Syvcocaptemp5_Xoinjhwseq_CLR = 0x20B8,
            Syvcocaptemp6_Xoinjhwseq_CLR = 0x20BC,
            Syvcocaptemp7_Xoinjhwseq_CLR = 0x20C0,
            Syvcocaptemp8_Xoinjhwseq_CLR = 0x20C4,
            Syvcocaptemp9_Xoinjhwseq_CLR = 0x20C8,
            Syvcocaptemp10_Xoinjhwseq_CLR = 0x20CC,
            Syvcocaptemp11_Xoinjhwseq_CLR = 0x20D0,
            Syvcocaptemp12_Xoinjhwseq_CLR = 0x20D4,
            Syvcocaptemp13_Xoinjhwseq_CLR = 0x20D8,
            Syvcocaptemp14_Xoinjhwseq_CLR = 0x20DC,
            Syvcocaptemp15_Xoinjhwseq_CLR = 0x20E0,
            Syvcocaptemp16_Xoinjhwseq_CLR = 0x20E4,
            Syvcocaptemp17_Xoinjhwseq_CLR = 0x20E8,
            Syvcocaptemp18_Xoinjhwseq_CLR = 0x20EC,
            Syvcocaptemp19_Xoinjhwseq_CLR = 0x20F0,
            
            Ipversion_TGL = 0x3000,
            Trim_TGL = 0x3004,
            Xouttrim_TGL = 0x300C,
            Xtalcfg_TGL = 0x3010,
            Xtalctrl_TGL = 0x3018,
            Xtalctrl1_TGL = 0x301C,
            Cfg_TGL = 0x3020,
            Sleepyxtalcfg0_TGL = 0x3028,
            Sleepyxtalcfg1_TGL = 0x302C,
            Ctrl_TGL = 0x3030,
            Pkdetctrl1_TGL = 0x3034,
            Lowpwrctrl_TGL = 0x3038,
            Pkdetctrl_TGL = 0x303C,
            Extclkpkdetctrl_TGL = 0x3040,
            Internalctrl_TGL = 0x3044,
            Internalxoutctrl_TGL = 0x3048,
            Bufouttrim_TGL = 0x304C,
            Bufoutctrl_TGL = 0x3050,
            Cmd_TGL = 0x3054,
            Status_TGL = 0x3058,
            Avgstartuptime_TGL = 0x305C,
            Dbgctrl_TGL = 0x3060,
            Dbgstatus_TGL = 0x3064,
            If_TGL = 0x3070,
            Ien_TGL = 0x3074,
            Lock_TGL = 0x3080,
            Rfcfg_Xoinjhwseq_TGL = 0x30A0,
            Syvcocaptemp0_Xoinjhwseq_TGL = 0x30A4,
            Syvcocaptemp1_Xoinjhwseq_TGL = 0x30A8,
            Syvcocaptemp2_Xoinjhwseq_TGL = 0x30AC,
            Syvcocaptemp3_Xoinjhwseq_TGL = 0x30B0,
            Syvcocaptemp4_Xoinjhwseq_TGL = 0x30B4,
            Syvcocaptemp5_Xoinjhwseq_TGL = 0x30B8,
            Syvcocaptemp6_Xoinjhwseq_TGL = 0x30BC,
            Syvcocaptemp7_Xoinjhwseq_TGL = 0x30C0,
            Syvcocaptemp8_Xoinjhwseq_TGL = 0x30C4,
            Syvcocaptemp9_Xoinjhwseq_TGL = 0x30C8,
            Syvcocaptemp10_Xoinjhwseq_TGL = 0x30CC,
            Syvcocaptemp11_Xoinjhwseq_TGL = 0x30D0,
            Syvcocaptemp12_Xoinjhwseq_TGL = 0x30D4,
            Syvcocaptemp13_Xoinjhwseq_TGL = 0x30D8,
            Syvcocaptemp14_Xoinjhwseq_TGL = 0x30DC,
            Syvcocaptemp15_Xoinjhwseq_TGL = 0x30E0,
            Syvcocaptemp16_Xoinjhwseq_TGL = 0x30E4,
            Syvcocaptemp17_Xoinjhwseq_TGL = 0x30E8,
            Syvcocaptemp18_Xoinjhwseq_TGL = 0x30EC,
            Syvcocaptemp19_Xoinjhwseq_TGL = 0x30F0,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}