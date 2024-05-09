/********************************************************
*
* Warning!
* This file was generated automatically.
* Please do not edit. Changes should be made in the
* appropriate *.tt file.
*
*/
using System;
using System.Text;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Extensions
{
    public static class ReadWriteExtensions
    {
        public static ushort ReadWordUsingByte(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ushort)((ushort)peripheral.ReadByte(address)
                    | (ushort)peripheral.ReadByte(address + 1) << 8
                );
            }
        }

        public static BusAccess.WordReadMethod BuildWordReadUsing(BusAccess.ByteReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ushort)((ushort)read(address)
                        | (ushort)read(address + 1) << 8
                    );
                }
            };
        }

        public static void WriteWordUsingByte(this IBytePeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 0, (byte)(value >> 0));
                peripheral.WriteByte(address + 1, (byte)(value >> 8));
            }
        }

        public static BusAccess.WordWriteMethod BuildWordWriteUsing(BusAccess.ByteReadMethod read, BusAccess.ByteWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, (byte)(value >> 0));
                    write(address + 1, (byte)(value >> 8));
                }
            };
        }

        public static ushort ReadWordUsingByteBigEndian(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ushort)((ushort)(peripheral.ReadByte(address + 1))
                    | (ushort)(peripheral.ReadByte(address + 0)) << 8
                );
            }
        }

        public static BusAccess.WordReadMethod BuildWordReadBigEndianUsing(BusAccess.ByteReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ushort)((ushort)(read(address + 1))
                        | (ushort)(read(address + 0)) << 8
                    );
                }
            };
        }

        public static void WriteWordUsingByteBigEndian(this IBytePeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 0, ((byte)(value >> 8)));
                peripheral.WriteByte(address + 1, ((byte)(value >> 0)));
            }
        }

        public static BusAccess.WordWriteMethod BuildWordWriteBigEndianUsing(BusAccess.ByteReadMethod read, BusAccess.ByteWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, ((byte)(value >> 8)));
                    write(address + 1, ((byte)(value >> 0)));
                }
            };
        }

        public static uint ReadDoubleWordUsingByte(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)((uint)peripheral.ReadByte(address)
                    | (uint)peripheral.ReadByte(address + 1) << 8
                    | (uint)peripheral.ReadByte(address + 2) << 16
                    | (uint)peripheral.ReadByte(address + 3) << 24
                );
            }
        }

        public static BusAccess.DoubleWordReadMethod BuildDoubleWordReadUsing(BusAccess.ByteReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (uint)((uint)read(address)
                        | (uint)read(address + 1) << 8
                        | (uint)read(address + 2) << 16
                        | (uint)read(address + 3) << 24
                    );
                }
            };
        }

        public static void WriteDoubleWordUsingByte(this IBytePeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 0, (byte)(value >> 0));
                peripheral.WriteByte(address + 1, (byte)(value >> 8));
                peripheral.WriteByte(address + 2, (byte)(value >> 16));
                peripheral.WriteByte(address + 3, (byte)(value >> 24));
            }
        }

        public static BusAccess.DoubleWordWriteMethod BuildDoubleWordWriteUsing(BusAccess.ByteReadMethod read, BusAccess.ByteWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, (byte)(value >> 0));
                    write(address + 1, (byte)(value >> 8));
                    write(address + 2, (byte)(value >> 16));
                    write(address + 3, (byte)(value >> 24));
                }
            };
        }

        public static uint ReadDoubleWordUsingByteBigEndian(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)((uint)(peripheral.ReadByte(address + 3))
                    | (uint)(peripheral.ReadByte(address + 2)) << 8
                    | (uint)(peripheral.ReadByte(address + 1)) << 16
                    | (uint)(peripheral.ReadByte(address + 0)) << 24
                );
            }
        }

        public static BusAccess.DoubleWordReadMethod BuildDoubleWordReadBigEndianUsing(BusAccess.ByteReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (uint)((uint)(read(address + 3))
                        | (uint)(read(address + 2)) << 8
                        | (uint)(read(address + 1)) << 16
                        | (uint)(read(address + 0)) << 24
                    );
                }
            };
        }

        public static void WriteDoubleWordUsingByteBigEndian(this IBytePeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 0, ((byte)(value >> 24)));
                peripheral.WriteByte(address + 1, ((byte)(value >> 16)));
                peripheral.WriteByte(address + 2, ((byte)(value >> 8)));
                peripheral.WriteByte(address + 3, ((byte)(value >> 0)));
            }
        }

        public static BusAccess.DoubleWordWriteMethod BuildDoubleWordWriteBigEndianUsing(BusAccess.ByteReadMethod read, BusAccess.ByteWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, ((byte)(value >> 24)));
                    write(address + 1, ((byte)(value >> 16)));
                    write(address + 2, ((byte)(value >> 8)));
                    write(address + 3, ((byte)(value >> 0)));
                }
            };
        }

        public static ulong ReadQuadWordUsingByte(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)((ulong)peripheral.ReadByte(address)
                    | (ulong)peripheral.ReadByte(address + 1) << 8
                    | (ulong)peripheral.ReadByte(address + 2) << 16
                    | (ulong)peripheral.ReadByte(address + 3) << 24
                    | (ulong)peripheral.ReadByte(address + 4) << 32
                    | (ulong)peripheral.ReadByte(address + 5) << 40
                    | (ulong)peripheral.ReadByte(address + 6) << 48
                    | (ulong)peripheral.ReadByte(address + 7) << 56
                );
            }
        }

        public static BusAccess.QuadWordReadMethod BuildQuadWordReadUsing(BusAccess.ByteReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ulong)((ulong)read(address)
                        | (ulong)read(address + 1) << 8
                        | (ulong)read(address + 2) << 16
                        | (ulong)read(address + 3) << 24
                        | (ulong)read(address + 4) << 32
                        | (ulong)read(address + 5) << 40
                        | (ulong)read(address + 6) << 48
                        | (ulong)read(address + 7) << 56
                    );
                }
            };
        }

        public static void WriteQuadWordUsingByte(this IBytePeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 0, (byte)(value >> 0));
                peripheral.WriteByte(address + 1, (byte)(value >> 8));
                peripheral.WriteByte(address + 2, (byte)(value >> 16));
                peripheral.WriteByte(address + 3, (byte)(value >> 24));
                peripheral.WriteByte(address + 4, (byte)(value >> 32));
                peripheral.WriteByte(address + 5, (byte)(value >> 40));
                peripheral.WriteByte(address + 6, (byte)(value >> 48));
                peripheral.WriteByte(address + 7, (byte)(value >> 56));
            }
        }

        public static BusAccess.QuadWordWriteMethod BuildQuadWordWriteUsing(BusAccess.ByteReadMethod read, BusAccess.ByteWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, (byte)(value >> 0));
                    write(address + 1, (byte)(value >> 8));
                    write(address + 2, (byte)(value >> 16));
                    write(address + 3, (byte)(value >> 24));
                    write(address + 4, (byte)(value >> 32));
                    write(address + 5, (byte)(value >> 40));
                    write(address + 6, (byte)(value >> 48));
                    write(address + 7, (byte)(value >> 56));
                }
            };
        }

        public static ulong ReadQuadWordUsingByteBigEndian(this IBytePeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)((ulong)(peripheral.ReadByte(address + 7))
                    | (ulong)(peripheral.ReadByte(address + 6)) << 8
                    | (ulong)(peripheral.ReadByte(address + 5)) << 16
                    | (ulong)(peripheral.ReadByte(address + 4)) << 24
                    | (ulong)(peripheral.ReadByte(address + 3)) << 32
                    | (ulong)(peripheral.ReadByte(address + 2)) << 40
                    | (ulong)(peripheral.ReadByte(address + 1)) << 48
                    | (ulong)(peripheral.ReadByte(address + 0)) << 56
                );
            }
        }

        public static BusAccess.QuadWordReadMethod BuildQuadWordReadBigEndianUsing(BusAccess.ByteReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ulong)((ulong)(read(address + 7))
                        | (ulong)(read(address + 6)) << 8
                        | (ulong)(read(address + 5)) << 16
                        | (ulong)(read(address + 4)) << 24
                        | (ulong)(read(address + 3)) << 32
                        | (ulong)(read(address + 2)) << 40
                        | (ulong)(read(address + 1)) << 48
                        | (ulong)(read(address + 0)) << 56
                    );
                }
            };
        }

        public static void WriteQuadWordUsingByteBigEndian(this IBytePeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteByte(address + 0, ((byte)(value >> 56)));
                peripheral.WriteByte(address + 1, ((byte)(value >> 48)));
                peripheral.WriteByte(address + 2, ((byte)(value >> 40)));
                peripheral.WriteByte(address + 3, ((byte)(value >> 32)));
                peripheral.WriteByte(address + 4, ((byte)(value >> 24)));
                peripheral.WriteByte(address + 5, ((byte)(value >> 16)));
                peripheral.WriteByte(address + 6, ((byte)(value >> 8)));
                peripheral.WriteByte(address + 7, ((byte)(value >> 0)));
            }
        }

        public static BusAccess.QuadWordWriteMethod BuildQuadWordWriteBigEndianUsing(BusAccess.ByteReadMethod read, BusAccess.ByteWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, ((byte)(value >> 56)));
                    write(address + 1, ((byte)(value >> 48)));
                    write(address + 2, ((byte)(value >> 40)));
                    write(address + 3, ((byte)(value >> 32)));
                    write(address + 4, ((byte)(value >> 24)));
                    write(address + 5, ((byte)(value >> 16)));
                    write(address + 6, ((byte)(value >> 8)));
                    write(address + 7, ((byte)(value >> 0)));
                }
            };
        }

        public static byte ReadByteUsingWord(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~1);
                var offset = (int)(address & 1);
                return (byte)(peripheral.ReadWord(readAddress) >> offset * 8);
            }
        }

        public static BusAccess.ByteReadMethod BuildByteReadUsing(BusAccess.WordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~1);
                    var offset = (int)(address & 1);
                    return (byte)(read(readAddress) >> offset * 8);
                }
            };
        }

        public static void WriteByteUsingWord(this IWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~1);
                var offset = (int)(address & 1);
                var oldValue = peripheral.ReadWord(writeAddress) & ~((ushort)0xFF << offset * 8);
                peripheral.WriteWord(writeAddress, (ushort)(oldValue | ((ushort)value << 8 * offset)));
            }
        }

        public static BusAccess.ByteWriteMethod BuildByteWriteUsing(BusAccess.WordReadMethod read, BusAccess.WordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~1);
                    var offset = (int)(address & 1);
                    var oldValue = read(writeAddress) & ~((ushort)0xFF << offset * 8);
                    write(writeAddress, (ushort)(oldValue | ((ushort)value << 8 * offset)));
                }
            };
        }

        public static byte ReadByteUsingWordBigEndian(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~1);
                var offset = 1 - (int)(address & 1);
                return ((byte)(peripheral.ReadWord(readAddress) >> offset * 8));
            }
        }

        public static BusAccess.ByteReadMethod BuildByteReadBigEndianUsing(BusAccess.WordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~1);
                    var offset = 1 - (int)(address & 1);
                    return ((byte)(read(readAddress) >> offset * 8));
                }
            };
        }

        public static void WriteByteUsingWordBigEndian(this IWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~1);
                var offset = 1 - (int)(address & 1);
                var oldValue = peripheral.ReadWord(writeAddress) & ~((ushort)0xFF << offset * 8);
                peripheral.WriteWord(writeAddress, (ushort)(oldValue | ((ushort)value << 8 * offset)));
            }
        }

        public static BusAccess.ByteWriteMethod BuildByteWriteBigEndianUsing(BusAccess.WordReadMethod read, BusAccess.WordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~1);
                    var offset = 1 - (int)(address & 1);
                    var oldValue = read(writeAddress) & ~((ushort)0xFF << offset * 8);
                    write(writeAddress, (ushort)(oldValue | ((ushort)value << 8 * offset)));
                }
            };
        }
        public static uint ReadDoubleWordUsingWord(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)((uint)peripheral.ReadWord(address)
                    | (uint)peripheral.ReadWord(address + 2) << 16
                );
            }
        }

        public static BusAccess.DoubleWordReadMethod BuildDoubleWordReadUsing(BusAccess.WordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (uint)((uint)read(address)
                        | (uint)read(address + 2) << 16
                    );
                }
            };
        }

        public static void WriteDoubleWordUsingWord(this IWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteWord(address + 0, (ushort)(value >> 0));
                peripheral.WriteWord(address + 2, (ushort)(value >> 16));
            }
        }

        public static BusAccess.DoubleWordWriteMethod BuildDoubleWordWriteUsing(BusAccess.WordReadMethod read, BusAccess.WordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, (ushort)(value >> 0));
                    write(address + 2, (ushort)(value >> 16));
                }
            };
        }

        public static uint ReadDoubleWordUsingWordBigEndian(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (uint)((uint)Misc.SwapBytesUShort(peripheral.ReadWord(address + 2))
                    | (uint)Misc.SwapBytesUShort(peripheral.ReadWord(address + 0)) << 16
                );
            }
        }

        public static BusAccess.DoubleWordReadMethod BuildDoubleWordReadBigEndianUsing(BusAccess.WordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (uint)((uint)Misc.SwapBytesUShort(read(address + 2))
                        | (uint)Misc.SwapBytesUShort(read(address + 0)) << 16
                    );
                }
            };
        }

        public static void WriteDoubleWordUsingWordBigEndian(this IWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                peripheral.WriteWord(address + 0, Misc.SwapBytesUShort((ushort)(value >> 16)));
                peripheral.WriteWord(address + 2, Misc.SwapBytesUShort((ushort)(value >> 0)));
            }
        }

        public static BusAccess.DoubleWordWriteMethod BuildDoubleWordWriteBigEndianUsing(BusAccess.WordReadMethod read, BusAccess.WordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, Misc.SwapBytesUShort((ushort)(value >> 16)));
                    write(address + 2, Misc.SwapBytesUShort((ushort)(value >> 0)));
                }
            };
        }

        public static ulong ReadQuadWordUsingWord(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)((ulong)peripheral.ReadWord(address)
                    | (ulong)peripheral.ReadWord(address + 2) << 16
                    | (ulong)peripheral.ReadWord(address + 4) << 32
                    | (ulong)peripheral.ReadWord(address + 6) << 48
                );
            }
        }

        public static BusAccess.QuadWordReadMethod BuildQuadWordReadUsing(BusAccess.WordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ulong)((ulong)read(address)
                        | (ulong)read(address + 2) << 16
                        | (ulong)read(address + 4) << 32
                        | (ulong)read(address + 6) << 48
                    );
                }
            };
        }

        public static void WriteQuadWordUsingWord(this IWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteWord(address + 0, (ushort)(value >> 0));
                peripheral.WriteWord(address + 2, (ushort)(value >> 16));
                peripheral.WriteWord(address + 4, (ushort)(value >> 32));
                peripheral.WriteWord(address + 6, (ushort)(value >> 48));
            }
        }

        public static BusAccess.QuadWordWriteMethod BuildQuadWordWriteUsing(BusAccess.WordReadMethod read, BusAccess.WordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, (ushort)(value >> 0));
                    write(address + 2, (ushort)(value >> 16));
                    write(address + 4, (ushort)(value >> 32));
                    write(address + 6, (ushort)(value >> 48));
                }
            };
        }

        public static ulong ReadQuadWordUsingWordBigEndian(this IWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)((ulong)Misc.SwapBytesUShort(peripheral.ReadWord(address + 6))
                    | (ulong)Misc.SwapBytesUShort(peripheral.ReadWord(address + 4)) << 16
                    | (ulong)Misc.SwapBytesUShort(peripheral.ReadWord(address + 2)) << 32
                    | (ulong)Misc.SwapBytesUShort(peripheral.ReadWord(address + 0)) << 48
                );
            }
        }

        public static BusAccess.QuadWordReadMethod BuildQuadWordReadBigEndianUsing(BusAccess.WordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ulong)((ulong)Misc.SwapBytesUShort(read(address + 6))
                        | (ulong)Misc.SwapBytesUShort(read(address + 4)) << 16
                        | (ulong)Misc.SwapBytesUShort(read(address + 2)) << 32
                        | (ulong)Misc.SwapBytesUShort(read(address + 0)) << 48
                    );
                }
            };
        }

        public static void WriteQuadWordUsingWordBigEndian(this IWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteWord(address + 0, Misc.SwapBytesUShort((ushort)(value >> 48)));
                peripheral.WriteWord(address + 2, Misc.SwapBytesUShort((ushort)(value >> 32)));
                peripheral.WriteWord(address + 4, Misc.SwapBytesUShort((ushort)(value >> 16)));
                peripheral.WriteWord(address + 6, Misc.SwapBytesUShort((ushort)(value >> 0)));
            }
        }

        public static BusAccess.QuadWordWriteMethod BuildQuadWordWriteBigEndianUsing(BusAccess.WordReadMethod read, BusAccess.WordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, Misc.SwapBytesUShort((ushort)(value >> 48)));
                    write(address + 2, Misc.SwapBytesUShort((ushort)(value >> 32)));
                    write(address + 4, Misc.SwapBytesUShort((ushort)(value >> 16)));
                    write(address + 6, Misc.SwapBytesUShort((ushort)(value >> 0)));
                }
            };
        }

        public static byte ReadByteUsingDoubleWord(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = (int)(address & 3);
                return (byte)(peripheral.ReadDoubleWord(readAddress) >> offset * 8);
            }
        }

        public static BusAccess.ByteReadMethod BuildByteReadUsing(BusAccess.DoubleWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~3);
                    var offset = (int)(address & 3);
                    return (byte)(read(readAddress) >> offset * 8);
                }
            };
        }

        public static void WriteByteUsingDoubleWord(this IDoubleWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~3);
                var offset = (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(writeAddress) & ~((uint)0xFF << offset * 8);
                peripheral.WriteDoubleWord(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
            }
        }

        public static BusAccess.ByteWriteMethod BuildByteWriteUsing(BusAccess.DoubleWordReadMethod read, BusAccess.DoubleWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~3);
                    var offset = (int)(address & 3);
                    var oldValue = read(writeAddress) & ~((uint)0xFF << offset * 8);
                    write(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
                }
            };
        }

        public static byte ReadByteUsingDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = 3 - (int)(address & 3);
                return ((byte)(peripheral.ReadDoubleWord(readAddress) >> offset * 8));
            }
        }

        public static BusAccess.ByteReadMethod BuildByteReadBigEndianUsing(BusAccess.DoubleWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~3);
                    var offset = 3 - (int)(address & 3);
                    return ((byte)(read(readAddress) >> offset * 8));
                }
            };
        }

        public static void WriteByteUsingDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~3);
                var offset = 3 - (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(writeAddress) & ~((uint)0xFF << offset * 8);
                peripheral.WriteDoubleWord(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
            }
        }

        public static BusAccess.ByteWriteMethod BuildByteWriteBigEndianUsing(BusAccess.DoubleWordReadMethod read, BusAccess.DoubleWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~3);
                    var offset = 3 - (int)(address & 3);
                    var oldValue = read(writeAddress) & ~((uint)0xFF << offset * 8);
                    write(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
                }
            };
        }
        public static ushort ReadWordUsingDoubleWord(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = (int)(address & 3);
                return (ushort)(peripheral.ReadDoubleWord(readAddress) >> offset * 8);
            }
        }

        public static BusAccess.WordReadMethod BuildWordReadUsing(BusAccess.DoubleWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~3);
                    var offset = (int)(address & 3);
                    return (ushort)(read(readAddress) >> offset * 8);
                }
            };
        }

        public static void WriteWordUsingDoubleWord(this IDoubleWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                var writeAddress = address & (~3);
                var offset = (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(writeAddress) & ~((uint)0xFFFF << offset * 8);
                peripheral.WriteDoubleWord(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
            }
        }

        public static BusAccess.WordWriteMethod BuildWordWriteUsing(BusAccess.DoubleWordReadMethod read, BusAccess.DoubleWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~3);
                    var offset = (int)(address & 3);
                    var oldValue = read(writeAddress) & ~((uint)0xFFFF << offset * 8);
                    write(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
                }
            };
        }

        public static ushort ReadWordUsingDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~3);
                var offset = 2 - (int)(address & 3);
                return Misc.SwapBytesUShort((ushort)(peripheral.ReadDoubleWord(readAddress) >> offset * 8));
            }
        }

        public static BusAccess.WordReadMethod BuildWordReadBigEndianUsing(BusAccess.DoubleWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~3);
                    var offset = 2 - (int)(address & 3);
                    return Misc.SwapBytesUShort((ushort)(read(readAddress) >> offset * 8));
                }
            };
        }

        public static void WriteWordUsingDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                value = Misc.SwapBytesUShort(value);
                var writeAddress = address & (~3);
                var offset = 2 - (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(writeAddress) & ~((uint)0xFFFF << offset * 8);
                peripheral.WriteDoubleWord(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
            }
        }

        public static BusAccess.WordWriteMethod BuildWordWriteBigEndianUsing(BusAccess.DoubleWordReadMethod read, BusAccess.DoubleWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    value = Misc.SwapBytesUShort(value);
                    var writeAddress = address & (~3);
                    var offset = 2 - (int)(address & 3);
                    var oldValue = read(writeAddress) & ~((uint)0xFFFF << offset * 8);
                    write(writeAddress, (uint)(oldValue | ((uint)value << 8 * offset)));
                }
            };
        }
        public static ulong ReadQuadWordUsingDoubleWord(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)((ulong)peripheral.ReadDoubleWord(address)
                    | (ulong)peripheral.ReadDoubleWord(address + 4) << 32
                );
            }
        }

        public static BusAccess.QuadWordReadMethod BuildQuadWordReadUsing(BusAccess.DoubleWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ulong)((ulong)read(address)
                        | (ulong)read(address + 4) << 32
                    );
                }
            };
        }

        public static void WriteQuadWordUsingDoubleWord(this IDoubleWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteDoubleWord(address + 0, (uint)(value >> 0));
                peripheral.WriteDoubleWord(address + 4, (uint)(value >> 32));
            }
        }

        public static BusAccess.QuadWordWriteMethod BuildQuadWordWriteUsing(BusAccess.DoubleWordReadMethod read, BusAccess.DoubleWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, (uint)(value >> 0));
                    write(address + 4, (uint)(value >> 32));
                }
            };
        }

        public static ulong ReadQuadWordUsingDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address)
        {
            unchecked
            {
                return (ulong)((ulong)Misc.SwapBytesUInt(peripheral.ReadDoubleWord(address + 4))
                    | (ulong)Misc.SwapBytesUInt(peripheral.ReadDoubleWord(address + 0)) << 32
                );
            }
        }

        public static BusAccess.QuadWordReadMethod BuildQuadWordReadBigEndianUsing(BusAccess.DoubleWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    return (ulong)((ulong)Misc.SwapBytesUInt(read(address + 4))
                        | (ulong)Misc.SwapBytesUInt(read(address + 0)) << 32
                    );
                }
            };
        }

        public static void WriteQuadWordUsingDoubleWordBigEndian(this IDoubleWordPeripheral peripheral, long address, ulong value)
        {
            unchecked
            {
                peripheral.WriteDoubleWord(address + 0, Misc.SwapBytesUInt((uint)(value >> 32)));
                peripheral.WriteDoubleWord(address + 4, Misc.SwapBytesUInt((uint)(value >> 0)));
            }
        }

        public static BusAccess.QuadWordWriteMethod BuildQuadWordWriteBigEndianUsing(BusAccess.DoubleWordReadMethod read, BusAccess.DoubleWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    write(address + 0, Misc.SwapBytesUInt((uint)(value >> 32)));
                    write(address + 4, Misc.SwapBytesUInt((uint)(value >> 0)));
                }
            };
        }

        public static byte ReadByteUsingQuadWord(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = (int)(address & 7);
                return (byte)(peripheral.ReadQuadWord(readAddress) >> offset * 8);
            }
        }

        public static BusAccess.ByteReadMethod BuildByteReadUsing(BusAccess.QuadWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~7);
                    var offset = (int)(address & 7);
                    return (byte)(read(readAddress) >> offset * 8);
                }
            };
        }

        public static void WriteByteUsingQuadWord(this IQuadWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~7);
                var offset = (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~((ulong)0xFF << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static BusAccess.ByteWriteMethod BuildByteWriteUsing(BusAccess.QuadWordReadMethod read, BusAccess.QuadWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~7);
                    var offset = (int)(address & 7);
                    var oldValue = read(writeAddress) & ~((ulong)0xFF << offset * 8);
                    write(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
                }
            };
        }

        public static byte ReadByteUsingQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = 7 - (int)(address & 7);
                return ((byte)(peripheral.ReadQuadWord(readAddress) >> offset * 8));
            }
        }

        public static BusAccess.ByteReadMethod BuildByteReadBigEndianUsing(BusAccess.QuadWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~7);
                    var offset = 7 - (int)(address & 7);
                    return ((byte)(read(readAddress) >> offset * 8));
                }
            };
        }

        public static void WriteByteUsingQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address, byte value)
        {
            unchecked
            {
                var writeAddress = address & (~7);
                var offset = 7 - (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~((ulong)0xFF << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static BusAccess.ByteWriteMethod BuildByteWriteBigEndianUsing(BusAccess.QuadWordReadMethod read, BusAccess.QuadWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~7);
                    var offset = 7 - (int)(address & 7);
                    var oldValue = read(writeAddress) & ~((ulong)0xFF << offset * 8);
                    write(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
                }
            };
        }
        public static ushort ReadWordUsingQuadWord(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = (int)(address & 7);
                return (ushort)(peripheral.ReadQuadWord(readAddress) >> offset * 8);
            }
        }

        public static BusAccess.WordReadMethod BuildWordReadUsing(BusAccess.QuadWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~7);
                    var offset = (int)(address & 7);
                    return (ushort)(read(readAddress) >> offset * 8);
                }
            };
        }

        public static void WriteWordUsingQuadWord(this IQuadWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                var writeAddress = address & (~7);
                var offset = (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~((ulong)0xFFFF << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static BusAccess.WordWriteMethod BuildWordWriteUsing(BusAccess.QuadWordReadMethod read, BusAccess.QuadWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~7);
                    var offset = (int)(address & 7);
                    var oldValue = read(writeAddress) & ~((ulong)0xFFFF << offset * 8);
                    write(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
                }
            };
        }

        public static ushort ReadWordUsingQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = 6 - (int)(address & 7);
                return Misc.SwapBytesUShort((ushort)(peripheral.ReadQuadWord(readAddress) >> offset * 8));
            }
        }

        public static BusAccess.WordReadMethod BuildWordReadBigEndianUsing(BusAccess.QuadWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~7);
                    var offset = 6 - (int)(address & 7);
                    return Misc.SwapBytesUShort((ushort)(read(readAddress) >> offset * 8));
                }
            };
        }

        public static void WriteWordUsingQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address, ushort value)
        {
            unchecked
            {
                value = Misc.SwapBytesUShort(value);
                var writeAddress = address & (~7);
                var offset = 6 - (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~((ulong)0xFFFF << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static BusAccess.WordWriteMethod BuildWordWriteBigEndianUsing(BusAccess.QuadWordReadMethod read, BusAccess.QuadWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    value = Misc.SwapBytesUShort(value);
                    var writeAddress = address & (~7);
                    var offset = 6 - (int)(address & 7);
                    var oldValue = read(writeAddress) & ~((ulong)0xFFFF << offset * 8);
                    write(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
                }
            };
        }
        public static uint ReadDoubleWordUsingQuadWord(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = (int)(address & 7);
                return (uint)(peripheral.ReadQuadWord(readAddress) >> offset * 8);
            }
        }

        public static BusAccess.DoubleWordReadMethod BuildDoubleWordReadUsing(BusAccess.QuadWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~7);
                    var offset = (int)(address & 7);
                    return (uint)(read(readAddress) >> offset * 8);
                }
            };
        }

        public static void WriteDoubleWordUsingQuadWord(this IQuadWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                var writeAddress = address & (~7);
                var offset = (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~((ulong)0xFFFFFFFFUL << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static BusAccess.DoubleWordWriteMethod BuildDoubleWordWriteUsing(BusAccess.QuadWordReadMethod read, BusAccess.QuadWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    var writeAddress = address & (~7);
                    var offset = (int)(address & 7);
                    var oldValue = read(writeAddress) & ~((ulong)0xFFFFFFFFUL << offset * 8);
                    write(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
                }
            };
        }

        public static uint ReadDoubleWordUsingQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address)
        {
            unchecked
            {
                var readAddress = address & (~7);
                var offset = 4 - (int)(address & 7);
                return Misc.SwapBytesUInt((uint)(peripheral.ReadQuadWord(readAddress) >> offset * 8));
            }
        }

        public static BusAccess.DoubleWordReadMethod BuildDoubleWordReadBigEndianUsing(BusAccess.QuadWordReadMethod read)
        {
            return address =>
            {
                unchecked
                {
                    var readAddress = address & (~7);
                    var offset = 4 - (int)(address & 7);
                    return Misc.SwapBytesUInt((uint)(read(readAddress) >> offset * 8));
                }
            };
        }

        public static void WriteDoubleWordUsingQuadWordBigEndian(this IQuadWordPeripheral peripheral, long address, uint value)
        {
            unchecked
            {
                value = Misc.SwapBytesUInt(value);
                var writeAddress = address & (~7);
                var offset = 4 - (int)(address & 7);
                var oldValue = peripheral.ReadQuadWord(writeAddress) & ~((ulong)0xFFFFFFFFUL << offset * 8);
                peripheral.WriteQuadWord(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
            }
        }

        public static BusAccess.DoubleWordWriteMethod BuildDoubleWordWriteBigEndianUsing(BusAccess.QuadWordReadMethod read, BusAccess.QuadWordWriteMethod write)
        {
            return (address, value) =>
            {
                unchecked
                {
                    value = Misc.SwapBytesUInt(value);
                    var writeAddress = address & (~7);
                    var offset = 4 - (int)(address & 7);
                    var oldValue = read(writeAddress) & ~((ulong)0xFFFFFFFFUL << offset * 8);
                    write(writeAddress, (ulong)(oldValue | ((ulong)value << 8 * offset)));
                }
            };
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

        public static void WriteByteNotTranslated(this IBusPeripheral peripheral, long address, byte value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.Byte, address, value);
        }

        public static ushort ReadWordNotTranslated(this IBusPeripheral peripheral, long address)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.Word, address);
            return 0;
        }

        public static void WriteWordNotTranslated(this IBusPeripheral peripheral, long address, ushort value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.Word, address, value);
        }

        public static uint ReadDoubleWordNotTranslated(this IBusPeripheral peripheral, long address)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.DoubleWord, address);
            return 0;
        }

        public static void WriteDoubleWordNotTranslated(this IBusPeripheral peripheral, long address, uint value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.DoubleWord, address, value);
        }

        public static ulong ReadQuadWordNotTranslated(this IBusPeripheral peripheral, long address)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.QuadWord, address);
            return 0;
        }

        public static void WriteQuadWordNotTranslated(this IBusPeripheral peripheral, long address, ulong value)
        {
            LogNotTranslated(peripheral, SysbusAccessWidth.QuadWord, address, value);
        }

        private static void LogNotTranslated(IBusPeripheral peripheral, SysbusAccessWidth operationWidth, long address, ulong? value = null)
        {
            var strBldr = new StringBuilder();
            var isWrite = value.HasValue;
            strBldr.AppendFormat("Attempted {0} {1} isn't supported by the peripheral.", operationWidth, isWrite ? "write" : "read");
            strBldr.AppendFormat(" Offset 0x{0:X}", address);
            if(isWrite)
            {
                strBldr.AppendFormat(", value 0x{0:X}", value.Value);
            }
            strBldr.Append(".");

            peripheral.Log(LogLevel.Warning, peripheral.GetMachine().GetSystemBus(peripheral).DecorateWithCPUNameAndPC(strBldr.ToString()));
        }
    }
}
