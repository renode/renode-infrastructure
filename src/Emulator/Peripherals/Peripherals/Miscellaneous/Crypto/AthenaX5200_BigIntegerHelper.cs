//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Numerics;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public static class AthenaX5200_BigIntegerHelper
    {
        public static BigInteger CreateBigInteger(byte[] bytes, int byteCount)
        {
            if(bytes[0] >= 0x80)
            {
                var temp = new byte[bytes.Length + 1];
                Array.Copy(bytes, 0, temp, 1, bytes.Length);
                bytes = temp;
            }
            return new BigInteger(bytes.Take(byteCount).Reverse().ToArray());
        }

        public static BigInteger CreateBigIntegerFromMemory(InternalMemoryManager manager, long register, uint wordCount)
        {
            manager.TryReadBytes((long)register + ((wordCount - 1) * 4), 1, out var b);
            // All calculations require positive values, so we are verifying the sign here:
            // if the highest bit of the first byte is set, we effectively add a zero at
            // the beginning of the array, so the data can be interpreted as a positive value.
            var shouldHavePadding = b[0] >= 0x80;
            manager.TryReadBytes(register, (int)(wordCount * 4), out var bytesRead);

            if((long)register < LittleEndianStartingAddress)
            {
                // BigIntegers are expecting Little-Endian data, but internal memories in range <0x0000 - 0x7FFF> are Big-Endian
                Misc.EndiannessSwapInPlace(bytesRead, WordSize);
            }
            if(shouldHavePadding)
            {
                var wordBytesLength = shouldHavePadding ? wordCount * 4 + 1 : wordCount * 4;
                var bytesReadPadded = new byte[wordBytesLength];
                Array.Copy(bytesRead, 0, bytesReadPadded, 0, (int)(wordCount * 4));
                return new BigInteger(bytesReadPadded);
            }
            return new BigInteger(bytesRead);
        }

        public static void StoreBigIntegerBytes(InternalMemoryManager manager, uint length, byte[] resultBytes, long baseAddress)
        {
            var byteCount = (int)(length * WordSize);
            if(resultBytes.Length > byteCount)
            {
                // BigInteger.ToByteArray might return an array with an extra element
                // to indicate the sign of resulting value; we need to get rid of it here
                resultBytes = resultBytes.Take(byteCount).ToArray();
            }
            if(baseAddress < LittleEndianStartingAddress)
            {
                // BigIntegers are containing Little-Endian data, but internal memories in range <0x0000 - 0x7FFF> are Big-Endian
                Misc.EndiannessSwapInPlace(resultBytes, WordSize);
            }
            manager.TryWriteBytes(baseAddress, resultBytes);
        }

        public static BigInteger CalculateModularInverse(BigInteger value, BigInteger modulo)
        {
            BigInteger leftFactor = 0;
            BigInteger mod = modulo;
            BigInteger gcd = 0;
            BigInteger u = 1;

            while(value != 0)
            {
                var q = mod / value;
                var r = mod % value;
                var m = leftFactor - u * q;
                mod = value;
                value = r;
                leftFactor = u;
                u = m;
                gcd = mod;
            }

            if(gcd != 1)
            {
                throw new ArgumentException(string.Format("Invalid modulo: {0} while trying to calculate modular inverse!", modulo));
            }
            if(leftFactor < 0)
            {
                leftFactor += modulo;
            }
            return leftFactor % modulo;
        }

        private const int WordSize = 4; // in bytes
        private const uint LittleEndianStartingAddress = 0x8000;
    }
}
