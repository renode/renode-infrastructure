//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class AnalyzersCommand : AutoLoadCommand
    {
        public override void PrintHelp(AntShell.Commands.ICommandInteraction writer)
        {
            writer.WriteLine("Usage:");
            writer.WriteLine("------");
            writer.WriteLine("analyzers [peripheral]");
            writer.WriteLine("\tlists ids of available analyzer for [peripheral]");
            writer.WriteLine("");
            writer.WriteLine("analyzers default [peripheral]");
            writer.WriteLine("\twrites id of default analyzer for [peripheral]");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken peripheralName)
        {
            var emu = EmulationManager.Instance.CurrentEmulation;
            IPeripheral p;

            try
            {
                p = (IPeripheral)monitor.ConvertValueOrThrowRecoverable(peripheralName.Value, typeof(IPeripheral));
            }
            catch(RecoverableException)
            {
                writer.WriteError(string.Format("Peripheral not found: {0}", peripheralName.Value));
                return;
            }

            IAnalyzableBackend backend;
            if(!emu.BackendManager.TryGetBackendFor(p, out backend))
            {
                writer.WriteError(string.Format("No backend found for {0}", peripheralName.Value));
                return;
            }

            var def = emu.BackendManager.GetPreferredAnalyzerFor(backend);
            foreach(var a in emu.BackendManager.GetAvailableAnalyzersFor(backend))
            {
                writer.WriteLine(String.Format("{0}{1}", a, a == def ? " (default)" : String.Empty));
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("default")] LiteralToken @default, LiteralToken peripheralName)
        {
            var emu = EmulationManager.Instance.CurrentEmulation;
            IPeripheral p;
            string fake;

            var m = monitor.Machine;
            if(m == null || !m.TryGetByName(peripheralName.Value, out p, out fake))
            {
                writer.WriteError(string.Format("Peripheral not found: {0}", peripheralName.Value));
                return;
            }

            IAnalyzableBackend backend;
            if(!emu.BackendManager.TryGetBackendFor(p, out backend))
            {
                writer.WriteError(string.Format("No backend found for {0}", peripheralName.Value));
                return;
            }

            var def = emu.BackendManager.GetPreferredAnalyzerFor(backend);
            writer.WriteLine(def ?? "No default analyzer found.");
        }

        public AnalyzersCommand(Monitor monitor) : base(monitor, "analyzers", "shows available analyzers for peripheral.")
        {
        }
    }
}

