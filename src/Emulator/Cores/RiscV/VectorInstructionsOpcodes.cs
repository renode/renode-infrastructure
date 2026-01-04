//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU
{
    public static class VectorInstructionsOpcodes
    {
        public static void EnableVectorOpcodesCounting(this BaseRiscV cpu)
        {
            cpu.EnableRiscvOpcodesCountingByExtension(BaseRiscV.InstructionSet.V);
        }
    }
}