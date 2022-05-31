//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core.Structure;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core
{
    public sealed class ExternalsManager : IHasChildren<IExternal>
    {
        public ExternalsManager()
        {
            externals = new Dictionary<string, IExternal>();
            registeredIHasOwnLifeObjects = new List<SerializableWeakReference<IHasOwnLife>>();
            paused = true;
        }

        public void AddExternal(IExternal external, string name)
        {
            lock(externals)
            {
                if(externals.ContainsValue(external))
                {
                    throw new RecoverableException("External already registered");
                }
                if(externals.ContainsKey(name))
                {
                    throw new RecoverableException("External's name already in use");
                }
                externals.Add(name, external);
            }

            OnExternalsChanged(external, true);
        }

        public IEnumerable<T> GetExternalsOfType<T>() where T : IExternal
        {
            return externals.Values.OfType<T>();
        }

        public void RemoveExternal(IExternal external)
        {
            lock(externals)
            {
                if(!externals.ContainsValue(external))
                {
                    throw new RecoverableException("External not registered");
                }

                externals.Remove(externals.Single(x => x.Value == external).Key);
            }

            OnExternalsChanged(external, false);
        }

        public void Clear()
        {
            IDisposable[] toDispose;
            lock(externals)
            {
                toDispose = externals.Select(x => x.Value as IDisposable).Where(x => x != null).ToArray();
                externals.Clear();
            }
            foreach(var td in toDispose)
            {
                td.Dispose();
            }
        }

        public bool TryGetByName<T>(string name, out T result) where T : class
        {
            return TryGetByNameInner(this, name, out result);
        }

        public bool TryGetName(IExternal external, out string name)
        {
            lock(externals)
            {
                var result = externals.SingleOrDefault(x => x.Value == external);
                if(result.Value != null)
                {
                    name = result.Key;
                    return true;
                }

                name = null;
                return false;
            }
        }

        public IEnumerable<string> GetNames()
        {
            lock(externals)
            {
                var result = new List<string>();
                var keys = externals.Keys.ToArray();
                GetNamesInner(result, string.Empty, keys.Select(x => externals[x]).ToArray(), externals.Keys);
                return result;
            }
        }

        public IEnumerable<IExternal> Externals
        {
            get
            {
                return externals.Values;
            }
        }

        public void Start()
        {
            lock(externals)
            {
                if(!paused)
                {
                    return;
                }
                paused = false;
                var ownLifeExternals = externals.Select(x => x.Value as IHasOwnLife).Where(x => x != null);
                if(alreadyStarted)
                {
                    foreach(var external in ownLifeExternals)
                    {
                        external.Resume();
                    }
                    foreach(var iHasOwnLife in registeredIHasOwnLifeObjects)
                    {
                        var target = iHasOwnLife.Target;
                        if(target != null)
                        {
                            target.Resume();
                        }
                    }
                    return;
                }

                foreach(var external in ownLifeExternals)
                {
                    external.Start();
                }

                foreach(var iHasOwnLife in registeredIHasOwnLifeObjects)
                {
                    var target = iHasOwnLife.Target;
                    if(target != null)
                    {
                        target.Start();
                    }
                }

                alreadyStarted = true;
            }
        }

        public void Pause()
        {
            lock(externals)
            {
                if(paused)
                {
                    return;
                }
                paused = true;
                var ownLifeExternals = externals.Select(x => x.Value as IHasOwnLife).Where(x => x != null);
                foreach(var external in ownLifeExternals)
                {
                    external.Pause();
                }
                foreach(var iHasOwnLife in registeredIHasOwnLifeObjects)
                {
                    var target = iHasOwnLife.Target;
                    if(target != null)
                    {
                        target.Pause();
                    }
                }
            }
        }

        IExternal IHasChildren<IExternal>.TryGetByName(string name, out bool success)
        {
            lock(externals)
            {
                if(externals.ContainsKey(name))
                {
                    success = true;
                    return externals[name];
                }
                success = false;
                return null;
            }
        }

        private void GetNamesInner(List<string> result, string prefix, IEnumerable<object> objects, IEnumerable<string> theirNames)
        {
            bool notUsed;
            var namesEnumerator = theirNames.GetEnumerator();
            namesEnumerator.MoveNext();
            foreach(var obj in objects)
            {
                var currentName = string.IsNullOrEmpty(prefix) ? namesEnumerator.Current : prefix + Machine.PathSeparator + namesEnumerator.Current;
                result.Add(currentName);
                var withChildren = obj as IHasChildren<object>;
                if(withChildren != null)
                {
                    var objectsAndNames = withChildren.GetNames().Select(x => new { Name = x, Value = withChildren.TryGetByName(x, out notUsed) })
                        .Where(x => x.Value != null).ToArray();
                    GetNamesInner(result, currentName, objectsAndNames.Select(x => x.Value).ToArray(), objectsAndNames.Select(x => x.Name).ToArray());
                }
                namesEnumerator.MoveNext();
            }
        }

        private bool TryGetByNameInner<T>(IHasChildren<object> currentParent, string subname, out T result) where T : class
        {
            if(subname == null)
            {
                result = null;
                return false;
            }
            var parts = subname.Split(new [] { Machine.PathSeparator } , 2);
            object candidate;
            if(!currentParent.TryGetByName(parts[0], out candidate))
            {
                result = null;
                return false;
            }
            result = candidate as T;
            if(parts.Length == 1)
            {
                return result != null;
            }
            var parent = candidate as IHasChildren<object>;
            if(parent != null)
            {
                return TryGetByNameInner(parent, parts[1], out result);
            }
            return false;
        }

        [field: Transient]
        public event Action<ExternalsChangedEventArgs> ExternalsChanged;

        private bool alreadyStarted;
        private bool paused;

        [Constructor]
        private readonly Dictionary<string, IExternal> externals;

        // those fields are used to serialize externals (and skip the transient ones)
        private List<string> externalsKeys;
        private List<IExternal> externalsValues;

        [PreSerialization]
        private void SerializeExternals()
        {
            externalsKeys = new List<string>();
            externalsValues = new List<IExternal>();

            foreach(var item in externals)
            {
                if(item.Value.GetType().GetCustomAttributes(typeof(TransientAttribute), true).Any())
                {
                    Logger.Log(LogLevel.Info, "Skipping serialization of the '{0}' external as it's marked as transient", item.Key);
                    continue;
                }
                
                externalsKeys.Add(item.Key);
                externalsValues.Add(item.Value);
            }
        }

        [PostDeserialization]
        private void RestoreExternals()
        {
            for(var i = 0; i < externalsKeys.Count; i++)
            {
                externals.Add(externalsKeys[i], externalsValues[i]);
            }

            externalsKeys = null;
            externalsValues = null;
        }

        public void RegisterIHasOwnLife(IHasOwnLife own)
        {
            lock(externals)
            {
                registeredIHasOwnLifeObjects.Add(new SerializableWeakReference<IHasOwnLife>(own));
                if(!paused)
                {
                    own.Start();
                }
            }
        }

        public void UnregisterIHasOwnLife(IHasOwnLife own)
        {
            lock(externals)
            {
                registeredIHasOwnLifeObjects.RemoveAll(x => x.Target == own);
                if(alreadyStarted)
                {
                    own.Pause();
                }
            }
        }

        private void OnExternalsChanged(IExternal external, bool added)
        {
            var ec = ExternalsChanged;
            if(ec != null)
            {
                ec(new ExternalsChangedEventArgs(external, added ? ExternalsChangedEventArgs.ChangeType.Added : ExternalsChangedEventArgs.ChangeType.Removed)); 
            }
        }

        private readonly List<SerializableWeakReference<IHasOwnLife>> registeredIHasOwnLifeObjects;

        public class ExternalsChangedEventArgs : EventArgs
        {
            public ExternalsChangedEventArgs(IExternal external, ChangeType change)
            {
                External = external;
                Change = change;
            }

            public IExternal External { get; private set; }
            public ChangeType Change { get; private set; }

            public enum ChangeType
            {
                Added,
                Removed
            }
        }
    }
}

