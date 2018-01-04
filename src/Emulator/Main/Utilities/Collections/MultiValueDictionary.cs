//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Utilities.Collections
{
    /// <summary>
    /// This is an implementation of MultiValueDictionary which is memory effictient at expense of sligthly
    /// more logic. It holds key-value  associations for singular values in plain dictionary, and moves them to
    /// a key-list of values dictionary when more than one value gets associated to the same key.
    ///
    /// This way, the overhead of keeping lists is limited only to keys that actually need it.
    ///
    /// Semantically it behaves as Dictionary<TKey, IReadOnlyCollection<TValue>>. So Get methods
    /// always return a collection, even in cases when there are no elements or one element to return.
    /// </summary>
    public class MultiValueDictionary<TKey, TValue> : IEnumerable<TValue> where TValue : IEquatable<TValue>
    {
        public MultiValueDictionary()
        {
            multiple = new Dictionary<TKey,List<TValue>>();
            single = new Dictionary<TKey,TValue>();
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return single.Values.Concat(multiple.Values.SelectMany(list => list)).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(TKey key, TValue value)
        {
            TValue existingSingle;
            if(single.TryGetValue(key, out existingSingle))
            {
                single.Remove(key);
                multiple.Add(key, new List<TValue>{ existingSingle, value });
                return;
            }

            List<TValue> existingMultiple;
            if(multiple.TryGetValue(key, out existingMultiple))
            {
                existingMultiple.Add(value);
                return;
            }

            single.Add(key, value);
        }

        public void AddRange(TKey key, IEnumerable<TValue> values)
        {
            TValue existingSingle;
            if(single.TryGetValue(key, out existingSingle))
            {
                single.Remove(key);
                var list = new List<TValue>{ existingSingle };
                list.AddRange(values);
                multiple.Add(key, list);
                return;
            }

            List<TValue> existingMultiple;
            if(multiple.TryGetValue(key, out existingMultiple))
            {
                existingMultiple.AddRange(values);
            }
            else
            {
                multiple.Add(key, new List<TValue>(values));
            }
        }

        /// <summary>
        /// Removes all values associated with the key.
        /// </summary>
        /// <param name="key">Key.</param>
        public bool Remove(TKey key)
        {
            return single.Remove(key) || multiple.Remove(key);
        }

        /// <summary>
        /// Removes particular key, value association.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="value">Value.</param>
        public bool Remove(TKey key, TValue value)
        {
            TValue existingSingle;
            if(single.TryGetValue(key, out existingSingle) && existingSingle.Equals(value))
            {
                return single.Remove(key);
            }
            List<TValue> existingMultipleValues;
            if(multiple.TryGetValue(key, out existingMultipleValues) && existingMultipleValues.Remove(value))
            {
                //if there is one element left in list, move it to the single values
                if(existingMultipleValues.Count == 1)
                {
                    single.Add(key, existingMultipleValues.First());
                    multiple.Remove(key);
                }
            }
            return true;
        }

        /// <summary>
        /// Tries to get the collection of values associated with the key.
        /// </summary>
        /// <returns><c>true</c>, if collection is not empty, <c>false</c> otherwise.</returns>
        /// <param name="key">Key.</param>
        /// <param name="values">Values.</param>
        public bool TryGetValue(TKey key, out IReadOnlyCollection<TValue> values)
        {
            TValue existingSingle;
            if(single.TryGetValue(key, out existingSingle))
            {
                values = new List<TValue> { existingSingle };
                return true;
            }

            List<TValue> existingMultiple;
            var ret = multiple.TryGetValue(key, out existingMultiple);
            if(ret)
            {
                values = existingMultiple.AsReadOnly();
            }
            else
            {
                values = new List<TValue>();
            }
            return ret;
        }

        public bool ContainsKey(TKey key)
        {
            return single.ContainsKey(key) || multiple.ContainsKey(key);
        }

        public bool ContainsValue(TValue value)
        {
            return this.Contains(value);
        }

        public bool Contains(TKey key)
        {
            return ContainsKey(key);
        }

        public bool Contains(TKey key, TValue value)
        {
            IReadOnlyCollection<TValue> values;
            return TryGetValue(key, out values) && values.Contains(value);
        }

        public void Clear()
        {
            single.Clear();
            multiple.Clear();
        }

        /// <summary>
        /// Contains dictionary of keys associated with one value.
        /// </summary>
        private readonly Dictionary<TKey, TValue> single;

        /// <summary>
        /// Contains dictionary of keys associated with more than one value.
        /// </summary>
        private readonly Dictionary<TKey, List<TValue>> multiple;
    }
}

