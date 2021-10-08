//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using ELFSharp.ELF;
using Microsoft.Scripting.Utils;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct RegisterValue
    {
        public ulong RawValue { get; private set; }
        public byte[] ByteArrayValue { get; private set; }
        public uint Bits { get; private set; }

        // this is not the perfect solution,
        // as it displays the value always in hex
        // which may be inconsistent with monitor
        // output mode
        public override string ToString()
        {
            return $"0x{RawValue.ToString("x")}";
        }

        public byte[] GetBytes(Endianess endianness)
        {
            bool needsByteSwap = (endianness == Endianess.LittleEndian) != BitConverter.IsLittleEndian;
            byte[] bytes;

            if(ByteArrayValue != null)
            {
                return ByteArrayValue;
            }

            switch (Bits)
            {
                case 8:
                    bytes = new[] { (byte)RawValue };
                    break;
                case 16:
                    bytes = BitConverter.GetBytes((ushort)RawValue);
                    break;
                case 32:
                    bytes = BitConverter.GetBytes((uint)RawValue);
                    break;
                case 64:
                    bytes = BitConverter.GetBytes(RawValue);
                    break;
                default:
                    throw new ArgumentException($"Unexpected bits count: {Bits}");
            }

            return needsByteSwap ? bytes.Reverse().ToArray() : bytes;
        }

        // this is to support using 64bit integer values for RegisterValue in monitor
        public static implicit operator RegisterValue(long v)
        {
            return new RegisterValue { RawValue = (ulong)v, ByteArrayValue = null, Bits = 64 };
        }

        public static implicit operator RegisterValue(ulong v)
        {
            return new RegisterValue { RawValue = v, ByteArrayValue = null, Bits = 64 };
        }

        public static implicit operator RegisterValue(uint v)
        {
            return new RegisterValue { RawValue = v, ByteArrayValue = null, Bits = 32 };
        }

        public static implicit operator RegisterValue(ushort v)
        {
            return new RegisterValue { RawValue = v, ByteArrayValue = null, Bits = 16 };
        }

        public static implicit operator RegisterValue(byte v)
        {
            return new RegisterValue { RawValue = v, ByteArrayValue = null, Bits = 8 };
        }

        public static implicit operator RegisterValue(byte[] v)
        {
            return new RegisterValue { RawValue = default(ulong), ByteArrayValue = v, Bits = (uint)v.Length * 8 };
        }

        public static implicit operator ulong(RegisterValue v)
        {
            if(v.ByteArrayValue != null)
            {
                if(v.TryGetBytesForInt(8, out var bytes))
                {
                    return BitConverter.ToUInt64(bytes, 0);
                }
                throw new InvalidCastException("Value is too big to be expressed as UInt64");
            }

            return v.RawValue;
        }

        public static implicit operator uint(RegisterValue v)
        {
            if(v.ByteArrayValue != null)
            {
                if(v.TryGetBytesForInt(4, out var bytes))
                {
                    return BitConverter.ToUInt32(bytes, 0);
                }
                throw new InvalidCastException("Value is too big to be expressed as UInt32");
            }

            if(v.Bits > 32 && v.RawValue > UInt32.MaxValue)
            {
                throw new InvalidCastException("Value is too big to be expressed as UInt32");
            }

            return (UInt32)v.RawValue;
        }

        public static implicit operator ushort(RegisterValue v)
        {
            if(v.ByteArrayValue != null)
            {
                if(v.TryGetBytesForInt(2, out var bytes))
                {
                    return BitConverter.ToUInt16(bytes, 0);
                }
                throw new InvalidCastException("Value is too big to be expressed as UInt16");
            }

            if(v.Bits > 16 && v.RawValue > UInt16.MaxValue)
            {
                throw new InvalidCastException("Value is too big to be expressed as UInt16");
            }

            return (UInt16)v.RawValue;
        }

        public static implicit operator byte(RegisterValue v)
        {
            if(v.ByteArrayValue != null)
            {
                if(v.TryGetBytesForInt(1, out var bytes))
                {
                    return bytes[0];
                }
                throw new InvalidCastException("Value is too big to be expressed as Byte");
            }

            if(v.Bits > 8 && v.RawValue > Byte.MaxValue)
            {
                throw new InvalidCastException("Value is too big to be expressed as Byte");
            }

            return (Byte)v.RawValue;
        }

        public static RegisterValue Create(ulong rawValue, uint bits)
        {
            return new RegisterValue { RawValue = rawValue, ByteArrayValue = null, Bits = bits };
        }

        private bool TryGetBytesForInt(int size, out byte[] bytes)
        {
            if(ByteArrayValue == null || ByteArrayValue.Skip(size).Any(b => b != 0))
            {
                bytes = default(byte[]);
                return false;
            }

            bytes = ByteArrayValue.Take(size).ToArray();
            return true;
        }
    }
}
