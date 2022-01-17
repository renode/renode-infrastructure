//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Utilities;
using System.Linq;
using AntShell.Terminal;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.UI
{
    public class ConsoleWindowBackendAnalyzer : IAnalyzableBackendAnalyzer<UARTBackend>, IDisposable
    {
        // Default constructor is required for the showAnalyzer command.
        public ConsoleWindowBackendAnalyzer() : this(false)
        {
        }

        public ConsoleWindowBackendAnalyzer(bool isMonitorWindow)
        {
            IO = new IOProvider();
            this.isMonitorWindow = isMonitorWindow;
        }

        public void Dispose()
        {
            if(provider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
        }

        public void AttachTo(UARTBackend backend)
        {
            Backend = backend;
            if(EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementName(backend.UART, out var uartName))
            {
                Name = uartName;
            }
        }

        public void Show()
        {
            var availableProviders = TypeManager.Instance.AutoLoadedTypes.Where(x => !x.IsAbstract && typeof(IConsoleBackendAnalyzerProvider).IsAssignableFrom(x)).ToDictionary(x => GetProviderName(x), x => x);
            var preferredProvider = ConfigurationManager.Instance.Get("general", "terminal", "XTerm");

            foreach(var providerName in availableProviders.Keys.OrderByDescending(x => x == preferredProvider))
            {
                var providerType = availableProviders[providerName];
                if(providerType.GetConstructor(new Type[0]) == null)
                {
                    Logger.Log(LogLevel.Warning, "There is no default public constructor for {0} console backend analyzer provider. Skipping it.", providerName);
                    continue;
                }
                provider = (IConsoleBackendAnalyzerProvider)Activator.CreateInstance(availableProviders[providerName]);
                provider.OnClose += OnClose;
                if(!provider.TryOpen(Name, out IIOSource ioSource, isMonitorWindow))
                {
                    Logger.Log(LogLevel.Warning, "Could not open {0} console backend analyzer provider. Trying the next one.", providerName);
                    continue;
                }
                IO.Backend = ioSource;
                if(Backend != null)
                {
                    ((UARTBackend)Backend).BindAnalyzer(IO);
                }
                return;
            }

            throw new InvalidOperationException($"Could not start any console backend analyzer. Tried: {(string.Join(", ", availableProviders.Keys))}.");
        }

        public void Hide()
        {
            var p = provider;
            if(p == null)
            {
                return;
            }

            if(Backend != null)
            {
                ((UARTBackend)Backend).UnbindAnalyzer(IO);
                Backend = null;
            }
            p.Close();
            provider = null;
        }

        public string Name { get; private set; }

        public IAnalyzableBackend Backend { get; private set; }

        public IOProvider IO { get; private set; }

        public event Action Quitted;

        private string GetProviderName(Type type)
        {
            var attribute = type.GetCustomAttributes(false).OfType<ConsoleBackendAnalyzerProviderAttribute>().SingleOrDefault();
            if(attribute != null)
            {
                return attribute.Name;
            }

            return type.Name.EndsWith("Provider", StringComparison.Ordinal) ? type.Name.Substring(0, type.Name.Length - 8) : type.Name;
        }

        private void OnClose()
        {
            Quitted?.Invoke();
        }

        private readonly bool isMonitorWindow;
        private IConsoleBackendAnalyzerProvider provider;
    }
}
