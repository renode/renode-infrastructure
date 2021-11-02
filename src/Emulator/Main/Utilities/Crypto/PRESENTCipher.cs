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
    public class PRESENTCipher
    {
        // For cryptographic purposes use provided constants, more info: https://link.springer.com/content/pdf/10.1007%2F978-3-540-74735-2_31.pdf
        // Note that current implementation assumes 64-bit `key`, but could be extended to 80-bit or 128-bit in the future.

        static public ulong Scramble(ulong data, ulong key, int width = OriginalDataWidth, uint rounds = OriginalNumberOfRounds)
        {
            if(width < 0 || width > 64)
            {
                throw new ArgumentException($"{width} is out of bounds [0, 64]", "width");
            }
            if(data > (ulong.MaxValue >> (64 - width)))
            {
                throw new ArgumentException($"0x{data:X} doesn't fit in width ({width}) bits", "data");
            }

            var state = data;
            for(var i = 0; i < rounds; ++i)
            {
                state ^= key;
                state = Substitute(state, width, coefficientsForward);
                state = Permutate(state, width);
            }

            return state ^ key;
        }

        static public ulong Descramble(ulong data, ulong key, int width = OriginalDataWidth, uint rounds = OriginalNumberOfRounds)
        {
            if(width < 0 || width > 64)
            {
                throw new ArgumentException($"{width} is out of bounds [0, 64]", "width");
            }
            if(data > (ulong.MaxValue >> (64 - width)))
            {
                throw new ArgumentException($"0x{data:X} doesn't fit in width ({width}) bits", "data");
            }

            var state = data;
            for(var i = 0; i < rounds; ++i)
            {
                state ^= key;
                state = ReversePermutate(state, width);
                state = Substitute(state, width, coefficientsReverse);
            }

            return state ^ key;
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

        static private ulong Permutate(ulong data, int width)
        {
            var mask = ulong.MaxValue >> (64 - width);
            var permutationMask = ulong.MaxValue >> (64 - (width & ~0x1));

            var reversedData = BitHelper.ReverseBits(data << (64 - width));
            var state = reversedData & (mask & ~permutationMask);
            for(byte j = 0; j < width / 2; ++j)
            {
                BitHelper.SetBit(ref state, j, BitHelper.IsBitSet(reversedData, (byte)(2 * j)));
                BitHelper.SetBit(ref state, (byte)(width / 2 + j), BitHelper.IsBitSet(reversedData, (byte)(2 * j + 1)));
            }

            return state;
        }

        static private ulong ReversePermutate(ulong data, int width)
        {
            var mask = ulong.MaxValue >> (64 - width);
            var permutationMask = ulong.MaxValue >> (64 - (width & ~0x1));

            var state = data & (mask & ~permutationMask);
            for(byte j = 0; j < width / 2; ++j)
            {
                BitHelper.SetBit(ref state, (byte)(2 * j), BitHelper.IsBitSet(data, j));
                BitHelper.SetBit(ref state, (byte)(2 * j + 1), BitHelper.IsBitSet(data, (byte)(width / 2 + j)));
            }

            return BitHelper.ReverseBits(state << (64 - width));
        }

        private static readonly ulong[] coefficientsForward = new ulong[]
        {
            0xc, 0x5, 0x6, 0xb, 0x9, 0x0, 0xa, 0xd,
            0x3, 0xe, 0xf, 0x8, 0x4, 0x7, 0x1, 0x2
        };

        private static readonly ulong[] coefficientsReverse = new ulong[]
        {
            0x5, 0xe, 0xf, 0x8, 0xc, 0x1, 0x2, 0xd,
            0xb, 0x4, 0x6, 0x3, 0x0, 0x7, 0x9, 0xa
        };
        
        private const int OriginalDataWidth = 64;
        private const uint OriginalNumberOfRounds = 31;
    }
}
