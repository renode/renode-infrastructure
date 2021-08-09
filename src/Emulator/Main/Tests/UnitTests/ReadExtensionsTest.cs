//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Peripherals.Bus;
using NUnit.Framework;
using Moq;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class ReadExtensionsTest
    {
        [SetUp]
        public void SetUp()
        {
            var bytePeriMock = new Mock<IBytePeripheral>();
            bytePeriMock.Setup(x => x.ReadByte(0)).Returns(0x12);
            bytePeriMock.Setup(x => x.ReadByte(1)).Returns(0x34);
            bytePeriMock.Setup(x => x.ReadByte(2)).Returns(0x56);
            bytePeriMock.Setup(x => x.ReadByte(3)).Returns(0x78);
            bytePeriMock.Setup(x => x.ReadByte(4)).Returns(0x90);
            bytePeriMock.Setup(x => x.ReadByte(5)).Returns(0xAA);
            bytePeriMock.Setup(x => x.ReadByte(6)).Returns(0xBB);
            bytePeriMock.Setup(x => x.ReadByte(7)).Returns(0xCC);
            bytePeripheral = bytePeriMock.Object;

            var wordPeriMock = new Mock<IWordPeripheral>();
            wordPeriMock.Setup(x => x.ReadWord(0)).Returns(0x3412);
            wordPeriMock.Setup(x => x.ReadWord(2)).Returns(0x7856);
            wordPeriMock.Setup(x => x.ReadWord(4)).Returns(0xAA90);
            wordPeriMock.Setup(x => x.ReadWord(6)).Returns(0xCCBB);
            wordPeripheral = wordPeriMock.Object;

            var dwordPeriMock = new Mock<IDoubleWordPeripheral>();
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x78563412);
            dwordPeriMock.Setup(x => x.ReadDoubleWord(4)).Returns(0xCCBBAA90);
            dwordPeripheral = dwordPeriMock.Object;
        }

        [Test]
        public void ShouldReadByteUsingWord()
        {
            Assert.AreEqual(0x12, wordPeripheral.ReadByteUsingWord(0));
            Assert.AreEqual(0x34, wordPeripheral.ReadByteUsingWord(1));
            Assert.AreEqual(0x56, wordPeripheral.ReadByteUsingWord(2));
            Assert.AreEqual(0x78, wordPeripheral.ReadByteUsingWord(3));
        }

        [Test]
        public void ShouldReadByteUsingWordBigEndian()
        {
            Assert.AreEqual(0x34, wordPeripheral.ReadByteUsingWordBigEndian(0));
            Assert.AreEqual(0x12, wordPeripheral.ReadByteUsingWordBigEndian(1));
            Assert.AreEqual(0x78, wordPeripheral.ReadByteUsingWordBigEndian(2));
            Assert.AreEqual(0x56, wordPeripheral.ReadByteUsingWordBigEndian(3));
        }

        [Test]
        public void ShouldReadByteUsingDoubleWord()
        {
            Assert.AreEqual(0x12, dwordPeripheral.ReadByteUsingDword(0));
            Assert.AreEqual(0x34, dwordPeripheral.ReadByteUsingDword(1));
            Assert.AreEqual(0x56, dwordPeripheral.ReadByteUsingDword(2));
            Assert.AreEqual(0x78, dwordPeripheral.ReadByteUsingDword(3));
        }

        [Test]
        public void ShouldReadByteUsingDoubleWordBigEndian()
        {
            Assert.AreEqual(0x78, dwordPeripheral.ReadByteUsingDwordBigEndian(0));
            Assert.AreEqual(0x56, dwordPeripheral.ReadByteUsingDwordBigEndian(1));
            Assert.AreEqual(0x34, dwordPeripheral.ReadByteUsingDwordBigEndian(2));
            Assert.AreEqual(0x12, dwordPeripheral.ReadByteUsingDwordBigEndian(3));
        }

        [Test]
        public void ShouldReadWordUsingByte()
        {
            Assert.AreEqual(0x3412, bytePeripheral.ReadWordUsingByte(0));
            Assert.AreEqual(0x7856, bytePeripheral.ReadWordUsingByte(2));
        }

        [Test]
        public void ShouldReadWordUsingByteBigEndian()
        {
            Assert.AreEqual(0x1234, bytePeripheral.ReadWordUsingByteBigEndian(0));
            Assert.AreEqual(0x5678, bytePeripheral.ReadWordUsingByteBigEndian(2));
        }

        [Test]
        public void ShouldReadWordUsingByteNotAligned()
        {
            Assert.AreEqual(0x5634, bytePeripheral.ReadWordUsingByte(1));
            Assert.AreEqual(0x9078, bytePeripheral.ReadWordUsingByte(3));
        }

        [Test]
        public void ShouldReadWordUsingByteNotAlignedBigEndian()
        {
            Assert.AreEqual(0x3456, bytePeripheral.ReadWordUsingByteBigEndian(1));
            Assert.AreEqual(0x7890, bytePeripheral.ReadWordUsingByteBigEndian(3));
        }

        [Test]
        public void ShouldReadWordUsingDoubleWord()
        {
            Assert.AreEqual(0x3412, dwordPeripheral.ReadWordUsingDword(0));
            Assert.AreEqual(0x7856, dwordPeripheral.ReadWordUsingDword(2));
        }

        [Test]
        public void ShouldReadWordUsingDoubleWordBigEndian()
        {
            Assert.AreEqual(0x5678, dwordPeripheral.ReadWordUsingDwordBigEndian(0));
            Assert.AreEqual(0x1234, dwordPeripheral.ReadWordUsingDwordBigEndian(2));
        }

        [Test]
        public void ShouldReadDoubleWordUsingByte()
        {
            Assert.AreEqual(0x78563412, bytePeripheral.ReadDoubleWordUsingByte(0));
        }

        [Test]
        public void ShouldReadDoubleWordUsingByteBigEndian()
        {
            Assert.AreEqual(0x12345678, bytePeripheral.ReadDoubleWordUsingByteBigEndian(0));
        }

        [Test]
        public void ShouldReadDoubleWordUsingByteNotAligned()
        {
            Assert.AreEqual(0x90785634, bytePeripheral.ReadDoubleWordUsingByte(1));
        }

        [Test]
        public void ShouldReadDoubleWordUsingByteNotAlignedBigEndian()
        {
            Assert.AreEqual(0x34567890, bytePeripheral.ReadDoubleWordUsingByteBigEndian(1));
        }

        [Test]
        public void ShouldReadDoubleWordUsingWord()
        {
            Assert.AreEqual(0x78563412, wordPeripheral.ReadDoubleWordUsingWord(0));
        }

        [Test]
        public void ShouldReadDoubleWordUsingWordBigEndian()
        {
            Assert.AreEqual(0x12345678, wordPeripheral.ReadDoubleWordUsingWordBigEndian(0));
        }

        private IBytePeripheral bytePeripheral;
        private IWordPeripheral wordPeripheral;
        private IDoubleWordPeripheral dwordPeripheral;
    }




}

