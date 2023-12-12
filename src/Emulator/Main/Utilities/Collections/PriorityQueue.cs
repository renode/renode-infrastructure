//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Collections
{
    public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        public PriorityQueue()
        {
            list = new SortedList<Key, TElement>();
        }

        public void Enqueue(TElement item, TPriority priority)
        {
            list.Add(new Key(priority, count), item);
            count++;
        }

        public bool TryDequeue(out TElement item, out TPriority priority)
        {
            if(!TryPeek(out item, out priority))
            {
                return false;
            }
            list.RemoveAt(0);
            return true;
        }

        public TElement Dequeue()
        {
            var item = Peek();
            list.RemoveAt(0);
            return item;
        }

        public bool TryPeek(out TElement item, out TPriority priority)
        {
            if(Count == 0)
            {
                item = default(TElement);
                priority = default(TPriority);
                return false;
            }
            item = list[list.Keys[0]];
            priority = list.Keys[0].priority;
            return true;
        }

        public TElement Peek()
        {
            if(!TryPeek(out var item, out _))
            {
                throw new InvalidOperationException("The queue is empty");
            }
            return item;
        }

        public void Clear()
        {
            list.Clear();
            count = 0;
        }

        public int Count => list.Count;

        private readonly SortedList<Key, TElement> list;
        // Used to ensure keys are not duplicate, incremented for every item inserted
        private ulong count;

        private class Key : IComparable<Key>
        {
            public Key(TPriority priority, ulong count)
            {
                this.priority = priority;
                this.count = count;
            }

            public int CompareTo(Key other)
            {
                int priorityDiff = priority.CompareTo(other.priority);
                return priorityDiff != 0 ? priorityDiff : count.CompareTo(other.count);
            }

            public readonly TPriority priority;
            public readonly ulong count;
        }
    }
}
