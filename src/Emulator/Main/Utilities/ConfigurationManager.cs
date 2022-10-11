//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Nini.Config;
using System.IO;
using System;
using Antmicro.Renode.Logging;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public sealed class ConfigurationManager
    {
        static ConfigurationManager()
        {
            Initialize(Path.Combine(Emulator.UserDirectoryPath, "config"));
        }

        public static ConfigurationManager Instance { get; private set; }

        public static void Initialize(string configFile)
        {
            Instance = new ConfigurationManager(configFile);
        }

        private ConfigurationManager(string configFile)
        {
            Config = new ConfigSource(configFile);
        }

        public T Get<T>(string group, string name, T defaultValue, Func<T, bool> validation = null)
        {
            T result;
            if(!TryFindInCache(group, name, out result))
            {
                var config = VerifyValue(group, name, defaultValue);
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

        public void SetNonPersistent<T>(string group, string name, T value)
        {
            AddToCache(group, name, value);
        }

        public void Set<T>(string group, string name, T value)
        {
            var config = VerifyValue(group, name, value);
            AddToCache(group, name, value);
            config.Set(name, value);
        }

        public string FilePath => Config.FileName;

        private IConfig VerifyValue(string group, string name, object defaultValue)
        {
            if(defaultValue == null)
            {
                throw new ArgumentException("Default value cannot be null", "defaultValue");
            }
            var config = VerifyGroup(group);
            if(!config.Contains(name))
            {
                config.Set(name, defaultValue);
            }
            return config;
        }

        private IConfig VerifyGroup(string group)
        {
            var config = Config.Source.Configs[group];
            return config ?? Config.Source.AddConfig(group);
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

        public IConfigSource Source
        {
            get
            {
                if(source == null)
                {
                    using(var locker = new FileLocker(FileName + ConfigurationLockSuffix))
                    {
                        if(File.Exists(FileName))
                        {
                            try
                            {
                                source = new IniConfigSource(FileName);
                            }
                            catch(Exception)
                            {
                                Logger.Log(LogLevel.Warning, "Configuration file {0} exists, but it cannot be read.", FileName);
                            }
                        }
                        else
                        {
                            source = new IniConfigSource();
                            source.Save(FileName);
                        }
                    }
                }
                source.AutoSave = !Emulator.InCIMode;
                return source;
            }

        }

        public string FileName { get; private set; }

        private IniConfigSource source;

        private const string ConfigurationLockSuffix = ".lock";
    }
}

