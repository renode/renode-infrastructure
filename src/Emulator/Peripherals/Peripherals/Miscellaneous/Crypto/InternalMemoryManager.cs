//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class InternalMemoryManager
    {
        public InternalMemoryManager()
        {
            coreMemories = new Dictionary<long, InternalMemoryAccessor>
            {
                { 0x0, new InternalMemoryAccessor(BERLength, "BER_BE", Endianness.BigEndian) },
                { 0x1, new InternalMemoryAccessor(MMRLength, "MMR_BE", Endianness.BigEndian) },
                { 0x2, new InternalMemoryAccessor(TSRLength, "TSR_BE", Endianness.BigEndian) },
                { 0x3, new InternalMemoryAccessor(FPRLength, "FPR_BE", Endianness.BigEndian) },
                { 0x8, new InternalMemoryAccessor(BERLength, "BER_LE", Endianness.LittleEndian) },
                { 0x9, new InternalMemoryAccessor(MMRLength, "MMR_LE", Endianness.LittleEndian) },
                { 0xA, new InternalMemoryAccessor(TSRLength, "TSR_LE", Endianness.LittleEndian) },
                { 0xB, new InternalMemoryAccessor(FPRLength, "FPR_LE", Endianness.LittleEndian) }
            };
        }

        public void ResetMemories()
        {
            foreach(var memory in coreMemories)
            {
                memory.Value.Reset();
            }
        }

        public bool TryReadDoubleWord(long offset, out uint result)
        {
            if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
            {
                result = 0;
                return false;
            }
            result = mem.ReadDoubleWord(internalOffset);
            return true;
        }

        public bool TryReadBytes(long offset, int count, out byte[] result)
        {
            if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
            {
                result = new byte[0];
                return false;
            }

            result = mem.ReadBytes(internalOffset, count).ToArray();
            return true;
        }

        public bool TryWriteDoubleWord(long offset, uint value)
        {
            if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
            {
                return false;
            }
            mem.WriteDoubleWord(internalOffset, value);
            return true;
        }

        public bool TryWriteBytes(long offset, byte[] bytes)
        {
            if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
            {
                return false;
            }
            mem.WriteBytes(internalOffset, bytes);
            return true;
        }

        private bool TryAddressInternalMemory(long offset, out InternalMemoryAccessor mem, out long internalMemoryOffset)
        {
            var offsetMask = offset >> OffsetShift;
            if(!coreMemories.TryGetValue(offsetMask, out mem))
            {
                internalMemoryOffset = 0;
                Logger.Log(LogLevel.Noisy, "Could not write to internal memory at address 0x{0:X}", offset);
                return false;
            }
            internalMemoryOffset = offset - (offsetMask << OffsetShift);
            return true;
        }

        private readonly Dictionary<long, InternalMemoryAccessor> coreMemories;

        private const int BERLength = 0x1000;
        private const int MMRLength = 0x1000;
        private const int TSRLength = 0x1000;
        private const int FPRLength = 0x1000;
        private const int OffsetShift = 12;
    }
}
