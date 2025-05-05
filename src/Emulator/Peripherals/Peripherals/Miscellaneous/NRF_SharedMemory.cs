//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class NRF_SharedMemory : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize
    {
        public NRF_SharedMemory()
        {
            this.memory = new byte[MemorySize];
        }

        public void Reset()
        {
            memory = new byte[MemorySize];
        }

        public byte ReadByte(long offset)
        {
            return memory[offset];
        }

        public void WriteByte(long offset, byte value)
        {
            memory[offset] = value;
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)BitHelper.ToUInt16(memory, (int)offset, true);
        }

        public void WriteWord(long offset, ushort value)
        {
            foreach(var b in BitHelper.GetBytesFromValue(value, 2, true))
            {
                memory[offset++] = b;
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return BitHelper.ToUInt32(memory, (int)offset, 4, true);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            foreach(var b in BitHelper.GetBytesFromValue(value, 4, true))
            {
                memory[offset++] = b;
            }
        }

        public long Size => 0x80;

        private byte[] memory;

        private const int MemorySize = 0x80;
    }
}
