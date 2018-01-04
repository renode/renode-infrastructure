//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Core
{
    public class PseudorandomNumberGenerator
    {
        public PseudorandomNumberGenerator()
        {
            instanceSeed = new Random().Next();
            locker = new object();
        }

        public void ResetSeed(int newSeed)
        {
            lock(locker)
            {
                if(generator != null)
                {
                    Logger.Log(LogLevel.Warning, "Pseudorandom Number Generator has already been used with seed {0}. Next time it will use a new one {1}. It won't be possible to repeat this exact execution.", instanceSeed, newSeed);
                    generator = null;
                }
                instanceSeed = newSeed;
            }
        }

        public int GetCurrentSeed()
        {
            return instanceSeed;
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

        private Random GetOrCreateGenerator()
        {
            lock(locker)
            {
                if(generator == null)
                {
                    generator = new Random(instanceSeed);
                    Logger.Log(LogLevel.Info, "Pseudorandom Number Generator was created with seed: {0}", instanceSeed);
                }
                return generator;
            }
        }

        private int instanceSeed;
        private Random generator;

        private readonly object locker;
    }
}

