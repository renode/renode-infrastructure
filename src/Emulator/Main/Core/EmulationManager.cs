//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Python;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;

using IronPython.Runtime;

namespace Antmicro.Renode.Core
{
    public sealed class EmulationManager
    {
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

        public static ITimeDomain ExternalWorld { get; private set; }

        public static EmulationManager Instance { get; private set; }

        // Incremented every time new Emulation is created on current Renode instance
        public static int EmulationEpoch { get; private set; }

        public static bool DisableEmulationFilesCleanup = false;

        public TimerResult StopTimer(string eventName = null)
        {
            stopwatchCounter++;
            var timerResult = new TimerResult
            {
                FromBeginning = stopwatch.Elapsed,
                SequenceNumber = stopwatchCounter,
                Timestamp = CustomDateTime.Now,
                EventName = eventName
            };
            stopwatch.Stop();
            stopwatch.Reset();
            return timerResult;
        }

        public TimerResult CurrentTimer(string eventName = null)
        {
            stopwatchCounter++;
            return new TimerResult
            {
                FromBeginning = stopwatch.Elapsed,
                SequenceNumber = stopwatchCounter,
                Timestamp = CustomDateTime.Now,
                EventName = eventName
            };
        }

        public TimerResult StartTimer(string eventName = null)
        {
            stopwatch.Reset();
            stopwatchCounter = 0;
            var timerResult = new TimerResult
            {
                FromBeginning = TimeSpan.FromTicks(0),
                SequenceNumber = stopwatchCounter,
                Timestamp = CustomDateTime.Now,
                EventName = eventName
            };
            stopwatch.Start();
            return timerResult;
        }

        public void Clear()
        {
            CurrentEmulation = GetNewEmulation();
        }

        public void EnableCompiledFilesCache(bool value)
        {
            CompiledFilesCache.Enabled = value;
        }

        public void Load(ReadFilePath path)
        {
            using(var fstream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using(var stream = path.ToString().EndsWith(".gz", StringComparison.InvariantCulture)
                                ? (Stream)new GZipStream(fstream, CompressionMode.Decompress)
                                : (Stream)fstream)
            {
                EmulationEpoch++;
                var deserializationResult = serializer.TryDeserialize<Emulation>(stream, out var emulation, out var metadata);
                string metadataStringFromFile = null;

                try
                {
                    if(metadata != null)
                    {
                        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                        metadataStringFromFile = utf8.GetString(metadata);
                    }
                }
                catch(Exception) // Metadata is not a valid UTF-8 byte sequence
                {
                    throw CreateLoadException(DeserializationResult.MetadataCorrupted, metadataStringFromFile);
                }

                if(deserializationResult != DeserializationResult.OK)
                {
                    throw CreateLoadException(deserializationResult, metadataStringFromFile);
                }

                CurrentEmulation = emulation;
                CurrentEmulation.BlobManager.Load(stream, fstream.Name);

                if(metadataStringFromFile != MetadataString)
                {
                    Logger.Log(LogLevel.Warning, "Version of deserialized emulation ({0}) does not match current one ({1}). Things may go awry!", metadataStringFromFile, MetadataString);
                }
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
                            serializer.Serialize(CurrentEmulation, stream, Encoding.UTF8.GetBytes(MetadataString));
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

        public ProgressMonitor ProgressMonitor { get; private set; }

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
                    var version = entryAssembly.GetName().Version;

                    // version.Revision is intentionally skipped here
                    return string.Format("{0}, version {1}.{2}.{3} ({4})",
                        entryAssembly.GetName().Name,
                        version.Major,
                        version.Minor,
                        version.Build,
                        ((AssemblyInformationalVersionAttribute)entryAssembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion
                    );
                }
                catch(System.Exception)
                {
                    return string.Empty;
                }
            }
        }

        public string MetadataString => $"{VersionString} running on {RuntimeInfo.OSIdentifier}-{RuntimeInfo.ArchitectureIdentifier} {RuntimeInfo.Version}";

        public SimpleFileCache CompiledFilesCache { get; } = new SimpleFileCache("compiler-cache", !Emulator.InCIMode && ConfigurationManager.Instance.Get("general", "compiler-cache-enabled", false));

        public event Action EmulationChanged;

        private static Emulation GetNewEmulation()
        {
            EmulationEpoch++;
            return new Emulation();
        }

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
            currentEmulation = GetNewEmulation();
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

        private RecoverableException CreateLoadException(DeserializationResult result, string metadata)
        {
            var errorMessage = result == DeserializationResult.MetadataCorrupted
                ? $"The snapshot cannot be loaded as its metadata is corrupted."
                : $"This snapshot is incompatible or the emulation's state is corrupted. Snapshot version: {metadata}. Your version: {MetadataString}";

            return new RecoverableException(errorMessage);
        }

        private Emulation currentEmulation;
        private string profilerPathPrefix;

        private int stopwatchCounter;
        private readonly Stopwatch stopwatch;
        private readonly Serializer serializer;
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