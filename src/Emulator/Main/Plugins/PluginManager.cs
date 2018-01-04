//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.UserInterface;
using Mono.Cecil;

namespace Antmicro.Renode.Plugins
{
    public sealed class PluginManager : IDisposable
    {
        public void DisableAllPlugins()
        {
            Dispose();
            SaveConfiguration();
        }

        public void DisablePlugin(string name)
        {
            DisablePlugin(FindPluginFromName(name));
        }

        public void DisablePlugin(PluginDescriptor plugin)
        {
            DisablePlugin(plugin, new HashSet<PluginDescriptor>(), false);
        }

        public void EnablePlugin(string name)
        {
            EnablePlugin(FindPluginFromName(name));
        }

        public void EnablePlugin(PluginDescriptor plugin)
        {
            if(plugin.Modes.Any() && !(enabledModes.Any(x => plugin.Modes.Contains(x))))
            {
                throw new RecoverableException(string.Format("Plugin {0} is not suitable for any of available modes: {1}.", plugin.FullName, string.Join(", ", enabledModes)));
            }
            EnablePlugin(plugin, new HashSet<PluginDescriptor>(), false);
        }

        public string[,] GetPlugins()
        {
            var table = new Table().AddRow("Name", "Description", "Vendor", "Version", "Mode", "State");
            table.AddRows(TypeManager.Instance.AvailablePlugins.Where(x => !x.IsHidden),
                x => x.Name,
                x => x.Description,
                x => x.Vendor,
                x => x.Version.ToString(),
                x => string.Join(", ", x.Modes),
                x => activePlugins.ContainsKey(x) ? "enabled" : "disabled"
            );
            return table.ToArray();
        }

        public void Dispose()
        {
            foreach(var plugin in activePlugins.Values.OfType<IDisposable>())
            {
                plugin.Dispose();
            }

            activePlugins.Clear();
        }

        [HideInMonitor]
        public IDictionary<PluginDescriptor, bool> GetPluginsMap()
        {
            return TypeManager.Instance.AvailablePlugins.ToDictionary(key => key, value => activePlugins.ContainsKey(value));
        }

        [HideInMonitor]
        public void Init(params string[] modes)
        {
            enabledModes = new HashSet<string>(modes);
            var enabledPlugins = ConfigurationManager.Instance.Get(ConfigSection, ConfigOption, string.Empty);
            foreach(var pluginName in enabledPlugins.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    EnablePlugin(pluginName.Trim());
                }
                catch(RecoverableException e)
                {
                    Logger.LogAs(this, LogLevel.Warning, "Could not load plugin. {0}", e.Message);
                }
            }
        }

        private void DisablePlugin(PluginDescriptor plugin, HashSet<PluginDescriptor> disabledPlugins, bool disableHidden)
        {
            if(plugin.IsHidden && !disableHidden)
            {
                throw new RecoverableException(string.Format("This plugin cannot be disabled directly: {0}.", plugin.FullName));
            }

            var availablePlugins = TypeManager.Instance.AvailablePlugins;

            if(!availablePlugins.Contains(plugin))
            {
                throw new RecoverableException(string.Format("There is no plugin named {0}.", plugin.FullName));
            }
            if(activePlugins.ContainsKey(plugin))
            {
                disabledPlugins.Add(plugin);
                foreach(var dependantPlugin in availablePlugins.Where(x => x != plugin && x.Dependencies != null && x.Dependencies.Contains(plugin.ThisType, typeComparer)))
                {
                    if(!disabledPlugins.Contains(dependantPlugin))
                    {
                        DisablePlugin(dependantPlugin, disabledPlugins, true);
                    }
                }
                var disposable = activePlugins[plugin] as IDisposable;
                if(disposable != null)
                {
                    disposable.Dispose();
                }
                activePlugins.Remove(plugin);
                disabledPlugins.Remove(plugin);
                SaveConfiguration();
            }
        }

