//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    ULFRCO, Generated on : 2023-10-25 13:47:34.122649
    ULFRCO, ID Version : 134310a064d24b21ac97496e79d6c143.1 */

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
    public partial class Ulfrco_1
    {
        public Ulfrco_1(Machine machine) : base(machine)
        {
            Ulfrco_1_constructor();
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
    public partial class Ulfrco_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public Ulfrco_1(Machine machine) : base(machine)
        {
            Define_Registers();
            Ulfrco_1_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.Status1, GenerateStatus1Register()},
                {(long)Registers.Cal, GenerateCalRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Syncbusy, GenerateSyncbusyRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Dutymodecal, GenerateDutymodecalRegister()},
                {(long)Registers.Syncbusy1, GenerateSyncbusy1Register()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            ULFRCO_Reset();
        }
        
        protected enum STATUS1_LOCK
        {
            UNLOCKED = 0, // All ULFRCO lockable registers are unlocked.
            LOCKED = 1, // All ULFRCO lockable registers are locked.
        }
        
        protected enum DUTYMODECAL_ULFRCOMODE
        {
            FREQ_1KHZ = 0, // ULFRCODUTY Clock Frequency = 1 kHz
            FREQ_2KHZ = 1, // ULFRCODUTY Clock Frequency = 2 kHz
            FREQ_4KHZ = 2, // ULFRCODUTY Clock Frequency = 4 kHz
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x1)
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
            .WithReservedBits(17, 15)
            .WithReadCallback((_, __) => Status_Read(_, __))
            .WithWriteCallback((_, __) => Status_Write(_, __));
        
        // Status1 - Offset : 0xC
        protected DoubleWordRegister  GenerateStatus1Register() => new DoubleWordRegister(this, 0x0)
            .WithEnumField<DoubleWordRegister, STATUS1_LOCK>(0, 1, out status1_lock_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status1_Lock_ValueProvider(_);
                        return status1_lock_bit.Value;               
                    },
                    readCallback: (_, __) => Status1_Lock_Read(_, __),
                    name: "Lock")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Status1_Read(_, __))
            .WithWriteCallback((_, __) => Status1_Write(_, __));
        
        // Cal - Offset : 0x10
        protected DoubleWordRegister  GenerateCalRegister() => new DoubleWordRegister(this, 0x60)
            .WithValueField(0, 7, out cal_freqtrim_field, 
                    valueProviderCallback: (_) => {
                        Cal_Freqtrim_ValueProvider(_);
                        return cal_freqtrim_field.Value;               
                    },
                    writeCallback: (_, __) => Cal_Freqtrim_Write(_, __),
                    readCallback: (_, __) => Cal_Freqtrim_Read(_, __),
                    name: "Freqtrim")
            .WithReservedBits(7, 25)
            .WithReadCallback((_, __) => Cal_Read(_, __))
            .WithWriteCallback((_, __) => Cal_Write(_, __));
        
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
            .WithReservedBits(3, 29)
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
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Syncbusy - Offset : 0x1C
        protected DoubleWordRegister  GenerateSyncbusyRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy_calbsy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy_Calbsy_ValueProvider(_);
                        return syncbusy_calbsy_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy_Calbsy_Read(_, __),
                    name: "Calbsy")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Syncbusy_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy_Write(_, __));
        
        // Lock - Offset : 0x20
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x80EB)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Dutymodecal - Offset : 0x24
        protected DoubleWordRegister  GenerateDutymodecalRegister() => new DoubleWordRegister(this, 0x20)
            .WithFlag(0, out dutymodecal_override_bit, 
                    valueProviderCallback: (_) => {
                        Dutymodecal_Override_ValueProvider(_);
                        return dutymodecal_override_bit.Value;               
                    },
                    writeCallback: (_, __) => Dutymodecal_Override_Write(_, __),
                    readCallback: (_, __) => Dutymodecal_Override_Read(_, __),
                    name: "Override")
            .WithReservedBits(1, 3)
            .WithEnumField<DoubleWordRegister, DUTYMODECAL_ULFRCOMODE>(4, 2, out dutymodecal_ulfrcomode_field, 
                    valueProviderCallback: (_) => {
                        Dutymodecal_Ulfrcomode_ValueProvider(_);
                        return dutymodecal_ulfrcomode_field.Value;               
                    },
                    writeCallback: (_, __) => Dutymodecal_Ulfrcomode_Write(_, __),
                    readCallback: (_, __) => Dutymodecal_Ulfrcomode_Read(_, __),
                    name: "Ulfrcomode")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Dutymodecal_Read(_, __))
            .WithWriteCallback((_, __) => Dutymodecal_Write(_, __));
        
        // Syncbusy1 - Offset : 0x28
        protected DoubleWordRegister  GenerateSyncbusy1Register() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out syncbusy1_dutymodecalbsy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Syncbusy1_Dutymodecalbsy_ValueProvider(_);
                        return syncbusy1_dutymodecalbsy_bit.Value;               
                    },
                    readCallback: (_, __) => Syncbusy1_Dutymodecalbsy_Read(_, __),
                    name: "Dutymodecalbsy")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Syncbusy1_Read(_, __))
            .WithWriteCallback((_, __) => Syncbusy1_Write(_, __));
        

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

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // Status1 - Offset : 0xC
        protected IEnumRegisterField<STATUS1_LOCK> status1_lock_bit;
        partial void Status1_Lock_Read(STATUS1_LOCK a, STATUS1_LOCK b);
        partial void Status1_Lock_ValueProvider(STATUS1_LOCK a);

        partial void Status1_Write(uint a, uint b);
        partial void Status1_Read(uint a, uint b);
        
        // Cal - Offset : 0x10
        protected IValueRegisterField cal_freqtrim_field;
        partial void Cal_Freqtrim_Write(ulong a, ulong b);
        partial void Cal_Freqtrim_Read(ulong a, ulong b);
        partial void Cal_Freqtrim_ValueProvider(ulong a);

        partial void Cal_Write(uint a, uint b);
        partial void Cal_Read(uint a, uint b);
        
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

        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Syncbusy - Offset : 0x1C
        protected IFlagRegisterField syncbusy_calbsy_bit;
        partial void Syncbusy_Calbsy_Read(bool a, bool b);
        partial void Syncbusy_Calbsy_ValueProvider(bool a);

        partial void Syncbusy_Write(uint a, uint b);
        partial void Syncbusy_Read(uint a, uint b);
        
        // Lock - Offset : 0x20
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // Dutymodecal - Offset : 0x24
        protected IFlagRegisterField dutymodecal_override_bit;
        partial void Dutymodecal_Override_Write(bool a, bool b);
        partial void Dutymodecal_Override_Read(bool a, bool b);
        partial void Dutymodecal_Override_ValueProvider(bool a);
        protected IEnumRegisterField<DUTYMODECAL_ULFRCOMODE> dutymodecal_ulfrcomode_field;
        partial void Dutymodecal_Ulfrcomode_Write(DUTYMODECAL_ULFRCOMODE a, DUTYMODECAL_ULFRCOMODE b);
        partial void Dutymodecal_Ulfrcomode_Read(DUTYMODECAL_ULFRCOMODE a, DUTYMODECAL_ULFRCOMODE b);
        partial void Dutymodecal_Ulfrcomode_ValueProvider(DUTYMODECAL_ULFRCOMODE a);

        partial void Dutymodecal_Write(uint a, uint b);
        partial void Dutymodecal_Read(uint a, uint b);
        
        // Syncbusy1 - Offset : 0x28
        protected IFlagRegisterField syncbusy1_dutymodecalbsy_bit;
        partial void Syncbusy1_Dutymodecalbsy_Read(bool a, bool b);
        partial void Syncbusy1_Dutymodecalbsy_ValueProvider(bool a);

        partial void Syncbusy1_Write(uint a, uint b);
        partial void Syncbusy1_Read(uint a, uint b);
        
        partial void ULFRCO_Reset();

        partial void Ulfrco_1_Constructor();

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
            Status1 = 0xC,
            Cal = 0x10,
            If = 0x14,
            Ien = 0x18,
            Syncbusy = 0x1C,
            Lock = 0x20,
            Dutymodecal = 0x24,
            Syncbusy1 = 0x28,
            
            Ipversion_SET = 0x1000,
            Ctrl_SET = 0x1004,
            Status_SET = 0x1008,
            Status1_SET = 0x100C,
            Cal_SET = 0x1010,
            If_SET = 0x1014,
            Ien_SET = 0x1018,
            Syncbusy_SET = 0x101C,
            Lock_SET = 0x1020,
            Dutymodecal_SET = 0x1024,
            Syncbusy1_SET = 0x1028,
            
            Ipversion_CLR = 0x2000,
            Ctrl_CLR = 0x2004,
            Status_CLR = 0x2008,
            Status1_CLR = 0x200C,
            Cal_CLR = 0x2010,
            If_CLR = 0x2014,
            Ien_CLR = 0x2018,
            Syncbusy_CLR = 0x201C,
            Lock_CLR = 0x2020,
            Dutymodecal_CLR = 0x2024,
            Syncbusy1_CLR = 0x2028,
            
            Ipversion_TGL = 0x3000,
            Ctrl_TGL = 0x3004,
            Status_TGL = 0x3008,
            Status1_TGL = 0x300C,
            Cal_TGL = 0x3010,
            If_TGL = 0x3014,
            Ien_TGL = 0x3018,
            Syncbusy_TGL = 0x301C,
            Lock_TGL = 0x3020,
            Dutymodecal_TGL = 0x3024,
            Syncbusy1_TGL = 0x3028,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}