//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Memory
{
    public class ArrayMemoryWithReadonlys : ArrayMemory
    {
        public ArrayMemoryWithReadonlys(ulong size):base(size)
        {
        }
        public ArrayMemoryWithReadonlys(byte[] source):base(source)
        {
        }

        public void SetReadOnlyDoubleWord(long offset, uint value)
        {
            WriteDoubleWord(offset, value);
            ignoreWrites.Add(offset);
        }

        public void SetReadOnlyWord(long offset, ushort value)
        {
            WriteWord(offset, value);
            ignoreWrites.Add(offset);
        }

        public void SetReadOnlyByte(long offset, byte value)
        {
            WriteByte(offset, value);
            ignoreWrites.Add(offset);
        }
        
        public override void WriteDoubleWord(long offset, uint value)
        {           
            if(!ignoreWrites.Contains(offset))
            {
                var bytes = BitConverter.GetBytes(value);
                bytes.CopyTo(array, offset);
            }
        }
        public override void WriteWord(long offset, ushort value)
        {
            if(!ignoreWrites.Contains(offset))
            {
                var bytes = BitConverter.GetBytes(value);
                bytes.CopyTo(array, offset);
            }
        }
        public override void WriteByte(long offset, byte value)
        { 
            if(!ignoreWrites.Contains(offset))
            {
                var intOffset = (int)offset;
                array[intOffset] = value;
            }
        }
        
        private HashSet<long> ignoreWrites = new HashSet<long>();
    }
}

