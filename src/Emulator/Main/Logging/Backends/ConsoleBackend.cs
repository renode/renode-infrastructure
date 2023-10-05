//
// Copyright (c) 2010-2023 Antmicro
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
        public bool ReportRepeatingLines { get; set; }

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

                var width = 0;
                
                if(!PlainMode)
                {
                    try
                    {
                        width = Console.WindowWidth;
                    }
                    catch(IOException)
                    {
                        // It sometimes happens that we cannot read the console window width.
                        // There is not much we can do about it, so just use the default value of 0 (this will disable the repeated messages counter)
                    }
                }

                if(output == Console.Out && !ReportRepeatingLines && width != 0 &&
                   lastMessage == message && lastMessageLinesCount != -1 && lastType == type && !isRedirected)
                {
                    try
                    {
                        counter++;
                        Console.CursorVisible = false;
                        Console.CursorTop = Math.Max(0, Console.CursorTop - lastMessageLinesCount);
                        // it can happen that console is resized between one and other write
                        // in case console is widened it would not erase previous messages
                        var realLine = string.Format("{0} ({1})", line, counter);
                        var currentLinesCount = GetMessageLinesCount(realLine, width);
                        var lineDiff = Math.Max(0, lastMessageLinesCount - currentLinesCount);
                        
                        Console.WriteLine(realLine);
                        lineDiff.Times(() => Console.WriteLine(Enumerable.Repeat<char>(' ', width - 1).ToArray()));
                        Console.CursorVisible = true;
                        Console.CursorTop = Math.Max(0, Console.CursorTop - lineDiff);
                        lastMessageLinesCount = GetMessageLinesCount(realLine, width);
                    }
                    catch(ArgumentOutOfRangeException)
                    {
                        // console was resized during computations
                        Console.Clear();
                        WriteNewLine(line, width);
                    }
                }
                else
                {
                    WriteNewLine(line, width);
                    lastMessage = message;
                    lastType = type;
                }
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

        private void WriteNewLine(string line, int width)
        {
            counter = 1;
            output.WriteLine(line);
            if(output == Console.Out && !isRedirected && width != 0)
            {
                lastMessageLinesCount = GetMessageLinesCount(line, width);
            }
            else
            {
                lastMessageLinesCount = -1; // -1 here means that during last message logging output wasn't console
            }
        }

        private static int GetMessageLinesCount(string message, int width)
        {
            var cnt = message.Split(new [] { System.Environment.NewLine }, StringSplitOptions.None).Sum(x => GetMessageLinesCountForRow(x, width));
            return cnt;
        }
        
        private static int GetMessageLinesCountForRow(string row, int width)
        {
            DebugHelper.Assert(width != 0);

            var cnt = Convert.ToInt32(Math.Ceiling(1.0 * row.Length / width));
            return cnt;
        }

        private TextWriter output = Console.Out;
        private object syncObject;
        private readonly bool isRedirected;
        private string lastMessage;
        private int lastMessageLinesCount;
        private int counter = 1;
        private LogLevel lastType;
    }
}

