using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class MH1903_CRC : BasicDoubleWordPeripheral, IBytePeripheral, IKnownSize
    {
        public MH1903_CRC(IMachine machine) : base(machine)
        {
            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            crcValue = 0;
            initialValue = 0;
            polynomialSelect = false;
            typeSelect = false;
            reverseInput = false;
            reverseOutput = false;
            xorOutput = false;
        }

        public void WriteByte(long offset, byte value)
        {
            // CrcDataRegister (offset 0x08) accepts byte writes for feeding data
            if(offset == (long)Registers.CrcDataRegister)
            {
                ProcessByte(value);
                return;
            }

            // For other registers, use read-modify-write
            long alignedOffset = offset & ~3;
            int byteOffset = (int)(offset & 3);
            uint currentValue = (uint)RegistersCollection.Read(alignedOffset);
            uint mask = (uint)(0xFF << (byteOffset * 8));
            uint newValue = (currentValue & ~mask) | ((uint)value << (byteOffset * 8));
            RegistersCollection.Write(alignedOffset, newValue);
        }

        public byte ReadByte(long offset)
        {
            // Read the doubleword and extract the appropriate byte
            uint value = (uint)RegistersCollection.Read(offset & ~3);
            int byteOffset = (int)(offset & 3);
            return (byte)((value >> (byteOffset * 8)) & 0xFF);
        }

        public long Size => 0x100;

        private static byte ReverseBits(byte b)
        {
            b = (byte)((b & 0xF0) >> 4 | (b & 0x0F) << 4);
            b = (byte)((b & 0xCC) >> 2 | (b & 0x33) << 2);
            b = (byte)((b & 0xAA) >> 1 | (b & 0x55) << 1);
            return b;
        }

        private static ushort ReverseBits16(ushort value)
        {
            ushort result = 0;
            for(int i = 0; i < 16; i++)
            {
                result = (ushort)((result << 1) | (value & 1));
                value >>= 1;
            }
            return result;
        }

        private static uint ReverseBits32(uint value)
        {
            uint result = 0;
            for(int i = 0; i < 32; i++)
            {
                result = (result << 1) | (value & 1);
                value >>= 1;
            }
            return result;
        }

        private void DefineRegisters()
        {
            Registers.CrcControlStatusRegister.Define(this, resetValue: 0x00000000)
                .WithFlag(0, name: "PolynomialSelect",
                    writeCallback: (_, value) => polynomialSelect = value,
                    valueProviderCallback: _ => polynomialSelect)
                .WithFlag(1, name: "TypeSelect",
                    writeCallback: (_, value) =>
                    {
                        typeSelect = value;
                        // When switching modes, reinitialize CRC
                        crcValue = initialValue;
                    },
                    valueProviderCallback: _ => typeSelect)
                .WithFlag(2, name: "ReverseInputSelect",
                    writeCallback: (_, value) => reverseInput = value,
                    valueProviderCallback: _ => reverseInput)
                .WithFlag(3, name: "ReverseOutputSelect",
                    writeCallback: (_, value) => reverseOutput = value,
                    valueProviderCallback: _ => reverseOutput)
                .WithFlag(4, name: "XorOutputSelect",
                    writeCallback: (_, value) => xorOutput = value,
                    valueProviderCallback: _ => xorOutput)
                .WithReservedBits(5, 27);

            Registers.CrcInitializationValueRegister.Define(this, resetValue: 0x00000000)
                .WithValueField(0, 32, FieldMode.Write, name: "CrcInitializationValue",
                    writeCallback: (_, value) =>
                    {
                        initialValue = (uint)value;
                        crcValue = initialValue;
                        this.Log(LogLevel.Debug, "CRC initialized to 0x{0:X}", initialValue);
                    });

            // CrcDataRegister at 0x08
            // Write (byte): DataInput - Feed data byte into CRC
            // Read (32-bit): DataOutput - Read CRC result
            RegistersCollection.DefineRegister((long)Registers.CrcDataRegister)
                .WithValueField(0, 32,
                    writeCallback: (_, value) =>
                    {
                        // Only lowest byte is used for input
                        byte inputByte = (byte)(value & 0xFF);
                        ProcessByte(inputByte);
                    },
                    valueProviderCallback: _ => GetCRCResult());
        }

        private void ProcessByte(byte data)
        {
            byte processedByte = data;

            // Reverse input bits if needed
            if(reverseInput)
            {
                processedByte = ReverseBits(processedByte);
            }

            if(typeSelect)
            {
                // CRC32 mode
                ProcessByteCRC32(processedByte);
            }
            else
            {
                // CRC16 mode
                ProcessByteCRC16(processedByte);
            }
        }

        private void ProcessByteCRC16(byte data)
        {
            uint polynomial = polynomialSelect ? (uint)0x1021 : (uint)0x8005;

            // CRC16 uses only lower 16 bits
            ushort crc16 = (ushort)(crcValue & 0xFFFF);

            crc16 ^= (ushort)(data << 8);

            for(int i = 0; i < 8; i++)
            {
                if((crc16 & 0x8000) != 0)
                {
                    crc16 = (ushort)((crc16 << 1) ^ polynomial);
                }
                else
                {
                    crc16 = (ushort)(crc16 << 1);
                }
            }

            crcValue = crc16;
        }

        private void ProcessByteCRC32(byte data)
        {
            const uint polynomial = 0x04C11DB7;

            crcValue ^= (uint)(data << 24);

            for(int i = 0; i < 8; i++)
            {
                if((crcValue & 0x80000000) != 0)
                {
                    crcValue = (crcValue << 1) ^ polynomial;
                }
                else
                {
                    crcValue = crcValue << 1;
                }
            }
        }

        private uint GetCRCResult()
        {
            uint result = crcValue;

            if(typeSelect)
            {
                // CRC32 - use full 32 bits
                if(reverseOutput)
                {
                    result = ReverseBits32(result);
                }

                if(xorOutput)
                {
                    result ^= 0xFFFFFFFF;
                }
            }
            else
            {
                // CRC16 - use only lower 16 bits
                ushort result16 = (ushort)(result & 0xFFFF);

                if(reverseOutput)
                {
                    result16 = ReverseBits16(result16);
                }

                result = result16;
            }

            return result;
        }

        private uint crcValue;
        private uint initialValue;
        private bool polynomialSelect;  // false=0x8005, true=0x1021
        private bool typeSelect;        // false=CRC16, true=CRC32
        private bool reverseInput;
        private bool reverseOutput;
        private bool xorOutput;

        private enum Registers : long
        {
            CrcControlStatusRegister = 0x00,
            CrcInitializationValueRegister = 0x04,
            CrcDataRegister = 0x08,
        }
    }
}
