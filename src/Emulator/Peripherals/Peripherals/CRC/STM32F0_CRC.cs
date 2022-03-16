//
// Copyright (c) 2022 Pieter Agten
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.CRC
{
    public class STM32F0_CRC : IBytePeripheral, IWordPeripheral, IDoubleWordPeripheral, IKnownSize
    {
        public static byte PolySizeToCrcBits(STM32F0_CRC.PolySize polySize)
        {
            switch(polySize)
            {
            case STM32F0_CRC.PolySize.CRC32:
                return 32;
            case STM32F0_CRC.PolySize.CRC16:
                return 16;
            case STM32F0_CRC.PolySize.CRC8:
                return 8;
            case STM32F0_CRC.PolySize.CRC7:
                return 7;
            default:
                return 0;
            }
        }

        private static UInt32 ReverseValue(STM32F0_CRC.PolySize polySize, UInt32 value)
        {
            switch(polySize)
            {
            case STM32F0_CRC.PolySize.CRC32:
                return BitReversal.ByDoubleWord.ReverseDoubleWord(value);
            case STM32F0_CRC.PolySize.CRC16:
                return BitReversal.ByDoubleWord.ReverseWord((UInt16)value);
            case STM32F0_CRC.PolySize.CRC8:
                return BitReversal.ByDoubleWord.ReverseByte((Byte)value);
            case STM32F0_CRC.PolySize.CRC7:
                return (byte)(BitReversal.ByDoubleWord.ReverseByte((Byte)value) >> 1);
            default:
                return 0;
            }
        }
        
        public STM32F0_CRC(bool configurablePoly)
        {
            this.configurablePoly = configurablePoly;
            this.control = new ControlRegister();
            Reset();
        }

        public byte ReadByte(long offset)
        {
            if ((Registers)offset == Registers.Data) {
                return (byte)this.GetOutput();
            }
            // All other registers must be read as double words
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteByte(long offset, byte value)
        {
            if ((Registers)offset == Registers.Data) {
                this.DigestByte(value);
            } else {
                // All other registers must be written as double words
                this.LogUnhandledWrite(offset, value);
            }
        }

        public ushort ReadWord(long offset)
        {
            if ((Registers)offset == Registers.Data) {
                return (ushort)this.GetOutput();
            }
            // All other registers must be read as double words
            this.LogUnhandledRead(offset);
            return 0;
        }

        public void WriteWord(long offset, ushort value)
        {
            if ((Registers)offset == Registers.Data) {
                this.DigestWord(value);
            } else {
                // All other registers must be written as double words
                this.LogUnhandledWrite(offset, value);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            switch((Registers)offset)
            {
            case Registers.Data:
                return this.GetOutput();
            case Registers.IndependentData:
                return this.independentData;
            case Registers.Control:
                return this.control.GetValue();
            case Registers.InitialValue:
                return this.initialValue;
            case Registers.Polynomial:
                return this.polynomial;
            default:
                this.LogUnhandledRead(offset);
                return 0;
            }
        }
        public void WriteDoubleWord(long offset, uint value)
        {
            switch((Registers)offset)
            {
            case Registers.Data:
                this.DigestDoubleWord(value);
                break;
            case Registers.IndependentData:
                if ((value & 0xFFFFFF00) != 0)
                {
                    this.Log(LogLevel.Warning, "Write to reserved bits of CRC_IDR register, offset 0x{0:X}, value 0x{1:X}.", offset, value);
                }
                this.independentData = value;
                break;
            case Registers.Control:
                this.control.SetValue(value);
                if(this.control.ResetRequest == true)
                {
                    ResetCalculation();
                }
                break;
            case Registers.InitialValue:
                this.initialValue = value;
                break;
            case Registers.Polynomial:
                if (this.configurablePoly) {
                    this.polynomial = value;
                } else {
                    this.LogUnhandledWrite(offset, value);
                }
                break;
            default:
                this.LogUnhandledWrite(offset, value);
                break;
            }
        }

        public void Reset()
        {
            this.data = 0xFFFFFFFF;
            this.initialValue = 0xFFFFFFFF;
            this.polynomial = DEFAULT_POLYNOMIAL;
            this.control.SetValue(0);
            this.independentData = 0;
        }

        public long Size => 0x400;

        private void DigestByte(byte value)
        {
            value = this.control.ReverseInput.ReverseByte(value);
            this.Digest(value);
        }

        private void DigestWord(UInt16 value)
        {
            value = this.control.ReverseInput.ReverseWord(value);
            this.Digest((byte)(value >> 8));
            this.Digest((byte)value);
        }

        private void DigestDoubleWord(UInt32 value)
        {
            value = this.control.ReverseInput.ReverseDoubleWord(value);
            this.Digest((byte)(value >> 24));
            this.Digest((byte)(value >> 16));
            this.Digest((byte)(value >> 8));
            this.Digest((byte)value);
        }

        private void Digest(byte value)
        {
            EnsureTableUpToDate();
            this.data ^= ((UInt32)value) << 24;
            byte table_index = (byte)(this.data >> 24);
            this.data <<= 8;
            this.data ^= this.table.GetValue(table_index);
        }

        private void EnsureTableUpToDate()
        {
            if (this.table == null || 
                this.table.Polynomial != this.polynomial || 
                this.table.PolySize != this.control.PolySize)
            {
                this.table = new CrcTable(this.polynomial, this.control.PolySize);
            }
        }

        private UInt32 GetOutput()
        {
            UInt32 value = this.data >> (32 - PolySizeToCrcBits(this.control.PolySize));
            if (this.control.ReverseOutput)
            {
                value = ReverseValue(this.control.PolySize, value);
            }

            return value;
        }       

        private void ResetCalculation()
        {
            this.data = this.initialValue << (32 - PolySizeToCrcBits(this.control.PolySize));
            this.control.ResetRequest = false;
        }
        

        private readonly bool configurablePoly;

        private UInt32 data;

        private UInt32 initialValue;

        private UInt32 polynomial;

        private ControlRegister control;

        private UInt32 independentData;

        private CrcTable table;

        private const UInt32 DEFAULT_POLYNOMIAL = 0x04C11DB7;

        private enum Registers
        {
            Data = 0x0,            // CRC_DR
            IndependentData = 0x4, // CRC_IDR
            Control = 0x8,         // CRC_CR
            InitialValue = 0x10,   // CRC_INIT
            Polynomial = 0x14      // CRC_POL
        }

        public enum PolySize
        {
            CRC32 = 0b00,
            CRC16 = 0b01,
            CRC8 = 0b10,
            CRC7 = 0b11,
        }

        private class ControlRegister
        {
            public void SetValue(uint value)
            {
                ReverseOutput = (value & REV_OUT) != 0;
                ReverseInput  = BitReversal.FromValue((BitReversal.Type)((value >> REV_IN_SHIFT) & REV_IN_MASK));
                PolySize = (PolySize)((value >> POLYSIZE_SHIFT) & POLYSIZE_MASK);
                ResetRequest  = (value & RESET) != 0;
            }

            public uint GetValue()
            {
                var retVal =
                    (ReverseOutput                      ? REV_OUT : 0)     |
                    (((UInt32)ReverseInput.ToValue())  << REV_IN_SHIFT)    |
                    (((UInt32)PolySize)                << POLYSIZE_SHIFT)  |
                    (ResetRequest                       ? RESET : 0);
                return retVal;
            }

            public bool ReverseOutput;
            public BitReversal ReverseInput;
            public PolySize PolySize;
            public bool ResetRequest;

            private const uint REV_OUT        = (1u << 7);
            private const uint REV_IN_MASK    = 0b11;
            private const int REV_IN_SHIFT    = 5;
            private const int POLYSIZE_MASK   = 0b11;
            private const int POLYSIZE_SHIFT  = 3;
            private const uint RESET          = (1u << 0);
        }
    }

    public abstract class BitReversal
    {
        public static BitReversal FromValue(Type value)
        {
            switch(value)
            {
            case Type.Disabled:
                return Disabled;
            case Type.ByByte:
                return ByByte;
            case Type.ByWord:
                return ByWord;
            case Type.ByDoubleWord:
                return ByDoubleWord;
            default:
                Console.WriteLine("INVALID BITREVERSAL VALUE " + value);
                return null;
            }
        }

        public static BitReversal Disabled = new BitReversalDisabled();
        public static BitReversal ByByte = new BitReversalByByte();
        public static BitReversal ByWord = new BitReversalByWord();
        public static BitReversal ByDoubleWord = new BitReversalByDoubleWord();
        
        protected static UInt32 Reverse8(UInt32 v)
        {
            v = ((v >> 1) & 0x55555555) | ((v & 0x55555555) << 1); // swap odd and even bits
            v = ((v >> 2) & 0x33333333) | ((v & 0x33333333) << 2); // swap consecutive pairs of bits
            v = ((v >> 4) & 0x0F0F0F0F) | ((v & 0x0F0F0F0F) << 4); // swap nibbles ... 
            return v;
        }

        protected static UInt32 Reverse16(UInt32 v)
        {
            v = Reverse8(v);
            v = ((v >> 8) & 0x00FF00FF) | ((v & 0x00FF00FF) << 8); // swap bytes
            return v;
        }

        protected static UInt32 Reverse32(UInt32 v)
        {
            v = Reverse16(v);
            v = (v >> 16) | (v << 16); // swap 16-bit pairs
            return v;
        }


        public abstract UInt32 ReverseDoubleWord(UInt32 v);

        public abstract UInt16 ReverseWord(UInt16 v);

        public abstract byte ReverseByte(byte v);

        public abstract Type ToValue();

        public enum Type
        {
            Disabled = 0b00,
            ByByte = 0b01,
            ByWord = 0b10,
            ByDoubleWord = 0b11
        }

        private class BitReversalDisabled : BitReversal
        {
            public override UInt32 ReverseDoubleWord(UInt32 v)
            {
                return v;
            }

            public override UInt16 ReverseWord(UInt16 v)
            {
                return v;
            }

            public override byte ReverseByte(byte v)
            {
                return v;
            }

            public override Type ToValue()
            {
                return Type.Disabled;
            }
        }

        private class BitReversalByByte : BitReversal
        {
            public override UInt32 ReverseDoubleWord(UInt32 v)
            {
                return Reverse8(v);
            }

            public override UInt16 ReverseWord(UInt16 v)
            {
                return (UInt16)Reverse8((UInt32)v);
            }

            public override byte ReverseByte(byte v)
            {
                return (byte)Reverse8((UInt32)v);
            }

            public override Type ToValue()
            {
                return Type.ByByte;
            }
        }

        private class BitReversalByWord : BitReversal
        {
            public override UInt32 ReverseDoubleWord(UInt32 v)
            {
                return Reverse16(v);
            }

            public override UInt16 ReverseWord(UInt16 v)
            {
                return (UInt16)Reverse16((UInt32)v);
            }

            public override byte ReverseByte(byte v)
            {
                return (byte)Reverse8((UInt32)v);
            }

            public override Type ToValue()
            {
                return Type.ByWord;
            }
        }

        private class BitReversalByDoubleWord : BitReversal
        {
            public override UInt32 ReverseDoubleWord(UInt32 v)
            {
                return Reverse32(v);
            }

            public override UInt16 ReverseWord(UInt16 v)
            {
                return (UInt16)Reverse16((UInt32)v);
            }

            public override byte ReverseByte(byte v)
            {
                return (byte)Reverse8((UInt32)v);
            }

            public override Type ToValue()
            {
                return Type.ByDoubleWord;
            }
        }
    }

    public class CrcTable
    {
        public CrcTable(UInt32 polynomial, STM32F0_CRC.PolySize polySize)
        {
            this.polynomial = polynomial;
            this.polySize = polySize;
            this.table = GenerateTable(polynomial, STM32F0_CRC.PolySizeToCrcBits(polySize));
        }

        private static UInt32[] GenerateTable(UInt32 polynomial, byte crcBits)
        {
            UInt32[] result = new UInt32[256];
            UInt32 shiftedPoly = polynomial << (32 - crcBits);
            for (UInt32 dividend = 0; dividend < 256; ++dividend)
            {
                UInt32 value = dividend << 24;
                for (var bit = 0; bit < 8; ++bit)
                {
                    value = (value << 1) ^ (unchecked((UInt32)(-((value & 0x80000000) >> 31))) & shiftedPoly);
                }
                result[dividend] = value;
            }
            return result;
        }

        public UInt32 Polynomial { get { return polynomial; }  }

        public STM32F0_CRC.PolySize PolySize { get { return polySize; }  }

        public UInt32 GetValue(byte idx)
        {
            return this.table[idx];
        }

        private readonly UInt32 polynomial;

        private readonly STM32F0_CRC.PolySize polySize;

        private readonly UInt32[] table;
    }
}