//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Text;
using System.Threading;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Core
{
    public class PseudorandomNumberGenerator
    {
        public PseudorandomNumberGenerator()
        {
            locker = new object();
            generator = new ThreadLocal<Random>(() => new Random(GetSeedForThread()), true);
        }

        public void ResetSeed(int newSeed)
        {
            lock(locker)
            {
                if(generator.Values.Count != 0)
                {
                    Logger.Log(LogLevel.Warning, "Pseudorandom Number Generator has already been used with seed {0}. Next time it will use a new one {1}. It won't be possible to repeat this exact execution.", baseSeed, newSeed);
                    generator = new ThreadLocal<Random>(() => new Random(GetSeedForThread()), true);
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
                if(generator.Values.Count == 0)
                {
                    Logger.Log(LogLevel.Info, "Pseudorandom Number Generator was created with seed: {0}", baseSeed);
                }
                return generator.Value;
            }
        }

        private ThreadLocal<Random> generator;
        private readonly object locker;

        private static int baseSeed = new Random().Next();
    }
}

