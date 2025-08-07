//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

// this strange construct is to globally enable/disable `Trace` methods using single #define here;
// normally each file calling `Trace` should have it defined either on its own or globally in csproj/command line;
#if DEBUG
// uncomment line below to enable tracing
//#define TRACE_ENABLED
#endif

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Utilities;
using System.Threading;
using System.Runtime.CompilerServices;
using System.IO;
using System.Collections.Concurrent;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities.Collections;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Antmicro.Renode.Debugging;

namespace Antmicro.Renode.Logging
{
    public static class Logger
    {
        public static void AddBackend(ILoggerBackend backend, string name, bool overwrite = false)
        {
            lock(backendsChangeLock)
            {
                backendNames.AddOrUpdate(name, backend, (key, value) =>
                {
                    if(!overwrite)
                    {
                        throw new RecoverableException(string.Format("Backend with name '{0}' already exists", key));
                    }
                    value.Dispose();
                    return backend;
                });
                levels[new BackendSourceIdPair(backend, -1)] = backend.GetLogLevel();
                foreach(var level in backend.GetCustomLogLevels())
                {
                    levels[new BackendSourceIdPair(backend, level.Key)] = level.Value;
                }
                UpdateMinimumLevel();
                backends.Add(backend);
            }
        }

        public static void RemoveBackend(ILoggerBackend backend)
        {
            lock(backendsChangeLock)
            {
                foreach(var level in levels.Where(pair => pair.Key.backend == backend).ToList())
                {
                    levels.TryRemove(level.Key, out var _);
                }
                UpdateMinimumLevel();

                foreach(var nameEntry in backendNames.Where(pair => pair.Value == backend).ToArray())
                {
                    backendNames.TryRemove(nameEntry.Key, out var _);
                }

                backends.Remove(backend);
                backend.Dispose();
            }
        }

        public static IDictionary<string, ILoggerBackend> GetBackends()
        {
            lock(backendsChangeLock)
            {
                return backendNames;
            }
        }

        public static void Dispose()
        {
            Flush();
            foreach(var backend in backends.Items)
            {
                backend.Dispose();
            }
            backends.Clear();
            backendNames.Clear();
            levels.Clear();
        }

        public static void SetLogLevel(ILoggerBackend backend, LogLevel level, int sourceId)
        {
            levels[new BackendSourceIdPair(backend, sourceId)] = level;
            UpdateMinimumLevel();
        }

        public static void Log(LogLevel type, string message, params object[] args)
        {
            LogAs(null, type, message, args);
        }

        public static void Log(LogLevel type, string message)
        {
            LogAs(null, type, message);
        }

        public static void Log(LogLevel type, string message, object arg1)
        {
            LogAs(null, type, message, arg1);
        }

        public static void Log(LogLevel type, string message, object arg1, object arg2)
        {
            LogAs(null, type, message, arg1, arg2);
        }

        public static void Log(LogLevel type, string message, object arg1, object arg2, object arg3)
        {
            LogAs(null, type, message, arg1, arg2, arg3);
        }

        public static void Error(string message)
        {
            LogAs(null, LogLevel.Error, message);
        }

        public static void Warning(string message)
        {
            LogAs(null, LogLevel.Warning, message);
        }

        public static void Info(string message)
        {
            LogAs(null, LogLevel.Info, message);
        }

        public static void Debug(string message)
        {
            LogAs(null, LogLevel.Debug, message);
        }

        public static void Noisy(string message)
        {
            LogAs(null, LogLevel.Noisy, message);
        }

        public static void ErrorLog(this IEmulationElement e, string message, params object[] args)
        {
            LogAs(e, LogLevel.Error, message, args);
        }

        public static void ErrorLog(this IEmulationElement e, string message)
        {
            LogAs(e, LogLevel.Error, message);
        }

        public static void ErrorLog(this IEmulationElement e, string message, object arg1)
        {
            LogAs(e, LogLevel.Error, message, arg1);
        }

        public static void ErrorLog(this IEmulationElement e, string message, object arg1, object arg2)
        {
            LogAs(e, LogLevel.Error, message, arg1, arg2);
        }

        public static void ErrorLog(this IEmulationElement e, string message, object arg1, object arg2, object arg3)
        {
            LogAs(e, LogLevel.Error, message, arg1, arg2, arg3);
        }

        public static void WarningLog(this IEmulationElement e, string message, params object[] args)
        {
            LogAs(e, LogLevel.Warning, message, args);
        }

        public static void WarningLog(this IEmulationElement e, string message)
        {
            LogAs(e, LogLevel.Warning, message);
        }

