//
// Copyright (c) 2010-2024 Antmicro
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

        public static void RemoveBackend(ILoggerBackend backend)
        {
            foreach(var level in levels.Where(pair => pair.Key.backend == backend).ToList())
            {
                levels.TryRemove(level.Key, out var _);
            }
            UpdateMinimumLevel();
            backends.Remove(backend);
            backend.Dispose();
        }

        public static IDictionary<string, ILoggerBackend> GetBackends()
        {
            return backendNames;
        }

        public static void Dispose()
        {
            foreach(var backend in backends.Items)
            {
                backend.Dispose();
            }
            backends.Clear();
            backendNames.Clear();
        }

        public static void SetLogLevel(ILoggerBackend backend, LogLevel level, int sourceId)
        {
            levels[new BackendSourceIdPair(backend, sourceId)] = level;
            UpdateMinimumLevel();
        }

        public static void Log(LogLevel type, string message, params object[] args)
        {
            Log(null, type, message, args);
        }

        public static void Error(string message)
        {
            Log(LogLevel.Error, message);
        }

        public static void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public static void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public static void Noisy(string message)
        {
            Log(LogLevel.Noisy, message);
        }

        public static void ErrorLog(this IEmulationElement e, string message)
        {
            Log(e, LogLevel.Error, message);
        }

        public static void ErrorLog(this IEmulationElement e, string message, params object[] args)
        {
            ErrorLog(e, string.Format(message, args));
        }

        public static void WarningLog(this IEmulationElement e, string message)
        {
            Log(e, LogLevel.Warning, message);
        }

        public static void WarningLog(this IEmulationElement e, string message, params object[] args)
        {
            WarningLog(e, string.Format(message, args));
        }

        public static void InfoLog(this IEmulationElement e, string message)
        {
            Log(e, LogLevel.Info, message);
        }

        public static void InfoLog(this IEmulationElement e, string message, params object[] args)
        {
            InfoLog(e, string.Format(message, args));
        }

        public static void DebugLog(this IEmulationElement e, string message)
        {
            Log(e, LogLevel.Debug, message);
        }

        public static void DebugLog(this IEmulationElement e, string message, params object[] args)
        {
            DebugLog(e, string.Format(message, args));
        }

        public static void NoisyLog(this IEmulationElement e, string message)
        {
            Log(e, LogLevel.Noisy, message);
        }

        public static void NoisyLog(this IEmulationElement e, string message, params object[] args)
        {
            NoisyLog(e, string.Format(message, args));
        }

        public static void Log(this IEmulationElement e, LogLevel type, string message)
        {
            LogAs(e, type, message, null);
        }

        public static void Log(this IEmulationElement e, LogLevel type, string message, params object[] args)
        {
            LogAs(e, type, message, args);
        }

        public static void LogAs(object o, LogLevel type, string message, params object[] args)
        {
            var emulationManager = EmulationManager.Instance;
            if(emulationManager != null)
            {
                ((ActualLogger)emulationManager.CurrentEmulation.CurrentLogger).ObjectInnerLog(o, type, message, args);
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

                    // we set ids here to avoid the need of locking counter in `ObjectInnerLog`
                    entry.Id = Logger.nextEntryId++;
                    WriteLogEntryToBackends(entry);
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

            [PostDeserialization]
            private void Init()
            {
                logSourcesMap = new LogSourcesMap();
                nextNameId = 0;

                innerLock = new object();

                SynchronousLogging = ConfigurationManager.Instance.Get("general", "use-synchronous-logging", false);
                alwaysAppendMachineName = ConfigurationManager.Instance.Get("general", "always-log-machine-name", false);

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
                }
            }

            [Transient]
            private bool alwaysAppendMachineName;

            [Transient]
            private bool useSynchronousLogging;

            [Transient]
            private object innerLock;

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