        private void EnablePlugin(PluginDescriptor plugin, HashSet<PluginDescriptor> enabledPlugins, bool enableHidden)
        {
            if(plugin.IsHidden && !enableHidden)
            {
                throw new RecoverableException(string.Format("This plugin cannot be enabled directly: {0}.", plugin.FullName));
            }

            var availablePlugins = TypeManager.Instance.AvailablePlugins;
            if(!availablePlugins.Contains(plugin))
            {
                throw new RecoverableException(string.Format("There is no plugin named {0}.", plugin.FullName));
            }
            if(!activePlugins.ContainsKey(plugin))
            {
                enabledPlugins.Add(plugin);
                if(plugin.Dependencies != null)
                {
                    foreach(var referencedPlugin in plugin.Dependencies)
                    {
                        var referencedPluginDescriptor = availablePlugins.SingleOrDefault(x => typeComparer.Equals(x.ThisType, referencedPlugin));
                        if(referencedPluginDescriptor == null)
                        {
                            throw new RecoverableException("Plugin {0} not found.".FormatWith(referencedPlugin.GetFullNameOfMember()));
                        }
                        if(enabledPlugins.Contains(referencedPluginDescriptor))
                        {
                            throw new RecoverableException("Circular plugin dependency between {0} and {1}.".FormatWith(plugin.FullName, referencedPluginDescriptor.FullName));
                        }
                        EnablePlugin(referencedPluginDescriptor, enabledPlugins, true);
                    }
                }
                activePlugins[plugin] = plugin.CreatePlugin();
                enabledPlugins.Remove(plugin);
                SaveConfiguration();
            }
        }

        private PluginDescriptor FindPluginFromName(string name)
        {
            var pluginNameComponents = name.Split(SplitSeparator);
            var availablePlugins = TypeManager.Instance.AvailablePlugins;
            IEnumerable<PluginDescriptor> plugins = null;
            switch(pluginNameComponents.Length)
            {
            case 3:
                plugins = availablePlugins.Where(x => x.FullName == name);
                break;
            case 2:
                plugins = availablePlugins.Where(x => x.Name == pluginNameComponents[0] && x.Version.ToString() == pluginNameComponents[1]);
                break;
            case 1:
                plugins = availablePlugins.Where(x => x.Name == pluginNameComponents[0]);
                break;
            default:
                throw new RecoverableException("Malformed plugin name \"{0}\"".FormatWith(name));
            }
            if(plugins.Count() == 0)
            {
                throw new RecoverableException(string.Format("There is no plugin named {0}.", name));
            }
            if(plugins.Count() > 1)
            {
                throw new RecoverableException(string.Format("Ambiguous choice for \"{0}\". The possible choices are: {1}."
                    .FormatWith(name, plugins.Select(x => "\"{0}\"".FormatWith(x.FullName)).Stringify(", "))));
            }
            return plugins.First();
        }

        private void SaveConfiguration()
        {
            ConfigurationManager.Instance.Set(ConfigSection, ConfigOption, activePlugins.Any() ? activePlugins.Select(x => x.Key.FullName).Aggregate((curr, next) => curr + "," + next) : string.Empty);
        }

        private readonly TypeDefinitionComparer typeComparer = new TypeDefinitionComparer();
        private readonly Dictionary<PluginDescriptor, object> activePlugins = new Dictionary<PluginDescriptor, object>();

        private HashSet<string> enabledModes;

        private const string ConfigOption = "enabled-plugins";
        private const string ConfigSection = "plugins";
        private const char SplitSeparator = ':';

        private class TypeDefinitionComparer : IEqualityComparer<TypeDefinition>
        {
            public bool Equals(TypeDefinition t1, TypeDefinition t2)
            {
                if(t1.HasGenericParameters || t2.HasGenericParameters)
                {
                    throw new ArgumentException("Generic parameters in plugin of type {0} not supported.".FormatWith(t1.HasGenericParameters ? t1.Name : t2.Name));
                }
                return t1.Module.Mvid == t2.Module.Mvid && t1.MetadataToken == t2.MetadataToken;
            }

            public int GetHashCode(TypeDefinition obj)
            {
                var hash = obj.Module.Mvid.GetHashCode();
                hash = (hash * 397) ^ obj.MetadataToken.GetHashCode();
                hash = (hash * 397) ^ obj.HasGenericParameters.GetHashCode();
                return hash;
            }
        }
    }
}

