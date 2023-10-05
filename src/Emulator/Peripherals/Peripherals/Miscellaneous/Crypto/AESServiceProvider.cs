//
// Copyright (c) 2010-2023 Antmicro
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
using System.Linq;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class AESServiceProvider
    {
        public AESServiceProvider(InternalMemoryManager manager, IBusController bus)
        {
            this.manager = manager;
            this.bus = bus;

            segment = new byte[SegmentSize];
        }

        public void PerformAESOperation()
        {
            manager.TryReadDoubleWord((long)AESRegisters.SegmentCount, out var segmentCount);
            manager.TryReadDoubleWord((long)AESRegisters.IterationCount, out var iterationCount);

            var keyByteCount = 0;
            switch(iterationCount)
            {
                case (uint)IterationCount.AES128:
                    keyByteCount = KeyLengthByteCountAES128;
                    break;
                case (uint)IterationCount.AES192:
                case (uint)IterationCount.AES256:
                    keyByteCount = KeyLengthByteCountAES256;
                    break;
                default:
                    Logger.Log(LogLevel.Error, "Encountered unsupported number of iterations, falling back to default key size for AES128.");
                    keyByteCount = KeyLengthByteCountAES128;
                    break;
            }

            manager.TryReadBytes((long)AESRegisters.Key, keyByteCount, out var keyBytes);
            manager.TryReadBytes((long)AESRegisters.InitVector, InitializationVectorByteCount, out var ivBytes);
            manager.TryReadBytes((long)AESRegisters.InputData, (SegmentSize * (int)segmentCount), out var inputBytes);
            
            manager.TryReadDoubleWord((long)AESRegisters.Config, out var config);
            var operation = BitHelper.IsBitSet(config, 0)
                    ? Operation.Decryption
                    : Operation.Encryption;

            var result = GetResultBytesCBC(keyBytes, ivBytes, inputBytes, operation);
            manager.TryWriteBytes((long)AESRegisters.Cipher, result);
        }

        public void PerformAESOperationDMA()
        {
            manager.TryReadDoubleWord((long)AESRegisters.SegmentCount, out var segmentCount);
            manager.TryReadBytes((long)AESRegisters.Key, KeyLengthByteCountAES256, out var keyBytes);
            manager.TryReadBytes((long)AESRegisters.InitVectorDMA, InitializationVectorByteCount, out var ivBytes);
            manager.TryReadDoubleWord((long)AESRegisters.InputDataAddrDMA, out var inputDataAddr);
            manager.TryReadDoubleWord((long)AESRegisters.ResultDataAddrDMA, out var resultDataAddr);
            manager.TryReadDoubleWord((long)AESRegisters.ConfigDMA, out var config);
            var operation = BitHelper.IsBitSet(config, 0)
                    ? Operation.Decryption
                    : Operation.Encryption;
            var mode = BitHelper.IsBitSet(config, 3)
                    ? Mode.CTR
                    : Mode.CBC;
            var inputBytes = bus.ReadBytes(inputDataAddr, SegmentSize);
            switch(mode)
            {
                case Mode.CTR:
                    bus.WriteBytes(GetResultBytesCTR(keyBytes, ivBytes, inputBytes, operation), resultDataAddr);
                    break;
                default:
                    bus.WriteBytes(GetResultBytesCBC(keyBytes, ivBytes, inputBytes, operation), resultDataAddr);
                    break;
            }
        }

        public void Reset()
        {
            manager.ResetMemories();
            segment = new byte[SegmentSize];
        }

        private byte[] GetResultBytesCTR(byte[] keyBytes, byte[] ivBytes, byte[] inputBytes, Operation operation)
        {
            Debug.Assert(keyBytes.Length >= SegmentSize, "Length of key bytes should at least ${SegmentSize}");
            Debug.Assert(inputBytes.Length >= SegmentSize, "Length of input bytes should at least ${SegmentSize}");
            Debug.Assert(inputBytes.Length % SegmentSize == 0, "Length of input bytes should be a multiple of ${SegmentSize}");
            var segment = ProcessSegment(keyBytes.Take(SegmentSize).ToArray(), ivBytes, new byte[SegmentSize], operation);
            for(var i = 0; i < segment.Length; ++i)
            {
                segment[i] ^= inputBytes[i];
            }
            return segment;
        }

        private byte[] GetResultBytesCBC(byte[] keyBytes, byte[] ivBytes, byte[] inputBytes, Operation operation)
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
            using(var aes = Aes.Create())
            {
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                aes.IV = iv;
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

        private readonly IBusController bus;
        private readonly InternalMemoryManager manager;

        // These values are taken directly from the driver because they are not written to the internal memories
        private const int KeyLengthByteCountAES256 = 32;
        private const int KeyLengthByteCountAES128 = 16;
        private const int InitializationVectorByteCount = 16;
        private const int SegmentSize = 16;
        
        private enum IterationCount
        {
            AES128 = 10,
            AES192 = 12,
            AES256 = 14
        }

        private enum Operation
        {
            Encryption,
            Decryption
        }

        private enum Mode
        {
            CBC,
            CTR
        }

        private enum AESRegisters
        {
            SegmentCount = 0x8,
            IterationCount = 0xC,
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
