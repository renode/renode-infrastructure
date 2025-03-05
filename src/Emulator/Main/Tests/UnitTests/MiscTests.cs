//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using NUnit.Framework;
using System;

namespace Antmicro.Renode.Utilities
{
    [TestFixture]
    public class MiscTests
    {
        [Test]
        public void ShouldGetBytesFromHexString()
        {
            Assert.AreEqual(new byte[] {0x0a}, Misc.HexStringToByteArray("0a"));
            Assert.AreEqual(
                new byte[] {0xab, 0xcd, 0xef, 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89}, 
                Misc.HexStringToByteArray("abcdefABCDEF0123456789")
            );
            Assert.AreEqual(new byte[] {0x0a, 0xb1}, Misc.HexStringToByteArray("0aB1"));
            Assert.AreEqual(new byte[] {0xb1, 0x0a}, Misc.HexStringToByteArray("0aB1", reverse: true));
            Assert.AreEqual(new byte[] {0x00, 0xab, 0x15}, Misc.HexStringToByteArray("00aB15"));
            Assert.AreEqual(new byte[] {0x15, 0xab, 0x00}, Misc.HexStringToByteArray("00aB15", reverse: true));
            Assert.AreEqual(new byte[] {0xab}, Misc.HexStringToByteArray("a\tb", ignoreWhitespace: true));
            Assert.AreEqual(new byte[] {0xab}, Misc.HexStringToByteArray("a\t \nb", ignoreWhitespace: true));
            Assert.AreEqual(new byte[] {0xab, 0xcd}, Misc.HexStringToByteArray("ab cd", ignoreWhitespace: true));
            Assert.AreEqual(new byte[] {0xcd, 0xab}, Misc.HexStringToByteArray("ab cd", ignoreWhitespace: true, reverse: true));
            Assert.AreEqual(new byte[] {0xab, 0xcd, 0xef}, Misc.HexStringToByteArray("abc def", ignoreWhitespace: true));
            Assert.AreEqual(new byte[] {0xef, 0xcd, 0xab}, Misc.HexStringToByteArray("abc def", ignoreWhitespace: true, reverse: true));

            Assert.Throws<FormatException>(
                () => {
                    Misc.HexStringToByteArray("ab3");
                }
            );
            Assert.Throws<FormatException>(
                () => {
                    Misc.HexStringToByteArray("ab cd");
                }
            );
            Assert.Throws<FormatException>(
                () => {
                    Misc.HexStringToByteArray("abc def");
                }
            );
            Assert.Throws<FormatException>(
                () => {
                    Misc.HexStringToByteArray("x");
                }
            );
            Assert.Throws<FormatException>(
                () => {
                    Misc.HexStringToByteArray("xx");
                }
            );
        }
    }
}
