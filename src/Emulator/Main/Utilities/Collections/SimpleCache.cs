//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Collections
{
    public class SimpleCache
    {

        public int MaxEntries { get; set; } = 5000; // the Maximum bound I use

        public R Get<T1, R>(T1 parameter, Func<T1, R> generator)
        {
            if(generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }
            var obj = new CacheObject<T1, Object, Object, Func<T1, R>>(generator, parameter);
            if(simpleCache.TryGetValue(obj, out var result))
            {
                CacheHits++;
                return (R)result;
            }

            CacheMisses++;
            var generated = generator(parameter);
            if(simpleCache.Count >= MaxEntries)
            {
                var oldestKey = insertionOrderQueue.Dequeue();
                simpleCache.Remove(oldestKey);
            }

            simpleCache[obj] = generated;
            insertionOrderQueue.Enqueue(obj);
            return generated;
        }

        public R Get<T1, T2, R>(T1 parameterT1, T2 parameterT2, Func<T1, T2, R> generator)
        {
            if(generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }
            var obj = new CacheObject<T1, T2, Object, Func<T1, T2, R>>(generator, parameterT1, parameterT2);
            if(simpleCache.TryGetValue(obj, out var result))
            {
                CacheHits++;
                
                return (R)result;
            }

            CacheMisses++;
            var generated = generator(parameterT1, parameterT2);

            if(simpleCache.Count >= MaxEntries)
            {
                var oldestKey = insertionOrderQueue.Dequeue();
                simpleCache.Remove(oldestKey);
            }

            simpleCache[obj] = generated;
            insertionOrderQueue.Enqueue(obj);
            return generated;
        }

        public R Get<T1, T2, T3, R>(T1 parameterT1, T2 parameterT2, T3 parameterT3, Func<T1, T2, T3, R> generator)
        {
            if(generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }
            var obj = new CacheObject<T1, T2, T3, Func<T1, T2, T3, R>>(generator, parameterT1, parameterT2, parameterT3);
            if(simpleCache.TryGetValue(obj, out var result))
            {
                this.CacheHits = this.CacheHits + 1;
                return (R)result;
            }
            this.CacheMisses = this.CacheMisses + 1;
            var generated = generator(parameterT1, parameterT2, parameterT3);
            simpleCache[obj] = generated;
            return generated;
        }

        public void ClearCache()
        {
            CacheMisses = 0;
            CacheHits = 0;
            simpleCache.Clear();
            insertionOrderQueue.Clear();
        }

        public ulong CacheMisses { get; private set; }

        public ulong CacheHits { get; private set; }

        public int CacheSize { get { return simpleCache.Count; } }

        private readonly Dictionary<object, object> simpleCache = new Dictionary<object, object>();
        
        private readonly Queue<object> insertionOrderQueue = new Queue<object>();

        private struct CacheObject<T1, T2, T3, R>
        {
            public CacheObject(R g, T1 x = default(T1), T2 y = default(T2), T3 z = default(T3))
            {
                this.parameterT1 = x;
                this.parameterT2 = y;
                this.parameterT3 = z;
                this.generator = g;
            }

            public override bool Equals(Object obj)
            {
                if(!(obj is CacheObject<T1, T2, T3, R> cacheObj))
                {
                    return false;
                }

                var isEqualT1 = EqualityComparer<T1>.Default.Equals(parameterT1, cacheObj.parameterT1);
                var isEqualT2 = EqualityComparer<T2>.Default.Equals(parameterT2, cacheObj.parameterT2);
                var isEqualR = EqualityComparer<R>.Default.Equals(generator, cacheObj.generator);

                return isEqualT1 && isEqualT2 && isEqualR;
            }

            //https://docs.microsoft.com/en-us/dotnet/api/system.object.gethashcode?view=netframework-4.7.2&viewFallbackFrom=netf
            public override int GetHashCode()
            {
                var isDefaultT1 = EqualityComparer<T1>.Default.Equals(parameterT1, default(T1));
                var isDefaultT2 = EqualityComparer<T2>.Default.Equals(parameterT2, default(T2));
                var isDefaultT3 = EqualityComparer<T3>.Default.Equals(parameterT3, default(T3));

                var hash = generator.GetHashCode();

                if(!isDefaultT1)
                {
                    hash ^= Misc.RotateLeft(parameterT1.GetHashCode(), 2);
                }

                if(!isDefaultT2)
                {
                    hash ^= Misc.RotateLeft(parameterT2.GetHashCode(), 4);
                }

                if(!isDefaultT3)
                {
                    hash ^= Misc.RotateLeft(parameterT3.GetHashCode(), 6);
                }

                return hash;
            }

            private readonly T1 parameterT1;
            private readonly T2 parameterT2;
            private readonly T3 parameterT3;
            private readonly R generator;
        }
    }
}
