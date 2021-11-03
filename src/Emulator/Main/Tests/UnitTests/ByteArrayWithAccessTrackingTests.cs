//
// Copyright (c) 2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Utilities;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class ByteArrayWithAccessTrackingTests
    {
        [SetUp]
        public void Setup()
        {
            testArray = new ByteArrayWithAccessTracking(new StubPeripheral(), 4, 4, "TestArray");
        }

        [Test]
        public void ShouldAllowOnlyAccessWithWidthEqual4()
        {
            Assert.Throws<ArgumentException>(() => new ByteArrayWithAccessTracking(new StubPeripheral(), 4, 3, "WrongAccesWdithArray"));
            Assert.Throws<ArgumentException>(() => new ByteArrayWithAccessTracking(new StubPeripheral(), 4, 2, "WrongAccesWdithArray"));
            Assert.Throws<ArgumentException>(() => new ByteArrayWithAccessTracking(new StubPeripheral(), 4, 1, "WrongAccesWdithArray"));
        }

        [Test]
        public void HandleAccessingInputDataThroughRegisters()
        {
            var firstWrite  = new byte[] { 0x11, 0x22, 0x44, 0x88, 0x10, 0x20, 0x40, 0x80, 0x81, 0x42, 0x24, 0x18, 0x01, 0x02, 0x04, 0x08 };
            var secondWrite = new byte[] { 0x11, 0x22, 0x44, 0x88, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x81, 0x42, 0x24, 0x18 };

            // Assert data are written with correct endianness (it should be reverted)
            testArray.SetPart(0, 0x88442211);
            testArray.SetPart(1, 0x80402010);
            testArray.SetPart(2, 0x18244281);
            testArray.SetPart(3, 0x08040201);
            Assert.AreEqual(testArray.RetriveData(), firstWrite);

            testArray.SetPart(0, 0x88442211);
            testArray.SetPart(1, 0x0);
            testArray.SetPart(2, 0x0);
            testArray.SetPart(3, 0x18244281);
            Assert.AreEqual(testArray.RetriveData(), secondWrite);

        }

        [Test]
        public void ShouldKnowIfNotAllPartsWritten()
        {
            Assert.AreEqual(false, testArray.AllDataWritten);
            testArray.SetPart(0, 0x0);
            Assert.AreEqual(false, testArray.AllDataWritten);
            testArray.SetPart(1, 0x0);
            Assert.AreEqual(false, testArray.AllDataWritten);
            testArray.SetPart(2, 0x0);
            Assert.AreEqual(false, testArray.AllDataWritten);
        }

        [Test]
        public void ShouldNotRetriveDataUntilAllPartsWritten()
        {
            testArray.SetPart(0, 0x0);
            testArray.SetPart(1, 0x0);
            testArray.SetPart(2, 0x0);
            Assert.AreEqual(new byte[0], testArray.RetriveData());
        }

        [Test]
        public void ShouldKnowIfAllPartsWritten()
        {
            testArray.SetPart(0, 0x0);
            testArray.SetPart(1, 0x0);
            testArray.SetPart(2, 0x0);
            testArray.SetPart(3, 0x0);

            Assert.AreEqual(true, testArray.AllDataWritten);
        }

        [Test]
        public void ShouldRetriveDataWhenAllWritten()
        {
            testArray.SetPart(0, 0x0);
            testArray.SetPart(1, 0x0);
            testArray.SetPart(2, 0x0);
            testArray.SetPart(3, 0x0);
            Assert.AreNotEqual(new byte[0], testArray.RetriveData());
        }

        [Test]
        public void ShouldBeAbleToPreserveTrackingWhenRetrivingData()
        {
            testArray.SetPart(0, 0x0);
            testArray.SetPart(1, 0x0);
            testArray.SetPart(2, 0x0);
            testArray.SetPart(3, 0x0);
            testArray.RetriveData(trackAccess: false);
            Assert.AreEqual(true, testArray.AllDataWritten);
        }

        [Test]
        public void ShouldClearAccessTrackingAfterGettingNewData()
        {
            testArray.SetPart(0, 0x0);
            testArray.SetPart(1, 0x0);
            testArray.SetPart(2, 0x0);
            testArray.SetPart(3, 0x0);
            // New data coming after all parts have been written
            testArray.SetPart(3, 0x0);
            Assert.AreEqual(false, testArray.AllDataWritten);
        }

        [Test]
        public void HandleAccessingOutputDataThroughRegisters()
        {
            var data = new byte[] { 0x1, 0x2, 0x4, 0x8, 0x11, 0x22, 0x44, 0x88, 0x10, 0x20, 0x40, 0x80, 0x81, 0x42, 0x24, 0x18 };

            testArray.SetArrayTo(data);

            Assert.AreEqual(0x08040201, testArray.GetPartAsDoubleWord(0));
            Assert.AreEqual(0x88442211, testArray.GetPartAsDoubleWord(1));
            Assert.AreEqual(0x80402010, testArray.GetPartAsDoubleWord(2));
            Assert.AreEqual(0x18244281, testArray.GetPartAsDoubleWord(3));
        }

        [Test]
        public void ShouldThrowWhenSettingArrayWithWrongLength()
        {
            var data = new byte[6];
            Assert.Throws<ArgumentException>(() => testArray.SetArrayTo(data));
        }

        [Test]
        public void ShouldThrowWhenTryingToGetUnexistingPart()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => testArray.SetPart(17, 0x0));
        }

        private ByteArrayWithAccessTracking testArray;

        private class StubPeripheral : IPeripheral
        {
            public StubPeripheral()
            {
            }

            public void Reset()
            {
            }
        }
    }
}

