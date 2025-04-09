//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2022-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    HFRCO, Generated on : 2023-07-20 14:23:50.603902
    HFRCO, ID Version : 165adedf604742fda856a08648e115e5.2 */

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
    public partial class EFR32xG2_HFRCO_2
    {
        public EFR32xG2_HFRCO_2(Machine machine) : base(machine)
        {
            EFR32xG2_HFRCO_2_constructor();
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
    public partial class EFR32xG2_HFRCO_2 : BasicDoubleWordPeripheral, IKnownSize
    {
        public EFR32xG2_HFRCO_2(Machine machine) : base(machine)
        {
            Define_Registers();
            EFR32xG2_HFRCO_2_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Cal, GenerateCalRegister()},
                {(long)Registers.Status, GenerateStatusRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Lock, GenerateLockRegister()},
                {(long)Registers.Test, GenerateTestRegister()},
                {(long)Registers.Feature, GenerateFeatureRegister()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            HFRCO_Reset();
        }
        
        protected enum CAL_CLKDIV
        {
            DIV1 = 0, // Divide by 1.
            DIV2 = 1, // Divide by 2.
            DIV4 = 2, // Divide by 4.
        }
        
        protected enum STATUS_LOCK
        {
            UNLOCKED = 0, // HFRCO is unlocked
            LOCKED = 1, // HFRCO is locked
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
            .WithFlag(2, out ctrl_em23ondemand_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Em23ondemand_ValueProvider(_);
                        return ctrl_em23ondemand_bit.Value;               
                    },
                    writeCallback: (_, __) => Ctrl_Em23ondemand_Write(_, __),
                    readCallback: (_, __) => Ctrl_Em23ondemand_Read(_, __),
                    name: "Em23ondemand")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write(_, __));
        
        // Cal - Offset : 0x8
        protected DoubleWordRegister  GenerateCalRegister() => new DoubleWordRegister(this, 0xA8689F7F)
            .WithValueField(0, 7, out cal_tuning_field, 
                    valueProviderCallback: (_) => {
                        Cal_Tuning_ValueProvider(_);
                        return cal_tuning_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Tuning_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Tuning_Read(_, __),
                    name: "Tuning")
            .WithReservedBits(7, 1)
            .WithValueField(8, 6, out cal_finetuning_field, 
                    valueProviderCallback: (_) => {
                        Cal_Finetuning_ValueProvider(_);
                        return cal_finetuning_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Finetuning_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Finetuning_Read(_, __),
                    name: "Finetuning")
            .WithReservedBits(14, 1)
            .WithFlag(15, out cal_ldohp_bit, 
                    valueProviderCallback: (_) => {
                        Cal_Ldohp_ValueProvider(_);
                        return cal_ldohp_bit.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Ldohp_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Ldohp_Read(_, __),
                    name: "Ldohp")
            .WithValueField(16, 5, out cal_freqrange_field, 
                    valueProviderCallback: (_) => {
                        Cal_Freqrange_ValueProvider(_);
                        return cal_freqrange_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Freqrange_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Freqrange_Read(_, __),
                    name: "Freqrange")
            .WithValueField(21, 3, out cal_cmpbias_field, 
                    valueProviderCallback: (_) => {
                        Cal_Cmpbias_ValueProvider(_);
                        return cal_cmpbias_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Cmpbias_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Cmpbias_Read(_, __),
                    name: "Cmpbias")
            .WithEnumField<DoubleWordRegister, CAL_CLKDIV>(24, 2, out cal_clkdiv_field, 
                    valueProviderCallback: (_) => {
                        Cal_Clkdiv_ValueProvider(_);
                        return cal_clkdiv_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Clkdiv_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Clkdiv_Read(_, __),
                    name: "Clkdiv")
            .WithValueField(26, 2, out cal_cmpsel_field, 
                    valueProviderCallback: (_) => {
                        Cal_Cmpsel_ValueProvider(_);
                        return cal_cmpsel_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Cmpsel_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Cmpsel_Read(_, __),
                    name: "Cmpsel")
            .WithValueField(28, 4, out cal_ireftc_field, 
                    valueProviderCallback: (_) => {
                        Cal_Ireftc_ValueProvider(_);
                        return cal_ireftc_field.Value;               
                    },
                    writeCallback: (_, __) => {
                        WriteRWSYNC();
                        Cal_Ireftc_Write(_, __);
                    },
                    readCallback: (_, __) => Cal_Ireftc_Read(_, __),
                    name: "Ireftc")
            .WithReadCallback((_, __) => Cal_Read(_, __))
            .WithWriteCallback((_, __) => Cal_Write(_, __));
        
        // Status - Offset : 0xC
        protected DoubleWordRegister  GenerateStatusRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out status_rdy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Rdy_ValueProvider(_);
                        return status_rdy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Rdy_Read(_, __),
                    name: "Rdy")
            .WithFlag(1, out status_freqbsy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Freqbsy_ValueProvider(_);
                        return status_freqbsy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Freqbsy_Read(_, __),
                    name: "Freqbsy")
            .WithFlag(2, out status_syncbusy_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Status_Syncbusy_ValueProvider(_);
                        return status_syncbusy_bit.Value;               
                    },
                    readCallback: (_, __) => Status_Syncbusy_Read(_, __),
                    name: "Syncbusy")
            .WithReservedBits(3, 13)
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
        
        // If - Offset : 0x10
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_rdy_bit, 
                    valueProviderCallback: (_) => {
                        If_Rdy_ValueProvider(_);
                        return if_rdy_bit.Value;               
                    },
                    writeCallback: (_, __) => If_Rdy_Write(_, __),
                    readCallback: (_, __) => If_Rdy_Read(_, __),
                    name: "Rdy")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x14
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_rdy_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Rdy_ValueProvider(_);
                        return ien_rdy_bit.Value;               
                    },
                    writeCallback: (_, __) => Ien_Rdy_Write(_, __),
                    readCallback: (_, __) => Ien_Rdy_Read(_, __),
                    name: "Rdy")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Lock - Offset : 0x1C
        protected DoubleWordRegister  GenerateLockRegister() => new DoubleWordRegister(this, 0x8195)
            .WithValueField(0, 16, out lock_lockkey_field, FieldMode.Write,
                    writeCallback: (_, __) => Lock_Lockkey_Write(_, __),
                    name: "Lockkey")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Lock_Read(_, __))
            .WithWriteCallback((_, __) => Lock_Write(_, __));
        
        // Test - Offset : 0x20
        protected DoubleWordRegister  GenerateTestRegister() => new DoubleWordRegister(this, 0x4)
            .WithFlag(0, out test_inv_bit, 
                    valueProviderCallback: (_) => {
                        Test_Inv_ValueProvider(_);
                        return test_inv_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Inv_Write(_, __),
                    readCallback: (_, __) => Test_Inv_Read(_, __),
                    name: "Inv")
            .WithFlag(1, out test_maxtoen_bit, 
                    valueProviderCallback: (_) => {
                        Test_Maxtoen_ValueProvider(_);
                        return test_maxtoen_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Maxtoen_Write(_, __),
                    readCallback: (_, __) => Test_Maxtoen_Read(_, __),
                    name: "Maxtoen")
            .WithFlag(2, out test_clkoutdis0_bit, 
                    valueProviderCallback: (_) => {
                        Test_Clkoutdis0_ValueProvider(_);
                        return test_clkoutdis0_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Clkoutdis0_Write(_, __),
                    readCallback: (_, __) => Test_Clkoutdis0_Read(_, __),
                    name: "Clkoutdis0")
            .WithFlag(3, out test_clkoutdis1_bit, 
                    valueProviderCallback: (_) => {
                        Test_Clkoutdis1_ValueProvider(_);
                        return test_clkoutdis1_bit.Value;               
                    },
                    writeCallback: (_, __) => Test_Clkoutdis1_Write(_, __),
                    readCallback: (_, __) => Test_Clkoutdis1_Read(_, __),
                    name: "Clkoutdis1")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Test_Read(_, __))
            .WithWriteCallback((_, __) => Test_Write(_, __));
        
        // Feature - Offset : 0x24
        protected DoubleWordRegister  GenerateFeatureRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out feature_force40_bit, 
                    valueProviderCallback: (_) => {
                        Feature_Force40_ValueProvider(_);
                        return feature_force40_bit.Value;               
                    },
                    writeCallback: (_, __) => Feature_Force40_Write(_, __),
                    readCallback: (_, __) => Feature_Force40_Read(_, __),
                    name: "Force40")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Feature_Read(_, __))
            .WithWriteCallback((_, __) => Feature_Write(_, __));
        

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
        
        // Ctrl - Offset : 0x4
        protected IFlagRegisterField ctrl_forceen_bit;
        partial void Ctrl_Forceen_Write(bool a, bool b);
        partial void Ctrl_Forceen_Read(bool a, bool b);
        partial void Ctrl_Forceen_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_disondemand_bit;
        partial void Ctrl_Disondemand_Write(bool a, bool b);
        partial void Ctrl_Disondemand_Read(bool a, bool b);
        partial void Ctrl_Disondemand_ValueProvider(bool a);
        protected IFlagRegisterField ctrl_em23ondemand_bit;
        partial void Ctrl_Em23ondemand_Write(bool a, bool b);
        partial void Ctrl_Em23ondemand_Read(bool a, bool b);
        partial void Ctrl_Em23ondemand_ValueProvider(bool a);

        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Cal - Offset : 0x8
        protected IValueRegisterField cal_tuning_field;
        partial void Cal_Tuning_Write(ulong a, ulong b);
        partial void Cal_Tuning_Read(ulong a, ulong b);
        partial void Cal_Tuning_ValueProvider(ulong a);
        protected IValueRegisterField cal_finetuning_field;
        partial void Cal_Finetuning_Write(ulong a, ulong b);
        partial void Cal_Finetuning_Read(ulong a, ulong b);
        partial void Cal_Finetuning_ValueProvider(ulong a);
        protected IFlagRegisterField cal_ldohp_bit;
        partial void Cal_Ldohp_Write(bool a, bool b);
        partial void Cal_Ldohp_Read(bool a, bool b);
        partial void Cal_Ldohp_ValueProvider(bool a);
        protected IValueRegisterField cal_freqrange_field;
        partial void Cal_Freqrange_Write(ulong a, ulong b);
        partial void Cal_Freqrange_Read(ulong a, ulong b);
        partial void Cal_Freqrange_ValueProvider(ulong a);
        protected IValueRegisterField cal_cmpbias_field;
        partial void Cal_Cmpbias_Write(ulong a, ulong b);
        partial void Cal_Cmpbias_Read(ulong a, ulong b);
        partial void Cal_Cmpbias_ValueProvider(ulong a);
        protected IEnumRegisterField<CAL_CLKDIV> cal_clkdiv_field;
        partial void Cal_Clkdiv_Write(CAL_CLKDIV a, CAL_CLKDIV b);
        partial void Cal_Clkdiv_Read(CAL_CLKDIV a, CAL_CLKDIV b);
        partial void Cal_Clkdiv_ValueProvider(CAL_CLKDIV a);
        protected IValueRegisterField cal_cmpsel_field;
        partial void Cal_Cmpsel_Write(ulong a, ulong b);
        partial void Cal_Cmpsel_Read(ulong a, ulong b);
        partial void Cal_Cmpsel_ValueProvider(ulong a);
        protected IValueRegisterField cal_ireftc_field;
        partial void Cal_Ireftc_Write(ulong a, ulong b);
        partial void Cal_Ireftc_Read(ulong a, ulong b);
        partial void Cal_Ireftc_ValueProvider(ulong a);

        partial void Cal_Write(uint a, uint b);
        partial void Cal_Read(uint a, uint b);
        
        // Status - Offset : 0xC
        protected IFlagRegisterField status_rdy_bit;
        partial void Status_Rdy_Read(bool a, bool b);
        partial void Status_Rdy_ValueProvider(bool a);
        protected IFlagRegisterField status_freqbsy_bit;
        partial void Status_Freqbsy_Read(bool a, bool b);
        partial void Status_Freqbsy_ValueProvider(bool a);
        protected IFlagRegisterField status_syncbusy_bit;
        partial void Status_Syncbusy_Read(bool a, bool b);
        partial void Status_Syncbusy_ValueProvider(bool a);
        protected IFlagRegisterField status_ens_bit;
        partial void Status_Ens_Read(bool a, bool b);
        partial void Status_Ens_ValueProvider(bool a);
        protected IEnumRegisterField<STATUS_LOCK> status_lock_bit;
        partial void Status_Lock_Read(STATUS_LOCK a, STATUS_LOCK b);
        partial void Status_Lock_ValueProvider(STATUS_LOCK a);

        partial void Status_Write(uint a, uint b);
        partial void Status_Read(uint a, uint b);
        
        // If - Offset : 0x10
        protected IFlagRegisterField if_rdy_bit;
        partial void If_Rdy_Write(bool a, bool b);
        partial void If_Rdy_Read(bool a, bool b);
        partial void If_Rdy_ValueProvider(bool a);

        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x14
        protected IFlagRegisterField ien_rdy_bit;
        partial void Ien_Rdy_Write(bool a, bool b);
        partial void Ien_Rdy_Read(bool a, bool b);
        partial void Ien_Rdy_ValueProvider(bool a);

        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Lock - Offset : 0x1C
        protected IValueRegisterField lock_lockkey_field;
        partial void Lock_Lockkey_Write(ulong a, ulong b);
        partial void Lock_Lockkey_ValueProvider(ulong a);

        partial void Lock_Write(uint a, uint b);
        partial void Lock_Read(uint a, uint b);
        
        // Test - Offset : 0x20
        protected IFlagRegisterField test_inv_bit;
        partial void Test_Inv_Write(bool a, bool b);
        partial void Test_Inv_Read(bool a, bool b);
        partial void Test_Inv_ValueProvider(bool a);
        protected IFlagRegisterField test_maxtoen_bit;
        partial void Test_Maxtoen_Write(bool a, bool b);
        partial void Test_Maxtoen_Read(bool a, bool b);
        partial void Test_Maxtoen_ValueProvider(bool a);
        protected IFlagRegisterField test_clkoutdis0_bit;
        partial void Test_Clkoutdis0_Write(bool a, bool b);
        partial void Test_Clkoutdis0_Read(bool a, bool b);
        partial void Test_Clkoutdis0_ValueProvider(bool a);
        protected IFlagRegisterField test_clkoutdis1_bit;
        partial void Test_Clkoutdis1_Write(bool a, bool b);
        partial void Test_Clkoutdis1_Read(bool a, bool b);
        partial void Test_Clkoutdis1_ValueProvider(bool a);

        partial void Test_Write(uint a, uint b);
        partial void Test_Read(uint a, uint b);
        
        // Feature - Offset : 0x24
        protected IFlagRegisterField feature_force40_bit;
        partial void Feature_Force40_Write(bool a, bool b);
        partial void Feature_Force40_Read(bool a, bool b);
        partial void Feature_Force40_ValueProvider(bool a);

        partial void Feature_Write(uint a, uint b);
        partial void Feature_Read(uint a, uint b);
        
        partial void HFRCO_Reset();

        partial void EFR32xG2_HFRCO_2_Constructor();

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
            Cal = 0x8,
            Status = 0xC,
            If = 0x10,
            Ien = 0x14,
            Lock = 0x1C,
            Test = 0x20,
            Feature = 0x24,
            
            Ipversion_SET = 0x1000,
            Ctrl_SET = 0x1004,
            Cal_SET = 0x1008,
            Status_SET = 0x100C,
            If_SET = 0x1010,
            Ien_SET = 0x1014,
            Lock_SET = 0x101C,
            Test_SET = 0x1020,
            Feature_SET = 0x1024,
            
            Ipversion_CLR = 0x2000,
            Ctrl_CLR = 0x2004,
            Cal_CLR = 0x2008,
            Status_CLR = 0x200C,
            If_CLR = 0x2010,
            Ien_CLR = 0x2014,
            Lock_CLR = 0x201C,
            Test_CLR = 0x2020,
            Feature_CLR = 0x2024,
            
            Ipversion_TGL = 0x3000,
            Ctrl_TGL = 0x3004,
            Cal_TGL = 0x3008,
            Status_TGL = 0x300C,
            If_TGL = 0x3010,
            Ien_TGL = 0x3014,
            Lock_TGL = 0x301C,
            Test_TGL = 0x3020,
            Feature_TGL = 0x3024,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}