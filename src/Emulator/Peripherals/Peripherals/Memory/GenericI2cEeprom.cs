//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Memory
{
    public class GenericI2cEeprom: II2CPeripheral
    {
        public GenericI2cEeprom(IMemory memory, int addressBitSize = DefaultAddressBitSize, bool writable = DefaultWritable, int pageSize = DefaultPageSize)
        {
            if(addressBitSize != 8 && addressBitSize != 16)
            {
                throw new ConstructionException($"'{nameof(addressBitSize)}' has to be one of the legal values: 8, 16");
            }
            if(pageSize <= 0 || !Misc.IsPowerOfTwo((ulong)pageSize))
            {
                throw new ConstructionException($"'{nameof(pageSize)}' has to be a positive power of 2");
            }
            Memory = memory;
            addressSize = addressBitSize / 8;
            this.writable = writable;
            this.pageSize = pageSize;
        }

        public GenericI2cEeprom(ulong size, int addressBitSize = DefaultAddressBitSize, bool writable = DefaultWritable, int pageSize = DefaultPageSize)
            : this(new ArrayMemory(size, Byte.MaxValue), addressBitSize, writable, pageSize)
        {
        }

        public void Write(byte[] data)
        {
            var index = 0;
            if(addressBytes < addressSize)
            {
                var bytesLeft = addressSize - addressBytes;
                var bytesWritten = Math.Min(data.Length, bytesLeft);

                var addressValue = BitHelper.ToUInt32(data, 0, bytesWritten, reverse: false);
                currentAddress = (int)BitHelper.ReplaceBits((uint)currentAddress, addressValue, width: bytesWritten * 8, destinationPosition: (bytesLeft - bytesWritten) * 8);

                addressBytes += bytesWritten;
                index = bytesWritten;
                if(addressBytes == addressSize)
                {
                    this.NoisyLog("Set address: 0x{0:X4}", currentAddress);
                }
            }

            var count = data.Length - index;
            if(count == 0)
            {
                return;
            }

            if(!writable)
            {
                this.WarningLog("This memory is configured as read only, but write was attempted");
                return;
            }

            var pageOffset = currentAddress % pageSize;
            var pageStart = currentAddress - pageOffset;
            if(count > pageSize)
            {
                count = pageSize;
                index = data.Length - count;
            }

            var bytesToWrite = Math.Min(count, pageSize - pageOffset);
            this.DebugLog("Write {0} bytes to address 0x{1:X4}: {2}", bytesToWrite, currentAddress, data.Skip(index).Take(bytesToWrite).ToLazyHexString());
            Memory.WriteBytes(currentAddress, data, index, bytesToWrite);
            index += bytesToWrite;
            count -= bytesToWrite;
            currentAddress = pageStart;

            // rollover to the start of the current page
            if(count > 0)
            {
                this.DebugLog("Write {0} bytes to address 0x{1:X4}: {2}", count, currentAddress, data.Skip(index).Take(count).ToLazyHexString());
                Memory.WriteBytes(pageStart, data, index, count);
                currentAddress += count;
            }
        }

        public byte[] Read(int count = 1)
        {
            var startingAddress = currentAddress;
            var address = Enumerable.Empty<byte>();
            var data = Enumerable.Empty<byte>();

            if(addressBytes != addressSize && count > 0)
            {
                address = BitHelper.GetBytesFromValue((ulong)currentAddress, typeSize: addressSize, reverse: true)
                    .Skip(addressBytes)
                ;
                var bytesAdded = Math.Min(addressSize - addressBytes, count);
                count -= bytesAdded;
                addressBytes += bytesAdded;
            }
            var dataCount = count;

            // reads rollover to the start of the memory
            if(count >= Memory.Size)
            {
                // read is spanning the whole memory
                data = ReadRollover(0, (int)Memory.Size, currentAddress, count);
                currentAddress = (currentAddress + count) % (int)Memory.Size;
                count = 0;
            }
            else
            {
                var bytesToRead = (int)Math.Min(count, Memory.Size - currentAddress);
                data = Memory.ReadBytes(currentAddress, bytesToRead);
                currentAddress += bytesToRead;
                count -= bytesToRead;

                // rollover
                if(count > 0)
                {
                    data = data.Concat(Memory.ReadBytes(0, count));
                    currentAddress = count;
                    count = 0;
                }
            }

            this.NoisyLog("Read {0} bytes from address 0x{1:X4}: {2}", dataCount, startingAddress, data.ToLazyHexString());
            return address.Concat(data).ToArray();
        }

        public void FinishTransmission()
        {
            this.NoisyLog("Transmission finished");
            addressBytes = 0;
        }

        public void Reset()
        {
            addressBytes = 0;
            currentAddress = 0;
        }

        public IMemory Memory { get; }

        private IEnumerable<byte> ReadRollover(int start, int size, int offset, int count)
        {
            var data = Memory.ReadBytes(start, size);
            return Enumerable.Repeat<IEnumerable<byte>>(data, (count + size - 1) / size)
                .SelectMany(x => x)
                .Skip(offset)
                .Take(count)
            ;
         }

        private int addressBytes;
        private int currentAddress;
        private readonly int addressSize;
        private readonly bool writable;
        private readonly int pageSize;

        private const int DefaultAddressBitSize = 8;
        private const bool DefaultWritable = true;
        private const int DefaultPageSize = 64;
    }
}
