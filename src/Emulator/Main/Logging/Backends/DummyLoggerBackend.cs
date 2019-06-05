//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using System.Collections.Generic;

namespace Antmicro.Renode.Logging.Backends
{
    public class DummyLoggerBackend : ILoggerBackend
    {
        public IDictionary<int, LogLevel> GetCustomLogLevels()
        {
            return new Dictionary<int, LogLevel>();
        }

        public LogLevel GetLogLevel()
        {
            return LogLevel.Noisy;
        }

        public void Log(LogEntry entry)
        {
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

        public bool IsControllable { get { return true; } }
    }
}