        public static void WarningLog(this IEmulationElement e, string message, object arg1)
        {
            LogAs(e, LogLevel.Warning, message, arg1);
        }

        public static void WarningLog(this IEmulationElement e, string message, object arg1, object arg2)
        {
            LogAs(e, LogLevel.Warning, message, arg1, arg2);
        }

        public static void WarningLog(this IEmulationElement e, string message, object arg1, object arg2, object arg3)
        {
            LogAs(e, LogLevel.Warning, message, arg1, arg2, arg3);
        }

        public static void InfoLog(this IEmulationElement e, string message, params object[] args)
        {
            LogAs(e, LogLevel.Info, message, args);
        }

        public static void InfoLog(this IEmulationElement e, string message)
        {
            LogAs(e, LogLevel.Info, message);
        }

        public static void InfoLog(this IEmulationElement e, string message, object arg1)
        {
            LogAs(e, LogLevel.Info, message, arg1);
        }

        public static void InfoLog(this IEmulationElement e, string message, object arg1, object arg2)
        {
            LogAs(e, LogLevel.Info, message, arg1, arg2);
        }

        public static void InfoLog(this IEmulationElement e, string message, object arg1, object arg2, object arg3)
        {
            LogAs(e, LogLevel.Info, message, arg1, arg2, arg3);
        }

        public static void DebugLog(this IEmulationElement e, string message, params object[] args)
        {
            LogAs(e, LogLevel.Debug, message, args);
        }

        public static void DebugLog(this IEmulationElement e, string message)
        {
            LogAs(e, LogLevel.Debug, message);
        }

        public static void DebugLog(this IEmulationElement e, string message, object arg1)
        {
            LogAs(e, LogLevel.Debug, message, arg1);
        }

        public static void DebugLog(this IEmulationElement e, string message, object arg1, object arg2)
        {
            LogAs(e, LogLevel.Debug, message, arg1, arg2);
        }

        public static void DebugLog(this IEmulationElement e, string message, object arg1, object arg2, object arg3)
        {
            LogAs(e, LogLevel.Debug, message, arg1, arg2, arg3);
        }

        public static void NoisyLog(this IEmulationElement e, string message, params object[] args)
        {
            LogAs(e, LogLevel.Noisy, message, args);
        }

        public static void NoisyLog(this IEmulationElement e, string message)
        {
            LogAs(e, LogLevel.Noisy, message);
        }

        public static void NoisyLog(this IEmulationElement e, string message, object arg1)
        {
            LogAs(e, LogLevel.Noisy, message, arg1);
        }

        public static void NoisyLog(this IEmulationElement e, string message, object arg1, object arg2)
        {
            LogAs(e, LogLevel.Noisy, message, arg1, arg2);
        }

        public static void NoisyLog(this IEmulationElement e, string message, object arg1, object arg2, object arg3)
        {
            LogAs(e, LogLevel.Noisy, message, arg1, arg2, arg3);
        }

        public static void Log(this IEmulationElement e, LogLevel type, string message, params object[] args)
        {
            LogAs(e, type, message, args);
        }

        public static void Log(this IEmulationElement e, LogLevel type, string message)
        {
            LogAs(e, type, message);
        }

        public static void Log(this IEmulationElement e, LogLevel type, string message, object arg1)
        {
            LogAs(e, type, message, arg1);
        }

        public static void Log(this IEmulationElement e, LogLevel type, string message, object arg1, object arg2)
        {
            LogAs(e, type, message, arg1, arg2);
        }

        public static void Log(this IEmulationElement e, LogLevel type, string message, object arg1, object arg2, object arg3)
        {
            LogAs(e, type, message, arg1, arg2, arg3);
        }

        public static void LogAs(object o, LogLevel type, string message, params object[] args)
        {
            // The inner log method is only skipped if the level of this message is lower than the level set
            // for any source on any backend. This means that setting any element's log level to Debug will
            // make all Debug and higher logs get sent to the backends.
            if(type < minLevel)
            {
                return;
            }
            var emulationManager = EmulationManager.Instance;
            if(emulationManager != null)
            {
                ((ActualLogger)emulationManager.CurrentEmulation.CurrentLogger).ObjectInnerLog(o, type, message, args);
            }
        }

        public static void LogAs(object o, LogLevel type, string message)
        {
            if(type < minLevel)
            {
                return;
            }
            var emulationManager = EmulationManager.Instance;
            if(emulationManager != null)
            {
                ((ActualLogger)emulationManager.CurrentEmulation.CurrentLogger).ObjectInnerLog(o, type, message);
            }
        }

