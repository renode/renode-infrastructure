//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Exceptions;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.Memory
{
    public class ArrayMemory : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize, IMemory, IMultibyteWritePeripheral, IQuadWordPeripheral, ICanLoadFiles, IEndiannessAware
    {
        public ArrayMemory(byte[] source)
        {
            array = source;
        }

        public ArrayMemory(ulong size)
        {
            if(size > MaxSize)
            {
                throw new ConstructionException($"Memory size cannot be larger than 0x{MaxSize:X}, requested: 0x{size:X}");
            }
            array = new byte[size];
        }

        public virtual ulong ReadQuadWord(long offset)
        {
            if(!IsCorrectOffset(offset, sizeof(ulong)))
            {
                return 0;
            }
            var intOffset = (int)offset;
            var result = BitConverter.ToUInt64(array, intOffset);
            return result;
        }

        public virtual void WriteQuadWord(long offset, ulong value)
        {
            if(!IsCorrectOffset(offset, sizeof(ulong)))
            {
                return;
            }
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(array, offset);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!IsCorrectOffset(offset, sizeof(uint)))
            {
                return 0;
            }
            var intOffset = (int)offset;
            var result = BitConverter.ToUInt32(array, intOffset);
            return result;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            if(!IsCorrectOffset(offset, sizeof(uint)))
            {
                return;
            }
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(array, offset);
        }

        public void Reset()
        {
            // nothing happens
        }

        public ushort ReadWord(long offset)
        {
            if(!IsCorrectOffset(offset, sizeof(ushort)))
            {
                return 0;
            }
            var intOffset = (int)offset;
            var result = BitConverter.ToUInt16(array, intOffset);
            return result;
        }

        public virtual void WriteWord(long offset, ushort value)
        {
            if(!IsCorrectOffset(offset, sizeof(ushort)))
            {
                return;
            }
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(array, offset);
        }

        public byte ReadByte(long offset)
        {
            if(!IsCorrectOffset(offset, sizeof(byte)))
            {
                return 0;
            }
            var intOffset = (int)offset;
            var result = array[intOffset];
            return result;
        }

        public virtual void WriteByte(long offset, byte value)
        {
            if(!IsCorrectOffset(offset, sizeof(byte)))
            {
                return;
            }
            var intOffset = (int)offset;
            array[intOffset] = value;
        }

        public byte[] ReadBytes(long offset, int count, ICPU context = null)
        {
            if(!IsCorrectOffset(offset, count))
            {
                return new byte[count];
            }
            var result = new byte[count];
            Array.Copy(array, offset, result, 0, count);
            return result;
        }

        public void WriteBytes(long offset, byte[] bytes, int startingIndex, int count, ICPU context = null)
        {
            if(!IsCorrectOffset(offset, count))
            {
                return;
            }
            Array.Copy(bytes, startingIndex, array, offset, count);
        }

        public void LoadFileChunks(string path, IEnumerable<FileChunk> chunks, ICPU cpu)
        {
            this.LoadFileChunks(chunks, cpu);
        }

        public long Size
        {
            get
            {
                return array.Length;
            }
        }

        // ArrayMemory matches the host endianness because host-endian BitConverter operations are used for
        // accesses wider than a byte.
        public Endianess Endianness => BitConverter.IsLittleEndian ? Endianess.LittleEndian : Endianess.BigEndian;

        protected readonly byte[] array;

        private bool IsCorrectOffset(long offset, int size)
        {
            var result = offset >= 0 && offset <= array.Length - size;
            if(!result)
            {
                this.Log(LogLevel.Error, "Tried to read {0} byte(s) at offset 0x{1:X} outside the range of the peripheral 0x0 - 0x{2:X}", size, offset, array.Length - 1);
            }
            return result;
        }

        // Objects bigger than 2GB are supported in .NET Framework with `gcAllowVeryLargeObjects`
        // enabled and in .NET by default but there can be no more elements than that in a single
        // dimension of an array. We could, e.g., double it by using more dimensions but generally
        // ArrayMemory is mostly intended to be used for memory smaller than page size, which is
        // required by MappedMemory, so this is much more than should be needed for ArrayMemory.
        private const ulong MaxSize = 0x7FFFFFC7;
    }
}
