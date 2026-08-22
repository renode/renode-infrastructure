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
using Antmicro.Migrant.Hooks;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Core
{
    public sealed class PseudorandomNumberGenerator
    {
        public PseudorandomNumberGenerator()
        {
            locker = new object();
            InitializeGenerator();
        }

        public void ResetSeed(int newSeed)
        {
            lock(locker)
            {
                if(generator.Values.Count != 0)
                {
                    Logger.Log(LogLevel.Warning, "Pseudorandom Number Generator has already been used with seed {0}. Next time it will use a new one {1}. It won't be possible to repeat this exact execution.", baseSeed, newSeed);
                    InitializeGenerator();
                    generatorsByName.Clear();
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

        private RandomGenerator GetGeneratorForCurentThread()
        {
            // The per-thread generator must be keyed by the logical thread *name*, not by the physical
            // managed-thread identity that ThreadLocal uses. Some threads (e.g. emulated CPU cores) are
            // torn down and recreated under the same name during a run. Creating a brand-new Random for
            // each physical thread would re-seed it identically (the seed derives only from the name and
            // baseSeed), restarting the pseudo-random stream from the beginning every time. By caching the
            // generator by name we let a recreated thread continue its existing, deterministic stream.
            var name = Thread.CurrentThread.Name;
            if(!generatorsByName.TryGetValue(name, out var randomGenerator))
            {
                randomGenerator = new RandomGenerator
                {
                    Random = new Random(GetSeedForThread()),
                    ThreadName = name
                };
                generatorsByName.Add(name, randomGenerator);
            }
            return randomGenerator;
        }

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
                if(generator.Values.Count == 0 && serializedGenerators.Count == 0)
                {
                    Logger.Log(LogLevel.Info, "Pseudorandom Number Generator was created with seed: {0}", baseSeed);
                }
                if(!generator.IsValueCreated && serializedGenerators.Count > 0)
                {
                    var possibleGenerator = serializedGenerators.FirstOrDefault(x => x.ThreadName == Thread.CurrentThread.Name);
                    if(possibleGenerator != null)
                    {
                        //this is a small optimization so we don't fall in this `if` after we deserialize all entries
                        serializedGenerators.Remove(possibleGenerator);
                        generator.Value = possibleGenerator;
                    }
                }
                return generator.Value.Random;
            }
        }

        [PreSerialization]
        private void BeforeSerialization()
        {
            foreach(var randomGenerator in generator.Values)
            {
                serializedGenerators.Add(randomGenerator);
            }
        }

        [PostDeserialization]
        private void InitializeGenerator()
        {
            generator = new ThreadLocal<RandomGenerator>(() => GetGeneratorForCurentThread(), true);
        }

        [Transient]
        private ThreadLocal<RandomGenerator> generator;

        private readonly HashSet<RandomGenerator> serializedGenerators = new HashSet<RandomGenerator>(); // we initialize the collection to simplify the rest of the code
        // Generators kept alive by logical thread name so a thread recreated under the same name continues
        // its stream instead of restarting it. Outlives the physical threads tracked by `generator`.
        private readonly Dictionary<string, RandomGenerator> generatorsByName = new Dictionary<string, RandomGenerator>();
        private readonly object locker;

        private class RandomGenerator
        {
            public Random Random;
            public string ThreadName;
        }
    }
}
