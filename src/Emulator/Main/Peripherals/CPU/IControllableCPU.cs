//
// Copyright (c) 2010-2021 Antmicro
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
        void SetRegisterUnsafe(int register, RegisterValue value);

        RegisterValue GetRegisterUnsafe(int register);

        IEnumerable<CPURegister> GetRegisters();

        string[,] GetRegistersValues();

        void InitFromElf(IELF elf);

        void InitFromUImage(UImage uImage);

        Endianess Endianness { get; }
    }

    public static class ControllableCPUExtension
    {
        // this is to support Python integration
        public static void SetRegisterUnsafeUlong(this IControllableCPU cpu, int register, ulong value)
        {
            cpu.SetRegisterUnsafe(register, value);
        }
    }
}

