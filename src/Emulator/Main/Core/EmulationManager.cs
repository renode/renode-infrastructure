//
// Copyright (c) 2010-2017 Antmicro
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

namespace Antmicro.Renode.Core
{
    public sealed class EmulationManager
    {
        public static EmulationManager Instance { get; private set; }

        static EmulationManager()
        {
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
                var oldEmulation = Interlocked.Exchange(ref currentEmulation, value);
                oldEmulation.Dispose();
                InvokeEmulationChanged();
            }
        }

        public void Load(string path)
        {
            string version;
            using(var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                version = serializer.Deserialize<string>(stream);
                CurrentEmulation = serializer.Deserialize<Emulation>(stream);
                CurrentEmulation.BlobManager.Load(stream);
            }

            if(version != VersionString)
            {
                Logger.Log(LogLevel.Warning, "Version of deserialized emulation ({0}) does not match current one {1}. Things may go awry!", version, VersionString);
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
                return string.Format("{0}, version {1} ({2})", 
		            Assembly.GetEntryAssembly().GetName().Name, 
		            Assembly.GetEntryAssembly().GetName().Version, 
		            ((AssemblyInformationalVersionAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion
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
    }
}

