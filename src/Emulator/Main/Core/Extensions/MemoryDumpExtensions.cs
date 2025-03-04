//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Extensions
{
    public static class MemoryDumpExtensions
    {
        public static void DumpBinary(this IMemory memory, SequencedFilePath fileName, ulong offset = 0, ICPU context = null)
        {
            memory.DumpBinary(fileName, offset, (ulong)memory.Size - offset, context);
        }

        public static void DumpBinary(this IMemory memory, SequencedFilePath fileName, ulong offset, ulong size, ICPU context = null)
        {
            AssertArguments(memory, offset, size);

            try
            {
                using(var writer = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    var windows = (int)((size - offset + WindowSize - 1) / WindowSize - 1);
                    for(var i = 0; i < windows; ++i)
                    {
                        WriteBinaryMemoryChunk(writer, memory, offset, i, context);
                    }

                    var lastChunkSize = (size - offset) % WindowSize;
                    lastChunkSize = lastChunkSize == 0 ? WindowSize : lastChunkSize;
                    WriteBinaryMemoryChunk(writer, memory, offset, windows, context, lastChunkSize);
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException($"Exception while saving to file {fileName}: {e.Message}");
            }
        }

        public static void DumpHEX(this IMemory memory, SequencedFilePath fileName, ulong offset = 0, ICPU context = null, HexAddressOption addressOption = HexAddressOption.OffsetRelativeAddress)
        {
            memory.DumpHEX(fileName, offset, (ulong)memory.Size - offset, context, addressOption);
        }

        public static void DumpHEX(this IMemory memory, SequencedFilePath fileName, ulong offset, ulong size, ICPU context = null, HexAddressOption addressOption = HexAddressOption.OffsetRelativeAddress)
        {
            AssertArguments(memory, offset, size);
            var extendedAddress = 0UL;
            var baseAddress = 0UL;
            switch(addressOption)
            {
            case HexAddressOption.OffsetRelativeAddress:
                break;
            case HexAddressOption.PeripheralRelativeAddress:
                baseAddress = offset;
                break;
            default:
                throw new RecoverableException($"Invalid '{nameof(addressOption)}' value");
            }

            if(baseAddress + size > (1UL << 32))
            {
                throw new RecoverableException("Hex format is limited to 4GiB addressing");
            }

            try
            {
                using(var writer = new StreamWriter(fileName))
                {
                    var chunks = (int)((size - offset + MaxHexRecordDataLength - 1) / MaxHexRecordDataLength - 1);
                    for(var i = 0; i < chunks; ++i)
                    {
                        WriteHexMemoryChunk(writer, memory, offset, baseAddress, ref extendedAddress, i, context);
                    }

                    var lastChunkSize = (size - offset) % MaxHexRecordDataLength;
                    lastChunkSize = lastChunkSize == 0 ? MaxHexRecordDataLength : lastChunkSize;
                    WriteHexMemoryChunk(writer, memory, offset, baseAddress, ref extendedAddress, chunks, context, lastChunkSize);
                    writer.WriteLine(":00000001FF"); // End Of File record
                }
            }
            catch(IOException e)
            {
                throw new RecoverableException($"Exception while saving to file {fileName}: {e.Message}");
            }
        }

        private static void WriteBinaryMemoryChunk(FileStream writer, IMemory memory, ulong offset, int chunk, ICPU context, ulong size = WindowSize)
        {
            var data = memory.ReadBytes((long)(offset + (ulong)chunk * WindowSize), (int)size, context);
            writer.Write(data, offset: 0, count: (int)size);
        }

        private static void WriteHexMemoryChunk(StreamWriter writer, IMemory memory, ulong offset, ulong baseAddress, ref ulong extendedAddress, int chunk, ICPU context, ulong size = MaxHexRecordDataLength)
        {
            var readOffset = offset + (ulong)chunk * MaxHexRecordDataLength;
            var address = baseAddress + readOffset - extendedAddress;
            var data = memory.ReadBytes((long)readOffset, (int)size, context);

            if(address > UInt16.MaxValue)
            {
                var extendedBy = address & ~(ulong)UInt16.MaxValue;
                extendedAddress += extendedBy;
                address -= extendedBy;
                writer.WriteHexExtendedLinearAddressRecord((uint)extendedAddress);
            }

            writer.WriteHexDataRecord((byte)size, (ushort)address, data);
        }

        private static void WriteHexExtendedLinearAddressRecord(this StreamWriter writer, uint extendedAddress)
        {
            byte length = 2;
            ushort address = 0;
            byte type = 4;
            extendedAddress = extendedAddress >> 16;
            var checksum = GetHexChecksum(length, address, type, BitHelper.GetBytesFromValue(extendedAddress, typeSize: 2));
            writer.WriteLine($":{length:X02}{address:X04}{type:X02}{extendedAddress:X04}{checksum:X02}");
        }

        private static void WriteHexDataRecord(this StreamWriter writer, byte length, ushort address, byte[] data)
        {
            byte type = 0;
            writer.Write($":{length:X02}{address:X04}{type:X02}");
            writer.Write(data.ToHexString());
            writer.WriteLine("{0:X02}", GetHexChecksum(length, address, type, data));
        }

        private static byte GetHexChecksum(byte length, ushort address, byte type, byte[] data)
        {
            var checksum = length + address + (address >> 8) + type;
            foreach(var b in data)
            {
                checksum += b;
            }
            return (byte)(-checksum);
        }

        private static void AssertArguments(this IMemory memory, ulong offset, ulong size)
        {
            if(size == 0)
            {
                throw new RecoverableException($"'{nameof(size)}' must be greater than zero");
            }
            if(offset > (ulong)memory.Size)
            {
                throw new RecoverableException($"'{nameof(offset)}' is outside of memory");
            }
            if(offset + size > (ulong)memory.Size)
            {
                throw new RecoverableException($"'{nameof(size)}' is too big, region is outside of memory");
            }
        }

        private const ulong WindowSize = 100 * 1024;
        private const ulong MaxHexRecordDataLength = Byte.MaxValue;

        public enum HexAddressOption
        {
            OffsetRelativeAddress,
            PeripheralRelativeAddress,
        }
    }
}
