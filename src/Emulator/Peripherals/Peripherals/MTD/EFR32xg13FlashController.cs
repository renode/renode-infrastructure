//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.MTD
{
    public class EFR32xg13FlashController : IDoubleWordPeripheral, IKnownSize
    {
        public EFR32xg13FlashController(IMachine machine, MappedMemory flash)
        {
            this.flash = flash;
            if(flash.Size < PageNumber * PageSize)
            {
                throw new ConstructionException($"Provided flash size is too small, expected 0x{PageNumber * PageSize:X} bytes, got 0x{flash.Size:X} bytes");
            }
            interruptsManager = new InterruptManager<Interrupt>(this);

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Control, new DoubleWordRegister(this, 0x1)
                    .WithTaggedFlag("ADDRFAULTEN", 0)
                    .WithTaggedFlag("CLKDISFAULTEN", 1)
                    .WithTaggedFlag("PWRUPONDEMAND", 2)
                    .WithTaggedFlag("IFCREADCLEAR", 3)
                    .WithTaggedFlag("TIMEOUTFAULTEN", 4)
                    .WithReservedBits(5, 3)
                    .WithIgnoredBits(8, 1) // this is written by emlib as an errata
                    .WithReservedBits(9, 23)
                },
                {(long)Registers.ReadControl, new DoubleWordRegister(this, 0x1000100)
                    .WithReservedBits(0, 3)
                    .WithTaggedFlag("IFCDIS", 3)
                    .WithTaggedFlag("AIDIS", 4)
                    .WithTaggedFlag("ICCDIS", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("PREFETCH", 8)
                    .WithTaggedFlag("USEHPROT", 9)
                    .WithReservedBits(10, 14)
                    .WithTag("MODE", 24, 2)
                    .WithReservedBits(26, 2)
                    .WithTaggedFlag("SCBTP", 28)
                    .WithReservedBits(29, 3)
                },
                {(long)Registers.WriteControl, new DoubleWordRegister(this)
                    .WithFlag(0, out isWriteEnabled, name: "WREN")
                    .WithTaggedFlag("IRQERASEABORT", 1)
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.WriteCommand, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Toggle, changeCallback: (_, __) => UpdateWriteAddress(), name: "LADDRIM")
                    .WithFlag(1, FieldMode.Toggle, changeCallback: (_, __) => ErasePage(), name: "ERASEPAGE")
                    .WithTaggedFlag("WRITEEND", 2)
                    .WithFlag(3, FieldMode.Toggle, changeCallback: (_, __) => WriteWordToFlash(), name: "WRITEONCE")
                    .WithTaggedFlag("WRITETRIG", 4)
                    .WithTaggedFlag("ERASEABORT", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("ERASEMAIN0", 8)
                    .WithReservedBits(9, 3)
                    .WithTaggedFlag("CLEARWDATA", 12)
                    .WithReservedBits(13, 19)
                },
                {(long)Registers.AddressBuffer, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out writeAddress, name: "ADDRB")
                },
                {(long)Registers.WriteData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out writeData, writeCallback: (_, __) => writeDataReady.Value = false, name: "WDATA")
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 0x8)
                    .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "BUSY")
                    .WithTaggedFlag("LOCKED", 1)
                    .WithFlag(2, out invalidAddress, FieldMode.Read, name: "INVADDR")
                    // we assume a single-word buffer
                    .WithFlag(3,  out writeDataReady, FieldMode.Read, name: "WDATAREADY")
                    .WithTaggedFlag("WORDTIMEOUT", 4)
                    .WithTaggedFlag("ERASEABORT", 5)
                    .WithTaggedFlag("PCRUNNING", 6)
                    .WithReservedBits(7, 17)
                    .WithTag("WDATAVALID", 24, 4)
                    .WithTag("CLEARWDATA", 28, 4)
                },
                {(long)Registers.InterruptFlag, interruptsManager.GetMaskedInterruptFlagRegister<DoubleWordRegister>()},
                {(long)Registers.InterruptFlagSet, interruptsManager.GetInterruptSetRegister<DoubleWordRegister>()},
                {(long)Registers.InterruptFlagClear, interruptsManager.GetInterruptClearRegister<DoubleWordRegister>()},
                {(long)Registers.InterruptEnable, interruptsManager.GetInterruptEnableRegister<DoubleWordRegister>()},
                {(long)Registers.ConfigurationLock, new DoubleWordRegister(this)
                    .WithValueField(0, 16,
                        writeCallback: (_, value) => { isLocked = value != UnlockPattern; },
                        valueProviderCallback: _ => isLocked ? 1u : 0u,
                        name: "LOCKKEY")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithTaggedFlag("PWRUP", 0)
                    .WithReservedBits(1, 31)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);

            //calling Reset to fill the memory with 0xFFs
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(isLocked && lockableRegisters.Contains((Registers)offset))
            {
                this.Log(LogLevel.Warning, "Trying to write 0x{0:X} to {1} register but the configuration is locked", value, (Registers)offset);
                return;
            }
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
            isLocked = false;
            internalAddressRegister = 0;

            // Clear the whole flash memory
            for(var i = 0; i < PageNumber; ++i)
            {
                flash.WriteBytes(PageSize * i, ErasePattern, 0, PageSize);
            }
        }

        [IrqProvider]
        public GPIO IRQ => new GPIO();
        public long Size => 0x800;

        private void WriteWordToFlash()
        {
            const uint highWord = 0xFFFF0000;
            const uint lowWord = 0xFFFF;
            if(isWriteEnabled.Value && !invalidAddress.Value)
            {
                var effectiveValue = 0u;
                var oldValue = flash.ReadDoubleWord(internalAddressRegister);
                var writeValue = (uint)writeData.Value;
                var newValue = oldValue & writeValue;

                // This code is here to reflect half-word (16bits) writes. It is legal to unset bits once for each half-word.
                // The driver can write 0xFFFF1111 followed by 0x2222FFFF, resulting in 0x22221111.
                // It is also ok to write 0xFFFF1111 followed by 0x22221111.
                // Technicaly, it should also be ok to write 0xFFFF1111 followed by 0x22223333 (the lower half-word is not zeroing any more bits),
                // which would result in 0x22221111, but we treat this situation as a possible bug and we issue a warning.
                // The same result would be achieved when writing 0xFFFF1111 followed 0x22220000 -> 0x22221111 (the lower part is not effective).
                var writeHigh = writeValue & highWord;
                var oldHigh = oldValue & highWord;
                var newHigh = newValue & highWord;
                if(writeHigh != highWord && oldHigh != highWord && oldHigh != writeHigh)
                {
                    this.Log(LogLevel.Warning, "Writing upper word at dirty address 0x{0:X} - currently holding 0x{2:X}, trying to write 0x{1:X}", internalAddressRegister, writeHigh, oldHigh);
                    effectiveValue |= oldHigh;
                }
                else
                {
                    effectiveValue |= newHigh;
                }

                var writeLow = writeValue & lowWord;
                var oldLow = oldValue & lowWord;
                var newLow = newValue & lowWord;
                if(writeLow != lowWord && oldLow != lowWord && oldLow != writeLow)
                {
                    this.Log(LogLevel.Warning, "Writing lower word at dirty address 0x{0:X} - currently holding 0x{2:X}, trying to write 0x{1:X}", internalAddressRegister, writeLow, oldLow);
                    effectiveValue |= oldLow;
                }
                else
                {
                    effectiveValue |= newLow;
                }

                this.Log(LogLevel.Noisy, "Writing 0x{0:X} to 0x{1:X}", effectiveValue, internalAddressRegister);
                flash.WriteDoubleWord(internalAddressRegister, effectiveValue);
                internalAddressRegister += 4;
                writeDataReady.Value = true;
                interruptsManager.SetInterrupt(Interrupt.WriteDone);
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to write to flash, but it didn't work {0} {1}", isWriteEnabled.Value, invalidAddress.Value);
            }
        }

        private void ErasePage()
        {
            //while the MSC_WRITECMD.ERASEPAGE suggests using MSC_ADDRB directly, MSC_STATUS.INVADDR is described
            //as "Invalid Write Address or Erase Page", suggesting using the internal address register
            if(isWriteEnabled.Value && !invalidAddress.Value)
            {
                flash.WriteBytes((long)(internalAddressRegister), ErasePattern, 0, PageSize);
                this.Log(LogLevel.Noisy, "Erasing page on address 0x{0:X}", internalAddressRegister);
                interruptsManager.SetInterrupt(Interrupt.EraseDone);
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to erase page at 0x{0:X}, but writing is disabled", internalAddressRegister);
            }
        }

        private void UpdateWriteAddress()
        {
            if(isWriteEnabled.Value)
            {
                if((long)writeAddress.Value < flash.Size)
                {
                    internalAddressRegister = (uint)writeAddress.Value;
                    invalidAddress.Value = false;
                }
                else
                {
                    this.Log(LogLevel.Error, "Trying to write outside the flash memory at 0x{0:X}", writeAddress.Value);
                    invalidAddress.Value = true;
                }
            }
        }

        private bool isLocked;
        private uint internalAddressRegister;

        private readonly IFlagRegisterField isWriteEnabled;
        private readonly IValueRegisterField writeAddress;
        private readonly IValueRegisterField writeData;
        private readonly IFlagRegisterField invalidAddress;
        private readonly IFlagRegisterField writeDataReady;
        private readonly DoubleWordRegisterCollection registers;
        private readonly MappedMemory flash;
        private readonly byte[] ErasePattern = (byte[])Enumerable.Repeat((byte)0xFF, PageSize).ToArray();
        private readonly Registers[] lockableRegisters = new Registers[] { Registers.Control, Registers.ReadControl,
                                Registers.WriteCommand, Registers.StartupControl, Registers.SoftwareUnlockAPPCommand };
        private readonly InterruptManager<Interrupt> interruptsManager;

        private const uint UnlockPattern = 0x1B71;
        private const int PageSize = 2048;
        private const int PageNumber = 256;

        private enum Interrupt
        {
            EraseDone,
            WriteDone,
            CacheHitsOverflow,
            CacheMissesOverflow,
            FlashPowerUpSequenceComplete,
            ICacheRAMParityError,
            FlashControllerWriteBufferOverflow,
            [NotSettable]
            Reserved,
            FlashLVEWriteError
        }

        private enum Registers : long
        {
            Control = 0x00,
            ReadControl = 0x04,
            WriteControl = 0x08,
            WriteCommand = 0x0C,
            AddressBuffer = 0x10,
            WriteData = 0x18,
            Status = 0x1C,
            InterruptFlag = 0x30,
            InterruptFlagSet = 0x34,
            InterruptFlagClear = 0x38,
            InterruptEnable = 0x3C,
            ConfigurationLock = 0x40,
            FlashCacheCommand = 0x44,
            CacheHitsCounter = 0x48,
            CacheMissesCounter = 0x4C,
            MassEraseLock = 0x54,
            StartupControl = 0x5C,
            Command = 0x74,
            BootloaderReadAndWriteEnable = 0x90,
            SoftwareUnlockAPPCommand = 0x94,
            CacheConfiguration0 = 0x98,
        }
    }
}