        public static void LogAs(object o, LogLevel type, string message, object arg1)
        {
            if(type < minLevel)
            {
                return;
            }
            var emulationManager = EmulationManager.Instance;
            if(emulationManager != null)
            {
                ((ActualLogger)emulationManager.CurrentEmulation.CurrentLogger).ObjectInnerLog(o, type, message, arg1);
            }
        }

        public static void LogAs(object o, LogLevel type, string message, object arg1, object arg2)
        {
            if(type < minLevel)
            {
                return;
            }
            var emulationManager = EmulationManager.Instance;
            if(emulationManager != null)
            {
                ((ActualLogger)emulationManager.CurrentEmulation.CurrentLogger).ObjectInnerLog(o, type, message, arg1, arg2);
            }
        }

        public static void LogAs(object o, LogLevel type, string message, object arg1, object arg2, object arg3)
        {
            if(type < minLevel)
            {
                return;
            }
            var emulationManager = EmulationManager.Instance;
            if(emulationManager != null)
            {
                ((ActualLogger)emulationManager.CurrentEmulation.CurrentLogger).ObjectInnerLog(o, type, message, arg1, arg2, arg3);
            }
        }

        public static void Flush()
        {
            var emulationManager = EmulationManager.Instance;
            if(emulationManager != null)
            {
                ((ActualLogger)emulationManager.CurrentEmulation.CurrentLogger).Flush();
            }
        }

// see a comment at the top
#if !TRACE_ENABLED
        [Conditional("TRACE_ENABLED")]
#endif
        public static void Trace(this object o, LogLevel type, string message = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string fileName = null)
        {
            var fullMessage = new StringBuilder($"[TRACE][t:{Thread.CurrentThread.Name}/{Thread.CurrentThread.ManagedThreadId}]");
#if DEBUG
            if(o is IIdentifiable identifiable)
            {
                fullMessage.Append($"[s:{identifiable.GetDescription()}]");
            }
#endif
            fullMessage.Append($" {message} in {caller} ({Path.GetFileName(fileName)}:{lineNumber})");

            LogAs(o, type, fullMessage.ToString());
        }

// see a comment at the top
#if !TRACE_ENABLED
        [Conditional("TRACE_ENABLED")]
#endif
        public static void Trace(this object o, string message = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string fileName = null)
        {
            Trace(o, LogLevel.Info, message, lineNumber, caller, fileName);
        }

        public static IDisposable TraceRegion(this object o, string message = null,
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string fileName = null)
        {
            var result = new DisposableWrapper();
            Trace(o, $"Entering: {message}", lineNumber, caller, fileName);
            result.RegisterDisposeAction(() => Trace(o, $"Leaving: {message}. Entered", lineNumber, caller, fileName));
            return result;
        }

        public static void LogUnhandledRead(this IPeripheral peripheral, long offset)
        {
            peripheral.Log(LogLevel.Warning, "Unhandled read from offset 0x{0:X}.", offset);
        }

        public static void LogUnhandledWrite(this IPeripheral peripheral, long offset, ulong value)
        {
            peripheral.Log(LogLevel.Warning, "Unhandled write to offset 0x{0:X}, value 0x{1:X}.", offset, value);
        }

        public static bool PrintFullName { get; set; }

        public static LogLevel MinimumLogLevel => minLevel;

        public static readonly LogLevel DefaultLogLevel = LogLevel.Info;

        internal static ILogger GetLogger()
        {
            var logger = new ActualLogger();
            foreach(var backend in backends.Items)
            {
                backend.Reset();
            }
            return logger;
        }

        private static ulong nextEntryId = 0;
        private static LogLevel minLevel = DefaultLogLevel;
        private static readonly object backendsChangeLock = new object();
        private static readonly ConcurrentDictionary<string, ILoggerBackend> backendNames = new ConcurrentDictionary<string, ILoggerBackend>();
        private static readonly FastReadConcurrentCollection<ILoggerBackend> backends = new FastReadConcurrentCollection<ILoggerBackend>();
        private static readonly ConcurrentDictionary<BackendSourceIdPair, LogLevel> levels = new ConcurrentDictionary<BackendSourceIdPair, LogLevel>();


        private static string GetGenericName(object o)
        {
            if(Misc.IsPythonObject(o))
            {
                return Misc.GetPythonName(o);
            }
            var type = o.GetType();
            return PrintFullName ? type.FullName : type.Name;
        }

        private static void UpdateMinimumLevel()
        {
            minLevel = levels.Min(l => l.Value);
        }

