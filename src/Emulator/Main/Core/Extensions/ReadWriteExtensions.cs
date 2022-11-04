//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using System.Text;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Extensions
{
    public static class ReadWriteExtensions
    {
        public static ushort ReadWordUsingByte(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ushort)(peripheral.ReadByte(address + 1) << 8 | peripheral.ReadByte(address));
            }
        }

        public static ushort ReadWordUsingByteBigEndian(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ushort)(peripheral.ReadByte(address + 1) | peripheral.ReadByte(address) << 8);
            }
        }

        public static void WriteWordUsingByte(this IBytePeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 1, (byte)(value >> 8));
                peripheral.WriteByte(address, (byte)value);
            }
        }

        public static void WriteWordUsingByteBigEndian(this IBytePeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                peripheral.WriteByte(address, (byte)(value >> 8));
                peripheral.WriteByte(address + 1, (byte)value);
            }
        }

        public static uint ReadDoubleWordUsingByte(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)
                    (peripheral.ReadByte(address + 3) << 24 | peripheral.ReadByte(address + 2) << 16
                     | peripheral.ReadByte(address + 1) << 8 | peripheral.ReadByte(address));
            }
        }

        public static uint ReadDoubleWordUsingByteBigEndian(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)
                    (peripheral.ReadByte(address + 3) | peripheral.ReadByte(address + 2) << 8
                     | peripheral.ReadByte(address + 1) << 16 | peripheral.ReadByte(address) << 24);
            }
        }

        public static void WriteDoubleWordUsingByte(this IBytePeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 3, (byte)(value >> 24));
                peripheral.WriteByte(address + 2, (byte)(value >> 16));
                peripheral.WriteByte(address + 1, (byte)(value >> 8));
                peripheral.WriteByte(address, (byte)value);
            }
        }

        public static void WriteDoubleWordUsingByteBigEndian(this IBytePeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteByte(address, (byte)(value >> 24));
                peripheral.WriteByte(address + 1, (byte)(value >> 16));
                peripheral.WriteByte(address + 2, (byte)(value >> 8));
                peripheral.WriteByte(address + 3, (byte)value);
            }
        }

        public static ulong ReadQuadWordUsingByte(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)
                    (peripheral.ReadByte(address + 7) << 56 | peripheral.ReadByte(address + 6) << 48
                     | peripheral.ReadByte(address + 5) << 40 | peripheral.ReadByte(address + 4) << 32
                     | peripheral.ReadByte(address + 3) << 24 | peripheral.ReadByte(address + 2) << 16
                     | peripheral.ReadByte(address + 1) << 8 | peripheral.ReadByte(address));
            }
        }

        public static ulong ReadQuadWordUsingByteBigEndian(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)
                    (peripheral.ReadByte(address + 7) | peripheral.ReadByte(address + 6) << 8
                     | peripheral.ReadByte(address + 5) << 16 | peripheral.ReadByte(address + 4) << 24
                     | peripheral.ReadByte(address + 3) << 32 | peripheral.ReadByte(address + 2) << 40
                     | peripheral.ReadByte(address + 1) << 48 | peripheral.ReadByte(address) << 56);
            }
        }

        public static void WriteQuadWordUsingByte(this IBytePeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 7, (byte)(value >> 56));
                peripheral.WriteByte(address + 6, (byte)(value >> 48));
                peripheral.WriteByte(address + 5, (byte)(value >> 40));
                peripheral.WriteByte(address + 4, (byte)(value >> 32));
                peripheral.WriteByte(address + 3, (byte)(value >> 24));
                peripheral.WriteByte(address + 2, (byte)(value >> 16));
                peripheral.WriteByte(address + 1, (byte)(value >> 8));
                peripheral.WriteByte(address, (byte)value);
            }
        }

        public static void WriteQuadWordUsingByteBigEndian(this IBytePeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteByte(address, (byte)(value >> 56));
                peripheral.WriteByte(address + 1, (byte)(value >> 48));
                peripheral.WriteByte(address + 2, (byte)(value >> 40));
                peripheral.WriteByte(address + 3, (byte)(value >> 32));
                peripheral.WriteByte(address + 4, (byte)(value >> 24));
                peripheral.WriteByte(address + 5, (byte)(value >> 16));
                peripheral.WriteByte(address + 6, (byte)(value >> 8));
                peripheral.WriteByte(address + 7, (byte)value);
            }
        }

        public static byte ReadByteUsingWord(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~1);
                var offset = (int)(address & 1);
                return (byte)(peripheral.ReadWord(readAddress) >> 8 * offset);
            }
        }

        public static byte ReadByteUsingWordBigEndian(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~1);
                var offset = 1 - (int)(address & 1);
                return (byte)(peripheral.ReadWord(readAddress) >> 8 * offset);
            }
        }

        public static void WriteByteUsingWord(this IWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~1);
                var offset = (int)(address & 1);
                var oldValue = peripheral.ReadWord(writeAddress) & (0xFF << (1 - offset) * 8);
                peripheral.WriteWord(writeAddress, (ushort)(oldValue | (value << 8 * offset)));
            }
        }

        public static void WriteByteUsingWordBigEndian(this IWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~1);
                var offset = 1 - (int)(address & 1);
                var oldValue = peripheral.ReadWord(writeAddress) & (0xFF << (1 - offset) * 8);
                peripheral.WriteWord(writeAddress, (ushort)(oldValue | (value << 8 * offset)));
            }
        }

        public static byte ReadByteUsingDword(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = (int)(address & 3);
                return (byte)(peripheral.ReadDoubleWord(readAddress) >> offset * 8);
            }
        }

        public static byte ReadByteUsingDwordBigEndian(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = 3 - (int)(address & 3);
                return (byte)(peripheral.ReadDoubleWord(readAddress) >> offset * 8);
            }
        }

        public static void WriteByteUsingDword(this IDoubleWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~3);
                var offset = (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(writeAddress) & ~(0xFF << offset * 8);
                peripheral.WriteDoubleWord(writeAddress, (uint)(oldValue | (uint)(value << 8 * offset)));
            }
        }

        public static void WriteByteUsingDwordBigEndian(this IDoubleWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~3);
                var offset = 3 - (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(writeAddress) & ~(0xFF << offset * 8);
                peripheral.WriteDoubleWord(writeAddress, (uint)(oldValue | (uint)(value << 8 * offset)));
            }
        }

        public static byte ReadByteUsingQword(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = (int)(address & 7);
                return (byte)(peripheral.ReadQuadWord(readAddress) >> offset * 8);
            }
        }

        public static byte ReadByteUsingQwordBigEndian(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = 7 - (int)(address & 7);
                return (byte)(peripheral.ReadQuadWord(readAddress) >> offset * 8);
            }
        }

        public static void WriteByteUsingQword(this IQuadWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~7);
                var offset = (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~(0xFFul << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static void WriteByteUsingQwordBigEndian(this IQuadWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~7);
                var offset = 7 - (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~(0xFFul << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static ushort ReadWordUsingDword(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = (int)(address & 3);
                return (ushort)(peripheral.ReadDoubleWord(readAddress) >> offset * 8);
            }
        }

        public static ushort ReadWordUsingDwordBigEndian(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = 2 - (int)(address & 3);
                return Misc.SwapBytesUShort((ushort)(peripheral.ReadDoubleWord(readAddress) >> offset * 8));
            }
        }

        public static void WriteWordUsingDword(this IDoubleWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                var alignedAddress = address & (~3);
                var offset = (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(alignedAddress) & ~(0xFFFF << offset * 8);
                peripheral.WriteDoubleWord(alignedAddress, (uint)(oldValue | (uint)(value << 8 * offset)));
            }
        }

        public static void WriteWordUsingDwordBigEndian(this IDoubleWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                value = Misc.SwapBytesUShort(value);
                var alignedAddress = address & (~3);
                var offset = 2 - (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(alignedAddress) & ~(0xFFFF << offset * 8);
                peripheral.WriteDoubleWord(alignedAddress, (uint)(oldValue | (uint)(value << 8 * offset)));
            }
        }

        public static ushort ReadWordUsingQword(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = (int)(address & 7);
                return (ushort)(peripheral.ReadQuadWord(readAddress) >> offset * 8);
            }
        }

        public static ushort ReadWordUsingQwordBigEndian(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = 6 - (int)(address & 7);
                return Misc.SwapBytesUShort((ushort)(peripheral.ReadQuadWord(readAddress) >> offset * 8));
            }
        }

        public static void WriteWordUsingQword(this IQuadWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                var alignedAddress = address & (~7);
                var offset = (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(alignedAddress) & ~(0xFFFFul << offset * 8);
                peripheral.WriteQuadWord(alignedAddress, (uint)(oldValue | (uint)(value << 8 * offset)));
            }
        }

        public static void WriteWordUsingQwordBigEndian(this IQuadWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                value = Misc.SwapBytesUShort(value);
                var alignedAddress = address & (~7);
                var offset = 6 - (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(alignedAddress) & ~(0xFFFFul << offset * 8);
                peripheral.WriteQuadWord(alignedAddress, (uint)(oldValue | (uint)(value << 8 * offset)));
            }
        }

        public static uint ReadDoubleWordUsingWord(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)((peripheral.ReadWord(address + 2) << 16) | peripheral.ReadWord(address));
            }
        }

        public static uint ReadDoubleWordUsingWordBigEndian(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)((Misc.SwapBytesUShort(peripheral.ReadWord(address)) << 16) |
                        Misc.SwapBytesUShort(peripheral.ReadWord(address + 2)));
            }
        }

        public static void WriteDoubleWordUsingWord(this IWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteWord(address + 2, (ushort)(value >> 16));
                peripheral.WriteWord(address, (ushort)(value));
            }
        }

        public static void WriteDoubleWordUsingWordBigEndian(this IWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteWord(address, Misc.SwapBytesUShort((ushort)(value >> 16)));
                peripheral.WriteWord(address + 2, Misc.SwapBytesUShort((ushort)(value)));
            }
        }

        public static uint ReadDoubleWordUsingQword(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = (int)(address & 7);
                return (uint)(peripheral.ReadQuadWord(readAddress) >> offset * 8);
            }
        }

        public static uint ReadDoubleWordUsingQwordBigEndian(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = 4 - (int)(address & 7);
                return Misc.SwapBytesUInt((uint)(peripheral.ReadQuadWord(readAddress) >> offset * 8));
            }
        }

        public static void WriteDoubleWordUsingQword(this IQuadWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                var alignedAddress = address & (~7);
                var offset = (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(alignedAddress) & ~(0xFFFFFFFFul << offset * 8);
                peripheral.WriteQuadWord(alignedAddress, (ulong)(oldValue | (ulong)(value << 8 * offset)));
            }
        }

        public static void WriteDoubleWordUsingQwordBigEndian(this IQuadWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                value = Misc.SwapBytesUInt(value);
                var alignedAddress = address & (~7);
                var offset = 4 - (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(alignedAddress) & ~(0xFFFFFFFFul << offset * 8);
                peripheral.WriteQuadWord(alignedAddress, (ulong)(oldValue | (ulong)(value << 8 * offset)));
            }
        }

        public static ulong ReadQuadWordUsingWord(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)
                    (peripheral.ReadWord(address + 6) << 48 | peripheral.ReadWord(address + 4) << 32
                     | peripheral.ReadWord(address + 2) << 16 | peripheral.ReadWord(address));
            }
        }

        public static ulong ReadQuadWordUsingWordBigEndian(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)
                    (Misc.SwapBytesUShort(peripheral.ReadWord(address)) << 48 |
                     Misc.SwapBytesUShort(peripheral.ReadWord(address + 2)) << 32 |
                     Misc.SwapBytesUShort(peripheral.ReadWord(address + 4)) << 16 |
                     Misc.SwapBytesUShort(peripheral.ReadWord(address + 6)));
            }
        }

        public static void WriteQuadWordUsingWord(this IWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteWord(address + 6, (ushort)(value >> 48));
                peripheral.WriteWord(address + 4, (ushort)(value >> 32));
                peripheral.WriteWord(address + 2, (ushort)(value >> 16));
                peripheral.WriteWord(address, (ushort)value);
            }
        }

        public static void WriteQuadWordUsingWordBigEndian(this IWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteWord(address, Misc.SwapBytesUShort((ushort)(value >> 48)));
                peripheral.WriteWord(address + 2, Misc.SwapBytesUShort((ushort)(value >> 32)));
                peripheral.WriteWord(address + 4, Misc.SwapBytesUShort((ushort)(value >> 16)));
                peripheral.WriteWord(address + 6, Misc.SwapBytesUShort((ushort)value));
            }
        }

        public static ulong ReadQuadWordUsingDword(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)
                    (peripheral.ReadDoubleWord(address + 4) << 32
                     | peripheral.ReadDoubleWord(address));
            }
        }

        public static ulong ReadQuadWordUsingDwordBigEndian(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)
                    (Misc.SwapBytesUInt(peripheral.ReadDoubleWord(address)) << 32 |
                     Misc.SwapBytesUInt(peripheral.ReadDoubleWord(address + 4)));
            }
        }

        public static void WriteQuadWordUsingDword(this IDoubleWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteDoubleWord(address + 4, (uint)(value >> 32));
                peripheral.WriteDoubleWord(address, (uint)value);
            }
        }

        public static void WriteQuadWordUsingDwordBigEndian(this IDoubleWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteDoubleWord(address, Misc.SwapBytesUInt((uint)(value >> 32)));
                peripheral.WriteDoubleWord(address + 4, Misc.SwapBytesUInt((uint)value));
            }
        }

        public static ushort ReadWordBigEndian(this IWordPeripheral peripheral, long address)
        {
            return Misc.SwapBytesUShort(peripheral.ReadWord(address));
        }

        public static void WriteWordBigEndian(this IWordPeripheral peripheral, long address, ushort value)
        {
            peripheral.WriteWord(address, Misc.SwapBytesUShort(value));
        }

        public static uint ReadDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address)
        {
            return Misc.SwapBytesUInt(peripheral.ReadDoubleWord(address));
        }

        public static void WriteDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address, uint value)
        {
            peripheral.WriteDoubleWord(address, Misc.SwapBytesUInt(value));
        }

        public static ulong ReadQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address)
        {
            return Misc.SwapBytesULong(peripheral.ReadQuadWord(address));
        }

        public static void WriteQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address, ulong value)
        {
            peripheral.WriteQuadWord(address, Misc.SwapBytesULong(value));
        }

        public static byte ReadByteNotTranslated(this IBusPeripheral peripheral, long address)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.Byte, address);
            return 0;
        }

        public static ushort ReadWordNotTranslated(this IBusPeripheral peripheral, long address)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.Word, address);
            return 0;
        }

        public static uint ReadDoubleWordNotTranslated(this IBusPeripheral peripheral, long address)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.DoubleWord, address);
            return 0;
        }

        public static ulong ReadQuadWordNotTranslated(this IBusPeripheral peripheral, long address)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.QuadWord, address);
            return 0;
        }

        public static void WriteByteNotTranslated(this IBusPeripheral peripheral, long address, byte value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.Byte, address, value);
        }

        public static void WriteWordNotTranslated(this IBusPeripheral peripheral, long address, ushort value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.Word, address, value);
        }

        public static void WriteDoubleWordNotTranslated(this IBusPeripheral peripheral, long address, uint value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.DoubleWord, address, value);
        }

        public static void WriteQuadWordNotTranslated(this IBusPeripheral peripheral, long address, ulong value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.QuadWord, address, value);
        }

        private static void LogNotTranslated(IBusPeripheral peripheral, SysbusAccessWidth operationWidth, long address, ulong? value = null)
        {
            var strBldr = new StringBuilder();
            strBldr.AppendFormat("Attempt to {0} {1} from peripheral that doesn't support {1} interface.", value.HasValue ? "write" : "read", operationWidth);
            strBldr.AppendFormat(" Offset 0x{0:X}", address);
            if(value.HasValue)
            {
                strBldr.AppendFormat(", value 0x{0:X}", value.Value);
            }
            strBldr.Append(".");

            peripheral.Log(LogLevel.Warning, peripheral.GetMachine().SystemBus.DecorateWithCPUNameAndPC(strBldr.ToString()));
        }
    }
}
