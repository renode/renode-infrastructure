//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Debugging;

namespace Antmicro.Renode.Logging
{
    public class ConsoleBackend : TextBackend
    {
        public static ConsoleBackend Instance { get; private set; }

        public string WindowTitle
        {
            // Console.Title *getter* is only supported on Windows,
            // so we always return an empty string on other platforms.
            get
            {
#if PLATFORM_WINDOWS
                return PlainMode
                    ? string.Empty
                    : Console.Title;
#else
                return string.Empty;
#endif
            }
            // The setter is supported on all the platforms we target,
            // so it doesn't need the if clause.
            set
            {
                if(!PlainMode)
                {
                    Console.Title = value;
                }
            }
        }

        // do not generate color output
        // do not set window title
        // minimize the use of VT100 codes
        public bool PlainMode { get; set; }

        public bool LogThreadId { get; set; }

        public override void Log(LogEntry entry)
        {
            if(!ShouldBeLogged(entry))
            {
                return;
            }

            var type = entry.Type;
            var changeColor = !PlainMode && output == Console.Out && type.Color.HasValue && !isRedirected;
            var message = FormatLogEntry(entry);

            lock(syncObject)
            {
                if(changeColor)
                {
                    Console.ForegroundColor = type.Color.Value;
                }
                string line;
                if(LogThreadId && entry.ThreadId != null)
                {
                    line = string.Format("{0:HH:mm:ss.ffff} [{1}] ({3}) {2}", CustomDateTime.Now, type, message, entry.ThreadId);
                }
                else
                {
                    line = string.Format("{0:HH:mm:ss.ffff} [{1}] {2}", CustomDateTime.Now, type, message);
                }

                WriteNewLine(line);
                if(changeColor)
                {
                    Console.ResetColor();
                }
            }
        }

        public override void Flush()
        {
            output.Flush();
        }

        static ConsoleBackend()
        {
            Instance = new ConsoleBackend();
        }

        private ConsoleBackend()
        {
            syncObject = new object();
            isRedirected = Console.IsOutputRedirected;
        }

        public override void Dispose()
        {

        }

        private void WriteNewLine(string line)
        {
            output.WriteLine(line);
        }

        private TextWriter output = Console.Out;
        private object syncObject;
        private readonly bool isRedirected;
    }
}
