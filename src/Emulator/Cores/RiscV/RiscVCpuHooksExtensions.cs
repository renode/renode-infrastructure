//
// Copyright (c) 2010-2022 Antmicro
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
    public static class RiscVCpuHooksExtensions
    {
        public static void RegisterCSRHandlerFromString(this BaseRiscV cpu, ulong csr, string pythonScript, bool initable = false)
        {
            var engine = new RiscVCsrPythonEngine(cpu, csr, initable, script: pythonScript);
            cpu.RegisterCSR(csr, engine.CsrReadHook, engine.CsrWriteHook);
        }

        public static void RegisterCSRHandlerFromFile(this BaseRiscV cpu, ulong csr, string path, bool initable = false)
        {
            var engine = new RiscVCsrPythonEngine(cpu, csr, initable, path: path);
            cpu.RegisterCSR(csr, engine.CsrReadHook, engine.CsrWriteHook);
        }

        public static void InstallCustomInstructionHandlerFromString(this BaseRiscV cpu, string pattern, string pythonScript)
        {
            var engine = new RiscVInstructionPythonEngine(cpu, pattern, script: pythonScript);
            cpu.InstallCustomInstruction(pattern, engine.Hook);
        }

        public static void InstallCustomInstructionHandlerFromFile(this BaseRiscV cpu, string pattern, string path)
        {
            var engine = new RiscVInstructionPythonEngine(cpu, pattern, path: path);
            cpu.InstallCustomInstruction(pattern, engine.Hook);
        }
    }
}

