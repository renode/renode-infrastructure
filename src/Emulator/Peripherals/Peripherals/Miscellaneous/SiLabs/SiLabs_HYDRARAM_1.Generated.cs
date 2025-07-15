//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

/*  WARNING: Auto-Generated Peripheral  -  DO NOT EDIT
    HYDRARAM, Generated on : 2024-07-24 09:16:24.292941
    HYDRARAM, ID Version : 08a3945aeae8415a8ca9207162bc899d.1 */

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
    public partial class SiLabs_HYDRARAM_1
    {
        public SiLabs_HYDRARAM_1(Machine machine) : base(machine)
        {
            SiLabs_HYDRARAM_1_constructor();
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
    public partial class SiLabs_HYDRARAM_1 : BasicDoubleWordPeripheral, IKnownSize
    {
        public SiLabs_HYDRARAM_1(Machine machine) : base(machine)
        {
            Define_Registers();
            SiLabs_HYDRARAM_1_Constructor();
        }

        private void Define_Registers()
        {
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Ipversion, GenerateIpversionRegister()},
                {(long)Registers.Cmd, GenerateCmdRegister()},
                {(long)Registers.Ctrl, GenerateCtrlRegister()},
                {(long)Registers.Portpriority, GeneratePortpriorityRegister()},
                {(long)Registers.Interleave2bank, GenerateInterleave2bankRegister()},
                {(long)Registers.Interleave4bank, GenerateInterleave4bankRegister()},
                {(long)Registers.Interleave8bank, GenerateInterleave8bankRegister()},
                {(long)Registers.Eccerraddr, GenerateEccerraddrRegister()},
                {(long)Registers.Eccmerrind, GenerateEccmerrindRegister()},
                {(long)Registers.If, GenerateIfRegister()},
                {(long)Registers.Ien, GenerateIenRegister()},
                {(long)Registers.Bankscacheen, GenerateBankscacheenRegister()},
                {(long)Registers.Retnctrl, GenerateRetnctrlRegister()},
                {(long)Registers.Banksvalid, GenerateBanksvalidRegister()},
                {(long)Registers.Drpu_Rpuratd0, GenerateDrpu_rpuratd0Register()},
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public override void Reset()
        {
            base.Reset();
            HYDRARAM_Reset();
        }
        
        protected enum BANKSVALID_BANKSVALID
        {
            BANK0 = 1, // Enable BANK0 (and lower banks)
            BANK1 = 3, // Enable BANK1 (and lower banks)
            BANK2 = 7, // Enable BANK2 (and lower banks)
            BANK3 = 15, // Enable BANK3 (and lower banks)
            BANK4 = 31, // Enable BANK4 (and lower banks)
            BANK5 = 63, // Enable BANK5 (and lower banks)
            BANK6 = 127, // Enable BANK6 (and lower banks)
            BANK7 = 255, // Enable BANK7 (and lower banks)
            BANK8 = 511, // Enable BANK8 (and lower banks)
            BANK9 = 1023, // Enable BANK9 (and lower banks)
            BANK10 = 2047, // Enable BANK10 (and lower banks)
            BANK11 = 4095, // Enable BANK11 (and lower banks)
            BANK12 = 8191, // Enable BANK12 (and lower banks)
            BANK13 = 16383, // Enable BANK13 (and lower banks)
            BANK14 = 32767, // Enable BANK14 (and lower banks)
            BANK15 = 65535, // Enable BANK15 (and lower banks)
            BANK16 = 131071, // Enable BANK16 (and lower banks)
            BANK17 = 262143, // Enable BANK17 (and lower banks)
            BANK18 = 524287, // Enable BANK18 (and lower banks)
            BANK19 = 1048575, // Enable BANK19 (and lower banks)
            BANK20 = 2097151, // Enable BANK20 (and lower banks)
            BANK21 = 4194303, // Enable BANK21 (and lower banks)
            BANK22 = 8388607, // Enable BANK22 (and lower banks)
            BANK23 = 16777215, // Enable BANK23 (and lower banks)
            BANK24 = 33554431, // Enable BANK24 (and lower banks)
            BANK25 = 67108863, // Enable BANK25 (and lower banks)
            BANK26 = 134217727, // Enable BANK26 (and lower banks)
            BANK27 = 268435455, // Enable BANK027(and lower banks)
            BANK28 = 536870911, // Enable BANK28 (and lower banks)
            BANK29 = 1073741823, // Enable BANK29 (and lower banks)
            BANK30 = 2147483647, // Enable BANK30 (and lower banks)
            //BANK31 = 4294967295, // Enable BANK31 (and lower banks)
        }
        
        // Ipversion - Offset : 0x0
        protected DoubleWordRegister  GenerateIpversionRegister() => new DoubleWordRegister(this, 0x1)
            
            .WithValueField(0, 2, out ipversion_ipversion_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Ipversion_Ipversion_ValueProvider(_);
                        return ipversion_ipversion_field.Value;
                    },
                    
                    readCallback: (_, __) => Ipversion_Ipversion_Read(_, __),
                    name: "Ipversion")
            .WithReservedBits(2, 30)
            .WithReadCallback((_, __) => Ipversion_Read(_, __))
            .WithWriteCallback((_, __) => Ipversion_Write(_, __));
        
        // Cmd - Offset : 0x4
        protected DoubleWordRegister  GenerateCmdRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out cmd_clearecc_bit, FieldMode.Write,
                    
                    writeCallback: (_, __) => Cmd_Clearecc_Write(_, __),
                    name: "Clearecc")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Cmd_Read(_, __))
            .WithWriteCallback((_, __) => Cmd_Write(_, __));
        
        // Ctrl - Offset : 0x8
        protected DoubleWordRegister  GenerateCtrlRegister() => new DoubleWordRegister(this, 0x8)
            .WithFlag(0, out ctrl_eccen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Eccen_ValueProvider(_);
                        return ctrl_eccen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Eccen_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Eccen_Read(_, __),
                    name: "Eccen")
            .WithFlag(1, out ctrl_eccwen_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Eccwen_ValueProvider(_);
                        return ctrl_eccwen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Eccwen_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Eccwen_Read(_, __),
                    name: "Eccwen")
            .WithFlag(2, out ctrl_eccerrfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Eccerrfaulten_ValueProvider(_);
                        return ctrl_eccerrfaulten_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Eccerrfaulten_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Eccerrfaulten_Read(_, __),
                    name: "Eccerrfaulten")
            .WithFlag(3, out ctrl_addrfaulten_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Addrfaulten_ValueProvider(_);
                        return ctrl_addrfaulten_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Addrfaulten_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Addrfaulten_Read(_, __),
                    name: "Addrfaulten")
            .WithFlag(4, out ctrl_waitstatesread_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Waitstatesread_ValueProvider(_);
                        return ctrl_waitstatesread_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Waitstatesread_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Waitstatesread_Read(_, __),
                    name: "Waitstatesread")
            .WithFlag(5, out ctrl_waitstatesctrl_bit, 
                    valueProviderCallback: (_) => {
                        Ctrl_Waitstatesctrl_ValueProvider(_);
                        return ctrl_waitstatesctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ctrl_Waitstatesctrl_Write(_, __),
                    
                    readCallback: (_, __) => Ctrl_Waitstatesctrl_Read(_, __),
                    name: "Waitstatesctrl")
            .WithReservedBits(6, 26)
            .WithReadCallback((_, __) => Ctrl_Read(_, __))
            .WithWriteCallback((_, __) => Ctrl_Write(_, __));
        
        // Portpriority - Offset : 0xC
        protected DoubleWordRegister  GeneratePortpriorityRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out portpriority_portpriority0_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority0_ValueProvider(_);
                        return portpriority_portpriority0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority0_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority0_Read(_, __),
                    name: "Portpriority0")
            .WithReservedBits(1, 1)
            .WithFlag(2, out portpriority_portpriority1_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority1_ValueProvider(_);
                        return portpriority_portpriority1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority1_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority1_Read(_, __),
                    name: "Portpriority1")
            .WithReservedBits(3, 1)
            .WithFlag(4, out portpriority_portpriority2_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority2_ValueProvider(_);
                        return portpriority_portpriority2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority2_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority2_Read(_, __),
                    name: "Portpriority2")
            .WithReservedBits(5, 1)
            .WithFlag(6, out portpriority_portpriority3_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority3_ValueProvider(_);
                        return portpriority_portpriority3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority3_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority3_Read(_, __),
                    name: "Portpriority3")
            .WithReservedBits(7, 1)
            .WithFlag(8, out portpriority_portpriority4_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority4_ValueProvider(_);
                        return portpriority_portpriority4_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority4_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority4_Read(_, __),
                    name: "Portpriority4")
            .WithReservedBits(9, 1)
            .WithFlag(10, out portpriority_portpriority5_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority5_ValueProvider(_);
                        return portpriority_portpriority5_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority5_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority5_Read(_, __),
                    name: "Portpriority5")
            .WithReservedBits(11, 1)
            .WithFlag(12, out portpriority_portpriority6_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority6_ValueProvider(_);
                        return portpriority_portpriority6_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority6_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority6_Read(_, __),
                    name: "Portpriority6")
            .WithReservedBits(13, 1)
            .WithFlag(14, out portpriority_portpriority7_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority7_ValueProvider(_);
                        return portpriority_portpriority7_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority7_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority7_Read(_, __),
                    name: "Portpriority7")
            .WithReservedBits(15, 1)
            .WithFlag(16, out portpriority_portpriority8_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority8_ValueProvider(_);
                        return portpriority_portpriority8_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority8_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority8_Read(_, __),
                    name: "Portpriority8")
            .WithReservedBits(17, 1)
            .WithFlag(18, out portpriority_portpriority9_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority9_ValueProvider(_);
                        return portpriority_portpriority9_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority9_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority9_Read(_, __),
                    name: "Portpriority9")
            .WithReservedBits(19, 1)
            .WithFlag(20, out portpriority_portpriority10_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority10_ValueProvider(_);
                        return portpriority_portpriority10_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority10_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority10_Read(_, __),
                    name: "Portpriority10")
            .WithReservedBits(21, 1)
            .WithFlag(22, out portpriority_portpriority11_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority11_ValueProvider(_);
                        return portpriority_portpriority11_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority11_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority11_Read(_, __),
                    name: "Portpriority11")
            .WithReservedBits(23, 1)
            .WithFlag(24, out portpriority_portpriority12_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority12_ValueProvider(_);
                        return portpriority_portpriority12_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority12_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority12_Read(_, __),
                    name: "Portpriority12")
            .WithReservedBits(25, 1)
            .WithFlag(26, out portpriority_portpriority13_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority13_ValueProvider(_);
                        return portpriority_portpriority13_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority13_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority13_Read(_, __),
                    name: "Portpriority13")
            .WithReservedBits(27, 1)
            .WithFlag(28, out portpriority_portpriority14_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority14_ValueProvider(_);
                        return portpriority_portpriority14_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority14_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority14_Read(_, __),
                    name: "Portpriority14")
            .WithReservedBits(29, 1)
            .WithFlag(30, out portpriority_portpriority15_bit, 
                    valueProviderCallback: (_) => {
                        Portpriority_Portpriority15_ValueProvider(_);
                        return portpriority_portpriority15_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Portpriority_Portpriority15_Write(_, __),
                    
                    readCallback: (_, __) => Portpriority_Portpriority15_Read(_, __),
                    name: "Portpriority15")
            .WithReservedBits(31, 1)
            .WithReadCallback((_, __) => Portpriority_Read(_, __))
            .WithWriteCallback((_, __) => Portpriority_Write(_, __));
        
        // Interleave2bank - Offset : 0x10
        protected DoubleWordRegister  GenerateInterleave2bankRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out interleave2bank_intl2bankbit0_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit0_ValueProvider(_);
                        return interleave2bank_intl2bankbit0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit0_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit0_Read(_, __),
                    name: "Intl2bankbit0")
            .WithFlag(1, out interleave2bank_intl2bankbit1_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit1_ValueProvider(_);
                        return interleave2bank_intl2bankbit1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit1_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit1_Read(_, __),
                    name: "Intl2bankbit1")
            .WithFlag(2, out interleave2bank_intl2bankbit2_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit2_ValueProvider(_);
                        return interleave2bank_intl2bankbit2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit2_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit2_Read(_, __),
                    name: "Intl2bankbit2")
            .WithFlag(3, out interleave2bank_intl2bankbit3_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit3_ValueProvider(_);
                        return interleave2bank_intl2bankbit3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit3_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit3_Read(_, __),
                    name: "Intl2bankbit3")
            .WithFlag(4, out interleave2bank_intl2bankbit4_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit4_ValueProvider(_);
                        return interleave2bank_intl2bankbit4_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit4_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit4_Read(_, __),
                    name: "Intl2bankbit4")
            .WithFlag(5, out interleave2bank_intl2bankbit5_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit5_ValueProvider(_);
                        return interleave2bank_intl2bankbit5_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit5_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit5_Read(_, __),
                    name: "Intl2bankbit5")
            .WithFlag(6, out interleave2bank_intl2bankbit6_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit6_ValueProvider(_);
                        return interleave2bank_intl2bankbit6_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit6_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit6_Read(_, __),
                    name: "Intl2bankbit6")
            .WithFlag(7, out interleave2bank_intl2bankbit7_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit7_ValueProvider(_);
                        return interleave2bank_intl2bankbit7_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit7_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit7_Read(_, __),
                    name: "Intl2bankbit7")
            .WithFlag(8, out interleave2bank_intl2bankbit8_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit8_ValueProvider(_);
                        return interleave2bank_intl2bankbit8_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit8_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit8_Read(_, __),
                    name: "Intl2bankbit8")
            .WithFlag(9, out interleave2bank_intl2bankbit9_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit9_ValueProvider(_);
                        return interleave2bank_intl2bankbit9_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit9_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit9_Read(_, __),
                    name: "Intl2bankbit9")
            .WithFlag(10, out interleave2bank_intl2bankbit10_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit10_ValueProvider(_);
                        return interleave2bank_intl2bankbit10_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit10_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit10_Read(_, __),
                    name: "Intl2bankbit10")
            .WithFlag(11, out interleave2bank_intl2bankbit11_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit11_ValueProvider(_);
                        return interleave2bank_intl2bankbit11_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit11_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit11_Read(_, __),
                    name: "Intl2bankbit11")
            .WithFlag(12, out interleave2bank_intl2bankbit12_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit12_ValueProvider(_);
                        return interleave2bank_intl2bankbit12_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit12_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit12_Read(_, __),
                    name: "Intl2bankbit12")
            .WithFlag(13, out interleave2bank_intl2bankbit13_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit13_ValueProvider(_);
                        return interleave2bank_intl2bankbit13_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit13_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit13_Read(_, __),
                    name: "Intl2bankbit13")
            .WithFlag(14, out interleave2bank_intl2bankbit14_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit14_ValueProvider(_);
                        return interleave2bank_intl2bankbit14_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit14_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit14_Read(_, __),
                    name: "Intl2bankbit14")
            .WithFlag(15, out interleave2bank_intl2bankbit15_bit, 
                    valueProviderCallback: (_) => {
                        Interleave2bank_Intl2bankbit15_ValueProvider(_);
                        return interleave2bank_intl2bankbit15_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave2bank_Intl2bankbit15_Write(_, __),
                    
                    readCallback: (_, __) => Interleave2bank_Intl2bankbit15_Read(_, __),
                    name: "Intl2bankbit15")
            .WithReservedBits(16, 16)
            .WithReadCallback((_, __) => Interleave2bank_Read(_, __))
            .WithWriteCallback((_, __) => Interleave2bank_Write(_, __));
        
        // Interleave4bank - Offset : 0x14
        protected DoubleWordRegister  GenerateInterleave4bankRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out interleave4bank_intl4bankbit0_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit0_ValueProvider(_);
                        return interleave4bank_intl4bankbit0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit0_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit0_Read(_, __),
                    name: "Intl4bankbit0")
            .WithFlag(1, out interleave4bank_intl4bankbit1_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit1_ValueProvider(_);
                        return interleave4bank_intl4bankbit1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit1_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit1_Read(_, __),
                    name: "Intl4bankbit1")
            .WithFlag(2, out interleave4bank_intl4bankbit2_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit2_ValueProvider(_);
                        return interleave4bank_intl4bankbit2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit2_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit2_Read(_, __),
                    name: "Intl4bankbit2")
            .WithFlag(3, out interleave4bank_intl4bankbit3_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit3_ValueProvider(_);
                        return interleave4bank_intl4bankbit3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit3_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit3_Read(_, __),
                    name: "Intl4bankbit3")
            .WithFlag(4, out interleave4bank_intl4bankbit4_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit4_ValueProvider(_);
                        return interleave4bank_intl4bankbit4_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit4_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit4_Read(_, __),
                    name: "Intl4bankbit4")
            .WithFlag(5, out interleave4bank_intl4bankbit5_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit5_ValueProvider(_);
                        return interleave4bank_intl4bankbit5_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit5_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit5_Read(_, __),
                    name: "Intl4bankbit5")
            .WithFlag(6, out interleave4bank_intl4bankbit6_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit6_ValueProvider(_);
                        return interleave4bank_intl4bankbit6_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit6_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit6_Read(_, __),
                    name: "Intl4bankbit6")
            .WithFlag(7, out interleave4bank_intl4bankbit7_bit, 
                    valueProviderCallback: (_) => {
                        Interleave4bank_Intl4bankbit7_ValueProvider(_);
                        return interleave4bank_intl4bankbit7_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave4bank_Intl4bankbit7_Write(_, __),
                    
                    readCallback: (_, __) => Interleave4bank_Intl4bankbit7_Read(_, __),
                    name: "Intl4bankbit7")
            .WithReservedBits(8, 24)
            .WithReadCallback((_, __) => Interleave4bank_Read(_, __))
            .WithWriteCallback((_, __) => Interleave4bank_Write(_, __));
        
        // Interleave8bank - Offset : 0x18
        protected DoubleWordRegister  GenerateInterleave8bankRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out interleave8bank_intl8bankbit0_bit, 
                    valueProviderCallback: (_) => {
                        Interleave8bank_Intl8bankbit0_ValueProvider(_);
                        return interleave8bank_intl8bankbit0_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave8bank_Intl8bankbit0_Write(_, __),
                    
                    readCallback: (_, __) => Interleave8bank_Intl8bankbit0_Read(_, __),
                    name: "Intl8bankbit0")
            .WithFlag(1, out interleave8bank_intl8bankbit1_bit, 
                    valueProviderCallback: (_) => {
                        Interleave8bank_Intl8bankbit1_ValueProvider(_);
                        return interleave8bank_intl8bankbit1_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave8bank_Intl8bankbit1_Write(_, __),
                    
                    readCallback: (_, __) => Interleave8bank_Intl8bankbit1_Read(_, __),
                    name: "Intl8bankbit1")
            .WithFlag(2, out interleave8bank_intl8bankbit2_bit, 
                    valueProviderCallback: (_) => {
                        Interleave8bank_Intl8bankbit2_ValueProvider(_);
                        return interleave8bank_intl8bankbit2_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave8bank_Intl8bankbit2_Write(_, __),
                    
                    readCallback: (_, __) => Interleave8bank_Intl8bankbit2_Read(_, __),
                    name: "Intl8bankbit2")
            .WithFlag(3, out interleave8bank_intl8bankbit3_bit, 
                    valueProviderCallback: (_) => {
                        Interleave8bank_Intl8bankbit3_ValueProvider(_);
                        return interleave8bank_intl8bankbit3_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Interleave8bank_Intl8bankbit3_Write(_, __),
                    
                    readCallback: (_, __) => Interleave8bank_Intl8bankbit3_Read(_, __),
                    name: "Intl8bankbit3")
            .WithReservedBits(4, 28)
            .WithReadCallback((_, __) => Interleave8bank_Read(_, __))
            .WithWriteCallback((_, __) => Interleave8bank_Write(_, __));
        
        // Eccerraddr - Offset : 0x1C
        protected DoubleWordRegister  GenerateEccerraddrRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out eccerraddr_addr_field, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Eccerraddr_Addr_ValueProvider(_);
                        return eccerraddr_addr_field.Value;
                    },
                    
                    readCallback: (_, __) => Eccerraddr_Addr_Read(_, __),
                    name: "Addr")
            .WithReadCallback((_, __) => Eccerraddr_Read(_, __))
            .WithWriteCallback((_, __) => Eccerraddr_Write(_, __));
        
        // Eccmerrind - Offset : 0x20
        protected DoubleWordRegister  GenerateEccmerrindRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out eccmerrind_merrind_bit, FieldMode.Read,
                    valueProviderCallback: (_) => {
                        Eccmerrind_Merrind_ValueProvider(_);
                        return eccmerrind_merrind_bit.Value;
                    },
                    
                    readCallback: (_, __) => Eccmerrind_Merrind_Read(_, __),
                    name: "Merrind")
            .WithReservedBits(1, 31)
            .WithReadCallback((_, __) => Eccmerrind_Read(_, __))
            .WithWriteCallback((_, __) => Eccmerrind_Write(_, __));
        
        // If - Offset : 0x24
        protected DoubleWordRegister  GenerateIfRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out if_err1b_bit, 
                    valueProviderCallback: (_) => {
                        If_Err1b_ValueProvider(_);
                        return if_err1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Err1b_Write(_, __),
                    
                    readCallback: (_, __) => If_Err1b_Read(_, __),
                    name: "Err1b")
            .WithFlag(1, out if_err2b_bit, 
                    valueProviderCallback: (_) => {
                        If_Err2b_ValueProvider(_);
                        return if_err2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Err2b_Write(_, __),
                    
                    readCallback: (_, __) => If_Err2b_Read(_, __),
                    name: "Err2b")
            .WithFlag(2, out if_integrityerr_bit, 
                    valueProviderCallback: (_) => {
                        If_Integrityerr_ValueProvider(_);
                        return if_integrityerr_bit.Value;
                    },
                    
                    writeCallback: (_, __) => If_Integrityerr_Write(_, __),
                    
                    readCallback: (_, __) => If_Integrityerr_Read(_, __),
                    name: "Integrityerr")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => If_Read(_, __))
            .WithWriteCallback((_, __) => If_Write(_, __));
        
        // Ien - Offset : 0x28
        protected DoubleWordRegister  GenerateIenRegister() => new DoubleWordRegister(this, 0x0)
            .WithFlag(0, out ien_err1b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Err1b_ValueProvider(_);
                        return ien_err1b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Err1b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Err1b_Read(_, __),
                    name: "Err1b")
            .WithFlag(1, out ien_err2b_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Err2b_ValueProvider(_);
                        return ien_err2b_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Err2b_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Err2b_Read(_, __),
                    name: "Err2b")
            .WithFlag(2, out ien_integrityerr_bit, 
                    valueProviderCallback: (_) => {
                        Ien_Integrityerr_ValueProvider(_);
                        return ien_integrityerr_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Ien_Integrityerr_Write(_, __),
                    
                    readCallback: (_, __) => Ien_Integrityerr_Read(_, __),
                    name: "Integrityerr")
            .WithReservedBits(3, 29)
            .WithReadCallback((_, __) => Ien_Read(_, __))
            .WithWriteCallback((_, __) => Ien_Write(_, __));
        
        // Bankscacheen - Offset : 0x2C
        protected DoubleWordRegister  GenerateBankscacheenRegister() => new DoubleWordRegister(this, 0x0)
            
            .WithValueField(0, 32, out bankscacheen_bankscacheen_field, 
                    valueProviderCallback: (_) => {
                        Bankscacheen_Bankscacheen_ValueProvider(_);
                        return bankscacheen_bankscacheen_field.Value;
                    },
                    
                    writeCallback: (_, __) => Bankscacheen_Bankscacheen_Write(_, __),
                    
                    readCallback: (_, __) => Bankscacheen_Bankscacheen_Read(_, __),
                    name: "Bankscacheen")
            .WithReadCallback((_, __) => Bankscacheen_Read(_, __))
            .WithWriteCallback((_, __) => Bankscacheen_Write(_, __));
        
        // Retnctrl - Offset : 0x30
        protected DoubleWordRegister  GenerateRetnctrlRegister() => new DoubleWordRegister(this, 0xFFFFFFFF)
            
            .WithValueField(0, 32, out retnctrl_retnctrl_field, 
                    valueProviderCallback: (_) => {
                        Retnctrl_Retnctrl_ValueProvider(_);
                        return retnctrl_retnctrl_field.Value;
                    },
                    
                    writeCallback: (_, __) => Retnctrl_Retnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Retnctrl_Retnctrl_Read(_, __),
                    name: "Retnctrl")
            .WithReadCallback((_, __) => Retnctrl_Read(_, __))
            .WithWriteCallback((_, __) => Retnctrl_Write(_, __));
        
        // Banksvalid - Offset : 0x34
        protected DoubleWordRegister  GenerateBanksvalidRegister() => new DoubleWordRegister(this, 0xFFFFFFFF)
            .WithEnumField<DoubleWordRegister, BANKSVALID_BANKSVALID>(0, 32, out banksvalid_banksvalid_field, 
                    valueProviderCallback: (_) => {
                        Banksvalid_Banksvalid_ValueProvider(_);
                        return banksvalid_banksvalid_field.Value;
                    },
                    
                    writeCallback: (_, __) => Banksvalid_Banksvalid_Write(_, __),
                    
                    readCallback: (_, __) => Banksvalid_Banksvalid_Read(_, __),
                    name: "Banksvalid")
            .WithReadCallback((_, __) => Banksvalid_Read(_, __))
            .WithWriteCallback((_, __) => Banksvalid_Write(_, __));
        
        // Drpu_Rpuratd0 - Offset : 0x38
        protected DoubleWordRegister  GenerateDrpu_rpuratd0Register() => new DoubleWordRegister(this, 0x0)
            .WithReservedBits(0, 1)
            .WithFlag(1, out drpu_rpuratd0_ratdcmd_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdcmd_ValueProvider(_);
                        return drpu_rpuratd0_ratdcmd_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdcmd_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdcmd_Read(_, __),
                    name: "Ratdcmd")
            .WithFlag(2, out drpu_rpuratd0_ratdctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdctrl_Read(_, __),
                    name: "Ratdctrl")
            .WithFlag(3, out drpu_rpuratd0_ratdportpriority_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdportpriority_ValueProvider(_);
                        return drpu_rpuratd0_ratdportpriority_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdportpriority_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdportpriority_Read(_, __),
                    name: "Ratdportpriority")
            .WithFlag(4, out drpu_rpuratd0_ratdinterleave2bank_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdinterleave2bank_ValueProvider(_);
                        return drpu_rpuratd0_ratdinterleave2bank_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdinterleave2bank_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdinterleave2bank_Read(_, __),
                    name: "Ratdinterleave2bank")
            .WithFlag(5, out drpu_rpuratd0_ratdinterleave4bank_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdinterleave4bank_ValueProvider(_);
                        return drpu_rpuratd0_ratdinterleave4bank_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdinterleave4bank_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdinterleave4bank_Read(_, __),
                    name: "Ratdinterleave4bank")
            .WithFlag(6, out drpu_rpuratd0_ratdinterleave8bank_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdinterleave8bank_ValueProvider(_);
                        return drpu_rpuratd0_ratdinterleave8bank_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdinterleave8bank_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdinterleave8bank_Read(_, __),
                    name: "Ratdinterleave8bank")
            .WithReservedBits(7, 2)
            .WithFlag(9, out drpu_rpuratd0_ratdif_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdif_ValueProvider(_);
                        return drpu_rpuratd0_ratdif_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdif_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdif_Read(_, __),
                    name: "Ratdif")
            .WithFlag(10, out drpu_rpuratd0_ratdien_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdien_ValueProvider(_);
                        return drpu_rpuratd0_ratdien_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdien_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdien_Read(_, __),
                    name: "Ratdien")
            .WithFlag(11, out drpu_rpuratd0_ratdbankscacheen_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdbankscacheen_ValueProvider(_);
                        return drpu_rpuratd0_ratdbankscacheen_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdbankscacheen_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdbankscacheen_Read(_, __),
                    name: "Ratdbankscacheen")
            .WithFlag(12, out drpu_rpuratd0_ratdretnctrl_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdretnctrl_ValueProvider(_);
                        return drpu_rpuratd0_ratdretnctrl_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdretnctrl_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdretnctrl_Read(_, __),
                    name: "Ratdretnctrl")
            .WithFlag(13, out drpu_rpuratd0_ratdbanksvalid_bit, 
                    valueProviderCallback: (_) => {
                        Drpu_Rpuratd0_Ratdbanksvalid_ValueProvider(_);
                        return drpu_rpuratd0_ratdbanksvalid_bit.Value;
                    },
                    
                    writeCallback: (_, __) => Drpu_Rpuratd0_Ratdbanksvalid_Write(_, __),
                    
                    readCallback: (_, __) => Drpu_Rpuratd0_Ratdbanksvalid_Read(_, __),
                    name: "Ratdbanksvalid")
            .WithReservedBits(14, 18)
            .WithReadCallback((_, __) => Drpu_Rpuratd0_Read(_, __))
            .WithWriteCallback((_, __) => Drpu_Rpuratd0_Write(_, __));
        

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
        
        // Cmd - Offset : 0x4
    
        protected IFlagRegisterField cmd_clearecc_bit;
        partial void Cmd_Clearecc_Write(bool a, bool b);
        partial void Cmd_Clearecc_ValueProvider(bool a);
        partial void Cmd_Write(uint a, uint b);
        partial void Cmd_Read(uint a, uint b);
        
        // Ctrl - Offset : 0x8
    
        protected IFlagRegisterField ctrl_eccen_bit;
        partial void Ctrl_Eccen_Write(bool a, bool b);
        partial void Ctrl_Eccen_Read(bool a, bool b);
        partial void Ctrl_Eccen_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_eccwen_bit;
        partial void Ctrl_Eccwen_Write(bool a, bool b);
        partial void Ctrl_Eccwen_Read(bool a, bool b);
        partial void Ctrl_Eccwen_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_eccerrfaulten_bit;
        partial void Ctrl_Eccerrfaulten_Write(bool a, bool b);
        partial void Ctrl_Eccerrfaulten_Read(bool a, bool b);
        partial void Ctrl_Eccerrfaulten_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_addrfaulten_bit;
        partial void Ctrl_Addrfaulten_Write(bool a, bool b);
        partial void Ctrl_Addrfaulten_Read(bool a, bool b);
        partial void Ctrl_Addrfaulten_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_waitstatesread_bit;
        partial void Ctrl_Waitstatesread_Write(bool a, bool b);
        partial void Ctrl_Waitstatesread_Read(bool a, bool b);
        partial void Ctrl_Waitstatesread_ValueProvider(bool a);
    
        protected IFlagRegisterField ctrl_waitstatesctrl_bit;
        partial void Ctrl_Waitstatesctrl_Write(bool a, bool b);
        partial void Ctrl_Waitstatesctrl_Read(bool a, bool b);
        partial void Ctrl_Waitstatesctrl_ValueProvider(bool a);
        partial void Ctrl_Write(uint a, uint b);
        partial void Ctrl_Read(uint a, uint b);
        
        // Portpriority - Offset : 0xC
    
        protected IFlagRegisterField portpriority_portpriority0_bit;
        partial void Portpriority_Portpriority0_Write(bool a, bool b);
        partial void Portpriority_Portpriority0_Read(bool a, bool b);
        partial void Portpriority_Portpriority0_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority1_bit;
        partial void Portpriority_Portpriority1_Write(bool a, bool b);
        partial void Portpriority_Portpriority1_Read(bool a, bool b);
        partial void Portpriority_Portpriority1_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority2_bit;
        partial void Portpriority_Portpriority2_Write(bool a, bool b);
        partial void Portpriority_Portpriority2_Read(bool a, bool b);
        partial void Portpriority_Portpriority2_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority3_bit;
        partial void Portpriority_Portpriority3_Write(bool a, bool b);
        partial void Portpriority_Portpriority3_Read(bool a, bool b);
        partial void Portpriority_Portpriority3_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority4_bit;
        partial void Portpriority_Portpriority4_Write(bool a, bool b);
        partial void Portpriority_Portpriority4_Read(bool a, bool b);
        partial void Portpriority_Portpriority4_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority5_bit;
        partial void Portpriority_Portpriority5_Write(bool a, bool b);
        partial void Portpriority_Portpriority5_Read(bool a, bool b);
        partial void Portpriority_Portpriority5_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority6_bit;
        partial void Portpriority_Portpriority6_Write(bool a, bool b);
        partial void Portpriority_Portpriority6_Read(bool a, bool b);
        partial void Portpriority_Portpriority6_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority7_bit;
        partial void Portpriority_Portpriority7_Write(bool a, bool b);
        partial void Portpriority_Portpriority7_Read(bool a, bool b);
        partial void Portpriority_Portpriority7_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority8_bit;
        partial void Portpriority_Portpriority8_Write(bool a, bool b);
        partial void Portpriority_Portpriority8_Read(bool a, bool b);
        partial void Portpriority_Portpriority8_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority9_bit;
        partial void Portpriority_Portpriority9_Write(bool a, bool b);
        partial void Portpriority_Portpriority9_Read(bool a, bool b);
        partial void Portpriority_Portpriority9_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority10_bit;
        partial void Portpriority_Portpriority10_Write(bool a, bool b);
        partial void Portpriority_Portpriority10_Read(bool a, bool b);
        partial void Portpriority_Portpriority10_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority11_bit;
        partial void Portpriority_Portpriority11_Write(bool a, bool b);
        partial void Portpriority_Portpriority11_Read(bool a, bool b);
        partial void Portpriority_Portpriority11_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority12_bit;
        partial void Portpriority_Portpriority12_Write(bool a, bool b);
        partial void Portpriority_Portpriority12_Read(bool a, bool b);
        partial void Portpriority_Portpriority12_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority13_bit;
        partial void Portpriority_Portpriority13_Write(bool a, bool b);
        partial void Portpriority_Portpriority13_Read(bool a, bool b);
        partial void Portpriority_Portpriority13_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority14_bit;
        partial void Portpriority_Portpriority14_Write(bool a, bool b);
        partial void Portpriority_Portpriority14_Read(bool a, bool b);
        partial void Portpriority_Portpriority14_ValueProvider(bool a);
    
        protected IFlagRegisterField portpriority_portpriority15_bit;
        partial void Portpriority_Portpriority15_Write(bool a, bool b);
        partial void Portpriority_Portpriority15_Read(bool a, bool b);
        partial void Portpriority_Portpriority15_ValueProvider(bool a);
        partial void Portpriority_Write(uint a, uint b);
        partial void Portpriority_Read(uint a, uint b);
        
        // Interleave2bank - Offset : 0x10
    
        protected IFlagRegisterField interleave2bank_intl2bankbit0_bit;
        partial void Interleave2bank_Intl2bankbit0_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit0_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit0_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit1_bit;
        partial void Interleave2bank_Intl2bankbit1_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit1_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit1_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit2_bit;
        partial void Interleave2bank_Intl2bankbit2_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit2_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit2_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit3_bit;
        partial void Interleave2bank_Intl2bankbit3_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit3_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit3_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit4_bit;
        partial void Interleave2bank_Intl2bankbit4_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit4_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit4_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit5_bit;
        partial void Interleave2bank_Intl2bankbit5_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit5_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit5_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit6_bit;
        partial void Interleave2bank_Intl2bankbit6_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit6_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit6_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit7_bit;
        partial void Interleave2bank_Intl2bankbit7_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit7_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit7_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit8_bit;
        partial void Interleave2bank_Intl2bankbit8_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit8_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit8_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit9_bit;
        partial void Interleave2bank_Intl2bankbit9_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit9_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit9_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit10_bit;
        partial void Interleave2bank_Intl2bankbit10_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit10_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit10_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit11_bit;
        partial void Interleave2bank_Intl2bankbit11_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit11_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit11_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit12_bit;
        partial void Interleave2bank_Intl2bankbit12_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit12_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit12_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit13_bit;
        partial void Interleave2bank_Intl2bankbit13_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit13_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit13_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit14_bit;
        partial void Interleave2bank_Intl2bankbit14_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit14_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit14_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave2bank_intl2bankbit15_bit;
        partial void Interleave2bank_Intl2bankbit15_Write(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit15_Read(bool a, bool b);
        partial void Interleave2bank_Intl2bankbit15_ValueProvider(bool a);
        partial void Interleave2bank_Write(uint a, uint b);
        partial void Interleave2bank_Read(uint a, uint b);
        
        // Interleave4bank - Offset : 0x14
    
        protected IFlagRegisterField interleave4bank_intl4bankbit0_bit;
        partial void Interleave4bank_Intl4bankbit0_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit0_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit0_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave4bank_intl4bankbit1_bit;
        partial void Interleave4bank_Intl4bankbit1_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit1_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit1_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave4bank_intl4bankbit2_bit;
        partial void Interleave4bank_Intl4bankbit2_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit2_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit2_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave4bank_intl4bankbit3_bit;
        partial void Interleave4bank_Intl4bankbit3_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit3_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit3_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave4bank_intl4bankbit4_bit;
        partial void Interleave4bank_Intl4bankbit4_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit4_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit4_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave4bank_intl4bankbit5_bit;
        partial void Interleave4bank_Intl4bankbit5_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit5_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit5_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave4bank_intl4bankbit6_bit;
        partial void Interleave4bank_Intl4bankbit6_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit6_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit6_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave4bank_intl4bankbit7_bit;
        partial void Interleave4bank_Intl4bankbit7_Write(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit7_Read(bool a, bool b);
        partial void Interleave4bank_Intl4bankbit7_ValueProvider(bool a);
        partial void Interleave4bank_Write(uint a, uint b);
        partial void Interleave4bank_Read(uint a, uint b);
        
        // Interleave8bank - Offset : 0x18
    
        protected IFlagRegisterField interleave8bank_intl8bankbit0_bit;
        partial void Interleave8bank_Intl8bankbit0_Write(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit0_Read(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit0_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave8bank_intl8bankbit1_bit;
        partial void Interleave8bank_Intl8bankbit1_Write(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit1_Read(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit1_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave8bank_intl8bankbit2_bit;
        partial void Interleave8bank_Intl8bankbit2_Write(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit2_Read(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit2_ValueProvider(bool a);
    
        protected IFlagRegisterField interleave8bank_intl8bankbit3_bit;
        partial void Interleave8bank_Intl8bankbit3_Write(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit3_Read(bool a, bool b);
        partial void Interleave8bank_Intl8bankbit3_ValueProvider(bool a);
        partial void Interleave8bank_Write(uint a, uint b);
        partial void Interleave8bank_Read(uint a, uint b);
        
        // Eccerraddr - Offset : 0x1C
    
        protected IValueRegisterField eccerraddr_addr_field;
        partial void Eccerraddr_Addr_Read(ulong a, ulong b);
        partial void Eccerraddr_Addr_ValueProvider(ulong a);
        partial void Eccerraddr_Write(uint a, uint b);
        partial void Eccerraddr_Read(uint a, uint b);
        
        // Eccmerrind - Offset : 0x20
    
        protected IFlagRegisterField eccmerrind_merrind_bit;
        partial void Eccmerrind_Merrind_Read(bool a, bool b);
        partial void Eccmerrind_Merrind_ValueProvider(bool a);
        partial void Eccmerrind_Write(uint a, uint b);
        partial void Eccmerrind_Read(uint a, uint b);
        
        // If - Offset : 0x24
    
        protected IFlagRegisterField if_err1b_bit;
        partial void If_Err1b_Write(bool a, bool b);
        partial void If_Err1b_Read(bool a, bool b);
        partial void If_Err1b_ValueProvider(bool a);
    
        protected IFlagRegisterField if_err2b_bit;
        partial void If_Err2b_Write(bool a, bool b);
        partial void If_Err2b_Read(bool a, bool b);
        partial void If_Err2b_ValueProvider(bool a);
    
        protected IFlagRegisterField if_integrityerr_bit;
        partial void If_Integrityerr_Write(bool a, bool b);
        partial void If_Integrityerr_Read(bool a, bool b);
        partial void If_Integrityerr_ValueProvider(bool a);
        partial void If_Write(uint a, uint b);
        partial void If_Read(uint a, uint b);
        
        // Ien - Offset : 0x28
    
        protected IFlagRegisterField ien_err1b_bit;
        partial void Ien_Err1b_Write(bool a, bool b);
        partial void Ien_Err1b_Read(bool a, bool b);
        partial void Ien_Err1b_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_err2b_bit;
        partial void Ien_Err2b_Write(bool a, bool b);
        partial void Ien_Err2b_Read(bool a, bool b);
        partial void Ien_Err2b_ValueProvider(bool a);
    
        protected IFlagRegisterField ien_integrityerr_bit;
        partial void Ien_Integrityerr_Write(bool a, bool b);
        partial void Ien_Integrityerr_Read(bool a, bool b);
        partial void Ien_Integrityerr_ValueProvider(bool a);
        partial void Ien_Write(uint a, uint b);
        partial void Ien_Read(uint a, uint b);
        
        // Bankscacheen - Offset : 0x2C
    
        protected IValueRegisterField bankscacheen_bankscacheen_field;
        partial void Bankscacheen_Bankscacheen_Write(ulong a, ulong b);
        partial void Bankscacheen_Bankscacheen_Read(ulong a, ulong b);
        partial void Bankscacheen_Bankscacheen_ValueProvider(ulong a);
        partial void Bankscacheen_Write(uint a, uint b);
        partial void Bankscacheen_Read(uint a, uint b);
        
        // Retnctrl - Offset : 0x30
    
        protected IValueRegisterField retnctrl_retnctrl_field;
        partial void Retnctrl_Retnctrl_Write(ulong a, ulong b);
        partial void Retnctrl_Retnctrl_Read(ulong a, ulong b);
        partial void Retnctrl_Retnctrl_ValueProvider(ulong a);
        partial void Retnctrl_Write(uint a, uint b);
        partial void Retnctrl_Read(uint a, uint b);
        
        // Banksvalid - Offset : 0x34
    
        protected IEnumRegisterField<BANKSVALID_BANKSVALID> banksvalid_banksvalid_field;
        partial void Banksvalid_Banksvalid_Write(BANKSVALID_BANKSVALID a, BANKSVALID_BANKSVALID b);
        partial void Banksvalid_Banksvalid_Read(BANKSVALID_BANKSVALID a, BANKSVALID_BANKSVALID b);
        partial void Banksvalid_Banksvalid_ValueProvider(BANKSVALID_BANKSVALID a);
        partial void Banksvalid_Write(uint a, uint b);
        partial void Banksvalid_Read(uint a, uint b);
        
        // Drpu_Rpuratd0 - Offset : 0x38
    
        protected IFlagRegisterField drpu_rpuratd0_ratdcmd_bit;
        partial void Drpu_Rpuratd0_Ratdcmd_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdcmd_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdcmd_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdctrl_bit;
        partial void Drpu_Rpuratd0_Ratdctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdportpriority_bit;
        partial void Drpu_Rpuratd0_Ratdportpriority_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdportpriority_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdportpriority_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdinterleave2bank_bit;
        partial void Drpu_Rpuratd0_Ratdinterleave2bank_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdinterleave2bank_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdinterleave2bank_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdinterleave4bank_bit;
        partial void Drpu_Rpuratd0_Ratdinterleave4bank_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdinterleave4bank_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdinterleave4bank_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdinterleave8bank_bit;
        partial void Drpu_Rpuratd0_Ratdinterleave8bank_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdinterleave8bank_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdinterleave8bank_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdif_bit;
        partial void Drpu_Rpuratd0_Ratdif_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdif_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdif_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdien_bit;
        partial void Drpu_Rpuratd0_Ratdien_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdien_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdien_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdbankscacheen_bit;
        partial void Drpu_Rpuratd0_Ratdbankscacheen_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdbankscacheen_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdbankscacheen_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdretnctrl_bit;
        partial void Drpu_Rpuratd0_Ratdretnctrl_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdretnctrl_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdretnctrl_ValueProvider(bool a);
    
        protected IFlagRegisterField drpu_rpuratd0_ratdbanksvalid_bit;
        partial void Drpu_Rpuratd0_Ratdbanksvalid_Write(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdbanksvalid_Read(bool a, bool b);
        partial void Drpu_Rpuratd0_Ratdbanksvalid_ValueProvider(bool a);
        partial void Drpu_Rpuratd0_Write(uint a, uint b);
        partial void Drpu_Rpuratd0_Read(uint a, uint b);
        partial void HYDRARAM_Reset();

        partial void SiLabs_HYDRARAM_1_Constructor();

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
            Cmd = 0x4,
            Ctrl = 0x8,
            Portpriority = 0xC,
            Interleave2bank = 0x10,
            Interleave4bank = 0x14,
            Interleave8bank = 0x18,
            Eccerraddr = 0x1C,
            Eccmerrind = 0x20,
            If = 0x24,
            Ien = 0x28,
            Bankscacheen = 0x2C,
            Retnctrl = 0x30,
            Banksvalid = 0x34,
            Drpu_Rpuratd0 = 0x38,
            
            Ipversion_SET = 0x1000,
            Cmd_SET = 0x1004,
            Ctrl_SET = 0x1008,
            Portpriority_SET = 0x100C,
            Interleave2bank_SET = 0x1010,
            Interleave4bank_SET = 0x1014,
            Interleave8bank_SET = 0x1018,
            Eccerraddr_SET = 0x101C,
            Eccmerrind_SET = 0x1020,
            If_SET = 0x1024,
            Ien_SET = 0x1028,
            Bankscacheen_SET = 0x102C,
            Retnctrl_SET = 0x1030,
            Banksvalid_SET = 0x1034,
            Drpu_Rpuratd0_SET = 0x1038,
            
            Ipversion_CLR = 0x2000,
            Cmd_CLR = 0x2004,
            Ctrl_CLR = 0x2008,
            Portpriority_CLR = 0x200C,
            Interleave2bank_CLR = 0x2010,
            Interleave4bank_CLR = 0x2014,
            Interleave8bank_CLR = 0x2018,
            Eccerraddr_CLR = 0x201C,
            Eccmerrind_CLR = 0x2020,
            If_CLR = 0x2024,
            Ien_CLR = 0x2028,
            Bankscacheen_CLR = 0x202C,
            Retnctrl_CLR = 0x2030,
            Banksvalid_CLR = 0x2034,
            Drpu_Rpuratd0_CLR = 0x2038,
            
            Ipversion_TGL = 0x3000,
            Cmd_TGL = 0x3004,
            Ctrl_TGL = 0x3008,
            Portpriority_TGL = 0x300C,
            Interleave2bank_TGL = 0x3010,
            Interleave4bank_TGL = 0x3014,
            Interleave8bank_TGL = 0x3018,
            Eccerraddr_TGL = 0x301C,
            Eccmerrind_TGL = 0x3020,
            If_TGL = 0x3024,
            Ien_TGL = 0x3028,
            Bankscacheen_TGL = 0x302C,
            Retnctrl_TGL = 0x3030,
            Banksvalid_TGL = 0x3034,
            Drpu_Rpuratd0_TGL = 0x3038,
        }   
        
        public long Size => 0x4000;

        protected DoubleWordRegisterCollection registers;
    }
}