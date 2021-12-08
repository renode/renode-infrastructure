//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Reflection;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.CPU
{
    public static class VectorInstructionsOpcodes
    {
        public static void EnableVectorOpcodesCounting(this BaseRiscV cpu)
        {
            var assembly = Assembly.GetExecutingAssembly();
            if(!assembly.TryFromResourceToTemporaryFile("Antmicro.Renode.Cores.RiscV.Opcodes.Rvv", out var file))
            {
                throw new RecoverableException("Couldn't unpack RVV opcodes");
            }

            cpu.EnableRiscvOpcodesCounting(file);
        }
    }
}
