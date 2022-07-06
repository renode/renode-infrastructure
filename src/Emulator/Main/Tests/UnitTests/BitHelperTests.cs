//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using NUnit.Framework;

namespace Antmicro.Renode.Utilities
{
    [TestFixture]
    public class BitHelperTests
    {
        [Test]
        public void ShouldReverseBitsByByte()
        {
            Assert.AreEqual(0x00, BitHelper.ReverseBits((byte)0x00));
            Assert.AreEqual(0x00, BitHelper.ReverseBitsByByte(0x00));
            Assert.AreEqual(0x4D, BitHelper.ReverseBits((byte)0xB2));            
            Assert.AreEqual(0x1A2B3C4D, BitHelper.ReverseBitsByByte(0x58D43CB2));
        }

        [Test]
        public void ShouldReverseBitsByWord()
        {
            Assert.AreEqual(0x00, BitHelper.ReverseBits((ushort)0x00));
            Assert.AreEqual(0x00, BitHelper.ReverseBitsByWord(0x00));
            Assert.AreEqual(0x3C4D, BitHelper.ReverseBits((ushort)0xB23C));
            Assert.AreEqual(0x1A2B3C4D, BitHelper.ReverseBitsByWord(0xD458B23C));
        }

        [Test]
        public void ShouldReverseDoubleWordBits()
        {
            Assert.AreEqual(0x00, BitHelper.ReverseBits((uint)0x00));
            Assert.AreEqual(0x1A2B3C4D, BitHelper.ReverseBits((uint)0xB23CD458));
        }

        [Test]
        public void ShouldReverseQuadrupleWordBits()
        {
            Assert.AreEqual(0x00, BitHelper.ReverseBits((ulong)0x00));
            Assert.AreEqual(0x1A2B3C4D5E6F7A8B, BitHelper.ReverseBits((ulong)0xD15EF67AB23CD458));
        }
    }
}
