//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using ELFSharp.ELF;
using ELFSharp.UImage;

namespace Antmicro.Renode.Peripherals.CPU
{
    public struct CPURegister
    {
        public CPURegister(int index, int width, bool isGeneral, bool isReadonly, string[] aliases = null)
        {
            if(width % 8 != 0)
            {
                throw new ArgumentException($"Unsupported width: {width}");
            }

            Index = index;
            IsGeneral = isGeneral;
            Width = width;
            IsReadonly = isReadonly;
            Aliases = aliases;
        }

        public RegisterValue ValueFromBytes(byte[] bytes, Endianess endianness)
        {
            if(bytes.Length > Width)
            {
                throw new ArgumentException($"Expected {Width} bytes, but {bytes.Length} received");
            }
            bool needsByteSwap = (endianness == Endianess.LittleEndian) != BitConverter.IsLittleEndian;

            RegisterValue result = 0;

            var bytesWithPadding = Enumerable.Repeat<byte>(0, (Width / 8) - bytes.Length).Concat(bytes).ToArray();
            if(needsByteSwap)
            {
                bytesWithPadding = bytesWithPadding.Reverse().ToArray();
            }

            switch(Width)
            {
                case 8:
                    result = bytesWithPadding[0];
                    break;
                case 16:
                    result = BitConverter.ToUInt16(bytesWithPadding, 0);
                    break;
                case 32:
                    result = BitConverter.ToUInt32(bytesWithPadding, 0);
                    break;
                case 64:
                    result = BitConverter.ToUInt64(bytesWithPadding, 0);
                    break;
                default:
                    result = bytesWithPadding;
                    break;
            }

            return result;
        }

        public int Index { get; }
        public bool IsGeneral { get; }
        public int Width { get; }
        public bool IsReadonly { get; }
        public string[] Aliases { get; }

        // this is to support monitor output
        public override string ToString()
        {
            return Aliases != null
                ? String.Join(" / ", Aliases)
                : $"#{Index}";
        }
    }
}

