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

        public ArrayMemory(int size)
        {
            if(size <= 0)
            {
                throw new ConstructionException($"Memory size should be positive, but tried to configure it to: {size}");
            }
            array = new byte[size];
        }

        public virtual ulong ReadQuadWord(long offset)
        {
            if(!IsCorrectOffset(offset, sizeof(ulong)))
            {
                LogOffsetError(offset);
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
                LogOffsetError(offset);
                return;
            }
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(array, offset);
        }

        public uint ReadDoubleWord(long offset)
        {
            if(!IsCorrectOffset(offset, sizeof(uint)))
            {
                LogOffsetError(offset);
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
                LogOffsetError(offset);
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
                LogOffsetError(offset);
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
                LogOffsetError(offset);
                return;
            }
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(array, offset);
        }

        public byte ReadByte(long offset)
        {
            if(!IsCorrectOffset(offset, sizeof(byte)))
            {
                LogOffsetError(offset);
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
                LogOffsetError(offset);
                return;
            }
            var intOffset = (int)offset;
            array[intOffset] = value;
        }

        public byte[] ReadBytes(long offset, int count, ICPU context = null)
        {
            if(!IsCorrectOffset(offset, count))
            {
                LogOffsetError(offset);
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
                LogOffsetError(offset);
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
            return offset >= 0 && offset <= array.Length - size;
        }

        private void LogOffsetError(long offset)
        {
            this.Log(LogLevel.Error, "Tried to read byte at offset 0x{0:X} outside the range of the peripheral 0x0 - 0x{1:X}", offset, array.Length - 1);
        }
    }
}
