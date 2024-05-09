//
// Copyright (c) 2010-2024 Antmicro
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

            var qwordPeriMock = new Mock<IQuadWordPeripheral>();
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x7856341221436587);
            qwordPeripheral = qwordPeriMock.Object;
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
            Assert.AreEqual(0x12, dwordPeripheral.ReadByteUsingDoubleWord(0));
            Assert.AreEqual(0x34, dwordPeripheral.ReadByteUsingDoubleWord(1));
            Assert.AreEqual(0x56, dwordPeripheral.ReadByteUsingDoubleWord(2));
            Assert.AreEqual(0x78, dwordPeripheral.ReadByteUsingDoubleWord(3));
        }

        [Test]
        public void ShouldReadByteUsingDoubleWordBigEndian()
        {
            Assert.AreEqual(0x78, dwordPeripheral.ReadByteUsingDoubleWordBigEndian(0));
            Assert.AreEqual(0x56, dwordPeripheral.ReadByteUsingDoubleWordBigEndian(1));
            Assert.AreEqual(0x34, dwordPeripheral.ReadByteUsingDoubleWordBigEndian(2));
            Assert.AreEqual(0x12, dwordPeripheral.ReadByteUsingDoubleWordBigEndian(3));
        }

        [Test]
        public void ShouldReadByteUsingQuadWord()
        {
            Assert.AreEqual(0x87, qwordPeripheral.ReadByteUsingQuadWord(0));
            Assert.AreEqual(0x65, qwordPeripheral.ReadByteUsingQuadWord(1));
            Assert.AreEqual(0x43, qwordPeripheral.ReadByteUsingQuadWord(2));
            Assert.AreEqual(0x21, qwordPeripheral.ReadByteUsingQuadWord(3));
            Assert.AreEqual(0x12, qwordPeripheral.ReadByteUsingQuadWord(4));
            Assert.AreEqual(0x34, qwordPeripheral.ReadByteUsingQuadWord(5));
            Assert.AreEqual(0x56, qwordPeripheral.ReadByteUsingQuadWord(6));
            Assert.AreEqual(0x78, qwordPeripheral.ReadByteUsingQuadWord(7));
        }

        [Test]
        public void ShouldReadByteUsingQuadWordBigEndian()
        {
            Assert.AreEqual(0x78, qwordPeripheral.ReadByteUsingQuadWordBigEndian(0));
            Assert.AreEqual(0x56, qwordPeripheral.ReadByteUsingQuadWordBigEndian(1));
            Assert.AreEqual(0x34, qwordPeripheral.ReadByteUsingQuadWordBigEndian(2));
            Assert.AreEqual(0x12, qwordPeripheral.ReadByteUsingQuadWordBigEndian(3));
            Assert.AreEqual(0x21, qwordPeripheral.ReadByteUsingQuadWordBigEndian(4));
            Assert.AreEqual(0x43, qwordPeripheral.ReadByteUsingQuadWordBigEndian(5));
            Assert.AreEqual(0x65, qwordPeripheral.ReadByteUsingQuadWordBigEndian(6));
            Assert.AreEqual(0x87, qwordPeripheral.ReadByteUsingQuadWordBigEndian(7));
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
            Assert.AreEqual(0x3412, dwordPeripheral.ReadWordUsingDoubleWord(0));
            Assert.AreEqual(0x7856, dwordPeripheral.ReadWordUsingDoubleWord(2));
        }

        [Test]
        public void ShouldReadWordUsingDoubleWordBigEndian()
        {
            Assert.AreEqual(0x5678, dwordPeripheral.ReadWordUsingDoubleWordBigEndian(0));
            Assert.AreEqual(0x1234, dwordPeripheral.ReadWordUsingDoubleWordBigEndian(2));
        }

        [Test]
        public void ShouldReadWordUsingQuadWord()
        {
            Assert.AreEqual(0x6587, qwordPeripheral.ReadWordUsingQuadWord(0));
            Assert.AreEqual(0x2143, qwordPeripheral.ReadWordUsingQuadWord(2));
            Assert.AreEqual(0x3412, qwordPeripheral.ReadWordUsingQuadWord(4));
            Assert.AreEqual(0x7856, qwordPeripheral.ReadWordUsingQuadWord(6));
        }

        [Test]
        public void ShouldReadWordUsingQuadWordBigEndian()
        {
            Assert.AreEqual(0x5678, qwordPeripheral.ReadWordUsingQuadWordBigEndian(0));
            Assert.AreEqual(0x1234, qwordPeripheral.ReadWordUsingQuadWordBigEndian(2));
            Assert.AreEqual(0x4321, qwordPeripheral.ReadWordUsingQuadWordBigEndian(4));
            Assert.AreEqual(0x8765, qwordPeripheral.ReadWordUsingQuadWordBigEndian(6));
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

        [Test]
        public void ShouldReadDoubleWordUsingQuadWord()
        {
            Assert.AreEqual(0x21436587, qwordPeripheral.ReadDoubleWordUsingQuadWord(0));
            Assert.AreEqual(0x78563412, qwordPeripheral.ReadDoubleWordUsingQuadWord(4));
        }

        [Test]
        public void ShouldReadDoubleWordUsingQuadWordBigEndian()
        {
            Assert.AreEqual(0x12345678, qwordPeripheral.ReadDoubleWordUsingQuadWordBigEndian(0));
            Assert.AreEqual(0x87654321, qwordPeripheral.ReadDoubleWordUsingQuadWordBigEndian(4));
        }

        [Test]
        public void ShouldReadQuadWordUsingByte()
        {
            Assert.AreEqual(0xccbbaa9078563412, bytePeripheral.ReadQuadWordUsingByte(0));
        }

        [Test]
        public void ShouldReadQuadWordUsingByteBigEndian()
        {
            Assert.AreEqual(0x1234567890aabbcc, bytePeripheral.ReadQuadWordUsingByteBigEndian(0));
        }

        [Test]
        public void ShouldReadQuadWordUsingByteNotAligned()
        {
            Assert.AreEqual(0xccbbaa90785634, bytePeripheral.ReadQuadWordUsingByte(1));
        }

        [Test]
        public void ShouldReadQuadWordUsingByteNotAlignedBigEndian()
        {
            Assert.AreEqual(0x34567890aabbcc00, bytePeripheral.ReadQuadWordUsingByteBigEndian(1));
        }

        [Test]
        public void ShouldReadQuadWordUsingWord()
        {
            Assert.AreEqual(0xccbbaa9078563412, wordPeripheral.ReadQuadWordUsingWord(0));
        }

        [Test]
        public void ShouldReadQuadWordUsingWordBigEndian()
        {
            Assert.AreEqual(0x1234567890aabbcc, wordPeripheral.ReadQuadWordUsingWordBigEndian(0));
        }

        [Test]
        public void ShouldReadQuadWordUsingDoubleWord()
        {
            Assert.AreEqual(0xccbbaa9078563412, dwordPeripheral.ReadQuadWordUsingDoubleWord(0));
        }

        [Test]
        public void ShouldReadQuadWordUsingDoubleWordBigEndian()
        {
            Assert.AreEqual(0x1234567890aabbcc, dwordPeripheral.ReadQuadWordUsingDoubleWordBigEndian(0));
        }

        private IBytePeripheral bytePeripheral;
        private IWordPeripheral wordPeripheral;
        private IDoubleWordPeripheral dwordPeripheral;
        private IQuadWordPeripheral qwordPeripheral;
    }
}

