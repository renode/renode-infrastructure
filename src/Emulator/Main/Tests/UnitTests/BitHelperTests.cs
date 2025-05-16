//
// Copyright (c) 2010-2023 Antmicro
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

        [Test]
        public void ShouldCalculateDoubleWordMask()
        {
            Assert.AreEqual(0x00, BitHelper.CalculateMask(0, 0));
            Assert.AreEqual(0xFFFFFFFF, BitHelper.CalculateMask(32, 0));
            Assert.AreEqual(0xFFFFFFF8, BitHelper.CalculateMask(29, 3));
            Assert.AreEqual(0x7FFFFFFE, BitHelper.CalculateMask(30, 1));
        }

        [Test]
        public void ShouldCalculateQuadWordMask()
        {
            Assert.AreEqual(0x00, BitHelper.CalculateQuadWordMask(0, 0));
            Assert.AreEqual(0xFFFFFFFFFFFFFFFF, BitHelper.CalculateQuadWordMask(64, 0));
            Assert.AreEqual(0xFFFFFFFFFFFFFFF8, BitHelper.CalculateQuadWordMask(61, 3));
            Assert.AreEqual(0x7FFFFFFFFFFFFFFE, BitHelper.CalculateQuadWordMask(62, 1));
        }

        [Test]
        public void ShouldGetDoubleWordMaskedValue()
        {
            Assert.AreEqual(0x00, BitHelper.GetMaskedValue(0, 0, 0));
            Assert.AreEqual(0x00, BitHelper.GetMaskedValue(0x1234, 32, 0));
            Assert.AreEqual(0x1234, BitHelper.GetMaskedValue(0x1234, 0, 13));
            Assert.AreEqual(0xFFFFFFFF, BitHelper.GetMaskedValue(0xFFFFFFFF, 0, 32));
            Assert.AreEqual(0x10, BitHelper.GetMaskedValue(0x38, 4, 1));
        }

        [Test]
        public void ShouldGetQuadWordMaskedValue()
        {
            Assert.AreEqual(0x00, BitHelper.GetMaskedValue(0L, 0, 0));
            Assert.AreEqual(0x00, BitHelper.GetMaskedValue(0x1234L, 32, 0));
            Assert.AreEqual(0x1234, BitHelper.GetMaskedValue(0x1234L, 0, 13));
            Assert.AreEqual(0xFFFFFFFFFFFFFF, BitHelper.GetMaskedValue(0xFFFFFFFFFFFFFFL, 0, 64));
            Assert.AreEqual(0x10, BitHelper.GetMaskedValue(0x38L, 4, 1));
        }

        [Test]
        public void ShouldSetDoubleWordMaskedValue()
        {
            uint value = 0x00;
            BitHelper.SetMaskedValue(ref value, 0, 0, 0);
            Assert.AreEqual(0x00, value);
            BitHelper.SetMaskedValue(ref value, 0x1234, 32, 0);
            Assert.AreEqual(0x00, value);
            BitHelper.SetMaskedValue(ref value, 0x1234, 0, 13);
            Assert.AreEqual(0x1234, value);
            BitHelper.SetMaskedValue(ref value, 0xFFFFFFFF, 0, 32);
            Assert.AreEqual(0xFFFFFFFF, value);
            BitHelper.SetMaskedValue(ref value, 0x28, 4, 1);
            Assert.AreEqual(0xFFFFFFEF, value);
        }

        [Test]
        public void ShouldSetQuadWordMaskedValue()
        {
            ulong value = 0x00;
            BitHelper.SetMaskedValue(ref value, 0, 0, 0);
            Assert.AreEqual(0x00, value);
            BitHelper.SetMaskedValue(ref value, 0x1234, 32, 0);
            Assert.AreEqual(0x00, value);
            BitHelper.SetMaskedValue(ref value, 0x1234, 0, 13);
            Assert.AreEqual(0x1234, value);
            BitHelper.SetMaskedValue(ref value, 0xFFFFFFFFFFFFFFF, 0, 64);
            Assert.AreEqual(0xFFFFFFFFFFFFFFF, value);
            BitHelper.SetMaskedValue(ref value, 0x28, 4, 1);
            Assert.AreEqual(0xFFFFFFFFFFFFFEF, value);
        }
    }
}
