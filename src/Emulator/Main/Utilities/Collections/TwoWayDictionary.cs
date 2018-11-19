//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant.Hooks;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Migrant;

namespace Antmicro.Renode.Utilities.Collections
{
    public class TwoWayDictionary<TLeft, TRight>
    {
        public void Clear()
        {
            lock(lockObject)
            {
                var array = Lefts.ToArray();
                foreach(var item in array)
                {
                    Remove(item);
                }
            }
        }

        public bool Remove(TLeft left)
        {
            TRight tmp;
            return Remove(left, out tmp);
        }

        public bool TryExchange(TLeft left, TRight newRight, out TRight oldRight)
        {
            lock(lockObject)
            {
                if(!Remove(left, out oldRight))
                {
                    return false;
                }
                Add(left, newRight);
                return true;
            }
        }

        public bool TryExchange(TRight right, TLeft newLeft, out TLeft oldLeft)
        {
            lock(lockObject)
            {
                if(!Remove(right, out oldLeft))
                {
                    return false;
                }
                Add(newLeft, right);
                return true;
            }
        }

        public bool Remove(TLeft left, out TRight right)
        {
            right = default(TRight);
            lock(lockObject)
            {
                if(!Exists(left))
                {
                    return false;
                }
                right = lefts[left];
                RemoveExistingMapping(left, right);
            }
            return true;
        }

        public bool Remove(TRight right, out TLeft left)
        {
            left = default(TLeft);
            lock(lockObject)
            {
                if(!Exists(right))
                {
                    return false;
                }
                left = rights[right];
                RemoveExistingMapping(left, right);
            }
            return true;
        }

        public bool Remove(TRight right)
        {
            lock(lockObject)
            {
                if(!Exists(right))
                {
                    return false;
                }
                RemoveExistingMapping(rights[right], right);
            }
            return true;
        }

        public void Add(TLeft left, TRight right)
        {
            lock(lockObject)
            {
                lefts.Add(left, right);
                rights.Add(right, left);
            }

            OnItemAdded(left, right);
        }

        public bool TryGetValue(TLeft left, out TRight right)
        {
            return lefts.TryGetValue(left, out right);
        }

        public bool TryGetValue(TRight right, out TLeft left)
        {
            return rights.TryGetValue(right, out left);
        }

        public bool ExistsEither(TLeft left, TRight right)
        {
            lock(lockObject)
            {
                return Exists(left) || Exists(right);
            }
        }

        public bool Exists(TLeft left)
        {
            return lefts.ContainsKey(left);
        }

        public bool Exists(TRight right)
        {
            return rights.ContainsKey(right);
        }

        public TLeft this[TRight index]
        {
            get
            {
                return rights[index];
            }
        }

        public TRight this[TLeft index]
        {
            get
            {
                return lefts[index];
            }
        }

        public int Count
        {
            get { return lefts.Count; }
        }

        public IEnumerable<TLeft> Lefts { get { return lefts.Keys; } }

        public IEnumerable<TRight> Rights { get { return rights.Keys; } }

        public event Action<TLeft, TRight> ItemAdded;

        public event Action<TLeft, TRight> ItemRemoved;

        private void RemoveExistingMapping(TLeft left, TRight right)
        {
            lefts.Remove(left);
            rights.Remove(right);
            OnItemRemoved(left, right);
        }
               
        private void OnItemAdded(TLeft left, TRight right)
        {
            var itemAdded = ItemAdded;
            if(itemAdded != null)
            {
                itemAdded(left, right);
            }
        }

        private void OnItemRemoved(TLeft left, TRight right)
        {
            var itemRemoved = ItemRemoved;
            if(itemRemoved != null)
            {
                itemRemoved(left, right);
            }
        }

        [PostDeserialization]
        private void AfterDeserialization()
        {
            // rebuild not serialized dictionary
            foreach(var item in lefts)
            {
                rights.Add(item.Value, item.Key);
            }
        }

        private readonly Dictionary<TLeft, TRight> lefts = new Dictionary<TLeft, TRight>();

        [Constructor]
        private readonly Dictionary<TRight, TLeft> rights = new Dictionary<TRight, TLeft>();
        private readonly object lockObject = new object();
    }
}
