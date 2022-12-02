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

            var n = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.Modulus, modulusLength);
            var e = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.Exponent, exponentLength);
            var operand = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, baseAddress, modulusLength);

            var resultBytes = BigInteger.ModPow(operand, e, n).ToByteArray();
            AthenaX5200_BigIntegerHelper.StoreBigIntegerBytes(manager, modulusLength, resultBytes, baseAddress);
        }

        public void ModularReduction()
        {
            manager.TryReadDoubleWord((long)RSARegisters.ReductionModulusLength, out var modulusLength);
            manager.TryReadDoubleWord((long)RSARegisters.ReductionOperandLength, out var operandLength);

            var n = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.Modulus, modulusLength);
            var a = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.Operand, operandLength);
            
            var resultBytes = (a % n).ToByteArray();
            AthenaX5200_BigIntegerHelper.StoreBigIntegerBytes(manager, modulusLength, resultBytes, (long)RSARegisters.Operand);
        }

        public void DecryptData()
        {
            manager.TryReadDoubleWord((long)RSARegisters.DecryptionModulusLength, out var modulusLength);

            var n = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.N, modulusLength * 2);

            // Step 1:
            // m1 = c^dp mod p
            var c = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.Cipher, modulusLength * 2);
            var dp = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.DP, modulusLength);
            var p = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.P, modulusLength);
            var m1 = BigInteger.ModPow(c, dp, p);

            // Step 2:
            // m2 = c^dq mod q
            var dq = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.DQ, modulusLength);
            var q = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.Q, modulusLength);
            var m2 = BigInteger.ModPow(c, dq, q);

            // Step 3:
            // h = (qInv * (m1 - m2)) mod p
            var qInv = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)RSARegisters.QInverted, modulusLength);
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
            AthenaX5200_BigIntegerHelper.StoreBigIntegerBytes(manager, (uint)mBytes.Length, mBytes, (long)RSARegisters.BaseAddress);
        }

        public void Reset()
        {
            manager.ResetMemories();
        }

        private readonly InternalMemoryManager manager;
        
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
