//
// Copyright (c) 2010-2024 Antmicro
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
    public class STM32F4_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32F4_FlashController(IMachine machine, MappedMemory flash) : base(machine)
        {
            this.flash = flash;

            controlLock = new LockRegister(this, nameof(controlLock), ControlLockKey);
            optionControlLock = new LockRegister(this, nameof(optionControlLock), OptionLockKey);

            optionBytesRegisters = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            controlLock.Reset();
            optionControlLock.Reset();
        }

        [ConnectionRegion("optionBytes")]
        public uint ReadDoubleWordFromOptionBytes(long offset)
        {
            uint value = optionBytesRegisters.Read(offset);
            this.Log(LogLevel.Debug, "Reading from option bytes (offset: 0x{0:X} value: 0x{1:X8})", offset, value);
            return value;
        }

        [ConnectionRegion("optionBytes")]
        public void WriteDoubleWordToOptionBytes(long offset, uint value)
        {
            // This region is modified by using the OptionControl register. Direct modification is not allowed
            this.Log(LogLevel.Error, "Attempt to write 0x{0:X8} to {1} in the option bytes region", value, offset);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if((Registers)offset == Registers.Control && controlLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "Attempted to write 0x{0:X8} to a locked Control register. Ignoring...", value);
                return;
            }

            if((Registers)offset == Registers.OptionControl && optionControlLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "Attempted to write 0x{0:X8} to a locked OptionControl register. Ignoring...", value);
                return;
            }

            base.WriteDoubleWord(offset, value);
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this)
                //This field is written and read by software and we need to keep it's value.
                .WithValueField(0, 4, name: "LATENCY")
                .WithReservedBits(4, 4)
                .WithTaggedFlag("PRFTEN", 8)
                .WithTaggedFlag("ICEN", 9)
                .WithTaggedFlag("DCEN", 10)
                .WithTaggedFlag("ICRST", 11)
                .WithTaggedFlag("DCRST", 12)
                .WithReservedBits(13, 19);

            Registers.Key.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_KEYR",
                    writeCallback: (_, value) => controlLock.ConsumeValue((uint)value));

            Registers.OptionKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_OPTKEYR",
                    writeCallback: (_, value) => optionControlLock.ConsumeValue((uint)value));

            Registers.Status.Define(this)
                .WithTaggedFlag("EOP", 0)
                .WithTaggedFlag("OPERR", 1)
                .WithReservedBits(2, 2)
                .WithTaggedFlag("WRPERR", 4)
                .WithTaggedFlag("PGAERR", 5)
                .WithTaggedFlag("PGPERR", 6)
                .WithTaggedFlag("PGSERR", 7)
                .WithTaggedFlag("RDERR", 8)
                .WithReservedBits(9, 7)
                .WithTaggedFlag("BSY", 16)
                .WithReservedBits(17, 15);

            Registers.Control.Define(this)
                .WithTaggedFlag("PG", 0)
                .WithFlag(1, out var sectorErase, name: "SER")
                .WithFlag(2, out var massErase, name: "MER")
                .WithValueField(3, 4, out var sectorNumber, name: "SNB")
                .WithReservedBits(7, 1)
                .WithTag("PSIZE", 8, 2)
                .WithReservedBits(10, 6)
                .WithFlag(16, out var startErase, name: "STRT", mode: FieldMode.Read | FieldMode.Set, valueProviderCallback: _ => false)
                .WithReservedBits(17, 7)
                .WithTaggedFlag("EOPIE", 24)
                .WithTaggedFlag("ERRIE", 25)
                .WithReservedBits(26, 5)
                .WithFlag(31, FieldMode.Read | FieldMode.Set, name: "LOCK", valueProviderCallback: _ => controlLock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            controlLock.Lock();
                        }
                    })
                .WithChangeCallback((_, __) => 
                {
                    if(startErase.Value)
                    {
                        Erase(massErase.Value, sectorErase.Value, (uint)sectorNumber.Value);
                    }
                });

            Registers.OptionControl.Define(this, 0xFFFAAED)
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "OPTLOCK", valueProviderCallback: _ => optionControlLock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if (value)
                        {
                            optionControlLock.Lock();
                        }
                    })
                .WithFlag(1, FieldMode.Read | FieldMode.Set, name: "OPTSTRT", changeCallback: (_, value) =>
                    {
                        if(value)
                        {
                            readProtectionOptionBytes.Value = readProtectionRegister.Value;
                            writeProtectionOptionBytes.Value = writeProtectionRegister.Value;
                        }
                    })
                .WithTag("BOR_LEV", 2, 2)
                .WithReservedBits(4, 1)
                .WithTag("USER", 5, 2)
                // According to the documentation those fields should be reloaded from flash memory at reset
                // but it doesn't specify where they should be stored and how should they be saved
                .WithValueField(8, 8, out readProtectionRegister, name: "RDP", softResettable: false)
                .WithValueField(16, 12, out writeProtectionRegister, name: "nWRP", softResettable: false)
                .WithReservedBits(28, 3)
                .WithTaggedFlag("SPRMOD", 31);

            OptionBytesRegisters.ReadProtectionAndUser.Define(optionBytesRegisters, 0xAAEC)
                .WithReservedBits(0, 2)
                .WithTag("BOR_LEV", 2, 2)
                .WithReservedBits(4, 1)
                .WithTaggedFlag("WDG_SW", 5)
                .WithTaggedFlag("nRST_STOP", 6)
                .WithTaggedFlag("nRST_STDBY", 7)
                .WithValueField(8, 8, out readProtectionOptionBytes, name: "RDP")
                .WithReservedBits(16, 16);

            OptionBytesRegisters.WriteProtection.Define(optionBytesRegisters, 0xFFF)
                .WithValueField(0, 12, out writeProtectionOptionBytes, name: "nWRPi")
                .WithReservedBits(12, 3)
                .WithTaggedFlag("SPRMOD", 15)
                .WithReservedBits(16, 16);
        }

        private void Erase(bool massErase, bool sectorErase, uint sectorNumber)
        {
            if(!massErase && !sectorErase)
            {
                this.Log(LogLevel.Warning, "Tried to erase flash, but MER and SER are reset. This should be forbidden, ignoring...");
                return;
            }

            if(massErase)
            {
                PerformMassErase();
            }
            else
            {
                PerformSectorErase(sectorNumber);
            }
        }

        private void PerformSectorErase(uint sectorNumber)
        {
            if(!Sectors.ContainsKey(sectorNumber))
            {
                this.Log(LogLevel.Warning, "Tried to erase sector {0}, which doesn't exist. Ignoring...", sectorNumber);
                return;
            }

            this.Log(LogLevel.Noisy, "Erasing sector {0}, offset 0x{1:X}, size 0x{2:X}", sectorNumber, Sectors[sectorNumber].Offset, Sectors[sectorNumber].Size);
            flash.WriteBytes(Sectors[sectorNumber].Offset, ErasePattern, Sectors[sectorNumber].Size);
        }

        private void PerformMassErase()
        {
            this.Log(LogLevel.Noisy, "Performing flash mass erase");
            foreach(var sectorNumber in Sectors.Keys)
            {
                PerformSectorErase(sectorNumber);
            }
        }

        private readonly MappedMemory flash;
        private readonly LockRegister controlLock;
        private readonly LockRegister optionControlLock;
        private readonly DoubleWordRegisterCollection optionBytesRegisters;

        private IValueRegisterField readProtectionRegister;
        private IValueRegisterField writeProtectionRegister;

        private IValueRegisterField readProtectionOptionBytes;
        private IValueRegisterField writeProtectionOptionBytes;

        private static readonly uint[] ControlLockKey = {0x45670123, 0xCDEF89AB};
        private static readonly uint[] OptionLockKey = {0x8192A3B, 0x4C5D6E7F};
        private static readonly byte[] ErasePattern = Enumerable.Repeat((byte)0xFF, MaxSectorSize).ToArray();
        private static readonly Dictionary<uint, Sector> Sectors = new Dictionary<uint, Sector>()
        {
            { 0, new Sector { Offset = 0x00000000, Size = 0x4000 } },
            { 1, new Sector { Offset = 0x00004000, Size = 0x4000 } },
            { 2, new Sector { Offset = 0x00008000, Size = 0x4000 } },
            { 3, new Sector { Offset = 0x0000C000, Size = 0x4000 } },
            { 4, new Sector { Offset = 0x00010000, Size = 0x4000 } },
            { 5, new Sector { Offset = 0x00020000, Size = 0x10000 } },
            { 6, new Sector { Offset = 0x00040000, Size = 0x20000 } },
            { 7, new Sector { Offset = 0x00060000, Size = 0x20000 } },
            { 8, new Sector { Offset = 0x00080000, Size = 0x20000 } },
            { 9, new Sector { Offset = 0x000A0000, Size = 0x20000 } },
            { 10, new Sector { Offset = 0x000C0000, Size = 0x20000 } },
            { 11, new Sector { Offset = 0x000E0000, Size = 0x20000 } },
        };

        private const int MaxSectorSize = 0x20000;

        private class Sector
        {
            public uint Offset { get; set; }
            public int Size { get; set; }
        }

        private enum Registers
        {
            AccessControl = 0x00,   // FLASH_ACR
            Key = 0x04,             // FLASH_KEYR
            OptionKey = 0x08,       // FLASH_OPTKEYR
            Status = 0x0C,          // FLASH_SR
            Control = 0x10,         // FLASH_CR
            OptionControl = 0x14,   // FLASH_OPTCR
        }

        private enum OptionBytesRegisters
        {
            ReadProtectionAndUser = 0x0,
            WriteProtection = 0x8,
        }
    }
}
