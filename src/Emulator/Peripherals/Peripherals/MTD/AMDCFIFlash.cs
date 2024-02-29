//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Utilities;
using System;
using System.Linq;
using System.Collections.Generic;
using Endianess = ELFSharp.ELF.Endianess;
using static Antmicro.Renode.Utilities.BitHelper;
using System.IO;

namespace Antmicro.Renode.Peripherals.MTD
{
    public sealed class AMDCFIFlash : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize, IDisposable
    {
        public AMDCFIFlash(IMachine machine, MappedMemory underlyingMemory, int interleave = 4,
            ushort manufacturerId = 2, ushort alternateManufacturerId = 0, uint deviceId = 0x1234)
        {
            var sysbus = machine.GetSystemBus(this);
            // We need to consider the bus endianness for the reads that we pass through to the memory peripheral.
            if(sysbus.Endianess == Endianess.BigEndian)
            {
                readMemoryWord = underlyingMemory.ReadWordBigEndian;
                readMemoryDoubleWord = underlyingMemory.ReadDoubleWordBigEndian;
                writeMemoryWord = underlyingMemory.WriteWordBigEndian;
                writeMemoryDoubleWord = underlyingMemory.WriteDoubleWordBigEndian;
            }
            else
            {
                readMemoryWord = underlyingMemory.ReadWordBigEndian;
                readMemoryDoubleWord = underlyingMemory.ReadDoubleWordBigEndian;
                writeMemoryWord = underlyingMemory.WriteWordBigEndian;
                writeMemoryDoubleWord = underlyingMemory.WriteDoubleWordBigEndian;
            }
            this.underlyingMemory = underlyingMemory;
            this.interleave = interleave;
            this.manufacturerId = manufacturerId;
            this.alternateManufacturerId = alternateManufacturerId;
            this.deviceId = deviceId;
            bankSectorSizes = new List<List<long>>
            {
                Enumerable.Repeat(0x2000L, 8).Concat(Enumerable.Repeat(0x10000L, 15)).ToList(),
                Enumerable.Repeat(0x10000L, 48).ToList(),
                Enumerable.Repeat(0x10000L, 48).ToList(),
                Enumerable.Repeat(0x10000L, 15).Concat(Enumerable.Repeat(0x2000L, 8)).ToList(),
            };
            RecalculateSizes();
            Reset();
        }

        // Loading happens immediately, saving at dispose time.
        public void AddBackingFile(ReadFilePath path, long offset, int size, bool read = true, bool write = false)
        {
            if(read)
            {
                try
                {
                    if(size + offset > Size)
                    {
                        throw new ArgumentException($"Loaded size ({size} bytes) plus offset ({offset}) exceeds memory size ({Size} bytes)");
                    }
                    this.DebugLog("Loading {0} bytes at 0x{1:x} from {2}", size, offset, path);
                    byte[] bytes;
                    using(var stream = File.OpenRead(path))
                    {
                        using(var reader = new BinaryReader(stream))
                        {
                            bytes = reader.ReadBytes(size);
                        }
                    }
                    if(bytes.Length < size)
                    {
                        this.WarningLog("Wanted to read {0} bytes from '{1}', but got only {2}", size, path, bytes.Length);
                    }
                    underlyingMemory.WriteBytes(offset, bytes);
                }
                catch(Exception e)
                {
                    throw new RecoverableException($"Failed to load data from file: {e.Message}", e);
                }
            }

            if(write)
            {
                backingFiles.Add(offset, new BackingFile(size, path));
            }
        }

        // Used to define a custom flash layout in the repl/resc. The default one is equivalent to:
        // ClearSectorSizes
        // AddSectorSizes 0x2000 8 bank=0
        // AddSectorSizes 0x10000 15 bank=0
        // AddSectorSizes 0x10000 48 bank=1
        // AddSectorSizes 0x10000 48 bank=2
        // AddSectorSizes 0x10000 15 bank=3
        // AddSectorSizes 0x2000 8 bank=3
        // The banks must be in sequential order, and ClearSectorSizes should be used to clear the old layout first.
        public void ClearSectorSizes()
        {
            bankSectorSizes = new List<List<long>>();
        }

