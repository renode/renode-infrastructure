//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Text;

using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.UserInterface.Tokenizer;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class ShowBackendAnalyzerCommand : AutoLoadCommand
    {
        public static IAnalyzableBackendAnalyzer GetAnalyzer(IAnalyzable analyzable, string analyzerTypeName)
        {
            var emu = EmulationManager.Instance.CurrentEmulation;
            if(!emu.BackendManager.TryGetBackendFor(analyzable, out IAnalyzableBackend backend))
            {
                throw new Exception(string.Format("No backend found for {0}", analyzable));
            }

            IAnalyzableBackendAnalyzer analyzer;
            var available = emu.BackendManager.GetAvailableAnalyzersFor(backend).ToArray();
            if(available.Length == 0)
            {
                throw new Exception(string.Format("No suitable analyzer found for {0}", analyzable));
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
                buffer.AppendFormat("More than one analyzer available for {0}. Please choose which one to use:\r\n", analyzable);
                foreach(var x in available)
                {
                    buffer.AppendFormat(string.Format("\t{0}\r\n", x));
                }
                throw new Exception(buffer.ToString());
            }

            return analyzer;
        }

        public static void ShowAnalyzer(IAnalyzable analyzable, string analyzerTypeName, string analyzerName = null)
        {
            if(!Emulator.ShowAnalyzers)
            {
                return;
            }
            // If we're calling this on an arbitrary object and not a peripheral, the analysis backend has (probably) not been created
            // during peripheral registration, so try to create it now
            EmulationManager.Instance.CurrentEmulation.BackendManager.TryCreateBackend(analyzable);
            try
            {
                var analyzer = GetAnalyzer(analyzable, analyzerTypeName);
                if(analyzerName != null)
                {
                    EmulationManager.Instance.CurrentEmulation.ExternalsManager.AddExternal((IExternal)analyzer, analyzerName);
                }
                analyzer.Show();
            }
            catch(Exception e)
            {
                throw new RecoverableException(string.Format("Received '{0}' error while initializing analyzer for: {1}. Are you missing a required plugin?", e.Message, analyzable));
            }
        }

        public ShowBackendAnalyzerCommand(Monitor monitor) : base(monitor, "showAnalyzer", "opens a peripheral backend analyzer.", "sa")
        {
        }

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
        public void Run(ICommandInteraction _, StringToken analyzerName, LiteralToken peripheral, LiteralToken analyzerTypeName)
        {
            ShowAnalyzer(GetPeripheral(peripheral.Value), analyzerTypeName?.Value, analyzerName?.Value);
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

        private IPeripheral GetPeripheral(string peripheralName)
        {
            try
            {
                return (IPeripheral)monitor.ConvertValueOrThrowRecoverable(peripheralName, typeof(IPeripheral));
            }
            catch(RecoverableException)
            {
                throw new Exception(string.Format("Peripheral not found: {0}", peripheralName));
            }
        }

        private const string DefaultAnalyzerNamespace = "Antmicro.Renode.Analyzers.";
    }
}
