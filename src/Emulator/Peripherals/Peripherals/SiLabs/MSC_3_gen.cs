/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    MSC, Generated on : 2023-07-20 14:29:06.785700
    MSC, ID Version : 018b851d846949b488e40a8c6cae7220.3 */

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

namespace Antmicro.Renode.Peripherals.Silabs
{
    public partial class Msc_3
    {
        public Msc_3(Machine machine) : base(machine)
        {
            Msc_3_constructor();
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


namespace Antmicro.Renode.Peripherals.Silabs
{
    public partial class Msc_3 : BasicDoubleWordPeripheral, IKnownSize
    {
        public Msc_3(Machine machine) : base(machine)
        {
            Define_Registers();
            Msc_3_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Readctrl, GenerateReadctrlRegister()},
                {(long)Registers.Rdatactrl, GenerateRdatactrlRegister()},
                {(long)Registers.Writectrl, GenerateWritectrlRegister()},
                {(long)Registers.Writecmd, GenerateWritecmdRegister()},
                {(long)Registers.Addrb, GenerateAddrbRegister()},
                {(long)Registers.Wdata, GenerateWdataRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Userdatasize, GenerateUserdatasizeRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Misclockword, GenerateMisclockwordRegister()},
                {(long)Registers.Pwrctrl, GeneratePwrctrlRegister()},
                {(long)Registers.Sewritectrl, GenerateSewritectrlRegister()},
                {(long)Registers.Sewritecmd, GenerateSewritecmdRegister()},
                {(long)Registers.Seaddrb, GenerateSeaddrbRegister()},
                {(long)Registers.Sewdata, GenerateSewdataRegister()},
                {(long)Registers.Sestatus, GenerateSestatusRegister()},
                {(long)Registers.Seif, GenerateSeifRegister()},
                {(long)Registers.Seien, GenerateSeienRegister()},
                {(long)Registers.Memfeature, GenerateMemfeatureRegister()},
                {(long)Registers.Startup, GenerateStartupRegister()},
                {(long)Registers.Serdatactrl, GenerateSerdatactrlRegister()},
                {(long)Registers.Sepwrskip, GenerateSepwrskipRegister()},
                {(long)Registers.Mtpctrl, GenerateMtpctrlRegister()},
                {(long)Registers.Mtpsize, GenerateMtpsizeRegister()},
                {(long)Registers.Otperase, GenerateOtperaseRegister()},
                {(long)Registers.Flasherasetime, GenerateFlasherasetimeRegister()},
                {(long)Registers.Flashprogtime, GenerateFlashprogtimeRegister()},
                {(long)Registers.Selock, GenerateSelockRegister()},
                {(long)Registers.Pagelock0, GeneratePagelock0Register()},
                {(long)Registers.Pagelock1, GeneratePagelock1Register()},
                {(long)Registers.Pagelock2, GeneratePagelock2Register()},
                {(long)Registers.Pagelock3, GeneratePagelock3Register()},
                {(long)Registers.Pagelock4, GeneratePagelock4Register()},
                {(long)Registers.Pagelock5, GeneratePagelock5Register()},
                {(long)Registers.Repaddr0_Repinst, GenerateRepaddr0_repinstRegister()},
                {(long)Registers.Repaddr1_Repinst, GenerateRepaddr1_repinstRegister()},
                {(long)Registers.Dout0_Doutinst, GenerateDout0_doutinstRegister()},
                {(long)Registers.Dout1_Doutinst, GenerateDout1_doutinstRegister()},
                {(long)Registers.Testctrl_Flashtest, GenerateTestctrl_flashtestRegister()},
                {(long)Registers.Patdiagctrl_Flashtest, GeneratePatdiagctrl_flashtestRegister()},
                {(long)Registers.Pataddr_Flashtest, GeneratePataddr_flashtestRegister()},
                {(long)Registers.Patdoneaddr_Flashtest, GeneratePatdoneaddr_flashtestRegister()},
                {(long)Registers.Patstatus_Flashtest, GeneratePatstatus_flashtestRegister()},
                {(long)Registers.Testredundancy_Flashtest, GenerateTestredundancy_flashtestRegister()},
                {(long)Registers.Testlock_Flashtest, GenerateTestlock_flashtestRegister()},
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
            MSC_Reset();
        }
        
        protected enum READCTRL_MODE
        {
            WS0 = 0, // Zero wait-states inserted in fetch or read transfers
            WS1 = 1, // One wait-state inserted for each fetch or read transfer. See Flash Wait-States table for details
            WS2 = 2, // Two wait-states inserted for eatch fetch or read transfer. See Flash Wait-States table for details
            WS3 = 3, // Three wait-states inserted for eatch fetch or read transfer. See Flash Wait-States table for details
        }
        
        protected enum STATUS_REGLOCK
        {
            UNLOCKED = 0, // 
            LOCKED = 1, // 
        }
        
        protected enum SESTATUS_ROOTLOCK
        {
            UNLOCKED = 0, // 
            LOCKED = 1, // 
        }
        
        protected enum MEMFEATURE_FLASHSIZE
        {
            F16K = 4, // 16kB flash
            F32K = 5, // 32kB flash
            F64K = 6, // 64kB flash
            F128K = 7, // 128kB flash
            F256K = 8, // 256kB flash
            F512K = 9, // 512kB flash
            F768K = 10, // 768KB flash
            F1024K = 11, // 1024kB flash
            F2048K = 12, // 2048kB flash
            F352K = 13, // 352KB flash
            F1536K = 14, // 1536KB flash
        }
        
        protected enum MTPCTRL_MTPLOC
        {
            PAGE1 = 0, // 
            PAGE2 = 1, // 
        }
        
        protected enum SELOCK_SELOCKKEY
        {
            LOCK = 0, // 
            UNLOCK = 41950, // 
        }
        
        protected enum TESTCTRL_FLASHTEST_PATMODE
        {
            ALLONE = 0, // Check 1's pattern
            ALLZERO = 1, // Check 0's pattern
            DIAGONAL = 2, // Check diagonal pattern
            XOR = 3, // Check XOR pattern
            XNOR = 4, // Check XNOR pattern
            LXOR = 5, // Check Logic XOR pattern
            LXNOR = 6, // Check Logic XNOR pattern
        }
        
        protected enum TESTCTRL_FLASHTEST_SEMODE
        {
            SINGLECYCLETOGGLE = 0, // SE toggling every cycle
            DUALCYCLETOGGLE = 1, // SE toggling every two cycles
            NOTOGGLE = 2, // SE high all the time
        }
        
        protected enum TESTCTRL_FLASHTEST_XADRINC
        {
            ONE = 0, // 
            TWO = 1, // 
        }
        
        protected enum PATDIAGCTRL_FLASHTEST_INFODIAGINCR
        {
            DECR = 0, // decrement mode
            INCR = 1, // increment mode
        }
        
        protected enum PATDIAGCTRL_FLASHTEST_MAINDIAGINCR
        {
            DECR = 0, // decrement mode
            INCR = 1, // increment mode
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
        
        // Readctrl - Offset : 0x4
        protected DoubleWordRegister  GenerateReadctrlRegister() => new DoubleWordRegister(this, 0x200000)
            .WithReservedBits(0, 20)
            .WithEnumField<DoubleWordRegister, READCTRL_MODE>(20, 2, out readctrl_mode_field, 
                    valueProviderCallback: (_) => {
                        Readctrl_Mode_ValueProvider(_);
                        return readctrl_mode_field.Value;               
                    },
                    writeCallback: (_, __) => Readctrl_Mode_Write(_, __),
                    readCallback: (_, __) => Readctrl_Mode_Read(_, __),
                    name: "Mode")
            .WithReservedBits(22, 10)
            .WithReadCallback((_, __) => Readctrl_Read(_, __))
            .WithWriteCallback((_, __) => Readctrl_Write(_, __));
        
        // Rdatactrl - Offset : 0x8
        protected DoubleWordRegister  GenerateRdatactrlRegister() => new DoubleWordRegister(this, 0x1000)
            .WithReservedBits(0, 1)
            .WithFlag(1, out rdatactrl_afdis_bit, 
                    valueProviderCallback: (_) => {
                        Rdatactrl_Afdis_ValueProvider(_);
                        return rdatactrl_afdis_bit.Value;               
                    },
                    writeCallback: (_, __) => Rdatactrl_Afdis_Write(_, __),
                    readCallback: (_, __) => Rdatactrl_Afdis_Read(_, __),
                    name: "Afdis")
            .WithReservedBits(2, 10)
            .WithFlag(12, out rdatactrl_doutbufen_bit, 
                    valueProviderCallback: (_) => {
                        Rdatactrl_Doutbufen_ValueProvider(_);
                        return rdatactrl_doutbufen_bit.Value;               
                    },
                    writeCallback: (_, __) => Rdatactrl_Doutbufen_Write(_, __),
                    readCallback: (_, __) => Rdatactrl_Doutbufen_Read(_, __),
                    name: "Doutbufen")
            .WithReservedBits(13, 19)
            .WithReadCallback((_, __) => Rdatactrl_Read(_, __))
            .WithWriteCallback((_, __) => Rdatactrl_Write(_, __));
        
        // Writectrl - Offset : 0xC
        protected DoubleWordRegister  GenerateWritectrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out writectrl_wren_bit, 
                    valueProviderCallback: (_) => {
                        Writectrl_Wren_ValueProvider(_);
                        return writectrl_wren_bit.Value;               
                    },
                    writeCallback: (_, __) => Writectrl_Wren_Write(_, __),
                    readCallback: (_, __) => Writectrl_Wren_Read(_, __),
                    name: "Wren")
            .WithFlag(1, out writectrl_irqeraseabort_bit, 
                    valueProviderCallback: (_) => {
                        Writectrl_Irqeraseabort_ValueProvider(_);
                        return writectrl_irqeraseabort_bit.Value;               
                    },
                    writeCallback: (_, __) => Writectrl_Irqeraseabort_Write(_, __),
                    readCallback: (_, __) => Writectrl_Irqeraseabort_Read(_, __),
                    name: "Irqeraseabort")
            .WithReservedBits(2, 1)
            .WithFlag(3, out writectrl_lpwrite_bit, 
                    valueProviderCallback: (_) => {
                        Writectrl_Lpwrite_ValueProvider(_);
                        return writectrl_lpwrite_bit.Value;               
                    },
                    writeCallback: (_, __) => Writectrl_Lpwrite_Write(_, __),
                    readCallback: (_, __) => Writectrl_Lpwrite_Read(_, __),
                    name: "Lpwrite")
            .WithReservedBits(4, 12)
            .WithValueField(16, 10, out writectrl_rangecount_field, 
                    valueProviderCallback: (_) => {
                        Writectrl_Rangecount_ValueProvider(_);
                        return writectrl_rangecount_field.Value;               
                    },
                    writeCallback: (_, __) => Writectrl_Rangecount_Write(_, __),
                    readCallback: (_, __) => Writectrl_Rangecount_Read(_, __),
                    name: "Rangecount")
            .WithReservedBits(26, 6)
            .WithReadCallback((_, __) => Writectrl_Read(_, __))
            .WithWriteCallback((_, __) => Writectrl_Write(_, __));
        
        // Writecmd - Offset : 0x10
        protected DoubleWordRegister  GenerateWritecmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out writecmd_erasepage_bit, FieldMode.Write,
                    writeCallback: (_, __) => Writecmd_Erasepage_Write(_, __),
                    name: "Erasepage")
            .WithFlag(2, out writecmd_writeend_bit, FieldMode.Write,
                    writeCallback: (_, __) => Writecmd_Writeend_Write(_, __),
                    name: "Writeend")
            .WithReservedBits(3, 1)
            .WithFlag(4, out writecmd_eraserange_bit, FieldMode.Write,
                    writeCallback: (_, __) => Writecmd_Eraserange_Write(_, __),
                    name: "Eraserange")
            .WithFlag(5, out writecmd_eraseabort_bit, FieldMode.Write,
                    writeCallback: (_, __) => Writecmd_Eraseabort_Write(_, __),
                    name: "Eraseabort")
            .WithReservedBits(6, 2)
            .WithFlag(8, out writecmd_erasemain0_bit, FieldMode.Write,
                    writeCallback: (_, __) => Writecmd_Erasemain0_Write(_, __),
                    name: "Erasemain0")
            .WithReservedBits(9, 3)
            .WithFlag(12, out writecmd_clearwdata_bit, FieldMode.Write,
                    writeCallback: (_, __) => Writecmd_Clearwdata_Write(_, __),
                    name: "Clearwdata")
            .WithReservedBits(13, 19)
            .WithReadCallback((_, __) => Writecmd_Read(_, __))
            .WithWriteCallback((_, __) => Writecmd_Write(_, __));
        
        // Addrb - Offset : 0x14
        protected DoubleWordRegister  GenerateAddrbRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out addrb_addrb_field, 
                    valueProviderCallback: (_) => {
                        Addrb_Addrb_ValueProvider(_);
                        return addrb_addrb_field.Value;               
                    },
                    writeCallback: (_, __) => Addrb_Addrb_Write(_, __),
                    readCallback: (_, __) => Addrb_Addrb_Read(_, __),
                    name: "Addrb")
            .WithReadCallback((_, __) => Addrb_Read(_, __))
            .WithWriteCallback((_, __) => Addrb_Write(_, __));
        
        // Wdata - Offset : 0x18
        protected DoubleWordRegister  GenerateWdataRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out wdata_dataw_field, 
                    valueProviderCallback: (_) => {
                        Wdata_Dataw_ValueProvider(_);
                        return wdata_dataw_field.Value;               
                    },
                    writeCallback: (_, __) => Wdata_Dataw_Write(_, __),
                    readCallback: (_, __) => Wdata_Dataw_Read(_, __),
                    name: "Dataw")
            .WithReadCallback((_, __) => Wdata_Read(_, __))
            .WithWriteCallback((_, __) => Wdata_Write(_, __));
        
