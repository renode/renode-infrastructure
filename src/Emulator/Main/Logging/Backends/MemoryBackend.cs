//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Logging.Backends
{
    public class MemoryBackend : TextBackend
    {
        public static int MaxCount { get; private set; }

        public MemoryBackend()
        {
            entries = new Queue<MemoryLogEntry>();
            locker = new object();
            MaxCount = ConfigurationManager.Instance.Get("general", "log-history-limit", DefaultLimit);
        }

        public override void Log(LogEntry entry, Logger.TimestampType timestampType)
        {
            lock(locker)
            {
                if(!ShouldBeLogged(entry))
                {
                    return;
                }

                if(entries.Count == MaxCount)
                {
                    entries.Dequeue();
                }

                var memoryLogEntry = new MemoryLogEntry(entry, timestampType, FormatLogEntry);
                entries.Enqueue(memoryLogEntry);
            }
        }

        public IEnumerable<MemoryLogEntry> GetMemoryLogEntries(int numberOfElements)
        {
            lock(locker)
            {
                return entries.Skip(Math.Max(0, entries.Count() - numberOfElements)).ToList();
            }
        }

        public const string Name = "memory";
        private readonly object locker;

        private readonly Queue<MemoryLogEntry> entries;

        private const int DefaultLimit = 1000;
    }

    public class MemoryLogEntry
    {
        public MemoryLogEntry(LogEntry entry, Logger.TimestampType timestampType, Func<LogEntry, Logger.TimestampType, string> formatter)
        {
            DateTime = entry.Time;
            Type = entry.Type;
            Message = formatter(entry, timestampType);
        }

        public DateTime DateTime { get; }

        public LogLevel Type { get; }

        public string Message { get; }
    }
}