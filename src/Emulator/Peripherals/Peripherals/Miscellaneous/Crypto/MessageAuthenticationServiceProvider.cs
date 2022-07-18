//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Antmicro.Renode.Peripherals.Miscellaneous.Crypto
{
    public class MessageAuthenticationServiceProvider
    {
        public MessageAuthenticationServiceProvider(InternalMemoryManager manager, SystemBus bus)
        {
            this.manager = manager;
            this.bus = bus;
        }

        public void PerformSHA()
        {
            // Message length is taken directly from the driver because it is not written to the internal memories
            manager.TryReadBytes((long)MsgAuthRegisters.HashInput, SHAMsgLeng, out var hashInput);
            Misc.EndiannessSwapInPlace(hashInput, WordSize);

            manager.TryWriteBytes((long)MsgAuthRegisters.HashResult, GetHashedBytes(SHAMsgLeng, hashInput));
        }

        public void PerformSHADMA()
        {
            manager.TryReadDoubleWord((long)MsgAuthRegisters.SHADataBytesToProcess, out var hashInputLength);
            manager.TryReadDoubleWord((long)MsgAuthRegisters.SHAExternalDataLocation, out var hashInputAddr);
            var bytes = bus.ReadBytes(hashInputAddr, (int)hashInputLength);
            
            manager.TryReadDoubleWord((long)MsgAuthRegisters.SHAExternalDataResultLocation, out var hashResultLocation);
            bus.WriteBytes(GetHashedBytes((int)hashInputLength, bytes), hashResultLocation);
        }

        public void PerformHMACSHA()
        {
            manager.TryReadDoubleWord((long)MsgAuthRegisters.SHADMAChannelConfig, out var dmaConfig);
            switch((WriteType)dmaConfig)
            {
                case WriteType.Direct:
                    NonDMAHMACSHA();
                    break;
                case WriteType.DMA:
                    DMAHMACSHA();
                    break;
                default:
                    Logger.Log(LogLevel.Warning, "Encountered unexpected DMA configuration: 0x{0:X}", dmaConfig);
                    break;
            }
        }

        public void PerformGCMMessageAuthentication()
        {
            manager.TryReadDoubleWord((long)MsgAuthRegisters.DMAChannelConfig, out var dmaConfig);
            switch((WriteType)dmaConfig)
            {
                case WriteType.Direct:
                    NonDMAGCM();
                    break;
                case WriteType.DMA:
                    DMAGCM();
                    break;
                default:
                    Logger.Log(LogLevel.Warning, "Encountered unexpected DMA configuration: 0x{0:X}", dmaConfig);
                    break;
            }
        }

        private byte[] GetHashedBytes(int count, byte[] input)
        {
            var result = new byte[count];
            using (SHA256 sha256Hash = SHA256.Create())
            {
                result = sha256Hash.ComputeHash(input);
            }
            return result;
        }

        private void NonDMAHMACSHA()
        {
            manager.TryReadDoubleWord((long)MsgAuthRegisters.HMACSHAKeyByteCount, out var keyLength);
            manager.TryReadBytes((long)MsgAuthRegisters.HashMACKey, (int)keyLength, out var hashKey);
            Misc.EndiannessSwapInPlace(hashKey, WordSize);

            // Message length is taken directly from the driver because it is not written to the internal memories
            var msgBytesLength = HMACSHAMsgLeng;

            var msgBytesAddend = (msgBytesLength % 4);
            if(msgBytesAddend != 0)
            {
                msgBytesAddend = 4 - msgBytesAddend;
                msgBytesLength += msgBytesAddend;
            }                
            manager.TryReadBytes((long)MsgAuthRegisters.HashMACInput, (int)msgBytesLength, out var msgBytes, WordSize);
            if(msgBytesAddend != 0)
            {
                msgBytes = msgBytes.Take((int)(msgBytesLength - msgBytesAddend)).ToArray();
            }

            var myhmacsha256 = new HMACSHA256(hashKey);
            var stream = new MemoryStream(msgBytes);
            var result = myhmacsha256.ComputeHash(stream);
            manager.TryWriteBytes((long)MsgAuthRegisters.HashResult, result);
        }

        private void DMAHMACSHA()
        {
            manager.TryReadDoubleWord((long)MsgAuthRegisters.HMACSHAKeyByteCount, out var keyLength);
            manager.TryReadBytes((long)MsgAuthRegisters.HashMACKey, (int)keyLength, out var hashKey);
            Misc.EndiannessSwapInPlace(hashKey, WordSize);

            manager.TryReadDoubleWord((long)MsgAuthRegisters.SHADataBytesToProcess, out var hashInputLength);
            manager.TryReadDoubleWord((long)MsgAuthRegisters.SHAExternalDataLocation, out var hashInputAddr);
            var inputBytes = bus.ReadBytes(hashInputAddr, (int)hashInputLength);

            var myhmacsha256 = new HMACSHA256(hashKey);
            var stream = new MemoryStream(inputBytes);
            var result = myhmacsha256.ComputeHash(stream);
            
            manager.TryReadDoubleWord((long)MsgAuthRegisters.SHAExternalDataResultLocation, out var hashResultLocation);
            bus.WriteBytes(result, hashResultLocation);
        }

        private void NonDMAGCM()
        {
            manager.TryReadDoubleWord((long)MsgAuthRegisters.AuthDataByteCount, out var authBytesLength);
            manager.TryReadBytes((long)MsgAuthRegisters.AuthData, (int)authBytesLength, out var authBytes, WordSize);
            
            manager.TryReadDoubleWord((long)MsgAuthRegisters.InputDataByteCount, out var msgBytesLength);
            var msgBytesAddend = (msgBytesLength % 4);
            if(msgBytesAddend != 0)
            {
                msgBytesAddend = 4 - msgBytesAddend;
                msgBytesLength += msgBytesAddend;
            }
            
            manager.TryReadBytes((long)MsgAuthRegisters.Message, (int)msgBytesLength, out var msgBytes, WordSize);
            
            if(msgBytesAddend != 0)
            {
                msgBytes = msgBytes.Take((int)(msgBytesLength - msgBytesAddend)).ToArray();
            }

            CalculateGCM(msgBytes, authBytes, MsgAuthRegisters.InitVector, out var ciphertext, out var tag);

            manager.TryWriteBytes((long)MsgAuthRegisters.Message, ciphertext);
            // We clear the next 4 bytes after the ciphertext to remove any unwanted data written by
            // previous steps of the algorithm.
            manager.TryWriteBytes((long)MsgAuthRegisters.Message + ciphertext.Length, new byte[] { 0, 0, 0, 0 });
            manager.TryWriteBytes((long)MsgAuthRegisters.Tag, tag);
        }

        private void DMAGCM()
        {
            manager.TryReadDoubleWord((long)MsgAuthRegisters.KeyWordCount, out var msgByteCount);
            manager.TryReadDoubleWord((long)MsgAuthRegisters.PointerToExternalData, out var inputDataAddr);
            var msgBytes = bus.ReadBytes(inputDataAddr, (int)msgByteCount);

            manager.TryReadDoubleWord((long)MsgAuthRegisters.AuthDataByteCount, out var authByteCount);
            manager.TryReadDoubleWord((long)MsgAuthRegisters.PointerToExternalAuthData, out var authDataAddr);
            var authBytes = bus.ReadBytes(authDataAddr, (int)authByteCount);

            CalculateGCM(msgBytes, authBytes, MsgAuthRegisters.InitVectorDMA, out var ciphertext, out var tag);

            manager.TryReadDoubleWord((long)MsgAuthRegisters.PointerToExternalResultLocation, out var resultAddr);
            bus.WriteBytes(ciphertext, resultAddr);
            manager.TryReadDoubleWord((long)MsgAuthRegisters.PointerToExternalMACLocation, out var macAddr);
            bus.WriteBytes(tag, macAddr);
        }
        
        private void CalculateGCM(byte[] msgBytesSwapped, byte[] authBytesSwapped, MsgAuthRegisters initVector, out byte[] ciphertext, out byte[] tag)
        {
            var initVectorSize = GCMInitVectorSize128;

            manager.TryReadBytes((long)MsgAuthRegisters.Key, 32, out var keyBytesSwapped, WordSize);
            
            manager.TryReadBytes((long)initVector, initVectorSize, out var ivBytesSwapped, WordSize);
            // If the user has entered an initialization vector ending with [0x0, 0x0, 0x0, 0x1] (1 being the youngest byte)
            // it means that it is in fact a 96bit key that was padded with these special bytes, to be 128bit.
            // Unfortunately, we have to manually trim the padding here because BouncyCastle is expecting an unpadded
            // initialization vector.
            // See: https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-38d.pdf, p.15 for further details.
            if((ivBytesSwapped[12] == 0) && (ivBytesSwapped[13] == 0) && (ivBytesSwapped[14] == 0) && (ivBytesSwapped[15] == 1))
            {
                ivBytesSwapped = ivBytesSwapped.Take(12).ToArray();
                initVectorSize = GCMInitVectorSize96;
            }

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(keyBytesSwapped), (initVectorSize * 8), ivBytesSwapped, authBytesSwapped);
            cipher.Init(true, parameters);

            var encryptedBytes = new byte[msgBytesSwapped.Length + initVectorSize];

            var retLen = cipher.ProcessBytes(msgBytesSwapped, 0, msgBytesSwapped.Length, encryptedBytes, 0);
            cipher.DoFinal(encryptedBytes, retLen);

            ciphertext = new byte[msgBytesSwapped.Length];
            tag = new byte[initVectorSize];
            
            Buffer.BlockCopy(encryptedBytes, 0, ciphertext, 0, msgBytesSwapped.Length);
            Buffer.BlockCopy(encryptedBytes, msgBytesSwapped.Length, tag, 0, initVectorSize);
        }

        private const int GCMInitVectorSize128 = 16;
        private const int GCMInitVectorSize96 = 12;
        private const int WordSize = 4; // in bytes
        private const int HMACSHAMsgLeng = 34;
        private const int SHAMsgLeng = 32;
        
        private readonly SystemBus bus;
        private readonly InternalMemoryManager manager;
        
        private enum WriteType
        {
            Direct = 0x0,
            DMA = 0x8
        }

        private enum MsgAuthRegisters
        {
            KeyWordCount = 0x8,
            InputDataByteCount = 0x8,
            HMACSHAKeyByteCount = 0x18,
            SHADMAChannelConfig = 0x2C,
            AuthDataByteCount = 0x30,
            SHADataBytesToProcess = 0x44,
            SHAExternalDataLocation = 0x48,
            SHAExternalDataResultLocation = 0x4C,
            DMAChannelConfig = 0x58,
            PointerToExternalData = 0x60,
            PointerToExternalAuthData = 0x64,
            PointerToExternalResultLocation = 0x68,
            PointerToExternalMACLocation = 0x6C,
            OperationAuthBlock = 0x7C,
            TagWordCount = 0x1054,
            HashResult = 0x8064,
            InitVector = 0x807C,
            InitVectorDMA = 0x8080,
            HashMACKey = 0x80A4,
            HashInput = 0x80A8,
            AuthData = 0x80CC,
            Message = 0x80DC,
            Tag = 0x811C,
            HashMACInput = 0x81A4,
            Key = 0x9000
        }
    }
}
