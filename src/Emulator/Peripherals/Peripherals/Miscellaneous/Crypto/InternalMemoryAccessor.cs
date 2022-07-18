//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class InternalMemoryAccessor
    {
        public InternalMemoryAccessor(uint size, string name, Endianness endianness)
        {
            this.endianness = endianness;
            internalMemory = new byte[size];
            Name = name;
        }

        public uint ReadDoubleWord(long offset)
        {
            if(offset < 0 || (offset + 4) >= internalMemory.Length)
            {
                Logger.Log(LogLevel.Error, "Trying to read outside of {0} internal memory, at offset 0x{1:X}", Name, offset);
                return 0;
            }
            var result = BitHelper.ToUInt32(internalMemory, (int)offset, 4, endianness == Endianness.LittleEndian);
            Logger.Log(LogLevel.Debug, "Read value 0x{0:X} from memory {1} at offset 0x{2:X}", result, Name, offset);
            return result;
        }

        public IEnumerable<byte> ReadBytes(long offset, int count, int endiannessSwapSize = 0)
        {
            if(offset < 0 || (offset + count) >= internalMemory.Length)
            {
                Logger.Log(LogLevel.Error, "Trying to read {0} bytes outside of {1} internal memory, at offset 0x{2:X}", count, Name, offset);
                yield return 0;
            }
            if(endiannessSwapSize != 0 && count % endiannessSwapSize != 0)
            {
                Logger.Log(LogLevel.Error, "Trying to read {0} bytes with an unaligned endianess swap group size of {1}", count, endiannessSwapSize);
                yield return 0;
            }
            
            if(endiannessSwapSize != 0)
            {
                for(var i = 0; i < count; i += endiannessSwapSize)
                {
                    for(var j = endiannessSwapSize - 1; j >= 0; j--)
                    {
                        yield return internalMemory[offset + i + j];
                    }
                }
            }
            else
            {
                for(var i = 0; i < count; ++i)
                {
                    yield return internalMemory[offset + i];
                }
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(offset < 0 || (offset + 4) >= internalMemory.Length)
            {
                Logger.Log(LogLevel.Error, "Trying to write value 0x{0:X} outside of {1} internal memory, at offset 0x{2:X}", value, Name, offset);
                return;
            }
            Logger.Log(LogLevel.Debug, "Writing value 0x{0:X} to memory {1} at offset 0x{2:X}", value, Name, offset);

            foreach(var b in BitHelper.GetBytesFromValue(value, sizeof(uint), false))
            {
                internalMemory[offset] = b;
                ++offset;
            }
        }

        public void WriteBytes(long offset, byte[] bytes)
        {
            if(offset < 0 || (offset + bytes.Length) >= internalMemory.Length)
            {
                Logger.Log(LogLevel.Error, "Trying to write {0] bytes outside of {1} internal memory, at offset 0x{2:X}", bytes.Length, Name, offset);
                return;
            }
            foreach(var b in bytes)
            {
                internalMemory[offset] = b;
                ++offset;
            }
        }

        public void Reset()
        {
            for(var i = 0; i < internalMemory.Length; ++i)
            {
                internalMemory[i] = 0;
            }
        }

        public string Name { get; }

        private readonly Endianness endianness;
        private readonly byte[] internalMemory;
    }
}
