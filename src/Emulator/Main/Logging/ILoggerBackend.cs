//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Logging
{
    public interface ILoggerBackend : IDisposable
    {
        void Log(LogEntry entry);
        void SetLogLevel(LogLevel level, int sourceId = -1);
        LogLevel GetLogLevel();
        IDictionary<int, LogLevel> GetCustomLogLevels();
        void Reset();
        void Flush();

        bool IsControllable { get; }
    }
}
