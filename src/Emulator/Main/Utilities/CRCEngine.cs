//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Utilities
{
    public class CRCEngine
    {
        public CRCEngine(CRCType polynomial)
        {
            this.reversedPolynomial = polynomial.GetLength() == 4
                ? BitHelper.ReverseBits((uint)polynomial)
                : BitHelper.ReverseBits((ushort)polynomial); // this cast is needed to rotate only lower bits
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort CalculateCrc16(IEnumerable<byte> data, ushort initialValue)
        {
            var crc = initialValue;
            foreach(var b in data)
            {
                crc ^= b;
                for(var i = 0; i < 8; i++)
                {
                    if((crc & 1) != 0)
                    {
                        crc = (ushort)((crc >> 1) ^ reversedPolynomial);
                    }
                    else
                    {
                        crc = (ushort)(crc >> 1);
                    }
                }
            }
            return crc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint CalculateCrc32(IEnumerable<byte> data, uint initialValue)
        {
            var crc = initialValue;
            foreach(var b in data)
            {
                crc ^= b;
                for(var i = 0; i < 8; i++)
                {
                    if((crc & 1) != 0)
                    {
                        crc = ((crc >> 1) ^ reversedPolynomial);
                    }
                    else
                    {
                        crc = (crc >> 1);
                    }
                }
            }
            return crc;
        }

        private uint reversedPolynomial;
        private const ushort ushortMSB = 0x8000;
        private const uint uintMSB = 0x80000000;
    }
}
