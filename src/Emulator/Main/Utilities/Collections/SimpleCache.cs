//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities.Collections
{
    public class SimpleCache
    {
        public R Get<T1, R>(T1 parameter, Func<T1, R> generator)
        {
            if(generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }
            var obj = new CacheObject<T1, Object, Func<T1, R>>(generator, parameter);
            if(simpleCache.TryGetValue(obj, out var result))
            {
                this.CacheHits = this.CacheHits + 1;
                return (R)result;
            }

            this.CacheMisses = this.CacheMisses + 1;
            var generated = generator(parameter);
            simpleCache[obj] = generated;
            return generated;
        }

        public R Get<T1, T2, R>(T1 parameterT1, T2 parameterT2, Func<T1, T2, R> generator)
        {
            if(generator == null)
            {
               throw new ArgumentNullException(nameof(generator));
            }
            var obj = new CacheObject<T1, T2, Func<T1, T2, R>>(generator, parameterT1, parameterT2);
            if(simpleCache.TryGetValue(obj, out var result))
            {
                this.CacheHits = this.CacheHits + 1;
                return (R)result;
            }
            this.CacheMisses = this.CacheMisses + 1;
            var generated = generator(parameterT1, parameterT2);
            simpleCache[obj] = generated;
            return generated;
        }

        public void ClearCache()
        {
            CacheMisses = 0;
            CacheHits = 0;
            simpleCache.Clear();
        }

        public ulong CacheMisses { get; private set; }

        public ulong CacheHits { get; private set; }

        public int CacheSize { get { return simpleCache.Count; } }

        private struct CacheObject<T1, T2, R>
        {
            public CacheObject(R z, T1 x = default(T1), T2 y = default(T2))
            {
                this.parameterT1 = x;
                this.parameterT2 = y;
                this.generator = z;
            }

            public override bool Equals(Object obj)
            {
                if(!(obj is CacheObject<T1, T2, R> cacheObj))
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

                var hash = generator.GetHashCode();

                if(!isDefaultT1)
                {
                    hash ^= ShiftAndWrap(parameterT1.GetHashCode(), 2);
                }

                if(!isDefaultT2)
                {
                    hash ^= ShiftAndWrap(parameterT2.GetHashCode(), 4);
                }

                return hash;
            }

            private int ShiftAndWrap(int value, int positions)
            {
                positions = positions & 0x1F;

                // Save the existing bit pattern, but interpret it as an unsigned integer.
                uint number = BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
                // Preserve the bits to be discarded.
                uint wrapped = number >> (32 - positions);
                // Shift and wrap the discarded bits.
                return BitConverter.ToInt32(BitConverter.GetBytes((number << positions) | wrapped), 0);
            }

            private readonly T1 parameterT1;
            private readonly T2 parameterT2;
            private readonly R generator;
        }

        private readonly Dictionary<object, object> simpleCache = new Dictionary<object, object>();
    }
}
