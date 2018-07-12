//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct RegisterValue
    {
        public ulong RawValue { get; private set; }
        public uint Bits { get; private set; }

        // this is not the perfect solution,
        // as it displays the value always in hex
        // which may be inconsistent with monitor
        // output mode
        public override string ToString()
        {
            return $"0x{RawValue.ToString("x")}";
        }

        public byte[] GetBytes()
        {
            switch(Bits)
            {
                case 8:
                    return new [] { (byte)RawValue };
                case 16:
                    return BitConverter.GetBytes((ushort)RawValue);
                case 32:
                    return BitConverter.GetBytes((uint)RawValue);
                case 64:
                    return BitConverter.GetBytes(RawValue);
                default:
                    throw new ArgumentException($"Unexpected bits count: {Bits}");
            }
        }

        public static implicit operator RegisterValue(ulong v)
        {
            return new RegisterValue { RawValue = v, Bits = 64 };
        }

        public static implicit operator RegisterValue(uint v)
        {
            return new RegisterValue { RawValue = v, Bits = 32 };
        }

        public static implicit operator RegisterValue(ushort v)
        {
            return new RegisterValue { RawValue = v, Bits = 16 };
        }

        public static implicit operator RegisterValue(byte v)
        {
            return new RegisterValue { RawValue = v, Bits = 8 };
        }

        public static implicit operator ulong(RegisterValue v)
        {
            return v.RawValue;
        }

        public static implicit operator uint(RegisterValue v)
        {
            if(v.Bits > 32 && v.RawValue > UInt32.MaxValue)
            {
                throw new InvalidCastException("Value is too big to be expressed as UInt32");
            }

            return (UInt32)v.RawValue;
        }

        public static implicit operator ushort(RegisterValue v)
        {
            if(v.Bits > 16 && v.RawValue > UInt16.MaxValue)
            {
                throw new InvalidCastException("Value is too big to be expressed as UInt16");
            }

            return (UInt16)v.RawValue;
        }

        public static implicit operator byte(RegisterValue v)
        {
            if(v.Bits > 8 && v.RawValue > Byte.MaxValue)
            {
                throw new InvalidCastException("Value is too big to be expressed as Byte");
            }

            return (Byte)v.RawValue;
        }
    }
}
