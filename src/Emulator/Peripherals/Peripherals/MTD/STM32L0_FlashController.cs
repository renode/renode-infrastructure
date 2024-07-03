//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Profiling;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32L0_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32L0_FlashController(IMachine machine, MappedMemory flash, MappedMemory eeprom) : base(machine)
        {
            this.underlyingFlash = flash;
            this.underlyingEeprom = eeprom;

            powerDownLock = new LockRegister(this, nameof(powerDownLock), PowerDownKeys);
            programEraseControlLock = new LockRegister(this, nameof(programEraseControlLock), ProgramEraseControlKeys);
            programEraseLock = new LockRegister(this, nameof(programEraseLock), ProgramEraseKeys);
            optionByteLock = new LockRegister(this, nameof(optionByteLock), OptionByteKeys);

            signatureRegisters = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            programEraseControlLock.Locked += delegate
            {
                programEraseLock.Lock();
                optionByteLock.Lock();
                programEraseControl.Reset();
            };

            Reset();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            var reg = (Registers)offset;
            // The PECR lock is the only one that covers a whole register and not a single bit.
            // Handle it here for simplicity.
            if(reg == Registers.ProgramEraseControl && programEraseControlLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "Attempt to write {0:x8} to {1} while it is locked", value, reg);
                return;
            }
            base.WriteDoubleWord(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            powerDownLock.Reset();
            programEraseControlLock.Reset();
            programEraseLock.Reset();
            optionByteLock.Reset();
        }

        [ConnectionRegion("signature")]
        public uint ReadDoubleWordFromSignature(long offset)
        {
            return signatureRegisters.Read(offset);
        }

        [ConnectionRegion("signature")]
        public void WriteDoubleWordToSignature(long offset, uint value)
        {
            //intentionally left blank
            this.Log(LogLevel.Error, "Attempt to write {0:x8} to {1} in the signature region", value, offset);
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this)
                .WithFlag(0, name: "LATENCY")
                .WithFlag(1, out prefetchEnabled, name: "PRFTEN", changeCallback: (oldValue, value) =>
                    {
                        if(value && disableBuffer.Value)
                        {
                            this.Log(LogLevel.Warning, "Attempt to set PRFTEN while DISAB_BUF is set, ignoring");
                            prefetchEnabled.Value = oldValue;
                        }
                    })
                .WithReservedBits(2, 1)
                .WithTaggedFlag("SLEEP_PD", 3)
                .WithFlag(4, out runPowerDown, name: "RUN_PD", changeCallback: (oldValue, value) =>
                    {
                        if(powerDownLock.IsLocked)
                        {
                            this.Log(LogLevel.Warning, "Attempt to write RUN_PD while it is locked, ignoring");
                            runPowerDown.Value = oldValue;
                        }
                        else if(!value)
                        {
                            powerDownLock.Lock(); // Resetting the RUN_PD flag re-locks the bit
                        }
                    })
                .WithFlag(5, out disableBuffer, name: "DISAB_BUF", changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            prereadEnabled.Value = false;
                            prefetchEnabled.Value = false;
                        }
                    })
                .WithFlag(6, out prereadEnabled, name: "PRE_READ", changeCallback: (oldValue, value) =>
                    {
                        if(value && disableBuffer.Value)
                        {
                            this.Log(LogLevel.Warning, "Attempt to set PRE_READ while DISAB_BUF is set, ignoring");
                            prereadEnabled.Value = oldValue;
                        }
                    });

            // PECR is protected by programEraseControlLock in WriteDoubleWord. If we get to any of
            // the callbacks below, the PECR register is definitely not locked and they don't need to check.
            programEraseControl = Registers.ProgramEraseControl.Define(this, 0x7)
                .WithFlag(0, name: "PE_LOCK", valueProviderCallback: _ => programEraseControlLock.IsLocked, changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            programEraseControlLock.Lock();
                        }
                        // We don't need to handle an attempt at a keyless unlock because PECR can only
                        // be written if it is unlocked (see WriteDoubleWord)
                    })
                .WithFlag(1, name: "PRG_LOCK", valueProviderCallback: _ => programEraseLock.IsLocked, changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            programEraseLock.Lock();
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Attempt to unlock PRG_LOCK without key");
                        }
                    })
                .WithFlag(2, name: "OPT_LOCK", valueProviderCallback: _ => optionByteLock.IsLocked, changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            optionByteLock.Lock();
                        }
                        else
                        {
                            this.Log(LogLevel.Warning, "Attempt to unlock OPT_LOCK without key");
                        }
                    })
                .WithFlag(3, out programMemorySelect, name: "PROG")
                .WithTaggedFlag("DATA", 4)
                .WithReservedBits(5, 3)
                .WithTaggedFlag("FIX", 8)
                .WithFlag(9, name: "ERASE", changeCallback: (_, value) => { SetEraseMode(value); })
                .WithTaggedFlag("FPRG", 10)
                .WithReservedBits(11, 4)
                .WithTaggedFlag("PARALLELBANK", 15)
                .WithTaggedFlag("EOPIE", 16)
                .WithTaggedFlag("ERRIE", 17)
                .WithTaggedFlag("OBL_LAUNCH", 18)
                .WithReservedBits(19, 4)
                .WithTaggedFlag("NZDISABLE", 23)
                .WithReservedBits(24, 8);

            Registers.PowerDownKey.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "FLASH_PDKEYR", writeCallback: (_, value) =>
                    {
                        powerDownLock.ConsumeValue((uint)value);
                    });

            Registers.ProgramEraseControlKey.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "FLASH_PEKEYR", writeCallback: (_, value) =>
                    {
                        programEraseControlLock.ConsumeValue((uint)value);
                    });

            Registers.ProgramAndEraseKey.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "FLASH_PRGKEYR", writeCallback: (_, value) =>
                    {
                        programEraseLock.ConsumeValue((uint)value);
                    });

            Registers.OptionBytesUnlockKey.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Write, name: "FLASH_OPTKEYR", writeCallback: (_, value) =>
                    {
                        optionByteLock.ConsumeValue((uint)value);
                        this.Log(LogLevel.Warning, "Option bytes unlock key register accessed, option bytes currently unimplemented");
                    });

            Registers.Status.Define(this, 0xC)
                .WithFlag(0, mode: FieldMode.Read, valueProviderCallback: _ => false, name: "BSY")
                .WithFlag(1, mode: FieldMode.Read | FieldMode.WriteOneToClear, valueProviderCallback: _ => false, name: "EOP")
                .WithFlag(2, mode: FieldMode.Read, valueProviderCallback: _ => true, name: "ENDHV")
                .WithFlag(3, mode: FieldMode.Read, valueProviderCallback: _ => true, name: "READY")
                .WithReservedBits(4, 4)
                .WithFlag(8, mode: FieldMode.WriteOneToClear, name: "WRPERR")
                .WithFlag(9, mode: FieldMode.WriteOneToClear, name: "PGAERR")
                .WithFlag(10, mode: FieldMode.WriteOneToClear, name: "SIZERR")
                .WithFlag(11, mode: FieldMode.WriteOneToClear, name: "OPTVERR")
                .WithReservedBits(12, 1)
                .WithFlag(13, mode: FieldMode.WriteOneToClear, name: "RDERR")
                .WithReservedBits(14, 2)
                .WithFlag(16, mode: FieldMode.WriteOneToClear, name: "NOTZEROERR")
                .WithFlag(17, mode: FieldMode.WriteOneToClear, name: "FWWERR")
                .WithReservedBits(18, 14);

            Registers.OptionBytes.Define(this, 0x807000AA)
                .WithValueField(0, 8, mode: FieldMode.Read, name: "RDPROT")
                .WithFlag(8, mode: FieldMode.Read, name: "WPRMOD")
                .WithReservedBits(9, 7)
                .WithValueField(16, 4, mode: FieldMode.Read, name: "BOR_LEV")
                .WithFlag(20, mode: FieldMode.Read, name: "WDG_SW")
                .WithFlag(21, mode: FieldMode.Read, name: "nRTS_STOP")
                .WithFlag(22, mode: FieldMode.Read, name: "nRTS_STDBY")
                .WithFlag(23, mode: FieldMode.Read, name: "BFB2")
                .WithReservedBits(24, 5)
                .WithFlag(29, mode: FieldMode.Read, name: "nBOOT_SEL")
                .WithFlag(30, mode: FieldMode.Read, name: "nBOOT0")
                .WithFlag(31, mode: FieldMode.Read, name: "nBOOT1");

            Registers.WriteProtection1.Define(this)
                .WithValueField(0, 32, mode: FieldMode.Read, name: "WRPROT1");

            Registers.WriteProtection2.Define(this)
                .WithValueField(0, 16, mode: FieldMode.Read, name: "WRPROT2")
                .WithReservedBits(16, 16);

            // The fields containing the lot number are in ASCII (so the lot number is "000000")
            SignatureRegisters.UniqueId1.Define(signatureRegisters)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0, name: "WAF_NUM")
                .WithValueField(8, 24, FieldMode.Read, valueProviderCallback: _ => 0x3030, name: "LOT_NUM[55:32]");

            SignatureRegisters.UniqueId2.Define(signatureRegisters)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0x30303030, name: "LOT_NUM[31:0]");

            SignatureRegisters.UniqueId3.Define(signatureRegisters)
                .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => 0, name: "U_ID[95:64]");

            SignatureRegisters.FlashSize.Define(signatureRegisters)
                .WithValueField(0, 16, FieldMode.Read, valueProviderCallback: _ => (uint)(underlyingFlash.Size / 1024), name: "F_SIZE");
        }

        private void EraseMemoryAccessHook(ulong pc, MemoryOperation operation, ulong virtualAddress, ulong physicalAddress, ulong value)
        {
            // Only write accesses can be used to erase
            if(operation != MemoryOperation.MemoryWrite && operation != MemoryOperation.MemoryIOWrite)
            {
                return;
            }

            // Only accesses to our underlying flash or EEPROM peripheral can be used to erase
            var registered = machine.SystemBus.WhatIsAt(physicalAddress);
            if(registered == null)
            {
                return;
            }

            var offset = physicalAddress - registered.RegistrationPoint.Range.StartAddress;
            if(registered.Peripheral == underlyingFlash)
            {
                // Program memory must be selected
                if(!programMemorySelect.Value)
                {
                    this.Log(LogLevel.Warning, "Erase attempted without program memory being selected");
                    return;
                }

                // Writing anything anywhere within a flash page erases the whole page
                var flashPageStart = offset & ~(ulong)(FlashPageSize - 1);
                underlyingFlash.WriteBytes((long)flashPageStart, FlashPageErasePattern);
                this.Log(LogLevel.Debug, "Erased flash page {0} (at 0x{1:x8})", flashPageStart / FlashPageSize, flashPageStart);
            }
            else if(registered.Peripheral == underlyingEeprom)
            {
                // Program memory must not be selected
                if(programMemorySelect.Value)
                {
                    this.Log(LogLevel.Warning, "EEPROM word erase attempted with program memory selected");
                    return;
                }

                // Writing anything anywhere within an EEPROM word erases the whole word
                var eepromWordStart = offset & ~(ulong)(EepromWordSize - 1);
                underlyingEeprom.WriteDoubleWord((long)eepromWordStart, EepromWordErasePattern);
                this.Log(LogLevel.Debug, "Erased EEPROM word {0} (at 0x{1:x8})", eepromWordStart / EepromWordSize, eepromWordStart);
            }
        }

        private void SetEraseMode(bool enabled)
        {
            if(!machine.SystemBus.TryGetCurrentCPU(out var icpu))
            {
                this.Log(LogLevel.Error, "Failed to get CPU");
                return;
            }

            var cpu = icpu as ICPUWithMemoryAccessHooks;
            if(cpu == null)
            {
                this.Log(LogLevel.Error, "CPU does not support memory access hooks, cannot trigger memory erase");
                return;
            }

            Action<ulong, MemoryOperation, ulong, ulong, ulong> hook = EraseMemoryAccessHook;
            cpu.SetHookAtMemoryAccess(enabled ? hook : null);
        }

        private readonly MappedMemory underlyingFlash;
        private readonly MappedMemory underlyingEeprom;

        private DoubleWordRegister programEraseControl;
        private IFlagRegisterField prefetchEnabled;
        private IFlagRegisterField runPowerDown;
        private IFlagRegisterField prereadEnabled;
        private IFlagRegisterField disableBuffer;
        private IFlagRegisterField programMemorySelect;
        private readonly LockRegister powerDownLock;
        private readonly LockRegister programEraseControlLock;
        private readonly LockRegister programEraseLock;
        private readonly LockRegister optionByteLock;

        private readonly DoubleWordRegisterCollection signatureRegisters;

        private const int EepromWordSize = 4;
        private const int FlashPageSize = 128;
        private const uint EepromWordErasePattern = 0;
        private static readonly byte[] FlashPageErasePattern = (byte[])Enumerable.Repeat((byte)0x00, FlashPageSize).ToArray();
        private static readonly uint[] PowerDownKeys = {0x04152637, 0xFAFBFCFD};
        private static readonly uint[] ProgramEraseControlKeys = {0x89ABCDEF, 0x02030405};
        private static readonly uint[] ProgramEraseKeys = {0x8C9DAEBF, 0x13141516};
        private static readonly uint[] OptionByteKeys = {0xFBEAD9C8, 0x24252627};

        private enum Registers : long
        {
            AccessControl = 0x00,          // ACR
            ProgramEraseControl = 0x04,    // PECR
            PowerDownKey = 0x08,           // PDKEYR
            ProgramEraseControlKey = 0x0C, // PEKEYR
            ProgramAndEraseKey = 0x10,     // PRGKEYR
            OptionBytesUnlockKey = 0x14,   // OPTKEYR
            Status = 0x18,                 // SR
            OptionBytes = 0x1C,            // OPTR
            WriteProtection1 = 0x20,       // WRPROT1
            WriteProtection2 = 0x80,       // WRPROT2
        }

        private enum SignatureRegisters : long
        {
            UniqueId1 = 0x50, // U_ID(31:0)
            UniqueId2 = 0x54, // U_ID(63:32)
            UniqueId3 = 0x64, // U_ID(95:64)
            FlashSize = 0x7c, // F_SIZE
        }
    }
}
