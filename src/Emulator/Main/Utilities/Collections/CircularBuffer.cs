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
    public class CircularBuffer<T> : IEnumerable<T>
    {
        public CircularBuffer(int size)
        {
            buffer = new T[size];
        }

        public void Clear() 
        {
            isWrapped = false;
            lastPosition = 0;
        }

        public void Enqueue(T element)
        {
            buffer[lastPosition] = element;
            UpdateIndex();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if(!isWrapped)
            {
                Array.Copy(buffer, array, lastPosition);
                return;
            }
            var start = lastPosition + 1;
            var rightSideLength = buffer.Length - start;
            Array.Copy(buffer, start, array, arrayIndex, rightSideLength);
            Array.Copy(buffer, 0, array, arrayIndex + rightSideLength, start - 1);
        }

        public IEnumerator<T> GetEnumerator()
        {
            if(isWrapped)
            {
                var end = lastPosition;
                var currentYield = lastPosition + 1;
                while(currentYield != end)
                {
                    yield return buffer[currentYield];
                    currentYield++;
                    if(currentYield == buffer.Length)
                    {
                        currentYield = 0;
                    }
                }
            }
            else
            {
                for(var i = 0; i < lastPosition; i++)
                {
                    yield return buffer[i];
                }
            }
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void UpdateIndex()
        {
            lastPosition++;
            if(lastPosition == buffer.Length)
            {
                isWrapped = true;
                lastPosition = 0;
            }
        }

        private readonly T[] buffer;
        private int lastPosition;
        private bool isWrapped;
    }
}

