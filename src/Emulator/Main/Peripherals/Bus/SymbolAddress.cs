//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
namespace Antmicro.Renode.Core
{
    public struct SymbolAddress : IComparable<SymbolAddress>
    {
        public static SymbolAddress Max(SymbolAddress a, SymbolAddress b)
        {
            return (a.RawValue >= b.RawValue) ? a : b;
        }

        public static SymbolAddress Min(SymbolAddress a, SymbolAddress b)
        {
            return (a.RawValue <= b.RawValue) ? a : b;
        }

        public static bool operator >(SymbolAddress a, SymbolAddress b)
        {
            return a.RawValue > b.RawValue;
        }

        public static SymbolAddress operator %(SymbolAddress a, uint b)
        {
            return new SymbolAddress(a.RawValue % b);
        }

        public static SymbolAddress operator &(SymbolAddress a, int b)
        {
            return new SymbolAddress(a.RawValue & checked((uint)b));
        }

        public static SymbolAddress operator &(SymbolAddress a, uint b)
        {
            return new SymbolAddress(a.RawValue & b);
        }

        public static bool operator <(SymbolAddress a, SymbolAddress b)
        {
            return a.RawValue < b.RawValue;
        }

        public static bool operator <=(SymbolAddress a, SymbolAddress b)
        {
            return a.RawValue <= b.RawValue;
        }

        public static bool operator >=(SymbolAddress a, SymbolAddress b)
        {
            return a.RawValue >= b.RawValue;
        }

        public static bool operator ==(SymbolAddress a, SymbolAddress b)
        {
            return a.RawValue == b.RawValue;
        }

        public static bool operator !=(SymbolAddress a, SymbolAddress b)
        {
            return a.RawValue != b.RawValue;
        }

        public static SymbolAddress operator +(SymbolAddress a, SymbolAddress b)
        {
            return new SymbolAddress(a.RawValue + b.RawValue);
        }

        public static SymbolAddress operator -(SymbolAddress a, SymbolAddress b)
        {
            return new SymbolAddress(a.RawValue - b.RawValue);
        }

        public static implicit operator SymbolAddress(uint v)
        {
            return new SymbolAddress(v);
        }

        public static implicit operator SymbolAddress(ulong v)
        {
            return new SymbolAddress(v);
        }

        public static explicit operator uint(SymbolAddress s)
        {
            return checked((uint)s.RawValue);
        }

        public static explicit operator ulong(SymbolAddress s)
        {
            return s.RawValue;
        }

        public SymbolAddress(ulong value)
        {
            RawValue = value;
        }

        public int CompareTo(SymbolAddress other)
        {
            return RawValue.CompareTo(other.RawValue);
        }

        public override bool Equals(object obj)
        {
            if(obj is SymbolAddress s)
            {
                return RawValue == s.RawValue;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        public override string ToString()
        {
            return $"0x{RawValue:x}";
        }

        public ulong RawValue { get; private set; }

        // this is added because of the MarkerComparer
        public static SymbolAddress MaxValue;

        static SymbolAddress()
        {
            MaxValue = new SymbolAddress(ulong.MaxValue);
        }
    }
}
