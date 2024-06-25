//
// Copyright (c) 2010-2024 Antmicro
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
using System.Collections.Generic;

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
            TrySetLogLevel((LogLevel)level.Value);
        }

        [Runnable]
        public void Run([Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level)
        {
            TrySetLogLevel(LogLevel.Parse(level.Value));
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken emulationElementOrBackendName)
        {
            RunInner(writer, (LogLevel)level.Value, emulationElementOrBackendName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, StringToken emulationElementOrBackendName)
        {
            RunInner(writer, (LogLevel)level.Value, emulationElementOrBackendName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken emulationElementOrBackendName, BooleanToken recursive)
        {
            RunInner(writer, (LogLevel)level.Value, emulationElementOrBackendName.Value, recursive.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, StringToken emulationElementOrBackendName, BooleanToken recursive)
        {
            RunInner(writer, (LogLevel)level.Value, emulationElementOrBackendName.Value, recursive.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken emulationElementOrBackendName)
        {
            RunInner(writer, LogLevel.Parse(level.Value), emulationElementOrBackendName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, StringToken emulationElementOrBackendName)
        {
            RunInner(writer, LogLevel.Parse(level.Value), emulationElementOrBackendName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken emulationElementOrBackendName, BooleanToken recursive)
        {
            RunInner(writer, LogLevel.Parse(level.Value), emulationElementOrBackendName.Value, recursive.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, StringToken emulationElementOrBackendName, BooleanToken recursive)
        {
            RunInner(writer, LogLevel.Parse(level.Value), emulationElementOrBackendName.Value, recursive.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken backendName, LiteralToken emulationElementName)
        {
            RunInner(writer, (LogLevel)level.Value, backendName.Value, emulationElementName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken backendName, StringToken emulationElementName)
        {
            RunInner(writer, (LogLevel)level.Value, backendName.Value, emulationElementName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken backendName, LiteralToken emulationElementName, BooleanToken recursive)
        {
            RunInner(writer, (LogLevel)level.Value, backendName.Value, emulationElementName.Value, recursive.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values(-1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level, LiteralToken backendName, StringToken emulationElementName, BooleanToken recursive)
        {
            RunInner(writer, (LogLevel)level.Value, backendName.Value, emulationElementName.Value, recursive.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken backendName, LiteralToken emulationElementName)
        {
            RunInner(writer, LogLevel.Parse(level.Value), backendName.Value, emulationElementName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken backendName, StringToken emulationElementName)
        {
            RunInner(writer, LogLevel.Parse(level.Value), backendName.Value, emulationElementName.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken backendName, LiteralToken emulationElementName, BooleanToken recursive)
        {
            RunInner(writer, LogLevel.Parse(level.Value), backendName.Value, emulationElementName.Value, recursive.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, [Values("Noisy", "Debug", "Info", "Warning", "Error")] StringToken level, LiteralToken backendName, StringToken emulationElementName, BooleanToken recursive)
        {
            RunInner(writer, LogLevel.Parse(level.Value), backendName.Value, emulationElementName.Value, recursive.Value);
        }

        private void RunInner(ICommandInteraction writer, LogLevel level, string emulationElementOrBackendName, bool recursive = true)
        {
            if(!TrySetLogLevel(level, null, emulationElementOrBackendName, recursive)
                && !TrySetLogLevel(level, emulationElementOrBackendName, null))
            {
                writer.WriteError(string.Format("Could not find emulation element or backend named: {0}", emulationElementOrBackendName));
            }
        }

        private void RunInner(ICommandInteraction writer, LogLevel level, string backendName, string emulationElementName, bool recursive = true)
        {
            if(!TrySetLogLevel(level, backendName, emulationElementName, recursive))
            {
                writer.WriteError(string.Format("Could not find emulation element or backend"));
            }
        }

        private bool TrySetLogLevel(LogLevel level, string backendName = null, string emulationElementName = null, bool recursive = true)
        {
            IEnumerable<IEmulationElement> emulationElements = null;
            var emulation = EmulationManager.Instance.CurrentEmulation;

            if(emulationElementName != null)
            {
                if(!emulation.TryGetEmulationElementByName(emulationElementName, monitor.Machine, out var emulationElement))
                {
                    return false;
                }

                emulationElements = ((emulationElement is IMachine) && recursive)
                    ? (emulationElement as Machine).GetRegisteredPeripherals().Select(x => x.Peripheral).Cast<IEmulationElement>()
                    : new[] { emulationElement };
            }

            var emulationElementsIds = (emulationElements == null)
                ? new[] { -1 }
                : emulationElements.Select(x => emulation.CurrentLogger.GetOrCreateSourceId(x)).ToArray();

            bool somethingWasSet = false;
            foreach(var b in Logger.GetBackends()
                .Where(x => x.Value.IsControllable)
                .Where(x => (backendName == null || x.Key == backendName)))
            {
                foreach(var emulationElementId in emulationElementsIds)
                {
                    b.Value.SetLogLevel(level, emulationElementId);
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

