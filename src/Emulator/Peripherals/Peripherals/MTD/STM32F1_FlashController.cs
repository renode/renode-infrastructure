//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

// Based on STM32F4_FlashController by Gissio (C)-2025:

using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class STM32F1_FlashController : STM32_FlashController, IKnownSize
    {
        public STM32F1_FlashController(IMachine machine, MappedMemory flash, uint pageSize, uint pageNum) : base(machine)
        {
            this.flash = flash;
            this.pageSize = pageSize;
            this.pageNum = pageNum;

            controlLock = new LockRegister(this, nameof(controlLock), ControlLockKey);
            optionControlLock = new LockRegister(this, nameof(optionControlLock), OptionLockKey);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            controlLock.Reset();
            optionControlLock.Reset();
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            if ((Registers)offset == Registers.Control && controlLock.IsLocked)
            {
                this.Log(LogLevel.Warning, "Attempted to write 0x{0:X8} to a locked Control register. Ignoring...", value);
                return;
            }

            base.WriteDoubleWord(offset, value);
        }

        public long Size => 0x400;

        private void DefineRegisters()
        {
            Registers.AccessControl.Define(this, 0x00000030)
                .WithValueField(0, 3, name: "LATENCY")
                .WithTaggedFlag("HLFCYA", 3)
                .WithTaggedFlag("PRFTBE", 4)
                .WithTaggedFlag("PRFTBS", 5)
                .WithReservedBits(6, 26);

            Registers.Key.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_KEYR",
                    writeCallback: (_, value) => controlLock.ConsumeValue((uint)value));

            Registers.OptionKey.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FLASH_OPTKEYR",
                    writeCallback: (_, value) => optionControlLock.ConsumeValue((uint)value));

            Registers.Status.Define(this)
                .WithTaggedFlag("BSY", 0)
                .WithReservedBits(1, 1)
                .WithTaggedFlag("PGERR", 2)
                .WithReservedBits(3, 1)
                .WithTaggedFlag("WRPERR", 4)
                .WithTaggedFlag("EOP", 5)
                .WithReservedBits(6, 26);

            Registers.Control.Define(this, 0x00000080)
                .WithTaggedFlag("PG", 0)
                .WithFlag(1, out var pageErase, name: "PER")
                .WithFlag(2, out var massErase, name: "MER")
                .WithReservedBits(3, 1)
                .WithTaggedFlag("OPTPG", 4)
                .WithTaggedFlag("OPTER", 5)
                .WithFlag(6, out var startErase, name: "STRT",
                    mode: FieldMode.Read | FieldMode.Set,
                    valueProviderCallback: _ => false)
                .WithFlag(7, FieldMode.Read | FieldMode.Set, name: "LOCK",
                    valueProviderCallback: _ => controlLock.IsLocked,
                    changeCallback: (_, value) =>
                    {
                        if (value)
                        {
                            controlLock.Lock();
                        }
                    })
                .WithReservedBits(8, 1)
                .WithTaggedFlag("OPTWRE", 9)
                .WithTaggedFlag("ERRIE", 10)
                .WithReservedBits(11, 1)
                .WithTaggedFlag("EOPIE", 12)
                .WithReservedBits(13, 19)
                .WithChangeCallback((_, __) =>
                    {
                        if (startErase.Value)
                        {
                            Erase(massErase.Value, pageErase.Value);
                        }
                    });

            Registers.Address.Define(this)
                .WithValueField(0, 32,
                    writeCallback: (_, value) => pageAddress = (uint)value, name: "FLASH_AR");

            Registers.OptionByte.Define(this, 0x03FFFFFFC)
                .WithTaggedFlag("OPTERR", 0)
                .WithTaggedFlag("RDPRT", 1)
                .WithTaggedFlag("WDG_SW", 2)
                .WithTaggedFlag("nRST_STOP", 3)
                .WithTaggedFlag("nRST_STDBY", 4)
                .WithReservedBits(5, 27);

            Registers.WriteProtection.Define(this, 0xFFFFFFFF)
                .WithValueField(0, 32, name: "WRP");
        }

        private void Erase(bool massErase, bool sectorErase)
        {
            if (!massErase && !sectorErase)
            {
                this.Log(LogLevel.Warning, "Tried to erase flash, but MER and SER are reset. This should be forbidden, ignoring...");
                return;
            }

            if (massErase)
            {
                PerformMassErase();
            }
            else
            {
                PerformPageErase();
            }
        }

        private void PerformPageErase()
        {
            uint pageMask = pageSize * (pageNum - 1);
            uint eraseAddress = pageAddress & pageMask;

            this.Log(LogLevel.Info, "Erasing page, offset 0x{0:X}", eraseAddress);
            flash.WriteBytes(eraseAddress, ErasePattern, (int)pageSize);
        }

        private void PerformMassErase()
        {
            this.Log(LogLevel.Info, "Performing flash mass erase");
            flash.WriteBytes(0, ErasePattern, (int)(pageSize * pageNum));
        }

        private readonly MappedMemory flash;
        private readonly MappedMemory optionBytes;
        private readonly uint pageSize;
        private readonly uint pageNum;
        private readonly LockRegister controlLock;
        private readonly LockRegister optionControlLock;

        private static readonly uint[] ControlLockKey = { 0x45670123, 0xCDEF89AB };
        private static readonly uint[] OptionLockKey = { 0x45670123, 0xCDEF89AB };
        private static readonly byte[] ErasePattern = Enumerable.Repeat((byte)0xFF, MaxSectorSize).ToArray();

        private const int MaxSectorSize = 0x20000;

        private uint pageAddress = 0;

        private enum Registers
        {
            AccessControl = 0x00,   // FLASH_ACR
            Key = 0x04,             // FLASH_KEYR
            OptionKey = 0x08,       // FLASH_OPTKEYR
            Status = 0x0C,          // FLASH_SR
            Control = 0x10,         // FLASH_CR
            Address = 0x14,         // FLASH_AR
            OptionByte = 0x1C,      // FLASH_OBR
            WriteProtection = 0x20, // FLASH_WRPR
        }
    }
}
