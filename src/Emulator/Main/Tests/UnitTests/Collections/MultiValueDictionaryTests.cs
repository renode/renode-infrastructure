//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using NUnit.Framework;
using System.Collections.Generic;
using Antmicro.Renode.Utilities.Collections;
using System.Linq;

namespace Antmicro.Renode.UnitTests.Collections
{
    [TestFixture]
    public class MultiValueDictionaryTests
    {
        readonly static string[] values1 = { "test1", "test2", "test3", "test4" };
        readonly static string[] values2 = { "dojpa1", "dojpa2", "dojpa3", "dojpa4" };

        [Test]
        public static void ShouldAddRangeAndRetrieveViaIterator()
        {
            var expected = values2.Concat(values1);
            var multiDict = new MultiValueDictionary<string, string>();
            multiDict.AddRange("key2", values2);
            multiDict.AddRange("key1", values1);

            CollectionAssert.AreEqual(expected, multiDict);
        }

        [Test]
        public static void ShouldAddAndTryGetValue()
        {
            var multiDict = new MultiValueDictionary<string, string>();
            multiDict.Add("key", "value");
            IReadOnlyCollection<string> values;

            Assert.IsTrue(multiDict.TryGetValue("key", out values));
            Assert.AreSame("value", values.First());

            foreach(var value in values1)
            {
                multiDict.Add("key2", value);
            }

            Assert.IsTrue(multiDict.TryGetValue("key2", out values));
            CollectionAssert.AreEqual(values1, values);
        }

        [Test]
        public static void ShouldNotGetValue()
        {
            var multiDict = new MultiValueDictionary<string, string>();
            multiDict.Add("key", "value");
            foreach(var value in values1)
            {
                multiDict.Add("key2", value);
            }
            IReadOnlyCollection<string> values;
            Assert.IsFalse(multiDict.TryGetValue("海亀", out values));
        }

        [Test]
        public static void ShouldClear()
        {
            var multiDict = new MultiValueDictionary<string, string>();
            multiDict.AddRange("key2", values2);
            multiDict.AddRange("key1", values1);
            multiDict.Add("key3", "海亀");
            multiDict.Clear();
            IReadOnlyCollection<string> values;
            Assert.IsFalse(multiDict.TryGetValue("海亀", out values));
            Assert.IsFalse(multiDict.TryGetValue("key1", out values));
            Assert.IsFalse(multiDict.TryGetValue("key2", out values));
            Assert.IsFalse(multiDict.TryGetValue("key3", out values));
        }

        [Test]
        public static void ShouldContainValue()
        {
            var multiDict = new MultiValueDictionary<int, string>();
            multiDict.AddRange(100, values2);
            multiDict.AddRange(-5, values1);
            multiDict.Add(1337, "海亀");

            Assert.IsTrue(multiDict.ContainsValue("dojpa2"));
            Assert.IsTrue(multiDict.ContainsValue("海亀"));
            Assert.IsTrue(multiDict.ContainsValue("test4"));
            Assert.IsTrue(multiDict.Contains("海亀"));
            Assert.IsTrue(multiDict.Contains("test1"));
            Assert.IsTrue(multiDict.Contains("dojpa4"));
        }

        [Test]
        public static void ShouldContainKey()
        {
            var multiDict = new MultiValueDictionary<string, string>();
            multiDict.AddRange("key2", values2);
            multiDict.AddRange("key1", values1);
            multiDict.Add("key3", "海亀");

            Assert.IsTrue(multiDict.ContainsKey("key1"));
            Assert.IsTrue(multiDict.ContainsKey("key3"));
            Assert.IsTrue(multiDict.ContainsKey("key2"));
            Assert.IsTrue(multiDict.Contains("key1"));
            Assert.IsTrue(multiDict.Contains("key2"));
            Assert.IsTrue(multiDict.Contains("key3"));
        }

        [Test]
        public static void ShouldContainKeyValue()
        {
            var multiDict = new MultiValueDictionary<string, string>();
            multiDict.AddRange("key2", values2);
            multiDict.AddRange("key1", values1);
            multiDict.Add("key3", "海亀");

            Assert.IsTrue(multiDict.Contains("key2", "dojpa4"));
            Assert.IsTrue(multiDict.Contains("key3", "海亀"));
            Assert.IsTrue(multiDict.Contains("key1", "test1"));
        }
    }
}

