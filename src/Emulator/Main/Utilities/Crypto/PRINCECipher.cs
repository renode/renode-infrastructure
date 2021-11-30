//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Utilities
{
    public class PRINCECipher
    {
        // For cryptographic purposes use provided constants, more info: https://eprint.iacr.org/2012/529.pdf

        static public ulong Scramble(ulong data, ulong k1, ulong k0, int width = OriginalDataWidth, uint rounds = NumberOfRounds)
        {
            if(rounds > NumberOfRounds)
            {
                throw new ArgumentException($"{rounds} is greater than proper number of rounds ({NumberOfRounds})", "rounds");
            }
            if(width < 0 || width > 64)
            {
                throw new ArgumentException($"{width} is out of bounds [0, 64]", "width");
            }
            if(data > (ulong.MaxValue >> (64 - width)))
            {
                throw new ArgumentException($"0x{data:X} doesn't fit in width ({width}) bits", "data");
            }

            var k0Prime = (((k0 & 0x1) << 63) | (k0 >> 1)) ^ (k0 >> 63);
            var state = data ^ k0 ^ k1 ^ roundConstant[0];

            for(var i = 1; i < rounds / 2; ++i)
            {
                state = Substitute(state, 64, coefficientsForward);
                state = Multiply(state);
                state = ShiftRows(state, false);
                state ^= roundConstant[i];
                state ^= (i & 0x1) == 0x1 ? k0 : k1;
            }

            state = Substitute(state, 64, coefficientsForward);
            state = Multiply(state);
            state = Substitute(state, 64, coefficientsReverse);

            for(var i = NumberOfRounds - rounds / 2 + 1; i < NumberOfRounds; ++i)
            {
                state ^= (i & 0x1) == 0x1 ? k1 : k0;
                state ^= roundConstant[i];
                state = ShiftRows(state, true);
                state = Multiply(state);
                state = Substitute(state, 64, coefficientsReverse);
            }

            return state ^ k0Prime ^ k1 ^ roundConstant[NumberOfRounds];
        }

        static private ulong Substitute(ulong data, int width, ulong[] coefficients)
        {
            var mask = ulong.MaxValue >> (64 - width);
            var substitutionMask = ulong.MaxValue >> (64 - (width & ~0x3));

            var state = data & (mask & ~substitutionMask);
            for(int i = 0; i < width / 4; ++i)
            {
                var shift = i * 4;
                state |= coefficients[(data >> shift) & 0xf] << shift;
            }

            return state;
        }

        static private ulong Multiply(ulong data)
        {
            var state = 0UL;
            for(var i = 0; i < 4; ++i)
            {
                // segment := data[i * 16 .. (i + 1) * 16 - 1] * block[i]
                var segment = 0ul;
                for(int j = 0; j < 16; ++j)
                {
                    if(BitHelper.IsBitSet(data, (byte)(j + (i * 16))))
                    {
                        segment ^= m[i][j];
                    }
                }

                state |= segment << (i * 16);
            }
            return state;
        }

        static private ulong ShiftRows(ulong data, bool inverse)
        {
            // 0x0123456789ABCDEF -> 0x05AF49E38D27C16B
            var mask = 0xF000F000F000F000;
            var state = 0ul;
            for(var i = 0; i < 4; ++i)
            {
                var row = data & (mask >> (i * 4));
                var shift = (inverse ? i : (4 - i)) * 16;
                state |= (row >> shift) | (row << (64 - shift));
            }
            return state;
        }

        private static readonly ulong[] roundConstant = new ulong[]
        {
            0x0000000000000000, 0x13198a2e03707344,
            0xa4093822299f31d0, 0x082efa98ec4e6c89,
            0x452821e638d01377, 0xbe5466cf34e90c6c,
            0x7ef84f78fd955cb1, 0x85840851f1ac43aa,
            0xc882d32f25323c54, 0x64a51195e0e3610d,
            0xd3b5a399ca0c2399, 0xc0ac29b7c97c50dd
        };

        private static readonly ulong[] coefficientsForward = new ulong[]
        {
            0xb, 0xf, 0x3, 0x2, 0xa, 0xc, 0x9, 0x1,
            0x6, 0x7, 0x8, 0x0, 0xe, 0x5, 0xd, 0x4
        };

        private static readonly ulong[] coefficientsReverse = new ulong[]
        {
            0xb, 0x7, 0x3, 0x2, 0xf, 0xd, 0x8, 0x9,
            0xa, 0x6, 0x4, 0x0, 0x5, 0xe, 0xc, 0x1
        };

        // mX represents M^(X) matrix; mX[n] is n-th column of M^(0) matrix
        // each digit is one of the columns of M_0 to M_3 bit matrix
        private static readonly ulong[] m0 = new ulong[]
        {
            0x0111, 0x2220, 0x4404, 0x8088, 0x1011, 0x0222, 0x4440, 0x8808,
            0x1101, 0x2022, 0x0444, 0x8880, 0x1110, 0x2202, 0x4044, 0x0888
        };

        private static readonly ulong[] m1 = new ulong[]
        {
           0x1110, 0x2202, 0x4044, 0x0888, 0x0111, 0x2220, 0x4404, 0x8088,
           0x1011, 0x0222, 0x4440, 0x8808, 0x1101, 0x2022, 0x0444, 0x8880
        };

        // m[n] is a diagonal block of M' matrix
        private static readonly ulong[][] m = new ulong[][] { m0, m1, m1, m0 };

        private const int OriginalDataWidth = 64;
        private const uint NumberOfRounds = 11;
    }
}