        // Status - Offset : 0x1C
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x8000008)
            .WithFlag(0, out status_busy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Busy_ValueProvider(_);
                        return status_busy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Busy_Read(_, __),
                    name: "Busy")
            .WithFlag(1, out status_locked_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Locked_ValueProvider(_);
                        return status_locked_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Locked_Read(_, __),
                    name: "Locked")
            .WithFlag(2, out status_invaddr_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Invaddr_ValueProvider(_);
                        return status_invaddr_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Invaddr_Read(_, __),
                    name: "Invaddr")
            .WithFlag(3, out status_wdataready_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Wdataready_ValueProvider(_);
                        return status_wdataready_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Wdataready_Read(_, __),
                    name: "Wdataready")
            .WithFlag(4, out status_eraseaborted_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Eraseaborted_ValueProvider(_);
                        return status_eraseaborted_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Eraseaborted_Read(_, __),
                    name: "Eraseaborted")
            .WithFlag(5, out status_pending_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Pending_ValueProvider(_);
                        return status_pending_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Pending_Read(_, __),
                    name: "Pending")
            .WithFlag(6, out status_timeout_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Timeout_ValueProvider(_);
                        return status_timeout_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Timeout_Read(_, __),
                    name: "Timeout")
            .WithFlag(7, out status_rangepartial_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Rangepartial_ValueProvider(_);
                        return status_rangepartial_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Rangepartial_Read(_, __),
                    name: "Rangepartial")
            .WithReservedBits(8, 8)
            .WithEnumField<DoubleWordRegister, STATUS_REGLOCK>(16, 1, out status_reglock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Reglock_ValueProvider(_);
                        return status_reglock_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Reglock_Read(_, __),
                    name: "Reglock")
            .WithReservedBits(17, 7)
            .WithFlag(24, out status_pwron_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Pwron_ValueProvider(_);
                        return status_pwron_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Pwron_Read(_, __),
                    name: "Pwron")
            .WithReservedBits(25, 2)
            .WithFlag(27, out status_wready_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Wready_ValueProvider(_);
                        return status_wready_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Wready_Read(_, __),
                    name: "Wready")
            .WithValueField(28, 4, out status_pwrupckbdfailcount_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Pwrupckbdfailcount_ValueProvider(_);
                        return status_pwrupckbdfailcount_field.Value;               
                    },
                    readCallback: (_, __) => Status_Pwrupckbdfailcount_Read(_, __),
                    name: "Pwrupckbdfailcount")
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // If - Offset : 0x20
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_erase_bit, 
                    valueProviderCallback: (_) => {
                        If_Erase_ValueProvider(_);
                        return if_erase_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Erase_Write(_, __),
                    readCallback: (_, __) => If_Erase_Read(_, __),
                    name: "Erase")
            .WithFlag(1, out if_write_bit, 
                    valueProviderCallback: (_) => {
                        If_Write_ValueProvider(_);
                        return if_write_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Write_Write(_, __),
                    readCallback: (_, __) => If_Write_Read(_, __),
                    name: "Write")
            .WithFlag(2, out if_wdataov_bit, 
                    valueProviderCallback: (_) => {
                        If_Wdataov_ValueProvider(_);
                        return if_wdataov_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Wdataov_Write(_, __),
                    readCallback: (_, __) => If_Wdataov_Read(_, __),
                    name: "Wdataov")
            .WithReservedBits(3, 5)
            .WithFlag(8, out if_pwrupf_bit, 
                    valueProviderCallback: (_) => {
                        If_Pwrupf_ValueProvider(_);
                        return if_pwrupf_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Pwrupf_Write(_, __),
                    readCallback: (_, __) => If_Pwrupf_Read(_, __),
                    name: "Pwrupf")
            .WithFlag(9, out if_pwroff_bit, 
                    valueProviderCallback: (_) => {
                        If_Pwroff_ValueProvider(_);
                        return if_pwroff_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Pwroff_Write(_, __),
                    readCallback: (_, __) => If_Pwroff_Read(_, __),
                    name: "Pwroff")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x24
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_erase_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Erase_ValueProvider(_);
                        return ien_erase_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Erase_Write(_, __),
                    readCallback: (_, __) => Ien_Erase_Read(_, __),
                    name: "Erase")
            .WithFlag(1, out ien_write_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Write_ValueProvider(_);
                        return ien_write_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Write_Write(_, __),
                    readCallback: (_, __) => Ien_Write_Read(_, __),
                    name: "Write")
            .WithFlag(2, out ien_wdataov_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Wdataov_ValueProvider(_);
                        return ien_wdataov_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Wdataov_Write(_, __),
                    readCallback: (_, __) => Ien_Wdataov_Read(_, __),
                    name: "Wdataov")
            .WithReservedBits(3, 5)
            .WithFlag(8, out ien_pwrupf_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Pwrupf_ValueProvider(_);
                        return ien_pwrupf_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Pwrupf_Write(_, __),
                    readCallback: (_, __) => Ien_Pwrupf_Read(_, __),
                    name: "Pwrupf")
            .WithFlag(9, out ien_pwroff_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Pwroff_ValueProvider(_);
                        return ien_pwroff_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Pwroff_Write(_, __),
                    readCallback: (_, __) => Ien_Pwroff_Read(_, __),
                    name: "Pwroff")
            .WithReservedBits(10, 22)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Userdatasize - Offset : 0x34
        protected DoubleWordRegister  GenerateUserdatasizeRegister() => new DoubleWordRegister(this, 0x4)
            .WithValueField(0, 6, out userdatasize_userdatasize_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Userdatasize_Userdatasize_ValueProvider(_);
                        return userdatasize_userdatasize_field.Value;               
                    },
                    readCallback: (_, __) => Userdatasize_Userdatasize_Read(_, __),
                    name: "Userdatasize")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Userdatasize_Read(_, __))
            .WithWriteCallback((_, __) => Userdatasize_Write(_, __));
        
        // Cmd - Offset : 0x38
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cmd_pwrup_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Pwrup_Write(_, __),
                    name: "Pwrup")
            .WithReservedBits(1, 3)
            .WithFlag(4, out cmd_pwroff_bit, FieldMode.Write,
                    writeCallback: (_, __) => Cmd_Pwroff_Write(_, __),
                    name: "Pwroff")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write(_, __));
        
        // Lock - Offset : 0x3C
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Misclockword - Offset : 0x40
        protected DoubleWordRegister  GenerateMisclockwordRegister() => new DoubleWordRegister(this, 0x11)
            .WithFlag(0, out misclockword_melockbit_bit, 
                    valueProviderCallback: (_) => {
                        Misclockword_Melockbit_ValueProvider(_);
                        return misclockword_melockbit_bit.Value;               
                    },
                    writeCallback: (_, __) => Misclockword_Melockbit_Write(_, __),
                    readCallback: (_, __) => Misclockword_Melockbit_Read(_, __),
                    name: "Melockbit")
            .WithReservedBits(1, 3)
            .WithFlag(4, out misclockword_udlockbit_bit, 
                    valueProviderCallback: (_) => {
                        Misclockword_Udlockbit_ValueProvider(_);
                        return misclockword_udlockbit_bit.Value;               
                    },
                    writeCallback: (_, __) => Misclockword_Udlockbit_Write(_, __),
                    readCallback: (_, __) => Misclockword_Udlockbit_Read(_, __),
                    name: "Udlockbit")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Misclockword_Read(_, __))
            .WithWriteCallback((_, __) => Misclockword_Write(_, __));
        
        // Pwrctrl - Offset : 0x50
        protected DoubleWordRegister  GeneratePwrctrlRegister() => new DoubleWordRegister(this, 0x100002)
            .WithFlag(0, out pwrctrl_pwroffonem1entry_bit, 
                    valueProviderCallback: (_) => {
                        Pwrctrl_Pwroffonem1entry_ValueProvider(_);
                        return pwrctrl_pwroffonem1entry_bit.Value;               
                    },
                    writeCallback: (_, __) => Pwrctrl_Pwroffonem1entry_Write(_, __),
                    readCallback: (_, __) => Pwrctrl_Pwroffonem1entry_Read(_, __),
                    name: "Pwroffonem1entry")
            .WithFlag(1, out pwrctrl_pwroffonem1pentry_bit, 
                    valueProviderCallback: (_) => {
                        Pwrctrl_Pwroffonem1pentry_ValueProvider(_);
                        return pwrctrl_pwroffonem1pentry_bit.Value;               
                    },
                    writeCallback: (_, __) => Pwrctrl_Pwroffonem1pentry_Write(_, __),
                    readCallback: (_, __) => Pwrctrl_Pwroffonem1pentry_Read(_, __),
                    name: "Pwroffonem1pentry")
            .WithReservedBits(2, 2)
            .WithFlag(4, out pwrctrl_pwroffentryagain_bit, 
                    valueProviderCallback: (_) => {
                        Pwrctrl_Pwroffentryagain_ValueProvider(_);
                        return pwrctrl_pwroffentryagain_bit.Value;               
                    },
                    writeCallback: (_, __) => Pwrctrl_Pwroffentryagain_Write(_, __),
                    readCallback: (_, __) => Pwrctrl_Pwroffentryagain_Read(_, __),
                    name: "Pwroffentryagain")
            .WithReservedBits(5, 11)
            .WithValueField(16, 8, out pwrctrl_pwroffdly_field, 
                    valueProviderCallback: (_) => {
                        Pwrctrl_Pwroffdly_ValueProvider(_);
                        return pwrctrl_pwroffdly_field.Value;               
                    },
                    writeCallback: (_, __) => Pwrctrl_Pwroffdly_Write(_, __),
                    readCallback: (_, __) => Pwrctrl_Pwroffdly_Read(_, __),
                    name: "Pwroffdly")
            .WithReservedBits(24, 8)
            .WithReadCallback((_, __) => Pwrctrl_Read(_, __))
            .WithWriteCallback((_, __) => Pwrctrl_Write(_, __));
        
        // Sewritectrl - Offset : 0x80
        protected DoubleWordRegister  GenerateSewritectrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out sewritectrl_wren_bit, 
                    valueProviderCallback: (_) => {
                        Sewritectrl_Wren_ValueProvider(_);
                        return sewritectrl_wren_bit.Value;               
                    },
                    writeCallback: (_, __) => Sewritectrl_Wren_Write(_, __),
                    readCallback: (_, __) => Sewritectrl_Wren_Read(_, __),
                    name: "Wren")
            .WithFlag(1, out sewritectrl_irqeraseabort_bit, 
                    valueProviderCallback: (_) => {
                        Sewritectrl_Irqeraseabort_ValueProvider(_);
                        return sewritectrl_irqeraseabort_bit.Value;               
                    },
                    writeCallback: (_, __) => Sewritectrl_Irqeraseabort_Write(_, __),
                    readCallback: (_, __) => Sewritectrl_Irqeraseabort_Read(_, __),
                    name: "Irqeraseabort")
            .WithReservedBits(2, 1)
            .WithFlag(3, out sewritectrl_lpwrite_bit, 
                    valueProviderCallback: (_) => {
                        Sewritectrl_Lpwrite_ValueProvider(_);
                        return sewritectrl_lpwrite_bit.Value;               
                    },
                    writeCallback: (_, __) => Sewritectrl_Lpwrite_Write(_, __),
                    readCallback: (_, __) => Sewritectrl_Lpwrite_Read(_, __),
                    name: "Lpwrite")
            .WithReservedBits(4, 12)
            .WithValueField(16, 10, out sewritectrl_rangecount_field, 
                    valueProviderCallback: (_) => {
                        Sewritectrl_Rangecount_ValueProvider(_);
                        return sewritectrl_rangecount_field.Value;               
                    },
                    writeCallback: (_, __) => Sewritectrl_Rangecount_Write(_, __),
                    readCallback: (_, __) => Sewritectrl_Rangecount_Read(_, __),
                    name: "Rangecount")
            .WithReservedBits(26, 6)
            .WithReadCallback((_, __) => Sewritectrl_Read(_, __))
            .WithWriteCallback((_, __) => Sewritectrl_Write(_, __));
        
        // Sewritecmd - Offset : 0x84
        protected DoubleWordRegister  GenerateSewritecmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out sewritecmd_erasepage_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Erasepage_Write(_, __),
                    name: "Erasepage")
            .WithFlag(2, out sewritecmd_writeend_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Writeend_Write(_, __),
                    name: "Writeend")
            .WithReservedBits(3, 1)
            .WithFlag(4, out sewritecmd_eraserange_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Eraserange_Write(_, __),
                    name: "Eraserange")
            .WithFlag(5, out sewritecmd_eraseabort_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Eraseabort_Write(_, __),
                    name: "Eraseabort")
            .WithReservedBits(6, 2)
            .WithFlag(8, out sewritecmd_erasemain0_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Erasemain0_Write(_, __),
                    name: "Erasemain0")
            .WithFlag(9, out sewritecmd_erasemain1_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Erasemain1_Write(_, __),
                    name: "Erasemain1")
            .WithFlag(10, out sewritecmd_erasemaina_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Erasemaina_Write(_, __),
                    name: "Erasemaina")
            .WithReservedBits(11, 1)
            .WithFlag(12, out sewritecmd_clearwdata_bit, FieldMode.Write,
                    writeCallback: (_, __) => Sewritecmd_Clearwdata_Write(_, __),
                    name: "Clearwdata")
            .WithReservedBits(13, 19)
            .WithReadCallback((_, __) => Sewritecmd_Read(_, __))
            .WithWriteCallback((_, __) => Sewritecmd_Write(_, __));
        
        // Seaddrb - Offset : 0x88
        protected DoubleWordRegister  GenerateSeaddrbRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out seaddrb_addrb_field, 
                    valueProviderCallback: (_) => {
                        Seaddrb_Addrb_ValueProvider(_);
                        return seaddrb_addrb_field.Value;               
                    },
                    writeCallback: (_, __) => Seaddrb_Addrb_Write(_, __),
                    readCallback: (_, __) => Seaddrb_Addrb_Read(_, __),
                    name: "Addrb")
            .WithReadCallback((_, __) => Seaddrb_Read(_, __))
            .WithWriteCallback((_, __) => Seaddrb_Write(_, __));
        
        // Sewdata - Offset : 0x8C
        protected DoubleWordRegister  GenerateSewdataRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out sewdata_dataw_field, 
                    valueProviderCallback: (_) => {
                        Sewdata_Dataw_ValueProvider(_);
                        return sewdata_dataw_field.Value;               
                    },
                    writeCallback: (_, __) => Sewdata_Dataw_Write(_, __),
                    readCallback: (_, __) => Sewdata_Dataw_Read(_, __),
                    name: "Dataw")
            .WithReadCallback((_, __) => Sewdata_Read(_, __))
            .WithWriteCallback((_, __) => Sewdata_Write(_, __));
        
        // Sestatus - Offset : 0x90
        protected DoubleWordRegister  GenerateSestatusRegister() => new DoubleWordRegister(this, 0x8)
            .WithFlag(0, out sestatus_busy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Busy_ValueProvider(_);
                        return sestatus_busy_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Busy_Read(_, __),
                    name: "Busy")
            .WithFlag(1, out sestatus_locked_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Locked_ValueProvider(_);
                        return sestatus_locked_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Locked_Read(_, __),
                    name: "Locked")
            .WithFlag(2, out sestatus_invaddr_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Invaddr_ValueProvider(_);
                        return sestatus_invaddr_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Invaddr_Read(_, __),
                    name: "Invaddr")
            .WithFlag(3, out sestatus_wdataready_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Wdataready_ValueProvider(_);
                        return sestatus_wdataready_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Wdataready_Read(_, __),
                    name: "Wdataready")
            .WithFlag(4, out sestatus_eraseaborted_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Eraseaborted_ValueProvider(_);
                        return sestatus_eraseaborted_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Eraseaborted_Read(_, __),
                    name: "Eraseaborted")
            .WithFlag(5, out sestatus_pending_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Pending_ValueProvider(_);
                        return sestatus_pending_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Pending_Read(_, __),
                    name: "Pending")
            .WithFlag(6, out sestatus_timeout_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Timeout_ValueProvider(_);
                        return sestatus_timeout_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Timeout_Read(_, __),
                    name: "Timeout")
            .WithFlag(7, out sestatus_rangepartial_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Rangepartial_ValueProvider(_);
                        return sestatus_rangepartial_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Rangepartial_Read(_, __),
                    name: "Rangepartial")
            .WithReservedBits(8, 8)
            .WithEnumField<DoubleWordRegister, SESTATUS_ROOTLOCK>(16, 1, out sestatus_rootlock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Rootlock_ValueProvider(_);
                        return sestatus_rootlock_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Rootlock_Read(_, __),
                    name: "Rootlock")
            .WithReservedBits(17, 13)
            .WithFlag(30, out sestatus_pwrckdone_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Pwrckdone_ValueProvider(_);
                        return sestatus_pwrckdone_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Pwrckdone_Read(_, __),
                    name: "Pwrckdone")
            .WithFlag(31, out sestatus_pwrckskipstatus_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Sestatus_Pwrckskipstatus_ValueProvider(_);
                        return sestatus_pwrckskipstatus_bit.Value;               
                    },
                    readCallback: (_, __) => Sestatus_Pwrckskipstatus_Read(_, __),
                    name: "Pwrckskipstatus")
            .WithReadCallback((_, __) => Sestatus_Read(_, __))
            .WithWriteCallback((_, __) => Sestatus_Write(_, __));
        
        // Seif - Offset : 0x94
        protected DoubleWordRegister  GenerateSeifRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out seif_eraseif_bit, 
                    valueProviderCallback: (_) => {
                        Seif_Eraseif_ValueProvider(_);
                        return seif_eraseif_bit.Value;               
                    },
                    writeCallback: (_, __) => Seif_Eraseif_Write(_, __),
                    readCallback: (_, __) => Seif_Eraseif_Read(_, __),
                    name: "Eraseif")
            .WithFlag(1, out seif_writeif_bit, 
                    valueProviderCallback: (_) => {
                        Seif_Writeif_ValueProvider(_);
                        return seif_writeif_bit.Value;               
                    },
                    writeCallback: (_, __) => Seif_Writeif_Write(_, __),
                    readCallback: (_, __) => Seif_Writeif_Read(_, __),
                    name: "Writeif")
            .WithFlag(2, out seif_wdataovif_bit, 
                    valueProviderCallback: (_) => {
                        Seif_Wdataovif_ValueProvider(_);
                        return seif_wdataovif_bit.Value;               
                    },
                    writeCallback: (_, __) => Seif_Wdataovif_Write(_, __),
                    readCallback: (_, __) => Seif_Wdataovif_Read(_, __),
                    name: "Wdataovif")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Seif_Read(_, __))
            .WithWriteCallback((_, __) => Seif_Write(_, __));
        
        // Seien - Offset : 0x98
        protected DoubleWordRegister  GenerateSeienRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out seien_eraseien_bit, 
                    valueProviderCallback: (_) => {
                        Seien_Eraseien_ValueProvider(_);
                        return seien_eraseien_bit.Value;               
                    },
                    writeCallback: (_, __) => Seien_Eraseien_Write(_, __),
                    readCallback: (_, __) => Seien_Eraseien_Read(_, __),
                    name: "Eraseien")
            .WithFlag(1, out seien_writeien_bit, 
                    valueProviderCallback: (_) => {
                        Seien_Writeien_ValueProvider(_);
                        return seien_writeien_bit.Value;               
                    },
                    writeCallback: (_, __) => Seien_Writeien_Write(_, __),
                    readCallback: (_, __) => Seien_Writeien_Read(_, __),
                    name: "Writeien")
            .WithFlag(2, out seien_wdataovien_bit, 
                    valueProviderCallback: (_) => {
                        Seien_Wdataovien_ValueProvider(_);
                        return seien_wdataovien_bit.Value;               
                    },
                    writeCallback: (_, __) => Seien_Wdataovien_Write(_, __),
                    readCallback: (_, __) => Seien_Wdataovien_Read(_, __),
                    name: "Wdataovien")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Seien_Read(_, __))
            .WithWriteCallback((_, __) => Seien_Write(_, __));
        
        // Memfeature - Offset : 0x9C
        protected DoubleWordRegister  GenerateMemfeatureRegister() => new DoubleWordRegister(this, 0xC000E)
            .WithEnumField<DoubleWordRegister, MEMFEATURE_FLASHSIZE>(0, 4, out memfeature_flashsize_field, 
                    valueProviderCallback: (_) => {
                        Memfeature_Flashsize_ValueProvider(_);
                        return memfeature_flashsize_field.Value;               
                    },
                    writeCallback: (_, __) => Memfeature_Flashsize_Write(_, __),
                    readCallback: (_, __) => Memfeature_Flashsize_Read(_, __),
                    name: "Flashsize")
            .WithReservedBits(4, 8)
            .WithValueField(12, 10, out memfeature_usersize_field, 
                    valueProviderCallback: (_) => {
                        Memfeature_Usersize_ValueProvider(_);
                        return memfeature_usersize_field.Value;               
                    },
                    writeCallback: (_, __) => Memfeature_Usersize_Write(_, __),
                    readCallback: (_, __) => Memfeature_Usersize_Read(_, __),
                    name: "Usersize")
            .WithReservedBits(22, 2)
            .WithFlag(24, out memfeature_eraserangedis_bit, 
                    valueProviderCallback: (_) => {
                        Memfeature_Eraserangedis_ValueProvider(_);
                        return memfeature_eraserangedis_bit.Value;               
                    },
                    writeCallback: (_, __) => Memfeature_Eraserangedis_Write(_, __),
                    readCallback: (_, __) => Memfeature_Eraserangedis_Read(_, __),
                    name: "Eraserangedis")
            .WithReservedBits(25, 7)
            .WithReadCallback((_, __) => Memfeature_Read(_, __))
            .WithWriteCallback((_, __) => Memfeature_Write(_, __));
        
        // Startup - Offset : 0xA0
        protected DoubleWordRegister  GenerateStartupRegister() => new DoubleWordRegister(this, 0x23001078)
            .WithValueField(0, 10, out startup_stdly0_field, 
                    valueProviderCallback: (_) => {
                        Startup_Stdly0_ValueProvider(_);
                        return startup_stdly0_field.Value;               
                    },
                    writeCallback: (_, __) => Startup_Stdly0_Write(_, __),
                    readCallback: (_, __) => Startup_Stdly0_Read(_, __),
                    name: "Stdly0")
            .WithReservedBits(10, 2)
            .WithValueField(12, 10, out startup_stdly1_field, 
                    valueProviderCallback: (_) => {
                        Startup_Stdly1_ValueProvider(_);
                        return startup_stdly1_field.Value;               
                    },
                    writeCallback: (_, __) => Startup_Stdly1_Write(_, __),
                    readCallback: (_, __) => Startup_Stdly1_Read(_, __),
                    name: "Stdly1")
            .WithReservedBits(22, 2)
            .WithFlag(24, out startup_astwait_bit, 
                    valueProviderCallback: (_) => {
                        Startup_Astwait_ValueProvider(_);
                        return startup_astwait_bit.Value;               
                    },
                    writeCallback: (_, __) => Startup_Astwait_Write(_, __),
                    readCallback: (_, __) => Startup_Astwait_Read(_, __),
                    name: "Astwait")
            .WithFlag(25, out startup_stwsen_bit, 
                    valueProviderCallback: (_) => {
                        Startup_Stwsen_ValueProvider(_);
                        return startup_stwsen_bit.Value;               
                    },
                    writeCallback: (_, __) => Startup_Stwsen_Write(_, __),
                    readCallback: (_, __) => Startup_Stwsen_Read(_, __),
                    name: "Stwsen")
            .WithFlag(26, out startup_stwsaen_bit, 
                    valueProviderCallback: (_) => {
                        Startup_Stwsaen_ValueProvider(_);
                        return startup_stwsaen_bit.Value;               
                    },
                    writeCallback: (_, __) => Startup_Stwsaen_Write(_, __),
                    readCallback: (_, __) => Startup_Stwsaen_Read(_, __),
                    name: "Stwsaen")
            .WithReservedBits(27, 1)
            .WithValueField(28, 3, out startup_stws_field, 
                    valueProviderCallback: (_) => {
                        Startup_Stws_ValueProvider(_);
                        return startup_stws_field.Value;               
                    },
                    writeCallback: (_, __) => Startup_Stws_Write(_, __),
                    readCallback: (_, __) => Startup_Stws_Read(_, __),
                    name: "Stws")
            .WithReservedBits(31, 1)
            .WithReadCallback((_, __) => Startup_Read(_, __))
            .WithWriteCallback((_, __) => Startup_Write(_, __));
        
        // Serdatactrl - Offset : 0xA4
        protected DoubleWordRegister  GenerateSerdatactrlRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 12)
            .WithFlag(12, out serdatactrl_sedoutbufen_bit, 
                    valueProviderCallback: (_) => {
                        Serdatactrl_Sedoutbufen_ValueProvider(_);
                        return serdatactrl_sedoutbufen_bit.Value;               
                    },
                    writeCallback: (_, __) => Serdatactrl_Sedoutbufen_Write(_, __),
                    readCallback: (_, __) => Serdatactrl_Sedoutbufen_Read(_, __),
                    name: "Sedoutbufen")
            .WithReservedBits(13, 19)
            .WithReadCallback((_, __) => Serdatactrl_Read(_, __))
            .WithWriteCallback((_, __) => Serdatactrl_Write(_, __));
        
        // Sepwrskip - Offset : 0xA8
        protected DoubleWordRegister  GenerateSepwrskipRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out sepwrskip_pwrckskip_bit, 
                    valueProviderCallback: (_) => {
                        Sepwrskip_Pwrckskip_ValueProvider(_);
                        return sepwrskip_pwrckskip_bit.Value;               
                    },
                    writeCallback: (_, __) => Sepwrskip_Pwrckskip_Write(_, __),
                    readCallback: (_, __) => Sepwrskip_Pwrckskip_Read(_, __),
                    name: "Pwrckskip")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Sepwrskip_Read(_, __))
            .WithWriteCallback((_, __) => Sepwrskip_Write(_, __));
        
        // Mtpctrl - Offset : 0xAC
        protected DoubleWordRegister  GenerateMtpctrlRegister() => new DoubleWordRegister(this, 0x10)
            .WithEnumField<DoubleWordRegister, MTPCTRL_MTPLOC>(0, 1, out mtpctrl_mtploc_bit, 
                    valueProviderCallback: (_) => {
                        Mtpctrl_Mtploc_ValueProvider(_);
                        return mtpctrl_mtploc_bit.Value;               
                    },
                    writeCallback: (_, __) => Mtpctrl_Mtploc_Write(_, __),
                    readCallback: (_, __) => Mtpctrl_Mtploc_Read(_, __),
                    name: "Mtploc")
            .WithReservedBits(1, 3)
            .WithFlag(4, out mtpctrl_mtpcount_bit, 
                    valueProviderCallback: (_) => {
                        Mtpctrl_Mtpcount_ValueProvider(_);
                        return mtpctrl_mtpcount_bit.Value;               
                    },
                    writeCallback: (_, __) => Mtpctrl_Mtpcount_Write(_, __),
                    readCallback: (_, __) => Mtpctrl_Mtpcount_Read(_, __),
                    name: "Mtpcount")
            .WithReservedBits(5, 27)
            .WithReadCallback((_, __) => Mtpctrl_Read(_, __))
            .WithWriteCallback((_, __) => Mtpctrl_Write(_, __));
        
        // Mtpsize - Offset : 0xB0
        protected DoubleWordRegister  GenerateMtpsizeRegister() => new DoubleWordRegister(this, 0xA04)
            .WithValueField(0, 6, out mtpsize_udsize_field, 
                    valueProviderCallback: (_) => {
                        Mtpsize_Udsize_ValueProvider(_);
                        return mtpsize_udsize_field.Value;               
                    },
                    writeCallback: (_, __) => Mtpsize_Udsize_Write(_, __),
                    readCallback: (_, __) => Mtpsize_Udsize_Read(_, __),
                    name: "Udsize")
            .WithReservedBits(6, 2)
            .WithValueField(8, 7, out mtpsize_musize_field, 
                    valueProviderCallback: (_) => {
                        Mtpsize_Musize_ValueProvider(_);
                        return mtpsize_musize_field.Value;               
                    },
                    writeCallback: (_, __) => Mtpsize_Musize_Write(_, __),
                    readCallback: (_, __) => Mtpsize_Musize_Read(_, __),
                    name: "Musize")
            .WithReservedBits(15, 17)
            .WithReadCallback((_, __) => Mtpsize_Read(_, __))
            .WithWriteCallback((_, __) => Mtpsize_Write(_, __));
        
        // Otperase - Offset : 0xB4
        protected DoubleWordRegister  GenerateOtperaseRegister() => new DoubleWordRegister(this, 0x1)
            .WithFlag(0, out otperase_otperaselock_bit, 
                    valueProviderCallback: (_) => {
                        Otperase_Otperaselock_ValueProvider(_);
                        return otperase_otperaselock_bit.Value;               
                    },
                    writeCallback: (_, __) => Otperase_Otperaselock_Write(_, __),
                    readCallback: (_, __) => Otperase_Otperaselock_Read(_, __),
                    name: "Otperaselock")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Otperase_Read(_, __))
            .WithWriteCallback((_, __) => Otperase_Write(_, __));
        
        // Flasherasetime - Offset : 0xC0
        protected DoubleWordRegister  GenerateFlasherasetimeRegister() => new DoubleWordRegister(this, 0x27102710)
            .WithValueField(0, 15, out flasherasetime_terase_field, 
                    valueProviderCallback: (_) => {
                        Flasherasetime_Terase_ValueProvider(_);
                        return flasherasetime_terase_field.Value;               
                    },
                    writeCallback: (_, __) => Flasherasetime_Terase_Write(_, __),
                    readCallback: (_, __) => Flasherasetime_Terase_Read(_, __),
                    name: "Terase")
            .WithReservedBits(15, 1)
            .WithValueField(16, 15, out flasherasetime_tme_field, 
                    valueProviderCallback: (_) => {
                        Flasherasetime_Tme_ValueProvider(_);
                        return flasherasetime_tme_field.Value;               
                    },
                    writeCallback: (_, __) => Flasherasetime_Tme_Write(_, __),
                    readCallback: (_, __) => Flasherasetime_Tme_Read(_, __),
                    name: "Tme")
            .WithReservedBits(31, 1)
            .WithReadCallback((_, __) => Flasherasetime_Read(_, __))
            .WithWriteCallback((_, __) => Flasherasetime_Write(_, __));
        
        // Flashprogtime - Offset : 0xC4
        protected DoubleWordRegister  GenerateFlashprogtimeRegister() => new DoubleWordRegister(this, 0x12481878)
            .WithValueField(0, 4, out flashprogtime_tprog_field, 
                    valueProviderCallback: (_) => {
                        Flashprogtime_Tprog_ValueProvider(_);
                        return flashprogtime_tprog_field.Value;               
                    },
                    writeCallback: (_, __) => Flashprogtime_Tprog_Write(_, __),
                    readCallback: (_, __) => Flashprogtime_Tprog_Read(_, __),
                    name: "Tprog")
            .WithValueField(4, 4, out flashprogtime_txlpw_field, 
                    valueProviderCallback: (_) => {
                        Flashprogtime_Txlpw_ValueProvider(_);
                        return flashprogtime_txlpw_field.Value;               
                    },
                    writeCallback: (_, __) => Flashprogtime_Txlpw_Write(_, __),
                    readCallback: (_, __) => Flashprogtime_Txlpw_Read(_, __),
                    name: "Txlpw")
            .WithValueField(8, 5, out flashprogtime_timebase_field, 
                    valueProviderCallback: (_) => {
                        Flashprogtime_Timebase_ValueProvider(_);
                        return flashprogtime_timebase_field.Value;               
                    },
                    writeCallback: (_, __) => Flashprogtime_Timebase_Write(_, __),
                    readCallback: (_, __) => Flashprogtime_Timebase_Read(_, __),
                    name: "Timebase")
            .WithReservedBits(13, 3)
            .WithValueField(16, 15, out flashprogtime_thv_field, 
                    valueProviderCallback: (_) => {
                        Flashprogtime_Thv_ValueProvider(_);
                        return flashprogtime_thv_field.Value;               
                    },
                    writeCallback: (_, __) => Flashprogtime_Thv_Write(_, __),
                    readCallback: (_, __) => Flashprogtime_Thv_Read(_, __),
                    name: "Thv")
            .WithReservedBits(31, 1)
            .WithReadCallback((_, __) => Flashprogtime_Read(_, __))
            .WithWriteCallback((_, __) => Flashprogtime_Write(_, __));
        
        // Selock - Offset : 0xC8
        protected DoubleWordRegister  GenerateSelockRegister() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, SELOCK_SELOCKKEY>(0, 16, out selock_selockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Selock_Selockkey_Write(_, __),
                    name: "Selockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Selock_Read(_, __))
            .WithWriteCallback((_, __) => Selock_Write(_, __));
        
        // Pagelock0 - Offset : 0x120
        protected DoubleWordRegister  GeneratePagelock0Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out pagelock0_lockbit_field, 
                    valueProviderCallback: (_) => {
                        Pagelock0_Lockbit_ValueProvider(_);
                        return pagelock0_lockbit_field.Value;               
                    },
                    writeCallback: (_, __) => Pagelock0_Lockbit_Write(_, __),
                    readCallback: (_, __) => Pagelock0_Lockbit_Read(_, __),
                    name: "Lockbit")
            .WithReadCallback((_, __) => Pagelock0_Read(_, __))
            .WithWriteCallback((_, __) => Pagelock0_Write(_, __));
        
        // Pagelock1 - Offset : 0x124
        protected DoubleWordRegister  GeneratePagelock1Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out pagelock1_lockbit_field, 
                    valueProviderCallback: (_) => {
                        Pagelock1_Lockbit_ValueProvider(_);
                        return pagelock1_lockbit_field.Value;               
                    },
                    writeCallback: (_, __) => Pagelock1_Lockbit_Write(_, __),
                    readCallback: (_, __) => Pagelock1_Lockbit_Read(_, __),
                    name: "Lockbit")
            .WithReadCallback((_, __) => Pagelock1_Read(_, __))
            .WithWriteCallback((_, __) => Pagelock1_Write(_, __));
        
        // Pagelock2 - Offset : 0x128
        protected DoubleWordRegister  GeneratePagelock2Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out pagelock2_lockbit_field, 
                    valueProviderCallback: (_) => {
                        Pagelock2_Lockbit_ValueProvider(_);
                        return pagelock2_lockbit_field.Value;               
                    },
                    writeCallback: (_, __) => Pagelock2_Lockbit_Write(_, __),
                    readCallback: (_, __) => Pagelock2_Lockbit_Read(_, __),
                    name: "Lockbit")
            .WithReadCallback((_, __) => Pagelock2_Read(_, __))
            .WithWriteCallback((_, __) => Pagelock2_Write(_, __));
        
        // Pagelock3 - Offset : 0x12C
        protected DoubleWordRegister  GeneratePagelock3Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out pagelock3_lockbit_field, 
                    valueProviderCallback: (_) => {
                        Pagelock3_Lockbit_ValueProvider(_);
                        return pagelock3_lockbit_field.Value;               
                    },
                    writeCallback: (_, __) => Pagelock3_Lockbit_Write(_, __),
                    readCallback: (_, __) => Pagelock3_Lockbit_Read(_, __),
                    name: "Lockbit")
            .WithReadCallback((_, __) => Pagelock3_Read(_, __))
            .WithWriteCallback((_, __) => Pagelock3_Write(_, __));
        
        // Pagelock4 - Offset : 0x130
        protected DoubleWordRegister  GeneratePagelock4Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out pagelock4_lockbit_field, 
                    valueProviderCallback: (_) => {
                        Pagelock4_Lockbit_ValueProvider(_);
                        return pagelock4_lockbit_field.Value;               
                    },
                    writeCallback: (_, __) => Pagelock4_Lockbit_Write(_, __),
                    readCallback: (_, __) => Pagelock4_Lockbit_Read(_, __),
                    name: "Lockbit")
            .WithReadCallback((_, __) => Pagelock4_Read(_, __))
            .WithWriteCallback((_, __) => Pagelock4_Write(_, __));
        
        // Pagelock5 - Offset : 0x134
        protected DoubleWordRegister  GeneratePagelock5Register() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out pagelock5_lockbit_field, 
                    valueProviderCallback: (_) => {
                        Pagelock5_Lockbit_ValueProvider(_);
                        return pagelock5_lockbit_field.Value;               
                    },
                    writeCallback: (_, __) => Pagelock5_Lockbit_Write(_, __),
                    readCallback: (_, __) => Pagelock5_Lockbit_Read(_, __),
                    name: "Lockbit")
            .WithReadCallback((_, __) => Pagelock5_Read(_, __))
            .WithWriteCallback((_, __) => Pagelock5_Write(_, __));
        
        // Repaddr0_Repinst - Offset : 0x140
        protected DoubleWordRegister  GenerateRepaddr0_repinstRegister() => new DoubleWordRegister(this, 0x1)
            .WithFlag(0, out repaddr0_repinst_repinvalid_bit, 
                    valueProviderCallback: (_) => {
                        Repaddr0_Repinst_Repinvalid_ValueProvider(_);
                        return repaddr0_repinst_repinvalid_bit.Value;               
                    },
                    writeCallback: (_, __) => Repaddr0_Repinst_Repinvalid_Write(_, __),
                    readCallback: (_, __) => Repaddr0_Repinst_Repinvalid_Read(_, __),
                    name: "Repinvalid")
            .WithValueField(1, 15, out repaddr0_repinst_repaddr_field, 
                    valueProviderCallback: (_) => {
                        Repaddr0_Repinst_Repaddr_ValueProvider(_);
                        return repaddr0_repinst_repaddr_field.Value;               
                    },
                    writeCallback: (_, __) => Repaddr0_Repinst_Repaddr_Write(_, __),
                    readCallback: (_, __) => Repaddr0_Repinst_Repaddr_Read(_, __),
                    name: "Repaddr")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Repaddr0_Repinst_Read(_, __))
            .WithWriteCallback((_, __) => Repaddr0_Repinst_Write(_, __));
        
        // Repaddr1_Repinst - Offset : 0x144
        protected DoubleWordRegister  GenerateRepaddr1_repinstRegister() => new DoubleWordRegister(this, 0x1)
            .WithFlag(0, out repaddr1_repinst_repinvalid_bit, 
                    valueProviderCallback: (_) => {
                        Repaddr1_Repinst_Repinvalid_ValueProvider(_);
                        return repaddr1_repinst_repinvalid_bit.Value;               
                    },
                    writeCallback: (_, __) => Repaddr1_Repinst_Repinvalid_Write(_, __),
                    readCallback: (_, __) => Repaddr1_Repinst_Repinvalid_Read(_, __),
                    name: "Repinvalid")
            .WithValueField(1, 15, out repaddr1_repinst_repaddr_field, 
                    valueProviderCallback: (_) => {
                        Repaddr1_Repinst_Repaddr_ValueProvider(_);
                        return repaddr1_repinst_repaddr_field.Value;               
                    },
                    writeCallback: (_, __) => Repaddr1_Repinst_Repaddr_Write(_, __),
                    readCallback: (_, __) => Repaddr1_Repinst_Repaddr_Read(_, __),
                    name: "Repaddr")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Repaddr1_Repinst_Read(_, __))
            .WithWriteCallback((_, __) => Repaddr1_Repinst_Write(_, __));
        
        // Dout0_Doutinst - Offset : 0x160
        protected DoubleWordRegister  GenerateDout0_doutinstRegister() => new DoubleWordRegister(this, 0xFFFFFFFF)
            .WithValueField(0, 32, out dout0_doutinst_dout0_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Dout0_Doutinst_Dout0_ValueProvider(_);
                        return dout0_doutinst_dout0_field.Value;               
                    },
                    readCallback: (_, __) => Dout0_Doutinst_Dout0_Read(_, __),
                    name: "Dout0")
            .WithReadCallback((_, __) => Dout0_Doutinst_Read(_, __))
            .WithWriteCallback((_, __) => Dout0_Doutinst_Write(_, __));
        
        // Dout1_Doutinst - Offset : 0x164
        protected DoubleWordRegister  GenerateDout1_doutinstRegister() => new DoubleWordRegister(this, 0xFFFFFFFF)
            .WithValueField(0, 32, out dout1_doutinst_dout1_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Dout1_Doutinst_Dout1_ValueProvider(_);
                        return dout1_doutinst_dout1_field.Value;               
                    },
                    readCallback: (_, __) => Dout1_Doutinst_Dout1_Read(_, __),
                    name: "Dout1")
            .WithReadCallback((_, __) => Dout1_Doutinst_Read(_, __))
            .WithWriteCallback((_, __) => Dout1_Doutinst_Write(_, __));
        
        // Testctrl_Flashtest - Offset : 0x1A0
        protected DoubleWordRegister  GenerateTestctrl_flashtestRegister() => new DoubleWordRegister(this, 0x100)
            .WithFlag(0, out testctrl_flashtest_testmas1_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testmas1_ValueProvider(_);
                        return testctrl_flashtest_testmas1_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testmas1_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testmas1_Read(_, __),
                    name: "Testmas1")
            .WithFlag(1, out testctrl_flashtest_testifren_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testifren_ValueProvider(_);
                        return testctrl_flashtest_testifren_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testifren_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testifren_Read(_, __),
                    name: "Testifren")
            .WithFlag(2, out testctrl_flashtest_testxe_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testxe_ValueProvider(_);
                        return testctrl_flashtest_testxe_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testxe_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testxe_Read(_, __),
                    name: "Testxe")
            .WithFlag(3, out testctrl_flashtest_testye_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testye_ValueProvider(_);
                        return testctrl_flashtest_testye_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testye_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testye_Read(_, __),
                    name: "Testye")
            .WithFlag(4, out testctrl_flashtest_testerase_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testerase_ValueProvider(_);
                        return testctrl_flashtest_testerase_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testerase_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testerase_Read(_, __),
                    name: "Testerase")
            .WithFlag(5, out testctrl_flashtest_testprog_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testprog_ValueProvider(_);
                        return testctrl_flashtest_testprog_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testprog_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testprog_Read(_, __),
                    name: "Testprog")
            .WithFlag(6, out testctrl_flashtest_testse_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testse_ValueProvider(_);
                        return testctrl_flashtest_testse_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testse_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testse_Read(_, __),
                    name: "Testse")
            .WithFlag(7, out testctrl_flashtest_testnvstr_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testnvstr_ValueProvider(_);
                        return testctrl_flashtest_testnvstr_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testnvstr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testnvstr_Read(_, __),
                    name: "Testnvstr")
            .WithFlag(8, out testctrl_flashtest_testtmr_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testtmr_ValueProvider(_);
                        return testctrl_flashtest_testtmr_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testtmr_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testtmr_Read(_, __),
                    name: "Testtmr")
            .WithFlag(9, out testctrl_flashtest_testrmpe_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Testrmpe_ValueProvider(_);
                        return testctrl_flashtest_testrmpe_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Testrmpe_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Testrmpe_Read(_, __),
                    name: "Testrmpe")
            .WithReservedBits(10, 2)
            .WithFlag(12, out testctrl_flashtest_inst0_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Inst0_ValueProvider(_);
                        return testctrl_flashtest_inst0_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Inst0_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Inst0_Read(_, __),
                    name: "Inst0")
            .WithReservedBits(13, 3)
            .WithEnumField<DoubleWordRegister, TESTCTRL_FLASHTEST_PATMODE>(16, 3, out testctrl_flashtest_patmode_field, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Patmode_ValueProvider(_);
                        return testctrl_flashtest_patmode_field.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Patmode_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Patmode_Read(_, __),
                    name: "Patmode")
            .WithFlag(19, out testctrl_flashtest_patinfoen_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Patinfoen_ValueProvider(_);
                        return testctrl_flashtest_patinfoen_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Patinfoen_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Patinfoen_Read(_, __),
                    name: "Patinfoen")
            .WithEnumField<DoubleWordRegister, TESTCTRL_FLASHTEST_SEMODE>(20, 2, out testctrl_flashtest_semode_field, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Semode_ValueProvider(_);
                        return testctrl_flashtest_semode_field.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Semode_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Semode_Read(_, __),
                    name: "Semode")
            .WithReservedBits(22, 1)
            .WithEnumField<DoubleWordRegister, TESTCTRL_FLASHTEST_XADRINC>(23, 1, out testctrl_flashtest_xadrinc_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Xadrinc_ValueProvider(_);
                        return testctrl_flashtest_xadrinc_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Xadrinc_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Xadrinc_Read(_, __),
                    name: "Xadrinc")
            .WithFlag(24, out testctrl_flashtest_paten_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Paten_ValueProvider(_);
                        return testctrl_flashtest_paten_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Paten_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Paten_Read(_, __),
                    name: "Paten")
            .WithReservedBits(25, 2)
            .WithFlag(27, out testctrl_flashtest_patreset_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Patreset_ValueProvider(_);
                        return testctrl_flashtest_patreset_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Patreset_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Patreset_Read(_, __),
                    name: "Patreset")
            .WithFlag(28, out testctrl_flashtest_lvemode_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Lvemode_ValueProvider(_);
                        return testctrl_flashtest_lvemode_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Lvemode_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Lvemode_Read(_, __),
                    name: "Lvemode")
            .WithReservedBits(29, 1)
            .WithFlag(30, out testctrl_flashtest_cdaen_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Cdaen_ValueProvider(_);
                        return testctrl_flashtest_cdaen_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Cdaen_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Cdaen_Read(_, __),
                    name: "Cdaen")
            .WithFlag(31, out testctrl_flashtest_flashtesten_bit, 
                    valueProviderCallback: (_) => {
                        Testctrl_Flashtest_Flashtesten_ValueProvider(_);
                        return testctrl_flashtest_flashtesten_bit.Value;               
                    },
                    writeCallback: (_, __) => Testctrl_Flashtest_Flashtesten_Write(_, __),
                    readCallback: (_, __) => Testctrl_Flashtest_Flashtesten_Read(_, __),
                    name: "Flashtesten")
            .WithReadCallback((_, __) => Testctrl_Flashtest_Read(_, __))
            .WithWriteCallback((_, __) => Testctrl_Flashtest_Write(_, __));
        
        // Patdiagctrl_Flashtest - Offset : 0x1A4
        protected DoubleWordRegister  GeneratePatdiagctrl_flashtestRegister() => new DoubleWordRegister(this, 0x80008000)
            .WithValueField(0, 6, out patdiagctrl_flashtest_infodiagxadr_field, 
                    valueProviderCallback: (_) => {
                        Patdiagctrl_Flashtest_Infodiagxadr_ValueProvider(_);
                        return patdiagctrl_flashtest_infodiagxadr_field.Value;               
                    },
                    writeCallback: (_, __) => Patdiagctrl_Flashtest_Infodiagxadr_Write(_, __),
                    readCallback: (_, __) => Patdiagctrl_Flashtest_Infodiagxadr_Read(_, __),
                    name: "Infodiagxadr")
            .WithReservedBits(6, 9)
            .WithEnumField<DoubleWordRegister, PATDIAGCTRL_FLASHTEST_INFODIAGINCR>(15, 1, out patdiagctrl_flashtest_infodiagincr_bit, 
                    valueProviderCallback: (_) => {
                        Patdiagctrl_Flashtest_Infodiagincr_ValueProvider(_);
                        return patdiagctrl_flashtest_infodiagincr_bit.Value;               
                    },
                    writeCallback: (_, __) => Patdiagctrl_Flashtest_Infodiagincr_Write(_, __),
                    readCallback: (_, __) => Patdiagctrl_Flashtest_Infodiagincr_Read(_, __),
                    name: "Infodiagincr")
            .WithValueField(16, 6, out patdiagctrl_flashtest_maindiagxadr_field, 
                    valueProviderCallback: (_) => {
                        Patdiagctrl_Flashtest_Maindiagxadr_ValueProvider(_);
                        return patdiagctrl_flashtest_maindiagxadr_field.Value;               
                    },
                    writeCallback: (_, __) => Patdiagctrl_Flashtest_Maindiagxadr_Write(_, __),
                    readCallback: (_, __) => Patdiagctrl_Flashtest_Maindiagxadr_Read(_, __),
                    name: "Maindiagxadr")
            .WithReservedBits(22, 9)
            .WithEnumField<DoubleWordRegister, PATDIAGCTRL_FLASHTEST_MAINDIAGINCR>(31, 1, out patdiagctrl_flashtest_maindiagincr_bit, 
                    valueProviderCallback: (_) => {
                        Patdiagctrl_Flashtest_Maindiagincr_ValueProvider(_);
                        return patdiagctrl_flashtest_maindiagincr_bit.Value;               
                    },
                    writeCallback: (_, __) => Patdiagctrl_Flashtest_Maindiagincr_Write(_, __),
                    readCallback: (_, __) => Patdiagctrl_Flashtest_Maindiagincr_Read(_, __),
                    name: "Maindiagincr")
            .WithReadCallback((_, __) => Patdiagctrl_Flashtest_Read(_, __))
            .WithWriteCallback((_, __) => Patdiagctrl_Flashtest_Write(_, __));
        
        // Pataddr_Flashtest - Offset : 0x1A8
        protected DoubleWordRegister  GeneratePataddr_flashtestRegister() => new DoubleWordRegister(this, 0x0)
            .WithValueField(0, 32, out pataddr_flashtest_pataddr_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Pataddr_Flashtest_Pataddr_ValueProvider(_);
                        return pataddr_flashtest_pataddr_field.Value;               
                    },
                    readCallback: (_, __) => Pataddr_Flashtest_Pataddr_Read(_, __),
                    name: "Pataddr")
            .WithReadCallback((_, __) => Pataddr_Flashtest_Read(_, __))
            .WithWriteCallback((_, __) => Pataddr_Flashtest_Write(_, __));
        
        // Patdoneaddr_Flashtest - Offset : 0x1AC
        protected DoubleWordRegister  GeneratePatdoneaddr_flashtestRegister() => new DoubleWordRegister(this, 0xFFFFFFFF)
            .WithValueField(0, 32, out patdoneaddr_flashtest_patdoneaddr_field, 
                    valueProviderCallback: (_) => {
                        Patdoneaddr_Flashtest_Patdoneaddr_ValueProvider(_);
                        return patdoneaddr_flashtest_patdoneaddr_field.Value;               
                    },
                    writeCallback: (_, __) => Patdoneaddr_Flashtest_Patdoneaddr_Write(_, __),
                    readCallback: (_, __) => Patdoneaddr_Flashtest_Patdoneaddr_Read(_, __),
                    name: "Patdoneaddr")
            .WithReadCallback((_, __) => Patdoneaddr_Flashtest_Read(_, __))
            .WithWriteCallback((_, __) => Patdoneaddr_Flashtest_Write(_, __));
        
        // Patstatus_Flashtest - Offset : 0x1B0
        protected DoubleWordRegister  GeneratePatstatus_flashtestRegister() => new DoubleWordRegister(this, 0x10)
            .WithFlag(0, out patstatus_flashtest_patdone_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Patstatus_Flashtest_Patdone_ValueProvider(_);
                        return patstatus_flashtest_patdone_bit.Value;               
                    },
                    readCallback: (_, __) => Patstatus_Flashtest_Patdone_Read(_, __),
                    name: "Patdone")
            .WithReservedBits(1, 3)
            .WithFlag(4, out patstatus_flashtest_inst0patpass_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Patstatus_Flashtest_Inst0patpass_ValueProvider(_);
                        return patstatus_flashtest_inst0patpass_bit.Value;               
                    },
                    readCallback: (_, __) => Patstatus_Flashtest_Inst0patpass_Read(_, __),
                    name: "Inst0patpass")
            .WithReservedBits(5, 26)
            .WithFlag(31, out patstatus_flashtest_interfacerdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Patstatus_Flashtest_Interfacerdy_ValueProvider(_);
                        return patstatus_flashtest_interfacerdy_bit.Value;               
                    },
                    readCallback: (_, __) => Patstatus_Flashtest_Interfacerdy_Read(_, __),
                    name: "Interfacerdy")
            .WithReadCallback((_, __) => Patstatus_Flashtest_Read(_, __))
            .WithWriteCallback((_, __) => Patstatus_Flashtest_Write(_, __));
        
        // Testredundancy_Flashtest - Offset : 0x1BC
        protected DoubleWordRegister  GenerateTestredundancy_flashtestRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out testredundancy_flashtest_reden_bit, 
                    valueProviderCallback: (_) => {
                        Testredundancy_Flashtest_Reden_ValueProvider(_);
                        return testredundancy_flashtest_reden_bit.Value;               
                    },
                    writeCallback: (_, __) => Testredundancy_Flashtest_Reden_Write(_, __),
                    readCallback: (_, __) => Testredundancy_Flashtest_Reden_Read(_, __),
                    name: "Reden")
            .WithReservedBits(1, 15)
            .WithFlag(16, out testredundancy_flashtest_ifren1_bit, 
                    valueProviderCallback: (_) => {
                        Testredundancy_Flashtest_Ifren1_ValueProvider(_);
                        return testredundancy_flashtest_ifren1_bit.Value;               
                    },
                    writeCallback: (_, __) => Testredundancy_Flashtest_Ifren1_Write(_, __),
                    readCallback: (_, __) => Testredundancy_Flashtest_Ifren1_Read(_, __),
                    name: "Ifren1")
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Testredundancy_Flashtest_Read(_, __))
            .WithWriteCallback((_, __) => Testredundancy_Flashtest_Write(_, __));
        
        // Testlock_Flashtest - Offset : 0x1C0
        protected DoubleWordRegister  GenerateTestlock_flashtestRegister() => new DoubleWordRegister(this, 0x1)
            .WithFlag(0, out testlock_flashtest_testlock_bit, 
                    valueProviderCallback: (_) => {
                        Testlock_Flashtest_Testlock_ValueProvider(_);
                        return testlock_flashtest_testlock_bit.Value;               
                    },
                    writeCallback: (_, __) => Testlock_Flashtest_Testlock_Write(_, __),
                    readCallback: (_, __) => Testlock_Flashtest_Testlock_Read(_, __),
                    name: "Testlock")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Testlock_Flashtest_Read(_, __))
            .WithWriteCallback((_, __) => Testlock_Flashtest_Write(_, __));
        
        // Rpuratd0_Drpu - Offset : 0x1C4
        protected DoubleWordRegister  GenerateRpuratd0_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out rpuratd0_drpu_ratdmscreadctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscreadctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscreadctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscreadctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscreadctrl_Read(_, __),
                    name: "Ratdmscreadctrl")
            .WithFlag(2, out rpuratd0_drpu_ratdmscrdatactrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscrdatactrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscrdatactrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscrdatactrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscrdatactrl_Read(_, __),
                    name: "Ratdmscrdatactrl")
            .WithFlag(3, out rpuratd0_drpu_ratdmscwritectrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscwritectrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscwritectrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscwritectrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscwritectrl_Read(_, __),
                    name: "Ratdmscwritectrl")
            .WithFlag(4, out rpuratd0_drpu_ratdmscwritecmd_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscwritecmd_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscwritecmd_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscwritecmd_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscwritecmd_Read(_, __),
                    name: "Ratdmscwritecmd")
            .WithFlag(5, out rpuratd0_drpu_ratdmscaddrb_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscaddrb_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscaddrb_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscaddrb_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscaddrb_Read(_, __),
                    name: "Ratdmscaddrb")
            .WithFlag(6, out rpuratd0_drpu_ratdmscwdata_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscwdata_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscwdata_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscwdata_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscwdata_Read(_, __),
                    name: "Ratdmscwdata")
            .WithReservedBits(7, 1)
            .WithFlag(8, out rpuratd0_drpu_ratdmscif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscif_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscif_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscif_Read(_, __),
                    name: "Ratdmscif")
            .WithFlag(9, out rpuratd0_drpu_ratdmscien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscien_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscien_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscien_Read(_, __),
                    name: "Ratdmscien")
            .WithReservedBits(10, 4)
            .WithFlag(14, out rpuratd0_drpu_ratdmsccmd_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmsccmd_ValueProvider(_);
                        return rpuratd0_drpu_ratdmsccmd_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmsccmd_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmsccmd_Read(_, __),
                    name: "Ratdmsccmd")
            .WithFlag(15, out rpuratd0_drpu_ratdmsclock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmsclock_ValueProvider(_);
                        return rpuratd0_drpu_ratdmsclock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmsclock_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmsclock_Read(_, __),
                    name: "Ratdmsclock")
            .WithFlag(16, out rpuratd0_drpu_ratdmscmisclockword_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscmisclockword_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscmisclockword_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscmisclockword_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscmisclockword_Read(_, __),
                    name: "Ratdmscmisclockword")
            .WithReservedBits(17, 3)
            .WithFlag(20, out rpuratd0_drpu_ratdmscpwrctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd0_Drpu_Ratdmscpwrctrl_ValueProvider(_);
                        return rpuratd0_drpu_ratdmscpwrctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd0_Drpu_Ratdmscpwrctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd0_Drpu_Ratdmscpwrctrl_Read(_, __),
                    name: "Ratdmscpwrctrl")
            .WithReservedBits(21, 11)
            .WithReadCallback((_, __) => Rpuratd0_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd0_Drpu_Write(_, __));
        
        // Rpuratd1_Drpu - Offset : 0x1C8
        protected DoubleWordRegister  GenerateRpuratd1_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out rpuratd1_drpu_ratdmscsewritectrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscsewritectrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscsewritectrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsewritectrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsewritectrl_Read(_, __),
                    name: "Ratdmscsewritectrl")
            .WithFlag(1, out rpuratd1_drpu_ratdmscsewritecmd_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscsewritecmd_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscsewritecmd_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsewritecmd_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsewritecmd_Read(_, __),
                    name: "Ratdmscsewritecmd")
            .WithFlag(2, out rpuratd1_drpu_ratdmscseaddrb_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscseaddrb_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscseaddrb_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscseaddrb_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscseaddrb_Read(_, __),
                    name: "Ratdmscseaddrb")
            .WithFlag(3, out rpuratd1_drpu_ratdmscsewdata_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscsewdata_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscsewdata_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsewdata_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsewdata_Read(_, __),
                    name: "Ratdmscsewdata")
            .WithReservedBits(4, 1)
            .WithFlag(5, out rpuratd1_drpu_ratdmscseif_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscseif_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscseif_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscseif_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscseif_Read(_, __),
                    name: "Ratdmscseif")
            .WithFlag(6, out rpuratd1_drpu_ratdmscseien_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscseien_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscseien_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscseien_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscseien_Read(_, __),
                    name: "Ratdmscseien")
            .WithFlag(7, out rpuratd1_drpu_ratdmscmemfeature_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscmemfeature_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscmemfeature_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscmemfeature_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscmemfeature_Read(_, __),
                    name: "Ratdmscmemfeature")
            .WithFlag(8, out rpuratd1_drpu_ratdmscstartup_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscstartup_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscstartup_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscstartup_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscstartup_Read(_, __),
                    name: "Ratdmscstartup")
            .WithFlag(9, out rpuratd1_drpu_ratdmscserdatactrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscserdatactrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscserdatactrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscserdatactrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscserdatactrl_Read(_, __),
                    name: "Ratdmscserdatactrl")
            .WithFlag(10, out rpuratd1_drpu_ratdmscsepwrskip_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscsepwrskip_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscsepwrskip_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsepwrskip_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscsepwrskip_Read(_, __),
                    name: "Ratdmscsepwrskip")
            .WithFlag(11, out rpuratd1_drpu_ratdmscmtpctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscmtpctrl_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscmtpctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscmtpctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscmtpctrl_Read(_, __),
                    name: "Ratdmscmtpctrl")
            .WithFlag(12, out rpuratd1_drpu_ratdmscmtpsize_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscmtpsize_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscmtpsize_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscmtpsize_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscmtpsize_Read(_, __),
                    name: "Ratdmscmtpsize")
            .WithFlag(13, out rpuratd1_drpu_ratdmscotperase_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscotperase_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscotperase_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscotperase_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscotperase_Read(_, __),
                    name: "Ratdmscotperase")
            .WithReservedBits(14, 2)
            .WithFlag(16, out rpuratd1_drpu_ratdmscflasherasetime_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscflasherasetime_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscflasherasetime_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscflasherasetime_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscflasherasetime_Read(_, __),
                    name: "Ratdmscflasherasetime")
            .WithFlag(17, out rpuratd1_drpu_ratdmscflashprogtime_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscflashprogtime_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscflashprogtime_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscflashprogtime_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscflashprogtime_Read(_, __),
                    name: "Ratdmscflashprogtime")
            .WithFlag(18, out rpuratd1_drpu_ratdmscselock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd1_Drpu_Ratdmscselock_ValueProvider(_);
                        return rpuratd1_drpu_ratdmscselock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd1_Drpu_Ratdmscselock_Write(_, __),
                    readCallback: (_, __) => Rpuratd1_Drpu_Ratdmscselock_Read(_, __),
                    name: "Ratdmscselock")
            .WithReservedBits(19, 13)
            .WithReadCallback((_, __) => Rpuratd1_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd1_Drpu_Write(_, __));
        
        // Rpuratd2_Drpu - Offset : 0x1CC
        protected DoubleWordRegister  GenerateRpuratd2_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 8)
            .WithFlag(8, out rpuratd2_drpu_ratdinstpagelockword0_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstpagelockword0_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstpagelockword0_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword0_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword0_Read(_, __),
                    name: "Ratdinstpagelockword0")
            .WithFlag(9, out rpuratd2_drpu_ratdinstpagelockword1_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstpagelockword1_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstpagelockword1_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword1_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword1_Read(_, __),
                    name: "Ratdinstpagelockword1")
            .WithFlag(10, out rpuratd2_drpu_ratdinstpagelockword2_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstpagelockword2_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstpagelockword2_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword2_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword2_Read(_, __),
                    name: "Ratdinstpagelockword2")
            .WithFlag(11, out rpuratd2_drpu_ratdinstpagelockword3_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstpagelockword3_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstpagelockword3_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword3_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword3_Read(_, __),
                    name: "Ratdinstpagelockword3")
            .WithFlag(12, out rpuratd2_drpu_ratdinstpagelockword4_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstpagelockword4_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstpagelockword4_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword4_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword4_Read(_, __),
                    name: "Ratdinstpagelockword4")
            .WithFlag(13, out rpuratd2_drpu_ratdinstpagelockword5_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstpagelockword5_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstpagelockword5_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword5_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstpagelockword5_Read(_, __),
                    name: "Ratdinstpagelockword5")
            .WithReservedBits(14, 2)
            .WithFlag(16, out rpuratd2_drpu_ratdinstrepaddr0_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstrepaddr0_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstrepaddr0_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstrepaddr0_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstrepaddr0_Read(_, __),
                    name: "Ratdinstrepaddr0")
            .WithFlag(17, out rpuratd2_drpu_ratdinstrepaddr1_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd2_Drpu_Ratdinstrepaddr1_ValueProvider(_);
                        return rpuratd2_drpu_ratdinstrepaddr1_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd2_Drpu_Ratdinstrepaddr1_Write(_, __),
                    readCallback: (_, __) => Rpuratd2_Drpu_Ratdinstrepaddr1_Read(_, __),
                    name: "Ratdinstrepaddr1")
            .WithReservedBits(18, 14)
            .WithReadCallback((_, __) => Rpuratd2_Drpu_Read(_, __))
            .WithWriteCallback((_, __) => Rpuratd2_Drpu_Write(_, __));
        
        // Rpuratd3_Drpu - Offset : 0x1D0
        protected DoubleWordRegister  GenerateRpuratd3_drpuRegister() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 8)
            .WithFlag(8, out rpuratd3_drpu_ratdmsctestctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdmsctestctrl_ValueProvider(_);
                        return rpuratd3_drpu_ratdmsctestctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdmsctestctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdmsctestctrl_Read(_, __),
                    name: "Ratdmsctestctrl")
            .WithFlag(9, out rpuratd3_drpu_ratdmscpatdiagctrl_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdmscpatdiagctrl_ValueProvider(_);
                        return rpuratd3_drpu_ratdmscpatdiagctrl_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdmscpatdiagctrl_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdmscpatdiagctrl_Read(_, __),
                    name: "Ratdmscpatdiagctrl")
            .WithReservedBits(10, 1)
            .WithFlag(11, out rpuratd3_drpu_ratdmscpatdoneaddr_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdmscpatdoneaddr_ValueProvider(_);
                        return rpuratd3_drpu_ratdmscpatdoneaddr_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdmscpatdoneaddr_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdmscpatdoneaddr_Read(_, __),
                    name: "Ratdmscpatdoneaddr")
            .WithReservedBits(12, 3)
            .WithFlag(15, out rpuratd3_drpu_ratdmsctestredundancy_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdmsctestredundancy_ValueProvider(_);
                        return rpuratd3_drpu_ratdmsctestredundancy_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdmsctestredundancy_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdmsctestredundancy_Read(_, __),
                    name: "Ratdmsctestredundancy")
            .WithFlag(16, out rpuratd3_drpu_ratdmsctestlock_bit, 
                    valueProviderCallback: (_) => {
                        Rpuratd3_Drpu_Ratdmsctestlock_ValueProvider(_);
                        return rpuratd3_drpu_ratdmsctestlock_bit.Value;               
                    },
                    writeCallback: (_, __) => Rpuratd3_Drpu_Ratdmsctestlock_Write(_, __),
                    readCallback: (_, __) => Rpuratd3_Drpu_Ratdmsctestlock_Read(_, __),
                    name: "Ratdmsctestlock")
            .WithReservedBits(17, 15)
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

        



        // Ipversion - Offset : 0x0
        protected IValueRegisterField ipversion_ipversion_field;
        partial void Ipversion_Ipversion_Read(ulong a, ulong b);
        partial void Ipversion_Ipversion_ValueProvider(ulong a);

        partial void Ipversion_Write(uint a, uint b);
        partial void Ipversion_Read(uint a, uint b);
        
        // Readctrl - Offset : 0x4
        protected IEnumRegisterField<READCTRL_MODE> readctrl_mode_field;
        partial void Readctrl_Mode_Write(READCTRL_MODE a, READCTRL_MODE b);
        partial void Readctrl_Mode_Read(READCTRL_MODE a, READCTRL_MODE b);
        partial void Readctrl_Mode_ValueProvider(READCTRL_MODE a);

        partial void Readctrl_Write(uint a, uint b);
        partial void Readctrl_Read(uint a, uint b);
        
        // Rdatactrl - Offset : 0x8
        protected IFlagRegisterField rdatactrl_afdis_bit;
        partial void Rdatactrl_Afdis_Write(bool a, bool b);
        partial void Rdatactrl_Afdis_Read(bool a, bool b);
        partial void Rdatactrl_Afdis_ValueProvider(bool a);
        protected IFlagRegisterField rdatactrl_doutbufen_bit;
        partial void Rdatactrl_Doutbufen_Write(bool a, bool b);
        partial void Rdatactrl_Doutbufen_Read(bool a, bool b);
        partial void Rdatactrl_Doutbufen_ValueProvider(bool a);

        partial void Rdatactrl_Write(uint a, uint b);
        partial void Rdatactrl_Read(uint a, uint b);
        
        // Writectrl - Offset : 0xC
        protected IFlagRegisterField writectrl_wren_bit;
        partial void Writectrl_Wren_Write(bool a, bool b);
        partial void Writectrl_Wren_Read(bool a, bool b);
        partial void Writectrl_Wren_ValueProvider(bool a);
        protected IFlagRegisterField writectrl_irqeraseabort_bit;
        partial void Writectrl_Irqeraseabort_Write(bool a, bool b);
        partial void Writectrl_Irqeraseabort_Read(bool a, bool b);
        partial void Writectrl_Irqeraseabort_ValueProvider(bool a);
        protected IFlagRegisterField writectrl_lpwrite_bit;
        partial void Writectrl_Lpwrite_Write(bool a, bool b);
        partial void Writectrl_Lpwrite_Read(bool a, bool b);
        partial void Writectrl_Lpwrite_ValueProvider(bool a);
        protected IValueRegisterField writectrl_rangecount_field;
        partial void Writectrl_Rangecount_Write(ulong a, ulong b);
        partial void Writectrl_Rangecount_Read(ulong a, ulong b);
        partial void Writectrl_Rangecount_ValueProvider(ulong a);

        partial void Writectrl_Write(uint a, uint b);
        partial void Writectrl_Read(uint a, uint b);
        
        // Writecmd - Offset : 0x10
        protected IFlagRegisterField writecmd_erasepage_bit;
        partial void Writecmd_Erasepage_Write(bool a, bool b);
        partial void Writecmd_Erasepage_ValueProvider(bool a);
        protected IFlagRegisterField writecmd_writeend_bit;
        partial void Writecmd_Writeend_Write(bool a, bool b);
        partial void Writecmd_Writeend_ValueProvider(bool a);
        protected IFlagRegisterField writecmd_eraserange_bit;
        partial void Writecmd_Eraserange_Write(bool a, bool b);
        partial void Writecmd_Eraserange_ValueProvider(bool a);
        protected IFlagRegisterField writecmd_eraseabort_bit;
        partial void Writecmd_Eraseabort_Write(bool a, bool b);
        partial void Writecmd_Eraseabort_ValueProvider(bool a);
        protected IFlagRegisterField writecmd_erasemain0_bit;
        partial void Writecmd_Erasemain0_Write(bool a, bool b);
        partial void Writecmd_Erasemain0_ValueProvider(bool a);
        protected IFlagRegisterField writecmd_clearwdata_bit;
        partial void Writecmd_Clearwdata_Write(bool a, bool b);
        partial void Writecmd_Clearwdata_ValueProvider(bool a);

        partial void Writecmd_Write(uint a, uint b);
        partial void Writecmd_Read(uint a, uint b);
        
        // Addrb - Offset : 0x14
        protected IValueRegisterField addrb_addrb_field;
        partial void Addrb_Addrb_Write(ulong a, ulong b);
        partial void Addrb_Addrb_Read(ulong a, ulong b);
        partial void Addrb_Addrb_ValueProvider(ulong a);

        partial void Addrb_Write(uint a, uint b);
        partial void Addrb_Read(uint a, uint b);
        
        // Wdata - Offset : 0x18
        protected IValueRegisterField wdata_dataw_field;
        partial void Wdata_Dataw_Write(ulong a, ulong b);
        partial void Wdata_Dataw_Read(ulong a, ulong b);
        partial void Wdata_Dataw_ValueProvider(ulong a);

        partial void Wdata_Write(uint a, uint b);
        partial void Wdata_Read(uint a, uint b);
        
        // Status - Offset : 0x1C
        protected IFlagRegisterField status_busy_bit;
        partial void Status_Busy_Read(bool a, bool b);
        partial void Status_Busy_ValueProvider(bool a);
        protected IFlagRegisterField status_locked_bit;
        partial void Status_Locked_Read(bool a, bool b);
        partial void Status_Locked_ValueProvider(bool a);
        protected IFlagRegisterField status_invaddr_bit;
        partial void Status_Invaddr_Read(bool a, bool b);
        partial void Status_Invaddr_ValueProvider(bool a);
        protected IFlagRegisterField status_wdataready_bit;
        partial void Status_Wdataready_Read(bool a, bool b);
        partial void Status_Wdataready_ValueProvider(bool a);
        protected IFlagRegisterField status_eraseaborted_bit;
        partial void Status_Eraseaborted_Read(bool a, bool b);
        partial void Status_Eraseaborted_ValueProvider(bool a);
        protected IFlagRegisterField status_pending_bit;
        partial void Status_Pending_Read(bool a, bool b);
        partial void Status_Pending_ValueProvider(bool a);
        protected IFlagRegisterField status_timeout_bit;
        partial void Status_Timeout_Read(bool a, bool b);
        partial void Status_Timeout_ValueProvider(bool a);
        protected IFlagRegisterField status_rangepartial_bit;
        partial void Status_Rangepartial_Read(bool a, bool b);
        partial void Status_Rangepartial_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_REGLOCK> status_reglock_bit;
        partial void Status_Reglock_Read(STATUS_REGLOCK a, STATUS_REGLOCK b);
        partial void Status_Reglock_ValueProvider(STATUS_REGLOCK a);
        protected IFlagRegisterField status_pwron_bit;
        partial void Status_Pwron_Read(bool a, bool b);
        partial void Status_Pwron_ValueProvider(bool a);
        protected IFlagRegisterField status_wready_bit;
        partial void Status_Wready_Read(bool a, bool b);
        partial void Status_Wready_ValueProvider(bool a);
        protected IValueRegisterField status_pwrupckbdfailcount_field;
        partial void Status_Pwrupckbdfailcount_Read(ulong a, ulong b);
        partial void Status_Pwrupckbdfailcount_ValueProvider(ulong a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // If - Offset : 0x20
        protected IFlagRegisterField if_erase_bit;
        partial void If_Erase_Write(bool a, bool b);
        partial void If_Erase_Read(bool a, bool b);
        partial void If_Erase_ValueProvider(bool a);
        protected IFlagRegisterField if_write_bit;
        partial void If_Write_Write(bool a, bool b);
        partial void If_Write_Read(bool a, bool b);
        partial void If_Write_ValueProvider(bool a);
        protected IFlagRegisterField if_wdataov_bit;
        partial void If_Wdataov_Write(bool a, bool b);
        partial void If_Wdataov_Read(bool a, bool b);
        partial void If_Wdataov_ValueProvider(bool a);
        protected IFlagRegisterField if_pwrupf_bit;
        partial void If_Pwrupf_Write(bool a, bool b);
        partial void If_Pwrupf_Read(bool a, bool b);
        partial void If_Pwrupf_ValueProvider(bool a);
        protected IFlagRegisterField if_pwroff_bit;
        partial void If_Pwroff_Write(bool a, bool b);
        partial void If_Pwroff_Read(bool a, bool b);
        partial void If_Pwroff_ValueProvider(bool a);

        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x24
        protected IFlagRegisterField ien_erase_bit;
        partial void Ien_Erase_Write(bool a, bool b);
        partial void Ien_Erase_Read(bool a, bool b);
        partial void Ien_Erase_ValueProvider(bool a);
        protected IFlagRegisterField ien_write_bit;
        partial void Ien_Write_Write(bool a, bool b);
        partial void Ien_Write_Read(bool a, bool b);
        partial void Ien_Write_ValueProvider(bool a);
        protected IFlagRegisterField ien_wdataov_bit;
        partial void Ien_Wdataov_Write(bool a, bool b);
        partial void Ien_Wdataov_Read(bool a, bool b);
        partial void Ien_Wdataov_ValueProvider(bool a);
        protected IFlagRegisterField ien_pwrupf_bit;
        partial void Ien_Pwrupf_Write(bool a, bool b);
        partial void Ien_Pwrupf_Read(bool a, bool b);
        partial void Ien_Pwrupf_ValueProvider(bool a);
        protected IFlagRegisterField ien_pwroff_bit;
        partial void Ien_Pwroff_Write(bool a, bool b);
        partial void Ien_Pwroff_Read(bool a, bool b);
        partial void Ien_Pwroff_ValueProvider(bool a);

        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Userdatasize - Offset : 0x34
        protected IValueRegisterField userdatasize_userdatasize_field;
        partial void Userdatasize_Userdatasize_Read(ulong a, ulong b);
        partial void Userdatasize_Userdatasize_ValueProvider(ulong a);

        partial void Userdatasize_Write(uint a, uint b);
        partial void Userdatasize_Read(uint a, uint b);
        
        // Cmd - Offset : 0x38
        protected IFlagRegisterField cmd_pwrup_bit;
        partial void Cmd_Pwrup_Write(bool a, bool b);
        partial void Cmd_Pwrup_ValueProvider(bool a);
        protected IFlagRegisterField cmd_pwroff_bit;
        partial void Cmd_Pwroff_Write(bool a, bool b);
        partial void Cmd_Pwroff_ValueProvider(bool a);

        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);
        
        // Lock - Offset : 0x3C
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // Misclockword - Offset : 0x40
        protected IFlagRegisterField misclockword_melockbit_bit;
        partial void Misclockword_Melockbit_Write(bool a, bool b);
        partial void Misclockword_Melockbit_Read(bool a, bool b);
        partial void Misclockword_Melockbit_ValueProvider(bool a);
        protected IFlagRegisterField misclockword_udlockbit_bit;
        partial void Misclockword_Udlockbit_Write(bool a, bool b);
        partial void Misclockword_Udlockbit_Read(bool a, bool b);
        partial void Misclockword_Udlockbit_ValueProvider(bool a);

        partial void Misclockword_Write(uint a, uint b);
        partial void Misclockword_Read(uint a, uint b);
        
        // Pwrctrl - Offset : 0x50
        protected IFlagRegisterField pwrctrl_pwroffonem1entry_bit;
        partial void Pwrctrl_Pwroffonem1entry_Write(bool a, bool b);
        partial void Pwrctrl_Pwroffonem1entry_Read(bool a, bool b);
        partial void Pwrctrl_Pwroffonem1entry_ValueProvider(bool a);
        protected IFlagRegisterField pwrctrl_pwroffonem1pentry_bit;
        partial void Pwrctrl_Pwroffonem1pentry_Write(bool a, bool b);
        partial void Pwrctrl_Pwroffonem1pentry_Read(bool a, bool b);
        partial void Pwrctrl_Pwroffonem1pentry_ValueProvider(bool a);
        protected IFlagRegisterField pwrctrl_pwroffentryagain_bit;
        partial void Pwrctrl_Pwroffentryagain_Write(bool a, bool b);
        partial void Pwrctrl_Pwroffentryagain_Read(bool a, bool b);
        partial void Pwrctrl_Pwroffentryagain_ValueProvider(bool a);
        protected IValueRegisterField pwrctrl_pwroffdly_field;
        partial void Pwrctrl_Pwroffdly_Write(ulong a, ulong b);
        partial void Pwrctrl_Pwroffdly_Read(ulong a, ulong b);
        partial void Pwrctrl_Pwroffdly_ValueProvider(ulong a);

        partial void Pwrctrl_Write(uint a, uint b);
        partial void Pwrctrl_Read(uint a, uint b);
        
        // Sewritectrl - Offset : 0x80
        protected IFlagRegisterField sewritectrl_wren_bit;
        partial void Sewritectrl_Wren_Write(bool a, bool b);
        partial void Sewritectrl_Wren_Read(bool a, bool b);
        partial void Sewritectrl_Wren_ValueProvider(bool a);
        protected IFlagRegisterField sewritectrl_irqeraseabort_bit;
        partial void Sewritectrl_Irqeraseabort_Write(bool a, bool b);
        partial void Sewritectrl_Irqeraseabort_Read(bool a, bool b);
        partial void Sewritectrl_Irqeraseabort_ValueProvider(bool a);
        protected IFlagRegisterField sewritectrl_lpwrite_bit;
        partial void Sewritectrl_Lpwrite_Write(bool a, bool b);
        partial void Sewritectrl_Lpwrite_Read(bool a, bool b);
        partial void Sewritectrl_Lpwrite_ValueProvider(bool a);
        protected IValueRegisterField sewritectrl_rangecount_field;
        partial void Sewritectrl_Rangecount_Write(ulong a, ulong b);
        partial void Sewritectrl_Rangecount_Read(ulong a, ulong b);
        partial void Sewritectrl_Rangecount_ValueProvider(ulong a);

        partial void Sewritectrl_Write(uint a, uint b);
        partial void Sewritectrl_Read(uint a, uint b);
        
        // Sewritecmd - Offset : 0x84
        protected IFlagRegisterField sewritecmd_erasepage_bit;
        partial void Sewritecmd_Erasepage_Write(bool a, bool b);
        partial void Sewritecmd_Erasepage_ValueProvider(bool a);
        protected IFlagRegisterField sewritecmd_writeend_bit;
        partial void Sewritecmd_Writeend_Write(bool a, bool b);
        partial void Sewritecmd_Writeend_ValueProvider(bool a);
        protected IFlagRegisterField sewritecmd_eraserange_bit;
        partial void Sewritecmd_Eraserange_Write(bool a, bool b);
        partial void Sewritecmd_Eraserange_ValueProvider(bool a);
        protected IFlagRegisterField sewritecmd_eraseabort_bit;
        partial void Sewritecmd_Eraseabort_Write(bool a, bool b);
        partial void Sewritecmd_Eraseabort_ValueProvider(bool a);
        protected IFlagRegisterField sewritecmd_erasemain0_bit;
        partial void Sewritecmd_Erasemain0_Write(bool a, bool b);
        partial void Sewritecmd_Erasemain0_ValueProvider(bool a);
        protected IFlagRegisterField sewritecmd_erasemain1_bit;
        partial void Sewritecmd_Erasemain1_Write(bool a, bool b);
        partial void Sewritecmd_Erasemain1_ValueProvider(bool a);
        protected IFlagRegisterField sewritecmd_erasemaina_bit;
        partial void Sewritecmd_Erasemaina_Write(bool a, bool b);
        partial void Sewritecmd_Erasemaina_ValueProvider(bool a);
        protected IFlagRegisterField sewritecmd_clearwdata_bit;
        partial void Sewritecmd_Clearwdata_Write(bool a, bool b);
        partial void Sewritecmd_Clearwdata_ValueProvider(bool a);

        partial void Sewritecmd_Write(uint a, uint b);
        partial void Sewritecmd_Read(uint a, uint b);
        
        // Seaddrb - Offset : 0x88
        protected IValueRegisterField seaddrb_addrb_field;
        partial void Seaddrb_Addrb_Write(ulong a, ulong b);
        partial void Seaddrb_Addrb_Read(ulong a, ulong b);
        partial void Seaddrb_Addrb_ValueProvider(ulong a);

        partial void Seaddrb_Write(uint a, uint b);
        partial void Seaddrb_Read(uint a, uint b);
        
        // Sewdata - Offset : 0x8C
        protected IValueRegisterField sewdata_dataw_field;
        partial void Sewdata_Dataw_Write(ulong a, ulong b);
        partial void Sewdata_Dataw_Read(ulong a, ulong b);
        partial void Sewdata_Dataw_ValueProvider(ulong a);

        partial void Sewdata_Write(uint a, uint b);
        partial void Sewdata_Read(uint a, uint b);
        
        // Sestatus - Offset : 0x90
        protected IFlagRegisterField sestatus_busy_bit;
        partial void Sestatus_Busy_Read(bool a, bool b);
        partial void Sestatus_Busy_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_locked_bit;
        partial void Sestatus_Locked_Read(bool a, bool b);
        partial void Sestatus_Locked_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_invaddr_bit;
        partial void Sestatus_Invaddr_Read(bool a, bool b);
        partial void Sestatus_Invaddr_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_wdataready_bit;
        partial void Sestatus_Wdataready_Read(bool a, bool b);
        partial void Sestatus_Wdataready_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_eraseaborted_bit;
        partial void Sestatus_Eraseaborted_Read(bool a, bool b);
        partial void Sestatus_Eraseaborted_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_pending_bit;
        partial void Sestatus_Pending_Read(bool a, bool b);
        partial void Sestatus_Pending_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_timeout_bit;
        partial void Sestatus_Timeout_Read(bool a, bool b);
        partial void Sestatus_Timeout_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_rangepartial_bit;
        partial void Sestatus_Rangepartial_Read(bool a, bool b);
        partial void Sestatus_Rangepartial_ValueProvider(bool a);
        protected IEnumRegisterField<SESTATUS_ROOTLOCK> sestatus_rootlock_bit;
        partial void Sestatus_Rootlock_Read(SESTATUS_ROOTLOCK a, SESTATUS_ROOTLOCK b);
        partial void Sestatus_Rootlock_ValueProvider(SESTATUS_ROOTLOCK a);
        protected IFlagRegisterField sestatus_pwrckdone_bit;
        partial void Sestatus_Pwrckdone_Read(bool a, bool b);
        partial void Sestatus_Pwrckdone_ValueProvider(bool a);
        protected IFlagRegisterField sestatus_pwrckskipstatus_bit;
        partial void Sestatus_Pwrckskipstatus_Read(bool a, bool b);
        partial void Sestatus_Pwrckskipstatus_ValueProvider(bool a);

        partial void Sestatus_Write(uint a, uint b);
        partial void Sestatus_Read(uint a, uint b);
        
        // Seif - Offset : 0x94
        protected IFlagRegisterField seif_eraseif_bit;
        partial void Seif_Eraseif_Write(bool a, bool b);
        partial void Seif_Eraseif_Read(bool a, bool b);
        partial void Seif_Eraseif_ValueProvider(bool a);
        protected IFlagRegisterField seif_writeif_bit;
        partial void Seif_Writeif_Write(bool a, bool b);
        partial void Seif_Writeif_Read(bool a, bool b);
        partial void Seif_Writeif_ValueProvider(bool a);
        protected IFlagRegisterField seif_wdataovif_bit;
        partial void Seif_Wdataovif_Write(bool a, bool b);
        partial void Seif_Wdataovif_Read(bool a, bool b);
        partial void Seif_Wdataovif_ValueProvider(bool a);

        partial void Seif_Write(uint a, uint b);
        partial void Seif_Read(uint a, uint b);
        
        // Seien - Offset : 0x98
        protected IFlagRegisterField seien_eraseien_bit;
        partial void Seien_Eraseien_Write(bool a, bool b);
        partial void Seien_Eraseien_Read(bool a, bool b);
        partial void Seien_Eraseien_ValueProvider(bool a);
        protected IFlagRegisterField seien_writeien_bit;
        partial void Seien_Writeien_Write(bool a, bool b);
        partial void Seien_Writeien_Read(bool a, bool b);
        partial void Seien_Writeien_ValueProvider(bool a);
        protected IFlagRegisterField seien_wdataovien_bit;
        partial void Seien_Wdataovien_Write(bool a, bool b);
        partial void Seien_Wdataovien_Read(bool a, bool b);
        partial void Seien_Wdataovien_ValueProvider(bool a);

        partial void Seien_Write(uint a, uint b);
        partial void Seien_Read(uint a, uint b);
        
        // Memfeature - Offset : 0x9C
        protected IEnumRegisterField<MEMFEATURE_FLASHSIZE> memfeature_flashsize_field;
        partial void Memfeature_Flashsize_Write(MEMFEATURE_FLASHSIZE a, MEMFEATURE_FLASHSIZE b);
        partial void Memfeature_Flashsize_Read(MEMFEATURE_FLASHSIZE a, MEMFEATURE_FLASHSIZE b);
        partial void Memfeature_Flashsize_ValueProvider(MEMFEATURE_FLASHSIZE a);
        protected IValueRegisterField memfeature_usersize_field;
        partial void Memfeature_Usersize_Write(ulong a, ulong b);
        partial void Memfeature_Usersize_Read(ulong a, ulong b);
        partial void Memfeature_Usersize_ValueProvider(ulong a);
        protected IFlagRegisterField memfeature_eraserangedis_bit;
        partial void Memfeature_Eraserangedis_Write(bool a, bool b);
        partial void Memfeature_Eraserangedis_Read(bool a, bool b);
        partial void Memfeature_Eraserangedis_ValueProvider(bool a);

        partial void Memfeature_Write(uint a, uint b);
        partial void Memfeature_Read(uint a, uint b);
        
        // Startup - Offset : 0xA0
        protected IValueRegisterField startup_stdly0_field;
        partial void Startup_Stdly0_Write(ulong a, ulong b);
        partial void Startup_Stdly0_Read(ulong a, ulong b);
        partial void Startup_Stdly0_ValueProvider(ulong a);
        protected IValueRegisterField startup_stdly1_field;
        partial void Startup_Stdly1_Write(ulong a, ulong b);
        partial void Startup_Stdly1_Read(ulong a, ulong b);
        partial void Startup_Stdly1_ValueProvider(ulong a);
        protected IFlagRegisterField startup_astwait_bit;
        partial void Startup_Astwait_Write(bool a, bool b);
        partial void Startup_Astwait_Read(bool a, bool b);
        partial void Startup_Astwait_ValueProvider(bool a);
        protected IFlagRegisterField startup_stwsen_bit;
        partial void Startup_Stwsen_Write(bool a, bool b);
        partial void Startup_Stwsen_Read(bool a, bool b);
        partial void Startup_Stwsen_ValueProvider(bool a);
        protected IFlagRegisterField startup_stwsaen_bit;
        partial void Startup_Stwsaen_Write(bool a, bool b);
        partial void Startup_Stwsaen_Read(bool a, bool b);
        partial void Startup_Stwsaen_ValueProvider(bool a);
        protected IValueRegisterField startup_stws_field;
        partial void Startup_Stws_Write(ulong a, ulong b);
        partial void Startup_Stws_Read(ulong a, ulong b);
        partial void Startup_Stws_ValueProvider(ulong a);

        partial void Startup_Write(uint a, uint b);
        partial void Startup_Read(uint a, uint b);
        
        // Serdatactrl - Offset : 0xA4
        protected IFlagRegisterField serdatactrl_sedoutbufen_bit;
        partial void Serdatactrl_Sedoutbufen_Write(bool a, bool b);
        partial void Serdatactrl_Sedoutbufen_Read(bool a, bool b);
        partial void Serdatactrl_Sedoutbufen_ValueProvider(bool a);

        partial void Serdatactrl_Write(uint a, uint b);
        partial void Serdatactrl_Read(uint a, uint b);
        
        // Sepwrskip - Offset : 0xA8
        protected IFlagRegisterField sepwrskip_pwrckskip_bit;
        partial void Sepwrskip_Pwrckskip_Write(bool a, bool b);
        partial void Sepwrskip_Pwrckskip_Read(bool a, bool b);
        partial void Sepwrskip_Pwrckskip_ValueProvider(bool a);

        partial void Sepwrskip_Write(uint a, uint b);
        partial void Sepwrskip_Read(uint a, uint b);
        
        // Mtpctrl - Offset : 0xAC
        protected IEnumRegisterField<MTPCTRL_MTPLOC> mtpctrl_mtploc_bit;
        partial void Mtpctrl_Mtploc_Write(MTPCTRL_MTPLOC a, MTPCTRL_MTPLOC b);
        partial void Mtpctrl_Mtploc_Read(MTPCTRL_MTPLOC a, MTPCTRL_MTPLOC b);
        partial void Mtpctrl_Mtploc_ValueProvider(MTPCTRL_MTPLOC a);
        protected IFlagRegisterField mtpctrl_mtpcount_bit;
        partial void Mtpctrl_Mtpcount_Write(bool a, bool b);
        partial void Mtpctrl_Mtpcount_Read(bool a, bool b);
        partial void Mtpctrl_Mtpcount_ValueProvider(bool a);

        partial void Mtpctrl_Write(uint a, uint b);
        partial void Mtpctrl_Read(uint a, uint b);
        
        // Mtpsize - Offset : 0xB0
        protected IValueRegisterField mtpsize_udsize_field;
        partial void Mtpsize_Udsize_Write(ulong a, ulong b);
        partial void Mtpsize_Udsize_Read(ulong a, ulong b);
        partial void Mtpsize_Udsize_ValueProvider(ulong a);
        protected IValueRegisterField mtpsize_musize_field;
        partial void Mtpsize_Musize_Write(ulong a, ulong b);
        partial void Mtpsize_Musize_Read(ulong a, ulong b);
        partial void Mtpsize_Musize_ValueProvider(ulong a);

        partial void Mtpsize_Write(uint a, uint b);
        partial void Mtpsize_Read(uint a, uint b);
        
        // Otperase - Offset : 0xB4
        protected IFlagRegisterField otperase_otperaselock_bit;
        partial void Otperase_Otperaselock_Write(bool a, bool b);
        partial void Otperase_Otperaselock_Read(bool a, bool b);
        partial void Otperase_Otperaselock_ValueProvider(bool a);

        partial void Otperase_Write(uint a, uint b);
        partial void Otperase_Read(uint a, uint b);
        
        // Flasherasetime - Offset : 0xC0
        protected IValueRegisterField flasherasetime_terase_field;
        partial void Flasherasetime_Terase_Write(ulong a, ulong b);
        partial void Flasherasetime_Terase_Read(ulong a, ulong b);
        partial void Flasherasetime_Terase_ValueProvider(ulong a);
        protected IValueRegisterField flasherasetime_tme_field;
        partial void Flasherasetime_Tme_Write(ulong a, ulong b);
        partial void Flasherasetime_Tme_Read(ulong a, ulong b);
        partial void Flasherasetime_Tme_ValueProvider(ulong a);

        partial void Flasherasetime_Write(uint a, uint b);
        partial void Flasherasetime_Read(uint a, uint b);
        
        // Flashprogtime - Offset : 0xC4
        protected IValueRegisterField flashprogtime_tprog_field;
        partial void Flashprogtime_Tprog_Write(ulong a, ulong b);
        partial void Flashprogtime_Tprog_Read(ulong a, ulong b);
        partial void Flashprogtime_Tprog_ValueProvider(ulong a);
        protected IValueRegisterField flashprogtime_txlpw_field;
        partial void Flashprogtime_Txlpw_Write(ulong a, ulong b);
        partial void Flashprogtime_Txlpw_Read(ulong a, ulong b);
        partial void Flashprogtime_Txlpw_ValueProvider(ulong a);
        protected IValueRegisterField flashprogtime_timebase_field;
        partial void Flashprogtime_Timebase_Write(ulong a, ulong b);
        partial void Flashprogtime_Timebase_Read(ulong a, ulong b);
        partial void Flashprogtime_Timebase_ValueProvider(ulong a);
        protected IValueRegisterField flashprogtime_thv_field;
        partial void Flashprogtime_Thv_Write(ulong a, ulong b);
        partial void Flashprogtime_Thv_Read(ulong a, ulong b);
        partial void Flashprogtime_Thv_ValueProvider(ulong a);

        partial void Flashprogtime_Write(uint a, uint b);
        partial void Flashprogtime_Read(uint a, uint b);
        
        // Selock - Offset : 0xC8
        protected IEnumRegisterField<SELOCK_SELOCKKEY> selock_selockkey_field;
        partial void Selock_Selockkey_Write(SELOCK_SELOCKKEY a, SELOCK_SELOCKKEY b);
        partial void Selock_Selockkey_ValueProvider(SELOCK_SELOCKKEY a);

        partial void Selock_Write(uint a, uint b);
        partial void Selock_Read(uint a, uint b);
        
        // Pagelock0 - Offset : 0x120
        protected IValueRegisterField pagelock0_lockbit_field;
        partial void Pagelock0_Lockbit_Write(ulong a, ulong b);
        partial void Pagelock0_Lockbit_Read(ulong a, ulong b);
        partial void Pagelock0_Lockbit_ValueProvider(ulong a);

        partial void Pagelock0_Write(uint a, uint b);
        partial void Pagelock0_Read(uint a, uint b);
        
        // Pagelock1 - Offset : 0x124
        protected IValueRegisterField pagelock1_lockbit_field;
        partial void Pagelock1_Lockbit_Write(ulong a, ulong b);
        partial void Pagelock1_Lockbit_Read(ulong a, ulong b);
        partial void Pagelock1_Lockbit_ValueProvider(ulong a);

        partial void Pagelock1_Write(uint a, uint b);
        partial void Pagelock1_Read(uint a, uint b);
        
        // Pagelock2 - Offset : 0x128
        protected IValueRegisterField pagelock2_lockbit_field;
        partial void Pagelock2_Lockbit_Write(ulong a, ulong b);
        partial void Pagelock2_Lockbit_Read(ulong a, ulong b);
        partial void Pagelock2_Lockbit_ValueProvider(ulong a);

        partial void Pagelock2_Write(uint a, uint b);
        partial void Pagelock2_Read(uint a, uint b);
        
        // Pagelock3 - Offset : 0x12C
        protected IValueRegisterField pagelock3_lockbit_field;
        partial void Pagelock3_Lockbit_Write(ulong a, ulong b);
        partial void Pagelock3_Lockbit_Read(ulong a, ulong b);
        partial void Pagelock3_Lockbit_ValueProvider(ulong a);

        partial void Pagelock3_Write(uint a, uint b);
        partial void Pagelock3_Read(uint a, uint b);
        
        // Pagelock4 - Offset : 0x130
        protected IValueRegisterField pagelock4_lockbit_field;
        partial void Pagelock4_Lockbit_Write(ulong a, ulong b);
        partial void Pagelock4_Lockbit_Read(ulong a, ulong b);
        partial void Pagelock4_Lockbit_ValueProvider(ulong a);

        partial void Pagelock4_Write(uint a, uint b);
        partial void Pagelock4_Read(uint a, uint b);
        
        // Pagelock5 - Offset : 0x134
        protected IValueRegisterField pagelock5_lockbit_field;
        partial void Pagelock5_Lockbit_Write(ulong a, ulong b);
        partial void Pagelock5_Lockbit_Read(ulong a, ulong b);
        partial void Pagelock5_Lockbit_ValueProvider(ulong a);

        partial void Pagelock5_Write(uint a, uint b);
        partial void Pagelock5_Read(uint a, uint b);
        
        // Repaddr0_Repinst - Offset : 0x140
        protected IFlagRegisterField repaddr0_repinst_repinvalid_bit;
        partial void Repaddr0_Repinst_Repinvalid_Write(bool a, bool b);
        partial void Repaddr0_Repinst_Repinvalid_Read(bool a, bool b);
        partial void Repaddr0_Repinst_Repinvalid_ValueProvider(bool a);
        protected IValueRegisterField repaddr0_repinst_repaddr_field;
        partial void Repaddr0_Repinst_Repaddr_Write(ulong a, ulong b);
        partial void Repaddr0_Repinst_Repaddr_Read(ulong a, ulong b);
        partial void Repaddr0_Repinst_Repaddr_ValueProvider(ulong a);

        partial void Repaddr0_Repinst_Write(uint a, uint b);
        partial void Repaddr0_Repinst_Read(uint a, uint b);
        
        // Repaddr1_Repinst - Offset : 0x144
        protected IFlagRegisterField repaddr1_repinst_repinvalid_bit;
        partial void Repaddr1_Repinst_Repinvalid_Write(bool a, bool b);
        partial void Repaddr1_Repinst_Repinvalid_Read(bool a, bool b);
        partial void Repaddr1_Repinst_Repinvalid_ValueProvider(bool a);
        protected IValueRegisterField repaddr1_repinst_repaddr_field;
        partial void Repaddr1_Repinst_Repaddr_Write(ulong a, ulong b);
        partial void Repaddr1_Repinst_Repaddr_Read(ulong a, ulong b);
        partial void Repaddr1_Repinst_Repaddr_ValueProvider(ulong a);

        partial void Repaddr1_Repinst_Write(uint a, uint b);
        partial void Repaddr1_Repinst_Read(uint a, uint b);
        
        // Dout0_Doutinst - Offset : 0x160
        protected IValueRegisterField dout0_doutinst_dout0_field;
        partial void Dout0_Doutinst_Dout0_Read(ulong a, ulong b);
        partial void Dout0_Doutinst_Dout0_ValueProvider(ulong a);

        partial void Dout0_Doutinst_Write(uint a, uint b);
        partial void Dout0_Doutinst_Read(uint a, uint b);
        
        // Dout1_Doutinst - Offset : 0x164
        protected IValueRegisterField dout1_doutinst_dout1_field;
        partial void Dout1_Doutinst_Dout1_Read(ulong a, ulong b);
        partial void Dout1_Doutinst_Dout1_ValueProvider(ulong a);

        partial void Dout1_Doutinst_Write(uint a, uint b);
        partial void Dout1_Doutinst_Read(uint a, uint b);
        
        // Testctrl_Flashtest - Offset : 0x1A0
        protected IFlagRegisterField testctrl_flashtest_testmas1_bit;
        partial void Testctrl_Flashtest_Testmas1_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testmas1_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testmas1_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testifren_bit;
        partial void Testctrl_Flashtest_Testifren_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testifren_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testifren_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testxe_bit;
        partial void Testctrl_Flashtest_Testxe_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testxe_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testxe_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testye_bit;
        partial void Testctrl_Flashtest_Testye_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testye_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testye_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testerase_bit;
        partial void Testctrl_Flashtest_Testerase_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testerase_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testerase_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testprog_bit;
        partial void Testctrl_Flashtest_Testprog_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testprog_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testprog_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testse_bit;
        partial void Testctrl_Flashtest_Testse_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testse_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testse_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testnvstr_bit;
        partial void Testctrl_Flashtest_Testnvstr_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testnvstr_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testnvstr_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testtmr_bit;
        partial void Testctrl_Flashtest_Testtmr_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testtmr_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testtmr_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_testrmpe_bit;
        partial void Testctrl_Flashtest_Testrmpe_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Testrmpe_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Testrmpe_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_inst0_bit;
        partial void Testctrl_Flashtest_Inst0_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Inst0_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Inst0_ValueProvider(bool a);
        protected IEnumRegisterField<TESTCTRL_FLASHTEST_PATMODE> testctrl_flashtest_patmode_field;
        partial void Testctrl_Flashtest_Patmode_Write(TESTCTRL_FLASHTEST_PATMODE a, TESTCTRL_FLASHTEST_PATMODE b);
        partial void Testctrl_Flashtest_Patmode_Read(TESTCTRL_FLASHTEST_PATMODE a, TESTCTRL_FLASHTEST_PATMODE b);
        partial void Testctrl_Flashtest_Patmode_ValueProvider(TESTCTRL_FLASHTEST_PATMODE a);
        protected IFlagRegisterField testctrl_flashtest_patinfoen_bit;
        partial void Testctrl_Flashtest_Patinfoen_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Patinfoen_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Patinfoen_ValueProvider(bool a);
        protected IEnumRegisterField<TESTCTRL_FLASHTEST_SEMODE> testctrl_flashtest_semode_field;
        partial void Testctrl_Flashtest_Semode_Write(TESTCTRL_FLASHTEST_SEMODE a, TESTCTRL_FLASHTEST_SEMODE b);
        partial void Testctrl_Flashtest_Semode_Read(TESTCTRL_FLASHTEST_SEMODE a, TESTCTRL_FLASHTEST_SEMODE b);
        partial void Testctrl_Flashtest_Semode_ValueProvider(TESTCTRL_FLASHTEST_SEMODE a);
        protected IEnumRegisterField<TESTCTRL_FLASHTEST_XADRINC> testctrl_flashtest_xadrinc_bit;
        partial void Testctrl_Flashtest_Xadrinc_Write(TESTCTRL_FLASHTEST_XADRINC a, TESTCTRL_FLASHTEST_XADRINC b);
        partial void Testctrl_Flashtest_Xadrinc_Read(TESTCTRL_FLASHTEST_XADRINC a, TESTCTRL_FLASHTEST_XADRINC b);
        partial void Testctrl_Flashtest_Xadrinc_ValueProvider(TESTCTRL_FLASHTEST_XADRINC a);
        protected IFlagRegisterField testctrl_flashtest_paten_bit;
        partial void Testctrl_Flashtest_Paten_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Paten_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Paten_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_patreset_bit;
        partial void Testctrl_Flashtest_Patreset_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Patreset_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Patreset_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_lvemode_bit;
        partial void Testctrl_Flashtest_Lvemode_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Lvemode_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Lvemode_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_cdaen_bit;
        partial void Testctrl_Flashtest_Cdaen_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Cdaen_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Cdaen_ValueProvider(bool a);
        protected IFlagRegisterField testctrl_flashtest_flashtesten_bit;
        partial void Testctrl_Flashtest_Flashtesten_Write(bool a, bool b);
        partial void Testctrl_Flashtest_Flashtesten_Read(bool a, bool b);
        partial void Testctrl_Flashtest_Flashtesten_ValueProvider(bool a);

        partial void Testctrl_Flashtest_Write(uint a, uint b);
        partial void Testctrl_Flashtest_Read(uint a, uint b);
        
        // Patdiagctrl_Flashtest - Offset : 0x1A4
        protected IValueRegisterField patdiagctrl_flashtest_infodiagxadr_field;
        partial void Patdiagctrl_Flashtest_Infodiagxadr_Write(ulong a, ulong b);
        partial void Patdiagctrl_Flashtest_Infodiagxadr_Read(ulong a, ulong b);
        partial void Patdiagctrl_Flashtest_Infodiagxadr_ValueProvider(ulong a);
        protected IEnumRegisterField<PATDIAGCTRL_FLASHTEST_INFODIAGINCR> patdiagctrl_flashtest_infodiagincr_bit;
        partial void Patdiagctrl_Flashtest_Infodiagincr_Write(PATDIAGCTRL_FLASHTEST_INFODIAGINCR a, PATDIAGCTRL_FLASHTEST_INFODIAGINCR b);
        partial void Patdiagctrl_Flashtest_Infodiagincr_Read(PATDIAGCTRL_FLASHTEST_INFODIAGINCR a, PATDIAGCTRL_FLASHTEST_INFODIAGINCR b);
        partial void Patdiagctrl_Flashtest_Infodiagincr_ValueProvider(PATDIAGCTRL_FLASHTEST_INFODIAGINCR a);
        protected IValueRegisterField patdiagctrl_flashtest_maindiagxadr_field;
        partial void Patdiagctrl_Flashtest_Maindiagxadr_Write(ulong a, ulong b);
        partial void Patdiagctrl_Flashtest_Maindiagxadr_Read(ulong a, ulong b);
        partial void Patdiagctrl_Flashtest_Maindiagxadr_ValueProvider(ulong a);
        protected IEnumRegisterField<PATDIAGCTRL_FLASHTEST_MAINDIAGINCR> patdiagctrl_flashtest_maindiagincr_bit;
        partial void Patdiagctrl_Flashtest_Maindiagincr_Write(PATDIAGCTRL_FLASHTEST_MAINDIAGINCR a, PATDIAGCTRL_FLASHTEST_MAINDIAGINCR b);
        partial void Patdiagctrl_Flashtest_Maindiagincr_Read(PATDIAGCTRL_FLASHTEST_MAINDIAGINCR a, PATDIAGCTRL_FLASHTEST_MAINDIAGINCR b);
        partial void Patdiagctrl_Flashtest_Maindiagincr_ValueProvider(PATDIAGCTRL_FLASHTEST_MAINDIAGINCR a);

        partial void Patdiagctrl_Flashtest_Write(uint a, uint b);
        partial void Patdiagctrl_Flashtest_Read(uint a, uint b);
        
        // Pataddr_Flashtest - Offset : 0x1A8
        protected IValueRegisterField pataddr_flashtest_pataddr_field;
        partial void Pataddr_Flashtest_Pataddr_Read(ulong a, ulong b);
        partial void Pataddr_Flashtest_Pataddr_ValueProvider(ulong a);

        partial void Pataddr_Flashtest_Write(uint a, uint b);
        partial void Pataddr_Flashtest_Read(uint a, uint b);
        
        // Patdoneaddr_Flashtest - Offset : 0x1AC
        protected IValueRegisterField patdoneaddr_flashtest_patdoneaddr_field;
        partial void Patdoneaddr_Flashtest_Patdoneaddr_Write(ulong a, ulong b);
        partial void Patdoneaddr_Flashtest_Patdoneaddr_Read(ulong a, ulong b);
        partial void Patdoneaddr_Flashtest_Patdoneaddr_ValueProvider(ulong a);

        partial void Patdoneaddr_Flashtest_Write(uint a, uint b);
        partial void Patdoneaddr_Flashtest_Read(uint a, uint b);
        
        // Patstatus_Flashtest - Offset : 0x1B0
        protected IFlagRegisterField patstatus_flashtest_patdone_bit;
        partial void Patstatus_Flashtest_Patdone_Read(bool a, bool b);
        partial void Patstatus_Flashtest_Patdone_ValueProvider(bool a);
        protected IFlagRegisterField patstatus_flashtest_inst0patpass_bit;
        partial void Patstatus_Flashtest_Inst0patpass_Read(bool a, bool b);
        partial void Patstatus_Flashtest_Inst0patpass_ValueProvider(bool a);
        protected IFlagRegisterField patstatus_flashtest_interfacerdy_bit;
        partial void Patstatus_Flashtest_Interfacerdy_Read(bool a, bool b);
        partial void Patstatus_Flashtest_Interfacerdy_ValueProvider(bool a);

        partial void Patstatus_Flashtest_Write(uint a, uint b);
        partial void Patstatus_Flashtest_Read(uint a, uint b);
        
        // Testredundancy_Flashtest - Offset : 0x1BC
        protected IFlagRegisterField testredundancy_flashtest_reden_bit;
        partial void Testredundancy_Flashtest_Reden_Write(bool a, bool b);
        partial void Testredundancy_Flashtest_Reden_Read(bool a, bool b);
        partial void Testredundancy_Flashtest_Reden_ValueProvider(bool a);
        protected IFlagRegisterField testredundancy_flashtest_ifren1_bit;
        partial void Testredundancy_Flashtest_Ifren1_Write(bool a, bool b);
        partial void Testredundancy_Flashtest_Ifren1_Read(bool a, bool b);
        partial void Testredundancy_Flashtest_Ifren1_ValueProvider(bool a);

        partial void Testredundancy_Flashtest_Write(uint a, uint b);
        partial void Testredundancy_Flashtest_Read(uint a, uint b);
        
        // Testlock_Flashtest - Offset : 0x1C0
        protected IFlagRegisterField testlock_flashtest_testlock_bit;
        partial void Testlock_Flashtest_Testlock_Write(bool a, bool b);
        partial void Testlock_Flashtest_Testlock_Read(bool a, bool b);
        partial void Testlock_Flashtest_Testlock_ValueProvider(bool a);

        partial void Testlock_Flashtest_Write(uint a, uint b);
        partial void Testlock_Flashtest_Read(uint a, uint b);
        
        // Rpuratd0_Drpu - Offset : 0x1C4
        protected IFlagRegisterField rpuratd0_drpu_ratdmscreadctrl_bit;
        partial void Rpuratd0_Drpu_Ratdmscreadctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscreadctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscreadctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscrdatactrl_bit;
        partial void Rpuratd0_Drpu_Ratdmscrdatactrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscrdatactrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscrdatactrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscwritectrl_bit;
        partial void Rpuratd0_Drpu_Ratdmscwritectrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscwritectrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscwritectrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscwritecmd_bit;
        partial void Rpuratd0_Drpu_Ratdmscwritecmd_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscwritecmd_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscwritecmd_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscaddrb_bit;
        partial void Rpuratd0_Drpu_Ratdmscaddrb_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscaddrb_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscaddrb_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscwdata_bit;
        partial void Rpuratd0_Drpu_Ratdmscwdata_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscwdata_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscwdata_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscif_bit;
        partial void Rpuratd0_Drpu_Ratdmscif_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscif_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscien_bit;
        partial void Rpuratd0_Drpu_Ratdmscien_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscien_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmsccmd_bit;
        partial void Rpuratd0_Drpu_Ratdmsccmd_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmsccmd_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmsccmd_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmsclock_bit;
        partial void Rpuratd0_Drpu_Ratdmsclock_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmsclock_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmsclock_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscmisclockword_bit;
        partial void Rpuratd0_Drpu_Ratdmscmisclockword_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscmisclockword_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscmisclockword_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd0_drpu_ratdmscpwrctrl_bit;
        partial void Rpuratd0_Drpu_Ratdmscpwrctrl_Write(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscpwrctrl_Read(bool a, bool b);
        partial void Rpuratd0_Drpu_Ratdmscpwrctrl_ValueProvider(bool a);

        partial void Rpuratd0_Drpu_Write(uint a, uint b);
        partial void Rpuratd0_Drpu_Read(uint a, uint b);
        
        // Rpuratd1_Drpu - Offset : 0x1C8
        protected IFlagRegisterField rpuratd1_drpu_ratdmscsewritectrl_bit;
        partial void Rpuratd1_Drpu_Ratdmscsewritectrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsewritectrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsewritectrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscsewritecmd_bit;
        partial void Rpuratd1_Drpu_Ratdmscsewritecmd_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsewritecmd_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsewritecmd_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscseaddrb_bit;
        partial void Rpuratd1_Drpu_Ratdmscseaddrb_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscseaddrb_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscseaddrb_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscsewdata_bit;
        partial void Rpuratd1_Drpu_Ratdmscsewdata_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsewdata_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsewdata_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscseif_bit;
        partial void Rpuratd1_Drpu_Ratdmscseif_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscseif_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscseif_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscseien_bit;
        partial void Rpuratd1_Drpu_Ratdmscseien_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscseien_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscseien_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscmemfeature_bit;
        partial void Rpuratd1_Drpu_Ratdmscmemfeature_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscmemfeature_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscmemfeature_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscstartup_bit;
        partial void Rpuratd1_Drpu_Ratdmscstartup_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscstartup_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscstartup_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscserdatactrl_bit;
        partial void Rpuratd1_Drpu_Ratdmscserdatactrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscserdatactrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscserdatactrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscsepwrskip_bit;
        partial void Rpuratd1_Drpu_Ratdmscsepwrskip_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsepwrskip_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscsepwrskip_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscmtpctrl_bit;
        partial void Rpuratd1_Drpu_Ratdmscmtpctrl_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscmtpctrl_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscmtpctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscmtpsize_bit;
        partial void Rpuratd1_Drpu_Ratdmscmtpsize_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscmtpsize_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscmtpsize_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscotperase_bit;
        partial void Rpuratd1_Drpu_Ratdmscotperase_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscotperase_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscotperase_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscflasherasetime_bit;
        partial void Rpuratd1_Drpu_Ratdmscflasherasetime_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscflasherasetime_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscflasherasetime_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscflashprogtime_bit;
        partial void Rpuratd1_Drpu_Ratdmscflashprogtime_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscflashprogtime_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscflashprogtime_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd1_drpu_ratdmscselock_bit;
        partial void Rpuratd1_Drpu_Ratdmscselock_Write(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscselock_Read(bool a, bool b);
        partial void Rpuratd1_Drpu_Ratdmscselock_ValueProvider(bool a);

        partial void Rpuratd1_Drpu_Write(uint a, uint b);
        partial void Rpuratd1_Drpu_Read(uint a, uint b);
        
        // Rpuratd2_Drpu - Offset : 0x1CC
        protected IFlagRegisterField rpuratd2_drpu_ratdinstpagelockword0_bit;
        partial void Rpuratd2_Drpu_Ratdinstpagelockword0_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword0_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword0_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdinstpagelockword1_bit;
        partial void Rpuratd2_Drpu_Ratdinstpagelockword1_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword1_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword1_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdinstpagelockword2_bit;
        partial void Rpuratd2_Drpu_Ratdinstpagelockword2_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword2_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword2_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdinstpagelockword3_bit;
        partial void Rpuratd2_Drpu_Ratdinstpagelockword3_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword3_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword3_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdinstpagelockword4_bit;
        partial void Rpuratd2_Drpu_Ratdinstpagelockword4_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword4_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword4_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdinstpagelockword5_bit;
        partial void Rpuratd2_Drpu_Ratdinstpagelockword5_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword5_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstpagelockword5_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdinstrepaddr0_bit;
        partial void Rpuratd2_Drpu_Ratdinstrepaddr0_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstrepaddr0_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstrepaddr0_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd2_drpu_ratdinstrepaddr1_bit;
        partial void Rpuratd2_Drpu_Ratdinstrepaddr1_Write(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstrepaddr1_Read(bool a, bool b);
        partial void Rpuratd2_Drpu_Ratdinstrepaddr1_ValueProvider(bool a);

        partial void Rpuratd2_Drpu_Write(uint a, uint b);
        partial void Rpuratd2_Drpu_Read(uint a, uint b);
        
        // Rpuratd3_Drpu - Offset : 0x1D0
        protected IFlagRegisterField rpuratd3_drpu_ratdmsctestctrl_bit;
        partial void Rpuratd3_Drpu_Ratdmsctestctrl_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmsctestctrl_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmsctestctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd3_drpu_ratdmscpatdiagctrl_bit;
        partial void Rpuratd3_Drpu_Ratdmscpatdiagctrl_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmscpatdiagctrl_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmscpatdiagctrl_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd3_drpu_ratdmscpatdoneaddr_bit;
        partial void Rpuratd3_Drpu_Ratdmscpatdoneaddr_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmscpatdoneaddr_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmscpatdoneaddr_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd3_drpu_ratdmsctestredundancy_bit;
        partial void Rpuratd3_Drpu_Ratdmsctestredundancy_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmsctestredundancy_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmsctestredundancy_ValueProvider(bool a);
        protected IFlagRegisterField rpuratd3_drpu_ratdmsctestlock_bit;
        partial void Rpuratd3_Drpu_Ratdmsctestlock_Write(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmsctestlock_Read(bool a, bool b);
        partial void Rpuratd3_Drpu_Ratdmsctestlock_ValueProvider(bool a);

        partial void Rpuratd3_Drpu_Write(uint a, uint b);
        partial void Rpuratd3_Drpu_Read(uint a, uint b);
        
        partial void MSC_Reset();

        partial void Msc_3_Constructor();

        public bool Enabled = true;

        private ICmu _cmu;
        private ICmu cmu
        {
            get
            {
                if (Object.ReferenceEquals(_cmu, null))
                {
                    foreach(var cmu in machine.GetPeripheralsOfType<ICmu>())
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
            switch(offset & 0xF000){
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
            switch(address & 0xF000){
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
                    this.Log(LogLevel.Error, "writing doubleWord to non existing offset {0:X}, case : {1:X}", address, address & 0xF000);
                    break;
            }           
        }

        protected enum Registers
        {
            Ipversion = 0x0,
            Readctrl = 0x4,
            Rdatactrl = 0x8,
            Writectrl = 0xC,
            Writecmd = 0x10,
            Addrb = 0x14,
            Wdata = 0x18,
            Status = 0x1C,
            If = 0x20,
            Ien = 0x24,
            Userdatasize = 0x34,
            Cmd = 0x38,
            Lock = 0x3C,
            Misclockword = 0x40,
            Pwrctrl = 0x50,
            Sewritectrl = 0x80,
            Sewritecmd = 0x84,
            Seaddrb = 0x88,
            Sewdata = 0x8C,
            Sestatus = 0x90,
            Seif = 0x94,
            Seien = 0x98,
            Memfeature = 0x9C,
            Startup = 0xA0,
            Serdatactrl = 0xA4,
            Sepwrskip = 0xA8,
            Mtpctrl = 0xAC,
            Mtpsize = 0xB0,
            Otperase = 0xB4,
            Flasherasetime = 0xC0,
            Flashprogtime = 0xC4,
            Selock = 0xC8,
            Pagelock0 = 0x120,
            Pagelock1 = 0x124,
            Pagelock2 = 0x128,
            Pagelock3 = 0x12C,
            Pagelock4 = 0x130,
            Pagelock5 = 0x134,
            Repaddr0_Repinst = 0x140,
            Repaddr1_Repinst = 0x144,
            Dout0_Doutinst = 0x160,
            Dout1_Doutinst = 0x164,
            Testctrl_Flashtest = 0x1A0,
            Patdiagctrl_Flashtest = 0x1A4,
            Pataddr_Flashtest = 0x1A8,
            Patdoneaddr_Flashtest = 0x1AC,
            Patstatus_Flashtest = 0x1B0,
            Testredundancy_Flashtest = 0x1BC,
            Testlock_Flashtest = 0x1C0,
            Rpuratd0_Drpu = 0x1C4,
            Rpuratd1_Drpu = 0x1C8,
            Rpuratd2_Drpu = 0x1CC,
            Rpuratd3_Drpu = 0x1D0,
            
            Ipversion_SET = 0x1000,
            Readctrl_SET = 0x1004,
            Rdatactrl_SET = 0x1008,
            Writectrl_SET = 0x100C,
            Writecmd_SET = 0x1010,
            Addrb_SET = 0x1014,
            Wdata_SET = 0x1018,
            Status_SET = 0x101C,
            If_SET = 0x1020,
            Ien_SET = 0x1024,
            Userdatasize_SET = 0x1034,
            Cmd_SET = 0x1038,
            Lock_SET = 0x103C,
            Misclockword_SET = 0x1040,
            Pwrctrl_SET = 0x1050,
            Sewritectrl_SET = 0x1080,
            Sewritecmd_SET = 0x1084,
            Seaddrb_SET = 0x1088,
            Sewdata_SET = 0x108C,
            Sestatus_SET = 0x1090,
            Seif_SET = 0x1094,
            Seien_SET = 0x1098,
            Memfeature_SET = 0x109C,
            Startup_SET = 0x10A0,
            Serdatactrl_SET = 0x10A4,
            Sepwrskip_SET = 0x10A8,
            Mtpctrl_SET = 0x10AC,
            Mtpsize_SET = 0x10B0,
            Otperase_SET = 0x10B4,
            Flasherasetime_SET = 0x10C0,
            Flashprogtime_SET = 0x10C4,
            Selock_SET = 0x10C8,
            Pagelock0_SET = 0x1120,
            Pagelock1_SET = 0x1124,
            Pagelock2_SET = 0x1128,
            Pagelock3_SET = 0x112C,
            Pagelock4_SET = 0x1130,
            Pagelock5_SET = 0x1134,
            Repaddr0_Repinst_SET = 0x1140,
            Repaddr1_Repinst_SET = 0x1144,
            Dout0_Doutinst_SET = 0x1160,
            Dout1_Doutinst_SET = 0x1164,
            Testctrl_Flashtest_SET = 0x11A0,
            Patdiagctrl_Flashtest_SET = 0x11A4,
            Pataddr_Flashtest_SET = 0x11A8,
            Patdoneaddr_Flashtest_SET = 0x11AC,
            Patstatus_Flashtest_SET = 0x11B0,
            Testredundancy_Flashtest_SET = 0x11BC,
            Testlock_Flashtest_SET = 0x11C0,
            Rpuratd0_Drpu_SET = 0x11C4,
            Rpuratd1_Drpu_SET = 0x11C8,
            Rpuratd2_Drpu_SET = 0x11CC,
            Rpuratd3_Drpu_SET = 0x11D0,
            
            Ipversion_CLR = 0x2000,
            Readctrl_CLR = 0x2004,
            Rdatactrl_CLR = 0x2008,
            Writectrl_CLR = 0x200C,
            Writecmd_CLR = 0x2010,
            Addrb_CLR = 0x2014,
            Wdata_CLR = 0x2018,
            Status_CLR = 0x201C,
            If_CLR = 0x2020,
            Ien_CLR = 0x2024,
            Userdatasize_CLR = 0x2034,
            Cmd_CLR = 0x2038,
            Lock_CLR = 0x203C,
            Misclockword_CLR = 0x2040,
            Pwrctrl_CLR = 0x2050,
            Sewritectrl_CLR = 0x2080,
            Sewritecmd_CLR = 0x2084,
            Seaddrb_CLR = 0x2088,
            Sewdata_CLR = 0x208C,
            Sestatus_CLR = 0x2090,
            Seif_CLR = 0x2094,
            Seien_CLR = 0x2098,
            Memfeature_CLR = 0x209C,
            Startup_CLR = 0x20A0,
            Serdatactrl_CLR = 0x20A4,
            Sepwrskip_CLR = 0x20A8,
            Mtpctrl_CLR = 0x20AC,
            Mtpsize_CLR = 0x20B0,
            Otperase_CLR = 0x20B4,
            Flasherasetime_CLR = 0x20C0,
            Flashprogtime_CLR = 0x20C4,
            Selock_CLR = 0x20C8,
            Pagelock0_CLR = 0x2120,
            Pagelock1_CLR = 0x2124,
            Pagelock2_CLR = 0x2128,
            Pagelock3_CLR = 0x212C,
            Pagelock4_CLR = 0x2130,
            Pagelock5_CLR = 0x2134,
            Repaddr0_Repinst_CLR = 0x2140,
            Repaddr1_Repinst_CLR = 0x2144,
            Dout0_Doutinst_CLR = 0x2160,
            Dout1_Doutinst_CLR = 0x2164,
            Testctrl_Flashtest_CLR = 0x21A0,
            Patdiagctrl_Flashtest_CLR = 0x21A4,
            Pataddr_Flashtest_CLR = 0x21A8,
            Patdoneaddr_Flashtest_CLR = 0x21AC,
            Patstatus_Flashtest_CLR = 0x21B0,
            Testredundancy_Flashtest_CLR = 0x21BC,
            Testlock_Flashtest_CLR = 0x21C0,
            Rpuratd0_Drpu_CLR = 0x21C4,
            Rpuratd1_Drpu_CLR = 0x21C8,
            Rpuratd2_Drpu_CLR = 0x21CC,
            Rpuratd3_Drpu_CLR = 0x21D0,
            
            Ipversion_TGL = 0x3000,
            Readctrl_TGL = 0x3004,
            Rdatactrl_TGL = 0x3008,
            Writectrl_TGL = 0x300C,
            Writecmd_TGL = 0x3010,
            Addrb_TGL = 0x3014,
            Wdata_TGL = 0x3018,
            Status_TGL = 0x301C,
            If_TGL = 0x3020,
            Ien_TGL = 0x3024,
            Userdatasize_TGL = 0x3034,
            Cmd_TGL = 0x3038,
            Lock_TGL = 0x303C,
            Misclockword_TGL = 0x3040,
            Pwrctrl_TGL = 0x3050,
            Sewritectrl_TGL = 0x3080,
            Sewritecmd_TGL = 0x3084,
            Seaddrb_TGL = 0x3088,
            Sewdata_TGL = 0x308C,
            Sestatus_TGL = 0x3090,
            Seif_TGL = 0x3094,
            Seien_TGL = 0x3098,
            Memfeature_TGL = 0x309C,
            Startup_TGL = 0x30A0,
            Serdatactrl_TGL = 0x30A4,
            Sepwrskip_TGL = 0x30A8,
            Mtpctrl_TGL = 0x30AC,
            Mtpsize_TGL = 0x30B0,
            Otperase_TGL = 0x30B4,
            Flasherasetime_TGL = 0x30C0,
            Flashprogtime_TGL = 0x30C4,
            Selock_TGL = 0x30C8,
            Pagelock0_TGL = 0x3120,
            Pagelock1_TGL = 0x3124,
            Pagelock2_TGL = 0x3128,
            Pagelock3_TGL = 0x312C,
            Pagelock4_TGL = 0x3130,
            Pagelock5_TGL = 0x3134,
            Repaddr0_Repinst_TGL = 0x3140,
            Repaddr1_Repinst_TGL = 0x3144,
            Dout0_Doutinst_TGL = 0x3160,
            Dout1_Doutinst_TGL = 0x3164,
            Testctrl_Flashtest_TGL = 0x31A0,
            Patdiagctrl_Flashtest_TGL = 0x31A4,
            Pataddr_Flashtest_TGL = 0x31A8,
            Patdoneaddr_Flashtest_TGL = 0x31AC,
            Patstatus_Flashtest_TGL = 0x31B0,
            Testredundancy_Flashtest_TGL = 0x31BC,
            Testlock_Flashtest_TGL = 0x31C0,
            Rpuratd0_Drpu_TGL = 0x31C4,
            Rpuratd1_Drpu_TGL = 0x31C8,
            Rpuratd2_Drpu_TGL = 0x31CC,
            Rpuratd3_Drpu_TGL = 0x31D0,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}