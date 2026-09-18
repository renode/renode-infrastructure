//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.CPU.Assembler;
using Antmicro.Renode.Peripherals.CPU.Disassembler;
using Antmicro.Renode.Utilities;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public interface ICPUSupportingLLVMDisas : ICPU
    {
        string GetCurrentLLVMTriple(out ulong pc);

        string GetLLVMTriple(uint flags);

        string[] AllLLVMTriples { get; }

        string LLVMModel { get; }

        Endianess DisassemblyHexFormatting { get; }

        LLVMDisas LLVMDisasContainer { get; }

        public LLVMAssembler Assembler => LLVMDisasContainer.Assembler;

        public LLVMDisassembler Disassembler => LLVMDisasContainer.Disassembler;
    }

    public class LLVMDisas
    {
        public LLVMDisas(ICPUSupportingLLVMDisas cpu)
        {
            Cpu = cpu;
            InitDisas();
        }

        public LLVMAssembler Assembler => assembler;

        public LLVMDisassembler Disassembler => disassembler;

        [PostDeserialization]
        protected void InitDisas()
        {
            try
            {
                disassembler = new LLVMDisassembler(Cpu);
            }
            catch(ArgumentOutOfRangeException)
            {
                Logger.LogAs(Cpu, LogLevel.Warning, "Could not initialize disassembly engine");
            }
            try
            {
                assembler = new LLVMAssembler(Cpu);
            }
            catch(ArgumentOutOfRangeException)
            {
                Logger.LogAs(Cpu, LogLevel.Warning, "Could not initialize assembly engine");
            }
        }

        [Transient]
        private LLVMAssembler assembler;

        [Transient]
        private LLVMDisassembler disassembler;

        private readonly ICPUSupportingLLVMDisas Cpu;
    }

    public static class LLVMDisasExtension
    {
        public static uint AssembleBlock(this ICPUSupportingLLVMDisas cpu, ulong addr, string instructions, string triple = null, bool alternateDialect = false)
        {
            if(cpu.Assembler == null)
            {
                throw new RecoverableException("Assembler not available");
            }

            triple ??= cpu.GetLLVMTriple();

            if(cpu is ICPUWithMMU cpuWithMMU)
            {
                // Instruction fetch access used as we want to be able to write even pages mapped for execution only
                // We don't care if translation fails here (the address is unchanged in this case)
                cpuWithMMU.TryTranslateAddress(addr, MpuAccess.InstructionFetch, out addr);
            }

            var result = cpu.Assembler.AssembleBlock(addr, instructions, triple, alternateDialect);
            cpu.Bus.WriteBytes(result, addr, context: cpu);
            return (uint)result.Length;
        }

        public static string DisassembleBlock(this ICPUSupportingLLVMDisas cpu, ulong? addr = null, uint blockSize = 40, string triple = null, bool alternateDialect = false)
        {
            if(cpu.Disassembler == null)
            {
                throw new RecoverableException("Disassembly engine not available");
            }

            // For backwards compatibility requests with addr==ulong.MaxValue are interpreted as requests on PC.
            var disasAddr = addr ?? ulong.MaxValue;
            if(disasAddr == ulong.MaxValue)
            {
                var inferredTriple = cpu.GetCurrentLLVMTriple(out disasAddr);
                triple ??= inferredTriple;
            }
            else
            {
                triple ??= cpu.GetLLVMTriple();
            }

            if(cpu is ICPUWithMMU cpuWithMMU)
            {
                // Instruction fetch access used as we want to be able to read even pages mapped for execution only
                // We don't care if translation fails here (the address is unchanged in this case)
                cpuWithMMU.TryTranslateAddress(disasAddr, MpuAccess.InstructionFetch, out disasAddr);
            }

            var opcodes = cpu.Bus.ReadBytes(disasAddr, (int)blockSize, context: cpu);
            cpu.Disassembler.DisassembleBlock(disasAddr, opcodes, triple, alternateDialect, out var result);
            return result;
        }

        private static string GetLLVMTriple(this ICPUSupportingLLVMDisas cpu)
        {
            if(cpu.AllLLVMTriples.Length == 1)
            {
                return cpu.AllLLVMTriples[0];
            }

            throw new RecoverableException($"Triple must be specified because this CPU supports more than one triple: {Misc.PrettyPrintCollection(cpu.AllLLVMTriples)}");
        }
    }
}
