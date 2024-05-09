//
// Copyright (c) 2010-2024 Antmicro
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
            dwordPeripheral.WriteByteUsingDoubleWord(0, 0x12);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12);
            dwordPeripheral.WriteByteUsingDoubleWord(1, 0x34);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x3412));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x3412);
            dwordPeripheral.WriteByteUsingDoubleWord(2, 0x56);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x563412));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x563412);
            dwordPeripheral.WriteByteUsingDoubleWord(3, 0x78);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78563412));
        }

        [Test]
        public void ShouldWriteByteUsingDoubleWordBigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteByteUsingDoubleWordBigEndian(0, 0x12);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12000000));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12000000);
            dwordPeripheral.WriteByteUsingDoubleWordBigEndian(1, 0x34);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12340000));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12340000);
            dwordPeripheral.WriteByteUsingDoubleWordBigEndian(2, 0x56);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345600));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12345600);
            dwordPeripheral.WriteByteUsingDoubleWordBigEndian(3, 0x78);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345678));
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWord()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteWordUsingDoubleWord(0, 0x3412);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x3412));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x3412);
            dwordPeripheral.WriteWordUsingDoubleWord(2, 0x7856);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78563412));
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWordBigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteWordUsingDoubleWordBigEndian(0, 0x3412);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12340000));
            dwordPeriMock.Setup(x => x.ReadDoubleWord(0)).Returns(0x12340000);
            dwordPeripheral.WriteWordUsingDoubleWordBigEndian(2, 0x7856);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345678));
        }

        [Test]
        public void ShouldWriteQuadWordUsingDoubleWord()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteQuadWordUsingDoubleWord(0, 0x7856341221436587);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x21436587));
            dwordPeriMock.Verify(x => x.WriteDoubleWord(4, 0x78563412));
        }

        [Test]
        public void ShouldWriteQuadWordUsingDoubleWordBigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            dwordPeripheral.WriteQuadWordUsingDoubleWordBigEndian(0, 0x7856341221436587);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x12345678));
            dwordPeriMock.Verify(x => x.WriteDoubleWord(4, 0x87654321));
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWordNotAligned1()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            PrepareOldData();
            dwordPeripheral.WriteWordUsingDoubleWord(1, 0xDFEF);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78DFEF12), Times.Once());
        }

        [Test]
        public void ShouldWriteWordUsingDoubleWordNotAligned1BigEndian()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            PrepareOldData();
            dwordPeripheral.WriteWordUsingDoubleWordBigEndian(1, 0xDFEF);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78EFDF12), Times.Once());
        }

        [Test]
        public void ShouldWriteByteUsingQuadWord()
        {
            byte[] bytes = {0x12, 0x34, 0x56, 0x78, 0x87, 0x65, 0x43, 0x21};
            var qwordPeripheral = qwordPeriMock.Object;
            ulong act = 0;
            for(var i = 0; i < 7; i++)
            {
                qwordPeripheral.WriteByteUsingQuadWord(i, bytes[i]);
                act |= (ulong)bytes[i] << (8 * i);
                qwordPeriMock.Verify(x => x.WriteQuadWord(0, act));
                qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(act);
            }
        }

        [Test]
        public void ShouldWriteByteUsingQuadWordBigEndian()
        {
            byte[] bytes = {0x12, 0x34, 0x56, 0x78, 0x87, 0x65, 0x43, 0x21};
            var qwordPeripheral = qwordPeriMock.Object;
            ulong act = 0;
            for(var i = 0; i < 7; i++)
            {
                qwordPeripheral.WriteByteUsingQuadWordBigEndian(i, bytes[i]);
                act |= (ulong)bytes[i] << (7 - i) * 8;
                qwordPeriMock.Verify(x => x.WriteQuadWord(0, act));
                qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(act);
            }
        }

        [Test]
        public void ShouldWriteWordUsingQuadWord()
        {
            var qwordPeripheral = qwordPeriMock.Object;
            qwordPeripheral.WriteWordUsingQuadWord(0, 0x3412);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x3412));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x3412);
            qwordPeripheral.WriteWordUsingQuadWord(2, 0x7856);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x78563412));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x78563412);
            qwordPeripheral.WriteWordUsingQuadWord(4, 0x5678);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x567878563412));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x567878563412);
            qwordPeripheral.WriteWordUsingQuadWord(6, 0x1234);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234567878563412));
        }

        [Test]
        public void ShouldWriteWordUsingQuadWordBigEndian()
        {
            var qwordPeripheral = qwordPeriMock.Object;
            qwordPeripheral.WriteWordUsingQuadWordBigEndian(0, 0x3412);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234000000000000));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x1234000000000000);
            qwordPeripheral.WriteWordUsingQuadWordBigEndian(2, 0x7856);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234567800000000));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x1234567800000000);
            qwordPeripheral.WriteWordUsingQuadWordBigEndian(4, 0x6587);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234567887650000));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x1234567887650000);
            qwordPeripheral.WriteWordUsingQuadWordBigEndian(6, 0x2143);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234567887654321));
        }

        [Test]
        public void ShouldWriteDoubleWordUsingQuadWord()
        {
            var qwordPeripheral = qwordPeriMock.Object;
            qwordPeripheral.WriteDoubleWordUsingQuadWord(0, 0x78563412);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x78563412));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x78563412);
            qwordPeripheral.WriteDoubleWordUsingQuadWord(4, 0x12345678);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234567878563412));
        }

        [Test]
        public void ShouldWriteDoubleWordUsingQuadWordBigEndian()
        {
            var qwordPeripheral = qwordPeriMock.Object;
            qwordPeripheral.WriteDoubleWordUsingQuadWordBigEndian(0, 0x78563412);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234567800000000));
            qwordPeriMock.Setup(x => x.ReadQuadWord(0)).Returns(0x1234567800000000);
            qwordPeripheral.WriteDoubleWordUsingQuadWordBigEndian(4, 0x21436587);
            qwordPeriMock.Verify(x => x.WriteQuadWord(0, 0x1234567887654321));
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
