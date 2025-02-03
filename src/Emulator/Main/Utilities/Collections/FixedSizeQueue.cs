//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Collections
{
    public class SizeException : Exception { }

    public class FixedSizeQueue<T> {
        public FixedSizeQueue(int size, bool thresholdCrossingEmptying = true,
                              Action<FixedSizeQueue<T>> hasBecomeEmpty = null,
                              Action<FixedSizeQueue<T>> hasBecomeFull = null,
                              Action<FixedSizeQueue<T>> hasCrossedThreshold = null)
        {
            if(size <= 0) {
                throw new SizeException();
            }

            this.size = size;
            this.threshold = size / 2;
            this.hasBecomeEmpty = hasBecomeEmpty;
            this.hasBecomeFull = hasBecomeFull;
            this.hasCrossedThreshold = hasCrossedThreshold;
            this.thresholdCrossingEmptying = thresholdCrossingEmptying;

            inner = new Queue<T>();
            lastOrDefault = default(T);
        }

        public void Enqueue(T t) {
            // throw exception if queue is already full
            if(IsFull) {
                throw new InvalidOperationException();
            }

            var origCount = inner.Count;

            inner.Enqueue(t);
            lastOrDefault = t;

            // invoke action if threshold is crossed and not emptying
            if((origCount < threshold) && (inner.Count >= threshold) && !thresholdCrossingEmptying) {
                hasCrossedThreshold?.Invoke(this);
            }

            // invoke action if queue is going from non-full to full
            if(IsFull) {
                hasBecomeFull?.Invoke(this);
            }
        }

        public T Dequeue() {
            // throw exception if queue is empty
            if(IsEmpty) {
                throw new InvalidOperationException();
            }

            var origCount = inner.Count;

            // throws InvalidOperationException on empty queue
            T t = inner.Dequeue();

            // invoke action if queue is going from non-empty -> empty
            if(IsEmpty) {
                lastOrDefault = default(T);
                hasBecomeEmpty?.Invoke(this);
            }

            // invoke action if threshold is crossed and emptying
            if((origCount >= threshold) && (inner.Count < threshold) && thresholdCrossingEmptying) {
                hasCrossedThreshold?.Invoke(this);
            }

            return t;
        }

        // if queue is non-empty, returns a copy of the last element (without dequeueing)
        // if queue is empty, returns a default T
        public T LastOrDefault() {
            return lastOrDefault;
        }

        public void setThreshold(int threshold) {
            this.threshold = threshold;
        }

        public bool IsEmpty {
            get { return inner.Count == 0; }
        }

        public bool IsFull {
            get { return inner.Count >= size; }
        }

        public bool ThresholdCrossed {
            get {
                if(thresholdCrossingEmptying) {
                    return inner.Count < threshold;
                } else {
                    return inner.Count >= threshold;
                }
            }
        }

        private event Action<FixedSizeQueue<T>> hasBecomeEmpty;
        private event Action<FixedSizeQueue<T>> hasBecomeFull;
        private event Action<FixedSizeQueue<T>> hasCrossedThreshold;

        private readonly int size;
        private int threshold;
        private readonly bool thresholdCrossingEmptying;

        private Queue<T> inner;
        private T lastOrDefault;
    }

}
