//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Runtime.InteropServices;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Peripherals.Bus;
using NUnit.Framework;
using Moq;

namespace Antmicro.Renode.UnitTests
{
    [TestFixture]
    public class WriteExtensionsTest
    {
        [SetUp]
        public void SetUp()
        {
            bytePeriMock = new Mock<IBytePeripheral>();
            wordPeriMock = new Mock<IWordPeripheral>();
            dwordPeriMock = new Mock<IDoubleWordPeripheral>();
            qwordPeriMock = new Mock<IQuadWordPeripheral>();
        }

        [Test]
        public void ShouldWriteWordUsingByte()
        {
            var bytePeripheral = bytePeriMock.Object;
            bytePeripheral.WriteWordUsingByte(0, 0x3412);
            bytePeripheral.WriteWordUsingByte(2, 0x7856);
            bytePeriMock.Verify(x => x.WriteByte(0, 0x12), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(1, 0x34), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(2, 0x56), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(3, 0x78), Times.Once());
        }

        [Test]
        public void ShouldWriteWordUsingByteBigEndian()
        {
            var bytePeripheral = bytePeriMock.Object;
            bytePeripheral.WriteWordUsingByteBigEndian(0, 0x3412);
            bytePeripheral.WriteWordUsingByteBigEndian(2, 0x7856);
            bytePeriMock.Verify(x => x.WriteByte(0, 0x34), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(1, 0x12), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(2, 0x78), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(3, 0x56), Times.Once());
        }

        [Test]
        public void ShouldWriteDoubleWordUsingByte()
        {
            var bytePeripheral = bytePeriMock.Object;
            bytePeripheral.WriteDoubleWordUsingByte(0, 0x78563412);
            bytePeriMock.Verify(x => x.WriteByte(0, 0x12), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(1, 0x34), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(2, 0x56), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(3, 0x78), Times.Once());
        }

        [Test]
        public void ShouldWriteDoubleWordUsingByteBigEndian()
        {
            var bytePeripheral = bytePeriMock.Object;
            bytePeripheral.WriteDoubleWordUsingByteBigEndian(0, 0x78563412);
            bytePeriMock.Verify(x => x.WriteByte(0, 0x78), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(1, 0x56), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(2, 0x34), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(3, 0x12), Times.Once());
        }

        [Test]
        public void ShouldWriteQuadWordUsingByte()
        {
            var bytePeripheral = bytePeriMock.Object;
            bytePeripheral.WriteQuadWordUsingByte(0, 0x7856341221436587);
            bytePeriMock.Verify(x => x.WriteByte(0, 0x87), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(1, 0x65), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(2, 0x43), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(3, 0x21), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(4, 0x12), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(5, 0x34), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(6, 0x56), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(7, 0x78), Times.Once());
        }

        [Test]
        public void ShouldWriteQuadWordUsingByteBigEndian()
        {
            var bytePeripheral = bytePeriMock.Object;
            bytePeripheral.WriteQuadWordUsingByteBigEndian(0, 0x7856341221436587);
            bytePeriMock.Verify(x => x.WriteByte(0, 0x78), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(1, 0x56), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(2, 0x34), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(3, 0x12), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(4, 0x21), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(5, 0x43), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(6, 0x65), Times.Once());
            bytePeriMock.Verify(x => x.WriteByte(7, 0x87), Times.Once());
        }

