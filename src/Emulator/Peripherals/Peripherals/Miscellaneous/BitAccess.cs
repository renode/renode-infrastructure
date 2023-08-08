//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class BitAccess : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral
    {
        public BitAccess(IMachine machine, ulong address, BitAccessMode mode)
        {
            sysbus = machine.GetSystemBus(this);
            this.address = address;
            switch(mode)
            {
                case BitAccessMode.Set:
                    operation = (register, mask) => (register | mask);
                    break;
                case BitAccessMode.Clear:
                    operation = (register, mask) => (register & ~mask);
                    break;
            }
        }

        public void Reset()
        {
            // intentionally left empty
        }

        public byte ReadByte(long offset)
        {
            this.Log(LogLevel.Warning, "Reading from BitAccess is not supported.");
            return 0x0;
        }

        public void WriteByte(long offset, byte mask)
        {
            sysbus.WriteByte(address + (ulong)offset, (byte)operation(sysbus.ReadByte(address + (ulong)offset), mask));
        }

        public ushort ReadWord(long offset)
        {
            this.Log(LogLevel.Warning, "Reading from BitAccess is not supported.");
            return 0;
        }

        public void WriteWord(long offset, ushort mask)
        {
            sysbus.WriteWord(address + (ulong)offset, (ushort)operation(sysbus.ReadWord(address + (ulong)offset), mask));
        }

        public uint ReadDoubleWord(long offset)
        {
            this.Log(LogLevel.Warning, "Reading from BitAccess is not supported.");
            return 0;
        }

        public void WriteDoubleWord(long offset, uint mask)
        {
            sysbus.WriteDoubleWord(address + (ulong)offset, (uint)operation(sysbus.ReadDoubleWord(address + (ulong)offset), mask));
        }

        private readonly IBusController sysbus;
        private readonly ulong address;
        private readonly Func<uint, uint, uint> operation;

        public enum BitAccessMode
        {
            Set,
            Clear,
        }
    }
}
