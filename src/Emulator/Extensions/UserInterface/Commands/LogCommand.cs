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
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class LogCommand : AutoLoadCommand
    {
        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine(String.Format("{0} <<message to log>> <<log level>>", Name));
        }
        [Runnable]
        public void Run(ICommandInteraction writer, StringToken message)
        {
            InnerLog(LogLevel.Info, message.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken message, [Values( -1L, 0L, 1L, 2L, 3L)] DecimalIntegerToken level)
        {
            InnerLog((LogLevel)(int)level.Value, message.Value);
        }

        [Runnable]
        public void Run(ICommandInteraction writer, StringToken message, [Values( "Noisy", "Debug", "Info", "Warning", "Error")] StringToken level)
        {
            InnerLog(LogLevel.Parse(level.Value), message.Value);
        }

        private void InnerLog(LogLevel logLevel, string message)
        {
            Logger.LogAs(monitor, logLevel, "Script: " + message);
        }

        public LogCommand(Monitor monitor)
            : base(monitor, "log", "logs messages.")
        {
        }
    }
}

