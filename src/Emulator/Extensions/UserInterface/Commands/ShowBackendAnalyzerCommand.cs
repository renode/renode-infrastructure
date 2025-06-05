//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using AntShell.Commands;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Core;
using System.Linq;
using Antmicro.Renode.Peripherals;
using System.Text;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class ShowBackendAnalyzerCommand : AutoLoadCommand 
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            writer.WriteLine("Usage:");
            writer.WriteLine("------");
            writer.WriteLine("showAnalyzer ([externalName]) [peripheral] ([typeName])");
            writer.WriteLine("\tshows analyzer for [peripheral]");
            writer.WriteLine("");
            writer.WriteLine("[externalName] (optional) - if set, command will create external named [externalName]; this can be used only for analyzers implementing IExternal interface");
            writer.WriteLine("[typeName] (optional) - if set, command will select analyzer provided in class [typeName]; this must be used when there are more than one analyzers available and no default is set"); 
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken analyzerName, LiteralToken peripheral, LiteralToken analyzerTypeName)
        {
            if(!Emulator.ShowAnalyzers)
            {
                return;
            }
            try
            {
                var analyzer = GetAnalyzer(peripheral.Value, analyzerTypeName == null ? null : analyzerTypeName.Value);
                if (analyzerName != null)
                {
                    EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal((IExternal)analyzer, analyzerName.Value);
                }
                analyzer.Show();
            } 
            catch (Exception e)
            {
                throw new RecoverableException(string.Format("Received '{0}' error while initializing analyzer for: {1}. Are you missing a required plugin?", e.Message, peripheral.Value));
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken analyzerName, LiteralToken peripheral)
        {
            Run(writer, analyzerName, peripheral, null);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken peripheral)
        {
            Run(writer, peripheral, null);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, LiteralToken peripheral, LiteralToken analyzerTypeName)
        {
            Run(writer, null, peripheral, analyzerTypeName);
        }

        public IAnalyzableBackendAnalyzer GetAnalyzer(string peripheralName, string analyzerTypeName)
        {
            var emu = EmulationManager.Instance.CurrentEmulation;
            IPeripheral p;

            var m = monitor.Machine;
            try
            {
                p = (IPeripheral)monitor.ConvertValueOrThrowRecoverable(peripheralName, typeof(IPeripheral));
            }
            catch(RecoverableException)
            {
                throw new Exception(string.Format("Peripheral not found: {0}", peripheralName));
            }

            IAnalyzableBackend backend;
            if(!emu.BackendManager.TryGetBackendFor(p, out backend))
            {
                throw new Exception(string.Format("No backend found for {0}", peripheralName));
            }

            IAnalyzableBackendAnalyzer analyzer;
            var available = emu.BackendManager.GetAvailableAnalyzersFor(backend).ToArray();
            if(!available.Any())
            {
                throw new Exception(string.Format("No suitable analyzer found for {0}", peripheralName));
            }

            if(analyzerTypeName != null)
            {
                if(available.Contains(DefaultAnalyzerNamespace + analyzerTypeName))
                {
                    analyzerTypeName = DefaultAnalyzerNamespace + analyzerTypeName;
                }
                if(!available.Contains(analyzerTypeName))
                {
                    throw new Exception(string.Format("{0}: analyzer not found.", analyzerTypeName));
                }

                if(!emu.BackendManager.TryCreateAnalyzerForBackend(backend, analyzerTypeName, out analyzer))
                {
                    throw new Exception(string.Format("Couldn't create analyzer {0}.", analyzerTypeName));
                }
            }
            else if(!emu.BackendManager.TryCreateAnalyzerForBackend(backend, out analyzer))
            {
                var buffer = new StringBuilder();
                buffer.AppendFormat("More than one analyzer available for {0}. Please choose which one to use:\r\n", peripheralName);
                foreach(var x in available)
                {
                    buffer.AppendFormat(string.Format("\t{0}\r\n", x));
                }
                throw new Exception(buffer.ToString());
            }

            return analyzer;
        }

        public ShowBackendAnalyzerCommand(Monitor monitor) : base(monitor, "showAnalyzer", "opens a peripheral backend analyzer.", "sa")
        {
        }

        private const string DefaultAnalyzerNamespace = "Antmicro.Renode.Analyzers.";
    }
}

