//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Antmicro.Renode.Utilities
{
    public static class BitHelper
    {
        public static long Bits(byte b)
        {
            return (0x1 << b);
        }

        public static bool IsBitSet(uint reg, byte bit)
        {
            return ((0x1 << bit) & reg) != 0;
        }

        public static uint SetBitsFrom(uint source, uint newValue, int position, int width)
        {
            var mask = ((1u << width) - 1) << position;
            var bitsToSet = newValue & mask;
            return source | bitsToSet;
        }

        public static void ClearBits(ref uint reg, params byte[] bits)
        {
            uint mask = 0xFFFFFFFFu;
            foreach(var bit in bits)
            {
                mask -= 1u << bit;
            }
            reg &= mask;
        }

        public static void ClearBits(ref uint reg, int position, int width)
        {
            uint mask = 0xFFFFFFFFu;
            for(var i = 0; i < width; i++)
            {
                mask -= 1u << (position + i);
            }
            reg &= mask;
        }

        public static bool AreAnyBitsSet(uint reg, int position, int width)
        {
            var mask = CalculateMask(width, position);
            return (reg & mask) != 0;
        }

        public static void UpdateWithShifted(ref uint reg, uint newValue, int position, int width)
        {
            UpdateWith(ref reg, newValue << position, position, width);
        }

        public static void UpdateWith(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg = (reg & ~mask) | (newValue & mask);
        }

        public static void OrWith(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg |= (newValue & mask);
        }

        public static void AndWithNot(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg &= ~(newValue & mask);
        }

        public static void XorWith(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg ^= (newValue & mask);
        }

        public static void ClearBits(ref byte reg, params byte[] bits)
        {
            uint mask = 0xFFFFFFFFu;
            foreach(var bit in bits)
            {
                mask -= 1u << bit;
            }
            reg &= (byte)mask;
        }

        public static void ClearBitsIfSet(ref uint reg, uint testValue, params byte[] bits)
        {
            uint mask = 0xFFFFFFFFu;
            foreach(var bit in bits)
            {
                if(IsBitSet(testValue, bit))
                {
                    mask -= 1u << bit;
                }
            }
            reg &= mask;
        }

        public static void ClearBitsIfSet(ref byte reg, byte testValue, params byte[] bits)
        {
            uint mask = 0xFFFFFFFFu;
            foreach(var bit in bits)
            {
                if(IsBitSet(testValue, bit))
                {
                    mask -= 1u << bit;
                }
            }
            reg &= (byte)mask;
        }

        public static void SetBit(ref uint reg, byte bit, bool value)
        {
            if(value)
            {
                reg |= (0x1u << bit);
            }
            else
            {
                reg &= (0xFFFFFFFFu - (0x1u << bit));
            }
        }

        public static void SetBit(ref byte reg, byte bit, bool value)
        {
            if(value)
            {
                reg |= (byte)(0x1 << bit);
            }
            else
            {
                reg &= (byte)(0xFFFF - (0x1u << bit));
            }
        }

        public static IList<int> GetSetBits(uint reg)
        {
            var result = new List<int>();
            var pos = 0;
            while(reg > 0)
            {
                if((reg & 1u) == 1)
                {
                    result.Add(pos);
                }

                reg >>= 1;
                pos++;
            }

            return result;
        }

        public static string GetSetBitsPretty(uint reg)
        {
            var setBits = new HashSet<int>(GetSetBits(reg));
            if(setBits.Count == 0)
            {
                return "(none)";
            }
            var beginnings = setBits.Where(x => !setBits.Contains(x - 1)).ToArray();
            var endings = setBits.Where(x => !setBits.Contains(x + 1)).ToArray();
            return beginnings.Select((x, i) => endings[i] == x ? x.ToString() : string.Format("{0}-{1}", x, endings[i])).Stringify(", ");
        }

        public static void ForeachActiveBit(uint reg, Action<byte> action)
        {
            byte pos = 0;
            while(reg > 0)
            {
                if((reg & 1u) == 1)
                {
                    action(pos);
                }

                reg >>= 1;
                pos++;
            }

        }

        public static bool[] GetBits(uint reg)
        {
            var result = new bool[32];
            for(var i = 0; i < 32; ++i)
            {
                result[i] = (reg & 1u) == 1;
                reg >>= 1;
            }
            return result;
        }

        public static uint GetValue(uint reg, int offset, int size)
        {
            return (uint)((reg >> offset) & ((0x1ul << size) - 1));
        }

        public static uint GetValueFromBitsArray(IEnumerable<bool> array)
        {
            var ret = 0u;
            var i = 0;
            foreach(var item in array)
            {
                if(item)
                {
                    ret |= 1u << i;
                }
                i++;
            }
            return ret;
        }

        public static uint GetValueFromBitsArray(params bool[] array)
        {
            return GetValueFromBitsArray((IEnumerable<bool>)array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CalculateMask(int width, int position)
        {
            if(width == 32 && position == 0)
            {
                return uint.MaxValue;
            }
            return (1u << width) - 1 << position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReverseBits (byte b)
        {
            return (byte)(((b << 7) & 0x80) | ((b << 5) & 0x40) | ((b << 3) & 0x20) | ((b << 1) & 0x10) |
                          ((b >> 1) & 0x08) | ((b >> 3) & 0x04) | ((b >> 5) & 0x02) | ((b >> 7) & 0x01));
        }
    }
}

