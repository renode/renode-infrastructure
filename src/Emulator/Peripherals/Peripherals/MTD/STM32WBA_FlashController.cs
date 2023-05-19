//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32WBA_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32WBA_FlashController(Machine machine, MappedMemory flash) : base(machine)
        {
            bank = flash;

            DefineRegisters();
            Reset();
        }

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this, 0x1)
                .WithTag("LATENCY",0, 4 )
                .WithReservedBits(4,4)
                .WithTaggedFlag("PRFTEN", 8)
                .WithReservedBits(9,2)
                .WithTaggedFlag("LPM", 11)
                .WithTaggedFlag("PDREQ", 12)
                .WithReservedBits(13,1)
                .WithTaggedFlag("SLEEP_PD", 14)
                .WithReservedBits(15,17);
            Registers.NonSecureKey.Define(this)
                .WithTag("NSKEY", 0, 32);
            Registers.SecureKey.Define(this)
                .WithTag("SECKEY", 0, 32);
            Registers.OptionKey.Define(this)
                .WithTag("OPTKEY", 0, 32);
            Registers.PowerDownKey.Define(this)
                .WithTag("PDKEY", 0, 32);
            Registers.NonSecureStatus.Define(this)
                .WithTaggedFlag("EOP", 0)
                .WithTaggedFlag("OPERR", 1)
                .WithTaggedFlag("PROGERR", 3)
                .WithTaggedFlag("WRPERR", 4)
                .WithTaggedFlag("PGAERR", 5)
                .WithTaggedFlag("SIZERR", 6)
                .WithTaggedFlag("PGSERR", 7)
                .WithReservedBits(8, 5)
                .WithTaggedFlag("OPTWERR", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("BSY", 16)
                .WithTaggedFlag("WDW", 17)
                .WithTaggedFlag("OEM1LOCK", 18)
                .WithTaggedFlag("OEM2LOCK", 19)
                .WithTaggedFlag("PD", 20)
                .WithReservedBits(21, 11);
            Registers.NonSecureControl.Define(this)
                .WithTagedFlag("PG", 0)
                .WithTagedFlag("PER", 1)
                .WithTagedFlag("MER", 2)
                .WithTag("PNB", 3, 7) 9:3 [6:0]: Non-secure page number selection
                .WithReservedBits() // 13:10 Reserved, must be kept at reset value.
                .WithTagedFlag("BWR", 14)
                .WithReservedBit() // 15 Reserved, must be kept at reset value.
                .WithTagedFlag("STRT", 16)
                .WithTagedFlag("OPTSTRT", 17)
                .WithReservedBits() // 23:18 Reserved, must be kept at reset value.
                .WithTagedFlag("EOPIE", 24)
                .WithTagedFlag("ERRIE", 25)
                .WithReservedBit() // 26 Reserved, must be kept at reset value.
                .WithTagedFlag("OBL_LAUNCH", 27)
                .WithReservedBits() // 29:28 Reserved, must be kept at reset value.
                .WithTagedFlag("OPTLOCK", 30)
                .WithTagedFlag("LOCK", 31)
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x1000;

        private readonly MappedMemory bank;
        private enum Registers
        {
            AccessControl = 0x00, // ACR              
            //Intentional gap
            NonSecureKey = 0x08, // NSKEYR           
            SecureKey = 0x0C, // SECKEYR          
            OptionKey = 0x10, // OPTKEYR          
            //Intentional gap
            PowerDownKey = 0x18, // PDKEYR           
            //Intentional gap
            NonSecureStatus = 0x20, // NSSR             
            SecureStatus = 0x24, // SECSR            
            NonSecureControl1 = 0x28, // NSCR1            
            SecureControl1 = 0x2C, // SECCR1           
            Ecc = 0x30, // ECCR             
            Opsr = 0x34, // OPSR             
            NonSecureControl2 = 0x38, // NSCR2            
            SecureControl2 = 0x3C, // SECCR2           
            OptionControl = 0x40, // OPTR             
            NonSecureBootAddress0 = 0x44, // NSBOOTADD0R      
            NonSecureBootAddress1 = 0x48, // NSBOOTADD1R      
            SecureBootAddress0 = 0x4C, // SECBOOTADD0R     
            SecureWatermark11 = 0x50, // SECWMR1          
            SecureWatermark12 = 0x54, // SECWMR2          
            WrpAreaAaDdress = 0x58, // WRPAR            
            wrpAreaBaDdress = 0x5C, // WRPBR            
            //Intentional gap
            Oem1Key1 = 0x70, // OEM1KEYR1        
            Oem1Key2 = 0x74, // OEM1KEYR2        
            Oem2Key1 = 0x78, // OEM2KEYR1        
            Oem2Key2 = 0x7C, // OEM2KEYR2        
            SecureBlockBank1 = 0x80, // SECBBR1          
            SecureBlockBank2 = 0x84, // SECBBR2          
            SecureBlockBank3 = 0x88, // SECBBR3          
            SecureBlockBank4 = 0x8C, // SECBBR4          
            //Intentional gap
            SecureHdpControl = 0xC0, // SECHDPCR         
            PrivilegeConfiguration = 0xC4, // PRIVCFGR         
            //Intentional gap
            PrivilegeBlockBank1 = 0xD0, // PRIVBBR1         
            PrivilegeBlockBank2 = 0xD4, // PRIVBBR2         
            PrivilegeBlockBank3 = 0xD8, // PRIVBBR3         
            PrivilegeBlockBank4 = 0xDC, // PRIVBBR4         
        }
    }
}
