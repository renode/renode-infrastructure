//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2023 OS Systems
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.I2C
{
    public class AT24_I2CEeprom : II2CPeripheral
    {
        public AT24_I2CEeprom(uint size, uint pageSize, byte resetByte = 0xff)
        {
            if(!Misc.IsPowerOfTwo((ulong)size) || !Misc.IsPowerOfTwo((ulong)pageSize))
            {
                throw new ConstructionException("Size of the underlying memory must be a power of 2");
            }

            this.EepromMemory = new byte[size];
            this.PageSize = pageSize;
            this.ResetByte = resetByte;
            this.Pages = (uint)this.EepromMemory.Length / pageSize;
            this.PageAddressSize = this.EepromMemory.Length <= 0x100 ? 1u : 2u;
            this.CurrentOffset = 0;
        }

        public void Write(byte[] data)
        {
            if(data.Length < PageAddressSize)
            {
                this.Log(LogLevel.Error, "I2C Memory Write: Cannot receive less bytes than PageAddressSize which is {0} bytes: {1}: Invalid operation.", PageAddressSize, Misc.PrettyPrintCollectionHex(data));
                return;
            }

            this.Log(LogLevel.Debug, "I2C write size: {0}, bytes: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));

            uint offset = (PageAddressSize == 2) ? ((uint)data[0] << 8) + (uint)data[1] : (uint)data[0];
            uint localPage = offset / this.PageSize;
            uint localOffset = offset % this.PageSize;

            this.Log(LogLevel.Debug, "offset: {0}, page: {1}, cur offset: {2}", offset, localPage, localOffset);

            /* Dummy byte to initiate a Random Read */
            if(data.Length == PageAddressSize)
            {
                this.CurrentOffset = localOffset;

                return;
            }

            lock(lockObject)
            {
                for(var i = PageAddressSize; i < data.Length; ++i)
                {
                    /* Write rollover current page */
                    WriteToMemory((localPage * this.PageSize) + localOffset++, data[i]);
                    localOffset %= this.PageSize;
                }
            }

            this.CurrentOffset = (localPage * this.PageSize) + localOffset;
        }

        public byte[] Read(int count = 1)
        {
            if(count < 1)
            {
                this.Log(LogLevel.Error, "I2C Memory Read: Cannot read less than 1 byte or relative to {0}: Invalid operation.", count);
                return null;
            }

            Queue<byte> buffer = new Queue<byte>();

            lock(lockObject)
            {
                for(var i = 0; i < count; i++)
                {
                    /* Read rollover address range */
                    buffer.Enqueue(ReadFromMemory(this.CurrentOffset++));
                    this.CurrentOffset %= (uint)this.EepromMemory.Length;
                }
            }

            this.Log(LogLevel.Debug, "I2C read size: {0}, bytes: {1}", count, Misc.PrettyPrintCollectionHex(buffer.ToArray()));

            return buffer.ToArray();
        }

        public void FinishTransmission()
        {
            // Intentionally left blank.
        }

        public void Reset()
        {
            lock(lockObject)
            {
                for(int i = 0; i < this.EepromMemory.Length; ++i)
                {
                    this.EepromMemory[i] = this.ResetByte;
                }
            }
        }

        public void DumpMemory(uint offset, int size)
        {
            for(uint i = offset; (i < offset + size) && (i < this.EepromMemory.Length); ++i)
            {
                this.Log(LogLevel.Info, "0x{0:X4} = 0x{1:X2}", i, this.EepromMemory[i]);
            }
        }

        public void LoadMemory(uint offset, byte value, int repeat = 1)
        {
            if(repeat <= 0)
            {
                repeat = 1;
            }

            if(offset + repeat > this.EepromMemory.Length)
            {
                this.Log(LogLevel.Error, "Cannot load memory data because it is bigger than configured memory size.");
                return;
            }

            lock(lockObject)
            {
                for(var i = 0; i < repeat; i++)
                {
                    WriteToMemory(offset++, value);
                }
            }
        }

        public void LoadMemory(string path, uint offset, int repeat = 1)
        {
            var parsedValues = ParseMemoryFile(path);

            if(repeat <= 0)
            {
                repeat = 1;
            }

            if(offset + (parsedValues.Count() * repeat) > this.EepromMemory.Length)
            {
                this.Log(LogLevel.Error, "Cannot load memory data because it is bigger than configured memory size.");
                return;
            }

            lock(lockObject)
            {
                for(var i = 0; i < repeat; i++)
                {
                    foreach(byte value in parsedValues)
                    {
                        WriteToMemory(offset++, value);
                    }
                }
            }
        }

        private void WriteToMemory(uint offset, byte value)
        {
            if(offset > this.EepromMemory.Length)
            {
                this.Log(LogLevel.Error, "Cannot write to address 0x{0:X} because it is bigger than configured memory size.", offset);
                return;
            }

            this.Log(LogLevel.Debug, "0x{0:X4} = 0x{1:X2}", offset, value);

            this.EepromMemory[offset] = value;
        }

        private byte ReadFromMemory(uint offset)
        {
            if(offset > this.EepromMemory.Length)
            {
                this.Log(LogLevel.Error, "Cannot read from address 0x{0:X} because it is bigger than configured memory size.", offset);
                return 0;
            }

            return this.EepromMemory[offset];
        }

        private static IEnumerable<uint> ParseMemoryFile(string path)
        {
            var localQueue = new Queue<uint>();
            var lineNumber = 0;

            try
            {
                using(var reader = File.OpenText(path))
                {
                    var line = "";
                    var value = 0u;
                    while((line = reader.ReadLine()) != null)
                    {
                        ++lineNumber;

                        if(line.Trim().StartsWith("#"))
                        {
                            // this is a comment, just ignore
                            continue;
                        }

                        if(line.Trim().StartsWith("0x"))
                        {
                            if(!uint.TryParse(line.Trim().Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                            {
                                throw new RecoverableException($"Wrong data file format at line {lineNumber}. Expected an hexadecimal number, but got '{line}'");
                            }
                        }
                        else if(!uint.TryParse(line.Trim(), out value))
                        {
                            throw new RecoverableException($"Wrong data file format at line {lineNumber}. Expected an unsigned integer number, but got '{line}'");
                        }

                        localQueue.Enqueue(value);
                    }
                }
            }
            catch(Exception e)
            {
                if(e is RecoverableException)
                {
                    throw;
                }

                // this is to nicely handle IO errors in monitor
                throw new RecoverableException(e.Message);
            }

            return localQueue;
        }

        private byte[] EepromMemory;
        private byte ResetByte;
        private uint PageSize;
        private uint Pages;
        private uint PageAddressSize;
        private uint CurrentOffset;

        private object lockObject = new object();
    }
}
