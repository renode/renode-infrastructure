//
// Copyright (c) 2010-2019 Antmicro
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

namespace Antmicro.Renode.Peripherals.Memory
{
    public class ArrayMemory : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize, IMemory, IMultibyteWritePeripheral
    {		
        public ArrayMemory(byte[] source)
        {
            array = source;
        }

        public ArrayMemory(int size)
        {
            array = new byte[size];
        }

      
        public uint ReadDoubleWord(long offset)
        {
            var intOffset = (int)offset;
            var result = BitConverter.ToUInt32(array, intOffset);
            return result;
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {		
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(array, offset);

        }

        public void Reset()
        {
            // nothing happens
        }

        public ushort ReadWord(long offset)
        {
            var intOffset = (int)offset;
            var result = BitConverter.ToUInt16(array, intOffset);
            return result;
        }

        public virtual void WriteWord(long offset, ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            bytes.CopyTo(array, offset);
        }

        public byte ReadByte(long offset)
        {
            var intOffset = (int)offset;
            var result = array[intOffset];
            return result;
        }

        public virtual void WriteByte(long offset, byte value)
        { 
            var intOffset = (int)offset;
            array[intOffset] = value;
        }

        public byte[] ReadBytes(long offset, int count)
        {
            var result = new byte[count];
            Array.Copy(array, offset, result, 0, count);
            return result;
        }

        public void WriteBytes(long offset, byte[] bytes, int startingIndex, int count)
        {
            Array.Copy(bytes, startingIndex, array, offset, count);
        }

        public long Size
        {
            get
            {
                return array.Length;
            }
        }

        protected byte[] array;
    }
}

