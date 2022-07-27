//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class AESServiceProvider
    {
        public AESServiceProvider(InternalMemoryManager manager, SystemBus bus)
        {
            this.manager = manager;
            this.bus = bus;

            segment = new byte[SegmentSize];
        }

        public void PerformAESOperation()
        {
            manager.TryReadDoubleWord((long)AESRegisters.SegmentCount, out var segmentCount);

            manager.TryReadBytes((long)AESRegisters.Key, KeyLengthByteCount, out var keyBytes, WordSize);
            manager.TryReadBytes((long)AESRegisters.InitVector, InitializationVectorByteCount, out var ivBytes, WordSize);
            manager.TryReadBytes((long)AESRegisters.InputData, (SegmentSize * (int)segmentCount), out var inputBytes, WordSize);
            
            manager.TryReadDoubleWord((long)AESRegisters.Config, out var config);
            var operation = BitHelper.IsBitSet(config, 0)
                    ? Operation.Decryption
                    : Operation.Encryption;

            var result = GetResultBytes(keyBytes, ivBytes, inputBytes, operation);
            manager.TryWriteBytes((long)AESRegisters.Cipher, result);
        }

        public void PerformAESOperationDMA()
        {
            manager.TryReadDoubleWord((long)AESRegisters.SegmentCount, out var segmentCount);

            manager.TryReadBytes((long)AESRegisters.Key, KeyLengthByteCount, out var keyBytes, WordSize);
            manager.TryReadBytes((long)AESRegisters.InitVectorDMA, InitializationVectorByteCount, out var ivBytes, WordSize);
            manager.TryReadDoubleWord((long)AESRegisters.InputDataAddrDMA, out var inputDataAddr);
            manager.TryReadDoubleWord((long)AESRegisters.ResultDataAddrDMA, out var resultDataAddr);
            manager.TryReadDoubleWord((long)AESRegisters.ConfigDMA, out var config);
            var operation = BitHelper.IsBitSet(config, 0)
                    ? Operation.Decryption
                    : Operation.Encryption;

            var inputBytes = bus.ReadBytes(inputDataAddr, SegmentSize);
            var result = GetResultBytes(keyBytes, ivBytes, inputBytes, operation);
            bus.WriteBytes(result, resultDataAddr);
        }

        public void Reset()
        {
            manager.ResetMemories();
            segment = new byte[SegmentSize];
        }

        private byte[] GetResultBytes(byte[] keyBytes, byte[] ivBytes, byte[] inputBytes, Operation operation)
        {
            var inputSegment = new byte[SegmentSize];
            var result = new byte[SegmentSize];

            Debug.Assert(inputBytes.Length % SegmentSize == 0, "Length of input bytes should be a multiple of ${SegmentSize}");
            var segmentCount = inputBytes.Length / SegmentSize;
            for(var i = 0; i < segmentCount; ++i)
            {
                Array.Copy(inputBytes, (i * SegmentSize), inputSegment, 0, SegmentSize);
                for(var j = 0; j < SegmentSize; ++j)
                {
                    inputSegment[j] ^= segment[j];
                }

                segment = ProcessSegment(keyBytes, ivBytes, inputSegment, operation);
            }
            Array.Copy(segment, 0, result, 0, SegmentSize);

            if(segmentCount == 1)
            {
                segment = new byte[SegmentSize];
            }
            return result;
        }

        private byte[] ProcessSegment(byte[] keyBytes, byte[] ivBytes, byte[] inputBytes, Operation operation)
        {
            return operation == Operation.Encryption
                ? Encrypt(keyBytes, ivBytes, inputBytes)
                : Decrypt(keyBytes, ivBytes, inputBytes);
        }

        private byte[] Encrypt(byte[] key, byte[] iv, byte[] input)
        {                
            using(var ms = new MemoryStream())
            {
                using(var aes = new AesCryptoServiceProvider() { Padding = PaddingMode.None })
                using(var cs = new CryptoStream(ms, aes.CreateEncryptor(key, iv), CryptoStreamMode.Write))
                {
                    cs.Write(input, 0, input.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        private byte[] Decrypt(byte[] key, byte[] iv, byte[] input)
        {
            using(var aes = Aes.Create())
            {
                aes.Padding = PaddingMode.None;
                using(var decryptor = aes.CreateDecryptor(key, iv))
                {
                    return decryptor.TransformFinalBlock(input, 0, input.Length);
                }
            }
        }

        private byte[] segment;

        private readonly SystemBus bus;
        private readonly InternalMemoryManager manager;

        // These values are taken directly from the driver because they are not written to the internal memories
        private const int KeyLengthByteCount = 32;
        private const int InitializationVectorByteCount = 16;
        private const int SegmentSize = 16;
        private const int WordSize = 4; // in bytes

        private enum Operation
        {
            Encryption,
            Decryption
        }

        private enum AESRegisters
        {
            SegmentCount = 0x8,
            Config = 0x20,
            InputDataAddrDMA = 0x28,
            ResultDataAddrDMA = 0x2C,
            ConfigDMA = 0x3C,
            InitVector = 0x8024,
            InitVectorDMA = 0x8040,
            Cipher = 0x8048,
            InputData = 0x8058,
            Key = 0x9000,
        }
    }
}
