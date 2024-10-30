//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2022 Pieter Agten
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public class CRCEngine
    {
        public CRCEngine(CRCConfig crcConfig)
        {
            this.crcConfig = crcConfig;
            Reset();
        }

        public CRCEngine(uint polynomial, int crcWidth, bool reflectInput = true, bool reflectOutput = true,
            uint init = 0, uint xorOutput = 0)
            : this(new CRCConfig(polynomial, crcWidth, reflectInput, reflectOutput, init, xorOutput))
        {
        }

        public CRCEngine(CRCPolynomial polynomial, bool reflectInput = true, bool reflectOutput = true,
            uint init = 0, uint xorOutput = 0)
            : this(new CRCConfig(polynomial, reflectInput, reflectOutput, init, xorOutput))
        {
        }

        public void Update(IEnumerable<byte> data)
        {
            foreach(var dataByte in data)
            {
                var input = (crcConfig.ReflectInput)
                    ? BitHelper.ReverseBits(dataByte)
                    : (uint)dataByte;

                crc ^= input << 24;
                var table_index = (byte)(crc >> 24);
                crc = (crc << 8) ^ Table[table_index];
            }
        }

        public void Reset()
        {
            RawValue = crcConfig.Init;
        }
        
        public uint Calculate(IEnumerable<byte> data)
        {
            Reset();
            Update(data);
            return Value;
        }

        public CRCConfig Config => crcConfig;

        // This is the value without output reflection and output xoring
        public uint RawValue
        {
            get => crc >> (CRCConfig.MaxCRCWidth - crcConfig.Width);
            set
            {
                crc = value << (CRCConfig.MaxCRCWidth - crcConfig.Width);
            }
        }

        public uint Value
        {
            get
            {
                var crcAfterReflecting = (crcConfig.ReflectOutput)
                    ? BitHelper.ReverseBits(crc)
                    : RawValue;
                
                return crcAfterReflecting ^ crcConfig.XorOutput;
            }
        }

        private static uint[] GenerateLookupTable(CRCPolynomial crcPolynomial)
        {
            if(tablesCache.ContainsKey(crcPolynomial) && tablesCache[crcPolynomial] != null)
            {
                return tablesCache[crcPolynomial];
            }

            var table = new uint[CRCTableSize];
            var shiftedPoly = crcPolynomial.Polynomial << (CRCConfig.MaxCRCWidth - crcPolynomial.Width);

            for(var dividend = 0u; dividend < table.Length; dividend++)
            {
                var value = dividend << 24;
                for(var bit = 0; bit < 8; bit++)
                {
                    if((value & 0x80000000) != 0)
                    {
                        value <<= 1;
                        value ^= shiftedPoly;
                    }
                    else
                    {
                        value <<= 1;
                    }
                }
                table[dividend] = value;
            }

            if(tablesCache.ContainsKey(crcPolynomial))
            {
                tablesCache[crcPolynomial] = table;
            }

            return table;
        }

        private static readonly Dictionary<CRCPolynomial, uint[]> tablesCache = new Dictionary<CRCPolynomial, uint[]>()
        {
            { CRCPolynomial.CRC32, null },
            { CRCPolynomial.CRC32C, null },
            { CRCPolynomial.CRC16, null },
            { CRCPolynomial.CRC16_CCITT, null },
            { CRCPolynomial.CRC8_CCITT, null },
            { CRCPolynomial.CRC7, null }
        };
        
        private uint[] Table
        {
            get
            {
                if(table == null)
                {
                    table = GenerateLookupTable(crcConfig.CRCPolynomial);
                }
                return table;
            }
        }

        private uint crc;
        private uint[] table;
        
        private readonly CRCConfig crcConfig;

        private const int CRCTableSize = 256;
    }

    public struct CRCConfig
    {
        public CRCConfig(uint polynomial, int width, bool reflectInput, bool reflectOutput, 
            uint init, uint xorOutput)
            : this(new CRCPolynomial(polynomial, width), reflectInput, reflectOutput, init, xorOutput)
        {            
        }

        public CRCConfig(CRCPolynomial crcPolynomial, bool reflectInput, bool reflectOutput, 
            uint init, uint xorOutput)
        {
            CRCPolynomial = crcPolynomial;
            ReflectInput = reflectInput;
            ReflectOutput = reflectOutput;
            BitHelper.ClearBits(ref init, crcPolynomial.Width, MaxCRCWidth - crcPolynomial.Width);
            Init = init;
            BitHelper.ClearBits(ref xorOutput, crcPolynomial.Width, MaxCRCWidth - crcPolynomial.Width);
            XorOutput = xorOutput;
        }

        public CRCPolynomial CRCPolynomial { get; }
        public bool ReflectInput { get; }
        public bool ReflectOutput { get; }
        public uint Init { get; }
        public uint XorOutput { get; }

        public uint Polynomial => CRCPolynomial.Polynomial;

        public int Width => CRCPolynomial.Width;

        public const int MaxCRCWidth = 32;
    }

    public struct CRCPolynomial
    {
        public CRCPolynomial(uint polynomial, int width)
        {
            if(width < 1 || width > 32)
            {
                throw new ArgumentOutOfRangeException($"CRCConfig: {width} is incorrect width value");
            }
            if(BitHelper.GetMostSignificantSetBitIndex(polynomial) + 1 > width)
            {
                throw new ArgumentException($"CRCConfig: width ({width}) is too small for given polynomial 0x{polynomial:X}.");
            }
            
            Polynomial = polynomial;
            Width = width;
        }

        public uint Polynomial { get; }
        public int Width { get; }

        public int WidthInBytes 
        {
            get
            {
                return Width % 8 == 0
                    ? Width / 8
                    : Width / 8 + 1;
            }
        }

        public static readonly CRCPolynomial CRC32 = new CRCPolynomial(0x04C11DB7, 32);
        public static readonly CRCPolynomial CRC32C = new CRCPolynomial(0x1EDC6F41, 32);
        public static readonly CRCPolynomial CRC16 = new CRCPolynomial(0x8005, 16);
        public static readonly CRCPolynomial CRC16_CCITT = new CRCPolynomial(0x1021, 16);
        public static readonly CRCPolynomial CRC8_CCITT = new CRCPolynomial(0x07, 8);
        public static readonly CRCPolynomial CRC7 = new CRCPolynomial(0x09, 7);
    }
}
