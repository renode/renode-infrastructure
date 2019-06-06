//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using Antmicro.Renode.Utilities.Collections;
namespace Antmicro.Renode.Utilities
{
    [TestFixture]
    public class SimpleCacheTests
    {
        [SetUp]
        public void Init()
        {
            obj1 = new object();
            obj2 = new object();
        }

        [Test]
        public void ShouldNotMissCache()
        {
            var result = cache.Get(obj1, Generator1);
            var beforeMisses = cache.CacheMisses;
            var beforeHits = cache.CacheHits;
            var resultAfter = cache.Get(obj1, Generator1);
            var afterMisses = cache.CacheMisses;
            var afterHits = cache.CacheHits;
            Assert.AreEqual(beforeMisses, afterMisses);
            Assert.AreEqual(beforeHits + 1, afterHits);
            Assert.AreEqual(result, resultAfter);
        }

        [Test]
        public void ShouldNotMissCacheWithTwoParameters()
        {
            var result = cache.Get(obj1, obj2, Generator3);
            var beforeMisses = cache.CacheMisses;
            var beforeHits = cache.CacheHits;
            var resultAfter = cache.Get(obj1, obj2, Generator3);
            var afterMisses = cache.CacheMisses;
            var afterHits = cache.CacheHits;
            Assert.AreEqual(beforeHits + 1, afterHits);
            Assert.AreEqual(beforeMisses, afterMisses);
            Assert.AreEqual(result, resultAfter);
        }

        [Test]
        public void ShouldNotMixParametersAndGenerators() //in one test to make sure none of those duplicate in the cache
        {
            var misses1 = cache.CacheMisses;
            var hits1 = cache.CacheHits;
            cache.Get(obj2, Generator1);
            var misses2 = cache.CacheMisses;
            var hits2 = cache.CacheHits;
            Assert.AreEqual(misses1 + 1, misses2);
            Assert.AreEqual(hits1, hits2);
            cache.Get(obj2, Generator2);
            var misses3 = cache.CacheMisses;
            var hits3 = cache.CacheHits;
            Assert.AreEqual(hits2, hits3);
            Assert.AreEqual(misses2 + 1, misses3);
            cache.Get(obj1, Generator2);
            var misses4 = cache.CacheMisses;
            var hits4 = cache.CacheHits;
            Assert.AreEqual(hits3, hits4);
            Assert.AreEqual(misses3 + 1, misses4);
            cache.Get(obj1, obj2, Generator3);
            var misses5 = cache.CacheMisses;
            var hits5 = cache.CacheHits;
            Assert.AreEqual(misses4 + 1, misses5);
            Assert.AreEqual(hits4, hits5);
            cache.Get(obj1, obj2, Generator4);
            var misses6 = cache.CacheMisses;
            var hits6 = cache.CacheHits;
            Assert.AreEqual(hits5, hits6);
            Assert.AreEqual(misses5 + 1, misses6);
        }

        [Test]
        public void ShouldHandleNullsAsParameter()
        {
            int? val = null;
            var result = "";
            var resultAfter = "";
            var cacheMisses = 0UL;
            var cacheMissesAfter = 0UL;
            var cacheHits = 0UL;
            var cacheHitsAfter = 0UL;

            Assert.DoesNotThrow(() => 
            { 
                result = cache.Get(val, NullParameterGenerator);
                cacheMisses = cache.CacheMisses;
                cacheHits = cache.CacheHits;
                resultAfter = cache.Get(val, NullParameterGenerator);
                cacheMissesAfter = cache.CacheMisses;
                cacheHitsAfter = cache.CacheHits;
            });

            Assert.AreEqual(result, resultAfter);
            Assert.AreEqual(cacheMisses, cacheMissesAfter);
            Assert.AreEqual(cacheHits + 1, cacheHitsAfter);
        }

        [Test]
        public void ShouldHandleNullsAsParameters()
        {
            int? val = null;
            int? val2 = null;
            var result = "";
            var resultAfter = "";
            var cacheMisses = 0UL;
            var cacheMissesAfter = 0UL;
            var cacheHits = 0UL;
            var cacheHitsAfter = 0UL;

            Assert.DoesNotThrow(() =>
            {
                result = cache.Get(val, val2, NullParametersGenerator);
                cacheMisses = cache.CacheMisses;
                cacheHits = cache.CacheHits;
                resultAfter = cache.Get(val, val2, NullParametersGenerator);
                cacheMissesAfter = cache.CacheMisses;
                cacheHitsAfter = cache.CacheHits;
            });

            Assert.AreEqual(result, resultAfter);
            Assert.AreEqual(cacheMisses, cacheMissesAfter);
            Assert.AreEqual(cacheHits + 1, cacheHitsAfter);
        }

