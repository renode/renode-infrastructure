//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Utilities.Collections
{
    public class SerializableWeakKeyDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : class where TValue : class
    {
        #region IDictionary implementation

        public void Add(TKey key, TValue value)
        {
            lock(sync)
            {
                wl.Add(new WeakReference(key));
                cwt.Add(key, value);
            }
        }

        public bool ContainsKey(TKey key)
        {
            lock(sync)
            {
                return wl.Any(x => 
                {
                    TKey lkey;
                    return TryGetTarget(x, out lkey) && lkey != null && lkey.Equals(key);
                });
            }
        }

        public bool Remove(TKey key)
        {
            lock(sync)
            {
                wl.RemoveAll(x => 
                {
                    TKey lkey;
                    return TryGetTarget(x, out lkey) && lkey.Equals(key);
                });
                return cwt.Remove(key);
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock(sync)
            {
                return cwt.TryGetValue(key, out value);
            }
        }

        public TValue this[TKey index]
        {
            get
            {
                lock(sync)
                {
                    TValue value;
                    if(cwt.TryGetValue(index, out value))
                    {
                        return value;
                    }
                    throw new KeyNotFoundException();
                }
            }
            set
            {
                lock(sync)
                {
                    if(ContainsKey(index))
                    {
                        cwt.Remove(index);
                        cwt.Add(index, value);
                    }
                    else
                    {
                        Add(index, value);
                    }
                }
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                lock(sync)
                {
                    return wl.Select(x =>
                    {
                        TKey target;
                        return TryGetTarget(x, out target) ? target : null;
                    }).Where(x => x != null).ToList();
                }
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                lock(sync)
                {
                    return wl.Select(x =>
                    {
                        TKey target;
                        TValue value;
                        return TryGetTarget(x, out target) && cwt.TryGetValue(target, out value) ? value : null;
                    }).Where(x => x != null).ToList();
                }
            }
        }

        #endregion

        #region ICollection implementation

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public int Count { get { return wl.Count; } }

        public bool IsReadOnly { get { return false; } }

        #endregion

        #region IEnumerable implementation

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var result = new List<KeyValuePair<TKey, TValue>>();
            lock(sync)
            {
                foreach(var weakKey in wl)
                {
                    TKey key;
                    TValue value;
                    if(TryGetTarget(weakKey, out key) && cwt.TryGetValue(key, out value))
                    {
                        result.Add(new KeyValuePair<TKey, TValue>(key, value));
                    }
                }
            }

            for(var i = 0; i < result.Count; i++)
            {
                yield return result[i];
            }
        }

        #endregion

        #region IEnumerable implementation

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        public SerializableWeakKeyDictionary()
        {
            cwt = new ConditionalWeakTable<TKey, TValue>();
            wl = new List<WeakReference>();
            sync = new object();
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            keys = new List<TKey>();
            values = new List<TValue>();

            lock(sync)
            {
                foreach (var x in wl)
                {
                    TKey target;
                    TValue value;
                    if(TryGetTarget(x, out target) && cwt.TryGetValue(target, out value))
                    {
                        keys.Add(target);
                        values.Add(value);
                    }
                }
            }
        }

        [PostDeserialization]
        private void PostDeserialization()
        {
            lock(sync)
            {
                for(int i = 0; i < keys.Count; i++)
                {
                    Add(keys[i], values[i]);
                }
            }

            keys = null;
            values = null;
        }

        private bool TryGetTarget<T>(WeakReference wref, out T result) where T : class
        {
            result = wref.Target as T;
            return (result != null);
        }

        [Constructor]
        private readonly ConditionalWeakTable<TKey, TValue> cwt;
        [Constructor]
        private readonly List<WeakReference> wl;
        private readonly object sync;

        private List<TKey> keys;
        private List<TValue> values;
    }
}

