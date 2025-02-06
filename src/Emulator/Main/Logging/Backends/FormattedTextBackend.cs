//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
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

        protected readonly object sync = new object();
    }
}