        internal class ActualLogger : ILogger
        {
            public ActualLogger()
            {
                Init();
            }

            public void Dispose()
            {
                if(!SynchronousLogging)
                {
                    StopLoggingThread();
                }
            }

            public string GetMachineName(int id)
            {
                string objectName;
                string machineName;
                if(TryGetName(id, out objectName, out machineName))
                {
                    return machineName;
                }
                return null;
            }

            public string GetObjectName(int id)
            {
                string objectName;
                string machineName;
                if(TryGetName(id, out objectName, out machineName))
                {
                    return objectName;
                }
                return null;
            }

            public bool TryGetName(int id, out string objectName, out string machineName)
            {
                object obj;
                if(logSourcesMap.TryGetObject(id, out obj))
                {
                    if(EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementName(obj, out objectName, out machineName))
                    {
                        return true;
                    }
                }

                objectName = null;
                machineName = null;
                return false;
            }

            public int GetOrCreateSourceId(object source)
            {
                return logSourcesMap.GetOrCreateId(source, () => Interlocked.Increment(ref nextNameId));
            }

            public bool TryGetSourceId(object source, out int id)
            {
                return logSourcesMap.TryGetId(source, out id);
            }

            public void ObjectInnerLog(object o, LogLevel type, string message, params object[] args)
            {
                int sourceId = (o == null) ? -1 : GetOrCreateSourceId(o);
                if(args?.Length > 0)
                {
                    message = string.Format(message, args);
                }

                var entry = new LogEntry(CustomDateTime.Now, type, message, sourceId, alwaysAppendMachineName, Thread.CurrentThread.ManagedThreadId);

                if(SynchronousLogging)
                {
                    lock(innerLock)
                    {
                        entry.Id = Logger.nextEntryId++;
                        WriteLogEntryToBackends(entry);
                    }
                }
                else
                {
                    entries.Add(entry);
                }
            }

            public void Flush()
            {
                if(SynchronousLogging)
                {
                    FlushBackends();
                    return;
                }

                StopLoggingThread();

                if(aggregateLogs)
                {
                    FlushAggregatedLogs();
                }

                // switch collections to avoid
                // stucking forever in the loop below
                var localEntries = entries;
                entries = new BlockingCollection<LogEntry>(10000);

                while(localEntries.TryTake(out var entry))
                {
                    // we set ids here to avoid the need of locking counter in `ObjectInnerLog`
                    entry.Id = Logger.nextEntryId++;
                    WriteLogEntryToBackends(entry);
                }

                FlushBackends();
                StartLoggingThread();
            }

            public bool SynchronousLogging
            {
                get => useSynchronousLogging;
                set
                {
                    if(useSynchronousLogging == value)
                    {
                        return;
                    }

                    useSynchronousLogging = value;
                    if(value)
                    {
                        StopLoggingThread();
                    }
                    else
                    {
                        StartLoggingThread();
                    }
                }
            }

            private void LoggingThreadBody()
            {
                while(!stopThread)
                {
                    LogEntry entry;
                    try
                    {
                        entry = entries.Take(cancellationToken.Token);
                    }
                    catch(OperationCanceledException)
                    {
                        break;
                    }

                    if(!aggregateLogs)
                    {
                        // we set ids here to avoid the need of locking counter in `ObjectInnerLog`
                        entry.Id = Logger.nextEntryId++;
                        WriteLogEntryToBackends(entry);
                        continue;
                    }

                    lock(aggregationFlushLock)
                    {
                        if(entry.EqualsWithoutIdTimeAndCount(lastLoggedEntry))
                        {
                            repeatLogEntryCount++;
                            if(repeatLogEntryCount >= MaxRepeatedLogs)
                            {
                                FlushAggregatedLogs();
                            }
                        }
                        else
                        {
                            FlushAggregatedLogs();

                            // we set ids here to avoid the need of locking counter in `ObjectInnerLog`
                            entry.Id = Logger.nextEntryId++;
                            WriteLogEntryToBackends(entry);
                            lastLoggedEntry = entry;
                        }
                    }
                }
            }

            private void WriteLogEntryToBackends(LogEntry entry)
            {
                var allBackends = Logger.backends.Items;
                for(var i = 0; i < allBackends.Length; i++)
                {
                    allBackends[i].Log(entry);
                }
            }

            private void FlushBackends()
            {
                var allBackends = Logger.backends.Items;
                for(var i = 0; i < allBackends.Length; i++)
                {
                    allBackends[i].Flush();
                }
            }

