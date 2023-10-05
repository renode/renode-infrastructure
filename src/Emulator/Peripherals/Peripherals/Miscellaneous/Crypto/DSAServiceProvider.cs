//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class DSAServiceProvider
    {
        public DSAServiceProvider(InternalMemoryManager manager, IBusController bus)
        {
            this.manager = manager;
            this.bus = bus;
        }

        public void SignDMA()
        {
            // The driver is encoding data length as one doubleword: ((uiN<<16) | uiL))
            manager.TryReadDoubleWord((long)DSARegisters.DataLengthEncoded, out var dataLengthEncoded);
            var uiL = dataLengthEncoded & 0xFFFF;
            var uiN = dataLengthEncoded >> 16;

            var g = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)DSARegisters.G, uiL);
            var k = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)DSARegisters.K, uiN);
            var p = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)DSARegisters.P, uiL);
            var q = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)DSARegisters.Q, uiN);
            var x = AthenaX5200_BigIntegerHelper.CreateBigIntegerFromMemory(manager, (long)DSARegisters.X, uiN);

            // Step 1:
            // r = (g^k mod p) mod q
            var r = BigInteger.ModPow(g, k, p) % q;
            var rBytes = r.ToByteArray();
            AthenaX5200_BigIntegerHelper.StoreBigIntegerBytes(manager, (uint)rBytes.Length, rBytes, (long)DSARegisters.R);

            manager.TryReadDoubleWord((long)DSARegisters.MessageLenth, out var msgLength);
            manager.TryReadDoubleWord((long)DSARegisters.MessageExternalAddress, out var msgExternalAddress);
            var msgBytes = bus.ReadBytes(msgExternalAddress, (int)msgLength);
            // We could use SHA384.HashData static method, but it's not available in Mono.
            var hash = SHA384.Create().ComputeHash(msgBytes);
            
            var z = AthenaX5200_BigIntegerHelper.CreateBigInteger(hash, Math.Min(q.ToByteArray().Length, hash.Length));

            // Step 2:
            // s = (k^-1(z + xr)) mod q
            var inverseK = AthenaX5200_BigIntegerHelper.CalculateModularInverse(k, q);

            var S = (inverseK * (z + (x * r))) % q;
            var sBytes = S.ToByteArray();
            AthenaX5200_BigIntegerHelper.StoreBigIntegerBytes(manager, (uint)sBytes.Length, sBytes, (long)DSARegisters.S);
        }

        private readonly IBusController bus;
        private readonly InternalMemoryManager manager;

        private enum DSARegisters
        {
            R = 0x20,
            S = 0x40,
            MessageLenth = 0x44,
            MessageExternalAddress = 0x48,
            G = 0x1348,
            K = 0x14CC,
            X = 0x14F0,
            Q = 0x1000,
            P = 0x1044,
            DataLengthEncoded = 0x2410
        }
    }
}
