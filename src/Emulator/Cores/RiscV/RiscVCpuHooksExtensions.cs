//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Hooks
{
    public static class RiscVCpuHooksExtensions
    {
        public static void RegisterCSRHandlerFromString(this BaseRiscV cpu, ushort csr, string pythonScript, bool initable = false)
        {
            var engine = new RiscVCsrPythonEngine(cpu, csr, initable, script: pythonScript);
            cpu.RegisterCSR(csr, engine.CsrReadHook, engine.CsrWriteHook);
        }

        public static void RegisterCSRHandlerFromFile(this BaseRiscV cpu, ushort csr, string path, bool initable = false)
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

        public static void AddPreStackAccessHook(this BaseRiscV cpu, string pythonScript)
        {
            var engine = new RiscVStackAccessPythonEngine(cpu, script: pythonScript);
            cpu.EnablePreStackAccessHook(true);
            cpu.PreStackAccess += engine.Hook;
        }

        public static void AddPreStackAccessHookFromFile(this BaseRiscV cpu, string path)
        {
            var engine = new RiscVStackAccessPythonEngine(cpu, path: path);
            cpu.EnablePreStackAccessHook(true);
            cpu.PreStackAccess += engine.Hook;
        }
    }
}