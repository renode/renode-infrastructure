//
// Copyright (c) 2010-2018 Antmicro
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

        private RandomGenerator GetGeneratorForCurentThread()
        {
            return new RandomGenerator
            {
                Random = new Random(GetSeedForThread()),
                ThreadName = Thread.CurrentThread.Name
            };
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
        private readonly object locker;

        private static int baseSeed = new Random().Next();

        private class RandomGenerator
        {
            public Random Random;
            public string ThreadName;
        }
    }
}
