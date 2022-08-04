//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;
using System;
using System.Linq;
using System.Numerics;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class RSAServiceProvider
    {
        public RSAServiceProvider(InternalMemoryManager manager)
        {
            this.manager = manager;
        }

        public void ModularExponentation()
        {
            manager.TryReadDoubleWord((long)RSARegisters.ExponentationModulusLength, out var modulusLength);
            manager.TryReadDoubleWord((long)RSARegisters.ExponentLength, out var exponentLength);
            var baseAddress = (long)RSARegisters.BaseAddress + exponentLength * 4;

            var n = CreateBigInteger(RSARegisters.Modulus, modulusLength);
            var e = CreateBigInteger(RSARegisters.Exponent, exponentLength);

            var operand = CreateBigInteger((RSARegisters)baseAddress, modulusLength);
            var resultBytes = BigInteger.ModPow(operand, e, n).ToByteArray();

            StoreResultBytes(modulusLength, resultBytes, baseAddress);
        }

        public void ModularReduction()
        {
            manager.TryReadDoubleWord((long)RSARegisters.ReductionModulusLength, out var modulusLength);
            manager.TryReadDoubleWord((long)RSARegisters.ReductionOperandLength, out var operandLength);

            var n = CreateBigInteger(RSARegisters.Modulus, modulusLength);
            var a = CreateBigInteger(RSARegisters.Operand, operandLength);
            var resultBytes = (a % n).ToByteArray();

            StoreResultBytes(modulusLength, resultBytes, (long)RSARegisters.Operand);
        }

        public void DecryptData()
        {
            manager.TryReadDoubleWord((long)RSARegisters.DecryptionModulusLength, out var modulusLength);

            var n = CreateBigInteger(RSARegisters.N, modulusLength * 2);

            // Step 1:
            // m1 = c^dp mod p
            var c = CreateBigInteger(RSARegisters.Cipher, modulusLength * 2);
            var dp = CreateBigInteger(RSARegisters.DP, modulusLength);
            var p = CreateBigInteger(RSARegisters.P, modulusLength);
            var m1 = BigInteger.ModPow(c, dp, p);

            // Step 2:
            // m2 = c^dq mod q
            var dq = CreateBigInteger(RSARegisters.DQ, modulusLength);
            var q = CreateBigInteger(RSARegisters.Q, modulusLength);
            var m2 = BigInteger.ModPow(c, dq, q);

            // Step 3:
            // h = (qInv * (m1 - m2)) mod p
            var qInv = CreateBigInteger(RSARegisters.QInverted, modulusLength);
            var x = m1 - m2;
            // We add in an extra p here to keep x positive
            // example: https://www.di-mgt.com.au/crt_rsa.html
            while(x < 0)
            {
                x += p;
            }
            var h = (qInv * x) % p;

            // Step 4:
            // m = m2 + h * q
            var mBytes = (m2 + h * q).ToByteArray();

            Misc.EndiannessSwapInPlace(mBytes, WordSize);
            manager.TryWriteBytes((long)RSARegisters.BaseAddress, mBytes);
        }

        public void Reset()
        {
            manager.ResetMemories();
        }

        private BigInteger CreateBigInteger(RSARegisters register, uint wordCount)
        {
            manager.TryReadBytes((long)register + ((wordCount - 1) * 4), 1, out var b);
            // All calculations require positive values, so we are verifying the sign here:
            // if the highest bit of the first byte is set, we effectively add a zero at
            // the beginning of the array, so the data can be interpreted as a positive value.
            var shouldHavePadding = b[0] >= 0x80;
            manager.TryReadBytes((long)register, (int)(wordCount * 4), out var bytesRead, 4);
            if(shouldHavePadding)
            {
                var wordBytesLength = shouldHavePadding ? wordCount * 4 + 1 : wordCount * 4;
                var bytesReadPadded = new byte[wordBytesLength];
                Array.Copy(bytesRead, 0, bytesReadPadded, 0, (int)(wordCount * 4));
                return new BigInteger(bytesReadPadded);
            }
            return new BigInteger(bytesRead);
        }

        private void StoreResultBytes(uint length, byte[] resultBytes, long baseAddress)
        {
            var byteCount = (int)(length * WordSize);
            if(resultBytes.Length > byteCount)
            {
                // BigInteger.ToByteArray might return an array with an extra element
                // to indicate the sign of resulting value; we need to get rid of it here
                resultBytes = resultBytes.Take(byteCount).ToArray();
            }

            Misc.EndiannessSwapInPlace(resultBytes, WordSize);
            manager.TryWriteBytes(baseAddress, resultBytes);
        }

        private readonly InternalMemoryManager manager;

        private const int WordSize = 4; // in bytes
        
        private enum RSARegisters
        {
            ReductionOperandLength = 0x8,
            ReductionModulusLength = 0xC,
            ExponentationModulusLength = 0x10,
            Operand = 0x10,
            ExponentLength = 0x14,
            Exponent = 0x18,
            BaseAddress = 0x18,
            DecryptionModulusLength = 0x30,
            Cipher = 0x1e4,
            DP = 0x4f0,
            DQ = 0x5b0,
            QInverted = 0x670,
            Modulus = 0x1000,
            P = 0x1348,
            Q = 0x14cc,
            N = 0x1650,
        }
    }
}
