//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    EMU, Generated on : 2023-07-20 14:29:22.528585
    EMU, ID Version : ffc0fb7f840a4547a9111df0b917ddd3.3 */

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
    public partial class EFR32xG2_EMU_3
    {
        public EFR32xG2_EMU_3(Machine machine) : base(machine)
        {
            EFR32xG2_EMU_3_constructor();
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
    public partial class EFR32xG2_EMU_3 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_EMU_3(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_EMU_3_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ldreg, GenerateLdregRegister()},
                {(long)Registers.Dvddlebod, GenerateDvddlebodRegister()},
                {(long)Registers.Vlmthv, GenerateVlmthvRegister()},
                {(long)Registers.Dvddbod, GenerateDvddbodRegister()},
                {(long)Registers.Decbod, GenerateDecbodRegister()},
                {(long)Registers.Hdreg, GenerateHdregRegister()},
                {(long)Registers.Retreg, GenerateRetregRegister()},
                {(long)Registers.Bod3sensetrim, GenerateBod3sensetrimRegister()},
                {(long)Registers.Bod3sense, GenerateBod3senseRegister()},
                {(long)Registers.Isbias, GenerateIsbiasRegister()},
                {(long)Registers.Isbiastrim, GenerateIsbiastrimRegister()},
                {(long)Registers.Isbiasvrefregtrim, GenerateIsbiasvrefregtrimRegister()},
                {(long)Registers.Isbiasvreflvbodtrim, GenerateIsbiasvreflvbodtrimRegister()},
                {(long)Registers.Anastatus, GenerateAnastatusRegister()},
                {(long)Registers.Pfmbyp, GeneratePfmbypRegister()},
                {(long)Registers.Vregvddcmpctrl, GenerateVregvddcmpctrlRegister()},
                {(long)Registers.Pd1paretctrl, GeneratePd1paretctrlRegister()},
                {(long)Registers.Pd0pwrctrl, GeneratePd0pwrctrlRegister()},
                {(long)Registers.Pd0pwrem2ctrl, GeneratePd0pwrem2ctrlRegister()},
                {(long)Registers.Ipversion_Rev, GenerateIpversion_revRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Em4ctrl, GenerateEm4ctrlRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Templimits, GenerateTemplimitsRegister()},
                {(long)Registers.Templimitsdg, GenerateTemplimitsdgRegister()},
                {(long)Registers.Templimitsse, GenerateTemplimitsseRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Temp, GenerateTempRegister()},
                {(long)Registers.Testctrl, GenerateTestctrlRegister()},
                {(long)Registers.Rstctrl, GenerateRstctrlRegister()},
                {(long)Registers.Rstcause, GenerateRstcauseRegister()},
                {(long)Registers.Tamperrstcause, GenerateTamperrstcauseRegister()},
                {(long)Registers.Dgif, GenerateDgifRegister()},
                {(long)Registers.Dgien, GenerateDgienRegister()},
                {(long)Registers.Seqif, GenerateSeqifRegister()},
                {(long)Registers.Seqien, GenerateSeqienRegister()},
                {(long)Registers.Delaycfg, GenerateDelaycfgRegister()},
                {(long)Registers.Testlock, GenerateTestlockRegister()},
                {(long)Registers.Auxctrl, GenerateAuxctrlRegister()},
                {(long)Registers.Isbiasconf_Isbiasctrl, GenerateIsbiasconf_isbiasctrlRegister()},
                {(long)Registers.Isbiascalovr_Isbiasctrl, GenerateIsbiascalovr_isbiasctrlRegister()},
                {(long)Registers.Isbiasperiod_Isbiasctrl, GenerateIsbiasperiod_isbiasctrlRegister()},
                {(long)Registers.Isbiastempcomprate_Isbiasctrl, GenerateIsbiastempcomprate_isbiasctrlRegister()},
                {(long)Registers.Isbiastempcompthr_Isbiasctrl, GenerateIsbiastempcompthr_isbiasctrlRegister()},
                {(long)Registers.Isbiaspfmrefreshcfg_Isbiasctrl, GenerateIsbiaspfmrefreshcfg_isbiasctrlRegister()},
                {(long)Registers.Isbiasrefreshcfg_Isbiasctrl, GenerateIsbiasrefreshcfg_isbiasctrlRegister()},
                {(long)Registers.Isbiastempconst_Isbiasctrl, GenerateIsbiastempconst_isbiasctrlRegister()},
                {(long)Registers.Isbiasstatus_Isbiasctrl, GenerateIsbiasstatus_isbiasctrlRegister()},
                {(long)Registers.Vsbtempcomp_Isbiasctrl, GenerateVsbtempcomp_isbiasctrlRegister()},
                {(long)Registers.Vsbtempcompthr_Isbiasctrl, GenerateVsbtempcompthr_isbiasctrlRegister()},
                {(long)Registers.Retregtempcomp_Isbiasctrl, GenerateRetregtempcomp_isbiasctrlRegister()},
                {(long)Registers.Efpif, GenerateEfpifRegister()},
                {(long)Registers.Efpien, GenerateEfpienRegister()},
                {(long)Registers.Efpctrl, GenerateEfpctrlRegister()},
                {(long)Registers.Rootctrl, GenerateRootctrlRegister()},
                {(long)Registers.Rpuratd0_Drpu, GenerateRpuratd0_drpuRegister()},
                {(long)Registers.Rpuratd1_Drpu, GenerateRpuratd1_drpuRegister()},
                {(long)Registers.Rpuratd2_Drpu, GenerateRpuratd2_drpuRegister()},
                {(long)Registers.Rpuratd3_Drpu, GenerateRpuratd3_drpuRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            EMU_Reset();
        }
        
        protected enum DVDDLEBOD_LEBODBLANKINGDELAY
        {
            DLY_200US = 0, // 200us
            DLY_240US = 1, // 240us
            DLY_280US = 2, // 280us
            DLY_320US = 3, // 320us
        }
        
        protected enum RETREG_OVRRETREGOVERRIDEEN
        {
            DISABLED = 0, // 
            ENABLED = 1, // 
        }
        
        protected enum PD1PARETCTRL_PD1PARETDIS
        {
            PERIPHNORETAIN = 1, // Retain associated registers when in EM2/3
            RADIONORETAIN = 2, // Bit[1]. When set, do not retain RADIO associated registers when in EM2/3
        }
        
        protected enum PD0PWRCTRL_PD0PWRDN
        {
            PWRDNPD0B = 1, // Power Down PD0B
            PWRDNPD0C = 2, // Power Down PD0C
            PWRDNPD0D = 4, // Power Down PD0D
            PWRDNPD0E = 8, // Power Down PD0E
        }
        
        protected enum PD0PWRCTRL_EM2PDPWRDN
        {
            EM2PWRDNPD0B = 1, // Power down PD0B
            EM2PWRDNPD0C = 2, // Power down PD0C
            EM2PWRDNPD0D = 4, // Power down PD0D
            EM2PWRDNPD0E = 8, // Power Down PD0E
        }
        
        protected enum PD0PWRCTRL_DISCHRGPD
        {
            DISCHRGPD1 = 1, // Discharge PD1
            DISCHRGPD0B = 2, // Discharge PD0B
            DISCHRGPD0C = 3, // Discharge PD0C
            DISCHRGPD0D = 4, // Discharge PD0D
            DISCHRGPD0E = 8, // Discharge PD0E
        }
        
        protected enum EM4CTRL_EM4IORETMODE
        {
            DISABLE = 0, // No Retention: Pads enter reset state when entering EM4
            EM4EXIT = 1, // Retention through EM4: Pads enter reset state when exiting EM4
            SWUNLATCH = 2, // Retention through EM4 and Wakeup: software writes UNLATCH register to remove retention
        }
        
        protected enum CTRL_TEMPAVGNUM
        {
            N16 = 0, // 16 measurements
            N64 = 1, // 64 measurements
        }
        
        protected enum CTRL_EM23VSCALE
        {
            VSCALE0 = 0, // VSCALE0. 0.9v
            VSCALE1 = 1, // VSCALE1. 1.0v
            VSCALE2 = 2, // VSCALE2. 1.1v
        }
        
        protected enum STATUS_LOCK
        {
            UNLOCKED = 0, // All EMU lockable registers are unlocked.
            LOCKED = 1, // All EMU lockable registers are locked.
        }
        
        protected enum STATUS_VSCALE
        {
            VSCALE0 = 0, // Voltage scaling set to 0.9v
            VSCALE1 = 1, // Voltage scaling set to 1.0v
            VSCALE2 = 2, // Voltage scaling set to 1.1v
        }
        
        protected enum RSTCTRL_WDOG0RMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_WDOG1RMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_SYSRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // Device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_LOCKUPRMODE
        {
            DISABLED = 0, // Reset Request is Block
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_AVDDBODRMODE
        {
            DISABLED = 0, // Reset Request is block
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_IOVDD0BODRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_IOVDD1BODRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_DECBODRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset
        }
        
        protected enum RSTCTRL_M0SYSRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_M0LOCKUPRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_SEM0SYSRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_SEM0LOCKUPRMODE
        {
            DISABLED = 0, // Reset request is blocked
            ENABLED = 1, // The entire device is reset except some EMU registers
        }
        
        protected enum RSTCTRL_PRSSYSRSTDLY
        {
            NODELAY = 0, // No Delay
            DELAY0P8US = 1, // Delay 0.8us
            DELAY1P6US = 2, // Delay 1.6us
            DELAY3P2US = 3, // Delay 3.2us
        }
        
        protected enum ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL
        {
            SEL0 = 0, // Nothing selected. Read 0's
            SELVH = 1, // Temp Sense VH counter {ph_vb1, ph_vbn, ph_vh, ph_vl, chl[11:0]}. Counter value has phase correction already applied.
            SELVBE = 2, // Temp Sense VBE counter {6'd0,   cdvbe[9:0]}. Counter value has phase correction already applied
            SELCAL = 3, // ISBIAS Cal output  {3'd0,   isbias_trim_vos_cal[12:0]}
            FSM1 = 4, // {4'd0, tmpcompstate, calibrastate, cfgstate}
            FSM2 = 5, // {11'd0, tmpsensstate}
        }
        
        protected enum ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD
        {
            PERIOD_2MS = 0, // PERIOD_2MS
            PERIOD_62MS = 1, // PERIOD_62MS
            PERIOD_125MS = 2, // PERIOD_125MS
            PERIOD_250MS = 3, // PERIOD_250MS
            PERIOD_500MS = 4, // PERIOD_500MS
            PERIOD_1S = 5, // PERIOD_1S
            PERIOD_2S = 6, // PERIOD_2S
            PERIOD_4S = 7, // PERIOD_4S
        }
        
        protected enum ISBIASPERIOD_ISBIASCTRL_CALPERIOD
        {
            PERIOD_2MS = 0, // PERIOD_2MS
            PERIOD_62MS = 1, // PERIOD_62MS
            PERIOD_125MS = 2, // PERIOD_125MS
            PERIOD_250MS = 3, // PERIOD_250MS
            PERIOD_500MS = 4, // PERIOD_500MS
            PERIOD_1S = 5, // PERIOD_1S
            PERIOD_2S = 6, // PERIOD_2S
            PERIOD_4S = 7, // PERIOD_4S
        }
        
        protected enum ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE
        {
            RATE_16HZ = 0, // RATE_16HZ
            RATE_62HZ = 1, // RATE_62HZ
            RATE_250HZ = 2, // RATE_250HZ
            RATE_500HZ = 3, // RATE_500HZ
            RATE_1KHZ = 4, // RATE_1KHZ
            RATE_2KHZ = 5, // RATE_2KHZ
            RATE_4KHZ = 6, // RATE_4KHZ
            RATE_CONTI = 7, // RATE_CONTI
        }
        
        protected enum ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT
        {
            N1 = 0, // 
            N2 = 1, // 
            N4 = 2, // 
            N8 = 3, // 
            N16 = 4, // 
            N32 = 5, // 
            N64 = 6, // 
            N128 = 7, // 
        }
        
        protected enum ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO
        {
            DIV1 = 0, // 
            DIV4 = 1, // 
            DIV8 = 2, // 
            DIV16 = 3, // 
        }
        
        protected enum ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO
        {
            NODIV = 0, // NODIV
            DIV2 = 1, // DIV2
            DIV4 = 2, // DIV4
            DIV8 = 3, // DIV8
            DIV16 = 4, // DIV16
            DIV32 = 5, // DIV32
            DIV64 = 6, // DIV64
            CONT = 7, // CONT
        }
        
        protected enum ISBIASSTATUS_ISBIASCTRL_TESTLOCK
        {
            UNLOCKED = 0, // All EMU lockable TEST registers are unlocked.
            LOCKED = 1, // All EMU lockable TEST registers are locked.
        }
        
        protected enum VSBTEMPCOMP_ISBIASCTRL_R0VSB
        {
            VSB100 = 0, // VSB100 is set
            VSB200 = 1, // VSB200 is set
            VSB300 = 2, // VSB300 is set
            VSB400 = 3, // VSB400 is set
        }
        
        protected enum VSBTEMPCOMP_ISBIASCTRL_R1VSB
        {
            VSB100 = 0, // VSB100 is set
            VSB200 = 1, // VSB200 is set
            VSB300 = 2, // VSB300 is set
            VSB400 = 3, // VSB400 is set
        }
        
        protected enum VSBTEMPCOMP_ISBIASCTRL_R2VSB
        {
            VSB100 = 0, // VSB100 is set
            VSB200 = 1, // VSB200 is set
            VSB300 = 2, // VSB300 is set
            VSB400 = 3, // VSB400 is set
        }
        
        protected enum VSBTEMPCOMP_ISBIASCTRL_R3VSB
        {
            VSB100 = 0, // VSB100 is set
            VSB200 = 1, // VSB200 is set
            VSB300 = 2, // VSB300 is set
            VSB400 = 3, // VSB400 is set
        }
        
        // Ldreg - Offset : 0x0
        protected DoubleWordRegister  GenerateLdregRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 2, out ldreg_ldregbiasctrl_field, 
                    valueProviderCallback: (_) => {
                        Ldreg_Ldregbiasctrl_ValueProvider(_);
                        return ldreg_ldregbiasctrl_field.Value;               
                    },
                    writeCallback: (_, __) => Ldreg_Ldregbiasctrl_Write(_, __),
                    readCallback: (_, __) => Ldreg_Ldregbiasctrl_Read(_, __),
                    name: "Ldregbiasctrl")
            .WithReservedBits(2, 28)
            .WithFlag(30, out ldreg_ovrldregen_bit, 
                    valueProviderCallback: (_) => {
                        Ldreg_Ovrldregen_ValueProvider(_);
                        return ldreg_ovrldregen_bit.Value;               
                    },
                    writeCallback: (_, __) => Ldreg_Ovrldregen_Write(_, __),
                    readCallback: (_, __) => Ldreg_Ovrldregen_Read(_, __),
                    name: "Ovrldregen")
            .WithFlag(31, out ldreg_ovrldregoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Ldreg_Ovrldregoverrideen_ValueProvider(_);
                        return ldreg_ovrldregoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Ldreg_Ovrldregoverrideen_Write(_, __),
                    readCallback: (_, __) => Ldreg_Ovrldregoverrideen_Read(_, __),
                    name: "Ovrldregoverrideen")
            .WithReadCallback((_, __) => Ldreg_Read(_, __))
            .WithWriteCallback((_, __) => Ldreg_Write(_, __));
        
        // Dvddlebod - Offset : 0x4
        protected DoubleWordRegister  GenerateDvddlebodRegister() => new DoubleWordRegister(this, 0xF6)
            .WithFlag(0, out dvddlebod_dvddleboden_bit, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Dvddleboden_ValueProvider(_);
                        return dvddlebod_dvddleboden_bit.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Dvddleboden_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Dvddleboden_Read(_, __),
                    name: "Dvddleboden")
            .WithFlag(1, out dvddlebod_dvddlebodmask_bit, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Dvddlebodmask_ValueProvider(_);
                        return dvddlebod_dvddlebodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Dvddlebodmask_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Dvddlebodmask_Read(_, __),
                    name: "Dvddlebodmask")
            .WithFlag(2, out dvddlebod_dvddleboddisem01_bit, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Dvddleboddisem01_ValueProvider(_);
                        return dvddlebod_dvddleboddisem01_bit.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Dvddleboddisem01_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Dvddleboddisem01_Read(_, __),
                    name: "Dvddleboddisem01")
            .WithReservedBits(3, 1)
            .WithValueField(4, 5, out dvddlebod_dvddlebodtrim_field, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Dvddlebodtrim_ValueProvider(_);
                        return dvddlebod_dvddlebodtrim_field.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Dvddlebodtrim_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Dvddlebodtrim_Read(_, __),
                    name: "Dvddlebodtrim")
            .WithReservedBits(9, 3)
            .WithValueField(12, 4, out dvddlebod_dvddlebodbiastrim_field, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Dvddlebodbiastrim_ValueProvider(_);
                        return dvddlebod_dvddlebodbiastrim_field.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Dvddlebodbiastrim_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Dvddlebodbiastrim_Read(_, __),
                    name: "Dvddlebodbiastrim")
            .WithReservedBits(16, 2)
            .WithValueField(18, 2, out dvddlebod_dvddlebodmode_field, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Dvddlebodmode_ValueProvider(_);
                        return dvddlebod_dvddlebodmode_field.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Dvddlebodmode_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Dvddlebodmode_Read(_, __),
                    name: "Dvddlebodmode")
            .WithReservedBits(20, 1)
            .WithEnumField<DoubleWordRegister, DVDDLEBOD_LEBODBLANKINGDELAY>(21, 2, out dvddlebod_lebodblankingdelay_field, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Lebodblankingdelay_ValueProvider(_);
                        return dvddlebod_lebodblankingdelay_field.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Lebodblankingdelay_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Lebodblankingdelay_Read(_, __),
                    name: "Lebodblankingdelay")
            .WithReservedBits(23, 8)
            .WithFlag(31, out dvddlebod_ovrlebodoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Dvddlebod_Ovrlebodoverrideen_ValueProvider(_);
                        return dvddlebod_ovrlebodoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dvddlebod_Ovrlebodoverrideen_Write(_, __),
                    readCallback: (_, __) => Dvddlebod_Ovrlebodoverrideen_Read(_, __),
                    name: "Ovrlebodoverrideen")
            .WithReadCallback((_, __) => Dvddlebod_Read(_, __))
            .WithWriteCallback((_, __) => Dvddlebod_Write(_, __));
        
        // Vlmthv - Offset : 0x8
        protected DoubleWordRegister  GenerateVlmthvRegister() => new DoubleWordRegister(this, 0x81)
            .WithValueField(0, 2, out vlmthv_vlmthvtrim_field, 
                    valueProviderCallback: (_) => {
                        Vlmthv_Vlmthvtrim_ValueProvider(_);
                        return vlmthv_vlmthvtrim_field.Value;               
                    },
                    writeCallback: (_, __) => Vlmthv_Vlmthvtrim_Write(_, __),
                    readCallback: (_, __) => Vlmthv_Vlmthvtrim_Read(_, __),
                    name: "Vlmthvtrim")
            .WithReservedBits(2, 2)
            .WithFlag(4, out vlmthv_vlmthventestload_bit, 
                    valueProviderCallback: (_) => {
                        Vlmthv_Vlmthventestload_ValueProvider(_);
                        return vlmthv_vlmthventestload_bit.Value;               
                    },
                    writeCallback: (_, __) => Vlmthv_Vlmthventestload_Write(_, __),
                    readCallback: (_, __) => Vlmthv_Vlmthventestload_Read(_, __),
                    name: "Vlmthventestload")
            .WithFlag(5, out vlmthv_vlmthvenstress_bit, 
                    valueProviderCallback: (_) => {
                        Vlmthv_Vlmthvenstress_ValueProvider(_);
                        return vlmthv_vlmthvenstress_bit.Value;               
                    },
                    writeCallback: (_, __) => Vlmthv_Vlmthvenstress_Write(_, __),
                    readCallback: (_, __) => Vlmthv_Vlmthvenstress_Read(_, __),
                    name: "Vlmthvenstress")
            .WithFlag(6, out vlmthv_vlmthvforcebypass_bit, 
                    valueProviderCallback: (_) => {
                        Vlmthv_Vlmthvforcebypass_ValueProvider(_);
                        return vlmthv_vlmthvforcebypass_bit.Value;               
                    },
                    writeCallback: (_, __) => Vlmthv_Vlmthvforcebypass_Write(_, __),
                    readCallback: (_, __) => Vlmthv_Vlmthvforcebypass_Read(_, __),
                    name: "Vlmthvforcebypass")
            .WithFlag(7, out vlmthv_vlmthvforceua_bit, 
                    valueProviderCallback: (_) => {
                        Vlmthv_Vlmthvforceua_ValueProvider(_);
                        return vlmthv_vlmthvforceua_bit.Value;               
                    },
                    writeCallback: (_, __) => Vlmthv_Vlmthvforceua_Write(_, __),
                    readCallback: (_, __) => Vlmthv_Vlmthvforceua_Read(_, __),
                    name: "Vlmthvforceua")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Vlmthv_Read(_, __))
            .WithWriteCallback((_, __) => Vlmthv_Write(_, __));
        
        // Dvddbod - Offset : 0xC
        protected DoubleWordRegister  GenerateDvddbodRegister() => new DoubleWordRegister(this, 0x17)
            .WithValueField(0, 6, out dvddbod_dvddbodthreshold_field, 
                    valueProviderCallback: (_) => {
                        Dvddbod_Dvddbodthreshold_ValueProvider(_);
                        return dvddbod_dvddbodthreshold_field.Value;               
                    },
                    writeCallback: (_, __) => Dvddbod_Dvddbodthreshold_Write(_, __),
                    readCallback: (_, __) => Dvddbod_Dvddbodthreshold_Read(_, __),
                    name: "Dvddbodthreshold")
            .WithReservedBits(6, 2)
            .WithFlag(8, out dvddbod_dvddbodmask_bit, 
                    valueProviderCallback: (_) => {
                        Dvddbod_Dvddbodmask_ValueProvider(_);
                        return dvddbod_dvddbodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Dvddbod_Dvddbodmask_Write(_, __),
                    readCallback: (_, __) => Dvddbod_Dvddbodmask_Read(_, __),
                    name: "Dvddbodmask")
            .WithReservedBits(9, 21)
            .WithFlag(30, out dvddbod_ovrhvbodbodthresholdsenseen_bit, 
                    valueProviderCallback: (_) => {
                        Dvddbod_Ovrhvbodbodthresholdsenseen_ValueProvider(_);
                        return dvddbod_ovrhvbodbodthresholdsenseen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dvddbod_Ovrhvbodbodthresholdsenseen_Write(_, __),
                    readCallback: (_, __) => Dvddbod_Ovrhvbodbodthresholdsenseen_Read(_, __),
                    name: "Ovrhvbodbodthresholdsenseen")
            .WithFlag(31, out dvddbod_ovrhvbodoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Dvddbod_Ovrhvbodoverrideen_ValueProvider(_);
                        return dvddbod_ovrhvbodoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Dvddbod_Ovrhvbodoverrideen_Write(_, __),
                    readCallback: (_, __) => Dvddbod_Ovrhvbodoverrideen_Read(_, __),
                    name: "Ovrhvbodoverrideen")
            .WithReadCallback((_, __) => Dvddbod_Read(_, __))
            .WithWriteCallback((_, __) => Dvddbod_Write(_, __));
        
        // Decbod - Offset : 0x10
        protected DoubleWordRegister  GenerateDecbodRegister() => new DoubleWordRegister(this, 0x22)
            .WithFlag(0, out decbod_decboden_bit, 
                    valueProviderCallback: (_) => {
                        Decbod_Decboden_ValueProvider(_);
                        return decbod_decboden_bit.Value;               
                    },
                    writeCallback: (_, __) => Decbod_Decboden_Write(_, __),
                    readCallback: (_, __) => Decbod_Decboden_Read(_, __),
                    name: "Decboden")
            .WithFlag(1, out decbod_decbodmask_bit, 
                    valueProviderCallback: (_) => {
                        Decbod_Decbodmask_ValueProvider(_);
                        return decbod_decbodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Decbod_Decbodmask_Write(_, __),
                    readCallback: (_, __) => Decbod_Decbodmask_Read(_, __),
                    name: "Decbodmask")
            .WithReservedBits(2, 2)
            .WithFlag(4, out decbod_decovmboden_bit, 
                    valueProviderCallback: (_) => {
                        Decbod_Decovmboden_ValueProvider(_);
                        return decbod_decovmboden_bit.Value;               
                    },
                    writeCallback: (_, __) => Decbod_Decovmboden_Write(_, __),
                    readCallback: (_, __) => Decbod_Decovmboden_Read(_, __),
                    name: "Decovmboden")
            .WithFlag(5, out decbod_decovmbodmask_bit, 
                    valueProviderCallback: (_) => {
                        Decbod_Decovmbodmask_ValueProvider(_);
                        return decbod_decovmbodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Decbod_Decovmbodmask_Write(_, __),
                    readCallback: (_, __) => Decbod_Decovmbodmask_Read(_, __),
                    name: "Decovmbodmask")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Decbod_Read(_, __))
            .WithWriteCallback((_, __) => Decbod_Write(_, __));
        
        // Hdreg - Offset : 0x14
        protected DoubleWordRegister  GenerateHdregRegister() => new DoubleWordRegister(this, 0x8)
            .WithValueField(0, 4, out hdreg_hdregtrimvreg_field, 
                    valueProviderCallback: (_) => {
                        Hdreg_Hdregtrimvreg_ValueProvider(_);
                        return hdreg_hdregtrimvreg_field.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Hdregtrimvreg_Write(_, __),
                    readCallback: (_, __) => Hdreg_Hdregtrimvreg_Read(_, __),
                    name: "Hdregtrimvreg")
            .WithReservedBits(4, 15)
            .WithFlag(19, out hdreg_ovrhdregswhardswlowleak_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregswhardswlowleak_ValueProvider(_);
                        return hdreg_ovrhdregswhardswlowleak_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregswhardswlowleak_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregswhardswlowleak_Read(_, __),
                    name: "Ovrhdregswhardswlowleak")
            .WithFlag(20, out hdreg_ovrhdregswsoftswon_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregswsoftswon_ValueProvider(_);
                        return hdreg_ovrhdregswsoftswon_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregswsoftswon_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregswsoftswon_Read(_, __),
                    name: "Ovrhdregswsoftswon")
            .WithFlag(21, out hdreg_ovrhdregswhardswon_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregswhardswon_ValueProvider(_);
                        return hdreg_ovrhdregswhardswon_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregswhardswon_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregswhardswon_Read(_, __),
                    name: "Ovrhdregswhardswon")
            .WithFlag(22, out hdreg_ovrhdregswoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregswoverrideen_ValueProvider(_);
                        return hdreg_ovrhdregswoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregswoverrideen_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregswoverrideen_Read(_, __),
                    name: "Ovrhdregswoverrideen")
            .WithReservedBits(23, 4)
            .WithFlag(27, out hdreg_ovrhdregwarmstart_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregwarmstart_ValueProvider(_);
                        return hdreg_ovrhdregwarmstart_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregwarmstart_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregwarmstart_Read(_, __),
                    name: "Ovrhdregwarmstart")
            .WithFlag(28, out hdreg_ovrhdregenramp_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregenramp_ValueProvider(_);
                        return hdreg_ovrhdregenramp_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregenramp_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregenramp_Read(_, __),
                    name: "Ovrhdregenramp")
            .WithFlag(29, out hdreg_ovrhdregenreg_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregenreg_ValueProvider(_);
                        return hdreg_ovrhdregenreg_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregenreg_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregenreg_Read(_, __),
                    name: "Ovrhdregenreg")
            .WithFlag(30, out hdreg_ovrhdregen_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregen_ValueProvider(_);
                        return hdreg_ovrhdregen_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregen_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregen_Read(_, __),
                    name: "Ovrhdregen")
            .WithFlag(31, out hdreg_ovrhdregoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Hdreg_Ovrhdregoverrideen_ValueProvider(_);
                        return hdreg_ovrhdregoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Hdreg_Ovrhdregoverrideen_Write(_, __),
                    readCallback: (_, __) => Hdreg_Ovrhdregoverrideen_Read(_, __),
                    name: "Ovrhdregoverrideen")
            .WithReadCallback((_, __) => Hdreg_Read(_, __))
            .WithWriteCallback((_, __) => Hdreg_Write(_, __));
        
        // Retreg - Offset : 0x18
        protected DoubleWordRegister  GenerateRetregRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 4, out retreg_retreghighsidetrim_field, 
                    valueProviderCallback: (_) => {
                        Retreg_Retreghighsidetrim_ValueProvider(_);
                        return retreg_retreghighsidetrim_field.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Retreghighsidetrim_Write(_, __),
                    readCallback: (_, __) => Retreg_Retreghighsidetrim_Read(_, __),
                    name: "Retreghighsidetrim")
            .WithFlag(4, out retreg_retreghstrimtempcompen_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Retreghstrimtempcompen_ValueProvider(_);
                        return retreg_retreghstrimtempcompen_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Retreghstrimtempcompen_Write(_, __),
                    readCallback: (_, __) => Retreg_Retreghstrimtempcompen_Read(_, __),
                    name: "Retreghstrimtempcompen")
            .WithReservedBits(5, 3)
            .WithValueField(8, 4, out retreg_retregidactrim_field, 
                    valueProviderCallback: (_) => {
                        Retreg_Retregidactrim_ValueProvider(_);
                        return retreg_retregidactrim_field.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Retregidactrim_Write(_, __),
                    readCallback: (_, __) => Retreg_Retregidactrim_Read(_, __),
                    name: "Retregidactrim")
            .WithReservedBits(12, 4)
            .WithFlag(16, out retreg_retregcalrst_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Retregcalrst_ValueProvider(_);
                        return retreg_retregcalrst_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Retregcalrst_Write(_, __),
                    readCallback: (_, __) => Retreg_Retregcalrst_Read(_, __),
                    name: "Retregcalrst")
            .WithFlag(17, out retreg_retregcalen_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Retregcalen_ValueProvider(_);
                        return retreg_retregcalen_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Retregcalen_Write(_, __),
                    readCallback: (_, __) => Retreg_Retregcalen_Read(_, __),
                    name: "Retregcalen")
            .WithFlag(18, out retreg_retregtristateen_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Retregtristateen_ValueProvider(_);
                        return retreg_retregtristateen_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Retregtristateen_Write(_, __),
                    readCallback: (_, __) => Retreg_Retregtristateen_Read(_, __),
                    name: "Retregtristateen")
            .WithReservedBits(19, 9)
            .WithFlag(28, out retreg_ovrretreghighsidepuweakdis_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Ovrretreghighsidepuweakdis_ValueProvider(_);
                        return retreg_ovrretreghighsidepuweakdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Ovrretreghighsidepuweakdis_Write(_, __),
                    readCallback: (_, __) => Retreg_Ovrretreghighsidepuweakdis_Read(_, __),
                    name: "Ovrretreghighsidepuweakdis")
            .WithFlag(29, out retreg_ovrretreghighsidepudis_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Ovrretreghighsidepudis_ValueProvider(_);
                        return retreg_ovrretreghighsidepudis_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Ovrretreghighsidepudis_Write(_, __),
                    readCallback: (_, __) => Retreg_Ovrretreghighsidepudis_Read(_, __),
                    name: "Ovrretreghighsidepudis")
            .WithFlag(30, out retreg_ovrretregbypassen_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Ovrretregbypassen_ValueProvider(_);
                        return retreg_ovrretregbypassen_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Ovrretregbypassen_Write(_, __),
                    readCallback: (_, __) => Retreg_Ovrretregbypassen_Read(_, __),
                    name: "Ovrretregbypassen")
            .WithEnumField<DoubleWordRegister, RETREG_OVRRETREGOVERRIDEEN>(31, 1, out retreg_ovrretregoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Retreg_Ovrretregoverrideen_ValueProvider(_);
                        return retreg_ovrretregoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Retreg_Ovrretregoverrideen_Write(_, __),
                    readCallback: (_, __) => Retreg_Ovrretregoverrideen_Read(_, __),
                    name: "Ovrretregoverrideen")
            .WithReadCallback((_, __) => Retreg_Read(_, __))
            .WithWriteCallback((_, __) => Retreg_Write(_, __));
        
        // Bod3sensetrim - Offset : 0x1C
        protected DoubleWordRegister  GenerateBod3sensetrimRegister() => new DoubleWordRegister(this, 0x4A52)
            .WithValueField(0, 5, out bod3sensetrim_avddbodtrim_field, 
                    valueProviderCallback: (_) => {
                        Bod3sensetrim_Avddbodtrim_ValueProvider(_);
                        return bod3sensetrim_avddbodtrim_field.Value;               
                    },
                    writeCallback: (_, __) => Bod3sensetrim_Avddbodtrim_Write(_, __),
                    readCallback: (_, __) => Bod3sensetrim_Avddbodtrim_Read(_, __),
                    name: "Avddbodtrim")
            .WithValueField(5, 5, out bod3sensetrim_vddio0bodtrim_field, 
                    valueProviderCallback: (_) => {
                        Bod3sensetrim_Vddio0bodtrim_ValueProvider(_);
                        return bod3sensetrim_vddio0bodtrim_field.Value;               
                    },
                    writeCallback: (_, __) => Bod3sensetrim_Vddio0bodtrim_Write(_, __),
                    readCallback: (_, __) => Bod3sensetrim_Vddio0bodtrim_Read(_, __),
                    name: "Vddio0bodtrim")
            .WithValueField(10, 5, out bod3sensetrim_vddio1bodtrim_field, 
                    valueProviderCallback: (_) => {
                        Bod3sensetrim_Vddio1bodtrim_ValueProvider(_);
                        return bod3sensetrim_vddio1bodtrim_field.Value;               
                    },
                    writeCallback: (_, __) => Bod3sensetrim_Vddio1bodtrim_Write(_, __),
                    readCallback: (_, __) => Bod3sensetrim_Vddio1bodtrim_Read(_, __),
                    name: "Vddio1bodtrim")
            .WithReservedBits(15, 5)
            .WithFlag(20, out bod3sensetrim_bod3sensemode_bit, 
                    valueProviderCallback: (_) => {
                        Bod3sensetrim_Bod3sensemode_ValueProvider(_);
                        return bod3sensetrim_bod3sensemode_bit.Value;               
                    },
                    writeCallback: (_, __) => Bod3sensetrim_Bod3sensemode_Write(_, __),
                    readCallback: (_, __) => Bod3sensetrim_Bod3sensemode_Read(_, __),
                    name: "Bod3sensemode")
            .WithReservedBits(21, 11)
            .WithReadCallback((_, __) => Bod3sensetrim_Read(_, __))
            .WithWriteCallback((_, __) => Bod3sensetrim_Write(_, __));
        
        // Bod3sense - Offset : 0x20
        protected DoubleWordRegister  GenerateBod3senseRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out bod3sense_avddboden_bit, 
                    valueProviderCallback: (_) => {
                        Bod3sense_Avddboden_ValueProvider(_);
                        return bod3sense_avddboden_bit.Value;               
                    },
                    writeCallback: (_, __) => Bod3sense_Avddboden_Write(_, __),
                    readCallback: (_, __) => Bod3sense_Avddboden_Read(_, __),
                    name: "Avddboden")
            .WithFlag(1, out bod3sense_vddio0boden_bit, 
                    valueProviderCallback: (_) => {
                        Bod3sense_Vddio0boden_ValueProvider(_);
                        return bod3sense_vddio0boden_bit.Value;               
                    },
                    writeCallback: (_, __) => Bod3sense_Vddio0boden_Write(_, __),
                    readCallback: (_, __) => Bod3sense_Vddio0boden_Read(_, __),
                    name: "Vddio0boden")
            .WithFlag(2, out bod3sense_vddio1boden_bit, 
                    valueProviderCallback: (_) => {
                        Bod3sense_Vddio1boden_ValueProvider(_);
                        return bod3sense_vddio1boden_bit.Value;               
                    },
                    writeCallback: (_, __) => Bod3sense_Vddio1boden_Write(_, __),
                    readCallback: (_, __) => Bod3sense_Vddio1boden_Read(_, __),
                    name: "Vddio1boden")
            .WithReservedBits(3, 1)
            .WithFlag(4, out bod3sense_avddbodmask_bit, 
                    valueProviderCallback: (_) => {
                        Bod3sense_Avddbodmask_ValueProvider(_);
                        return bod3sense_avddbodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Bod3sense_Avddbodmask_Write(_, __),
                    readCallback: (_, __) => Bod3sense_Avddbodmask_Read(_, __),
                    name: "Avddbodmask")
            .WithFlag(5, out bod3sense_vddio0bodmask_bit, 
                    valueProviderCallback: (_) => {
                        Bod3sense_Vddio0bodmask_ValueProvider(_);
                        return bod3sense_vddio0bodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Bod3sense_Vddio0bodmask_Write(_, __),
                    readCallback: (_, __) => Bod3sense_Vddio0bodmask_Read(_, __),
                    name: "Vddio0bodmask")
            .WithFlag(6, out bod3sense_vddio1bodmask_bit, 
                    valueProviderCallback: (_) => {
                        Bod3sense_Vddio1bodmask_ValueProvider(_);
                        return bod3sense_vddio1bodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Bod3sense_Vddio1bodmask_Write(_, __),
                    readCallback: (_, __) => Bod3sense_Vddio1bodmask_Read(_, __),
                    name: "Vddio1bodmask")
            .WithReservedBits(7, 25)
            .WithReadCallback((_, __) => Bod3sense_Read(_, __))
            .WithWriteCallback((_, __) => Bod3sense_Write(_, __));
        
        // Isbias - Offset : 0x24
        protected DoubleWordRegister  GenerateIsbiasRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out isbias_ovrpfmperprep_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrpfmperprep_ValueProvider(_);
                        return isbias_ovrpfmperprep_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrpfmperprep_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrpfmperprep_Read(_, __),
                    name: "Ovrpfmperprep")
            .WithFlag(1, out isbias_ovrpfmperpresamp_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrpfmperpresamp_ValueProvider(_);
                        return isbias_ovrpfmperpresamp_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrpfmperpresamp_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrpfmperpresamp_Read(_, __),
                    name: "Ovrpfmperpresamp")
            .WithFlag(2, out isbias_ovrpfmpersamp_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrpfmpersamp_ValueProvider(_);
                        return isbias_ovrpfmpersamp_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrpfmpersamp_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrpfmpersamp_Read(_, __),
                    name: "Ovrpfmpersamp")
            .WithFlag(3, out isbias_ovrpfmperoverriderfrsh_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrpfmperoverriderfrsh_ValueProvider(_);
                        return isbias_ovrpfmperoverriderfrsh_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrpfmperoverriderfrsh_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrpfmperoverriderfrsh_Read(_, __),
                    name: "Ovrpfmperoverriderfrsh")
            .WithReservedBits(4, 1)
            .WithFlag(5, out isbias_ovrisbiasprep_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasprep_ValueProvider(_);
                        return isbias_ovrisbiasprep_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasprep_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasprep_Read(_, __),
                    name: "Ovrisbiasprep")
            .WithFlag(6, out isbias_ovrisbiaspresamp_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiaspresamp_ValueProvider(_);
                        return isbias_ovrisbiaspresamp_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiaspresamp_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiaspresamp_Read(_, __),
                    name: "Ovrisbiaspresamp")
            .WithFlag(7, out isbias_ovrisbiassamp_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiassamp_ValueProvider(_);
                        return isbias_ovrisbiassamp_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiassamp_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiassamp_Read(_, __),
                    name: "Ovrisbiassamp")
            .WithFlag(8, out isbias_ovrisbiasoverriderfrsh_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasoverriderfrsh_ValueProvider(_);
                        return isbias_ovrisbiasoverriderfrsh_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasoverriderfrsh_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasoverriderfrsh_Read(_, __),
                    name: "Ovrisbiasoverriderfrsh")
            .WithReservedBits(9, 2)
            .WithFlag(11, out isbias_ovrisbiastsensestart_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiastsensestart_ValueProvider(_);
                        return isbias_ovrisbiastsensestart_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiastsensestart_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiastsensestart_Read(_, __),
                    name: "Ovrisbiastsensestart")
            .WithFlag(12, out isbias_ovrisbiastsenseen_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiastsenseen_ValueProvider(_);
                        return isbias_ovrisbiastsenseen_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiastsenseen_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiastsenseen_Read(_, __),
                    name: "Ovrisbiastsenseen")
            .WithFlag(13, out isbias_ovrisbiasselvl_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasselvl_ValueProvider(_);
                        return isbias_ovrisbiasselvl_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasselvl_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasselvl_Read(_, __),
                    name: "Ovrisbiasselvl")
            .WithFlag(14, out isbias_ovrisbiasselvh_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasselvh_ValueProvider(_);
                        return isbias_ovrisbiasselvh_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasselvh_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasselvh_Read(_, __),
                    name: "Ovrisbiasselvh")
            .WithFlag(15, out isbias_ovrisbiasselvbn_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasselvbn_ValueProvider(_);
                        return isbias_ovrisbiasselvbn_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasselvbn_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasselvbn_Read(_, __),
                    name: "Ovrisbiasselvbn")
            .WithFlag(16, out isbias_ovrisbiasselvb1_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasselvb1_ValueProvider(_);
                        return isbias_ovrisbiasselvb1_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasselvb1_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasselvb1_Read(_, __),
                    name: "Ovrisbiasselvb1")
            .WithFlag(17, out isbias_ovrisbiasrsttsensecomp_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasrsttsensecomp_ValueProvider(_);
                        return isbias_ovrisbiasrsttsensecomp_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasrsttsensecomp_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasrsttsensecomp_Read(_, __),
                    name: "Ovrisbiasrsttsensecomp")
            .WithFlag(18, out isbias_ovrisbiasoverridetemp_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasoverridetemp_ValueProvider(_);
                        return isbias_ovrisbiasoverridetemp_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasoverridetemp_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasoverridetemp_Read(_, __),
                    name: "Ovrisbiasoverridetemp")
            .WithReservedBits(19, 2)
            .WithFlag(21, out isbias_ovrisbiascalen_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiascalen_ValueProvider(_);
                        return isbias_ovrisbiascalen_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiascalen_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiascalen_Read(_, __),
                    name: "Ovrisbiascalen")
            .WithFlag(22, out isbias_ovrisbiascalrst_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiascalrst_ValueProvider(_);
                        return isbias_ovrisbiascalrst_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiascalrst_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiascalrst_Read(_, __),
                    name: "Ovrisbiascalrst")
            .WithFlag(23, out isbias_ovrisbiasoverridecal_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasoverridecal_ValueProvider(_);
                        return isbias_ovrisbiasoverridecal_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasoverridecal_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasoverridecal_Read(_, __),
                    name: "Ovrisbiasoverridecal")
            .WithReservedBits(24, 2)
            .WithFlag(26, out isbias_ovrisbiaswakeup_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiaswakeup_ValueProvider(_);
                        return isbias_ovrisbiaswakeup_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiaswakeup_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiaswakeup_Read(_, __),
                    name: "Ovrisbiaswakeup")
            .WithFlag(27, out isbias_ovrisbiasoscen_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasoscen_ValueProvider(_);
                        return isbias_ovrisbiasoscen_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasoscen_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasoscen_Read(_, __),
                    name: "Ovrisbiasoscen")
            .WithFlag(28, out isbias_ovrisbiasbgcont_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasbgcont_ValueProvider(_);
                        return isbias_ovrisbiasbgcont_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasbgcont_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasbgcont_Read(_, __),
                    name: "Ovrisbiasbgcont")
            .WithFlag(29, out isbias_ovrisbiassampbufen_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiassampbufen_ValueProvider(_);
                        return isbias_ovrisbiassampbufen_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiassampbufen_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiassampbufen_Read(_, __),
                    name: "Ovrisbiassampbufen")
            .WithFlag(30, out isbias_ovrisbiascscont_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiascscont_ValueProvider(_);
                        return isbias_ovrisbiascscont_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiascscont_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiascscont_Read(_, __),
                    name: "Ovrisbiascscont")
            .WithFlag(31, out isbias_ovrisbiasoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Isbias_Ovrisbiasoverrideen_ValueProvider(_);
                        return isbias_ovrisbiasoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbias_Ovrisbiasoverrideen_Write(_, __),
                    readCallback: (_, __) => Isbias_Ovrisbiasoverrideen_Read(_, __),
                    name: "Ovrisbiasoverrideen")
            .WithReadCallback((_, __) => Isbias_Read(_, __))
            .WithWriteCallback((_, __) => Isbias_Write(_, __));
        
        // Isbiastrim - Offset : 0x28
        protected DoubleWordRegister  GenerateIsbiastrimRegister() => new DoubleWordRegister(this, 0x1D442523)
            .WithValueField(0, 3, out isbiastrim_isbiastrim1p1_field, 
                    valueProviderCallback: (_) => {
                        Isbiastrim_Isbiastrim1p1_ValueProvider(_);
                        return isbiastrim_isbiastrim1p1_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastrim_Isbiastrim1p1_Write(_, __),
                    readCallback: (_, __) => Isbiastrim_Isbiastrim1p1_Read(_, __),
                    name: "Isbiastrim1p1")
            .WithReservedBits(3, 1)
            .WithValueField(4, 5, out isbiastrim_isbiastrimltc_field, 
                    valueProviderCallback: (_) => {
                        Isbiastrim_Isbiastrimltc_ValueProvider(_);
                        return isbiastrim_isbiastrimltc_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastrim_Isbiastrimltc_Write(_, __),
                    readCallback: (_, __) => Isbiastrim_Isbiastrimltc_Read(_, __),
                    name: "Isbiastrimltc")
            .WithReservedBits(9, 1)
            .WithValueField(10, 4, out isbiastrim_isbiastrimoscrc_field, 
                    valueProviderCallback: (_) => {
                        Isbiastrim_Isbiastrimoscrc_ValueProvider(_);
                        return isbiastrim_isbiastrimoscrc_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastrim_Isbiastrimoscrc_Write(_, __),
                    readCallback: (_, __) => Isbiastrim_Isbiastrimoscrc_Read(_, __),
                    name: "Isbiastrimoscrc")
            .WithReservedBits(14, 1)
            .WithValueField(15, 4, out isbiastrim_isbiastrimtc_field, 
                    valueProviderCallback: (_) => {
                        Isbiastrim_Isbiastrimtc_ValueProvider(_);
                        return isbiastrim_isbiastrimtc_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastrim_Isbiastrimtc_Write(_, __),
                    readCallback: (_, __) => Isbiastrim_Isbiastrimtc_Read(_, __),
                    name: "Isbiastrimtc")
            .WithReservedBits(19, 1)
            .WithValueField(20, 5, out isbiastrim_isbiastrimoscgmc_field, 
                    valueProviderCallback: (_) => {
                        Isbiastrim_Isbiastrimoscgmc_ValueProvider(_);
                        return isbiastrim_isbiastrimoscgmc_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastrim_Isbiastrimoscgmc_Write(_, __),
                    readCallback: (_, __) => Isbiastrim_Isbiastrimoscgmc_Read(_, __),
                    name: "Isbiastrimoscgmc")
            .WithReservedBits(25, 1)
            .WithValueField(26, 4, out isbiastrim_isbiastrim1p18_field, 
                    valueProviderCallback: (_) => {
                        Isbiastrim_Isbiastrim1p18_ValueProvider(_);
                        return isbiastrim_isbiastrim1p18_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastrim_Isbiastrim1p18_Write(_, __),
                    readCallback: (_, __) => Isbiastrim_Isbiastrim1p18_Read(_, __),
                    name: "Isbiastrim1p18")
            .WithReservedBits(30, 2)
            .WithReadCallback((_, __) => Isbiastrim_Read(_, __))
            .WithWriteCallback((_, __) => Isbiastrim_Write(_, __));
        
        // Isbiasvrefregtrim - Offset : 0x2C
        protected DoubleWordRegister  GenerateIsbiasvrefregtrimRegister() => new DoubleWordRegister(this, 0x1A406)
            .WithValueField(0, 5, out isbiasvrefregtrim_vregvscale0trim_field, 
                    valueProviderCallback: (_) => {
                        Isbiasvrefregtrim_Vregvscale0trim_ValueProvider(_);
                        return isbiasvrefregtrim_vregvscale0trim_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasvrefregtrim_Vregvscale0trim_Write(_, __),
                    readCallback: (_, __) => Isbiasvrefregtrim_Vregvscale0trim_Read(_, __),
                    name: "Vregvscale0trim")
            .WithReservedBits(5, 1)
            .WithValueField(6, 5, out isbiasvrefregtrim_vregvscale1trim_field, 
                    valueProviderCallback: (_) => {
                        Isbiasvrefregtrim_Vregvscale1trim_ValueProvider(_);
                        return isbiasvrefregtrim_vregvscale1trim_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasvrefregtrim_Vregvscale1trim_Write(_, __),
                    readCallback: (_, __) => Isbiasvrefregtrim_Vregvscale1trim_Read(_, __),
                    name: "Vregvscale1trim")
            .WithReservedBits(11, 1)
            .WithValueField(12, 5, out isbiasvrefregtrim_vregvscale2trim_field, 
                    valueProviderCallback: (_) => {
                        Isbiasvrefregtrim_Vregvscale2trim_ValueProvider(_);
                        return isbiasvrefregtrim_vregvscale2trim_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasvrefregtrim_Vregvscale2trim_Write(_, __),
                    readCallback: (_, __) => Isbiasvrefregtrim_Vregvscale2trim_Read(_, __),
                    name: "Vregvscale2trim")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Isbiasvrefregtrim_Read(_, __))
            .WithWriteCallback((_, __) => Isbiasvrefregtrim_Write(_, __));
        
        // Isbiasvreflvbodtrim - Offset : 0x30
        protected DoubleWordRegister  GenerateIsbiasvreflvbodtrimRegister() => new DoubleWordRegister(this, 0x1A406)
            .WithValueField(0, 5, out isbiasvreflvbodtrim_vregm70mvscale0trim_field, 
                    valueProviderCallback: (_) => {
                        Isbiasvreflvbodtrim_Vregm70mvscale0trim_ValueProvider(_);
                        return isbiasvreflvbodtrim_vregm70mvscale0trim_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasvreflvbodtrim_Vregm70mvscale0trim_Write(_, __),
                    readCallback: (_, __) => Isbiasvreflvbodtrim_Vregm70mvscale0trim_Read(_, __),
                    name: "Vregm70mvscale0trim")
            .WithReservedBits(5, 1)
            .WithValueField(6, 5, out isbiasvreflvbodtrim_vregm70mvscale1trim_field, 
                    valueProviderCallback: (_) => {
                        Isbiasvreflvbodtrim_Vregm70mvscale1trim_ValueProvider(_);
                        return isbiasvreflvbodtrim_vregm70mvscale1trim_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasvreflvbodtrim_Vregm70mvscale1trim_Write(_, __),
                    readCallback: (_, __) => Isbiasvreflvbodtrim_Vregm70mvscale1trim_Read(_, __),
                    name: "Vregm70mvscale1trim")
            .WithReservedBits(11, 1)
            .WithValueField(12, 5, out isbiasvreflvbodtrim_vregm70mvscale2trim_field, 
                    valueProviderCallback: (_) => {
                        Isbiasvreflvbodtrim_Vregm70mvscale2trim_ValueProvider(_);
                        return isbiasvreflvbodtrim_vregm70mvscale2trim_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasvreflvbodtrim_Vregm70mvscale2trim_Write(_, __),
                    readCallback: (_, __) => Isbiasvreflvbodtrim_Vregm70mvscale2trim_Read(_, __),
                    name: "Vregm70mvscale2trim")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Isbiasvreflvbodtrim_Read(_, __))
            .WithWriteCallback((_, __) => Isbiasvreflvbodtrim_Write(_, __));
        
        // Anastatus - Offset : 0x34
        protected DoubleWordRegister  GenerateAnastatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out anastatus_dvddlebodmaskoncfgchg_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Dvddlebodmaskoncfgchg_ValueProvider(_);
                        return anastatus_dvddlebodmaskoncfgchg_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Dvddlebodmaskoncfgchg_Read(_, __),
                    name: "Dvddlebodmaskoncfgchg")
            .WithFlag(1, out anastatus_avddbodmaskoncfgchg_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Avddbodmaskoncfgchg_ValueProvider(_);
                        return anastatus_avddbodmaskoncfgchg_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Avddbodmaskoncfgchg_Read(_, __),
                    name: "Avddbodmaskoncfgchg")
            .WithFlag(2, out anastatus_vddio0bodmaskoncfgchg_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Vddio0bodmaskoncfgchg_ValueProvider(_);
                        return anastatus_vddio0bodmaskoncfgchg_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Vddio0bodmaskoncfgchg_Read(_, __),
                    name: "Vddio0bodmaskoncfgchg")
            .WithFlag(3, out anastatus_vddio1bodmaskoncfgchg_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Vddio1bodmaskoncfgchg_ValueProvider(_);
                        return anastatus_vddio1bodmaskoncfgchg_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Vddio1bodmaskoncfgchg_Read(_, __),
                    name: "Vddio1bodmaskoncfgchg")
            .WithFlag(4, out anastatus_decbodmaskoncfgchg_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Decbodmaskoncfgchg_ValueProvider(_);
                        return anastatus_decbodmaskoncfgchg_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Decbodmaskoncfgchg_Read(_, __),
                    name: "Decbodmaskoncfgchg")
            .WithFlag(5, out anastatus_vregincmpenmaskoncfgchg_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Vregincmpenmaskoncfgchg_ValueProvider(_);
                        return anastatus_vregincmpenmaskoncfgchg_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Vregincmpenmaskoncfgchg_Read(_, __),
                    name: "Vregincmpenmaskoncfgchg")
            .WithReservedBits(6, 6)
            .WithFlag(12, out anastatus_dvddbod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Dvddbod_ValueProvider(_);
                        return anastatus_dvddbod_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Dvddbod_Read(_, __),
                    name: "Dvddbod")
            .WithFlag(13, out anastatus_dvddlebod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Dvddlebod_ValueProvider(_);
                        return anastatus_dvddlebod_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Dvddlebod_Read(_, __),
                    name: "Dvddlebod")
            .WithFlag(14, out anastatus_avddbod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Avddbod_ValueProvider(_);
                        return anastatus_avddbod_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Avddbod_Read(_, __),
                    name: "Avddbod")
            .WithFlag(15, out anastatus_vddio0bod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Vddio0bod_ValueProvider(_);
                        return anastatus_vddio0bod_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Vddio0bod_Read(_, __),
                    name: "Vddio0bod")
            .WithFlag(16, out anastatus_vddio1bod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Vddio1bod_ValueProvider(_);
                        return anastatus_vddio1bod_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Vddio1bod_Read(_, __),
                    name: "Vddio1bod")
            .WithFlag(17, out anastatus_decbod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Decbod_ValueProvider(_);
                        return anastatus_decbod_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Decbod_Read(_, __),
                    name: "Decbod")
            .WithFlag(18, out anastatus_decovmbod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Decovmbod_ValueProvider(_);
                        return anastatus_decovmbod_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Decovmbod_Read(_, __),
                    name: "Decovmbod")
            .WithReservedBits(19, 5)
            .WithFlag(24, out anastatus_pfmbypvreginltthres_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Pfmbypvreginltthres_ValueProvider(_);
                        return anastatus_pfmbypvreginltthres_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Pfmbypvreginltthres_Read(_, __),
                    name: "Pfmbypvreginltthres")
            .WithFlag(25, out anastatus_pfmbypcmpout_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Anastatus_Pfmbypcmpout_ValueProvider(_);
                        return anastatus_pfmbypcmpout_bit.Value;               
                    },
                    readCallback: (_, __) => Anastatus_Pfmbypcmpout_Read(_, __),
                    name: "Pfmbypcmpout")
            .WithReservedBits(26, 6)
            .WithReadCallback((_, __) => Anastatus_Read(_, __))
            .WithWriteCallback((_, __) => Anastatus_Write(_, __));
        
        // Pfmbyp - Offset : 0x38
        protected DoubleWordRegister  GeneratePfmbypRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out pfmbyp_hyssel_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Hyssel_ValueProvider(_);
                        return pfmbyp_hyssel_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Hyssel_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Hyssel_Read(_, __),
                    name: "Hyssel")
            .WithReservedBits(1, 6)
            .WithFlag(7, out pfmbyp_climboosten_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Climboosten_ValueProvider(_);
                        return pfmbyp_climboosten_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Climboosten_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Climboosten_Read(_, __),
                    name: "Climboosten")
            .WithFlag(8, out pfmbyp_climregdis_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Climregdis_ValueProvider(_);
                        return pfmbyp_climregdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Climregdis_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Climregdis_Read(_, __),
                    name: "Climregdis")
            .WithFlag(9, out pfmbyp_cmforceen_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Cmforceen_ValueProvider(_);
                        return pfmbyp_cmforceen_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Cmforceen_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Cmforceen_Read(_, __),
                    name: "Cmforceen")
            .WithFlag(10, out pfmbyp_cmforceval_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Cmforceval_ValueProvider(_);
                        return pfmbyp_cmforceval_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Cmforceval_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Cmforceval_Read(_, __),
                    name: "Cmforceval")
            .WithFlag(11, out pfmbyp_compsel_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Compsel_ValueProvider(_);
                        return pfmbyp_compsel_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Compsel_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Compsel_Read(_, __),
                    name: "Compsel")
            .WithFlag(12, out pfmbyp_dcdcpfetdis_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Dcdcpfetdis_ValueProvider(_);
                        return pfmbyp_dcdcpfetdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Dcdcpfetdis_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Dcdcpfetdis_Read(_, __),
                    name: "Dcdcpfetdis")
            .WithFlag(13, out pfmbyp_dcdcpfetforceon_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Dcdcpfetforceon_ValueProvider(_);
                        return pfmbyp_dcdcpfetforceon_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Dcdcpfetforceon_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Dcdcpfetforceon_Read(_, __),
                    name: "Dcdcpfetforceon")
            .WithReservedBits(14, 2)
            .WithValueField(16, 3, out pfmbyp_pfmisomode_field, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Pfmisomode_ValueProvider(_);
                        return pfmbyp_pfmisomode_field.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Pfmisomode_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Pfmisomode_Read(_, __),
                    name: "Pfmisomode")
            .WithReservedBits(19, 4)
            .WithFlag(23, out pfmbyp_swweak_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Swweak_ValueProvider(_);
                        return pfmbyp_swweak_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Swweak_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Swweak_Read(_, __),
                    name: "Swweak")
            .WithReservedBits(24, 3)
            .WithFlag(27, out pfmbyp_ovrpfmbypswdis_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Ovrpfmbypswdis_ValueProvider(_);
                        return pfmbyp_ovrpfmbypswdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Ovrpfmbypswdis_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Ovrpfmbypswdis_Read(_, __),
                    name: "Ovrpfmbypswdis")
            .WithFlag(28, out pfmbyp_ovrpfmbypclimdis_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Ovrpfmbypclimdis_ValueProvider(_);
                        return pfmbyp_ovrpfmbypclimdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Ovrpfmbypclimdis_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Ovrpfmbypclimdis_Read(_, __),
                    name: "Ovrpfmbypclimdis")
            .WithFlag(29, out pfmbyp_ovrpfmbyprstn_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Ovrpfmbyprstn_ValueProvider(_);
                        return pfmbyp_ovrpfmbyprstn_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Ovrpfmbyprstn_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Ovrpfmbyprstn_Read(_, __),
                    name: "Ovrpfmbyprstn")
            .WithFlag(30, out pfmbyp_ovrpfmbypclimsel_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Ovrpfmbypclimsel_ValueProvider(_);
                        return pfmbyp_ovrpfmbypclimsel_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Ovrpfmbypclimsel_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Ovrpfmbypclimsel_Read(_, __),
                    name: "Ovrpfmbypclimsel")
            .WithFlag(31, out pfmbyp_ovrpfmbypoverrideen_bit, 
                    valueProviderCallback: (_) => {
                        Pfmbyp_Ovrpfmbypoverrideen_ValueProvider(_);
                        return pfmbyp_ovrpfmbypoverrideen_bit.Value;               
                    },
                    writeCallback: (_, __) => Pfmbyp_Ovrpfmbypoverrideen_Write(_, __),
                    readCallback: (_, __) => Pfmbyp_Ovrpfmbypoverrideen_Read(_, __),
                    name: "Ovrpfmbypoverrideen")
            .WithReadCallback((_, __) => Pfmbyp_Read(_, __))
            .WithWriteCallback((_, __) => Pfmbyp_Write(_, __));
        
        // Vregvddcmpctrl - Offset : 0x3C
        protected DoubleWordRegister  GenerateVregvddcmpctrlRegister() => new DoubleWordRegister(this, 0x6)
            .WithFlag(0, out vregvddcmpctrl_vregincmpen_bit, 
                    valueProviderCallback: (_) => {
                        Vregvddcmpctrl_Vregincmpen_ValueProvider(_);
                        return vregvddcmpctrl_vregincmpen_bit.Value;               
                    },
                    writeCallback: (_, __) => Vregvddcmpctrl_Vregincmpen_Write(_, __),
                    readCallback: (_, __) => Vregvddcmpctrl_Vregincmpen_Read(_, __),
                    name: "Vregincmpen")
            .WithValueField(1, 2, out vregvddcmpctrl_thressel_field, 
                    valueProviderCallback: (_) => {
                        Vregvddcmpctrl_Thressel_ValueProvider(_);
                        return vregvddcmpctrl_thressel_field.Value;               
                    },
                    writeCallback: (_, __) => Vregvddcmpctrl_Thressel_Write(_, __),
                    readCallback: (_, __) => Vregvddcmpctrl_Thressel_Read(_, __),
                    name: "Thressel")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Vregvddcmpctrl_Read(_, __))
            .WithWriteCallback((_, __) => Vregvddcmpctrl_Write(_, __));
        
        // Pd1paretctrl - Offset : 0x40
        protected DoubleWordRegister  GeneratePd1paretctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, PD1PARETCTRL_PD1PARETDIS>(0, 16, out pd1paretctrl_pd1paretdis_field, 
                    valueProviderCallback: (_) => {
                        Pd1paretctrl_Pd1paretdis_ValueProvider(_);
                        return pd1paretctrl_pd1paretdis_field.Value;               
                    },
                    writeCallback: (_, __) => Pd1paretctrl_Pd1paretdis_Write(_, __),
                    readCallback: (_, __) => Pd1paretctrl_Pd1paretdis_Read(_, __),
                    name: "Pd1paretdis")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Pd1paretctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pd1paretctrl_Write(_, __));
        
        // Pd0pwrctrl - Offset : 0x44
        protected DoubleWordRegister  GeneratePd0pwrctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, PD0PWRCTRL_PD0PWRDN>(0, 4, out pd0pwrctrl_pd0pwrdn_field, 
                    valueProviderCallback: (_) => {
                        Pd0pwrctrl_Pd0pwrdn_ValueProvider(_);
                        return pd0pwrctrl_pd0pwrdn_field.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrctrl_Pd0pwrdn_Write(_, __),
                    readCallback: (_, __) => Pd0pwrctrl_Pd0pwrdn_Read(_, __),
                    name: "Pd0pwrdn")
            .WithReservedBits(4, 4)
            .WithEnumField<DoubleWordRegister, PD0PWRCTRL_EM2PDPWRDN>(8, 4, out pd0pwrctrl_em2pdpwrdn_field, 
                    valueProviderCallback: (_) => {
                        Pd0pwrctrl_Em2pdpwrdn_ValueProvider(_);
                        return pd0pwrctrl_em2pdpwrdn_field.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrctrl_Em2pdpwrdn_Write(_, __),
                    readCallback: (_, __) => Pd0pwrctrl_Em2pdpwrdn_Read(_, __),
                    name: "Em2pdpwrdn")
            .WithReservedBits(12, 12)
            .WithEnumField<DoubleWordRegister, PD0PWRCTRL_DISCHRGPD>(24, 5, out pd0pwrctrl_dischrgpd_field, 
                    valueProviderCallback: (_) => {
                        Pd0pwrctrl_Dischrgpd_ValueProvider(_);
                        return pd0pwrctrl_dischrgpd_field.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrctrl_Dischrgpd_Write(_, __),
                    readCallback: (_, __) => Pd0pwrctrl_Dischrgpd_Read(_, __),
                    name: "Dischrgpd")
            .WithReservedBits(29, 2)
            .WithFlag(31, out pd0pwrctrl_dischrgpdem2en_bit, 
                    valueProviderCallback: (_) => {
                        Pd0pwrctrl_Dischrgpdem2en_ValueProvider(_);
                        return pd0pwrctrl_dischrgpdem2en_bit.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrctrl_Dischrgpdem2en_Write(_, __),
                    readCallback: (_, __) => Pd0pwrctrl_Dischrgpdem2en_Read(_, __),
                    name: "Dischrgpdem2en")
            .WithReadCallback((_, __) => Pd0pwrctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pd0pwrctrl_Write(_, __));
        
        // Pd0pwrem2ctrl - Offset : 0x48
        protected DoubleWordRegister  GeneratePd0pwrem2ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out pd0pwrem2ctrl_em2pd0ben_bit, 
                    valueProviderCallback: (_) => {
                        Pd0pwrem2ctrl_Em2pd0ben_ValueProvider(_);
                        return pd0pwrem2ctrl_em2pd0ben_bit.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0ben_Write(_, __),
                    readCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0ben_Read(_, __),
                    name: "Em2pd0ben")
            .WithFlag(1, out pd0pwrem2ctrl_em2pd0cen_bit, 
                    valueProviderCallback: (_) => {
                        Pd0pwrem2ctrl_Em2pd0cen_ValueProvider(_);
                        return pd0pwrem2ctrl_em2pd0cen_bit.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0cen_Write(_, __),
                    readCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0cen_Read(_, __),
                    name: "Em2pd0cen")
            .WithFlag(2, out pd0pwrem2ctrl_em2pd0den_bit, 
                    valueProviderCallback: (_) => {
                        Pd0pwrem2ctrl_Em2pd0den_ValueProvider(_);
                        return pd0pwrem2ctrl_em2pd0den_bit.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0den_Write(_, __),
                    readCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0den_Read(_, __),
                    name: "Em2pd0den")
            .WithFlag(3, out pd0pwrem2ctrl_em2pd0een_bit, 
                    valueProviderCallback: (_) => {
                        Pd0pwrem2ctrl_Em2pd0een_ValueProvider(_);
                        return pd0pwrem2ctrl_em2pd0een_bit.Value;               
                    },
                    writeCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0een_Write(_, __),
                    readCallback: (_, __) => Pd0pwrem2ctrl_Em2pd0een_Read(_, __),
                    name: "Em2pd0een")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Pd0pwrem2ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pd0pwrem2ctrl_Write(_, __));
        
        // Ipversion_Rev - Offset : 0x5C
        protected DoubleWordRegister  GenerateIpversion_revRegister() => new DoubleWordRegister(this, 0x3)
            .WithValueField(0, 32, out ipversion_rev_ipversion_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ipversion_Rev_Ipversion_ValueProvider(_);
                        return ipversion_rev_ipversion_field.Value;               
                    },
                    readCallback: (_, __) => Ipversion_Rev_Ipversion_Read(_, __),
                    name: "Ipversion")
            .WithReadCallback((_, __) => Ipversion_Rev_Read(_, __))
            .WithWriteCallback((_, __) => Ipversion_Rev_Write(_, __));
        
        // Lock - Offset : 0x60
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0xADE8)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // If - Offset : 0x64
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 16)
            .WithFlag(16, out if_avddbod_bit, 
                    valueProviderCallback: (_) => {
                        If_Avddbod_ValueProvider(_);
                        return if_avddbod_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Avddbod_Write(_, __),
                    readCallback: (_, __) => If_Avddbod_Read(_, __),
                    name: "Avddbod")
            .WithFlag(17, out if_iovdd0bod_bit, 
                    valueProviderCallback: (_) => {
                        If_Iovdd0bod_ValueProvider(_);
                        return if_iovdd0bod_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Iovdd0bod_Write(_, __),
                    readCallback: (_, __) => If_Iovdd0bod_Read(_, __),
                    name: "Iovdd0bod")
            .WithFlag(18, out if_iovdd1bod_bit, 
                    valueProviderCallback: (_) => {
                        If_Iovdd1bod_ValueProvider(_);
                        return if_iovdd1bod_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Iovdd1bod_Write(_, __),
                    readCallback: (_, __) => If_Iovdd1bod_Read(_, __),
                    name: "Iovdd1bod")
            .WithReservedBits(19, 5)
            .WithFlag(24, out if_em23wakeup_bit, 
                    valueProviderCallback: (_) => {
                        If_Em23wakeup_ValueProvider(_);
                        return if_em23wakeup_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Em23wakeup_Write(_, __),
                    readCallback: (_, __) => If_Em23wakeup_Read(_, __),
                    name: "Em23wakeup")
            .WithFlag(25, out if_vscaledone_bit, 
                    valueProviderCallback: (_) => {
                        If_Vscaledone_ValueProvider(_);
                        return if_vscaledone_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Vscaledone_Write(_, __),
                    readCallback: (_, __) => If_Vscaledone_Read(_, __),
                    name: "Vscaledone")
            .WithReservedBits(26, 1)
            .WithFlag(27, out if_tempavg_bit, 
                    valueProviderCallback: (_) => {
                        If_Tempavg_ValueProvider(_);
                        return if_tempavg_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Tempavg_Write(_, __),
                    readCallback: (_, __) => If_Tempavg_Read(_, __),
                    name: "Tempavg")
            .WithReservedBits(28, 1)
            .WithFlag(29, out if_temp_bit, 
                    valueProviderCallback: (_) => {
                        If_Temp_ValueProvider(_);
                        return if_temp_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Temp_Write(_, __),
                    readCallback: (_, __) => If_Temp_Read(_, __),
                    name: "Temp")
            .WithFlag(30, out if_templow_bit, 
                    valueProviderCallback: (_) => {
                        If_Templow_ValueProvider(_);
                        return if_templow_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Templow_Write(_, __),
                    readCallback: (_, __) => If_Templow_Read(_, __),
                    name: "Templow")
            .WithFlag(31, out if_temphigh_bit, 
                    valueProviderCallback: (_) => {
                        If_Temphigh_ValueProvider(_);
                        return if_temphigh_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Temphigh_Write(_, __),
                    readCallback: (_, __) => If_Temphigh_Read(_, __),
                    name: "Temphigh")
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x68
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 16)
            .WithFlag(16, out ien_avddbod_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Avddbod_ValueProvider(_);
                        return ien_avddbod_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Avddbod_Write(_, __),
                    readCallback: (_, __) => Ien_Avddbod_Read(_, __),
                    name: "Avddbod")
            .WithFlag(17, out ien_iovdd0bod_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Iovdd0bod_ValueProvider(_);
                        return ien_iovdd0bod_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Iovdd0bod_Write(_, __),
                    readCallback: (_, __) => Ien_Iovdd0bod_Read(_, __),
                    name: "Iovdd0bod")
            .WithFlag(18, out ien_iovdd1bod_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Iovdd1bod_ValueProvider(_);
                        return ien_iovdd1bod_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Iovdd1bod_Write(_, __),
                    readCallback: (_, __) => Ien_Iovdd1bod_Read(_, __),
                    name: "Iovdd1bod")
            .WithReservedBits(19, 5)
            .WithFlag(24, out ien_em23wakeup_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Em23wakeup_ValueProvider(_);
                        return ien_em23wakeup_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Em23wakeup_Write(_, __),
                    readCallback: (_, __) => Ien_Em23wakeup_Read(_, __),
                    name: "Em23wakeup")
            .WithFlag(25, out ien_vscaledone_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Vscaledone_ValueProvider(_);
                        return ien_vscaledone_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Vscaledone_Write(_, __),
                    readCallback: (_, __) => Ien_Vscaledone_Read(_, __),
                    name: "Vscaledone")
            .WithReservedBits(26, 1)
            .WithFlag(27, out ien_tempavg_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Tempavg_ValueProvider(_);
                        return ien_tempavg_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Tempavg_Write(_, __),
                    readCallback: (_, __) => Ien_Tempavg_Read(_, __),
                    name: "Tempavg")
            .WithReservedBits(28, 1)
            .WithFlag(29, out ien_temp_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Temp_ValueProvider(_);
                        return ien_temp_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Temp_Write(_, __),
                    readCallback: (_, __) => Ien_Temp_Read(_, __),
                    name: "Temp")
            .WithFlag(30, out ien_templow_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Templow_ValueProvider(_);
                        return ien_templow_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Templow_Write(_, __),
                    readCallback: (_, __) => Ien_Templow_Read(_, __),
                    name: "Templow")
            .WithFlag(31, out ien_temphigh_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Temphigh_ValueProvider(_);
                        return ien_temphigh_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Temphigh_Write(_, __),
                    readCallback: (_, __) => Ien_Temphigh_Read(_, __),
                    name: "Temphigh")
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Em4ctrl - Offset : 0x6C
        protected DoubleWordRegister  GenerateEm4ctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 2, out em4ctrl_em4entry_field, 
                    valueProviderCallback: (_) => {
                        Em4ctrl_Em4entry_ValueProvider(_);
                        return em4ctrl_em4entry_field.Value;               
                    },
                    writeCallback: (_, __) => Em4ctrl_Em4entry_Write(_, __),
                    readCallback: (_, __) => Em4ctrl_Em4entry_Read(_, __),
                    name: "Em4entry")
            .WithReservedBits(2, 2)
            .WithEnumField<DoubleWordRegister, EM4CTRL_EM4IORETMODE>(4, 2, out em4ctrl_em4ioretmode_field, 
                    valueProviderCallback: (_) => {
                        Em4ctrl_Em4ioretmode_ValueProvider(_);
                        return em4ctrl_em4ioretmode_field.Value;               
                    },
                    writeCallback: (_, __) => Em4ctrl_Em4ioretmode_Write(_, __),
                    readCallback: (_, __) => Em4ctrl_Em4ioretmode_Read(_, __),
                    name: "Em4ioretmode")
            .WithReservedBits(6, 2)
            .WithFlag(8, out em4ctrl_bod3senseem4wu_bit, 
                    valueProviderCallback: (_) => {
                        Em4ctrl_Bod3senseem4wu_ValueProvider(_);
                        return em4ctrl_bod3senseem4wu_bit.Value;               
                    },
                    writeCallback: (_, __) => Em4ctrl_Bod3senseem4wu_Write(_, __),
                    readCallback: (_, __) => Em4ctrl_Bod3senseem4wu_Read(_, __),
                    name: "Bod3senseem4wu")
            .WithReservedBits(9, 23)
            .WithReadCallback((_, __) => Em4ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Em4ctrl_Write(_, __));
        
        // Cmd - Offset : 0x70
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out cmd_em4unlatch_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Em4unlatch_Write(_, __),
                    name: "Em4unlatch")
            .WithReservedBits(2, 2)
            .WithFlag(4, out cmd_tempavgreq_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Tempavgreq_Write(_, __),
                    name: "Tempavgreq")
            .WithReservedBits(5, 4)
            .WithFlag(9, out cmd_em01vscale0_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Em01vscale0_Write(_, __),
                    name: "Em01vscale0")
            .WithFlag(10, out cmd_em01vscale1_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Em01vscale1_Write(_, __),
                    name: "Em01vscale1")
            .WithFlag(11, out cmd_em01vscale2_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Em01vscale2_Write(_, __),
                    name: "Em01vscale2")
            .WithReservedBits(12, 5)
            .WithFlag(17, out cmd_rstcauseclr_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Rstcauseclr_Write(_, __),
                    name: "Rstcauseclr")
            .WithFlag(18, out cmd_tamperrcclr_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Tamperrcclr_Write(_, __),
                    name: "Tamperrcclr")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write(_, __));
        
        // Ctrl - Offset : 0x74
        protected DoubleWordRegister  GenerateCtrlRegister() => new DoubleWordRegister(this, 0x200)
            .WithFlag(0, out ctrl_em2dbgen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Em2dbgen_ValueProvider(_);
                        return ctrl_em2dbgen_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Em2dbgen_Write(_, __),
                    readCallback: (_, __) => Ctrl_Em2dbgen_Read(_, __),
                    name: "Em2dbgen")
            .WithReservedBits(1, 2)
            .WithEnumField<DoubleWordRegister, CTRL_TEMPAVGNUM>(3, 1, out ctrl_tempavgnum_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Tempavgnum_ValueProvider(_);
                        return ctrl_tempavgnum_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Tempavgnum_Write(_, __),
                    readCallback: (_, __) => Ctrl_Tempavgnum_Read(_, __),
                    name: "Tempavgnum")
            .WithReservedBits(4, 4)
            .WithEnumField<DoubleWordRegister, CTRL_EM23VSCALE>(8, 2, out ctrl_em23vscale_field, 
                    valueProviderCallback: (_) => {
                        Ctrl_Em23vscale_ValueProvider(_);
                        return ctrl_em23vscale_field.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Em23vscale_Write(_, __),
                    readCallback: (_, __) => Ctrl_Em23vscale_Read(_, __),
                    name: "Em23vscale")
            .WithReservedBits(10, 6)
            .WithFlag(16, out ctrl_flashpwrupondemand_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Flashpwrupondemand_ValueProvider(_);
                        return ctrl_flashpwrupondemand_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Flashpwrupondemand_Write(_, __),
                    readCallback: (_, __) => Ctrl_Flashpwrupondemand_Read(_, __),
                    name: "Flashpwrupondemand")
            .WithReservedBits(17, 12)
            .WithFlag(29, out ctrl_efpdirectmodeen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Efpdirectmodeen_ValueProvider(_);
                        return ctrl_efpdirectmodeen_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Efpdirectmodeen_Write(_, __),
                    readCallback: (_, __) => Ctrl_Efpdirectmodeen_Read(_, __),
                    name: "Efpdirectmodeen")
            .WithFlag(30, out ctrl_efpdrvdecouple_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Efpdrvdecouple_ValueProvider(_);
                        return ctrl_efpdrvdecouple_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Efpdrvdecouple_Write(_, __),
                    readCallback: (_, __) => Ctrl_Efpdrvdecouple_Read(_, __),
                    name: "Efpdrvdecouple")
            .WithFlag(31, out ctrl_efpdrvdvdd_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Efpdrvdvdd_ValueProvider(_);
                        return ctrl_efpdrvdvdd_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Efpdrvdvdd_Write(_, __),
                    readCallback: (_, __) => Ctrl_Efpdrvdvdd_Read(_, __),
                    name: "Efpdrvdvdd")
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write(_, __));
        
        // Templimits - Offset : 0x78
        protected DoubleWordRegister  GenerateTemplimitsRegister() => new DoubleWordRegister(this, 0x1FF0000)
            .WithValueField(0, 9, out templimits_templow_field, 
                    valueProviderCallback: (_) => {
                        Templimits_Templow_ValueProvider(_);
                        return templimits_templow_field.Value;               
                    },
                    writeCallback: (_, __) => Templimits_Templow_Write(_, __),
                    readCallback: (_, __) => Templimits_Templow_Read(_, __),
                    name: "Templow")
            .WithReservedBits(9, 7)
            .WithValueField(16, 9, out templimits_temphigh_field, 
                    valueProviderCallback: (_) => {
                        Templimits_Temphigh_ValueProvider(_);
                        return templimits_temphigh_field.Value;               
                    },
                    writeCallback: (_, __) => Templimits_Temphigh_Write(_, __),
                    readCallback: (_, __) => Templimits_Temphigh_Read(_, __),
                    name: "Temphigh")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => Templimits_Read(_, __))
            .WithWriteCallback((_, __) => Templimits_Write(_, __));
        
        // Templimitsdg - Offset : 0x7C
        protected DoubleWordRegister  GenerateTemplimitsdgRegister() => new DoubleWordRegister(this, 0x1FF0000)
            .WithValueField(0, 9, out templimitsdg_templowdg_field, 
                    valueProviderCallback: (_) => {
                        Templimitsdg_Templowdg_ValueProvider(_);
                        return templimitsdg_templowdg_field.Value;               
                    },
                    writeCallback: (_, __) => Templimitsdg_Templowdg_Write(_, __),
                    readCallback: (_, __) => Templimitsdg_Templowdg_Read(_, __),
                    name: "Templowdg")
            .WithReservedBits(9, 7)
            .WithValueField(16, 9, out templimitsdg_temphidg_field, 
                    valueProviderCallback: (_) => {
                        Templimitsdg_Temphidg_ValueProvider(_);
                        return templimitsdg_temphidg_field.Value;               
                    },
                    writeCallback: (_, __) => Templimitsdg_Temphidg_Write(_, __),
                    readCallback: (_, __) => Templimitsdg_Temphidg_Read(_, __),
                    name: "Temphidg")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => Templimitsdg_Read(_, __))
            .WithWriteCallback((_, __) => Templimitsdg_Write(_, __));
        
        // Templimitsse - Offset : 0x80
        protected DoubleWordRegister  GenerateTemplimitsseRegister() => new DoubleWordRegister(this, 0x1FF0000)
            .WithValueField(0, 9, out templimitsse_templowse_field, 
                    valueProviderCallback: (_) => {
                        Templimitsse_Templowse_ValueProvider(_);
                        return templimitsse_templowse_field.Value;               
                    },
                    writeCallback: (_, __) => Templimitsse_Templowse_Write(_, __),
                    readCallback: (_, __) => Templimitsse_Templowse_Read(_, __),
                    name: "Templowse")
            .WithReservedBits(9, 7)
            .WithValueField(16, 9, out templimitsse_temphise_field, 
                    valueProviderCallback: (_) => {
                        Templimitsse_Temphise_ValueProvider(_);
                        return templimitsse_temphise_field.Value;               
                    },
                    writeCallback: (_, __) => Templimitsse_Temphise_Write(_, __),
                    readCallback: (_, __) => Templimitsse_Temphise_Read(_, __),
                    name: "Temphise")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => Templimitsse_Read(_, __))
            .WithWriteCallback((_, __) => Templimitsse_Write(_, __));
        
        // Status - Offset : 0x84
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x80)
            .WithEnumField<DoubleWordRegister, STATUS_LOCK>(0, 1, out status_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lock_ValueProvider(_);
                        return status_lock_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Lock_Read(_, __),
                    name: "Lock")
            .WithFlag(1, out status_firsttempdone_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Firsttempdone_ValueProvider(_);
                        return status_firsttempdone_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Firsttempdone_Read(_, __),
                    name: "Firsttempdone")
            .WithFlag(2, out status_tempactive_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Tempactive_ValueProvider(_);
                        return status_tempactive_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Tempactive_Read(_, __),
                    name: "Tempactive")
            .WithFlag(3, out status_tempavgactive_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Tempavgactive_ValueProvider(_);
                        return status_tempavgactive_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Tempavgactive_Read(_, __),
                    name: "Tempavgactive")
            .WithFlag(4, out status_vscalebusy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Vscalebusy_ValueProvider(_);
                        return status_vscalebusy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Vscalebusy_Read(_, __),
                    name: "Vscalebusy")
            .WithFlag(5, out status_vscalefailed_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Vscalefailed_ValueProvider(_);
                        return status_vscalefailed_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Vscalefailed_Read(_, __),
                    name: "Vscalefailed")
            .WithEnumField<DoubleWordRegister, STATUS_VSCALE>(6, 2, out status_vscale_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Vscale_ValueProvider(_);
                        return status_vscale_field.Value;               
                    },
                    readCallback: (_, __) => Status_Vscale_Read(_, __),
                    name: "Vscale")
            .WithFlag(8, out status_racactive_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Racactive_ValueProvider(_);
                        return status_racactive_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Racactive_Read(_, __),
                    name: "Racactive")
            .WithFlag(9, out status_em4ioret_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Em4ioret_ValueProvider(_);
                        return status_em4ioret_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Em4ioret_Read(_, __),
                    name: "Em4ioret")
            .WithFlag(10, out status_em2entered_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Em2entered_ValueProvider(_);
                        return status_em2entered_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Em2entered_Read(_, __),
                    name: "Em2entered")
            .WithFlag(11, out status_em1p_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Em1p_ValueProvider(_);
                        return status_em1p_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Em1p_Read(_, __),
                    name: "Em1p")
            .WithReservedBits(12, 1)
            .WithValueField(13, 19, out status_pwrdwnstatus_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Pwrdwnstatus_ValueProvider(_);
                        return status_pwrdwnstatus_field.Value;               
                    },
                    readCallback: (_, __) => Status_Pwrdwnstatus_Read(_, __),
                    name: "Pwrdwnstatus")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Temp - Offset : 0x88
        protected DoubleWordRegister  GenerateTempRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 2, out temp_templsb_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Temp_Templsb_ValueProvider(_);
                        return temp_templsb_field.Value;               
                    },
                    readCallback: (_, __) => Temp_Templsb_Read(_, __),
                    name: "Templsb")
            .WithValueField(2, 9, out temp_temp_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Temp_Temp_ValueProvider(_);
                        return temp_temp_field.Value;               
                    },
                    readCallback: (_, __) => Temp_Temp_Read(_, __),
                    name: "Temp")
            .WithReservedBits(11, 5)
            .WithValueField(16, 11, out temp_tempavg_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Temp_Tempavg_ValueProvider(_);
                        return temp_tempavg_field.Value;               
                    },
                    readCallback: (_, __) => Temp_Tempavg_Read(_, __),
                    name: "Tempavg")
            .WithReservedBits(27, 5)
            .WithReadCallback((_, __) => Temp_Read(_, __))
            .WithWriteCallback((_, __) => Temp_Write(_, __));
        
        // Testctrl - Offset : 0x8C
        protected DoubleWordRegister  GenerateTestctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 12)
            .WithFlag(12, out testctrl_keepradioinem0_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Keepradioinem0_ValueProvider(_);
                        return testctrl_keepradioinem0_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Keepradioinem0_Write(_, __),
                    readCallback: (_, __) => Testctrl_Keepradioinem0_Read(_, __),
                    name: "Keepradioinem0")
            .WithFlag(13, out testctrl_bodmask_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Bodmask_ValueProvider(_);
                        return testctrl_bodmask_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Bodmask_Write(_, __),
                    readCallback: (_, __) => Testctrl_Bodmask_Read(_, __),
                    name: "Bodmask")
            .WithFlag(14, out testctrl_regdis_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Regdis_ValueProvider(_);
                        return testctrl_regdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Regdis_Write(_, __),
                    readCallback: (_, __) => Testctrl_Regdis_Read(_, __),
                    name: "Regdis")
            .WithFlag(15, out testctrl_emuoschven_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Emuoschven_ValueProvider(_);
                        return testctrl_emuoschven_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Emuoschven_Write(_, __),
                    readCallback: (_, __) => Testctrl_Emuoschven_Read(_, __),
                    name: "Emuoschven")
            .WithFlag(16, out testctrl_emuosclven_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Emuosclven_ValueProvider(_);
                        return testctrl_emuosclven_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Emuosclven_Write(_, __),
                    readCallback: (_, __) => Testctrl_Emuosclven_Read(_, __),
                    name: "Emuosclven")
            .WithFlag(17, out testctrl_em2entrytimeouten_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Em2entrytimeouten_ValueProvider(_);
                        return testctrl_em2entrytimeouten_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Em2entrytimeouten_Write(_, __),
                    readCallback: (_, __) => Testctrl_Em2entrytimeouten_Read(_, __),
                    name: "Em2entrytimeouten")
            .WithFlag(18, out testctrl_hvtrimdone_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Hvtrimdone_ValueProvider(_);
                        return testctrl_hvtrimdone_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Hvtrimdone_Write(_, __),
                    readCallback: (_, __) => Testctrl_Hvtrimdone_Read(_, __),
                    name: "Hvtrimdone")
            .WithReservedBits(19, 1)
            .WithFlag(20, out testctrl_maskexportreset_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Maskexportreset_ValueProvider(_);
                        return testctrl_maskexportreset_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Maskexportreset_Write(_, __),
                    readCallback: (_, __) => Testctrl_Maskexportreset_Read(_, __),
                    name: "Maskexportreset")
            .WithFlag(21, out testctrl_forceexportreset_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Forceexportreset_ValueProvider(_);
                        return testctrl_forceexportreset_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Forceexportreset_Write(_, __),
                    readCallback: (_, __) => Testctrl_Forceexportreset_Read(_, __),
                    name: "Forceexportreset")
            .WithReservedBits(22, 1)
            .WithFlag(23, out testctrl_flashpwrswovr_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashpwrswovr_ValueProvider(_);
                        return testctrl_flashpwrswovr_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashpwrswovr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashpwrswovr_Read(_, __),
                    name: "Flashpwrswovr")
            .WithFlag(24, out testctrl_flashpwrsoftswovr_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashpwrsoftswovr_ValueProvider(_);
                        return testctrl_flashpwrsoftswovr_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashpwrsoftswovr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashpwrsoftswovr_Read(_, __),
                    name: "Flashpwrsoftswovr")
            .WithValueField(25, 4, out testctrl_prslvcfg_field, 
                    valueProviderCallback: (_) => {
                        Testctrl_Prslvcfg_ValueProvider(_);
                        return testctrl_prslvcfg_field.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Prslvcfg_Write(_, __),
                    readCallback: (_, __) => Testctrl_Prslvcfg_Read(_, __),
                    name: "Prslvcfg")
            .WithValueField(29, 3, out testctrl_prshvcfg_field, 
                    valueProviderCallback: (_) => {
                        Testctrl_Prshvcfg_ValueProvider(_);
                        return testctrl_prshvcfg_field.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Prshvcfg_Write(_, __),
                    readCallback: (_, __) => Testctrl_Prshvcfg_Read(_, __),
                    name: "Prshvcfg")
            .WithReadCallback((_, __) => Testctrl_Read(_, __))
            .WithWriteCallback((_, __) => Testctrl_Write(_, __));
        
        // Rstctrl - Offset : 0x90
        protected DoubleWordRegister  GenerateRstctrlRegister() => new DoubleWordRegister(this, 0x60407)
            .WithEnumField<DoubleWordRegister, RSTCTRL_WDOG0RMODE>(0, 1, out rstctrl_wdog0rmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Wdog0rmode_ValueProvider(_);
                        return rstctrl_wdog0rmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Wdog0rmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Wdog0rmode_Read(_, __),
                    name: "Wdog0rmode")
            .WithEnumField<DoubleWordRegister, RSTCTRL_WDOG1RMODE>(1, 1, out rstctrl_wdog1rmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Wdog1rmode_ValueProvider(_);
                        return rstctrl_wdog1rmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Wdog1rmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Wdog1rmode_Read(_, __),
                    name: "Wdog1rmode")
            .WithEnumField<DoubleWordRegister, RSTCTRL_SYSRMODE>(2, 1, out rstctrl_sysrmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Sysrmode_ValueProvider(_);
                        return rstctrl_sysrmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Sysrmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Sysrmode_Read(_, __),
                    name: "Sysrmode")
            .WithEnumField<DoubleWordRegister, RSTCTRL_LOCKUPRMODE>(3, 1, out rstctrl_lockuprmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Lockuprmode_ValueProvider(_);
                        return rstctrl_lockuprmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Lockuprmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Lockuprmode_Read(_, __),
                    name: "Lockuprmode")
            .WithReservedBits(4, 2)
            .WithEnumField<DoubleWordRegister, RSTCTRL_AVDDBODRMODE>(6, 1, out rstctrl_avddbodrmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Avddbodrmode_ValueProvider(_);
                        return rstctrl_avddbodrmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Avddbodrmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Avddbodrmode_Read(_, __),
                    name: "Avddbodrmode")
            .WithEnumField<DoubleWordRegister, RSTCTRL_IOVDD0BODRMODE>(7, 1, out rstctrl_iovdd0bodrmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Iovdd0bodrmode_ValueProvider(_);
                        return rstctrl_iovdd0bodrmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Iovdd0bodrmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Iovdd0bodrmode_Read(_, __),
                    name: "Iovdd0bodrmode")
            .WithEnumField<DoubleWordRegister, RSTCTRL_IOVDD1BODRMODE>(8, 1, out rstctrl_iovdd1bodrmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Iovdd1bodrmode_ValueProvider(_);
                        return rstctrl_iovdd1bodrmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Iovdd1bodrmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Iovdd1bodrmode_Read(_, __),
                    name: "Iovdd1bodrmode")
            .WithReservedBits(9, 1)
            .WithEnumField<DoubleWordRegister, RSTCTRL_DECBODRMODE>(10, 1, out rstctrl_decbodrmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Decbodrmode_ValueProvider(_);
                        return rstctrl_decbodrmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Decbodrmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Decbodrmode_Read(_, __),
                    name: "Decbodrmode")
            .WithReservedBits(11, 3)
            .WithEnumField<DoubleWordRegister, RSTCTRL_M0SYSRMODE>(14, 1, out rstctrl_m0sysrmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_M0sysrmode_ValueProvider(_);
                        return rstctrl_m0sysrmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_M0sysrmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_M0sysrmode_Read(_, __),
                    name: "M0sysrmode")
            .WithEnumField<DoubleWordRegister, RSTCTRL_M0LOCKUPRMODE>(15, 1, out rstctrl_m0lockuprmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_M0lockuprmode_ValueProvider(_);
                        return rstctrl_m0lockuprmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_M0lockuprmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_M0lockuprmode_Read(_, __),
                    name: "M0lockuprmode")
            .WithReservedBits(16, 1)
            .WithEnumField<DoubleWordRegister, RSTCTRL_SEM0SYSRMODE>(17, 1, out rstctrl_sem0sysrmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Sem0sysrmode_ValueProvider(_);
                        return rstctrl_sem0sysrmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Sem0sysrmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Sem0sysrmode_Read(_, __),
                    name: "Sem0sysrmode")
            .WithEnumField<DoubleWordRegister, RSTCTRL_SEM0LOCKUPRMODE>(18, 1, out rstctrl_sem0lockuprmode_bit, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Sem0lockuprmode_ValueProvider(_);
                        return rstctrl_sem0lockuprmode_bit.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Sem0lockuprmode_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Sem0lockuprmode_Read(_, __),
                    name: "Sem0lockuprmode")
            .WithReservedBits(19, 11)
            .WithEnumField<DoubleWordRegister, RSTCTRL_PRSSYSRSTDLY>(30, 2, out rstctrl_prssysrstdly_field, 
                    valueProviderCallback: (_) => {
                        Rstctrl_Prssysrstdly_ValueProvider(_);
                        return rstctrl_prssysrstdly_field.Value;               
                    },
                    writeCallback: (_, __) => Rstctrl_Prssysrstdly_Write(_, __),
                    readCallback: (_, __) => Rstctrl_Prssysrstdly_Read(_, __),
                    name: "Prssysrstdly")
            .WithReadCallback((_, __) => Rstctrl_Read(_, __))
            .WithWriteCallback((_, __) => Rstctrl_Write(_, __));
        
        // Rstcause - Offset : 0x94
        protected DoubleWordRegister  GenerateRstcauseRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rstcause_por_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Por_ValueProvider(_);
                        return rstcause_por_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Por_Read(_, __),
                    name: "Por")
            .WithFlag(1, out rstcause_pin_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Pin_ValueProvider(_);
                        return rstcause_pin_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Pin_Read(_, __),
                    name: "Pin")
            .WithFlag(2, out rstcause_em4_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Em4_ValueProvider(_);
                        return rstcause_em4_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Em4_Read(_, __),
                    name: "Em4")
            .WithFlag(3, out rstcause_wdog0_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Wdog0_ValueProvider(_);
                        return rstcause_wdog0_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Wdog0_Read(_, __),
                    name: "Wdog0")
            .WithFlag(4, out rstcause_wdog1_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Wdog1_ValueProvider(_);
                        return rstcause_wdog1_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Wdog1_Read(_, __),
                    name: "Wdog1")
            .WithFlag(5, out rstcause_lockup_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Lockup_ValueProvider(_);
                        return rstcause_lockup_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Lockup_Read(_, __),
                    name: "Lockup")
            .WithFlag(6, out rstcause_sysreq_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Sysreq_ValueProvider(_);
                        return rstcause_sysreq_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Sysreq_Read(_, __),
                    name: "Sysreq")
            .WithFlag(7, out rstcause_dvddbod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Dvddbod_ValueProvider(_);
                        return rstcause_dvddbod_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Dvddbod_Read(_, __),
                    name: "Dvddbod")
            .WithFlag(8, out rstcause_dvddlebod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Dvddlebod_ValueProvider(_);
                        return rstcause_dvddlebod_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Dvddlebod_Read(_, __),
                    name: "Dvddlebod")
            .WithFlag(9, out rstcause_decbod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Decbod_ValueProvider(_);
                        return rstcause_decbod_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Decbod_Read(_, __),
                    name: "Decbod")
            .WithFlag(10, out rstcause_avddbod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Avddbod_ValueProvider(_);
                        return rstcause_avddbod_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Avddbod_Read(_, __),
                    name: "Avddbod")
            .WithFlag(11, out rstcause_iovdd0bod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Iovdd0bod_ValueProvider(_);
                        return rstcause_iovdd0bod_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Iovdd0bod_Read(_, __),
                    name: "Iovdd0bod")
            .WithFlag(12, out rstcause_iovdd1bod_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Iovdd1bod_ValueProvider(_);
                        return rstcause_iovdd1bod_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Iovdd1bod_Read(_, __),
                    name: "Iovdd1bod")
            .WithFlag(13, out rstcause_tamper_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Tamper_ValueProvider(_);
                        return rstcause_tamper_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Tamper_Read(_, __),
                    name: "Tamper")
            .WithFlag(14, out rstcause_m0sysreq_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_M0sysreq_ValueProvider(_);
                        return rstcause_m0sysreq_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_M0sysreq_Read(_, __),
                    name: "M0sysreq")
            .WithFlag(15, out rstcause_m0lockup_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_M0lockup_ValueProvider(_);
                        return rstcause_m0lockup_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_M0lockup_Read(_, __),
                    name: "M0lockup")
            .WithReservedBits(16, 1)
            .WithFlag(17, out rstcause_sem0sysreq_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Sem0sysreq_ValueProvider(_);
                        return rstcause_sem0sysreq_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Sem0sysreq_Read(_, __),
                    name: "Sem0sysreq")
            .WithFlag(18, out rstcause_sem0lockup_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Sem0lockup_ValueProvider(_);
                        return rstcause_sem0lockup_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Sem0lockup_Read(_, __),
                    name: "Sem0lockup")
            .WithReservedBits(19, 12)
            .WithFlag(31, out rstcause_vregin_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Rstcause_Vregin_ValueProvider(_);
                        return rstcause_vregin_bit.Value;               
                    },
                    readCallback: (_, __) => Rstcause_Vregin_Read(_, __),
                    name: "Vregin")
            .WithReadCallback((_, __) => Rstcause_Read(_, __))
            .WithWriteCallback((_, __) => Rstcause_Write(_, __));
        
        // Tamperrstcause - Offset : 0x98
        protected DoubleWordRegister  GenerateTamperrstcauseRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out tamperrstcause_tamperrst_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Tamperrstcause_Tamperrst_ValueProvider(_);
                        return tamperrstcause_tamperrst_field.Value;               
                    },
                    readCallback: (_, __) => Tamperrstcause_Tamperrst_Read(_, __),
                    name: "Tamperrst")
            .WithReadCallback((_, __) => Tamperrstcause_Read(_, __))
            .WithWriteCallback((_, __) => Tamperrstcause_Write(_, __));
        
        // Dgif - Offset : 0xA0
        protected DoubleWordRegister  GenerateDgifRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 24)
            .WithFlag(24, out dgif_em23wakeupdgif_bit, 
                    valueProviderCallback: (_) => {
                        Dgif_Em23wakeupdgif_ValueProvider(_);
                        return dgif_em23wakeupdgif_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgif_Em23wakeupdgif_Write(_, __),
                    readCallback: (_, __) => Dgif_Em23wakeupdgif_Read(_, __),
                    name: "Em23wakeupdgif")
            .WithReservedBits(25, 4)
            .WithFlag(29, out dgif_tempdgif_bit, 
                    valueProviderCallback: (_) => {
                        Dgif_Tempdgif_ValueProvider(_);
                        return dgif_tempdgif_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgif_Tempdgif_Write(_, __),
                    readCallback: (_, __) => Dgif_Tempdgif_Read(_, __),
                    name: "Tempdgif")
            .WithFlag(30, out dgif_templowdgif_bit, 
                    valueProviderCallback: (_) => {
                        Dgif_Templowdgif_ValueProvider(_);
                        return dgif_templowdgif_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgif_Templowdgif_Write(_, __),
                    readCallback: (_, __) => Dgif_Templowdgif_Read(_, __),
                    name: "Templowdgif")
            .WithFlag(31, out dgif_temphighdgif_bit, 
                    valueProviderCallback: (_) => {
                        Dgif_Temphighdgif_ValueProvider(_);
                        return dgif_temphighdgif_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgif_Temphighdgif_Write(_, __),
                    readCallback: (_, __) => Dgif_Temphighdgif_Read(_, __),
                    name: "Temphighdgif")
            .WithReadCallback((_, __) => Dgif_Read(_, __))
            .WithWriteCallback((_, __) => Dgif_Write(_, __));
        
        // Dgien - Offset : 0xA4
        protected DoubleWordRegister  GenerateDgienRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 24)
            .WithFlag(24, out dgien_em23wakeupdgien_bit, 
                    valueProviderCallback: (_) => {
                        Dgien_Em23wakeupdgien_ValueProvider(_);
                        return dgien_em23wakeupdgien_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgien_Em23wakeupdgien_Write(_, __),
                    readCallback: (_, __) => Dgien_Em23wakeupdgien_Read(_, __),
                    name: "Em23wakeupdgien")
            .WithReservedBits(25, 4)
            .WithFlag(29, out dgien_tempdgien_bit, 
                    valueProviderCallback: (_) => {
                        Dgien_Tempdgien_ValueProvider(_);
                        return dgien_tempdgien_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgien_Tempdgien_Write(_, __),
                    readCallback: (_, __) => Dgien_Tempdgien_Read(_, __),
                    name: "Tempdgien")
            .WithFlag(30, out dgien_templowdgien_bit, 
                    valueProviderCallback: (_) => {
                        Dgien_Templowdgien_ValueProvider(_);
                        return dgien_templowdgien_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgien_Templowdgien_Write(_, __),
                    readCallback: (_, __) => Dgien_Templowdgien_Read(_, __),
                    name: "Templowdgien")
            .WithFlag(31, out dgien_temphighdgien_bit, 
                    valueProviderCallback: (_) => {
                        Dgien_Temphighdgien_ValueProvider(_);
                        return dgien_temphighdgien_bit.Value;               
                    },
                    writeCallback: (_, __) => Dgien_Temphighdgien_Write(_, __),
                    readCallback: (_, __) => Dgien_Temphighdgien_Read(_, __),
                    name: "Temphighdgien")
            .WithReadCallback((_, __) => Dgien_Read(_, __))
            .WithWriteCallback((_, __) => Dgien_Write(_, __));
        
        // Seqif - Offset : 0xA8
        protected DoubleWordRegister  GenerateSeqifRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 29)
            .WithFlag(29, out seqif_temp_bit, 
                    valueProviderCallback: (_) => {
                        Seqif_Temp_ValueProvider(_);
                        return seqif_temp_bit.Value;               
                    },
                    writeCallback: (_, __) => Seqif_Temp_Write(_, __),
                    readCallback: (_, __) => Seqif_Temp_Read(_, __),
                    name: "Temp")
            .WithFlag(30, out seqif_templow_bit, 
                    valueProviderCallback: (_) => {
                        Seqif_Templow_ValueProvider(_);
                        return seqif_templow_bit.Value;               
                    },
                    writeCallback: (_, __) => Seqif_Templow_Write(_, __),
                    readCallback: (_, __) => Seqif_Templow_Read(_, __),
                    name: "Templow")
            .WithFlag(31, out seqif_temphigh_bit, 
                    valueProviderCallback: (_) => {
                        Seqif_Temphigh_ValueProvider(_);
                        return seqif_temphigh_bit.Value;               
                    },
                    writeCallback: (_, __) => Seqif_Temphigh_Write(_, __),
                    readCallback: (_, __) => Seqif_Temphigh_Read(_, __),
                    name: "Temphigh")
            .WithReadCallback((_, __) => Seqif_Read(_, __))
            .WithWriteCallback((_, __) => Seqif_Write(_, __));
        
        // Seqien - Offset : 0xAC
        protected DoubleWordRegister  GenerateSeqienRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 29)
            .WithFlag(29, out seqien_temp_bit, 
                    valueProviderCallback: (_) => {
                        Seqien_Temp_ValueProvider(_);
                        return seqien_temp_bit.Value;               
                    },
                    writeCallback: (_, __) => Seqien_Temp_Write(_, __),
                    readCallback: (_, __) => Seqien_Temp_Read(_, __),
                    name: "Temp")
            .WithFlag(30, out seqien_templow_bit, 
                    valueProviderCallback: (_) => {
                        Seqien_Templow_ValueProvider(_);
                        return seqien_templow_bit.Value;               
                    },
                    writeCallback: (_, __) => Seqien_Templow_Write(_, __),
                    readCallback: (_, __) => Seqien_Templow_Read(_, __),
                    name: "Templow")
            .WithFlag(31, out seqien_temphigh_bit, 
                    valueProviderCallback: (_) => {
                        Seqien_Temphigh_ValueProvider(_);
                        return seqien_temphigh_bit.Value;               
                    },
                    writeCallback: (_, __) => Seqien_Temphigh_Write(_, __),
                    readCallback: (_, __) => Seqien_Temphigh_Read(_, __),
                    name: "Temphigh")
            .WithReadCallback((_, __) => Seqien_Read(_, __))
            .WithWriteCallback((_, __) => Seqien_Write(_, __));
        
        // Delaycfg - Offset : 0xB0
        protected DoubleWordRegister  GenerateDelaycfgRegister() => new DoubleWordRegister(this, 0x205)
            .WithValueField(0, 6, out delaycfg_vscalestepupwait_field, 
                    valueProviderCallback: (_) => {
                        Delaycfg_Vscalestepupwait_ValueProvider(_);
                        return delaycfg_vscalestepupwait_field.Value;               
                    },
                    writeCallback: (_, __) => Delaycfg_Vscalestepupwait_Write(_, __),
                    readCallback: (_, __) => Delaycfg_Vscalestepupwait_Read(_, __),
                    name: "Vscalestepupwait")
            .WithReservedBits(6, 2)
            .WithValueField(8, 3, out delaycfg_vscalestepdnwait_field, 
                    valueProviderCallback: (_) => {
                        Delaycfg_Vscalestepdnwait_ValueProvider(_);
                        return delaycfg_vscalestepdnwait_field.Value;               
                    },
                    writeCallback: (_, __) => Delaycfg_Vscalestepdnwait_Write(_, __),
                    readCallback: (_, __) => Delaycfg_Vscalestepdnwait_Read(_, __),
                    name: "Vscalestepdnwait")
            .WithReservedBits(11, 15)
            .WithValueField(26, 2, out delaycfg_retaindly_field, 
                    valueProviderCallback: (_) => {
                        Delaycfg_Retaindly_ValueProvider(_);
                        return delaycfg_retaindly_field.Value;               
                    },
                    writeCallback: (_, __) => Delaycfg_Retaindly_Write(_, __),
                    readCallback: (_, __) => Delaycfg_Retaindly_Read(_, __),
                    name: "Retaindly")
            .WithValueField(28, 2, out delaycfg_isodly_field, 
                    valueProviderCallback: (_) => {
                        Delaycfg_Isodly_ValueProvider(_);
                        return delaycfg_isodly_field.Value;               
                    },
                    writeCallback: (_, __) => Delaycfg_Isodly_Write(_, __),
                    readCallback: (_, __) => Delaycfg_Isodly_Read(_, __),
                    name: "Isodly")
            .WithReservedBits(30, 2)
            .WithReadCallback((_, __) => Delaycfg_Read(_, __))
            .WithWriteCallback((_, __) => Delaycfg_Write(_, __));
        
        // Testlock - Offset : 0xB4
        protected DoubleWordRegister  GenerateTestlockRegister() => new DoubleWordRegister(this, 0xADE8)
            .WithValueField(0, 16, out testlock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Testlock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Testlock_Read(_, __))
            .WithWriteCallback((_, __) => Testlock_Write(_, __));
        
        // Auxctrl - Offset : 0xB8
        protected DoubleWordRegister  GenerateAuxctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 8, out auxctrl_aux0_field, 
                    valueProviderCallback: (_) => {
                        Auxctrl_Aux0_ValueProvider(_);
                        return auxctrl_aux0_field.Value;               
                    },
                    writeCallback: (_, __) => Auxctrl_Aux0_Write(_, __),
                    readCallback: (_, __) => Auxctrl_Aux0_Read(_, __),
                    name: "Aux0")
            .WithValueField(8, 8, out auxctrl_aux1_field, 
                    valueProviderCallback: (_) => {
                        Auxctrl_Aux1_ValueProvider(_);
                        return auxctrl_aux1_field.Value;               
                    },
                    writeCallback: (_, __) => Auxctrl_Aux1_Write(_, __),
                    readCallback: (_, __) => Auxctrl_Aux1_Read(_, __),
                    name: "Aux1")
            .WithValueField(16, 8, out auxctrl_aux2_field, 
                    valueProviderCallback: (_) => {
                        Auxctrl_Aux2_ValueProvider(_);
                        return auxctrl_aux2_field.Value;               
                    },
                    writeCallback: (_, __) => Auxctrl_Aux2_Write(_, __),
                    readCallback: (_, __) => Auxctrl_Aux2_Read(_, __),
                    name: "Aux2")
            .WithValueField(24, 8, out auxctrl_aux3_field, 
                    valueProviderCallback: (_) => {
                        Auxctrl_Aux3_ValueProvider(_);
                        return auxctrl_aux3_field.Value;               
                    },
                    writeCallback: (_, __) => Auxctrl_Aux3_Write(_, __),
                    readCallback: (_, __) => Auxctrl_Aux3_Read(_, __),
                    name: "Aux3")
            .WithReadCallback((_, __) => Auxctrl_Read(_, __))
            .WithWriteCallback((_, __) => Auxctrl_Write(_, __));
        
        // Isbiasconf_Isbiasctrl - Offset : 0xC0
        protected DoubleWordRegister  GenerateIsbiasconf_isbiasctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out isbiasconf_isbiasctrl_isbiasctrlen_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Isbiasctrlen_ValueProvider(_);
                        return isbiasconf_isbiasctrl_isbiasctrlen_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Isbiasctrlen_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Isbiasctrlen_Read(_, __),
                    name: "Isbiasctrlen")
            .WithReservedBits(1, 1)
            .WithFlag(2, out isbiasconf_isbiasctrl_forcecalreq_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forcecalreq_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forcecalreq_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcecalreq_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcecalreq_Read(_, __),
                    name: "Forcecalreq")
            .WithFlag(3, out isbiasconf_isbiasctrl_forcetempreq_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forcetempreq_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forcetempreq_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcetempreq_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcetempreq_Read(_, __),
                    name: "Forcetempreq")
            .WithReservedBits(4, 1)
            .WithFlag(5, out isbiasconf_isbiasctrl_caldis_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Caldis_ValueProvider(_);
                        return isbiasconf_isbiasctrl_caldis_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Caldis_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Caldis_Read(_, __),
                    name: "Caldis")
            .WithFlag(6, out isbiasconf_isbiasctrl_tempcompdis_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Tempcompdis_ValueProvider(_);
                        return isbiasconf_isbiasctrl_tempcompdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Tempcompdis_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Tempcompdis_Read(_, __),
                    name: "Tempcompdis")
            .WithFlag(7, out isbiasconf_isbiasctrl_tempdis_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Tempdis_ValueProvider(_);
                        return isbiasconf_isbiasctrl_tempdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Tempdis_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Tempdis_Read(_, __),
                    name: "Tempdis")
            .WithReservedBits(8, 1)
            .WithFlag(9, out isbiasconf_isbiasctrl_forcecont_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forcecont_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forcecont_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcecont_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcecont_Read(_, __),
                    name: "Forcecont")
            .WithFlag(10, out isbiasconf_isbiasctrl_forceduty_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forceduty_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forceduty_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forceduty_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forceduty_Read(_, __),
                    name: "Forceduty")
            .WithFlag(11, out isbiasconf_isbiasctrl_forcebiasosc_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forcebiasosc_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forcebiasosc_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcebiasosc_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcebiasosc_Read(_, __),
                    name: "Forcebiasosc")
            .WithFlag(12, out isbiasconf_isbiasctrl_forceemuosc_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forceemuosc_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forceemuosc_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forceemuosc_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forceemuosc_Read(_, __),
                    name: "Forceemuosc")
            .WithFlag(13, out isbiasconf_isbiasctrl_caldlydbl_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Caldlydbl_ValueProvider(_);
                        return isbiasconf_isbiasctrl_caldlydbl_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Caldlydbl_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Caldlydbl_Read(_, __),
                    name: "Caldlydbl")
            .WithFlag(14, out isbiasconf_isbiasctrl_isbiasregen_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Isbiasregen_ValueProvider(_);
                        return isbiasconf_isbiasctrl_isbiasregen_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Isbiasregen_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Isbiasregen_Read(_, __),
                    name: "Isbiasregen")
            .WithReservedBits(15, 1)
            .WithFlag(16, out isbiasconf_isbiasctrl_forcerefreshrate_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forcerefreshrate_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forcerefreshrate_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcerefreshrate_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcerefreshrate_Read(_, __),
                    name: "Forcerefreshrate")
            .WithFlag(17, out isbiasconf_isbiasctrl_forcebgcont_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Forcebgcont_ValueProvider(_);
                        return isbiasconf_isbiasctrl_forcebgcont_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcebgcont_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Forcebgcont_Read(_, __),
                    name: "Forcebgcont")
            .WithReservedBits(18, 11)
            .WithEnumField<DoubleWordRegister, ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL>(29, 3, out isbiasconf_isbiasctrl_isbiasoutsel_field, 
                    valueProviderCallback: (_) => {
                        Isbiasconf_Isbiasctrl_Isbiasoutsel_ValueProvider(_);
                        return isbiasconf_isbiasctrl_isbiasoutsel_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasconf_Isbiasctrl_Isbiasoutsel_Write(_, __),
                    readCallback: (_, __) => Isbiasconf_Isbiasctrl_Isbiasoutsel_Read(_, __),
                    name: "Isbiasoutsel")
            .WithReadCallback((_, __) => Isbiasconf_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiasconf_Isbiasctrl_Write(_, __));
        
        // Isbiascalovr_Isbiasctrl - Offset : 0xC4
        protected DoubleWordRegister  GenerateIsbiascalovr_isbiasctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out isbiascalovr_isbiasctrl_calovr_bit, 
                    valueProviderCallback: (_) => {
                        Isbiascalovr_Isbiasctrl_Calovr_ValueProvider(_);
                        return isbiascalovr_isbiasctrl_calovr_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiascalovr_Isbiasctrl_Calovr_Write(_, __),
                    readCallback: (_, __) => Isbiascalovr_Isbiasctrl_Calovr_Read(_, __),
                    name: "Calovr")
            .WithReservedBits(1, 7)
            .WithValueField(8, 13, out isbiascalovr_isbiasctrl_calovrvalue_field, 
                    valueProviderCallback: (_) => {
                        Isbiascalovr_Isbiasctrl_Calovrvalue_ValueProvider(_);
                        return isbiascalovr_isbiasctrl_calovrvalue_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiascalovr_Isbiasctrl_Calovrvalue_Write(_, __),
                    readCallback: (_, __) => Isbiascalovr_Isbiasctrl_Calovrvalue_Read(_, __),
                    name: "Calovrvalue")
            .WithReservedBits(21, 11)
            .WithReadCallback((_, __) => Isbiascalovr_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiascalovr_Isbiasctrl_Write(_, __));
        
        // Isbiasperiod_Isbiasctrl - Offset : 0xC8
        protected DoubleWordRegister  GenerateIsbiasperiod_isbiasctrlRegister() => new DoubleWordRegister(this, 0x1030003)
            .WithEnumField<DoubleWordRegister, ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD>(0, 3, out isbiasperiod_isbiasctrl_tempperiod_field, 
                    valueProviderCallback: (_) => {
                        Isbiasperiod_Isbiasctrl_Tempperiod_ValueProvider(_);
                        return isbiasperiod_isbiasctrl_tempperiod_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasperiod_Isbiasctrl_Tempperiod_Write(_, __),
                    readCallback: (_, __) => Isbiasperiod_Isbiasctrl_Tempperiod_Read(_, __),
                    name: "Tempperiod")
            .WithReservedBits(3, 13)
            .WithEnumField<DoubleWordRegister, ISBIASPERIOD_ISBIASCTRL_CALPERIOD>(16, 3, out isbiasperiod_isbiasctrl_calperiod_field, 
                    valueProviderCallback: (_) => {
                        Isbiasperiod_Isbiasctrl_Calperiod_ValueProvider(_);
                        return isbiasperiod_isbiasctrl_calperiod_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasperiod_Isbiasctrl_Calperiod_Write(_, __),
                    readCallback: (_, __) => Isbiasperiod_Isbiasctrl_Calperiod_Read(_, __),
                    name: "Calperiod")
            .WithReservedBits(19, 5)
            .WithFlag(24, out isbiasperiod_isbiasctrl_em2periodx4_bit, 
                    valueProviderCallback: (_) => {
                        Isbiasperiod_Isbiasctrl_Em2periodx4_ValueProvider(_);
                        return isbiasperiod_isbiasctrl_em2periodx4_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiasperiod_Isbiasctrl_Em2periodx4_Write(_, __),
                    readCallback: (_, __) => Isbiasperiod_Isbiasctrl_Em2periodx4_Read(_, __),
                    name: "Em2periodx4")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => Isbiasperiod_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiasperiod_Isbiasctrl_Write(_, __));
        
        // Isbiastempcomprate_Isbiasctrl - Offset : 0xCC
        protected DoubleWordRegister  GenerateIsbiastempcomprate_isbiasctrlRegister() => new DoubleWordRegister(this, 0x6431)
            .WithEnumField<DoubleWordRegister, ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE>(0, 3, out isbiastempcomprate_isbiasctrl_r0refreshrate_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempcomprate_Isbiasctrl_R0refreshrate_ValueProvider(_);
                        return isbiastempcomprate_isbiasctrl_r0refreshrate_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R0refreshrate_Write(_, __),
                    readCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R0refreshrate_Read(_, __),
                    name: "R0refreshrate")
            .WithReservedBits(3, 1)
            .WithValueField(4, 3, out isbiastempcomprate_isbiasctrl_r1refreshrate_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempcomprate_Isbiasctrl_R1refreshrate_ValueProvider(_);
                        return isbiastempcomprate_isbiasctrl_r1refreshrate_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R1refreshrate_Write(_, __),
                    readCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R1refreshrate_Read(_, __),
                    name: "R1refreshrate")
            .WithReservedBits(7, 1)
            .WithValueField(8, 3, out isbiastempcomprate_isbiasctrl_r2refreshrate_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempcomprate_Isbiasctrl_R2refreshrate_ValueProvider(_);
                        return isbiastempcomprate_isbiasctrl_r2refreshrate_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R2refreshrate_Write(_, __),
                    readCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R2refreshrate_Read(_, __),
                    name: "R2refreshrate")
            .WithReservedBits(11, 1)
            .WithValueField(12, 3, out isbiastempcomprate_isbiasctrl_r3refreshrate_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempcomprate_Isbiasctrl_R3refreshrate_ValueProvider(_);
                        return isbiastempcomprate_isbiasctrl_r3refreshrate_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R3refreshrate_Write(_, __),
                    readCallback: (_, __) => Isbiastempcomprate_Isbiasctrl_R3refreshrate_Read(_, __),
                    name: "R3refreshrate")
            .WithReservedBits(15, 17)
            .WithReadCallback((_, __) => Isbiastempcomprate_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiastempcomprate_Isbiasctrl_Write(_, __));
        
        // Isbiastempcompthr_Isbiasctrl - Offset : 0xD0
        protected DoubleWordRegister  GenerateIsbiastempcompthr_isbiasctrlRegister() => new DoubleWordRegister(this, 0x17559948)
            .WithValueField(0, 9, out isbiastempcompthr_isbiasctrl_r1tempthr_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempcompthr_Isbiasctrl_R1tempthr_ValueProvider(_);
                        return isbiastempcompthr_isbiasctrl_r1tempthr_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempcompthr_Isbiasctrl_R1tempthr_Write(_, __),
                    readCallback: (_, __) => Isbiastempcompthr_Isbiasctrl_R1tempthr_Read(_, __),
                    name: "R1tempthr")
            .WithReservedBits(9, 1)
            .WithValueField(10, 9, out isbiastempcompthr_isbiasctrl_r2tempthr_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempcompthr_Isbiasctrl_R2tempthr_ValueProvider(_);
                        return isbiastempcompthr_isbiasctrl_r2tempthr_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempcompthr_Isbiasctrl_R2tempthr_Write(_, __),
                    readCallback: (_, __) => Isbiastempcompthr_Isbiasctrl_R2tempthr_Read(_, __),
                    name: "R2tempthr")
            .WithReservedBits(19, 1)
            .WithValueField(20, 9, out isbiastempcompthr_isbiasctrl_r3tempthr_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempcompthr_Isbiasctrl_R3tempthr_ValueProvider(_);
                        return isbiastempcompthr_isbiasctrl_r3tempthr_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempcompthr_Isbiasctrl_R3tempthr_Write(_, __),
                    readCallback: (_, __) => Isbiastempcompthr_Isbiasctrl_R3tempthr_Read(_, __),
                    name: "R3tempthr")
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => Isbiastempcompthr_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiastempcompthr_Isbiasctrl_Write(_, __));
        
        // Isbiaspfmrefreshcfg_Isbiasctrl - Offset : 0xD8
        protected DoubleWordRegister  GenerateIsbiaspfmrefreshcfg_isbiasctrlRegister() => new DoubleWordRegister(this, 0x60020C)
            .WithEnumField<DoubleWordRegister, ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT>(0, 3, out isbiaspfmrefreshcfg_isbiasctrl_s2fastrfshcnt_field, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfshcnt_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2fastrfshcnt_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfshcnt_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfshcnt_Read(_, __),
                    name: "S2fastrfshcnt")
            .WithValueField(3, 3, out isbiaspfmrefreshcfg_isbiasctrl_s2fastrfrshsmpduration_field, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshsmpduration_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2fastrfrshsmpduration_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshsmpduration_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshsmpduration_Read(_, __),
                    name: "S2fastrfrshsmpduration")
            .WithReservedBits(6, 2)
            .WithEnumField<DoubleWordRegister, ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO>(8, 3, out isbiaspfmrefreshcfg_isbiasctrl_s2prepdivratio_field, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2prepdivratio_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2prepdivratio_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2prepdivratio_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2prepdivratio_Read(_, __),
                    name: "S2prepdivratio")
            .WithReservedBits(11, 1)
            .WithFlag(12, out isbiaspfmrefreshcfg_isbiasctrl_s2roomtempdiv2_bit, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2roomtempdiv2_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2roomtempdiv2_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2roomtempdiv2_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2roomtempdiv2_Read(_, __),
                    name: "S2roomtempdiv2")
            .WithFlag(13, out isbiaspfmrefreshcfg_isbiasctrl_s2fastrfrshem2entryctrl_bit, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshem2entryctrl_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2fastrfrshem2entryctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshem2entryctrl_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshem2entryctrl_Read(_, __),
                    name: "S2fastrfrshem2entryctrl")
            .WithReservedBits(14, 4)
            .WithValueField(18, 3, out isbiaspfmrefreshcfg_isbiasctrl_s2delta_field, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2delta_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2delta_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2delta_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2delta_Read(_, __),
                    name: "S2delta")
            .WithValueField(21, 3, out isbiaspfmrefreshcfg_isbiasctrl_s2smpduration_field, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2smpduration_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2smpduration_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2smpduration_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2smpduration_Read(_, __),
                    name: "S2smpduration")
            .WithValueField(24, 3, out isbiaspfmrefreshcfg_isbiasctrl_s2divratio_field, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_S2divratio_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_s2divratio_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2divratio_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_S2divratio_Read(_, __),
                    name: "S2divratio")
            .WithReservedBits(27, 4)
            .WithFlag(31, out isbiaspfmrefreshcfg_isbiasctrl_pfmem2wuwaitimax_bit, 
                    valueProviderCallback: (_) => {
                        Isbiaspfmrefreshcfg_Isbiasctrl_Pfmem2wuwaitimax_ValueProvider(_);
                        return isbiaspfmrefreshcfg_isbiasctrl_pfmem2wuwaitimax_bit.Value;               
                    },
                    writeCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_Pfmem2wuwaitimax_Write(_, __),
                    readCallback: (_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_Pfmem2wuwaitimax_Read(_, __),
                    name: "Pfmem2wuwaitimax")
            .WithReadCallback((_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiaspfmrefreshcfg_Isbiasctrl_Write(_, __));
        
        // Isbiasrefreshcfg_Isbiasctrl - Offset : 0xDC
        protected DoubleWordRegister  GenerateIsbiasrefreshcfg_isbiasctrlRegister() => new DoubleWordRegister(this, 0x80004AA4)
            .WithValueField(0, 3, out isbiasrefreshcfg_isbiasctrl_s0delta_field, 
                    valueProviderCallback: (_) => {
                        Isbiasrefreshcfg_Isbiasctrl_S0delta_ValueProvider(_);
                        return isbiasrefreshcfg_isbiasctrl_s0delta_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S0delta_Write(_, __),
                    readCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S0delta_Read(_, __),
                    name: "S0delta")
            .WithValueField(3, 3, out isbiasrefreshcfg_isbiasctrl_s0smpduration_field, 
                    valueProviderCallback: (_) => {
                        Isbiasrefreshcfg_Isbiasctrl_S0smpduration_ValueProvider(_);
                        return isbiasrefreshcfg_isbiasctrl_s0smpduration_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S0smpduration_Write(_, __),
                    readCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S0smpduration_Read(_, __),
                    name: "S0smpduration")
            .WithEnumField<DoubleWordRegister, ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO>(6, 3, out isbiasrefreshcfg_isbiasctrl_s0divratio_field, 
                    valueProviderCallback: (_) => {
                        Isbiasrefreshcfg_Isbiasctrl_S0divratio_ValueProvider(_);
                        return isbiasrefreshcfg_isbiasctrl_s0divratio_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S0divratio_Write(_, __),
                    readCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S0divratio_Read(_, __),
                    name: "S0divratio")
            .WithValueField(9, 3, out isbiasrefreshcfg_isbiasctrl_s1delta_field, 
                    valueProviderCallback: (_) => {
                        Isbiasrefreshcfg_Isbiasctrl_S1delta_ValueProvider(_);
                        return isbiasrefreshcfg_isbiasctrl_s1delta_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1delta_Write(_, __),
                    readCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1delta_Read(_, __),
                    name: "S1delta")
            .WithValueField(12, 3, out isbiasrefreshcfg_isbiasctrl_s1smpduration_field, 
                    valueProviderCallback: (_) => {
                        Isbiasrefreshcfg_Isbiasctrl_S1smpduration_ValueProvider(_);
                        return isbiasrefreshcfg_isbiasctrl_s1smpduration_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1smpduration_Write(_, __),
                    readCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1smpduration_Read(_, __),
                    name: "S1smpduration")
            .WithValueField(15, 3, out isbiasrefreshcfg_isbiasctrl_s1divratio_field, 
                    valueProviderCallback: (_) => {
                        Isbiasrefreshcfg_Isbiasctrl_S1divratio_ValueProvider(_);
                        return isbiasrefreshcfg_isbiasctrl_s1divratio_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1divratio_Write(_, __),
                    readCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1divratio_Read(_, __),
                    name: "S1divratio")
            .WithReservedBits(18, 10)
            .WithValueField(28, 4, out isbiasrefreshcfg_isbiasctrl_s1temprangecont_field, 
                    valueProviderCallback: (_) => {
                        Isbiasrefreshcfg_Isbiasctrl_S1temprangecont_ValueProvider(_);
                        return isbiasrefreshcfg_isbiasctrl_s1temprangecont_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1temprangecont_Write(_, __),
                    readCallback: (_, __) => Isbiasrefreshcfg_Isbiasctrl_S1temprangecont_Read(_, __),
                    name: "S1temprangecont")
            .WithReadCallback((_, __) => Isbiasrefreshcfg_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiasrefreshcfg_Isbiasctrl_Write(_, __));
        
        // Isbiastempconst_Isbiasctrl - Offset : 0xE0
        protected DoubleWordRegister  GenerateIsbiastempconst_isbiasctrlRegister() => new DoubleWordRegister(this, 0x83317)
            .WithValueField(0, 20, out isbiastempconst_isbiasctrl_tempcalcconst_field, 
                    valueProviderCallback: (_) => {
                        Isbiastempconst_Isbiasctrl_Tempcalcconst_ValueProvider(_);
                        return isbiastempconst_isbiasctrl_tempcalcconst_field.Value;               
                    },
                    writeCallback: (_, __) => Isbiastempconst_Isbiasctrl_Tempcalcconst_Write(_, __),
                    readCallback: (_, __) => Isbiastempconst_Isbiasctrl_Tempcalcconst_Read(_, __),
                    name: "Tempcalcconst")
            .WithReservedBits(20, 12)
            .WithReadCallback((_, __) => Isbiastempconst_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiastempconst_Isbiasctrl_Write(_, __));
        
        // Isbiasstatus_Isbiasctrl - Offset : 0xE4
        protected DoubleWordRegister  GenerateIsbiasstatus_isbiasctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out isbiasstatus_isbiasctrl_isbiasout_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Isbiasout_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_isbiasout_field.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Isbiasout_Read(_, __),
                    name: "Isbiasout")
            .WithFlag(16, out isbiasstatus_isbiasctrl_firstcaldone_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Firstcaldone_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_firstcaldone_bit.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Firstcaldone_Read(_, __),
                    name: "Firstcaldone")
            .WithReservedBits(17, 3)
            .WithFlag(20, out isbiasstatus_isbiasctrl_isbiascalactive_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Isbiascalactive_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_isbiascalactive_bit.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Isbiascalactive_Read(_, __),
                    name: "Isbiascalactive")
            .WithFlag(21, out isbiasstatus_isbiasctrl_tempcompactive_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Tempcompactive_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_tempcompactive_bit.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Tempcompactive_Read(_, __),
                    name: "Tempcompactive")
            .WithReservedBits(22, 1)
            .WithFlag(23, out isbiasstatus_isbiasctrl_isbiascalcompout_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Isbiascalcompout_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_isbiascalcompout_bit.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Isbiascalcompout_Read(_, __),
                    name: "Isbiascalcompout")
            .WithReservedBits(24, 1)
            .WithValueField(25, 2, out isbiasstatus_isbiasctrl_vsbtemprange_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Vsbtemprange_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_vsbtemprange_field.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Vsbtemprange_Read(_, __),
                    name: "Vsbtemprange")
            .WithValueField(27, 3, out isbiasstatus_isbiasctrl_isbiasrefreshrate_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Isbiasrefreshrate_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_isbiasrefreshrate_field.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Isbiasrefreshrate_Read(_, __),
                    name: "Isbiasrefreshrate")
            .WithReservedBits(30, 1)
            .WithEnumField<DoubleWordRegister, ISBIASSTATUS_ISBIASCTRL_TESTLOCK>(31, 1, out isbiasstatus_isbiasctrl_testlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Isbiasstatus_Isbiasctrl_Testlock_ValueProvider(_);
                        return isbiasstatus_isbiasctrl_testlock_bit.Value;               
                    },
                    readCallback: (_, __) => Isbiasstatus_Isbiasctrl_Testlock_Read(_, __),
                    name: "Testlock")
            .WithReadCallback((_, __) => Isbiasstatus_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Isbiasstatus_Isbiasctrl_Write(_, __));
        
        // Vsbtempcomp_Isbiasctrl - Offset : 0xE8
        protected DoubleWordRegister  GenerateVsbtempcomp_isbiasctrlRegister() => new DoubleWordRegister(this, 0x51)
            .WithFlag(0, out vsbtempcomp_isbiasctrl_vsbtempcompen_bit, 
                    valueProviderCallback: (_) => {
                        Vsbtempcomp_Isbiasctrl_Vsbtempcompen_ValueProvider(_);
                        return vsbtempcomp_isbiasctrl_vsbtempcompen_bit.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcomp_Isbiasctrl_Vsbtempcompen_Write(_, __),
                    readCallback: (_, __) => Vsbtempcomp_Isbiasctrl_Vsbtempcompen_Read(_, __),
                    name: "Vsbtempcompen")
            .WithReservedBits(1, 1)
            .WithEnumField<DoubleWordRegister, VSBTEMPCOMP_ISBIASCTRL_R0VSB>(2, 2, out vsbtempcomp_isbiasctrl_r0vsb_field, 
                    valueProviderCallback: (_) => {
                        Vsbtempcomp_Isbiasctrl_R0vsb_ValueProvider(_);
                        return vsbtempcomp_isbiasctrl_r0vsb_field.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R0vsb_Write(_, __),
                    readCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R0vsb_Read(_, __),
                    name: "R0vsb")
            .WithEnumField<DoubleWordRegister, VSBTEMPCOMP_ISBIASCTRL_R1VSB>(4, 2, out vsbtempcomp_isbiasctrl_r1vsb_field, 
                    valueProviderCallback: (_) => {
                        Vsbtempcomp_Isbiasctrl_R1vsb_ValueProvider(_);
                        return vsbtempcomp_isbiasctrl_r1vsb_field.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R1vsb_Write(_, __),
                    readCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R1vsb_Read(_, __),
                    name: "R1vsb")
            .WithEnumField<DoubleWordRegister, VSBTEMPCOMP_ISBIASCTRL_R2VSB>(6, 2, out vsbtempcomp_isbiasctrl_r2vsb_field, 
                    valueProviderCallback: (_) => {
                        Vsbtempcomp_Isbiasctrl_R2vsb_ValueProvider(_);
                        return vsbtempcomp_isbiasctrl_r2vsb_field.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R2vsb_Write(_, __),
                    readCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R2vsb_Read(_, __),
                    name: "R2vsb")
            .WithEnumField<DoubleWordRegister, VSBTEMPCOMP_ISBIASCTRL_R3VSB>(8, 2, out vsbtempcomp_isbiasctrl_r3vsb_field, 
                    valueProviderCallback: (_) => {
                        Vsbtempcomp_Isbiasctrl_R3vsb_ValueProvider(_);
                        return vsbtempcomp_isbiasctrl_r3vsb_field.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R3vsb_Write(_, __),
                    readCallback: (_, __) => Vsbtempcomp_Isbiasctrl_R3vsb_Read(_, __),
                    name: "R3vsb")
            .WithReservedBits(10, 21)
            .WithFlag(31, out vsbtempcomp_isbiasctrl_vsbtestmodeen_bit, 
                    valueProviderCallback: (_) => {
                        Vsbtempcomp_Isbiasctrl_Vsbtestmodeen_ValueProvider(_);
                        return vsbtempcomp_isbiasctrl_vsbtestmodeen_bit.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcomp_Isbiasctrl_Vsbtestmodeen_Write(_, __),
                    readCallback: (_, __) => Vsbtempcomp_Isbiasctrl_Vsbtestmodeen_Read(_, __),
                    name: "Vsbtestmodeen")
            .WithReadCallback((_, __) => Vsbtempcomp_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Vsbtempcomp_Isbiasctrl_Write(_, __));
        
        // Vsbtempcompthr_Isbiasctrl - Offset : 0xEC
        protected DoubleWordRegister  GenerateVsbtempcompthr_isbiasctrlRegister() => new DoubleWordRegister(this, 0x17A5491B)
            .WithValueField(0, 9, out vsbtempcompthr_isbiasctrl_r1vsbtempthr_field, 
                    valueProviderCallback: (_) => {
                        Vsbtempcompthr_Isbiasctrl_R1vsbtempthr_ValueProvider(_);
                        return vsbtempcompthr_isbiasctrl_r1vsbtempthr_field.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcompthr_Isbiasctrl_R1vsbtempthr_Write(_, __),
                    readCallback: (_, __) => Vsbtempcompthr_Isbiasctrl_R1vsbtempthr_Read(_, __),
                    name: "R1vsbtempthr")
            .WithReservedBits(9, 1)
            .WithValueField(10, 9, out vsbtempcompthr_isbiasctrl_r2vsbtempthr_field, 
                    valueProviderCallback: (_) => {
                        Vsbtempcompthr_Isbiasctrl_R2vsbtempthr_ValueProvider(_);
                        return vsbtempcompthr_isbiasctrl_r2vsbtempthr_field.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcompthr_Isbiasctrl_R2vsbtempthr_Write(_, __),
                    readCallback: (_, __) => Vsbtempcompthr_Isbiasctrl_R2vsbtempthr_Read(_, __),
                    name: "R2vsbtempthr")
            .WithReservedBits(19, 1)
            .WithValueField(20, 9, out vsbtempcompthr_isbiasctrl_r3vsbtempthr_field, 
                    valueProviderCallback: (_) => {
                        Vsbtempcompthr_Isbiasctrl_R3vsbtempthr_ValueProvider(_);
                        return vsbtempcompthr_isbiasctrl_r3vsbtempthr_field.Value;               
                    },
                    writeCallback: (_, __) => Vsbtempcompthr_Isbiasctrl_R3vsbtempthr_Write(_, __),
                    readCallback: (_, __) => Vsbtempcompthr_Isbiasctrl_R3vsbtempthr_Read(_, __),
                    name: "R3vsbtempthr")
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => Vsbtempcompthr_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Vsbtempcompthr_Isbiasctrl_Write(_, __));
        
        // Retregtempcomp_Isbiasctrl - Offset : 0xF0
        protected DoubleWordRegister  GenerateRetregtempcomp_isbiasctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 4, out retregtempcomp_isbiasctrl_r0retreghstrim_field, 
                    valueProviderCallback: (_) => {
                        Retregtempcomp_Isbiasctrl_R0retreghstrim_ValueProvider(_);
                        return retregtempcomp_isbiasctrl_r0retreghstrim_field.Value;               
                    },
                    writeCallback: (_, __) => Retregtempcomp_Isbiasctrl_R0retreghstrim_Write(_, __),
                    readCallback: (_, __) => Retregtempcomp_Isbiasctrl_R0retreghstrim_Read(_, __),
                    name: "R0retreghstrim")
            .WithValueField(4, 4, out retregtempcomp_isbiasctrl_r1retreghstrim_field, 
                    valueProviderCallback: (_) => {
                        Retregtempcomp_Isbiasctrl_R1retreghstrim_ValueProvider(_);
                        return retregtempcomp_isbiasctrl_r1retreghstrim_field.Value;               
                    },
                    writeCallback: (_, __) => Retregtempcomp_Isbiasctrl_R1retreghstrim_Write(_, __),
                    readCallback: (_, __) => Retregtempcomp_Isbiasctrl_R1retreghstrim_Read(_, __),
                    name: "R1retreghstrim")
            .WithValueField(8, 4, out retregtempcomp_isbiasctrl_r2retreghstrim_field, 
                    valueProviderCallback: (_) => {
                        Retregtempcomp_Isbiasctrl_R2retreghstrim_ValueProvider(_);
                        return retregtempcomp_isbiasctrl_r2retreghstrim_field.Value;               
                    },
                    writeCallback: (_, __) => Retregtempcomp_Isbiasctrl_R2retreghstrim_Write(_, __),
                    readCallback: (_, __) => Retregtempcomp_Isbiasctrl_R2retreghstrim_Read(_, __),
                    name: "R2retreghstrim")
            .WithValueField(12, 4, out retregtempcomp_isbiasctrl_r3retreghstrim_field, 
                    valueProviderCallback: (_) => {
                        Retregtempcomp_Isbiasctrl_R3retreghstrim_ValueProvider(_);
                        return retregtempcomp_isbiasctrl_r3retreghstrim_field.Value;               
                    },
                    writeCallback: (_, __) => Retregtempcomp_Isbiasctrl_R3retreghstrim_Write(_, __),
                    readCallback: (_, __) => Retregtempcomp_Isbiasctrl_R3retreghstrim_Read(_, __),
                    name: "R3retreghstrim")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Retregtempcomp_Isbiasctrl_Read(_, __))
            .WithWriteCallback((_, __) => Retregtempcomp_Isbiasctrl_Write(_, __));
        
        // Efpif - Offset : 0x100
        protected DoubleWordRegister  GenerateEfpifRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out efpif_efpif_bit, 
                    valueProviderCallback: (_) => {
                        Efpif_Efpif_ValueProvider(_);
                        return efpif_efpif_bit.Value;               
                    },
                    writeCallback: (_, __) => Efpif_Efpif_Write(_, __),
                    readCallback: (_, __) => Efpif_Efpif_Read(_, __),
                    name: "Efpif")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Efpif_Read(_, __))
            .WithWriteCallback((_, __) => Efpif_Write(_, __));
        
        // Efpien - Offset : 0x104
        protected DoubleWordRegister  GenerateEfpienRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out efpien_efpien_bit, 
                    valueProviderCallback: (_) => {
                        Efpien_Efpien_ValueProvider(_);
                        return efpien_efpien_bit.Value;               
                    },
                    writeCallback: (_, __) => Efpien_Efpien_Write(_, __),
                    readCallback: (_, __) => Efpien_Efpien_Read(_, __),
                    name: "Efpien")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Efpien_Read(_, __))
            .WithWriteCallback((_, __) => Efpien_Write(_, __));
        
        // Efpctrl - Offset : 0x108
        protected DoubleWordRegister  GenerateEfpctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out efpctrl_efpdmem2inem4_bit, 
                    valueProviderCallback: (_) => {
                        Efpctrl_Efpdmem2inem4_ValueProvider(_);
                        return efpctrl_efpdmem2inem4_bit.Value;               
                    },
                    writeCallback: (_, __) => Efpctrl_Efpdmem2inem4_Write(_, __),
                    readCallback: (_, __) => Efpctrl_Efpdmem2inem4_Read(_, __),
                    name: "Efpdmem2inem4")
            .WithReservedBits(1, 1)
            .WithFlag(2, out efpctrl_efpdmswap_bit, 
                    valueProviderCallback: (_) => {
                        Efpctrl_Efpdmswap_ValueProvider(_);
                        return efpctrl_efpdmswap_bit.Value;               
                    },
                    writeCallback: (_, __) => Efpctrl_Efpdmswap_Write(_, __),
                    readCallback: (_, __) => Efpctrl_Efpdmswap_Read(_, __),
                    name: "Efpdmswap")
            .WithReservedBits(3, 5)
            .WithFlag(8, out efpctrl_efpdmoverride_bit, 
                    valueProviderCallback: (_) => {
                        Efpctrl_Efpdmoverride_ValueProvider(_);
                        return efpctrl_efpdmoverride_bit.Value;               
                    },
                    writeCallback: (_, __) => Efpctrl_Efpdmoverride_Write(_, __),
                    readCallback: (_, __) => Efpctrl_Efpdmoverride_Read(_, __),
                    name: "Efpdmoverride")
            .WithValueField(9, 2, out efpctrl_efpdmoverrideval_field, 
                    valueProviderCallback: (_) => {
                        Efpctrl_Efpdmoverrideval_ValueProvider(_);
                        return efpctrl_efpdmoverrideval_field.Value;               
                    },
                    writeCallback: (_, __) => Efpctrl_Efpdmoverrideval_Write(_, __),
                    readCallback: (_, __) => Efpctrl_Efpdmoverrideval_Read(_, __),
                    name: "Efpdmoverrideval")
            .WithReservedBits(11, 21)
            .WithReadCallback((_, __) => Efpctrl_Read(_, __))
            .WithWriteCallback((_, __) => Efpctrl_Write(_, __));
        
        // Rootctrl - Offset : 0x188
        protected DoubleWordRegister  GenerateRootctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rootctrl_vscalelock_bit, 
                    valueProviderCallback: (_) => {
                        Rootctrl_Vscalelock_ValueProvider(_);
                        return rootctrl_vscalelock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rootctrl_Vscalelock_Write(_, __),
                    readCallback: (_, __) => Rootctrl_Vscalelock_Read(_, __),
                    name: "Vscalelock")
            .WithReservedBits(1, 7)
            .WithValueField(8, 8, out rootctrl_tamperinfo_field, 
                    valueProviderCallback: (_) => {
                        Rootctrl_Tamperinfo_ValueProvider(_);
                        return rootctrl_tamperinfo_field.Value;               
                    },
                    writeCallback: (_, __) => Rootctrl_Tamperinfo_Write(_, __),
                    readCallback: (_, __) => Rootctrl_Tamperinfo_Read(_, __),
                    name: "Tamperinfo")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Rootctrl_Read(_, __))
            .WithWriteCallback((_, __) => Rootctrl_Write(_, __));
        
        // Rpuratd0_Drpu - Offset : 0x18C
        protected DoubleWordRegister  GenerateRpuratd0_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd0_drpu_ratdldreg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdldreg_ValueProvider(_);
                        return rpuratd0_drpu_ratdldreg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdldreg_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdldreg_Read(_, __),
                    name: "Ratdldreg")
            .WithFlag(1, out rpuratd0_drpu_ratddvddlebod_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratddvddlebod_ValueProvider(_);
                        return rpuratd0_drpu_ratddvddlebod_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratddvddlebod_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratddvddlebod_Read(_, __),
                    name: "Ratddvddlebod")
            .WithFlag(2, out rpuratd0_drpu_ratdvlmthv_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdvlmthv_ValueProvider(_);
                        return rpuratd0_drpu_ratdvlmthv_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdvlmthv_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdvlmthv_Read(_, __),
                    name: "Ratdvlmthv")
            .WithFlag(3, out rpuratd0_drpu_ratddvddbod_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratddvddbod_ValueProvider(_);
                        return rpuratd0_drpu_ratddvddbod_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratddvddbod_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratddvddbod_Read(_, __),
                    name: "Ratddvddbod")
            .WithFlag(4, out rpuratd0_drpu_ratddecbod_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratddecbod_ValueProvider(_);
                        return rpuratd0_drpu_ratddecbod_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratddecbod_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratddecbod_Read(_, __),
                    name: "Ratddecbod")
            .WithFlag(5, out rpuratd0_drpu_ratdhdreg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdhdreg_ValueProvider(_);
                        return rpuratd0_drpu_ratdhdreg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdhdreg_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdhdreg_Read(_, __),
                    name: "Ratdhdreg")
            .WithFlag(6, out rpuratd0_drpu_ratdretreg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdretreg_ValueProvider(_);
                        return rpuratd0_drpu_ratdretreg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdretreg_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdretreg_Read(_, __),
                    name: "Ratdretreg")
            .WithFlag(7, out rpuratd0_drpu_ratdbod3sensetrim_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdbod3sensetrim_ValueProvider(_);
                        return rpuratd0_drpu_ratdbod3sensetrim_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdbod3sensetrim_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdbod3sensetrim_Read(_, __),
                    name: "Ratdbod3sensetrim")
            .WithFlag(8, out rpuratd0_drpu_ratdbod3sense_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdbod3sense_ValueProvider(_);
                        return rpuratd0_drpu_ratdbod3sense_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdbod3sense_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdbod3sense_Read(_, __),
                    name: "Ratdbod3sense")
            .WithFlag(9, out rpuratd0_drpu_ratdisbias_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdisbias_ValueProvider(_);
                        return rpuratd0_drpu_ratdisbias_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdisbias_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdisbias_Read(_, __),
                    name: "Ratdisbias")
            .WithFlag(10, out rpuratd0_drpu_ratdisbiastrim_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdisbiastrim_ValueProvider(_);
                        return rpuratd0_drpu_ratdisbiastrim_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdisbiastrim_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdisbiastrim_Read(_, __),
                    name: "Ratdisbiastrim")
            .WithFlag(11, out rpuratd0_drpu_ratdisbiasvrefregtrim_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdisbiasvrefregtrim_ValueProvider(_);
                        return rpuratd0_drpu_ratdisbiasvrefregtrim_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdisbiasvrefregtrim_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdisbiasvrefregtrim_Read(_, __),
                    name: "Ratdisbiasvrefregtrim")
            .WithFlag(12, out rpuratd0_drpu_ratdisbiasvreflvbodtrim_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdisbiasvreflvbodtrim_ValueProvider(_);
                        return rpuratd0_drpu_ratdisbiasvreflvbodtrim_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdisbiasvreflvbodtrim_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdisbiasvreflvbodtrim_Read(_, __),
                    name: "Ratdisbiasvreflvbodtrim")
            .WithReservedBits(13, 1)
            .WithFlag(14, out rpuratd0_drpu_ratdpfmbyp_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdpfmbyp_ValueProvider(_);
                        return rpuratd0_drpu_ratdpfmbyp_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdpfmbyp_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdpfmbyp_Read(_, __),
                    name: "Ratdpfmbyp")
            .WithFlag(15, out rpuratd0_drpu_ratdpfmbypctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdpfmbypctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdpfmbypctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdpfmbypctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdpfmbypctrl_Read(_, __),
                    name: "Ratdpfmbypctrl")
            .WithFlag(16, out rpuratd0_drpu_ratdpd1paretctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdpd1paretctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdpd1paretctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdpd1paretctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdpd1paretctrl_Read(_, __),
                    name: "Ratdpd1paretctrl")
            .WithFlag(17, out rpuratd0_drpu_ratdpd0pwrctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdpd0pwrctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdpd0pwrctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdpd0pwrctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdpd0pwrctrl_Read(_, __),
                    name: "Ratdpd0pwrctrl")
            .WithFlag(18, out rpuratd0_drpu_ratdpd0pwrem2ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdpd0pwrem2ctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdpd0pwrem2ctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdpd0pwrem2ctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdpd0pwrem2ctrl_Read(_, __),
                    name: "Ratdpd0pwrem2ctrl")
            .WithReservedBits(19, 5)
            .WithFlag(24, out rpuratd0_drpu_ratdlock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdlock_ValueProvider(_);
                        return rpuratd0_drpu_ratdlock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdlock_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdlock_Read(_, __),
                    name: "Ratdlock")
            .WithFlag(25, out rpuratd0_drpu_ratdif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdif_ValueProvider(_);
                        return rpuratd0_drpu_ratdif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdif_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdif_Read(_, __),
                    name: "Ratdif")
            .WithFlag(26, out rpuratd0_drpu_ratdien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdien_ValueProvider(_);
                        return rpuratd0_drpu_ratdien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdien_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdien_Read(_, __),
                    name: "Ratdien")
            .WithFlag(27, out rpuratd0_drpu_ratdem4ctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdem4ctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdem4ctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdem4ctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdem4ctrl_Read(_, __),
                    name: "Ratdem4ctrl")
            .WithFlag(28, out rpuratd0_drpu_ratdcmd_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdcmd_ValueProvider(_);
                        return rpuratd0_drpu_ratdcmd_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdcmd_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdcmd_Read(_, __),
                    name: "Ratdcmd")
            .WithFlag(29, out rpuratd0_drpu_ratdctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdctrl_Read(_, __),
                    name: "Ratdctrl")
            .WithFlag(30, out rpuratd0_drpu_ratdtemplimits_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtemplimits_ValueProvider(_);
                        return rpuratd0_drpu_ratdtemplimits_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtemplimits_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtemplimits_Read(_, __),
                    name: "Ratdtemplimits")
            .WithFlag(31, out rpuratd0_drpu_ratdtemplimitsdg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdtemplimitsdg_ValueProvider(_);
                        return rpuratd0_drpu_ratdtemplimitsdg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdtemplimitsdg_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdtemplimitsdg_Read(_, __),
                    name: "Ratdtemplimitsdg")
            .WithReadCallback((_, __) => Rpuratd0_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd0_Drpu_Write(_, __));
        
        // Rpuratd1_Drpu - Offset : 0x190
        protected DoubleWordRegister  GenerateRpuratd1_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd1_drpu_ratdtemplimitsse_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdtemplimitsse_ValueProvider(_);
                        return rpuratd1_drpu_ratdtemplimitsse_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdtemplimitsse_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdtemplimitsse_Read(_, __),
                    name: "Ratdtemplimitsse")
            .WithReservedBits(1, 2)
            .WithFlag(3, out rpuratd1_drpu_ratdtestctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdtestctrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdtestctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdtestctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdtestctrl_Read(_, __),
                    name: "Ratdtestctrl")
            .WithFlag(4, out rpuratd1_drpu_ratdrmuctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdrmuctrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdrmuctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdrmuctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdrmuctrl_Read(_, __),
                    name: "Ratdrmuctrl")
            .WithReservedBits(5, 3)
            .WithFlag(8, out rpuratd1_drpu_ratddgif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratddgif_ValueProvider(_);
                        return rpuratd1_drpu_ratddgif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratddgif_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratddgif_Read(_, __),
                    name: "Ratddgif")
            .WithFlag(9, out rpuratd1_drpu_ratddgien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratddgien_ValueProvider(_);
                        return rpuratd1_drpu_ratddgien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratddgien_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratddgien_Read(_, __),
                    name: "Ratddgien")
            .WithFlag(10, out rpuratd1_drpu_ratdseqif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdseqif_ValueProvider(_);
                        return rpuratd1_drpu_ratdseqif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdseqif_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdseqif_Read(_, __),
                    name: "Ratdseqif")
            .WithFlag(11, out rpuratd1_drpu_ratdseqien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdseqien_ValueProvider(_);
                        return rpuratd1_drpu_ratdseqien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdseqien_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdseqien_Read(_, __),
                    name: "Ratdseqien")
            .WithFlag(12, out rpuratd1_drpu_ratddelaycfg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratddelaycfg_ValueProvider(_);
                        return rpuratd1_drpu_ratddelaycfg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratddelaycfg_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratddelaycfg_Read(_, __),
                    name: "Ratddelaycfg")
            .WithFlag(13, out rpuratd1_drpu_ratdtestlock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdtestlock_ValueProvider(_);
                        return rpuratd1_drpu_ratdtestlock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdtestlock_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdtestlock_Read(_, __),
                    name: "Ratdtestlock")
            .WithFlag(14, out rpuratd1_drpu_ratdauxctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdauxctrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdauxctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdauxctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdauxctrl_Read(_, __),
                    name: "Ratdauxctrl")
            .WithReservedBits(15, 1)
            .WithFlag(16, out rpuratd1_drpu_ratdisbiasconf_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiasconf_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiasconf_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiasconf_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiasconf_Read(_, __),
                    name: "Ratdisbiasconf")
            .WithFlag(17, out rpuratd1_drpu_ratdisbiascalovr_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiascalovr_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiascalovr_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiascalovr_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiascalovr_Read(_, __),
                    name: "Ratdisbiascalovr")
            .WithFlag(18, out rpuratd1_drpu_ratdisbiasperiod_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiasperiod_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiasperiod_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiasperiod_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiasperiod_Read(_, __),
                    name: "Ratdisbiasperiod")
            .WithFlag(19, out rpuratd1_drpu_ratdisbiastempcomprate_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiastempcomprate_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiastempcomprate_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiastempcomprate_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiastempcomprate_Read(_, __),
                    name: "Ratdisbiastempcomprate")
            .WithFlag(20, out rpuratd1_drpu_ratdisbiastempcompthr_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiastempcompthr_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiastempcompthr_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiastempcompthr_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiastempcompthr_Read(_, __),
                    name: "Ratdisbiastempcompthr")
            .WithReservedBits(21, 1)
            .WithFlag(22, out rpuratd1_drpu_ratdisbiaspfmrefreshcfg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiaspfmrefreshcfg_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiaspfmrefreshcfg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiaspfmrefreshcfg_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiaspfmrefreshcfg_Read(_, __),
                    name: "Ratdisbiaspfmrefreshcfg")
            .WithFlag(23, out rpuratd1_drpu_ratdisbiasrefreshcfg_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiasrefreshcfg_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiasrefreshcfg_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiasrefreshcfg_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiasrefreshcfg_Read(_, __),
                    name: "Ratdisbiasrefreshcfg")
            .WithFlag(24, out rpuratd1_drpu_ratdisbiastempconst_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdisbiastempconst_ValueProvider(_);
                        return rpuratd1_drpu_ratdisbiastempconst_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiastempconst_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdisbiastempconst_Read(_, __),
                    name: "Ratdisbiastempconst")
            .WithReservedBits(25, 1)
            .WithFlag(26, out rpuratd1_drpu_ratdvsbtempcomp_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdvsbtempcomp_ValueProvider(_);
                        return rpuratd1_drpu_ratdvsbtempcomp_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdvsbtempcomp_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdvsbtempcomp_Read(_, __),
                    name: "Ratdvsbtempcomp")
            .WithFlag(27, out rpuratd1_drpu_ratdvsbtempcompthr_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdvsbtempcompthr_ValueProvider(_);
                        return rpuratd1_drpu_ratdvsbtempcompthr_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdvsbtempcompthr_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdvsbtempcompthr_Read(_, __),
                    name: "Ratdvsbtempcompthr")
            .WithFlag(28, out rpuratd1_drpu_ratdretregtempcomp_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdretregtempcomp_ValueProvider(_);
                        return rpuratd1_drpu_ratdretregtempcomp_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdretregtempcomp_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdretregtempcomp_Read(_, __),
                    name: "Ratdretregtempcomp")
            .WithReservedBits(29, 3)
            .WithReadCallback((_, __) => Rpuratd1_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd1_Drpu_Write(_, __));
        
        // Rpuratd2_Drpu - Offset : 0x194
        protected DoubleWordRegister  GenerateRpuratd2_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd2_drpu_ratdefpif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdefpif_ValueProvider(_);
                        return rpuratd2_drpu_ratdefpif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdefpif_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdefpif_Read(_, __),
                    name: "Ratdefpif")
            .WithFlag(1, out rpuratd2_drpu_ratdefpien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdefpien_ValueProvider(_);
                        return rpuratd2_drpu_ratdefpien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdefpien_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdefpien_Read(_, __),
                    name: "Ratdefpien")
            .WithFlag(2, out rpuratd2_drpu_ratdefpctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdefpctrl_ValueProvider(_);
                        return rpuratd2_drpu_ratdefpctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdefpctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdefpctrl_Read(_, __),
                    name: "Ratdefpctrl")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Rpuratd2_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd2_Drpu_Write(_, __));
        
        // Rpuratd3_Drpu - Offset : 0x198
        protected DoubleWordRegister  GenerateRpuratd3_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 2)
            .WithFlag(2, out rpuratd3_drpu_ratdrootctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdrootctrl_ValueProvider(_);
                        return rpuratd3_drpu_ratdrootctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdrootctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdrootctrl_Read(_, __),
                    name: "Ratdrootctrl")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Rpuratd3_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd3_Drpu_Write(_, __));
        

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

        



        // Ldreg - Offset : 0x0
        protected IValueRegisterField ldreg_ldregbiasctrl_field;
        partial void Ldreg_Ldregbiasctrl_Write(ulong a, ulong b);
        partial void Ldreg_Ldregbiasctrl_Read(ulong a, ulong b);
        partial void Ldreg_Ldregbiasctrl_ValueProvider(ulong a);
        protected IFlagRegisterField ldreg_ovrldregen_bit;
        partial void Ldreg_Ovrldregen_Write(bool a, bool b);
        partial void Ldreg_Ovrldregen_Read(bool a, bool b);
        partial void Ldreg_Ovrldregen_ValueProvider(bool a);
        protected IFlagRegisterField ldreg_ovrldregoverrideen_bit;
        partial void Ldreg_Ovrldregoverrideen_Write(bool a, bool b);
        partial void Ldreg_Ovrldregoverrideen_Read(bool a, bool b);
        partial void Ldreg_Ovrldregoverrideen_ValueProvider(bool a);

        partial void Ldreg_Write(uint a, uint b);
        partial void Ldreg_Read(uint a, uint b);
        
        // Dvddlebod - Offset : 0x4
        protected IFlagRegisterField dvddlebod_dvddleboden_bit;
        partial void Dvddlebod_Dvddleboden_Write(bool a, bool b);
        partial void Dvddlebod_Dvddleboden_Read(bool a, bool b);
        partial void Dvddlebod_Dvddleboden_ValueProvider(bool a);
        protected IFlagRegisterField dvddlebod_dvddlebodmask_bit;
        partial void Dvddlebod_Dvddlebodmask_Write(bool a, bool b);
        partial void Dvddlebod_Dvddlebodmask_Read(bool a, bool b);
        partial void Dvddlebod_Dvddlebodmask_ValueProvider(bool a);
        protected IFlagRegisterField dvddlebod_dvddleboddisem01_bit;
        partial void Dvddlebod_Dvddleboddisem01_Write(bool a, bool b);
        partial void Dvddlebod_Dvddleboddisem01_Read(bool a, bool b);
        partial void Dvddlebod_Dvddleboddisem01_ValueProvider(bool a);
        protected IValueRegisterField dvddlebod_dvddlebodtrim_field;
        partial void Dvddlebod_Dvddlebodtrim_Write(ulong a, ulong b);
        partial void Dvddlebod_Dvddlebodtrim_Read(ulong a, ulong b);
        partial void Dvddlebod_Dvddlebodtrim_ValueProvider(ulong a);
        protected IValueRegisterField dvddlebod_dvddlebodbiastrim_field;
        partial void Dvddlebod_Dvddlebodbiastrim_Write(ulong a, ulong b);
        partial void Dvddlebod_Dvddlebodbiastrim_Read(ulong a, ulong b);
        partial void Dvddlebod_Dvddlebodbiastrim_ValueProvider(ulong a);
        protected IValueRegisterField dvddlebod_dvddlebodmode_field;
        partial void Dvddlebod_Dvddlebodmode_Write(ulong a, ulong b);
        partial void Dvddlebod_Dvddlebodmode_Read(ulong a, ulong b);
        partial void Dvddlebod_Dvddlebodmode_ValueProvider(ulong a);
        protected IEnumRegisterField<DVDDLEBOD_LEBODBLANKINGDELAY> dvddlebod_lebodblankingdelay_field;
        partial void Dvddlebod_Lebodblankingdelay_Write(DVDDLEBOD_LEBODBLANKINGDELAY a, DVDDLEBOD_LEBODBLANKINGDELAY b);
        partial void Dvddlebod_Lebodblankingdelay_Read(DVDDLEBOD_LEBODBLANKINGDELAY a, DVDDLEBOD_LEBODBLANKINGDELAY b);
        partial void Dvddlebod_Lebodblankingdelay_ValueProvider(DVDDLEBOD_LEBODBLANKINGDELAY a);
        protected IFlagRegisterField dvddlebod_ovrlebodoverrideen_bit;
        partial void Dvddlebod_Ovrlebodoverrideen_Write(bool a, bool b);
        partial void Dvddlebod_Ovrlebodoverrideen_Read(bool a, bool b);
        partial void Dvddlebod_Ovrlebodoverrideen_ValueProvider(bool a);

        partial void Dvddlebod_Write(uint a, uint b);
        partial void Dvddlebod_Read(uint a, uint b);
        
        // Vlmthv - Offset : 0x8
        protected IValueRegisterField vlmthv_vlmthvtrim_field;
        partial void Vlmthv_Vlmthvtrim_Write(ulong a, ulong b);
        partial void Vlmthv_Vlmthvtrim_Read(ulong a, ulong b);
        partial void Vlmthv_Vlmthvtrim_ValueProvider(ulong a);
        protected IFlagRegisterField vlmthv_vlmthventestload_bit;
        partial void Vlmthv_Vlmthventestload_Write(bool a, bool b);
        partial void Vlmthv_Vlmthventestload_Read(bool a, bool b);
        partial void Vlmthv_Vlmthventestload_ValueProvider(bool a);
        protected IFlagRegisterField vlmthv_vlmthvenstress_bit;
        partial void Vlmthv_Vlmthvenstress_Write(bool a, bool b);
        partial void Vlmthv_Vlmthvenstress_Read(bool a, bool b);
        partial void Vlmthv_Vlmthvenstress_ValueProvider(bool a);
        protected IFlagRegisterField vlmthv_vlmthvforcebypass_bit;
        partial void Vlmthv_Vlmthvforcebypass_Write(bool a, bool b);
        partial void Vlmthv_Vlmthvforcebypass_Read(bool a, bool b);
        partial void Vlmthv_Vlmthvforcebypass_ValueProvider(bool a);
        protected IFlagRegisterField vlmthv_vlmthvforceua_bit;
        partial void Vlmthv_Vlmthvforceua_Write(bool a, bool b);
        partial void Vlmthv_Vlmthvforceua_Read(bool a, bool b);
        partial void Vlmthv_Vlmthvforceua_ValueProvider(bool a);

        partial void Vlmthv_Write(uint a, uint b);
        partial void Vlmthv_Read(uint a, uint b);
        
        // Dvddbod - Offset : 0xC
        protected IValueRegisterField dvddbod_dvddbodthreshold_field;
        partial void Dvddbod_Dvddbodthreshold_Write(ulong a, ulong b);
        partial void Dvddbod_Dvddbodthreshold_Read(ulong a, ulong b);
        partial void Dvddbod_Dvddbodthreshold_ValueProvider(ulong a);
        protected IFlagRegisterField dvddbod_dvddbodmask_bit;
        partial void Dvddbod_Dvddbodmask_Write(bool a, bool b);
        partial void Dvddbod_Dvddbodmask_Read(bool a, bool b);
        partial void Dvddbod_Dvddbodmask_ValueProvider(bool a);
        protected IFlagRegisterField dvddbod_ovrhvbodbodthresholdsenseen_bit;
        partial void Dvddbod_Ovrhvbodbodthresholdsenseen_Write(bool a, bool b);
        partial void Dvddbod_Ovrhvbodbodthresholdsenseen_Read(bool a, bool b);
        partial void Dvddbod_Ovrhvbodbodthresholdsenseen_ValueProvider(bool a);
        protected IFlagRegisterField dvddbod_ovrhvbodoverrideen_bit;
        partial void Dvddbod_Ovrhvbodoverrideen_Write(bool a, bool b);
        partial void Dvddbod_Ovrhvbodoverrideen_Read(bool a, bool b);
        partial void Dvddbod_Ovrhvbodoverrideen_ValueProvider(bool a);

        partial void Dvddbod_Write(uint a, uint b);
        partial void Dvddbod_Read(uint a, uint b);
        
        // Decbod - Offset : 0x10
        protected IFlagRegisterField decbod_decboden_bit;
        partial void Decbod_Decboden_Write(bool a, bool b);
        partial void Decbod_Decboden_Read(bool a, bool b);
        partial void Decbod_Decboden_ValueProvider(bool a);
        protected IFlagRegisterField decbod_decbodmask_bit;
        partial void Decbod_Decbodmask_Write(bool a, bool b);
        partial void Decbod_Decbodmask_Read(bool a, bool b);
        partial void Decbod_Decbodmask_ValueProvider(bool a);
        protected IFlagRegisterField decbod_decovmboden_bit;
        partial void Decbod_Decovmboden_Write(bool a, bool b);
        partial void Decbod_Decovmboden_Read(bool a, bool b);
        partial void Decbod_Decovmboden_ValueProvider(bool a);
        protected IFlagRegisterField decbod_decovmbodmask_bit;
        partial void Decbod_Decovmbodmask_Write(bool a, bool b);
        partial void Decbod_Decovmbodmask_Read(bool a, bool b);
        partial void Decbod_Decovmbodmask_ValueProvider(bool a);

        partial void Decbod_Write(uint a, uint b);
        partial void Decbod_Read(uint a, uint b);
        
        // Hdreg - Offset : 0x14
        protected IValueRegisterField hdreg_hdregtrimvreg_field;
        partial void Hdreg_Hdregtrimvreg_Write(ulong a, ulong b);
        partial void Hdreg_Hdregtrimvreg_Read(ulong a, ulong b);
        partial void Hdreg_Hdregtrimvreg_ValueProvider(ulong a);
        protected IFlagRegisterField hdreg_ovrhdregswhardswlowleak_bit;
        partial void Hdreg_Ovrhdregswhardswlowleak_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregswhardswlowleak_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregswhardswlowleak_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregswsoftswon_bit;
        partial void Hdreg_Ovrhdregswsoftswon_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregswsoftswon_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregswsoftswon_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregswhardswon_bit;
        partial void Hdreg_Ovrhdregswhardswon_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregswhardswon_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregswhardswon_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregswoverrideen_bit;
        partial void Hdreg_Ovrhdregswoverrideen_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregswoverrideen_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregswoverrideen_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregwarmstart_bit;
        partial void Hdreg_Ovrhdregwarmstart_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregwarmstart_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregwarmstart_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregenramp_bit;
        partial void Hdreg_Ovrhdregenramp_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregenramp_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregenramp_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregenreg_bit;
        partial void Hdreg_Ovrhdregenreg_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregenreg_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregenreg_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregen_bit;
        partial void Hdreg_Ovrhdregen_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregen_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregen_ValueProvider(bool a);
        protected IFlagRegisterField hdreg_ovrhdregoverrideen_bit;
        partial void Hdreg_Ovrhdregoverrideen_Write(bool a, bool b);
        partial void Hdreg_Ovrhdregoverrideen_Read(bool a, bool b);
        partial void Hdreg_Ovrhdregoverrideen_ValueProvider(bool a);

        partial void Hdreg_Write(uint a, uint b);
        partial void Hdreg_Read(uint a, uint b);
        
        // Retreg - Offset : 0x18
        protected IValueRegisterField retreg_retreghighsidetrim_field;
        partial void Retreg_Retreghighsidetrim_Write(ulong a, ulong b);
        partial void Retreg_Retreghighsidetrim_Read(ulong a, ulong b);
        partial void Retreg_Retreghighsidetrim_ValueProvider(ulong a);
        protected IFlagRegisterField retreg_retreghstrimtempcompen_bit;
        partial void Retreg_Retreghstrimtempcompen_Write(bool a, bool b);
        partial void Retreg_Retreghstrimtempcompen_Read(bool a, bool b);
        partial void Retreg_Retreghstrimtempcompen_ValueProvider(bool a);
        protected IValueRegisterField retreg_retregidactrim_field;
        partial void Retreg_Retregidactrim_Write(ulong a, ulong b);
        partial void Retreg_Retregidactrim_Read(ulong a, ulong b);
        partial void Retreg_Retregidactrim_ValueProvider(ulong a);
        protected IFlagRegisterField retreg_retregcalrst_bit;
        partial void Retreg_Retregcalrst_Write(bool a, bool b);
        partial void Retreg_Retregcalrst_Read(bool a, bool b);
        partial void Retreg_Retregcalrst_ValueProvider(bool a);
        protected IFlagRegisterField retreg_retregcalen_bit;
        partial void Retreg_Retregcalen_Write(bool a, bool b);
        partial void Retreg_Retregcalen_Read(bool a, bool b);
        partial void Retreg_Retregcalen_ValueProvider(bool a);
        protected IFlagRegisterField retreg_retregtristateen_bit;
        partial void Retreg_Retregtristateen_Write(bool a, bool b);
        partial void Retreg_Retregtristateen_Read(bool a, bool b);
        partial void Retreg_Retregtristateen_ValueProvider(bool a);
        protected IFlagRegisterField retreg_ovrretreghighsidepuweakdis_bit;
        partial void Retreg_Ovrretreghighsidepuweakdis_Write(bool a, bool b);
        partial void Retreg_Ovrretreghighsidepuweakdis_Read(bool a, bool b);
        partial void Retreg_Ovrretreghighsidepuweakdis_ValueProvider(bool a);
        protected IFlagRegisterField retreg_ovrretreghighsidepudis_bit;
        partial void Retreg_Ovrretreghighsidepudis_Write(bool a, bool b);
        partial void Retreg_Ovrretreghighsidepudis_Read(bool a, bool b);
        partial void Retreg_Ovrretreghighsidepudis_ValueProvider(bool a);
        protected IFlagRegisterField retreg_ovrretregbypassen_bit;
        partial void Retreg_Ovrretregbypassen_Write(bool a, bool b);
        partial void Retreg_Ovrretregbypassen_Read(bool a, bool b);
        partial void Retreg_Ovrretregbypassen_ValueProvider(bool a);
        protected IEnumRegisterField<RETREG_OVRRETREGOVERRIDEEN> retreg_ovrretregoverrideen_bit;
        partial void Retreg_Ovrretregoverrideen_Write(RETREG_OVRRETREGOVERRIDEEN a, RETREG_OVRRETREGOVERRIDEEN b);
        partial void Retreg_Ovrretregoverrideen_Read(RETREG_OVRRETREGOVERRIDEEN a, RETREG_OVRRETREGOVERRIDEEN b);
        partial void Retreg_Ovrretregoverrideen_ValueProvider(RETREG_OVRRETREGOVERRIDEEN a);

        partial void Retreg_Write(uint a, uint b);
        partial void Retreg_Read(uint a, uint b);
        
        // Bod3sensetrim - Offset : 0x1C
        protected IValueRegisterField bod3sensetrim_avddbodtrim_field;
        partial void Bod3sensetrim_Avddbodtrim_Write(ulong a, ulong b);
        partial void Bod3sensetrim_Avddbodtrim_Read(ulong a, ulong b);
        partial void Bod3sensetrim_Avddbodtrim_ValueProvider(ulong a);
        protected IValueRegisterField bod3sensetrim_vddio0bodtrim_field;
        partial void Bod3sensetrim_Vddio0bodtrim_Write(ulong a, ulong b);
        partial void Bod3sensetrim_Vddio0bodtrim_Read(ulong a, ulong b);
        partial void Bod3sensetrim_Vddio0bodtrim_ValueProvider(ulong a);
        protected IValueRegisterField bod3sensetrim_vddio1bodtrim_field;
        partial void Bod3sensetrim_Vddio1bodtrim_Write(ulong a, ulong b);
        partial void Bod3sensetrim_Vddio1bodtrim_Read(ulong a, ulong b);
        partial void Bod3sensetrim_Vddio1bodtrim_ValueProvider(ulong a);
        protected IFlagRegisterField bod3sensetrim_bod3sensemode_bit;
        partial void Bod3sensetrim_Bod3sensemode_Write(bool a, bool b);
        partial void Bod3sensetrim_Bod3sensemode_Read(bool a, bool b);
        partial void Bod3sensetrim_Bod3sensemode_ValueProvider(bool a);

        partial void Bod3sensetrim_Write(uint a, uint b);
        partial void Bod3sensetrim_Read(uint a, uint b);
        
        // Bod3sense - Offset : 0x20
        protected IFlagRegisterField bod3sense_avddboden_bit;
        partial void Bod3sense_Avddboden_Write(bool a, bool b);
        partial void Bod3sense_Avddboden_Read(bool a, bool b);
        partial void Bod3sense_Avddboden_ValueProvider(bool a);
        protected IFlagRegisterField bod3sense_vddio0boden_bit;
        partial void Bod3sense_Vddio0boden_Write(bool a, bool b);
        partial void Bod3sense_Vddio0boden_Read(bool a, bool b);
        partial void Bod3sense_Vddio0boden_ValueProvider(bool a);
        protected IFlagRegisterField bod3sense_vddio1boden_bit;
        partial void Bod3sense_Vddio1boden_Write(bool a, bool b);
        partial void Bod3sense_Vddio1boden_Read(bool a, bool b);
        partial void Bod3sense_Vddio1boden_ValueProvider(bool a);
        protected IFlagRegisterField bod3sense_avddbodmask_bit;
        partial void Bod3sense_Avddbodmask_Write(bool a, bool b);
        partial void Bod3sense_Avddbodmask_Read(bool a, bool b);
        partial void Bod3sense_Avddbodmask_ValueProvider(bool a);
        protected IFlagRegisterField bod3sense_vddio0bodmask_bit;
        partial void Bod3sense_Vddio0bodmask_Write(bool a, bool b);
        partial void Bod3sense_Vddio0bodmask_Read(bool a, bool b);
        partial void Bod3sense_Vddio0bodmask_ValueProvider(bool a);
        protected IFlagRegisterField bod3sense_vddio1bodmask_bit;
        partial void Bod3sense_Vddio1bodmask_Write(bool a, bool b);
        partial void Bod3sense_Vddio1bodmask_Read(bool a, bool b);
        partial void Bod3sense_Vddio1bodmask_ValueProvider(bool a);

        partial void Bod3sense_Write(uint a, uint b);
        partial void Bod3sense_Read(uint a, uint b);
        
        // Isbias - Offset : 0x24
        protected IFlagRegisterField isbias_ovrpfmperprep_bit;
        partial void Isbias_Ovrpfmperprep_Write(bool a, bool b);
        partial void Isbias_Ovrpfmperprep_Read(bool a, bool b);
        partial void Isbias_Ovrpfmperprep_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrpfmperpresamp_bit;
        partial void Isbias_Ovrpfmperpresamp_Write(bool a, bool b);
        partial void Isbias_Ovrpfmperpresamp_Read(bool a, bool b);
        partial void Isbias_Ovrpfmperpresamp_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrpfmpersamp_bit;
        partial void Isbias_Ovrpfmpersamp_Write(bool a, bool b);
        partial void Isbias_Ovrpfmpersamp_Read(bool a, bool b);
        partial void Isbias_Ovrpfmpersamp_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrpfmperoverriderfrsh_bit;
        partial void Isbias_Ovrpfmperoverriderfrsh_Write(bool a, bool b);
        partial void Isbias_Ovrpfmperoverriderfrsh_Read(bool a, bool b);
        partial void Isbias_Ovrpfmperoverriderfrsh_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasprep_bit;
        partial void Isbias_Ovrisbiasprep_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasprep_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasprep_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiaspresamp_bit;
        partial void Isbias_Ovrisbiaspresamp_Write(bool a, bool b);
        partial void Isbias_Ovrisbiaspresamp_Read(bool a, bool b);
        partial void Isbias_Ovrisbiaspresamp_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiassamp_bit;
        partial void Isbias_Ovrisbiassamp_Write(bool a, bool b);
        partial void Isbias_Ovrisbiassamp_Read(bool a, bool b);
        partial void Isbias_Ovrisbiassamp_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasoverriderfrsh_bit;
        partial void Isbias_Ovrisbiasoverriderfrsh_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasoverriderfrsh_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasoverriderfrsh_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiastsensestart_bit;
        partial void Isbias_Ovrisbiastsensestart_Write(bool a, bool b);
        partial void Isbias_Ovrisbiastsensestart_Read(bool a, bool b);
        partial void Isbias_Ovrisbiastsensestart_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiastsenseen_bit;
        partial void Isbias_Ovrisbiastsenseen_Write(bool a, bool b);
        partial void Isbias_Ovrisbiastsenseen_Read(bool a, bool b);
        partial void Isbias_Ovrisbiastsenseen_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasselvl_bit;
        partial void Isbias_Ovrisbiasselvl_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasselvl_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasselvl_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasselvh_bit;
        partial void Isbias_Ovrisbiasselvh_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasselvh_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasselvh_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasselvbn_bit;
        partial void Isbias_Ovrisbiasselvbn_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasselvbn_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasselvbn_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasselvb1_bit;
        partial void Isbias_Ovrisbiasselvb1_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasselvb1_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasselvb1_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasrsttsensecomp_bit;
        partial void Isbias_Ovrisbiasrsttsensecomp_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasrsttsensecomp_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasrsttsensecomp_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasoverridetemp_bit;
        partial void Isbias_Ovrisbiasoverridetemp_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasoverridetemp_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasoverridetemp_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiascalen_bit;
        partial void Isbias_Ovrisbiascalen_Write(bool a, bool b);
        partial void Isbias_Ovrisbiascalen_Read(bool a, bool b);
        partial void Isbias_Ovrisbiascalen_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiascalrst_bit;
        partial void Isbias_Ovrisbiascalrst_Write(bool a, bool b);
        partial void Isbias_Ovrisbiascalrst_Read(bool a, bool b);
        partial void Isbias_Ovrisbiascalrst_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasoverridecal_bit;
        partial void Isbias_Ovrisbiasoverridecal_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasoverridecal_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasoverridecal_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiaswakeup_bit;
        partial void Isbias_Ovrisbiaswakeup_Write(bool a, bool b);
        partial void Isbias_Ovrisbiaswakeup_Read(bool a, bool b);
        partial void Isbias_Ovrisbiaswakeup_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasoscen_bit;
        partial void Isbias_Ovrisbiasoscen_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasoscen_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasoscen_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasbgcont_bit;
        partial void Isbias_Ovrisbiasbgcont_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasbgcont_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasbgcont_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiassampbufen_bit;
        partial void Isbias_Ovrisbiassampbufen_Write(bool a, bool b);
        partial void Isbias_Ovrisbiassampbufen_Read(bool a, bool b);
        partial void Isbias_Ovrisbiassampbufen_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiascscont_bit;
        partial void Isbias_Ovrisbiascscont_Write(bool a, bool b);
        partial void Isbias_Ovrisbiascscont_Read(bool a, bool b);
        partial void Isbias_Ovrisbiascscont_ValueProvider(bool a);
        protected IFlagRegisterField isbias_ovrisbiasoverrideen_bit;
        partial void Isbias_Ovrisbiasoverrideen_Write(bool a, bool b);
        partial void Isbias_Ovrisbiasoverrideen_Read(bool a, bool b);
        partial void Isbias_Ovrisbiasoverrideen_ValueProvider(bool a);

        partial void Isbias_Write(uint a, uint b);
        partial void Isbias_Read(uint a, uint b);
        
        // Isbiastrim - Offset : 0x28
        protected IValueRegisterField isbiastrim_isbiastrim1p1_field;
        partial void Isbiastrim_Isbiastrim1p1_Write(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrim1p1_Read(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrim1p1_ValueProvider(ulong a);
        protected IValueRegisterField isbiastrim_isbiastrimltc_field;
        partial void Isbiastrim_Isbiastrimltc_Write(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimltc_Read(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimltc_ValueProvider(ulong a);
        protected IValueRegisterField isbiastrim_isbiastrimoscrc_field;
        partial void Isbiastrim_Isbiastrimoscrc_Write(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimoscrc_Read(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimoscrc_ValueProvider(ulong a);
        protected IValueRegisterField isbiastrim_isbiastrimtc_field;
        partial void Isbiastrim_Isbiastrimtc_Write(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimtc_Read(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimtc_ValueProvider(ulong a);
        protected IValueRegisterField isbiastrim_isbiastrimoscgmc_field;
        partial void Isbiastrim_Isbiastrimoscgmc_Write(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimoscgmc_Read(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrimoscgmc_ValueProvider(ulong a);
        protected IValueRegisterField isbiastrim_isbiastrim1p18_field;
        partial void Isbiastrim_Isbiastrim1p18_Write(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrim1p18_Read(ulong a, ulong b);
        partial void Isbiastrim_Isbiastrim1p18_ValueProvider(ulong a);

        partial void Isbiastrim_Write(uint a, uint b);
        partial void Isbiastrim_Read(uint a, uint b);
        
        // Isbiasvrefregtrim - Offset : 0x2C
        protected IValueRegisterField isbiasvrefregtrim_vregvscale0trim_field;
        partial void Isbiasvrefregtrim_Vregvscale0trim_Write(ulong a, ulong b);
        partial void Isbiasvrefregtrim_Vregvscale0trim_Read(ulong a, ulong b);
        partial void Isbiasvrefregtrim_Vregvscale0trim_ValueProvider(ulong a);
        protected IValueRegisterField isbiasvrefregtrim_vregvscale1trim_field;
        partial void Isbiasvrefregtrim_Vregvscale1trim_Write(ulong a, ulong b);
        partial void Isbiasvrefregtrim_Vregvscale1trim_Read(ulong a, ulong b);
        partial void Isbiasvrefregtrim_Vregvscale1trim_ValueProvider(ulong a);
        protected IValueRegisterField isbiasvrefregtrim_vregvscale2trim_field;
        partial void Isbiasvrefregtrim_Vregvscale2trim_Write(ulong a, ulong b);
        partial void Isbiasvrefregtrim_Vregvscale2trim_Read(ulong a, ulong b);
        partial void Isbiasvrefregtrim_Vregvscale2trim_ValueProvider(ulong a);

        partial void Isbiasvrefregtrim_Write(uint a, uint b);
        partial void Isbiasvrefregtrim_Read(uint a, uint b);
        
        // Isbiasvreflvbodtrim - Offset : 0x30
        protected IValueRegisterField isbiasvreflvbodtrim_vregm70mvscale0trim_field;
        partial void Isbiasvreflvbodtrim_Vregm70mvscale0trim_Write(ulong a, ulong b);
        partial void Isbiasvreflvbodtrim_Vregm70mvscale0trim_Read(ulong a, ulong b);
        partial void Isbiasvreflvbodtrim_Vregm70mvscale0trim_ValueProvider(ulong a);
        protected IValueRegisterField isbiasvreflvbodtrim_vregm70mvscale1trim_field;
        partial void Isbiasvreflvbodtrim_Vregm70mvscale1trim_Write(ulong a, ulong b);
        partial void Isbiasvreflvbodtrim_Vregm70mvscale1trim_Read(ulong a, ulong b);
        partial void Isbiasvreflvbodtrim_Vregm70mvscale1trim_ValueProvider(ulong a);
        protected IValueRegisterField isbiasvreflvbodtrim_vregm70mvscale2trim_field;
        partial void Isbiasvreflvbodtrim_Vregm70mvscale2trim_Write(ulong a, ulong b);
        partial void Isbiasvreflvbodtrim_Vregm70mvscale2trim_Read(ulong a, ulong b);
        partial void Isbiasvreflvbodtrim_Vregm70mvscale2trim_ValueProvider(ulong a);

        partial void Isbiasvreflvbodtrim_Write(uint a, uint b);
        partial void Isbiasvreflvbodtrim_Read(uint a, uint b);
        
        // Anastatus - Offset : 0x34
        protected IFlagRegisterField anastatus_dvddlebodmaskoncfgchg_bit;
        partial void Anastatus_Dvddlebodmaskoncfgchg_Read(bool a, bool b);
        partial void Anastatus_Dvddlebodmaskoncfgchg_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_avddbodmaskoncfgchg_bit;
        partial void Anastatus_Avddbodmaskoncfgchg_Read(bool a, bool b);
        partial void Anastatus_Avddbodmaskoncfgchg_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_vddio0bodmaskoncfgchg_bit;
        partial void Anastatus_Vddio0bodmaskoncfgchg_Read(bool a, bool b);
        partial void Anastatus_Vddio0bodmaskoncfgchg_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_vddio1bodmaskoncfgchg_bit;
        partial void Anastatus_Vddio1bodmaskoncfgchg_Read(bool a, bool b);
        partial void Anastatus_Vddio1bodmaskoncfgchg_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_decbodmaskoncfgchg_bit;
        partial void Anastatus_Decbodmaskoncfgchg_Read(bool a, bool b);
        partial void Anastatus_Decbodmaskoncfgchg_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_vregincmpenmaskoncfgchg_bit;
        partial void Anastatus_Vregincmpenmaskoncfgchg_Read(bool a, bool b);
        partial void Anastatus_Vregincmpenmaskoncfgchg_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_dvddbod_bit;
        partial void Anastatus_Dvddbod_Read(bool a, bool b);
        partial void Anastatus_Dvddbod_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_dvddlebod_bit;
        partial void Anastatus_Dvddlebod_Read(bool a, bool b);
        partial void Anastatus_Dvddlebod_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_avddbod_bit;
        partial void Anastatus_Avddbod_Read(bool a, bool b);
        partial void Anastatus_Avddbod_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_vddio0bod_bit;
        partial void Anastatus_Vddio0bod_Read(bool a, bool b);
        partial void Anastatus_Vddio0bod_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_vddio1bod_bit;
        partial void Anastatus_Vddio1bod_Read(bool a, bool b);
        partial void Anastatus_Vddio1bod_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_decbod_bit;
        partial void Anastatus_Decbod_Read(bool a, bool b);
        partial void Anastatus_Decbod_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_decovmbod_bit;
        partial void Anastatus_Decovmbod_Read(bool a, bool b);
        partial void Anastatus_Decovmbod_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_pfmbypvreginltthres_bit;
        partial void Anastatus_Pfmbypvreginltthres_Read(bool a, bool b);
        partial void Anastatus_Pfmbypvreginltthres_ValueProvider(bool a);
        protected IFlagRegisterField anastatus_pfmbypcmpout_bit;
        partial void Anastatus_Pfmbypcmpout_Read(bool a, bool b);
        partial void Anastatus_Pfmbypcmpout_ValueProvider(bool a);

        partial void Anastatus_Write(uint a, uint b);
        partial void Anastatus_Read(uint a, uint b);
        
        // Pfmbyp - Offset : 0x38
        protected IFlagRegisterField pfmbyp_hyssel_bit;
        partial void Pfmbyp_Hyssel_Write(bool a, bool b);
        partial void Pfmbyp_Hyssel_Read(bool a, bool b);
        partial void Pfmbyp_Hyssel_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_climboosten_bit;
        partial void Pfmbyp_Climboosten_Write(bool a, bool b);
        partial void Pfmbyp_Climboosten_Read(bool a, bool b);
        partial void Pfmbyp_Climboosten_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_climregdis_bit;
        partial void Pfmbyp_Climregdis_Write(bool a, bool b);
        partial void Pfmbyp_Climregdis_Read(bool a, bool b);
        partial void Pfmbyp_Climregdis_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_cmforceen_bit;
        partial void Pfmbyp_Cmforceen_Write(bool a, bool b);
        partial void Pfmbyp_Cmforceen_Read(bool a, bool b);
        partial void Pfmbyp_Cmforceen_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_cmforceval_bit;
        partial void Pfmbyp_Cmforceval_Write(bool a, bool b);
        partial void Pfmbyp_Cmforceval_Read(bool a, bool b);
        partial void Pfmbyp_Cmforceval_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_compsel_bit;
        partial void Pfmbyp_Compsel_Write(bool a, bool b);
        partial void Pfmbyp_Compsel_Read(bool a, bool b);
        partial void Pfmbyp_Compsel_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_dcdcpfetdis_bit;
        partial void Pfmbyp_Dcdcpfetdis_Write(bool a, bool b);
        partial void Pfmbyp_Dcdcpfetdis_Read(bool a, bool b);
        partial void Pfmbyp_Dcdcpfetdis_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_dcdcpfetforceon_bit;
        partial void Pfmbyp_Dcdcpfetforceon_Write(bool a, bool b);
        partial void Pfmbyp_Dcdcpfetforceon_Read(bool a, bool b);
        partial void Pfmbyp_Dcdcpfetforceon_ValueProvider(bool a);
        protected IValueRegisterField pfmbyp_pfmisomode_field;
        partial void Pfmbyp_Pfmisomode_Write(ulong a, ulong b);
        partial void Pfmbyp_Pfmisomode_Read(ulong a, ulong b);
        partial void Pfmbyp_Pfmisomode_ValueProvider(ulong a);
        protected IFlagRegisterField pfmbyp_swweak_bit;
        partial void Pfmbyp_Swweak_Write(bool a, bool b);
        partial void Pfmbyp_Swweak_Read(bool a, bool b);
        partial void Pfmbyp_Swweak_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_ovrpfmbypswdis_bit;
        partial void Pfmbyp_Ovrpfmbypswdis_Write(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypswdis_Read(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypswdis_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_ovrpfmbypclimdis_bit;
        partial void Pfmbyp_Ovrpfmbypclimdis_Write(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypclimdis_Read(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypclimdis_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_ovrpfmbyprstn_bit;
        partial void Pfmbyp_Ovrpfmbyprstn_Write(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbyprstn_Read(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbyprstn_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_ovrpfmbypclimsel_bit;
        partial void Pfmbyp_Ovrpfmbypclimsel_Write(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypclimsel_Read(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypclimsel_ValueProvider(bool a);
        protected IFlagRegisterField pfmbyp_ovrpfmbypoverrideen_bit;
        partial void Pfmbyp_Ovrpfmbypoverrideen_Write(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypoverrideen_Read(bool a, bool b);
        partial void Pfmbyp_Ovrpfmbypoverrideen_ValueProvider(bool a);

        partial void Pfmbyp_Write(uint a, uint b);
        partial void Pfmbyp_Read(uint a, uint b);
        
        // Vregvddcmpctrl - Offset : 0x3C
        protected IFlagRegisterField vregvddcmpctrl_vregincmpen_bit;
        partial void Vregvddcmpctrl_Vregincmpen_Write(bool a, bool b);
        partial void Vregvddcmpctrl_Vregincmpen_Read(bool a, bool b);
        partial void Vregvddcmpctrl_Vregincmpen_ValueProvider(bool a);
        protected IValueRegisterField vregvddcmpctrl_thressel_field;
        partial void Vregvddcmpctrl_Thressel_Write(ulong a, ulong b);
        partial void Vregvddcmpctrl_Thressel_Read(ulong a, ulong b);
        partial void Vregvddcmpctrl_Thressel_ValueProvider(ulong a);

        partial void Vregvddcmpctrl_Write(uint a, uint b);
        partial void Vregvddcmpctrl_Read(uint a, uint b);
        
        // Pd1paretctrl - Offset : 0x40
        protected IEnumRegisterField<PD1PARETCTRL_PD1PARETDIS> pd1paretctrl_pd1paretdis_field;
        partial void Pd1paretctrl_Pd1paretdis_Write(PD1PARETCTRL_PD1PARETDIS a, PD1PARETCTRL_PD1PARETDIS b);
        partial void Pd1paretctrl_Pd1paretdis_Read(PD1PARETCTRL_PD1PARETDIS a, PD1PARETCTRL_PD1PARETDIS b);
        partial void Pd1paretctrl_Pd1paretdis_ValueProvider(PD1PARETCTRL_PD1PARETDIS a);

        partial void Pd1paretctrl_Write(uint a, uint b);
        partial void Pd1paretctrl_Read(uint a, uint b);
        
        // Pd0pwrctrl - Offset : 0x44
        protected IEnumRegisterField<PD0PWRCTRL_PD0PWRDN> pd0pwrctrl_pd0pwrdn_field;
        partial void Pd0pwrctrl_Pd0pwrdn_Write(PD0PWRCTRL_PD0PWRDN a, PD0PWRCTRL_PD0PWRDN b);
        partial void Pd0pwrctrl_Pd0pwrdn_Read(PD0PWRCTRL_PD0PWRDN a, PD0PWRCTRL_PD0PWRDN b);
        partial void Pd0pwrctrl_Pd0pwrdn_ValueProvider(PD0PWRCTRL_PD0PWRDN a);
        protected IEnumRegisterField<PD0PWRCTRL_EM2PDPWRDN> pd0pwrctrl_em2pdpwrdn_field;
        partial void Pd0pwrctrl_Em2pdpwrdn_Write(PD0PWRCTRL_EM2PDPWRDN a, PD0PWRCTRL_EM2PDPWRDN b);
        partial void Pd0pwrctrl_Em2pdpwrdn_Read(PD0PWRCTRL_EM2PDPWRDN a, PD0PWRCTRL_EM2PDPWRDN b);
        partial void Pd0pwrctrl_Em2pdpwrdn_ValueProvider(PD0PWRCTRL_EM2PDPWRDN a);
        protected IEnumRegisterField<PD0PWRCTRL_DISCHRGPD> pd0pwrctrl_dischrgpd_field;
        partial void Pd0pwrctrl_Dischrgpd_Write(PD0PWRCTRL_DISCHRGPD a, PD0PWRCTRL_DISCHRGPD b);
        partial void Pd0pwrctrl_Dischrgpd_Read(PD0PWRCTRL_DISCHRGPD a, PD0PWRCTRL_DISCHRGPD b);
        partial void Pd0pwrctrl_Dischrgpd_ValueProvider(PD0PWRCTRL_DISCHRGPD a);
        protected IFlagRegisterField pd0pwrctrl_dischrgpdem2en_bit;
        partial void Pd0pwrctrl_Dischrgpdem2en_Write(bool a, bool b);
        partial void Pd0pwrctrl_Dischrgpdem2en_Read(bool a, bool b);
        partial void Pd0pwrctrl_Dischrgpdem2en_ValueProvider(bool a);

        partial void Pd0pwrctrl_Write(uint a, uint b);
        partial void Pd0pwrctrl_Read(uint a, uint b);
        
        // Pd0pwrem2ctrl - Offset : 0x48
        protected IFlagRegisterField pd0pwrem2ctrl_em2pd0ben_bit;
        partial void Pd0pwrem2ctrl_Em2pd0ben_Write(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0ben_Read(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0ben_ValueProvider(bool a);
        protected IFlagRegisterField pd0pwrem2ctrl_em2pd0cen_bit;
        partial void Pd0pwrem2ctrl_Em2pd0cen_Write(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0cen_Read(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0cen_ValueProvider(bool a);
        protected IFlagRegisterField pd0pwrem2ctrl_em2pd0den_bit;
        partial void Pd0pwrem2ctrl_Em2pd0den_Write(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0den_Read(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0den_ValueProvider(bool a);
        protected IFlagRegisterField pd0pwrem2ctrl_em2pd0een_bit;
        partial void Pd0pwrem2ctrl_Em2pd0een_Write(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0een_Read(bool a, bool b);
        partial void Pd0pwrem2ctrl_Em2pd0een_ValueProvider(bool a);

        partial void Pd0pwrem2ctrl_Write(uint a, uint b);
        partial void Pd0pwrem2ctrl_Read(uint a, uint b);
        
        // Ipversion_Rev - Offset : 0x5C
        protected IValueRegisterField ipversion_rev_ipversion_field;
        partial void Ipversion_Rev_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Rev_Ipversion_ValueProvider(ulong a);

        partial void Ipversion_Rev_Write(uint a, uint b);
        partial void Ipversion_Rev_Read(uint a, uint b);
        
        // Lock - Offset : 0x60
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // If - Offset : 0x64
        protected IFlagRegisterField if_avddbod_bit;
        partial void If_Avddbod_Write(bool a, bool b);
        partial void If_Avddbod_Read(bool a, bool b);
        partial void If_Avddbod_ValueProvider(bool a);
        protected IFlagRegisterField if_iovdd0bod_bit;
        partial void If_Iovdd0bod_Write(bool a, bool b);
        partial void If_Iovdd0bod_Read(bool a, bool b);
        partial void If_Iovdd0bod_ValueProvider(bool a);
        protected IFlagRegisterField if_iovdd1bod_bit;
        partial void If_Iovdd1bod_Write(bool a, bool b);
        partial void If_Iovdd1bod_Read(bool a, bool b);
        partial void If_Iovdd1bod_ValueProvider(bool a);
        protected IFlagRegisterField if_em23wakeup_bit;
        partial void If_Em23wakeup_Write(bool a, bool b);
        partial void If_Em23wakeup_Read(bool a, bool b);
        partial void If_Em23wakeup_ValueProvider(bool a);
        protected IFlagRegisterField if_vscaledone_bit;
        partial void If_Vscaledone_Write(bool a, bool b);
        partial void If_Vscaledone_Read(bool a, bool b);
        partial void If_Vscaledone_ValueProvider(bool a);
        protected IFlagRegisterField if_tempavg_bit;
        partial void If_Tempavg_Write(bool a, bool b);
        partial void If_Tempavg_Read(bool a, bool b);
        partial void If_Tempavg_ValueProvider(bool a);
        protected IFlagRegisterField if_temp_bit;
        partial void If_Temp_Write(bool a, bool b);
        partial void If_Temp_Read(bool a, bool b);
        partial void If_Temp_ValueProvider(bool a);
        protected IFlagRegisterField if_templow_bit;
        partial void If_Templow_Write(bool a, bool b);
        partial void If_Templow_Read(bool a, bool b);
        partial void If_Templow_ValueProvider(bool a);
        protected IFlagRegisterField if_temphigh_bit;
        partial void If_Temphigh_Write(bool a, bool b);
        partial void If_Temphigh_Read(bool a, bool b);
        partial void If_Temphigh_ValueProvider(bool a);

        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x68
        protected IFlagRegisterField ien_avddbod_bit;
        partial void Ien_Avddbod_Write(bool a, bool b);
        partial void Ien_Avddbod_Read(bool a, bool b);
        partial void Ien_Avddbod_ValueProvider(bool a);
        protected IFlagRegisterField ien_iovdd0bod_bit;
        partial void Ien_Iovdd0bod_Write(bool a, bool b);
        partial void Ien_Iovdd0bod_Read(bool a, bool b);
        partial void Ien_Iovdd0bod_ValueProvider(bool a);
        protected IFlagRegisterField ien_iovdd1bod_bit;
        partial void Ien_Iovdd1bod_Write(bool a, bool b);
        partial void Ien_Iovdd1bod_Read(bool a, bool b);
        partial void Ien_Iovdd1bod_ValueProvider(bool a);
        protected IFlagRegisterField ien_em23wakeup_bit;
        partial void Ien_Em23wakeup_Write(bool a, bool b);
        partial void Ien_Em23wakeup_Read(bool a, bool b);
        partial void Ien_Em23wakeup_ValueProvider(bool a);
        protected IFlagRegisterField ien_vscaledone_bit;
        partial void Ien_Vscaledone_Write(bool a, bool b);
        partial void Ien_Vscaledone_Read(bool a, bool b);
        partial void Ien_Vscaledone_ValueProvider(bool a);
        protected IFlagRegisterField ien_tempavg_bit;
        partial void Ien_Tempavg_Write(bool a, bool b);
        partial void Ien_Tempavg_Read(bool a, bool b);
        partial void Ien_Tempavg_ValueProvider(bool a);
        protected IFlagRegisterField ien_temp_bit;
        partial void Ien_Temp_Write(bool a, bool b);
        partial void Ien_Temp_Read(bool a, bool b);
        partial void Ien_Temp_ValueProvider(bool a);
        protected IFlagRegisterField ien_templow_bit;
        partial void Ien_Templow_Write(bool a, bool b);
        partial void Ien_Templow_Read(bool a, bool b);
        partial void Ien_Templow_ValueProvider(bool a);
        protected IFlagRegisterField ien_temphigh_bit;
        partial void Ien_Temphigh_Write(bool a, bool b);
        partial void Ien_Temphigh_Read(bool a, bool b);
        partial void Ien_Temphigh_ValueProvider(bool a);

        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Em4ctrl - Offset : 0x6C
        protected IValueRegisterField em4ctrl_em4entry_field;
        partial void Em4ctrl_Em4entry_Write(ulong a, ulong b);
        partial void Em4ctrl_Em4entry_Read(ulong a, ulong b);
        partial void Em4ctrl_Em4entry_ValueProvider(ulong a);
        protected IEnumRegisterField<EM4CTRL_EM4IORETMODE> em4ctrl_em4ioretmode_field;
        partial void Em4ctrl_Em4ioretmode_Write(EM4CTRL_EM4IORETMODE a, EM4CTRL_EM4IORETMODE b);
        partial void Em4ctrl_Em4ioretmode_Read(EM4CTRL_EM4IORETMODE a, EM4CTRL_EM4IORETMODE b);
        partial void Em4ctrl_Em4ioretmode_ValueProvider(EM4CTRL_EM4IORETMODE a);
        protected IFlagRegisterField em4ctrl_bod3senseem4wu_bit;
        partial void Em4ctrl_Bod3senseem4wu_Write(bool a, bool b);
        partial void Em4ctrl_Bod3senseem4wu_Read(bool a, bool b);
        partial void Em4ctrl_Bod3senseem4wu_ValueProvider(bool a);

        partial void Em4ctrl_Write(uint a, uint b);
        partial void Em4ctrl_Read(uint a, uint b);
        
        // Cmd - Offset : 0x70
        protected IFlagRegisterField cmd_em4unlatch_bit;
        partial void Cmd_Em4unlatch_Write(bool a, bool b);
        partial void Cmd_Em4unlatch_ValueProvider(bool a);
        protected IFlagRegisterField cmd_tempavgreq_bit;
        partial void Cmd_Tempavgreq_Write(bool a, bool b);
        partial void Cmd_Tempavgreq_ValueProvider(bool a);
        protected IFlagRegisterField cmd_em01vscale0_bit;
        partial void Cmd_Em01vscale0_Write(bool a, bool b);
        partial void Cmd_Em01vscale0_ValueProvider(bool a);
        protected IFlagRegisterField cmd_em01vscale1_bit;
        partial void Cmd_Em01vscale1_Write(bool a, bool b);
        partial void Cmd_Em01vscale1_ValueProvider(bool a);
        protected IFlagRegisterField cmd_em01vscale2_bit;
        partial void Cmd_Em01vscale2_Write(bool a, bool b);
        partial void Cmd_Em01vscale2_ValueProvider(bool a);
        protected IFlagRegisterField cmd_rstcauseclr_bit;
        partial void Cmd_Rstcauseclr_Write(bool a, bool b);
        partial void Cmd_Rstcauseclr_ValueProvider(bool a);
        protected IFlagRegisterField cmd_tamperrcclr_bit;
        partial void Cmd_Tamperrcclr_Write(bool a, bool b);
        partial void Cmd_Tamperrcclr_ValueProvider(bool a);

        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);
        
        // Ctrl - Offset : 0x74
        protected IFlagRegisterField ctrl_em2dbgen_bit;
        partial void Ctrl_Em2dbgen_Write(bool a, bool b);
        partial void Ctrl_Em2dbgen_Read(bool a, bool b);
        partial void Ctrl_Em2dbgen_ValueProvider(bool a);
        protected IEnumRegisterField<CTRL_TEMPAVGNUM> ctrl_tempavgnum_bit;
        partial void Ctrl_Tempavgnum_Write(CTRL_TEMPAVGNUM a, CTRL_TEMPAVGNUM b);
        partial void Ctrl_Tempavgnum_Read(CTRL_TEMPAVGNUM a, CTRL_TEMPAVGNUM b);
        partial void Ctrl_Tempavgnum_ValueProvider(CTRL_TEMPAVGNUM a);
        protected IEnumRegisterField<CTRL_EM23VSCALE> ctrl_em23vscale_field;
        partial void Ctrl_Em23vscale_Write(CTRL_EM23VSCALE a, CTRL_EM23VSCALE b);
        partial void Ctrl_Em23vscale_Read(CTRL_EM23VSCALE a, CTRL_EM23VSCALE b);
        partial void Ctrl_Em23vscale_ValueProvider(CTRL_EM23VSCALE a);
        protected IFlagRegisterField ctrl_flashpwrupondemand_bit;
        partial void Ctrl_Flashpwrupondemand_Write(bool a, bool b);
        partial void Ctrl_Flashpwrupondemand_Read(bool a, bool b);
        partial void Ctrl_Flashpwrupondemand_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_efpdirectmodeen_bit;
        partial void Ctrl_Efpdirectmodeen_Write(bool a, bool b);
        partial void Ctrl_Efpdirectmodeen_Read(bool a, bool b);
        partial void Ctrl_Efpdirectmodeen_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_efpdrvdecouple_bit;
        partial void Ctrl_Efpdrvdecouple_Write(bool a, bool b);
        partial void Ctrl_Efpdrvdecouple_Read(bool a, bool b);
        partial void Ctrl_Efpdrvdecouple_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_efpdrvdvdd_bit;
        partial void Ctrl_Efpdrvdvdd_Write(bool a, bool b);
        partial void Ctrl_Efpdrvdvdd_Read(bool a, bool b);
        partial void Ctrl_Efpdrvdvdd_ValueProvider(bool a);

        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Templimits - Offset : 0x78
        protected IValueRegisterField templimits_templow_field;
        partial void Templimits_Templow_Write(ulong a, ulong b);
        partial void Templimits_Templow_Read(ulong a, ulong b);
        partial void Templimits_Templow_ValueProvider(ulong a);
        protected IValueRegisterField templimits_temphigh_field;
        partial void Templimits_Temphigh_Write(ulong a, ulong b);
        partial void Templimits_Temphigh_Read(ulong a, ulong b);
        partial void Templimits_Temphigh_ValueProvider(ulong a);

        partial void Templimits_Write(uint a, uint b);
        partial void Templimits_Read(uint a, uint b);
        
        // Templimitsdg - Offset : 0x7C
        protected IValueRegisterField templimitsdg_templowdg_field;
        partial void Templimitsdg_Templowdg_Write(ulong a, ulong b);
        partial void Templimitsdg_Templowdg_Read(ulong a, ulong b);
        partial void Templimitsdg_Templowdg_ValueProvider(ulong a);
        protected IValueRegisterField templimitsdg_temphidg_field;
        partial void Templimitsdg_Temphidg_Write(ulong a, ulong b);
        partial void Templimitsdg_Temphidg_Read(ulong a, ulong b);
        partial void Templimitsdg_Temphidg_ValueProvider(ulong a);

        partial void Templimitsdg_Write(uint a, uint b);
        partial void Templimitsdg_Read(uint a, uint b);
        
        // Templimitsse - Offset : 0x80
        protected IValueRegisterField templimitsse_templowse_field;
        partial void Templimitsse_Templowse_Write(ulong a, ulong b);
        partial void Templimitsse_Templowse_Read(ulong a, ulong b);
        partial void Templimitsse_Templowse_ValueProvider(ulong a);
        protected IValueRegisterField templimitsse_temphise_field;
        partial void Templimitsse_Temphise_Write(ulong a, ulong b);
        partial void Templimitsse_Temphise_Read(ulong a, ulong b);
        partial void Templimitsse_Temphise_ValueProvider(ulong a);

        partial void Templimitsse_Write(uint a, uint b);
        partial void Templimitsse_Read(uint a, uint b);
        
        // Status - Offset : 0x84
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);
        protected IFlagRegisterField status_firsttempdone_bit;
        partial void Status_Firsttempdone_Read(bool a, bool b);
        partial void Status_Firsttempdone_ValueProvider(bool a);
        protected IFlagRegisterField status_tempactive_bit;
        partial void Status_Tempactive_Read(bool a, bool b);
        partial void Status_Tempactive_ValueProvider(bool a);
        protected IFlagRegisterField status_tempavgactive_bit;
        partial void Status_Tempavgactive_Read(bool a, bool b);
        partial void Status_Tempavgactive_ValueProvider(bool a);
        protected IFlagRegisterField status_vscalebusy_bit;
        partial void Status_Vscalebusy_Read(bool a, bool b);
        partial void Status_Vscalebusy_ValueProvider(bool a);
        protected IFlagRegisterField status_vscalefailed_bit;
        partial void Status_Vscalefailed_Read(bool a, bool b);
        partial void Status_Vscalefailed_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_VSCALE> status_vscale_field;
        partial void Status_Vscale_Read(STATUS_VSCALE a, STATUS_VSCALE b);
        partial void Status_Vscale_ValueProvider(STATUS_VSCALE a);
        protected IFlagRegisterField status_racactive_bit;
        partial void Status_Racactive_Read(bool a, bool b);
        partial void Status_Racactive_ValueProvider(bool a);
        protected IFlagRegisterField status_em4ioret_bit;
        partial void Status_Em4ioret_Read(bool a, bool b);
        partial void Status_Em4ioret_ValueProvider(bool a);
        protected IFlagRegisterField status_em2entered_bit;
        partial void Status_Em2entered_Read(bool a, bool b);
        partial void Status_Em2entered_ValueProvider(bool a);
        protected IFlagRegisterField status_em1p_bit;
        partial void Status_Em1p_Read(bool a, bool b);
        partial void Status_Em1p_ValueProvider(bool a);
        protected IValueRegisterField status_pwrdwnstatus_field;
        partial void Status_Pwrdwnstatus_Read(ulong a, ulong b);
        partial void Status_Pwrdwnstatus_ValueProvider(ulong a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // Temp - Offset : 0x88
        protected IValueRegisterField temp_templsb_field;
        partial void Temp_Templsb_Read(ulong a, ulong b);
        partial void Temp_Templsb_ValueProvider(ulong a);
        protected IValueRegisterField temp_temp_field;
        partial void Temp_Temp_Read(ulong a, ulong b);
        partial void Temp_Temp_ValueProvider(ulong a);
        protected IValueRegisterField temp_tempavg_field;
        partial void Temp_Tempavg_Read(ulong a, ulong b);
        partial void Temp_Tempavg_ValueProvider(ulong a);

        partial void Temp_Write(uint a, uint b);
        partial void Temp_Read(uint a, uint b);
        
        // Testctrl - Offset : 0x8C
        protected IFlagRegisterField testctrl_keepradioinem0_bit;
        partial void Testctrl_Keepradioinem0_Write(bool a, bool b);
        partial void Testctrl_Keepradioinem0_Read(bool a, bool b);
        partial void Testctrl_Keepradioinem0_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_bodmask_bit;
        partial void Testctrl_Bodmask_Write(bool a, bool b);
        partial void Testctrl_Bodmask_Read(bool a, bool b);
        partial void Testctrl_Bodmask_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_regdis_bit;
        partial void Testctrl_Regdis_Write(bool a, bool b);
        partial void Testctrl_Regdis_Read(bool a, bool b);
        partial void Testctrl_Regdis_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_emuoschven_bit;
        partial void Testctrl_Emuoschven_Write(bool a, bool b);
        partial void Testctrl_Emuoschven_Read(bool a, bool b);
        partial void Testctrl_Emuoschven_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_emuosclven_bit;
        partial void Testctrl_Emuosclven_Write(bool a, bool b);
        partial void Testctrl_Emuosclven_Read(bool a, bool b);
        partial void Testctrl_Emuosclven_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_em2entrytimeouten_bit;
        partial void Testctrl_Em2entrytimeouten_Write(bool a, bool b);
        partial void Testctrl_Em2entrytimeouten_Read(bool a, bool b);
        partial void Testctrl_Em2entrytimeouten_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_hvtrimdone_bit;
        partial void Testctrl_Hvtrimdone_Write(bool a, bool b);
        partial void Testctrl_Hvtrimdone_Read(bool a, bool b);
        partial void Testctrl_Hvtrimdone_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_maskexportreset_bit;
        partial void Testctrl_Maskexportreset_Write(bool a, bool b);
        partial void Testctrl_Maskexportreset_Read(bool a, bool b);
        partial void Testctrl_Maskexportreset_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_forceexportreset_bit;
        partial void Testctrl_Forceexportreset_Write(bool a, bool b);
        partial void Testctrl_Forceexportreset_Read(bool a, bool b);
        partial void Testctrl_Forceexportreset_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashpwrswovr_bit;
        partial void Testctrl_Flashpwrswovr_Write(bool a, bool b);
        partial void Testctrl_Flashpwrswovr_Read(bool a, bool b);
        partial void Testctrl_Flashpwrswovr_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashpwrsoftswovr_bit;
        partial void Testctrl_Flashpwrsoftswovr_Write(bool a, bool b);
        partial void Testctrl_Flashpwrsoftswovr_Read(bool a, bool b);
        partial void Testctrl_Flashpwrsoftswovr_ValueProvider(bool a);
        protected IValueRegisterField testctrl_prslvcfg_field;
        partial void Testctrl_Prslvcfg_Write(ulong a, ulong b);
        partial void Testctrl_Prslvcfg_Read(ulong a, ulong b);
        partial void Testctrl_Prslvcfg_ValueProvider(ulong a);
        protected IValueRegisterField testctrl_prshvcfg_field;
        partial void Testctrl_Prshvcfg_Write(ulong a, ulong b);
        partial void Testctrl_Prshvcfg_Read(ulong a, ulong b);
        partial void Testctrl_Prshvcfg_ValueProvider(ulong a);

        partial void Testctrl_Write(uint a, uint b);
        partial void Testctrl_Read(uint a, uint b);
        
        // Rstctrl - Offset : 0x90
        protected IEnumRegisterField<RSTCTRL_WDOG0RMODE> rstctrl_wdog0rmode_bit;
        partial void Rstctrl_Wdog0rmode_Write(RSTCTRL_WDOG0RMODE a, RSTCTRL_WDOG0RMODE b);
        partial void Rstctrl_Wdog0rmode_Read(RSTCTRL_WDOG0RMODE a, RSTCTRL_WDOG0RMODE b);
        partial void Rstctrl_Wdog0rmode_ValueProvider(RSTCTRL_WDOG0RMODE a);
        protected IEnumRegisterField<RSTCTRL_WDOG1RMODE> rstctrl_wdog1rmode_bit;
        partial void Rstctrl_Wdog1rmode_Write(RSTCTRL_WDOG1RMODE a, RSTCTRL_WDOG1RMODE b);
        partial void Rstctrl_Wdog1rmode_Read(RSTCTRL_WDOG1RMODE a, RSTCTRL_WDOG1RMODE b);
        partial void Rstctrl_Wdog1rmode_ValueProvider(RSTCTRL_WDOG1RMODE a);
        protected IEnumRegisterField<RSTCTRL_SYSRMODE> rstctrl_sysrmode_bit;
        partial void Rstctrl_Sysrmode_Write(RSTCTRL_SYSRMODE a, RSTCTRL_SYSRMODE b);
        partial void Rstctrl_Sysrmode_Read(RSTCTRL_SYSRMODE a, RSTCTRL_SYSRMODE b);
        partial void Rstctrl_Sysrmode_ValueProvider(RSTCTRL_SYSRMODE a);
        protected IEnumRegisterField<RSTCTRL_LOCKUPRMODE> rstctrl_lockuprmode_bit;
        partial void Rstctrl_Lockuprmode_Write(RSTCTRL_LOCKUPRMODE a, RSTCTRL_LOCKUPRMODE b);
        partial void Rstctrl_Lockuprmode_Read(RSTCTRL_LOCKUPRMODE a, RSTCTRL_LOCKUPRMODE b);
        partial void Rstctrl_Lockuprmode_ValueProvider(RSTCTRL_LOCKUPRMODE a);
        protected IEnumRegisterField<RSTCTRL_AVDDBODRMODE> rstctrl_avddbodrmode_bit;
        partial void Rstctrl_Avddbodrmode_Write(RSTCTRL_AVDDBODRMODE a, RSTCTRL_AVDDBODRMODE b);
        partial void Rstctrl_Avddbodrmode_Read(RSTCTRL_AVDDBODRMODE a, RSTCTRL_AVDDBODRMODE b);
        partial void Rstctrl_Avddbodrmode_ValueProvider(RSTCTRL_AVDDBODRMODE a);
        protected IEnumRegisterField<RSTCTRL_IOVDD0BODRMODE> rstctrl_iovdd0bodrmode_bit;
        partial void Rstctrl_Iovdd0bodrmode_Write(RSTCTRL_IOVDD0BODRMODE a, RSTCTRL_IOVDD0BODRMODE b);
        partial void Rstctrl_Iovdd0bodrmode_Read(RSTCTRL_IOVDD0BODRMODE a, RSTCTRL_IOVDD0BODRMODE b);
        partial void Rstctrl_Iovdd0bodrmode_ValueProvider(RSTCTRL_IOVDD0BODRMODE a);
        protected IEnumRegisterField<RSTCTRL_IOVDD1BODRMODE> rstctrl_iovdd1bodrmode_bit;
        partial void Rstctrl_Iovdd1bodrmode_Write(RSTCTRL_IOVDD1BODRMODE a, RSTCTRL_IOVDD1BODRMODE b);
        partial void Rstctrl_Iovdd1bodrmode_Read(RSTCTRL_IOVDD1BODRMODE a, RSTCTRL_IOVDD1BODRMODE b);
        partial void Rstctrl_Iovdd1bodrmode_ValueProvider(RSTCTRL_IOVDD1BODRMODE a);
        protected IEnumRegisterField<RSTCTRL_DECBODRMODE> rstctrl_decbodrmode_bit;
        partial void Rstctrl_Decbodrmode_Write(RSTCTRL_DECBODRMODE a, RSTCTRL_DECBODRMODE b);
        partial void Rstctrl_Decbodrmode_Read(RSTCTRL_DECBODRMODE a, RSTCTRL_DECBODRMODE b);
        partial void Rstctrl_Decbodrmode_ValueProvider(RSTCTRL_DECBODRMODE a);
        protected IEnumRegisterField<RSTCTRL_M0SYSRMODE> rstctrl_m0sysrmode_bit;
        partial void Rstctrl_M0sysrmode_Write(RSTCTRL_M0SYSRMODE a, RSTCTRL_M0SYSRMODE b);
        partial void Rstctrl_M0sysrmode_Read(RSTCTRL_M0SYSRMODE a, RSTCTRL_M0SYSRMODE b);
        partial void Rstctrl_M0sysrmode_ValueProvider(RSTCTRL_M0SYSRMODE a);
        protected IEnumRegisterField<RSTCTRL_M0LOCKUPRMODE> rstctrl_m0lockuprmode_bit;
        partial void Rstctrl_M0lockuprmode_Write(RSTCTRL_M0LOCKUPRMODE a, RSTCTRL_M0LOCKUPRMODE b);
        partial void Rstctrl_M0lockuprmode_Read(RSTCTRL_M0LOCKUPRMODE a, RSTCTRL_M0LOCKUPRMODE b);
        partial void Rstctrl_M0lockuprmode_ValueProvider(RSTCTRL_M0LOCKUPRMODE a);
        protected IEnumRegisterField<RSTCTRL_SEM0SYSRMODE> rstctrl_sem0sysrmode_bit;
        partial void Rstctrl_Sem0sysrmode_Write(RSTCTRL_SEM0SYSRMODE a, RSTCTRL_SEM0SYSRMODE b);
        partial void Rstctrl_Sem0sysrmode_Read(RSTCTRL_SEM0SYSRMODE a, RSTCTRL_SEM0SYSRMODE b);
        partial void Rstctrl_Sem0sysrmode_ValueProvider(RSTCTRL_SEM0SYSRMODE a);
        protected IEnumRegisterField<RSTCTRL_SEM0LOCKUPRMODE> rstctrl_sem0lockuprmode_bit;
        partial void Rstctrl_Sem0lockuprmode_Write(RSTCTRL_SEM0LOCKUPRMODE a, RSTCTRL_SEM0LOCKUPRMODE b);
        partial void Rstctrl_Sem0lockuprmode_Read(RSTCTRL_SEM0LOCKUPRMODE a, RSTCTRL_SEM0LOCKUPRMODE b);
        partial void Rstctrl_Sem0lockuprmode_ValueProvider(RSTCTRL_SEM0LOCKUPRMODE a);
        protected IEnumRegisterField<RSTCTRL_PRSSYSRSTDLY> rstctrl_prssysrstdly_field;
        partial void Rstctrl_Prssysrstdly_Write(RSTCTRL_PRSSYSRSTDLY a, RSTCTRL_PRSSYSRSTDLY b);
        partial void Rstctrl_Prssysrstdly_Read(RSTCTRL_PRSSYSRSTDLY a, RSTCTRL_PRSSYSRSTDLY b);
        partial void Rstctrl_Prssysrstdly_ValueProvider(RSTCTRL_PRSSYSRSTDLY a);

        partial void Rstctrl_Write(uint a, uint b);
        partial void Rstctrl_Read(uint a, uint b);
        
        // Rstcause - Offset : 0x94
        protected IFlagRegisterField rstcause_por_bit;
        partial void Rstcause_Por_Read(bool a, bool b);
        partial void Rstcause_Por_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_pin_bit;
        partial void Rstcause_Pin_Read(bool a, bool b);
        partial void Rstcause_Pin_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_em4_bit;
        partial void Rstcause_Em4_Read(bool a, bool b);
        partial void Rstcause_Em4_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_wdog0_bit;
        partial void Rstcause_Wdog0_Read(bool a, bool b);
        partial void Rstcause_Wdog0_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_wdog1_bit;
        partial void Rstcause_Wdog1_Read(bool a, bool b);
        partial void Rstcause_Wdog1_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_lockup_bit;
        partial void Rstcause_Lockup_Read(bool a, bool b);
        partial void Rstcause_Lockup_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_sysreq_bit;
        partial void Rstcause_Sysreq_Read(bool a, bool b);
        partial void Rstcause_Sysreq_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_dvddbod_bit;
        partial void Rstcause_Dvddbod_Read(bool a, bool b);
        partial void Rstcause_Dvddbod_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_dvddlebod_bit;
        partial void Rstcause_Dvddlebod_Read(bool a, bool b);
        partial void Rstcause_Dvddlebod_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_decbod_bit;
        partial void Rstcause_Decbod_Read(bool a, bool b);
        partial void Rstcause_Decbod_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_avddbod_bit;
        partial void Rstcause_Avddbod_Read(bool a, bool b);
        partial void Rstcause_Avddbod_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_iovdd0bod_bit;
        partial void Rstcause_Iovdd0bod_Read(bool a, bool b);
        partial void Rstcause_Iovdd0bod_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_iovdd1bod_bit;
        partial void Rstcause_Iovdd1bod_Read(bool a, bool b);
        partial void Rstcause_Iovdd1bod_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_tamper_bit;
        partial void Rstcause_Tamper_Read(bool a, bool b);
        partial void Rstcause_Tamper_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_m0sysreq_bit;
        partial void Rstcause_M0sysreq_Read(bool a, bool b);
        partial void Rstcause_M0sysreq_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_m0lockup_bit;
        partial void Rstcause_M0lockup_Read(bool a, bool b);
        partial void Rstcause_M0lockup_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_sem0sysreq_bit;
        partial void Rstcause_Sem0sysreq_Read(bool a, bool b);
        partial void Rstcause_Sem0sysreq_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_sem0lockup_bit;
        partial void Rstcause_Sem0lockup_Read(bool a, bool b);
        partial void Rstcause_Sem0lockup_ValueProvider(bool a);
        protected IFlagRegisterField rstcause_vregin_bit;
        partial void Rstcause_Vregin_Read(bool a, bool b);
        partial void Rstcause_Vregin_ValueProvider(bool a);

        partial void Rstcause_Write(uint a, uint b);
        partial void Rstcause_Read(uint a, uint b);
        
        // Tamperrstcause - Offset : 0x98
        protected IValueRegisterField tamperrstcause_tamperrst_field;
        partial void Tamperrstcause_Tamperrst_Read(ulong a, ulong b);
        partial void Tamperrstcause_Tamperrst_ValueProvider(ulong a);

        partial void Tamperrstcause_Write(uint a, uint b);
        partial void Tamperrstcause_Read(uint a, uint b);
        
        // Dgif - Offset : 0xA0
        protected IFlagRegisterField dgif_em23wakeupdgif_bit;
        partial void Dgif_Em23wakeupdgif_Write(bool a, bool b);
        partial void Dgif_Em23wakeupdgif_Read(bool a, bool b);
        partial void Dgif_Em23wakeupdgif_ValueProvider(bool a);
        protected IFlagRegisterField dgif_tempdgif_bit;
        partial void Dgif_Tempdgif_Write(bool a, bool b);
        partial void Dgif_Tempdgif_Read(bool a, bool b);
        partial void Dgif_Tempdgif_ValueProvider(bool a);
        protected IFlagRegisterField dgif_templowdgif_bit;
        partial void Dgif_Templowdgif_Write(bool a, bool b);
        partial void Dgif_Templowdgif_Read(bool a, bool b);
        partial void Dgif_Templowdgif_ValueProvider(bool a);
        protected IFlagRegisterField dgif_temphighdgif_bit;
        partial void Dgif_Temphighdgif_Write(bool a, bool b);
        partial void Dgif_Temphighdgif_Read(bool a, bool b);
        partial void Dgif_Temphighdgif_ValueProvider(bool a);

        partial void Dgif_Write(uint a, uint b);
        partial void Dgif_Read(uint a, uint b);
        
        // Dgien - Offset : 0xA4
        protected IFlagRegisterField dgien_em23wakeupdgien_bit;
        partial void Dgien_Em23wakeupdgien_Write(bool a, bool b);
        partial void Dgien_Em23wakeupdgien_Read(bool a, bool b);
        partial void Dgien_Em23wakeupdgien_ValueProvider(bool a);
        protected IFlagRegisterField dgien_tempdgien_bit;
        partial void Dgien_Tempdgien_Write(bool a, bool b);
        partial void Dgien_Tempdgien_Read(bool a, bool b);
        partial void Dgien_Tempdgien_ValueProvider(bool a);
        protected IFlagRegisterField dgien_templowdgien_bit;
        partial void Dgien_Templowdgien_Write(bool a, bool b);
        partial void Dgien_Templowdgien_Read(bool a, bool b);
        partial void Dgien_Templowdgien_ValueProvider(bool a);
        protected IFlagRegisterField dgien_temphighdgien_bit;
        partial void Dgien_Temphighdgien_Write(bool a, bool b);
        partial void Dgien_Temphighdgien_Read(bool a, bool b);
        partial void Dgien_Temphighdgien_ValueProvider(bool a);

        partial void Dgien_Write(uint a, uint b);
        partial void Dgien_Read(uint a, uint b);
        
        // Seqif - Offset : 0xA8
        protected IFlagRegisterField seqif_temp_bit;
        partial void Seqif_Temp_Write(bool a, bool b);
        partial void Seqif_Temp_Read(bool a, bool b);
        partial void Seqif_Temp_ValueProvider(bool a);
        protected IFlagRegisterField seqif_templow_bit;
        partial void Seqif_Templow_Write(bool a, bool b);
        partial void Seqif_Templow_Read(bool a, bool b);
        partial void Seqif_Templow_ValueProvider(bool a);
        protected IFlagRegisterField seqif_temphigh_bit;
        partial void Seqif_Temphigh_Write(bool a, bool b);
        partial void Seqif_Temphigh_Read(bool a, bool b);
        partial void Seqif_Temphigh_ValueProvider(bool a);

        partial void Seqif_Write(uint a, uint b);
        partial void Seqif_Read(uint a, uint b);
        
        // Seqien - Offset : 0xAC
        protected IFlagRegisterField seqien_temp_bit;
        partial void Seqien_Temp_Write(bool a, bool b);
        partial void Seqien_Temp_Read(bool a, bool b);
        partial void Seqien_Temp_ValueProvider(bool a);
        protected IFlagRegisterField seqien_templow_bit;
        partial void Seqien_Templow_Write(bool a, bool b);
        partial void Seqien_Templow_Read(bool a, bool b);
        partial void Seqien_Templow_ValueProvider(bool a);
        protected IFlagRegisterField seqien_temphigh_bit;
        partial void Seqien_Temphigh_Write(bool a, bool b);
        partial void Seqien_Temphigh_Read(bool a, bool b);
        partial void Seqien_Temphigh_ValueProvider(bool a);

        partial void Seqien_Write(uint a, uint b);
        partial void Seqien_Read(uint a, uint b);
        
        // Delaycfg - Offset : 0xB0
        protected IValueRegisterField delaycfg_vscalestepupwait_field;
        partial void Delaycfg_Vscalestepupwait_Write(ulong a, ulong b);
        partial void Delaycfg_Vscalestepupwait_Read(ulong a, ulong b);
        partial void Delaycfg_Vscalestepupwait_ValueProvider(ulong a);
        protected IValueRegisterField delaycfg_vscalestepdnwait_field;
        partial void Delaycfg_Vscalestepdnwait_Write(ulong a, ulong b);
        partial void Delaycfg_Vscalestepdnwait_Read(ulong a, ulong b);
        partial void Delaycfg_Vscalestepdnwait_ValueProvider(ulong a);
        protected IValueRegisterField delaycfg_retaindly_field;
        partial void Delaycfg_Retaindly_Write(ulong a, ulong b);
        partial void Delaycfg_Retaindly_Read(ulong a, ulong b);
        partial void Delaycfg_Retaindly_ValueProvider(ulong a);
        protected IValueRegisterField delaycfg_isodly_field;
        partial void Delaycfg_Isodly_Write(ulong a, ulong b);
        partial void Delaycfg_Isodly_Read(ulong a, ulong b);
        partial void Delaycfg_Isodly_ValueProvider(ulong a);

        partial void Delaycfg_Write(uint a, uint b);
        partial void Delaycfg_Read(uint a, uint b);
        
        // Testlock - Offset : 0xB4
        protected IValueRegisterField testlock_lockkey_field;
        partial void Testlock_Lockkey_Write(ulong a, ulong b);
        partial void Testlock_Lockkey_ValueProvider(ulong a);

        partial void Testlock_Write(uint a, uint b);
        partial void Testlock_Read(uint a, uint b);
        
        // Auxctrl - Offset : 0xB8
        protected IValueRegisterField auxctrl_aux0_field;
        partial void Auxctrl_Aux0_Write(ulong a, ulong b);
        partial void Auxctrl_Aux0_Read(ulong a, ulong b);
        partial void Auxctrl_Aux0_ValueProvider(ulong a);
        protected IValueRegisterField auxctrl_aux1_field;
        partial void Auxctrl_Aux1_Write(ulong a, ulong b);
        partial void Auxctrl_Aux1_Read(ulong a, ulong b);
        partial void Auxctrl_Aux1_ValueProvider(ulong a);
        protected IValueRegisterField auxctrl_aux2_field;
        partial void Auxctrl_Aux2_Write(ulong a, ulong b);
        partial void Auxctrl_Aux2_Read(ulong a, ulong b);
        partial void Auxctrl_Aux2_ValueProvider(ulong a);
        protected IValueRegisterField auxctrl_aux3_field;
        partial void Auxctrl_Aux3_Write(ulong a, ulong b);
        partial void Auxctrl_Aux3_Read(ulong a, ulong b);
        partial void Auxctrl_Aux3_ValueProvider(ulong a);

        partial void Auxctrl_Write(uint a, uint b);
        partial void Auxctrl_Read(uint a, uint b);
        
        // Isbiasconf_Isbiasctrl - Offset : 0xC0
        protected IFlagRegisterField isbiasconf_isbiasctrl_isbiasctrlen_bit;
        partial void Isbiasconf_Isbiasctrl_Isbiasctrlen_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Isbiasctrlen_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Isbiasctrlen_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forcecalreq_bit;
        partial void Isbiasconf_Isbiasctrl_Forcecalreq_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcecalreq_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcecalreq_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forcetempreq_bit;
        partial void Isbiasconf_Isbiasctrl_Forcetempreq_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcetempreq_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcetempreq_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_caldis_bit;
        partial void Isbiasconf_Isbiasctrl_Caldis_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Caldis_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Caldis_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_tempcompdis_bit;
        partial void Isbiasconf_Isbiasctrl_Tempcompdis_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Tempcompdis_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Tempcompdis_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_tempdis_bit;
        partial void Isbiasconf_Isbiasctrl_Tempdis_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Tempdis_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Tempdis_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forcecont_bit;
        partial void Isbiasconf_Isbiasctrl_Forcecont_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcecont_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcecont_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forceduty_bit;
        partial void Isbiasconf_Isbiasctrl_Forceduty_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forceduty_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forceduty_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forcebiasosc_bit;
        partial void Isbiasconf_Isbiasctrl_Forcebiasosc_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcebiasosc_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcebiasosc_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forceemuosc_bit;
        partial void Isbiasconf_Isbiasctrl_Forceemuosc_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forceemuosc_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forceemuosc_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_caldlydbl_bit;
        partial void Isbiasconf_Isbiasctrl_Caldlydbl_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Caldlydbl_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Caldlydbl_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_isbiasregen_bit;
        partial void Isbiasconf_Isbiasctrl_Isbiasregen_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Isbiasregen_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Isbiasregen_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forcerefreshrate_bit;
        partial void Isbiasconf_Isbiasctrl_Forcerefreshrate_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcerefreshrate_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcerefreshrate_ValueProvider(bool a);
        protected IFlagRegisterField isbiasconf_isbiasctrl_forcebgcont_bit;
        partial void Isbiasconf_Isbiasctrl_Forcebgcont_Write(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcebgcont_Read(bool a, bool b);
        partial void Isbiasconf_Isbiasctrl_Forcebgcont_ValueProvider(bool a);
        protected IEnumRegisterField<ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL> isbiasconf_isbiasctrl_isbiasoutsel_field;
        partial void Isbiasconf_Isbiasctrl_Isbiasoutsel_Write(ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL a, ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL b);
        partial void Isbiasconf_Isbiasctrl_Isbiasoutsel_Read(ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL a, ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL b);
        partial void Isbiasconf_Isbiasctrl_Isbiasoutsel_ValueProvider(ISBIASCONF_ISBIASCTRL_ISBIASOUTSEL a);

        partial void Isbiasconf_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiasconf_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiascalovr_Isbiasctrl - Offset : 0xC4
        protected IFlagRegisterField isbiascalovr_isbiasctrl_calovr_bit;
        partial void Isbiascalovr_Isbiasctrl_Calovr_Write(bool a, bool b);
        partial void Isbiascalovr_Isbiasctrl_Calovr_Read(bool a, bool b);
        partial void Isbiascalovr_Isbiasctrl_Calovr_ValueProvider(bool a);
        protected IValueRegisterField isbiascalovr_isbiasctrl_calovrvalue_field;
        partial void Isbiascalovr_Isbiasctrl_Calovrvalue_Write(ulong a, ulong b);
        partial void Isbiascalovr_Isbiasctrl_Calovrvalue_Read(ulong a, ulong b);
        partial void Isbiascalovr_Isbiasctrl_Calovrvalue_ValueProvider(ulong a);

        partial void Isbiascalovr_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiascalovr_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiasperiod_Isbiasctrl - Offset : 0xC8
        protected IEnumRegisterField<ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD> isbiasperiod_isbiasctrl_tempperiod_field;
        partial void Isbiasperiod_Isbiasctrl_Tempperiod_Write(ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD a, ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD b);
        partial void Isbiasperiod_Isbiasctrl_Tempperiod_Read(ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD a, ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD b);
        partial void Isbiasperiod_Isbiasctrl_Tempperiod_ValueProvider(ISBIASPERIOD_ISBIASCTRL_TEMPPERIOD a);
        protected IEnumRegisterField<ISBIASPERIOD_ISBIASCTRL_CALPERIOD> isbiasperiod_isbiasctrl_calperiod_field;
        partial void Isbiasperiod_Isbiasctrl_Calperiod_Write(ISBIASPERIOD_ISBIASCTRL_CALPERIOD a, ISBIASPERIOD_ISBIASCTRL_CALPERIOD b);
        partial void Isbiasperiod_Isbiasctrl_Calperiod_Read(ISBIASPERIOD_ISBIASCTRL_CALPERIOD a, ISBIASPERIOD_ISBIASCTRL_CALPERIOD b);
        partial void Isbiasperiod_Isbiasctrl_Calperiod_ValueProvider(ISBIASPERIOD_ISBIASCTRL_CALPERIOD a);
        protected IFlagRegisterField isbiasperiod_isbiasctrl_em2periodx4_bit;
        partial void Isbiasperiod_Isbiasctrl_Em2periodx4_Write(bool a, bool b);
        partial void Isbiasperiod_Isbiasctrl_Em2periodx4_Read(bool a, bool b);
        partial void Isbiasperiod_Isbiasctrl_Em2periodx4_ValueProvider(bool a);

        partial void Isbiasperiod_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiasperiod_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiastempcomprate_Isbiasctrl - Offset : 0xCC
        protected IEnumRegisterField<ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE> isbiastempcomprate_isbiasctrl_r0refreshrate_field;
        partial void Isbiastempcomprate_Isbiasctrl_R0refreshrate_Write(ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE a, ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE b);
        partial void Isbiastempcomprate_Isbiasctrl_R0refreshrate_Read(ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE a, ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE b);
        partial void Isbiastempcomprate_Isbiasctrl_R0refreshrate_ValueProvider(ISBIASTEMPCOMPRATE_ISBIASCTRL_R0REFRESHRATE a);
        protected IValueRegisterField isbiastempcomprate_isbiasctrl_r1refreshrate_field;
        partial void Isbiastempcomprate_Isbiasctrl_R1refreshrate_Write(ulong a, ulong b);
        partial void Isbiastempcomprate_Isbiasctrl_R1refreshrate_Read(ulong a, ulong b);
        partial void Isbiastempcomprate_Isbiasctrl_R1refreshrate_ValueProvider(ulong a);
        protected IValueRegisterField isbiastempcomprate_isbiasctrl_r2refreshrate_field;
        partial void Isbiastempcomprate_Isbiasctrl_R2refreshrate_Write(ulong a, ulong b);
        partial void Isbiastempcomprate_Isbiasctrl_R2refreshrate_Read(ulong a, ulong b);
        partial void Isbiastempcomprate_Isbiasctrl_R2refreshrate_ValueProvider(ulong a);
        protected IValueRegisterField isbiastempcomprate_isbiasctrl_r3refreshrate_field;
        partial void Isbiastempcomprate_Isbiasctrl_R3refreshrate_Write(ulong a, ulong b);
        partial void Isbiastempcomprate_Isbiasctrl_R3refreshrate_Read(ulong a, ulong b);
        partial void Isbiastempcomprate_Isbiasctrl_R3refreshrate_ValueProvider(ulong a);

        partial void Isbiastempcomprate_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiastempcomprate_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiastempcompthr_Isbiasctrl - Offset : 0xD0
        protected IValueRegisterField isbiastempcompthr_isbiasctrl_r1tempthr_field;
        partial void Isbiastempcompthr_Isbiasctrl_R1tempthr_Write(ulong a, ulong b);
        partial void Isbiastempcompthr_Isbiasctrl_R1tempthr_Read(ulong a, ulong b);
        partial void Isbiastempcompthr_Isbiasctrl_R1tempthr_ValueProvider(ulong a);
        protected IValueRegisterField isbiastempcompthr_isbiasctrl_r2tempthr_field;
        partial void Isbiastempcompthr_Isbiasctrl_R2tempthr_Write(ulong a, ulong b);
        partial void Isbiastempcompthr_Isbiasctrl_R2tempthr_Read(ulong a, ulong b);
        partial void Isbiastempcompthr_Isbiasctrl_R2tempthr_ValueProvider(ulong a);
        protected IValueRegisterField isbiastempcompthr_isbiasctrl_r3tempthr_field;
        partial void Isbiastempcompthr_Isbiasctrl_R3tempthr_Write(ulong a, ulong b);
        partial void Isbiastempcompthr_Isbiasctrl_R3tempthr_Read(ulong a, ulong b);
        partial void Isbiastempcompthr_Isbiasctrl_R3tempthr_ValueProvider(ulong a);

        partial void Isbiastempcompthr_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiastempcompthr_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiaspfmrefreshcfg_Isbiasctrl - Offset : 0xD8
        protected IEnumRegisterField<ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT> isbiaspfmrefreshcfg_isbiasctrl_s2fastrfshcnt_field;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfshcnt_Write(ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT a, ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfshcnt_Read(ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT a, ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfshcnt_ValueProvider(ISBIASPFMREFRESHCFG_ISBIASCTRL_S2FASTRFSHCNT a);
        protected IValueRegisterField isbiaspfmrefreshcfg_isbiasctrl_s2fastrfrshsmpduration_field;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshsmpduration_Write(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshsmpduration_Read(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshsmpduration_ValueProvider(ulong a);
        protected IEnumRegisterField<ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO> isbiaspfmrefreshcfg_isbiasctrl_s2prepdivratio_field;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2prepdivratio_Write(ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO a, ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2prepdivratio_Read(ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO a, ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2prepdivratio_ValueProvider(ISBIASPFMREFRESHCFG_ISBIASCTRL_S2PREPDIVRATIO a);
        protected IFlagRegisterField isbiaspfmrefreshcfg_isbiasctrl_s2roomtempdiv2_bit;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2roomtempdiv2_Write(bool a, bool b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2roomtempdiv2_Read(bool a, bool b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2roomtempdiv2_ValueProvider(bool a);
        protected IFlagRegisterField isbiaspfmrefreshcfg_isbiasctrl_s2fastrfrshem2entryctrl_bit;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshem2entryctrl_Write(bool a, bool b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshem2entryctrl_Read(bool a, bool b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2fastrfrshem2entryctrl_ValueProvider(bool a);
        protected IValueRegisterField isbiaspfmrefreshcfg_isbiasctrl_s2delta_field;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2delta_Write(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2delta_Read(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2delta_ValueProvider(ulong a);
        protected IValueRegisterField isbiaspfmrefreshcfg_isbiasctrl_s2smpduration_field;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2smpduration_Write(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2smpduration_Read(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2smpduration_ValueProvider(ulong a);
        protected IValueRegisterField isbiaspfmrefreshcfg_isbiasctrl_s2divratio_field;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2divratio_Write(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2divratio_Read(ulong a, ulong b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_S2divratio_ValueProvider(ulong a);
        protected IFlagRegisterField isbiaspfmrefreshcfg_isbiasctrl_pfmem2wuwaitimax_bit;
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_Pfmem2wuwaitimax_Write(bool a, bool b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_Pfmem2wuwaitimax_Read(bool a, bool b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_Pfmem2wuwaitimax_ValueProvider(bool a);

        partial void Isbiaspfmrefreshcfg_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiaspfmrefreshcfg_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiasrefreshcfg_Isbiasctrl - Offset : 0xDC
        protected IValueRegisterField isbiasrefreshcfg_isbiasctrl_s0delta_field;
        partial void Isbiasrefreshcfg_Isbiasctrl_S0delta_Write(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S0delta_Read(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S0delta_ValueProvider(ulong a);
        protected IValueRegisterField isbiasrefreshcfg_isbiasctrl_s0smpduration_field;
        partial void Isbiasrefreshcfg_Isbiasctrl_S0smpduration_Write(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S0smpduration_Read(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S0smpduration_ValueProvider(ulong a);
        protected IEnumRegisterField<ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO> isbiasrefreshcfg_isbiasctrl_s0divratio_field;
        partial void Isbiasrefreshcfg_Isbiasctrl_S0divratio_Write(ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO a, ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S0divratio_Read(ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO a, ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S0divratio_ValueProvider(ISBIASREFRESHCFG_ISBIASCTRL_S0DIVRATIO a);
        protected IValueRegisterField isbiasrefreshcfg_isbiasctrl_s1delta_field;
        partial void Isbiasrefreshcfg_Isbiasctrl_S1delta_Write(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1delta_Read(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1delta_ValueProvider(ulong a);
        protected IValueRegisterField isbiasrefreshcfg_isbiasctrl_s1smpduration_field;
        partial void Isbiasrefreshcfg_Isbiasctrl_S1smpduration_Write(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1smpduration_Read(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1smpduration_ValueProvider(ulong a);
        protected IValueRegisterField isbiasrefreshcfg_isbiasctrl_s1divratio_field;
        partial void Isbiasrefreshcfg_Isbiasctrl_S1divratio_Write(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1divratio_Read(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1divratio_ValueProvider(ulong a);
        protected IValueRegisterField isbiasrefreshcfg_isbiasctrl_s1temprangecont_field;
        partial void Isbiasrefreshcfg_Isbiasctrl_S1temprangecont_Write(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1temprangecont_Read(ulong a, ulong b);
        partial void Isbiasrefreshcfg_Isbiasctrl_S1temprangecont_ValueProvider(ulong a);

        partial void Isbiasrefreshcfg_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiasrefreshcfg_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiastempconst_Isbiasctrl - Offset : 0xE0
        protected IValueRegisterField isbiastempconst_isbiasctrl_tempcalcconst_field;
        partial void Isbiastempconst_Isbiasctrl_Tempcalcconst_Write(ulong a, ulong b);
        partial void Isbiastempconst_Isbiasctrl_Tempcalcconst_Read(ulong a, ulong b);
        partial void Isbiastempconst_Isbiasctrl_Tempcalcconst_ValueProvider(ulong a);

        partial void Isbiastempconst_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiastempconst_Isbiasctrl_Read(uint a, uint b);
        
        // Isbiasstatus_Isbiasctrl - Offset : 0xE4
        protected IValueRegisterField isbiasstatus_isbiasctrl_isbiasout_field;
        partial void Isbiasstatus_Isbiasctrl_Isbiasout_Read(ulong a, ulong b);
        partial void Isbiasstatus_Isbiasctrl_Isbiasout_ValueProvider(ulong a);
        protected IFlagRegisterField isbiasstatus_isbiasctrl_firstcaldone_bit;
        partial void Isbiasstatus_Isbiasctrl_Firstcaldone_Read(bool a, bool b);
        partial void Isbiasstatus_Isbiasctrl_Firstcaldone_ValueProvider(bool a);
        protected IFlagRegisterField isbiasstatus_isbiasctrl_isbiascalactive_bit;
        partial void Isbiasstatus_Isbiasctrl_Isbiascalactive_Read(bool a, bool b);
        partial void Isbiasstatus_Isbiasctrl_Isbiascalactive_ValueProvider(bool a);
        protected IFlagRegisterField isbiasstatus_isbiasctrl_tempcompactive_bit;
        partial void Isbiasstatus_Isbiasctrl_Tempcompactive_Read(bool a, bool b);
        partial void Isbiasstatus_Isbiasctrl_Tempcompactive_ValueProvider(bool a);
        protected IFlagRegisterField isbiasstatus_isbiasctrl_isbiascalcompout_bit;
        partial void Isbiasstatus_Isbiasctrl_Isbiascalcompout_Read(bool a, bool b);
        partial void Isbiasstatus_Isbiasctrl_Isbiascalcompout_ValueProvider(bool a);
        protected IValueRegisterField isbiasstatus_isbiasctrl_vsbtemprange_field;
        partial void Isbiasstatus_Isbiasctrl_Vsbtemprange_Read(ulong a, ulong b);
        partial void Isbiasstatus_Isbiasctrl_Vsbtemprange_ValueProvider(ulong a);
        protected IValueRegisterField isbiasstatus_isbiasctrl_isbiasrefreshrate_field;
        partial void Isbiasstatus_Isbiasctrl_Isbiasrefreshrate_Read(ulong a, ulong b);
        partial void Isbiasstatus_Isbiasctrl_Isbiasrefreshrate_ValueProvider(ulong a);
        protected IEnumRegisterField<ISBIASSTATUS_ISBIASCTRL_TESTLOCK> isbiasstatus_isbiasctrl_testlock_bit;
        partial void Isbiasstatus_Isbiasctrl_Testlock_Read(ISBIASSTATUS_ISBIASCTRL_TESTLOCK a, ISBIASSTATUS_ISBIASCTRL_TESTLOCK b);
        partial void Isbiasstatus_Isbiasctrl_Testlock_ValueProvider(ISBIASSTATUS_ISBIASCTRL_TESTLOCK a);

        partial void Isbiasstatus_Isbiasctrl_Write(uint a, uint b);
        partial void Isbiasstatus_Isbiasctrl_Read(uint a, uint b);
        
        // Vsbtempcomp_Isbiasctrl - Offset : 0xE8
        protected IFlagRegisterField vsbtempcomp_isbiasctrl_vsbtempcompen_bit;
        partial void Vsbtempcomp_Isbiasctrl_Vsbtempcompen_Write(bool a, bool b);
        partial void Vsbtempcomp_Isbiasctrl_Vsbtempcompen_Read(bool a, bool b);
        partial void Vsbtempcomp_Isbiasctrl_Vsbtempcompen_ValueProvider(bool a);
        protected IEnumRegisterField<VSBTEMPCOMP_ISBIASCTRL_R0VSB> vsbtempcomp_isbiasctrl_r0vsb_field;
        partial void Vsbtempcomp_Isbiasctrl_R0vsb_Write(VSBTEMPCOMP_ISBIASCTRL_R0VSB a, VSBTEMPCOMP_ISBIASCTRL_R0VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R0vsb_Read(VSBTEMPCOMP_ISBIASCTRL_R0VSB a, VSBTEMPCOMP_ISBIASCTRL_R0VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R0vsb_ValueProvider(VSBTEMPCOMP_ISBIASCTRL_R0VSB a);
        protected IEnumRegisterField<VSBTEMPCOMP_ISBIASCTRL_R1VSB> vsbtempcomp_isbiasctrl_r1vsb_field;
        partial void Vsbtempcomp_Isbiasctrl_R1vsb_Write(VSBTEMPCOMP_ISBIASCTRL_R1VSB a, VSBTEMPCOMP_ISBIASCTRL_R1VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R1vsb_Read(VSBTEMPCOMP_ISBIASCTRL_R1VSB a, VSBTEMPCOMP_ISBIASCTRL_R1VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R1vsb_ValueProvider(VSBTEMPCOMP_ISBIASCTRL_R1VSB a);
        protected IEnumRegisterField<VSBTEMPCOMP_ISBIASCTRL_R2VSB> vsbtempcomp_isbiasctrl_r2vsb_field;
        partial void Vsbtempcomp_Isbiasctrl_R2vsb_Write(VSBTEMPCOMP_ISBIASCTRL_R2VSB a, VSBTEMPCOMP_ISBIASCTRL_R2VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R2vsb_Read(VSBTEMPCOMP_ISBIASCTRL_R2VSB a, VSBTEMPCOMP_ISBIASCTRL_R2VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R2vsb_ValueProvider(VSBTEMPCOMP_ISBIASCTRL_R2VSB a);
        protected IEnumRegisterField<VSBTEMPCOMP_ISBIASCTRL_R3VSB> vsbtempcomp_isbiasctrl_r3vsb_field;
        partial void Vsbtempcomp_Isbiasctrl_R3vsb_Write(VSBTEMPCOMP_ISBIASCTRL_R3VSB a, VSBTEMPCOMP_ISBIASCTRL_R3VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R3vsb_Read(VSBTEMPCOMP_ISBIASCTRL_R3VSB a, VSBTEMPCOMP_ISBIASCTRL_R3VSB b);
        partial void Vsbtempcomp_Isbiasctrl_R3vsb_ValueProvider(VSBTEMPCOMP_ISBIASCTRL_R3VSB a);
        protected IFlagRegisterField vsbtempcomp_isbiasctrl_vsbtestmodeen_bit;
        partial void Vsbtempcomp_Isbiasctrl_Vsbtestmodeen_Write(bool a, bool b);
        partial void Vsbtempcomp_Isbiasctrl_Vsbtestmodeen_Read(bool a, bool b);
        partial void Vsbtempcomp_Isbiasctrl_Vsbtestmodeen_ValueProvider(bool a);

        partial void Vsbtempcomp_Isbiasctrl_Write(uint a, uint b);
        partial void Vsbtempcomp_Isbiasctrl_Read(uint a, uint b);
        
        // Vsbtempcompthr_Isbiasctrl - Offset : 0xEC
        protected IValueRegisterField vsbtempcompthr_isbiasctrl_r1vsbtempthr_field;
        partial void Vsbtempcompthr_Isbiasctrl_R1vsbtempthr_Write(ulong a, ulong b);
        partial void Vsbtempcompthr_Isbiasctrl_R1vsbtempthr_Read(ulong a, ulong b);
        partial void Vsbtempcompthr_Isbiasctrl_R1vsbtempthr_ValueProvider(ulong a);
        protected IValueRegisterField vsbtempcompthr_isbiasctrl_r2vsbtempthr_field;
        partial void Vsbtempcompthr_Isbiasctrl_R2vsbtempthr_Write(ulong a, ulong b);
        partial void Vsbtempcompthr_Isbiasctrl_R2vsbtempthr_Read(ulong a, ulong b);
        partial void Vsbtempcompthr_Isbiasctrl_R2vsbtempthr_ValueProvider(ulong a);
        protected IValueRegisterField vsbtempcompthr_isbiasctrl_r3vsbtempthr_field;
        partial void Vsbtempcompthr_Isbiasctrl_R3vsbtempthr_Write(ulong a, ulong b);
        partial void Vsbtempcompthr_Isbiasctrl_R3vsbtempthr_Read(ulong a, ulong b);
        partial void Vsbtempcompthr_Isbiasctrl_R3vsbtempthr_ValueProvider(ulong a);

        partial void Vsbtempcompthr_Isbiasctrl_Write(uint a, uint b);
        partial void Vsbtempcompthr_Isbiasctrl_Read(uint a, uint b);
        
        // Retregtempcomp_Isbiasctrl - Offset : 0xF0
        protected IValueRegisterField retregtempcomp_isbiasctrl_r0retreghstrim_field;
        partial void Retregtempcomp_Isbiasctrl_R0retreghstrim_Write(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R0retreghstrim_Read(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R0retreghstrim_ValueProvider(ulong a);
        protected IValueRegisterField retregtempcomp_isbiasctrl_r1retreghstrim_field;
        partial void Retregtempcomp_Isbiasctrl_R1retreghstrim_Write(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R1retreghstrim_Read(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R1retreghstrim_ValueProvider(ulong a);
        protected IValueRegisterField retregtempcomp_isbiasctrl_r2retreghstrim_field;
        partial void Retregtempcomp_Isbiasctrl_R2retreghstrim_Write(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R2retreghstrim_Read(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R2retreghstrim_ValueProvider(ulong a);
        protected IValueRegisterField retregtempcomp_isbiasctrl_r3retreghstrim_field;
        partial void Retregtempcomp_Isbiasctrl_R3retreghstrim_Write(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R3retreghstrim_Read(ulong a, ulong b);
        partial void Retregtempcomp_Isbiasctrl_R3retreghstrim_ValueProvider(ulong a);

        partial void Retregtempcomp_Isbiasctrl_Write(uint a, uint b);
        partial void Retregtempcomp_Isbiasctrl_Read(uint a, uint b);
        
        // Efpif - Offset : 0x100
        protected IFlagRegisterField efpif_efpif_bit;
        partial void Efpif_Efpif_Write(bool a, bool b);
        partial void Efpif_Efpif_Read(bool a, bool b);
        partial void Efpif_Efpif_ValueProvider(bool a);

        partial void Efpif_Write(uint a, uint b);
        partial void Efpif_Read(uint a, uint b);
        
        // Efpien - Offset : 0x104
        protected IFlagRegisterField efpien_efpien_bit;
        partial void Efpien_Efpien_Write(bool a, bool b);
        partial void Efpien_Efpien_Read(bool a, bool b);
        partial void Efpien_Efpien_ValueProvider(bool a);

        partial void Efpien_Write(uint a, uint b);
        partial void Efpien_Read(uint a, uint b);
        
        // Efpctrl - Offset : 0x108
        protected IFlagRegisterField efpctrl_efpdmem2inem4_bit;
        partial void Efpctrl_Efpdmem2inem4_Write(bool a, bool b);
        partial void Efpctrl_Efpdmem2inem4_Read(bool a, bool b);
        partial void Efpctrl_Efpdmem2inem4_ValueProvider(bool a);
        protected IFlagRegisterField efpctrl_efpdmswap_bit;
        partial void Efpctrl_Efpdmswap_Write(bool a, bool b);
        partial void Efpctrl_Efpdmswap_Read(bool a, bool b);
        partial void Efpctrl_Efpdmswap_ValueProvider(bool a);
        protected IFlagRegisterField efpctrl_efpdmoverride_bit;
        partial void Efpctrl_Efpdmoverride_Write(bool a, bool b);
        partial void Efpctrl_Efpdmoverride_Read(bool a, bool b);
        partial void Efpctrl_Efpdmoverride_ValueProvider(bool a);
        protected IValueRegisterField efpctrl_efpdmoverrideval_field;
        partial void Efpctrl_Efpdmoverrideval_Write(ulong a, ulong b);
        partial void Efpctrl_Efpdmoverrideval_Read(ulong a, ulong b);
        partial void Efpctrl_Efpdmoverrideval_ValueProvider(ulong a);

        partial void Efpctrl_Write(uint a, uint b);
        partial void Efpctrl_Read(uint a, uint b);
        
        // Rootctrl - Offset : 0x188
        protected IFlagRegisterField rootctrl_vscalelock_bit;
        partial void Rootctrl_Vscalelock_Write(bool a, bool b);
        partial void Rootctrl_Vscalelock_Read(bool a, bool b);
        partial void Rootctrl_Vscalelock_ValueProvider(bool a);
        protected IValueRegisterField rootctrl_tamperinfo_field;
        partial void Rootctrl_Tamperinfo_Write(ulong a, ulong b);
        partial void Rootctrl_Tamperinfo_Read(ulong a, ulong b);
        partial void Rootctrl_Tamperinfo_ValueProvider(ulong a);

        partial void Rootctrl_Write(uint a, uint b);
        partial void Rootctrl_Read(uint a, uint b);
        
        // Rpuratd0_Drpu - Offset : 0x18C
        protected IFlagRegisterField rpuratd0_drpu_ratdldreg_bit;
        partial void Rpuratd0_Drpu_Ratdldreg_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdldreg_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdldreg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratddvddlebod_bit;
        partial void Rpuratd0_Drpu_Ratddvddlebod_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddvddlebod_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddvddlebod_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdvlmthv_bit;
        partial void Rpuratd0_Drpu_Ratdvlmthv_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdvlmthv_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdvlmthv_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratddvddbod_bit;
        partial void Rpuratd0_Drpu_Ratddvddbod_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddvddbod_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddvddbod_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratddecbod_bit;
        partial void Rpuratd0_Drpu_Ratddecbod_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddecbod_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratddecbod_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdhdreg_bit;
        partial void Rpuratd0_Drpu_Ratdhdreg_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdhdreg_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdhdreg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdretreg_bit;
        partial void Rpuratd0_Drpu_Ratdretreg_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdretreg_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdretreg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdbod3sensetrim_bit;
        partial void Rpuratd0_Drpu_Ratdbod3sensetrim_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdbod3sensetrim_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdbod3sensetrim_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdbod3sense_bit;
        partial void Rpuratd0_Drpu_Ratdbod3sense_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdbod3sense_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdbod3sense_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdisbias_bit;
        partial void Rpuratd0_Drpu_Ratdisbias_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbias_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbias_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdisbiastrim_bit;
        partial void Rpuratd0_Drpu_Ratdisbiastrim_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbiastrim_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbiastrim_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdisbiasvrefregtrim_bit;
        partial void Rpuratd0_Drpu_Ratdisbiasvrefregtrim_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbiasvrefregtrim_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbiasvrefregtrim_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdisbiasvreflvbodtrim_bit;
        partial void Rpuratd0_Drpu_Ratdisbiasvreflvbodtrim_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbiasvreflvbodtrim_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdisbiasvreflvbodtrim_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdpfmbyp_bit;
        partial void Rpuratd0_Drpu_Ratdpfmbyp_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpfmbyp_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpfmbyp_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdpfmbypctrl_bit;
        partial void Rpuratd0_Drpu_Ratdpfmbypctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpfmbypctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpfmbypctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdpd1paretctrl_bit;
        partial void Rpuratd0_Drpu_Ratdpd1paretctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpd1paretctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpd1paretctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdpd0pwrctrl_bit;
        partial void Rpuratd0_Drpu_Ratdpd0pwrctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpd0pwrctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpd0pwrctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdpd0pwrem2ctrl_bit;
        partial void Rpuratd0_Drpu_Ratdpd0pwrem2ctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpd0pwrem2ctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdpd0pwrem2ctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdlock_bit;
        partial void Rpuratd0_Drpu_Ratdlock_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdlock_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdlock_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdif_bit;
        partial void Rpuratd0_Drpu_Ratdif_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdif_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdien_bit;
        partial void Rpuratd0_Drpu_Ratdien_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdien_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdem4ctrl_bit;
        partial void Rpuratd0_Drpu_Ratdem4ctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem4ctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdem4ctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdcmd_bit;
        partial void Rpuratd0_Drpu_Ratdcmd_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcmd_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdcmd_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdctrl_bit;
        partial void Rpuratd0_Drpu_Ratdctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtemplimits_bit;
        partial void Rpuratd0_Drpu_Ratdtemplimits_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtemplimits_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtemplimits_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdtemplimitsdg_bit;
        partial void Rpuratd0_Drpu_Ratdtemplimitsdg_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtemplimitsdg_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdtemplimitsdg_ValueProvider(bool a);

        partial void Rpuratd0_Drpu_Write(uint a, uint b);
        partial void Rpuratd0_Drpu_Read(uint a, uint b);
        
        // Rpuratd1_Drpu - Offset : 0x190
        protected IFlagRegisterField rpuratd1_drpu_ratdtemplimitsse_bit;
        partial void Rpuratd1_Drpu_Ratdtemplimitsse_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtemplimitsse_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtemplimitsse_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdtestctrl_bit;
        partial void Rpuratd1_Drpu_Ratdtestctrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtestctrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtestctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdrmuctrl_bit;
        partial void Rpuratd1_Drpu_Ratdrmuctrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdrmuctrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdrmuctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratddgif_bit;
        partial void Rpuratd1_Drpu_Ratddgif_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddgif_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddgif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratddgien_bit;
        partial void Rpuratd1_Drpu_Ratddgien_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddgien_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddgien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdseqif_bit;
        partial void Rpuratd1_Drpu_Ratdseqif_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdseqif_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdseqif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdseqien_bit;
        partial void Rpuratd1_Drpu_Ratdseqien_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdseqien_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdseqien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratddelaycfg_bit;
        partial void Rpuratd1_Drpu_Ratddelaycfg_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddelaycfg_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratddelaycfg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdtestlock_bit;
        partial void Rpuratd1_Drpu_Ratdtestlock_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtestlock_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdtestlock_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdauxctrl_bit;
        partial void Rpuratd1_Drpu_Ratdauxctrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdauxctrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdauxctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiasconf_bit;
        partial void Rpuratd1_Drpu_Ratdisbiasconf_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiasconf_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiasconf_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiascalovr_bit;
        partial void Rpuratd1_Drpu_Ratdisbiascalovr_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiascalovr_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiascalovr_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiasperiod_bit;
        partial void Rpuratd1_Drpu_Ratdisbiasperiod_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiasperiod_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiasperiod_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiastempcomprate_bit;
        partial void Rpuratd1_Drpu_Ratdisbiastempcomprate_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiastempcomprate_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiastempcomprate_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiastempcompthr_bit;
        partial void Rpuratd1_Drpu_Ratdisbiastempcompthr_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiastempcompthr_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiastempcompthr_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiaspfmrefreshcfg_bit;
        partial void Rpuratd1_Drpu_Ratdisbiaspfmrefreshcfg_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiaspfmrefreshcfg_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiaspfmrefreshcfg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiasrefreshcfg_bit;
        partial void Rpuratd1_Drpu_Ratdisbiasrefreshcfg_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiasrefreshcfg_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiasrefreshcfg_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdisbiastempconst_bit;
        partial void Rpuratd1_Drpu_Ratdisbiastempconst_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiastempconst_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdisbiastempconst_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdvsbtempcomp_bit;
        partial void Rpuratd1_Drpu_Ratdvsbtempcomp_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdvsbtempcomp_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdvsbtempcomp_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdvsbtempcompthr_bit;
        partial void Rpuratd1_Drpu_Ratdvsbtempcompthr_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdvsbtempcompthr_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdvsbtempcompthr_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdretregtempcomp_bit;
        partial void Rpuratd1_Drpu_Ratdretregtempcomp_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdretregtempcomp_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdretregtempcomp_ValueProvider(bool a);

        partial void Rpuratd1_Drpu_Write(uint a, uint b);
        partial void Rpuratd1_Drpu_Read(uint a, uint b);
        
        // Rpuratd2_Drpu - Offset : 0x194
        protected IFlagRegisterField rpuratd2_drpu_ratdefpif_bit;
        partial void Rpuratd2_Drpu_Ratdefpif_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdefpif_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdefpif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdefpien_bit;
        partial void Rpuratd2_Drpu_Ratdefpien_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdefpien_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdefpien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdefpctrl_bit;
        partial void Rpuratd2_Drpu_Ratdefpctrl_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdefpctrl_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdefpctrl_ValueProvider(bool a);

        partial void Rpuratd2_Drpu_Write(uint a, uint b);
        partial void Rpuratd2_Drpu_Read(uint a, uint b);
        
        // Rpuratd3_Drpu - Offset : 0x198
        protected IFlagRegisterField rpuratd3_drpu_ratdrootctrl_bit;
        partial void Rpuratd3_Drpu_Ratdrootctrl_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdrootctrl_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdrootctrl_ValueProvider(bool a);

        partial void Rpuratd3_Drpu_Write(uint a, uint b);
        partial void Rpuratd3_Drpu_Read(uint a, uint b);
        
        partial void EMU_Reset();

        partial void EFR32xG2_EMU_3_Constructor();

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
            Ldreg = 0x0,
            Dvddlebod = 0x4,
            Vlmthv = 0x8,
            Dvddbod = 0xC,
            Decbod = 0x10,
            Hdreg = 0x14,
            Retreg = 0x18,
            Bod3sensetrim = 0x1C,
            Bod3sense = 0x20,
            Isbias = 0x24,
            Isbiastrim = 0x28,
            Isbiasvrefregtrim = 0x2C,
            Isbiasvreflvbodtrim = 0x30,
            Anastatus = 0x34,
            Pfmbyp = 0x38,
            Vregvddcmpctrl = 0x3C,
            Pd1paretctrl = 0x40,
            Pd0pwrctrl = 0x44,
            Pd0pwrem2ctrl = 0x48,
            Ipversion_Rev = 0x5C,
            Lock = 0x60,
            If = 0x64,
            Ien = 0x68,
            Em4ctrl = 0x6C,
            Cmd = 0x70,
            Ctrl = 0x74,
            Templimits = 0x78,
            Templimitsdg = 0x7C,
            Templimitsse = 0x80,
            Status = 0x84,
            Temp = 0x88,
            Testctrl = 0x8C,
            Rstctrl = 0x90,
            Rstcause = 0x94,
            Tamperrstcause = 0x98,
            Dgif = 0xA0,
            Dgien = 0xA4,
            Seqif = 0xA8,
            Seqien = 0xAC,
            Delaycfg = 0xB0,
            Testlock = 0xB4,
            Auxctrl = 0xB8,
            Isbiasconf_Isbiasctrl = 0xC0,
            Isbiascalovr_Isbiasctrl = 0xC4,
            Isbiasperiod_Isbiasctrl = 0xC8,
            Isbiastempcomprate_Isbiasctrl = 0xCC,
            Isbiastempcompthr_Isbiasctrl = 0xD0,
            Isbiaspfmrefreshcfg_Isbiasctrl = 0xD8,
            Isbiasrefreshcfg_Isbiasctrl = 0xDC,
            Isbiastempconst_Isbiasctrl = 0xE0,
            Isbiasstatus_Isbiasctrl = 0xE4,
            Vsbtempcomp_Isbiasctrl = 0xE8,
            Vsbtempcompthr_Isbiasctrl = 0xEC,
            Retregtempcomp_Isbiasctrl = 0xF0,
            Efpif = 0x100,
            Efpien = 0x104,
            Efpctrl = 0x108,
            Rootctrl = 0x188,
            Rpuratd0_Drpu = 0x18C,
            Rpuratd1_Drpu = 0x190,
            Rpuratd2_Drpu = 0x194,
            Rpuratd3_Drpu = 0x198,
            
            Ldreg_SET = 0x1000,
            Dvddlebod_SET = 0x1004,
            Vlmthv_SET = 0x1008,
            Dvddbod_SET = 0x100C,
            Decbod_SET = 0x1010,
            Hdreg_SET = 0x1014,
            Retreg_SET = 0x1018,
            Bod3sensetrim_SET = 0x101C,
            Bod3sense_SET = 0x1020,
            Isbias_SET = 0x1024,
            Isbiastrim_SET = 0x1028,
            Isbiasvrefregtrim_SET = 0x102C,
            Isbiasvreflvbodtrim_SET = 0x1030,
            Anastatus_SET = 0x1034,
            Pfmbyp_SET = 0x1038,
            Vregvddcmpctrl_SET = 0x103C,
            Pd1paretctrl_SET = 0x1040,
            Pd0pwrctrl_SET = 0x1044,
            Pd0pwrem2ctrl_SET = 0x1048,
            Ipversion_Rev_SET = 0x105C,
            Lock_SET = 0x1060,
            If_SET = 0x1064,
            Ien_SET = 0x1068,
            Em4ctrl_SET = 0x106C,
            Cmd_SET = 0x1070,
            Ctrl_SET = 0x1074,
            Templimits_SET = 0x1078,
            Templimitsdg_SET = 0x107C,
            Templimitsse_SET = 0x1080,
            Status_SET = 0x1084,
            Temp_SET = 0x1088,
            Testctrl_SET = 0x108C,
            Rstctrl_SET = 0x1090,
            Rstcause_SET = 0x1094,
            Tamperrstcause_SET = 0x1098,
            Dgif_SET = 0x10A0,
            Dgien_SET = 0x10A4,
            Seqif_SET = 0x10A8,
            Seqien_SET = 0x10AC,
            Delaycfg_SET = 0x10B0,
            Testlock_SET = 0x10B4,
            Auxctrl_SET = 0x10B8,
            Isbiasconf_Isbiasctrl_SET = 0x10C0,
            Isbiascalovr_Isbiasctrl_SET = 0x10C4,
            Isbiasperiod_Isbiasctrl_SET = 0x10C8,
            Isbiastempcomprate_Isbiasctrl_SET = 0x10CC,
            Isbiastempcompthr_Isbiasctrl_SET = 0x10D0,
            Isbiaspfmrefreshcfg_Isbiasctrl_SET = 0x10D8,
            Isbiasrefreshcfg_Isbiasctrl_SET = 0x10DC,
            Isbiastempconst_Isbiasctrl_SET = 0x10E0,
            Isbiasstatus_Isbiasctrl_SET = 0x10E4,
            Vsbtempcomp_Isbiasctrl_SET = 0x10E8,
            Vsbtempcompthr_Isbiasctrl_SET = 0x10EC,
            Retregtempcomp_Isbiasctrl_SET = 0x10F0,
            Efpif_SET = 0x1100,
            Efpien_SET = 0x1104,
            Efpctrl_SET = 0x1108,
            Rootctrl_SET = 0x1188,
            Rpuratd0_Drpu_SET = 0x118C,
            Rpuratd1_Drpu_SET = 0x1190,
            Rpuratd2_Drpu_SET = 0x1194,
            Rpuratd3_Drpu_SET = 0x1198,
            
            Ldreg_CLR = 0x2000,
            Dvddlebod_CLR = 0x2004,
            Vlmthv_CLR = 0x2008,
            Dvddbod_CLR = 0x200C,
            Decbod_CLR = 0x2010,
            Hdreg_CLR = 0x2014,
            Retreg_CLR = 0x2018,
            Bod3sensetrim_CLR = 0x201C,
            Bod3sense_CLR = 0x2020,
            Isbias_CLR = 0x2024,
            Isbiastrim_CLR = 0x2028,
            Isbiasvrefregtrim_CLR = 0x202C,
            Isbiasvreflvbodtrim_CLR = 0x2030,
            Anastatus_CLR = 0x2034,
            Pfmbyp_CLR = 0x2038,
            Vregvddcmpctrl_CLR = 0x203C,
            Pd1paretctrl_CLR = 0x2040,
            Pd0pwrctrl_CLR = 0x2044,
            Pd0pwrem2ctrl_CLR = 0x2048,
            Ipversion_Rev_CLR = 0x205C,
            Lock_CLR = 0x2060,
            If_CLR = 0x2064,
            Ien_CLR = 0x2068,
            Em4ctrl_CLR = 0x206C,
            Cmd_CLR = 0x2070,
            Ctrl_CLR = 0x2074,
            Templimits_CLR = 0x2078,
            Templimitsdg_CLR = 0x207C,
            Templimitsse_CLR = 0x2080,
            Status_CLR = 0x2084,
            Temp_CLR = 0x2088,
            Testctrl_CLR = 0x208C,
            Rstctrl_CLR = 0x2090,
            Rstcause_CLR = 0x2094,
            Tamperrstcause_CLR = 0x2098,
            Dgif_CLR = 0x20A0,
            Dgien_CLR = 0x20A4,
            Seqif_CLR = 0x20A8,
            Seqien_CLR = 0x20AC,
            Delaycfg_CLR = 0x20B0,
            Testlock_CLR = 0x20B4,
            Auxctrl_CLR = 0x20B8,
            Isbiasconf_Isbiasctrl_CLR = 0x20C0,
            Isbiascalovr_Isbiasctrl_CLR = 0x20C4,
            Isbiasperiod_Isbiasctrl_CLR = 0x20C8,
            Isbiastempcomprate_Isbiasctrl_CLR = 0x20CC,
            Isbiastempcompthr_Isbiasctrl_CLR = 0x20D0,
            Isbiaspfmrefreshcfg_Isbiasctrl_CLR = 0x20D8,
            Isbiasrefreshcfg_Isbiasctrl_CLR = 0x20DC,
            Isbiastempconst_Isbiasctrl_CLR = 0x20E0,
            Isbiasstatus_Isbiasctrl_CLR = 0x20E4,
            Vsbtempcomp_Isbiasctrl_CLR = 0x20E8,
            Vsbtempcompthr_Isbiasctrl_CLR = 0x20EC,
            Retregtempcomp_Isbiasctrl_CLR = 0x20F0,
            Efpif_CLR = 0x2100,
            Efpien_CLR = 0x2104,
            Efpctrl_CLR = 0x2108,
            Rootctrl_CLR = 0x2188,
            Rpuratd0_Drpu_CLR = 0x218C,
            Rpuratd1_Drpu_CLR = 0x2190,
            Rpuratd2_Drpu_CLR = 0x2194,
            Rpuratd3_Drpu_CLR = 0x2198,
            
            Ldreg_TGL = 0x3000,
            Dvddlebod_TGL = 0x3004,
            Vlmthv_TGL = 0x3008,
            Dvddbod_TGL = 0x300C,
            Decbod_TGL = 0x3010,
            Hdreg_TGL = 0x3014,
            Retreg_TGL = 0x3018,
            Bod3sensetrim_TGL = 0x301C,
            Bod3sense_TGL = 0x3020,
            Isbias_TGL = 0x3024,
            Isbiastrim_TGL = 0x3028,
            Isbiasvrefregtrim_TGL = 0x302C,
            Isbiasvreflvbodtrim_TGL = 0x3030,
            Anastatus_TGL = 0x3034,
            Pfmbyp_TGL = 0x3038,
            Vregvddcmpctrl_TGL = 0x303C,
            Pd1paretctrl_TGL = 0x3040,
            Pd0pwrctrl_TGL = 0x3044,
            Pd0pwrem2ctrl_TGL = 0x3048,
            Ipversion_Rev_TGL = 0x305C,
            Lock_TGL = 0x3060,
            If_TGL = 0x3064,
            Ien_TGL = 0x3068,
            Em4ctrl_TGL = 0x306C,
            Cmd_TGL = 0x3070,
            Ctrl_TGL = 0x3074,
            Templimits_TGL = 0x3078,
            Templimitsdg_TGL = 0x307C,
            Templimitsse_TGL = 0x3080,
            Status_TGL = 0x3084,
            Temp_TGL = 0x3088,
            Testctrl_TGL = 0x308C,
            Rstctrl_TGL = 0x3090,
            Rstcause_TGL = 0x3094,
            Tamperrstcause_TGL = 0x3098,
            Dgif_TGL = 0x30A0,
            Dgien_TGL = 0x30A4,
            Seqif_TGL = 0x30A8,
            Seqien_TGL = 0x30AC,
            Delaycfg_TGL = 0x30B0,
            Testlock_TGL = 0x30B4,
            Auxctrl_TGL = 0x30B8,
            Isbiasconf_Isbiasctrl_TGL = 0x30C0,
            Isbiascalovr_Isbiasctrl_TGL = 0x30C4,
            Isbiasperiod_Isbiasctrl_TGL = 0x30C8,
            Isbiastempcomprate_Isbiasctrl_TGL = 0x30CC,
            Isbiastempcompthr_Isbiasctrl_TGL = 0x30D0,
            Isbiaspfmrefreshcfg_Isbiasctrl_TGL = 0x30D8,
            Isbiasrefreshcfg_Isbiasctrl_TGL = 0x30DC,
            Isbiastempconst_Isbiasctrl_TGL = 0x30E0,
            Isbiasstatus_Isbiasctrl_TGL = 0x30E4,
            Vsbtempcomp_Isbiasctrl_TGL = 0x30E8,
            Vsbtempcompthr_Isbiasctrl_TGL = 0x30EC,
            Retregtempcomp_Isbiasctrl_TGL = 0x30F0,
            Efpif_TGL = 0x3100,
            Efpien_TGL = 0x3104,
            Efpctrl_TGL = 0x3108,
            Rootctrl_TGL = 0x3188,
            Rpuratd0_Drpu_TGL = 0x318C,
            Rpuratd1_Drpu_TGL = 0x3190,
            Rpuratd2_Drpu_TGL = 0x3194,
            Rpuratd3_Drpu_TGL = 0x3198,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}