        [Test]
        public void ShouldWriteByteUsingWord()
        {
            var wordPeripheral = wordPeriMock.Object;
            wordPeripheral.WriteByteUsingWord(0, 0x12);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x0012));
            wordPeriMock.Setup(x => x.ReadWord(0)).Returns(0x0012);
            wordPeripheral.WriteByteUsingWord(1, 0x34);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x3412));
            wordPeripheral.WriteByteUsingWord(2, 0x56);
            wordPeriMock.Verify(x => x.WriteWord(2, 0x0056));
            wordPeriMock.Setup(x => x.ReadWord(2)).Returns(0x0056);
            wordPeripheral.WriteByteUsingWord(3, 0x78);
            wordPeriMock.Verify(x => x.WriteWord(2, 0x7856));
        }

        [Test]
        public void ShouldWriteByteUsingWordBigEndian()
        {
            var wordPeripheral = wordPeriMock.Object;
            wordPeripheral.WriteByteUsingWordBigEndian(0, 0x12);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x1200));
            wordPeriMock.Setup(x => x.ReadWord(0)).Returns(0x1200);
            wordPeripheral.WriteByteUsingWordBigEndian(1, 0x34);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x1234));
            wordPeripheral.WriteByteUsingWordBigEndian(2, 0x56);
            wordPeriMock.Verify(x => x.WriteWord(2, 0x5600));
            wordPeriMock.Setup(x => x.ReadWord(2)).Returns(0x5600);
            wordPeripheral.WriteByteUsingWordBigEndian(3, 0x78);
            wordPeriMock.Verify(x => x.WriteWord(2, 0x5678));
        }

        [Test]
        public void ShouldWriteDoubleWordUsingWord()
        {
            var wordPeripheral = wordPeriMock.Object;
            wordPeripheral.WriteDoubleWordUsingWord(0, 0x78563412);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x3412));
            wordPeriMock.Verify(x => x.WriteWord(2, 0x7856));
        }

        [Test]
        public void ShouldWriteDoubleWordUsingWordBigEndian()
        {
            var wordPeripheral = wordPeriMock.Object;
            wordPeripheral.WriteDoubleWordUsingWordBigEndian(0, 0x78563412);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x5678));
            wordPeriMock.Verify(x => x.WriteWord(2, 0x1234));
        }

        [Test]
        public void ShouldWriteQuadWordUsingWord()
        {
            var wordPeripheral = wordPeriMock.Object;
            wordPeripheral.WriteQuadWordUsingWord(0, 0x7856341221436587);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x6587));
            wordPeriMock.Verify(x => x.WriteWord(2, 0x2143));
            wordPeriMock.Verify(x => x.WriteWord(4, 0x3412));
            wordPeriMock.Verify(x => x.WriteWord(6, 0x7856));
        }

        [Test]
        public void ShouldWriteQuadWordUsingWordBigEndian()
        {
            var wordPeripheral = wordPeriMock.Object;
            wordPeripheral.WriteQuadWordUsingWordBigEndian(0, 0x7856341221436587);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x5678));
            wordPeriMock.Verify(x => x.WriteWord(2, 0x1234));
            wordPeriMock.Verify(x => x.WriteWord(4, 0x4321));
            wordPeriMock.Verify(x => x.WriteWord(6, 0x8765));
        }

        [Test]
        public void ShouldWriteByteUsingDoubleWord()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteByteUsingDword(0, 0x12);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12);
            dwordPeripheral.WriteByteUsingDword(1, 0x34);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x3412));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x3412);
            dwordPeripheral.WriteByteUsingDword(2, 0x56);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x563412));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x563412);
            dwordPeripheral.WriteByteUsingDword(3, 0x78);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78563412));
        }

        [Test]
        public void ShouldWriteByteUsingDoubleWordBigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteByteUsingDwordBigEndian(0, 0x12);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12000000));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12000000);
            dwordPeripheral.WriteByteUsingDwordBigEndian(1, 0x34);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12340000));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12340000);
            dwordPeripheral.WriteByteUsingDwordBigEndian(2, 0x56);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345600));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12345600);
            dwordPeripheral.WriteByteUsingDwordBigEndian(3, 0x78);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345678));
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWord()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteWordUsingDword(0, 0x3412);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x3412));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x3412);
            dwordPeripheral.WriteWordUsingDword(2, 0x7856);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78563412));
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWordBigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteWordUsingDwordBigEndian(0, 0x3412);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12340000));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12340000);
            dwordPeripheral.WriteWordUsingDwordBigEndian(2, 0x7856);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345678));
        }

        [Test]
        public void ShouldWriteQuadWordUsingDoubleWord()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteQuadWordUsingDword(0, 0x7856341221436587);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x21436587));
            dwordPeriMock.Verify(x => x.WriteDoubleWord(4, 0x78563412));
        }

        [Test]
        public void ShouldWriteQuadWordUsingDoubleWordBigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteQuadWordUsingDwordBigEndian(0, 0x7856341221436587);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345678));
            dwordPeriMock.Verify(x => x.WriteDoubleWord(4, 0x87654321));
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWordNotAligned1()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            PrepareOldData();
            dwordPeripheral.WriteWordUsingDword(1, 0xDFEF);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78DFEF12), Times.Once());
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWordNotAligned1BigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            PrepareOldData();
            dwordPeripheral.WriteWordUsingDwordBigEndian(1, 0xDFEF);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78EFDF12), Times.Once());
        }

        private void PrepareOldData()
        {
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x78563412);
            dwordPeriMock.Setup(x => x.ReadDoubleWord(4)).Returns(0xCCBBAA90);
            wordPeriMock.Setup(x => x.ReadWord(0)).Returns(0x3412);
            wordPeriMock.Setup(x => x.ReadWord(2)).Returns(0x7856);
            wordPeriMock.Setup(x => x.ReadWord(4)).Returns(0xAA90);
            wordPeriMock.Setup(x => x.ReadWord(6)).Returns(0xCCBB);
        }

        private Mock<IBytePeripheral> bytePeriMock;
        private Mock<IWordPeripheral> wordPeriMock;
        private Mock<IDoubleWordPeripheral> dwordPeriMock;
        private Mock<IQuadWordPeripheral> qwordPeriMock;
    }
}
