//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using System.Text;
using Antmicro.Renode.Peripherals;

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
                return (ushort)(peripheral.ReadDoubleWord(readAddress) >> offset * 8);
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
                var alignedAddress = address & (~3);            
                var offset = 2 - (int)(address & 3);
                var oldValue = peripheral.ReadDoubleWord(alignedAddress) & ~(0xFFFF << offset * 8);                   
                peripheral.WriteDoubleWord(alignedAddress, (uint)(oldValue | (uint)(value << 8 * offset)));             
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
                return (uint)((peripheral.ReadWord(address) << 16) | peripheral.ReadWord(address + 2));

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
                peripheral.WriteWord(address, (ushort)(value >> 16));
                peripheral.WriteWord(address + 2, (ushort)(value));
            }
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

        private static void LogNotTranslated(IBusPeripheral peripheral, SysbusAccessWidth operationWidth, long address, uint? value = null)
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
