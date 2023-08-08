//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32H7_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32H7_FlashController(IMachine machine, MappedMemory flash1, MappedMemory flash2) : base(machine)
        {
            bank1 = flash1;
            bank2 = flash2;

            controlBank1Lock = new LockRegister(this, nameof(controlBank1Lock), ControlBankKey);
            controlBank2Lock = new LockRegister(this, nameof(controlBank2Lock), ControlBankKey);
            optionControlLock = new LockRegister(this, nameof(optionControlLock), OptionControlKey);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            controlBank1Lock.Reset();
            controlBank2Lock.Reset();
            optionControlLock.Reset();

            ProgramCurrentValues();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if(optionControlLock.IsLocked && IsOffsetToProgramRegister(offset))
            {
                this.Log(LogLevel.Warning, "Attempted to write to a program register ({0}) while OPTLOCK bit is set. Ignoring", Enum.GetName(typeof(Registers), (Registers)offset));
                return;
            }
            base.WriteDoubleWord(offset, value);
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            // Software writes to this register and expects written value back during reads to work correctly
            Registers.AccessControl.Define(this, 0x37)
                .WithValueField(0, 4, name: "LATENCY")
                .WithValueField(4, 2, name: "WRHIGHFREQ")
                .WithReservedBits(6, 26);

            Registers.KeyBank1.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_KEYR1",
                    writeCallback: (_, value) => controlBank1Lock.ConsumeValue((uint)value));

            Registers.OptionKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_OPTKEYR",
                    writeCallback: (_, value) => optionControlLock.ConsumeValue((uint)value));

            Registers.ControlBank1.Define(this, 0x31)
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "LOCK1", valueProviderCallback: _ => controlBank1Lock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            controlBank1Lock.Lock();
                        }
                    })
                .WithTaggedFlag("PG1", 1)
                .WithTaggedFlag("SER1", 2)
                .WithTaggedFlag("BER1", 3)
                .WithTag("PSIZE1", 4, 2)
                .WithTaggedFlag("FW1", 6)
                .WithTaggedFlag("START1", 7)
                .WithTag("SNB1", 8, 3)
                .WithReservedBits(11, 4)
                .WithTaggedFlag("CRC_EN", 15)
                .WithTaggedFlag("EOPIE1", 16)
                .WithTaggedFlag("WRPERRIE1", 17)
                .WithTaggedFlag("PGSERRIE1", 18)
                .WithTaggedFlag("STRBERRIE1", 19)
                .WithReservedBits(20, 1)
                .WithTaggedFlag("INCERRIE1", 21)
                .WithTaggedFlag("OPERRIE1", 22)
                .WithTaggedFlag("RDPERRIE1", 23)
                .WithTaggedFlag("RDSERRIE1", 24)
                .WithTaggedFlag("SNECCERRIE1", 25)
                .WithTaggedFlag("DBECCERRIE1", 26)
                .WithTaggedFlag("CRCENDIE1", 27)
                .WithTaggedFlag("CRCRDERRIE1", 28)
                .WithReservedBits(29, 3);

            Registers.OptionControl.Define(this, 0x1)
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "OPTLOCK", valueProviderCallback: _ => optionControlLock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            optionControlLock.Lock();
                        }
                    })
                .WithFlag(1, FieldMode.Write, name: "OPTSTART", writeCallback: (_, value) =>
                    {
                        if(!value)
                        {
                            return;
                        }

                        if(optionControlLock.IsLocked)
                        {
                            this.Log(LogLevel.Warning, "Trying to start option byte change operation while the controller is locked. Ignoring");
                            return;
                        }

                        ProgramCurrentValues();
                    })
                .WithReservedBits(2, 2)
                .WithTaggedFlag("MER", 4)
                .WithReservedBits(5, 25)
                .WithTaggedFlag("OPTCHANGEERRIE", 30)
                .WithTaggedFlag("SWAP_BANK", 31);

            Registers.OptionStatusCurrent.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "FLASH_OPTSR_CUR", valueProviderCallback: _ => optionStatusCurrentValue);

            Registers.OptionStatusProgram.Define(this, 0x406AAF0, false)
                .WithValueField(0, 32, out optionStatusProgramRegister, name: "FLASH_OPTSR_PRG", softResettable: false);

            Registers.WriteSectorProtectionCurrentBank1.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "WRPSn1", valueProviderCallback: _ => bank1WriteProtectionCurrentValue)
                .WithReservedBits(8, 24);

            Registers.WriteSectorProtectionProgramBank1.Define(this, 0xFF, false)
                .WithValueField(0, 8, out bank1WriteProtectionProgramRegister, name: "WRPSn1", softResettable: false)
                .WithReservedBits(8, 24);

            Registers.KeyBank2.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_KEYR2",
                    writeCallback: (_, value) => controlBank2Lock.ConsumeValue((uint)value));

            Registers.ControlBank2.Define(this, 0x31)
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "LOCK2", valueProviderCallback: _ => controlBank2Lock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            controlBank2Lock.Lock();
                        }
                    })
                .WithTaggedFlag("PG1", 1)
                .WithTaggedFlag("SER1", 2)
                .WithTaggedFlag("BER1", 3)
                .WithTag("PSIZE1", 4, 2)
                .WithTaggedFlag("FW1", 6)
                .WithTaggedFlag("START1", 7)
                .WithTag("SNB1", 8, 3)
                .WithReservedBits(11, 4)
                .WithTaggedFlag("CRC_EN", 15)
                .WithTaggedFlag("EOPIE1", 16)
                .WithTaggedFlag("WRPERRIE1", 17)
                .WithTaggedFlag("PGSERRIE1", 18)
                .WithTaggedFlag("STRBERRIE1", 19)
                .WithReservedBits(20, 1)
                .WithTaggedFlag("INCERRIE1", 21)
                .WithTaggedFlag("OPERRIE1", 22)
                .WithTaggedFlag("RDPERRIE1", 23)
                .WithTaggedFlag("RDSERRIE1", 24)
                .WithTaggedFlag("SNECCERRIE1", 25)
                .WithTaggedFlag("DBECCERRIE1", 26)
                .WithTaggedFlag("CRCENDIE1", 27)
                .WithTaggedFlag("CRCRDERRIE1", 28)
                .WithReservedBits(29, 3);

            Registers.WriteSectorProtectionCurrentBank2.Define(this)
                .WithValueField(0, 8, FieldMode.Read, name: "WRPSn2", valueProviderCallback: _ => bank2WriteProtectionCurrentValue)
                .WithReservedBits(8, 24);

            Registers.WriteSectorProtectionProgramBank2.Define(this, 0xFF, false)
                .WithValueField(0, 8, out bank2WriteProtectionProgramRegister, name: "WRPSn2", softResettable: false)
                .WithReservedBits(8, 24);
        }

        private void ProgramCurrentValues()
        {
            optionStatusCurrentValue = (uint)optionStatusProgramRegister.Value;
            bank1WriteProtectionCurrentValue = (byte)bank1WriteProtectionProgramRegister.Value;
            bank2WriteProtectionCurrentValue = (byte)bank2WriteProtectionProgramRegister.Value;
        }

        private bool IsOffsetToProgramRegister(long offset)
        {
            switch((Registers)offset)
            {
                case Registers.ProtectionAddressProgramBank1:
                case Registers.ProtectionAddressProgramBank2:
                case Registers.BootAddressProgram:
                case Registers.OptionStatusProgram:
                case Registers.SecureAddressProgramBank1:
                case Registers.SecureAddressProgramBank2:
                case Registers.WriteSectorProtectionProgramBank1:
                case Registers.WriteSectorProtectionProgramBank2:
                    return true;
                default:
                    return false;
            }
        }

        private uint optionStatusCurrentValue;
        private byte bank1WriteProtectionCurrentValue;
        private byte bank2WriteProtectionCurrentValue;

        private IValueRegisterField optionStatusProgramRegister;
        private IValueRegisterField bank1WriteProtectionProgramRegister;
        private IValueRegisterField bank2WriteProtectionProgramRegister;

        private readonly MappedMemory bank1;
        private readonly MappedMemory bank2;

        private readonly LockRegister controlBank1Lock;
        private readonly LockRegister controlBank2Lock;
        private readonly LockRegister optionControlLock;

        private static readonly uint[] ControlBankKey = {0x45670123, 0xCDEF89AB};
        private static readonly uint[] OptionControlKey = {0x08192A3B, 0x4C5D6E7F};

        private enum Registers
        {
            AccessControl = 0x000,                      // FLASH_ACR
            KeyBank1 = 0x004,                           // FLASH_KEYR1
            OptionKey = 0x008,                          // FLASH_OPTKEYR
            ControlBank1 = 0x00C,                       // FLASH_CR1
            StatusBank1 = 0x010,                        // FLASH_SR1
            ClearControlBank1 = 0x014,                  // FLASH_CCR1
            OptionControl = 0x018,                      // FLASH_OPTCR
            OptionStatusCurrent = 0x01C,                // FLASH_OPTSR_CUR
            OptionStatusProgram = 0x020,                // FLASH_OPTSR_PRG
            OptionClearControl = 0x024,                 // FLASH_OPTCCR
            ProtectionAddressCurrentBank1 = 0x028,      // FLASH_PRAR_CUR1
            ProtectionAddressProgramBank1 = 0x02C,      // FLASH_PRAR_PRG1
            SecureAddressCurrentBank1 = 0x030,          // FLASH_SCAR_CUR1
            SecureAddressProgramBank1 = 0x034,          // FLASH_SCAR_PRG1
            WriteSectorProtectionCurrentBank1 = 0x038,  // FLASH_WPSN_CUR1R
            WriteSectorProtectionProgramBank1 = 0x03C,  // FLASH_WPSN_PRG1R
            BootAddressCurrent = 0x040,                 // FLASH_BOOT_CURR
            BootAddressProgram = 0x044,                 // FLASH_BOOT_PRGR
            CRCControlBank1 = 0x050,                    // FLASH_CRCCR1
            CRCStartAddressBank1 = 0x054,               // FLASH_CRCSADD1R
            CRCEndAddressBank1 = 0x058,                 // FLASH_CRCEADD1R
            CRCData = 0x05C,                            // FLASH_CRCDATAR
            ECCFailAddressBank1 = 0x060,                // FLASH_ECC_FA1R
            KeyBank2 = 0x104,                           // FLASH_KEYR2
            ControlBank2 = 0x10C,                       // FLASH_CR2
            StatusBank2 = 0x110,                        // FLASH_SR2
            ClearControlBank2 = 0x114,                  // FLASH_CCR2
            ProtectionAddressCurrentBank2 = 0x128,      // FLASH_PRAR_CUR2
            ProtectionAddressProgramBank2 = 0x12C,      // FLASH_PRAR_PRG2
            SecureAddressCurrentBank2 = 0x130,          // FLASH_SCAR_CUR2
            SecureAddressProgramBank2 = 0x134,          // FLASH_SCAR_PRG2
            WriteSectorProtectionCurrentBank2 = 0x138,  // FLASH_WPSN_CUR2R
            WriteSectorProtectionProgramBank2 = 0x13C,  // FLASH_WPSN_PRG2R
            CRCControlBank2 = 0x150,                    // FLASH_CRCCR2
            CRCStartAddressBank2 = 0x154,               // FLASH_CRCSADD2R
            CRCEndAddressBank2 = 0x158,                 // FLASH_CRCEADD2R
            ECCFailAddressBank2 = 0x160,                // FLASH_ECC_FA2R
        }
    }
}
