//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections;
using Antmicro.Migrant;
using System.Linq;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.Utilities.Collections
{
    public class SerializableWeakDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        public SerializableWeakDictionary()
        {
            sync = new object();
            Clear();
        }

        public void Add(TKey key, TValue value)
        {
            lock(sync)
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                if(currentKeys.Contains(key))
                {
                    throw new InvalidOperationException(string.Format("Key '{0}' already exists.", key));
                }
                keys.Add(new WeakReference(key));
                values.Add(new WeakReference(value));
            }
        }

        public bool ContainsKey(TKey key)
        {
            lock(sync)
            {
                return keys.Select(x => x.Target).Where(x => x != null).Any(x => x.Equals(key));
            }
        }

        public bool Remove(TKey key)
        {
            lock(sync)
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                var index = currentKeys.IndexOf(key);
                if(index == -1)
                {
                    return false;
                }
                keys.RemoveAt(index);
                values.RemoveAt(index);
                return true;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock(sync)
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                var index = currentKeys.IndexOf(key);
                if(index == -1)
                {
                    value = default(TValue);
                    return false;
                }
                value = currentValues[index];
                return true;
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if(!TryGetValue(key, out value))
                {
                    throw new KeyNotFoundException();
                }
                return value;
            }
            set
            {
                lock(sync)
                {
                    List<TKey> currentKeys;
                    List<TValue> currentValues;
                    ObtainKeysAndValues(out currentKeys, out currentValues);
                    var index = currentKeys.IndexOf(key);
                    if(index == -1)
                    {
                        Add(key, value);
                        return;
                    }
                    values[index] = new WeakReference(value);
                }
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                return currentKeys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                return currentValues;
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            lock(sync)
            {
                keys = new List<WeakReference>();
                values = new List<WeakReference>();
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            lock(sync)
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                var index = currentKeys.IndexOf(item.Key);
                if(index == -1)
                {
                    return false;
                }
                return currentValues[index].Equals(item.Value);
            }
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                return currentKeys.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }


        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            List<TKey> currentKeys;
            List<TValue> currentValues;
            ObtainKeysAndValues(out currentKeys, out currentValues);
            for(var i = 0; i < currentKeys.Count; i++)
            {
                yield return new KeyValuePair<TKey, TValue>(currentKeys[i], currentValues[i]);
            }
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public TValue GetOrCreateValue(TKey key, TValue value)
        {
            lock(sync)
            {
                List<TKey> currentKeys;
                List<TValue> currentValues;
                ObtainKeysAndValues(out currentKeys, out currentValues);
                var index = currentKeys.IndexOf(key);
                if(index == -1)
                {
                    Add(key, value);
                    return value;
                }
                return currentValues[index];
            }
        }

        private void ObtainKeysAndValues(out List<TKey> currentKeys, out List<TValue> currentValues)
        {
            lock(sync)
            {
                currentKeys = new List<TKey>();
                currentValues = new List<TValue>();
                for(var i = 0; i < keys.Count; i++)
                {
                    // if the key OR value is not available, the whole entry is not available
                    var key = keys[i].Target;
                    var value = values[i].Target;
                    if(key == null || value == null)
                    {
                        keys.RemoveAt(i);
                        values.RemoveAt(i);
                        i--;
                        continue;
                    }
                    currentKeys.Add((TKey)key);
                    currentValues.Add((TValue)value);
                }
            }
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            ObtainKeysAndValues(out serializedKeys, out serializedValues);
        }

        [PostSerialization]
        private void AfterSerialization()
        {
            serializedKeys = null;
            serializedValues = null;
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            keys = serializedKeys.Select(x => new WeakReference(x)).ToList();
            values = serializedValues.Select(x => new WeakReference(x)).ToList();
            AfterSerialization();
        }

        private object sync;

        [Transient]
        private List<WeakReference> keys;

        [Transient]
        private List<WeakReference> values;

        private List<TKey> serializedKeys;
        private List<TValue> serializedValues;
    }
}

