//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    LFRCO, Generated on : 2023-07-20 14:24:01.841604
    LFRCO, ID Version : c5b974e8e30f414db1344f6c7175dc21.2 */

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
    public partial class EFR32xG2_LFRCO_2
    {
        public EFR32xG2_LFRCO_2(Machine machine) : base(machine)
        {
            EFR32xG2_LFRCO_2_constructor();
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
    public partial class EFR32xG2_LFRCO_2 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_LFRCO_2(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_LFRCO_2_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Cfg, GenerateCfgRegister()},
                {(long)Registers.Hiprecision, GenerateHiprecisionRegister()},
                {(long)Registers.Nomcal, GenerateNomcalRegister()},
                {(long)Registers.Nomcalinv, GenerateNomcalinvRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Trim, GenerateTrimRegister()},
                {(long)Registers.Trimhiprec, GenerateTrimhiprecRegister()},
                {(long)Registers.Stepsize, GenerateStepsizeRegister()},
                {(long)Registers.Testctrl, GenerateTestctrlRegister()},
                {(long)Registers.Testcalstatus, GenerateTestcalstatusRegister()},
                {(long)Registers.Testtcstatus, GenerateTesttcstatusRegister()},
                {(long)Registers.Testtclatest, GenerateTesttclatestRegister()},
                {(long)Registers.Testcalres, GenerateTestcalresRegister()},
                {(long)Registers.Testcmd, GenerateTestcmdRegister()},
                {(long)Registers.Defeat, GenerateDefeatRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            LFRCO_Reset();
        }
        
        protected enum STATUS_LOCK
        {
            UNLOCKED = 0, // Access to configuration registers not locked
            LOCKED = 1, // Access to configuration registers locked
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x2)
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
            .WithFlag(0, out ctrl_forceen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Forceen_ValueProvider(_);
                        return ctrl_forceen_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Forceen_Write(_, __),
                    readCallback: (_, __) => Ctrl_Forceen_Read(_, __),
                    name: "Forceen")
            .WithFlag(1, out ctrl_disondemand_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Disondemand_ValueProvider(_);
                        return ctrl_disondemand_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Disondemand_Write(_, __),
                    readCallback: (_, __) => Ctrl_Disondemand_Read(_, __),
                    name: "Disondemand")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write(_, __));
        
        // Status - Offset : 0x8
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_rdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Rdy_ValueProvider(_);
                        return status_rdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Rdy_Read(_, __),
                    name: "Rdy")
            .WithReservedBits(1, 15)
            .WithFlag(16, out status_ens_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Ens_ValueProvider(_);
                        return status_ens_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Ens_Read(_, __),
                    name: "Ens")
            .WithReservedBits(17, 14)
            .WithEnumField<DoubleWordRegister, STATUS_LOCK>(31, 1, out status_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Lock_ValueProvider(_);
                        return status_lock_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Lock_Read(_, __),
                    name: "Lock")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // If - Offset : 0x14
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_rdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Rdy_ValueProvider(_);
                        return if_rdy_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Rdy_Write(_, __),
                    readCallback: (_, __) => If_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out if_posedge_bit, 
                    valueProviderCallback: (_) => {
                        If_Posedge_ValueProvider(_);
                        return if_posedge_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Posedge_Write(_, __),
                    readCallback: (_, __) => If_Posedge_Read(_, __),
                    name: "Posedge")
            .WithFlag(2, out if_negedge_bit, 
                    valueProviderCallback: (_) => {
                        If_Negedge_ValueProvider(_);
                        return if_negedge_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Negedge_Write(_, __),
                    readCallback: (_, __) => If_Negedge_Read(_, __),
                    name: "Negedge")
            .WithReservedBits(3, 5)
            .WithFlag(8, out if_tcdone_bit, 
                    valueProviderCallback: (_) => {
                        If_Tcdone_ValueProvider(_);
                        return if_tcdone_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Tcdone_Write(_, __),
                    readCallback: (_, __) => If_Tcdone_Read(_, __),
                    name: "Tcdone")
            .WithFlag(9, out if_caldone_bit, 
                    valueProviderCallback: (_) => {
                        If_Caldone_ValueProvider(_);
                        return if_caldone_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Caldone_Write(_, __),
                    readCallback: (_, __) => If_Caldone_Read(_, __),
                    name: "Caldone")
            .WithFlag(10, out if_tempchange_bit, 
                    valueProviderCallback: (_) => {
                        If_Tempchange_ValueProvider(_);
                        return if_tempchange_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Tempchange_Write(_, __),
                    readCallback: (_, __) => If_Tempchange_Read(_, __),
                    name: "Tempchange")
            .WithReservedBits(11, 5)
            .WithFlag(16, out if_schederr_bit, 
                    valueProviderCallback: (_) => {
                        If_Schederr_ValueProvider(_);
                        return if_schederr_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Schederr_Write(_, __),
                    readCallback: (_, __) => If_Schederr_Read(_, __),
                    name: "Schederr")
            .WithFlag(17, out if_tcoor_bit, 
                    valueProviderCallback: (_) => {
                        If_Tcoor_ValueProvider(_);
                        return if_tcoor_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Tcoor_Write(_, __),
                    readCallback: (_, __) => If_Tcoor_Read(_, __),
                    name: "Tcoor")
            .WithFlag(18, out if_caloor_bit, 
                    valueProviderCallback: (_) => {
                        If_Caloor_ValueProvider(_);
                        return if_caloor_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Caloor_Write(_, __),
                    readCallback: (_, __) => If_Caloor_Read(_, __),
                    name: "Caloor")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x18
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_rdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Rdy_ValueProvider(_);
                        return ien_rdy_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Rdy_Write(_, __),
                    readCallback: (_, __) => Ien_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out ien_posedge_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Posedge_ValueProvider(_);
                        return ien_posedge_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Posedge_Write(_, __),
                    readCallback: (_, __) => Ien_Posedge_Read(_, __),
                    name: "Posedge")
            .WithFlag(2, out ien_negedge_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Negedge_ValueProvider(_);
                        return ien_negedge_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Negedge_Write(_, __),
                    readCallback: (_, __) => Ien_Negedge_Read(_, __),
                    name: "Negedge")
            .WithReservedBits(3, 5)
            .WithFlag(8, out ien_tcdone_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Tcdone_ValueProvider(_);
                        return ien_tcdone_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Tcdone_Write(_, __),
                    readCallback: (_, __) => Ien_Tcdone_Read(_, __),
                    name: "Tcdone")
            .WithFlag(9, out ien_caldone_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Caldone_ValueProvider(_);
                        return ien_caldone_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Caldone_Write(_, __),
                    readCallback: (_, __) => Ien_Caldone_Read(_, __),
                    name: "Caldone")
            .WithFlag(10, out ien_tempchange_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Tempchange_ValueProvider(_);
                        return ien_tempchange_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Tempchange_Write(_, __),
                    readCallback: (_, __) => Ien_Tempchange_Read(_, __),
                    name: "Tempchange")
            .WithReservedBits(11, 5)
            .WithFlag(16, out ien_schederr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Schederr_ValueProvider(_);
                        return ien_schederr_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Schederr_Write(_, __),
                    readCallback: (_, __) => Ien_Schederr_Read(_, __),
                    name: "Schederr")
            .WithFlag(17, out ien_tcoor_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Tcoor_ValueProvider(_);
                        return ien_tcoor_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Tcoor_Write(_, __),
                    readCallback: (_, __) => Ien_Tcoor_Read(_, __),
                    name: "Tcoor")
            .WithFlag(18, out ien_caloor_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Caloor_ValueProvider(_);
                        return ien_caloor_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Caloor_Write(_, __),
                    readCallback: (_, __) => Ien_Caloor_Read(_, __),
                    name: "Caloor")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Lock - Offset : 0x20
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Cfg - Offset : 0x24
        protected DoubleWordRegister  GenerateCfgRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cfg_highprecen_bit, 
                    valueProviderCallback: (_) => {
                        Cfg_Highprecen_ValueProvider(_);
                        return cfg_highprecen_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Cfg_Highprecen_Write(_, __);
                    },
                    readCallback: (_, __) => Cfg_Highprecen_Read(_, __),
                    name: "Highprecen")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Cfg_Read(_, __))
            .WithWriteCallback((_, __) => Cfg_Write(_, __));
        
        // Hiprecision - Offset : 0x28
        protected DoubleWordRegister  GenerateHiprecisionRegister() => new DoubleWordRegister(this, 0x10E22645)
            .WithValueField(0, 4, out hiprecision_calwnd_field, 
                    valueProviderCallback: (_) => {
                        Hiprecision_Calwnd_ValueProvider(_);
                        return hiprecision_calwnd_field.Value;               
                    },
                    writeCallback: (_, __) => Hiprecision_Calwnd_Write(_, __),
                    readCallback: (_, __) => Hiprecision_Calwnd_Read(_, __),
                    name: "Calwnd")
            .WithValueField(4, 4, out hiprecision_tcwnd_field, 
                    valueProviderCallback: (_) => {
                        Hiprecision_Tcwnd_ValueProvider(_);
                        return hiprecision_tcwnd_field.Value;               
                    },
                    writeCallback: (_, __) => Hiprecision_Tcwnd_Write(_, __),
                    readCallback: (_, __) => Hiprecision_Tcwnd_Read(_, __),
                    name: "Tcwnd")
            .WithValueField(8, 8, out hiprecision_tcthresh_field, 
                    valueProviderCallback: (_) => {
                        Hiprecision_Tcthresh_ValueProvider(_);
                        return hiprecision_tcthresh_field.Value;               
                    },
                    writeCallback: (_, __) => Hiprecision_Tcthresh_Write(_, __),
                    readCallback: (_, __) => Hiprecision_Tcthresh_Read(_, __),
                    name: "Tcthresh")
            .WithValueField(16, 3, out hiprecision_tcintshort_field, 
                    valueProviderCallback: (_) => {
                        Hiprecision_Tcintshort_ValueProvider(_);
                        return hiprecision_tcintshort_field.Value;               
                    },
                    writeCallback: (_, __) => Hiprecision_Tcintshort_Write(_, __),
                    readCallback: (_, __) => Hiprecision_Tcintshort_Read(_, __),
                    name: "Tcintshort")
            .WithValueField(19, 3, out hiprecision_tcintlong_field, 
                    valueProviderCallback: (_) => {
                        Hiprecision_Tcintlong_ValueProvider(_);
                        return hiprecision_tcintlong_field.Value;               
                    },
                    writeCallback: (_, __) => Hiprecision_Tcintlong_Write(_, __),
                    readCallback: (_, __) => Hiprecision_Tcintlong_Read(_, __),
                    name: "Tcintlong")
            .WithValueField(22, 3, out hiprecision_calint_field, 
                    valueProviderCallback: (_) => {
                        Hiprecision_Calint_ValueProvider(_);
                        return hiprecision_calint_field.Value;               
                    },
                    writeCallback: (_, __) => Hiprecision_Calint_Write(_, __),
                    readCallback: (_, __) => Hiprecision_Calint_Read(_, __),
                    name: "Calint")
            .WithValueField(25, 7, out hiprecision_tcintthresh_field, 
                    valueProviderCallback: (_) => {
                        Hiprecision_Tcintthresh_ValueProvider(_);
                        return hiprecision_tcintthresh_field.Value;               
                    },
                    writeCallback: (_, __) => Hiprecision_Tcintthresh_Write(_, __),
                    readCallback: (_, __) => Hiprecision_Tcintthresh_Read(_, __),
                    name: "Tcintthresh")
            .WithReadCallback((_, __) => Hiprecision_Read(_, __))
            .WithWriteCallback((_, __) => Hiprecision_Write(_, __));
        
        // Nomcal - Offset : 0x2C
        protected DoubleWordRegister  GenerateNomcalRegister() => new DoubleWordRegister(this, 0x5B8D8)
            .WithValueField(0, 21, out nomcal_nomcalcnt_field, 
                    valueProviderCallback: (_) => {
                        Nomcal_Nomcalcnt_ValueProvider(_);
                        return nomcal_nomcalcnt_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Nomcal_Nomcalcnt_Write(_, __);
                    },
                    readCallback: (_, __) => Nomcal_Nomcalcnt_Read(_, __),
                    name: "Nomcalcnt")
            .WithReservedBits(21, 11)
            .WithReadCallback((_, __) => Nomcal_Read(_, __))
            .WithWriteCallback((_, __) => Nomcal_Write(_, __));
        
        // Nomcalinv - Offset : 0x30
        protected DoubleWordRegister  GenerateNomcalinvRegister() => new DoubleWordRegister(this, 0x597A)
            .WithValueField(0, 17, out nomcalinv_nomcalcntinv_field, 
                    valueProviderCallback: (_) => {
                        Nomcalinv_Nomcalcntinv_ValueProvider(_);
                        return nomcalinv_nomcalcntinv_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteWSTATIC();
                        Nomcalinv_Nomcalcntinv_Write(_, __);
                    },
                    readCallback: (_, __) => Nomcalinv_Nomcalcntinv_Read(_, __),
                    name: "Nomcalcntinv")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Nomcalinv_Read(_, __))
            .WithWriteCallback((_, __) => Nomcalinv_Write(_, __));
        
        // Cmd - Offset : 0x34
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cmd_reducetcint_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Reducetcint_Write(_, __),
                    name: "Reducetcint")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write(_, __));
        
        // Trim - Offset : 0x38
        protected DoubleWordRegister  GenerateTrimRegister() => new DoubleWordRegister(this, 0x44002020)
            .WithValueField(0, 6, out trim_trimfine_field, 
                    valueProviderCallback: (_) => {
                        Trim_Trimfine_ValueProvider(_);
                        return trim_trimfine_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Trimfine_Write(_, __),
                    readCallback: (_, __) => Trim_Trimfine_Read(_, __),
                    name: "Trimfine")
            .WithReservedBits(6, 2)
            .WithValueField(8, 6, out trim_trim_field, 
                    valueProviderCallback: (_) => {
                        Trim_Trim_ValueProvider(_);
                        return trim_trim_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Trim_Write(_, __),
                    readCallback: (_, __) => Trim_Trim_Read(_, __),
                    name: "Trim")
            .WithReservedBits(14, 2)
            .WithValueField(16, 3, out trim_trimcoarse_field, 
                    valueProviderCallback: (_) => {
                        Trim_Trimcoarse_ValueProvider(_);
                        return trim_trimcoarse_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Trimcoarse_Write(_, __),
                    readCallback: (_, __) => Trim_Trimcoarse_Read(_, __),
                    name: "Trimcoarse")
            .WithReservedBits(19, 1)
            .WithValueField(20, 3, out trim_bias_field, 
                    valueProviderCallback: (_) => {
                        Trim_Bias_ValueProvider(_);
                        return trim_bias_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Bias_Write(_, __),
                    readCallback: (_, __) => Trim_Bias_Read(_, __),
                    name: "Bias")
            .WithReservedBits(23, 1)
            .WithFlag(24, out trim_tempcob_bit, 
                    valueProviderCallback: (_) => {
                        Trim_Tempcob_ValueProvider(_);
                        return trim_tempcob_bit.Value;               
                    },
                    writeCallback: (_, __) => Trim_Tempcob_Write(_, __),
                    readCallback: (_, __) => Trim_Tempcob_Read(_, __),
                    name: "Tempcob")
            .WithReservedBits(25, 1)
            .WithFlag(26, out trim_tempcoc_bit, 
                    valueProviderCallback: (_) => {
                        Trim_Tempcoc_ValueProvider(_);
                        return trim_tempcoc_bit.Value;               
                    },
                    writeCallback: (_, __) => Trim_Tempcoc_Write(_, __),
                    readCallback: (_, __) => Trim_Tempcoc_Read(_, __),
                    name: "Tempcoc")
            .WithValueField(27, 3, out trim_roten_field, 
                    valueProviderCallback: (_) => {
                        Trim_Roten_ValueProvider(_);
                        return trim_roten_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Roten_Write(_, __),
                    readCallback: (_, __) => Trim_Roten_Read(_, __),
                    name: "Roten")
            .WithValueField(30, 2, out trim_timeout_field, 
                    valueProviderCallback: (_) => {
                        Trim_Timeout_ValueProvider(_);
                        return trim_timeout_field.Value;               
                    },
                    writeCallback: (_, __) => Trim_Timeout_Write(_, __),
                    readCallback: (_, __) => Trim_Timeout_Read(_, __),
                    name: "Timeout")
            .WithReadCallback((_, __) => Trim_Read(_, __))
            .WithWriteCallback((_, __) => Trim_Write(_, __));
        
        // Trimhiprec - Offset : 0x3C
        protected DoubleWordRegister  GenerateTrimhiprecRegister() => new DoubleWordRegister(this, 0x3C602820)
            .WithValueField(0, 6, out trimhiprec_trimtemp_field, 
                    valueProviderCallback: (_) => {
                        Trimhiprec_Trimtemp_ValueProvider(_);
                        return trimhiprec_trimtemp_field.Value;               
                    },
                    writeCallback: (_, __) => Trimhiprec_Trimtemp_Write(_, __),
                    readCallback: (_, __) => Trimhiprec_Trimtemp_Read(_, __),
                    name: "Trimtemp")
            .WithReservedBits(6, 2)
            .WithValueField(8, 6, out trimhiprec_trimhiprec_field, 
                    valueProviderCallback: (_) => {
                        Trimhiprec_Trimhiprec_ValueProvider(_);
                        return trimhiprec_trimhiprec_field.Value;               
                    },
                    writeCallback: (_, __) => Trimhiprec_Trimhiprec_Write(_, __),
                    readCallback: (_, __) => Trimhiprec_Trimhiprec_Read(_, __),
                    name: "Trimhiprec")
            .WithReservedBits(14, 6)
            .WithValueField(20, 3, out trimhiprec_biashiprec_field, 
                    valueProviderCallback: (_) => {
                        Trimhiprec_Biashiprec_ValueProvider(_);
                        return trimhiprec_biashiprec_field.Value;               
                    },
                    writeCallback: (_, __) => Trimhiprec_Biashiprec_Write(_, __),
                    readCallback: (_, __) => Trimhiprec_Biashiprec_Read(_, __),
                    name: "Biashiprec")
            .WithReservedBits(23, 1)
            .WithFlag(24, out trimhiprec_tempcobhiprec_bit, 
                    valueProviderCallback: (_) => {
                        Trimhiprec_Tempcobhiprec_ValueProvider(_);
                        return trimhiprec_tempcobhiprec_bit.Value;               
                    },
                    writeCallback: (_, __) => Trimhiprec_Tempcobhiprec_Write(_, __),
                    readCallback: (_, __) => Trimhiprec_Tempcobhiprec_Read(_, __),
                    name: "Tempcobhiprec")
            .WithReservedBits(25, 1)
            .WithFlag(26, out trimhiprec_tempcochiprec_bit, 
                    valueProviderCallback: (_) => {
                        Trimhiprec_Tempcochiprec_ValueProvider(_);
                        return trimhiprec_tempcochiprec_bit.Value;               
                    },
                    writeCallback: (_, __) => Trimhiprec_Tempcochiprec_Write(_, __),
                    readCallback: (_, __) => Trimhiprec_Tempcochiprec_Read(_, __),
                    name: "Tempcochiprec")
            .WithValueField(27, 3, out trimhiprec_rotenhiprec_field, 
                    valueProviderCallback: (_) => {
                        Trimhiprec_Rotenhiprec_ValueProvider(_);
                        return trimhiprec_rotenhiprec_field.Value;               
                    },
                    writeCallback: (_, __) => Trimhiprec_Rotenhiprec_Write(_, __),
                    readCallback: (_, __) => Trimhiprec_Rotenhiprec_Read(_, __),
                    name: "Rotenhiprec")
            .WithReservedBits(30, 2)
            .WithReadCallback((_, __) => Trimhiprec_Read(_, __))
            .WithWriteCallback((_, __) => Trimhiprec_Write(_, __));
        
        // Stepsize - Offset : 0x40
        protected DoubleWordRegister  GenerateStepsizeRegister() => new DoubleWordRegister(this, 0x2039C3)
            .WithValueField(0, 15, out stepsize_stepoffset_field, 
                    valueProviderCallback: (_) => {
                        Stepsize_Stepoffset_ValueProvider(_);
                        return stepsize_stepoffset_field.Value;               
                    },
                    writeCallback: (_, __) => Stepsize_Stepoffset_Write(_, __),
                    readCallback: (_, __) => Stepsize_Stepoffset_Read(_, __),
                    name: "Stepoffset")
            .WithReservedBits(15, 1)
            .WithValueField(16, 8, out stepsize_stepgain_field, 
                    valueProviderCallback: (_) => {
                        Stepsize_Stepgain_ValueProvider(_);
                        return stepsize_stepgain_field.Value;               
                    },
                    writeCallback: (_, __) => Stepsize_Stepgain_Write(_, __),
                    readCallback: (_, __) => Stepsize_Stepgain_Read(_, __),
                    name: "Stepgain")
            .WithReservedBits(24, 8)
            .WithReadCallback((_, __) => Stepsize_Read(_, __))
            .WithWriteCallback((_, __) => Stepsize_Write(_, __));
        
        // Testctrl - Offset : 0x44
        protected DoubleWordRegister  GenerateTestctrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out testctrl_autodis_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Autodis_ValueProvider(_);
                        return testctrl_autodis_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Autodis_Write(_, __),
                    readCallback: (_, __) => Testctrl_Autodis_Read(_, __),
                    name: "Autodis")
            .WithFlag(1, out testctrl_tempbasedcaldis_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Tempbasedcaldis_ValueProvider(_);
                        return testctrl_tempbasedcaldis_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Tempbasedcaldis_Write(_, __),
                    readCallback: (_, __) => Testctrl_Tempbasedcaldis_Read(_, __),
                    name: "Tempbasedcaldis")
            .WithFlag(2, out testctrl_calovren_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Calovren_ValueProvider(_);
                        return testctrl_calovren_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Calovren_Write(_, __),
                    readCallback: (_, __) => Testctrl_Calovren_Read(_, __),
                    name: "Calovren")
            .WithFlag(3, out testctrl_temposcovren_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Temposcovren_ValueProvider(_);
                        return testctrl_temposcovren_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Temposcovren_Write(_, __),
                    readCallback: (_, __) => Testctrl_Temposcovren_Read(_, __),
                    name: "Temposcovren")
            .WithReservedBits(4, 4)
            .WithValueField(8, 8, out testctrl_calsdminovr_field, 
                    valueProviderCallback: (_) => {
                        Testctrl_Calsdminovr_ValueProvider(_);
                        return testctrl_calsdminovr_field.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Calsdminovr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Calsdminovr_Read(_, __),
                    name: "Calsdminovr")
            .WithValueField(16, 6, out testctrl_calfineovr_field, 
                    valueProviderCallback: (_) => {
                        Testctrl_Calfineovr_ValueProvider(_);
                        return testctrl_calfineovr_field.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Calfineovr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Calfineovr_Read(_, __),
                    name: "Calfineovr")
            .WithFlag(22, out testctrl_calsdmoutovr_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Calsdmoutovr_ValueProvider(_);
                        return testctrl_calsdmoutovr_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Calsdmoutovr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Calsdmoutovr_Read(_, __),
                    name: "Calsdmoutovr")
            .WithValueField(23, 2, out testctrl_temposcctrlovr_field, 
                    valueProviderCallback: (_) => {
                        Testctrl_Temposcctrlovr_ValueProvider(_);
                        return testctrl_temposcctrlovr_field.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Temposcctrlovr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Temposcctrlovr_Read(_, __),
                    name: "Temposcctrlovr")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => Testctrl_Read(_, __))
            .WithWriteCallback((_, __) => Testctrl_Write(_, __));
        
        // Testcalstatus - Offset : 0x48
        protected DoubleWordRegister  GenerateTestcalstatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 21, out testcalstatus_calcnt_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testcalstatus_Calcnt_ValueProvider(_);
                        return testcalstatus_calcnt_field.Value;               
                    },
                    readCallback: (_, __) => Testcalstatus_Calcnt_Read(_, __),
                    name: "Calcnt")
            .WithReservedBits(21, 8)
            .WithFlag(29, out testcalstatus_sleep_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testcalstatus_Sleep_ValueProvider(_);
                        return testcalstatus_sleep_bit.Value;               
                    },
                    readCallback: (_, __) => Testcalstatus_Sleep_Read(_, __),
                    name: "Sleep")
            .WithFlag(30, out testcalstatus_calactive_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testcalstatus_Calactive_ValueProvider(_);
                        return testcalstatus_calactive_bit.Value;               
                    },
                    readCallback: (_, __) => Testcalstatus_Calactive_Read(_, __),
                    name: "Calactive")
            .WithFlag(31, out testcalstatus_tcactive_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testcalstatus_Tcactive_ValueProvider(_);
                        return testcalstatus_tcactive_bit.Value;               
                    },
                    readCallback: (_, __) => Testcalstatus_Tcactive_Read(_, __),
                    name: "Tcactive")
            .WithReadCallback((_, __) => Testcalstatus_Read(_, __))
            .WithWriteCallback((_, __) => Testcalstatus_Write(_, __));
        
        // Testtcstatus - Offset : 0x4C
        protected DoubleWordRegister  GenerateTesttcstatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out testtcstatus_tccntcal_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testtcstatus_Tccntcal_ValueProvider(_);
                        return testtcstatus_tccntcal_field.Value;               
                    },
                    readCallback: (_, __) => Testtcstatus_Tccntcal_Read(_, __),
                    name: "Tccntcal")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Testtcstatus_Read(_, __))
            .WithWriteCallback((_, __) => Testtcstatus_Write(_, __));
        
        // Testtclatest - Offset : 0x50
        protected DoubleWordRegister  GenerateTesttclatestRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out testtclatest_tccntlatest_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testtclatest_Tccntlatest_ValueProvider(_);
                        return testtclatest_tccntlatest_field.Value;               
                    },
                    readCallback: (_, __) => Testtclatest_Tccntlatest_Read(_, __),
                    name: "Tccntlatest")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Testtclatest_Read(_, __))
            .WithWriteCallback((_, __) => Testtclatest_Write(_, __));
        
        // Testcalres - Offset : 0x54
        protected DoubleWordRegister  GenerateTestcalresRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 8, out testcalres_calressdm_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testcalres_Calressdm_ValueProvider(_);
                        return testcalres_calressdm_field.Value;               
                    },
                    readCallback: (_, __) => Testcalres_Calressdm_Read(_, __),
                    name: "Calressdm")
            .WithValueField(8, 6, out testcalres_calresfine_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Testcalres_Calresfine_ValueProvider(_);
                        return testcalres_calresfine_field.Value;               
                    },
                    readCallback: (_, __) => Testcalres_Calresfine_Read(_, __),
                    name: "Calresfine")
            .WithReservedBits(14, 18)
            .WithReadCallback((_, __) => Testcalres_Read(_, __))
            .WithWriteCallback((_, __) => Testcalres_Write(_, __));
        
        // Testcmd - Offset : 0x58
        protected DoubleWordRegister  GenerateTestcmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out testcmd_starttc_bit, FieldMode.Write,
                    writeCallback: (_, __) => Testcmd_Starttc_Write(_, __),
                    name: "Starttc")
            .WithFlag(1, out testcmd_startcal_bit, FieldMode.Write,
                    writeCallback: (_, __) => Testcmd_Startcal_Write(_, __),
                    name: "Startcal")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Testcmd_Read(_, __))
            .WithWriteCallback((_, __) => Testcmd_Write(_, __));
        
        // Defeat - Offset : 0x5C
        protected DoubleWordRegister  GenerateDefeatRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out defeat_highprec_bit, 
                    valueProviderCallback: (_) => {
                        Defeat_Highprec_ValueProvider(_);
                        return defeat_highprec_bit.Value;               
                    },
                    writeCallback: (_, __) => Defeat_Highprec_Write(_, __),
                    readCallback: (_, __) => Defeat_Highprec_Read(_, __),
                    name: "Highprec")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Defeat_Read(_, __))
            .WithWriteCallback((_, __) => Defeat_Write(_, __));
        

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



        // Ipversion - Offset : 0x0
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);

        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        // Ctrl - Offset : 0x4
        protected IFlagRegisterField ctrl_forceen_bit;
        partial void Ctrl_Forceen_Write(bool a, bool b);
        partial void Ctrl_Forceen_Read(bool a, bool b);
        partial void Ctrl_Forceen_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_disondemand_bit;
        partial void Ctrl_Disondemand_Write(bool a, bool b);
        partial void Ctrl_Disondemand_Read(bool a, bool b);
        partial void Ctrl_Disondemand_ValueProvider(bool a);

        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Status - Offset : 0x8
        protected IFlagRegisterField status_rdy_bit;
        partial void Status_Rdy_Read(bool a, bool b);
        partial void Status_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField status_ens_bit;
        partial void Status_Ens_Read(bool a, bool b);
        partial void Status_Ens_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // If - Offset : 0x14
        protected IFlagRegisterField if_rdy_bit;
        partial void If_Rdy_Write(bool a, bool b);
        partial void If_Rdy_Read(bool a, bool b);
        partial void If_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField if_posedge_bit;
        partial void If_Posedge_Write(bool a, bool b);
        partial void If_Posedge_Read(bool a, bool b);
        partial void If_Posedge_ValueProvider(bool a);
        protected IFlagRegisterField if_negedge_bit;
        partial void If_Negedge_Write(bool a, bool b);
        partial void If_Negedge_Read(bool a, bool b);
        partial void If_Negedge_ValueProvider(bool a);
        protected IFlagRegisterField if_tcdone_bit;
        partial void If_Tcdone_Write(bool a, bool b);
        partial void If_Tcdone_Read(bool a, bool b);
        partial void If_Tcdone_ValueProvider(bool a);
        protected IFlagRegisterField if_caldone_bit;
        partial void If_Caldone_Write(bool a, bool b);
        partial void If_Caldone_Read(bool a, bool b);
        partial void If_Caldone_ValueProvider(bool a);
        protected IFlagRegisterField if_tempchange_bit;
        partial void If_Tempchange_Write(bool a, bool b);
        partial void If_Tempchange_Read(bool a, bool b);
        partial void If_Tempchange_ValueProvider(bool a);
        protected IFlagRegisterField if_schederr_bit;
        partial void If_Schederr_Write(bool a, bool b);
        partial void If_Schederr_Read(bool a, bool b);
        partial void If_Schederr_ValueProvider(bool a);
        protected IFlagRegisterField if_tcoor_bit;
        partial void If_Tcoor_Write(bool a, bool b);
        partial void If_Tcoor_Read(bool a, bool b);
        partial void If_Tcoor_ValueProvider(bool a);
        protected IFlagRegisterField if_caloor_bit;
        partial void If_Caloor_Write(bool a, bool b);
        partial void If_Caloor_Read(bool a, bool b);
        partial void If_Caloor_ValueProvider(bool a);

        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x18
        protected IFlagRegisterField ien_rdy_bit;
        partial void Ien_Rdy_Write(bool a, bool b);
        partial void Ien_Rdy_Read(bool a, bool b);
        partial void Ien_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField ien_posedge_bit;
        partial void Ien_Posedge_Write(bool a, bool b);
        partial void Ien_Posedge_Read(bool a, bool b);
        partial void Ien_Posedge_ValueProvider(bool a);
        protected IFlagRegisterField ien_negedge_bit;
        partial void Ien_Negedge_Write(bool a, bool b);
        partial void Ien_Negedge_Read(bool a, bool b);
        partial void Ien_Negedge_ValueProvider(bool a);
        protected IFlagRegisterField ien_tcdone_bit;
        partial void Ien_Tcdone_Write(bool a, bool b);
        partial void Ien_Tcdone_Read(bool a, bool b);
        partial void Ien_Tcdone_ValueProvider(bool a);
        protected IFlagRegisterField ien_caldone_bit;
        partial void Ien_Caldone_Write(bool a, bool b);
        partial void Ien_Caldone_Read(bool a, bool b);
        partial void Ien_Caldone_ValueProvider(bool a);
        protected IFlagRegisterField ien_tempchange_bit;
        partial void Ien_Tempchange_Write(bool a, bool b);
        partial void Ien_Tempchange_Read(bool a, bool b);
        partial void Ien_Tempchange_ValueProvider(bool a);
        protected IFlagRegisterField ien_schederr_bit;
        partial void Ien_Schederr_Write(bool a, bool b);
        partial void Ien_Schederr_Read(bool a, bool b);
        partial void Ien_Schederr_ValueProvider(bool a);
        protected IFlagRegisterField ien_tcoor_bit;
        partial void Ien_Tcoor_Write(bool a, bool b);
        partial void Ien_Tcoor_Read(bool a, bool b);
        partial void Ien_Tcoor_ValueProvider(bool a);
        protected IFlagRegisterField ien_caloor_bit;
        partial void Ien_Caloor_Write(bool a, bool b);
        partial void Ien_Caloor_Read(bool a, bool b);
        partial void Ien_Caloor_ValueProvider(bool a);

        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Lock - Offset : 0x20
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // Cfg - Offset : 0x24
        protected IFlagRegisterField cfg_highprecen_bit;
        partial void Cfg_Highprecen_Write(bool a, bool b);
        partial void Cfg_Highprecen_Read(bool a, bool b);
        partial void Cfg_Highprecen_ValueProvider(bool a);

        partial void Cfg_Write(uint a, uint b);
        partial void Cfg_Read(uint a, uint b);
        
        // Hiprecision - Offset : 0x28
        protected IValueRegisterField hiprecision_calwnd_field;
        partial void Hiprecision_Calwnd_Write(ulong a, ulong b);
        partial void Hiprecision_Calwnd_Read(ulong a, ulong b);
        partial void Hiprecision_Calwnd_ValueProvider(ulong a);
        protected IValueRegisterField hiprecision_tcwnd_field;
        partial void Hiprecision_Tcwnd_Write(ulong a, ulong b);
        partial void Hiprecision_Tcwnd_Read(ulong a, ulong b);
        partial void Hiprecision_Tcwnd_ValueProvider(ulong a);
        protected IValueRegisterField hiprecision_tcthresh_field;
        partial void Hiprecision_Tcthresh_Write(ulong a, ulong b);
        partial void Hiprecision_Tcthresh_Read(ulong a, ulong b);
        partial void Hiprecision_Tcthresh_ValueProvider(ulong a);
        protected IValueRegisterField hiprecision_tcintshort_field;
        partial void Hiprecision_Tcintshort_Write(ulong a, ulong b);
        partial void Hiprecision_Tcintshort_Read(ulong a, ulong b);
        partial void Hiprecision_Tcintshort_ValueProvider(ulong a);
        protected IValueRegisterField hiprecision_tcintlong_field;
        partial void Hiprecision_Tcintlong_Write(ulong a, ulong b);
        partial void Hiprecision_Tcintlong_Read(ulong a, ulong b);
        partial void Hiprecision_Tcintlong_ValueProvider(ulong a);
        protected IValueRegisterField hiprecision_calint_field;
        partial void Hiprecision_Calint_Write(ulong a, ulong b);
        partial void Hiprecision_Calint_Read(ulong a, ulong b);
        partial void Hiprecision_Calint_ValueProvider(ulong a);
        protected IValueRegisterField hiprecision_tcintthresh_field;
        partial void Hiprecision_Tcintthresh_Write(ulong a, ulong b);
        partial void Hiprecision_Tcintthresh_Read(ulong a, ulong b);
        partial void Hiprecision_Tcintthresh_ValueProvider(ulong a);

        partial void Hiprecision_Write(uint a, uint b);
        partial void Hiprecision_Read(uint a, uint b);
        
        // Nomcal - Offset : 0x2C
        protected IValueRegisterField nomcal_nomcalcnt_field;
        partial void Nomcal_Nomcalcnt_Write(ulong a, ulong b);
        partial void Nomcal_Nomcalcnt_Read(ulong a, ulong b);
        partial void Nomcal_Nomcalcnt_ValueProvider(ulong a);

        partial void Nomcal_Write(uint a, uint b);
        partial void Nomcal_Read(uint a, uint b);
        
        // Nomcalinv - Offset : 0x30
        protected IValueRegisterField nomcalinv_nomcalcntinv_field;
        partial void Nomcalinv_Nomcalcntinv_Write(ulong a, ulong b);
        partial void Nomcalinv_Nomcalcntinv_Read(ulong a, ulong b);
        partial void Nomcalinv_Nomcalcntinv_ValueProvider(ulong a);

        partial void Nomcalinv_Write(uint a, uint b);
        partial void Nomcalinv_Read(uint a, uint b);
        
        // Cmd - Offset : 0x34
        protected IFlagRegisterField cmd_reducetcint_bit;
        partial void Cmd_Reducetcint_Write(bool a, bool b);
        partial void Cmd_Reducetcint_ValueProvider(bool a);

        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);
        
        // Trim - Offset : 0x38
        protected IValueRegisterField trim_trimfine_field;
        partial void Trim_Trimfine_Write(ulong a, ulong b);
        partial void Trim_Trimfine_Read(ulong a, ulong b);
        partial void Trim_Trimfine_ValueProvider(ulong a);
        protected IValueRegisterField trim_trim_field;
        partial void Trim_Trim_Write(ulong a, ulong b);
        partial void Trim_Trim_Read(ulong a, ulong b);
        partial void Trim_Trim_ValueProvider(ulong a);
        protected IValueRegisterField trim_trimcoarse_field;
        partial void Trim_Trimcoarse_Write(ulong a, ulong b);
        partial void Trim_Trimcoarse_Read(ulong a, ulong b);
        partial void Trim_Trimcoarse_ValueProvider(ulong a);
        protected IValueRegisterField trim_bias_field;
        partial void Trim_Bias_Write(ulong a, ulong b);
        partial void Trim_Bias_Read(ulong a, ulong b);
        partial void Trim_Bias_ValueProvider(ulong a);
        protected IFlagRegisterField trim_tempcob_bit;
        partial void Trim_Tempcob_Write(bool a, bool b);
        partial void Trim_Tempcob_Read(bool a, bool b);
        partial void Trim_Tempcob_ValueProvider(bool a);
        protected IFlagRegisterField trim_tempcoc_bit;
        partial void Trim_Tempcoc_Write(bool a, bool b);
        partial void Trim_Tempcoc_Read(bool a, bool b);
        partial void Trim_Tempcoc_ValueProvider(bool a);
        protected IValueRegisterField trim_roten_field;
        partial void Trim_Roten_Write(ulong a, ulong b);
        partial void Trim_Roten_Read(ulong a, ulong b);
        partial void Trim_Roten_ValueProvider(ulong a);
        protected IValueRegisterField trim_timeout_field;
        partial void Trim_Timeout_Write(ulong a, ulong b);
        partial void Trim_Timeout_Read(ulong a, ulong b);
        partial void Trim_Timeout_ValueProvider(ulong a);

        partial void Trim_Write(uint a, uint b);
        partial void Trim_Read(uint a, uint b);
        
        // Trimhiprec - Offset : 0x3C
        protected IValueRegisterField trimhiprec_trimtemp_field;
        partial void Trimhiprec_Trimtemp_Write(ulong a, ulong b);
        partial void Trimhiprec_Trimtemp_Read(ulong a, ulong b);
        partial void Trimhiprec_Trimtemp_ValueProvider(ulong a);
        protected IValueRegisterField trimhiprec_trimhiprec_field;
        partial void Trimhiprec_Trimhiprec_Write(ulong a, ulong b);
        partial void Trimhiprec_Trimhiprec_Read(ulong a, ulong b);
        partial void Trimhiprec_Trimhiprec_ValueProvider(ulong a);
        protected IValueRegisterField trimhiprec_biashiprec_field;
        partial void Trimhiprec_Biashiprec_Write(ulong a, ulong b);
        partial void Trimhiprec_Biashiprec_Read(ulong a, ulong b);
        partial void Trimhiprec_Biashiprec_ValueProvider(ulong a);
        protected IFlagRegisterField trimhiprec_tempcobhiprec_bit;
        partial void Trimhiprec_Tempcobhiprec_Write(bool a, bool b);
        partial void Trimhiprec_Tempcobhiprec_Read(bool a, bool b);
        partial void Trimhiprec_Tempcobhiprec_ValueProvider(bool a);
        protected IFlagRegisterField trimhiprec_tempcochiprec_bit;
        partial void Trimhiprec_Tempcochiprec_Write(bool a, bool b);
        partial void Trimhiprec_Tempcochiprec_Read(bool a, bool b);
        partial void Trimhiprec_Tempcochiprec_ValueProvider(bool a);
        protected IValueRegisterField trimhiprec_rotenhiprec_field;
        partial void Trimhiprec_Rotenhiprec_Write(ulong a, ulong b);
        partial void Trimhiprec_Rotenhiprec_Read(ulong a, ulong b);
        partial void Trimhiprec_Rotenhiprec_ValueProvider(ulong a);

        partial void Trimhiprec_Write(uint a, uint b);
        partial void Trimhiprec_Read(uint a, uint b);
        
        // Stepsize - Offset : 0x40
        protected IValueRegisterField stepsize_stepoffset_field;
        partial void Stepsize_Stepoffset_Write(ulong a, ulong b);
        partial void Stepsize_Stepoffset_Read(ulong a, ulong b);
        partial void Stepsize_Stepoffset_ValueProvider(ulong a);
        protected IValueRegisterField stepsize_stepgain_field;
        partial void Stepsize_Stepgain_Write(ulong a, ulong b);
        partial void Stepsize_Stepgain_Read(ulong a, ulong b);
        partial void Stepsize_Stepgain_ValueProvider(ulong a);

        partial void Stepsize_Write(uint a, uint b);
        partial void Stepsize_Read(uint a, uint b);
        
        // Testctrl - Offset : 0x44
        protected IFlagRegisterField testctrl_autodis_bit;
        partial void Testctrl_Autodis_Write(bool a, bool b);
        partial void Testctrl_Autodis_Read(bool a, bool b);
        partial void Testctrl_Autodis_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_tempbasedcaldis_bit;
        partial void Testctrl_Tempbasedcaldis_Write(bool a, bool b);
        partial void Testctrl_Tempbasedcaldis_Read(bool a, bool b);
        partial void Testctrl_Tempbasedcaldis_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_calovren_bit;
        partial void Testctrl_Calovren_Write(bool a, bool b);
        partial void Testctrl_Calovren_Read(bool a, bool b);
        partial void Testctrl_Calovren_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_temposcovren_bit;
        partial void Testctrl_Temposcovren_Write(bool a, bool b);
        partial void Testctrl_Temposcovren_Read(bool a, bool b);
        partial void Testctrl_Temposcovren_ValueProvider(bool a);
        protected IValueRegisterField testctrl_calsdminovr_field;
        partial void Testctrl_Calsdminovr_Write(ulong a, ulong b);
        partial void Testctrl_Calsdminovr_Read(ulong a, ulong b);
        partial void Testctrl_Calsdminovr_ValueProvider(ulong a);
        protected IValueRegisterField testctrl_calfineovr_field;
        partial void Testctrl_Calfineovr_Write(ulong a, ulong b);
        partial void Testctrl_Calfineovr_Read(ulong a, ulong b);
        partial void Testctrl_Calfineovr_ValueProvider(ulong a);
        protected IFlagRegisterField testctrl_calsdmoutovr_bit;
        partial void Testctrl_Calsdmoutovr_Write(bool a, bool b);
        partial void Testctrl_Calsdmoutovr_Read(bool a, bool b);
        partial void Testctrl_Calsdmoutovr_ValueProvider(bool a);
        protected IValueRegisterField testctrl_temposcctrlovr_field;
        partial void Testctrl_Temposcctrlovr_Write(ulong a, ulong b);
        partial void Testctrl_Temposcctrlovr_Read(ulong a, ulong b);
        partial void Testctrl_Temposcctrlovr_ValueProvider(ulong a);

        partial void Testctrl_Write(uint a, uint b);
        partial void Testctrl_Read(uint a, uint b);
        
        // Testcalstatus - Offset : 0x48
        protected IValueRegisterField testcalstatus_calcnt_field;
        partial void Testcalstatus_Calcnt_Read(ulong a, ulong b);
        partial void Testcalstatus_Calcnt_ValueProvider(ulong a);
        protected IFlagRegisterField testcalstatus_sleep_bit;
        partial void Testcalstatus_Sleep_Read(bool a, bool b);
        partial void Testcalstatus_Sleep_ValueProvider(bool a);
        protected IFlagRegisterField testcalstatus_calactive_bit;
        partial void Testcalstatus_Calactive_Read(bool a, bool b);
        partial void Testcalstatus_Calactive_ValueProvider(bool a);
        protected IFlagRegisterField testcalstatus_tcactive_bit;
        partial void Testcalstatus_Tcactive_Read(bool a, bool b);
        partial void Testcalstatus_Tcactive_ValueProvider(bool a);

        partial void Testcalstatus_Write(uint a, uint b);
        partial void Testcalstatus_Read(uint a, uint b);
        
        // Testtcstatus - Offset : 0x4C
        protected IValueRegisterField testtcstatus_tccntcal_field;
        partial void Testtcstatus_Tccntcal_Read(ulong a, ulong b);
        partial void Testtcstatus_Tccntcal_ValueProvider(ulong a);

        partial void Testtcstatus_Write(uint a, uint b);
        partial void Testtcstatus_Read(uint a, uint b);
        
        // Testtclatest - Offset : 0x50
        protected IValueRegisterField testtclatest_tccntlatest_field;
        partial void Testtclatest_Tccntlatest_Read(ulong a, ulong b);
        partial void Testtclatest_Tccntlatest_ValueProvider(ulong a);

        partial void Testtclatest_Write(uint a, uint b);
        partial void Testtclatest_Read(uint a, uint b);
        
        // Testcalres - Offset : 0x54
        protected IValueRegisterField testcalres_calressdm_field;
        partial void Testcalres_Calressdm_Read(ulong a, ulong b);
        partial void Testcalres_Calressdm_ValueProvider(ulong a);
        protected IValueRegisterField testcalres_calresfine_field;
        partial void Testcalres_Calresfine_Read(ulong a, ulong b);
        partial void Testcalres_Calresfine_ValueProvider(ulong a);

        partial void Testcalres_Write(uint a, uint b);
        partial void Testcalres_Read(uint a, uint b);
        
        // Testcmd - Offset : 0x58
        protected IFlagRegisterField testcmd_starttc_bit;
        partial void Testcmd_Starttc_Write(bool a, bool b);
        partial void Testcmd_Starttc_ValueProvider(bool a);
        protected IFlagRegisterField testcmd_startcal_bit;
        partial void Testcmd_Startcal_Write(bool a, bool b);
        partial void Testcmd_Startcal_ValueProvider(bool a);

        partial void Testcmd_Write(uint a, uint b);
        partial void Testcmd_Read(uint a, uint b);
        
        // Defeat - Offset : 0x5C
        protected IFlagRegisterField defeat_highprec_bit;
        partial void Defeat_Highprec_Write(bool a, bool b);
        partial void Defeat_Highprec_Read(bool a, bool b);
        partial void Defeat_Highprec_ValueProvider(bool a);

        partial void Defeat_Write(uint a, uint b);
        partial void Defeat_Read(uint a, uint b);
        
        partial void LFRCO_Reset();

        partial void EFR32xG2_LFRCO_2_Constructor();

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
            Ctrl = 0x4,
            Status = 0x8,
            If = 0x14,
            Ien = 0x18,
            Lock = 0x20,
            Cfg = 0x24,
            Hiprecision = 0x28,
            Nomcal = 0x2C,
            Nomcalinv = 0x30,
            Cmd = 0x34,
            Trim = 0x38,
            Trimhiprec = 0x3C,
            Stepsize = 0x40,
            Testctrl = 0x44,
            Testcalstatus = 0x48,
            Testtcstatus = 0x4C,
            Testtclatest = 0x50,
            Testcalres = 0x54,
            Testcmd = 0x58,
            Defeat = 0x5C,
            
            Ipversion_SET = 0x1000,
            Ctrl_SET = 0x1004,
            Status_SET = 0x1008,
            If_SET = 0x1014,
            Ien_SET = 0x1018,
            Lock_SET = 0x1020,
            Cfg_SET = 0x1024,
            Hiprecision_SET = 0x1028,
            Nomcal_SET = 0x102C,
            Nomcalinv_SET = 0x1030,
            Cmd_SET = 0x1034,
            Trim_SET = 0x1038,
            Trimhiprec_SET = 0x103C,
            Stepsize_SET = 0x1040,
            Testctrl_SET = 0x1044,
            Testcalstatus_SET = 0x1048,
            Testtcstatus_SET = 0x104C,
            Testtclatest_SET = 0x1050,
            Testcalres_SET = 0x1054,
            Testcmd_SET = 0x1058,
            Defeat_SET = 0x105C,
            
            Ipversion_CLR = 0x2000,
            Ctrl_CLR = 0x2004,
            Status_CLR = 0x2008,
            If_CLR = 0x2014,
            Ien_CLR = 0x2018,
            Lock_CLR = 0x2020,
            Cfg_CLR = 0x2024,
            Hiprecision_CLR = 0x2028,
            Nomcal_CLR = 0x202C,
            Nomcalinv_CLR = 0x2030,
            Cmd_CLR = 0x2034,
            Trim_CLR = 0x2038,
            Trimhiprec_CLR = 0x203C,
            Stepsize_CLR = 0x2040,
            Testctrl_CLR = 0x2044,
            Testcalstatus_CLR = 0x2048,
            Testtcstatus_CLR = 0x204C,
            Testtclatest_CLR = 0x2050,
            Testcalres_CLR = 0x2054,
            Testcmd_CLR = 0x2058,
            Defeat_CLR = 0x205C,
            
            Ipversion_TGL = 0x3000,
            Ctrl_TGL = 0x3004,
            Status_TGL = 0x3008,
            If_TGL = 0x3014,
            Ien_TGL = 0x3018,
            Lock_TGL = 0x3020,
            Cfg_TGL = 0x3024,
            Hiprecision_TGL = 0x3028,
            Nomcal_TGL = 0x302C,
            Nomcalinv_TGL = 0x3030,
            Cmd_TGL = 0x3034,
            Trim_TGL = 0x3038,
            Trimhiprec_TGL = 0x303C,
            Stepsize_TGL = 0x3040,
            Testctrl_TGL = 0x3044,
            Testcalstatus_TGL = 0x3048,
            Testtcstatus_TGL = 0x304C,
            Testtclatest_TGL = 0x3050,
            Testcalres_TGL = 0x3054,
            Testcmd_TGL = 0x3058,
            Defeat_TGL = 0x305C,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}