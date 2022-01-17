//
// Copyright (c) 2010-2022 Antmicro
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
    public class FastReadConcurrentTwoWayDictionary<TLeft, TRight>: IDisposable
    {
        public FastReadConcurrentTwoWayDictionary()
        {
            locker = new object();

            lefts = new Dictionary<TLeft, TRight>();
            rights = new Dictionary<TRight, TLeft>();

            Lefts = new TLeft[0];
            Rights = new TRight[0];
        }
        
        public void Dispose()
        {
            Clear();
            ItemAdded = null;
            ItemRemoved = null;
        }

        public void Clear()
        {
            lock(locker)
            {
                var copyOfRights = rights;

                rights = new Dictionary<TRight, TLeft>();
                lefts = new Dictionary<TLeft, TRight>();

                Lefts = new TLeft[0];
                Rights = new TRight[0];

                foreach(var item in copyOfRights)
                {
                    OnItemRemoved(item.Value, item.Key);
                }
            }
        }

        public void Remove(TLeft left)
        {
            TRight fake;
            TryRemove(left, out fake);
        }

        public bool TryRemove(TLeft left, out TRight right)
        {
            lock(locker)
            {
                var newRights = new Dictionary<TRight, TLeft>(rights);
                var newLefts = new Dictionary<TLeft, TRight>(lefts);

                if(!newLefts.TryGetValue(left, out right))
                {
                    return false;
                }

                newRights.Remove(right);
                newLefts.Remove(left);

                lefts = newLefts;
                rights = newRights;

                Lefts = lefts.Keys.ToArray();
                Rights = rights.Keys.ToArray();

                OnItemRemoved(left, right);
                return true;
            }
        }

        public void Remove(TRight right)
        {
            lock(locker)
            {
                var newRights = new Dictionary<TRight, TLeft>(rights);
                var newLefts = new Dictionary<TLeft, TRight>(lefts);

                var leftToRemove = newRights[right];
                newRights.Remove(right);
                newLefts.Remove(leftToRemove);

                lefts = newLefts;
                rights = newRights;

                Lefts = lefts.Keys.ToArray();
                Rights = rights.Keys.ToArray();

                OnItemRemoved(leftToRemove, right);
            }
        }

        public void Add(TLeft left, TRight right)
        {
            Add(right, left);
        }

        public void Add(TRight right, TLeft left)
        {
            lock(locker)
            {
                var newRights = new Dictionary<TRight, TLeft>(rights);
                var newLefts = new Dictionary<TLeft, TRight>(lefts);

                newRights.Add(right, left);
                newLefts.Add(left, right);

                lefts = newLefts;
                rights = newRights;

                Lefts = lefts.Keys.ToArray();
                Rights = rights.Keys.ToArray();

                var ia = ItemAdded;
                if(ia != null)
                {
                    ia(left, right);
                }
            }
        }

        public bool ExistsEither(TRight right, TLeft left)
        {
            return ExistsEither(left, right);
        }

        public bool ExistsEither(TLeft left, TRight right)
        {
            Dictionary<TLeft, TRight> copyOfLefts;
            Dictionary<TRight, TLeft> copyOfRights;

            lock(locker)
            {
                copyOfLefts = lefts;
                copyOfRights = rights;
            }

            return copyOfLefts.ContainsKey(left) || copyOfRights.ContainsKey(right);
        }

        public bool Exists(TLeft left)
        {
            var copy = lefts;
            return copy.ContainsKey(left);
        }

        public bool TryGetValue(TLeft left, out TRight right)
        {
            right = default(TRight);
            if(left == null)
            {
                return false;
            }
            var copy = lefts;
            return copy.TryGetValue(left, out right);
        }

        public bool TryGetValue(TRight right, out TLeft left)
        {
            left = default(TLeft);
            if(right == null)
            {
                return false;
            }
            var copy = rights;
            return copy.TryGetValue(right, out left);
        }

        public event Action<TLeft, TRight> ItemAdded;

        public event Action<TLeft, TRight> ItemRemoved;

        public TLeft[] Lefts { get; private set; }

        public TRight[] Rights { get; private set; }

        public TLeft this[TRight index]
        {
            get
            {
                var copy = rights;
                return copy[index];
            }
        }

        public TRight this[TLeft index]
        {
            get
            {
                var copy = lefts;
                return copy[index];
            }
        }

        public int Count
        {
            get { return lefts.Count; }
        }

        private void OnItemRemoved(TLeft left, TRight right)
        {
            var itemRemoved = ItemRemoved;
            if(itemRemoved != null)
            {
                itemRemoved(left, right);
            }
        }

        private readonly object locker;
        private Dictionary<TLeft, TRight> lefts;
        private Dictionary<TRight, TLeft> rights;
    }
}

