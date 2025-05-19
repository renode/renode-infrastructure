//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Hooks
{
    public static class PSCIHookExtensions
    {
        public static void AddCustomPSCIHandler(this ICPUWithPSCI cpu, ulong functionIdentifier, string pythonScript)
        {
            var engine = new PSCIPythonEngine(cpu, pythonScript, functionIdentifier);
            cpu.AddCustomPSCIHandler(functionIdentifier, engine.Hook);
        }
    }
}

