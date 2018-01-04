//
// Copyright (c) 2010-2018 Antmicro
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
        public void ShouldWriteDoubleWordUsingWord()
        {
            var wordPeripheral = wordPeriMock.Object;
            wordPeripheral.WriteDoubleWordUsingWord(0, 0x78563412);
            wordPeriMock.Verify(x => x.WriteWord(0, 0x3412));
            wordPeriMock.Verify(x => x.WriteWord(2, 0x7856));
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
        public void ShouldWriteWordUsingDoubleWordNotAligned1()
        {
            var dwordPeripheral = dwordPeriMock.Object;
            PrepareOldData();
            dwordPeripheral.WriteWordUsingDword(1, 0xDFEF);
            dwordPeriMock.Verify(x => x.WriteDoubleWord(0, 0x78DFEF12), Times.Once());
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
    }
}
