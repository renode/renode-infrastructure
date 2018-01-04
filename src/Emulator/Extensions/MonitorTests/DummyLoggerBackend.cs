//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using System.Collections.Generic;

namespace Antmicro.Renode.MonitorTests
{
    public class DummyLoggerBackend : ILoggerBackend
    {
        public bool IsControllable { get { return true; } }
        public bool AcceptEverything { get { return false; } }

        public IDictionary<int, LogLevel> GetCustomLogLevels()
        {
            return new Dictionary<int, LogLevel>();
        }

        public LogEntry NextEntry()
        {
            if(entries.Count > 0)
            {
                return entries.Dequeue();
            }
            return null;
        }

        public LogLevel GetLogLevel()
        {
            return LogLevel.Noisy;
        }

        public void Log(LogEntry entry)
        {
            entries.Enqueue(entry);
        }

        public void Clear()
        {
            entries.Clear();
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }

        public void SetLogLevel(LogLevel level, int sourceId = -1)
        {
        }

        public bool ShouldBePrinted(object obj, LogLevel level)
        {
            return true;
        }

        private readonly Queue<LogEntry> entries = new Queue<LogEntry>();
    }
}

