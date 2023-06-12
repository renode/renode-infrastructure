//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Net.NetworkInformation;
using System.Globalization;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.Structure
{
    [Convertible]
    public struct MACAddress : IEquatable<MACAddress>
    {
        public byte A { get; private set; }

        public byte B { get; private set; }

        public byte C { get; private set; }

        public byte D { get; private set; }

        public byte E { get; private set; }

        public byte F { get; private set; }

        public byte[] Bytes { get { return new [] { A, B, C, D, E, F }; } }

        public MACAddress(ulong address) : this()
        {
            A = (byte)(address >> 40);
            B = (byte)(address >> 32);
            C = (byte)(address >> 24);
            D = (byte)(address >> 16);
            E = (byte)(address >> 8);
            F = (byte)address;
        }

        public static MACAddress Default { get { return new MACAddress(); } }

        public MACAddress WithNewOctets(byte? a = null, byte? b = null, byte? c = null, byte? d = null, byte? e = null, byte? f = null)
        {
            var result = new MACAddress();
            result.A = a ?? A;
            result.B = b ?? B;
            result.C = c ?? C;
            result.D = d ?? D;
            result.E = e ?? E;
            result.F = f ?? F;
            return result;
        }

        public MACAddress Next(int count = 1)
        {
            for(var i = 0; i < count; i++)
            {
                InnerNext();
            }

            return this;
	}

        public MACAddress Previous()
        {
            var result = this;

            var cp = 5;
            while(true)
            {
                if(result.GetByte(cp) == 0)
                {
                    if(cp == 0)
                    {
                        throw new OverflowException();
                    }
                    cp--;
                }
                else
                {
                    result.SetByte(cp, (byte)(result.GetByte(cp) - 1));
                    for(int i = cp + 1; i < 6; i++)
                    {
                        result.SetByte(i, byte.MaxValue);
                    }
                    break;
                }
            }

            return result;
        }

        public byte[] GetBytes()
        {
            var bytes = new byte[6];
            for(var i = 0; i < 6; ++i)
            {
                bytes[i] = GetByte(i);
            }
            return bytes;
        }

        public override bool Equals(object obj)
        {
            if(obj == null)
            {
                return false;
            }
            if(obj.GetType() != typeof(MACAddress))
            {
                return false;
            }
            var other = (MACAddress)obj;
            return this == other;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return A.GetHashCode() ^ B.GetHashCode() ^ C.GetHashCode() ^ D.GetHashCode() ^ E.GetHashCode() ^ F.GetHashCode();
            }
        }

        public static bool TryParse(string str, out MACAddress result)
        {
            result = default(MACAddress);

            var splits = str.Split(':');
            if(splits.Length != 6)
            {
                return false;
            }

            for(var i = 0; i < 6; i++)
            {
                result.SetByte(i, byte.Parse(splits[i], NumberStyles.HexNumber));
            }

            return true;
        }

        public static MACAddress Parse(string str)
        {
            if(!TryParse(str, out var result))
            {
                throw new FormatException();
            }

            return result;
        }

        public static MACAddress FromBytes(byte[] array, int startingIndex = 0)
        {
            var result = new MACAddress();
            for(var i = 0; i < 6; i++)
            {
                result.SetByte(i, array[startingIndex + i]);
            }
            return result;
        }

        public bool IsBroadcast
        {
            get
            {
                return A == 0xFF && B == 0xFF && C == 0xFF && D == 0xFF && E == 0xFF && F == 0xFF;
            }
        }

        public bool IsMulticast
        {
            get
            {
                return A == 0x01 && B == 0x00 && C == 0x5E;
            }
        }

        public bool IsUnicast
        {
            get
            {
                return !IsBroadcast && !IsMulticast;
            }
        }

        public override string ToString()
        {
            return string.Format("{0:X2}:{1:X2}:{2:X2}:{3:X2}:{4:X2}:{5:X2}", A, B, C, D, E, F);
        }

        public static explicit operator MACAddress(PhysicalAddress address)
        {
            return MACAddress.FromBytes(address.GetAddressBytes());
        }

        public static explicit operator PhysicalAddress(MACAddress address)
        {
            return new PhysicalAddress(address.GetBytes());
        }

        public static explicit operator string(MACAddress m)
        {
            return m.ToString();
        }

        public static explicit operator MACAddress(string input)
        {
            return MACAddress.Parse(input);
        }

        public bool Equals(MACAddress other)
        {
            return this == other;
        }

        public static bool operator ==(MACAddress a, MACAddress b)
        {
            return a.A == b.A && a.B == b.B && a.C == b.C && a.D == b.D && a.E == b.E && a.F == b.F;
        }

        public static bool operator !=(MACAddress a, MACAddress b)
        {
            return !(a == b);
        }

        private void InnerNext()
        {
            var cp = 5;
            while(true)
            {
                if(this.GetByte(cp) == byte.MaxValue)
                {
                    if(cp == 0)
                    {
                        throw new OverflowException();
                    }
                    cp--;
                }
                else
                {
                    this.SetByte(cp, (byte)(this.GetByte(cp) + 1));
                    for(int i = cp + 1; i < 6; i++)
                    {
                        this.SetByte(i, 0);
                    }
                    break;
                }
            }
        }

        private byte GetByte(int index)
        {
            switch(index)
            {
            case 0:
                return A;
            case 1:
                return B;
            case 2:
                return C;
            case 3:
                return D;
            case 4:
                return E;
            case 5:
                return F;
            default:
                throw new ArgumentException();
            }
        }

        private void SetByte(int index, byte value)
        {
            switch(index)
            {
            case 0:
                A = value;
                break;
            case 1:
                B = value;
                break;
            case 2:
                C = value;
                break;
            case 3:
                D = value;
                break;
            case 4:
                E = value;
                break;
            case 5:
                F = value;
                break;
            default:
                throw new ArgumentException();
            }
        }
    }
}

