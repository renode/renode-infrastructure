//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Hooks
{
    public static class GPIOHookExtensions
    {
        public static void AddStateChangedHook(this IGPIOWithHooks gpio, string pythonScript)
        {
            var engine = new GPIOPythonEngine(gpio, pythonScript);
            gpio.AddStateChangedHook(engine.Hook);
        }
    }
}

