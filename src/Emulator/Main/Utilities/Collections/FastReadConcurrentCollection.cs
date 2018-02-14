//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Collections
{
    public class FastReadConcurrentCollection<T>
    {
        public FastReadConcurrentCollection()
        {
            Clear();
        }

        public void Add(T item)
        {
            lock(locker)
            {
                var copy = new List<T>(Items);
                copy.Add(item);
                Items = copy.ToArray();
            }
        }

        public void Remove(T item)
        {
            lock(locker)
            {
                var copy = new List<T>(Items);
                copy.Remove(item);
                Items = copy.ToArray();
            }
        }

        public void Clear()
        {
            lock(locker)
            {
                Items = new T[0];
            }
        }

        public T[] Items { get; private set; }

        private readonly object locker = new object();
    }
}

