//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using NUnit.Framework;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.UnitTests.Utilities;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class PseudorandomNumberGeneratorTests
    {
        [Test]
        public void ShouldGenerateNumbersDeterministically()
        {
            var result1 = new List<int>();
            var result2 = new List<int>();

            generator.ResetSeed(Seed);

            for (var i = 0; i < 10; ++i)
            {
                result1.Add((int)tester.Execute(thread1, () => generator.Next()).ShouldFinish().Result);
            }

            for (var i = 0; i < 10; ++i)
            {
                result2.Add((int)tester.Execute(thread2, () => generator.Next()).ShouldFinish().Result);
            }

            CollectionAssert.AreEqual(thread1Results, result1);
            CollectionAssert.AreEqual(thread2Results, result2);
        }

        [Test]
        public void ShouldGenerateNumbersDeterministicallyMultithreaded()
        {
            var result1 = new List<int>();
            var result2 = new List<int>();

            generator.ResetSeed(Seed);

            for (var i = 0; i < 10; ++i)
            {
                result1.Add((int)tester.Execute(thread1, () => generator.Next()).ShouldFinish().Result);
                result2.Add((int)tester.Execute(thread2, () => generator.Next()).ShouldFinish().Result);
            }

            CollectionAssert.AreEqual(thread1Results, result1);
            CollectionAssert.AreEqual(thread2Results, result2);
        }

        [Test]
        public void ShouldHandleSerialization()
        {
            int test1Result = 0, test2Result = 0;
            var generatorCopy = Serializer.DeepClone(generator);

            test1Result = (int)tester.Execute(thread1, () => generator.Next()).ShouldFinish().Result;
            test2Result = (int)tester.Execute(thread1, () => generatorCopy.Next()).ShouldFinish().Result;

            Assert.AreEqual(test1Result, test2Result);
        }

        [OneTimeSetUp]
        public void SetUp()
        {
            thread1 = tester.ObtainThread(Thread1Name);
            thread2 = tester.ObtainThread(Thread2Name);
            generator.ResetSeed(Seed);

            for (var i = 0; i < 10; ++i)
            {
                thread1Results.Add((int)tester.Execute(thread1, () => generator.Next()).ShouldFinish().Result);
            }

            for (var i = 0; i < 10; ++i)
            {
                thread2Results.Add((int)tester.Execute(thread2, ()  => generator.Next()).ShouldFinish().Result);
            }
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            tester.Finish();
            tester.Dispose();
        }

        private ThreadSyncTester.TestThread thread1;
        private ThreadSyncTester.TestThread thread2;

        private readonly ThreadSyncTester tester = new ThreadSyncTester();
        private readonly List<int> thread1Results = new List<int>();
        private readonly List<int> thread2Results = new List<int>();
        private readonly PseudorandomNumberGenerator generator = new PseudorandomNumberGenerator();

        private const int Seed = 0xC0DE;
        private const string Thread1Name = "Thread1";
        private const string Thread2Name = "Thread2";
    }
}