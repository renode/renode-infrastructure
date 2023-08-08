//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32WBA_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32WBA_FlashController(IMachine machine, MappedMemory flash) : base(machine)
        {
            bank = flash;
            bank.ResetByte = ResetByte;

            controlLock = new LockRegister(this, nameof(controlLock), NonSecureLockKey, unlockedAfterReset: true);

            NonSecureInterrupt = new GPIO();

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            controlLock.Reset();
            base.Reset();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if((offset == (long)Registers.NonSecureControl1) && controlLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "Trying to write to a locked register");
                return;
            }
            base.WriteDoubleWord(offset, value);
        }

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this, 0x1)
                .WithValueField(0, 4, name: "LATENCY") // Software expects this field to retain the written value
                .WithReservedBits(4, 4)
                .WithTaggedFlag("PRFTEN", 8)
                .WithReservedBits(9, 2)
                .WithTaggedFlag("LPM", 11)
                .WithTaggedFlag("PDREQ", 12)
                .WithReservedBits(13, 1)
                .WithTaggedFlag("SLEEP_PD", 14)
                .WithReservedBits(15, 17);
            Registers.NonSecureKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => controlLock.ConsumeValue((uint)val), name: "NSKEY");
            Registers.SecureKey.Define(this)
                .WithTag("SECKEY", 0, 32);
            Registers.OptionKey.Define(this)
                .WithTag("OPTKEY", 0, 32);
            Registers.PowerDownKey.Define(this)
                .WithTag("PDKEY", 0, 32);
            Registers.NonSecureStatus.Define(this)
                .WithFlag(0, out operationCompletedInterruptStatus, FieldMode.WriteOneToClear, name: "EOP")
                .WithFlag(1, out operationErrorInterruptStatus, FieldMode.WriteOneToClear, name: "OPERR")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("PROGERR", 3)
                .WithTaggedFlag("WRPERR", 4)
                .WithTaggedFlag("PGAERR", 5)
                .WithTaggedFlag("SIZERR", 6)
                .WithFlag(7, out secureProgrammingSequenceError, FieldMode.WriteOneToClear, name: "PGSERR")
                .WithReservedBits(8, 5)
                .WithTaggedFlag("OPTWERR", 13)
                .WithReservedBits(14, 2)
                .WithTaggedFlag("BSY", 16)
                .WithTaggedFlag("WDW", 17)
                .WithTaggedFlag("OEM1LOCK", 18)
                .WithTaggedFlag("OEM2LOCK", 19)
                .WithTaggedFlag("PD", 20)
                .WithReservedBits(21, 11);
            Registers.SecureStatus.Define(this)
                .WithTaggedFlag("EOP", 0)
                .WithTaggedFlag("OPERR", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("PROGERR", 3)
                .WithTaggedFlag("WRPERR", 4)
                .WithTaggedFlag("PGAERR", 5)
                .WithTaggedFlag("SIZERR", 6)
                .WithTaggedFlag("PGSERR", 7)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("BSY", 16)
                .WithTaggedFlag("WDW", 17)
                .WithReservedBits(18, 14);
            Registers.NonSecureControl1.Define(this)
                .WithTaggedFlag("PG", 0)
                .WithFlag(1, out nonSecurePageEraseEnabled, name: "PER")
                .WithFlag(2, out nonSecureMassEraseEnabled, name: "MER")
                .WithValueField(3, 7, out nonSecureErasePageSelection, name: "PNB") // Non-secure page number selection
                .WithReservedBits(10, 4) // 13:10 Reserved, must be kept at reset value.
                .WithTaggedFlag("BWR", 14)
                .WithReservedBits(15, 1) // 15 Reserved, must be kept at reset value.
                .WithFlag(16, out nonSecureOperationStartEnabled, name: "STRT")
                .WithTaggedFlag("OPTSTRT", 17)
                .WithReservedBits(18, 6) // 23:18 Reserved, must be kept at reset value.
                .WithFlag(24, out operationCompletedInterruptEnable, name: "EOPIE")
                .WithFlag(25, out operationErrorInterruptEnable, name: "ERRIE")
                .WithReservedBits(26, 1) // 26 Reserved, must be kept at reset value.
                .WithTaggedFlag("OBL_LAUNCH", 27)
                .WithReservedBits(28, 2) // 29:28 Reserved, must be kept at reset value.
                .WithTaggedFlag("OPTLOCK", 30)
                .WithFlag(31, FieldMode.Read,
                    valueProviderCallback: (_) => controlLock.IsLocked,
                    writeCallback: (_, val) =>
                    {
                        if(val) controlLock.Lock();
                    }, name: "LOCK")
                .WithWriteCallback((_, __) =>
                {
                    if(nonSecureOperationStartEnabled.Value)
                    {
                        EraseMemory();
                    }
                });
            Registers.SecureControl1.Define(this)
                .WithTaggedFlag("PG", 0)
                .WithTaggedFlag("PER", 1)
                .WithTaggedFlag("MER", 2)
                .WithTag("PNB", 3, 7)
                .WithReservedBits(10, 4)
                .WithTaggedFlag("BWR", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("STRT", 16)
                .WithReservedBits(17, 7)
                .WithTaggedFlag("EOPIE", 24)
                .WithTaggedFlag("ERRIE", 25)
                .WithReservedBits(26, 3)
                .WithTaggedFlag("INV", 29)
                .WithReservedBits(30, 1)
                .WithTaggedFlag("LOCK", 31);
            Registers.Ecc.Define(this)
                .WithTag("ADDR_ECC", 0, 20)
                .WithReservedBits(20, 2)
                .WithTaggedFlag("SYSF_ECC", 22)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("ECCIE", 24)
                .WithReservedBits(25, 5)
                .WithTaggedFlag("ECCC", 30)
                .WithTaggedFlag("ECCD", 31);
            Registers.Opsr.Define(this)
                .WithTag("ADDR_OP", 0, 20)
                .WithReservedBits(20, 2)
                .WithTaggedFlag("SYSF_OP", 22)
                .WithReservedBits(23, 6)
                .WithTag("CODE_OP", 29, 3);
            Registers.NonSecureControl2.Define(this)
                .WithTaggedFlag("PS", 0)
                .WithTaggedFlag("ES", 1)
                .WithReservedBits(2, 30);
            Registers.SecureControl2.Define(this)
                .WithTaggedFlag("PS", 0)
                .WithTaggedFlag("ES", 1)
                .WithReservedBits(2, 30);
            Registers.OptionControl.Define(this)
                .WithTag("RDP", 0, 8)
                .WithTag("BOR_LEV", 8, 3)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("NRST_STOP", 12)
                .WithTaggedFlag("NRST_STDBY", 13)
                .WithReservedBits(14, 1)
                .WithTaggedFlag("SRAM1_RST", 15)
                .WithTaggedFlag("IWDG_SW", 16)
                .WithTaggedFlag("IWDG_STOP", 17)
                .WithTaggedFlag("IWDG_STDBY", 18)
                .WithTaggedFlag("WWDG_SW", 19)
                .WithReservedBits(20, 4)
                .WithTaggedFlag("SRAM2_PE", 24)
                .WithTaggedFlag("SRAM2_RST", 25)
                .WithTaggedFlag("NSWBOOT0", 26)
                .WithTaggedFlag("NBOOT0", 27)
                .WithReservedBits(28, 3)
                .WithTaggedFlag("TZEN", 31);
            Registers.NonSecureBootAddress0.Define(this)
                .WithReservedBits(0, 7)
                .WithTag("NSBOOTADD0", 7, 25);
            Registers.NonSecureBootAddress1.Define(this)
                .WithReservedBits(0, 7)
                .WithTag("NSBOOTADD1", 7, 25);
            Registers.SecureBootAddress0.Define(this)
                .WithTaggedFlag("BOOT_LOCK", 0)
                .WithReservedBits(1, 6)
                .WithTag("SECBOOTADD0", 7, 25);
            Registers.SecureWatermark11.Define(this)
                .WithTag("SECWM_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("SECWM_PEND", 16, 7)
                .WithReservedBits(23, 9);
            Registers.SecureWatermark12.Define(this)
                .WithReservedBits(0, 16)
                .WithTag("HDP_PEND", 16, 7)
                .WithReservedBits(23, 8)
                .WithTaggedFlag("HDPEN", 31);
            Registers.WrpAreaAAddress.Define(this)
                .WithTag("WRPA_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("WRPA_PEND", 16, 7)
                .WithReservedBits(23, 8)
                .WithTaggedFlag("UNLOCK", 31);
            Registers.WrpAreaBAddress.Define(this)
                .WithTag("WRPB_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("WRPB_PEND", 16, 7)
                .WithReservedBits(23, 8)
                .WithTaggedFlag("UNLOCK", 31);
            Registers.Oem1Key1.Define(this)
                .WithTag("OEM1KEY", 0, 32);
            Registers.Oem1Key2.Define(this)
                .WithTag("OEM1KEY", 0, 32);
            Registers.Oem2Key1.Define(this)
                .WithTag("OEM2KEY", 0, 32);
            Registers.Oem2Key2.Define(this)
                .WithTag("OEM2KEY", 0, 32);
            Registers.SecureBlockBank1.Define(this)
                .WithTag("SECBB", 0, 32);
            Registers.SecureBlockBank2.Define(this)
                .WithTag("SECBB", 0, 32);
            Registers.SecureBlockBank3.Define(this)
                .WithTag("SECBB", 0, 32);
            Registers.SecureBlockBank4.Define(this)
                .WithTag("SECBB", 0, 32);
            Registers.SecureHdpControl.Define(this)
                .WithTaggedFlag("HDP_ACCDIS", 0)
                .WithReservedBits(1, 31);
            Registers.PrivilegeConfiguration.Define(this)
                .WithTaggedFlag("SPRIV", 0)
                .WithTaggedFlag("NSPRIV", 1)
                .WithReservedBits(2, 30);
            Registers.PrivilegeBlockBank1.Define(this)
                .WithTag("PRIVBB", 0, 32);
            Registers.PrivilegeBlockBank2.Define(this)
                .WithTag("PRIVBB", 0, 32);
            Registers.PrivilegeBlockBank3.Define(this)
                .WithTag("PRIVBB", 0, 32);
            Registers.PrivilegeBlockBank4.Define(this)
                .WithTag("PRIVBB", 0, 32);
        }

        private void EraseMemory()
        {
            nonSecureOperationStartEnabled.Value = false;
            if(!nonSecurePageEraseEnabled.Value && !nonSecureMassEraseEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Running erase while neither PER nor MER are selected is forbidden");
                secureProgrammingSequenceError.Value = true;
                if(operationErrorInterruptEnable.Value)
                {
                    // Spec states that this bit can be set only if the interrupt is enabled
                    operationErrorInterruptStatus.Value = true;
                    UpdateInterrupts();
                }
                return;
            }

            if(nonSecureMassEraseEnabled.Value)
            {
                this.DebugLog("Erasing whole flash memory");
                bank.ZeroAll();
            }
            else
            {
                this.DebugLog("Erasing memory page {0}", nonSecureErasePageSelection.Value);
                ErasePage(nonSecureErasePageSelection.Value);
            }

            if(operationCompletedInterruptEnable.Value)
            {
                // Spec states that this bit can be set only if the interrupt is enabled
                operationCompletedInterruptStatus.Value = true;
                UpdateInterrupts();
            }
        }

        private void ErasePage(ulong pageIndex)
        {
            var rangeStartOffset = (long)(PageSize * pageIndex);
            bank.ZeroRange(rangeStartOffset, PageSize);
        }

        private void UpdateInterrupts()
        {
            var operationCompleted = operationCompletedInterruptEnable.Value && operationCompletedInterruptStatus.Value;
            var operationError = operationErrorInterruptEnable.Value && operationErrorInterruptStatus.Value;
            NonSecureInterrupt.Set(operationCompleted || operationError);
        }

        public long Size => 0x1000;
        public GPIO NonSecureInterrupt { get; }

        private IFlagRegisterField nonSecurePageEraseEnabled;
        private IFlagRegisterField nonSecureMassEraseEnabled;
        private IFlagRegisterField nonSecureOperationStartEnabled;
        private IFlagRegisterField secureProgrammingSequenceError;
        private IValueRegisterField nonSecureErasePageSelection;
        private IFlagRegisterField operationCompletedInterruptEnable;
        private IFlagRegisterField operationCompletedInterruptStatus;
        private IFlagRegisterField operationErrorInterruptEnable;
        private IFlagRegisterField operationErrorInterruptStatus;

        private readonly MappedMemory bank;
        private readonly LockRegister controlLock;

        // Per spec the flash memory page size is 8kBytes
        private const byte ResetByte = 0xff;
        private const long PageSize = 8 * 1024;
        private static readonly uint[] NonSecureLockKey = { 0x45670123, 0xCDEF89AB };

        private enum Registers
        {
#pragma warning disable format 
            AccessControl          = 0x00, // ACR              
            //Intentional gap
            NonSecureKey           = 0x08, // NSKEYR           
            SecureKey              = 0x0C, // SECKEYR          
            OptionKey              = 0x10, // OPTKEYR          
            //Intentional gap
            PowerDownKey           = 0x18, // PDKEYR           
            //Intentional gap
            NonSecureStatus        = 0x20, // NSSR             
            SecureStatus           = 0x24, // SECSR            
            NonSecureControl1      = 0x28, // NSCR1            
            SecureControl1         = 0x2C, // SECCR1           
            Ecc                    = 0x30, // ECCR             
            Opsr                   = 0x34, // OPSR             
            NonSecureControl2      = 0x38, // NSCR2            
            SecureControl2         = 0x3C, // SECCR2           
            OptionControl          = 0x40, // OPTR             
            NonSecureBootAddress0  = 0x44, // NSBOOTADD0R      
            NonSecureBootAddress1  = 0x48, // NSBOOTADD1R      
            SecureBootAddress0     = 0x4C, // SECBOOTADD0R     
            SecureWatermark11      = 0x50, // SECWMR1          
            SecureWatermark12      = 0x54, // SECWMR2          
            WrpAreaAAddress        = 0x58, // WRPAR            
            WrpAreaBAddress        = 0x5C, // WRPBR            
            //Intentional gap
            Oem1Key1               = 0x70, // OEM1KEYR1        
            Oem1Key2               = 0x74, // OEM1KEYR2        
            Oem2Key1               = 0x78, // OEM2KEYR1        
            Oem2Key2               = 0x7C, // OEM2KEYR2        
            SecureBlockBank1       = 0x80, // SECBBR1          
            SecureBlockBank2       = 0x84, // SECBBR2          
            SecureBlockBank3       = 0x88, // SECBBR3          
            SecureBlockBank4       = 0x8C, // SECBBR4          
            //Intentional gap
            SecureHdpControl       = 0xC0, // SECHDPCR         
            PrivilegeConfiguration = 0xC4, // PRIVCFGR         
            //Intentional gap
            PrivilegeBlockBank1    = 0xD0, // PRIVBBR1         
            PrivilegeBlockBank2    = 0xD4, // PRIVBBR2         
            PrivilegeBlockBank3    = 0xD8, // PRIVBBR3         
            PrivilegeBlockBank4    = 0xDC, // PRIVBBR4         
#pragma warning restore format 
        }
    }
}
