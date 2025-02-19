//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using System.Linq;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities.Collections;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals
{
    public class BackendManager : IDisposable
    {
        public BackendManager()
        {
            map = new SerializableWeakKeyDictionary<IAnalyzable, IAnalyzableBackend>();
            Init();
        }

        public void Dispose()
        {
            foreach(var analyzer in activeAnalyzers.OfType<IDisposable>())
            {
                analyzer.Dispose();
            }
            TypeManager.Instance.AutoLoadedType -= HandleAutoLoadTypeFound;
        }

        public IEnumerable<string> GetAvailableAnalyzersFor(IAnalyzableBackend backend)
        {
            if (!analyzers.ContainsKey(backend.GetType()))
            {
                return new string[0];
            }

            return analyzers[backend.GetType()].Where(x => x.Item2).Select(x => x.Item1.FullName);
        }

        public void SetPreferredAnalyzer(Type backendType, Type analyzerType)
        {
            preferredAnalyzer[backendType] = analyzerType;
        }

        public string GetPreferredAnalyzerFor(IAnalyzableBackend backend)
        {
            return preferredAnalyzer.ContainsKey(backend.GetType()) ? ((IAnalyzableBackendAnalyzer)Activator.CreateInstance(preferredAnalyzer[backend.GetType()])).GetType().FullName : null;
        }

        public bool TryCreateBackend<T>(T analyzable) where T : IAnalyzable
        {
            Type backendType = null;
            foreach(var b in backends)
            {
                if(b.Key.IsAssignableFrom(analyzable.GetType()))
                {
                    backendType = b.Value;
                    break;
                }
            }

            if(backendType != null)
            {
                dynamic backend = (IAnalyzableBackend) Activator.CreateInstance(backendType);
                backend.Attach((dynamic)analyzable);
                map[analyzable] = backend;

                return true;
            }
            return false;
        }

        public bool TryGetBackendFor(IAnalyzable peripheral, out IAnalyzableBackend backend)
        {
            return map.TryGetValue(peripheral, out backend);
        }

        public bool TryGetBackendFor<T>(T element, out IAnalyzableBackend<T> backend) where T : IAnalyzable
        {
            IAnalyzableBackend outValue = null;
            var result = map.TryGetValue(element, out outValue);
            backend = (IAnalyzableBackend<T>)outValue;
            return result;
        }

        public bool TryCreateAnalyzerForBackend<T>(T backend, out IAnalyzableBackendAnalyzer analyzer) where T : IAnalyzableBackend
        {
            Type analyzerType;
            var backendType = backend.GetType();
            if(preferredAnalyzer.ContainsKey(backendType))
            {
                analyzerType = preferredAnalyzer[backendType];
            }
            else
            {
                List<Tuple<Type, bool>> foundAnalyzers;
                if(!analyzers.TryGetValue(backendType, out foundAnalyzers) || foundAnalyzers.Count(x => x.Item2) > 1)
                {
                    analyzer = null;
                    return false;
                }

                analyzerType = foundAnalyzers.First(x => x.Item2).Item1;
            }

            analyzer = CreateAndAttach(analyzerType, backend);
            activeAnalyzers.Add(analyzer);
            PeripheralBackendAnalyzerCreated?.Invoke(analyzer);
            return true;
        }

        public bool TryCreateAnalyzerForBackend<T>(T backend, string analyzerTypeName, out IAnalyzableBackendAnalyzer analyzer) where T : IAnalyzableBackend
        {
            if (!analyzers.ContainsKey(backend.GetType()))
            {
                analyzer = null;
                return false;
            }

            var foundAnalyzers = analyzers[backend.GetType()];
            var analyzerType = foundAnalyzers.FirstOrDefault(x => x.Item1.FullName == analyzerTypeName).Item1;
            if(analyzerType != null)
            {
                analyzer = CreateAndAttach(analyzerType, backend);
                activeAnalyzers.Add(analyzer);
                PeripheralBackendAnalyzerCreated?.Invoke(analyzer);
                return true;
            }

            analyzer = null;
            return false;
        }

        public void HideAnalyzersFor(IPeripheral peripheral)
        {
            var toRemove = new List<IAnalyzableBackendAnalyzer>();
            foreach(var analyzer in activeAnalyzers.Where(x => x.Backend.AnalyzableElement == peripheral))
            {
                analyzer.Hide();
                toRemove.Add(analyzer);
            }

            foreach(var rem in toRemove)
            {
                activeAnalyzers.Remove(rem);
            }
        }

        public void HideAnalyzersFor(IMachine machine)
        {
            string name;
            var toRemove = new List<IAnalyzableBackendAnalyzer>();
            foreach(var analyzer in activeAnalyzers.Where(x => machine.TryGetLocalName(x.Backend.AnalyzableElement as IPeripheral, out name)))
            {
                analyzer.Hide();
                toRemove.Add(analyzer);
            }

            foreach(var rem in toRemove)
            {
                activeAnalyzers.Remove(rem);
            }
        }

        public event Action<IAnalyzableBackendAnalyzer> PeripheralBackendAnalyzerCreated;

        private IAnalyzableBackendAnalyzer CreateAndAttach(Type analyzerType, object backend)
        {
            dynamic danalyzer = Activator.CreateInstance(analyzerType);
            danalyzer.AttachTo((dynamic)backend);
            return (IAnalyzableBackendAnalyzer) danalyzer;
        }

        private bool TryCreateAndAttach(Type analyzerType, object backend, Func<IAnalyzableBackendAnalyzer, bool> condition, out IAnalyzableBackendAnalyzer analyzer)
        {
            dynamic danalyzer = Activator.CreateInstance(analyzerType);
            if(condition(danalyzer))
            {
                danalyzer.AttachTo((dynamic)backend);
                analyzer = (IAnalyzableBackendAnalyzer)danalyzer;
                return true;
            }

            analyzer = null;
            return false;
        }

        private void HandleAutoLoadTypeFound(Type t)
        {
            var interestingInterfaces = t.GetInterfaces().Where(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(IAnalyzableBackendAnalyzer<>) ||
                    i.GetGenericTypeDefinition() == typeof(IAnalyzableBackend<>)));

            if(!interestingInterfaces.Any())
            {
                return;
            }

            var hidden = t.GetCustomAttributes(typeof(HideInMonitorAttribute), true).Any();
            var analyzerTypes = interestingInterfaces.Where(i => i.GetGenericTypeDefinition() == typeof(IAnalyzableBackendAnalyzer<>)).SelectMany(i => i.GetGenericArguments()).ToArray();
            foreach(var arg in analyzerTypes)
            {
                if(!analyzers.ContainsKey(arg))
                {
                    analyzers.Add(arg, new List<Tuple<Type, bool>>());
                }

                analyzers[arg].Add(Tuple.Create(t, !hidden));
            }

            var backendTypes = interestingInterfaces.Where(i => i.GetGenericTypeDefinition() == typeof(IAnalyzableBackend<>)).SelectMany(i => i.GetGenericArguments()).ToArray();
            foreach(var arg in backendTypes)
            {
                if(backends.ContainsKey(arg))
                {
                    throw new InvalidProgramException(string.Format("There can be only one backend class for a peripheral type, but found at least two: {0}, {1}", backends[arg].AssemblyQualifiedName, t.AssemblyQualifiedName));
                }
                backends[arg] = t;
            }
        }

        [PreSerialization]
        private void SavePreferredAnalyzers()
        {
            preferredAnalyzersString = new Dictionary<string, string>();
            foreach(var pa in preferredAnalyzer)
            {
                preferredAnalyzersString.Add(pa.Key.AssemblyQualifiedName, pa.Value.AssemblyQualifiedName);
            }
        }

        private void RestorePreferredAnalyzers()
        {
            if(preferredAnalyzersString == null)
            {
                return;
            }

            foreach(var pas in preferredAnalyzersString)
            {
                try
                {
                    preferredAnalyzer.Add(Type.GetType(pas.Key), Type.GetType(pas.Value));
                }
                catch (Exception)
                {
                    Logger.LogAs(this, LogLevel.Warning, "Could not restore preferred analyzer for {0}: {1}. Error while loading types", pas.Key, pas.Value);
                }
            }

            preferredAnalyzersString = null;
        }

        [PostDeserialization]
        private void Init()
        {
            analyzers = new Dictionary<Type, List<Tuple<Type, bool>>>();
            backends = new Dictionary<Type, Type>();
            preferredAnalyzer = new Dictionary<Type, Type>();
            activeAnalyzers = new List<IAnalyzableBackendAnalyzer>();

            RestorePreferredAnalyzers();
            TypeManager.Instance.AutoLoadedType += HandleAutoLoadTypeFound;
        }

        private SerializableWeakKeyDictionary<IAnalyzable, IAnalyzableBackend> map;
        [Transient]
        private Dictionary<Type, List<Tuple<Type, bool>>> analyzers;
        [Transient]
        private Dictionary<Type, Type> backends;
        [Transient]
        private Dictionary<Type, Type> preferredAnalyzer;

        private Dictionary<string, string> preferredAnalyzersString;

        [Transient]
        private List<IAnalyzableBackendAnalyzer> activeAnalyzers;
    }
}

