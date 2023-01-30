//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using Antmicro.Renode.Logging;
using Gcov = Antmicro.Renode.Integrations.Gcov;
using CPU = Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class CoverageCommand : AutoLoadCommand
    {
        public CoverageCommand(Monitor monitor) : base(monitor, "gcov", "starts tracing for gcov coverage")
        {
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("start", "stop")] LiteralToken operation, PathToken elfPath)
        {
            if(operation.Value == "start")
            {
                StartGcov(writer, elfPath);
            }
            else
            {
                StopGcov(writer, elfPath);
            }
        }

        private void StartGcov(ICommandInteraction writer, PathToken elfPath)
        {
            writer.WriteLine("Coverage data is now being recorded");

            if(dwarf != null || gcovParsers != null)
            {
                writer.WriteLine("GCOV tracing has been already started, stop it first", ConsoleColor.Red);
                return;
            }

            try
            {
                using(var elf = ELFSharp.ELF.ELFReader.Load(elfPath.Value))
                {
                    try
                    {
                        dwarf = DWARF.DWARFReader.ParseDWARF(elf);
                    }
                    catch(Exception e)
                    {
                        writer.WriteLine($"Could not parse {elfPath.Value} DWARF data: {e.Message}", ConsoleColor.Red);
                        return;
                    }
                }
            }
            catch(Exception e)
            {
                writer.WriteLine($"Could not read {elfPath.Value} ELF file: {e.Message}", ConsoleColor.Red);
                return;
            }

            gcovParsers = new List<Gcov.Parser>();

            var cpus = monitor.Machine.GetPeripheralsOfType<CPU.ICPUWithHooks>();
            foreach(var cpu in cpus)
            {
                var parser = new Gcov.Parser(dwarf, monitor.Machine.SystemBus.Lookup);
                gcovParsers.Add(parser);

                ((dynamic)cpu).SetHookAtBlockEnd((Action<ulong, uint>)((pc, _) => parser.PushBlockExecution(pc)));
            }
        }

        private void StopGcov(ICommandInteraction writer, PathToken path)
        {
            if(gcovParsers == null)
            {
                writer.WriteLine("GCOV tracing has not been started, start it first", ConsoleColor.Red);
                return;
            }

            var graphs = Gcov.Parser.CompileFunctionExecutions(gcovParsers);
            var functions = graphs.Select(g => new Gcov.Function(g, dwarf)).ToList();

            var gcnoPath = path.Value + ".gcno";
            using(var f = new Gcov.Writer(new FileStream(gcnoPath, FileMode.Create, FileAccess.Write)))
            {
                Gcov.File.WriteGCNO(f, functions);
            }

            var gcdaPath = path.Value + ".gcda";
            using(var f = new Gcov.Writer(new FileStream(gcdaPath, FileMode.Create, FileAccess.Write)))
            {
                Gcov.File.WriteGCDA(f, functions);
            }

            writer.WriteLine($"Coverage data has been saved into {path.Value}.gcno, {path.Value}.gcda");

            dwarf = null;
            gcovParsers = null;
        }

        [Runnable]
        public void F2L(ICommandInteraction writer, [Values("func2lines")] LiteralToken operation, PathToken elfPath, StringToken symbolName)
        {
            var symbolAddress = monitor.Machine.SystemBus.GetSymbolAddress(symbolName.Value);
            writer.WriteLine($"Looking for mappings for '{symbolName.Value}' starting at 0x{symbolAddress:X}");

            try
            {
                using(var elf = ELFSharp.ELF.ELFReader.Load(elfPath.Value))
                {
                    try
                    {
                        var dwarf = DWARF.DWARFReader.ParseDWARF(elf);
                        foreach(var cu in dwarf.DebugInfo.CompilationUnits)
                        {
                            foreach(var line in cu.Lines.Lines.Where(x => x.Address == symbolAddress))
                            {
                                var file = cu.Lines.Files[(int)line.GetFileIdx()];
                                var directory = cu.Lines.Directories[file.DirectoryIndex - 1];
                                writer.WriteLine($"PC 0x{symbolAddress:X} maps to {directory.Path}/{file.Path}:{line.LineNumber}|{line.Column}");
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        writer.WriteLine($"Could not parse {elfPath.Value} DWARF data: {e.Message}", ConsoleColor.Red);
                        return;
                    }
                }
            }
            catch(Exception e)
            {
                writer.WriteLine($"Could not read {elfPath.Value} ELF file: {e.Message}", ConsoleColor.Red);
                return;
            }
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);

            writer.WriteLine("Usage:");
            writer.WriteLine(" gcov start <elf_path>");
            writer.WriteLine(" gcov stop <output path> <substitutions>");

            writer.WriteLine("");
            writer.WriteLine("example:");
            writer.WriteLine(" gcov start @program_1.elf");
            writer.WriteLine(" gcov stop @program_1 \"old:new,wrong/path4:correct/path\"");
        }

        private DWARF.DWARFReader dwarf;
        private List<Gcov.Parser> gcovParsers;
    }
}

