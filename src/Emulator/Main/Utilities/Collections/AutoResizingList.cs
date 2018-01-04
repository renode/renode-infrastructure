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

namespace Antmicro.Renode.Utilities.Collections
{
    public sealed class AutoResizingList<T> : IList<T>
    {
        public AutoResizingList(int initialCapacity = 4)
        {
            this.initialCapacity = initialCapacity;
            Clear();
        }
        
        public int Count { get; private set; }
        
        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }
        
        public T this[int index]
        {
            get
            {
                ResizeTo(index + 1);
                return data[index];
            }
            set
            {
                ResizeTo(index + 1);
                data[index] = value;
            }
        }
        
        public void Add(T item)
        {
            ResizeTo(Count + 1);
            data[Count - 1] = item;
        }
        
        public void Clear()
        {
            data = new T[initialCapacity];
            Count = 0;
        }
        
        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }
        
        public void CopyTo(T[] array, int index)
        {
            Array.Copy(data, array, Count);
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            var count = Count;
            for(var i = 0; i < count; i++)
            {
                yield return data[i];
            }
        }
        
        public int IndexOf(T item)
        {
            var index = Array.IndexOf(data, item);
            if(index >= Count)
            {
                return -1;
            }
            return index;
        }
        
        public void Insert(int index, T item)
        {
            if(index >= Count)
            {
                this[index] = item;
                return;
            }
            ResizeTo(Count + 1);
            for(var i = Count - 2; i >= index; i--)
            {
                data[i + 1] = data[i];
            }
            data[index] = item;
        }
        
        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if(index == -1)
            {
                return false;
            }
            RemoveAt(index);
            return true;
        }
        
        public void RemoveAt(int index)
        {
            ResizeTo(Count - 1);
            var count = Count;
            for(var i = index; i < count; i++)
            {
                data[i] = data[i + 1];
            }
        }
        
        private void ResizeTo(int neededSize)
        {
            if(neededSize < 0)
            {
                throw new ArgumentException("Index cannot be negative.");
            }
            Count = Math.Max(Count, neededSize);
            if(data.Length >= neededSize)
            {
                return;
            }
            var newData = new T[data.Length*2];
            data.CopyTo(newData, 0);
            data = newData;
        }
        
        private T[] data;
        private readonly int initialCapacity;
    }
}