        public void AddSectorSizes(long size, int repeat, int bank)
        {
            var lastBank = bankSectorSizes.Count - 1;
            var entry = Enumerable.Repeat(size, repeat);
            if(bank != lastBank)
            {
                if(bank != lastBank + 1)
                {
                    throw new RecoverableException($"Banks must be added in sequential order (expected {lastBank + 1})");
                }
                bankSectorSizes.Add(entry.ToList());
            }
            else
            {
                bankSectorSizes[bank].AddRange(entry);
            }

            RecalculateSizes();
        }

        public void Reset()
        {
            state = State.ReadArray;
            unlockCycle = 0;
        }

        public void Dispose()
        {
            foreach(var fileOffset in backingFiles)
            {
                var offset = fileOffset.Key;
                var file = fileOffset.Value;
                this.DebugLog("Saving {0} bytes at 0x{1:x} to {2}", file.size, offset, file.path);
                try
                {
                    File.WriteAllBytes(file.path, underlyingMemory.ReadBytes(offset, file.size));
                }
                catch(Exception e)
                {
                    throw new RecoverableException($"Failed to save data to file: {e.Message}", e);
                }
            }
        }

        public byte ReadByte(long offset)
        {
            if(state == State.ReadArray)
            {
                return underlyingMemory.ReadByte(offset);
            }
            if(state == State.ReadQuery)
            {
                // No alignment requirement, in fact for example for interleave = 4 it's expected that
                // ReadByte(0x10..0x13) gives the same result - all the chips get the same value on their
                // address lines and so the CFI space looks like every byte is repeated 4 times
                return HandleQuery(offset / interleave);
            }
            if(state == State.ReadAutoselect)
            {
                // The same applies for the autoselect data
                return HandleAutoselect(offset / interleave);
            }

            this.WarningLog("ReadByte in unexpected state {0}", state);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            if(state == State.ProgramWord && Unlocked)
            {
                underlyingMemory.WriteByte(offset, value);
                state = State.ReadArray;
                unlockCycle = 0;
                return;
            }
            if(state == State.SectorEraseSetup && Unlocked)
            {
                var sectorIdx = Array.BinarySearch(sectorStarts, offset);
                if(sectorIdx < 0)
                {
                    this.WarningLog("Not erasing at unaligned address 0x{0:x}", offset);
                }
                else if(value != (byte)Command.SectorErase)
                {
                    this.WarningLog("Unexpected byte written in ready-to-erase state: 0x{0:x2}", value);
                }
                else
                {
                    var sectorSize = sectorSizes[sectorIdx];
                    underlyingMemory.SetRange(offset, sectorSize, ErasedValue);
                    this.DebugLog("Erased {0}-byte sector at 0x{1:x}", sectorSize, offset);
                }
                state = State.ReadArray;
                unlockCycle = 0;
                return;
            }

            this.DebugLog("Handling command {0} (0x{1:x})", (Command)value, value);
            switch((Command)value)
            {
                case Command.ReadQuery:
                    state = State.ReadQuery;
                    break;
                case Command.ReadAutoselect:
                    state = State.ReadAutoselect;
                    break;
                case Command.Reset:
                case Command.ReadArray:
                    state = State.ReadArray;
                    break;
                case Command.UnlockCycle1:
                    unlockCycle = unlockCycle == 0 ? 1 : 0;
                    break;
                case Command.UnlockCycle2:
                    unlockCycle = unlockCycle == 1 ? 2 : 0;
                    break;
                case Command.ProgramWord:
                    state = Unlocked ? State.ProgramWord : State.ReadArray;
                    break;
                case Command.SectorEraseSetup:
                    // To allow setting up an erase operation, the device needs to be unlocked,
                    if(Unlocked)
                    {
                        state = State.SectorEraseSetup;
                        // and another unlock sequence will be required to actually perform the erase.
                        unlockCycle = 0;
                    }
                    break;
                default:
                    this.WarningLog("Unknown command 0x{0:x}", value);
                    break;
            }
        }

