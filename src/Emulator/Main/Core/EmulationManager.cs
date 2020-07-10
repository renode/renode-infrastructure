//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;
using System.IO;
using System;
using Antmicro.Renode.Exceptions;
using IronPython.Runtime;
using Antmicro.Renode.Peripherals.Python;
using Antmicro.Renode.Utilities;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using Antmicro.Renode.Logging;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Core
{
    public sealed class EmulationManager
    {
        public static ITimeDomain ExternalWorld { get; private set; }

        public static EmulationManager Instance { get; private set; }

        static EmulationManager()
        {
            ExternalWorld = new ExternalWorldTimeDomain();
            RebuildInstance();
        }

        [HideInMonitor]
        public static void RebuildInstance()
        {
            Instance = new EmulationManager();
        }

        public ProgressMonitor ProgressMonitor { get; private set; }

        public Emulation CurrentEmulation
        { 
            get
            {
                return currentEmulation;
            }
            set
            {
                lock(currentEmulationLock)
                {
                    currentEmulation.Dispose();
                    currentEmulation = value;
                    InvokeEmulationChanged();
                }
            }
        }

        public void EnsureTypeIsLoaded(string name)
        {
            // this is a bit hacky - calling `GetTypeByName` forces
            // TypeManager to load the type into memory making
            // it accessible for dynamically compiled C# code
            TypeManager.Instance.GetTypeByName(name);
        }

        public void Load(string path)
        {
            using(var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var deserializationResult = serializer.TryDeserialize<string>(stream, out var version);
                if(deserializationResult != DeserializationResult.OK)
                {
                    throw new RecoverableException($"There was an error when deserializing the emulation: {deserializationResult}");
                }

                deserializationResult = serializer.TryDeserialize<Emulation>(stream, out var emulation);
                if(deserializationResult != DeserializationResult.OK)
                {
                    throw new RecoverableException($"There was an error when deserializing the emulation: {deserializationResult}");
                }

                CurrentEmulation = emulation;
                CurrentEmulation.BlobManager.Load(stream);

                if(version != VersionString)
                {
                    Logger.Log(LogLevel.Warning, "Version of deserialized emulation ({0}) does not match current one {1}. Things may go awry!", version, VersionString);
                }
            }
        }

        public void Save(string path)
        {
            try
            {
                using(var stream = new FileStream(path, FileMode.Create))
                {
                    using(CurrentEmulation.ObtainPausedState())
                    {
                        try
                        {
                            serializer.Serialize(VersionString, stream);
                            serializer.Serialize(CurrentEmulation, stream);
                            CurrentEmulation.BlobManager.Save(stream);
                        }
                        catch(InvalidOperationException e)
                        {
                            throw new RecoverableException(string.Format("Error encountered during saving: {0}.", e.Message));
                        }
                    }
                }
            }
            catch(Exception)
            {
                File.Delete(path);
                throw;
            }
        }

        public void Clear()
        {
            CurrentEmulation = new Emulation();
        }

        public TimerResult StartTimer(string eventName = null)
        {
            stopwatch.Reset();
            stopwatchCounter = 0;
            var timerResult = new TimerResult {
                FromBeginning = TimeSpan.FromTicks(0),
                SequenceNumber = stopwatchCounter,
                Timestamp = CustomDateTime.Now,
                EventName = eventName
            };
            stopwatch.Start();
            return timerResult;
        }

        public TimerResult CurrentTimer(string eventName = null)
        {
            stopwatchCounter++;
            return new TimerResult {
                FromBeginning = stopwatch.Elapsed,
                SequenceNumber = stopwatchCounter,
                Timestamp = CustomDateTime.Now,
                EventName = eventName
            };
        }

        public TimerResult StopTimer(string eventName = null)
        {
            stopwatchCounter++;
            var timerResult = new TimerResult {
                FromBeginning = stopwatch.Elapsed,
                SequenceNumber = stopwatchCounter,
                Timestamp = CustomDateTime.Now,
                EventName = eventName
            };
            stopwatch.Stop();
            stopwatch.Reset();
            return timerResult;
        }

        public string VersionString
        {
            get
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                if(entryAssembly == null)
                {
                    // When running from NUnit in MonoDevelop entryAssembly is null, but we don't care
                    return string.Empty;
                }

                return string.Format("{0}, version {1} ({2})",
                    entryAssembly.GetName().Name,
                    entryAssembly.GetName().Version,
                    ((AssemblyInformationalVersionAttribute)entryAssembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion
                );
            }
        }

        public event Action EmulationChanged;

        private EmulationManager()
        {
            var settings = new Antmicro.Migrant.Customization.Settings(Antmicro.Migrant.Customization.Method.Generated, Antmicro.Migrant.Customization.Method.Generated,
                Antmicro.Migrant.Customization.VersionToleranceLevel.AllowGuidChange, disableTypeStamping: true);
            serializer = new Serializer(settings);
            serializer.ForObject<PythonDictionary>().SetSurrogate(x => new PythonDictionarySurrogate(x));
            serializer.ForSurrogate<PythonDictionarySurrogate>().SetObject(x => x.Restore());
            currentEmulation = new Emulation();
            ProgressMonitor = new ProgressMonitor();
            stopwatch = new Stopwatch();
            currentEmulationLock = new object();
        }

        private void InvokeEmulationChanged()
        {
            var emulationChanged = EmulationChanged;
            if(emulationChanged != null)
            {
                emulationChanged();
            }
        }

        private int stopwatchCounter;
        private Stopwatch stopwatch;
        private readonly Serializer serializer;
        private Emulation currentEmulation;
        private readonly object currentEmulationLock;

        /// <summary>
        /// Represents external world time domain.
        /// </summary>
        /// <remarks>
        /// Is used as a source of all external, asynchronous input events (e.g., user input on uart analyzer).
        /// </remarks>
        private class ExternalWorldTimeDomain : ITimeDomain
        {
        }
    }
}

