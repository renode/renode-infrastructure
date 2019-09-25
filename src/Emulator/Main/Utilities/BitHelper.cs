//
// Copyright (c) 2010-2018 Antmicro
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
        public static int GetMostSignificantSetBitIndex(ulong value)
        {
            var mask = 1UL << 63;
            var result = 63;

            while(mask != 0)
            {
                if((value & mask) != 0)
                {
                    return result;
                }

                mask >>= 1;
                result--;
            }

            return -1;
        }

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

        public static ulong SetBitsFrom(ulong source, ulong newValue, int position, int width)
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

        public static void ReplaceBits(ref uint destination, uint source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            uint mask = (1u << width) - 1;
            source &= mask << sourcePosition;
            destination &= ~(mask << destinationPosition);

            var positionDifference = sourcePosition - destinationPosition;
            destination |= (positionDifference >= 0)
                ? (source >> positionDifference)
                : (source << -positionDifference);
        }

        public static byte ReplaceBits(this byte destination, byte source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            var mask = (byte)((1u << width) - 1);
            source &= (byte)(mask << sourcePosition);
            destination &= (byte)(~(mask << destinationPosition));

            var positionDifference = sourcePosition - destinationPosition;
            return (byte)(destination | ((positionDifference >= 0)
                ? (byte)(source >> positionDifference)
                : (byte)(source << -positionDifference)));
        }

        public static uint ReplaceBits(this uint destination, uint source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            uint mask = (1u << width) - 1;
            source &= mask << sourcePosition;
            destination &= ~(mask << destinationPosition);

            var positionDifference = sourcePosition - destinationPosition;
            return destination | ((positionDifference >= 0)
                ? (source >> positionDifference)
                : (source << -positionDifference));
        }

        public static ulong ReplaceBits(this ulong destination, ulong source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            ulong mask = (1u << width) - 1;
            source &= mask << sourcePosition;
            destination &= ~(mask << destinationPosition);

            var positionDifference = sourcePosition - destinationPosition;
            return destination | ((positionDifference >= 0)
                ? (source >> positionDifference)
                : (source << -positionDifference));
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

        public static void UpdateWithShifted(ref ulong reg, ulong newValue, int position, int width)
        {
            UpdateWith(ref reg, newValue << position, position, width);
        }

        public static void UpdateWith(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg = (reg & ~mask) | (newValue & mask);
        }

        public static void UpdateWith(ref ulong reg, ulong newValue, int position, int width)
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

        public static ulong GetValue(ulong reg, int offset, int size)
        {
            return (ulong)((reg >> offset) & ((0x1ul << size) - 1));
        }

        public static uint GetMaskedValue(uint reg, int maskOffset, int maskSize)
        {
            var mask = ((0x1u << maskSize) - 1);
            return reg & (mask << maskOffset);
        }

        public static void SetMaskedValue(ref uint reg, uint value, int maskOffset, int maskSize)
        {
            var mask = ((0x1u << maskSize) - 1);
            value &= mask;
            reg &= ~(mask << maskOffset);
            reg |= value << maskOffset;
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

        public static uint ToUInt32(byte[] data, int index, int length, bool reverse)
        {
            uint result = 0;
            if(index + length > data.Length)
            {
                throw new ArgumentException("index/length combination exceeds buffer length");
            }
            for(var i = 0; i < length; i++)
            {
                result = (result << 8) | data[index + (reverse ? length - 1 - i : i)];
            }
            return result;
        }

        public static ushort ToUInt16(byte[] data, int index, bool reverse)
        {
            return (ushort)ToUInt32(data, index, 2, reverse);
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
        public static byte ReverseBits(byte b)
        {
            return (byte)(((b << 7) & 0x80) | ((b << 5) & 0x40) | ((b << 3) & 0x20) | ((b << 1) & 0x10) |
                          ((b >> 1) & 0x08) | ((b >> 3) & 0x04) | ((b >> 5) & 0x02) | ((b >> 7) & 0x01));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReverseBits(ushort s)
        {
            return (ushort)((ReverseBits((byte)s) << 8) | ReverseBits((byte)(s >> 8)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBits(uint s)
        {
            return (uint)((ReverseBits((byte)s) << 24) | (ReverseBits((byte)(s >> 8)) << 16) |
                           (ReverseBits((byte)(s >> 16)) << 8) | ReverseBits((byte)(s >> 24)));
        }

        // TODO: enumerator + lazy calculation
        public class VariableLengthValue
        {
            public VariableLengthValue(int? size = null)
            {
                fragments = new List<Fragment>();
                sizeLimit = size;
            }

            public VariableLengthValue DefineFragment(int offset, int length, ulong value, string name = null, int valueOffset = 0)
            {
                if(valueOffset + length > 64)
                {
                    throw new ArgumentException("value offset/length combination exceeds value length");
                }

                var f = new Fragment
                {
                    Offset = offset,
                    Length = length,
                    RawValue = (value >> valueOffset) & ((1u << length) - 1),
                    Name = name
                };

                InnerDefine(f);
                return this;
            }

            public VariableLengthValue DefineFragment(int offset, int length, Func<ulong> valueProvider, string name = null)
            {
                if(length > 64)
                {
                    throw new ArgumentException("Length exceeds");
                }

                var f = new Fragment
                {
                    Offset = offset,
                    Length = length,
                    ValueProvider = valueProvider,
                    Name = name
                };

                InnerDefine(f);
                return this;
            }

            public BitStream GetBits(int skip)
            {
                // TODO: skip value works only for padding area - make it complete

                // construct the final value
                var result = new BitStream();
                for(var i = 0; i < fragments.Count; i++)
                {
                    // pad the space before the fragment
                    while(result.Length + skip < fragments[i].Offset)
                    {
                        result.AppendBit(false);
                    }

                    // append the fragment value
                    var value = fragments[i].EffectiveValue;
                    result.AppendMaskedValue((uint)value, 0, (uint)Math.Min(fragments[i].Length, 32));
                    if(fragments[i].Length > 32)
                    {
                        result.AppendMaskedValue((uint)(value >> 32), 0, (uint)(fragments[i].Length - 32));
                    }
                }
                if(sizeLimit.HasValue)
                {
                    // add final padding
                    while(result.Length + skip < sizeLimit.Value)
                    {
                        result.AppendBit(false);
                    }
                }

                return result;
            }

            public BitStream Bits
            {
                get
                {
                    return GetBits(skip: 0);
                }
            }

            private void InnerDefine(Fragment f)
            {
                if(sizeLimit.HasValue && f.Offset + f.Length > sizeLimit.Value)
                {
                    throw new ArgumentException("Fragment exceeds size limit.");
                }
                if(!CanAcceptFragment(f, out var index))
                {
                    throw new ArgumentException("Fragment overlaps with another one.");
                }
                fragments.Insert(index, f);
            }

            private bool CanAcceptFragment(Fragment f, out int index)
            {
                var result = fragments.BinarySearch(f, comparer);
                if(result >= 0)
                {
                    index = -1;
                    return false;
                }

                index = ~result;
                return ((index == 0) || (fragments[index - 1].Offset + fragments[index - 1].Length <= f.Offset))
                    && ((index == fragments.Count) || (f.Offset + f.Length <= fragments[index + 1].Offset));
            }

            private readonly List<Fragment> fragments;
            private readonly int? sizeLimit;

            private static FragmentComparer comparer = new FragmentComparer();

            private struct Fragment
            {
                public int Offset;
                public int Length;
                public ulong RawValue;
                public Func<ulong> ValueProvider;
                public string Name;

                public ulong EffectiveValue => ValueProvider != null ? ValueProvider() & ((1u << Length) - 1) : RawValue;
            }

            private class FragmentComparer : IComparer<Fragment>
            {
                public int Compare(Fragment x, Fragment y)
                {
                    if(x.Offset < y.Offset)
                    {
                        return -1;
                    }
                    else if(x.Offset > y.Offset)
                    {
                        return 1;
                    }
                    return 0;
                }
            }
        }

        // TODO: optimize it - add padding automatically, limit number of copying
        public class BitConcatenator
        {
            public static BitConcatenator New(int? size = null)
            {
                return new BitConcatenator(size);
            }

            private BitStream bs = new BitStream();

            public BitStream Bits => bs;

            public BitConcatenator StackAbove(uint value, int length, int position = 0)
            {
                if(maxHeight.HasValue && bs.Length + length > maxHeight.Value)
                {
                    throw new ArgumentException("This operation would exceed maximal height");
                }

                bs.AppendMaskedValue(value, (uint)position, (uint)length);
                return this;
            }

            private BitConcatenator(int? size)
            {
                maxHeight = size;
            }

            private readonly int? maxHeight;
        }
    }
}

