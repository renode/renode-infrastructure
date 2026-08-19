//
// Copyright (c) 2010-2026 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

using Nini.Config;

namespace Antmicro.Renode.Utilities
{
    public sealed class ConfigurationManager
    {
        static ConfigurationManager()
        {
            Initialize(Path.Combine(Emulator.UserDirectoryPath, "config"));
        }

        public static void Initialize(string configFile)
        {
            Instance = new ConfigurationManager(configFile);
        }

        public static ConfigurationManager Instance { get; private set; }

        public T Get<T>(string group, string name, T defaultValue, Func<T, bool> validation = null)
        {
            T result;
            if(!TryFindInCache(group, name, out result))
            {
                if(!Config.TryGet(group, out var config) || !config.Contains(name))
                {
                    if(defaultValue == null)
                    {
                        throw new ArgumentException("Default value cannot be null", "defaultValue");
                    }
                    Set(group, name, defaultValue);
                    return defaultValue;
                }

                try
                {
                    if(typeof(T) == typeof(int))
                    {
                        result = (T)(object)config.GetInt(name);
                    }
                    else if(typeof(T) == typeof(string))
                    {
                        result = (T)(object)config.GetString(name);
                    }
                    else if(typeof(T) == typeof(bool))
                    {
                        result = (T)(object)config.GetBoolean(name);
                    }
                    else if(typeof(T).IsEnum)
                    {
                        var value = Get<string>(group, name, defaultValue.ToString());
                        if(!Enum.IsDefined(typeof(T), value))
                        {
                            throw new ConfigurationException(String.Format("Could not apply value '{0}' for type {1}. Verify your configuration file {5} in section {2}->{3}. Available options are: {4}.",
                                        value, typeof(T).Name, group, name, Enum.GetNames(typeof(T)).Aggregate((x, y) => x + ", " + y), Config.FileName));
                        }
                        result = (T)Enum.Parse(typeof(T), value);
                    }
                    else
                    {
                        throw new ConfigurationException("Unsupported type: " + typeof(T));
                    }
                    AddToCache(group, name, result);
                }
                catch(FormatException)
                {
                    throw new ConfigurationException(String.Format("Field {0}->{1} is not of type {2}.", group, name, typeof(T).Name));
                }
            }
            if(validation != null && !validation(result))
            {
                throw new ConfigurationException(String.Format("Value '{0}' is not valid for entry in section {1}->{2}.", result.ToString(), group, name));
            }
            return result;
        }

        public bool TryGet<T>(string group, string name, out T result)
        {
            var config = Config.Get(group);
            if(config == null || !config.Contains(name))
            {
                result = default(T);
                return false;
            }

            // value for this variable already exists so default value will not be used
            result = Get<T>(group, name, default(T));
            return true;
        }

        public void SetNonPersistent<T>(string group, string name, T value)
        {
            AddToCache(group, name, value);
        }

        public void Set<T>(string group, string name, T value)
        {
            if(!Config.TryGet(group, out var config))
            {
                config = Config.Add(group);
            }

            AddToCache(group, name, value);
            // Note that `Config.Source` takes a LockSource when the file does not exist.
            // The sequence below is safe, because `TryGetGroup` ensures the file is created.
            using(var locker = Config.LockSource())
            {
                config.Set(name, value);
                Config.Save();
            }
        }

        public string FilePath => Config.FileName;

        private ConfigurationManager(string configFile)
        {
            Config = new ConfigSource(configFile);
        }

        private void AddToCache<T>(string group, string name, T value)
        {
            cachedValues[Tuple.Create(group, name)] = value;
        }

        private bool TryFindInCache<T>(string group, string name, out T value)
        {
            value = default(T);
            object obj;
            var result = cachedValues.TryGetValue(Tuple.Create(group, name), out obj);
            if(result)
            {
                value = (T)obj;
            }
            return result;
        }

        private readonly Dictionary<Tuple<string, string>, object> cachedValues = new Dictionary<Tuple<string, string>, object>();

        private readonly ConfigSource Config;
    }

    public class ConfigSource
    {
        public ConfigSource(string filePath)
        {
            FileName = filePath;
        }

        public void Save()
        {
            if(Emulator.InCIMode)
            {
                return;
            }
            niniSource.Save(FileName);
        }

        public IConfig Add(string group) => Source.AddConfig(group);

        public IConfig Get(string group) => Source.Configs[group];

        public bool TryGet(string group, out IConfig config)
        {
            config = Get(group);
            return config != null;
        }

        public IDisposable LockSource() => Emulator.InCIMode ? new DisposableWrapper() : new FileLocker(FileName + ConfigurationLockSuffix);

        public string FileName { get; private set; }

        private IConfigSource Source
        {
            get
            {
                if(niniSource == null)
                {
                    using(var locker = LockSource())
                    {
                        if(File.Exists(FileName))
                        {
                            try
                            {
                                niniSource = new IniConfigSource(FileName);
                            }
                            catch(Exception)
                            {
                                Logger.Log(LogLevel.Warning, "Configuration file {0} exists, but it cannot be read.", FileName);
                            }
                        }
                        else
                        {
                            niniSource = new IniConfigSource();
                            Save();
                        }
                    }
                }
                return niniSource;
            }
        }

        private IniConfigSource niniSource;

        private const string ConfigurationLockSuffix = ".lock";
    }
}
