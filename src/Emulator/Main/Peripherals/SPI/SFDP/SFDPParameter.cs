//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI.SFDP
{
    /// <summary>
    /// Base class for all SFDP parameters.
    /// </summary>
    /// <remarks>
    /// Every class inheriting from <see cref="SFDPParameter"/>
    /// must register its record in the
    /// <c>ParameterIdToSFDPParameterDictionary</c> in <see cref="SFDPData"/> class
    /// in order to be correctly recognized and decoded
    /// by the SFDP decoding mechanism.
    /// </remarks>
    public abstract class SFDPParameter
    {
        public SFDPParameter(byte major, byte minor)
        {
            Major = major;
            Minor = minor;
        }

        public byte[] Header(uint parameterTablePointer)
        {
            if(parameterTablePointer % 4 != 0)
            {
                throw new Exception($"SFPD Parameter Table Pointer length is not aligned to DWORD");
            }
            if(parameterTablePointer > SFDPData.PTPMaxValue)
            {
                throw new Exception($"SFDP Parameter Table Pointer length ({parameterTablePointer}) exceeeds {SFDPData.PTPMaxValue}");
            }
            var binaryHeader = new byte[2 * DWord];
            binaryHeader[0] = ParameterIdLsb;
            binaryHeader[1] = Minor;
            binaryHeader[2] = Major;
            var lengthDw = (uint)this.Bytes.Length.DivCeil(4);
            if(lengthDw > byte.MaxValue)
            {
                throw new Exception($"SFDP parameter length ({lengthDw}) exceeeds {byte.MaxValue} bytes.");
            }
            binaryHeader[3] = (byte)lengthDw;
            binaryHeader[DWord] = (byte)parameterTablePointer;
            binaryHeader[DWord + 1] = (byte)(parameterTablePointer >> 8);
            binaryHeader[DWord + 2] = (byte)(parameterTablePointer >> 16);
            binaryHeader[DWord + 3] = ParameterIdMsb;

            return binaryHeader;
        }

        public byte[] Bytes
        {
            get
            {
                if(bytes == null)
                {
                    bytes = ToBytes();
                }
                return bytes;
            }
        }

        public byte Major { get; set; }

        public byte Minor { get; set; }

        public abstract byte ParameterIdMsb { get; }

        public abstract byte ParameterIdLsb { get; }

        protected abstract byte[] ToBytes();

        protected const byte DWord = 4;

        private byte[] bytes = null;
    }
}
