//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Hooks
{
    public static class PSCIHookExtensions
    {
        public static void AddCustomPSCIStub(this ICPUWithPSCI cpu, ulong functionIdentifier, string pythonScript)
        {
            var engine = new PSCIPythonEngine(cpu, pythonScript, functionIdentifier);
            cpu.AddCustomPSCIStub(functionIdentifier, engine.Hook);
        }
    }
}

