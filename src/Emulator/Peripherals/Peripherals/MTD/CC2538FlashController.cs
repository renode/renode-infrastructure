//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Memory;

namespace Antmicro.Renode.Peripherals.MTD
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class CC2538FlashController : IDoubleWordPeripheral, IKnownSize
    {
        public CC2538FlashController(IMachine machine, MappedMemory flash)
        {
            this.flash = flash;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.FlashControl, new DoubleWordRegister(this, 0x4)
                    .WithFlag(0, valueProviderCallback: _ => false, changeCallback: (_, value) =>
                            {
                                if(value)
                                {
                                    Erase();
                                }
                            }, name: "ERASE")
                    .WithFlag(1, out write, name: "WRITE")
                    .WithTag("CACHE_MODE", 2, 2)
                    .WithReservedBits(4, 1)
                    .WithTag("ABORT", 5, 1)
                    .WithFlag(6, FieldMode.Read, name: "FULL")
                    .WithFlag(7, FieldMode.Read, valueProviderCallback: _ => write.Value, name: "BUSY")
                    .WithTag("SEL_INFO_PAGE", 8, 1)
                    .WithTag("UPPER_PAGE_ACCESS", 9, 1)
                    .WithReservedBits(10, 22)
                },
                {(long)Registers.FlashAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 16, valueProviderCallback: _ => writeAddress >> 2, writeCallback: (_, value) => { writeAddress = (uint)value; }, name : "FADDR")
                    .WithReservedBits(17, 15)
                },
                {(long)Registers.FlashData, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, value) => Write((uint)value), name: "FWDATA")
                },
                {(long)Registers.DieConfig0, new DoubleWordRegister(this, 0xB9640580)
                    .WithValueField(0, 32, FieldMode.Read)
                },
                {(long)Registers.DieConfig1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read)
                },
                {(long)Registers.DieConfig2, new DoubleWordRegister(this, 0x2000)
                    .WithValueField(0, 32, FieldMode.Read)
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
            writeAddress = 0;
            // Clear the whole flash memory
            for(uint i = 0; i < PageNumber; ++i)
            {
                flash.WriteBytes(0x800 * i, ErasePattern, 0, PageSize);
            }
        }

        public long Size => 0x1000;

        private void Write(uint newValue)
        {
            var targetAddress = writeAddress;
            if(!write.Value)
            {
                this.Log(LogLevel.Warning, "Writing 0x{0:X} to 0x{1:X} when not in write mode", newValue, targetAddress);
                return;
            }
            if(targetAddress > flash.Size)
            {
                this.Log(LogLevel.Error, "Trying to write outside the flash memory at 0x{0:X}", targetAddress);
                return;
            }
            var oldValue = flash.ReadDoubleWord(targetAddress);
            if(oldValue != 0xffffffff)
            {
                this.Log(LogLevel.Warning, "Writing to a dirty word at address 0x{0:X}", targetAddress);
            }
            this.Log(LogLevel.Noisy, "Writing 0x{0:X} to 0x{1:X}", newValue, targetAddress);
            flash.WriteDoubleWord(targetAddress, oldValue & newValue);
            write.Value = false;
        }

        private void Erase()
        {
            flash.WriteBytes((long)((writeAddress) & ~(PageSize - 1)), ErasePattern, 0, PageSize);
            this.Log(LogLevel.Noisy, "Erasing on address 0x{0:X}", (writeAddress & ~(PageSize - 1)));
        }

        private uint writeAddress;
        private readonly IFlagRegisterField write;

        private readonly DoubleWordRegisterCollection registers;
        private readonly MappedMemory flash;

        private const int PageSize = 2048;
        private const int PageNumber = 256;
        private readonly byte[] ErasePattern = (byte[])Enumerable.Repeat((byte)0xFF, PageSize).ToArray();

        private enum Registers : long
        {
            FlashControl = 0x08,
            FlashAddress = 0x0c,
            FlashData = 0x10,
            DieConfig0 = 0x14,
            DieConfig1 = 0x18,
            DieConfig2 = 0x1c
        }
    }
}
