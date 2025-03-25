//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    HFXO, Generated on : 2023-07-20 14:23:56.647256
    HFXO, ID Version : dc00b03cfe6f4853848d5d69c970d617.3 */

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
    public partial class EFR32xG2_HFXO_3
    {
        public EFR32xG2_HFXO_3(Machine machine) : base(machine)
        {
            EFR32xG2_HFXO_3_constructor();
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
    public partial class EFR32xG2_HFXO_3 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_HFXO_3(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_HFXO_3_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Trim, GenerateTrimRegister()},
                {(long)Registers.Swrst, GenerateSwrstRegister()},
                {(long)Registers.Xouttrim, GenerateXouttrimRegister()},
                {(long)Registers.Xtalcfg, GenerateXtalcfgRegister()},
                {(long)Registers.Xtalctrl, GenerateXtalctrlRegister()},
                {(long)Registers.Xtalctrl1, GenerateXtalctrl1Register()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Pkdetctrl1, GeneratePkdetctrl1Register()},
                {(long)Registers.Lowpwrctrl, GenerateLowpwrctrlRegister()},
                {(long)Registers.Pkdetctrl, GeneratePkdetctrlRegister()},
                {(long)Registers.Internalctrl, GenerateInternalctrlRegister()},
                {(long)Registers.Internalxoutctrl, GenerateInternalxoutctrlRegister()},
                {(long)Registers.Bufouttrim, GenerateBufouttrimRegister()},
                {(long)Registers.Bufoutctrl, GenerateBufoutctrlRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Dbgstatus, GenerateDbgstatusRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            HFXO_Reset();
        }
        
        protected enum TRIM_VTRREGTCANA
        {
            ADJ0 = 0, // 
            NONFLASH = 1, // 
            FLASH = 2, // 
            ADJ3 = 3, // 
        }
        
        protected enum TRIM_VTRCORETCANA
        {
            ADJ0 = 0, // 
            NONFLASH = 1, // 
            FLASH = 2, // 
            ADJ3 = 3, // 
        }
        
        protected enum XTALCFG_TIMEOUTSTEADY
        {
            T4US = 0, // The steady state timeout is set to 16 us minimum.  The maximum can be +40%.
            T16US = 1, // The steady state timeout is set to 41 us minimum.  The maximum can be +40%.
            T41US = 2, // The steady state timeout is set to 83 us minimum.  The maximum can be +40%.
            T83US = 3, // The steady state timeout is set to 125 us minimum.  The maximum can be +40%.
            T125US = 4, // The steady state timeout is set to 166 us minimum.  The maximum can be +40%.
            T166US = 5, // The steady state timeout is set to 208 us minimum.  The maximum can be +40%.
            T208US = 6, // The steady state timeout is set to 250 us minimum.  The maximum can be +40%.
            T250US = 7, // The steady state timeout is set to 333 us minimum.  The maximum can be +40%.
            T333US = 8, // The steady state timeout is set to 416 us minimum.  The maximum can be +40%.
            T416US = 9, // The steady state timeout is set to 500 us minimum.  The maximum can be +40%.
            T500US = 10, // The steady state timeout is set to 666 us minimum.  The maximum can be +40%.
            T666US = 11, // The steady state timeout is set to 833 us minimum.  The maximum can be +40%.
            T833US = 12, // The steady state timeout is set to 1666 us minimum.  The maximum can be +40%.
            T1666US = 13, // The steady state timeout is set to 2500 us minimum.  The maximum can be +40%.
            T2500US = 14, // The steady state timeout is set to 4166 us minimum.  The maximum can be +40%.
            T4166US = 15, // The steady state timeout is set to 7500 us minimum.  The maximum can be +40%.
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
        
        protected enum XTALCTRL_CTUNEFIXANA
        {
            NONE = 0, // Remove fixed capacitance on XI and XO nodes
            XI = 1, // Adds fixed capacitance on XI node
            XO = 2, // Adds fixed capacitance on XO node
            BOTH = 3, // Adds fixed capacitance on both XI and XO nodes
        }
        
        protected enum XTALCTRL_COREDGENANA
        {
            NONE = 0, // Do not apply core degeneration resistence
            DGEN33 = 1, // Apply 33 ohm core degeneration resistence
            DGEN50 = 2, // Apply 50 ohm core degeneration resistence
            DGEN100 = 3, // Apply 100 ohm core degeneration resistence
        }
        
        protected enum CFG_MODE
        {
            XTAL = 0, // crystal oscillator
            EXTCLK = 1, // external sinusoidal clock can be supplied on XI pin.
            EXTCLKPKDET = 2, // external sinusoidal clock can be supplied on XI pin (peak detector used).
        }
        
        protected enum CFG_SQBUFSCHTRGANA
        {
            DISABLE = 0, // Squaring buffer schmitt trigger is disabled
            ENABLE = 1, // Squaring buffer schmitt trigger is enabled
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
            BUFOUTRDY = 5, // PRS mux outputs BUFOUT ready status
            CLKINTRA0RDY = 6, // PRS mux outputs intra-clock 0 ready status
            CLKINTRA1RDY = 7, // PRS mux outputs intra-clock 1 ready status
            HWREQ = 8, // PRS mux outputs oscillator requested by digital clock status
            PRSHWREQ = 9, // PRS mux outputs oscillator requested by PRS request status
            BUFOUTHWREQ = 10, // PRS mux outputs oscillator requested by BUFOUT request status
            CLKINTRA0HWREQ = 11, // PRS mux outputs oscillator requested by intra-clock 0 request status
            CLKINTRA1HWREQ = 12, // PRS mux outputs oscillator requested by intra-clock 1 request status
        }
        
        protected enum CTRL_PRSSTATUSSEL1
        {
            DISABLED = 0, // PRS mux outputs 0
            ENS = 1, // PRS mux outputs enabled status
            COREBIASOPTRDY = 2, // PRS mux outputs core bias optimization ready status
            RDY = 3, // PRS mux outputs ready status
            PRSRDY = 4, // PRS mux outputs PRS ready status
            BUFOUTRDY = 5, // PRS mux outputs BUFOUT ready status
            CLKINTRA0RDY = 6, // PRS mux outputs intra-clock 0 ready status
            CLKINTRA1RDY = 7, // PRS mux outputs intra-clock 1 ready status
            HWREQ = 8, // PRS mux outputs oscillator requested by digital clock status
            PRSHWREQ = 9, // PRS mux outputs oscillator requested by PRS request status
            BUFOUTHWREQ = 10, // PRS mux outputs oscillator requested by BUFOUT request status
            CLKINTRA0HWREQ = 11, // PRS mux outputs oscillator requested by intra-clock 0 request status
            CLKINTRA1HWREQ = 12, // PRS mux outputs oscillator requested by intra-clock 1 request status
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
        
        protected enum LOWPWRCTRL_SHUNTBIASANA
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
            V105MV = 0, // 
            V132MV = 1, // 
            V157MV = 2, // 
            V184MV = 3, // 
            V210MV = 4, // 
            V236MV = 5, // 
            V262MV = 6, // 
            V289MV = 7, // 
            V315MV = 8, // 
            V341MV = 9, // 
            V367MV = 10, // 
            V394MV = 11, // 
            V420MV = 12, // 
            V446MV = 13, // 
            V472MV = 14, // 
            V499MV = 15, // 
        }
        
        protected enum PKDETCTRL_PKDETTHSTARTUPI
        {
            V105MV = 0, // 
            V132MV = 1, // 
            V157MV = 2, // 
            V184MV = 3, // 
            V210MV = 4, // 
            V236MV = 5, // 
            V262MV = 6, // 
            V289MV = 7, // 
            V315MV = 8, // 
            V341MV = 9, // 
            V367MV = 10, // 
            V394MV = 11, // 
            V420MV = 12, // 
            V446MV = 13, // 
            V472MV = 14, // 
            V499MV = 15, // 
        }
        
        protected enum PKDETCTRL_PKDETTHSTARTUP
        {
            V105MV = 0, // 
            V132MV = 1, // 
            V157MV = 2, // 
            V184MV = 3, // 
            V210MV = 4, // 
            V236MV = 5, // 
            V262MV = 6, // 
            V289MV = 7, // 
            V315MV = 8, // 
            V341MV = 9, // 
            V367MV = 10, // 
            V394MV = 11, // 
            V420MV = 12, // 
            V446MV = 13, // 
            V472MV = 14, // 
            V499MV = 15, // 
        }
        
        protected enum PKDETCTRL_PKDETTHHIGH
        {
            V105MV = 0, // 
            V132MV = 1, // 
            V157MV = 2, // 
            V184MV = 3, // 
            V210MV = 4, // 
            V236MV = 5, // 
            V262MV = 6, // 
            V289MV = 7, // 
            V315MV = 8, // 
            V341MV = 9, // 
            V367MV = 10, // 
            V394MV = 11, // 
            V420MV = 12, // 
            V446MV = 13, // 
            V472MV = 14, // 
            V499MV = 15, // 
        }
        
        protected enum PKDETCTRL_TIMEOUTPKDET
        {
            T4US = 0, // The peak detector timeout is set to 4 us minimum.  The maximum can be +40%.
            T8US = 1, // The peak detector timeout is set to 8 us minimum.  The maximum can be +40%.
            T16US = 2, // The peak detector timeout is set to 16 us minimum.  The maximum can be +40%.
            T33US = 3, // The peak detector timeout is set to 33 us minimum.  The maximum can be +40%.
        }
        
        protected enum PKDETCTRL_REGLVLANA
        {
            V1P39 = 0, // 
            V1P43 = 1, // 
            V1P47 = 2, // 
            V1P51 = 3, // 
            V1P54 = 4, // 
            V1P57 = 5, // 
            V1P61 = 6, // 
            V1P64 = 7, // 
        }
        
        protected enum PKDETCTRL_REGLVLSTARTUP
        {
            V1P39 = 0, // 
            V1P43 = 1, // 
            V1P47 = 2, // 
            V1P51 = 3, // 
            V1P54 = 4, // 
            V1P57 = 5, // 
            V1P61 = 6, // 
            V1P64 = 7, // 
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
        
        protected enum INTERNALCTRL_VTRCOREFORCESTARTUPANA
        {
            OFF = 0, // 
            FORCE = 1, // 
        }
        
        protected enum INTERNALCTRL_VTRREGDISSTARTUPANA
        {
            OFF = 0, // 
            DISABLE = 1, // 
        }
        
        protected enum INTERNALCTRL_VTRREGFORCESTARTUPANA
        {
            OFF = 0, // 
            FORCE = 1, // 
        }
        
        protected enum BUFOUTCTRL_PEAKDETTHRESANA
        {
            V105MV = 0, // 
            V132MV = 1, // 
            V157MV = 2, // 
            V184MV = 3, // 
            V210MV = 4, // 
            V236MV = 5, // 
            V262MV = 6, // 
            V289MV = 7, // 
            V315MV = 8, // 
            V341MV = 9, // 
            V367MV = 10, // 
            V394MV = 11, // 
            V420MV = 12, // 
            V446MV = 13, // 
            V472MV = 14, // 
            V499MV = 15, // 
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
        
        // Trim - Offset : 0x4
        protected DoubleWordRegister  GenerateTrimRegister() => new DoubleWordRegister(this, 0x8770200)
            .WithReservedBits(0, 8)
            .WithEnumField<DoubleWordRegister, TRIM_VTRREGTCANA>(8, 2, out trim_vtrregtcana_field, 
                    valueProviderCallback: (_) => {
                        Trim_Vtrregtcana_ValueProvider(_);
                        return trim_vtrregtcana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Vtrregtcana_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Vtrregtcana_Read(_, __),
                    name: "Vtrregtcana")
            .WithEnumField<DoubleWordRegister, TRIM_VTRCORETCANA>(10, 2, out trim_vtrcoretcana_field, 
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
            .WithReservedBits(12, 4)
            .WithValueField(16, 4, out trim_vtrcoretrimana_field, 
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
            .WithValueField(20, 4, out trim_vtrregtrimana_field, 
                    valueProviderCallback: (_) => {
                        Trim_Vtrregtrimana_ValueProvider(_);
                        return trim_vtrregtrimana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Trim_Vtrregtrimana_Write(_, __);
                    },
                    readCallback: (_, __) => Trim_Vtrregtrimana_Read(_, __),
                    name: "Vtrregtrimana")
            .WithValueField(24, 4, out trim_shuntlvlana_field, 
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
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Trim_Read(_, __))
            .WithWriteCallback((_, __) => Trim_Write(_, __));
        
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
        
        // Xouttrim - Offset : 0xC
        protected DoubleWordRegister  GenerateXouttrimRegister() => new DoubleWordRegister(this, 0x44534)
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
        protected DoubleWordRegister  GenerateXtalcfgRegister() => new DoubleWordRegister(this, 0xBB00820)
            .WithValueField(0, 6, out xtalcfg_corebiasstartupi_field, 
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
            .WithValueField(6, 6, out xtalcfg_corebiasstartup_field, 
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
            .WithValueField(12, 4, out xtalcfg_ctunexistartup_field, 
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
            .WithValueField(16, 4, out xtalcfg_ctunexostartup_field, 
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
            .WithReservedBits(28, 4)
            .WithReadCallback((_, __) => Xtalcfg_Read(_, __))
            .WithWriteCallback((_, __) => Xtalcfg_Write(_, __));
        
        // Xtalctrl - Offset : 0x18
        protected DoubleWordRegister  GenerateXtalctrlRegister() => new DoubleWordRegister(this, 0x33C3C3C)
            .WithValueField(0, 8, out xtalctrl_corebiasana_field, 
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
            .WithValueField(8, 8, out xtalctrl_ctunexiana_field, 
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
            .WithValueField(16, 8, out xtalctrl_ctunexoana_field, 
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
            .WithEnumField<DoubleWordRegister, XTALCTRL_CTUNEFIXANA>(24, 2, out xtalctrl_ctunefixana_field, 
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
            .WithEnumField<DoubleWordRegister, XTALCTRL_COREDGENANA>(26, 2, out xtalctrl_coredgenana_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl_Coredgenana_ValueProvider(_);
                        return xtalctrl_coredgenana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalctrl_Coredgenana_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl_Coredgenana_Read(_, __),
                    name: "Coredgenana")
            .WithReservedBits(28, 3)
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
        protected DoubleWordRegister  GenerateXtalctrl1Register() => new DoubleWordRegister(this, 0x3C)
            .WithValueField(0, 8, out xtalctrl1_ctunexibufoutana_field, 
                    valueProviderCallback: (_) => {
                        Xtalctrl1_Ctunexibufoutana_ValueProvider(_);
                        return xtalctrl1_ctunexibufoutana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Xtalctrl1_Ctunexibufoutana_Write(_, __);
                    },
                    readCallback: (_, __) => Xtalctrl1_Ctunexibufoutana_Read(_, __),
                    name: "Ctunexibufoutana")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Xtalctrl1_Read(_, __))
            .WithWriteCallback((_, __) => Xtalctrl1_Write(_, __));
        
        // Cfg - Offset : 0x20
        protected DoubleWordRegister  GenerateCfgRegister() => new DoubleWordRegister(this, 0x10000000)
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
            .WithEnumField<DoubleWordRegister, CFG_SQBUFSCHTRGANA>(3, 1, out cfg_sqbufschtrgana_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Sqbufschtrgana_ValueProvider(_);
                        return cfg_sqbufschtrgana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Sqbufschtrgana_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Sqbufschtrgana_Read(_, __),
                    name: "Sqbufschtrgana")
            .WithReservedBits(4, 24)
            .WithFlag(28, out cfg_forcelftimeout_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Forcelftimeout_ValueProvider(_);
                        return cfg_forcelftimeout_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cfg_Forcelftimeout_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Forcelftimeout_Read(_, __),
                    name: "Forcelftimeout")
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
            .WithReservedBits(30, 1)
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
        
        // Ctrl - Offset : 0x28
        protected DoubleWordRegister  GenerateCtrlRegister() => new DoubleWordRegister(this, 0x7000040)
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
            .WithReservedBits(19, 5)
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
            .WithReservedBits(27, 4)
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
        
        // Pkdetctrl1 - Offset : 0x2C
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
        
        // Lowpwrctrl - Offset : 0x30
        protected DoubleWordRegister  GenerateLowpwrctrlRegister() => new DoubleWordRegister(this, 0xC32F)
            .WithValueField(0, 4, out lowpwrctrl_lowpowermodeana_field, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Lowpowermodeana_ValueProvider(_);
                        return lowpwrctrl_lowpowermodeana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Lowpowermodeana_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Lowpowermodeana_Read(_, __),
                    name: "Lowpowermodeana")
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
            .WithReservedBits(11, 1)
            .WithEnumField<DoubleWordRegister, LOWPWRCTRL_SHUNTBIASANA>(12, 4, out lowpwrctrl_shuntbiasana_field, 
                    valueProviderCallback: (_) => {
                        Lowpwrctrl_Shuntbiasana_ValueProvider(_);
                        return lowpwrctrl_shuntbiasana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Lowpwrctrl_Shuntbiasana_Write(_, __);
                    },
                    readCallback: (_, __) => Lowpwrctrl_Shuntbiasana_Read(_, __),
                    name: "Shuntbiasana")
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
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Lowpwrctrl_Read(_, __))
            .WithWriteCallback((_, __) => Lowpwrctrl_Write(_, __));
        
        // Pkdetctrl - Offset : 0x34
        protected DoubleWordRegister  GeneratePkdetctrlRegister() => new DoubleWordRegister(this, 0x81B78558)
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
            .WithEnumField<DoubleWordRegister, PKDETCTRL_REGLVLANA>(26, 3, out pkdetctrl_reglvlana_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Reglvlana_ValueProvider(_);
                        return pkdetctrl_reglvlana_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Reglvlana_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Reglvlana_Read(_, __),
                    name: "Reglvlana")
            .WithEnumField<DoubleWordRegister, PKDETCTRL_REGLVLSTARTUP>(29, 3, out pkdetctrl_reglvlstartup_field, 
                    valueProviderCallback: (_) => {
                        Pkdetctrl_Reglvlstartup_ValueProvider(_);
                        return pkdetctrl_reglvlstartup_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Pkdetctrl_Reglvlstartup_Write(_, __);
                    },
                    readCallback: (_, __) => Pkdetctrl_Reglvlstartup_Read(_, __),
                    name: "Reglvlstartup")
            .WithReadCallback((_, __) => Pkdetctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pkdetctrl_Write(_, __));
        
        // Internalctrl - Offset : 0x38
        protected DoubleWordRegister  GenerateInternalctrlRegister() => new DoubleWordRegister(this, 0x9029F)
            .WithFlag(0, out internalctrl_enregvtrana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Enregvtrana_ValueProvider(_);
                        return internalctrl_enregvtrana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Enregvtrana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Enregvtrana_Read(_, __),
                    name: "Enregvtrana")
            .WithFlag(1, out internalctrl_enregana_bit, 
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
            .WithFlag(2, out internalctrl_encorevtrana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Encorevtrana_ValueProvider(_);
                        return internalctrl_encorevtrana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Encorevtrana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Encorevtrana_Read(_, __),
                    name: "Encorevtrana")
            .WithFlag(3, out internalctrl_ensqbufana_bit, 
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
            .WithFlag(4, out internalctrl_encoreana_bit, 
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
            .WithReservedBits(5, 1)
            .WithFlag(6, out internalctrl_shortxi2xoana_bit, 
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
            .WithFlag(7, out internalctrl_shortxi2xofsm_bit, 
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
            .WithFlag(8, out internalctrl_enhighgmmodeana_bit, 
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
            .WithFlag(9, out internalctrl_enhighgmmodefsm_bit, 
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
            .WithEnumField<DoubleWordRegister, INTERNALCTRL_SQBUFFILTANA>(10, 2, out internalctrl_sqbuffiltana_field, 
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
            .WithFlag(12, out internalctrl_enclkdifana_bit, 
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
            .WithReservedBits(13, 3)
            .WithFlag(16, out internalctrl_enclkdigana_bit, 
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
            .WithFlag(17, out internalctrl_enclkauxadcana_bit, 
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
            .WithFlag(18, out internalctrl_enclkclkmultana_bit, 
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
            .WithFlag(19, out internalctrl_enclksyana_bit, 
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
            .WithFlag(20, out internalctrl_enclktxana_bit, 
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
            .WithReservedBits(21, 1)
            .WithFlag(22, out internalctrl_invclkdigana_bit, 
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
            .WithFlag(23, out internalctrl_invclkauxadcana_bit, 
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
            .WithFlag(24, out internalctrl_invclkclkmultana_bit, 
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
            .WithFlag(25, out internalctrl_invclksyana_bit, 
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
            .WithFlag(26, out internalctrl_invclktxana_bit, 
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
            .WithReservedBits(27, 1)
            .WithEnumField<DoubleWordRegister, INTERNALCTRL_VTRCOREDISSTARTUPANA>(28, 1, out internalctrl_vtrcoredisstartupana_bit, 
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
            .WithEnumField<DoubleWordRegister, INTERNALCTRL_VTRCOREFORCESTARTUPANA>(29, 1, out internalctrl_vtrcoreforcestartupana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Vtrcoreforcestartupana_ValueProvider(_);
                        return internalctrl_vtrcoreforcestartupana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Vtrcoreforcestartupana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Vtrcoreforcestartupana_Read(_, __),
                    name: "Vtrcoreforcestartupana")
            .WithEnumField<DoubleWordRegister, INTERNALCTRL_VTRREGDISSTARTUPANA>(30, 1, out internalctrl_vtrregdisstartupana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Vtrregdisstartupana_ValueProvider(_);
                        return internalctrl_vtrregdisstartupana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Vtrregdisstartupana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Vtrregdisstartupana_Read(_, __),
                    name: "Vtrregdisstartupana")
            .WithEnumField<DoubleWordRegister, INTERNALCTRL_VTRREGFORCESTARTUPANA>(31, 1, out internalctrl_vtrregforcestartupana_bit, 
                    valueProviderCallback: (_) => {
                        Internalctrl_Vtrregforcestartupana_ValueProvider(_);
                        return internalctrl_vtrregforcestartupana_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Internalctrl_Vtrregforcestartupana_Write(_, __);
                    },
                    readCallback: (_, __) => Internalctrl_Vtrregforcestartupana_Read(_, __),
                    name: "Vtrregforcestartupana")
            .WithReadCallback((_, __) => Internalctrl_Read(_, __))
            .WithWriteCallback((_, __) => Internalctrl_Write(_, __));
        
        // Internalxoutctrl - Offset : 0x3C
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
        
        // Bufouttrim - Offset : 0x40
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
        
        // Bufoutctrl - Offset : 0x44
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
            .WithEnumField<DoubleWordRegister, BUFOUTCTRL_PEAKDETTHRESANA>(12, 4, out bufoutctrl_peakdetthresana_field, 
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
        
        // Cmd - Offset : 0x50
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cmd_corebiasopt_bit, FieldMode.Write,
                    writeCallback: (_, __) => {
                        WriteWSYNC();
                        Cmd_Corebiasopt_Write(_, __);
                    },
                    name: "Corebiasopt")
            .WithReservedBits(1, 31)
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
            .WithReservedBits(4, 11)
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
            .WithReservedBits(22, 8)
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
        
        // Dbgstatus - Offset : 0x5C
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
            .WithReservedBits(3, 29)
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
            .WithReservedBits(4, 11)
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
            .WithReservedBits(22, 5)
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
            .WithReservedBits(4, 11)
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
            .WithReservedBits(22, 5)
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
        
        // Trim - Offset : 0x4
        protected IEnumRegisterField<TRIM_VTRREGTCANA> trim_vtrregtcana_field;
        partial void Trim_Vtrregtcana_Write(TRIM_VTRREGTCANA a, TRIM_VTRREGTCANA b);
        partial void Trim_Vtrregtcana_Read(TRIM_VTRREGTCANA a, TRIM_VTRREGTCANA b);
        partial void Trim_Vtrregtcana_ValueProvider(TRIM_VTRREGTCANA a);
        protected IEnumRegisterField<TRIM_VTRCORETCANA> trim_vtrcoretcana_field;
        partial void Trim_Vtrcoretcana_Write(TRIM_VTRCORETCANA a, TRIM_VTRCORETCANA b);
        partial void Trim_Vtrcoretcana_Read(TRIM_VTRCORETCANA a, TRIM_VTRCORETCANA b);
        partial void Trim_Vtrcoretcana_ValueProvider(TRIM_VTRCORETCANA a);
        protected IValueRegisterField trim_vtrcoretrimana_field;
        partial void Trim_Vtrcoretrimana_Write(ulong a, ulong b);
        partial void Trim_Vtrcoretrimana_Read(ulong a, ulong b);
        partial void Trim_Vtrcoretrimana_ValueProvider(ulong a);
        protected IValueRegisterField trim_vtrregtrimana_field;
        partial void Trim_Vtrregtrimana_Write(ulong a, ulong b);
        partial void Trim_Vtrregtrimana_Read(ulong a, ulong b);
        partial void Trim_Vtrregtrimana_ValueProvider(ulong a);
        protected IValueRegisterField trim_shuntlvlana_field;
        partial void Trim_Shuntlvlana_Write(ulong a, ulong b);
        partial void Trim_Shuntlvlana_Read(ulong a, ulong b);
        partial void Trim_Shuntlvlana_ValueProvider(ulong a);

        partial void Trim_Write(uint a, uint b);
        partial void Trim_Read(uint a, uint b);
        
        // Swrst - Offset : 0x8
        protected IFlagRegisterField swrst_swrst_bit;
        partial void Swrst_Swrst_Write(bool a, bool b);
        partial void Swrst_Swrst_ValueProvider(bool a);
        protected IFlagRegisterField swrst_resetting_bit;
        partial void Swrst_Resetting_Read(bool a, bool b);
        partial void Swrst_Resetting_ValueProvider(bool a);

        partial void Swrst_Write(uint a, uint b);
        partial void Swrst_Read(uint a, uint b);
        
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
        protected IEnumRegisterField<XTALCTRL_COREDGENANA> xtalctrl_coredgenana_field;
        partial void Xtalctrl_Coredgenana_Write(XTALCTRL_COREDGENANA a, XTALCTRL_COREDGENANA b);
        partial void Xtalctrl_Coredgenana_Read(XTALCTRL_COREDGENANA a, XTALCTRL_COREDGENANA b);
        partial void Xtalctrl_Coredgenana_ValueProvider(XTALCTRL_COREDGENANA a);
        protected IFlagRegisterField xtalctrl_skipcorebiasopt_bit;
        partial void Xtalctrl_Skipcorebiasopt_Write(bool a, bool b);
        partial void Xtalctrl_Skipcorebiasopt_Read(bool a, bool b);
        partial void Xtalctrl_Skipcorebiasopt_ValueProvider(bool a);

        partial void Xtalctrl_Write(uint a, uint b);
        partial void Xtalctrl_Read(uint a, uint b);
        
        // Xtalctrl1 - Offset : 0x1C
        protected IValueRegisterField xtalctrl1_ctunexibufoutana_field;
        partial void Xtalctrl1_Ctunexibufoutana_Write(ulong a, ulong b);
        partial void Xtalctrl1_Ctunexibufoutana_Read(ulong a, ulong b);
        partial void Xtalctrl1_Ctunexibufoutana_ValueProvider(ulong a);

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
        protected IEnumRegisterField<CFG_SQBUFSCHTRGANA> cfg_sqbufschtrgana_bit;
        partial void Cfg_Sqbufschtrgana_Write(CFG_SQBUFSCHTRGANA a, CFG_SQBUFSCHTRGANA b);
        partial void Cfg_Sqbufschtrgana_Read(CFG_SQBUFSCHTRGANA a, CFG_SQBUFSCHTRGANA b);
        partial void Cfg_Sqbufschtrgana_ValueProvider(CFG_SQBUFSCHTRGANA a);
        protected IFlagRegisterField cfg_forcelftimeout_bit;
        partial void Cfg_Forcelftimeout_Write(bool a, bool b);
        partial void Cfg_Forcelftimeout_Read(bool a, bool b);
        partial void Cfg_Forcelftimeout_ValueProvider(bool a);
        protected IFlagRegisterField cfg_forcehftimeout_bit;
        partial void Cfg_Forcehftimeout_Write(bool a, bool b);
        partial void Cfg_Forcehftimeout_Read(bool a, bool b);
        partial void Cfg_Forcehftimeout_ValueProvider(bool a);
        protected IFlagRegisterField cfg_disfsm_bit;
        partial void Cfg_Disfsm_Write(bool a, bool b);
        partial void Cfg_Disfsm_Read(bool a, bool b);
        partial void Cfg_Disfsm_ValueProvider(bool a);

        partial void Cfg_Write(uint a, uint b);
        partial void Cfg_Read(uint a, uint b);
        
        // Ctrl - Offset : 0x28
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
        protected IFlagRegisterField ctrl_forcerawclk_bit;
        partial void Ctrl_Forcerawclk_Write(bool a, bool b);
        partial void Ctrl_Forcerawclk_Read(bool a, bool b);
        partial void Ctrl_Forcerawclk_ValueProvider(bool a);

        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Pkdetctrl1 - Offset : 0x2C
        protected IFlagRegisterField pkdetctrl1_notimeouterr_bit;
        partial void Pkdetctrl1_Notimeouterr_Write(bool a, bool b);
        partial void Pkdetctrl1_Notimeouterr_Read(bool a, bool b);
        partial void Pkdetctrl1_Notimeouterr_ValueProvider(bool a);

        partial void Pkdetctrl1_Write(uint a, uint b);
        partial void Pkdetctrl1_Read(uint a, uint b);
        
        // Lowpwrctrl - Offset : 0x30
        protected IValueRegisterField lowpwrctrl_lowpowermodeana_field;
        partial void Lowpwrctrl_Lowpowermodeana_Write(ulong a, ulong b);
        partial void Lowpwrctrl_Lowpowermodeana_Read(ulong a, ulong b);
        partial void Lowpwrctrl_Lowpowermodeana_ValueProvider(ulong a);
        protected IEnumRegisterField<LOWPWRCTRL_SQBUFBIASRESANA> lowpwrctrl_sqbufbiasresana_field;
        partial void Lowpwrctrl_Sqbufbiasresana_Write(LOWPWRCTRL_SQBUFBIASRESANA a, LOWPWRCTRL_SQBUFBIASRESANA b);
        partial void Lowpwrctrl_Sqbufbiasresana_Read(LOWPWRCTRL_SQBUFBIASRESANA a, LOWPWRCTRL_SQBUFBIASRESANA b);
        partial void Lowpwrctrl_Sqbufbiasresana_ValueProvider(LOWPWRCTRL_SQBUFBIASRESANA a);
        protected IEnumRegisterField<LOWPWRCTRL_SQBUFBIASANA> lowpwrctrl_sqbufbiasana_field;
        partial void Lowpwrctrl_Sqbufbiasana_Write(LOWPWRCTRL_SQBUFBIASANA a, LOWPWRCTRL_SQBUFBIASANA b);
        partial void Lowpwrctrl_Sqbufbiasana_Read(LOWPWRCTRL_SQBUFBIASANA a, LOWPWRCTRL_SQBUFBIASANA b);
        partial void Lowpwrctrl_Sqbufbiasana_ValueProvider(LOWPWRCTRL_SQBUFBIASANA a);
        protected IEnumRegisterField<LOWPWRCTRL_SHUNTBIASANA> lowpwrctrl_shuntbiasana_field;
        partial void Lowpwrctrl_Shuntbiasana_Write(LOWPWRCTRL_SHUNTBIASANA a, LOWPWRCTRL_SHUNTBIASANA b);
        partial void Lowpwrctrl_Shuntbiasana_Read(LOWPWRCTRL_SHUNTBIASANA a, LOWPWRCTRL_SHUNTBIASANA b);
        partial void Lowpwrctrl_Shuntbiasana_ValueProvider(LOWPWRCTRL_SHUNTBIASANA a);
        protected IEnumRegisterField<LOWPWRCTRL_TIMEOUTWARM> lowpwrctrl_timeoutwarm_field;
        partial void Lowpwrctrl_Timeoutwarm_Write(LOWPWRCTRL_TIMEOUTWARM a, LOWPWRCTRL_TIMEOUTWARM b);
        partial void Lowpwrctrl_Timeoutwarm_Read(LOWPWRCTRL_TIMEOUTWARM a, LOWPWRCTRL_TIMEOUTWARM b);
        partial void Lowpwrctrl_Timeoutwarm_ValueProvider(LOWPWRCTRL_TIMEOUTWARM a);

        partial void Lowpwrctrl_Write(uint a, uint b);
        partial void Lowpwrctrl_Read(uint a, uint b);
        
        // Pkdetctrl - Offset : 0x34
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
        protected IEnumRegisterField<PKDETCTRL_REGLVLANA> pkdetctrl_reglvlana_field;
        partial void Pkdetctrl_Reglvlana_Write(PKDETCTRL_REGLVLANA a, PKDETCTRL_REGLVLANA b);
        partial void Pkdetctrl_Reglvlana_Read(PKDETCTRL_REGLVLANA a, PKDETCTRL_REGLVLANA b);
        partial void Pkdetctrl_Reglvlana_ValueProvider(PKDETCTRL_REGLVLANA a);
        protected IEnumRegisterField<PKDETCTRL_REGLVLSTARTUP> pkdetctrl_reglvlstartup_field;
        partial void Pkdetctrl_Reglvlstartup_Write(PKDETCTRL_REGLVLSTARTUP a, PKDETCTRL_REGLVLSTARTUP b);
        partial void Pkdetctrl_Reglvlstartup_Read(PKDETCTRL_REGLVLSTARTUP a, PKDETCTRL_REGLVLSTARTUP b);
        partial void Pkdetctrl_Reglvlstartup_ValueProvider(PKDETCTRL_REGLVLSTARTUP a);

        partial void Pkdetctrl_Write(uint a, uint b);
        partial void Pkdetctrl_Read(uint a, uint b);
        
        // Internalctrl - Offset : 0x38
        protected IFlagRegisterField internalctrl_enregvtrana_bit;
        partial void Internalctrl_Enregvtrana_Write(bool a, bool b);
        partial void Internalctrl_Enregvtrana_Read(bool a, bool b);
        partial void Internalctrl_Enregvtrana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_enregana_bit;
        partial void Internalctrl_Enregana_Write(bool a, bool b);
        partial void Internalctrl_Enregana_Read(bool a, bool b);
        partial void Internalctrl_Enregana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_encorevtrana_bit;
        partial void Internalctrl_Encorevtrana_Write(bool a, bool b);
        partial void Internalctrl_Encorevtrana_Read(bool a, bool b);
        partial void Internalctrl_Encorevtrana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_ensqbufana_bit;
        partial void Internalctrl_Ensqbufana_Write(bool a, bool b);
        partial void Internalctrl_Ensqbufana_Read(bool a, bool b);
        partial void Internalctrl_Ensqbufana_ValueProvider(bool a);
        protected IFlagRegisterField internalctrl_encoreana_bit;
        partial void Internalctrl_Encoreana_Write(bool a, bool b);
        partial void Internalctrl_Encoreana_Read(bool a, bool b);
        partial void Internalctrl_Encoreana_ValueProvider(bool a);
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
        protected IEnumRegisterField<INTERNALCTRL_VTRCOREDISSTARTUPANA> internalctrl_vtrcoredisstartupana_bit;
        partial void Internalctrl_Vtrcoredisstartupana_Write(INTERNALCTRL_VTRCOREDISSTARTUPANA a, INTERNALCTRL_VTRCOREDISSTARTUPANA b);
        partial void Internalctrl_Vtrcoredisstartupana_Read(INTERNALCTRL_VTRCOREDISSTARTUPANA a, INTERNALCTRL_VTRCOREDISSTARTUPANA b);
        partial void Internalctrl_Vtrcoredisstartupana_ValueProvider(INTERNALCTRL_VTRCOREDISSTARTUPANA a);
        protected IEnumRegisterField<INTERNALCTRL_VTRCOREFORCESTARTUPANA> internalctrl_vtrcoreforcestartupana_bit;
        partial void Internalctrl_Vtrcoreforcestartupana_Write(INTERNALCTRL_VTRCOREFORCESTARTUPANA a, INTERNALCTRL_VTRCOREFORCESTARTUPANA b);
        partial void Internalctrl_Vtrcoreforcestartupana_Read(INTERNALCTRL_VTRCOREFORCESTARTUPANA a, INTERNALCTRL_VTRCOREFORCESTARTUPANA b);
        partial void Internalctrl_Vtrcoreforcestartupana_ValueProvider(INTERNALCTRL_VTRCOREFORCESTARTUPANA a);
        protected IEnumRegisterField<INTERNALCTRL_VTRREGDISSTARTUPANA> internalctrl_vtrregdisstartupana_bit;
        partial void Internalctrl_Vtrregdisstartupana_Write(INTERNALCTRL_VTRREGDISSTARTUPANA a, INTERNALCTRL_VTRREGDISSTARTUPANA b);
        partial void Internalctrl_Vtrregdisstartupana_Read(INTERNALCTRL_VTRREGDISSTARTUPANA a, INTERNALCTRL_VTRREGDISSTARTUPANA b);
        partial void Internalctrl_Vtrregdisstartupana_ValueProvider(INTERNALCTRL_VTRREGDISSTARTUPANA a);
        protected IEnumRegisterField<INTERNALCTRL_VTRREGFORCESTARTUPANA> internalctrl_vtrregforcestartupana_bit;
        partial void Internalctrl_Vtrregforcestartupana_Write(INTERNALCTRL_VTRREGFORCESTARTUPANA a, INTERNALCTRL_VTRREGFORCESTARTUPANA b);
        partial void Internalctrl_Vtrregforcestartupana_Read(INTERNALCTRL_VTRREGFORCESTARTUPANA a, INTERNALCTRL_VTRREGFORCESTARTUPANA b);
        partial void Internalctrl_Vtrregforcestartupana_ValueProvider(INTERNALCTRL_VTRREGFORCESTARTUPANA a);

        partial void Internalctrl_Write(uint a, uint b);
        partial void Internalctrl_Read(uint a, uint b);
        
        // Internalxoutctrl - Offset : 0x3C
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
        
        // Bufouttrim - Offset : 0x40
        protected IValueRegisterField bufouttrim_vtrtrimana_field;
        partial void Bufouttrim_Vtrtrimana_Write(ulong a, ulong b);
        partial void Bufouttrim_Vtrtrimana_Read(ulong a, ulong b);
        partial void Bufouttrim_Vtrtrimana_ValueProvider(ulong a);

        partial void Bufouttrim_Write(uint a, uint b);
        partial void Bufouttrim_Read(uint a, uint b);
        
        // Bufoutctrl - Offset : 0x44
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
        protected IEnumRegisterField<BUFOUTCTRL_PEAKDETTHRESANA> bufoutctrl_peakdetthresana_field;
        partial void Bufoutctrl_Peakdetthresana_Write(BUFOUTCTRL_PEAKDETTHRESANA a, BUFOUTCTRL_PEAKDETTHRESANA b);
        partial void Bufoutctrl_Peakdetthresana_Read(BUFOUTCTRL_PEAKDETTHRESANA a, BUFOUTCTRL_PEAKDETTHRESANA b);
        partial void Bufoutctrl_Peakdetthresana_ValueProvider(BUFOUTCTRL_PEAKDETTHRESANA a);
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
        
        // Cmd - Offset : 0x50
        protected IFlagRegisterField cmd_corebiasopt_bit;
        partial void Cmd_Corebiasopt_Write(bool a, bool b);
        partial void Cmd_Corebiasopt_ValueProvider(bool a);

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
        protected IFlagRegisterField status_syncbusy_bit;
        partial void Status_Syncbusy_Read(bool a, bool b);
        partial void Status_Syncbusy_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // Dbgstatus - Offset : 0x5C
        protected IEnumRegisterField<DBGSTATUS_PKDETSTATUS> dbgstatus_pkdetstatus_bit;
        partial void Dbgstatus_Pkdetstatus_Read(DBGSTATUS_PKDETSTATUS a, DBGSTATUS_PKDETSTATUS b);
        partial void Dbgstatus_Pkdetstatus_ValueProvider(DBGSTATUS_PKDETSTATUS a);
        protected IFlagRegisterField dbgstatus_startupdone_bit;
        partial void Dbgstatus_Startupdone_Read(bool a, bool b);
        partial void Dbgstatus_Startupdone_ValueProvider(bool a);
        protected IEnumRegisterField<DBGSTATUS_XOUTPKDETSTATUS> dbgstatus_xoutpkdetstatus_bit;
        partial void Dbgstatus_Xoutpkdetstatus_Read(DBGSTATUS_XOUTPKDETSTATUS a, DBGSTATUS_XOUTPKDETSTATUS b);
        partial void Dbgstatus_Xoutpkdetstatus_ValueProvider(DBGSTATUS_XOUTPKDETSTATUS a);

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
        
        partial void HFXO_Reset();

        partial void EFR32xG2_HFXO_3_Constructor();

        public bool Enabled
        {
            get 
            {
                // Your boolean which you have to define in your partial class file
                return isEnabled;
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
            Trim = 0x4,
            Swrst = 0x8,
            Xouttrim = 0xC,
            Xtalcfg = 0x10,
            Xtalctrl = 0x18,
            Xtalctrl1 = 0x1C,
            Cfg = 0x20,
            Ctrl = 0x28,
            Pkdetctrl1 = 0x2C,
            Lowpwrctrl = 0x30,
            Pkdetctrl = 0x34,
            Internalctrl = 0x38,
            Internalxoutctrl = 0x3C,
            Bufouttrim = 0x40,
            Bufoutctrl = 0x44,
            Cmd = 0x50,
            Status = 0x58,
            Dbgstatus = 0x5C,
            If = 0x70,
            Ien = 0x74,
            Lock = 0x80,
            
            Ipversion_SET = 0x1000,
            Trim_SET = 0x1004,
            Swrst_SET = 0x1008,
            Xouttrim_SET = 0x100C,
            Xtalcfg_SET = 0x1010,
            Xtalctrl_SET = 0x1018,
            Xtalctrl1_SET = 0x101C,
            Cfg_SET = 0x1020,
            Ctrl_SET = 0x1028,
            Pkdetctrl1_SET = 0x102C,
            Lowpwrctrl_SET = 0x1030,
            Pkdetctrl_SET = 0x1034,
            Internalctrl_SET = 0x1038,
            Internalxoutctrl_SET = 0x103C,
            Bufouttrim_SET = 0x1040,
            Bufoutctrl_SET = 0x1044,
            Cmd_SET = 0x1050,
            Status_SET = 0x1058,
            Dbgstatus_SET = 0x105C,
            If_SET = 0x1070,
            Ien_SET = 0x1074,
            Lock_SET = 0x1080,
            
            Ipversion_CLR = 0x2000,
            Trim_CLR = 0x2004,
            Swrst_CLR = 0x2008,
            Xouttrim_CLR = 0x200C,
            Xtalcfg_CLR = 0x2010,
            Xtalctrl_CLR = 0x2018,
            Xtalctrl1_CLR = 0x201C,
            Cfg_CLR = 0x2020,
            Ctrl_CLR = 0x2028,
            Pkdetctrl1_CLR = 0x202C,
            Lowpwrctrl_CLR = 0x2030,
            Pkdetctrl_CLR = 0x2034,
            Internalctrl_CLR = 0x2038,
            Internalxoutctrl_CLR = 0x203C,
            Bufouttrim_CLR = 0x2040,
            Bufoutctrl_CLR = 0x2044,
            Cmd_CLR = 0x2050,
            Status_CLR = 0x2058,
            Dbgstatus_CLR = 0x205C,
            If_CLR = 0x2070,
            Ien_CLR = 0x2074,
            Lock_CLR = 0x2080,
            
            Ipversion_TGL = 0x3000,
            Trim_TGL = 0x3004,
            Swrst_TGL = 0x3008,
            Xouttrim_TGL = 0x300C,
            Xtalcfg_TGL = 0x3010,
            Xtalctrl_TGL = 0x3018,
            Xtalctrl1_TGL = 0x301C,
            Cfg_TGL = 0x3020,
            Ctrl_TGL = 0x3028,
            Pkdetctrl1_TGL = 0x302C,
            Lowpwrctrl_TGL = 0x3030,
            Pkdetctrl_TGL = 0x3034,
            Internalctrl_TGL = 0x3038,
            Internalxoutctrl_TGL = 0x303C,
            Bufouttrim_TGL = 0x3040,
            Bufoutctrl_TGL = 0x3044,
            Cmd_TGL = 0x3050,
            Status_TGL = 0x3058,
            Dbgstatus_TGL = 0x305C,
            If_TGL = 0x3070,
            Ien_TGL = 0x3074,
            Lock_TGL = 0x3080,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}