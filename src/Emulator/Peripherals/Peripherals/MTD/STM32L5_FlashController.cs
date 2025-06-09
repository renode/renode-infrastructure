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
    public class STM32L5_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32L5_FlashController(IMachine machine, MappedMemory flash) : base(machine)
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
            Registers.AccessControl.Define(this)
                .WithValueField(0, 4, name: "LATENCY") // Software expects this field to retain the written value
                .WithReservedBits(4, 9)
                .WithTaggedFlag("RUN_PD", 13)
                .WithTaggedFlag("SLEEP_PD", 14)
                .WithTaggedFlag("LVEN", 15)
                .WithReservedBits(16, 16);
            Registers.PowerDownKey.Define(this)
                .WithTag("PDKEYR", 0, 32);
            Registers.NonSecureKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => controlLock.ConsumeValue((uint)val), name: "NSKEY");
            Registers.SecureKey.Define(this)
                .WithTag("SECKEY", 0, 32);
            Registers.OptionKey.Define(this)
                .WithTag("OPTKEY", 0, 32);
            Registers.VoltageKey.Define(this)
                .WithTag("LVEKEYR", 0, 32);
            Registers.NonSecureStatus.Define(this)
                .WithFlag(0, out operationCompletedInterruptStatus, FieldMode.WriteOneToClear, name: "NSEOP")
                .WithFlag(1, out operationErrorInterruptStatus, FieldMode.WriteOneToClear, name: "NSOPERR")
                .WithReservedBits(2, 1)
                .WithTaggedFlag("NSPROGERR", 3)
                .WithTaggedFlag("NSWRPERR", 4)
                .WithTaggedFlag("NSPGAERR", 5)
                .WithTaggedFlag("NSSIZERR", 6)
                .WithFlag(7, out secureProgrammingSequenceError, FieldMode.WriteOneToClear, name: "NSPGSERR")
                .WithReservedBits(8, 4)
                .WithTaggedFlag("OPTWERR", 12)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("NSBSY", 16)
                .WithReservedBits(17, 15);
            Registers.SecureStatus.Define(this)
                .WithTaggedFlag("SECEOP", 0)
                .WithTaggedFlag("SECOPERR", 1)
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SECPROGERR", 3)
                .WithTaggedFlag("SECWRPERR", 4)
                .WithTaggedFlag("SECPGAERR", 5)
                .WithTaggedFlag("SECSIZERR", 6)
                .WithTaggedFlag("SECPGSERR", 7)
                .WithReservedBits(8, 8)
                .WithTaggedFlag("SECBSY", 16)
                .WithReservedBits(17, 15);
            Registers.NonSecureControl1.Define(this, 0xC0000000)
                .WithTaggedFlag("PG", 0)
                .WithFlag(1, out nonSecurePageEraseEnabled, name: "PER")
                .WithFlag(2, out nonSecureMassEraseEnabled, name: "MER")
                .WithValueField(3, 7, out nonSecureErasePageSelection, name: "PNB") // Non-secure page number selection
                .WithReservedBits(10, 1)
                .WithTaggedFlag("ker", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("BWR", 14)
                .WithFlag(15, out nonSecureMassEraseEnabled, name: "MER2")
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
            Registers.SecureControl1.Define(this, 0x80000000)
                // .WithTaggedFlag("PG", 0)
                // .WithTaggedFlag("PER", 1)
                // .WithTaggedFlag("MER", 2)
                // .WithTag("PNB", 3, 7)
                // .WithReservedBits(10, 1)
                // .WithTaggedFlag("ker", 11)
                // .WithReservedBits(12, 2)  
                // .WithTaggedFlag("BWR", 14)
                // .WithTaggedFlag("MER2", 15)
                // .WithTaggedFlag("STRT", 16)
                // .WithReservedBits(17, 7)
                // .WithTaggedFlag("EOPIE", 24)
                // .WithTaggedFlag("ERRIE", 25)
                // .WithReservedBits(26, 3)
                // .WithTaggedFlag("INV", 29)
                // .WithReservedBits(30, 1)
                // .WithTaggedFlag("LOCK", 31);
                .WithTaggedFlag("PG", 0)
                .WithFlag(1, out nonSecurePageEraseEnabled, name: "PER")
                .WithFlag(2, out nonSecureMassEraseEnabled, name: "MER")
                .WithValueField(3, 7, out nonSecureErasePageSelection, name: "PNB") // Non-secure page number selection
                .WithReservedBits(10, 1)
                .WithTaggedFlag("ker", 11)
                .WithReservedBits(12, 2)
                .WithTaggedFlag("BWR", 14)
                .WithFlag(15, out nonSecureMassEraseEnabled, name: "MER2")
                .WithFlag(16, out nonSecureOperationStartEnabled, name: "STRT")
                .WithReservedBits(17, 7)
                .WithFlag(24, out operationCompletedInterruptEnable, name: "EOPIE")
                .WithFlag(25, out operationErrorInterruptEnable, name: "ERRIE")
                .WithReservedBits(26, 3)
                .WithTaggedFlag("INV", 29)
                .WithReservedBits(30, 1)
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
            Registers.Ecc.Define(this)
                .WithTag("ADDR_ECC", 0, 19)
                .WithReservedBits(19, 2)
                .WithTaggedFlag("BK_ECC", 21)
                .WithTaggedFlag("SYSF_ECC", 22)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("ECCIE", 24)
                .WithReservedBits(25, 3)
                .WithTaggedFlag("ECCC2", 28)
                .WithTaggedFlag("ECCD2", 29)
                .WithTaggedFlag("ECCC", 30)
                .WithTaggedFlag("ECCD", 31);
            Registers.OptionControl.Define(this, 0x7FEFF8AA)
                .WithTag("RDP", 0, 8)
                .WithTag("BOR_LEV", 8, 3)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("NRST_STOP", 12)
                .WithTaggedFlag("NRST_STDBY", 13)
                .WithTaggedFlag("NRST_SHDW", 14)
                .WithReservedBits(15, 1)
                .WithTaggedFlag("IWDG_SW", 16)
                .WithTaggedFlag("IWDG_STOP", 17)
                .WithTaggedFlag("IWDG_STDBY", 18)
                .WithTaggedFlag("WWDG_SW", 19)
                .WithTaggedFlag("SWAP_BANK", 20)
                .WithTaggedFlag("DB256", 21)
                .WithTaggedFlag("DBANK", 22)
                .WithReservedBits(23, 1)
                .WithTaggedFlag("SRAM2_PE", 24)
                .WithTaggedFlag("SRAM2_RST", 25)
                .WithTaggedFlag("NSWBOOT0", 26)
                .WithTaggedFlag("NBOOT0", 27)
                .WithTaggedFlag("PA15_PUPEN", 28)
                .WithReservedBits(29, 2)
                .WithTaggedFlag("TZEN", 31);
            Registers.NonSecureBootAddress0.Define(this, 0x0800007F)
                .WithReservedBits(0, 7)
                .WithTag("NSBOOTADD0", 7, 25);
            Registers.NonSecureBootAddress1.Define(this, 0x0BF9007F)
                .WithReservedBits(0, 7)
                .WithTag("NSBOOTADD1", 7, 25);
            Registers.SecureBootAddress0.Define(this, 0x0C00007C)
                .WithTaggedFlag("BOOT_LOCK", 0)
                .WithReservedBits(1, 6)
                .WithTag("SECBOOTADD0", 7, 25);
            Registers.SecureWatermark11.Define(this, 0xFFFFFF80)
                .WithTag("SECWM_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("SECWM_PEND", 16, 7)
                .WithReservedBits(23, 9);
            Registers.SecureWatermark12.Define(this, 0x7F807F80)
                .WithReservedBits(0, 16)
                .WithTag("HDP_PEND", 16, 7)
                .WithReservedBits(23, 8)
                .WithTaggedFlag("HDPEN", 31);
            Registers.WrpAreaAAddress1.Define(this, 0xFF80FFFF )
                .WithTag("WRP1A_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("WRP1A_PEND", 16, 7)
                .WithReservedBits(23, 9);
            Registers.WrpAreaBAddress1.Define(this, 0xFF80FFFF)
                .WithTag("WRP1B_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("WRP1B_PEND", 16, 7)
                .WithReservedBits(23, 9);
            Registers.SecureWatermark21.Define(this, 0xFFFFFF80)
                .WithTag("SECWRPB_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("SECWRPB_PEND", 16, 7)
                .WithReservedBits(23, 9);
            Registers.SecureWatermark22.Define(this, 0x7F807F80)
                .WithReservedBits(0, 16)
                .WithTag("HDP2_PEND", 16, 7)
                .WithReservedBits(23, 8)
                .WithTaggedFlag("HDP2EN", 31);
            Registers.WrpAreaAAddress2.Define(this, 0xFF80FFFF)
                .WithTag("WRP2A_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("WRP2A_PEND", 16, 7)
                .WithReservedBits(23, 9);
            Registers.WrpAreaBAddress2.Define(this, 0xFF80FFFF)
                .WithTag("WRP2B_PSTRT", 0, 7)
                .WithReservedBits(7, 9)
                .WithTag("WRP2B_PEND", 16, 7)
                .WithReservedBits(23, 9);
            Registers.SecureBlockBank1R1.Define(this)
                .WithTag("SECBB1", 0, 32);
            Registers.SecureBlockBank1R2.Define(this)
                .WithTag("SECBB1", 0, 32);
            Registers.SecureBlockBank1R3.Define(this)
                .WithTag("SECBB1", 0, 32);
            Registers.SecureBlockBank1R4.Define(this)
                .WithTag("SECBB1", 0, 32);
            Registers.SecureBlockBank2R1.Define(this)
                .WithTag("SECBB2", 0, 32);
            Registers.SecureBlockBank2R2.Define(this)
                .WithTag("SECBB2", 0, 32);
            Registers.SecureBlockBank2R3.Define(this)
                .WithTag("SECBB2", 0, 32);
            Registers.SecureBlockBank2R4.Define(this)
                .WithTag("SECBB2", 0, 32);    
            Registers.SecureHdpControl.Define(this)
                .WithTaggedFlag("HDP1_ACCDIS", 0)
                .WithTaggedFlag("HDP2_ACCDIS", 1)
                .WithReservedBits(2, 30);
            Registers.PrivilegeConfiguration.Define(this)
                .WithTaggedFlag("PRIV", 0)
                .WithReservedBits(1, 31);
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
            PowerDownKey           = 0x04, // PDKEYR           
            //Intentional gap
            NonSecureKey           = 0x08, // NSKEYR           
            SecureKey              = 0x0C, // SECKEYR          
            OptionKey              = 0x10, // OPTKEYR          
            VoltageKey             = 0x14, // LVEKEYR          
            //Intentional gap
            NonSecureStatus        = 0x20, // NSSR             
            SecureStatus           = 0x24, // SECSR            
            NonSecureControl1      = 0x28, // NSCR1            
            SecureControl1         = 0x2C, // SECCR1           
            Ecc                    = 0x30, // ECCR                     
            //Intentional gap
            OptionControl          = 0x40, // OPTR             
            SecureControl2         = 0x3C, // SECCR2           
            NonSecureBootAddress0  = 0x44, // NSBOOTADD0R      
            NonSecureBootAddress1  = 0x48, // NSBOOTADD1R      
            SecureBootAddress0     = 0x4C, // SECBOOTADD0R     
            SecureWatermark11      = 0x50, // SECWMR1          
            SecureWatermark12      = 0x54, // SECWMR2          
            WrpAreaAAddress1       = 0x58, // WRPAR            
            WrpAreaBAddress1       = 0x5C, // WRPBR            
            SecureWatermark21      = 0x60, // SECWM2R1
            SecureWatermark22      = 0x64, // SECWM2R2
            WrpAreaAAddress2       = 0x68, // FLASH_WRP2AR
            WrpAreaBAddress2       = 0x6C, // FLASH_WRP2BR
            //Intentional gap     
            SecureBlockBank1R1     = 0x80, // SECBB1R1          
            SecureBlockBank1R2     = 0x84, // SECBB1R2          
            SecureBlockBank1R3     = 0x88, // SECBB1R3          
            SecureBlockBank1R4     = 0x8C, // SECBB1R4 
            SecureBlockBank2R1     = 0xA0, // SECBB2R1          
            SecureBlockBank2R2     = 0xA4, // SECBB2R2          
            SecureBlockBank2R3     = 0xA8, // SECBB2R3          
            SecureBlockBank2R4     = 0xAC, // SECBB2R4             
            //Intentional gap
            SecureHdpControl       = 0xC0, // SECHDPCR         
            PrivilegeConfiguration = 0xC4, // PRIVCFGR                
#pragma warning restore format 
        }
    }
}
