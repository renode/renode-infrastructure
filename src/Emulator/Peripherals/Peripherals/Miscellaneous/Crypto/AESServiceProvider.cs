//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using System.Security.Cryptography;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class AESServiceProvider
    {
        public AESServiceProvider(InternalMemoryManager manager, SystemBus bus)
        {
            this.manager = manager;
            this.bus = bus;
        }

        public void PerformAESOperation()
        {
            manager.TryReadBytes((long)AESRegisters.Key, KeyLengthByteCount, out var keyBytes);
            manager.TryReadBytes((long)AESRegisters.InitVector, InitializationVectorByteCount, out var ivBytes);
            manager.TryReadBytes((long)AESRegisters.InputData, InputDataByteCount, out var inputBytes);
            manager.TryReadDoubleWord((long)AESRegisters.Config, out var config);
            var operation = BitHelper.IsBitSet(config, 0)
                    ? Operation.Decryption
                    : Operation.Encryption;

            // The memory in which Key bytes are stored is little-endian, but both Encryptor and Decryptor require big-endian data
            Misc.EndiannessSwapInPlace(keyBytes, WordSize);
            var result = GetResultBytes(keyBytes, ivBytes, inputBytes, operation);

            manager.TryWriteBytes((long)AESRegisters.Cipher, result);
        }

        public void PerformAESOperationDMA()
        {
            manager.TryReadBytes((long)AESRegisters.Key, KeyLengthByteCount, out var keyBytes);
            manager.TryReadBytes((long)AESRegisters.InitVectorDMA, InitializationVectorByteCount, out var ivBytes);
            manager.TryReadDoubleWord((long)AESRegisters.InputDataAddrDMA, out var inputDataAddr);
            manager.TryReadDoubleWord((long)AESRegisters.ResultDataAddrDMA, out var resultDataAddr);
            manager.TryReadDoubleWord((long)AESRegisters.ConfigDMA, out var config);
            var operation = BitHelper.IsBitSet(config, 0)
                    ? Operation.Decryption
                    : Operation.Encryption;

            // The memory in which Key bytes are stored is little-endian, but both Encryptor and Decryptor require big-endian data
            Misc.EndiannessSwapInPlace(keyBytes, WordSize);
            bus.WriteBytes(GetResultBytes(keyBytes, ivBytes, bus.ReadBytes(inputDataAddr, CipherByteCount), operation), resultDataAddr);
        }

        private byte[] GetResultBytes(byte[] keyBytes, byte[] ivBytes, byte[] inputBytes, Operation operation)
        {
            return operation == Operation.Encryption
                ? Encryption(keyBytes, ivBytes, inputBytes)
                : Decryption(keyBytes, ivBytes, inputBytes);
        }

        private byte[] Encryption(byte[] key, byte[] iv, byte[] input)
        {                
            using(var aes = Aes.Create())
            using(var encryptor = aes.CreateEncryptor(key, iv))
            {
                return encryptor.TransformFinalBlock(input, 0, input.Length);
            }
        }

        private byte[] Decryption(byte[] key, byte[] iv, byte[] input)
        {
            using(var aes = Aes.Create())
            {
                aes.Padding = PaddingMode.None;
                using(var encryptor = aes.CreateDecryptor(key, iv))
                {
                    return encryptor.TransformFinalBlock(input, 0, input.Length);
                }
            }
        }

        private readonly SystemBus bus;
        private readonly InternalMemoryManager manager;

        // These values are taken directly from the driver because they are not written to the internal memories
        private const int KeyLengthByteCount = 32;
        private const int InitializationVectorByteCount = 16;
        private const int InputDataByteCount = 16;
        private const int CipherByteCount = 16;
        private const int WordSize = 4; // in bytes

        private enum Operation
        {
            Encryption,
            Decryption
        }

        private enum AESRegisters
        {
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
