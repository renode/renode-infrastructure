//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Backends;
using Antmicro.Renode.UserInterface.Tokenizer;
using AntShell.Commands;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class LastLogCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine(Name);
            writer.WriteLine($"{Name} <<numberOfEntries>>");
        }

        [Runnable]
        public void Run(ICommandInteraction writer)
        {
            PrintLastLogs(writer);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, DecimalIntegerToken numberOfEntries)
        {
            PrintLastLogs(writer, (int)numberOfEntries.Value);
        }

        private void PrintLastLogs(ICommandInteraction writer, int numberOfEntries = DefaultNumberOfEntries)
        {
            Logger.Flush();
            if(numberOfEntries > MemoryBackend.MaxCount)
            {
                throw new RecoverableException($"The number of entries cannot be larger than {MemoryBackend.MaxCount}");
            }

            if(!Logger.GetBackends().TryGetValue(MemoryBackend.Name, out var backend))
            {
                throw new RecoverableException("Could not get memory backend");
            }

            var memoryBackend = backend as MemoryBackend;
            if(memoryBackend == null)
            {
                throw new RecoverableException("Memory backend of unexpected type");
            }

            foreach(var entry in ((MemoryBackend)memoryBackend).GetMemoryLogEntries(numberOfEntries))
            {
                writer.WriteLine($"{entry.DateTime:HH:mm:ss.ffff} [{entry.Type}] {entry.Message}", entry.Type.Color);
            }
        }

        public LastLogCommand(Monitor monitor)
            : base(monitor, "lastLog", "Logs last n logs.")
        {
        }

        private const int DefaultNumberOfEntries = 20;
    }
}
