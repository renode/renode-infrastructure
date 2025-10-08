//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;

using AntShell;

namespace Antmicro.Renode.UserInterface
{
    public static class ShellProvider
    {
        public static Shell GenerateShell(Monitor monitor, bool forceVCursor = false)
        {
            var settings = new ShellSettings
            {
                NormalPrompt = new Prompt("(monitor) ", ConsoleColor.DarkRed),
                BannerProvider = () => Enumerable.Repeat(Environment.NewLine, NumberOfDummyLines).Aggregate(String.Empty, (x, y) => x + y) + EmulationManager.Instance.VersionString,
                PreprocessSuggestionsInput = Monitor.SanitizePathSeparator,
                UseBuiltinQuit = false,
                UseBuiltinHelp = false,
                UseBuiltinSave = false,
                ForceVirtualCursor = forceVCursor,
                ClearScreen = false,
                DirectorySeparator = '/',
                HistorySavePath = ConfigurationManager.Instance.Get("general", "history-path", Path.Combine(Emulator.UserDirectoryPath, "history"))
            };

            var shell = new Shell(monitor, settings);

            var startupCommand = Environment.GetEnvironmentVariable(Monitor.StartupCommandEnv);
            if(!string.IsNullOrEmpty(startupCommand) && shell != null)
            {
                shell.StartupCommand = startupCommand;
            }

            return shell;
        }

        public static int NumberOfDummyLines;
    }
}