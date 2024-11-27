//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using Antmicro.Renode.Debugging;

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

        public static long Bit(byte b)
        {
            return (0x1 << b);
        }

        public static ulong Bits(int position, int width)
        {
            // Force 0 with width = 64 and up because (1UL << 64) is 1
            var pow2 = width < 64 ? (1UL << width) : 0;
            var nbits = pow2 - 1;
            // Same as above
            return position < 64 ? (nbits << position) : 0;
        }

        public static bool IsBitSet(uint reg, byte bit)
        {
            return ((0x1 << bit) & reg) != 0;
        }

        public static bool IsBitSet(ulong reg, byte bit)
        {
            return ((0x1UL << bit) & reg) != 0;
        }

        public static uint SetBitsFrom(uint source, uint newValue, int position, int width)
        {
            var mask = ((1u << width) - 1) << position;
            var bitsToSet = newValue & mask;
            return source | bitsToSet;
        }

        public static ulong SetBitsFrom(ulong source, ulong newValue, int position, int width)
        {
            var mask = ((1UL << width) - 1) << position;
            var bitsToSet = newValue & mask;
            return source | bitsToSet;
        }

        public static void ClearBits(ref uint reg, params byte[] bits)
        {
            uint mask = uint.MaxValue;
            foreach(var bit in bits)
            {
                mask -= 1u << bit;
            }
            reg &= mask;
        }

        public static void ClearBits(ref ulong reg, params byte[] bits)
        {
            ulong mask = ulong.MaxValue;
            foreach(var bit in bits)
            {
                mask -= 1ul << bit;
            }
            reg &= mask;
        }

        public static void ClearBits(ref uint reg, int position, int width)
        {
            uint mask = uint.MaxValue;
            for(var i = 0; i < width; i++)
            {
                mask -= 1u << (position + i);
            }
            reg &= mask;
        }

        public static void ClearBits(ref ulong reg, int position, int width)
        {
            ulong mask = ulong.MaxValue;
            for(var i = 0; i < width; i++)
            {
                mask -= 1ul << (position + i);
            }
            reg &= mask;
        }

        public static void SetBits(ref uint reg, int position, int width)
        {
            var mask = 0x0u;
            for(var i = 0; i < width; i++)
            {
                mask += 1u << (position + i);
            }
            reg |= mask;
        }

        public static void SetBits(ref ulong reg, int position, int width)
        {
            var mask = 0x0ul;
            for(var i = 0; i < width; i++)
            {
                mask += 1ul << (position + i);
            }
            reg |= mask;
        }

        public static void ReplaceBits(ref uint destination, uint source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            if(width < 0 || width > 32)
            {
                throw new ArgumentException("width not in [0,32]");
            }
            uint mask = (uint)((1ul << width) - 1);
            source &= mask << sourcePosition;
            destination &= ~(mask << destinationPosition);

            var positionDifference = sourcePosition - destinationPosition;
            destination |= (positionDifference >= 0)
                ? (source >> positionDifference)
                : (source << -positionDifference);
        }

        public static void ReplaceBits(ref ulong destination, ulong source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            if(width < 0 || width > 64)
            {
                throw new ArgumentException("width not in [0,64]");
            }
            ulong mask = (width == 64)
                ? ulong.MaxValue
                : (ulong)((1ul << width) - 1);

            source &= mask << sourcePosition;
            destination &= ~(mask << destinationPosition);

            var positionDifference = sourcePosition - destinationPosition;
            destination |= (positionDifference >= 0)
                ? (source >> positionDifference)
                : (source << -positionDifference);
        }

        public static byte ReplaceBits(this byte destination, byte source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            if(width < 0 || width > 8)
            {
                throw new ArgumentException("width not in [0,8]");
            }
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
            if(width < 0 || width > 32)
            {
                throw new ArgumentException("width not in [0,32]");
            }
            uint mask = (uint)((1ul << width) - 1);
            source &= mask << sourcePosition;
            destination &= ~(mask << destinationPosition);

            var positionDifference = sourcePosition - destinationPosition;
            return destination | ((positionDifference >= 0)
                ? (source >> positionDifference)
                : (source << -positionDifference));
        }

        public static ulong ReplaceBits(this ulong destination, ulong source, int width, int destinationPosition = 0, int sourcePosition = 0)
        {
            if(width < 0 || width > 64)
            {
                throw new ArgumentException("width not in [0,64]");
            }
            ulong mask = (width == 64 ? 0 : (1ul << width)) - 1;
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

        public static bool AreAnyBitsSet(ulong reg, int position, int width)
        {
            var mask = CalculateQuadWordMask(width, position);
            return (reg & mask) != 0;
        }

        public static bool AreAllBitsSet(ulong reg, int position, int width)
        {
            var mask = CalculateQuadWordMask(width, position);
            return (~reg & mask) == 0;
        }

        public static void UpdateWithShifted(ref uint reg, uint newValue, int position, int width)
        {
            UpdateWith(ref reg, newValue << position, position, width);
        }

        public static void UpdateWithShifted(ref ulong reg, ulong newValue, int position, int width)
        {
            UpdateWith(ref reg, newValue << position, position, width);
        }

        public static void UpdateWithMasked(ref uint reg, uint newValue, uint mask)
        {
            reg = (reg & ~mask) | (newValue & mask);
        }

        public static void UpdateWithMasked(ref ulong reg, ulong newValue, ulong mask)
        {
            reg = (reg & ~mask) | (newValue & mask);
        }

        public static void UpdateWith(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg = (reg & ~mask) | (newValue & mask);
        }

        public static void UpdateWith(ref ulong reg, ulong newValue, int position, int width)
        {
            var mask = CalculateQuadWordMask(width, position);
            reg = (reg & ~mask) | (newValue & mask);
        }

        public static void OrWith(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg |= (newValue & mask);
        }

        public static void OrWith(ref ulong reg, ulong newValue, int position, int width)
        {
            var mask = CalculateQuadWordMask(width, position);
            reg |= (newValue & mask);
        }

        public static void AndWithNot(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg &= ~(newValue & mask);
        }

        public static void AndWithNot(ref ulong reg, ulong newValue, int position, int width)
        {
            var mask = CalculateQuadWordMask(width, position);
            reg &= ~(newValue & mask);
        }

        public static void XorWith(ref uint reg, uint newValue, int position, int width)
        {
            var mask = CalculateMask(width, position);
            reg ^= (newValue & mask);
        }

        public static void XorWith(ref ulong reg, ulong newValue, int position, int width)
        {
            var mask = CalculateQuadWordMask(width, position);
            reg ^= (newValue & mask);
        }

        public static bool CalculateParity(uint word)
        {
            word ^= word >> 16;
            word ^= word >> 8;
            word ^= word >> 4;
            word ^= word >> 2;
            word ^= word >> 1;
            return (word & 0x1) == 0x1;
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

        public static void SetBit(ref ulong reg, byte bit, bool value)
        {
            if(value)
            {
                reg |= (0x1ul << bit);
            }
            else
            {
                reg &= (ulong.MaxValue - (0x1ul << bit));
            }
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

        public static IList<int> GetSetBits(ulong reg)
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

        public static uint GetSetBitsCount(ulong value)
        {
            var count = 0u;
            while(value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }

        public static string GetSetBitsPretty(ulong reg)
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

        public static void ForeachBit(ulong reg, Action<byte, bool> action, byte? bitCount = null)
        {
            // Iterate bitCount times or all bits in ulong
            byte iterations = bitCount ?? sizeof(ulong) * 8;
            for(byte i = 0; i < iterations; i++)
            {
                action(i, IsBitSet(reg, i));
            }
        }

        public static void ForeachActiveBit(ulong reg, Action<byte> action)
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

        public static ulong GetLeastSignificantOne(ulong val)
        {
            return (ulong)((long)val & -(long)val);
        }

        public static ulong GetLeastSignificantZero(ulong val)
        {
            return GetLeastSignificantOne(~val);
        }

        public static void GetBytesFromValue(byte[] bytes, int offset, ulong val, int typeSize, bool reverse = false)
        {
            if(offset + typeSize > bytes.Length)
            {
                throw new ArgumentOutOfRangeException("The sum of offset and typeSize can't be greater that length of the bytes array.");
            }

            int valOffset = 0;
            for(int i = typeSize - 1; i >= 0; --i)
            {
                int byteIndex = offset + (reverse ? typeSize - 1 - i : i);
                bytes[byteIndex] = (byte)((val >> valOffset) & 0xFF);
                valOffset += 8;
            }
        }

        public static byte[] GetBytesFromValue(ulong val, int typeSize, bool reverse = false)
        {
            var result = new byte[typeSize];
            GetBytesFromValue(result, 0, val, typeSize, reverse);
            return result;
        }

        public static bool[] GetBits(ulong reg)
        {
            return GetBitsInner(reg, 64);
        }

        public static bool[] GetBits(uint reg)
        {
            return GetBitsInner(reg, 32);
        }

        public static bool[] GetBits(ushort reg)
        {
            return GetBitsInner(reg, 16);
        }

        public static bool[] GetBits(byte reg)
        {
            return GetBitsInner(reg, 8);
        }

        public static byte[] GetNibbles(ulong reg)
        {
            var nibbles = new byte[16];
            var i = 0;
            while(reg > 0)
            {
                nibbles[i++] = (byte)(reg & 0xF);
                reg >>= 4;
            }
            return nibbles;
        }

        public static byte GetValue(byte reg, int offset, int size)
        {
            if(size < 0 || size > 8)
            {
                throw new ArgumentException("size not in [0,8]");
            }
            return (byte)(((uint)reg >> offset) & ((0x1ul << size) - 1));
        }

        public static uint GetValue(uint reg, int offset, int size)
        {
            if(size < 0 || size > 32)
            {
                throw new ArgumentException("size not in [0,32]");
            }
            return (uint)((reg >> offset) & ((0x1ul << size) - 1));
        }

        public static ulong GetValue(ulong reg, int offset, int size)
        {
            if(size < 0 || size > 64)
            {
                throw new ArgumentException("size not in [0,64]");
            }
            return (ulong)((reg >> offset) & ((size == 64 ? 0 : (0x1ul << size)) - 1));
        }

        public static uint GetMaskedValue(uint reg, int maskOffset, int maskSize)
        {
            return reg & CalculateMask(maskSize, maskOffset);
        }

        public static ulong GetMaskedValue(ulong reg, int maskOffset, int maskSize)
        {
            return reg & CalculateQuadWordMask(maskSize, maskOffset);
        }

        public static void SetMaskedValue(ref uint reg, uint value, int maskOffset, int maskSize)
        {
            var mask = CalculateMask(maskSize, maskOffset);
            value <<= maskOffset;
            value &= mask;
            reg &= ~mask;
            reg |= value;
        }

        public static uint SetMaskedValue(uint source, uint value, int maskOffset, int maskSize)
        {
            SetMaskedValue(ref source, value, maskOffset, maskSize);
            return source;
        }

        public static void SetMaskedValue(ref ulong reg, ulong value, int maskOffset, int maskSize)
        {
            var mask = CalculateQuadWordMask(maskSize, maskOffset);
            value <<= maskOffset;
            value &= mask;
            reg &= ~mask;
            reg |= value;
        }

        public static ulong SetMaskedValue(ulong source, ulong value, int maskOffset, int maskSize)
        {
            SetMaskedValue(ref source, value, maskOffset, maskSize);
            return source;
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

        public static ulong ToUInt64(byte[] data, int index, int length, bool reverse)
        {
            ulong result = 0;
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

        public static uint ToUInt32(byte[] data, int index, int length, bool reverse)
        {
            return (uint)ToUInt64(data, index, length, reverse);
        }

        public static ushort ToUInt16(byte[] data, int index, bool reverse)
        {
            return (ushort)ToUInt32(data, index, 2, reverse);
        }

        public static uint SignExtend(uint value, int size)
        {
            if(value >= (1 << size - 1))
            {
                return 0xFFFFFFFF << size | value;
            }
            return value;
        }

        public static ulong SignExtend(ulong value, int size)
        {
            if(value >= (1ul << size - 1))
            {
                return 0xFFFFFFFFFFFFFFFF << size | value;
            }
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CalculateMask(int width, int position)
        {
            const int MaxWidth = 32;
            AssertMaskParameters(width, position, MaxWidth);
            if(width == MaxWidth && position == 0)
            {
                return uint.MaxValue;
            }
            return (1u << width) - 1 << position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CalculateQuadWordMask(int width, int position)
        {
            const int MaxWidth = 64;
            AssertMaskParameters(width, position, MaxWidth);
            if(width == MaxWidth && position == 0)
            {
                return ulong.MaxValue;
            }
            return (1ul << width) - 1 << position;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBitsByByte(uint i)
        {
            i = ((i >> 1) & 0x55555555) | ((i & 0x55555555) << 1);
            i = ((i >> 2) & 0x33333333) | ((i & 0x33333333) << 2);
            return ((i >> 4) & 0x0F0F0F0F) | ((i & 0x0F0F0F0F) << 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBitsByWord(uint i)
        {
            i = ReverseBitsByByte(i);
            return ((i >> 8) & 0x00FF00FF) | ((i & 0x00FF00FF) << 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReverseBits(byte b)
        {
            return (byte)ReverseBitsByByte(b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReverseBits(ushort s)
        {
            return (ushort)ReverseBitsByWord(s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBits(uint i)
        {
            i = ReverseBitsByWord(i);
            return (i >> 16) | (i << 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReverseBits(ulong v)
        {
            return ((ulong)ReverseBits((uint)v) << 32) | ReverseBits((uint)(v >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReverseBytes(ushort v)
        {
            return (ushort)((v << 8) | (v >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBytes(uint v)
        {
            return ((uint)ReverseBytes((ushort)v) << 16) | ReverseBytes((ushort)(v >> 16));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReverseBytes(ulong v)
        {
            return ((ulong)ReverseBytes((uint)v) << 32) | ReverseBytes((uint)(v >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseWords(uint v)
        {
            return (v << 16) | (v >> 16);
        }

        public static int CalculateBytesCount(int bitsCount)
        {
            DebugHelper.Assert(bitsCount >= 0);
            // Return count of bytes, including one which isn't full of bits.
            return (bitsCount - 1) / BitsPerByte + 1;
        }

        public const int BitsPerByte = 8;

        private static void AssertMaskParameters(int width, int position, int maxWidth)
        {
            if(width < 0)
            {
                throw new ArgumentException($"'{nameof(width)}' (0x{width:X}) should be grater than 0.");
            }
            if(position < 0)
            {
                throw new ArgumentException($"'{nameof(position)} ({position}) should be grater than or equal to 0.");
            }
            if(checked(width + position) > maxWidth)
            {
                throw new ArgumentException($"Sum of '{nameof(width)}' ({width}) and '{nameof(position)}' ({position}) should be less than or equal to {maxWidth}.");
            }
        }

        private static bool[] GetBitsInner(ulong reg, int length)
        {
            var result = new bool[length];
            for(var i = 0; i < result.Length; ++i)
            {
                result[i] = (reg & 1u) == 1;
                reg >>= 1;
            }
            return result;
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
