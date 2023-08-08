/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public sealed class BitBanding : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral
    {
        public BitBanding(IMachine machine, ulong peripheralBase)
        {
            sysbus = machine.GetSystemBus(this);
            this.peripheralBase = peripheralBase;
        }

        public void Reset()
        {
            // nothing happens
        }

        public byte ReadByte(long offset)
        {
            var realAddress = GetBitBandAddress(offset) & ~0UL;
            var readValue = sysbus.ReadByte(realAddress);
            var bitNumber = (int)(offset >> 2) & 7;
            return (byte)((readValue >> bitNumber) & 1);
        }

        public void WriteByte(long offset, byte value)
        {
            var realAddress = GetBitBandAddress(offset) & ~0UL;
            var readValue = sysbus.ReadByte(realAddress);
            var bitNumber = (int)(offset >> 2) & 7;
            var mask = (1 << bitNumber);
            if((value & 1) == 1)
            {
                readValue |= (byte)mask;
            }
            else
            {
                readValue &= (byte)~mask;
            }
            sysbus.WriteByte(realAddress, readValue);
        }

        public ushort ReadWord(long offset)
        {
            var realAddress = GetBitBandAddress(offset) & ~1UL;
            var readValue = sysbus.ReadWord(realAddress);
            var bitNumber = (int)(offset >> 2) & 15;
            return (ushort)((readValue >> bitNumber) & 1);
        }

        public void WriteWord(long offset, ushort value)
        {
            var realAddress = GetBitBandAddress(offset) & ~1UL;
            var readValue = sysbus.ReadWord(realAddress);
            var bitNumber = (int)(offset >> 2) & 15;
            var mask = (1 << bitNumber);
            if((value & 1) == 1)
            {
                readValue |= (ushort)mask;
            }
            else
            {
                readValue &= (ushort)~mask;
            }
            sysbus.WriteWord(realAddress, readValue);
        }

        public uint ReadDoubleWord(long offset)
        {
            var realAddress = GetBitBandAddress(offset) & ~3UL;
            var readValue = sysbus.ReadDoubleWord(realAddress);
            var bitNumber = (int)(offset >> 2) & 31;
            return (uint)((readValue >> bitNumber) & 1);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            var realAddress = GetBitBandAddress(offset) & ~3UL;
            var readValue = sysbus.ReadDoubleWord(realAddress);
            var bitNumber = (int)(offset >> 2) & 31;
            var mask = (1 << bitNumber);
            if((value & 1) == 1)
            {
                readValue |= (uint)mask;
            }
            else
            {
                readValue &= (uint)~mask;
            }
            sysbus.WriteDoubleWord(realAddress, readValue);
        }

        private ulong GetBitBandAddress(long from)
        {
            var retval = peripheralBase + checked((ulong)(from >> 5));
            this.NoisyLog("Bit-band operation: 0x{0:X} -> 0x{1:X}.", from, retval);
            return retval;
        }

        private readonly IBusController sysbus;
        private readonly ulong peripheralBase;
    }
}
