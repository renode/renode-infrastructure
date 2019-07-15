//
// Copyright (c) 2010-2019 Antmicro
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
    public class CircularBuffer<T> : IEnumerable<T>
    {
        public CircularBuffer(int size)
        {
            buffer = new T[size];
            Clear();
        }

        public void Clear()
        {
            IsWrapped = false;
            LastPosition = buffer.Length - 1;
            FirstPosition = 0;
            IsEmpty = true;
        }

        public void Enqueue(T element)
        {
            if(!IsEmpty && ((LastPosition + 1) % buffer.Length) == FirstPosition)
            {
                MoveFirst();
            }
            MoveLast();
            buffer[LastPosition] = element;
            IsEmpty = false;
        }

        public bool TryDequeue(out T result)
        {
            result = default(T);
            if(IsEmpty)
            {
                return false;
            }
            result = buffer[FirstPosition];
            if(FirstPosition == LastPosition)
            {
                IsEmpty = true;
            }
            MoveFirst();
            return true;
        }

        public IEnumerable<T> DequeueAll()
        {
            while(TryDequeue(out var value))
            {
                yield return value;
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if(IsEmpty)
            {
                return;
            }
            if(!IsWrapped)
            {
                Array.Copy(buffer, FirstPosition, array, arrayIndex, LastPosition + 1 - FirstPosition);
                return;
            }
            var start = FirstPosition;
            var rightSideLength = buffer.Length - start;
            Array.Copy(buffer, start, array, arrayIndex, rightSideLength);
            Array.Copy(buffer, 0, array, arrayIndex + rightSideLength, LastPosition + 1);
        }

        public IEnumerator<T> GetEnumerator()
        {
            if(IsEmpty)
            {
                yield break;
            }
            var end = LastPosition;
            var currentYield = FirstPosition;
            do
            {
                yield return buffer[currentYield];
                currentYield++;
                if(currentYield == buffer.Length)
                {
                    currentYield = 0;
                }
            }
            while(currentYield != (end + 1 % buffer.Length));
        }

        public T this[int i]
        {
            get
            {
                return buffer[i];
            }
            set
            {
                buffer[i] = value;
            }
        }

        private int LastPosition { get; set; }
        private int FirstPosition { get; set; }
        private bool IsEmpty { get; set; }
        public bool IsWrapped { get; set; }
        public int Capacity => buffer.Length;
        public int Count => IsEmpty ? 0
            : (IsWrapped
                ? LastPosition + buffer.Length + 1 - FirstPosition
                : LastPosition + 1 - FirstPosition);

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void UpdateIndex()
        {
        }

        private void MoveFirst()
        {
            FirstPosition++;
            if(FirstPosition == buffer.Length)
            {
                IsWrapped = false;
                FirstPosition = 0;
            }
        }
        private void MoveLast()
        {
            LastPosition++;
            if(LastPosition == buffer.Length)
            {
                IsWrapped = !IsEmpty; // usually we should isWrapped=true here, unless there is nothing in the collection. Then lastPosition should jump to 0, but there is no wrapping
                LastPosition = 0;
            }
        }

        private readonly T[] buffer;
    }
}

