//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Migrant;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Core
{
    public class PreservableManager
    {
        public PreservableManager()
        {
            registeredPreservables = new Dictionary<IPreservable, int?>();
        }

        public void RegisterEvents()
        {
            if(EmulationManager.Instance == null)
            {
                throw new InvalidOperationException("Emulation Manager instance is required to register events");
            }

            EmulationManager.Instance.EmulationChanged -= ClearDisposableRegisteredPreservables;
            EmulationManager.Instance.EmulationChanged += ClearDisposableRegisteredPreservables;
        }

        public void RegisterPreservable(IPreservable preservable, bool livesThroughEmulationChange)
        {
            // We check against GetPreservableObjects() because not every IExternal is registered in ExternalsManager
            if(preservable is IEmulationElement && GetPreservableObjects().Contains(preservable))
            {
                throw new ArgumentException("Manual registration of Emulation elements as preservable is forbidden");
            }

            if(registeredPreservables.ContainsKey(preservable))
            {
                throw new InvalidOperationException("Double registration of a Preservable is forbidden");
            }

            registeredPreservables.Add(preservable, livesThroughEmulationChange ? null : (int?)EmulationManager.EmulationEpoch);
        }

        public void UnregisterPreservable(IPreservable preservable)
        {
            registeredPreservables.Remove(preservable);
        }

        public Dictionary<string, object> ExtractPreservableStates()
        {
            var preservedStates = new Dictionary<string, object>();
            var preservables = GetPreservableObjects();

            foreach(var element in preservables.OfType<IHasPreservableState>())
            {
                Logger.LogAs(this, LogLevel.Debug, "Obtaining preservable state for {0}", element.PreservableName);
                preservedStates[element.PreservableName] = element.ExtractPreservedState();
            }

            foreach(var element in preservables.OfType<IDisconnectableState>())
            {
                element.DisconnectState();
            }

            ClearDisposableRegisteredPreservables();

            return preservedStates;
        }

        public bool LoadPreservedStates(Dictionary<string, object> preservedStates)
        {
            var preservablesWithNames = GetPreservableObjects().OfType<IHasPreservableState>().ToDictionary(x => x.PreservableName, x => x);
            var success = true;

            foreach(var kvp in preservablesWithNames)
            {
                var preservableName = kvp.Key;
                var preservableObject = kvp.Value;

                if(!preservedStates.TryGetValue(preservableName, out var preservedState))
                {
                    Logger.LogAs(this, LogLevel.Warning, "Preserved state not found for {0}", preservableName);
                    success = false;
                }

                preservableObject.LoadPreservedState(preservedState);
            }
            return success;
        }

        private void ClearDisposableRegisteredPreservables()
        {
            var preservablesToUnregister = registeredPreservables.Where(x => x.Value != null && x.Value != EmulationManager.EmulationEpoch).Select(x => x.Key).ToList();
            foreach(var preservable in preservablesToUnregister)
            {
                registeredPreservables.Remove(preservable);
            }
        }

        private IEnumerable<IPreservable> GetPreservableObjects()
        {
            return EmulationManager.Instance.CurrentEmulation.GetEmulationElementsOfType<IPreservable>().Concat(registeredPreservables.Keys);
        }

        [Transient]
        private readonly Dictionary<IPreservable, int?> registeredPreservables;
    }
}