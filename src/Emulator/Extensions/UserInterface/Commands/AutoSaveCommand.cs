//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Diagnostics;
using System.IO;

using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class AutoSaveCommand : AutoLoadCommand
    {
        public static void SetAutoSave(ICommandInteraction writer, bool enableAutoSave, float periodValue = 0.2f, string outputDirectory = null, bool pidSubdirectory = true)
        {
            var autoSnapshotCreator = EmulationManager.Instance.CurrentEmulation.AutoSnapshotCreator;

            if(!enableAutoSave)
            {
                autoSnapshotCreator.DisableSnapshotCreator();
                return;
            }

            if(outputDirectory == null)
            {
                outputDirectory = GetDefaultPath(out var fallback);
                if(fallback)
                {
                    LogMessageToLoggerAndMonitor($"Setting output directory for snapshots to {outputDirectory}", writer, LogLevel.Info);
                    LogMessageToLoggerAndMonitor($"You may change output directory by setting '{ConfigVariable}' variable in [{ConfigGroup}] group in config or via command argument", writer, LogLevel.Info);
                }
            }
            if(!Directory.Exists(outputDirectory))
            {
                LogMessageToLoggerAndMonitor($"Enabling autosave failed: directory from {ConfigVariable} config variable: {outputDirectory} doesn't exist", writer, LogLevel.Error);
                return;
            }
            if(pidSubdirectory)
            {
                outputDirectory = Path.Combine(outputDirectory, Process.GetCurrentProcess().Id.ToString());
                if(!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }
            }

            // it's a temporary solution for user to know that auto-save will be imprecise
            // it could be replaced with effective Quantum calculation and resizing it to a proper value
            var currentQuantum = EmulationManager.Instance.CurrentEmulation.MasterTimeSource.Quantum;
            var period = TimeInterval.FromSeconds(periodValue);
            if(currentQuantum > period || period.Ticks % currentQuantum.Ticks != 0)
            {
                LogMessageToLoggerAndMonitor("Current Quantum value is not compatible with requested period so effective period will be different", writer, LogLevel.Warning);
            }

            autoSnapshotCreator.StartTakingSnapshots(outputDirectory, period);
        }

        public AutoSaveCommand(Monitor monitor) : base(monitor, Command, "Enables auto snapshot creation")
        {
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine($"{Command} enableAutoSave [periodInSeconds] [path] [createPidSubdirectory]");
            writer.WriteLine();
            writer.WriteLine("enableAutoSave        - boolean, enable auto-saving mechanism");
            writer.WriteLine("periodInSeconds       - number,  period at which a snapshot will be created (default: 0.2)");
            writer.WriteLine($"path                  - string,  directory for saving snapshots (default: {GetDefaultPath(out var _)})");
            writer.WriteLine("createPidSubdirectory - boolean, create a sub-directory for storing snapshots based on Renode PID (default: true)");
            writer.WriteLine();
            writer.WriteLine($"Default path can be changed in config file by specifying '{ConfigVariable}' in [{ConfigGroup}]");
        }

        [Runnable]
        public void Run(ICommandInteraction writer, BooleanToken enableAutoSave)
        {
            SetAutoSave(writer, enableAutoSave.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, BooleanToken enableAutoSave, FloatToken period)
        {
            SetAutoSave(writer, enableAutoSave.Value, period.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, BooleanToken enableAutoSave, FloatToken period, StringToken path)
        {
            SetAutoSave(writer, enableAutoSave.Value, period.Value, path.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, BooleanToken enableAutoSave, FloatToken period, StringToken path, BooleanToken pidSubdirectory)
        {
            SetAutoSave(writer, enableAutoSave.Value, period.Value, path.Value, pidSubdirectory.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, BooleanToken enableAutoSave, StringToken path)
        {
            SetAutoSave(writer, enableAutoSave.Value, outputDirectory: path.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, BooleanToken enableAutoSave, StringToken path, BooleanToken pidSubdirectory)
        {
            SetAutoSave(writer, enableAutoSave.Value, outputDirectory: path.Value, pidSubdirectory: pidSubdirectory.Value);
        }

        private static void LogMessageToLoggerAndMonitor(string message, ICommandInteraction writer, LogLevel logLevel)
        {
            if(logLevel == LogLevel.Error)
            {
                writer.WriteError(message);
            }
            else
            {
                writer.WriteLine(message);
            }
            Logger.Log(logLevel, message);
        }

        private static string GetDefaultPath(out bool fallback)
        {
            fallback = !ConfigurationManager.Instance.TryGet(ConfigGroup, ConfigVariable, out string outputDirectory);
            return fallback ? TemporaryFilesManager.Instance.EmulatorTemporaryPath : outputDirectory;
        }

        private const string ConfigGroup = "general";
        private const string ConfigVariable = "autosave-path";
        private const string Command = "autoSave";
    }
}