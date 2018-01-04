//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using ELFSharp.ELF;
using ELFSharp.UImage;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface IControllableCPU : ICPU
    {
        void SetRegisterUnsafe(int register, uint value);

        uint GetRegisterUnsafe(int register);

        int[] GetRegisters();

        string[,] GetRegistersValues();

        void InitFromElf(ELF<uint> elf);

        void InitFromUImage(UImage uImage);

        Endianess Endianness { get; }
    }
}

