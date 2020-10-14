//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities.Collections;
using System.Linq;
using NUnit.Framework;

namespace Antmicro.Renode.UnitTests.Collections
{
    public class WeakMultiTableTest
    {
        [Test]
        public void ShouldHandleManyRightValuesForLeft()
        {
            var table = new WeakMultiTable<int, int>();
            table.Add(0, 5);
            table.Add(0, 6);
            Assert.AreEqual(2, table.GetAllForLeft(0).Count());
        }

        [Test]
        public void ShouldHandleManyLeftValuesForRight()
        {
            var table = new WeakMultiTable<int, int>();
            table.Add(5, 0);
            table.Add(6, 0);
            Assert.AreEqual(2, table.GetAllForRight(0).Count());
        }

        [Test,Ignore("Ignored")]
        public void ShouldRemovePair()
        {
            var table = new WeakMultiTable<int, int>();
            table.Add(0, 0);
            table.Add(1, 1);

            Assert.AreEqual(1, table.GetAllForLeft(0).Count());
            Assert.AreEqual(1, table.GetAllForLeft(1).Count());

            table.RemovePair(0, 0);

            Assert.AreEqual(0, table.GetAllForLeft(0).Count());
            Assert.AreEqual(1, table.GetAllForLeft(1).Count());
        }

        [Test]
        public void ShouldHandleRemoveOfUnexistingItem()
        {
            var table = new WeakMultiTable<int, int>();

            Assert.AreEqual(0, table.GetAllForLeft(0).Count());
            table.RemovePair(0, 0);
            Assert.AreEqual(0, table.GetAllForLeft(0).Count());
        }

        [Test,Ignore("Ignored")]
        public void ShouldHoldWeakReference()
        {
            if (GC.MaxGeneration == 0)
            {
                Assert.Inconclusive("Not working on boehm");
            }

            var table = new WeakMultiTable<NotSoWeakClass, int>();
            var wr = GenerateWeakReferenceAndInsertIntoTable(table);

            // In theory one GC.Collect() call should be sufficient;
            // Ubuntu32 with mono 4.8.1 shows that this is just a theory ;)
            // My tests showed that calling it twice is enough, but
            // in order to be on a safe side I decided to round it up to 10.
            // I know it is not the prettiest solution, but:
            //   (a) it's just a test,
            //   (b) it works fine on every other setup,
            //   (c) we are not responsible for the code of GC.
            for(var i = 0; i < 10 && wr.IsAlive; i++)
            {
                GC.Collect();
            }

            Assert.IsFalse(wr.IsAlive);
        }

        private WeakReference GenerateWeakReferenceAndInsertIntoTable(WeakMultiTable<NotSoWeakClass, int> table)
        {
            var item = new NotSoWeakClass();
            table.Add(item, 0);
            return new WeakReference(item);
        }

        private class NotSoWeakClass
        {
        }
    }
}

