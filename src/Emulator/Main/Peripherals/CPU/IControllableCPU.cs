//
// Copyright (c) 2010-2018 Antmicro
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
    public interface IControllableCPU : ICPU
    {
        void SetRegisterUnsafe(int register, ulong value);

        RegisterValue GetRegisterUnsafe(int register);

        IEnumerable<CPURegister> GetRegisters();

        string[,] GetRegistersValues();

        void InitFromElf(IELF elf);

        void InitFromUImage(UImage uImage);

        Endianess Endianness { get; }
    }

    public struct CPURegister
    {
        public CPURegister(int index, int width, bool isGeneral, bool isReadonly)
        {
            if(width != 8 && width != 16 && width != 32 && width != 64)
            {
                throw new ArgumentException($"Unsupported width: {width}");
            }

            Index = index;
            IsGeneral = isGeneral;
            Width = width;
            IsReadonly = isReadonly; 
        }

        public ulong ValueFromBytes(byte[] bytes)
        {
            if(bytes.Length > Width)
            {
                throw new ArgumentException($"Expected {Width} bytes, but {bytes.Length} received");
            }

            ulong result = 0;

            var bytesWithPadding = Enumerable.Repeat<byte>(0, (Width / 8) - bytes.Length).Concat(bytes).ToArray();
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
            }

            return result;
        }

        public int Index { get; private set; }
        public bool IsGeneral { get; private set; }
        public int Width { get; private set; }
        public bool IsReadonly { get; private set; }

        // this is to support monitor output
        public override string ToString()
        {
            return Index.ToString();
        }
    }
}

