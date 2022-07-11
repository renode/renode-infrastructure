//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Security.Cryptography;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Macs;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class AthenaX5200 : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize
    {
        public AthenaX5200(Machine machine)
        {
            memoryManager = new InternalMemoryManager();
            RegistersCollection = new DoubleWordRegisterCollection(this);
            rsaServiceProvider = new RSAServiceProvider(memoryManager);
            aesServiceProvider = new AESServiceProvider(memoryManager, machine.SystemBus);
            msgAuthServiceProvider = new MessageAuthenticationServiceProvider(memoryManager, machine.SystemBus);

            Registers.CSR.Define(this)
                .WithFlag(0,
                    writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            Reset();
                        }
                        coreReset = val;
                    },
                    valueProviderCallback: _ => coreReset, name: "RESET")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) =>
                    {
                        if(val)
                        {
                            isCompleted = false;
                        }
                    }, name: "CLEAR_COMPLETE")
                .WithFlag(2, FieldMode.Read, valueProviderCallback: _ => isCompleted, name: "COMPLETE")
                .WithTag("BUSY", 3, 1)
                .WithFlag(4, out coreExecuteCommand, name: "EXECUTE_COMMAND")
                .WithReservedBits(5, 3)
                .WithEnumField(8, 8, out operation)
                .WithReservedBits(16, 3)
                .WithTag("PKX_OFFSET", 19, 1)
                .WithReservedBits(20, 12)
                .WithWriteCallback((_, __) =>
                {
                    if(!coreExecuteCommand.Value)
                    {
                        return;
                    }
                    if(!commands.TryGetValue(operation.Value, out var command))
                    {
                        this.Log(LogLevel.Error, "Unknown command: [{0}].", operation.Value);
                        return;
                    }
                    this.Log(LogLevel.Noisy, "Executing command: [{0}]", operation.Value);
                    command();
                    isCompleted = true;
                    coreExecuteCommand.Value = false;
                });

            commands = new Dictionary<JumpTable, Action>
            {
                { JumpTable.PrecomputeValueRSA, EmptyHandler },
                { JumpTable.InstantiateDRBG, InstantiateDRBG },
                { JumpTable.GenerateBlocksFromDRBG, GenerateBlocksWithDRBG },
                { JumpTable.UninstantiateDRBG, UninstantiateDRBG },
                { JumpTable.ModularExponentationRSA, rsaServiceProvider.ModularExponentation },
                { JumpTable.ModularReductionRSA, rsaServiceProvider.ModularReduction },
                { JumpTable.DecryptCipherRSA, rsaServiceProvider.DecryptData },
                { JumpTable.RunAES, aesServiceProvider.PerformAESOperation },
                { JumpTable.RunAES_DMA, aesServiceProvider.PerformAESOperationDMA },
                { JumpTable.RunGCM, msgAuthServiceProvider.PerformGCMMessageAuthentication },
                { JumpTable.RunGCMNew, msgAuthServiceProvider.PerformGCMMessageAuthentication },
            };

            Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return memoryManager.TryReadDoubleWord(offset, out uint result)
                ? result
                : RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(!memoryManager.TryWriteDoubleWord(offset, value))
            {
                RegistersCollection.Write(offset, value);
            }
        }

        public void Reset()
        {
            memoryManager.ResetMemories();
            RegistersCollection.Reset();
            randomGenerator?.Reset();
            isCompleted = false;
            coreReset = false;
        }

        public long Size => 0x1000000;
        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void EmptyHandler()
        {
            // There are commands in which no processing is needed,
            // but we want to report in the Status Register that it has completed.
        }

        private void InstantiateDRBG()
        {
            if(randomGenerator != null)
            {
                this.Log(LogLevel.Error, "RNG subsystem already instantiated. Aborting!");
                return;
            }
            randomGenerator = new PseudorandomBitGenerator(memoryManager);
        }

        private void GenerateBlocksWithDRBG()
        {
            if(randomGenerator == null)
            {
                this.Log(LogLevel.Error, "RNG subsystem is not instantiated. Aborting!");
                return;
            }
            randomGenerator.Generate();
        }

        private void UninstantiateDRBG()
        {
            if(randomGenerator == null)
            {
                this.Log(LogLevel.Error, "RNG subsystem is not instantiated. Aborting!");
                return;
            }
            randomGenerator = null;
        }

        private bool coreReset;
        private bool isCompleted;
        private PseudorandomBitGenerator randomGenerator;

        private readonly InternalMemoryManager memoryManager;
        private readonly Dictionary<JumpTable, Action> commands;
        private readonly IEnumRegisterField<JumpTable> operation;
        private readonly IFlagRegisterField coreExecuteCommand;
        private readonly RSAServiceProvider rsaServiceProvider;
        private readonly AESServiceProvider aesServiceProvider;
        private readonly MessageAuthenticationServiceProvider msgAuthServiceProvider;

        private class InternalMemoryManager
        {
            public InternalMemoryManager()
            {
                coreMemories = new Dictionary<long, InternalMemoryAccessor>
                {
                    { 0x0, new InternalMemoryAccessor(BERLength, "BER_BE", Endianness.BigEndian) },
                    { 0x1, new InternalMemoryAccessor(MMRLength, "MMR_BE", Endianness.BigEndian) },
                    { 0x2, new InternalMemoryAccessor(TSRLength, "TSR_BE", Endianness.BigEndian) },
                    { 0x3, new InternalMemoryAccessor(FPRLength, "FPR_BE", Endianness.BigEndian) },
                    { 0x8, new InternalMemoryAccessor(BERLength, "BER_LE", Endianness.LittleEndian) },
                    { 0x9, new InternalMemoryAccessor(MMRLength, "MMR_LE", Endianness.LittleEndian) },
                    { 0xA, new InternalMemoryAccessor(TSRLength, "TSR_LE", Endianness.LittleEndian) },
                    { 0xB, new InternalMemoryAccessor(FPRLength, "FPR_LE", Endianness.LittleEndian) }
                };
            }

            public void ResetMemories()
            {
                foreach(KeyValuePair<long, InternalMemoryAccessor> memory in coreMemories)
                {
                    memory.Value.Reset();
                }
            }

            public bool TryReadDoubleWord(long offset, out uint result)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    result = 0;
                    return false;
                }
                result = mem.ReadDoubleWord(internalOffset);
                return true;
            }

            public bool TryReadBytes(long offset, int count, out byte[] result, int endiannessSwapSize = 0)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    result = new byte[0];
                    return false;
                }

                result = mem.ReadBytes(internalOffset, count, endiannessSwapSize).ToArray();
                return true;
            }

            public bool TryWriteDoubleWord(long offset, uint value)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    return false;
                }
                mem.WriteDoubleWord(internalOffset, value);
                return true;
            }

            public bool TryWriteBytes(long offset, byte[] bytes)
            {
                if(!TryAddressInternalMemory(offset, out var mem, out var internalOffset))
                {
                    return false;
                }
                mem.WriteBytes(internalOffset, bytes);
                return true;
            }

            private bool TryAddressInternalMemory(long offset, out InternalMemoryAccessor mem, out long internalMemoryOffset)
            {
                var offsetMask = offset >> OffsetShift;
                if(!coreMemories.TryGetValue(offsetMask, out mem))
                {
                    internalMemoryOffset = 0;
                    Logger.Log(LogLevel.Noisy, "Could not write to internal memory at address 0x{0:X}", offset);
                    return false;
                }
                internalMemoryOffset = offset - (offsetMask << OffsetShift);
                return true;
            }

            private readonly Dictionary<long, InternalMemoryAccessor> coreMemories;

            private const int BERLength = 0x1000;
            private const int MMRLength = 0x1000;
            private const int TSRLength = 0x1000;
            private const int FPRLength = 0x1000;
            private const int OffsetShift = 12;
        }

        private class InternalMemoryAccessor
        {
            public InternalMemoryAccessor(uint size, string name, Endianness endianness)
            {
                this.endianness = endianness;
                internalMemory = new byte[size];
                Name = name;
            }

            public uint ReadDoubleWord(long offset)
            {
                if(offset < 0 || (offset + 4) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to read outside of {0} internal memory, at offset 0x{1:X}", Name, offset);
                    return 0;
                }
                var result = BitHelper.ToUInt32(internalMemory, (int)offset, 4, endianness == Endianness.LittleEndian);
                Logger.Log(LogLevel.Debug, "Read value 0x{0:X} from memory {1} at offset 0x{2:X}", result, Name, offset);
                return result;
            }

            public IEnumerable<byte> ReadBytes(long offset, int count, int endiannessSwapSize = 0)
            {
                if(offset < 0 || (offset + count) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to read {0} bytes outside of {1} internal memory, at offset 0x{2:X}", count, Name, offset);
                    yield return 0;
                }
                if(endiannessSwapSize != 0 && count % endiannessSwapSize != 0)
                {
                    Logger.Log(LogLevel.Error, "Trying to read {0} bytes with an unaligned endianess swap group size of {1}", count, endiannessSwapSize);
                    yield return 0;
                }
                
                if(endiannessSwapSize != 0)
                {
                    for(var i = 0; i < count; i += endiannessSwapSize)
                    {
                        for(var j = endiannessSwapSize - 1; j >= 0; j--)
                        {
                            yield return internalMemory[offset + i + j];
                        }
                    }
                }
                else
                {
                    for(var i = 0; i < count; ++i)
                    {
                        yield return internalMemory[offset + i];
                    }
                }
            }

            public void WriteDoubleWord(long offset, uint value)
            {
                if(offset < 0 || (offset + 4) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to write value 0x{0:X} outside of {1} internal memory, at offset 0x{2:X}", value, Name, offset);
                    return;
                }
                Logger.Log(LogLevel.Debug, "Writing value 0x{0:X} to memory {1} at offset 0x{2:X}", value, Name, offset);

                foreach(var b in BitHelper.GetBytesFromValue(value, sizeof(uint), false))
                {
                    internalMemory[offset] = b;
                    ++offset;
                }
            }

            public void WriteBytes(long offset, byte[] bytes)
            {
                if(offset < 0 || (offset + bytes.Length) >= internalMemory.Length)
                {
                    Logger.Log(LogLevel.Error, "Trying to write {0] bytes outside of {1} internal memory, at offset 0x{2:X}", bytes.Length, Name, offset);
                    return;
                }
                foreach(var b in bytes)
                {
                    internalMemory[offset] = b;
                    ++offset;
                }
            }

            public void Reset()
            {
                for(var i = 0; i < internalMemory.Length; ++i)
                {
                    internalMemory[i] = 0;
                }
            }

            public string Name { get; }

            private readonly Endianness endianness;
            private readonly byte[] internalMemory;
        }

        // This class exposes the functionality of the DRBG core (short for "Deterministic Random Bit Generator"),
        // but because it is simplified, the name is also changed to be more adequate.
        private class PseudorandomBitGenerator
        {
            public PseudorandomBitGenerator(InternalMemoryManager manager)
            {
                this.manager = manager;
                Reset();
            }

            public void Generate()
            {
                manager.TryReadDoubleWord((long)DRBGRegisters.EntropyFactor, out reqLen);
                manager.TryReadDoubleWord((long)DRBGRegisters.ReseedLimit, out var reseedLimit);
                if(reseedLimit == 1 || reseedCounter == 0)
                {
                    Reseed(reseedLimit);
                }
                for(var i = 0; i < reqLen * 4; ++i)
                {
                    manager.TryWriteDoubleWord(
                        (long)DRBGRegisters.ResponseDataAddress + (i * 4),
                        (uint)EmulationManager.Instance.CurrentEmulation.RandomGenerator.Next()
                    );
                }
                if(reseedCounter >= 0)
                {
                    reseedCounter--;
                }
            }

            public void Reset()
            {
                reseedCounter = 0;
                reqLen = 0;
            }

            private void Reseed(uint limit)
            {
                Logger.Log(LogLevel.Noisy, "Requested seed reset.");
                // As a simplification, and to ensure execution determinism, we increment the existing seed by one.
                EmulationManager.Instance.CurrentEmulation.RandomGenerator.ResetSeed(
                    EmulationManager.Instance.CurrentEmulation.RandomGenerator.GetCurrentSeed() + 1
                );
                reseedCounter = limit;
            }

            private uint reseedCounter;
            private uint reqLen;

            private readonly InternalMemoryManager manager;

            private enum DRBGRegisters
            {
                EntropyFactor = 0x8,
                KeyOrdinal = 0xC,
                ReseedLimit = 0x10,
                IsTestInstantiation = 0x14,
                AdditionalInputDataLength = 0x18,
                PersonalizationStringAddress = 0x1C,
                ContextAddress = 0x20,
                AdditionalInputDataAddress = 0x188,
                ResponseDataAddress = 0x8090,
            }
        }

        private class RSAServiceProvider
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

            private BigInteger CreateBigInteger(RSARegisters register, uint wordCount)
            {
                manager.TryReadBytes((long)register + ((wordCount - 1) * 4), 1, out var b);
                // All calculations require positive values, so we are verifying the sign here:
                // if the highest bit of the first byte is set, we effectively add a zero at
                // the beginning of the array, so the data can be interpreted as a positive value.
                var shouldHavePadding = b[0] >= 0x80;
                var wordBytesLength = shouldHavePadding ? wordCount * 4 + 1 : wordCount * 4;
                manager.TryReadBytes((long)register, (int)(wordCount * 4), out var bytesRead, 4);
                if(shouldHavePadding)
                {
                    var bytesReadPadded = new byte[wordBytesLength];
                    Array.Copy(bytesRead, 0, bytesReadPadded, 0, (int)(wordCount * 4));
                    return new BigInteger(bytesReadPadded);
                }
                return new BigInteger(bytesRead);
            }

            private void StoreResultBytes(uint modulusLength, byte[] resultBytes, long baseAddress)
            {
                var modulusByteCount = (int)(modulusLength * WordSize);
                if(resultBytes.Length > modulusByteCount)
                {
                    // BigInteger.ToByteArray might return an array with an extra element
                    // to indicate the sign of resulting value; we need to get rid of it here
                    resultBytes = resultBytes.Take(modulusByteCount).ToArray();
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

        private class AESServiceProvider
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

        private class MessageAuthenticationServiceProvider
        {
            public MessageAuthenticationServiceProvider(InternalMemoryManager manager, SystemBus bus)
            {
                this.manager = manager;
                this.bus = bus;
            }

            public void PerformGCMMessageAuthentication()
            {
                manager.TryReadDoubleWord((long)MsgAuthRegisters.DMAChannelConfig, out var dmaConfig);
                switch((GCMType)dmaConfig)
                {
                    case GCMType.Direct:
                        NonDMAGCM();
                        break;
                    case GCMType.DMA:
                        DMAGCM();
                        break;
                    default:
                        Logger.Log(LogLevel.Warning, "Encountered unexpected DMA configuration: 0x{0:X}", dmaConfig);
                        break;
                }
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
            
            private readonly SystemBus bus;
            private readonly InternalMemoryManager manager;
            
            private enum GCMType
            {
                Direct = 0x0,
                DMA = 0x8
            }

            private enum MsgAuthRegisters
            {
                KeyWordCount = 0x8,
                InputDataByteCount = 0x8,
                AuthDataByteCount = 0x30,
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

        private enum Endianness
        {
            BigEndian,
            LittleEndian
        }
        
        private enum JumpTable
        {
            // gaps in addressing - only a few commands are implemented
            PrecomputeValueRSA = 0x0,
            ModularExponentationRSA = 0x2,
            ModularReductionRSA = 0x12,
            RunAES = 0x20,
            RunAESK = 0x22,
            RunGCM = 0x24,
            InstantiateDRBG = 0x2C,
            GenerateBlocksFromDRBG = 0x30,
            RunAES_DMA = 0x38,
            UninstantiateDRBG = 0x32,
            DecryptCipherRSA = 0x4E,
            RunGCMNew = 0x5A
        }

        private enum Registers : uint
        {
            CSR = 0x7F80
        }
    }
}
