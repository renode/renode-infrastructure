//
// Copyright (c) 2010-2018 Antmicro
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
    public class STM32F407FlashController : IDoubleWordPeripheral, IKnownSize
    { 
        public STM32F407FlashController(Machine machine, MappedMemory flash)
        {
            this.flash = flash;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.FlashControl, new DoubleWordRegister(this, 0x0)

                    .WithFlag(0, name: "PG", mode: FieldMode.Write | FieldMode.Read)
                    .WithFlag(1, name: "SER", mode: FieldMode.Write | FieldMode.Read)
                    .WithValueField(3, 4, changeCallback: (_ ,value) => { SectorNumber = value; }, name: "SNB")
                    .WithTag("PSIZE", 8, 2)
                    .WithFlag(16, valueProviderCallback: _ => false, changeCallback: (_, value) =>
                            {
                                if(value)
                                {
                                    Erase();
                                }
                            }, name: "START")
                    
                    .WithFlag(24, name: "EOPIE", mode: FieldMode.Write | FieldMode.Read)
                    .WithFlag(25, name: "ERRIE", mode: FieldMode.Write | FieldMode.Read)
                    .WithFlag(31, name: "LOCK",valueProviderCallback: _ => false, mode: FieldMode.Write | FieldMode.Read) 
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
            this.Log(LogLevel.Info, "Reset");
            
            // Clear the whole flash memory
            for (uint i = 0; i < SectorsDesc.Count; ++i)
            {
                flash.WriteBytes(SectorsDesc[i].offset, ErasePattern, 0, SectorsDesc[i].size);
            }
        }

        public long Size => 0x1C;

        private void Erase()
        {
            this.Log(LogLevel.Noisy, "Erasing segment {0}, offset 0x{1:X}, size 0x{2:X}", SectorNumber, SectorsDesc[SectorNumber].offset, SectorsDesc[SectorNumber].size);
            
            flash.WriteBytes(SectorsDesc[SectorNumber].offset, ErasePattern, 0, SectorsDesc[SectorNumber].size);
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly MappedMemory flash;
        private const int PageSize = 0x20000; // max segment size
        private readonly byte[] ErasePattern = (byte[])Enumerable.Repeat((byte)0xFF, PageSize).ToArray();
        private uint SectorNumber;
        
        private class SectorDesc
        {
            public uint offset { get; set; }
            public int size { get; set; }
        }

        private Dictionary<uint, SectorDesc> SectorsDesc = new Dictionary<uint, SectorDesc>()
        {
            {  0, new SectorDesc { offset=0x0000000, size=0x4000} },
            {  1, new SectorDesc { offset=0x0004000, size=0x4000} },
            {  2, new SectorDesc { offset=0x0008000, size=0x4000} },
            {  3, new SectorDesc { offset=0x000C000, size=0x4000} },
            {  4, new SectorDesc { offset=0x0010000, size=0x10000} },
            {  5, new SectorDesc { offset=0x0020000, size=0x20000} },
            {  6, new SectorDesc { offset=0x0040000, size=0x20000} },
            {  7, new SectorDesc { offset=0x0060000, size=0x20000} },
            {  8, new SectorDesc { offset=0x0080000, size=0x20000} },
            {  9, new SectorDesc { offset=0x00A0000, size=0x20000} },
            { 10, new SectorDesc { offset=0x00C0000, size=0x20000} },
            { 11, new SectorDesc { offset=0x00E0000, size=0x20000} }
        };

        private enum Registers : long
        {
            FlashAccessControl = 0x00,
            FlashKey = 0x04,
            FlashOptionKey = 0x08,
            FlashStatus = 0x08,
            FlashControl = 0x10,
            FlashOptionControl = 0x14,
            FlashOptionControl1 = 0x18
        }
    }
}
