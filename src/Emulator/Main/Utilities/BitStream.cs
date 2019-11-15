
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Utilities
{
    public class BitStream
    {
        public static BitStream Empty = new BitStream();

        public BitStream(IEnumerable<byte> b = null)
        {
            segments = (b == null)
                ? new List<byte>()
                : new List<byte>(b);

            Length = (uint)segments.Count * 8;
        }

        public void Clear()
        {
            Length = 0;
            segments.Clear();
        }

        public BitStream Append(byte b)
        {
            EnsureIsAligned();
            segments.Add(b);
            Length += 8;
            return this;
        }

        public BitStream Append(short s)
        {
            EnsureIsAligned();
            segments.Add((byte)s);
            segments.Add((byte)(s >> 8));
            Length += 16;
            return this;
        }

        public BitStream Append(ushort s)
        {
            return Append((short)s);
        }

        private void EnsureIsAligned()
        {
            var offset = (int)(Length % BitsPerSegment);
            if(offset != 0)
            {
                throw new ArgumentException("Appending in an unaligned state is not supported yet.");
            }
        }

        public void AppendBit(bool state)
        {
            var offset = (int)(Length % BitsPerSegment);
            if(offset == 0)
            {
                segments.Add(0);
            }

            if(state)
            {
                var segmentId = (int)(Length / BitsPerSegment);
                segments[segmentId] |= (byte)(1 << offset);
            }

            Length++;
        }

        // TODO: this is not the most efficient way of doing things
        public void AppendMaskedValue(uint value, uint offset = 0, uint length = 32, bool msbFirst = true)
        {
            // TODO: add checks
            var bits = BitHelper.GetBits(value).Skip((int)offset).Take((int)length);
            if(!msbFirst)
            {
                bits = bits.Reverse();
            }
            foreach(var bit in bits)
            {
                AppendBit(bit);
            }
        }

        public byte AsByte(uint offset = 0, int length = 8)
        {
            if(length > 8)
            {
                length = 8;
            }

            if(offset >= Length || length <= 0)
            {
                return 0;
            }

            byte result;
            var firstSegment = (int)(offset / BitsPerSegment);
            var segmentOffset = (int)(offset % BitsPerSegment);
            if(segmentOffset == 0)
            {
                // aligned access
                result = segments[firstSegment];
            }
            else
            {
                var s1 = segments[firstSegment];
                var s2 = ((firstSegment + 1) < segments.Count) ? segments[firstSegment + 1] : (byte)0;
                result = (byte)((s1 << segmentOffset)
                            | (s2 >> (BitsPerSegment - segmentOffset)));
            }

            return (length == 8)
                ? result
                : (byte)(result & BitHelper.CalculateMask((int)length, 0));
        }

        public byte[] AsByteArray()
        {
            return segments.ToArray();
        }

        public byte[] AsByteArray(uint length)
        {
            return (length % 8 == 0)
                ? segments.Take((int)(length / 8)).ToArray()
                : AsByteArray(0, length);
        }

        public byte[] AsByteArray(uint offset, uint length)
        {
            var result = new byte[length];
            for(var i = 0u; i < length; i++)
            {
                result[i] = AsByte(offset + 8 * i);
            }
            return result;
        }

        public uint AsUInt32(uint offset = 0, int length = 32)
        {
            if(length > 32)
            {
                length = 32;
            }

            return (AsByte(offset, length)
                    | (uint)AsByte(offset + 8, length - 8) << 8
                    | (uint)AsByte(offset + 16, length - 16) << 16
                    | (uint)AsByte(offset + 24, length - 24) << 24);
        }

        public ulong AsUInt64(uint offset = 0, int length = 64)
        {
            if(length > 64)
            {
                length = 64;
            }

            return AsUInt32(offset, length)
                | (ulong)AsUInt32(offset + 32, length - 32) << 32;
        }

        public override string ToString()
        {
            return Misc.PrettyPrintCollectionHex(segments);
        }

        public uint Length { get; private set; }

        private readonly List<byte> segments;

        private const int BitsPerSegment = 8;
    }
}