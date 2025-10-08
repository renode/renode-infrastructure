//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Utilities;

using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class LoggerFileCommand : AutoLoadCommand
    {
        public LoggerFileCommand(Monitor monitor) : base(monitor, "logFile", "sets the output file for logger.", "logF")
        {
        }

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteError("\nYou must specify the filename (full path or relative) for output file.");
        }

        [Runnable]
        public void Run(StringToken path)
        {
            InnerRun(path.Value, false);
        }

        [Runnable]
        public void Run(StringToken path, BooleanToken token)
        {
            InnerRun(path.Value, token.Value);
        }

        private void InnerRun(string path, bool flushAfterEveryWrite)
        {
            if(Logger.GetBackends().TryGetValue(BackendName, out var backend))
            {
                // We are explicitly removing existing backend to close
                // file opened by it. When using the same path twice,
                // this allows for the previous file to be moved by
                // SequencedFilePath.
                Logger.RemoveBackend(backend);
            }
            Logger.AddBackend(new FileBackend(path, flushAfterEveryWrite), BackendName, true);
        }

        private readonly string BackendName = "file";
    }
}