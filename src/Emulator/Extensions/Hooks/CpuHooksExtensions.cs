//
// Copyright (c) 2010-2023 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Hooks
{
    public static class CpuHooksExtensions
    {
        public static void AddHook(this ICPUWithHooks cpu, [AutoParameter]Machine m, ulong addr, string pythonScript)
        {
            var engine = new BlockPythonEngine(m, cpu, pythonScript);
            cpu.AddHook(addr, engine.Hook);
        }

        public static void AddHookAtWfiStateChange(this ICPUWithHooks cpu, [AutoParameter]Machine m, string pythonScript)
        {
            var engine = new WFIPythonEngine(m, cpu, pythonScript);
            cpu.AddHookAtWfiStateChange(engine.HookWithWfiEnterExit);
        }

        public static void AddHookAtInterruptBegin(this ICPUWithHooks cpu, [AutoParameter]Machine m, string pythonScript)
        {
            var engine = new InterruptPythonEngine(m, cpu, pythonScript);
            cpu.AddHookAtInterruptBegin(engine.HookWithExceptionIndex);
        }

        public static void AddHookAtInterruptEnd(this ICPUWithHooks cpu, [AutoParameter]Machine m, string pythonScript)
        {
            var engine = new InterruptPythonEngine(m, cpu, pythonScript);
            cpu.AddHookAtInterruptEnd(engine.HookWithExceptionIndex);
        }
    }
}

