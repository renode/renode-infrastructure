//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.CPU
{
    // If modified, make sure tlib's 'tlib_update_execution_mode'
    // function from arch/xtensa/arch_export.c remains correct.
    public enum ExecutionMode
    {
        Continuous,
        SingleStepNonBlocking,
        SingleStepBlocking,
    }
}

