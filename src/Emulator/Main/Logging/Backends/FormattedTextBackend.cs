//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Text;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging
{
    public abstract class FormattedTextBackend : TextBackend
    {
        public override void Log(LogEntry entry)
        {
            if(!ShouldBeLogged(entry))
            {
                return;
            }

            var color = entry.Type.Color;
            var line = FormatLogEntry(entry);
            lock(sync)
            {
                if(!PlainMode && color.HasValue)
                {
                    SetColor(color.Value);
                }

                WriteLine(line);

                if(!PlainMode && color.HasValue)
                {
                    ResetColor();
                }
            }
        }

        public virtual bool PlainMode { get; set; }

        public bool LogThreadId { get; set; }

        protected override string FormatLogEntry(LogEntry entry)
        {
            var threadString = "";
            if(LogThreadId && entry.ThreadId.HasValue)
            {
                threadString = $" ({entry.ThreadId})";
            }

            return $"{CustomDateTime.Now:HH:mm:ss.ffff} [{entry.Type}]{threadString} {base.FormatLogEntry(entry)}";
        }

        protected abstract void SetColor(ConsoleColor color);

        protected abstract void ResetColor();

        protected abstract void WriteLine(string line);

        protected static readonly byte[][] ANSIColorControlSequence = {
            Encoding.ASCII.GetBytes("\x1b[30m"), // ConsoleColor.Black
            Encoding.ASCII.GetBytes("\x1b[34m"), // ConsoleColor.DarkBlue
            Encoding.ASCII.GetBytes("\x1b[32m"), // ConsoleColor.DarkGreen
            Encoding.ASCII.GetBytes("\x1b[36m"), // ConsoleColor.DarkCyan
            Encoding.ASCII.GetBytes("\x1b[31m"), // ConsoleColor.DarkRed
            Encoding.ASCII.GetBytes("\x1b[35m"), // ConsoleColor.DarkMagenta
            Encoding.ASCII.GetBytes("\x1b[33m"), // ConsoleColor.DarkYellow
            Encoding.ASCII.GetBytes("\x1b[37m"), // ConsoleColor.Gray
            Encoding.ASCII.GetBytes("\x1b[90m"), // ConsoleColor.DarkGray
            Encoding.ASCII.GetBytes("\x1b[94m"), // ConsoleColor.Blue
            Encoding.ASCII.GetBytes("\x1b[92m"), // ConsoleColor.Green
            Encoding.ASCII.GetBytes("\x1b[96m"), // ConsoleColor.Cyan
            Encoding.ASCII.GetBytes("\x1b[91m"), // ConsoleColor.Red
            Encoding.ASCII.GetBytes("\x1b[95m"), // ConsoleColor.Magenta
            Encoding.ASCII.GetBytes("\x1b[93m"), // ConsoleColor.Yellow
            Encoding.ASCII.GetBytes("\x1b[97m"), // ConsoleColor.White
        };

        protected readonly object sync = new object();
    }
}