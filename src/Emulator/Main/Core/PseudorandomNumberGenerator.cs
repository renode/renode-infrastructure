//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Core
{
    public sealed class PseudorandomNumberGenerator
    {
        public PseudorandomNumberGenerator()
        {
            locker = new object();
        }

        public void ResetSeed(int newSeed)
        {
            lock(locker)
            {
                if(generators.Count != 0)
                {
                    Logger.Log(LogLevel.Warning, "Pseudorandom Number Generator has already been used with seed {0}. Next time it will use a new one {1}. It won't be possible to repeat this exact execution.", baseSeed, newSeed);
                    generator = new();
                    generators.Clear();
                }
                baseSeed = newSeed;
            }
        }

        public int GetCurrentSeed()
        {
            return baseSeed;
        }

        public double NextDouble()
        {
            return GetOrCreateGenerator().NextDouble();
        }

        public int Next()
        {
            return GetOrCreateGenerator().Next();
        }

        public int Next(int maxValue)
        {
            return GetOrCreateGenerator().Next(maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            return GetOrCreateGenerator().Next(minValue, maxValue);
        }

        public void NextBytes(byte[] buffer)
        {
            GetOrCreateGenerator().NextBytes(buffer);
        }

        public ulong NextUlong()
        {
            byte[] buffer = new byte[8];
            NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }

        public void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination)
        {
            GetOrCreateGenerator().GetItems(choices, destination);
        }

        private static int baseSeed = new Random().Next();

        private int GetSeedForThread()
        {
            if(Thread.CurrentThread.IsThreadPoolThread)
            {
                throw new InvalidOperationException($"Cannot access {typeof(PseudorandomNumberGenerator)} from a thread pool.");
            }
            var name = Thread.CurrentThread.Name;
            if(string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException($"Cannot access {typeof(PseudorandomNumberGenerator)} from an unnamed thread.");
            }

            return Encoding.UTF8.GetBytes(name).Sum(x => (int)x) ^ baseSeed;
        }

        private Random GetOrCreateGenerator()
        {
            lock(locker)
            {
                if(generator.Value != null)
                {
                    return generator.Value;
                }
                var threadName = Thread.CurrentThread.Name;
                if(generators.TryGetValue(threadName, out var rand))
                {
                    generator.Value = rand;
                    return rand;
                }
                if(generators.Count == 0)
                {
                    Logger.Log(LogLevel.Info, "Pseudorandom Number Generator was created with seed: {0}", baseSeed);
                }
                rand = new Random(GetSeedForThread());
                generators[threadName] = rand;
                generator.Value = rand;
                return rand;
            }
        }

        [Constructor]
        private ThreadLocal<Random> generator = new();

        private readonly Dictionary<string, Random> generators = new();
        private readonly object locker;
    }
}
