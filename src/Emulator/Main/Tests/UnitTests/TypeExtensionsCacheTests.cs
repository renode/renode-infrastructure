//
// Copyright (c) 2010-2019 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using System.Reflection;
using Antmicro.Renode.Utilities.Collections;
using System.Linq;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    [TestFixture]
    public class TypeExtensionsCacheTests
    {
        [SetUp]
        public void Init()
        {
            var cacheField = typeof(TypeExtensions).GetField("cache", BindingFlags.Static | BindingFlags.NonPublic);
            var cache = cacheField.GetValue(null);
            Cache = (SimpleCache)cache;
        }

        [TestCaseSource("testCases")]
        [Test]
        public void TypeExtensionsCacheTest(Tuple<int, Action> tuple)
        {
            Cache.ClearCache();
            var shouldMiss = tuple.Item1;
            var action = tuple.Item2;
            action();
            action();
            Assert.AreEqual(Cache.CacheMisses, shouldMiss);
        }

        [Test]
        public void AreAllPublicMethodTested()
        {
            var methodsCounts = typeof(TypeExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static).Count();
            Assert.AreEqual(methodsCounts, testCases.Count);
        }

        private static readonly List<Tuple<int, Action>> testCases = new List<Tuple<int, Action>>
        {
            new Tuple<int, Action>(1, () => methodInfo.IsStatic()),
            new Tuple<int, Action>(2, () => methodInfo.IsCallable()), //first condition in method is false, so it does not check IsBaseCallable -> so the score is 2 not 3
            new Tuple<int, Action>(3, () => propertyInfo.IsCallable()),
            new Tuple<int, Action>(4, () => fieldInfo.IsCallable()), //recursive call of IsTypeConvertible inside this method, so it's 4 instead 3
            new Tuple<int, Action>(2, () => propertyInfo.IsCallableIndexer()), //first condition in method is false, so it does not check IsBaseCallable -> so the score is 2 not 3
            new Tuple<int, Action>(2, () => methodInfo.IsExtensionCallable()), //first condition in method is false, so it does not check IsTypeConvertible -> so the score is 2 not 3
            new Tuple<int, Action>(1, () => propertyInfo.IsCurrentlyGettable(BindingFlags.Default)),
            new Tuple<int, Action>(1, () => propertyInfo.IsCurrentlySettable(BindingFlags.Default)),
            new Tuple<int, Action>(1, () => methodInfo.IsExtension()),
        };

        private static MethodInfo methodInfo = typeof(String).GetMethods().First(x => x.Name == "Equals");
        private static PropertyInfo propertyInfo = typeof(String).GetProperties().First(x => x.Name == "Length");
        private static FieldInfo fieldInfo = typeof(String).GetFields().First(x => x.Name == "Empty");
        private static SimpleCache Cache;
    }
}