        [Test]
        public void ShouldHandleNullableAsParameter()
        {
            int? val = null;
            var result = "";
            var resultAfter = "";
            var cacheMisses = 0UL;
            var cacheMissesAfter = 0UL;
            var cacheHits = 0UL;
            var cacheHitsAfter = 0UL;

            Assert.DoesNotThrow(() =>
            {
                result = cache.Get(val, NullParameterGenerator);
                cacheMisses = cache.CacheMisses;
                cacheHits = cache.CacheHits;
            });

            val = 0;

            Assert.DoesNotThrow(() =>
            {
                resultAfter = cache.Get(val, NullParameterGenerator);
                cacheMissesAfter = cache.CacheMisses;
                cacheHitsAfter = cache.CacheHits;
            });

            Assert.AreNotEqual(result, resultAfter);
            Assert.AreEqual(cacheMisses + 1, cacheMissesAfter);
            Assert.AreEqual(cacheHits, cacheHitsAfter);
        }

        [Test]
        public void ShouldHandleNullableAsParameters()
        {
            int? val = null;
            int? val2 = null;
            var result = "";
            var resultAfter = "";
            var resultAfter2 = "";
            var cacheMisses = 0UL;
            var cacheMissesAfter = 0UL;
            var cacheMissesAfter2 = 0UL;
            var cacheHits = 0UL;
            var cacheHitsAfter = 0UL;
            var cacheHitsAfter2 = 0UL;

            Assert.DoesNotThrow(() =>
            {
                result = cache.Get(val, val2, NullParametersGenerator);
                cacheMisses = cache.CacheMisses;
                cacheHits = cache.CacheHits;
                val = 0;
                resultAfter = cache.Get(val, val2, NullParametersGenerator);
                cacheMissesAfter = cache.CacheMisses;
                cacheHitsAfter = cache.CacheHits;
            });

            Assert.AreNotEqual(result, resultAfter);
            Assert.AreEqual(cacheMisses + 1, cacheMissesAfter);
            Assert.AreEqual(cacheHits, cacheHitsAfter);

            Assert.DoesNotThrow(() =>
            {
                val = null;
                val2 = 0;
                resultAfter2 = cache.Get(val, val2, NullParametersGenerator);
                cacheMissesAfter2 = cache.CacheMisses;
                cacheHitsAfter2 = cache.CacheHits;
            });

            Assert.AreNotEqual(resultAfter, resultAfter2);
            Assert.AreEqual(cacheMissesAfter + 1, cacheMissesAfter2);
            Assert.AreEqual(cacheHitsAfter, cacheHitsAfter2);
        }

        [Test]
        public void ShouldNotHandleNullAsAGenerator()
        {
            Assert.Throws<ArgumentNullException>(() => cache.Get(obj1, (Func<object, string>)null));
            Assert.Throws<ArgumentNullException>(() => cache.Get(obj1, obj2, (Func<object, object, string>)null));
        }

        [Test]
        public void ShouldNotMixOrderOfParameteres()
        {
            var result = cache.Get(obj1, obj2, Generator3);
            var misses = cache.CacheMisses;
            var hits = cache.CacheHits;
            var resultAfter = cache.Get(obj2, obj1, Generator3);
            var afterMisses = cache.CacheMisses;
            var afterHits = cache.CacheHits;
            Assert.AreNotEqual(misses, afterMisses);
            Assert.AreEqual(hits, afterHits);
            Assert.AreNotEqual(result, resultAfter);
        }

        [Test]
        public void ShouldClearCache()
        {
            cache.Get(obj1, Generator1);
            cache.Get(obj2, Generator1);
            cache.Get(obj1, Generator2);
            cache.Get(obj2, Generator2);
            Assert.AreEqual(4, cache.CacheSize);
            cache.ClearCache();
            Assert.AreEqual(0, cache.CacheSize); 
        }

        [TearDown]
        public void Cleanup()
        {
            cache.ClearCache();
        }

        private string NullParameterGenerator(int? val)
        {
            return val.HasValue.ToString() + "abcde".GetHashCode();
        }

        private string NullParametersGenerator(int? val, int? val2)
        {
            return val.HasValue.ToString() + val.HasValue.ToString() + "abcde".GetHashCode();
        }

        private string Generator1(object obj)
        {
            return obj.ToString();
        }

        private string Generator2(object obj)
        {
            return obj.GetHashCode() + " this is unit test";
        }

        private string Generator3(object obj, object obj_1)
        {
            return obj.GetHashCode().ToString() + obj_1;
        }

        private string Generator4(object obj, object obj_1)
        {
            return obj.GetHashCode().ToString() + obj_1 + " this is another test";
        }

        private SimpleCache cache = new SimpleCache();
        private object obj1;
        private object obj2;
    }
}
