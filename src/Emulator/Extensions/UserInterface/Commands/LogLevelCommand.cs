//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using System.Linq;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class LogLevelCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usages:");
            writer.WriteLine(" logLevel");
            writer.WriteLine(" logLevel [LEVEL]");
            writer.WriteLine(" logLevel [LEVEL] [OBJECT]");
            writer.WriteLine(" logLevel [LEVEL] [BACKEND]");
            writer.WriteLine(" logLevel [LEVEL] [BACKEND] [OBJECT]");
            writer.WriteLine();
            writer.WriteLine("To see currently available backends execute command: logLevel");
            writer.WriteLine();
            PrintAvailableLevels(writer);
        }

        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            PrintCurrentLevels(writer);
        }
      
        [Runnable]
        public void Run([Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level)
        {
            SetLogLevel((LogLevel)level.Value);
        }

        [Runnable]
        public void Run([Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level)
        {
            SetLogLevel(LogLevel.Parse(level.Value));
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken peripheralOrBackendName)
        {
            if(!SetLogLevel((LogLevel)level.Value, null, peripheralOrBackendName.Value)
                && !SetLogLevel((LogLevel)level.Value, peripheralOrBackendName.Value, null))
            {
                writer.WriteError(string.Format("Could not find emulation element or backend named: {0}", peripheralOrBackendName.Value));
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken peripheralOrBackendName)
        {
            var logLevel = LogLevel.Parse(level.Value);
            if(!SetLogLevel(logLevel, null, peripheralOrBackendName.Value)
                && !SetLogLevel(logLevel, peripheralOrBackendName.Value, null))
            {
                writer.WriteError(string.Format("Could not find emulation element or backend named: {0}", peripheralOrBackendName.Value));
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken backendName, LiteralToken peripheralName)
        {
            if(!SetLogLevel((LogLevel)level.Value, backendName.Value, peripheralName.Value))
            {
                writer.WriteError(string.Format("Could not find emulation element or backend"));
            }
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken backendName, LiteralToken peripheralName)
        {
            if(!SetLogLevel(LogLevel.Parse(level.Value), backendName.Value, peripheralName.Value))
            {
                writer.WriteError(string.Format("Could not find emulation element or backend"));
            }
        }

        private bool SetLogLevel(LogLevel level, string backendName = null, string peripheralName = null)
        {
            IEmulationElement emulationElement = null;
            if(peripheralName != null && 
                !EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementByName(peripheralName, monitor.Machine, out emulationElement))
            {
                return false;
            }

            int id = (emulationElement == null) ? -1 : EmulationManager.Instance.CurrentEmulation.CurrentLogger.GetOrCreateSourceId(emulationElement);
            bool somethingWasSet = false;
            foreach(var b in Logger.GetBackends())
            {
                if((backendName == null || b.Key == backendName) && b.Value.IsControllable)
                {
                    b.Value.SetLogLevel(level, id);
                    somethingWasSet = true;
                }
            }

            return somethingWasSet;
        }

        private void PrintAvailableLevels(ICommandInteraction writer)
        {
            writer.WriteLine("Available levels:\n");
            writer.WriteLine(string.Format("{0,-18}| {1}", "Level", "Name"));
            writer.WriteLine("=======================================");
            foreach(var item in LogLevel.AvailableLevels)
            {
                writer.WriteLine(string.Format("{0,-18}: {1}", item.NumericLevel, item));
            }
            writer.WriteLine();
        }

        private void PrintCurrentLevels(ICommandInteraction writer)
        {
            string objectName;
            string machineName;

            writer.WriteLine("Currently set levels:\n");
            writer.WriteLine(string.Format("{0,-18}| {1,-36}| {2}", "Backend", "Emulation element", "Level"));
            writer.WriteLine("=================================================================");
            foreach(var backend in Logger.GetBackends().Where(b => b.Value.IsControllable))
            {
                writer.WriteLine(string.Format("{0,-18}: {1,-36}: {2}", backend.Key, string.Empty, backend.Value.GetLogLevel()));
                foreach (var custom in backend.Value.GetCustomLogLevels())
                {
                    EmulationManager.Instance.CurrentEmulation.CurrentLogger.TryGetName(custom.Key, out objectName, out machineName);
                    writer.WriteLine(string.Format("{0,-18}: {1,-36}: {2}", string.Empty, string.Format("{0}:{1}", machineName, objectName), custom.Value));
                }
                writer.WriteLine("-----------------------------------------------------------------");
            }
        }

        public LogLevelCommand(Monitor monitor) : base(monitor, "logLevel", "sets logging level for backends.")
        {
        }
    }
}