            private void FlushAggregatedLogs()
            {
                if(repeatLogEntryCount == 0)
                {
                    return;
                }

                lastLoggedEntry.Count = repeatLogEntryCount;

                WriteLogEntryToBackends(lastLoggedEntry);

                repeatLogEntryCount = 0;

                // reset timer
                logAggregatorTimer?.Change(MaxAggregateTimeMs, MaxAggregateTimeMs);
            }

            [PostDeserialization]
            private void Init()
            {
                logSourcesMap = new LogSourcesMap();
                nextNameId = 0;

                innerLock = new object();
                aggregationFlushLock = new object();

                SynchronousLogging = ConfigurationManager.Instance.Get("general", "use-synchronous-logging", false);
                alwaysAppendMachineName = ConfigurationManager.Instance.Get("general", "always-log-machine-name", false);
                aggregateLogs = ConfigurationManager.Instance.Get("general", "collapse-repeated-log-entries", true);

                if(!SynchronousLogging)
                {
                    entries = new BlockingCollection<LogEntry>(10000);

                    StartLoggingThread();
                }
            }

            private void StartLoggingThread()
            {
                lock(innerLock)
                {
                    if(aggregateLogs)
                    {
                        logAggregatorTimer = new Timer(x =>
                        {
                            lock(aggregationFlushLock)
                            {
                                FlushAggregatedLogs();
                            }
                        }, null, MaxAggregateTimeMs, MaxAggregateTimeMs);
                    }

                    cancellationToken = new CancellationTokenSource();
                    loggingThread = new Thread(LoggingThreadBody);
                    loggingThread.IsBackground = true;
                    loggingThread.Name = "Logging thread";
                    loggingThread.Start();
                }
            }

            private void StopLoggingThread()
            {
                lock(innerLock)
                {
                    if(loggingThread == null)
                    {
                        return;
                    }

                    stopThread = true;
                    cancellationToken.Cancel();
                    loggingThread.Join();
                    loggingThread = null;

                    logAggregatorTimer?.Dispose();
                    logAggregatorTimer = null;
                }
            }

            [Transient]
            private int repeatLogEntryCount;
            [Transient]
            private LogEntry lastLoggedEntry;
            [Transient]
            private Timer logAggregatorTimer;
            [Transient]
            private bool aggregateLogs;

            [Transient]
            private bool alwaysAppendMachineName;

            [Transient]
            private bool useSynchronousLogging;

            [Transient]
            private object innerLock;

            [Transient]
            private object aggregationFlushLock;

            [Transient]
            private Thread loggingThread;

            [Transient]
            private CancellationTokenSource cancellationToken;

            [Transient]
            private volatile bool stopThread = false;

            [Transient]
            private BlockingCollection<LogEntry> entries;
            [Transient]
            private int nextNameId;
            [Transient]
            private LogSourcesMap logSourcesMap;

            private const int MaxRepeatedLogs = 10000;
            private const int MaxAggregateTimeMs = 500;

            private class LogSourcesMap
            {
                public LogSourcesMap()
                {
                    objectToIdMap = new ConcurrentDictionary<WeakWrapper<object>, int>();
                    idToObjectMap = new ConcurrentDictionary<int, WeakWrapper<object>>();
                }

                public int GetOrCreateId(object o, Func<int> idProvider)
                {
                    return objectToIdMap.GetOrAdd(WeakWrapper<object>.CreateForComparison(o), s =>
                    {
                        s.ConvertToRealWeakWrapper();

                        var id = idProvider();
                        idToObjectMap.TryAdd(id, s);
                        return id;
                    });
                }

                public bool TryGetId(object o, out int sourceId)
                {
                    return objectToIdMap.TryGetValue(WeakWrapper<object>.CreateForComparison(o), out sourceId);
                }

                public bool TryGetObject(int id, out object obj)
                {
                    WeakWrapper<object> outResult;
                    var result = idToObjectMap.TryGetValue(id, out outResult);
                    if(result)
                    {
                        return outResult.TryGetTarget(out obj);
                    }

                    obj = null;
                    return false;
                }

                private readonly ConcurrentDictionary<int, WeakWrapper<object>> idToObjectMap;
                private readonly ConcurrentDictionary<WeakWrapper<object>, int> objectToIdMap;
            }
        }

        internal struct BackendSourceIdPair
        {
            public BackendSourceIdPair(ILoggerBackend backend, int sourceId)
            {
                this.backend = backend;
                this.sourceId = sourceId;
            }

            public readonly ILoggerBackend backend;
            public readonly int sourceId;
        }
    }
}