        public ushort ReadWord(long offset)
        {
            if(state == State.ReadArray)
            {
                return readMemoryWord(offset);
            }
            return this.ReadWordUsingByte(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            if(state == State.ProgramWord && Unlocked)
            {
                writeMemoryWord(offset, value);
                state = State.ReadArray;
                unlockCycle = 0;
                return;
            }
            this.WriteWordUsingByte(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(state == State.ReadArray)
            {
                return readMemoryDoubleWord(offset);
            }
            return this.ReadDoubleWordUsingByte(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(state == State.ProgramWord && Unlocked)
            {
                writeMemoryDoubleWord(offset, value);
                state = State.ReadArray;
                unlockCycle = 0;
                return;
            }
            this.WriteDoubleWordUsingByte(offset, value);
        }

        public long Size => underlyingMemory.Size;

        private byte[] PrepareCfiData()
        {
            return new VariableLengthValue(0x5c * 8)
                .DefineFragment(0x10 * 8, 8, (byte)'Q')
                .DefineFragment(0x11 * 8, 8, (byte)'R')
                .DefineFragment(0x12 * 8, 8, (byte)'Y')
                .DefineFragment(0x13 * 8, 16, manufacturerId, name: "Manufacturer ID")
                .DefineFragment(0x15 * 8, 16, 0x0040, name: "Starting Address for the Primary Vendor-Specific Extended Query Table")
                .DefineFragment(0x17 * 8, 16, alternateManufacturerId, name: "Alternate manufacturer ID")
                .DefineFragment(0x19 * 8, 16, 0x0000, name: "Starting Address for the Alternate Vendor-Specific Extended Query Table")
                .DefineFragment(0x1B * 8, 8, 0x27, name: "V_cc lower limit")
                .DefineFragment(0x1C * 8, 8, 0x36, name: "V_cc upper limit")
                .DefineFragment(0x1D * 8, 8, 0x00, name: "V_pp lower limit")
                .DefineFragment(0x1E * 8, 8, 0x00, name: "V_pp upper limit")
                .DefineFragment(0x1F * 8, 8, 0x03, name: "Typical word programming time")
                .DefineFragment(0x20 * 8, 8, 0x00, name: "Typical buffer programming time")
                .DefineFragment(0x21 * 8, 8, 0x09, name: "Typical sector erase time")
                .DefineFragment(0x22 * 8, 8, 0x0F, name: "Typical chip erase time")
                .DefineFragment(0x23 * 8, 8, 0x04, name: "Maximum word programming time")
                .DefineFragment(0x24 * 8, 8, 0x00, name: "Maximum buffer programming time")
                .DefineFragment(0x25 * 8, 8, 0x04, name: "Maximum sector erase time")
                .DefineFragment(0x26 * 8, 8, 0x00, name: "Maximum chip erase time")
                .DefineFragment(0x27 * 8, 8, (ulong)Math.Log(Size, 2), name: "Base-2 logarithm of device size")
                .DefineFragment(0x28 * 8, 8, 0x0002, name: "Flash Device Interface Code description")
                .DefineFragment(0x2A * 8, 16, 0x0000, name: "Base-2 logarithm of the maximum number of bytes in a multi-byte program")
                .DefineFragment(0x2C * 8, 8, (ulong)sectorRegions.Length, name: "Number of erase sector regions within device")
                .DefineFragment(0x2D * 8, 16, (sectorRegions.ElementAtOrDefault(0)?.count ?? 1) - 1, name: "Number of sectors in region 1")
                .DefineFragment(0x2F * 8, 16, (sectorRegions.ElementAtOrDefault(0)?.size ?? 0UL) / 256, name: "Size of sector in region 1")
                .DefineFragment(0x31 * 8, 16, (sectorRegions.ElementAtOrDefault(1)?.count ?? 1) - 1, name: "Number of sectors in region 2")
                .DefineFragment(0x33 * 8, 16, (sectorRegions.ElementAtOrDefault(1)?.size ?? 0UL) / 256, name: "Size of sector in region 2")
                .DefineFragment(0x35 * 8, 16, (sectorRegions.ElementAtOrDefault(2)?.count ?? 1) - 1, name: "Number of sectors in region 3")
                .DefineFragment(0x37 * 8, 16, (sectorRegions.ElementAtOrDefault(2)?.size ?? 0UL) / 256, name: "Size of sector in region 3")
                .DefineFragment(0x39 * 8, 16, (sectorRegions.ElementAtOrDefault(3)?.count ?? 1) - 1, name: "Number of sectors in region 4")
                .DefineFragment(0x3B * 8, 16, (sectorRegions.ElementAtOrDefault(3)?.size ?? 0UL) / 256, name: "Size of sector in region 4")
                .DefineFragment(0x40 * 8, 8, (byte)'P')
                .DefineFragment(0x41 * 8, 8, (byte)'R')
                .DefineFragment(0x42 * 8, 8, (byte)'I')
                .DefineFragment(0x43 * 8, 8, (byte)'1', name: "Major version number, ASCII (reflects modifications to the silicon)")
                .DefineFragment(0x44 * 8, 8, (byte)'3', name: "Major version number, ASCII (reflects modifications to the CFI table)")
                .DefineFragment(0x45 * 8, 2, 0b00, name: "Address sensitive unlock")
                .DefineFragment(0x45 * 8 + 2, 6, 0b110000, name: "Process technology")
                .DefineFragment(0x46 * 8, 8, 0x02, name: "Erase suspend")
                .DefineFragment(0x47 * 8, 8, 0x01, name: "Sector protect")
                .DefineFragment(0x48 * 8, 8, 0x01, name: "Temporary sector unprotect")
                .DefineFragment(0x49 * 8, 8, 0x04, name: "Sector protection scheme")
                .DefineFragment(0x4A * 8, 8, 0x07, name: "Simultaneous operation")
                .DefineFragment(0x4B * 8, 8, 0x00, name: "Burst mode type")
                .DefineFragment(0x4C * 8, 8, 0x00, name: "Page mode type")
                .DefineFragment(0x4D * 8, 8, 0x00, name: "Acceleration power supply voltage lower limit")
                .DefineFragment(0x4E * 8, 8, 0x00, name: "Acceleration power supply voltage lower limit")
                .DefineFragment(0x4F * 8, 8, 0x01, name: "Sector layout (uniform / 8x8 KiB top/bottom)")
                .DefineFragment(0x50 * 8, 8, 0x00, name: "Program suspend")
                .DefineFragment(0x57 * 8, 8, (ulong)bankSectorSizes.Count, name: "Number of banks")
                .DefineFragment(0x58 * 8, 8, (ulong)(bankSectorSizes.ElementAtOrDefault(0)?.Count ?? 0), name: "Number of sectors in bank 1")
                .DefineFragment(0x59 * 8, 8, (ulong)(bankSectorSizes.ElementAtOrDefault(1)?.Count ?? 0), name: "Number of sectors in bank 2")
                .DefineFragment(0x5A * 8, 8, (ulong)(bankSectorSizes.ElementAtOrDefault(2)?.Count ?? 0), name: "Number of sectors in bank 3")
                .DefineFragment(0x5B * 8, 8, (ulong)(bankSectorSizes.ElementAtOrDefault(3)?.Count ?? 0), name: "Number of sectors in bank 4")
                .Bits.AsByteArray();
        }

        private byte[] PrepareAutoselectData()
        {
            return new VariableLengthValue(0x1f * 8)
                .DefineFragment(0x00 * 8, 8, (ulong)(manufacturerId & 0xff), name: "Manufacturer ID")
                .DefineFragment(0x02 * 8, 8, (deviceId >> 16) & 0xff, name: "Device ID 1")
                .DefineFragment(0x1c * 8, 8, (deviceId >> 8) & 0xff, name: "Device ID 2")
                .DefineFragment(0x1e * 8, 8, deviceId & 0xff, name: "Device ID 3")
                .Bits.AsByteArray();
        }

        private byte HandleQuery(long offset)
        {
            // This division is to use sequential (byte mode) offsets in the data definition
            var byteOffset = offset / 2;
            if(byteOffset >= cfiData.Length)
            {
                this.WarningLog("Unhandled query read at offset 0x{0:x}, returning 0", offset);
                return 0;
            }
            return cfiData[byteOffset];
        }

        private byte HandleAutoselect(long offset)
        {
            if(offset >= autoselectData.Length)
            {
                this.WarningLog("Unhandled autoselect read at offset 0x{0:x}, returning 0", offset);
                return 0;
            }
            return cfiData[offset];
        }

        private void RecalculateSizes()
        {
            sectorSizes = bankSectorSizes.SelectMany(bankSizes => bankSizes.Select(s => s * interleave)).ToArray();
            sectorStarts = Enumerable.Concat(new [] { 0L }, Misc.Prefix(sectorSizes, (a, b) => a + b)).ToArray();
            // A sector region is a run of contiguous sectors with the same size, including across banks.
            // We divide the sizes in here by the number of chips in parallel as they are used in CFI data, and that of
            // course needs to contain the block size for *one* chip.
            sectorRegions = sectorSizes.Aggregate(new List<SectorRegion>(), (regions, size) =>
            {
                if(regions.LastOrDefault()?.size == (ulong)(size / interleave))
                {
                    regions.Last().count++;
                }
                else
                {
                    regions.Add(new SectorRegion((ulong)(size / interleave), 1));
                }
                return regions;
            }).ToArray();

            cfiData = PrepareCfiData();
            autoselectData = PrepareAutoselectData();
        }

        private bool Unlocked => unlockCycle == 2;

        private List<List<long>> bankSectorSizes; // For one chip
        private long[] sectorSizes; // For all chips in parallel
        private long[] sectorStarts; // For all chips in parallel
        private SectorRegion[] sectorRegions; // For one chip
        private byte[] cfiData;
        private byte[] autoselectData;
        private State state;
        private int unlockCycle;

        private readonly MappedMemory underlyingMemory;
        private readonly int interleave;
        private readonly ushort manufacturerId;
        private readonly ushort alternateManufacturerId;
        private readonly uint deviceId;
        private readonly Func<long, ushort> readMemoryWord;
        private readonly Func<long, uint> readMemoryDoubleWord;
        private readonly Action<long, ushort> writeMemoryWord;
        private readonly Action<long, uint> writeMemoryDoubleWord;
        private readonly Dictionary<long, BackingFile> backingFiles = new Dictionary<long, BackingFile>();

        private const byte ErasedValue = 0xFF;

        private class SectorRegion
        {
            public SectorRegion(ulong size, ulong count)
            {
                this.size = size;
                this.count = count;
            }

            public readonly ulong size;
            public ulong count;
        }

        private class BackingFile
        {
            public BackingFile(int size, FilePath path)
            {
                this.size = size;
                this.path = path;
            }

            public readonly int size;
            public readonly FilePath path;
        }

        private enum Command : byte
        {
            Reset = 0xF0,
            ReadArray = 0xFF,
            ReadQuery = 0x98,
            ReadAutoselect = 0x90,
            UnlockCycle1 = 0xAA,
            UnlockCycle2 = 0x55,
            SectorEraseSetup = 0x80,
            SectorErase = 0x30,
            ProgramWord = 0xA0,
        }

        // Not currently used as the unlock mechanism is not address sensitive
        private enum Registers : long
        {
            UnlockCycle1 = 0xAAA,
            UnlockCycle2 = 0x555,
        }

        private enum State
        {
            ReadArray,
            ReadQuery,
            ReadAutoselect,
            ProgramWord,
            SectorEraseSetup,
        }
    }
}

