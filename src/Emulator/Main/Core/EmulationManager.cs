//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Migrant;
using System.IO;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
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

                    if(profilerPathPrefix != null)
                    {
                        currentEmulation.MachineAdded += EnableProfilerInMachine;
                    }
                }
            }
        }

        public void EnableProfilerGlobally(WriteFilePath pathPrefix)
        {
            profilerPathPrefix = pathPrefix;

            CurrentEmulation.MachineAdded -= EnableProfilerInMachine;
            CurrentEmulation.MachineAdded += EnableProfilerInMachine;

            foreach(var machine in CurrentEmulation.Machines)
            {
                EnableProfilerInMachine(machine);
            }
        }

        public void EnsureTypeIsLoaded(string name)
        {
            // this is a bit hacky - calling `GetTypeByName` forces
            // TypeManager to load the type into memory making
            // it accessible for dynamically compiled C# code
            try
            {
                TypeManager.Instance.GetTypeByName(name);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"Unable to load the type `{name}`: {e.Message}");
            }
        }

        public void Load(ReadFilePath path)
        {
            using(var fstream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using(var stream = path.ToString().EndsWith(".gz", StringComparison.InvariantCulture)
                                ? (Stream) new GZipStream(fstream, CompressionMode.Decompress)
                                : (Stream) fstream)
            {
                var deserializationResult = serializer.TryDeserialize<string>(stream, out var version);
                if(deserializationResult != DeserializationResult.OK)
                {
                    throw new RecoverableException($"There was an error when deserializing the emulation: {deserializationResult}\n Underlying exception: {serializer.LastException.Message}\n{serializer.LastException.StackTrace}");
                }

                deserializationResult = serializer.TryDeserialize<Emulation>(stream, out var emulation);
                if(deserializationResult != DeserializationResult.OK)
                {
                    throw new RecoverableException($"There was an error when deserializing the emulation: {deserializationResult}\n Underlying exception: {serializer.LastException.Message}\n{serializer.LastException.StackTrace}");
                }

                CurrentEmulation = emulation;
                CurrentEmulation.BlobManager.Load(stream, fstream.Name);

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
                    using(CurrentEmulation.ObtainSafeState())
                    {
                        try
                        {
                            CurrentEmulation.SnapshotTracker.Save(CurrentEmulation.MasterTimeSource.ElapsedVirtualTime, path);
                            serializer.Serialize(VersionString, stream);
                            serializer.Serialize(CurrentEmulation, stream);
                            CurrentEmulation.BlobManager.Save(stream);
                        }
                        catch(InvalidOperationException e)
                        {
                            var message = string.Format("Error encountered during saving: {0}", e.Message);
                            if(e is NonSerializableTypeException && serializer.Settings.SerializationMethod == Migrant.Customization.Method.Generated)
                            {
                                message += "\nHint: Set 'serialization-mode = Reflection' in the Renode config file for detailed information.";
                            }
                            else if(e is NonSerializableTypeException && e.Data.Contains("nonSerializableObject") && e.Data.Contains("parentsObjects"))
                            {
                                if(TryFindPath(e.Data["nonSerializableObject"], (Dictionary<object, IEnumerable<object>>)e.Data["parentsObjects"], typeof(Emulation), out List<object> parentsPath))
                                {
                                    var pathText = new StringBuilder();

                                    parentsPath.Reverse();
                                    foreach(var o in parentsPath)
                                    {
                                        pathText.Append(o.GetType().Name);
                                        pathText.Append(" => ");
                                    }
                                    pathText.Remove(pathText.Length - 4, 4);
                                    pathText.Append("\n");

                                    message += "The class path that led to it was:\n" + pathText;
                                }
                            }

                            throw new RecoverableException(message);
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

        public void EnableCompiledFilesCache(bool value)
        {
            CompiledFilesCache.Enabled = value;
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

                try
                {
                    return string.Format("{0}, version {1} ({2})",
                        entryAssembly.GetName().Name,
                        entryAssembly.GetName().Version,
                        ((AssemblyInformationalVersionAttribute)entryAssembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion
                    );
                }
                catch(System.Exception)
                {
                    return string.Empty;
                }

            }
        }
        
        public SimpleFileCache CompiledFilesCache { get; } = new SimpleFileCache("compiler-cache", !Emulator.InCIMode && ConfigurationManager.Instance.Get("general", "compiler-cache-enabled", false));

        public event Action EmulationChanged;

        public static bool DisableEmulationFilesCleanup = false;

        private static bool TryFindPath(object obj, Dictionary<object, IEnumerable<object>> parents, Type finalType, out List<object> resultPath)
        {
            return TryFindPathInnerRecursive(obj, parents, new List<object>(), finalType, out resultPath);
        }

        private static bool TryFindPathInnerRecursive(object obj, Dictionary<object, IEnumerable<object>> parents, List<object> currentPath, Type finalType, out List<object> resultPath)
        {
            currentPath.Add(obj);
            if(obj.GetType() == finalType)
            {
                resultPath = currentPath;
                return true;
            }

            if(parents.ContainsKey(obj))
            {
                foreach(var parent in parents[obj])
                {
                    if(!currentPath.Contains(parent))
                    {
                        if(TryFindPathInnerRecursive(parent, parents, currentPath, finalType, out resultPath))
                        {
                            return true;
                        }
                    }
                }

                currentPath.RemoveAt(currentPath.Count - 1);
                resultPath = null;
                return false;
            }
            else
            {
                resultPath = currentPath;
                return false;
            }
        }

        private EmulationManager()
        {
            var serializerMode = ConfigurationManager.Instance.Get("general", "serialization-mode", Antmicro.Migrant.Customization.Method.Generated);
            
            var settings = new Antmicro.Migrant.Customization.Settings(serializerMode, serializerMode,
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

        private void EnableProfilerInMachine(IMachine machine)
        {
            var profilerPath = new SequencedFilePath($"{profilerPathPrefix}-{CurrentEmulation[machine]}");
            machine.EnableProfiler(profilerPath);
        }

        private int stopwatchCounter;
        private Stopwatch stopwatch;
        private readonly Serializer serializer;
        private Emulation currentEmulation;
        private readonly object currentEmulationLock;
        private string profilerPathPrefix;

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

